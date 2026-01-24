    using UnityEngine;

/// <summary>
/// GameplayBootstrap
/// - Скрипт начальной настройки ГЕЙМПЛЕЙНОЙ сцены (3D-локации).
/// - При старте переводит игру в gameplay-режим:
///   скрывает курсор, включает прицел.
/// 
/// Использование:
/// - Повесь на объект в геймплейной сцене (например, UIManager, GameManagers, GameplayRoot).
/// - При загрузке этой сцены после любой другой (WorldMap, меню, и т.п.)
///   CursorManager будет переведён в нужное состояние.
/// </summary>
public class GameplayBootstrap : MonoBehaviour
{
    void Start()
    {
        Debug.Log("[GameplayBootstrap] Start");

        if (CursorManager.Instance != null)
        {
            Debug.Log("[GameplayBootstrap] Calling EnterGameplayMode");
            CursorManager.Instance.EnterGameplayMode();
        }
        else
        {
            // Фоллбек для случая запуска сцены напрямую в редакторе без CursorManager
            Debug.LogWarning("[GameplayBootstrap] CursorManager.Instance is null, fallback to manual cursor lock");
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }
}