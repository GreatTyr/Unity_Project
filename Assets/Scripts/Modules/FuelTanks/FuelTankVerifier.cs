using System;

/// <summary>
/// Полная верификация и нормализация строкового кода топливного бака.
/// Работает со всем полным кодом.
/// </summary>
public static class FuelTankVerifier
{
    public struct VerificationResult
    {
        public bool IsValid;
        public bool IsNormalized;
        public bool IsExactMatch;

        public string ErrorMessage;
        public string WarningMessage;
        public string SuccessMessage;

        public string InputFirstLine;
        public string CanonicalFirstLine;

        public string InputSecondLine;
        public string CanonicalSecondLine;

        public string InputAlloyLine;
        public string CanonicalAlloyLine;

        public string NormalizedFullCode;
    }

    public static VerificationResult VerifyFullCodeAgainstCurrent(
        string fullInputCode,
        string currentCanonicalCode)
    {
        VerificationResult result = new VerificationResult
        {
            IsValid = false,
            IsNormalized = false,
            IsExactMatch = false,
            ErrorMessage = string.Empty,
            WarningMessage = string.Empty,
            SuccessMessage = string.Empty,
            NormalizedFullCode = string.Empty
        };

        string input = FuelTankCode.NormalizeCodeText(fullInputCode);
        string canonical = FuelTankCode.NormalizeCodeText(currentCanonicalCode);

        if (!FuelTankCode.TryParseFullCode(input, out var parsedInput))
        {
            result.ErrorMessage = parsedInput.ErrorMessage;
            return result;
        }

        if (!FuelTankCode.TryParseFullCode(canonical, out var parsedCanonical))
        {
            result.ErrorMessage = "Не удалось разобрать канонический код бака.";
            return result;
        }

        string[] inputLines = input.Split('\n');
        string[] canonicalLines = canonical.Split('\n');

        result.InputFirstLine = inputLines.Length > 0 ? inputLines[0] : string.Empty;
        result.CanonicalFirstLine = canonicalLines.Length > 0 ? canonicalLines[0] : string.Empty;

        result.InputSecondLine = inputLines.Length > 1 ? inputLines[1] : string.Empty;
        result.CanonicalSecondLine = canonicalLines.Length > 1 ? canonicalLines[1] : string.Empty;

        result.InputAlloyLine = inputLines.Length > 2 ? inputLines[2] : string.Empty;
        result.CanonicalAlloyLine = canonicalLines.Length > 2 ? canonicalLines[2] : string.Empty;

        if (!FuelTankCode.AreFirstLinesEquivalent(result.InputFirstLine, result.CanonicalFirstLine))
        {
            result.ErrorMessage = "Первая строка чертежа бака не совпадает с допустимыми параметрами.";
            return result;
        }

        if (!FuelTankCode.AreSecondLinesEquivalent(result.InputSecondLine, result.CanonicalSecondLine))
        {
            result.ErrorMessage = "Параметры бака из второй строки не совпадают с допустимыми значениями.";
            return result;
        }

        if (!string.Equals(result.InputAlloyLine, result.CanonicalAlloyLine, StringComparison.Ordinal))
        {
            result.WarningMessage =
                $"Код бака соответствует текущей конфигурации, но код сплава отличается.\n" +
                $"Было: {result.InputAlloyLine}\n" +
                $"Ожидалось: {result.CanonicalAlloyLine}";
        }

        if (!FuelTankCode.TryNormalizeFullCode(input, out string normalizedFullCode, out _))
        {
            result.ErrorMessage = "Не удалось нормализовать код бака.";
            return result;
        }

        result.NormalizedFullCode = normalizedFullCode;
        result.IsValid = true;
        result.IsExactMatch = string.Equals(input, canonical, StringComparison.Ordinal);

        if (!result.IsExactMatch)
        {
            result.IsNormalized = true;

            if (string.IsNullOrEmpty(result.WarningMessage))
            {
                result.WarningMessage =
                    $"Чертёж бака применён, но код был нормализован.\n" +
                    $"Было:\n{input}\n\n" +
                    $"Стало:\n{canonical}";
            }
        }
        else
        {
            result.SuccessMessage = "Чертёж бака успешно применён.";
        }

        return result;
    }
}