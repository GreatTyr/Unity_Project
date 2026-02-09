using System;
using UnityEngine;

namespace UnityProject.Inventory
{
    /// <summary>
    /// Экземпляр предмета в инвентаре: ссылка на определение + количество.
    /// Без координат сетки, без вложенных контейнеров.
    /// </summary>
    [Serializable]
    public class InventoryItem
    {
        [Tooltip("Тип предмета (ScriptableObject).")]
        public ItemDefinition definition;

        [Tooltip("Количество в стеке (для stackable предметов).")]
        public int quantity = 1;

        public InventoryItem(ItemDefinition def, int quantity = 1)
        {
            definition = def;
            this.quantity = Mathf.Max(1, quantity);
        }

        public bool CanAddToStack(int count)
        {
            if (definition == null || !definition.stackable) return false;
            if (count <= 0) return false;
            return quantity + count <= definition.maxStack;
        }

        public int AddToStack(int count)
        {
            if (definition == null || !definition.stackable) return 0;
            int available = definition.maxStack - quantity;
            int added = Mathf.Clamp(count, 0, available);
            quantity += added;
            return added;
        }
    }
}