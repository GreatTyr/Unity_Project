using System;
using UnityEngine;

/// <summary>
/// Логика масштабирования модуля.
/// Вынесена из ModuleWorkbench. Занимается расчетом размеров, объемов, масс и прочности.
/// Используется всеми верстаками через композицию.
/// Путь: Assets/Scripts/CoreMechanics/ModuleScaler.cs
/// </summary>
[Serializable]
public class ModuleScaler
{
    // ====================== Режим масштабирования ======================
    public enum ScaleMode { Length, Width, Height, Mass, EffectiveVolume }

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
        _scaleFactor = Mathf.Max(0.001f, s);
        Recalculate();
        UpdateScaleInputFromCurrent();
    }

    public void SetShellPercent(float percent)
    {
        _shellPercent = Mathf.Clamp(percent, 0.001f, 100f);
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
        float shellFrac = _shellPercent / 100f;
        float massFactor = 1000f * (shellFrac + (1f - shellFrac) * (RefFillPercent / 100f));
        if (massFactor <= 0f || RefRealVolume <= 0f) return;
        double s3 = (double)targetMass / ((double)RefRealVolume * massFactor);
        if (s3 > 0) SetScaleFactor((float)Math.Pow(s3, 1.0 / 3.0));
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
                    float effFactor = RefRealVolume * (1f - (_shellPercent / 100f));
                    if (effFactor > 0)
                        _scaleFactor = (float)Math.Pow((double)_scaleInputValue / effFactor, 1.0 / 3.0);
                    break;
                }
        }
        _scaleFactor = Mathf.Max(0.001f, _scaleFactor);
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
        float s = Mathf.Max(0.001f, _scaleFactor);
        float s3 = s * s * s;

        // 1. Размеры
        CalcLength = R3(RefLength * s);
        CalcWidth = R3(RefWidth * s);
        CalcHeight = R3(RefHeight * s);

        // 2. Объемы
        CalcAABBVolume = R6(CalcLength * CalcWidth * CalcHeight);
        CalcRealVolume = R6(RefRealVolume * s3);

        float shellFrac = Mathf.Clamp(_shellPercent, 0.001f, 100f) / 100f;
        CalcShellVolume = R6(CalcRealVolume * shellFrac);
        CalcEffectiveVolume = R6(CalcRealVolume - CalcShellVolume);
        if (CalcEffectiveVolume < 0f) CalcEffectiveVolume = 0f;

        // 3. Массы
        CalcShellMass = R3(CalcShellVolume * 1000f);
        CalcInnerMass = R3(CalcEffectiveVolume * (RefFillPercent / 100f) * 1000f);
        CalcTotalMass = R3(CalcShellMass + CalcInnerMass);

        // 4. Прочность
        float shellVolDm3 = R3(CalcShellVolume * 1000f);
        CalcDurability = R3(shellVolDm3 * TierCoeffs.Get(_alloyTier));

        // 5. Площадь поверхности
        float area = 2f * (CalcLength * CalcWidth + CalcLength * CalcHeight + CalcWidth * CalcHeight);
        CalcSurfaceArea = R6(area);
    }

    // ====================== Построение ModuleScaleData ======================

    public ModuleScaleData BuildScaleData()
    {
        return new ModuleScaleData
        {
            scaleFactor = Mathf.Max(0.001f, _scaleFactor),
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
    private static float R3(float v) => (float)Math.Round(v, 3);
    private static float R6(float v) => (float)Math.Round(v, 6);
}