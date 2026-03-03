using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// База данных Генераторов.
/// Вся логика поиска наследуется от GenericModuleDatabase.
/// </summary>
[CreateAssetMenu(fileName = "GeneratorDatabase", menuName = "Game/Generator Database")]
public class GeneratorDatabase : GenericModuleDatabase<StandardGenerator>
{
    private static GeneratorDatabase _instance;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatic() => _instance = null;

    public static GeneratorDatabase Instance
    {
        get
        {
            if (_instance == null) _instance = Resources.Load<GeneratorDatabase>("GeneratorDatabase");
            return _instance;
        }
    }

    private void OnEnable() => _instance = this;
    private void OnDisable() { if (_instance == this) _instance = null; }
}

#if UNITY_EDITOR
[CustomEditor(typeof(GeneratorDatabase))]
public class GeneratorDatabaseEditor : Editor
{
    private GeneratorDatabase db;
    void OnEnable() { db = target as GeneratorDatabase; }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        EditorGUILayout.LabelField("Generator Database", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(serializedObject.FindProperty("modules"), new GUIContent("Generators"), true);
        EditorGUILayout.Space();

        if (db.modules.Count > 0)
        {
            EditorGUILayout.LabelField("Summary", EditorStyles.boldLabel);
            for (int i = 0; i < db.modules.Count; i++)
            {
                var go = db.modules[i];
                if (go == null) continue;
                var sg = go.GetComponent<StandardGenerator>();
                if (sg == null) continue;
                
                string faction = string.IsNullOrEmpty(sg.FactionShortName) ? "NONE" : sg.FactionShortName;
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                
                EditorGUILayout.LabelField($"[{i}] [{faction}-{sg.BlueprintId}] {go.name}", EditorStyles.boldLabel);
                EditorGUILayout.LabelField($"  Tier: {sg.ModuleTier}  Fuel Tier: {sg.FuelTier}  Power: {sg.SpecificPower:F3}");
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