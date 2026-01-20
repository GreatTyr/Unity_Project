using System;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// PlayerVehicleController (обновлён)
/// - Работает с IControllableVehicle (а не напрямую с PepelacController).
/// - EnterVehicle / ExitVehicle используют контракт интерфейса.
/// - CharacterController отключается один раз при посадке и включается один раз при выходе.
/// - При входе playerRoot привязывается к vehicle.Root (player будет следовать за палубой).
/// - События OnEnteredVehicle / OnExitedVehicle для подписчиков.
/// - ДОПОЛНЕНО: поддержка глобального выхода по кнопке (exitVehicleAction),
///   не требующего наведения на штурвал.
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

    [Tooltip("Корневой объект игрока (обычно тот же объект, где CC/PlayerController).")]
    public Transform playerRoot;

    [Header("Input")]
    [Tooltip("Action для выхода из транспорта (например, та же F или другая клавиша).")]
    public InputActionReference exitVehicleAction;

    [Header("Debug / State (read-only)")]
    [SerializeField] private bool isInVehicle = false;
    [SerializeField] private IControllableVehicle currentVehicle;        // контроллер транспорта (IControllableVehicle)
    [SerializeField] private VehicleSeatInteractable currentSeat;        // сиденье/штурвал
    [SerializeField] private Transform currentSeatStandPoint;            // точка стояния у штурвала

    // Сохранённые данные до посадки
    private Vector3 storedPlayerPosition;
    private Quaternion storedPlayerRotation;
    private Transform originalParent;

    // События
    public event Action<IControllableVehicle> OnEnteredVehicle;
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

    private void OnEnable()
    {
        if (exitVehicleAction != null && exitVehicleAction.action != null)
        {
            exitVehicleAction.action.performed += OnExitVehiclePerformed;
            exitVehicleAction.action.Enable();
        }
    }

    private void OnDisable()
    {
        if (exitVehicleAction != null && exitVehicleAction.action != null)
        {
            exitVehicleAction.action.performed -= OnExitVehiclePerformed;
            exitVehicleAction.action.Disable();
        }
    }

    void Update()
    {
        if (isInVehicle && currentSeatStandPoint != null)
        {
            // Держим игрока строго в точке, соответствующей seatStandPoint (в мировых координатах)
            playerRoot.position = currentSeatStandPoint.position;
            playerRoot.rotation = currentSeatStandPoint.rotation;
        }
    }

    public bool IsInVehicle => isInVehicle;

    /// <summary>
    /// Внешний запрос выхода (например, от VehicleSeatInteractable).
    /// </summary>
    public void RequestExit()
    {
        if (!isInVehicle) return;
        ExitVehicle();
    }

    /// <summary>
    /// Обработчик нажатия кнопки выхода из транспорта.
    /// Работает глобально: не требуется наводиться на штурвал.
    /// </summary>
    private void OnExitVehiclePerformed(InputAction.CallbackContext ctx)
    {
        if (!ctx.performed) return;

        if (isInVehicle)
        {
            ExitVehicle();
        }
    }

    /// <summary>
    /// EnterVehicle: принимает VehicleSeatInteractable, IControllableVehicle и точку стояния.
    /// </summary>
    public void EnterVehicle(VehicleSeatInteractable seat, IControllableVehicle vehicle, Transform seatStandPoint)
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

        // Сохраняем позицию/ротацию и родителя
        originalParent = playerRoot.parent;
        storedPlayerPosition = playerRoot.position;
        storedPlayerRotation = playerRoot.rotation;

        // 1) Перемещаем к seatStandPoint
        if (seatStandPoint != null)
        {
            if (characterController != null && characterController.enabled)
                characterController.enabled = false;

            playerRoot.position = seatStandPoint.position;
            playerRoot.rotation = seatStandPoint.rotation;
        }

        // 2) Привязываем к vehicle.Root — playerRoot будет следовать за палубой
        var vehicleTransform = vehicle.Root;
        playerRoot.SetParent(vehicleTransform, true);

        // 3) Отключаем управление пешим персонажем
        if (playerController != null)
            playerController.enabled = false;

        // 4) Включаем управление транспортом через интерфейс
        vehicle.EnableControl();

        isInVehicle = true;

        Debug.Log($"[PlayerVehicleController] Вход в транспорт (IControllableVehicle) playerRoot теперь ребёнок {playerRoot.parent?.name}");

        OnEnteredVehicle?.Invoke(vehicle);

        // ВАЖНО: никаких подсказок про выход здесь не показываем.
        // Логику подсказки привяжем к глобальному exitVehicleAction или к другому UI.
        InteractionHintUI.Instance?.SetVisible(false);
    }

    /// <summary>
    /// ExitVehicle — отключение управления транспортом и восстановление управления персонажем.
    /// </summary>
    public void ExitVehicle()
    {
        if (!isInVehicle)
            return;

        Debug.Log($"[PlayerVehicleController] Выход из транспорта: {currentVehicle?.Root?.name}");

        // 1) Отключаем управление транспортом
        currentVehicle?.DisableControl();

        // 2) Отвязываем и возвращаем родителя
        if (originalParent != null)
            playerRoot.SetParent(originalParent, true);
        else
            playerRoot.SetParent(null, true);

        // 3) Включаем управление персонажем и CharacterController
        if (playerController != null)
            playerController.enabled = true;

        if (characterController != null && !characterController.enabled)
            characterController.enabled = true;

        isInVehicle = false;
        currentVehicle = null;
        currentSeat = null;
        currentSeatStandPoint = null;

        OnExitedVehicle?.Invoke();

        // После выхода можно снова показывать подсказки, но пускай это решает look-based система.
        InteractionHintUI.Instance?.SetVisible(false);
    }
}