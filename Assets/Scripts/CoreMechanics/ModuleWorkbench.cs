//using System;
//using System.Collections.Generic;
//using UnityEngine;
//using UnityEngine.InputSystem;

///// <summary>
///// UI верстака крафта модулей на IMGUI.
///// Вешается на GameObject верстака. Склады назначаются в инспекторе.
///// Калькуляторы модулей создаются автоматически из баз данных,
///// привязанных к типам в ModuleTypesDatabase.
///// Путь: Assets/Scripts/CoreMechanics/ModuleWorkbench.cs
///// </summary>
//public class ModuleWorkbench : MonoBehaviour
//{
//    [Header("Storage References")]
//    [Tooltip("Alloy storage to pick shell alloy from and consume on craft.")]
//    public AlloyStorage alloyStorage;

//    [Tooltip("Resources storage to consume metal and energy on craft.")]
//    public ResourcesStorage resourcesStorage;

//    [Tooltip("Module storage to save crafted modules.")]
//    public ModuleStorage moduleStorage;

//    // ====================== State ======================
//    private bool panelOpen;
//    private Rect windowRect = new Rect(30, 30, 1000, 612);

//    // Module type selection
//    private int selectedModuleTypeIndex;
//    private string[] moduleTypeNames;

//    // Calculators — создаются из IModuleDatabase
//    private Dictionary<string, IModuleCalculator> calculators;
//    private IModuleCalculator activeCalculator;

//    // Shell
//    private float shellPercent = 5f;
//    private string shellPercentStr = "5.000";

//    // Alloy selection
//    private int selectedAlloyIndex;
//    private string[] alloyDisplayNames;
//    private string[] alloyCodes;

//    // Scaling
//    private enum ScaleMode { Length, Width, Height, Mass, EffectiveVolume }
//    private ScaleMode scaleMode = ScaleMode.Mass;
//    private float scaleInputValue;
//    private string scaleInputStr = "";

//    // Computed results (common)
//    private float scaleFactor = 1f;
//    private float calcLength, calcWidth, calcHeight;
//    private float calcAABBVolume, calcRealVolume;
//    private float calcShellVolumeM3, calcEffectiveVolume;
//    private float calcShellMassKg, calcInnerMassKg, calcTotalMassKg;
//    private float calcDurability;

//    // Alloy params (decoded from selected alloy)
//    private bool alloyDecoded;
//    private AlloyCode.AlloyParams alloyParams;

//    // Code
//    private string currentModuleCode = "";
//    private string codeInputField = "";

//    // Error
//    private string errorMessage = "";
//    private float errorTimer;

//    // Crafted object
//    private GameObject craftedInstance;

//    // Scroll
//    private Vector2 scrollPos;

//    // ====================== Init ======================
//    private void Awake()
//    {
//        BuildCalculators();
//    }

//    /// <summary>
//    /// Автоматически создаёт калькуляторы из всех баз данных,
//    /// привязанных к типам в ModuleTypesDatabase.
//    /// </summary>
//    private void BuildCalculators()
//    {
//        calculators = new Dictionary<string, IModuleCalculator>();

//        var db = ModuleTypesDatabase.Instance;
//        if (db == null)
//        {
//            Debug.LogWarning("[ModuleWorkbench] ModuleTypesDatabase not found!");
//            return;
//        }

//        foreach (var moduleDb in db.GetAllDatabases())
//        {
//            string typeName = moduleDb.ModuleType;
//            if (string.IsNullOrEmpty(typeName)) continue;

//            try
//            {
//                var calc = moduleDb.CreateCalculator();
//                if (calc != null)
//                {
//                    calculators[typeName] = calc;
//                }
//            }
//            catch (System.Exception ex)
//            {
//                Debug.LogError($"[ModuleWorkbench] Failed to create calculator for '{typeName}': {ex.Message}");
//            }
//        }
//    }

//    // ====================== Open / Close ======================
//    public void OpenPanel()
//    {
//        panelOpen = true;
//        // Перестраиваем калькуляторы при каждом открытии — на случай если БД изменились
//        BuildCalculators();
//        RebuildAllLists();
//        ResetToDefaults();
//    }

//    public void ClosePanel()
//    {
//        panelOpen = false;
//        WorkbenchPopup.Hide();
//    }

//    private void Update()
//    {
//        if (panelOpen && Keyboard.current != null)
//        {
//            if (Keyboard.current.escapeKey.wasPressedThisFrame)
//                ClosePanel();
//        }

//        if (errorTimer > 0f)
//        {
//            errorTimer -= Time.deltaTime;
//            if (errorTimer <= 0f) errorMessage = "";
//        }
//    }

//    // ====================== OnGUI ======================
//    private void OnGUI()
//    {
//        if (!panelOpen) return;

//        // Если popup открыт и кликнули вне него — закрыть
//        if (WorkbenchPopup.IsShowing && Event.current.type == EventType.MouseDown)
//        {
//            Vector2 screenMouse = Event.current.mousePosition;
//            if (!WorkbenchPopup.PopupRect.Contains(screenMouse))
//            {
//                WorkbenchPopup.Hide();
//                Event.current.Use();
//            }
//        }

//        windowRect = GUI.Window(298765, windowRect, DrawWindow, "Верстак модулей");
//        WorkbenchPopup.DrawPopup();
//    }

//    // ====================== Main Window ======================
//    private void DrawWindow(int id)
//    {
//        GUI.DragWindow(new Rect(0, 0, 10000, 20));

