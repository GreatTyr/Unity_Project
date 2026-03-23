#if UNITY_EDITOR
public static class EnergyStorageCraftedDataDrawer
{
    public static void Draw(EnergyStorageData data)
    {
        CraftedModuleEditorCommon.DrawSection("Energy Storage");
        CraftedModuleEditorCommon.DrawValue("Energy Capacity", CraftedModuleEditorCommon.Format3(data.energyCapacity));
    }
}
#endif