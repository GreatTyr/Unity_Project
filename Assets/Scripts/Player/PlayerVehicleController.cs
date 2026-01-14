using UnityEngine;

/// <summary>
/// PlayerVehicleController
/// ----------------------------------------
/// - Садит игрока за штурвал транспорта (Paluba с PepelacController).
/// - Привязывает корень игрока к палубе и каждый кадр удерживает его
///   на seatStandPoint, чтобы он "замирал" относительно палубы.
/// - При выходе возвращает родителя и управление.
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
        // Пока игрок в транспорте — жёстко держим его в точке seatStandPoint
        if (isInVehicle && currentSeatStandPoint != null)
        {
            // Отключаем CC на время позиционирования, чтобы он не вмешивался
            if (characterController != null && characterController.enabled)
                characterController.enabled = false;

            // Удерживаем позицию и ориентацию точно как у seatStandPoint (в мировых координатах)
            playerRoot.position = currentSeatStandPoint.position;
            playerRoot.rotation = currentSeatStandPoint.rotation;

            if (characterController != null)
                characterController.enabled = true;
        }
    }

    void OnEnable() { }
    void OnDisable() { }

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

        // 1) Перемещаем к seatStandPoint
        if (seatStandPoint != null)
        {
            if (characterController != null)
                characterController.enabled = false;

            playerRoot.position = seatStandPoint.position;
            playerRoot.rotation = seatStandPoint.rotation;
        }

        // 2) Привязываем к транспорту (Paluba)
        Transform vehicleTransform = currentVehicle.transform;
        playerRoot.SetParent(vehicleTransform, true); // сохраняем мировые координаты

        if (characterController != null)
            characterController.enabled = true;

        // 3) Отключаем управление пешим персонажем
        if (playerController != null)
            playerController.enabled = false;

        // 4) Включаем управление транспортом
        currentVehicle.EnableControl();

        isInVehicle = true;

        Debug.Log($"[PlayerVehicleController] Вход в транспорт: {vehicle.name} (seat={seat?.name}), " +
                  $"playerRoot теперь ребёнок {playerRoot.parent?.name}");

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
        if (characterController != null)
            characterController.enabled = false;

        if (originalParent != null)
            playerRoot.SetParent(originalParent, true);
        else
            playerRoot.SetParent(null, true);

        if (characterController != null)
            characterController.enabled = true;

        // (Опционально) вернуть в позицию до посадки:
        // playerRoot.position = storedPlayerPosition;
        // playerRoot.rotation = storedPlayerRotation;

        // 3) Включаем обратно управление персонажем
        if (playerController != null)
            playerController.enabled = true;

        isInVehicle = false;
        currentVehicle = null;
        currentSeat = null;
        currentSeatStandPoint = null;

        InteractionHintUI.Instance?.SetVisible(false);
    }
}