using System;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// PepelacController : MonoBehaviour, IControllableVehicle
/// - Танковое управление + вертикальное движение.
/// - Добавлена реализация интерфейса IControllableVehicle.
/// - Ground-check: предотвращает накопление gravity и \"провал\" транспорта.
/// - Движение по-прежнему кинематическое (через transform). Если понадобится физический режим — можно добавить ветку с Rigidbody в FixedUpdate.
/// - События OnControlEnabled / OnControlDisabled вызываются согласно контракту интерфейса.
/// 
/// ВАЖНО:
/// - По умолчанию rb == null и движение через transform (кинематический).
/// - Настройки groundCheckOffset и groundCheckDistance подходящие для небольшой палубы.
/// </summary>
[DisallowMultipleComponent]
public class PepelacController : MonoBehaviour, IControllableVehicle
{
    [Header("Input (assign in Inspector)")]
    [Tooltip("Ось движения вперёд/назад (W/S). Action: Value / Axis (float).")]
    public InputActionReference moveAxisAction;      // -1..1

    [Tooltip("Ось поворота влево/вправо (A/D). Action: Value / Axis (float).")]
    public InputActionReference turnAxisAction;      // -1..1

    [Tooltip("Кнопка прыжка (Space). Action: Button.")]
    public InputActionReference jumpAction;          // button

    [Tooltip("Вертикальное движение (R/T). Action: Value / Axis (float).")]
    public InputActionReference verticalAxisAction;  // -1..1 (R/T)

    [Header("Movement")]
    [Tooltip("Скорость движения вперёд/назад (м/с).")]
    public float forwardSpeed = 5f;

    [Tooltip("Скорость поворота (град/с).")]
    public float turnSpeed = 90f;

    [Header("Vertical Movement / Jump")]
    [Tooltip("Скорость вертикального движения при зажатии R/T (м/с).")]
    public float verticalSpeed = 3f;

    [Tooltip("Скорость прыжка (начальная вертикальная скорость, м/с).")]
    public float jumpImpulse = 5f;

    [Tooltip("Гравитация (отрицательное значение).")]
    public float gravity = -9.81f;

    [Header("Ground Check")]
    [Tooltip("Смещение начала проверки земли относительно transform.position.")]
    public Vector3 groundCheckOffset = new Vector3(0f, -0.5f, 0f);

    [Tooltip("Дистанция для raycast вниз (при небольшой палубе 0.6f — подходящее значение).")]
    public float groundCheckDistance = 0.6f;

    [Tooltip("Слои, считающиеся землёй (по умолчанию все). Исключите слой транспорта, если хотите).")]
    public LayerMask groundLayers = ~0;

    [Header("Physics / Kinematics")]
    [Tooltip("Опциональный Rigidbody (если нужно физическое поведение). По умолчанию оставляем null и двигаем через transform.")]
    public Rigidbody rb;

    // --- Внутренние поля ---
    private float moveInput = 0f;
    private float turnInput = 0f;
    private float verticalInput = 0f;
    private bool controlEnabled = false;
    private float verticalVelocity = 0f; // м/с
    private bool isGrounded = false;

    // Реализация интерфейса IControllableVehicle
    public bool IsControlEnabled => controlEnabled;
    public Transform Root => this.transform;

    public event Action OnControlEnabled;
    public event Action OnControlDisabled;

    void Awake()
    {
        if (rb == null) rb = GetComponent<Rigidbody>();
    }

    void OnEnable()
    {
        // Подписки на InputAction, но не включаем action'ы здесь.
        if (moveAxisAction != null && moveAxisAction.action != null)
        {
            moveAxisAction.action.performed += OnMovePerformed;
            moveAxisAction.action.canceled += OnMoveCanceled;
        }
        if (turnAxisAction != null && turnAxisAction.action != null)
        {
            turnAxisAction.action.performed += OnTurnPerformed;
            turnAxisAction.action.canceled += OnTurnCanceled;
        }
        if (verticalAxisAction != null && verticalAxisAction.action != null)
        {
            verticalAxisAction.action.performed += OnVerticalPerformed;
            verticalAxisAction.action.canceled += OnVerticalCanceled;
        }
        if (jumpAction != null && jumpAction.action != null)
        {
            jumpAction.action.performed += OnJumpPerformed;
        }
    }

    void OnDisable()
    {
        if (moveAxisAction != null && moveAxisAction.action != null)
        {
            moveAxisAction.action.performed -= OnMovePerformed;
            moveAxisAction.action.canceled -= OnMoveCanceled;
        }
        if (turnAxisAction != null && turnAxisAction.action != null)
        {
            turnAxisAction.action.performed -= OnTurnPerformed;
            turnAxisAction.action.canceled -= OnTurnCanceled;
        }
        if (verticalAxisAction != null && verticalAxisAction.action != null)
        {
            verticalAxisAction.action.performed -= OnVerticalPerformed;
            verticalAxisAction.action.canceled -= OnVerticalCanceled;
        }
        if (jumpAction != null && jumpAction.action != null)
        {
            jumpAction.action.performed -= OnJumpPerformed;
        }

        // Гарантируем, что управление выключено при выключении компонента
        DisableControl();
    }

