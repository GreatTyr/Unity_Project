using UnityEngine;

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

    private ModuleData cachedData;

    public string ModuleType => GetData()?.moduleType ?? string.Empty;

    public bool HasData => !string.IsNullOrEmpty(serializedData) && !string.IsNullOrEmpty(dataTypeName);

    public ModuleData GetData()
    {
        if (cachedData != null)
            return cachedData;

        if (!HasData)
            return null;

        cachedData = DeserializeData(dataTypeName, serializedData);
        return cachedData;
    }

    public T GetData<T>() where T : ModuleData
    {
        return GetData() as T;
    }

    public bool TryGetData<T>(out T data) where T : ModuleData
    {
        data = GetData<T>();
        return data != null;
    }

    public void SetData(ModuleData data)
    {
        if (data == null) return;

        cachedData = data;
        dataTypeName = data.GetType().Name;
        serializedData = JsonUtility.ToJson(data, false);
    }

    public string GetSerializedJson() => serializedData;
    public string GetDataTypeName() => dataTypeName;

    public static ModuleData DeserializeData(string typeName, string json)
    {
        if (string.IsNullOrEmpty(typeName) || string.IsNullOrEmpty(json))
            return null;

        if (ModuleTypeRegistry.TryDeserialize(typeName, json, out ModuleData typedData))
            return typedData;

        Debug.LogWarning($"[CraftedModule] Unknown data type '{typeName}'. Fallback to ModuleData.");
        return JsonUtility.FromJson<ModuleData>(json);
    }
}