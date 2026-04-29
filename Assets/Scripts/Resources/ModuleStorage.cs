using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

public class ModuleStorage : MonoBehaviour
{
    public static ModuleStorage Instance { get; private set; } // Синглтон для быстрого доступа

    [Header("Persistence")]
    [SerializeField] private bool autoSave = true;

    [SerializeField, HideInInspector]
    private List<ModuleEntry> entries = new List<ModuleEntry>();

    private const string SAVE_FILE = "module_storage.json";

    private static string GetSaveFolder()
    {
#if UNITY_EDITOR
        return "Assets/SaveData";
#else
        return Application.persistentDataPath;
#endif
    }

    [Serializable]
    public struct ModuleEntry
    {
        public string moduleCode;      // Уникальный 3-строчный код
        public int quantity;           // Количество в стаке
        public string dataTypeName;    // "GeneratorData"
        public string json;            // Сериализованные данные базового модуля
    }

    [Serializable]
    private struct SaveData
    {
        public int version;
        public List<ModuleEntry> entries;
    }

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else if (Instance != this) { Destroy(gameObject); return; }
        Load();
    }

    private void OnApplicationQuit() => Save();
    private void OnApplicationPause(bool pause) { if (pause) Save(); }

    public int Count => entries.Count;

    public string AddModule(ModuleCommonData data)
    {
        if (data == null || string.IsNullOrEmpty(data.moduleCode)) return null;

        string code = data.moduleCode;
        int idx = entries.FindIndex(e => e.moduleCode == code);

        if (idx >= 0)
        {
            var entry = entries[idx];
            entry.quantity++;
            entries[idx] = entry;
        }
        else
        {
            entries.Add(new ModuleEntry
            {
                moduleCode = code,
                quantity = 1,
                dataTypeName = data.GetType().Name,
                json = JsonUtility.ToJson(data, false)
            });
        }

        Debug.Log($"[ModuleStorage] Added/Stacked module: \n{code}");
        if (autoSave) Save();
        return code;
    }

    public bool RemoveModule(string code, int amount = 1)
    {
        if (string.IsNullOrEmpty(code)) return false;
        int idx = entries.FindIndex(e => e.moduleCode == code);
        if (idx < 0) return false;

        var entry = entries[idx];
        if (entry.quantity <= amount)
        {
            entries.RemoveAt(idx);
        }
        else
        {
            entry.quantity -= amount;
            entries[idx] = entry;
        }

        if (autoSave) Save();
        return true;
    }

    public int GetQuantity(string code)
    {
        if (string.IsNullOrEmpty(code)) return 0;
        int idx = entries.FindIndex(e => e.moduleCode == code);
        if (idx < 0) return 0;
        return Mathf.Max(0, entries[idx].quantity);
    }

    public bool HasModule(string code, int amount = 1)
    {
        if (amount <= 0) return true;
        return GetQuantity(code) >= amount;
    }

    public void SetEntryQuantity(int index, int newQuantity)
    {
        if (index < 0 || index >= entries.Count) return;
        var entry = entries[index];
        entry.quantity = Mathf.Max(1, newQuantity);
        entries[index] = entry;
        if (autoSave) Save();
    }

    public ModuleCommonData GetBaseModuleCommonData(string code)
    {
        if (string.IsNullOrEmpty(code)) return null;
        int idx = entries.FindIndex(e => e.moduleCode == code);
        if (idx < 0) return null;
        return CraftedModule.DeserializeData(entries[idx].dataTypeName, entries[idx].json);
    }

    public ModuleEntry GetEntryByIndex(int index)
    {
        if (index < 0 || index >= entries.Count) return default;
        return entries[index];
    }

    public void Clear()
    {
        entries.Clear();
        if (autoSave) Save();
    }

    public void ForceSave() => Save();

    public void Save()
    {
        try
        {
            if (entries == null) return;
            var data = new SaveData { version = 2, entries = new List<ModuleEntry>(entries) };
            string json = JsonUtility.ToJson(data, true);
            string folder = GetSaveFolder();
            if (!Directory.Exists(folder)) Directory.CreateDirectory(folder);

            string path = Path.Combine(folder, SAVE_FILE);
            File.WriteAllText(path, json);
            Debug.Log($"<color=#00FFFF>[ModuleStorage] Saved {entries.Count} entries to {path}</color>");
        }
        catch (Exception ex) { Debug.LogError($"[ModuleStorage] Save failed: {ex.Message}"); }
    }

    public void Load()
    {
        string path = Path.Combine(GetSaveFolder(), SAVE_FILE);
        if (!File.Exists(path))
        {
            Debug.Log("[ModuleStorage] No save file found, starting empty.");
            return;
        }

        try
        {
            string json = File.ReadAllText(path);
            if (string.IsNullOrEmpty(json)) return;

            var data = JsonUtility.FromJson<SaveData>(json);
            if (data.entries != null)

            {
                entries = data.entries;
                entries.RemoveAll(e => string.IsNullOrEmpty(e.moduleCode));
                Debug.Log($"<color=#00FF00>[ModuleStorage] Loaded {entries.Count} entries from {path}</color>");
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"[ModuleStorage] Load failed: {ex.Message}");
            File.Copy(path, path + ".bak", true);
        }
    }
}

