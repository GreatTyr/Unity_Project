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
    [Tooltip("Количество клеток по ширине Пепелаца (ось X)")]
    public int gridWidth = 10;
    [Tooltip("Количество клеток по длине Пепелаца (ось Z)")]
    public int gridHeight = 8;
    [Tooltip("Размер одной клетки в метрах")]
    public float cellSize = 1.0f;

    private GridCell[,] cells;
    private List<RuntimeModuleBase> installedModules = new List<RuntimeModuleBase>();

    private void Awake()
    {
        cells = new GridCell[gridWidth, gridHeight];
        for (int x = 0; x < gridWidth; x++)
        {
            for (int z = 0; z < gridHeight; z++)
            {
                cells[x, z] = new GridCell();
            }
        }
    }

    public IReadOnlyList<RuntimeModuleBase> GetAllModules() => installedModules;

    // ==========================================
    // СИСТЕМА КООРДИНАТ (Конвертация 2D <-> 3D)
    // ==========================================

    public Vector2Int LocalToGridPosition(Vector3 localPosition)
    {
        float offsetX = -(gridWidth * cellSize) / 2f;
        float offsetZ = -(gridHeight * cellSize) / 2f;

        int cellX = Mathf.FloorToInt((localPosition.x - offsetX) / cellSize);
        int cellZ = Mathf.FloorToInt((localPosition.z - offsetZ) / cellSize);

        if (cellX < 0 || cellX >= gridWidth || cellZ < 0 || cellZ >= gridHeight)
        {
            return new Vector2Int(-1, -1);
        }

        return new Vector2Int(cellX, cellZ);
    }

    public Vector3 GridToLocalPosition(int cellX, int cellZ)
    {
        float offsetX = -(gridWidth * cellSize) / 2f;
        float offsetZ = -(gridHeight * cellSize) / 2f;

        float x = offsetX + (cellX * cellSize) + (cellSize / 2f);
        float z = offsetZ + (cellZ * cellSize) + (cellSize / 2f);

        return new Vector3(x, 0f, z);
    }

    // ==========================================
    // ЛОГИКА РАЗМЕЩЕНИЯ И ПРОВЕРКИ
    // ==========================================

    /// <summary>
    /// Вычисляет габариты модуля в клетках сетки.
    /// Ось X = Ширина (Width), Ось Z = Длина (Length).
    /// Учитывает поворот модуля (Orientation).
    /// </summary>
    public Vector2Int CalculateGridSize(float lengthMeters, float widthMeters, ModuleOrientation orientation)
    {
        int cellsLength = Mathf.CeilToInt(lengthMeters / cellSize);
        int cellsWidth = Mathf.CeilToInt(widthMeters / cellSize);

        // По умолчанию: длина смотрит вдоль оси Z (вперёд), ширина вдоль X (вправо)
        int sizeX = cellsWidth;
        int sizeZ = cellsLength;

        // Если модуль повернут на 90 или 270 градусов, оси меняются местами
        if (orientation == ModuleOrientation.Deg90 || orientation == ModuleOrientation.Deg270)
        {
            sizeX = cellsLength;
            sizeZ = cellsWidth;
        }

        return new Vector2Int(sizeX, sizeZ);
    }

    /// <summary>
    /// Проверяет, можно ли разместить модуль с заданными размерами в указанном центре.
    /// Возвращает список клеток, которые он займёт (null, если размещение невозможно).
    /// </summary>
    public List<Vector2Int> GetPlacementFootprint(Vector2Int centerCell, Vector2Int gridSize)
    {
        List<Vector2Int> footprint = new List<Vector2Int>();

        int startX = centerCell.x - (gridSize.x / 2);
        int startZ = centerCell.y - (gridSize.y / 2);
        int endX = startX + gridSize.x - 1;
        int endZ = startZ + gridSize.y - 1;

        // Проверка выхода за границы сетки
        if (startX < 0 || endX >= gridWidth || startZ < 0 || endZ >= gridHeight)
        {
            return null;
        }

        // Проверка занятости клеток
        for (int x = startX; x <= endX; x++)
        {
            for (int z = startZ; z <= endZ; z++)
            {
                if (cells[x, z].isOccupied)
                {
                    return null; // Наложение на другой модуль
                }
                footprint.Add(new Vector2Int(x, z));
            }
        }

        return footprint;
    }

    /// <summary>
    /// Пытается разместить модуль на сетке. 
    /// Возвращает true и "прописывает" модуль в клетках, если место свободно.
    /// </summary>
    public bool TryPlaceModule(RuntimeModuleBase module, Vector2Int centerCell, float lengthMeters, float widthMeters)
    {
        Vector2Int gridSize = CalculateGridSize(lengthMeters, widthMeters, module.Orientation);
        List<Vector2Int> footprint = GetPlacementFootprint(centerCell, gridSize);

        if (footprint == null)
        {
            Debug.LogWarning($"[PepelacGrid] Невозможно разместить {module.name} в ({centerCell.x}, {centerCell.y}). Нет места.");
            return false; // Нет места или выход за границы
        }

        // Размещаем
        foreach (var cellPos in footprint)
        {
            cells[cellPos.x, cellPos.y].isOccupied = true;
            cells[cellPos.x, cellPos.y].occupant = module;
        }

        // Записываем данные в сам модуль, чтобы он знал о себе
        module.GridPosition = centerCell;

        // ВАЖНО: Мы должны добавить поле GridSize в RuntimeModuleBase, 
        // но пока я не трогаю тот скрипт, оставлю комментарием на будущее, 
        // если оно понадобится для физики (расчет центра масс)
        // module.GridSize = gridSize; 

        installedModules.Add(module);
        return true;
    }

    /// <summary>
    /// Удаляет модуль с сетки и освобождает занимаемые им клетки.
    /// </summary>
    public void RemoveModule(RuntimeModuleBase module, float lengthMeters, float widthMeters)
    {
        if (!installedModules.Contains(module)) return;

        Vector2Int gridSize = CalculateGridSize(lengthMeters, widthMeters, module.Orientation);

        int startX = module.GridPosition.x - (gridSize.x / 2);
        int startZ = module.GridPosition.y - (gridSize.y / 2);
        int endX = startX + gridSize.x - 1;
        int endZ = startZ + gridSize.y - 1;

        for (int x = startX; x <= endX; x++)
        {
            for (int z = startZ; z <= endZ; z++)
            {
                // Защита от выхода за индексы (на случай багов)
                if (x >= 0 && x < gridWidth && z >= 0 && z < gridHeight)
                {
                    if (cells[x, z].occupant == module)
                    {
                        cells[x, z].isOccupied = false;
                        cells[x, z].occupant = null;
                    }
                }
            }
        }

        installedModules.Remove(module);
    }
}