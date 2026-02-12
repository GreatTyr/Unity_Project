using UnityEngine;

namespace UnityProject.Inventory
{
    public class BaseInventorySource : IInventorySource
    {
        private Inventory inventory;

        public string DisplayName => "База";
        public Inventory MainInventory
        {
            get
            {
                if (inventory == null)
                    inventory = new Inventory();
                return inventory;
            }
        }
        public bool IsAvailable => false;
    }
}