using UnityEngine;

namespace UnityProject.Inventory
{
    public class ObjectInventorySource : IInventorySource
    {
        private Inventory containerInventory;
        private string objectDisplayName;

        private ResourcesStorage containerResources;
        private AlloyStorage containerAlloys;

        public ResourcesStorage Resources => containerResources;
        public AlloyStorage Alloys => containerAlloys;

        public void SetContainer(Inventory inv, string displayName = "Объект",
        ResourcesStorage resources = null, AlloyStorage alloys = null)
        {
            containerInventory = inv;
            objectDisplayName = displayName;
            containerResources = resources;
            containerAlloys = alloys;
        }

        public string DisplayName =>
            string.IsNullOrEmpty(objectDisplayName) ? "Объект" : objectDisplayName;
        public Inventory MainInventory => containerInventory;
        public bool IsAvailable => containerInventory != null;



        public void ClearContainer()
        {
            containerInventory = null;
            objectDisplayName = null;
            containerResources = null;
            containerAlloys = null;
        }
    }
}