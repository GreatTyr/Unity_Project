using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// UI верстака крафта модулей на IMGUI.
/// Вешается на GameObject верстака. Склады назначаются в инспекторе.
/// Использует IModuleCalculator для делегирования расчётов конкретным типам модулей.
/// Путь: Assets/Scripts/CoreMechanics/ModuleWorkbench.cs
/// </summary>
public class ModuleWorkbench : MonoBehaviour
{
    [Header("Storage References")]
    [Tooltip("Alloy storage to pick shell alloy from and consume on craft.")]
    public AlloyStorage alloyStorage;

    [Tooltip("Resources storage to consume metal and energy on craft.")]
    public ResourcesStorage resourcesStorage;

    // ====================== State ======================
    private bool panelOpen;
    private Rect windowRect = new Rect(30, 30, 1000, 612);

    // Module type selection
    private int selectedModuleTypeIndex;
    private string[] moduleTypeNames;

    // Calculators
    private Dictionary<string, IModuleCalculator> calculators;
    private IModuleCalculator activeCalculator;

    // Shell
    private float shellPercent = 5f;
    private string shellPercentStr = "5.000";

    // Alloy selection
    private int selectedAlloyIndex;
    private string[] alloyDisplayNames;
    private string[] alloyCodes;

    // Scaling
    private enum ScaleMode { Length, Width, Height, Mass, EffectiveVolume }
    private ScaleMode scaleMode = ScaleMode.Mass;
    private float scaleInputValue;
    private string scaleInputStr = "";

    // Computed results (common)
    private float scaleFactor = 1f;
    private float calcLength, calcWidth, calcHeight;
    private float calcAABBVolume, calcRealVolume;
    private float calcShellVolumeM3, calcEffectiveVolume;
    private float calcShellMassKg, calcInnerMassKg, calcTotalMassKg;
    private float calcDurability;

    // Alloy params (decoded from selected alloy)
    private bool alloyDecoded;
    private AlloyCode.AlloyParams alloyParams;

    // Code
    private string currentModuleCode = "";
    private string codeInputField = "";

    // Error
    private string errorMessage = "";
    private float errorTimer;

    // Crafted object
    private GameObject craftedInstance;

    // Scroll
    private Vector2 scrollPos;

    // ====================== Init ======================

    private void Awake()
    {
        BuildCalculators();
    }

    private void BuildCalculators()
    {
        calculators = new Dictionary<string, IModuleCalculator>();

        // Регистрируем все калькуляторы
        RegisterCalculator(new EnergyStorageCalculator());
        // В будущем:
        // RegisterCalculator(new GeneratorCalculator());
        // RegisterCalculator(new FuelTankCalculator());
    }

    private void RegisterCalculator(IModuleCalculator calc)
    {
        if (calc == null) return;
        calculators[calc.ModuleType] = calc;
    }

    // ====================== Open / Close ======================

    public void OpenPanel()
    {
        panelOpen = true;
        RebuildAllLists();
        ResetToDefaults();
    }

    public void ClosePanel()
    {
        panelOpen = false;
    }

    private void Update()
    {
        if (panelOpen && Keyboard.current != null)
        {
            if (Keyboard.current.escapeKey.wasPressedThisFrame)
                ClosePanel();
        }

        if (errorTimer > 0f)
        {
            errorTimer -= Time.deltaTime;
            if (errorTimer <= 0f) errorMessage = "";
        }
    }

    // ====================== OnGUI ======================

    private void OnGUI()
    {
        if (!panelOpen) return;
        windowRect = GUI.Window(298765, windowRect, DrawWindow, "Верстак модулей");
        GenericMenuIMGUI.DrawPopup();
    }

    // ====================== Main Window ======================

