using System;
using System.Text;
using UnityEngine;

#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
using UnityEngine.InputSystem;
#endif

#pragma warning disable IDE0090
public class Smelting : MonoBehaviour
{
    private bool windowOpen = false;
    private Rect windowRect = new Rect(50, 50, 936, 555);

    // Inputs
    public float furnaceCapacity = 100f;
    public int furnaceTier = 1;
    public int smelterEfficiency = 100;
    public int metalTier = 1; // will be clamped to furnaceTier
    public float metalAmount = 50f;
    public bool usePolymers = false;
    public bool useNanites = false;

    // Mitigation
    public int kineticAbsorb = 0;
    public float kineticResist = 0f;

    public int thermalAbsorb = 0;
    public float thermalResist = 0f;

    public int energyAbsorb = 0;
    public float energyResist = 0f;

    public int chemicalAbsorb = 0;
    public float chemicalResist = 0f;

    // Results
    private string alloyCode = "";
    private float alloyAmount = 0f;
    private int usedMetalTier = 0;
    private float usedMetalAmount = 0f;
    private int usedPolymerTier = 0;
    private float usedPolymerAmount = 0f;
    private int usedNaniteTier = 0;
    private float usedNaniteAmount = 0f;

    // Free points
    public int freePoints = 0;
    private int baseFreePoints = 0;

    private const float POLYMER_SHARE_IF_USED = 0.20f; // 20% of metal mass
    private const float NANITE_SHARE = 0.10f; // 10% of total mass when enabled

    private const float MAX_RESIST_BASE = 45f;
    private const float RESIST_STEP = 0.1f;
    private const float MIN_RESIST = -200f;
    private const int ABSORB_STEP = 1;

    private bool isDirty = true;

    // Editable code input field
    private string codeInputField = "";

    void Start()
    {
        RecalculateBaseFreePoints();

        // Ensure tiers validity
        if (metalTier > furnaceTier) metalTier = furnaceTier;
        if (metalTier < 1) metalTier = 1;

        // By default set metalAmount to maximum possible given current toggles and furnaceCapacity
        metalAmount = ComputeMaxMetalForCapacity(furnaceCapacity, usePolymers, useNanites);
        Recalculate();
    }

    void Update()
    {
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
        if (Keyboard.current != null && Keyboard.current.oKey.wasPressedThisFrame)
        {
            windowOpen = !windowOpen;
        }
#else
        if (Input.GetKeyDown(KeyCode.O)) windowOpen = !windowOpen;
#endif

        if (isDirty)
        {
            Recalculate();
            isDirty = false;
        }
    }

    void OnGUI()
    {
        if (!windowOpen) return;
        windowRect = GUI.Window(123456, windowRect, DrawWindow, "Smelting");
    }

