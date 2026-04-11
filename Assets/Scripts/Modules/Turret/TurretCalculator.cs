// TurretCalculator.cs
using System;
using UnityEngine;

/// <summary>
/// Чистая математика турели.
/// Не знает о UI, storage, сцене и крафт-пайплайне.
/// </summary>
public static class TurretCalculator
{
    // =========================================
    // КОНСТАНТЫ
    // =========================================

    public const int MinComponentPercent = 1;
    public const float BarrelDensity = 1f; // кг/дм³
    private const float Mm3ToDm3 = 1e-6f; // ИСПРАВЛЕНО: было 1e-9f

    // =========================================
    // ВХОДНЫЕ ДАННЫЕ
    // =========================================

    public struct ReceiverInput
    {
        public float totalMassKg;       // из ModuleScaler
        public int alloyTier;           // ИЗМЕНЕНО: был corpusTier
        public int loadingTier;
        public int chamberTier;
        public int loadingPercent;      // высокий приоритет
        public int chamberPercent;      // высокий приоритет
    }

    public struct BarrelInput
    {
        public float innerDiameterMm;
        public float outerDiameterMm;
        public float lengthMm;
    }

    public struct MountInput
    {
        public float mountTotalMass;    // = receiverTotalMass * mountCoeff
        public int alloyTier;           // ИЗМЕНЕНО: был corpusTier
        public int gyroPercent;         // ИЗМЕНЕНО: высокий приоритет
        public int compensatorPercent;  // ИЗМЕНЕНО: высокий приоритет
                                        // motorPercent = 100 - gyro - compensator
    }

    public struct AlloyInput
    {
        public bool hasAlloy;
        public int tier;
        public int kineticAbsorption;
        public float kineticResistance;
    }

    // =========================================
    // РЕЗУЛЬТАТ
    // =========================================

    public struct Result
    {
        // Receiver
        public float receiverMassKg;    // ДОБАВЛЕНО
        public float corpusMassKg;
        public float loadingMassKg;
        public float chamberMassKg;
        public int corpusPercent;

        public float loadingPower;
        public float chamberCapacity;
        public int maxAmmoTier;
        public float receiverDurability;

        // Barrel
        public float barrelMassKg;
        public float barrelStrengthCoeff;
        public float barrelWallThicknessMm;

        // Mount
        public float mountTotalMass;
        public float motorMassKg;
        public float gyroMassKg;
        public float compensatorMassKg;
        public int motorPercent;        // ИЗМЕНЕНО: теперь вычисляемый

        public float aimSpeed;
        public float recoilResistance;
        public float rotationSpeed;

        // Итого
        public float totalTurretMass;
        public float totalDurability;

        // Совместимость с боеприпасом
        public float minCaliberMm;
        public float maxCaliberMm;
        public float maxAmmoLengthMm;

        // Крафт
        public float craftTimeSeconds;
        public long energyCost;
    }

    // =========================================
    // ОСНОВНОЙ РАСЧЁТ
    // =========================================

    public static Result Calculate(
        StandardTurret template,
        ModuleScaler scaler,
        ReceiverInput receiver,
        BarrelInput barrel,
        MountInput mount,
        AlloyInput alloy,
        int workbenchTier,
        float innerVolumeM3)
    {
        Result r = new Result();

        if (template == null || scaler == null)
            return r;

        // ---- RECEIVER ----
        CalculateReceiver(template, receiver, alloy, ref r);

        // ---- BARREL ----
        CalculateBarrel(barrel, alloy, ref r);

        // ---- MOUNT ----
        CalculateMount(template, mount, ref r);

        // ---- ИТОГО ----
        r.receiverMassKg = receiver.totalMassKg;
        r.totalTurretMass =
            r.receiverMassKg +
            r.mountTotalMass +
            r.barrelMassKg;

        r.totalDurability =
            r.receiverDurability *
            r.barrelStrengthCoeff *
            template.MountCoeff;

        r.totalDurability = Round3(Mathf.Max(0f, r.totalDurability));

        // ---- СОВМЕСТИМОСТЬ С БОЕПРИПАСАМИ ----
        r.minCaliberMm = Round2(barrel.innerDiameterMm * 0.75f);
        r.maxCaliberMm = Round2(barrel.innerDiameterMm);
        r.maxAmmoLengthMm = Round1(barrel.lengthMm);

        // ---- КРАФТ ----
        float moduleCoeff = TierCoeffs.Get(template.ModuleTier);
        float wbCoeff = Mathf.Max(TierCoeffs.Get(workbenchTier), 0.0001f);
        float safeInner = Mathf.Max(innerVolumeM3, 0.0001f);

        r.craftTimeSeconds = Round3(
            (r.totalTurretMass * moduleCoeff * template.CraftCoefficient) /
            (wbCoeff * safeInner));

        r.energyCost = (long)Math.Ceiling(r.totalTurretMass * safeInner);

        return r;
    }

