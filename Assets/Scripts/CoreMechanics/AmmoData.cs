// AmmoData.cs
using UnityEngine;

/// <summary>
/// ScriptableObject — запись об одном типе выстрелов.
/// </summary>
[CreateAssetMenu(fileName = "AmmoData", menuName = "Ammo/AmmoData")]
public class AmmoData : ScriptableObject
{
    [Tooltip("Код выстрела (26 элементов через дефис)")]
    public string ammoCode;

    [Tooltip("Количество выстрелов данного типа")]
    public int quantity;

    [Tooltip("Масса одного выстрела (кг)")]
    public float singleShotMassKg;

    [Tooltip("Суммарная масса всех выстрелов (кг)")]
    public float totalMassKg;

    public void Recalculate()
    {
        totalMassKg = AmmoCalc.Ceil3(quantity * singleShotMassKg);
    }
}