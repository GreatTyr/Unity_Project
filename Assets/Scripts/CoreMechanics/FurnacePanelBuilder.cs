using UnityEngine;
using UnityEngine.UI;
using TMPro;

#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Автоматически создаёт FurnacePanel со всеми UI элементами
/// и привязывает их к FurnaceUI.
/// 
/// ИСПОЛЬЗОВАНИЕ:
/// 1. Повесить этот скрипт на Canvas в сцене _Init
/// 2. В Inspector нажать кнопку "Создать FurnacePanel"
/// 3. Проверить результат
/// 4. Удалить этот компонент и файл скрипта
/// </summary>
public class FurnacePanelBuilder : MonoBehaviour
{
#if UNITY_EDITOR
    [ContextMenu("Создать FurnacePanel")]
    public void BuildPanel()
    {
        Canvas canvas = GetComponent<Canvas>();
        if (canvas == null)
        {
            Debug.LogError("FurnacePanelBuilder должен быть на Canvas!");
            return;
        }

        // ═══════════════ КОРНЕВАЯ ПАНЕЛЬ ═══════════════

        GameObject panelGO = CreatePanel(canvas.transform, "FurnacePanel",
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(960, 700), Vector2.zero, new Color(0.1f, 0.1f, 0.1f, 0.92f));

        // Добавить VerticalLayoutGroup для авто-расположения
        var vlg = panelGO.AddComponent<VerticalLayoutGroup>();
        vlg.padding = new RectOffset(16, 16, 12, 12);
        vlg.spacing = 6;
        vlg.childAlignment = TextAnchor.UpperCenter;
        vlg.childControlWidth = true;
        vlg.childControlHeight = false;
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;

        // ContentSizeFitter чтобы панель подстраивалась
        var csf = panelGO.AddComponent<ContentSizeFitter>();
        csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        // ═══════════════ СООБЩЕНИЕ ОБ ОШИБКЕ (поверх всего) ═══════════════

        GameObject errorGO = CreateTextLabel(panelGO.transform, "ErrorMessageLabel",
            "НЕДОСТАТОЧНО РЕСУРСОВ", 32, Color.red, TextAlignmentOptions.Center, 50);
        // Поверх панели, по центру вверху
        RectTransform errorRT = errorGO.GetComponent<RectTransform>();
        errorRT.anchorMin = new Vector2(0, 1);
        errorRT.anchorMax = new Vector2(1, 1);
        errorRT.pivot = new Vector2(0.5f, 1);
        errorRT.anchoredPosition = new Vector2(0, 20);
        errorRT.sizeDelta = new Vector2(0, 60);
        // Убрать из layout
        var errorLE = errorGO.AddComponent<LayoutElement>();
        errorLE.ignoreLayout = true;
        errorGO.SetActive(false);

        // ═══════════════ СТРОКА 1: Вставка кода ═══════════════

        GameObject headerRow = CreateRow(panelGO.transform, "HeaderArea", 40);

        GameObject pasteCodeInput = CreateInputField(headerRow.transform, "PasteCodeInput", "Вставьте код сплава...", 200);
        AddFlexible(pasteCodeInput, 1f);

        GameObject pasteBtn = CreateButton(headerRow.transform, "PasteButton", "Вставить", 100);
        GameObject applyBtn = CreateButton(headerRow.transform, "ApplyCodeButton", "Применить", 110);

        // ═══════════════ СТРОКА 2: Параметры печи ═══════════════

        GameObject furnaceInfoRow = CreateRow(panelGO.transform, "FurnaceInfoArea", 30);

        GameObject capacityLbl = CreateTextLabel(furnaceInfoRow.transform, "CapacityLabel",
            "Ёмкость: 100.000 кг", 16, Color.white, TextAlignmentOptions.Left);
        AddFlexible(capacityLbl, 1f);

        GameObject furnaceTierLbl = CreateTextLabel(furnaceInfoRow.transform, "FurnaceTierLabel",
            "Тир плавильни: T1", 16, Color.white, TextAlignmentOptions.Center);
        AddFlexible(furnaceTierLbl, 1f);

        GameObject efficiencyLbl = CreateTextLabel(furnaceInfoRow.transform, "EfficiencyLabel",
            "Эффективность: 100%", 16, Color.white, TextAlignmentOptions.Right);
        AddFlexible(efficiencyLbl, 1f);

        // ═══════════════ СТРОКА 3: Тир металла ═══════════════

        GameObject metalTierRow = CreateRow(panelGO.transform, "MetalTierArea", 35);

        GameObject metalTierLbl = CreateTextLabel(metalTierRow.transform, "MetalTierLabel",
            "Тир металла: T1", 16, Color.white, TextAlignmentOptions.Left);
        AddFixed(metalTierLbl, 160);

        GameObject sliderGO = CreateSlider(metalTierRow.transform, "MetalTierSlider", 1, 10, 1);
        AddFlexible(sliderGO, 1f);

        // ═══════════════ СТРОКА 4: Количество металла ═══════════════

        GameObject metalAmountRow = CreateRow(panelGO.transform, "MetalAmountArea", 35);

        GameObject metalAmountLbl = CreateTextLabel(metalAmountRow.transform, "MetalAmountLbl",
            "Металл:", 16, Color.white, TextAlignmentOptions.Left);
        AddFixed(metalAmountLbl, 70);

        GameObject metalAmountInput = CreateInputField(metalAmountRow.transform, "MetalAmountInput", "100.000", 140);

        GameObject kgLbl1 = CreateTextLabel(metalAmountRow.transform, "KgLabel1",
            "кг", 16, Color.white, TextAlignmentOptions.Left);
        AddFixed(kgLbl1, 30);

        GameObject maxToggle = CreateToggle(metalAmountRow.transform, "MaxAmountToggle", "Максимум", true);
        AddFlexible(maxToggle, 1f);

        // ═══════════════ СТРОКА 5: Химикаты ═══════════════

        GameObject chemRow = CreateRow(panelGO.transform, "ChemicalsArea", 30);

        GameObject chemToggle = CreateToggle(chemRow.transform, "UseChemicalsToggle", "Химикаты", false);
        AddFixed(chemToggle, 150);

        GameObject chemInfoLbl = CreateTextLabel(chemRow.transform, "ChemicalsInfoLabel",
            "", 14, Color.white, TextAlignmentOptions.Left);
        AddFlexible(chemInfoLbl, 1f);

        // ═══════════════ СТРОКА 6: Наниты ═══════════════

        GameObject nanRow = CreateRow(panelGO.transform, "NanitesArea", 30);

        GameObject nanToggle = CreateToggle(nanRow.transform, "UseNanitesToggle", "Наниты", false);
        AddFixed(nanToggle, 150);

        GameObject nanInfoLbl = CreateTextLabel(nanRow.transform, "NanitesInfoLabel",
            "", 14, Color.white, TextAlignmentOptions.Left);
        AddFlexible(nanInfoLbl, 1f);

        // ═══════════════ СТРОКИ 7-10: Поглощения и сопротивления ═══════════════

        // Заголовок
        GameObject statsHeader = CreateRow(panelGO.transform, "StatsHeader", 22);
        CreateTextLabel(statsHeader.transform, "H1", "", 14, Color.clear, TextAlignmentOptions.Left); // пустое место
        AddFixed(statsHeader.transform.GetChild(0).gameObject, 100);
        CreateTextLabel(statsHeader.transform, "H2", "Поглощение", 13, Color.gray, TextAlignmentOptions.Center);
        AddFlexible(statsHeader.transform.GetChild(1).gameObject, 1f);
        CreateTextLabel(statsHeader.transform, "H3", "Сопротивление", 13, Color.gray, TextAlignmentOptions.Center);
        AddFlexible(statsHeader.transform.GetChild(2).gameObject, 1f);

        // Кинетика
        var kin = CreateStatRow(panelGO.transform, "KineticArea", "Кинетика",
            "KinAbsorbMinus", "KinAbsorbInput", "KinAbsorbPlus",
            "KinResistMinus", "KinResistInput", "KinResistPlus");

        // Термика
        var therm = CreateStatRow(panelGO.transform, "ThermalArea", "Термика",
            "ThermAbsorbMinus", "ThermAbsorbInput", "ThermAbsorbPlus",
            "ThermResistMinus", "ThermResistInput", "ThermResistPlus");

        // Химия
        var chem = CreateStatRow(panelGO.transform, "ChemicalArea", "Химия",
            "ChemAbsorbMinus", "ChemAbsorbInput", "ChemAbsorbPlus",
            "ChemResistMinus", "ChemResistInput", "ChemResistPlus");

        // Энергия
        var ener = CreateStatRow(panelGO.transform, "EnergyArea", "Энергия",
            "EnergyAbsorbMinus", "EnergyAbsorbInput", "EnergyAbsorbPlus",
            "EnergyResistMinus", "EnergyResistInput", "EnergyResistPlus");

        // ═══════════════ СТРОКА 11: Очки ═══════════════

        GameObject pointsRow = CreateRow(panelGO.transform, "PointsArea", 30);

        GameObject basePtsLbl = CreateTextLabel(pointsRow.transform, "BasePointsLabel",
            "Базовые очки: 300", 16, Color.white, TextAlignmentOptions.Left);
        AddFlexible(basePtsLbl, 1f);

        GameObject freePtsLbl = CreateTextLabel(pointsRow.transform, "FreePointsLabel",
            "Свободные очки: 300", 16, new Color(0.2f, 1f, 0.2f), TextAlignmentOptions.Right);
        AddFlexible(freePtsLbl, 1f);

        // ═══════════════ СТРОКА 12: Энергия и выход ═══════════════

        GameObject energyOutRow = CreateRow(panelGO.transform, "EnergyOutputArea", 30);

        GameObject energyCostLbl = CreateTextLabel(energyOutRow.transform, "EnergyCostLabel",
            "Затраты энергии: 0", 16, Color.white, TextAlignmentOptions.Left);
        AddFlexible(energyCostLbl, 1f);

        GameObject outputLbl = CreateTextLabel(energyOutRow.transform, "OutputAmountLabel",
            "Получаемый сплав: 0.000 кг", 16, Color.white, TextAlignmentOptions.Right);
        AddFlexible(outputLbl, 1f);

        // ═══════════════ СТРОКА 13: Код сплава ═══════════════

        GameObject codeRow = CreateRow(panelGO.transform, "CodeArea", 35);

        GameObject codeLbl = CreateTextLabel(codeRow.transform, "AlloyCodeLabel",
            "1-K0/000-T0/000-C0/000-E0/000", 14, new Color(0.8f, 0.8f, 1f),
            TextAlignmentOptions.Left);
        AddFlexible(codeLbl, 1f);

        GameObject copyBtn = CreateButton(codeRow.transform, "CopyCodeButton", "Копировать", 110);

        // ═══════════════ СТРОКА 14: Кнопки управления ═══════════════

        GameObject ctrlRow = CreateRow(panelGO.transform, "ControlArea", 40);

        // Распорка слева
        GameObject spacer = new GameObject("Spacer", typeof(RectTransform));
        spacer.transform.SetParent(ctrlRow.transform, false);
        AddFlexible(spacer, 1f);

        GameObject resetBtn = CreateButton(ctrlRow.transform, "ResetButton", "Сброс", 120);
        SetButtonColor(resetBtn, new Color(0.6f, 0.6f, 0.6f));

        // Распорка между кнопками
        GameObject spacer2 = new GameObject("Spacer2", typeof(RectTransform));
        spacer2.transform.SetParent(ctrlRow.transform, false);
        AddFixed(spacer2, 20);

        GameObject craftBtn = CreateButton(ctrlRow.transform, "CraftButton", "Изготовить", 160);
        SetButtonColor(craftBtn, new Color(0.2f, 0.7f, 0.2f));

        // Распорка справа
        GameObject spacer3 = new GameObject("Spacer3", typeof(RectTransform));
        spacer3.transform.SetParent(ctrlRow.transform, false);
        AddFlexible(spacer3, 1f);

        // ═══════════════ ПРИВЯЗКА К FurnaceUI ═══════════════

        FurnaceUI ui = panelGO.AddComponent<FurnaceUI>();

        // Используем SerializedObject для назначения private [SerializeField] полей
        SerializedObject so = new SerializedObject(ui);

        SetField(so, "furnacePanel", panelGO);
        SetField(so, "errorMessageLabel", errorGO);

        // Header
        SetField(so, "pasteCodeInput", pasteCodeInput);
        SetField(so, "pasteButton", pasteBtn);
        SetField(so, "applyCodeButton", applyBtn);

        // Furnace info
        SetField(so, "capacityLabel", capacityLbl);
        SetField(so, "furnaceTierLabel", furnaceTierLbl);
        SetField(so, "efficiencyLabel", efficiencyLbl);

        // Metal tier
        SetField(so, "metalTierSlider", sliderGO);
        SetField(so, "metalTierLabel", metalTierLbl);

        // Metal amount
        SetField(so, "metalAmountInput", metalAmountInput);
        SetField(so, "maxAmountToggle", maxToggle);

        // Chemicals
        SetField(so, "useChemicalsToggle", chemToggle);
        SetField(so, "chemicalsInfoLabel", chemInfoLbl);

        // Nanites
        SetField(so, "useNanitesToggle", nanToggle);
        SetField(so, "nanitesInfoLabel", nanInfoLbl);

        // Kinetic
        SetField(so, "kinAbsorbMinus", FindChild(kin, "KinAbsorbMinus"));
        SetField(so, "kinAbsorbInput", FindChild(kin, "KinAbsorbInput"));
        SetField(so, "kinAbsorbPlus", FindChild(kin, "KinAbsorbPlus"));
        SetField(so, "kinResistMinus", FindChild(kin, "KinResistMinus"));
        SetField(so, "kinResistInput", FindChild(kin, "KinResistInput"));
        SetField(so, "kinResistPlus", FindChild(kin, "KinResistPlus"));

        // Thermal
        SetField(so, "thermAbsorbMinus", FindChild(therm, "ThermAbsorbMinus"));
        SetField(so, "thermAbsorbInput", FindChild(therm, "ThermAbsorbInput"));
        SetField(so, "thermAbsorbPlus", FindChild(therm, "ThermAbsorbPlus"));
        SetField(so, "thermResistMinus", FindChild(therm, "ThermResistMinus"));
        SetField(so, "thermResistInput", FindChild(therm, "ThermResistInput"));
        SetField(so, "thermResistPlus", FindChild(therm, "ThermResistPlus"));

        // Chemical
        SetField(so, "chemAbsorbMinus", FindChild(chem, "ChemAbsorbMinus"));
        SetField(so, "chemAbsorbInput", FindChild(chem, "ChemAbsorbInput"));
        SetField(so, "chemAbsorbPlus", FindChild(chem, "ChemAbsorbPlus"));
        SetField(so, "chemResistMinus", FindChild(chem, "ChemResistMinus"));
        SetField(so, "chemResistInput", FindChild(chem, "ChemResistInput"));
        SetField(so, "chemResistPlus", FindChild(chem, "ChemResistPlus"));

        // Energy
        SetField(so, "energyAbsorbMinus", FindChild(ener, "EnergyAbsorbMinus"));
        SetField(so, "energyAbsorbInput", FindChild(ener, "EnergyAbsorbInput"));
        SetField(so, "energyAbsorbPlus", FindChild(ener, "EnergyAbsorbPlus"));
        SetField(so, "energyResistMinus", FindChild(ener, "EnergyResistMinus"));
        SetField(so, "energyResistInput", FindChild(ener, "EnergyResistInput"));
        SetField(so, "energyResistPlus", FindChild(ener, "EnergyResistPlus"));

        // Points
        SetField(so, "basePointsLabel", basePtsLbl);
        SetField(so, "freePointsLabel", freePtsLbl);

        // Energy cost & output
        SetField(so, "energyCostLabel", energyCostLbl);
        SetField(so, "outputAmountLabel", outputLbl);

        // Code
        SetField(so, "alloyCodeLabel", codeLbl);
        SetField(so, "copyCodeButton", copyBtn);

        // Control
        SetField(so, "resetButton", resetBtn);
        SetField(so, "craftButton", craftBtn);

        so.ApplyModifiedProperties();

        // Выключить панель по умолчанию
        panelGO.SetActive(false);

        Debug.Log("FurnacePanel создана успешно! Можно удалить FurnacePanelBuilder.");

        // Пометить сцену как изменённую
        EditorUtility.SetDirty(panelGO);
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
            UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene());
    }

    // ═══════════════ ФАБРИЧНЫЕ МЕТОДЫ ═══════════════

    private GameObject CreatePanel(Transform parent, string name,
        Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot,
        Vector2 size, Vector2 pos, Color color)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);

        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.pivot = pivot;
        rt.sizeDelta = size;
        rt.anchoredPosition = pos;

        Image img = go.GetComponent<Image>();
        img.color = color;

        return go;
    }

    private GameObject CreateRow(Transform parent, string name, float height)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);

        var le = go.AddComponent<LayoutElement>();
        le.preferredHeight = height;
        le.minHeight = height;

        var hlg = go.AddComponent<HorizontalLayoutGroup>();
        hlg.spacing = 6;
        hlg.childAlignment = TextAnchor.MiddleLeft;
        hlg.childControlWidth = false;
        hlg.childControlHeight = true;
        hlg.childForceExpandWidth = false;
        hlg.childForceExpandHeight = true;

        return go;
    }

    private GameObject CreateTextLabel(Transform parent, string name,
        string text, int fontSize, Color color,
        TextAlignmentOptions align, float height = 0)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);

        TextMeshProUGUI tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = fontSize;
        tmp.color = color;
        tmp.alignment = align;
        tmp.textWrappingMode = TextWrappingModes.NoWrap;
        tmp.overflowMode = TextOverflowModes.Ellipsis;
        tmp.richText = true;

        if (height > 0)
        {
            var le = go.AddComponent<LayoutElement>();
            le.preferredHeight = height;
        }

        return go;
    }

    private GameObject CreateButton(Transform parent, string name, string label, float width)
    {
        // Кнопка с фоном
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);

        Image img = go.GetComponent<Image>();
        img.color = new Color(0.25f, 0.25f, 0.3f, 1f);

        var le = go.AddComponent<LayoutElement>();
        le.preferredWidth = width;
        le.minWidth = width;

        // Текст внутри
        GameObject textGO = new GameObject("Text", typeof(RectTransform));
        textGO.transform.SetParent(go.transform, false);

        TextMeshProUGUI tmp = textGO.AddComponent<TextMeshProUGUI>();
        tmp.text = label;
        tmp.fontSize = 14;
        tmp.color = Color.white;
        tmp.alignment = TextAlignmentOptions.Center;

        RectTransform textRT = textGO.GetComponent<RectTransform>();
        textRT.anchorMin = Vector2.zero;
        textRT.anchorMax = Vector2.one;
        textRT.sizeDelta = Vector2.zero;
        textRT.offsetMin = Vector2.zero;
        textRT.offsetMax = Vector2.zero;

        return go;
    }

    private GameObject CreateInputField(Transform parent, string name, string placeholder, float width)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);

        Image img = go.GetComponent<Image>();
        img.color = new Color(0.15f, 0.15f, 0.2f, 1f);

        var le = go.AddComponent<LayoutElement>();
        le.preferredWidth = width;
        le.minWidth = width;

        // Text Area
        GameObject textArea = new GameObject("Text Area", typeof(RectTransform));
        textArea.transform.SetParent(go.transform, false);
        RectTransform taRT = textArea.GetComponent<RectTransform>();
        taRT.anchorMin = Vector2.zero;
        taRT.anchorMax = Vector2.one;
        taRT.offsetMin = new Vector2(5, 2);
        taRT.offsetMax = new Vector2(-5, -2);

        // Placeholder
        GameObject phGO = new GameObject("Placeholder", typeof(RectTransform));
        phGO.transform.SetParent(textArea.transform, false);
        TextMeshProUGUI phTMP = phGO.AddComponent<TextMeshProUGUI>();
        phTMP.text = placeholder;
        phTMP.fontSize = 14;
        phTMP.color = new Color(0.5f, 0.5f, 0.5f, 0.7f);
        phTMP.alignment = TextAlignmentOptions.Left;
        RectTransform phRT = phGO.GetComponent<RectTransform>();
        phRT.anchorMin = Vector2.zero;
        phRT.anchorMax = Vector2.one;
        phRT.offsetMin = Vector2.zero;
        phRT.offsetMax = Vector2.zero;

        // Text
        GameObject txtGO = new GameObject("Text", typeof(RectTransform));
        txtGO.transform.SetParent(textArea.transform, false);
        TextMeshProUGUI txtTMP = txtGO.AddComponent<TextMeshProUGUI>();
        txtTMP.text = "";
        txtTMP.fontSize = 14;
        txtTMP.color = Color.white;
        txtTMP.alignment = TextAlignmentOptions.Left;
        RectTransform txtRT = txtGO.GetComponent<RectTransform>();
        txtRT.anchorMin = Vector2.zero;
        txtRT.anchorMax = Vector2.one;
        txtRT.offsetMin = Vector2.zero;
        txtRT.offsetMax = Vector2.zero;

        // TMP_InputField
        TMP_InputField input = go.AddComponent<TMP_InputField>();
        input.textViewport = taRT;
        input.textComponent = txtTMP;
        input.placeholder = phTMP;
        input.fontAsset = txtTMP.font;

        return go;
    }

    private GameObject CreateSlider(Transform parent, string name, float min, float max, float value)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Slider));
        go.transform.SetParent(parent, false);

        var le = go.AddComponent<LayoutElement>();
        le.flexibleWidth = 1;

        // Background
        GameObject bg = new GameObject("Background", typeof(RectTransform), typeof(Image));
        bg.transform.SetParent(go.transform, false);
        bg.GetComponent<Image>().color = new Color(0.2f, 0.2f, 0.25f);
        RectTransform bgRT = bg.GetComponent<RectTransform>();
        bgRT.anchorMin = new Vector2(0, 0.25f);
        bgRT.anchorMax = new Vector2(1, 0.75f);
        bgRT.sizeDelta = Vector2.zero;
        bgRT.offsetMin = Vector2.zero;
        bgRT.offsetMax = Vector2.zero;

        // Fill Area
        GameObject fillArea = new GameObject("Fill Area", typeof(RectTransform));
        fillArea.transform.SetParent(go.transform, false);
        RectTransform faRT = fillArea.GetComponent<RectTransform>();
        faRT.anchorMin = new Vector2(0, 0.25f);
        faRT.anchorMax = new Vector2(1, 0.75f);
        faRT.offsetMin = new Vector2(5, 0);
        faRT.offsetMax = new Vector2(-5, 0);

        GameObject fill = new GameObject("Fill", typeof(RectTransform), typeof(Image));
        fill.transform.SetParent(fillArea.transform, false);
        fill.GetComponent<Image>().color = new Color(0.3f, 0.6f, 0.9f);
        RectTransform fillRT = fill.GetComponent<RectTransform>();
        fillRT.anchorMin = Vector2.zero;
        fillRT.anchorMax = Vector2.one;
        fillRT.sizeDelta = Vector2.zero;

        // Handle Area
        GameObject handleArea = new GameObject("Handle Slide Area", typeof(RectTransform));
        handleArea.transform.SetParent(go.transform, false);
        RectTransform haRT = handleArea.GetComponent<RectTransform>();
        haRT.anchorMin = Vector2.zero;
        haRT.anchorMax = Vector2.one;
        haRT.offsetMin = new Vector2(10, 0);
        haRT.offsetMax = new Vector2(-10, 0);

        GameObject handle = new GameObject("Handle", typeof(RectTransform), typeof(Image));
        handle.transform.SetParent(handleArea.transform, false);
        handle.GetComponent<Image>().color = Color.white;
        RectTransform hRT = handle.GetComponent<RectTransform>();
        hRT.sizeDelta = new Vector2(20, 0);

        // Setup Slider
        Slider slider = go.GetComponent<Slider>();
        slider.fillRect = fillRT;
        slider.handleRect = hRT;
        slider.minValue = min;
        slider.maxValue = max;
        slider.value = value;
        slider.wholeNumbers = true;

        return go;
    }

    private GameObject CreateToggle(Transform parent, string name, string label, bool isOn)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Toggle));
        go.transform.SetParent(parent, false);

        var hlg = go.AddComponent<HorizontalLayoutGroup>();
        hlg.spacing = 4;
        hlg.childAlignment = TextAnchor.MiddleLeft;
        hlg.childControlWidth = false;
        hlg.childControlHeight = true;
        hlg.childForceExpandWidth = false;
        hlg.childForceExpandHeight = false;

        // Checkbox background
        GameObject bgGO = new GameObject("Background", typeof(RectTransform), typeof(Image));
        bgGO.transform.SetParent(go.transform, false);
        bgGO.GetComponent<Image>().color = new Color(0.2f, 0.2f, 0.25f);
        var bgLE = bgGO.AddComponent<LayoutElement>();
        bgLE.preferredWidth = 20;
        bgLE.preferredHeight = 20;

        // Checkmark
        GameObject checkGO = new GameObject("Checkmark", typeof(RectTransform), typeof(Image));
        checkGO.transform.SetParent(bgGO.transform, false);
        checkGO.GetComponent<Image>().color = new Color(0.3f, 0.8f, 0.3f);
        RectTransform checkRT = checkGO.GetComponent<RectTransform>();
        checkRT.anchorMin = new Vector2(0.15f, 0.15f);
        checkRT.anchorMax = new Vector2(0.85f, 0.85f);
        checkRT.sizeDelta = Vector2.zero;
        checkRT.offsetMin = Vector2.zero;
        checkRT.offsetMax = Vector2.zero;

        // Label
        GameObject lblGO = CreateTextLabel(go.transform, "Label", label, 14, Color.white,
            TextAlignmentOptions.Left);
        AddFixed(lblGO, 120);

        // Setup Toggle
        Toggle toggle = go.GetComponent<Toggle>();
        toggle.graphic = checkGO.GetComponent<Image>();
        toggle.targetGraphic = bgGO.GetComponent<Image>();
        toggle.isOn = isOn;

        return go;
    }

    private GameObject CreateStatRow(Transform parent, string areaName, string label,
        string absMinusName, string absInputName, string absPlusName,
        string resMinusName, string resInputName, string resPlusName)
    {
        GameObject row = CreateRow(parent, areaName, 32);

        // Label
        GameObject lbl = CreateTextLabel(row.transform, areaName + "Label", label,
            14, Color.white, TextAlignmentOptions.Left);
        AddFixed(lbl, 100);

        // Absorb: [-] [input] [+]
        GameObject absMinus = CreateButton(row.transform, absMinusName, "−", 30);
        GameObject absInput = CreateInputField(row.transform, absInputName, "0", 80);
        GameObject absPlus = CreateButton(row.transform, absPlusName, "+", 30);

        // Разделитель
        GameObject sep = CreateTextLabel(row.transform, areaName + "Sep", "|",
            14, Color.gray, TextAlignmentOptions.Center);
        AddFixed(sep, 15);

        // Resist: [-] [input] [+]
        GameObject resMinus = CreateButton(row.transform, resMinusName, "−", 30);
        GameObject resInput = CreateInputField(row.transform, resInputName, "0.0%", 90);
        GameObject resPlus = CreateButton(row.transform, resPlusName, "+", 30);

        // Распорка справа
        GameObject spacer = new GameObject("Spacer", typeof(RectTransform));
        spacer.transform.SetParent(row.transform, false);
        AddFlexible(spacer, 1f);

        return row;
    }

    // ═══════════════ LAYOUT ХЕЛПЕРЫ ═══════════════

    private void AddFlexible(GameObject go, float flex)
    {
        var le = go.GetComponent<LayoutElement>();
        if (le == null) le = go.AddComponent<LayoutElement>();
        le.flexibleWidth = flex;
    }

    private void AddFixed(GameObject go, float width)
    {
        var le = go.GetComponent<LayoutElement>();
        if (le == null) le = go.AddComponent<LayoutElement>();
        le.preferredWidth = width;
        le.minWidth = width;
    }

    private void SetButtonColor(GameObject btnGO, Color color)
    {
        Image img = btnGO.GetComponent<Image>();
        if (img != null) img.color = color;
    }

    // ═══════════════ SERIALIZED OBJECT ХЕЛПЕРЫ ═══════════════

    private void SetField(SerializedObject so, string fieldName, GameObject go)
    {
        SerializedProperty prop = so.FindProperty(fieldName);
        if (prop == null)
        {
            Debug.LogWarning($"Поле '{fieldName}' не найдено в FurnaceUI");
            return;
        }

        // Определяем какой компонент нужен по типу поля
        Component target = null;

        string typeName = prop.type;

        if (typeName.Contains("Button"))
            target = go.GetComponent<Button>();
        else if (typeName.Contains("Slider"))
            target = go.GetComponent<Slider>();
        else if (typeName.Contains("Toggle"))
            target = go.GetComponent<Toggle>();
        else if (typeName.Contains("InputField") || typeName.Contains("TMP_InputField"))
            target = go.GetComponent<TMP_InputField>();
        else if (typeName.Contains("TextMeshProUGUI") || typeName.Contains("TMP_Text"))
            target = go.GetComponent<TextMeshProUGUI>();
        else if (typeName.Contains("GameObject"))
        {
            prop.objectReferenceValue = go;
            return;
        }

        if (target != null)
            prop.objectReferenceValue = target;
        else
            prop.objectReferenceValue = go;
    }

    private GameObject FindChild(GameObject parent, string name)
    {
        Transform t = parent.transform.Find(name);
        if (t != null) return t.gameObject;

        // Поиск по всем детям
        foreach (Transform child in parent.transform)
        {
            if (child.name == name) return child.gameObject;
        }

        Debug.LogWarning($"Дочерний объект '{name}' не найден в '{parent.name}'");
        return null;
    }

#endif
}