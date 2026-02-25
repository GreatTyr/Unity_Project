using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

namespace UnityProject.Inventory
{
    /// <summary>
    /// Строка сплава в раскрывающейся секции.
    /// Отображает: код сплава + масса в кг.
    /// ПКМ = переместить всё, ЛКМ = меню, Hover = тултип с декодированием.
    /// </summary>
    public class AlloyRowView : MonoBehaviour,
        IPointerClickHandler,
        IPointerEnterHandler,
        IPointerExitHandler
    {
        [Header("UI References")]
        [SerializeField] private TextMeshProUGUI codeText;
        [SerializeField] private TextMeshProUGUI massText;
        [SerializeField] private Image backgroundImage;

        [Header("Colors")]
        [SerializeField] private Color normalColor = new Color(0.18f, 0.18f, 0.25f, 0.6f);
        [SerializeField] private Color hoverColor = new Color(0.25f, 0.25f, 0.35f, 0.8f);

        private string alloyCode;
        private double masssKg;
        private AlloyStorage sourceStorage;
        private InventoryPanelView ownerPanel;

        public string AlloyCode => alloyCode;
        public AlloyStorage SourceStorage => sourceStorage;

        public void Setup(string code, double massKg, AlloyStorage storage, InventoryPanelView panel)
        {
            alloyCode = code;
            masssKg = massKg;
            sourceStorage = storage;
            ownerPanel = panel;

            if (codeText != null)
                codeText.text = code ?? "(нет кода)";

            if (massText != null)
                massText.text = $"{massKg:F3} кг";

            if (backgroundImage != null)
                backgroundImage.color = normalColor;
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.button == PointerEventData.InputButton.Right)
            {
                TransferToOtherPanel();
            }
            else if (eventData.button == PointerEventData.InputButton.Left)
            {
                // TODO: контекстное меню (Фаза 6)
                Debug.Log($"[AlloyRow] ЛКМ по сплаву {alloyCode}");
            }
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (backgroundImage != null)
                backgroundImage.color = hoverColor;

            ShowTooltip();
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (backgroundImage != null)
                backgroundImage.color = normalColor;

            HideTooltip();
        }

        private void TransferToOtherPanel()
        {
            if (ownerPanel == null || ownerPanel.OtherPanel == null) return;

            var targetSource = ownerPanel.OtherPanel.CurrentSource;
            if (targetSource == null) return;

            var targetAlloys = targetSource.Alloys;
            if (targetAlloys == null || sourceStorage == null)
            {
                Debug.LogWarning("[AlloyRow] Целевое хранилище сплавов недоступно");
                return;
            }

            double mass = sourceStorage.GetMass(alloyCode);
            if (mass <= 0.0) return;

            if (sourceStorage.TryConsumeMass(alloyCode, mass))
            {
                targetAlloys.AddAlloy(alloyCode, mass);
                Debug.Log($"[AlloyRow] Перенесён сплав {alloyCode}: {mass:F3} кг");
            }

            ownerPanel.RefreshList();
            ownerPanel.OtherPanel.RefreshList();
        }

        private void ShowTooltip()
        {
            var tooltip = StorageTooltipUI.Instance;
            if (tooltip == null) return;

            tooltip.ShowAlloy(alloyCode, masssKg);
        }

        private void HideTooltip()
        {
            StorageTooltipUI.Instance?.Hide();
        }
    }
}