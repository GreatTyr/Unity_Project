using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// PlayerLookInteractor (with radius + fallback)
/// - Луч/сфера от камеры для поиска интерактивных объектов.
/// - Поддержка SphereCastAll (aimRadius) и fallback-луча вперёд.
/// - Игнорирует игрока по слою/компонентам/тегам.
/// - Дополнение: если игрок сидит в транспорте (через PlayerVehicleController),
///   штурвал (VehicleSeatInteractable) и InteractableActionHost c ignoreWhileInVehicle
///   не считаются целями наведения (не дают подсказку и подсветку).
/// </summary>
[DisallowMultipleComponent]
public class PlayerLookInteractor : MonoBehaviour
{
    [Header("Raycast")]
    public Camera mainCamera;
    public float maxDistance = 4.0f;

    [Tooltip("Если > 0 — выполняется SphereCastAll с данным радиусом (прощение прицела).")]
    public float aimRadius = 0.12f;

    [Tooltip("Смещение origin вперёд от камеры, чтобы луч начинался не в самой камере.")]
    public float originForwardOffset = 0.06f;

    [Tooltip("Если primary не сработал — делаем fallback-луч с origin, смещённым вперёд на это расстояние (м).")]
    public float fallbackForward = 0.6f;

    [Tooltip("Слой игрока, который нужно игнорировать (оставь 0, если не используешь).")]
    public LayerMask ignoreLayer = 0;

    [Tooltip("Слой маска для поиска интерактивных объектов (по умолчанию все).")]
    public LayerMask layerMask = ~0;

    [Header("Ignore")]
    [Tooltip("Имена типов компонентов, которые следует игнорировать при хиттесте (например, PlayerController, CharacterController).")]
    public string[] ignoreComponentTypeNames = new string[] { "PlayerController", "CharacterController" };

    [Tooltip("Теги, которые нужно игнорировать.")]
    public string[] ignoreTags = new string[0];

    [Header("Input")]
    [Tooltip("Действие для активации интерактивного объекта (например, F).")]
    public InputActionReference interactAction;

    [Header("Player / Vehicle")]
    [Tooltip("Опционально: ссылка на PlayerVehicleController, чтобы знать, сидит ли игрок в транспорте.")]
    public PlayerVehicleController playerVehicleController;

    [Header("Debug")]
    public bool debugRay = false;

    Interactable currentTarget;
    Interactable lastTarget;

    void Awake()
    {
        if (mainCamera == null) mainCamera = Camera.main;

        // Если указан ignoreLayer, исключаем его из layerMask на время кастов
        if (ignoreLayer.value != 0)
        {
            layerMask &= ~ignoreLayer;
        }

        // Пытаемся найти PlayerVehicleController, если не задан в инспекторе
        if (playerVehicleController == null)
        {
            var go = GameObject.FindWithTag("Player");
            if (go != null)
                playerVehicleController = go.GetComponent<PlayerVehicleController>();

            if (playerVehicleController == null)
                playerVehicleController = FindObjectOfType<PlayerVehicleController>();
        }
    }

    void OnEnable()
    {
        if (interactAction != null && interactAction.action != null)
        {
            interactAction.action.performed += OnInteractPerformed;
            interactAction.action.Enable();
        }
    }

    void OnDisable()
    {
        if (interactAction != null && interactAction.action != null)
        {
            interactAction.action.performed -= OnInteractPerformed;
            interactAction.action.Disable();
        }
    }

    void Update()
    {
        UpdateLookTarget();
    }

    /// <summary>
    /// Обновление текущей цели, на которую смотрит игрок.
    /// </summary>
    void UpdateLookTarget()
    {
        lastTarget = currentTarget;
        currentTarget = null;

        if (mainCamera == null) return;

        Vector3 origin = mainCamera.transform.position + mainCamera.transform.forward * originForwardOffset;
        Vector3 dir = mainCamera.transform.forward;

        if (debugRay) Debug.DrawRay(origin, dir * maxDistance, Color.green);

        // Основной проход: sphere или ray
        bool found = TryFindInteractable(origin, dir, maxDistance, aimRadius, out Interactable foundInteractable);

        // Fallback: если не найдено и fallbackForward > 0 — пробуем ещё раз с origin, смещённым вперёд
        if (!found && fallbackForward > 0f)
        {
            Vector3 fallbackOrigin = origin + dir * fallbackForward;
            if (debugRay) Debug.DrawRay(fallbackOrigin, dir * (maxDistance - fallbackForward), Color.yellow);
            found = TryFindInteractable(fallbackOrigin, dir, maxDistance - fallbackForward, 0f, out foundInteractable);
        }

        if (found)
            currentTarget = foundInteractable;
        else
            currentTarget = null;

        // Обработка смены цели (enter/exit)
        if (lastTarget != currentTarget)
        {
            if (lastTarget != null)
            {
                lastTarget.OnHoverExit();
                CrosshairUI.Instance?.SetHover(false);
                InteractionHintUI.Instance?.HideImmediate();
            }

            if (currentTarget != null)
            {
                currentTarget.OnHoverEnter();
                CrosshairUI.Instance?.SetHover(true);

                var baseComp = (currentTarget as MonoBehaviour)?.GetComponent<InteractableBase>();
                string key = baseComp != null ? baseComp.keyLabel : "F";
                string hint = baseComp != null ? baseComp.hintText : "Взаимодействие";

                InteractionHintUI.Instance?.SetVisible(true, $"[{key}] {hint}");
            }
        }
    }