    private void DrawWindow(int id)
    {
        GUI.DragWindow(new Rect(0, 0, 10000, 20));
        GUILayout.BeginVertical();

        // Code input field + Apply button (above capacity/tier/efficiency)
        GUILayout.BeginHorizontal();
        GUILayout.Label("Код для вставки:", GUILayout.Width(110));
        codeInputField = GUILayout.TextField(codeInputField, GUILayout.Width(windowRect.width - 260));
        if (GUILayout.Button("Применить код", GUILayout.Width(120)))
        {
            bool ok = ParseAlloyCode(codeInputField.Trim());
            if (!ok)
            {
                Debug.LogWarning("Неправильный код");
            }
            else
            {
                isDirty = true;
                Recalculate();
            }
        }
        GUILayout.EndHorizontal();

        GUILayout.Space(6);

        GUILayout.BeginHorizontal();

        // Left column for other controls moved one line down
        GUILayout.BeginVertical(GUILayout.Width(windowRect.width * 0.5f - 10));

        GUILayout.BeginHorizontal();
        GUILayout.Label("Емкость плавильни (кг):", GUILayout.Width(200));
        float newCapacity = FloatFieldClampInline(furnaceCapacity, 0f, 100000f, GUILayout.Width(140));
        if (!Mathf.Approximately(newCapacity, furnaceCapacity))
        {
            furnaceCapacity = newCapacity;
            ClampMetalToCapacity();
            isDirty = true;
        }
        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();
        GUILayout.Label("Тир плавильни:", GUILayout.Width(200));
        int newFurnaceTier = Mathf.Clamp(Mathf.RoundToInt(GUILayout.HorizontalSlider(furnaceTier, 1, 10, GUILayout.Width(120))), 1, 10);
        if (newFurnaceTier != furnaceTier)
        {
            furnaceTier = newFurnaceTier;
            if (metalTier > furnaceTier) metalTier = furnaceTier;
            isDirty = true;
        }
        GUILayout.Label($"{furnaceTier}", GUILayout.Width(40));
        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();
        GUILayout.Label("Эффективность плавильщика:", GUILayout.Width(200));
        int newEff = IntFieldClampInline(smelterEfficiency, 0, 9999, GUILayout.Width(140));
        if (newEff != smelterEfficiency) { smelterEfficiency = newEff; isDirty = true; }
        GUILayout.EndHorizontal();

        GUILayout.EndVertical();

        // Right column
        GUILayout.BeginVertical(GUILayout.Width(windowRect.width * 0.5f - 10));

        GUILayout.BeginHorizontal();
        GUILayout.Label("Кол-во металла (кг):", GUILayout.Width(200));
        float newMetalAmount = FloatFieldClampInline(metalAmount, 0f, 100000f, GUILayout.Width(140));
        if (!Mathf.Approximately(newMetalAmount, metalAmount))
        {
            metalAmount = newMetalAmount;
            ClampMetalToCapacity();
            isDirty = true;
        }
        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();
        GUILayout.Label("Тир металла:", GUILayout.Width(200));
        int newMetalTier = Mathf.Clamp(Mathf.RoundToInt(GUILayout.HorizontalSlider(metalTier, 1, furnaceTier, GUILayout.Width(120))), 1, furnaceTier);
        if (newMetalTier != metalTier) { metalTier = newMetalTier; isDirty = true; }
        GUILayout.Label($"{metalTier}", GUILayout.Width(40));
        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();
        bool newUsePol = GUILayout.Toggle(usePolymers, "Использовать полимеры (20% от металла)", GUILayout.Width(300));
        if (newUsePol != usePolymers)
        {
            usePolymers = newUsePol;
            ClampMetalToCapacity();
            isDirty = true;
        }
        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();
        bool newUseNano = GUILayout.Toggle(useNanites, "Использовать наниты (10% от суммарной массы)", GUILayout.Width(300));
        if (newUseNano != useNanites)
        {
            useNanites = newUseNano;
            ClampMetalToCapacity();
            if (!useNanites)
            {
                if (kineticResist < 0f) kineticResist = 0f;
                if (thermalResist < 0f) thermalResist = 0f;
                if (energyResist < 0f) energyResist = 0f;
                if (chemicalResist < 0f) chemicalResist = 0f;
            }
            isDirty = true;
        }
        GUILayout.EndHorizontal();

        GUILayout.EndVertical();
        GUILayout.EndHorizontal(); // end top

        GUILayout.Space(6);

        GUILayout.BeginHorizontal();
        GUILayout.Label($"Базовые свободные очки (metalTier*300): {baseFreePoints}", GUILayout.Width(windowRect.width * 0.5f));
        GUILayout.Label($"Текущие свободные очки: {freePoints}", GUILayout.Width(windowRect.width * 0.5f));
        GUILayout.EndHorizontal();

        GUILayout.Space(6);

        GUILayout.BeginHorizontal();

        // Left group
        GUILayout.BeginVertical("box", GUILayout.Width(windowRect.width * 0.5f - 10));

        GUILayout.BeginHorizontal();
        GUILayout.Label("Поглощение кинетического урона:", GUILayout.Width(220));
        int newKAbs = IntFieldWithButtonsConditionalUI(kineticAbsorb, ref kineticResist);
        if (newKAbs != kineticAbsorb) { kineticAbsorb = newKAbs; isDirty = true; }
        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();
        GUILayout.Label("Сопротивление кинетическому урону (%):", GUILayout.Width(220));
        float newKRes = FloatFieldWithButtonsConditionalUI_WithNanoAndFree(kineticResist, ref kineticAbsorb);
        if (!Mathf.Approximately(newKRes, kineticResist)) { kineticResist = newKRes; isDirty = true; }
        GUILayout.EndHorizontal();

        GUILayout.Space(4);

        GUILayout.BeginHorizontal();
        GUILayout.Label("Поглощение термического урона:", GUILayout.Width(220));
        int newTAbs = IntFieldWithButtonsConditionalUI(thermalAbsorb, ref thermalResist);
        if (newTAbs != thermalAbsorb) { thermalAbsorb = newTAbs; isDirty = true; }
        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();
        GUILayout.Label("Сопротивление термическому урону (%):", GUILayout.Width(220));
        float newTRes = FloatFieldWithButtonsConditionalUI_WithNanoAndFree(thermalResist, ref thermalAbsorb);
        if (!Mathf.Approximately(newTRes, thermalResist)) { thermalResist = newTRes; isDirty = true; }
        GUILayout.EndHorizontal();

        GUILayout.EndVertical();

        // Right group
        GUILayout.BeginVertical("box", GUILayout.Width(windowRect.width * 0.5f - 10));

        // Energy (locked if polymers not used)
        GUILayout.BeginHorizontal();
        GUILayout.Label("Поглощение энергетического урона:", GUILayout.Width(220));
        bool energyControlsEnabled = usePolymers;
        if (!energyControlsEnabled) EditorDisableBegin(true);
        int newEAbs = IntFieldWithButtonsConditionalUI(energyAbsorb, ref energyResist, energyControlsEnabled);
        if (!energyControlsEnabled) EditorDisableEnd();
        if (energyControlsEnabled && newEAbs != energyAbsorb) { energyAbsorb = newEAbs; isDirty = true; }
        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();
        GUILayout.Label("Сопротивление энергетическому урону (%):", GUILayout.Width(220));
        if (!energyControlsEnabled) EditorDisableBegin(true);
        float newERes = FloatFieldWithButtonsConditionalUI_WithNanoAndFree(energyResist, ref energyAbsorb, energyControlsEnabled);
        if (!energyControlsEnabled) EditorDisableEnd();
        if (energyControlsEnabled && !Mathf.Approximately(newERes, energyResist)) { energyResist = newERes; isDirty = true; }
        GUILayout.EndHorizontal();

        GUILayout.Space(4);

        // Chemical (locked if polymers not used)
        GUILayout.BeginHorizontal();
        GUILayout.Label("Поглощение химического урона:", GUILayout.Width(220));
        bool chemControlsEnabled = usePolymers;
        if (!chemControlsEnabled) EditorDisableBegin(true);
        int newCAbs = IntFieldWithButtonsConditionalUI(chemicalAbsorb, ref chemicalResist, chemControlsEnabled);
        if (!chemControlsEnabled) EditorDisableEnd();
        if (chemControlsEnabled && newCAbs != chemicalAbsorb) { chemicalAbsorb = newCAbs; isDirty = true; }
        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();
        GUILayout.Label("Сопротивление химическому урону (%):", GUILayout.Width(220));
        if (!chemControlsEnabled) EditorDisableBegin(true);
        float newCRes = FloatFieldWithButtonsConditionalUI_WithNanoAndFree(chemicalResist, ref chemicalAbsorb, chemControlsEnabled);
        if (!chemControlsEnabled) EditorDisableEnd();
        if (chemControlsEnabled && !Mathf.Approximately(newCRes, chemicalResist)) { chemicalResist = newCRes; isDirty = true; }
        GUILayout.EndHorizontal();

        GUILayout.EndVertical();

        GUILayout.EndHorizontal();

        GUILayout.Space(8);

        // Bottom
        GUILayout.BeginHorizontal("box");

        GUILayout.BeginVertical(GUILayout.Width(windowRect.width * 0.6f - 10));
        // Code label and copy button on same row
        GUILayout.BeginHorizontal();
        GUILayout.Label($"Код сплава: {alloyCode}", GUILayout.Width(windowRect.width * 0.6f - 140));
        if (GUILayout.Button("Копировать код", GUILayout.Width(120)))
        {
            GUIUtility.systemCopyBuffer = alloyCode ?? string.Empty;
        }
        GUILayout.EndHorizontal();

        GUILayout.Label($"Количество получаемого сплава (кг): {alloyAmount:F2}");
        GUILayout.Label($"Количество затрачиваемого металла: {usedMetalAmount:F2}");
        GUILayout.Label($"Количество затрачиваемых полимеров: {usedPolymerAmount:F2}");
        GUILayout.Label($"Количество затрачиваемых нанитов: {usedNaniteAmount:F2}");
        GUILayout.EndVertical();

        GUILayout.BeginVertical(GUILayout.Width(windowRect.width * 0.4f - 10));
        GUILayout.FlexibleSpace();

        GUILayout.BeginHorizontal();
        GUILayout.Space(10);
        GUILayout.Label($"Тир затрачиваемого металла: {usedMetalTier}");
        GUILayout.FlexibleSpace();
        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();
        GUILayout.Space(10);
        GUILayout.Label($"Тир затрачиваемых полимеров: {usedPolymerTier}");
        GUILayout.FlexibleSpace();
        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();
        GUILayout.Space(10);
        GUILayout.Label($"Тир затрачиваемых нанитов: {usedNaniteTier}");
        GUILayout.FlexibleSpace();
        GUILayout.EndHorizontal();

        GUILayout.FlexibleSpace();
        GUILayout.EndVertical();

        GUILayout.EndHorizontal();

        GUILayout.Space(6);

        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Изготовить", GUILayout.Height(34)))
        {
            TryCraft();
            isDirty = true;
        }
        if (GUILayout.Button("Сброс", GUILayout.Height(34)))
        {
            ResetExceptFurnace();
            metalAmount = ComputeMaxMetalForCapacity(furnaceCapacity, usePolymers, useNanites);
            if (metalTier > furnaceTier) metalTier = furnaceTier;
            isDirty = true;
        }

        GUILayout.EndHorizontal();

        GUILayout.EndVertical();
    }

