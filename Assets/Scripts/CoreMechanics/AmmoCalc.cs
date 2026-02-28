// AmmoCalc.cs
using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Все расчёты для конического снаряда (тип S).
/// Чистая статическая логика без MonoBehaviour.
/// Не зависит от UI, складов и верстаков.
/// </summary>
public static class AmmoCalc
{
    // ===================== ПЕРЕЧИСЛЕНИЯ =====================

    public enum ChargeType { FM, HE, EQ }

    public enum DamageElementType
    {
        None = 0,
        Shrapnel = 1,   // Sh — только HE, авто
        Buckshot = 2,    // Bag(n)
        Pellet = 3,      // Pel
        Fire = 4,        // F
        Chemical = 5,    // Ch
        Energy = 6       // En
    }

    public enum AreaType
    {
        None = 0,
        Point = 1,    // P
        Sphere = 2,   // Sp
        Cone = 3,     // Cn
        Cloud = 4     // Cl
    }

    public enum FuzeType
    {
        No = 0,
        Ct = 1,
        Tm = 2,
        Alt = 3,
        Se = 4,
        Re = 5
    }

    // ===================== ВХОДНЫЕ ДАННЫЕ =====================

    [Serializable]
    public class AmmoInput
    {
        [Header("Оболочка")]
        [Range(1, 10)] public int shellTier = 1;
        public float diameterMm = 10f;
        public float lengthMm = 20f;

        [Header("Разрывной заряд")]
        [Range(0, 10)] public int explosiveTier = 0;
        public float explosiveMassKg = 0f;

        [Header("Поражающий элемент")]
        public DamageElementType damageElementType = DamageElementType.None;
        [Range(2, 11)] public int buckshotCount = 2;
        [Range(0, 10)] public int damageElementTier = 0;
        public float damageElementMassKg = 0f;

        [Header("Область поражения")]
        public AreaType areaType = AreaType.None;

        [Header("Взрыватель")]
        public FuzeType fuzeType = FuzeType.No;

        [Header("Толкающий заряд")]
        [Range(1, 10)] public int propellantTier = 1;
        public float propellantMassKg = 0.001f;

        [Header("Гильза")]
        [Range(1, 10)] public int caseTier = 1;
        public float caseMassKg = 0.001f;

        [Header("Количество")]
        public int craftCount = 1;
    }

    [Serializable]
    public class BarrelInput
    {
        public float barrelLengthMm = 100f;
        public float barrelDiameterMm = 10f;
    }

    // ===================== ВЫХОДНЫЕ ДАННЫЕ =====================

    [Serializable]
    public class AmmoOutput
    {
        public ChargeType chargeType;
        public int shellTier;
        public float diameterMm;
        public float lengthMm;
        public float totalProjectileMassKg;
        public float shellMassKg;
        public float shellStrength;
        public int explosiveTier;
        public float explosiveMassKg;
        public float explosivePower;
        public DamageElementType damageElementType;
        public int buckshotCount;
        public AreaType areaType;
        public int damageElementTier;
        public float damageElementMassKg;
        public float damageRadius;
        public float areaPenetration;
        public float areaDamage;
        public float coneAngleDeg;
        public FuzeType fuzeType;
        public int propellantTier;
        public float propellantMassKg;
        public float propulsionForce;
        public int caseTier;
        public float caseMassKg;
        public float caseStrength;
        public float totalShotMassKg;
        public string ammoCode;
        public string error;
    }

    [Serializable]
    public class BarrelOutput
    {
        public float projectileSpeed;
        public float accuracy;
        public float maxRange;
        public float directFireRange;
        public float directDamage;
        public float directPenetration;
    }

    [Serializable]
    public class ResourceCost
    {
        public ResourcesStorage.ResourceType resourceType;
        public int tier;
        public float amountKg;
        public long amountEnergy;
        public bool isEnergy;

        public ResourceCost() { }

        public ResourceCost(ResourcesStorage.ResourceType type, int tier, float kg)
        {
            this.resourceType = type;
            this.tier = tier;
            this.amountKg = kg;
            this.isEnergy = false;
        }

