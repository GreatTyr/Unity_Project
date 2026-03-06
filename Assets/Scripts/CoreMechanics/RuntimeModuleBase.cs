using UnityEngine;

public enum ModuleOrientation { Deg0, Deg90, Deg180, Deg270 }

public struct RuntimeModuleStatus
{
    public string moduleType;
    public bool isActive;
    public bool isOverheated;
    public float currentTemp;
    public float maxTemp;

    // ЗАПРОСЫ (Сколько ресурсов нужно в ЭТОТ кадр)
    public float fuelDemandThisFrame;
    public float energyDemandThisFrame;

    // ВЫДАЧА (Сколько произведено в ЭТОТ кадр)
    public float energyOutputThisFrame;
    public float heatOutputThisFrame;
    public float coolingOutputThisFrame;

    // ФИЗИКА
    public float massKg;
    public Vector2Int gridPosition;
    public Vector2Int gridSize;
    public ModuleOrientation orientation;
    public int priority;
}

public abstract class RuntimeModuleBase : MonoBehaviour
{
    public bool IsActive { get; protected set; }
    public bool IsOverheated { get; protected set; }
    public float CurrentTemperature { get; protected set; }
    public float MaxTemperature { get; protected set; }
    public int Priority { get; set; } = 5;

    public ModuleOrientation Orientation { get; set; }
    public Vector2Int GridPosition { get; set; }

    public abstract void TurnOn();
    public abstract void TurnOff();

    public void ForceOverheat()
    {
        IsOverheated = true;
        TurnOff();
        Debug.LogWarning($"[RuntimeModule] Модуль {gameObject.name} АВАРИЙНО ОТКЛЮЧЕН из-за перегрева!");
    }

    /// <summary>
    /// ФАЗА 1: Система спрашивает, сколько ресурсов нужно модулю.
    /// </summary>
    public abstract RuntimeModuleStatus GetStatus();

    /// <summary>
    /// ФАЗА 2: Система передает ресурсы. Модуль выполняет свою логику.
    /// </summary>
    public abstract void Tick(float dt, float providedFuel, float providedEnergy);
}