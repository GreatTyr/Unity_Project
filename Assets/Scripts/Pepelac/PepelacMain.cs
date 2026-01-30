using UnityEngine;

/// <summary>
/// PepelacMain — "паспорт" и runtime-стат Pepelac.
/// Хранит:
/// - конфиг движения/hover (как было в PepelacController),
/// - массу/груз,
/// - топливо/энергию,
/// - текущее состояние (runtime).
///
/// Идея:
/// - ВСЕ системы (движение, расход топлива, груз, оружие и т.п.)
///   читают/пишут параметры через этот компонент.
/// - PepelacController НЕ является местом, где настраиваются числа —
///   он только исполняет логику на основе значений из PepelacMain.
/// </summary>
[DisallowMultipleComponent]
public class PepelacMain : MonoBehaviour
{
    [Header("Movement (config)")]
    [Tooltip("Максимальная скорость вперёд (м/с). Соответствует forwardSpeed в PepelacController.")]
    public float forwardSpeed = 8f;

    [Tooltip("Максимальная скорость стрейфа (м/с). Соответствует strafeSpeed.")]
    public float strafeSpeed = 6f;

    [Tooltip("Максимальное горизонтальное ускорение (м/с²). Ограничивает разгон (maxHorizontalAcceleration).")]
    public float maxHorizontalAcceleration = 20f;

    [Tooltip("Скорость поворота (град/с). Соответствует turnSpeed.")]
    public float turnSpeed = 90f;

    [Tooltip("Скорость сглаживания поворота (0 = мгновенно). Соответствует rotationSlerpSpeed.")]
    public float rotationSlerpSpeed = 10f;

    [Tooltip("Сила прыжка (м/с), как jumpImpulse в PepelacController.")]
    public float jumpImpulse = 5f;

    [Tooltip("Гравитация (для fallback-режима без Rigidbody). Соответствует gravity в PepelacController.")]
    public float gravity = -9.81f;

    [Header("Hover (config)")]
    [Tooltip("Скорость набора высоты при удержании Rise (м/с).")]
    public float riseSpeed = 2f;

    [Tooltip("Скорость уменьшения высоты при удержании Lower (м/с).")]
    public float lowerSpeed = 3f;

    [Tooltip("Максимальная относительная высота над поверхностью (м).")]
    public float maxHoverOffset = 50f;

    [Tooltip("Коэффициент P вертикального PD-контроллера.")]
    public float verticalSpringKp = 300f;

    [Tooltip("Коэффициент D вертикального PD-контроллера.")]
    public float verticalSpringKd = 40f;

    [Tooltip("Максимальная вертикальная сила (Н) для hover-контроллера.")]
    public float maxVerticalForce = 5000f;

    [Tooltip("Длительность временного отключения hover после прыжка (сек).")]
    public float jumpBreaksHoverDuration = 0.6f;

    [Tooltip("Использовать ли 'жёсткое' удержание высоты (holdHoverPreventsGravity).")]
    public bool holdHoverPreventsGravity = true;

    [Tooltip("Включать ли усиленный snap-режим при отпускании rise/lower.")]
    public bool snapOnRelease = false;

    [Tooltip("Множитель усиления PD в snap-режиме.")]
    public float snapForceMultiplier = 3f;

    [Tooltip("Длительность snap-режима (сек).")]
    public float snapDuration = 0.15f;

    [Tooltip("Использовать ли базовую высоту поверхности как origin (useBaseGroundY).")]
    public bool useBaseGroundY = true;

    [Header("Physics / Rigidbody (config)")]
    [Tooltip("Базовая 'сухая' масса Pepelac без груза и топлива (кг).")]
    public float baseMass = 1000f;

    [Tooltip("Использовать ли Rigidbody-режим (useRigidbody в PepelacController). Обычно true.")]
    public bool useRigidbody = true;

    [Tooltip("Блокировать ли заваливание по X/Z (freezeTiltAxes).")]
    public bool freezeTiltAxes = true;

