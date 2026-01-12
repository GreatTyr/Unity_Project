using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
public class PlayerController : MonoBehaviour
{
    [Header("Input (assign InputActionReference assets)")]
    public InputActionReference moveAction;
    public InputActionReference sprintAction;
    public InputActionReference jumpAction;

    [Header("References")]
    public Transform cameraTarget; // optional

    [Header("Movement")]
    public float walkSpeed = 3.5f;
    public float sprintMultiplier = 1.8f;

    [Header("Rotation")]
    public bool rotateToCameraOnInput = true;
    public bool instantRotateToCamera = false;
    public float rotationSmoothTime = 0.12f;
    public bool debugLogsEnabled = true;

    [Header("Jump & Gravity")]
    public float jumpForce = 5f;
    public float gravity = -9.81f;
    public bool useCharacterControllerGround = true;

    [Header("Ground Check")]
    public LayerMask groundLayers = ~0;
    public Vector3 groundCheckOffset = new Vector3(0, -0.1f, 0);
    public float groundCheckRadius = 0.2f;

    [Header("Cursor")]
    public bool lockCursorOnStart = true;

    [Header("Animation")]
    [SerializeField] private string speedParam = "Speed"; // имя параметра в Animator
    private Animator animator;

    CharacterController cc;
    Vector2 moveInput = Vector2.zero;
    bool isSprinting = false;
    bool jumpRequested = false;
    float verticalVelocity = 0f;

    float currentVelocityAngle;
    float smoothYaw;

    void Awake()
    {
        cc = GetComponent<CharacterController>();

        // Попытка найти Animator на дочерней модели игрока (обычно модель — дочерний объект)
        animator = GetComponentInChildren<Animator>();
        if (animator == null)
        {
            Debug.LogWarning("[PlayerController] Animator not found on this GameObject or its children. " +
                             "Назначь Animator вручную или помести Animator на дочерний объект модели.");
        }
    }

    void OnEnable()
    {
        if (moveAction != null)
        {
            moveAction.action.performed += OnMovePerformed;
            moveAction.action.canceled += OnMoveCanceled;
            moveAction.action.Enable();
        }
        if (sprintAction != null)
        {
            sprintAction.action.performed += OnSprintPerformed;
            sprintAction.action.canceled += OnSprintCanceled;
            sprintAction.action.Enable();
        }
        if (jumpAction != null)
        {
            jumpAction.action.performed += OnJumpPerformed;
            jumpAction.action.Enable();
        }
    }

    void OnDisable()
    {
        if (moveAction != null)
        {
            moveAction.action.performed -= OnMovePerformed;
            moveAction.action.canceled -= OnMoveCanceled;
            moveAction.action.Disable();
        }
        if (sprintAction != null)
        {
            sprintAction.action.performed -= OnSprintPerformed;
            sprintAction.action.canceled -= OnSprintCanceled;
            sprintAction.action.Disable();
        }
        if (jumpAction != null)
        {
            jumpAction.action.performed -= OnJumpPerformed;
            jumpAction.action.Disable();
        }
    }

    void Start()
    {
        if (lockCursorOnStart)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
        smoothYaw = transform.eulerAngles.y;
    }

    void Update()
    {
        HandleGravityAndJump();

        Vector3 move = CalculateMoveVector();
        float baseSpeed = walkSpeed * (isSprinting ? sprintMultiplier : 1f);

        // Текущая горизонтальная скорость (в units/second)
        float currentHorizontalSpeed = baseSpeed * move.magnitude;

        // Передаём скорость в Animator (если найден)
        if (animator != null)
        {
            animator.SetFloat(speedParam, currentHorizontalSpeed);
        }

        Vector3 horizontalMotion = move * baseSpeed * Time.deltaTime;
        Vector3 verticalMotion = Vector3.up * verticalVelocity * Time.deltaTime;
        cc.Move(horizontalMotion + verticalMotion);

        if (rotateToCameraOnInput && moveInput != Vector2.zero)
        {
            RotateToCameraYaw();
        }

        if (debugLogsEnabled && moveInput != Vector2.zero)
        {
            LogDebugInfo();
        }
    }

    // Input
    void OnMovePerformed(InputAction.CallbackContext ctx) => moveInput = ctx.ReadValue<Vector2>();
    void OnMoveCanceled(InputAction.CallbackContext ctx) => moveInput = Vector2.zero;
    void OnSprintPerformed(InputAction.CallbackContext ctx) => isSprinting = ctx.ReadValueAsButton();
    void OnSprintCanceled(InputAction.CallbackContext ctx) => isSprinting = false;
    void OnJumpPerformed(InputAction.CallbackContext ctx) { if (ctx.performed) jumpRequested = true; }

