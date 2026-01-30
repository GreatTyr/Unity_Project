using System;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// PepelacController — управление транспортом Pepelac через физику (Rigidbody) или fallback-трансформ.
/// Логика:
/// - Танковое движение: вперёд/назад + поворот + стрейф.
/// - Прыжок, hover-контроль высоты (PD-контроллер).
/// - Все параметры (скорости, силы, hover, масса и т.д.) берутся из PepelacMain.
/// 
/// ВАЖНО:
/// - Этот компонент НЕ является местом настройки чисел.
///   Все численные параметры задаются в PepelacMain (паспорт + runtime-стат).
/// - Здесь только исполнение логики поверх параметров из PepelacMain.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody))]
public class PepelacController : MonoBehaviour, IControllableVehicle
{
    [Header("Stats / Config source")]
    [Tooltip("Основной компонент с параметрами Pepelac (паспорт + runtime-стат).")]
    public PepelacMain main;

    [Header("Input (назначить в Инспекторе)")]
    [Tooltip("Ось движения вперед/назад (W/S). Action: Value (float).")]
    public InputActionReference moveAxisAction;
    [Tooltip("Ось поворота влево/вправо (A/D). Action: Value (float).")]
    public InputActionReference turnAxisAction;
    [Tooltip("Кнопка прыжка (Space). Action: Button.")]
    public InputActionReference jumpAction;
    [Tooltip("Ось стрейфа Q/E (опционально). Action: Value (float) или Vector2).")]
    public InputActionReference strafeAxisAction;

    [Header("Hover Height Input")]
    [Tooltip("Кнопка удержания для повышения целевой высоты (например R). Тип: Button, interaction Hold.")]
    public InputActionReference riseAction;
    [Tooltip("Кнопка удержания для понижения целевой высоты (например T). Тип: Button, interaction Hold.")]
    public InputActionReference lowerAction;

    [Header("Ground check (config для raycast)")]
    [Tooltip("Смещение origin для raycast вниз от текущей позиции (локальный offset).")]
    public Vector3 groundCheckOffset = new Vector3(0f, -0.5f, 0f);
    [Tooltip("Дистанция raycast вниз для проверки земли.")]
    public float groundCheckDistance = 0.6f;
    [Tooltip("Слои, считающиеся землёй.")]
    public LayerMask groundLayers = ~0;

    // -----------------------------
    // Внутренние поля ввода
    // -----------------------------
    private float moveInput = 0f;
    private float turnInput = 0f;
    private float strafeInput = 0f;

    // Старый "вертикальный" ввод оставим как fallback, если где-то нужен
    private float legacyVerticalInput = 0f;

    // Флаги управления
    private bool controlEnabled = false;

    // Состояние hover / вертикали
    private float baseGroundY = 0f;          // Y поверхности под Pepelac
    private float targetHoverOffset = 0f;    // Относительная высота над baseGroundY
    private bool isGrounded = false;
    private float hoverLockUntil = 0f;       // Time.time, до которого hover заблокирован (после прыжка)

    // Флаги rise/lower
    private bool risePressed = false;
    private bool lowerPressed = false;

    // Для fallback-режима (без Rigidbody)
    private float currentVerticalVelocity = 0f;
    private float hoverVelocityRef = 0f;

    // Snap таймер (усиленный PD после отпускания rise/lower)
    private float snapBoostUntil = 0f;

    // IControllableVehicle
    public bool IsControlEnabled => controlEnabled;
    public Transform Root => this.transform;
    public event Action OnControlEnabled;
    public event Action OnControlDisabled;

    [Tooltip("Rigidbody транспорта. Если не назначен, будет взят с этого объекта.")]
    public Rigidbody rb;

