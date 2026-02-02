using System;
using UnityEngine;

namespace UnityProject.Inventory
{
    [Serializable]
    public class InventoryItem
    {
        [Tooltip("Тип предмета (ScriptableObject).")]
        public ItemDefinition definition;

        [Tooltip("Количество в стеке (для stackable предметов).")]
        public int quantity = 1;

        [Tooltip("Повернут ли предмет (width/height поменяны местами).")]
        public bool rotated = false;

        // Позиция в сетке (левая верхняя клетка)
        public int x;
        public int y;

        // Вложенный контейнер (для сумок, рюкзаков и т.п.)
        // Не сериализуем, чтобы не провоцировать рекурсивную сериализацию.
        [NonSerialized]
        public InventoryGrid nestedContainer;

        public int CurrentWidth => definition == null
            ? 1
            : (rotated ? definition.gridHeight : definition.gridWidth);

        public int CurrentHeight => definition == null
            ? 1
            : (rotated ? definition.gridWidth : definition.gridHeight);

        public InventoryItem(ItemDefinition def, int quantity = 1)
        {
            definition = def;
            this.quantity = Mathf.Max(1, quantity);

            if (definition != null && definition.isContainer &&
                definition.containerWidth > 0 && definition.containerHeight > 0)
            {
                nestedContainer = new InventoryGrid(
                    definition.containerWidth,
                    definition.containerHeight);
            }
        }

        public bool CanAddToStack(int count)
        {
            if (definition == null || !definition.stackable) return false;
            if (count <= 0) return false;
            return quantity + count <= definition.maxStack;
        }

        public int AddToStack(int count)
        {
            if (!CanAddToStack(count))
            {
                if (definition == null || !definition.stackable) return 0;
                int available = definition.maxStack - quantity;
                int added = Mathf.Clamp(count, 0, available);
                quantity += added;
                return added;
            }
            quantity += count;
            return count;
        }
    }
}