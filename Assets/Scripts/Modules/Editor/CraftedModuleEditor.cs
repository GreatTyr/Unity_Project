#if UNITY_EDITOR
using UnityEditor;

[CustomEditor(typeof(CraftedModule))]
public class CraftedModuleEditor : Editor
{
    public override void OnInspectorGUI()
    {
        CraftedModule cm = target as CraftedModule;
        if (cm == null) return;

        ModuleData data = cm.GetData();
        if (data == null)
        {
            EditorGUILayout.HelpBox("No module data.", MessageType.Warning);
            return;
        }

        CraftedModuleEditorCommon.DrawHeader("Crafted Module");
        CraftedModuleEditorCommon.DrawIdentitySection(data);
        CraftedModuleEditorCommon.DrawGeometrySection(data);
        CraftedModuleEditorCommon.DrawMassSection(data);

        if (data is CommonModuleData commonData)
            CraftedModuleEditorCommon.DrawCommonSection(commonData);

        if (data is GeneratorData gd)
            GeneratorCraftedDataDrawer.Draw(gd);

        if (data is EnergyStorageData esd)
            EnergyStorageCraftedDataDrawer.Draw(esd);

        if (data is FuelTankData ftd)
            FuelTankCraftedDataDrawer.Draw(ftd);

        if (data is CoolerData cd)
            CoolerCraftedDataDrawer.Draw(cd);

        CraftedModuleEditorCommon.DrawBuildSection(data);
        CraftedModuleEditorCommon.DrawExplosionSection(data);
        CraftedModuleEditorCommon.DrawCodeSection(data, cm);
    }
}
#endif