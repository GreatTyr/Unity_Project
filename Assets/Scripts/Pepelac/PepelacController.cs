using System;
using UnityEngine;

/// <summary>
/// PepelacController — фасад, координирующий подсистемы транспорта.
/// Реализует IControllableVehicle.
/// Делегирует логику: PepelacInputHandler, PepelacMovement, PepelacHoverSystem, PepelacGroundCheck.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(PepelacMovementConfig))] // НОВОЕ: требует конфиг
[RequireComponent(typeof(PepelacInputHandler))]
[RequireComponent(typeof(PepelacGroundCheck))]
[RequireComponent(typeof(PepelacHoverSystem))]
[RequireComponent(typeof(PepelacMovement))]
public class PepelacController : MonoBehaviour, IControllableVehicle
{
    [Header("References")]
    [SerializeField] private PepelacMain main;
    [SerializeField] private Rigidbody rb;

    // Подсистемы (GetComponent в Awake)
    private PepelacMovementConfig config;
    private PepelacInputHandler inputHandler;
    private PepelacGroundCheck groundCheck;
    private PepelacHoverSystem hoverSystem;
    private PepelacMovement movement;

    private bool controlEnabled;

    // IControllableVehicle
    public bool IsControlEnabled => controlEnabled;
    public Transform Root => transform;
    public event Action OnControlEnabled;
    public event Action OnControlDisabled;

    private void Awake()
    {
        if (rb == null) rb = GetComponent<Rigidbody>();
        if (main == null) main = GetComponent<PepelacMain>();

        config = GetComponent<PepelacMovementConfig>();

        inputHandler = GetComponent<PepelacInputHandler>();
        groundCheck = GetComponent<PepelacGroundCheck>();
        hoverSystem = GetComponent<PepelacHoverSystem>();
        movement = GetComponent<PepelacMovement>();

        // Инициализация подсистем с передачей КОНФИГА
        hoverSystem.Initialize(config, inputHandler, groundCheck, rb);
        movement.Initialize(config, inputHandler, groundCheck, hoverSystem, rb);

        // Настройка Rigidbody (читает настройки из Config, а массу из Main)
        if (config != null && rb != null)
        {
            if (config.freezeTiltAxes)
                rb.constraints |= RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
        }

        if (main != null && rb != null)
        {
            rb.mass = main.TotalMassKg > 0f ? main.TotalMassKg : 1f;
            // Центр масс теперь вычисляется каждый кадр внутри PepelacMain.FixedUpdate
        }
    }

    private void Update()
    {
        groundCheck.UpdateGrounded(transform.position);

        if (controlEnabled)
            hoverSystem.UpdateHoverInput(Time.deltaTime);
    }

    private void FixedUpdate()
    {
        bool useRB = config != null ? config.useRigidbody : true;

        // Прыжок — всегда обрабатываем если управление включено
        if (controlEnabled)
            hoverSystem.ProcessJump();

        if (useRB && rb != null && !rb.isKinematic)
        {
            if (controlEnabled)
                movement.TickPhysicsMovement(Time.fixedDeltaTime);

            hoverSystem.ApplyVerticalPhysics(Time.fixedDeltaTime);
        }
        else
        {
            if (controlEnabled)
                movement.TickKinematicMovement(Time.fixedDeltaTime);
        }
    }

    public void EnableControl()
    {
        if (controlEnabled) return;
        controlEnabled = true;

        hoverSystem.ResetState();
        hoverSystem.RefreshBaseGround();
        inputHandler.EnableInput();

        if (main != null && rb != null)
        {
            rb.mass = main.TotalMassKg > 0f ? main.TotalMassKg : 1f;
            rb.isKinematic = false;
            rb.useGravity = true;
        }

        OnControlEnabled?.Invoke();
    }

    public void DisableControl()
    {
        if (!controlEnabled) return;
        controlEnabled = false;

        inputHandler.DisableInput();
        hoverSystem.ResetState();

        OnControlDisabled?.Invoke();
    }
}