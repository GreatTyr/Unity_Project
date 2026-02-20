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

        private void Awake()
        {
            if (playerInventory == null)
                playerInventory = GetComponent<PlayerInventory>();
        }

        private void OnEnable()
        {
            for (int i = 0; i < hotbarActions.Length; i++)
            {
                if (hotbarActions[i]?.action == null) continue;

                int slotIndex = i;
                hotbarActions[i].action.performed += ctx => OnHotbarPressed(slotIndex);
                hotbarActions[i].action.Enable();
            }
        }

        private void OnDisable()
        {
            for (int i = 0; i < hotbarActions.Length; i++)
            {
                if (hotbarActions[i]?.action == null) continue;
                hotbarActions[i].action.Disable();
            }
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