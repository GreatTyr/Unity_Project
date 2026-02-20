using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

[ExecuteAlways]
public class StandardEnergyStorage : MonoBehaviour
{
    // ====================== Identity ======================

    [Header("Identity")]
    [SerializeField, HideInInspector]
    private string moduleType = ModuleTypesDatabase.TYPE_ENERGY_STORAGE;

    [Range(1, 10)] public int ModuleTier = 1;

    [Header("Faction")]
    [Tooltip("Short name of the faction (from FactionDatabase).")]
    [SerializeField] private string factionShortName = "";

    [Header("Volume & Fill")]
    [Range(0f, 100f)] public float VolumeCoefficientPercent = 100f;
    [Tooltip("If true — module may increase fill above ConstantFillPercent.")]
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

    // --- EnergyStorage-specific outputs ---
    [SerializeField, HideInInspector] private float energyCapacity;

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
    public float EnergyCapacity => energyCapacity;
    public float VolCoeffPercent => VolumeCoefficientPercent;

    // ====================== Public setters ======================

    public void SetFaction(string shortName)
    {
        factionShortName = shortName ?? "";
    }

    // ====================== Static calculation ======================

    /// <summary>
    /// Рассчитать ёмкость хранилища энергии.
    /// capacity = TierCoeffs.Get(moduleTier) * effectiveVolumeDm3
    /// effectiveVolumeDm3 = effectiveVolumeM3 * 1000
    /// </summary>
    public static float CalcCapacity(float effectiveVolumeM3, int moduleTier)
    {
        float effVolDm3 = effectiveVolumeM3 * 1000f;
        float tierCoeff = TierCoeffs.Get(moduleTier);
        return R3(effVolDm3 * tierCoeff);
    }

    // ====================== Constants ======================

    const float EPS_ROUND = 1e-7f;

    // ====================== Lifecycle ======================

    void OnEnable() => RecalculateAll();

    void OnValidate()
    {
        moduleType = ModuleTypesDatabase.TYPE_ENERGY_STORAGE;
        ModuleTier = Mathf.Clamp(ModuleTier, 1, 10);
        VolumeCoefficientPercent = Mathf.Clamp(VolumeCoefficientPercent, 0f, 100f);
        ConstantFillPercent = Mathf.Clamp(ConstantFillPercent, 0f, 100f);
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
        ComputeEnergyStorageOutputs();
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
        aabbVolume = R6(Mathf.Max(0f, length) * Mathf.Max(0f, width) * Mathf.Max(0f, height));
        realVolume = R6(aabbVolume * Mathf.Clamp01(VolumeCoefficientPercent / 100f));
        effectiveVolume = R6(realVolume);
        fillPercentUsed = ConstantFillPercent;
        massKg = R3(realVolume * (fillPercentUsed / 100f) * 1000f);
    }

    private void ComputeEnergyStorageOutputs()
    {
        energyCapacity = CalcCapacity(effectiveVolume, ModuleTier);
    }

    // ====================== Rounding ======================

    private static float R3(float v)
    {
        return (float)System.Math.Round(v, 3);
    }

    private static float R6(float v)
    {
        return (float)System.Math.Round(v, 6);
    }
}


// ======================== CUSTOM EDITOR ========================
#if UNITY_EDITOR
[CustomEditor(typeof(StandardEnergyStorage))]
public class StandardEnergyStorageEditor : Editor
{
    SerializedProperty pModuleTier, pVolumeCoeff, pVariableFill, pConstantFill;
    SerializedProperty pFactionShortName;

    StandardEnergyStorage t;

    private string[] factionDisplayNames;
    private string[] factionShortNames;

    void OnEnable()
    {
        t = target as StandardEnergyStorage;
        if (t == null) return;

        if (serializedObject != null)
        {
            pModuleTier = serializedObject.FindProperty(nameof(StandardEnergyStorage.ModuleTier));
            pVolumeCoeff = serializedObject.FindProperty(nameof(StandardEnergyStorage.VolumeCoefficientPercent));
            pVariableFill = serializedObject.FindProperty(nameof(StandardEnergyStorage.VariableFill));
            pConstantFill = serializedObject.FindProperty(nameof(StandardEnergyStorage.ConstantFillPercent));
            pFactionShortName = serializedObject.FindProperty("factionShortName");
        }

        RebuildFactionList();
    }

    private void RebuildFactionList()
    {
        var db = FactionDatabase.Instance;
        if (db == null || db.factions == null || db.factions.Count == 0)
        {
            factionDisplayNames = new string[] { "(None)" };
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
        if (mtDb != null && !mtDb.Exists(t.ModuleType))
        {
            EditorGUILayout.HelpBox(
                $"Type \"{t.ModuleType}\" is NOT registered in ModuleTypesDatabase!",
                MessageType.Error);
        }

        EditorGUILayout.PropertyField(pModuleTier);

        // ---- Faction ----
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Faction", EditorStyles.boldLabel);

        if (factionShortNames.Length > 1)
        {
            string current = pFactionShortName.stringValue ?? "";
            int selectedIndex = 0;
            for (int i = 0; i < factionShortNames.Length; i++)
            {
                if (factionShortNames[i] == current) { selectedIndex = i; break; }
            }

            EditorGUI.BeginChangeCheck();
            int newIndex = EditorGUILayout.Popup("Faction", selectedIndex, factionDisplayNames);
            if (EditorGUI.EndChangeCheck())
                pFactionShortName.stringValue = factionShortNames[newIndex];
        }
        else
        {
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
        EditorGUILayout.LabelField("Fill % used", t.FillPercentUsed.ToString("F3"));
        EditorGUILayout.LabelField("Mass (kg)", t.MassKg.ToString("F3"));

        // ---- Output ----
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Energy Storage Output", EditorStyles.boldLabel);
        EditorGUILayout.LabelField("Energy Capacity", t.EnergyCapacity.ToString("F3"));

        serializedObject.ApplyModifiedProperties();
    }
}
#endif