using UnityEngine;

namespace UnityProject.Inventory
{
    /// <summary>
    /// Заглушка для инвентаря транспорта Pepelac.
    /// Пока возвращает пустую сетку, в будущем будет подключена к реальному PepelacInventory.
    /// </summary>
    public class PepelacInventorySource : IInventorySource
    {
        private InventoryGrid emptyGrid;

        public string DisplayName => "Pepelac";
        public InventoryGrid MainInventory
        {
            get
            {
                if (emptyGrid == null)
                    emptyGrid = new InventoryGrid(8, 6); // Временная заглушка
                return emptyGrid;
            }
        }
        public bool IsAvailable => true; // Пока всегда доступен, даже если пустой
    }
}
