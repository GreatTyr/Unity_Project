using UnityEngine.InputSystem;

/// <summary>
/// Статический хелпер для подписки/отписки на Input Actions.
/// Убирает дублирование паттерна performed += ...; canceled += ...; Enable()
/// из PepelacInputHandler, PlayerController и других скриптов.
/// </summary>
public static class InputActionHelper
{
    /// <summary>
    /// Подписаться на performed + canceled и включить action.
    /// </summary>
    public static void Subscribe(InputActionReference actionRef,
        System.Action<InputAction.CallbackContext> performed,
        System.Action<InputAction.CallbackContext> canceled = null)
    {
        if (actionRef?.action == null) return;

        actionRef.action.performed += performed;

        if (canceled != null)
            actionRef.action.canceled += canceled;

        actionRef.action.Enable();
    }

    /// <summary>
    /// Отписаться от performed + canceled и выключить action.
    /// </summary>
    public static void Unsubscribe(InputActionReference actionRef,
        System.Action<InputAction.CallbackContext> performed,
        System.Action<InputAction.CallbackContext> canceled = null)
    {
        if (actionRef?.action == null) return;

        actionRef.action.performed -= performed;

        if (canceled != null)
            actionRef.action.canceled -= canceled;

        actionRef.action.Disable();
    }
}