using UnityEngine;
using System.Collections.Generic;

public class VolumeCalculator : MonoBehaviour
{
    [Header("Результаты")]
    [SerializeField] private float volumeM3 = 0f;
    [SerializeField] private string status = "Не рассчитан";
    [SerializeField] private bool meshClosed = false;
    [SerializeField] private int openEdges = 0;

    private void OnValidate()
    {
        CalculateVolume();
    }

    private void Start()
    {
        CalculateVolume();
    }

    private void CalculateVolume()
    {
        MeshFilter meshFilter = GetComponent<MeshFilter>();

        if (meshFilter == null || meshFilter.sharedMesh == null)
        {
            status = "❌ Нет MeshFilter или Mesh";
            volumeM3 = 0f;
            meshClosed = false;
            return;
        }

        Mesh mesh = meshFilter.sharedMesh;

        // Проверяем замкнутость
        meshClosed = CheckIfClosed(mesh, out openEdges);

        // Считаем объём
        float volume = CalculateMeshVolume(mesh);

        // Применяем масштаб объекта
        Vector3 scale = transform.lossyScale;
        volumeM3 = volume * scale.x * scale.y * scale.z;

        if (volumeM3 < 0.000001f)
        {
            status = "❌ Объём = 0! Меш плоский или имеет проблемы";
        }
        else if (meshClosed)
        {
            status = $"✅ Меш замкнут. Объём: {volumeM3:F6} м³";
        }
        else
        {
            status = $"⚠️ Меш НЕ замкнут (открытых рёбер: {openEdges}). Объём: {volumeM3:F6} м³";
        }
    }

    private float CalculateMeshVolume(Mesh mesh)
    {
        float volumeSum = 0f;
        Vector3[] vertices = mesh.vertices;
        int[] triangles = mesh.triangles;

        for (int i = 0; i < triangles.Length; i += 3)
        {
            Vector3 p1 = vertices[triangles[i]];
            Vector3 p2 = vertices[triangles[i + 1]];
            Vector3 p3 = vertices[triangles[i + 2]];

            volumeSum += Vector3.Dot(p1, Vector3.Cross(p2, p3)) / 6.0f;
        }

        return Mathf.Abs(volumeSum);
    }

    private bool CheckIfClosed(Mesh mesh, out int openEdgesCount)
    {
        // Словарь: ребро → количество использований
        Dictionary<Edge, int> edgeCount = new Dictionary<Edge, int>();
        Vector3[] vertices = mesh.vertices;
        int[] triangles = mesh.triangles;

        const float TOLERANCE = 0.00001f;

        for (int i = 0; i < triangles.Length; i += 3)
        {
            Vector3 v0 = vertices[triangles[i]];
            Vector3 v1 = vertices[triangles[i + 1]];
            Vector3 v2 = vertices[triangles[i + 2]];

            // Добавляем три ребра треугольника
            AddEdge(edgeCount, v0, v1, TOLERANCE);
            AddEdge(edgeCount, v1, v2, TOLERANCE);
            AddEdge(edgeCount, v2, v0, TOLERANCE);
        }

        // Подсчитываем рёбра, которые встречаются не 2 раза
        openEdgesCount = 0;
        foreach (var kvp in edgeCount)
        {
            if (kvp.Value != 2)
            {
                openEdgesCount++;
            }
        }

        return openEdgesCount == 0;
    }

    private void AddEdge(Dictionary<Edge, int> dict, Vector3 a, Vector3 b, float tolerance)
    {
        Edge edge = new Edge(a, b, tolerance);

        if (dict.ContainsKey(edge))
        {
            dict[edge]++;
        }
        else
        {
            dict[edge] = 1;
        }
    }

    private struct Edge : System.IEquatable<Edge>
    {
        private readonly int hash;
        private readonly Vector3 min;
        private readonly Vector3 max;
        private readonly float tolerance;

        public Edge(Vector3 a, Vector3 b, float tol)
        {
            tolerance = tol;

            // Округляем координаты для избежания проблем с float
            a = RoundVector(a, tolerance);
            b = RoundVector(b, tolerance);

            // Сортируем вершины, чтобы (A,B) == (B,A)
            if (a.x < b.x || (Mathf.Approximately(a.x, b.x) && a.y < b.y) ||
                (Mathf.Approximately(a.x, b.x) && Mathf.Approximately(a.y, b.y) && a.z < b.z))
            {
                min = a;
                max = b;
            }
            else
            {
                min = b;
                max = a;
            }

            // Создаём хеш
            hash = min.GetHashCode() ^ (max.GetHashCode() << 2);
        }

        private static Vector3 RoundVector(Vector3 v, float tolerance)
        {
            float scale = 1f / tolerance;
            return new Vector3(
                Mathf.Round(v.x * scale) / scale,
                Mathf.Round(v.y * scale) / scale,
                Mathf.Round(v.z * scale) / scale
            );
        }

        public bool Equals(Edge other)
        {
            return Vector3.Distance(min, other.min) < tolerance * 2f &&
                   Vector3.Distance(max, other.max) < tolerance * 2f;
        }

        public override bool Equals(object obj)
        {
            return obj is Edge other && Equals(other);
        }

        public override int GetHashCode()
        {
            return hash;
        }
    }
}