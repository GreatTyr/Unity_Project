using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// База данных Бронеплит.
/// Не использует GenericModuleDatabase, т.к. StandardArmorPlate не наследует StandardModuleBase.
/// </summary>
[CreateAssetMenu(fileName = "ArmorPlateDatabase", menuName = "Game/ArmorPlate Database")]
public class ArmorPlateDatabase : ScriptableObject
{
    public List<GameObject> modules = new List<GameObject>();

    private static ArmorPlateDatabase _instance;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatic() => _instance = null;

    public static ArmorPlateDatabase Instance
    {
        get
        {
            if (_instance == null) _instance = Resources.Load<ArmorPlateDatabase>("ArmorPlateDatabase");
            return _instance;
        }
    }

    private void OnEnable() => _instance = this;
    private void OnDisable() { if (_instance == this) _instance = null; }

    public int Count => modules.Count;

    public List<StandardArmorPlate> GetAll()
    {
        List<StandardArmorPlate> result = new List<StandardArmorPlate>();
        foreach (var go in modules)
        {
            if (go == null) continue;
            var comp = go.GetComponent<StandardArmorPlate>();
            if (comp != null) result.Add(comp);
        }
        return result;
    }

    public StandardArmorPlate GetByIndex(int index)
    {
        if (index < 0 || index >= modules.Count || modules[index] == null)
            return null;
        return modules[index].GetComponent<StandardArmorPlate>();
    }

    public StandardArmorPlate GetByFactionAndBlueprintID(string faction, int blueprintId)
    {
        foreach (var go in modules)
        {
            if (go == null) continue;
            var comp = go.GetComponent<StandardArmorPlate>();
            if (comp == null) continue;

            string compFaction = string.IsNullOrEmpty(comp.FactionShortName) ? "NONE" : comp.FactionShortName;
            string searchFaction = string.IsNullOrEmpty(faction) ? "NONE" : faction;

            if (compFaction == searchFaction && comp.BlueprintIdInt == blueprintId)


                return comp;
        }
        return null;
    }
    public StandardArmorPlate GetByName(string referenceName)
    {
        foreach (var go in modules)
        {
            if (go == null) continue;
            if (go.name == referenceName)
            {
                var comp = go.GetComponent<StandardArmorPlate>();
                if (comp != null) return comp;
            }
        }
        return null;
    }
}

#if UNITY_EDITOR
[CustomEditor(typeof(ArmorPlateDatabase))]
public class ArmorPlateDatabaseEditor : Editor
{
    private ArmorPlateDatabase db;
    void OnEnable() { db = target as ArmorPlateDatabase; }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        EditorGUILayout.LabelField("ArmorPlate Database", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(serializedObject.FindProperty("modules"), new GUIContent("Armor Plates"), true);
        EditorGUILayout.Space();

        if (db.modules.Count > 0)
        {
            EditorGUILayout.LabelField("Summary", EditorStyles.boldLabel);
            for (int i = 0; i < db.modules.Count; i++)
            {
                var go = db.modules[i];
                if (go == null) continue;
                var ap = go.GetComponent<StandardArmorPlate>();
                if (ap == null) continue;
                
                string faction = string.IsNullOrEmpty(ap.FactionShortName) ? "NONE" : ap.FactionShortName;
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                
                EditorGUILayout.LabelField($"[{i}] [{faction}-{ap.BlueprintId}] {go.name}", EditorStyles.boldLabel);
                EditorGUILayout.LabelField($"  Tier: {ap.ModuleTier}  Volume: {ap.VolumeM3:F6} m³  Mass Coeff: {ap.MassCoefficient:F3}");
                EditorGUILayout.EndVertical();
            }
        }
        EditorGUILayout.Space();
        if (GUILayout.Button("Remove Empty Slots"))
        {
            Undo.RecordObject(db, "Remove Empty Slots");
            db.modules.RemoveAll(g => g == null);
            EditorUtility.SetDirty(db);
        }
        serializedObject.ApplyModifiedProperties();
    }
}
#endif