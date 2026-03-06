using UnityEngine;
using System.Collections.Generic;

public struct PepelacStats
{
    public float totalEnergyOutput;       // Энергии произведено в кадр (E/s)
    public float totalEnergyConsumption;  // Энергии потреблено в кадр (E/s)
    public float energyBalance;           // Разница (E/s)

    public float currentEnergyStored;     // Запас во всех батареях
    public float maxEnergyStorage;        // Макс емкость всех батарей

    public float totalFuelConsumption;    // Топлива сожжено в кадр (кг/с)
    public float fuelRemaining;           // Текущий запас топлива во всех баках
    public float maxFuelStorage;          // Макс емкость всех баков

    public float totalModulesMassKg;      // Суммарная масса модулей
    public Vector2 centerOfMass;          // Центр масс на сетке

    public int activeModules;
    public int overheatedModulesCount;
}

[RequireComponent(typeof(PepelacGrid))]
public class PepelacSystems : MonoBehaviour
{
    private PepelacGrid grid;

    public PepelacStats CurrentStats { get; private set; }

    // Кешированные списки для быстрой работы
    private List<RuntimeFuelTank> fuelTanks = new List<RuntimeFuelTank>();
    private List<RuntimeEnergyStorage> energyStorages = new List<RuntimeEnergyStorage>();

    private void Awake()
    {
        grid = GetComponent<PepelacGrid>();
    }

    private void FixedUpdate()
    {
        float dt = Time.fixedDeltaTime;
        var modules = grid.GetAllModules();

        // Очищаем списки баков и батарей на этот кадр
        fuelTanks.Clear();
        energyStorages.Clear();

        // ==========================================
        // ФАЗА 1: СБОР ДАННЫХ И ЗАПРОСОВ
        // ==========================================
        float totalEnergyDemand = 0f;
        float totalFuelDemand = 0f;
        float totalMass = 0f;

        float totalEnergyGenerated = 0f;

        float currentFuel = 0f;
        float maxFuel = 0f;
        float currentEnergy = 0f;
        float maxEnergy = 0f;

        foreach (var m in modules)
        {
            var status = m.GetStatus();

            totalEnergyDemand += status.energyDemandThisFrame;
            totalFuelDemand += status.fuelDemandThisFrame;
            totalMass += status.massKg;

            // Авансовый сбор энергии (генераторы уже произвели её в прошлом кадре)
            totalEnergyGenerated += status.energyOutputThisFrame;

            // Собираем статы хранилищ и сортируем их по спискам
            if (m is RuntimeFuelTank tank)
            {
                fuelTanks.Add(tank);
                currentFuel += status.currentFuelStorage;
                maxFuel += status.maxFuelStorage;
            }
            else if (m is RuntimeEnergyStorage storage)
            {
                energyStorages.Add(storage);
                currentEnergy += status.currentEnergyStorage;
                maxEnergy += status.maxEnergyStorage;
            }
        }

        // ==========================================
        // ФАЗА 2: ДОБЫЧА ТОПЛИВА (Для Генераторов)
        // ==========================================
        float actualFuelExtracted = 0f;

        // Если кто-то просит топливо, пытаемся достать его из баков
        if (totalFuelDemand > 0f)
        {
            float remainingToExtract = totalFuelDemand;

            foreach (var tank in fuelTanks)
            {
                if (remainingToExtract <= 0f) break;

                // Просим у бака топливо
                float extracted = tank.ConsumeFuel(remainingToExtract);
                actualFuelExtracted += extracted;
                remainingToExtract -= extracted;
            }
        }

        // Высчитываем общий КПД обеспечения топливом (хватило ли всем?)
        float fuelRatio = totalFuelDemand > 0f ? Mathf.Clamp01(actualFuelExtracted / totalFuelDemand) : 1f;


        // ==========================================
        // ФАЗА 3: РАСПРЕДЕЛЕНИЕ ЭНЕРГИИ И TICK
        // ==========================================

        // В будущем здесь будет сложная логика приоритетов.
        // Сейчас все потребители (в основном Батареи) делят энергию пропорционально.
        float energyRatio = totalEnergyDemand > 0f ? Mathf.Clamp01(totalEnergyGenerated / totalEnergyDemand) : 1f;

        int overheatedCount = 0;
        int activeCount = 0;

        foreach (var m in modules)
        {
            var status = m.GetStatus();

            // Раздаем ресурсы модулям (с учетом дефицита)
            float providedFuel = status.fuelDemandThisFrame * fuelRatio;
            float providedEnergy = status.energyDemandThisFrame * energyRatio;

            m.Tick(dt, providedFuel, providedEnergy);

            if (m.IsOverheated) overheatedCount++;
            if (m.IsActive) activeCount++;
        }

        // Если осталась лишняя энергия, а батареи забиты — она просто сгорает.

        // ==========================================
        // ФАЗА 4: ПУБЛИКАЦИЯ РЕЗУЛЬТАТОВ (PepelacStats)
        // ==========================================
        CurrentStats = new PepelacStats
        {
            totalEnergyOutput = totalEnergyGenerated / dt,
            totalEnergyConsumption = totalEnergyDemand / dt,
            energyBalance = (totalEnergyGenerated - totalEnergyDemand) / dt,

            currentEnergyStored = currentEnergy,
            maxEnergyStorage = maxEnergy,

            totalFuelConsumption = actualFuelExtracted / dt,
            fuelRemaining = currentFuel - actualFuelExtracted, // Обновляем с учетом сожженного в этом кадре
            maxFuelStorage = maxFuel,

            totalModulesMassKg = totalMass,
            centerOfMass = Vector2.zero, // TODO: Пересчитывать только при добавлении/снятии модуля!

            activeModules = activeCount,
            overheatedModulesCount = overheatedCount
        };
    }
}