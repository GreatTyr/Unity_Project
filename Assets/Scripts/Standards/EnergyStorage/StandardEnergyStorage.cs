using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

[ExecuteAlways]
public class StandardEnergyStorage : MonoBehaviour
{
    public const string TYPE_ENERGY_STORAGE = "EnergyStorage";

    [Header("Identity")]
    [SerializeField, HideInInspector] private string moduleType = TYPE_ENERGY_STORAGE;
    [Range(1, 10)] public int ModuleTier = 1;

    [Header("Faction")]
    [SerializeField] private string factionShortName = "";

    [Header("Blueprint")]
    [Tooltip("Уникальный ID чертежа внутри фракции (например: 001)")]
    [SerializeField] private string blueprintId = "001";

    [Header("Volume & Fill")]
    [Range(0f, 100f)] public float VolumeCoefficientPercent = 100f;
    [Range(0f, 100f)] public float ConstantFillPercent = 100f;

    [Header("Crafting")]
    [Tooltip("Время крафта (в секундах) на 1 литр (0.001 м3) объема.")]
    [Min(0f)] public float CraftTimePerLiter = 0.5f;

    [SerializeField, HideInInspector] private float length = 1f;
    [SerializeField, HideInInspector] private float width = 1f;
    [SerializeField, HideInInspector] private float height = 1f;

    [SerializeField, HideInInspector] private float aabbVolume;
    [SerializeField, HideInInspector] private float realVolume;
    [SerializeField, HideInInspector] private float effectiveVolume;
    [SerializeField, HideInInspector] private float fillPercentUsed;
    [SerializeField, HideInInspector] private float massKg;

    [SerializeField, HideInInspector] private float energyCapacity;

    public string ModuleType => moduleType;
    public string FactionShortName => factionShortName;
    public string BlueprintId => blueprintId;

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

    const float EPS_ROUND = 1e-7f;

    public void SetFaction(string shortName) => factionShortName = shortName ?? "";

    void OnEnable() => RecalculateAll();

    void OnValidate()
    {
        moduleType = TYPE_ENERGY_STORAGE;
        ModuleTier = Mathf.Clamp(ModuleTier, 1, 10);
        VolumeCoefficientPercent = Mathf.Clamp(VolumeCoefficientPercent, 0f, 100f);
        ConstantFillPercent = Mathf.Clamp(ConstantFillPercent, 0f, 100f);
        CraftTimePerLiter = Mathf.Max(0f, CraftTimePerLiter);
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
        ComputeOutputs();
        RoundAndStoreResults();
    }

    private void MeasureWorldDimensions()
    {
        Vector3 size = ModuleMeasurer.GetSize(this.gameObject);
        length = size.x;
        height = size.y;
        width = size.z;
    }

    private void ComputeVolumesAndMass()
    {
        aabbVolume = Mathf.Max(0f, length) * Mathf.Max(0f, width) * Mathf.Max(0f, height);
        realVolume = aabbVolume * Mathf.Clamp01(VolumeCoefficientPercent / 100f);
        effectiveVolume = realVolume;
        fillPercentUsed = ConstantFillPercent;
        massKg = realVolume * (fillPercentUsed / 100f) * 1000f;
    }

    private void ComputeOutputs()
    {
        float effVolDm3 = effectiveVolume * 1000f;
        float tierCoeff = TierCoeffs.Get(ModuleTier);
        energyCapacity = effVolDm3 * tierCoeff;
    }

    private void RoundAndStoreResults()
    {
        aabbVolume = RoundToWithEps(aabbVolume, 6);
        realVolume = RoundToWithEps(realVolume, 6);
        effectiveVolume = RoundToWithEps(effectiveVolume, 6);
        fillPercentUsed = RoundToWithEps(fillPercentUsed, 3);
        massKg = RoundToWithEps(massKg, 3);
        energyCapacity = RoundToWithEps(energyCapacity, 3);
    }

    static float RoundToWithEps(float v, int d)
    {
        float mul = Mathf.Pow(10f, d);
        return Mathf.Round((v + EPS_ROUND) * mul) / mul;
    }
}

