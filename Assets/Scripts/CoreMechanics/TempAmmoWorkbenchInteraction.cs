// TempAmmoWorkbenchInteraction.cs
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Временный скрипт взаимодействия.
/// Открывает/закрывает UI верстака по клавише U.
/// Использует новую Input System.
/// </summary>
public class TempAmmoWorkbenchInteraction : MonoBehaviour
{
    [Header("Ссылка на UI верстака")]
    [SerializeField] private AmmoWorkbenchUI workbenchUI;

    [Header("Клавиша взаимодействия")]
    [SerializeField] private Key toggleKey = Key.U;

    private bool isOpen = false;

    private void Start()
    {
        if (workbenchUI != null)
        {
            workbenchUI.enabled = false;
            isOpen = false;
        }
    }

    private void Update()
    {
        if (Keyboard.current != null && Keyboard.current[toggleKey].wasPressedThisFrame)
        {
            Toggle();
        }
    }

    public void Toggle()
    {
        if (workbenchUI == null)
        {
            Debug.LogWarning("[TempAmmoWorkbenchInteraction] AmmoWorkbenchUI не назначен.");
            return;
        }

        isOpen = !isOpen;
        workbenchUI.enabled = isOpen;

        if (isOpen)
            Debug.Log("[TempAmmoWorkbenchInteraction] Верстак открыт.");
        else
            Debug.Log("[TempAmmoWorkbenchInteraction] Верстак закрыт.");
    }

    public void Open()
    {
        if (workbenchUI != null)
        {
            isOpen = true;
            workbenchUI.enabled = true;
        }
    }

    public void Close()
    {
        if (workbenchUI != null)
        {
            isOpen = false;
            workbenchUI.enabled = false;
        }
    }

    public bool IsOpen => isOpen;
}