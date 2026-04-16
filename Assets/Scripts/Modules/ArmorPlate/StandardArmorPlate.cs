using UnityEngine;
using System;

#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Стоимость внутренних ресурсов на 1 литр (1 дм³) эффективного объема.
/// </summary>
[Serializable]
public class InternalResourceCost
{
    public ResourcesStorage.ResourceIndex resourceType;
    [Min(0f)] public float gramsPerLiter = 1f;
}

/// <summary>
/// Эталонный модуль Бронеплиты.
/// В отличие от других модулей, использует точный расчет объема меша.
/// </summary>
public class StandardArmorPlate : MonoBehaviour
{
    public const string TYPE_ARMORPLATE = "ArmorPlate";
    public string ModuleType => TYPE_ARMORPLATE;

    [Header("Identity")]
    [Range(1, 10)] public int ModuleTier = 1;
    public string factionShortName = "";
    public int blueprintId = 0;

    [Header("Geometry")]
    [SerializeField, HideInInspector] private float lengthMeters;
    [SerializeField, HideInInspector] private float widthMeters;
    [SerializeField, HideInInspector] private float heightMeters;
    [SerializeField, HideInInspector] private float volumeM3;

    [Header("Coefficients")]
    [Min(0.001f)] public float MassCoefficient = 1f;
    [Min(0.001f)] public float DurabilityCoefficient = 1f;
    [Min(0.001f)] public float WallThicknessCoefficient = 1f;

    [Header("Absorption Bonuses")]
    [Min(0f)] public float KineticAbsorptionRelativeBonus = 1f;
    [Min(0f)] public float ThermalAbsorptionRelativeBonus = 1f;
    [Min(0f)] public float ChemicalAbsorptionRelativeBonus = 1f;
    [Min(0f)] public float EnergyAbsorptionRelativeBonus = 1f;

    public int KineticAbsorptionAbsoluteBonus = 0;
    public int ThermalAbsorptionAbsoluteBonus = 0;
    public int ChemicalAbsorptionAbsoluteBonus = 0;
    public int EnergyAbsorptionAbsoluteBonus = 0;

    [Header("Resistance Bonuses")]
    [Min(0f)] public float KineticResistanceRelativeBonus = 1f;
    [Min(0f)] public float ThermalResistanceRelativeBonus = 1f;
    [Min(0f)] public float ChemicalResistanceRelativeBonus = 1f;
    [Min(0f)] public float EnergyResistanceRelativeBonus = 1f;

    public float KineticResistanceAbsoluteBonus = 0f;
    public float ThermalResistanceAbsoluteBonus = 0f;
    public float ChemicalResistanceAbsoluteBonus = 0f;
    public float EnergyResistanceAbsoluteBonus = 0f;

    [Header("Thermal Physics")]
    [Min(0f)] public float BaseHeating = 10f;
    [Min(0.001f)] public float HeatCapacityCoeff = 1000f;

    [Header("Internal Resource Costs")]
    public InternalResourceCost[] InternalResourceCosts = Array.Empty<InternalResourceCost>();

    [Header("Crafting")]
    [Min(0.001f)] public float CraftCoefficient = 1f;

    [Header("Destruction")]
    public bool IsVolatile = false;
    public DamageType ExplosionDamageType = DamageType.Kinetic;
    [Min(0f)] public float ExplosionRadiusCoefficient = 1f;
    [Min(0f)] public float ExplosionPenetrationCoefficient = 1f;
    [Min(0f)] public float ExplosionDamageCoefficient = 1f;

    [Header("Build Visual")]
    public float BuildVisualYawOffset = 0f;
    public Vector3 BuildAnchorLocal = Vector3.zero;
    public bool UseBuildAnchorPlacement = false;
    public Vector3Int BuildAnchorCellLocal = Vector3Int.zero;

    [Header("Description")]
    [TextArea(3, 10)] public string Description = "";

    // Public accessors
    public float LengthMeters => lengthMeters;
    public float WidthMeters => widthMeters;
    public float HeightMeters => heightMeters;
    public float VolumeM3 => volumeM3;
    public string FactionShortName => factionShortName;
    public int BlueprintId => blueprintId;

    private void OnValidate()
    {
        ModuleTier = Mathf.Clamp(ModuleTier, 1, 10);
        MassCoefficient = Mathf.Max(0.001f, MassCoefficient);
        DurabilityCoefficient = Mathf.Max(0.001f, DurabilityCoefficient);
        WallThicknessCoefficient = Mathf.Max(0.001f, WallThicknessCoefficient);

        KineticAbsorptionRelativeBonus = Mathf.Max(0f, KineticAbsorptionRelativeBonus);
        ThermalAbsorptionRelativeBonus = Mathf.Max(0f, ThermalAbsorptionRelativeBonus);
        ChemicalAbsorptionRelativeBonus = Mathf.Max(0f, ChemicalAbsorptionRelativeBonus);
        EnergyAbsorptionRelativeBonus = Mathf.Max(0f, EnergyAbsorptionRelativeBonus);

        KineticResistanceRelativeBonus = Mathf.Max(0f, KineticResistanceRelativeBonus);
        ThermalResistanceRelativeBonus = Mathf.Max(0f, ThermalResistanceRelativeBonus);
        ChemicalResistanceRelativeBonus = Mathf.Max(0f, ChemicalResistanceRelativeBonus);
        EnergyResistanceRelativeBonus = Mathf.Max(0f, EnergyResistanceRelativeBonus);

        BaseHeating = Mathf.Max(0f, BaseHeating);
        HeatCapacityCoeff = Mathf.Max(0.001f, HeatCapacityCoeff);
        CraftCoefficient = Mathf.Max(0.001f, CraftCoefficient);

        ExplosionRadiusCoefficient = Mathf.Max(0f, ExplosionRadiusCoefficient);
        ExplosionPenetrationCoefficient = Mathf.Max(0f, ExplosionPenetrationCoefficient);
        ExplosionDamageCoefficient = Mathf.Max(0f, ExplosionDamageCoefficient);

        RecalculateGeometry();
    }

