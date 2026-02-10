using System;

/// <summary>
/// Статическая логика плавки. Доступна из любого скрипта.
/// Путь: Assets/Scripts/CoreMechanics/Smelting.cs
/// </summary>
public static class Smelting
{
    // ═══════════════════════ БАЗОВЫЕ ФОРМУЛЫ ═══════════════════════

    public static int BasePoints(int metalTier)
    {
        return 300 * Math.Clamp(metalTier, 1, 10);
    }

    public static float MaxResistance(int metalTier)
    {
        return 45f + 5f * Math.Clamp(metalTier, 1, 10);
    }

    public static long ChemicalsGrams(long metalGrams)
    {
        return (long)Math.Round(metalGrams * 0.2);
    }

    public static long NanitesGrams(long metalGrams)
    {
        return (long)Math.Round(metalGrams * 0.1);
    }

    public static long MaxMetalGrams(long capacityGrams, bool useChemicals, bool useNanites)
    {
        double divisor = 1.0;
        if (useChemicals) divisor += 0.2;
        if (useNanites) divisor += 0.1;
        return Math.Max(0, (long)Math.Floor(capacityGrams / divisor));
    }

    public static long EnergyCost(long capacityGrams, int furnaceTier,
        long metalGrams, long chemGrams, long nanGrams, int metalTier)
    {
        double fCoeff = TierCoeffs.Get(furnaceTier);
        double mCoeff = TierCoeffs.Get(metalTier);
        double capKg = capacityGrams / 1000.0;
        double inputKg = (metalGrams + chemGrams + nanGrams) / 1000.0;
        return (long)Math.Ceiling(capKg * fCoeff + inputKg * mCoeff);
    }

    public static long OutputAlloyGrams(long metalGrams, long chemGrams,
        long nanGrams, int furnaceTier, int metalTier)
    {
        long total = metalGrams + chemGrams + nanGrams;
        double pct = 45.0 + 5.0 * furnaceTier - 5.0 * metalTier;
        pct = Math.Max(0.0, pct);
        return (long)Math.Floor(total * pct / 100.0);
    }

    // ═══════════════════════ СТОИМОСТЬ СОПРОТИВЛЕНИЙ ═══════════════════════

    /// <summary>
    /// Стоимость сопротивления в очках.
    /// Положительный результат = расход очков, отрицательный = возврат.
    /// </summary>
    public static int ResistancePointsCost(float resistance)
    {
        if (resistance >= 0f)
        {
            return (int)Math.Round(resistance * 10f);
        }
        return -NegativeResistanceBonusPoints(resistance);
    }

    /// <summary>
    /// Бонусные очки за отрицательное сопротивление (resistance <= 0).
    /// Работает в миллипроцентах (0.001%) для избежания погрешностей float.
    /// </summary>
    public static int NegativeResistanceBonusPoints(float resistance)
    {
        if (resistance >= 0f) return 0;

        int absMillis = (int)Math.Round(-resistance * 1000.0);
        int points = 0;

        // 0..50% (0..50000 милли): 1 очко за 200 милли (0.2%)
        if (absMillis > 0)
        {
            int inRange = Math.Min(absMillis, 50000);
            points += inRange / 200;
        }

        // 50..100% (50000..100000 милли): тройной паттерн (333+333+334 = 1000 милли = 3 очка)
        if (absMillis > 50000)
        {
            int inRange = Math.Min(absMillis, 100000) - 50000;
            int fullPercents = inRange / 1000;
            points += fullPercents * 3;

            int rem = inRange - fullPercents * 1000;
            if (rem >= 333)
            {
                points++;
                rem -= 333;
                if (rem >= 333)
                {
                    points++;
                }
            }
        }

        // 100..150% (100000..150000 милли): 1 очко за 500 милли (0.5%)
        if (absMillis > 100000)
        {
            int inRange = Math.Min(absMillis, 150000) - 100000;
            points += inRange / 500;
        }

        // 150..200% (150000..200000 милли): 1 очко за 1000 милли (1.0%)
        if (absMillis > 150000)
        {
            int inRange = Math.Min(absMillis, 200000) - 150000;
            points += inRange / 1000;
        }

        return points;
    }

    // ═══════════════════════ ШАГИ КНОПОК ═══════════════════════