//        scrollPos = GUILayout.BeginScrollView(scrollPos);
//        GUILayout.BeginVertical();

//        // ─── Error ───
//        if (!string.IsNullOrEmpty(errorMessage))
//        {
//            Color prev = GUI.color;
//            GUI.color = Color.red;
//            GUILayout.Label(errorMessage, GetCenteredBoldStyle());
//            GUI.color = prev;
//            GUILayout.Space(2);
//        }

//        // ─── Code area ───
//        DrawCodeSection();
//        GUILayout.Space(3);

//        float halfW = (windowRect.width - 30) * 0.5f;

//        GUILayout.BeginHorizontal();

//        // ══════ Left column ══════
//        GUILayout.BeginVertical(GUILayout.Width(halfW));
//        DrawSelectionSection();
//        GUILayout.Space(3);
//        DrawShellSection();
//        GUILayout.Space(3);
//        DrawScalingSection();
//        GUILayout.EndVertical();

//        // ══════ Right column ══════
//        GUILayout.BeginVertical(GUILayout.Width(halfW));
//        DrawComputedSection();
//        GUILayout.Space(3);
//        DrawModuleSpecificSection();
//        GUILayout.Space(3);
//        DrawAlloyParamsSection();
//        GUILayout.EndVertical();

//        GUILayout.EndHorizontal();

//        GUILayout.Space(4);

//        // ─── Costs & Buttons ───
//        DrawCostsAndButtons();

//        GUILayout.EndVertical();
//        GUILayout.EndScrollView();
//    }

//    // ====================== Sections ======================

//    private void DrawCodeSection()
//    {
//        GUILayout.BeginHorizontal();
//        GUILayout.Label("Код модуля:", GUILayout.Width(85));
//        GUI.enabled = false;
//        GUILayout.TextField(currentModuleCode, GUILayout.Width(windowRect.width - 300));
//        GUI.enabled = true;

//        if (GUILayout.Button("Копировать", GUILayout.Width(80)))
//        {
//            if (!string.IsNullOrEmpty(currentModuleCode))
//                GUIUtility.systemCopyBuffer = currentModuleCode;
//        }
//        GUILayout.EndHorizontal();

//        GUILayout.BeginHorizontal();
//        GUILayout.Label("Вставить код:", GUILayout.Width(85));
//        codeInputField = GUILayout.TextField(codeInputField, GUILayout.Width(windowRect.width - 380));

//        if (GUILayout.Button("Вставить", GUILayout.Width(70)))
//        {
//            codeInputField = (GUIUtility.systemCopyBuffer ?? "").Trim();
//        }
//        if (GUILayout.Button("Применить", GUILayout.Width(75)))
//        {
//            ShowError("Система кодов модулей в разработке");
//        }
//        GUILayout.EndHorizontal();
//    }

//    private void DrawSelectionSection()
//    {
//        GUILayout.Label("Выбор модуля", GetBoldStyle());

//        // Module type
//        GUILayout.BeginHorizontal();
//        GUILayout.Label("Тип модуля:", GUILayout.Width(130));
//        if (moduleTypeNames != null && moduleTypeNames.Length > 0)
//        {
//            int newIdx = DrawDropdown("wb_moduleType", selectedModuleTypeIndex, moduleTypeNames);
//            if (newIdx != selectedModuleTypeIndex)
//            {
//                selectedModuleTypeIndex = newIdx;
//                OnModuleTypeChanged();
//            }
//        }
//        GUILayout.EndHorizontal();

//        // Reference — dropdown
//        GUILayout.BeginHorizontal();
//        GUILayout.Label("Эталонный модуль:", GUILayout.Width(130));
//        if (activeCalculator != null && activeCalculator.ReferenceCount > 0)
//        {
//            string[] refNames = activeCalculator.GetReferenceNames();
//            int curIdx = activeCalculator.SelectedIndex;
//            int newIdx = DrawDropdown("wb_moduleRef", curIdx, refNames);
//            if (newIdx != curIdx)
//            {
//                activeCalculator.SelectReference(newIdx);
//                OnReferenceChanged();
//            }
//        }
//        else
//        {
//            GUILayout.Label("(Нет эталонов для этого типа)");
//        }
//        GUILayout.EndHorizontal();

//        // Show reference params
//        if (activeCalculator != null && activeCalculator.ReferenceCount > 0)
//        {
//            string faction = string.IsNullOrEmpty(activeCalculator.RefFaction) ? "—" : activeCalculator.RefFaction;
//            GUILayout.Label($"  Тир: {activeCalculator.RefModuleTier}   " +
//                            $"Фракция: {faction}   " +
//                            $"Fill: {activeCalculator.RefFillPercent:F1}%   " +
//                            $"VCoeff: {activeCalculator.RefVolumeCoefficientPercent:F1}%");
//        }
//    }

//    private void DrawShellSection()
//    {
//        GUILayout.Label("Оболочка", GetBoldStyle());

//        // Shell percent
//        GUILayout.BeginHorizontal();
//        GUILayout.Label("Shell Volume (%):", GUILayout.Width(130));

//        string newStr = GUILayout.TextField(shellPercentStr, GUILayout.Width(70));
//        if (newStr != shellPercentStr)
//        {
//            shellPercentStr = newStr;
//            if (float.TryParse(shellPercentStr, out float val))
//            {
//                shellPercent = Mathf.Clamp(val, 0.001f, 100f);
//                RecalculateAll();
//                UpdateScaleInputFromCurrent();
//            }
//        }

