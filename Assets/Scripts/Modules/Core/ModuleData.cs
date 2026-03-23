using System;
using UnityEngine;

/// <summary>
/// Базовые данные готового модуля.
/// Это уже не craft-DTO и не editor-настройка, а итоговый набор параметров готового объекта.
/// Общие thermal/operational параметры вынесены в CommonModuleData.
/// </summary>
[Serializable]
public class ModuleData
{
    // Identity
    public string moduleType;
    public int moduleTier;
    public string faction;
    public int referenceIndex;
    public string referenceName;

    // Alloy
    public string alloyCode;
    public int alloyTier;

    // Shell
    public float shellPercent;

    // Dimensions (метры)
    public float length;
    public float width;
    public float height;

    // Volumes (м³)
    public float aabbVolume;
    public float realVolume;
    public float shellVolumeM3;
    public float effectiveVolume;

    // Mass (кг)
    public float shellMassKg;
    public float innerMassKg;
    public float totalMassKg;

    // Durability
    public float durability;

    // Scale / Fill
    public float scaleFactor;
    public float fillPercent;

    // Code
    public string moduleCode;

    // Wall Thickness
    public float wallThicknessMm;

    // Build Visual
    public float buildVisualYawOffset;
    public Vector3 buildAnchorLocal;
    public Vector2Int buildAnchorCellLocal;
    public Vector3 referenceVisualScale;

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

    // Meta
    public string craftTimestamp;
    public int dataVersion = 8;
}