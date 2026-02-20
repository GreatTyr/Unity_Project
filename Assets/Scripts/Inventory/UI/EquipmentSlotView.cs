using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.EventSystems;

namespace UnityProject.Inventory
{
    public class EquipmentSlotView : MonoBehaviour,
        IPointerClickHandler,
        IDropHandler,
        IPointerEnterHandler,
        IPointerExitHandler
    {
        [Header("Config")]
        [SerializeField] private EquipmentSlotType slotType = EquipmentSlotType.None;

        [Header("UI References")]
        [SerializeField] private Image iconImage;
        [SerializeField] private TextMeshProUGUI slotLabel;

        [Header("Background")]
        [SerializeField] private Image backgroundImage;

        [Header("Visual Feedback Colors")]
        [SerializeField] private Color normalColor = new Color(0.18f, 0.18f, 0.25f, 0.8f);
        [SerializeField] private Color validDropColor = new Color(0.2f, 0.6f, 0.2f, 0.9f);
        [SerializeField] private Color invalidDropColor = new Color(0.6f, 0.2f, 0.2f, 0.9f);
        [SerializeField] private Color occupiedDropColor = new Color(0.6f, 0.5f, 0.1f, 0.9f);

        private PlayerPaperDollView paperDollView;

        public EquipmentSlotType SlotType => slotType;

        public void Initialize(PlayerPaperDollView owner)
        {
            paperDollView = owner;

            if (slotLabel != null && string.IsNullOrEmpty(slotLabel.text))
                slotLabel.text = slotType.ToString().ToUpperInvariant();

            if (backgroundImage == null)
                backgroundImage = GetComponent<Image>();

            ResetVisual();
        }

        public void Refresh(EquipmentSlot slot)
        {
            bool hasItem = slot != null
                && slot.equippedItem != null
                && slot.equippedItem.definition != null;

            if (iconImage != null)
            {
                if (hasItem)
                {
                    iconImage.enabled = true;
                    iconImage.sprite = slot.equippedItem.definition.icon;
                }
                else
                {
                    iconImage.enabled = false;
                    iconImage.sprite = null;
                }
            }
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left) return;
            if (paperDollView == null) return;
            paperDollView.OnSlotClicked(this);
        }

        public void OnDrop(PointerEventData eventData)
        {
            ResetVisual();

            if (eventData.pointerDrag == null) return;
            var draggedRow = eventData.pointerDrag.GetComponent<InventoryListRowView>();
            if (draggedRow == null) return;

            var entry = draggedRow.BoundEntry;
            if (entry.definition == null) return;
            if (!entry.definition.isEquippable) return;
            if (entry.definition.equipmentSlotType != slotType) return;
            if (paperDollView == null) return;

            paperDollView.OnItemDroppedOnSlot(this, draggedRow);
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (eventData.pointerDrag == null) return;
            var draggedRow = eventData.pointerDrag.GetComponent<InventoryListRowView>();
            if (draggedRow == null) return;

            var entry = draggedRow.BoundEntry;
            if (entry.definition == null) return;
            if (backgroundImage == null) return;

            if (!entry.definition.isEquippable ||
                entry.definition.equipmentSlotType != slotType)
            {
                backgroundImage.color = invalidDropColor;
                return;
            }

            bool slotOccupied = false;
            var playerInv = PlayerLocator.Inventory;
            if (playerInv?.Equipment != null)
            {
                var s = playerInv.Equipment.GetSlot(slotType);
                slotOccupied = s?.equippedItem?.definition != null;
            }

            backgroundImage.color = slotOccupied ? occupiedDropColor : validDropColor;
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            ResetVisual();
        }

        private void ResetVisual()
        {
            if (backgroundImage != null)
                backgroundImage.color = normalColor;
        }
    }
}