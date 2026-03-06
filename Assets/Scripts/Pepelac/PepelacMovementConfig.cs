using UnityEngine;

/// <summary>
/// Хранит настройки физики, ховера и движения Пепелаца.
/// </summary>
[DisallowMultipleComponent]
public class PepelacMovementConfig : MonoBehaviour
{
    [Header("Movement")]
    public float forwardSpeed = 8f;
    public float strafeSpeed = 6f;
    public float maxHorizontalAcceleration = 20f;
    public float turnSpeed = 90f;
    public float rotationSlerpSpeed = 10f;
    public float jumpImpulse = 5f;
    public float gravity = -9.81f;

    [Header("Hover")]
    public float riseSpeed = 2f;
    public float lowerSpeed = 3f;
    public float maxHoverOffset = 50f;
    public float verticalSpringKp = 300f;
    public float verticalSpringKd = 40f;
    public float maxVerticalForce = 5000f;
    public float jumpBreaksHoverDuration = 0.6f;
    public bool holdHoverPreventsGravity = true;

    [Header("Hover Snap")]
    public bool snapOnRelease = false;
    public float snapForceMultiplier = 3f;
    public float snapDuration = 0.15f;
    public bool useBaseGroundY = true;

    [Header("Physics")]
    public bool useRigidbody = true;
    public bool freezeTiltAxes = true;
}