    /// <summary>
    /// Пытается найти первый подходящий Interactable по лучу/сфере.
    /// ВАЖНО: учитывает состояние PlayerVehicleController.IsInVehicle и игнорирует штурвал
    /// (VehicleSeatInteractable) и InteractableActionHost с ignoreWhileInVehicle == true
    /// во время пилотирования.
    /// </summary>
    bool TryFindInteractable(Vector3 origin, Vector3 dir, float distance, float radius, out Interactable result)
    {
        result = null;
        RaycastHit[] hits;
        if (radius > 0f)
            hits = Physics.SphereCastAll(origin, radius, dir, distance, layerMask, QueryTriggerInteraction.Ignore);
        else
            hits = Physics.RaycastAll(origin, dir, distance, layerMask, QueryTriggerInteraction.Ignore);

        if (hits == null || hits.Length == 0) return false;

        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        foreach (var h in hits)
        {
            if (h.collider == null) continue;

            // Пропуск по игнорируемому слою
            if (ignoreLayer.value != 0 && ((1 << h.collider.gameObject.layer) & ignoreLayer) != 0)
                continue;

            // Пропуск по тегам
            bool skip = false;
            if (ignoreTags != null && ignoreTags.Length > 0)
            {
                foreach (var t in ignoreTags)
                {
                    if (!string.IsNullOrEmpty(t) && h.collider.CompareTag(t))
                    {
                        skip = true;
                        break;
                    }
                }
            }
            if (skip) continue;

            // Пропуск по типам компонентов
            if (ignoreComponentTypeNames != null && ignoreComponentTypeNames.Length > 0)
            {
                foreach (var typeName in ignoreComponentTypeNames)
                {
                    if (string.IsNullOrEmpty(typeName)) continue;

                    var type = System.Type.GetType(typeName);
                    if (type != null)
                    {
                        var comp = h.collider.GetComponentInParent(type);
                        if (comp != null) { skip = true; break; }
                    }
                    else
                    {
                        var mb = h.collider.GetComponentInParent<MonoBehaviour>();
                        if (mb != null && mb.GetType().Name == typeName) { skip = true; break; }
                    }
                }
            }
            if (skip) continue;

            // Ищем Interactable
            Interactable found = null;
            var mbCandidate = h.collider.GetComponentInParent<MonoBehaviour>();
            if (mbCandidate is Interactable) found = mbCandidate as Interactable;
            else
            {
                var ib = h.collider.GetComponentInParent<InteractableBase>();
                if (ib != null) found = ib as Interactable;
            }

            if (found != null)
            {
                // --- Дополнительный фильтр для режима пилотирования ---
                if (playerVehicleController != null && playerVehicleController.IsInVehicle)
                {
                    var mb = (found as MonoBehaviour);
                    if (mb != null)
                    {
                        // Если это штурвал (VehicleSeatInteractable) — игнорируем его, пока игрок в транспорте
                        var seat = mb.GetComponent<VehicleSeatInteractable>();
                        if (seat != null)
                        {
                            // Пропускаем этот хит и ищем следующего кандидата
                            continue;
                        }

                        // Если это InteractableActionHost и включен ignoreWhileInVehicle — тоже игнорируем
                        var host = mb.GetComponent<InteractableActionHost>();
                        if (host != null && host.ignoreWhileInVehicle)
                        {
                            continue;
                        }
                    }
                }

                // Если не отфильтровано — принимаем как текущую цель
                result = found;
                return true;
            }
        }

        return false;
    }

    void OnInteractPerformed(InputAction.CallbackContext ctx)
    {
        if (!ctx.performed) return;

        if (currentTarget != null)
        {
            currentTarget.Interact();
            CrosshairUI.Instance?.DoClickPulse();
        }
    }
}