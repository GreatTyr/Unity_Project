using System;

/// <summary>
/// Данные изготовленного модуля хранилища энергии.
/// Наследует общие поля из ModuleData.
/// </summary>
[Serializable]
public class EnergyStorageData : ModuleData
{
    public float energyCapacity;

    /// <summary>
    /// Инициализация базовых параметров через DTO и специфичных параметров EnergyStorage.
    /// </summary>
    public void Initialize(ModuleCraftDTO baseDto, float energyCapacity)
    {
        base.Initialize(baseDto); // Вызов заполнения базовых полей
        this.energyCapacity = energyCapacity;
    }
}