using System;
using System.Globalization;
using UnityEngine;

/// <summary>
/// Чистый статический сервис для разбора, валидации и сравнения строковых кодов чертежей.
/// Отвязан от UI и MonoBehaviour.
/// </summary>
public static class BlueprintParser
{
    // ---- Допуски семантической проверки (из старого верстака) ----
    private const float TolMass = 0.015f;       // кг
    private const float TolDurability = 0.15f;  // усл. ед.
    private const float TolDim = 0.0015f;       // м
    private const float TolPower = 0.005f;      // E/s
    private const float TolFuel = 0.00015f;     // kg/s

    private const string ShellPercentTokenPrefix = "sp";

    /// <summary>
    /// Результат парсинга первой строки чертежа (базовые габариты и идентификаторы).
    /// </summary>
    public struct ParsedBaseData
    {
        public bool IsValid;
        public string ErrorMessage;

        public string ModuleType;
        public int Tier;
        public float TargetMassKg;
        public float TargetDurability;
        public float TargetLength;
        public float TargetWidth;
        public float TargetHeight;
        public float ShellPercent;
        public string Faction;
        public string BlueprintId;
    }

    /// <summary>
    /// Парсит только первую строку кода чертежа.
    /// Формат: Type-Tn-mX-dY-L/W/H-spS-Faction-BP
    /// </summary>
    public static ParsedBaseData ParseFirstLine(string line1, string expectedModuleType)
    {
        var result = new ParsedBaseData();

        if (string.IsNullOrWhiteSpace(line1))
        {
            result.ErrorMessage = "Пустая строка чертежа.";
            return result;
        }

        // FIX: Метод устойчив к передаче полного 3-строчного кода.
        // Берем только первую строку и парсим именно ее.
        string normalized = NormalizeCodeText(line1);
        string[] rawLines = normalized.Split('\n');
        string firstLine = (rawLines.Length > 0 ? rawLines[0] : string.Empty).Trim();

        if (string.IsNullOrWhiteSpace(firstLine))
        {
            result.ErrorMessage = "Первая строка чертежа пуста.";
            return result;
        }

        string[] parts = firstLine.Split('-');
        if (parts.Length < 7)
        {
            result.ErrorMessage = "Неверная или устаревшая первая строка (слишком мало секций).";
            return result;
        }

        if (parts[0] != expectedModuleType)
        {
            result.ErrorMessage = $"Чертеж не от {expectedModuleType}.";
            return result;
        }
        result.ModuleType = parts[0];

        // Тир
        string tStr = parts[1].Replace("T", "");
        if (!int.TryParse(tStr, out result.Tier))
        {
            result.ErrorMessage = "Невозможно прочитать тир из кода.";
            return result;
        }

        // Масса
        if (!TryParsePrefixedFloat(parts[2], 'm', out result.TargetMassKg) || result.TargetMassKg <= 0f)
        {
            result.ErrorMessage = "Неверная масса в первой строке чертежа.";
            return result;
        }

        // Прочность (сохраняем, хоть пока и не используем активно для обратного реверса)
        TryParsePrefixedFloat(parts[3], 'd', out result.TargetDurability);

        // Габариты X/Y/Z
        string[] dims = parts[4].Split('/');
        if (dims.Length != 3 ||
            !TryParseInvariant(dims[0], out result.TargetLength) ||
            !TryParseInvariant(dims[1], out result.TargetWidth) ||
            !TryParseInvariant(dims[2], out result.TargetHeight))
        {
            result.ErrorMessage = "Невозможно прочитать габариты из кода.";
            return result;
        }

        // Faction и BP (всегда последние 2 элемента)
        result.Faction = parts[parts.Length - 2];
        result.BlueprintId = parts[parts.Length - 1];

        // Ищем токен sp (shell percent)
        if (!TryExtractShellPercent(parts, out result.ShellPercent))
        {
            // Если токена нет, возвращаем -1, чтобы верстак знал, что нужно использовать fallback-восстановление по массе
            result.ShellPercent = -1f;
        }

        result.IsValid = true;
        return result;
    }

    /// <summary>
    /// Проверяет, совпадают ли два чертежа с учетом математических погрешностей float.
    /// Используется для античита (чтобы игрок не ввел код с невозможными статами).
    /// </summary>
    public static bool IsBlueprintSemanticallyValid(string inputCode, string generatedCode, string moduleType)
    {
        string[] inLines = NormalizeCodeText(inputCode).Split('\n');
        string[] genLines = NormalizeCodeText(generatedCode).Split('\n');

        if (inLines.Length < 2 || genLines.Length < 2) return false;

        if (!CompareFirstLineWithTolerance(inLines[0], genLines[0])) return false;

        // Специфичная проверка 2 строки для Генератора
        if (moduleType == "Generator")
        {
            if (!CompareGeneratorSecondLineWithTolerance(inLines[1], genLines[1])) return false;
        }
        else
        {
            // Для остальных (EnergyStorage и др) просто сравниваем строки
            if (inLines[1].Trim() != genLines[1].Trim()) return false;
        }

        return true;
    }

