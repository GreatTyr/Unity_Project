using System;
using System.Collections.Generic;
using System.Linq;

namespace UnityProject.Inventory
{
    public struct InventoryListEntry
    {
        public ItemDefinition definition;
        public int totalQuantity;

        public InventoryListEntry(ItemDefinition def, int quantity)
        {
            definition = def;
            totalQuantity = quantity;
        }
    }

    /// <summary>
    /// Преобразует Inventory в отсортированный список строк для UI.
    /// Поддерживает фильтрацию по категории.
    /// </summary>
    public static class InventoryListModel
    {
        private static readonly ItemCategory[] CategoryOrder =
        {
            ItemCategory.Weapon,
            ItemCategory.Armor,
            ItemCategory.Module,
            ItemCategory.Resource,
            ItemCategory.Other
        };

        /// <summary>
        /// Построить список строк из инвентаря.
        /// Группирует по definition, суммирует quantity, сортирует.
        /// </summary>
        public static List<InventoryListEntry> BuildList(
            Inventory inventory,
            ItemCategory? filter = null)
        {
            if (inventory == null || inventory.Items == null)
                return new List<InventoryListEntry>();

            var query = inventory.Items
                .Where(item => item != null && item.definition != null);

            if (filter.HasValue)
                query = query.Where(item => item.definition.itemCategory == filter.Value);

            var grouped = query
                .GroupBy(item => item.definition)
                .Select(g => new InventoryListEntry(g.Key, g.Sum(i => i.quantity)))
                .ToList();

            grouped.Sort((a, b) =>
            {
                int catA = GetCategorySortOrder(a.definition.itemCategory);
                int catB = GetCategorySortOrder(b.definition.itemCategory);
                if (catA != catB) return catA.CompareTo(catB);

                string nameA = a.definition.displayName ?? "";
                string nameB = b.definition.displayName ?? "";
                return string.Compare(nameA, nameB, StringComparison.OrdinalIgnoreCase);
            });

            return grouped;
        }

        public static int GetCategorySortOrder(ItemCategory category)
        {
            int index = Array.IndexOf(CategoryOrder, category);
            return index < 0 ? int.MaxValue : index;
        }
    }
}