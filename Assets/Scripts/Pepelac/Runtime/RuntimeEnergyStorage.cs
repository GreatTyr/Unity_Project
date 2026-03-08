using UnityEngine;

[RequireComponent(typeof(CraftedModule))]
public class RuntimeEnergyStorage : RuntimeModuleBase
{
    private EnergyStorageData data;
    public float CurrentCharge { get; private set; }

    private void Start()
    {
        data = GetComponent<CraftedModule>().GetData<EnergyStorageData>();
        IsActive = true;
    }

    public override void TurnOn() { }
    public override void TurnOff() { }

    public override RuntimeModuleStatus GetStatus()
    {
        if (data == null) return default;

        return new RuntimeModuleStatus
        {
            moduleType = data.moduleType,
            isActive = IsActive,

            // Батарея "просит" энергию, чтобы заполнить себя до максимума
            energyDemandThisFrame = data.energyCapacity - CurrentCharge,
            fuelDemandThisFrame = 0f,

            currentEnergyStorage = CurrentCharge,
            maxEnergyStorage = data.energyCapacity,

            massKg = data.totalMassKg,
            gridPosition = this.GridPosition,
            orientation = this.Orientation,
            priority = this.Priority
        };
    }

    public override void Tick(float dt, float providedFuel, float providedEnergy)
    {
        if (data == null) return;

        // Принимаем энергию, которую нам выделила система (PepelacSystems)
        CurrentCharge += providedEnergy;
        if (CurrentCharge > data.energyCapacity)
            CurrentCharge = data.energyCapacity;
    }

    // Будущий метод: если системе не хватает выработки от генераторов, 
    // она будет брать остатки энергии из батареи через этот метод
    public float ConsumeEnergy(float amount)
    {
        float consumed = Mathf.Min(amount, CurrentCharge);
        CurrentCharge -= consumed;
        return consumed;
    }
}