    private void EditorDisableBegin(bool disabled)
    {
        GUI.enabled = !disabled;
        if (disabled)
        {
            var prev = GUI.color;
            GUI.color = new Color(prev.r * 0.8f, prev.g * 0.8f, prev.b * 0.8f, prev.a);
        }
    }

    private void EditorDisableEnd()
    {
        GUI.enabled = true;
        GUI.color = Color.white;
    }

    private float FloatFieldClampInline(float value, float min, float max, params GUILayoutOption[] options)
    {
        string s = GUILayout.TextField(value.ToString("0.##"), options);
        if (float.TryParse(s, out float parsed)) value = Mathf.Clamp(parsed, min, max);
        return value;
    }

    private int IntFieldClampInline(int value, int min, int max, params GUILayoutOption[] options)
    {
        string s = GUILayout.TextField(value.ToString(), options);
        if (int.TryParse(s, out int parsed)) value = Mathf.Clamp(parsed, min, max);
        return value;
    }

    // Overload: allow passing enabled flag so we can fully disable both visual and logical behavior
    private int IntFieldWithButtonsConditionalUI(int current, ref float correspondingResist, bool enabled = true)
    {
        GUILayout.BeginHorizontal();

        bool minusEnabled = enabled && current > 0;
        if (!minusEnabled) EditorDisableBegin(true);
        if (GUILayout.Button("-", GUILayout.Width(24)) && minusEnabled)
        {
            current = Mathf.Max(0, current - ABSORB_STEP);
        }
        if (!minusEnabled) EditorDisableEnd();

        string s = GUILayout.TextField(current.ToString(), GUILayout.Width(80));
        if (enabled && int.TryParse(s, out int parsed))
        {
            current = Mathf.Max(0, parsed);
            if (current > 0 && correspondingResist < 0f) current = 0;
        }

        bool plusEnabled = enabled && correspondingResist >= 0f;
        if (!plusEnabled) EditorDisableBegin(true);
        if (GUILayout.Button("+", GUILayout.Width(24)) && plusEnabled)
        {
            current += ABSORB_STEP;
            if (current > 0 && correspondingResist < 0f) current = 0;
        }
        if (!plusEnabled) EditorDisableEnd();

        GUILayout.EndHorizontal();
        return current;
    }

