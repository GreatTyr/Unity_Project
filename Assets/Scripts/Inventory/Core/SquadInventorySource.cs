using UnityEngine;

namespace UnityProject.Inventory
{
    /// <summary>
    /// Заглушка для инвентаря отряда.
    /// В будущем будет подключена к системе отряда.
    /// </summary>
    public class SquadInventorySource : IInventorySource
    {
        private InventoryGrid emptyGrid;

        public string DisplayName => "Отряд";
        public InventoryGrid MainInventory
        {
            get
            {
                if (emptyGrid == null)
                    emptyGrid = new InventoryGrid(10, 8); // Временная заглушка
                return emptyGrid;
            }
        }
        public bool IsAvailable => false; // Пока не реализовано, скрываем вкладку
    }
}
