using System;

[Serializable]
public class GeneratorData : CommonModuleData
{
    public float specificPower;
    public float fuelKgPerS;
    public int fuelTier;
    public float powerTimesTierPer0001;
    public float fuelPer0001m3Tiered;
    public float powerBy0001m3;
    public float fuelBy0001m3Base;
    public float energyCapacity;

    public void Initialize(
        CommonModuleCraftData commonData,
        float specificPower,
        float fuelKgPerS,
        int fuelTier,
        float powerTimesTierPer0001,
        float fuelPer0001m3Tiered,
        float powerBy0001m3,
        float fuelBy0001m3Base,
        float energyCapacity)
    {
        InitializeCommon(commonData);

        this.specificPower = specificPower;
        this.fuelKgPerS = fuelKgPerS;
        this.fuelTier = fuelTier;
        this.powerTimesTierPer0001 = powerTimesTierPer0001;
        this.fuelPer0001m3Tiered = fuelPer0001m3Tiered;
        this.powerBy0001m3 = powerBy0001m3;
        this.fuelBy0001m3Base = fuelBy0001m3Base;
        this.energyCapacity = energyCapacity;
    }
}