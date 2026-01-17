using System;
using UnityEngine;

#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
using UnityEngine.InputSystem;
#endif

public class Smelting : MonoBehaviour
{
    private bool windowOpen = false;
    private Rect windowRect = new(50, 50, 936, 555);

    // Inputs
    public float furnaceCapacity = 100f;
    public int furnaceTier = 1;
    public int smelterEfficiency = 100;
    public int metalTier = 1;
    public float metalAmount = 50f;
    public bool usePolymers = false;
    public float polymerSharePercent = 0f;
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

    private const float MAX_POLYMER_SHARE = 20f;
    private const float MAX_RESIST_BASE = 45f;
    private const float RESIST_STEP = 0.1f;
    private const float MIN_RESIST = -200f;
    private const int ABSORB_STEP = 1;

    private bool isDirty = true;

    void Start()
    {
        RecalculateBaseFreePoints();
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

        GUILayout.BeginHorizontal();

        // Left column
        GUILayout.BeginVertical(GUILayout.Width(windowRect.width * 0.5f - 10));

        GUILayout.BeginHorizontal();
        GUILayout.Label("Емкость плавильни (кг):", GUILayout.Width(200));
        float newCapacity = FloatFieldClampInline(furnaceCapacity, 0f, 100000f, GUILayout.Width(140));
        if (!Mathf.Approximately(newCapacity, furnaceCapacity)) { furnaceCapacity = newCapacity; isDirty = true; }
        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();
        GUILayout.Label("Тир плавильни:", GUILayout.Width(200));
        int newFurnaceTier = Mathf.Clamp(Mathf.RoundToInt(GUILayout.HorizontalSlider(furnaceTier, 1, 10, GUILayout.Width(120))), 1, 10);
        if (newFurnaceTier != furnaceTier) { furnaceTier = newFurnaceTier; isDirty = true; }
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
        if (!Mathf.Approximately(newMetalAmount, metalAmount)) { metalAmount = newMetalAmount; isDirty = true; }
        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();
        GUILayout.Label("Тир металла:", GUILayout.Width(200));
        int newMetalTier = Mathf.Clamp(Mathf.RoundToInt(GUILayout.HorizontalSlider(metalTier, 1, 10, GUILayout.Width(120))), 1, 10);
        if (newMetalTier != metalTier) { metalTier = newMetalTier; isDirty = true; }
        GUILayout.Label($"{metalTier}", GUILayout.Width(40));
        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();
        bool newUsePol = GUILayout.Toggle(usePolymers, "Использовать полимеры", GUILayout.Width(200));
        if (newUsePol != usePolymers)
        {
            usePolymers = newUsePol;
            if (!usePolymers)
            {
                energyAbsorb = 0; energyResist = 0f;
                chemicalAbsorb = 0; chemicalResist = 0f;
            }
            isDirty = true;
        }
        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();
        GUILayout.Label("Доля полимеров (%):", GUILayout.Width(200));
        EditorDisableBegin(!usePolymers);
        float newPoly = GUILayout.HorizontalSlider(polymerSharePercent, 0f, MAX_POLYMER_SHARE, GUILayout.Width(120));
        if (!usePolymers) newPoly = polymerSharePercent;
        if (!Mathf.Approximately(newPoly, polymerSharePercent)) { polymerSharePercent = newPoly; isDirty = true; }
        GUILayout.Label($"{polymerSharePercent:F1}%", GUILayout.Width(40));
        EditorDisableEnd();
        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();
        bool newUseNano = GUILayout.Toggle(useNanites, "Использовать наниты", GUILayout.Width(200));
        if (newUseNano != useNanites)
        {
            useNanites = newUseNano;
            if (!useNanites)
            {
                // сбрасываем любые отрицательные значения при отключенных нанитах
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

        // Энергетика - блокируем визуально и логически, если usePolymers == false
        GUILayout.BeginHorizontal();
        GUILayout.Label("Поглощение энергетического урона:", GUILayout.Width(220));
        bool energyControlsEnabled = usePolymers; // полностью завязаны на usePolymers
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

        // Химия - аналогично
        GUILayout.BeginHorizontal();
        GUILayout.Label("Поглощение химического урона:", GUILayout.Width(220));
        bool chemControlsEnabled = usePolymers; // полностью завязаны на usePolymers
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
        GUILayout.Label($"Код сплава: {alloyCode}");
        GUILayout.Label($"Количество получаемого сплава (кг): {alloyAmount:F2}");
        GUILayout.Label($"Количество затрачиваемого металла: {usedMetalAmount:F2}");
        GUILayout.Label($"Количество затрачиваемых полимеров: {usedPolymerAmount:F2}");
        GUILayout.Label($"Количество затрачиваемых нанитов: {usedNaniteAmount:F2}");
        GUILayout.EndVertical();

        GUILayout.BeginVertical(GUILayout.Width(windowRect.width * 0.4f - 10));
        GUILayout.FlexibleSpace();

        // Надписи про тиры смещены левее (даем дополнительный отступ слева)
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

        // '-' disabled when current == 0 or when overall control disabled
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

        // '+' button: disabled when corresponding resist < 0 (absorb must be 0 then) or control disabled
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

    // Updated resist method (removed local freePoints increments and unused locals)
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
            // Recalculate will handle freePoints changes
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
            // Recalculate will handle freePoints changes
        }

        int beforeCost = Mathf.FloorToInt(Mathf.Abs(current) / RESIST_STEP);
        int afterCost = Mathf.FloorToInt(Mathf.Abs(current + RESIST_STEP) / RESIST_STEP);
        int costDelta = afterCost - beforeCost;

        bool plusEnabled = enabled && costDelta <= freePoints;
        if (!plusEnabled) EditorDisableBegin(true);
        if (GUILayout.Button("+", GUILayout.Width(24)) && plusEnabled)
        {
            current = Mathf.Min(current + RESIST_STEP, MAX_RESIST_BASE + 5f * metalTier);
            // Recalculate will handle freePoints changes
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

        // Ensure negative resists are ignored if nanites disabled BEFORE computing refund
        if (!useNanites)
        {
            if (kineticResist < 0f) kineticResist = 0f;
            if (thermalResist < 0f) thermalResist = 0f;
            if (energyResist < 0f) energyResist = 0f;
            if (chemicalResist < 0f) chemicalResist = 0f;
        }

        // Enforce absorb vs negative resist rule (can't have absorb >0 while resist<0)
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

        // Allow netResCost to be negative: negative means extra points beyond pool
        int netResCostSigned = costRes - refund;
        int totalCostSigned = totalAbsorb + netResCostSigned;

        // If over pool, try to reduce absorbs first and then positive resists proportionaly
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

            // Recalculate after reductions
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

        // Compute final freePoints. Now freePoints can be greater than pool if netResCostSigned < 0
        int computedFree = pool - totalCostSigned;
        if (computedFree < 0) computedFree = 0;
        freePoints = computedFree;

        // Compute used masses/tiers for display (now updated continuously)
        float polymersMass = 0f;
        if (usePolymers) polymersMass = (polymerSharePercent / 100f) * metalAmount;
        usedNaniteAmount = useNanites ? 0.01f * (metalAmount + polymersMass) : 0f;
        usedMetalAmount = metalAmount;
        usedPolymerAmount = polymersMass;
        usedMetalTier = metalTier;
        usedPolymerTier = usePolymers ? metalTier : 0;
        usedNaniteTier = useNanites ? metalTier : 0;

        // Compute alloy amount continuously (same formula as TryCraft)
        float baseMass = 0.5f * usedMetalAmount + 0.5f * usedPolymerAmount;
        float furnaceBonus = 0.05f * furnaceTier;
        float metalTierPenalty = 0.01f * metalTier;
        float multiplier = 1f + furnaceBonus - metalTierPenalty;
        alloyAmount = baseMass * multiplier;
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

    // For each 0.1% negative resist returns 1 point
    private int ComputeRefundPointsForNegative(float resist)
    {
        if (resist >= 0f) return 0;
        int steps = Mathf.FloorToInt(Mathf.Abs(resist) / RESIST_STEP);
        return steps;
    }

    private void TryCraft()
    {
        // Keep TryCraft for possible additional behaviour (animations, consumption, etc.)
        // But main numeric computations already done in Recalculate()
        if (usedMetalAmount + usedPolymerAmount + usedNaniteAmount > furnaceCapacity)
        {
            Debug.LogWarning("Total used mass exceeds furnace capacity.");
            return;
        }

        // Example: lock in alloyCode or actually consume resources here if desired.
        alloyCode = ""; // could compute code here
        isDirty = true;
    }

    private void ResetExceptFurnace()
    {
        metalTier = 1;
        metalAmount = 0f;
        usePolymers = false;
        polymerSharePercent = 0f;
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

        isDirty = true;
    }
}