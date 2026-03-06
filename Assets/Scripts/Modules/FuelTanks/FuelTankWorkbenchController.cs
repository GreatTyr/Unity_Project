using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

/// <summary>
/// Контроллер Верстака Топливных Баков. Отвечает только за логику (MVP/MVC паттерн).
/// Бак состоит только из оболочки (стенки). Полость — пространство для топлива.
/// Сердечника нет, innerMass = 0, металл тира модуля не требуется.
/// Минимальный тир сплава оболочки = тир эталона.
/// </summary>
public class FuelTankWorkbenchController : MonoBehaviour
{
    public enum CraftPlacementMode { SpawnInWorld = 0, SaveToStorage = 1 }

    [Header("Workbench Parameters")]
    [Range(1, 10)] public int workbenchTier = 1;
    public float innerLength = 2f;
    public float innerWidth = 2f;
    public float innerHeight = 2f;

    [Header("Databases & Storages")]
    public FuelTankDatabase fuelTankDatabase;
    public ResourcesStorage resourcesStorage;
    public AlloyStorage alloyStorage;
    public ModuleStorage moduleStorage;

    [Header("Settings")]
    public CraftPlacementMode placementMode = CraftPlacementMode.SpawnInWorld;

    // ================= СОСТОЯНИЕ (STATE) =================
    public ModuleScaler Scaler { get; private set; } = new ModuleScaler();

    public StandardFuelTank SelectedRef { get; private set; }
    public int SelectedRefIndex { get; private set; }
    public string[] RefNames { get; private set; } = new string[0];

    public float ShellPercent { get; private set; } = 5f;

    // Фильтрованные списки сплавов (только тир >= тир эталона)
    public int SelectedAlloyIndex { get; private set; }
    public string[] AlloyDisplayNames { get; private set; } = new string[0];
    public string[] AlloyCodes { get; private set; } = new string[0];
    public AlloyCode.AlloyParams AlloyParams { get; private set; }
    public bool IsAlloyDecoded { get; private set; }

    public string CurrentModuleCode { get; private set; } = "";

    // Специфичные расчеты топливного бака
    public float CalcCapacity { get; private set; }
    public float CalcHeatCapacity { get; private set; }
    public float CalcMaxTemperature { get; private set; }
    public float CalcWallThicknessMm { get; private set; }

    // Время и энергия крафта
    public float CalcCraftTimeSeconds { get; private set; }
    public long CalcEnergyCost { get; private set; }

    // НОВОЕ: Расчеты взрыва
    public float CalcExplosionRadius { get; private set; }
    public float CalcExplosionPenetration { get; private set; }
    public float CalcExplosionDamage { get; private set; }

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
        if (fuelTankDatabase == null || index < 0 || index >= fuelTankDatabase.Count) return;
        SelectedRefIndex = index;
        SelectedRef = fuelTankDatabase.GetByIndex(index);

        if (SelectedRef != null)
        {
            Scaler.SetReference(
                SelectedRef.LengthMeters, SelectedRef.WidthMeters, SelectedRef.HeightMeters,
                SelectedRef.RealVolumeM3, 0f // ИСПРАВЛЕНО: было SelectedRef.ConstantFillPercent
            );
        }

        // При смене эталона пересобираем список сплавов (фильтр по тиру)
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
        var parsed = BlueprintParser.ParseFirstLine(code, StandardFuelTank.TYPE_FUELTANK);

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

        var foundRef = fuelTankDatabase.GetByFactionAndBlueprintID(parsed.Faction, parsed.BlueprintId);
        if (foundRef == null)
        {
            ShowError($"Эталон [{parsed.Faction}-{parsed.BlueprintId}] не найден в БД!");
            return;
        }

        SelectReference(fuelTankDatabase.modules.IndexOf(foundRef.gameObject));

        // Масштаб
        float sx = parsed.TargetLength / Scaler.RefLength;
        float sy = parsed.TargetWidth / Scaler.RefWidth;
        float sz = parsed.TargetHeight / Scaler.RefHeight;
        float uniformScale = (sx + sy + sz) / 3f;
        Scaler.SetScaleFactor(uniformScale);

        // Оболочка
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
                    // Проверяем минимальный тир сплава
                    int minTier = SelectedRef != null ? SelectedRef.ModuleTier : 1;
                    if (p.tier < minTier)
                    {
                        ShowMessage($"Тир сплава в чертеже ({p.tier}) ниже минимального ({minTier})!", true);
                    }
                    else
                    {
                        AlloyParams = p;
                        IsAlloyDecoded = true;
                        ShowMessage("Чертеж применен, но указанного сплава нет на складе!", true);
                    }
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

