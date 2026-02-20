using UnityEngine;
using UnityEngine.InputSystem;

[DisallowMultipleComponent]
public class CursorManager : MonoBehaviour
{
    [Header("Input")]
    public InputActionReference toggleCursorAction;

    [Header("Initial state")]
    public bool startInGameplayMode = true;

    public bool IsInGameplayMode { get; private set; } = true;

    void Awake()
    {
        UIServices.Register(this);

        if (startInGameplayMode)
            EnterGameplayMode();
        else
            EnterUIMode();
    }

    void OnEnable()
    {
        InputActionHelper.Subscribe(toggleCursorAction, OnToggleCursorPerformed);
    }

    void OnDisable()
    {
        InputActionHelper.Unsubscribe(toggleCursorAction, OnToggleCursorPerformed);
    }

    void OnDestroy()
    {
        UIServices.Unregister(this);
    }

    void OnToggleCursorPerformed(InputAction.CallbackContext ctx)
    {
        if (!ctx.performed) return;

        if (IsInGameplayMode)
            EnterUIMode();
        else
            EnterGameplayMode();
    }

    public void EnterGameplayMode()
    {
        IsInGameplayMode = true;

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        UIServices.Get<CrosshairUI>()?.SetVisible(true);
    }

    public void EnterUIMode()
    {
        IsInGameplayMode = false;

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        UIServices.Get<CrosshairUI>()?.SetVisible(false);
        UIServices.Get<InteractionHintUI>()?.SetVisible(false);
    }
}