    private float FloatFieldWithButtonsConditionalUI_WithNanoAndFree(float current, ref int correspondingAbsorb, bool enabled = true)
    {
        GUILayout.BeginHorizontal();

        bool canDecreasePositive = enabled && current > 0f;
        bool canDecreaseIntoNegative = enabled && useNanites && current > MIN_RESIST;
        bool minusEnabled = correspondingAbsorb == 0 && (canDecreasePositive || canDecreaseIntoNegative);

        if (!minusEnabled) EditorDisableBegin(true);
        if (GUILayout.Button("-", GUILayout.Width(24)) && minusEnabled)
        {
            float next = current - RESIST_STEP;

            if (!useNanites && next < 0f) next = 0f;
            next = Mathf.Max(next, MIN_RESIST);

            current = next;
        }
        if (!minusEnabled) EditorDisableEnd();

        string fieldStr = GUILayout.TextField(current.ToString("0.0"), GUILayout.Width(80));
        if (enabled && float.TryParse(fieldStr, out float parsed))
        {
            if (!useNanites && parsed < 0f) parsed = 0f;
            if (parsed < MIN_RESIST) parsed = MIN_RESIST;

            float maxResist = MAX_RESIST_BASE + 5f * metalTier;
            if (parsed > maxResist) parsed = maxResist;

            current = parsed;
        }

        int beforeCost = Mathf.FloorToInt(Mathf.Abs(current) / RESIST_STEP);
        int afterCost = Mathf.FloorToInt(Mathf.Abs(current + RESIST_STEP) / RESIST_STEP);
        int costDelta = afterCost - beforeCost;

        bool plusEnabled = enabled && costDelta <= freePoints;
        if (!plusEnabled) EditorDisableBegin(true);
        if (GUILayout.Button("+", GUILayout.Width(24)) && plusEnabled)
        {
            current = Mathf.Min(current + RESIST_STEP, MAX_RESIST_BASE + 5f * metalTier);
        }
        if (!plusEnabled) EditorDisableEnd();

        GUILayout.EndHorizontal();

        float maxR = MAX_RESIST_BASE + 5f * metalTier;
        current = Mathf.Clamp(current, MIN_RESIST, maxR);

        if (correspondingAbsorb > 0 && current < 0f) current = 0f;
        if (!useNanites && current < 0f) current = 0f;

        return current;
    }

