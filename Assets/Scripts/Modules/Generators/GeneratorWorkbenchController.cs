using System;
using UnityEngine;
/// <summary>
/// Контроллер Верстака Генераторов.
/// Специфичная логика: мощность, топливо, ёмкость, нагрев.
/// Толщина стенок вычисляется в ModuleScaler (единая формула для всех модулей).
/// </summary>
public class GeneratorWorkbenchController
    : BaseModuleWorkbenchController<StandardGenerator, GeneratorData, GeneratorDatabase>
{
    // Специфичные расчёты генератора (для UI)
    public float CalcSpecificPower { get; private set; }
    public float CalcFuelKgPerS { get; private set; }
    public float CalcHeatCapacity { get; private set; }
    public float CalcMaxTemperature { get; private set; }
    public float CalcHeatingRate { get; private set; }
    public float CalcEnergyCapacity { get; private set; }
    // УДАЛЕНО: CalcWallThicknessMm — теперь в Scaler.CalcWallThicknessMm
    private float calcPowerTimesTierPer0001;
    private float calcFuelPer0001m3Tiered;
    protected override string ModuleTypeName => StandardGenerator.TYPE_GENERATOR;
    protected override float GetExplosionPowerSource() => CalcSpecificPower;
    protected override void CalculateSpecificOutputs()
    {
        float effectiveVolumeDm3 = Scaler.CalcEffectiveVolume * 1000f;
        float moduleCoeff = TierCoeffs.Get(SelectedRef.ModuleTier);
        // Мощность
        double powerTier = (double)SelectedRef.PowerBy0001m3 * moduleCoeff;
        calcPowerTimesTierPer0001 = (float)Math.Round(powerTier, 3);
        CalcSpecificPower = (float)Math.Round(powerTier * effectiveVolumeDm3, 3);
        // Топливо
        float fuelTierCoeff = TierCoeffs.Get(SelectedRef.FuelTier);
        double rawFuelPer0001D = fuelTierCoeff > 0f
            ? (double)SelectedRef.FuelBy0001m3_Base / fuelTierCoeff : 0.0;
        if (rawFuelPer0001D <= 0.0) rawFuelPer0001D = 1e-6;
        calcFuelPer0001m3Tiered = (float)Math.Round(rawFuelPer0001D, 6);
        double totalFuelD = rawFuelPer0001D * effectiveVolumeDm3;
        CalcFuelKgPerS = (float)Math.Round(Math.Max(totalFuelD, 0.0001), 4);
        // Ёмкость
        CalcEnergyCapacity = (float)Math.Round(
            effectiveVolumeDm3 * moduleCoeff * SelectedRef.CapacityCoefficient, 3);
        // Тепло
        CalcHeatCapacity = (float)Math.Round(
            Scaler.CalcRealVolume * SelectedRef.HeatCapacityCoeff * moduleCoeff, 1);
        int thermAbsorb = IsAlloyDecoded ? AlloyParams.thermalAbsorption : 0;
        CalcMaxTemperature = 300f + thermAbsorb;
        // УДАЛЕНО: CalcWallThicknessMm — теперь вычисляется в ModuleScaler.CalculateWallThickness()
        float thermResist = IsAlloyDecoded ? AlloyParams.thermalResistance : 0f;
        CalcHeatingRate = (float)Math.Round(
            SelectedRef.BaseHeating * Mathf.Max(0f, 1f - (thermResist / 100f)), 2);
    }
    protected override string BuildSecondCodeLine()
    {
        return $"P{FormatF(CalcSpecificPower, 3)}-F{FormatF(CalcFuelKgPerS, 4)}-FT{SelectedRef.FuelTier}";
    }
    protected override GeneratorData CreateModuleData(ModuleCraftDTO dto)
    {
        var data = new GeneratorData();
        data.Initialize(dto, CalcSpecificPower, CalcFuelKgPerS, SelectedRef.FuelTier,
            calcPowerTimesTierPer0001, calcFuelPer0001m3Tiered,
            SelectedRef.PowerBy0001m3, SelectedRef.FuelBy0001m3_Base,
            CalcMaxTemperature, CalcEnergyCapacity);
        return data;
    }
    protected override RuntimeModuleBase AddRuntimeComponent(GameObject obj)
    {
        return obj.AddComponent<RuntimeGenerator>();
    }
}