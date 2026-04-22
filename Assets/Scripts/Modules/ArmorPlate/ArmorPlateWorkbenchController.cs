using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

/// <summary>
/// Полностью автономный контроллер Верстака Бронеплит.
/// Не зависит от BaseModuleWorkbenchController.
/// </summary>
public class ArmorPlateWorkbenchController : MonoBehaviour
{
    public enum CraftPlacementMode
    {
        SpawnInWorld = 0,
        SaveToStorage = 1
    }

    private const float MessageDuration = 4f;

    [Header("Workbench Parameters")]
    [Range(1, 10)] public int workbenchTier = 1;
    public float innerLength = 2f;
    public float innerWidth = 2f;
    public float innerHeight = 2f;

    [Header("Databases & Storages")]
    public ArmorPlateDatabase database;
    public ResourcesStorage resourcesStorage;
    public AlloyStorage alloyStorage;
    public ModuleStorage moduleStorage;

    [Header("Settings")]
    public CraftPlacementMode placementMode = CraftPlacementMode.SpawnInWorld;

    // State
    public ArmorPlateScaler Scaler { get; private set; } = new ArmorPlateScaler();
    public StandardArmorPlate SelectedRef { get; private set; }
    public int SelectedRefIndex { get; private set; }
    public string[] RefNames { get; private set; } = Array.Empty<string>();

    public int SelectedAlloyIndex { get; private set; }
    public string[] AlloyDisplayNames { get; private set; } = Array.Empty<string>();
    public string[] AlloyCodes { get; private set; } = Array.Empty<string>();

    public AlloyCode.AlloyParams AlloyParams { get; private set; }
    public bool IsAlloyDecoded { get; private set; }

    public string CurrentModuleCode { get; private set; } = "";

    public Dictionary<ResourcesStorage.ResourceIndex, long> RequiredInternalResources { get; private set; }
        = new Dictionary<ResourcesStorage.ResourceIndex, long>();

    // Common calculated
    public float CalcCraftTimeSeconds { get; private set; }
    public long CalcEnergyCost { get; private set; }
    public float CalcExplosionRadius { get; private set; }
    public float CalcExplosionPenetration { get; private set; }
    public float CalcExplosionDamage { get; private set; }

    // Messages
    public string ErrorMessage { get; private set; } = "";
    public string SuccessMessage { get; private set; } = "";
    public string WarningMessage { get; private set; } = "";
    private float messageTimer;

    // Craft
    public bool IsCrafting { get; private set; }
    public float CraftProgress { get; private set; }

    protected float InnerVolumeM3 => innerLength * innerWidth * innerHeight;

    // ArmorPlate-specific calculated
    public float CalcDurability { get; private set; }
    public int CalcKineticAbsorption { get; private set; }
    public int CalcThermalAbsorption { get; private set; }
    public int CalcChemicalAbsorption { get; private set; }
    public int CalcEnergyAbsorption { get; private set; }
    public float CalcKineticResistance { get; private set; }
    public float CalcThermalResistance { get; private set; }
    public float CalcChemicalResistance { get; private set; }
    public float CalcEnergyResistance { get; private set; }
    public float CalcHeatCapacity { get; private set; }
    public float CalcMaxTemperature { get; private set; }
    public float CalcHeatingRate { get; private set; }
    public float CalcWallThicknessMm { get; private set; }

    // New common params (MVP)
    public string CalcOperationalResourceUsageSummary { get; private set; } = "—";
    public OperationalResourceUsagePerSecond[] CalcOperationalResourceUsagePerSecond { get; private set; }
        = Array.Empty<OperationalResourceUsagePerSecond>();

    public float CalcStaticCapacityMax { get; private set; }
    public float CalcStaticCapacityCurrent { get; private set; }
    public float CalcStaticCapacityDrainPerSecond { get; private set; }

    private void Update()
    {
        if (messageTimer > 0f)
        {
            messageTimer -= Time.deltaTime;
            if (messageTimer <= 0f)
                ClearMessages();
        }
    }

    // =========================================
    // INITIALIZATION
    // =========================================

    public void Initialize()
    {
        RebuildReferenceList();
        RebuildAlloyList();
        ResetToDefaults();
    }

