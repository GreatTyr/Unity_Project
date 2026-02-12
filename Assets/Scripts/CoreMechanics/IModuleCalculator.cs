using UnityEngine;

/// <summary>
/// Интерфейс калькулятора модуля для ModuleWorkbench.
/// Каждый тип модуля реализует свой калькулятор.
/// </summary>
public interface IModuleCalculator
{
    /// <summary>Тип модуля (константа из ModuleTypesDatabase).</summary>
    string ModuleType { get; }

    /// <summary>Количество эталонов в базе данных этого типа.</summary>
    int ReferenceCount { get; }

    /// <summary>Имена эталонов для отображения в popup.</summary>
    string[] GetReferenceNames();

    /// <summary>Выбрать эталон по индексу.</summary>
    void SelectReference(int index);

    /// <summary>Индекс текущего выбранного эталона.</summary>
    int SelectedIndex { get; }

    // --- Данные эталона для масштабирования (Workbench читает) ---

    float RefLength { get; }
    float RefWidth { get; }
    float RefHeight { get; }
    float RefRealVolume { get; }
    float RefFillPercent { get; }
    float RefVolumeCoefficientPercent { get; }
    int RefModuleTier { get; }
    string RefFaction { get; }

    /// <summary>Рассчитать характеристики модуля по данным масштабирования.</summary>
    void Calculate(ModuleScaleData data);

    /// <summary>Нарисовать секцию результатов в IMGUI (характеристики, специфичные для этого типа).</summary>
    void DrawResultsGUI();

    /// <summary>Часть кода модуля, специфичная для этого типа (для вставки в общий код).</summary>
    string GetCodeSegment();

    /// <summary>Префаб выбранного эталона для инстанцирования.</summary>
    GameObject GetPrefab();
}