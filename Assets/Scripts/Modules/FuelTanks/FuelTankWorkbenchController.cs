using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

/// <summary>
/// Полностью автономный контроллер Верстака Топливных Баков.
/// Не зависит от BaseModuleWorkbenchController.
/// FuelTank — special-case модуль:
/// - fillPercent = 0
/// - нет внутреннего resource-pipeline как у обычных модулей
/// - alloy tier должен быть >= tier эталона
/// </summary>
public class FuelTankWorkbenchController : MonoBehaviour
{
    public enum CraftPlacementMode
    {
        SpawnInWorld = 0,
        SaveToStorage = 1
    }

    private const int MinShellPercent = 1;
    private const int MaxShellPercent = 99;
    private const float MessageDuration = 4f;

    [Header("Workbench Parameters")]
    [Range(1, 10)] public int workbenchTier = 1;
    public float innerLength = 2f;
    public float innerWidth = 2f;
    public float innerHeight = 2f;

    [Header("Databases & Storages")]
    public FuelTankDatabase database;
    public ResourcesStorage resourcesStorage;
    public AlloyStorage alloyStorage;
    public ModuleStorage moduleStorage;

    [Header("Settings")]
    public CraftPlacementMode placementMode = CraftPlacementMode.SpawnInWorld;

    // State
    public ModuleScaler Scaler { get; private set; } = new ModuleScaler();
    public StandardFuelTank SelectedRef { get; private set; }
    public int SelectedRefIndex { get; private set; }
    public string[] RefNames { get; private set; } = Array.Empty<string>();

    public float ShellPercent { get; private set; } = 5f;

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

    // Specific calculated
    public float CalcCapacity { get; private set; }
    public float CalcHeatCapacity { get; private set; }
    public float CalcMaxTemperature { get; private set; }
    public float CalcHeatingRate { get; private set; }

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

        int minTier = SelectedRef != null ? SelectedRef.ModuleTier : 1;
        string[] allCodes = alloyStorage.GetAllCodes();
        string[] allNames = alloyStorage.GetDisplayNames();

        List<string> filteredCodes = new List<string>();
        List<string> filteredNames = new List<string>();

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
            // Бак не использует FillFactor как обычный модуль
            float fillPercent = 0f;

