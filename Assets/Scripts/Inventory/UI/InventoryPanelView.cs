using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace UnityProject.Inventory
{
    public class InventoryPanelView : MonoBehaviour
    {
        [Header("Tabs")]
        [SerializeField] private Transform tabsContainer;
        [SerializeField] private GameObject tabButtonPrefab;

        [Header("List")]
        [SerializeField] private InventoryListView listView;

        // НЕ SerializeField — Unity не сериализует интерфейсы.
        // Инициализируется программно через SetSources().
        private List<IInventorySource> sources = new List<IInventorySource>();

        private int selectedTabIndex = 0;
        private readonly List<Button> tabButtons = new List<Button>();

        public InventoryPanelView OtherPanel { get; set; }

        public IInventorySource CurrentSource
        {
            get
            {
                if (selectedTabIndex >= 0 && selectedTabIndex < sources.Count)
                    return sources[selectedTabIndex];
                return null;
            }
        }

        public void SetSources(List<IInventorySource> newSources)
        {
            sources = newSources ?? new List<IInventorySource>();
            selectedTabIndex = 0;
            RefreshTabs();
            RefreshList();
        }

        private void RefreshTabs()
        {
            foreach (var btn in tabButtons)
                if (btn != null) Destroy(btn.gameObject);
            tabButtons.Clear();

            if (tabsContainer == null || tabButtonPrefab == null) return;

            for (int i = 0; i < sources.Count; i++)
            {
                var source = sources[i];
                if (source == null || !source.IsAvailable) continue;

                var buttonObj = Instantiate(tabButtonPrefab, tabsContainer);
                var button = buttonObj.GetComponent<Button>();
                if (button == null) button = buttonObj.AddComponent<Button>();

                var text = buttonObj.GetComponentInChildren<TMPro.TextMeshProUGUI>();
                if (text != null) text.text = source.DisplayName;

                int index = i;
                button.onClick.AddListener(() => OnTabClicked(index));
                tabButtons.Add(button);
            }
        }

        private void OnTabClicked(int index)
        {
            if (index < 0 || index >= sources.Count) return;
            selectedTabIndex = index;
            RefreshTabs();
            RefreshList();
        }

        public void RefreshList()
        {
            if (listView != null)
            {
                listView.SetOwnerPanel(this);
                listView.SetSource(CurrentSource);
            }
        }

        public void OnItemDropped(
            IInventorySource sourceInventory,
            ItemDefinition definition,
            int quantity)
        {
            if (CurrentSource == null || sourceInventory == null
                || definition == null || CurrentSource == sourceInventory)
                return;

            var result = InventoryTransferService.TransferItems(
                sourceInventory, CurrentSource, definition, quantity);

            if (!result.Success)
                Debug.LogWarning($"[InventoryPanelView] Перенос: {result.Message}");

            RefreshList();
            OtherPanel?.RefreshList();
        }
    }
}