using System;
using System.Collections.Generic;
using UnityEngine;

public class GeneratorWorkbench : BaseModuleWorkbench
{
    [Header("Generator")]
    public GeneratorDatabase generatorDatabase;

    private List<StandardGenerator> allRefs = new List<StandardGenerator>();
    private string[] refNames = new string[0];
    private int selectedRefIndex;
    private StandardGenerator selectedRef;

    private float calcSpecificPower;
    private float calcFuelKgPerS;
    private float calcPowerTimesTierPer0001;
    private float calcFuelPer0001m3Tiered;

    private float calcHeatCapacity;
    private float calcMaxTemperature;
    private float calcWallThicknessMm;
    private float calcHeatingRate;

    private float calcCraftTimeSeconds;

    const double MIN_FUEL_PER0001_D = 1e-6;
    const float MIN_FUEL_DISPLAY_TOTAL = 0.0001f;

    protected override string ModuleTypeName => "Generator";

    protected override void RebuildReferenceList()
    {
        allRefs.Clear();

        if (generatorDatabase == null)
        {
            refNames = new string[0];
            selectedRef = null;
            return;
        }

        allRefs = generatorDatabase.GetAll();
        refNames = new string[allRefs.Count];

        for (int i = 0; i < allRefs.Count; i++)
        {
            var sg = allRefs[i];
            if (sg != null)
            {
                string faction = string.IsNullOrEmpty(sg.FactionShortName) ? "NONE" : sg.FactionShortName;
                string bp = string.IsNullOrEmpty(sg.BlueprintId) ? "000" : sg.BlueprintId;
                refNames[i] = $"[{faction}-{bp}] {sg.gameObject.name} (T{sg.ModuleTier})";
            }
            else
            {
                refNames[i] = "(null)";
            }
        }

        if (selectedRefIndex >= allRefs.Count)
            selectedRefIndex = 0;

        if (allRefs.Count > 0)
            SelectReference(selectedRefIndex);
        else
            selectedRef = null;
    }

    protected override string[] GetReferenceNames() => refNames;
    protected override int GetSelectedReferenceIndex() => selectedRefIndex;
    protected override int GetReferenceCount() => allRefs.Count;

    protected override string GetReferenceBlueprintID()
    {
        return selectedRef != null ? selectedRef.BlueprintId : "000";
    }

    protected override bool TryFindAndSelectReference(string faction, string blueprintId)
    {
        if (generatorDatabase == null) return false;

        var found = generatorDatabase.GetByFactionAndBlueprintID(faction, blueprintId);
        if (found == null) return false;

        int idx = allRefs.IndexOf(found);
        if (idx < 0) return false;

        SelectReference(idx);
        return true;
    }

    protected override void SelectReference(int index)
    {
        if (index < 0 || index >= allRefs.Count) return;

        selectedRefIndex = index;
        selectedRef = allRefs[index];

        if (selectedRef != null)
        {
            scaler.SetReference(
                selectedRef.LengthMeters,
                selectedRef.WidthMeters,
                selectedRef.HeightMeters,
                selectedRef.RealVolumeM3,
                selectedRef.ConstantFillPercent
            );
        }
    }

    protected override int GetReferenceTier() => selectedRef != null ? selectedRef.ModuleTier : 1;
    protected override string GetReferenceFaction() => selectedRef != null ? selectedRef.FactionShortName : "";
    protected override float GetReferenceFillPercent() => selectedRef != null ? selectedRef.ConstantFillPercent : 100f;
    protected override float GetReferenceVolumeCoeffPercent() => selectedRef != null ? selectedRef.VolumeCoefficientPercent : 100f;
    protected override string GetReferenceName() => selectedRef != null ? selectedRef.gameObject.name : "";
    protected override GameObject GetReferencePrefab() => selectedRef != null ? selectedRef.gameObject : null;

    protected override ResourcesStorage.ResourceIndex GetMetalIndex()
    {
        int tier = GetReferenceTier();
        return (ResourcesStorage.ResourceIndex)((int)ResourcesStorage.ResourceType.Metal * ResourcesStorage.TiersPerType + (tier - 1));
    }

