using System;
using UnityEngine;

/// <summary>
/// Общий payload уже рассчитанных параметров модуля.
/// Используется модульными контроллерами для сборки CommonModuleData + модульно-специфичных данных.
/// </summary>
[Serializable]
public struct CommonModuleCraftData
{
    // Identity
    public string moduleType;
    public int moduleTier;
    public string faction;
    public int referenceIndex;
    public string referenceName;

    // Alloy / shell
    public string alloyCode;
    public int alloyTier;
    public float shellPercent;

    // Scale / fill
    public float scaleFactor;
    public float fillPercent;

    // Geometry
    public float length;
    public float width;
    public float height;

    // Volumes
    public float aabbVolume;
    public float realVolume;
    public float shellVolumeM3;
    public float effectiveVolume;

    // Mass / durability
    public float shellMassKg;
    public float innerMassKg;
    public float totalMassKg;
    public float durability;
    public float wallThicknessMm;

    // Common thermal / craft
    public float heatCapacity;
    public float maxTemperature;
    public float heatingRate;
    public float craftTimeSeconds;

    // Common operational / static
    public string operationalResourceUsageSummary;
    public float staticCapacityMax;
    public float staticCapacityCurrent;
    public float staticCapacityDrainPerSecond;

    // Module code
    public string moduleCode;

    // Capabilities
    public bool canTurnOnOff;
    public float turnOnOffTime;
    public bool canPulseMode;
    public float pulseInterval;
    public bool isControllable;

    // Volatility / explosion
    public bool isVolatile;
    public DamageType explosionDamageType;
    public float explosionRadiusMeters;
    public float explosionPenetration;
    public float explosionDamage;

    // Build visual
    public float buildVisualYawOffset;
    public Vector3 buildAnchorLocal;
    public Vector2Int buildAnchorCellLocal;
    public Vector3 referenceVisualScale;
}