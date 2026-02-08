using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace UnityProject.Inventory
{
    /// <summary>
    /// Сервис для переноса предметов между источниками инвентаря (левая ↔ правая панели).
    /// Реализует безопасную логику перемещения с учётом стеков и ограничений сетки.
    /// </summary>
    public static class InventoryTransferService
    {
        /// <summary>
        /// Перенести N штук предмета definition из источника source в источник target.
        /// </summary>
        /// <param name="source">Источник-отправитель</param>
        /// <param name="target">Источник-получатель</param>
        /// <param name="definition">Тип предмета для переноса</param>
        /// <param name="quantity">Количество для переноса (если -1, переносим всё доступное)</param>
        /// <returns>Результат операции с информацией об ошибке, если что-то пошло не так</returns>
        public static InventoryOperationResult TransferItems(
            IInventorySource source,
            IInventorySource target,
            ItemDefinition definition,
            int quantity = -1)
        {
            if (source == null || target == null)
            {
                return InventoryOperationResult.Fail(
                    InventoryOperationError.InvalidSource,
                    "[InventoryTransferService] source или target == null");
            }

            if (definition == null)
            {
                return InventoryOperationResult.Fail(
                    InventoryOperationError.InvalidSource,
                    "[InventoryTransferService] definition == null");
            }

            var sourceGrid = source.MainInventory;
            var targetGrid = target.MainInventory;

            if (sourceGrid == null || targetGrid == null)
            {
                return InventoryOperationResult.Fail(
                    InventoryOperationError.InvalidTarget,
                    "[InventoryTransferService] sourceGrid или targetGrid == null");
            }

            // Находим все предметы нужного типа в источнике.
            var sourceItems = sourceGrid.Items
                .Where(item => item != null && item.definition == definition)
                .ToList();

            if (sourceItems.Count == 0)
            {
                return InventoryOperationResult.Fail(
                    InventoryOperationError.ItemNotFound,
                    $"[InventoryTransferService] Предмет {definition.displayName} не найден в источнике");
            }

            // Вычисляем доступное количество.
            int availableQuantity = sourceItems.Sum(item => item.quantity);
            int toTransfer = quantity < 0 ? availableQuantity : Mathf.Min(quantity, availableQuantity);

            if (toTransfer <= 0)
            {
                return InventoryOperationResult.Fail(
                    InventoryOperationError.ItemNotFound,
                    "[InventoryTransferService] Нет доступного количества для переноса");
            }

            // Удаляем предметы из источника (сначала уменьшаем quantity, потом удаляем стеки).
            int remainingToRemove = toTransfer;
            var itemsToRemove = new List<InventoryItem>();

            foreach (var item in sourceItems)
            {
                if (remainingToRemove <= 0) break;

                if (item.quantity <= remainingToRemove)
                {
                    // Удаляем весь стек.
                    remainingToRemove -= item.quantity;
                    itemsToRemove.Add(item);
                }
                else
                {
                    // Уменьшаем количество в стеке.
                    item.quantity -= remainingToRemove;
                    remainingToRemove = 0;
                }
            }

            // Удаляем целые стеки из сетки.
            foreach (var item in itemsToRemove)
            {
                sourceGrid.RemoveItem(item);
            }

            // Пытаемся добавить в целевой инвентарь.
            int actuallyAdded = AddItemToGrid(targetGrid, definition, toTransfer);

            // Если не удалось добавить всё, возвращаем остаток обратно в источник.
            if (actuallyAdded < toTransfer)
            {
                int toReturn = toTransfer - actuallyAdded;
                AddItemToGrid(sourceGrid, definition, toReturn);

                return InventoryOperationResult.Fail(
                    InventoryOperationError.NoSpace,
                    $"[InventoryTransferService] Удалось перенести только {actuallyAdded} из {toTransfer} штук");
            }

            return InventoryOperationResult.Ok();
        }

        /// <summary>
        /// Добавить предмет в сетку (с учётом стеков и поиска места).
        /// Возвращает фактически добавленное количество.
        /// </summary>
        private static int AddItemToGrid(InventoryGrid grid, ItemDefinition definition, int quantity)
        {
            if (grid == null || definition == null || quantity <= 0)
                return 0;

            int remaining = quantity;

            // Сначала пытаемся добавить в существующие стеки.
            if (definition.stackable)
            {
                foreach (var item in grid.Items)
                {
                    if (remaining <= 0) break;
                    if (item.definition != definition) continue;
                    if (item.quantity >= definition.maxStack) continue;

                    int space = definition.maxStack - item.quantity;
                    int toAdd = Mathf.Min(space, remaining);

                    item.quantity += toAdd;
                    remaining -= toAdd;
                }
            }

            // Затем создаём новые стеки.
            while (remaining > 0)
            {
                int stackAmount = definition.stackable
                    ? Mathf.Min(definition.maxStack, remaining)
                    : 1;

                var newItem = new InventoryItem(definition, stackAmount);
                bool placed = grid.TryAddItemToFirstAvailable(newItem);

                if (!placed)
                    break; // Нет места для нового стека.

                remaining -= stackAmount;
            }

            return quantity - remaining;
        }
    }
}
