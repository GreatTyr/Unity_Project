using UnityEngine;
using System.Collections.Generic;

#if UNITY_EDITOR
using UnityEditor;
#endif

[System.Serializable]
public class ModuleTypeEntry
{
    [Tooltip("Unique type name, e.g. Generator, FuelTank, EnergyStorage")]
    public string typeName;
}

[CreateAssetMenu(fileName = "ModuleTypesDatabase", menuName = "Game/Module Types Database")]
public class ModuleTypesDatabase : ScriptableObject
{
    // ====================== Константы стандартных типов ======================
    // Используются скриптами модулей для жёсткой привязки.
    // Должны совпадать с записями в списке moduleTypes в ассете.

    public const string TYPE_GENERATOR = "Generator";
    public const string TYPE_FUEL_TANK = "FuelTank";
    public const string TYPE_ENERGY_STORAGE = "EnergyStorage";

    // ====================== Singleton ======================
    private static ModuleTypesDatabase _instance;

    public static ModuleTypesDatabase Instance
    {
        get
        {
            if (_instance == null)
                _instance = Resources.Load<ModuleTypesDatabase>("ModuleTypesDatabase");
            return _instance;
        }
    }

    // ====================== Data ======================

    [Tooltip("List of all module types in the game.")]
    public List<ModuleTypeEntry> moduleTypes = new();

    // ====================== Access methods ======================

    /// <summary>
    /// Количество зарегистрированных типов.
    /// </summary>
    public int Count => moduleTypes.Count;

    /// <summary>
    /// Проверить, существует ли тип с таким именем.
    /// </summary>
    public bool Exists(string typeName)
    {
        if (string.IsNullOrEmpty(typeName)) return false;
        foreach (var entry in moduleTypes)
        {
            if (entry != null && entry.typeName == typeName)
                return true;
        }
        return false;
    }

    /// <summary>
    /// Получить имя типа по индексу.
    /// </summary>
    public string GetByIndex(int index)
    {
        if (index < 0 || index >= moduleTypes.Count) return null;
        return moduleTypes[index]?.typeName;
    }

    /// <summary>
    /// Получить индекс типа по имени. -1 если не найден.
    /// </summary>
    public int IndexOf(string typeName)
    {
        if (string.IsNullOrEmpty(typeName)) return -1;
        for (int i = 0; i < moduleTypes.Count; i++)
        {
            if (moduleTypes[i] != null && moduleTypes[i].typeName == typeName)
                return i;
        }
        return -1;
    }

    /// <summary>
    /// Все имена типов как массив (для Popup и UI).
    /// </summary>
    public string[] GetAllNames()
    {
        var result = new List<string>();
        foreach (var entry in moduleTypes)
        {
            if (entry != null && !string.IsNullOrEmpty(entry.typeName))
                result.Add(entry.typeName);
        }
        return result.ToArray();
    }

    /// <summary>
    /// Все имена типов с вариантом "(None)" первым элементом (для Popup).
    /// </summary>
    public string[] GetDisplayNamesWithNone()
    {
        var names = GetAllNames();
        var result = new string[names.Length + 1];
        result[0] = "(None)";
        for (int i = 0; i < names.Length; i++)
            result[i + 1] = names[i];
        return result;
    }

    /// <summary>
    /// Проверяет, что все константы стандартных типов присутствуют в списке.
    /// Возвращает список отсутствующих.
    /// </summary>
    public List<string> GetMissingStandardTypes()
    {
        var missing = new List<string>();
        string[] standards = { TYPE_GENERATOR, TYPE_FUEL_TANK, TYPE_ENERGY_STORAGE };
        foreach (var st in standards)
        {
            if (!Exists(st))
                missing.Add(st);
        }
        return missing;
    }
}


// ======================== CUSTOM EDITOR ========================
#if UNITY_EDITOR
[CustomEditor(typeof(ModuleTypesDatabase))]
public class ModuleTypesDatabaseEditor : Editor
{
    private ModuleTypesDatabase db;

