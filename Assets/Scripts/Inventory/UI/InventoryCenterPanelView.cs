using UnityEngine;
using UnityEngine.UI;

namespace UnityProject.Inventory
{
    /// <summary>
    /// Центральная панель инвентаря (1/3 экрана):
    /// - Кнопки над куклой (переключение вида, как в Mount and Blade)
    /// - Кукла персонажа со слотами экипировки
    /// - Placeholder под куклой (для будущей сетки или параметров персонажа)
    /// </summary>
    public class InventoryCenterPanelView : MonoBehaviour
    {
        [Header("Top Buttons Area")]
        [Tooltip("Контейнер для кнопок над куклой (переключение вида).")]
        [SerializeField] private Transform topButtonsContainer;

        [Header("Paper Doll")]
        [Tooltip("Компонент куклы персонажа со слотами экипировки.")]
        [SerializeField] private PlayerPaperDollView paperDollView;

        [Header("Bottom Placeholder")]
        [Tooltip("Placeholder под куклой для будущей сетки или параметров персонажа.")]
        [SerializeField] private GameObject bottomPlaceholder;

        private void Awake()
        {
            // Создаём заглушки кнопок, если контейнер пуст.
            if (topButtonsContainer != null && topButtonsContainer.childCount == 0)
            {
                CreatePlaceholderButtons();
            }
        }

        /// <summary>
        /// Создать заглушки кнопок над куклой (для будущей реализации переключения вида).
        /// </summary>
        private void CreatePlaceholderButtons()
        {
            // Пример: создаём 2-3 кнопки-заглушки.
            string[] buttonLabels = { "Инвентарь", "Экипировка", "Параметры" };

            foreach (var label in buttonLabels)
            {
                var buttonObj = new GameObject($"Button_{label}");
                buttonObj.transform.SetParent(topButtonsContainer, false);

                var button = buttonObj.AddComponent<Button>();
                var text = buttonObj.AddComponent<TMPro.TextMeshProUGUI>();
                text.text = label;
                text.alignment = TMPro.TextAlignmentOptions.Center;

                // Пока кнопки без логики (заглушка).
                button.onClick.AddListener(() =>
                {
                    Debug.Log($"[InventoryCenterPanelView] Кнопка '{label}' нажата (пока без логики)");
                });

                // Настраиваем размер кнопки.
                var rectTransform = buttonObj.GetComponent<RectTransform>();
                if (rectTransform != null)
                {
                    rectTransform.sizeDelta = new Vector2(120, 40);
                }
            }
        }

        /// <summary>
        /// Обновить отображение центральной панели (кукла и т.д.).
        /// </summary>
        public void Refresh()
        {
            if (paperDollView != null)
            {
                paperDollView.Refresh();
            }
        }
    }
}
