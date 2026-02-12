using UnityEngine;
using System.Collections.Generic;

#if UNITY_EDITOR
using UnityEditor;
#endif

[CreateAssetMenu(fileName = "EnergyStorageDatabase", menuName = "Game/Energy Storage Database")]
public class EnergyStorageDatabase : ScriptableObject
{
    private static EnergyStorageDatabase _instance;

    public static EnergyStorageDatabase Instance
    {
        get
        {
            if (_instance == null)
                _instance = Resources.Load<EnergyStorageDatabase>("EnergyStorageDatabase");
            return _instance;
        }
    }

    [Tooltip("Drag energy storage prefabs here. Each must have StandardEnergyStorage component.")]
    public List<GameObject> storages = new List<GameObject>();

    public int Count => storages.Count;

    public StandardEnergyStorage GetByIndex(int index)
    {
        if (index < 0 || index >= storages.Count) return null;
        var go = storages[index];
        return go != null ? go.GetComponent<StandardEnergyStorage>() : null;
    }

    public List<StandardEnergyStorage> GetAll()
    {
        var result = new List<StandardEnergyStorage>();
        foreach (var go in storages)
        {
            if (go == null) continue;
            var es = go.GetComponent<StandardEnergyStorage>();
            if (es != null) result.Add(es);
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
        EditorGUILayout.HelpBox(
            "Drag energy storage prefabs into the list.\nEach must have StandardEnergyStorage component.",
            MessageType.Info);

        EditorGUILayout.Space();

        // Рисуем стандартный список — гарантированно работает drag & drop
        var prop = serializedObject.FindProperty("storages");
        EditorGUILayout.PropertyField(prop, new GUIContent($"Storages ({prop.arraySize})"), true);

        EditorGUILayout.Space();

        // Валидация
        bool hasIssues = false;
        for (int i = 0; i < db.storages.Count; i++)
        {
            var go = db.storages[i];
            if (go == null)
            {
                EditorGUILayout.HelpBox($"[{i}] — empty slot", MessageType.Warning);
                hasIssues = true;
                continue;
            }
            if (go.GetComponent<StandardEnergyStorage>() == null)
            {
                EditorGUILayout.HelpBox($"[{i}] \"{go.name}\" — no StandardEnergyStorage!", MessageType.Error);
                hasIssues = true;
            }
        }

        if (!hasIssues && db.storages.Count > 0)
            EditorGUILayout.LabelField("✓ All entries valid", EditorStyles.miniLabel);

        // Сводка
        if (db.storages.Count > 0)
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Summary", EditorStyles.boldLabel);

            foreach (var go in db.storages)
            {
                if (go == null) continue;
                var es = go.GetComponent<StandardEnergyStorage>();
                if (es == null) continue;

                string faction = string.IsNullOrEmpty(es.FactionShortName) ? "—" : es.FactionShortName;
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.LabelField(go.name, EditorStyles.boldLabel);
                EditorGUILayout.LabelField($"  Faction: {faction}   Tier: {es.ModuleTier}   Capacity: {es.EnergyCapacity:F3}");
                EditorGUILayout.LabelField($"  Eff.Vol: {es.EffectiveVolumeM3:F6} m³   Mass: {es.MassKg:F3} kg");
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