    private void RebuildReferenceList()
    {
        if (database == null) return;

        var allRefs = database.GetAll();
        RefNames = new string[allRefs.Count];

        for (int i = 0; i < allRefs.Count; i++)
        {
            var s = allRefs[i];
            string faction = string.IsNullOrEmpty(s.FactionShortName) ? "NONE" : s.FactionShortName;
            RefNames[i] = $"[{faction}-{s.BlueprintId}] {s.gameObject.name} (T{s.ModuleTier})";
        }

        if (allRefs.Count > 0)
            SelectReference(0);
    }

    public void RebuildAlloyList()
    {
        if (alloyStorage == null || alloyStorage.Count == 0)
        {
            AlloyDisplayNames = Array.Empty<string>();
            AlloyCodes = Array.Empty<string>();
            IsAlloyDecoded = false;
            return;
        }

        AlloyDisplayNames = alloyStorage.GetDisplayNames();
        AlloyCodes = alloyStorage.GetAllCodes();
        SelectAlloy(0);
    }

    // =========================================
    // SELECTION
    // =========================================

    public void SelectReference(int index)
    {
        if (database == null || index < 0 || index >= database.Count)
            return;

        SelectedRefIndex = index;
        SelectedRef = database.GetByIndex(index);

        if (SelectedRef != null)
        {
            MeshFilter meshFilter = SelectedRef.GetComponent<MeshFilter>();

            Scaler.SetReference(
                SelectedRef.LengthMeters,
                SelectedRef.WidthMeters,
                SelectedRef.HeightMeters,
                SelectedRef.VolumeM3,
                meshFilter
            );

            Scaler.SetMassCoefficient(SelectedRef.MassCoefficient);
            Scaler.SetDurabilityCoefficient(SelectedRef.DurabilityCoefficient);
            Scaler.SetWallThicknessCoefficient(SelectedRef.WallThicknessCoefficient);
        }

        RecalculateAll();
    }

    public void SelectAlloy(int index)
    {
        SelectedAlloyIndex = index;
        IsAlloyDecoded = false;

        if (AlloyCodes != null && index >= 0 && index < AlloyCodes.Length)
        {
            if (AlloyCode.Decode(AlloyCodes[index], out AlloyCode.AlloyParams p))
            {
                AlloyParams = p;
                IsAlloyDecoded = true;
            }
        }

        RecalculateAll();
    }

    public void SetScaleMode(ArmorPlateScaler.ScaleMode mode)
    {
        Scaler.SetScaleMode(mode);
        RecalculateAll();
    }

    public void HandleScaleInput(string input, ArmorPlateScaler.ScaleMode mode)
    {
        Scaler.SetScaleMode(mode);
        if (Scaler.HandleScaleInput(input))
            RecalculateAll();
    }

    public void SetScaleX(float value)
    {
        Scaler.SetScaleX(value);
        RecalculateAll();
    }

    public void SetScaleY(float value)
    {
        Scaler.SetScaleY(value);
        RecalculateAll();
    }

    public void SetScaleZ(float value)
    {
        Scaler.SetScaleZ(value);
        RecalculateAll();
    }

    public void ResetScale()
    {
        Scaler.SetScaleX(1f);
        Scaler.SetScaleY(1f);
        Scaler.SetScaleZ(1f);
        RecalculateAll();
    }

    public void ResetToDefaults()
    {
        SelectAlloy(0);
        ResetScale();
        placementMode = CraftPlacementMode.SpawnInWorld;
        ClearMessages();
        RecalculateAll();
    }

    // =========================================
    // RECALCULATION
    // =========================================

    private void RecalculateAll()
    {
        if (SelectedRef == null) return;

        Scaler.SetAlloyTier(IsAlloyDecoded ? AlloyParams.tier : 1);
        Scaler.Recalculate();

        RecalculateInternalResources();
        CalculateCommonOutputs();
        CalculateSpecificOutputs();

        CurrentModuleCode = BuildModuleCode();
    }

    private void RecalculateInternalResources()
    {
        RequiredInternalResources.Clear();

        if (SelectedRef == null || SelectedRef.InternalResourceCosts == null)
            return;

        float volumeLiters = Scaler.CalcVolume * 1000f;

        foreach (var cost in SelectedRef.InternalResourceCosts)
        {
            long grams = (long)Math.Ceiling(cost.gramsPerLiter * volumeLiters);

            if (RequiredInternalResources.ContainsKey(cost.resourceType))
                RequiredInternalResources[cost.resourceType] += grams;
            else
                RequiredInternalResources[cost.resourceType] = grams;
        }
    }

