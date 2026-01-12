// InteractableWithMenu.cs
using UnityEngine;

public class InteractableWithMenu : InteractableBase
{
    [Header("Menu Options")]
    public Transform teleportTarget; // точка перемещения внутри сцены (можно null)
    public string sceneToLoad;       // имя сцены для полной загрузки (опционально)

    public override void Interact()
    {
        System.Action option1 = null;
        System.Action option2 = null;

        if (teleportTarget != null)
        {
            option1 = () =>
            {
                var mover = FindObjectOfType<PlayerMover>();
                if (mover != null) mover.TeleportTo(teleportTarget.position);
                else Debug.LogWarning("[InteractableWithMenu] PlayerMover not found");
            };
        }
        else
        {
            option1 = () => Debug.Log("[InteractableWithMenu] Option1 selected but no teleportTarget assigned");
        }

        if (!string.IsNullOrEmpty(sceneToLoad))
        {
            option2 = () =>
            {
                UnityEngine.SceneManagement.SceneManager.LoadScene(sceneToLoad);
            };
        }

        if (option2 != null)
        {
            InteractionMenuUI.Instance.Show(hintText, teleportTarget != null ? "Переместиться" : "Нет места", option1, "Загрузить сцену", option2, () => { });
        }
        else
        {
            InteractionMenuUI.Instance.Show(hintText, teleportTarget != null ? "Переместиться" : "Нет места", option1, null, null, () => { });
        }
    }
}