            Scaler.SetReference(
                SelectedRef.LengthMeters,
                SelectedRef.WidthMeters,
                SelectedRef.HeightMeters,
                SelectedRef.RealVolumeM3,
                fillPercent
            );
        }

        RebuildAlloyList();
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

    public void SetShellPercent(float percent)
    {
        ShellPercent = Mathf.Clamp(Mathf.RoundToInt(percent), MinShellPercent, MaxShellPercent);
        Scaler.SetShellPercent(ShellPercent);
        RecalculateAll();
    }

    public void SetScaleMode(ModuleScaler.ScaleMode mode)
    {
        Scaler.SetScaleMode(mode);
        RecalculateAll();
    }

    public void HandleScaleInput(string input)
    {
        if (Scaler.HandleScaleInput(input))
            RecalculateAll();
    }

    public void ResetScale()
    {
        Scaler.SetScaleFactor(1f);
        RecalculateAll();
    }

    public void ResetToDefaults()
    {
        SetShellPercent(5f);
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
        // Бак не использует внутренние ресурсы как обычный модуль
        RequiredInternalResources.Clear();
    }

    private void CalculateCommonOutputs()
    {
        float moduleCoeff = TierCoeffs.Get(SelectedRef.ModuleTier);
        float wbCoeff = TierCoeffs.Get(workbenchTier);
        float innerVol = InnerVolumeM3 <= 0f ? 1f : InnerVolumeM3;

        CalcCraftTimeSeconds = (Scaler.CalcTotalMass * moduleCoeff * SelectedRef.CraftCoefficient) / (wbCoeff * innerVol);
        CalcEnergyCost = (long)Math.Ceiling(Scaler.CalcTotalMass * innerVol);

        // У бака нет meaningful explosion power source
        CalcExplosionRadius = 0f;

        CalcExplosionPenetration = SelectedRef.CalculateExplosionPenetration(
            Scaler.CalcEffectiveVolume,
            Scaler.CalcShellMass,
            Scaler.CurrentAlloyTier);

        CalcExplosionDamage = SelectedRef.CalculateExplosionDamage(
            Scaler.CalcShellMass,
            Scaler.CurrentAlloyTier);
    }

    private void CalculateSpecificOutputs()
    {
        FuelTankCalculator.Result result = FuelTankCalculator.Calculate(
            SelectedRef,
            Scaler,
            IsAlloyDecoded,
            AlloyParams
        );

        CalcCapacity = result.capacity;
        CalcHeatCapacity = result.heatCapacity;
        CalcMaxTemperature = result.maxTemperature;
        CalcHeatingRate = result.heatingRate;

        CalcOperationalResourceUsagePerSecond = result.operationalResourceUsagePerSecond ?? Array.Empty<OperationalResourceUsagePerSecond>();
        CalcOperationalResourceUsageSummary = string.IsNullOrEmpty(result.operationalResourceUsageSummary)
            ? "—"
            : result.operationalResourceUsageSummary;

        CalcStaticCapacityMax = result.staticCapacityMax;
        CalcStaticCapacityCurrent = result.staticCapacityCurrent;
        CalcStaticCapacityDrainPerSecond = result.staticCapacityDrainPerSecond;
    }

    // =========================================
    // CODE PIPELINE (FULL VARIANT A)
    // =========================================

    private string BuildModuleCode()
    {
        string firstLine = FuelTankCode.BuildFirstLine(
            StandardFuelTank.TYPE_FUELTANK,
            SelectedRef.ModuleTier,
            Scaler.CalcTotalMass,
            Scaler.CalcDurability,
            Scaler.CalcLength,
            Scaler.CalcWidth,
            Scaler.CalcHeight,
            ShellPercent,
            Scaler.CurrentScaleFactor,
            string.IsNullOrEmpty(SelectedRef.FactionShortName) ? "NONE" : SelectedRef.FactionShortName,
            SelectedRef.BlueprintId
        );

        string secondLine = FuelTankCode.BuildSecondLine(CalcCapacity);

        string alloyCode = (IsAlloyDecoded && AlloyCodes.Length > 0 && SelectedAlloyIndex >= 0)
            ? AlloyCodes[SelectedAlloyIndex]
            : "NONE";

        return FuelTankCode.BuildFullCode(firstLine, secondLine, alloyCode);
    }

    public void ApplyBlueprintCode(string code)
    {
        string normalizedCode = FuelTankCode.NormalizeCodeText(code);

        if (string.IsNullOrWhiteSpace(normalizedCode))
        {
            ShowError("Пустой код бака.");
            return;
        }

        if (!FuelTankCode.TryParseFullCode(normalizedCode, out var parsed))
        {
            ShowError(parsed.ErrorMessage);
            return;
        }

        if (parsed.FirstLine.ModuleType != StandardFuelTank.TYPE_FUELTANK)
        {
            ShowError($"Чертёж не относится к модулю типа {StandardFuelTank.TYPE_FUELTANK}.");
            return;
        }

        if (parsed.FirstLine.Tier > workbenchTier)
        {
            ShowError($"Тир чертежа (T{parsed.FirstLine.Tier}) превышает тир верстака (T{workbenchTier}).");
            return;
        }

        if (database == null)
        {
            ShowError("База баков не назначена.");
            return;
        }

        StandardFuelTank foundRef = database.GetByFactionAndBlueprintID(parsed.FirstLine.Faction, parsed.FirstLine.BlueprintId);
        if (foundRef == null)
        {
            ShowError($"Эталон бака [{parsed.FirstLine.Faction}-{parsed.FirstLine.BlueprintId}] не найден в БД.");
            return;
        }

        int refIndex = database.modules.IndexOf(foundRef.gameObject);
        if (refIndex < 0)
        {
            ShowError("Не удалось определить индекс эталона бака в базе.");
            return;
        }

        SelectReference(refIndex);

        if (parsed.FirstLine.HasScaleFactor)
        {
            Scaler.SetScaleFactor(parsed.FirstLine.ScaleFactor);
        }
        else
        {
            float sx = parsed.FirstLine.Length / Scaler.RefLength;
            float sy = parsed.FirstLine.Width / Scaler.RefWidth;
            float sz = parsed.FirstLine.Height / Scaler.RefHeight;
            Scaler.SetScaleFactor((sx + sy + sz) / 3f);

            ShowMessage("Чертёж бака старого формата: масштаб восстановлен приблизительно.", true);
        }

        SetShellPercent(parsed.FirstLine.ShellPercent);

        string alloyCodeFromInput = parsed.AlloyCode;
        int alloyIndex = Array.IndexOf(AlloyCodes, alloyCodeFromInput);

        if (alloyIndex >= 0)
        {
            SelectAlloy(alloyIndex);
        }
        else if (AlloyCode.Decode(alloyCodeFromInput, out var alloyParams))
        {
            AlloyParams = alloyParams;
            IsAlloyDecoded = true;

            int minTier = SelectedRef != null ? SelectedRef.ModuleTier : 1;
            if (alloyParams.tier < minTier)
            {
                ShowMessage($"Тир сплава в чертеже ({alloyParams.tier}) ниже минимального ({minTier})!", true);
            }
            else
            {
                ShowMessage("Чертёж применён, но указанного сплава нет на складе.", true);
            }
        }
        else
        {
            ShowError("Не удалось распознать код сплава бака.");
            return;
        }

        RecalculateAll();

        var verification = FuelTankVerifier.VerifyFullCodeAgainstCurrent(
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

    protected CommonModuleCraftData BuildCommonCraftData(string alloyCode, float shellMass)
    {
        return new CommonModuleCraftData
        {
            moduleType = StandardFuelTank.TYPE_FUELTANK,
            moduleTier = SelectedRef.ModuleTier,
            faction = string.IsNullOrEmpty(SelectedRef.FactionShortName) ? "NONE" : SelectedRef.FactionShortName,
            referenceIndex = SelectedRefIndex,
            referenceName = SelectedRef.gameObject.name,

            alloyCode = alloyCode,
            alloyTier = AlloyParams.tier,
            shellPercent = ShellPercent,

            scaleFactor = Scaler.CurrentScaleFactor,
            fillPercent = Scaler.RefFillPercent,

            length = Scaler.CalcLength,
            width = Scaler.CalcWidth,
            height = Scaler.CalcHeight,

            aabbVolume = Scaler.CalcAABBVolume,
            realVolume = Scaler.CalcRealVolume,
            shellVolumeM3 = Scaler.CalcShellVolume,
            effectiveVolume = Scaler.CalcEffectiveVolume,

            shellMassKg = shellMass,
            innerMassKg = Scaler.CalcInnerMass,
            totalMassKg = Scaler.CalcTotalMass,
            durability = Scaler.CalcDurability,
            wallThicknessMm = Scaler.CalcWallThicknessMm,

            heatCapacity = CalcHeatCapacity,
            maxTemperature = CalcMaxTemperature,
            heatingRate = CalcHeatingRate,
            craftTimeSeconds = CalcCraftTimeSeconds,

            operationalResourceUsageSummary = CalcOperationalResourceUsageSummary,
            staticCapacityMax = CalcStaticCapacityMax,
            staticCapacityCurrent = CalcStaticCapacityCurrent,
            staticCapacityDrainPerSecond = CalcStaticCapacityDrainPerSecond,

            moduleCode = CurrentModuleCode,

            canTurnOnOff = SelectedRef.CanTurnOnOff,
            turnOnOffTime = SelectedRef.TurnOnOffTime,
            canPulseMode = SelectedRef.CanPulseMode,
            pulseInterval = SelectedRef.PulseInterval,
            isControllable = SelectedRef.IsControllable,

            isVolatile = SelectedRef.IsVolatile,
            explosionDamageType = SelectedRef.ExplosionDamageType,
            explosionRadiusMeters = CalcExplosionRadius,
            explosionPenetration = CalcExplosionPenetration,
            explosionDamage = CalcExplosionDamage,

            buildVisualYawOffset = SelectedRef.BuildVisualYawOffset,
            buildAnchorLocal = SelectedRef.BuildAnchorLocal,
            buildAnchorCellLocal = SelectedRef.BuildAnchorCellLocal,
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

        if (AlloyParams.tier < SelectedRef.ModuleTier)
        {
            failReason = $"Тир сплава ({AlloyParams.tier}) ниже тира эталона ({SelectedRef.ModuleTier}).";
            return false;
        }

        string alloyCode = AlloyCodes.Length > 0 ? AlloyCodes[SelectedAlloyIndex] : null;
        if (string.IsNullOrEmpty(alloyCode) || !alloyStorage.HasEnoughMass(alloyCode, Scaler.CalcShellMass))
        {
            failReason = "Недостаточно сплава.";
            return false;
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
        float craftShellMass = Scaler.CalcShellMass;

        if (!TryConsumeCraftCosts(alloyCode, craftShellMass, out string consumeFail))
        {
            FinalizeCraftFailure(consumeFail);
            yield break;
        }

        yield return RunCraftTimer();

        CommonModuleCraftData commonData = BuildCommonCraftData(alloyCode, craftShellMass);
        FuelTankData moduleData = CreateModuleData(commonData);

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

    private bool TryConsumeCraftCosts(string alloyCode, float shellMass, out string failReason)
    {
        failReason = "";

        if (!alloyStorage.TryConsumeMass(alloyCode, shellMass))
        {
            failReason = "Не удалось списать сплав.";
            return false;
        }

        if (!resourcesStorage.TryConsumeEnergy(CalcEnergyCost))
        {
            failReason = "Не удалось списать энергию.";
            return false;
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

    private FuelTankData CreateModuleData(CommonModuleCraftData commonData)
    {
        var data = new FuelTankData();
        data.Initialize(
            commonData,
            CalcCapacity
        );
        return data;
    }

    private bool HandleCraftResult(FuelTankData moduleData, out string failReason)
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
        inst.transform.localScale = moduleData.referenceVisualScale == Vector3.zero
            ? SelectedRef.transform.localScale * Mathf.Max(0.001f, moduleData.scaleFactor)
            : moduleData.referenceVisualScale * Mathf.Max(0.001f, moduleData.scaleFactor);

        var standardComp = inst.GetComponent<StandardFuelTank>();
        if (standardComp != null) Destroy(standardComp);

        var craftedComp = inst.AddComponent<CraftedModule>();
        craftedComp.SetData(moduleData);

        inst.AddComponent<RuntimeFuelTank>();

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

        ShowMessage("Топливный бак успешно изготовлен!", false);

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
}