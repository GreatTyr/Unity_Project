using System;
using UnityEngine;

[Serializable]
public class ModuleCommonData
{
    // Из ModuleData
    public string moduleType;
    public int moduleTier;
    public string faction;
    public int referenceIndex;
    public string referenceName;
    public string alloyCode;
    public int alloyTier;
    public float shellPercent;
    public float length;
    public float width;
    public float height;
    public float aabbVolume;
    public float realVolume;
    public float shellVolumeM3;
    public float effectiveVolume;
    public float shellMassKg;
    public float innerMassKg;
    public float totalMassKg;
    public float durability; // Max HP
    public float scaleFactor;
    public float fillPercent;
    public string moduleCode;
    public float wallThicknessMm;
    public float buildVisualYawOffset;
    public Vector3 buildAnchorLocal;
    public Vector2Int buildAnchorCellLocal;
    public Vector3 referenceVisualScale;
    public bool canTurnOnOff;
    public float turnOnOffTime;
    public bool canPulseMode;
    public float pulseInterval;
    public bool isControllable;
    public bool isVolatile;
    public DamageType explosionDamageType;
    public float explosionRadiusMeters;
    public float explosionPenetration;
    public float explosionDamage;
    public string craftTimestamp;
    public int dataVersion = 10;

    // Из CommonModuleData
    public float heatCapacity;
    public float maxTemperature; // Max Temp
    public float heatingRate;
    public float craftTimeSeconds;
    public string operationalResourceUsageSummary;
    public float staticCapacityMax; // Max Static
    public float staticCapacityCurrent;
    public float staticCapacityDrainPerSecond;

    // Метод переноса данных из Scaler (чтобы не писать 20 строк в каждом контроллере)
    public void SetBaseStats(ModuleScaler scaler, StandardModuleBase std, string code, string alloy)
    {
        this.moduleType = std.ModuleType;
        this.moduleTier = std.ModuleTier;
        this.faction = std.FactionShortName;
        this.referenceName = std.gameObject.name;
        this.alloyCode = alloy;
        this.moduleCode = code;

        this.length = scaler.CalcLength;
        this.width = scaler.CalcWidth;
        this.height = scaler.CalcHeight;
        this.totalMassKg = scaler.CalcTotalMass;
        this.durability = scaler.CalcDurability;
        this.scaleFactor = scaler.CurrentScaleFactor;
        this.shellPercent = scaler.CurrentShellPercent;
        this.wallThicknessMm = scaler.CalcWallThicknessMm;

        this.canTurnOnOff = std.CanTurnOnOff;
        this.isControllable = std.IsControllable;
        this.canPulseMode = std.CanPulseMode;

        this.isVolatile = std.IsVolatile;
        this.explosionDamageType = std.ExplosionDamageType;

        this.craftTimestamp = DateTime.UtcNow.ToString("o");
    }
    // Специальная версия для Бронеплит
    public void SetBaseStatsArmor(ArmorPlateScaler scaler, StandardArmorPlate std, string code, string alloy)
    {
        this.moduleType = std.ModuleType;
        this.moduleTier = std.ModuleTier;
        this.faction = std.FactionShortName;
        this.referenceName = std.gameObject.name;
        this.alloyCode = alloy;
        this.moduleCode = code;

        this.length = scaler.CalcLength;
        this.width = scaler.CalcWidth;
        this.height = scaler.CalcHeight;
        this.totalMassKg = scaler.CalcMass;
        this.durability = scaler.CalcDurability;
        this.scaleFactor = (scaler.ScaleX + scaler.ScaleY + scaler.ScaleZ) / 3f;
        this.shellPercent = 100f; // У бронеплиты всегда 100% оболочка
        this.wallThicknessMm = scaler.CalcWallThicknessMm;

        this.canTurnOnOff = false;
        this.isControllable = false;

        this.craftTimestamp = DateTime.UtcNow.ToString("o");
    }
}