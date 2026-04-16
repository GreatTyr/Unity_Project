using System;
using UnityEngine;

/// <summary>
/// Скейлер для бронеплит с независимым масштабированием по осям.
/// В отличие от ModuleScaler, позволяет растягивать модель по отдельным осям.
/// </summary>
[Serializable]
public class ArmorPlateScaler
{
    public enum ScaleMode
    {
        ByLength = 0,
        ByWidth = 1,
        ByHeight = 2,
        ByMass = 3,
        ByVolume = 4
    }

    // Reference (эталонные параметры)
    public float RefLength { get; private set; }
    public float RefWidth { get; private set; }
    public float RefHeight { get; private set; }
    public float RefVolume { get; private set; }
    public MeshFilter RefMeshFilter { get; private set; }

    // Current scale factors (независимые по осям)
    public float ScaleX { get; private set; } = 1f;
    public float ScaleY { get; private set; } = 1f;
    public float ScaleZ { get; private set; } = 1f;

    // Current mode
    public ScaleMode CurrentScaleMode { get; private set; } = ScaleMode.ByLength;

    // Input strings
    public string ScaleInputStr { get; private set; } = "";

    // Alloy tier
    public int CurrentAlloyTier { get; private set; } = 1;

    // Mass coefficient
    public float MassCoefficient { get; private set; } = 1f;

    // Durability coefficient
    public float DurabilityCoefficient { get; private set; } = 1f;

    // Wall thickness coefficient
    public float WallThicknessCoefficient { get; private set; } = 1f;

    // Calculated
    public float CalcLength { get; private set; }
    public float CalcWidth { get; private set; }
    public float CalcHeight { get; private set; }
    public float CalcVolume { get; private set; }
    public float CalcMass { get; private set; }
    public float CalcDurability { get; private set; }
    public float CalcWallThicknessMm { get; private set; }

    public void SetReference(float length, float width, float height, float volume, MeshFilter meshFilter)
    {
        RefLength = Mathf.Max(0.001f, length);
        RefWidth = Mathf.Max(0.001f, width);
        RefHeight = Mathf.Max(0.001f, height);
        RefVolume = Mathf.Max(0.000001f, volume);
        RefMeshFilter = meshFilter;

        ScaleX = 1f;
        ScaleY = 1f;
        ScaleZ = 1f;
    }

    public void SetScaleMode(ScaleMode mode)
    {
        CurrentScaleMode = mode;
        ScaleInputStr = GetCurrentInputValue();
    }

    public void SetAlloyTier(int tier)
    {
        CurrentAlloyTier = Mathf.Clamp(tier, 1, 10);
    }

    public void SetMassCoefficient(float coeff)
    {
        MassCoefficient = Mathf.Max(0.001f, coeff);
    }

    public void SetDurabilityCoefficient(float coeff)
    {
        DurabilityCoefficient = Mathf.Max(0.001f, coeff);
    }

    public void SetWallThicknessCoefficient(float coeff)
    {
        WallThicknessCoefficient = Mathf.Max(0.001f, coeff);
    }

    public void SetScaleX(float value)
    {
        ScaleX = Mathf.Max(0.001f, value);
    }

    public void SetScaleY(float value)
    {
        ScaleY = Mathf.Max(0.001f, value);
    }

    public void SetScaleZ(float value)
    {
        ScaleZ = Mathf.Max(0.001f, value);
    }

    public bool HandleScaleInput(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return false;

        input = input.Trim().Replace(',', '.');
        if (!float.TryParse(input, System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture, out float parsed))
            return false;

        ScaleInputStr = input;

        switch (CurrentScaleMode)
        {
            case ScaleMode.ByLength:
                if (parsed > 0f && RefLength > 0f)
                {
                    ScaleX = parsed / RefLength;
                    return true;
                }
                break;

            case ScaleMode.ByWidth:
                if (parsed > 0f && RefWidth > 0f)
                {
                    ScaleZ = parsed / RefWidth;
                    return true;
                }
                break;

            case ScaleMode.ByHeight:
                if (parsed > 0f && RefHeight > 0f)
                {
                    ScaleY = parsed / RefHeight;
                    return true;
                }
                break;

            case ScaleMode.ByMass:
                return HandleMassInput(parsed);

            case ScaleMode.ByVolume:
                return HandleVolumeInput(parsed);
        }

        return false;
    }

    private bool HandleMassInput(float targetMass)
    {
        if (targetMass <= 0f || MassCoefficient <= 0f)
            return false;

        // Текущий объем с учетом scale
        float currentVolume = CalculateCurrentVolume();
        if (currentVolume <= 0f)
            return false;

        // Текущая масса
        float currentMass = currentVolume * MassCoefficient;
        if (currentMass <= 0f)
            return false;

        // Нужный uniform scale factor
        float ratio = targetMass / currentMass;
        float uniformScale = Mathf.Pow(ratio, 1f / 3f);

        ScaleX *= uniformScale;
        ScaleY *= uniformScale;
        ScaleZ *= uniformScale;

        return true;
    }

    private bool HandleVolumeInput(float targetVolume)
    {
        if (targetVolume <= 0f)
            return false;

        float currentVolume = CalculateCurrentVolume();
        if (currentVolume <= 0f)
            return false;

        float ratio = targetVolume / currentVolume;
        float uniformScale = Mathf.Pow(ratio, 1f / 3f);

        ScaleX *= uniformScale;
        ScaleY *= uniformScale;
        ScaleZ *= uniformScale;

        return true;
    }

    public void Recalculate()
    {
        CalcLength = RefLength * ScaleX;
        CalcWidth = RefWidth * ScaleZ;
        CalcHeight = RefHeight * ScaleY;

        CalcVolume = CalculateCurrentVolume();
        CalcMass = CalcVolume * MassCoefficient;

        float tierCoeff = TierCoeffs.Get(CurrentAlloyTier);
        CalcDurability = CalcMass * DurabilityCoefficient * tierCoeff;

        float minDimension = Mathf.Min(CalcLength, CalcWidth, CalcHeight);
        CalcWallThicknessMm = minDimension * 1000f * WallThicknessCoefficient;

        ScaleInputStr = GetCurrentInputValue();
    }

    private float CalculateCurrentVolume()
    {
        if (RefMeshFilter == null || RefMeshFilter.sharedMesh == null)
        {
            return RefVolume * ScaleX * ScaleY * ScaleZ;
        }

        // Точный пересчёт объёма с учётом неравномерного scale
        Vector3 currentScale = new Vector3(ScaleX, ScaleY, ScaleZ);
        return MeshVolumeCalculator.CalculateVolumeWithRescale(RefMeshFilter.sharedMesh, currentScale);
    }

    private string GetCurrentInputValue()
    {
        switch (CurrentScaleMode)
        {
            case ScaleMode.ByLength: return CalcLength.ToString("F3");
            case ScaleMode.ByWidth: return CalcWidth.ToString("F3");
            case ScaleMode.ByHeight: return CalcHeight.ToString("F3");
            case ScaleMode.ByMass: return CalcMass.ToString("F3");
            case ScaleMode.ByVolume: return CalcVolume.ToString("F6");
            default: return "";
        }
    }
}