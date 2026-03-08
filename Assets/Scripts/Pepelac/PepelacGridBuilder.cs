using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections.Generic;

/// <summary>
/// Ядро режима строительства Пепелаца.
/// Работает через PepelacBuildSurface + PepelacGrid + PepelacGridOverlay.
/// Placement строится от anchor cell.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(PepelacGrid))]
public class PepelacGridBuilder : MonoBehaviour
{
    [Header("Raycast Camera")]
    [Tooltip("Реальная Unity Camera, из которой выполняется raycast. Если не назначена — используется Camera.main.")]
    public Camera raycastCamera;

    [Header("References")]
    [Tooltip("Ссылка на ModuleStorage для списания/возврата модулей")]
    public ModuleStorage moduleStorage;

    [Header("Databases (legacy fallback)")]
    public GeneratorDatabase generatorDb;
    public EnergyStorageDatabase energyStorageDb;
    public FuelTankDatabase fuelTankDb;

    [Header("Ghost & Highlight Visuals")]
    public Material validGhostMaterial;
    public Material invalidGhostMaterial;

    [Tooltip("Цвет подсветки установленного модуля при наведении (Emission)")]
    [ColorUsage(false, true)]
    public Color highlightEmissionColor = new Color(0.8f, 0.4f, 0f, 1f);

    [Header("Footprint Overlay Colors")]
    [SerializeField] private Color validFootprintColor = new Color(0.2f, 1f, 0.2f, 0.35f);
    [SerializeField] private Color invalidFootprintColor = new Color(1f, 0.2f, 0.2f, 0.35f);

    [Header("Input")]
    public InputActionReference rotateAction;
    public InputActionReference clickAction;
    public InputActionReference cancelAction;

    [Header("Raycast")]
    [Min(1f)]
    [SerializeField] private float maxBuildRayDistance = 500f;

    private PepelacGrid grid;
    private PepelacBuildSurface buildSurface;
    private PepelacGridOverlay gridOverlay;

    private ModuleData selectedData;
    private string selectedCode;
    private ModuleOrientation currentOrientation = ModuleOrientation.Deg0;

    private GameObject ghostObject;

    private bool isHoveringGrid;
    private Vector2Int currentHoverCell = new Vector2Int(-1, -1); // anchor cell
    private bool isPlacementValid;

    private RuntimeModuleBase hoveredInstalledModule;

    private void Awake()
    {
        ResolveReferences();
    }

    private void OnValidate()
    {
        ResolveReferences();
    }

    private void ResolveReferences()
    {
        if (grid == null)
            grid = GetComponent<PepelacGrid>();

        if (buildSurface == null && grid != null)
            buildSurface = grid.BuildSurface;

        if (buildSurface == null)
            buildSurface = GetComponent<PepelacBuildSurface>();

        if (gridOverlay == null)
            gridOverlay = GetComponentInChildren<PepelacGridOverlay>(true);
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

        if (cancelAction?.action != null)
        {
            cancelAction.action.performed += OnCancelPerformed;
            cancelAction.action.Enable();
        }

        ClearSelection();
    }

    private void OnDisable()
    {
        if (rotateAction?.action != null)
        {
            rotateAction.action.performed -= OnRotatePerformed;
            rotateAction.action.Disable();
        }

        if (clickAction?.action != null)
        {
            clickAction.action.performed -= OnClickPerformed;
            clickAction.action.Disable();
        }

        if (cancelAction?.action != null)
        {
            cancelAction.action.performed -= OnCancelPerformed;
            cancelAction.action.Disable();
        }

        ClearSelection();

        if (hoveredInstalledModule != null)
        {
            SetHighlight(hoveredInstalledModule, false);
            hoveredInstalledModule = null;
        }
    }

    // =========================================
    // ВЗАИМОДЕЙСТВИЕ С UI
    // =========================================

    public void SetSelectedModule(ModuleData data, string code)
    {
        selectedData = data;
        selectedCode = code;
        currentOrientation = ModuleOrientation.Deg0;

        DestroyGhost();

        if (hoveredInstalledModule != null)
        {
            SetHighlight(hoveredInstalledModule, false);
            hoveredInstalledModule = null;
        }

        if (data != null)
            CreateGhost(data);
    }

    public void ClearSelection()
    {
        selectedData = null;
        selectedCode = null;
        currentOrientation = ModuleOrientation.Deg0;
        isPlacementValid = false;
        currentHoverCell = new Vector2Int(-1, -1);

        DestroyGhost();

        if (gridOverlay != null)
            gridOverlay.HideFootprint();
    }

    // =========================================
    // GHOST
    // =========================================