    private void CalculateCommonOutputs()
    {
        float moduleCoeff = TierCoeffs.Get(SelectedRef.ModuleTier);
        float wbCoeff = TierCoeffs.Get(workbenchTier);
        float innerVol = InnerVolumeM3 <= 0f ? 1f : InnerVolumeM3;

        CalcCraftTimeSeconds = (Scaler.CalcMass * moduleCoeff * SelectedRef.CraftCoefficient) / (wbCoeff * innerVol);
        CalcEnergyCost = (long)Math.Ceiling(Scaler.CalcMass * innerVol);

        CalcExplosionRadius = SelectedRef.CalculateExplosionRadius(Scaler.CalcMass);
        CalcExplosionPenetration = SelectedRef.CalculateExplosionPenetration(
            Scaler.CalcVolume,
            Scaler.CalcMass,
            Scaler.CurrentAlloyTier);

        CalcExplosionDamage = SelectedRef.CalculateExplosionDamage(
            Scaler.CalcMass,
            Scaler.CurrentAlloyTier);
    }

    private void CalculateSpecificOutputs()
    {
        ArmorPlateCalculator.Result result = ArmorPlateCalculator.Calculate(
            SelectedRef,
            Scaler,
            IsAlloyDecoded,
            AlloyParams
        );

        CalcDurability = result.durability;

        CalcKineticAbsorption = result.kineticAbsorption;
        CalcThermalAbsorption = result.thermalAbsorption;
        CalcChemicalAbsorption = result.chemicalAbsorption;
        CalcEnergyAbsorption = result.energyAbsorption;

        CalcKineticResistance = result.kineticResistance;
        CalcThermalResistance = result.thermalResistance;
        CalcChemicalResistance = result.chemicalResistance;
        CalcEnergyResistance = result.energyResistance;

        CalcHeatCapacity = result.heatCapacity;
        CalcMaxTemperature = result.maxTemperature;
        CalcHeatingRate = result.heatingRate;
        CalcWallThicknessMm = result.wallThicknessMm;

        CalcOperationalResourceUsagePerSecond = result.operationalResourceUsagePerSecond ?? Array.Empty<OperationalResourceUsagePerSecond>();
        CalcOperationalResourceUsageSummary = string.IsNullOrEmpty(result.operationalResourceUsageSummary)
            ? "—"
            : result.operationalResourceUsageSummary;

        CalcStaticCapacityMax = result.staticCapacityMax;
        CalcStaticCapacityCurrent = result.staticCapacityCurrent;
        CalcStaticCapacityDrainPerSecond = result.staticCapacityDrainPerSecond;
    }

    // =========================================
    // CODE PIPELINE
    // =========================================

    private string BuildModuleCode()
    {
        string firstLine = ArmorPlateCode.BuildFirstLine(
            StandardArmorPlate.TYPE_ARMORPLATE,
            SelectedRef.ModuleTier,
            Scaler.CalcMass,
            CalcDurability,
            Scaler.CalcLength,
            Scaler.CalcWidth,
            Scaler.CalcHeight,
            Scaler.ScaleX,
            Scaler.ScaleY,
            Scaler.ScaleZ,
            string.IsNullOrEmpty(SelectedRef.FactionShortName) ? "NONE" : SelectedRef.FactionShortName,
            SelectedRef.BlueprintIdInt
        );

        string secondLine = ArmorPlateCode.BuildSecondLine(
            CalcDurability,
            CalcKineticAbsorption,
            CalcThermalAbsorption,
            CalcChemicalAbsorption,
            CalcEnergyAbsorption,
            CalcKineticResistance,
            CalcThermalResistance,
            CalcChemicalResistance,
            CalcEnergyResistance,
            CalcHeatCapacity,
            CalcMaxTemperature,
            CalcHeatingRate,
            CalcWallThicknessMm
        );

        string alloyCode = (IsAlloyDecoded && AlloyCodes.Length > 0 && SelectedAlloyIndex >= 0)
            ? AlloyCodes[SelectedAlloyIndex]
            : "NONE";

        return ArmorPlateCode.BuildFullCode(firstLine, secondLine, alloyCode);
    }

