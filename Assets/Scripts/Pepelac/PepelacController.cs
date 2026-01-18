// PepelacController.cs
// Обновлённая версия с поддержкой:
// - стрейфа (Q/E) (если заданы соответствующие action'ы)
// - hover высоты относительно поверхности (baseGroundY + offset)
// - rise (R) / lower (T) как Hold actions (отдельные action'ы)
// - прыжок временно прерывает фиксацию hover (jumpBreaksHoverDuration)
// - плавное снижение/подъём и опция snapOnRelease для мгновенной фиксации при отпускании
// - обработка случаев, когда rise/lower сделаны как отдельные Button actions (рекомендуется)
// Версия ориентирована на Unity 6000.2, C#.
//
// Интеграция:
// - В Input System создайте два Action (Type = Button): "Rise" (binding R, interaction = Hold) и "Lower" (binding T, interaction = Hold).
// - Привяжите их в инспекторе PepelacController.riseAction и PepelacController.lowerAction соответственно.
// - Остальные действия: moveAxisAction (W/S), turnAxisAction (A/D), strafeAxisAction (Q/E), jumpAction (Space).
//
// Поведение:
// - При удержании Rise (R) targetHoverOffset увеличивается; при отпускании — значение фиксируется, и аппарат остаётся на высоте.
// - При удержании Lower (T) targetHoverOffset уменьшается; при отпускании — значение фиксируется.
// - Прыжок (jumpAction) задаёт vertical impulse и временно (jumpBreaksHoverDuration) снимает фиксацию hover, чтобы прыжок был видим.
// - Если snapOnRelease == true, то при отпускании rise/lower позиция Y устанавливается мгновенно в целевую; иначе происходит плавное сглаживание (hoverSmoothTime).
//
// Замечания:
// - Если раньше у вас был verticalAxis (R/T в виде одного axis/composite) — удалите его, иначе он будет конфликтовать.
// - Код рассчитан на кинематическое перемещение через transform.position; если вы используете Rigidbody-физику, рекомендуется адаптировать (возможен переключатель rb.isKinematic).
//

using System;
using UnityEngine;
using UnityEngine.InputSystem;

[DisallowMultipleComponent]
public class PepelacController : MonoBehaviour, IControllableVehicle
{
    [Header("Input (assign in Inspector)")]
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

    [Header("Movement")]
    public float forwardSpeed = 5f;
    public float strafeSpeed = 5f;
    public float turnSpeed = 90f;

    [Header("Vertical / Jump")]
    public float verticalSpeed = 3f;    // legacy vertical axis speed (if used)
    public float jumpImpulse = 5f;
    public float gravity = -9.81f;

    [Header("Hover Height Settings")]
    [Tooltip("Скорость набора высоты при удержании rise (м/с).")]
    public float riseSpeed = 2f;

    [Tooltip("Скорость уменьшения высоты при удержании lower (м/с).")]
    public float lowerSpeed = 3f;

    [Tooltip("Максимальная относительная высота над поверхностью (м).")]
    public float maxHoverOffset = 50f;

    [Tooltip("Плавность интерполяции фактической Y позиции к целевой (в секундах). 0 = мгновенно.")]
    public float hoverSmoothTime = 0.12f;

    [Tooltip("Длительность временного отключения фиксации hover после прыжка (сек).")]
    public float jumpBreaksHoverDuration = 0.6f;

    [Tooltip("Если true — когда hoverOffset > 0, гравитация при удержании будет отменяться (Pepelac 'висит').")]
    public bool holdHoverPreventsGravity = true;

    [Tooltip("Если true — при отпускании rise/lower позиция Y будет установлена мгновенно в целевой (без сглаживания).")]
    public bool snapOnRelease = false;

    [Tooltip("Использовать базовую высоту поверхности под Pepelac как origin (рекомендуется=true).")]
    public bool useBaseGroundY = true;

    [Header("Ground Check")]
    public Vector3 groundCheckOffset = new Vector3(0f, -0.5f, 0f);
    public float groundCheckDistance = 0.6f;
    public LayerMask groundLayers = ~0;

    [Header("Physics / Kinematics")]
    [Tooltip("Опциональный Rigidbody (если нужно физическое поведение). Текущее движение реализуется кинематически через transform.")]
    public Rigidbody rb;

    // Internal state
    private float moveInput = 0f;
    private float turnInput = 0f;
    private float strafeInput = 0f;
    private float legacyVerticalInput = 0f; // fallback if using old axis
    private bool controlEnabled = false;

    // Hover internals
    private float baseGroundY = 0f; // world Y поверхности под Pepelac (определяется raycast)
    private float targetHoverOffset = 0f; // relative meters above baseGroundY (>=0)
    private float currentVerticalVelocity = 0f; // для gravity/jump, м/с
    private bool isGrounded = false;
    private float hoverVelocityRef = 0f; // для SmoothDamp
    private float hoverLockUntil = 0f; // Time.time пока фиксация hover отключена (например после прыжка)

    // Input flags for rise/lower
    private bool risePressed = false;
    private bool lowerPressed = false;

