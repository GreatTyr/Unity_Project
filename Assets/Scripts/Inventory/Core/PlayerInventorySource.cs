using UnityEngine;

namespace UnityProject.Inventory
{
    /// <summary>
    /// Адаптер для PlayerInventory, реализующий IInventorySource.
    /// Используется для отображения инвентаря игрока во вкладках.
    /// </summary>
    public class PlayerInventorySource : IInventorySource
    {
        private readonly PlayerInventory playerInventory;

        public string DisplayName => "Игрок";
        public InventoryGrid MainInventory => playerInventory?.MainInventory;
        public bool IsAvailable => playerInventory != null;

        public PlayerInventorySource(PlayerInventory inventory)
        {
            playerInventory = inventory;
        }
    }
}
