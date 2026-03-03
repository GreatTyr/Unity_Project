using System;
using UnityEngine;

/// <summary>
/// Объект для безопасной передачи всех параметров крафта из Верстака в Данные.
/// Избавляет от метода с 22 параметрами.
/// </summary>
public struct ModuleCraftDTO
{
    // Identity
    public string moduleType;
    public int moduleTier;
    public string faction;
    public int referenceIndex;
    public string referenceName;

    // Alloy & Shell
    public string alloyCode;
    public int alloyTier;
    public float shellPercent;

    // Scaler & Fill
    public float scaleFactor;
    public float fillPercent;

    // Dimensions
    public float length;
    public float width;
    public float height;

    // Volumes
    public float aabbVolume;
    public float realVolume;
    public float shellVolumeM3;
    public float effectiveVolume;

    // Mass & Durability
    public float shellMassKg;
    public float innerMassKg;
    public float totalMassKg;
    public float durability;

    // String code
    public string moduleCode;

    // НОВЫЕ ПАРАМЕТРЫ УПРАВЛЕНИЯ
    public bool canTurnOnOff;
    public float turnOnOffTime;
    public bool canPulseMode;
    public float pulseInterval;
    public bool isControllable;
}

/// <summary>
/// Базовые данные изготовленного модуля.
/// Наследники добавляют специфичные для типа поля.
/// Все данные — readonly после крафта (кроме случаев сохранения/загрузки).
/// </summary>
[Serializable]
public class ModuleData
{
    // ── Identity ──
    public string moduleType;          // "EnergyStorage", "Generator", etc.
    public int moduleTier;
    public string faction;             // short name or "NONE"
    public int referenceIndex;         // индекс эталона в БД
    public string referenceName;       // имя префаба эталона

    // ── Alloy ──
    public string alloyCode;           // код сплава оболочки
    public int alloyTier;

    // ── Shell ──
    public float shellPercent;         // % объёма оболочки

    // ── Dimensions (метры) ──
    public float length;               // X
    public float width;                // Z
    public float height;               // Y

    // ── Volumes (м³) ──
    public float aabbVolume;
    public float realVolume;
    public float shellVolumeM3;
    public float effectiveVolume;

    // ── Mass (кг) ──
    public float shellMassKg;
    public float innerMassKg;
    public float totalMassKg;

    // ── Durability ──
    public float durability;

    // ── Scale ──
    public float scaleFactor;

    // ── Fill ──
    public float fillPercent;

    // ── Code (строковое представление для UI/обмена) ──
    public string moduleCode;

    // ── Meta ──
    public string craftTimestamp;       // ISO 8601 время крафта
    public int dataVersion = 2;         // для миграции данных
                                        // НОВЫЕ ПАРАМЕТРЫ УПРАВЛЕНИЯ
    public bool canTurnOnOff;
    public float turnOnOffTime;
    public bool canPulseMode;
    public float pulseInterval;
    public bool isControllable;


    /// <summary>
    /// Заполнить общие поля из структурированного DTO (безопасный способ).
    /// </summary>
    public virtual void Initialize(ModuleCraftDTO dto)
    {
        this.moduleType = dto.moduleType;
        this.moduleTier = dto.moduleTier;
        this.faction = dto.faction;
        this.referenceIndex = dto.referenceIndex;
        this.referenceName = dto.referenceName;

        this.alloyCode = dto.alloyCode;
        this.alloyTier = dto.alloyTier;
        this.shellPercent = dto.shellPercent;

        this.scaleFactor = dto.scaleFactor;
        this.fillPercent = dto.fillPercent;

        this.length = dto.length;
        this.width = dto.width;
        this.height = dto.height;

        this.aabbVolume = dto.aabbVolume;
        this.realVolume = dto.realVolume;
        this.shellVolumeM3 = dto.shellVolumeM3;
        this.effectiveVolume = dto.effectiveVolume;

        this.shellMassKg = dto.shellMassKg;
        this.innerMassKg = dto.innerMassKg;
        this.totalMassKg = dto.totalMassKg;
        this.durability = dto.durability;

        this.moduleCode = dto.moduleCode;

        this.canTurnOnOff = dto.canTurnOnOff;
        this.turnOnOffTime = dto.turnOnOffTime;
        this.canPulseMode = dto.canPulseMode;
        this.pulseInterval = dto.pulseInterval;
        this.isControllable = dto.isControllable;

        this.craftTimestamp = DateTime.UtcNow.ToString("o");
        this.dataVersion = 2;
    }
}