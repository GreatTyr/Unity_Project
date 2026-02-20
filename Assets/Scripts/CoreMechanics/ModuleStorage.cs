using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

/// <summary>
/// Хранилище изготовленных модулей.
/// MonoBehaviour — вешается на любой GameObject, назначается в ModuleWorkbench.
/// Сохраняет/загружает данные в Assets/SaveData/module_storage.json.
/// </summary>
public class ModuleStorage : MonoBehaviour
{
    [Header("Persistence")]
    [Tooltip("Auto-save after every change.")]
    [SerializeField] private bool autoSave = true;

    // ── Внутреннее хранение ──
    // Каждый модуль хранится как пара (typeName, json)
    [SerializeField, HideInInspector]
    private List<ModuleEntry> entries = new List<ModuleEntry>();

    private const string SAVE_FILE = "module_storage.json";
    private const string SAVE_FOLDER = "Assets/SaveData";

    // ── Сериализуемая запись ──
    [Serializable]
    public struct ModuleEntry
    {
        public string id;              // уникальный ID (GUID)
        public string dataTypeName;    // "EnergyStorageData", "GeneratorData", etc.
        public string json;            // сериализованные данные
    }

    [Serializable]
    private struct SaveData
    {
        public int version;
        public List<ModuleEntry> entries;
    }

    // ====================== Lifecycle ======================
    private void Awake()
    {
        Load();
    }

    private void OnApplicationQuit()
    {
        Save();
    }

    private void OnApplicationPause(bool pause)
    {
        if (pause) Save();
    }

    // ====================== Public API ======================

    /// <summary>Количество модулей в хранилище.</summary>
    public int Count => entries.Count;

    /// <summary>
    /// Добавить модуль в хранилище.
    /// Возвращает уникальный ID записи.
    /// </summary>
    public string AddModule(ModuleData data)
    {
        if (data == null) return null;

        string id = Guid.NewGuid().ToString("N");
        string typeName = data.GetType().Name;
        string json = JsonUtility.ToJson(data, false);

        entries.Add(new ModuleEntry
        {
            id = id,
            dataTypeName = typeName,
            json = json
        });

        Debug.Log($"[ModuleStorage] Added module: {data.moduleType} T{data.moduleTier}, ID: {id}");

        if (autoSave) Save();
        return id;
    }

    /// <summary>Удалить модуль по ID.</summary>
    public bool RemoveModule(string id)
    {
        if (string.IsNullOrEmpty(id)) return false;
        int idx = entries.FindIndex(e => e.id == id);
        if (idx < 0) return false;

        entries.RemoveAt(idx);
        Debug.Log($"[ModuleStorage] Removed module ID: {id}");

        if (autoSave) Save();
        return true;
    }

    /// <summary>Получить данные модуля по ID.</summary>
    public ModuleData GetModule(string id)
    {
        if (string.IsNullOrEmpty(id)) return null;
        int idx = entries.FindIndex(e => e.id == id);
        if (idx < 0) return null;

        var entry = entries[idx];
        return CraftedModule.DeserializeData(entry.dataTypeName, entry.json);
    }

    /// <summary>Получить данные модуля как конкретный тип.</summary>
    public T GetModule<T>(string id) where T : ModuleData
    {
        return GetModule(id) as T;
    }

    /// <summary>Получить запись по индексу.</summary>
    public ModuleEntry GetEntryByIndex(int index)
    {
        if (index < 0 || index >= entries.Count) return default;
        return entries[index];
    }

    /// <summary>Получить данные модуля по индексу.</summary>
    public ModuleData GetModuleByIndex(int index)
    {
        if (index < 0 || index >= entries.Count) return null;
        var entry = entries[index];
        return CraftedModule.DeserializeData(entry.dataTypeName, entry.json);
    }

    /// <summary>Получить все ID.</summary>
    public string[] GetAllIds()
    {
        var result = new string[entries.Count];
        for (int i = 0; i < entries.Count; i++)
            result[i] = entries[i].id;
        return result;
    }

    /// <summary>Получить отображаемые имена для UI.</summary>
    public string[] GetDisplayNames()
    {
        var result = new string[entries.Count];
        for (int i = 0; i < entries.Count; i++)
        {
            var entry = entries[i];
            var data = CraftedModule.DeserializeData(entry.dataTypeName, entry.json);
            if (data != null)
                result[i] = $"{data.moduleType} T{data.moduleTier} [{data.referenceName}] {data.totalMassKg:F1}kg";
            else
                result[i] = $"(corrupted) {entry.id}";
        }
        return result;
    }

    /// <summary>Получить все модули определённого типа.</summary>
    public List<ModuleData> GetAllByType(string moduleType)
    {
        var result = new List<ModuleData>();
        foreach (var entry in entries)
        {
            var data = CraftedModule.DeserializeData(entry.dataTypeName, entry.json);
            if (data != null && data.moduleType == moduleType)
                result.Add(data);
        }
        return result;
    }

    /// <summary>Очистить всё хранилище.</summary>
    public void Clear()
    {
        entries.Clear();
        if (autoSave) Save();
    }

    /// <summary>Все записи (readonly).</summary>
    public IReadOnlyList<ModuleEntry> Entries => entries;

    // ====================== Save / Load ======================

    private static string GetSavePath()
    {
        return Path.Combine(SAVE_FOLDER, SAVE_FILE);
    }

