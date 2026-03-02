using UnityEngine;
using UnityEngine.InputSystem;

public class WorkbenchToggle : MonoBehaviour
{
    [Tooltip("—сылка на верстак. ≈сли не назначена Ч ищет на этом же объекте.")]
    public BaseModuleWorkbench workbench;

    [Header("Input")]
    [SerializeField] private Key toggleKey = Key.P;

    private bool isOpen;

    private void Awake()
    {
        if (workbench == null)
            workbench = GetComponent<BaseModuleWorkbench>();
    }

    private void Update()
    {
        if (workbench == null || Keyboard.current == null) return;
        if (!Keyboard.current[toggleKey].wasPressedThisFrame) return;

        isOpen = !isOpen;
        if (isOpen) workbench.OpenPanel();
        else workbench.ClosePanel();
    }
}