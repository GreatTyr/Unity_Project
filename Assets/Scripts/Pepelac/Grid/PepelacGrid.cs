using UnityEngine;
using System.Collections.Generic;

[System.Serializable]
public class GridCell
{
    public bool isOccupied;
    public RuntimeModuleBase occupant;
}

[DisallowMultipleComponent]
[RequireComponent(typeof(PepelacBuildSurface))]
public class PepelacGrid : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private PepelacBuildSurface buildSurface;

    [Header("Debug / Computed")]
    [SerializeField, HideInInspector] private int gridWidth;
    [SerializeField, HideInInspector] private int gridHeight;
    [SerializeField, HideInInspector] private float cellSize = 0.33f;

    private GridCell[,] cells;
    private readonly List<RuntimeModuleBase> installedModules = new List<RuntimeModuleBase>();

    public PepelacBuildSurface BuildSurface => buildSurface;
    public int GridWidth => gridWidth;
    public int GridHeight => gridHeight;
    public float CellSize => cellSize;

    private void Awake()
    {
        ResolveSurface();
        RebuildGrid();
    }

    private void OnValidate()
    {
        ResolveSurface();
        RebuildGrid();
    }

    private void ResolveSurface()
    {
        if (buildSurface == null)
            buildSurface = GetComponent<PepelacBuildSurface>();

        if (buildSurface != null)
            buildSurface.RecalculateSurface();
    }

    [ContextMenu("Rebuild Grid")]
    public void RebuildGrid()
    {
        ResolveSurface();

        if (buildSurface == null)
        {
            gridWidth = 0;
            gridHeight = 0;
            cellSize = 0.33f;
            cells = null;
            return;
        }

        gridWidth = buildSurface.GridWidth;
        gridHeight = buildSurface.GridHeight;
        cellSize = buildSurface.CellSize;

        cells = new GridCell[gridWidth, gridHeight];
        for (int x = 0; x < gridWidth; x++)
        {
            for (int z = 0; z < gridHeight; z++)
            {
                cells[x, z] = new GridCell();
            }
        }

        installedModules.Clear();
    }

    public IReadOnlyList<RuntimeModuleBase> GetAllModules() => installedModules;

    // ==========================================
    // КООРДИНАТЫ
    // ==========================================

    /// <summary>
    /// Возвращает cell под указанной local-point.
    /// Теперь эта cell трактуется как anchor cell.
    /// </summary>
    public Vector2Int LocalToGridPosition(Vector3 localPosition)
    {
        if (buildSurface == null)
            return new Vector2Int(-1, -1);

        return buildSurface.TryLocalPointToCell(localPosition, out var cell)
            ? cell
            : new Vector2Int(-1, -1);
    }

    /// <summary>
    /// Возвращает local-позицию ЦЕНТРА footprint-а,
    /// построенного от anchorCell с размерами gridSize.
    /// То есть anchorCell — это опорная клетка модуля.
    /// </summary>
    public Vector3 GridToLocalPosition(int anchorCellX, int anchorCellZ, Vector2Int gridSize)
    {
        if (buildSurface == null)
            return Vector3.zero;

        float x = buildSurface.LocalGridMin.x + (anchorCellX * cellSize) + (gridSize.x * cellSize * 0.5f);
        float z = buildSurface.LocalGridMin.z + (anchorCellZ * cellSize) + (gridSize.y * cellSize * 0.5f);

        return new Vector3(x, buildSurface.LocalGridCenter.y, z);
    }

    // ==========================================
    // ЛОГИКА РАЗМЕЩЕНИЯ
    // ==========================================

    public GridCell GetCell(int x, int z)
    {
        if (cells == null) return null;
        if (x < 0 || x >= gridWidth || z < 0 || z >= gridHeight) return null;
        return cells[x, z];
    }

    /// <summary>
    /// Перевод габаритов модуля в размеры footprint-а в клетках.
    /// </summary>
    public Vector2Int CalculateGridSize(float lengthMeters, float widthMeters, ModuleOrientation orientation)
    {
        int cellsLength = Mathf.Max(1, Mathf.CeilToInt(lengthMeters / cellSize));
        int cellsWidth = Mathf.Max(1, Mathf.CeilToInt(widthMeters / cellSize));

        int sizeX = cellsWidth;
        int sizeZ = cellsLength;

        if (orientation == ModuleOrientation.Deg90 || orientation == ModuleOrientation.Deg270)
        {
            sizeX = cellsLength;
            sizeZ = cellsWidth;
        }

        return new Vector2Int(sizeX, sizeZ);
    }

    /// <summary>
    /// Возвращает footprint клеток, начиная от anchorCell.
    /// AnchorCell = опорная клетка модуля.
    /// </summary>
    public List<Vector2Int> GetPlacementFootprint(Vector2Int anchorCell, Vector2Int gridSize)
    {
        if (cells == null) return null;

        List<Vector2Int> footprint = new List<Vector2Int>();

        int startX = anchorCell.x;
        int startZ = anchorCell.y;
        int endX = startX + gridSize.x - 1;
        int endZ = startZ + gridSize.y - 1;

        if (startX < 0 || endX >= gridWidth || startZ < 0 || endZ >= gridHeight)
            return null;

        for (int x = startX; x <= endX; x++)
        {
            for (int z = startZ; z <= endZ; z++)
            {
                if (cells[x, z].isOccupied)
                    return null;

                footprint.Add(new Vector2Int(x, z));
            }
        }

        return footprint;
    }

    /// <summary>
    /// Размещает модуль по anchorCell.
    /// </summary>
    public bool TryPlaceModule(RuntimeModuleBase module, Vector2Int anchorCell, float lengthMeters, float widthMeters)
    {
        if (module == null || cells == null)
            return false;

        Vector2Int gridSize = CalculateGridSize(lengthMeters, widthMeters, module.Orientation);
        List<Vector2Int> footprint = GetPlacementFootprint(anchorCell, gridSize);

        if (footprint == null)
        {
            Debug.LogWarning($"[PepelacGrid] Невозможно разместить {module.name} в anchor ({anchorCell.x}, {anchorCell.y}).");
            return false;
        }

        foreach (var cellPos in footprint)
        {
            cells[cellPos.x, cellPos.y].isOccupied = true;
            cells[cellPos.x, cellPos.y].occupant = module;
        }

        module.GridPosition = anchorCell;
        installedModules.Add(module);
        return true;
    }

    public void RemoveModule(RuntimeModuleBase module)
    {
        if (module == null || cells == null) return;
        if (!installedModules.Contains(module)) return;

        for (int x = 0; x < gridWidth; x++)
        {
            for (int z = 0; z < gridHeight; z++)
            {
                if (cells[x, z].occupant == module)
                {
                    cells[x, z].isOccupied = false;
                    cells[x, z].occupant = null;
                }
            }
        }

        installedModules.Remove(module);
    }
}