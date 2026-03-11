using System;
using UnityEngine;
/// <summary>
/// Логика масштабирования модуля.
/// Вынесена из ModuleWorkbench. Занимается расчетом размеров, объемов, масс, прочности и толщины стенок.
/// Используется всеми верстаками через композицию.
///
/// Толщина стенок вычисляется из кубического уравнения:
///   (X − 2t)(Y − 2t)(Z − 2t) = X·Y·Z · (1 − ShellPercent/100)
/// Решение методом бисекции. Гарантирует, что толщина t точно соответствует
/// объёмной доле оболочки ShellPercent.
/// </summary>
[Serializable]
public class ModuleScaler
{
    // ====================== Режим масштабирования ======================
    public enum ScaleMode { Length, Width, Height, Mass, EffectiveVolume }
    // ====================== Константы ======================
    private const float MIN_SHELL_PERCENT = 1f;
    private const float MAX_SHELL_PERCENT = 99f;
    private const float MIN_SCALE_FACTOR = 0.001f;
    private const float MIN_DIMENSION = 0.0001f;
    private const float DENSITY_KG_PER_M3 = 1000f;
    private const float METERS_TO_MM = 1000f;
    private const float HUNDRED = 100f;
    private const float HALF = 0.5f;
    private const int BISECTION_ITERATIONS = 64;
    // ====================== Параметры эталона (задаются при выборе) ======================
    public float RefLength { get; private set; }
    public float RefWidth { get; private set; }
    public float RefHeight { get; private set; }
    public float RefRealVolume { get; private set; }
    public float RefFillPercent { get; private set; }
    // ====================== Текущее состояние ======================
    private float _scaleFactor = 1f;
    private float _shellPercent = 5f;
    private int _alloyTier = 1;
    private ScaleMode _scaleMode = ScaleMode.Mass;
    // Строковый буфер ввода (для IMGUI — не мерцает)
    private string _scaleInputStr = "";
    private float _scaleInputValue;
    // ====================== Результаты расчетов ======================
    public float CalcLength { get; private set; }
    public float CalcWidth { get; private set; }
    public float CalcHeight { get; private set; }
    public float CalcAABBVolume { get; private set; }
    public float CalcRealVolume { get; private set; }
    public float CalcShellVolume { get; private set; }
    public float CalcEffectiveVolume { get; private set; }
    public float CalcShellMass { get; private set; }
    public float CalcInnerMass { get; private set; }
    public float CalcTotalMass { get; private set; }
    public float CalcDurability { get; private set; }
    public float CalcSurfaceArea { get; private set; }
    // ====================== Толщина стенок ======================
    /// <summary>
    /// Толщина стенок в метрах.
    /// Вычисляется из кубического уравнения: (X−2t)(Y−2t)(Z−2t) = V·(1 − Shell%/100).
    /// Гарантирует точное соответствие объёмной доле ShellPercent.
    /// </summary>
    public float CalcWallThickness { get; private set; }
    /// <summary>Толщина стенок в миллиметрах.</summary>
    public float CalcWallThicknessMm { get; private set; }
    /// <summary>Внутренняя длина полости (X − 2t).</summary>
    public float CalcInnerLength { get; private set; }
    /// <summary>Внутренняя ширина полости (Z − 2t).</summary>
    public float CalcInnerWidth { get; private set; }
    /// <summary>Внутренняя высота полости (Y − 2t).</summary>
    public float CalcInnerHeight { get; private set; }
    // ====================== Доступ к состоянию ======================
    public float CurrentScaleFactor => _scaleFactor;
    public float CurrentShellPercent => _shellPercent;
    public int CurrentAlloyTier => _alloyTier;
    public ScaleMode CurrentScaleMode => _scaleMode;
    public string ScaleInputStr => _scaleInputStr;
    // ====================== Инициализация ======================
    /// <summary>Установить параметры эталона. Сбрасывает масштаб к 1.</summary>
    public void SetReference(float len, float wid, float hei, float realVol, float fillPct)
    {
        RefLength = len;
        RefWidth = wid;
        RefHeight = hei;
        RefRealVolume = realVol;
        RefFillPercent = fillPct;
        _scaleFactor = 1f;
        Recalculate();
        UpdateScaleInputFromCurrent();
    }
    // ====================== Установка параметров ======================
    public void SetScaleFactor(float s)
    {
        _scaleFactor = Mathf.Max(MIN_SCALE_FACTOR, s);
        Recalculate();
        UpdateScaleInputFromCurrent();
    }
    public void SetShellPercent(float percent)
    {
        _shellPercent = Mathf.Clamp(percent, MIN_SHELL_PERCENT, MAX_SHELL_PERCENT);
        Recalculate();
        UpdateScaleInputFromCurrent();
    }
    public void SetAlloyTier(int tier)
    {
        _alloyTier = Mathf.Clamp(tier, 1, 10);
        Recalculate();
    }
    public void SetScaleMode(ScaleMode mode)
    {
        _scaleMode = mode;
        UpdateScaleInputFromCurrent();
    }
    // ====================== Ввод значения масштаба ======================
    /// <summary>
    /// Обработка ввода значения масштабирования из текстового поля.
    /// Возвращает true если значение изменилось.
    /// </summary>
    public bool HandleScaleInput(string newStr)
    {
        if (newStr == _scaleInputStr) return false;
        _scaleInputStr = newStr;
        if (!float.TryParse(_scaleInputStr, out float val) || val <= 0f)
            return false;
        _scaleInputValue = val;
        RecalculateFromScaleInput();
        return true;
    }
    // ====================== Обратный расчет масштаба ======================
    public void SetScaleByTotalMass(float targetMass)
    {
        float shellFrac = _shellPercent / HUNDRED;
        float fillFrac = RefFillPercent / HUNDRED;
        float massFactor = (shellFrac * DENSITY_KG_PER_M3)
                         + ((1f - shellFrac) * fillFrac * DENSITY_KG_PER_M3);
        if (massFactor <= 0f || RefRealVolume <= 0f) return;
        double s3 = (double)targetMass / ((double)RefRealVolume * massFactor);
        if (s3 > 0)
            SetScaleFactor((float)Math.Pow(s3, 1.0 / 3.0));
    }
    private void RecalculateFromScaleInput()
    {
        if (RefLength <= 0f || RefWidth <= 0f || RefHeight <= 0f) return;
        switch (_scaleMode)
        {
            case ScaleMode.Length:
                _scaleFactor = _scaleInputValue / RefLength;
                break;
            case ScaleMode.Width:
                _scaleFactor = _scaleInputValue / RefWidth;
                break;
            case ScaleMode.Height:
                _scaleFactor = _scaleInputValue / RefHeight;
                break;
            case ScaleMode.Mass:
                SetScaleByTotalMass(_scaleInputValue);
                return;
            case ScaleMode.EffectiveVolume:
                {
                    float effFactor = RefRealVolume * (1f - (_shellPercent / HUNDRED));
                    if (effFactor > 0)
                        _scaleFactor = (float)Math.Pow((double)_scaleInputValue / effFactor, 1.0 / 3.0);
                    break;
                }
        }
        _scaleFactor = Mathf.Max(MIN_SCALE_FACTOR, _scaleFactor);
        Recalculate();
    }
    public void UpdateScaleInputFromCurrent()
    {
        switch (_scaleMode)
        {
            case ScaleMode.Length: _scaleInputValue = CalcLength; break;
            case ScaleMode.Width: _scaleInputValue = CalcWidth; break;
            case ScaleMode.Height: _scaleInputValue = CalcHeight; break;
            case ScaleMode.Mass: _scaleInputValue = CalcTotalMass; break;
            case ScaleMode.EffectiveVolume: _scaleInputValue = CalcEffectiveVolume; break;
        }
        string fmt = (_scaleMode == ScaleMode.EffectiveVolume) ? "F6" : "F3";
        _scaleInputStr = _scaleInputValue.ToString(fmt);
    }
    // ====================== Основной пересчет ======================
    public void Recalculate()
    {
        float s = Mathf.Max(MIN_SCALE_FACTOR, _scaleFactor);
        float s3 = s * s * s;
        // 1. Размеры
        CalcLength = R3(RefLength * s);
        CalcWidth = R3(RefWidth * s);
        CalcHeight = R3(RefHeight * s);
        // 2. Объемы
        CalcAABBVolume = R6(CalcLength * CalcWidth * CalcHeight);
        CalcRealVolume = R6(RefRealVolume * s3);
        float shellFrac = Mathf.Clamp(_shellPercent, MIN_SHELL_PERCENT, MAX_SHELL_PERCENT) / HUNDRED;
        CalcShellVolume = R6(CalcRealVolume * shellFrac);
        CalcEffectiveVolume = R6(CalcRealVolume - CalcShellVolume);
        if (CalcEffectiveVolume < 0f) CalcEffectiveVolume = 0f;
        // 3. Массы
        CalcShellMass = R3(CalcShellVolume * DENSITY_KG_PER_M3);
        float fillFrac = RefFillPercent / HUNDRED;
        CalcInnerMass = R3(CalcEffectiveVolume * fillFrac * DENSITY_KG_PER_M3);
        CalcTotalMass = R3(CalcShellMass + CalcInnerMass);
        // 4. Прочность
        float shellVolDm3 = R3(CalcShellVolume * METERS_TO_MM);
        CalcDurability = R3(shellVolDm3 * TierCoeffs.Get(_alloyTier));
        // 5. Площадь поверхности
        float area = 2f * (CalcLength * CalcWidth + CalcLength * CalcHeight + CalcWidth * CalcHeight);
        CalcSurfaceArea = R6(area);
        // 6. Толщина стенок
        CalculateWallThickness();
    }
    // ====================== Расчёт толщины стенок ======================
    /// <summary>
    /// Вычисляет физическую толщину стенок и внутренние размеры полости.
    ///
    /// Решает кубическое уравнение методом бисекции:
    ///   (X − 2t)(Y − 2t)(Z − 2t) = X·Y·Z · (1 − ShellPercent / 100)
    ///
    /// f(t) = (X−2t)(Y−2t)(Z−2t) − targetInnerVolume
    /// f(0) = V · ShellPercent/100 > 0   (при ShellPercent > 0)
    /// f(min/2): один из множителей ≤ 0  → f(min/2) ≤ 0
    /// Корень гарантирован на интервале (0, min(X,Y,Z)/2).
    ///
    /// Точность: 64 итерации бисекции ≈ 10⁻¹⁵ от интервала.
    /// </summary>
    private void CalculateWallThickness()
    {
        float X = CalcLength;
        float Y = CalcWidth;
        float Z = CalcHeight;
        float minSide = Mathf.Min(X, Mathf.Min(Y, Z));
        // Защита от вырожденных случаев
        if (minSide <= MIN_DIMENSION || _shellPercent <= 0f)
        {
            SetWallThicknessResults(0f, X, Y, Z);
            return;
        }
        float volume = X * Y * Z;
        if (volume <= 0f)
        {
            SetWallThicknessResults(0f, X, Y, Z);
            return;
        }
        float shellFrac = Mathf.Clamp(_shellPercent, MIN_SHELL_PERCENT, MAX_SHELL_PERCENT) / HUNDRED;
        float targetInnerVolume = volume * (1f - shellFrac);
        float tMax = minSide * HALF;
        // Бисекция на интервале [0, tMax]
        float lo = 0f;
        float hi = tMax;
        for (int i = 0; i < BISECTION_ITERATIONS; i++)
        {
            float mid = (lo + hi) * HALF;
            float mid2 = mid * 2f;
            float innerVolume = (X - mid2) * (Y - mid2) * (Z - mid2);
            if (innerVolume > targetInnerVolume)
                lo = mid;
            else
                hi = mid;
        }
        float t = (lo + hi) * HALF;
        float t2 = t * 2f;
        SetWallThicknessResults(
            t,
            Mathf.Max(0f, X - t2),
            Mathf.Max(0f, Y - t2),
            Mathf.Max(0f, Z - t2)
        );
    }
    /// <summary>
    /// Записывает результаты расчёта толщины стенок с округлением.
    /// </summary>
    private void SetWallThicknessResults(float thickness, float innerX, float innerY, float innerZ)
    {
        CalcWallThickness = R3(thickness);
        CalcWallThicknessMm = R1(CalcWallThickness * METERS_TO_MM);
        CalcInnerLength = R3(innerX);
        CalcInnerWidth = R3(innerY);
        CalcInnerHeight = R3(innerZ);
    }
    // ====================== Построение ModuleScaleData ======================
    public ModuleScaleData BuildScaleData()
    {
        return new ModuleScaleData
        {
            scaleFactor = Mathf.Max(MIN_SCALE_FACTOR, _scaleFactor),
            realVolume = CalcRealVolume,
            shellVolumeM3 = CalcShellVolume,
            effectiveVolume = CalcEffectiveVolume,
            shellPercent = _shellPercent,
            fillPercent = RefFillPercent,
            shellMassKg = CalcShellMass,
            innerMassKg = CalcInnerMass,
            totalMassKg = CalcTotalMass,
            durability = CalcDurability,
            alloyTier = _alloyTier
        };
    }
    // ====================== Утилиты округления ======================
    private static float R1(float v) => (float)Math.Round(v, 1);
    private static float R3(float v) => (float)Math.Round(v, 3);
    private static float R6(float v) => (float)Math.Round(v, 6);
}