    // =========================
    // Жизненный цикл
    // =========================
    private void Awake()
    {
        if (rb == null)
            rb = GetComponent<Rigidbody>();

        if (main == null)
            main = GetComponent<PepelacMain>();

        if (main == null)
        {
            Debug.LogError("[PepelacController] PepelacMain не найден на объекте. Параметры будут дефолтными.");
        }
        else
        {
            // Настройка Rigidbody по данным из PepelacMain
            if (rb != null)
            {
                // Центр масс
                if (main.centerOfMassOffset != Vector3.zero)
                    rb.centerOfMass += main.centerOfMassOffset;

                // Блокировка наклонов
                if (main.freezeTiltAxes)
                    rb.constraints |= RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;

                // Масса
                main.RecalculateTotalMass();
                rb.mass = (main.currentTotalMass > 0f ? main.currentTotalMass : main.baseMass);
            }

            // Ground Y
            baseGroundY = QuerySurfaceYUnder(transform.position, fallbackToCurrentY: true);
            targetHoverOffset = 0f;
        }

        // Скейлим настройки ground-люгики под локальные поля
        // (groundCheckOffset/Distance/Layers могут быть и в PepelacMain, но оставим их здесь как локальные)
    }

    private void OnEnable()
    {
        // Подписки на Input System
        if (moveAxisAction?.action != null)
        {
            moveAxisAction.action.performed += OnMovePerformed;
            moveAxisAction.action.canceled += OnMoveCanceled;
        }
        if (turnAxisAction?.action != null)
        {
            turnAxisAction.action.performed += OnTurnPerformed;
            turnAxisAction.action.canceled += OnTurnCanceled;
        }
        if (strafeAxisAction?.action != null)
        {
            strafeAxisAction.action.performed += OnStrafePerformed;
            strafeAxisAction.action.canceled += OnStrafeCanceled;
        }
        if (jumpAction?.action != null)
        {
            jumpAction.action.performed += OnJumpPerformed;
        }
        if (riseAction?.action != null)
        {
            riseAction.action.performed += OnRisePerformed;
            riseAction.action.canceled += OnRiseCanceled;
        }
        if (lowerAction?.action != null)
        {
            lowerAction.action.performed += OnLowerPerformed;
            lowerAction.action.canceled += OnLowerCanceled;
        }
    }

    private void OnDisable()
    {
        // Отписки от Input System
        if (moveAxisAction?.action != null)
        {
            moveAxisAction.action.performed -= OnMovePerformed;
            moveAxisAction.action.canceled -= OnMoveCanceled;
        }
        if (turnAxisAction?.action != null)
        {
            turnAxisAction.action.performed -= OnTurnPerformed;
            turnAxisAction.action.canceled -= OnTurnCanceled;
        }
        if (strafeAxisAction?.action != null)
        {
            strafeAxisAction.action.performed -= OnStrafePerformed;
            strafeAxisAction.action.canceled -= OnStrafeCanceled;
        }
        if (jumpAction?.action != null)
        {
            jumpAction.action.performed -= OnJumpPerformed;
        }
        if (riseAction?.action != null)
        {
            riseAction.action.performed -= OnRisePerformed;
            riseAction.action.canceled -= OnRiseCanceled;
        }
        if (lowerAction?.action != null)
        {
            lowerAction.action.performed -= OnLowerPerformed;
            lowerAction.action.canceled -= OnLowerCanceled;
        }

        // При выключении компонента гарантированно отключаем управление
        DisableControl();
    }

    private void Update()
    {
        if (!controlEnabled) return;

        UpdateGrounded();
        HandleHoverInput(Time.deltaTime);
    }

    private void FixedUpdate()
    {
        if (!controlEnabled) return;

        bool useRB = main != null ? main.useRigidbody : true;

        if (useRB && rb != null && rb.isKinematic == false)
        {
            TickPhysicsMovement(Time.fixedDeltaTime);
        }
        else
        {
            TickKinematicMovement(Time.fixedDeltaTime);
        }
    }

    // =========================
    // Обработка земли
    // =========================
    private void UpdateGrounded()
    {
        Vector3 origin = transform.position + groundCheckOffset;
        RaycastHit hit;
        isGrounded = Physics.Raycast(origin, Vector3.down, out hit, groundCheckDistance, groundLayers, QueryTriggerInteraction.Ignore);
    }

