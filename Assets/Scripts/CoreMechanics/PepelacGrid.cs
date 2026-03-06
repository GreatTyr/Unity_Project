using UnityEngine;
using System.Collections.Generic;

[System.Serializable]
public class GridCell
{
    public bool isOccupied;
    public RuntimeModuleBase occupant;
}

public class PepelacGrid : MonoBehaviour
{
    [Header("Grid Config")]
    public int gridWidth = 10;
    public int gridHeight = 8;
    public float cellSize = 1.0f;

    private GridCell[,] cells;
    private List<RuntimeModuleBase> installedModules = new List<RuntimeModuleBase>();

    private void Awake()
    {
        cells = new GridCell[gridWidth, gridHeight];
        for (int x = 0; x < gridWidth; x++)
            for (int z = 0; z < gridHeight; z++)
                cells[x, z] = new GridCell();
    }

    public IReadOnlyList<RuntimeModuleBase> GetAllModules() => installedModules;

    public bool TryPlaceModule(RuntimeModuleBase module, Vector2Int centerCell)
    {
        // TODO: –еализаци€ проверки €чеек с учетом габаритов и поворота
        installedModules.Add(module);
        module.GridPosition = centerCell;
        return true;
    }

    public void RemoveModule(RuntimeModuleBase module)
    {
        installedModules.Remove(module);
        // TODO: ќсвободить клетки
    }

    public Vector3 GridToLocalPosition(float cellX, float cellZ)
    {
        float offsetX = -(gridWidth * cellSize) / 2f;
        float offsetZ = -(gridHeight * cellSize) / 2f;
        return new Vector3(offsetX + (cellX + 0.5f) * cellSize, 0f, offsetZ + (cellZ + 0.5f) * cellSize);
    }
}