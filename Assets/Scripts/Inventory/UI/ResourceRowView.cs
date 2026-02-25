using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

namespace UnityProject.Inventory
{
    /// <summary>
    /// Строка ресурса в раскрывающейся секции.
    /// Отображает: название ресурса + количество в кг.
    /// ПКМ = переместить всё, ЛКМ = меню, Hover = тултип.
    /// </summary>
    public class ResourceRowView : MonoBehaviour,
        IPointerClickHandler,
        IPointerEnterHandler,
        IPointerExitHandler
    {
        [Header("UI References")]
        [SerializeField] private Image iconImage;
        [SerializeField] private TextMeshProUGUI nameText;
        [SerializeField] private TextMeshProUGUI amountText;
        [SerializeField] private Image backgroundImage;

        [Header("Colors")]
        [SerializeField] private Color normalColor = new Color(0.18f, 0.18f, 0.25f, 0.6f);
        [SerializeField] private Color hoverColor = new Color(0.25f, 0.25f, 0.35f, 0.8f);
        [SerializeField] private Color energyColor = new Color(0.9f, 0.8f, 0.2f, 1f);

        // Данные привязанного ресурса
        private bool isEnergyRow;
        private ResourcesStorage.ResourceIndex resourceIndex;
        private long currentGrams;
        private long currentEnergyUnits;
        private ResourcesStorage sourceStorage;
        private InventoryPanelView ownerPanel;

        public bool IsEnergyRow => isEnergyRow;
        public ResourcesStorage.ResourceIndex ResourceIndex => resourceIndex;
        public ResourcesStorage SourceStorage => sourceStorage;

        /// <summary>
        /// Настроить как строку обычного ресурса.
        /// </summary>
        public void SetupResource(
            ResourcesStorage.ResourceIndex index,
            long grams,
            ResourcesStorage storage,
            InventoryPanelView panel)
        {
            isEnergyRow = false;
            resourceIndex = index;
            currentGrams = grams;
            sourceStorage = storage;
            ownerPanel = panel;

            string fullName = ResourcesStorage.ResourceFullName((int)index);
            double kg = grams / (double)ResourcesStorage.GramsPerKg;

            if (nameText != null)
                nameText.text = fullName;

            if (amountText != null)
                amountText.text = $"{kg:F3} кг";

            if (iconImage != null)
                iconImage.color = GetResourceTypeColor(index);

            if (backgroundImage != null)
                backgroundImage.color = normalColor;
        }

        /// <summary>
        /// Настроить как строку энергии.
        /// </summary>
        public void SetupEnergy(long energyUnits, ResourcesStorage storage, InventoryPanelView panel)
        {
            isEnergyRow = true;
            currentEnergyUnits = energyUnits;
            sourceStorage = storage;
            ownerPanel = panel;

            if (nameText != null)
                nameText.text = "⚡ Энергия";

            if (amountText != null)
                amountText.text = FormatEnergy(energyUnits);

            if (iconImage != null)
                iconImage.color = energyColor;

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
                // TODO: открыть контекстное меню (Фаза 6)
                Debug.Log($"[ResourceRow] ЛКМ по {(isEnergyRow ? "Энергия" : ResourcesStorage.ResourceFullName((int)resourceIndex))}");
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

            var targetStorage = targetSource.Resources;
            if (targetStorage == null || sourceStorage == null)
            {
                Debug.LogWarning("[ResourceRow] Целевое хранилище ресурсов недоступно");
                return;
            }

            if (isEnergyRow)
            {
                long amount = sourceStorage.EnergyUnits;
                if (amount <= 0) return;

                if (sourceStorage.TryConsumeEnergy(amount))
                {
                    targetStorage.AddEnergy(amount);
                    Debug.Log($"[ResourceRow] Перенесено энергии: {amount}");
                }
            }
            else
            {
                long grams = sourceStorage.GetGrams(resourceIndex);
                if (grams <= 0) return;

                if (sourceStorage.TryRemoveGrams(resourceIndex, grams))
                {
                    targetStorage.AddGrams(resourceIndex, grams);
                    Debug.Log($"[ResourceRow] Перенесено: {ResourcesStorage.ResourceFullName((int)resourceIndex)} {grams / 1000.0:F3} кг");
                }
            }

            // Обновить обе панели
            ownerPanel.RefreshList();
            ownerPanel.OtherPanel.RefreshList();
        }

        private void ShowTooltip()
        {
            var tooltip = StorageTooltipUI.Instance;
            if (tooltip == null) return;

            if (isEnergyRow)
            {
                tooltip.ShowEnergy(currentEnergyUnits);
            }
            else
            {
                tooltip.ShowResource(resourceIndex, currentGrams);
            }
        }

        private void HideTooltip()
        {
            StorageTooltipUI.Instance?.Hide();
        }

        private Color GetResourceTypeColor(ResourcesStorage.ResourceIndex index)
        {
            int typeIndex = (int)index / ResourcesStorage.TiersPerType;
            return typeIndex switch
            {
                0 => new Color(0.2f, 0.8f, 0.2f),   // Provisions — зелёный
                1 => new Color(0.9f, 0.5f, 0.1f),   // Fuel — оранжевый
                2 => new Color(0.6f, 0.6f, 0.7f),   // Metal — серый
                3 => new Color(0.7f, 0.5f, 0.3f),   // Building — коричневый
                4 => new Color(0.3f, 0.7f, 0.9f),   // Chemicals — голубой
                5 => new Color(0.7f, 0.3f, 0.9f),   // Nanites — фиолетовый
                _ => Color.white
            };
        }

        private string FormatEnergy(long energy)
        {
            if (energy >= 1_000_000_000)
                return $"{energy / 1_000_000_000.0:F2} млрд";
            if (energy >= 1_000_000)
                return $"{energy / 1_000_000.0:F2} млн";
            if (energy >= 1_000)
                return $"{energy / 1_000.0:F1} тыс";
            return energy.ToString();
        }
    }
}