    /// <summary>
    /// Размер одного шага кнопки «−» для сопротивления при текущем значении.
    /// При значении > 0: шаг 0.1%. При значении <= 0: шаг зависит от зоны, даёт ровно 1 очко.
    /// Возвращает положительное число (величина уменьшения).
    /// </summary>
    public static float StepDown(float current)
    {
        if (current > 0.001f)
            return 0.1f;

        int absMillis = (int)Math.Round(-current * 1000.0);

        if (absMillis < 50000)
            return 0.2f;
        if (absMillis < 100000)
            return TripletStep(absMillis - 50000);
        if (absMillis < 150000)
            return 0.5f;
        return 1.0f;
    }

    /// <summary>
    /// Размер одного шага кнопки «+» для сопротивления при текущем значении.
    /// При значении >= 0: шаг 0.1%. При значении < 0: шаг зависит от зоны.
    /// Возвращает положительное число (величина увеличения).
    /// </summary>
    public static float StepUp(float current)
    {
        if (current >= -0.001f)
            return 0.1f;

        int absMillis = (int)Math.Round(-current * 1000.0);

        if (absMillis <= 50000)
            return 0.2f;
        if (absMillis <= 100000)
            return TripletStepReverse(absMillis - 50000);
        if (absMillis <= 150000)
            return 0.5f;
        return 1.0f;
    }

    /// <summary>
    /// Размер шага в тройной зоне при движении вниз.
    /// posInZoneMillis: позиция внутри зоны 0..50000 (в миллипроцентах).
    /// Паттерн: 0.333%, 0.333%, 0.334%, 0.333%, 0.333%, 0.334%...
    /// </summary>
    private static float TripletStep(int posInZoneMillis)
    {
        int inPercent = posInZoneMillis % 1000;

        // 0 → шаг 0.333 (первый в тройке)
        // 333 → шаг 0.333 (второй)
        // 666 → шаг 0.334 (третий, завершает до целого %)
        if (inPercent < 10)
            return 0.333f;
        if (inPercent < 340)
            return 0.333f;
        return 0.334f;
    }

    /// <summary>
    /// Размер шага в тройной зоне при движении вверх (обратный).
    /// </summary>
    private static float TripletStepReverse(int posInZoneMillis)
    {
        int inPercent = posInZoneMillis % 1000;

        // На 0 (целый %) → последний шаг был 0.334
        // На 666 → последний шаг был 0.333
        // На 333 → последний шаг был 0.333
        if (inPercent < 10)
            return 0.334f;
        if (inPercent < 340)
            return 0.333f;
        return 0.333f;
    }

    /// <summary>
    /// Применить N шагов вниз. Возвращает новое значение.
    /// pointsDelta: + = получено очков, − = потрачено.
    /// </summary>
    public static float ApplyStepsDown(float current, float min, int steps, out int pointsDelta)
    {
        pointsDelta = 0;
        float val = current;

        for (int i = 0; i < steps; i++)
        {
            float step = StepDown(val);
            float next = val - step;
            next = (float)(Math.Round(next * 1000.0) / 1000.0);

            if (next < min - 0.0001f) break;

            if (val > 0.001f)
                pointsDelta--;
            else
                pointsDelta++;

            val = next;
        }
        return val;
    }

    /// <summary>
    /// Применить N шагов вверх. Возвращает новое значение.
    /// pointsDelta: − = потрачено, + = возвращено.
    /// </summary>
    public static float ApplyStepsUp(float current, float max, int freePoints, int steps, out int pointsDelta)
    {
        pointsDelta = 0;
        float val = current;

        for (int i = 0; i < steps; i++)
        {
            float step = StepUp(val);
            float next = val + step;
            next = (float)(Math.Round(next * 1000.0) / 1000.0);

            if (next > max + 0.0001f) break;

            if (val < -0.001f)
            {
                pointsDelta--;
            }
            else
            {
                if (freePoints + pointsDelta <= 0) break;
                pointsDelta--;
            }

            val = next;
        }
        return val;
    }

    // ═══════════════════════ СВОБОДНЫЕ ОЧКИ ═══════════════════════

    public static int CalculateFreePoints(int metalTier,
        int kinA, float kinR, int thermA, float thermR,
        int chemA, float chemR, int enA, float enR)
    {
        int baseP = BasePoints(metalTier);
        int absorb = kinA + thermA + chemA + enA;
        int resist = ResistancePointsCost(kinR) + ResistancePointsCost(thermR) +
                     ResistancePointsCost(chemR) + ResistancePointsCost(enR);
        return Math.Max(0, baseP - absorb - resist);
    }
}