    // IControllableVehicle
    public bool IsControlEnabled => controlEnabled;
    public Transform Root => this.transform;
    public event Action OnControlEnabled;
    public event Action OnControlDisabled;

    void Awake()
    {
        if (rb == null) rb = GetComponent<Rigidbody>();

        // Инициализируем baseGroundY текущей поверхностью под объектом (или просто текущим Y)
        baseGroundY = QuerySurfaceYUnder(transform.position, fallbackToCurrentY: true);
        targetHoverOffset = 0f; // по умолчанию на земле
    }

    void OnEnable()
    {
        // Подписка на callbacks (action'ы включаются в EnableControl)
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

    void OnDisable()
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

        DisableControl();
    }

    void Update()
    {
        if (!controlEnabled) return;

        float dt = Time.deltaTime;

        UpdateGrounded();
        HandleHoverInputAndPhysics(dt);
        TickMovement(dt);
    }

    // -----------------------
    // Ground / surface queries
    // -----------------------
    void UpdateGrounded()
    {
        Vector3 origin = transform.position + groundCheckOffset;
        RaycastHit hit;
        isGrounded = Physics.Raycast(origin, Vector3.down, out hit, groundCheckDistance, groundLayers, QueryTriggerInteraction.Ignore);
    }

    float QuerySurfaceYUnder(Vector3 worldPos, bool fallbackToCurrentY = false)
    {
        RaycastHit hit;
        Vector3 origin = worldPos + Vector3.up * 0.5f; // немного выше для стабильности
        if (Physics.Raycast(origin, Vector3.down, out hit, 200f, groundLayers, QueryTriggerInteraction.Ignore))
        {
            return hit.point.y;
        }
        return fallbackToCurrentY ? worldPos.y : 0f;
    }

    // -----------------------
    // Hover & gravity logic
    // -----------------------
    void HandleHoverInputAndPhysics(float dt)
    {
        // Обновляем baseGroundY периодически (полезно если земля движется)
        if (useBaseGroundY)
        {
            baseGroundY = QuerySurfaceYUnder(transform.position, fallbackToCurrentY: true);
        }

        // Управление targetHoverOffset:
        // Если назначены riseAction/lowerAction — используем их; иначе можно добавить fallback к legacyVerticalInput
        bool isRising = risePressed || (riseAction?.action == null && legacyVerticalInput > 0.1f);
        bool isLowering = lowerPressed || (lowerAction?.action == null && legacyVerticalInput < -0.1f);

        // Если сейчас hover временно разблокирован (после прыжка), и время не истекло — игнорируем фиксацию.
        bool hoverTemporarilyBlocked = Time.time < hoverLockUntil;

        // Изменяем целевой offset пока держат кнопку
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

        bool wantHover = targetHoverOffset > 0f;

        if (holdHoverPreventsGravity && wantHover && !hoverTemporarilyBlocked)
        {
            // Фиксируем высоту: цель world Y = baseGroundY + targetHoverOffset
            float targetY = baseGroundY + targetHoverOffset;

            if (snapOnRelease)
            {
                // Если включен snapOnRelease — ставим позицию мгновенно
                Vector3 pos = transform.position;
                pos.y = targetY;
                transform.position = pos;
                currentVerticalVelocity = 0f;
                hoverVelocityRef = 0f;
            }
            else
            {
                // Плавное приближение к целевой высоте (SmoothDamp)
                if (hoverSmoothTime <= 0f)
                {
                    Vector3 pos = transform.position;
                    pos.y = targetY;
                    transform.position = pos;
                    currentVerticalVelocity = 0f;
                    hoverVelocityRef = 0f;
                }
                else
                {
                    float newY = Mathf.SmoothDamp(transform.position.y, targetY, ref hoverVelocityRef, hoverSmoothTime, Mathf.Infinity, dt);

                    // Если кнопки сейчас не нажаты (мы только что отпустили), и разница почти 0 — snap и обнулить velocityRef
                    if (!risePressed && !lowerPressed)
                    {
                        if (Mathf.Abs(newY - targetY) < 0.001f)
                        {
                            newY = targetY;
                            hoverVelocityRef = 0f;
                        }
                    }

                    Vector3 pos = transform.position;
                    pos.y = newY;
                    transform.position = pos;

                    if (!risePressed && !lowerPressed)
                        currentVerticalVelocity = 0f;
                }
            }
        }
        else
        {
            // Hover неактивен -> применяем гравитацию и вертикальное управление
            if (isGrounded)
            {
                if (currentVerticalVelocity < 0f) currentVerticalVelocity = 0f;
            }
            else
            {
                currentVerticalVelocity += gravity * dt;
            }

            // legacy vertical input (редко используется сейчас) - добавляем вертикальное ручное управление
            currentVerticalVelocity += legacyVerticalInput * verticalSpeed * dt;
        }
    }

