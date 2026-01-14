using UnityEngine;
using System;

#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
using UnityEngine.InputSystem;
#endif

// Smelting — IMGUI debug window for alloy smelter calculations.
// Toggle window with KeyCode.O (or via new Input System if enabled).
// Inputs are edited as text fields; press "Apply" or Enter to commit and run recalculation.

public class Smelting : MonoBehaviour
{
    public KeyCode toggleKey = KeyCode.O;
    public bool windowOpen = false;

    // Inputs (editable strings for IMGUI)
    private string sVolumeLiters;
    private string sMetalKg;
    private int metalTier = 1;
    private bool usePolymers = false;
    private string sPolyPercent;              // text input
    private float sliderPolyPercent = 0f;     // slider input (0..90, step 0.1)
    private bool useNanites = false;

    // Characteristic inputs (strings)
    private string sAbsKinetic, sAbsThermal, sResKinetic, sResThermal;
    private string sAbsEnergy, sAbsChemical, sResEnergy, sResChemical;

    // Internal numeric values (last applied)
    private float volumeLiters = 100f;
    private float metalKg = 10f;
    private float polyPercent = 0f;

    private int valAbsKinetic = 0, valAbsThermal = 0, valAbsEnergy = 0, valAbsChemical = 0;
    private float valResKinetic = 0f, valResThermal = 0f, valResEnergy = 0f, valResChemical = 0f;

    // Calculated outputs
    private float obtainedMetalKg = 0f;
    private float consumedPolyKg = 0f;
    private float consumedNanitesKg = 0f;
    private int metalPointsTotal = 0, polyPointsTotal = 0, metalPointsFree = 0, polyPointsFree = 0;

    // Window
    private Rect windowRect = new Rect(20, 20, 560, 560);
    private Vector2 scroll = Vector2.zero;

    // Constants
    private const float MAX_POLY_PERCENT = 90f;
    private const float MAX_NEGATIVE_RESISTANCE = -200f;

    void Start()
    {
        // Initialize string fields from defaults
        sVolumeLiters = volumeLiters.ToString("F1");
        sMetalKg = metalKg.ToString("F3");
        sPolyPercent = polyPercent.ToString("F1");
        sliderPolyPercent = polyPercent;

        sAbsKinetic = valAbsKinetic.ToString();
        sAbsThermal = valAbsThermal.ToString();
        sResKinetic = valResKinetic.ToString("F1");
        sResThermal = valResThermal.ToString("F1");
        sAbsEnergy = valAbsEnergy.ToString();
        sAbsChemical = valAbsChemical.ToString();
        sResEnergy = valResEnergy.ToString("F1");
        sResChemical = valResChemical.ToString("F1");

        // Initial calculation
        Recalculate();
    }

    void Update()
    {
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
        if (Keyboard.current != null)
        {
            var kp = Keyboard.current.oKey;
            if (kp != null && kp.wasPressedThisFrame) windowOpen = !windowOpen;
        }
#else
        if (Input.GetKeyDown(toggleKey)) windowOpen = !windowOpen;
#endif
        ClampWindowToScreen();
    }

    void OnGUI()
    {
        if (!windowOpen) return;

        float maxW = Mathf.Min(Screen.width - 20, 1000);
        float maxH = Mathf.Min(Screen.height - 20, 1200);
        windowRect.width = Mathf.Min(windowRect.width, maxW);
        windowRect.height = Mathf.Min(windowRect.height, maxH);

        windowRect = GUI.Window(12345, windowRect, WindowFunc, "Smelting (Alloy calculator)");
    }