    private void DrawWindow(int id)
    {
        GUI.DragWindow(new Rect(0, 0, 10000, 20));

        scrollPos = GUILayout.BeginScrollView(scrollPos);
        GUILayout.BeginVertical();

        // ─── Error ───
        if (!string.IsNullOrEmpty(errorMessage))
        {
            Color prev = GUI.color;
            GUI.color = Color.red;
            GUILayout.Label(errorMessage, GetCenteredBoldStyle());
            GUI.color = prev;
            GUILayout.Space(2);
        }

        // ─── Code area ───
        DrawCodeSection();
        GUILayout.Space(3);

        float halfW = (windowRect.width - 30) * 0.5f;

        GUILayout.BeginHorizontal();

        // ══════ Left column ══════
        GUILayout.BeginVertical(GUILayout.Width(halfW));
        DrawSelectionSection();
        GUILayout.Space(3);
        DrawShellSection();
        GUILayout.Space(3);
        DrawScalingSection();
        GUILayout.EndVertical();

        // ══════ Right column ══════
        GUILayout.BeginVertical(GUILayout.Width(halfW));
        DrawComputedSection();
        GUILayout.Space(3);
        DrawModuleSpecificSection();
        GUILayout.Space(3);
        DrawAlloyParamsSection();
        GUILayout.EndVertical();

        GUILayout.EndHorizontal();

        GUILayout.Space(4);

        // ─── Costs & Buttons ───
        DrawCostsAndButtons();

        GUILayout.EndVertical();
        GUILayout.EndScrollView();
    }

    // ====================== Sections ======================

    private void DrawCodeSection()
    {
        GUILayout.BeginHorizontal();
        GUILayout.Label("Код модуля:", GUILayout.Width(85));
        GUI.enabled = false;
        GUILayout.TextField(currentModuleCode, GUILayout.Width(windowRect.width - 300));
        GUI.enabled = true;

        if (GUILayout.Button("Копировать", GUILayout.Width(80)))
        {
            if (!string.IsNullOrEmpty(currentModuleCode))
                GUIUtility.systemCopyBuffer = currentModuleCode;
        }
        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();
        GUILayout.Label("Вставить код:", GUILayout.Width(85));
        codeInputField = GUILayout.TextField(codeInputField, GUILayout.Width(windowRect.width - 380));

        if (GUILayout.Button("Вставить", GUILayout.Width(70)))
        {
            codeInputField = (GUIUtility.systemCopyBuffer ?? "").Trim();
        }
        if (GUILayout.Button("Применить", GUILayout.Width(75)))
        {
            ShowError("Система кодов модулей в разработке");
        }
        GUILayout.EndHorizontal();
    }

    private void DrawSelectionSection()
    {
        GUILayout.Label("Выбор модуля", GetBoldStyle());

        // Module type
        GUILayout.BeginHorizontal();
        GUILayout.Label("Тип модуля:", GUILayout.Width(130));
        if (moduleTypeNames != null && moduleTypeNames.Length > 0)
        {
            int newIdx = DrawPopup(selectedModuleTypeIndex, moduleTypeNames, "moduleType");
            if (newIdx != selectedModuleTypeIndex)
            {
                selectedModuleTypeIndex = newIdx;
                OnModuleTypeChanged();
            }
        }
        GUILayout.EndHorizontal();

        // Reference — dropdown
        GUILayout.BeginHorizontal();
        GUILayout.Label("Эталонный модуль:", GUILayout.Width(130));
        if (activeCalculator != null && activeCalculator.ReferenceCount > 0)
        {
            string[] refNames = activeCalculator.GetReferenceNames();
            int curIdx = activeCalculator.SelectedIndex;
            int newIdx = DrawPopup(curIdx, refNames, "moduleRef");
            if (newIdx != curIdx)
            {
                activeCalculator.SelectReference(newIdx);
                OnReferenceChanged();
            }
        }
        else
        {
            GUILayout.Label("(Нет эталонов для этого типа)");
        }
        GUILayout.EndHorizontal();

        // Show reference params
        if (activeCalculator != null && activeCalculator.ReferenceCount > 0)
        {
            string faction = string.IsNullOrEmpty(activeCalculator.RefFaction) ? "—" : activeCalculator.RefFaction;
            GUILayout.Label($"  Тир: {activeCalculator.RefModuleTier}   " +
                            $"Фракция: {faction}   " +
                            $"Fill: {activeCalculator.RefFillPercent:F1}%   " +
                            $"VCoeff: {activeCalculator.RefVolumeCoefficientPercent:F1}%");
        }
    }

