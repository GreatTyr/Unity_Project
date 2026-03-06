using UnityEngine;

public struct PepelacStats
{
    public float totalEnergyOutput;
    public float totalEnergyConsumption;
    public float energyBalance;
    public float totalFuelConsumption;
    public float fuelRemaining;
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
        float dt = Time.fixedDeltaTime;
        var modules = grid.GetAllModules();

        // --- ФАЗА 1: СБОР ДАННЫХ И ЗАПРОСОВ ---
        float totalEnergyDemand = 0f;
        float totalFuelDemand = 0f;
        float totalMass = 0f;
        float totalEnergyGenerated = 0f;

        foreach (var m in modules)
        {
            var status = m.GetStatus();
            totalEnergyDemand += status.energyDemandThisFrame;
            totalFuelDemand += status.fuelDemandThisFrame;
            totalMass += status.massKg;

            // Авансовый сбор энергии (генераторы уже произвели её в прошлом кадре)
            totalEnergyGenerated += status.energyOutputThisFrame;
        }

        // --- ФАЗА 2: ВЫДЕЛЕНИЕ ТОПЛИВА ---
        // TODO: Вычесть totalFuelDemand из FuelStorage
        float fuelRatio = 1f; // Заглушка

        // --- ФАЗА 3: РАСПРЕДЕЛЕНИЕ ЭНЕРГИИ И TICK ---
        float energyRatio = totalEnergyDemand > 0 ? Mathf.Clamp01(totalEnergyGenerated / totalEnergyDemand) : 1f;

        int overheatedCount = 0;
        int activeCount = 0;

        foreach (var m in modules)
        {
            var status = m.GetStatus();

            float providedFuel = status.fuelDemandThisFrame * fuelRatio;
            float providedEnergy = status.energyDemandThisFrame * energyRatio;

            m.Tick(dt, providedFuel, providedEnergy);

            if (m.IsOverheated) overheatedCount++;
            if (m.IsActive) activeCount++;
        }

        // --- ФАЗА 4: ПУБЛИКАЦИЯ РЕЗУЛЬТАТОВ ---
        CurrentStats = new PepelacStats
        {
            totalModulesMassKg = totalMass,
            totalEnergyConsumption = totalEnergyDemand / dt,
            totalEnergyOutput = totalEnergyGenerated / dt,
            energyBalance = (totalEnergyGenerated - totalEnergyDemand) / dt,
            totalFuelConsumption = totalFuelDemand / dt,
            fuelRemaining = 100f, // Заглушка
            activeModules = activeCount,
            overheatedModulesCount = overheatedCount,
            centerOfMass = Vector2.zero // Заглушка: считать только при изменении сетки
        };
    }
}