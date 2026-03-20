using UnityEngine;
using System;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Эталонный модуль Охлаждающего Радиатора. Наследует общие параметры от StandardModuleBase.
/// </summary>
public class StandardCooler : StandardModuleBase
{
    public const string TYPE_COOLER = "Cooler";
    public override string ModuleType => TYPE_COOLER;

    [Header("Cooler")]
    [Min(0f)] public float RadiusCoefficient = 1f;
    [Min(0f)] public float SpecificCoolingPowerBase = 1f;
    [Min(0f)] public float SpecificEnergyConsumption = 1f;

    [Header("Thermal Physics")]
    [Min(0f)] public float BaseHeating = 0f;
    [Min(0.001f)] public float HeatCapacityCoeff = 1f;

    [SerializeField, HideInInspector] private float specificCoolingPower;
    [SerializeField, HideInInspector] private float coolingPower;
    [SerializeField, HideInInspector] private float energyConsumption;
    [SerializeField, HideInInspector] private float coolingRadius;
    [SerializeField, HideInInspector] private float maxCoolingDifference;
    [SerializeField, HideInInspector] private float minTemperature;

    public float SpecificCoolingPower => specificCoolingPower;
    public float CoolingPower => coolingPower;
    public float EnergyConsumption => energyConsumption;
    public float CoolingRadius => coolingRadius;
    public float MaxCoolingDifference => maxCoolingDifference;
    public float MinTemperature => minTemperature;

    protected override void OnValidate()
    {
        base.OnValidate();

        RadiusCoefficient = Mathf.Max(0f, RadiusCoefficient);
        SpecificCoolingPowerBase = Mathf.Max(0f, SpecificCoolingPowerBase);
        SpecificEnergyConsumption = Mathf.Max(0f, SpecificEnergyConsumption);
        BaseHeating = Mathf.Max(0f, BaseHeating);
        HeatCapacityCoeff = Mathf.Max(0.001f, HeatCapacityCoeff);
    }

    protected override void ComputeSpecificOutputs()
    {
        float effectiveVolumeDm3 = effectiveVolume * 1000f;
        float moduleCoeff = TierCoeffs.Get(ModuleTier);

        specificCoolingPower = SpecificCoolingPowerBase * moduleCoeff;
        coolingPower = specificCoolingPower * effectiveVolumeDm3;
        energyConsumption = SpecificEnergyConsumption * effectiveVolumeDm3;
        coolingRadius = (LengthMeters + WidthMeters) / 2f * RadiusCoefficient;
        maxCoolingDifference = 30f * moduleCoeff;
        minTemperature = -20f * ModuleTier;
    }

    protected override void RoundAndStoreSpecificResults()
    {
        specificCoolingPower = RoundToWithEps(specificCoolingPower, 3);
        coolingPower = RoundToWithEps(coolingPower, 3);
        energyConsumption = RoundToWithEps(energyConsumption, 3);
        coolingRadius = RoundToWithEps(coolingRadius, 3);
        maxCoolingDifference = RoundToWithEps(maxCoolingDifference, 1);
        minTemperature = RoundToWithEps(minTemperature, 1);
    }
}

#if UNITY_EDITOR
[CustomEditor(typeof(StandardCooler))]
public class StandardCoolerEditor : Editor
{
    private SerializedProperty pModuleTier;
    private SerializedProperty pVolumeCoeff;
    private SerializedProperty pConstantFill;
    private SerializedProperty pInternalResourceCosts;

    private SerializedProperty pRadiusCoefficient;
    private SerializedProperty pSpecificCoolingPowerBase;
    private SerializedProperty pSpecificEnergyConsumption;
    private SerializedProperty pBaseHeating;
    private SerializedProperty pHeatCapacityCoeff;
    private SerializedProperty pCraftTime;

    private SerializedProperty pFactionShortName;
    private SerializedProperty pBlueprintId;

    private SerializedProperty pBuildVisualYawOffset;
    private SerializedProperty pBuildAnchorLocal;
    private SerializedProperty pUseBuildAnchorPlacement;
            private SerializedProperty pBuildAnchorCellLocal;

    private SerializedProperty pIsVolatile;
    private SerializedProperty pExplosionDamageType;
    private SerializedProperty pExplosionRadiusCoeff;
    private SerializedProperty pExplosionPenetrationCoeff;
    private SerializedProperty pExplosionDamageCoeff;

    private StandardCooler t;
    private string[] factionDisplayNames;
    private string[] factionShortNames;

    private void OnEnable()
    {
        t = target as StandardCooler;
        if (t == null || serializedObject == null) return;

        pModuleTier = serializedObject.FindProperty("ModuleTier");
        pVolumeCoeff = serializedObject.FindProperty("VolumeCoefficientPercent");
        pConstantFill = serializedObject.FindProperty("ConstantFillPercent");
        pInternalResourceCosts = serializedObject.FindProperty("InternalResourceCosts");

        pRadiusCoefficient = serializedObject.FindProperty("RadiusCoefficient");
        pSpecificCoolingPowerBase = serializedObject.FindProperty("SpecificCoolingPowerBase");
        pSpecificEnergyConsumption = serializedObject.FindProperty("SpecificEnergyConsumption");
        pBaseHeating = serializedObject.FindProperty("BaseHeating");
        pHeatCapacityCoeff = serializedObject.FindProperty("HeatCapacityCoeff");
        pCraftTime = serializedObject.FindProperty("CraftCoefficient");

        pFactionShortName = serializedObject.FindProperty("factionShortName");
        pBlueprintId = serializedObject.FindProperty("blueprintId");

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
        EditorGUILayout.PropertyField(pRadiusCoefficient, new GUIContent("Radius Coefficient"));
        EditorGUILayout.PropertyField(pSpecificCoolingPowerBase, new GUIContent("Specific Cooling Power (per dm³, base)"));
        EditorGUILayout.PropertyField(pSpecificEnergyConsumption, new GUIContent("Specific Energy Consumption (per dm³)"));
        EditorGUILayout.PropertyField(pBaseHeating, new GUIContent("Base Heating (°/s)"));
        EditorGUILayout.PropertyField(pHeatCapacityCoeff, new GUIContent("Heat Capacity Coeff"));

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
        EditorGUILayout.LabelField("Specific Cooling Power (with tier)", t.SpecificCoolingPower.ToString("F3"));
        EditorGUILayout.LabelField("Cooling Power", t.CoolingPower.ToString("F3"));
        EditorGUILayout.LabelField("Energy Consumption", t.EnergyConsumption.ToString("F3"));
        EditorGUILayout.LabelField("Cooling Radius (m)", t.CoolingRadius.ToString("F3"));
        EditorGUILayout.LabelField("Max Cooling Difference (°)", t.MaxCoolingDifference.ToString("F1"));
        EditorGUILayout.LabelField("Min Temperature (°)", t.MinTemperature.ToString("F1"));

        serializedObject.ApplyModifiedProperties();
    }
}
#endif