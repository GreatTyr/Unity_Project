using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

public class EnergyStorageWorkbenchController : MonoBehaviour
{
    public enum CraftPlacementMode { SpawnInWorld = 0, SaveToStorage = 1 }

    [Header("Workbench Parameters")]
    [Range(1, 10)] public int workbenchTier = 1;
    public float innerLength = 2f;
    public float innerWidth = 2f;
    public float innerHeight = 2f;

    [Header("Databases & Storages")]
    public EnergyStorageDatabase database;
    public ResourcesStorage resourcesStorage;
    public AlloyStorage alloyStorage;
    public ModuleStorage moduleStorage;

    [Header("Settings")]
    public CraftPlacementMode placementMode = CraftPlacementMode.SpawnInWorld;

    public ModuleScaler Scaler { get; private set; } = new ModuleScaler();

    public StandardEnergyStorage SelectedRef { get; private set; }
    public int SelectedRefIndex { get; private set; }
    public string[] RefNames { get; private set; } = new string[0];

    public float ShellPercent { get; private set; } = 5f;

    public int SelectedAlloyIndex { get; private set; }
    public string[] AlloyDisplayNames { get; private set; } = new string[0];
    public string[] AlloyCodes { get; private set; } = new string[0];
    public AlloyCode.AlloyParams AlloyParams { get; private set; }
    public bool IsAlloyDecoded { get; private set; }

    public string CurrentModuleCode { get; private set; } = "";

    // НОВОЕ: Словарь требуемых ресурсов за литр
    public Dictionary<ResourcesStorage.ResourceIndex, long> RequiredInternalResources { get; private set; } = new Dictionary<ResourcesStorage.ResourceIndex, long>();

    public float CalcEnergyCapacity { get; private set; }

    public float CalcCraftTimeSeconds { get; private set; }
    public long CalcEnergyCost { get; private set; }

    public string ErrorMessage { get; private set; } = "";
    public string SuccessMessage { get; private set; } = "";
    public string WarningMessage { get; private set; } = "";
    private float messageTimer;

    public bool IsCrafting { get; private set; } = false;
    public float CraftProgress { get; private set; } = 0f;

    private float InnerVolumeM3 => innerLength * innerWidth * innerHeight;

    public void Initialize()
    {
        RebuildReferenceList();
        RebuildAlloyList();
        ResetToDefaults();
    }

    private void Update()
    {
        if (messageTimer > 0f)
        {
            messageTimer -= Time.deltaTime;
            if (messageTimer <= 0f) ClearMessages();
        }
    }

    public void SelectReference(int index)
    {
        if (database == null || index < 0 || index >= database.Count) return;
        SelectedRefIndex = index;
        SelectedRef = database.GetByIndex(index);

        if (SelectedRef != null)
        {
            // НОВОЕ: Считаем суммарные граммы на литр для Scaler
            float totalGramsPerLiter = 0f;
            if (SelectedRef.InternalResourceCosts != null)
            {
                foreach (var cost in SelectedRef.InternalResourceCosts)
                    totalGramsPerLiter += cost.gramsPerLiter;
            }

            Scaler.SetReference(
                SelectedRef.LengthMeters, SelectedRef.WidthMeters, SelectedRef.HeightMeters,
                SelectedRef.RealVolumeM3, totalGramsPerLiter
            );
        }
        RecalculateAll();
    }

    public void SelectAlloy(int index)
    {
        SelectedAlloyIndex = index;
        IsAlloyDecoded = false;
        if (AlloyCodes != null && index >= 0 && index < AlloyCodes.Length)
        {
            if (AlloyCode.Decode(AlloyCodes[index], out var p))
            {
                AlloyParams = p;
                IsAlloyDecoded = true;
            }
        }
        RecalculateAll();
    }

    public void SetShellPercent(float percent)
    {
        ShellPercent = Mathf.Clamp(Mathf.RoundToInt(percent), 1, 100);
        Scaler.SetShellPercent(ShellPercent);
        RecalculateAll();
    }

    public void SetScaleMode(ModuleScaler.ScaleMode mode) { Scaler.SetScaleMode(mode); RecalculateAll(); }
    public void HandleScaleInput(string input) { if (Scaler.HandleScaleInput(input)) RecalculateAll(); }
    public void ResetScale() { Scaler.SetScaleFactor(1f); RecalculateAll(); }

    public void ResetToDefaults()
    {
        SetShellPercent(5f);
        SelectAlloy(0);
        ResetScale();
        placementMode = CraftPlacementMode.SpawnInWorld;
        ClearMessages();
        RecalculateAll();
    }

