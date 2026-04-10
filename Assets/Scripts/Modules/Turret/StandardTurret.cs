// StandardTurret.cs
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Эталонный модуль Турели. Наследует общие параметры от StandardModuleBase.
/// Содержит коэффициенты ствольной коробки и параметры встроенной станины.
/// </summary>
public class StandardTurret : StandardModuleBase
{
    public const string TYPE_TURRET = "Turret";
    public override string ModuleType => TYPE_TURRET;

    [Header("Receiver Coefficients")]
    [Min(0.001f)] public float DurabilityCoeff = 1f;
    [Min(0.001f)] public float LoadingPowerCoeff = 1f;
    [Min(0.001f)] public float ChamberCapacityCoeff = 1f;
    [Range(0, 9)] public int AmmoTierBonus = 0;
    [Min(0f)] public float ShotResourceUseCoeff = 1f;
    [Min(0f)] public float OperationalUseCoeff = 1f;

    [Header("Mount")]
    [Min(0.001f)] public float MountCoeff = 1f;
    [Min(0f)] public float AimSpeedCoeff = 1f;
    [Min(0f)] public float RecoilCoeff = 1f;
    [Min(0f)] public float RotationSpeedCoeff = 1f;
    [Min(0f)] public float MaxElevationDeg = 45f;
    [Min(0f)] public float MaxDepressionDeg = 10f;
    [Range(1f, 360f)] public float TraverseArcDeg = 360f;
    [Min(0f)] public float EnergyConsumption = 0f;

    [Header("Defaults for Workbench")]
    [Range(1, 10)] public int DefaultCorpusTier = 1;
    [Range(1, 10)] public int DefaultLoadingTier = 1;
    [Range(1, 10)] public int DefaultChamberTier = 1;
    [Range(1, 99)] public int DefaultLoadingPercent = 33;
    [Range(1, 99)] public int DefaultChamberPercent = 33;
    [Range(1, 99)] public int DefaultMotorPercent = 34;
    [Range(1, 99)] public int DefaultGyroPercent = 33;

    [Header("Defaults for Barrel")]
    [Min(1f)] public float DefaultBarrelInnerDiameterMm = 100f;
    [Min(1f)] public float DefaultBarrelOuterDiameterMm = 120f;
    [Min(1f)] public float DefaultBarrelLengthMm = 1000f;

    [Header("Defaults for Cannonball Propellant")]
    [Range(1, 10)] public int DefaultPropellantTier = 1;
    [Min(0.001f)] public float DefaultPropellantMassKg = 0.001f;

    protected override void OnValidate()
    {
        base.OnValidate();

        DurabilityCoeff = Mathf.Max(0.001f, DurabilityCoeff);
        LoadingPowerCoeff = Mathf.Max(0.001f, LoadingPowerCoeff);
        ChamberCapacityCoeff = Mathf.Max(0.001f, ChamberCapacityCoeff);
        AmmoTierBonus = Mathf.Clamp(AmmoTierBonus, 0, 9);
        MountCoeff = Mathf.Max(0.001f, MountCoeff);

        DefaultBarrelOuterDiameterMm = Mathf.Max(
            DefaultBarrelInnerDiameterMm + 1f,
            DefaultBarrelOuterDiameterMm);

        DefaultBarrelLengthMm = Mathf.Max(
            DefaultBarrelInnerDiameterMm,
            DefaultBarrelLengthMm);

        DefaultPropellantMassKg = Mathf.Max(0.001f, DefaultPropellantMassKg);
    }

    protected override void ComputeSpecificOutputs() { }
    protected override void RoundAndStoreSpecificResults() { }
}

#if UNITY_EDITOR
[CustomEditor(typeof(StandardTurret))]
public class StandardTurretEditor : Editor
{
    private StandardTurret t;

    private SerializedProperty pModuleTier;
    private SerializedProperty pVolumeCoeff;
    private SerializedProperty pConstantFill;
    private SerializedProperty pInternalResourceCosts;
    private SerializedProperty pCraftCoefficient;
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

