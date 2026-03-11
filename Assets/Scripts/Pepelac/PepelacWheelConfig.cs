using UnityEngine;

[DisallowMultipleComponent]
public class PepelacWheelConfig : MonoBehaviour
{
    [Header("Drive")]
    [Min(0f)] public float motorTorque = 1800f;
    [Min(0f)] public float maxSteerAngle = 28f;
    [Min(0f)] public float brakeTorque = 3500f;
    [Min(0f)] public float reverseBrakeTorque = 5000f;
    [Min(0f)] public float idleBrakeTorque = 50f;

    [Header("Speed Limit")]
    [Min(1f)] public float maxSpeedKmh = 80f;

    [Header("Steering")]
    [Tooltip("Насколько сильно уменьшается угол руля на высокой скорости.")]
    [Range(0f, 1f)] public float highSpeedSteerReduction = 0.5f;

    [Header("Downforce")]
    [Min(0f)] public float extraDownforce = 50f;

    [Header("Debug")]
    public bool debugLog = false;
}