    private void RecalculateBaseFreePoints() => baseFreePoints = metalTier * 300;

    public void Recalculate()
    {
        RecalculateBaseFreePoints();
        int pool = baseFreePoints;

        if (!useNanites)
        {
            if (kineticResist < 0f) kineticResist = 0f;
            if (thermalResist < 0f) thermalResist = 0f;
            if (energyResist < 0f) energyResist = 0f;
            if (chemicalResist < 0f) chemicalResist = 0f;
        }

        if (kineticAbsorb > 0 && kineticResist < 0f) kineticResist = 0f;
        if (thermalAbsorb > 0 && thermalResist < 0f) thermalResist = 0f;
        if (energyAbsorb > 0 && energyResist < 0f) energyResist = 0f;
        if (chemicalAbsorb > 0 && chemicalResist < 0f) chemicalResist = 0f;

        int totalAbsorb = kineticAbsorb + thermalAbsorb + energyAbsorb + chemicalAbsorb;

        int costRes = Mathf.FloorToInt(Mathf.Abs(Mathf.Max(0f, kineticResist)) / RESIST_STEP)
                    + Mathf.FloorToInt(Mathf.Abs(Mathf.Max(0f, thermalResist)) / RESIST_STEP)
                    + Mathf.FloorToInt(Mathf.Abs(Mathf.Max(0f, energyResist)) / RESIST_STEP)
                    + Mathf.FloorToInt(Mathf.Abs(Mathf.Max(0f, chemicalResist)) / RESIST_STEP);

        int refund = ComputeRefundPointsForNegative(kineticResist)
                   + ComputeRefundPointsForNegative(thermalResist)
                   + ComputeRefundPointsForNegative(energyResist)
                   + ComputeRefundPointsForNegative(chemicalResist);

        int netResCostSigned = costRes - refund;
        int totalCostSigned = totalAbsorb + netResCostSigned;

        if (totalCostSigned > pool)
        {
            int over = totalCostSigned - pool;
            int reducibleAbs = totalAbsorb;
            int reduceAbs = Math.Min(over, reducibleAbs);
            if (reduceAbs > 0)
            {
                ReduceAbsorbs(reduceAbs);
                over -= reduceAbs;
            }
            if (over > 0)
            {
                ReducePositiveResists(over * RESIST_STEP);
            }

            totalAbsorb = kineticAbsorb + thermalAbsorb + energyAbsorb + chemicalAbsorb;
            costRes = Mathf.FloorToInt(Mathf.Abs(Mathf.Max(0f, kineticResist)) / RESIST_STEP)
                    + Mathf.FloorToInt(Mathf.Abs(Mathf.Max(0f, thermalResist)) / RESIST_STEP)
                    + Mathf.FloorToInt(Mathf.Abs(Mathf.Max(0f, energyResist)) / RESIST_STEP)
                    + Mathf.FloorToInt(Mathf.Abs(Mathf.Max(0f, chemicalResist)) / RESIST_STEP);

            refund = ComputeRefundPointsForNegative(kineticResist)
                   + ComputeRefundPointsForNegative(thermalResist)
                   + ComputeRefundPointsForNegative(energyResist)
                   + ComputeRefundPointsForNegative(chemicalResist);

            netResCostSigned = costRes - refund;
            totalCostSigned = totalAbsorb + netResCostSigned;
        }

        int computedFree = pool - totalCostSigned;
        if (computedFree < 0) computedFree = 0;
        freePoints = computedFree;

        float polymersMass = 0f;
        if (usePolymers) polymersMass = POLYMER_SHARE_IF_USED * metalAmount;
        usedNaniteAmount = useNanites ? NANITE_SHARE * (metalAmount + polymersMass) : 0f;
        usedMetalAmount = metalAmount;
        usedPolymerAmount = polymersMass;
        usedMetalTier = metalTier;
        usedPolymerTier = usePolymers ? metalTier : 0;
        usedNaniteTier = useNanites ? metalTier : 0;

        float baseMass = 0.5f * usedMetalAmount + 0.5f * usedPolymerAmount;
        float furnaceBonus = 0.05f * furnaceTier;
        float metalTierPenalty = 0.01f * metalTier;
        float multiplier = 1f + furnaceBonus - metalTierPenalty;
        alloyAmount = baseMass * multiplier;

        BuildAlloyCode();
    }