    public void OnMove(InputAction.CallbackContext ctx) => moveInput = ctx.ReadValue<Vector2>();
    public void OnMove(Vector2 v) => moveInput = v;
    public void OnMove() => moveInput = Vector2.zero;
    public void OnSprint(InputAction.CallbackContext ctx) => isSprinting = ctx.ReadValueAsButton();
    public void OnSprint(bool v) => isSprinting = v;
    public void OnSprint() => isSprinting = true;
    public void OnJump(InputAction.CallbackContext ctx) { if (ctx.performed) jumpRequested = true; }
    public void OnJump() { jumpRequested = true; }

    Vector3 CalculateMoveVector()
    {
        Transform refTransform = cameraTarget;
        if (refTransform == null && Camera.main != null) refTransform = Camera.main.transform;

        Vector3 forward = Vector3.forward;
        Vector3 right = Vector3.right;

        if (refTransform != null)
        {
            forward = refTransform.forward; forward.y = 0f;
            if (forward.sqrMagnitude < 0.001f) forward = Vector3.forward;
            forward.Normalize();

            right = refTransform.right; right.y = 0f;
            if (right.sqrMagnitude < 0.001f) right = Vector3.right;
            right.Normalize();
        }

        Vector3 move = forward * moveInput.y + right * moveInput.x;
        if (move.sqrMagnitude > 1f) move.Normalize();
        return move;
    }

    void RotateToCameraYaw()
    {
        float cameraYaw = GetPreferedCameraYaw();
        float currentYaw = transform.eulerAngles.y;

        if (instantRotateToCamera)
        {
            transform.rotation = Quaternion.Euler(0f, cameraYaw, 0f);
            smoothYaw = cameraYaw;
            currentVelocityAngle = 0f;
            if (debugLogsEnabled) Debug.Log($"[PlayerController] Instant rotate to cameraYaw={cameraYaw:F3}");
        }
        else
        {
            smoothYaw = Mathf.SmoothDampAngle(currentYaw, cameraYaw, ref currentVelocityAngle, rotationSmoothTime);
            transform.rotation = Quaternion.Euler(0f, smoothYaw, 0f);
            if (debugLogsEnabled) Debug.Log($"[PlayerController] Smooth rotate: cameraYaw={cameraYaw:F3}, currentYaw={currentYaw:F3}, newYaw={smoothYaw:F3}");
        }
    }

    float GetPreferedCameraYaw()
    {
        float mainYaw = (Camera.main != null) ? Camera.main.transform.eulerAngles.y : float.NaN;

        if (cameraTarget == null)
            return !float.IsNaN(mainYaw) ? mainYaw : transform.eulerAngles.y;

        float targetYaw = cameraTarget.eulerAngles.y;
        if (float.IsNaN(mainYaw))
            return targetYaw;

        float diff = Mathf.Abs(Mathf.DeltaAngle(targetYaw, mainYaw));
        // If cameraTarget and main camera differ by more than threshold, prefer Main Camera (controlled by FreeLook)
        if (diff > 0.5f)
            return mainYaw;

        return targetYaw;
    }

    void LogDebugInfo()
    {
        string targetName = cameraTarget != null ? cameraTarget.name : "(null)";
        string mainCamName = Camera.main != null ? Camera.main.name : "(no MainCamera)";

        float targetYaw = cameraTarget != null ? cameraTarget.eulerAngles.y : float.NaN;
        float mainYaw = Camera.main != null ? Camera.main.transform.eulerAngles.y : float.NaN;
        float playerYaw = transform.eulerAngles.y;

        Debug.Log($"[DBG] move={moveInput} | cameraTarget={targetName} yaw={(cameraTarget != null ? targetYaw : float.NaN):0} | MainCamera={mainCamName} yaw={(Camera.main != null ? mainYaw : float.NaN):0} | playerYaw={playerYaw:F3}");
    }

    void HandleGravityAndJump()
    {
        bool isGrounded = IsGrounded();
        if (isGrounded)
        {
            if (verticalVelocity < 0f) verticalVelocity = -2f;
            if (jumpRequested) { verticalVelocity = jumpForce; jumpRequested = false; }
        }
        else verticalVelocity += gravity * Time.deltaTime;
    }

    bool IsGrounded()
    {
        if (useCharacterControllerGround) return cc.isGrounded;
        Vector3 origin = transform.position + groundCheckOffset;
        return Physics.CheckSphere(origin, groundCheckRadius, groundLayers, QueryTriggerInteraction.Ignore);
    }

    void OnDrawGizmosSelected()
    {
        if (!Application.isPlaying) return;
        Gizmos.color = Color.cyan;
        Vector3 origin = transform.position + groundCheckOffset;
        Gizmos.DrawWireSphere(origin, groundCheckRadius);
    }
}