    // -----------------------
    // Horizontal movement / application
    // -----------------------
    void TickMovement(float dt)
    {
        // Поворот
        float yawDelta = turnInput * turnSpeed * dt;
        transform.Rotate(0f, yawDelta, 0f);

        // Горизонтальные скорости
        Vector3 forwardVel = transform.forward * (moveInput * forwardSpeed);
        Vector3 rightVel = transform.right * (strafeInput * strafeSpeed);

        // Вертикальная составляющая: если hover активен и держится - вертикаль уже применена в HandleHover (мы не складываем)
        float verticalVel = 0f;
        bool hoverTemporarilyBlocked = Time.time < hoverLockUntil;
        bool wantHover = targetHoverOffset > 0f;
        if (!(holdHoverPreventsGravity && wantHover && !hoverTemporarilyBlocked))
        {
            verticalVel = currentVerticalVelocity;
        }
        else
        {
            verticalVel = 0f;
        }

        Vector3 totalVel = forwardVel + rightVel + Vector3.up * verticalVel;
        Vector3 delta = totalVel * dt;

        // Если стоим на земле и вниз движение не требуется - не двигаем вниз
        if (isGrounded && verticalVel <= 0f) delta.y = 0f;

        transform.position += delta;
    }

    // -----------------------
    // Input callbacks
    // -----------------------
    void OnMovePerformed(InputAction.CallbackContext ctx) { if (!controlEnabled) return; moveInput = ctx.ReadValue<float>(); }
    void OnMoveCanceled(InputAction.CallbackContext ctx) { if (!controlEnabled) return; moveInput = 0f; }
    void OnTurnPerformed(InputAction.CallbackContext ctx) { if (!controlEnabled) return; turnInput = ctx.ReadValue<float>(); }
    void OnTurnCanceled(InputAction.CallbackContext ctx) { if (!controlEnabled) return; turnInput = 0f; }
    void OnStrafePerformed(InputAction.CallbackContext ctx)
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
    void OnStrafeCanceled(InputAction.CallbackContext ctx) { if (!controlEnabled) return; strafeInput = 0f; }

    // Jump: при прыжке временно разблокируем фиксацию hover (чтобы vertical velocity применился)
    void OnJumpPerformed(InputAction.CallbackContext ctx)
    {
        if (!controlEnabled) return;
        if (ctx.performed)
        {
            currentVerticalVelocity = jumpImpulse;
            // временно отключаем фиксацию hover (позволим подпрыгнуть)
            hoverLockUntil = Time.time + jumpBreaksHoverDuration;
        }
    }

    // Rise / Lower callbacks (explicit)
    void OnRisePerformed(InputAction.CallbackContext ctx)
    {
        if (!controlEnabled) return;
        // Если Action реализован как Button -> performed при нажатии, canceled при отпускании.
        // Поддержим также случай, когда action настроен как Value (float):
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

    void OnRiseCanceled(InputAction.CallbackContext ctx)
    {
        if (!controlEnabled) return;
        risePressed = false;

        // При отпускании фиксируем текущую высоту как целевую offset, чтобы не было продолжения движения.
        float currentY = transform.position.y;
        targetHoverOffset = Mathf.Clamp(currentY - baseGroundY, 0f, maxHoverOffset);

        // Сбрасываем вертикальные инерции, чтобы не продолжать движение после отпускания
        currentVerticalVelocity = 0f;
        hoverVelocityRef = 0f;

        // Если включен snapOnRelease — моментально выставляем позицию (дополнительно)
        if (snapOnRelease)
        {
            Vector3 pos = transform.position;
            pos.y = baseGroundY + targetHoverOffset;
            transform.position = pos;
        }
    }

    void OnLowerPerformed(InputAction.CallbackContext ctx)
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

    void OnLowerCanceled(InputAction.CallbackContext ctx)
    {
        if (!controlEnabled) return;
        lowerPressed = false;

        float currentY = transform.position.y;
        targetHoverOffset = Mathf.Clamp(currentY - baseGroundY, 0f, maxHoverOffset);

        currentVerticalVelocity = 0f;
        hoverVelocityRef = 0f;

        if (snapOnRelease)
        {
            Vector3 pos = transform.position;
            pos.y = baseGroundY + targetHoverOffset;
            transform.position = pos;
        }
    }

    // Fallback: если вы используете legacy vertical axis, внешне можно назначить обработчики подобно:
    public void OnLegacyVerticalPerformed(float value) { legacyVerticalInput = value; }
    public void OnLegacyVerticalCanceled() { legacyVerticalInput = 0f; }

    // -----------------------
    // IControllableVehicle
    // -----------------------
    public void EnableControl()
    {
        if (controlEnabled) return;
        controlEnabled = true;

        moveInput = turnInput = strafeInput = 0f;
        legacyVerticalInput = 0f;
        currentVerticalVelocity = 0f;
        hoverLockUntil = 0f;

        // обновим базовую поверхность при входе
        baseGroundY = QuerySurfaceYUnder(transform.position, fallbackToCurrentY: true);

        if (targetHoverOffset < 0f) targetHoverOffset = 0f;

        // Включаем action'ы
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

        OnControlDisabled?.Invoke();
    }
}