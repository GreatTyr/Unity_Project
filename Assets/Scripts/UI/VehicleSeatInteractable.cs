using UnityEngine;

public class VehicleSeatInteractable : InteractableBase
{
    [Header("Seat / Vehicle")]
    [Tooltip("Ссылка на корневой объект транспорта/штурвала")]
    public GameObject vehicleRoot;

    private void Reset()
    {
        // Значения по умолчанию при добавлении компонента
        hintText = "Сесть за штурвал";
        interactionType = InteractionType.VehicleEnter;
        keyLabel = "F";
    }

    public override void Interact()
    {
        // ШАГ 1: только лог/заготовка.
        // ШАГ 2: здесь будем переключать управление от PlayerController к vehicle controller.
        Debug.Log($"[VehicleSeatInteractable] Interact -> сесть за штурвал {name} (vehicle={vehicleRoot})");

        // Например, можно временно показать меню-подтверждение вместо мгновенного входа:
        /*
        InteractionMenuUI.Instance.Show(
            "Сесть за штурвал?",
            "Да", () => { /* здесь будет реальный вход в транспорт * / },
            "Отмена", () => { },    // опциональная вторая опция
            () => { }               // cancel
        );
        */
    }
}