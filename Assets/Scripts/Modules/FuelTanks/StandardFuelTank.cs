using UnityEngine;
using System;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Эталонный модуль Топливного Бака. Наследует общие параметры от StandardModuleBase.
/// Бак — пассивный модуль, состоящий только из оболочки (стенки) и полости (для топлива).
/// </summary>
public class StandardFuelTank : StandardModuleBase
{
    public const string TYPE_FUELTANK = "FuelTank";
    public override string ModuleType => TYPE_FUELTANK;

    [Header("Fuel Tank")]
    [Min(0f)] public float CapacityCoefficient = 1f;

    [Header("Thermal Physics")]
    [Min(0.001f)] public float HeatCapacityCoeff = 1000f;

    // Расчётные значения
    [SerializeField, HideInInspector] private float capacity;

    public float Capacity => capacity;

    protected override void OnValidate()
    {
        base.OnValidate();
        CapacityCoefficient = Mathf.Max(0f, CapacityCoefficient);
        HeatCapacityCoeff = Mathf.Max(0.001f, HeatCapacityCoeff);
        RecalculateAll();
    }

    protected override void ComputeSpecificOutputs()
    {
        float effectiveVolumeDm3 = effectiveVolume * 1000f;
        float moduleCoeff = TierCoeffs.Get(ModuleTier);
        capacity = effectiveVolumeDm3 * moduleCoeff * CapacityCoefficient;
    }

    protected override void RoundAndStoreSpecificResults()
    {
        capacity = RoundToWithEps(capacity, 3);
    }
}

#if UNITY_EDITOR
[CustomEditor(typeof(StandardFuelTank))]
public class StandardFuelTankEditor : Editor
{
    SerializedProperty pModuleTier, pVolumeCoeff;
    SerializedProperty pCapacityCoefficient;
    SerializedProperty pFactionShortName, pBlueprintId, pHeatCapacityCoeff, pCraftTime;
    
    // Свойства базового класса (управление)
    SerializedProperty pCanTurnOnOff, pTurnOnOffTime, pCanPulseMode, pPulseInterval, pIsControllable;

    // Свойства волатильности
    SerializedProperty pIsVolatile, pExplosionDamageType;
    SerializedProperty pExplosionRadiusCoeff, pExplosionPenetrationCoeff, pExplosionDamageCoeff;

    StandardFuelTank t;
    private string[] factionDisplayNames;
    private string[] factionShortNames;

    void OnEnable()
    {
        t = target as StandardFuelTank;
        if (t == null || serializedObject == null) return;

        pModuleTier = serializedObject.FindProperty("ModuleTier");
        pVolumeCoeff = serializedObject.FindProperty("VolumeCoefficientPercent");
        pCapacityCoefficient = serializedObject.FindProperty("CapacityCoefficient");
        pFactionShortName = serializedObject.FindProperty("factionShortName");
        pBlueprintId = serializedObject.FindProperty("blueprintId");
        pHeatCapacityCoeff = serializedObject.FindProperty("HeatCapacityCoeff");
        pCraftTime = serializedObject.FindProperty("CraftCoefficient");

        // ИСПРАВЛЕНИЕ: Восстановлены ссылки на поля базового класса
        pCanTurnOnOff = serializedObject.FindProperty("CanTurnOnOff");
        pTurnOnOffTime = serializedObject.FindProperty("TurnOnOffTime");
        pCanPulseMode = serializedObject.FindProperty("CanPulseMode");
        pPulseInterval = serializedObject.FindProperty("PulseInterval");
        pIsControllable = serializedObject.FindProperty("IsControllable");

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
    EditorGUILayout.HelpBox("FuelTank не использует FillFactor и InternalResourceCosts. Масса определяется оболочкой и геометрией.", MessageType.Info);

    // ================= SPECIFIC INPUTS =================
    EditorGUILayout.Space();
    EditorGUILayout.LabelField("Specific Inputs", EditorStyles.boldLabel);
    EditorGUILayout.PropertyField(pCapacityCoefficient, new GUIContent("Capacity Coefficient"));
    EditorGUILayout.PropertyField(pHeatCapacityCoeff, new GUIContent("Heat Capacity Coeff"));

    // ================= CRAFTING =================
    EditorGUILayout.Space();
    EditorGUILayout.LabelField("Crafting", EditorStyles.boldLabel);
    if (pCraftTime != null)
        EditorGUILayout.PropertyField(pCraftTime, new GUIContent("Craft Coefficient"));

    // ================= MODULE CAPABILITIES =================
    EditorGUILayout.Space();
    EditorGUILayout.LabelField("Module Capabilities", EditorStyles.boldLabel);
    if (pCanTurnOnOff != null) EditorGUILayout.PropertyField(pCanTurnOnOff, new GUIContent("Can Turn On/Off"));
    if (pTurnOnOffTime != null) EditorGUILayout.PropertyField(pTurnOnOffTime, new GUIContent("Turn On/Off Time"));
    if (pCanPulseMode != null) EditorGUILayout.PropertyField(pCanPulseMode, new GUIContent("Can Pulse Mode"));
    if (pPulseInterval != null) EditorGUILayout.PropertyField(pPulseInterval, new GUIContent("Pulse Interval"));
    if (pIsControllable != null) EditorGUILayout.PropertyField(pIsControllable, new GUIContent("Is Controllable"));

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
    EditorGUILayout.LabelField("Reference Inner Mass (kg)", t.MassKg.ToString("0.###"));

    // ================= COMPUTED SPECIFIC =================
    EditorGUILayout.Space();
    EditorGUILayout.LabelField("Специфичные расчётные параметры", EditorStyles.boldLabel);
    EditorGUILayout.LabelField("Capacity", t.Capacity.ToString("F3"));

    serializedObject.ApplyModifiedProperties();
    }
}
#endif