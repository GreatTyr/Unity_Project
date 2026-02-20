using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using TMPro;

namespace UnityProject.Inventory
{
    public class InventoryContextMenuUI : MonoBehaviour
    {
        public static InventoryContextMenuUI Instance { get; private set; }

        [Header("UI References")]
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private RectTransform menuRoot;
        [SerializeField] private Button equipButton;
        [SerializeField] private Button useButton;
        [SerializeField] private Button dropButton;
        [SerializeField] private Button cancelButton;

        [Header("Texts")]
        [SerializeField] private TextMeshProUGUI equipText;
        [SerializeField] private TextMeshProUGUI useText;
        [SerializeField] private TextMeshProUGUI dropText;

        [Header("Input")]
        [Tooltip("Action для закрытия меню (Escape / Cancel).")]
        [SerializeField] private InputActionReference cancelAction;

        private InventoryListEntry currentEntry;
        private IInventorySource currentSource;
        private Action onClosed;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            Hide();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void OnEnable()
        {
            if (cancelAction?.action != null)
            {
                cancelAction.action.performed += OnCancelPerformed;
                cancelAction.action.Enable();
            }
        }

        private void OnDisable()
        {
            if (cancelAction?.action != null)
            {
                cancelAction.action.performed -= OnCancelPerformed;
                cancelAction.action.Disable();
            }
        }

        private void OnCancelPerformed(InputAction.CallbackContext ctx)
        {
            if (!ctx.performed) return;
            if (!IsVisible()) return;
            Hide();
        }

        public bool IsVisible()
        {
            return canvasGroup != null && canvasGroup.alpha > 0.5f;
        }

        public void Show(
            InventoryListEntry entry,
            IInventorySource source,
            Vector2 screenPosition,
            PlayerInventory playerInventory)
        {
            if (entry.definition == null) return;

            currentEntry = entry;
            currentSource = source;

            if (menuRoot != null)
            {
                menuRoot.position = screenPosition;
                UIUtils.ClampToScreen(menuRoot);
            }

            SetupButtons(entry, source, playerInventory);

            if (canvasGroup != null)
            {
                canvasGroup.alpha = 1f;
                canvasGroup.interactable = true;
                canvasGroup.blocksRaycasts = true;
            }
        }

        public void Hide()
        {
            if (canvasGroup != null)
            {
                canvasGroup.alpha = 0f;
                canvasGroup.interactable = false;
                canvasGroup.blocksRaycasts = false;
            }
            onClosed?.Invoke();
            onClosed = null;
        }

        private void SetupButtons(
            InventoryListEntry entry,
            IInventorySource source,
            PlayerInventory playerInventory)
        {
            var def = entry.definition;

            bool canEquip = def.isEquippable && def.equipmentSlotType != EquipmentSlotType.None;
            if (equipButton != null)
            {
                equipButton.gameObject.SetActive(canEquip);
                if (canEquip)
                {
                    if (equipText != null) equipText.text = "Экипировать";
                    equipButton.onClick.RemoveAllListeners();
                    equipButton.onClick.AddListener(() =>
                    {
                        EquipFromSource(entry, source, playerInventory);
                        Hide();
                    });
                }
            }

            if (useButton != null)
            {
                bool showUse = !canEquip;
                useButton.gameObject.SetActive(showUse);
                if (showUse)
                {
                    if (useText != null) useText.text = "Использовать";
                    useButton.onClick.RemoveAllListeners();
                    useButton.onClick.AddListener(() =>
                    {
                        Debug.Log($"[ContextMenu] Использовать: {def.displayName} (заглушка)");
                        Hide();
                    });
                }
            }

            if (dropButton != null)
            {
                dropButton.gameObject.SetActive(true);
                if (dropText != null) dropText.text = "Выбросить";
                dropButton.onClick.RemoveAllListeners();
                dropButton.onClick.AddListener(() =>
                {
                    DropItem(entry, source);
                    Hide();
                });
            }

            if (cancelButton != null)
            {
                cancelButton.onClick.RemoveAllListeners();
                cancelButton.onClick.AddListener(() => Hide());
            }
        }

        private void EquipFromSource(
            InventoryListEntry entry,
            IInventorySource source,
            PlayerInventory playerInventory)
        {
            if (playerInventory == null || entry.definition == null) return;

            var sourceInv = source?.MainInventory;
            if (sourceInv == null) return;

            var targetSlot = entry.definition.equipmentSlotType;

            InventoryItem found = sourceInv.FindItem(entry.definition);
            if (found == null)
            {
                Debug.LogWarning($"[ContextMenu] Предмет {entry.definition.displayName} не найден в инвентаре.");
                return;
            }

            if (!playerInventory.Equipment.CanEquip(
                    new InventoryItem(entry.definition, 1), targetSlot))
            {
                Debug.LogWarning($"[ContextMenu] Нельзя экипировать {entry.definition.displayName} в {targetSlot}");
                return;
            }

            bool transferredFromExternal = false;

            if (sourceInv != playerInventory.MainInventory)
            {
                int removed = sourceInv.RemoveItem(entry.definition, 1);
                if (removed <= 0)
                {
                    Debug.LogWarning($"[ContextMenu] Не удалось забрать {entry.definition.displayName} из источника.");
                    return;
                }

                int added = playerInventory.AddItem(entry.definition, 1);
                if (added <= 0)
                {
                    sourceInv.AddItem(entry.definition, 1);
                    Debug.LogWarning($"[ContextMenu] Не удалось добавить в инвентарь игрока, откат.");
                    return;
                }

                transferredFromExternal = true;

                found = playerInventory.MainInventory.FindItem(entry.definition);
                if (found == null)
                {
                    playerInventory.MainInventory.RemoveItem(entry.definition, 1);
                    sourceInv.AddItem(entry.definition, 1);
                    Debug.LogWarning($"[ContextMenu] Предмет потерялся после переноса, откат.");
                    return;
                }
            }

            bool result = playerInventory.TryEquipItem(found, targetSlot);

            if (!result && transferredFromExternal)
            {
                int rolledBack = playerInventory.MainInventory.RemoveItem(entry.definition, 1);
                if (rolledBack > 0)
                    sourceInv.AddItem(entry.definition, 1);

                Debug.LogWarning($"[ContextMenu] Экипировка провалилась, откат переноса.");
            }

            Debug.Log($"[ContextMenu] Экипировка {entry.definition.displayName} → {targetSlot}: {result}");
        }

        private void DropItem(InventoryListEntry entry, IInventorySource source)
        {
            if (source?.MainInventory == null || entry.definition == null) return;

            int removed = source.MainInventory.RemoveItem(entry.definition, entry.totalQuantity);
            Debug.Log($"[ContextMenu] Выброшено {removed}x {entry.definition.displayName}");
        }
    }
}