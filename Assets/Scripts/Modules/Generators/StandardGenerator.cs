using UnityEngine;
using System;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Эталонный модуль Генератора. Наследует общие параметры от StandardModuleBase.
/// </summary>
public class StandardGenerator : StandardModuleBase
{
    public const string TYPE_GENERATOR = "Generator";
    public override string ModuleType => TYPE_GENERATOR;

    [Header("Generator")]
    [Min(0f)] public float PowerBy0001m3 = 1f;
    [Range(1, 10)] public int FuelTier = 1;
    [Min(0f)] public float FuelBy0001m3_Base = 0.0001f;

    [Header("Thermal Physics")]
    [Min(0f)] public float BaseHeating = 10f;
    [Min(0.001f)] public float HeatCapacityCoeff = 1000f;

    [SerializeField, HideInInspector] private float powerTimesTierPer0001;
    [SerializeField, HideInInspector] private float fuelPer0001m3Tiered;
    [SerializeField, HideInInspector] private float specificPower;
    [SerializeField, HideInInspector] private float fuelKgPerS;

    [Header("Capacity")]
    [Min(0f)] public float CapacityCoefficient = 1f; // Коэф. ёмкости генератора

    public float PowerBy0_001m3 => PowerBy0001m3;
    public float PowerTimesTierBy0_001m3 => powerTimesTierPer0001;
    public float FuelBy0_001m3_Tier => fuelPer0001m3Tiered;
    public float SpecificPower => specificPower;
    public float FuelKgPerS => fuelKgPerS;

    const double MIN_FUEL_PER0001_D = 1e-6;
    const float MIN_FUEL_DISPLAY_TOTAL = 0.0001f;

    protected override void OnValidate()
    {
        base.OnValidate(); // Обязательно вызываем базу для проверки общих полей
        FuelTier = Mathf.Clamp(FuelTier, 1, 10);
        PowerBy0001m3 = Mathf.Max(0f, PowerBy0001m3);
        FuelBy0001m3_Base = Mathf.Max(0f, FuelBy0001m3_Base);
        BaseHeating = Mathf.Max(0f, BaseHeating);
        HeatCapacityCoeff = Mathf.Max(0.001f, HeatCapacityCoeff);
        CapacityCoefficient = Mathf.Max(0f, CapacityCoefficient);
        RecalculateAll();
    }

    protected override void ComputeSpecificOutputs()
    {
        float unitsPer0001 = effectiveVolume * 1000f;
        float moduleCoeff = TierCoeffs.Get(ModuleTier);

        double rawPowerD = (double)PowerBy0001m3 * (double)unitsPer0001 * (double)moduleCoeff;
        specificPower = (float)rawPowerD;

        float fuelTierCoeff = TierCoeffs.Get(FuelTier);
        double rawFuelPer0001D = (fuelTierCoeff > 0f) ? (double)FuelBy0001m3_Base / (double)fuelTierCoeff : 0.0;
        if (rawFuelPer0001D <= 0.0) rawFuelPer0001D = MIN_FUEL_PER0001_D;
        fuelPer0001m3Tiered = (float)rawFuelPer0001D;

        // ИСПРАВЛЕНИЕ: Вернул переменную powerTierCoeff
        float powerTierCoeff = TierCoeffs.Get(ModuleTier);
        powerTimesTierPer0001 = (float)((double)PowerBy0001m3 * (double)powerTierCoeff);

        double totalFuelD = rawFuelPer0001D * (double)effectiveVolume * 1000.0;
        fuelKgPerS = (float)Mathf.Max(0f, (float)totalFuelD);
    }

    protected override void RoundAndStoreSpecificResults()
    {
        specificPower = RoundToWithEps(specificPower, 3);

        double perD = Math.Max((double)fuelPer0001m3Tiered, MIN_FUEL_PER0001_D);
        perD = Math.Round(perD * 1_000_000.0) / 1_000_000.0;
        fuelPer0001m3Tiered = (float)perD;

        powerTimesTierPer0001 = RoundToWithEps(powerTimesTierPer0001, 3);

        double totalD = perD * (double)effectiveVolume * 1000.0;
        totalD = Math.Round(totalD * 10000.0) / 10000.0;
        if (totalD < MIN_FUEL_DISPLAY_TOTAL) totalD = MIN_FUEL_DISPLAY_TOTAL;
        fuelKgPerS = (float)totalD;
    }
}

#if UNITY_EDITOR
[CustomEditor(typeof(StandardGenerator))]
public class StandardGeneratorEditor : Editor
{
    SerializedProperty pModuleTier, pVolumeCoeff, pInternalResourceCosts;
    SerializedProperty pPowerBy0001m3, pFuelTier, pFuelBy0001m3_Base;
    SerializedProperty pFactionShortName, pBlueprintId, pBaseHeating, pHeatCapacityCoeff, pCraftTime;
    
    // Новые свойства волатильности
    SerializedProperty pIsVolatile, pExplosionDamageType;
    SerializedProperty pExplosionRadiusCoeff, pExplosionPenetrationCoeff, pExplosionDamageCoeff;

    StandardGenerator t;
    private string[] factionDisplayNames;
    private string[] factionShortNames;

