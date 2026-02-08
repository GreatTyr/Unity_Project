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
                Debug.LogWarning("[InventoryGridView] PlayerInventory ?? ??????.");
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
        /// ?????????? drag-??????????? ?????? ????????.
        /// ????? ?? ????????? ??????? ?????? ? ???????? ???????
        /// ? ???????????? ??????????? ? ???? ?????? (PlayerInventory).
        /// </summary>
        public void OnItemDragEnd(InventoryItem item, Vector3 iconWorldPosition)
        {
            if (playerInventory == null || playerInventory.MainInventory == null)
            {
                Refresh();
                return;
            }

            var grid = playerInventory.MainInventory;

            // ????????? ??????? ?????? ?? ????/?????? ? ????????? ?????????? RectTransform.
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                rectTransform,
                RectTransformUtility.WorldToScreenPoint(null, iconWorldPosition),
                null,
                out Vector2 localPoint);

            // ???????? ? ??????? ????????? ? ??????? ? ????? ?????? ???? ??????????????.
            Vector2 halfSize = rectTransform.sizeDelta * 0.5f;
            float localX = localPoint.x + halfSize.x;
            float localY = localPoint.y + halfSize.y;

            // ????????? padding.
            float contentX = localX - padding.x;
            float contentY = localY + padding.y; // padding.y ?????????????

            int targetX = Mathf.FloorToInt(contentX / cellSize);
            int targetY = Mathf.FloorToInt(-contentY / cellSize); // ??? Y ?????????????

            // ???? ?????? ????? ?? ??????? ????? ? ?????? ?????????????? ??????? ?????????.
            if (targetX < 0 || targetY < 0 || targetX >= grid.Width || targetY >= grid.Height)
            {
                Refresh();
                return;
            }

            // ???????? ???????? ? PlayerInventory, ??????? ????????? ? ????????????.
            InventoryOperationResult result = playerInventory.TryMoveItemInMainGrid(
                item,
                targetX,
                targetY,
                item.rotated);

            if (!result.Success)
            {
                Debug.LogWarning($"[InventoryGridView] OnItemDragEnd: ??????????? ?? ???????: {result}");
            }

            Refresh();
        }
    }
}