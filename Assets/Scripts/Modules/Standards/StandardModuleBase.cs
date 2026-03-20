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
    [Header("Fill Factor")]
    [Tooltip("Процент эффективного объёма, занятый механизмами/содержимым. 0% = пустой бак, 100% = забит полностью.")]
    [Range(0, 100)] public int ConstantFillPercent = 100;

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

    [Header("Build Visual")]
    [Tooltip("Дополнительный визуальный поворот модуля при строительстве. Не влияет на footprint, только на ghost и установленную модель.")]
    [Range(0f, 270f)]
    public float BuildVisualYawOffset = 0f;

    [Tooltip("Локальное ВИЗУАЛЬНОЕ смещение модуля относительно логической точки размещения. " +
             "Положительные значения двигают объект в +X / +Y / +Z.")]
    public Vector3 BuildAnchorLocal = Vector3.zero;

    [Tooltip("Локальная клетка внутри неразвёрнутого footprint-а, считающаяся логической anchor-клеткой. " +
             "Именно она будет привязываться к anchor cell сетки при UseBuildAnchorPlacement.")]
    public Vector2Int BuildAnchorCellLocal = Vector2Int.zero;

    [Tooltip("Если включено, placement будет строиться от центра anchor cell, " +
         "а BuildAnchorLocal станет логической опорной точкой модуля. " +
         "Если выключено — используется старая legacy-модель от центра footprint-а.")]
    [SerializeField, HideInInspector]
    private bool useBuildAnchorPlacement = true;

    public bool UseBuildAnchorPlacement => true;

    // НОВОЕ: Коэффициенты взрыва
    [Min(0f)] public float ExplosionRadiusCoefficient = 0.1f;
    [Min(0f)] public float ExplosionPenetrationCoefficient = 0.1f;
    [Min(0f)] public float ExplosionDamageCoefficient = 0.1f;

    [SerializeField, HideInInspector] protected float length = 1f;
    [SerializeField, HideInInspector] protected float width = 1f;
    [SerializeField, HideInInspector] protected float height = 1f;

    [SerializeField, HideInInspector] protected float aabbVolume;
    [SerializeField, HideInInspector] protected float realVolume;
    [SerializeField, HideInInspector] protected float effectiveVolume;
    [SerializeField, HideInInspector] protected float massKg;
    [SerializeField, HideInInspector] protected int fillPercentUsed;

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
    public int FillPercentUsed => fillPercentUsed;


    protected const float EPS_ROUND = 1e-7f;

    public void SetFaction(string shortName) => factionShortName = shortName ?? "";

    protected virtual void OnEnable() => RecalculateAll();

    protected virtual void OnValidate()
    {
        ModuleTier = Mathf.Clamp(ModuleTier, 1, 10);
        VolumeCoefficientPercent = Mathf.Clamp(VolumeCoefficientPercent, 0f, 100f);
        ConstantFillPercent = Mathf.Clamp(ConstantFillPercent, 0, 100);
        CraftCoefficient = Mathf.Max(0.01f, CraftCoefficient);
        useBuildAnchorPlacement = true;
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

        fillPercentUsed = Mathf.Clamp(ConstantFillPercent, 0, 100);
        float fillFrac = fillPercentUsed / 100f;

        // Масса внутренностей = занятый объём * плотность 1 кг/л (= 1000 кг/м³)
        massKg = effectiveVolume * fillFrac * 1000f;
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

    // ==========================================
    // НОВОЕ: ВЫЧИСЛЕНИЕ ПАРАМЕТРОВ ВЗРЫВА
    // Вызывается из Верстака (Controller) при крафте
    // ==========================================

    public float CalculateExplosionRadius(float modulePower)
    {
        // Радиус = корень(Мощность) * Коэффициент
        if (!IsVolatile || modulePower <= 0f) return 0f;
        return Mathf.Sqrt(modulePower) * ExplosionRadiusCoefficient;
    }

    public float CalculateExplosionPenetration(float calculatedEffectiveVolume, float calculatedShellMass, int alloyTier)
    {
        // Пробитие = Эфф.Объем * Масса Оболочки * Коэфф.ТираОболочки * Коэффициент
        if (!IsVolatile) return 0f;
        float alloyCoeff = TierCoeffs.Get(alloyTier);
        return calculatedEffectiveVolume * calculatedShellMass * alloyCoeff * ExplosionPenetrationCoefficient;
    }

    public float CalculateExplosionDamage(float calculatedShellMass, int alloyTier)
    {
        // Урон = Масса Оболочки * Коэфф.ТираОболочки * Коэффициент
        if (!IsVolatile) return 0f;
        float alloyCoeff = TierCoeffs.Get(alloyTier);
        return calculatedShellMass * alloyCoeff * ExplosionDamageCoefficient;
    }
}