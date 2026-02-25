using UnityEngine;

/// <summary>
/// Интерфейс калькулятора модуля для ModuleWorkbench.
/// </summary>
public interface IModuleCalculator
{
    string ModuleType { get; }
    int ReferenceCount { get; }
    string[] GetReferenceNames();
    void SelectReference(int index);
    int SelectedIndex { get; }

    float RefLength { get; }
    float RefWidth { get; }
    float RefHeight { get; }
    float RefRealVolume { get; }
    float RefFillPercent { get; }
    float RefVolumeCoefficientPercent { get; }
    int RefModuleTier { get; }
    string RefFaction { get; }

    void Calculate(ModuleScaleData data);
    void DrawResultsGUI();
    string GetCodeSegment();
    GameObject GetPrefab();

    

    /// <summary>
    /// Создать ModuleData со всеми специфичными для типа полями.
    /// Вызывается верстаком при крафте. Общие поля заполняет верстак через FillCommon().
    /// </summary>
    ModuleData CreateModuleData(ModuleScaleData scaleData);
}