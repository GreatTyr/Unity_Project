using UnityEngine;
using System.Collections.Generic;

public static class MeshVolumeCalculator
{
    private const float EDGE_TOLERANCE = 0.00001f;
    private static Dictionary<Mesh, float> volumeCache = new Dictionary<Mesh, float>();

    public static float CalculateVolume(MeshFilter meshFilter, Vector3 scale)
    {
        if (meshFilter == null || meshFilter.sharedMesh == null)
            return 0f;

        Mesh mesh = meshFilter.sharedMesh;

        // Проверяем кэш
        if (volumeCache.TryGetValue(mesh, out float cachedVolume))
        {
            return cachedVolume * scale.x * scale.y * scale.z;
        }

        float localVolume = 0f;

        // Пытаемся прочитать меш
        if (mesh.isReadable)
        {
            localVolume = CalculateMeshVolume(mesh.vertices, mesh.triangles);
        }
        else
        {
            // Fallback на bounds
            Bounds bounds = mesh.bounds;
            localVolume = bounds.size.x * bounds.size.y * bounds.size.z;
        }

        // Кэшируем локальный объём
        volumeCache[mesh] = localVolume;

        return localVolume * scale.x * scale.y * scale.z;
    }

    public static float CalculateVolume(Mesh mesh, Vector3 scale)
    {
        if (mesh == null) return 0f;

        if (volumeCache.TryGetValue(mesh, out float cachedVolume))
        {
            return cachedVolume * scale.x * scale.y * scale.z;
        }

        float localVolume = 0f;

        if (mesh.isReadable)
        {
            localVolume = CalculateMeshVolume(mesh.vertices, mesh.triangles);
        }
        else
        {
            Bounds bounds = mesh.bounds;
            localVolume = bounds.size.x * bounds.size.y * bounds.size.z;
        }

        volumeCache[mesh] = localVolume;
        return localVolume * scale.x * scale.y * scale.z;
    }

    public static float CalculateVolume(GameObject gameObject)
    {
        if (gameObject == null) return 0f;

        MeshFilter meshFilter = gameObject.GetComponent<MeshFilter>();
        if (meshFilter == null) return 0f;

        Vector3 scale = gameObject.transform.lossyScale;
        return CalculateVolume(meshFilter, scale);
    }

    public static void ClearCache()
    {
        volumeCache.Clear();
    }

    private static float CalculateMeshVolume(Vector3[] vertices, int[] triangles)
    {
        float volumeSum = 0f;

        for (int i = 0; i < triangles.Length; i += 3)
        {
            Vector3 p1 = vertices[triangles[i]];
            Vector3 p2 = vertices[triangles[i + 1]];
            Vector3 p3 = vertices[triangles[i + 2]];

            volumeSum += Vector3.Dot(p1, Vector3.Cross(p2, p3)) / 6.0f;
        }

        return Mathf.Abs(volumeSum);
    }
    /// <summary>
    /// Пересчитывает объём меша заново с применением scale к каждой вершине.
    /// Медленно, но точно. Используется только в верстаке.
    /// </summary>
    public static float CalculateVolumeWithRescale(Mesh mesh, Vector3 scale)
    {
        if (mesh == null || !mesh.isReadable)
        {
            if (mesh != null)
            {
                Bounds bounds = mesh.bounds;
                return bounds.size.x * bounds.size.y * bounds.size.z * scale.x * scale.y * scale.z;
            }
            return 0f;
        }

        Vector3[] vertices = mesh.vertices;
        int[] triangles = mesh.triangles;

        float volumeSum = 0f;

        for (int i = 0; i < triangles.Length; i += 3)
        {
            Vector3 p1 = Vector3.Scale(vertices[triangles[i]], scale);
            Vector3 p2 = Vector3.Scale(vertices[triangles[i + 1]], scale);
            Vector3 p3 = Vector3.Scale(vertices[triangles[i + 2]], scale);

            volumeSum += Vector3.Dot(p1, Vector3.Cross(p2, p3)) / 6.0f;
        }

        return Mathf.Abs(volumeSum);
    }
}