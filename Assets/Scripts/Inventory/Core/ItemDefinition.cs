using UnityEngine;

namespace UnityProject.Inventory
{
    public enum EquipmentSlotType
    {
        None = 0,
        Head,
        Body,
        Legs,
        WeaponMain,
        WeaponSecondary,
        Backpack,
        Pouch,
        // дополняй по необходимости
    }

    [CreateAssetMenu(
        fileName = "ItemDefinition",
        menuName = "Inventory/Item Definition",
        order = 0)]
    public class ItemDefinition : ScriptableObject
    {
        [Header("Identification")]
        [Tooltip("Уникальный ID предмета (для сохранений/ссылок).")]
        public string itemId;

        [Header("Visual")]
        public string displayName;
        public Sprite icon;

        [Header("Grid Size")]
        [Tooltip("Ширина предмета в клетках по X.")]
        public int gridWidth = 1;
        [Tooltip("Высота предмета в клетках по Y.")]
        public int gridHeight = 1;
        [Tooltip("Можно ли поворачивать предмет (менять местами ширину/высоту).")]
        public bool canRotate = true;

        [Header("Stacking")]
        [Tooltip("Можно ли стакать этот предмет.")]
        public bool stackable = false;
        [Tooltip("Максимальное количество в стеке.")]
        public int maxStack = 1;

        [Header("Equipment")]
        [Tooltip("Является ли предмет экипируемым.")]
        public bool isEquippable = false;
        [Tooltip("Слот, куда надевается предмет (если он экипируемый).")]
        public EquipmentSlotType equipmentSlotType = EquipmentSlotType.None;

        [Header("Container")]
        [Tooltip("Является ли этот предмет контейнером (внутренний инвентарь: сумка, рюкзак).")]
        public bool isContainer = false;
        [Tooltip("Ширина внутреннего контейнера (в клетках).")]
        public int containerWidth = 0;
        [Tooltip("Высота внутреннего контейнера (в клетках).")]
        public int containerHeight = 0;

        [Header("Gameplay (optional)")]
        public string category;   // оружие, броня, расходник и т.п.
        public float weight = 0f;
        public int price = 0;
        public int rarity = 0;
    }
}