    public void ApplyBlueprintCode(string code)
    {
        string normalizedCode = ArmorPlateCode.NormalizeCodeText(code);

        if (string.IsNullOrWhiteSpace(normalizedCode))
        {
            ShowError("Пустой код бронеплиты.");
            return;
        }

        if (!ArmorPlateCode.TryParseFullCode(normalizedCode, out var parsed))
        {
            ShowError(parsed.ErrorMessage);
            return;
        }

        if (parsed.FirstLine.ModuleType != StandardArmorPlate.TYPE_ARMORPLATE)
        {
            ShowError($"Чертёж не относится к модулю типа {StandardArmorPlate.TYPE_ARMORPLATE}.");
            return;
        }

        if (parsed.FirstLine.Tier > workbenchTier)
        {
            ShowError($"Тир чертежа (T{parsed.FirstLine.Tier}) превышает тир верстака (T{workbenchTier}).");
            return;
        }

        if (database == null)
        {
            ShowError("База бронеплит не назначена.");
            return;
        }

        StandardArmorPlate foundRef = database.GetByFactionAndBlueprintID(parsed.FirstLine.Faction, parsed.FirstLine.BlueprintId);
        if (foundRef == null)
        {
            ShowError($"Эталон бронеплиты [{parsed.FirstLine.Faction}-{parsed.FirstLine.BlueprintId}] не найден в БД.");
            return;
        }

        int refIndex = database.modules.IndexOf(foundRef.gameObject);
        if (refIndex < 0)
        {
            ShowError("Не удалось определить индекс эталона бронеплиты в базе.");
            return;
        }

        SelectReference(refIndex);

        if (parsed.FirstLine.HasScaleFactors)
        {
            Scaler.SetScaleX(parsed.FirstLine.ScaleX);
            Scaler.SetScaleY(parsed.FirstLine.ScaleY);
            Scaler.SetScaleZ(parsed.FirstLine.ScaleZ);
        }
        else
        {
            float sx = parsed.FirstLine.Length / Scaler.RefLength;
            float sy = parsed.FirstLine.Height / Scaler.RefHeight;
            float sz = parsed.FirstLine.Width / Scaler.RefWidth;
            Scaler.SetScaleX(sx);
            Scaler.SetScaleY(sy);
            Scaler.SetScaleZ(sz);

            ShowMessage("Чертёж бронеплиты старого формата: масштаб восстановлен приблизительно.", true);
        }

        string alloyCodeFromInput = parsed.AlloyCode;
        int alloyIndex = System.Array.IndexOf(AlloyCodes, alloyCodeFromInput);

        if (alloyIndex >= 0)
        {
            SelectAlloy(alloyIndex);
        }
        else if (AlloyCode.Decode(alloyCodeFromInput, out var alloyParams))
        {
            AlloyParams = alloyParams;
            IsAlloyDecoded = true;
            ShowMessage("Чертёж применён, но указанного сплава нет на складе.", true);
        }
        else
        {
            ShowError("Не удалось распознать код сплава бронеплиты.");
            return;
        }

        RecalculateAll();

        var verification = ArmorPlateVerifier.VerifyFullCodeAgainstCurrent(
            normalizedCode,
            CurrentModuleCode
        );

        if (!verification.IsValid)
        {
            ShowError(verification.ErrorMessage);
            return;
        }

        if (verification.IsNormalized)
        {
            ShowMessage(verification.WarningMessage, true);
            return;
        }

        ShowMessage(verification.SuccessMessage, false);
    }

    // =========================================
    // COMMON CRAFT DATA
    // =========================================

