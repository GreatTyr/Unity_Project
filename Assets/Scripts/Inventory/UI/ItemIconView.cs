using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.EventSystems;

namespace UnityProject.Inventory
{
    /// <summary>
    /// Визуальный элемент одного предмета в гриде.
    /// Поддерживает простой drag&drop внутри одного InventoryGridView.
    /// </summary>
    public class ItemIconView : MonoBehaviour,
        IPointerDownHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        [Header("UI")]
        [SerializeField] private Image iconImage;
        [SerializeField] private TextMeshProUGUI quantityText;

        private RectTransform rectTransform;
        private Canvas rootCanvas;
        private CanvasGroup canvasGroup;

        // Контекст
        private InventoryItem boundItem;
        private InventoryGridView ownerGridView;
        private float cellSize;

        // для drag
        private Vector2 dragOffset;

        private void Awake()
        {
            rectTransform = transform as RectTransform;
            canvasGroup = GetComponent<CanvasGroup>();
            if (canvasGroup == null)
                canvasGroup = gameObject.AddComponent<CanvasGroup>();

            rootCanvas = GetComponentInParent<Canvas>();
        }

        /// <summary>
        /// Настройка визуала и контекста.
        /// </summary>
        public void Setup(InventoryItem item, float cellSize, InventoryGridView owner)
        {
            this.boundItem = item;
            this.cellSize = cellSize;
            this.ownerGridView = owner;

            if (item == null || item.definition == null)
            {
                if (iconImage != null) iconImage.enabled = false;
                if (quantityText != null) quantityText.text = "";
                return;
            }

            if (iconImage != null)
            {
                iconImage.enabled = true;
                iconImage.sprite = item.definition.icon;
            }

            if (quantityText != null)
            {
                quantityText.text = item.definition.stackable && item.quantity > 1
                    ? item.quantity.ToString()
                    : "";
            }

            if (rectTransform != null)
            {
                float w = item.CurrentWidth * cellSize;
                float h = item.CurrentHeight * cellSize;
                rectTransform.sizeDelta = new Vector2(w, h);
            }
        }

        // =========================
        // Drag & Drop
        // =========================

        public void OnPointerDown(PointerEventData eventData)
        {
            // пока ничего
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (boundItem == null || ownerGridView == null) return;

            // делаем иконку полупрозрачной и на передний план
            canvasGroup.alpha = 0.7f;
            canvasGroup.blocksRaycasts = false; // чтобы не ловить собственные raycast

            // считаем оффсет от центра
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                rectTransform, eventData.position, eventData.pressEventCamera, out dragOffset);
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (boundItem == null || ownerGridView == null) return;

            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    rootCanvas.transform as RectTransform,
                    eventData.position,
                    eventData.pressEventCamera,
                    out Vector2 localPoint))
            {
                rectTransform.anchoredPosition = localPoint; // грубый вариант — просто следуем за мышью
            }
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (boundItem == null || ownerGridView == null) return;

            canvasGroup.alpha = 1f;
            canvasGroup.blocksRaycasts = true;

            // Просим GridView обработать окончание drag-а: вычислить целевую клетку и переместить предмет
            ownerGridView.OnItemDragEnd(boundItem, rectTransform.position);

            // После этого GridView сам вызовет Refresh() и перерисует всё
        }
    }
}