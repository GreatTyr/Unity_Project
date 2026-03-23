#if UNITY_EDITOR
public static class CoolerCraftedDataDrawer
{
    public static void Draw(CoolerData data)
    {
        CraftedModuleEditorCommon.DrawSection("Cooler");
        CraftedModuleEditorCommon.DrawValue("Cooling Radius", CraftedModuleEditorCommon.Format3(data.coolingRadius));
        CraftedModuleEditorCommon.DrawValue("Cooling Power", CraftedModuleEditorCommon.Format3(data.coolingPower));
        CraftedModuleEditorCommon.DrawValue("Energy Consumption", CraftedModuleEditorCommon.Format3(data.energyConsumption));
        CraftedModuleEditorCommon.DrawValue("Specific Cooling Power", CraftedModuleEditorCommon.Format3(data.specificCoolingPower));
        CraftedModuleEditorCommon.DrawValue("Specific Cooling Power Base", CraftedModuleEditorCommon.Format3(data.specificCoolingPowerBase));
        CraftedModuleEditorCommon.DrawValue("Specific Energy Consumption", CraftedModuleEditorCommon.Format3(data.specificEnergyConsumption));
        CraftedModuleEditorCommon.DrawValue("Radius Coefficient", CraftedModuleEditorCommon.Format3(data.radiusCoefficient));
        CraftedModuleEditorCommon.DrawValue("Max Cooling Difference", CraftedModuleEditorCommon.Format1(data.maxCoolingDifference));
        CraftedModuleEditorCommon.DrawValue("Min Temperature", CraftedModuleEditorCommon.Format1(data.minTemperature));
    }
}
#endif