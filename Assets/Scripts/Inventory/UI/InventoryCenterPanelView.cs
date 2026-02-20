using System.Collections.Generic;
using UnityEngine;

namespace UnityProject.Inventory
{
    /// <summary>
    /// Центральная панель инвентаря (1/3 экрана):
    /// - Кнопки над куклой (переключение вида)
    /// - Кукла персонажа со слотами экипировки
    /// - Хотбар (4 слота) под куклой
    /// - Placeholder под хотбаром (для будущих статов)
    ///
    /// Объединяет старую версию (topButtonsContainer, bottomPlaceholder)
    /// и новую (hotbarSlots, playerInventory).
    /// Кнопки-заглушки больше НЕ создаются программно.
    /// </summary>
    public class InventoryCenterPanelView : MonoBehaviour
    {
        [Header("Top Buttons Area")]
        [Tooltip("Контейнер для кнопок над куклой (Инвентарь / Экипировка / Параметры).")]
        [SerializeField] private Transform topButtonsContainer;

        [Header("Paper Doll")]
        [Tooltip("Компонент куклы персонажа со слотами экипировки.")]
        [SerializeField] private PlayerPaperDollView paperDollView;

        [Header("Hotbar")]
        [Tooltip("Список UI-слотов хотбара (4 штуки).")]
        [SerializeField] private List<HotbarSlotView> hotbarSlots = new List<HotbarSlotView>();

        [Header("Bottom Placeholder")]
        [Tooltip("Placeholder под хотбаром для будущих статов персонажа.")]
        [SerializeField] private GameObject bottomPlaceholder;

        [Header("Player Reference")]
        [Tooltip("Ссылка на PlayerInventory. Если не назначена — ищет по тегу Player.")]
        [SerializeField] private PlayerInventory playerInventory;

        private void Awake()
        {
            if (playerInventory == null)
            {
                var playerGo = GameObject.FindWithTag("Player");
                if (playerGo != null)
                    playerInventory = playerGo.GetComponent<PlayerInventory>();
            }

            // Инициализируем хотбар-слоты
            for (int i = 0; i < hotbarSlots.Count; i++)
            {
                if (hotbarSlots[i] != null)
                    hotbarSlots[i].Initialize(playerInventory);
            }
        }

        /// <summary>
        /// Обновить всё: кукла + хотбар.
        /// </summary>
        public void Refresh()
        {
            paperDollView?.Refresh();

            foreach (var slot in hotbarSlots)
                slot?.Refresh();
        }

        /// <summary>
        /// Получить HotbarSlotView по индексу (для внешнего доступа).
        /// </summary>
        public HotbarSlotView GetHotbarSlotView(int index)
        {
            if (index < 0 || index >= hotbarSlots.Count) return null;
            return hotbarSlots[index];
        }
    }
}