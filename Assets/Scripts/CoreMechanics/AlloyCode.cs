using System;
using System.Text;

/// <summary>
/// Кодирование, декодирование и валидация кода сплава.
/// Доступен из любого скрипта: AlloyCode.Encode(), AlloyCode.Decode(), AlloyCode.Validate()
/// </summary>
public static class AlloyCode
{
    public struct AlloyParams
    {
        public int tier;
        public bool useChemicals;
        public bool useNanites;
        public int kineticAbsorption;
        public float kineticResistance;
        public int thermalAbsorption;
        public float thermalResistance;
        public int chemicalAbsorption;
        public float chemicalResistance;
        public int energyAbsorption;
        public float energyResistance;
    }

    public struct ValidationResult
    {
        public bool isValid;
        public string error;
        public AlloyParams parameters;
        public int freePoints;
    }

    // ═══════════════════════ ENCODE ═══════════════════════

    public static string Encode(in AlloyParams p)
    {
        var sb = new StringBuilder(64);

        sb.Append(p.tier);
        if (p.useChemicals) sb.Append('C');
        if (p.useNanites) sb.Append('N');

        sb.Append("-K");
        sb.Append(p.kineticAbsorption);
        sb.Append('/');
        AppendResistance(sb, p.kineticResistance);

        sb.Append("-T");
        sb.Append(p.thermalAbsorption);
        sb.Append('/');
        AppendResistance(sb, p.thermalResistance);

        sb.Append("-C");
        sb.Append(p.chemicalAbsorption);
        sb.Append('/');
        AppendResistance(sb, p.chemicalResistance);

        sb.Append("-E");
        sb.Append(p.energyAbsorption);
        sb.Append('/');
        AppendResistance(sb, p.energyResistance);

        return sb.ToString();
    }

    private static void AppendResistance(StringBuilder sb, float resistance)
    {
        int val = (int)Math.Round(resistance * 10f);
        if (val < 0)
        {
            sb.Append('m');
            sb.Append((-val).ToString("D4"));
        }
        else
        {
            sb.Append(val.ToString("D3"));
        }
    }

    // ═══════════════════════ DECODE ═══════════════════════

    public static bool Decode(string code, out AlloyParams p)
    {
        p = default;
        if (string.IsNullOrEmpty(code)) return false;

        try
        {
            int pos = 0;

            p.tier = ReadInt(code, ref pos);
            if (p.tier < 1 || p.tier > 10) return false;

            while (pos < code.Length && code[pos] != '-')
            {
                if (code[pos] == 'C') p.useChemicals = true;
                else if (code[pos] == 'N') p.useNanites = true;
                else return false;
                pos++;
            }

            if (!Expect(code, ref pos, '-')) return false;
            if (!Expect(code, ref pos, 'K')) return false;
            p.kineticAbsorption = ReadInt(code, ref pos);
            if (!Expect(code, ref pos, '/')) return false;
            p.kineticResistance = ReadResistance(code, ref pos);

            if (!Expect(code, ref pos, '-')) return false;
            if (!Expect(code, ref pos, 'T')) return false;
            p.thermalAbsorption = ReadInt(code, ref pos);
            if (!Expect(code, ref pos, '/')) return false;
            p.thermalResistance = ReadResistance(code, ref pos);

            if (!Expect(code, ref pos, '-')) return false;
            if (!Expect(code, ref pos, 'C')) return false;
            p.chemicalAbsorption = ReadInt(code, ref pos);
            if (!Expect(code, ref pos, '/')) return false;
            p.chemicalResistance = ReadResistance(code, ref pos);

            if (!Expect(code, ref pos, '-')) return false;
            if (!Expect(code, ref pos, 'E')) return false;
            p.energyAbsorption = ReadInt(code, ref pos);
            if (!Expect(code, ref pos, '/')) return false;
            p.energyResistance = ReadResistance(code, ref pos);

            return pos == code.Length;
        }
        catch
        {
            return false;
        }
    }

    private static int ReadInt(string s, ref int pos)
    {
        int start = pos;
        while (pos < s.Length && char.IsDigit(s[pos])) pos++;
        if (pos == start) throw new FormatException();
        return int.Parse(s.Substring(start, pos - start));
    }

