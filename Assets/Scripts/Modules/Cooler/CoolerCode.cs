using System;
using System.Globalization;

/// <summary>
/// Полный кодек строкового представления кулера.
/// Поддерживает новый токен sf (scaleFactor) в первой строке.
/// Старые коды без sf тоже поддерживаются.
/// </summary>
public static class CoolerCode
{
    public struct ParsedFirstLine
    {
        public bool IsValid;
        public string ErrorMessage;

        public string ModuleType;
        public int Tier;
        public float TotalMassKg;
        public float Durability;
        public float Length;
        public float Width;
        public float Height;
        public float ShellPercent;
        public string Faction;
        public string BlueprintId;

        public bool HasScaleFactor;
        public float ScaleFactor;
    }

    public struct ParsedSecondLine
    {
        public bool IsValid;
        public string ErrorMessage;

        public float CoolingPower;
        public float CoolingRadius;
        public float EnergyConsumption;
    }

    public struct ParsedFullCode
    {
        public bool IsValid;
        public string ErrorMessage;

        public ParsedFirstLine FirstLine;
        public ParsedSecondLine SecondLine;
        public string AlloyCode;
    }

    private const float FirstLineMassTolerance = 0.015f;
    private const float FirstLineDurabilityTolerance = 0.15f;
    private const float FirstLineDimTolerance = 0.0015f;
    private const float FirstLineShellTolerance = 0.001f;
    private const float FirstLineScaleTolerance = 0.000001f;

    private const float SecondLineTolerance = 0.0005f;

    public static string BuildFirstLine(
        string moduleType,
        int tier,
        float totalMassKg,
        float durability,
        float length,
        float width,
        float height,
        float shellPercent,
        float scaleFactor,
        string faction,
        string blueprintId)
    {
        return $"{moduleType}-T{tier}" +
               $"-m{Format3(totalMassKg)}" +
               $"-d{Format3(durability)}" +
               $"-{Format3(length)}/{Format3(width)}/{Format3(height)}" +
               $"-sp{Format3(shellPercent)}" +
               $"-sf{Format6(scaleFactor)}" +
               $"-{(string.IsNullOrEmpty(faction) ? "NONE" : faction)}-{blueprintId}";
    }

    public static string BuildSecondLine(
        float coolingPower,
        float coolingRadius,
        float energyConsumption)
    {
        return $"C{Format3(coolingPower)}-R{Format3(coolingRadius)}-E{Format3(energyConsumption)}";
    }

    public static string BuildFullCode(
        string firstLine,
        string secondLine,
        string alloyCode)
    {
        return $"{NormalizeCodeText(firstLine)}\n{NormalizeCodeText(secondLine)}\n{NormalizeCodeText(alloyCode)}";
    }

    public static bool TryParseFirstLine(string line, out ParsedFirstLine parsed)
    {
        parsed = default;

        if (string.IsNullOrWhiteSpace(line))
        {
            parsed.ErrorMessage = "Пустая первая строка кода кулера.";
            return false;
        }

        string normalized = NormalizeCodeText(line);
        string firstLine = normalized.Split('\n')[0].Trim();
        string[] parts = firstLine.Split('-');

        if (parts.Length < 7)
        {
            parsed.ErrorMessage = "Неверная первая строка кода кулера.";
            return false;
        }

        parsed.ModuleType = parts[0];

        string tierRaw = parts[1].Replace("T", "");
        if (!int.TryParse(tierRaw, NumberStyles.Integer, CultureInfo.InvariantCulture, out parsed.Tier))
        {
            parsed.ErrorMessage = "Не удалось прочитать тир кулера.";
            return false;
        }

        if (!TryParsePrefixedFloat(parts[2], 'm', out parsed.TotalMassKg))
        {
            parsed.ErrorMessage = "Не удалось прочитать массу кулера.";
            return false;
        }

        if (!TryParsePrefixedFloat(parts[3], 'd', out parsed.Durability))
        {
            parsed.ErrorMessage = "Не удалось прочитать прочность кулера.";
            return false;
        }

        if (!TryParseDimensions(parts[4], out parsed.Length, out parsed.Width, out parsed.Height))
        {
            parsed.ErrorMessage = "Не удалось прочитать размеры кулера.";
            return false;
        }

        if (!TryExtractShellPercent(parts, out parsed.ShellPercent))
        {
            parsed.ErrorMessage = "Не удалось прочитать shell percent кулера.";
            return false;
        }

        parsed.HasScaleFactor = TryExtractScaleFactor(parts, out parsed.ScaleFactor);

        parsed.Faction = parts[parts.Length - 2];
        parsed.BlueprintId = parts[parts.Length - 1];
        parsed.IsValid = true;
        return true;
    }

