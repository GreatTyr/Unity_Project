using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "ModuleTypesDatabase", menuName = "Game/Module Types Database")]
public class ModuleTypesDatabase : ScriptableObject
{
    // ====================== Standard type constants ======================
    public const string TYPE_GENERATOR = "Generator";
    public const string TYPE_FUEL_TANK = "FuelTank";
    public const string TYPE_ENERGY_STORAGE = "EnergyStorage";

    // ====================== Singleton ======================
    private static ModuleTypesDatabase _instance;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatic()
    {
        _instance = null;
    }

    public static ModuleTypesDatabase Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = Resources.Load<ModuleTypesDatabase>("ModuleTypesDatabase");
                if (_instance == null)
                {
                    var all = Resources.LoadAll<ModuleTypesDatabase>("");
                    if (all != null && all.Length > 0)
                        _instance = all[0];
                }
                if (_instance == null)
                    Debug.LogError("[ModuleTypesDatabase] Asset not found in Resources!");
            }
            return _instance;
        }
    }

    public static void InvalidateCache()
    {
        _instance = null;
    }

    // ====================== Data ======================
    [SerializeField]
    [Tooltip("Drag module database assets here (GeneratorDatabase, EnergyStorageDatabase, etc.).\n" +
             "Each must implement IModuleDatabase. Module type is read automatically from IModuleDatabase.ModuleType.")]
    private List<ScriptableObject> databases = new();

    // ====================== Lifecycle ======================
    private void OnEnable()
    {
        _instance = this;
    }

    private void OnDisable()
    {
        if (_instance == this)
            _instance = null;
    }

    // ====================== Access ======================

    /// <summary>Number of registered databases</summary>
    public int Count => databases.Count;

    /// <summary>Get all valid IModuleDatabase references</summary>
    public List<IModuleDatabase> GetAllDatabases()
    {
        var result = new List<IModuleDatabase>();
        foreach (var so in databases)
        {
            if (so != null && so is IModuleDatabase mdb)
                result.Add(mdb);
        }
        return result;
    }

    /// <summary>Get database by module type name</summary>
    public IModuleDatabase GetDatabase(string moduleType)
    {
        if (string.IsNullOrEmpty(moduleType)) return null;
        foreach (var so in databases)
        {
            if (so != null && so is IModuleDatabase mdb && mdb.ModuleType == moduleType)
                return mdb;
        }
        return null;
    }

    /// <summary>Get raw ScriptableObject by module type name</summary>
    public ScriptableObject GetDatabaseAsset(string moduleType)
    {
        if (string.IsNullOrEmpty(moduleType)) return null;
        foreach (var so in databases)
        {
            if (so != null && so is IModuleDatabase mdb && mdb.ModuleType == moduleType)
                return so;
        }
        return null;
    }

    /// <summary>Check if a module type is registered</summary>
    public bool Exists(string moduleType)
    {
        return GetDatabase(moduleType) != null;
    }

    /// <summary>Get module type name by index</summary>
    public string GetTypeByIndex(int index)
    {
        if (index < 0 || index >= databases.Count) return null;
        var so = databases[index];
        if (so != null && so is IModuleDatabase mdb)
            return mdb.ModuleType;
        return null;
    }

    /// <summary>Get index of module type</summary>
    public int IndexOf(string moduleType)
    {
        if (string.IsNullOrEmpty(moduleType)) return -1;
        for (int i = 0; i < databases.Count; i++)
        {
            var so = databases[i];
            if (so != null && so is IModuleDatabase mdb && mdb.ModuleType == moduleType)
                return i;
        }
        return -1;
    }

    /// <summary>Get all registered module type names</summary>
    public string[] GetAllTypeNames()
    {
        var result = new List<string>();
        foreach (var so in databases)
        {
            if (so != null && so is IModuleDatabase mdb)
            {
                string t = mdb.ModuleType;
                if (!string.IsNullOrEmpty(t))
                    result.Add(t);
            }
        }
        return result.ToArray();
    }

    /// <summary>Get type names with "(None)" at index 0 for dropdowns</summary>
    public string[] GetDisplayNamesWithNone()
    {
        var names = GetAllTypeNames();
        var result = new string[names.Length + 1];
        result[0] = "(None)";
        for (int i = 0; i < names.Length; i++)
            result[i + 1] = names[i];
        return result;
    }

    /// <summary>Check which standard types are missing</summary>
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