    protected CommonModuleCraftData BuildCommonCraftData(string alloyCode, float totalMass)
    {
        return new CommonModuleCraftData
        {
            moduleType = StandardArmorPlate.TYPE_ARMORPLATE,
            moduleTier = SelectedRef.ModuleTier,
            faction = string.IsNullOrEmpty(SelectedRef.FactionShortName) ? "NONE" : SelectedRef.FactionShortName,
            referenceIndex = SelectedRefIndex,
            referenceName = SelectedRef.gameObject.name,

            alloyCode = alloyCode,
            alloyTier = AlloyParams.tier,
            shellPercent = 100f,

            scaleFactor = (Scaler.ScaleX + Scaler.ScaleY + Scaler.ScaleZ) / 3f,
            fillPercent = 100f,

            length = Scaler.CalcLength,
            width = Scaler.CalcWidth,
            height = Scaler.CalcHeight,

            aabbVolume = Scaler.CalcLength * Scaler.CalcWidth * Scaler.CalcHeight,
            realVolume = Scaler.CalcVolume,
            shellVolumeM3 = Scaler.CalcVolume,
            effectiveVolume = Scaler.CalcVolume,

            shellMassKg = totalMass,
            innerMassKg = 0f,
            totalMassKg = totalMass,
            durability = CalcDurability,
            wallThicknessMm = CalcWallThicknessMm,

            heatCapacity = CalcHeatCapacity,
            maxTemperature = CalcMaxTemperature,
            heatingRate = CalcHeatingRate,
            craftTimeSeconds = CalcCraftTimeSeconds,

            operationalResourceUsageSummary = CalcOperationalResourceUsageSummary,
            staticCapacityMax = CalcStaticCapacityMax,
            staticCapacityCurrent = CalcStaticCapacityCurrent,
            staticCapacityDrainPerSecond = CalcStaticCapacityDrainPerSecond,

            moduleCode = CurrentModuleCode,

            canTurnOnOff = false,
            turnOnOffTime = 0f,
            canPulseMode = false,
            pulseInterval = 0f,
            isControllable = false,

            isVolatile = SelectedRef.IsVolatile,
            explosionDamageType = SelectedRef.ExplosionDamageType,
            explosionRadiusMeters = CalcExplosionRadius,
            explosionPenetration = CalcExplosionPenetration,
            explosionDamage = CalcExplosionDamage,

            buildVisualYawOffset = SelectedRef.BuildVisualYawOffset,
            buildAnchorLocal = SelectedRef.BuildAnchorLocal,
            buildAnchorCellLocal = new Vector2Int(SelectedRef.BuildAnchorCellLocal.x, SelectedRef.BuildAnchorCellLocal.y),
            referenceVisualScale = SelectedRef.transform.localScale
        };
    }

    // =========================================
    // VALIDATION
    // =========================================

    public bool CanCraft(out string failReason)
    {
        failReason = "";

        if (IsCrafting)
        {
            failReason = "Верстак уже занят!";
            return false;
        }

        if (SelectedRef == null)
        {
            failReason = "Эталон не выбран.";
            return false;
        }

        if (alloyStorage == null || resourcesStorage == null)
        {
            failReason = "Склады не назначены.";
            return false;
        }

        if (Scaler.CalcLength > innerLength ||
            Scaler.CalcWidth > innerWidth ||
            Scaler.CalcHeight > innerHeight)
        {
            failReason = "Габариты превышают размеры камеры верстака.";
            return false;
        }

        if (SelectedRef.ModuleTier > workbenchTier)
        {
            failReason = "Тир эталона выше тира верстака.";
            return false;
        }

        if (!IsAlloyDecoded)
        {
            failReason = "Сплав не выбран.";
            return false;
        }

        string alloyCode = AlloyCodes.Length > 0 ? AlloyCodes[SelectedAlloyIndex] : null;
        if (string.IsNullOrEmpty(alloyCode) || !alloyStorage.HasEnoughMass(alloyCode, Scaler.CalcMass))
        {
            failReason = "Недостаточно сплава.";
            return false;
        }

        foreach (var kvp in RequiredInternalResources)
        {
            if (resourcesStorage.GetGrams(kvp.Key) < kvp.Value)
            {
                failReason = $"Недостаточно: {ResourcesStorage.ResourceFullName((int)kvp.Key)}";
                return false;
            }
        }

        if (resourcesStorage.EnergyUnits < CalcEnergyCost)
        {
            failReason = "Недостаточно энергии.";
            return false;
        }

        return true;
    }

    // =========================================
    // CRAFT
    // =========================================

    public void ExecuteCraft()
    {
        if (!CanCraft(out string err))
        {
            ShowError(err);
            return;
        }

        StartCoroutine(CraftRoutine());
    }

    private IEnumerator CraftRoutine()
    {
        IsCrafting = true;
        CraftProgress = 0f;

        string alloyCode = AlloyCodes[SelectedAlloyIndex];
        float craftMass = Scaler.CalcMass;

        if (!TryConsumeCraftCosts(alloyCode, craftMass, out string consumeFail))
        {
            FinalizeCraftFailure(consumeFail);
            yield break;
        }

        yield return RunCraftTimer();

        CommonModuleCraftData commonData = BuildCommonCraftData(alloyCode, craftMass);
        ArmorPlateData moduleData = CreateModuleData(commonData);

        if (moduleData == null)
        {
            FinalizeCraftFailure("Не удалось создать данные модуля.");
            yield break;
        }

        if (!HandleCraftResult(moduleData, out string resultFail))
        {
            FinalizeCraftFailure(string.IsNullOrEmpty(resultFail)
                ? "Не удалось выдать результат крафта."
                : resultFail);
            yield break;
        }

        FinalizeCraftSuccess();
    }

