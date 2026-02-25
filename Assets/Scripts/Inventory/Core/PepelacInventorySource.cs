using UnityEngine;

namespace UnityProject.Inventory
{
    public class PepelacInventorySource : IInventorySource
    {
        private Inventory inventory;

        public ResourcesStorage Resources => null;  // TODO: подключить к реальному Pepelac
        public AlloyStorage Alloys => null;

        public string DisplayName => "Pepelac";
        public Inventory MainInventory
        {
            get
            {
                if (inventory == null)
                    inventory = new Inventory();
                return inventory;
            }
        }
        public bool IsAvailable => true;
    }
}