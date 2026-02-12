using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace UnityProject.Inventory
{
    /// <summary>
    /// Главный менеджер UI инвентаря (Mount & Blade стиль).
    /// Управляет тремя панелями и переключением курсора.
    /// </summary>
    public class InventoryUIManager : MonoBehaviour
    {
        public static InventoryUIManager Instance { get; private set; }

        [Header("UI Root")]
        [Tooltip("Корневой объект всего инвентаря (дочерний Canvas или Panel).")]
        [SerializeField] private GameObject inventoryPanelRoot;

        [Header("Three-column Layout")]
        [SerializeField] private InventoryPanelView leftPanel;
        [SerializeField] private InventoryCenterPanelView centerPanel;
        [SerializeField] private InventoryPanelView rightPanel;

        [Header("Input")]
        [SerializeField] private InputActionReference openInventoryAction;

        [Header("Player Reference")]
        [Tooltip("Назначьте вручную в инспекторе вместо FindObjectOfType.")]
        [SerializeField] private PlayerInventory playerInventory;

        private bool isOpen;
        private List<IInventorySource> sources;
        private ObjectInventorySource objectSource;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            if (inventoryPanelRoot != null)
                inventoryPanelRoot.SetActive(false);

            InitializeSources();
        }

        private void InitializeSources()
        {
            // Если не назначен в инспекторе — пробуем найти
            if (playerInventory == null)
                playerInventory = FindFirstObjectByType<PlayerInventory>();

            objectSource = new ObjectInventorySource();

            sources = new List<IInventorySource>
            {
                new PlayerInventorySource(playerInventory),
                new PepelacInventorySource(),
                new SquadInventorySource(),
                new BaseInventorySource(),
                objectSource
            };

            if (leftPanel != null)
                leftPanel.SetSources(sources);

            if (rightPanel != null)
            {
                rightPanel.SetSources(sources);
                rightPanel.OtherPanel = leftPanel;
            }

            if (leftPanel != null)
                leftPanel.OtherPanel = rightPanel;
        }

        private void OnEnable()
        {
            if (openInventoryAction?.action != null)
            {
                openInventoryAction.action.performed += OnToggleInventory;
                openInventoryAction.action.Enable();
            }
        }

        private void OnDisable()
        {
            if (openInventoryAction?.action != null)
            {
                openInventoryAction.action.performed -= OnToggleInventory;
                openInventoryAction.action.Disable();
            }
            if (Instance == this) Instance = null;
        }

        private void OnToggleInventory(InputAction.CallbackContext ctx)
        {
            if (!ctx.performed) return;
            if (isOpen) CloseInventory();
            else OpenInventory();
        }

        public void OpenInventory()
        {
            if (isOpen) return;
            isOpen = true;

            if (inventoryPanelRoot != null)
                inventoryPanelRoot.SetActive(true);

            leftPanel?.RefreshList();
            rightPanel?.RefreshList();
            centerPanel?.Refresh();

            CursorManager.Instance?.EnterUIMode();
        }

        public void CloseInventory()
        {
            if (!isOpen) return;
            isOpen = false;

            if (inventoryPanelRoot != null)
                inventoryPanelRoot.SetActive(false);

            objectSource?.ClearContainer();
            CursorManager.Instance?.EnterGameplayMode();
        }

        public void SetOpenedContainer(Inventory containerInv, string displayName = "Объект")
        {
            if (objectSource != null && containerInv != null)
            {
                objectSource.SetContainer(containerInv, displayName);
                leftPanel?.RefreshList();
                rightPanel?.RefreshList();
            }
        }

        public void ClearOpenedContainer()
        {
            objectSource?.ClearContainer();
            leftPanel?.RefreshList();
            rightPanel?.RefreshList();
        }
    }
}
