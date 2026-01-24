using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// CursorManager (сценовый)
/// - В каждой сцене свой экземпляр.
/// - Управляет курсором и прицелом в рамках ТЕКУЩЕЙ сцены.
/// - Слушает toggleCursorAction (например, Tab) в этой сцене.
/// 
/// Использование:
/// - Повесить на объект (UIManager/Systems) в каждой сцене, где нужен Tab.
/// - В Inspector:
///   - toggleCursorAction -> InputActionReference на ToggleCursor (Tab).
///   - startInGameplayMode:
///       true  для геймплейной сцены (курсор скрыт по умолчанию),
///       false для WorldMap (курсор виден по умолчанию).
/// </summary>
[DisallowMultipleComponent]
public class CursorManager : MonoBehaviour
{
    public static CursorManager Instance { get; private set; }

    [Header("Input")]
    [Tooltip("Action для переключения курсора (например, Tab). Тип: Button.")]
    public InputActionReference toggleCursorAction;

    [Header("Initial state")]
    [Tooltip("Если true — при старте сцены сразу включится геймплейный режим (курсор скрыт). " +
             "Если false — сразу UI-режим (курсор виден).")]
    public bool startInGameplayMode = true;

    /// <summary>
    /// true  = gameplay (курсор скрыт, прицел включен),
    /// false = UI       (курсор виден, прицел выключен).
    /// </summary>
    public bool IsInGameplayMode { get; private set; } = true;

    void Awake()
    {
        // Локальный Singleton на сцену: если вдруг два в одной сцене — уничтожаем второй.
        if (Instance != null && Instance != this)
        {
            Debug.Log($"[CursorManager] Duplicate instance in scene on {name}, destroying this.");
            Destroy(gameObject);
            return;
        }

        Instance = this;

        Debug.Log($"[CursorManager] Awake on {name}. startInGameplayMode={startInGameplayMode}");

        if (startInGameplayMode)
            EnterGameplayMode();
        else
            EnterUIMode();
    }

    void OnEnable()
    {
        Debug.Log("[CursorManager] OnEnable (scene-local)");

        if (toggleCursorAction != null && toggleCursorAction.action != null)
        {
            toggleCursorAction.action.performed += OnToggleCursorPerformed;
            toggleCursorAction.action.Enable();
            Debug.Log($"[CursorManager] toggleCursorAction enabled: {toggleCursorAction.action.name}");
        }
        else
        {
            Debug.LogWarning("[CursorManager] toggleCursorAction is null or action is null in OnEnable");
        }
    }

    void OnDisable()
    {
        Debug.Log("[CursorManager] OnDisable (scene-local)");

        if (toggleCursorAction != null && toggleCursorAction.action != null)
        {
            toggleCursorAction.action.performed -= OnToggleCursorPerformed;
            toggleCursorAction.action.Disable();
            Debug.Log($"[CursorManager] toggleCursorAction disabled: {toggleCursorAction.action.name}");
        }

        if (Instance == this)
            Instance = null;
    }

    void OnToggleCursorPerformed(InputAction.CallbackContext ctx)
    {
        if (!ctx.performed) return;
        Debug.Log("[CursorManager] OnToggleCursorPerformed (scene-local)");

        if (IsInGameplayMode)
            EnterUIMode();
        else
            EnterGameplayMode();
    }

    /// <summary>
    /// Геймплейный режим: курсор скрыт и залочен, прицел включён.
    /// </summary>
    public void EnterGameplayMode()
    {
        IsInGameplayMode = true;
        Debug.Log("[CursorManager] EnterGameplayMode");

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        CrosshairUI.Instance?.SetVisible(true);
    }

    /// <summary>
    /// UI-режим: курсор виден и свободен, прицел/подсказка выключены.
    /// </summary>
    public void EnterUIMode()
    {
        IsInGameplayMode = false;
        Debug.Log("[CursorManager] EnterUIMode");

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        CrosshairUI.Instance?.SetVisible(false);
        InteractionHintUI.Instance?.SetVisible(false);
    }
}