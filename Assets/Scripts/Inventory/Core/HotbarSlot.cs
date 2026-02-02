using System;
using System.Collections.Generic;
using UnityEngine;

namespace UnityProject.Inventory
{
    [Serializable]
    public class HotbarSlot
    {
        [Tooltip("Предмет, привязанный к этому хотбар-слоту.")]
        public InventoryItem linkedItem;
    }

    [Serializable]
    public class Hotbar
    {
        [SerializeField]
        private List<HotbarSlot> slots = new List<HotbarSlot>();

        public IReadOnlyList<HotbarSlot> Slots => slots;

        public Hotbar(int size)
        {
            size = Mathf.Max(1, size);
            slots = new List<HotbarSlot>(size);
            for (int i = 0; i < size; i++)
                slots.Add(new HotbarSlot());
        }

        public void Assign(int index, InventoryItem item)
        {
            if (index < 0 || index >= slots.Count)
            {
                Debug.LogWarning($"[Hotbar] Assign: index {index} out of range (size={slots.Count})");
                return;
            }
            slots[index].linkedItem = item;
        }

        public InventoryItem GetItem(int index)
        {
            if (index < 0 || index >= slots.Count) return null;
            return slots[index].linkedItem;
        }
    }
}