    private void CreateGhost(ModuleData data)
    {
        ghostObject = SpawnRealModulePrefab(data);
        if (ghostObject == null)
            return;

        ghostObject.name = "GridGhost";

        Collider[] colliders = ghostObject.GetComponentsInChildren<Collider>();
        foreach (var col in colliders)
            Destroy(col);

        Renderer[] renderers = ghostObject.GetComponentsInChildren<Renderer>();
        foreach (var rend in renderers)
        {
            if (validGhostMaterial != null)
                rend.material = validGhostMaterial;
        }

        ghostObject.transform.SetParent(transform, false);
        UpdateGhostTransformOnly();
    }

    private void DestroyGhost()
    {
        if (ghostObject != null)
        {
            Destroy(ghostObject);
            ghostObject = null;
        }
    }

    private void UpdateGhostTransformOnly()
    {
        if (ghostObject == null || selectedData == null)
            return;

        float s = Mathf.Max(0.001f, selectedData.scaleFactor);
        ghostObject.transform.localScale = Vector3.one * s;

        float yRot = GetFinalVisualYaw(selectedData);
        ghostObject.transform.localRotation = Quaternion.Euler(0f, yRot, 0f);
    }

    private void UpdateGhostPlacementVisual()
    {
        if (ghostObject == null || selectedData == null || !isHoveringGrid)
            return;

        Vector2Int gridSize = grid.CalculateGridSize(selectedData.length, selectedData.width, currentOrientation);
        Vector3 localSnapPos = grid.GridToLocalPosition(currentHoverCell.x, currentHoverCell.y, gridSize);

        ghostObject.transform.localPosition = localSnapPos;

        Material targetMat = isPlacementValid ? validGhostMaterial : invalidGhostMaterial;
        if (targetMat != null)
        {
            Renderer[] renderers = ghostObject.GetComponentsInChildren<Renderer>();
            foreach (var rend in renderers)
            {
                if (rend.sharedMaterial != targetMat)
                    rend.material = targetMat;
            }
        }
    }

    private void OnRotatePerformed(InputAction.CallbackContext ctx)
    {
        if (selectedData == null || ghostObject == null)
            return;

        currentOrientation = (ModuleOrientation)(((int)currentOrientation + 1) % 4);
        UpdateGhostTransformOnly();

        if (isHoveringGrid)
        {
            Vector2Int gridSize = grid.CalculateGridSize(selectedData.length, selectedData.width, currentOrientation);
            List<Vector2Int> footprint = grid.GetPlacementFootprint(currentHoverCell, gridSize);
            isPlacementValid = footprint != null;

            UpdateGhostPlacementVisual();
            UpdateFootprintOverlay(footprint, gridSize);
        }
    }

    // =========================================
    // UPDATE
    // =========================================

    private void Update()
    {
        ResolveReferences();

        if (grid == null || buildSurface == null)
            return;

        UpdateHoveredCell();

        if (selectedData != null && ghostObject != null)
            UpdatePlacementMode();
        else
            UpdateRemovalHoverMode();
    }

    private void UpdateHoveredCell()
    {
        isHoveringGrid = false;
        currentHoverCell = new Vector2Int(-1, -1);

        Camera rayCamera = ResolveRaycastCamera();
        if (rayCamera == null || Mouse.current == null || buildSurface.SurfaceCollider == null)
            return;

        Vector2 mousePos = Mouse.current.position.ReadValue();
        Ray ray = rayCamera.ScreenPointToRay(mousePos);

        if (!buildSurface.SurfaceCollider.Raycast(ray, out RaycastHit hit, maxBuildRayDistance))
            return;

        if (!buildSurface.TryWorldPointToCell(hit.point, out Vector2Int cell))
            return;

        isHoveringGrid = true;
        currentHoverCell = cell;
    }

    private Camera ResolveRaycastCamera()
    {
        if (raycastCamera != null)
            return raycastCamera;

        if (Camera.main != null)
            return Camera.main;

        Debug.LogWarning("[PepelacGridBuilder] Не найдена реальная raycast camera (raycastCamera и Camera.main == null).");
        return null;
    }

    private void UpdatePlacementMode()
    {
        if (!isHoveringGrid)
        {
            if (ghostObject != null)
                ghostObject.SetActive(false);

            isPlacementValid = false;

            if (gridOverlay != null)
                gridOverlay.HideFootprint();

            return;
        }

        if (ghostObject != null)
            ghostObject.SetActive(true);

        Vector2Int gridSize = grid.CalculateGridSize(selectedData.length, selectedData.width, currentOrientation);
        List<Vector2Int> footprint = grid.GetPlacementFootprint(currentHoverCell, gridSize);
        isPlacementValid = footprint != null;

        UpdateGhostPlacementVisual();
        UpdateFootprintOverlay(footprint, gridSize);
    }

