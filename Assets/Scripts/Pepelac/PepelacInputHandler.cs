using UnityEngine;
using UnityEngine.InputSystem;

[DisallowMultipleComponent]
public class PepelacInputHandler : MonoBehaviour
{
    [Header("Input Actions")]
    [SerializeField] private InputActionReference moveAxisAction;
    [SerializeField] private InputActionReference turnAxisAction;
    [SerializeField] private InputActionReference jumpAction;
    [SerializeField] private InputActionReference strafeAxisAction;
    [SerializeField] private InputActionReference riseAction;
    [SerializeField] private InputActionReference lowerAction;

    public float MoveInput { get; private set; }
    public float TurnInput { get; private set; }
    public float StrafeInput { get; private set; }
    public bool RisePressed { get; private set; }
    public bool LowerPressed { get; private set; }
    public bool JumpRequested { get; private set; }

    private bool controlEnabled;

    public void EnableInput()
    {
        controlEnabled = true;
        ResetAll();
        EnableAllActions();
    }

    public void DisableInput()
    {
        controlEnabled = false;
        DisableAllActions();
        ResetAll();
    }

    public void ConsumeJump()
    {
        JumpRequested = false;
    }

    private void OnEnable()
    {
        InputActionHelper.Subscribe(moveAxisAction, OnMovePerformed, OnMoveCanceled);
        InputActionHelper.Subscribe(turnAxisAction, OnTurnPerformed, OnTurnCanceled);
        InputActionHelper.Subscribe(strafeAxisAction, OnStrafePerformed, OnStrafeCanceled);
        InputActionHelper.Subscribe(jumpAction, OnJumpPerformed);
        InputActionHelper.Subscribe(riseAction, OnRisePerformed, OnRiseCanceled);
        InputActionHelper.Subscribe(lowerAction, OnLowerPerformed, OnLowerCanceled);
    }

    private void OnDisable()
    {
        InputActionHelper.Unsubscribe(moveAxisAction, OnMovePerformed, OnMoveCanceled);
        InputActionHelper.Unsubscribe(turnAxisAction, OnTurnPerformed, OnTurnCanceled);
        InputActionHelper.Unsubscribe(strafeAxisAction, OnStrafePerformed, OnStrafeCanceled);
        InputActionHelper.Unsubscribe(jumpAction, OnJumpPerformed);
        InputActionHelper.Unsubscribe(riseAction, OnRisePerformed, OnRiseCanceled);
        InputActionHelper.Unsubscribe(lowerAction, OnLowerPerformed, OnLowerCanceled);
        ResetAll();
    }

    private void ResetAll()
    {
        MoveInput = 0f;
        TurnInput = 0f;
        StrafeInput = 0f;
        RisePressed = false;
        LowerPressed = false;
        JumpRequested = false;
    }

    private void EnableAllActions()
    {
        moveAxisAction?.action?.Enable();
        turnAxisAction?.action?.Enable();
        strafeAxisAction?.action?.Enable();
        jumpAction?.action?.Enable();
        riseAction?.action?.Enable();
        lowerAction?.action?.Enable();
    }

    private void DisableAllActions()
    {
        moveAxisAction?.action?.Disable();
        turnAxisAction?.action?.Disable();
        strafeAxisAction?.action?.Disable();
        jumpAction?.action?.Disable();
        riseAction?.action?.Disable();
        lowerAction?.action?.Disable();
    }

    // === Callbacks ===

    private void OnMovePerformed(InputAction.CallbackContext ctx)
    {
        if (!controlEnabled) return;
        MoveInput = ctx.ReadValue<float>();
    }
    private void OnMoveCanceled(InputAction.CallbackContext ctx) { MoveInput = 0f; }

    private void OnTurnPerformed(InputAction.CallbackContext ctx)
    {
        if (!controlEnabled) return;
        TurnInput = ctx.ReadValue<float>();
    }
    private void OnTurnCanceled(InputAction.CallbackContext ctx) { TurnInput = 0f; }

    private void OnStrafePerformed(InputAction.CallbackContext ctx)
    {
        if (!controlEnabled) return;
        if (ctx.control != null && ctx.control.valueType == typeof(Vector2))
            StrafeInput = Mathf.Clamp(ctx.ReadValue<Vector2>().x, -1f, 1f);
        else
            StrafeInput = ctx.ReadValue<float>();
    }
    private void OnStrafeCanceled(InputAction.CallbackContext ctx) { StrafeInput = 0f; }

    private void OnJumpPerformed(InputAction.CallbackContext ctx)
    {
        if (!controlEnabled) return;
        if (ctx.performed) JumpRequested = true;
    }

    private void OnRisePerformed(InputAction.CallbackContext ctx)
    {
        if (!controlEnabled) return;
        if (ctx.control != null && ctx.control.valueType == typeof(float))
            RisePressed = ctx.ReadValue<float>() > 0.1f;
        else
            RisePressed = true;
    }
    private void OnRiseCanceled(InputAction.CallbackContext ctx) { RisePressed = false; }

    private void OnLowerPerformed(InputAction.CallbackContext ctx)
    {
        if (!controlEnabled) return;
        if (ctx.control != null && ctx.control.valueType == typeof(float))
            LowerPressed = ctx.ReadValue<float>() > 0.1f;
        else
            LowerPressed = true;
    }
    private void OnLowerCanceled(InputAction.CallbackContext ctx) { LowerPressed = false; }
}