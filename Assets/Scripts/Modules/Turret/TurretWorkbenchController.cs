// TurretWorkbenchController.cs
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
///  онтроллер верстака турелей.
/// ѕолный аналог GeneratorWorkbenchController.
/// </summary>
public class TurretWorkbenchController : MonoBehaviour
{
    public enum CraftPlacementMode { SpawnInWorld = 0, SaveToStorage = 1 }

    private const float MessageDuration = 4f;

    // =========================================
    // INSPECTOR
    // =========================================

    [Header("Workbench Parameters")]
    [Range(1, 10)] public int workbenchTier = 1;
    public float innerLength = 3f;
    public float innerWidth = 3f;
    public float innerHeight = 3f;

    [Header("Databases & Storages")]
    public TurretDatabase database;
    public ResourcesStorage resourcesStorage;
    public AlloyStorage alloyStorage;
    public AmmoStorage ammoStorage;
    public ModuleStorage moduleStorage;

    [Header("Settings")]
    public CraftPlacementMode placementMode = CraftPlacementMode.SpawnInWorld;

    // =========================================
    // STATE Ч REFERENCE
    // =========================================

    public ModuleScaler Scaler { get; private set; } = new ModuleScaler();
    public StandardTurret SelectedRef { get; private set; }
    public int SelectedRefIndex { get; private set; }
    public string[] RefNames { get; private set; } = Array.Empty<string>();

    // =========================================
    // STATE Ч ALLOY
    // =========================================

    public int SelectedAlloyIndex { get; private set; }
    public string[] AlloyDisplayNames { get; private set; } = Array.Empty<string>();
    public string[] AlloyCodes { get; private set; } = Array.Empty<string>();
    public AlloyCode.AlloyParams AlloyParams { get; private set; }
    public bool IsAlloyDecoded { get; private set; }

    // =========================================
    // STATE Ч RECEIVER
    // =========================================

    public int LoadingPercent { get; private set; } = 33;
    public int ChamberPercent { get; private set; } = 33;

    public int CorpusTier { get; private set; } = 1;
    public int LoadingTier { get; private set; } = 1;
    public int ChamberTier { get; private set; } = 1;

    // =========================================
    // STATE Ч BARREL
    // =========================================

    public float BarrelInnerDiameterMm { get; private set; } = 100f;
    public float BarrelOuterDiameterMm { get; private set; } = 120f;
    public float BarrelLengthMm { get; private set; } = 1000f;

    // =========================================
    // STATE Ч MOUNT
    // =========================================

    public int MotorPercent { get; private set; } = 34;
    public int GyroPercent { get; private set; } = 33;

    // =========================================
    // STATE Ч PROPELLANT DEFAULTS
    // =========================================

    public int DefaultPropellantTier { get; private set; } = 1;
    public float DefaultPropellantMassKg { get; private set; } = 0.001f;

    // =========================================
    // STATE Ч AMMO PREVIEW
    // =========================================

    public string[] CompatibleAmmoCodes { get; private set; } = Array.Empty<string>();
    public string[] CompatibleAmmoNames { get; private set; } = Array.Empty<string>();
    public int SelectedAmmoIndex { get; private set; } = -1;
    public float PreviewAngleDeg { get; private set; } = 45f;

    public TurretCalculator.AmmoCompatibilityResult LastAmmoResult { get; private set; }
    public TurretCalculator.ShotPreview LastShotPreview { get; private set; }

    // =========================================
    // RESULTS
    // =========================================

    public TurretCalculator.Result CalcResult { get; private set; }
    public string CurrentModuleCode { get; private set; } = "";

    public Dictionary<ResourcesStorage.ResourceIndex, long> RequiredInternalResources { get; private set; }
        = new Dictionary<ResourcesStorage.ResourceIndex, long>();

    // =========================================
    // MESSAGES
    // =========================================

    public string ErrorMessage { get; private set; } = "";
    public string SuccessMessage { get; private set; } = "";
    public string WarningMessage { get; private set; } = "";
    private float messageTimer;

    // =========================================
    // CRAFT
    // =========================================

    public bool IsCrafting { get; private set; }
    public float CraftProgress { get; private set; }

    protected float InnerVolumeM3 => innerLength * innerWidth * innerHeight;

    // =========================================
    // UNITY
    // =========================================

