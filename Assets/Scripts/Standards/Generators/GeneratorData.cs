using System;

/// <summary>
/// Данные изготовленного модуля генератора.
/// Наследует общие поля из ModuleData.
/// </summary>
[Serializable]
public class GeneratorData : ModuleData
{
    public float specificPower;              // energy/s
    public float fuelKgPerS;                // kg/s
    public int fuelTier;
    public float powerTimesTierPer0001;     // power*tier per 0.001 m³
    public float fuelPer0001m3Tiered;       // fuel*tier per 0.001 m³
    public float powerBy0001m3;             // base power per 0.001 m³ (from reference)
    public float fuelBy0001m3Base;          // base fuel per 0.001 m³ (from reference)
}