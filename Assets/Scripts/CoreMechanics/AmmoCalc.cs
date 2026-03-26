using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Все расчёты для конического боеприпаса (тип S).
/// Чистая статическая логика без MonoBehaviour.
/// Не зависит от UI, складов и верстаков.
/// </summary>
public static class AmmoCalc
{
    public enum ChargeType { FM = 0, HE = 1, EQ = 2 }

    public enum DamageElementType
    {
        None = 0,
        Shrapnel = 1,
        Buckshot = 2,
        Pellet = 3,
        Fire = 4,
        Chemical = 5,
        Energy = 6
    }

    public enum AreaType
    {
        None = 0,
        Point = 1,
        Sphere = 2,
        Cone = 3,
        Cloud = 4
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

    [Serializable]
    public class AmmoInput
    {
        [Header("Тип боеприпаса")]
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
        [Range(0, 10)] public int buckshotCount = 0;
        [Range(1, 10)] public int damageElementTier = 1;
        public float damageElementMassKg = 0f;

        [Header("Область поражения")]
        public AreaType areaType = AreaType.Point;

        [Header("Взрыватель")]
        public FuzeType fuzeType = FuzeType.No;

        [Header("Метательный заряд")]
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
        public float shotAngleDeg = 45f;
    }

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
        public int damageElementTier;
        public float damageElementMassKg;
        public float buckshotSingleMassKg;

        public AreaType areaType;
        public float damageRadius;
        public float areaPenetration;
        public float areaDamage;
        public float coneAngleDeg;
        public float buckshotSpreadAngleDeg;

        public FuzeType fuzeType;

        public int propellantTier;
        public float propellantMassKg;
        public float propulsionForce;

        public int caseTier;
        public float caseMassKg;
        public float caseStrength;

        public float totalAmmoMassKg;
        public float effectiveGravity;

        public bool weakExplosiveCharge;
        public string weakExplosiveChargeWarning;

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
        public float flightDistance;
        public float maxHeight;
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

    private const float SHELL_STRENGTH_SAFETY_RATIO = 0.10f;
    private const float GAME_GRAVITY = 10f;
    private const float SHOT_HEIGHT_METERS = 1.5f;
    private const float MAX_UI_ANGLE_DEG = 89.999f;

    public static float Ceil3(float v) => Mathf.Ceil(v * 1000f) / 1000f;
    public static float Ceil2(float v) => Mathf.Ceil(v * 100f) / 100f;
    public static float Ceil1(float v) => Mathf.Ceil(v);

    public static float NormalizeDiameterMm(float v)
    {
        return Ceil2(Mathf.Clamp(v, 1f, 100000f));
    }

    public static float NormalizeLengthMm(float v, float diameterMm)
    {
        float minLen = Ceil1(diameterMm * 2f);
        float maxLen = Ceil1(diameterMm * 10f);
        return Ceil1(Mathf.Clamp(v, minLen, maxLen));
    }

    public static float NormalizeMassKg(float v)
    {
        return Ceil3(Mathf.Max(0f, v));
    }

    public static float NormalizeAngleDeg(float v)
    {
        return Ceil3(Mathf.Clamp(v, 0f, MAX_UI_ANGLE_DEG));
    }

    public static float ProjectileMassKg(float diamMm, float lengthMm)
    {
        return (diamMm * diamMm * lengthMm) / 200000f;
    }

    public static float GetMinPartKg(float totalMassKg)
    {
        float minPart = totalMassKg * 0.0001f;
        if (minPart < 0.001f) minPart = 0.001f;
        return Ceil3(minPart);
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
            case ChargeType.HE: return projectileMassKg >= 0.5f;
            case ChargeType.EQ: return projectileMassKg >= 1.0f;
            default: return false;
        }
    }

    public static AreaType NormalizeAreaType(AreaType a)
    {
        return a == AreaType.None ? AreaType.Point : a;
    }

