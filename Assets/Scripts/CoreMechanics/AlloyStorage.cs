using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

/// <summary>
/// Хранилище сплавов. Код сплава → масса в кг (double, точность 0.001).
/// Данные сохраняются в Assets/SaveData/alloy_storage.json.
/// </summary>
[Serializable]
public struct AlloyEntry
{
    public string code;
    public double massKg;
}

public class AlloyStorage : MonoBehaviour
{
    [SerializeField]
    private List<AlloyEntry> entries = new List<AlloyEntry>();

    [Header("Persistence")]
    [Tooltip("Auto-save after every change.")]
    [SerializeField] private bool autoSave = true;

    private Dictionary<string, int> indexByCode =
        new Dictionary<string, int>(StringComparer.Ordinal);

    private const string SAVE_FILE = "alloy_storage.json";
    private const string SAVE_FOLDER = "Assets/SaveData";

    // ====================== Serialization DTO ======================

    [Serializable]
    private struct SaveData
    {
        public int version;
        public List<AlloyEntry> entries;
    }

    // ====================== Lifecycle ======================

    private void Awake()
    {
        Load();
    }

    private void OnValidate()
    {
        RebuildIndex();
    }

    private void OnApplicationQuit()
    {
        Save();
    }

    private void OnApplicationPause(bool pause)
    {
        if (pause) Save();
    }

    // ====================== Index ======================

    private void RebuildIndex()
    {
        indexByCode.Clear();
        for (int i = 0; i < entries.Count; i++)
        {
            if (string.IsNullOrEmpty(entries[i].code)) continue;
            indexByCode[entries[i].code] = i;
        }
    }

    // ====================== Public API ======================

    public void AddAlloy(string code, double massKg)
    {
        if (string.IsNullOrEmpty(code) || massKg <= 0.0) return;

        if (indexByCode.TryGetValue(code, out int idx))
        {
            var e = entries[idx];
            e.massKg = Math.Round(e.massKg + massKg, 3);
            entries[idx] = e;
        }
        else
        {
            indexByCode[code] = entries.Count;
            entries.Add(new AlloyEntry { code = code, massKg = Math.Round(massKg, 3) });
        }

        if (autoSave) Save();
    }

    public double GetMass(string code)
    {
        if (string.IsNullOrEmpty(code)) return 0.0;
        if (indexByCode.TryGetValue(code, out int idx))
            return entries[idx].massKg;
        return 0.0;
    }

    public bool TryConsumeMass(string code, double massKg)
    {
        if (string.IsNullOrEmpty(code) || massKg <= 0.0) return false;
        if (!indexByCode.TryGetValue(code, out int idx)) return false;

        var e = entries[idx];
        if (e.massKg < massKg - 0.0001) return false;

        e.massKg -= massKg;
        e.massKg = Math.Round(e.massKg, 3);

        if (e.massKg <= 0.001)
        {
            entries.RemoveAt(idx);
            RebuildIndex();
        }
        else
        {
            entries[idx] = e;
        }

        if (autoSave) Save();
        return true;
    }

    public bool HasEnoughMass(string code, double massKg)
    {
        if (string.IsNullOrEmpty(code) || massKg <= 0.0) return true;
        return GetMass(code) >= massKg - 0.0001;
    }

    public bool RemoveAlloy(string code)
    {
        if (string.IsNullOrEmpty(code)) return false;
        if (!indexByCode.TryGetValue(code, out int idx)) return false;
        entries.RemoveAt(idx);
        RebuildIndex();
        if (autoSave) Save();
        return true;
    }

    public void ClearAlloys()
    {
        entries.Clear();
        indexByCode.Clear();
        if (autoSave) Save();
    }

    public IReadOnlyList<AlloyEntry> Entries => entries;
    public int Count => entries.Count;

    public AlloyEntry GetEntryByIndex(int index)
    {
        if (index < 0 || index >= entries.Count)
            return default;
        return entries[index];
    }

    public string[] GetAllCodes()
    {
        var result = new string[entries.Count];
        for (int i = 0; i < entries.Count; i++)
            result[i] = entries[i].code ?? "";
        return result;
    }

    public string[] GetDisplayNames()
    {
        var result = new string[entries.Count];
        for (int i = 0; i < entries.Count; i++)
        {
            var e = entries[i];
            result[i] = $"{e.code}  ({e.massKg:F3} kg)";
        }
        return result;
    }

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
                entries = new List<AlloyEntry>(entries)
            };

            string json = JsonUtility.ToJson(data, true);
            string path = GetSavePath();

            if (!Directory.Exists(SAVE_FOLDER))
                Directory.CreateDirectory(SAVE_FOLDER);

            File.WriteAllText(path, json);

#if UNITY_EDITOR
            UnityEditor.AssetDatabase.Refresh();
#endif

            Debug.Log($"[AlloyStorage] Saved {entries.Count} entries to {path}");
        }
        catch (Exception ex)
        {
            Debug.LogError($"[AlloyStorage] Save failed: {ex.Message}");
        }
    }

    public void Load()
    {
        string path = GetSavePath();

        if (!File.Exists(path))
        {
            Debug.Log($"[AlloyStorage] No save file at {path}, using inspector data.");
            RebuildIndex();
            return;
        }

        try
        {
            string json = File.ReadAllText(path);
            var data = JsonUtility.FromJson<SaveData>(json);

            if (data.entries != null)
            {
                entries = data.entries;
                entries.RemoveAll(e => string.IsNullOrEmpty(e.code));
                Debug.Log($"[AlloyStorage] Loaded {entries.Count} entries from {path}");
            }
            else
            {
                Debug.LogWarning("[AlloyStorage] Save file has null entries, using inspector data.");
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"[AlloyStorage] Load failed: {ex.Message}");
        }

        RebuildIndex();
    }

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

            Debug.Log($"[AlloyStorage] Deleted save file: {path}");
        }
    }

    public void ForceSave()
    {
        Save();
    }
}