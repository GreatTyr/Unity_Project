using System.Collections.Generic;
using UnityEngine;

namespace UnityProject.Inventory
{
    /// <summary>
    /// PlayerInventory � �������� ��������� ������.
    /// </summary>
    public class PlayerInventory : MonoBehaviour, IInventoryOwner
    {
        [Header("Main Inventory (Grid)")]
        [Tooltip("������ ��������� ������� (� �������).")]
        [SerializeField] private int mainWidth = 8;
        [Tooltip("������ ��������� ������� (� �������).")]
        [SerializeField] private int mainHeight = 6;

        [Header("Equipment Slots")]
        [Tooltip("������ ����� ������ ����������, ������� ���� � ������.")]
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
        [Tooltip("���������� ������� ������ (1..N).")]
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

        #region High-level operations API

        /// <summary>
        /// ?????????? ??????????? ???????? ?????? ???????? ????? ?????????.
        /// ????????????, ????????, ??? drag&amp;drop ? UI.
        /// </summary>
        public InventoryOperationResult TryMoveItemInMainGrid(
            InventoryItem item,
            int targetX,
            int targetY,
            bool rotated)
        {
            if (mainInventory == null)
            {
                return InventoryOperationResult.Fail(
                    InventoryOperationError.InvalidTarget,
                    "[PlayerInventory] TryMoveItemInMainGrid: mainInventory == null");
            }

            return mainInventory.TryMoveItemSafely(item, targetX, targetY, rotated);
        }

        /// <summary>
        /// ?????????? ???????? ??????? ?? ???????? ????? ? ????????? ?????????-???????.
        /// ?? ?????? ????? ????????? ?????????? ?????????? ???? ? ?????,
        /// ????? ???????? ??????? ??????????? ??????.
        /// </summary>
        public InventoryOperationResult TryPlaceIntoContainer(
            InventoryItem itemToPlace,
            InventoryItem containerItem)
        {
            if (itemToPlace == null || containerItem == null)
            {
                return InventoryOperationResult.Fail(
                    InventoryOperationError.InvalidSource,
                    "[PlayerInventory] TryPlaceIntoContainer: itemToPlace ??? containerItem == null");
            }

            if (containerItem.definition == null || !containerItem.definition.isContainer)
            {
                return InventoryOperationResult.Fail(
                    InventoryOperationError.NotAContainer,
                    "[PlayerInventory] TryPlaceIntoContainer: ??????? ??????? ?? ???????? ???????????.");
            }

            // ????????? ?????????? ?????? ??????????? (????? ???????? ?????).
            if (itemToPlace.definition != null && itemToPlace.definition.isContainer)
            {
                return InventoryOperationResult.Fail(
                    InventoryOperationError.NestedContainersNotAllowed,
                    "[PlayerInventory] TryPlaceIntoContainer: ????????? ?????????? ???? ?? ??????????????.");
            }

            if (containerItem.nestedContainer == null)
            {
                return InventoryOperationResult.Fail(
                    InventoryOperationError.InvalidTarget,
                    "[PlayerInventory] TryPlaceIntoContainer: nestedContainer == null ? ??????????.");
            }

            // ??????? ??????? ?? ???????? ????? ? ??????? ???????? ? ?????????.
            mainInventory?.RemoveItem(itemToPlace);

            bool placed = containerItem.nestedContainer.TryAddItemToFirstAvailable(itemToPlace);
            if (!placed)
            {
                // ???? ?? ??????? ??????????, ???????? ??????? ??????? ???????.
                bool returnedBack = mainInventory != null &&
                                    mainInventory.TryAddItemToFirstAvailable(itemToPlace);

                var error = returnedBack
                    ? InventoryOperationError.ContainerFull
                    : InventoryOperationError.NoSpace;

                return InventoryOperationResult.Fail(
                    error,
                    "[PlayerInventory] TryPlaceIntoContainer: ?? ??????? ?????????? ??????? ? ??????????.");
            }

            return InventoryOperationResult.Ok();
        }

