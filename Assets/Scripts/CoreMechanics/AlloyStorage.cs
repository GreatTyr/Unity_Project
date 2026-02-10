using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Хранилище сплавов. Код сплава → масса в кг (double, точность 0.001).
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

    private Dictionary<string, int> indexByCode =
        new Dictionary<string, int>(StringComparer.Ordinal);

    private void Awake()
    {
        RebuildIndex();
    }

    private void OnValidate()
    {
        RebuildIndex();
    }

    private void RebuildIndex()
    {
        indexByCode.Clear();
        for (int i = 0; i < entries.Count; i++)
        {
            if (string.IsNullOrEmpty(entries[i].code)) continue;
            indexByCode[entries[i].code] = i;
        }
    }

    public void AddAlloy(string code, double massKg)
    {
        if (string.IsNullOrEmpty(code) || massKg <= 0.0) return;

        if (indexByCode.TryGetValue(code, out int idx))
        {
            var e = entries[idx];
            e.massKg += massKg;
            entries[idx] = e;
        }
        else
        {
            indexByCode[code] = entries.Count;
            entries.Add(new AlloyEntry { code = code, massKg = massKg });
        }
    }

    public double GetMass(string code)
    {
        if (string.IsNullOrEmpty(code)) return 0.0;
        if (indexByCode.TryGetValue(code, out int idx))
            return entries[idx].massKg;
        return 0.0;
    }

    public bool RemoveAlloy(string code)
    {
        if (string.IsNullOrEmpty(code)) return false;
        if (!indexByCode.TryGetValue(code, out int idx)) return false;
        entries.RemoveAt(idx);
        RebuildIndex();
        return true;
    }

    public void ClearAlloys()
    {
        entries.Clear();
        indexByCode.Clear();
    }

    public IReadOnlyList<AlloyEntry> Entries => entries;
}