    private static float ReadResistance(string s, ref int pos)
    {
        bool negative = false;
        if (pos < s.Length && s[pos] == 'm')
        {
            negative = true;
            pos++;
        }

        int start = pos;
        while (pos < s.Length && char.IsDigit(s[pos])) pos++;
        if (pos == start) throw new FormatException();

        int raw = int.Parse(s.Substring(start, pos - start));
        float val = raw / 10f;
        return negative ? -val : val;
    }

    private static bool Expect(string s, ref int pos, char c)
    {
        if (pos >= s.Length || s[pos] != c) return false;
        pos++;
        return true;
    }

    // ═══════════════════════ VALIDATE ═══════════════════════

    public static ValidationResult Validate(string code)
    {
        var r = new ValidationResult();

        if (!Decode(code, out AlloyParams p))
        {
            r.error = "Некорректный формат кода";
            return r;
        }

        r.parameters = p;
        float maxRes = Smelting.MaxResistance(p.tier);

        // Макс. сопротивления
        if (p.kineticResistance > maxRes + 0.01f)
        { r.error = "Кинетическое сопротивление превышает максимум"; return r; }
        if (p.thermalResistance > maxRes + 0.01f)
        { r.error = "Термическое сопротивление превышает максимум"; return r; }
        if (p.chemicalResistance > maxRes + 0.01f)
        { r.error = "Химическое сопротивление превышает максимум"; return r; }
        if (p.energyResistance > maxRes + 0.01f)
        { r.error = "Энергетическое сопротивление превышает максимум"; return r; }

        // Мин. сопротивления
        float minKT = p.useNanites ? -200f : 0f;
        float minCE = (p.useNanites && p.useChemicals) ? -200f : 0f;

        if (p.kineticResistance < minKT - 0.01f)
        { r.error = "Кинетическое сопротивление ниже минимума"; return r; }
        if (p.thermalResistance < minKT - 0.01f)
        { r.error = "Термическое сопротивление ниже минимума"; return r; }
        if (p.chemicalResistance < minCE - 0.01f)
        { r.error = "Химическое сопротивление ниже минимума"; return r; }
        if (p.energyResistance < minCE - 0.01f)
        { r.error = "Энергетическое сопротивление ниже минимума"; return r; }

        // Без химикатов — хим/энерг параметры = 0
        if (!p.useChemicals)
        {
            if (p.chemicalAbsorption != 0 || Math.Abs(p.chemicalResistance) > 0.01f ||
                p.energyAbsorption != 0 || Math.Abs(p.energyResistance) > 0.01f)
            {
                r.error = "Хим/энерг параметры заданы без химикатов";
                return r;
            }
        }

        // Без нанитов — нет отрицательных
        if (!p.useNanites)
        {
            if (p.kineticResistance < -0.01f || p.thermalResistance < -0.01f ||
                p.chemicalResistance < -0.01f || p.energyResistance < -0.01f)
            {
                r.error = "Отрицательные сопротивления без нанитов";
                return r;
            }
        }

        // Поглощения >= 0
        if (p.kineticAbsorption < 0 || p.thermalAbsorption < 0 ||
            p.chemicalAbsorption < 0 || p.energyAbsorption < 0)
        {
            r.error = "Поглощение не может быть отрицательным";
            return r;
        }

        // Свободные очки
        int baseP = Smelting.BasePoints(p.tier);
        int totalAbsorb = p.kineticAbsorption + p.thermalAbsorption +
                          p.chemicalAbsorption + p.energyAbsorption;
        int resistCost = Smelting.ResistancePointsCost(p.kineticResistance) +
                         Smelting.ResistancePointsCost(p.thermalResistance) +
                         Smelting.ResistancePointsCost(p.chemicalResistance) +
                         Smelting.ResistancePointsCost(p.energyResistance);

        int free = baseP - totalAbsorb - resistCost;
        r.freePoints = Math.Max(0, free);

        if (free < 0)
        {
            r.error = $"Перерасход очков на {-free}";
            return r;
        }

        r.isValid = true;
        return r;
    }

    public static ValidationResult ValidateForFurnace(string code, int furnaceTier)
    {
        var r = Validate(code);
        if (!r.isValid) return r;

        if (r.parameters.tier > furnaceTier)
        {
            r.isValid = false;
            r.error = $"Тир сплава ({r.parameters.tier}) превышает тир плавильни ({furnaceTier})";
        }
        return r;
    }
}