// InteractableWithMenu.cs
using UnityEngine;

public class InteractableWithMenu : InteractableBase
{
    [Header("Menu Options")]
    public Transform teleportTarget; // ����� ����������� ������ ����� (����� null)
    public string sceneToLoad;       // ��� ����� ��� ������ �������� (�����������)

    public override void Interact()
    {
        System.Action option1 = null;
        System.Action option2 = null;

        if (teleportTarget != null)
        {
            option1 = () =>
            {
                var mover = UnityEngine.Object.FindFirstObjectByType<PlayerMover>();
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
            InteractionMenuUI.Instance.Show(hintText, teleportTarget != null ? "�������������" : "��� �����", option1, "��������� �����", option2, () => { });
        }
        else
        {
            InteractionMenuUI.Instance.Show(hintText, teleportTarget != null ? "�������������" : "��� �����", option1, null, null, () => { });
        }
    }
}