    // =========================================
    // RECEIVER
    // =========================================

    private static void CalculateReceiver(
        StandardTurret template,
        ReceiverInput inp,
        AlloyInput alloy,
        ref Result r)
    {
        float total = inp.totalMassKg;

        // Clamp процентов с высоким приоритетом
        int lp = Mathf.Clamp(inp.loadingPercent, MinComponentPercent, 98);
        int cp = Mathf.Clamp(inp.chamberPercent, MinComponentPercent, 99 - lp);
        int corpP = 100 - lp - cp;
        corpP = Mathf.Max(corpP, MinComponentPercent);

        r.loadingMassKg = Round3(total * lp / 100f);
        r.chamberMassKg = Round3(total * cp / 100f);
        r.corpusMassKg = Round3(Mathf.Max(0f, total - r.loadingMassKg - r.chamberMassKg));
        r.corpusPercent = corpP;

        // Мощность механизма заряжания
        r.loadingPower = Round3(
            r.loadingMassKg *
            TierCoeffs.Get(inp.loadingTier) *
            template.LoadingPowerCoeff);

        // Вместимость патронника
        r.chamberCapacity = Round3(
      r.chamberMassKg *
      template.ChamberCapacityCoeff);

        // Максимальный тир боеприпаса
        r.maxAmmoTier = Mathf.Clamp(inp.chamberTier + template.AmmoTierBonus, 1, 10);

        // Прочность корпуса
        r.receiverDurability = Round3(
    r.corpusMassKg *
    TierCoeffs.Get(inp.alloyTier) *
    template.DurabilityCoeff);
    }

    // =========================================
    // BARREL
    // =========================================

    private static void CalculateBarrel(
        BarrelInput barrel,
        AlloyInput alloy,
        ref Result r)
    {
        float d = Mathf.Max(barrel.innerDiameterMm, 0.001f);
        float D = Mathf.Max(barrel.outerDiameterMm, d + 0.001f);
        float L = Mathf.Max(barrel.lengthMm, d);

        r.barrelWallThicknessMm = Round2((D - d) / 2f);

        // Масса ствола
        float volumeMm3 = Mathf.PI / 4f * (D * D - d * d) * L;
        float volumeDm3 = volumeMm3 * Mm3ToDm3;
        r.barrelMassKg = Round3(volumeDm3 * BarrelDensity);

        // Коэффициент прочности ствола
        float wallThickness = (D - d) / 2f;
        float meanDiameter = (D + d) / 2f;

        float numerator = wallThickness / d;
        float denominator = L / Mathf.Max(meanDiameter, 0.001f);

        r.barrelStrengthCoeff = denominator > 0f
            ? Round4(numerator / denominator)
            : 1f;
    }

    // =========================================
    // MOUNT
    // =========================================

    private static void CalculateMount(
        StandardTurret template,
        MountInput inp,
        ref Result r)
    {
        r.mountTotalMass = Round3(inp.mountTotalMass);

        // Двигатель — низкий приоритет (остаток)
        int gp = Mathf.Clamp(inp.gyroPercent, MinComponentPercent, 98);
        int cop = Mathf.Clamp(inp.compensatorPercent, MinComponentPercent, 99 - gp);
        int mp = Mathf.Max(MinComponentPercent, 100 - gp - cop);

        r.motorPercent = mp;

        r.motorMassKg = Round3(r.mountTotalMass * mp / 100f);
        r.gyroMassKg = Round3(r.mountTotalMass * gp / 100f);
        r.compensatorMassKg = Round3(Mathf.Max(0f,
            r.mountTotalMass - r.motorMassKg - r.gyroMassKg));

        float tierCoeff = TierCoeffs.Get(inp.alloyTier);

        r.aimSpeed = Round3(r.gyroMassKg * tierCoeff * template.AimSpeedCoeff);
        r.recoilResistance = Round3(r.compensatorMassKg * tierCoeff * template.RecoilCoeff);
        r.rotationSpeed = Round3(r.motorMassKg * tierCoeff * template.RotationSpeedCoeff);
    }

    // =========================================
    // СОВМЕСТИМОСТЬ БОЕПРИПАСА
    // =========================================

    public struct AmmoCompatibilityResult
    {
        public bool isCompatible;
        public string reason;

        public bool isCannonball;
        public float diameterMm;
        public float lengthMm;
        public float ammoMassKg;
        public int ammoTier;

