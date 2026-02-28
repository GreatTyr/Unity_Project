using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.InputSystem;

public abstract class BaseModuleWorkbench : MonoBehaviour
{
    [Header("Workbench Parameters")]
    [Tooltip("Уровень верстака. Ограничивает макс. тир модулей.")]
    [Range(1, 10)] public int workbenchTier = 1;

    [Header("Workbench Dimensions (Microwave)")]
    public float innerLength = 2f;
    public float innerWidth = 2f;
    public float innerHeight = 2f;

    [Header("Storage References")]
    public AlloyStorage alloyStorage;
    public ResourcesStorage resourcesStorage;
    public ModuleStorage moduleStorage;

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
    private string warningMessage = "";
    private string successMessage = "";
    private float messageTimer;

    private bool spawnInWorld = true;
    private bool saveToStorage = true;

    private Dictionary<string, int> _pendingSelections = new Dictionary<string, int>();
    private GameObject craftedInstance;

    protected abstract string ModuleTypeName { get; }
    protected abstract void RebuildReferenceList();
    protected abstract string[] GetReferenceNames();
    protected abstract int GetSelectedReferenceIndex();
    protected abstract void SelectReference(int index);
    protected abstract int GetReferenceCount();

    protected abstract string GetReferenceBlueprintID();
    protected abstract bool TryFindAndSelectReference(string faction, string blueprintId);

    protected abstract int GetReferenceTier();
    protected abstract string GetReferenceFaction();
    protected abstract float GetReferenceFillPercent();

    // Возвращено для совместимости контракта и будущего использования.
    protected virtual float GetReferenceVolumeCoeffPercent() => 100f;

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

