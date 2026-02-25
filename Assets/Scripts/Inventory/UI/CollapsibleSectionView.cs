using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace UnityProject.Inventory
{
    /// <summary>
    /// Раскрывающаяся секция: кнопка-заголовок + контейнер контента.
    /// При клике на заголовок контент показывается/скрывается.
    /// </summary>
    public class CollapsibleSectionView : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private Button headerButton;
        [SerializeField] private TextMeshProUGUI headerText;
        [SerializeField] private GameObject contentContainer;
        [SerializeField] private LayoutElement contentLayoutElement;

        [Header("Settings")]
        [SerializeField] private string sectionTitle = "Секция";
        [SerializeField] private float expandedMaxHeight = 200f;
        [SerializeField] private bool startExpanded = false;

        private bool isExpanded;
        private string expandedArrow = "▼";
        private string collapsedArrow = "▶";

        public bool IsExpanded => isExpanded;

        private void Awake()
        {
            if (headerButton != null)
                headerButton.onClick.AddListener(Toggle);

            SetExpanded(startExpanded, immediate: true);
        }

        private void OnDestroy()
        {
            if (headerButton != null)
                headerButton.onClick.RemoveListener(Toggle);
        }

        public void Toggle()
        {
            SetExpanded(!isExpanded);
        }

        public void SetExpanded(bool expanded, bool immediate = false)
        {
            isExpanded = expanded;

            if (contentContainer != null)
                contentContainer.SetActive(isExpanded);

            if (contentLayoutElement != null)
            {
                contentLayoutElement.preferredHeight = isExpanded ? expandedMaxHeight : 0f;
                contentLayoutElement.minHeight = isExpanded ? 50f : 0f;
            }

            UpdateHeaderText();
        }

        /// <summary>
        /// Обновить заголовок с количеством элементов.
        /// Вызывается из ResourcesStorageView / AlloyStorageView.
        /// </summary>
        public void UpdateTitle(string title, int count)
        {
            sectionTitle = title;
            UpdateHeaderText(count);
        }

        public void UpdateTitle(string title, int count, int total)
        {
            sectionTitle = title;
            UpdateHeaderText(count, total);
        }

        private void UpdateHeaderText(int count = -1, int total = -1)
        {
            if (headerText == null) return;

            string arrow = isExpanded ? expandedArrow : collapsedArrow;

            if (total >= 0)
                headerText.text = $"{arrow} {sectionTitle} ({count}/{total})";
            else if (count >= 0)
                headerText.text = $"{arrow} {sectionTitle} ({count})";
            else
                headerText.text = $"{arrow} {sectionTitle}";
        }
    }
}