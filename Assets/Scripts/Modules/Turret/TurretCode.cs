// TurretCode.cs
using System;
using System.Globalization;

/// <summary>
/// Кодек строкового представления турели.
/// Формат: 5 строк, разделитель '\n'.
/// Строка 1: общие параметры
/// Строка 2: параметры ствольной коробки
/// Строка 3: параметры ствола
/// Строка 4: параметры станины
/// Строка 5: сплав
/// </summary>
public static class TurretCode
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
        public bool HasScaleFactor;
        public float ScaleFactor;
        public string Faction;
        public string BlueprintId;
    }

    public struct ParsedReceiverLine
    {
        public bool IsValid;
        public string ErrorMessage;
        public int LoadingPercent;
        public int LoadingTier;
        public int ChamberPercent;
        public int ChamberTier;
        public int CorpusPercent;
        public int AmmoTierBonus;
        public float LoadingPower;
        public float ChamberCapacity;
        public int MaxAmmoTier;
        public float ReceiverDurability;
        public float LoadingMassKg;
        public float ChamberMassKg;
        public float CorpusMassKg;
    }

    public struct ParsedBarrelLine
    {
        public bool IsValid;
        public string ErrorMessage;
        public float InnerDiameterMm;
        public float OuterDiameterMm;
        public float LengthMm;
        public float StrengthCoeff;
        public float MassKg;
        public float WallThicknessMm;
    }

    public struct ParsedMountLine
    {
        public bool IsValid;
        public string ErrorMessage;
        public float MountTotalMass;
        public int MotorPercent;
        public int GyroPercent;
        public int CompensatorPercent;
        public float MotorMassKg;
        public float GyroMassKg;
        public float CompensatorMassKg;
        public float AimSpeed;
        public float RecoilResistance;
        public float RotationSpeed;
        public float MaxElevationDeg;
        public float MaxDepressionDeg;
        public float TraverseArcDeg;
        public float EnergyConsumption;
    }

    public struct ParsedFullCode
    {
        public bool IsValid;
        public string ErrorMessage;
        public ParsedFirstLine FirstLine;
        public ParsedReceiverLine ReceiverLine;
        public ParsedBarrelLine BarrelLine;
        public ParsedMountLine MountLine;
        public string AlloyCode;
    }

    private const float MassTol = 0.015f;
    private const float DurTol = 0.15f;
    private const float DimTol = 0.0015f;
    private const float ScaleTol = 0.000001f;
    private const float GenTol = 0.005f;

    // =========================================
    // BUILD
    // =========================================

    public static string BuildFirstLine(
        string moduleType, int tier,
        float totalMassKg, float durability,
        float length, float width, float height,
        float shellPercent, float scaleFactor,
        string faction, string blueprintId)
    {
        return $"{moduleType}-T{tier}" +
               $"-m{F3(totalMassKg)}" +
               $"-d{F3(durability)}" +
               $"-{F3(length)}/{F3(width)}/{F3(height)}" +
               $"-sp{F3(shellPercent)}" +
               $"-sf{F6(scaleFactor)}" +
               $"-{(string.IsNullOrEmpty(faction) ? "NONE" : faction)}-{blueprintId}";
    }

    public static string BuildReceiverLine(
        int loadingPercent, int loadingTier,
        int chamberPercent, int chamberTier,
        int corpusPercent,
        int ammoTierBonus,
        float loadingPower, float chamberCapacity,
        int maxAmmoTier, float receiverDurability,
        float loadingMassKg, float chamberMassKg, float corpusMassKg)
    {
        return $"LP{loadingPercent}-LT{loadingTier}" +
               $"-CP{chamberPercent}-CT{chamberTier}" +
               $"-RP{corpusPercent}" +
               $"-ATB{ammoTierBonus}" +
               $"-LPW{F3(loadingPower)}" +
               $"-CC{F3(chamberCapacity)}" +
               $"-MAT{maxAmmoTier}" +
               $"-RD{F3(receiverDurability)}" +
               $"-LM{F3(loadingMassKg)}" +
               $"-CHM{F3(chamberMassKg)}" +
               $"-RM{F3(corpusMassKg)}";
    }

    public static string BuildBarrelLine(
        float innerDiameterMm, float outerDiameterMm,
        float lengthMm, float strengthCoeff,
        float massKg, float wallThicknessMm)
    {
        return $"BD{F3(innerDiameterMm)}" +
               $"-BDO{F3(outerDiameterMm)}" +
               $"-BL{F3(lengthMm)}" +
               $"-BSC{F4(strengthCoeff)}" +
               $"-BM{F3(massKg)}" +
               $"-BW{F3(wallThicknessMm)}";
    }

    public static string BuildMountLine(
        float mountTotalMass,
        int motorPercent, int gyroPercent, int compensatorPercent,
        float motorMassKg, float gyroMassKg, float compensatorMassKg,
        float aimSpeed, float recoilResistance, float rotationSpeed,
        float maxElevationDeg, float maxDepressionDeg,
        float traverseArcDeg, float energyConsumption)
    {
        return $"MM{F3(mountTotalMass)}" +
               $"-MOP{motorPercent}-MOP_M{F3(motorMassKg)}" +
               $"-GP{gyroPercent}-GP_M{F3(gyroMassKg)}" +
               $"-COP{compensatorPercent}-COP_M{F3(compensatorMassKg)}" +
               $"-AS{F3(aimSpeed)}" +
               $"-RR{F3(recoilResistance)}" +
               $"-RS{F3(rotationSpeed)}" +
               $"-EL{F3(maxElevationDeg)}" +
               $"-DE{F3(maxDepressionDeg)}" +
               $"-TR{F3(traverseArcDeg)}" +
               $"-EC{F3(energyConsumption)}";
    }

    public static string BuildFullCode(
        string firstLine, string receiverLine,
        string barrelLine, string mountLine,
        string alloyCode)
    {
        return $"{Norm(firstLine)}\n" +
               $"{Norm(receiverLine)}\n" +
               $"{Norm(barrelLine)}\n" +
               $"{Norm(mountLine)}\n" +
               $"{Norm(alloyCode)}";
    }

    // =========================================
    // PARSE
    // =========================================

    public static bool TryParseFullCode(string code, out ParsedFullCode parsed)
    {
        parsed = default;
        string norm = Norm(code);
        string[] lines = norm.Split('\n');

        if (lines.Length < 5)
        {
            parsed.ErrorMessage = "Код турели должен содержать 5 строк.";
            return false;
        }

        if (!TryParseFirstLine(lines[0], out parsed.FirstLine))
        {
            parsed.ErrorMessage = parsed.FirstLine.ErrorMessage;
            return false;
        }

        if (!TryParseReceiverLine(lines[1], out parsed.ReceiverLine))
        {
            parsed.ErrorMessage = parsed.ReceiverLine.ErrorMessage;
            return false;
        }

        if (!TryParseBarrelLine(lines[2], out parsed.BarrelLine))
        {
            parsed.ErrorMessage = parsed.BarrelLine.ErrorMessage;
            return false;
        }

        if (!TryParseMountLine(lines[3], out parsed.MountLine))
        {
            parsed.ErrorMessage = parsed.MountLine.ErrorMessage;
            return false;
        }

        parsed.AlloyCode = lines[4].Trim();
        parsed.IsValid = true;
        return true;
    }

    public static bool TryParseFirstLine(string line, out ParsedFirstLine p)
    {
        p = default;
        if (string.IsNullOrWhiteSpace(line))
        {
            p.ErrorMessage = "Пустая первая строка кода турели.";
            return false;
        }

        string[] parts = Norm(line).Split('-');
        if (parts.Length < 7)
        {
            p.ErrorMessage = "Неверная первая строка кода турели.";
            return false;
        }

        p.ModuleType = parts[0];

        if (!IntAfterPrefix(parts[1], 'T', out p.Tier))
        { p.ErrorMessage = "Не удалось прочитать тир турели."; return false; }

        if (!FloatAfterChar(parts[2], 'm', out p.TotalMassKg))
        { p.ErrorMessage = "Не удалось прочитать массу турели."; return false; }

        if (!FloatAfterChar(parts[3], 'd', out p.Durability))
        { p.ErrorMessage = "Не удалось прочитать прочность турели."; return false; }

        if (!TryParseDims(parts[4], out p.Length, out p.Width, out p.Height))
        { p.ErrorMessage = "Не удалось прочитать размеры турели."; return false; }

        if (!TryExtractPrefixedFloat(parts, "sp", out p.ShellPercent))
        { p.ErrorMessage = "Не удалось прочитать shell percent турели."; return false; }

        p.HasScaleFactor = TryExtractPrefixedFloat(parts, "sf", out p.ScaleFactor);
        p.Faction = parts[parts.Length - 2];
        p.BlueprintId = parts[parts.Length - 1];
        p.IsValid = true;
        return true;
    }

    public static bool TryParseReceiverLine(string line, out ParsedReceiverLine p)
    {
        p = default;
        if (string.IsNullOrWhiteSpace(line))
        { p.ErrorMessage = "Пустая строка ствольной коробки."; return false; }

        var tokens = TokenDict(Norm(line).Split('-'));

        if (!TryGetInt(tokens, "LP", out p.LoadingPercent) ||
            !TryGetInt(tokens, "LT", out p.LoadingTier) ||
            !TryGetInt(tokens, "CP", out p.ChamberPercent) ||
            !TryGetInt(tokens, "CT", out p.ChamberTier) ||
            !TryGetInt(tokens, "RP", out p.CorpusPercent) ||
            !TryGetInt(tokens, "ATB", out p.AmmoTierBonus) ||
            !TryGetFloat(tokens, "LPW", out p.LoadingPower) ||
            !TryGetFloat(tokens, "CC", out p.ChamberCapacity) ||
            !TryGetInt(tokens, "MAT", out p.MaxAmmoTier) ||
            !TryGetFloat(tokens, "RD", out p.ReceiverDurability) ||
            !TryGetFloat(tokens, "LM", out p.LoadingMassKg) ||
            !TryGetFloat(tokens, "CHM", out p.ChamberMassKg) ||
            !TryGetFloat(tokens, "RM", out p.CorpusMassKg))
        {
            p.ErrorMessage = "Ошибка разбора строки ствольной коробки.";
            return false;
        }

        p.IsValid = true;
        return true;
    }

    public static bool TryParseBarrelLine(string line, out ParsedBarrelLine p)
    {
        p = default;
        if (string.IsNullOrWhiteSpace(line))
        { p.ErrorMessage = "Пустая строка ствола."; return false; }

        var tokens = TokenDict(Norm(line).Split('-'));

        if (!TryGetFloat(tokens, "BD", out p.InnerDiameterMm) ||
            !TryGetFloat(tokens, "BDO", out p.OuterDiameterMm) ||
            !TryGetFloat(tokens, "BL", out p.LengthMm) ||
            !TryGetFloat(tokens, "BSC", out p.StrengthCoeff) ||
            !TryGetFloat(tokens, "BM", out p.MassKg) ||
            !TryGetFloat(tokens, "BW", out p.WallThicknessMm))
        {
            p.ErrorMessage = "Ошибка разбора строки ствола.";
            return false;
        }

        p.IsValid = true;
        return true;
    }

    public static bool TryParseMountLine(string line, out ParsedMountLine p)
    {
        p = default;
        if (string.IsNullOrWhiteSpace(line))
        { p.ErrorMessage = "Пустая строка станины."; return false; }

        var tokens = TokenDict(Norm(line).Split('-'));

        if (!TryGetFloat(tokens, "MM", out p.MountTotalMass) ||
            !TryGetInt(tokens, "MOP", out p.MotorPercent) ||
            !TryGetFloat(tokens, "MOP_M", out p.MotorMassKg) ||
            !TryGetInt(tokens, "GP", out p.GyroPercent) ||
            !TryGetFloat(tokens, "GP_M", out p.GyroMassKg) ||
            !TryGetInt(tokens, "COP", out p.CompensatorPercent) ||
            !TryGetFloat(tokens, "COP_M", out p.CompensatorMassKg) ||
            !TryGetFloat(tokens, "AS", out p.AimSpeed) ||
            !TryGetFloat(tokens, "RR", out p.RecoilResistance) ||
            !TryGetFloat(tokens, "RS", out p.RotationSpeed) ||
            !TryGetFloat(tokens, "EL", out p.MaxElevationDeg) ||
            !TryGetFloat(tokens, "DE", out p.MaxDepressionDeg) ||
            !TryGetFloat(tokens, "TR", out p.TraverseArcDeg) ||
            !TryGetFloat(tokens, "EC", out p.EnergyConsumption))
        {
            p.ErrorMessage = "Ошибка разбора строки станины.";
            return false;
        }

        p.IsValid = true;
        return true;
    }

    // =========================================
    // EQUIVALENCE
    // =========================================

    public static bool AreFirstLinesEquivalent(string a, string b)
    {
        if (!TryParseFirstLine(a, out var pa)) return false;
        if (!TryParseFirstLine(b, out var pb)) return false;

        bool scaleOk = (!pa.HasScaleFactor || !pb.HasScaleFactor) ||
                       Near(pa.ScaleFactor, pb.ScaleFactor, ScaleTol);

        return pa.ModuleType == pb.ModuleType &&
               pa.Tier == pb.Tier &&
               Near(pa.TotalMassKg, pb.TotalMassKg, MassTol) &&
               Near(pa.Durability, pb.Durability, DurTol) &&
               Near(pa.Length, pb.Length, DimTol) &&
               Near(pa.Width, pb.Width, DimTol) &&
               Near(pa.Height, pb.Height, DimTol) &&
               Near(pa.ShellPercent, pb.ShellPercent, 0.001f) &&
               pa.Faction == pb.Faction &&
               pa.BlueprintId == pb.BlueprintId &&
               scaleOk;
    }

    public static bool AreReceiverLinesEquivalent(string a, string b)
    {
        if (!TryParseReceiverLine(a, out var pa)) return false;
        if (!TryParseReceiverLine(b, out var pb)) return false;

        return pa.LoadingPercent == pb.LoadingPercent &&
               pa.LoadingTier == pb.LoadingTier &&
               pa.ChamberPercent == pb.ChamberPercent &&
               pa.ChamberTier == pb.ChamberTier &&
               pa.AmmoTierBonus == pb.AmmoTierBonus &&
               Near(pa.LoadingPower, pb.LoadingPower, GenTol) &&
               Near(pa.ChamberCapacity, pb.ChamberCapacity, GenTol) &&
               Near(pa.ReceiverDurability, pb.ReceiverDurability, GenTol);
    }

    public static bool AreBarrelLinesEquivalent(string a, string b)
    {
        if (!TryParseBarrelLine(a, out var pa)) return false;
        if (!TryParseBarrelLine(b, out var pb)) return false;

        return Near(pa.InnerDiameterMm, pb.InnerDiameterMm, GenTol) &&
               Near(pa.OuterDiameterMm, pb.OuterDiameterMm, GenTol) &&
               Near(pa.LengthMm, pb.LengthMm, GenTol) &&
               Near(pa.StrengthCoeff, pb.StrengthCoeff, GenTol) &&
               Near(pa.MassKg, pb.MassKg, GenTol);
    }

    public static bool AreMountLinesEquivalent(string a, string b)
    {
        if (!TryParseMountLine(a, out var pa)) return false;
        if (!TryParseMountLine(b, out var pb)) return false;

        return pa.MotorPercent == pb.MotorPercent &&
               pa.GyroPercent == pb.GyroPercent &&
               pa.CompensatorPercent == pb.CompensatorPercent &&
               Near(pa.AimSpeed, pb.AimSpeed, GenTol) &&
               Near(pa.RecoilResistance, pb.RecoilResistance, GenTol) &&
               Near(pa.RotationSpeed, pb.RotationSpeed, GenTol);
    }

    // =========================================
    // NORMALIZE
    // =========================================

    public static string Norm(string text)
        => (text ?? string.Empty).Trim().Replace("\r", "");

    // =========================================
    // HELPERS
    // =========================================

    private static bool TryParseDims(string raw,
        out float l, out float w, out float h)
    {
        l = w = h = 0f;
        string[] d = raw.Split('/');
        if (d.Length != 3) return false;
        return ParseF(d[0], out l) && ParseF(d[1], out w) && ParseF(d[2], out h);
    }

    private static bool TryExtractPrefixedFloat(
        string[] parts, string prefix, out float value)
    {
        value = 0f;
        foreach (var p in parts)
        {
            if (p.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                return ParseF(p.Substring(prefix.Length), out value);
        }
        return false;
    }

    private static bool FloatAfterChar(string s, char c, out float v)
    {
        v = 0f;
        if (string.IsNullOrEmpty(s) || s[0] != c) return false;
        return ParseF(s.Substring(1), out v);
    }

    private static bool IntAfterPrefix(string s, char c, out int v)
    {
        v = 0;
        if (string.IsNullOrEmpty(s) || s[0] != c) return false;
        return int.TryParse(s.Substring(1),
            System.Globalization.NumberStyles.Integer,
            CultureInfo.InvariantCulture, out v);
    }

    private static System.Collections.Generic.Dictionary<string, string>
        TokenDict(string[] parts)
    {
        var d = new System.Collections.Generic.Dictionary<string, string>(
            StringComparer.Ordinal);
        foreach (var p in parts)
        {
            if (string.IsNullOrEmpty(p)) continue;
            int splitAt = -1;
            for (int i = 0; i < p.Length; i++)
            {
                if (char.IsDigit(p[i]) || p[i] == '.' || p[i] == '-' && i > 0)
                { splitAt = i; break; }
            }
            if (splitAt > 0)
                d[p.Substring(0, splitAt)] = p.Substring(splitAt);
            else
                d[p] = "";
        }
        return d;
    }

    private static bool TryGetFloat(
        System.Collections.Generic.Dictionary<string, string> d,
        string key, out float v)
    {
        v = 0f;
        return d.TryGetValue(key, out string s) && ParseF(s, out v);
    }

    private static bool TryGetInt(
        System.Collections.Generic.Dictionary<string, string> d,
        string key, out int v)
    {
        v = 0;
        return d.TryGetValue(key, out string s) &&
               int.TryParse(s,
                   System.Globalization.NumberStyles.Integer,
                   CultureInfo.InvariantCulture, out v);
    }

    private static bool ParseF(string s, out float v)
        => float.TryParse(s, System.Globalization.NumberStyles.Float,
               CultureInfo.InvariantCulture, out v);

    private static bool Near(float a, float b, float tol)
        => Math.Abs(a - b) <= tol;

    private static string F3(float v) => v.ToString("F3", CultureInfo.InvariantCulture);
    private static string F4(float v) => v.ToString("F4", CultureInfo.InvariantCulture);
    private static string F6(float v) => v.ToString("F6", CultureInfo.InvariantCulture);
}