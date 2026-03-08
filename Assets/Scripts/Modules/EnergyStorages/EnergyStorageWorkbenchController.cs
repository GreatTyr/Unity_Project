using System;
using UnityEngine;

/// <summary>
/// Контроллер Верстака Хранилищ Энергии.
/// Специфичная логика: ёмкость.
/// </summary>
public class EnergyStorageWorkbenchController
    : BaseModuleWorkbenchController<StandardEnergyStorage, EnergyStorageData, EnergyStorageDatabase>
{
    public float CalcEnergyCapacity { get; private set; }

    protected override string ModuleTypeName => StandardEnergyStorage.TYPE_ENERGY_STORAGE;

    protected override float GetExplosionPowerSource() => CalcEnergyCapacity;

    protected override void CalculateSpecificOutputs()
    {
        float effectiveVolumeDm3 = Scaler.CalcEffectiveVolume * 1000f;
        float moduleCoeff = TierCoeffs.Get(SelectedRef.ModuleTier);

        CalcEnergyCapacity = (float)Math.Round(
            effectiveVolumeDm3 * moduleCoeff * SelectedRef.CapacityCoefficient, 3);
    }

    protected override string BuildSecondCodeLine()
    {
        return $"C{FormatF(CalcEnergyCapacity, 3)}";
    }

    protected override EnergyStorageData CreateModuleData(ModuleCraftDTO dto)
    {
        var data = new EnergyStorageData();
        data.Initialize(dto, CalcEnergyCapacity);
        return data;
    }

    protected override RuntimeModuleBase AddRuntimeComponent(GameObject obj)
    {
        return obj.AddComponent<RuntimeEnergyStorage>();
    }
}