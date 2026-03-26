using System;
using System.Globalization;
using UnityEngine;

/// <summary>
/// Каноническая сборка, парсинг и строгая валидация кода боеприпаса.
/// Код описывает только свойства боеприпаса и не содержит параметров конкретного ствола.
/// </summary>
public static class AmmoValidator
{
    // Формат:
    //  0  prefix = S
    //  1  chargeType
    //  2  shellTier
    //  3  diameterMm
    //  4  lengthMm
    //  5  totalProjectileMassKg
    //  6  shellMassKg
    //  7  shellStrength
    //  8  explosiveTier
    //  9  explosiveMassKg
    // 10  explosivePower
    // 11  damageElementType
    // 12  buckshotCount
    // 13  damageElementTier
    // 14  damageElementMassKg
    // 15  buckshotSingleMassKg
    // 16  areaType
    // 17  damageRadius
    // 18  areaPenetration
    // 19  areaDamage
    // 20  coneAngleDeg
    // 21  buckshotSpreadAngleDeg
    // 22  fuzeType
    // 23  propellantTier
    // 24  propellantMassKg
    // 25  propulsionForce
    // 26  caseTier
    // 27  caseMassKg
    // 28  caseStrength
    // 29  totalAmmoMassKg
    // 30  effectiveGravity

    public const int PartsCount = 31;
    public const string Prefix = "S";

    public static string BuildCode(AmmoCalc.AmmoOutput o)
    {
        if (o == null) return string.Empty;

        string[] p = new string[PartsCount];

        p[0] = Prefix;
        p[1] = ((int)o.chargeType).ToString();
        p[2] = o.shellTier.ToString();
        p[3] = FN(o.diameterMm);
        p[4] = FN(o.lengthMm);
        p[5] = FN(o.totalProjectileMassKg);
        p[6] = FN(o.shellMassKg);
        p[7] = FN(o.shellStrength);
        p[8] = o.explosiveTier.ToString();
        p[9] = FN(o.explosiveMassKg);
        p[10] = FN(o.explosivePower);
        p[11] = ((int)o.damageElementType).ToString();
        p[12] = o.buckshotCount.ToString();
        p[13] = o.damageElementTier.ToString();
        p[14] = FN(o.damageElementMassKg);
        p[15] = FN(o.buckshotSingleMassKg);
        p[16] = ((int)o.areaType).ToString();
        p[17] = FN(o.damageRadius);
        p[18] = FN(o.areaPenetration);
        p[19] = FN(o.areaDamage);
        p[20] = FN(o.coneAngleDeg);
        p[21] = FN(o.buckshotSpreadAngleDeg);
        p[22] = ((int)o.fuzeType).ToString();
        p[23] = o.propellantTier.ToString();
        p[24] = FN(o.propellantMassKg);
        p[25] = FN(o.propulsionForce);
        p[26] = o.caseTier.ToString();
        p[27] = FN(o.caseMassKg);
        p[28] = FN(o.caseStrength);
        p[29] = FN(o.totalAmmoMassKg);
        p[30] = FN(o.effectiveGravity);

        return string.Join("-", p);
    }

    public static bool TryParseCode(string code, out AmmoCalc.AmmoInput input, out string error)
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
            error = "Неверное количество элементов в коде боеприпаса.";
            return false;
        }

        if (p[0] != Prefix)
        {
            error = "Неверный префикс кода боеприпаса.";
            return false;
        }

        var parsed = new AmmoCalc.AmmoInput();

        if (!int.TryParse(p[1], out int chargeTypeInt) ||
            !Enum.IsDefined(typeof(AmmoCalc.ChargeType), chargeTypeInt))
        {
            error = "Неверный тип боеприпаса.";
            return false;
        }
        parsed.chargeType = (AmmoCalc.ChargeType)chargeTypeInt;

        if (!int.TryParse(p[2], out parsed.shellTier)) { error = "Неверный тир оболочки."; return false; }
        if (!TryParseFloatAny(p[3], out parsed.diameterMm)) { error = "Неверный диаметр."; return false; }
        if (!TryParseFloatAny(p[4], out parsed.lengthMm)) { error = "Неверная длина."; return false; }

        if (!int.TryParse(p[8], out parsed.explosiveTier)) { error = "Неверный тир разрывного заряда."; return false; }
        if (!TryParseFloatAny(p[9], out parsed.explosiveMassKg)) { error = "Неверная масса разрывного заряда."; return false; }

        if (!int.TryParse(p[11], out int deInt) ||
            !Enum.IsDefined(typeof(AmmoCalc.DamageElementType), deInt))
        {
            error = "Неверный тип поражающего элемента.";
            return false;
        }
        parsed.damageElementType = (AmmoCalc.DamageElementType)deInt;

        if (!int.TryParse(p[12], out parsed.buckshotCount)) { error = "Неверное количество картечин."; return false; }
        if (!int.TryParse(p[13], out parsed.damageElementTier)) { error = "Неверный тир поражающего элемента."; return false; }
        if (!TryParseFloatAny(p[14], out parsed.damageElementMassKg)) { error = "Неверная масса поражающего элемента."; return false; }

        if (!int.TryParse(p[16], out int areaInt) ||
            !Enum.IsDefined(typeof(AmmoCalc.AreaType), areaInt))
        {
            error = "Неверный тип области поражения.";
            return false;
        }
        parsed.areaType = (AmmoCalc.AreaType)areaInt;

        if (!int.TryParse(p[22], out int fuzeInt) ||
            !Enum.IsDefined(typeof(AmmoCalc.FuzeType), fuzeInt))
        {
            error = "Неверный тип взрывателя.";
            return false;
        }
        parsed.fuzeType = (AmmoCalc.FuzeType)fuzeInt;

        if (!int.TryParse(p[23], out parsed.propellantTier)) { error = "Неверный тир метательного заряда."; return false; }
        if (!TryParseFloatAny(p[24], out parsed.propellantMassKg)) { error = "Неверная масса метательного заряда."; return false; }

        if (!int.TryParse(p[26], out parsed.caseTier)) { error = "Неверный тир гильзы."; return false; }
        if (!TryParseFloatAny(p[27], out parsed.caseMassKg)) { error = "Неверная масса гильзы."; return false; }

        parsed.craftCount = 1;

        AmmoCalc.NormalizeInput(parsed);
        AmmoCalc.AmmoOutput calc = AmmoCalc.Calculate(parsed);

        if (calc == null || !string.IsNullOrEmpty(calc.error))
        {
            error = calc != null ? calc.error : "Ошибка расчёта боеприпаса.";
            return false;
        }

        string canonical = BuildCode(calc);
        if (!string.Equals(code, canonical, StringComparison.Ordinal))
        {
            error = "Код боеприпаса не соответствует каноническому формату.";
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
        float rounded = AmmoCalc.Ceil3(v);
        if (rounded == Mathf.Floor(rounded) && rounded < 1000000f)
            return ((int)rounded).ToString();
        return rounded.ToString("F3", CultureInfo.InvariantCulture).Replace('.', ',');
    }
}