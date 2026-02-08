using System.Collections.Generic;
using UnityEngine;

namespace UnityProject.Inventory
{
    /// <summary>
    /// Визуальное представление "куклы игрока" (экипировка по слотам).
    /// Этот компонент не хранит данные, а только читает их из PlayerInventory
    /// и обновляет связанные EquipmentSlotView.
    /// </summary>
    public class PlayerPaperDollView : MonoBehaviour
    {
        [Header("References")]
        [Tooltip("Инвентарь игрока, из которого берём данные об экипировке.")]
        [SerializeField] private PlayerInventory playerInventory;

        [Tooltip("Список UI-слотов, соответствующих разным типам экипировки.")]
        [SerializeField] private List<EquipmentSlotView> slotViews = new List<EquipmentSlotView>();

        private void Awake()
        {
            // Если ссылка на PlayerInventory не задана вручную – пробуем найти по тэгу.
            if (playerInventory == null)
            {
                var playerGo = GameObject.FindWithTag("Player");
                if (playerGo != null)
                    playerInventory = playerGo.GetComponent<PlayerInventory>();
            }

            if (playerInventory == null)
            {
                Debug.LogWarning("[PlayerPaperDollView] PlayerInventory не найден.");
            }

            // Инициализируем все EquipmentSlotView ссылкой на этот контроллер.
            foreach (var view in slotViews)
            {
                if (view != null)
                {
                    view.Initialize(this);
                }
            }
        }

        private void Start()
        {
            Refresh();
        }

        /// <summary>
        /// Обновить отображение всех слотов экипировки.
        /// Вызывается при открытии инвентаря и после операций экипировки/снятия.
        /// </summary>
        public void Refresh()
        {
            if (playerInventory == null || playerInventory.Equipment == null)
                return;

            foreach (var view in slotViews)
            {
                if (view == null) continue;

                var slot = playerInventory.Equipment.GetSlot(view.SlotType);
                view.Refresh(slot);
            }
        }

        /// <summary>
        /// Обработка клика по конкретному UI-слоту.
        /// По умолчанию – попытка снять предмет и вернуть его в основную сетку.
        /// </summary>
        public void OnSlotClicked(EquipmentSlotView view)
        {
            if (playerInventory == null || view == null)
                return;

            bool success = playerInventory.TryUnequipItem(view.SlotType);
            if (!success)
            {
                Debug.Log("[PlayerPaperDollView] OnSlotClicked: не удалось снять предмет из слота " +
                          view.SlotType);
            }

            // После любой операции – обновляем и куклу, и основную сетку.
            Refresh();

            // Обновляем все гриды инвентаря, если такие есть в сцене.
            var allGridViews = FindObjectsByType<InventoryGridView>(FindObjectsSortMode.None);
            foreach (var gridView in allGridViews)
            {
                gridView.Refresh();
            }
        }
    }
}

