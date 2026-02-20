using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace UnityProject.Inventory
{
    /// <summary>
    /// Отображает список предметов инвентаря в ScrollView.
    /// M-008: Использует object pooling вместо Destroy/Instantiate при каждом Refresh.
    /// </summary>
    public class InventoryListView : MonoBehaviour
    {
        [SerializeField] private RectTransform contentContainer;
        [SerializeField] private InventoryListRowView rowPrefab;

        private IInventorySource currentSource;
        private InventoryPanelView ownerPanel;
        private ItemCategory? currentFilter = null;

        // Пул строк: activeRows — видимые, pooledRows — скрытые и готовые к переиспользованию
        private readonly List<InventoryListRowView> activeRows = new List<InventoryListRowView>();
        private readonly List<InventoryListRowView> pooledRows = new List<InventoryListRowView>();

        public void SetOwnerPanel(InventoryPanelView panel) => ownerPanel = panel;

        public void SetSource(IInventorySource source)
        {
            currentSource = source;
            Refresh();
        }

        public void SetFilter(ItemCategory? filter)
        {
            currentFilter = filter;
            Refresh();
        }

        public void Refresh()
        {
            // Возвращаем все активные строки в пул
            ReturnAllToPool();

            if (currentSource?.MainInventory == null || rowPrefab == null) return;

            var entries = InventoryListModel.BuildList(currentSource.MainInventory, currentFilter);

            foreach (var entry in entries)
            {
                var row = GetRowFromPool();
                row.Setup(entry, this, currentSource);
            }

            // Перестраиваем layout
            if (contentContainer != null)
                LayoutRebuilder.ForceRebuildLayoutImmediate(contentContainer);
        }

        /// <summary>
        /// Получить строку из пула или создать новую.
        /// </summary>
        private InventoryListRowView GetRowFromPool()
        {
            InventoryListRowView row;

            if (pooledRows.Count > 0)
            {
                // Берём последнюю из пула (быстрее чем из начала)
                int lastIndex = pooledRows.Count - 1;
                row = pooledRows[lastIndex];
                pooledRows.RemoveAt(lastIndex);
                row.gameObject.SetActive(true);
            }
            else
            {
                // Пул пуст — создаём новую строку
                row = Instantiate(rowPrefab, contentContainer);
            }

            activeRows.Add(row);
            return row;
        }

        /// <summary>
        /// Вернуть все активные строки в пул (деактивировать, не уничтожать).
        /// </summary>
        private void ReturnAllToPool()
        {
            foreach (var row in activeRows)
            {
                if (row == null) continue;
                row.gameObject.SetActive(false);
                pooledRows.Add(row);
            }
            activeRows.Clear();
        }

        /// <summary>
        /// Очистить пул полностью (при уничтожении компонента).
        /// </summary>
        private void OnDestroy()
        {
            foreach (var row in activeRows)
                if (row != null) Destroy(row.gameObject);

            foreach (var row in pooledRows)
                if (row != null) Destroy(row.gameObject);

            activeRows.Clear();
            pooledRows.Clear();
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