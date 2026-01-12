using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// CameraOnWorldMap
/// Объединяет: Follow (только когда target движется), WASD, Zoom, Rotation.
/// Изменение: Follow выполняется только если target действительно перемещается (или followWhileTargetMovingOnly == false).
/// </summary>
[AddComponentMenu("Camera/CameraOnWorldMap")]
public class CameraOnWorldMap : MonoBehaviour
{
    [Header("Target / Follow")]
    public Transform target;                     // PlayerOnWorldMap
    public bool enableFollow = true;
    public bool followWhileTargetMovingOnly = true; // NEW: follow only when target moves
    public float targetMovementThreshold = 0.01f;  // meters per frame to consider as movement
    public float gracePeriodAfterStop = 0.2f;      // seconds to continue following after target stops
    public bool useInitialOffset = true;
    public Vector3 offset = new Vector3(0f, 10f, -10f);
    public bool followOnlyXZ = true;
    public bool smoothFollow = true;
    public float smoothTime = 0.15f;
    public bool snapOnStart = true;

    [Header("Bounds (optional)")]
    public bool useBounds = false;
    public Vector2 minXZ = new Vector2(-50f, -50f);
    public Vector2 maxXZ = new Vector2(50f, 50f);

    [Header("WASD Move")]
    public bool enableWASD = true;
    public float moveSpeed = 30f;

    [Header("Zoom")]
    public bool enableZoom = true;
    public float zoomSpeed = 500f;
    public float minY = 10f;
    public float maxY = 100f;

    [Header("Rotation (mouse)")]
    public bool enableRotation = true;
    public float rotationSpeed = 0.5f * 500f;
    public float minX = 30f;
    public float maxX = 85f;

    [Header("General")]
    public bool debugLogs = false;
    public bool lockCursorOnStart = false;

    // Input
    private InputSystem_Actions inputActions;
    private Vector2 wasdMoveInput = Vector2.zero;
    private float zoomInput = 0f;

    // Rotation state
    private bool isRotating = false;
    private Vector2 lastMousePosition;

    // Follow state
    private Vector3 velocity = Vector3.zero;
    private Vector3 desiredPosition;
    private Vector3 lastTargetPosition;
    private float timeSinceTargetStopped = Mathf.Infinity;
    private bool targetIsMoving = false;
    private float verticalVelocity = 0f;

    void Awake()
    {
        inputActions = new InputSystem_Actions();
    }

    void OnEnable()
    {
        inputActions.Enable();

        if (enableWASD)
        {
            inputActions.CameraOnWorldMap.Move.performed += OnMovePerformed;
            inputActions.CameraOnWorldMap.Move.canceled += OnMoveCanceled;
        }
        if (enableZoom)
        {
            inputActions.CameraOnWorldMap.Zoom.performed += OnZoomPerformed;
            inputActions.CameraOnWorldMap.Zoom.canceled += OnZoomCanceled;
        }

        // init lastTargetPosition
        if (target != null) lastTargetPosition = target.position;
    }

    void OnDisable()
    {
        if (enableWASD)
        {
            inputActions.CameraOnWorldMap.Move.performed -= OnMovePerformed;
            inputActions.CameraOnWorldMap.Move.canceled -= OnMoveCanceled;
        }
        if (enableZoom)
        {
            inputActions.CameraOnWorldMap.Zoom.performed -= OnZoomPerformed;
            inputActions.CameraOnWorldMap.Zoom.canceled -= OnZoomCanceled;
        }

        inputActions.Disable();
    }

    void Start()
    {
        if (lockCursorOnStart)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        if (useInitialOffset && target != null)
        {
            offset = transform.position - target.position;
        }

        if (snapOnStart && target != null && enableFollow)
        {
            desiredPosition = ComputeDesiredPosition();
            transform.position = desiredPosition;
        }

        if (target != null) lastTargetPosition = target.position;
    }

    void Update()
    {
        HandleRotationInput();
        HandleZoomInput();
        HandleWASDMovement();

        UpdateTargetMovementState();
    }

    void LateUpdate()
    {
        if (enableFollow && target != null)
        {
            // decide if follow should apply this frame
            bool shouldFollow = !followWhileTargetMovingOnly || targetIsMoving || timeSinceTargetStopped < gracePeriodAfterStop;

            if (shouldFollow)
            {
                ApplyFollow();
            }
            else
            {
                if (debugLogs) Debug.Log("[CameraOnWorldMap] Skipping follow since target is stationary and followWhileTargetMovingOnly==true");
            }
        }
    }

    // ---------- Input Callbacks ----------
    private void OnMovePerformed(InputAction.CallbackContext ctx) => wasdMoveInput = ctx.ReadValue<Vector2>();
    private void OnMoveCanceled(InputAction.CallbackContext ctx) => wasdMoveInput = Vector2.zero;
    private void OnZoomPerformed(InputAction.CallbackContext ctx) => zoomInput = ctx.ReadValue<float>();
    private void OnZoomCanceled(InputAction.CallbackContext ctx) => zoomInput = 0f;

