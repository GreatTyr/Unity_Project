using System;
using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

namespace UnityProject.Inventory
{
    public class HotbarInputHandler : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private PlayerInventory playerInventory;

        [Header("Input Actions")]
        [SerializeField] private InputActionReference[] hotbarActions = new InputActionReference[4];

        [Header("Settings")]
        [SerializeField] private float highlightDuration = 0.3f;

        private Action<InputAction.CallbackContext>[] hotbarPerformedHandlers;

        private void Awake()
        {
            if (playerInventory == null)
                playerInventory = GetComponent<PlayerInventory>();

            EnsureHandlersArray();
        }

        private void OnEnable()
        {
            EnsureHandlersArray();

            for (int i = 0; i < hotbarActions.Length; i++)
            {
                var action = hotbarActions[i]?.action;
                if (action == null) continue;

                if (hotbarPerformedHandlers[i] == null)
                {
                    int slotIndex = i;
                    hotbarPerformedHandlers[i] = ctx =>
                    {
                        if (ctx.performed)
                            OnHotbarPressed(slotIndex);
                    };
                }

                // Защитно снимаем перед повторной подпиской,
                // чтобы не копить дубли при нестандартном lifecycle.
                action.performed -= hotbarPerformedHandlers[i];
                action.performed += hotbarPerformedHandlers[i];
                action.Enable();
            }
        }

        private void OnDisable()
        {
            if (hotbarPerformedHandlers == null) return;

            for (int i = 0; i < hotbarActions.Length; i++)
            {
                var action = hotbarActions[i]?.action;
                if (action == null) continue;

                if (i < hotbarPerformedHandlers.Length && hotbarPerformedHandlers[i] != null)
                    action.performed -= hotbarPerformedHandlers[i];

                action.Disable();
            }
        }

        private void EnsureHandlersArray()
        {
            int size = hotbarActions != null ? hotbarActions.Length : 0;

            if (hotbarPerformedHandlers == null || hotbarPerformedHandlers.Length != size)
                hotbarPerformedHandlers = new Action<InputAction.CallbackContext>[size];
        }

        private void OnHotbarPressed(int index)
        {
            var cursor = UIServices.Get<CursorManager>();
            if (cursor != null && !cursor.IsInGameplayMode)
                return;

            UseHotbarSlot(index);
        }

        private void UseHotbarSlot(int index)
        {
            if (playerInventory == null) return;

            var item = playerInventory.GetHotbarItem(index);
            if (item == null || item.definition == null)
            {
                Debug.Log($"[HotbarInput] Слот {index + 1} пуст");
                return;
            }

            Debug.Log($"[HotbarInput] Используем слот {index + 1}: {item.definition.displayName}");

            HighlightSlotVisual(index);
        }

        private void HighlightSlotVisual(int index)
        {
            var uiManager = UIServices.Get<InventoryUIManager>();
            if (uiManager == null) return;

            var slotView = uiManager.GetHotbarSlotView(index);
            if (slotView == null) return;

            StartCoroutine(HighlightCoroutine(slotView));
        }

        private IEnumerator HighlightCoroutine(HotbarSlotView slotView)
        {
            slotView.SetHighlight(true);
            yield return new WaitForSeconds(highlightDuration);
            slotView.SetHighlight(false);
        }
    }
}