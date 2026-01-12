// Interactable.cs
// Интерфейс для интерактивных объектов.
// Любой объект, с которым игрок может взаимодействовать, должен реализовать этот интерфейс.
public interface Interactable
{
    // Вызывается, когда игрок активирует объект (нажатием F)
    void Interact();

    // Вызывается, когда игрок подошёл/навёлся на объект (hover / proximity enter)
    void OnHoverEnter();

    // Вызывается, когда игрок отошёл/перестал наведение (hover / proximity exit)
    void OnHoverExit();
}