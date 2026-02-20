using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Калькулятор крафта генератора.
/// </summary>
public class GeneratorCalculator : IModuleCalculator
{
    private GeneratorDatabase db;
    private List<StandardGenerator> allRefs;
    private string[] refNames;
    private int selectedIndex;
    private StandardGenerator selected;

    private float calcSpecificPower;
    private float calcFuelKgPerS;
    private float calcPowerTimesTierPer0001;
    private float calcFuelPer0001m3Tiered;

    const double MIN_FUEL_PER0001_D = 1e-6;
    const float MIN_FUEL_DISPLAY_TOTAL = 0.0001f;

    public GeneratorCalculator(GeneratorDatabase database)
    {
        db = database;
        Refresh();
    }

    public void Refresh()
    {
        allRefs = db != null ? db.GetAll() : new List<StandardGenerator>();
        refNames = new string[allRefs.Count];
        for (int i = 0; i < allRefs.Count; i++)
        {
            var sg = allRefs[i];
            refNames[i] = sg != null ? $"{sg.gameObject.name} (T{sg.ModuleTier})" : "(null)";
        }
        selectedIndex = 0;
        selected = allRefs.Count > 0 ? allRefs[0] : null;
    }

    public string ModuleType => ModuleTypesDatabase.TYPE_GENERATOR;
    public int ReferenceCount => allRefs.Count;
    public string[] GetReferenceNames() => refNames;

    public void SelectReference(int index)
    {
        if (index < 0 || index >= allRefs.Count) return;
        selectedIndex = index;
        selected = allRefs[index];
    }

    public int SelectedIndex => selectedIndex;
    public float RefLength => selected != null ? selected.LengthMeters : 0f;
    public float RefWidth => selected != null ? selected.WidthMeters : 0f;
    public float RefHeight => selected != null ? selected.HeightMeters : 0f;
    public float RefRealVolume => selected != null ? selected.RealVolumeM3 : 0f;
    public float RefFillPercent => selected != null ? selected.FillPercentUsed : 100f;
    public float RefVolumeCoefficientPercent => selected != null ? selected.VolumeCoefficientPercent : 100f;
    public int RefModuleTier => selected != null ? selected.ModuleTier : 1;
    public string RefFaction => selected != null ? selected.FactionShortName : "";

    public void Calculate(ModuleScaleData data)
    {
        if (selected == null)
        {
            calcSpecificPower = calcFuelKgPerS = calcPowerTimesTierPer0001 = calcFuelPer0001m3Tiered = 0f;
            return;
        }

        float effectiveVolume = data.effectiveVolume;
        float unitsPer0001 = effectiveVolume * 1000f;

        float moduleCoeff = TierCoeffs.Get(selected.ModuleTier);
        double rawPowerD = (double)selected.PowerBy0_001m3 * (double)unitsPer0001 * (double)moduleCoeff;
        calcSpecificPower = R3((float)rawPowerD);

        float powerTierCoeff = TierCoeffs.Get(selected.ModuleTier);
        calcPowerTimesTierPer0001 = R3((float)((double)selected.PowerBy0_001m3 * (double)powerTierCoeff));

        float fuelTierCoeff = TierCoeffs.Get(selected.FuelTier);
        double rawFuelPer0001D = (fuelTierCoeff > 0f)
            ? (double)selected.FuelBy0001m3_Base / (double)fuelTierCoeff
            : 0.0;
        if (rawFuelPer0001D <= 0.0) rawFuelPer0001D = MIN_FUEL_PER0001_D;
        rawFuelPer0001D = Math.Round(rawFuelPer0001D * 1_000_000.0) / 1_000_000.0;
        calcFuelPer0001m3Tiered = (float)rawFuelPer0001D;

        double totalFuelD = rawFuelPer0001D * (double)effectiveVolume * 1000.0;
        totalFuelD = Math.Round(totalFuelD * 10000.0) / 10000.0;
        if (totalFuelD < MIN_FUEL_DISPLAY_TOTAL) totalFuelD = MIN_FUEL_DISPLAY_TOTAL;
        calcFuelKgPerS = (float)totalFuelD;
    }

    public void DrawResultsGUI()
    {
        LP("Power (energy/s):", $"{calcSpecificPower:F3}");
        LP("Fuel (kg/s):", $"{calcFuelKgPerS:F4}");
        LP("Power*Tier /0.001m³:", $"{calcPowerTimesTierPer0001:F3}");
        LP("Fuel*Tier /0.001m³:", $"{calcFuelPer0001m3Tiered:F6}");
        if (selected != null)
            LP("Fuel Tier:", $"{selected.FuelTier}");
    }

    public string GetCodeSegment()
    {
        int fuelTier = selected != null ? selected.FuelTier : 1;
        return $"G{calcSpecificPower:F3}F{calcFuelKgPerS:F4}FT{fuelTier}";
    }

    public GameObject GetPrefab() => selected != null ? selected.gameObject : null;

    public ModuleData CreateModuleData(ModuleScaleData scaleData)
    {
        var data = new GeneratorData();
        data.specificPower = calcSpecificPower;
        data.fuelKgPerS = calcFuelKgPerS;
        data.fuelTier = selected != null ? selected.FuelTier : 1;
        data.powerTimesTierPer0001 = calcPowerTimesTierPer0001;
        data.fuelPer0001m3Tiered = calcFuelPer0001m3Tiered;
        data.powerBy0001m3 = selected != null ? selected.PowerBy0_001m3 : 0f;
        data.fuelBy0001m3Base = selected != null ? selected.FuelBy0001m3_Base : 0f;
        return data;
    }

    private static float R3(float v) => (float)Math.Round(v, 3);

    private void LP(string l, string r)
    {
        GUILayout.BeginHorizontal();
        GUILayout.Label(l, GUILayout.Width(160));
        GUILayout.Label(r);
        GUILayout.EndHorizontal();
    }
}