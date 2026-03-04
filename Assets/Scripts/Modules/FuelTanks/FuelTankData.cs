using System;

[Serializable]
public class FuelTankData : ModuleData
{
    public float capacity;          // Ёмкость бака (количество топлива)
    public float maxTemperature;    // Максимальная температура
    public float heatCapacity;      // Теплоёмкость
    public float wallThicknessMm;   // Толщина стенок

    public void Initialize(
        ModuleCraftDTO baseDto,
        float capacity,
        float maxTemperature,
        float heatCapacity,
        float wallThicknessMm)
    {
        base.Initialize(baseDto);
        this.capacity = capacity;
        this.maxTemperature = maxTemperature;
        this.heatCapacity = heatCapacity;
        this.wallThicknessMm = wallThicknessMm;
    }
}