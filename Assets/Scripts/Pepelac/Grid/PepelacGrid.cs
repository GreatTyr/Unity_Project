using UnityEngine;
using System.Collections.Generic;

[System.Serializable]
public class GridCell
{
    public Vector2Int coordinates;
    public bool isBuildable = true;
    public bool isOccupied;
    public RuntimeModuleBase occupant;
}

public enum PlacementBlockReason
{
    None = 0,
    MissingCell = 1,
    Occupied = 2,
    RegionMismatch = 3,
    Unknown = 4
}

[System.Serializable]
public class PlacementQueryResult
{
    public bool isValid;
    public PlacementBlockReason blockReason = PlacementBlockReason.Unknown;
    public Vector2Int firstBlockedCell = new Vector2Int(-1, -1);

    public int expectedRegionId = -1;
    public int blockedRegionId = -1;

    public List<Vector2Int> rawFootprint = new List<Vector2Int>();
    public List<Vector2Int> validatedFootprint = new List<Vector2Int>();
}

[System.Serializable]
public class PlacedModuleRecord
{
    public RuntimeModuleBase module;
    public Vector2Int anchorCell;
    public ModuleOrientation orientation;
    public Vector2Int buildAnchorCellLocal;
    public int buildableRegionId = -1;
    public List<Vector2Int> occupiedCells = new List<Vector2Int>();
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

    [Header("Debug Summary")]
    [SerializeField, HideInInspector] private int existingCellsCount;
    [SerializeField, HideInInspector] private int buildableCellsCount;
    [SerializeField, HideInInspector] private int placedModulesCount;
    [SerializeField, HideInInspector] private int placedRecordsCount;
    [SerializeField, HideInInspector] private int buildableRegionCount;
    [SerializeField, HideInInspector] private int largestBuildableRegionId = -1;
    [SerializeField, HideInInspector] private int largestBuildableRegionSize;

    private GridCell[,] cells;

    private readonly List<RuntimeModuleBase> installedModules = new List<RuntimeModuleBase>();
    private readonly Dictionary<RuntimeModuleBase, PlacedModuleRecord> placedModuleRecords =
        new Dictionary<RuntimeModuleBase, PlacedModuleRecord>();

    private readonly HashSet<Vector2Int> existingCells = new HashSet<Vector2Int>();
    private readonly HashSet<Vector2Int> buildableCells = new HashSet<Vector2Int>();

    private readonly Dictionary<Vector2Int, List<Vector2Int>> buildableAdjacency =
        new Dictionary<Vector2Int, List<Vector2Int>>();
    private readonly Dictionary<Vector2Int, int> buildableRegionIds =
        new Dictionary<Vector2Int, int>();

    private static readonly Vector2Int[] CardinalDirections =
    {
        Vector2Int.right,
        Vector2Int.left,
        Vector2Int.up,
        Vector2Int.down
    };

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

            existingCells.Clear();
            buildableCells.Clear();
            buildableAdjacency.Clear();
            buildableRegionIds.Clear();
            installedModules.Clear();
            placedModuleRecords.Clear();

