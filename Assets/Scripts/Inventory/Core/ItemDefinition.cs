using System.Collections.Generic;
using UnityEngine;

namespace UnityProject.Inventory
{
    /// <summary>
    /// Категория предмета для фильтрации и сортировки в инвентаре.
    /// </summary>
    public enum ItemCategory
    {
        Weapon = 0,
        Armor = 1,
        Module = 2,
        Resource = 3,
        Other = 4
    }

    /// <summary>
    /// Тип слота экипировки на кукле персонажа.
    /// </summary>
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
    }

    /// <summary>
    /// ScriptableObject — паспорт типа предмета.
    /// Определяет визуал, свойства стакинга, экипировки, вес и цену.
    /// </summary>
    [CreateAssetMenu(
        fileName = "ItemDefinition",
        menuName = "Inventory/Item Definition",
        order = 0)]
    public class ItemDefinition : ScriptableObject
    {
        [Header("Идентификация")]
        [Tooltip("Уникальный ID предмета (для сохранения и поиска).")]
        public string itemId;

        [Header("Визуал")]
        [Tooltip("Отображаемое имя предмета.")]
        public string displayName;

        [Tooltip("Иконка предмета для UI.")]
        public Sprite icon;

        [Header("Категория")]
        [Tooltip("Категория предмета для фильтрации в инвентаре.")]
        public ItemCategory itemCategory = ItemCategory.Other;

        [Header("Стакинг")]
        [Tooltip("Может ли предмет быть в стеке.")]
        public bool stackable = false;

        [Tooltip("Максимальное количество в стеке.")]
        public int maxStack = 1;

        [Header("Экипировка")]
        [Tooltip("Является ли предмет экипируемым.")]
        public bool isEquippable = false;

        [Tooltip("Слот, куда надевается предмет (если он экипируемый).")]
        public EquipmentSlotType equipmentSlotType = EquipmentSlotType.None;

        [Header("Геймплей")]
        [Tooltip("Вес предмета (кг). Используется для расчёта грузоподъёмности.")]
        public float weight = 0f;

        [Tooltip("Цена предмета (для будущей торговли).")]
        public int price = 0;

        [Tooltip("Редкость предмета (0 = обычный).")]
        public int rarity = 0;

        [Tooltip("Произвольные теги для поиска и фильтрации.")]
        public List<string> tags = new List<string>();
    }
}