    private float QuerySurfaceYUnder(Vector3 worldPos, bool fallbackToCurrentY = false)
    {
        RaycastHit hit;
        Vector3 origin = worldPos + Vector3.up * 0.5f; // чуть выше текущей позиции
        if (Physics.Raycast(origin, Vector3.down, out hit, 200f, groundLayers, QueryTriggerInteraction.Ignore))
        {
            return hit.point.y;
        }
        return fallbackToCurrentY ? worldPos.y : 0f;
    }

    // =========================
    // Hover & ввод по высоте
    // =========================
    private void HandleHoverInput(float dt)
    {
        if (main == null) return;

        // Обновляем базовую поверхность под Pepelac
        if (main.useBaseGroundY)
        {
            baseGroundY = QuerySurfaceYUnder(transform.position, fallbackToCurrentY: true);
        }

        // Вариант с legacyVerticalInput оставлен как fallback
        bool isRising = risePressed || (riseAction?.action == null && legacyVerticalInput > 0.1f);
        bool isLowering = lowerPressed || (lowerAction?.action == null && legacyVerticalInput < -0.1f);

        // hover заблокирован (например, после прыжка)?
        bool hoverTemporarilyBlocked = Time.time < hoverLockUntil;

        // Изменяем целевой offset только от ввода
        if (isRising)
        {
            targetHoverOffset += main.riseSpeed * dt;
            if (targetHoverOffset > main.maxHoverOffset) targetHoverOffset = main.maxHoverOffset;
        }
        else if (isLowering)
        {
            targetHoverOffset -= main.lowerSpeed * dt;
            if (targetHoverOffset < 0f) targetHoverOffset = 0f;
        }

        // Если hover заблокирован, всё равно держим targetHoverOffset,
        // но вертикальное удержание не применяем, пока не истечёт hoverLockUntil.
    }

    // =========================
    // Движение через Rigidbody
    // =========================
    private void TickPhysicsMovement(float dt)
    {
        if (rb == null || main == null) return;

        // --------- Поворот (танковый) ---------
        float yawDelta = turnInput * main.turnSpeed * dt;
        Quaternion currentRot = rb.rotation;
        Quaternion deltaRot = Quaternion.Euler(0f, yawDelta, 0f);
        Quaternion targetRot = deltaRot * currentRot;

        if (main.rotationSlerpSpeed <= 0f)
        {
            rb.MoveRotation(targetRot);
        }
        else
        {
            Quaternion slerped = Quaternion.Slerp(currentRot, targetRot, main.rotationSlerpSpeed * dt);
            rb.MoveRotation(slerped);
        }

        // --------- Горизонтальная скорость ---------
        float desiredForward = moveInput * main.forwardSpeed;
        float desiredStrafe = strafeInput * main.strafeSpeed;

        Vector3 velocity = rb.linearVelocity;
        Vector3 localVel = transform.InverseTransformDirection(velocity);
        float currentForward = localVel.z;
        float currentStrafe = localVel.x;

        float deltaForward = desiredForward - currentForward;
        float deltaStrafe = desiredStrafe - currentStrafe;

        float maxDeltaSpeed = main.maxHorizontalAcceleration * dt;
        deltaForward = Mathf.Clamp(deltaForward, -maxDeltaSpeed, maxDeltaSpeed);
        deltaStrafe = Mathf.Clamp(deltaStrafe, -maxDeltaSpeed, maxDeltaSpeed);

        Vector3 deltaVelLocal = new Vector3(deltaStrafe, 0f, deltaForward);
        Vector3 requiredAccelLocal = deltaVelLocal / Mathf.Max(dt, 0.0001f);
        Vector3 requiredForceLocal = requiredAccelLocal * rb.mass;

        rb.AddRelativeForce(requiredForceLocal, ForceMode.Force);

        // --------- Вертикаль / Hover ---------
        ApplyVerticalPhysics(dt);
    }

