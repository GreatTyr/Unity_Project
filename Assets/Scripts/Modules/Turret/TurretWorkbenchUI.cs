// TurretWorkbenchUI.cs
using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// IMGUI интерфейс верстака турелей.
/// </summary>
[RequireComponent(typeof(TurretWorkbenchController))]
public class TurretWorkbenchUI : MonoBehaviour, IWorkbenchUI
{
    private TurretWorkbenchController controller;
    private bool panelOpen;
    private Rect windowRect;
    private bool windowRectInitialized;
    private Vector2 scrollPos;

    private string codeInputField = "";

    // Буферы ввода
    private string sBarrelInner = "100";
    private string sBarrelOuter = "120";
    private string sBarrelLength = "1000";
    private string sLoadingPct = "33";
    private string sChamberPct = "33";
    private string sGyroPct = "33";
    private string sCompensatorPct = "33";
    private string sPropTier = "1";
    private string sPropMass = "0,001";
    private string sPreviewAngle = "45";

    private static Texture2D _bgTex, _panelTex, _sepTex;
    private static GUIStyle _windowStyle, _panelStyle;
    private GUIStyle _centeredBold, _boldStyle;

    private readonly Dictionary<string, int> _pendingSelections
        = new Dictionary<string, int>();

    private void Awake()
    {
        controller = GetComponent<TurretWorkbenchController>();
    }

    public void OpenPanel()
    {
        panelOpen = true;
        controller.Initialize();
        PullBuffers();
        codeInputField = "";
    }

    public void ClosePanel()
    {
        panelOpen = false;
        WorkbenchPopup.Hide();
    }

    private void Update()
    {
        if (panelOpen &&
            Keyboard.current != null &&
            Keyboard.current.escapeKey.wasPressedThisFrame)
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
            windowRect = new Rect(
                Screen.width * 0.02f,
                Screen.height * 0.05f,
                Mathf.Min(1200f, Screen.width * 0.96f),
                Screen.height * 0.9f);
            windowRectInitialized = true;
        }

        windowRect.x = Mathf.Clamp(windowRect.x, 0,
            Mathf.Max(0, Screen.width - windowRect.width));
        windowRect.y = Mathf.Clamp(windowRect.y, 0,
            Mathf.Max(0, Screen.height - windowRect.height));

        if (WorkbenchPopup.IsShowing &&
            Event.current.type == EventType.MouseDown &&
            !WorkbenchPopup.PopupRect.Contains(Event.current.mousePosition))
        {
            WorkbenchPopup.Hide();
            Event.current.Use();
        }

