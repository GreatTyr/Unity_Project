using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

[CreateAssetMenu(fileName = "ModuleTypesConfig", menuName = "Game/Module Types Config")]
public class ModuleTypesConfig : ScriptableObject
{
    [Serializable]
    public class ModuleScriptRef
    {
#if UNITY_EDITOR
        public MonoScript script;
#endif
    }

    [Serializable]
    public class ModuleTypeEntry
    {
#if UNITY_EDITOR
        public MonoScript standardScript; // StandardGenerator.cs / StandardCooler.cs ...
#endif
        public ScriptableObject standardDatabase; // GeneratorDatabase / CoolerDatabase ...
        public string resolvedStandardClassName;
        public string resolvedModuleType;
        public List<ModuleScriptRef> scripts = new List<ModuleScriptRef>();
    }

    public List<ModuleTypeEntry> moduleTypes = new List<ModuleTypeEntry>();

    public bool TryGetByModuleType(string moduleType, out ModuleTypeEntry entry)
    {
        entry = null;
        if (string.IsNullOrEmpty(moduleType)) return false;

        for (int i = 0; i < moduleTypes.Count; i++)
        {
            var e = moduleTypes[i];
            if (e == null) continue;

            if (string.Equals(e.resolvedModuleType, moduleType, StringComparison.Ordinal))
            {
                entry = e;
                return true;
            }
        }

        return false;
    }

    public bool TryResolveStandardByName(string moduleType, string referenceName, out StandardModuleBase standard)
    {
        standard = null;

        if (string.IsNullOrEmpty(moduleType) || string.IsNullOrEmpty(referenceName))
            return false;

        if (!TryGetByModuleType(moduleType, out var entry) || entry == null || entry.standardDatabase == null)
            return false;

        var dbType = entry.standardDatabase.GetType();
        var method = dbType.GetMethod("GetByName", BindingFlags.Instance | BindingFlags.Public);
        if (method == null) return false;

        object obj = method.Invoke(entry.standardDatabase, new object[] { referenceName });
        standard = obj as StandardModuleBase;
        return standard != null;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        for (int i = 0; i < moduleTypes.Count; i++)
        {
            var e = moduleTypes[i];
            if (e == null) continue;

            e.resolvedModuleType = string.Empty;
            e.resolvedStandardClassName = string.Empty;

            if (e.standardScript == null) continue;

            Type t = e.standardScript.GetClass();
            if (t == null) continue;

            e.resolvedStandardClassName = t.Name;
            e.resolvedModuleType = TryReadTypeConst(t);
        }
    }

    private static string TryReadTypeConst(Type t)
    {
        FieldInfo[] fields = t.GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy);
        for (int i = 0; i < fields.Length; i++)
        {
            var f = fields[i];
            if (f.FieldType != typeof(string)) continue;
            if (!f.IsLiteral || f.IsInitOnly) continue;
            if (!f.Name.StartsWith("TYPE_", StringComparison.Ordinal)) continue;

            return f.GetRawConstantValue() as string;
        }

        return string.Empty;
    }
#endif
}