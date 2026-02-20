using System;
using System.Collections.Generic;
using UnityEngine;

namespace UnityProject.Inventory
{
    [Serializable]
    public class Inventory
    {
        [SerializeField]
        private List<InventoryItem> items = new List<InventoryItem>();

        public IReadOnlyList<InventoryItem> Items => items;

        public event Action OnChanged;

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

        public bool RemoveItem(InventoryItem item)
        {
            if (item == null) return false;
            bool removed = items.Remove(item);
            if (removed) OnChanged?.Invoke();
            return removed;
        }

        /// <summary>
        /// Найти первый InventoryItem с указанным definition.
        /// Возвращает null если не найден.
        /// </summary>
        public InventoryItem FindItem(ItemDefinition definition)
        {
            if (definition == null) return null;
            foreach (var item in items)
                if (item.definition == definition) return item;
            return null;
        }

        public bool Contains(ItemDefinition definition)
        {
            if (definition == null) return false;
            foreach (var item in items)
                if (item.definition == definition) return true;
            return false;
        }

        public int GetCount(ItemDefinition definition)
        {
            if (definition == null) return 0;
            int total = 0;
            foreach (var item in items)
                if (item.definition == definition)
                    total += item.quantity;
            return total;
        }

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

        public void Clear()
        {
            if (items.Count == 0) return;
            items.Clear();
            OnChanged?.Invoke();
        }
    }
}