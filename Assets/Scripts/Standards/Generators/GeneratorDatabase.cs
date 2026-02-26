using UnityEngine;
using System.Collections.Generic;
#if UNITY_EDITOR
using UnityEditor;
#endif

[CreateAssetMenu(fileName = "GeneratorDatabase", menuName = "Game/Generator Database")]
public class GeneratorDatabase : ScriptableObject
{
    private static GeneratorDatabase _instance;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatic()
    {
        _instance = null;
    }

    public static GeneratorDatabase Instance
    {
        get
        {
            if (_instance == null)
                _instance = Resources.Load<GeneratorDatabase>("GeneratorDatabase");
            return _instance;
        }
    }

    private void OnEnable()
    {
        _instance = this;
    }

    private void OnDisable()
    {
        if (_instance == this)
            _instance = null;
    }

    [Tooltip("Drag generator prefabs here. Each must have StandardGenerator component.")]
    public List<GameObject> generators = new();

    public int Count => generators.Count;

    // ====================== Access ======================
    public StandardGenerator GetByIndex(int index)
    {
        if (index < 0 || index >= generators.Count) return null;
        var go = generators[index];
        return go != null ? go.GetComponent<StandardGenerator>() : null;
    }

    public StandardGenerator GetByName(string prefabName)
    {
        if (string.IsNullOrEmpty(prefabName)) return null;
        foreach (var go in generators)
        {
            if (go != null && go.name == prefabName)
                return go.GetComponent<StandardGenerator>();
        }
        return null;
    }

    public StandardGenerator GetByFactionAndTier(string factionShortName, int moduleTier)
    {
        foreach (var go in generators)
        {
            if (go == null) continue;
            var sg = go.GetComponent<StandardGenerator>();
            if (sg == null) continue;
            if (sg.FactionShortName == factionShortName && sg.ModuleTier == moduleTier)
                return sg;
        }
        return null;
    }

    public List<StandardGenerator> GetAllByFaction(string factionShortName)
    {
        var result = new List<StandardGenerator>();
        foreach (var go in generators)
        {
            if (go == null) continue;
            var sg = go.GetComponent<StandardGenerator>();
            if (sg != null && sg.FactionShortName == factionShortName)
                result.Add(sg);
        }
        return result;
    }

    public List<StandardGenerator> GetAllByTier(int moduleTier)
    {
        var result = new List<StandardGenerator>();
        foreach (var go in generators)
        {
            if (go == null) continue;
            var sg = go.GetComponent<StandardGenerator>();
            if (sg != null && sg.ModuleTier == moduleTier)
                result.Add(sg);
        }
        return result;
    }

    public List<StandardGenerator> GetAll()
    {
        var result = new List<StandardGenerator>();
        foreach (var go in generators)
        {
            if (go == null) continue;
            var sg = go.GetComponent<StandardGenerator>();
            if (sg != null) result.Add(sg);
        }
        return result;
    }

    public bool Contains(string prefabName) => GetByName(prefabName) != null;
    public bool Contains(GameObject prefab) => prefab != null && generators.Contains(prefab);
}

#if UNITY_EDITOR
[CustomEditor(typeof(GeneratorDatabase))]
public class GeneratorDatabaseEditor : Editor
{
    private GeneratorDatabase db;
    void OnEnable() { db = target as GeneratorDatabase; }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        EditorGUILayout.LabelField("Generator Database", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Drag generator prefabs into the list.\nEach must have StandardGenerator component.",
            MessageType.Info);

        EditorGUILayout.Space();

        var prop = serializedObject.FindProperty("generators");
        EditorGUILayout.PropertyField(prop, new GUIContent($"Generators ({db.generators.Count})"), true);

        EditorGUILayout.Space();

        bool hasIssues = false;
        for (int i = 0; i < db.generators.Count; i++)
        {
            var go = db.generators[i];
            if (go == null)
            {
                EditorGUILayout.HelpBox($"[{i}] — empty slot", MessageType.Warning);
                hasIssues = true;
                continue;
            }
            if (go.GetComponent<StandardGenerator>() == null)
            {
                EditorGUILayout.HelpBox($"[{i}] \"{go.name}\" — no StandardGenerator!", MessageType.Error);
                hasIssues = true;
            }
        }

        if (!hasIssues && db.generators.Count > 0)
            EditorGUILayout.LabelField("✓ All entries valid", EditorStyles.miniLabel);

        if (db.generators.Count > 0)
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Summary", EditorStyles.boldLabel);
            for (int i = 0; i < db.generators.Count; i++)
            {
                var go = db.generators[i];
                if (go == null) continue;
                var sg = go.GetComponent<StandardGenerator>();
                if (sg == null) continue;
                string faction = string.IsNullOrEmpty(sg.FactionShortName) ? "—" : sg.FactionShortName;
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.LabelField($"[{i}] {go.name}", EditorStyles.boldLabel);
                EditorGUILayout.LabelField($"  Faction: {faction}  Module Tier: {sg.ModuleTier}  Fuel Tier: {sg.FuelTier}");
                EditorGUILayout.LabelField($"  Power: {sg.SpecificPower:F3} energy/s  Fuel: {sg.FuelKgPerS:F4} kg/s  Mass: {sg.MassKg:F3} kg");
                EditorGUILayout.LabelField($"  Eff. Volume: {sg.EffectiveVolumeM3:F6} m³  Fill: {sg.FillPercentUsed:F1}%");
                EditorGUILayout.EndVertical();
            }
        }

        EditorGUILayout.Space();
        if (GUILayout.Button("Remove Empty Slots"))
        {
            Undo.RecordObject(db, "Remove Empty Slots");
            db.generators.RemoveAll(g => g == null);
            EditorUtility.SetDirty(db);
        }

        serializedObject.ApplyModifiedProperties();
    }
}
#endif