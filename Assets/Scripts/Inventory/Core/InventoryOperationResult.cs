using System;
using UnityEngine;

namespace UnityProject.Inventory
{
    /// <summary>
    /// Перечисление возможных ошибок операций с инвентарём.
    /// Используется для более безопасной и прозрачной логики перемещения предметов.
    /// </summary>
    public enum InventoryOperationError
    {
        None = 0,

        // Общие ошибки
        Unknown = 1,
        InvalidSource = 2,
        InvalidTarget = 3,
        ItemNotFound = 4,

        // Сетка / размещение
        NoSpace = 10,
        OutOfBounds = 11,
        OverlapsOtherItem = 12,

        // Экипировка
        WrongSlotType = 20,
        NotEquippable = 21,

        // Контейнеры
        NotAContainer = 30,
        ContainerFull = 31,
        NestedContainersNotAllowed = 32
    }

    /// <summary>
    /// Результат операции с инвентарём: успех/ошибка + тип ошибки и опциональное сообщение.
    /// Это простой, но расширяемый объект, который можно логировать и показывать в UI.
    /// </summary>
    [Serializable]
    public struct InventoryOperationResult
    {
        [SerializeField] private bool success;
        [SerializeField] private InventoryOperationError error;
        [SerializeField] private string message;

        public bool Success => success;
        public InventoryOperationError Error => error;
        public string Message => message;

        public static InventoryOperationResult Ok()
        {
            return new InventoryOperationResult
            {
                success = true,
                error = InventoryOperationError.None,
                message = string.Empty
            };
        }

        public static InventoryOperationResult Fail(
            InventoryOperationError error,
            string message = "")
        {
            return new InventoryOperationResult
            {
                success = false,
                error = error,
                message = message
            };
        }

        public override string ToString()
        {
            return $"InventoryOperationResult(Success={success}, Error={error}, Message={message})";
        }
    }
}

