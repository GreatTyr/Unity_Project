using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// CrosshairController
/// - Управляет видимостью прицела и состоянием курсора ОС.
/// - Включает прицел при игровом управлении, отключает его в UI/worldmap/inventory.
/// - Публичные методы для вызова из других систем.
/// </summary>
[DisallowMultipleComponent]
public class CrosshairController : MonoBehaviour
{
    [Header("References")]
    public Image crosshairImage; // перетащи сюда UI Image (Crosshair)

    [Header("Cursor")]
    public bool lockCursorWhenActive = true; // блокировать курсор при активном прицеле
    public CursorLockMode cursorLockMode = CursorLockMode.Locked;
    public bool showCursorWhenInactive = true; // показывать курсор когда прицел выключен

    // internal state
    bool isActive = false;

    void Reset()
    {
        // Попытка найти Image автоматически
        var img = GetComponentInChildren<Image>();
        if (img != null) crosshairImage = img;
    }

    void Awake()
    {
        if (crosshairImage == null)
        {
            Debug.LogWarning("[CrosshairController] crosshairImage not assigned. Trying to find in children.");
            crosshairImage = GetComponentInChildren<Image>();
        }

        // ensure initial state hidden
        if (crosshairImage != null) crosshairImage.enabled = false;
        ApplyCursorState(false);
    }

    /// <summary>
    /// Показывает или скрывает прицел.
    /// </summary>
    public void ShowCrosshair(bool show)
    {
        if (crosshairImage != null) crosshairImage.enabled = show;
        isActive = show;
        ApplyCursorState(show);
    }

    /// <summary>
    /// Включить прицел для игрового режима (например, при входе в 3D локацию).
    /// </summary>
    public void EnableForGameplay()
    {
        ShowCrosshair(true);
    }

    /// <summary>
    /// Отключить прицел для UI / worldmap / inventory.
    /// </summary>
    public void DisableForUI()
    {
        ShowCrosshair(false);
    }

    void ApplyCursorState(bool active)
    {
        if (active)
        {
            if (lockCursorWhenActive)
            {
                Cursor.lockState = cursorLockMode;
                Cursor.visible = false;
            }
        }
        else
        {
            if (showCursorWhenInactive)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
        }
    }

    // Optional: toggle
    public void ToggleCrosshair()
    {
        ShowCrosshair(!isActive);
    }
}