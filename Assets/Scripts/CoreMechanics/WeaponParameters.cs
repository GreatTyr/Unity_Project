using UnityEngine;
using System;

#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
using UnityEngine.InputSystem;
#endif

// WeaponParameters — runtime debug UI for simple ballistic model.
// Shows Dmax (unified formula) and D_direct for two cases:
//  - originY = 0 (H_target = 1.8 m -> H_rel_lim = 1.8)
//  - originY = 1.5 (H_target = 1.8 m -> H_rel_lim = 0.3)
//
// Model:
//   Dmax(v,m) = A * v^2 * m^q / (1 + B * m^p)
//   with A=0.02175, B=0.3058, p=1.0, q=0.30
//   D(θ) = Dmax * sin(2θ)
//   Hmax(θ) = c * Dmax * sin^2θ   (c = 0.084)
//
// Direct-shot distance when Hmax = H_rel_lim:
//   u = H_rel_lim / (c * Dmax)
//   if 0 < u < 1: D_direct = 2 * Dmax * sqrt(u * (1 - u))
//   otherwise D_direct = 0 (no valid θ)
public class WeaponParameters : MonoBehaviour
{
    // toggle key
    public KeyCode toggleKey = KeyCode.P;
    public bool windowOpen = false;

    // defaults
    public float v = 900f;           // m/s
    public float mGrams = 3.23f;     // g (mass input is in grams)

    // UI
    private Rect windowRect = new Rect(20, 20, 460, 300);
    private Vector2 scroll = Vector2.zero;
    private string vText, mText;

    // model constants
    const double A_coef = 0.02175;   // A
    const double B_coef = 0.3058;    // B
    const double p = 1.0;            // p
    const double q = 0.30;           // q
    const double c_height = 0.084;   // relates Hmax and Dmax

    void Start()
    {
        vText = v.ToString("F2");
        mText = mGrams.ToString("F3"); // grams input
    }

    void Update()
    {
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
        if (Keyboard.current != null)
        {
            var kp = Keyboard.current.pKey;
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

        float maxW = Mathf.Min(Screen.width - 20, 800);
        float maxH = Mathf.Min(Screen.height - 20, 900);
        windowRect.width = Mathf.Min(windowRect.width, maxW);
        windowRect.height = Mathf.Min(windowRect.height, maxH);

        windowRect = GUI.Window(9999, windowRect, WindowFunc, "Weapon Parameters");
    }

    void WindowFunc(int id)
    {
        GUILayout.BeginVertical();

        scroll = GUILayout.BeginScrollView(scroll, GUILayout.Height(Mathf.Min(240, Screen.height - 140)));

        GUILayout.Label("Enter projectile parameters (mass in grams):");

        GUILayout.BeginHorizontal();
        GUILayout.Label("v (m/s):", GUILayout.Width(90));
        GUI.SetNextControlName("vField");
        vText = GUILayout.TextField(vText, GUILayout.Width(200));
        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();
        GUILayout.Label("m (g):", GUILayout.Width(90));
        GUI.SetNextControlName("mField");
        mText = GUILayout.TextField(mText, GUILayout.Width(200));
        GUILayout.EndHorizontal();

        GUILayout.Space(8);

        // Parse inputs safely (double precision internally)
        double vParsed = ParseDoubleSafe(vText, (double)v);
        double mParsedGrams = ParseDoubleSafe(mText, (double)mGrams);

        // convert to kg
        double mKg = Math.Max(0.0, mParsedGrams / 1000.0);

        // compute Dmax and D_direct values
        double Dmax = ComputeDmax(vParsed, mKg);
        double D_direct_surface = ComputeDdirect_for_Hrel(vParsed, mKg, 1.8); // originY=0 => H_rel_lim=1.8
        double D_direct_from_1p5 = ComputeDdirect_for_Hrel(vParsed, mKg, 0.3); // originY=1.5 => H_rel_lim=0.3

        GUILayout.Label(string.Format("Input: v = {0:F3} m/s, mass = {1:F6} kg ({2:F3} g)", vParsed, mKg, mParsedGrams));
        GUILayout.Label(string.Format("Model constants: A={0}, B={1}, p={2}, q={3}, c={4}", A_coef, B_coef, p, q, c_height));
        GUILayout.Space(6);

        GUILayout.Label(string.Format("Dmax = {0:F3} m", Dmax));
        GUILayout.Label(string.Format("D_direct (originY=0, H_target=1.8 m) = {0:F3} m", D_direct_surface));
        GUILayout.Label(string.Format("D_direct (originY=1.5, H_target=1.8 m) = {0:F3} m", D_direct_from_1p5));

        GUILayout.Space(6);

        // Warnings when no valid solution
        if (Dmax <= 0.0) GUILayout.Label("<color=red>Dmax is zero or invalid (check inputs)</color>");
        if (D_direct_surface <= 0.0) GUILayout.Label("<color=orange>No valid direct-shot angle for H_target=1.8 (originY=0)</color>");
        if (D_direct_from_1p5 <= 0.0) GUILayout.Label("<color=orange>No valid direct-shot angle for H_target=1.8 from originY=1.5 m</color>");

        GUILayout.EndScrollView();

        GUILayout.Space(6);

        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Apply", GUILayout.Height(30)))
        {
            // apply parsed values to public fields
            v = (float)Math.Max(0.0, vParsed);
            mGrams = (float)Math.Max(0.0, mParsedGrams);
            vText = v.ToString("F2");
            mText = mGrams.ToString("F3");
        }
        if (GUILayout.Button("Close", GUILayout.Height(30)))
        {
            windowOpen = false;
        }
        GUILayout.EndHorizontal();

        GUILayout.Space(6);
        GUILayout.Label("Tip: press P to toggle window. Enter values and press Apply.");

        GUILayout.EndVertical();

        // Commit on Enter
        if (Event.current.isKey && Event.current.keyCode == KeyCode.Return)
        {
            GUI.FocusControl(null);
            double vP = ParseDoubleSafe(vText, v);
            double mP = ParseDoubleSafe(mText, mGrams);
            v = (float)Math.Max(0.0, vP);
            mGrams = (float)Math.Max(0.0, mP);
            vText = v.ToString("F2");
            mText = mGrams.ToString("F3");
            Event.current.Use();
        }

        GUI.DragWindow(new Rect(0, 0, 10000, 20));
    }

    // ---------- Model computations ----------

    // Dmax(v,m) = A * v^2 * m^q / (1 + B * m^p)
    static double ComputeDmax(double v0, double mKg)
    {
        if (v0 <= 0.0 || mKg < 0.0) return 0.0;
        double numerator = A_coef * v0 * v0 * Math.Pow(Math.Max(mKg, 1e-12), q);
        double denom = 1.0 + B_coef * Math.Pow(Math.Max(mKg, 0.0), p);
        if (denom <= 0.0) return 0.0;
        return numerator / denom;
    }

    // Compute D_direct given relative allowed height H_rel_lim (height above origin)
    static double ComputeDdirect_for_Hrel(double v0, double mKg, double H_rel_lim)
    {
        double Dm = ComputeDmax(v0, mKg);
        if (Dm <= 0.0) return 0.0;

        double denom = c_height * Dm;
        if (denom <= 0.0) return 0.0;

        double u = H_rel_lim / denom; // u must be in (0,1)
        if (!(u > 0.0 && u < 1.0)) return 0.0;

        double sqrtTerm = Math.Sqrt(u * (1.0 - u));
        double result = 2.0 * Dm * sqrtTerm;
        return result;
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