            RefreshDebugSummary();
            return;
        }

        gridWidth = buildSurface.GridWidth;
        gridHeight = buildSurface.GridHeight;
        cellSize = buildSurface.CellSize;

        cells = new GridCell[gridWidth, gridHeight];

        existingCells.Clear();
        buildableCells.Clear();
        buildableAdjacency.Clear();
        buildableRegionIds.Clear();
        installedModules.Clear();
        placedModuleRecords.Clear();

        for (int x = 0; x < gridWidth; x++)
        {
            for (int z = 0; z < gridHeight; z++)
            {
                if (!buildSurface.HasExistingCell(x, z))
                {
                    cells[x, z] = null;
                    continue;
                }

                Vector2Int coords = new Vector2Int(x, z);

                cells[x, z] = new GridCell
                {
                    coordinates = coords,
                    isBuildable = true,
                    isOccupied = false,
                    occupant = null
                };

                existingCells.Add(coords);
            }
        }

        RefreshBuildableMask();
    }

    [ContextMenu("Refresh Buildable Mask")]
    public void RefreshBuildableMask()
    {
        if (cells == null || buildSurface == null)
        {
            buildableCells.Clear();
            buildableAdjacency.Clear();
            buildableRegionIds.Clear();
            RefreshDebugSummary();
            return;
        }

        existingCells.Clear();
        buildableCells.Clear();
        buildableAdjacency.Clear();
        buildableRegionIds.Clear();

        for (int x = 0; x < gridWidth; x++)
        {
            for (int z = 0; z < gridHeight; z++)
            {
                if (!buildSurface.HasExistingCell(x, z))
                {
                    cells[x, z] = null;
                    continue;
                }

                Vector2Int coords = new Vector2Int(x, z);

                if (cells[x, z] == null)
                {
                    cells[x, z] = new GridCell
                    {
                        coordinates = coords,
                        isBuildable = true,
                        isOccupied = false,
                        occupant = null
                    };
                }

                existingCells.Add(coords);

                // В текущей surface-driven модели buildable == existing
                cells[x, z].isBuildable = true;
                buildableCells.Add(coords);
            }
        }

        RebuildBuildableConnectivity();
        RefreshDebugSummary();
    }

    [ContextMenu("Rebuild Buildable Connectivity")]
    public void RebuildBuildableConnectivity()
    {
        buildableAdjacency.Clear();
        buildableRegionIds.Clear();

        foreach (var cell in buildableCells)
        {
            List<Vector2Int> neighbors = new List<Vector2Int>(4);

            for (int i = 0; i < CardinalDirections.Length; i++)
            {
                Vector2Int neighbor = cell + CardinalDirections[i];

                if (!existingCells.Contains(neighbor))
                    continue;

                if (buildableCells.Contains(neighbor))
                    neighbors.Add(neighbor);
            }

            buildableAdjacency[cell] = neighbors;
        }

        int nextRegionId = 0;
        Queue<Vector2Int> queue = new Queue<Vector2Int>();

        foreach (var startCell in buildableCells)
        {
            if (buildableRegionIds.ContainsKey(startCell))
                continue;

            nextRegionId++;
            buildableRegionIds[startCell] = nextRegionId;
            queue.Enqueue(startCell);

            while (queue.Count > 0)
            {
                Vector2Int current = queue.Dequeue();

                if (!buildableAdjacency.TryGetValue(current, out var neighbors))
                    continue;

                for (int i = 0; i < neighbors.Count; i++)
                {
                    Vector2Int neighbor = neighbors[i];
                    if (buildableRegionIds.ContainsKey(neighbor))
                        continue;

                    buildableRegionIds[neighbor] = nextRegionId;
                    queue.Enqueue(neighbor);
                }
            }
        }

        RefreshDebugSummary();
    }

    private void RefreshDebugSummary()
    {
        existingCellsCount = existingCells.Count;
        buildableCellsCount = buildableCells.Count;
        placedModulesCount = installedModules.Count;
        placedRecordsCount = placedModuleRecords.Count;

        HashSet<int> uniqueRegions = new HashSet<int>();
        foreach (var kvp in buildableRegionIds)
            uniqueRegions.Add(kvp.Value);

        buildableRegionCount = uniqueRegions.Count;

        largestBuildableRegionId = GetLargestBuildableRegionId();
        largestBuildableRegionSize = largestBuildableRegionId > 0
            ? GetBuildableRegionSize(largestBuildableRegionId)
            : 0;
    }

    public IReadOnlyList<RuntimeModuleBase> GetAllModules() => installedModules;

    public IReadOnlyCollection<Vector2Int> GetAllExistingCells()
    {
        return existingCells;
    }

    public bool HasExistingCell(Vector2Int cell)
    {
        return existingCells.Contains(cell);
    }

    public IReadOnlyCollection<Vector2Int> GetAllBuildableCells()
    {
        return buildableCells;
    }

    public bool HasBuildableCell(Vector2Int cell)
    {
        return buildableCells.Contains(cell);
    }

    public IReadOnlyList<Vector2Int> GetBuildableNeighbors(Vector2Int cell)
    {
        if (buildableAdjacency.TryGetValue(cell, out var neighbors))
            return neighbors;

        return System.Array.Empty<Vector2Int>();
    }

    public bool TryGetBuildableRegionId(Vector2Int cell, out int regionId)
    {
        return buildableRegionIds.TryGetValue(cell, out regionId);
    }

    public bool AreBuildableCellsConnected(Vector2Int a, Vector2Int b)
    {
        if (!buildableRegionIds.TryGetValue(a, out int regionA))
            return false;

        if (!buildableRegionIds.TryGetValue(b, out int regionB))
            return false;

        return regionA == regionB;
    }

    public IReadOnlyDictionary<Vector2Int, int> GetBuildableRegions()
    {
        return buildableRegionIds;
    }

    public Dictionary<int, List<Vector2Int>> GetBuildableRegionsMap()
    {
        Dictionary<int, List<Vector2Int>> result = new Dictionary<int, List<Vector2Int>>();

        foreach (var kvp in buildableRegionIds)
        {
            if (!result.TryGetValue(kvp.Value, out var cellsInRegion))
            {
                cellsInRegion = new List<Vector2Int>();
                result[kvp.Value] = cellsInRegion;
            }

            cellsInRegion.Add(kvp.Key);
        }

        return result;
    }

    public int GetLargestBuildableRegionId()
    {
        Dictionary<int, List<Vector2Int>> regions = GetBuildableRegionsMap();

        int bestRegionId = -1;
        int bestCount = -1;

        foreach (var kvp in regions)
        {
            int count = kvp.Value != null ? kvp.Value.Count : 0;
            if (count > bestCount)
            {
                bestCount = count;
                bestRegionId = kvp.Key;
            }
        }

        return bestRegionId;
    }

    public bool HasMultipleBuildableRegions()
    {
        HashSet<int> uniqueRegions = new HashSet<int>();

        foreach (var kvp in buildableRegionIds)
            uniqueRegions.Add(kvp.Value);

        return uniqueRegions.Count > 1;
    }

    public int GetBuildableRegionSize(int regionId)
    {
        int count = 0;

        foreach (var kvp in buildableRegionIds)
        {
            if (kvp.Value == regionId)
                count++;
        }

        return count;
    }

    public List<Vector2Int> GetCellsInBuildableRegion(int regionId)
    {
        List<Vector2Int> result = new List<Vector2Int>();

        if (regionId <= 0)
            return result;

        foreach (var kvp in buildableRegionIds)
        {
            if (kvp.Value == regionId)
                result.Add(kvp.Key);
        }

        return result;
    }

    public bool TryGetPlacedModuleRecord(RuntimeModuleBase module, out PlacedModuleRecord record)
    {
        if (module == null)
        {
            record = null;
            return false;
        }

        return placedModuleRecords.TryGetValue(module, out record);
    }

    public IReadOnlyList<Vector2Int> GetModuleFootprint(RuntimeModuleBase module)
    {
        if (module == null) return null;

        if (placedModuleRecords.TryGetValue(module, out var record))
            return record.occupiedCells;

        return null;
    }

    public Vector2Int GetModuleAnchorCell(RuntimeModuleBase module)
    {
        if (module == null) return new Vector2Int(-1, -1);

        if (placedModuleRecords.TryGetValue(module, out var record))
            return record.anchorCell;

        return new Vector2Int(-1, -1);
    }

    public int GetModuleBuildableRegionId(RuntimeModuleBase module)
    {
        if (module == null) return -1;

        if (placedModuleRecords.TryGetValue(module, out var record))
            return record.buildableRegionId;

        return -1;
    }

    // ==========================================
    // COORDINATES
    // ==========================================

    public Vector2Int LocalToGridPosition(Vector3 localPosition)
    {
        if (buildSurface == null)
            return new Vector2Int(-1, -1);

        return buildSurface.TryLocalPointToCell(localPosition, out var cell)
            ? cell
            : new Vector2Int(-1, -1);
    }

    public Vector3 AnchorCellToLocalCenter(int anchorCellX, int anchorCellZ)
    {
        if (buildSurface == null)
            return Vector3.zero;

        return buildSurface.CellToLocalCenter(anchorCellX, anchorCellZ);
    }

    // ==========================================
    // CELL STATE
    // ==========================================

    public GridCell GetCell(int x, int z)
    {
        if (cells == null) return null;
        if (x < 0 || x >= gridWidth || z < 0 || z >= gridHeight) return null;
        return cells[x, z];
    }

    public bool TryGetCell(Vector2Int coords, out GridCell cell)
    {
        cell = null;

        if (!HasExistingCell(coords))
            return false;

        cell = GetCell(coords.x, coords.y);
        return cell != null;
    }

    public bool IsInsideBounds(int x, int z)
    {
        return x >= 0 && x < gridWidth && z >= 0 && z < gridHeight;
    }

    public bool IsCellBuildable(int x, int z)
    {
        return buildableCells.Contains(new Vector2Int(x, z));
    }

    public bool IsCellOccupied(int x, int z)
    {
        GridCell cell = GetCell(x, z);
        return cell != null && cell.isOccupied;
    }

    // ==========================================
    // FOOTPRINT SIZE / ANCHOR
    // ==========================================

    public Vector2Int CalculateBaseGridSize(float lengthMeters, float widthMeters)
    {
        int sizeX = Mathf.Max(1, Mathf.CeilToInt(lengthMeters / cellSize));
        int sizeZ = Mathf.Max(1, Mathf.CeilToInt(widthMeters / cellSize));
        return new Vector2Int(sizeX, sizeZ);
    }

    private float GetOrientationYaw(ModuleOrientation orientation)
    {
        switch (orientation)
        {
            case ModuleOrientation.Deg90: return 90f;
            case ModuleOrientation.Deg180: return 180f;
            case ModuleOrientation.Deg270: return 270f;
            default: return 0f;
        }
    }

    public Vector3 GetAnchorToFootprintCenterOffset(
        float lengthMeters,
        float widthMeters,
        ModuleOrientation orientation,
        Vector2Int buildAnchorCellLocal)
    {
        Vector2Int baseGridSize = CalculateBaseGridSize(lengthMeters, widthMeters);
        Vector2Int clampedAnchor = ClampBuildAnchorCellLocal(buildAnchorCellLocal, baseGridSize);

        float offsetX = ((((float)baseGridSize.x - 1f) * 0.5f) - clampedAnchor.x) * cellSize;
        float offsetZ = ((((float)baseGridSize.y - 1f) * 0.5f) - clampedAnchor.y) * cellSize;

        Quaternion logicalRotation = Quaternion.Euler(0f, GetOrientationYaw(orientation), 0f);
        return logicalRotation * new Vector3(offsetX, 0f, offsetZ);
    }

    private Vector2Int ClampBuildAnchorCellLocal(Vector2Int buildAnchorCellLocal, Vector2Int baseGridSize)
    {
        int clampedX = Mathf.Clamp(buildAnchorCellLocal.x, 0, Mathf.Max(0, baseGridSize.x - 1));
        int clampedZ = Mathf.Clamp(buildAnchorCellLocal.y, 0, Mathf.Max(0, baseGridSize.y - 1));
        return new Vector2Int(clampedX, clampedZ);
    }

    private Vector2Int RotateCellOffset(Vector2Int offset, ModuleOrientation orientation)
    {
        switch (orientation)
        {
            case ModuleOrientation.Deg90:
                return new Vector2Int(offset.y, -offset.x);

            case ModuleOrientation.Deg180:
                return new Vector2Int(-offset.x, -offset.y);

            case ModuleOrientation.Deg270:
                return new Vector2Int(-offset.y, offset.x);

            default:
                return offset;
        }
    }

    private List<Vector2Int> BuildAnchorFootprint(
        Vector2Int anchorCell,
        Vector2Int baseGridSize,
        ModuleOrientation orientation,
        Vector2Int buildAnchorCellLocal,
        bool validateBounds,
        bool validateOccupied)
    {
        if (cells == null) return null;

        Vector2Int clampedAnchorLocal = ClampBuildAnchorCellLocal(buildAnchorCellLocal, baseGridSize);
        List<Vector2Int> footprint = new List<Vector2Int>();

        for (int localX = 0; localX < baseGridSize.x; localX++)
        {
            for (int localZ = 0; localZ < baseGridSize.y; localZ++)
            {
                Vector2Int localCell = new Vector2Int(localX, localZ);
                Vector2Int relativeOffset = localCell - clampedAnchorLocal;
                Vector2Int rotatedOffset = RotateCellOffset(relativeOffset, orientation);
                Vector2Int worldCell = anchorCell + rotatedOffset;

                if (validateBounds && !HasExistingCell(worldCell))
                    return null;

                if (!validateBounds || HasExistingCell(worldCell))
                {
                    GridCell cell = GetCell(worldCell.x, worldCell.y);

                    if (validateOccupied && (cell == null || cell.isOccupied))
                        return null;
                }

                footprint.Add(worldCell);
            }
        }

        return footprint;
    }

    // ==========================================
    // PLACEMENT API
    // ==========================================

    public List<Vector2Int> GetPlacementFootprint(
        Vector2Int anchorCell,
        float lengthMeters,
        float widthMeters,
        ModuleOrientation orientation,
        Vector2Int buildAnchorCellLocal)
    {
        PlacementQueryResult query = QueryPlacement(
            anchorCell,
            lengthMeters,
            widthMeters,
            orientation,
            buildAnchorCellLocal
        );

        return query.isValid ? query.validatedFootprint : null;
    }

    public List<Vector2Int> GetRawFootprint(
        Vector2Int anchorCell,
        float lengthMeters,
        float widthMeters,
        ModuleOrientation orientation,
        Vector2Int buildAnchorCellLocal)
    {
        Vector2Int baseGridSize = CalculateBaseGridSize(lengthMeters, widthMeters);

        return BuildAnchorFootprint(
            anchorCell,
            baseGridSize,
            orientation,
            buildAnchorCellLocal,
            validateBounds: false,
            validateOccupied: false
        );
    }

    public PlacementQueryResult QueryPlacement(
        Vector2Int anchorCell,
        float lengthMeters,
        float widthMeters,
        ModuleOrientation orientation,
        Vector2Int buildAnchorCellLocal)
    {
        PlacementQueryResult result = new PlacementQueryResult();

        if (cells == null)
        {
            result.isValid = false;
            result.blockReason = PlacementBlockReason.Unknown;
            return result;
        }

        List<Vector2Int> raw = GetRawFootprint(
            anchorCell,
            lengthMeters,
            widthMeters,
            orientation,
            buildAnchorCellLocal
        );

        if (raw == null || raw.Count == 0)
        {
            result.isValid = false;
            result.blockReason = PlacementBlockReason.Unknown;
            return result;
        }

        result.rawFootprint.AddRange(raw);

        int regionId = -1;

        foreach (var cellPos in raw)
        {
            if (!HasExistingCell(cellPos))
            {
                result.isValid = false;
                result.blockReason = PlacementBlockReason.MissingCell;
                result.firstBlockedCell = cellPos;
                return result;
            }

            GridCell cell = GetCell(cellPos.x, cellPos.y);
            if (cell == null)
            {
                result.isValid = false;
                result.blockReason = PlacementBlockReason.MissingCell;
                result.firstBlockedCell = cellPos;
                return result;
            }

            if (cell.isOccupied)
            {
                result.isValid = false;
                result.blockReason = PlacementBlockReason.Occupied;
                result.firstBlockedCell = cellPos;
                return result;
            }

            if (!TryGetBuildableRegionId(cellPos, out int currentRegionId))
            {
                result.isValid = false;
                result.blockReason = PlacementBlockReason.MissingCell;
                result.firstBlockedCell = cellPos;
                return result;
            }

            if (regionId < 0)
            {
                regionId = currentRegionId;
                result.expectedRegionId = regionId;
            }
            else if (currentRegionId != regionId)
            {
                result.isValid = false;
                result.blockReason = PlacementBlockReason.RegionMismatch;
                result.firstBlockedCell = cellPos;
                result.blockedRegionId = currentRegionId;
                return result;
            }

            result.validatedFootprint.Add(cellPos);
        }

        result.isValid = true;
        result.blockReason = PlacementBlockReason.None;
        return result;
    }

    public bool TryPlaceModule(
        RuntimeModuleBase module,
        Vector2Int anchorCell,
        float lengthMeters,
        float widthMeters,
        Vector2Int buildAnchorCellLocal)
    {
        if (module == null || cells == null)
            return false;

        if (installedModules.Contains(module) || placedModuleRecords.ContainsKey(module))
        {
            Debug.LogWarning($"[PepelacGrid] Модуль {module.name} уже установлен в сетке.");
            return false;
        }

        PlacementQueryResult query = QueryPlacement(
            anchorCell,
            lengthMeters,
            widthMeters,
            module.Orientation,
            buildAnchorCellLocal
        );

        if (!query.isValid)
        {
            Debug.LogWarning(
                $"[PepelacGrid] Невозможно разместить {module.name} в anchor ({anchorCell.x}, {anchorCell.y}). " +
                $"Причина: {query.blockReason}, клетка: {query.firstBlockedCell}");
            return false;
        }

        List<Vector2Int> occupiedFootprint = new List<Vector2Int>(query.validatedFootprint);

        foreach (var cellPos in occupiedFootprint)
        {
            GridCell cell = cells[cellPos.x, cellPos.y];
            cell.isOccupied = true;
            cell.occupant = module;
        }

        PlacedModuleRecord record = new PlacedModuleRecord
        {
            module = module,
            anchorCell = anchorCell,
            orientation = module.Orientation,
            buildAnchorCellLocal = buildAnchorCellLocal,
            buildableRegionId = query.expectedRegionId,
            occupiedCells = occupiedFootprint
        };

        module.GridPosition = anchorCell;
        installedModules.Add(module);
        placedModuleRecords[module] = record;

        RefreshDebugSummary();
        return true;
    }

    public void RemoveModule(RuntimeModuleBase module)
    {
        if (module == null || cells == null) return;
        if (!installedModules.Contains(module)) return;

        if (placedModuleRecords.TryGetValue(module, out var record) &&
            record != null &&
            record.occupiedCells != null)
        {
            foreach (var cellPos in record.occupiedCells)
            {
                GridCell cell = GetCell(cellPos.x, cellPos.y);
                if (cell == null) continue;

                if (cell.occupant == module)
                {
                    cell.isOccupied = false;
                    cell.occupant = null;
                }
            }

            placedModuleRecords.Remove(module);
        }
        else
        {
            for (int x = 0; x < gridWidth; x++)
            {
                for (int z = 0; z < gridHeight; z++)
                {
                    if (cells[x, z] != null && cells[x, z].occupant == module)
                    {
                        cells[x, z].isOccupied = false;
                        cells[x, z].occupant = null;
                    }
                }
            }
        }

        installedModules.Remove(module);
        RefreshDebugSummary();
    }

    // ==========================================
    // DEBUG
    // ==========================================

    [ContextMenu("Debug Print Existing Cells")]
    private void DebugPrintExistingCells()
    {
        Debug.Log($"[PepelacGrid] Existing Cells Count = {existingCells.Count}");

        foreach (var cell in existingCells)
        {
            Debug.Log($"[PepelacGrid] Existing Cell: {cell}");
        }
    }

    [ContextMenu("Debug Print Buildable Connectivity")]
    private void DebugPrintBuildableConnectivity()
    {
        Debug.Log($"[PepelacGrid] Buildable Adjacency Count = {buildableAdjacency.Count}");

        foreach (var kvp in buildableAdjacency)
        {
            string neighbors = kvp.Value != null
                ? string.Join(", ", kvp.Value)
                : "none";

            Debug.Log($"[PepelacGrid] Cell {kvp.Key} -> Neighbors: {neighbors}");
        }
    }

    [ContextMenu("Debug Print Buildable Regions")]
    private void DebugPrintBuildableRegions()
    {
        Debug.Log($"[PepelacGrid] Buildable Regions Count = {buildableRegionIds.Count}");

        foreach (var kvp in buildableRegionIds)
        {
            Debug.Log($"[PepelacGrid] Cell {kvp.Key} -> Region {kvp.Value}");
        }
    }

    [ContextMenu("Debug Print Region Summary")]
    private void DebugPrintRegionSummary()
    {
        Dictionary<int, List<Vector2Int>> regions = GetBuildableRegionsMap();

        Debug.Log(
            $"[PepelacGrid] Region Summary | " +
            $"Existing={existingCells.Count} | " +
            $"Buildable={buildableCells.Count} | " +
            $"Regions={regions.Count}");

        foreach (var kvp in regions)
        {
            int regionId = kvp.Key;
            int count = kvp.Value != null ? kvp.Value.Count : 0;
            Debug.Log($"[PepelacGrid] Region {regionId} -> {count} cells");
        }

        int largest = GetLargestBuildableRegionId();
        if (largest > 0)
            Debug.Log($"[PepelacGrid] Largest Region = {largest} ({GetBuildableRegionSize(largest)} cells)");
    }

    [ContextMenu("Debug Print Small Regions")]
    private void DebugPrintSmallRegions()
    {
        Dictionary<int, List<Vector2Int>> regions = GetBuildableRegionsMap();

        foreach (var kvp in regions)
        {
            int regionId = kvp.Key;
            int count = kvp.Value != null ? kvp.Value.Count : 0;

            if (count <= 4)
            {
                string cellsText = kvp.Value != null
                    ? string.Join(", ", kvp.Value)
                    : "none";

                Debug.Log($"[PepelacGrid] Small Region {regionId} ({count} cells): {cellsText}");
            }
        }
    }

    [ContextMenu("Debug Print Placed Modules")]
    private void DebugPrintPlacedModules()
    {
        Debug.Log($"[PepelacGrid] Placed Modules Count = {placedModuleRecords.Count}");

        foreach (var kvp in placedModuleRecords)
        {
            RuntimeModuleBase module = kvp.Key;
            PlacedModuleRecord record = kvp.Value;

            if (module == null || record == null) continue;

            Debug.Log(
                $"[PepelacGrid] Module={module.name} | " +
                $"Anchor={record.anchorCell} | " +
                $"Orientation={record.orientation} | " +
                $"Region={record.buildableRegionId} | " +
                $"Cells={record.occupiedCells.Count} | " +
                $"BuildAnchorCellLocal={record.buildAnchorCellLocal}");
        }
    }

    [ContextMenu("Debug Print Grid Summary")]
    private void DebugPrintGridSummary()
    {
        RefreshDebugSummary();

        Debug.Log(
            $"[PepelacGrid] Grid Summary | " +
            $"Existing={existingCellsCount} | " +
            $"Buildable={buildableCellsCount} | " +
            $"PlacedModules={placedModulesCount} | " +
            $"PlacedRecords={placedRecordsCount} | " +
            $"Regions={buildableRegionCount} | " +
            $"LargestRegion={largestBuildableRegionId} ({largestBuildableRegionSize} cells)");
    }

    [ContextMenu("Validate Grid State")]
    private void ValidateGridState()
    {
        int errorCount = 0;
        int warningCount = 0;

        foreach (var coords in existingCells)
        {
            if (!IsInsideBounds(coords.x, coords.y))
            {
                Debug.LogError($"[PepelacGrid] Existing cell out of bounds: {coords}");
                errorCount++;
                continue;
            }

            if (GetCell(coords.x, coords.y) == null)
            {
                Debug.LogError($"[PepelacGrid] Existing cell has null GridCell: {coords}");
                errorCount++;
            }
        }

        for (int x = 0; x < gridWidth; x++)
        {
            for (int z = 0; z < gridHeight; z++)
            {
                Vector2Int coords = new Vector2Int(x, z);
                bool existsInSet = existingCells.Contains(coords);
                bool existsInArray = cells != null && cells[x, z] != null;

                if (existsInSet != existsInArray)
                {
                    Debug.LogError($"[PepelacGrid] Existing cell mismatch at {coords}: set={existsInSet}, array={existsInArray}");
                    errorCount++;
                }
            }
        }

        foreach (var coords in buildableCells)
        {
            if (!existingCells.Contains(coords))
            {
                Debug.LogError($"[PepelacGrid] Buildable cell is not existing: {coords}");
                errorCount++;
            }
        }

        foreach (var kvp in buildableAdjacency)
        {
            Vector2Int cell = kvp.Key;
            List<Vector2Int> neighbors = kvp.Value;

            if (!buildableCells.Contains(cell))
            {
                Debug.LogError($"[PepelacGrid] Adjacency key is not buildable: {cell}");
                errorCount++;
            }

            if (neighbors == null)
            {
                Debug.LogWarning($"[PepelacGrid] Adjacency neighbors list is null for {cell}");
                warningCount++;
                continue;
            }

            foreach (var neighbor in neighbors)
            {
                if (!buildableCells.Contains(neighbor))
                {
                    Debug.LogError($"[PepelacGrid] Adjacency neighbor is not buildable: {cell} -> {neighbor}");
                    errorCount++;
                }

                int manhattan = Mathf.Abs(cell.x - neighbor.x) + Mathf.Abs(cell.y - neighbor.y);
                if (manhattan != 1)
                {
                    Debug.LogError($"[PepelacGrid] Adjacency contains non-cardinal neighbor: {cell} -> {neighbor}");
                    errorCount++;
                }
            }
        }

        foreach (var kvp in buildableRegionIds)
        {
            if (!buildableCells.Contains(kvp.Key))
            {
                Debug.LogError($"[PepelacGrid] Region entry points to non-buildable cell: {kvp.Key}");
                errorCount++;
            }
        }

        HashSet<Vector2Int> occupiedFromRecords = new HashSet<Vector2Int>();

        foreach (var kvp in placedModuleRecords)
        {
            RuntimeModuleBase module = kvp.Key;
            PlacedModuleRecord record = kvp.Value;

            if (module == null)
            {
                Debug.LogError("[PepelacGrid] placedModuleRecords contains null module key.");
                errorCount++;
                continue;
            }

            if (record == null)
            {
                Debug.LogError($"[PepelacGrid] Module {module.name} has null placement record.");
                errorCount++;
                continue;
            }

            if (!installedModules.Contains(module))
            {
                Debug.LogWarning($"[PepelacGrid] Module {module.name} has placement record but is absent in installedModules.");
                warningCount++;
            }

            if (record.occupiedCells == null || record.occupiedCells.Count == 0)
            {
                Debug.LogError($"[PepelacGrid] Module {module.name} has empty occupiedCells record.");
                errorCount++;
                continue;
            }

            foreach (var coords in record.occupiedCells)
            {
                if (!existingCells.Contains(coords))
                {
                    Debug.LogError($"[PepelacGrid] Module {module.name} occupies non-existing cell {coords}");
                    errorCount++;
                    continue;
                }

                GridCell cell = GetCell(coords.x, coords.y);
                if (cell == null)
                {
                    Debug.LogError($"[PepelacGrid] Module {module.name} record points to null cell {coords}");
                    errorCount++;
                    continue;
                }

                if (!cell.isOccupied)
                {
                    Debug.LogError($"[PepelacGrid] Module {module.name} record cell is not marked occupied: {coords}");
                    errorCount++;
                }

                if (cell.occupant != module)
                {
                    Debug.LogError($"[PepelacGrid] Module {module.name} record mismatch occupant at {coords}. Actual occupant={cell.occupant}");
                    errorCount++;
                }

                if (!occupiedFromRecords.Add(coords))
                {
                    Debug.LogError($"[PepelacGrid] Overlapping placement records at cell {coords}");
                    errorCount++;
                }
            }
        }

        for (int x = 0; x < gridWidth; x++)
        {
            for (int z = 0; z < gridHeight; z++)
            {
                GridCell cell = cells[x, z];
                if (cell == null) continue;

                if (!cell.isOccupied)
                    continue;

                Vector2Int coords = new Vector2Int(x, z);

                if (cell.occupant == null)
                {
                    Debug.LogError($"[PepelacGrid] Cell {coords} marked occupied but occupant is null.");
                    errorCount++;
                }

                if (!occupiedFromRecords.Contains(coords))
                {
                    Debug.LogWarning($"[PepelacGrid] Occupied cell {coords} is not covered by any placement record.");
                    warningCount++;
                }
            }
        }

        RefreshDebugSummary();

        if (errorCount == 0 && warningCount == 0)
        {
            Debug.Log("[PepelacGrid] Validate Grid State: OK (no errors, no warnings).");
        }
        else
        {
            Debug.LogWarning(
                $"[PepelacGrid] Validate Grid State finished with Errors={errorCount}, Warnings={warningCount}.");
        }
    }
}