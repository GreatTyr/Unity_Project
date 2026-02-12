using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// PlayerLookInteractor (with radius + fallback)
/// - ���/����� �� ������ ��� ������ ������������� ��������.
/// - ��������� SphereCastAll (aimRadius) � fallback-���� �����.
/// - ���������� ������ �� ����/�����������/�����.
/// - ����������: ���� ����� ����� � ���������� (����� PlayerVehicleController),
///   ������� (VehicleSeatInteractable) � InteractableActionHost c ignoreWhileInVehicle
///   �� ��������� ������ ��������� (�� ���� ��������� � ���������).
/// </summary>
[DisallowMultipleComponent]
public class PlayerLookInteractor : MonoBehaviour
{
    [Header("Raycast")]
    public Camera mainCamera;
    public float maxDistance = 4.0f;

    [Tooltip("���� > 0 � ����������� SphereCastAll � ������ �������� (�������� �������).")]
    public float aimRadius = 0.12f;

    [Tooltip("�������� origin ����� �� ������, ����� ��� ��������� �� � ����� ������.")]
    public float originForwardOffset = 0.06f;

    [Tooltip("���� primary �� �������� � ������ fallback-��� � origin, ��������� ����� �� ��� ���������� (�).")]
    public float fallbackForward = 0.6f;

    [Tooltip("���� ������, ������� ����� ������������ (������ 0, ���� �� �����������).")]
    public LayerMask ignoreLayer = 0;

    [Tooltip("���� ����� ��� ������ ������������� �������� (�� ��������� ���).")]
    public LayerMask layerMask = ~0;

    [Header("Ignore")]
    [Tooltip("����� ����� �����������, ������� ������� ������������ ��� �������� (��������, PlayerController, CharacterController).")]
    public string[] ignoreComponentTypeNames = new string[] { "PlayerController", "CharacterController" };

    [Tooltip("����, ������� ����� ������������.")]
    public string[] ignoreTags = new string[0];

    [Header("Input")]
    [Tooltip("�������� ��� ��������� �������������� ������� (��������, F).")]
    public InputActionReference interactAction;

    [Header("Player / Vehicle")]
    [Tooltip("�����������: ������ �� PlayerVehicleController, ����� �����, ����� �� ����� � ����������.")]
    public PlayerVehicleController playerVehicleController;

    [Header("Debug")]
    public bool debugRay = false;

    Interactable currentTarget;
    Interactable lastTarget;

    void Awake()
    {
        if (mainCamera == null) mainCamera = Camera.main;

        // ���� ������ ignoreLayer, ��������� ��� �� layerMask �� ����� ������
        if (ignoreLayer.value != 0)
        {
            layerMask &= ~ignoreLayer;
        }

        // �������� ����� PlayerVehicleController, ���� �� ����� � ����������
        if (playerVehicleController == null)
        {
            var go = GameObject.FindWithTag("Player");
            if (go != null)
                playerVehicleController = go.GetComponent<PlayerVehicleController>();

            if (playerVehicleController == null)
                playerVehicleController = UnityEngine.Object.FindFirstObjectByType<PlayerVehicleController>();
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
    /// ���������� ������� ����, �� ������� ������� �����.
    /// </summary>
    void UpdateLookTarget()
    {
        lastTarget = currentTarget;
        currentTarget = null;

        if (mainCamera == null) return;

        Vector3 origin = mainCamera.transform.position + mainCamera.transform.forward * originForwardOffset;
        Vector3 dir = mainCamera.transform.forward;

        if (debugRay) Debug.DrawRay(origin, dir * maxDistance, Color.green);

        // �������� ������: sphere ��� ray
        bool found = TryFindInteractable(origin, dir, maxDistance, aimRadius, out Interactable foundInteractable);

        // Fallback: ���� �� ������� � fallbackForward > 0 � ������� ��� ��� � origin, ��������� �����
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

        // ��������� ����� ���� (enter/exit)
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
                string key = baseComp != null && !string.IsNullOrEmpty(baseComp.keyLabel) ? baseComp.keyLabel : "F";
                string hint = baseComp != null && !string.IsNullOrEmpty(baseComp.hintText) ? baseComp.hintText : "��������������";

                // ���������� ����������� API InteractionHintUI: key � ����� ��������.
                // UI ��� ���������� [F] � ���������.
                InteractionHintUI.Instance?.SetVisible(true, key, hint);
            }
        }
    }

    /// <summary>
    /// �������� ����� ������ ���������� Interactable �� ����/�����.
    /// �����: ��������� ��������� PlayerVehicleController.IsInVehicle � ���������� �������
    /// (VehicleSeatInteractable) � InteractableActionHost � ignoreWhileInVehicle == true
    /// �� ����� �������������.
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

            // ������� �� ������������� ����
            if (ignoreLayer.value != 0 && ((1 << h.collider.gameObject.layer) & ignoreLayer) != 0)
                continue;

            // ������� �� �����
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

            // ������� �� ����� �����������
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

            // ���� Interactable
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
                // --- �������������� ������ ��� ������ ������������� ---
                if (playerVehicleController != null && playerVehicleController.IsInVehicle)
                {
                    var mb = (found as MonoBehaviour);
                    if (mb != null)
                    {
                        // ���� ��� ������� (VehicleSeatInteractable) � ���������� ���, ���� ����� � ����������
                        var seat = mb.GetComponent<VehicleSeatInteractable>();
                        if (seat != null)
                        {
                            // ���������� ���� ��� � ���� ���������� ���������
                            continue;
                        }

                        // ���� ��� InteractableActionHost � ������� ignoreWhileInVehicle � ���� ����������
                        var host = mb.GetComponent<InteractableActionHost>();
                        if (host != null && host.ignoreWhileInVehicle)
                        {
                            continue;
                        }
                    }
                }

                // ���� �� ������������� � ��������� ��� ������� ����
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