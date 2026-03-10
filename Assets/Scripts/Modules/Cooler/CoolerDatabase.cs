using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// База данных Охлаждающих Радиаторов.
/// Вся логика поиска наследуется от GenericModuleDatabase.
/// </summary>
[CreateAssetMenu(fileName = "CoolerDatabase", menuName = "Game/Cooler Database")]
public class CoolerDatabase : GenericModuleDatabase<StandardCooler>
{
    private static CoolerDatabase _instance;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatic() => _instance = null;

    public static CoolerDatabase Instance
    {
        get
        {
            if (_instance == null) _instance = Resources.Load<CoolerDatabase>("CoolerDatabase");
            return _instance;
        }
    }

    private void OnEnable() => _instance = this;
    private void OnDisable() { if (_instance == this) _instance = null; }
}

#if UNITY_EDITOR
[CustomEditor(typeof(CoolerDatabase))]
public class CoolerDatabaseEditor : Editor
{
    private CoolerDatabase db;
    void OnEnable() { db = target as CoolerDatabase; }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        EditorGUILayout.LabelField("Cooler Database", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(serializedObject.FindProperty("modules"), new GUIContent("Coolers"), true);
        EditorGUILayout.Space();

        if (db.modules.Count > 0)
        {
            EditorGUILayout.LabelField("Summary", EditorStyles.boldLabel);
            for (int i = 0; i < db.modules.Count; i++)
            {
                var go = db.modules[i];
                if (go == null) continue;
                var sc = go.GetComponent<StandardCooler>();
                if (sc == null) continue;

                string faction = string.IsNullOrEmpty(sc.FactionShortName) ? "NONE" : sc.FactionShortName;
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);

                EditorGUILayout.LabelField($"[{i}] [{faction}-{sc.BlueprintId}] {go.name}", EditorStyles.boldLabel);
                EditorGUILayout.LabelField($"  Tier: {sc.ModuleTier}  Cooling: {sc.CoolingPower:F3}  Radius: {sc.CoolingRadius:F3}");
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