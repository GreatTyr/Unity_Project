using UnityEngine;
using UnityEngine.InputSystem;
using Unity.Cinemachine;
/// <summary>
/// Менеджер режима строительства Пепелаца.
/// Переключает камеры, включает/выключает скрипт билдера и UI, управляет курсором.
/// </summary>
[DisallowMultipleComponent]
public class PepelacBuilderMode : MonoBehaviour
{
    [Header("Input")]
    [Tooltip("Кнопка для входа/выхода из режима строительства (например, 'B')")]
    public InputActionReference toggleBuildModeAction;

    [Tooltip("Камера вида сверху для строительства")]
    public CinemachineVirtualCameraBase builderCamera;

    [Header("References")]
    [Tooltip("Ссылка на ядро строительства (напишем на следующем этапе)")]
    public MonoBehaviour gridBuilder; // Пока используем базовый тип, позже заменим на PepelacGridBuilder

    [Tooltip("Ссылка на UI панель строительства (справа)")]
    public GameObject builderUI;

    public bool IsBuildModeActive { get; private set; }

    private void Awake()
    {
        // Изначально режим выключен
        SetBuildMode(false);
    }

    private void OnEnable()
    {
        InputActionHelper.Subscribe(toggleBuildModeAction, OnToggleBuildMode);
    }

    private void OnDisable()
    {
        InputActionHelper.Unsubscribe(toggleBuildModeAction, OnToggleBuildMode);
    }

    private void OnToggleBuildMode(InputAction.CallbackContext ctx)
    {
        if (!ctx.performed) return;

        // TODO: Добавить проверку, что игрок находится в Пепелаце (за рулем) или рядом с ним
        SetBuildMode(!IsBuildModeActive);
    }

    public void SetBuildMode(bool active)
    {
        IsBuildModeActive = active;

        // Камера билдера получает максимальный приоритет (30), чтобы перекрыть всё.
        // При выключении падает в 0, позволяя системе PlayerVehicleController
        // самой решать, показывать камеру игрока (20) или пепелаца (10/20).
        if (builderCamera != null)
        {
            builderCamera.Priority = active ? 30 : 0;
        }

        // Включаем/выключаем скрипт с логикой строительства
        if (gridBuilder != null)
            gridBuilder.enabled = active;

        // Показываем/скрываем панель модулей
        if (builderUI != null)
            builderUI.SetActive(active);

        // Управляем курсором через наш сервис
        var cursorManager = UIServices.Get<CursorManager>();
        if (cursorManager != null)
        {
            if (active)
                cursorManager.EnterUIMode(); // Показываем мышку
            else
                cursorManager.EnterGameplayMode(); // Прячем мышку
        }

        Debug.Log($"[PepelacBuilderMode] Режим строительства: {(active ? "ВКЛ" : "ВЫКЛ")}");
    }
}