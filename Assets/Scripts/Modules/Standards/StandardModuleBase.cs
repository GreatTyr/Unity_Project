using UnityEngine;
using System.Collections.Generic;

[ExecuteAlways]
public abstract class StandardModuleBase : MonoBehaviour
{
    [Header("Identity")]
    [Range(1, 10)] public int ModuleTier = 1;

    [Header("Faction")]
    [SerializeField] protected string factionShortName = "";

    [Header("Blueprint")]
    [Tooltip("Уникальный ID чертежа внутри фракции (например: 001)")]
    [SerializeField] protected string blueprintId = "001";

    [Header("Volume & Fill")]
    [Range(0f, 100f)] public float VolumeCoefficientPercent = 100f;

    [Header("Crafting Costs (Per Liter of Effective Volume)")]
    [Tooltip("Список ресурсов, необходимых для создания 1 литра (дм3) внутренностей модуля")]
    public List<ResourceCostPerLiter> InternalResourceCosts = new List<ResourceCostPerLiter>();

    [Header("Crafting Parameters")]
    [Tooltip("Коэффициент крафта модуля для формулы времени.")]
    [Min(0f)] public float CraftCoefficient = 1f;

    [Header("Module Capabilities")]
    public bool CanTurnOnOff = true;
    [Min(0f)] public float TurnOnOffTime = 1f;

    public bool CanPulseMode = false;
    [Min(0f)] public float PulseInterval = 0f;

    public bool IsControllable = false;

    [Header("Destruction")]
    public bool IsVolatile = false;
    public DamageType ExplosionDamageType = DamageType.Kinetic;

    [SerializeField, HideInInspector] protected float length = 1f;
    [SerializeField, HideInInspector] protected float width = 1f;
    [SerializeField, HideInInspector] protected float height = 1f;

    [SerializeField, HideInInspector] protected float aabbVolume;
    [SerializeField, HideInInspector] protected float realVolume;
    [SerializeField, HideInInspector] protected float effectiveVolume;
    [SerializeField, HideInInspector] protected float massKg;

    public abstract string ModuleType { get; }
    public string FactionShortName => factionShortName;
    public string BlueprintId => blueprintId;
    public string FactionFullName
    {
        get
        {
            var db = FactionDatabase.Instance;
            if (db == null || string.IsNullOrEmpty(factionShortName)) return factionShortName;
            return db.GetFullName(factionShortName);
        }
    }

    public float LengthMeters => length;
    public float WidthMeters => width;
    public float HeightMeters => height;
    public float AABBVolumeM3 => aabbVolume;
    public float RealVolumeM3 => realVolume;
    public float EffectiveVolumeM3 => effectiveVolume;
    public float MassKg => massKg;

    protected const float EPS_ROUND = 1e-7f;

    public void SetFaction(string shortName) => factionShortName = shortName ?? "";

    protected virtual void OnEnable() => RecalculateAll();

    protected virtual void OnValidate()
    {
        ModuleTier = Mathf.Clamp(ModuleTier, 1, 10);
        VolumeCoefficientPercent = Mathf.Clamp(VolumeCoefficientPercent, 0f, 100f);
        CraftCoefficient = Mathf.Max(0.01f, CraftCoefficient);
        RecalculateAll();
    }

#if UNITY_EDITOR
    protected virtual void Update()
    {
        if (!Application.isPlaying) RecalculateAll();
    }
#endif

    public void RecalculateAll()
    {
        MeasureWorldDimensions();
        ComputeVolumesAndMass();
        ComputeSpecificOutputs();
        RoundAndStoreBaseResults();
        RoundAndStoreSpecificResults();
    }

    private void MeasureWorldDimensions()
    {
        Vector3 size = ModuleMeasurer.GetSize(this.gameObject);
        length = size.x; height = size.y; width = size.z;
    }

    private void ComputeVolumesAndMass()
    {
        aabbVolume = Mathf.Max(0f, length) * Mathf.Max(0f, width) * Mathf.Max(0f, height);
        realVolume = aabbVolume * Mathf.Clamp01(VolumeCoefficientPercent / 100f);
        effectiveVolume = realVolume;

        // НОВОЕ: Подсчет базовой массы внутренностей через рецепт
        float totalGramsPerLiter = 0f;
        if (InternalResourceCosts != null)
        {
            foreach (var cost in InternalResourceCosts)
                totalGramsPerLiter += cost.gramsPerLiter;
        }

        // Масса = Объем(м3) * 1000 (перевод в литры) * totalGramsPerLiter / 1000 (перевод в кг)
        massKg = effectiveVolume * totalGramsPerLiter;
    }

    protected abstract void ComputeSpecificOutputs();

    private void RoundAndStoreBaseResults()
    {
        aabbVolume = RoundToWithEps(aabbVolume, 6);
        realVolume = RoundToWithEps(realVolume, 6);
        effectiveVolume = RoundToWithEps(effectiveVolume, 6);
        massKg = RoundToWithEps(massKg, 3);
    }

    protected abstract void RoundAndStoreSpecificResults();

    protected static float RoundToWithEps(float v, int d)
    {
        float mul = Mathf.Pow(10f, d);
        return Mathf.Round((v + EPS_ROUND) * mul) / mul;
    }
}