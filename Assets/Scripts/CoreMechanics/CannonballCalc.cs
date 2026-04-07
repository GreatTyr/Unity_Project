using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Все расчёты для ядра (тип C).
/// Чистая статическая логика без MonoBehaviour.
/// Не зависит от UI, складов и верстаков.
/// </summary>
public static class CannonballCalc
{
    public enum ChargeType { FM = 0, HE = 1, EQ = 2 }

    public enum DamageElementType
    {
        None = 0,
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
    public class CannonballInput
    {
        [Header("Тип боеприпаса")]
        public ChargeType chargeType = ChargeType.FM;

        [Header("Оболочка")]
        [Range(1, 10)] public int shellTier = 1;
        public float diameterMm = 10f;

        [Header("Разрывной заряд")]
        [Range(1, 10)] public int explosiveTier = 1;
        public float explosiveMassKg = 0f;

        [Header("Поражающий элемент")]
        public DamageElementType damageElementType = DamageElementType.Pellet;
        [Range(1, 10)] public int damageElementTier = 1;
        public float damageElementMassKg = 0f;

        [Header("Область поражения")]
        public AreaType areaType = AreaType.Point;

        [Header("Взрыватель")]
        public FuzeType fuzeType = FuzeType.No;

        [Header("Метательный заряд")]
        [Range(1, 10)] public int propellantTier = 1;
        public float propellantMassKg = 0.001f;

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
    public class CannonballOutput
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
        public int damageElementTier;
        public float damageElementMassKg;

        public AreaType areaType;
        public float damageRadius;
        public float areaPenetration;
        public float areaDamage;

        public FuzeType fuzeType;

        public int propellantTier;
        public float propellantMassKg;
        public float propulsionForce;

        public float totalAmmoMassKg;

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
        public float flightTime;

        public float maxRangeAt45Deg;
        public float directFireRange;

        public float directDamage;
        public float directPenetration;

        public float ballisticP;
        public float ballisticK;
        public float horizontalSpeed;
        public float verticalSpeed;
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
    private const float SHOT_HEIGHT_METERS = 0f;
    private const float MAX_UI_ANGLE_DEG = 90f;

    private const float REAL_GRAVITY = 9.81f;
    private const float C_RANGE_V2 = 0.040269f;

    private const float SPEED_MULTIPLIER = 0.7f;
    private const float ACCURACY_MULTIPLIER = 1.3f;
    private const float SHELL_STRENGTH_MULTIPLIER = 1.5f;

    // Масса шара при диаметре в мм и результате в кг.
    // Коэффициент уже включает:
    // - формулу объёма шара V = 4/3 * pi * (d/2)^3
    // - условную плотность 8 г/см^3
    // - перевод единиц в кг
    private const float SPHERE_MASS_COEFF = 0.0000041887902f;

    public static float Ceil3(float v) => Mathf.Ceil(v * 1000f) / 1000f;
    public static float Ceil2(float v) => Mathf.Ceil(v * 100f) / 100f;
    public static float Ceil1(float v) => Mathf.Ceil(v);

    public static float NormalizeDiameterMm(float v)
    {
        return Ceil2(Mathf.Clamp(v, 1f, 100000f));
    }

    public static float NormalizeMassKg(float v)
    {
        return Ceil3(Mathf.Max(0f, v));
    }

    public static float NormalizeAngleDeg(float v)
    {
        return Ceil3(Mathf.Clamp(v, 0f, MAX_UI_ANGLE_DEG));
    }

    public static float ProjectileMassKg(float diamMm)
    {
        return diamMm * diamMm * diamMm * SPHERE_MASS_COEFF;
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

    public static void NormalizeInput(CannonballInput input)
    {
        if (input == null) return;

        input.shellTier = Mathf.Clamp(input.shellTier, 1, 10);
        input.explosiveTier = Mathf.Clamp(input.explosiveTier, 1, 10);
        input.damageElementTier = Mathf.Clamp(input.damageElementTier, 1, 10);
        input.propellantTier = Mathf.Clamp(input.propellantTier, 1, 10);
        input.craftCount = Mathf.Max(1, input.craftCount);

        input.diameterMm = NormalizeDiameterMm(input.diameterMm);
        input.propellantMassKg = Ceil3(Mathf.Max(input.propellantMassKg, 0.001f));
        input.explosiveMassKg = NormalizeMassKg(input.explosiveMassKg);
        input.damageElementMassKg = NormalizeMassKg(input.damageElementMassKg);

        input.areaType = NormalizeAreaType(input.areaType);

        float totalMassKg = Ceil3(ProjectileMassKg(input.diameterMm));
        float minPart = GetMinPartKg(totalMassKg);

        if (!IsChargeTypeAllowed(input.chargeType, totalMassKg))
            input.chargeType = ChargeType.FM;

        switch (input.chargeType)
        {
            case ChargeType.FM:
                input.explosiveTier = 0;
                input.explosiveMassKg = 0f;
                input.damageElementType = DamageElementType.None;
                input.damageElementTier = 0;
                input.damageElementMassKg = 0f;
                input.fuzeType = FuzeType.No;
                input.areaType = AreaType.Point;
                break;

            case ChargeType.HE:
                input.damageElementType = DamageElementType.None;
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
                if (input.damageElementType == DamageElementType.None)
                    input.damageElementType = DamageElementType.Pellet;

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
                    case DamageElementType.Pellet:
                        input.areaType = AreaType.Sphere;
                        break;
                    case DamageElementType.Fire:
                    case DamageElementType.Chemical:
                    case DamageElementType.Energy:
                        input.areaType = AreaType.Cloud;
                        break;
                    default:
                        input.damageElementType = DamageElementType.Pellet;
                        input.areaType = AreaType.Sphere;
                        break;
                }
                break;
        }
    }

