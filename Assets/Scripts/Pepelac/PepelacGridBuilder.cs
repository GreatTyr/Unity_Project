using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections.Generic;

/// <summary>
/// Ядро режима строительства Пепелаца.
/// Управляет "призраком" (Ghost) модуля, Raycast'ом по сетке и фактической установкой.
/// </summary>
[RequireComponent(typeof(PepelacGrid))]
public class PepelacGridBuilder : MonoBehaviour
{
    [Header("References")]
    [Tooltip("Камера Билдера (которая смотрит сверху)")]
    public Camera builderCamera;

    [Tooltip("Ссылка на ModuleStorage для списания предметов")]
    public ModuleStorage moduleStorage;

    [Header("Databases (для спавна префабов)")]
    public GeneratorDatabase generatorDb;
    public EnergyStorageDatabase energyStorageDb;
    public FuelTankDatabase fuelTankDb;

    [Header("Ghost Visuals")]
    public Material validGhostMaterial;    // Полупрозрачный зеленый
    public Material invalidGhostMaterial;  // Полупрозрачный красный

    [Header("Input")]
    public InputActionReference rotateAction; // Назначь на R
    public InputActionReference clickAction;  // Назначь на ЛКМ

    private PepelacGrid grid;

    // Стейт
    private ModuleData selectedData;
    private string selectedCode;
    private ModuleOrientation currentOrientation = ModuleOrientation.Deg0;

    // Ghost объект
    private GameObject ghostObject;

    // Кэш текущего наведения
    private bool isHoveringGrid = false;
    private Vector2Int currentHoverCell;
    private bool isPlacementValid = false;

    private void Awake()
    {
        grid = GetComponent<PepelacGrid>();
    }

    private void OnEnable()
    {
        if (rotateAction?.action != null)
        {
            rotateAction.action.performed += OnRotatePerformed;
            rotateAction.action.Enable();
        }
        if (clickAction?.action != null)
        {
            clickAction.action.performed += OnClickPerformed;
            clickAction.action.Enable();
        }

        // При включении режима сбрасываем стейт
        ClearSelection();
    }

    private void OnDisable()
    {
        if (rotateAction?.action != null) rotateAction.action.performed -= OnRotatePerformed;
        if (clickAction?.action != null) clickAction.action.performed -= OnClickPerformed;

        ClearSelection();
    }

    // =========================================
    // ВЗАИМОДЕЙСТВИЕ С UI (Выбор модуля)
    // =========================================

    public void SetSelectedModule(ModuleData data, string code)
    {
        selectedData = data;
        selectedCode = code;
        currentOrientation = ModuleOrientation.Deg0;

        DestroyGhost();

        if (data != null)
        {
            CreateGhost(data);
        }
    }

    public void ClearSelection()
    {
        selectedData = null;
        selectedCode = null;
        DestroyGhost();
    }

    // =========================================
    // GHOST (Создание и Обновление)
    // =========================================

    private void CreateGhost(ModuleData data)
    {
        // Спавним реальную модельку вместо куба
        ghostObject = SpawnRealModulePrefab(data);
        if (ghostObject == null) return;

        ghostObject.name = "GridGhost";

        // Убираем все коллайдеры с призрака
        var colliders = ghostObject.GetComponentsInChildren<Collider>();
        foreach (var col in colliders) Destroy(col);

        // Находим все меш-рендереры и меняем им материал на "призрачный"
        Renderer[] renderers = ghostObject.GetComponentsInChildren<Renderer>();
        foreach (var rend in renderers)
        {
            rend.material = validGhostMaterial;
        }

        // Привязываем призрака к сетке
        ghostObject.transform.SetParent(grid.transform, false);

        UpdateGhostScale();
    }

    private void DestroyGhost()
    {
        if (ghostObject != null)
        {
            Destroy(ghostObject);
            ghostObject = null;
        }
    }

    private void UpdateGhostScale()
    {
        if (ghostObject == null || selectedData == null) return;

        // Применяем единый правильный масштаб
        float s = Mathf.Max(0.001f, selectedData.scaleFactor);
        ghostObject.transform.localScale = Vector3.one * s;

        // Вращаем сам объект
        float yRot = 0f;
        switch (currentOrientation)
        {
            case ModuleOrientation.Deg90: yRot = 90f; break;
            case ModuleOrientation.Deg180: yRot = 180f; break;
            case ModuleOrientation.Deg270: yRot = 270f; break;
        }

        ghostObject.transform.localRotation = Quaternion.Euler(0f, yRot, 0f);
    }

    private void OnRotatePerformed(InputAction.CallbackContext ctx)
    {
        if (selectedData == null || ghostObject == null) return;

        // Крутим ориентацию
        currentOrientation = (ModuleOrientation)(((int)currentOrientation + 1) % 4);
        UpdateGhostScale();
    }

    // =========================================
    // UPDATE (Raycast и Снэппинг)
    // =========================================

