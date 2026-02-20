using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

[ExecuteAlways]
public class StandardGenerator : MonoBehaviour
{
    // ====================== Identity ======================

    [Header("Identity")]
    [SerializeField, HideInInspector]
    private string moduleType = ModuleTypesDatabase.TYPE_GENERATOR;

    [Range(1, 10)] public int ModuleTier = 1;

    [Header("Faction")]
    [Tooltip("Short name of the faction this generator belongs to (from FactionDatabase).")]
    [SerializeField] private string factionShortName = "";

    [Header("Volume & Fill")]
    [Range(0f, 100f)] public float VolumeCoefficientPercent = 100f;
    [Tooltip("If true — module may increase fill above ConstantFillPercent; ConstantFillPercent is the minimum.")]
    public bool VariableFill = true;
    [Range(0f, 100f)] public float ConstantFillPercent = 100f;

    // --- Computed (measured/derived) ---
    [SerializeField, HideInInspector] private float length = 1f;
    [SerializeField, HideInInspector] private float width = 1f;
    [SerializeField, HideInInspector] private float height = 1f;

    [SerializeField, HideInInspector] private float aabbVolume;
    [SerializeField, HideInInspector] private float realVolume;
    [SerializeField, HideInInspector] private float effectiveVolume;
    [SerializeField, HideInInspector] private float fillPercentUsed;
    [SerializeField, HideInInspector] private float massKg;

    // --- Generator-specific inputs ---
    [Header("Generator")]
    [Min(0f)] public float PowerBy0001m3 = 1f;
    [Range(1, 10)] public int FuelTier = 1;
    [Min(0f)]
    [Tooltip("Fuel by 0.001 m³ (kg/s) base value (for tier=1). Default 0.0001.")]
    public float FuelBy0001m3_Base = 0.0001f;

    // --- Outputs (stored rounded) ---
    [SerializeField, HideInInspector] private float powerTimesTierPer0001;
    [SerializeField, HideInInspector] private float fuelPer0001m3Tiered;
    [SerializeField, HideInInspector] private float specificPower;
    [SerializeField, HideInInspector] private float fuelKgPerS;

    // ====================== Public getters ======================

    public string ModuleType => moduleType;
    public string FactionShortName => factionShortName;
    public string FactionFullName
    {
        get
        {
            var db = FactionDatabase.Instance;
            if (db == null || string.IsNullOrEmpty(factionShortName)) return factionShortName;
            return db.GetFullName(factionShortName);
        }
    }

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

    // ====================== Public setters ======================

    public void SetFaction(string shortName)
    {
        factionShortName = shortName ?? "";
    }

    // ====================== Constants ======================

    const double MIN_FUEL_PER0001_D = 1e-6;
    const float MIN_FUEL_DISPLAY_TOTAL = 0.0001f;
    const float EPS_ROUND = 1e-7f;

    // ====================== Lifecycle ======================

    void OnEnable() => RecalculateAll();

    void OnValidate()
    {
        moduleType = ModuleTypesDatabase.TYPE_GENERATOR;

        ModuleTier = Mathf.Clamp(ModuleTier, 1, 10);
        FuelTier = Mathf.Clamp(FuelTier, 1, 10);
        VolumeCoefficientPercent = Mathf.Clamp(VolumeCoefficientPercent, 0f, 100f);
        ConstantFillPercent = Mathf.Clamp(ConstantFillPercent, 0f, 100f);
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

    // ====================== Calculation ======================

    public void RecalculateAll()
    {
        MeasureWorldDimensions();
        ComputeVolumesAndMass();
        ComputeGeneratorOutputs();
        RoundAndStoreResults();
    }

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
            var ws = new Vector3(
                Mathf.Abs(b.size.x * ls.x),
                Mathf.Abs(b.size.y * ls.y),
                Mathf.Abs(b.size.z * ls.z));
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
        effectiveVolume = realVolume;
        fillPercentUsed = ConstantFillPercent;
        massKg = realVolume * (fillPercentUsed / 100f) * 1000f;
    }

    private void ComputeGeneratorOutputs()
    {
        float unitsPer0001 = effectiveVolume * 1000f;

        float moduleCoeff = TierCoeffs.Get(ModuleTier);
        double rawPowerD = (double)PowerBy0001m3 * (double)unitsPer0001 * (double)moduleCoeff;
        specificPower = (float)rawPowerD;

        float fuelTierCoeff = TierCoeffs.Get(FuelTier);
        double rawFuelPer0001D = (fuelTierCoeff > 0f)
            ? (double)FuelBy0001m3_Base / (double)fuelTierCoeff
            : 0.0;
        if (rawFuelPer0001D <= 0.0) rawFuelPer0001D = MIN_FUEL_PER0001_D;
        fuelPer0001m3Tiered = (float)rawFuelPer0001D;

        float powerTierCoeff = TierCoeffs.Get(ModuleTier);
        double powerTimesTierD = (double)PowerBy0001m3 * (double)powerTierCoeff;
        powerTimesTierPer0001 = (float)powerTimesTierD;

        double totalFuelD = rawFuelPer0001D * (double)effectiveVolume * 1000.0;
        float totalFuel = (float)totalFuelD;
        if (totalFuel <= 0f) totalFuel = 0f;
        fuelKgPerS = totalFuel;
    }

