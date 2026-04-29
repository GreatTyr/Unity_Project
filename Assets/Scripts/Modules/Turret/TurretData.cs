// TurretData.cs
using System;
using UnityEngine;

/// <summary>
/// Данные готовой турели после крафта.
/// Хранит все параметры как сериализуемую запись.
/// </summary>
[Serializable]
public class TurretData : ModuleCommonData
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
    public float corpusMassKg;

    public int ammoTierBonus;

    [Header("Receiver Derived")]
    public float receiverMassKg;
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

    [Header("Totals")]
    public float totalTurretMass;
    public float totalDurability;

    
}