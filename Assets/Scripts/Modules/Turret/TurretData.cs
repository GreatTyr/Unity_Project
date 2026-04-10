// TurretData.cs
using System;
using UnityEngine;

/// <summary>
/// Данные готовой турели после крафта.
/// Хранит все параметры как сериализуемую запись.
/// </summary>
[Serializable]
public class TurretData : CommonModuleData
{
    [Header("Barrel")]
    public float barrelInnerDiameterMm;
    public float barrelOuterDiameterMm;
    public float barrelLengthMm;
    public float barrelMassKg;
    public float barrelStrengthCoeff;
    public float barrelWallThicknessMm;

    [Header("Receiver Components")]
    public int loadingPercent;
    public int loadingTier;
    public float loadingMassKg;

    public int chamberPercent;
    public int chamberTier;
    public float chamberMassKg;

    public int corpusPercent;
    public int corpusTier;
    public float corpusMassKg;

    public int ammoTierBonus;

    [Header("Receiver Derived")]
    public float loadingPower;
    public float chamberCapacity;
    public int maxAmmoTier;
    public float receiverDurability;

    [Header("Mount")]
    public float mountTotalMass;
    public int motorPercent;
    public float motorMassKg;
    public int gyroPercent;
    public float gyroMassKg;
    public int compensatorPercent;
    public float compensatorMassKg;

    public float aimSpeed;
    public float recoilResistance;
    public float rotationSpeed;

    public float maxElevationDeg;
    public float maxDepressionDeg;
    public float traverseArcDeg;
    public float energyConsumption;

    [Header("Ammo Compatibility")]
    public float minCaliberMm;
    public float maxCaliberMm;
    public float maxAmmoLengthMm;

    [Header("Cannonball Propellant Defaults")]
    public int defaultPropellantTier;
    public float defaultPropellantMassKg;
    public float maxPropellantMassKg;

    [Header("Totals")]
    public float totalTurretMass;
    public float totalDurability;

    public void Initialize(
        CommonModuleCraftData commonData,
        TurretCalculator.Result calc,
        TurretCalculator.BarrelInput barrelIn,
        int defaultPropellantTier,
        float defaultPropellantMassKg,
        StandardTurret template)
    {
        InitializeCommon(commonData);

        // Barrel
        barrelInnerDiameterMm = barrelIn.innerDiameterMm;
        barrelOuterDiameterMm = barrelIn.outerDiameterMm;
        barrelLengthMm = barrelIn.lengthMm;
        barrelMassKg = calc.barrelMassKg;
        barrelStrengthCoeff = calc.barrelStrengthCoeff;
        barrelWallThicknessMm = calc.barrelWallThicknessMm;

        // Receiver
        loadingPercent = calc.loadingMassKg > 0f
            ? Mathf.RoundToInt(calc.loadingMassKg / Mathf.Max(commonData.totalMassKg, 0.001f) * 100f)
            : 0;
        this.loadingPercent = calc.loadingMassKg > 0f
            ? Mathf.RoundToInt(calc.loadingMassKg / Mathf.Max(commonData.totalMassKg, 0.001f) * 100f)
            : 0;

        loadingMassKg = calc.loadingMassKg;
        chamberMassKg = calc.chamberMassKg;
        corpusMassKg = calc.corpusMassKg;
        loadingPercent = calc.corpusPercent > 0
            ? 100 - calc.corpusPercent - chamberPercent
            : 0;

        // Проще просто взять из результата калькулятора напрямую
        loadingMassKg = calc.loadingMassKg;
        chamberMassKg = calc.chamberMassKg;
        corpusMassKg = calc.corpusMassKg;
        corpusPercent = calc.corpusPercent;

        loadingPower = calc.loadingPower;
        chamberCapacity = calc.chamberCapacity;
        maxAmmoTier = calc.maxAmmoTier;
        receiverDurability = calc.receiverDurability;
        ammoTierBonus = template != null ? template.AmmoTierBonus : 0;

        // Mount
        mountTotalMass = calc.mountTotalMass;
        motorMassKg = calc.motorMassKg;
        gyroMassKg = calc.gyroMassKg;
        compensatorMassKg = calc.compensatorMassKg;
        compensatorPercent = calc.compensatorPercent;

        aimSpeed = calc.aimSpeed;
        recoilResistance = calc.recoilResistance;
        rotationSpeed = calc.rotationSpeed;

        if (template != null)
        {
            maxElevationDeg = template.MaxElevationDeg;
            maxDepressionDeg = template.MaxDepressionDeg;
            traverseArcDeg = template.TraverseArcDeg;
            energyConsumption = template.EnergyConsumption;
        }

        // Ammo compatibility
        minCaliberMm = calc.minCaliberMm;
        maxCaliberMm = calc.maxCaliberMm;
        maxAmmoLengthMm = calc.maxAmmoLengthMm;

        // Propellant
        this.defaultPropellantTier = defaultPropellantTier;
        this.defaultPropellantMassKg = defaultPropellantMassKg;
        this.maxPropellantMassKg = calc.maxPropellantMassKg;

        // Totals
        totalTurretMass = calc.totalTurretMass;
        totalDurability = calc.totalDurability;
    }
}