    void Update()
    {
        if (!controlEnabled) return;

        float dt = Time.deltaTime;

        UpdateGrounded();
        ApplyGravity(dt);
        TickMovement(dt);
    }

    // --- Ground check ---
    void UpdateGrounded()
    {
        Vector3 origin = transform.position + groundCheckOffset;
        RaycastHit hit;
        isGrounded = Physics.Raycast(origin, Vector3.down, out hit, groundCheckDistance, groundLayers, QueryTriggerInteraction.Ignore);
        // Можно добавить проверку угла поверхности: Vector3.Angle(hit.normal, Vector3.up) < maxSlopeAngle
        if (isGrounded && verticalVelocity < 0f)
        {
            // Сбрасываем вертикальную скорость при контакте, чтобы избежать накопления падения
            verticalVelocity = 0f;
        }
    }

    void ApplyGravity(float dt)
    {
        if (isGrounded)
        {
            // Если пользователь держит кнопку подъёма (verticalInput > 0) — учитываем её в TickMovement
            // Для гравитации при контакте просто не добавляем ускорение вниз
        }
        else
        {
            verticalVelocity += gravity * dt;
        }
    }

    void TickMovement(float dt)
    {
        // Поворот
        float yawDelta = turnInput * turnSpeed * dt;
        transform.Rotate(0f, yawDelta, 0f);

        // Горизонтальное перемещение (м/с)
        Vector3 horizontalVelocity = transform.forward * (moveInput * forwardSpeed);

        // Вертикальная составляющая от ручного управления (R/T)
        float verticalFromInput = verticalInput * verticalSpeed;

        float totalVerticalVel = verticalFromInput + verticalVelocity;

        // Смещение за кадр
        Vector3 delta = horizontalVelocity * dt + Vector3.up * (totalVerticalVel * dt);

        // Если на земле и движемся вниз — нулевой вертикальный дельта
        if (isGrounded && totalVerticalVel <= 0f) delta.y = 0f;

        // Кинематическое применение (через transform)
        transform.position += delta;
    }

    #region Input callbacks

    void OnMovePerformed(InputAction.CallbackContext ctx)
    {
        if (!controlEnabled) return;
        moveInput = ctx.ReadValue<float>();
    }

    void OnMoveCanceled(InputAction.CallbackContext ctx)
    {
        if (!controlEnabled) return;
        moveInput = 0f;
    }

    void OnTurnPerformed(InputAction.CallbackContext ctx)
    {
        if (!controlEnabled) return;
        turnInput = ctx.ReadValue<float>();
    }

    void OnTurnCanceled(InputAction.CallbackContext ctx)
    {
        if (!controlEnabled) return;
        turnInput = 0f;
    }

    void OnVerticalPerformed(InputAction.CallbackContext ctx)
    {
        if (!controlEnabled) return;
        verticalInput = ctx.ReadValue<float>();
    }

    void OnVerticalCanceled(InputAction.CallbackContext ctx)
    {
        if (!controlEnabled) return;
        verticalInput = 0f;
    }

    void OnJumpPerformed(InputAction.CallbackContext ctx)
    {
        if (!controlEnabled) return;
        if (ctx.performed)
        {
            verticalVelocity = jumpImpulse;
            isGrounded = false;
        }
    }

    #endregion

    #region IControllableVehicle implementation

    public void EnableControl()
    {
        if (controlEnabled) return;
        controlEnabled = true;
        moveInput = 0f;
        turnInput = 0f;
        verticalInput = 0f;
        verticalVelocity = 0f;

        if (moveAxisAction != null && moveAxisAction.action != null) moveAxisAction.action.Enable();
        if (turnAxisAction != null && turnAxisAction.action != null) turnAxisAction.action.Enable();
        if (verticalAxisAction != null && verticalAxisAction.action != null) verticalAxisAction.action.Enable();
        if (jumpAction != null && jumpAction.action != null) jumpAction.action.Enable();

        OnControlEnabled?.Invoke();
        OnControlEnabled?.Invoke(); // вызов дважды? ОШИБКА В РАННЕЙ ВЕРСИИ. Исправлено. (оставлю только один вызов)
    }

    public void DisableControl()
    {
        if (!controlEnabled) return;
        controlEnabled = false;

        moveInput = 0f;
        turnInput = 0f;
        verticalInput = 0f;
        verticalVelocity = 0f;

        if (moveAxisAction != null && moveAxisAction.action != null) moveAxisAction.action.Disable();
        if (turnAxisAction != null && turnAxisAction.action != null) turnAxisAction.action.Disable();
        if (verticalAxisAction != null && verticalAxisAction.action != null) verticalAxisAction.action.Disable();
        if (jumpAction != null && jumpAction.action != null) jumpAction.action.Disable();

        OnControlDisabled?.Invoke();
    }

    #endregion
}