    public void ApplyBlueprintCode(string code)
    {
        var parsed = BlueprintParser.ParseFirstLine(code, StandardEnergyStorage.TYPE_ENERGY_STORAGE);

        if (!parsed.IsValid) { ShowError(parsed.ErrorMessage); return; }
        if (parsed.Tier > workbenchTier) { ShowError($"Тир чертежа (T{parsed.Tier}) выше уровня верстака (T{workbenchTier})!"); return; }

        var foundRef = database.GetByFactionAndBlueprintID(parsed.Faction, parsed.BlueprintId);
        if (foundRef == null) { ShowError($"Эталон [{parsed.Faction}-{parsed.BlueprintId}] не найден в БД!"); return; }

        SelectReference(database.modules.IndexOf(foundRef.gameObject));

        float sx = parsed.TargetLength / Scaler.RefLength;
        float sy = parsed.TargetWidth / Scaler.RefWidth;
        float sz = parsed.TargetHeight / Scaler.RefHeight;
        Scaler.SetScaleFactor((sx + sy + sz) / 3f);

        if (parsed.ShellPercent > 0f) SetShellPercent(parsed.ShellPercent);
        else SetShellPercent(5f);

        string[] lines = BlueprintParser.NormalizeCodeText(code).Split('\n');
        if (lines.Length >= 3)
        {
            string inputAlloy = lines[2].Trim();
            int idx = Array.IndexOf(AlloyCodes, inputAlloy);
            if (idx >= 0) SelectAlloy(idx);
            else if (AlloyCode.Decode(inputAlloy, out var p))
            {
                AlloyParams = p;
                IsAlloyDecoded = true;
                ShowMessage("Чертеж применен, но указанного сплава нет на складе!", true);
            }
        }
        RecalculateAll();
    }

