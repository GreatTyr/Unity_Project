using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Базовый класс верстака крафта модулей (IMGUI).
/// Реализует общий UI, масштабирование, оболочку, стоимость и спавн.
/// Наследники реализуют специфику конкретного типа модуля.
/// Путь: Assets/Scripts/CoreMechanics/BaseModuleWorkbench.cs
/// </summary>
public abstract class BaseModuleWorkbench : MonoBehaviour
{
    [Header("Storage References")]
    [Tooltip("Alloy storage to pick shell alloy from and consume on craft.")]
    public AlloyStorage alloyStorage;

    [Tooltip("Resources storage to consume metal and energy on craft.")]
    public ResourcesStorage resourcesStorage;

    [Tooltip("Module storage to save crafted modules.")]
    public ModuleStorage moduleStorage;

    // ====================== State ======================
    protected bool panelOpen;
    private Rect windowRect;
    private bool windowRectInitialized;
    private Vector2 scrollPos;

    // Масштабирование (композиция)
    protected ModuleScaler scaler = new ModuleScaler();

    // Shell — строковый буфер
    private float shellPercent = 5f;
    private string shellPercentStr = "5.000";

    // Alloy
    protected int selectedAlloyIndex;
    protected string[] alloyDisplayNames;
    protected string[] alloyCodes;
    protected bool alloyDecoded;
    protected AlloyCode.AlloyParams alloyParams;

    // Code
    private string currentModuleCode = "";
    private string codeInputField = "";

    // Error
    private string errorMessage = "";
    private float errorTimer;

    // Crafted object
    private GameObject craftedInstance;

    // Dropdown pending selections (IMGUI корректность)
    private Dictionary<string, int> _pendingSelections = new Dictionary<string, int>();

    // ====================== Abstract (наследники реализуют) ======================

