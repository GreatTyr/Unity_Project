using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

[ExecuteAlways]
public class CalcMass : MonoBehaviour
{
    [Header("Measured (world meters) — read-only")]
    [SerializeField, HideInInspector] private float length = 1f;
    [SerializeField, HideInInspector] private float width = 1f;
    [SerializeField, HideInInspector] private float height = 1f;

    [Header("Coefficients")]
    [Tooltip("Доля заполненного объёма (FillFactor). Процент заполненного внутреннего объёма от внешнего (0.1 - 100.0).")]
    [Range(0.1f, 100f)] public float FillFactor = 100f;

    [Tooltip("Volume coefficient (0 - 100%). Multiply AABB volume (L*W*H) by this % to get object volume. E.g. cube=100, sphere≈52.36")]
    [Range(0f, 100f)] public float VolumeCoefficientPercent = 100f;

    [Space]
    [SerializeField, HideInInspector] private float fullAABBVolume = 0f; // m^3 (L*W*H)
    [SerializeField, HideInInspector] private float effectiveVolume = 0f; // m^3 (after volume coefficient)
    [SerializeField, HideInInspector] private float massKg = 0f; // kg

    public float LengthMeters => length;
    public float WidthMeters => width;
    public float HeightMeters => height;
    public float AABBVolumeM3 => fullAABBVolume;
    public float EffectiveVolumeM3 => effectiveVolume;
    public float MassKg => massKg;

    private void OnEnable() => RecalculateAll();
    private void OnValidate()
    {
        FillFactor = Mathf.Clamp(FillFactor, 0.1f, 100f);
        VolumeCoefficientPercent = Mathf.Clamp(VolumeCoefficientPercent, 0f, 100f);
        RecalculateAll();
    }

#if UNITY_EDITOR
    private void Update()
    {
        if (!Application.isPlaying) RecalculateAll();
    }
#endif

    public void RecalculateAll()
    {
        MeasureWorldDimensions();
        ComputeVolumesAndMass();
    }

    private void MeasureWorldDimensions()
    {
        Renderer rend = GetComponentInChildren<Renderer>();
        if (rend != null)
        {
            Vector3 ws = rend.bounds.size;
            length = Mathf.Max(0f, ws.x);
            height = Mathf.Max(0f, ws.y);
            width = Mathf.Max(0f, ws.z);
            return;
        }

        Collider col = GetComponentInChildren<Collider>();
        if (col != null)
        {
            Vector3 ws = col.bounds.size;
            length = Mathf.Max(0f, ws.x);
            height = Mathf.Max(0f, ws.y);
            width = Mathf.Max(0f, ws.z);
            return;
        }

        MeshFilter mf = GetComponentInChildren<MeshFilter>();
        if (mf != null && mf.sharedMesh != null)
        {
            Bounds b = mf.sharedMesh.bounds;
            Vector3 localSize = b.size;
            Transform meshT = mf.transform;
            Vector3 ls = meshT.lossyScale;
            Vector3 ws;
            ws.x = Mathf.Abs(localSize.x * ls.x);
            ws.y = Mathf.Abs(localSize.y * ls.y);
            ws.z = Mathf.Abs(localSize.z * ls.z);
            length = Mathf.Max(0f, ws.x);
            height = Mathf.Max(0f, ws.y);
            width = Mathf.Max(0f, ws.z);
            return;
        }

        Vector3 approx = transform.lossyScale;
        length = Mathf.Max(0f, Mathf.Abs(approx.x));
        height = Mathf.Max(0f, Mathf.Abs(approx.y));
        width = Mathf.Max(0f, Mathf.Abs(approx.z));
    }

    private void ComputeVolumesAndMass()
    {
        fullAABBVolume = Mathf.Max(0f, length) * Mathf.Max(0f, width) * Mathf.Max(0f, height);
        effectiveVolume = fullAABBVolume * (VolumeCoefficientPercent / 100f);
        massKg = effectiveVolume * (FillFactor / 100f) * 1000f;
    }
}

#if UNITY_EDITOR
[CustomEditor(typeof(CalcMass))]
public class CalcMassEditor : Editor
{
    SerializedProperty pFillFactor;
    SerializedProperty pVolumeCoeff;
    CalcMass t;

    void OnEnable()
    {
        pFillFactor = serializedObject.FindProperty("FillFactor");
        pVolumeCoeff = serializedObject.FindProperty("VolumeCoefficientPercent");
        t = (CalcMass)target;
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        EditorGUILayout.LabelField("Measured (world meters)", EditorStyles.boldLabel);
        GUI.enabled = false;
        EditorGUILayout.FloatField("Length (X, m)", t.LengthMeters);
        EditorGUILayout.FloatField("Width  (Z, m)", t.WidthMeters);
        EditorGUILayout.FloatField("Height (Y, m)", t.HeightMeters);
        GUI.enabled = true;

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Коэффициенты", EditorStyles.boldLabel);

        EditorGUILayout.PropertyField(pVolumeCoeff, new GUIContent("Volume Coeff %"));
        pVolumeCoeff.floatValue = Mathf.Round(pVolumeCoeff.floatValue * 10f) / 10f;

        EditorGUILayout.PropertyField(pFillFactor, new GUIContent("Доля заполненного объёма (FillFactor)"));
        pFillFactor.floatValue = Mathf.Clamp(Mathf.Round(pFillFactor.floatValue * 10f) / 10f, 0.1f, 100f);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Computed (read-only)", EditorStyles.boldLabel);
        GUI.enabled = false;
        EditorGUILayout.FloatField("AABB Volume (m³)", t.AABBVolumeM3);
        EditorGUILayout.FloatField("Effective Volume (m³)", t.EffectiveVolumeM3);
        EditorGUILayout.FloatField("Mass (kg)", t.MassKg);
        GUI.enabled = true;

        serializedObject.ApplyModifiedProperties();
    }
}
#endif