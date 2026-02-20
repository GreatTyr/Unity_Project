using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using TMPro;

namespace UnityProject.Inventory
{
    public class ItemTooltipUI : MonoBehaviour
    {
        public static ItemTooltipUI Instance { get; private set; }

        [Header("UI References")]
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private RectTransform tooltipRoot;
        [SerializeField] private TextMeshProUGUI nameText;
        [SerializeField] private TextMeshProUGUI categoryText;
        [SerializeField] private TextMeshProUGUI statsText;
        [SerializeField] private Image iconImage;

        [Header("Input")]
        [Tooltip("Ссылка на Pointer Position action (или Mouse Position). Тип: Value, Vector2.")]
        [SerializeField] private InputActionReference pointerPositionAction;

        [Header("Settings")]
        [SerializeField] private Vector2 offset = new Vector2(15f, -15f);
        [SerializeField] private float showDelay = 0.3f;

        private float hoverTimer;
        private bool isWaiting;
        private ItemDefinition pendingItem;

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
            if (isWaiting)
            {
                hoverTimer += Time.unscaledDeltaTime;
                if (hoverTimer >= showDelay && pendingItem != null)
                {
                    ShowImmediate(pendingItem);
                    isWaiting = false;
                }
            }

            if (IsVisible() && tooltipRoot != null)
            {
                Vector2 pos = ReadPointerPosition();
                tooltipRoot.position = pos + offset;
                UIUtils.ClampToScreen(tooltipRoot);
            }
        }

        /// <summary>
        /// Читает позицию курсора через Input Action.
        /// Fallback на Mouse.current если action не назначен.
        /// </summary>
        private Vector2 ReadPointerPosition()
        {
            if (pointerPositionAction?.action != null)
                return pointerPositionAction.action.ReadValue<Vector2>();

            // Fallback для случая если action не назначен в Inspector
            if (Mouse.current != null)
                return Mouse.current.position.ReadValue();

            return Vector2.zero;
        }

        public bool IsVisible()
        {
            return canvasGroup != null && canvasGroup.alpha > 0.5f;
        }

        public void Show(ItemDefinition def)
        {
            if (def == null) { Hide(); return; }
            pendingItem = def;
            hoverTimer = 0f;
            isWaiting = true;
        }

        private void ShowImmediate(ItemDefinition def)
        {
            if (def == null) return;

            if (nameText != null) nameText.text = def.displayName;
            if (categoryText != null) categoryText.text = def.itemCategory.ToString();
            if (iconImage != null)
            {
                iconImage.sprite = def.icon;
                iconImage.enabled = def.icon != null;
            }

            string stats = "";
            if (def.weight > 0f) stats += $"Вес: {def.weight:F1} кг\n";
            if (def.price > 0) stats += $"Цена: {def.price}\n";
            if (def.stackable) stats += $"Стак: до {def.maxStack}\n";
            if (def.isEquippable) stats += $"Слот: {def.equipmentSlotType}\n";
            if (def.rarity > 0) stats += $"Редкость: {def.rarity}\n";

            if (statsText != null) statsText.text = stats.TrimEnd('\n');

            if (canvasGroup != null)
            {
                canvasGroup.alpha = 1f;
                canvasGroup.interactable = false;
                canvasGroup.blocksRaycasts = false;
            }
        }

        public void Hide()
        {
            isWaiting = false;
            pendingItem = null;
            if (canvasGroup != null)
            {
                canvasGroup.alpha = 0f;
                canvasGroup.interactable = false;
                canvasGroup.blocksRaycasts = false;
            }
        }
    }
}