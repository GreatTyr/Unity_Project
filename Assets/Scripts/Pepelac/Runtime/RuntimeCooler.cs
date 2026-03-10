using UnityEngine;

[RequireComponent(typeof(CraftedModule))]
public class RuntimeCooler : RuntimeModuleBase
{
    private CoolerData data;
    private float coolingProvidedLastFrame = 0f;

    private void Start()
    {
        var crafted = GetComponent<CraftedModule>();
        data = crafted.GetData<CoolerData>();

        if (data == null)
        {
            Debug.LogError($"[RuntimeCooler] Ошибка: на объекте {name} нет CoolerData!");
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

            // Кулер не потребляет топливо
            fuelDemandThisFrame = 0f,
            // Сколько энергии нужно для работы на 100% мощности в этом кадре?
            energyDemandThisFrame = IsActive ? data.energyConsumption * dt : 0f,

            // Кулер не производит энергию
            energyOutputThisFrame = 0f,
            // Выдаём охлаждение, которое рассчитали в прошлом вызове Tick()
            heatOutputThisFrame = -coolingProvidedLastFrame,

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
            coolingProvidedLastFrame = 0f;
            return;
        }

        // Высчитываем КПД работы в этом кадре (хватило ли энергии?)
        float desiredEnergy = data.energyConsumption * dt;
        float efficiency = desiredEnergy > 0f ? Mathf.Clamp01(providedEnergy / desiredEnergy) : 0f;

        // Рассчитываем охлаждение. Система заберёт его у нас в следующем вызове GetStatus()
        coolingProvidedLastFrame = (data.coolingPower * efficiency) * dt;

        // TODO: Логика нагрева
    }

    /// <summary>Радиус области действия охлаждения (м).</summary>
    public float CoolingRadius => data != null ? data.coolingRadius : 0f;
}