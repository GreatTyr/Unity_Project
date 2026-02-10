using System;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// UI плавильни на IMGUI. Вешается на FurnacePanel (или любой GameObject).
/// Печь назначается динамически через OpenForFurnace().
/// Путь: Assets/Scripts/CoreMechanics/FurnaceUI.cs
/// </summary>
public class FurnaceUI : MonoBehaviour
{
    private FurnaceCore currentFurnace;
    private bool panelOpen;
    private Rect windowRect = new Rect(50, 50, 940, 660);

    // Состояние
    private int currentMetalTier = 1;
    private long currentMetalGrams;
    private bool currentUseChemicals;
    private bool currentUseNanites;
    private long currentChemicalsGrams;
    private long currentNanitesGrams;
    private bool maxAmountOn = true;

    private int stateKinAbsorb;
    private float stateKinResist;
    private int stateThermAbsorb;
    private float stateThermResist;
    private int stateChemAbsorb;
    private float stateChemResist;
    private int stateEnergyAbsorb;
    private float stateEnergyResist;

    // Код для вставки
    private string codeInputField = "";

    // Сообщение об ошибке
    private string errorMessage = "";
    private float errorTimer;

    // Строковые буферы для полей ввода (чтобы не мерцали при редактировании)
    private string metalAmountStr = "";
    private string kinAbsorbStr = "0";
    private string thermAbsorbStr = "0";
    private string chemAbsorbStr = "0";
    private string energyAbsorbStr = "0";
    private string kinResistStr = "0.0";
    private string thermResistStr = "0.0";
    private string chemResistStr = "0.0";
    private string energyResistStr = "0.0";

    // ═══════════════ ОТКРЫТИЕ / ЗАКРЫТИЕ ═══════════════

    public void OpenForFurnace(FurnaceCore target)
    {
        currentFurnace = target;
        panelOpen = true;
        ResetToDefaults();
    }

    public void ClosePanel()
    {
        panelOpen = false;
        currentFurnace = null;
    }

    // ═══════════════ LIFECYCLE ═══════════════

    private void Update()
    {
        if (panelOpen && Keyboard.current != null)
        {
            if (Keyboard.current.oKey.wasPressedThisFrame ||
                Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                ClosePanel();
            }
        }

        if (errorTimer > 0f)
        {
            errorTimer -= Time.deltaTime;
            if (errorTimer <= 0f) errorMessage = "";
        }
    }

    private void OnGUI()
    {
        if (!panelOpen || currentFurnace == null) return;
        windowRect = GUI.Window(198765, windowRect, DrawWindow, "Плавильня");
    }

    // ═══════════════ ОСНОВНОЕ ОКНО ═══════════════

