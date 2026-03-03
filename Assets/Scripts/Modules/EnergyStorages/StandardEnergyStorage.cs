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
    [Header("Capacity")]
    [Min(0f)] public float CapacityCoefficient = 1f; // Коэф. ёмкости батареи
    public const string TYPE_ENERGY_STORAGE = "EnergyStorage";
    public override string ModuleType => TYPE_ENERGY_STORAGE;

    [SerializeField, HideInInspector] private float energyCapacity;

    public float EnergyCapacity => energyCapacity;

    protected override void OnValidate()
    {
        base.OnValidate(); // Проверка общих полей
        RecalculateAll();
    }

    /// <summary>
    /// Реализация абстрактного метода из базы. Считает только емкость энергии.
    /// </summary>
    protected override void ComputeSpecificOutputs()
    {
        float effVolDm3 = effectiveVolume * 1000f;
        float tierCoeff = TierCoeffs.Get(ModuleTier);
        // Добавлен CapacityCoefficient в формулу емкости!
        energyCapacity = effVolDm3 * tierCoeff * CapacityCoefficient;
    }

    /// <summary>
    /// Округление специфичных результатов.
    /// </summary>
    protected override void RoundAndStoreSpecificResults()
    {
        energyCapacity = RoundToWithEps(energyCapacity, 3);
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

        pModuleTier = serializedObject.FindProperty("ModuleTier");
        pVolumeCoeff = serializedObject.FindProperty("VolumeCoefficientPercent");
        pConstantFill = serializedObject.FindProperty("ConstantFillPercent");
        pFactionShortName = serializedObject.FindProperty("factionShortName");
        pBlueprintId = serializedObject.FindProperty("blueprintId");
        
        // Исправлено здесь
        pCraftTime = serializedObject.FindProperty("CraftCoefficient");

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
        EditorGUILayout.LabelField("Capacity", EditorStyles.boldLabel);
        var pCap = serializedObject.FindProperty("CapacityCoefficient");
        if (pCap != null) EditorGUILayout.PropertyField(pCap, new GUIContent("Capacity Coefficient"));

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Crafting", EditorStyles.boldLabel);
        if (pCraftTime != null) EditorGUILayout.PropertyField(pCraftTime, new GUIContent("Craft Coefficient"));

        // Новые галочки
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

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Computed Base", EditorStyles.boldLabel);
        EditorGUILayout.LabelField("Length (X, m)", t.LengthMeters.ToString("0.###"));
        EditorGUILayout.LabelField("Width  (Z, m)", t.WidthMeters.ToString("0.###"));
        EditorGUILayout.LabelField("Height (Y, m)", t.HeightMeters.ToString("0.###"));
        EditorGUILayout.LabelField("AABB Volume (m³)", t.AABBVolumeM3.ToString("F6"));
        EditorGUILayout.LabelField("Real Volume (m³)", t.RealVolumeM3.ToString("F6"));
        EditorGUILayout.LabelField("Effective Volume (m³)", t.EffectiveVolumeM3.ToString("F6"));
        EditorGUILayout.LabelField("Fill % used", t.FillPercentUsed.ToString("0.###"));
        EditorGUILayout.LabelField("Mass (kg)", t.MassKg.ToString("0.###"));

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Outputs Specific", EditorStyles.boldLabel);
        EditorGUILayout.LabelField("Energy Capacity", t.EnergyCapacity.ToString("F3"));

        serializedObject.ApplyModifiedProperties();
    }
}
#endif