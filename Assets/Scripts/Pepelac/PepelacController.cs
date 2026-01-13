using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Контроллер Pepelac с танковым управлением:
/// - W/S (Move) — движение вперёд/назад по локальному forward.
/// - A/D (Turn) — поворот вокруг вертикальной оси (Y).
/// </summary>
[DisallowMultipleComponent]
public class PepelacController : MonoBehaviour
{
    [Header("Input (assign in Inspector)")]
    [Tooltip("Ось движения вперёд/назад (W/S)")]
    public InputActionReference moveAxisAction;  // float -1..1

    [Tooltip("Ось поворота влево/вправо (A/D)")]
    public InputActionReference turnAxisAction;  // float -1..1

    [Header("Movement")]
    [Tooltip("Скорость движения вперёд/назад (м/с)")]
    public float forwardSpeed = 5f;

    [Tooltip("Скорость поворота (градусов/сек)")]
    public float turnSpeed = 90f;

    [Header("Physics / Kinematics")]
    [Tooltip("Если указан Rigidbody — движение через него, иначе через transform.")]
    public Rigidbody rb;

    // внутренние оси
    [SerializeField] private float moveInput;
    [SerializeField] private float turnInput;

    [SerializeField] private bool controlEnabled = false;

    void Awake()
    {
        if (rb == null) rb = GetComponent<Rigidbody>();
    }

    void OnEnable()
    {
        // Подписка на события инпута
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

        DisableControl();
    }

    void Update()
    {
        if (!controlEnabled) return;

        float dt = Time.deltaTime;

        // Можно раскомментировать для отладки:
        // if (Mathf.Abs(moveInput) > 0.01f || Mathf.Abs(turnInput) > 0.01f)
        //     Debug.Log($"[PepelacController] move={moveInput:F2}, turn={turnInput:F2}");

        TickMovement(dt);
    }

    /// <summary>
    /// Основное движение: вперёд/назад + вращение.
    /// </summary>
    void TickMovement(float dt)
    {
        // 1) Движение вперёд/назад
        Vector3 forwardMove = transform.forward * (moveInput * forwardSpeed * dt);

        // 2) Поворот вокруг вертикальной оси
        float yawDelta = turnInput * turnSpeed * dt;
        Quaternion deltaRot = Quaternion.Euler(0f, yawDelta, 0f);

        if (rb != null)
        {
            // Через Rigidbody
            rb.MovePosition(rb.position + forwardMove);
            rb.MoveRotation(rb.rotation * deltaRot);
        }
        else
        {
            // Через transform
            transform.position += forwardMove;
            transform.rotation = transform.rotation * deltaRot;
        }
    }

    #region Input callbacks

    void OnMovePerformed(InputAction.CallbackContext ctx)
    {
        if (!controlEnabled) return;
        moveInput = ctx.ReadValue<float>();   // -1..1
    }

    void OnMoveCanceled(InputAction.CallbackContext ctx)
    {
        if (!controlEnabled) return;
        moveInput = 0f;
    }

    void OnTurnPerformed(InputAction.CallbackContext ctx)
    {
        if (!controlEnabled) return;
        turnInput = ctx.ReadValue<float>();   // -1..1
    }

    void OnTurnCanceled(InputAction.CallbackContext ctx)
    {
        if (!controlEnabled) return;
        turnInput = 0f;
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

        if (moveAxisAction != null && moveAxisAction.action != null)
            moveAxisAction.action.Enable();
        if (turnAxisAction != null && turnAxisAction.action != null)
            turnAxisAction.action.Enable();
    }

    /// <summary>
    /// Выключить управление (когда игрок выходит).
    /// </summary>
    public void DisableControl()
    {
        controlEnabled = false;
        moveInput = 0f;
        turnInput = 0f;

        if (moveAxisAction != null && moveAxisAction.action != null)
            moveAxisAction.action.Disable();
        if (turnAxisAction != null && turnAxisAction.action != null)
            turnAxisAction.action.Disable();
    }
}