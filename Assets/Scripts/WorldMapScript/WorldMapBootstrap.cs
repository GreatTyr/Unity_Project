using UnityEngine;

/// <summary>
/// WorldMapBootstrap
/// - Скрипт начальной настройки сцены карты мира (WorldMap).
/// - При старте переводит игру в UI-режим:
///   включает курсор, скрывает прицел/подсказки,
///   чтобы можно было спокойно кликать по карте и по UI-кнопкам.
/// 
/// Использование:
/// - Повесь на любой объект в сцене WorldMap (например, WorldMapRoot или PlayerOnWorldMap).
/// - Предполагается, что CursorManager создан в первой сцене и помечен DontDestroyOnLoad.
/// </summary>
public class WorldMapBootstrap : MonoBehaviour
{
    void Start()
    {
        Debug.Log("[WorldMapBootstrap] Start");

        if (CursorManager.Instance != null)
        {
            Debug.Log("[WorldMapBootstrap] Forcing EnterUIMode");
            CursorManager.Instance.EnterUIMode();
        }
        else
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
    }
}