#if UNITY_EDITOR
[CustomEditor(typeof(StandardEnergyStorage))]
public class StandardEnergyStorageEditor : Editor
{
    SerializedProperty pModuleTier, pVolumeCoeff, pConstantFill;
    SerializedProperty pFactionShortName, pBlueprintId, pCraftTime;
    StandardEnergyStorage t;

    private string[] factionDisplayNames;
    private string[] factionShortNames;

    void OnEnable()
    {
        t = target as StandardEnergyStorage;
        if (t == null || serializedObject == null) return;

        pModuleTier = serializedObject.FindProperty(nameof(StandardEnergyStorage.ModuleTier));
        pVolumeCoeff = serializedObject.FindProperty(nameof(StandardEnergyStorage.VolumeCoefficientPercent));
        pConstantFill = serializedObject.FindProperty(nameof(StandardEnergyStorage.ConstantFillPercent));
        pFactionShortName = serializedObject.FindProperty("factionShortName");
        pBlueprintId = serializedObject.FindProperty("blueprintId");
        pCraftTime = serializedObject.FindProperty(nameof(StandardEnergyStorage.CraftTimePerLiter));

        RebuildFactionList();
    }

    private void RebuildFactionList()
    {
        var db = FactionDatabase.Instance;
        if (db == null || db.factions == null || db.factions.Count == 0)
        {
            factionDisplayNames = new[] { "(None — no FactionDatabase)" };
            factionShortNames = new[] { "" };
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
        if (t == null) return;
        serializedObject.Update();

        EditorGUILayout.LabelField("Identity", EditorStyles.boldLabel);
        GUI.enabled = false;
        EditorGUILayout.TextField("Module Type", t.ModuleType);
        GUI.enabled = true;
        EditorGUILayout.PropertyField(pModuleTier);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Faction & Blueprint", EditorStyles.boldLabel);

        if (factionShortNames != null && factionShortNames.Length > 1)
        {
            string current = pFactionShortName.stringValue ?? "";
            int selectedIndex = System.Array.IndexOf(factionShortNames, current);
            if (selectedIndex < 0) selectedIndex = 0;

            EditorGUI.BeginChangeCheck();
            int newIndex = EditorGUILayout.Popup("Faction", selectedIndex, factionDisplayNames);
            if (EditorGUI.EndChangeCheck())
                pFactionShortName.stringValue = factionShortNames[newIndex];
        }
        else
        {
            EditorGUILayout.PropertyField(pFactionShortName, new GUIContent("Faction (short name)"));
        }

        EditorGUILayout.PropertyField(pBlueprintId, new GUIContent("Blueprint ID"));

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Volume & Fill", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(pVolumeCoeff, new GUIContent("Volume Coeff %"));
        EditorGUILayout.PropertyField(pConstantFill, new GUIContent("Constant Fill %"));

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Crafting", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(pCraftTime, new GUIContent("Craft Time Per Liter (sec/l)"));

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Computed", EditorStyles.boldLabel);
        EditorGUILayout.LabelField("Length (X, m)", t.LengthMeters.ToString("0.###"));
        EditorGUILayout.LabelField("Width  (Z, m)", t.WidthMeters.ToString("0.###"));
        EditorGUILayout.LabelField("Height (Y, m)", t.HeightMeters.ToString("0.###"));
        EditorGUILayout.LabelField("AABB Volume (m³)", t.AABBVolumeM3.ToString("F6"));
        EditorGUILayout.LabelField("Real Volume (m³)", t.RealVolumeM3.ToString("F6"));
        EditorGUILayout.LabelField("Effective Volume (m³)", t.EffectiveVolumeM3.ToString("F6"));
        EditorGUILayout.LabelField("Fill % used", t.FillPercentUsed.ToString("0.###"));
        EditorGUILayout.LabelField("Mass (kg)", t.MassKg.ToString("0.###"));
        EditorGUILayout.LabelField("Energy Capacity", t.EnergyCapacity.ToString("F3"));

        serializedObject.ApplyModifiedProperties();
    }
}
#endif