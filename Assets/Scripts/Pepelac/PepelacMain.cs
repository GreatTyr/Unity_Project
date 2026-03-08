using UnityEngine;

/// <summary>
/// PepelacMain — "Паспорт" и фасад Пепелаца.
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

    // Суммарная масса: Корпус + Модули на сетке + Груз в инвентаре
    public float TotalMassKg => hullMassKg + CurrentStats.totalModulesMassKg + currentCarryWeight;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        if (systems == null) systems = GetComponentInChildren<PepelacSystems>();
        if (grid == null) grid = GetComponentInChildren<PepelacGrid>();
    }

    private void FixedUpdate()
    {
        if (rb == null || systems == null || grid == null) return;

        // 1. Применяем суммарную массу к физике
        rb.mass = TotalMassKg > 0f ? TotalMassKg : 1f;

        // 2. Рассчитываем смещение центра масс (взвешенное среднее)

        // ИСПРАВЛЕНИЕ: Добавлен Vector2Int.one в качестве размера (так как это абстрактная точка)
        Vector3 modulesCoM3D = grid.GridToLocalPosition(
            Mathf.RoundToInt(CurrentStats.centerOfMass.x),
            Mathf.RoundToInt(CurrentStats.centerOfMass.y),
            Vector2Int.one
        );

        Vector3 weightedSum = (hullCenterOfMassOffset * hullMassKg) + (modulesCoM3D * CurrentStats.totalModulesMassKg);
        rb.centerOfMass = weightedSum / rb.mass;
    }

    public void SetCurrentCarryWeight(float newWeight)
    {
        currentCarryWeight = Mathf.Clamp(newWeight, 0f, maxCarryWeight);
    }
}