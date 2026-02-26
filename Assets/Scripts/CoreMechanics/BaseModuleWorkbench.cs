using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public abstract class BaseModuleWorkbench : MonoBehaviour
{
    [Header("Storage References")]
    public AlloyStorage alloyStorage;
    public ResourcesStorage resourcesStorage;
    public ModuleStorage moduleStorage;

    [Header("Workbench Dimensions (Microwave)")]
    public float innerLength = 2f;
    public float innerWidth = 2f;
    public float innerHeight = 2f;

    protected bool panelOpen;
    private Rect windowRect;
    private bool windowRectInitialized;
    private Vector2 scrollPos;

    protected ModuleScaler scaler = new ModuleScaler();

    private float shellPercent = 5f;
    private string shellPercentStr = "5.000";

    protected int selectedAlloyIndex;
    protected string[] alloyDisplayNames;
    protected string[] alloyCodes;
    protected bool alloyDecoded;
    protected AlloyCode.AlloyParams alloyParams;

    private string currentModuleCode = "";
    private string codeInputField = "";

    private string errorMessage = "";
    private float errorTimer;

    private GameObject craftedInstance;
    private Dictionary<string, int> _pendingSelections = new Dictionary<string, int>();

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

    protected virtual void Update()
    {
        if (panelOpen && Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            ClosePanel();

        if (errorTimer > 0f)
        {
            errorTimer -= Time.deltaTime;
            if (errorTimer <= 0f) errorMessage = "";
        }
    }

    // ================== ВИЗУАЛЬНАЯ ТЕМА (ГРАФИТ) ==================
    private static Texture2D _bgTex;
    private static Texture2D _panelTex;
    private static Texture2D _sepTex;
    private static GUIStyle _windowStyle;
    private static GUIStyle _panelStyle;

    private void InitStyles()
    {
        // Темно-графитовый фон
        if (_bgTex == null) _bgTex = WorkbenchPopup.MakeTex(1, 1, new Color(0.15f, 0.15f, 0.15f, 0.98f));
        // Панели чуть светлее
        if (_panelTex == null) _panelTex = WorkbenchPopup.MakeTex(1, 1, new Color(0.2f, 0.2f, 0.2f, 0.9f));
        // Нейтрально-серый разделитель
        if (_sepTex == null) _sepTex = WorkbenchPopup.MakeTex(1, 1, new Color(0.35f, 0.35f, 0.35f, 0.5f));

        if (_windowStyle == null)
        {
            _windowStyle = new GUIStyle(GUI.skin.window);
            _windowStyle.normal.background = _bgTex;
            _windowStyle.onNormal.background = _bgTex;
            _windowStyle.normal.textColor = Color.white;
            _windowStyle.fontSize = 14;
            _windowStyle.fontStyle = FontStyle.Bold;
        }

        if (_panelStyle == null)
        {
            _panelStyle = new GUIStyle(GUI.skin.box);
            _panelStyle.normal.background = _panelTex;
            _panelStyle.normal.textColor = Color.white;
            _panelStyle.padding = new RectOffset(10, 10, 10, 10);
            _panelStyle.margin = new RectOffset(0, 0, 5, 5);
        }

        GUI.skin.label.richText = true;
    }

    private void DrawSeparator()
    {
        GUILayout.Space(5);
        GUIStyle sep = new GUIStyle();
        sep.normal.background = _sepTex;
        GUILayout.Box(GUIContent.none, sep, GUILayout.Height(2), GUILayout.ExpandWidth(true));
        GUILayout.Space(5);
    }
    // ==============================================================

    private void OnGUI()
    {
        if (!panelOpen) return;
        InitStyles();

        if (!windowRectInitialized)
        {
            float w = Mathf.Min(1100f, Screen.width * 0.96f);
            float h = Screen.height * 0.9f;
            windowRect = new Rect((Screen.width - w) * 0.5f, (Screen.height - h) * 0.5f, w, h);
            windowRectInitialized = true;
        }

        windowRect.x = Mathf.Clamp(windowRect.x, 0, Mathf.Max(0, Screen.width - windowRect.width));
        windowRect.y = Mathf.Clamp(windowRect.y, 0, Mathf.Max(0, Screen.height - windowRect.height));

        if (WorkbenchPopup.IsShowing && Event.current.type == EventType.MouseDown)
        {
            if (!WorkbenchPopup.PopupRect.Contains(Event.current.mousePosition))
            {
                WorkbenchPopup.Hide();
                Event.current.Use();
            }
        }

        // Оригинальное название окна
        windowRect = GUI.Window(GetInstanceID(), windowRect, DrawWindow, $"{ModuleTypeName}Workbench", _windowStyle);
        WorkbenchPopup.DrawPopup();
    }

    private void DrawWindow(int id)
    {
        GUI.DragWindow(new Rect(0, 0, 10000, 20));
        float padding = 20f;
        float contentWidth = windowRect.width - (padding * 2);

        GUILayout.BeginArea(new Rect(padding, 35, contentWidth, windowRect.height - 45));
        scrollPos = GUILayout.BeginScrollView(scrollPos, false, true);
        GUILayout.BeginVertical();

        if (!string.IsNullOrEmpty(errorMessage))
        {
            GUILayout.Label($"<color=#FF4444><b>⚠ ОШИБКА: {errorMessage}</b></color>", GetCenteredBoldStyle());
            GUILayout.Space(5);
        }

        // Блок кодов
        GUILayout.BeginHorizontal();
        GUILayout.BeginVertical(GUILayout.Width(contentWidth * 0.75f));
        DrawCompactCodeSection("ГЕНЕРАЦИЯ КОДА", ref currentModuleCode, true, "КОПИРОВАТЬ", () => {
            if (!string.IsNullOrEmpty(currentModuleCode)) GUIUtility.systemCopyBuffer = currentModuleCode;
        });
        GUILayout.Space(5);
        DrawCompactCodeSection("ВВОД ЧЕРТЕЖА", ref codeInputField, false, "ВСТАВИТЬ", () => {
            codeInputField = (GUIUtility.systemCopyBuffer ?? "").Trim();
        }, "ПРИМЕНИТЬ", ApplyCodeFromInput);
        GUILayout.EndVertical();
        GUILayout.FlexibleSpace();
        GUILayout.EndHorizontal();

        DrawSeparator();

        // Основная рабочая зона
        GUILayout.BeginHorizontal();
        float leftW = (contentWidth - 30) * 0.55f;

        GUILayout.BeginVertical(GUILayout.Width(leftW));
        DrawSelectionSection();
        DrawShellSection();
        DrawScalingSection();
        GUILayout.EndVertical();

        GUILayout.Space(10);

        GUILayout.BeginVertical();
        DrawComputedSection();
        GUILayout.EndVertical();

        GUILayout.EndHorizontal();

        DrawSeparator();

        // Специфичная секция (генератор)
        DrawModuleSpecificSection();

        DrawSeparator();

        DrawAlloyParamsSection();

        DrawSeparator();

        // Стоимость и Крафт
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

    private void DrawCompactCodeSection(string title, ref string text, bool readOnly, string btn1Text, Action act1, string btn2Text = null, Action act2 = null)
    {
        GUILayout.BeginVertical(_panelStyle);
        GUILayout.Label($"<color=#E0E0E0>{title}</color>", GetBoldStyle());
        GUILayout.BeginHorizontal();

        GUIStyle areaStyle = new GUIStyle(GUI.skin.textArea);
        areaStyle.fontSize = 13;
        areaStyle.normal.textColor = readOnly ? new Color(0.8f, 0.9f, 0.8f) : Color.white;
        areaStyle.normal.background = WorkbenchPopup.MakeTex(1, 1, new Color(0.1f, 0.1f, 0.1f, 1f));

        if (readOnly) GUI.enabled = false;
        text = GUILayout.TextArea(text, areaStyle, GUILayout.Height(55));
        if (readOnly) GUI.enabled = true;

        GUILayout.BeginVertical(GUILayout.Width(110));
        if (GUILayout.Button(btn1Text, GUILayout.Height(25))) act1?.Invoke();
        if (btn2Text != null && act2 != null && GUILayout.Button(btn2Text, GUILayout.Height(25))) act2.Invoke();
        GUILayout.EndVertical();

        GUILayout.EndHorizontal();
        GUILayout.EndVertical();
    }

    private void ApplyCodeFromInput()
    {
        if (string.IsNullOrEmpty(codeInputField)) return;
        string[] lines = codeInputField.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
        if (lines.Length < 3) { ShowError("Неверный формат чертежа (нужно 3 строки)"); return; }

        string[] parts = lines[0].Split('-');
        if (parts.Length < 5) { ShowError("Неверная первая строка"); return; }
        if (parts[0] != ModuleTypeName) { ShowError($"Чертеж не от {ModuleTypeName}"); return; }

        string[] dims = parts[4].Split('/');
        if (dims.Length != 3) { ShowError("Неверные габариты в коде"); return; }

        float l = float.Parse(dims[0], System.Globalization.CultureInfo.InvariantCulture);

        string alloyCode = lines[2].Trim();
        int newAlloyIndex = Array.IndexOf(alloyCodes, alloyCode);
        if (newAlloyIndex >= 0)
        {
            selectedAlloyIndex = newAlloyIndex;
            OnAlloyChanged();
        }

        scaler.SetScaleMode(ModuleScaler.ScaleMode.Length);
        scaler.HandleScaleInput(l.ToString(System.Globalization.CultureInfo.InvariantCulture));
        RecalculateAll();

        if (currentModuleCode.Trim() != codeInputField.Trim())
        {
            ShowError("Чертеж поврежден или содержит невозможные параметры!");
            ResetToDefaults();
        }
    }

    private void DrawSelectionSection()
    {
        GUILayout.BeginVertical(_panelStyle);
        GUILayout.Label("<color=#E0E0E0>ВЫБОР ЭТАЛОНА</color>", GetBoldStyle());
        GUILayout.BeginHorizontal();
        if (GetReferenceCount() > 0)
        {
            int curIdx = GetSelectedReferenceIndex();
            int newIdx = DrawDropdown("wb_ref", curIdx, GetReferenceNames());
            if (newIdx != curIdx) { SelectReference(newIdx); OnReferenceChanged(); }
        }
        else GUILayout.Label("<color=#FF8888>(Нет эталонов в БД)</color>");
        GUILayout.EndHorizontal();

        if (GetReferenceCount() > 0)
        {
            string faction = string.IsNullOrEmpty(GetReferenceFaction()) ? "—" : GetReferenceFaction();
            GUILayout.Label($"<color=#AAAAAA>Тир:</color> {GetReferenceTier()}  |  <color=#AAAAAA>Фракция:</color> {faction}  |  <color=#AAAAAA>Заполнение:</color> {GetReferenceFillPercent():F0}%");
        }
        GUILayout.EndVertical();
    }

    private void DrawShellSection()
    {
        GUILayout.BeginVertical(_panelStyle);
        GUILayout.Label("<color=#E0E0E0>ОБОЛОЧКА</color>", GetBoldStyle());

        GUILayout.BeginHorizontal();
        GUILayout.Label("Объем %:", GUILayout.Width(70));
        string newStr = GUILayout.TextField(shellPercentStr, GUILayout.Width(60));
        if (newStr != shellPercentStr && float.TryParse(newStr, out float val))
        {
            shellPercent = Mathf.Clamp(val, 0.001f, 100f);
            shellPercentStr = newStr;
            scaler.SetShellPercent(shellPercent);
            RecalculateAll();
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

        GUILayout.BeginHorizontal();
        GUILayout.Label("Сплав:", GUILayout.Width(70));
        if (alloyDisplayNames != null && alloyDisplayNames.Length > 0)
        {
            int newIdx = DrawDropdown("wb_alloy", selectedAlloyIndex, alloyDisplayNames);
            if (newIdx != selectedAlloyIndex) { selectedAlloyIndex = newIdx; OnAlloyChanged(); }
        }
        else GUILayout.Label("<color=#FFCC00>(не выбран)</color>", GUILayout.MinWidth(150));
        GUILayout.EndHorizontal();
        GUILayout.EndVertical();
    }

    private void DrawScalingSection()
    {
        GUILayout.BeginVertical(_panelStyle);
        GUILayout.Label("<color=#E0E0E0>МАСШТАБИРОВАНИЕ</color>", GetBoldStyle());

        string[] modeNames = { "По Длине", "По Ширине", "По Высоте", "По Массе", "По Объёму" };
        int curMode = (int)scaler.CurrentScaleMode;
        int newMode = GUILayout.SelectionGrid(curMode, modeNames, 3);
        if (newMode != curMode) scaler.SetScaleMode((ModuleScaler.ScaleMode)newMode);

        GUILayout.Space(5);
        GUILayout.BeginHorizontal();
        GUILayout.Label("Ввод:", GUILayout.Width(80));
        string currentStr = scaler.ScaleInputStr;
        string newScaleStr = GUILayout.TextField(currentStr);
        if (newScaleStr != currentStr && scaler.HandleScaleInput(newScaleStr)) RecalculateAll();
        GUILayout.EndHorizontal();

        GUILayout.Space(5);
        if (GUILayout.Button("СБРОСИТЬ МАСШТАБ", GUILayout.Height(24))) { scaler.SetScaleFactor(1f); RecalculateAll(); }
        GUILayout.EndVertical();
    }

    private bool CheckFitsInWorkbench()
    {
        return scaler.CalcLength <= innerLength && scaler.CalcWidth <= innerWidth && scaler.CalcHeight <= innerHeight;
    }

    private void DrawComputedSection()
    {
        GUILayout.BeginVertical(_panelStyle);
        GUILayout.Label("<color=#E0E0E0>ГЕОМЕТРИЯ И ФИЗИКА</color>", GetBoldStyle());

        bool fits = CheckFitsInWorkbench();
        string dimColor = fits ? "#00FF00" : "#FF4444";

        DrawGridRow("Длина (X):", $"<color={dimColor}>{scaler.CalcLength:F3} м</color>", "Объём (Полный):", $"{scaler.CalcRealVolume:F4} м³");
        DrawGridRow("Ширина (Z):", $"<color={dimColor}>{scaler.CalcWidth:F3} м</color>", "Объём (Стенки):", $"{scaler.CalcShellVolume:F4} м³");
        DrawGridRow("Высота (Y):", $"<color={dimColor}>{scaler.CalcHeight:F3} м</color>", "Объём (Внутр.):", $"{scaler.CalcEffectiveVolume:F4} м³");

        DrawSeparator();

        DrawGridRow("Масса оболочки:", $"{scaler.CalcShellMass:F1} кг", "Прочность:", $"<color=#FFD700>{scaler.CalcDurability:F1}</color>");
        DrawGridRow("Масса внутрянки:", $"{scaler.CalcInnerMass:F1} кг", "", "");
        DrawGridRow("ОБЩАЯ МАССА:", $"<b>{scaler.CalcTotalMass:F1} кг</b>", "", "");

        if (!fits)
        {
            GUILayout.Space(10);
            GUILayout.Label($"<color=#FF4444><b>⚠ ГАБАРИТЫ ПРЕВЫШАЮТ КАМЕРУ ВЕРСТАКА (Макс: {innerLength}x{innerWidth}x{innerHeight})</b></color>", GetCenteredBoldStyle());
        }

        GUILayout.EndVertical();
    }

    private void DrawGridRow(string l1, string v1, string l2, string v2)
    {
        GUILayout.BeginHorizontal();
        GUILayout.Label($"<color=#AAAAAA>{l1}</color>", GUILayout.Width(120));
        GUILayout.Label(v1, GUILayout.Width(100));
        GUILayout.Label($"<color=#AAAAAA>{l2}</color>", GUILayout.Width(120));
        GUILayout.Label(v2);
        GUILayout.EndHorizontal();
    }

    private void DrawAlloyParamsSection()
    {
        GUILayout.BeginVertical(_panelStyle);
        GUILayout.Label("<color=#E0E0E0>АНАЛИЗ ОБОЛОЧКИ</color>", GetBoldStyle());

        if (!alloyDecoded || alloyCodes == null || alloyCodes.Length == 0)
        {
            GUILayout.Label("<color=#FFCC00>(Сплав не выбран или не распознан)</color>");
            GUILayout.EndVertical();
            return;
        }

        GUILayout.BeginHorizontal();
        GUILayout.Label($"<color=#AAAAAA>Тир сплава:</color> <b>{alloyParams.tier}</b>", GUILayout.Width(130));
        GUILayout.Label($"<color=#AAAAAA>Химикаты:</color> {(alloyParams.useChemicals ? "<color=#00FF00>Да</color>" : "<color=#FF4444>Нет</color>")}", GUILayout.Width(120));
        GUILayout.Label($"<color=#AAAAAA>Наниты:</color> {(alloyParams.useNanites ? "<color=#00FF00>Да</color>" : "<color=#FF4444>Нет</color>")}", GUILayout.Width(120));
        GUILayout.EndHorizontal();

        GUILayout.Space(5);
        float colW = (windowRect.width - 70) / 4f;
        GUILayout.BeginHorizontal();
        DrawAlloyCol("КИНИТИКА", alloyParams.kineticAbsorption, alloyParams.kineticResistance, colW);
        DrawAlloyCol("ТЕРМИКА", alloyParams.thermalAbsorption, alloyParams.thermalResistance, colW);
        DrawAlloyCol("ХИМИЯ", alloyParams.chemicalAbsorption, alloyParams.chemicalResistance, colW);
        DrawAlloyCol("ЭНЕРГИЯ", alloyParams.energyAbsorption, alloyParams.energyResistance, colW);
        GUILayout.EndHorizontal();
        GUILayout.EndVertical();
    }

    private void DrawAlloyCol(string title, int absorb, float resist, float width)
    {
        GUILayout.BeginVertical("box", GUILayout.Width(width));
        GUILayout.Label($"<color=#CCCCCC><b>{title}</b></color>");
        GUILayout.Label($"Поглощ: <b>{absorb}</b>");
        GUILayout.Label($"Резист: <b>{resist:F1}%</b>");
        GUILayout.EndVertical();
    }

    private void DrawCostsAndButtons()
    {
        GUILayout.BeginVertical(_panelStyle);
        GUILayout.Label("<color=#E0E0E0>ТРЕБОВАНИЯ ПРОИЗВОДСТВА</color>", GetBoldStyle());

        string alloyCode = GetSelectedAlloyCode();
        float alloyAvailable = alloyCode != null && alloyStorage != null ? (float)alloyStorage.GetMass(alloyCode) : 0f;
        bool enoughAlloy = alloyCode != null && alloyStorage != null && alloyStorage.HasEnoughMass(alloyCode, scaler.CalcShellMass);

        int metalTier = GetReferenceTier();
        var metalIdx = GetMetalIndex();
        float metalAvailable = resourcesStorage != null ? (float)(resourcesStorage.GetGrams(metalIdx) / 1000.0) : 0f;
        float metalNeeded = scaler.CalcInnerMass;
        bool enoughMetal = metalAvailable >= metalNeeded - 0.001f;

        long energyNeeded = (long)Math.Ceiling(scaler.CalcTotalMass);
        long energyAvailable = resourcesStorage != null ? resourcesStorage.EnergyUnits : 0;
        bool enoughEnergy = energyAvailable >= energyNeeded;

        GUILayout.BeginHorizontal();
        DrawCostItem("Сплав", scaler.CalcShellMass, alloyAvailable, "кг", enoughAlloy);
        GUILayout.FlexibleSpace();
        DrawCostItem($"Металл T{metalTier}", metalNeeded, metalAvailable, "кг", enoughMetal);
        GUILayout.FlexibleSpace();
        DrawCostItem("Энергия", energyNeeded, energyAvailable, "E", enoughEnergy, "#FFD700");
        GUILayout.EndHorizontal();

        GUILayout.Space(15);

        GUILayout.BeginHorizontal();
        bool fits = CheckFitsInWorkbench();
        bool canCraft = GetReferenceCount() > 0 && alloyCode != null && enoughAlloy && enoughMetal && enoughEnergy && scaler.CalcEffectiveVolume > 0.000001f && fits;

        Color oldBg = GUI.backgroundColor;
        GUI.backgroundColor = canCraft ? new Color(0.2f, 0.6f, 0.3f) : new Color(0.4f, 0.2f, 0.2f);

        GUI.enabled = canCraft;
        if (GUILayout.Button("ИЗГОТОВИТЬ МОДУЛЬ", GUILayout.Height(35))) OnCraft();
        GUI.enabled = true;
        GUI.backgroundColor = oldBg;

        GUILayout.Space(10);
        if (GUILayout.Button("СБРОС", GUILayout.Height(35), GUILayout.Width(100))) ResetToDefaults();
        GUILayout.EndHorizontal();
        GUILayout.EndVertical();
    }

    private void DrawCostItem(string label, float needed, float available, string unit, bool enough, string highlightColor = "#FFFFFF")
    {
        GUILayout.BeginVertical(GUILayout.MinWidth(110));
        GUILayout.Label($"<color=#AAAAAA>{label}:</color> <color={highlightColor}><b>{needed:F1} {unit}</b></color>");
        string availStr = enough ? $"<color=#00FF00>{available:F1}</color>" : $"<color=#FF4444>{available:F1}</color>";
        GUILayout.Label($"На складе: {availStr} {unit}", new GUIStyle(GUI.skin.label) { fontSize = 11 });
        GUILayout.EndVertical();
    }

    private void OnCraft()
    {
        if (GetReferenceCount() == 0 || !CheckFitsInWorkbench()) return;

        string alloyCode = GetSelectedAlloyCode();
        if (string.IsNullOrEmpty(alloyCode)) { ShowError("Сплав не выбран"); return; }

        string craftCode = currentModuleCode;
        float craftShellMass = scaler.CalcShellMass;
        float craftInnerMass = scaler.CalcInnerMass;
        float craftTotalMass = scaler.CalcTotalMass;

        if (!alloyStorage.HasEnoughMass(alloyCode, craftShellMass)) { ShowError("Недостаточно сплава для оболочки"); return; }

        var metalIdx = GetMetalIndex();
        long metalNeededG = (long)Math.Ceiling(craftInnerMass * 1000.0);
        if (resourcesStorage.GetGrams(metalIdx) < metalNeededG) { ShowError("Недостаточно металла"); return; }

        long energyNeeded = (long)Math.Ceiling(craftTotalMass);
        if (resourcesStorage.EnergyUnits < energyNeeded) { ShowError("Недостаточно энергии"); return; }

        ModuleData moduleData = CreateSpecificModuleData();
        if (moduleData == null) { ShowError("Ошибка создания данных модуля"); return; }

        string faction = string.IsNullOrEmpty(GetReferenceFaction()) ? "NONE" : GetReferenceFaction();
        string refName = GetReferenceName() ?? "";

        moduleData.FillCommon(
            ModuleTypeName, GetReferenceTier(), faction, GetSelectedReferenceIndex(), refName,
            alloyCode, alloyDecoded ? alloyParams.tier : 1, shellPercent, scaler.CurrentScaleFactor,
            GetReferenceFillPercent(), scaler.CalcLength, scaler.CalcWidth, scaler.CalcHeight,
            scaler.CalcAABBVolume, scaler.CalcRealVolume, scaler.CalcShellVolume, scaler.CalcEffectiveVolume,
            craftShellMass, craftInnerMass, craftTotalMass, scaler.CalcDurability, craftCode
        );

        alloyStorage.TryConsumeMass(alloyCode, craftShellMass);
        resourcesStorage.TryRemoveGrams(metalIdx, metalNeededG);
        resourcesStorage.TryConsumeEnergy(energyNeeded);

        if (craftedInstance != null) { Destroy(craftedInstance); craftedInstance = null; }

        GameObject prefab = GetReferencePrefab();
        if (prefab == null) { ShowError("Префаб эталона не найден"); return; }

        Vector3 spawnPos = transform.position + Vector3.up * 2f;
        craftedInstance = Instantiate(prefab, spawnPos, Quaternion.identity);
        craftedInstance.name = $"Crafted_{prefab.name}_T{GetReferenceTier()}";
        craftedInstance.transform.localScale = prefab.transform.localScale * Mathf.Max(0.001f, scaler.CurrentScaleFactor);

        var oldGen = craftedInstance.GetComponent<StandardGenerator>();
        if (oldGen != null) Destroy(oldGen);

        var craftedComp = craftedInstance.AddComponent<CraftedModule>();
        craftedComp.SetData(moduleData);

        if (moduleStorage != null)
        {
            string mid = moduleStorage.AddModule(moduleData);
            Debug.Log($"[{ModuleTypeName}Workbench] Saved to ModuleStorage, ID: {mid}");
        }

        RebuildAlloyList();
        RecalculateAll();
    }

    protected void RecalculateAll()
    {
        scaler.SetAlloyTier(alloyDecoded ? alloyParams.tier : 1);
        RecalculateSpecifics();
        currentModuleCode = BuildModuleCode();
    }

    private string BuildModuleCode()
    {
        if (GetReferenceCount() == 0) return "";
        int tier = GetReferenceTier();
        string faction = string.IsNullOrEmpty(GetReferenceFaction()) ? "NONE" : GetReferenceFaction();
        string alloyCode = GetSelectedAlloyCode() ?? "NONE";
        string specific = GetSpecificCodeSegment();

        string line1 = $"{ModuleTypeName}-T{tier}-m{scaler.CalcTotalMass.ToString("F1", System.Globalization.CultureInfo.InvariantCulture)}-d{scaler.CalcDurability.ToString("F3", System.Globalization.CultureInfo.InvariantCulture)}-{scaler.CalcLength.ToString("F3", System.Globalization.CultureInfo.InvariantCulture)}/{scaler.CalcWidth.ToString("F3", System.Globalization.CultureInfo.InvariantCulture)}/{scaler.CalcHeight.ToString("F3", System.Globalization.CultureInfo.InvariantCulture)}-{faction}";
        return $"{line1}\n{specific}\n{alloyCode}";
    }

    private void OnReferenceChanged() => RecalculateAll();

    protected void OnAlloyChanged()
    {
        alloyDecoded = false;
        if (alloyCodes != null && selectedAlloyIndex >= 0 && selectedAlloyIndex < alloyCodes.Length)
        {
            if (AlloyCode.Decode(alloyCodes[selectedAlloyIndex], out AlloyCode.AlloyParams p))
            {
                alloyParams = p;
                alloyDecoded = true;
            }
        }
        RecalculateAll();
    }

    private void RebuildAllLists() { RebuildReferenceList(); RebuildAlloyList(); }

    protected void RebuildAlloyList()
    {
        if (alloyStorage == null || alloyStorage.Count == 0)
        {
            alloyDisplayNames = new string[0]; alloyCodes = new string[0];
            selectedAlloyIndex = 0; alloyDecoded = false; return;
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

    private void ResetToDefaults()
    {
        shellPercent = 5f; shellPercentStr = "5.000"; selectedAlloyIndex = 0;
        codeInputField = ""; errorMessage = ""; _pendingSelections.Clear(); WorkbenchPopup.Hide();
        scaler.SetScaleFactor(1f); scaler.SetShellPercent(5f); scaler.SetScaleMode(ModuleScaler.ScaleMode.Mass);
        RebuildAllLists(); RecalculateAll();
    }

    protected void ShowError(string msg) { errorMessage = msg; errorTimer = 4f; }

    protected int DrawDropdown(string tag, int selected, string[] options)
    {
        if (options == null || options.Length == 0) return selected;
        selected = Mathf.Clamp(selected, 0, options.Length - 1);

        GUIStyle btnStyle = new GUIStyle(GUI.skin.button) { fontSize = 13, fontStyle = FontStyle.Normal };

        if (GUILayout.Button(options[selected], btnStyle, GUILayout.MinWidth(150), GUILayout.Height(25)))
        {
            WorkbenchPopup.Show(options, selected, GUIUtility.GUIToScreenPoint(Event.current.mousePosition), idx => _pendingSelections[tag] = idx);
        }
        if (_pendingSelections.TryGetValue(tag, out int result))
        {
            _pendingSelections.Remove(tag);
            return Mathf.Clamp(result, 0, options.Length - 1);
        }
        return selected;
    }

    private GUIStyle _centeredBold;
    private GUIStyle GetCenteredBoldStyle()
    {
        if (_centeredBold == null) _centeredBold = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold, fontSize = 15 };
        return _centeredBold;
    }

    private GUIStyle _boldStyle;
    protected GUIStyle GetBoldStyle()
    {
        if (_boldStyle == null) _boldStyle = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold };
        return _boldStyle;
    }
}

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
        _options = options; _current = current; _callback = callback; _showing = true;
        _scrollPos = Vector2.zero; _showFrame = Time.frameCount;
        float itemHeight = 26f;
        float h = Mathf.Min(options.Length * itemHeight + 10, 400);
        float w = 350;
        _popupRect = new Rect(Mathf.Clamp(screenPos.x, 5, Mathf.Max(5, Screen.width - w - 5)), Mathf.Clamp(screenPos.y, 5, Mathf.Max(5, Screen.height - h - 5)), w, h);
    }

    public static void Hide() { _showing = false; _options = null; _callback = null; }

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
            if (GUILayout.Button(_options[i], i == _current ? GetSelectedStyle() : GetNormalStyle(), GUILayout.Height(24)))
            {
                if (canInteract) { _callback?.Invoke(i); _showing = false; GUIUtility.ExitGUI(); return; }
            }
        }
        GUILayout.EndScrollView();
    }

    private static GUIStyle _normalStyle, _selectedStyle;
    private static GUIStyle GetNormalStyle()
    {
        if (_normalStyle == null) _normalStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleLeft, padding = new RectOffset(8, 4, 2, 2), hover = { textColor = Color.white, background = MakeTex(1, 1, new Color(0.3f, 0.3f, 0.3f, 0.8f)) }, normal = { textColor = Color.white } };
        return _normalStyle;
    }
    private static GUIStyle GetSelectedStyle()
    {
        if (_selectedStyle == null) _selectedStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleLeft, fontStyle = FontStyle.Bold, padding = new RectOffset(8, 4, 2, 2), normal = { textColor = Color.white, background = MakeTex(1, 1, new Color(0.25f, 0.25f, 0.25f, 0.9f)) } };
        return _selectedStyle;
    }

    public static Texture2D MakeTex(int w, int h, Color col)
    {
        var pix = new Color[w * h];
        for (int i = 0; i < pix.Length; i++) pix[i] = col;
        var tex = new Texture2D(w, h);
        tex.SetPixels(pix); tex.Apply(); return tex;
    }
}