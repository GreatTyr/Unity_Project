using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// PlayerLookInteractor (with radius + fallback)
/// - Первичный проход: SphereCastAll (если aimRadius>0) или RaycastAll.
/// - Если не найден интерактивный объект, выполняется fallback: RaycastAll начиная из origin + forward * fallbackForward (короче максимальной дальности),
///   что помогает обнаружить цель «за» игроком, если он небольшая помеха.
/// - Пропускает коллайдеры игрока (по слою Player или по компонентам), сортирует хиты по distance и выбирает первый Interactable.
/// </summary>
[DisallowMultipleComponent]
public class PlayerLookInteractor : MonoBehaviour
{
    [Header("Raycast")]
    public Camera mainCamera;
    public float maxDistance = 4.0f;
    [Tooltip("Если > 0 — выполняется SphereCastAll с данным радиусом (прощение прицела).")]
    public float aimRadius = 0.12f;
    public float originForwardOffset = 0.06f;
    [Tooltip("Если primary не сработал — делаем fallback-луч с origin смещённым вперёд на этот множитель (в метрах)")]
    public float fallbackForward = 0.6f;

    [Tooltip("Слой игрока, который нужно игнорировать (оставь -1, если не используешь)")]
    public LayerMask ignoreLayer = 0;
    [Tooltip("Слой маска для поиска интерактивных объектов (по умолчанию все)")]
    public LayerMask layerMask = ~0;

    [Header("Ignore")]
    public string[] ignoreComponentTypeNames = new string[] { "PlayerController", "CharacterController" };
    public string[] ignoreTags = new string[0];

    [Header("Input")]
    public InputActionReference interactAction;

    [Header("Debug")]
    public bool debugRay = false;

    Interactable currentTarget;
    Interactable lastTarget;

    void Awake()
    {
        if (mainCamera == null) mainCamera = Camera.main;
        // Если указан ignoreLayer, исключаем его из layerMask на время кастов (опционально)
        if (ignoreLayer.value != 0)
        {
            // layerMask without ignoreLayer
            layerMask &= ~ignoreLayer;
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

    void UpdateLookTarget()
    {
        lastTarget = currentTarget;
        currentTarget = null;

        if (mainCamera == null) return;

        Vector3 origin = mainCamera.transform.position + mainCamera.transform.forward * originForwardOffset;
        Vector3 dir = mainCamera.transform.forward;

        if (debugRay) Debug.DrawRay(origin, dir * maxDistance, Color.green);

        // Primary pass: sphere or ray
        bool found = TryFindInteractable(origin, dir, maxDistance, aimRadius, out Interactable foundInteractable);

        // Fallback: если не найдено и fallbackForward>0 — попробуем вторым проходом origin смещённым вперёд
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

        // Handle enter/exit
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

            // skip if collider is on ignoreLayer
            if (ignoreLayer.value != 0 && ((1 << h.collider.gameObject.layer) & ignoreLayer) != 0)
                continue;

            // skip by tag
            bool skip = false;
            if (ignoreTags != null && ignoreTags.Length > 0)
            {
                foreach (var t in ignoreTags)
                {
                    if (!string.IsNullOrEmpty(t) && h.collider.CompareTag(t))
                    {
                        skip = true; break;
                    }
                }
            }
            if (skip) continue;

            // skip by component type names
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

            // try find Interactable
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
                result = found;
                return true;
            }
        }

        return false;
    }

    void OnInteractPerformed(InputAction.CallbackContext ctx)
    {
        if (currentTarget != null)
        {
            currentTarget.Interact();
            CrosshairUI.Instance?.DoClickPulse();
        }
    }
}