using UnityEngine;

[RequireComponent(typeof(CraftedModule))]
public class RuntimeFuelTank : RuntimeModuleBase
{
    private FuelTankData data;
    public float CurrentFuel { get; private set; }

    private void Start()
    {
        data = GetComponent<CraftedModule>().GetData<FuelTankData>();

        if (data != null)
        {
            // Для теста бак спавнится полным. Позже сделаем его пустым.
            CurrentFuel = data.capacity;
        }
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

            currentFuelStorage = CurrentFuel,
            maxFuelStorage = data.capacity,

            massKg = data.totalMassKg,
            gridPosition = this.GridPosition,
            orientation = this.Orientation,
            priority = this.Priority
        };
    }

    public override void Tick(float dt, float providedFuel, float providedEnergy)
    {
        // Бак не делает ничего каждый кадр
    }

    /// <summary>
    /// PepelacSystems вызывает этот метод, когда забирает топливо для генераторов
    /// </summary>
    public float ConsumeFuel(float amount)
    {
        float consumed = Mathf.Min(amount, CurrentFuel);
        CurrentFuel -= consumed;
        return consumed;
    }
}