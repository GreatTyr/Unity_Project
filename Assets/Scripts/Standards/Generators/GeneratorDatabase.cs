using UnityEngine;
using System.Collections.Generic;

#if UNITY_EDITOR
using UnityEditor;
#endif

[CreateAssetMenu(fileName = "GeneratorDatabase", menuName = "Game/Generator Database")]
public class GeneratorDatabase : ScriptableObject
{
    // ====================== Singleton ======================
    private static GeneratorDatabase _instance;

    public static GeneratorDatabase Instance
    {
        get
        {
            if (_instance == null)
                _instance = Resources.Load<GeneratorDatabase>("GeneratorDatabase");
            return _instance;
        }
    }

    // ====================== Data ======================

    [Tooltip("Drag generator prefabs here. Each must have StandardGenerator component.")]
    public List<GameObject> generators = new();

    // ====================== Access methods ======================

    /// <summary>
    /// Количество генераторов в базе.
    /// </summary>
    public int Count => generators.Count;

    /// <summary>
    /// Получить компонент StandardGenerator по индексу.
    /// </summary>
    public StandardGenerator GetByIndex(int index)
    {
        if (index < 0 || index >= generators.Count) return null;
        var go = generators[index];
        return go != null ? go.GetComponent<StandardGenerator>() : null;
    }

    /// <summary>
    /// Получить первый генератор по имени префаба (GameObject.name).
    /// </summary>
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

    /// <summary>
    /// Получить первый генератор по фракции и тиру модуля.
    /// </summary>
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

    /// <summary>
    /// Получить все генераторы определённой фракции.
    /// </summary>
    public List<StandardGenerator> GetAllByFaction(string factionShortName)
    {
        var result = new List<StandardGenerator>();
        foreach (var go in generators)
        {
            if (go == null) continue;
            var sg = go.GetComponent<StandardGenerator>();
            if (sg == null) continue;
            if (sg.FactionShortName == factionShortName)
                result.Add(sg);
        }
        return result;
    }

    /// <summary>
    /// Получить все генераторы определённого тира.
    /// </summary>
    public List<StandardGenerator> GetAllByTier(int moduleTier)
    {
        var result = new List<StandardGenerator>();
        foreach (var go in generators)
        {
            if (go == null) continue;
            var sg = go.GetComponent<StandardGenerator>();
            if (sg == null) continue;
            if (sg.ModuleTier == moduleTier)
                result.Add(sg);
        }
        return result;
    }

    /// <summary>
    /// Получить все StandardGenerator из базы (пропускает null и без компонента).
    /// </summary>
    public List<StandardGenerator> GetAll()
    {
        var result = new List<StandardGenerator>();
        foreach (var go in generators)
        {
            if (go == null) continue;
            var sg = go.GetComponent<StandardGenerator>();
            if (sg != null)
                result.Add(sg);
        }
        return result;
    }

    /// <summary>
    /// Проверяет, есть ли генератор с таким именем в базе.
    /// </summary>
    public bool Contains(string prefabName)
    {
        return GetByName(prefabName) != null;
    }

    /// <summary>
    /// Проверяет, есть ли этот префаб в базе.
    /// </summary>
    public bool Contains(GameObject prefab)
    {
        return prefab != null && generators.Contains(prefab);
    }
}


// ======================== CUSTOM EDITOR ========================
#if UNITY_EDITOR
[CustomEditor(typeof(GeneratorDatabase))]
public class GeneratorDatabaseEditor : Editor
{
    private GeneratorDatabase db;

    void OnEnable()
    {
        db = target as GeneratorDatabase;
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        EditorGUILayout.LabelField("Generator Database", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Drag generator prefabs into the list below.\n" +
            "Each prefab must have a StandardGenerator component.",
            MessageType.Info);

        EditorGUILayout.Space();

        // Основной список
        var prop = serializedObject.FindProperty("generators");
        EditorGUILayout.PropertyField(prop, new GUIContent($"Generators ({db.generators.Count})"), true);

        EditorGUILayout.Space();

        // Валидация — показываем проблемы
        bool hasIssues = false;
        for (int i = 0; i < db.generators.Count; i++)
        {
            var go = db.generators[i];
            if (go == null)
            {
                EditorGUILayout.HelpBox($"[{i}] — empty slot (null)", MessageType.Warning);
                hasIssues = true;
                continue;
            }
            var sg = go.GetComponent<StandardGenerator>();
            if (sg == null)
            {
                EditorGUILayout.HelpBox(
                    $"[{i}] \"{go.name}\" — no StandardGenerator component!",
                    MessageType.Error);
                hasIssues = true;
                continue;
            }
        }

        if (!hasIssues && db.generators.Count > 0)
        {
            EditorGUILayout.LabelField("✓ All entries valid", EditorStyles.miniLabel);
        }

        // Сводка по генераторам
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
                EditorGUILayout.LabelField($"  Faction: {faction}    Module Tier: {sg.ModuleTier}    Fuel Tier: {sg.FuelTier}");
                EditorGUILayout.LabelField($"  Power: {sg.SpecificPower:F3} energy/s    Fuel: {sg.FuelKgPerS:F4} kg/s    Mass: {sg.MassKg:F3} kg");
                EditorGUILayout.LabelField($"  Eff. Volume: {sg.EffectiveVolumeM3:F3} m³    Fill: {sg.FillPercentUsed:F1}%");
                EditorGUILayout.EndVertical();
            }
        }

        // Кнопка очистки пустых слотов
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