    private void DrawWindow(int id)
    {
        GUI.DragWindow(new Rect(0, 0, 10000, 20));
        GUILayout.BeginVertical();

        // ─── Ошибка ───
        if (!string.IsNullOrEmpty(errorMessage))
        {
            Color prev = GUI.color;
            GUI.color = Color.red;
            GUILayout.Label(errorMessage, GetCenteredBoldStyle());
            GUI.color = prev;
            GUILayout.Space(2);
        }

        // ─── Строка вставки кода ───
        GUILayout.BeginHorizontal();
        GUILayout.Label("Код для вставки:", GUILayout.Width(110));
        codeInputField = GUILayout.TextField(codeInputField, GUILayout.Width(windowRect.width - 380));
        if (GUILayout.Button("Вставить", GUILayout.Width(80)))
        {
            codeInputField = (GUIUtility.systemCopyBuffer ?? "").Trim();
        }
        if (GUILayout.Button("Применить код", GUILayout.Width(120)))
        {
            ApplyCodeFromField();
        }
        GUILayout.EndHorizontal();

        GUILayout.Space(4);

        // ─── Верхняя часть: два столбца ───
        float halfW = (windowRect.width - 20) * 0.5f;

        GUILayout.BeginHorizontal();

        // Левый столбец: параметры печи
        GUILayout.BeginVertical(GUILayout.Width(halfW));

        GUILayout.BeginHorizontal();
        GUILayout.Label("Ёмкость плавильни (кг):", GUILayout.Width(200));
        GUILayout.Label(currentFurnace.CapacityKg.ToString("F3"), GUILayout.Width(140));
        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();
        GUILayout.Label("Тир плавильни:", GUILayout.Width(200));
        GUILayout.Label(currentFurnace.FurnaceTier.ToString(), GUILayout.Width(40));
        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();
        GUILayout.Label("Эффективность плавильщика:", GUILayout.Width(200));
        GUILayout.Label(currentFurnace.EfficiencyPercent.ToString("F0") + "%", GUILayout.Width(140));
        GUILayout.EndHorizontal();

        GUILayout.EndVertical();

        // Правый столбец: параметры плавки
        GUILayout.BeginVertical(GUILayout.Width(halfW));

        // Количество металла
        GUILayout.BeginHorizontal();
        GUILayout.Label("Кол-во металла (кг):", GUILayout.Width(200));
        string newMetalStr = GUILayout.TextField(metalAmountStr, GUILayout.Width(100));
        if (newMetalStr != metalAmountStr)
        {
            metalAmountStr = newMetalStr;
            maxAmountOn = false;
            if (double.TryParse(metalAmountStr, out double kg))
            {
                kg = Math.Max(0.0, kg);
                long g = (long)Math.Round(kg * 1000.0);
                long maxG = CalcMaxMetalGrams();
                if (g > maxG) g = maxG;
                long onStorage = currentFurnace.GetMetalOnStorageGrams(currentMetalTier);
                if (g > onStorage) g = onStorage;
                currentMetalGrams = g;
                RecalcChemNan();
            }
        }
        bool newMax = GUILayout.Toggle(maxAmountOn, "Максимум", GUILayout.Width(100));
        if (newMax != maxAmountOn)
        {
            maxAmountOn = newMax;
            if (maxAmountOn) RecalcAllAmounts();
        }
        GUILayout.EndHorizontal();

        // Тир металла
        GUILayout.BeginHorizontal();
        GUILayout.Label("Тир металла:", GUILayout.Width(200));
        int newMetalTier = Mathf.Clamp(
            Mathf.RoundToInt(GUILayout.HorizontalSlider(currentMetalTier, 1, currentFurnace.FurnaceTier, GUILayout.Width(120))),
            1, currentFurnace.FurnaceTier);
        if (newMetalTier != currentMetalTier)
        {
            currentMetalTier = newMetalTier;
            float maxRes = Smelting.MaxResistance(currentMetalTier);
            if (stateKinResist > maxRes) stateKinResist = maxRes;
            if (stateThermResist > maxRes) stateThermResist = maxRes;
            if (stateChemResist > maxRes) stateChemResist = maxRes;
            if (stateEnergyResist > maxRes) stateEnergyResist = maxRes;
            RecalcAllAmounts();
        }
        GUILayout.Label(currentMetalTier.ToString(), GUILayout.Width(40));
        GUILayout.EndHorizontal();

        // Химикаты
        GUILayout.BeginHorizontal();
        bool newChem = GUILayout.Toggle(currentUseChemicals, "Использовать химикаты (20% от металла)", GUILayout.Width(300));
        if (newChem != currentUseChemicals)
        {
            currentUseChemicals = newChem;
            if (!currentUseChemicals)
            {
                stateChemAbsorb = 0; stateChemResist = 0f;
                stateEnergyAbsorb = 0; stateEnergyResist = 0f;
                currentChemicalsGrams = 0;
            }
            RecalcAllAmounts();
        }
        if (currentUseChemicals)
        {
            double chemKg = currentChemicalsGrams / 1000.0;
            long onStorage = currentFurnace.GetChemicalsOnStorageGrams(currentMetalTier);
            if (onStorage < currentChemicalsGrams)
            {
                Color prev = GUI.color; GUI.color = Color.red;
                GUILayout.Label(string.Format("{0:F3} кг (не хватает {1:F3})", chemKg, (currentChemicalsGrams - onStorage) / 1000.0));
                GUI.color = prev;
            }
            else
            {
                GUILayout.Label(string.Format("{0:F3} кг", chemKg));
            }
        }
        GUILayout.EndHorizontal();

        // Наниты
        GUILayout.BeginHorizontal();
        bool newNan = GUILayout.Toggle(currentUseNanites, "Использовать наниты (10% от металла)", GUILayout.Width(300));
        if (newNan != currentUseNanites)
        {
            currentUseNanites = newNan;
            if (!currentUseNanites)
            {
                if (stateKinResist < 0f) stateKinResist = 0f;
                if (stateThermResist < 0f) stateThermResist = 0f;
                if (stateChemResist < 0f) stateChemResist = 0f;
                if (stateEnergyResist < 0f) stateEnergyResist = 0f;
                currentNanitesGrams = 0;
            }
            RecalcAllAmounts();
        }
        if (currentUseNanites)
        {
            double nanKg = currentNanitesGrams / 1000.0;
            long onStorage = currentFurnace.GetNanitesOnStorageGrams(currentMetalTier);
            if (onStorage < currentNanitesGrams)
            {
                Color prev = GUI.color; GUI.color = Color.red;
                GUILayout.Label(string.Format("{0:F3} кг (не хватает {1:F3})", nanKg, (currentNanitesGrams - onStorage) / 1000.0));
                GUI.color = prev;
            }
            else
            {
                GUILayout.Label(string.Format("{0:F3} кг", nanKg));
            }
        }
        GUILayout.EndHorizontal();

        GUILayout.EndVertical();
        GUILayout.EndHorizontal();

        GUILayout.Space(6);

        // ─── Очки ───
        int baseP = Smelting.BasePoints(currentMetalTier);
        int freeP = CalcFreePoints();
        GUILayout.BeginHorizontal();
        GUILayout.Label(string.Format("Базовые очки: {0}", baseP), GUILayout.Width(halfW));
        GUILayout.Label(string.Format("Свободные очки: {0}", freeP), GUILayout.Width(halfW));
        GUILayout.EndHorizontal();

        GUILayout.Space(6);

        // ─── Поглощения и сопротивления: два столбца ───
        float colWidth = (windowRect.width - 30) * 0.5f;
        GUILayout.BeginHorizontal();

        // Левый: кинетика + термика
        GUILayout.BeginVertical("box", GUILayout.Width(colWidth));
        DrawAbsorbRow("Поглощение кинетического урона:", ref stateKinAbsorb, ref kinAbsorbStr, freeP, true);
        DrawResistRow("Сопротивление кинетическому урону (%):", ref stateKinResist, ref kinResistStr, 0, freeP, true);
        GUILayout.Space(4);
        DrawAbsorbRow("Поглощение термического урона:", ref stateThermAbsorb, ref thermAbsorbStr, freeP, true);
        DrawResistRow("Сопротивление термическому урону (%):", ref stateThermResist, ref thermResistStr, 1, freeP, true);
        GUILayout.EndVertical();

        // Правый: химия + энергия
        GUILayout.BeginVertical("box", GUILayout.Width(colWidth));
        DrawAbsorbRow("Поглощение химического урона:", ref stateChemAbsorb, ref chemAbsorbStr, freeP, currentUseChemicals);
        DrawResistRow("Сопротивление химическому урону (%):", ref stateChemResist, ref chemResistStr, 2, freeP, currentUseChemicals);
        GUILayout.Space(4);
        DrawAbsorbRow("Поглощение энергетического урона:", ref stateEnergyAbsorb, ref energyAbsorbStr, freeP, currentUseChemicals);
        DrawResistRow("Сопротивление энергетическому урону (%):", ref stateEnergyResist, ref energyResistStr, 3, freeP, currentUseChemicals);
        GUILayout.EndVertical();

        GUILayout.EndHorizontal();

        GUILayout.Space(8);

        // ─── Нижняя информация ───
        long energy = Smelting.EnergyCost(currentFurnace.CapacityGrams, currentFurnace.FurnaceTier,
            currentMetalGrams, currentChemicalsGrams, currentNanitesGrams, currentMetalTier);
        long outG = Smelting.OutputAlloyGrams(currentMetalGrams, currentChemicalsGrams, currentNanitesGrams,
            currentFurnace.FurnaceTier, currentMetalTier);
        string code = BuildCurrentCode();

        GUILayout.BeginHorizontal("box");

        GUILayout.BeginVertical(GUILayout.Width(windowRect.width * 0.6f - 10));
        GUILayout.BeginHorizontal();
        GUILayout.Label(string.Format("Код сплава: {0}", code), GUILayout.Width(windowRect.width * 0.6f - 140));
        if (GUILayout.Button("Копировать код", GUILayout.Width(120)))
        {
            GUIUtility.systemCopyBuffer = code;
        }
        GUILayout.EndHorizontal();

        GUILayout.Label(string.Format("Получаемый сплав: {0:F3} кг", outG / 1000.0));
        GUILayout.Label(string.Format("Затраты металла: {0:F3} кг (T{1})", currentMetalGrams / 1000.0, currentMetalTier));
        GUILayout.Label(string.Format("Затраты химикатов: {0:F3} кг (T{1})", currentChemicalsGrams / 1000.0, currentMetalTier));
        GUILayout.Label(string.Format("Затраты нанитов: {0:F3} кг (T{1})", currentNanitesGrams / 1000.0, currentMetalTier));
        GUILayout.Label(string.Format("Затраты энергии: {0}", energy));
        GUILayout.EndVertical();

        GUILayout.BeginVertical(GUILayout.Width(windowRect.width * 0.4f - 10));
        GUILayout.FlexibleSpace();

        bool canCraft = currentMetalGrams > 0 &&
            currentFurnace.HasEnoughResources(currentMetalGrams, currentMetalTier,
                currentChemicalsGrams, currentNanitesGrams, energy);

        if (!canCraft) GUI.enabled = false;
        if (GUILayout.Button("Изготовить", GUILayout.Height(30), GUILayout.Width(160)))
        {
            OnCraft();
        }
        GUI.enabled = true;

        GUILayout.Space(10);

        if (GUILayout.Button("Сброс", GUILayout.Height(30), GUILayout.Width(160)))
        {
            ResetToDefaults();
        }

        GUILayout.FlexibleSpace();
        GUILayout.EndVertical();

        GUILayout.EndHorizontal();

        GUILayout.EndVertical();

        // Обновить строковые буферы
        SyncStringBuffers();
    }

