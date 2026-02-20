using UnityEngine;

/// <summary>
/// Проверка земли под транспортом.
/// Отдельный компонент — единственная ответственность.
/// </summary>
[DisallowMultipleComponent]
public class PepelacGroundCheck : MonoBehaviour
{
    [Header("Ground Check")]
    [Tooltip("Смещение origin для raycast вниз.")]
    public Vector3 groundCheckOffset = new Vector3(0f, -0.5f, 0f);

    [Tooltip("Дистанция raycast вниз.")]
    public float groundCheckDistance = 0.6f;

    [Tooltip("Слои, считающиеся землёй.")]
    public LayerMask groundLayers = ~0;

    public bool IsGrounded { get; private set; }

    /// <summary>
    /// Вызывать каждый кадр из Update.
    /// </summary>
    public void UpdateGrounded(Vector3 position)
    {
        Vector3 origin = position + groundCheckOffset;
        IsGrounded = Physics.Raycast(origin, Vector3.down, groundCheckDistance, groundLayers, QueryTriggerInteraction.Ignore);
    }

    /// <summary>
    /// Найти Y поверхности под указанной позицией.
    /// </summary>
    public float QuerySurfaceY(Vector3 worldPos, bool fallbackToCurrentY = false)
    {
        Vector3 origin = worldPos + Vector3.up * 0.5f;
        if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, 200f, groundLayers, QueryTriggerInteraction.Ignore))
            return hit.point.y;

        return fallbackToCurrentY ? worldPos.y : 0f;
    }
}