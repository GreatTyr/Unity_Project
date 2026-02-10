using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

[ExecuteAlways]
public class StandardGenerator : MonoBehaviour
{
    public enum ModuleType { Generator }

    [Header("Identity")]
    [SerializeField] private ModuleType moduleType = ModuleType.Generator;
    [Range(1, 10)] public int ModuleTier = 1;

    [Header("Volume & Fill")]
    [Range(0f, 100f)] public float VolumeCoefficientPercent = 100f;
    [Tooltip("If true — module may increase fill above ConstantFillPercent; ConstantFillPercent is the minimum.")]
    public bool VariableFill = true;
    [Range(0f, 100f)] public float ConstantFillPercent = 100f;
    [Min(0f)] public float ShellVolume = 0f;

    // --- Computed (measured/derived) ---
    [SerializeField, HideInInspector] private float length = 1f; // X (m)
    [SerializeField, HideInInspector] private float width = 1f; // Z (m)
    [SerializeField, HideInInspector] private float height = 1f; // Y (m)

    [SerializeField, HideInInspector] private float aabbVolume;
    [SerializeField, HideInInspector] private float realVolume;
    [SerializeField, HideInInspector] private float effectiveVolume;
    [SerializeField, HideInInspector] private float fillPercentUsed;
    [SerializeField, HideInInspector] private float massKg;

    // --- Generator-specific inputs ---
    [Header("Generator")]
    [Min(0f)] public float PowerBy0001m3 = 1f; // energy per 0.001 m^3 /s
    [Range(1, 10)] public int FuelTier = 1;
    [Min(0f)]
    [Tooltip("Fuel by 0.001 m³ (kg/s) base value (for tier=1). Default 0.0001.")]
    public float FuelBy0001m3_Base = 0.0001f; // default per request

    // --- Outputs (stored rounded) ---
    [SerializeField, HideInInspector] private float powerTimesTierPer0001; // rounded to 3 decimals
    [SerializeField, HideInInspector] private float fuelPer0001m3Tiered;    // rounded to 6 decimals
    [SerializeField, HideInInspector] private float specificPower;          // rounded to 3 decimals (total power)
    [SerializeField, HideInInspector] private float fuelKgPerS;             // rounded to 4 decimals (total fuel)

    // Public getters
    public ModuleType Type => moduleType;
    public float LengthMeters => length;
    public float WidthMeters => width;
    public float HeightMeters => height;
    public float AABBVolumeM3 => aabbVolume;
    public float RealVolumeM3 => realVolume;
    public float EffectiveVolumeM3 => effectiveVolume;
    public float FillPercentUsed => fillPercentUsed;
    public float MassKg => massKg;
    public float PowerBy0_001m3 => PowerBy0001m3;
    public float PowerTimesTierBy0_001m3 => powerTimesTierPer0001;
    public float FuelBy0_001m3_Tier => fuelPer0001m3Tiered;
    public float SpecificPower => specificPower;
    public float FuelKgPerS => fuelKgPerS;

    // constants
    const double MIN_FUEL_PER0001_D = 1e-6;
    const float MIN_FUEL_DISPLAY_TOTAL = 0.0001f;
    const float EPS_ROUND = 1e-7f;

    void OnEnable() => RecalculateAll();

    void OnValidate()
    {
        ModuleTier = Mathf.Clamp(ModuleTier, 1, 10);
        FuelTier = Mathf.Clamp(FuelTier, 1, 10);
        VolumeCoefficientPercent = Mathf.Clamp(VolumeCoefficientPercent, 0f, 100f);
        ConstantFillPercent = Mathf.Clamp(ConstantFillPercent, 0f, 100f);
        ShellVolume = Mathf.Max(0f, ShellVolume);
        PowerBy0001m3 = Mathf.Max(0f, PowerBy0001m3);
        FuelBy0001m3_Base = Mathf.Max(0f, FuelBy0001m3_Base);

        RecalculateAll();
    }

#if UNITY_EDITOR
    void Update()
    {
        if (!Application.isPlaying) RecalculateAll();
    }
#endif

    public void RecalculateAll()
    {
        MeasureWorldDimensions();
        ComputeVolumesAndMass();
        ComputeGeneratorOutputs();
        RoundAndStoreResults();
    }

