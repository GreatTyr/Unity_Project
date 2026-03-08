using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Универсальный переключатель окон. 
/// Автоматически находит любой скрипт, реализующий IWorkbenchUI (Генератор, Батарея и т.д.).
/// </summary>
public class WorkbenchToggle : MonoBehaviour
{
    [Header("Input")]
    [SerializeField] private Key toggleKey = Key.P;

    private IWorkbenchUI workbenchUI;
    private bool isOpen;

    private void Awake()
    {
        // Ищем любой UI верстака, который висит на этом же объекте
        workbenchUI = GetComponent<IWorkbenchUI>();

        if (workbenchUI == null)
        {
            Debug.LogError($"[WorkbenchToggle] На объекте {gameObject.name} не найден компонент, реализующий IWorkbenchUI!");
        }
    }

    private void Update()
    {
        if (workbenchUI == null || Keyboard.current == null) return;

        if (!Keyboard.current[toggleKey].wasPressedThisFrame) return;

        isOpen = !isOpen;
        if (isOpen)
        {
            workbenchUI.OpenPanel();
        }
        else
        {
            workbenchUI.ClosePanel();
        }
    }
}