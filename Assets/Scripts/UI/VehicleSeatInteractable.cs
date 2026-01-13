using UnityEngine;

public class VehicleSeatInteractable : InteractableBase
{
    [Header("Seat / Vehicle")]
    [Tooltip("Ссылка на корневой объект транспорта/штурвала")]
    public GameObject vehicleRoot;

    [Tooltip("Точка, куда будет перемещён игрок при посадке (позиция/ориентация у штурвала)")]
    public Transform seatStandPoint;

    private void Reset()
    {
        // Значения по умолчанию при добавлении компонента
        hintText = "Сесть за штурвал";
        interactionType = InteractionType.VehicleEnter;
        keyLabel = "F";
    }

    public override void Interact()
    {
        if (vehicleRoot == null)
        {
            Debug.LogWarning($"[VehicleSeatInteractable] vehicleRoot не назначен на {name}");
            return;
        }

        // Пытаемся найти контроллер транспорта на корне
        var vehicleController = vehicleRoot.GetComponent<PepelacController>();
        if (vehicleController == null)
        {
            Debug.LogWarning($"[VehicleSeatInteractable] На vehicleRoot={vehicleRoot.name} не найден PepelacController.");
            return;
        }

        // Ищем игрока в сцене (можно заменить на более строгую ссылку, если есть GameManager)
        var player = FindObjectOfType<PlayerVehicleController>();
        if (player == null)
        {
            Debug.LogError("[VehicleSeatInteractable] PlayerVehicleController не найден в сцене.");
            return;
        }

        // Передаём управление менеджеру
        player.EnterVehicle(this, vehicleController, seatStandPoint);

        Debug.Log($"[VehicleSeatInteractable] Игрок сел за штурвал {name} (vehicle={vehicleRoot.name})");
    }
}