    // ═══════════════ СТРОКИ ПОГЛОЩЕНИЙ И СОПРОТИВЛЕНИЙ ═══════════════

    private void DrawAbsorbRow(string label, ref int absorb, ref string absStr, int freePoints, bool enabled)
    {
        GUILayout.BeginHorizontal();
        GUILayout.Label(label, GUILayout.Width(220));

        int mul = GetMultiplier();

        bool minusOk = enabled && absorb > 0;
        if (!minusOk) GUI.enabled = false;
        if (GUILayout.Button("-", GUILayout.Width(22)) && minusOk)
        {
            absorb = Math.Max(0, absorb - mul);
        }
        GUI.enabled = true;

        if (!enabled) GUI.enabled = false;
        string newStr = GUILayout.TextField(absStr, GUILayout.Width(56));
        if (enabled && newStr != absStr)
        {
            absStr = newStr;
            if (int.TryParse(absStr, out int val))
            {
                val = Math.Max(0, val);
                int old = absorb;
                absorb = 0;
                int fp = CalcFreePoints();
                absorb = Math.Min(val, old + fp);
            }
        }
        GUI.enabled = true;

        bool plusOk = enabled && freePoints > 0;
        if (!plusOk) GUI.enabled = false;
        if (GUILayout.Button("+", GUILayout.Width(22)) && plusOk)
        {
            int add = Math.Min(mul, CalcFreePoints());
            absorb += add;
        }
        GUI.enabled = true;

        GUILayout.EndHorizontal();
    }