    public static CannonballOutput Calculate(CannonballInput input)
    {
        var o = new CannonballOutput
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
        float l = d;
        int shellTier = input.shellTier;
        ChargeType chargeType = input.chargeType;

        o.diameterMm = d;
        o.lengthMm = l;
        o.shellTier = shellTier;
        o.chargeType = chargeType;

        float totalMassKg = Ceil3(ProjectileMassKg(d));
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
        AreaType area = NormalizeAreaType(input.areaType);

        switch (chargeType)
        {
            case ChargeType.FM:
                expTier = 0;
                expMass = 0f;
                deType = DamageElementType.None;
                deTier = 0;
                deMass = 0f;
                fuze = FuzeType.No;
                area = AreaType.Point;
                break;

            case ChargeType.HE:
                deType = DamageElementType.None;
                deTier = 0;
                deMass = 0f;
                area = AreaType.Sphere;
                expMass = Mathf.Clamp(expMass, minPart, Mathf.Max(minPart, totalMassKg - minPart));
                expMass = Ceil3(expMass);
                break;

            case ChargeType.EQ:
                if (deType == DamageElementType.None)
                    deType = DamageElementType.Pellet;

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
                    case DamageElementType.Pellet:
                        area = AreaType.Sphere;
                        break;
                    case DamageElementType.Fire:
                    case DamageElementType.Chemical:
                    case DamageElementType.Energy:
                        area = AreaType.Cloud;
                        break;
                    default:
                        o.error = "Недопустимый поражающий элемент для ядра.";
                        return o;
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
        o.damageElementTier = deTier;
        o.damageElementMassKg = deMass;
        o.fuzeType = fuze;
        o.areaType = area;

        float shellCoeff = TierCoeffs.Get(shellTier);
        float baseShellStrength = shellMass * shellCoeff;
        o.shellStrength = Ceil3(baseShellStrength * SHELL_STRENGTH_MULTIPLIER);

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

        if (chargeType == ChargeType.HE)
        {
            if (canExplode)
            {
                if (fuze == FuzeType.No)
                    o.damageRadius = Ceil3(Mathf.Sqrt(o.explosivePower));
                else
                    o.damageRadius = Ceil3(Mathf.Sqrt(Mathf.Max(0f, o.explosivePower - o.shellStrength)));

                o.areaPenetration = Ceil3(o.explosivePower * o.shellStrength);
                o.areaDamage = Ceil3(o.shellStrength);

                if (fuze == FuzeType.No || fuze == FuzeType.Se)
                    o.areaPenetration = Ceil3(o.areaPenetration * 0.5f);
            }
        }
        else if (chargeType == ChargeType.EQ && deTier >= 1 && deMass > 0f)
        {
            float deCoeff = TierCoeffs.Get(deTier);

            switch (deType)
            {
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
                    }
                    break;
            }
        }

        int propTier = input.propellantTier;
        float propMass = Ceil3(Mathf.Max(input.propellantMassKg, 0.001f));
        o.propellantTier = propTier;
        o.propellantMassKg = propMass;
        o.propulsionForce = Ceil3(propMass * TierCoeffs.Get(propTier));

        o.totalAmmoMassKg = Ceil3(totalMassKg + propMass);

        o.ammoCode = CannonballValidator.BuildCode(o);

        return o;
    }

