using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Обобщенная база данных для любого типа модулей.
/// Содержит всю логику поиска по фракции, тиру, ID чертежа и т.д.
/// </summary>
public abstract class GenericModuleDatabase<T> : ScriptableObject where T : StandardModuleBase
{
    [Tooltip("Drag module prefabs here.")]
    public List<GameObject> modules = new List<GameObject>();

    public int Count => modules.Count;

    public T GetByIndex(int index)
    {
        if (index < 0 || index >= modules.Count) return null;
        return modules[index] != null ? modules[index].GetComponent<T>() : null;
    }

    public T GetByFactionAndBlueprintID(string factionShortName, string blueprintId)
    {
        foreach (var go in modules)
        {
            if (go == null) continue;
            var comp = go.GetComponent<T>();
            if (comp == null) continue;
            if (comp.FactionShortName == factionShortName && comp.BlueprintId == blueprintId)
                return comp;
        }
        return null;
    }

    public T GetByName(string prefabName)
    {
        if (string.IsNullOrEmpty(prefabName)) return null;
        foreach (var go in modules)
        {
            if (go != null && go.name == prefabName)
                return go.GetComponent<T>();
        }
        return null;
    }

    public T GetByFactionAndTier(string factionShortName, int moduleTier)
    {
        foreach (var go in modules)
        {
            if (go == null) continue;
            var comp = go.GetComponent<T>();
            if (comp == null) continue;
            if (comp.FactionShortName == factionShortName && comp.ModuleTier == moduleTier)
                return comp;
        }
        return null;
    }

    public List<T> GetAllByFaction(string factionShortName)
    {
        var result = new List<T>();
        foreach (var go in modules)
        {
            if (go == null) continue;
            var comp = go.GetComponent<T>();
            if (comp != null && comp.FactionShortName == factionShortName)
                result.Add(comp);
        }
        return result;
    }

    public List<T> GetAllByTier(int moduleTier)
    {
        var result = new List<T>();
        foreach (var go in modules)
        {
            if (go == null) continue;
            var comp = go.GetComponent<T>();
            if (comp != null && comp.ModuleTier == moduleTier)
                result.Add(comp);
        }
        return result;
    }

    public List<T> GetAll()
    {
        var result = new List<T>();
        foreach (var go in modules)
        {
            if (go == null) continue;
            var comp = go.GetComponent<T>();
            if (comp != null) result.Add(comp);
        }
        return result;
    }

    public bool Contains(string prefabName) => GetByName(prefabName) != null;
    public bool Contains(GameObject prefab) => prefab != null && modules.Contains(prefab);
}