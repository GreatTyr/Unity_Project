using System.Collections.Generic;
using UnityEngine;

namespace UnityProject.Inventory
{
    public class PlayerInventory : MonoBehaviour, IInventoryOwner
    {
        [Header("Equipment Slots")]
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
        [SerializeField] private int hotbarSize = 4;

        [SerializeField] private Inventory mainInventory;

        private EquipmentSlots equipment;
        private Hotbar hotbar;

        public Inventory MainInventory => mainInventory;
        public EquipmentSlots Equipment => equipment;
        public Hotbar Hotbar => hotbar;

        private void Awake()
        {
            if (mainInventory == null)
                mainInventory = new Inventory();

            equipment = new EquipmentSlots(equipmentSlotTypes);
            hotbar = new Hotbar(hotbarSize);
        }

        public int AddItem(ItemDefinition definition, int quantity = 1)
        {
            if (mainInventory == null) return 0;
            return mainInventory.AddItem(definition, quantity);
        }

        /// <summary>
        /// Экипировать предмет из инвентаря в указанный слот.
        /// Транзакционно: проверяет все условия ДО изменений.
        /// Если в слоте что-то есть — старый предмет возвращается в инвентарь.
        /// При неудаче на любом шаге — откат.
        /// </summary>
        public bool TryEquipItem(InventoryItem item, EquipmentSlotType targetSlot)
        {
            if (item == null || item.definition == null)
            {
                Debug.LogWarning("[PlayerInventory] TryEquipItem: item или definition == null");
                return false;
            }

            // ===== ФАЗА 1: ПРОВЕРКИ (ничего не меняем) =====

            if (!equipment.CanEquip(item, targetSlot))
                return false;

            // Проверяем, есть ли предмет в инвентаре
            if (!mainInventory.Contains(item.definition))
            {
                Debug.LogWarning($"[PlayerInventory] TryEquipItem: {item.definition.displayName} не найден в инвентаре");
                return false;
            }

            // Узнаём, что сейчас в целевом слоте (без изменений)
            var slot = equipment.GetSlot(targetSlot);
            bool slotOccupied = slot != null
                && slot.equippedItem != null
                && slot.equippedItem.definition != null;

            // ===== ФАЗА 2: ВЫПОЛНЕНИЕ С ОТКАТОМ =====

            // Шаг 1: убираем предмет из инвентаря
            bool removed = mainInventory.RemoveItem(item);
            if (!removed)
            {
                Debug.LogWarning($"[PlayerInventory] TryEquipItem: не удалось удалить {item.definition.displayName} из инвентаря");
                return false;
            }

            // Шаг 2: экипируем (получаем предыдущий предмет)
            if (!equipment.TryEquip(item, targetSlot, out InventoryItem previous))
            {
                // Откат шага 1: возвращаем предмет в инвентарь
                mainInventory.AddItem(item.definition, item.quantity);
                Debug.LogWarning($"[PlayerInventory] TryEquipItem: TryEquip провалился, откат");
                return false;
            }

            // Шаг 3: возвращаем предыдущий предмет в инвентарь
            if (previous != null && previous.definition != null)
            {
                int returned = mainInventory.AddItem(previous.definition, previous.quantity);

                // Если не удалось вернуть полностью — логируем, но не откатываем
                // (в List-based инвентаре без лимита это невозможно,
                //  но на будущее — защита)
                if (returned < previous.quantity)
                {
                    Debug.LogError($"[PlayerInventory] TryEquipItem: не удалось вернуть " +
                                   $"{previous.definition.displayName} полностью в инвентарь! " +
                                   $"Возвращено {returned}/{previous.quantity}");
                }
            }

            return true;
        }

        /// <summary>
        /// Снять предмет из слота экипировки и вернуть в инвентарь.
        /// </summary>
        public bool TryUnequipItem(EquipmentSlotType slotType)
        {
            // Проверяем, есть ли что снимать
            var slot = equipment.GetSlot(slotType);
            if (slot == null || slot.equippedItem == null || slot.equippedItem.definition == null)
                return false;

            if (!equipment.TryUnequip(slotType, out InventoryItem previous))
                return false;

            if (previous == null || previous.definition == null)
                return false;

            int added = mainInventory.AddItem(previous.definition, previous.quantity);

            if (added < previous.quantity)
            {
                // Откат: надеваем обратно
                equipment.TryEquip(previous, slotType, out _);
                Debug.LogWarning($"[PlayerInventory] TryUnequipItem: не удалось вернуть " +
                                 $"{previous.definition.displayName} в инвентарь, откат");
                return false;
            }

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