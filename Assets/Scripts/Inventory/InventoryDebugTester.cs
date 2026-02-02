using UnityEngine;

namespace UnityProject.Inventory
{
    /// <summary>
    /// Простой отладочный скрипт для проверки работы PlayerInventory/InventoryGrid без UI.
    /// Повесь на объект Player рядом с PlayerInventory.
    /// </summary>
    public class InventoryDebugTester : MonoBehaviour
    {
        [Header("References")]
        [Tooltip("Ссылка на PlayerInventory (если не указана, возьмём с этого же объекта).")]
        public PlayerInventory playerInventory;

        [Header("Test Items")]
        [Tooltip("Обычный предмет 1x1 (например, патроны).")]
        public ItemDefinition stackableItem;
        [Tooltip("Крупный предмет (2x2, броня или оружие).")]
        public ItemDefinition bigItem;
        [Tooltip("Экипируемый предмет для WeaponMain, Body и т.п.")]
        public ItemDefinition equippableItem;

        [Header("Settings")]
        [Tooltip("Сколько штук стекаемого предмета попытаться добавить.")]
        public int stackableCount = 25;

        private void Awake()
        {
            if (playerInventory == null)
                playerInventory = GetComponent<PlayerInventory>();

            if (playerInventory == null)
                Debug.LogError("[InventoryDebugTester] PlayerInventory не найден на объекте.");
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
            if (stackableItem == null)
            {
                Debug.LogWarning("[InventoryDebugTester] stackableItem не назначен.");
                return;
            }

            int added = playerInventory.AddItem(stackableItem, stackableCount);
            Debug.Log($"[InventoryDebugTester] Добавили стакаемый предмет {stackableItem.displayName}: запрошено {stackableCount}, добавлено {added}");
        }

        private void TestAddBigItem()
        {
            if (bigItem == null)
            {
                Debug.LogWarning("[InventoryDebugTester] bigItem не назначен.");
                return;
            }

            int added = playerInventory.AddItem(bigItem, 1);
            Debug.Log($"[InventoryDebugTester] Добавили большой предмет {bigItem.displayName}: добавлено {added}");
        }

        private void TestEquipItem()
        {
            if (equippableItem == null)
            {
                Debug.LogWarning("[InventoryDebugTester] equippableItem не назначен.");
                return;
            }

            // Добавляем один экземпляр экипируемого предмета
            int added = playerInventory.AddItem(equippableItem, 1);
            Debug.Log($"[InventoryDebugTester] Добавили экипируемый предмет {equippableItem.displayName}: {added}");

            // Ищем этот предмет в основной сетке
            InventoryItem found = null;
            foreach (var item in playerInventory.MainInventory.Items)
            {
                if (item.definition == equippableItem)
                {
                    found = item;
                    break;
                }
            }

            if (found == null)
            {
                Debug.LogWarning("[InventoryDebugTester] Не нашли экипируемый предмет в инвентаре.");
                return;
            }

            // Целевой слот берём из definition.equipmentSlotType
            var targetSlot = equippableItem.equipmentSlotType;
            if (targetSlot == EquipmentSlotType.None)
            {
                Debug.LogWarning("[InventoryDebugTester] equippableItem.equipmentSlotType == None, некуда экипировать.");
                return;
            }

            bool equipped = playerInventory.TryEquipItem(found, targetSlot);
            Debug.Log($"[InventoryDebugTester] Попытка экипировать {equippableItem.displayName} в слот {targetSlot}: результат={equipped}");
        }

        private void DumpInventory()
        {
            var grid = playerInventory.MainInventory;
            Debug.Log($"[InventoryDebugTester] MainInventory {grid.Width}x{grid.Height}, предметов: {grid.Items.Count}");

            foreach (var item in grid.Items)
            {
                string defName = item.definition != null ? item.definition.displayName : "NULL_DEF";
                Debug.Log($"  - {defName} x{item.quantity} " +
                          $"pos=({item.x},{item.y}) size={item.CurrentWidth}x{item.CurrentHeight} rotated={item.rotated}");
            }

            // Экипировка
            if (playerInventory.Equipment != null && playerInventory.Equipment.Slots != null)
            {
                Debug.Log("[InventoryDebugTester] Equipment slots:");
                foreach (var slot in playerInventory.Equipment.Slots)
                {
                    string itemName = slot.equippedItem != null && slot.equippedItem.definition != null
                        ? slot.equippedItem.definition.displayName
                        : "(пусто)";
                    Debug.Log($"  - {slot.slotType}: {itemName}");
                }
            }

            float totalWeight = playerInventory.CalculateTotalWeight();
            Debug.Log($"[InventoryDebugTester] Total weight = {totalWeight}");
        }
    }
}