using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace UnityProject.Inventory
{
    /// <summary>
    /// Компонент, отображающий список предметов из источника инвентаря.
    /// Использует вертикальный ScrollView и генерирует строки из префаба.
    /// </summary>
    public class InventoryListView : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private ScrollRect scrollRect;
        [SerializeField] private RectTransform contentContainer;

        [Header("Prefabs")]
        [SerializeField] private InventoryListRowView rowPrefab;

        private IInventorySource currentSource;
        private InventoryPanelView ownerPanel;
        private readonly List<InventoryListRowView> spawnedRows = new List<InventoryListRowView>();

        /// <summary>
        /// Установить ссылку на родительскую панель (для обработки drop).
        /// </summary>
        public void SetOwnerPanel(InventoryPanelView panel)
        {
            ownerPanel = panel;
        }

        /// <summary>
        /// Установить источник инвентаря и обновить список.
        /// </summary>
        public void SetSource(IInventorySource source)
        {
            currentSource = source;
            Refresh();
        }

        /// <summary>
        /// Обновить отображение списка по текущему источнику.
        /// </summary>
        public void Refresh()
        {
            // Очищаем старые строки.
            foreach (var row in spawnedRows)
            {
                if (row != null)
                    Destroy(row.gameObject);
            }
            spawnedRows.Clear();

            if (currentSource == null || currentSource.MainInventory == null || rowPrefab == null)
                return;

            // Получаем отсортированный список записей.
            var entries = InventoryListModel.BuildList(currentSource.MainInventory);

            // Создаём строки для каждой записи.
            foreach (var entry in entries)
            {
                var row = Instantiate(rowPrefab, contentContainer);
                spawnedRows.Add(row);
                row.Setup(entry, this, currentSource);
            }

            // Обновляем размер контейнера для ScrollView.
            if (contentContainer != null)
            {
                float rowHeight = 64f; // Примерная высота строки (можно вычислить динамически).
                contentContainer.sizeDelta = new Vector2(
                    contentContainer.sizeDelta.x,
                    entries.Count * rowHeight);
            }
        }

        /// <summary>
        /// Обработка завершения drag строки.
        /// Проверяет, над какой панелью был drop, и вызывает перенос предметов.
        /// </summary>
        public void OnRowDragEnd(
            InventoryListRowView draggedRow,
            Vector2 screenPosition,
            IInventorySource sourceInventory,
            InventoryListEntry entry)
        {
            if (ownerPanel == null || entry.definition == null)
                return;

            // Проверяем, над какой панелью был drop через raycast.
            var results = new System.Collections.Generic.List<RaycastResult>();
            var eventData = new PointerEventData(EventSystem.current)
            {
                position = screenPosition
            };
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

            // Если drop был над другой панелью, выполняем перенос.
            if (targetPanel != null)
            {
                targetPanel.OnItemDropped(sourceInventory, entry.definition, entry.totalQuantity);
            }
        }
    }
}
