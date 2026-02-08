using UnityEngine;

namespace UnityProject.Inventory
{
    /// <summary>
    /// Заглушка для инвентаря базы.
    /// В будущем будет подключена к системе базы.
    /// </summary>
    public class BaseInventorySource : IInventorySource
    {
        private InventoryGrid emptyGrid;

        public string DisplayName => "База";
        public InventoryGrid MainInventory
        {
            get
            {
                if (emptyGrid == null)
                    emptyGrid = new InventoryGrid(12, 10); // Временная заглушка
                return emptyGrid;
            }
        }
        public bool IsAvailable => false; // Пока не реализовано, скрываем вкладку
    }
}