    private bool TryConsumeCraftCosts(string alloyCode, float totalMass, out string failReason)
    {
        failReason = "";

        if (!alloyStorage.TryConsumeMass(alloyCode, totalMass))
        {
            failReason = "Не удалось списать сплав.";
            return false;
        }

        if (!resourcesStorage.TryConsumeEnergy(CalcEnergyCost))
        {
            failReason = "Не удалось списать энергию.";
            return false;
        }

        foreach (var kvp in RequiredInternalResources)
        {
            resourcesStorage.TryRemoveGrams(kvp.Key, kvp.Value);
        }

        return true;
    }

    private IEnumerator RunCraftTimer()
    {
        float timer = 0f;
        while (timer < CalcCraftTimeSeconds)
        {
            timer += Time.deltaTime;
            CraftProgress = Mathf.Clamp01(timer / CalcCraftTimeSeconds);
            yield return null;
        }
    }

    private ArmorPlateData CreateModuleData(CommonModuleCraftData commonData)
    {
        var data = new ArmorPlateData();
        data.Initialize(
            commonData,
            CalcDurability,
            CalcKineticAbsorption,
            CalcThermalAbsorption,
            CalcChemicalAbsorption,
            CalcEnergyAbsorption,
            CalcKineticResistance,
            CalcThermalResistance,
            CalcChemicalResistance,
            CalcEnergyResistance,
            CalcWallThicknessMm
        );
        return data;
    }

    private bool HandleCraftResult(ArmorPlateData moduleData, out string failReason)
    {
        failReason = "";

        if (placementMode == CraftPlacementMode.SaveToStorage)
        {
            if (moduleStorage != null)
            {
                moduleStorage.AddModule(moduleData);
                return true;
            }

            failReason = "ModuleStorage не назначен.";
            return false;
        }

        Vector3 spawnPos = transform.position + Vector3.up * 2f;
        GameObject inst = Instantiate(SelectedRef.gameObject, spawnPos, Quaternion.identity);

        inst.name = $"Crafted_{SelectedRef.gameObject.name}_T{SelectedRef.ModuleTier}";

        Vector3 finalScale = new Vector3(
            SelectedRef.transform.localScale.x * Scaler.ScaleX,
            SelectedRef.transform.localScale.y * Scaler.ScaleY,
            SelectedRef.transform.localScale.z * Scaler.ScaleZ
        );
        inst.transform.localScale = finalScale;

        var standardComp = inst.GetComponent<StandardArmorPlate>();
        if (standardComp != null) Destroy(standardComp);

        var craftedComp = inst.AddComponent<CraftedModule>();
        craftedComp.SetData(moduleData);

        // TODO: Добавить RuntimeArmorPlate если нужен

        if (moduleData.isVolatile)
        {
            var volComp = inst.AddComponent<RuntimeVolatileModule>();
            volComp.Initialize(
                moduleData.explosionRadiusMeters,
                moduleData.explosionPenetration,
                moduleData.explosionDamage,
                moduleData.explosionDamageType,
                moduleData.totalMassKg,
                moduleData.moduleTier,
                moduleData.effectiveVolume
            );
        }

        return true;
    }

    private void FinalizeCraftSuccess()
    {
        IsCrafting = false;
        CraftProgress = 0f;

        ShowMessage("Бронеплита успешно изготовлена!", false);

        RebuildAlloyList();
        RecalculateAll();
    }

    private void FinalizeCraftFailure(string failReason)
    {
        IsCrafting = false;
        CraftProgress = 0f;
        ShowError(failReason);
        RecalculateAll();
    }

    // =========================================
    // UI / STATUS
    // =========================================

    protected void ShowError(string msg)
    {
        ErrorMessage = msg;
        SuccessMessage = "";
        WarningMessage = "";
        messageTimer = MessageDuration;
    }

    protected void ShowMessage(string msg, bool isWarning)
    {
        if (isWarning) WarningMessage = msg;
        else SuccessMessage = msg;

        ErrorMessage = "";
        messageTimer = MessageDuration;
    }

    private void ClearMessages()
    {
        ErrorMessage = "";
        SuccessMessage = "";
        WarningMessage = "";
    }
    public void ClosePanel()
    {
        // Очищаем кэш мешей при закрытии верстака
        MeshVolumeCalculator.ClearCache();
    }
}