    private void DrawShellSection()
    {
        GUILayout.Label("Оболочка", GetBoldStyle());

        // Shell percent
        GUILayout.BeginHorizontal();
        GUILayout.Label("Shell Volume (%):", GUILayout.Width(130));

        string newStr = GUILayout.TextField(shellPercentStr, GUILayout.Width(70));
        if (newStr != shellPercentStr)
        {
            shellPercentStr = newStr;
            if (float.TryParse(shellPercentStr, out float val))
            {
                shellPercent = Mathf.Clamp(val, 0.001f, 100f);
                RecalculateAll();
                UpdateScaleInputFromCurrent();
            }
        }

        float sliderVal = GUILayout.HorizontalSlider(shellPercent, 0.001f, 100f, GUILayout.Width(160));
        if (Mathf.Abs(sliderVal - shellPercent) > 0.0005f)
        {
            shellPercent = (float)Math.Round(sliderVal, 3);
            shellPercentStr = shellPercent.ToString("F3");
            RecalculateAll();
            UpdateScaleInputFromCurrent();
        }

        GUILayout.Label($"{shellPercent:F3}%", GUILayout.Width(65));
        GUILayout.EndHorizontal();

        // Alloy selection — dropdown
        GUILayout.BeginHorizontal();
        GUILayout.Label("Сплав оболочки:", GUILayout.Width(130));
        if (alloyDisplayNames != null && alloyDisplayNames.Length > 0)
        {
            int newIdx = DrawPopup(selectedAlloyIndex, alloyDisplayNames, "alloySelect");
            if (newIdx != selectedAlloyIndex)
            {
                selectedAlloyIndex = newIdx;
                OnAlloyChanged();
            }
        }
        else
        {
            Color prev = GUI.color;
            GUI.color = Color.yellow;
            GUILayout.Label("(AlloyStorage пуст)");
            GUI.color = prev;
        }
        GUILayout.EndHorizontal();
    }

    private void DrawScalingSection()
    {
        GUILayout.Label("Масштабирование", GetBoldStyle());

        GUILayout.BeginHorizontal();
        GUILayout.Label("Параметр:", GUILayout.Width(130));
        string[] modeNames = { "Длина", "Ширина", "Высота", "Масса", "Эфф.объём" };
        int newMode = GUILayout.SelectionGrid((int)scaleMode, modeNames, modeNames.Length);
        if (newMode != (int)scaleMode)
        {
            scaleMode = (ScaleMode)newMode;
            UpdateScaleInputFromCurrent();
        }
        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();
        GUILayout.Label("Значение:", GUILayout.Width(130));
        string newScaleStr = GUILayout.TextField(scaleInputStr, GUILayout.Width(100));
        if (newScaleStr != scaleInputStr)
        {
            scaleInputStr = newScaleStr;
            if (float.TryParse(scaleInputStr, out float val) && val > 0f)
            {
                scaleInputValue = val;
                RecalculateFromScaleInput();
            }
        }
        string unit = scaleMode == ScaleMode.Mass ? "кг" :
                      scaleMode == ScaleMode.EffectiveVolume ? "м³" : "м";
        GUILayout.Label(unit, GUILayout.Width(25));

        if (GUILayout.Button("Сброс масштаба", GUILayout.Width(110)))
        {
            scaleFactor = 1f;
            RecalculateAll();
            UpdateScaleInputFromCurrent();
        }
        GUILayout.EndHorizontal();
    }

    private void DrawComputedSection()
    {
        GUILayout.Label("Общие параметры", GetBoldStyle());

        LabelPair("Длина (X):", $"{calcLength:F3} м");
        LabelPair("Ширина (Z):", $"{calcWidth:F3} м");
        LabelPair("Высота (Y):", $"{calcHeight:F3} м");
        GUILayout.Space(2);
        LabelPair("AABB объём:", $"{calcAABBVolume:F6} м³");
        LabelPair("Real объём:", $"{calcRealVolume:F6} м³");
        LabelPair("Shell объём:", $"{calcShellVolumeM3:F6} м³");
        LabelPair("Effective объём:", $"{calcEffectiveVolume:F6} м³");
        GUILayout.Space(2);
        LabelPair("Масса оболочки:", $"{calcShellMassKg:F3} кг");
        LabelPair("Масса начинки:", $"{calcInnerMassKg:F3} кг");
        LabelPair("Общая масса:", $"{calcTotalMassKg:F3} кг");
        GUILayout.Space(2);

        Color prevC = GUI.color;
        GUI.color = Color.cyan;
        LabelPair("Прочность:", $"{calcDurability:F3}");
        GUI.color = prevC;
    }

    private void DrawModuleSpecificSection()
    {
        if (activeCalculator == null) return;

        GUILayout.Label($"Параметры: {activeCalculator.ModuleType}", GetBoldStyle());
        activeCalculator.DrawResultsGUI();
    }

