/// <summary>
/// Данные масштабирования, передаваемые от ModuleWorkbench к калькуляторам модулей.
/// </summary>
public struct ModuleScaleData
{
    public float scaleFactor;
    public float realVolume;          // м³, после масштаба
    public float shellVolumeM3;       // м³, объём оболочки
    public float effectiveVolume;     // м³, realVolume - shellVolume
    public float shellPercent;        // % от realVolume
    public float fillPercent;         // % из эталона
    public float shellMassKg;         // кг
    public float innerMassKg;         // кг
    public float totalMassKg;         // кг
    public float durability;          // прочность
    public int alloyTier;           // тир сплава оболочки
}