    private void UpdateFootprintOverlay(List<Vector2Int> footprint, Vector2Int gridSize)
    {
        if (gridOverlay == null)
            return;

        if (footprint != null && footprint.Count > 0)
        {
            gridOverlay.ShowFootprint(
                footprint,
                isPlacementValid ? validFootprintColor : invalidFootprintColor
            );
            return;
        }

        List<Vector2Int> rawFootprint = BuildRawFootprint(currentHoverCell, gridSize);
        if (rawFootprint != null && rawFootprint.Count > 0)
        {
            gridOverlay.ShowFootprint(rawFootprint, invalidFootprintColor);
        }
        else
        {
            gridOverlay.HideFootprint();
        }
    }

    private List<Vector2Int> BuildRawFootprint(Vector2Int anchorCell, Vector2Int gridSize)
    {
        List<Vector2Int> footprint = new List<Vector2Int>();

        int startX = anchorCell.x;
        int startZ = anchorCell.y;
        int endX = startX + gridSize.x - 1;
        int endZ = startZ + gridSize.y - 1;

        for (int x = startX; x <= endX; x++)
        {
            for (int z = startZ; z <= endZ; z++)
            {
                footprint.Add(new Vector2Int(x, z));
            }
        }

        return footprint;
    }

    private void UpdateRemovalHoverMode()
    {
        if (gridOverlay != null)
            gridOverlay.HideFootprint();

        RuntimeModuleBase moduleUnderMouse = null;

        if (isHoveringGrid)
        {
            GridCell cell = grid.GetCell(currentHoverCell.x, currentHoverCell.y);
            if (cell != null && cell.isOccupied)
                moduleUnderMouse = cell.occupant;
        }

        if (moduleUnderMouse != hoveredInstalledModule)
        {
            if (hoveredInstalledModule != null)
                SetHighlight(hoveredInstalledModule, false);

            hoveredInstalledModule = moduleUnderMouse;

            if (hoveredInstalledModule != null)
                SetHighlight(hoveredInstalledModule, true);
        }
    }

    private void SetHighlight(RuntimeModuleBase module, bool enable)
    {
        if (module == null)
            return;

        Renderer[] renderers = module.GetComponentsInChildren<Renderer>();
        foreach (var rend in renderers)
        {
            Material mat = rend.material;

            if (enable)
            {
                mat.EnableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", highlightEmissionColor);
            }
            else
            {
                mat.DisableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", Color.black);
            }
        }
    }

    // =========================================
    // УСТАНОВКА
    // =========================================

    private void OnClickPerformed(InputAction.CallbackContext ctx)
    {
        if (selectedData == null || !isHoveringGrid || !isPlacementValid)
            return;

        GameObject newModuleObj = SpawnRealModulePrefab(selectedData);
        if (newModuleObj == null)
        {
            Debug.LogError("[PepelacGridBuilder] Не удалось найти префаб для спавна!");
            return;
        }

        CraftedModule craftedComp = newModuleObj.AddComponent<CraftedModule>();
        craftedComp.SetData(selectedData);

        RuntimeModuleBase runtimeMod = AddRuntimeComponent(newModuleObj, selectedData);
        if (runtimeMod == null)
        {
            Destroy(newModuleObj);
            return;
        }

        runtimeMod.Orientation = currentOrientation;

        if (selectedData.isVolatile)
        {
            RuntimeVolatileModule volatileModule = newModuleObj.AddComponent<RuntimeVolatileModule>();
            volatileModule.Initialize(
                selectedData.totalMassKg,
                selectedData.moduleTier,
                selectedData.effectiveVolume,
                selectedData.explosionDamageType);
        }

        bool success = grid.TryPlaceModule(runtimeMod, currentHoverCell, selectedData.length, selectedData.width);
        if (!success)
        {
            Destroy(newModuleObj);
            return;
        }

        newModuleObj.transform.SetParent(transform, false);

        Vector2Int gridSize = grid.CalculateGridSize(selectedData.length, selectedData.width, currentOrientation);
        Vector3 localPos = grid.GridToLocalPosition(currentHoverCell.x, currentHoverCell.y, gridSize);
        newModuleObj.transform.localPosition = localPos;

        float yRot = GetFinalVisualYaw(selectedData);
        newModuleObj.transform.localRotation = Quaternion.Euler(0f, yRot, 0f);
        newModuleObj.transform.localScale = Vector3.one * Mathf.Max(0.001f, selectedData.scaleFactor);

        if (moduleStorage != null)
        {
            moduleStorage.RemoveModule(selectedCode, 1);
        }
        else
        {
            Debug.LogWarning("[PepelacGridBuilder] ModuleStorage не назначен, модуль не будет списан со склада.");
        }

        Debug.Log($"[PepelacGridBuilder] Установлен модуль {selectedData.moduleType} в anchor cell {currentHoverCell}.");

        ClearSelection();
    }

