using System;

[Serializable]
public class FuelTankData : CommonModuleData
{
    public float capacity;

    public void Initialize(
        CommonModuleCraftData commonData,
        float capacity)
    {
        InitializeCommon(commonData);
        this.capacity = capacity;
    }
}