        public static ResourceCost Energy(long amount)
        {
            return new ResourceCost
            {
                isEnergy = true,
                amountEnergy = amount
            };
        }
    }

    // ===================== УТИЛИТЫ =====================

    /// <summary>
    /// Округление вверх до 3 знаков после запятой.
    /// </summary>
    public static float Ceil3(float v)
    {
        return Mathf.Ceil(v * 1000f) / 1000f;
    }

    /// <summary>
    /// Объём цилиндра через диаметр (мм) и длину (мм), результат в кг
    /// (плотность = 1 кг/дм³ = 0.000001 кг/мм³).
    /// V = π × d² × h / 4 (мм³) → /1_000_000 → дм³ = кг
    /// </summary>
    public static float CylinderMassKg(float diamMm, float lengthMm)
    {
        float volumeMm3 = Mathf.PI * diamMm * diamMm * lengthMm / 4f;
        return volumeMm3 / 1_000_000f;
    }

    /// <summary>
    /// Получить ResourceIndex по типу ресурса и тиру (1..10).
    /// </summary>
    public static ResourcesStorage.ResourceIndex GetResourceIndex(
        ResourcesStorage.ResourceType type, int tier)
    {
        int idx = (int)type * ResourcesStorage.TiersPerType + (tier - 1);
        return (ResourcesStorage.ResourceIndex)idx;
    }

    // ===================== ОСНОВНОЙ РАСЧЁТ =====================