    protected abstract string ModuleTypeName { get; }
    protected abstract void RebuildReferenceList();
    protected abstract string[] GetReferenceNames();
    protected abstract int GetSelectedReferenceIndex();
    protected abstract void SelectReference(int index);
    protected abstract int GetReferenceTier();
    protected abstract string GetReferenceFaction();
    protected abstract float GetReferenceFillPercent();
    protected abstract float GetReferenceVolumeCoeffPercent();
    protected abstract int GetReferenceCount();
    protected abstract string GetReferenceName();
    protected abstract GameObject GetReferencePrefab();
    protected abstract void RecalculateSpecifics();
    protected abstract void DrawModuleSpecificSection();
    protected abstract string GetSpecificCodeSegment();
    protected abstract ModuleData CreateSpecificModuleData();
    protected abstract ResourcesStorage.ResourceIndex GetMetalIndex();

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
        WorkbenchPopup.Hide();
    }

    // ====================== Unity Lifecycle ======================

    protected virtual void Update()
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

    private void OnGUI()
    {
        if (!panelOpen) return;

        // Адаптация к экрану: окно занимает 96% ширины (макс 1200px) и 90% высоты
        if (!windowRectInitialized)
        {
            float w = Mathf.Min(1200f, Screen.width * 0.96f);
            float h = Screen.height * 0.9f;
            float x = (Screen.width - w) * 0.5f;
            float y = (Screen.height - h) * 0.5f;
            windowRect = new Rect(x, y, w, h);
            windowRectInitialized = true;
        }

        // Clamp to screen
        windowRect.x = Mathf.Clamp(windowRect.x, 0, Mathf.Max(0, Screen.width - windowRect.width));
        windowRect.y = Mathf.Clamp(windowRect.y, 0, Mathf.Max(0, Screen.height - windowRect.height));

        // Закрытие popup по клику вне
        if (WorkbenchPopup.IsShowing && Event.current.type == EventType.MouseDown)
        {
            Vector2 screenMouse = Event.current.mousePosition;
            if (!WorkbenchPopup.PopupRect.Contains(screenMouse))
            {
                WorkbenchPopup.Hide();
                Event.current.Use();
            }
        }

        windowRect = GUI.Window(GetInstanceID(), windowRect, DrawWindow, $"{ModuleTypeName}Workbench");
        WorkbenchPopup.DrawPopup();
    }

    // ====================== Main Window ======================

    private void DrawWindow(int id)
    {
        GUI.DragWindow(new Rect(0, 0, 10000, 20));

        // Отступ контента от краев окна (Padding)
        float padding = 20f;
        float contentWidth = windowRect.width - (padding * 2);

        // Начало зоны с отступами
        GUILayout.BeginArea(new Rect(padding, 25, contentWidth, windowRect.height - 35));

        // Отключаем горизонтальный скролл (false, true для вертикального)
        scrollPos = GUILayout.BeginScrollView(scrollPos, false, true);
        GUILayout.BeginVertical();

        // ─── Error ───
        if (!string.IsNullOrEmpty(errorMessage))
        {
            Color prev = GUI.color;
            GUI.color = Color.red;
            GUILayout.Label(errorMessage, GetCenteredBoldStyle());
            GUI.color = prev;
            GUILayout.Space(5);
        }

        // ═══ 1. Код модуля (75% ширины, слева) ═══
        float codeWidth = contentWidth * 0.75f;

        GUILayout.BeginHorizontal();
        GUILayout.BeginVertical(GUILayout.Width(codeWidth));
        DrawCompactCodeSection("Код модуля", ref currentModuleCode, true, "Копировать", () => {
            if (!string.IsNullOrEmpty(currentModuleCode))
                GUIUtility.systemCopyBuffer = currentModuleCode;
        });
        GUILayout.Space(5);
        DrawCompactCodeSection("Вставить код", ref codeInputField, false, "Вставить", () => {
            codeInputField = (GUIUtility.systemCopyBuffer ?? "").Trim();
        }, "Применить", () => {
            ShowError("Система кодов модулей в разработке");
        });
        GUILayout.EndVertical();
        GUILayout.FlexibleSpace(); // Заполняет пустоту справа
        GUILayout.EndHorizontal();

        GUILayout.Space(10);

        // ═══ Основная рабочая зона (2 колонки) ═══
        GUILayout.BeginHorizontal();

        // Учитываем отступ между колонками (10px) и скроллбар (20px)
        float colSpace = 10f;
        float availableW = contentWidth - colSpace - 20;
        float leftW = availableW * 0.6f;

        // Левая колонка (60%)
        GUILayout.BeginVertical(GUILayout.Width(leftW));
        DrawSelectionSection();
        GUILayout.Space(5);
        DrawShellSection();
        GUILayout.Space(5);
        DrawScalingSection();
        GUILayout.EndVertical();

        GUILayout.Space(colSpace);

        // Правая колонка (остаток)
        GUILayout.BeginVertical();
        DrawComputedSection();
        GUILayout.EndVertical();

        GUILayout.EndHorizontal();

        GUILayout.Space(10);

        // ═══ Нижняя часть ═══
        DrawModuleSpecificSection();

        GUILayout.Space(5);
        DrawAlloyParamsSection();

        GUILayout.Space(5);

        // Стоимость (75% ширины, слева)
        GUILayout.BeginHorizontal();
        GUILayout.BeginVertical(GUILayout.Width(contentWidth * 0.75f));
        DrawCostsAndButtons();
        GUILayout.EndVertical();
        GUILayout.FlexibleSpace();
        GUILayout.EndHorizontal();

        GUILayout.EndVertical();
        GUILayout.EndScrollView();

        GUILayout.EndArea();
    }

    // ====================== Code Sections (Compact) ======================

    private void DrawCompactCodeSection(string title, ref string text, bool readOnly, string btn1Text, Action act1, string btn2Text = null, Action act2 = null)
    {
        GUILayout.BeginVertical("box");
        GUILayout.Label(title, GetBoldStyle());
        GUILayout.BeginHorizontal();

        // TextArea с увеличенным шрифтом (14)
        GUIStyle areaStyle = new GUIStyle(GUI.skin.textArea);
        areaStyle.fontSize = 14;
        areaStyle.normal.textColor = Color.white;

        if (readOnly) GUI.enabled = false;
        text = GUILayout.TextArea(text, areaStyle, GUILayout.Height(60));
        if (readOnly) GUI.enabled = true;

        // Кнопки справа в столбик, ширина 100px чтобы текст влезал
        GUILayout.BeginVertical(GUILayout.Width(100));
        if (GUILayout.Button(btn1Text, GUILayout.Height(28))) act1?.Invoke();

        if (btn2Text != null && act2 != null)
        {
            if (GUILayout.Button(btn2Text, GUILayout.Height(28))) act2.Invoke();
        }
        GUILayout.EndVertical();

        GUILayout.EndHorizontal();
        GUILayout.EndVertical();
    }

    // ====================== Selection Section ======================

    private void DrawSelectionSection()
    {
        GUILayout.BeginVertical("box");
        GUILayout.Label("Выбор эталона", GetBoldStyle());

        GUILayout.BeginHorizontal();
        if (GetReferenceCount() > 0)
        {
            string[] refNames = GetReferenceNames();
            int curIdx = GetSelectedReferenceIndex();
            int newIdx = DrawDropdown("wb_ref", curIdx, refNames);
            if (newIdx != curIdx)
            {
                SelectReference(newIdx);
                OnReferenceChanged();
            }
        }
        else
        {
            GUILayout.Label("(Нет эталонов)");
        }
        GUILayout.EndHorizontal();

        if (GetReferenceCount() > 0)
        {
            string faction = string.IsNullOrEmpty(GetReferenceFaction()) ? "—" : GetReferenceFaction();
            GUILayout.Label($"Тир: {GetReferenceTier()} | Фракция: {faction} | Fill: {GetReferenceFillPercent():F0}%", GUI.skin.label);
        }
        GUILayout.EndVertical();
    }

    // ====================== Shell Section ======================

    private void DrawShellSection()
    {
        GUILayout.BeginVertical("box");
        GUILayout.Label("Оболочка", GetBoldStyle());

        // Shell percent: Label -> Input -> 0% -> Slider -> 100%
        GUILayout.BeginHorizontal();
        GUILayout.Label("Объем %:", GUILayout.Width(70));

        string newStr = GUILayout.TextField(shellPercentStr, GUILayout.Width(60));
        if (newStr != shellPercentStr)
        {
            shellPercentStr = newStr;
            if (float.TryParse(shellPercentStr, out float val))
            {
                shellPercent = Mathf.Clamp(val, 0.001f, 100f);
                scaler.SetShellPercent(shellPercent);
                RecalculateAll();
            }
        }

        GUILayout.Space(5);
        GUILayout.Label("0%", GUILayout.Width(25));

        float sliderVal = GUILayout.HorizontalSlider(shellPercent, 0.001f, 100f);
        if (Mathf.Abs(sliderVal - shellPercent) > 0.0005f)
        {
            shellPercent = (float)Math.Round(sliderVal, 3);
            shellPercentStr = shellPercent.ToString("F3");
            scaler.SetShellPercent(shellPercent);
            RecalculateAll();
        }

        GUILayout.Label("100%", GUILayout.Width(35));
        GUILayout.EndHorizontal();

        // Alloy selection
        GUILayout.BeginHorizontal();
        GUILayout.Label("Сплав:", GUILayout.Width(70));

        // Увеличиваем кнопку списка сплавов
        if (alloyDisplayNames != null && alloyDisplayNames.Length > 0)
        {
            int newIdx = DrawDropdown("wb_alloy", selectedAlloyIndex, alloyDisplayNames);
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
            GUILayout.Label("(не выбран)", GUILayout.MinWidth(150));
            GUI.color = prev;
        }
        GUILayout.EndHorizontal();
        GUILayout.EndVertical();
    }

    // ====================== Scaling Section ======================

    private void DrawScalingSection()
    {
        GUILayout.BeginVertical("box");
        GUILayout.Label("Масштабирование", GetBoldStyle());

        // Выбор режима
        string[] modeNames = { "Длина", "Ширина", "Высота", "Масса", "Эфф.Объём" };
        int curMode = (int)scaler.CurrentScaleMode;
        int newMode = GUILayout.SelectionGrid(curMode, modeNames, 3);
        if (newMode != curMode)
        {
            scaler.SetScaleMode((ModuleScaler.ScaleMode)newMode);
        }

        GUILayout.Space(5);

        // Поле ввода
        GUILayout.BeginHorizontal();
        GUILayout.Label("Значение:", GUILayout.Width(80));
        string currentStr = scaler.ScaleInputStr;
        string newScaleStr = GUILayout.TextField(currentStr);
        if (newScaleStr != currentStr)
        {
            if (scaler.HandleScaleInput(newScaleStr))
            {
                RecalculateAll();
            }
        }
        GUILayout.EndHorizontal();

        if (GUILayout.Button("Сброс масштаба"))
        {
            scaler.SetScaleFactor(1f);
            RecalculateAll();
        }
        GUILayout.EndVertical();
    }

    // ====================== Computed Section ======================

    private void DrawComputedSection()
    {
        GUILayout.BeginVertical("box");
        GUILayout.Label("Общие параметры", GetBoldStyle());

        GUILayout.BeginHorizontal();
        LabelVal("Длина:", $"{scaler.CalcLength:F3} м");
        LabelVal("Ширина:", $"{scaler.CalcWidth:F3} м");
        GUILayout.EndHorizontal();
        LabelVal("Высота:", $"{scaler.CalcHeight:F3} м");

        GUILayout.Space(5);

        LabelVal("Объём (Real):", $"{scaler.CalcRealVolume:F6} м³");
        LabelVal("Объём (Shell):", $"{scaler.CalcShellVolume:F6} м³");
        LabelVal("Объём (Eff):", $"{scaler.CalcEffectiveVolume:F6} м³");

        GUILayout.Space(5);

        // Полные названия
        LabelVal("Масса (Оболочка):", $"{scaler.CalcShellMass:F1} кг");
        LabelVal("Масса (Внутренняя):", $"{scaler.CalcInnerMass:F1} кг");
        LabelVal("Масса (Общая):", $"{scaler.CalcTotalMass:F1} кг");

        GUILayout.Space(5);
        Color prev = GUI.color;
        GUI.color = Color.cyan;
        LabelVal("Прочность:", $"{scaler.CalcDurability:F1}");
        GUI.color = prev;

        GUILayout.EndVertical();
    }

    private void LabelVal(string label, string val)
    {
        GUILayout.BeginHorizontal();
        GUILayout.Label(label, GUILayout.Width(140)); // Увеличена ширина лейбла
        GUILayout.Label(val, GetBoldStyle());
        GUILayout.EndHorizontal();
    }

    // ====================== Alloy Params Section ======================

    private void DrawAlloyParamsSection()
    {
        GUILayout.BeginVertical("box");
        GUILayout.Label("Параметры сплава оболочки", GetBoldStyle());

        if (!alloyDecoded || alloyCodes == null || alloyCodes.Length == 0)
        {
            GUILayout.Label("(Сплав не выбран или не распознан)");
            GUILayout.EndVertical();
            return;
        }

        // Красивое отображение тира и флагов
        GUILayout.BeginHorizontal();
        GUILayout.Label($"Тир сплава: {alloyParams.tier}", GetBoldStyle(), GUILayout.Width(120));
        GUILayout.Label($"Химикаты: {(alloyParams.useChemicals ? "Да" : "Нет")}", GUILayout.Width(120));
        GUILayout.Label($"Наниты: {(alloyParams.useNanites ? "Да" : "Нет")}", GUILayout.Width(120));
        GUILayout.EndHorizontal();

        float colW = (windowRect.width - 50) / 4f;

        GUILayout.BeginHorizontal();
        DrawAlloyCol("Кинетика", alloyParams.kineticAbsorption, alloyParams.kineticResistance, colW);
        DrawAlloyCol("Термика", alloyParams.thermalAbsorption, alloyParams.thermalResistance, colW);
        DrawAlloyCol("Химия", alloyParams.chemicalAbsorption, alloyParams.chemicalResistance, colW);
        DrawAlloyCol("Энергия", alloyParams.energyAbsorption, alloyParams.energyResistance, colW);
        GUILayout.EndHorizontal();
        GUILayout.EndVertical();
    }

    private void DrawAlloyCol(string title, int absorb, float resist, float width)
    {
        GUILayout.BeginVertical(GUILayout.Width(width));
        GUILayout.Label(title, GetBoldStyle());
        GUILayout.Label($"Поглощение: {absorb}");
        GUILayout.Label($"Сопротивление: {resist:F1}%");
        GUILayout.EndVertical();
    }

    // ====================== Costs & Buttons ======================

    private void DrawCostsAndButtons()
    {
        GUILayout.BeginVertical("box");
        GUILayout.Label("Стоимость изготовления", GetBoldStyle());

        string alloyCode = GetSelectedAlloyCode();
        float alloyAvailable = alloyCode != null && alloyStorage != null
            ? (float)alloyStorage.GetMass(alloyCode) : 0f;
        bool enoughAlloy = alloyCode != null && alloyStorage != null &&
            alloyStorage.HasEnoughMass(alloyCode, scaler.CalcShellMass);

        int metalTier = GetReferenceTier();
        var metalIdx = GetMetalIndex();
        float metalAvailable = resourcesStorage != null
            ? (float)(resourcesStorage.GetGrams(metalIdx) / 1000.0) : 0f;
        float metalNeeded = scaler.CalcInnerMass;
        bool enoughMetal = metalAvailable >= metalNeeded - 0.001f;

        long energyNeeded = (long)Math.Ceiling(scaler.CalcTotalMass);
        long energyAvailable = resourcesStorage != null ? resourcesStorage.EnergyUnits : 0;
        bool enoughEnergy = energyAvailable >= energyNeeded;

        // Вывод стоимости в 3 колонки
        GUILayout.BeginHorizontal();
        DrawCostItem($"Сплав", scaler.CalcShellMass, alloyAvailable, "кг", enoughAlloy);
        GUILayout.FlexibleSpace();
        DrawCostItem($"Металл T{metalTier}", metalNeeded, metalAvailable, "кг", enoughMetal);
        GUILayout.FlexibleSpace();
        DrawCostItem("Энергия", energyNeeded, energyAvailable, "E", enoughEnergy);
        GUILayout.EndHorizontal();

        GUILayout.Space(10);

        GUILayout.BeginHorizontal();
        bool canCraft = GetReferenceCount() > 0 &&
                        alloyCode != null &&
                        enoughAlloy && enoughMetal && enoughEnergy &&
                        scaler.CalcEffectiveVolume > 0.000001f;

        if (!canCraft) GUI.enabled = false;
        if (GUILayout.Button("ИЗГОТОВИТЬ", GUILayout.Height(30)))
        {
            OnCraft();
        }
        GUI.enabled = true;

        if (GUILayout.Button("СБРОС", GUILayout.Height(30)))
        {
            ResetToDefaults();
        }
        GUILayout.EndHorizontal();

        GUILayout.EndVertical();
    }

    private void DrawCostItem(string label, float needed, float available, string unit, bool enough)
    {
        GUILayout.BeginVertical(GUILayout.MinWidth(100));
        GUILayout.Label($"{label}: {needed:F1} {unit}", GetBoldStyle());

        GUIStyle st = new GUIStyle(GUI.skin.label);
        if (!enough) st.normal.textColor = Color.red;
        GUILayout.Label($"(есть: {available:F1})", st);
        GUILayout.EndVertical();
    }

    // ====================== Craft ======================

    private void OnCraft()
    {
        if (GetReferenceCount() == 0) return;

        string alloyCode = GetSelectedAlloyCode();
        if (string.IsNullOrEmpty(alloyCode)) { ShowError("Сплав не выбран"); return; }

        string craftCode = currentModuleCode;
        float craftShellMass = scaler.CalcShellMass;
        float craftInnerMass = scaler.CalcInnerMass;
        float craftTotalMass = scaler.CalcTotalMass;

        if (!alloyStorage.HasEnoughMass(alloyCode, craftShellMass))
        {
            ShowError("Недостаточно сплава для оболочки");
            return;
        }

        var metalIdx = GetMetalIndex();
        long metalNeededG = (long)Math.Ceiling(craftInnerMass * 1000.0);
        if (resourcesStorage.GetGrams(metalIdx) < metalNeededG)
        {
            ShowError("Недостаточно металла");
            return;
        }

        long energyNeeded = (long)Math.Ceiling(craftTotalMass);
        if (resourcesStorage.EnergyUnits < energyNeeded)
        {
            ShowError("Недостаточно энергии");
            return;
        }

        // ─── Create ModuleData ───
        ModuleData moduleData = CreateSpecificModuleData();
        if (moduleData == null)
        {
            ShowError("Ошибка создания данных модуля");
            return;
        }

        string faction = string.IsNullOrEmpty(GetReferenceFaction()) ? "NONE" : GetReferenceFaction();
        string refName = GetReferenceName() ?? "";

        moduleData.FillCommon(
            ModuleTypeName,
            GetReferenceTier(),
            faction,
            GetSelectedReferenceIndex(),
            refName,
            alloyCode,
            alloyDecoded ? alloyParams.tier : 1,
            shellPercent,
            scaler.CurrentScaleFactor,
            GetReferenceFillPercent(),
            scaler.CalcLength, scaler.CalcWidth, scaler.CalcHeight,
            scaler.CalcAABBVolume, scaler.CalcRealVolume, scaler.CalcShellVolume, scaler.CalcEffectiveVolume,
            craftShellMass, craftInnerMass, craftTotalMass,
            scaler.CalcDurability,
            craftCode
        );

        // ─── Consume resources ───
        alloyStorage.TryConsumeMass(alloyCode, craftShellMass);
        resourcesStorage.TryRemoveGrams(metalIdx, metalNeededG);
        resourcesStorage.TryConsumeEnergy(energyNeeded);

        // ─── Destroy old ───
        if (craftedInstance != null)
        {
            Destroy(craftedInstance);
            craftedInstance = null;
        }

        // ─── Instantiate ───
        GameObject prefab = GetReferencePrefab();
        if (prefab == null) { ShowError("Префаб эталона не найден"); return; }

        Vector3 spawnPos = transform.position + Vector3.up * 2f;
        craftedInstance = Instantiate(prefab, spawnPos, Quaternion.identity);
        craftedInstance.name = $"Crafted_{prefab.name}_T{GetReferenceTier()}";

        float s = Mathf.Max(0.001f, scaler.CurrentScaleFactor);
        craftedInstance.transform.localScale = prefab.transform.localScale * s;

        // ─── Remove Standard* component, add CraftedModule ───
        var oldES = craftedInstance.GetComponent<StandardEnergyStorage>();
        if (oldES != null) Destroy(oldES);

        var oldGen = craftedInstance.GetComponent<StandardGenerator>();
        if (oldGen != null) Destroy(oldGen);

        var craftedComp = craftedInstance.AddComponent<CraftedModule>();
        craftedComp.SetData(moduleData);

        // ─── Save to ModuleStorage ───
        if (moduleStorage != null)
        {
            string mid = moduleStorage.AddModule(moduleData);
            Debug.Log($"[{ModuleTypeName}Workbench] Saved to ModuleStorage, ID: {mid}");
        }
        else
        {
            Debug.LogWarning($"[{ModuleTypeName}Workbench] ModuleStorage not assigned!");
        }

        // ─── Refresh UI ───
        RebuildAlloyList();
        RecalculateAll();

        Debug.Log($"[{ModuleTypeName}Workbench] Crafted: {craftedInstance.name}, Code: {craftCode}");
    }

    // ====================== RecalculateAll ======================

    protected void RecalculateAll()
    {
        int alloyTier = alloyDecoded ? alloyParams.tier : 1;
        scaler.SetAlloyTier(alloyTier);
        RecalculateSpecifics();
        currentModuleCode = BuildModuleCode();
    }

    // ====================== Module Code (3 строки) ======================

    private string BuildModuleCode()
    {
        if (GetReferenceCount() == 0) return "";

        int tier = GetReferenceTier();
        string faction = string.IsNullOrEmpty(GetReferenceFaction()) ? "NONE" : GetReferenceFaction();
        string alloyCode = GetSelectedAlloyCode() ?? "NONE";
        string specific = GetSpecificCodeSegment();

        string line1 = $"{ModuleTypeName}-T{tier}-m{scaler.CalcTotalMass:F1}-d{scaler.CalcDurability:F3}-{scaler.CalcLength:F3}/{scaler.CalcWidth:F3}/{scaler.CalcHeight:F3}-{faction}";
        string line2 = specific;
        string line3 = alloyCode;

        return $"{line1}\n{line2}\n{line3}";
    }

    // ====================== Callbacks ======================

    private void OnReferenceChanged()
    {
        RecalculateAll();
    }

    protected void OnAlloyChanged()
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

    // ====================== List Building ======================

    private void RebuildAllLists()
    {
        RebuildReferenceList();
        RebuildAlloyList();
    }

    protected void RebuildAlloyList()
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

    protected string GetSelectedAlloyCode()
    {
        if (alloyCodes == null || alloyCodes.Length == 0) return null;
        if (selectedAlloyIndex < 0 || selectedAlloyIndex >= alloyCodes.Length) return null;
        return alloyCodes[selectedAlloyIndex];
    }

    // ====================== Reset ======================

    private void ResetToDefaults()
    {
        shellPercent = 5f;
        shellPercentStr = "5.000";
        selectedAlloyIndex = 0;
        codeInputField = "";
        errorMessage = "";
        _pendingSelections.Clear();
        WorkbenchPopup.Hide();

        scaler.SetScaleFactor(1f);
        scaler.SetShellPercent(5f);
        scaler.SetScaleMode(ModuleScaler.ScaleMode.Mass);

        RebuildAllLists();
        RecalculateAll();
    }

    // ====================== Helpers ======================

    protected void ShowError(string msg)
    {
        errorMessage = msg;
        errorTimer = 3f;
    }

    // ====================== Dropdown ======================

    protected int DrawDropdown(string tag, int selected, string[] options)
    {
        if (options == null || options.Length == 0) return selected;
        selected = Mathf.Clamp(selected, 0, options.Length - 1);

        string current = options[selected];

        if (GUILayout.Button(current, GUI.skin.button, GUILayout.MinWidth(80)))
        {
            Vector2 screenPos = GUIUtility.GUIToScreenPoint(Event.current.mousePosition);
            string capturedTag = tag;
            string[] capturedOptions = options;

            WorkbenchPopup.Show(capturedOptions, selected, screenPos, idx =>
            {
                _pendingSelections[capturedTag] = idx;
            });
        }

        if (_pendingSelections.TryGetValue(tag, out int result))
        {
            _pendingSelections.Remove(tag);
            return Mathf.Clamp(result, 0, options.Length - 1);
        }

        return selected;
    }

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
    protected GUIStyle GetBoldStyle()
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