        windowRect = GUI.Window(GetInstanceID(), windowRect,
            DrawWindow, "Turret Workbench", _windowStyle);
        WorkbenchPopup.DrawPopup();
    }

    private void DrawWindow(int id)
    {
        GUI.DragWindow(new Rect(0, 0, 10000, 20));
        bool isCrafting = controller.IsCrafting;

        GUILayout.BeginArea(new Rect(20, 35,
            windowRect.width - 40, windowRect.height - 45));
        scrollPos = GUILayout.BeginScrollView(scrollPos, false, true);
        GUILayout.BeginVertical();

        DrawMessages();

        if (isCrafting) GUI.enabled = false;

        DrawWorkbenchSection();
        DrawSeparator();

        GUILayout.BeginHorizontal();

        // Левая колонка — настройки
        GUILayout.BeginVertical(GUILayout.Width((windowRect.width - 60f) * 0.5f));
        DrawReferenceSection();
        DrawScalingSection();
        DrawAlloySection();
        DrawReceiverSection();
        DrawBarrelSection();
        DrawMountSection();
        DrawAmmoSelectionSection();
        GUILayout.EndVertical();

        GUILayout.Space(10);

        // Правая колонка — результаты
        GUILayout.BeginVertical();
        DrawParametersSection();
        DrawAmmoResultsSection();
        GUILayout.EndVertical();

        GUILayout.EndHorizontal();

        DrawSeparator();

        if (isCrafting) GUI.enabled = true;

        GUILayout.BeginHorizontal();
        GUILayout.BeginVertical(GUILayout.Width((windowRect.width - 40) * 0.75f));
        DrawCostsAndButtons(isCrafting);
        GUILayout.EndVertical();
        GUILayout.FlexibleSpace();
        GUILayout.EndHorizontal();

        DrawSeparator();
        DrawCodeSection();

        GUILayout.EndVertical();
        GUILayout.EndScrollView();
        GUILayout.EndArea();

        GUI.enabled = true;
    }

    // =========================================
    // SECTIONS
    // =========================================

    private void DrawMessages()
    {
        if (!string.IsNullOrEmpty(controller.ErrorMessage))
            GUILayout.Label(
                $"<color=#FF4444><b>⚠ ОШИБКА: {controller.ErrorMessage}</b></color>",
                GetCenteredBoldStyle());

        if (!string.IsNullOrEmpty(controller.WarningMessage))
            GUILayout.Label(
                $"<color=#FFCC00><b>⚠ ПРЕДУПРЕЖДЕНИЕ: {controller.WarningMessage}</b></color>",
                GetCenteredBoldStyle());

        if (!string.IsNullOrEmpty(controller.SuccessMessage))
            GUILayout.Label(
                $"<color=#00FF66><b>✓ {controller.SuccessMessage}</b></color>",
                GetCenteredBoldStyle());
    }

    private void DrawCodeSection()
    {
        GUILayout.BeginVertical(_panelStyle);
        GUILayout.Label("<color=#E0E0E0>КОД ТУРЕЛИ</color>", GetBoldStyle());

        GUILayout.BeginHorizontal();
        string generatedCode = controller.CurrentModuleCode;
        GUI.enabled = false;
        generatedCode = GUILayout.TextArea(generatedCode, GUILayout.Height(90f), GUILayout.ExpandWidth(true));
        GUI.enabled = true;

        GUILayout.BeginVertical(GUILayout.Width(100));
        if (GUILayout.Button("КОПИРОВАТЬ", GUILayout.Height(24)))
        {
            if (!string.IsNullOrEmpty(controller.CurrentModuleCode))
                GUIUtility.systemCopyBuffer = controller.CurrentModuleCode;
        }
        GUILayout.EndVertical();
        GUILayout.EndHorizontal();

        GUILayout.Space(10);

        GUILayout.Label("<color=#E0E0E0>ВВОД ЧЕРТЕЖА</color>", GetBoldStyle());

        GUILayout.BeginHorizontal();
        codeInputField = GUILayout.TextArea(codeInputField, GUILayout.Height(90f), GUILayout.ExpandWidth(true));

        GUILayout.BeginVertical(GUILayout.Width(100));
        if (GUILayout.Button("ВСТАВИТЬ", GUILayout.Height(24)))
        {
            codeInputField = (GUIUtility.systemCopyBuffer ?? "").Trim();
        }
        if (GUILayout.Button("ПРИМЕНИТЬ", GUILayout.Height(24)))
        {
            controller.ApplyBlueprintCode(codeInputField);
            PullBuffers();
        }
        GUILayout.EndVertical();
        GUILayout.EndHorizontal();

        GUILayout.EndVertical();
    }

    private void DrawWorkbenchSection()
    {
        GUILayout.BeginVertical(_panelStyle);
        GUILayout.Label("<color=#E0E0E0>ПАРАМЕТРЫ ВЕРСТАКА</color>", GetBoldStyle());
        GUILayout.BeginHorizontal();
        ParamBox("Тир верстака", $"T{controller.workbenchTier}");
        ParamBox("Длина камеры", $"{controller.innerLength:F2} м");
        ParamBox("Ширина камеры", $"{controller.innerWidth:F2} м");
        ParamBox("Высота камеры", $"{controller.innerHeight:F2} м");
        GUILayout.EndHorizontal();
        GUILayout.EndVertical();
    }

    private void DrawReferenceSection()
    {
        GUILayout.BeginVertical(_panelStyle);
        GUILayout.Label("<color=#E0E0E0>ВЫБОР ЭТАЛОНА</color>", GetBoldStyle());

        if (controller.RefNames.Length > 0)
        {
            int cur = controller.SelectedRefIndex;
            int nw = DrawDropdown("wb_ref", cur, controller.RefNames);
            if (nw != cur) { controller.SelectReference(nw); PullBuffers(); }
        }
        else
        {
            GUILayout.Label("<color=#FF8888>(Нет эталонов в БД)</color>");
        }

        if (controller.SelectedRef != null)
        {
            var r = controller.SelectedRef;
            string fac = string.IsNullOrEmpty(r.FactionShortName) ? "—" : r.FactionShortName;
            GUILayout.Label(
                $"<color=#AAAAAA>Тир:</color> {r.ModuleTier}  " +
                $"|  <color=#AAAAAA>ID:</color> {fac}-{r.BlueprintId}  " +
                $"|  <color=#AAAAAA>MountCoeff:</color> {r.MountCoeff:F2}");
        }

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

        GUILayout.Space(4);
        GUILayout.BeginHorizontal();
        GUILayout.Label("Ввод:", GUILayout.Width(80));
        string current = controller.Scaler.ScaleInputStr;
        string newStr = GUILayout.TextField(current);
        if (newStr != current) controller.HandleScaleInput(newStr);
        GUILayout.EndHorizontal();

        if (GUILayout.Button("СБРОСИТЬ МАСШТАБ", GUILayout.Height(24)))
            controller.ResetScale();

        var sc = controller.Scaler;
        bool fits =
            sc.CalcLength <= controller.innerLength &&
            sc.CalcWidth <= controller.innerWidth &&
            sc.CalcHeight <= controller.innerHeight;

        string dc = fits ? "#00FF00" : "#FF4444";
        GUILayout.Label(
            $"<color={dc}>Д:{sc.CalcLength:F3}м  " +
            $"Ш:{sc.CalcWidth:F3}м  " +
            $"В:{sc.CalcHeight:F3}м</color>");

        GUILayout.EndVertical();
    }

    private void DrawAlloySection()
    {
        GUILayout.BeginVertical(_panelStyle);
        GUILayout.Label("<color=#E0E0E0>СПЛАВ</color>", GetBoldStyle());

        if (controller.AlloyDisplayNames.Length > 0)
        {
            int cur = controller.SelectedAlloyIndex;
            int nw = DrawDropdown("wb_alloy", cur, controller.AlloyDisplayNames);
            if (nw != cur) controller.SelectAlloy(nw);
        }
        else
        {
            GUILayout.Label("<color=#FFCC00>(Сплав не выбран)</color>");
        }

        GUILayout.EndVertical();
    }

    private void DrawReceiverSection()
    {
        GUILayout.BeginVertical(_panelStyle);
        GUILayout.Label("<color=#E0E0E0>СТВОЛЬНАЯ КОРОБКА</color>", GetBoldStyle());

        int maxTier = controller.SelectedRef != null ? controller.SelectedRef.ModuleTier : 10;

        // Тир механизма заряжания
        GUILayout.BeginHorizontal();
        GUILayout.Label("Тир механизма заряжания:", GUILayout.Width(180));
        int lt = Mathf.RoundToInt(GUILayout.HorizontalSlider(controller.LoadingTier, 1, maxTier));
        GUILayout.Label($"T{controller.LoadingTier}", GUILayout.Width(30));
        if (lt != controller.LoadingTier) controller.SetLoadingTier(lt);
        GUILayout.EndHorizontal();

        // Процент механизма заряжания
        GUILayout.BeginHorizontal();
        GUILayout.Label("Механизм заряжания %:", GUILayout.Width(180));
        sLoadingPct = GUILayout.TextField(sLoadingPct, GUILayout.Width(50));
        if (GUILayout.Button("OK", GUILayout.Width(30)))
        {
            if (int.TryParse(sLoadingPct, out int lp))
            { controller.SetLoadingPercent(lp); PullBuffers(); }
        }
        float rawL = GUILayout.HorizontalSlider(controller.LoadingPercent, 1, 98);
        int newL = Mathf.RoundToInt(rawL);
        if (newL != controller.LoadingPercent)
        { controller.SetLoadingPercent(newL); PullBuffers(); }
        GUILayout.Label($"{controller.LoadingPercent}%", GUILayout.Width(40));
        GUILayout.EndHorizontal();

        GUILayout.Space(4);

        // Тир патронника
        GUILayout.BeginHorizontal();
        GUILayout.Label("Тир патронника:", GUILayout.Width(180));
        int cht = Mathf.RoundToInt(GUILayout.HorizontalSlider(controller.ChamberTier, 1, maxTier));
        GUILayout.Label($"T{controller.ChamberTier}", GUILayout.Width(30));
        if (cht != controller.ChamberTier) controller.SetChamberTier(cht);
        GUILayout.EndHorizontal();

        // Процент патронника
        GUILayout.BeginHorizontal();
        GUILayout.Label("Патронник %:", GUILayout.Width(180));
        sChamberPct = GUILayout.TextField(sChamberPct, GUILayout.Width(50));
        if (GUILayout.Button("OK", GUILayout.Width(30)))
        {
            if (int.TryParse(sChamberPct, out int cp))
            { controller.SetChamberPercent(cp); PullBuffers(); }
        }
        float rawC = GUILayout.HorizontalSlider(controller.ChamberPercent, 1, 98);
        int newC = Mathf.RoundToInt(rawC);
        if (newC != controller.ChamberPercent)
        { controller.SetChamberPercent(newC); PullBuffers(); }
        GUILayout.Label($"{controller.ChamberPercent}%", GUILayout.Width(40));
        GUILayout.EndHorizontal();

        GUILayout.EndVertical();
    }

    private void DrawBarrelSection()
    {
        GUILayout.BeginVertical(_panelStyle);
        GUILayout.Label("<color=#E0E0E0>СТВОЛ</color>", GetBoldStyle());

        FloatInputRow("Внутр. диам. (мм):", ref sBarrelInner, v =>
        { controller.SetBarrelInnerDiameter(v); PullBuffers(); });

        FloatInputRow("Внешн. диам. (мм):", ref sBarrelOuter, v =>
        { controller.SetBarrelOuterDiameter(v); PullBuffers(); });

        FloatInputRow("Длина (мм):", ref sBarrelLength, v =>
        { controller.SetBarrelLength(v); PullBuffers(); });

        GUILayout.EndVertical();
    }

    private void DrawMountSection()
    {
        GUILayout.BeginVertical(_panelStyle);
        GUILayout.Label("<color=#E0E0E0>СТАНИНА</color>", GetBoldStyle());

        // Гироскоп — высокий приоритет
        GUILayout.BeginHorizontal();
        GUILayout.Label("Гироскоп %:", GUILayout.Width(120));
        sGyroPct = GUILayout.TextField(sGyroPct, GUILayout.Width(50));
        if (GUILayout.Button("OK", GUILayout.Width(30)))
        {
            if (int.TryParse(sGyroPct, out int gp))
            { controller.SetGyroPercent(gp); PullBuffers(); }
        }
        float rawG = GUILayout.HorizontalSlider(controller.GyroPercent, 1, 98);
        int newG = Mathf.RoundToInt(rawG);
        if (newG != controller.GyroPercent)
        { controller.SetGyroPercent(newG); PullBuffers(); }
        GUILayout.Label($"{controller.GyroPercent}%", GUILayout.Width(40));
        GUILayout.EndHorizontal();

        // Компенсатор — высокий приоритет
        GUILayout.BeginHorizontal();
        GUILayout.Label("Компенсатор %:", GUILayout.Width(120));
        sCompensatorPct = GUILayout.TextField(sCompensatorPct, GUILayout.Width(50));
        if (GUILayout.Button("OK", GUILayout.Width(30)))
        {
            if (int.TryParse(sCompensatorPct, out int cp))
            { controller.SetCompensatorPercent(cp); PullBuffers(); }
        }
        float rawCo = GUILayout.HorizontalSlider(controller.CompensatorPercent, 1, 98);
        int newCo = Mathf.RoundToInt(rawCo);
        if (newCo != controller.CompensatorPercent)
        { controller.SetCompensatorPercent(newCo); PullBuffers(); }
        GUILayout.Label($"{controller.CompensatorPercent}%", GUILayout.Width(40));
        GUILayout.EndHorizontal();

        GUILayout.EndVertical();
    }

    private void DrawAmmoSelectionSection()
    {
        GUILayout.BeginVertical(_panelStyle);
        GUILayout.Label("<color=#E0E0E0>ВЫБОР БОЕПРИПАСА</color>", GetBoldStyle());

        if (controller.CompatibleAmmoCodes.Length == 0)
        {
            GUILayout.Label("<color=#FFCC00>Нет совместимых боеприпасов.</color>");
            GUILayout.EndVertical();
            return;
        }

        // Выбор боеприпаса
        int curAmmo = controller.SelectedAmmoIndex;
        int newAmmo = DrawDropdown("wb_ammo",
            Mathf.Max(0, curAmmo), controller.CompatibleAmmoNames);
        if (newAmmo != curAmmo) controller.SelectAmmo(newAmmo);

        var ammo = controller.LastAmmoResult;
        if (!ammo.isCompatible)
        {
            GUILayout.Label($"<color=#FF4444>{ammo.reason}</color>");
            GUILayout.EndVertical();
            return;
        }

        // Угол возвышения
        GUILayout.BeginHorizontal();
        GUILayout.Label("Угол возвышения:", GUILayout.Width(140));
        sPreviewAngle = GUILayout.TextField(sPreviewAngle, GUILayout.Width(60));
        if (GUILayout.Button("OK", GUILayout.Width(30)))
        {
            if (TryParseFloat(sPreviewAngle, out float ang))
                controller.SetPreviewAngle(ang);
        }
        GUILayout.Label($"{controller.PreviewAngleDeg:F1}°", GUILayout.Width(50));
        GUILayout.EndHorizontal();

        float rawAng = GUILayout.HorizontalSlider(controller.PreviewAngleDeg, 0f, 90f);
        if (Mathf.Abs(rawAng - controller.PreviewAngleDeg) > 0.1f)
        {
            controller.SetPreviewAngle(rawAng);
            sPreviewAngle = rawAng.ToString("F1",
                CultureInfo.InvariantCulture).Replace('.', ',');
        }

        var preview = controller.LastShotPreview;

        // Метательный заряд (только для ядер)
        if (preview.isCannonball)
        {
            GUILayout.Space(4);
            GUILayout.Label("<color=#AAAAAA>Метательный заряд (для ядер):</color>");

            // Тир
            GUILayout.BeginHorizontal();
            GUILayout.Label("Тир заряда:", GUILayout.Width(100));
            sPropTier = GUILayout.TextField(sPropTier, GUILayout.Width(40));
            if (GUILayout.Button("OK", GUILayout.Width(30)))
            {
                if (int.TryParse(sPropTier, out int pt))
                    controller.SetDefaultPropellantTier(pt);
            }
            GUILayout.Label($"T{controller.DefaultPropellantTier}", GUILayout.Width(30));
            GUILayout.EndHorizontal();

            int slPt = Mathf.RoundToInt(
                GUILayout.HorizontalSlider(controller.DefaultPropellantTier, 1, 10));
            if (slPt != controller.DefaultPropellantTier)
            {
                controller.SetDefaultPropellantTier(slPt);
                sPropTier = slPt.ToString();
            }

            // Масса
            FloatInputRow("Масса заряда (кг):", ref sPropMass, v =>
                controller.SetDefaultPropellantMass(v));

            float maxPropMass = Mathf.Max(0f, controller.CalcResult.chamberCapacity - ammo.ammoMassKg);
            GUILayout.Label(
                $"<color=#AAAAAA>Макс.: {maxPropMass:F3} кг</color>");
        }

        GUILayout.EndVertical();
    }

    private void DrawParametersSection()
    {
        GUILayout.BeginVertical(_panelStyle);
        GUILayout.Label("<color=#E0E0E0>ПАРАМЕТРЫ ТУРЕЛИ</color>", GetBoldStyle());

        var r = controller.CalcResult;
        var tpl = controller.SelectedRef;

        // Общие параметры
        GUILayout.Label("<color=#CCCCCC><b>Общие параметры</b></color>");
        GUILayout.BeginHorizontal();
        ParamBox("Тир турели", tpl != null ? $"T{tpl.ModuleTier}" : "—");
        ParamBox("Масса турели", $"{r.totalTurretMass:F3} кг");
        ParamBox("Прочность", $"{r.totalDurability:F3}");
        ParamBox("Крафт", $"{r.craftTimeSeconds:F1} с");
        GUILayout.EndHorizontal();

        if (tpl != null)
        {
            GUILayout.BeginHorizontal();
            ParamBox("Макс. угол возв.", $"+{tpl.MaxElevationDeg:F1}°");
            ParamBox("Макс. угол скл.", $"-{tpl.MaxDepressionDeg:F1}°");
            ParamBox("Сектор", $"{tpl.TraverseArcDeg:F1}°");
            ParamBox("Потребл. энергии", $"{tpl.EnergyConsumption:F2} E/s");
            GUILayout.EndHorizontal();
        }

        GUILayout.Space(6);

        // Параметры сплава
        if (controller.IsAlloyDecoded)
        {
            var p = controller.AlloyParams;
            GUILayout.Label($"<color=#CCCCCC><b>Параметры сплава (Тир {p.tier})</b></color>");

            GUILayout.BeginHorizontal();
            ParamBox("Кинет. погл.", $"{p.kineticAbsorption}");
            ParamBox("Терм. погл.", $"{p.thermalAbsorption}");
            ParamBox("Хим. погл.", $"{p.chemicalAbsorption}");
            ParamBox("Энерг. погл.", $"{p.energyAbsorption}");
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            ParamBox("Кинет. сопр.", $"{p.kineticResistance:F1}%");
            ParamBox("Терм. сопр.", $"{p.thermalResistance:F1}%");
            ParamBox("Хим. сопр.", $"{p.chemicalResistance:F1}%");
            ParamBox("Энерг. сопр.", $"{p.energyResistance:F1}%");
            GUILayout.EndHorizontal();
        }

        GUILayout.Space(6);

        // Ствольная коробка
        GUILayout.Label("<color=#CCCCCC><b>Ствольная коробка</b></color>");
        GUILayout.BeginHorizontal();
        ParamBox("Масса", $"{r.receiverMassKg:F3} кг");
        ParamBox("Масса корпуса", $"{r.corpusMassKg:F3} кг");
        ParamBox("Мощность мех. заряж.", $"{r.loadingPower:F3}");
        ParamBox("Макс. масса бп.", $"{r.chamberCapacity:F3} кг");
        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();
        ParamBox("Макс. тир бп.", $"T{r.maxAmmoTier}");
        ParamBox("Прочность", $"{r.receiverDurability:F3}");
        GUILayout.EndHorizontal();

        GUILayout.Space(6);

        // Ствол
        GUILayout.Label("<color=#CCCCCC><b>Ствол</b></color>");
        GUILayout.BeginHorizontal();
        ParamBox("Масса", $"{r.barrelMassKg:F3} кг");
        ParamBox("Стенка", $"{r.barrelWallThicknessMm:F2} мм");
        ParamBox("Коэфф. прочн.", $"{r.barrelStrengthCoeff:F4}");
        ParamBox("Калибр", $"{r.minCaliberMm:F1}–{r.maxCaliberMm:F1} мм");
        GUILayout.EndHorizontal();

        GUILayout.Label($"<color=#AAAAAA>Макс. длина снаряда: {r.maxAmmoLengthMm:F1} мм</color>");

        GUILayout.Space(6);

        // Станина
        GUILayout.Label("<color=#CCCCCC><b>Станина</b></color>");
        GUILayout.BeginHorizontal();
        ParamBox("Масса", $"{r.mountTotalMass:F3} кг");
        ParamBox("Двигатель", $"{r.motorPercent}% / {r.motorMassKg:F3} кг");
        ParamBox("Гироскоп", $"{controller.GyroPercent}% / {r.gyroMassKg:F3} кг");
        ParamBox("Компенсатор", $"{controller.CompensatorPercent}% / {r.compensatorMassKg:F3} кг");
        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();
        ParamBox("Скорость поворота", $"{r.rotationSpeed:F3}");
        ParamBox("Скорость сведения", $"{r.aimSpeed:F3}");
        ParamBox("Гашение отдачи", $"{r.recoilResistance:F3}");
        GUILayout.EndHorizontal();

        GUILayout.EndVertical();
    }

    private void DrawAmmoResultsSection()
    {
        GUILayout.BeginVertical(_panelStyle);
        GUILayout.Label("<color=#E0E0E0>ОЦЕНКА СТРЕЛЬБЫ</color>", GetBoldStyle());

        if (controller.CompatibleAmmoCodes.Length == 0)
        {
            GUILayout.Label("<color=#FFCC00>Нет боеприпасов.</color>");
            GUILayout.EndVertical();
            return;
        }

        var preview = controller.LastShotPreview;
        var ammo = controller.LastAmmoResult;

        if (!ammo.isCompatible)
        {
            GUILayout.Label($"<color=#FF4444>Несовместим</color>");
            GUILayout.EndVertical();
            return;
        }

        string typeStr = preview.isCannonball ? "Ядро" : "Снаряд/Пуля";
        GUILayout.Label($"<color=#AAAAAA>Тип:</color> {typeStr}  " +
                         $"<color=#AAAAAA>Диам.:</color> {ammo.diameterMm:F1} мм  " +
                         $"<color=#AAAAAA>Масса:</color> {ammo.ammoMassKg:F3} кг  " +
                         $"<color=#AAAAAA>Тир:</color> T{ammo.ammoTier}");

        if (!preview.valid && !string.IsNullOrEmpty(preview.error))
        {
            GUILayout.Label($"<color=#FF8800>⚠ {preview.error}</color>");
        }

        GUILayout.Space(4);

        // Баллистика
        GUILayout.BeginHorizontal();
        ParamBox("Скорость", $"{preview.projectileSpeed:F1} м/с");
        ParamBox("Точность", $"{preview.accuracy:F4}°");
        ParamBox("Дальность", $"{preview.flightDistance:F1} м");
        ParamBox("Макс. высота", $"{preview.maxHeight:F1} м");
        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();
        ParamBox("Время полёта", $"{preview.flightTime:F2} с");
        ParamBox("Прям. выстр.", $"{preview.directFireRange:F1} м");
        ParamBox("Прям. урон", $"{preview.directDamage:F3}");
        ParamBox("Прям. пробит.", $"{preview.directPenetration:F3}");
        GUILayout.EndHorizontal();

        // Скорострельность и перезарядка
        GUILayout.Space(4);
        GUILayout.BeginHorizontal();
        ParamBox("Скорострельность", $"{preview.rateOfFireRPM:F2} выстр/мин");
        ParamBox("Перезарядка", $"{preview.reloadTimeS:F2} с");
        GUILayout.EndHorizontal();

        // Предупреждение о лучевой механике
        if (preview.rateOfFireRPM > 600f)
        {
            GUILayout.Label(
                "<color=#FFD700><b>⚡ При скорострельности более 10 выстр/с механика турели меняется на лучевую.</b></color>");
        }

        GUILayout.EndVertical();
    }

    private void DrawCostsAndButtons(bool isCrafting)
    {
        GUILayout.BeginVertical(_panelStyle);
        GUILayout.Label("<color=#E0E0E0>ТРЕБОВАНИЯ ПРОИЗВОДСТВА</color>",
            GetBoldStyle());

        float alloyReq = controller.CalcResult.totalTurretMass;
        float alloyAvail = 0f;
        if (controller.alloyStorage != null &&
            controller.AlloyCodes.Length > 0 &&
            controller.SelectedAlloyIndex >= 0)
        {
            alloyAvail = (float)controller.alloyStorage.GetMass(
                controller.AlloyCodes[controller.SelectedAlloyIndex]);
        }

        long energyReq = controller.CalcResult.energyCost;
        long energyAvail = controller.resourcesStorage != null
            ? controller.resourcesStorage.EnergyUnits
            : 0;

        GUILayout.BeginHorizontal();
        DrawCostItem("Сплав", alloyReq, alloyAvail, "кг",
            alloyAvail >= alloyReq - 0.001f);
        GUILayout.Space(15);
        DrawCostItem("Энергия", energyReq, energyAvail, "E",
            energyAvail >= energyReq, "#FFD700");
        GUILayout.FlexibleSpace();
        GUILayout.EndHorizontal();

        if (controller.RequiredInternalResources.Count > 0)
        {
            GUILayout.Space(8);
            GUILayout.Label("<color=#AAAAAA>Внутренние компоненты:</color>",
                new GUIStyle(GUI.skin.label) { fontSize = 11 });

            GUILayout.BeginHorizontal();
            foreach (var kvp in controller.RequiredInternalResources)
            {
                float reqKg = kvp.Value / 1000f;
                float availKg = controller.resourcesStorage != null
                    ? controller.resourcesStorage.GetGrams(kvp.Key) / 1000f
                    : 0f;
                string resName = ResourcesStorage.ResourceFullName((int)kvp.Key);
                DrawCostItem(resName, reqKg, availKg, "кг", availKg >= reqKg - 0.001f);
                GUILayout.Space(10);
            }
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
        }

        GUILayout.Space(6);
        GUILayout.Label(
            $"<color=#AAAAAA>Время крафта:</color> " +
            $"<color=#00FF00><b>{controller.CalcResult.craftTimeSeconds:F1} сек</b></color>");

        GUILayout.Space(15);

        GUILayout.BeginHorizontal();
        GUILayout.Label("Размещение:", GUILayout.Width(120));
        GUI.enabled = !isCrafting;
        controller.placementMode =
            (TurretWorkbenchController.CraftPlacementMode)GUILayout.Toolbar(
                (int)controller.placementMode,
                new[] { "В сцену (Мир)", "На склад (Storage)" },
                GUILayout.Height(24));
        GUI.enabled = true;
        GUILayout.EndHorizontal();

        GUILayout.Space(10);
        GUILayout.BeginHorizontal();

        if (isCrafting)
        {
            Rect barRect = GUILayoutUtility.GetRect(
                200, 35, GUILayout.ExpandWidth(true));
            DrawProgressBar(barRect, controller.CraftProgress, "ПРОИЗВОДСТВО...");
        }
        else
        {
            bool canCraft = controller.CanCraft(out _);
            Color oldBg = GUI.backgroundColor;
            GUI.backgroundColor = canCraft
                ? new Color(0.2f, 0.6f, 0.3f)
                : new Color(0.4f, 0.2f, 0.2f);
            GUI.enabled = canCraft;

            if (GUILayout.Button("◆ ИЗГОТОВИТЬ ТУРЕЛЬ ◆", GUILayout.Height(35)))
                controller.ExecuteCraft();

            GUI.enabled = true;
            GUI.backgroundColor = oldBg;
        }

        GUILayout.Space(10);
        GUI.enabled = !isCrafting;
        if (GUILayout.Button("СБРОС", GUILayout.Height(35), GUILayout.Width(100)))
        {
            controller.ResetToDefaults();
            PullBuffers();
            codeInputField = "";
        }
        GUI.enabled = true;

        GUILayout.EndHorizontal();
        GUILayout.EndVertical();
    }

    // =========================================
    // HELPERS
    // =========================================

    private void PullBuffers()
    {
        sBarrelInner = controller.BarrelInnerDiameterMm.ToString(
            "F2", CultureInfo.InvariantCulture).Replace('.', ',');
        sBarrelOuter = controller.BarrelOuterDiameterMm.ToString(
            "F2", CultureInfo.InvariantCulture).Replace('.', ',');
        sBarrelLength = controller.BarrelLengthMm.ToString(
            "F1", CultureInfo.InvariantCulture).Replace('.', ',');
        sLoadingPct = controller.LoadingPercent.ToString();
        sChamberPct = controller.ChamberPercent.ToString();
        sGyroPct = controller.GyroPercent.ToString();
        sCompensatorPct = controller.CompensatorPercent.ToString();
        sPropTier = controller.DefaultPropellantTier.ToString();
        sPropMass = controller.DefaultPropellantMassKg.ToString(
            "0.###", CultureInfo.InvariantCulture).Replace('.', ',');
        sPreviewAngle = controller.PreviewAngleDeg.ToString(
            "F1", CultureInfo.InvariantCulture).Replace('.', ',');
    }

    private void FloatInputRow(string label, ref string buffer, Action<float> setter)
    {
        GUILayout.BeginHorizontal();
        GUILayout.Label(label, GUILayout.Width(160));
        buffer = GUILayout.TextField(buffer, GUILayout.Width(80));
        if (GUILayout.Button("OK", GUILayout.Width(30)))
        {
            if (TryParseFloat(buffer, out float v)) setter(v);
        }
        GUILayout.EndHorizontal();
    }

    private bool TryParseFloat(string s, out float v)
    {
        string norm = (s ?? "").Replace(',', '.');
        return float.TryParse(norm,
            System.Globalization.NumberStyles.Float,
            CultureInfo.InvariantCulture, out v);
    }

    private void ParamBox(string label, string val)
    {
        GUILayout.BeginVertical(GUILayout.Width(130));
        GUILayout.Label(
            $"<color=#AAAAAA>{label}</color>",
            new GUIStyle(GUI.skin.label) { fontSize = 11 });
        GUILayout.Label(val, GetBoldStyle());
        GUILayout.EndVertical();
    }

    private void DrawCostItem(string label, float needed, float available,
        string unit, bool enough, string highlightColor = "#FFFFFF")
    {
        GUILayout.BeginVertical(GUILayout.MinWidth(110));
        GUILayout.Label(
            $"<color=#AAAAAA>{label}:</color> " +
            $"<color={highlightColor}><b>{needed:F3} {unit}</b></color>");
        string avStr = enough
            ? $"<color=#00FF00>{available:F3}</color>"
            : $"<color=#FF4444>{available:F3}</color>";
        GUILayout.Label($"На складе: {avStr} {unit}",
            new GUIStyle(GUI.skin.label) { fontSize = 11 });
        GUILayout.EndVertical();
    }

    private void DrawCostItem(string label, long needed, long available,
        string unit, bool enough, string highlightColor = "#FFFFFF")
    {
        GUILayout.BeginVertical(GUILayout.MinWidth(110));
        GUILayout.Label(
            $"<color=#AAAAAA>{label}:</color> " +
            $"<color={highlightColor}><b>{needed} {unit}</b></color>");
        string avStr = enough
            ? $"<color=#00FF00>{available}</color>"
            : $"<color=#FF4444>{available}</color>";
        GUILayout.Label($"На складе: {avStr} {unit}",
            new GUIStyle(GUI.skin.label) { fontSize = 11 });
        GUILayout.EndVertical();
    }

    private int DrawDropdown(string tag, int selected, string[] options)
    {
        if (options == null || options.Length == 0) return selected;
        selected = Mathf.Clamp(selected, 0, options.Length - 1);

        var btnStyle = new GUIStyle(GUI.skin.button)
        { fontSize = 13, fontStyle = FontStyle.Normal };

        if (GUILayout.Button(options[selected], btnStyle,
            GUILayout.MinWidth(150), GUILayout.Height(25)))
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
        if (_bgTex == null)
            _bgTex = WorkbenchPopup.MakeTex(1, 1,
                new Color(0.15f, 0.15f, 0.15f, 0.98f));
        if (_panelTex == null)
            _panelTex = WorkbenchPopup.MakeTex(1, 1,
                new Color(0.2f, 0.2f, 0.2f, 0.9f));
        if (_sepTex == null)
            _sepTex = WorkbenchPopup.MakeTex(1, 1,
                new Color(0.35f, 0.35f, 0.35f, 0.5f));

        if (_windowStyle == null)
        {
            _windowStyle = new GUIStyle(GUI.skin.window)
            {
                normal = { background = _bgTex, textColor = Color.white },
                fontSize = 14,
                fontStyle = FontStyle.Bold
            };
        }

        if (_panelStyle == null)
        {
            _panelStyle = new GUIStyle(GUI.skin.box)
            {
                normal = { background = _panelTex, textColor = Color.white },
                padding = new RectOffset(10, 10, 10, 10),
                margin = new RectOffset(0, 0, 5, 5)
            };
        }

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
            _boldStyle = new GUIStyle(GUI.skin.label)
            { fontStyle = FontStyle.Bold };
        return _boldStyle;
    }
}