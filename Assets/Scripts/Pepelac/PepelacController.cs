using System;
using UnityEngine;
using UnityEngine.InputSystem;

[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody))]
public class PepelacController : MonoBehaviour, IControllableVehicle
{
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

    [Header("Горизонтальное движение (плоскость XZ)")]
    [Tooltip("Максимальная скорость вперёд/назад (м/с).")]
    public float forwardSpeed = 8f;

    [Tooltip("Максимальная скорость стрейфа (м/с).")]
    public float strafeSpeed = 6f;

    [Tooltip("Максимальное горизонтальное ускорение (м/с²). Ограничивает силу разгона.")]
    public float maxHorizontalAcceleration = 20f;

    [Header("Поворот (танковый стиль)")]
    [Tooltip("Скорость поворота (град/с) при единичном turnInput.")]
    public float turnSpeed = 90f;

    [Tooltip("Скорость сглаживания поворота (0 = мгновенно). Применяется к MoveRotation.")]
    public float rotationSlerpSpeed = 10f;

    [Tooltip("Блокировать вращение по X/Z, чтобы Pepelac не заваливался на бок.")]
    public bool freezeTiltAxes = true;

    [Header("Вертикаль / Прыжок")]
    [Tooltip("Сила прыжка (в пересчёте на изменение скорости вверх, м/с). Применяется как VelocityChange.")]
    public float jumpImpulse = 5f;

    [Tooltip("Гравитация (обычно отрицательное значение). Используется только в fallback-режиме без Rigidbody.")]
    public float gravity = -9.81f;

    [Header("Hover (удержание высоты над поверхностью)")]
    [Tooltip("Скорость набора высоты при удержании Rise (м/с).")]
    public float riseSpeed = 2f;

    [Tooltip("Скорость уменьшения высоты при удержании Lower (м/с).")]
    public float lowerSpeed = 3f;

    [Tooltip("Максимальная относительная высота над поверхностью (м).")]
    public float maxHoverOffset = 50f;

    [Tooltip("Коэффициент пропорционального звена (P) вертикального PD-контроллера.")]
    public float verticalSpringKp = 300f;

    [Tooltip("Коэффициент дифференциального звена (D) вертикального PD-контроллера.")]
    public float verticalSpringKd = 40f;

    [Tooltip("Максимальная вертикальная сила (Ньютон), которую может приложить hover-контроллер.")]
    public float maxVerticalForce = 5000f;

    [Tooltip("Длительность временного отключения фиксации hover после прыжка (сек).")]
    public float jumpBreaksHoverDuration = 0.6f;

    [Tooltip("Если true — когда hover активен, вертикальный PD-контроллер отменяет провал от гравитации.")]
    public bool holdHoverPreventsGravity = true;

    [Tooltip("Если true — при отпускании rise/lower будет использоваться усиленный вертикальный PD для быстрого 'прищёлкивания' к целевой высоте.")]
    public bool snapOnRelease = false;

    [Tooltip("Множитель усиления PD на короткое время после отпускания rise/lower (для snap эффекта).")]
    public float snapForceMultiplier = 3f;

    [Tooltip("Длительность усиленного PD после отпускания rise/lower (сек).")]
    public float snapDuration = 0.15f;

    [Tooltip("Использовать базовую высоту поверхности под Pepelac как origin (рекомендуется=true).")]
    public bool useBaseGroundY = true;

    [Header("Проверка земли")]
    [Tooltip("Смещение origin для raycast вниз от текущей позиции (локальный offset).")]
    public Vector3 groundCheckOffset = new Vector3(0f, -0.5f, 0f);

    [Tooltip("Дистанция raycast вниз для проверки земли.")]
    public float groundCheckDistance = 0.6f;

    [Tooltip("Слои, считающиеся землёй.")]
    public LayerMask groundLayers = ~0;

