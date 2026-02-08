using UnityEngine;

namespace UnityProject.Inventory
{
    /// <summary>
    /// Источник инвентаря для объектов (сундуки, контейнеры и т.п.).
    /// Хранит ссылку на текущий открытый контейнер.
    /// Когда игрок открывает инвентарь из интерактива "сундук",
    /// в InventoryUIManager выставляется этот источник.
    /// </summary>
    public class ObjectInventorySource : IInventorySource
    {
        private InventoryGrid containerGrid;
        private string objectDisplayName;

        public string DisplayName => string.IsNullOrEmpty(objectDisplayName) ? "Объект" : objectDisplayName;
        public InventoryGrid MainInventory => containerGrid;
        public bool IsAvailable => containerGrid != null;

        /// <summary>
        /// Установить текущий открытый контейнер.
        /// </summary>
        public void SetContainer(InventoryGrid grid, string displayName = "Объект")
        {
            containerGrid = grid;
            objectDisplayName = displayName;
        }

        /// <summary>
        /// Очистить ссылку на контейнер (когда объект закрыт).
        /// </summary>
        public void ClearContainer()
        {
            containerGrid = null;
            objectDisplayName = null;
        }
    }
}