    [Tooltip("Смещение центра масс относительно локального центра объекта.")]
    public Vector3 centerOfMassOffset = Vector3.zero;

    [Header("Cargo (config)")]
    [Tooltip("Максимальный вес груза (кг), который может перевозить Pepelac.")]
    public float maxCarryWeight = 500f;

    [Header("Fuel & Energy (config)")]
    [Tooltip("Максимальный запас топлива.")]
    public float maxFuel = 100f;

    [Tooltip("Базовый расход топлива в секунду при работе двигателей (условный множитель).")]
    public float fuelConsumptionBase = 0.1f;

    [Tooltip("Дополнительный множитель расхода при интенсивном тяговом режиме (ускорение, подъём и т.п.).")]
    public float fuelConsumptionThrustMultiplier = 0.5f;

    [Tooltip("Максимальный запас энергии (для оружия, щитов и т.п.).")]
    public float maxEnergy = 100f;

    [Tooltip("Базовый расход энергии за выстрел / действие (будущие системы).")]
    public float energyConsumptionPerShot = 5f;

    [Header("Runtime state (read/write, отображение текущего состояния)")]
    [Tooltip("Текущий запас топлива.")]
    public float currentFuel;

    [Tooltip("Текущий запас энергии.")]
    public float currentEnergy;

    [Tooltip("Текущий вес груза (кг). Сюда может писать система инвентаря/груза.")]
    public float currentCarryWeight;

    [Tooltip("Текущая полная масса (кг) = baseMass + груз + топливо. Можно пересчитывать в рантайме.")]
    public float currentTotalMass;

    void Reset()
    {
        // При добавлении компонента зададим стартовые runtime-значения
        currentFuel = maxFuel;
        currentEnergy = maxEnergy;
        currentCarryWeight = 0f;
        RecalculateTotalMass();
    }

    /// <summary>
    /// Пересчитать текущую общую массу на основе базовой массы, груза и топлива.
    /// Можно вызывать из других систем после изменения груза/топлива.
    /// </summary>
    public void RecalculateTotalMass()
    {
        // Можно считать топливо с каким-то коэффициентом плотности, но для простоты 1:1
        currentTotalMass = baseMass + currentCarryWeight + currentFuel;
    }

    /// <summary>
    /// Расходует топливо. Возвращает фактически потраченное количество (с учётом, что currentFuel не уйдёт в минус).
    /// </summary>
    public float ConsumeFuel(float amount)
    {
        if (amount <= 0f || currentFuel <= 0f)
            return 0f;

        float consumed = Mathf.Min(amount, currentFuel);
        currentFuel -= consumed;
        RecalculateTotalMass();
        return consumed;
    }

    /// <summary>
    /// Добавляет топливо, не превышая maxFuel. Возвращает фактически добавленное количество.
    /// </summary>
    public float AddFuel(float amount)
    {
        if (amount <= 0f)
            return 0f;

        float space = maxFuel - currentFuel;
        float added = Mathf.Min(amount, space);
        currentFuel += added;
        RecalculateTotalMass();
        return added;
    }

    /// <summary>
    /// Изменить текущий вес груза (например, при загрузке/разгрузке инвентаря).
    /// </summary>
    public void SetCurrentCarryWeight(float newWeight)
    {
        currentCarryWeight = Mathf.Clamp(newWeight, 0f, maxCarryWeight);
        RecalculateTotalMass();
    }

    /// <summary>
    /// Расходует энергию. Аналогично ConsumeFuel.
    /// </summary>
    public float ConsumeEnergy(float amount)
    {
        if (amount <= 0f || currentEnergy <= 0f)
            return 0f;

        float consumed = Mathf.Min(amount, currentEnergy);
        currentEnergy -= consumed;
        return consumed;
    }

    /// <summary>
    /// Добавляет энергию (например, от генератора/модуля).
    /// </summary>
    public float AddEnergy(float amount)
    {
        if (amount <= 0f)
            return 0f;

        float space = maxEnergy - currentEnergy;
        float added = Mathf.Min(amount, space);
        currentEnergy += added;
        return added;
    }
}