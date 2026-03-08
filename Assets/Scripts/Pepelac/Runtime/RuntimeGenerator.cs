using UnityEngine;

[RequireComponent(typeof(CraftedModule))]
public class RuntimeGenerator : RuntimeModuleBase
{
    private GeneratorData data;
    private float energyProducedLastFrame = 0f;

    private void Start()
    {
        var crafted = GetComponent<CraftedModule>();
        data = crafted.GetData<GeneratorData>();

        if (data == null)
        {
            Debug.LogError($"[RuntimeGenerator] Ошибка: на объекте {name} нет GeneratorData!");
            return;
        }

        MaxTemperature = data.maxTemperature;
        CurrentTemperature = 20f; // Стартовая температура
        IsActive = data.canTurnOnOff ? false : true; // Если нельзя выключать, он всегда включен
    }

    public override void TurnOn()
    {
        if (data != null && data.canTurnOnOff && !IsOverheated)
            IsActive = true;
    }

    public override void TurnOff()
    {
        if (data != null && data.canTurnOnOff)
            IsActive = false;
    }

    public override RuntimeModuleStatus GetStatus()
    {
        if (data == null) return default;

        float dt = Time.fixedDeltaTime;

        return new RuntimeModuleStatus
        {
            moduleType = data.moduleType,
            isActive = IsActive,
            isOverheated = IsOverheated,
            currentTemp = CurrentTemperature,
            maxTemp = MaxTemperature,

            // Сколько топлива нам нужно для работы на 100% мощности в этом кадре?
            fuelDemandThisFrame = IsActive ? data.fuelKgPerS * dt : 0f,
            energyDemandThisFrame = 0f,

            // Выдаем энергию, которую успели сгенерировать в прошлом вызове Tick()
            energyOutputThisFrame = energyProducedLastFrame,
            heatOutputThisFrame = 0f, // Заглушка, добавим позже

            massKg = data.totalMassKg,
            gridPosition = this.GridPosition,
            orientation = this.Orientation,
            priority = this.Priority
        };
    }

    public override void Tick(float dt, float providedFuel, float providedEnergy)
    {
        if (!IsActive || data == null)
        {
            energyProducedLastFrame = 0f;
            return;
        }

        // Высчитываем КПД работы в этом кадре (хватило ли нам топлива?)
        float desiredFuel = data.fuelKgPerS * dt;
        float efficiency = desiredFuel > 0f ? Mathf.Clamp01(providedFuel / desiredFuel) : 0f;

        // Генерируем энергию. Система заберет её у нас в следующем вызове GetStatus()
        energyProducedLastFrame = (data.specificPower * efficiency) * dt;

        // TODO: Логика нагрева
    }
}