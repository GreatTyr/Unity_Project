using UnityEngine;
using System;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Эталонный модуль Хранилища Энергии. Наследует общие параметры от StandardModuleBase.
/// </summary>
public class StandardEnergyStorage : StandardModuleBase
{
    public const string TYPE_ENERGY_STORAGE = "EnergyStorage";
    public override string ModuleType => TYPE_ENERGY_STORAGE;

    [Header("Capacity")]
    [Min(0f)] public float CapacityCoefficient = 1f;

    [SerializeField, HideInInspector] private float energyCapacity;

    public float EnergyCapacity => energyCapacity;

    protected override void OnValidate()
    {
        base.OnValidate();
        CapacityCoefficient = Mathf.Max(0f, CapacityCoefficient);
    }

    protected override void ComputeSpecificOutputs()
    {
        float effVolDm3 = effectiveVolume * 1000f;
        float tierCoeff = TierCoeffs.Get(ModuleTier);

        energyCapacity = effVolDm3 * tierCoeff * CapacityCoefficient * (fillPercentUsed / 100f);
    }

    protected override void RoundAndStoreSpecificResults()
    {
        energyCapacity = RoundToWithEps(energyCapacity, 3);
    }
}

#if UNITY_EDITOR
[CustomEditor(typeof(StandardEnergyStorage))]
public class StandardEnergyStorageEditor : Editor
{
    private SerializedProperty pModuleTier;
    private SerializedProperty pVolumeCoeff;
    private SerializedProperty pConstantFill;
    private SerializedProperty pInternalResourceCosts;

    private SerializedProperty pFactionShortName;
    private SerializedProperty pBlueprintId;
    private SerializedProperty pCraftTime;

    private SerializedProperty pBuildVisualYawOffset;
    private SerializedProperty pBuildAnchorLocal;
    private SerializedProperty pUseBuildAnchorPlacement;
    private SerializedProperty pBuildAnchorCellLocal;

    private SerializedProperty pIsVolatile;
    private SerializedProperty pExplosionDamageType;
    private SerializedProperty pExplosionRadiusCoeff;
    private SerializedProperty pExplosionPenetrationCoeff;
    private SerializedProperty pExplosionDamageCoeff;

    private StandardEnergyStorage t;
    private string[] factionDisplayNames;
    private string[] factionShortNames;

