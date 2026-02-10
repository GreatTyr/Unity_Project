using UnityEngine;
using UnityEngine.InputSystem;

public class TempFurnaceInteraction : MonoBehaviour
{
    [SerializeField] private FurnaceCore furnace;

    private void Update()
    {
        if (Keyboard.current != null && Keyboard.current.oKey.wasPressedThisFrame)
        {
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

            Debug.Log("[TempFurnace] Opening furnace panel");
            ui.OpenForFurnace(furnace);
        }
    }
}