//        float sliderVal = GUILayout.HorizontalSlider(shellPercent, 0.001f, 100f, GUILayout.Width(160));
//        if (Mathf.Abs(sliderVal - shellPercent) > 0.0005f)
//        {
//            shellPercent = (float)Math.Round(sliderVal, 3);
//            shellPercentStr = shellPercent.ToString("F3");
//            RecalculateAll();
//            UpdateScaleInputFromCurrent();
//        }

//        GUILayout.Label($"{shellPercent:F3}%", GUILayout.Width(65));
//        GUILayout.EndHorizontal();

//        // Alloy selection — dropdown
//        GUILayout.BeginHorizontal();
//        GUILayout.Label("Сплав оболочки:", GUILayout.Width(130));
//        if (alloyDisplayNames != null && alloyDisplayNames.Length > 0)
//        {
//            int newIdx = DrawDropdown("wb_alloySelect", selectedAlloyIndex, alloyDisplayNames);
//            if (newIdx != selectedAlloyIndex)
//            {
//                selectedAlloyIndex = newIdx;
//                OnAlloyChanged();
//            }
//        }
//        else
//        {
//            Color prev = GUI.color;
//            GUI.color = Color.yellow;
//            GUILayout.Label("(AlloyStorage пуст)");
//            GUI.color = prev;
//        }
//        GUILayout.EndHorizontal();
//    }

//    private void DrawScalingSection()
//    {
//        GUILayout.Label("Масштабирование", GetBoldStyle());

//        GUILayout.BeginHorizontal();
//        GUILayout.Label("Параметр:", GUILayout.Width(130));
//        string[] modeNames = { "Длина", "Ширина", "Высота", "Масса", "Эфф.объём" };
//        int newMode = GUILayout.SelectionGrid((int)scaleMode, modeNames, modeNames.Length);
//        if (newMode != (int)scaleMode)
//        {
//            scaleMode = (ScaleMode)newMode;
//            UpdateScaleInputFromCurrent();
//        }
//        GUILayout.EndHorizontal();

//        GUILayout.BeginHorizontal();
//        GUILayout.Label("Значение:", GUILayout.Width(130));
//        string newScaleStr = GUILayout.TextField(scaleInputStr, GUILayout.Width(100));
//        if (newScaleStr != scaleInputStr)
//        {
//            scaleInputStr = newScaleStr;
//            if (float.TryParse(scaleInputStr, out float val) && val > 0f)
//            {
//                scaleInputValue = val;
//                RecalculateFromScaleInput();
//            }
//        }

//        string unit = scaleMode == ScaleMode.Mass ? "кг" :
//                      scaleMode == ScaleMode.EffectiveVolume ? "м³" : "м";
//        GUILayout.Label(unit, GUILayout.Width(25));

//        if (GUILayout.Button("Сброс масштаба", GUILayout.Width(110)))
//        {
//            scaleFactor = 1f;
//            RecalculateAll();
//            UpdateScaleInputFromCurrent();
//        }
//        GUILayout.EndHorizontal();
//    }

//    private void DrawComputedSection()
//    {
//        GUILayout.Label("Общие параметры", GetBoldStyle());

//        LabelPair("Длина (X):", $"{calcLength:F3} м");
//        LabelPair("Ширина (Z):", $"{calcWidth:F3} м");
//        LabelPair("Высота (Y):", $"{calcHeight:F3} м");
//        GUILayout.Space(2);
//        LabelPair("AABB объём:", $"{calcAABBVolume:F6} м³");
//        LabelPair("Real объём:", $"{calcRealVolume:F6} м³");
//        LabelPair("Shell объём:", $"{calcShellVolumeM3:F6} м³");
//        LabelPair("Effective объём:", $"{calcEffectiveVolume:F6} м³");
//        GUILayout.Space(2);
//        LabelPair("Масса оболочки:", $"{calcShellMassKg:F3} кг");
//        LabelPair("Масса начинки:", $"{calcInnerMassKg:F3} кг");
//        LabelPair("Общая масса:", $"{calcTotalMassKg:F3} кг");
//        GUILayout.Space(2);

//        Color prevC = GUI.color;
//        GUI.color = Color.cyan;
//        LabelPair("Прочность:", $"{calcDurability:F3}");
//        GUI.color = prevC;
//    }

//    private void DrawModuleSpecificSection()
//    {
//        if (activeCalculator == null) return;
//        GUILayout.Label($"Параметры: {activeCalculator.ModuleType}", GetBoldStyle());
//        activeCalculator.DrawResultsGUI();
//    }

//    private void DrawAlloyParamsSection()
//    {
//        GUILayout.Label("Параметры сплава оболочки", GetBoldStyle());

//        if (!alloyDecoded || alloyCodes == null || alloyCodes.Length == 0)
//        {
//            GUILayout.Label("(Сплав не выбран или не распознан)");
//            return;
//        }

//        GUILayout.Label($"Тир сплава: {alloyParams.tier}   " +
//                        $"Химикаты: {(alloyParams.useChemicals ? "Да" : "Нет")}   " +
//                        $"Наниты: {(alloyParams.useNanites ? "Да" : "Нет")}");

//        float colW = (windowRect.width - 40) * 0.24f;