    private void DrawResistRow(string label, ref float resist, ref string resStr, int typeIndex, int freePoints, bool enabled)
    {
        GUILayout.BeginHorizontal();
        GUILayout.Label(label, GUILayout.Width(220));

        float minRes = CalcMinResist(typeIndex);
        float maxRes = Smelting.MaxResistance(currentMetalTier);
        int mul = GetMultiplier();

        if (!enabled) GUI.enabled = false;

        // Слайдер
        float sliderVal = GUILayout.HorizontalSlider(resist, minRes, maxRes, GUILayout.Width(120));
        if (enabled && Math.Abs(sliderVal - resist) > 0.01f)
        {
            // Округлить до 0.1
            float rounded = (float)Math.Round(sliderVal * 10f) / 10f;
            rounded = Mathf.Clamp(rounded, minRes, maxRes);
            float oldVal = resist;
            resist = rounded;
            if (CalcFreePoints() < 0) resist = oldVal;
        }

        // Текстовое поле
        string newResStr = GUILayout.TextField(resStr, GUILayout.Width(56));
        if (enabled && newResStr != resStr)
        {
            resStr = newResStr;
            if (float.TryParse(resStr, out float val))
            {
                val = Mathf.Clamp(val, minRes, maxRes);
                float oldVal = resist;
                resist = val;
                if (CalcFreePoints() < 0) resist = oldVal;
            }
        }

        GUI.enabled = true;
        GUILayout.EndHorizontal();
    }