    public static AmmoOutput Calculate(AmmoInput input)
    {
        var o = new AmmoOutput();
        o.error = "";

        // --- Клэмп ввода ---
        float d = Mathf.Clamp(input.diameterMm, 1f, 100000f);
        float l = Mathf.Clamp(input.lengthMm, d * 2f, 1000000f);
        int shellTier = Mathf.Clamp(input.shellTier, 1, 10);

        o.diameterMm = d;
        o.lengthMm = l;
        o.shellTier = shellTier;

        // --- Общая масса снаряда ---
        float totalMassKg = Ceil3(CylinderMassKg(d, l));
        if (totalMassKg <= 0f)
        {
            o.error = "Масса снаряда слишком мала.";
            return o;
        }
        o.totalProjectileMassKg = totalMassKg;

        // --- Входные массы ---
        float expMass = Mathf.Max(input.explosiveMassKg, 0f);
        float deMass = Mathf.Max(input.damageElementMassKg, 0f);
        DamageElementType deType = input.damageElementType;
        FuzeType fuze = input.fuzeType;
        int expTier = Mathf.Clamp(input.explosiveTier, 0, 10);
        int deTier = Mathf.Clamp(input.damageElementTier, 0, 10);
        int buckshotCount = Mathf.Clamp(input.buckshotCount, 2, 11);
        AreaType area = input.areaType;

        // --- Определение типа заряда ---
        ChargeType chargeType;

        if (expMass <= 0f && deMass <= 0f)
        {
            chargeType = ChargeType.FM;
            expMass = 0f;
            deMass = 0f;
            deType = DamageElementType.None;
            fuze = FuzeType.No;
            area = AreaType.None;
            expTier = 0;
            deTier = 0;
        }
        else if (expMass > 0f &&
                 (deMass <= 0f || deType == DamageElementType.None ||
                  deType == DamageElementType.Shrapnel))
        {
            chargeType = ChargeType.HE;
            deMass = 0f;
            deType = DamageElementType.Shrapnel;
            deTier = 0;
        }
        else if (expMass > 0f && deMass > 0f &&
                 deType != DamageElementType.None &&
                 deType != DamageElementType.Shrapnel)
        {
            chargeType = ChargeType.EQ;
            if (expTier < 1) expTier = 1;
            if (deTier < 1) deTier = 1;
        }
        else
        {
            o.error = "Поражающий элемент требует разрывного заряда (масса заряда > 0).";
            return o;
        }

        o.chargeType = chargeType;

        // --- Валидация FM ---
        if (chargeType == ChargeType.FM && fuze != FuzeType.No)
        {
            o.error = "Болванка (FM) не может иметь взрыватель.";
            return o;
        }

        // --- Балансировка масс ---
        float minPart = Ceil3(totalMassKg * 0.0001f); // 0.01%
        if (minPart <= 0f) minPart = 0.001f;

        float maxFill = totalMassKg - minPart; // макс. под заряд + ПЭ

        if (expMass + deMass > maxFill)
        {
            float sum = expMass + deMass;
            if (sum > 0f)
            {
                float ratio = maxFill / sum;
                expMass = Ceil3(expMass * ratio);
                deMass = Ceil3(deMass * ratio);
            }
            // Гарантируем непревышение
            if (expMass + deMass > maxFill)
            {
                deMass = Ceil3(maxFill - expMass);
                if (deMass < 0f) deMass = 0f;
            }
        }

        // Минимальные пороги для ненулевых компонентов
        if (chargeType != ChargeType.FM && expMass > 0f && expMass < minPart)
            expMass = minPart;
        if (chargeType == ChargeType.EQ && deMass > 0f && deMass < minPart)
            deMass = minPart;

        // Финальная перепроверка
        if (expMass + deMass > maxFill)
        {
            float over = (expMass + deMass) - maxFill;
            if (deMass >= over) deMass = Ceil3(deMass - over);
            else
            {
                deMass = 0f;
                expMass = Ceil3(maxFill);
            }
        }

        float shellMass = Ceil3(totalMassKg - expMass - deMass);
        if (shellMass < minPart) shellMass = minPart;

        o.shellMassKg = shellMass;
        o.explosiveMassKg = expMass;
        o.damageElementMassKg = (chargeType == ChargeType.EQ) ? deMass : 0f;
        o.explosiveTier = expTier;
        o.damageElementTier = deTier;
        o.buckshotCount = buckshotCount;
        o.fuzeType = fuze;

        // --- Прочность оболочки ---
        float shellCoeff = TierCoeffs.Get(shellTier);
        o.shellStrength = Ceil3(shellMass * shellCoeff);

        // --- Мощность разрывного заряда ---
        float expPower = 0f;
        if (chargeType != ChargeType.FM && expTier >= 1 && expMass > 0f)
        {
            expPower = Ceil3(expMass * TierCoeffs.Get(expTier));
        }
        o.explosivePower = expPower;

        // --- Валидация области поражения ---
        if (chargeType == ChargeType.FM)
        {
            area = AreaType.None;
            deType = DamageElementType.None;
        }
        else if (chargeType == ChargeType.HE)
        {
            deType = DamageElementType.Shrapnel;
            if (area != AreaType.Sphere && area != AreaType.Cone)
                area = AreaType.Sphere;
        }
        else // EQ
        {
            switch (deType)
            {
                case DamageElementType.Buckshot:
                    area = AreaType.Point;
                    break;
                case DamageElementType.Pellet:
                    if (area != AreaType.Sphere && area != AreaType.Cone)
                        area = AreaType.Sphere;
                    break;
                case DamageElementType.Fire:
                case DamageElementType.Chemical:
                case DamageElementType.Energy:
                    area = AreaType.Cloud;
                    break;
                default:
                    area = AreaType.None;
                    break;
            }
        }

        o.areaType = area;
        o.damageElementType = deType;

        // --- Радиус поражения ---
        float radius = 0f;
        if (chargeType != ChargeType.FM && expPower > 0f)
        {
            radius = Ceil3(Mathf.Sqrt(expPower));
        }
        o.damageRadius = radius;

        // --- Пробитие и повреждение в радиусе ---
        float areaPen = 0f;
        float areaDmg = 0f;

        if (chargeType == ChargeType.HE)
        {
            areaPen = Ceil3(expPower * shellMass * shellCoeff);
            areaDmg = Ceil3(shellMass * shellCoeff);
        }
        else if (chargeType == ChargeType.EQ && deTier >= 1 && deMass > 0f)
        {
            float deCoeff = TierCoeffs.Get(deTier);

            if (deType == DamageElementType.Buckshot)
            {
                int count = Mathf.Clamp(buckshotCount, 2, 10);
                float singleMass = Ceil3(deMass / count);
                areaPen = Ceil3(expPower * singleMass * deCoeff);
                areaDmg = Ceil3(singleMass * deCoeff);
            }
            else
            {
                areaPen = Ceil3(expPower * deMass * deCoeff);
                areaDmg = Ceil3(deMass * deCoeff);
            }
        }

        o.areaPenetration = areaPen;
        o.areaDamage = areaDmg;

        // --- Угол конуса ---
        o.coneAngleDeg = (area == AreaType.Cone) ? Ceil3(d / l * 100f) : 0f;

        // --- Толкающий заряд ---
        int propTier = Mathf.Clamp(input.propellantTier, 1, 10);
        float propMass = Ceil3(Mathf.Max(input.propellantMassKg, 0.001f));
        o.propellantTier = propTier;
        o.propellantMassKg = propMass;
        o.propulsionForce = Ceil3(propMass * TierCoeffs.Get(propTier));

        // --- Гильза ---
        int caseTier = Mathf.Clamp(input.caseTier, 1, 10);
        float caseMass = Ceil3(Mathf.Max(input.caseMassKg, 0.001f));
        o.caseTier = caseTier;
        o.caseMassKg = caseMass;
        o.caseStrength = Ceil3(caseMass * TierCoeffs.Get(caseTier));

        // --- Масса выстрела ---
        o.totalShotMassKg = Ceil3(totalMassKg + propMass + caseMass);

        // --- Код ---
        o.ammoCode = GenerateCode(o);

        return o;
    }

