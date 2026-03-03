using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Ѕаза данных ’ранилищ Ёнергии. 
/// Ћогика поиска наследуетс€ от GenericModuleDatabase.
/// </summary>
[CreateAssetMenu(fileName = "EnergyStorageDatabase", menuName = "Game/Energy Storage Database")]
public class EnergyStorageDatabase : GenericModuleDatabase<StandardEnergyStorage>
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
}

#if UNITY_EDITOR
[CustomEditor(typeof(EnergyStorageDatabase))]
public class EnergyStorageDatabaseEditor : Editor
{
    private EnergyStorageDatabase db;
    void OnEnable() { db = target as EnergyStorageDatabase; }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        EditorGUILayout.LabelField("Energy Storage Database", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(serializedObject.FindProperty("modules"), new GUIContent("Storages"), true);
        EditorGUILayout.Space();

        if (db.modules.Count > 0)
        {
            EditorGUILayout.LabelField("Summary", EditorStyles.boldLabel);
            for (int i = 0; i < db.modules.Count; i++)
            {
                var go = db.modules[i];
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
            db.modules.RemoveAll(g => g == null);
            EditorUtility.SetDirty(db);
        }

        serializedObject.ApplyModifiedProperties();
    }
}
#endif