    // ═══════════════ МНОЖИТЕЛЬ ═══════════════

    private int GetMultiplier()
    {
        if (Keyboard.current == null) return 1;
        if (Keyboard.current.leftShiftKey.isPressed || Keyboard.current.rightShiftKey.isPressed) return 100;
        if (Keyboard.current.leftCtrlKey.isPressed || Keyboard.current.rightCtrlKey.isPressed) return 10;
        return 1;
    }

    // ═══════════════ СБРОС ═══════════════

    private void ResetToDefaults()
    {
        currentMetalTier = 1;
        currentUseChemicals = false;
        currentUseNanites = false;
        currentChemicalsGrams = 0;
        currentNanitesGrams = 0;
        maxAmountOn = true;

        stateKinAbsorb = 0; stateKinResist = 0f;
        stateThermAbsorb = 0; stateThermResist = 0f;
        stateChemAbsorb = 0; stateChemResist = 0f;
        stateEnergyAbsorb = 0; stateEnergyResist = 0f;

        codeInputField = "";
        errorMessage = "";

        RecalcAllAmounts();
        SyncStringBuffers();
    }

    // ═══════════════ ПЕРЕСЧЁТЫ ═══════════════

    private void RecalcAllAmounts()
    {
        if (maxAmountOn)
        {
            long maxG = CalcMaxMetalGrams();
            long onStorage = currentFurnace.GetMetalOnStorageGrams(currentMetalTier);
            if (maxG > onStorage) maxG = onStorage;
            currentMetalGrams = maxG;
        }
        RecalcChemNan();
    }

    private void RecalcChemNan()
    {
        currentChemicalsGrams = currentUseChemicals ? Smelting.ChemicalsGrams(currentMetalGrams) : 0;
        currentNanitesGrams = currentUseNanites ? Smelting.NanitesGrams(currentMetalGrams) : 0;
    }

    private long CalcMaxMetalGrams()
    {
        if (currentFurnace == null) return 0;
        return Smelting.MaxMetalGrams(currentFurnace.CapacityGrams, currentUseChemicals, currentUseNanites);
    }

    private int CalcFreePoints()
    {
        return Smelting.CalculateFreePoints(currentMetalTier,
            stateKinAbsorb, stateKinResist, stateThermAbsorb, stateThermResist,
            stateChemAbsorb, stateChemResist, stateEnergyAbsorb, stateEnergyResist);
    }

    private float CalcMinResist(int typeIndex)
    {
        if (!currentUseNanites) return 0f;
        if (!currentUseChemicals && (typeIndex == 2 || typeIndex == 3)) return 0f;
        return -200f;
    }

    // ═══════════════ КОД СПЛАВА ═══════════════

