using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace UnityProject.Inventory
{
    public class InventoryUIManager : MonoBehaviour
    {
        [Header("UI Root")]
        [SerializeField] private GameObject inventoryPanelRoot;

        [Header("Three-column Layout")]
        [SerializeField] private InventoryPanelView leftPanel;
        [SerializeField] private InventoryCenterPanelView centerPanel;
        [SerializeField] private InventoryPanelView rightPanel;

        [Header("Input")]
        [SerializeField] private InputActionReference openInventoryAction;

        [Header("Player Reference")]
        [SerializeField] private PlayerInventory playerInventory;

        private bool isOpen;
        private List<IInventorySource> sources;
        private ObjectInventorySource objectSource;

        private void Awake()
        {
            UIServices.Register(this);

            if (inventoryPanelRoot != null)
                inventoryPanelRoot.SetActive(false);

            InitializeSources();
        }

        private void OnDestroy()
        {
            UIServices.Unregister(this);
        }

        private void InitializeSources()
        {
            if (playerInventory == null)
                playerInventory = PlayerLocator.Inventory;

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
            InputActionHelper.Subscribe(openInventoryAction, OnToggleInventory);
        }

        private void OnDisable()
        {
            InputActionHelper.Unsubscribe(openInventoryAction, OnToggleInventory);
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

            UIServices.Get<CursorManager>()?.EnterUIMode();
        }

        public void CloseInventory()
        {
            if (!isOpen) return;
            isOpen = false;

            if (inventoryPanelRoot != null)
                inventoryPanelRoot.SetActive(false);

            objectSource?.ClearContainer();
            UIServices.Get<CursorManager>()?.EnterGameplayMode();
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

        public void RefreshAllPanels()
        {
            leftPanel?.RefreshList();
            rightPanel?.RefreshList();
            centerPanel?.Refresh();
        }

        public HotbarSlotView GetHotbarSlotView(int index)
        {
            if (centerPanel == null) return null;
            return centerPanel.GetHotbarSlotView(index);
        }
    }
}