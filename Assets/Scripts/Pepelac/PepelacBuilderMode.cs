using UnityEngine;
using UnityEngine.InputSystem;
using Unity.Cinemachine;

/// <summary>
/// Менеджер режима строительства Пепелаца.
/// Переключает virtual camera, builder logic, UI и overlay.
/// </summary>
[DisallowMultipleComponent]
public class PepelacBuilderMode : MonoBehaviour
{
    [Header("Input")]
    [Tooltip("Кнопка для входа/выхода из режима строительства")]
    public InputActionReference toggleBuildModeAction;

    [Header("Virtual Builder Camera")]
    [Tooltip("Cinemachine virtual camera для режима строительства")]
    public CinemachineVirtualCameraBase builderCamera;

    [Header("References")]
    [Tooltip("Ядро строительства")]
    public PepelacGridBuilder gridBuilder;

    [Tooltip("UI панели строительства")]
    public GameObject builderUI;

    [Tooltip("Визуализатор сетки строительства")]
    public PepelacGridOverlay gridOverlay;

    public bool IsBuildModeActive { get; private set; }

    private void Awake()
    {
        ResolveReferences();
        SetBuildMode(false);
    }

    private void OnValidate()
    {
        ResolveReferences();
    }

    private void OnEnable()
    {
        InputActionHelper.Subscribe(toggleBuildModeAction, OnToggleBuildMode);
    }

    private void OnDisable()
    {
        InputActionHelper.Unsubscribe(toggleBuildModeAction, OnToggleBuildMode);
    }

    private void ResolveReferences()
    {
        if (gridBuilder == null)
            gridBuilder = GetComponentInChildren<PepelacGridBuilder>(true);

        if (gridOverlay == null && gridBuilder != null)
            gridOverlay = gridBuilder.GetComponentInChildren<PepelacGridOverlay>(true);
    }

    private void OnToggleBuildMode(InputAction.CallbackContext ctx)
    {
        if (!ctx.performed) return;

        SetBuildMode(!IsBuildModeActive);
    }

    public void SetBuildMode(bool active)
    {
        ResolveReferences();

        IsBuildModeActive = active;

        if (builderCamera != null)
            builderCamera.Priority = active ? 30 : 0;

        if (gridBuilder != null)
            gridBuilder.enabled = active;

        if (builderUI != null)
            builderUI.SetActive(active);

        if (gridOverlay != null)
        {
            if (active)
                gridOverlay.Rebuild();

            gridOverlay.SetVisible(active);
        }

        var cursorManager = UIServices.Get<CursorManager>();
        if (cursorManager != null)
        {
            if (active)
                cursorManager.EnterUIMode();
            else
                cursorManager.EnterGameplayMode();
        }

        Debug.Log($"[PepelacBuilderMode] Режим строительства: {(active ? "ВКЛ" : "ВЫКЛ")}");
    }
}