    private void ReduceAbsorbs(int toReduce)
    {
        for (int i = 0; i < toReduce; i++)
        {
            int maxVal = Mathf.Max(kineticAbsorb, thermalAbsorb, energyAbsorb, chemicalAbsorb);
            if (maxVal == 0) break;
            if (kineticAbsorb == maxVal) kineticAbsorb = Mathf.Max(0, kineticAbsorb - 1);
            else if (thermalAbsorb == maxVal) thermalAbsorb = Mathf.Max(0, thermalAbsorb - 1);
            else if (energyAbsorb == maxVal) energyAbsorb = Mathf.Max(0, energyAbsorb - 1);
            else if (chemicalAbsorb == maxVal) chemicalAbsorb = Mathf.Max(0, chemicalAbsorb - 1);
        }
    }

    private void ReducePositiveResists(float totalPercent)
    {
        float k = Mathf.Max(0f, kineticResist);
        float t = Mathf.Max(0f, thermalResist);
        float e = Mathf.Max(0f, energyResist);
        float c = Mathf.Max(0f, chemicalResist);
        float sum = k + t + e + c;
        if (sum <= 0f) return;
        kineticResist = Mathf.Max(0f, kineticResist - totalPercent * (k / sum));
        thermalResist = Mathf.Max(0f, thermalResist - totalPercent * (t / sum));
        energyResist = Mathf.Max(0f, energyResist - totalPercent * (e / sum));
        chemicalResist = Mathf.Max(0f, chemicalResist - totalPercent * (c / sum));
    }

    // Compute refund points for negative resist using tiered steps:
    // 0 .. -50%   => 1 point per 0.2%
    // -50 .. -100 => 1 point per 0.33%
    // -100 .. -150=> 1 per 0.5%
    // -150 .. -200=> 1 per 1%
    private int ComputeRefundPointsForNegative(float resist)
    {
        if (resist >= 0f) return 0;

        float r = -resist; // positive magnitude in percent
        int points = 0;

        const float t1 = 50f;
        const float t2 = 100f;
        const float t3 = 150f;
        const float t4 = 200f;

        const float s1 = 0.2f;
        const float s2 = 0.333f;
        const float s3 = 0.5f;
        const float s4 = 1.0f;

        float seg;

        seg = Mathf.Min(r, t1);
        if (seg > 0f) points += Mathf.FloorToInt(seg / s1);

        if (r > t1)
        {
            seg = Mathf.Min(r, t2) - t1;
            if (seg > 0f) points += Mathf.FloorToInt(seg / s2);
        }

        if (r > t2)
        {
            seg = Mathf.Min(r, t3) - t2;
            if (seg > 0f) points += Mathf.FloorToInt(seg / s3);
        }

        if (r > t3)
        {
            seg = Mathf.Min(r, t4) - t3;
            if (seg > 0f) points += Mathf.FloorToInt(seg / s4);
        }

        return points;
    }

