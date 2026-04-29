using System;

[Serializable]
public class ArmorPlateData : ModuleCommonData
{
    // Прочность (скрывает базовое поле с new)
    public new float durability;

    // Финальные поглощения (с учетом бонусов эталона и сплава)
    public int kineticAbsorption;
    public int thermalAbsorption;
    public int chemicalAbsorption;
    public int energyAbsorption;

    // Финальные сопротивления (с учетом бонусов эталона и сплава)
    public float kineticResistance;
    public float thermalResistance;
    public float chemicalResistance;
    public float energyResistance;

    // Толщина стенок (скрывает базовое поле с new)
    public new float wallThicknessMm;

}