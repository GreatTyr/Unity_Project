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

    [Header("Input (Optional)")]
    public InputActionReference rotateAction; // Назначь на R
    public InputActionReference clickAction;  // Назначь на ЛКМ

    private PepelacGrid grid;

    // Стейт
    private ModuleData selectedData;
    private string selectedCode;
    private ModuleOrientation currentOrientation = ModuleOrientation.Deg0;

    // Ghost объект
    private GameObject ghostObject;
    private MeshRenderer ghostRenderer;

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

    /// <summary>
    /// Вызывается из PepelacBuilderUI, когда игрок кликает на модуль в списке.
    /// </summary>
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
        ghostObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
        ghostObject.name = "GridGhost";

        // Убираем коллизию, чтобы луч мыши не врезался в самого призрака
        Destroy(ghostObject.GetComponent<Collider>());

        ghostRenderer = ghostObject.GetComponent<MeshRenderer>();
        ghostRenderer.material = validGhostMaterial;

        // Привязываем призрака к сетке, чтобы он двигался вместе с Пепелацем
        ghostObject.transform.SetParent(grid.transform, false);

        UpdateGhostScale();
    }

    private void DestroyGhost()
    {
        if (ghostObject != null)
        {
            Destroy(ghostObject);
            ghostObject = null;
            ghostRenderer = null;
        }
    }

    private void UpdateGhostScale()
    {
        if (ghostObject == null || selectedData == null) return;

        float l = selectedData.length;
        float w = selectedData.width;
        float h = selectedData.height;

        // Если повернут, меняем X и Z местами для визуала
        if (currentOrientation == ModuleOrientation.Deg90 || currentOrientation == ModuleOrientation.Deg270)
        {
            ghostObject.transform.localScale = new Vector3(l, h, w);
        }
        else
        {
            ghostObject.transform.localScale = new Vector3(w, h, l);
        }
    }

    private void OnRotatePerformed(InputAction.CallbackContext ctx)
    {
        if (selectedData == null || ghostObject == null) return;

        // Крутим ориентацию: 0 -> 90 -> 180 -> 270 -> 0
        currentOrientation = (ModuleOrientation)(((int)currentOrientation + 1) % 4);
        UpdateGhostScale();
    }

    // =========================================
    // UPDATE (Raycast и Снэппинг)
    // =========================================

    private void Update()
    {
        if (selectedData == null || ghostObject == null || builderCamera == null) return;

        // Пускаем луч от мыши (New Input System)
        Vector2 mousePos = Mouse.current.position.ReadValue();
        Ray ray = builderCamera.ScreenPointToRay(mousePos);

        // Ищем попадание в коллайдер нашей Сетки (GridSurface)
        // Предполагается, что на GridSurface висит MeshCollider
        if (Physics.Raycast(ray, out RaycastHit hit))
        {
            // Проверяем, попали ли мы в саму сетку Пепелаца
            if (hit.collider.transform == grid.transform)
            {
                isHoveringGrid = true;

                // Переводим мировую точку попадания в локальную для сетки
                Vector3 localHitPos = grid.transform.InverseTransformPoint(hit.point);

                // Получаем индексы клетки [X, Z]
                currentHoverCell = grid.LocalToGridPosition(localHitPos);

                // Если мышка вышла за пределы математической сетки
                if (currentHoverCell.x < 0)
                {
                    isHoveringGrid = false;
                    ghostObject.SetActive(false);
                    return;
                }

                ghostObject.SetActive(true);

                // Вычисляем размеры модуля в клетках
                Vector2Int gridSize = grid.CalculateGridSize(selectedData.length, selectedData.width, currentOrientation);

                // Проверяем, можно ли туда поставить
                var footprint = grid.GetPlacementFootprint(currentHoverCell, gridSize);
                isPlacementValid = (footprint != null);

                // Красим призрака
                ghostRenderer.material = isPlacementValid ? validGhostMaterial : invalidGhostMaterial;

                // Привязываем призрака к центру клетки (Снэппинг)
                Vector3 localSnapPos = grid.GridToLocalPosition(currentHoverCell.x, currentHoverCell.y);
                // Поднимаем куб на половину его высоты, чтобы он "стоял" на сетке, а не проваливался в нее
                localSnapPos.y = selectedData.height / 2f;

                ghostObject.transform.localPosition = localSnapPos;
            }
            else
            {
                // Луч попал во что-то другое (землю, здания)
                isHoveringGrid = false;
                ghostObject.SetActive(false);
            }
        }
        else
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

        // 1. Пытаемся занять клетки в математике сетки
        // Создадим "пустой" RuntimeModuleBase-контейнер, пока мы не заспавнили реальный
        GameObject newModuleObj = SpawnRealModulePrefab(selectedData);
        if (newModuleObj == null)
        {
            Debug.LogError("[PepelacGridBuilder] Не удалось найти префаб для спавна!");
            return;
        }

        RuntimeModuleBase runtimeMod = AddRuntimeComponent(newModuleObj, selectedData);
        runtimeMod.Orientation = currentOrientation;

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

        // Поворот визуала префаба
        float yRot = 0f;
        switch (currentOrientation)
        {
            case ModuleOrientation.Deg90: yRot = 90f; break;
            case ModuleOrientation.Deg180: yRot = 180f; break;
            case ModuleOrientation.Deg270: yRot = 270f; break;
        }
        newModuleObj.transform.localRotation = Quaternion.Euler(0f, yRot, 0f);

        // Применяем масштаб из Data
        newModuleObj.transform.localScale = Vector3.one * Mathf.Max(0.001f, selectedData.scaleFactor);

        // 3. Вешаем паспорт (CraftedModule)
        var craftedComp = newModuleObj.AddComponent<CraftedModule>();
        craftedComp.SetData(selectedData);

        // 4. Списываем со склада (ModuleStorage)
        moduleStorage.RemoveModule(selectedCode, 1);

        Debug.Log($"[PepelacGridBuilder] Успешно установлен {selectedData.moduleType}!");

        // Сбрасываем выбор (чтобы не спавнить 100 штук случайно)
        ClearSelection();

        // ВАЖНО: Нужно как-то сказать UI обновить список (так как кол-во уменьшилось)
        // Но мы пока просто обнулим выбор. Игрок кликнет еще раз в UI, если захочет.
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

        // Создаем инстанс эталона
        GameObject instance = Instantiate(reference.gameObject);

        // Сразу удаляем скрипт-эталон, он нам на живом Пепелаце не нужен
        Destroy(instance.GetComponent<StandardModuleBase>());

        return instance;
    }

    private RuntimeModuleBase AddRuntimeComponent(GameObject obj, ModuleData data)
    {
        // В зависимости от типа вешаем нужный Runtime-скрипт (которые мы написали в Шаге 3)
        if (data.moduleType == StandardGenerator.TYPE_GENERATOR)
            return obj.AddComponent<RuntimeGenerator>();

        if (data.moduleType == StandardEnergyStorage.TYPE_ENERGY_STORAGE)
            return obj.AddComponent<RuntimeEnergyStorage>();

        if (data.moduleType == StandardFuelTank.TYPE_FUELTANK)
            return obj.AddComponent<RuntimeFuelTank>();

        // Fallback
        return obj.AddComponent<RuntimeFuelTank>();
    }
}