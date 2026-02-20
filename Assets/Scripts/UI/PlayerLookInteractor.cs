using UnityEngine;
using UnityEngine.InputSystem;

[DisallowMultipleComponent]
public class PlayerLookInteractor : MonoBehaviour
{
    [Header("Raycast")]
    public Camera mainCamera;
    public float maxDistance = 4.0f;
    public float aimRadius = 0.12f;
    public float originForwardOffset = 0.06f;
    public float fallbackForward = 0.6f;
    public LayerMask ignoreLayer = 0;
    public LayerMask layerMask = ~0;

    [Header("Фильтры игнорирования")]
    public string[] ignoreComponentTypeNames = new string[] { "PlayerController", "CharacterController" };
    public string[] ignoreTags = new string[0];

    [Header("Input")]
    public InputActionReference interactAction;

    [Header("Ссылки")]
    public PlayerVehicleController playerVehicleController;

    [Header("Отладка")]
    public bool debugRay = false;

    private System.Type[] cachedIgnoreTypes;
    private const int MaxHits = 16;
    private readonly RaycastHit[] hitBuffer = new RaycastHit[MaxHits];

    IInteractable currentTarget;
    IInteractable lastTarget;

    void Awake()
    {
        if (mainCamera == null) mainCamera = Camera.main;

        if (ignoreLayer.value != 0)
            layerMask &= ~ignoreLayer;

        if (playerVehicleController == null)
            playerVehicleController = PlayerLocator.VehicleController;

        CacheIgnoreTypes();
    }

    private void CacheIgnoreTypes()
    {
        if (ignoreComponentTypeNames == null || ignoreComponentTypeNames.Length == 0)
        {
            cachedIgnoreTypes = System.Array.Empty<System.Type>();
            return;
        }

        var typeList = new System.Collections.Generic.List<System.Type>();

        foreach (var typeName in ignoreComponentTypeNames)
        {
            if (string.IsNullOrEmpty(typeName)) continue;

            System.Type type = System.Type.GetType(typeName);

            if (type == null)
            {
                foreach (var assembly in System.AppDomain.CurrentDomain.GetAssemblies())
                {
                    type = assembly.GetType(typeName);
                    if (type != null) break;

                    type = assembly.GetType("UnityEngine." + typeName);
                    if (type != null) break;
                }
            }

            if (type != null)
                typeList.Add(type);
            else
                Debug.LogWarning($"[PlayerLookInteractor] Тип '{typeName}' не найден.");
        }

        cachedIgnoreTypes = typeList.ToArray();
    }

    void OnEnable()
    {
        InputActionHelper.Subscribe(interactAction, OnInteractPerformed);
    }

    void OnDisable()
    {
        InputActionHelper.Unsubscribe(interactAction, OnInteractPerformed);
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

        bool found = TryFindInteractable(origin, dir, maxDistance, aimRadius, out IInteractable foundInteractable);

        if (!found && fallbackForward > 0f)
        {
            Vector3 fallbackOrigin = origin + dir * fallbackForward;
            if (debugRay) Debug.DrawRay(fallbackOrigin, dir * (maxDistance - fallbackForward), Color.yellow);
            found = TryFindInteractable(fallbackOrigin, dir, maxDistance - fallbackForward, 0f, out foundInteractable);
        }

        currentTarget = found ? foundInteractable : null;

        if (lastTarget != currentTarget)
        {
            if (lastTarget != null)
            {
                lastTarget.OnHoverExit();
                UIServices.Get<CrosshairUI>()?.SetHover(false);
                UIServices.Get<InteractionHintUI>()?.HideImmediate();
            }

            if (currentTarget != null)
            {
                currentTarget.OnHoverEnter();
                UIServices.Get<CrosshairUI>()?.SetHover(true);

                var baseComp = (currentTarget as MonoBehaviour)?.GetComponent<InteractableBase>();
                string key = baseComp != null && !string.IsNullOrEmpty(baseComp.keyLabel) ? baseComp.keyLabel : "F";
                string hint = baseComp != null && !string.IsNullOrEmpty(baseComp.hintText) ? baseComp.hintText : "Взаимодействовать";

                UIServices.Get<InteractionHintUI>()?.SetVisible(true, key, hint);
            }
        }
    }

    bool TryFindInteractable(Vector3 origin, Vector3 dir, float distance, float radius, out IInteractable result)
    {
        result = null;

        int hitCount;
        if (radius > 0f)
            hitCount = Physics.SphereCastNonAlloc(origin, radius, dir, hitBuffer, distance, layerMask, QueryTriggerInteraction.Ignore);
        else
            hitCount = Physics.RaycastNonAlloc(origin, dir, hitBuffer, distance, layerMask, QueryTriggerInteraction.Ignore);

        if (hitCount == 0) return false;

        System.Array.Sort(hitBuffer, 0, hitCount, HitDistanceComparer.Instance);

        for (int i = 0; i < hitCount; i++)
        {
            var h = hitBuffer[i];
            if (h.collider == null) continue;

            if (ignoreLayer.value != 0 && ((1 << h.collider.gameObject.layer) & ignoreLayer) != 0)
                continue;

            if (ShouldIgnoreByTag(h.collider))
                continue;

            if (ShouldIgnoreByType(h.collider))
                continue;

            var interactableBase = h.collider.GetComponentInParent<InteractableBase>();

            IInteractable found = null;
            if (interactableBase != null)
            {
                found = interactableBase;
            }
            else
            {
                var mb = h.collider.GetComponentInParent<MonoBehaviour>();
                if (mb is IInteractable directInteractable)
                    found = directInteractable;
            }

            if (found == null) continue;

            if (ShouldIgnoreInVehicle(found))
                continue;

            result = found;
            return true;
        }

        return false;
    }

    private bool ShouldIgnoreByTag(Collider col)
    {
        if (ignoreTags == null || ignoreTags.Length == 0) return false;

        foreach (var t in ignoreTags)
        {
            if (!string.IsNullOrEmpty(t) && col.CompareTag(t))
                return true;
        }
        return false;
    }

    private bool ShouldIgnoreByType(Collider col)
    {
        if (cachedIgnoreTypes == null || cachedIgnoreTypes.Length == 0) return false;

        foreach (var type in cachedIgnoreTypes)
        {
            if (col.GetComponentInParent(type) != null)
                return true;
        }
        return false;
    }

    private bool ShouldIgnoreInVehicle(IInteractable found)
    {
        if (playerVehicleController == null || !playerVehicleController.IsInVehicle)
            return false;

        var mb = found as MonoBehaviour;
        if (mb == null) return false;

        if (mb.GetComponent<VehicleSeatInteractable>() != null)
            return true;

        var host = mb.GetComponent<InteractableActionHost>();
        if (host != null && host.ignoreWhileInVehicle)
            return true;

        return false;
    }

    void OnInteractPerformed(InputAction.CallbackContext ctx)
    {
        if (!ctx.performed) return;

        if (currentTarget != null)
        {
            currentTarget.Interact();
            UIServices.Get<CrosshairUI>()?.DoClickPulse();
        }
    }

    private class HitDistanceComparer : System.Collections.Generic.IComparer<RaycastHit>
    {
        public static readonly HitDistanceComparer Instance = new HitDistanceComparer();

        public int Compare(RaycastHit a, RaycastHit b)
        {
            return a.distance.CompareTo(b.distance);
        }
    }
}