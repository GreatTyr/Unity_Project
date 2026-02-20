using System;
using UnityEngine;

/// <summary>
/// Базовые данные изготовленного модуля.
/// Наследники добавляют специфичные для типа поля.
/// Все данные — readonly после крафта.
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
    public int dataVersion = 1;        // для миграции данных

    /// <summary>Заполнить общие поля из данных верстака.</summary>
    public void FillCommon(
        string moduleType, int moduleTier, string faction,
        int referenceIndex, string referenceName,
        string alloyCode, int alloyTier,
        float shellPercent, float scaleFactor, float fillPercent,
        float length, float width, float height,
        float aabbVolume, float realVolume, float shellVolumeM3, float effectiveVolume,
        float shellMassKg, float innerMassKg, float totalMassKg,
        float durability, string moduleCode)
    {
        this.moduleType = moduleType;
        this.moduleTier = moduleTier;
        this.faction = faction;
        this.referenceIndex = referenceIndex;
        this.referenceName = referenceName;
        this.alloyCode = alloyCode;
        this.alloyTier = alloyTier;
        this.shellPercent = shellPercent;
        this.scaleFactor = scaleFactor;
        this.fillPercent = fillPercent;
        this.length = length;
        this.width = width;
        this.height = height;
        this.aabbVolume = aabbVolume;
        this.realVolume = realVolume;
        this.shellVolumeM3 = shellVolumeM3;
        this.effectiveVolume = effectiveVolume;
        this.shellMassKg = shellMassKg;
        this.innerMassKg = innerMassKg;
        this.totalMassKg = totalMassKg;
        this.durability = durability;
        this.moduleCode = moduleCode;
        this.craftTimestamp = DateTime.UtcNow.ToString("o");
        this.dataVersion = 1;
    }
}