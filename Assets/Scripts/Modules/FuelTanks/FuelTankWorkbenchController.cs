using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Контроллер Верстака Топливных Баков.
/// Особенности: нет внутренних ресурсов, фильтрация сплавов по тиру, FillPercent = 0.
/// </summary>
public class FuelTankWorkbenchController
    : BaseModuleWorkbenchController<StandardFuelTank, FuelTankData, FuelTankDatabase>
{
    public float CalcCapacity { get; private set; }
    public float CalcHeatCapacity { get; private set; }
    public float CalcMaxTemperature { get; private set; }
    public float CalcWallThicknessMm { get; private set; }

    protected override string ModuleTypeName => StandardFuelTank.TYPE_FUELTANK;

    // Бак: FillPercent = 0, внутренних ресурсов нет
    protected override void GetReferenceScalerParams(StandardFuelTank reference, out float fillPercent)
    {
        fillPercent = 0f;
    }

    // Бак: при смене эталона пересобираем сплавы (фильтр по тиру)
    protected override void OnReferenceChanged()
    {
        RebuildAlloyList();
    }

    // Бак: фильтрует сплавы по тиру эталона
    public override void RebuildAlloyList()
    {
        if (alloyStorage == null || alloyStorage.Count == 0)
        {
            AlloyDisplayNames = Array.Empty<string>();
            AlloyCodes = Array.Empty<string>();
            IsAlloyDecoded = false;
            return;
        }

        int minTier = SelectedRef != null ? SelectedRef.ModuleTier : 1;

        string[] allCodes = alloyStorage.GetAllCodes();
        string[] allNames = alloyStorage.GetDisplayNames();

        var filteredCodes = new List<string>();
        var filteredNames = new List<string>();

        for (int i = 0; i < allCodes.Length; i++)
        {
            if (AlloyCode.Decode(allCodes[i], out AlloyCode.AlloyParams p) && p.tier >= minTier)
            {
                filteredCodes.Add(allCodes[i]);
                filteredNames.Add(allNames[i]);
            }
        }

        AlloyCodes = filteredCodes.ToArray();
        AlloyDisplayNames = filteredNames.ToArray();

        if (AlloyCodes.Length > 0)
            SelectAlloy(0);
        else
            IsAlloyDecoded = false;
    }

    // Бак: проверяет тир сплава >= тир эталона
    protected override bool CheckSpecificCraftConditions(out string failReason)
    {
        failReason = "";
        if (IsAlloyDecoded && AlloyParams.tier < SelectedRef.ModuleTier)
        {
            failReason = $"Тир сплава ({AlloyParams.tier}) ниже тира эталона ({SelectedRef.ModuleTier}).";
            return false;
        }
        return true;
    }

    // Бак: не списывает внутренние ресурсы
    protected override void ConsumeSpecificResources() { }

    // Бак: при вставке чертежа проверяет тир сплава
    protected override void OnBlueprintAlloyApplied(AlloyCode.AlloyParams parsedAlloy)
    {
        int minTier = SelectedRef != null ? SelectedRef.ModuleTier : 1;
        if (parsedAlloy.tier < minTier)
        {
            ShowMessage($"Тир сплава в чертеже ({parsedAlloy.tier}) ниже минимального ({minTier})!", true);
        }
    }

    protected override void CalculateSpecificOutputs()
    {
        float effectiveVolumeDm3 = Scaler.CalcEffectiveVolume * 1000f;
        float moduleCoeff = TierCoeffs.Get(SelectedRef.ModuleTier);

        CalcCapacity = (float)Math.Round(
            effectiveVolumeDm3 * moduleCoeff * SelectedRef.CapacityCoefficient, 3);

        CalcHeatCapacity = (float)Math.Round(
            Scaler.CalcRealVolume * SelectedRef.HeatCapacityCoeff * moduleCoeff, 1);

        int thermAbsorb = IsAlloyDecoded ? AlloyParams.thermalAbsorption : 0;
        CalcMaxTemperature = 300f + thermAbsorb;

        float surfArea = Scaler.CalcSurfaceArea;
        CalcWallThicknessMm = surfArea > 0.000001f
            ? (float)Math.Round((Scaler.CalcShellVolume / surfArea) * 1000f, 1) : 0f;
    }

    protected override string BuildSecondCodeLine()
    {
        return $"C{FormatF(CalcCapacity, 3)}";
    }

    protected override FuelTankData CreateModuleData(ModuleCraftDTO dto)
    {
        var data = new FuelTankData();
        data.Initialize(dto, CalcCapacity, CalcMaxTemperature, CalcHeatCapacity, CalcWallThicknessMm);
        return data;
    }

    protected override RuntimeModuleBase AddRuntimeComponent(GameObject obj)
    {
        return obj.AddComponent<RuntimeFuelTank>();
    }
}