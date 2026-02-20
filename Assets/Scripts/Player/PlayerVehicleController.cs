using System;
using UnityEngine;
using UnityEngine.InputSystem;
using Unity.Cinemachine;

[DisallowMultipleComponent]
[RequireComponent(typeof(CharacterController))]
public class PlayerVehicleController : MonoBehaviour
{
    [Header("Cameras")]
    public CinemachineVirtualCameraBase playerCamera;
    public CinemachineVirtualCameraBase pepelacCamera;

    [Header("References")]
    public PlayerController playerController;
    public CharacterController characterController;
    [SerializeField] private Animator playerAnimator;
    public Transform playerRoot;

    [Header("Input")]
    public InputActionReference exitVehicleAction;

    [Header("Exit Settings")]
    public float exitGraceTime = 0.2f;

    [Header("Seat Lerp")]
    [SerializeField] private float posLerpSpeed = 20f;
    [SerializeField] private float rotLerpSpeed = 20f;

    [Header("Camera Priority")]
    [SerializeField] private int activeCameraPriority = 20;
    [SerializeField] private int inactiveCameraPriority = 10;

    [Header("Debug (read-only)")]
    [SerializeField] private bool isInVehicle = false;
    [SerializeField] private string debugVehicleName = "";
    [SerializeField] private string debugSeatName = "";

    private IControllableVehicle currentVehicle;
    private VehicleSeatInteractable currentSeat;
    private Transform currentSeatStandPoint;

    private Vector3 storedPlayerPosition;
    private Quaternion storedPlayerRotation;
    private Transform originalParent;
    private float lastEnterTime = -999f;

    public event Action<IControllableVehicle> OnEnteredVehicle;
    public event Action OnExitedVehicle;

    public bool IsInVehicle => isInVehicle;

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
        InputActionHelper.Subscribe(exitVehicleAction, OnExitVehiclePerformed);
    }

    private void OnDisable()
    {
        InputActionHelper.Unsubscribe(exitVehicleAction, OnExitVehiclePerformed);
    }

    public void RequestExit()
    {
        if (!isInVehicle) return;
        ExitVehicle();
    }

    private void OnExitVehiclePerformed(InputAction.CallbackContext ctx)
    {
        if (!ctx.performed) return;
        if (!isInVehicle) return;

        if (Time.time - lastEnterTime < exitGraceTime)
            return;

        ExitVehicle();
    }

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

        debugVehicleName = vehicle.Root != null ? vehicle.Root.name : "unknown";
        debugSeatName = seat != null ? seat.name : "none";

        originalParent = playerRoot.parent;
        storedPlayerPosition = playerRoot.position;
        storedPlayerRotation = playerRoot.rotation;

        if (playerAnimator == null)
            playerAnimator = playerRoot.GetComponentInChildren<Animator>();

        if (playerAnimator != null)
        {
            playerAnimator.applyRootMotion = false;
            playerAnimator.SetFloat("Speed", 0f);
        }

        if (seatStandPoint != null)
        {
            if (characterController != null && characterController.enabled)
                characterController.enabled = false;

            playerRoot.position = seatStandPoint.position;
            playerRoot.rotation = seatStandPoint.rotation;
        }

        var vehicleTransform = vehicle.Root;
        playerRoot.SetParent(vehicleTransform, true);

        if (playerController != null)
            playerController.enabled = false;

        vehicle.EnableControl();
        isInVehicle = true;
        lastEnterTime = Time.time;

        SetActiveCamera(pepelacCamera, playerCamera);

        Debug.Log($"[PlayerVehicleController] Вход в транспорт: {debugVehicleName}");
        OnEnteredVehicle?.Invoke(vehicle);

        UIServices.Get<InteractionHintUI>()?.SetVisible(false);
    }

    public void ExitVehicle()
    {
        if (!isInVehicle)
            return;

        Debug.Log($"[PlayerVehicleController] Выход из транспорта: {debugVehicleName}");

        currentVehicle?.DisableControl();

        if (originalParent != null)
            playerRoot.SetParent(originalParent, true);
        else
            playerRoot.SetParent(null, true);

        if (playerController != null)
            playerController.enabled = true;
        if (characterController != null && !characterController.enabled)
            characterController.enabled = true;

        if (playerAnimator != null)
            playerAnimator.applyRootMotion = true;

        isInVehicle = false;
        currentVehicle = null;
        currentSeat = null;
        currentSeatStandPoint = null;
        debugVehicleName = "";
        debugSeatName = "";

        SetActiveCamera(playerCamera, pepelacCamera);

        OnExitedVehicle?.Invoke();
        UIServices.Get<InteractionHintUI>()?.SetVisible(false);
    }

    private void SetActiveCamera(CinemachineVirtualCameraBase active, CinemachineVirtualCameraBase inactive)
    {
        if (active != null)
            active.Priority = activeCameraPriority;

        if (inactive != null)
            inactive.Priority = inactiveCameraPriority;
    }

    void LateUpdate()
    {
        if (isInVehicle && currentSeatStandPoint != null)
        {
            playerRoot.position = Vector3.Lerp(
                playerRoot.position,
                currentSeatStandPoint.position,
                posLerpSpeed * Time.deltaTime);

            playerRoot.rotation = Quaternion.Slerp(
                playerRoot.rotation,
                currentSeatStandPoint.rotation,
                rotLerpSpeed * Time.deltaTime);
        }
    }
}