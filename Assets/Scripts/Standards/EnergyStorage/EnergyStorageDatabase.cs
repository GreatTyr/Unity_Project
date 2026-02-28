using UnityEngine;
using System.Collections.Generic;
#if UNITY_EDITOR
using UnityEditor;
#endif

[CreateAssetMenu(fileName = "EnergyStorageDatabase", menuName = "Game/Energy Storage Database")]
public class EnergyStorageDatabase : ScriptableObject
{
    private static EnergyStorageDatabase _instance;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatic() => _instance = null;

    public static EnergyStorageDatabase Instance
    {
        get
        {
            if (_instance == null) _instance = Resources.Load<EnergyStorageDatabase>("EnergyStorageDatabase");
            return _instance;
        }
    }

    private void OnEnable() => _instance = this;
    private void OnDisable() { if (_instance == this) _instance = null; }

    [Tooltip("Drag energy storage prefabs here.")]
    public List<GameObject> storages = new();

    public int Count => storages.Count;

    public StandardEnergyStorage GetByIndex(int index)
    {
        if (index < 0 || index >= storages.Count) return null;
        return storages[index] != null ? storages[index].GetComponent<StandardEnergyStorage>() : null;
    }

    public StandardEnergyStorage GetByFactionAndBlueprintID(string factionShortName, string blueprintId)
    {
        foreach (var go in storages)
        {
            if (go == null) continue;
            var s = go.GetComponent<StandardEnergyStorage>();
            if (s == null) continue;
            if (s.FactionShortName == factionShortName && s.BlueprintId == blueprintId)
                return s;
        }
        return null;
    }

    public List<StandardEnergyStorage> GetAll()
    {
        var result = new List<StandardEnergyStorage>();
        foreach (var go in storages)
        {
            if (go == null) continue;
            var s = go.GetComponent<StandardEnergyStorage>();
            if (s != null) result.Add(s);
        }
        return result;
    }
}

#if UNITY_EDITOR
[CustomEditor(typeof(EnergyStorageDatabase))]
public class EnergyStorageDatabaseEditor : Editor
{
    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        var db = target as EnergyStorageDatabase;

        EditorGUILayout.LabelField("Energy Storage Database", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(serializedObject.FindProperty("storages"), true);
        EditorGUILayout.Space();

        if (db.storages.Count > 0)
        {
            EditorGUILayout.LabelField("Summary", EditorStyles.boldLabel);
            for (int i = 0; i < db.storages.Count; i++)
            {
                var go = db.storages[i];
                if (go == null) continue;
                var s = go.GetComponent<StandardEnergyStorage>();
                if (s == null) continue;

                string faction = string.IsNullOrEmpty(s.FactionShortName) ? "NONE" : s.FactionShortName;
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.LabelField($"[{i}] [{faction}-{s.BlueprintId}] {go.name}", EditorStyles.boldLabel);
                EditorGUILayout.LabelField($"  Tier: {s.ModuleTier}  Capacity: {s.EnergyCapacity:F3}");
                EditorGUILayout.EndVertical();
            }
        }

        EditorGUILayout.Space();
        if (GUILayout.Button("Remove Empty Slots"))
        {
            Undo.RecordObject(db, "Remove Empty Slots");
            db.storages.RemoveAll(g => g == null);
            EditorUtility.SetDirty(db);
        }

        serializedObject.ApplyModifiedProperties();
    }
}
#endif