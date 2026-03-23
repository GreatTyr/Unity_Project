using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

/// <summary>
/// „иста€ математика топливного бака.
/// FuelTank Ч special-case:
/// - нет внутреннего fill-содержимого как у обычного модул€;
/// - нет обычного internal-resource pipeline;
/// - heating rate по текущему правилу = 0.
/// </summary>
public static class FuelTankCalculator
{
    [Serializable]
    public struct Result
    {
        // —пецифичный параметр бака
        public float capacity;

        // ќбщие параметры
        public float heatCapacity;
        public float maxTemperature;
        public float wallThicknessMm;
        public float heatingRate;

        // Ќовые общие параметры (MVP)
        public OperationalResourceUsagePerSecond[] operationalResourceUsagePerSecond;
        public string operationalResourceUsageSummary;

        public float staticCapacityMax;
        public float staticCapacityCurrent;
        public float staticCapacityDrainPerSecond;
    }

    private const float BaseMaxTemperature = 300f;

    public static Result Calculate(
        StandardFuelTank standard,
        ModuleScaler scaler,
        bool hasDecodedAlloy,
        AlloyCode.AlloyParams alloyParams)
    {
        if (standard == null || scaler == null)
            return default;

        float effectiveVolumeDm3 = scaler.CalcEffectiveVolume * 1000f;
        float moduleCoeff = TierCoeffs.Get(standard.ModuleTier);

        int thermalAbsorption = hasDecodedAlloy ? alloyParams.thermalAbsorption : 0;

        Result result = new Result
        {
            capacity = Round3(effectiveVolumeDm3 * moduleCoeff * standard.CapacityCoefficient),
            heatCapacity = Round1(scaler.CalcRealVolume * standard.HeatCapacityCoeff * moduleCoeff),
            maxTemperature = Round1(BaseMaxTemperature + thermalAbsorption),
            wallThicknessMm = scaler.CalcWallThicknessMm,

            // ѕо твоему правилу дл€ бака нагрев = 0
            heatingRate = 0f
        };

        CalculateOperationalUsage(standard, scaler, ref result);
        CalculateStaticCapacity(standard, scaler, ref result);

        return result;
    }

    private static void CalculateOperationalUsage(StandardFuelTank standard, ModuleScaler scaler, ref Result result)
    {
        if (standard.OperationalResourceCostsPerLiterPerSecond == null ||
            standard.OperationalResourceCostsPerLiterPerSecond.Count == 0)
        {
            result.operationalResourceUsagePerSecond = Array.Empty<OperationalResourceUsagePerSecond>();
            result.operationalResourceUsageSummary = "Ч";
            return;
        }

        float effectiveVolumeLiters = scaler.CalcEffectiveVolume * 1000f;
        List<OperationalResourceUsagePerSecond> usage = new List<OperationalResourceUsagePerSecond>();
        StringBuilder sb = new StringBuilder();

        for (int i = 0; i < standard.OperationalResourceCostsPerLiterPerSecond.Count; i++)
        {
            var entry = standard.OperationalResourceCostsPerLiterPerSecond[i];
            float gramsPerSecond = entry.gramsPerLiterPerSecond * effectiveVolumeLiters;

            OperationalResourceUsagePerSecond finalEntry = new OperationalResourceUsagePerSecond
            {
                resourceType = entry.resourceType,
                gramsPerSecond = Round3(gramsPerSecond)
            };

            usage.Add(finalEntry);

            if (sb.Length > 0)
                sb.Append(" | ");

            if (finalEntry.gramsPerSecond >= 1000f)
            {
                sb.Append($"{ResourcesStorage.ResourceName(finalEntry.resourceType)}: {(finalEntry.gramsPerSecond / 1000f):F3} кг/с");
            }
            else
            {
                sb.Append($"{ResourcesStorage.ResourceName(finalEntry.resourceType)}: {finalEntry.gramsPerSecond:F3} г/с");
            }
        }

        result.operationalResourceUsagePerSecond = usage.ToArray();
        result.operationalResourceUsageSummary = sb.Length > 0 ? sb.ToString() : "Ч";
    }

    private static void CalculateStaticCapacity(StandardFuelTank standard, ModuleScaler scaler, ref Result result)
    {
        float aabbVolume = scaler.CalcAABBVolume;
        float tierPercent = standard.ModuleTier * 0.01f;

        result.staticCapacityMax = Round1(aabbVolume * 1000f * standard.StaticCapacityCoefficient);
        result.staticCapacityCurrent = 0f;
        result.staticCapacityDrainPerSecond = Round3(tierPercent * standard.GroundingCoefficient * aabbVolume);
    }

    private static float Round1(float value) => (float)Math.Round(value, 1);
    private static float Round3(float value) => (float)Math.Round(value, 3);
}