namespace UnityProject.Inventory
{
    /// <summary>
    /// Интерфейс владельца инвентаря (игрок, транспорт, контейнер и т.п.).
    /// </summary>
    public interface IInventoryOwner
    {
        InventoryGrid MainInventory { get; }
        EquipmentSlots Equipment { get; }
        Hotbar Hotbar { get; }
    }
}