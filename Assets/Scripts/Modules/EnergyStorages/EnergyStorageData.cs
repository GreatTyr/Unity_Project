using System;

[Serializable]
public class EnergyStorageData : CommonModuleData
{
    public float energyCapacity;

    public void Initialize(
        CommonModuleCraftData commonData,
        float energyCapacity)
    {
        InitializeCommon(commonData);
        this.energyCapacity = energyCapacity;
    }
}