using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// PlayerController — обновлённая версия с поддержкой jump buffering и опцией удержания.
/// Исправляет баг: когда игрок нажимает кнопку прыжка в воздухе (до приземления),
/// текущая реализация могла выполнить второй прыжок сразу при касании земли (неожиданно для игрока).
/// 
/// Теперь:
/// - При нажатии jumpAction устанавливается флаг jumpRequested и время jumpRequestedTime = Time.time.
/// - В Update/HandleGravityAndJump: прыжок выполняется только если IsGrounded() И (jumpRequested && Time.time - jumpRequestedTime <= jumpBufferTime).
/// - После выполнения прыжка флаг сбрасывается.
/// - Если игрок удерживает кнопку прыжка, можно контролировать поведение через acceptHoldToRepeat (по умолчанию false).
/// </summary>
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

    [Header("Jump & Gravity")]
    public float jumpForce = 5f;
    public float gravity = -9.81f;
    public bool useCharacterControllerGround = true;

    [Header("Jump Buffer / Hold behavior")]
    [Tooltip("Время в секундах, в течение которого нажатие прыжка до приземления будет принято (buffer).")]
    public float jumpBufferTime = 0.15f;

    [Tooltip("Если true — удержание кнопки прыжка приведёт к повторным прыжкам при каждом приземлении (обычно false).")]
    public bool acceptHoldToRepeat = false;

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

    // Прыжковая логика
    bool jumpRequested = false;
    float jumpRequestedTime = -999f;
    float verticalVelocity = 0f;

    float currentVelocityAngle;
    float smoothYaw;

    void Awake()
    {
        cc = GetComponent<CharacterController>();
        animator = GetComponentInChildren<Animator>();
        if (animator == null)
            Debug.LogWarning("[PlayerController] Animator not found on this GameObject or its children.");
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
            jumpAction.action.canceled += OnJumpCanceled;
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
            jumpAction.action.canceled -= OnJumpCanceled;
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

        float currentHorizontalSpeed = baseSpeed * move.magnitude;

        if (animator != null)
        {
            int hash = Animator.StringToHash(speedParam);
            animator.SetFloat(hash, currentHorizontalSpeed);
        }

        Vector3 horizontalMotion = move * baseSpeed * Time.deltaTime;
        Vector3 verticalMotion = Vector3.up * verticalVelocity * Time.deltaTime;
        cc.Move(horizontalMotion + verticalMotion);

        if (rotateToCameraOnInput && moveInput != Vector2.zero)
        {
            RotateToCameraYaw();
        }
    }

    // Input callbacks
    void OnMovePerformed(InputAction.CallbackContext ctx) => moveInput = ctx.ReadValue<Vector2>();
    void OnMoveCanceled(InputAction.CallbackContext ctx) => moveInput = Vector2.zero;
    void OnSprintPerformed(InputAction.CallbackContext ctx) => isSprinting = ctx.ReadValueAsButton();
    void OnSprintCanceled(InputAction.CallbackContext ctx) => isSprinting = false;

    void OnJumpPerformed(InputAction.CallbackContext ctx)
    {
        if (ctx.performed)
        {
            // Записываем запрос на прыжок и время
            jumpRequested = true;
            jumpRequestedTime = Time.time;
        }
    }

    void OnJumpCanceled(InputAction.CallbackContext ctx)
    {
        // При отпускании кнопки: если hold не разрешён — сбрасываем запрос
        if (!acceptHoldToRepeat)
        {
            jumpRequested = false;
        }
    }

    void HandleGravityAndJump()
    {
        bool grounded = IsGrounded();

        if (grounded)
        {
            if (verticalVelocity < 0f) verticalVelocity = -2f;

            // Выполняем прыжок только если есть валидный запрос и он не слишком стар (jumpBuffer)
            if (jumpRequested && (Time.time - jumpRequestedTime) <= jumpBufferTime)
            {
                verticalVelocity = jumpForce;
                jumpRequested = acceptHoldToRepeat; // если hold не разрешён — сбрасываем
                // если hold разрешён, оставляем флаг в зависимости от логики (можно захотеть сбрасывать тоже)
            }
            else
            {
                // Если запрос старый — сбрасываем
                if ((Time.time - jumpRequestedTime) > jumpBufferTime) jumpRequested = false;
            }
        }
        else
        {
            verticalVelocity += gravity * Time.deltaTime;
        }
    }

    bool IsGrounded()
    {
        if (useCharacterControllerGround) return cc.isGrounded;
        Vector3 origin = transform.position + groundCheckOffset;
        return Physics.CheckSphere(origin, groundCheckRadius, groundLayers, QueryTriggerInteraction.Ignore);
    }

    // Остальные методы (RotateToCameraYaw, CalculateMoveVector и т.д.) остаются прежними из оригинала.
    // Для компактности они не изменялись логически — оставим их как в исходной версии.
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
        }
        else
        {
            smoothYaw = Mathf.SmoothDampAngle(currentYaw, cameraYaw, ref currentVelocityAngle, rotationSmoothTime);
            transform.rotation = Quaternion.Euler(0f, smoothYaw, 0f);
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
        if (diff > 0.5f)
            return mainYaw;

        return targetYaw;
    }
}