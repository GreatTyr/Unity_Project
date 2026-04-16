using System;
using System.Globalization;
using UnityEngine;

/// <summary>
/// Система кодирования и декодирования чертежей бронеплит.
/// Формат: 3 строки.
/// Строка 1: базовые параметры (тип, тир, масса, габариты, фракция и т.п.)
/// Строка 2: прочностные параметры (прочность, поглощения, сопротивления, тепловые)
/// Строка 3: код сплава
/// </summary>
public static class ArmorPlateCode
{
    public struct FirstLineData
    {
        public string ModuleType;
        public int Tier;
        public float TotalMass;
        public float Durability;
        public float Length;
        public float Width;
        public float Height;
        public float ScaleX;
        public float ScaleY;
        public float ScaleZ;
        public string Faction;
        public int BlueprintId;
        public bool HasScaleFactors;
    }

    public struct SecondLineData
    {
        public float Durability;
        public int KineticAbsorption;
        public int ThermalAbsorption;
        public int ChemicalAbsorption;
        public int EnergyAbsorption;
        public float KineticResistance;
        public float ThermalResistance;
        public float ChemicalResistance;
        public float EnergyResistance;
        public float HeatCapacity;
        public float MaxTemperature;
        public float HeatingRate;
        public float WallThicknessMm;
    }

    public struct FullCodeData
    {
        public FirstLineData FirstLine;
        public SecondLineData SecondLine;
        public string AlloyCode;
        public string ErrorMessage;
    }

    private const char SEP = '|';

    // =========================================
    // BUILD
    // =========================================

    public static string BuildFirstLine(
        string moduleType,
        int tier,
        float totalMass,
        float durability,
        float length,
        float width,
        float height,
        float scaleX,
        float scaleY,
        float scaleZ,
        string faction,
        int blueprintId)
    {
        return string.Format(CultureInfo.InvariantCulture,
            "{0}{1}{2}{1}{3:F1}{1}{4:F1}{1}{5:F3}{1}{6:F3}{1}{7:F3}{1}{8:F4}{1}{9:F4}{1}{10:F4}{1}{11}{1}{12}",
            moduleType, SEP, tier, totalMass, durability, length, width, height, scaleX, scaleY, scaleZ, faction, blueprintId);
    }

    public static string BuildSecondLine(
        float durability,
        int kineticAbs, int thermalAbs, int chemicalAbs, int energyAbs,
        float kineticRes, float thermalRes, float chemicalRes, float energyRes,
        float heatCapacity, float maxTemp, float heatingRate, float wallThicknessMm)
    {
        return string.Format(CultureInfo.InvariantCulture,
            "{0:F1}{1}{2}{1}{3}{1}{4}{1}{5}{1}{6:F1}{1}{7:F1}{1}{8:F1}{1}{9:F1}{1}{10:F1}{1}{11:F1}{1}{12:F2}{1}{13:F1}",
            durability, SEP,
            kineticAbs, thermalAbs, chemicalAbs, energyAbs,
            kineticRes, thermalRes, chemicalRes, energyRes,
            heatCapacity, maxTemp, heatingRate, wallThicknessMm);
    }

    public static string BuildFullCode(string firstLine, string secondLine, string alloyCode)
    {
        return $"{firstLine}\n{secondLine}\n{alloyCode}";
    }

    // =========================================
    // PARSE
    // =========================================

    public static bool TryParseFirstLine(string line, out FirstLineData data)
    {
        data = default;
        if (string.IsNullOrWhiteSpace(line))
            return false;

        string[] parts = line.Split(SEP);
        if (parts.Length < 13)
            return false;

        try
        {
            data.ModuleType = parts[0].Trim();
            data.Tier = int.Parse(parts[1], CultureInfo.InvariantCulture);
            data.TotalMass = float.Parse(parts[2], CultureInfo.InvariantCulture);
            data.Durability = float.Parse(parts[3], CultureInfo.InvariantCulture);
            data.Length = float.Parse(parts[4], CultureInfo.InvariantCulture);
            data.Width = float.Parse(parts[5], CultureInfo.InvariantCulture);
            data.Height = float.Parse(parts[6], CultureInfo.InvariantCulture);
            data.ScaleX = float.Parse(parts[7], CultureInfo.InvariantCulture);
            data.ScaleY = float.Parse(parts[8], CultureInfo.InvariantCulture);
            data.ScaleZ = float.Parse(parts[9], CultureInfo.InvariantCulture);
            data.Faction = parts[10].Trim();
            data.BlueprintId = int.Parse(parts[11], CultureInfo.InvariantCulture);
            data.HasScaleFactors = true;

            return true;
        }
        catch
        {
            return false;
        }
    }

    public static bool TryParseSecondLine(string line, out SecondLineData data)
    {
        data = default;
        if (string.IsNullOrWhiteSpace(line))
            return false;

        string[] parts = line.Split(SEP);
        if (parts.Length < 13)
            return false;

        try
        {
            data.Durability = float.Parse(parts[0], CultureInfo.InvariantCulture);
            data.KineticAbsorption = int.Parse(parts[1], CultureInfo.InvariantCulture);
            data.ThermalAbsorption = int.Parse(parts[2], CultureInfo.InvariantCulture);
            data.ChemicalAbsorption = int.Parse(parts[3], CultureInfo.InvariantCulture);
            data.EnergyAbsorption = int.Parse(parts[4], CultureInfo.InvariantCulture);
            data.KineticResistance = float.Parse(parts[5], CultureInfo.InvariantCulture);
            data.ThermalResistance = float.Parse(parts[6], CultureInfo.InvariantCulture);
            data.ChemicalResistance = float.Parse(parts[7], CultureInfo.InvariantCulture);
            data.EnergyResistance = float.Parse(parts[8], CultureInfo.InvariantCulture);
            data.HeatCapacity = float.Parse(parts[9], CultureInfo.InvariantCulture);
            data.MaxTemperature = float.Parse(parts[10], CultureInfo.InvariantCulture);
            data.HeatingRate = float.Parse(parts[11], CultureInfo.InvariantCulture);
            data.WallThicknessMm = float.Parse(parts[12], CultureInfo.InvariantCulture);

            return true;
        }
        catch
        {
            return false;
        }
    }