    public static void NormalizeInput(AmmoInput input)
    {
        if (input == null) return;

        input.shellTier = Mathf.Clamp(input.shellTier, 1, 10);
        input.explosiveTier = Mathf.Clamp(input.explosiveTier, 1, 10);
        input.damageElementTier = Mathf.Clamp(input.damageElementTier, 1, 10);
        input.propellantTier = Mathf.Clamp(input.propellantTier, 1, 10);
        input.caseTier = Mathf.Clamp(input.caseTier, 1, 10);
        input.buckshotCount = Mathf.Clamp(input.buckshotCount, 0, 10);
        input.craftCount = Mathf.Max(1, input.craftCount);

        input.diameterMm = NormalizeDiameterMm(input.diameterMm);
        input.lengthMm = NormalizeLengthMm(input.lengthMm, input.diameterMm);

        input.propellantMassKg = Ceil3(Mathf.Max(input.propellantMassKg, 0.001f));
        input.caseMassKg = Ceil3(Mathf.Max(input.caseMassKg, 0.001f));
        input.explosiveMassKg = NormalizeMassKg(input.explosiveMassKg);
        input.damageElementMassKg = NormalizeMassKg(input.damageElementMassKg);

        input.areaType = NormalizeAreaType(input.areaType);

        float totalMassKg = Ceil3(ProjectileMassKg(input.diameterMm, input.lengthMm));
        float minPart = GetMinPartKg(totalMassKg);

        if (!IsChargeTypeAllowed(input.chargeType, totalMassKg))
            input.chargeType = ChargeType.FM;

        switch (input.chargeType)
        {
            case ChargeType.FM:
                input.explosiveTier = 0;
                input.explosiveMassKg = 0f;
                input.damageElementType = DamageElementType.None;
                input.buckshotCount = 0;
                input.damageElementTier = 0;
                input.damageElementMassKg = 0f;
                input.fuzeType = FuzeType.No;
                input.areaType = AreaType.Point;
                break;

            case ChargeType.HE:
                input.damageElementType = DamageElementType.Shrapnel;
                input.buckshotCount = 0;
                input.damageElementTier = 0;
                input.damageElementMassKg = 0f;
                input.areaType = AreaType.Sphere;

                {
                    float maxExp = Mathf.Max(minPart, totalMassKg - minPart);
                    input.explosiveMassKg = Mathf.Clamp(input.explosiveMassKg, minPart, maxExp);
                    input.explosiveMassKg = Ceil3(input.explosiveMassKg);
                }
                break;

            case ChargeType.EQ:
                if (input.damageElementType == DamageElementType.None ||
                    input.damageElementType == DamageElementType.Shrapnel)
                {
                    input.damageElementType = DamageElementType.Buckshot;
                }

                if (input.damageElementType == DamageElementType.Buckshot)
                {
                    input.buckshotCount = Mathf.Clamp(input.buckshotCount, 2, 10);
                }
                else
                {
                    input.buckshotCount = 0;
                }

                input.explosiveMassKg = Mathf.Max(input.explosiveMassKg, minPart);
                input.damageElementMassKg = Mathf.Max(input.damageElementMassKg, minPart);

                {
                    float maxExp = Mathf.Max(minPart, totalMassKg - minPart - input.damageElementMassKg);
                    input.explosiveMassKg = Mathf.Clamp(input.explosiveMassKg, minPart, maxExp);
                    input.explosiveMassKg = Ceil3(input.explosiveMassKg);
                }

                {
                    float maxDe = Mathf.Max(minPart, totalMassKg - minPart - input.explosiveMassKg);
                    input.damageElementMassKg = Mathf.Clamp(input.damageElementMassKg, minPart, maxDe);
                    input.damageElementMassKg = Ceil3(input.damageElementMassKg);
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

    public static AmmoOutput Calculate(AmmoInput input, float effectiveGravityA = 8f, float effectiveGravityB = 145f)
    {
        var o = new AmmoOutput
        {
            error = "",
            weakExplosiveCharge = false,
            weakExplosiveChargeWarning = ""
        };

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
            o.error = "Масса боеприпаса слишком мала.";
            return o;
        }

        if (!IsChargeTypeAllowed(chargeType, totalMassKg))
        {
            o.error = chargeType == ChargeType.HE
                ? "Недостаточная масса боеприпаса для фугасного типа. Минимум 0.500 кг."
                : chargeType == ChargeType.EQ
                    ? "Недостаточная масса боеприпаса для снаряженного типа. Минимум 1.000 кг."
                    : "Недопустимый тип боеприпаса.";
            return o;
        }

        o.totalProjectileMassKg = totalMassKg;

        float minPart = GetMinPartKg(totalMassKg);

        float expMass = input.explosiveMassKg;
        float deMass = input.damageElementMassKg;
        DamageElementType deType = input.damageElementType;
        FuzeType fuze = input.fuzeType;
        int expTier = input.explosiveTier;
        int deTier = input.damageElementTier;
        int buckshotCount = input.buckshotCount;
        AreaType area = NormalizeAreaType(input.areaType);

        switch (chargeType)
        {
            case ChargeType.FM:
                expTier = 0;
                expMass = 0f;
                deType = DamageElementType.None;
                buckshotCount = 0;
                deTier = 0;
                deMass = 0f;
                fuze = FuzeType.No;
                area = AreaType.Point;
                break;

            case ChargeType.HE:
                deType = DamageElementType.Shrapnel;
                buckshotCount = 0;
                deTier = 0;
                deMass = 0f;
                area = AreaType.Sphere;
                expMass = Mathf.Clamp(expMass, minPart, Mathf.Max(minPart, totalMassKg - minPart));
                expMass = Ceil3(expMass);
                break;

            case ChargeType.EQ:
                if (deType == DamageElementType.None || deType == DamageElementType.Shrapnel)
                    deType = DamageElementType.Buckshot;

                if (deType == DamageElementType.Buckshot)
                    buckshotCount = Mathf.Clamp(buckshotCount, 2, 10);
                else
                    buckshotCount = 0;

                expMass = Mathf.Max(expMass, minPart);
                deMass = Mathf.Max(deMass, minPart);

                {
                    float maxExp = Mathf.Max(minPart, totalMassKg - minPart - deMass);
                    expMass = Mathf.Clamp(expMass, minPart, maxExp);
                    expMass = Ceil3(expMass);
                }

                {
                    float maxDe = Mathf.Max(minPart, totalMassKg - minPart - expMass);
                    deMass = Mathf.Clamp(deMass, minPart, maxDe);
                    deMass = Ceil3(deMass);
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
                    default:
                        area = AreaType.Point;
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
        o.explosiveTier = expTier;
        o.explosiveMassKg = expMass;
        o.damageElementType = deType;
        o.buckshotCount = buckshotCount;
        o.damageElementTier = deTier;
        o.damageElementMassKg = deMass;
        o.fuzeType = fuze;
        o.areaType = area;

        float shellCoeff = TierCoeffs.Get(shellTier);
        o.shellStrength = Ceil3(shellMass * shellCoeff);

        o.explosivePower = (expTier > 0 && expMass > 0f)
            ? Ceil3(expMass * TierCoeffs.Get(expTier))
            : 0f;

        bool canExplode = o.explosivePower > o.shellStrength;

        if (chargeType != ChargeType.FM && !canExplode)
        {
            o.weakExplosiveCharge = true;
            o.weakExplosiveChargeWarning = "Слабый разрывной заряд. Боеприпас не сработает в стандартных условиях.";
        }

        o.damageRadius = 0f;
        o.areaPenetration = 0f;
        o.areaDamage = 0f;
        o.coneAngleDeg = 0f;
        o.buckshotSpreadAngleDeg = 0f;
        o.buckshotSingleMassKg = 0f;

        if (chargeType == ChargeType.HE)
        {
            if (canExplode)
            {
                if (fuze == FuzeType.No)
                    o.damageRadius = Ceil3(Mathf.Sqrt(o.explosivePower));
                else
                    o.damageRadius = Ceil3(Mathf.Sqrt(Mathf.Max(0f, o.explosivePower - o.shellStrength)));

                o.areaPenetration = Ceil3(o.explosivePower * shellMass * shellCoeff);
                o.areaDamage = Ceil3(shellMass * shellCoeff);

                if (fuze == FuzeType.No || fuze == FuzeType.Se)
                    o.areaPenetration = Ceil3(o.areaPenetration * 0.5f);
            }
        }
        else if (chargeType == ChargeType.EQ && deTier >= 1 && deMass > 0f)
        {
            float deCoeff = TierCoeffs.Get(deTier);

            switch (deType)
            {
                case DamageElementType.Buckshot:
                    o.buckshotSingleMassKg = Ceil3(deMass / Mathf.Max(1, buckshotCount));
                    o.buckshotSpreadAngleDeg = Ceil3((d / l) * 100f);
                    break;

                case DamageElementType.Pellet:
                case DamageElementType.Fire:
                case DamageElementType.Chemical:
                case DamageElementType.Energy:
                    if (canExplode)
                    {
                        if (fuze == FuzeType.No)
                            o.damageRadius = Ceil3(Mathf.Sqrt(o.explosivePower));
                        else
                            o.damageRadius = Ceil3(Mathf.Sqrt(Mathf.Max(0f, o.explosivePower - o.shellStrength)));

                        o.areaPenetration = Ceil3(o.explosivePower * deMass * deCoeff);
                        o.areaDamage = Ceil3(deMass * deCoeff);

                        if (fuze == FuzeType.No || fuze == FuzeType.Se)
                            o.areaPenetration = Ceil3(o.areaPenetration * 0.5f);

                        if (deType == DamageElementType.Pellet && area == AreaType.Cone)
                        {
                            o.coneAngleDeg = Ceil3((d / l) * 100f);
                            float A = Mathf.Sqrt(360f / Mathf.Max(o.coneAngleDeg, 0.001f));
                            o.areaDamage = Ceil3(o.areaDamage * A);
                        }
                    }
                    break;
            }
        }

        if (area == AreaType.Cone && deType != DamageElementType.Buckshot)
            o.coneAngleDeg = Ceil3((d / l) * 100f);

        int propTier = input.propellantTier;
        float propMass = Ceil3(Mathf.Max(input.propellantMassKg, 0.001f));
        o.propellantTier = propTier;
        o.propellantMassKg = propMass;
        o.propulsionForce = Ceil3(propMass * TierCoeffs.Get(propTier));

        int caseTier = input.caseTier;
        float caseMass = Ceil3(Mathf.Max(input.caseMassKg, 0.001f));
        o.caseTier = caseTier;
        o.caseMassKg = caseMass;
        o.caseStrength = Ceil3(caseMass * TierCoeffs.Get(caseTier));

        o.totalAmmoMassKg = Ceil3(totalMassKg + propMass + caseMass);
        o.effectiveGravity = CalculateEffectiveGravity(o, effectiveGravityA, effectiveGravityB);

        o.ammoCode = AmmoValidator.BuildCode(o);

        return o;
    }

    public static bool IsBarrelGeometryValid(AmmoOutput ammo, BarrelInput barrel)
    {
        if (ammo == null || barrel == null) return false;
        if (barrel.barrelLengthMm < ammo.lengthMm) return false;

        float maxBarrelD = Mathf.Floor(ammo.diameterMm * 1.25f);
        if (maxBarrelD < ammo.diameterMm) maxBarrelD = ammo.diameterMm;
        if (barrel.barrelDiameterMm < ammo.diameterMm) return false;
        if (barrel.barrelDiameterMm > maxBarrelD) return false;

        return true;
    }

    public static float CalculateBaseSpeed(float propulsionForce, float projectileMassKg)
    {
        float m = Mathf.Max(projectileMassKg, 0.000001f);
        float ratio = Mathf.Max(propulsionForce, 0f) / m;
        float n = (Mathf.Sqrt(1f + 8f * ratio) - 1f) * 0.5f;
        return 100f * n;
    }

    public static float CalculateEffectiveGravity(AmmoOutput ammo, float A = 8f, float B = 145f)
    {
        if (ammo == null) return GAME_GRAVITY;

        float d = Mathf.Max(ammo.diameterMm / 1000f, 0.000001f);
        float l = Mathf.Max(ammo.lengthMm / 1000f, 0.000001f);
        float m = Mathf.Max(ammo.totalProjectileMassKg, 0.000001f);
        float t = Mathf.Max(TierCoeffs.Get(ammo.shellTier), 0.000001f);

        const float a = 0.55f;
        const float b = 0.30f;
        const float c = 0.30f;

        float sectionalTerm = (d * d) / m;
        float shapeTerm = d / l;

        float geff =
            A +
            B *
            Mathf.Pow(sectionalTerm, a) *
            Mathf.Pow(shapeTerm, b) *
            Mathf.Pow(t, -c);

        return Ceil3(Mathf.Max(geff, 0.001f));
    }

    public static float CalculatePropellantMassForMaxSpeed(
        AmmoOutput ammo,
        BarrelInput barrel)
    {
        if (ammo == null || barrel == null) return 0.001f;
        if (!IsBarrelGeometryValid(ammo, barrel)) return 0.001f;

        float d = ammo.diameterMm;
        float D = barrel.barrelDiameterMm;
        float L = barrel.barrelLengthMm;
        float mass = Mathf.Max(ammo.totalProjectileMassKg, 0.000001f);

        float Z = d / D;
        float Z2 = Z * Z;
        float barrelMultiplier = Mathf.Min(10f, Mathf.Sqrt(L / D));

        float totalMultiplier = barrelMultiplier * Z2;
        if (totalMultiplier <= 0f) return 0.001f;

        float targetSpeed = ammo.propellantTier * 300f;
        float baseSpeedNeeded = targetSpeed / totalMultiplier;

        float n = baseSpeedNeeded / 100f;
        float requiredForce = mass * (n * (n + 1f) * 0.5f);

        float tierCoeff = TierCoeffs.Get(ammo.propellantTier);
        if (tierCoeff <= 0f) return 0.001f;

        float requiredPropellantMass = requiredForce / tierCoeff;
        return Mathf.Max(0.001f, Ceil3(requiredPropellantMass));
    }

    public static float CalculateCaseMassForMinStrength(AmmoOutput ammo)
    {
        if (ammo == null) return 0.001f;

        float requiredStrength = Mathf.Max(ammo.propulsionForce, 0f);
        float tierCoeff = TierCoeffs.Get(ammo.caseTier);
        if (tierCoeff <= 0f) return 0.001f;

        float requiredCaseMass = requiredStrength / tierCoeff;
        return Mathf.Max(0.001f, Ceil3(requiredCaseMass));
    }

    public static BarrelOutput CalculateBarrel(AmmoOutput ammo, BarrelInput barrel)
    {
        var b = new BarrelOutput();

        if (ammo == null || barrel == null)
        {
            b.valid = false;
            b.error = "Неверные параметры ствола.";
            return b;
        }

        if (!IsBarrelGeometryValid(ammo, barrel))
        {
            b.valid = false;
            b.error = "Неверные параметры ствола.";
            return b;
        }

        float d = ammo.diameterMm;
        float l = ammo.lengthMm;
        float D = barrel.barrelDiameterMm;
        float L = barrel.barrelLengthMm;

        float dM = d / 1000f;
        float mass = Mathf.Max(ammo.totalProjectileMassKg, 0.000001f);

        float Z = d / D;
        float Z2 = Z * Z;

        float baseSpeed = CalculateBaseSpeed(ammo.propulsionForce, mass);
        float barrelMultiplier = Mathf.Min(10f, Mathf.Sqrt(L / D));
        float rawSpeed = baseSpeed * barrelMultiplier * Z2;

        float maxSpeedByPropellantTier = ammo.propellantTier * 300f;
        b.projectileSpeed = Ceil3(Mathf.Min(rawSpeed, maxSpeedByPropellantTier));

        b.accuracy = Ceil3(30f * D * d / (L * l) / Mathf.Max(Z2, 0.000001f));

        float minRequiredStrength = SHELL_STRENGTH_SAFETY_RATIO * ammo.propulsionForce;
        if (ammo.shellStrength < minRequiredStrength)
        {
            b.valid = false;
            b.error = "Боеприпас разрушится в стволе. Увеличьте прочность оболочки или уменьшите метательный заряд.";
            return b;
        }

        float angleDeg = NormalizeAngleDeg(barrel.shotAngleDeg);
        float angleRad = angleDeg * Mathf.Deg2Rad;

        float speed2 = b.projectileSpeed * b.projectileSpeed;
        float geff = Mathf.Max(ammo.effectiveGravity, 0.0001f);

        float rangeCoeff = speed2 / geff;
        float heightCoeff = speed2 / (2f * geff);

        float sin2A = Mathf.Sin(2f * angleRad);
        if (sin2A < 0f) sin2A = 0f;

        float sinA = Mathf.Sin(angleRad);
        float sinA2 = sinA * sinA;

        b.flightDistance = Ceil3(rangeCoeff * sin2A);
        b.maxHeight = Ceil3(SHOT_HEIGHT_METERS + heightCoeff * sinA2);

        if (ammo.fuzeType == FuzeType.Ct)
        {
            b.directDamage = 0f;
            b.directPenetration = 0f;
        }
        else
        {
            b.directDamage = Ceil3(b.projectileSpeed * ammo.totalProjectileMassKg * dM);
            b.directPenetration = Ceil3(
                b.projectileSpeed * ammo.shellMassKg * TierCoeffs.Get(ammo.shellTier) / Mathf.Max(dM, 0.000001f));
        }

        return b;
    }

    public static List<ResourceCost> CalculateCosts(AmmoOutput o)
    {
        var costs = new List<ResourceCost>();

        costs.Add(new ResourceCost(ResourcesStorage.ResourceType.Metal, o.shellTier, o.shellMassKg));

        if (o.chargeType != ChargeType.FM && o.explosiveMassKg > 0f && o.explosiveTier >= 1)
        {
            costs.Add(new ResourceCost(ResourcesStorage.ResourceType.Chemicals, o.explosiveTier, o.explosiveMassKg));
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

            costs.Add(new ResourceCost(ResourcesStorage.ResourceType.Nanites, fuzeTier, fuzeMassKg));
        }

        costs.Add(new ResourceCost(ResourcesStorage.ResourceType.Chemicals, o.propellantTier, o.propellantMassKg));
        costs.Add(new ResourceCost(ResourcesStorage.ResourceType.Metal, o.caseTier, o.caseMassKg));

        long energyCost = (long)Mathf.Ceil(o.totalAmmoMassKg * 10f);
        costs.Add(ResourceCost.Energy(energyCost));

        return costs;
    }

    public static string ValidateResources(
        ResourcesStorage storage, List<ResourceCost> costsPerAmmo, int count)
    {
        if (storage == null) return "Не назначен склад ресурсов.";
        if (count <= 0) return "Количество должно быть > 0.";

        foreach (var cost in costsPerAmmo)
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
        ResourcesStorage storage, List<ResourceCost> costsPerAmmo, int count)
    {
        string err = ValidateResources(storage, costsPerAmmo, count);
        if (!string.IsNullOrEmpty(err)) return false;

        foreach (var cost in costsPerAmmo)
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
}