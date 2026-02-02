using System;
using System.Collections.Generic;
using UnityEngine;

namespace UnityProject.Inventory
{
    [Serializable]
    public class EquipmentSlot
    {
        public EquipmentSlotType slotType;
        public InventoryItem equippedItem;
    }

    [Serializable]
    public class EquipmentSlots
    {
        [SerializeField]
        private List<EquipmentSlot> slots = new List<EquipmentSlot>();

        public IReadOnlyList<EquipmentSlot> Slots => slots;

        public EquipmentSlots(IEnumerable<EquipmentSlotType> slotTypes)
        {
            slots = new List<EquipmentSlot>();
            foreach (var t in slotTypes)
            {
                slots.Add(new EquipmentSlot { slotType = t, equippedItem = null });
            }
        }

        public EquipmentSlot GetSlot(EquipmentSlotType type)
        {
            return slots.Find(s => s.slotType == type);
        }

        public bool CanEquip(InventoryItem item, EquipmentSlotType targetSlot)
        {
            if (item == null || item.definition == null)
            {
                Debug.LogWarning("[EquipmentSlots] CanEquip: item/definition == null");
                return false;
            }
            if (!item.definition.isEquippable)
            {
                Debug.LogWarning($"[EquipmentSlots] CanEquip: {item.definition.displayName} не помечен как isEquippable");
                return false;
            }
            if (item.definition.equipmentSlotType != targetSlot)
            {
                Debug.LogWarning($"[EquipmentSlots] CanEquip: equipmentSlotType={item.definition.equipmentSlotType}, " +
                                 $"targetSlot={targetSlot} не совпадает");
                return false;
            }

            var slot = GetSlot(targetSlot);
            if (slot == null)
            {
                Debug.LogWarning($"[EquipmentSlots] CanEquip: слот {targetSlot} не найден в списке equipmentSlotTypes");
                return false;
            }

            return true;
        }

        public bool TryEquip(InventoryItem item, EquipmentSlotType targetSlot, out InventoryItem previous)
        {
            previous = null;
            if (!CanEquip(item, targetSlot)) return false;

            var slot = GetSlot(targetSlot);
            if (slot == null) return false;

            // если в слоте лежит "пустой" предмет без definition — считаем, что его нет
            if (slot.equippedItem != null && slot.equippedItem.definition != null)
                previous = slot.equippedItem;
            else
                previous = null;

            slot.equippedItem = item;
            return true;
        }

        public bool TryUnequip(EquipmentSlotType slotType, out InventoryItem previous)
        {
            previous = null;
            var slot = GetSlot(slotType);
            if (slot == null) return false;

            // слот считается пустым, если нет валидного предмета
            if (slot.equippedItem == null || slot.equippedItem.definition == null)
                return false;

            previous = slot.equippedItem;
            slot.equippedItem = null;
            return true;
        }
    }
}