    void OnEnable()
    {
        db = target as ModuleTypesDatabase;
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        EditorGUILayout.LabelField("Module Types Database", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Add module type names here.\n" +
            "Each entry is a unique type: Generator, FuelTank, EnergyStorage, etc.\n" +
            "Standard types must match constants in code:\n" +
            $"  • {ModuleTypesDatabase.TYPE_GENERATOR}\n" +
            $"  • {ModuleTypesDatabase.TYPE_FUEL_TANK}\n" +
            $"  • {ModuleTypesDatabase.TYPE_ENERGY_STORAGE}",
            MessageType.Info);

        EditorGUILayout.Space();

        // Список
        var prop = serializedObject.FindProperty("moduleTypes");
        EditorGUILayout.PropertyField(prop, new GUIContent($"Module Types ({db.moduleTypes.Count})"), true);

        EditorGUILayout.Space();

        // ---- Проверка стандартных типов ----
        var missing = db.GetMissingStandardTypes();
        if (missing.Count > 0)
        {
            EditorGUILayout.HelpBox(
                "Missing standard types:\n• " + string.Join("\n• ", missing) +
                "\n\nThese are required by module scripts. Add them or click the button below.",
                MessageType.Error);

            if (GUILayout.Button("Add Missing Standard Types"))
            {
                Undo.RecordObject(db, "Add Missing Standard Types");
                foreach (var m in missing)
                {
                    db.moduleTypes.Add(new ModuleTypeEntry { typeName = m });
                }
                EditorUtility.SetDirty(db);
            }

            EditorGUILayout.Space();
        }

        // ---- Валидация ----
        bool hasIssues = false;
        var seen = new HashSet<string>();

        for (int i = 0; i < db.moduleTypes.Count; i++)
        {
            var entry = db.moduleTypes[i];

            if (entry == null || string.IsNullOrEmpty(entry.typeName))
            {
                EditorGUILayout.HelpBox($"[{i}] — empty type name!", MessageType.Warning);
                hasIssues = true;
                continue;
            }

            if (entry.typeName.Contains(" "))
            {
                EditorGUILayout.HelpBox(
                    $"[{i}] \"{entry.typeName}\" — contains spaces. Use PascalCase (e.g. FuelTank).",
                    MessageType.Warning);
                hasIssues = true;
            }

            if (!seen.Add(entry.typeName))
            {
                EditorGUILayout.HelpBox(
                    $"[{i}] \"{entry.typeName}\" — duplicate! Each type must be unique.",
                    MessageType.Error);
                hasIssues = true;
            }
        }

        if (!hasIssues && db.moduleTypes.Count > 0 && missing.Count == 0)
        {
            EditorGUILayout.LabelField("✓ All entries valid", EditorStyles.miniLabel);
        }

        // ---- Сводка ----
        if (db.moduleTypes.Count > 0)
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Registered Types", EditorStyles.boldLabel);

            string allNames = string.Join(", ", db.GetAllNames());
            EditorGUILayout.LabelField(allNames, EditorStyles.wordWrappedLabel);
        }

        // ---- Кнопки ----
        EditorGUILayout.Space();

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Remove Empty Entries"))
        {
            Undo.RecordObject(db, "Remove Empty Module Types");
            db.moduleTypes.RemoveAll(e => e == null || string.IsNullOrEmpty(e.typeName));
            EditorUtility.SetDirty(db);
        }

        if (GUILayout.Button("Remove Duplicates"))
        {
            Undo.RecordObject(db, "Remove Duplicate Module Types");
            var unique = new HashSet<string>();
            db.moduleTypes.RemoveAll(e =>
            {
                if (e == null || string.IsNullOrEmpty(e.typeName)) return true;
                return !unique.Add(e.typeName);
            });
            EditorUtility.SetDirty(db);
        }
        EditorGUILayout.EndHorizontal();

        serializedObject.ApplyModifiedProperties();
    }
}
#endif