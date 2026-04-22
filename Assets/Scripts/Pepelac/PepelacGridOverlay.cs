using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(MeshFilter))]
[RequireComponent(typeof(MeshRenderer))]
public class PepelacGridOverlay : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private PepelacBuildSurface buildSurface;
    [SerializeField] private PepelacGrid grid;
    [SerializeField] private MeshFilter gridMeshFilter;
    [SerializeField] private MeshRenderer gridMeshRenderer;

    [Header("Missing Cells Overlay")]
    [SerializeField] private MeshFilter missingCellsMeshFilter;
    [SerializeField] private MeshRenderer missingCellsMeshRenderer;
    [SerializeField] private Material missingCellsMaterial;

    [Header("Region Debug Overlay")]
    [SerializeField] private MeshFilter regionDebugMeshFilter;
    [SerializeField] private MeshRenderer regionDebugMeshRenderer;
    [SerializeField] private Material regionDebugMaterial;

    [Header("Anchor Debug Overlay")]
    [SerializeField] private MeshFilter anchorDebugMeshFilter;
    [SerializeField] private MeshRenderer anchorDebugMeshRenderer;
    [SerializeField] private Material anchorDebugMaterial;

    [Header("Blocked Cell Debug Overlay")]
    [SerializeField] private MeshFilter blockedCellDebugMeshFilter;
    [SerializeField] private MeshRenderer blockedCellDebugMeshRenderer;
    [SerializeField] private Material blockedCellDebugMaterial;

    [Header("Footprint Overlay")]
    [SerializeField] private MeshFilter footprintMeshFilter;
    [SerializeField] private MeshRenderer footprintMeshRenderer;

    [Header("Visual")]
    [Min(0.001f)]
    [SerializeField] private float lineThickness = 0.01f;

    [SerializeField] private float gridYOffset = 0.01f;
    [SerializeField] private float missingCellsYOffset = 0.0108f;
    [SerializeField] private float footprintYOffset = 0.012f;
    [SerializeField] private float regionDebugYOffset = 0.02f;
    [SerializeField] private float anchorDebugYOffset = 0.021f;
    [SerializeField] private float blockedCellDebugYOffset = 0.0215f;

    [SerializeField] private bool visibleOnStart = false;

    [SerializeField] private bool showMissingCells = true;
    [SerializeField] private bool showRegionDebug = true;
    [SerializeField] private bool showAnchorDebug = true;
    [SerializeField] private bool showBlockedCellDebug = true;

    [ColorUsage(false, true)]
    [SerializeField] private Color missingCellsColor = new Color(0.35f, 0.35f, 0.35f, 0.45f);

    [ColorUsage(false, true)]
    [SerializeField] private Color anchorDebugColor = new Color(1f, 0.9f, 0.15f, 0.65f);

    [ColorUsage(false, true)]
    [SerializeField] private Color blockedCellDebugColor = new Color(1f, 0.15f, 0.75f, 0.85f);

    private Mesh gridMesh;
    private Mesh missingCellsMesh;
    private Mesh regionDebugMesh;
    private Mesh anchorDebugMesh;
    private Mesh blockedCellDebugMesh;
    private Mesh footprintMesh;

    public bool IsVisible => gridMeshRenderer != null && gridMeshRenderer.enabled;

    private void Awake()
    {
        ResolveReferences();
        Rebuild();
        HideFootprint();
        HideRegionDebug();
        HideAnchorDebug();
        HideBlockedCellDebug();
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
        DestroyMeshSafe(missingCellsMesh);
        DestroyMeshSafe(regionDebugMesh);
        DestroyMeshSafe(anchorDebugMesh);
        DestroyMeshSafe(blockedCellDebugMesh);
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

        if (grid == null)
            grid = GetComponentInParent<PepelacGrid>();

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

        if (missingCellsMeshFilter == null || missingCellsMeshRenderer == null)
        {
            Transform child = transform.Find("MissingCellsOverlay");
            if (child != null)
            {
                if (missingCellsMeshFilter == null)
                    missingCellsMeshFilter = child.GetComponent<MeshFilter>();

                if (missingCellsMeshRenderer == null)
                    missingCellsMeshRenderer = child.GetComponent<MeshRenderer>();
            }
        }

        if (regionDebugMeshFilter == null || regionDebugMeshRenderer == null)
        {
            Transform child = transform.Find("RegionDebugOverlay");
            if (child != null)
            {
                if (regionDebugMeshFilter == null)
                    regionDebugMeshFilter = child.GetComponent<MeshFilter>();

                if (regionDebugMeshRenderer == null)
                    regionDebugMeshRenderer = child.GetComponent<MeshRenderer>();
            }
        }

        if (anchorDebugMeshFilter == null || anchorDebugMeshRenderer == null)
        {
            Transform child = transform.Find("AnchorDebugOverlay");
            if (child != null)
            {
                if (anchorDebugMeshFilter == null)
                    anchorDebugMeshFilter = child.GetComponent<MeshFilter>();

                if (anchorDebugMeshRenderer == null)
                    anchorDebugMeshRenderer = child.GetComponent<MeshRenderer>();
            }
        }

        if (blockedCellDebugMeshFilter == null || blockedCellDebugMeshRenderer == null)
        {
            Transform child = transform.Find("BlockedCellDebugOverlay");
            if (child != null)
            {
                if (blockedCellDebugMeshFilter == null)
                    blockedCellDebugMeshFilter = child.GetComponent<MeshFilter>();

                if (blockedCellDebugMeshRenderer == null)
                    blockedCellDebugMeshRenderer = child.GetComponent<MeshRenderer>();
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

    private void ApplyOverlayMaterialAndColor(MeshRenderer renderer, Material explicitMaterial, Color color)
    {
        if (renderer == null)
            return;

        if (explicitMaterial != null && renderer.sharedMaterial != explicitMaterial)
            renderer.sharedMaterial = explicitMaterial;

        var mat = renderer.sharedMaterial;
        if (mat != null)
        {
            if (mat.HasProperty("_BaseColor"))
                mat.SetColor("_BaseColor", color);

            if (mat.HasProperty("_Color"))
                mat.SetColor("_Color", color);

            mat.color = color;
        }

        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
    }

    private Mesh BuildCellsOverlayMesh(IReadOnlyCollection<Vector2Int> cells, float yOffset, string meshName)
    {
        if (buildSurface == null || cells == null || cells.Count == 0)
            return null;

        Mesh mesh = new Mesh { name = meshName };

        List<Vector3> vertices = new List<Vector3>();
        List<int> triangles = new List<int>();

        float y = buildSurface.LocalGridCenter.y + yOffset;
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

        if (vertices.Count == 0)
            return null;

        mesh.SetVertices(vertices);
        mesh.SetTriangles(triangles, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    [ContextMenu("Refresh All Visual Debug Overlays")]
    private void RefreshAllVisualDebugOverlays()
    {
        Rebuild();
        HideRegionDebug();
        HideAnchorDebug();
        HideBlockedCellDebug();
    }

    [ContextMenu("Rebuild Grid Overlay")]
    public void Rebuild()
    {
        ResolveReferences();

        if (buildSurface == null || gridMeshFilter == null)
            return;

        buildSurface.RecalculateSurface();

        if (grid != null)
            grid.RebuildGrid();

        if (buildSurface.GridWidth <= 0 || buildSurface.GridHeight <= 0)
        {
            DestroyMeshSafe(gridMesh);
            gridMesh = null;

            if (gridMeshFilter != null)
                gridMeshFilter.sharedMesh = null;

            HideMissingCells();
            HideFootprint();
            HideRegionDebug();
            HideAnchorDebug();
            HideBlockedCellDebug();
            return;
        }

        DestroyMeshSafe(gridMesh);
        gridMesh = new Mesh { name = "PepelacGridOverlayMesh" };

        List<Vector3> vertices = new List<Vector3>();
        List<int> triangles = new List<int>();

        float y = buildSurface.LocalGridCenter.y + gridYOffset;
        float cellSize = buildSurface.CellSize;
        float halfThickness = lineThickness * 0.5f;

        IReadOnlyCollection<Vector2Int> existingCells =
            grid != null ? grid.GetAllExistingCells() : null;

        if (existingCells == null || existingCells.Count == 0)
        {
            gridMeshFilter.sharedMesh = null;
            HideMissingCells();
            HideFootprint();
            HideRegionDebug();
            HideAnchorDebug();
            HideBlockedCellDebug();
            return;
        }

        foreach (var cell in existingCells)
        {
            Vector3 cellMin = new Vector3(
                buildSurface.LocalGridMin.x + cell.x * cellSize,
                y,
                buildSurface.LocalGridMin.z + cell.y * cellSize
            );

            float minX = cellMin.x;
            float maxX = cellMin.x + cellSize;
            float minZ = cellMin.z;
            float maxZ = cellMin.z + cellSize;

            // Левая грань
            AddQuad(
                new Vector3(minX - halfThickness, y, minZ),
                new Vector3(minX + halfThickness, y, minZ),
                new Vector3(minX + halfThickness, y, maxZ),
                new Vector3(minX - halfThickness, y, maxZ),
                vertices,
                triangles
            );

            // Правая грань
            AddQuad(
                new Vector3(maxX - halfThickness, y, minZ),
                new Vector3(maxX + halfThickness, y, minZ),
                new Vector3(maxX + halfThickness, y, maxZ),
                new Vector3(maxX - halfThickness, y, maxZ),
                vertices,
                triangles
            );

            // Нижняя грань
            AddQuad(
                new Vector3(minX, y, minZ - halfThickness),
                new Vector3(maxX, y, minZ - halfThickness),
                new Vector3(maxX, y, minZ + halfThickness),
                new Vector3(minX, y, minZ + halfThickness),
                vertices,
                triangles
            );

            // Верхняя грань
            AddQuad(
                new Vector3(minX, y, maxZ - halfThickness),
                new Vector3(maxX, y, maxZ - halfThickness),
                new Vector3(maxX, y, maxZ + halfThickness),
                new Vector3(minX, y, maxZ + halfThickness),
                vertices,
                triangles
            );
        }

        if (vertices.Count == 0)
        {
            gridMeshFilter.sharedMesh = null;
            HideMissingCells();
            HideFootprint();
            HideRegionDebug();
            HideAnchorDebug();
            HideBlockedCellDebug();
            return;
        }

        gridMesh.SetVertices(vertices);
        gridMesh.SetTriangles(triangles, 0);
        gridMesh.RecalculateNormals();
        gridMesh.RecalculateBounds();

        gridMeshFilter.sharedMesh = gridMesh;

        RebuildMissingCellsOverlay();
    }

    private void RebuildMissingCellsOverlay()
    {
        ResolveReferences();

        if (!showMissingCells || missingCellsMeshFilter == null || missingCellsMeshRenderer == null || grid == null)
        {
            HideMissingCells();
            return;
        }

        List<Vector2Int> missingCells = new List<Vector2Int>();

        for (int x = 0; x < grid.GridWidth; x++)
        {
            for (int z = 0; z < grid.GridHeight; z++)
            {
                Vector2Int coords = new Vector2Int(x, z);
                if (!grid.HasExistingCell(coords))
                    missingCells.Add(coords);
            }
        }

        DestroyMeshSafe(missingCellsMesh);
        missingCellsMesh = BuildCellsOverlayMesh(missingCells, missingCellsYOffset, "PepelacMissingCellsOverlayMesh");

        if (missingCellsMesh == null)
        {
            HideMissingCells();
            return;
        }

        missingCellsMeshFilter.sharedMesh = missingCellsMesh;
        ApplyOverlayMaterialAndColor(missingCellsMeshRenderer, missingCellsMaterial, missingCellsColor);
        missingCellsMeshRenderer.enabled = IsVisible && showMissingCells;
    }

    public void ShowRegionDebug(IReadOnlyCollection<Vector2Int> cells, Color color)
    {
        ResolveReferences();

        if (!showRegionDebug || regionDebugMeshFilter == null || regionDebugMeshRenderer == null || buildSurface == null)
        {
            HideRegionDebug();
            return;
        }

        if (cells == null || cells.Count == 0)
        {
            HideRegionDebug();
            return;
        }

        DestroyMeshSafe(regionDebugMesh);
        regionDebugMesh = BuildCellsOverlayMesh(cells, regionDebugYOffset, "PepelacRegionDebugOverlayMesh");

        if (regionDebugMesh == null)
        {
            HideRegionDebug();
            return;
        }

        regionDebugMeshFilter.sharedMesh = regionDebugMesh;
        ApplyOverlayMaterialAndColor(regionDebugMeshRenderer, regionDebugMaterial, color);
        regionDebugMeshRenderer.enabled = true;
    }

    public void HideRegionDebug()
    {
        ResolveReferences();

        if (regionDebugMeshFilter != null)
            regionDebugMeshFilter.sharedMesh = null;

        if (regionDebugMeshRenderer != null)
            regionDebugMeshRenderer.enabled = false;
    }

    public void ShowAnchorDebug(Vector2Int cell)
    {
        ResolveReferences();

        if (!showAnchorDebug || anchorDebugMeshFilter == null || anchorDebugMeshRenderer == null)
        {
            HideAnchorDebug();
            return;
        }

        DestroyMeshSafe(anchorDebugMesh);
        anchorDebugMesh = BuildCellsOverlayMesh(
            new List<Vector2Int> { cell },
            anchorDebugYOffset,
            "PepelacAnchorDebugOverlayMesh"
        );

        if (anchorDebugMesh == null)
        {
            HideAnchorDebug();
            return;
        }

        anchorDebugMeshFilter.sharedMesh = anchorDebugMesh;
        ApplyOverlayMaterialAndColor(anchorDebugMeshRenderer, anchorDebugMaterial, anchorDebugColor);
        anchorDebugMeshRenderer.enabled = IsVisible;
    }

    public void HideAnchorDebug()
    {
        ResolveReferences();

        if (anchorDebugMeshFilter != null)
            anchorDebugMeshFilter.sharedMesh = null;

        if (anchorDebugMeshRenderer != null)
            anchorDebugMeshRenderer.enabled = false;
    }

    public void ShowBlockedCellDebug(Vector2Int cell)
    {
        ResolveReferences();

        if (!showBlockedCellDebug || blockedCellDebugMeshFilter == null || blockedCellDebugMeshRenderer == null)
        {
            HideBlockedCellDebug();
            return;
        }

        DestroyMeshSafe(blockedCellDebugMesh);
        blockedCellDebugMesh = BuildCellsOverlayMesh(
            new List<Vector2Int> { cell },
            blockedCellDebugYOffset,
            "PepelacBlockedCellDebugOverlayMesh"
        );

        if (blockedCellDebugMesh == null)
        {
            HideBlockedCellDebug();
            return;
        }

        blockedCellDebugMeshFilter.sharedMesh = blockedCellDebugMesh;
        ApplyOverlayMaterialAndColor(blockedCellDebugMeshRenderer, blockedCellDebugMaterial, blockedCellDebugColor);
        blockedCellDebugMeshRenderer.enabled = IsVisible;
    }

    public void HideBlockedCellDebug()
    {
        ResolveReferences();

        if (blockedCellDebugMeshFilter != null)
            blockedCellDebugMeshFilter.sharedMesh = null;

        if (blockedCellDebugMeshRenderer != null)
            blockedCellDebugMeshRenderer.enabled = false;
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

        if (footprintMeshRenderer.sharedMaterial != null)
            footprintMeshRenderer.sharedMaterial.color = color;
    }

    public void HideFootprint()
    {
        ResolveReferences();

        if (footprintMeshFilter != null)
            footprintMeshFilter.sharedMesh = null;

        if (footprintMeshRenderer != null)
            footprintMeshRenderer.enabled = false;
    }

    private void HideMissingCells()
    {
        ResolveReferences();

        if (missingCellsMeshFilter != null)
            missingCellsMeshFilter.sharedMesh = null;

        if (missingCellsMeshRenderer != null)
            missingCellsMeshRenderer.enabled = false;
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

        if (missingCellsMeshRenderer != null)
        {
            bool hasMissingMesh = missingCellsMeshFilter != null && missingCellsMeshFilter.sharedMesh != null;
            missingCellsMeshRenderer.enabled = visible && showMissingCells && hasMissingMesh;
        }

        if (regionDebugMeshRenderer != null)
        {
            bool hasRegionMesh = regionDebugMeshFilter != null && regionDebugMeshFilter.sharedMesh != null;
            regionDebugMeshRenderer.enabled = visible && showRegionDebug && hasRegionMesh;
        }

        if (anchorDebugMeshRenderer != null)
        {
            bool hasAnchorMesh = anchorDebugMeshFilter != null && anchorDebugMeshFilter.sharedMesh != null;
            anchorDebugMeshRenderer.enabled = visible && showAnchorDebug && hasAnchorMesh;
        }

        if (blockedCellDebugMeshRenderer != null)
        {
            bool hasBlockedCellMesh = blockedCellDebugMeshFilter != null && blockedCellDebugMeshFilter.sharedMesh != null;
            blockedCellDebugMeshRenderer.enabled = visible && showBlockedCellDebug && hasBlockedCellMesh;
        }

        if (!visible)
        {
            HideFootprint();
            HideRegionDebug();
            HideAnchorDebug();
            HideBlockedCellDebug();
        }
    }
}