    private void DrawAlloyParamsSection()
    {
        GUILayout.Label("Параметры сплава оболочки", GetBoldStyle());

        if (!alloyDecoded || alloyCodes == null || alloyCodes.Length == 0)
        {
            GUILayout.Label("(Сплав не выбран или не распознан)");
            return;
        }

        GUILayout.Label($"Тир сплава: {alloyParams.tier}    " +
                        $"Химикаты: {(alloyParams.useChemicals ? "Да" : "Нет")}    " +
                        $"Наниты: {(alloyParams.useNanites ? "Да" : "Нет")}");

        float colW = (windowRect.width - 40) * 0.24f;

        GUILayout.BeginHorizontal();
        DrawAlloyColumn("Кинетика", alloyParams.kineticAbsorption, alloyParams.kineticResistance, colW);
        DrawAlloyColumn("Термика", alloyParams.thermalAbsorption, alloyParams.thermalResistance, colW);
        DrawAlloyColumn("Химия", alloyParams.chemicalAbsorption, alloyParams.chemicalResistance, colW);
        DrawAlloyColumn("Энергия", alloyParams.energyAbsorption, alloyParams.energyResistance, colW);
        GUILayout.EndHorizontal();
    }

    private void DrawAlloyColumn(string title, int absorb, float resist, float width)
    {
        GUILayout.BeginVertical("box", GUILayout.Width(width));
        GUILayout.Label(title, GetBoldStyle());
        GUILayout.Label($"Погл: {absorb}");
        GUILayout.Label($"Сопр: {resist:F1}%");
        GUILayout.EndVertical();
    }

    private void DrawCostsAndButtons()
    {
        GUILayout.BeginHorizontal("box");

        // Left: costs
        GUILayout.BeginVertical(GUILayout.Width(windowRect.width * 0.6f - 10));

        GUILayout.Label("Стоимость изготовления", GetBoldStyle());

        string alloyCode = GetSelectedAlloyCode();
        float alloyAvailable = alloyCode != null && alloyStorage != null
            ? (float)alloyStorage.GetMass(alloyCode) : 0f;
        bool enoughAlloy = alloyCode != null && alloyStorage != null &&
            alloyStorage.HasEnoughMass(alloyCode, calcShellMassKg);

        int metalTier = activeCalculator != null ? activeCalculator.RefModuleTier : 1;
        var metalIdx = GetMetalIndex(metalTier);
        float metalAvailable = resourcesStorage != null
            ? (float)(resourcesStorage.GetGrams(metalIdx) / 1000.0) : 0f;
        float metalNeeded = calcInnerMassKg;
        bool enoughMetal = metalAvailable >= metalNeeded - 0.001f;

        long energyNeeded = (long)Math.Ceiling(calcTotalMassKg);
        long energyAvailable = resourcesStorage != null ? resourcesStorage.EnergyUnits : 0;
        bool enoughEnergy = energyAvailable >= energyNeeded;

        DrawCostLine($"Сплав ({alloyCode ?? "—"}):", calcShellMassKg, alloyAvailable, "кг", enoughAlloy);
        DrawCostLine($"Металл T{metalTier}:", metalNeeded, metalAvailable, "кг", enoughMetal);
        DrawCostLineEnergy("Энергия:", energyNeeded, energyAvailable, enoughEnergy);

        GUILayout.EndVertical();

        // Right: buttons
        GUILayout.BeginVertical(GUILayout.Width(windowRect.width * 0.4f - 10));
        GUILayout.FlexibleSpace();

        bool canCraft = activeCalculator != null &&
                        activeCalculator.ReferenceCount > 0 &&
                        alloyCode != null &&
                        enoughAlloy && enoughMetal && enoughEnergy &&
                        calcEffectiveVolume > 0.000001f;

        if (!canCraft) GUI.enabled = false;
        if (GUILayout.Button("Изготовить", GUILayout.Height(28), GUILayout.Width(150)))
        {
            OnCraft();
        }
        GUI.enabled = true;

        GUILayout.Space(8);

        if (GUILayout.Button("Сброс", GUILayout.Height(28), GUILayout.Width(150)))
        {
            ResetToDefaults();
        }

        GUILayout.FlexibleSpace();
        GUILayout.EndVertical();

        GUILayout.EndHorizontal();
    }

    // ====================== Helpers ======================