        // Параметры для preview стрельбы
        public AmmoCalc.AmmoOutput ammoOutput;
        public CannonballCalc.CannonballOutput cannonballOutput;
    }

    public static AmmoCompatibilityResult CheckAmmoCompatibility(
        string ammoCode,
        Result turretResult,
        float barrelInnerDiameterMm,
        float barrelLengthMm)
    {
        var res = new AmmoCompatibilityResult();

        if (string.IsNullOrEmpty(ammoCode))
        {
            res.reason = "Код боеприпаса пуст.";
            return res;
        }

        bool isCannonball = ammoCode.StartsWith(CannonballValidator.Prefix,
            StringComparison.Ordinal);

        if (isCannonball)
        {
            if (!CannonballValidator.TryParseCode(ammoCode,
                out var cbInput, out var cbErr))
            {
                res.reason = cbErr;
                return res;
            }

            var cbOutput = CannonballCalc.Calculate(cbInput);
            if (cbOutput == null || !string.IsNullOrEmpty(cbOutput.error))
            {
                res.reason = cbOutput?.error ?? "Ошибка расчёта ядра.";
                return res;
            }

            res.isCannonball = true;
            res.diameterMm = cbOutput.diameterMm;
            res.lengthMm = cbOutput.lengthMm;
            res.ammoMassKg = cbOutput.totalCannonballMassKg;
            res.ammoTier = GetCannonballMaxTier(cbInput);
            res.cannonballOutput = cbOutput;
        }
        else
        {
            if (!AmmoValidator.TryParseCode(ammoCode,
                out var ammoInput, out var ammoErr))
            {
                res.reason = ammoErr;
                return res;
            }

            var ammoOutput = AmmoCalc.Calculate(ammoInput);
            if (ammoOutput == null || !string.IsNullOrEmpty(ammoOutput.error))
            {
                res.reason = ammoOutput?.error ?? "Ошибка расчёта боеприпаса.";
                return res;
            }

            res.isCannonball = false;
            res.diameterMm = ammoOutput.diameterMm;
            res.lengthMm = ammoOutput.lengthMm;
            res.ammoMassKg = ammoOutput.totalAmmoMassKg;
            res.ammoTier = GetAmmoMaxTier(ammoInput);
            res.ammoOutput = ammoOutput;
        }

        // Проверка совместимости со стволом
        if (res.diameterMm < barrelInnerDiameterMm * 0.75f)
        {
            res.reason = $"Диаметр боеприпаса ({res.diameterMm:F1} мм) " +
                         $"слишком мал для ствола (мин. {barrelInnerDiameterMm * 0.75f:F1} мм).";
            return res;
        }

        if (res.diameterMm > barrelInnerDiameterMm)
        {
            res.reason = $"Диаметр боеприпаса ({res.diameterMm:F1} мм) " +
                         $"превышает внутренний диаметр ствола ({barrelInnerDiameterMm:F1} мм).";
            return res;
        }

        if (res.lengthMm > barrelLengthMm)
        {
            res.reason = $"Длина боеприпаса ({res.lengthMm:F1} мм) " +
                         $"превышает длину ствола ({barrelLengthMm:F1} мм).";
            return res;
        }

        // Проверка совместимости с патронником
        if (res.ammoMassKg > turretResult.chamberCapacity)
        {
            res.reason = $"Масса боеприпаса ({res.ammoMassKg:F3} кг) " +
                         $"превышает вместимость патронника ({turretResult.chamberCapacity:F3} кг).";
            return res;
        }

        if (res.ammoTier > turretResult.maxAmmoTier)
        {
            res.reason = $"Тир боеприпаса ({res.ammoTier}) " +
                         $"превышает максимальный тир патронника ({turretResult.maxAmmoTier}).";
            return res;
        }

        res.isCompatible = true;
        res.reason = "";
        return res;
    }

    // =========================================
    // ОПРЕДЕЛЕНИЕ ТИРА БОЕПРИПАСА
    // =========================================

    private static int GetAmmoMaxTier(AmmoCalc.AmmoInput inp)
    {
        int t = inp.shellTier;
        if (inp.explosiveTier > 0) t = Mathf.Max(t, inp.explosiveTier);
        if (inp.damageElementTier > 0) t = Mathf.Max(t, inp.damageElementTier);
        if (inp.propellantTier > 0) t = Mathf.Max(t, inp.propellantTier);
        if (inp.caseTier > 0) t = Mathf.Max(t, inp.caseTier);
        return t;
    }

