using System;

[Serializable]
public class ArmorPlateData : CommonModuleData
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

    public void Initialize(
        CommonModuleCraftData commonData,
        float durability,
        int kineticAbsorption,
        int thermalAbsorption,
        int chemicalAbsorption,
        int energyAbsorption,
        float kineticResistance,
        float thermalResistance,
        float chemicalResistance,
        float energyResistance,
        float wallThicknessMm)
    {
        InitializeCommon(commonData);

        this.durability = durability;

        this.kineticAbsorption = kineticAbsorption;
        this.thermalAbsorption = thermalAbsorption;
        this.chemicalAbsorption = chemicalAbsorption;
        this.energyAbsorption = energyAbsorption;

        this.kineticResistance = kineticResistance;
        this.thermalResistance = thermalResistance;
        this.chemicalResistance = chemicalResistance;
        this.energyResistance = energyResistance;

        this.wallThicknessMm = wallThicknessMm;
    }
}