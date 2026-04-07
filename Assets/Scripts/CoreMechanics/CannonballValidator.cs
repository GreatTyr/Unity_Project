using System;
using System.Globalization;
using UnityEngine;

/// <summary>
/// Каноническая сборка, парсинг и строгая валидация кода ядра.
/// Код описывает только свойства ядра и не содержит параметров конкретного ствола.
/// </summary>
public static class CannonballValidator
{
    // Формат:
    //  0  prefix = C
    //  1  chargeType
    //  2  shellTier
    //  3  diameterMm
    //  4  totalProjectileMassKg
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
    // 18  propellantTier
    // 19  propellantMassKg
    // 20  propulsionForce
    // 21  totalAmmoMassKg

    public const int PartsCount = 22;
    public const string Prefix = "C";

    public static string BuildCode(CannonballCalc.CannonballOutput o)
    {
        if (o == null) return string.Empty;

        string[] p = new string[PartsCount];

        p[0] = Prefix;
        p[1] = ((int)o.chargeType).ToString();
        p[2] = o.shellTier.ToString();
        p[3] = FN(o.diameterMm);
        p[4] = FN(o.totalProjectileMassKg);
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
        p[18] = o.propellantTier.ToString();
        p[19] = FN(o.propellantMassKg);
        p[20] = FN(o.propulsionForce);
        p[21] = FN(o.totalAmmoMassKg);

        return string.Join("-", p);
    }

    public static bool TryParseCode(string code, out CannonballCalc.CannonballInput input, out string error)
    {
        input = null;
        error = "";

        if (string.IsNullOrWhiteSpace(code))
        {
            error = "Код боеприпаса пуст.";
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
            error = "Неверный тип боеприпаса.";
            return false;
        }
        parsed.chargeType = (CannonballCalc.ChargeType)chargeTypeInt;

        if (!int.TryParse(p[2], out parsed.shellTier)) { error = "Неверный тир оболочки."; return false; }
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

        if (!int.TryParse(p[18], out parsed.propellantTier)) { error = "Неверный тир метательного заряда."; return false; }
        if (!TryParseFloatAny(p[19], out parsed.propellantMassKg)) { error = "Неверная масса метательного заряда."; return false; }

        parsed.craftCount = 1;

        // Строгое соответствие формату ядра без "несуществующих сущностей".
        if (!IsStrictFormatAllowed(parsed))
        {
            error = "Неправильный формат кода ядра.";
            return false;
        }

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

    private static bool IsStrictFormatAllowed(CannonballCalc.CannonballInput input)
    {
        if (input == null) return false;

        float mass = CannonballCalc.Ceil3(CannonballCalc.ProjectileMassKg(
            CannonballCalc.NormalizeDiameterMm(input.diameterMm)));

        if (!CannonballCalc.IsChargeTypeAllowed(input.chargeType, mass))
            return false;

        switch (input.chargeType)
        {
            case CannonballCalc.ChargeType.FM:
                if (input.explosiveTier != 0) return false;
                if (Mathf.Abs(input.explosiveMassKg) > 0.0001f) return false;
                if (input.damageElementType != CannonballCalc.DamageElementType.None) return false;
                if (input.damageElementTier != 0) return false;
                if (Mathf.Abs(input.damageElementMassKg) > 0.0001f) return false;
                if (input.fuzeType != CannonballCalc.FuzeType.No) return false;
                if (input.areaType != CannonballCalc.AreaType.Point) return false;
                return true;

            case CannonballCalc.ChargeType.HE:
                if (input.damageElementType != CannonballCalc.DamageElementType.None) return false;
                if (input.damageElementTier != 0) return false;
                if (Mathf.Abs(input.damageElementMassKg) > 0.0001f) return false;
                if (input.areaType != CannonballCalc.AreaType.Sphere) return false;
                return input.explosiveTier >= 1 && input.explosiveMassKg > 0f;

            case CannonballCalc.ChargeType.EQ:
                if (input.explosiveTier < 1 || input.explosiveMassKg <= 0f) return false;
                if (input.damageElementTier < 1 || input.damageElementMassKg <= 0f) return false;

                switch (input.damageElementType)
                {
                    case CannonballCalc.DamageElementType.Pellet:
                        return input.areaType == CannonballCalc.AreaType.Sphere;

                    case CannonballCalc.DamageElementType.Fire:
                    case CannonballCalc.DamageElementType.Chemical:
                    case CannonballCalc.DamageElementType.Energy:
                        return input.areaType == CannonballCalc.AreaType.Cloud;

                    default:
                        return false;
                }

            default:
                return false;
        }
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