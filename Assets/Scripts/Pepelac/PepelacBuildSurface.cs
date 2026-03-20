using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[ExecuteAlways]
[RequireComponent(typeof(BoxCollider))]
public class PepelacBuildSurface : MonoBehaviour
{
    [Header("Grid Settings")]
    [Min(0.01f)]
    [SerializeField] private float cellSize = 0.33f;

    [Header("Surface Sources")]
    [Tooltip("Broad-phase bounds для генерации сетки. Если не назначен — используется BoxCollider на этом объекте.")]
    [SerializeField] private BoxCollider boundsCollider;

    [Tooltip("Обязательная реальная строительная поверхность. По ней определяется, существует ли клетка.")]
    [SerializeField] private Collider buildableCollider;

    [Header("Grid Bounds Mode")]
    [Tooltip("Если включено — размер суперсетки берётся из boundsCollider. Иначе используются ручные размеры ниже.")]
    [SerializeField] private bool useBoundsColliderSize = true;

    [Tooltip("Дополнительный запас по X вокруг boundsCollider при автогенерации сетки.")]
    [Min(0f)]
    [SerializeField] private float gridPaddingX = 0f;

    [Tooltip("Дополнительный запас по Z вокруг boundsCollider при автогенерации сетки.")]
    [Min(0f)]
    [SerializeField] private float gridPaddingZ = 0f;

    [Tooltip("Ручной размер суперсетки по X, если useBoundsColliderSize выключен.")]
    [Min(0.01f)]
    [SerializeField] private float manualGridWidth = 5f;

    [Tooltip("Ручной размер суперсетки по Z, если useBoundsColliderSize выключен.")]
    [Min(0.01f)]
    [SerializeField] private float manualGridLength = 5f;

    [Header("Cell Validation")]
    [Tooltip("Отступ sample points от краёв клетки.")]
    [Min(0f)]
    [SerializeField] private float cellSampleInset = 0.01f;

    [Tooltip("Высота старта луча над поверхностью для проверки клетки.")]
    [Min(0.01f)]
    [SerializeField] private float surfaceProbeHeight = 2f;

    [Tooltip("Максимальная длина луча вниз для проверки поверхности.")]
    [Min(0.01f)]
    [SerializeField] private float surfaceProbeDistance = 5f;

    [Tooltip("Минимально допустимый dot(normal, up), чтобы поверхность считалась достаточно горизонтальной.")]
    [Range(0f, 1f)]
    [SerializeField] private float minSurfaceUpDot = 0.9f;

    [Header("Debug / Computed")]
    [SerializeField, HideInInspector] private int gridWidth;
    [SerializeField, HideInInspector] private int gridHeight;
    [SerializeField, HideInInspector] private Vector3 localGridMin;
    [SerializeField, HideInInspector] private Vector3 localGridMax;
    [SerializeField, HideInInspector] private Vector3 localGridCenter;
    [SerializeField, HideInInspector] private int existingCellsCount;

    private readonly HashSet<Vector2Int> existingCells = new HashSet<Vector2Int>();

    public float CellSize => cellSize;
    public int GridWidth => gridWidth;
    public int GridHeight => gridHeight;
    public Vector3 LocalGridMin => localGridMin;
    public Vector3 LocalGridMax => localGridMax;
    public Vector3 LocalGridCenter => localGridCenter;

    /// <summary>
    /// Реальная строительная поверхность для raycast и проверки существования клетки.
    /// Должна быть назначена явно.
    /// </summary>
    public Collider SurfaceCollider => buildableCollider;

    public IReadOnlyCollection<Vector2Int> ExistingCells => existingCells;

    private void Awake()
    {
        ResolveColliders();
        RecalculateSurface();
    }

    private void OnValidate()
    {
        ResolveColliders();
        RecalculateSurface();
        RefreshLinkedGridAndOverlay();
    }

    private void ResolveColliders()
    {
        if (boundsCollider == null)
            boundsCollider = GetComponent<BoxCollider>();
    }

