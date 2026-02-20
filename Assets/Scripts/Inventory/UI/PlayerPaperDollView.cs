using System.Collections.Generic;
using UnityEngine;

namespace UnityProject.Inventory
{
    public class PlayerPaperDollView : MonoBehaviour
    {
        [SerializeField] private PlayerInventory playerInventory;
        [SerializeField] private List<EquipmentSlotView> slotViews = new List<EquipmentSlotView>();

        private void Awake()
        {
            if (playerInventory == null)
                playerInventory = PlayerLocator.Inventory;

            foreach (var view in slotViews)
                view?.Initialize(this);
        }

        private void Start() => Refresh();

        public void Refresh()
        {
            if (playerInventory?.Equipment == null) return;
            foreach (var view in slotViews)
            {
                if (view == null) continue;
                view.Refresh(playerInventory.Equipment.GetSlot(view.SlotType));
            }
        }

        public void OnSlotClicked(EquipmentSlotView view)
        {
            if (playerInventory == null || view == null) return;

            bool result = playerInventory.TryUnequipItem(view.SlotType);
            if (result)
                Debug.Log($"[PaperDoll] Снято из слота {view.SlotType}");

            Refresh();
            UIServices.Get<InventoryUIManager>()?.RefreshAllPanels();
        }

        public void OnItemDroppedOnSlot(EquipmentSlotView slotView, InventoryListRowView draggedRow)
        {
            if (playerInventory == null || slotView == null || draggedRow == null) return;

            var entry = draggedRow.BoundEntry;
            var source = draggedRow.SourceInventory;

            if (entry.definition == null || source == null) return;
            if (!entry.definition.isEquippable) return;
            if (entry.definition.equipmentSlotType != slotView.SlotType) return;

            var sourceInv = source.MainInventory;
            if (sourceInv == null) return;

            InventoryItem found = sourceInv.FindItem(entry.definition);
            if (found == null)
            {
                Debug.LogWarning($"[PaperDoll] Предмет {entry.definition.displayName} не найден в источнике");
                return;
            }

            if (!playerInventory.Equipment.CanEquip(
                    new InventoryItem(entry.definition, 1), slotView.SlotType))
            {
                Debug.LogWarning($"[PaperDoll] Нельзя экипировать {entry.definition.displayName} в {slotView.SlotType}");
                return;
            }

            bool transferredFromExternal = false;

            if (sourceInv != playerInventory.MainInventory)
            {
                int removed = sourceInv.RemoveItem(entry.definition, 1);
                if (removed <= 0)
                {
                    Debug.LogWarning($"[PaperDoll] Не удалось забрать {entry.definition.displayName} из источника");
                    return;
                }

                int added = playerInventory.AddItem(entry.definition, 1);
                if (added <= 0)
                {
                    sourceInv.AddItem(entry.definition, 1);
                    Debug.LogWarning($"[PaperDoll] Не удалось добавить в инвентарь игрока, откат");
                    return;
                }

                transferredFromExternal = true;

                found = playerInventory.MainInventory.FindItem(entry.definition);
                if (found == null)
                {
                    playerInventory.MainInventory.RemoveItem(entry.definition, 1);
                    sourceInv.AddItem(entry.definition, 1);
                    Debug.LogWarning($"[PaperDoll] Предмет потерялся после переноса, откат");
                    return;
                }
            }

            bool result = playerInventory.TryEquipItem(found, slotView.SlotType);

            if (!result && transferredFromExternal)
            {
                int rolledBack = playerInventory.MainInventory.RemoveItem(entry.definition, 1);
                if (rolledBack > 0)
                    sourceInv.AddItem(entry.definition, 1);

                Debug.LogWarning($"[PaperDoll] Экипировка провалилась, откат переноса");
            }

            Debug.Log($"[PaperDoll] Drop: {entry.definition.displayName} → {slotView.SlotType}: {(result ? "OK" : "FAIL")}");

            Refresh();
            UIServices.Get<InventoryUIManager>()?.RefreshAllPanels();
        }
    }
}