    protected override void RecalculateSpecifics()
    {
        if (selectedRef == null)
        {
            ResetCalculatedValues();
            return;
        }

        float fillFactor = selectedRef.ConstantFillPercent / 100f;
        float effectiveVolume = scaler.CalcEffectiveVolume * fillFactor;
        float effectiveVolumeDm3 = effectiveVolume * 1000f;

        float moduleCoeff = TierCoeffs.Get(selectedRef.ModuleTier);

        double powerTier = (double)selectedRef.PowerBy0001m3 * (double)moduleCoeff;
        calcPowerTimesTierPer0001 = R3((float)powerTier);
        calcSpecificPower = R3((float)(powerTier * effectiveVolumeDm3));

        float fuelTierCoeff = TierCoeffs.Get(selectedRef.FuelTier);
        double rawFuelPer0001D = (fuelTierCoeff > 0f)
            ? (double)selectedRef.FuelBy0001m3_Base / (double)fuelTierCoeff
            : 0.0;

        if (rawFuelPer0001D <= 0.0) rawFuelPer0001D = MIN_FUEL_PER0001_D;
        rawFuelPer0001D = Math.Round(rawFuelPer0001D * 1_000_000.0) / 1_000_000.0;
        calcFuelPer0001m3Tiered = (float)rawFuelPer0001D;

        double totalFuelD = rawFuelPer0001D * effectiveVolumeDm3;
        totalFuelD = Math.Round(totalFuelD * 10000.0) / 10000.0;
        if (totalFuelD < MIN_FUEL_DISPLAY_TOTAL) totalFuelD = MIN_FUEL_DISPLAY_TOTAL;
        calcFuelKgPerS = (float)totalFuelD;

        float realVol = scaler.CalcRealVolume;
        float heatCoeff = selectedRef.HeatCapacityCoeff;
        calcHeatCapacity = R3(realVol * heatCoeff * moduleCoeff);

        int thermAbsorb = alloyDecoded ? alloyParams.thermalAbsorption : 0;
        calcMaxTemperature = 300f + thermAbsorb;

        float shellVol = scaler.CalcShellVolume;
        float surfArea = scaler.CalcSurfaceArea;
        calcWallThicknessMm = surfArea > 0.000001f ? R3((shellVol / surfArea) * 1000f) : 0f;

        float baseHeat = selectedRef.BaseHeating;
        float thermResist = alloyDecoded ? alloyParams.thermalResistance : 0f;
        float heatingMult = Mathf.Max(0f, 1f - (thermResist / 100f));
        calcHeatingRate = R3(baseHeat * heatingMult);

        calcCraftTimeSeconds = selectedRef.CraftTimePerLiter * effectiveVolumeDm3;
    }

    private void ResetCalculatedValues()
    {
        calcSpecificPower = 0f;
        calcFuelKgPerS = 0f;
        calcPowerTimesTierPer0001 = 0f;
        calcFuelPer0001m3Tiered = 0f;

        calcHeatCapacity = 0f;
        calcMaxTemperature = 0f;
        calcWallThicknessMm = 0f;
        calcHeatingRate = 0f;

        calcCraftTimeSeconds = 0f;
    }

    protected override void DrawModuleSpecificSection()
    {
        GUILayout.BeginVertical("box");
        GUILayout.Label("Параметры Генератора", GetBoldStyle());

        GUILayout.BeginHorizontal();
        ParamBox("Мощность", $"{calcSpecificPower:F3} E/s");
        ParamBox("Топливо", $"{calcFuelKgPerS:F4} кг/с");
        ParamBox("Тир топлива", selectedRef != null ? selectedRef.FuelTier.ToString() : "-");
        ParamBox("Время крафта", $"<color=#00FF00>{calcCraftTimeSeconds:F1} сек</color>");
        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();
        ParamBox("Теплоемкость", $"{calcHeatCapacity:F1}");
        ParamBox("Макс. T", $"{calcMaxTemperature:F0}°");
        ParamBox("Толщина стенок", $"{calcWallThicknessMm:F1} мм");
        ParamBox("Нагрев", $"{calcHeatingRate:F2}°/с");
        GUILayout.EndHorizontal();

        GUILayout.EndVertical();
    }

    private void ParamBox(string label, string val)
    {
        GUILayout.BeginVertical(GUILayout.Width(130));
        GUILayout.Label($"<color=#AAAAAA>{label}</color>", new GUIStyle(GUI.skin.label) { fontSize = 12 });
        GUILayout.Label(val, GetBoldStyle());
        GUILayout.EndVertical();
    }

    protected override string GetSpecificCodeSegment()
    {
        int fuelTier = selectedRef != null ? selectedRef.FuelTier : 1;
        return $"P{calcSpecificPower.ToString("F3", System.Globalization.CultureInfo.InvariantCulture)}-F{calcFuelKgPerS.ToString("F4", System.Globalization.CultureInfo.InvariantCulture)}-FT{fuelTier}";
    }

    protected override ModuleData CreateSpecificModuleData()
    {
        var data = new GeneratorData();
        data.specificPower = calcSpecificPower;
        data.fuelKgPerS = calcFuelKgPerS;
        data.fuelTier = selectedRef != null ? selectedRef.FuelTier : 1;
        data.powerTimesTierPer0001 = calcPowerTimesTierPer0001;
        data.fuelPer0001m3Tiered = calcFuelPer0001m3Tiered;
        data.powerBy0001m3 = selectedRef != null ? selectedRef.PowerBy0001m3 : 0f;
        data.fuelBy0001m3Base = selectedRef != null ? selectedRef.FuelBy0001m3_Base : 0f;
        return data;
    }

    private static float R3(float v) => (float)Math.Round(v, 3);
}