    private void Update()
    {
        if (selectedData == null || ghostObject == null || builderCamera == null) return;

        Vector2 mousePos = Mouse.current.position.ReadValue();
        Ray ray = builderCamera.ScreenPointToRay(mousePos);

        // Пускаем Raycast, получаем ВСЕ хиты (чтобы пробить сквозь другие коллайдеры Пепелаца)
        RaycastHit[] hits = Physics.RaycastAll(ray, 100f);
        bool hitGrid = false;

        foreach (var hit in hits)
        {
            if (hit.collider.transform == grid.transform)
            {
                hitGrid = true;
                isHoveringGrid = true;

                Vector3 localHitPos = grid.transform.InverseTransformPoint(hit.point);
                currentHoverCell = grid.LocalToGridPosition(localHitPos);

                if (currentHoverCell.x < 0)
                {
                    isHoveringGrid = false;
                    ghostObject.SetActive(false);
                    return;
                }

                ghostObject.SetActive(true);

                Vector2Int gridSize = grid.CalculateGridSize(selectedData.length, selectedData.width, currentOrientation);
                var footprint = grid.GetPlacementFootprint(currentHoverCell, gridSize);
                isPlacementValid = (footprint != null);

                // Красим призрака (все его меши)
                Material targetMat = isPlacementValid ? validGhostMaterial : invalidGhostMaterial;
                Renderer[] renderers = ghostObject.GetComponentsInChildren<Renderer>();
                foreach (var rend in renderers)
                {
                    if (rend.sharedMaterial != targetMat)
                        rend.material = targetMat;
                }

                // Привязываем призрака к центру клетки
                Vector3 localSnapPos = grid.GridToLocalPosition(currentHoverCell.x, currentHoverCell.y);

                // Если префаб проваливается в пол — раскомментируй строку ниже:
                // localSnapPos.y += (selectedData.height / 2f); 

                ghostObject.transform.localPosition = localSnapPos;

                break; // Нашли сетку, дальше не ищем
            }
        }

        if (!hitGrid)
        {
            isHoveringGrid = false;
            ghostObject.SetActive(false);
        }
    }

    // =========================================
    // УСТАНОВКА (Клик ЛКМ)
    // =========================================

    private void OnClickPerformed(InputAction.CallbackContext ctx)
    {
        if (selectedData == null || !isHoveringGrid || !isPlacementValid) return;

        // 1. Создаем пустую модель
        GameObject newModuleObj = SpawnRealModulePrefab(selectedData);
        if (newModuleObj == null)
        {
            Debug.LogError("[PepelacGridBuilder] Не удалось найти префаб для спавна!");
            return;
        }

        // СНАЧАЛА вешаем CraftedModule и загружаем в него Data
        var craftedComp = newModuleObj.AddComponent<CraftedModule>();
        craftedComp.SetData(selectedData);

        // И только ПОТОМ вешаем Runtime-компонент
        RuntimeModuleBase runtimeMod = AddRuntimeComponent(newModuleObj, selectedData);
        runtimeMod.Orientation = currentOrientation;

        // Пытаемся занять клетки в математике сетки
        bool success = grid.TryPlaceModule(runtimeMod, currentHoverCell, selectedData.length, selectedData.width);
        if (!success)
        {
            Destroy(newModuleObj);
            return;
        }

        // 2. Физически размещаем объект на сцене
        newModuleObj.transform.SetParent(grid.transform, false);
        Vector3 localPos = grid.GridToLocalPosition(currentHoverCell.x, currentHoverCell.y);
        newModuleObj.transform.localPosition = localPos;

        float yRot = 0f;
        switch (currentOrientation)
        {
            case ModuleOrientation.Deg90: yRot = 90f; break;
            case ModuleOrientation.Deg180: yRot = 180f; break;
            case ModuleOrientation.Deg270: yRot = 270f; break;
        }
        newModuleObj.transform.localRotation = Quaternion.Euler(0f, yRot, 0f);

        newModuleObj.transform.localScale = Vector3.one * Mathf.Max(0.001f, selectedData.scaleFactor);

        // 3. Списываем со склада
        moduleStorage.RemoveModule(selectedCode, 1);

        Debug.Log($"[PepelacGridBuilder] Успешно установлен {selectedData.moduleType}!");

        ClearSelection();
    }

    // =========================================
    // ХЕЛПЕРЫ СПАВНА
    // =========================================

    private GameObject SpawnRealModulePrefab(ModuleData data)
    {
        StandardModuleBase reference = null;

        if (data.moduleType == StandardGenerator.TYPE_GENERATOR)
            reference = generatorDb.GetByName(data.referenceName);
        else if (data.moduleType == StandardEnergyStorage.TYPE_ENERGY_STORAGE)
            reference = energyStorageDb.GetByName(data.referenceName);
        else if (data.moduleType == StandardFuelTank.TYPE_FUELTANK)
            reference = fuelTankDb.GetByName(data.referenceName);

        if (reference == null) return null;

        GameObject instance = Instantiate(reference.gameObject);
        Destroy(instance.GetComponent<StandardModuleBase>());

        return instance;
    }

    private RuntimeModuleBase AddRuntimeComponent(GameObject obj, ModuleData data)
    {
        if (data.moduleType == StandardGenerator.TYPE_GENERATOR)
            return obj.AddComponent<RuntimeGenerator>();

        if (data.moduleType == StandardEnergyStorage.TYPE_ENERGY_STORAGE)
            return obj.AddComponent<RuntimeEnergyStorage>();

        if (data.moduleType == StandardFuelTank.TYPE_FUELTANK)
            return obj.AddComponent<RuntimeFuelTank>();

        return obj.AddComponent<RuntimeFuelTank>();
    }
}