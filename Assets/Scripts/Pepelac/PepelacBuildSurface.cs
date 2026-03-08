using UnityEngine;

[DisallowMultipleComponent]
[ExecuteAlways]
[RequireComponent(typeof(BoxCollider))]
public class PepelacBuildSurface : MonoBehaviour
{
    [Header("Grid Settings")]
    [Min(0.01f)]
    [SerializeField] private float cellSize = 0.33f;

    [Header("Debug / Computed")]
    [SerializeField, HideInInspector] private int gridWidth;
    [SerializeField, HideInInspector] private int gridHeight;
    [SerializeField, HideInInspector] private Vector3 localGridMin;
    [SerializeField, HideInInspector] private Vector3 localGridMax;
    [SerializeField, HideInInspector] private Vector3 localGridCenter;

    private BoxCollider surfaceCollider;

    public float CellSize => cellSize;
    public int GridWidth => gridWidth;
    public int GridHeight => gridHeight;
    public Vector3 LocalGridMin => localGridMin;
    public Vector3 LocalGridMax => localGridMax;
    public Vector3 LocalGridCenter => localGridCenter;
    public BoxCollider SurfaceCollider => surfaceCollider;

    private void Awake()
    {
        ResolveCollider();
        RecalculateSurface();
    }

    private void OnValidate()
    {
        ResolveCollider();
        RecalculateSurface();
    }

    private void ResolveCollider()
    {
        if (surfaceCollider == null)
            surfaceCollider = GetComponent<BoxCollider>();
    }

    [ContextMenu("Recalculate Build Surface")]
    public void RecalculateSurface()
    {
        ResolveCollider();

        if (surfaceCollider == null)
        {
            gridWidth = 0;
            gridHeight = 0;
            localGridMin = Vector3.zero;
            localGridMax = Vector3.zero;
            localGridCenter = Vector3.zero;
            return;
        }

        Vector3 boxCenter = surfaceCollider.center;
        Vector3 boxSize = surfaceCollider.size;

        float usableWidth = Mathf.Max(0f, boxSize.x);
        float usableLength = Mathf.Max(0f, boxSize.z);

        gridWidth = Mathf.Max(1, Mathf.FloorToInt(usableWidth / cellSize));
        gridHeight = Mathf.Max(1, Mathf.FloorToInt(usableLength / cellSize));

        float snappedWidth = gridWidth * cellSize;
        float snappedLength = gridHeight * cellSize;

        float unusedWidth = usableWidth - snappedWidth;
        float unusedLength = usableLength - snappedLength;

        float minX = boxCenter.x - usableWidth * 0.5f + unusedWidth * 0.5f;
        float maxX = minX + snappedWidth;

        float minZ = boxCenter.z - usableLength * 0.5f + unusedLength * 0.5f;
        float maxZ = minZ + snappedLength;

        float y = boxCenter.y;

        localGridMin = new Vector3(minX, y, minZ);
        localGridMax = new Vector3(maxX, y, maxZ);
        localGridCenter = new Vector3((minX + maxX) * 0.5f, y, (minZ + maxZ) * 0.5f);
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

        cell = new Vector2Int(cellX, cellZ);
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
}