    [Header("Физика / Режим")]
    [Tooltip("Если true — используем Rigidbody для движения. Если false или rb=null — fallback на transform-движение.")]
    public bool useRigidbody = true;

    [Tooltip("Опциональный Rigidbody (если нужно физическое поведение). Если не назначен, будет взят с этого объекта.")]
    public Rigidbody rb;

    [Tooltip("Смещение центра масс относительно локального центра объекта.")]
    public Vector3 centerOfMassOffset = Vector3.zero;

    // -----------------------------
    // Внутренние поля ввода
    // -----------------------------
    private float moveInput = 0f;
    private float turnInput = 0f;
    private float strafeInput = 0f;

    // Старый "вертикальный" ввод оставим только для fallback-режима
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

    // =========================
    // Жизненный цикл
    // =========================
    private void Awake()
    {
        if (rb == null)
            rb = GetComponent<Rigidbody>();

        if (rb == null)
        {
            Debug.LogWarning("[PepelacController] Rigidbody не найден, будет использован fallback-режим через transform.");
            useRigidbody = false;
        }

        if (rb != null)
        {
            // Настраиваем центр масс
            if (centerOfMassOffset != Vector3.zero)
                rb.centerOfMass += centerOfMassOffset;

            // Блокируем вращение по X/Z, чтобы не заваливался, если опция включена
            if (freezeTiltAxes)
            {
                rb.constraints |= RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
            }
        }

        // Инициализируем базовую высоту
        baseGroundY = QuerySurfaceYUnder(transform.position, fallbackToCurrentY: true);
        targetHoverOffset = 0f;
    }

    private void OnEnable()
    {
        // Подписка на InputSystem-коллбеки (action'ы включаются/выключаются в EnableControl/DisableControl)
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

        // В Update обновляем только информацию о земле и целевые параметры hover.
        // Прямое изменение позиций/скоростей для физики — в FixedUpdate.
        UpdateGrounded();
        HandleHoverInput(Time.deltaTime);
    }