    // ---------- Handlers ----------
    void HandleWASDMovement()
    {
        if (!enableWASD) return;
        if (wasdMoveInput == Vector2.zero) return;

        Vector3 right = transform.right; right.y = 0f; right.Normalize();
        Vector3 forward = transform.forward; forward.y = 0f; forward.Normalize();

        Vector3 move = right * wasdMoveInput.x + forward * wasdMoveInput.y;
        if (move.sqrMagnitude > 1f) move.Normalize();

        transform.position += move * moveSpeed * Time.deltaTime;

        if (debugLogs) Debug.Log($"[CameraOnWorldMap] WASD move {wasdMoveInput} -> delta {move * moveSpeed * Time.deltaTime}");
    }

    void HandleZoomInput()
    {
        if (!enableZoom) return;
        if (Mathf.Abs(zoomInput) <= 0.001f) return;

        float newY = Mathf.Clamp(transform.position.y - zoomInput * zoomSpeed * Time.deltaTime, minY, maxY);
        transform.position = new Vector3(transform.position.x, newY, transform.position.z);

        if (debugLogs) Debug.Log($"[CameraOnWorldMap] Zoom input {zoomInput} -> newY {newY}");
    }

    void HandleRotationInput()
    {
        if (!enableRotation) return;

        if (Mouse.current != null && Mouse.current.middleButton.isPressed)
        {
            if (!isRotating)
            {
                isRotating = true;
                lastMousePosition = Mouse.current.position.ReadValue();
                if (debugLogs) Debug.Log("[CameraOnWorldMap] Rotation started");
            }
            else
            {
                Vector2 currentMousePosition = Mouse.current.position.ReadValue();
                Vector2 delta = currentMousePosition - lastMousePosition;

                float newX = transform.eulerAngles.x - delta.y * rotationSpeed * Time.deltaTime;
                newX = ClampAngle(newX, minX, maxX);

                float newY = transform.eulerAngles.y + delta.x * rotationSpeed * Time.deltaTime;

                transform.rotation = Quaternion.Euler(newX, newY, 0f);

                lastMousePosition = currentMousePosition;

                if (debugLogs) Debug.Log($"[CameraOnWorldMap] Rotated to x={newX:F2}, y={newY:F2}");
            }
        }
        else
        {
            if (isRotating && debugLogs) Debug.Log("[CameraOnWorldMap] Rotation ended");
            isRotating = false;
        }
    }

    void ApplyFollow()
    {
        desiredPosition = ComputeDesiredPosition();

        if (useBounds)
        {
            desiredPosition = ApplyBounds(desiredPosition);
        }

        if (smoothFollow)
        {
            transform.position = Vector3.SmoothDamp(transform.position, desiredPosition, ref velocity, smoothTime);
        }
        else
        {
            transform.position = desiredPosition;
        }

        if (debugLogs) Debug.Log($"[CameraOnWorldMap] Follow applied to {desiredPosition}");
    }

    Vector3 ComputeDesiredPosition()
    {
        Vector3 tgt = target.position + offset;
        if (followOnlyXZ)
        {
            tgt.y = transform.position.y;
        }
        return tgt;
    }

    Vector3 ApplyBounds(Vector3 pos)
    {
        float x = Mathf.Clamp(pos.x, minXZ.x, maxXZ.x);
        float z = Mathf.Clamp(pos.z, minXZ.y, maxXZ.y);
        return new Vector3(x, pos.y, z);
    }

    // ---------- Target movement detection ----------
    void UpdateTargetMovementState()
    {
        if (target == null) { targetIsMoving = false; return; }

        Vector3 delta = target.position - lastTargetPosition;
        float dist = delta.magnitude;

        if (dist > targetMovementThreshold)
        {
            targetIsMoving = true;
            timeSinceTargetStopped = 0f;
            if (debugLogs) Debug.Log($"[CameraOnWorldMap] Target moved by {dist:F3}, enabling follow");
        }
        else
        {
            // increment timer since last significant movement
            timeSinceTargetStopped += Time.deltaTime;
            if (timeSinceTargetStopped >= gracePeriodAfterStop)
            {
                if (targetIsMoving && debugLogs) Debug.Log("[CameraOnWorldMap] Target considered stopped; follow will stop after grace period");
                targetIsMoving = false;
            }
        }

        lastTargetPosition = target.position;
    }

    // ---------- Utilities ----------
    static float ClampAngle(float angle, float min, float max)
    {
        angle = NormalizeAngle(angle);
        min = NormalizeAngle(min);
        max = NormalizeAngle(max);

        if (min <= max)
            return Mathf.Clamp(angle, min, max);

        if (angle > min || angle < max) return angle;

        float distToMin = Mathf.Abs(Mathf.DeltaAngle(angle, min));
        float distToMax = Mathf.Abs(Mathf.DeltaAngle(angle, max));
        return distToMin < distToMax ? min : max;
    }

    static float NormalizeAngle(float a)
    {
        a = a % 360f;
        if (a < 0f) a += 360f;
        return a;
    }
}