using System;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Контроллер Pepelac с танковым управлением + вертикальное движение.
/// Важные изменения относительно исходной версии:
/// - Добавлен ground-check (groundCheckOffset, groundCheckDistance, groundLayers).
/// - При контакте с землёй verticalVelocity сбрасывается, чтобы избежать накопления падения.
/// - Движение по-прежнему через transform (rb по умолчанию null). Если rb назначен, поведение остаётся кинематическим
///   (движение через transform). В будущем можно добавить физический режим с FixedUpdate.
/// - Добавлены события OnControlEnabled / OnControlDisabled для подписчиков (камера, UI и т.д.).
/// - Защитные проверки InputActionReference.
/// 
/// Рекомендации:
/// - Если хотите физику транспорта (реакция на столкновения), сделайте Rigidbody != null и реализуйте перемещение в FixedUpdate.
/// - Убедитесь, что groundLayers не включает layer самого транспорта (иначе палуба будет считаться «землёй»).
/// </summary>
[DisallowMultipleComponent]
public class PepelacController : MonoBehaviour
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
    [Tooltip("Скорость постоянного вертикального движения (м/с) при зажатии R/T.")]
    public float verticalSpeed = 3f;

    [Tooltip("Скорость прыжка (начальная вертикальная скорость, м/с).")]
    public float jumpImpulse = 5f;

    [Tooltip("Гравитация для транспорта (отрицательное значение).")]
    public float gravity = -9.81f;

    [Header("Ground Check")]
    [Tooltip("Смещение начала проверки земли относительно transform.position.")]
    public Vector3 groundCheckOffset = new Vector3(0f, -0.5f, 0f);

    [Tooltip("Дистанция для raycast вниз для определения grounded.")]
    public float groundCheckDistance = 0.6f;

    [Tooltip("Слои, считающиеся землёй.")]
    public LayerMask groundLayers = ~0;

    [Header("Physics / Kinematics")]
    [Tooltip("Если указан Rigidbody — движение через него (пока используется кинематический режим).\n" +
             "Для простоты сейчас движение осуществляется через transform; при желании можно реализовать физический режим в FixedUpdate.")]
    public Rigidbody rb;

    [Header("Debug")]
    [SerializeField] private float moveInput;
    [SerializeField] private float turnInput;
    [SerializeField] private float verticalInput;
    [SerializeField] private bool controlEnabled = false;

    // внутреннее состояние для вертикальной скорости (м/с)
    private float verticalVelocity = 0f;

    // состояние земли
    private bool isGrounded = false;

    // События для других систем
    public event Action OnControlEnabled;
    public event Action OnControlDisabled;

    void Awake()
    {
        if (rb == null) rb = GetComponent<Rigidbody>();
    }

    void OnEnable()
    {
        // Подписываемся на InputActionReference, если назначены
        if (moveAxisAction != null && moveAxisAction.action != null)
        {
            moveAxisAction.action.performed += OnMovePerformed;
            moveAxisAction.action.canceled += OnMoveCanceled;
            // Не вызываем Enable() здесь — управление Enable/Disable делается в EnableControl/DisableControl
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

        // при отключении компонента (не обязательно при выходе игрока) убедимся, что управление выключено
        DisableControl();
    }

    void Update()
    {
        if (!controlEnabled) return;

        float dt = Time.deltaTime;

        // Обновляем проверку земли
        UpdateGrounded();

        // Применяем гравитацию только если не на земле
        ApplyGravity(dt);

        // Поворачиваем и перемещаем
        TickMovement(dt);
    }

    /// <summary>
    /// Обновляет флаг isGrounded через Raycast (или SphereCast при необходимости).
    /// </summary>
    void UpdateGrounded()
    {
        Vector3 origin = transform.position + groundCheckOffset;
        RaycastHit hit;
        // Игнорируем триггеры
        if (Physics.Raycast(origin, Vector3.down, out hit, groundCheckDistance, groundLayers, QueryTriggerInteraction.Ignore))
        {
            // Можно расширить: проверять нормаль поверхности и углы
            isGrounded = true;
        }
        else
        {
            isGrounded = false;
        }
    }

    void ApplyGravity(float dt)
    {
        if (isGrounded)
        {
            // Сбрасываем вертикальную скорость при контакте с землёй,
            // чтобы избежать накопления отрицательного значения.
            if (verticalVelocity < 0f) verticalVelocity = 0f;
        }
        else
        {
            verticalVelocity += gravity * dt;
        }
    }

    /// <summary>
    /// Основное движение: вперёд/назад + поворот + вертикальное смещение.
    /// Движение через transform (если rb == null). Если rb назначен, пока
    /// перемещаем через transform и оставляем rb для будущих физических целей.
    /// </summary>
    void TickMovement(float dt)
    {
        // 1) Поворот вокруг Y
        float yawDelta = turnInput * turnSpeed * dt;
        Vector3 euler = transform.rotation.eulerAngles;
        euler.y += yawDelta;
        transform.rotation = Quaternion.Euler(euler);

        // 2) Горизонтальное движение вперёд/назад
        Vector3 horizontalMove = transform.forward * (moveInput * forwardSpeed);

        // 3) Вертикальное движение от оси R/T (verticalInput)
        float verticalFromInput = verticalInput * verticalSpeed;

        // 4) Итоговая вертикальная скорость (м/с)
        float totalVerticalVelocity = verticalFromInput + verticalVelocity;

        // 5) Итоговое смещение за кадр:
        Vector3 move = horizontalMove * dt + Vector3.up * (totalVerticalVelocity * dt);

        // Если на земле и вертикальная составляющая вниз — предотвращаем «провал»
        if (isGrounded && totalVerticalVelocity <= 0f)
        {
            // обнуляем вертикальную часть смещения
            move.y = 0f;
        }

        if (rb != null)
        {
            // Пока используем кинематический подход — применяем transform.
            // В будущем: если хотите физический режим, перенести сюда логику для FixedUpdate с rb.MovePosition или rb.velocity.
            transform.position += move;
        }
        else
        {
            transform.position += move;
        }
    }

    #region Input callbacks

    void OnMovePerformed(InputAction.CallbackContext ctx)
    {
        if (!controlEnabled) return;
        moveInput = ctx.ReadValue<float>();   // -1..1 (W/S)
    }

    void OnMoveCanceled(InputAction.CallbackContext ctx)
    {
        if (!controlEnabled) return;
        moveInput = 0f;
    }

    void OnTurnPerformed(InputAction.CallbackContext ctx)
    {
        if (!controlEnabled) return;
        turnInput = ctx.ReadValue<float>();   // -1..1 (A/D)
    }

    void OnTurnCanceled(InputAction.CallbackContext ctx)
    {
        if (!controlEnabled) return;
        turnInput = 0f;
    }

    void OnVerticalPerformed(InputAction.CallbackContext ctx)
    {
        if (!controlEnabled) return;
        verticalInput = ctx.ReadValue<float>(); // -1..1 (R/T)
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
            // Одноразовый прыжок: задаём вертикальную скорость
            verticalVelocity = jumpImpulse;
            // При прыжке снимаемся с земли
            isGrounded = false;
        }
    }

    #endregion

    /// <summary>
    /// Включить управление (когда игрок садится за штурвал).
    /// Включает action-ы, сбрасывает input и вызывает событие OnControlEnabled.
    /// </summary>
    public void EnableControl()
    {
        if (controlEnabled) return;

        controlEnabled = true;
        moveInput = 0f;
        turnInput = 0f;
        verticalInput = 0f;
        verticalVelocity = 0f;

        // Включаем только action'ы, если они назначены
        if (moveAxisAction != null && moveAxisAction.action != null) moveAxisAction.action.Enable();
        if (turnAxisAction != null && turnAxisAction.action != null) turnAxisAction.action.Enable();
        if (verticalAxisAction != null && verticalAxisAction.action != null) verticalAxisAction.action.Enable();
        if (jumpAction != null && jumpAction.action != null) jumpAction.action.Enable();

        OnControlEnabled?.Invoke();
    }

    /// <summary>
    /// Выключить управление (когда игрок выходит).
    /// Отключает action-ы и вызывает событие OnControlDisabled.
    /// </summary>
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
}