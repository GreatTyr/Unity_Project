using System;
using UnityEngine;

/// <summary>
/// ќбщий базовый класс дл€ итоговых данных модулей.
/// —одержит common thermal / craft / operational / static параметры,
/// а также заполн€ет общий слой ModuleData из CommonModuleCraftData.
/// </summary>
[Serializable]
public class CommonModuleData : ModuleData
{
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

    public virtual void InitializeCommon(CommonModuleCraftData commonData)
    {
        // Base ModuleData
        this.moduleType = commonData.moduleType;
        this.moduleTier = commonData.moduleTier;
        this.faction = commonData.faction;
        this.referenceIndex = commonData.referenceIndex;
        this.referenceName = commonData.referenceName;

        this.alloyCode = commonData.alloyCode;
        this.alloyTier = commonData.alloyTier;
        this.shellPercent = commonData.shellPercent;

        this.scaleFactor = commonData.scaleFactor;
        this.fillPercent = commonData.fillPercent;

        this.length = commonData.length;
        this.width = commonData.width;
        this.height = commonData.height;

        this.aabbVolume = commonData.aabbVolume;
        this.realVolume = commonData.realVolume;
        this.shellVolumeM3 = commonData.shellVolumeM3;
        this.effectiveVolume = commonData.effectiveVolume;

        this.shellMassKg = commonData.shellMassKg;
        this.innerMassKg = commonData.innerMassKg;
        this.totalMassKg = commonData.totalMassKg;
        this.durability = commonData.durability;
        this.wallThicknessMm = commonData.wallThicknessMm;

        this.moduleCode = commonData.moduleCode;

        this.canTurnOnOff = commonData.canTurnOnOff;
        this.turnOnOffTime = commonData.turnOnOffTime;
        this.canPulseMode = commonData.canPulseMode;
        this.pulseInterval = commonData.pulseInterval;
        this.isControllable = commonData.isControllable;

        this.isVolatile = commonData.isVolatile;
        this.explosionDamageType = commonData.explosionDamageType;
        this.explosionRadiusMeters = commonData.explosionRadiusMeters;
        this.explosionPenetration = commonData.explosionPenetration;
        this.explosionDamage = commonData.explosionDamage;

        this.buildVisualYawOffset = commonData.buildVisualYawOffset;
        this.buildAnchorLocal = commonData.buildAnchorLocal;
        this.buildAnchorCellLocal = commonData.buildAnchorCellLocal;
        this.referenceVisualScale = commonData.referenceVisualScale;

        this.craftTimestamp = DateTime.UtcNow.ToString("o");

        // CommonModuleData
        this.heatCapacity = commonData.heatCapacity;
        this.maxTemperature = commonData.maxTemperature;
        this.heatingRate = commonData.heatingRate;
        this.craftTimeSeconds = commonData.craftTimeSeconds;

        this.operationalResourceUsageSummary = commonData.operationalResourceUsageSummary ?? "Ч";
        this.staticCapacityMax = commonData.staticCapacityMax;
        this.staticCapacityCurrent = commonData.staticCapacityCurrent;
        this.staticCapacityDrainPerSecond = commonData.staticCapacityDrainPerSecond;
    }
}