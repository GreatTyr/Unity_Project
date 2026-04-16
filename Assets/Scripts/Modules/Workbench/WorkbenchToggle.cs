using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Универсальный переключатель окон верстака. 
/// Можно указать верстак напрямую или он найдется автоматически на этом объекте.
/// </summary>
public class WorkbenchToggle : MonoBehaviour
{
    [Header("Workbench Reference")]
    [Tooltip("Оставь пустым для автопоиска на этом объекте")]
    [SerializeField] private GameObject workbenchObject;

    [Header("Input")]
    [SerializeField] private Key toggleKey = Key.P;

    private IWorkbenchUI workbenchUI;
    private bool isOpen;

    private void Awake()
    {
        // Если объект указан вручную — ищем UI на нём
        if (workbenchObject != null)
        {
            workbenchUI = workbenchObject.GetComponent<IWorkbenchUI>();

            if (workbenchUI == null)
            {
                Debug.LogError($"[WorkbenchToggle] На объекте {workbenchObject.name} не найден компонент, реализующий IWorkbenchUI!");
            }
        }
        // Иначе ищем на текущем объекте
        else
        {
            workbenchUI = GetComponent<IWorkbenchUI>();

            if (workbenchUI == null)
            {
                Debug.LogError($"[WorkbenchToggle] На объекте {gameObject.name} не найден компонент, реализующий IWorkbenchUI!");
            }
        }
    }

    private void Update()
    {
        if (workbenchUI == null || Keyboard.current == null) return;

        if (Keyboard.current[toggleKey].wasPressedThisFrame)
        {
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
}