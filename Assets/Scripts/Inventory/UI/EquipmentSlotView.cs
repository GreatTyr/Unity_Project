using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.EventSystems;

namespace UnityProject.Inventory
{
    /// <summary>
    /// Визуальное представление одного слота экипировки на кукле игрока.
    /// Отвечает только за отображение и обработку клика по слоту,
    /// а логику экипировки/снятия делегирует во внешний контроллер.
    /// </summary>
    public class EquipmentSlotView : MonoBehaviour, IPointerClickHandler
    {
        [Header("Config")]
        [Tooltip("Тип экипировочного слота, которому соответствует этот UI-элемент.")]
        [SerializeField] private EquipmentSlotType slotType = EquipmentSlotType.None;

        [Header("UI References")]
        [Tooltip("Иконка экипированного предмета (может быть пустой).")]
        [SerializeField] private Image iconImage;
        [Tooltip("Текстовая метка слота (например, 'HEAD', 'BODY').")]
        [SerializeField] private TextMeshProUGUI slotLabel;

        /// <summary>
        /// Ссылка на контроллер куклы игрока.
        /// Через него мы вызываем операции инвентаря.
        /// </summary>
        private PlayerPaperDollView paperDollView;

        public EquipmentSlotType SlotType => slotType;

        /// <summary>
        /// Инициализация вида слота ссылкой на контроллер куклы.
        /// </summary>
        public void Initialize(PlayerPaperDollView owner)
        {
            paperDollView = owner;

            // Можно автоматически проставить текст метки, если он не задан.
            if (slotLabel != null && string.IsNullOrEmpty(slotLabel.text))
            {
                slotLabel.text = slotType.ToString().ToUpperInvariant();
            }
        }

        /// <summary>
        /// Обновление визуальной части из модели слота экипировки.
        /// </summary>
        public void Refresh(EquipmentSlot slot)
        {
            if (slot == null || slot.equippedItem == null || slot.equippedItem.definition == null)
            {
                // Если слота нет или предмет не экипирован – скрываем иконку.
                if (iconImage != null)
                {
                    iconImage.enabled = false;
                    iconImage.sprite = null;
                }
                return;
            }

            if (iconImage != null)
            {
                iconImage.enabled = true;
                iconImage.sprite = slot.equippedItem.definition.icon;
            }
        }

        /// <summary>
        /// Обработка клика по слоту.
        /// По умолчанию – попытка снять предмет (левый клик).
        /// Вся бизнесс-логика лежит в PlayerPaperDollView / PlayerInventory.
        /// </summary>
        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left)
                return;

            if (paperDollView == null)
                return;

            paperDollView.OnSlotClicked(this);
        }
    }
}

