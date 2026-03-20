using UnityEngine;

/// <summary>
/// PepelacMain — "паспорт" и фасад Пепелаца.
/// Единая точка доступа к текущему состоянию (масса, топливо, энергия).
/// Берет данные от PepelacSystems и применяет их к Rigidbody.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody))]
public class PepelacMain : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private PepelacSystems systems;
    [SerializeField] private PepelacGrid grid;

    [Header("Hull Config (Корпус)")]
    [Tooltip("Базовая 'сухая' масса корпуса без модулей (кг).")]
    public float hullMassKg = 1000f;

    [Tooltip("Смещение центра масс пустого корпуса.")]
    public Vector3 hullCenterOfMassOffset = Vector3.zero;

    [Tooltip("Максимальный вес груза в инвентаре (кг).")]
    public float maxCarryWeight = 500f;

    [Header("Runtime Cargo")]
    [Tooltip("Текущий вес инвентаря. Сюда пишет InventorySystem.")]
    public float currentCarryWeight;

    private Rigidbody rb;

    // Публичные геттеры для UI и других систем
    public PepelacStats CurrentStats => systems != null ? systems.CurrentStats : default;

    // Суммарная масса: корпус + модули + груз
    public float TotalMassKg => hullMassKg + CurrentStats.totalModulesMassKg + currentCarryWeight;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();

        if (systems == null)
            systems = GetComponentInChildren<PepelacSystems>();

        if (grid == null)
            grid = GetComponentInChildren<PepelacGrid>();
    }

    private void FixedUpdate()
    {
        if (rb == null || systems == null || grid == null)
            return;

        // 1. Применяем суммарную массу
        float totalMass = TotalMassKg > 0f ? TotalMassKg : 1f;
        rb.mass = totalMass;

        // 2. Временный безопасный расчёт центра масс
        // Пока PepelacSystems не считает centerOfMass как полноценную физическую модель,
        // используем fallback через центр корпуса.
        Vector3 fallbackModulesCoM = hullCenterOfMassOffset;

        bool hasModulesMass = CurrentStats.totalModulesMassKg > 0.001f;
        bool hasValidGridCoM = hasModulesMass && CurrentStats.centerOfMass != Vector2.zero;

        Vector3 modulesCoM3D = fallbackModulesCoM;

        if (hasValidGridCoM)
        {
            modulesCoM3D = grid.AnchorCellToLocalCenter(
                Mathf.RoundToInt(CurrentStats.centerOfMass.x),
                Mathf.RoundToInt(CurrentStats.centerOfMass.y)
            );
        }

        // Груз пока считаем приложенным в точке центра корпуса.
        Vector3 weightedSum =
            (hullCenterOfMassOffset * hullMassKg) +
            (modulesCoM3D * CurrentStats.totalModulesMassKg) +
            (hullCenterOfMassOffset * currentCarryWeight);

        rb.centerOfMass = weightedSum / totalMass;
    }

    public void SetCurrentCarryWeight(float newWeight)
    {
        currentCarryWeight = Mathf.Clamp(newWeight, 0f, maxCarryWeight);
    }
}