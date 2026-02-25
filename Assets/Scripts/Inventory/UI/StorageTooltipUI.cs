using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using TMPro;

namespace UnityProject.Inventory
{
    /// <summary>
    /// Тултип для ресурсов и сплавов.
    /// Показывает подробную информацию при наведении.
    /// </summary>
    public class StorageTooltipUI : MonoBehaviour
    {
        public static StorageTooltipUI Instance { get; private set; }

        [Header("UI References")]
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private RectTransform tooltipRoot;
        [SerializeField] private TextMeshProUGUI titleText;
        [SerializeField] private TextMeshProUGUI detailsText;

        [Header("Input")]
        [SerializeField] private InputActionReference pointerPositionAction;

        [Header("Settings")]
        [SerializeField] private Vector2 offset = new Vector2(15f, -15f);

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            Hide();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void OnEnable()
        {
            if (pointerPositionAction?.action != null)
                pointerPositionAction.action.Enable();
        }

        private void OnDisable()
        {
            if (pointerPositionAction?.action != null)
                pointerPositionAction.action.Disable();
        }

        private void Update()
        {
            if (!IsVisible() || tooltipRoot == null) return;

            Vector2 pos = ReadPointerPosition();
            tooltipRoot.position = pos + offset;
            UIUtils.ClampToScreen(tooltipRoot);
        }

        public bool IsVisible()
        {
            return canvasGroup != null && canvasGroup.alpha > 0.5f;
        }

        /// <summary>
        /// Показать тултип для ресурса.
        /// </summary>
        public void ShowResource(ResourcesStorage.ResourceIndex index, long grams)
        {
            int typeIndex = (int)index / ResourcesStorage.TiersPerType;
            int tier = ((int)index % ResourcesStorage.TiersPerType) + 1;
            string typeName = GetResourceTypeName(typeIndex);
            double kg = grams / (double)ResourcesStorage.GramsPerKg;

            if (titleText != null)
                titleText.text = $"{typeName} T{tier}";

            string details = $"Количество: {kg:F3} кг ({grams} г)\n";
            details += $"Тип: {typeName}\n";
            details += $"Тир: {tier}\n";
            details += $"ПКМ — переместить всё";

            if (detailsText != null)
                detailsText.text = details;

            ShowCanvas();
        }

        /// <summary>
        /// Показать тултип для энергии.
        /// </summary>
        public void ShowEnergy(long energyUnits)
        {
            if (titleText != null)
                titleText.text = "⚡ Энергия";

            string details = $"Количество: {energyUnits:N0} ед.\n";
            details += $"ПКМ — переместить всё";

            if (detailsText != null)
                detailsText.text = details;

            ShowCanvas();
        }

        /// <summary>
        /// Показать тултип для сплава с декодированием.
        /// </summary>
        public void ShowAlloy(string code, double massKg)
        {
            if (titleText != null)
                titleText.text = $"Сплав: {code}";

            string details = $"Масса: {massKg:F3} кг\n";

            if (AlloyCode.Decode(code, out AlloyCode.AlloyParams p))
            {
                details += $"\nТир металла: {p.tier}\n";
                details += $"Химикаты: {(p.useChemicals ? "Да" : "Нет")}\n";
                details += $"Наниты: {(p.useNanites ? "Да" : "Нет")}\n";
                details += $"\n— Кинетика —\n";
                details += $"  Поглощение: {p.kineticAbsorption}  Сопротивление: {p.kineticResistance:F1}%\n";
                details += $"— Термика —\n";
                details += $"  Поглощение: {p.thermalAbsorption}  Сопротивление: {p.thermalResistance:F1}%\n";
                details += $"— Химия —\n";
                details += $"  Поглощение: {p.chemicalAbsorption}  Сопротивление: {p.chemicalResistance:F1}%\n";
                details += $"— Энергия —\n";
                details += $"  Поглощение: {p.energyAbsorption}  Сопротивление: {p.energyResistance:F1}%\n";
            }
            else
            {
                details += "(не удалось декодировать код)\n";
            }

            details += $"\nПКМ — переместить всё";

            if (detailsText != null)
                detailsText.text = details;

            ShowCanvas();
        }

        public void Hide()
        {
            if (canvasGroup != null)
            {
                canvasGroup.alpha = 0f;
                canvasGroup.interactable = false;
                canvasGroup.blocksRaycasts = false;
            }
        }

        private void ShowCanvas()
        {
            if (canvasGroup != null)
            {
                canvasGroup.alpha = 1f;
                canvasGroup.interactable = false;
                canvasGroup.blocksRaycasts = false;
            }
        }

        private Vector2 ReadPointerPosition()
        {
            if (pointerPositionAction?.action != null)
                return pointerPositionAction.action.ReadValue<Vector2>();

            if (Mouse.current != null)
                return Mouse.current.position.ReadValue();

            return Vector2.zero;
        }

        private string GetResourceTypeName(int typeIndex)
        {
            return typeIndex switch
            {
                0 => "Провизия",
                1 => "Топливо",
                2 => "Металл",
                3 => "Стройматериалы",
                4 => "Химикаты",
                5 => "Наниты",
                _ => "Неизвестно"
            };
        }
    }
}