using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(MeshFilter))]
[RequireComponent(typeof(MeshRenderer))]
public class PepelacGridOverlay : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private PepelacBuildSurface buildSurface;
    [SerializeField] private MeshFilter gridMeshFilter;
    [SerializeField] private MeshRenderer gridMeshRenderer;

    [Header("Footprint Overlay")]
    [SerializeField] private MeshFilter footprintMeshFilter;
    [SerializeField] private MeshRenderer footprintMeshRenderer;

    [Header("Visual")]
    [Min(0.001f)]
    [SerializeField] private float lineThickness = 0.01f;

    [SerializeField] private float gridYOffset = 0.01f;
    [SerializeField] private float footprintYOffset = 0.012f;

    [SerializeField] private bool visibleOnStart = false;

    private Mesh gridMesh;
    private Mesh footprintMesh;

    public bool IsVisible => gridMeshRenderer != null && gridMeshRenderer.enabled;

    private void Awake()
    {
        ResolveReferences();
        Rebuild();
        HideFootprint();
        SetVisible(visibleOnStart);
    }

    private void OnValidate()
    {
        ResolveReferences();
        Rebuild();
    }

    private void OnDestroy()
    {
        DestroyMeshSafe(gridMesh);
        DestroyMeshSafe(footprintMesh);
    }

    private void ResolveReferences()
    {
        if (gridMeshFilter == null)
            gridMeshFilter = GetComponent<MeshFilter>();

        if (gridMeshRenderer == null)
            gridMeshRenderer = GetComponent<MeshRenderer>();

        if (buildSurface == null)
            buildSurface = GetComponentInParent<PepelacBuildSurface>();

        if (footprintMeshFilter == null || footprintMeshRenderer == null)
        {
            Transform child = transform.Find("FootprintOverlay");
            if (child != null)
            {
                if (footprintMeshFilter == null)
                    footprintMeshFilter = child.GetComponent<MeshFilter>();

                if (footprintMeshRenderer == null)
                    footprintMeshRenderer = child.GetComponent<MeshRenderer>();
            }
        }
    }

    private void DestroyMeshSafe(Mesh mesh)
    {
        if (mesh == null) return;

        if (Application.isPlaying)
            Destroy(mesh);
        else
            DestroyImmediate(mesh);
    }

    [ContextMenu("Rebuild Grid Overlay")]
    public void Rebuild()
    {
        ResolveReferences();

        if (buildSurface == null || gridMeshFilter == null)
            return;

        buildSurface.RecalculateSurface();

        DestroyMeshSafe(gridMesh);
        gridMesh = new Mesh { name = "PepelacGridOverlayMesh" };

        List<Vector3> vertices = new List<Vector3>();
        List<int> triangles = new List<int>();

        float minX = buildSurface.LocalGridMin.x;
        float maxX = buildSurface.LocalGridMax.x;
        float minZ = buildSurface.LocalGridMin.z;
        float maxZ = buildSurface.LocalGridMax.z;
        float y = buildSurface.LocalGridCenter.y + gridYOffset;

        int gridWidth = buildSurface.GridWidth;
        int gridHeight = buildSurface.GridHeight;
        float cellSize = buildSurface.CellSize;

        float halfThickness = lineThickness * 0.5f;

        // Вертикальные линии
        for (int x = 0; x <= gridWidth; x++)
        {
            float lineX = minX + x * cellSize;

            AddQuad(
                new Vector3(lineX - halfThickness, y, minZ),
                new Vector3(lineX + halfThickness, y, minZ),
                new Vector3(lineX + halfThickness, y, maxZ),
                new Vector3(lineX - halfThickness, y, maxZ),
                vertices,
                triangles
            );
        }

        // Горизонтальные линии
        for (int z = 0; z <= gridHeight; z++)
        {
            float lineZ = minZ + z * cellSize;

            AddQuad(
                new Vector3(minX, y, lineZ - halfThickness),
                new Vector3(maxX, y, lineZ - halfThickness),
                new Vector3(maxX, y, lineZ + halfThickness),
                new Vector3(minX, y, lineZ + halfThickness),
                vertices,
                triangles
            );
        }

        gridMesh.SetVertices(vertices);
        gridMesh.SetTriangles(triangles, 0);
        gridMesh.RecalculateNormals();
        gridMesh.RecalculateBounds();

        gridMeshFilter.sharedMesh = gridMesh;
    }

    public void ShowFootprint(List<Vector2Int> cells, Color color)
    {
        ResolveReferences();

        if (footprintMeshFilter == null || footprintMeshRenderer == null || buildSurface == null)
            return;

        if (cells == null || cells.Count == 0)
        {
            HideFootprint();
            return;
        }

        DestroyMeshSafe(footprintMesh);
        footprintMesh = new Mesh { name = "PepelacFootprintOverlayMesh" };

        List<Vector3> vertices = new List<Vector3>();
        List<int> triangles = new List<int>();

        float y = buildSurface.LocalGridCenter.y + footprintYOffset;
        float cellSize = buildSurface.CellSize;

        foreach (var cell in cells)
        {
            Vector3 cellMin = new Vector3(
                buildSurface.LocalGridMin.x + cell.x * cellSize,
                y,
                buildSurface.LocalGridMin.z + cell.y * cellSize
            );

            Vector3 v0 = new Vector3(cellMin.x, y, cellMin.z);
            Vector3 v1 = new Vector3(cellMin.x + cellSize, y, cellMin.z);
            Vector3 v2 = new Vector3(cellMin.x + cellSize, y, cellMin.z + cellSize);
            Vector3 v3 = new Vector3(cellMin.x, y, cellMin.z + cellSize);

            AddQuad(v0, v1, v2, v3, vertices, triangles);
        }

        footprintMesh.SetVertices(vertices);
        footprintMesh.SetTriangles(triangles, 0);
        footprintMesh.RecalculateNormals();
        footprintMesh.RecalculateBounds();

        footprintMeshFilter.sharedMesh = footprintMesh;

        if (!footprintMeshRenderer.gameObject.activeSelf)
            footprintMeshRenderer.gameObject.SetActive(true);

        footprintMeshRenderer.enabled = true;

        if (footprintMeshRenderer.material != null)
            footprintMeshRenderer.material.color = color;
    }

    public void HideFootprint()
    {
        ResolveReferences();

        if (footprintMeshFilter != null)
            footprintMeshFilter.sharedMesh = null;

        if (footprintMeshRenderer != null)
            footprintMeshRenderer.enabled = false;
    }

    private void AddQuad(
        Vector3 v0,
        Vector3 v1,
        Vector3 v2,
        Vector3 v3,
        List<Vector3> vertices,
        List<int> triangles)
    {
        int start = vertices.Count;

        vertices.Add(v0);
        vertices.Add(v1);
        vertices.Add(v2);
        vertices.Add(v3);

        triangles.Add(start + 0);
        triangles.Add(start + 1);
        triangles.Add(start + 2);

        triangles.Add(start + 0);
        triangles.Add(start + 2);
        triangles.Add(start + 3);
    }

    public void SetVisible(bool visible)
    {
        ResolveReferences();

        if (visible)
        {
            if (!gameObject.activeSelf)
                gameObject.SetActive(true);

            if (gridMesh == null || gridMeshFilter == null || gridMeshFilter.sharedMesh == null)
                Rebuild();
        }

        if (gridMeshRenderer != null)
            gridMeshRenderer.enabled = visible;

        if (!visible)
            HideFootprint();
    }
}