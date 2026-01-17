using UnityEngine;

/// <summary>
/// VehicleSeatInteractable (обновлён)
/// - Добавлено поле playerVehicleController (опционально) для инспекторного связывания.
/// - Ищет IControllableVehicle на vehicleRoot (GetComponentInChildren<IControllableVehicle>).
/// - Если не находит, логирует предупреждение.
/// - Вызов EnterVehicle / RequestExit делается через PlayerVehicleController (как и раньше), но теперь с IControllableVehicle.
/// </summary>
public class VehicleSeatInteractable : InteractableBase
{
    [Header("Seat / Vehicle")]
    [Tooltip("Ссылка на корневой объект транспорта/штурвала (где висит PepelacController или другой контроллер).")]
    public GameObject vehicleRoot;

    [Tooltip("Точка, куда будет перемещён игрок при посадке (позиция/ориентация у штурвала).")]
    public Transform seatStandPoint;

    [Header("Player reference (optional)")]
    [Tooltip("Если назначено — используется для входа/выхода. Иначе будет попытка найти объект с тегом 'Player' и получить PlayerVehicleController.")]
    public PlayerVehicleController playerVehicleController; // optional inspector link

    private void Reset()
    {
        hintText = "Сесть за штурвал";
        interactionType = InteractionType.VehicleEnter;
        keyLabel = "F";
    }

    public override void Interact()
    {
        // Получаем PlayerVehicleController (инспектор -> по тегу -> FindObjectOfType)
        PlayerVehicleController player = playerVehicleController;

        if (player == null)
        {
            var g = GameObject.FindWithTag("Player");
            if (g != null) player = g.GetComponent<PlayerVehicleController>();
        }

        if (player == null) player = GameObject.FindObjectOfType<PlayerVehicleController>();

        if (player == null)
        {
            Debug.LogError("[VehicleSeatInteractable] PlayerVehicleController не найден.");
            return;
        }

        if (player.IsInVehicle)
        {
            player.RequestExit();
            return;
        }

        if (vehicleRoot == null)
        {
            Debug.LogWarning($"[VehicleSeatInteractable] vehicleRoot не назначен на {name}");
            return;
        }

        // Ищем IControllableVehicle на vehicleRoot
        IControllableVehicle vehicle = vehicleRoot.GetComponentInChildren<IControllableVehicle>();
        if (vehicle == null)
        {
            Debug.LogWarning($"[VehicleSeatInteractable] На vehicleRoot={vehicleRoot.name} не найден компонент, реализующий IControllableVehicle.");
            return;
        }

        player.EnterVehicle(this, vehicle, seatStandPoint);

        Debug.Log($"[VehicleSeatInteractable] Игрок сел за штурвал {name} (vehicle={vehicleRoot.name})");
    }
}