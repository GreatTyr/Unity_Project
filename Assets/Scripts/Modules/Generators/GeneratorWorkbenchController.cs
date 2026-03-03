using System;
using System.Collections;
using System.Globalization;
using UnityEngine;

/// <summary>
/// Контроллер Верстака Генераторов. Отвечает только за логику (MVP/MVC паттерн).
/// Хранит состояние, проверяет ресурсы, выполняет крафт. Ничего не рисует!
/// </summary>
public class GeneratorWorkbenchController : MonoBehaviour
{
    public enum CraftPlacementMode { SpawnInWorld = 0, SaveToStorage = 1 }

    [Header("Workbench Parameters")]
    [Range(1, 10)] public int workbenchTier = 1;
    public float innerLength = 2f;
    public float innerWidth = 2f;
    public float innerHeight = 2f;

    [Header("Databases & Storages")]
    public GeneratorDatabase generatorDatabase;
    public ResourcesStorage resourcesStorage;
    public AlloyStorage alloyStorage;
    public ModuleStorage moduleStorage;

    [Header("Settings")]
    public CraftPlacementMode placementMode = CraftPlacementMode.SpawnInWorld;

    // ================= СОСТОЯНИЕ (STATE) =================
    public ModuleScaler Scaler { get; private set; } = new ModuleScaler();

    public StandardGenerator SelectedRef { get; private set; }
    public int SelectedRefIndex { get; private set; }
    public string[] RefNames { get; private set; } = new string[0];

    public float ShellPercent { get; private set; } = 5f;

    public int SelectedAlloyIndex { get; private set; }
    public string[] AlloyDisplayNames { get; private set; } = new string[0];
    public string[] AlloyCodes { get; private set; } = new string[0];
    public AlloyCode.AlloyParams AlloyParams { get; private set; }
    public bool IsAlloyDecoded { get; private set; }

    public string CurrentModuleCode { get; private set; } = "";

    // Специфичные расчеты генератора
    public float CalcSpecificPower { get; private set; }
    public float CalcFuelKgPerS { get; private set; }
    public float CalcHeatCapacity { get; private set; }
    public float CalcMaxTemperature { get; private set; }
    public float CalcWallThicknessMm { get; private set; }
    public float CalcHeatingRate { get; private set; }
    public float CalcEnergyCapacity { get; private set; } // Новая емкость!

    // Новые расчеты времени и энергии
    public float CalcCraftTimeSeconds { get; private set; }
    public long CalcEnergyCost { get; private set; }

    private float calcPowerTimesTierPer0001;
    private float calcFuelPer0001m3Tiered;

    // Сообщения для UI
    public string ErrorMessage { get; private set; } = "";
    public string SuccessMessage { get; private set; } = "";
    public string WarningMessage { get; private set; } = "";
    private float messageTimer;

    // Таймер крафта
    public bool IsCrafting { get; private set; } = false;
    public float CraftProgress { get; private set; } = 0f;

    private float InnerVolumeM3 => innerLength * innerWidth * innerHeight;