    private void LabelPair(string left, string right)
    {
        GUILayout.BeginHorizontal();
        GUILayout.Label(left, GUILayout.Width(130));
        GUILayout.Label(right);
        GUILayout.EndHorizontal();
    }

    private void DrawCostLine(string label, float needed, float available, string unit, bool enough)
    {
        GUILayout.BeginHorizontal();
        GUILayout.Label(label, GUILayout.Width(180));
        GUILayout.Label($"{needed:F3} {unit}", GUILayout.Width(110));

        Color prev = GUI.color;
        if (!enough) GUI.color = Color.red;
        GUILayout.Label($"(есть: {available:F3})", GUILayout.Width(150));
        GUI.color = prev;

        GUILayout.EndHorizontal();
    }

    private void DrawCostLineEnergy(string label, long needed, long available, bool enough)
    {
        GUILayout.BeginHorizontal();
        GUILayout.Label(label, GUILayout.Width(180));
        GUILayout.Label($"{needed}", GUILayout.Width(110));

        Color prev = GUI.color;
        if (!enough) GUI.color = Color.red;
        GUILayout.Label($"(есть: {available})", GUILayout.Width(150));
        GUI.color = prev;

        GUILayout.EndHorizontal();
    }

    // ====================== Popup ======================

    // Хранилище состояний popup'ов по тегу
    private Dictionary<string, int> _popupResults = new Dictionary<string, int>();

    private int DrawPopup(int selected, string[] options, string tag)
    {
        if (options == null || options.Length == 0) return selected;
        selected = Mathf.Clamp(selected, 0, options.Length - 1);

        string current = options[selected];
        if (GUILayout.Button(current, "popup"))
        {
            string capturedTag = tag;
            GenericMenuIMGUI.Show(options, selected, idx =>
            {
                _popupResults[capturedTag] = idx;
            });
        }

        if (_popupResults.TryGetValue(tag, out int result))
        {
            _popupResults.Remove(tag);
            return Mathf.Clamp(result, 0, options.Length - 1);
        }

        return selected;
    }

    private static ResourcesStorage.ResourceIndex GetMetalIndex(int tier)
    {
        return (ResourcesStorage.ResourceIndex)(
            (int)ResourcesStorage.ResourceType.Metal * ResourcesStorage.TiersPerType + (tier - 1));
    }

    // ====================== List Building ======================

    private void RebuildAllLists()
    {
        RebuildModuleTypeList();
        RebuildAlloyList();
        OnModuleTypeChanged();
    }

    private void RebuildModuleTypeList()
    {
        var db = ModuleTypesDatabase.Instance;
        if (db != null)
        {
            // Показываем только те типы, для которых есть калькулятор
            var available = new List<string>();
            string[] allNames = db.GetAllNames();
            foreach (var name in allNames)
            {
                if (calculators.ContainsKey(name))
                    available.Add(name);
            }

            if (available.Count > 0)
            {
                moduleTypeNames = available.ToArray();
            }
            else
            {
                moduleTypeNames = new string[] { "(Нет доступных типов)" };
            }
        }
        else
        {
            moduleTypeNames = new string[] { "(No ModuleTypesDatabase)" };
        }

        selectedModuleTypeIndex = 0;
    }

    private void RebuildAlloyList()
    {
        if (alloyStorage == null || alloyStorage.Count == 0)
        {
            alloyDisplayNames = new string[0];
            alloyCodes = new string[0];
            selectedAlloyIndex = 0;
            alloyDecoded = false;
            return;
        }

        alloyDisplayNames = alloyStorage.GetDisplayNames();
        alloyCodes = alloyStorage.GetAllCodes();
        selectedAlloyIndex = 0;
        OnAlloyChanged();
    }

    // ====================== Selection Callbacks ======================

    private void OnModuleTypeChanged()
    {
        activeCalculator = null;

        if (moduleTypeNames == null || moduleTypeNames.Length == 0) return;
        if (selectedModuleTypeIndex < 0 || selectedModuleTypeIndex >= moduleTypeNames.Length) return;

        string typeName = moduleTypeNames[selectedModuleTypeIndex];
        if (calculators.TryGetValue(typeName, out var calc))
        {
            activeCalculator = calc;

            // Выбрать первый эталон
            if (activeCalculator.ReferenceCount > 0)
                activeCalculator.SelectReference(0);
        }

        scaleFactor = 1f;
        RecalculateAll();
        UpdateScaleInputFromCurrent();
    }

