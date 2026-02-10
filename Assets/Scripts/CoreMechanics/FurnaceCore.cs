using UnityEngine;

/// <summary>
/// Параметры конкретной печи. Вешается на GameObject печи.
/// Путь: Assets/Scripts/CoreMechanics/FurnaceCore.cs
/// </summary>
public class FurnaceCore : MonoBehaviour
{
    [Header("Параметры плавильни")]

    [Tooltip("Ёмкость печи в кг")]
    [SerializeField]
    private double capacityKg = 100.0;

    [Tooltip("Тир плавильни (1-10)")]
    [Range(1, 10)]
    [SerializeField]
    private int furnaceTier = 1;

    [Tooltip("Коэффициент эффективности (%). Заглушка.")]
    [SerializeField]
    private float efficiencyPercent = 100f;

    [Header("Ссылки на хранилища")]

    [Tooltip("Склад ресурсов, из которого берутся ресурсы")]
    [SerializeField]
    private ResourcesStorage resourcesStorage;

    [Tooltip("Склад сплавов, куда поступают готовые сплавы")]
    [SerializeField]
    private AlloyStorage alloyStorage;

    // ─────────────────── Публичные свойства ───────────────────

    public double CapacityKg => capacityKg;
    public long CapacityGrams => (long)System.Math.Round(capacityKg * 1000.0);

    public int FurnaceTier => furnaceTier;
    public float EfficiencyPercent => efficiencyPercent;

    public ResourcesStorage Resources => resourcesStorage;
    public AlloyStorage Alloys => alloyStorage;

    // ─────────────────── Проверка наличия ресурсов ───────────────────

    /// <summary>
    /// Проверить, хватает ли всех ресурсов для плавки.
    /// </summary>
    public bool HasEnoughResources(long metalGrams, int metalTier,
        long chemicalsGrams, long nanitesGrams, long energyCost)
    {
        if (resourcesStorage == null) return false;

        var metalIdx = GetMetalIndex(metalTier);
        if (resourcesStorage.GetGrams(metalIdx) < metalGrams) return false;

        if (chemicalsGrams > 0)
        {
            var chemIdx = GetChemicalsIndex(metalTier);
            if (resourcesStorage.GetGrams(chemIdx) < chemicalsGrams) return false;
        }

        if (nanitesGrams > 0)
        {
            var nanIdx = GetNanitesIndex(metalTier);
            if (resourcesStorage.GetGrams(nanIdx) < nanitesGrams) return false;
        }

        if (resourcesStorage.EnergyUnits < energyCost) return false;

        return true;
    }

    /// <summary>
    /// Выполнить плавку: списать ресурсы, добавить сплав.
    /// Возвращает true при успехе.
    /// </summary>
    public bool ExecuteSmelt(long metalGrams, int metalTier,
        long chemicalsGrams, long nanitesGrams, long energyCost,
        string alloyCode, double outputKg)
    {
        if (resourcesStorage == null || alloyStorage == null) return false;

        if (!HasEnoughResources(metalGrams, metalTier, chemicalsGrams, nanitesGrams, energyCost))
            return false;

        // Списание
        var metalIdx = GetMetalIndex(metalTier);
        resourcesStorage.TryRemoveGrams(metalIdx, metalGrams);

        if (chemicalsGrams > 0)
        {
            var chemIdx = GetChemicalsIndex(metalTier);
            resourcesStorage.TryRemoveGrams(chemIdx, chemicalsGrams);
        }

        if (nanitesGrams > 0)
        {
            var nanIdx = GetNanitesIndex(metalTier);
            resourcesStorage.TryRemoveGrams(nanIdx, nanitesGrams);
        }

        resourcesStorage.TryConsumeEnergy(energyCost);

        // Добавление сплава
        // Округляем до 3 знаков
        outputKg = System.Math.Round(outputKg, 3);
        alloyStorage.AddAlloy(alloyCode, outputKg);

        return true;
    }

    // ─────────────────── Количество ресурсов на складе ───────────────────

    public long GetMetalOnStorageGrams(int tier)
    {
        if (resourcesStorage == null) return 0;
        return resourcesStorage.GetGrams(GetMetalIndex(tier));
    }

    public long GetChemicalsOnStorageGrams(int tier)
    {
        if (resourcesStorage == null) return 0;
        return resourcesStorage.GetGrams(GetChemicalsIndex(tier));
    }

    public long GetNanitesOnStorageGrams(int tier)
    {
        if (resourcesStorage == null) return 0;
        return resourcesStorage.GetGrams(GetNanitesIndex(tier));
    }

    public long GetEnergyOnStorage()
    {
        if (resourcesStorage == null) return 0;
        return resourcesStorage.EnergyUnits;
    }

    // ─────────────────── Хелперы индексов ───────────────────

    public static ResourcesStorage.ResourceIndex GetMetalIndex(int tier)
    {
        // Metal T1 = M1 = index 20, tier 1-based
        return (ResourcesStorage.ResourceIndex)(20 + tier - 1);
    }

    public static ResourcesStorage.ResourceIndex GetChemicalsIndex(int tier)
    {
        // Chemicals T1 = C1 = index 40
        return (ResourcesStorage.ResourceIndex)(40 + tier - 1);
    }

    public static ResourcesStorage.ResourceIndex GetNanitesIndex(int tier)
    {
        // Nanites T1 = N1 = index 50
        return (ResourcesStorage.ResourceIndex)(50 + tier - 1);
    }
}