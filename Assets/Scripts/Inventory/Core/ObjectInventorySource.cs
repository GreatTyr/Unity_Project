using UnityEngine;

namespace UnityProject.Inventory
{
    public class ObjectInventorySource : IInventorySource
    {
        private Inventory containerInventory;
        private string objectDisplayName;

        public string DisplayName =>
            string.IsNullOrEmpty(objectDisplayName) ? "Объект" : objectDisplayName;
        public Inventory MainInventory => containerInventory;
        public bool IsAvailable => containerInventory != null;

        public void SetContainer(Inventory inv, string displayName = "Объект")
        {
            containerInventory = inv;
            objectDisplayName = displayName;
        }

        public void ClearContainer()
        {
            containerInventory = null;
            objectDisplayName = null;
        }
    }
}