using UnityEngine;
/// <summary>
/// Компонент, вешаемый на изготовленный модуль вместо Standard*.
/// Хранит все данные модуля в ModuleData.
/// Параметры нельзя менять — только читать.
/// </summary>
public class CraftedModule : MonoBehaviour
{
    [SerializeField, HideInInspector]
    private string serializedData;
    [SerializeField, HideInInspector]
    private string dataTypeName;
    // ── Кэш ──
    private ModuleData _cachedData;
    /// <summary>Тип модуля (для быстрого доступа без десериализации).</summary>
    public string ModuleType => GetData()?.moduleType ?? "";
    /// <summary>Получить данные модуля.</summary>
    public ModuleData GetData()
    {
        if (_cachedData != null) return _cachedData;
        if (string.IsNullOrEmpty(serializedData) || string.IsNullOrEmpty(dataTypeName))
            return null;
        _cachedData = DeserializeData(dataTypeName, serializedData);
        return _cachedData;
    }
    /// <summary>Получить данные как конкретный тип.</summary>
    public T GetData<T>() where T : ModuleData
    {
        return GetData() as T;
    }
    /// <summary>Установить данные (вызывается при крафте, один раз).</summary>
    public void SetData(ModuleData data)
    {
        if (data == null) return;
        _cachedData = data;
        dataTypeName = data.GetType().Name;
        serializedData = JsonUtility.ToJson(data, false);
    }
    /// <summary>Получить сериализованный JSON (для сохранения).</summary>
    public string GetSerializedJson()
    {
        return serializedData;
    }
    /// <summary>Получить имя типа данных (для десериализации).</summary>
    public string GetDataTypeName()
    {
        return dataTypeName;
    }
    // ── Десериализация по имени типа ──
    public static ModuleData DeserializeData(string typeName, string json)
    {
        if (string.IsNullOrEmpty(typeName) || string.IsNullOrEmpty(json))
            return null;
        if (ModuleTypeRegistry.TryDeserialize(typeName, json, out ModuleData typedData))
            return typedData;
        Debug.LogWarning($"[CraftedModule] Unknown data type '{typeName}'. Fallback to ModuleData.");
        return JsonUtility.FromJson<ModuleData>(json);
    }
    // ── Inspector display ──
#if UNITY_EDITOR
    [UnityEditor.CustomEditor(typeof(CraftedModule))]
    public class CraftedModuleEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            var cm = target as CraftedModule;
            if (cm == null) return;
            var data = cm.GetData();
            if (data == null)
            {
                UnityEditor.EditorGUILayout.HelpBox("No module data.", UnityEditor.MessageType.Warning);
                return;
            }
            GUI.enabled = false;
            UnityEditor.EditorGUILayout.LabelField("Crafted Module", UnityEditor.EditorStyles.boldLabel);
            UnityEditor.EditorGUILayout.TextField("Type", data.moduleType);
            UnityEditor.EditorGUILayout.IntField("Tier", data.moduleTier);
            UnityEditor.EditorGUILayout.TextField("Faction", data.faction);
            UnityEditor.EditorGUILayout.TextField("Reference", data.referenceName);
            UnityEditor.EditorGUILayout.TextField("Alloy", data.alloyCode);
            UnityEditor.EditorGUILayout.Space();
            UnityEditor.EditorGUILayout.LabelField("Dimensions", UnityEditor.EditorStyles.boldLabel);
            UnityEditor.EditorGUILayout.FloatField("Length (X)", data.length);
            UnityEditor.EditorGUILayout.FloatField("Width (Z)", data.width);
            UnityEditor.EditorGUILayout.FloatField("Height (Y)", data.height);
            UnityEditor.EditorGUILayout.FloatField("Scale Factor", data.scaleFactor);
            UnityEditor.EditorGUILayout.Space();
            UnityEditor.EditorGUILayout.LabelField("Volumes (m³)", UnityEditor.EditorStyles.boldLabel);
            UnityEditor.EditorGUILayout.FloatField("Real Volume", data.realVolume);
            UnityEditor.EditorGUILayout.FloatField("Shell Volume", data.shellVolumeM3);
            UnityEditor.EditorGUILayout.FloatField("Effective Volume", data.effectiveVolume);
            UnityEditor.EditorGUILayout.Space();
            UnityEditor.EditorGUILayout.LabelField("Mass (kg)", UnityEditor.EditorStyles.boldLabel);
            UnityEditor.EditorGUILayout.FloatField("Shell Mass", data.shellMassKg);
            UnityEditor.EditorGUILayout.FloatField("Inner Mass", data.innerMassKg);
            UnityEditor.EditorGUILayout.FloatField("Total Mass", data.totalMassKg);
            UnityEditor.EditorGUILayout.FloatField("Durability", data.durability);
            // НОВОЕ: Толщина стенок
            UnityEditor.EditorGUILayout.Space();
            UnityEditor.EditorGUILayout.LabelField("Shell Geometry", UnityEditor.EditorStyles.boldLabel);
            UnityEditor.EditorGUILayout.FloatField("Wall Thickness (mm)", data.wallThicknessMm);
            UnityEditor.EditorGUILayout.FloatField("Shell %", data.shellPercent);
            // Специфичные поля
            if (data is GeneratorData gd)
            {
                UnityEditor.EditorGUILayout.Space();
                UnityEditor.EditorGUILayout.LabelField("Generator", UnityEditor.EditorStyles.boldLabel);
                UnityEditor.EditorGUILayout.FloatField("Power (energy/s)", gd.specificPower);
                UnityEditor.EditorGUILayout.FloatField("Fuel (kg/s)", gd.fuelKgPerS);
                UnityEditor.EditorGUILayout.IntField("Fuel Tier", gd.fuelTier);
            }
            if (data is EnergyStorageData esd)
            {
                UnityEditor.EditorGUILayout.Space();
                UnityEditor.EditorGUILayout.LabelField("Energy Storage", UnityEditor.EditorStyles.boldLabel);
                UnityEditor.EditorGUILayout.FloatField("Energy Capacity", esd.energyCapacity);
            }
            if (data is FuelTankData ftd)
            {
                UnityEditor.EditorGUILayout.Space();
                UnityEditor.EditorGUILayout.LabelField("Fuel Tank", UnityEditor.EditorStyles.boldLabel);
                UnityEditor.EditorGUILayout.FloatField("Capacity", ftd.capacity);
                UnityEditor.EditorGUILayout.FloatField("Max Temperature", ftd.maxTemperature);
                UnityEditor.EditorGUILayout.FloatField("Heat Capacity", ftd.heatCapacity);
            }
            UnityEditor.EditorGUILayout.Space();
            UnityEditor.EditorGUILayout.LabelField("Code", UnityEditor.EditorStyles.boldLabel);
            UnityEditor.EditorGUILayout.TextField("Module Code", data.moduleCode);
            UnityEditor.EditorGUILayout.TextField("Craft Time", data.craftTimestamp);
            GUI.enabled = true;
        }
    }
#endif
}