using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class ModuleTypeDescriptor
{
    public string moduleType;
    public string dataTypeName;
    public Type standardComponentType;
    public Func<string, ModuleData> deserialize;
    public Func<string, StandardModuleBase> resolveReferenceByName;
    public Func<GameObject, RuntimeModuleBase> addRuntimeComponent;
}

public static class ModuleTypeRegistry
{
    private static readonly Dictionary<string, ModuleTypeDescriptor> byModuleType =
        new Dictionary<string, ModuleTypeDescriptor>(StringComparer.Ordinal);

    private static readonly Dictionary<string, ModuleTypeDescriptor> byDataTypeName =
        new Dictionary<string, ModuleTypeDescriptor>(StringComparer.Ordinal);

    private static bool initialized;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatic()
    {
        initialized = false;
        byModuleType.Clear();
        byDataTypeName.Clear();
    }

    private static void EnsureInitialized()
    {
        if (initialized) return;
        initialized = true;

        Register(new ModuleTypeDescriptor
        {
            moduleType = StandardGenerator.TYPE_GENERATOR,
            dataTypeName = nameof(GeneratorData),
            standardComponentType = typeof(StandardGenerator),
            deserialize = json => JsonUtility.FromJson<GeneratorData>(json),
            resolveReferenceByName = name => GeneratorDatabase.Instance != null
                ? GeneratorDatabase.Instance.GetByName(name)
                : null,
            addRuntimeComponent = go => go != null ? go.AddComponent<RuntimeGenerator>() : null
        });

        Register(new ModuleTypeDescriptor
        {
            moduleType = StandardEnergyStorage.TYPE_ENERGY_STORAGE,
            dataTypeName = nameof(EnergyStorageData),
            standardComponentType = typeof(StandardEnergyStorage),
            deserialize = json => JsonUtility.FromJson<EnergyStorageData>(json),
            resolveReferenceByName = name => EnergyStorageDatabase.Instance != null
                ? EnergyStorageDatabase.Instance.GetByName(name)
                : null,
            addRuntimeComponent = go => go != null ? go.AddComponent<RuntimeEnergyStorage>() : null
        });

        Register(new ModuleTypeDescriptor
        {
            moduleType = StandardFuelTank.TYPE_FUELTANK,
            dataTypeName = nameof(FuelTankData),
            standardComponentType = typeof(StandardFuelTank),
            deserialize = json => JsonUtility.FromJson<FuelTankData>(json),
            resolveReferenceByName = name => FuelTankDatabase.Instance != null
                ? FuelTankDatabase.Instance.GetByName(name)
                : null,
            addRuntimeComponent = go => go != null ? go.AddComponent<RuntimeFuelTank>() : null
        });

        Register(new ModuleTypeDescriptor
        {
            moduleType = StandardCooler.TYPE_COOLER,
            dataTypeName = nameof(CoolerData),
            standardComponentType = typeof(StandardCooler),
            deserialize = json => JsonUtility.FromJson<CoolerData>(json),
            resolveReferenceByName = name => CoolerDatabase.Instance != null
                ? CoolerDatabase.Instance.GetByName(name)
                : null,
            addRuntimeComponent = go => go != null ? go.AddComponent<RuntimeCooler>() : null
        });
    }

    private static void Register(ModuleTypeDescriptor descriptor)
    {
        if (descriptor == null) return;
        if (string.IsNullOrEmpty(descriptor.moduleType)) return;
        if (string.IsNullOrEmpty(descriptor.dataTypeName)) return;

        byModuleType[descriptor.moduleType] = descriptor;
        byDataTypeName[descriptor.dataTypeName] = descriptor;
    }

    public static bool TryGetByModuleType(string moduleType, out ModuleTypeDescriptor descriptor)
    {
        EnsureInitialized();
        return byModuleType.TryGetValue(moduleType ?? string.Empty, out descriptor);
    }

    public static bool TryGetByDataTypeName(string dataTypeName, out ModuleTypeDescriptor descriptor)
    {
        EnsureInitialized();
        return byDataTypeName.TryGetValue(dataTypeName ?? string.Empty, out descriptor);
    }

    public static bool TryDeserialize(string dataTypeName, string json, out ModuleData data)
    {
        data = null;
        if (string.IsNullOrEmpty(dataTypeName) || string.IsNullOrEmpty(json))
            return false;

        if (!TryGetByDataTypeName(dataTypeName, out var descriptor))
            return false;

        try
        {
            data = descriptor.deserialize?.Invoke(json);
            return data != null;
        }
        catch (Exception ex)
        {
            Debug.LogError($"[ModuleTypeRegistry] Deserialize failed for '{dataTypeName}': {ex.Message}");
            return false;
        }
    }

    public static bool TryResolveReference(string moduleType, string referenceName, out StandardModuleBase reference)
    {
        reference = null;
        if (string.IsNullOrEmpty(moduleType) || string.IsNullOrEmpty(referenceName))
            return false;

        if (!TryGetByModuleType(moduleType, out var descriptor))
            return false;

        try
        {
            reference = descriptor.resolveReferenceByName?.Invoke(referenceName);
            return reference != null;
        }
        catch (Exception ex)
        {
            Debug.LogError($"[ModuleTypeRegistry] ResolveReference failed for '{moduleType}' / '{referenceName}': {ex.Message}");
            return false;
        }
    }

    public static bool TryAddRuntimeComponent(string moduleType, GameObject target, out RuntimeModuleBase runtimeModule)
    {
        runtimeModule = null;
        if (target == null || string.IsNullOrEmpty(moduleType))
            return false;

        if (!TryGetByModuleType(moduleType, out var descriptor))
            return false;

        try
        {
            runtimeModule = descriptor.addRuntimeComponent?.Invoke(target);
            return runtimeModule != null;
        }
        catch (Exception ex)
        {
            Debug.LogError($"[ModuleTypeRegistry] AddRuntimeComponent failed for '{moduleType}': {ex.Message}");
            return false;
        }
    }

    public static bool TryRemoveStandardComponent(string moduleType, GameObject target)
    {
        if (target == null || string.IsNullOrEmpty(moduleType))
            return false;

        if (!TryGetByModuleType(moduleType, out var descriptor))
            return false;

        if (descriptor.standardComponentType == null)
            return false;

        Component component = target.GetComponent(descriptor.standardComponentType);
        if (component == null)
            return false;

        if (Application.isPlaying)
            UnityEngine.Object.Destroy(component);
        else
            UnityEngine.Object.DestroyImmediate(component);

        return true;
    }
}