    private void OnEnable()
    {
        t = target as StandardEnergyStorage;
        if (t == null || serializedObject == null) return;

        pModuleTier = serializedObject.FindProperty("ModuleTier");
        pVolumeCoeff = serializedObject.FindProperty("VolumeCoefficientPercent");
        pConstantFill = serializedObject.FindProperty("ConstantFillPercent");
        pInternalResourceCosts = serializedObject.FindProperty("InternalResourceCosts");

        pFactionShortName = serializedObject.FindProperty("factionShortName");
        pBlueprintId = serializedObject.FindProperty("blueprintId");
        pCraftTime = serializedObject.FindProperty("CraftCoefficient");

        pBuildVisualYawOffset = serializedObject.FindProperty("BuildVisualYawOffset");
        pBuildAnchorLocal = serializedObject.FindProperty("BuildAnchorLocal");
        pUseBuildAnchorPlacement = serializedObject.FindProperty("UseBuildAnchorPlacement");
        pBuildAnchorCellLocal = serializedObject.FindProperty("BuildAnchorCellLocal");



        pIsVolatile = serializedObject.FindProperty("IsVolatile");
        pExplosionDamageType = serializedObject.FindProperty("ExplosionDamageType");
        pExplosionRadiusCoeff = serializedObject.FindProperty("ExplosionRadiusCoefficient");
        pExplosionPenetrationCoeff = serializedObject.FindProperty("ExplosionPenetrationCoefficient");
        pExplosionDamageCoeff = serializedObject.FindProperty("ExplosionDamageCoefficient");

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

        // ================= IDENTITY =================
        EditorGUILayout.LabelField("Identity", EditorStyles.boldLabel);
        GUI.enabled = false;
        EditorGUILayout.TextField("Module Type", t.ModuleType);
        GUI.enabled = true;
        EditorGUILayout.PropertyField(pModuleTier);

        // ================= FACTION & BLUEPRINT =================
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Faction & Blueprint", EditorStyles.boldLabel);

        if (factionShortNames != null && factionShortNames.Length > 1)
        {
            string current = pFactionShortName.stringValue ?? "";
            int selectedIndex = Array.IndexOf(factionShortNames, current);
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

        // ================= VOLUME / FILL / RECIPE =================
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Volume / Fill / Recipe", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(pVolumeCoeff, new GUIContent("Volume Coeff %"));
        EditorGUILayout.PropertyField(pConstantFill, new GUIContent("Constant Fill %"));
        EditorGUILayout.PropertyField(pInternalResourceCosts, new GUIContent("Resources per Liter (1 dm3)"), true);

        // ================= BUILD VISUAL =================
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Build Visual", EditorStyles.boldLabel);
        if (pBuildVisualYawOffset != null)
            EditorGUILayout.PropertyField(pBuildVisualYawOffset, new GUIContent("Build Visual Yaw Offset"));
        if (pBuildAnchorLocal != null)
            EditorGUILayout.PropertyField(pBuildAnchorLocal, new GUIContent("Build Anchor Local"));
        if (pUseBuildAnchorPlacement != null)
            EditorGUILayout.PropertyField(pUseBuildAnchorPlacement, new GUIContent("Use Build Anchor Placement"));
        if (pBuildAnchorCellLocal != null)
            EditorGUILayout.PropertyField(pBuildAnchorCellLocal, new GUIContent("Build Anchor Cell Local"));
        // ================= SPECIFIC INPUTS =================
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Specific Inputs", EditorStyles.boldLabel);
        var pCap = serializedObject.FindProperty("CapacityCoefficient");
        if (pCap != null)
            EditorGUILayout.PropertyField(pCap, new GUIContent("Capacity Coefficient"));

        // ================= CRAFTING =================
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Crafting", EditorStyles.boldLabel);
        if (pCraftTime != null)
            EditorGUILayout.PropertyField(pCraftTime, new GUIContent("Craft Coefficient"));

        // ================= MODULE CAPABILITIES =================
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Module Capabilities", EditorStyles.boldLabel);
        var pTurn = serializedObject.FindProperty("CanTurnOnOff");
        var pTurnTime = serializedObject.FindProperty("TurnOnOffTime");
        var pPulse = serializedObject.FindProperty("CanPulseMode");
        var pPulseInt = serializedObject.FindProperty("PulseInterval");
        var pControl = serializedObject.FindProperty("IsControllable");

        if (pTurn != null) EditorGUILayout.PropertyField(pTurn, new GUIContent("Can Turn On/Off"));
        if (pTurnTime != null) EditorGUILayout.PropertyField(pTurnTime, new GUIContent("Turn On/Off Time"));
        if (pPulse != null) EditorGUILayout.PropertyField(pPulse, new GUIContent("Can Pulse Mode"));
        if (pPulseInt != null) EditorGUILayout.PropertyField(pPulseInt, new GUIContent("Pulse Interval"));
        if (pControl != null) EditorGUILayout.PropertyField(pControl, new GUIContent("Is Controllable"));

        // ================= DESTRUCTION =================
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Destruction", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(pIsVolatile, new GUIContent("Is Volatile (Взрывоопасен)"));
        if (pIsVolatile != null && pIsVolatile.boolValue)
        {
            EditorGUILayout.PropertyField(pExplosionDamageType, new GUIContent("Explosion Damage Type"));
            EditorGUILayout.PropertyField(pExplosionRadiusCoeff, new GUIContent("Radius Coefficient"));
            EditorGUILayout.PropertyField(pExplosionPenetrationCoeff, new GUIContent("Penetration Coefficient"));
            EditorGUILayout.PropertyField(pExplosionDamageCoeff, new GUIContent("Damage Coefficient"));
        }

        // ================= COMPUTED GEOMETRY & MASS =================
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Расчётная геометрия и масса", EditorStyles.boldLabel);
        EditorGUILayout.LabelField("Length (X, m)", t.LengthMeters.ToString("0.###"));
        EditorGUILayout.LabelField("Width  (Z, m)", t.WidthMeters.ToString("0.###"));
        EditorGUILayout.LabelField("Height (Y, m)", t.HeightMeters.ToString("0.###"));
        EditorGUILayout.LabelField("AABB Volume (m³)", t.AABBVolumeM3.ToString("F6"));
        EditorGUILayout.LabelField("Real Volume (m³)", t.RealVolumeM3.ToString("F6"));
        EditorGUILayout.LabelField("Effective Volume (m³)", t.EffectiveVolumeM3.ToString("F6"));
        EditorGUILayout.LabelField("Fill % used", t.FillPercentUsed.ToString() + "%");
        EditorGUILayout.LabelField("Reference Inner Mass (kg)", t.MassKg.ToString("0.###"));

        // ================= COMPUTED SPECIFIC =================
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Специфичные расчётные параметры", EditorStyles.boldLabel);
        EditorGUILayout.LabelField("Energy Capacity", t.EnergyCapacity.ToString("F3"));

        serializedObject.ApplyModifiedProperties();
    }
}
#endif