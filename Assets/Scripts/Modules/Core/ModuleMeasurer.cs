using UnityEngine;

/// <summary>
/// Статический утилитный класс для вычисления физических габаритов GameObject.
/// Избавляет от дублирования кода в эталонных модулях.
/// Путь: Assets/Scripts/CoreMechanics/ModuleMeasurer.cs
/// </summary>
public static class ModuleMeasurer
{
    /// <summary>
    /// Вычисляет размеры (X - Длина, Y - Высота, Z - Ширина) объекта, 
    /// опираясь последовательно на Renderer, Collider, MeshFilter или localScale.
    /// </summary>
    public static Vector3 GetSize(GameObject obj)
    {
        if (obj == null) return Vector3.zero;

        // 1. Попытка через Renderer
        Renderer rend = obj.GetComponentInChildren<Renderer>();
        if (rend != null)
        {
            Vector3 ws = rend.bounds.size;
            return new Vector3(Mathf.Max(0f, ws.x), Mathf.Max(0f, ws.y), Mathf.Max(0f, ws.z));
        }

        // 2. Попытка через Collider
        Collider col = obj.GetComponentInChildren<Collider>();
        if (col != null)
        {
            Vector3 ws = col.bounds.size;
            return new Vector3(Mathf.Max(0f, ws.x), Mathf.Max(0f, ws.y), Mathf.Max(0f, ws.z));
        }

        // 3. Попытка через MeshFilter
        MeshFilter mf = obj.GetComponentInChildren<MeshFilter>();
        if (mf != null && mf.sharedMesh != null)
        {
            Bounds b = mf.sharedMesh.bounds;
            Vector3 localSize = b.size;
            Vector3 ls = mf.transform.lossyScale;
            return new Vector3(
                Mathf.Max(0f, Mathf.Abs(localSize.x * ls.x)),
                Mathf.Max(0f, Mathf.Abs(localSize.y * ls.y)),
                Mathf.Max(0f, Mathf.Abs(localSize.z * ls.z))
            );
        }

        // 4. Фолбэк на Transform Scale
        Vector3 approx = obj.transform.lossyScale;
        return new Vector3(
            Mathf.Max(0f, Mathf.Abs(approx.x)),
            Mathf.Max(0f, Mathf.Abs(approx.y)),
            Mathf.Max(0f, Mathf.Abs(approx.z))
        );
    }
}