   void OnEnable()
    {
        t = target as StandardGenerator;
        if (t == null || serializedObject == null) return;

        pModuleTier = serializedObject.FindProperty("ModuleTier");
        pVolumeCoeff = serializedObject.FindProperty("VolumeCoefficientPercent");
        pInternalResourceCosts = serializedObject.FindProperty("InternalResourceCosts");
        
        pPowerBy0001m3 = serializedObject.FindProperty("PowerBy0001m3");
        pFuelTier = serializedObject.FindProperty("FuelTier");
        pFuelBy0001m3_Base = serializedObject.FindProperty("FuelBy0001m3_Base");
        pFactionShortName = serializedObject.FindProperty("factionShortName");
        pBlueprintId = serializedObject.FindProperty("blueprintId");
        pBaseHeating = serializedObject.FindProperty("BaseHeating");
        pHeatCapacityCoeff = serializedObject.FindProperty("HeatCapacityCoeff");
        pCraftTime = serializedObject.FindProperty("CraftCoefficient"); 

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
        if (t == null) return;
        serializedObject.Update();

        EditorGUILayout.LabelField("Identity", EditorStyles.boldLabel);
        GUI.enabled = false;
        EditorGUILayout.TextField("Module Type", t.ModuleType);
        GUI.enabled = true;
        EditorGUILayout.PropertyField(pModuleTier);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Faction & Blueprint", EditorStyles.boldLabel);

        if (factionDisplayNames != null && factionShortNames != null && factionShortNames.Length > 1)
        {
            string current = pFactionShortName.stringValue ?? "";
            int selectedIndex = 0;
            for (int i = 0; i < factionShortNames.Length; i++)
            {
                if (factionShortNames[i] == current) { selectedIndex = i; break; }
            }

            EditorGUI.BeginChangeCheck();
            int newIndex = EditorGUILayout.Popup("Faction", selectedIndex, factionDisplayNames);
            if (EditorGUI.EndChangeCheck()) pFactionShortName.stringValue = factionShortNames[newIndex];
        }
        else
        {
            EditorGUILayout.PropertyField(pFactionShortName, new GUIContent("Faction (short name)"));
        }

        EditorGUILayout.PropertyField(pBlueprintId, new GUIContent("Blueprint ID"));

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Volume & Recipe", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(pVolumeCoeff, new GUIContent("Volume Coeff %"));
        EditorGUILayout.PropertyField(pInternalResourceCosts, new GUIContent("Resources per Liter (1 dm3)"), true);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Computed Base", EditorStyles.boldLabel);
        EditorGUILayout.LabelField("Length (X, m)", t.LengthMeters.ToString("0.###"));
        EditorGUILayout.LabelField("Width  (Z, m)", t.WidthMeters.ToString("0.###"));
        EditorGUILayout.LabelField("Height (Y, m)", t.HeightMeters.ToString("0.###"));
        
        // ИСПРАВЛЕНИЕ: Вернул поля AABB Volume и Real Volume
        EditorGUILayout.LabelField("AABB Volume (m³)", t.AABBVolumeM3.ToString("F6"));
        EditorGUILayout.LabelField("Real Volume (m³)", t.RealVolumeM3.ToString("F6"));
        
        EditorGUILayout.LabelField("Effective Volume (m³)", t.EffectiveVolumeM3.ToString("F6"));
        EditorGUILayout.LabelField("Base Inner Mass (kg)", t.MassKg.ToString("0.###"));

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Generator (inputs)", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(pPowerBy0001m3, new GUIContent("Power by 0.001 m³ (energy/s)"));
        EditorGUILayout.PropertyField(pFuelTier);
        EditorGUILayout.PropertyField(pFuelBy0001m3_Base, new GUIContent("Fuel by 0.001 m³ (kg/s)"));
        
        var pCap = serializedObject.FindProperty("CapacityCoefficient");
        if (pCap != null) EditorGUILayout.PropertyField(pCap, new GUIContent("Capacity Coefficient"));

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Thermal Physics", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(pBaseHeating, new GUIContent("Base Heating (°/s)"));
        EditorGUILayout.PropertyField(pHeatCapacityCoeff, new GUIContent("Heat Capacity Coeff"));

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Crafting", EditorStyles.boldLabel);
        if (pCraftTime != null) EditorGUILayout.PropertyField(pCraftTime, new GUIContent("Craft Coefficient"));

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

        // ВОЛАТИЛЬНОСТЬ
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

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Outputs Specific", EditorStyles.boldLabel);
        EditorGUILayout.LabelField("Power*Tier by 0.001 m³ (energy/s)", t.PowerTimesTierBy0_001m3.ToString("0.###"));
        EditorGUILayout.LabelField("Fuel*Tier by 0.001 m³ (kg/s)", t.FuelBy0_001m3_Tier.ToString("0.######"));
        EditorGUILayout.LabelField("Power (energy/s)", t.SpecificPower.ToString("F3"));
        EditorGUILayout.LabelField("Fuel (kg/s)", t.FuelKgPerS.ToString("F4"));

        serializedObject.ApplyModifiedProperties();
    }
}
#endif