// ====================== Workbench Popup ======================
public static class WorkbenchPopup
{
    private static bool _showing;
    private static string[] _options;
    private static int _current;
    private static Action<int> _callback;
    private static Rect _popupRect;
    private static Vector2 _scrollPos;
    private static int _windowId = 987655;
    private static int _showFrame;

    public static bool IsShowing => _showing;
    public static Rect PopupRect => _popupRect;

    public static void Show(string[] options, int current, Vector2 screenPos, Action<int> callback)
    {
        _options = options;
        _current = current;
        _callback = callback;
        _showing = true;
        _scrollPos = Vector2.zero;
        _showFrame = Time.frameCount;

        float itemHeight = 26f;
        float h = Mathf.Min(options.Length * itemHeight + 10, 400);
        float w = 350;

        float maxX = Screen.width - w - 5;
        float maxY = Screen.height - h - 5;
        float px = Mathf.Clamp(screenPos.x, 5, Mathf.Max(5, maxX));
        float py = Mathf.Clamp(screenPos.y, 5, Mathf.Max(5, maxY));

        _popupRect = new Rect(px, py, w, h);
    }

    public static void Hide()
    {
        _showing = false;
        _options = null;
        _callback = null;
    }

    public static void DrawPopup()
    {
        if (!_showing || _options == null) return;

        GUI.BringWindowToFront(_windowId);
        _popupRect = GUI.Window(_windowId, _popupRect, DrawPopupWindow, "", GUI.skin.box);
    }

