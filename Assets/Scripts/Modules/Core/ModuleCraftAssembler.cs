using UnityEngine;

/// <summary>
/// Единая сборка готового объекта модуля:
/// 1) CraftedModule
/// 2) Скрипты из ModuleTypesConfig (по порядку)
/// 3) ModuleRuntimeState (только если крафт в мир)
/// </summary>
public static class ModuleCraftAssembler
{
    public static bool Assemble(
        GameObject target,
        ModuleCommonData data,
        ModuleTypesConfig typesConfig,
        bool craftToWorld,
        out string error)
    {
        error = string.Empty;

        if (target == null)
        {
            error = "Target GameObject is null.";
            return false;
        }

        if (data == null)
        {
            error = "ModuleCommonData is null.";
            return false;
        }

        if (typesConfig == null)
        {
            error = "ModuleTypesConfig is null.";
            return false;
        }

        CraftedModule crafted = target.GetComponent<CraftedModule>();
        if (crafted == null)
            crafted = target.AddComponent<CraftedModule>();

        crafted.SetData(data);

        if (!ModuleTypeApplier.ApplyScripts(target, data.moduleType, typesConfig, out error))
            return false;

        if (craftToWorld)
        {
            if (!typesConfig.TryResolveStandardByName(data.moduleType, data.referenceName, out StandardModuleBase standard) || standard == null)
            {
                error = $"Standard not found for moduleType='{data.moduleType}', referenceName='{data.referenceName}'.";
                return false;
            }

            ModuleRuntimeState state = target.GetComponent<ModuleRuntimeState>();
            if (state == null)
                state = target.AddComponent<ModuleRuntimeState>();

            state.InitializeFromStandard(standard);
        }

        return true;
    }
}