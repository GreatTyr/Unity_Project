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
        Buckshot = 2,   // Bag(n)
        Pellet = 3,     // Pel
        Fire = 4,       // F
        Chemical = 5,   // Ch
        Energy = 6      // En
    }

    public enum AreaType
    {
        None = 0,   // трактуется как Point
        Point = 1,  // P
        Sphere = 2, // Sp
        Cone = 3,   // Cn
        Cloud = 4   // Cl
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
        [Header("Тип снаряда")]
        public ChargeType chargeType = ChargeType.FM;

        [Header("Оболочка")]
        [Range(1, 10)] public int shellTier = 1;
        public float diameterMm = 10f;
        public float lengthMm = 20f;

        [Header("Разрывной заряд")]
        [Range(1, 10)] public int explosiveTier = 1;
        public float explosiveMassKg = 0f;

        [Header("Поражающий элемент")]
        public DamageElementType damageElementType = DamageElementType.Buckshot;
        [Range(2, 10)] public int buckshotCount = 2;
        [Range(1, 10)] public int damageElementTier = 1;
        public float damageElementMassKg = 0f;

        [Header("Область поражения")]
        public AreaType areaType = AreaType.Point;

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
        public bool valid = true;
        public string error = "";
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
            resourceType = type;
            this.tier = tier;
            amountKg = kg;
            isEnergy = false;
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

    public static float Ceil3(float v)
    {
        return Mathf.Ceil(v * 1000f) / 1000f;
    }

    public static float Ceil2(float v)
    {
        return Mathf.Ceil(v * 100f) / 100f;
    }

    public static float Ceil1(float v)
    {
        return Mathf.Ceil(v);
    }

    public static float NormalizeDiameterMm(float v)
    {
        return Ceil2(Mathf.Clamp(v, 1f, 100000f));
    }

    public static float NormalizeLengthMm(float v, float diameterMm)
    {
        float minLen = Ceil1(diameterMm * 2f);
        return Ceil1(Mathf.Clamp(v, minLen, 1000000f));
    }

    /// <summary>
    /// Игровая условность:
    /// масса снаряда (кг) = диаметр(мм) * длина(мм) / 1000.
    /// Пример: 10 * 20 = 200 г = 0.2 кг.
    /// </summary>
    public static float ProjectileMassKg(float diamMm, float lengthMm)
    {
        return (diamMm * lengthMm) / 1000f;
    }

    public static float GetMinPartKg(float totalMassKg)
    {
        float minPart = totalMassKg * 0.0001f; // 0.01%
        if (minPart < 0.001f) minPart = 0.001f;
        return minPart;
    }

    public static ResourcesStorage.ResourceIndex GetResourceIndex(
        ResourcesStorage.ResourceType type, int tier)
    {
        int idx = (int)type * ResourcesStorage.TiersPerType + (tier - 1);
        return (ResourcesStorage.ResourceIndex)idx;
    }

    public static bool IsChargeTypeAllowed(ChargeType type, float projectileMassKg)
    {
        switch (type)
        {
            case ChargeType.FM: return projectileMassKg > 0f;
            case ChargeType.HE: return projectileMassKg >= 0.1f;
            case ChargeType.EQ: return projectileMassKg >= 0.3f;
            default: return false;
        }
    }

    public static AreaType NormalizeAreaType(AreaType a)
    {
        return a == AreaType.None ? AreaType.Point : a;
    }

    // ===================== НОРМАЛИЗАЦИЯ ВВОДА =====================

    public static void NormalizeInput(AmmoInput input)
    {
        if (input == null) return;

        input.shellTier = Mathf.Clamp(input.shellTier, 1, 10);
        input.explosiveTier = Mathf.Clamp(input.explosiveTier, 1, 10);
        input.damageElementTier = Mathf.Clamp(input.damageElementTier, 1, 10);
        input.propellantTier = Mathf.Clamp(input.propellantTier, 1, 10);
        input.caseTier = Mathf.Clamp(input.caseTier, 1, 10);
        input.buckshotCount = Mathf.Clamp(input.buckshotCount, 2, 10);
        input.craftCount = Mathf.Max(1, input.craftCount);

        input.diameterMm = NormalizeDiameterMm(input.diameterMm);
        input.lengthMm = NormalizeLengthMm(input.lengthMm, input.diameterMm);

        input.propellantMassKg = Mathf.Max(input.propellantMassKg, 0.001f);
        input.caseMassKg = Mathf.Max(input.caseMassKg, 0.001f);
        input.explosiveMassKg = Mathf.Max(input.explosiveMassKg, 0f);
        input.damageElementMassKg = Mathf.Max(input.damageElementMassKg, 0f);

        input.areaType = NormalizeAreaType(input.areaType);

        float totalMassKg = ProjectileMassKg(input.diameterMm, input.lengthMm);
        float minPart = GetMinPartKg(totalMassKg);

        if (!IsChargeTypeAllowed(input.chargeType, totalMassKg))
            input.chargeType = ChargeType.FM;

        switch (input.chargeType)
        {
            case ChargeType.FM:
                input.explosiveMassKg = 0f;
                input.damageElementMassKg = 0f;
                input.damageElementType = DamageElementType.None;
                input.fuzeType = FuzeType.No;
                input.areaType = AreaType.Point;
                break;

            case ChargeType.HE:
                input.damageElementMassKg = 0f;
                input.damageElementType = DamageElementType.Shrapnel;
                input.areaType = AreaType.Sphere;

                if (totalMassKg >= 0.1f)
                {
                    float maxExp = Mathf.Max(minPart, totalMassKg - minPart);
                    if (input.explosiveMassKg < minPart) input.explosiveMassKg = minPart;
                    if (input.explosiveMassKg > maxExp) input.explosiveMassKg = maxExp;
                }
                else
                {
                    input.explosiveMassKg = 0f;
                }
                break;

            case ChargeType.EQ:
                if (input.damageElementType == DamageElementType.None ||
                    input.damageElementType == DamageElementType.Shrapnel)
                {
                    input.damageElementType = DamageElementType.Buckshot;
                }

                if (totalMassKg >= 0.3f)
                {
                    if (input.explosiveMassKg < minPart) input.explosiveMassKg = minPart;
                    if (input.damageElementMassKg < minPart) input.damageElementMassKg = minPart;

                    float maxExp = Mathf.Max(minPart, totalMassKg - minPart - input.damageElementMassKg);
                    if (input.explosiveMassKg > maxExp) input.explosiveMassKg = maxExp;

                    float maxDe = Mathf.Max(minPart, totalMassKg - minPart - input.explosiveMassKg);
                    if (input.damageElementMassKg > maxDe) input.damageElementMassKg = maxDe;
                }
                else
                {
                    input.chargeType = ChargeType.FM;
                    input.explosiveMassKg = 0f;
                    input.damageElementMassKg = 0f;
                    input.damageElementType = DamageElementType.None;
                    input.fuzeType = FuzeType.No;
                    input.areaType = AreaType.Point;
                    break;
                }

                switch (input.damageElementType)
                {
                    case DamageElementType.Buckshot:
                        input.areaType = AreaType.Point;
                        break;
                    case DamageElementType.Pellet:
                        if (input.areaType != AreaType.Sphere && input.areaType != AreaType.Cone)
                            input.areaType = AreaType.Sphere;
                        break;
                    case DamageElementType.Fire:
                    case DamageElementType.Chemical:
                    case DamageElementType.Energy:
                        input.areaType = AreaType.Cloud;
                        break;
                    default:
                        input.areaType = AreaType.Point;
                        break;
                }
                break;
        }
    }

    // ===================== ОСНОВНОЙ РАСЧЁТ =====================

    public static AmmoOutput Calculate(AmmoInput input)
    {
        var o = new AmmoOutput();
        o.error = "";

        if (input == null)
        {
            o.error = "Отсутствуют входные данные.";
            return o;
        }

        NormalizeInput(input);

        float d = input.diameterMm;
        float l = input.lengthMm;
        int shellTier = input.shellTier;
        ChargeType chargeType = input.chargeType;

        o.diameterMm = d;
        o.lengthMm = l;
        o.shellTier = shellTier;
        o.chargeType = chargeType;

        float totalMassKg = Ceil3(ProjectileMassKg(d, l));
        if (totalMassKg <= 0f)
        {
            o.error = "Масса снаряда слишком мала.";
            return o;
        }

        if (!IsChargeTypeAllowed(chargeType, totalMassKg))
        {
            o.error = chargeType == ChargeType.HE
                ? "Недостаточная масса снаряда для фугасного типа. Минимум 0.100 кг."
                : chargeType == ChargeType.EQ
                    ? "Недостаточная масса снаряда для снаряженного типа. Минимум 0.300 кг."
                    : "Недопустимый тип снаряда.";
            return o;
        }

        o.totalProjectileMassKg = totalMassKg;

        float minPart = GetMinPartKg(totalMassKg);

        float expMass = Mathf.Max(input.explosiveMassKg, 0f);
        float deMass = Mathf.Max(input.damageElementMassKg, 0f);
        DamageElementType deType = input.damageElementType;
        FuzeType fuze = input.fuzeType;
        int expTier = input.explosiveTier;
        int deTier = input.damageElementTier;
        int buckshotCount = input.buckshotCount;
        AreaType area = NormalizeAreaType(input.areaType);

        switch (chargeType)
        {
            case ChargeType.FM:
                expMass = 0f;
                deMass = 0f;
                deType = DamageElementType.None;
                fuze = FuzeType.No;
                area = AreaType.Point;
                break;

            case ChargeType.HE:
                deMass = 0f;
                deType = DamageElementType.Shrapnel;
                area = AreaType.Sphere;
                if (expMass < minPart) expMass = minPart;
                {
                    float maxExp = totalMassKg - minPart;
                    if (expMass > maxExp) expMass = maxExp;
                }
                break;

            case ChargeType.EQ:
                if (deType == DamageElementType.None || deType == DamageElementType.Shrapnel)
                    deType = DamageElementType.Buckshot;

                if (expMass < minPart) expMass = minPart;
                if (deMass < minPart) deMass = minPart;

                {
                    float maxExp = totalMassKg - minPart - deMass;
                    if (expMass > maxExp) expMass = maxExp;
                }

                {
                    float maxDe = totalMassKg - minPart - expMass;
                    if (deMass > maxDe) deMass = maxDe;
                }

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
                }
                break;
        }

        float shellMass = totalMassKg - expMass - deMass;
        if (chargeType != ChargeType.FM && shellMass < minPart)
            shellMass = minPart;

        shellMass = Ceil3(shellMass);
        expMass = Ceil3(expMass);
        deMass = Ceil3(deMass);

        if (chargeType != ChargeType.FM)
        {
            float sum = shellMass + expMass + deMass;
            float diff = Ceil3(totalMassKg - sum);
            shellMass = Ceil3(shellMass + diff);
        }

        o.shellMassKg = shellMass;
        o.explosiveMassKg = (chargeType == ChargeType.FM) ? 0f : expMass;
        o.damageElementMassKg = (chargeType == ChargeType.EQ) ? deMass : 0f;
        o.explosiveTier = (chargeType == ChargeType.FM) ? 0 : expTier;
        o.damageElementTier = (chargeType == ChargeType.EQ) ? deTier : 0;
        o.buckshotCount = buckshotCount;
        o.fuzeType = fuze;
        o.areaType = area;
        o.damageElementType = deType;

        float shellCoeff = TierCoeffs.Get(shellTier);
        o.shellStrength = Ceil3(shellMass * shellCoeff);

        float expPower = 0f;
        if (chargeType != ChargeType.FM && expMass > 0f)
            expPower = Ceil3(expMass * TierCoeffs.Get(expTier));
        o.explosivePower = expPower;

        float radius = 0f;
        float areaPen = 0f;
        float areaDmg = 0f;

        if (chargeType == ChargeType.HE)
        {
            radius = Ceil3(Mathf.Sqrt(expPower));
            areaPen = Ceil3(expPower * shellMass * shellCoeff);
            areaDmg = Ceil3(shellMass * shellCoeff);
        }
        else if (chargeType == ChargeType.EQ)
        {
            float deCoeff = TierCoeffs.Get(deTier);

            switch (deType)
            {
                case DamageElementType.Buckshot:
                    radius = 0f;
                    areaPen = 0f;
                    areaDmg = 0f;
                    break;

                case DamageElementType.Pellet:
                    radius = Ceil3(Mathf.Sqrt(expPower));
                    areaPen = Ceil3(expPower * deMass * deCoeff);
                    areaDmg = Ceil3(deMass * deCoeff);
                    break;

                case DamageElementType.Fire:
                case DamageElementType.Chemical:
                case DamageElementType.Energy:
                    radius = Ceil3(Mathf.Sqrt(expPower));
                    areaPen = Ceil3(expPower * deMass * deCoeff);
                    areaDmg = Ceil3(deMass * deCoeff);
                    break;
            }
        }

        o.damageRadius = radius;
        o.areaPenetration = areaPen;
        o.areaDamage = areaDmg;
        o.coneAngleDeg = (area == AreaType.Cone) ? Ceil3(d / l * 100f) : 0f;

        int propTier = input.propellantTier;
        float propMass = Mathf.Max(input.propellantMassKg, 0.001f);
        o.propellantTier = propTier;
        o.propellantMassKg = propMass;
        o.propulsionForce = Ceil3(propMass * TierCoeffs.Get(propTier));

        int caseTier = input.caseTier;
        float caseMass = Mathf.Max(input.caseMassKg, 0.001f);
        o.caseTier = caseTier;
        o.caseMassKg = caseMass;
        o.caseStrength = Ceil3(caseMass * TierCoeffs.Get(caseTier));

        o.totalShotMassKg = Ceil3(totalMassKg + propMass + caseMass);
        o.ammoCode = GenerateCode(o);

        return o;
    }

    // ===================== РАСЧЁТ СТВОЛА =====================

    public static bool IsBarrelValid(AmmoOutput ammo, BarrelInput barrel)
    {
        if (ammo == null || barrel == null) return false;
        if (barrel.barrelLengthMm < ammo.lengthMm) return false;

        float maxBarrelD = Mathf.Floor(ammo.diameterMm * 1.25f);
        if (maxBarrelD < ammo.diameterMm) maxBarrelD = ammo.diameterMm;
        if (barrel.barrelDiameterMm < ammo.diameterMm) return false;
        if (barrel.barrelDiameterMm > maxBarrelD) return false;

        return true;
    }

    public static BarrelOutput CalculateBarrel(AmmoOutput ammo, BarrelInput barrel)
    {
        var b = new BarrelOutput();

        if (ammo == null || barrel == null)
        {
            b.valid = false;
            b.error = "неверные параметры ствола";
            return b;
        }

        if (!IsBarrelValid(ammo, barrel))
        {
            b.valid = false;
            b.error = "неверные параметры ствола";
            return b;
        }

        float d = ammo.diameterMm;
        float l = ammo.lengthMm;

        float barrelLen = barrel.barrelLengthMm;
        float barrelD = barrel.barrelDiameterMm;

        float barrelLenM = barrelLen / 1000f;
        float barrelDM = barrelD / 1000f;
        float dM = d / 1000f;

        float mass = ammo.totalProjectileMassKg;
        if (mass <= 0f) mass = 0.001f;

        b.projectileSpeed = Ceil3(ammo.propulsionForce * barrelLenM / (barrelDM * mass));
        b.accuracy = Ceil3((barrelD / barrelLen) * (d / l));
        b.maxRange = Ceil3(b.projectileSpeed * 10f);
        b.directFireRange = Ceil3(b.projectileSpeed * 5f);
        b.directDamage = Ceil3(b.projectileSpeed * ammo.totalProjectileMassKg * dM);
        b.directPenetration = Ceil3(
            b.projectileSpeed * ammo.shellMassKg * TierCoeffs.Get(ammo.shellTier) / dM);

        return b;
    }

    // ===================== РАСЧЁТ СТОИМОСТИ =====================

    public static List<ResourceCost> CalculateCosts(AmmoOutput o)
    {
        var costs = new List<ResourceCost>();

        costs.Add(new ResourceCost(
            ResourcesStorage.ResourceType.Metal, o.shellTier, o.shellMassKg));

        if (o.chargeType != ChargeType.FM && o.explosiveMassKg > 0f && o.explosiveTier >= 1)
        {
            costs.Add(new ResourceCost(
                ResourcesStorage.ResourceType.Chemicals, o.explosiveTier, o.explosiveMassKg));
        }

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

        costs.Add(new ResourceCost(
            ResourcesStorage.ResourceType.Chemicals, o.propellantTier, o.propellantMassKg));

        costs.Add(new ResourceCost(
            ResourcesStorage.ResourceType.Metal, o.caseTier, o.caseMassKg));

        long energyCost = (long)Mathf.Ceil(o.totalShotMassKg * 10f);
        costs.Add(ResourceCost.Energy(energyCost));

        return costs;
    }

    // ===================== ПРОВЕРКА РЕСУРСОВ =====================

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

    public static bool ConsumeResources(
        ResourcesStorage storage, List<ResourceCost> costsPerShot, int count)
    {
        string err = ValidateResources(storage, costsPerShot, count);
        if (!string.IsNullOrEmpty(err)) return false;

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

    // ===================== КОД: ГЕНЕРАЦИЯ / ПАРСИНГ =====================

    public static bool TryParseCode(string code, out AmmoInput input, out string error)
    {
        input = null;
        error = "";

        if (string.IsNullOrWhiteSpace(code))
        {
            error = "Код снаряда пуст.";
            return false;
        }

        string[] p = code.Split('-');
        if (p.Length != 26)
        {
            error = "Неверный формат кода снаряда.";
            return false;
        }

        var parsed = new AmmoInput();

        if (p[0] != "S")
        {
            error = "Неверный префикс кода.";
            return false;
        }

        if (!Enum.TryParse(p[1], out ChargeType chargeType))
        {
            error = "Неверный тип снаряда в коде.";
            return false;
        }
        parsed.chargeType = chargeType;

        if (!int.TryParse(p[2], out parsed.shellTier)) { error = "Неверный тир оболочки."; return false; }
        if (!TryParseFloatAny(p[3], out parsed.diameterMm)) { error = "Неверный диаметр."; return false; }
        if (!TryParseFloatAny(p[4], out parsed.lengthMm)) { error = "Неверная длина."; return false; }

        if (!int.TryParse(p[8], out parsed.explosiveTier)) { error = "Неверный тир заряда."; return false; }
        if (!TryParseFloatAny(p[9], out parsed.explosiveMassKg)) { error = "Неверная масса заряда."; return false; }

        if (!TryParseDamageElement(p[11], out DamageElementType deType, out int buckCount))
        {
            error = "Неверный поражающий элемент.";
            return false;
        }
        parsed.damageElementType = deType;
        parsed.buckshotCount = buckCount;

        if (!TryParseArea(p[12], out parsed.areaType))
        {
            error = "Неверная область поражения.";
            return false;
        }

        if (!int.TryParse(p[13], out parsed.damageElementTier)) { error = "Неверный тир поражающего элемента."; return false; }
        if (!TryParseFloatAny(p[14], out parsed.damageElementMassKg)) { error = "Неверная масса поражающего элемента."; return false; }

        if (!Enum.TryParse(p[18], out FuzeType fuzeType))
        {
            error = "Неверный взрыватель.";
            return false;
        }
        parsed.fuzeType = fuzeType;

        if (!int.TryParse(p[19], out parsed.propellantTier)) { error = "Неверный тир толкающего заряда."; return false; }
        if (!TryParseFloatAny(p[20], out parsed.propellantMassKg)) { error = "Неверная масса толкающего заряда."; return false; }

        if (!int.TryParse(p[22], out parsed.caseTier)) { error = "Неверный тир гильзы."; return false; }
        if (!TryParseFloatAny(p[23], out parsed.caseMassKg)) { error = "Неверная масса гильзы."; return false; }

        parsed.craftCount = 1;

        NormalizeInput(parsed);
        AmmoOutput calc = Calculate(parsed);
        if (!string.IsNullOrEmpty(calc.error))
        {
            error = calc.error;
            return false;
        }

        string normalizedCode = calc.ammoCode;
        if (!string.Equals(normalizedCode, code, StringComparison.Ordinal))
        {
            error = "Код снаряда не соответствует допустимым правилам.";
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
            System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture,
            out value);
    }

    private static bool TryParseDamageElement(string s, out DamageElementType type, out int buckCount)
    {
        type = DamageElementType.None;
        buckCount = 2;

        if (s == "0") { type = DamageElementType.None; return true; }
        if (s == "Sh") { type = DamageElementType.Shrapnel; return true; }
        if (s == "Pel") { type = DamageElementType.Pellet; return true; }
        if (s == "F") { type = DamageElementType.Fire; return true; }
        if (s == "Ch") { type = DamageElementType.Chemical; return true; }
        if (s == "En") { type = DamageElementType.Energy; return true; }

        if (s.StartsWith("Bag"))
        {
            string num = s.Substring(3);
            if (int.TryParse(num, out buckCount))
            {
                buckCount = Mathf.Clamp(buckCount, 2, 10);
                type = DamageElementType.Buckshot;
                return true;
            }
        }

        return false;
    }

    private static bool TryParseArea(string s, out AreaType area)
    {
        area = AreaType.Point;

        switch (s)
        {
            case "0":
            case "P":
                area = AreaType.Point;
                return true;
            case "Sp":
                area = AreaType.Sphere;
                return true;
            case "Cn":
                area = AreaType.Cone;
                return true;
            case "Cl":
                area = AreaType.Cloud;
                return true;
            default:
                return false;
        }
    }

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
        a = NormalizeAreaType(a);

        switch (a)
        {
            case AreaType.Point: return "P";
            case AreaType.Sphere: return "Sp";
            case AreaType.Cone: return "Cn";
            case AreaType.Cloud: return "Cl";
            default: return "P";
        }
    }
}