using UnityEngine;
using UnityEngine.InputSystem;

public class TempFurnaceInteraction : MonoBehaviour
{
    [SerializeField] private FurnaceCore furnace;
    [SerializeField] private Key toggleKey = Key.O;

    private bool isOpen;

    private void Update()
    {
        if (Keyboard.current == null || !Keyboard.current[toggleKey].wasPressedThisFrame)
            return;

        Debug.Log("[TempFurnace] O pressed");

        if (furnace == null)
        {
            Debug.LogError("[TempFurnace] furnace is null!");
            return;
        }

        if (PersistentUI.Instance == null)
        {
            Debug.LogError("[TempFurnace] PersistentUI.Instance is null!");
            return;
        }

        FurnaceUI ui = PersistentUI.Instance.FurnaceUI;
        if (ui == null)
        {
            Debug.LogError("[TempFurnace] FurnaceUI is null!");
            return;
        }

        isOpen = !isOpen;

        if (isOpen)
        {
            Debug.Log("[TempFurnace] Opening furnace panel");
            ui.OpenForFurnace(furnace);
        }
        else
        {
            Debug.Log("[TempFurnace] Closing furnace panel");
            ui.ClosePanel();
        }
    }
}