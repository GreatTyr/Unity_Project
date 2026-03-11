using UnityEngine;
using Unity.Cinemachine;


public class VehicleSeatInteractable : InteractableBase
{
    [Header("Seat / Vehicle")]
    [Tooltip("Ссылка на корневой объект транспорта.")]
    public GameObject vehicleRoot;

    [Tooltip("Точка, куда будет перемещён игрок при посадке.")]
    public Transform seatStandPoint;

    [Header("Player reference")]
    [Tooltip("Если назначено — используется напрямую. Иначе берётся из PlayerLocator.")]
    public PlayerVehicleController playerVehicleController;

    [Header("Vehicle Camera")]
    public CinemachineVirtualCameraBase vehicleCamera;

    private void Reset()
    {
        hintText = "Сесть за штурвал";
        interactionType = InteractionType.VehicleEnter;
        keyLabel = "F";
    }

    private PlayerVehicleController ResolvePlayer()
    {
        if (playerVehicleController != null)
            return playerVehicleController;

        return PlayerLocator.VehicleController;
    }

    public override void Interact()
    {
        var player = ResolvePlayer();
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

        IControllableVehicle vehicle = vehicleRoot.GetComponentInChildren<IControllableVehicle>();
        if (vehicle == null)
        {
            Debug.LogWarning($"[VehicleSeatInteractable] IControllableVehicle не найден на {vehicleRoot.name}");
            return;
        }

        player.EnterVehicle(this, vehicle, seatStandPoint);
        Debug.Log($"[VehicleSeatInteractable] Игрок сел за штурвал {name}");
    }

    public override void OnHoverEnter()
    {
        var player = ResolvePlayer();
        if (player != null && player.IsInVehicle)
            return;

        base.OnHoverEnter();
    }

    public override void OnHoverExit()
    {
        var player = ResolvePlayer();
        if (player != null && player.IsInVehicle)
            return;

        base.OnHoverExit();
    }
}