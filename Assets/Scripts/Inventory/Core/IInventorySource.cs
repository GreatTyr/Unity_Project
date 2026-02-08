namespace UnityProject.Inventory
{
    /// <summary>
    /// Интерфейс источника инвентаря для вкладок в UI (Player, Pepelac, Отряд, База, Объект).
    /// Позволяет левой и правой панелям работать с любым типом инвентаря единообразно.
    /// </summary>
    public interface IInventorySource
    {
        /// <summary>
        /// Отображаемое имя источника (для вкладки).
        /// </summary>
        string DisplayName { get; }

        /// <summary>
        /// Основная сетка инвентаря, из которой берутся предметы для списка
        /// и куда кладутся предметы при переносе.
        /// </summary>
        InventoryGrid MainInventory { get; }

        /// <summary>
        /// Доступен ли этот источник для отображения (для заглушек можно вернуть false).
        /// </summary>
        bool IsAvailable { get; }
    }
}
