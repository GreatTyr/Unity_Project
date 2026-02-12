using UnityEngine;

namespace UnityProject.Inventory
{
    public class PlayerInventorySource : IInventorySource
    {
        private readonly PlayerInventory playerInventory;

        public string DisplayName => "Игрок";
        public Inventory MainInventory => playerInventory?.MainInventory;
        public bool IsAvailable => playerInventory != null;

        public PlayerInventorySource(PlayerInventory inventory)
        {
            playerInventory = inventory;
        }
    }
}