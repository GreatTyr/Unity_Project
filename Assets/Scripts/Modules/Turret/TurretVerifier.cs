// TurretVerifier.cs
using System;

/// <summary>
/// Верификация и нормализация строкового кода турели.
/// </summary>
public static class TurretVerifier
{
    public struct VerificationResult
    {
        public bool IsValid;
        public bool IsNormalized;
        public bool IsExactMatch;
        public string ErrorMessage;
        public string WarningMessage;
        public string SuccessMessage;
        public string NormalizedFullCode;
    }

    public static VerificationResult VerifyFullCodeAgainstCurrent(
        string fullInputCode,
        string currentCanonicalCode)
    {
        var result = new VerificationResult();

        string input = TurretCode.Norm(fullInputCode);
        string canonical = TurretCode.Norm(currentCanonicalCode);

        if (!TurretCode.TryParseFullCode(input, out var parsedInput))
        {
            result.ErrorMessage = parsedInput.ErrorMessage;
            return result;
        }

        if (!TurretCode.TryParseFullCode(canonical, out _))
        {
            result.ErrorMessage = "Не удалось разобрать канонический код турели.";
            return result;
        }

        string[] inputLines = input.Split('\n');
        string[] canonicalLines = canonical.Split('\n');

        string inL1 = inputLines.Length > 0 ? inputLines[0] : "";
        string caL1 = canonicalLines.Length > 0 ? canonicalLines[0] : "";

        string inL2 = inputLines.Length > 1 ? inputLines[1] : "";
        string caL2 = canonicalLines.Length > 1 ? canonicalLines[1] : "";

        string inL3 = inputLines.Length > 2 ? inputLines[2] : "";
        string caL3 = canonicalLines.Length > 2 ? canonicalLines[2] : "";

        string inL4 = inputLines.Length > 3 ? inputLines[3] : "";
        string caL4 = canonicalLines.Length > 3 ? canonicalLines[3] : "";

        string inL5 = inputLines.Length > 4 ? inputLines[4] : "";
        string caL5 = canonicalLines.Length > 4 ? canonicalLines[4] : "";

        if (!TurretCode.AreFirstLinesEquivalent(inL1, caL1))
        {
            result.ErrorMessage = "Первая строка чертежа турели не совпадает.";
            return result;
        }

        if (!TurretCode.AreReceiverLinesEquivalent(inL2, caL2))
        {
            result.ErrorMessage = "Параметры ствольной коробки не совпадают.";
            return result;
        }

        if (!TurretCode.AreBarrelLinesEquivalent(inL3, caL3))
        {
            result.ErrorMessage = "Параметры ствола не совпадают.";
            return result;
        }

        if (!TurretCode.AreMountLinesEquivalent(inL4, caL4))
        {
            result.ErrorMessage = "Параметры станины не совпадают.";
            return result;
        }

        if (!string.Equals(inL5, caL5, StringComparison.Ordinal))
        {
            result.WarningMessage =
                $"Код турели соответствует конфигурации, но код сплава отличается.\n" +
                $"Было: {inL5}\nОжидалось: {caL5}";
        }

        result.IsValid = true;
        result.IsExactMatch = string.Equals(input, canonical, StringComparison.Ordinal);

        if (!result.IsExactMatch)
        {
            result.IsNormalized = true;
            if (string.IsNullOrEmpty(result.WarningMessage))
            {
                result.WarningMessage =
                    $"Чертёж турели применён, но код был нормализован.\n" +
                    $"Было:\n{input}\n\nСтало:\n{canonical}";
            }
        }
        else
        {
            result.SuccessMessage = "Чертёж турели успешно применён.";
        }

        result.NormalizedFullCode = canonical;
        return result;
    }
}