    private void OnReferenceChanged()
    {
        scaleFactor = 1f;
        RecalculateAll();
        UpdateScaleInputFromCurrent();
    }

    private void OnAlloyChanged()
    {
        alloyDecoded = false;
        if (alloyCodes != null && selectedAlloyIndex >= 0 && selectedAlloyIndex < alloyCodes.Length)
        {
            string code = alloyCodes[selectedAlloyIndex];
            if (AlloyCode.Decode(code, out AlloyCode.AlloyParams p))
            {
                alloyParams = p;
                alloyDecoded = true;
            }
        }
        RecalculateAll();
    }

    private string GetSelectedAlloyCode()
    {
        if (alloyCodes == null || alloyCodes.Length == 0) return null;
        if (selectedAlloyIndex < 0 || selectedAlloyIndex >= alloyCodes.Length) return null;
        return alloyCodes[selectedAlloyIndex];
    }

    // ====================== Scaling ======================

    private void RecalculateFromScaleInput()
    {
        if (activeCalculator == null || activeCalculator.ReferenceCount == 0) return;

        float refLen = activeCalculator.RefLength;
        float refWid = activeCalculator.RefWidth;
        float refHei = activeCalculator.RefHeight;
        float refRealVol = activeCalculator.RefRealVolume;
        float refFill = activeCalculator.RefFillPercent;

        if (refLen <= 0f || refWid <= 0f || refHei <= 0f) return;

        switch (scaleMode)
        {
            case ScaleMode.Length:
                scaleFactor = scaleInputValue / refLen;
                break;
            case ScaleMode.Width:
                scaleFactor = scaleInputValue / refWid;
                break;
            case ScaleMode.Height:
                scaleFactor = scaleInputValue / refHei;
                break;
            case ScaleMode.Mass:
                {
                    float shellFrac = shellPercent / 100f;
                    float massFactor = 1000f * (shellFrac + (1f - shellFrac) * (refFill / 100f));
                    if (massFactor <= 0f || refRealVol <= 0f) break;
                    double s3 = (double)scaleInputValue / ((double)refRealVol * massFactor);
                    if (s3 <= 0.0) break;
                    scaleFactor = (float)Math.Pow(s3, 1.0 / 3.0);
                    break;
                }
            case ScaleMode.EffectiveVolume:
                {
                    float shellFrac = shellPercent / 100f;
                    float effFactor = refRealVol * (1f - shellFrac);
                    if (effFactor <= 0f) break;
                    double s3 = (double)scaleInputValue / (double)effFactor;
                    if (s3 <= 0.0) break;
                    scaleFactor = (float)Math.Pow(s3, 1.0 / 3.0);
                    break;
                }
        }

        scaleFactor = Mathf.Max(0.001f, scaleFactor);
        RecalculateAll();
    }

    private void UpdateScaleInputFromCurrent()
    {
        switch (scaleMode)
        {
            case ScaleMode.Length: scaleInputValue = calcLength; break;
            case ScaleMode.Width: scaleInputValue = calcWidth; break;
            case ScaleMode.Height: scaleInputValue = calcHeight; break;
            case ScaleMode.Mass: scaleInputValue = calcTotalMassKg; break;
            case ScaleMode.EffectiveVolume: scaleInputValue = calcEffectiveVolume; break;
        }

        string fmt = (scaleMode == ScaleMode.EffectiveVolume) ? "F6" : "F3";
        scaleInputStr = scaleInputValue.ToString(fmt);
    }

    // ====================== Core Calculation ======================

