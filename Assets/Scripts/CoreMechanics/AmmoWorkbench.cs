// AmmoWorkbench.cs
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Логика крафта конических снарядов.
/// Связывает ввод, расчёт (AmmoCalc) и склады (через AmmoWorkbenchCore).
/// </summary>
[RequireComponent(typeof(AmmoWorkbenchCore))]
public class AmmoWorkbench : MonoBehaviour
{
    private AmmoWorkbenchCore core;

    [Header("Ввод — снаряд")]
    public AmmoCalc.AmmoInput ammoInput = new AmmoCalc.AmmoInput();

    [Header("Ввод — ствол (дополнительная оценка)")]
    public AmmoCalc.BarrelInput barrelInput = new AmmoCalc.BarrelInput();

    // --- Результаты (только для чтения извне) ---
    private AmmoCalc.AmmoOutput output;
    private AmmoCalc.BarrelOutput barrelOutput;
    private List<AmmoCalc.ResourceCost> costs;
    private bool canCraft;
    private string craftError = "";

    // --- Геттеры ---
    public AmmoCalc.AmmoOutput Output => output;
    public AmmoCalc.BarrelOutput BarrelOutput => barrelOutput;
    public IReadOnlyList<AmmoCalc.ResourceCost> Costs => costs;
    public bool CanCraft => canCraft;
    public string CraftError => craftError;

    private void Awake()
    {
        core = GetComponent<AmmoWorkbenchCore>();
    }

    /// <summary>
    /// Полный пересчёт параметров, стоимости, проверка ресурсов.
    /// Вызывать при любом изменении ввода.
    /// </summary>
    public void Recalculate()
    {
        canCraft = false;
        craftError = "";

        // Проверка ядра
        if (!core.IsReady)
        {
            craftError = core.GetReadyError();
            output = null;
            barrelOutput = null;
            costs = null;
            return;
        }

        // Расчёт снаряда
        output = AmmoCalc.Calculate(ammoInput);

        if (!string.IsNullOrEmpty(output.error))
        {
            craftError = output.error;
            barrelOutput = null;
            costs = null;
            return;
        }

        // Расчёт ствола
        barrelOutput = AmmoCalc.CalculateBarrel(output, barrelInput);

        // Стоимость
        costs = AmmoCalc.CalculateCosts(output);

        // Количество
        int count = Mathf.Max(ammoInput.craftCount, 1);

        // Проверка ресурсов
        string resErr = AmmoCalc.ValidateResources(core.ResourcesStorage, costs, count);
        if (!string.IsNullOrEmpty(resErr))
        {
            craftError = resErr;
            canCraft = false;
            return;
        }

        canCraft = true;
    }

    /// <summary>
    /// Крафт. Возвращает true при успехе.
    /// </summary>
    public bool TryCraft()
    {
        Recalculate();

        if (!canCraft)
        {
            Debug.LogWarning($"[AmmoWorkbench] Крафт невозможен: {craftError}");
            return false;
        }

        int count = Mathf.Max(ammoInput.craftCount, 1);

        // Списание ресурсов
        if (!AmmoCalc.ConsumeResources(core.ResourcesStorage, costs, count))
        {
            craftError = "Ошибка списания ресурсов.";
            canCraft = false;
            Debug.LogError($"[AmmoWorkbench] {craftError}");
            return false;
        }

        // Запись в склад боеприпасов
        core.AmmoStorage.AddAmmo(output.ammoCode, count, output.totalShotMassKg);

        Debug.Log($"[AmmoWorkbench] Скрафчено {count} выстрелов: {output.ammoCode}");
        return true;
    }
}