    public static bool TryParseSecondLine(string line, out ParsedSecondLine parsed)
    {
        parsed = default;

        if (string.IsNullOrWhiteSpace(line))
        {
            parsed.ErrorMessage = "Пустая строка параметров кулера.";
            return false;
        }

        string normalized = NormalizeCodeText(line);
        string[] parts = normalized.Split('-');

        if (parts.Length != 3)
        {
            parsed.ErrorMessage = "Ожидалось 3 секции: C...-R...-E...";
            return false;
        }

        if (!TryParsePrefixedFloat(parts[0], 'C', out float coolingPower))
        {
            parsed.ErrorMessage = "Не удалось прочитать охлаждающую способность кулера (C...).";
            return false;
        }

        if (!TryParsePrefixedFloat(parts[1], 'R', out float coolingRadius))
        {
            parsed.ErrorMessage = "Не удалось прочитать радиус охлаждения кулера (R...).";
            return false;
        }

        if (!TryParsePrefixedFloat(parts[2], 'E', out float energyConsumption))
        {
            parsed.ErrorMessage = "Не удалось прочитать энергопотребление кулера (E...).";
            return false;
        }

        parsed.CoolingPower = coolingPower;
        parsed.CoolingRadius = coolingRadius;
        parsed.EnergyConsumption = energyConsumption;
        parsed.IsValid = true;
        return true;
    }

    public static bool TryParseFullCode(string code, out ParsedFullCode parsed)
    {
        parsed = default;

        string normalized = NormalizeCodeText(code);
        string[] lines = normalized.Split('\n');

        if (lines.Length < 3)
        {
            parsed.ErrorMessage = "Код кулера должен содержать 3 строки.";
            return false;
        }

        if (!TryParseFirstLine(lines[0], out parsed.FirstLine))
        {
            parsed.ErrorMessage = parsed.FirstLine.ErrorMessage;
            return false;
        }

        if (!TryParseSecondLine(lines[1], out parsed.SecondLine))
        {
            parsed.ErrorMessage = parsed.SecondLine.ErrorMessage;
            return false;
        }

        parsed.AlloyCode = lines[2].Trim();
        parsed.IsValid = true;
        return true;
    }

    public static bool TryNormalizeFullCode(
        string fullCode,
        out string normalizedFullCode,
        out ParsedFullCode parsed)
    {
        normalizedFullCode = string.Empty;
        parsed = default;

        if (!TryParseFullCode(fullCode, out parsed))
            return false;

        string firstLine = BuildFirstLine(
            parsed.FirstLine.ModuleType,
            parsed.FirstLine.Tier,
            parsed.FirstLine.TotalMassKg,
            parsed.FirstLine.Durability,
            parsed.FirstLine.Length,
            parsed.FirstLine.Width,
            parsed.FirstLine.Height,
            parsed.FirstLine.ShellPercent,
            parsed.FirstLine.HasScaleFactor ? parsed.FirstLine.ScaleFactor : 1f,
            parsed.FirstLine.Faction,
            parsed.FirstLine.BlueprintId
        );

        string secondLine = BuildSecondLine(
            parsed.SecondLine.CoolingPower,
            parsed.SecondLine.CoolingRadius,
            parsed.SecondLine.EnergyConsumption
        );

        normalizedFullCode = BuildFullCode(firstLine, secondLine, parsed.AlloyCode);
        return true;
    }