    private void RecalculateAll()
    {
        if (activeCalculator == null || activeCalculator.ReferenceCount == 0)
        {
            ClearResults();
            return;
        }

        float s = Mathf.Max(0.001f, scaleFactor);
        float s3 = s * s * s;

        float refLen = activeCalculator.RefLength;
        float refWid = activeCalculator.RefWidth;
        float refHei = activeCalculator.RefHeight;
        float refRealVol = activeCalculator.RefRealVolume;
        float refFill = activeCalculator.RefFillPercent;

        // Dimensions
        calcLength = R3(refLen * s);
        calcWidth = R3(refWid * s);
        calcHeight = R3(refHei * s);

        // Volumes
        calcAABBVolume = R6(calcLength * calcWidth * calcHeight);
        calcRealVolume = R6(refRealVol * s3);

        float shellFrac = Mathf.Clamp(shellPercent, 0.001f, 100f) / 100f;
        calcShellVolumeM3 = R6(calcRealVolume * shellFrac);
        calcEffectiveVolume = R6(calcRealVolume - calcShellVolumeM3);
        if (calcEffectiveVolume < 0f) calcEffectiveVolume = 0f;

        // Mass
        calcShellMassKg = R3(calcShellVolumeM3 * 1000f);
        calcInnerMassKg = R3(calcEffectiveVolume * (refFill / 100f) * 1000f);
        calcTotalMassKg = R3(calcShellMassKg + calcInnerMassKg);

        // Durability
        float shellVolDm3 = R3(calcShellVolumeM3 * 1000f);
        int alloyTier = alloyDecoded ? alloyParams.tier : 1;
        calcDurability = R3(shellVolDm3 * TierCoeffs.Get(alloyTier));

        // Build scale data for calculator
        var scaleData = new ModuleScaleData
        {
            scaleFactor = s,
            realVolume = calcRealVolume,
            shellVolumeM3 = calcShellVolumeM3,
            effectiveVolume = calcEffectiveVolume,
            shellPercent = shellPercent,
            fillPercent = refFill,
            shellMassKg = calcShellMassKg,
            innerMassKg = calcInnerMassKg,
            totalMassKg = calcTotalMassKg,
            durability = calcDurability,
            alloyTier = alloyTier
        };

        // Delegate to calculator
        activeCalculator.Calculate(scaleData);

        // Refresh alloy list display
        if (alloyStorage != null)
        {
            alloyDisplayNames = alloyStorage.GetDisplayNames();
            alloyCodes = alloyStorage.GetAllCodes();
            if (alloyCodes.Length > 0 && selectedAlloyIndex >= alloyCodes.Length)
                selectedAlloyIndex = 0;
        }

        // Build code
        currentModuleCode = BuildModuleCode();
    }

    private void ClearResults()
    {
        calcLength = calcWidth = calcHeight = 0f;
        calcAABBVolume = calcRealVolume = calcShellVolumeM3 = calcEffectiveVolume = 0f;
        calcShellMassKg = calcInnerMassKg = calcTotalMassKg = 0f;
        calcDurability = 0f;
        currentModuleCode = "";
    }

    // ====================== Module Code ======================

    private string BuildModuleCode()
    {
        if (activeCalculator == null || activeCalculator.ReferenceCount == 0) return "";

        string type = activeCalculator.ModuleType;
        int tier = activeCalculator.RefModuleTier;
        string faction = string.IsNullOrEmpty(activeCalculator.RefFaction)
            ? "NONE" : activeCalculator.RefFaction;
        int refIndex = activeCalculator.SelectedIndex;
        string alloyCode = GetSelectedAlloyCode() ?? "NONE";
        string specific = activeCalculator.GetCodeSegment();

        // [Type]-[Tier]-[Faction]-[RefIndex]-[Specific]-H[durability]-[X]*[Y]*[Z]-m[mass]:alloy
        return $"{type}-{tier}-{faction}-{refIndex}" +
               $"-{specific}" +
               $"-H{calcDurability:F3}" +
               $"-{calcLength:F3}*{calcWidth:F3}*{calcHeight:F3}" +
               $"-m{calcTotalMassKg:F3}:{alloyCode}";
    }

    // ====================== Craft ======================