    public static string NormalizeCodeText(string text)
    {
        return (text ?? string.Empty).Trim().Replace("\r", "");
    }

    // ==========================================
    // Внутренние методы сравнения и парсинга
    // ==========================================

    private static bool CompareFirstLineWithTolerance(string a, string b)
    {
        var pa = a.Split('-');
        var pb = b.Split('-');
        if (pa.Length < 7 || pb.Length < 7) return false;

        // type, tier
        if (pa[0] != pb[0]) return false;
        if (pa[1] != pb[1]) return false;

        // faction + blueprint
        if (pa[pa.Length - 2] != pb[pb.Length - 2]) return false;
        if (pa[pa.Length - 1] != pb[pb.Length - 1]) return false;

        if (!TryParsePrefixedFloat(pa[2], 'm', out var ma) || !TryParsePrefixedFloat(pb[2], 'm', out var mb)) return false;
        if (!TryParsePrefixedFloat(pa[3], 'd', out var da) || !TryParsePrefixedFloat(pb[3], 'd', out var db)) return false;

        if (Mathf.Abs(ma - mb) > TolMass) return false;
        if (Mathf.Abs(da - db) > TolDurability) return false;

        if (!TryParseDims(pa[4], out var aL, out var aW, out var aH)) return false;
        if (!TryParseDims(pb[4], out var bL, out var bW, out var bH)) return false;

        if (Mathf.Abs(aL - bL) > TolDim) return false;
        if (Mathf.Abs(aW - bW) > TolDim) return false;
        if (Mathf.Abs(aH - bH) > TolDim) return false;

        bool aHasSp = TryExtractShellPercent(pa, out float aSp);
        bool bHasSp = TryExtractShellPercent(pb, out float bSp);
        if (aHasSp && bHasSp)
        {
            if (Mathf.Abs(aSp - bSp) > 0.001f) return false;
        }

        return true;
    }

    private static bool CompareGeneratorSecondLineWithTolerance(string a, string b)
    {
        if (!TryParseGeneratorLine(a, out var p1, out var f1, out var t1)) return false;
        if (!TryParseGeneratorLine(b, out var p2, out var f2, out var t2)) return false;

        if (t1 != t2) return false;
        if (Mathf.Abs(p1 - p2) > TolPower) return false;
        if (Mathf.Abs(f1 - f2) > TolFuel) return false;

        return true;
    }

    private static bool TryParseGeneratorLine(string line, out float p, out float f, out int ft)
    {
        p = 0f; f = 0f; ft = 0;

        var parts = line.Split('-');
        if (parts.Length != 3) return false;
        if (!TryParsePrefixedFloat(parts[0], 'P', out p)) return false;
        if (!TryParsePrefixedFloat(parts[1], 'F', out f)) return false;
        if (!parts[2].StartsWith("FT")) return false;

        return int.TryParse(parts[2].Substring(2), out ft);
    }

    private static bool TryExtractShellPercent(string[] parts, out float shellPercentValue)
    {
        shellPercentValue = 0f;
        if (parts == null) return false;

        for (int i = 0; i < parts.Length; i++)
        {
            string p = parts[i];
            if (string.IsNullOrEmpty(p)) continue;

            if (!p.StartsWith(ShellPercentTokenPrefix, StringComparison.OrdinalIgnoreCase))
                continue;

            string raw = p.Substring(ShellPercentTokenPrefix.Length);
            if (!TryParseInvariant(raw, out float parsed)) return false;

            shellPercentValue = parsed;
            return true;
        }

        return false;
    }

    private static bool TryParsePrefixedFloat(string s, char prefix, out float v)
    {
        v = 0f;
        if (string.IsNullOrEmpty(s) || s[0] != prefix) return false;
        return TryParseInvariant(s.Substring(1), out v);
    }

    private static bool TryParseDims(string s, out float l, out float w, out float h)
    {
        l = w = h = 0f;
        var d = s.Split('/');
        if (d.Length != 3) return false;
        return TryParseInvariant(d[0], out l) &&
               TryParseInvariant(d[1], out w) &&
               TryParseInvariant(d[2], out h);
    }

    private static bool TryParseInvariant(string value, out float result)
    {
        return float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out result);
    }
}