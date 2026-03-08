using UnityEngine;

public static class TierCoeffs
{
    // fixed-point scale = 100 (2 знака после запятой). Хранит 10 элементов, индекс 0 -> tier1.
    private static readonly ushort[] R = { 100, 167, 300, 500, 800, 1300, 2200, 3600, 6000, 10000 };
    private const float INV_SCALE = 1f / 100f;

    // Быстрый доступ: tier в диапазоне 1..10; значения вне диапазона зажимаются.
    public static float Get(int tier)
    {
        int i = tier - 1;
        if ((uint)i >= (uint)R.Length) i = (i < 0) ? 0 : R.Length - 1;
        return R[i] * INV_SCALE;
    }
}