    [ContextMenu("Recalculate Build Surface")]
    public void RecalculateSurface()
    {
        ResolveColliders();

        if (boundsCollider == null)
        {
            gridWidth = 0;
            gridHeight = 0;
            localGridMin = Vector3.zero;
            localGridMax = Vector3.zero;
            localGridCenter = Vector3.zero;
            existingCells.Clear();
            existingCellsCount = 0;
            return;
        }

        if (buildableCollider == null)
        {
            Debug.LogWarning($"[PepelacBuildSurface] buildableCollider не назначен на {name}. Сетка не будет построена.");

            gridWidth = 0;
            gridHeight = 0;
            localGridMin = Vector3.zero;
            localGridMax = Vector3.zero;
            localGridCenter = Vector3.zero;
            existingCells.Clear();
            existingCellsCount = 0;
            return;
        }

        Vector3 boxCenter = boundsCollider.center;

        float usableWidth = useBoundsColliderSize
            ? Mathf.Max(0f, boundsCollider.size.x + gridPaddingX * 2f)
            : Mathf.Max(0.01f, manualGridWidth);

        float usableLength = useBoundsColliderSize
            ? Mathf.Max(0f, boundsCollider.size.z + gridPaddingZ * 2f)
            : Mathf.Max(0.01f, manualGridLength);

        int calculatedWidth = Mathf.FloorToInt(usableWidth / cellSize);
        int calculatedHeight = Mathf.FloorToInt(usableLength / cellSize);

        if (calculatedWidth <= 0 || calculatedHeight <= 0)
        {
            gridWidth = 0;
            gridHeight = 0;

            float y = boxCenter.y;
            localGridMin = new Vector3(boxCenter.x, y, boxCenter.z);
            localGridMax = new Vector3(boxCenter.x, y, boxCenter.z);
            localGridCenter = new Vector3(boxCenter.x, y, boxCenter.z);

            existingCells.Clear();
            existingCellsCount = 0;
            return;
        }

        gridWidth = calculatedWidth;
        gridHeight = calculatedHeight;

        float snappedWidth = gridWidth * cellSize;
        float snappedLength = gridHeight * cellSize;

        float unusedWidth = usableWidth - snappedWidth;
        float unusedLength = usableLength - snappedLength;

        float minX = boxCenter.x - usableWidth * 0.5f + unusedWidth * 0.5f;
        float maxX = minX + snappedWidth;

        float minZ = boxCenter.z - usableLength * 0.5f + unusedLength * 0.5f;
        float maxZ = minZ + snappedLength;

        float yPos = boxCenter.y;

        localGridMin = new Vector3(minX, yPos, minZ);
        localGridMax = new Vector3(maxX, yPos, maxZ);
        localGridCenter = new Vector3((minX + maxX) * 0.5f, yPos, (minZ + maxZ) * 0.5f);

        RebuildExistingCellsFromGeometry();
    }

    private void RebuildExistingCellsFromGeometry()
    {
        existingCells.Clear();

        for (int x = 0; x < gridWidth; x++)
        {
            for (int z = 0; z < gridHeight; z++)
            {
                if (IsCellFullySupportedBySurface(x, z))
                    existingCells.Add(new Vector2Int(x, z));
            }
        }

        existingCellsCount = existingCells.Count;
    }

    private bool IsCellFullySupportedBySurface(int cellX, int cellZ)
    {
        if (!IsCellInsideGrid(cellX, cellZ))
            return false;

        if (buildableCollider == null)
            return false;

        float y = localGridCenter.y;

        float minX = localGridMin.x + cellX * cellSize;
        float maxX = minX + cellSize;

        float minZ = localGridMin.z + cellZ * cellSize;
        float maxZ = minZ + cellSize;

        float inset = Mathf.Clamp(cellSampleInset, 0f, cellSize * 0.49f);

        Vector3 center = new Vector3((minX + maxX) * 0.5f, y, (minZ + maxZ) * 0.5f);

        Vector3 corner00 = new Vector3(minX + inset, y, minZ + inset);
        Vector3 corner10 = new Vector3(maxX - inset, y, minZ + inset);
        Vector3 corner01 = new Vector3(minX + inset, y, maxZ - inset);
        Vector3 corner11 = new Vector3(maxX - inset, y, maxZ - inset);

        Vector3 edgeMidLeft = new Vector3(minX + inset, y, (minZ + maxZ) * 0.5f);
        Vector3 edgeMidRight = new Vector3(maxX - inset, y, (minZ + maxZ) * 0.5f);
        Vector3 edgeMidBottom = new Vector3((minX + maxX) * 0.5f, y, minZ + inset);
        Vector3 edgeMidTop = new Vector3((minX + maxX) * 0.5f, y, maxZ - inset);

        return IsSamplePointOnBuildableSurface(center) &&
               IsSamplePointOnBuildableSurface(corner00) &&
               IsSamplePointOnBuildableSurface(corner10) &&
               IsSamplePointOnBuildableSurface(corner01) &&
               IsSamplePointOnBuildableSurface(corner11) &&
               IsSamplePointOnBuildableSurface(edgeMidLeft) &&
               IsSamplePointOnBuildableSurface(edgeMidRight) &&
               IsSamplePointOnBuildableSurface(edgeMidBottom) &&
               IsSamplePointOnBuildableSurface(edgeMidTop);
    }

