// CannonballValidator.cs
using System;
using System.Globalization;
using UnityEngine;

/// <summary>
/// Каноническая сборка, парсинг и строгая валидация кода ядра.
/// Код описывает только свойства ядра и не содержит параметров конкретного ствола и метательного заряда.
/// </summary>
public static class CannonballValidator
{
    // Формат:
    //  0  prefix = C
    //  1  chargeType
    //  2  shellTier
    //  3  diameterMm
    //  4  totalCannonballMassKg
    //  5  shellMassKg
    //  6  shellStrength
    //  7  explosiveTier
    //  8  explosiveMassKg
    //  9  explosivePower
    // 10  damageElementType
    // 11  damageElementTier
    // 12  damageElementMassKg
    // 13  areaType
    // 14  damageRadius
    // 15  areaPenetration
    // 16  areaDamage
    // 17  fuzeType

    public const int PartsCount = 18;
    public const string Prefix = "C";

    public static string BuildCode(CannonballCalc.CannonballOutput o)
    {
        if (o == null) return string.Empty;

        string[] p = new string[PartsCount];

        p[0] = Prefix;
        p[1] = ((int)o.chargeType).ToString();
        p[2] = o.shellTier.ToString();
        p[3] = FN(o.diameterMm);
        p[4] = FN(o.totalCannonballMassKg);
        p[5] = FN(o.shellMassKg);
        p[6] = FN(o.shellStrength);
        p[7] = o.explosiveTier.ToString();
        p[8] = FN(o.explosiveMassKg);
        p[9] = FN(o.explosivePower);
        p[10] = ((int)o.damageElementType).ToString();
        p[11] = o.damageElementTier.ToString();
        p[12] = FN(o.damageElementMassKg);
        p[13] = ((int)o.areaType).ToString();
        p[14] = FN(o.damageRadius);
        p[15] = FN(o.areaPenetration);
        p[16] = FN(o.areaDamage);
        p[17] = ((int)o.fuzeType).ToString();

        return string.Join("-", p);
    }

    public static bool TryParseCode(string code, out CannonballCalc.CannonballInput input, out string error)
    {
        input = null;
        error = "";

        if (string.IsNullOrWhiteSpace(code))
        {
            error = "Код ядра пуст.";
            return false;
        }

        string[] p = code.Split('-');
        if (p.Length != PartsCount)
        {
            error = "Неверное количество элементов в коде ядра.";
            return false;
        }

        if (p[0] != Prefix)
        {
            error = "Неверный префикс кода ядра.";
            return false;
        }

        var parsed = new CannonballCalc.CannonballInput();

        if (!int.TryParse(p[1], out int chargeTypeInt) ||
            !Enum.IsDefined(typeof(CannonballCalc.ChargeType), chargeTypeInt))
        {
            error = "Неверный тип ядра.";
            return false;
        }
        parsed.chargeType = (CannonballCalc.ChargeType)chargeTypeInt;

        if (!int.TryParse(p[2], out parsed.shellTier)) { error = "Неверный тир корпуса ядра."; return false; }
        if (!TryParseFloatAny(p[3], out parsed.diameterMm)) { error = "Неверный диаметр."; return false; }

        if (!int.TryParse(p[7], out parsed.explosiveTier)) { error = "Неверный тир разрывного заряда."; return false; }
        if (!TryParseFloatAny(p[8], out parsed.explosiveMassKg)) { error = "Неверная масса разрывного заряда."; return false; }

        if (!int.TryParse(p[10], out int deInt) ||
            !Enum.IsDefined(typeof(CannonballCalc.DamageElementType), deInt))
        {
            error = "Неверный тип поражающего элемента.";
            return false;
        }
        parsed.damageElementType = (CannonballCalc.DamageElementType)deInt;

        if (!int.TryParse(p[11], out parsed.damageElementTier)) { error = "Неверный тир поражающего элемента."; return false; }
        if (!TryParseFloatAny(p[12], out parsed.damageElementMassKg)) { error = "Неверная масса поражающего элемента."; return false; }

        if (!int.TryParse(p[13], out int areaInt) ||
            !Enum.IsDefined(typeof(CannonballCalc.AreaType), areaInt))
        {
            error = "Неверный тип области поражения.";
            return false;
        }
        parsed.areaType = (CannonballCalc.AreaType)areaInt;

        if (!int.TryParse(p[17], out int fuzeInt) ||
            !Enum.IsDefined(typeof(CannonballCalc.FuzeType), fuzeInt))
        {
            error = "Неверный тип взрывателя.";
            return false;
        }
        parsed.fuzeType = (CannonballCalc.FuzeType)fuzeInt;

        parsed.craftCount = 1;

        CannonballCalc.NormalizeInput(parsed);
        CannonballCalc.CannonballOutput calc = CannonballCalc.Calculate(parsed);

        if (calc == null || !string.IsNullOrEmpty(calc.error))
        {
            error = calc != null ? calc.error : "Ошибка расчёта ядра.";
            return false;
        }

        string canonical = BuildCode(calc);
        if (!string.Equals(code, canonical, StringComparison.Ordinal))
        {
            error = "Код ядра не соответствует каноническому формату.";
            return false;
        }

        input = parsed;
        return true;
    }

    private static bool TryParseFloatAny(string s, out float value)
    {
        string normalized = s.Replace(',', '.');
        return float.TryParse(
            normalized,
            NumberStyles.Float,
            CultureInfo.InvariantCulture,
            out value);
    }

    public static string FN(float v)
    {
        float rounded = CannonballCalc.Ceil3(v);
        if (rounded == Mathf.Floor(rounded) && rounded < 1000000f)
            return ((int)rounded).ToString();
        return rounded.ToString("F3", CultureInfo.InvariantCulture).Replace('.', ',');
    }
}