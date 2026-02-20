using UnityEngine;

public class WorldMapBootstrap : MonoBehaviour
{
    void Start()
    {
        var cursor = UIServices.Get<CursorManager>();
        if (cursor != null)
        {
            cursor.EnterUIMode();
        }
        else
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
    }
}