    private void TryCraft()
    {
        if (usedMetalAmount + usedPolymerAmount + usedNaniteAmount > furnaceCapacity)
        {
            Debug.LogWarning("Total used mass exceeds furnace capacity.");
            return;
        }

        alloyCode = BuildAlloyCodeString();
        isDirty = true;
    }

    private float ComputeMaxMetalForCapacity(float capacity, bool polymersEnabled, bool nanitesEnabled)
    {
        float factor = 1f;
        if (polymersEnabled) factor *= (1f + POLYMER_SHARE_IF_USED);
        if (nanitesEnabled) factor *= (1f + NANITE_SHARE);
        if (factor <= 0f) return 0f;
        return capacity / factor;
    }

    private void ClampMetalToCapacity()
    {
        float maxMetal = ComputeMaxMetalForCapacity(furnaceCapacity, usePolymers, useNanites);
        if (metalAmount > maxMetal) metalAmount = maxMetal;
        if (metalAmount < 0f) metalAmount = 0f;
    }

    private void ResetExceptFurnace()
    {
        metalTier = 1;
        metalAmount = 0f;
        usePolymers = false;
        useNanites = false;

        kineticAbsorb = 0; kineticResist = 0f;
        thermalAbsorb = 0; thermalResist = 0f;
        energyAbsorb = 0; energyResist = 0f;
        chemicalAbsorb = 0; chemicalResist = 0f;

        alloyCode = "";
        alloyAmount = 0f;
        usedMetalTier = 0;
        usedMetalAmount = 0f;
        usedPolymerTier = 0;
        usedPolymerAmount = 0f;
        usedNaniteTier = 0;
        usedNaniteAmount = 0f;

        metalAmount = ComputeMaxMetalForCapacity(furnaceCapacity, usePolymers, useNanites);

        isDirty = true;
    }

    private void BuildAlloyCode()
    {
        alloyCode = BuildAlloyCodeString();
    }

    private string BuildAlloyCodeString()
    {
        StringBuilder sb = new StringBuilder();

        // Part 1: metal tier
        sb.Append(metalTier);

        // Part 2 & 3: P and N flags immediately after tier
        if (usePolymers) sb.Append('P');
        if (useNanites) sb.Append('N');

        sb.Append('-');

        // Kinetic
        sb.Append('K');
        sb.Append(kineticAbsorb);
        sb.Append('/');
        sb.Append(FormatResist(kineticResist));

        sb.Append('-');

        // Thermal
        sb.Append('T');
        sb.Append(thermalAbsorb);
        sb.Append('/');
        sb.Append(FormatResist(thermalResist));

        sb.Append('-');

        // Energy
        sb.Append('E');
        sb.Append(energyAbsorb);
        sb.Append('/');
        sb.Append(FormatResist(energyResist));

        sb.Append('-');

        // Chemical
        sb.Append('C');
        sb.Append(chemicalAbsorb);
        sb.Append('/');
        sb.Append(FormatResist(chemicalResist));

        return sb.ToString();
    }

    private static string FormatResist(float resist)
    {
        int scaled = Mathf.RoundToInt(resist * 10f);
        if (scaled >= 0)
        {
            return scaled.ToString("D3");
        }
        else
        {
            int absVal = Math.Abs(scaled);
            return "m" + absVal.ToString("D3");
        }
    }