        if (messageTimer > 0f)
        {
            messageTimer -= Time.deltaTime;
            if (messageTimer <= 0f)
            {
                errorMessage = "";
                warningMessage = "";
                successMessage = "";
            }
        }
    }
    private static bool TryParseInvariant(string value, out float result)
    {
        return float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out result);
    }

    private static bool TryParseMassPart(string massPart, out float massKg)
    {
        massKg = 0f;
        if (string.IsNullOrEmpty(massPart) || massPart[0] != 'm')
            return false;

        return TryParseInvariant(massPart.Substring(1), out massKg) && massKg > 0f;
    }

    private bool TryApplyShellPercentFromMass(float targetMassKg)
    {
        // Формула:
        // total = realVol*1000*(fill + shell*(1-fill))
        // shell = (total/(realVol*1000) - fill) / (1-fill)
        float realVol = scaler.CalcRealVolume;
        if (realVol <= 0.000001f) return false;

        float fill = Mathf.Clamp01(GetReferenceFillPercent() / 100f);
        float denom = 1f - fill;

        float shellFrac;
        if (denom <= 0.000001f)
        {
            // fill=100% => масса почти не зависит от shell%; оставляем текущее значение
            shellFrac = Mathf.Clamp01(shellPercent / 100f);
        }
        else
        {
            float normalized = targetMassKg / (realVol * 1000f);
            shellFrac = (normalized - fill) / denom;
        }

        if (float.IsNaN(shellFrac) || float.IsInfinity(shellFrac))
            return false;

        // допустимый диапазон интерфейса: 0.001..100%
        shellFrac = Mathf.Clamp(shellFrac, 0.00001f, 1f);
        float newShellPercent = Mathf.Clamp(shellFrac * 100f, 0.001f, 100f);

        shellPercent = (float)Math.Round(newShellPercent, 3);
        shellPercentStr = shellPercent.ToString("F3", CultureInfo.InvariantCulture);
        scaler.SetShellPercent(shellPercent);

        RecalculateAll();

        // Проверяем, что реально попали в массу с небольшой погрешностью
        return Mathf.Abs(scaler.CalcTotalMass - targetMassKg) <= 0.2f;
    }

    private static string NormalizeCodeText(string text)
    {
        return (text ?? string.Empty).Trim().Replace("\r", "");
    }

    private static bool CompareFirstTwoLines(string a, string b)
    {
        string[] la = a.Split('\n');
        string[] lb = b.Split('\n');
        if (la.Length < 2 || lb.Length < 2) return false;
        return la[0].Trim() == lb[0].Trim() && la[1].Trim() == lb[1].Trim();
    }
    private static Texture2D _bgTex, _panelTex, _sepTex;
    private static GUIStyle _windowStyle, _panelStyle;

    private void InitStyles()
    {
        if (_bgTex == null) _bgTex = WorkbenchPopup.MakeTex(1, 1, new Color(0.15f, 0.15f, 0.15f, 0.98f));
        if (_panelTex == null) _panelTex = WorkbenchPopup.MakeTex(1, 1, new Color(0.2f, 0.2f, 0.2f, 0.9f));
        if (_sepTex == null) _sepTex = WorkbenchPopup.MakeTex(1, 1, new Color(0.35f, 0.35f, 0.35f, 0.5f));

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

    private void DrawSeparator()
    {
        GUILayout.Space(5);
        GUILayout.Box(
            GUIContent.none,
            new GUIStyle { normal = { background = _sepTex } },
            GUILayout.Height(2),
            GUILayout.ExpandWidth(true));
        GUILayout.Space(5);
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
                Mathf.Min(1100f, Screen.width * 0.96f),
                Screen.height * 0.9f);
            windowRectInitialized = true;
        }

        windowRect.x = Mathf.Clamp(windowRect.x, 0, Mathf.Max(0, Screen.width - windowRect.width));
        windowRect.y = Mathf.Clamp(windowRect.y, 0, Mathf.Max(0, Screen.height - windowRect.height));

        if (WorkbenchPopup.IsShowing &&
            Event.current.type == EventType.MouseDown &&
            !WorkbenchPopup.PopupRect.Contains(Event.current.mousePosition))
        {
            WorkbenchPopup.Hide();
            Event.current.Use();
        }

        windowRect = GUI.Window(GetInstanceID(), windowRect, DrawWindow, $"{ModuleTypeName}Workbench", _windowStyle);
        WorkbenchPopup.DrawPopup();
    }

    private void DrawWindow(int id)
    {
        GUI.DragWindow(new Rect(0, 0, 10000, 20));

        GUILayout.BeginArea(new Rect(20, 35, windowRect.width - 40, windowRect.height - 45));
        scrollPos = GUILayout.BeginScrollView(scrollPos, false, true);
        GUILayout.BeginVertical();

        if (!string.IsNullOrEmpty(errorMessage))
            GUILayout.Label($"<color=#FF4444><b>⚠ ОШИБКА: {errorMessage}</b></color>", GetCenteredBoldStyle());

        if (!string.IsNullOrEmpty(warningMessage))
            GUILayout.Label($"<color=#FFCC00><b>⚠ ПРЕДУПРЕЖДЕНИЕ: {warningMessage}</b></color>", GetCenteredBoldStyle());

        if (!string.IsNullOrEmpty(successMessage))
            GUILayout.Label($"<color=#00FF66><b>✓ {successMessage}</b></color>", GetCenteredBoldStyle());

        GUILayout.Label(
            $"<color=#AAAAAA>Параметры Верстака:</color> Тир {workbenchTier} | Вместимость камеры: {innerLength}×{innerHeight}×{innerWidth} м ({(innerLength * innerWidth * innerHeight):F1} м³)",
            new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter });

        GUILayout.BeginHorizontal();
        GUILayout.BeginVertical(GUILayout.Width((windowRect.width - 40) * 0.75f));
        DrawCompactCodeSection("ГЕНЕРАЦИЯ КОДА", ref currentModuleCode, true, "КОПИРОВАТЬ", () =>
        {
            if (!string.IsNullOrEmpty(currentModuleCode)) GUIUtility.systemCopyBuffer = currentModuleCode;
        });
        GUILayout.Space(5);
        DrawCompactCodeSection("ВВОД ЧЕРТЕЖА", ref codeInputField, false, "ВСТАВИТЬ", () =>
        {
            codeInputField = (GUIUtility.systemCopyBuffer ?? "").Trim();
        }, "ПРИМЕНИТЬ", ApplyCodeFromInput);
        GUILayout.EndVertical();
        GUILayout.FlexibleSpace();
        GUILayout.EndHorizontal();

        DrawSeparator();

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
        DrawModuleSpecificSection();

        DrawSeparator();
        DrawAlloyParamsSection();

        DrawSeparator();

        GUILayout.BeginHorizontal();
        GUILayout.BeginVertical(GUILayout.Width((windowRect.width - 40) * 0.75f));
        DrawCostsAndButtons();
        GUILayout.EndVertical();
        GUILayout.FlexibleSpace();
        GUILayout.EndHorizontal();

        GUILayout.EndVertical();
        GUILayout.EndScrollView();
        GUILayout.EndArea();
    }

    private void DrawCompactCodeSection(string title, ref string text, bool readOnly, string btn1, Action act1, string btn2 = null, Action act2 = null)
    {
        GUILayout.BeginVertical(_panelStyle);
        GUILayout.Label($"<color=#E0E0E0>{title}</color>", GetBoldStyle());

        GUILayout.BeginHorizontal();
        GUIStyle st = new GUIStyle(GUI.skin.textArea)
        {
            fontSize = 13,
            normal =
            {
                textColor = readOnly ? new Color(0.8f, 0.9f, 0.8f) : Color.white,
                background = WorkbenchPopup.MakeTex(1, 1, new Color(0.1f, 0.1f, 0.1f, 1f))
            }
        };

        if (readOnly) GUI.enabled = false;
        text = GUILayout.TextArea(text, st, GUILayout.Height(55));
        if (readOnly) GUI.enabled = true;

        GUILayout.BeginVertical(GUILayout.Width(110));
        if (GUILayout.Button(btn1, GUILayout.Height(25))) act1?.Invoke();
        if (btn2 != null && GUILayout.Button(btn2, GUILayout.Height(25))) act2?.Invoke();
        GUILayout.EndVertical();

        GUILayout.EndHorizontal();
        GUILayout.EndVertical();
    }

    private void ApplyCodeFromInput()
    {
        if (string.IsNullOrWhiteSpace(codeInputField))
            return;

        string[] lines = codeInputField.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
        if (lines.Length < 3)
        {
            ShowError("Неверный формат чертежа (нужно 3 строки)");
            return;
        }

        string[] parts = lines[0].Split('-');
        // Type-Tier-mMass-dDur-X/Y/Z-Faction-BP
        if (parts.Length < 7)
        {
            ShowError("Неверная или устаревшая первая строка");
            return;
        }

        if (parts[0] != ModuleTypeName)
        {
            ShowError($"Чертеж не от {ModuleTypeName}");
            return;
        }

        string tStr = parts[1].Replace("T", "");
        if (int.TryParse(tStr, out int reqTier) && reqTier > workbenchTier)
        {
            ShowError($"Тир чертежа (T{reqTier}) превышает уровень верстака (T{workbenchTier})!");
            return;
        }

        // Масса из mXXXX
        if (!TryParseMassPart(parts[2], out float targetMassKg))
        {
            ShowError("Неверная масса в первой строке чертежа");
            return;
        }

        string faction = parts[parts.Length - 2];
        string bpId = parts[parts.Length - 1];

        if (!TryFindAndSelectReference(faction, bpId))
        {
            ShowError($"Эталон [{faction}-{bpId}] не найден в базе данных или недоступен!");
            return;
        }

        // Габариты X/Y/Z
        string[] dims = parts[4].Split('/');
        if (dims.Length != 3)
        {
            ShowError("Неверные габариты в коде");
            return;
        }

        if (!TryParseInvariant(dims[0], out float targetL) ||
            !TryParseInvariant(dims[1], out float targetW) ||
            !TryParseInvariant(dims[2], out float targetH))
        {
            ShowError("Невозможно прочитать габариты из кода");
            return;
        }

        float refL = scaler.RefLength;
        float refW = scaler.RefWidth;
        float refH = scaler.RefHeight;

        if (refL <= 0f || refW <= 0f || refH <= 0f)
        {
            ShowError("Ошибка эталона: некорректные базовые габариты");
            return;
        }

        float sx = targetL / refL;
        float sy = targetW / refW;
        float sz = targetH / refH;

        const float scaleEps = 0.0015f;
        float minS = Mathf.Min(sx, Mathf.Min(sy, sz));
        float maxS = Mathf.Max(sx, Mathf.Max(sy, sz));

        if (minS <= 0f || (maxS - minS) > scaleEps)
        {
            ShowError("Код поврежден: масштаб по осям X/Y/Z несовместим");
            return;
        }

        // 1) применяем uniform scale из X/Y/Z
        float uniformScale = (sx + sy + sz) / 3f;
        scaler.SetScaleFactor(uniformScale);

        // 2) восстанавливаем shell% из массы кода
        if (!TryApplyShellPercentFromMass(targetMassKg))
        {
            ShowError("Код поврежден: невозможная масса для выбранного эталона");
            return;
        }

        // 3) применяем сплав
        string inputAlloyCode = lines[2].Trim();
        int newAlloyIndex = Array.IndexOf(alloyCodes, inputAlloyCode);

        bool alloyPresentOnStorage = false;
        if (newAlloyIndex >= 0)
        {
            selectedAlloyIndex = newAlloyIndex;
            OnAlloyChanged();
            alloyPresentOnStorage = true;
        }
        else
        {
            if (AlloyCode.Decode(inputAlloyCode, out AlloyCode.AlloyParams p))
            {
                alloyParams = p;
                alloyDecoded = true;
                // Пересчет с tier из кода сплава
                RecalculateAll();
            }
            else
            {
                ShowError("Неизвестный формат сплава в чертеже.");
                return;
            }
        }

        // 4) античит-проверка
        string cleanInput = NormalizeCodeText(codeInputField);
        string cleanGenerated = NormalizeCodeText(currentModuleCode);

        bool codeMatches;
        if (alloyPresentOnStorage)
        {
            // когда сплав есть на складе — сравниваем все 3 строки
            codeMatches = (cleanInput == cleanGenerated);
        }
        else
        {
            // когда сплава нет на складе — сравниваем только строки 1+2
            codeMatches = CompareFirstTwoLines(cleanInput, cleanGenerated);
        }

        if (!codeMatches)
        {
            ShowError("Код поврежден или содержит невозможные параметры!");
            return;
        }

        if (alloyPresentOnStorage)
            ShowMessage("Чертеж успешно применен!", false);
        else
            ShowMessage("Чертеж применен, но указанного сплава нет на складе!", true);
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
            if (newIdx != curIdx)
            {
                SelectReference(newIdx);
                OnReferenceChanged();
            }
        }
        else
        {
            GUILayout.Label("<color=#FF8888>(Нет эталонов в БД)</color>");
        }
        GUILayout.EndHorizontal();

        if (GetReferenceCount() > 0)
        {
            string faction = string.IsNullOrEmpty(GetReferenceFaction()) ? "—" : GetReferenceFaction();
            string bp = GetReferenceBlueprintID();
            GUILayout.Label($"<color=#AAAAAA>Тир:</color> {GetReferenceTier()}  |  <color=#AAAAAA>ID:</color> {faction}-{bp}  |  <color=#AAAAAA>VolCoeff:</color> {GetReferenceVolumeCoeffPercent():F1}%");
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
            if (newIdx != selectedAlloyIndex)
            {
                selectedAlloyIndex = newIdx;
                OnAlloyChanged();
            }
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
        if (GUILayout.Button("СБРОСИТЬ МАСШТАБ", GUILayout.Height(24)))
        {
            scaler.SetScaleFactor(1f);
            RecalculateAll();
        }

        GUILayout.EndVertical();
    }

    private bool CheckFitsInWorkbench()
    {
        return scaler.CalcLength <= innerLength &&
               scaler.CalcWidth <= innerWidth &&
               scaler.CalcHeight <= innerHeight;
    }

    private bool CheckTierConstraints()
    {
        return GetReferenceTier() <= workbenchTier;
    }

    private void DrawComputedSection()
    {
        GUILayout.BeginVertical(_panelStyle);
        GUILayout.Label("<color=#E0E0E0>ГЕОМЕТРИЯ И ФИЗИКА</color>", GetBoldStyle());

        bool fits = CheckFitsInWorkbench();
        string dimColor = fits ? "#00FF00" : "#FF4444";

        DrawGridRow("Длина (X):", $"<color={dimColor}>{scaler.CalcLength:F3} м</color>", "Объём Real:", $"{scaler.CalcRealVolume:F4} м³");
        DrawGridRow("Ширина (Z):", $"<color={dimColor}>{scaler.CalcWidth:F3} м</color>", "Объём оболочки:", $"{scaler.CalcShellVolume:F4} м³");
        DrawGridRow("Высота (Y):", $"<color={dimColor}>{scaler.CalcHeight:F3} м</color>", "Эфф. внутр. объём:", $"{scaler.CalcEffectiveVolume:F4} м³");

        DrawSeparator();

        DrawGridRow("Масса оболочки:", $"{scaler.CalcShellMass:F1} кг", "Прочность:", $"<color=#FFD700>{scaler.CalcDurability:F1}</color>");
        DrawGridRow("Масса внутр. объема:", $"{scaler.CalcInnerMass:F1} кг", "", "");
        DrawGridRow("ОБЩАЯ МАССА:", $"<b>{scaler.CalcTotalMass:F1} кг</b>", "", "");

        if (!fits)
        {
            GUILayout.Space(10);
            GUILayout.Label(
                $"<color=#FF4444><b>⚠ ГАБАРИТЫ ПРЕВЫШАЮТ КАМЕРУ ВЕРСТАКА (Макс: {innerLength}x{innerWidth}x{innerHeight})</b></color>",
                GetCenteredBoldStyle());
        }

        if (!CheckTierConstraints())
        {
            GUILayout.Space(5);
            GUILayout.Label(
                $"<color=#FF4444><b>⚠ ТИР ЭТАЛОНА (T{GetReferenceTier()}) ВЫШЕ ТИРА ВЕРСТАКА (T{workbenchTier})</b></color>",
                GetCenteredBoldStyle());
        }

        GUILayout.EndVertical();
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

    private void DrawAlloyParamsSection()
    {
        GUILayout.BeginVertical(_panelStyle);
        GUILayout.Label("<color=#E0E0E0>АНАЛИЗ ОБОЛОЧКИ</color>", GetBoldStyle());

        if (!alloyDecoded)
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
        DrawAlloyCol("KINETIC", alloyParams.kineticAbsorption, alloyParams.kineticResistance, colW);
        DrawAlloyCol("THERMAL", alloyParams.thermalAbsorption, alloyParams.thermalResistance, colW);
        DrawAlloyCol("CHEMICAL", alloyParams.chemicalAbsorption, alloyParams.chemicalResistance, colW);
        DrawAlloyCol("ENERGY", alloyParams.energyAbsorption, alloyParams.energyResistance, colW);
        GUILayout.EndHorizontal();

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
        GUILayout.Label("Размещение готового модуля: ", GUILayout.Width(200));
        spawnInWorld = GUILayout.Toggle(spawnInWorld, "В сцену (Мир)", GUILayout.Width(120));
        saveToStorage = GUILayout.Toggle(saveToStorage, "На склад (Storage)");
        GUILayout.EndHorizontal();

        GUILayout.Space(10);

        GUILayout.BeginHorizontal();
        bool canCraft = GetReferenceCount() > 0 &&
                        alloyCode != null &&
                        enoughAlloy &&
                        enoughMetal &&
                        enoughEnergy &&
                        scaler.CalcEffectiveVolume > 0.000001f &&
                        CheckFitsInWorkbench() &&
                        CheckTierConstraints();

        Color oldBg = GUI.backgroundColor;
        GUI.backgroundColor = canCraft ? new Color(0.2f, 0.6f, 0.3f) : new Color(0.4f, 0.2f, 0.2f);

        GUI.enabled = canCraft;
        if (GUILayout.Button("◆ ИЗГОТОВИТЬ МОДУЛЬ ◆", GUILayout.Height(35)))
            OnCraft();
        GUI.enabled = true;

        GUI.backgroundColor = oldBg;

        GUILayout.Space(10);
        if (GUILayout.Button("СБРОС", GUILayout.Height(35), GUILayout.Width(100)))
            ResetToDefaults();

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
        if (GetReferenceCount() == 0 || !CheckFitsInWorkbench() || !CheckTierConstraints())
            return;

        string alloyCode = GetSelectedAlloyCode();
        if (string.IsNullOrEmpty(alloyCode))
        {
            ShowError("Сплав не выбран");
            return;
        }

        if (!spawnInWorld && !saveToStorage)
        {
            ShowError("Выберите место размещения модуля!");
            return;
        }

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

        ModuleData moduleData = CreateSpecificModuleData();
        if (moduleData == null)
        {
            ShowError("Ошибка создания данных модуля");
            return;
        }

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

        if (spawnInWorld)
        {
            if (craftedInstance != null)
            {
                Destroy(craftedInstance);
                craftedInstance = null;
            }

            GameObject prefab = GetReferencePrefab();
            if (prefab != null)
            {
                Vector3 spawnPos = transform.position + Vector3.up * 2f;
                craftedInstance = Instantiate(prefab, spawnPos, Quaternion.identity);
                craftedInstance.name = $"Crafted_{prefab.name}_T{GetReferenceTier()}";
                craftedInstance.transform.localScale = prefab.transform.localScale * Mathf.Max(0.001f, scaler.CurrentScaleFactor);

                var oldGen = craftedInstance.GetComponent<StandardGenerator>();
                if (oldGen != null) Destroy(oldGen);

                var craftedComp = craftedInstance.AddComponent<CraftedModule>();
                craftedComp.SetData(moduleData);
            }
        }

        if (saveToStorage && moduleStorage != null)
            moduleStorage.AddModule(moduleData);

        ShowMessage("Модуль успешно изготовлен!", false);
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
        string bp = GetReferenceBlueprintID();
        string alloyCode = GetSelectedAlloyCode() ?? "NONE";
        string specific = GetSpecificCodeSegment();

        string line1 = $"{ModuleTypeName}-T{tier}-m{scaler.CalcTotalMass.ToString("F1", CultureInfo.InvariantCulture)}-d{scaler.CalcDurability.ToString("F3", CultureInfo.InvariantCulture)}-{scaler.CalcLength.ToString("F3", CultureInfo.InvariantCulture)}/{scaler.CalcWidth.ToString("F3", CultureInfo.InvariantCulture)}/{scaler.CalcHeight.ToString("F3", CultureInfo.InvariantCulture)}-{faction}-{bp}";
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

    private void ResetToDefaults()
    {
        shellPercent = 5f;
        shellPercentStr = "5.000";
        selectedAlloyIndex = 0;

        codeInputField = "";
        errorMessage = "";
        warningMessage = "";
        successMessage = "";

        _pendingSelections.Clear();
        WorkbenchPopup.Hide();

        scaler.SetScaleFactor(1f);
        scaler.SetShellPercent(5f);
        scaler.SetScaleMode(ModuleScaler.ScaleMode.Mass);

        RebuildAllLists();
        RecalculateAll();
    }

    protected void ShowError(string msg)
    {
        errorMessage = msg;
        warningMessage = "";
        successMessage = "";
        messageTimer = 4f;
    }

    protected void ShowMessage(string msg, bool isWarning)
    {
        if (isWarning)
        {
            warningMessage = msg;
            errorMessage = "";
            successMessage = "";
        }
        else
        {
            successMessage = msg;
            errorMessage = "";
            warningMessage = "";
        }

        messageTimer = 4f;
    }

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
        if (_centeredBold == null)
            _centeredBold = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontStyle = FontStyle.Bold,
                fontSize = 15
            };
        return _centeredBold;
    }

    private GUIStyle _boldStyle;
    protected GUIStyle GetBoldStyle()
    {
        if (_boldStyle == null)
            _boldStyle = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold };
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
    private static readonly int _windowId = 987655;
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
        float h = Mathf.Min((options != null ? options.Length : 0) * itemHeight + 10f, 400f);
        float w = 350f;

        float x = Mathf.Clamp(screenPos.x, 5f, Mathf.Max(5f, Screen.width - w - 5f));
        float y = Mathf.Clamp(screenPos.y, 5f, Mathf.Max(5f, Screen.height - h - 5f));
        _popupRect = new Rect(x, y, w, h);
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
            bool selected = (i == _current);
            if (GUILayout.Button(_options[i], selected ? GetSelectedStyle() : GetNormalStyle(), GUILayout.Height(24)))
            {
                if (!canInteract) continue;
                _callback?.Invoke(i);
                Hide();
                GUIUtility.ExitGUI();
                return;
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
                normal = { textColor = Color.white },
                hover =
                {
                    textColor = Color.white,
                    background = MakeTex(1, 1, new Color(0.3f, 0.3f, 0.3f, 0.8f))
                }
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
                normal =
                {
                    textColor = Color.white,
                    background = MakeTex(1, 1, new Color(0.25f, 0.25f, 0.25f, 0.9f))
                }
            };
        }
        return _selectedStyle;
    }

    public static Texture2D MakeTex(int w, int h, Color col)
    {
        var pix = new Color[w * h];
        for (int i = 0; i < pix.Length; i++) pix[i] = col;

        var tex = new Texture2D(w, h);
        tex.SetPixels(pix);
        tex.Apply();
        return tex;
    }
}