    private bool IsSamplePointOnBuildableSurface(Vector3 localPoint)
    {
        if (buildableCollider == null)
            return false;

        Vector3 worldPoint = transform.TransformPoint(localPoint);
        Vector3 rayOrigin = worldPoint + Vector3.up * surfaceProbeHeight;
        Ray ray = new Ray(rayOrigin, Vector3.down);

        if (!buildableCollider.Raycast(ray, out RaycastHit hit, surfaceProbeDistance))
            return false;

        float upDot = Vector3.Dot(hit.normal.normalized, Vector3.up);
        if (upDot < minSurfaceUpDot)
            return false;

        return true;
    }

    public bool IsCellInsideGrid(int cellX, int cellZ)
    {
        return cellX >= 0 && cellX < gridWidth &&
               cellZ >= 0 && cellZ < gridHeight;
    }

    /// <summary>
    /// Клетка существует, если она полностью лежит на назначенной buildable-поверхности.
    /// </summary>
    public bool HasExistingCell(int cellX, int cellZ)
    {
        return existingCells.Contains(new Vector2Int(cellX, cellZ));
    }

    /// <summary>
    /// В текущей упрощённой модели buildable == existing.
    /// </summary>
    public bool IsCellBuildable(int cellX, int cellZ)
    {
        return HasExistingCell(cellX, cellZ);
    }

    public bool TryLocalPointToCell(Vector3 localPoint, out Vector2Int cell)
    {
        cell = new Vector2Int(-1, -1);

        if (gridWidth <= 0 || gridHeight <= 0)
            return false;

        float x = localPoint.x;
        float z = localPoint.z;

        if (x < localGridMin.x || x >= localGridMax.x ||
            z < localGridMin.z || z >= localGridMax.z)
            return false;

        int cellX = Mathf.FloorToInt((x - localGridMin.x) / cellSize);
        int cellZ = Mathf.FloorToInt((z - localGridMin.z) / cellSize);

        if (cellX < 0 || cellX >= gridWidth || cellZ < 0 || cellZ >= gridHeight)
            return false;

        Vector2Int candidate = new Vector2Int(cellX, cellZ);
        if (!existingCells.Contains(candidate))
            return false;

        cell = candidate;
        return true;
    }

    public bool TryWorldPointToCell(Vector3 worldPoint, out Vector2Int cell)
    {
        Vector3 localPoint = transform.InverseTransformPoint(worldPoint);
        return TryLocalPointToCell(localPoint, out cell);
    }

    public Vector3 CellToLocalCenter(int cellX, int cellZ)
    {
        float x = localGridMin.x + cellX * cellSize + cellSize * 0.5f;
        float z = localGridMin.z + cellZ * cellSize + cellSize * 0.5f;
        return new Vector3(x, localGridCenter.y, z);
    }

    public Vector3 CellToWorldCenter(int cellX, int cellZ)
    {
        return transform.TransformPoint(CellToLocalCenter(cellX, cellZ));
    }

    private void RefreshLinkedGridAndOverlay()
    {
        PepelacGrid grid = GetComponent<PepelacGrid>();
        if (grid == null)
            grid = GetComponentInParent<PepelacGrid>();

        if (grid != null)
            grid.RebuildGrid();

        PepelacGridOverlay overlay = null;

        if (grid != null)
            overlay = grid.GetComponentInChildren<PepelacGridOverlay>(true);

        if (overlay == null)
            overlay = GetComponentInChildren<PepelacGridOverlay>(true);

        if (overlay != null)
            overlay.Rebuild();
    }

    [ContextMenu("Debug Print Existing Cells")]
    private void DebugPrintExistingCells()
    {
        Debug.Log($"[PepelacBuildSurface] Existing Cells Count = {existingCells.Count}");

        foreach (var cell in existingCells)
        {
            Debug.Log($"[PepelacBuildSurface] Existing Cell: {cell}");
        }
    }

    [ContextMenu("Debug Print Surface Summary")]
    private void DebugPrintSurfaceSummary()
    {
        Debug.Log(
            $"[PepelacBuildSurface] Surface Summary | " +
            $"Grid={gridWidth}x{gridHeight} | " +
            $"Existing={existingCellsCount} | " +
            $"CellSize={cellSize}");
    }
}