//        GUILayout.BeginHorizontal();
//        DrawAlloyColumn("Кинетика", alloyParams.kineticAbsorption, alloyParams.kineticResistance, colW);
//        DrawAlloyColumn("Термика", alloyParams.thermalAbsorption, alloyParams.thermalResistance, colW);
//        DrawAlloyColumn("Химия", alloyParams.chemicalAbsorption, alloyParams.chemicalResistance, colW);
//        DrawAlloyColumn("Энергия", alloyParams.energyAbsorption, alloyParams.energyResistance, colW);
//        GUILayout.EndHorizontal();
//    }

//    private void DrawAlloyColumn(string title, int absorb, float resist, float width)
//    {
//        GUILayout.BeginVertical("box", GUILayout.Width(width));
//        GUILayout.Label(title, GetBoldStyle());
//        GUILayout.Label($"Погл: {absorb}");
//        GUILayout.Label($"Сопр: {resist:F1}%");
//        GUILayout.EndVertical();
//    }

//    private void DrawCostsAndButtons()
//    {
//        GUILayout.BeginHorizontal("box");

//        // Left: costs
//        GUILayout.BeginVertical(GUILayout.Width(windowRect.width * 0.6f - 10));
//        GUILayout.Label("Стоимость изготовления", GetBoldStyle());

//        string alloyCode = GetSelectedAlloyCode();
//        float alloyAvailable = alloyCode != null && alloyStorage != null
//            ? (float)alloyStorage.GetMass(alloyCode) : 0f;
//        bool enoughAlloy = alloyCode != null && alloyStorage != null &&
//            alloyStorage.HasEnoughMass(alloyCode, calcShellMassKg);

//        int metalTier = activeCalculator != null ? activeCalculator.RefModuleTier : 1;
//        var metalIdx = GetMetalIndex(metalTier);
//        float metalAvailable = resourcesStorage != null
//            ? (float)(resourcesStorage.GetGrams(metalIdx) / 1000.0) : 0f;
//        float metalNeeded = calcInnerMassKg;
//        bool enoughMetal = metalAvailable >= metalNeeded - 0.001f;

//        long energyNeeded = (long)Math.Ceiling(calcTotalMassKg);
//        long energyAvailable = resourcesStorage != null ? resourcesStorage.EnergyUnits : 0;
//        bool enoughEnergy = energyAvailable >= energyNeeded;

//        DrawCostLine($"Сплав ({alloyCode ?? "—"}):", calcShellMassKg, alloyAvailable, "кг", enoughAlloy);
//        DrawCostLine($"Металл T{metalTier}:", metalNeeded, metalAvailable, "кг", enoughMetal);
//        DrawCostLineEnergy("Энергия:", energyNeeded, energyAvailable, enoughEnergy);

//        GUILayout.EndVertical();

//        // Right: buttons
//        GUILayout.BeginVertical(GUILayout.Width(windowRect.width * 0.4f - 10));
//        GUILayout.FlexibleSpace();

//        bool canCraft = activeCalculator != null &&
//                        activeCalculator.ReferenceCount > 0 &&
//                        alloyCode != null &&
//                        enoughAlloy && enoughMetal && enoughEnergy &&
//                        calcEffectiveVolume > 0.000001f;

//        if (!canCraft) GUI.enabled = false;
//        if (GUILayout.Button("Изготовить", GUILayout.Height(28), GUILayout.Width(150)))
//        {
//            OnCraft();
//        }
//        GUI.enabled = true;

//        GUILayout.Space(8);

//        if (GUILayout.Button("Сброс", GUILayout.Height(28), GUILayout.Width(150)))
//        {
//            ResetToDefaults();
//        }

//        GUILayout.FlexibleSpace();
//        GUILayout.EndVertical();

//        GUILayout.EndHorizontal();
//    }

//    // ====================== Helpers ======================
//    private void LabelPair(string left, string right)
//    {
//        GUILayout.BeginHorizontal();
//        GUILayout.Label(left, GUILayout.Width(130));
//        GUILayout.Label(right);
//        GUILayout.EndHorizontal();
//    }

//    private void DrawCostLine(string label, float needed, float available, string unit, bool enough)
//    {
//        GUILayout.BeginHorizontal();
//        GUILayout.Label(label, GUILayout.Width(180));
//        GUILayout.Label($"{needed:F3} {unit}", GUILayout.Width(110));
//        Color prev = GUI.color;
//        if (!enough) GUI.color = Color.red;
//        GUILayout.Label($"(есть: {available:F3})", GUILayout.Width(150));
//        GUI.color = prev;
//        GUILayout.EndHorizontal();
//    }

//    private void DrawCostLineEnergy(string label, long needed, long available, bool enough)
//    {
//        GUILayout.BeginHorizontal();
//        GUILayout.Label(label, GUILayout.Width(180));
//        GUILayout.Label($"{needed}", GUILayout.Width(110));
//        Color prev = GUI.color;
//        if (!enough) GUI.color = Color.red;
//        GUILayout.Label($"(есть: {available})", GUILayout.Width(150));
//        GUI.color = prev;
//        GUILayout.EndHorizontal();
//    }

//    // ====================== Dropdown ======================
//    private Dictionary<string, int> _pendingSelections = new Dictionary<string, int>();

//    private int DrawDropdown(string tag, int selected, string[] options)
//    {
//        if (options == null || options.Length == 0) return selected;
//        selected = Mathf.Clamp(selected, 0, options.Length - 1);

//        string current = options[selected];

