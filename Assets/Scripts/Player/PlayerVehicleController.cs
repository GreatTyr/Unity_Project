using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// PlayerVehicleController
/// ----------------------------------------
/// Отвечает за:
/// - Посадку игрока за штурвал транспорта.
/// - Привязку игрока к транспорту (становится дочерним, чтобы "стоять" на палубе).
/// - Выход из транспорта и возврат к обычному управлению персонажем.
///
/// ВНИМАНИЕ:
/// - Этот компонент НЕ слушает кнопку F напрямую.
///   Вход/выход инициируются через VehicleSeatInteractable.Interact().
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(CharacterController))]
public class PlayerVehicleController : MonoBehaviour
{
    [Header("References")]
    [Tooltip("Основной контроллер игрока (ходьба/бег/прыжок). Отключается при посадке в транспорт.")]
    public PlayerController playerController;

    [Tooltip("CharacterController игрока (для контроля коллизии при телепорте и смене родителя).")]
    public CharacterController characterController;

    [Header("Debug / State (read-only)")]
    [SerializeField] private bool isInVehicle = false;           // Находится ли игрок сейчас в транспорте
    [SerializeField] private PepelacController currentVehicle;   // Текущий контроллер транспорта
    [SerializeField] private VehicleSeatInteractable currentSeat;// Ссылка на сиденье/штурвал, за которым сидит игрок

    // Сохранённая позиция/ротация игрока (если когда-нибудь захочется возвращать к исходной точке)
    private Vector3 storedPlayerPosition;
    private Quaternion storedPlayerRotation;

    // Родитель игрока до посадки (чтобы вернуть при выходе).
    private Transform originalParent;

    void Awake()
    {
        if (playerController == null)
            playerController = GetComponent<PlayerController>();

        if (characterController == null)
            characterController = GetComponent<CharacterController>();
    }

    void OnEnable()
    {
        // Больше НЕ подписываемся на interactAction (F).
        // Вход/выход обрабатывает VehicleSeatInteractable.
    }

    void OnDisable()
    {
        // Аналогично, здесь также ничего не отписываем.
    }

    /// <summary>
    /// True, если игрок сейчас в режиме транспорта.
    /// </summary>
    public bool IsInVehicle => isInVehicle;

    /// <summary>
    /// Публичный метод: "запрос выхода".
    /// Вызывается из VehicleSeatInteractable.Interact(), когда игрок уже сидит за штурвалом.
    /// </summary>
    public void RequestExit()
    {
        if (!isInVehicle) return;
        ExitVehicle();
    }

    /// <summary>
    /// Вход в транспорт с указанного сиденья.
    /// Вызывается из VehicleSeatInteractable.Interact(), когда игрок НЕ в транспорте.
    /// </summary>
    /// <param name="seat">Сиденье/штурвал</param>
    /// <param name="vehicle">Контроллер транспорта (PepelacController)</param>
    /// <param name="seatStandPoint">Точка "стояния" игрока у штурвала (на палубе)</param>
    public void EnterVehicle(VehicleSeatInteractable seat, PepelacController vehicle, Transform seatStandPoint)
    {
        if (isInVehicle)
        {
            Debug.LogWarning("[PlayerVehicleController] Попытка EnterVehicle, когда игрок уже в транспорте. Игнорируем.");
            return;
        }

        if (vehicle == null)
        {
            Debug.LogError("[PlayerVehicleController] EnterVehicle: vehicle == null.");
            return;
        }

        currentSeat = seat;
        currentVehicle = vehicle;

        // Сохраняем текущую позицию/ротацию и родителя для возможного возврата
        storedPlayerPosition = transform.position;
        storedPlayerRotation = transform.rotation;
        originalParent = transform.parent;

        // 1) Перемещаем игрока к точке у штурвала
        if (seatStandPoint != null)
        {
            if (characterController != null)
                characterController.enabled = false; // временно отключаем, чтобы не мешал перемещению

            transform.position = seatStandPoint.position;
            transform.rotation = seatStandPoint.rotation;

            // 2) Привязываем игрока к корню транспорта, чтобы он "замёрз" относительно палубы,
            //    а не мировых координат.
            //
            // Сохранение мировых координат (true) значит:
            // - позиция/ротация останутся такими же в мире, но теперь считаются относительно родителя.
            Transform vehicleRoot = currentVehicle.transform; // корень Pepelac
            transform.SetParent(vehicleRoot, true);

            if (characterController != null)
                characterController.enabled = true;
        }
        else
        {
            // Если seatStandPoint не задан, всё равно привязываем к транспорту,
            // чтобы движение транспорта "таскало" игрока.
            Transform vehicleRoot = currentVehicle.transform;
            transform.SetParent(vehicleRoot, true);
        }

        // 3) Отключаем управление пешим персонажем
        if (playerController != null)
            playerController.enabled = false;

        // 4) Включаем управление транспортом
        currentVehicle.EnableControl();

        isInVehicle = true;

        Debug.Log($"[PlayerVehicleController] Вход в транспорт: {vehicle.name} с сиденья {seat?.name}");

        // Обновляем UI-подсказку — теперь F = "Выйти из транспорта"
        InteractionHintUI.Instance?.SetVisible(true, "[F]", "Выйти из транспорта");
    }

    /// <summary>
    /// Реальный выход из транспорта.
    /// Вызывается из RequestExit().
    /// </summary>
    public void ExitVehicle()
    {
        if (!isInVehicle)
            return;

        Debug.Log($"[PlayerVehicleController] Выход из транспорта: {currentVehicle?.name}");

        // 1) Отключаем управление транспортом
        if (currentVehicle != null)
        {
            currentVehicle.DisableControl();
        }

        // 2) Отключаем привязку к транспорту: возвращаем исходного родителя
        if (characterController != null)
            characterController.enabled = false; // на время смены родителя

        if (originalParent != null)
        {
            // Вернуть в исходную иерархию (например, к PlayerRoot)
            transform.SetParent(originalParent, true);
        }
        else
        {
            // Если родителя не было, просто отвяжем (в корень сцены)
            transform.SetParent(null, true);
        }

        if (characterController != null)
            characterController.enabled = true;

        // 3) Вариант позиционирования после выхода:
        //
        // Сейчас мы оставляем игрока в той же мировой позиции, в которой он находился на палубе
        // в момент выхода (SetParent(..., true) сохраняет мировые координаты).
        //
        // Если захочешь возвращать к storedPlayerPosition — можно раскомментировать:
        // transform.position = storedPlayerPosition;
        // transform.rotation = storedPlayerRotation;

        // 4) Включаем обратно управление персонажем
        if (playerController != null)
            playerController.enabled = true;

        isInVehicle = false;
        currentVehicle = null;
        currentSeat = null;

        // 5) Скрываем или обновляем подсказку
        InteractionHintUI.Instance?.SetVisible(false);
    }
}