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

        [Header("Filters")]
        [SerializeField] private Transform filtersContainer;
        [SerializeField] private GameObject filterButtonPrefab;

        [Header("List")]
        [SerializeField] private InventoryListView listView;

        private List<IInventorySource> sources = new List<IInventorySource>();
        private int selectedTabIndex = 0;
        private ItemCategory? currentFilter = null;
        private readonly List<Button> tabButtons = new List<Button>();
        private readonly List<Button> filterButtons = new List<Button>();

        // Цвета для активной/неактивной вкладки
        private static readonly Color ActiveTabColor = new Color(0.16f, 0.63f, 0.78f, 1f);
        private static readonly Color InactiveTabColor = new Color(0.24f, 0.24f, 0.31f, 1f);
        private static readonly Color ActiveFilterColor = new Color(0.16f, 0.63f, 0.78f, 1f);
        private static readonly Color InactiveFilterColor = new Color(0.24f, 0.24f, 0.31f, 1f);

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
            // Отписываемся от старых источников
            UnsubscribeFromSources();

            sources = newSources ?? new List<IInventorySource>();
            selectedTabIndex = 0;
            currentFilter = null;

            RefreshTabs();
            RefreshFilters();
            RefreshList();

            // Подписываемся на события изменений
            SubscribeToSources();
        }

        private void OnDestroy()
        {
            UnsubscribeFromSources();
        }

        // ========== ПОДПИСКА НА ИЗМЕНЕНИЯ ==========

        private void SubscribeToSources()
        {
            foreach (var source in sources)
            {
                if (source?.MainInventory != null)
                    source.MainInventory.OnChanged += OnInventoryChanged;
            }
        }

        private void UnsubscribeFromSources()
        {
            foreach (var source in sources)
            {
                if (source?.MainInventory != null)
                    source.MainInventory.OnChanged -= OnInventoryChanged;
            }
        }

        private void OnInventoryChanged()
        {
            RefreshList();
        }

        // ========== ВКЛАДКИ ==========

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

            UpdateTabVisuals();
        }

        private void OnTabClicked(int index)
        {
            if (index < 0 || index >= sources.Count) return;
            selectedTabIndex = index;
            currentFilter = null; // Сбрасываем фильтр при смене вкладки
            UpdateTabVisuals();
            UpdateFilterVisuals();
            RefreshList();
        }

        private void UpdateTabVisuals()
        {
            // Подсвечиваем активную вкладку
            int visibleIndex = 0;
            for (int i = 0; i < sources.Count; i++)
            {
                if (sources[i] == null || !sources[i].IsAvailable) continue;
                if (visibleIndex < tabButtons.Count)
                {
                    var img = tabButtons[visibleIndex].GetComponent<Image>();
                    if (img != null)
                        img.color = (i == selectedTabIndex) ? ActiveTabColor : InactiveTabColor;
                }
                visibleIndex++;
            }
        }

        // ========== ФИЛЬТРЫ ==========

        private void RefreshFilters()
        {
            foreach (var btn in filterButtons)
                if (btn != null) Destroy(btn.gameObject);
            filterButtons.Clear();

            if (filtersContainer == null) return;

            // Если нет prefab — создаём кнопки из tabButtonPrefab или вручную
            var prefab = filterButtonPrefab != null ? filterButtonPrefab : tabButtonPrefab;
            if (prefab == null) return;

            // Кнопка "Все"
            CreateFilterButton(prefab, "Все", null);

            // Кнопки по категориям
            CreateFilterButton(prefab, "Оружие", ItemCategory.Weapon);
            CreateFilterButton(prefab, "Броня", ItemCategory.Armor);
            CreateFilterButton(prefab, "Модули", ItemCategory.Module);
            CreateFilterButton(prefab, "Ресурсы", ItemCategory.Resource);
            CreateFilterButton(prefab, "Прочее", ItemCategory.Other);

            UpdateFilterVisuals();
        }

        private void CreateFilterButton(GameObject prefab, string label, ItemCategory? category)
        {
            var buttonObj = Instantiate(prefab, filtersContainer);
            var button = buttonObj.GetComponent<Button>();
            if (button == null) button = buttonObj.AddComponent<Button>();

            var text = buttonObj.GetComponentInChildren<TMPro.TextMeshProUGUI>();
            if (text != null)
            {
                text.text = label;
                text.fontSize = 11;
            }

            button.onClick.AddListener(() => OnFilterClicked(category));
            filterButtons.Add(button);
        }

        private void OnFilterClicked(ItemCategory? category)
        {
            currentFilter = category;
            UpdateFilterVisuals();
            RefreshList();
        }

        private void UpdateFilterVisuals()
        {
            // Индекс 0 = "Все" (null), 1 = Weapon, 2 = Armor, ...
            ItemCategory?[] filterOrder = { null, ItemCategory.Weapon, ItemCategory.Armor, ItemCategory.Module, ItemCategory.Resource, ItemCategory.Other };

            for (int i = 0; i < filterButtons.Count && i < filterOrder.Length; i++)
            {
                var img = filterButtons[i].GetComponent<Image>();
                if (img != null)
                    img.color = (currentFilter == filterOrder[i]) ? ActiveFilterColor : InactiveFilterColor;
            }
        }

        // ========== СПИСОК ==========

        public void RefreshList()
        {
            if (listView != null)
            {
                listView.SetOwnerPanel(this);
                listView.SetFilter(currentFilter);
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