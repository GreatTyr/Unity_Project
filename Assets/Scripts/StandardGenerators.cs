using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Standards/Standard Generator", fileName = "StandardGenerator")]
public class StandardGenerator : ScriptableObject
{
    [Header("Identity")]
    [Tooltip("Ключ типа, которым будет пользоваться система крафта (можно менять)")]
    public string TypeKey = "Generator";

    [Header("Entries (drag prefabs/assets here)")]
    [Tooltip("Список эталонных префабов/ассетов для генераторов")]
    public List<GameObject> Entries = new List<GameObject>();

    [Serializable]
    public class EntryData
    {
        public string id; // уникальный id, например имя префаба
        public Vector3 sizeMeters = new Vector3(0.1f, 0.1f, 0.1f);
        public float energyPerTick = 1f;
        public float fuelKgPerTick = 0.001f;
    }

    [Header("Optional: explicit data per entry (index-aligned with Entries list)")]
    public List<EntryData> EntryDatas = new List<EntryData>();

    // runtime cache
    private Dictionary<string, EntryData> _lookup;

    private void BuildLookupIfNeeded()
    {
        if (_lookup != null) return;

        // Инициализируем словарь
        _lookup = new Dictionary<string, EntryData>(StringComparer.OrdinalIgnoreCase);

        int count = Math.Max(Entries.Count, EntryDatas.Count);

        for (int i = 0; i < count; i++)
        {
            string key = (i < Entries.Count && Entries[i] != null) ? Entries[i].name : "entry_" + i;

            if (i < EntryDatas.Count && EntryDatas[i] != null)
            {
                // используем существующую запись из EntryDatas
                var existing = EntryDatas[i];
                if (string.IsNullOrEmpty(existing.id))
                    existing.id = key;

                if (!_lookup.ContainsKey(existing.id))
                    _lookup[existing.id] = existing;
                else
                    Debug.LogWarning($"StandardGenerator ({name}): duplicate id '{existing.id}' at index {i}");
            }
            else
            {
                // генерируем минимальную запись и добавляем в словарь
                var generated = new EntryData { id = key };
                _lookup[generated.id] = generated;
            }
        }
    }

    public bool TryGetEntryData(string id, out EntryData data)
    {
        BuildLookupIfNeeded();
        if (string.IsNullOrEmpty(id))
        {
            data = null;
            return false;
        }

        return _lookup.TryGetValue(id, out data);
    }

    public List<string> GetAllIds()
    {
        BuildLookupIfNeeded();
        return new List<string>(_lookup.Keys);
    }

    public EntryData GetEntryDataByIndex(int index)
    {
        BuildLookupIfNeeded();
        if (index < 0 || index >= _lookup.Count) return null;
        var keys = new List<string>(_lookup.Keys);
        _lookup.TryGetValue(keys[index], out var data);
        return data;
    }

    public void ClearCache()
    {
        _lookup = null;
    }
}