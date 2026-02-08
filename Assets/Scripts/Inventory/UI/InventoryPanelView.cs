using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace UnityProject.Inventory
{
    /// <summary>
    /// Панель инвентаря с вкладками и списком предметов (левая или правая 1/3 экрана).
    /// Управляет переключением между источниками (Player, Pepelac, Отряд, База, Объект)
    /// и отображает список предметов выбранного источника.
    /// </summary>
    public class InventoryPanelView : MonoBehaviour
    {
        [Header("Tabs")]
        [SerializeField] private Transform tabsContainer;
        [SerializeField] private GameObject tabButtonPrefab;

        [Header("List")]
        [SerializeField] private InventoryListView listView;

        [Header("Sources")]
        [Tooltip("Список источников инвентаря для вкладок (Player, Pepelac, Отряд, База, Объект).")]
        [SerializeField] private List<IInventorySource> sources = new List<IInventorySource>();

        private int selectedTabIndex = 0;
        private readonly List<Button> tabButtons = new List<Button>();

        /// <summary>
        /// Другая панель для переноса предметов (левая ↔ правая).
        /// </summary>
        public InventoryPanelView OtherPanel { get; set; }

        /// <summary>
        /// Текущий выбранный источник инвентаря.
        /// </summary>
        public IInventorySource CurrentSource
        {
            get
            {
                if (selectedTabIndex >= 0 && selectedTabIndex < sources.Count)
                    return sources[selectedTabIndex];
                return null;
            }
        }

        private void Awake()
        {
            // Если источники не заданы в инспекторе, создаём заглушки.
            if (sources == null || sources.Count == 0)
            {
                InitializeDefaultSources();
            }
        }

        private void Start()
        {
            RefreshTabs();
            RefreshList();
        }

        /// <summary>
        /// Инициализация источников по умолчанию (если не заданы в инспекторе).
        /// </summary>
        private void InitializeDefaultSources()
        {
            sources = new List<IInventorySource>
            {
                new PlayerInventorySource(FindFirstObjectByType<PlayerInventory>()),
                new PepelacInventorySource(),
                new SquadInventorySource(),
                new BaseInventorySource(),
                new ObjectInventorySource()
            };
        }

        /// <summary>
        /// Установить список источников (вызывается из InventoryUIManager).
        /// </summary>
        public void SetSources(List<IInventorySource> newSources)
        {
            sources = newSources ?? new List<IInventorySource>();
            RefreshTabs();
            RefreshList();
        }

        /// <summary>
        /// Обновить отображение вкладок.
        /// </summary>
        private void RefreshTabs()
        {
            // Очищаем старые кнопки.
            foreach (var button in tabButtons)
            {
                if (button != null)
                    Destroy(button.gameObject);
            }
            tabButtons.Clear();

            if (tabsContainer == null || tabButtonPrefab == null)
                return;

            // Создаём кнопки для каждого доступного источника.
            for (int i = 0; i < sources.Count; i++)
            {
                var source = sources[i];
                if (source == null || !source.IsAvailable)
                    continue;

                var buttonObj = Instantiate(tabButtonPrefab, tabsContainer);
                var button = buttonObj.GetComponent<Button>();
                if (button == null)
                    button = buttonObj.AddComponent<Button>();

                // Настраиваем текст кнопки.
                var text = buttonObj.GetComponentInChildren<TMPro.TextMeshProUGUI>();
                if (text != null)
                    text.text = source.DisplayName;

                // Подписываемся на клик.
                int index = i; // Замыкание для индекса.
                button.onClick.AddListener(() => OnTabClicked(index));

                // Выделяем активную вкладку.
                if (i == selectedTabIndex)
                {
                    // Можно добавить визуальное выделение (изменить цвет, добавить рамку и т.п.).
                }

                tabButtons.Add(button);
            }
        }

        /// <summary>
        /// Обработка клика по вкладке.
        /// </summary>
        private void OnTabClicked(int index)
        {
            if (index < 0 || index >= sources.Count)
                return;

            selectedTabIndex = index;
            RefreshTabs(); // Обновляем выделение.
            RefreshList(); // Обновляем список.
        }

        /// <summary>
        /// Обновить отображение списка предметов.
        /// </summary>
        public void RefreshList()
        {
            var source = CurrentSource;
            if (listView != null)
            {
                listView.SetOwnerPanel(this);
                listView.SetSource(source);
            }
        }

        /// <summary>
        /// Обработка drop предмета из другой панели.
        /// Вызывается из InventoryListView при завершении drag.
        /// </summary>
        public void OnItemDropped(
            IInventorySource sourceInventory,
            ItemDefinition definition,
            int quantity)
        {
            if (CurrentSource == null || sourceInventory == null || definition == null)
                return;

            // Если drop в ту же панель, ничего не делаем.
            if (CurrentSource == sourceInventory)
                return;

            // Выполняем перенос через сервис.
            var result = InventoryTransferService.TransferItems(
                sourceInventory,
                CurrentSource,
                definition,
                quantity);

            if (!result.Success)
            {
                Debug.LogWarning($"[InventoryPanelView] Перенос не удался: {result.Message}");
            }

            // Обновляем обе панели.
            RefreshList();
            if (OtherPanel != null)
                OtherPanel.RefreshList();
        }
    }
}
