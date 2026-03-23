#if UNITY_EDITOR
public static class GeneratorCraftedDataDrawer
{
    public static void Draw(GeneratorData gd)
    {
        CraftedModuleEditorCommon.DrawSection("Generator");
        CraftedModuleEditorCommon.DrawValue("Power (E/s)", CraftedModuleEditorCommon.Format3(gd.specificPower));
        CraftedModuleEditorCommon.DrawValue("Fuel (kg/s)", CraftedModuleEditorCommon.Format4(gd.fuelKgPerS));
        CraftedModuleEditorCommon.DrawValue("Fuel Tier", gd.fuelTier.ToString());
        CraftedModuleEditorCommon.DrawValue("Energy Capacity", CraftedModuleEditorCommon.Format3(gd.energyCapacity));
        CraftedModuleEditorCommon.DrawValue("Power*Tier / 0.001m³", CraftedModuleEditorCommon.Format3(gd.powerTimesTierPer0001));
        CraftedModuleEditorCommon.DrawValue("Fuel / 0.001m³ Tiered", CraftedModuleEditorCommon.Format6(gd.fuelPer0001m3Tiered));
        CraftedModuleEditorCommon.DrawValue("Power by 0.001m³", CraftedModuleEditorCommon.Format3(gd.powerBy0001m3));
        CraftedModuleEditorCommon.DrawValue("Fuel by 0.001m³ Base", CraftedModuleEditorCommon.Format6(gd.fuelBy0001m3Base));
    }
}
#endif