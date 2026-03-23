using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

/// <summary>
/// Чистая математика кулера.
/// Не знает о UI, storage, сцене и крафт-пайплайне.
/// Получает эталон, scaler и alloy-параметры — возвращает итоговые рассчитанные значения.
/// </summary>
public static class CoolerCalculator
{
    [Serializable]
    public struct Result
    {
        // Специфичные параметры кулера
        public float specificCoolingPower;
        public float coolingPower;
        public float energyConsumption;
        public float coolingRadius;
        public float maxCoolingDifference;
        public float minTemperature;

        // Общие параметры
        public float heatCapacity;
        public float maxTemperature;
        public float wallThicknessMm;
        public float heatingRate;

        // Новые общие параметры (MVP)
        public OperationalResourceUsagePerSecond[] operationalResourceUsagePerSecond;
        public string operationalResourceUsageSummary;

        public float staticCapacityMax;
        public float staticCapacityCurrent;
        public float staticCapacityDrainPerSecond;
    }

    private const float BaseMaxTemperature = 300f;
    private const float MinTemperatureStart = 20f;
    private const float MinTemperatureStepPerTier = 15f;

    public static Result Calculate(
        StandardCooler standard,
        ModuleScaler scaler,
        bool hasDecodedAlloy,
        AlloyCode.AlloyParams alloyParams)
    {
        if (standard == null || scaler == null)
            return default;

        float effectiveVolumeDm3 = scaler.CalcEffectiveVolume * 1000f;
        float moduleCoeff = TierCoeffs.Get(standard.ModuleTier);

        int thermalAbsorption = hasDecodedAlloy ? alloyParams.thermalAbsorption : 0;
        float thermalResistance = hasDecodedAlloy ? alloyParams.thermalResistance : 0f;

        Result result = new Result
        {
            // Кулер-специфичное
            specificCoolingPower = Round3(standard.SpecificCoolingPowerBase * moduleCoeff),
            coolingPower = Round3((standard.SpecificCoolingPowerBase * moduleCoeff) * effectiveVolumeDm3),
            energyConsumption = Round3(standard.SpecificEnergyConsumption * effectiveVolumeDm3),
            coolingRadius = Round3(((scaler.CalcLength + scaler.CalcWidth) * 0.5f) * standard.RadiusCoefficient),
            maxCoolingDifference = Round1(30f * moduleCoeff),

            // T1 = +20, далее -15 за каждый следующий тир
            minTemperature = Round1(MinTemperatureStart - ((standard.ModuleTier - 1) * MinTemperatureStepPerTier)),

            // Общие тепловые параметры
            heatCapacity = Round1(scaler.CalcRealVolume * standard.HeatCapacityCoeff * moduleCoeff),
            maxTemperature = Round1(BaseMaxTemperature + thermalAbsorption),
            wallThicknessMm = scaler.CalcWallThicknessMm,
            heatingRate = Round2(standard.BaseHeating * Mathf.Max(0f, 1f - (thermalResistance / 100f)))
        };

        CalculateOperationalUsage(standard, scaler, ref result);
        CalculateStaticCapacity(standard, scaler, ref result);

        return result;
    }

    private static void CalculateOperationalUsage(StandardCooler standard, ModuleScaler scaler, ref Result result)
    {
        if (standard.OperationalResourceCostsPerLiterPerSecond == null ||
            standard.OperationalResourceCostsPerLiterPerSecond.Count == 0)
        {
            result.operationalResourceUsagePerSecond = Array.Empty<OperationalResourceUsagePerSecond>();
            result.operationalResourceUsageSummary = "—";
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
        result.operationalResourceUsageSummary = sb.Length > 0 ? sb.ToString() : "—";
    }

    private static void CalculateStaticCapacity(StandardCooler standard, ModuleScaler scaler, ref Result result)
    {
        // MVP-формула:
        // max = AABBVolume * 1000 * coefficient
        // current = 0 (стартовое накопление)
        // drain = tierPercent * grounding * AABBVolume
        //
        // Где tierPercent:
        // T1 = 1%, T10 = 10%

        float aabbVolume = scaler.CalcAABBVolume;
        float tierPercent = standard.ModuleTier * 0.01f;

        result.staticCapacityMax = Round1(aabbVolume * 1000f * standard.StaticCapacityCoefficient);
        result.staticCapacityCurrent = 0f;
        result.staticCapacityDrainPerSecond = Round3(tierPercent * standard.GroundingCoefficient * aabbVolume);
    }

    private static float Round1(float value) => (float)Math.Round(value, 1);
    private static float Round2(float value) => (float)Math.Round(value, 2);
    private static float Round3(float value) => (float)Math.Round(value, 3);
}