    // Measurement
    private void MeasureWorldDimensions()
    {
        var rend = GetComponentInChildren<Renderer>();
        if (rend != null)
        {
            var s = rend.bounds.size;
            length = Mathf.Max(0f, s.x);
            height = Mathf.Max(0f, s.y);
            width = Mathf.Max(0f, s.z);
            return;
        }

        var col = GetComponentInChildren<Collider>();
        if (col != null)
        {
            var s = col.bounds.size;
            length = Mathf.Max(0f, s.x);
            height = Mathf.Max(0f, s.y);
            width = Mathf.Max(0f, s.z);
            return;
        }

        var mf = GetComponentInChildren<MeshFilter>();
        if (mf != null && mf.sharedMesh != null)
        {
            var b = mf.sharedMesh.bounds;
            var ls = mf.transform.lossyScale;
            var ws = new Vector3(Mathf.Abs(b.size.x * ls.x), Mathf.Abs(b.size.y * ls.y), Mathf.Abs(b.size.z * ls.z));
            length = Mathf.Max(0f, ws.x);
            height = Mathf.Max(0f, ws.y);
            width = Mathf.Max(0f, ws.z);
            return;
        }

        var approx = transform.lossyScale;
        length = Mathf.Max(0f, Mathf.Abs(approx.x));
        height = Mathf.Max(0f, Mathf.Abs(approx.y));
        width = Mathf.Max(0f, Mathf.Abs(approx.z));
    }

    private void ComputeVolumesAndMass()
    {
        aabbVolume = Mathf.Max(0f, length) * Mathf.Max(0f, width) * Mathf.Max(0f, height);
        realVolume = aabbVolume * Mathf.Clamp01(VolumeCoefficientPercent / 100f);
        effectiveVolume = Mathf.Max(0f, realVolume - ShellVolume);

        fillPercentUsed = ConstantFillPercent;
        massKg = effectiveVolume * (fillPercentUsed / 100f) * 1000f;
    }

    private void ComputeGeneratorOutputs()
    {
        float unitsPer0001 = effectiveVolume * 1000f;

        // Power total
        float moduleCoeff = TierCoeffs.Get(ModuleTier);
        double rawPowerD = (double)PowerBy0001m3 * (double)unitsPer0001 * (double)moduleCoeff;
        float rawPower = (float)rawPowerD;
        specificPower = rawPower;

        // Fuel per 0.001 m3 base -> tiered
        float fuelTierCoeff = TierCoeffs.Get(FuelTier);
        double rawFuelPer0001D = (fuelTierCoeff > 0f) ? (double)FuelBy0001m3_Base / (double)fuelTierCoeff : 0.0;
        if (rawFuelPer0001D <= 0.0) rawFuelPer0001D = MIN_FUEL_PER0001_D;
        fuelPer0001m3Tiered = (float)rawFuelPer0001D;

        // Power * Tier per 0.001 m3 (uses Tier coefficient)
        float powerTierCoeff = TierCoeffs.Get(ModuleTier);
        double powerTimesTierD = (double)PowerBy0001m3 * (double)powerTierCoeff;
        powerTimesTierPer0001 = (float)powerTimesTierD;

        // total engine fuel (kg/s) = fuelPer0001m3Tiered * effectiveVolume * 1000
        double totalFuelD = rawFuelPer0001D * (double)effectiveVolume * 1000.0;
        float totalFuel = (float)totalFuelD;
        if (totalFuel <= 0f) totalFuel = 0f;
        fuelKgPerS = totalFuel;
    }

    private void RoundAndStoreResults()
    {
        aabbVolume = RoundToWithEps(aabbVolume, 3);
        realVolume = RoundToWithEps(realVolume, 3);
        effectiveVolume = RoundToWithEps(effectiveVolume, 3);
        specificPower = RoundToWithEps(specificPower, 3);
        massKg = RoundToWithEps(massKg, 3);
        fillPercentUsed = RoundToWithEps(fillPercentUsed, 3);

        double perD = System.Math.Max((double)fuelPer0001m3Tiered, MIN_FUEL_PER0001_D);
        perD = System.Math.Round(perD * 1_000_000.0) / 1_000_000.0;
        fuelPer0001m3Tiered = (float)perD;

        powerTimesTierPer0001 = RoundToWithEps(powerTimesTierPer0001, 3);

        double eff = (double)effectiveVolume;
        double totalD = perD * eff * 1000.0;
        totalD = System.Math.Round(totalD * 10000.0) / 10000.0;
        if (totalD < MIN_FUEL_DISPLAY_TOTAL) totalD = MIN_FUEL_DISPLAY_TOTAL;
        fuelKgPerS = (float)totalD;
    }

