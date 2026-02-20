using UnityEngine;
using UnityProject.Inventory;

/// <summary>
/// Централизованный кэш ссылок на компоненты игрока.
/// Один раз находит Player и раздаёт ссылки всем, кто просит.
/// Избавляет от FindWithTag("Player") в 8+ файлах.
///
/// Использование:
///   PlayerLocator.PlayerInventory   — вместо FindWithTag("Player").GetComponent<PlayerInventory>()
///   PlayerLocator.VehicleController — вместо FindWithTag("Player").GetComponent<PlayerVehicleController>()
///
/// Автоматически ищет при первом обращении (lazy init).
/// Можно вызвать PlayerLocator.Initialize() явно из bootstrap-скрипта.
/// </summary>
public static class PlayerLocator
{
    private static GameObject cachedPlayerGo;
    private static PlayerInventory cachedInventory;
    private static PlayerVehicleController cachedVehicleController;
    private static PlayerController cachedPlayerController;

    /// <summary>
    /// GameObject игрока (тег "Player").
    /// </summary>
    public static GameObject PlayerObject
    {
        get
        {
            EnsureInitialized();
            return cachedPlayerGo;
        }
    }

    /// <summary>
    /// Компонент PlayerInventory.
    /// </summary>
    public static PlayerInventory Inventory
    {
        get
        {
            EnsureInitialized();
            return cachedInventory;
        }
    }

    /// <summary>
    /// Компонент PlayerVehicleController.
    /// </summary>
    public static PlayerVehicleController VehicleController
    {
        get
        {
            EnsureInitialized();
            return cachedVehicleController;
        }
    }

    /// <summary>
    /// Компонент PlayerController.
    /// </summary>
    public static PlayerController Controller
    {
        get
        {
            EnsureInitialized();
            return cachedPlayerController;
        }
    }

    /// <summary>
    /// Явная инициализация. Можно вызвать из GameplayBootstrap.Start().
    /// </summary>
    public static void Initialize()
    {
        cachedPlayerGo = GameObject.FindWithTag("Player");

        if (cachedPlayerGo != null)
        {
            cachedInventory = cachedPlayerGo.GetComponent<PlayerInventory>();
            cachedVehicleController = cachedPlayerGo.GetComponent<PlayerVehicleController>();
            cachedPlayerController = cachedPlayerGo.GetComponent<PlayerController>();
        }
        else
        {
            Debug.LogWarning("[PlayerLocator] Объект с тегом 'Player' не найден в сцене.");
            cachedInventory = null;
            cachedVehicleController = null;
            cachedPlayerController = null;
        }
    }

    /// <summary>
    /// Сброс кэша. Вызывать при смене сцены.
    /// </summary>
    public static void Reset()
    {
        cachedPlayerGo = null;
        cachedInventory = null;
        cachedVehicleController = null;
        cachedPlayerController = null;
    }

    private static void EnsureInitialized()
    {
        // Если кэш пуст или объект уничтожен — переинициализируем
        if (cachedPlayerGo == null)
            Initialize();
    }
}