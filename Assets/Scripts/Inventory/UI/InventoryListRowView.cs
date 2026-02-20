using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.EventSystems;

namespace UnityProject.Inventory
{
    public class InventoryListRowView : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler,
        IPointerClickHandler,
        IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        [Header("UI References")]
        [SerializeField] private Image iconImage;
        [SerializeField] private TextMeshProUGUI nameText;
        [SerializeField] private TextMeshProUGUI quantityText;

        private RectTransform rectTransform;
        private Canvas rootCanvas;
        private CanvasGroup canvasGroup;

        private InventoryListEntry boundEntry;
        private InventoryListView ownerListView;
        private IInventorySource sourceInventory;

        private GameObject dragPreview;
        private bool isDragging;

        public InventoryListEntry BoundEntry => boundEntry;
        public IInventorySource SourceInventory => sourceInventory;

        private void Awake()
        {
            rectTransform = transform as RectTransform;
            canvasGroup = GetComponent<CanvasGroup>();
            if (canvasGroup == null)
                canvasGroup = gameObject.AddComponent<CanvasGroup>();

            rootCanvas = GetComponentInParent<Canvas>();
        }

        public void Setup(InventoryListEntry entry, InventoryListView owner, IInventorySource source)
        {
            boundEntry = entry;
            ownerListView = owner;
            sourceInventory = source;

            if (entry.definition == null)
            {
                if (iconImage != null) iconImage.enabled = false;
                if (nameText != null) nameText.text = "";
                if (quantityText != null) quantityText.text = "";
                return;
            }

            if (iconImage != null)
            {
                iconImage.enabled = entry.definition.icon != null;
                iconImage.sprite = entry.definition.icon;
            }

            if (nameText != null)
                nameText.text = entry.definition.displayName;

            if (quantityText != null)
                quantityText.text = entry.totalQuantity > 1 ? $"x{entry.totalQuantity}" : "";
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (isDragging) return;

            if (eventData.button == PointerEventData.InputButton.Right)
            {
                OpenContextMenu(eventData.position);
            }
        }

        private void OpenContextMenu(Vector2 screenPos)
        {
            if (boundEntry.definition == null) return;

            var menu = InventoryContextMenuUI.Instance;
            if (menu == null)
            {
                Debug.LogWarning("[InventoryListRowView] InventoryContextMenuUI.Instance == null.");
                return;
            }

            menu.Show(boundEntry, sourceInventory, screenPos, PlayerLocator.Inventory);
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (boundEntry.definition == null || ownerListView == null || sourceInventory == null)
                return;

            isDragging = true;
            canvasGroup.alpha = 0.5f;
            canvasGroup.blocksRaycasts = false;

            if (iconImage != null && iconImage.sprite != null)
            {
                dragPreview = new GameObject("DragPreview");
                dragPreview.transform.SetParent(rootCanvas.transform, false);
                var previewImage = dragPreview.AddComponent<Image>();
                previewImage.sprite = iconImage.sprite;
                previewImage.raycastTarget = false;
                previewImage.color = new Color(1f, 1f, 1f, 0.7f);
                var previewRect = dragPreview.GetComponent<RectTransform>();
                previewRect.sizeDelta = new Vector2(48, 48);
            }
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (dragPreview == null) return;

            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                rootCanvas.transform as RectTransform,
                eventData.position,
                eventData.pressEventCamera,
                out Vector2 localPoint);

            dragPreview.transform.localPosition = localPoint;
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            isDragging = false;
            canvasGroup.alpha = 1f;
            canvasGroup.blocksRaycasts = true;

            if (dragPreview != null)
            {
                Destroy(dragPreview);
                dragPreview = null;
            }

            if (ownerListView != null)
            {
                ownerListView.OnRowDragEnd(this, eventData.position, sourceInventory, boundEntry);
            }
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (boundEntry.definition == null) return;
            ItemTooltipUI.Instance?.Show(boundEntry.definition);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            ItemTooltipUI.Instance?.Hide();
        }
    }
}