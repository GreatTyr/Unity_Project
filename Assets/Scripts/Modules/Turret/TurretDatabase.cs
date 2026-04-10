// TurretDatabase.cs
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// База данных эталонов турелей.
/// </summary>
[CreateAssetMenu(fileName = "TurretDatabase", menuName = "Game/Turret Database")]
public class TurretDatabase : GenericModuleDatabase<StandardTurret>
{
    private static TurretDatabase _instance;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatic() => _instance = null;

    public static TurretDatabase Instance
    {
        get
        {
            if (_instance == null) _instance = Resources.Load<TurretDatabase>("TurretDatabase");
            return _instance;
        }
    }

    private void OnEnable() { _instance = this; }
    private void OnDisable() { if (_instance == this) _instance = null; }
}

#if UNITY_EDITOR
[CustomEditor(typeof(TurretDatabase))]
public class TurretDatabaseEditor : Editor
{
    private TurretDatabase db;

    private void OnEnable() { db = target as TurretDatabase; }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        EditorGUILayout.LabelField("Turret Database", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(
            serializedObject.FindProperty("modules"),
            new GUIContent("Turrets"),
            true);

        EditorGUILayout.Space();

        if (db != null && db.modules.Count > 0)
        {
            EditorGUILayout.LabelField("Summary", EditorStyles.boldLabel);
            for (int i = 0; i < db.modules.Count; i++)
            {
                var go = db.modules[i];
                if (go == null) continue;
                var st = go.GetComponent<StandardTurret>();
                if (st == null) continue;

                string faction = string.IsNullOrEmpty(st.FactionShortName)
                    ? "NONE"
                    : st.FactionShortName;

                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.LabelField(
                    $"[{i}] [{faction}-{st.BlueprintId}] {go.name}",
                    EditorStyles.boldLabel);
                EditorGUILayout.LabelField(
                    $"  Tier: {st.ModuleTier}  " +
                    $"MountCoeff: {st.MountCoeff:F2}  " +
                    $"DurabilityCoeff: {st.DurabilityCoeff:F2}  " +
                    $"AmmoTierBonus: +{st.AmmoTierBonus}");
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