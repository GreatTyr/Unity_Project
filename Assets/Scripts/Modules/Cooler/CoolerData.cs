using System;

[Serializable]
public class CoolerData : CommonModuleData
{
    public float coolingRadius;
    public float coolingPower;
    public float energyConsumption;
    public float specificCoolingPower;
    public float specificCoolingPowerBase;
    public float specificEnergyConsumption;
    public float radiusCoefficient;
    public float maxCoolingDifference;
    public float minTemperature;

    public void Initialize(
        CommonModuleCraftData commonData,
        float coolingRadius,
        float coolingPower,
        float energyConsumption,
        float specificCoolingPower,
        float specificCoolingPowerBase,
        float specificEnergyConsumption,
        float radiusCoefficient,
        float maxCoolingDifference,
        float minTemperature)
    {
        InitializeCommon(commonData);

        this.coolingRadius = coolingRadius;
        this.coolingPower = coolingPower;
        this.energyConsumption = energyConsumption;
        this.specificCoolingPower = specificCoolingPower;
        this.specificCoolingPowerBase = specificCoolingPowerBase;
        this.specificEnergyConsumption = specificEnergyConsumption;
        this.radiusCoefficient = radiusCoefficient;
        this.maxCoolingDifference = maxCoolingDifference;
        this.minTemperature = minTemperature;
    }
}