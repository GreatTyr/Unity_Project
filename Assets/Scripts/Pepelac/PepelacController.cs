using System;
using UnityEngine;

/// <summary>
/// PepelacController — фасад, координирующий подсистемы транспорта.
/// Реализует IControllableVehicle.
/// Делегирует логику: PepelacInputHandler, PepelacMovement, PepelacHoverSystem, PepelacGroundCheck.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody))]
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

        inputHandler = GetComponent<PepelacInputHandler>();
        groundCheck = GetComponent<PepelacGroundCheck>();
        hoverSystem = GetComponent<PepelacHoverSystem>();
        movement = GetComponent<PepelacMovement>();

        // Инициализация подсистем
        hoverSystem.Initialize(main, inputHandler, groundCheck, rb);
        movement.Initialize(main, inputHandler, groundCheck, hoverSystem, rb);

        // Настройка Rigidbody
        if (main != null && rb != null)
        {
            if (main.centerOfMassOffset != Vector3.zero)
                rb.centerOfMass += main.centerOfMassOffset;

            if (main.freezeTiltAxes)
                rb.constraints |= RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;

            main.RecalculateTotalMass();
            rb.mass = main.currentTotalMass > 0f ? main.currentTotalMass : main.baseMass;
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
        bool useRB = main != null ? main.useRigidbody : true;

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
            main.RecalculateTotalMass();
            rb.mass = main.currentTotalMass > 0f ? main.currentTotalMass : main.baseMass;
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