    // ================= ЖИЗНЕННЫЙ ЦИКЛ =================

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
            if (messageTimer <= 0f)
                ClearMessages();
        }
    }

    // ================= ПУБЛИЧНОЕ API (Для UI) =================

    public void SelectReference(int index)
    {
        if (generatorDatabase == null || index < 0 || index >= generatorDatabase.Count) return;
        SelectedRefIndex = index;
        SelectedRef = generatorDatabase.GetByIndex(index);

        if (SelectedRef != null)
        {
            Scaler.SetReference(
                SelectedRef.LengthMeters, SelectedRef.WidthMeters, SelectedRef.HeightMeters,
                SelectedRef.RealVolumeM3, SelectedRef.ConstantFillPercent
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
        ShellPercent = Mathf.Clamp(Mathf.RoundToInt(percent), 1, 100);
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

    // ================= ПАРСИНГ ЧЕРТЕЖА =================

    public void ApplyBlueprintCode(string code)
    {
        var parsed = BlueprintParser.ParseFirstLine(code, StandardGenerator.TYPE_GENERATOR);

        if (!parsed.IsValid)
        {
            ShowError(parsed.ErrorMessage);
            return;
        }

        if (parsed.Tier > workbenchTier)
        {
            ShowError($"Тир чертежа (T{parsed.Tier}) превышает уровень верстака (T{workbenchTier})!");
            return;
        }

        var foundRef = generatorDatabase.GetByFactionAndBlueprintID(parsed.Faction, parsed.BlueprintId);
        if (foundRef == null)
        {
            ShowError($"Эталон [{parsed.Faction}-{parsed.BlueprintId}] не найден в БД!");
            return;
        }

        SelectReference(generatorDatabase.modules.IndexOf(foundRef.gameObject));

        // Масштаб
        float sx = parsed.TargetLength / Scaler.RefLength;
        float sy = parsed.TargetWidth / Scaler.RefWidth;
        float sz = parsed.TargetHeight / Scaler.RefHeight;
        float uniformScale = (sx + sy + sz) / 3f;
        Scaler.SetScaleFactor(uniformScale);

        // Оболочка (новый или старый формат)
        if (parsed.ShellPercent > 0f)
        {
            SetShellPercent(parsed.ShellPercent);
        }
        else
        {
            SetShellPercent(5f);
        }

        // Сплав
        string[] lines = BlueprintParser.NormalizeCodeText(code).Split('\n');
        if (lines.Length >= 3)
        {
            string inputAlloy = lines[2].Trim();
            int idx = Array.IndexOf(AlloyCodes, inputAlloy);
            if (idx >= 0) SelectAlloy(idx);
            else
            {
                if (AlloyCode.Decode(inputAlloy, out var p))
                {
                    AlloyParams = p;
                    IsAlloyDecoded = true;
                    ShowMessage("Чертеж применен, но указанного сплава нет на складе!", true);
                }
            }
        }

        RecalculateAll();
    }

    // ================= ЛОГИКА РАСЧЕТОВ =================

    private void RecalculateAll()
    {
        if (SelectedRef == null) return;

        Scaler.SetAlloyTier(IsAlloyDecoded ? AlloyParams.tier : 1);
        Scaler.Recalculate();

        CalculateGeneratorSpecifics();
        CurrentModuleCode = BuildModuleCode();
    }

    private void CalculateGeneratorSpecifics()
    {
        float fillFactor = SelectedRef.ConstantFillPercent / 100f;
        float effectiveVolume = Scaler.CalcEffectiveVolume * fillFactor;
        float effectiveVolumeDm3 = effectiveVolume * 1000f;
        float moduleCoeff = TierCoeffs.Get(SelectedRef.ModuleTier);
        float wbCoeff = TierCoeffs.Get(workbenchTier);

        // Мощность
        double powerTier = (double)SelectedRef.PowerBy0001m3 * (double)moduleCoeff;
        calcPowerTimesTierPer0001 = (float)Math.Round(powerTier, 3);
        CalcSpecificPower = (float)Math.Round(powerTier * effectiveVolumeDm3, 3);

        // Топливо
        float fuelTierCoeff = TierCoeffs.Get(SelectedRef.FuelTier);
        double rawFuelPer0001D = (fuelTierCoeff > 0f) ? (double)SelectedRef.FuelBy0001m3_Base / (double)fuelTierCoeff : 0.0;
        if (rawFuelPer0001D <= 0.0) rawFuelPer0001D = 1e-6;
        calcFuelPer0001m3Tiered = (float)Math.Round(rawFuelPer0001D, 6);

        double totalFuelD = rawFuelPer0001D * effectiveVolumeDm3;
        CalcFuelKgPerS = (float)Math.Round(Math.Max(totalFuelD, 0.0001), 4);

        // Емкость
        CalcEnergyCapacity = (float)Math.Round(effectiveVolumeDm3 * moduleCoeff * SelectedRef.CapacityCoefficient, 3);

        // Тепло
        CalcHeatCapacity = (float)Math.Round(Scaler.CalcRealVolume * SelectedRef.HeatCapacityCoeff * moduleCoeff, 1);
        int thermAbsorb = IsAlloyDecoded ? AlloyParams.thermalAbsorption : 0;
        CalcMaxTemperature = 300f + thermAbsorb;

        float surfArea = Scaler.CalcSurfaceArea;
        CalcWallThicknessMm = surfArea > 0.000001f ? (float)Math.Round((Scaler.CalcShellVolume / surfArea) * 1000f, 1) : 0f;

        float thermResist = IsAlloyDecoded ? AlloyParams.thermalResistance : 0f;
        CalcHeatingRate = (float)Math.Round(SelectedRef.BaseHeating * Mathf.Max(0f, 1f - (thermResist / 100f)), 2);

        // Формула времени и энергии
        float innerVol = InnerVolumeM3 <= 0f ? 1f : InnerVolumeM3;
        CalcCraftTimeSeconds = (Scaler.CalcTotalMass * moduleCoeff * SelectedRef.CraftCoefficient) / (wbCoeff * innerVol);
        CalcEnergyCost = (long)Math.Ceiling(Scaler.CalcTotalMass * innerVol);
    }

    private string BuildModuleCode()
    {
        int tier = SelectedRef.ModuleTier;
        string faction = string.IsNullOrEmpty(SelectedRef.FactionShortName) ? "NONE" : SelectedRef.FactionShortName;
        string alloyCode = (IsAlloyDecoded && AlloyCodes.Length > 0 && SelectedAlloyIndex >= 0) ? AlloyCodes[SelectedAlloyIndex] : "NONE";

        string line1 = $"{StandardGenerator.TYPE_GENERATOR}-T{tier}" +
                       $"-m{FormatF(Scaler.CalcTotalMass, 3)}" +
                       $"-d{FormatF(Scaler.CalcDurability, 3)}" +
                       $"-{FormatF(Scaler.CalcLength, 3)}/{FormatF(Scaler.CalcWidth, 3)}/{FormatF(Scaler.CalcHeight, 3)}" +
                       $"-sp{FormatF(ShellPercent, 3)}" +
                       $"-{faction}-{SelectedRef.BlueprintId}";

        string line2 = $"P{FormatF(CalcSpecificPower, 3)}-F{FormatF(CalcFuelKgPerS, 4)}-FT{SelectedRef.FuelTier}";

        return $"{line1}\n{line2}\n{alloyCode}";
    }

    private string FormatF(float v, int dec) => v.ToString($"F{dec}", CultureInfo.InvariantCulture);

    // ================= КРАФТ (ТРАНЗАКЦИЯ) =================

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

        var metalIdx = (ResourcesStorage.ResourceIndex)(20 + SelectedRef.ModuleTier - 1);
        if (resourcesStorage.GetGrams(metalIdx) < (long)Math.Ceiling(Scaler.CalcInnerMass * 1000.0))
        { failReason = "Недостаточно металла."; return false; }

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
        long metalNeededG = (long)Math.Ceiling(Scaler.CalcInnerMass * 1000.0);
        long energyNeeded = CalcEnergyCost;
        var metalIdx = (ResourcesStorage.ResourceIndex)(20 + SelectedRef.ModuleTier - 1);

        // Списываем ресурсы в начале крафта
        alloyStorage.TryConsumeMass(alloyCode, craftShellMass);
        resourcesStorage.TryRemoveGrams(metalIdx, metalNeededG);
        resourcesStorage.TryConsumeEnergy(energyNeeded);

        // Таймер крафта
        float timer = 0f;
        while (timer < CalcCraftTimeSeconds)
        {
            timer += Time.deltaTime;
            CraftProgress = Mathf.Clamp01(timer / CalcCraftTimeSeconds);
            yield return null;
        }

        // 1. Создаем DTO (С добавлением новых галочек)
        var dto = new ModuleCraftDTO
        {
            moduleType = StandardGenerator.TYPE_GENERATOR,
            moduleTier = SelectedRef.ModuleTier,
            faction = string.IsNullOrEmpty(SelectedRef.FactionShortName) ? "NONE" : SelectedRef.FactionShortName,
            referenceIndex = SelectedRefIndex,
            referenceName = SelectedRef.gameObject.name,
            alloyCode = alloyCode,
            alloyTier = AlloyParams.tier,
            shellPercent = ShellPercent,
            scaleFactor = Scaler.CurrentScaleFactor,
            fillPercent = SelectedRef.ConstantFillPercent,
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
            isControllable = SelectedRef.IsControllable
        };

        // 2. Инициализируем данные (С добавлением температуры и емкости!)
        var genData = new GeneratorData();
        genData.Initialize(dto, CalcSpecificPower, CalcFuelKgPerS, SelectedRef.FuelTier, calcPowerTimesTierPer0001, calcFuelPer0001m3Tiered, SelectedRef.PowerBy0001m3, SelectedRef.FuelBy0001m3_Base, CalcMaxTemperature, CalcEnergyCapacity);

        // 3. Размещаем
        if (placementMode == CraftPlacementMode.SpawnInWorld)
        {
            Vector3 spawnPos = transform.position + Vector3.up * 2f;
            GameObject inst = Instantiate(SelectedRef.gameObject, spawnPos, Quaternion.identity);
            inst.name = $"Crafted_{SelectedRef.gameObject.name}_T{SelectedRef.ModuleTier}";
            inst.transform.localScale = SelectedRef.transform.localScale * Mathf.Max(0.001f, Scaler.CurrentScaleFactor);

            Destroy(inst.GetComponent<StandardGenerator>()); // Удаляем эталон
            var craftedComp = inst.AddComponent<CraftedModule>();
            craftedComp.SetData(genData);
        }
        else
        {
            if (moduleStorage != null) moduleStorage.AddModule(genData);
        }

        IsCrafting = false;
        CraftProgress = 0f;
        ShowMessage("Генератор успешно изготовлен!", false);
        RebuildAlloyList();
        RecalculateAll();
    }

    // ================= ПОМОЩНИКИ =================

    private void RebuildReferenceList()
    {
        if (generatorDatabase == null) return;
        var allRefs = generatorDatabase.GetAll();
        RefNames = new string[allRefs.Count];
        for (int i = 0; i < allRefs.Count; i++)
        {
            var sg = allRefs[i];
            string faction = string.IsNullOrEmpty(sg.FactionShortName) ? "NONE" : sg.FactionShortName;
            RefNames[i] = $"[{faction}-{sg.BlueprintId}] {sg.gameObject.name} (T{sg.ModuleTier})";
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