    private SerializedProperty pDurabilityCoeff;
    private SerializedProperty pLoadingPowerCoeff;
    private SerializedProperty pChamberCapacityCoeff;
    private SerializedProperty pAmmoTierBonus;
    private SerializedProperty pShotResourceUseCoeff;
    private SerializedProperty pOperationalUseCoeff;

    private SerializedProperty pMountCoeff;
    private SerializedProperty pAimSpeedCoeff;
    private SerializedProperty pRecoilCoeff;
    private SerializedProperty pRotationSpeedCoeff;
    private SerializedProperty pMaxElevationDeg;
    private SerializedProperty pMaxDepressionDeg;
    private SerializedProperty pTraverseArcDeg;
    private SerializedProperty pEnergyConsumption;

    private SerializedProperty pDefaultCorpusTier;
    private SerializedProperty pDefaultLoadingTier;
    private SerializedProperty pDefaultChamberTier;
    private SerializedProperty pDefaultLoadingPercent;
    private SerializedProperty pDefaultChamberPercent;
    private SerializedProperty pDefaultMotorPercent;
    private SerializedProperty pDefaultGyroPercent;

    private SerializedProperty pDefaultBarrelInnerDiameterMm;
    private SerializedProperty pDefaultBarrelOuterDiameterMm;
    private SerializedProperty pDefaultBarrelLengthMm;

    private SerializedProperty pDefaultPropellantTier;
    private SerializedProperty pDefaultPropellantMassKg;

    private string[] factionDisplayNames;
    private string[] factionShortNames;