    private void RecalculateGeometry()
    {
        MeshFilter meshFilter = GetComponent<MeshFilter>();
        if (meshFilter == null || meshFilter.sharedMesh == null)
        {
            lengthMeters = 0f;
            widthMeters = 0f;
            heightMeters = 0f;
            volumeM3 = 0f;
            return;
        }

        Bounds bounds = meshFilter.sharedMesh.bounds;
        Vector3 size = bounds.size;
        Vector3 scale = transform.lossyScale;

        lengthMeters = size.x * scale.x;
        widthMeters = size.z * scale.z;
        heightMeters = size.y * scale.y;

        volumeM3 = MeshVolumeCalculator.CalculateVolume(meshFilter, scale);
    }

    public float CalculateExplosionRadius(float mass)
    {
        return mass * ExplosionRadiusCoefficient * 0.01f;
    }

    public float CalculateExplosionPenetration(float volume, float mass, int alloyTier)
    {
        float tierCoeff = TierCoeffs.Get(alloyTier);
        return (mass / Mathf.Max(0.001f, volume)) * tierCoeff * ExplosionPenetrationCoefficient;
    }

    public float CalculateExplosionDamage(float mass, int alloyTier)
    {
        float tierCoeff = TierCoeffs.Get(alloyTier);
        return mass * tierCoeff * ExplosionDamageCoefficient;
    }
}

#if UNITY_EDITOR
[CustomEditor(typeof(StandardArmorPlate))]
public class StandardArmorPlateEditor : Editor
{
    private StandardArmorPlate t;
    private string[] factionDisplayNames;
    private string[] factionShortNames;

    private void OnEnable()
    {
        t = target as StandardArmorPlate;
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
        EditorGUILayout.PropertyField(serializedObject.FindProperty("ModuleTier"));

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Faction & Blueprint", EditorStyles.boldLabel);

        var pFaction = serializedObject.FindProperty("factionShortName");
        if (factionDisplayNames != null && factionShortNames != null && factionShortNames.Length > 1)
        {
            string current = pFaction.stringValue ?? "";
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
                pFaction.stringValue = factionShortNames[newIndex];
        }
        else
        {
            EditorGUILayout.PropertyField(pFaction, new GUIContent("Faction (short name)"));
        }

        EditorGUILayout.PropertyField(serializedObject.FindProperty("blueprintId"), new GUIContent("Blueprint ID"));

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Coefficients", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(serializedObject.FindProperty("MassCoefficient"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("DurabilityCoefficient"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("WallThicknessCoefficient"));

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Absorption Bonuses", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(serializedObject.FindProperty("KineticAbsorptionRelativeBonus"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("KineticAbsorptionAbsoluteBonus"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("ThermalAbsorptionRelativeBonus"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("ThermalAbsorptionAbsoluteBonus"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("ChemicalAbsorptionRelativeBonus"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("ChemicalAbsorptionAbsoluteBonus"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("EnergyAbsorptionRelativeBonus"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("EnergyAbsorptionAbsoluteBonus"));

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Resistance Bonuses", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(serializedObject.FindProperty("KineticResistanceRelativeBonus"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("KineticResistanceAbsoluteBonus"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("ThermalResistanceRelativeBonus"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("ThermalResistanceAbsoluteBonus"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("ChemicalResistanceRelativeBonus"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("ChemicalResistanceAbsoluteBonus"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("EnergyResistanceRelativeBonus"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("EnergyResistanceAbsoluteBonus"));

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Thermal Physics", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(serializedObject.FindProperty("BaseHeating"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("HeatCapacityCoeff"));

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Internal Resource Costs", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(serializedObject.FindProperty("InternalResourceCosts"), true);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Crafting", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(serializedObject.FindProperty("CraftCoefficient"));

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Destruction", EditorStyles.boldLabel);
        var pVolatile = serializedObject.FindProperty("IsVolatile");
        EditorGUILayout.PropertyField(pVolatile);
        if (pVolatile.boolValue)
        {
            EditorGUILayout.PropertyField(serializedObject.FindProperty("ExplosionDamageType"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("ExplosionRadiusCoefficient"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("ExplosionPenetrationCoefficient"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("ExplosionDamageCoefficient"));
        }

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Build Visual", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(serializedObject.FindProperty("BuildVisualYawOffset"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("BuildAnchorLocal"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("UseBuildAnchorPlacement"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("BuildAnchorCellLocal"));

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Description", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(serializedObject.FindProperty("Description"), GUIContent.none);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Calculated Geometry", EditorStyles.boldLabel);
        EditorGUILayout.LabelField("Length (X, m)", t.LengthMeters.ToString("F6"));
        EditorGUILayout.LabelField("Width  (Z, m)", t.WidthMeters.ToString("F6"));
        EditorGUILayout.LabelField("Height (Y, m)", t.HeightMeters.ToString("F6"));
        EditorGUILayout.LabelField("Volume (m³)", t.VolumeM3.ToString("F6"));

        serializedObject.ApplyModifiedProperties();
    }
}
#endif