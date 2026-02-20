using UnityEngine;

public class GameplayBootstrap : MonoBehaviour
{
    void Start()
    {
        PlayerLocator.Initialize();

        var cursor = UIServices.Get<CursorManager>();
        if (cursor != null)
        {
            cursor.EnterGameplayMode();
        }
        else
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }
}