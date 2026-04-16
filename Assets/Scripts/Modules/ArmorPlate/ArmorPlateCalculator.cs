using System;
using UnityEngine;

/// <summary>
/// Чистая математика бронеплиты.
/// Не знает о UI, storage, сцене и крафт-пайплайне.
/// Получает эталон, scaler и alloy-параметры — возвращает итоговые рассчитанные значения.
/// </summary>
public static class ArmorPlateCalculator
{
    [Serializable]
    public struct Result
    {
        // Прочность
        public float durability;

        // Финальные поглощения
        public int kineticAbsorption;
        public int thermalAbsorption;
        public int chemicalAbsorption;
        public int energyAbsorption;

        // Финальные сопротивления
        public float kineticResistance;
        public float thermalResistance;
        public float chemicalResistance;
        public float energyResistance;

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

    public static Result Calculate(
        StandardArmorPlate standard,
        ArmorPlateScaler scaler,
        bool hasDecodedAlloy,
        AlloyCode.AlloyParams alloyParams)
    {
        if (standard == null || scaler == null)
            return default;

        float moduleCoeff = TierCoeffs.Get(standard.ModuleTier);
        int alloyTier = hasDecodedAlloy ? alloyParams.tier : 1;

        Result result = new Result();

        // Прочность
        result.durability = scaler.CalcDurability;

        // Поглощения и сопротивления
        if (hasDecodedAlloy)
        {
            result.kineticAbsorption = CalculateAbsorption(
                alloyParams.kineticAbsorption,
                standard.KineticAbsorptionRelativeBonus,
                standard.KineticAbsorptionAbsoluteBonus);

            result.thermalAbsorption = CalculateAbsorption(
                alloyParams.thermalAbsorption,
                standard.ThermalAbsorptionRelativeBonus,
                standard.ThermalAbsorptionAbsoluteBonus);

            result.chemicalAbsorption = CalculateAbsorption(
                alloyParams.chemicalAbsorption,
                standard.ChemicalAbsorptionRelativeBonus,
                standard.ChemicalAbsorptionAbsoluteBonus);

            result.energyAbsorption = CalculateAbsorption(
                alloyParams.energyAbsorption,
                standard.EnergyAbsorptionRelativeBonus,
                standard.EnergyAbsorptionAbsoluteBonus);

            result.kineticResistance = CalculateResistance(
                alloyParams.kineticResistance,
                standard.KineticResistanceRelativeBonus,
                standard.KineticResistanceAbsoluteBonus);

            result.thermalResistance = CalculateResistance(
                alloyParams.thermalResistance,
                standard.ThermalResistanceRelativeBonus,
                standard.ThermalResistanceAbsoluteBonus);

            result.chemicalResistance = CalculateResistance(
                alloyParams.chemicalResistance,
                standard.ChemicalResistanceRelativeBonus,
                standard.ChemicalResistanceAbsoluteBonus);

            result.energyResistance = CalculateResistance(
                alloyParams.energyResistance,
                standard.EnergyResistanceRelativeBonus,
                standard.EnergyResistanceAbsoluteBonus);
        }
        else
        {
            result.kineticAbsorption = 0;
            result.thermalAbsorption = 0;
            result.chemicalAbsorption = 0;
            result.energyAbsorption = 0;

            result.kineticResistance = 0f;
            result.thermalResistance = 0f;
            result.chemicalResistance = 0f;
            result.energyResistance = 0f;
        }

        // Общие тепловые параметры
        result.heatCapacity = Round1(scaler.CalcVolume * standard.HeatCapacityCoeff * moduleCoeff);
        result.maxTemperature = Round1(BaseMaxTemperature + result.thermalAbsorption);
        result.wallThicknessMm = scaler.CalcWallThicknessMm;

        float thermalResistanceFactor = Mathf.Max(0f, 1f - (result.thermalResistance / 100f));
        result.heatingRate = Round2(standard.BaseHeating * thermalResistanceFactor);

        CalculateOperationalUsage(standard, scaler, ref result);
        CalculateStaticCapacity(standard, scaler, ref result);

        return result;
    }

    private static int CalculateAbsorption(int baseValue, float relativeBonus, int absoluteBonus)
    {
        float result = (baseValue * relativeBonus) + absoluteBonus;
        return Mathf.RoundToInt(result);
    }

    private static float CalculateResistance(float baseValue, float relativeBonus, float absoluteBonus)
    {
        float result = (baseValue * relativeBonus) + absoluteBonus;
        return Round1(result);
    }

    private static void CalculateOperationalUsage(StandardArmorPlate standard, ArmorPlateScaler scaler, ref Result result)
    {
        // У бронеплиты нет operational usage — заглушка
        result.operationalResourceUsagePerSecond = Array.Empty<OperationalResourceUsagePerSecond>();
        result.operationalResourceUsageSummary = "—";
    }

    private static void CalculateStaticCapacity(StandardArmorPlate standard, ArmorPlateScaler scaler, ref Result result)
    {
        // У бронеплиты нет статической ёмкости — MVP-заглушка
        result.staticCapacityMax = 0f;
        result.staticCapacityCurrent = 0f;
        result.staticCapacityDrainPerSecond = 0f;
    }

    private static float Round1(float value) => (float)Math.Round(value, 1);
    private static float Round2(float value) => (float)Math.Round(value, 2);
}