    public static bool TryParseFullCode(string fullCode, out FullCodeData parsed)
    {
        parsed = new FullCodeData { ErrorMessage = "" };

        if (string.IsNullOrWhiteSpace(fullCode))
        {
            parsed.ErrorMessage = "Код пустой.";
            return false;
        }

        string[] lines = fullCode.Split('\n');
        if (lines.Length < 3)
        {
            parsed.ErrorMessage = "Код должен содержать 3 строки.";
            return false;
        }

        if (!TryParseFirstLine(lines[0], out parsed.FirstLine))
        {
            parsed.ErrorMessage = "Не удалось разобрать первую строку кода.";
            return false;
        }

        if (!TryParseSecondLine(lines[1], out parsed.SecondLine))
        {
            parsed.ErrorMessage = "Не удалось разобрать вторую строку кода.";
            return false;
        }

        parsed.AlloyCode = lines[2].Trim();
        return true;
    }

    // =========================================
    // NORMALIZE
    // =========================================

    public static string NormalizeCodeText(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return string.Empty;

        return input.Trim().Replace("\r\n", "\n").Replace("\r", "\n");
    }

    public static bool TryNormalizeFullCode(string input, out string normalized, out string error)
    {
        normalized = string.Empty;
        error = string.Empty;

        string clean = NormalizeCodeText(input);
        if (!TryParseFullCode(clean, out var parsed))
        {
            error = parsed.ErrorMessage;
            return false;
        }

        string line1 = BuildFirstLine(
            parsed.FirstLine.ModuleType,
            parsed.FirstLine.Tier,
            parsed.FirstLine.TotalMass,
            parsed.FirstLine.Durability,
            parsed.FirstLine.Length,
            parsed.FirstLine.Width,
            parsed.FirstLine.Height,
            parsed.FirstLine.ScaleX,
            parsed.FirstLine.ScaleY,
            parsed.FirstLine.ScaleZ,
            parsed.FirstLine.Faction,
            parsed.FirstLine.BlueprintId);

        string line2 = BuildSecondLine(
            parsed.SecondLine.Durability,
            parsed.SecondLine.KineticAbsorption,
            parsed.SecondLine.ThermalAbsorption,
            parsed.SecondLine.ChemicalAbsorption,
            parsed.SecondLine.EnergyAbsorption,
            parsed.SecondLine.KineticResistance,
            parsed.SecondLine.ThermalResistance,
            parsed.SecondLine.ChemicalResistance,
            parsed.SecondLine.EnergyResistance,
            parsed.SecondLine.HeatCapacity,
            parsed.SecondLine.MaxTemperature,
            parsed.SecondLine.HeatingRate,
            parsed.SecondLine.WallThicknessMm);

        normalized = BuildFullCode(line1, line2, parsed.AlloyCode);
        return true;
    }

    // =========================================
    // COMPARISON
    // =========================================

    public static bool AreFirstLinesEquivalent(string line1, string line2)
    {
        if (!TryParseFirstLine(line1, out var d1) || !TryParseFirstLine(line2, out var d2))
            return false;

        return d1.ModuleType == d2.ModuleType &&
               d1.Tier == d2.Tier &&
               d1.Faction == d2.Faction &&
               d1.BlueprintId == d2.BlueprintId &&
               Mathf.Approximately(d1.TotalMass, d2.TotalMass) &&
               Mathf.Approximately(d1.Durability, d2.Durability) &&
               Mathf.Approximately(d1.Length, d2.Length) &&
               Mathf.Approximately(d1.Width, d2.Width) &&
               Mathf.Approximately(d1.Height, d2.Height) &&
               Mathf.Approximately(d1.ScaleX, d2.ScaleX) &&
               Mathf.Approximately(d1.ScaleY, d2.ScaleY) &&
               Mathf.Approximately(d1.ScaleZ, d2.ScaleZ);
    }

    public static bool AreSecondLinesEquivalent(string line1, string line2)
    {
        if (!TryParseSecondLine(line1, out var d1) || !TryParseSecondLine(line2, out var d2))
            return false;

        return Mathf.Approximately(d1.Durability, d2.Durability) &&
               d1.KineticAbsorption == d2.KineticAbsorption &&
               d1.ThermalAbsorption == d2.ThermalAbsorption &&
               d1.ChemicalAbsorption == d2.ChemicalAbsorption &&
               d1.EnergyAbsorption == d2.EnergyAbsorption &&
               Mathf.Approximately(d1.KineticResistance, d2.KineticResistance) &&
               Mathf.Approximately(d1.ThermalResistance, d2.ThermalResistance) &&
               Mathf.Approximately(d1.ChemicalResistance, d2.ChemicalResistance) &&
               Mathf.Approximately(d1.EnergyResistance, d2.EnergyResistance) &&
               Mathf.Approximately(d1.HeatCapacity, d2.HeatCapacity) &&
               Mathf.Approximately(d1.MaxTemperature, d2.MaxTemperature) &&
               Mathf.Approximately(d1.HeatingRate, d2.HeatingRate) &&
               Mathf.Approximately(d1.WallThicknessMm, d2.WallThicknessMm);
    }
}