    public static bool IsBarrelGeometryValid(CannonballOutput ammo, BarrelInput barrel)
    {
        if (ammo == null || barrel == null) return false;
        if (barrel.barrelLengthMm < ammo.lengthMm) return false;

        float maxBarrelD = Ceil2(ammo.diameterMm * 1.25f);
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

    public static float CalculatePropellantMassForMaxSpeed(
        CannonballOutput ammo,
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

        float totalMultiplier = barrelMultiplier * Z2 * SPEED_MULTIPLIER;
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

    public static BarrelOutput CalculateBarrel(CannonballOutput ammo, BarrelInput barrel)
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
        float rawSpeed = baseSpeed * barrelMultiplier * Z2 * SPEED_MULTIPLIER;

        float maxSpeedByPropellantTier = ammo.propellantTier * 300f;
        b.projectileSpeed = Ceil3(Mathf.Min(rawSpeed, maxSpeedByPropellantTier));

        b.accuracy = Ceil3(
            30f * D * d / (L * l) / Mathf.Max(Z2, 0.000001f) * ACCURACY_MULTIPLIER);

        float minRequiredStrength = SHELL_STRENGTH_SAFETY_RATIO * ammo.propulsionForce;
        if (ammo.shellStrength < minRequiredStrength)
        {
            b.valid = false;
            b.error = "Боеприпас разрушится в стволе. Увеличьте прочность оболочки или уменьшите метательный заряд.";
            return b;
        }

        float angleDeg = NormalizeAngleDeg(barrel.shotAngleDeg);
        float angleRad = angleDeg * Mathf.Deg2Rad;

        float v = Mathf.Max(b.projectileSpeed, 0.0001f);
        float dMeters = Mathf.Max(ammo.diameterMm / 1000f, 0.000001f);
        float kL = Mathf.Max(ammo.lengthMm / Mathf.Max(ammo.diameterMm, 0.0001f), 0.0001f);

        float rMax = C_RANGE_V2 * v * v * Mathf.Pow(dMeters, 0.675f) * Mathf.Pow(kL, 0.6f);
        rMax = Mathf.Max(0f, rMax);

        float P = rMax * REAL_GRAVITY / (v * v);
        P = Mathf.Max(P, 0.0001f);

        float sqrtP = Mathf.Sqrt(P);

        float sinA = Mathf.Sin(angleRad);
        float cosA = Mathf.Cos(angleRad);
        float sinASqrt = Mathf.Sqrt(Mathf.Max(sinA, 0f));

        float K = 1f + (sqrtP - 1f) * sinASqrt;
        K = Mathf.Max(K, 0.0001f);

        float vx = v * cosA * K;
        float vy = P * v * sinA / K;

        float flightTime = 0f;
        if (vy > 0f)
            flightTime = 2f * vy / REAL_GRAVITY;

        float range = 0f;
        if (flightTime > 0f)
            range = vx * flightTime;

        float maxHeight = SHOT_HEIGHT_METERS;
        if (vy > 0f)
            maxHeight = SHOT_HEIGHT_METERS + (vy * vy) / (2f * REAL_GRAVITY);

        float directRange = 0f;
        {
            const float directMaxHeight = 1.8f;
            float bestRange = 0f;

            for (int i = 0; i <= 200; i++)
            {
                float testAngleDeg = 90f * i / 200f;
                float testAngleRad = testAngleDeg * Mathf.Deg2Rad;

                float testSin = Mathf.Sin(testAngleRad);
                float testCos = Mathf.Cos(testAngleRad);
                float testSinSqrt = Mathf.Sqrt(Mathf.Max(testSin, 0f));

                float testK = 1f + (sqrtP - 1f) * testSinSqrt;
                testK = Mathf.Max(testK, 0.0001f);

                float testVx = v * testCos * testK;
                float testVy = P * v * testSin / testK;

                float testHeight = 0f;
                if (testVy > 0f)
                    testHeight = (testVy * testVy) / (2f * REAL_GRAVITY);

                if (testHeight <= directMaxHeight)
                {
                    float testTime = 0f;
                    if (testVy > 0f)
                        testTime = 2f * testVy / REAL_GRAVITY;

                    float testRange = testVx * testTime;
                    if (testRange > bestRange)
                        bestRange = testRange;
                }
            }

            directRange = bestRange;
        }

        b.flightDistance = Ceil3(Mathf.Max(0f, range));
        b.maxHeight = Ceil3(Mathf.Max(0f, maxHeight));
        b.flightTime = Ceil3(Mathf.Max(0f, flightTime));

        b.maxRangeAt45Deg = Ceil3(rMax);
        b.directFireRange = Ceil3(Mathf.Max(0f, directRange));

        b.ballisticP = Ceil3(P);
        b.ballisticK = Ceil3(K);
        b.horizontalSpeed = Ceil3(vx);
        b.verticalSpeed = Ceil3(vy);

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

    public static List<ResourceCost> CalculateCosts(CannonballOutput o)
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