//        if (GUILayout.Button(current, GUI.skin.button, GUILayout.MinWidth(120)))
//        {
//            Vector2 screenPos = GUIUtility.GUIToScreenPoint(Event.current.mousePosition);
//            string capturedTag = tag;
//            string[] capturedOptions = options;

//            WorkbenchPopup.Show(capturedOptions, selected, screenPos, idx =>
//            {
//                _pendingSelections[capturedTag] = idx;
//            });
//        }

//        if (_pendingSelections.TryGetValue(tag, out int result))
//        {
//            _pendingSelections.Remove(tag);
//            return Mathf.Clamp(result, 0, options.Length - 1);
//        }

//        return selected;
//    }

//    private static ResourcesStorage.ResourceIndex GetMetalIndex(int tier)
//    {
//        return (ResourcesStorage.ResourceIndex)(
//            (int)ResourcesStorage.ResourceType.Metal * ResourcesStorage.TiersPerType + (tier - 1));
//    }

//    // ====================== List Building ======================
//    private void RebuildAllLists()
//    {
//        RebuildModuleTypeList();
//        RebuildAlloyList();
//        OnModuleTypeChanged();
//    }

//    private void RebuildModuleTypeList()
//    {
//        // Показываем только те типы, для которых есть калькулятор
//        var available = new List<string>();

//        var db = ModuleTypesDatabase.Instance;
//        if (db != null)
//        {
//            foreach (var moduleDb in db.GetAllDatabases())
//            {
//                string typeName = moduleDb.ModuleType;
//                if (string.IsNullOrEmpty(typeName)) continue;
//                if (calculators.ContainsKey(typeName))
//                    available.Add(typeName);
//            }
//        }

//        if (available.Count > 0)
//        {
//            moduleTypeNames = available.ToArray();
//        }
//        else
//        {
//            moduleTypeNames = new string[] { "(Нет доступных типов)" };
//        }

//        selectedModuleTypeIndex = 0;
//    }

//    private void RebuildAlloyList()
//    {
//        if (alloyStorage == null || alloyStorage.Count == 0)
//        {
//            alloyDisplayNames = new string[0];
//            alloyCodes = new string[0];
//            selectedAlloyIndex = 0;
//            alloyDecoded = false;
//            return;
//        }

//        alloyDisplayNames = alloyStorage.GetDisplayNames();
//        alloyCodes = alloyStorage.GetAllCodes();
//        selectedAlloyIndex = 0;
//        OnAlloyChanged();
//    }

//    // ====================== Selection Callbacks ======================
//    private void OnModuleTypeChanged()
//    {
//        activeCalculator = null;

//        if (moduleTypeNames == null || moduleTypeNames.Length == 0) return;
//        if (selectedModuleTypeIndex < 0 || selectedModuleTypeIndex >= moduleTypeNames.Length) return;

//        string typeName = moduleTypeNames[selectedModuleTypeIndex];
//        if (calculators.TryGetValue(typeName, out var calc))
//        {
//            activeCalculator = calc;
//            if (activeCalculator.ReferenceCount > 0)
//                activeCalculator.SelectReference(0);
//        }

//        scaleFactor = 1f;
//        RecalculateAll();
//        UpdateScaleInputFromCurrent();
//    }

//    private void OnReferenceChanged()
//    {
//        scaleFactor = 1f;
//        RecalculateAll();
//        UpdateScaleInputFromCurrent();
//    }

//    private void OnAlloyChanged()
//    {
//        alloyDecoded = false;
//        if (alloyCodes != null && selectedAlloyIndex >= 0 && selectedAlloyIndex < alloyCodes.Length)
//        {
//            string code = alloyCodes[selectedAlloyIndex];
//            if (AlloyCode.Decode(code, out AlloyCode.AlloyParams p))
//            {
//                alloyParams = p;
//                alloyDecoded = true;
//            }
//        }
//        RecalculateAll();
//    }

//    private string GetSelectedAlloyCode()
//    {
//        if (alloyCodes == null || alloyCodes.Length == 0) return null;
//        if (selectedAlloyIndex < 0 || selectedAlloyIndex >= alloyCodes.Length) return null;
//        return alloyCodes[selectedAlloyIndex];
//    }

//    // ====================== Scaling ======================
//    private void RecalculateFromScaleInput()
//    {
//        if (activeCalculator == null || activeCalculator.ReferenceCount == 0) return;

//        float refLen = activeCalculator.RefLength;
//        float refWid = activeCalculator.RefWidth;
//        float refHei = activeCalculator.RefHeight;
//        float refRealVol = activeCalculator.RefRealVolume;
//        float refFill = activeCalculator.RefFillPercent;

//        if (refLen <= 0f || refWid <= 0f || refHei <= 0f) return;

//        switch (scaleMode)
//        {
//            case ScaleMode.Length:
//                scaleFactor = scaleInputValue / refLen;
//                break;
//            case ScaleMode.Width:
//                scaleFactor = scaleInputValue / refWid;
//                break;
//            case ScaleMode.Height:
//                scaleFactor = scaleInputValue / refHei;
//                break;
//            case ScaleMode.Mass:
//                {
//                    float shellFrac = shellPercent / 100f;
//                    float massFactor = 1000f * (shellFrac + (1f - shellFrac) * (refFill / 100f));
//                    if (massFactor <= 0f || refRealVol <= 0f) break;
//                    double s3 = (double)scaleInputValue / ((double)refRealVol * massFactor);
//                    if (s3 <= 0.0) break;
//                    scaleFactor = (float)Math.Pow(s3, 1.0 / 3.0);
//                    break;
//                }
//            case ScaleMode.EffectiveVolume:
//                {
//                    float shellFrac = shellPercent / 100f;
//                    float effFactor = refRealVol * (1f - shellFrac);
//                    if (effFactor <= 0f) break;
//                    double s3 = (double)scaleInputValue / (double)effFactor;
//                    if (s3 <= 0.0) break;
//                    scaleFactor = (float)Math.Pow(s3, 1.0 / 3.0);
//                    break;
//                }
//        }