    private void OnEnable()
    {
        t = target as StandardTurret;
        if (t == null) return;

        pModuleTier = serializedObject.FindProperty("ModuleTier");
        pVolumeCoeff = serializedObject.FindProperty("VolumeCoefficientPercent");
        pConstantFill = serializedObject.FindProperty("ConstantFillPercent");
        pInternalResourceCosts = serializedObject.FindProperty("InternalResourceCosts");
        pCraftCoefficient = serializedObject.FindProperty("CraftCoefficient");
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

        pDurabilityCoeff = serializedObject.FindProperty("DurabilityCoeff");
        pLoadingPowerCoeff = serializedObject.FindProperty("LoadingPowerCoeff");
        pChamberCapacityCoeff = serializedObject.FindProperty("ChamberCapacityCoeff");
        pAmmoTierBonus = serializedObject.FindProperty("AmmoTierBonus");
        pShotResourceUseCoeff = serializedObject.FindProperty("ShotResourceUseCoeff");
        pOperationalUseCoeff = serializedObject.FindProperty("OperationalUseCoeff");

        pMountCoeff = serializedObject.FindProperty("MountCoeff");
        pAimSpeedCoeff = serializedObject.FindProperty("AimSpeedCoeff");
        pRecoilCoeff = serializedObject.FindProperty("RecoilCoeff");
        pRotationSpeedCoeff = serializedObject.FindProperty("RotationSpeedCoeff");
        pMaxElevationDeg = serializedObject.FindProperty("MaxElevationDeg");
        pMaxDepressionDeg = serializedObject.FindProperty("MaxDepressionDeg");
        pTraverseArcDeg = serializedObject.FindProperty("TraverseArcDeg");
        pEnergyConsumption = serializedObject.FindProperty("EnergyConsumption");

        pDefaultCorpusTier = serializedObject.FindProperty("DefaultCorpusTier");
        pDefaultLoadingTier = serializedObject.FindProperty("DefaultLoadingTier");
        pDefaultChamberTier = serializedObject.FindProperty("DefaultChamberTier");
        pDefaultLoadingPercent = serializedObject.FindProperty("DefaultLoadingPercent");
        pDefaultChamberPercent = serializedObject.FindProperty("DefaultChamberPercent");
        pDefaultMotorPercent = serializedObject.FindProperty("DefaultMotorPercent");
        pDefaultGyroPercent = serializedObject.FindProperty("DefaultGyroPercent");

        pDefaultBarrelInnerDiameterMm = serializedObject.FindProperty("DefaultBarrelInnerDiameterMm");
        pDefaultBarrelOuterDiameterMm = serializedObject.FindProperty("DefaultBarrelOuterDiameterMm");
        pDefaultBarrelLengthMm = serializedObject.FindProperty("DefaultBarrelLengthMm");

        pDefaultPropellantTier = serializedObject.FindProperty("DefaultPropellantTier");
        pDefaultPropellantMassKg = serializedObject.FindProperty("DefaultPropellantMassKg");

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

        // IDENTITY
        EditorGUILayout.LabelField("Identity", EditorStyles.boldLabel);
        GUI.enabled = false;
        EditorGUILayout.TextField("Module Type", t.ModuleType);
        GUI.enabled = true;
        EditorGUILayout.PropertyField(pModuleTier);

        // FACTION & BLUEPRINT
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Faction & Blueprint", EditorStyles.boldLabel);

        if (factionShortNames != null && factionShortNames.Length > 1)
        {
            string current = pFactionShortName.stringValue ?? "";
            int selectedIndex = 0;
            for (int i = 0; i < factionShortNames.Length; i++)
                if (factionShortNames[i] == current) { selectedIndex = i; break; }

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

        // VOLUME / FILL / RECIPE
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Volume / Fill / Recipe", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(pVolumeCoeff, new GUIContent("Volume Coeff %"));
        EditorGUILayout.PropertyField(pConstantFill, new GUIContent("Constant Fill %"));
        EditorGUILayout.PropertyField(pInternalResourceCosts, new GUIContent("Resources per Liter"), true);

        // BUILD VISUAL
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Build Visual", EditorStyles.boldLabel);
        if (pBuildVisualYawOffset != null) EditorGUILayout.PropertyField(pBuildVisualYawOffset, new GUIContent("Build Visual Yaw Offset"));
        if (pBuildAnchorLocal != null) EditorGUILayout.PropertyField(pBuildAnchorLocal, new GUIContent("Build Anchor Local"));
        if (pUseBuildAnchorPlacement != null) EditorGUILayout.PropertyField(pUseBuildAnchorPlacement, new GUIContent("Use Build Anchor Placement"));
        if (pBuildAnchorCellLocal != null) EditorGUILayout.PropertyField(pBuildAnchorCellLocal, new GUIContent("Build Anchor Cell Local"));

        // RECEIVER COEFFICIENTS
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Receiver Coefficients", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(pDurabilityCoeff, new GUIContent("Durability Coeff"));
        EditorGUILayout.PropertyField(pLoadingPowerCoeff, new GUIContent("Loading Power Coeff"));
        EditorGUILayout.PropertyField(pChamberCapacityCoeff, new GUIContent("Chamber Capacity Coeff"));
        EditorGUILayout.PropertyField(pAmmoTierBonus, new GUIContent("Ammo Tier Bonus (0..9)"));
        EditorGUILayout.PropertyField(pShotResourceUseCoeff, new GUIContent("Shot Resource Use Coeff"));
        EditorGUILayout.PropertyField(pOperationalUseCoeff, new GUIContent("Operational Use Coeff"));

        // MOUNT
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Mount (Built-in)", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(pMountCoeff, new GUIContent("Mount Coeff"));
        EditorGUILayout.PropertyField(pAimSpeedCoeff, new GUIContent("Aim Speed Coeff"));
        EditorGUILayout.PropertyField(pRecoilCoeff, new GUIContent("Recoil Coeff"));
        EditorGUILayout.PropertyField(pRotationSpeedCoeff, new GUIContent("Rotation Speed Coeff"));
        EditorGUILayout.PropertyField(pMaxElevationDeg, new GUIContent("Max Elevation (°)"));
        EditorGUILayout.PropertyField(pMaxDepressionDeg, new GUIContent("Max Depression (°)"));
        EditorGUILayout.PropertyField(pTraverseArcDeg, new GUIContent("Traverse Arc (°)"));
        EditorGUILayout.PropertyField(pEnergyConsumption, new GUIContent("Energy Consumption (E/s)"));

        // DEFAULTS
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Workbench Defaults — Receiver", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(pDefaultCorpusTier, new GUIContent("Default Corpus Tier"));
        EditorGUILayout.PropertyField(pDefaultLoadingTier, new GUIContent("Default Loading Tier"));
        EditorGUILayout.PropertyField(pDefaultChamberTier, new GUIContent("Default Chamber Tier"));
        EditorGUILayout.PropertyField(pDefaultLoadingPercent, new GUIContent("Default Loading %"));
        EditorGUILayout.PropertyField(pDefaultChamberPercent, new GUIContent("Default Chamber %"));

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Workbench Defaults — Mount", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(pDefaultMotorPercent, new GUIContent("Default Motor %"));
        EditorGUILayout.PropertyField(pDefaultGyroPercent, new GUIContent("Default Gyro %"));

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Workbench Defaults — Barrel", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(pDefaultBarrelInnerDiameterMm, new GUIContent("Default Inner Diameter (mm)"));
        EditorGUILayout.PropertyField(pDefaultBarrelOuterDiameterMm, new GUIContent("Default Outer Diameter (mm)"));
        EditorGUILayout.PropertyField(pDefaultBarrelLengthMm, new GUIContent("Default Length (mm)"));

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Workbench Defaults — Cannonball Propellant", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(pDefaultPropellantTier, new GUIContent("Default Propellant Tier"));
        EditorGUILayout.PropertyField(pDefaultPropellantMassKg, new GUIContent("Default Propellant Mass (kg)"));

        // CRAFTING
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Crafting", EditorStyles.boldLabel);
        if (pCraftCoefficient != null)
            EditorGUILayout.PropertyField(pCraftCoefficient, new GUIContent("Craft Coefficient"));

        // MODULE CAPABILITIES
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Module Capabilities", EditorStyles.boldLabel);
        var pTurn = serializedObject.FindProperty("CanTurnOnOff");
        var pTurnT = serializedObject.FindProperty("TurnOnOffTime");
        var pPulse = serializedObject.FindProperty("CanPulseMode");
        var pPulseI = serializedObject.FindProperty("PulseInterval");
        var pControl = serializedObject.FindProperty("IsControllable");
        if (pTurn != null) EditorGUILayout.PropertyField(pTurn, new GUIContent("Can Turn On/Off"));
        if (pTurnT != null) EditorGUILayout.PropertyField(pTurnT, new GUIContent("Turn On/Off Time"));
        if (pPulse != null) EditorGUILayout.PropertyField(pPulse, new GUIContent("Can Pulse Mode"));
        if (pPulseI != null) EditorGUILayout.PropertyField(pPulseI, new GUIContent("Pulse Interval"));
        if (pControl != null) EditorGUILayout.PropertyField(pControl, new GUIContent("Is Controllable"));

        // DESTRUCTION
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Destruction", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(pIsVolatile, new GUIContent("Is Volatile"));
        if (pIsVolatile != null && pIsVolatile.boolValue)
        {
            EditorGUILayout.PropertyField(pExplosionDamageType, new GUIContent("Explosion Damage Type"));
            EditorGUILayout.PropertyField(pExplosionRadiusCoeff, new GUIContent("Radius Coefficient"));
            EditorGUILayout.PropertyField(pExplosionPenetrationCoeff, new GUIContent("Penetration Coefficient"));
            EditorGUILayout.PropertyField(pExplosionDamageCoeff, new GUIContent("Damage Coefficient"));
        }

        // COMPUTED GEOMETRY
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

        serializedObject.ApplyModifiedProperties();
    }
}
#endif