    // ===================== РАСЧЁТ СТВОЛА =====================

    public static BarrelOutput CalculateBarrel(AmmoOutput ammo, BarrelInput barrel)
    {
        var b = new BarrelOutput();

        float d = ammo.diameterMm;
        float l = ammo.lengthMm;

        float barrelLen = Mathf.Clamp(barrel.barrelLengthMm, l, 1000000f);
        float maxBarrelD = Mathf.Floor(d * 1.25f);
        if (maxBarrelD < d) maxBarrelD = d;
        float barrelD = Mathf.Clamp(barrel.barrelDiameterMm, d, maxBarrelD);

        // мм → м
        float barrelLenM = barrelLen / 1000f;
        float barrelDM = barrelD / 1000f;
        float dM = d / 1000f;

        float mass = ammo.totalProjectileMassKg;
        if (mass <= 0f) mass = 0.001f;

        // Скорость = сила выталкивания * длина ствола(м) / (диаметр ствола(м) * масса снаряда(кг))
        b.projectileSpeed = Ceil3(ammo.propulsionForce * barrelLenM / (barrelDM * mass));

        // Точность = (диаметр ствола / длина ствола) * (диаметр снаряда / длина снаряда)
        b.accuracy = Ceil3((barrelD / barrelLen) * (d / l));

        // Дальности
        b.maxRange = Ceil3(b.projectileSpeed * 10f);
        b.directFireRange = Ceil3(b.projectileSpeed * 5f);

        // Прямой урон = скорость * масса снаряда * диаметр(м)
        b.directDamage = Ceil3(b.projectileSpeed * ammo.totalProjectileMassKg * dM);

        // Прямое пробитие = скорость * масса оболочки * коэфф тира оболочки / диаметр(м)
        b.directPenetration = Ceil3(
            b.projectileSpeed * ammo.shellMassKg * TierCoeffs.Get(ammo.shellTier) / dM);

        return b;
    }

    // ===================== РАСЧЁТ СТОИМОСТИ =====================

