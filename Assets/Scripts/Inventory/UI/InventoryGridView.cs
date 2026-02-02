using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

namespace UnityProject.Inventory
{
    public class InventoryGridView : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private PlayerInventory playerInventory;

        [Header("Grid Settings")]
        [SerializeField] private float cellSize = 64f;
        [SerializeField] private Vector2 padding = new Vector2(10f, -10f);

        [Header("Prefabs")]
        [SerializeField] private ItemIconView itemIconPrefab;

        private RectTransform rectTransform;
        private readonly List<ItemIconView> spawnedIcons = new List<ItemIconView>();

        private void Awake()
        {
            rectTransform = GetComponent<RectTransform>();

            if (playerInventory == null)
            {
                var playerGo = GameObject.FindWithTag("Player");
                if (playerGo != null)
                    playerInventory = playerGo.GetComponent<PlayerInventory>();
            }

            if (playerInventory == null)
            {
                Debug.LogWarning("[InventoryGridView] PlayerInventory не найден.");
            }
        }

        private void Start()
        {
            Refresh();
        }

        public void Refresh()
        {
            foreach (var icon in spawnedIcons)
            {
                if (icon != null)
                    Destroy(icon.gameObject);
            }
            spawnedIcons.Clear();

            if (playerInventory == null || playerInventory.MainInventory == null || itemIconPrefab == null)
                return;

            var grid = playerInventory.MainInventory;

            if (rectTransform != null)
            {
                float w = grid.Width * cellSize + Mathf.Abs(padding.x) * 2f;
                float h = grid.Height * cellSize + Mathf.Abs(padding.y) * 2f;
                rectTransform.sizeDelta = new Vector2(w, h);
            }

            foreach (var item in grid.Items)
            {
                var icon = Object.Instantiate(itemIconPrefab, rectTransform);
                spawnedIcons.Add(icon);

                icon.Setup(item, cellSize, this);

                var iconRect = icon.transform as RectTransform;
                if (iconRect != null)
                {
                    float x = padding.x + item.x * cellSize + (item.CurrentWidth * cellSize) / 2f;
                    float y = padding.y - item.y * cellSize - (item.CurrentHeight * cellSize) / 2f;

                    iconRect.anchorMin = new Vector2(0, 1);
                    iconRect.anchorMax = new Vector2(0, 1);
                    iconRect.pivot = new Vector2(0.5f, 0.5f);
                    iconRect.anchoredPosition = new Vector2(x, y);
                }
            }
        }

        /// <summary>
        /// ¬ызываетс€ ItemIconView при окончании drag.
        /// screenPosition Ч мирова€ позици€ иконки или screenPoint.
        /// </summary>
        public void OnItemDragEnd(InventoryItem item, Vector3 iconWorldPosition)
        {
            if (playerInventory == null || playerInventory.MainInventory == null) return;
            var grid = playerInventory.MainInventory;

            // ѕереводим мировую позицию в локальную в пространстве rectTransform
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                rectTransform,
                RectTransformUtility.WorldToScreenPoint(null, iconWorldPosition),
                null,
                out Vector2 localPoint);

            // —мещаем локальную точку относительно origin (левый верх)
            // rectTransform.pivot может быть не (0,1), но мы ставим anchor/pivot у иконок в (0.5, 0.5)
            // ƒл€ простоты считаем, что rectTransform.anchor/pivot остаютс€ по умолчанию (0.5,0.5),
            // смещение поправим через sizeDelta/2.
            Vector2 halfSize = rectTransform.sizeDelta * 0.5f;
            float localX = localPoint.x + halfSize.x;
            float localY = localPoint.y + halfSize.y;

            // ¬ычитаем padding
            float contentX = localX - padding.x;
            float contentY = localY + padding.y; // padding.y отрицательный

            int targetX = Mathf.FloorToInt(contentX / cellSize);
            int targetY = Mathf.FloorToInt(-contentY / cellSize); // y вниз

            // ѕровер€ем границы
            if (targetX < 0 || targetY < 0 || targetX >= grid.Width || targetY >= grid.Height)
            {
                // ¬ышли за пределы Ч просто перерисуем и вернЄм иконку на прежнее место
                Refresh();
                return;
            }

            // ѕробуем переместить предмет в новую позицию
            // ¬ажно: временно убираем предмет из списка, чтобы он не пересекал сам себ€
            grid.RemoveItem(item);
            bool canPlace = grid.CanPlaceItem(item, targetX, targetY, item.rotated);
            if (canPlace)
            {
                grid.TryAddItem(item, targetX, targetY, item.rotated);
            }
            else
            {
                // возвращаем на старую позицию
                grid.TryAddItem(item, item.x, item.y, item.rotated);
            }

            Refresh();
        }
    }
}