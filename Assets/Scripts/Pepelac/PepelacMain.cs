using UnityEngine;

// Простая заглушка для отображения параметров в инспекторе.
// Никакой логики: только публичные поля.
public class PepelacMain : MonoBehaviour
{
    // Движение
    public float Speed;
    public float Inertia;
    public float MaxSpeed;
    public float Acceleration;
    public float MaxSpeedBack;
    public float AccelerationBack;
    public float MaxSpeedStrafe;
    public float AccelerationStrafe;
    public float RotationSpeed;

    // Масса и груз
    public float TotalMass;
    public float Carryweight;
    public float MaxCarryweight;

    // Энергия и топливо
    public float Energy;
    public float MaxEnergy;
    public float EnergyConsumption;
    public float Fuel;
    public float MaxFuel;
    public float FuelConsumption;
}