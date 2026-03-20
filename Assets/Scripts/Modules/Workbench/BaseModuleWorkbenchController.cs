using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
/// <summary>
/// Базовый контроллер верстака для всех типов модулей.
/// Содержит общую логику: масштабирование, выбор сплава, проверку ресурсов, крафт.
/// Наследники реализуют только специфичные расчёты и создание данных.
/// </summary>
public abstract class BaseModuleWorkbenchController<TRef, TData, TDatabase> : MonoBehaviour
    where TRef : StandardModuleBase
    where TData : ModuleData, new()
    where TDatabase : GenericModuleDatabase<TRef>
{
    public enum CraftPlacementMode { SpawnInWorld = 0, SaveToStorage = 1 }
    // ====================== Константы ======================
    private const int MinShellPercent = 1;
    private const int MaxShellPercent = 99;
    private const float MessageDuration = 4f;
    [Header("Workbench Parameters")]
    [Range(1, 10)] public int workbenchTier = 1;
    public float innerLength = 2f;
    public float innerWidth = 2f;
    public float innerHeight = 2f;
    [Header("Databases & Storages")]
    public TDatabase database;
    public ResourcesStorage resourcesStorage;
    public AlloyStorage alloyStorage;
    public ModuleStorage moduleStorage;
    [Header("Settings")]
    public CraftPlacementMode placementMode = CraftPlacementMode.SpawnInWorld;
    // ================= СОСТОЯНИЕ =================
    public ModuleScaler Scaler { get; private set; } = new ModuleScaler();
    public TRef SelectedRef { get; private set; }
    public int SelectedRefIndex { get; private set; }
    public string[] RefNames { get; private set; } = Array.Empty<string>();
    public float ShellPercent { get; private set; } = 5f;
    public int SelectedAlloyIndex { get; private set; }
    public string[] AlloyDisplayNames { get; protected set; } = Array.Empty<string>();
    public string[] AlloyCodes { get; protected set; } = Array.Empty<string>();
    public AlloyCode.AlloyParams AlloyParams { get; private set; }
    public bool IsAlloyDecoded { get; protected set; }
    public string CurrentModuleCode { get; private set; } = "";
    public Dictionary<ResourcesStorage.ResourceIndex, long> RequiredInternalResources { get; private set; }
        = new Dictionary<ResourcesStorage.ResourceIndex, long>();
    // Общие расчёты
    public float CalcCraftTimeSeconds { get; protected set; }
    public long CalcEnergyCost { get; protected set; }
    public float CalcExplosionRadius { get; protected set; }
    public float CalcExplosionPenetration { get; protected set; }
    public float CalcExplosionDamage { get; protected set; }
    // Сообщения
    public string ErrorMessage { get; private set; } = "";
    public string SuccessMessage { get; private set; } = "";
    public string WarningMessage { get; private set; } = "";
    private float messageTimer;
    // Крафт
    public bool IsCrafting { get; private set; }
    public float CraftProgress { get; private set; }
    protected float InnerVolumeM3 => innerLength * innerWidth * innerHeight;
    // ================= АБСТРАКТНЫЕ МЕТОДЫ (наследник реализует) =================
    /// <summary>Тип модуля для кода чертежа (например "Generator").</summary>
    protected abstract string ModuleTypeName { get; }
    /// <summary>Рассчитать специфичные выходы модуля (мощность, ёмкость и т.д.).</summary>
    protected abstract void CalculateSpecificOutputs();
    /// <summary>Построить вторую строку кода чертежа.</summary>
    protected abstract string BuildSecondCodeLine();
    /// <summary>Создать и инициализировать конкретный ModuleData.</summary>
    protected abstract TData CreateModuleData(ModuleCraftDTO dto);
    /// <summary>Добавить Runtime-компонент нужного типа на объект.</summary>
    protected abstract RuntimeModuleBase AddRuntimeComponent(GameObject obj);
    // ================= ВИРТУАЛЬНЫЕ МЕТОДЫ (бак переопределяет) =================
    protected virtual void GetReferenceScalerParams(TRef reference, out float fillPercent)
    {
        fillPercent = reference.ConstantFillPercent;
    }
    /// <summary>Пересобрать список сплавов. Бак фильтрует по тиру эталона.</summary>
    public virtual void RebuildAlloyList()
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
    /// <summary>Проверить специфичные условия крафта. Бак проверяет тир сплава.</summary>
    protected virtual bool CheckSpecificCraftConditions(out string failReason)
    {
        failReason = "";
        return true;
    }
    /// <summary>Списать специфичные ресурсы при крафте. Бак ничего не списывает.</summary>
    protected virtual void ConsumeSpecificResources()
    {
        foreach (var kvp in RequiredInternalResources)
        {
            resourcesStorage.TryRemoveGrams(kvp.Key, kvp.Value);
        }
    }
    /// <summary>Дополнительная логика после применения чертежа. Бак проверяет тир сплава.</summary>
    protected virtual void OnBlueprintAlloyApplied(AlloyCode.AlloyParams parsedAlloy) { }
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
    // ================= ПУБЛИЧНОЕ API =================
    public void SelectReference(int index)
    {
        if (database == null || index < 0 || index >= database.Count) return;
        SelectedRefIndex = index;
        SelectedRef = database.GetByIndex(index);
        if (SelectedRef != null)
        {
            GetReferenceScalerParams(SelectedRef, out float fillPct);
            Scaler.SetReference(
                SelectedRef.LengthMeters,
                SelectedRef.WidthMeters,
                SelectedRef.HeightMeters,
                SelectedRef.RealVolumeM3,
                fillPct
            );
        }
        OnReferenceChanged();
        RecalculateAll();
    }
    /// <summary>Хук для наследников после смены эталона (например, пересобрать список сплавов).</summary>
    protected virtual void OnReferenceChanged() { }
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
    // ================= ПАРСИНГ ЧЕРТЕЖА =================
    public void ApplyBlueprintCode(string code)
    {
        var parsed = BlueprintParser.ParseFirstLine(code, ModuleTypeName);
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
        var foundRef = database.GetByFactionAndBlueprintID(parsed.Faction, parsed.BlueprintId);
        if (foundRef == null)
        {
            ShowError($"Эталон [{parsed.Faction}-{parsed.BlueprintId}] не найден в БД!");
            return;
        }
        SelectReference(database.modules.IndexOf(foundRef.gameObject));
        // Масштаб
        float sx = parsed.TargetLength / Scaler.RefLength;
        float sy = parsed.TargetWidth / Scaler.RefWidth;
        float sz = parsed.TargetHeight / Scaler.RefHeight;
        Scaler.SetScaleFactor((sx + sy + sz) / 3f);
        // Оболочка
        if (parsed.ShellPercent > 0f)
            SetShellPercent(parsed.ShellPercent);
        else
            SetShellPercent(5f);
        // Сплав
        string[] lines = BlueprintParser.NormalizeCodeText(code).Split('\n');
        if (lines.Length >= 3)
        {
            string inputAlloy = lines[2].Trim();
            int idx = Array.IndexOf(AlloyCodes, inputAlloy);
            if (idx >= 0)
            {
                SelectAlloy(idx);
            }
            else if (AlloyCode.Decode(inputAlloy, out var p))
            {
                OnBlueprintAlloyApplied(p);
                AlloyParams = p;
                IsAlloyDecoded = true;
                ShowMessage("Чертеж применен, но указанного сплава нет на складе!", true);
            }
        }
        RecalculateAll();
    }
    // ================= РАСЧЁТЫ =================
    protected void RecalculateAll()
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
        if (SelectedRef.InternalResourceCosts == null) return;
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
    private void CalculateCommonOutputs()
    {
        float moduleCoeff = TierCoeffs.Get(SelectedRef.ModuleTier);
        float wbCoeff = TierCoeffs.Get(workbenchTier);
        float innerVol = InnerVolumeM3 <= 0f ? 1f : InnerVolumeM3;
        CalcCraftTimeSeconds = (Scaler.CalcTotalMass * moduleCoeff * SelectedRef.CraftCoefficient) / (wbCoeff * innerVol);
        CalcEnergyCost = (long)Math.Ceiling(Scaler.CalcTotalMass * innerVol);
        CalcExplosionRadius = SelectedRef.CalculateExplosionRadius(GetExplosionPowerSource());
        CalcExplosionPenetration = SelectedRef.CalculateExplosionPenetration(
            Scaler.CalcEffectiveVolume, Scaler.CalcShellMass, Scaler.CurrentAlloyTier);
        CalcExplosionDamage = SelectedRef.CalculateExplosionDamage(
            Scaler.CalcShellMass, Scaler.CurrentAlloyTier);
    }
    /// <summary>Источник мощности для расчёта радиуса взрыва. Генератор = Power, Батарея = Capacity, Бак = 0.</summary>
    protected virtual float GetExplosionPowerSource() => 0f;
    private string BuildModuleCode()
    {
        string faction = string.IsNullOrEmpty(SelectedRef.FactionShortName)
            ? "NONE" : SelectedRef.FactionShortName;
        string alloyCode = (IsAlloyDecoded && AlloyCodes.Length > 0 && SelectedAlloyIndex >= 0)
            ? AlloyCodes[SelectedAlloyIndex] : "NONE";
        string line1 = $"{ModuleTypeName}-T{SelectedRef.ModuleTier}" +
                       $"-m{FormatF(Scaler.CalcTotalMass, 3)}" +
                       $"-d{FormatF(Scaler.CalcDurability, 3)}" +
                       $"-{FormatF(Scaler.CalcLength, 3)}/{FormatF(Scaler.CalcWidth, 3)}/{FormatF(Scaler.CalcHeight, 3)}" +
                       $"-sp{FormatF(ShellPercent, 3)}" +
                       $"-{faction}-{SelectedRef.BlueprintId}";
        string line2 = BuildSecondCodeLine();
        return $"{line1}\n{line2}\n{alloyCode}";
    }
    // ================= КРАФТ =================
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
        if (!IsAlloyDecoded)
        { failReason = "Сплав не выбран."; return false; }
        string alloyCode = AlloyCodes.Length > 0 ? AlloyCodes[SelectedAlloyIndex] : null;
        if (string.IsNullOrEmpty(alloyCode) || !alloyStorage.HasEnoughMass(alloyCode, Scaler.CalcShellMass))
        { failReason = "Недостаточно сплава."; return false; }
        // Проверка внутренних ресурсов (бак пропускает — словарь пустой)
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
        // Специфичные проверки наследника
        if (!CheckSpecificCraftConditions(out string specificFail))
        { failReason = specificFail; return false; }
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
        // Списываем общие ресурсы
        alloyStorage.TryConsumeMass(alloyCode, craftShellMass);
        resourcesStorage.TryConsumeEnergy(CalcEnergyCost);
        // Списываем специфичные ресурсы (бак переопределяет на пустой метод)
        ConsumeSpecificResources();
        // Таймер крафта
        float timer = 0f;
        while (timer < CalcCraftTimeSeconds)
        {
            timer += Time.deltaTime;
            CraftProgress = Mathf.Clamp01(timer / CalcCraftTimeSeconds);
            yield return null;
        }
        // Создаём DTO
        var dto = BuildBaseCraftDTO(alloyCode, craftShellMass);
        // Наследник создаёт конкретный ModuleData
        TData moduleData = CreateModuleData(dto);
        // Спавн или сохранение
        if (placementMode == CraftPlacementMode.SpawnInWorld)
            SpawnInWorld(moduleData);
        else if (moduleStorage != null)
            moduleStorage.AddModule(moduleData);
        IsCrafting = false;
        CraftProgress = 0f;
        ShowMessage($"{ModuleTypeName} успешно изготовлен!", false);
        RebuildAlloyList();
        RecalculateAll();
    }
    private ModuleCraftDTO BuildBaseCraftDTO(string alloyCode, float shellMass)
    {
        return new ModuleCraftDTO
        {
            moduleType = ModuleTypeName,
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
            moduleCode = CurrentModuleCode,
            wallThicknessMm = Scaler.CalcWallThicknessMm,
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
            useBuildAnchorPlacement = true,
            buildAnchorCellLocal = SelectedRef.BuildAnchorCellLocal,
            referenceVisualScale = SelectedRef.transform.localScale,

        };
    }
    private void SpawnInWorld(TData moduleData)
    {
        Vector3 spawnPos = transform.position + Vector3.up * 2f;
        GameObject inst = Instantiate(SelectedRef.gameObject, spawnPos, Quaternion.identity);
        inst.name = $"Crafted_{SelectedRef.gameObject.name}_T{SelectedRef.ModuleTier}";
        Vector3 referenceScale = moduleData.referenceVisualScale == Vector3.zero
            ? SelectedRef.transform.localScale
            : moduleData.referenceVisualScale;

        inst.transform.localScale = referenceScale * Mathf.Max(0.001f, moduleData.scaleFactor);
        // Удаляем эталонный компонент
        var standardComp = inst.GetComponent<TRef>();
        if (standardComp != null) Destroy(standardComp);
        // Добавляем CraftedModule с данными
        var craftedComp = inst.AddComponent<CraftedModule>();
        craftedComp.SetData(moduleData);
        // Добавляем Runtime-компонент
        AddRuntimeComponent(inst);
        // Волатильность
        if (SelectedRef.IsVolatile)
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
    }
    // ================= ПОМОЩНИКИ =================
    protected void RebuildReferenceList()
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
    protected void ShowError(string msg)
    { ErrorMessage = msg; SuccessMessage = ""; WarningMessage = ""; messageTimer = MessageDuration; }
    protected void ShowMessage(string msg, bool isWarning)
    { if (isWarning) WarningMessage = msg; else SuccessMessage = msg; ErrorMessage = ""; messageTimer = MessageDuration; }
    private void ClearMessages()
    { ErrorMessage = ""; SuccessMessage = ""; WarningMessage = ""; }
    protected static string FormatF(float v, int dec)
        => v.ToString($"F{dec}", CultureInfo.InvariantCulture);
}