using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace UnityProject.Inventory
{
    /// <summary>
    /// Отображает содержимое AlloyStorage в раскрывающейся секции.
    /// </summary>
    public class AlloyStorageView : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private CollapsibleSectionView sectionView;
        [SerializeField] private RectTransform contentContainer;
        [SerializeField] private AlloyRowView rowPrefab;

        private AlloyStorage currentStorage;
        private InventoryPanelView ownerPanel;

        private readonly List<AlloyRowView> activeRows = new List<AlloyRowView>();
        private readonly List<AlloyRowView> pooledRows = new List<AlloyRowView>();

        public void Initialize(InventoryPanelView panel)
        {
            ownerPanel = panel;
        }

        public void SetStorage(AlloyStorage storage)
        {
            currentStorage = storage;
            Refresh();
        }

        public void Refresh()
        {
            ReturnAllToPool();

            if (currentStorage == null || rowPrefab == null)
            {
                UpdateSectionTitle(0);
                return;
            }

            var entries = currentStorage.Entries;
            int count = 0;

            for (int i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                if (string.IsNullOrEmpty(entry.code) || entry.massKg <= 0.0) continue;

                var row = GetRowFromPool();
                row.Setup(entry.code, entry.massKg, currentStorage, ownerPanel);
                count++;
            }

            UpdateSectionTitle(count);

            if (contentContainer != null)
                LayoutRebuilder.ForceRebuildLayoutImmediate(contentContainer);
        }

        private void UpdateSectionTitle(int count)
        {
            if (sectionView != null)
                sectionView.UpdateTitle("Сплавы", count);
        }

        private AlloyRowView GetRowFromPool()
        {
            AlloyRowView row;

            if (pooledRows.Count > 0)
            {
                int lastIndex = pooledRows.Count - 1;
                row = pooledRows[lastIndex];
                pooledRows.RemoveAt(lastIndex);
                row.gameObject.SetActive(true);
            }
            else
            {
                row = Instantiate(rowPrefab, contentContainer);
            }

            activeRows.Add(row);
            return row;
        }

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

        private void OnDestroy()
        {
            foreach (var row in activeRows)
                if (row != null) Destroy(row.gameObject);
            foreach (var row in pooledRows)
                if (row != null) Destroy(row.gameObject);
            activeRows.Clear();
            pooledRows.Clear();
        }
    }
}