    void WindowFunc(int id)
    {
        GUILayout.BeginVertical();

        scroll = GUILayout.BeginScrollView(scroll, GUILayout.Height(Mathf.Min(480, Screen.height - 180)));

        // Header with basic inputs
        GUILayout.Label("Input materials and parameters:");

        GUILayout.BeginHorizontal();
        GUILayout.Label("Volume (liters):", GUILayout.Width(140));
        GUI.SetNextControlName("volField");
        sVolumeLiters = GUILayout.TextField(sVolumeLiters, GUILayout.Width(200));
        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();
        GUILayout.Label("Metal (kg):", GUILayout.Width(140));
        GUI.SetNextControlName("metalField");
        sMetalKg = GUILayout.TextField(sMetalKg, GUILayout.Width(200));
        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();
        GUILayout.Label("Metal tier (1..10):", GUILayout.Width(140));
        metalTier = (int)GUILayout.HorizontalSlider(metalTier, 1, 10, GUILayout.Width(240));
        GUILayout.Label(metalTier.ToString(), GUILayout.Width(40));
        GUILayout.EndHorizontal();

        // Polymers: toggle, text input (0..90), and slider alternative (0..90 step 0.1)
        GUILayout.BeginHorizontal();
        usePolymers = GUILayout.Toggle(usePolymers, "Use polymers");
        GUILayout.FlexibleSpace();
        GUILayout.Label("Polymers % (of total):", GUILayout.Width(160));
        GUI.SetNextControlName("polyField");
        sPolyPercent = GUILayout.TextField(sPolyPercent, GUILayout.Width(60));
        GUILayout.EndHorizontal();

        // Slider for poly percent (0..90 step 0.1)
        GUILayout.BeginHorizontal();
        GUILayout.Label("Poly % slider:", GUILayout.Width(140));
        float prevSlider = sliderPolyPercent;
        sliderPolyPercent = GUILayout.HorizontalSlider(sliderPolyPercent, 0f, MAX_POLY_PERCENT, GUILayout.Width(260));
        // Snap to 0.1
        sliderPolyPercent = Mathf.Round(sliderPolyPercent * 10f) / 10f;
        GUILayout.Label(sliderPolyPercent.ToString("F1") + "%", GUILayout.Width(60));
        GUILayout.EndHorizontal();

        // Two-way sync: if user changed text, update slider immediately (visual). If user moved slider, update text.
        // Determine whether text is a valid float
        bool textValid = float.TryParse(sPolyPercent, out float parsedPolyText);
        if (textValid)
        {
            if (parsedPolyText > MAX_POLY_PERCENT)
            {
                parsedPolyText = MAX_POLY_PERCENT;
                sPolyPercent = parsedPolyText.ToString("F1");
            }
            // if text changed by user (not equal to slider), set slider to text
            if (!Mathf.Approximately(parsedPolyText, sliderPolyPercent))
            {
                sliderPolyPercent = Mathf.Clamp(parsedPolyText, 0f, MAX_POLY_PERCENT);
            }
            // ensure sPolyPercent formatted
            sPolyPercent = sliderPolyPercent.ToString("F1");
        }
        else
        {
            // text invalid -> reflect slider to text
            sPolyPercent = sliderPolyPercent.ToString("F1");
        }

        GUILayout.BeginHorizontal();
        useNanites = GUILayout.Toggle(useNanites, "Use nanites (allow negative resistances)");
        GUILayout.EndHorizontal();

        GUILayout.Space(8);

        // Metal properties header + metal points free in same row
        GUILayout.BeginHorizontal();
        GUILayout.Label("Metal properties (points from metal):", GUILayout.Width(380));
        GUILayout.FlexibleSpace();
        GUILayout.Label(string.Format("Free: {0}/{1}", metalPointsFree, metalPointsTotal), GUILayout.Width(140));
        GUILayout.EndHorizontal();

        // Abs Kinetic + buttons, Abs Thermal + buttons
        GUILayout.BeginHorizontal();
        GUILayout.Label("Abs Kinetic:", GUILayout.Width(140));
        if (GUILayout.Button("-", GUILayout.Width(24))) { TryAdjustAbs(ref valAbsKinetic, -1, ref metalPointsFree); sAbsKinetic = valAbsKinetic.ToString(); }
        GUI.SetNextControlName("absKField");
        string parsedAbsKText = sAbsKinetic;
        sAbsKinetic = GUILayout.TextField(sAbsKinetic, GUILayout.Width(80));
        if (sAbsKinetic != parsedAbsKText)
        {
            int parsedAbsK = (int)Math.Max(0, ParseDoubleSafe(sAbsKinetic, valAbsKinetic));
            int delta = parsedAbsK - valAbsKinetic;
            if (delta > 0)
            {
                int allowed = Mathf.Min(delta, metalPointsFree);
                valAbsKinetic += allowed;
                metalPointsFree -= allowed;
            }
            else if (delta < 0)
            {
                valAbsKinetic += delta;
                metalPointsFree -= delta;
                if (valAbsKinetic < 0) valAbsKinetic = 0;
            }
            sAbsKinetic = valAbsKinetic.ToString();
        }
        if (GUILayout.Button("+", GUILayout.Width(24))) { TryAdjustAbs(ref valAbsKinetic, +1, ref metalPointsFree); sAbsKinetic = valAbsKinetic.ToString(); }
        GUILayout.Label("Abs Thermal:", GUILayout.Width(100));
        if (GUILayout.Button("-", GUILayout.Width(24))) { TryAdjustAbs(ref valAbsThermal, -1, ref metalPointsFree); sAbsThermal = valAbsThermal.ToString(); }
        GUI.SetNextControlName("absTField");
        string parsedAbsTText = sAbsThermal;
        sAbsThermal = GUILayout.TextField(sAbsThermal, GUILayout.Width(80));
        if (sAbsThermal != parsedAbsTText)
        {
            int parsedAbsT = (int)Math.Max(0, ParseDoubleSafe(sAbsThermal, valAbsThermal));
            int delta = parsedAbsT - valAbsThermal;
            if (delta > 0)
            {
                int allowed = Mathf.Min(delta, metalPointsFree);
                valAbsThermal += allowed;
                metalPointsFree -= allowed;
            }
            else if (delta < 0)
            {
                valAbsThermal += delta;
                metalPointsFree -= delta;
                if (valAbsThermal < 0) valAbsThermal = 0;
            }
            sAbsThermal = valAbsThermal.ToString();
        }
        if (GUILayout.Button("+", GUILayout.Width(24))) { TryAdjustAbs(ref valAbsThermal, +1, ref metalPointsFree); sAbsThermal = valAbsThermal.ToString(); }
        GUILayout.EndHorizontal();

        // Res Kinetic + buttons, Res Thermal + buttons
        GUILayout.BeginHorizontal();
        GUILayout.Label("Res Kinetic %:", GUILayout.Width(140));
        if (GUILayout.Button("-", GUILayout.Width(24))) { TryAdjustRes(ref valResKinetic, -0.1f, ref metalPointsFree); sResKinetic = valResKinetic.ToString("F1"); }
        GUI.SetNextControlName("resKField");
        string parsedResKText = sResKinetic;
        sResKinetic = GUILayout.TextField(sResKinetic, GUILayout.Width(80));
        if (sResKinetic != parsedResKText)
        {
            float parsedResK = (float)ParseDoubleSafe(sResKinetic, valResKinetic);
            float delta = parsedResK - valResKinetic;
            AttemptSetRes(ref valResKinetic, delta, ref metalPointsFree);
            sResKinetic = valResKinetic.ToString("F1");
        }
        if (GUILayout.Button("+", GUILayout.Width(24))) { TryAdjustRes(ref valResKinetic, +0.1f, ref metalPointsFree); sResKinetic = valResKinetic.ToString("F1"); }
        GUILayout.Label("Res Thermal %:", GUILayout.Width(100));
        if (GUILayout.Button("-", GUILayout.Width(24))) { TryAdjustRes(ref valResThermal, -0.1f, ref metalPointsFree); sResThermal = valResThermal.ToString("F1"); }
        GUI.SetNextControlName("resTField");
        string parsedResTText = sResThermal;
        sResThermal = GUILayout.TextField(sResThermal, GUILayout.Width(80));
        if (sResThermal != parsedResTText)
        {
            float parsedResT = (float)ParseDoubleSafe(sResThermal, valResThermal);
            float delta = parsedResT - valResThermal;
            AttemptSetRes(ref valResThermal, delta, ref metalPointsFree);
            sResThermal = valResThermal.ToString("F1");
        }
        if (GUILayout.Button("+", GUILayout.Width(24))) { TryAdjustRes(ref valResThermal, +0.1f, ref metalPointsFree); sResThermal = valResThermal.ToString("F1"); }
        GUILayout.EndHorizontal();

        GUILayout.Space(6);

        // Polymer properties header + poly points free in same row
        GUILayout.BeginHorizontal();
        GUILayout.Label("Polymer properties (points from polymers):", GUILayout.Width(380));
        GUILayout.FlexibleSpace();
        GUILayout.Label(string.Format("Free: {0}/{1}", polyPointsFree, polyPointsTotal), GUILayout.Width(140));
        GUILayout.EndHorizontal();

        // Abs Energy + buttons, Abs Chemical + buttons
        GUILayout.BeginHorizontal();
        GUILayout.Label("Abs Energy:", GUILayout.Width(140));
        if (GUILayout.Button("-", GUILayout.Width(24))) { TryAdjustAbs(ref valAbsEnergy, -1, ref polyPointsFree); sAbsEnergy = valAbsEnergy.ToString(); }
        GUI.SetNextControlName("absEField");
        string parsedAbsEText = sAbsEnergy;
        sAbsEnergy = GUILayout.TextField(sAbsEnergy, GUILayout.Width(80));
        if (sAbsEnergy != parsedAbsEText)
        {
            int parsedAbsE = (int)Math.Max(0, ParseDoubleSafe(sAbsEnergy, valAbsEnergy));
            int delta = parsedAbsE - valAbsEnergy;
            if (delta > 0)
            {
                int allowed = Mathf.Min(delta, polyPointsFree);
                valAbsEnergy += allowed;
                polyPointsFree -= allowed;
            }
            else if (delta < 0)
            {
                valAbsEnergy += delta;
                polyPointsFree -= delta;
                if (valAbsEnergy < 0) valAbsEnergy = 0;
            }
            sAbsEnergy = valAbsEnergy.ToString();
        }
        if (GUILayout.Button("+", GUILayout.Width(24))) { TryAdjustAbs(ref valAbsEnergy, +1, ref polyPointsFree); sAbsEnergy = valAbsEnergy.ToString(); }
        GUILayout.Label("Abs Chemical:", GUILayout.Width(100));
        if (GUILayout.Button("-", GUILayout.Width(24))) { TryAdjustAbs(ref valAbsChemical, -1, ref polyPointsFree); sAbsChemical = valAbsChemical.ToString(); }
        GUI.SetNextControlName("absCField");
        string parsedAbsCText = sAbsChemical;
        sAbsChemical = GUILayout.TextField(sAbsChemical, GUILayout.Width(80));
        if (sAbsChemical != parsedAbsCText)
        {
            int parsedAbsC = (int)Math.Max(0, ParseDoubleSafe(sAbsChemical, valAbsChemical));
            int delta = parsedAbsC - valAbsChemical;
            if (delta > 0)
            {
                int allowed = Mathf.Min(delta, polyPointsFree);
                valAbsChemical += allowed;
                polyPointsFree -= allowed;
            }
            else if (delta < 0)
            {
                valAbsChemical += delta;
                polyPointsFree -= delta;
                if (valAbsChemical < 0) valAbsChemical = 0;
            }
            sAbsChemical = valAbsChemical.ToString();
        }
        if (GUILayout.Button("+", GUILayout.Width(24))) { TryAdjustAbs(ref valAbsChemical, +1, ref polyPointsFree); sAbsChemical = valAbsChemical.ToString(); }
        GUILayout.EndHorizontal();

        // Res Energy + buttons, Res Chemical + buttons
        GUILayout.BeginHorizontal();
        GUILayout.Label("Res Energy %:", GUILayout.Width(140));
        if (GUILayout.Button("-", GUILayout.Width(24))) { TryAdjustRes(ref valResEnergy, -0.1f, ref polyPointsFree); sResEnergy = valResEnergy.ToString("F1"); }
        GUI.SetNextControlName("resEField");
        string parsedResEText = sResEnergy;
        sResEnergy = GUILayout.TextField(sResEnergy, GUILayout.Width(80));
        if (sResEnergy != parsedResEText)
        {
            float parsedResE = (float)ParseDoubleSafe(sResEnergy, valResEnergy);
            float delta = parsedResE - valResEnergy;
            AttemptSetRes(ref valResEnergy, delta, ref polyPointsFree);
            sResEnergy = valResEnergy.ToString("F1");
        }
        if (GUILayout.Button("+", GUILayout.Width(24))) { TryAdjustRes(ref valResEnergy, +0.1f, ref polyPointsFree); sResEnergy = valResEnergy.ToString("F1"); }
        GUILayout.Label("Res Chemical %:", GUILayout.Width(100));
        if (GUILayout.Button("-", GUILayout.Width(24))) { TryAdjustRes(ref valResChemical, -0.1f, ref polyPointsFree); sResChemical = valResChemical.ToString("F1"); }
        GUI.SetNextControlName("resCField");
        string parsedResCText = sResChemical;
        sResChemical = GUILayout.TextField(sResChemical, GUILayout.Width(80));
        if (sResChemical != parsedResCText)
        {
            float parsedResC = (float)ParseDoubleSafe(sResChemical, valResChemical);
            float delta = parsedResC - valResChemical;
            AttemptSetRes(ref valResChemical, delta, ref polyPointsFree);
            sResChemical = valResChemical.ToString("F1");
        }
        if (GUILayout.Button("+", GUILayout.Width(24))) { TryAdjustRes(ref valResChemical, +0.1f, ref polyPointsFree); sResChemical = valResChemical.ToString("F1"); }
        GUILayout.EndHorizontal();

        GUILayout.Space(8);

        // Parse inputs but do not commit until Apply or Enter
        double volParsedD = ParseDoubleSafe(sVolumeLiters, volumeLiters);
        double metalParsedD = ParseDoubleSafe(sMetalKg, metalKg);
        double polyPercentParsedD = ParseDoubleSafe(sPolyPercent, polyPercent);

        // cast to floats when needed
        float volParsed = (float)volParsedD;
        float metalParsed = (float)metalParsedD;
        float polyPercentParsed = (float)polyPercentParsedD;

        GUILayout.Label(string.Format("Parsed inputs: Volume={0:F2} L, Metal={1:F3} kg, Polymers%={2:F1}%, Tier={3}, Nanites={4}",
            volParsed, metalParsed, polyPercentParsed, metalTier, useNanites));

        GUILayout.Space(6);

        // Show calculated outputs (based on current applied numeric state)
        GUILayout.Label(string.Format("Obtained metal (kg): {0:F3}", obtainedMetalKg));
        GUILayout.Label(string.Format("Consumed metal (kg): {0:F3}", metalKg));
        GUILayout.Label(string.Format("Consumed polymers (kg): {0:F3}", consumedPolyKg));
        GUILayout.Label(string.Format("Consumed nanites (kg): {0:F3}", consumedNanitesKg));

        // Requirement 5: show maximum totals where earlier free values were
        GUILayout.Label(string.Format("Max metal points (total): {0}", metalPointsTotal));
        GUILayout.Label(string.Format("Max poly points (total): {0}", polyPointsTotal));

        GUILayout.Space(6);

        // Warnings
        if (metalPointsFree <= 0) GUILayout.Label("<color=red>No free metal points remaining</color>");
        if (polyPointsFree <= 0 && usePolymers) GUILayout.Label("<color=red>No free polymer points remaining</color>");
        if (!usePolymers && sliderPolyPercent > 0f) GUILayout.Label("<color=orange>Polymers percent set but polymers are disabled</color>");

        GUILayout.EndScrollView();

        GUILayout.Space(6);

        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Apply", GUILayout.Height(30)))
        {
            // Commit parsed inputs to internal numeric state and recalc
            volumeLiters = Mathf.Max(0f, volParsed);
            metalKg = Mathf.Max(0f, metalParsed);

            // pick polyPercent from text or slider; prefer text if valid
            float chosenPoly;
            if (float.TryParse(sPolyPercent, out float tpoly))
            {
                chosenPoly = tpoly;
            }
            else chosenPoly = sliderPolyPercent;
            chosenPoly = Mathf.Clamp(chosenPoly, 0f, MAX_POLY_PERCENT);
            polyPercent = chosenPoly;
            // reflect
            sPolyPercent = polyPercent.ToString("F1");
            sliderPolyPercent = polyPercent;

            // parse property inputs (they may have been adjusted via +/- already)
            valAbsKinetic = (int)Math.Max(0, ParseDoubleSafe(sAbsKinetic, valAbsKinetic));
            valAbsThermal = (int)Math.Max(0, ParseDoubleSafe(sAbsThermal, valAbsThermal));
            valResKinetic = (float)ParseDoubleSafe(sResKinetic, valResKinetic);
            valResThermal = (float)ParseDoubleSafe(sResThermal, valResThermal);

            valAbsEnergy = (int)Math.Max(0, ParseDoubleSafe(sAbsEnergy, valAbsEnergy));
            valAbsChemical = (int)Math.Max(0, ParseDoubleSafe(sAbsChemical, valAbsChemical));
            valResEnergy = (float)ParseDoubleSafe(sResEnergy, valResEnergy);
            valResChemical = (float)ParseDoubleSafe(sResChemical, valResChemical);

            // enforce poly% <= MAX_POLY_PERCENT
            if (polyPercent > MAX_POLY_PERCENT)
            {
                polyPercent = MAX_POLY_PERCENT;
                sPolyPercent = polyPercent.ToString("F1");
                sliderPolyPercent = polyPercent;
            }

            // Initialize totals (informational only) and free points equal totals
            int basePoints = 200 + 100 * metalTier;
            float polyKg = 0f;
            if (usePolymers)
            {
                if (Mathf.Approximately(polyPercent, 0f)) polyKg = 0f;
                else polyKg = metalKg * (polyPercent / (100f - polyPercent));
            }
            else polyKg = 0f;
            float totalMass = metalKg + polyKg;
            float metalShare = totalMass > 0f ? metalKg / totalMass : 1f;
            float polyShare = totalMass > 0f ? polyKg / totalMass : 0f;

            metalPointsTotal = Mathf.RoundToInt(basePoints * metalShare);
            polyPointsTotal = Mathf.RoundToInt(basePoints * polyShare);

            // Initialize free points equal totals (they will be consumed as user edits)
            metalPointsFree = metalPointsTotal;
            polyPointsFree = polyPointsTotal;

            Recalculate();
            // Update string fields (format nicely)
            sVolumeLiters = volumeLiters.ToString("F1");
            sMetalKg = metalKg.ToString("F3");
            sPolyPercent = polyPercent.ToString("F1");
            sAbsKinetic = valAbsKinetic.ToString();
            sAbsThermal = valAbsThermal.ToString();
            sResKinetic = valResKinetic.ToString("F1");
            sResThermal = valResThermal.ToString("F1");
            sAbsEnergy = valAbsEnergy.ToString();
            sAbsChemical = valAbsChemical.ToString();
            sResEnergy = valResEnergy.ToString("F1");
            sResChemical = valResChemical.ToString("F1");
        }

        if (GUILayout.Button("Close", GUILayout.Height(30)))
        {
            windowOpen = false;
        }
        GUILayout.EndHorizontal();

        GUILayout.Space(6);
        GUILayout.Label("Tip: press O to toggle window. Enter commits Apply.");

        GUILayout.EndVertical();

        // Commit on Enter
        if (Event.current.isKey && Event.current.keyCode == KeyCode.Return)
        {
            GUI.FocusControl(null);
            double volP = ParseDoubleSafe(sVolumeLiters, volumeLiters);
            double mP = ParseDoubleSafe(sMetalKg, metalKg);
            double polyP = ParseDoubleSafe(sPolyPercent, polyPercent);

            volumeLiters = (float)Math.Max(0.0, volP);
            metalKg = (float)Math.Max(0.0, mP);
            // if text invalid, fallback to slider
            if (!double.TryParse(sPolyPercent, out double polyParsedEnter)) polyParsedEnter = sliderPolyPercent;
            polyPercent = (float)Mathf.Clamp((float)polyParsedEnter, 0f, MAX_POLY_PERCENT);

            valAbsKinetic = (int)Math.Max(0, ParseDoubleSafe(sAbsKinetic, valAbsKinetic));
            valAbsThermal = (int)Math.Max(0, ParseDoubleSafe(sAbsThermal, valAbsThermal));
            valResKinetic = (float)ParseDoubleSafe(sResKinetic, valResKinetic);
            valResThermal = (float)ParseDoubleSafe(sResThermal, valResThermal);
            valAbsEnergy = (int)Math.Max(0, ParseDoubleSafe(sAbsEnergy, valAbsEnergy));
            valAbsChemical = (int)Math.Max(0, ParseDoubleSafe(sAbsChemical, valAbsChemical));
            valResEnergy = (float)ParseDoubleSafe(sResEnergy, valResEnergy);
            valResChemical = (float)ParseDoubleSafe(sResChemical, valResChemical);

            // enforce poly% clamp
            if (polyPercent > MAX_POLY_PERCENT) polyPercent = MAX_POLY_PERCENT;

            // Initialize totals (informational only) and free points equal totals
            int basePointsEnter = 200 + 100 * metalTier;
            float polyKgEnter = 0f;
            if (usePolymers)
            {
                if (Mathf.Approximately(polyPercent, 0f)) polyKgEnter = 0f;
                else polyKgEnter = metalKg * (polyPercent / (100f - polyPercent));
            }
            else polyKgEnter = 0f;
            float totalMassEnter = metalKg + polyKgEnter;
            float metalShareEnter = totalMassEnter > 0f ? metalKg / totalMassEnter : 1f;
            float polyShareEnter = totalMassEnter > 0f ? polyKgEnter / totalMassEnter : 0f;

            metalPointsTotal = Mathf.RoundToInt(basePointsEnter * metalShareEnter);
            polyPointsTotal = Mathf.RoundToInt(basePointsEnter * polyShareEnter);

            metalPointsFree = metalPointsTotal;
            polyPointsFree = polyPointsTotal;

            Recalculate();

            // reflect nicely
            sVolumeLiters = volumeLiters.ToString("F1");
            sMetalKg = metalKg.ToString("F3");
            sPolyPercent = polyPercent.ToString("F1");
            sAbsKinetic = valAbsKinetic.ToString();
            sAbsThermal = valAbsThermal.ToString();
            sResKinetic = valResKinetic.ToString("F1");
            sResThermal = valResThermal.ToString("F1");
            sAbsEnergy = valAbsEnergy.ToString();
            sAbsChemical = valAbsChemical.ToString();
            sResEnergy = valResEnergy.ToString("F1");
            sResChemical = valResChemical.ToString("F1");

            Event.current.Use();
        }

        GUI.DragWindow(new Rect(0, 0, 10000, 20));
    }

