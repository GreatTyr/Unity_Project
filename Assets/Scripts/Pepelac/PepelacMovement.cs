using UnityEngine;

/// <summary>
/// Горизонтальное движение транспорта: поворот + движение через Rigidbody или transform.
/// Читает данные из PepelacMain и PepelacInputHandler.
/// </summary>
[DisallowMultipleComponent]
public class PepelacMovement : MonoBehaviour
{
    private PepelacMain main;
    private PepelacInputHandler input;
    private PepelacGroundCheck groundCheck;
    private PepelacHoverSystem hoverSystem;
    private Rigidbody rb;

    public void Initialize(PepelacMain pepelacMain, PepelacInputHandler inputHandler,
                           PepelacGroundCheck ground, PepelacHoverSystem hover, Rigidbody rigidbody)
    {
        main = pepelacMain;
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
        if (rb == null || main == null || input == null) return;

        ApplyRotation(dt);
        ApplyHorizontalForce(dt);
    }

    /// <summary>
    /// Полное движение через transform (fallback без Rigidbody). Вызывать из FixedUpdate.
    /// </summary>
    public void TickKinematicMovement(float dt)
    {
        if (main == null || input == null) return;

        // Поворот
        float yawDelta = input.TurnInput * main.turnSpeed * dt;
        transform.Rotate(0f, yawDelta, 0f);

        // Горизонтальное движение
        Vector3 forwardVel = transform.forward * (input.MoveInput * main.forwardSpeed);
        Vector3 rightVel = transform.right * (input.StrafeInput * main.strafeSpeed);

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
        float yawDelta = input.TurnInput * main.turnSpeed * dt;
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
    }

    private void ApplyHorizontalForce(float dt)
    {
        float desiredForward = input.MoveInput * main.forwardSpeed;
        float desiredStrafe = input.StrafeInput * main.strafeSpeed;

        Vector3 velocity = rb.linearVelocity;
        Vector3 localVel = transform.InverseTransformDirection(velocity);

        float deltaForward = desiredForward - localVel.z;
        float deltaStrafe = desiredStrafe - localVel.x;

        float maxDelta = main.maxHorizontalAcceleration * dt;
        deltaForward = Mathf.Clamp(deltaForward, -maxDelta, maxDelta);
        deltaStrafe = Mathf.Clamp(deltaStrafe, -maxDelta, maxDelta);

        Vector3 deltaVelLocal = new Vector3(deltaStrafe, 0f, deltaForward);
        Vector3 requiredAccel = deltaVelLocal / Mathf.Max(dt, 0.0001f);
        Vector3 requiredForce = requiredAccel * rb.mass;

        rb.AddRelativeForce(requiredForce, ForceMode.Force);
    }
}