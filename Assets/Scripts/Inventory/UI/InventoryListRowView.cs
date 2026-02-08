using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.EventSystems;

namespace UnityProject.Inventory
{
    /// <summary>
    /// Визуальное представление одной строки в списке инвентаря.
    /// Отображает иконку, название и количество предмета.
    /// Поддерживает drag & drop для переноса между панелями.
    /// </summary>
    public class InventoryListRowView : MonoBehaviour,
        IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        [Header("UI References")]
        [SerializeField] private Image iconImage;
        [SerializeField] private TextMeshProUGUI nameText;
        [SerializeField] private TextMeshProUGUI quantityText;

        private RectTransform rectTransform;
        private Canvas rootCanvas;
        private CanvasGroup canvasGroup;

        // Контекст
        private InventoryListEntry boundEntry;
        private InventoryListView ownerListView;
        private IInventorySource sourceInventory;

        // Для drag
        private Vector2 dragOffset;
        private GameObject dragPreview;

        private void Awake()
        {
            rectTransform = transform as RectTransform;
            canvasGroup = GetComponent<CanvasGroup>();
            if (canvasGroup == null)
                canvasGroup = gameObject.AddComponent<CanvasGroup>();

            rootCanvas = GetComponentInParent<Canvas>();
        }

        /// <summary>
        /// Настройка визуала и контекста строки.
        /// </summary>
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
                iconImage.enabled = true;
                iconImage.sprite = entry.definition.icon;
            }

            if (nameText != null)
            {
                nameText.text = entry.definition.displayName;
            }

            if (quantityText != null)
            {
                quantityText.text = entry.totalQuantity > 1
                    ? $"x{entry.totalQuantity}"
                    : "";
            }
        }

        // =========================
        // Drag & Drop
        // =========================

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (boundEntry.definition == null || ownerListView == null || sourceInventory == null)
                return;

            // Делаем строку полупрозрачной и создаём визуальный превью для drag.
            canvasGroup.alpha = 0.5f;
            canvasGroup.blocksRaycasts = false;

            // Создаём простой превью (можно улучшить позже).
            if (dragPreview == null && iconImage != null && iconImage.sprite != null)
            {
                dragPreview = new GameObject("DragPreview");
                dragPreview.transform.SetParent(rootCanvas.transform, false);
                var previewImage = dragPreview.AddComponent<Image>();
                previewImage.sprite = iconImage.sprite;
                previewImage.raycastTarget = false;
                var previewRect = dragPreview.GetComponent<RectTransform>();
                previewRect.sizeDelta = new Vector2(64, 64);
            }

            // Сохраняем offset для плавного следования за курсором.
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                rectTransform, eventData.position, eventData.pressEventCamera, out dragOffset);
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (dragPreview == null) return;

            // Перемещаем превью за курсором.
            Vector2 screenPoint = eventData.position;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                rootCanvas.transform as RectTransform,
                screenPoint,
                eventData.pressEventCamera,
                out Vector2 localPoint);

            dragPreview.transform.localPosition = localPoint;
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            canvasGroup.alpha = 1f;
            canvasGroup.blocksRaycasts = true;

            // Уничтожаем превью.
            if (dragPreview != null)
            {
                Destroy(dragPreview);
                dragPreview = null;
            }

            // Проверяем, над какой панелью был drop.
            if (ownerListView != null)
            {
                ownerListView.OnRowDragEnd(this, eventData.position, sourceInventory, boundEntry);
            }
        }
    }
}