    /// <summary>
    /// Вертикальная физика: прыжок + hover (PD-контроллер по высоте).
    /// </summary>
    private void ApplyVerticalPhysics(float dt)
    {
        if (rb == null || main == null) return;

        bool hoverTemporarilyBlocked = Time.time < hoverLockUntil;
        bool wantHover = targetHoverOffset > 0f;

        if (main.holdHoverPreventsGravity && wantHover && !hoverTemporarilyBlocked)
        {
            float targetY = baseGroundY + targetHoverOffset;
            float currentY = rb.position.y;
            float error = targetY - currentY;
            float velY = rb.linearVelocity.y;

            float kp = main.verticalSpringKp;
            float kd = main.verticalSpringKd;

            // Snap-усиление
            if (main.snapOnRelease && Time.time < snapBoostUntil)
            {
                kp *= main.snapForceMultiplier;
                kd *= main.snapForceMultiplier;
            }

            float forceY = kp * error - kd * velY;
            forceY = Mathf.Clamp(forceY, -main.maxVerticalForce, main.maxVerticalForce);

            rb.AddForce(Vector3.up * forceY, ForceMode.Force);
        }
        else
        {
            // rely on нормальную гравитацию Rigidbody
        }
    }

    // =========================
    // Fallback (transform) для случаев без Rigidbody
    // =========================
    private void TickKinematicMovement(float dt)
    {
        if (main == null) return;

        // Поворот
        float yawDelta = turnInput * main.turnSpeed * dt;
        transform.Rotate(0f, yawDelta, 0f);

        // Горизонтальное движение
        Vector3 forwardVel = transform.forward * (moveInput * main.forwardSpeed);
        Vector3 rightVel = transform.right * (strafeInput * main.strafeSpeed);

        // Hover / гравитация (упрощённо)
        bool hoverTemporarilyBlocked = Time.time < hoverLockUntil;
        bool wantHover = targetHoverOffset > 0f;
        float verticalVel = 0f;

        if (main.holdHoverPreventsGravity && wantHover && !hoverTemporarilyBlocked)
        {
            float targetY = baseGroundY + targetHoverOffset;
            Vector3 pos = transform.position;
            pos.y = targetY;
            transform.position = pos;
            currentVerticalVelocity = 0f;
        }
        else
        {
            if (isGrounded)
            {
                if (currentVerticalVelocity < 0f) currentVerticalVelocity = 0f;
            }
            else
            {
                currentVerticalVelocity += main.gravity * dt;
            }
            verticalVel = currentVerticalVelocity;
        }

        Vector3 totalVel = forwardVel + rightVel + Vector3.up * verticalVel;
        Vector3 delta = totalVel * dt;
        if (isGrounded && verticalVel <= 0f) delta.y = 0f;
        transform.position += delta;
    }

    // =========================
    // Обработчики ввода
    // =========================
    private void OnMovePerformed(InputAction.CallbackContext ctx)
    {
        if (!controlEnabled) return;
        moveInput = ctx.ReadValue<float>();
    }

    private void OnMoveCanceled(InputAction.CallbackContext ctx)
    {
        if (!controlEnabled) return;
        moveInput = 0f;
    }

    private void OnTurnPerformed(InputAction.CallbackContext ctx)
    {
        if (!controlEnabled) return;
        turnInput = ctx.ReadValue<float>();
    }

    private void OnTurnCanceled(InputAction.CallbackContext ctx)
    {
        if (!controlEnabled) return;
        turnInput = 0f;
    }

    private void OnStrafePerformed(InputAction.CallbackContext ctx)
    {
        if (!controlEnabled) return;

        if (ctx.control != null && ctx.control.valueType == typeof(Vector2))
        {
            Vector2 v = ctx.ReadValue<Vector2>();
            strafeInput = Mathf.Clamp(v.x, -1f, 1f);
        }
        else
        {
            strafeInput = ctx.ReadValue<float>();
        }
    }

    private void OnStrafeCanceled(InputAction.CallbackContext ctx)
    {
        if (!controlEnabled) return;
        strafeInput = 0f;
    }

    /// <summary>
    /// Прыжок: задаём вертикальный импульс и временно блокируем hover.
    /// </summary>
    private void OnJumpPerformed(InputAction.CallbackContext ctx)
    {
        if (!controlEnabled) return;
        if (!ctx.performed) return;
        if (main == null) return;

        if (rb != null && main.useRigidbody)
        {
            rb.AddForce(Vector3.up * main.jumpImpulse, ForceMode.VelocityChange);
        }
        else
        {
            currentVerticalVelocity = main.jumpImpulse;
        }

        hoverLockUntil = Time.time + main.jumpBreaksHoverDuration;
    }