    // Recalculate based on current internal numeric state
    private void Recalculate()
    {
        // compute polymer kg from metal and polyPercent if enabled
        float polyKg = 0f;
        if (usePolymers)
        {
            if (Mathf.Approximately(polyPercent, 0f)) polyKg = 0f;
            else polyKg = metalKg * (polyPercent / (100f - polyPercent));
        }
        else polyKg = 0f;

        consumedPolyKg = polyKg;

        // Nanites consumption = 1% of sum of metal and polymers
        consumedNanitesKg = 0.01f * (metalKg + polyKg);

        // base points
        int basePoints = 200 + 100 * metalTier;

        float totalMass = metalKg + polyKg;
        float metalShare = totalMass > 0f ? metalKg / totalMass : 1f;
        float polyShare = totalMass > 0f ? polyKg / totalMass : 0f;

        // Totals are informational only (already set on Apply/Enter) and not used as caps.
        // Keep them in sync visually if desired (they were set on Apply/Enter).
        // Do not modify metalPointsFree/polyPointsFree here — they are managed by adjustments.

        // Enforce resistance bounds (max positive and min negative)
        float minResAllowed = useNanites ? MAX_NEGATIVE_RESISTANCE : 0f;
        float maxRes = 45f + 5f * metalTier;

        valResKinetic = Mathf.Clamp(valResKinetic, minResAllowed, maxRes);
        valResThermal = Mathf.Clamp(valResThermal, minResAllowed, maxRes);
        valResEnergy = Mathf.Clamp(valResEnergy, minResAllowed, maxRes);
        valResChemical = Mathf.Clamp(valResChemical, minResAllowed, maxRes);

        // Abs values non-negative
        valAbsKinetic = Mathf.Max(0, valAbsKinetic);
        valAbsThermal = Mathf.Max(0, valAbsThermal);
        valAbsEnergy = Mathf.Max(0, valAbsEnergy);
        valAbsChemical = Mathf.Max(0, valAbsChemical);

        obtainedMetalKg = 0.5f * metalKg + 0.25f * polyKg;
    }

