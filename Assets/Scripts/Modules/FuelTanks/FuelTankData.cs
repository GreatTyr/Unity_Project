using System;
[Serializable]
public class FuelTankData : ModuleData
{
    public float capacity;          // Ёмкость бака (количество топлива)
    public float maxTemperature;    // Максимальная температура
    public float heatCapacity;      // Теплоёмкость
    // wallThicknessMm перенесён в базовый класс ModuleData
    public void Initialize(
        ModuleCraftDTO baseDto,
        float capacity,
        float maxTemperature,
        float heatCapacity)
    {
        base.Initialize(baseDto); // Заполняет все базовые поля, включая wallThicknessMm
        this.capacity = capacity;
        this.maxTemperature = maxTemperature;
        this.heatCapacity = heatCapacity;
    }
}