    public static List<ResourceCost> CalculateCosts(AmmoOutput o)
    {
        var costs = new List<ResourceCost>();

        // 1. Оболочка → Metal тира оболочки
        costs.Add(new ResourceCost(
            ResourcesStorage.ResourceType.Metal, o.shellTier, o.shellMassKg));

        // 2. Разрывной заряд → Chemicals тира заряда
        if (o.chargeType != ChargeType.FM && o.explosiveMassKg > 0f && o.explosiveTier >= 1)
        {
            costs.Add(new ResourceCost(
                ResourcesStorage.ResourceType.Chemicals, o.explosiveTier, o.explosiveMassKg));
        }

        // 3. Поражающий элемент
        if (o.chargeType == ChargeType.EQ && o.damageElementMassKg > 0f && o.damageElementTier >= 1)
        {
            ResourcesStorage.ResourceType deResType;
            switch (o.damageElementType)
            {
                case DamageElementType.Buckshot:
                case DamageElementType.Pellet:
                    deResType = ResourcesStorage.ResourceType.Metal;
                    break;
                case DamageElementType.Fire:
                    deResType = ResourcesStorage.ResourceType.Fuel;
                    break;
                case DamageElementType.Chemical:
                    deResType = ResourcesStorage.ResourceType.Chemicals;
                    break;
                case DamageElementType.Energy:
                    deResType = ResourcesStorage.ResourceType.Nanites;
                    break;
                default:
                    deResType = ResourcesStorage.ResourceType.Metal;
                    break;
            }
            costs.Add(new ResourceCost(deResType, o.damageElementTier, o.damageElementMassKg));
        }

        // 4. Взрыватель → Nanites
        if (o.fuzeType != FuzeType.No)
        {
            float fuzeMassKg = Ceil3(o.totalProjectileMassKg * 0.05f);
            int fuzeTier;
            switch (o.fuzeType)
            {
                case FuzeType.Ct: fuzeTier = 1; break;
                case FuzeType.Tm: fuzeTier = 2; break;
                case FuzeType.Alt: fuzeTier = 3; break;
                case FuzeType.Se: fuzeTier = 4; break;
                case FuzeType.Re: fuzeTier = 5; break;
                default: fuzeTier = 1; break;
            }
            costs.Add(new ResourceCost(
                ResourcesStorage.ResourceType.Nanites, fuzeTier, fuzeMassKg));
        }

        // 5. Толкающий заряд → Chemicals
        costs.Add(new ResourceCost(
            ResourcesStorage.ResourceType.Chemicals, o.propellantTier, o.propellantMassKg));

        // 6. Гильза → Metal
        costs.Add(new ResourceCost(
            ResourcesStorage.ResourceType.Metal, o.caseTier, o.caseMassKg));

        // 7. Энергия = масса выстрела * 10
        long energyCost = (long)Mathf.Ceil(o.totalShotMassKg * 10f);
        costs.Add(ResourceCost.Energy(energyCost));

        return costs;
    }

    // ===================== ПРОВЕРКА РЕСУРСОВ =====================

    /// <summary>
    /// Проверить наличие ресурсов на складе для крафта count выстрелов.
    /// Возвращает пустую строку если ресурсов хватает, иначе текст ошибки.
    /// </summary>
    public static string ValidateResources(
        ResourcesStorage storage, List<ResourceCost> costsPerShot, int count)
    {
        if (storage == null) return "Не назначен склад ресурсов.";
        if (count <= 0) return "Количество должно быть > 0.";

        foreach (var cost in costsPerShot)
        {
            if (cost.isEnergy)
            {
                long totalEnergy = cost.amountEnergy * count;
                if (storage.EnergyUnits < totalEnergy)
                {
                    return $"Не хватает энергии: нужно {totalEnergy}, есть {storage.EnergyUnits}";
                }
            }
            else
            {
                var ri = GetResourceIndex(cost.resourceType, cost.tier);
                long needGrams = ResourcesStorage.KgToGramsRounded(cost.amountKg * count);
                long haveGrams = storage.GetGrams(ri);
                if (haveGrams < needGrams)
                {
                    double haveKg = haveGrams / (double)ResourcesStorage.GramsPerKg;
                    double needKg = needGrams / (double)ResourcesStorage.GramsPerKg;
                    string name = ResourcesStorage.ResourceName(ri);
                    return $"Не хватает {name}: нужно {needKg:F3} кг, есть {haveKg:F3} кг";
                }
            }
        }

        return "";
    }

