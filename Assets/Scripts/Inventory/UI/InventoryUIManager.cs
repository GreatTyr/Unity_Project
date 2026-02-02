using UnityEngine;
using UnityEngine.InputSystem;

namespace UnityProject.Inventory
{
    /// <summary>
    /// Управляет окном инвентаря:
    /// - показывает/скрывает панель,
    /// - обновляет InventoryGridView,
    /// - переключает курсор через CursorManager.
    /// </summary>
    public class InventoryUIManager : MonoBehaviour
    {
        public static InventoryUIManager Instance { get; private set; }

        [Header("UI")]
        [Tooltip("Корневой объект панели инвентаря (обычно панель внутри Canvas).")]
        [SerializeField] private GameObject inventoryPanelRoot;
        [Tooltip("Компонент, который рисует грид игрока.")]
        [SerializeField] private InventoryGridView playerGridView;

        [Header("Input")]
        [Tooltip("InputAction для открытия/закрытия инвентаря (например, клавиша I или Tab).")]
        [SerializeField] private InputActionReference openInventoryAction;

        private bool isOpen;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            if (inventoryPanelRoot != null)
                inventoryPanelRoot.SetActive(false); // стартуем скрытым
        }

        private void OnEnable()
        {
            if (openInventoryAction != null && openInventoryAction.action != null)
            {
                openInventoryAction.action.performed += OnOpenInventoryPerformed;
                openInventoryAction.action.Enable();
            }
        }

        private void OnDisable()
        {
            if (openInventoryAction != null && openInventoryAction.action != null)
            {
                openInventoryAction.action.performed -= OnOpenInventoryPerformed;
                openInventoryAction.action.Disable();
            }

            if (Instance == this)
                Instance = null;
        }

        private void OnOpenInventoryPerformed(InputAction.CallbackContext ctx)
        {
            if (!ctx.performed) return;

            if (isOpen)
                CloseInventory();
            else
                OpenInventory();
        }

        public void OpenInventory()
        {
            if (isOpen) return;
            isOpen = true;

            if (inventoryPanelRoot != null)
                inventoryPanelRoot.SetActive(true);

            // Обновляем визуал
            playerGridView?.Refresh();

            // Переводим курсор в UI-режим
            CursorManager.Instance?.EnterUIMode();
        }

        public void CloseInventory()
        {
            if (!isOpen) return;
            isOpen = false;

            if (inventoryPanelRoot != null)
                inventoryPanelRoot.SetActive(false);

            // Возврат в геймплейный режим
            CursorManager.Instance?.EnterGameplayMode();
        }
    }
}