    private void RecalculateAll()
    {
        if (SelectedRef == null) return;

        Scaler.SetAlloyTier(IsAlloyDecoded ? AlloyParams.tier : 1);
        Scaler.Recalculate();

        // НОВОЕ: Заполняем словарь требуемых ресурсов
        RequiredInternalResources.Clear();
        if (SelectedRef.InternalResourceCosts != null)
        {
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

        // Внимание: мы убрали ConstantFillPercent, поэтому емкость теперь просто от эффективного объема
        float effectiveVolumeDm3 = Scaler.CalcEffectiveVolume * 1000f;
        float modCoeff = TierCoeffs.Get(SelectedRef.ModuleTier);
        float wbCoeff = TierCoeffs.Get(workbenchTier);

        CalcEnergyCapacity = (float)Math.Round(effectiveVolumeDm3 * modCoeff * SelectedRef.CapacityCoefficient, 3);

        float innerVol = InnerVolumeM3 <= 0f ? 1f : InnerVolumeM3;
        CalcCraftTimeSeconds = (Scaler.CalcTotalMass * modCoeff * SelectedRef.CraftCoefficient) / (wbCoeff * innerVol);
        CalcEnergyCost = (long)Math.Ceiling(Scaler.CalcTotalMass * innerVol);

        CurrentModuleCode = BuildModuleCode();
    }

    private string BuildModuleCode()
    {
        string faction = string.IsNullOrEmpty(SelectedRef.FactionShortName) ? "NONE" : SelectedRef.FactionShortName;
        string alloyCode = (IsAlloyDecoded && AlloyCodes.Length > 0 && SelectedAlloyIndex >= 0) ? AlloyCodes[SelectedAlloyIndex] : "NONE";

        string line1 = $"{StandardEnergyStorage.TYPE_ENERGY_STORAGE}-T{SelectedRef.ModuleTier}" +
                       $"-m{FormatF(Scaler.CalcTotalMass, 3)}" +
                       $"-d{FormatF(Scaler.CalcDurability, 3)}" +
                       $"-{FormatF(Scaler.CalcLength, 3)}/{FormatF(Scaler.CalcWidth, 3)}/{FormatF(Scaler.CalcHeight, 3)}" +
                       $"-sp{FormatF(ShellPercent, 3)}" +
                       $"-{faction}-{SelectedRef.BlueprintId}";

        string line2 = $"C{FormatF(CalcEnergyCapacity, 3)}";

        return $"{line1}\n{line2}\n{alloyCode}";
    }

    private string FormatF(float v, int dec) => v.ToString($"F{dec}", CultureInfo.InvariantCulture);

    public bool CanCraft(out string failReason)
    {
        failReason = "";
        if (IsCrafting) { failReason = "Верстак уже занят!"; return false; }
        if (SelectedRef == null) { failReason = "Эталон не выбран."; return false; }
        if (alloyStorage == null || resourcesStorage == null) { failReason = "Склады не назначены."; return false; }

        if (Scaler.CalcLength > innerLength || Scaler.CalcWidth > innerWidth || Scaler.CalcHeight > innerHeight)
        { failReason = "Габариты превышают размеры камеры верстака."; return false; }

        if (SelectedRef.ModuleTier > workbenchTier) { failReason = "Тир эталона выше тира верстака."; return false; }

        string alloyCode = AlloyCodes.Length > 0 ? AlloyCodes[SelectedAlloyIndex] : null;
        if (string.IsNullOrEmpty(alloyCode) || !alloyStorage.HasEnoughMass(alloyCode, Scaler.CalcShellMass))
        { failReason = "Недостаточно сплава."; return false; }

        // НОВОЕ: Проверка по словарю ресурсов
        foreach (var kvp in RequiredInternalResources)
        {
            if (resourcesStorage.GetGrams(kvp.Key) < kvp.Value)
            {
                failReason = $"Недостаточно: {ResourcesStorage.ResourceFullName((int)kvp.Key)}";
                return false;
            }
        }

        if (resourcesStorage.EnergyUnits < CalcEnergyCost)
        { failReason = "Недостаточно энергии."; return false; }

        return true;
    }

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
        long energyNeeded = CalcEnergyCost;

        // Списываем сплав и энергию
        alloyStorage.TryConsumeMass(alloyCode, craftShellMass);
        resourcesStorage.TryConsumeEnergy(energyNeeded);

        // НОВОЕ: Списание ресурсов по словарю
        foreach (var kvp in RequiredInternalResources)
        {
            resourcesStorage.TryRemoveGrams(kvp.Key, kvp.Value);
        }

        float timer = 0f;
        while (timer < CalcCraftTimeSeconds)
        {
            timer += Time.deltaTime;
            CraftProgress = Mathf.Clamp01(timer / CalcCraftTimeSeconds);
            yield return null;
        }

        var dto = new ModuleCraftDTO
        {
            moduleType = StandardEnergyStorage.TYPE_ENERGY_STORAGE,
            moduleTier = SelectedRef.ModuleTier,
            faction = string.IsNullOrEmpty(SelectedRef.FactionShortName) ? "NONE" : SelectedRef.FactionShortName,
            referenceIndex = SelectedRefIndex,
            referenceName = SelectedRef.gameObject.name,
            alloyCode = alloyCode,
            alloyTier = AlloyParams.tier,
            shellPercent = ShellPercent,
            scaleFactor = Scaler.CurrentScaleFactor,
            fillPercent = 0f,
            length = Scaler.CalcLength,
            width = Scaler.CalcWidth,
            height = Scaler.CalcHeight,
            aabbVolume = Scaler.CalcAABBVolume,
            realVolume = Scaler.CalcRealVolume,
            shellVolumeM3 = Scaler.CalcShellVolume,
            effectiveVolume = Scaler.CalcEffectiveVolume,
            shellMassKg = craftShellMass,
            innerMassKg = Scaler.CalcInnerMass,
            totalMassKg = Scaler.CalcTotalMass,
            durability = Scaler.CalcDurability,
            moduleCode = CurrentModuleCode,

            canTurnOnOff = SelectedRef.CanTurnOnOff,
            turnOnOffTime = SelectedRef.TurnOnOffTime,
            canPulseMode = SelectedRef.CanPulseMode,
            pulseInterval = SelectedRef.PulseInterval,
            isControllable = SelectedRef.IsControllable,

            // НОВЫЕ ПАРАМЕТРЫ ВОЛАТИЛЬНОСТИ
            isVolatile = SelectedRef.IsVolatile,
            explosionDamageType = SelectedRef.ExplosionDamageType
        };

        var data = new EnergyStorageData();
        data.Initialize(dto, CalcEnergyCapacity);

        if (placementMode == CraftPlacementMode.SpawnInWorld)
        {
            Vector3 spawnPos = transform.position + Vector3.up * 2f;
            GameObject inst = Instantiate(SelectedRef.gameObject, spawnPos, Quaternion.identity);
            inst.name = $"Crafted_{SelectedRef.gameObject.name}_T{SelectedRef.ModuleTier}";
            inst.transform.localScale = SelectedRef.transform.localScale * Mathf.Max(0.001f, Scaler.CurrentScaleFactor);

            Destroy(inst.GetComponent<StandardEnergyStorage>());
            inst.AddComponent<CraftedModule>().SetData(data);

            // НОВАЯ ЛОГИКА ВЗРЫВА ПРИ СПАВНЕ
            if (SelectedRef.IsVolatile)
            {
                var volComp = inst.AddComponent<RuntimeVolatileModule>();
                volComp.Initialize(Scaler.CalcTotalMass, SelectedRef.ModuleTier, Scaler.CalcEffectiveVolume, SelectedRef.ExplosionDamageType);
            }
        }
        else
        {
            if (moduleStorage != null) moduleStorage.AddModule(data);
        }

        IsCrafting = false;
        CraftProgress = 0f;
        ShowMessage("Батарея успешно изготовлена!", false);
        RebuildAlloyList();
        RecalculateAll();
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
        if (allRefs.Count > 0) SelectReference(0);
    }

    public void RebuildAlloyList()
    {
        if (alloyStorage == null || alloyStorage.Count == 0)
        {
            AlloyDisplayNames = new string[0];
            AlloyCodes = new string[0];
            IsAlloyDecoded = false;
            return;
        }
        AlloyDisplayNames = alloyStorage.GetDisplayNames();
        AlloyCodes = alloyStorage.GetAllCodes();
        SelectAlloy(0);
    }

    private void ShowError(string msg) { ErrorMessage = msg; SuccessMessage = ""; WarningMessage = ""; messageTimer = 4f; }
    private void ShowMessage(string msg, bool isWarning) { if (isWarning) WarningMessage = msg; else SuccessMessage = msg; ErrorMessage = ""; messageTimer = 4f; }
    private void ClearMessages() { ErrorMessage = ""; SuccessMessage = ""; WarningMessage = ""; }
}