    /// <summary>
    /// Списать ресурсы со склада. Вызывать только после ValidateResources.
    /// Возвращает true при успехе.
    /// </summary>
    public static bool ConsumeResources(
        ResourcesStorage storage, List<ResourceCost> costsPerShot, int count)
    {
        // Сначала проверяем всё
        string err = ValidateResources(storage, costsPerShot, count);
        if (!string.IsNullOrEmpty(err)) return false;

        // Списываем
        foreach (var cost in costsPerShot)
        {
            if (cost.isEnergy)
            {
                long totalEnergy = cost.amountEnergy * count;
                storage.TryConsumeEnergy(totalEnergy);
            }
            else
            {
                var ri = GetResourceIndex(cost.resourceType, cost.tier);
                long needGrams = ResourcesStorage.KgToGramsRounded(cost.amountKg * count);
                storage.TryRemoveGrams(ri, needGrams);
            }
        }

        return true;
    }

    // ===================== ГЕНЕРАЦИЯ КОДА =====================

    private static string GenerateCode(AmmoOutput o)
    {
        string[] p = new string[26];

        p[0] = "S";
        p[1] = o.chargeType.ToString();
        p[2] = o.shellTier.ToString();
        p[3] = FN(o.diameterMm);
        p[4] = FN(o.lengthMm);
        p[5] = FN(o.totalProjectileMassKg);
        p[6] = FN(o.shellMassKg);
        p[7] = FN(o.shellStrength);
        p[8] = o.explosiveTier.ToString();
        p[9] = FN(o.explosiveMassKg);
        p[10] = FN(o.explosivePower);
        p[11] = DECode(o.damageElementType, o.buckshotCount);
        p[12] = AreaCode(o.areaType);
        p[13] = (o.chargeType == ChargeType.EQ) ? o.damageElementTier.ToString() : "0";
        p[14] = (o.chargeType == ChargeType.EQ) ? FN(o.damageElementMassKg) : "0";
        p[15] = FN(o.damageRadius);
        p[16] = FN(o.areaPenetration);
        p[17] = FN(o.areaDamage);
        p[18] = o.fuzeType.ToString();
        p[19] = o.propellantTier.ToString();
        p[20] = FN(o.propellantMassKg);
        p[21] = FN(o.propulsionForce);
        p[22] = o.caseTier.ToString();
        p[23] = FN(o.caseMassKg);
        p[24] = FN(o.caseStrength);
        p[25] = FN(o.totalShotMassKg);

        return string.Join("-", p);
    }

    private static string FN(float v)
    {
        float rounded = Ceil3(v);
        // Если целое число — без дробной части
        if (rounded == Mathf.Floor(rounded) && rounded < 1000000f)
            return ((int)rounded).ToString();
        return rounded.ToString("F3").Replace('.', ',');
    }

    private static string DECode(DamageElementType t, int buckCount)
    {
        switch (t)
        {
            case DamageElementType.None: return "0";
            case DamageElementType.Shrapnel: return "Sh";
            case DamageElementType.Buckshot:
                return "Bag" + Mathf.Clamp(buckCount, 2, 10);
            case DamageElementType.Pellet: return "Pel";
            case DamageElementType.Fire: return "F";
            case DamageElementType.Chemical: return "Ch";
            case DamageElementType.Energy: return "En";
            default: return "0";
        }
    }

    private static string AreaCode(AreaType a)
    {
        switch (a)
        {
            case AreaType.None: return "0";
            case AreaType.Point: return "P";
            case AreaType.Sphere: return "Sp";
            case AreaType.Cone: return "Cn";
            case AreaType.Cloud: return "Cl";
            default: return "0";
        }
    }
}