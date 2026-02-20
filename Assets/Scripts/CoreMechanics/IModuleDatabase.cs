/// <summary>
/// Интерфейс базы данных модулей.
/// Каждая конкретная БД (EnergyStorageDatabase, GeneratorDatabase и т.д.)
/// реализует этот интерфейс, чтобы ModuleWorkbench мог автоматически
/// получать калькуляторы для любого типа модулей.
/// </summary>
public interface IModuleDatabase
{
    /// <summary>Тип модуля (константа из ModuleTypesDatabase).</summary>
    string ModuleType { get; }

    /// <summary>Количество эталонов в базе.</summary>
    int Count { get; }

    /// <summary>Создать калькулятор для этого типа модулей.</summary>
    IModuleCalculator CreateCalculator();
}