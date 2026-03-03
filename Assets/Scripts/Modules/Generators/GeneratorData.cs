using System;

[Serializable]
public class GeneratorData : ModuleData
{
    public float specificPower;
    public float fuelKgPerS;
    public int fuelTier;
    public float powerTimesTierPer0001;
    public float fuelPer0001m3Tiered;
    public float powerBy0001m3;
    public float fuelBy0001m3Base;

    public float maxTemperature; // Сохраняем лимит температуры
    public float energyCapacity; // Новая емкость

    public void Initialize(
        ModuleCraftDTO baseDto,
        float specificPower,
        float fuelKgPerS,
        int fuelTier,
        float powerTimesTierPer0001,
        float fuelPer0001m3Tiered,
        float powerBy0001m3,
        float fuelBy0001m3Base,
        float maxTemperature,
        float energyCapacity)
    {
        base.Initialize(baseDto);
        this.specificPower = specificPower;
        this.fuelKgPerS = fuelKgPerS;
        this.fuelTier = fuelTier;
        this.powerTimesTierPer0001 = powerTimesTierPer0001;
        this.fuelPer0001m3Tiered = fuelPer0001m3Tiered;
        this.powerBy0001m3 = powerBy0001m3;
        this.fuelBy0001m3Base = fuelBy0001m3Base;
        this.maxTemperature = maxTemperature;
        this.energyCapacity = energyCapacity;
    }
}