    private void RoundAndStoreResults()
    {
        aabbVolume = RoundToWithEps(aabbVolume, 6);
        realVolume = RoundToWithEps(realVolume, 6);
        effectiveVolume = RoundToWithEps(effectiveVolume, 6);
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


// ======================== CUSTOM EDITOR ========================
#if UNITY_EDITOR
[CustomEditor(typeof(StandardGenerator))]
public class StandardGeneratorEditor : Editor
{
    SerializedProperty pModuleTier, pVolumeCoeff, pVariableFill, pConstantFill;
    SerializedProperty pPowerBy0001m3, pFuelTier, pFuelBy0001m3_Base;
    SerializedProperty pFactionShortName;

    StandardGenerator t;

    private string[] factionDisplayNames;
    private string[] factionShortNames;

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
            pPowerBy0001m3 = serializedObject.FindProperty(nameof(StandardGenerator.PowerBy0001m3));
            pFuelTier = serializedObject.FindProperty(nameof(StandardGenerator.FuelTier));
            pFuelBy0001m3_Base = serializedObject.FindProperty(nameof(StandardGenerator.FuelBy0001m3_Base));
            pFactionShortName = serializedObject.FindProperty("factionShortName");
        }

        RebuildFactionList();
    }

    private void RebuildFactionList()
    {
        var db = FactionDatabase.Instance;

        if (db == null || db.factions == null || db.factions.Count == 0)
        {
            factionDisplayNames = new string[] { "(None — no FactionDatabase)" };
            factionShortNames = new string[] { "" };
            return;
        }

        int count = db.factions.Count;
        factionDisplayNames = new string[count + 1];
        factionShortNames = new string[count + 1];

        factionDisplayNames[0] = "(None)";
        factionShortNames[0] = "";

        for (int i = 0; i < count; i++)
        {
            var f = db.factions[i];
            string sn = f.shortName ?? "";
            string fn = f.fullName ?? "";
            factionDisplayNames[i + 1] = string.IsNullOrEmpty(fn) ? sn : $"{sn}  —  {fn}";
            factionShortNames[i + 1] = sn;
        }
    }

    public override void OnInspectorGUI()
    {
        if (t == null) { base.OnInspectorGUI(); return; }

        serializedObject.Update();

        // ---- Identity ----
        EditorGUILayout.LabelField("Identity", EditorStyles.boldLabel);

        GUI.enabled = false;
        EditorGUILayout.TextField("Module Type", t.ModuleType);
        GUI.enabled = true;

        var mtDb = ModuleTypesDatabase.Instance;
        if (mtDb != null)
        {
            if (!mtDb.Exists(t.ModuleType))
            {
                EditorGUILayout.HelpBox(
                    $"Type \"{t.ModuleType}\" is NOT registered in ModuleTypesDatabase!\n" +
                    "Open ModuleTypesDatabase and add it, or click 'Add Missing Standard Types' there.",
                    MessageType.Error);
            }
        }
        else
        {
            EditorGUILayout.HelpBox(
                "ModuleTypesDatabase not found in Resources/.\n" +
                "Create via: Assets → Create → Game → Module Types Database\n" +
                "and place at Resources/ModuleTypesDatabase.asset",
                MessageType.Warning);
        }

        EditorGUILayout.PropertyField(pModuleTier);

        // ---- Faction ----
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Faction", EditorStyles.boldLabel);

        if (factionDisplayNames != null && factionShortNames != null && factionShortNames.Length > 1)
        {
            string current = pFactionShortName.stringValue ?? "";
            int selectedIndex = 0;
            for (int i = 0; i < factionShortNames.Length; i++)
            {
                if (factionShortNames[i] == current)
                {
                    selectedIndex = i;
                    break;
                }
            }

            EditorGUI.BeginChangeCheck();
            int newIndex = EditorGUILayout.Popup("Faction", selectedIndex, factionDisplayNames);
            if (EditorGUI.EndChangeCheck())
            {
                pFactionShortName.stringValue = factionShortNames[newIndex];
            }
        }
        else
        {
            EditorGUILayout.HelpBox(
                "FactionDatabase not found in Resources/ or is empty.\n" +
                "Create via: Assets → Create → Game → Faction Database\n" +
                "and place at Resources/FactionDatabase.asset",
                MessageType.Warning);
            EditorGUILayout.PropertyField(pFactionShortName, new GUIContent("Faction (short name)"));
        }

        // ---- Volume & Fill ----
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Volume & Fill", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(pVolumeCoeff, new GUIContent("Volume Coeff %"));
        EditorGUILayout.PropertyField(pVariableFill, new GUIContent("Variable Fill"));
        EditorGUILayout.PropertyField(pConstantFill, new GUIContent("Constant Fill %"));

        // ---- Computed ----
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Computed", EditorStyles.boldLabel);
        EditorGUILayout.LabelField("Length (X, m)", t.LengthMeters.ToString("F3"));
        EditorGUILayout.LabelField("Width  (Z, m)", t.WidthMeters.ToString("F3"));
        EditorGUILayout.LabelField("Height (Y, m)", t.HeightMeters.ToString("F3"));
        EditorGUILayout.LabelField("AABB Volume (m³)", t.AABBVolumeM3.ToString("F6"));
        EditorGUILayout.LabelField("Real Volume (m³)", t.RealVolumeM3.ToString("F6"));
        EditorGUILayout.LabelField("Effective Volume (m³)", t.EffectiveVolumeM3.ToString("F6"));
        EditorGUILayout.LabelField("Fill % used (min)", t.FillPercentUsed.ToString("F3"));
        EditorGUILayout.LabelField("Mass (kg)", t.MassKg.ToString("F3"));

        // ---- Generator (inputs) ----
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Generator (inputs)", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(pPowerBy0001m3, new GUIContent("Power by 0.001 m³ (energy/s)"));
        EditorGUILayout.PropertyField(pFuelTier);
        EditorGUILayout.PropertyField(pFuelBy0001m3_Base, new GUIContent("Fuel by 0.001 m³ (kg/s)"));

        // ---- Outputs ----
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