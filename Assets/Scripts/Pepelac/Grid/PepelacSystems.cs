using UnityEngine;
using System.Collections.Generic;

public struct PepelacStats
{
    public float totalEnergyOutput;
    public float totalEnergyConsumption;
    public float energyBalance;

    public float currentEnergyStored;
    public float maxEnergyStorage;

    public float totalFuelConsumption;
    public float fuelRemaining;
    public float maxFuelStorage;

    public float totalModulesMassKg;
    public Vector2 centerOfMass;

    public int activeModules;
    public int overheatedModulesCount;
}

[RequireComponent(typeof(PepelacGrid))]
public class PepelacSystems : MonoBehaviour
{
    private PepelacGrid grid;

    public PepelacStats CurrentStats { get; private set; }

    private void Awake()
    {
        grid = GetComponent<PepelacGrid>();
    }

    private void FixedUpdate()
    {
        var modules = grid.GetAllModules();

        float totalMass = 0f;
        int activeCount = 0;

        for (int i = 0; i < modules.Count; i++)
        {
            var state = modules[i];
            if (state == null) continue;

            var crafted = state.GetComponent<CraftedModule>();
            var data = crafted != null ? crafted.GetData() : null;

            if (data != null)
                totalMass += Mathf.Max(0f, data.totalMassKg);

            if (state.alive && state.isOn)
                activeCount++;
        }

        CurrentStats = new PepelacStats
        {
            totalEnergyOutput = 0f,
            totalEnergyConsumption = 0f,
            energyBalance = 0f,

            currentEnergyStored = 0f,
            maxEnergyStorage = 0f,

            totalFuelConsumption = 0f,
            fuelRemaining = 0f,
            maxFuelStorage = 0f,

            totalModulesMassKg = totalMass,
            centerOfMass = Vector2.zero, // TODO: расчет CoM в новой runtime-модели

            activeModules = activeCount,
            overheatedModulesCount = 0
        };
    }
}