    public void Save()
    {
        try
        {
            var data = new SaveData
            {
                version = 1,
                entries = new List<ModuleEntry>(entries)
            };

            string json = JsonUtility.ToJson(data, true);
            string path = GetSavePath();

            if (!Directory.Exists(SAVE_FOLDER))
                Directory.CreateDirectory(SAVE_FOLDER);

            File.WriteAllText(path, json);

#if UNITY_EDITOR
            UnityEditor.AssetDatabase.Refresh();
#endif

            Debug.Log($"[ModuleStorage] Saved {entries.Count} modules to {path}");
        }
        catch (Exception ex)
        {
            Debug.LogError($"[ModuleStorage] Save failed: {ex.Message}");
        }
    }

    public void Load()
    {
        string path = GetSavePath();

        if (!File.Exists(path))
        {
            Debug.Log($"[ModuleStorage] No save file at {path}, starting empty.");
            return;
        }

        try
        {
            string json = File.ReadAllText(path);
            var data = JsonUtility.FromJson<SaveData>(json);

            if (data.entries != null)
            {
                entries = data.entries;
                entries.RemoveAll(e => string.IsNullOrEmpty(e.id) || string.IsNullOrEmpty(e.json));
                Debug.Log($"[ModuleStorage] Loaded {entries.Count} modules from {path}");
            }
            else
            {
                Debug.LogWarning("[ModuleStorage] Save file has null entries.");
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"[ModuleStorage] Load failed: {ex.Message}");
        }
    }

    public void ForceSave() => Save();

    public void DeleteSaveFile()
    {
        string path = GetSavePath();
        if (File.Exists(path))
        {
            File.Delete(path);
            string metaPath = path + ".meta";
            if (File.Exists(metaPath))
                File.Delete(metaPath);

#if UNITY_EDITOR
            UnityEditor.AssetDatabase.Refresh();
#endif
            Debug.Log($"[ModuleStorage] Deleted save file: {path}");
        }
    }
}

// ======================== CUSTOM EDITOR ========================
#if UNITY_EDITOR
[UnityEditor.CustomEditor(typeof(ModuleStorage))]
public class ModuleStorageEditor : UnityEditor.Editor
{
    public override void OnInspectorGUI()
    {
        var storage = target as ModuleStorage;
        if (storage == null) return;

        serializedObject.Update();

        UnityEditor.EditorGUILayout.LabelField("Module Storage", UnityEditor.EditorStyles.boldLabel);
        UnityEditor.EditorGUILayout.HelpBox(
            "Stores crafted modules as JSON.\nAssign this in ModuleWorkbench.",
            UnityEditor.MessageType.Info);

        // Auto-save toggle
        var autoSaveProp = serializedObject.FindProperty("autoSave");
        UnityEditor.EditorGUILayout.PropertyField(autoSaveProp);

        UnityEditor.EditorGUILayout.Space();
        UnityEditor.EditorGUILayout.LabelField($"Modules: {storage.Count}", UnityEditor.EditorStyles.boldLabel);

        // Список модулей
        for (int i = 0; i < storage.Count; i++)
        {
            var entry = storage.GetEntryByIndex(i);
            var data = CraftedModule.DeserializeData(entry.dataTypeName, entry.json);

            if (data == null)
            {
                UnityEditor.EditorGUILayout.HelpBox($"[{i}] Corrupted entry: {entry.id}", UnityEditor.MessageType.Error);
                continue;
            }

            UnityEditor.EditorGUILayout.BeginVertical(UnityEditor.EditorStyles.helpBox);

            string faction = string.IsNullOrEmpty(data.faction) || data.faction == "NONE" ? "—" : data.faction;
            UnityEditor.EditorGUILayout.LabelField($"[{i}] {data.moduleType} T{data.moduleTier}", UnityEditor.EditorStyles.boldLabel);
            UnityEditor.EditorGUILayout.LabelField($"  Ref: {data.referenceName}   Faction: {faction}");
            UnityEditor.EditorGUILayout.LabelField($"  Mass: {data.totalMassKg:F3} kg   Durability: {data.durability:F3}");
            UnityEditor.EditorGUILayout.LabelField($"  Size: {data.length:F3}×{data.width:F3}×{data.height:F3} m");
            UnityEditor.EditorGUILayout.LabelField($"  Alloy: {data.alloyCode}");

            // Специфичные поля
            if (data is EnergyStorageData esd)
                UnityEditor.EditorGUILayout.LabelField($"  Capacity: {esd.energyCapacity:F3}");
            else if (data is GeneratorData gd)
                UnityEditor.EditorGUILayout.LabelField($"  Power: {gd.specificPower:F3}   Fuel: {gd.fuelKgPerS:F4} kg/s   FuelTier: {gd.fuelTier}");

            UnityEditor.EditorGUILayout.LabelField($"  ID: {entry.id}", UnityEditor.EditorStyles.miniLabel);

            UnityEditor.EditorGUILayout.EndVertical();
        }

        // Кнопки
        UnityEditor.EditorGUILayout.Space();
        UnityEditor.EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Force Save"))
            storage.ForceSave();
        if (GUILayout.Button("Reload"))
            storage.Load();
        if (GUILayout.Button("Clear All"))
        {
            if (UnityEditor.EditorUtility.DisplayDialog("Clear Module Storage",
                "Delete all stored modules?", "Yes", "Cancel"))
            {
                storage.Clear();
            }
        }
        UnityEditor.EditorGUILayout.EndHorizontal();

        serializedObject.ApplyModifiedProperties();
    }
}
#endif