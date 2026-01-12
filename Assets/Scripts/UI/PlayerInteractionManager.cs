// PlayerInteractionManager.cs
// Ставится на Player (или объект, управляющий взаимодействием).
// Обнаруживает hovered интерактивный объект (через вызов OnObjectHovered/OnObjectUnhovered; эти вызовы выполняются InteractableBase при OnTriggerEnter/Exit).
// Подключает InputAction (кнопка F) и при нажатии вызывает Interact() на текущем hovered объекте.
// Также заботится о показе/скрытии подсказки и закрытии меню при уходе из зоны.

using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
public class PlayerInteractionManager : MonoBehaviour
{
    [Header("Input")]
    [Tooltip("Ссылка на Input Action (Player -> Interact)")]
    public InputActionReference interactAction; // назначить через инспектор (Player -> Interact)

    Interactable currentHovered; // текущее наведённое (может быть null)

    void OnEnable()
    {
        // Подписываемся на событие действия Interact (например, клавиша F)
        if (interactAction != null && interactAction.action != null)
        {
            interactAction.action.performed += OnInteractPerformed;
            interactAction.action.Enable();
        }
        else
        {
            Debug.LogWarning("[PlayerInteractionManager] interactAction не назначен или action == null");
        }
    }

    void OnDisable()
    {
        // Отписываемся
        if (interactAction != null && interactAction.action != null)
        {
            interactAction.action.performed -= OnInteractPerformed;
            interactAction.action.Disable();
        }
    }

    void Update()
    {
        // Здесь можно добавить raycast-проверку взгляда, или дистанцию, если нужно
        // Пример: если хочешь показывать подсказку только когда игрок смотрит на объект — реализуй здесь.
    }

    // Вызывается InteractableBase при входе игрока в зону взаимодействия
    //public void OnObjectHovered(Interactable interactable)
    //{
    //    // Запоминаем текущий объект
    //    currentHovered = interactable;

    //    // Извлекаем текст подсказки из базового компонента (если доступен)
    //    var baseComp = (interactable as MonoBehaviour)?.GetComponent<InteractableBase>();
    //    string hint = baseComp != null ? baseComp.hintText : "Нажмите F";

    //    // Показать подсказку (нижний HUD)
    //    if (InteractionHintUI.Instance != null)
    //        InteractionHintUI.Instance.SetVisible(true, hint);
    //    else
    //        Debug.LogWarning("[PlayerInteractionManager] InteractionHintUI.Instance == null");

    //    // Вызываем OnHoverEnter у интерактивного объекта (например, чтобы включить highlight)
    //    interactable?.OnHoverEnter();
    //}
    public void OnObjectHovered(Interactable interactable)
    {
        currentHovered = interactable;

        var baseComp = (interactable as MonoBehaviour)?.GetComponent<InteractableBase>();
        string finalHint = "Нажмите F";

        if (baseComp != null)
        {
            // Собираем красивый текст: [F] + hintText
            string key = string.IsNullOrEmpty(baseComp.keyLabel) ? "F" : baseComp.keyLabel;
            string hint = string.IsNullOrEmpty(baseComp.hintText) ? "Взаимодействие" : baseComp.hintText;
            finalHint = $"[{key}] {hint}";
        }

        if (InteractionHintUI.Instance != null)
            InteractionHintUI.Instance.SetVisible(true, finalHint);
        else
            Debug.LogWarning("[PlayerInteractionManager] InteractionHintUI.Instance == null");

        interactable?.OnHoverEnter();
    }
    // Вызывается InteractableBase при выходе из зоны
    public void OnObjectUnhovered(Interactable interactable)
    {
        // Если уходящий объект был текущим — сбрасываем
        if (currentHovered == interactable) currentHovered = null;

        // Скрываем подсказку
        if (InteractionHintUI.Instance != null)
            InteractionHintUI.Instance.SetVisible(false);

        // Если меню открыто — закрываем его (чтобы кнопки не висели после ухода)
        if (InteractionMenuUI.Instance != null && InteractionMenuUI.Instance.IsVisible())
        {
            InteractionMenuUI.Instance.Hide();
        }

        // Вызываем OnHoverExit у интерактивного объекта (например, чтобы выключить highlight)
        interactable?.OnHoverExit();
    }

    // Обработчик нажатия клавиши Interact (подписан на InputAction)
    void OnInteractPerformed(InputAction.CallbackContext ctx)
    {
        // Если есть текущий наведённый интерактивный объект — активируем его
        if (currentHovered != null)
        {
            currentHovered.Interact();
        }
    }
}