    static float RoundTo(float v, int d)
    {
        float mul = Mathf.Pow(10f, d);
        return Mathf.Round(v * mul) / mul;
    }

    static float RoundToWithEps(float v, int d)
    {
        float mul = Mathf.Pow(10f, d);
        return Mathf.Round((v + EPS_ROUND) * mul) / mul;
    }
}

#if UNITY_EDITOR
[CustomEditor(typeof(StandardGenerator))]
public class StandardGeneratorEditor : Editor
{
    SerializedProperty pModuleTier, pVolumeCoeff, pVariableFill, pConstantFill, pShellVolume;
    SerializedProperty pPowerBy0001m3, pFuelTier, pFuelBy0001m3_Base;
    StandardGenerator t;

    void OnEnable()
    {
        t = target as StandardGenerator;
        if (t == null) return;

        if (serializedObject != null)
        {
            pModuleTier = serializedObject.FindProperty(nameof(StandardGenerator.ModuleTier));
            pVolumeCoeff = serializedObject.FindProperty(nameof(StandardGenerator.VolumeCoefficientPercent));
            pVariableFill = serializedObject.FindProperty(nameof(StandardGenerator.VariableFill));
            pConstantFill = serializedObject.FindProperty(nameof(StandardGenerator.ConstantFillPercent));
            pShellVolume = serializedObject.FindProperty(nameof(StandardGenerator.ShellVolume));
            pPowerBy0001m3 = serializedObject.FindProperty(nameof(StandardGenerator.PowerBy0001m3));
            pFuelTier = serializedObject.FindProperty(nameof(StandardGenerator.FuelTier));
            pFuelBy0001m3_Base = serializedObject.FindProperty(nameof(StandardGenerator.FuelBy0001m3_Base));
        }
    }

    public override void OnInspectorGUI()
    {
        if (t == null) { base.OnInspectorGUI(); return; }
        serializedObject.Update();

        EditorGUILayout.LabelField("Identity", EditorStyles.boldLabel);
        GUI.enabled = false;
        EditorGUILayout.EnumPopup("Module Type", t.Type);
        GUI.enabled = true;
        EditorGUILayout.PropertyField(pModuleTier);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Volume & Fill", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(pVolumeCoeff, new GUIContent("Volume Coeff %"));
        EditorGUILayout.PropertyField(pVariableFill, new GUIContent("Variable Fill"));
        EditorGUILayout.PropertyField(pConstantFill, new GUIContent("Constant Fill %"));
        EditorGUILayout.PropertyField(pShellVolume, new GUIContent("Shell Volume (m³)"));

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Computed", EditorStyles.boldLabel);
        EditorGUILayout.LabelField("Length (X, m)", t.LengthMeters.ToString("F3"));
        EditorGUILayout.LabelField("Width  (Z, m)", t.WidthMeters.ToString("F3"));
        EditorGUILayout.LabelField("Height (Y, m)", t.HeightMeters.ToString("F3"));
        EditorGUILayout.LabelField("AABB Volume (m³)", t.AABBVolumeM3.ToString("F3"));
        EditorGUILayout.LabelField("Real Volume (m³)", t.RealVolumeM3.ToString("F3"));
        EditorGUILayout.LabelField("Effective Volume (m³)", t.EffectiveVolumeM3.ToString("F3"));
        EditorGUILayout.LabelField("Fill % used (min)", t.FillPercentUsed.ToString("F3"));
        EditorGUILayout.LabelField("Mass (kg)", t.MassKg.ToString("F3"));

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Generator (inputs)", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(pPowerBy0001m3, new GUIContent("Power by 0.001 m³ (energy/s)"));
        EditorGUILayout.PropertyField(pFuelTier);
        EditorGUILayout.PropertyField(pFuelBy0001m3_Base, new GUIContent("Fuel by 0.001 m³ (kg/s)"));

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Outputs", EditorStyles.boldLabel);
        EditorGUILayout.LabelField("Power*Tier by 0.001 m³ (energy/s)", t.PowerTimesTierBy0_001m3.ToString("F3"));
        EditorGUILayout.LabelField("Fuel*Tier by 0.001 m³ (kg/s)", t.FuelBy0_001m3_Tier.ToString("F6"));
        EditorGUILayout.LabelField("Power (energy/s)", t.SpecificPower.ToString("F3"));
        EditorGUILayout.LabelField("Fuel (kg/s)", t.FuelKgPerS.ToString("F4"));

        serializedObject.ApplyModifiedProperties();
    }
}
#endif