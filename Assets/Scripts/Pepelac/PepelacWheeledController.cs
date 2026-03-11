using System;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(PepelacInputHandler))]
[RequireComponent(typeof(PepelacWheelMovement))]
public class PepelacWheeledController : MonoBehaviour, IControllableVehicle
{
    [Header("References")]
    [SerializeField] private PepelacMain main;
    [SerializeField] private Rigidbody rb;
    [SerializeField] private PepelacInputHandler inputHandler;
    [SerializeField] private PepelacWheelMovement wheelMovement;

    [Header("Debug")]
    [SerializeField] private bool controlEnabled = false;

    public bool IsControlEnabled => controlEnabled;
    public Transform Root => transform;

    public event Action OnControlEnabled;
    public event Action OnControlDisabled;

    private void Awake()
    {
        if (rb == null)
            rb = GetComponent<Rigidbody>();

        if (main == null)
            main = GetComponent<PepelacMain>();

        if (inputHandler == null)
            inputHandler = GetComponent<PepelacInputHandler>();

        if (wheelMovement == null)
            wheelMovement = GetComponent<PepelacWheelMovement>();

        if (main != null && rb != null)
        {
            rb.mass = main.TotalMassKg > 0f ? main.TotalMassKg : 1f;
        }

        DisableControlImmediate();
    }

    private void FixedUpdate()
    {
        if (!controlEnabled)
            return;

        if (wheelMovement != null)
            wheelMovement.TickPhysics(Time.fixedDeltaTime);
    }

    public void EnableControl()
    {
        if (controlEnabled)
            return;

        controlEnabled = true;

        if (inputHandler != null)
            inputHandler.EnableInput();

        if (wheelMovement != null)
            wheelMovement.SetControlEnabled(true);

        if (main != null && rb != null)
            rb.mass = main.TotalMassKg > 0f ? main.TotalMassKg : 1f;

        OnControlEnabled?.Invoke();
        Debug.Log("[PepelacWheeledController] Управление включено.");
    }

    public void DisableControl()
    {
        if (!controlEnabled)
            return;

        controlEnabled = false;

        if (inputHandler != null)
            inputHandler.DisableInput();

        if (wheelMovement != null)
            wheelMovement.SetControlEnabled(false);

        StopVehicle();

        OnControlDisabled?.Invoke();
        Debug.Log("[PepelacWheeledController] Управление выключено.");
    }

    private void DisableControlImmediate()
    {
        controlEnabled = false;

        if (inputHandler != null)
            inputHandler.DisableInput();

        if (wheelMovement != null)
            wheelMovement.SetControlEnabled(false);

        StopVehicle();
    }

    private void StopVehicle()
    {
        if (rb == null) return;

        Vector3 velocity = rb.linearVelocity;
        velocity.x = 0f;
        velocity.z = 0f;
        rb.linearVelocity = velocity;

        Vector3 angular = rb.angularVelocity;
        angular.y = 0f;
        rb.angularVelocity = angular;
    }
}