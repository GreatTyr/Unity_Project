using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
/// <summary>
/// Представление (View) для Верстака Топливных Баков.
/// Рисует IMGUI интерфейс и передает команды в FuelTankWorkbenchController.
/// Не содержит никакой математики и бизнес-логики.
/// </summary>
[RequireComponent(typeof(FuelTankWorkbenchController))]
public class FuelTankWorkbenchUI : MonoBehaviour, IWorkbenchUI
{
    private FuelTankWorkbenchController controller;
    private bool panelOpen;
    private Rect windowRect;
    private bool windowRectInitialized;
    private Vector2 scrollPos;
    // Временные строковые буферы для UI
    private string shellPercentStr = "5";
    private string codeInputField = "";
    // Стили
    private static Texture2D _bgTex, _panelTex, _sepTex, _progBgTex, _progFillTex;
    private static GUIStyle _windowStyle, _panelStyle;
    private GUIStyle _centeredBold, _boldStyle;
    // Стейт для кастомного выпадающего списка
    private Dictionary<string, int> _pendingSelections = new Dictionary<string, int>();
    private void Awake()
    {
        controller = GetComponent<FuelTankWorkbenchController>();
    }
    public void OpenPanel()
    {
        panelOpen = true;
        controller.Initialize();
        shellPercentStr = controller.ShellPercent.ToString();
        codeInputField = "";
    }
    public void ClosePanel()
    {
        panelOpen = false;
        WorkbenchPopup.Hide();
    }
    private void Update()
    {
        if (panelOpen && Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            ClosePanel();
        }
    }
    private void OnGUI()
    {
        if (!panelOpen) return;
        InitStyles();
        if (!windowRectInitialized)
        {
            windowRect = new Rect(Screen.width * 0.02f, Screen.height * 0.05f,
                Mathf.Min(1100f, Screen.width * 0.96f), Screen.height * 0.9f);
            windowRectInitialized = true;
        }
        windowRect.x = Mathf.Clamp(windowRect.x, 0, Mathf.Max(0, Screen.width - windowRect.width));
        windowRect.y = Mathf.Clamp(windowRect.y, 0, Mathf.Max(0, Screen.height - windowRect.height));
        if (WorkbenchPopup.IsShowing && Event.current.type == EventType.MouseDown &&
            !WorkbenchPopup.PopupRect.Contains(Event.current.mousePosition))
        {
            WorkbenchPopup.Hide();
            Event.current.Use();
        }
        windowRect = GUI.Window(GetInstanceID(), windowRect, DrawWindow, "FuelTank Workbench", _windowStyle);
        WorkbenchPopup.DrawPopup();
    }
    private void DrawWindow(int id)
    {
        GUI.DragWindow(new Rect(0, 0, 10000, 20));
        bool isCrafting = controller.IsCrafting;
        GUILayout.BeginArea(new Rect(20, 35, windowRect.width - 40, windowRect.height - 45));
        scrollPos = GUILayout.BeginScrollView(scrollPos, false, true);
        GUILayout.BeginVertical();
        // Сообщения
        if (!string.IsNullOrEmpty(controller.ErrorMessage))
            GUILayout.Label($"<color=#FF4444><b>⚠ ОШИБКА: {controller.ErrorMessage}</b></color>",
                GetCenteredBoldStyle());
        if (!string.IsNullOrEmpty(controller.WarningMessage))
            GUILayout.Label($"<color=#FFCC00><b>⚠ ПРЕДУПРЕЖДЕНИЕ: {controller.WarningMessage}</b></color>",
                GetCenteredBoldStyle());
        if (!string.IsNullOrEmpty(controller.SuccessMessage))
            GUILayout.Label($"<color=#00FF66><b>✓ {controller.SuccessMessage}</b></color>",
                GetCenteredBoldStyle());
        GUILayout.Label(
            $"<color=#AAAAAA>Параметры Верстака:</color> Тир {controller.workbenchTier} | Вместимость: {controller.innerLength}×{controller.innerHeight}×{controller.innerWidth} м",
            new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter });
        if (isCrafting) GUI.enabled = false;
        // Блок Кодов
        GUILayout.BeginHorizontal();
        GUILayout.BeginVertical(GUILayout.Width((windowRect.width - 40) * 0.75f));
        string tankCode = controller.CurrentModuleCode;
        DrawCompactCodeSection("ГЕНЕРАЦИЯ КОДА", ref tankCode, true, "КОПИРОВАТЬ", () =>
        {
            if (!string.IsNullOrEmpty(controller.CurrentModuleCode))
                GUIUtility.systemCopyBuffer = controller.CurrentModuleCode;
        });
        GUILayout.Space(5);
        DrawCompactCodeSection("ВВОД ЧЕРТЕЖА", ref codeInputField, false, "ВСТАВИТЬ", () =>
        {
            codeInputField = (GUIUtility.systemCopyBuffer ?? "").Trim();
        }, "ПРИМЕНИТЬ", () =>
        {
            controller.ApplyBlueprintCode(codeInputField);
            shellPercentStr = controller.ShellPercent.ToString();
        });
        GUILayout.EndVertical();
        GUILayout.FlexibleSpace();
        GUILayout.EndHorizontal();
        DrawSeparator();
        // Основной блок параметров
        GUILayout.BeginHorizontal();
        GUILayout.BeginVertical(GUILayout.Width(((windowRect.width - 40) - 30) * 0.55f));
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
        DrawFuelTankSpecificSection();
        DrawSeparator();
        DrawAlloyParamsSection();
        DrawSeparator();
        if (isCrafting) GUI.enabled = true;
        // Нижний блок: Стоимость и Крафт
        GUILayout.BeginHorizontal();
        GUILayout.BeginVertical(GUILayout.Width((windowRect.width - 40) * 0.75f));
        DrawCostsAndButtons(isCrafting);
        GUILayout.EndVertical();
        GUILayout.FlexibleSpace();
        GUILayout.EndHorizontal();
        GUILayout.EndVertical();
        GUILayout.EndScrollView();
        GUILayout.EndArea();
        GUI.enabled = true;
    }
    // ================= ОТРИСОВКА СЕКЦИЙ =================
    private void DrawSelectionSection()
    {
        GUILayout.BeginVertical(_panelStyle);
        GUILayout.Label("<color=#E0E0E0>ВЫБОР ЭТАЛОНА</color>", GetBoldStyle());
        GUILayout.BeginHorizontal();
        if (controller.RefNames.Length > 0)
        {
            int curIdx = controller.SelectedRefIndex;
            int newIdx = DrawDropdown("wb_ref", curIdx, controller.RefNames);
            if (newIdx != curIdx) controller.SelectReference(newIdx);
        }
        else
        {
            GUILayout.Label("<color=#FF8888>(Нет эталонов в БД)</color>");
        }
        GUILayout.EndHorizontal();
        if (controller.SelectedRef != null)
        {
            var r = controller.SelectedRef;
            string fac = string.IsNullOrEmpty(r.FactionShortName) ? "—" : r.FactionShortName;
            GUILayout.Label(
                $"<color=#AAAAAA>Тир:</color> {r.ModuleTier}  |  <color=#AAAAAA>ID:</color> {fac}-{r.BlueprintId}  |  <color=#AAAAAA>VolCoeff:</color> {r.VolumeCoefficientPercent:F1}%");
        }
        GUILayout.EndVertical();
    }
    private void DrawShellSection()
    {
        GUILayout.BeginVertical(_panelStyle);
        GUILayout.Label("<color=#E0E0E0>ОБОЛОЧКА (стенки бака)</color>", GetBoldStyle());
        GUILayout.BeginHorizontal();
        GUILayout.Label("Объем %:", GUILayout.Width(70));
        string newStr = GUILayout.TextField(shellPercentStr, GUILayout.Width(60));
        if (newStr != shellPercentStr && float.TryParse(newStr, out float parsed))
        {
            shellPercentStr = newStr;
            controller.SetShellPercent(parsed);
        }
        GUILayout.Space(5);
        GUILayout.Label("1%", GUILayout.Width(25));
        // ИЗМЕНЕНО: максимум 99% вместо 100%
        float sliderRaw = GUILayout.HorizontalSlider(controller.ShellPercent, 1f, 99f);
        if (Mathf.RoundToInt(sliderRaw) != controller.ShellPercent)
        {
            controller.SetShellPercent(sliderRaw);
            shellPercentStr = controller.ShellPercent.ToString();
        }
        GUILayout.Label("99%", GUILayout.Width(40));
        GUILayout.EndHorizontal();
        GUILayout.BeginHorizontal();
        GUILayout.Label("Сплав:", GUILayout.Width(70));
        if (controller.AlloyDisplayNames.Length > 0)
        {
            int curIdx = controller.SelectedAlloyIndex;
            int newIdx = DrawDropdown("wb_alloy", curIdx, controller.AlloyDisplayNames);
            if (newIdx != curIdx) controller.SelectAlloy(newIdx);
        }
        else
        {
            GUILayout.Label("<color=#FFCC00>(не выбран)</color>", GUILayout.MinWidth(150));
        }
        GUILayout.EndHorizontal();
        GUILayout.EndVertical();
    }
    private void DrawScalingSection()
    {
        GUILayout.BeginVertical(_panelStyle);
        GUILayout.Label("<color=#E0E0E0>МАСШТАБИРОВАНИЕ</color>", GetBoldStyle());
        string[] modeNames = { "По Длине", "По Ширине", "По Высоте", "По Массе", "По Объёму" };
        int curMode = (int)controller.Scaler.CurrentScaleMode;
        int newMode = GUILayout.SelectionGrid(curMode, modeNames, 3);
        if (newMode != curMode) controller.SetScaleMode((ModuleScaler.ScaleMode)newMode);
        GUILayout.Space(5);
        GUILayout.BeginHorizontal();
        GUILayout.Label("Ввод:", GUILayout.Width(80));
        string currentStr = controller.Scaler.ScaleInputStr;
        string newScaleStr = GUILayout.TextField(currentStr);
        if (newScaleStr != currentStr) controller.HandleScaleInput(newScaleStr);
        GUILayout.EndHorizontal();
        GUILayout.Space(5);
        if (GUILayout.Button("СБРОСИТЬ МАСШТАБ", GUILayout.Height(24)))
        {
            controller.ResetScale();
        }
        GUILayout.EndVertical();
    }
    private void DrawComputedSection()
    {
        GUILayout.BeginVertical(_panelStyle);
        GUILayout.Label("<color=#E0E0E0>ГЕОМЕТРИЯ И ФИЗИКА</color>", GetBoldStyle());
        var sc = controller.Scaler;
        bool fits = sc.CalcLength <= controller.innerLength &&
                    sc.CalcWidth <= controller.innerWidth &&
                    sc.CalcHeight <= controller.innerHeight;
        string dimColor = fits ? "#00FF00" : "#FF4444";
        DrawGridRow("Длина (X):", $"<color={dimColor}>{sc.CalcLength:F3} м</color>",
            "Объём Real:", $"{sc.CalcRealVolume:F4} м³");
        DrawGridRow("Ширина (Z):", $"<color={dimColor}>{sc.CalcWidth:F3} м</color>",
            "Объём оболочки:", $"{sc.CalcShellVolume:F4} м³");
        DrawGridRow("Высота (Y):", $"<color={dimColor}>{sc.CalcHeight:F3} м</color>",
            "Полость (для топлива):", $"{sc.CalcEffectiveVolume:F4} м³");
        DrawSeparator();
        DrawGridRow("Масса оболочки:", $"{sc.CalcShellMass:F1} кг",
            "Прочность:", $"<color=#FFD700>{sc.CalcDurability:F1}</color>");
        DrawGridRow("ОБЩАЯ МАССА:", $"<b>{sc.CalcTotalMass:F1} кг</b>", "", "");
        if (!fits)
        {
            GUILayout.Space(10);
            GUILayout.Label(
                $"<color=#FF4444><b>⚠ ГАБАРИТЫ ПРЕВЫШАЮТ КАМЕРУ ВЕРСТАКА (Макс: {controller.innerLength}x{controller.innerWidth}x{controller.innerHeight})</b></color>",
                GetCenteredBoldStyle());
        }
        if (controller.SelectedRef != null && controller.SelectedRef.ModuleTier > controller.workbenchTier)
        {
            GUILayout.Space(5);
            GUILayout.Label(
                $"<color=#FF4444><b>⚠ ТИР ЭТАЛОНА (T{controller.SelectedRef.ModuleTier}) ВЫШЕ ТИРА ВЕРСТАКА (T{controller.workbenchTier})</b></color>",
                GetCenteredBoldStyle());
        }
        GUILayout.EndVertical();
    }
    private void DrawFuelTankSpecificSection()
    {
        GUILayout.BeginVertical("box");
        GUILayout.Label("Параметры Топливного Бака", GetBoldStyle());
        GUILayout.BeginHorizontal();
        ParamBox("Ёмкость", $"<color=#00FFFF>{controller.CalcCapacity:F3}</color>");
        ParamBox("Теплоемкость", $"{controller.CalcHeatCapacity:F1}");
        ParamBox("Макс. T", $"{controller.CalcMaxTemperature:F0}°");
        // ИЗМЕНЕНО: читаем толщину стенок из Scaler
        ParamBox("Толщина стенок", $"{controller.Scaler.CalcWallThicknessMm:F1} мм");
        ParamBox("Время крафта", $"<color=#00FF00>{controller.CalcCraftTimeSeconds:F1} сек</color>");
        GUILayout.EndHorizontal();
        GUILayout.EndVertical();
    }
    private void DrawAlloyParamsSection()
    {
        GUILayout.BeginVertical(_panelStyle);
        GUILayout.Label("<color=#E0E0E0>АНАЛИЗ ОБОЛОЧКИ</color>", GetBoldStyle());
        if (!controller.IsAlloyDecoded)
        {
            GUILayout.Label("<color=#FFCC00>(Сплав не выбран или не распознан)</color>");
            GUILayout.EndVertical();
            return;
        }
        var p = controller.AlloyParams;
        GUILayout.BeginHorizontal();
        GUILayout.Label($"<color=#AAAAAA>Тир сплава:</color> <b>{p.tier}</b>", GUILayout.Width(130));
        GUILayout.Label(
            $"<color=#AAAAAA>Химикаты:</color> {(p.useChemicals ? "<color=#00FF00>Да</color>" : "<color=#FF4444>Нет</color>")}",
            GUILayout.Width(120));
        GUILayout.Label(
            $"<color=#AAAAAA>Наниты:</color> {(p.useNanites ? "<color=#00FF00>Да</color>" : "<color=#FF4444>Нет</color>")}",
            GUILayout.Width(120));
        GUILayout.EndHorizontal();
        GUILayout.Space(5);
        float colW = (windowRect.width - 70) / 4f;
        GUILayout.BeginHorizontal();
        DrawAlloyCol("KINETIC", p.kineticAbsorption, p.kineticResistance, colW);
        DrawAlloyCol("THERMAL", p.thermalAbsorption, p.thermalResistance, colW);
        DrawAlloyCol("CHEMICAL", p.chemicalAbsorption, p.chemicalResistance, colW);
        DrawAlloyCol("ENERGY", p.energyAbsorption, p.energyResistance, colW);
        GUILayout.EndHorizontal();
        GUILayout.EndVertical();
    }
    private void DrawCostsAndButtons(bool isCrafting)
    {
        GUILayout.BeginVertical(_panelStyle);
        GUILayout.Label("<color=#E0E0E0>ТРЕБОВАНИЯ ПРОИЗВОДСТВА</color>", GetBoldStyle());
        // Сплав
        float alloyReq = controller.Scaler.CalcShellMass;
        float alloyAvail = 0f;
        if (controller.alloyStorage != null && controller.AlloyCodes.Length > 0 && controller.SelectedAlloyIndex >= 0)
        {
            alloyAvail = (float)controller.alloyStorage.GetMass(controller.AlloyCodes[controller.SelectedAlloyIndex]);
        }
        // Энергия
        long energyReq = controller.CalcEnergyCost;
        long energyAvail = controller.resourcesStorage != null ? controller.resourcesStorage.EnergyUnits : 0;
        GUILayout.BeginHorizontal();
        DrawCostItem("Сплав", alloyReq, alloyAvail, "кг", alloyAvail >= alloyReq - 0.001f);
        GUILayout.FlexibleSpace();
        // Металл не нужен — бак состоит только из оболочки
        DrawCostItem("Энергия", energyReq, energyAvail, "E", energyAvail >= energyReq, "#FFD700");
        GUILayout.EndHorizontal();
        GUILayout.Space(6);
        GUILayout.Label(
            $"<color=#AAAAAA>Время крафта:</color> <color=#00FF00><b>{controller.CalcCraftTimeSeconds:F1} сек</b></color>");
        GUILayout.Space(15);
        GUILayout.BeginHorizontal();
        GUILayout.Label("Размещение готового модуля:", GUILayout.Width(220));
        GUI.enabled = !isCrafting;
        controller.placementMode = (FuelTankWorkbenchController.CraftPlacementMode)GUILayout.Toolbar(
            (int)controller.placementMode, new[] { "В сцену (Мир)", "На склад (Storage)" }, GUILayout.Height(24));
        GUI.enabled = true;
        GUILayout.EndHorizontal();
        GUILayout.Space(10);
        GUILayout.BeginHorizontal();
        if (isCrafting)
        {
            Rect barRect = GUILayoutUtility.GetRect(200, 35, GUILayout.ExpandWidth(true));
            DrawProgressBar(barRect, controller.CraftProgress, "ПРОИЗВОДСТВО...");
        }
        else
        {
            bool canCraft = controller.CanCraft(out _);
            Color oldBg = GUI.backgroundColor;
            GUI.backgroundColor = canCraft ? new Color(0.2f, 0.6f, 0.3f) : new Color(0.4f, 0.2f, 0.2f);
            GUI.enabled = canCraft;
            if (GUILayout.Button("◆ ИЗГОТОВИТЬ МОДУЛЬ ◆", GUILayout.Height(35)))
            {
                controller.ExecuteCraft();
            }
            GUI.enabled = true;
            GUI.backgroundColor = oldBg;
        }
        GUILayout.Space(10);
        GUI.enabled = !isCrafting;
        if (GUILayout.Button("СБРОС", GUILayout.Height(35), GUILayout.Width(100)))
        {
            controller.ResetToDefaults();
            shellPercentStr = "5";
            codeInputField = "";
        }
        GUI.enabled = true;
        GUILayout.EndHorizontal();
        GUILayout.EndVertical();
    }
    // ================= УТИЛИТЫ ОТРИСОВКИ =================
    private void DrawCompactCodeSection(string title, ref string text, bool readOnly, string btn1, Action act1,
        string btn2 = null, Action act2 = null)
    {
        WorkbenchUICommon.DrawCompactCodeSection(
            title,
            ref text,
            readOnly,
            btn1,
            act1,
            _panelStyle,
            GetBoldStyle(),
            btn2,
            act2
        );
    }
    private void DrawGridRow(string l1, string v1, string l2, string v2)
    {
        GUILayout.BeginHorizontal();
        GUILayout.Label($"<color=#AAAAAA>{l1}</color>", GUILayout.Width(135));
        GUILayout.Label(v1, GUILayout.Width(100));
        GUILayout.Label($"<color=#AAAAAA>{l2}</color>", GUILayout.Width(140));
        GUILayout.Label(v2);
        GUILayout.EndHorizontal();
    }
    private void ParamBox(string label, string val)
    {
        GUILayout.BeginVertical(GUILayout.Width(130));
        GUILayout.Label($"<color=#AAAAAA>{label}</color>", new GUIStyle(GUI.skin.label) { fontSize = 12 });
        GUILayout.Label(val, GetBoldStyle());
        GUILayout.EndVertical();
    }
    private void DrawAlloyCol(string title, int absorb, float resist, float width)
    {
        GUILayout.BeginVertical("box", GUILayout.Width(width));
        GUILayout.Label($"<color=#CCCCCC><b>{title}</b></color>");
        GUILayout.Label($"Поглощение: <b>{absorb}</b>");
        GUILayout.Label($"Сопротивление: <b>{resist:F1}%</b>");
        GUILayout.EndVertical();
    }
    private void DrawCostItem(string label, float needed, float available, string unit, bool enough,
        string highlightColor = "#FFFFFF")
    {
        GUILayout.BeginVertical(GUILayout.MinWidth(110));
        GUILayout.Label(
            $"<color=#AAAAAA>{label}:</color> <color={highlightColor}><b>{needed:F1} {unit}</b></color>");
        string availStr = enough
            ? $"<color=#00FF00>{available:F1}</color>"
            : $"<color=#FF4444>{available:F1}</color>";
        GUILayout.Label($"На складе: {availStr} {unit}", new GUIStyle(GUI.skin.label) { fontSize = 11 });
        GUILayout.EndVertical();
    }
    // Перегрузка для long (энергия)
    private void DrawCostItem(string label, long needed, long available, string unit, bool enough,
        string highlightColor = "#FFFFFF")
    {
        GUILayout.BeginVertical(GUILayout.MinWidth(110));
        GUILayout.Label(
            $"<color=#AAAAAA>{label}:</color> <color={highlightColor}><b>{needed} {unit}</b></color>");
        string availStr = enough
            ? $"<color=#00FF00>{available}</color>"
            : $"<color=#FF4444>{available}</color>";
        GUILayout.Label($"На складе: {availStr} {unit}", new GUIStyle(GUI.skin.label) { fontSize = 11 });
        GUILayout.EndVertical();
    }
    private int DrawDropdown(string tag, int selected, string[] options)
    {
        if (options == null || options.Length == 0) return selected;
        selected = Mathf.Clamp(selected, 0, options.Length - 1);
        GUIStyle btnStyle = new GUIStyle(GUI.skin.button) { fontSize = 13, fontStyle = FontStyle.Normal };
        if (GUILayout.Button(options[selected], btnStyle, GUILayout.MinWidth(150), GUILayout.Height(25)))
        {
            WorkbenchPopup.Show(options, selected,
                GUIUtility.GUIToScreenPoint(Event.current.mousePosition),
                idx => _pendingSelections[tag] = idx);
        }
        if (_pendingSelections.TryGetValue(tag, out int result))
        {
            _pendingSelections.Remove(tag);
            return Mathf.Clamp(result, 0, options.Length - 1);
        }
        return selected;
    }
    private void DrawSeparator()
    {
        WorkbenchUICommon.DrawSeparator(_sepTex);
    }
    private void DrawProgressBar(Rect rect, float progress, string text)
    {
        WorkbenchUICommon.DrawProgressBar(rect, progress, text);
    }
    private void InitStyles()
    {
        if (_bgTex == null) _bgTex = WorkbenchPopup.MakeTex(1, 1, new Color(0.15f, 0.15f, 0.15f, 0.98f));
        if (_panelTex == null) _panelTex = WorkbenchPopup.MakeTex(1, 1, new Color(0.2f, 0.2f, 0.2f, 0.9f));
        if (_sepTex == null) _sepTex = WorkbenchPopup.MakeTex(1, 1, new Color(0.35f, 0.35f, 0.35f, 0.5f));
        if (_windowStyle == null)
            _windowStyle = new GUIStyle(GUI.skin.window)
            {
                normal = { background = _bgTex, textColor = Color.white },
                fontSize = 14,
                fontStyle = FontStyle.Bold
            };
        if (_panelStyle == null)
            _panelStyle = new GUIStyle(GUI.skin.box)
            {
                normal = { background = _panelTex, textColor = Color.white },
                padding = new RectOffset(10, 10, 10, 10),
                margin = new RectOffset(0, 0, 5, 5)
            };
        GUI.skin.label.richText = true;
    }
    private GUIStyle GetCenteredBoldStyle()
    {
        if (_centeredBold == null)
            _centeredBold = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontStyle = FontStyle.Bold,
                fontSize = 15
            };
        return _centeredBold;
    }
    private GUIStyle GetBoldStyle()
    {
        if (_boldStyle == null)
            _boldStyle = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold };
        return _boldStyle;
    }
}