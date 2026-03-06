using UnityEngine;

/// <summary>
/// Вертикальная физика транспорта: hover PD-контроллер, прыжок, snap-режим.
/// Читает данные из PepelacMovementConfig, PepelacInputHandler, PepelacGroundCheck.
/// </summary>
[DisallowMultipleComponent]
public class PepelacHoverSystem : MonoBehaviour
{
    [Header("Debug")]
    public bool debugHover = false;

    // Состояние
    private float baseGroundY;
    private float targetHoverOffset;
    private float hoverLockUntil;
    private float snapBoostUntil;
    private float currentVerticalVelocity;

    // Зависимости (устанавливаются через Initialize)
    private PepelacMovementConfig config;
    private PepelacInputHandler input;
    private PepelacGroundCheck groundCheck;
    private Rigidbody rb;

    // Флаги для отслеживания отпускания Rise/Lower
    private bool wasRisePressed;
    private bool wasLowerPressed;

    public float TargetHoverOffset => targetHoverOffset;
    public float CurrentVerticalVelocity
    {
        get => currentVerticalVelocity;
        set => currentVerticalVelocity = value;
    }

    public void Initialize(PepelacMovementConfig movementConfig, PepelacInputHandler inputHandler,
                           PepelacGroundCheck ground, Rigidbody rigidbody)
    {
        config = movementConfig;
        input = inputHandler;
        groundCheck = ground;
        rb = rigidbody;

        baseGroundY = groundCheck.QuerySurfaceY(transform.position, fallbackToCurrentY: true);
        targetHoverOffset = 0f;
    }

    public void ResetState()
    {
        currentVerticalVelocity = 0f;
        hoverLockUntil = 0f;
        snapBoostUntil = 0f;
        wasRisePressed = false;
        wasLowerPressed = false;
    }

    public void RefreshBaseGround()
    {
        baseGroundY = groundCheck.QuerySurfaceY(transform.position, fallbackToCurrentY: true);
        if (targetHoverOffset < 0f) targetHoverOffset = 0f;
    }

    /// <summary>
    /// Обработка ввода высоты. Вызывать из Update.
    /// </summary>
    public void UpdateHoverInput(float dt)
    {
        if (config == null || input == null) return;

        bool isRising = input.RisePressed;
        bool isLowering = input.LowerPressed;

        if (isRising)
        {
            targetHoverOffset += config.riseSpeed * dt;
            if (targetHoverOffset > config.maxHoverOffset)
                targetHoverOffset = config.maxHoverOffset;
        }
        else if (isLowering)
        {
            targetHoverOffset -= config.lowerSpeed * dt;
            if (targetHoverOffset < 0f)
                targetHoverOffset = 0f;
        }

        // Обработка отпускания Rise/Lower — snap
        HandleReleaseSnap(isRising, isLowering);

        wasRisePressed = isRising;
        wasLowerPressed = isLowering;

        if (debugHover)
            Debug.Log($"[HoverInput] rising={isRising} lowering={isLowering} offset={targetHoverOffset:F2}");
    }

    private void HandleReleaseSnap(bool isRising, bool isLowering)
    {
        bool riseReleased = wasRisePressed && !isRising;
        bool lowerReleased = wasLowerPressed && !isLowering;

        if (riseReleased || lowerReleased)
        {
            float currentY = transform.position.y;
            targetHoverOffset = Mathf.Clamp(
                currentY - baseGroundY,
                0f,
                config != null ? config.maxHoverOffset : 50f);

            currentVerticalVelocity = 0f;

            if (config != null && config.snapOnRelease)
                snapBoostUntil = Time.time + config.snapDuration;

            if (debugHover)
                Debug.Log($"[HoverInput] Released -> targetOffset={targetHoverOffset:F2}");
        }
    }

    /// <summary>
    /// Обработка прыжка. Вызывать из FixedUpdate.
    /// </summary>
    public void ProcessJump()
    {
        if (config == null || input == null) return;
        if (!input.JumpRequested) return;

        input.ConsumeJump();

        if (rb != null && config.useRigidbody)
            rb.AddForce(Vector3.up * config.jumpImpulse, ForceMode.VelocityChange);
        else
            currentVerticalVelocity = config.jumpImpulse;

        hoverLockUntil = Time.time + config.jumpBreaksHoverDuration;
    }

    /// <summary>
    /// Применить вертикальную физику (PD-контроллер). Вызывать из FixedUpdate.
    /// </summary>
    public void ApplyVerticalPhysics(float dt)
    {
        if (rb == null || config == null) return;

        bool hoverBlocked = Time.time < hoverLockUntil;
        bool wantHover = targetHoverOffset > 0f;

        if (!config.holdHoverPreventsGravity || !wantHover || hoverBlocked)
            return;

        float targetY = baseGroundY + targetHoverOffset;
        float currentY = rb.position.y;
        float error = targetY - currentY;
        float velY = rb.linearVelocity.y;

        float kp = config.verticalSpringKp;
        float kd = config.verticalSpringKd;

        if (config.snapOnRelease && Time.time < snapBoostUntil)
        {
            kp *= config.snapForceMultiplier;
            kd *= config.snapForceMultiplier;
        }

        float forceY = kp * error - kd * velY;
        forceY = Mathf.Clamp(forceY, -config.maxVerticalForce, config.maxVerticalForce);

        rb.AddForce(Vector3.up * forceY, ForceMode.Force);

        if (debugHover)
        {
            Debug.Log($"[HoverPhysics] targetY={targetY:F2} currentY={currentY:F2} " +
                      $"err={error:F2} velY={velY:F2} forceY={forceY:F1}");
        }
    }

    /// <summary>
    /// Вертикальная физика для kinematic режима. Вызывать из FixedUpdate.
    /// Возвращает вертикальную скорость для применения к transform.
    /// </summary>
    public float CalculateKinematicVertical(float dt, bool isGrounded)
    {
        if (config == null) return 0f;

        bool hoverBlocked = Time.time < hoverLockUntil;
        bool wantHover = targetHoverOffset > 0f;

        if (config.holdHoverPreventsGravity && wantHover && !hoverBlocked)
        {
            float targetY = baseGroundY + targetHoverOffset;
            Vector3 pos = transform.position;
            pos.y = targetY;
            transform.position = pos;
            currentVerticalVelocity = 0f;
            return 0f;
        }

        if (isGrounded)
        {
            if (currentVerticalVelocity < 0f) currentVerticalVelocity = 0f;
        }
        else
        {
            currentVerticalVelocity += config.gravity * dt;
        }

        return currentVerticalVelocity;
    }
}