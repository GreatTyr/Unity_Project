using System;
using UnityEngine;

public static class ModuleTypeApplier
{
    public static bool ApplyScripts(GameObject target, string moduleType, ModuleTypesConfig config, out string error)
    {
        error = string.Empty;

        if (target == null)
        {
            error = "Target GameObject is null.";
            return false;
        }

        if (config == null)
        {
            error = "ModuleTypesConfig is null.";
            return false;
        }

        if (!config.TryGetByModuleType(moduleType, out var entry) || entry == null)
        {
            error = $"Module type '{moduleType}' not found in ModuleTypesConfig.";
            return false;
        }

        if (entry.scripts == null || entry.scripts.Count == 0)
            return true;

        for (int i = 0; i < entry.scripts.Count; i++)
        {
            var s = entry.scripts[i];
            if (s == null || s.script == null) continue;

#if UNITY_EDITOR
            Type t = s.script.GetClass();
            if (t == null) continue;
            if (!typeof(MonoBehaviour).IsAssignableFrom(t)) continue;

            if (target.GetComponent(t) == null)
                target.AddComponent(t);
#endif
        }

        return true;
    }
}