using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace UnityProject.Inventory
{
    public class InventoryListView : MonoBehaviour
    {
        [SerializeField] private ScrollRect scrollRect;
        [SerializeField] private RectTransform contentContainer;
        [SerializeField] private InventoryListRowView rowPrefab;

        private IInventorySource currentSource;
        private InventoryPanelView ownerPanel;
        private ItemCategory? currentFilter = null;
        private readonly List<InventoryListRowView> spawnedRows = new List<InventoryListRowView>();

        public void SetOwnerPanel(InventoryPanelView panel) => ownerPanel = panel;

        public void SetSource(IInventorySource source)
        {
            currentSource = source;
            Refresh();
        }

        /// <summary>
        /// Установить фильтр категории. null = показать все.
        /// </summary>
        public void SetFilter(ItemCategory? filter)
        {
            currentFilter = filter;
            Refresh();
        }

        public void Refresh()
        {
            // Очищаем старые строки
            foreach (var row in spawnedRows)
                if (row != null) Destroy(row.gameObject);
            spawnedRows.Clear();

            if (currentSource?.MainInventory == null || rowPrefab == null) return;

            // Строим список С УЧЁТОМ фильтра
            var entries = InventoryListModel.BuildList(currentSource.MainInventory, currentFilter);

            foreach (var entry in entries)
            {
                var row = Instantiate(rowPrefab, contentContainer);
                spawnedRows.Add(row);
                row.Setup(entry, this, currentSource);
            }

            // НЕ трогаем sizeDelta вручную!
            // ContentSizeFitter + VerticalLayoutGroup на Content сами рассчитают высоту.
            // Принудительно перестраиваем Layout, чтобы строки встали правильно.
            if (contentContainer != null)
            {
                LayoutRebuilder.ForceRebuildLayoutImmediate(contentContainer);
            }
        }

        public void OnRowDragEnd(
            InventoryListRowView draggedRow,
            Vector2 screenPosition,
            IInventorySource sourceInventory,
            InventoryListEntry entry)
        {
            if (ownerPanel == null || entry.definition == null) return;

            var results = new List<RaycastResult>();
            var eventData = new PointerEventData(EventSystem.current)
            { position = screenPosition };
            EventSystem.current.RaycastAll(eventData, results);

            InventoryPanelView targetPanel = null;
            foreach (var result in results)
            {
                var panel = result.gameObject.GetComponentInParent<InventoryPanelView>();
                if (panel != null && panel != ownerPanel)
                {
                    targetPanel = panel;
                    break;
                }
            }

            if (targetPanel != null)
                targetPanel.OnItemDropped(sourceInventory, entry.definition, entry.totalQuantity);
        }
    }
}