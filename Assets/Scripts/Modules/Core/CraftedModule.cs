using UnityEngine;
using System;

/// <summary>
/// Контейнер скрафченного модуля.
/// Хранит сериализованные данные модуля и предоставляет readonly-доступ к ним.
/// </summary>
[DisallowMultipleComponent]
public class CraftedModule : MonoBehaviour
{
    [SerializeField, HideInInspector]
    private string serializedData;

    [SerializeField, HideInInspector]
    private string dataTypeName;

    private ModuleCommonData cachedData;

    public string ModuleType => GetData()?.moduleType ?? string.Empty;

    public bool HasData => !string.IsNullOrEmpty(serializedData) && !string.IsNullOrEmpty(dataTypeName);

    public ModuleCommonData GetData()
    {
        if (cachedData != null)
            return cachedData;

        if (!HasData)
            return null;

        cachedData = DeserializeData(dataTypeName, serializedData);
        return cachedData;
    }

    public T GetData<T>() where T : ModuleCommonData
    {
        return GetData() as T;
    }

    public bool TryGetData<T>(out T data) where T : ModuleCommonData
    {
        data = GetData<T>();
        return data != null;
    }

    public void SetData(ModuleCommonData data)
    {
        if (data == null) return;

        cachedData = data;
        dataTypeName = data.GetType().Name;
        serializedData = JsonUtility.ToJson(data, false);
    }

    public string GetSerializedJson() => serializedData;
    public string GetDataTypeName() => dataTypeName;

    public static ModuleCommonData DeserializeData(string typeName, string json)
    {
        if (string.IsNullOrEmpty(typeName) || string.IsNullOrEmpty(json))
            return null;

        try
        {
            switch (typeName)
            {
                case nameof(GeneratorData):
                    return JsonUtility.FromJson<GeneratorData>(json);
                case nameof(EnergyStorageData):
                    return JsonUtility.FromJson<EnergyStorageData>(json);
                case nameof(FuelTankData):
                    return JsonUtility.FromJson<FuelTankData>(json);
                case nameof(CoolerData):
                    return JsonUtility.FromJson<CoolerData>(json);
                case nameof(TurretData):
                    return JsonUtility.FromJson<TurretData>(json);
                case nameof(ArmorPlateData):
                    return JsonUtility.FromJson<ArmorPlateData>(json);
                default:
                    Debug.LogWarning($"[CraftedModule] Unknown data type '{typeName}'. Fallback to ModuleCommonData.");
                    return JsonUtility.FromJson<ModuleCommonData>(json);
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"[CraftedModule] Deserialize failed for '{typeName}': {ex.Message}");
            return null;
        }
    }
}