    private void FixedUpdate()
    {
        if (!controlEnabled) return;

        if (useRigidbody && rb != null && rb.isKinematic == false)
        {
            TickPhysicsMovement(Time.fixedDeltaTime);
        }
        else
        {
            // Fallback на старый transform-режим, если нет Rigidbody
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
    /// <summary>
    /// Обработка ввода rise/lower и обновление целевого targetHoverOffset.
    /// Физическое удержание высоты делается в FixedUpdate.
    /// </summary>
    private void HandleHoverInput(float dt)
    {
        // Обновляем базовую поверхность под Pepelac
        if (useBaseGroundY)
        {
            baseGroundY = QuerySurfaceYUnder(transform.position, fallbackToCurrentY: true);
        }

        // Вариант с legacyVerticalInput оставляем как fallback, но он редко нужен
        bool isRising = risePressed || (riseAction?.action == null && legacyVerticalInput > 0.1f);
        bool isLowering = lowerPressed || (lowerAction?.action == null && legacyVerticalInput < -0.1f);

        // hover заблокирован (например, после прыжка)?
        bool hoverTemporarilyBlocked = Time.time < hoverLockUntil;

        // Изменяем целевой offset только от ввода
        if (isRising)
        {
            targetHoverOffset += riseSpeed * dt;
            if (targetHoverOffset > maxHoverOffset) targetHoverOffset = maxHoverOffset;
        }
        else if (isLowering)
        {
            targetHoverOffset -= lowerSpeed * dt;
            if (targetHoverOffset < 0f) targetHoverOffset = 0f;
        }

        // Если hover заблокирован (после прыжка), мы всё равно храним targetHoverOffset,
        // но вертикальное удержание не будет применяться в FixedUpdate, пока не истечёт hoverLockUntil.
    }

    // =========================
    // Движение через Rigidbody
    // =========================
    private void TickPhysicsMovement(float dt)
    {
        if (rb == null) return;

        // --------- Поворот (танковый) ---------
        // Вычисляем желаемый поворот на этот шаг
        float yawDelta = turnInput * turnSpeed * dt;
        Quaternion currentRot = rb.rotation;
        Quaternion deltaRot = Quaternion.Euler(0f, yawDelta, 0f);
        Quaternion targetRot = deltaRot * currentRot;

        if (rotationSlerpSpeed <= 0f)
        {
            rb.MoveRotation(targetRot);
        }
        else
        {
            Quaternion slerped = Quaternion.Slerp(currentRot, targetRot, rotationSlerpSpeed * dt);
            rb.MoveRotation(slerped);
        }

        // --------- Горизонтальная скорость ---------
        // Желаемая локальная скорость (по осям Forward/Right)
        float desiredForward = moveInput * forwardSpeed;
        float desiredStrafe = strafeInput * strafeSpeed;

        // Текущая мировая скорость
        Vector3 velocity = rb.linearVelocity;

        // Проекция в локальное пространство по горизонтали
        Vector3 localVel = transform.InverseTransformDirection(velocity);
        float currentForward = localVel.z;
        float currentStrafe = localVel.x;

        // Считаем требуемое изменение скорости (deltaV) и ограничиваем ускорение
        float deltaForward = desiredForward - currentForward;
        float deltaStrafe = desiredStrafe - currentStrafe;

        // Максимальное изменение скорости за шаг по модулю: a_max * dt
        float maxDeltaSpeed = maxHorizontalAcceleration * dt;

        deltaForward = Mathf.Clamp(deltaForward, -maxDeltaSpeed, maxDeltaSpeed);
        deltaStrafe = Mathf.Clamp(deltaStrafe, -maxDeltaSpeed, maxDeltaSpeed);

        // Преобразуем deltaV в силу: F = m * deltaV / dt
        Vector3 deltaVelLocal = new Vector3(deltaStrafe, 0f, deltaForward);
        Vector3 requiredAccelLocal = deltaVelLocal / Mathf.Max(dt, 0.0001f);
        Vector3 requiredForceLocal = requiredAccelLocal * rb.mass;

        // Применяем силу в локальных координатах
        rb.AddRelativeForce(requiredForceLocal, ForceMode.Force);

        // --------- Вертикаль / Hover ---------
        ApplyVerticalPhysics(dt);
    }

    /// <summary>
    /// Вертикальная физика: прыжок + hover (PD-контроллер по высоте).
    /// </summary>
    private void ApplyVerticalPhysics(float dt)
    {
        if (rb == null) return;

        bool hoverTemporarilyBlocked = Time.time < hoverLockUntil;
        bool wantHover = targetHoverOffset > 0f;

        if (holdHoverPreventsGravity && wantHover && !hoverTemporarilyBlocked)
        {
            float targetY = baseGroundY + targetHoverOffset;
            float currentY = rb.position.y;
            float error = targetY - currentY;

            // Текущая вертикальная скорость
            float velY = rb.linearVelocity.y;

            // Базовые коэффициенты PD
            float kp = verticalSpringKp;
            float kd = verticalSpringKd;

            // Если включён snapOnRelease и мы находимся в окне snapDuration после отпускания — усиливаем PD
            if (snapOnRelease && Time.time < snapBoostUntil)
            {
                kp *= snapForceMultiplier;
                kd *= snapForceMultiplier;
            }

            // PD-сила: F = kp * error - kd * velY
            float forceY = kp * error - kd * velY;

            // Ограничиваем силу
            forceY = Mathf.Clamp(forceY, -maxVerticalForce, maxVerticalForce);

            // Применяем вертикальную силу
            rb.AddForce(Vector3.up * forceY, ForceMode.Force);
        }
        else
        {
            // Hover неактивен или заблокирован — rely on нормальную гравитацию Rigidbody.
            // Т.к. гравитацией управляет сама физика (rb.useGravity), дополнительных действий не нужно.
            // Прыжок уже задаётся в OnJumpPerformed через AddForce(…, VelocityChange).
        }
    }

    // =========================
    // Fallback (transform) для случаев без Rigidbody
    // =========================
    private void TickKinematicMovement(float dt)
    {
        // Поворот
        float yawDelta = turnInput * turnSpeed * dt;
        transform.Rotate(0f, yawDelta, 0f);

        // Горизонтальное движение
        Vector3 forwardVel = transform.forward * (moveInput * forwardSpeed);
        Vector3 rightVel = transform.right * (strafeInput * strafeSpeed);

        // Hover / гравитация (старая логика, упрощённая)
        bool hoverTemporarilyBlocked = Time.time < hoverLockUntil;
        bool wantHover = targetHoverOffset > 0f;
        float verticalVel = 0f;

        if (holdHoverPreventsGravity && wantHover && !hoverTemporarilyBlocked)
        {
            // Простое позиционирование по высоте
            float targetY = baseGroundY + targetHoverOffset;

            // Без сглаживания, чтобы не раздувать код — можно добавить SmoothDamp при желании
            Vector3 pos = transform.position;
            pos.y = targetY;
            transform.position = pos;
            currentVerticalVelocity = 0f;
        }
        else
        {
            // Применяем гравитацию
            if (isGrounded)
            {
                if (currentVerticalVelocity < 0f) currentVerticalVelocity = 0f;
            }
            else
            {
                currentVerticalVelocity += gravity * dt;
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

        // Поддерживаем вариант, когда ось стрейфа задаётся как Vector2 (например, левый стик)
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
    /// Прыжок: задаём вертикальный импульс и временно разблокируем hover.
    /// </summary>
    private void OnJumpPerformed(InputAction.CallbackContext ctx)
    {
        if (!controlEnabled) return;
        if (!ctx.performed) return;

        if (useRigidbody && rb != null)
        {
            // Добавляем мгновенное изменение вертикальной скорости
            rb.AddForce(Vector3.up * jumpImpulse, ForceMode.VelocityChange);
        }
        else
        {
            // Fallback — просто задаём вертикальную скорость
            currentVerticalVelocity = jumpImpulse;
        }

        // Временно отключаем фиксацию hover
        hoverLockUntil = Time.time + jumpBreaksHoverDuration;
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

        // Фиксируем текущую высоту как целевой offset
        float currentY = transform.position.y;
        targetHoverOffset = Mathf.Clamp(currentY - baseGroundY, 0f, maxHoverOffset);

        currentVerticalVelocity = 0f;
        hoverVelocityRef = 0f;

        // Включаем временный boost для PD-контроллера, чтобы быстро "прищёлкнуть" к целевой высоте
        if (snapOnRelease)
        {
            snapBoostUntil = Time.time + snapDuration;
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
        targetHoverOffset = Mathf.Clamp(currentY - baseGroundY, 0f, maxHoverOffset);

        currentVerticalVelocity = 0f;
        hoverVelocityRef = 0f;

        if (snapOnRelease)
        {
            snapBoostUntil = Time.time + snapDuration;
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

        // Включаем Input Actions
        moveAxisAction?.action?.Enable();
        turnAxisAction?.action?.Enable();
        strafeAxisAction?.action?.Enable();
        jumpAction?.action?.Enable();
        riseAction?.action?.Enable();
        lowerAction?.action?.Enable();

        // Убеждаемся, что Rigidbody активен
        if (useRigidbody && rb != null)
        {
            rb.isKinematic = false;
            rb.useGravity = true;
        }

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

        // По желанию можно делать rb.isKinematic = true здесь,
        // но пока оставим транспорт физически "живым", чтобы он продолжал двигаться по инерции.
        // Если нужно "заморозить" транспорт при выходе, можно добавить отдельный флаг и обработать его.

        OnControlDisabled?.Invoke();
    }
}