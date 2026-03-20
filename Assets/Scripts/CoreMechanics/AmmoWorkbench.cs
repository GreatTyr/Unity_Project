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

    [Header("Ручной ввод кода")]
    public string manualAmmoCode = "";

    private AmmoCalc.AmmoOutput output;
    private AmmoCalc.BarrelOutput barrelOutput;
    private List<AmmoCalc.ResourceCost> costs;
    private bool canCraft;
    private string craftError = "";

    public AmmoCalc.AmmoOutput Output => output;
    public AmmoCalc.BarrelOutput BarrelOutput => barrelOutput;
    public IReadOnlyList<AmmoCalc.ResourceCost> Costs => costs;
    public bool CanCraft => canCraft;
    public string CraftError => craftError;

    private void Awake()
    {
        EnsureCore();
        ResetToDefaults();
    }

    private void EnsureCore()
    {
        if (core == null)
            core = GetComponent<AmmoWorkbenchCore>();
    }

    public void ResetToDefaults()
    {
        ammoInput = new AmmoCalc.AmmoInput
        {
            chargeType = AmmoCalc.ChargeType.FM,
            shellTier = 1,
            diameterMm = 10f,
            lengthMm = 20f,
            explosiveTier = 1,
            explosiveMassKg = 0f,
            damageElementType = AmmoCalc.DamageElementType.Buckshot,
            buckshotCount = 2,
            damageElementTier = 1,
            damageElementMassKg = 0f,
            areaType = AmmoCalc.AreaType.Point,
            fuzeType = AmmoCalc.FuzeType.No,
            propellantTier = 1,
            propellantMassKg = 0.001f,
            caseTier = 1,
            caseMassKg = 0.001f,
            craftCount = 1
        };

        barrelInput = new AmmoCalc.BarrelInput
        {
            barrelDiameterMm = ammoInput.diameterMm,
            barrelLengthMm = ammoInput.lengthMm * 10f
        };

        manualAmmoCode = "";
        Recalculate();
    }

    public bool TryApplyManualCode()
    {
        EnsureCore();

        if (!AmmoCalc.TryParseCode(manualAmmoCode, out var parsedInput, out var error))
        {
            craftError = error;
            return false;
        }

        ammoInput = parsedInput;
        barrelInput.barrelDiameterMm = ammoInput.diameterMm;
        barrelInput.barrelLengthMm = ammoInput.lengthMm * 10f;

        Recalculate();
        return true;
    }

    public void Recalculate()
    {
        EnsureCore();

        canCraft = false;
        craftError = "";

        if (core == null)
        {
            craftError = "Не найден компонент AmmoWorkbenchCore.";
            output = null;
            barrelOutput = null;
            costs = null;
            return;
        }

        if (!core.IsReady)
        {
            craftError = core.GetReadyError();
            output = null;
            barrelOutput = null;
            costs = null;
            return;
        }

        AmmoCalc.NormalizeInput(ammoInput);
        output = AmmoCalc.Calculate(ammoInput);

        if (output == null)
        {
            craftError = "Ошибка расчёта снаряда.";
            barrelOutput = null;
            costs = null;
            return;
        }

        if (!string.IsNullOrEmpty(output.error))
        {
            craftError = output.error;
            barrelOutput = AmmoCalc.CalculateBarrel(output, barrelInput);
            costs = null;
            return;
        }

        barrelOutput = AmmoCalc.CalculateBarrel(output, barrelInput);
        costs = AmmoCalc.CalculateCosts(output);

        int count = Mathf.Max(ammoInput.craftCount, 1);

        string resErr = AmmoCalc.ValidateResources(core.ResourcesStorage, costs, count);
        if (!string.IsNullOrEmpty(resErr))
        {
            craftError = resErr;
            canCraft = false;
            return;
        }

        canCraft = true;
    }

    public bool TryCraft()
    {
        EnsureCore();
        Recalculate();

        if (!canCraft)
        {
            Debug.LogWarning($"[AmmoWorkbench] Изготовление невозможно: {craftError}");
            return false;
        }

        int count = Mathf.Max(ammoInput.craftCount, 1);

        if (!AmmoCalc.ConsumeResources(core.ResourcesStorage, costs, count))
        {
            craftError = "Ошибка списания ресурсов.";
            canCraft = false;
            Debug.LogError($"[AmmoWorkbench] {craftError}");
            return false;
        }

        core.AmmoStorage.AddAmmo(output.ammoCode, count, output.totalShotMassKg);

        Debug.Log($"[AmmoWorkbench] Изготовлено {count} выстрелов: {output.ammoCode}");
        return true;
    }
}