    private static void DrawPopupWindow(int id)
    {
        bool canInteract = Time.frameCount > _showFrame;

        _scrollPos = GUILayout.BeginScrollView(_scrollPos);

        for (int i = 0; i < _options.Length; i++)
        {
            bool isCurrent = (i == _current);
            GUIStyle style = isCurrent ? GetSelectedStyle() : GetNormalStyle();

            if (GUILayout.Button(_options[i], style, GUILayout.Height(24)))
            {
                if (canInteract)
                {
                    _callback?.Invoke(i);
                    _showing = false;
                    GUIUtility.ExitGUI();
                    return;
                }
            }
        }

        GUILayout.EndScrollView();
    }

    private static GUIStyle _normalStyle;
    private static GUIStyle _selectedStyle;

    private static GUIStyle GetNormalStyle()
    {
        if (_normalStyle == null)
        {
            _normalStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleLeft,
                padding = new RectOffset(8, 4, 2, 2),
                hover = { textColor = Color.white, background = MakeTex(1, 1, new Color(0.3f, 0.5f, 0.8f, 0.5f)) },
                normal = { textColor = Color.white }
            };
        }
        return _normalStyle;
    }

    private static GUIStyle GetSelectedStyle()
    {
        if (_selectedStyle == null)
        {
            _selectedStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleLeft,
                fontStyle = FontStyle.Bold,
                padding = new RectOffset(8, 4, 2, 2),
                normal = { textColor = Color.cyan, background = MakeTex(1, 1, new Color(0.2f, 0.4f, 0.6f, 0.4f)) }
            };
        }
        return _selectedStyle;
    }

    private static Texture2D MakeTex(int w, int h, Color col)
    {
        var pix = new Color[w * h];
        for (int i = 0; i < pix.Length; i++) pix[i] = col;
        var tex = new Texture2D(w, h);
        tex.SetPixels(pix);
        tex.Apply();
        return tex;
    }
}