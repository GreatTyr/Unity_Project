using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Контроллер Pepelac с танковым управлением + вертикальное движение.
/// - W/S: движение вперёд/назад.
/// - A/D: поворот вокруг оси Y.
/// - Space: прыжок (импульс вверх).
/// - R/T: полёт вверх/вниз (пока зажаты).
///
/// ВАЖНО: скрипт должен висеть на Paluba (визуальный и физический корень транспорта).
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

    [Header("Physics / Kinematics")]
    [Tooltip("Если указан Rigidbody — движение через него, иначе через transform.\n" +
             "Для простоты можно оставить null и двигать через transform.")]
    public Rigidbody rb;

    [Header("Debug")]
    [SerializeField] private float moveInput;
    [SerializeField] private float turnInput;
    [SerializeField] private float verticalInput;
    [SerializeField] private bool controlEnabled = false;

    // внутреннее состояние для вертикальной скорости (гравитация + прыжок)
    private float verticalVelocity = 0f;

    void Awake()
    {
        if (rb == null) rb = GetComponent<Rigidbody>();
    }

    void OnEnable()
    {
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

        DisableControl();
    }

    void Update()
    {
        if (!controlEnabled) return;

        float dt = Time.deltaTime;

        // Простая гравитация для транспорта
        ApplyGravity(dt);

        TickMovement(dt);
    }

    void ApplyGravity(float dt)
    {
        // Если есть осмысленная вертикальная гравитация:
        // при отсутствии активного подъёма/спуска можно применять её.
        verticalVelocity += gravity * dt;
    }

    /// <summary>
    /// Основное движение: вперёд/назад + поворот + вертикальное смещение.
    /// </summary>
    void TickMovement(float dt)
    {
        // 1) Поворот вокруг Y
        float yawDelta = turnInput * turnSpeed * dt;
        Vector3 euler = transform.rotation.eulerAngles;
        euler.y += yawDelta;
        transform.rotation = Quaternion.Euler(euler);

        // 2) Горизонтальное движение вперёд/назад
        Vector3 horizontalMove = transform.forward * (moveInput * forwardSpeed * dt);

        // 3) Вертикальное движение от оси R/T (verticalInput)
        float verticalFromInput = verticalInput * verticalSpeed * dt;

        // 4) Итоговая вертикальная составляющая:
        float totalVertical = verticalFromInput + verticalVelocity * dt;

        Vector3 move = horizontalMove + Vector3.up * totalVertical;

        if (rb != null)
        {
            rb.MovePosition(rb.position + move);
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
        }
    }

    #endregion

    /// <summary>
    /// Включить управление (когда игрок садится за штурвал).
    /// </summary>
    public void EnableControl()
    {
        controlEnabled = true;
        moveInput = 0f;
        turnInput = 0f;
        verticalInput = 0f;
        verticalVelocity = 0f;

        if (moveAxisAction != null && moveAxisAction.action != null)
            moveAxisAction.action.Enable();
        if (turnAxisAction != null && turnAxisAction.action != null)
            turnAxisAction.action.Enable();
        if (verticalAxisAction != null && verticalAxisAction.action != null)
            verticalAxisAction.action.Enable();
        if (jumpAction != null && jumpAction.action != null)
            jumpAction.action.Enable();
    }

    /// <summary>
    /// Выключить управление (когда игрок выходит).
    /// </summary>
    public void DisableControl()
    {
        controlEnabled = false;
        moveInput = 0f;
        turnInput = 0f;
        verticalInput = 0f;
        verticalVelocity = 0f;

        if (moveAxisAction != null && moveAxisAction.action != null)
            moveAxisAction.action.Disable();
        if (turnAxisAction != null && turnAxisAction.action != null)
            turnAxisAction.action.Disable();
        if (verticalAxisAction != null && verticalAxisAction.action != null)
            verticalAxisAction.action.Disable();
        if (jumpAction != null && jumpAction.action != null)
            jumpAction.action.Disable();
    }
}