    // --- Rise / Lower ---
    private void OnRisePerformed(InputAction.CallbackContext ctx)
    {
        if (!controlEnabled) return;

        if (ctx.control != null && ctx.control.valueType == typeof(float))
        {
            float v = ctx.ReadValue<float>();
            risePressed = v > 0.1f;
        }
        else
        {
            risePressed = true;
        }
    }

    private void OnRiseCanceled(InputAction.CallbackContext ctx)
    {
        if (!controlEnabled) return;
        risePressed = false;

        float currentY = transform.position.y;
        targetHoverOffset = Mathf.Clamp(currentY - baseGroundY, 0f, main != null ? main.maxHoverOffset : 50f);
        currentVerticalVelocity = 0f;
        hoverVelocityRef = 0f;

        if (main != null && main.snapOnRelease)
        {
            snapBoostUntil = Time.time + main.snapDuration;
        }
    }

    private void OnLowerPerformed(InputAction.CallbackContext ctx)
    {
        if (!controlEnabled) return;

        if (ctx.control != null && ctx.control.valueType == typeof(float))
        {
            float v = ctx.ReadValue<float>();
            lowerPressed = v > 0.1f;
        }
        else
        {
            lowerPressed = true;
        }
    }

    private void OnLowerCanceled(InputAction.CallbackContext ctx)
    {
        if (!controlEnabled) return;
        lowerPressed = false;

        float currentY = transform.position.y;
        targetHoverOffset = Mathf.Clamp(currentY - baseGroundY, 0f, main != null ? main.maxHoverOffset : 50f);
        currentVerticalVelocity = 0f;
        hoverVelocityRef = 0f;

        if (main != null && main.snapOnRelease)
        {
            snapBoostUntil = Time.time + main.snapDuration;
        }
    }

    // Fallback-хуки для старого verticalAxis (если где-то используется)
    public void OnLegacyVerticalPerformed(float value) { legacyVerticalInput = value; }
    public void OnLegacyVerticalCanceled() { legacyVerticalInput = 0f; }

    // =========================
    // IControllableVehicle
    // =========================
    public void EnableControl()
    {
        if (controlEnabled) return;
        controlEnabled = true;

        moveInput = turnInput = strafeInput = 0f;
        legacyVerticalInput = 0f;
        currentVerticalVelocity = 0f;
        hoverLockUntil = 0f;
        snapBoostUntil = 0f;

        baseGroundY = QuerySurfaceYUnder(transform.position, fallbackToCurrentY: true);
        if (targetHoverOffset < 0f) targetHoverOffset = 0f;

        // Пересчёт массы по текущему состоянию
        if (main != null)
        {
            main.RecalculateTotalMass();
            if (rb != null)
            {
                rb.mass = (main.currentTotalMass > 0f ? main.currentTotalMass : main.baseMass);
                rb.isKinematic = false;
                rb.useGravity = true;
            }
        }

        // Включаем Input Actions
        moveAxisAction?.action?.Enable();
        turnAxisAction?.action?.Enable();
        strafeAxisAction?.action?.Enable();
        jumpAction?.action?.Enable();
        riseAction?.action?.Enable();
        lowerAction?.action?.Enable();

        OnControlEnabled?.Invoke();
    }

    public void DisableControl()
    {
        if (!controlEnabled) return;
        controlEnabled = false;

        moveAxisAction?.action?.Disable();
        turnAxisAction?.action?.Disable();
        strafeAxisAction?.action?.Disable();
        jumpAction?.action?.Disable();
        riseAction?.action?.Disable();
        lowerAction?.action?.Disable();

        moveInput = turnInput = strafeInput = 0f;
        legacyVerticalInput = 0f;
        currentVerticalVelocity = 0f;
        risePressed = lowerPressed = false;
        hoverLockUntil = 0f;
        snapBoostUntil = 0f;

        OnControlDisabled?.Invoke();
    }
}