//        scaleFactor = Mathf.Max(0.001f, scaleFactor);
//        RecalculateAll();
//    }

//    private void UpdateScaleInputFromCurrent()
//    {
//        switch (scaleMode)
//        {
//            case ScaleMode.Length: scaleInputValue = calcLength; break;
//            case ScaleMode.Width: scaleInputValue = calcWidth; break;
//            case ScaleMode.Height: scaleInputValue = calcHeight; break;
//            case ScaleMode.Mass: scaleInputValue = calcTotalMassKg; break;
//            case ScaleMode.EffectiveVolume: scaleInputValue = calcEffectiveVolume; break;
//        }

//        string fmt = (scaleMode == ScaleMode.EffectiveVolume) ? "F6" : "F3";
//        scaleInputStr = scaleInputValue.ToString(fmt);
//    }

//    // ====================== Core Calculation ======================
//    private void RecalculateAll()
//    {
//        if (activeCalculator == null || activeCalculator.ReferenceCount == 0)
//        {
//            ClearResults();
//            return;
//        }

//        float s = Mathf.Max(0.001f, scaleFactor);
//        float s3 = s * s * s;

//        float refLen = activeCalculator.RefLength;
//        float refWid = activeCalculator.RefWidth;
//        float refHei = activeCalculator.RefHeight;
//        float refRealVol = activeCalculator.RefRealVolume;
//        float refFill = activeCalculator.RefFillPercent;

//        // Dimensions
//        calcLength = R3(refLen * s);
//        calcWidth = R3(refWid * s);
//        calcHeight = R3(refHei * s);

//        // Volumes
//        calcAABBVolume = R6(calcLength * calcWidth * calcHeight);
//        calcRealVolume = R6(refRealVol * s3);

//        float shellFrac = Mathf.Clamp(shellPercent, 0.001f, 100f) / 100f;
//        calcShellVolumeM3 = R6(calcRealVolume * shellFrac);
//        calcEffectiveVolume = R6(calcRealVolume - calcShellVolumeM3);
//        if (calcEffectiveVolume < 0f) calcEffectiveVolume = 0f;

//        // Mass
//        calcShellMassKg = R3(calcShellVolumeM3 * 1000f);
//        calcInnerMassKg = R3(calcEffectiveVolume * (refFill / 100f) * 1000f);
//        calcTotalMassKg = R3(calcShellMassKg + calcInnerMassKg);

//        // Durability
//        float shellVolDm3 = R3(calcShellVolumeM3 * 1000f);
//        int alloyTier = alloyDecoded ? alloyParams.tier : 1;
//        calcDurability = R3(shellVolDm3 * TierCoeffs.Get(alloyTier));

//        // Build scale data for calculator
//        var scaleData = new ModuleScaleData
//        {
//            scaleFactor = s,
//            realVolume = calcRealVolume,
//            shellVolumeM3 = calcShellVolumeM3,
//            effectiveVolume = calcEffectiveVolume,
//            shellPercent = shellPercent,
//            fillPercent = refFill,
//            shellMassKg = calcShellMassKg,
//            innerMassKg = calcInnerMassKg,
//            totalMassKg = calcTotalMassKg,
//            durability = calcDurability,
//            alloyTier = alloyTier
//        };

//        // Delegate to calculator for module-specific calculations
//        activeCalculator.Calculate(scaleData);

//        // Build code
//        currentModuleCode = BuildModuleCode();
//    }

//    private void ClearResults()
//    {
//        calcLength = calcWidth = calcHeight = 0f;
//        calcAABBVolume = calcRealVolume = calcShellVolumeM3 = calcEffectiveVolume = 0f;
//        calcShellMassKg = calcInnerMassKg = calcTotalMassKg = 0f;
//        calcDurability = 0f;
//        currentModuleCode = "";
//    }

//    // ====================== Module Code ======================
//    private string BuildModuleCode()
//    {
//        if (activeCalculator == null || activeCalculator.ReferenceCount == 0) return "";

//        string type = activeCalculator.ModuleType;
//        int tier = activeCalculator.RefModuleTier;
//        string faction = string.IsNullOrEmpty(activeCalculator.RefFaction)
//            ? "NONE" : activeCalculator.RefFaction;
//        int refIndex = activeCalculator.SelectedIndex;
//        string alloyCode = GetSelectedAlloyCode() ?? "NONE";
//        string specific = activeCalculator.GetCodeSegment();

//        return $"{type}-{tier}-{faction}-{refIndex}" +
//               $"-{specific}" +
//               $"-H{calcDurability:F3}" +
//               $"-{calcLength:F3}*{calcWidth:F3}*{calcHeight:F3}" +
//               $"-m{calcTotalMassKg:F3}:{alloyCode}";
//    }

//    // ====================== Craft ======================
//    private void OnCraft()
//    {
//        if (activeCalculator == null || activeCalculator.ReferenceCount == 0) return;