        /// <summary>
        /// ?????????? ??????? ??????? ?? ?????????? ?????????? ? ???????? ?????.
        /// </summary>
        public InventoryOperationResult TryRemoveFromContainer(
            InventoryItem itemFromContainer,
            InventoryItem containerItem)
        {
            if (itemFromContainer == null || containerItem == null)
            {
                return InventoryOperationResult.Fail(
                    InventoryOperationError.InvalidSource,
                    "[PlayerInventory] TryRemoveFromContainer: itemFromContainer ??? containerItem == null");
            }

            if (containerItem.nestedContainer == null)
            {
                return InventoryOperationResult.Fail(
                    InventoryOperationError.InvalidSource,
                    "[PlayerInventory] TryRemoveFromContainer: nestedContainer == null ? ??????????.");
            }

            // ??????? ?? ????????? ?????
            containerItem.nestedContainer.RemoveItem(itemFromContainer);

            if (mainInventory == null)
            {
                return InventoryOperationResult.Fail(
                    InventoryOperationError.InvalidTarget,
                    "[PlayerInventory] TryRemoveFromContainer: mainInventory == null");
            }

            bool placed = mainInventory.TryAddItemToFirstAvailable(itemFromContainer);
            if (!placed)
            {
                // ???????? ??????? ??????? ??????? ? ????????? ?? ?????? ????????????.
                bool returnedBack = containerItem.nestedContainer.TryAddItemToFirstAvailable(itemFromContainer);
                var error = returnedBack
                    ? InventoryOperationError.NoSpace
                    : InventoryOperationError.Unknown;

                return InventoryOperationResult.Fail(
                    error,
                    "[PlayerInventory] TryRemoveFromContainer: ??? ????? ? ???????? ?????.");
            }

            return InventoryOperationResult.Ok();
        }

        #endregion

        /// <summary>
        /// ����������� �������� ������� ������.
        /// 1) ���� ������� ��������� � ������� ���������� ����� ��� � ������������ �����;
        /// 2) ������� � �������� ��� ��������� ���������� � �����.
        /// ������ ���������� ����������� ���������� (�� quantity).
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
        /// ����������� ������ ������� �� ��������� � �������� ���� ����������.
        /// ���� � ����� ��� ������� � �� ������������ � ��������� (���� ���� �����).
        /// </summary>
        public bool TryEquipItem(InventoryItem item, EquipmentSlotType targetSlot)
        {
            if (item == null || item.definition == null)
            {
                Debug.LogWarning("[PlayerInventory] TryEquipItem: item ��� definition == null");
                return false;
            }

            Debug.Log($"[PlayerInventory] TryEquipItem: item={item.definition.displayName}, " +
                      $"targetSlot={targetSlot}, isEquippable={item.definition.isEquippable}, " +
                      $"itemSlot={item.definition.equipmentSlotType}");

            if (!equipment.CanEquip(item, targetSlot))
            {
                Debug.LogWarning("[PlayerInventory] CanEquip ������ false");
                return false;
            }

            if (!equipment.TryEquip(item, targetSlot, out InventoryItem previous))
            {
                Debug.LogWarning("[PlayerInventory] EquipmentSlots.TryEquip ������ false");
                return false;
            }

            // ������� ������� ������� �� ����� (�� ������ "� �����")
            mainInventory.RemoveItem(item);

            // ���� ���-�� ���� ������ �� ����� � �������� ������� � �����
            if (previous != null && previous.definition != null)
            {
                Debug.Log($"[PlayerInventory] � ����� {targetSlot} ��� ��� {previous.definition.displayName}, " +
                          "�������� ������� � ���������");

                bool placed = mainInventory.TryAddItemToFirstAvailable(previous);
                if (!placed)
                {
                    Debug.LogWarning($"[PlayerInventory] ��� �����, ����� ������� {previous.definition.displayName} � ���������.");
                }
            }
            else
            {
                Debug.Log($"[PlayerInventory] ���� {targetSlot} ��� ����, previous == null ��� ��� definition");
            }

            return true;
        }

        /// <summary>
        /// ����� ������� �� ���������� ����� ���������� � �������� ������� � ���������.
        /// </summary>
        public bool TryUnequipItem(EquipmentSlotType slotType)
        {
            if (!equipment.TryUnequip(slotType, out InventoryItem previous))
                return false;

            if (previous == null || previous.definition == null) return false;

            bool placed = mainInventory.TryAddItemToFirstAvailable(previous);
            if (!placed)
            {
                Debug.LogWarning("[PlayerInventory] ��� ����� ��� �������� ����� ������ ����������.");
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