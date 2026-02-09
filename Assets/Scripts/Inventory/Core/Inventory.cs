using System;
using System.Collections.Generic;
using UnityEngine;

namespace UnityProject.Inventory
{
    /// <summary>
    /// Список предметов без координат. Замена InventoryGrid.
    /// Поддерживает стакинг, событие OnChanged для UI.
    /// </summary>
    [Serializable]
    public class Inventory
    {
        [SerializeField]
        private List<InventoryItem> items = new List<InventoryItem>();

        public IReadOnlyList<InventoryItem> Items => items;

        /// <summary>Событие при любом изменении содержимого.</summary>
        public event Action OnChanged;

        /// <summary>
        /// Добавить предмет. Сначала пытается заполнить существующие стаки,
        /// затем создаёт новые записи. Возвращает фактически добавленное количество.
        /// </summary>
        public int AddItem(ItemDefinition definition, int quantity = 1)
        {
            if (definition == null || quantity <= 0) return 0;

            int remaining = quantity;

            if (definition.stackable)
            {
                foreach (var item in items)
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

            while (remaining > 0)
            {
                int stackSize = definition.stackable
                    ? Mathf.Min(definition.maxStack, remaining)
                    : 1;

                items.Add(new InventoryItem(definition, stackSize));
                remaining -= stackSize;
            }

            int added = quantity - remaining;
            if (added > 0) OnChanged?.Invoke();
            return added;
        }

        /// <summary>
        /// Удалить указанное количество предмета по определению.
        /// Возвращает фактически удалённое количество.
        /// </summary>
        public int RemoveItem(ItemDefinition definition, int quantity = 1)
        {
            if (definition == null || quantity <= 0) return 0;

            int remaining = quantity;
            var toRemove = new List<InventoryItem>();

            foreach (var item in items)
            {
                if (remaining <= 0) break;
                if (item.definition != definition) continue;

                if (item.quantity <= remaining)
                {
                    remaining -= item.quantity;
                    toRemove.Add(item);
                }
                else
                {
                    item.quantity -= remaining;
                    remaining = 0;
                }
            }

            foreach (var item in toRemove)
                items.Remove(item);

            int removed = quantity - remaining;
            if (removed > 0) OnChanged?.Invoke();
            return removed;
        }

        /// <summary>Удалить конкретный экземпляр предмета.</summary>
        public bool RemoveItem(InventoryItem item)
        {
            if (item == null) return false;
            bool removed = items.Remove(item);
            if (removed) OnChanged?.Invoke();
            return removed;
        }

        /// <summary>Есть ли хотя бы один предмет данного типа.</summary>
        public bool Contains(ItemDefinition definition)
        {
            if (definition == null) return false;
            foreach (var item in items)
                if (item.definition == definition) return true;
            return false;
        }

        /// <summary>Общее количество предмета данного типа.</summary>
        public int GetCount(ItemDefinition definition)
        {
            if (definition == null) return 0;
            int total = 0;
            foreach (var item in items)
                if (item.definition == definition)
                    total += item.quantity;
            return total;
        }

        /// <summary>Суммарный вес всех предметов.</summary>
        public float CalculateTotalWeight()
        {
            float total = 0f;
            foreach (var item in items)
            {
                if (item.definition == null) continue;
                total += item.definition.weight * Mathf.Max(1, item.quantity);
            }
            return total;
        }

        /// <summary>Очистить весь инвентарь.</summary>
        public void Clear()
        {
            if (items.Count == 0) return;
            items.Clear();
            OnChanged?.Invoke();
        }
    }
}