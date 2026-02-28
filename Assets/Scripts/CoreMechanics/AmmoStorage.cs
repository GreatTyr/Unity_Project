// AmmoStorage.cs
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Склад боеприпасов. Хранит список записей AmmoData.
/// При совпадении кода — суммирует количество.
/// Без лимитов по типам и массе.
/// </summary>
public class AmmoStorage : MonoBehaviour
{
    [SerializeField] private List<AmmoData> entries = new List<AmmoData>();

    public IReadOnlyList<AmmoData> Entries => entries;

    /// <summary>
    /// Добавить выстрелы. Если код уже есть — суммировать.
    /// </summary>
    public void AddAmmo(string code, int quantity, float singleShotMassKg)
    {
        if (quantity <= 0 || string.IsNullOrEmpty(code)) return;

        for (int i = 0; i < entries.Count; i++)
        {
            if (entries[i] != null && entries[i].ammoCode == code)
            {
                entries[i].quantity += quantity;
                entries[i].Recalculate();
                return;
            }
        }

        var newEntry = ScriptableObject.CreateInstance<AmmoData>();
        newEntry.ammoCode = code;
        newEntry.quantity = quantity;
        newEntry.singleShotMassKg = singleShotMassKg;
        newEntry.Recalculate();
        newEntry.name = "Ammo_" + (code.Length > 30 ? code.Substring(0, 30) : code);
        entries.Add(newEntry);
    }

    /// <summary>
    /// Получить количество выстрелов по коду.
    /// </summary>
    public int GetQuantity(string code)
    {
        for (int i = 0; i < entries.Count; i++)
        {
            if (entries[i] != null && entries[i].ammoCode == code)
                return entries[i].quantity;
        }
        return 0;
    }

    /// <summary>
    /// Извлечь выстрелы со склада. Возвращает true при успехе.
    /// </summary>
    public bool RemoveAmmo(string code, int quantity)
    {
        if (quantity <= 0) return true;

        for (int i = 0; i < entries.Count; i++)
        {
            if (entries[i] != null && entries[i].ammoCode == code)
            {
                if (entries[i].quantity < quantity) return false;
                entries[i].quantity -= quantity;
                entries[i].Recalculate();
                if (entries[i].quantity <= 0)
                {
                    entries.RemoveAt(i);
                }
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// Общая масса всех боеприпасов на складе (кг).
    /// </summary>
    public float GetTotalMassKg()
    {
        float total = 0f;
        for (int i = 0; i < entries.Count; i++)
        {
            if (entries[i] != null) total += entries[i].totalMassKg;
        }
        return AmmoCalc.Ceil3(total);
    }
}