    private void Update()
    {
        if (messageTimer > 0f)
        {
            messageTimer -= Time.deltaTime;
            if (messageTimer <= 0f) ClearMessages();
        }
    }

    // =========================================
    // INITIALIZATION
    // =========================================

    public void Initialize()
    {
        RebuildReferenceList();
        RebuildAlloyList();
        RebuildAmmoList();
        ResetToDefaults();
    }

    private void RebuildReferenceList()
    {
        if (database == null) return;

        var all = database.GetAll();
        RefNames = new string[all.Count];
        for (int i = 0; i < all.Count; i++)
        {
            var s = all[i];
            string faction = string.IsNullOrEmpty(s.FactionShortName) ? "NONE" : s.FactionShortName;
            RefNames[i] = $"[{faction}-{s.BlueprintId}] {s.gameObject.name} (T{s.ModuleTier})";
        }

        if (all.Count > 0) SelectReference(0);
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

    public void RebuildAmmoList()
    {
        if (ammoStorage == null)
        {
            CompatibleAmmoCodes = Array.Empty<string>();
            CompatibleAmmoNames = Array.Empty<string>();
            SelectedAmmoIndex = -1;
            return;
        }

        var entries = ammoStorage.Entries;
        var compatibleCodes = new List<string>();
        var compatibleNames = new List<string>();

        foreach (var entry in entries)
        {
            if (entry == null || string.IsNullOrEmpty(entry.ammoCode)) continue;
            if (entry.quantity <= 0) continue;

            var compatResult = TurretCalculator.CheckAmmoCompatibility(
                entry.ammoCode, CalcResult,
                BarrelInnerDiameterMm, BarrelLengthMm);

            if (compatResult.isCompatible)
            {
                compatibleCodes.Add(entry.ammoCode);
                string prefix = compatResult.isCannonball ? "[ядро]" : "[ѕатрон]";
                compatibleNames.Add(
                    $"{prefix} d={compatResult.diameterMm:F0}мм " +
                    $"m={compatResult.ammoMassKg:F3}кг " +
                    $"T{compatResult.ammoTier} " +
                    $"x{entry.quantity}");
            }
        }

        CompatibleAmmoCodes = compatibleCodes.ToArray();
        CompatibleAmmoNames = compatibleNames.ToArray();
        SelectedAmmoIndex = CompatibleAmmoCodes.Length > 0 ? 0 : -1;

        UpdateAmmoPreview();
    }

    // =========================================
    // SELECTION
    // =========================================

    public void SelectReference(int index)
    {
        if (database == null || index < 0 || index >= database.Count) return;

        SelectedRefIndex = index;
        SelectedRef = database.GetByIndex(index);

        if (SelectedRef != null)
        {
            Scaler.SetReference(
                SelectedRef.LengthMeters,
                SelectedRef.WidthMeters,
                SelectedRef.HeightMeters,
                SelectedRef.RealVolumeM3,
                SelectedRef.ConstantFillPercent);

            LoadingPercent = SelectedRef.DefaultLoadingPercent;
            ChamberPercent = SelectedRef.DefaultChamberPercent;
            CorpusTier = SelectedRef.DefaultCorpusTier;
            LoadingTier = SelectedRef.DefaultLoadingTier;
            ChamberTier = SelectedRef.DefaultChamberTier;

            MotorPercent = SelectedRef.DefaultMotorPercent;
            GyroPercent = SelectedRef.DefaultGyroPercent;

            BarrelInnerDiameterMm = SelectedRef.DefaultBarrelInnerDiameterMm;
            BarrelOuterDiameterMm = SelectedRef.DefaultBarrelOuterDiameterMm;
            BarrelLengthMm = SelectedRef.DefaultBarrelLengthMm;

            DefaultPropellantTier = SelectedRef.DefaultPropellantTier;
            DefaultPropellantMassKg = SelectedRef.DefaultPropellantMassKg;
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

    public void SelectAmmo(int index)
    {
        SelectedAmmoIndex = Mathf.Clamp(index, -1, CompatibleAmmoCodes.Length - 1);
        UpdateAmmoPreview();
    }

    public void SetPreviewAngle(float deg)
    {
        PreviewAngleDeg = Mathf.Clamp(deg, 0f, 90f);
        UpdateAmmoPreview();
    }

    // =========================================
    // SETTERS Ч RECEIVER
    // =========================================

    public void SetLoadingPercent(int v)
    {
        int maxL = 99 - Mathf.Max(ChamberPercent, TurretCalculator.MinComponentPercent);
        LoadingPercent = Mathf.Clamp(v,
            TurretCalculator.MinComponentPercent, maxL);
        RecalculateAll();
    }

    public void SetChamberPercent(int v)
    {
        int maxC = 99 - Mathf.Max(LoadingPercent, TurretCalculator.MinComponentPercent);
        ChamberPercent = Mathf.Clamp(v,
            TurretCalculator.MinComponentPercent, maxC);
        RecalculateAll();
    }

    public void SetCorpusTier(int v) { CorpusTier = Mathf.Clamp(v, 1, 10); RecalculateAll(); }
    public void SetLoadingTier(int v) { LoadingTier = Mathf.Clamp(v, 1, 10); RecalculateAll(); }
    public void SetChamberTier(int v) { ChamberTier = Mathf.Clamp(v, 1, 10); RecalculateAll(); }

    // =========================================
    // SETTERS Ч BARREL
    // =========================================

    public void SetBarrelInnerDiameter(float v)
    {
        BarrelInnerDiameterMm = Mathf.Max(1f, v);
        BarrelOuterDiameterMm = Mathf.Max(
            BarrelInnerDiameterMm + 1f,
            BarrelOuterDiameterMm);
        RecalculateAll();
    }

    public void SetBarrelOuterDiameter(float v)
    {
        BarrelOuterDiameterMm = Mathf.Max(BarrelInnerDiameterMm + 1f, v);
        RecalculateAll();
    }

    public void SetBarrelLength(float v)
    {
        BarrelLengthMm = Mathf.Max(BarrelInnerDiameterMm, v);
        RecalculateAll();
    }

    // =========================================
    // SETTERS Ч MOUNT
    // =========================================

    public void SetMotorPercent(int v)
    {
        int maxM = 99 - Mathf.Max(GyroPercent, TurretCalculator.MinComponentPercent);
        MotorPercent = Mathf.Clamp(v,
            TurretCalculator.MinComponentPercent, maxM);
        RecalculateAll();
    }

    public void SetGyroPercent(int v)
    {
        int maxG = 99 - Mathf.Max(MotorPercent, TurretCalculator.MinComponentPercent);
        GyroPercent = Mathf.Clamp(v,
            TurretCalculator.MinComponentPercent, maxG);
        RecalculateAll();
    }

    // =========================================
    // SETTERS Ч PROPELLANT
    // =========================================

    public void SetDefaultPropellantTier(int v)
    {
        DefaultPropellantTier = Mathf.Clamp(v, 1, 10);
        UpdateAmmoPreview();
    }

    public void SetDefaultPropellantMass(float v)
    {
        DefaultPropellantMassKg = Mathf.Max(0.001f, v);
        UpdateAmmoPreview();
    }

    // =========================================
    // SETTERS Ч SCALE
    // =========================================

    public void SetScaleMode(ModuleScaler.ScaleMode mode)
    {
        Scaler.SetScaleMode(mode);
        RecalculateAll();
    }

    public void HandleScaleInput(string input)
    {
        if (Scaler.HandleScaleInput(input)) RecalculateAll();
    }

    public void ResetScale()
    {
        Scaler.SetScaleFactor(1f);
        RecalculateAll();
    }

    public void ResetToDefaults()
    {
        if (SelectedRef != null) SelectReference(SelectedRefIndex);
        SelectAlloy(0);
        ResetScale();
        PreviewAngleDeg = 45f;
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

        float receiverMass = Scaler.CalcTotalMass;
        float mountMass = receiverMass * SelectedRef.MountCoeff;

        var receiverInput = new TurretCalculator.ReceiverInput
        {
            totalMassKg = receiverMass,
            corpusTier = CorpusTier,
            loadingTier = LoadingTier,
            chamberTier = ChamberTier,
            loadingPercent = LoadingPercent,
            chamberPercent = ChamberPercent
        };

        var barrelInput = new TurretCalculator.BarrelInput
        {
            innerDiameterMm = BarrelInnerDiameterMm,
            outerDiameterMm = BarrelOuterDiameterMm,
            lengthMm = BarrelLengthMm
        };

        var mountInput = new TurretCalculator.MountInput
        {
            mountTotalMass = mountMass,
            corpusTier = CorpusTier,
            motorPercent = MotorPercent,
            gyroPercent = GyroPercent
        };

        var alloyInput = new TurretCalculator.AlloyInput
        {
            hasAlloy = IsAlloyDecoded,
            tier = IsAlloyDecoded ? AlloyParams.tier : 1,
            kineticAbsorption = IsAlloyDecoded ? AlloyParams.kineticAbsorption : 0,
            kineticResistance = IsAlloyDecoded ? AlloyParams.kineticResistance : 0f
        };

        CalcResult = TurretCalculator.Calculate(
            SelectedRef, Scaler,
            receiverInput, barrelInput, mountInput, alloyInput,
            workbenchTier, InnerVolumeM3);

        CurrentModuleCode = BuildModuleCode();

        RebuildAmmoList();
    }

    private void RecalculateInternalResources()
    {
        RequiredInternalResources.Clear();
        if (SelectedRef?.InternalResourceCosts == null) return;

        float effVolLiters = Scaler.CalcEffectiveVolume * 1000f;
        foreach (var cost in SelectedRef.InternalResourceCosts)
        {
            long grams = (long)Math.Ceiling(cost.gramsPerLiter * effVolLiters);
            if (RequiredInternalResources.ContainsKey(cost.resourceType))
                RequiredInternalResources[cost.resourceType] += grams;
            else
                RequiredInternalResources[cost.resourceType] = grams;
        }
    }

    private void UpdateAmmoPreview()
    {
        if (SelectedAmmoIndex < 0 ||
            SelectedAmmoIndex >= CompatibleAmmoCodes.Length)
        {
            LastAmmoResult = default;
            LastShotPreview = default;
            return;
        }

        string code = CompatibleAmmoCodes[SelectedAmmoIndex];

        LastAmmoResult = TurretCalculator.CheckAmmoCompatibility(
            code, CalcResult,
            BarrelInnerDiameterMm, BarrelLengthMm);

        var barrelIn = new TurretCalculator.BarrelInput
        {
            innerDiameterMm = BarrelInnerDiameterMm,
            outerDiameterMm = BarrelOuterDiameterMm,
            lengthMm = BarrelLengthMm
        };

        LastShotPreview = TurretCalculator.CalculateShotPreview(
            LastAmmoResult, barrelIn, CalcResult,
            PreviewAngleDeg,
            DefaultPropellantTier, DefaultPropellantMassKg);
    }

    // =========================================
    // CODE PIPELINE
    // =========================================

    private string BuildModuleCode()
    {
        if (SelectedRef == null) return "";

        string faction = string.IsNullOrEmpty(SelectedRef.FactionShortName)
            ? "NONE"
            : SelectedRef.FactionShortName;

        string firstLine = TurretCode.BuildFirstLine(
            StandardTurret.TYPE_TURRET,
            SelectedRef.ModuleTier,
            CalcResult.totalTurretMass,
            CalcResult.totalDurability,
            Scaler.CalcLength,
            Scaler.CalcWidth,
            Scaler.CalcHeight,
            Scaler.RefFillPercent,
            Scaler.CurrentScaleFactor,
            faction,
            SelectedRef.BlueprintId);

        string receiverLine = TurretCode.BuildReceiverLine(
            LoadingPercent, LoadingTier,
            ChamberPercent, ChamberTier,
            CalcResult.corpusPercent, CorpusTier,
            SelectedRef.AmmoTierBonus,
            CalcResult.loadingPower,
            CalcResult.chamberCapacity,
            CalcResult.maxAmmoTier,
            CalcResult.receiverDurability,
            CalcResult.loadingMassKg,
            CalcResult.chamberMassKg,
            CalcResult.corpusMassKg);

        string barrelLine = TurretCode.BuildBarrelLine(
            BarrelInnerDiameterMm,
            BarrelOuterDiameterMm,
            BarrelLengthMm,
            CalcResult.barrelStrengthCoeff,
            CalcResult.barrelMassKg,
            CalcResult.barrelWallThicknessMm);

        string mountLine = TurretCode.BuildMountLine(
            CalcResult.mountTotalMass,
            MotorPercent, GyroPercent, CalcResult.compensatorPercent,
            CalcResult.motorMassKg,
            CalcResult.gyroMassKg,
            CalcResult.compensatorMassKg,
            CalcResult.aimSpeed,
            CalcResult.recoilResistance,
            CalcResult.rotationSpeed,
            SelectedRef.MaxElevationDeg,
            SelectedRef.MaxDepressionDeg,
            SelectedRef.TraverseArcDeg,
            SelectedRef.EnergyConsumption);

        string propellantLine = TurretCode.BuildPropellantLine(
            DefaultPropellantTier,
            DefaultPropellantMassKg,
            CalcResult.maxPropellantMassKg);

        string alloyCode = (IsAlloyDecoded && AlloyCodes.Length > 0)
            ? AlloyCodes[SelectedAlloyIndex]
            : "NONE";

        return TurretCode.BuildFullCode(
            firstLine, receiverLine, barrelLine,
            mountLine, propellantLine, alloyCode);
    }

    public void ApplyBlueprintCode(string code)
    {
        string norm = TurretCode.Norm(code);

        if (string.IsNullOrWhiteSpace(norm))
        {
            ShowError("ѕустой код турели.");
            return;
        }

        if (!TurretCode.TryParseFullCode(norm, out var parsed))
        {
            ShowError(parsed.ErrorMessage);
            return;
        }

        if (parsed.FirstLine.ModuleType != StandardTurret.TYPE_TURRET)
        {
            ShowError($"„ертЄж не относитс€ к модулю типа {StandardTurret.TYPE_TURRET}.");
            return;
        }

        if (parsed.FirstLine.Tier > workbenchTier)
        {
            ShowError($"“ир чертежа (T{parsed.FirstLine.Tier}) превышает тир верстака (T{workbenchTier}).");
            return;
        }

        if (database == null)
        {
            ShowError("Ѕаза турелей не назначена.");
            return;
        }

        var foundRef = database.GetByFactionAndBlueprintID(
            parsed.FirstLine.Faction, parsed.FirstLine.BlueprintId);

        if (foundRef == null)
        {
            ShowError($"Ёталон [{parsed.FirstLine.Faction}-{parsed.FirstLine.BlueprintId}] не найден в Ѕƒ.");
            return;
        }

        int refIndex = database.modules.IndexOf(foundRef.gameObject);
        if (refIndex < 0)
        {
            ShowError("Ќе удалось определить индекс эталона турели.");
            return;
        }

        SelectReference(refIndex);

        if (parsed.FirstLine.HasScaleFactor)
            Scaler.SetScaleFactor(parsed.FirstLine.ScaleFactor);

        LoadingPercent = parsed.ReceiverLine.LoadingPercent;
        ChamberPercent = parsed.ReceiverLine.ChamberPercent;
        CorpusTier = parsed.ReceiverLine.CorpusTier;
        LoadingTier = parsed.ReceiverLine.LoadingTier;
        ChamberTier = parsed.ReceiverLine.ChamberTier;

        BarrelInnerDiameterMm = parsed.BarrelLine.InnerDiameterMm;
        BarrelOuterDiameterMm = parsed.BarrelLine.OuterDiameterMm;
        BarrelLengthMm = parsed.BarrelLine.LengthMm;

        MotorPercent = parsed.MountLine.MotorPercent;
        GyroPercent = parsed.MountLine.GyroPercent;

        DefaultPropellantTier = parsed.PropellantLine.PropellantTier;
        DefaultPropellantMassKg = parsed.PropellantLine.PropellantMassKg;

        string alloyCodeFromInput = parsed.AlloyCode;
        int alloyIndex = Array.IndexOf(AlloyCodes, alloyCodeFromInput);

        if (alloyIndex >= 0)
        {
            SelectAlloy(alloyIndex);
        }
        else if (AlloyCode.Decode(alloyCodeFromInput, out var ap))
        {
            AlloyParams = ap;
            IsAlloyDecoded = true;
            ShowMessage("„ертЄж применЄн, но указанного сплава нет на складе.", true);
        }
        else
        {
            ShowError("Ќе удалось распознать код сплава турели.");
            return;
        }

        RecalculateAll();

        var verification = TurretVerifier.VerifyFullCodeAgainstCurrent(
            norm, CurrentModuleCode);

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
    // VALIDATION
    // =========================================

    public bool CanCraft(out string failReason)
    {
        failReason = "";

        if (IsCrafting) { failReason = "¬ерстак уже зан€т!"; return false; }
        if (SelectedRef == null) { failReason = "Ёталон не выбран."; return false; }
        if (alloyStorage == null || resourcesStorage == null)
        { failReason = "—клады не назначены."; return false; }

        if (Scaler.CalcLength > innerLength ||
            Scaler.CalcWidth > innerWidth ||
            Scaler.CalcHeight > innerHeight)
        {
            failReason = "√абариты превышают камеру верстака.";
            return false;
        }

        if (SelectedRef.ModuleTier > workbenchTier)
        {
            failReason = "“ир эталона выше тира верстака.";
            return false;
        }

        if (!IsAlloyDecoded)
        {
            failReason = "—плав не выбран.";
            return false;
        }

        string alloyCode = AlloyCodes.Length > 0 ? AlloyCodes[SelectedAlloyIndex] : null;
        float totalAlloNeed = CalcResult.totalTurretMass;

        if (string.IsNullOrEmpty(alloyCode) ||
            !alloyStorage.HasEnoughMass(alloyCode, totalAlloNeed))
        {
            failReason = "Ќедостаточно сплава.";
            return false;
        }

        foreach (var kvp in RequiredInternalResources)
        {
            if (resourcesStorage.GetGrams(kvp.Key) < kvp.Value)
            {
                failReason = $"Ќедостаточно: {ResourcesStorage.ResourceFullName((int)kvp.Key)}";
                return false;
            }
        }

        if (resourcesStorage.EnergyUnits < CalcResult.energyCost)
        {
            failReason = "Ќедостаточно энергии.";
            return false;
        }

        if (BarrelOuterDiameterMm <= BarrelInnerDiameterMm)
        {
            failReason = "¬нешний диаметр ствола должен быть больше внутреннего.";
            return false;
        }

        return true;
    }

    // =========================================
    // CRAFT
    // =========================================

    public void ExecuteCraft()
    {
        if (!CanCraft(out string err)) { ShowError(err); return; }
        StartCoroutine(CraftRoutine());
    }

    private IEnumerator CraftRoutine()
    {
        IsCrafting = true;
        CraftProgress = 0f;

        string alloyCode = AlloyCodes[SelectedAlloyIndex];
        float totalAlloy = CalcResult.totalTurretMass;

        if (!TryConsumeCraftCosts(alloyCode, totalAlloy, out string fail))
        {
            FinalizeCraftFailure(fail);
            yield break;
        }

        float timer = 0f;
        while (timer < CalcResult.craftTimeSeconds)
        {
            timer += Time.deltaTime;
            CraftProgress = Mathf.Clamp01(timer / CalcResult.craftTimeSeconds);
            yield return null;
        }

        var commonData = BuildCommonCraftData(alloyCode);
        var barrelIn = new TurretCalculator.BarrelInput
        {
            innerDiameterMm = BarrelInnerDiameterMm,
            outerDiameterMm = BarrelOuterDiameterMm,
            lengthMm = BarrelLengthMm
        };

        var turretData = new TurretData();
        turretData.Initialize(
            commonData, CalcResult, barrelIn,
            DefaultPropellantTier, DefaultPropellantMassKg,
            SelectedRef);

        if (!HandleCraftResult(turretData, out string resultFail))
        {
            FinalizeCraftFailure(string.IsNullOrEmpty(resultFail)
                ? "Ќе удалось выдать результат крафта."
                : resultFail);
            yield break;
        }

        FinalizeCraftSuccess();
    }

    private bool TryConsumeCraftCosts(
        string alloyCode, float alloyMass, out string failReason)
    {
        failReason = "";

        if (!alloyStorage.TryConsumeMass(alloyCode, alloyMass))
        {
            failReason = "Ќе удалось списать сплав.";
            return false;
        }

        if (!resourcesStorage.TryConsumeEnergy(CalcResult.energyCost))
        {
            failReason = "Ќе удалось списать энергию.";
            return false;
        }

        foreach (var kvp in RequiredInternalResources)
            resourcesStorage.TryRemoveGrams(kvp.Key, kvp.Value);

        return true;
    }

    private CommonModuleCraftData BuildCommonCraftData(string alloyCode)
    {
        return new CommonModuleCraftData
        {
            moduleType = StandardTurret.TYPE_TURRET,
            moduleTier = SelectedRef.ModuleTier,
            faction = string.IsNullOrEmpty(SelectedRef.FactionShortName)
                            ? "NONE" : SelectedRef.FactionShortName,
            referenceIndex = SelectedRefIndex,
            referenceName = SelectedRef.gameObject.name,

            alloyCode = alloyCode,
            alloyTier = IsAlloyDecoded ? AlloyParams.tier : 1,
            shellPercent = Scaler.RefFillPercent,
            scaleFactor = Scaler.CurrentScaleFactor,
            fillPercent = Scaler.RefFillPercent,

            length = Scaler.CalcLength,
            width = Scaler.CalcWidth,
            height = Scaler.CalcHeight,

            aabbVolume = Scaler.CalcAABBVolume,
            realVolume = Scaler.CalcRealVolume,
            shellVolumeM3 = Scaler.CalcShellVolume,
            effectiveVolume = Scaler.CalcEffectiveVolume,

            shellMassKg = CalcResult.corpusMassKg,
            innerMassKg = Scaler.CalcInnerMass,
            totalMassKg = CalcResult.totalTurretMass,
            durability = CalcResult.totalDurability,
            wallThicknessMm = Scaler.CalcWallThicknessMm,

            heatCapacity = 0f,
            maxTemperature = 0f,
            heatingRate = 0f,
            craftTimeSeconds = CalcResult.craftTimeSeconds,

            operationalResourceUsageSummary = "Ч",
            staticCapacityMax = 0f,
            staticCapacityCurrent = 0f,
            staticCapacityDrainPerSecond = 0f,

            moduleCode = CurrentModuleCode,

            canTurnOnOff = SelectedRef.CanTurnOnOff,
            turnOnOffTime = SelectedRef.TurnOnOffTime,
            canPulseMode = SelectedRef.CanPulseMode,
            pulseInterval = SelectedRef.PulseInterval,
            isControllable = SelectedRef.IsControllable,

            isVolatile = SelectedRef.IsVolatile,
            explosionDamageType = SelectedRef.ExplosionDamageType,
            explosionRadiusMeters = 0f,
            explosionPenetration = 0f,
            explosionDamage = 0f,

            buildVisualYawOffset = SelectedRef.BuildVisualYawOffset,
            buildAnchorLocal = SelectedRef.BuildAnchorLocal,
            buildAnchorCellLocal = SelectedRef.BuildAnchorCellLocal,
            referenceVisualScale = SelectedRef.transform.localScale
        };
    }

    private bool HandleCraftResult(TurretData turretData, out string failReason)
    {
        failReason = "";

        if (placementMode == CraftPlacementMode.SaveToStorage)
        {
            if (moduleStorage != null)
            {
                moduleStorage.AddModule(turretData);
                return true;
            }
            failReason = "ModuleStorage не назначен.";
            return false;
        }

        Vector3 spawnPos = transform.position + Vector3.up * 2f;
        GameObject inst = Instantiate(
            SelectedRef.gameObject, spawnPos, Quaternion.identity);

        inst.name = $"Crafted_Turret_{SelectedRef.gameObject.name}_T{SelectedRef.ModuleTier}";
        inst.transform.localScale = turretData.referenceVisualScale == Vector3.zero
            ? SelectedRef.transform.localScale * Mathf.Max(0.001f, turretData.scaleFactor)
            : turretData.referenceVisualScale * Mathf.Max(0.001f, turretData.scaleFactor);

        var stdComp = inst.GetComponent<StandardTurret>();
        if (stdComp != null) Destroy(stdComp);

        var craftedComp = inst.AddComponent<CraftedModule>();
        craftedComp.SetData(turretData);

        return true;
    }

    private void FinalizeCraftSuccess()
    {
        IsCrafting = false;
        CraftProgress = 0f;
        ShowMessage("“урель успешно изготовлена!", false);
        RebuildAlloyList();
        RecalculateAll();
    }

    private void FinalizeCraftFailure(string reason)
    {
        IsCrafting = false;
        CraftProgress = 0f;
        ShowError(reason);
        RecalculateAll();
    }

    // =========================================
    // MESSAGES
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