using System;
using UnityEngine;

/// <summary>
/// Контроллер Верстака Охлаждающих Радиаторов.
/// Специфичная логика: охлаждение, радиус, энергопотребление.
/// </summary>
public class CoolerWorkbenchController
    : BaseModuleWorkbenchController<StandardCooler, CoolerData, CoolerDatabase>
{
    // Специфичные расчёты кулера (для UI)
    public float CalcCoolingPower { get; private set; }
    public float CalcEnergyConsumption { get; private set; }
    public float CalcCoolingRadius { get; private set; }
    public float CalcSpecificCoolingPower { get; private set; }
    public float CalcHeatCapacity { get; private set; }
    public float CalcMaxTemperature { get; private set; }
    public float CalcWallThicknessMm { get; private set; }
    public float CalcHeatingRate { get; private set; }
    public float CalcMaxCoolingDifference { get; private set; }
    public float CalcMinTemperature { get; private set; }

    protected override string ModuleTypeName => StandardCooler.TYPE_COOLER;

    protected override float GetExplosionPowerSource() => CalcCoolingPower;

    protected override void CalculateSpecificOutputs()
    {
        float effectiveVolumeDm3 = Scaler.CalcEffectiveVolume * 1000f;
        float moduleCoeff = TierCoeffs.Get(SelectedRef.ModuleTier);

        // Удельная охлаждающая способность (с учётом тира)
        CalcSpecificCoolingPower = (float)Math.Round(
            (double)SelectedRef.SpecificCoolingPowerBase * moduleCoeff, 3);

        // Охлаждающая способность
        CalcCoolingPower = (float)Math.Round(
            (double)CalcSpecificCoolingPower * effectiveVolumeDm3, 3);

        // Энергопотребление
        CalcEnergyConsumption = (float)Math.Round(
            (double)SelectedRef.SpecificEnergyConsumption * effectiveVolumeDm3, 3);

        // Радиус области действия
        CalcCoolingRadius = (float)Math.Round(
            ((double)Scaler.CalcLength + Scaler.CalcWidth) / 2.0 * SelectedRef.RadiusCoefficient, 3);

        // Максимальная разница охлаждения
        CalcMaxCoolingDifference = (float)Math.Round(30.0 * moduleCoeff, 1);

        // Минимальная температура
        CalcMinTemperature = (float)Math.Round(-20.0 * SelectedRef.ModuleTier, 1);

        // Тепло
        CalcHeatCapacity = (float)Math.Round(
            Scaler.CalcRealVolume * SelectedRef.HeatCapacityCoeff * moduleCoeff, 1);
        int thermAbsorb = IsAlloyDecoded ? AlloyParams.thermalAbsorption : 0;
        CalcMaxTemperature = 300f + thermAbsorb;

        // Толщина стенок — из общего точного расчёта ModuleScaler
        CalcWallThicknessMm = Scaler.CalcWallThicknessMm;

        float thermResist = IsAlloyDecoded ? AlloyParams.thermalResistance : 0f;
        CalcHeatingRate = (float)Math.Round(
            SelectedRef.BaseHeating * Mathf.Max(0f, 1f - (thermResist / 100f)), 2);
    }

    protected override string BuildSecondCodeLine()
    {
        return $"C{FormatF(CalcCoolingPower, 3)}-R{FormatF(CalcCoolingRadius, 3)}-E{FormatF(CalcEnergyConsumption, 3)}";
    }

    protected override CoolerData CreateModuleData(ModuleCraftDTO dto)
    {
        var data = new CoolerData();
        data.Initialize(dto,
            CalcCoolingRadius,
            CalcCoolingPower,
            CalcEnergyConsumption,
            CalcSpecificCoolingPower,
            SelectedRef.SpecificCoolingPowerBase,
            SelectedRef.SpecificEnergyConsumption,
            SelectedRef.RadiusCoefficient,
            CalcMaxTemperature,
            CalcMaxCoolingDifference,
            CalcMinTemperature);
        return data;
    }

    protected override RuntimeModuleBase AddRuntimeComponent(GameObject obj)
    {
        return obj.AddComponent<RuntimeCooler>();
    }
}