    // =========================================
    // УДАЛЕНИЕ / ОТМЕНА
    // =========================================

    private void OnCancelPerformed(InputAction.CallbackContext ctx)
    {
        if (selectedData != null)
        {
            ClearSelection();
            return;
        }

        if (!isHoveringGrid)
            return;

        GridCell cell = grid.GetCell(currentHoverCell.x, currentHoverCell.y);
        if (cell == null || !cell.isOccupied || cell.occupant == null)
            return;

        RuntimeModuleBase moduleToRemove = cell.occupant;

        if (moduleToRemove == hoveredInstalledModule)
            hoveredInstalledModule = null;

        CraftedModule craftedComp = moduleToRemove.GetComponent<CraftedModule>();
        if (craftedComp != null)
        {
            ModuleData dataToReturn = craftedComp.GetData();
            if (dataToReturn != null && moduleStorage != null)
            {
                moduleStorage.AddModule(dataToReturn);
                Debug.Log($"[PepelacGridBuilder] Модуль {dataToReturn.moduleType} возвращён на склад.");
            }
        }

        grid.RemoveModule(moduleToRemove);
        Destroy(moduleToRemove.gameObject);
    }

    // =========================================
    // ХЕЛПЕРЫ СПАВНА
    // =========================================

    private GameObject SpawnRealModulePrefab(ModuleData data)
    {
        if (data == null)
        {
            Debug.LogError("[PepelacGridBuilder] ModuleData is null.");
            return null;
        }

        StandardModuleBase reference = null;

        if (!ModuleTypeRegistry.TryResolveReference(data.moduleType, data.referenceName, out reference) || reference == null)
            reference = ResolveReferenceLegacy(data);

        if (reference == null)
        {
            Debug.LogError($"[PepelacGridBuilder] Не найден эталон для типа '{data.moduleType}' и reference '{data.referenceName}'.");
            return null;
        }

        GameObject instance = Instantiate(reference.gameObject);

        if (!ModuleTypeRegistry.TryRemoveStandardComponent(data.moduleType, instance))
        {
            StandardModuleBase standardBase = instance.GetComponent<StandardModuleBase>();
            if (standardBase != null)
                Destroy(standardBase);
        }

        return instance;
    }

    private StandardModuleBase ResolveReferenceLegacy(ModuleData data)
    {
        if (data == null)
            return null;

        if (data.moduleType == StandardGenerator.TYPE_GENERATOR)
            return generatorDb != null ? generatorDb.GetByName(data.referenceName) : null;

        if (data.moduleType == StandardEnergyStorage.TYPE_ENERGY_STORAGE)
            return energyStorageDb != null ? energyStorageDb.GetByName(data.referenceName) : null;

        if (data.moduleType == StandardFuelTank.TYPE_FUELTANK)
            return fuelTankDb != null ? fuelTankDb.GetByName(data.referenceName) : null;

        return null;
    }

    private RuntimeModuleBase AddRuntimeComponent(GameObject obj, ModuleData data)
    {
        if (obj == null || data == null)
            return null;

        if (ModuleTypeRegistry.TryAddRuntimeComponent(data.moduleType, obj, out RuntimeModuleBase runtimeModule))
            return runtimeModule;

        if (data.moduleType == StandardGenerator.TYPE_GENERATOR)
            return obj.AddComponent<RuntimeGenerator>();

        if (data.moduleType == StandardEnergyStorage.TYPE_ENERGY_STORAGE)
            return obj.AddComponent<RuntimeEnergyStorage>();

        if (data.moduleType == StandardFuelTank.TYPE_FUELTANK)
            return obj.AddComponent<RuntimeFuelTank>();

        Debug.LogError($"[PepelacGridBuilder] Неизвестный тип модуля: '{data.moduleType}'. RuntimeModuleBase не добавлен!");
        return null;
    }
    private float GetFinalVisualYaw(ModuleData data)
    {
        float orientationYaw = 0f;

        switch (currentOrientation)
        {
            case ModuleOrientation.Deg90: orientationYaw = 90f; break;
            case ModuleOrientation.Deg180: orientationYaw = 180f; break;
            case ModuleOrientation.Deg270: orientationYaw = 270f; break;
        }

        float visualOffset = data != null ? data.buildVisualYawOffset : 0f;
        return orientationYaw + visualOffset;
    }

}