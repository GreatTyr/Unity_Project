using UnityEngine;

// Тип ориентации теперь здесь
public enum ModuleOrientation { Deg0, Deg90, Deg180, Deg270 }

[DisallowMultipleComponent]
public class ModuleRuntimeState : MonoBehaviour
{
    [Header("Placement")]
    public bool placed = false;

    [Header("Runtime Flags")]
    public bool ableToOnOff = true;
    public bool isOn = false;

    public bool ableToUse = false;
    public bool isUsing = false;

    public bool ableToPulse = false;
    public bool pulseReady = false;

    public bool alive = true;

    [Header("Dynamic Parameters (Current Values)")]
    public float currentDurability;
    public float currentTemperature;
    public float currentStaticCharge;
    public float currentEnergy; // Только для батарей/генераторов
    public float currentFuel;   // Только для баков/генераторов

    [Header("Meta")]
    public ModuleOrientation orientation = ModuleOrientation.Deg0;
    public Vector2Int gridPosition = new Vector2Int(-1, -1);

    // Метод 1: Инициализация напрямую из эталона
    public void InitializeFromStandard(StandardModuleBase standard)
    {
        placed = false;

        if (standard == null)
        {
            ableToOnOff = true;
            isOn = false;
            ableToUse = false;
            isUsing = false;
            ableToPulse = false;
            pulseReady = false;
            alive = true;
            orientation = ModuleOrientation.Deg0;
            gridPosition = new Vector2Int(-1, -1);
            return;
        }

        ableToOnOff = standard.CanTurnOnOff;
        isOn = false;

        ableToUse = standard.IsControllable;
        isUsing = false;

        ableToPulse = standard.CanPulseMode;
        pulseReady = standard.CanPulseMode;

        alive = true;
        orientation = ModuleOrientation.Deg0;
        gridPosition = new Vector2Int(-1, -1);
    }

    // Метод 2: Прямая инициализация флагами (если нужно из кода)
    public void InitializeFromStandardFlags(bool canTurnOnOff, bool isControllable, bool canPulseMode)
    {
        placed = false;

        ableToOnOff = canTurnOnOff;
        isOn = false;

        ableToUse = isControllable;
        isUsing = false;

        ableToPulse = canPulseMode;
        pulseReady = canPulseMode;

        alive = true;
        orientation = ModuleOrientation.Deg0;
        gridPosition = new Vector2Int(-1, -1);
    }
}