#if UNITY_EDITOR
[UnityEditor.CustomEditor(typeof(ModuleStorage))]
public class ModuleStorageEditor : UnityEditor.Editor
{
    public override void OnInspectorGUI()
    {
        var storage = target as ModuleStorage;
        if (storage == null) return;
        serializedObject.Update();

        UnityEditor.EditorGUILayout.LabelField("Module Storage (Stacked)", UnityEditor.EditorStyles.boldLabel);
        UnityEditor.EditorGUILayout.PropertyField(serializedObject.FindProperty("autoSave"));
        UnityEditor.EditorGUILayout.Space();

        for (int i = 0; i < storage.Count; i++)
        {
            var entry = storage.GetEntryByIndex(i);
            var data = CraftedModule.DeserializeData(entry.dataTypeName, entry.json);

            UnityEditor.EditorGUILayout.BeginVertical(UnityEditor.EditorStyles.helpBox);

            if (data != null)
            {
                float totalMass = data.totalMassKg * entry.quantity;
                UnityEditor.EditorGUILayout.LabelField($"{data.moduleType} [Tier {data.moduleTier}]", UnityEditor.EditorStyles.boldLabel);
                UnityEditor.EditorGUILayout.LabelField($"Общая масса стака: {totalMass:F1} kg", UnityEditor.EditorStyles.miniLabel);
            }
            else
            {
                UnityEditor.EditorGUILayout.LabelField("Corrupted Data", UnityEditor.EditorStyles.boldLabel);
            }

            UnityEditor.EditorGUI.BeginChangeCheck();
            int newQty = UnityEditor.EditorGUILayout.IntField("Количество:", entry.quantity);
            if (UnityEditor.EditorGUI.EndChangeCheck())
            {
                UnityEditor.Undo.RecordObject(storage, "Change Module Quantity");
                storage.SetEntryQuantity(i, newQty);
                UnityEditor.EditorUtility.SetDirty(storage);
            }

            UnityEditor.EditorGUILayout.LabelField("Код модуля:");
            UnityEditor.EditorGUILayout.TextArea(entry.moduleCode, GUILayout.Height(45));

            UnityEditor.EditorGUILayout.EndVertical();
            UnityEditor.EditorGUILayout.Space(2);
        }

        UnityEditor.EditorGUILayout.Space();
        if (GUILayout.Button("Force Save")) storage.ForceSave();
        if (GUILayout.Button("Reload")) storage.Load();
        if (GUILayout.Button("Clear All")) { storage.Clear(); }

        serializedObject.ApplyModifiedProperties();
    }
}
#endif