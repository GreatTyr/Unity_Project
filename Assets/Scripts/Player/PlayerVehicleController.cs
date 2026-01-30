using System;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// PlayerVehicleController
/// Управляет посадкой/выходом игрока в транспорт через IControllableVehicle.
/// - Отключает пеший контроллер и CharacterController при посадке.
/// - Привязывает playerRoot к Root транспорта.
/// - Обрабатывает глобальный выход из транспорта по exitVehicleAction.
/// - События OnEnteredVehicle / OnExitedVehicle для подписчиков (камера, UI).
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

    [Tooltip("Animator игрока.")]
    [SerializeField] private Animator playerAnimator;

    [Tooltip("Корневой объект игрока (обычно тот же объект, где CC/PlayerController).")]
    public Transform playerRoot;

    [Header("Input")]
    [Tooltip("Action для выхода из транспорта (например, F или отдельная клавиша).")]
    public InputActionReference exitVehicleAction;

    [Header("Exit settings")]
    [Tooltip("Минимальное время после посадки, в течение которого нажатие выхода игнорируется (защита от мгновенного двойного срабатывания).")]
    public float exitGraceTime = 0.2f;

    [Header("Debug / State (read-only)")]
    [SerializeField] private bool isInVehicle = false;
    [SerializeField] private IControllableVehicle currentVehicle;        // контроллер транспорта (IControllableVehicle)
    [SerializeField] private VehicleSeatInteractable currentSeat;        // сиденье/штурвал
    [SerializeField] private Transform currentSeatStandPoint;            // точка стояния у штурвала

    // Сохранённые данные до посадки
    private Vector3 storedPlayerPosition;
    private Quaternion storedPlayerRotation;
    private Transform originalParent;

    // Время последней посадки в транспорт (для грейс-периода)
    private float lastEnterTime = -999f;

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

        if (playerAnimator == null)
            playerAnimator = GetComponentInChildren<Animator>();
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
        // Жёсткое позиционирование за штурвалом отключили,
        // так как playerRoot уже является дочерним vehicle.Root
        // и двигается физикой вместе с Pepelac.
        //
        // Если позже понадобится мягкая подтяжка к currentSeatStandPoint,
        // можно добавить это в LateUpdate через Lerp/Slerp.
    }

    /// <summary>
    /// true, если игрок сейчас находится в транспорте.
    /// </summary>
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
    /// Обработчик нажатия кнопки выхода из транспорта (exitVehicleAction).
    /// Работает глобально: не требуется наводиться на штурвал.
    /// </summary>
    private void OnExitVehiclePerformed(InputAction.CallbackContext ctx)
    {
        if (!ctx.performed) return;
        if (!isInVehicle) return;

        // Грейс-период: игнорируем нажатие, если прошло меньше exitGraceTime
        // с момента посадки. Это защищает от ситуации, когда та же кнопка
        // используется и для входа, и для выхода, и событие срабатывает дважды.
        if (Time.time - lastEnterTime < exitGraceTime)
            return;

        ExitVehicle();
    }

    /// <summary>
    /// EnterVehicle: принимает VehicleSeatInteractable, IControllableVehicle и точку стояния.
    /// Вызывается штурвалом (VehicleSeatInteractable.Interact).
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

        // Найдём Animator (если не задан в инспекторе)
        if (playerAnimator == null)
            playerAnimator = playerRoot.GetComponentInChildren<Animator>();

        if (playerAnimator != null)
        {
            // Отключаем root motion, чтобы анимация не тащила модельку за штурвалом
            playerAnimator.applyRootMotion = false;

            // Обнуляем параметр скорости (если он используется в контроллере анимаций)
            // Если параметра "Speed" нет, SetFloat просто не повредит.
            playerAnimator.SetFloat("Speed", 0f);
        }

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

        // Запоминаем время посадки для грейс-периода выхода
        lastEnterTime = Time.time;

        Debug.Log($"[PlayerVehicleController] Вход в транспорт (IControllableVehicle) playerRoot теперь ребёнок {playerRoot.parent?.name}");
        OnEnteredVehicle?.Invoke(vehicle);

        // При входе скрываем подсказки — их будет показывать отдельный UI (если нужен).
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

        // Возвращаем root motion для пешего режима (если он используется)
        if (playerAnimator != null)
        {
            playerAnimator.applyRootMotion = true;
        }

        isInVehicle = false;
        currentVehicle = null;
        currentSeat = null;
        currentSeatStandPoint = null;

        OnExitedVehicle?.Invoke();

        // После выхода подсказки решает показывать look-based система.
        InteractionHintUI.Instance?.SetVisible(false);
    }
}