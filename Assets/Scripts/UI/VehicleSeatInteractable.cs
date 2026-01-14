using UnityEngine;

/// <summary>
/// VehicleSeatInteractable
/// ----------------------------------------
/// Интерактивный объект "сиденье / штурвал".
/// При наведении и нажатии F:
/// - Если игрок НЕ в транспорте → посажает за штурвал (EnterVehicle).
/// - Если игрок УЖЕ в транспорте → инициирует выход (RequestExit).
///
/// Таким образом, вход/выход привязаны к одному и тому же объекту и к одной кнопке,
/// но без двойной обработки (PlayerVehicleController сам F больше не слушает).
/// </summary>
public class VehicleSeatInteractable : InteractableBase
{
    [Header("Seat / Vehicle")]
    [Tooltip("Ссылка на корневой объект транспорта/штурвала (где висит PepelacController или другой контроллер).")]
    public GameObject vehicleRoot;

    [Tooltip("Точка, куда будет перемещён игрок при посадке (позиция/ориентация у штурвала).")]
    public Transform seatStandPoint;

    private void Reset()
    {
        // Значения по умолчанию при добавлении компонента в инспекторе
        hintText = "Сесть за штурвал";
        interactionType = InteractionType.VehicleEnter;
        keyLabel = "F";
    }

    /// <summary>
    /// Основной метод взаимодействия:
    /// - если игрок в пешем режиме → садим за штурвал;
    /// - если уже сидит → выходим из транспорта.
    /// </summary>
    public override void Interact()
    {
        // Ищем контроллер игрока в сцене.
        // В будущем можно заменить на ссылку через Singleton/GameManager.
        var player = GameObject.FindObjectOfType<PlayerVehicleController>();
        if (player == null)
        {
            Debug.LogError("[VehicleSeatInteractable] PlayerVehicleController не найден в сцене.");
            return;
        }

        // === Если игрок уже за штурвалом (в транспорте) ===
        // В этом случае взаимодействие (F) воспринимаем как команду "Выйти".
        if (player.IsInVehicle)
        {
            Debug.Log("[VehicleSeatInteractable] Игрок уже за штурвалом -> команда на выход из транспорта.");
            player.RequestExit();
            return;
        }

        // === Игрок НЕ в транспорте — значит, это команда "Сесть за штурвал" ===

        if (vehicleRoot == null)
        {
            Debug.LogWarning($"[VehicleSeatInteractable] vehicleRoot не назначен на {name}");
            return;
        }

        // Ищем контроллер транспорта на корневом объекте.
        // Сейчас это PepelacController, но в будущем можно использовать интерфейс IVehicleController.
        var vehicleController = vehicleRoot.GetComponent<PepelacController>();
        if (vehicleController == null)
        {
            Debug.LogWarning($"[VehicleSeatInteractable] На vehicleRoot={vehicleRoot.name} не найден PepelacController.");
            return;
        }

        // Передаём управление менеджеру игрока: вход в транспорт
        player.EnterVehicle(this, vehicleController, seatStandPoint);

        Debug.Log($"[VehicleSeatInteractable] Игрок сел за штурвал {name} (vehicle={vehicleRoot.name})");
    }
}