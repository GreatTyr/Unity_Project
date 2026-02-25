using UnityEngine;

namespace UnityProject.Inventory
{
    public class SquadInventorySource : IInventorySource
    {
        private Inventory inventory;
        public ResourcesStorage Resources => null;
        public AlloyStorage Alloys => null;

        public string DisplayName => "Отряд";
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