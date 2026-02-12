using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Временный скрипт для открытия/закрытия верстака по клавише P.
/// Повесить на тот же GameObject, что и ModuleWorkbench, или любой другой.
/// </summary>
public class WorkbenchToggle : MonoBehaviour
{
    [Tooltip("Ссылка на верстак. Если не назначена — ищет на этом же объекте.")]
    public ModuleWorkbench workbench;

    private bool isOpen;

    private void Awake()
    {
        if (workbench == null)
            workbench = GetComponent<ModuleWorkbench>();
    }

    private void Update()
    {
        if (Keyboard.current == null) return;
        if (!Keyboard.current.pKey.wasPressedThisFrame) return;

        isOpen = !isOpen;

        if (isOpen)
            workbench.OpenPanel();
        else
            workbench.ClosePanel();
    }
}