using UnityEngine;

namespace UnityProject.Inventory
{
    /// <summary>
    /// ќтладочный скрипт дл€ проверки Inventory без UI.
    /// ѕовесить на Player р€дом с PlayerInventory.
    /// </summary>
    public class InventoryDebugTester : MonoBehaviour
    {
        [Header("References")]
        public PlayerInventory playerInventory;

        [Header("Test Items")]
        public ItemDefinition stackableItem;
        public ItemDefinition bigItem;
        public ItemDefinition equippableItem;

        [Header("Settings")]
        public int stackableCount = 25;

        private void Awake()
        {
            if (playerInventory == null)
                playerInventory = GetComponent<PlayerInventory>();

            if (playerInventory == null)
                Debug.LogError("[InventoryDebugTester] PlayerInventory не найден.");
        }

        private void Start()
        {
            if (playerInventory == null) return;

            Debug.Log("=== InventoryDebugTester START ===");

            TestAddStackable();
            TestAddBigItem();
            TestEquipItem();
            DumpInventory();
        }

        private void TestAddStackable()
        {
            if (stackableItem == null) return;
            int added = playerInventory.AddItem(stackableItem, stackableCount);
            Debug.Log($"[DebugTester] {stackableItem.displayName}: " +
                      $"запрошено {stackableCount}, добавлено {added}");
        }

        private void TestAddBigItem()
        {
            if (bigItem == null) return;
            int added = playerInventory.AddItem(bigItem, 1);
            Debug.Log($"[DebugTester] {bigItem.displayName}: добавлено {added}");
        }

        private void TestEquipItem()
        {
            if (equippableItem == null) return;

            playerInventory.AddItem(equippableItem, 1);

            InventoryItem found = null;
            foreach (var item in playerInventory.MainInventory.Items)
            {
                if (item.definition == equippableItem)
                {
                    found = item;
                    break;
                }
            }

            if (found == null) return;

            var targetSlot = equippableItem.equipmentSlotType;
            if (targetSlot == EquipmentSlotType.None) return;

            bool result = playerInventory.TryEquipItem(found, targetSlot);
            Debug.Log($"[DebugTester] Ёкипировка {equippableItem.displayName} " +
                      $"в {targetSlot}: {result}");
        }

        private void DumpInventory()
        {
            var inv = playerInventory.MainInventory;
            Debug.Log($"[DebugTester] »нвентарь: {inv.Items.Count} записей");

            foreach (var item in inv.Items)
            {
                string name = item.definition != null
                    ? item.definition.displayName : "NULL";
                Debug.Log($"  - {name} x{item.quantity}");
            }

            if (playerInventory.Equipment?.Slots != null)
            {
                Debug.Log("[DebugTester] Ёкипировка:");
                foreach (var slot in playerInventory.Equipment.Slots)
                {
                    string itemName = slot.equippedItem?.definition != null
                        ? slot.equippedItem.definition.displayName : "(пусто)";
                    Debug.Log($"  - {slot.slotType}: {itemName}");
                }
            }

            Debug.Log($"[DebugTester] ќбщий вес: {playerInventory.CalculateTotalWeight():F1}");
        }
    }
}