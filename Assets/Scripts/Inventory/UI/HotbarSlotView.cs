using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.EventSystems;
using System.Collections;

namespace UnityProject.Inventory
{
    public class HotbarSlotView : MonoBehaviour,
        IDropHandler,
        IPointerEnterHandler,
        IPointerExitHandler,
        IPointerClickHandler
    {
        [Header("Config")]
        [SerializeField] private int slotIndex = 0;

        [Header("UI References")]
        [SerializeField] private Image iconImage;
        [SerializeField] private TextMeshProUGUI keyLabel;
        [SerializeField] private Image backgroundImage;

        [Header("Colors")]
        [SerializeField] private Color normalColor = new Color(0.20f, 0.18f, 0.08f, 0.8f);
        [SerializeField] private Color hoverColor = new Color(0.40f, 0.35f, 0.10f, 0.9f);
        [SerializeField] private Color assignedColor = new Color(0.25f, 0.22f, 0.10f, 0.85f);
        [SerializeField] private Color activatedColor = new Color(0.9f, 0.7f, 0.1f, 1f);
        [SerializeField] private Color successFlashColor = new Color(0.2f, 0.7f, 0.2f, 1f);
        [SerializeField] private Color errorFlashColor = new Color(0.7f, 0.2f, 0.2f, 1f);

        private PlayerInventory playerInventory;

        public int SlotIndex => slotIndex;

        public void Initialize(PlayerInventory inventory)
        {
            playerInventory = inventory;

            if (keyLabel != null)
                keyLabel.text = (slotIndex + 1).ToString();

            if (backgroundImage == null)
                backgroundImage = GetComponent<Image>();

            Refresh();
        }

        public void Refresh()
        {
            if (playerInventory == null || playerInventory.Hotbar == null)
            {
                ClearIcon();
                return;
            }

            var item = playerInventory.GetHotbarItem(slotIndex);

            if (item == null || item.definition == null)
            {
                ClearIcon();
                return;
            }

            if (iconImage != null)
            {
                iconImage.enabled = item.definition.icon != null;
                iconImage.sprite = item.definition.icon;
            }

            if (backgroundImage != null)
                backgroundImage.color = assignedColor;
        }

        private void ClearIcon()
        {
            if (iconImage != null)
            {
                iconImage.enabled = false;
                iconImage.sprite = null;
            }
            if (backgroundImage != null)
                backgroundImage.color = normalColor;
        }

        public void OnDrop(PointerEventData eventData)
        {
            ResetVisual();

            if (eventData.pointerDrag == null) return;
            var draggedRow = eventData.pointerDrag.GetComponent<InventoryListRowView>();
            if (draggedRow == null) return;

            var entry = draggedRow.BoundEntry;
            if (entry.definition == null)
            {
                StartCoroutine(FlashColor(errorFlashColor));
                return;
            }

            if (playerInventory == null)
            {
                StartCoroutine(FlashColor(errorFlashColor));
                return;
            }

            // M-001: используем централизованный FindItem
            var source = draggedRow.SourceInventory;
            InventoryItem found = source?.MainInventory?.FindItem(entry.definition);

            playerInventory.AssignToHotbar(slotIndex, found);
            Debug.Log($"[HotbarSlot] Назначен {entry.definition.displayName} на слот {slotIndex + 1}");

            StartCoroutine(FlashColor(successFlashColor));
            Refresh();
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Right) return;
            if (playerInventory == null) return;

            var current = playerInventory.GetHotbarItem(slotIndex);
            if (current == null || current.definition == null) return;

            playerInventory.AssignToHotbar(slotIndex, null);
            Debug.Log($"[HotbarSlot] Слот {slotIndex + 1} очищен");
            Refresh();
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (eventData.pointerDrag == null) return;
            var draggedRow = eventData.pointerDrag.GetComponent<InventoryListRowView>();
            if (draggedRow == null) return;

            if (backgroundImage != null)
                backgroundImage.color = hoverColor;
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            ResetVisual();
        }

        private void ResetVisual()
        {
            Refresh();
        }

        public void SetHighlight(bool active)
        {
            if (backgroundImage == null) return;
            if (active)
                backgroundImage.color = activatedColor;
            else
                Refresh();
        }

        private IEnumerator FlashColor(Color color)
        {
            if (backgroundImage == null) yield break;

            backgroundImage.color = color;
            yield return new WaitForSeconds(0.3f);
            Refresh();
        }
    }
}