    public static bool AreFirstLinesEquivalent(string a, string b)
    {
        if (!TryParseFirstLine(a, out var pa)) return false;
        if (!TryParseFirstLine(b, out var pb)) return false;

        bool scaleCompatible = true;
        if (pa.HasScaleFactor && pb.HasScaleFactor)
            scaleCompatible = NearlyEqual(pa.ScaleFactor, pb.ScaleFactor, FirstLineScaleTolerance);

        return pa.ModuleType == pb.ModuleType &&
               pa.Tier == pb.Tier &&
               NearlyEqual(pa.TotalMassKg, pb.TotalMassKg, FirstLineMassTolerance) &&
               NearlyEqual(pa.Durability, pb.Durability, FirstLineDurabilityTolerance) &&
               NearlyEqual(pa.Length, pb.Length, FirstLineDimTolerance) &&
               NearlyEqual(pa.Width, pb.Width, FirstLineDimTolerance) &&
               NearlyEqual(pa.Height, pb.Height, FirstLineDimTolerance) &&
               NearlyEqual(pa.ShellPercent, pb.ShellPercent, FirstLineShellTolerance) &&
               string.Equals(pa.Faction, pb.Faction, StringComparison.Ordinal) &&
               string.Equals(pa.BlueprintId, pb.BlueprintId, StringComparison.Ordinal) &&
               scaleCompatible;
    }

    public static bool AreSecondLinesEquivalent(string a, string b)
    {
        if (!TryParseSecondLine(a, out var pa)) return false;
        if (!TryParseSecondLine(b, out var pb)) return false;

        return NearlyEqual(pa.CoolingPower, pb.CoolingPower, SecondLineTolerance) &&
               NearlyEqual(pa.CoolingRadius, pb.CoolingRadius, SecondLineTolerance) &&
               NearlyEqual(pa.EnergyConsumption, pb.EnergyConsumption, SecondLineTolerance);
    }

    public static string NormalizeCodeText(string text)
    {
        return (text ?? string.Empty).Trim().Replace("\r", "");
    }

    private static bool TryParseDimensions(string raw, out float length, out float width, out float height)
    {
        length = width = height = 0f;
        string[] dims = raw.Split('/');
        if (dims.Length != 3) return false;

        return TryParseInvariant(dims[0], out length) &&
               TryParseInvariant(dims[1], out width) &&
               TryParseInvariant(dims[2], out height);
    }

    private static bool TryExtractShellPercent(string[] parts, out float shellPercent)
    {
        shellPercent = 0f;

        for (int i = 0; i < parts.Length; i++)
        {
            string p = parts[i];
            if (string.IsNullOrEmpty(p)) continue;

            if (!p.StartsWith("sp", StringComparison.OrdinalIgnoreCase))
                continue;

            return TryParseInvariant(p.Substring(2), out shellPercent);
        }

        return false;
    }

    private static bool TryExtractScaleFactor(string[] parts, out float scaleFactor)
    {
        scaleFactor = 0f;

        for (int i = 0; i < parts.Length; i++)
        {
            string p = parts[i];
            if (string.IsNullOrEmpty(p)) continue;

            if (!p.StartsWith("sf", StringComparison.OrdinalIgnoreCase))
                continue;

            return TryParseInvariant(p.Substring(2), out scaleFactor);
        }

        return false;
    }

    private static bool TryParsePrefixedFloat(string source, char prefix, out float value)
    {
        value = 0f;

        if (string.IsNullOrEmpty(source) || source[0] != prefix)
            return false;

        return TryParseInvariant(source.Substring(1), out value);
    }

    private static bool TryParseInvariant(string source, out float value)
    {
        return float.TryParse(source, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
    }

    private static string Format3(float value) => value.ToString("F3", CultureInfo.InvariantCulture);
    private static string Format6(float value) => value.ToString("F6", CultureInfo.InvariantCulture);

    private static bool NearlyEqual(float a, float b, float tolerance)
    {
        return Math.Abs(a - b) <= tolerance;
    }
}