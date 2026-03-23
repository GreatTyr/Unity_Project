#if UNITY_EDITOR
public static class FuelTankCraftedDataDrawer
{
    public static void Draw(FuelTankData data)
    {
        CraftedModuleEditorCommon.DrawSection("Fuel Tank");
        CraftedModuleEditorCommon.DrawValue("Capacity", CraftedModuleEditorCommon.Format3(data.capacity));
    }
}
#endif