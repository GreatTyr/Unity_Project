using UnityEngine;

// ScriptableObject для хранения коэффициентов тиров (10 тиров).
// Коэффициенты хранятся в fixed-point формате: ushort values, scale = 100 (т.е. 1.67 -> 167).
// Это экономно по памяти и даёт точность до 0.01. Если потребуется больше точности, увеличьте SCALE.
[CreateAssetMenu(fileName = "TierCoeffs", menuName = "Config/TierCoeffs", order = 0)]
public sealed class TierCoeffs : ScriptableObject
{
    // Количество тиров фиксировано = 10
    public const int TIERS_COUNT = 10;

    // Масштаб фикcед-пойнт. 100 -> два знака после запятой.
    private const int SCALE = 100;

    // Храним как ushort (0..65535). Значение в единицах 1/SCALE.
    // Индекс 0 соответствует Т1, индекс 9 -> Т10.
    [SerializeField]
    [Tooltip("Коэффициенты тиров в fixed-point (scale = 100). Индекс 0 = Т1, индекс 9 = Т10.")]
    private ushort[] coeffs = new ushort[TIERS_COUNT]
    {
        100,   // T1 = 1.00
        167,   // T2 = 1.67
        300,   // T3 = 3.00
        500,   // T4 = 5.00
        800,   // T5 = 8.00
        1300,  // T6 = 13.00
        2200,  // T7 = 22.00
        3600,  // T8 = 36.00
        6000,  // T9 = 60.00
        10000  // T10 = 100.00
    };

    // Быстрая валидация (в редакторе или при загрузке)
    private void OnValidate()
    {
        if (coeffs == null || coeffs.Length != TIERS_COUNT)
        {
            coeffs = new ushort[TIERS_COUNT];
        }
    }

    // Получить raw значение (ushort, fixed-point)
    public ushort GetRaw(int тир)
    {
        int idx = ValidateAndIndex(тир);
        return coeffs[idx];
    }

    // Получить как float (декодированное значение)
    public float GetFloat(int тир)
    {
        int idx = ValidateAndIndex(тир);
        return coeffs[idx] / (float)SCALE;
    }

    // Получить как double (если нужна большая точность для вычислений)
    public double GetDouble(int тир)
    {
        int idx = ValidateAndIndex(тир);
        return coeffs[idx] / (double)SCALE;
    }

    // Вспомогательная проверка и перевод тир в индекс (тир ожидается 1..10)
    private int ValidateAndIndex(int тир)
    {
        if (тир < 1) тир = 1;
        if (тир > TIERS_COUNT) тир = TIERS_COUNT;
        return тир - 1;
    }

    // Опционально: установка значения (в редакторе или коде) в float
    public void SetFloat(int тир, float value)
    {
        int idx = ValidateAndIndex(тир);
        int raw = Mathf.RoundToInt(value * SCALE);
        if (raw < 0) raw = 0;
        if (raw > ushort.MaxValue) raw = ushort.MaxValue;
        coeffs[idx] = (ushort)raw;
    }

    // Опционально: установка raw значения
    public void SetRaw(int тир, ushort rawValue)
    {
        int idx = ValidateAndIndex(тир);
        coeffs[idx] = rawValue;
    }
}