//        string alloyCode = GetSelectedAlloyCode();
//        if (string.IsNullOrEmpty(alloyCode)) { ShowError("Сплав не выбран"); return; }

//        // Сохраняем данные до возможного пересчёта
//        string craftCode = currentModuleCode;
//        float craftShellMassKg = calcShellMassKg;
//        float craftInnerMassKg = calcInnerMassKg;
//        float craftTotalMassKg = calcTotalMassKg;

//        // Check alloy
//        if (!alloyStorage.HasEnoughMass(alloyCode, craftShellMassKg))
//        {
//            ShowError("Недостаточно сплава для оболочки");
//            return;
//        }

//        // Check metal
//        int metalTier = activeCalculator.RefModuleTier;
//        var metalIdx = GetMetalIndex(metalTier);
//        long metalNeededG = (long)Math.Ceiling(craftInnerMassKg * 1000.0);
//        if (resourcesStorage.GetGrams(metalIdx) < metalNeededG)
//        {
//            ShowError("Недостаточно металла");
//            return;
//        }

//        // Check energy
//        long energyNeeded = (long)Math.Ceiling(craftTotalMassKg);
//        if (resourcesStorage.EnergyUnits < energyNeeded)
//        {
//            ShowError("Недостаточно энергии");
//            return;
//        }

//        // ─── Create ModuleData ───
//        var scaleData = new ModuleScaleData
//        {
//            scaleFactor = Mathf.Max(0.001f, scaleFactor),
//            realVolume = calcRealVolume,
//            shellVolumeM3 = calcShellVolumeM3,
//            effectiveVolume = calcEffectiveVolume,
//            shellPercent = shellPercent,
//            fillPercent = activeCalculator.RefFillPercent,
//            shellMassKg = craftShellMassKg,
//            innerMassKg = craftInnerMassKg,
//            totalMassKg = craftTotalMassKg,
//            durability = calcDurability,
//            alloyTier = alloyDecoded ? alloyParams.tier : 1
//        };

//        ModuleData moduleData = activeCalculator.CreateModuleData(scaleData);
//        if (moduleData == null)
//        {
//            ShowError("Ошибка создания данных модуля");
//            return;
//        }

//        // Fill common fields
//        string faction = string.IsNullOrEmpty(activeCalculator.RefFaction)
//            ? "NONE" : activeCalculator.RefFaction;
//        string refName = "";
//        GameObject prefab = activeCalculator.GetPrefab();
//        if (prefab != null) refName = prefab.name;

//        moduleData.FillCommon(
//            activeCalculator.ModuleType,
//            activeCalculator.RefModuleTier,
//            faction,
//            activeCalculator.SelectedIndex,
//            refName,
//            alloyCode,
//            alloyDecoded ? alloyParams.tier : 1,
//            shellPercent,
//            Mathf.Max(0.001f, scaleFactor),
//            activeCalculator.RefFillPercent,
//            calcLength, calcWidth, calcHeight,
//            calcAABBVolume, calcRealVolume, calcShellVolumeM3, calcEffectiveVolume,
//            craftShellMassKg, craftInnerMassKg, craftTotalMassKg,
//            calcDurability,
//            craftCode
//        );

//        // ─── Consume resources ───
//        alloyStorage.TryConsumeMass(alloyCode, craftShellMassKg);
//        resourcesStorage.TryRemoveGrams(metalIdx, metalNeededG);
//        resourcesStorage.TryConsumeEnergy(energyNeeded);

//        // ─── Destroy old ───
//        if (craftedInstance != null)
//        {
//            Destroy(craftedInstance);
//            craftedInstance = null;
//        }

//        // ─── Instantiate ───
//        if (prefab == null) { ShowError("Префаб эталона не найден"); return; }

//        Vector3 spawnPos = transform.position + Vector3.up * 2f;
//        craftedInstance = Instantiate(prefab, spawnPos, Quaternion.identity);
//        craftedInstance.name = $"Crafted_{prefab.name}_T{activeCalculator.RefModuleTier}";

//        float s = Mathf.Max(0.001f, scaleFactor);
//        craftedInstance.transform.localScale = prefab.transform.localScale * s;

//        // ─── Remove Standard* component, add CraftedModule ───
//        // Удаляем стандартный компонент (если есть)
//        var oldES = craftedInstance.GetComponent<StandardEnergyStorage>();
//        if (oldES != null) Destroy(oldES);

//        var oldGen = craftedInstance.GetComponent<StandardGenerator>();
//        if (oldGen != null) Destroy(oldGen);

//        // Добавляем CraftedModule
//        var craftedComp = craftedInstance.AddComponent<CraftedModule>();
//        craftedComp.SetData(moduleData);

//        // ─── Save to ModuleStorage ───
//        if (moduleStorage != null)
//        {
//            string id = moduleStorage.AddModule(moduleData);
//            Debug.Log($"[ModuleWorkbench] Saved to ModuleStorage, ID: {id}");
//        }
//        else
//        {
//            Debug.LogWarning("[ModuleWorkbench] ModuleStorage not assigned! Module not saved.");
//        }

//        // ─── Refresh UI ───
//        RebuildAlloyList();
//        RecalculateAll();
//        UpdateScaleInputFromCurrent();

//        Debug.Log($"[ModuleWorkbench] Crafted: {craftedInstance.name}, Code: {craftCode}");
//    }

