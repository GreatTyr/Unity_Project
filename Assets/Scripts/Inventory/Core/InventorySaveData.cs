using System;
using System.Collections.Generic;
using UnityEngine;

namespace UnityProject.Inventory
{
    /// <summary>
    /// Интерфейс для поиска ItemDefinition по itemId при загрузке сохранений.
    /// Конкретная реализация может использовать ScriptableObject-реестр,
    /// Resources.LoadAll или Addressables – в зависимости от архитектуры проекта.
    /// </summary>
    public interface IItemDefinitionResolver
    {
        ItemDefinition Resolve(string itemId);
    }

    /// <summary>
    /// DTO описания одного предмета в сетчатом инвентаре.
    /// Содержит ссылку на тип (itemId), позицию и вращение в сетке,
    /// количество в стеке и опционально вложенный контейнер.
    /// </summary>
    [Serializable]
    public class InventoryItemData
    {
        public string itemId;
        public int quantity;
        public bool rotated;
        public int x;
        public int y;

        public InventoryGridData nestedContainer;
    }

    /// <summary>
    /// DTO описания одной сетки инвентаря (W×H + список предметов).
    /// Используется как для основного рюкзака, так и для вложенных контейнеров.
    /// </summary>
    [Serializable]
    public class InventoryGridData
    {
        public int width;
        public int height;
        public List<InventoryItemData> items = new List<InventoryItemData>();
    }

    /// <summary>
    /// DTO описания одного слота экипировки.
    /// </summary>
    [Serializable]
    public class EquipmentSlotData
    {
        public EquipmentSlotType slotType;
        public InventoryItemData equippedItem;
    }

    /// <summary>
    /// DTO всего инвентаря игрока:
    /// - основная сетка;
    /// - экипировка;
    /// - хотбар (как ссылки на предметы).
    /// </summary>
    [Serializable]
    public class PlayerInventorySaveData
    {
        public InventoryGridData mainInventory;
        public List<EquipmentSlotData> equipment = new List<EquipmentSlotData>();
        public List<InventoryItemData> hotbar = new List<InventoryItemData>();
    }

    /// <summary>
    /// Утилита для преобразования runtime-состояния инвентаря игрока
    /// в DTO-структуры для последующей сериализации (и наоборот).
    /// На данном этапе реализуем надёжный путь "в DTO" (сохранение).
    /// Путь загрузки можно доработать позже, опираясь на IItemDefinitionResolver.
    /// </summary>
    public static class InventorySaveUtility
    {
        /// <summary>
        /// Снять слепок инвентаря игрока в сериализуемую структуру.
        /// </summary>
        public static PlayerInventorySaveData Capture(PlayerInventory playerInventory)
        {
            if (playerInventory == null)
            {
                Debug.LogWarning("[InventorySaveUtility] Capture: playerInventory == null");
                return null;
            }

            var data = new PlayerInventorySaveData
            {
                mainInventory = CaptureGrid(playerInventory.MainInventory)
            };

            // Экипировка.
            if (playerInventory.Equipment != null &&
                playerInventory.Equipment.Slots != null)
            {
                foreach (var slot in playerInventory.Equipment.Slots)
                {
                    var slotData = new EquipmentSlotData
                    {
                        slotType = slot.slotType,
                        equippedItem = CaptureItem(slot.equippedItem)
                    };
                    data.equipment.Add(slotData);
                }
            }

            // Хотбар – сохраняем как отдельный список предметов.
            if (playerInventory.Hotbar != null &&
                playerInventory.Hotbar.Slots != null)
            {
                foreach (var hotbarSlot in playerInventory.Hotbar.Slots)
                {
                    var itemData = CaptureItem(hotbarSlot.linkedItem);
                    data.hotbar.Add(itemData);
                }
            }

            return data;
        }

        /// <summary>
        /// Преобразование одной сетки InventoryGrid в DTO.
        /// Включает рекурсивную обработку вложенных контейнеров.
        /// </summary>
        private static InventoryGridData CaptureGrid(InventoryGrid grid)
        {
            if (grid == null)
                return null;

            var data = new InventoryGridData
            {
                width = grid.Width,
                height = grid.Height
            };

            if (grid.Items != null)
            {
                foreach (var item in grid.Items)
                {
                    var itemData = CaptureItem(item);
                    if (itemData != null)
                        data.items.Add(itemData);
                }
            }

            return data;
        }

        /// <summary>
        /// Преобразование одного InventoryItem в DTO.
        /// При необходимости рекурсивно снимает слепок вложенного контейнера.
        /// </summary>
        private static InventoryItemData CaptureItem(InventoryItem item)
        {
            if (item == null || item.definition == null)
                return null;

            var data = new InventoryItemData
            {
                itemId = item.definition.itemId,
                quantity = item.quantity,
                rotated = item.rotated,
                x = item.x,
                y = item.y
            };

            if (item.definition.isContainer && item.nestedContainer != null)
            {
                data.nestedContainer = CaptureGrid(item.nestedContainer);
            }

            return data;
        }
    }
}

