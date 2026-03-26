using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Логика крафта конических боеприпасов.
/// Связывает ввод, расчёт (AmmoCalc) и склады (через AmmoWorkbenchCore).
/// </summary>
[RequireComponent(typeof(AmmoWorkbenchCore))]
public class AmmoWorkbench : MonoBehaviour
{
    private AmmoWorkbenchCore core;

    [Header("Баллистика боеприпаса")]
    [SerializeField] private float effectiveGravityA = 8f;
    [SerializeField] private float effectiveGravityB = 145f;

    public float EffectiveGravityA => effectiveGravityA;
    public float EffectiveGravityB => effectiveGravityB;

    [Header("Ввод — боеприпас")]
    public AmmoCalc.AmmoInput ammoInput = new AmmoCalc.AmmoInput();

    [Header("Ввод — ствол (оценка для верстака)")]
    public AmmoCalc.BarrelInput barrelInput = new AmmoCalc.BarrelInput();

    [Header("Ручной ввод кода")]
    public string manualAmmoCode = "";

    private AmmoCalc.AmmoOutput output;
    private AmmoCalc.BarrelOutput barrelOutput;
    private List<AmmoCalc.ResourceCost> costs;
    private bool canCraft;

    private readonly List<string> errors = new List<string>();
    private readonly List<string> warnings = new List<string>();

    public AmmoCalc.AmmoOutput Output => output;
    public AmmoCalc.BarrelOutput BarrelOutput => barrelOutput;
    public IReadOnlyList<AmmoCalc.ResourceCost> Costs => costs;
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
            barrelLengthMm = ammoInput.lengthMm * 10f,
            shotAngleDeg = 45f
        };

        manualAmmoCode = "";
        Recalculate();
    }

    public bool TryApplyManualCode()
    {
        EnsureCore();

        if (!AmmoValidator.TryParseCode(manualAmmoCode, out var parsedInput, out var error))
        {
            errors.Clear();
            warnings.Clear();
            errors.Add(error);
            return false;
        }

        ammoInput = parsedInput;
        barrelInput.barrelDiameterMm = ammoInput.diameterMm;
        barrelInput.barrelLengthMm = ammoInput.lengthMm * 10f;
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
            errors.Add("Не найден компонент AmmoWorkbenchCore.");
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

        AmmoCalc.NormalizeInput(ammoInput);
        output = AmmoCalc.Calculate(ammoInput, effectiveGravityA, effectiveGravityB);

        if (output == null)
        {
            errors.Add("Ошибка расчёта боеприпаса.");
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

        barrelOutput = AmmoCalc.CalculateBarrel(output, barrelInput);
        costs = AmmoCalc.CalculateCosts(output);

        if (output.weakExplosiveCharge)
            warnings.Add(output.weakExplosiveChargeWarning);

        if (output.caseStrength < output.propulsionForce)
            errors.Add("Прочность гильзы ниже выталкивающей силы. Увеличьте массу/тир гильзы или уменьшите метательный заряд.");

        if (barrelOutput != null && !barrelOutput.valid)
            errors.Add(barrelOutput.error);

        int craftCount = Mathf.Max(ammoInput.craftCount, 1);

        string resErr = AmmoCalc.ValidateResources(core.ResourcesStorage, costs, craftCount);
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
            Debug.LogWarning("[AmmoWorkbench] Изготовление невозможно.");
            return false;
        }

        int count = Mathf.Max(ammoInput.craftCount, 1);

        if (!AmmoCalc.ConsumeResources(core.ResourcesStorage, costs, count))
        {
            errors.Clear();
            errors.Add("Ошибка списания ресурсов.");
            canCraft = false;
            Debug.LogError("[AmmoWorkbench] Ошибка списания ресурсов.");
            return false;
        }

        core.AmmoStorage.AddAmmo(output.ammoCode, count, output.totalAmmoMassKg);

        Debug.Log($"[AmmoWorkbench] Изготовлено {count} боеприпасов: {output.ammoCode}");
        return true;
    }
}