using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace UnityProject.Inventory
{
    /// <summary>
    /// Модель одной строки в списке инвентаря (агрегированная по типу предмета).
    /// </summary>
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
    /// Утилита для преобразования InventoryGrid в список строк для UI (стиль Mount and Blade).
    /// Агрегирует предметы по типу (ItemDefinition) и сортирует по категориям.
    /// </summary>
    public static class InventoryListModel
    {
        /// <summary>
        /// Порядок категорий для сортировки (как они должны отображаться в списке).
        /// </summary>
        private static readonly ItemCategory[] CategoryOrder = new[]
        {
            ItemCategory.Weapon,
            ItemCategory.Armor,
            ItemCategory.Module,
            ItemCategory.Resource,
            ItemCategory.Other
        };

        /// <summary>
        /// Преобразовать сетку инвентаря в отсортированный список строк для отображения.
        /// </summary>
        public static List<InventoryListEntry> BuildList(InventoryGrid grid)
        {
            if (grid == null || grid.Items == null)
                return new List<InventoryListEntry>();

            // Группируем предметы по definition и суммируем количество.
            var grouped = grid.Items
                .Where(item => item != null && item.definition != null)
                .GroupBy(item => item.definition)
                .Select(group => new InventoryListEntry(
                    group.Key,
                    group.Sum(item => item.quantity)))
                .ToList();

            // Сортируем: сначала по категории (по порядку CategoryOrder),
            // затем внутри категории по имени.
            grouped.Sort((a, b) =>
            {
                if (a.definition == null || b.definition == null)
                    return 0;

                int categoryA = Array.IndexOf(CategoryOrder, a.definition.itemCategory);
                int categoryB = Array.IndexOf(CategoryOrder, b.definition.itemCategory);

                // Если категория не найдена в порядке, ставим в конец.
                if (categoryA < 0) categoryA = int.MaxValue;
                if (categoryB < 0) categoryB = int.MaxValue;

                if (categoryA != categoryB)
                    return categoryA.CompareTo(categoryB);

                // Внутри категории сортируем по имени.
                string nameA = string.IsNullOrEmpty(a.definition.displayName) ? "" : a.definition.displayName;
                string nameB = string.IsNullOrEmpty(b.definition.displayName) ? "" : b.definition.displayName;
                return string.Compare(nameA, nameB, StringComparison.OrdinalIgnoreCase);
            });

            return grouped;
        }

        /// <summary>
        /// Получить порядковый номер категории для сортировки (меньше = выше в списке).
        /// </summary>
        public static int GetCategorySortOrder(ItemCategory category)
        {
            int index = Array.IndexOf(CategoryOrder, category);
            return index < 0 ? int.MaxValue : index;
        }
    }
}
