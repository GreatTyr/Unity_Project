using System.Linq;
using UnityEngine;

namespace UnityProject.Inventory
{
    /// <summary>
    /// Сервис безопасного переноса предметов между источниками инвентаря.
    /// </summary>
    public static class InventoryTransferService
    {
        public static InventoryOperationResult TransferItems(
            IInventorySource source,
            IInventorySource target,
            ItemDefinition definition,
            int quantity = -1)
        {
            if (source == null || target == null)
                return InventoryOperationResult.Fail(
                    InventoryOperationError.InvalidSource,
                    "source или target == null");

            if (definition == null)
                return InventoryOperationResult.Fail(
                    InventoryOperationError.InvalidSource,
                    "definition == null");

            var sourceInv = source.MainInventory;
            var targetInv = target.MainInventory;

            if (sourceInv == null || targetInv == null)
                return InventoryOperationResult.Fail(
                    InventoryOperationError.InvalidTarget,
                    "sourceInventory или targetInventory == null");

            int available = sourceInv.GetCount(definition);
            if (available == 0)
                return InventoryOperationResult.Fail(
                    InventoryOperationError.ItemNotFound,
                    $"Предмет {definition.displayName} не найден в источнике");

            int toTransfer = quantity < 0 ? available : Mathf.Min(quantity, available);
            if (toTransfer <= 0)
                return InventoryOperationResult.Fail(
                    InventoryOperationError.ItemNotFound,
                    "Нет доступного количества для переноса");

            // Удаляем из источника
            int removed = sourceInv.RemoveItem(definition, toTransfer);

            // Добавляем в цель
            int added = targetInv.AddItem(definition, removed);

            // Если добавили не всё — возвращаем остаток
            if (added < removed)
            {
                int toReturn = removed - added;
                sourceInv.AddItem(definition, toReturn);

                return InventoryOperationResult.Fail(
                    InventoryOperationError.NoSpace,
                    $"Удалось перенести только {added} из {toTransfer}");
            }

            return InventoryOperationResult.Ok();
        }
    }
}