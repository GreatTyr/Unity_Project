using System;
using System.Collections.Generic;
using UnityEngine;

namespace UnityProject.Inventory
{
    /// <summary>
    /// Сетчатый инвентарь W×H. Хранит список предметов и реализует размещение.
    /// НЕ MonoBehaviour.
    /// </summary>
    [Serializable]
    public class InventoryGrid
    {
        [SerializeField]
        private int width;
        [SerializeField]
        private int height;

        [SerializeField]
        private List<InventoryItem> items = new List<InventoryItem>();

        public int Width => width;
        public int Height => height;
        public IReadOnlyList<InventoryItem> Items => items;

        public InventoryGrid(int width, int height)
        {
            this.width = Mathf.Max(1, width);
            this.height = Mathf.Max(1, height);
            items = new List<InventoryItem>();
        }

        public bool CanPlaceItem(InventoryItem item, int x, int y, bool rotated)
        {
            if (item == null || item.definition == null)
                return false;

            int w = rotated ? item.definition.gridHeight : item.definition.gridWidth;
            int h = rotated ? item.definition.gridWidth : item.definition.gridHeight;

            if (x < 0 || y < 0 || x + w > width || y + h > height)
                return false;

            foreach (var other in items)
            {
                if (other == item) continue;
                if (IsOverlap(x, y, w, h,
                              other.x, other.y, other.CurrentWidth, other.CurrentHeight))
                {
                    return false;
                }
            }

            return true;
        }

        public bool TryAddItem(InventoryItem item, int x, int y, bool rotated)
        {
            if (!CanPlaceItem(item, x, y, rotated))
                return false;

            item.x = x;
            item.y = y;
            item.rotated = rotated;

            if (!items.Contains(item))
                items.Add(item);

            return true;
        }

        public bool TryFindSpaceFor(InventoryItem item, out int x, out int y, out bool rotated)
        {
            x = y = 0;
            rotated = false;
            if (item == null || item.definition == null)
                return false;

            bool canRotate = item.definition.canRotate;

            for (int rot = 0; rot < (canRotate ? 2 : 1); rot++)
            {
                bool r = rot == 1;
                int w = r ? item.definition.gridHeight : item.definition.gridWidth;
                int h = r ? item.definition.gridWidth : item.definition.gridHeight;

                for (int j = 0; j <= height - h; j++)
                {
                    for (int i = 0; i <= width - w; i++)
                    {
                        if (CanPlaceItem(item, i, j, r))
                        {
                            x = i;
                            y = j;
                            rotated = r;
                            return true;
                        }
                    }
                }
            }

            return false;
        }

        public void RemoveItem(InventoryItem item)
        {
            if (item == null) return;
            items.Remove(item);
        }

        public bool TryAddToExistingStack(InventoryItem source, out InventoryItem targetStack, out int added)
        {
            targetStack = null;
            added = 0;

            if (source == null || source.definition == null)
                return false;
            var def = source.definition;
            if (!def.stackable) return false;

            foreach (var item in items)
            {
                if (item.definition == def && item.quantity < def.maxStack)
                {
                    int capacity = def.maxStack - item.quantity;
                    int toMove = Mathf.Min(capacity, source.quantity);
                    if (toMove <= 0) continue;

                    item.quantity += toMove;
                    source.quantity -= toMove;

                    targetStack = item;
                    added = toMove;
                    return true;
                }
            }

            return false;
        }

        public bool TryAddItemToFirstAvailable(InventoryItem item)
        {
            if (item == null) return false;
            if (!TryFindSpaceFor(item, out int x, out int y, out bool rotated))
                return false;

            return TryAddItem(item, x, y, rotated);
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

        private bool IsOverlap(
            int x1, int y1, int w1, int h1,
            int x2, int y2, int w2, int h2)
        {
            bool noOverlap =
                x1 + w1 <= x2 ||
                x2 + w2 <= x1 ||
                y1 + h1 <= y2 ||
                y2 + h2 <= y1;

            return !noOverlap;
        }
    }
}