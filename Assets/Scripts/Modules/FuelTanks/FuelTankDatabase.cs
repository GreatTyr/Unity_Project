using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// База данных Топливных Баков.
/// Вся логика поиска наследуется от GenericModuleDatabase.
/// </summary>
[CreateAssetMenu(fileName = "FuelTankDatabase", menuName = "Game/FuelTank Database")]
public class FuelTankDatabase : GenericModuleDatabase<StandardFuelTank>
{
    private static FuelTankDatabase _instance;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatic() => _instance = null;

    public static FuelTankDatabase Instance
    {
        get
        {
            if (_instance == null) _instance = Resources.Load<FuelTankDatabase>("FuelTankDatabase");
            return _instance;
        }
    }

    private void OnEnable() => _instance = this;
    private void OnDisable() { if (_instance == this) _instance = null; }
}

#if UNITY_EDITOR
[CustomEditor(typeof(FuelTankDatabase))]
public class FuelTankDatabaseEditor : Editor
{
    private FuelTankDatabase db;
    void OnEnable() { db = target as FuelTankDatabase; }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        EditorGUILayout.LabelField("FuelTank Database", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(serializedObject.FindProperty("modules"), new GUIContent("Fuel Tanks"), true);
        EditorGUILayout.Space();

        if (db.modules.Count > 0)
        {
            EditorGUILayout.LabelField("Summary", EditorStyles.boldLabel);
            for (int i = 0; i < db.modules.Count; i++)
            {
                var go = db.modules[i];
                if (go == null) continue;
                var sf = go.GetComponent<StandardFuelTank>();
                if (sf == null) continue;

                string faction = string.IsNullOrEmpty(sf.FactionShortName) ? "NONE" : sf.FactionShortName;
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);

                EditorGUILayout.LabelField($"[{i}] [{faction}-{sf.BlueprintId}] {go.name}", EditorStyles.boldLabel);
                EditorGUILayout.LabelField($"  Tier: {sf.ModuleTier}  Capacity: {sf.Capacity:F3}  CapCoeff: {sf.CapacityCoefficient:F2}");
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