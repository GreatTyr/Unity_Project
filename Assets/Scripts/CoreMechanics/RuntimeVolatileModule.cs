using UnityEngine;

/// <summary>
/// Обработка взрыва волатильных модулей при их уничтожении.
/// </summary>
public class RuntimeVolatileModule : MonoBehaviour
{
    private float moduleMassKg;
    private int moduleTier;
    private float effectiveVolumeM3;
    private DamageType damageType;

    public void Initialize(float mass, int tier, float vol, DamageType type)
    {
        moduleMassKg = mass;
        moduleTier = tier;
        effectiveVolumeM3 = vol;
        damageType = type;
    }

    [ContextMenu("Test Explode")]
    public void Explode()
    {
        float damage = moduleMassKg * moduleTier * effectiveVolumeM3;
        Debug.Log($"<color=#FF4444><b>[BOOM]</b> Модуль взорвался! Урон: {damage:F1}, Тип: {damageType}</color>");

        // TODO: Передать урон в сетку или радиус поражения
        Destroy(gameObject);
    }
}