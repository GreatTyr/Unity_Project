using System;

[Serializable]
public class CoolerData : ModuleData
{
    public float coolingRadius;
    public float coolingPower;
    public float energyConsumption;
    public float specificCoolingPower;
    public float specificCoolingPowerBase;
    public float specificEnergyConsumption;
    public float radiusCoefficient;
    public float maxTemperature;
    public float maxCoolingDifference;
    public float minTemperature;

    public void Initialize(
        ModuleCraftDTO baseDto,
        float coolingRadius,
        float coolingPower,
        float energyConsumption,
        float specificCoolingPower,
        float specificCoolingPowerBase,
        float specificEnergyConsumption,
        float radiusCoefficient,
        float maxTemperature,
        float maxCoolingDifference,
        float minTemperature)
    {
        base.Initialize(baseDto);
        this.coolingRadius = coolingRadius;
        this.coolingPower = coolingPower;
        this.energyConsumption = energyConsumption;
        this.specificCoolingPower = specificCoolingPower;
        this.specificCoolingPowerBase = specificCoolingPowerBase;
        this.specificEnergyConsumption = specificEnergyConsumption;
        this.radiusCoefficient = radiusCoefficient;
        this.maxTemperature = maxTemperature;
        this.maxCoolingDifference = maxCoolingDifference;
        this.minTemperature = minTemperature;
    }
}