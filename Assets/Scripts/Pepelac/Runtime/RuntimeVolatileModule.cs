using UnityEngine;

/// <summary>
/// Обработка взрыва волатильных модулей при их уничтожении.
/// Использует рассчитанные значения из ModuleData.
/// Старые mass/tier/volume оставлены как fallback для совместимости.
/// </summary>
public class RuntimeVolatileModule : MonoBehaviour
{
    // Основные данные взрыва (из craft/data pipeline)
    private float explosionRadiusMeters;
    private float explosionPenetration;
    private float explosionDamage;
    private DamageType damageType;

    // Fallback-данные для старой формулы, если новые значения не заданы
    private float fallbackModuleMassKg;
    private int fallbackModuleTier;
    private float fallbackEffectiveVolumeM3;

    public void Initialize(
        float explosionRadiusMeters,
        float explosionPenetration,
        float explosionDamage,
        DamageType damageType,
        float fallbackMassKg = 0f,
        int fallbackTier = 0,
        float fallbackEffectiveVolumeM3 = 0f)
    {
        this.explosionRadiusMeters = Mathf.Max(0f, explosionRadiusMeters);
        this.explosionPenetration = Mathf.Max(0f, explosionPenetration);
        this.explosionDamage = Mathf.Max(0f, explosionDamage);
        this.damageType = damageType;

        this.fallbackModuleMassKg = Mathf.Max(0f, fallbackMassKg);
        this.fallbackModuleTier = Mathf.Max(0, fallbackTier);
        this.fallbackEffectiveVolumeM3 = Mathf.Max(0f, fallbackEffectiveVolumeM3);
    }

    [ContextMenu("Test Explode")]
    public void Explode()
    {
        float finalDamage = explosionDamage;
        if (finalDamage <= 0f)
            finalDamage = fallbackModuleMassKg * fallbackModuleTier * fallbackEffectiveVolumeM3;

        Debug.Log(
            $"<color=#FF4444><b>[BOOM]</b> Модуль взорвался! " +
            $"Урон: {finalDamage:F1}, " +
            $"Радиус: {explosionRadiusMeters:F2} м, " +
            $"Пробитие: {explosionPenetration:F2}, " +
            $"Тип: {damageType}</color>");

        // TODO: Передать урон/радиус/пробитие в сетку или систему повреждений
        Destroy(gameObject);
    }
}