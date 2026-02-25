namespace UnityProject.Inventory
{
    /// <summary>
    /// Интерфейс источника инвентаря для вкладок в UI.
    /// </summary>
    public interface IInventorySource
    {
        string DisplayName { get; }
        Inventory MainInventory { get; }
        bool IsAvailable { get; }
        ResourcesStorage Resources { get; }
        AlloyStorage Alloys { get; }
    }
}