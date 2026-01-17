using System;
using UnityEngine;

/// <summary>
/// PlayerVehicleController
/// ----------------------------------------
/// - Садит игрока за штурвал транспорта (Paluba с PepelacController).
/// - Привязывает корень игрока к палубе и удерживает его на seatStandPoint.
/// - Исправления:
///   * CharacterController теперь отключается только один раз при посадке и включается один раз при выходе (не каждый кадр).
///   * При входе игрок привязывается к текущему транспорту и будет следовать за ним — при выходе окажется в актуальном месте палубы.
///   * Добавлены события OnEnteredVehicle / OnExitedVehicle.
/// 
/// Важно:
/// - playerController будет отключён при посадке (чтобы пешее управление не мешало).
/// - playerRoot (обычно корень игрока) должен быть назначен в инспекторе (по умолчанию this.transform).
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(CharacterController))]
public class PlayerVehicleController : MonoBehaviour
{
    [Header("References")]
    [Tooltip("Основной контроллер игрока (ходьба/бег/прыжок). Отключается при посадке.")]
    public PlayerController playerController;

    [Tooltip("CharacterController игрока.")]
    public CharacterController characterController;

    [Tooltip("Корневой объект игрока, который двигается (обычно тот же объект, где CC/PlayerController).")]
    public Transform playerRoot;

    [Header("Debug / State (read-only)")]
    [SerializeField] private bool isInVehicle = false;
    [SerializeField] private PepelacController currentVehicle;        // контроллер транспорта (на Paluba)
    [SerializeField] private VehicleSeatInteractable currentSeat;     // сиденье/штурвал
    [SerializeField] private Transform currentSeatStandPoint;         // точка стояния у штурвала

    // Сохранённые данные до посадки
    private Vector3 storedPlayerPosition;
    private Quaternion storedPlayerRotation;
    private Transform originalParent;

    // События
    public event Action<PepelacController> OnEnteredVehicle;
    public event Action OnExitedVehicle;

    void Awake()
    {
        if (playerController == null)
            playerController = GetComponent<PlayerController>();

        if (characterController == null)
            characterController = GetComponent<CharacterController>();

        if (playerRoot == null)
            playerRoot = this.transform;
    }

    void Update()
    {
        // Пока игрок в транспорте — держим его в точке seatStandPoint относительно палубы.
        // Важно: НЕ включаем/отключаем CharacterController здесь.
        if (isInVehicle && currentSeatStandPoint != null)
        {
            // Позиционируем игрока жёстко в мировых координатах как seatStandPoint.
            // Так как playerRoot является ребёнком транспорта, можно также задать localPosition/localRotation,
            // но установка мировых координат здесь безопасна и понятна.
            playerRoot.position = currentSeatStandPoint.position;
            playerRoot.rotation = currentSeatStandPoint.rotation;
        }
    }

    public bool IsInVehicle => isInVehicle;

    public void RequestExit()
    {
        if (!isInVehicle) return;
        ExitVehicle();
    }

    /// <summary>
    /// Вход в транспорт (Paluba) с указанного сиденья.
    /// Вызывается из VehicleSeatInteractable.Interact().
    /// </summary>
    public void EnterVehicle(VehicleSeatInteractable seat, PepelacController vehicle, Transform seatStandPoint)
    {
        if (isInVehicle)
        {
            Debug.LogWarning("[PlayerVehicleController] Попытка EnterVehicle, когда уже в транспорте.");
            return;
        }

        if (vehicle == null)
        {
            Debug.LogError("[PlayerVehicleController] EnterVehicle: vehicle == null.");
            return;
        }

        currentSeat = seat;
        currentVehicle = vehicle;
        currentSeatStandPoint = seatStandPoint;

        // Сохраняем позицию/ротацию и родителя корня игрока
        originalParent = playerRoot.parent;
        storedPlayerPosition = playerRoot.position;
        storedPlayerRotation = playerRoot.rotation;

        // 1) Перемещаем к seatStandPoint (если указан)
        if (seatStandPoint != null)
        {
            // Отключаем CharacterController один раз перед перемещением, чтобы избежать конфликтов
            if (characterController != null && characterController.enabled)
                characterController.enabled = false;

            playerRoot.position = seatStandPoint.position;
            playerRoot.rotation = seatStandPoint.rotation;
        }

        // 2) Привязываем к транспорту (Paluba)
        Transform vehicleTransform = currentVehicle.transform;
        // Становимся дочерним объектом палубы: playerRoot будет следовать за палубой.
        playerRoot.SetParent(vehicleTransform, true); // true - сохраним мировые координаты при присоединении

        // 3) Отключаем управление пешим персонажем
        if (playerController != null)
            playerController.enabled = false;

        // 4) Включаем управление транспортом
        currentVehicle.EnableControl();

        // Состояние
        isInVehicle = true;

        Debug.Log($"[PlayerVehicleController] Вход в транспорт: {vehicle.name} (seat={seat?.name}), playerRoot теперь ребёнок {playerRoot.parent?.name}");

        // Вызов события
        OnEnteredVehicle?.Invoke(currentVehicle);

        // UI hint (можно заменить/расширить извне через подписку на событие)
        InteractionHintUI.Instance?.SetVisible(true, "[F]", "Выйти из транспорта");
    }

    /// <summary>
    /// Выход из транспорта.
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

        // 2) Отвязываем корень игрока от палубы и возвращаем родителя
        if (originalParent != null)
            playerRoot.SetParent(originalParent, true);
        else
            playerRoot.SetParent(null, true);

        // По желанию: можно восстановить позицию до посадки (закомментировано)
        // playerRoot.position = storedPlayerPosition;
        // playerRoot.rotation = storedPlayerRotation;

        // 3) Включаем обратно управление персонажем и CharacterController
        if (playerController != null)
            playerController.enabled = true;

        if (characterController != null && !characterController.enabled)
            characterController.enabled = true;

        isInVehicle = false;
        currentVehicle = null;
        currentSeat = null;
        currentSeatStandPoint = null;

        // Вызов события
        OnExitedVehicle?.Invoke();

        InteractionHintUI.Instance?.SetVisible(false);
    }
}