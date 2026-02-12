using System.Collections.Generic;
using UnityEngine;

namespace UnityProject.Inventory
{
    /// <summary>
    /// Главный компонент инвентаря игрока.
    /// Содержит: основной инвентарь (List-based), экипировку, хотбар.
    /// </summary>
    public class PlayerInventory : MonoBehaviour, IInventoryOwner
    {
        [Header("Equipment Slots")]
        [Tooltip("Список типов слотов экипировки, которые есть у игрока.")]
        [SerializeField]
        private List<EquipmentSlotType> equipmentSlotTypes = new List<EquipmentSlotType>
        {
            EquipmentSlotType.Head,
            EquipmentSlotType.Body,
            EquipmentSlotType.Legs,
            EquipmentSlotType.WeaponMain,
            EquipmentSlotType.WeaponSecondary,
            EquipmentSlotType.Backpack
        };

        [Header("Hotbar")]
        [Tooltip("Количество быстрых слотов (1..N).")]
        [SerializeField] private int hotbarSize = 4;

        [SerializeField] private Inventory mainInventory;

        // НЕ сериализуем — всегда создаём в Awake из актуального списка
        private EquipmentSlots equipment;
        private Hotbar hotbar;

        public Inventory MainInventory => mainInventory;
        public EquipmentSlots Equipment => equipment;
        public Hotbar Hotbar => hotbar;

        private void Awake()
        {
            if (mainInventory == null)
                mainInventory = new Inventory();

            // Всегда создаём из актуального списка — без проверки на null
            equipment = new EquipmentSlots(equipmentSlotTypes);
            hotbar = new Hotbar(hotbarSize);
        }

        /// <summary>
        /// Добавить предмет в основной инвентарь.
        /// </summary>
        public int AddItem(ItemDefinition definition, int quantity = 1)
        {
            if (mainInventory == null) return 0;
            return mainInventory.AddItem(definition, quantity);
        }

        /// <summary>
        /// Экипировать предмет из инвентаря в указанный слот.
        /// Если в слоте уже что-то есть — старый предмет возвращается в инвентарь.
        /// </summary>
        public bool TryEquipItem(InventoryItem item, EquipmentSlotType targetSlot)
        {
            if (item == null || item.definition == null)
            {
                Debug.LogWarning("[PlayerInventory] TryEquipItem: item или definition == null");
                return false;
            }

            if (!equipment.CanEquip(item, targetSlot))
                return false;

            if (!equipment.TryEquip(item, targetSlot, out InventoryItem previous))
                return false;

            mainInventory.RemoveItem(item);

            if (previous != null && previous.definition != null)
                mainInventory.AddItem(previous.definition, previous.quantity);

            return true;
        }

        /// <summary>
        /// Снять предмет из слота экипировки и вернуть в инвентарь.
        /// </summary>
        public bool TryUnequipItem(EquipmentSlotType slotType)
        {
            if (!equipment.TryUnequip(slotType, out InventoryItem previous))
                return false;

            if (previous == null || previous.definition == null)
                return false;

            mainInventory.AddItem(previous.definition, previous.quantity);
            return true;
        }

        public void AssignToHotbar(int index, InventoryItem item)
        {
            hotbar?.Assign(index, item);
        }

        public InventoryItem GetHotbarItem(int index)
        {
            return hotbar?.GetItem(index);
        }

        /// <summary>
        /// Общий вес: инвентарь + экипировка.
        /// </summary>
        public float CalculateTotalWeight()
        {
            float total = 0f;

            if (mainInventory != null)
                total += mainInventory.CalculateTotalWeight();

            if (equipment?.Slots != null)
            {
                foreach (var slot in equipment.Slots)
                {
                    if (slot.equippedItem?.definition == null) continue;
                    total += slot.equippedItem.definition.weight
                           * Mathf.Max(1, slot.equippedItem.quantity);
                }
            }

            return total;
        }
    }
}