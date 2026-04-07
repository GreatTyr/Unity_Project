using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Логика крафта ядер.
/// Связывает ввод, расчёт (CannonballCalc) и склады.
/// </summary>
[RequireComponent(typeof(CannonballWorkbenchCore))]
public class CannonballWorkbench : MonoBehaviour
{
    private CannonballWorkbenchCore core;

    [Header("Ввод — ядро")]
    public CannonballCalc.CannonballInput cannonballInput = new CannonballCalc.CannonballInput();

    [Header("Ввод — ствол (оценка для верстака)")]
    public CannonballCalc.BarrelInput barrelInput = new CannonballCalc.BarrelInput();

    [Header("Ручной ввод кода")]
    public string manualAmmoCode = "";

    private CannonballCalc.CannonballOutput output;
    private CannonballCalc.BarrelOutput barrelOutput;
    private List<CannonballCalc.ResourceCost> costs;
    private bool canCraft;

    private readonly List<string> errors = new List<string>();
    private readonly List<string> warnings = new List<string>();

    public CannonballCalc.CannonballOutput Output => output;
    public CannonballCalc.BarrelOutput BarrelOutput => barrelOutput;
    public IReadOnlyList<CannonballCalc.ResourceCost> Costs => costs;
    public bool CanCraft => canCraft;
    public IReadOnlyList<string> Errors => errors;
    public IReadOnlyList<string> Warnings => warnings;

    private void Awake()
    {
        EnsureCore();
        ResetToDefaults();
    }

    private void EnsureCore()
    {
        if (core == null)
            core = GetComponent<CannonballWorkbenchCore>();
    }

    public void ResetToDefaults()
    {
        cannonballInput = new CannonballCalc.CannonballInput
        {
            chargeType = CannonballCalc.ChargeType.FM,
            shellTier = 1,
            diameterMm = 10f,
            explosiveTier = 1,
            explosiveMassKg = 0f,
            damageElementType = CannonballCalc.DamageElementType.Pellet,
            damageElementTier = 1,
            damageElementMassKg = 0f,
            areaType = CannonballCalc.AreaType.Point,
            fuzeType = CannonballCalc.FuzeType.No,
            propellantTier = 1,
            propellantMassKg = 0.001f,
            craftCount = 1
        };

        barrelInput = new CannonballCalc.BarrelInput
        {
            barrelDiameterMm = cannonballInput.diameterMm,
            barrelLengthMm = Mathf.Ceil(cannonballInput.diameterMm * 10f),
            shotAngleDeg = 45f
        };

        manualAmmoCode = "";
        Recalculate();
    }

    public bool TryApplyManualCode()
    {
        EnsureCore();

        if (!CannonballValidator.TryParseCode(manualAmmoCode, out var parsedInput, out var error))
        {
            errors.Clear();
            warnings.Clear();
            errors.Add(error);
            return false;
        }

        cannonballInput = parsedInput;
        barrelInput.barrelDiameterMm = cannonballInput.diameterMm;
        barrelInput.barrelLengthMm = Mathf.Ceil(cannonballInput.diameterMm * 10f);
        barrelInput.shotAngleDeg = 45f;

        Recalculate();
        return true;
    }

    public void Recalculate()
    {
        EnsureCore();

        canCraft = false;
        errors.Clear();
        warnings.Clear();

        if (core == null)
        {
            errors.Add("Не найден компонент CannonballWorkbenchCore.");
            output = null;
            barrelOutput = null;
            costs = null;
            return;
        }

        if (!core.IsReady)
        {
            errors.Add(core.GetReadyError());
            output = null;
            barrelOutput = null;
            costs = null;
            return;
        }

        CannonballCalc.NormalizeInput(cannonballInput);
        output = CannonballCalc.Calculate(cannonballInput);

        if (output == null)
        {
            errors.Add("Ошибка расчёта ядра.");
            barrelOutput = null;
            costs = null;
            return;
        }

        if (!string.IsNullOrEmpty(output.error))
        {
            errors.Add(output.error);
            barrelOutput = null;
            costs = null;
            return;
        }

        barrelOutput = CannonballCalc.CalculateBarrel(output, barrelInput);
        costs = CannonballCalc.CalculateCosts(output);

        if (output.weakExplosiveCharge)
            warnings.Add(output.weakExplosiveChargeWarning);

        if (barrelOutput != null && !barrelOutput.valid)
            errors.Add(barrelOutput.error);

        int craftCount = Mathf.Max(cannonballInput.craftCount, 1);

        string resErr = CannonballCalc.ValidateResources(core.ResourcesStorage, costs, craftCount);
        if (!string.IsNullOrEmpty(resErr))
            errors.Add(resErr);

        canCraft = errors.Count == 0;
    }

    public bool TryCraft()
    {
        EnsureCore();
        Recalculate();

        if (!canCraft)
        {
            Debug.LogWarning("[CannonballWorkbench] Изготовление невозможно.");
            return false;
        }

        int count = Mathf.Max(cannonballInput.craftCount, 1);

        if (!CannonballCalc.ConsumeResources(core.ResourcesStorage, costs, count))
        {
            errors.Clear();
            errors.Add("Ошибка списания ресурсов.");
            canCraft = false;
            Debug.LogError("[CannonballWorkbench] Ошибка списания ресурсов.");
            return false;
        }

        core.AmmoStorage.AddAmmo(output.ammoCode, count, output.totalAmmoMassKg);

        Debug.Log($"[CannonballWorkbench] Изготовлено {count} ядер: {output.ammoCode}");
        return true;
    }
}