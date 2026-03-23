#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

public static class CraftedModuleEditorCommon
{
    public static void DrawHeader(string title)
    {
        EditorGUILayout.Space(4);
        EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
        EditorGUILayout.Space(2);
    }

    public static void DrawSection(string title)
    {
        EditorGUILayout.Space(6);
        EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
    }

    public static void DrawValue(string label, string value)
    {
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField(label, GUILayout.Width(180));
        EditorGUILayout.SelectableLabel(
            string.IsNullOrEmpty(value) ? "—" : value,
            EditorStyles.textField,
            GUILayout.Height(EditorGUIUtility.singleLineHeight));
        EditorGUILayout.EndHorizontal();
    }

    public static void DrawMultilineValue(string label, string value)
    {
        EditorGUILayout.LabelField(label);
        EditorGUILayout.SelectableLabel(
            string.IsNullOrEmpty(value) ? "—" : value,
            EditorStyles.textArea,
            GUILayout.MinHeight(48));
    }

    public static void DrawIdentitySection(ModuleData data)
    {
        DrawSection("Identity");
        DrawValue("Type", data.moduleType);
        DrawValue("Tier", data.moduleTier.ToString());
        DrawValue("Faction", data.faction);
        DrawValue("Reference", data.referenceName);
        DrawValue("Alloy", data.alloyCode);
    }

    public static void DrawGeometrySection(ModuleData data)
    {
        DrawSection("Geometry");
        DrawValue("Length (X)", Format3(data.length));
        DrawValue("Width (Z)", Format3(data.width));
        DrawValue("Height (Y)", Format3(data.height));
        DrawValue("Scale Factor", Format3(data.scaleFactor));

        DrawValue("AABB Volume", Format6(data.aabbVolume));
        DrawValue("Real Volume", Format6(data.realVolume));
        DrawValue("Shell Volume", Format6(data.shellVolumeM3));
        DrawValue("Effective Volume", Format6(data.effectiveVolume));
        DrawValue("Shell %", Format3(data.shellPercent));
    }

    public static void DrawMassSection(ModuleData data)
    {
        DrawSection("Mass / Structure");
        DrawValue("Shell Mass (kg)", Format3(data.shellMassKg));
        DrawValue("Inner Mass (kg)", Format3(data.innerMassKg));
        DrawValue("Total Mass (kg)", Format3(data.totalMassKg));
        DrawValue("Durability", Format3(data.durability));
        DrawValue("Wall Thickness (mm)", Format1(data.wallThicknessMm));
    }

    public static void DrawCommonSection(CommonModuleData data)
    {
        DrawSection("Common Module Params");
        DrawValue("Heat Capacity", Format1(data.heatCapacity));
        DrawValue("Max Temperature", Format1(data.maxTemperature));
        DrawValue("Heating Rate", Format2(data.heatingRate));
        DrawValue("Craft Time (s)", Format1(data.craftTimeSeconds));
        DrawValue("Operational Usage", string.IsNullOrEmpty(data.operationalResourceUsageSummary) ? "—" : data.operationalResourceUsageSummary);
        DrawValue("Static Capacity Max", Format1(data.staticCapacityMax));
        DrawValue("Static Capacity Current", Format1(data.staticCapacityCurrent));
        DrawValue("Static Drain / s", Format3(data.staticCapacityDrainPerSecond));
    }

    public static void DrawBuildSection(ModuleData data)
    {
        DrawSection("Build");
        DrawValue("Build Visual Yaw Offset", Format3(data.buildVisualYawOffset));
        DrawValue("Build Anchor Local", data.buildAnchorLocal.ToString("F3"));
        DrawValue("Build Anchor Cell Local", data.buildAnchorCellLocal.ToString());
        DrawValue("Reference Visual Scale", data.referenceVisualScale.ToString("F3"));
    }

    public static void DrawExplosionSection(ModuleData data)
    {
        DrawSection("Explosion / Volatility");
        DrawValue("Is Volatile", data.isVolatile ? "Yes" : "No");
        DrawValue("Explosion Damage Type", data.explosionDamageType.ToString());
        DrawValue("Explosion Radius", Format3(data.explosionRadiusMeters));
        DrawValue("Explosion Penetration", Format3(data.explosionPenetration));
        DrawValue("Explosion Damage", Format3(data.explosionDamage));
    }

    public static void DrawCodeSection(ModuleData data, CraftedModule cm)
    {
        DrawSection("Code");
        DrawMultilineValue("Module Code", data.moduleCode);
        DrawValue("Craft Timestamp", data.craftTimestamp);
        DrawValue("Data Version", data.dataVersion.ToString());
        DrawValue("Data Type", cm.GetDataTypeName());
    }

    public static string Format1(float value) => value.ToString("F1");
    public static string Format2(float value) => value.ToString("F2");
    public static string Format3(float value) => value.ToString("F3");
    public static string Format4(float value) => value.ToString("F4");
    public static string Format6(float value) => value.ToString("F6");
}
#endif