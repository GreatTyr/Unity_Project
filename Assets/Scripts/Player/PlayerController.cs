using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
public class PlayerController : MonoBehaviour
{
    [Header("Input")]
    public InputActionReference moveAction;
    public InputActionReference sprintAction;
    public InputActionReference jumpAction;

    [Header("References")]
    public Transform cameraTarget;

    [Header("Movement")]
    public float walkSpeed = 3.5f;
    public float sprintMultiplier = 1.8f;

    [Header("Rotation")]
    public bool rotateToCameraOnInput = true;
    public bool instantRotateToCamera = false;
    public float rotationSmoothTime = 0.12f;

    [Header("Jump & Gravity")]
    public float jumpForce = 5f;
    public float gravity = -9.81f;
    public bool useCharacterControllerGround = true;

    [Header("Jump Buffer")]
    public float jumpBufferTime = 0.15f;
    public bool acceptHoldToRepeat = false;

    [Header("Ground Check")]
    public LayerMask groundLayers = ~0;
    public Vector3 groundCheckOffset = new Vector3(0, -0.1f, 0);
    public float groundCheckRadius = 0.2f;

    [Header("Animation")]
    [SerializeField] private string speedParam = "Speed";

    private Animator animator;
    private int speedParamHash;
    private CharacterController cc;
    private Vector2 moveInput;
    private bool isSprinting;
    private bool jumpRequested;
    private float jumpRequestedTime = -999f;
    private float verticalVelocity;
    private float currentVelocityAngle;
    private float smoothYaw;

    void Awake()
    {
        cc = GetComponent<CharacterController>();
        animator = GetComponent<Animator>();
        speedParamHash = Animator.StringToHash(speedParam);

        if (animator == null)
            Debug.LogWarning("[PlayerController] Animator not found on Player.");
    }

    void OnEnable()
    {
        InputActionHelper.Subscribe(moveAction, OnMovePerformed, OnMoveCanceled);
        InputActionHelper.Subscribe(sprintAction, OnSprintPerformed, OnSprintCanceled);
        InputActionHelper.Subscribe(jumpAction, OnJumpPerformed, OnJumpCanceled);
    }

    void OnDisable()
    {
        InputActionHelper.Unsubscribe(moveAction, OnMovePerformed, OnMoveCanceled);
        InputActionHelper.Unsubscribe(sprintAction, OnSprintPerformed, OnSprintCanceled);
        InputActionHelper.Unsubscribe(jumpAction, OnJumpPerformed, OnJumpCanceled);
    }

    void Start()
    {
        smoothYaw = transform.eulerAngles.y;
    }

    void Update()
    {
        HandleGravityAndJump();

        Vector3 move = CalculateMoveVector();
        float baseSpeed = walkSpeed * (isSprinting ? sprintMultiplier : 1f);
        float currentHorizontalSpeed = baseSpeed * move.magnitude;

        if (animator != null)
            animator.SetFloat(speedParamHash, currentHorizontalSpeed);

        Vector3 horizontalMotion = move * baseSpeed * Time.deltaTime;
        Vector3 verticalMotion = Vector3.up * verticalVelocity * Time.deltaTime;
        cc.Move(horizontalMotion + verticalMotion);

        if (rotateToCameraOnInput && moveInput != Vector2.zero)
            RotateToCameraYaw();
    }

    // === Input Callbacks ===

    void OnMovePerformed(InputAction.CallbackContext ctx) => moveInput = ctx.ReadValue<Vector2>();
    void OnMoveCanceled(InputAction.CallbackContext ctx) => moveInput = Vector2.zero;
    void OnSprintPerformed(InputAction.CallbackContext ctx) => isSprinting = ctx.ReadValueAsButton();
    void OnSprintCanceled(InputAction.CallbackContext ctx) => isSprinting = false;

    void OnJumpPerformed(InputAction.CallbackContext ctx)
    {
        if (ctx.performed)
        {
            jumpRequested = true;
            jumpRequestedTime = Time.time;
        }
    }

    void OnJumpCanceled(InputAction.CallbackContext ctx)
    {
        if (!acceptHoldToRepeat)
            jumpRequested = false;
    }

    // === Logic ===

    void HandleGravityAndJump()
    {
        bool grounded = IsGrounded();

        if (grounded)
        {
            if (verticalVelocity < 0f) verticalVelocity = -2f;

            if (jumpRequested && (Time.time - jumpRequestedTime) <= jumpBufferTime)
            {
                verticalVelocity = jumpForce;
                jumpRequested = acceptHoldToRepeat;
            }
            else if ((Time.time - jumpRequestedTime) > jumpBufferTime)
            {
                jumpRequested = false;
            }
        }
        else
        {
            verticalVelocity += gravity * Time.deltaTime;
        }
    }

    bool IsGrounded()
    {
        if (useCharacterControllerGround) return cc.isGrounded;
        Vector3 origin = transform.position + groundCheckOffset;
        return Physics.CheckSphere(origin, groundCheckRadius, groundLayers, QueryTriggerInteraction.Ignore);
    }

    Vector3 CalculateMoveVector()
    {
        Transform refTransform = cameraTarget;
        if (refTransform == null && Camera.main != null) refTransform = Camera.main.transform;

        Vector3 forward = Vector3.forward;
        Vector3 right = Vector3.right;

        if (refTransform != null)
        {
            forward = refTransform.forward; forward.y = 0f;
            if (forward.sqrMagnitude < 0.001f) forward = Vector3.forward;
            forward.Normalize();

            right = refTransform.right; right.y = 0f;
            if (right.sqrMagnitude < 0.001f) right = Vector3.right;
            right.Normalize();
        }

        Vector3 move = forward * moveInput.y + right * moveInput.x;
        if (move.sqrMagnitude > 1f) move.Normalize();
        return move;
    }

    void RotateToCameraYaw()
    {
        float cameraYaw = GetPreferredCameraYaw();
        float currentYaw = transform.eulerAngles.y;

        if (instantRotateToCamera)
        {
            transform.rotation = Quaternion.Euler(0f, cameraYaw, 0f);
            smoothYaw = cameraYaw;
            currentVelocityAngle = 0f;
        }
        else
        {
            smoothYaw = Mathf.SmoothDampAngle(currentYaw, cameraYaw, ref currentVelocityAngle, rotationSmoothTime);
            transform.rotation = Quaternion.Euler(0f, smoothYaw, 0f);
        }
    }

    float GetPreferredCameraYaw()
    {
        float mainYaw = (Camera.main != null) ? Camera.main.transform.eulerAngles.y : float.NaN;

        if (cameraTarget == null)
            return !float.IsNaN(mainYaw) ? mainYaw : transform.eulerAngles.y;

        float targetYaw = cameraTarget.eulerAngles.y;
        if (float.IsNaN(mainYaw))
            return targetYaw;

        float diff = Mathf.Abs(Mathf.DeltaAngle(targetYaw, mainYaw));
        if (diff > 0.5f)
            return mainYaw;

        return targetYaw;
    }

    public void ForceStop()
    {
        moveInput = Vector2.zero;
        isSprinting = false;

        if (animator != null)
            animator.SetFloat(speedParamHash, 0f);
    }
}