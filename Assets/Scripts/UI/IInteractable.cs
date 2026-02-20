/// <summary>
/// Интерфейс для интерактивных объектов.
/// Любой объект, с которым игрок может взаимодействовать, должен реализовать этот интерфейс.
/// </summary>
public interface IInteractable
{
    void Interact();
    void OnHoverEnter();
    void OnHoverExit();
}