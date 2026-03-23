using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

/// <summary>
/// Чистая математика генератора.
/// Не знает о UI, storage, сцене и крафт-пайплайне.
/// Получает эталон, scaler и alloy-параметры — возвращает итоговые рассчитанные значения.
/// </summary>
public static class GeneratorCalculator
{
    [Serializable]
    public struct Result
    {
        // Специфичные параметры генератора
        public float specificPower;
        public float fuelKgPerS;
        public int fuelTier;
        public float powerTimesTierPer0001;
        public float fuelPer0001m3Tiered;
        public float powerBy0001m3;
        public float fuelBy0001m3Base;
        public float energyCapacity;

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
    private const double MinFuelPer0001 = 1e-6;
    private const float MinFuelDisplayTotal = 0.0001f;

    public static Result Calculate(
        StandardGenerator standard,
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
            fuelTier = standard.FuelTier,
            powerBy0001m3 = standard.PowerBy0001m3,
            fuelBy0001m3Base = standard.FuelBy0001m3_Base
        };

        // Мощность
        double rawPowerTier = (double)standard.PowerBy0001m3 * moduleCoeff;
        result.powerTimesTierPer0001 = Round3((float)rawPowerTier);
        result.specificPower = Round3((float)(rawPowerTier * effectiveVolumeDm3));

        // Топливо
        float fuelTierCoeff = TierCoeffs.Get(standard.FuelTier);
        double rawFuelPer0001 = fuelTierCoeff > 0f
            ? (double)standard.FuelBy0001m3_Base / fuelTierCoeff
            : 0.0;

        if (rawFuelPer0001 <= 0.0)
            rawFuelPer0001 = MinFuelPer0001;

        result.fuelPer0001m3Tiered = Round6((float)rawFuelPer0001);

        double totalFuel = rawFuelPer0001 * effectiveVolumeDm3;
        float finalFuel = (float)Math.Max(totalFuel, MinFuelDisplayTotal);
        result.fuelKgPerS = Round4(finalFuel);

        // Ёмкость
        result.energyCapacity = Round3(effectiveVolumeDm3 * moduleCoeff * standard.CapacityCoefficient);

        // Общие тепловые параметры
        result.heatCapacity = Round1(scaler.CalcRealVolume * standard.HeatCapacityCoeff * moduleCoeff);
        result.maxTemperature = Round1(BaseMaxTemperature + thermalAbsorption);
        result.wallThicknessMm = scaler.CalcWallThicknessMm;
        result.heatingRate = Round2(standard.BaseHeating * Mathf.Max(0f, 1f - (thermalResistance / 100f)));

        CalculateOperationalUsage(standard, scaler, ref result);
        CalculateStaticCapacity(standard, scaler, ref result);

        return result;
    }

    private static void CalculateOperationalUsage(StandardGenerator standard, ModuleScaler scaler, ref Result result)
    {
        // Пока у генератора отдельной operational-таблицы нет — используем заглушку.
        result.operationalResourceUsagePerSecond = Array.Empty<OperationalResourceUsagePerSecond>();
        result.operationalResourceUsageSummary = "—";
    }

    private static void CalculateStaticCapacity(StandardGenerator standard, ModuleScaler scaler, ref Result result)
    {
        // Пока отдельной настройки статической ёмкости у генератора нет — MVP-заглушка.
        result.staticCapacityMax = 0f;
        result.staticCapacityCurrent = 0f;
        result.staticCapacityDrainPerSecond = 0f;
    }

    private static float Round1(float value) => (float)Math.Round(value, 1);
    private static float Round2(float value) => (float)Math.Round(value, 2);
    private static float Round3(float value) => (float)Math.Round(value, 3);
    private static float Round4(float value) => (float)Math.Round(value, 4);
    private static float Round6(float value) => (float)Math.Round(value, 6);
}