    // Parse alloy code and apply to current fields. Returns true if success, false if invalid.
    // Format:
    // <metalTier>[P][N]-K<absorb>/<resist>-T<absorb>/<resist>-E<absorb>/<resist>-C<absorb>/<resist>
    private bool ParseAlloyCode(string code)
    {
        if (string.IsNullOrEmpty(code)) return false;

        try
        {
            int idx = 0;
            int len = code.Length;

            // metal tier
            if (idx >= len || !char.IsDigit(code[idx])) return false;
            int start = idx;
            while (idx < len && char.IsDigit(code[idx])) idx++;
            string tierStr = code.Substring(start, idx - start);
            if (!int.TryParse(tierStr, out int parsedMetalTier)) return false;
            if (parsedMetalTier < 1) return false;

            bool parsedP = false;
            bool parsedN = false;
            while (idx < len && (code[idx] == 'P' || code[idx] == 'N'))
            {
                if (code[idx] == 'P') parsedP = true;
                if (code[idx] == 'N') parsedN = true;
                idx++;
            }

            if (idx >= len || code[idx] != '-') return false;
            idx++;

            bool ParseSegment(char expectedLetter, out int outAbsorb, out float outResist)
            {
                outAbsorb = 0;
                outResist = 0f;
                if (idx >= len || code[idx] != expectedLetter) return false;
                idx++;

                if (idx >= len || !char.IsDigit(code[idx])) return false;
                int s = idx;
                while (idx < len && char.IsDigit(code[idx])) idx++;
                string absStr = code.Substring(s, idx - s);
                if (!int.TryParse(absStr, out outAbsorb)) return false;

                if (idx >= len || code[idx] != '/') return false;
                idx++;

                if (idx >= len) return false;
                if (code[idx] == 'm')
                {
                    // negative: expect exactly 3 digits after 'm'
                    idx++;
                    if (idx + 3 > len) return false;
                    string num = code.Substring(idx, 3);
                    if (!IsAllDigits(num)) return false;
                    idx += 3;
                    if (!int.TryParse(num, out int val)) return false;
                    outResist = -(val / 10f);
                }
                else
                {
                    // positive: expect exactly 3 digits
                    if (idx + 3 > len) return false;
                    string num = code.Substring(idx, 3);
                    if (!IsAllDigits(num)) return false;
                    idx += 3;
                    if (!int.TryParse(num, out int val)) return false;
                    outResist = val / 10f;
                }

                return true;
            }

            if (!ParseSegment('K', out int kAbs, out float kRes)) return false;
            if (idx >= len || code[idx] != '-') return false;
            idx++;

            if (!ParseSegment('T', out int tAbs, out float tRes)) return false;
            if (idx >= len || code[idx] != '-') return false;
            idx++;

            if (!ParseSegment('E', out int eAbs, out float eRes)) return false;
            if (idx >= len || code[idx] != '-') return false;
            idx++;

            if (!ParseSegment('C', out int cAbs, out float cRes)) return false;

            if (idx != len) return false;

            // Validate ranges
            float maxResAllowed = MAX_RESIST_BASE + 5f * parsedMetalTier;
            if (kAbs < 0 || tAbs < 0 || eAbs < 0 || cAbs < 0) return false;
            if (kRes > maxResAllowed || tRes > maxResAllowed || eRes > maxResAllowed || cRes > maxResAllowed) return false;
            if (kRes < MIN_RESIST || tRes < MIN_RESIST || eRes < MIN_RESIST || cRes < MIN_RESIST) return false;
            if ((kAbs > 0 && kRes < 0f) || (tAbs > 0 && tRes < 0f) || (eAbs > 0 && eRes < 0f) || (cAbs > 0 && cRes < 0f)) return false;

            // Apply parsed values
            metalTier = parsedMetalTier;
            usePolymers = parsedP;
            useNanites = parsedN;

            kineticAbsorb = kAbs; kineticResist = kRes;
            thermalAbsorb = tAbs; thermalResist = tRes;
            energyAbsorb = eAbs; energyResist = eRes;
            chemicalAbsorb = cAbs; chemicalResist = cRes;

            if (metalTier > furnaceTier) metalTier = furnaceTier;
            if (metalTier < 1) metalTier = 1;

            ClampMetalToCapacity();

            BuildAlloyCode();
            codeInputField = alloyCode;

            return true;
        }
        catch
        {
            return false;
        }
    }

    private static bool IsAllDigits(string s)
    {
        if (string.IsNullOrEmpty(s)) return false;
        foreach (char c in s) if (!char.IsDigit(c)) return false;
        return true;
    }
}
#pragma warning restore IDE0090