    private void OnCraft()
    {
        if (activeCalculator == null || activeCalculator.ReferenceCount == 0) return;

        string alloyCode = GetSelectedAlloyCode();
        if (string.IsNullOrEmpty(alloyCode)) { ShowError("Сплав не выбран"); return; }

        // Check alloy
        if (!alloyStorage.HasEnoughMass(alloyCode, calcShellMassKg))
        {
            ShowError("Недостаточно сплава для оболочки");
            return;
        }

        // Check metal
        int metalTier = activeCalculator.RefModuleTier;
        var metalIdx = GetMetalIndex(metalTier);
        long metalNeededG = (long)Math.Ceiling(calcInnerMassKg * 1000.0);
        if (resourcesStorage.GetGrams(metalIdx) < metalNeededG)
        {
            ShowError("Недостаточно металла");
            return;
        }

        // Check energy
        long energyNeeded = (long)Math.Ceiling(calcTotalMassKg);
        if (resourcesStorage.EnergyUnits < energyNeeded)
        {
            ShowError("Недостаточно энергии");
            return;
        }

        // ─── Consume ───
        alloyStorage.TryConsumeMass(alloyCode, calcShellMassKg);
        resourcesStorage.TryRemoveGrams(metalIdx, metalNeededG);
        resourcesStorage.TryConsumeEnergy(energyNeeded);

        // ─── Destroy old ───
        if (craftedInstance != null)
        {
            Destroy(craftedInstance);
            craftedInstance = null;
        }

        // ─── Instantiate ───
        GameObject prefab = activeCalculator.GetPrefab();
        if (prefab == null) { ShowError("Префаб эталона не найден"); return; }

        Vector3 spawnPos = transform.position + Vector3.up * 2f;
        craftedInstance = Instantiate(prefab, spawnPos, Quaternion.identity);
        craftedInstance.name = $"Crafted_{prefab.name}_T{activeCalculator.RefModuleTier}";

        float s = Mathf.Max(0.001f, scaleFactor);
        craftedInstance.transform.localScale = prefab.transform.localScale * s;

        // Refresh
        RebuildAlloyList();
        RecalculateAll();
        UpdateScaleInputFromCurrent();

        Debug.Log($"[ModuleWorkbench] Crafted: {craftedInstance.name}, Code: {currentModuleCode}");
    }

    // ====================== Reset ======================

    private void ResetToDefaults()
    {
        selectedModuleTypeIndex = 0;
        shellPercent = 5f;
        shellPercentStr = "5.000";
        selectedAlloyIndex = 0;
        scaleMode = ScaleMode.Mass;
        scaleFactor = 1f;
        codeInputField = "";
        errorMessage = "";
        _popupResults.Clear();

        RebuildAllLists();
        RecalculateAll();
        UpdateScaleInputFromCurrent();
    }

    // ====================== Error ======================

    private void ShowError(string msg)
    {
        errorMessage = msg;
        errorTimer = 3f;
    }

    // ====================== Rounding ======================

    private static float R3(float v) => (float)Math.Round(v, 3);
    private static float R6(float v) => (float)Math.Round(v, 6);

    // ====================== Styles ======================

    private GUIStyle _centeredBold;
    private GUIStyle GetCenteredBoldStyle()
    {
        if (_centeredBold == null)
        {
            _centeredBold = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontStyle = FontStyle.Bold,
                fontSize = 16
            };
        }
        return _centeredBold;
    }

    private GUIStyle _boldStyle;
    private GUIStyle GetBoldStyle()
    {
        if (_boldStyle == null)
        {
            _boldStyle = new GUIStyle(GUI.skin.label)
            {
                fontStyle = FontStyle.Bold
            };
        }
        return _boldStyle;
    }
}

// ====================== IMGUI Popup Helper ======================

public static class GenericMenuIMGUI
{
    private static bool _showing;
    private static string[] _options;
    private static int _current;
    private static Action<int> _callback;
    private static Rect _popupRect;
    private static Vector2 _scrollPos;
    private static int _windowId = 987654;

    public static void Show(string[] options, int current, Action<int> callback)
    {
        if (_showing)
        {
            _showing = false;
            return;
        }
        _options = options;
        _current = current;
        _callback = callback;
        _showing = true;
        _scrollPos = Vector2.zero;

        Vector2 mouse = Event.current != null
            ? GUIUtility.GUIToScreenPoint(Event.current.mousePosition)
            : new Vector2(200, 200);
        float h = Mathf.Min(options.Length * 22 + 10, 300);
        _popupRect = new Rect(mouse.x, mouse.y, 280, h);
    }

    public static void DrawPopup()
    {
        if (!_showing || _options == null) return;
        _popupRect = GUI.Window(_windowId, _popupRect, DrawPopupWindow, "");
    }

    private static void DrawPopupWindow(int id)
    {
        _scrollPos = GUILayout.BeginScrollView(_scrollPos);
        for (int i = 0; i < _options.Length; i++)
        {
            string label = (i == _current) ? $"► {_options[i]}" : $"   {_options[i]}";
            if (GUILayout.Button(label, GUI.skin.label))
            {
                _callback?.Invoke(i);
                _showing = false;
            }
        }
        GUILayout.EndScrollView();

        if (Event.current.type == EventType.MouseDown)
        {
            _showing = false;
        }
    }
}