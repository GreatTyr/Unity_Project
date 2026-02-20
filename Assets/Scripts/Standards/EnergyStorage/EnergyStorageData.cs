using System;

/// <summary>
/// Данные изготовленного модуля хранилища энергии.
/// Наследует общие поля из ModuleData.
/// </summary>
[Serializable]
public class EnergyStorageData : ModuleData
{
    public float energyCapacity;
}