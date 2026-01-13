using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// PlayerVehicleController
/// - Отвечает за посадку игрока за штурвал транспорта и выход из него.
/// - Отключает/включает PlayerController, замораживает позицию игрока,
///   включает/отключает контроллер транспорта (PepelacController или любой IVehicleController в будущем).
/// - Подвязан к той же кнопке Interact (F), что и общая система взаимодействия.
/// </summary>
[DisallowMultipleComponent]
public class PlayerVehicleController : MonoBehaviour
{
    [Header("References")]
    [Tooltip("Основной контроллер игрока (ходьба/бег/прыжок)")]
    public PlayerController playerController;

    [Tooltip("CharacterController игрока (для включения/выключения коллизии при посадке)")]
    public CharacterController characterController;

    [Tooltip("Трансформ точки, где игрок 'стоит у штурвала' (обычно на транспорте)")]
    public Transform steeringStandPoint;

    [Header("Input")]
    [Tooltip("Ссылка на то же действие Interact (F), что и у PlayerInteractionManager / PlayerLookInteractor")]
    public InputActionReference interactAction;

    [Header("State (read-only for debug)")]
    [SerializeField] bool isInVehicle = false;
    [SerializeField] PepelacController currentVehicle;   // в дальнейшем можно заменить на интерфейс IVehicleController
    [SerializeField] VehicleSeatInteractable currentSeat;

    Vector3 storedPlayerPosition;
    Quaternion storedPlayerRotation;

    void Awake()
    {
        if (playerController == null) playerController = GetComponent<PlayerController>();
        if (characterController == null) characterController = GetComponent<CharacterController>();
    }

    void OnEnable()
    {
        if (interactAction != null && interactAction.action != null)
        {
            interactAction.action.performed += OnInteractPerformed;
        }
    }

    void OnDisable()
    {
        if (interactAction != null && interactAction.action != null)
        {
            interactAction.action.performed -= OnInteractPerformed;
        }
    }

    void OnInteractPerformed(InputAction.CallbackContext ctx)
    {
        // Кнопка F нажата.
        // Если мы в транспорте — выходим, если нет — ничего (вход инициируется через VehicleSeatInteractable.Interact()).
        if (!ctx.performed) return;

        if (isInVehicle)
        {
            ExitVehicle();
        }
    }

    /// <summary>
    /// Вход в конкретный транспорт от конкретного сиденья.
    /// Вызывается из VehicleSeatInteractable.Interact().
    /// </summary>
    public void EnterVehicle(VehicleSeatInteractable seat, PepelacController vehicle, Transform seatStandPoint)
    {
        if (isInVehicle)
        {
            Debug.LogWarning("[PlayerVehicleController] Уже в транспорте, повторный EnterVehicle проигнорирован.");
            return;
        }

        if (vehicle == null)
        {
            Debug.LogError("[PlayerVehicleController] EnterVehicle: vehicle == null.");
            return;
        }

        currentSeat = seat;
        currentVehicle = vehicle;

        // Сохраняем текущую позицию игрока (чтобы вернуть при выходе, если нужно)
        storedPlayerPosition = transform.position;
        storedPlayerRotation = transform.rotation;

        // Перемещаем игрока в точку у штурвала
        if (seatStandPoint != null)
        {
            if (characterController != null)
                characterController.enabled = false; // временно отключаем, чтобы не мешал перемещению

            transform.position = seatStandPoint.position;
            transform.rotation = seatStandPoint.rotation;

            if (characterController != null)
                characterController.enabled = true;
        }

        // Отключаем управление игроком
        if (playerController != null)
            playerController.enabled = false;

        // Включаем контроль транспорта
        currentVehicle.EnableControl();

        isInVehicle = true;

        Debug.Log($"[PlayerVehicleController] Вход в транспорт: {vehicle.name} с сиденья {seat?.name}");

        // Можно обновить hint UI — например, подсказать, что F теперь = выйти:
        InteractionHintUI.Instance?.SetVisible(true, "[F] Выйти из транспорта");
    }

    /// <summary>
    /// Выход из текущего транспорта.
    /// </summary>
    public void ExitVehicle()
    {
        if (!isInVehicle)
            return;

        Debug.Log($"[PlayerVehicleController] Выход из транспорта: {currentVehicle?.name}");

        // Выключаем контроллер транспорта
        if (currentVehicle != null)
        {
            currentVehicle.DisableControl();
        }

        // Возвращаем управление игроку
        if (playerController != null)
            playerController.enabled = true;

        // Можно либо вернуть игрока туда, где он был до посадки...
        //transform.position = storedPlayerPosition;
        //transform.rotation = storedPlayerRotation;

        // ...либо оставить у штурвала – реши, как удобнее.
        // Чтобы "слезать" чуть позади штурвала, можно сделать отдельную точку выхода (seat.exitPoint).

        isInVehicle = false;
        currentVehicle = null;
        currentSeat = null;

        // Обновляем UI
        InteractionHintUI.Instance?.SetVisible(false);
    }

    public bool IsInVehicle => isInVehicle;
}