//    // ====================== Reset ======================
//    private void ResetToDefaults()
//    {
//        selectedModuleTypeIndex = 0;
//        shellPercent = 5f;
//        shellPercentStr = "5.000";
//        selectedAlloyIndex = 0;
//        scaleMode = ScaleMode.Mass;
//        scaleFactor = 1f;
//        codeInputField = "";
//        errorMessage = "";
//        _pendingSelections.Clear();
//        WorkbenchPopup.Hide();

//        RebuildAllLists();
//        RecalculateAll();
//        UpdateScaleInputFromCurrent();
//    }

//    // ====================== Error ======================
//    private void ShowError(string msg)
//    {
//        errorMessage = msg;
//        errorTimer = 3f;
//    }

//    // ====================== Rounding ======================
//    private static float R3(float v) => (float)Math.Round(v, 3);
//    private static float R6(float v) => (float)Math.Round(v, 6);

//    // ====================== Styles ======================
//    private GUIStyle _centeredBold;
//    private GUIStyle GetCenteredBoldStyle()
//    {
//        if (_centeredBold == null)
//        {
//            _centeredBold = new GUIStyle(GUI.skin.label)
//            {
//                alignment = TextAnchor.MiddleCenter,
//                fontStyle = FontStyle.Bold,
//                fontSize = 16
//            };
//        }
//        return _centeredBold;
//    }

//    private GUIStyle _boldStyle;
//    private GUIStyle GetBoldStyle()
//    {
//        if (_boldStyle == null)
//        {
//            _boldStyle = new GUIStyle(GUI.skin.label)
//            {
//                fontStyle = FontStyle.Bold
//            };
//        }
//        return _boldStyle;
//    }
//}

//// ====================== Workbench Popup ======================
//public static class WorkbenchPopup
//{
//    private static bool _showing;
//    private static string[] _options;
//    private static int _current;
//    private static Action<int> _callback;
//    private static Rect _popupRect;
//    private static Vector2 _scrollPos;
//    private static int _windowId = 987655;
//    private static int _showFrame;

//    public static bool IsShowing => _showing;
//    public static Rect PopupRect => _popupRect;

//    public static void Show(string[] options, int current, Vector2 screenPos, Action<int> callback)
//    {
//        _options = options;
//        _current = current;
//        _callback = callback;
//        _showing = true;
//        _scrollPos = Vector2.zero;
//        _showFrame = Time.frameCount;

//        float itemHeight = 24f;
//        float h = Mathf.Min(options.Length * itemHeight + 10, 350);
//        float w = 300;

//        float maxX = Screen.width - w - 5;
//        float maxY = Screen.height - h - 5;
//        float px = Mathf.Clamp(screenPos.x, 5, Mathf.Max(5, maxX));
//        float py = Mathf.Clamp(screenPos.y, 5, Mathf.Max(5, maxY));

//        _popupRect = new Rect(px, py, w, h);
//    }

//    public static void Hide()
//    {
//        _showing = false;
//        _options = null;
//        _callback = null;
//    }

//    public static void DrawPopup()
//    {
//        if (!_showing || _options == null) return;

//        GUI.BringWindowToFront(_windowId);
//        _popupRect = GUI.Window(_windowId, _popupRect, DrawPopupWindow, "", GUI.skin.box);
//    }

//    private static void DrawPopupWindow(int id)
//    {
//        bool canInteract = Time.frameCount > _showFrame;

//        _scrollPos = GUILayout.BeginScrollView(_scrollPos);

//        for (int i = 0; i < _options.Length; i++)
//        {
//            bool isCurrent = (i == _current);
//            GUIStyle style = isCurrent ? GetSelectedStyle() : GetNormalStyle();

//            if (GUILayout.Button(_options[i], style, GUILayout.Height(22)))
//            {
//                if (canInteract)
//                {
//                    _callback?.Invoke(i);
//                    _showing = false;
//                    GUIUtility.ExitGUI();
//                    return;
//                }
//            }
//        }

//        GUILayout.EndScrollView();
//    }

//    private static GUIStyle _normalStyle;
//    private static GUIStyle _selectedStyle;

//    private static GUIStyle GetNormalStyle()
//    {
//        if (_normalStyle == null)
//        {
//            _normalStyle = new GUIStyle(GUI.skin.label)
//            {
//                alignment = TextAnchor.MiddleLeft,
//                padding = new RectOffset(8, 4, 2, 2),
//                hover = { textColor = Color.white, background = MakeTex(1, 1, new Color(0.3f, 0.5f, 0.8f, 0.5f)) },
//                normal = { textColor = Color.white }
//            };
//        }
//        return _normalStyle;
//    }

//    private static GUIStyle GetSelectedStyle()
//    {
//        if (_selectedStyle == null)
//        {
//            _selectedStyle = new GUIStyle(GUI.skin.label)
//            {
//                alignment = TextAnchor.MiddleLeft,
//                fontStyle = FontStyle.Bold,
//                padding = new RectOffset(8, 4, 2, 2),
//                normal = { textColor = Color.cyan, background = MakeTex(1, 1, new Color(0.2f, 0.4f, 0.6f, 0.4f)) }
//            };
//        }
//        return _selectedStyle;
//    }

//    private static Texture2D MakeTex(int w, int h, Color col)
//    {
//        var pix = new Color[w * h];
//        for (int i = 0; i < pix.Length; i++) pix[i] = col;
//        var tex = new Texture2D(w, h);
//        tex.SetPixels(pix);
//        tex.Apply();
//        return tex;
//    }
//}