    // Helpers to adjust values respecting free points (abs = integer points; res = steps of 0.1% = 0.1 -> 1 point)
    private void TryAdjustAbs(ref int val, int delta, ref int freePoints)
    {
        if (delta == 0) return;
        if (delta > 0)
        {
            int allowed = Mathf.Min(delta, freePoints);
            val += allowed;
            freePoints -= allowed;
        }
        else
        {
            // freeing points
            int remove = -delta;
            int actualRemove = Mathf.Min(remove, val); // don't go below zero
            val -= actualRemove;
            freePoints += actualRemove;
        }
    }

    private void TryAdjustRes(ref float val, float delta, ref int freePoints)
    {
        if (Mathf.Approximately(delta, 0f)) return;
        // Each 0.1% step costs 1 point for positive increments.
        int steps = Mathf.RoundToInt(Mathf.Abs(delta) / 0.1f);
        if (delta > 0f)
        {
            int allowedSteps = Mathf.Min(steps, freePoints);
            float applied = allowedSteps * 0.1f;
            val += applied;
            freePoints -= allowedSteps;
        }
        else
        {
            int allowedSteps = steps;
            float applied = allowedSteps * 0.1f;
            val -= applied;
            freePoints += allowedSteps;
        }
    }

    // Attempt to set a resistance value by delta (from textual input). When setting by text, delta may be large;
    // apply only up to available free points for positive increases; for decreases, free points are returned.
    private void AttemptSetRes(ref float currentVal, float delta, ref int freePoints)
    {
        if (Mathf.Approximately(delta, 0f)) return;
        int steps = Mathf.RoundToInt(Mathf.Abs(delta) / 0.1f);
        if (delta > 0f)
        {
            int allowed = Mathf.Min(steps, freePoints);
            currentVal += allowed * 0.1f;
            freePoints -= allowed;
        }
        else
        {
            int allow = steps;
            currentVal -= allow * 0.1f;
            freePoints += allow;
        }
    }

    static double ParseDoubleSafe(string s, double fallback)
    {
        if (string.IsNullOrEmpty(s)) return fallback;
        if (double.TryParse(s, out double v)) return v;
        return fallback;
    }

    void ClampWindowToScreen()
    {
        float w = windowRect.width;
        float h = windowRect.height;
        windowRect.x = Mathf.Clamp(windowRect.x, 0, Mathf.Max(0, Screen.width - w));
        windowRect.y = Mathf.Clamp(windowRect.y, 0, Mathf.Max(0, Screen.height - h));
    }
}