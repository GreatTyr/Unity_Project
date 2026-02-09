namespace UnityProject.Inventory
{
    /// <summary>
    /// Интерфейс владельца инвентаря (игрок, транспорт, контейнер).
    /// </summary>
    public interface IInventoryOwner
    {
        Inventory MainInventory { get; }
        EquipmentSlots Equipment { get; }
        Hotbar Hotbar { get; }
    }
}