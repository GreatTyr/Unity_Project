using UnityEngine;

/// <summary>
/// Параметры конкретной печи.
/// </summary>
public class FurnaceCore : MonoBehaviour
{
    [Header("Параметры плавильни")]
    [Tooltip("Ёмкость печи в кг")]
    [SerializeField] private double capacityKg = 100.0;

    [Tooltip("Тир плавильни (1-10)")]
    [Range(1, 10)]
    [SerializeField] private int furnaceTier = 1;

    [Tooltip("Коэффициент эффективности (%). Заглушка.")]
    [SerializeField] private float efficiencyPercent = 100f;

    [Header("Ссылки на хранилища (legacy fallback)")]
    [SerializeField] private ResourcesStorage resourcesStorage;
    [SerializeField] private AlloyStorage alloyStorage;

    [Header("Storage Manager")]
    [SerializeField] private bool useStorageManager = true;
    [SerializeField] private StorageNode localStorageNode;
    [SerializeField] private Transform actorTransform;

    public double CapacityKg => capacityKg;
    public long CapacityGrams => (long)System.Math.Round(capacityKg * 1000.0);

    public int FurnaceTier => furnaceTier;
    public float EfficiencyPercent => efficiencyPercent;

    public ResourcesStorage Resources => ResolveResourcesStorage();
    public AlloyStorage Alloys => ResolveAlloyStorage();

    public bool HasEnoughResources(long metalGrams, int metalTier, long chemicalsGrams, long nanitesGrams, long energyCost)
    {
        var rs = ResolveResourcesStorage();
        if (rs == null) return false;

        var metalIdx = GetMetalIndex(metalTier);
        if (rs.GetGrams(metalIdx) < metalGrams) return false;

        if (chemicalsGrams > 0)
        {
            var chemIdx = GetChemicalsIndex(metalTier);
            if (rs.GetGrams(chemIdx) < chemicalsGrams) return false;
        }

        if (nanitesGrams > 0)
        {
            var nanIdx = GetNanitesIndex(metalTier);
            if (rs.GetGrams(nanIdx) < nanitesGrams) return false;
        }

        if (rs.EnergyUnits < energyCost) return false;
        return true;
    }

    public bool ExecuteSmelt(long metalGrams, int metalTier,
        long chemicalsGrams, long nanitesGrams, long energyCost,
        string alloyCode, double outputKg)
    {
        var rs = ResolveResourcesStorage();
        var als = ResolveAlloyStorage();

        if (rs == null || als == null) return false;
        if (!HasEnoughResources(metalGrams, metalTier, chemicalsGrams, nanitesGrams, energyCost))
            return false;

        var metalIdx = GetMetalIndex(metalTier);
        rs.TryRemoveGrams(metalIdx, metalGrams);

        if (chemicalsGrams > 0)
        {
            var chemIdx = GetChemicalsIndex(metalTier);
            rs.TryRemoveGrams(chemIdx, chemicalsGrams);
        }

        if (nanitesGrams > 0)
        {
            var nanIdx = GetNanitesIndex(metalTier);
            rs.TryRemoveGrams(nanIdx, nanitesGrams);
        }

        rs.TryConsumeEnergy(energyCost);

        outputKg = System.Math.Round(outputKg, 3);
        als.AddAlloy(alloyCode, outputKg);

        return true;
    }

    public long GetMetalOnStorageGrams(int tier)
    {
        var rs = ResolveResourcesStorage();
        if (rs == null) return 0;
        return rs.GetGrams(GetMetalIndex(tier));
    }

    public long GetChemicalsOnStorageGrams(int tier)
    {
        var rs = ResolveResourcesStorage();
        if (rs == null) return 0;
        return rs.GetGrams(GetChemicalsIndex(tier));
    }

    public long GetNanitesOnStorageGrams(int tier)
    {
        var rs = ResolveResourcesStorage();
        if (rs == null) return 0;
        return rs.GetGrams(GetNanitesIndex(tier));
    }

    public long GetEnergyOnStorage()
    {
        var rs = ResolveResourcesStorage();
        if (rs == null) return 0;
        return rs.EnergyUnits;
    }

    public static ResourcesStorage.ResourceIndex GetMetalIndex(int tier)
    {
        return (ResourcesStorage.ResourceIndex)(20 + tier - 1);
    }

    public static ResourcesStorage.ResourceIndex GetChemicalsIndex(int tier)
    {
        return (ResourcesStorage.ResourceIndex)(40 + tier - 1);
    }

    public static ResourcesStorage.ResourceIndex GetNanitesIndex(int tier)
    {
        return (ResourcesStorage.ResourceIndex)(50 + tier - 1);
    }

    private Transform ResolveActorTransform()
    {
        if (actorTransform != null) return actorTransform;
        if (PlayerLocator.PlayerObject != null) return PlayerLocator.PlayerObject.transform;
        return null;
    }

    private ResourcesStorage ResolveResourcesStorage()
    {
        if (useStorageManager && StorageManager.Instance != null)
        {
            if (StorageManager.Instance.TryGetResourcesStorage(
                localStorageNode,
                ResolveActorTransform(),
                StorageAccessMode.CraftConsume | StorageAccessMode.Read,
                out var rs,
                out _))
                return rs;
        }

        return resourcesStorage;
    }

    private AlloyStorage ResolveAlloyStorage()
    {
        if (useStorageManager && StorageManager.Instance != null)
        {
            if (StorageManager.Instance.TryGetAlloyStorage(
                localStorageNode,
                ResolveActorTransform(),
                StorageAccessMode.CraftProduce | StorageAccessMode.Write,
                out var a,
                out _))
                return a;
        }

        return alloyStorage;
    }
}