using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Склад боеприпасов.
/// Хранит только код боеприпаса, массу одного боеприпаса, количество и массу стака.
/// При совпадении кода — суммирует количество и массу стака.
/// </summary>
public class AmmoStorage : MonoBehaviour
{
    [System.Serializable]
    public class AmmoEntry
    {
        [Tooltip("Код боеприпаса")]
        public string ammoCode;

        [Tooltip("Масса одного боеприпаса (кг)")]
        public float singleAmmoMassKg;

        [Tooltip("Количество боеприпасов в стаке")]
        public int quantity;

        [Tooltip("Суммарная масса стака (кг)")]
        public float totalMassKg;
    }

    [SerializeField] private List<AmmoEntry> entries = new List<AmmoEntry>();

    public IReadOnlyList<AmmoEntry> Entries => entries;

    /// <summary>
    /// Добавить боеприпасы. Если код уже есть — суммировать.
    /// </summary>
    public void AddAmmo(string code, int quantity, float singleAmmoMassKg)
    {
        if (quantity <= 0 || string.IsNullOrEmpty(code)) return;

        float normalizedSingleMass = AmmoCalc.Ceil3(Mathf.Max(0f, singleAmmoMassKg));
        float addedMass = AmmoCalc.Ceil3(normalizedSingleMass * quantity);

        for (int i = 0; i < entries.Count; i++)
        {
            if (entries[i] != null && entries[i].ammoCode == code)
            {
                entries[i].quantity += quantity;
                entries[i].singleAmmoMassKg = normalizedSingleMass;
                entries[i].totalMassKg = AmmoCalc.Ceil3(entries[i].singleAmmoMassKg * entries[i].quantity);
                return;
            }
        }

        var newEntry = new AmmoEntry
        {
            ammoCode = code,
            singleAmmoMassKg = normalizedSingleMass,
            quantity = quantity,
            totalMassKg = addedMass
        };

        entries.Add(newEntry);
    }

    /// <summary>
    /// Получить количество боеприпасов по коду.
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
    /// Извлечь боеприпасы со склада. Возвращает true при успехе.
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
                entries[i].totalMassKg = AmmoCalc.Ceil3(entries[i].singleAmmoMassKg * entries[i].quantity);

                if (entries[i].quantity <= 0)
                    entries.RemoveAt(i);

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