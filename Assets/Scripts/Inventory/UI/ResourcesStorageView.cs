using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace UnityProject.Inventory
{
    /// <summary>
    /// ќтображает содержимое ResourcesStorage в раскрывающейс€ секции.
    /// Ёнерги€ Ч отдельна€ строка вверху. –есурсы Ч только ненулевые, по типам.
    /// </summary>
    public class ResourcesStorageView : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private CollapsibleSectionView sectionView;
        [SerializeField] private RectTransform contentContainer;
        [SerializeField] private ResourceRowView rowPrefab;

        private ResourcesStorage currentStorage;
        private InventoryPanelView ownerPanel;

        private readonly List<ResourceRowView> activeRows = new List<ResourceRowView>();
        private readonly List<ResourceRowView> pooledRows = new List<ResourceRowView>();

        public void Initialize(InventoryPanelView panel)
        {
            ownerPanel = panel;
        }

        public void SetStorage(ResourcesStorage storage)
        {
            currentStorage = storage;
            Refresh();
        }

        public void Refresh()
        {
            ReturnAllToPool();

            if (currentStorage == null || rowPrefab == null)
            {
                UpdateSectionTitle(0, 0);
                return;
            }

            int nonZeroCount = 0;

            // 1. —трока энергии (всегда показываем если > 0)
            if (currentStorage.EnergyUnits > 0)
            {
                var energyRow = GetRowFromPool();
                energyRow.SetupEnergy(currentStorage.EnergyUnits, currentStorage, ownerPanel);
                nonZeroCount++;
            }

            // 2. –есурсы по типам (P, F, M, B, C, N), внутри по тирам T1-T10
            for (int typeIdx = 0; typeIdx < ResourcesStorage.ResourceTypesCount; typeIdx++)
            {
                for (int tier = 0; tier < ResourcesStorage.TiersPerType; tier++)
                {
                    int index = typeIdx * ResourcesStorage.TiersPerType + tier;
                    var resIndex = (ResourcesStorage.ResourceIndex)index;
                    long grams = currentStorage.GetGrams(resIndex);

                    if (grams <= 0) continue;

                    var row = GetRowFromPool();
                    row.SetupResource(resIndex, grams, currentStorage, ownerPanel);
                    nonZeroCount++;
                }
            }

            UpdateSectionTitle(nonZeroCount, ResourcesStorage.ResourceTiersCount + 1);

            if (contentContainer != null)
                LayoutRebuilder.ForceRebuildLayoutImmediate(contentContainer);
        }

        private void UpdateSectionTitle(int count, int total)
        {
            if (sectionView != null)
                sectionView.UpdateTitle("–есурсы", count, total);
        }

        private ResourceRowView GetRowFromPool()
        {
            ResourceRowView row;

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