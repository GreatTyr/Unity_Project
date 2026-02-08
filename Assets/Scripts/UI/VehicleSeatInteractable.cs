using UnityEngine;

/// <summary>
/// VehicleSeatInteractable — интерактив для посадки/выхода из транспорта через штурвал.
/// - Работает через PlayerVehicleController и IControllableVehicle.
/// - Если игрок уже в транспорте, hover по штурвалу не подсвечивает объект
///   и не показывает подсказку (чтобы не мешать пилотированию).
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
    public PlayerVehicleController playerVehicleController; // опциональная ссылка из инспектора

    private void Reset()
    {
        hintText = "Сесть за штурвал";
        interactionType = InteractionType.VehicleEnter;
        keyLabel = "F";
    }

    /// <summary>
    /// Получить PlayerVehicleController (из инспектора / по тегу / через FindObjectOfType).
    /// </summary>
    private PlayerVehicleController ResolvePlayer()
    {
        PlayerVehicleController player = playerVehicleController;

        if (player == null)
        {
            var g = GameObject.FindWithTag("Player");
            if (g != null) player = g.GetComponent<PlayerVehicleController>();
        }

        if (player == null)
        {
            // Современный API поиска первого объекта нужного типа
            player = UnityEngine.Object.FindFirstObjectByType<PlayerVehicleController>();
        }

        return player;
    }

    public override void Interact()
    {
        var player = ResolvePlayer();
        if (player == null)
        {
            Debug.LogError("[VehicleSeatInteractable] PlayerVehicleController не найден.");
            return;
        }

        // Если игрок уже в транспорте — трактуем Interact как запрос выхода.
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

    // --------------------------
    // Поведение при наведении
    // --------------------------

    /// <summary>
    /// При наведении на штурвал:
    /// - если игрок НЕ в транспорте -> ведём себя как обычный интерактив (подсветка).
    /// - если игрок УЖЕ в транспорте -> игнорируем hover (никакого свечения).
    /// Подсказку по тексту показывает PlayerLookInteractor.
    /// </summary>
    public override void OnHoverEnter()
    {
        var player = ResolvePlayer();

        if (player != null && player.IsInVehicle)
        {
            // Игрок уже пилотирует транспорт -> при наведении на штурвал
            // не подсвечиваем объект и не трогаем UI-подсказки.
            return;
        }

        base.OnHoverEnter();
    }

    /// <summary>
    /// Аналогично OnHoverEnter: при уходе курсора со штурвала,
    /// если игрок в транспорте — ничего не делаем.
    /// </summary>
    public override void OnHoverExit()
    {
        var player = ResolvePlayer();

        if (player != null && player.IsInVehicle)
        {
            return;
        }

        base.OnHoverExit();
    }
}