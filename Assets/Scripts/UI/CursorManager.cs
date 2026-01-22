using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// CursorManager
/// - Единая точка управления курсором ОС и режимом "игра / UI".
/// - В геймплее: курсор скрыт и залочен, прицел (CrosshairUI) включен.
/// - В UI-режиме: курсор видим и разблокирован, прицел скрыт.
/// - Поддерживает глобальный toggle по кнопке (например, Tab).
///
/// Интеграция:
/// - Повесь этот скрипт на отдельный GameObject (например, "Systems") или на UI-Canvas.
/// - В инспекторе назначь InputActionReference на toggleCursorAction (кнопка Tab).
/// - В местах, где открывается полноэкранный UI (инвентарь, меню, world map),
///   вызывай CursorManager.Instance.EnterUIMode() / EnterGameplayMode().
/// </summary>
[DisallowMultipleComponent]
public class CursorManager : MonoBehaviour
{
    public static CursorManager Instance { get; private set; }

    [Header("Input")]
    [Tooltip("Action для переключения курсора (например, Tab). Тип: Button.")]
    public InputActionReference toggleCursorAction;

    [Header("Initial state")]
    [Tooltip("Запустить игру сразу в геймплейном режиме (курсор скрыт, прицел включен).")]
    public bool startInGameplayMode = true;

    // Текущее состояние
    public bool IsInGameplayMode { get; private set; } = true;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        // Устанавливаем начальный режим
        if (startInGameplayMode)
            EnterGameplayMode();
        else
            EnterUIMode();
    }

    void OnEnable()
    {
        if (toggleCursorAction != null && toggleCursorAction.action != null)
        {
            toggleCursorAction.action.performed += OnToggleCursorPerformed;
            toggleCursorAction.action.Enable();
        }
    }

    void OnDisable()
    {
        if (toggleCursorAction != null && toggleCursorAction.action != null)
        {
            toggleCursorAction.action.performed -= OnToggleCursorPerformed;
            toggleCursorAction.action.Disable();
        }
    }

    void OnToggleCursorPerformed(InputAction.CallbackContext ctx)
    {
        if (!ctx.performed) return;

        // Простое переключение между режимами
        if (IsInGameplayMode)
            EnterUIMode();
        else
            EnterGameplayMode();
    }

    /// <summary>
    /// Войти в геймплейный режим:
    /// - Курсор скрыт и заблокирован.
    /// - Прицел (CrosshairUI) виден.
    /// - Взаимодействие идёт через look-based систему.
    /// </summary>
    public void EnterGameplayMode()
    {
        IsInGameplayMode = true;

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        // Прицел включаем (если есть)
        CrosshairUI.Instance?.SetVisible(true);
    }

    /// <summary>
    /// Войти в UI-режим:
    /// - Курсор видим и разблокирован.
    /// - Прицел скрыт.
    /// - Используется для меню, инвентаря, world map и т.п.
    /// </summary>
    public void EnterUIMode()
    {
        IsInGameplayMode = false;

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        // Прицел выключаем (если есть)
        CrosshairUI.Instance?.SetVisible(false);
        // Подсказку можно тоже скрыть, если она мешает UI
        InteractionHintUI.Instance?.SetVisible(false);
    }
}