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

    [Header("Debug / Placement Diagnostics")]
    [SerializeField] private bool showPlacementDiagnostics = true;

    [SerializeField, HideInInspector] private bool hasPlacementQuery;
    [SerializeField, HideInInspector] private bool lastPlacementQueryValid;
    [SerializeField, HideInInspector] private PlacementBlockReason lastPlacementBlockReason = PlacementBlockReason.Unknown;
    [SerializeField, HideInInspector] private Vector2Int lastPlacementBlockedCell = new Vector2Int(-1, -1);
    [SerializeField, HideInInspector] private int lastPlacementExpectedRegionId = -1;
    [SerializeField, HideInInspector] private int lastPlacementBlockedRegionId = -1;
    [SerializeField, HideInInspector] private Vector2Int lastPlacementAnchorCell = new Vector2Int(-1, -1);

    [Header("Region Debug")]
    [SerializeField] private bool showRegionDebugOverlay = true;
    [SerializeField] private Color hoveredRegionDebugColor = new Color(0.15f, 0.75f, 1f, 0.45f);

    [SerializeField, HideInInspector] private int hoveredBuildableRegionId = -1;

    private PlacementQueryResult lastPlacementQuery;
    private PlacementBlockReason lastLoggedBlockReason = PlacementBlockReason.Unknown;
    private Vector2Int lastLoggedBlockedCell = new Vector2Int(-999, -999);

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

        hoveredBuildableRegionId = -1;
        gridOverlay?.HideRegionDebug();
        gridOverlay?.HideAnchorDebug();
        gridOverlay?.HideBlockedCellDebug();

        ClearPlacementDiagnostics();
    }

    // =========================================
    // UI / SELECTION
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
        {
            gridOverlay.HideFootprint();
            gridOverlay.HideAnchorDebug();
            gridOverlay.HideBlockedCellDebug();
        }

        ClearPlacementDiagnostics();
    }

    // =========================================
    // DIAGNOSTICS
    // =========================================

    private void ClearPlacementDiagnostics()
    {
        hasPlacementQuery = false;
        lastPlacementQuery = null;
        lastPlacementQueryValid = false;
        lastPlacementBlockReason = PlacementBlockReason.Unknown;
        lastPlacementBlockedCell = new Vector2Int(-1, -1);
        lastPlacementExpectedRegionId = -1;
        lastPlacementBlockedRegionId = -1;
        lastPlacementAnchorCell = new Vector2Int(-1, -1);

        lastLoggedBlockReason = PlacementBlockReason.Unknown;
        lastLoggedBlockedCell = new Vector2Int(-999, -999);
    }

    private void ApplyPlacementDiagnostics(PlacementQueryResult query, Vector2Int anchorCell)
    {
        lastPlacementQuery = query;
        hasPlacementQuery = query != null;
        lastPlacementAnchorCell = anchorCell;

        if (query == null)
        {
            lastPlacementQueryValid = false;
            lastPlacementBlockReason = PlacementBlockReason.Unknown;
            lastPlacementBlockedCell = new Vector2Int(-1, -1);
            lastPlacementExpectedRegionId = -1;
            lastPlacementBlockedRegionId = -1;
            return;
        }

        lastPlacementQueryValid = query.isValid;
        lastPlacementBlockReason = query.blockReason;
        lastPlacementBlockedCell = query.firstBlockedCell;
        lastPlacementExpectedRegionId = query.expectedRegionId;
        lastPlacementBlockedRegionId = query.blockedRegionId;
    }

    private void LogPlacementDiagnosticsIfNeeded()
    {
        if (!showPlacementDiagnostics || !hasPlacementQuery || lastPlacementQuery == null)
            return;

        if (lastPlacementQuery.isValid)
            return;

        bool sameReason = lastLoggedBlockReason == lastPlacementBlockReason;
        bool sameCell = lastLoggedBlockedCell == lastPlacementBlockedCell;

        if (sameReason && sameCell)
            return;

        lastLoggedBlockReason = lastPlacementBlockReason;
        lastLoggedBlockedCell = lastPlacementBlockedCell;

        string extra = string.Empty;
        if (lastPlacementBlockReason == PlacementBlockReason.RegionMismatch)
        {
            extra = $" expectedRegion={lastPlacementExpectedRegionId}, blockedRegion={lastPlacementBlockedRegionId}";
        }

        Debug.Log(
            $"[PepelacGridBuilder] Placement invalid: {lastPlacementBlockReason}, " +
            $"anchor={lastPlacementAnchorCell}, blockedCell={lastPlacementBlockedCell}{extra}");
    }

    public string GetPlacementDiagnosticsText()
    {
        if (!hasPlacementQuery)
            return "No placement query.";

        if (lastPlacementQueryValid)
            return $"VALID | anchor={lastPlacementAnchorCell}";

        string text =
            $"INVALID | reason={lastPlacementBlockReason} | anchor={lastPlacementAnchorCell} | blocked={lastPlacementBlockedCell}";

        if (lastPlacementBlockReason == PlacementBlockReason.RegionMismatch)
            text += $" | expectedRegion={lastPlacementExpectedRegionId} | blockedRegion={lastPlacementBlockedRegionId}";

        return text;
    }

    [ContextMenu("Debug Print Placement Diagnostics")]
    private void DebugPrintPlacementDiagnostics()
    {
        Debug.Log($"[PepelacGridBuilder] {GetPlacementDiagnosticsText()}");
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

        EnsureReferenceVisualScale(data, ghostObject.transform);

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

    private Vector3 GetReferenceVisualScale(ModuleData data, Transform currentTransform)
    {
        if (data != null && data.referenceVisualScale != Vector3.zero)
            return data.referenceVisualScale;

        if (currentTransform != null && currentTransform.localScale != Vector3.zero)
            return currentTransform.localScale;

        return Vector3.one;
    }

    private void EnsureReferenceVisualScale(ModuleData data, Transform target)
    {
        if (data == null || target == null)
            return;

        if (data.referenceVisualScale == Vector3.zero)
            data.referenceVisualScale = target.localScale;
    }

    private float GetFinalVisualYaw(ModuleData data, ModuleOrientation orientation)
    {
        float orientationYaw = 0f;

        switch (orientation)
        {
            case ModuleOrientation.Deg90: orientationYaw = 90f; break;
            case ModuleOrientation.Deg180: orientationYaw = 180f; break;
            case ModuleOrientation.Deg270: orientationYaw = 270f; break;
        }

        float visualOffset = data != null ? data.buildVisualYawOffset : 0f;
        return orientationYaw + visualOffset;
    }

    private void ApplyModuleVisualTransform(
        Transform target,
        ModuleData data,
        ModuleOrientation orientation,
        Vector3 localPlacementPoint)
    {
        if (target == null || data == null)
            return;

        float scaleFactor = Mathf.Max(0.001f, data.scaleFactor);
        Vector3 referenceVisualScale = GetReferenceVisualScale(data, target);

        float finalYaw = GetFinalVisualYaw(data, orientation);
        Quaternion visualRotation = Quaternion.Euler(0f, finalYaw, 0f);

        target.localScale = referenceVisualScale * scaleFactor;
        target.localRotation = visualRotation;

        Vector3 automaticFootprintOffset = Vector3.zero;
        if (grid != null)
        {
            automaticFootprintOffset = grid.GetAnchorToFootprintCenterOffset(
                data.length,
                data.width,
                orientation,
                data.buildAnchorCellLocal
            );
        }

        // Ручной offset — только для тонкой визуальной подстройки
        Vector3 manualVisualOffset = visualRotation * data.buildAnchorLocal;

        target.localPosition = localPlacementPoint + automaticFootprintOffset + manualVisualOffset;
    }

    private void UpdateGhostTransformOnly()
    {
        if (ghostObject == null || selectedData == null)
            return;

        float scaleFactor = Mathf.Max(0.001f, selectedData.scaleFactor);
        Vector3 referenceVisualScale = GetReferenceVisualScale(selectedData, ghostObject.transform);

        float finalYaw = GetFinalVisualYaw(selectedData, currentOrientation);
        Quaternion visualRotation = Quaternion.Euler(0f, finalYaw, 0f);

        ghostObject.transform.localScale = referenceVisualScale * scaleFactor;
        ghostObject.transform.localRotation = visualRotation;

        Vector3 manualVisualOffset = visualRotation * selectedData.buildAnchorLocal;
        ghostObject.transform.localPosition = manualVisualOffset;
    }

    private void UpdateGhostPlacementVisual()
    {
        if (ghostObject == null || selectedData == null || !isHoveringGrid)
            return;

        Vector3 localPlacementPoint = GetPlacementPoint(currentHoverCell);

        ApplyModuleVisualTransform(ghostObject.transform, selectedData, currentOrientation, localPlacementPoint);

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

    // =========================================
    // INPUT / UPDATE
    // =========================================

    private void OnRotatePerformed(InputAction.CallbackContext ctx)
    {
        if (selectedData == null || ghostObject == null)
            return;

        currentOrientation = (ModuleOrientation)(((int)currentOrientation + 1) % 4);
        UpdateGhostTransformOnly();

        if (isHoveringGrid)
        {
            PlacementQueryResult query = grid.QueryPlacement(
                currentHoverCell,
                selectedData.length,
                selectedData.width,
                currentOrientation,
                selectedData.buildAnchorCellLocal
            );

            ApplyPlacementDiagnostics(query, currentHoverCell);

            isPlacementValid = query != null && query.isValid;

            UpdateGhostPlacementVisual();
            UpdateFootprintOverlay(query);
            LogPlacementDiagnosticsIfNeeded();
        }
    }

    private void Update()
    {
        ResolveReferences();

        if (grid == null || buildSurface == null)
            return;

        UpdateHoveredCell();
        UpdateRegionDebugOverlay();

        if (selectedData != null && ghostObject != null)
            UpdatePlacementMode();
        else
            UpdateRemovalHoverMode();

        UpdatePlacementDebugOverlay();
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

    // =========================================
    // PLACEMENT MODE
    // =========================================

    private void UpdatePlacementMode()
    {
        if (!isHoveringGrid)
        {
            if (ghostObject != null)
                ghostObject.SetActive(false);

            isPlacementValid = false;

            if (gridOverlay != null)
                gridOverlay.HideFootprint();

            ClearPlacementDiagnostics();
            return;
        }

        if (ghostObject != null)
            ghostObject.SetActive(true);

        PlacementQueryResult query = grid.QueryPlacement(
            currentHoverCell,
            selectedData.length,
            selectedData.width,
            currentOrientation,
            selectedData.buildAnchorCellLocal
        );

        ApplyPlacementDiagnostics(query, currentHoverCell);

        isPlacementValid = query != null && query.isValid;

        UpdateGhostPlacementVisual();
        UpdateFootprintOverlay(query);
        LogPlacementDiagnosticsIfNeeded();
    }

    private void UpdateFootprintOverlay(PlacementQueryResult query)
    {
        if (gridOverlay == null)
            return;

        if (query == null)
        {
            gridOverlay.HideFootprint();
            return;
        }

        List<Vector2Int> cellsToShow = query.isValid
            ? query.validatedFootprint
            : query.rawFootprint;

        if (cellsToShow != null && cellsToShow.Count > 0)
        {
            gridOverlay.ShowFootprint(
                cellsToShow,
                query.isValid ? validFootprintColor : invalidFootprintColor
            );
        }
        else
        {
            gridOverlay.HideFootprint();
        }
    }

    // =========================================
    // REMOVE MODE
    // =========================================

    private void UpdateRemovalHoverMode()
    {
        if (gridOverlay != null)
        {
            gridOverlay.HideFootprint();
            gridOverlay.HideAnchorDebug();
            gridOverlay.HideBlockedCellDebug();
        }

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
    // REGION / DEBUG OVERLAYS
    // =========================================

    private void UpdateRegionDebugOverlay()
    {
        hoveredBuildableRegionId = -1;

        if (!showRegionDebugOverlay || grid == null || gridOverlay == null || !isHoveringGrid)
        {
            gridOverlay?.HideRegionDebug();
            return;
        }

        if (!grid.TryGetBuildableRegionId(currentHoverCell, out int regionId))
        {
            gridOverlay.HideRegionDebug();
            return;
        }

        hoveredBuildableRegionId = regionId;

        List<Vector2Int> regionCells = grid.GetCellsInBuildableRegion(regionId);
        if (regionCells == null || regionCells.Count == 0)
        {
            gridOverlay.HideRegionDebug();
            return;
        }

        gridOverlay.ShowRegionDebug(regionCells, hoveredRegionDebugColor);
    }

    private void UpdatePlacementDebugOverlay()
    {
        if (gridOverlay == null)
            return;

        bool inPlacementMode = selectedData != null && ghostObject != null;

        if (!inPlacementMode || !isHoveringGrid)
        {
            gridOverlay.HideAnchorDebug();
            gridOverlay.HideBlockedCellDebug();
            return;
        }

        gridOverlay.ShowAnchorDebug(currentHoverCell);

        if (hasPlacementQuery &&
            !lastPlacementQueryValid &&
            lastPlacementBlockedCell.x >= 0 &&
            lastPlacementBlockedCell.y >= 0)
        {
            gridOverlay.ShowBlockedCellDebug(lastPlacementBlockedCell);
        }
        else
        {
            gridOverlay.HideBlockedCellDebug();
        }
    }

    // =========================================
    // PLACE / REMOVE
    // =========================================

    private void OnClickPerformed(InputAction.CallbackContext ctx)
    {
        if (selectedData == null || !isHoveringGrid || !isPlacementValid)
            return;

        bool requiresStorage = !string.IsNullOrEmpty(selectedCode);

        if (requiresStorage)
        {
            if (moduleStorage == null)
            {
                Debug.LogWarning("[PepelacGridBuilder] Невозможно установить модуль: ModuleStorage не назначен.");
                return;
            }

            if (!moduleStorage.HasModule(selectedCode, 1))
            {
                Debug.LogWarning($"[PepelacGridBuilder] Модуль '{selectedCode}' больше не доступен на складе.");
                ClearSelection();
                return;
            }
        }

        GameObject newModuleObj = SpawnRealModulePrefab(selectedData);
        if (newModuleObj == null)
        {
            Debug.LogError("[PepelacGridBuilder] Не удалось найти префаб для спавна!");
            return;
        }

        EnsureReferenceVisualScale(selectedData, newModuleObj.transform);

        CraftedModule craftedComp = newModuleObj.AddComponent<CraftedModule>();
        craftedComp.SetData(selectedData);

        RuntimeModuleBase runtimeMod = AddRuntimeComponent(newModuleObj, selectedData);
        // Runtime больше не обязателен, не прерываем если null

        if (runtimeMod != null)
            runtimeMod.Orientation = currentOrientation;

        if (selectedData.isVolatile)
        {
            RuntimeVolatileModule volatileModule = newModuleObj.AddComponent<RuntimeVolatileModule>();
            volatileModule.Initialize(
                selectedData.explosionRadiusMeters,
                selectedData.explosionPenetration,
                selectedData.explosionDamage,
                selectedData.explosionDamageType,
                selectedData.totalMassKg,
                selectedData.moduleTier,
                selectedData.effectiveVolume
            );
        }

        bool success = grid.TryPlaceModule(
            runtimeMod,
            currentHoverCell,
            selectedData.length,
            selectedData.width,
            selectedData.buildAnchorCellLocal
        );

        if (!success)
        {
            Destroy(newModuleObj);
            return;
        }

        newModuleObj.transform.SetParent(transform, false);

        Vector3 localPlacementPoint = GetPlacementPoint(currentHoverCell);
        ApplyModuleVisualTransform(newModuleObj.transform, selectedData, currentOrientation, localPlacementPoint);

        if (requiresStorage)
        {
            bool removedFromStorage = moduleStorage.RemoveModule(selectedCode, 1);
            if (!removedFromStorage)
            {
                Debug.LogError($"[PepelacGridBuilder] Не удалось списать модуль '{selectedCode}' со склада. Placement откатывается.");

                grid.RemoveModule(runtimeMod);
                Destroy(newModuleObj);
                return;
            }
        }

        Debug.Log($"[PepelacGridBuilder] Установлен модуль {selectedData.moduleType} в anchor cell {currentHoverCell}.");

        ClearSelection();
    }

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

        CraftedModule craftedComp = moduleToRemove.GetComponent<CraftedModule>();
        if (craftedComp == null)
        {
            Debug.LogWarning("[PepelacGridBuilder] Нельзя снять модуль: отсутствует CraftedModule.");
            return;
        }

        ModuleData dataToReturn = craftedComp.GetData();
        if (dataToReturn == null)
        {
            Debug.LogWarning("[PepelacGridBuilder] Нельзя снять модуль: не удалось прочитать ModuleData.");
            return;
        }

        if (moduleStorage == null)
        {
            Debug.LogWarning("[PepelacGridBuilder] Нельзя снять модуль: ModuleStorage не назначен.");
            return;
        }

        string returnedCode = moduleStorage.AddModule(dataToReturn);
        if (string.IsNullOrEmpty(returnedCode))
        {
            Debug.LogError("[PepelacGridBuilder] Не удалось вернуть модуль на склад. Снятие отменено.");
            return;
        }

        if (moduleToRemove == hoveredInstalledModule)
            hoveredInstalledModule = null;

        grid.RemoveModule(moduleToRemove);
        Destroy(moduleToRemove.gameObject);

        Debug.Log($"[PepelacGridBuilder] Модуль {dataToReturn.moduleType} возвращён на склад.");
    }

    // =========================================
    // HELPERS
    // =========================================

    private Vector3 GetPlacementPoint(Vector2Int anchorCell)
    {
        if (grid == null)
            return Vector3.zero;

        return grid.AnchorCellToLocalCenter(anchorCell.x, anchorCell.y);
    }

    private GameObject SpawnRealModulePrefab(ModuleData data)
    {
        if (data == null)
        {
            Debug.LogError("[PepelacGridBuilder] ModuleData is null.");
            return null;
        }

        if (!ModuleTypeRegistry.TryResolveReference(data.moduleType, data.referenceName, out StandardModuleBase reference) ||
            reference == null)
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

    private RuntimeModuleBase AddRuntimeComponent(GameObject obj, ModuleData data)
    {
        if (obj == null || data == null)
            return null;

        if (ModuleTypeRegistry.TryAddRuntimeComponent(data.moduleType, obj, out RuntimeModuleBase runtimeModule))
            return runtimeModule;

        // Runtime больше не обязателен
        Debug.LogWarning($"[PepelacGridBuilder] Runtime-компонент не добавлен для '{data.moduleType}'. Продолжаем без него.");
        return null;
    }
}