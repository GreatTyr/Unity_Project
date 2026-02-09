using System;
using System.Collections.Generic;
using UnityEngine;

namespace UnityProject.Inventory
{
    public interface IItemDefinitionResolver
    {
        ItemDefinition Resolve(string itemId);
    }

    [Serializable]
    public class InventoryItemData
    {
        public string itemId;
        public int quantity;
    }

    [Serializable]
    public class InventoryData
    {
        public List<InventoryItemData> items = new List<InventoryItemData>();
    }

    [Serializable]
    public class EquipmentSlotData
    {
        public EquipmentSlotType slotType;
        public InventoryItemData equippedItem;
    }

    [Serializable]
    public class PlayerInventorySaveData
    {
        public InventoryData mainInventory;
        public List<EquipmentSlotData> equipment = new List<EquipmentSlotData>();
        public List<InventoryItemData> hotbar = new List<InventoryItemData>();
    }

    public static class InventorySaveUtility
    {
        public static PlayerInventorySaveData Capture(PlayerInventory playerInventory)
        {
            if (playerInventory == null) return null;

            var data = new PlayerInventorySaveData
            {
                mainInventory = CaptureInventory(playerInventory.MainInventory)
            };

            if (playerInventory.Equipment?.Slots != null)
            {
                foreach (var slot in playerInventory.Equipment.Slots)
                {
                    data.equipment.Add(new EquipmentSlotData
                    {
                        slotType = slot.slotType,
                        equippedItem = CaptureItem(slot.equippedItem)
                    });
                }
            }

            if (playerInventory.Hotbar?.Slots != null)
            {
                foreach (var hotbarSlot in playerInventory.Hotbar.Slots)
                    data.hotbar.Add(CaptureItem(hotbarSlot.linkedItem));
            }

            return data;
        }

        private static InventoryData CaptureInventory(Inventory inventory)
        {
            if (inventory == null) return null;
            var data = new InventoryData();
            foreach (var item in inventory.Items)
            {
                var itemData = CaptureItem(item);
                if (itemData != null) data.items.Add(itemData);
            }
            return data;
        }

        private static InventoryItemData CaptureItem(InventoryItem item)
        {
            if (item?.definition == null) return null;
            return new InventoryItemData
            {
                itemId = item.definition.itemId,
                quantity = item.quantity
            };
        }
    }
}