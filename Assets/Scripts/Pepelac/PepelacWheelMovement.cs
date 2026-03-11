using System;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class PepelacWheelMovement : MonoBehaviour
{
    [Serializable]
    public class WheelSlot
    {
        public string name;
        public WheelCollider wheelCollider;
        public Transform wheelVisual;
        public bool canSteer;
        public bool canDrive;
        public bool canBrake = true;
    }

    [Header("References")]
    [SerializeField] private PepelacWheelConfig config;
    [SerializeField] private PepelacInputHandler inputHandler;
    [SerializeField] private Rigidbody rb;

    [Header("Wheel Setup")]
    [SerializeField] private List<WheelSlot> wheels = new List<WheelSlot>();


    private bool controlEnabled;

    public IReadOnlyList<WheelSlot> Wheels => wheels;

    private void Awake()
    {
        if (config == null)
            config = GetComponent<PepelacWheelConfig>();

        if (inputHandler == null)
            inputHandler = GetComponent<PepelacInputHandler>();

        if (rb == null)
            rb = GetComponent<Rigidbody>();
    }



    public void SetControlEnabled(bool enabled)
    {
        controlEnabled = enabled;
    }

    public void TickPhysics(float dt)
    {
        if (rb == null || config == null || inputHandler == null)
            return;

        ApplyWheelPhysics();
        ApplyDownforce();
        SyncWheelVisuals();
    }

    private void ApplyWheelPhysics()
    {
        float moveInput = controlEnabled ? inputHandler.MoveInput : 0f;
        float turnInput = controlEnabled ? inputHandler.TurnInput : 0f;

        float speedKmh = rb.linearVelocity.magnitude * 3.6f;
        float localForwardSpeed = transform.InverseTransformDirection(rb.linearVelocity).z;

        float steerReduction = Mathf.Lerp(1f, config.highSpeedSteerReduction, Mathf.Clamp01(speedKmh / config.maxSpeedKmh));
        float steerAngle = turnInput * config.maxSteerAngle * steerReduction;

        float motor = 0f;
        float brake = 0f;

        bool wantsMove = Mathf.Abs(moveInput) > 0.01f;
        bool movingForward = localForwardSpeed > 0.5f;
        bool movingBackward = localForwardSpeed < -0.5f;

        if (!wantsMove)
        {
            motor = 0f;
            brake = config.idleBrakeTorque;
        }
        else
        {
            bool reversingAgainstMotion =
                (moveInput > 0f && movingBackward) ||
                (moveInput < 0f && movingForward);

            if (reversingAgainstMotion)
            {
                motor = 0f;
                brake = config.reverseBrakeTorque;
            }
            else
            {
                if (speedKmh < config.maxSpeedKmh || Mathf.Sign(moveInput) != Mathf.Sign(localForwardSpeed))
                    motor = moveInput * config.motorTorque;
                else
                    motor = 0f;

                brake = 0f;
            }
        }

        for (int i = 0; i < wheels.Count; i++)
        {
            WheelSlot wheel = wheels[i];
            if (wheel == null || wheel.wheelCollider == null)
                continue;

            wheel.wheelCollider.steerAngle = wheel.canSteer ? steerAngle : 0f;
            wheel.wheelCollider.motorTorque = wheel.canDrive ? motor : 0f;
            wheel.wheelCollider.brakeTorque = wheel.canBrake ? brake : 0f;
        }

        if (config.debugLog)
        {
            Debug.Log($"[PepelacWheelMovement] speed={speedKmh:F1} km/h, move={moveInput:F2}, steer={turnInput:F2}, motor={motor:F1}, brake={brake:F1}");
        }
    }

    private void ApplyDownforce()
    {
        if (config.extraDownforce <= 0f || rb == null)
            return;

        float speed = rb.linearVelocity.magnitude;
        if (speed <= 0.1f)
            return;

        rb.AddForce(-transform.up * config.extraDownforce * speed, ForceMode.Force);
    }

    private void SyncWheelVisuals()
    {
        for (int i = 0; i < wheels.Count; i++)
        {
            WheelSlot wheel = wheels[i];
            if (wheel == null || wheel.wheelCollider == null || wheel.wheelVisual == null)
                continue;

            wheel.wheelCollider.GetWorldPose(out Vector3 pos, out Quaternion rot);
            wheel.wheelVisual.position = pos;
            wheel.wheelVisual.rotation = rot;
        }
    }
}