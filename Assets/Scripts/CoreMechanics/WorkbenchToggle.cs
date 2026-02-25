using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Временный скрипт для открытия/закрытия верстака по клавише P.
/// Работает с любым наследником BaseModuleWorkbench (GeneratorWorkbench, EnergyStorageWorkbench и т.д.)
/// Повесить на тот же GameObject, что и верстак, или любой другой.
/// </summary>
public class WorkbenchToggle : MonoBehaviour
{
    [Tooltip("Ссылка на верстак. Если не назначена — ищет на этом же объекте.")]
    public BaseModuleWorkbench workbench;

    private bool isOpen;

    private void Awake()
    {
        if (workbench == null)
            workbench = GetComponent<BaseModuleWorkbench>();
    }

    private void Update()
    {
        if (workbench == null) return;
        if (Keyboard.current == null) return;
        if (!Keyboard.current.pKey.wasPressedThisFrame) return;

        isOpen = !isOpen;

        if (isOpen)
            workbench.OpenPanel();
        else
            workbench.ClosePanel();
    }
}