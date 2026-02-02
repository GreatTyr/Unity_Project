using System.Collections.Generic;
using UnityEngine;

namespace UnityProject.Inventory
{
    /// <summary>
    /// PlayerInventory — владелец инвентаря игрока.
    /// </summary>
    public class PlayerInventory : MonoBehaviour, IInventoryOwner
    {
        [Header("Main Inventory (Grid)")]
        [Tooltip("Ширина основного рюкзака (в клетках).")]
        [SerializeField] private int mainWidth = 8;
        [Tooltip("Высота основного рюкзака (в клетках).")]
        [SerializeField] private int mainHeight = 6;

        [Header("Equipment Slots")]
        [Tooltip("Список типов слотов экипировки, которые есть у игрока.")]
        [SerializeField]
        private List<EquipmentSlotType> equipmentSlotTypes =
            new List<EquipmentSlotType>
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

        [SerializeField] private InventoryGrid mainInventory;
        [SerializeField, HideInInspector] private EquipmentSlots equipment;
        [SerializeField] private Hotbar hotbar;

        public InventoryGrid MainInventory => mainInventory;
        public EquipmentSlots Equipment => equipment;
        public Hotbar Hotbar => hotbar;

        private void Awake()
        {
            if (mainInventory == null)
                mainInventory = new InventoryGrid(mainWidth, mainHeight);

            if (equipment == null)
                equipment = new EquipmentSlots(equipmentSlotTypes);

            if (hotbar == null)
                hotbar = new Hotbar(hotbarSize);
        }

        /// <summary>
        /// Попробовать добавить предмет игроку.
        /// 1) если предмет стакаемый — сначала попытаться влить его в существующие стеки;
        /// 2) остаток — положить как отдельные экземпляры в сетку.
        /// Вернёт фактически добавленное количество (по quantity).
        /// </summary>
        public int AddItem(ItemDefinition def, int quantity)
        {
            if (def == null || quantity <= 0) return 0;

            int remaining = quantity;

            if (def.stackable)
            {
                foreach (var item in mainInventory.Items)
                {
                    if (remaining <= 0) break;
                    if (item.definition != def) continue;
                    if (item.quantity >= def.maxStack) continue;

                    int space = def.maxStack - item.quantity;
                    int toAdd = Mathf.Min(space, remaining);

                    item.quantity += toAdd;
                    remaining -= toAdd;
                }
            }

            while (remaining > 0)
            {
                int stackAmount = def.stackable
                    ? Mathf.Min(def.maxStack, remaining)
                    : 1;

                var newItem = new InventoryItem(def, stackAmount);
                bool placed = mainInventory.TryAddItemToFirstAvailable(newItem);

                if (!placed)
                    break;

                remaining -= stackAmount;
            }

            int added = quantity - remaining;
            return added;
        }

        /// <summary>
        /// Попробовать надеть предмет из инвентаря в заданный слот экипировки.
        /// Если в слоте был предмет — он возвращается в инвентарь (если есть место).
        /// </summary>
        public bool TryEquipItem(InventoryItem item, EquipmentSlotType targetSlot)
        {
            if (item == null || item.definition == null)
            {
                Debug.LogWarning("[PlayerInventory] TryEquipItem: item или definition == null");
                return false;
            }

            Debug.Log($"[PlayerInventory] TryEquipItem: item={item.definition.displayName}, " +
                      $"targetSlot={targetSlot}, isEquippable={item.definition.isEquippable}, " +
                      $"itemSlot={item.definition.equipmentSlotType}");

            if (!equipment.CanEquip(item, targetSlot))
            {
                Debug.LogWarning("[PlayerInventory] CanEquip вернул false");
                return false;
            }

            if (!equipment.TryEquip(item, targetSlot, out InventoryItem previous))
            {
                Debug.LogWarning("[PlayerInventory] EquipmentSlots.TryEquip вернул false");
                return false;
            }

            // Убираем надетый предмет из сетки (он теперь "в слоте")
            mainInventory.RemoveItem(item);

            // Если что-то было надето до этого — пытаемся вернуть в сетку
            if (previous != null && previous.definition != null)
            {
                Debug.Log($"[PlayerInventory] В слоте {targetSlot} уже был {previous.definition.displayName}, " +
                          "пытаемся вернуть в инвентарь");

                bool placed = mainInventory.TryAddItemToFirstAvailable(previous);
                if (!placed)
                {
                    Debug.LogWarning($"[PlayerInventory] Нет места, чтобы вернуть {previous.definition.displayName} в инвентарь.");
                }
            }
            else
            {
                Debug.Log($"[PlayerInventory] Слот {targetSlot} был пуст, previous == null или без definition");
            }

            return true;
        }

        /// <summary>
        /// Снять предмет из указанного слота экипировки и положить обратно в инвентарь.
        /// </summary>
        public bool TryUnequipItem(EquipmentSlotType slotType)
        {
            if (!equipment.TryUnequip(slotType, out InventoryItem previous))
                return false;

            if (previous == null || previous.definition == null) return false;

            bool placed = mainInventory.TryAddItemToFirstAvailable(previous);
            if (!placed)
            {
                Debug.LogWarning("[PlayerInventory] Нет места для предмета после снятия экипировки.");
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

            if (equipment != null && equipment.Slots != null)
            {
                foreach (var slot in equipment.Slots)
                {
                    if (slot.equippedItem == null || slot.equippedItem.definition == null)
                        continue;

                    float w = slot.equippedItem.definition.weight *
                              Mathf.Max(1, slot.equippedItem.quantity);
                    total += w;
                }
            }

            return total;
        }
    }
}