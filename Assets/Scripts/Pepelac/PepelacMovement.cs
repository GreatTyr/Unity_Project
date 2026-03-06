using UnityEngine;

/// <summary>
/// Горизонтальное движение транспорта: поворот + движение через Rigidbody или transform.
/// Читает данные из PepelacMovementConfig и PepelacInputHandler.
/// </summary>
[DisallowMultipleComponent]
public class PepelacMovement : MonoBehaviour
{
    private PepelacMovementConfig config;
    private PepelacInputHandler input;
    private PepelacGroundCheck groundCheck;
    private PepelacHoverSystem hoverSystem;
    private Rigidbody rb;

    public void Initialize(PepelacMovementConfig movementConfig, PepelacInputHandler inputHandler,
                           PepelacGroundCheck ground, PepelacHoverSystem hover, Rigidbody rigidbody)
    {
        config = movementConfig;
        input = inputHandler;
        groundCheck = ground;
        hoverSystem = hover;
        rb = rigidbody;
    }

    /// <summary>
    /// Горизонтальное движение через Rigidbody. Вызывать из FixedUpdate.
    /// </summary>
    public void TickPhysicsMovement(float dt)
    {
        if (rb == null || config == null || input == null) return;

        ApplyRotation(dt);
        ApplyHorizontalForce(dt);
    }

    /// <summary>
    /// Полное движение через transform (fallback без Rigidbody). Вызывать из FixedUpdate.
    /// </summary>
    public void TickKinematicMovement(float dt)
    {
        if (config == null || input == null) return;

        // Поворот
        float yawDelta = input.TurnInput * config.turnSpeed * dt;
        transform.Rotate(0f, yawDelta, 0f);

        // Горизонтальное движение
        Vector3 forwardVel = transform.forward * (input.MoveInput * config.forwardSpeed);
        Vector3 rightVel = transform.right * (input.StrafeInput * config.strafeSpeed);

        // Вертикаль из hover system
        float verticalVel = hoverSystem != null
            ? hoverSystem.CalculateKinematicVertical(dt, groundCheck != null && groundCheck.IsGrounded)
            : 0f;

        Vector3 totalVel = forwardVel + rightVel + Vector3.up * verticalVel;
        Vector3 delta = totalVel * dt;

        bool isGrounded = groundCheck != null && groundCheck.IsGrounded;
        if (isGrounded && verticalVel <= 0f)
            delta.y = 0f;

        transform.position += delta;
    }

    private void ApplyRotation(float dt)
    {
        float yawDelta = input.TurnInput * config.turnSpeed * dt;
        Quaternion currentRot = rb.rotation;
        Quaternion deltaRot = Quaternion.Euler(0f, yawDelta, 0f);
        Quaternion targetRot = deltaRot * currentRot;

        if (config.rotationSlerpSpeed <= 0f)
        {
            rb.MoveRotation(targetRot);
        }
        else
        {
            Quaternion slerped = Quaternion.Slerp(currentRot, targetRot, config.rotationSlerpSpeed * dt);
            rb.MoveRotation(slerped);
        }
    }

    private void ApplyHorizontalForce(float dt)
    {
        float desiredForward = input.MoveInput * config.forwardSpeed;
        float desiredStrafe = input.StrafeInput * config.strafeSpeed;

        Vector3 velocity = rb.linearVelocity;
        Vector3 localVel = transform.InverseTransformDirection(velocity);

        float deltaForward = desiredForward - localVel.z;
        float deltaStrafe = desiredStrafe - localVel.x;

        float maxDelta = config.maxHorizontalAcceleration * dt;
        deltaForward = Mathf.Clamp(deltaForward, -maxDelta, maxDelta);
        deltaStrafe = Mathf.Clamp(deltaStrafe, -maxDelta, maxDelta);

        Vector3 deltaVelLocal = new Vector3(deltaStrafe, 0f, deltaForward);
        Vector3 requiredAccel = deltaVelLocal / Mathf.Max(dt, 0.0001f);
        Vector3 requiredForce = requiredAccel * rb.mass;

        rb.AddRelativeForce(requiredForce, ForceMode.Force);
    }
}