        CalculateFuelTankSpecifics();
        CurrentModuleCode = BuildModuleCode();
    }

    private void CalculateFuelTankSpecifics()
    {
        // Эффективный объём БЕЗ fillFactor — для функциональных расчётов
        float effectiveVolumeDm3 = Scaler.CalcEffectiveVolume * 1000f;
        float moduleCoeff = TierCoeffs.Get(SelectedRef.ModuleTier);
        float wbCoeff = TierCoeffs.Get(workbenchTier);

        // Ёмкость бака = эфф. объём (дм³) × коэфф. тира модуля × коэфф. ёмкости
        CalcCapacity = (float)Math.Round(
            effectiveVolumeDm3 * moduleCoeff * SelectedRef.CapacityCoefficient, 3);

        // Тепло
        CalcHeatCapacity = (float)Math.Round(
            Scaler.CalcRealVolume * SelectedRef.HeatCapacityCoeff * moduleCoeff, 1);
        int thermAbsorb = IsAlloyDecoded ? AlloyParams.thermalAbsorption : 0;
        CalcMaxTemperature = 300f + thermAbsorb;

        // Толщина стенок
        float surfArea = Scaler.CalcSurfaceArea;
        CalcWallThicknessMm = surfArea > 0.000001f
            ? (float)Math.Round((Scaler.CalcShellVolume / surfArea) * 1000f, 1)
            : 0f;

        // Время крафта и энергия
        float innerVol = InnerVolumeM3 <= 0f ? 1f : InnerVolumeM3;
        CalcCraftTimeSeconds = (Scaler.CalcTotalMass * moduleCoeff * SelectedRef.CraftCoefficient) / (wbCoeff * innerVol);
        CalcEnergyCost = (long)Math.Ceiling(Scaler.CalcTotalMass * innerVol);

        // НОВОЕ: Расчет Взрыва (у бака мощность = 0, поэтому радиус тоже 0 или считается иначе, но метод безопасен)
        CalcExplosionRadius = SelectedRef.CalculateExplosionRadius(0f);
        CalcExplosionPenetration = SelectedRef.CalculateExplosionPenetration(Scaler.CalcEffectiveVolume, Scaler.CalcShellMass, Scaler.CurrentAlloyTier);
        CalcExplosionDamage = SelectedRef.CalculateExplosionDamage(Scaler.CalcShellMass, Scaler.CurrentAlloyTier);
    }

    private string BuildModuleCode()
    {
        int tier = SelectedRef.ModuleTier;
        string faction = string.IsNullOrEmpty(SelectedRef.FactionShortName) ? "NONE" : SelectedRef.FactionShortName;
        string alloyCode = (IsAlloyDecoded && AlloyCodes.Length > 0 && SelectedAlloyIndex >= 0)
            ? AlloyCodes[SelectedAlloyIndex]
            : "NONE";

        string line1 = $"{StandardFuelTank.TYPE_FUELTANK}-T{tier}" +
                        $"-m{FormatF(Scaler.CalcTotalMass, 3)}" +
                        $"-d{FormatF(Scaler.CalcDurability, 3)}" +
                        $"-{FormatF(Scaler.CalcLength, 3)}/{FormatF(Scaler.CalcWidth, 3)}/{FormatF(Scaler.CalcHeight, 3)}" +
                        $"-sp{FormatF(ShellPercent, 3)}" +
                        $"-{faction}-{SelectedRef.BlueprintId}";

        string line2 = $"C{FormatF(CalcCapacity, 3)}";

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

        if (SelectedRef.ModuleTier > workbenchTier)
        { failReason = "Тир эталона выше тира верстака."; return false; }

        // Проверяем тир сплава >= тир эталона
        if (!IsAlloyDecoded)
        { failReason = "Сплав не выбран."; return false; }

        if (AlloyParams.tier < SelectedRef.ModuleTier)
        { failReason = $"Тир сплава ({AlloyParams.tier}) ниже тира эталона ({SelectedRef.ModuleTier})."; return false; }

        string alloyCode = AlloyCodes.Length > 0 ? AlloyCodes[SelectedAlloyIndex] : null;
        if (string.IsNullOrEmpty(alloyCode) || !alloyStorage.HasEnoughMass(alloyCode, Scaler.CalcShellMass))
        { failReason = "Недостаточно сплава."; return false; }

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

        // Списываем ресурсы
        alloyStorage.TryConsumeMass(alloyCode, craftShellMass);
        resourcesStorage.TryConsumeEnergy(energyNeeded);

        // Таймер крафта
        float timer = 0f;
        while (timer < CalcCraftTimeSeconds)
        {
            timer += Time.deltaTime;
            CraftProgress = Mathf.Clamp01(timer / CalcCraftTimeSeconds);
            yield return null;
        }

        var dto = new ModuleCraftDTO
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
            fillPercent = 0f,
            length = Scaler.CalcLength,
            width = Scaler.CalcWidth,
            height = Scaler.CalcHeight,
            aabbVolume = Scaler.CalcAABBVolume,
            realVolume = Scaler.CalcRealVolume,
            shellVolumeM3 = Scaler.CalcShellVolume,
            effectiveVolume = Scaler.CalcEffectiveVolume,
            shellMassKg = craftShellMass,
            innerMassKg = 0f,
            totalMassKg = Scaler.CalcTotalMass,
            durability = Scaler.CalcDurability,
            moduleCode = CurrentModuleCode,

            canTurnOnOff = SelectedRef.CanTurnOnOff,
            turnOnOffTime = SelectedRef.TurnOnOffTime,
            canPulseMode = SelectedRef.CanPulseMode,
            pulseInterval = SelectedRef.PulseInterval,
            isControllable = SelectedRef.IsControllable,

            isVolatile = SelectedRef.IsVolatile,
            explosionDamageType = SelectedRef.ExplosionDamageType,

            // НОВОЕ: Физика взрыва в DTO
            explosionRadiusMeters = CalcExplosionRadius,
            explosionPenetration = CalcExplosionPenetration,
            explosionDamage = CalcExplosionDamage
        };

        var tankData = new FuelTankData();
        tankData.Initialize(dto, CalcCapacity, CalcMaxTemperature, CalcHeatCapacity, CalcWallThicknessMm);

        if (placementMode == CraftPlacementMode.SpawnInWorld)
        {
            Vector3 spawnPos = transform.position + Vector3.up * 2f;
            GameObject inst = Instantiate(SelectedRef.gameObject, spawnPos, Quaternion.identity);
            inst.name = $"Crafted_{SelectedRef.gameObject.name}_T{SelectedRef.ModuleTier}";
            inst.transform.localScale = SelectedRef.transform.localScale * Mathf.Max(0.001f, Scaler.CurrentScaleFactor);

            Destroy(inst.GetComponent<StandardFuelTank>());
            var craftedComp = inst.AddComponent<CraftedModule>();
            craftedComp.SetData(tankData);

            // НОВАЯ ЛОГИКА ВЗРЫВА ПРИ СПАВНЕ В МИР
            if (SelectedRef.IsVolatile)
            {
                var volComp = inst.AddComponent<RuntimeVolatileModule>();
                volComp.Initialize(Scaler.CalcTotalMass, SelectedRef.ModuleTier, Scaler.CalcEffectiveVolume, SelectedRef.ExplosionDamageType);
            }
        }
        else
        {
            if (moduleStorage != null) moduleStorage.AddModule(tankData);
        }

        IsCrafting = false;
        CraftProgress = 0f;
        ShowMessage("Топливный бак успешно изготовлен!", false);
        RebuildAlloyList();
        RecalculateAll();
    }

    private void RebuildReferenceList()
    {
        if (fuelTankDatabase == null) return;
        var allRefs = fuelTankDatabase.GetAll();
        RefNames = new string[allRefs.Count];
        for (int i = 0; i < allRefs.Count; i++)
        {
            var sf = allRefs[i];
            string faction = string.IsNullOrEmpty(sf.FactionShortName) ? "NONE" : sf.FactionShortName;
            RefNames[i] = $"[{faction}-{sf.BlueprintId}] {sf.gameObject.name} (T{sf.ModuleTier})";
        }
        if (allRefs.Count > 0) SelectReference(0);
    }

    /// <summary>
    /// Пересобирает список сплавов, фильтруя по минимальному тиру эталона.
    /// Сплавы с тиром ниже тира эталона не отображаются.
    /// </summary>
    public void RebuildAlloyList()
    {
        if (alloyStorage == null || alloyStorage.Count == 0)
        {
            AlloyDisplayNames = new string[0];
            AlloyCodes = new string[0];
            IsAlloyDecoded = false;
            return;
        }

        int minTier = SelectedRef != null ? SelectedRef.ModuleTier : 1;

        // Получаем все сплавы со склада
        string[] allCodes = alloyStorage.GetAllCodes();
        string[] allNames = alloyStorage.GetDisplayNames();

        // Фильтруем по тиру
        var filteredCodes = new List<string>();
        var filteredNames = new List<string>();

        for (int i = 0; i < allCodes.Length; i++)
        {
            if (AlloyCode.Decode(allCodes[i], out AlloyCode.AlloyParams p))
            {
                if (p.tier >= minTier)
                {
                    filteredCodes.Add(allCodes[i]);
                    filteredNames.Add(allNames[i]);
                }
            }
        }

        AlloyCodes = filteredCodes.ToArray();
        AlloyDisplayNames = filteredNames.ToArray();

        if (AlloyCodes.Length > 0)
        {
            SelectAlloy(0);
        }
        else
        {
            SelectedAlloyIndex = 0;
            IsAlloyDecoded = false;
        }
    }

    private void ShowError(string msg) { ErrorMessage = msg; SuccessMessage = ""; WarningMessage = ""; messageTimer = 4f; }
    private void ShowMessage(string msg, bool isWarning) { if (isWarning) WarningMessage = msg; else SuccessMessage = msg; ErrorMessage = ""; messageTimer = 4f; }
    private void ClearMessages() { ErrorMessage = ""; SuccessMessage = ""; WarningMessage = ""; }
}