    private static int GetCannonballMaxTier(CannonballCalc.CannonballInput inp)
    {
        int t = inp.shellTier;
        if (inp.explosiveTier > 0) t = Mathf.Max(t, inp.explosiveTier);
        if (inp.damageElementTier > 0) t = Mathf.Max(t, inp.damageElementTier);
        return t;
    }

    // =========================================
    // PREVIEW ВЫСТРЕЛА
    // =========================================

    public struct ShotPreview
    {
        public bool valid;
        public string error;
        public bool isCannonball;

        public float projectileSpeed;
        public float accuracy;
        public float flightDistance;
        public float maxHeight;
        public float flightTime;
        public float directFireRange;
        public float directDamage;
        public float directPenetration;

        // НОВЫЕ ПОЛЯ
        public float rateOfFireRPM;     // Скорострельность (выстр/мин)
        public float reloadTimeS;       // Перезарядка (сек)
    }

    public static ShotPreview CalculateShotPreview(
        AmmoCompatibilityResult ammo,
        BarrelInput barrel,
        Result turretResult,
        float previewAngleDeg,
        int propellantTier,
        float propellantMassKg,
        float loadingPowerCoeff)
    {
        var preview = new ShotPreview();

        if (!ammo.isCompatible)
        {
            preview.error = ammo.reason;
            return preview;
        }

        preview.isCannonball = ammo.isCannonball;

        // ---- РАСЧЁТ СКОРОСТРЕЛЬНОСТИ И ПЕРЕЗАРЯДКИ ----
        float loadingPower = turretResult.loadingPower;

        if (loadingPower > 0f && ammo.ammoMassKg > 0f)
        {
            // Формула: перезарядка = масса * 10 / корень(мощность) / coeff
            // speedFactor = корень(мощность) * coeff / 10
            float speedFactor = Mathf.Sqrt(loadingPower) * loadingPowerCoeff / 10f;

            if (ammo.isCannonball)
            {
                float safeCharge = Mathf.Max(0.001f, propellantMassKg);
                float reloadS = (ammo.ammoMassKg / speedFactor) + (safeCharge / speedFactor) * 9f;
                preview.reloadTimeS = (float)Math.Ceiling(reloadS * 100f) / 100f;
                preview.rateOfFireRPM = Round2(60f / reloadS);
            }
            else
            {
                float reloadS = ammo.ammoMassKg / speedFactor;
                preview.reloadTimeS = (float)Math.Ceiling(reloadS * 100f) / 100f;
                preview.rateOfFireRPM = Round2(60f / reloadS);
            }
        }

        // ---- БАЛЛИСТИКА ----
        if (ammo.isCannonball)
        {
            var bi = new CannonballCalc.BarrelInput
            {
                barrelDiameterMm = barrel.innerDiameterMm,
                barrelLengthMm = barrel.lengthMm,
                shotAngleDeg = previewAngleDeg,
                propellantTier = propellantTier,
                propellantMassKg = propellantMassKg
            };

            var bo = CannonballCalc.CalculateBarrel(ammo.cannonballOutput, bi);

            preview.valid = bo.valid;
            preview.error = bo.error;
            preview.projectileSpeed = bo.projectileSpeed;
            preview.accuracy = bo.accuracy;
            preview.flightDistance = bo.flightDistance;
            preview.maxHeight = bo.maxHeight;
            preview.flightTime = bo.flightTime;
            preview.directFireRange = bo.directFireRange;
            preview.directDamage = bo.directDamage;
            preview.directPenetration = bo.directPenetration;
        }
        else
        {
            var bi = new AmmoCalc.BarrelInput
            {
                barrelDiameterMm = barrel.innerDiameterMm,
                barrelLengthMm = barrel.lengthMm,
                shotAngleDeg = previewAngleDeg
            };

            var bo = AmmoCalc.CalculateBarrel(ammo.ammoOutput, bi);

            preview.valid = bo.valid;
            preview.error = bo.error;
            preview.projectileSpeed = bo.projectileSpeed;
            preview.accuracy = bo.accuracy;
            preview.flightDistance = bo.flightDistance;
            preview.maxHeight = bo.maxHeight;
            preview.flightTime = bo.flightTime;
            preview.directFireRange = bo.directFireRange;
            preview.directDamage = bo.directDamage;
            preview.directPenetration = bo.directPenetration;
        }

        return preview;
    }

    // =========================================
    // ВСПОМОГАТЕЛЬНЫЕ
    // =========================================

    private static float Round1(float v) => (float)Math.Round(v, 1);
    private static float Round2(float v) => (float)Math.Round(v, 2);
    private static float Round3(float v) => (float)Math.Round(v, 3);
    private static float Round4(float v) => (float)Math.Round(v, 4);
}