    private string BuildCurrentCode()
    {
        AlloyCode.AlloyParams p;
        p.tier = currentMetalTier;
        p.useChemicals = currentUseChemicals;
        p.useNanites = currentUseNanites;
        p.kineticAbsorption = stateKinAbsorb;
        p.kineticResistance = stateKinResist;
        p.thermalAbsorption = stateThermAbsorb;
        p.thermalResistance = stateThermResist;
        p.chemicalAbsorption = stateChemAbsorb;
        p.chemicalResistance = stateChemResist;
        p.energyAbsorption = stateEnergyAbsorb;
        p.energyResistance = stateEnergyResist;
        return AlloyCode.Encode(p);
    }

    private void ApplyCodeFromField()
    {
        if (currentFurnace == null) return;
        string code = codeInputField.Trim();
        AlloyCode.ValidationResult result = AlloyCode.ValidateForFurnace(code, currentFurnace.FurnaceTier);
        if (!result.isValid)
        {
            ShowError(result.error);
            return;
        }

        AlloyCode.AlloyParams p = result.parameters;
        currentMetalTier = p.tier;
        currentUseChemicals = p.useChemicals;
        currentUseNanites = p.useNanites;
        stateKinAbsorb = p.kineticAbsorption; stateKinResist = p.kineticResistance;
        stateThermAbsorb = p.thermalAbsorption; stateThermResist = p.thermalResistance;
        stateChemAbsorb = p.chemicalAbsorption; stateChemResist = p.chemicalResistance;
        stateEnergyAbsorb = p.energyAbsorption; stateEnergyResist = p.energyResistance;

        RecalcAllAmounts();
        SyncStringBuffers();
    }

    // ═══════════════ КРАФТ ═══════════════

    private void OnCraft()
    {
        if (currentFurnace == null || currentMetalGrams <= 0) return;

        long energy = Smelting.EnergyCost(currentFurnace.CapacityGrams, currentFurnace.FurnaceTier,
            currentMetalGrams, currentChemicalsGrams, currentNanitesGrams, currentMetalTier);

        if (!currentFurnace.HasEnoughResources(currentMetalGrams, currentMetalTier,
            currentChemicalsGrams, currentNanitesGrams, energy))
        {
            ShowError("НЕДОСТАТОЧНО РЕСУРСОВ");
            return;
        }

        long outG = Smelting.OutputAlloyGrams(currentMetalGrams, currentChemicalsGrams, currentNanitesGrams,
            currentFurnace.FurnaceTier, currentMetalTier);
        double outKg = Math.Round(outG / 1000.0, 3);
        string code = BuildCurrentCode();

        if (currentFurnace.ExecuteSmelt(currentMetalGrams, currentMetalTier,
            currentChemicalsGrams, currentNanitesGrams, energy, code, outKg))
        {
            RecalcAllAmounts();
            SyncStringBuffers();
        }
        else
        {
            ShowError("НЕДОСТАТОЧНО РЕСУРСОВ");
        }
    }

    // ═══════════════ ОШИБКА ═══════════════

    private void ShowError(string msg)
    {
        errorMessage = msg;
        errorTimer = 2f;
    }

    // ═══════════════ СИНХРОНИЗАЦИЯ СТРОК ═══════════════

    private void SyncStringBuffers()
    {
        metalAmountStr = (currentMetalGrams / 1000.0).ToString("F3");
        kinAbsorbStr = stateKinAbsorb.ToString();
        thermAbsorbStr = stateThermAbsorb.ToString();
        chemAbsorbStr = stateChemAbsorb.ToString();
        energyAbsorbStr = stateEnergyAbsorb.ToString();
        kinResistStr = stateKinResist.ToString("F1");
        thermResistStr = stateThermResist.ToString("F1");
        chemResistStr = stateChemResist.ToString("F1");
        energyResistStr = stateEnergyResist.ToString("F1");
    }

    // ═══════════════ СТИЛЬ ═══════════════

    private GUIStyle _centeredBold;
    private GUIStyle GetCenteredBoldStyle()
    {
        if (_centeredBold == null)
        {
            _centeredBold = new GUIStyle(GUI.skin.label);
            _centeredBold.alignment = TextAnchor.MiddleCenter;
            _centeredBold.fontStyle = FontStyle.Bold;
            _centeredBold.fontSize = 18;
        }
        return _centeredBold;
    }
}