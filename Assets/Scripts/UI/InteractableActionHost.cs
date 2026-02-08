using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// InteractableActionHost � ����������� ��������� ��� ������������� ��������.
/// ��������� � ���������� ������ ������ �������� (ActionEntry) � ��������� �� ��� Interact().
/// ��������� ��������� InteractableBase (���������, hintText � �.�.).
///
/// �����������:
/// - ��� ������ ����������: Single (��������� ���� ��������� action) � Sequence (��������� ��� �� �������).
/// - ����������� ������� ��������: LoadScene, TeleportLocal, OpenMenu, EnterVehicle, CustomCallback.
/// - �������� (delayBefore) � ����������� �������� ���� ��������������.
/// - CustomCallback ����� ���� �������� � �������� (action.customCallback = ()=>{ ... } ).
/// 
/// ����������:
/// - ���� ignoreWhileInVehicle ��������� ������������ hover (���������/���������),
///   ���� ����� ������ ����� � ���������� (PlayerVehicleController.IsInVehicle == true).
///   ������, ����� ���� ���� ����� �� ��������.
/// </summary>
[RequireComponent(typeof(Collider))]
public class InteractableActionHost : InteractableBase
{
    public enum ExecutionMode { Single, Sequence }

    [Header("Action Host")]
    [Tooltip("����� ����������: Single = ��������� ������ ��������� (activeIndex), Sequence = ��������� ��� �� �������")]
    public ExecutionMode executionMode = ExecutionMode.Single;

    [Tooltip("���� executionMode == Single, ����� ������ � ������ �������� (0-based).")]
    public int activeIndex = 0;

    [Tooltip("������ ��������, ������� ����� ��������� ��� Interact()")]
    public List<ActionEntry> actions = new List<ActionEntry>();

    [Header("Vehicle / Hover ���������")]
    [Tooltip("���� true � ����� ����� ����� � ���������� (PlayerVehicleController.IsInVehicle), " +
             "hover �� ����� ������� ����� �������������� (��� ��������� � ���������). " +
             "������ ��� �� ��������, ���� �� ��� ����� ���� ActionHost.")]
    public bool ignoreWhileInVehicle = false;

    /// <summary>
    /// ������� PlayerVehicleController � ����� (�� ���� 'Player' ��� ����� FindObjectOfType).
    /// </summary>
        private PlayerVehicleController ResolvePlayerVehicleController()
        {
            var go = GameObject.FindWithTag("Player");
            if (go != null)
            {
                var pvc = go.GetComponent<PlayerVehicleController>();
                if (pvc != null) return pvc;
            }

            // ??????????? API ?????? ??????? ? ?????
            return UnityEngine.Object.FindFirstObjectByType<PlayerVehicleController>();
        }

    public override void OnHoverEnter()
    {
        if (ignoreWhileInVehicle)
        {
            var pvc = ResolvePlayerVehicleController();
            if (pvc != null && pvc.IsInVehicle)
            {
                // ����� � ���������� � ���������� hover ��� ����� �����������.
                return;
            }
        }

        base.OnHoverEnter();
    }

    public override void OnHoverExit()
    {
        if (ignoreWhileInVehicle)
        {
            var pvc = ResolvePlayerVehicleController();
            if (pvc != null && pvc.IsInVehicle)
            {
                // ���������� OnHoverEnter � ��� ����� �������, ���� ����� � ����������,
                // �� ������� ��������� (��� � ��� �� ����������).
                return;
            }
        }

        base.OnHoverExit();
    }

    /// <summary>
    /// ��������� �������� �� ������� (�� �������� ����).
    /// </summary>
    public void ExecuteAction(int index)
    {
        if (index < 0 || index >= actions.Count)
        {
            Debug.LogWarning($"[InteractableActionHost] ExecuteAction: index {index} out of range for {name}");
            return;
        }

        StartCoroutine(ExecuteActionCoroutine(actions[index]));
    }

    IEnumerator ExecuteAll()
    {
        foreach (var a in actions)
        {
            yield return ExecuteActionCoroutine(a);
        }
    }

    IEnumerator ExecuteActionCoroutine(ActionEntry a)
    {
        if (a == null || a.type == ActionType.None)
            yield break;

        // ������������� �������� ����� action
        if (a.delayBefore > 0f)
            yield return new WaitForSeconds(a.delayBefore);

        switch (a.type)
        {
            case ActionType.LoadScene:
                if (string.IsNullOrEmpty(a.sceneName))
                {
                    Debug.LogWarning($"[InteractableActionHost] LoadScene action missing sceneName on {name}");
                    yield break;
                }
                if (a.useAsync)
                {
                    var op = SceneManager.LoadSceneAsync(a.sceneName);
                    while (!op.isDone) yield return null;
                }
                else
                {
                    SceneManager.LoadScene(a.sceneName);
                }
                break;

            case ActionType.TeleportLocal:
                if (a.teleportTarget == null)
                {
                    Debug.LogWarning($"[InteractableActionHost] TeleportLocal missing teleportTarget on {name}");
                    yield break;
                }
                var mover = UnityEngine.Object.FindFirstObjectByType<PlayerMover>();
                if (mover != null)
                {
                    mover.TeleportTo(a.teleportTarget.position);
                }
                else
                {
                    Debug.LogWarning($"[InteractableActionHost] PlayerMover not found for TeleportLocal on {name}");
                }
                break;

            case ActionType.OpenMenu:
                if (InteractionMenuUI.Instance == null)
                {
                    Debug.LogWarning($"[InteractableActionHost] OpenMenu requested but InteractionMenuUI.Instance == null on {name}");
                    yield break;
                }

                Action opt1 = null;
                Action opt2 = null;

                // option 1 � teleport local if provided, else sceneName, else no-op
                if (a.teleportTarget != null)
                    opt1 = () => {
                        var m = UnityEngine.Object.FindObjectOfType<PlayerMover>();
                        if (m != null) m.TeleportTo(a.teleportTarget.position);
                    };
                else if (!string.IsNullOrEmpty(a.sceneName))
                    opt1 = () => SceneManager.LoadScene(a.sceneName);
                else
                    opt1 = () => Debug.Log($"[InteractableActionHost] OpenMenu option1 no-op for {name}");

                // option 2 � secondary scene or custom callback if provided
                if (!string.IsNullOrEmpty(a.sceneNameSecondary))
                {
                    opt2 = () => SceneManager.LoadScene(a.sceneNameSecondary);
                }
                else
                {
                    opt2 = a.customCallback;
                }

                string title = string.IsNullOrEmpty(a.menuTitle) ? hintText : a.menuTitle;
                string label1 = string.IsNullOrEmpty(a.option1Label) ? "OK" : a.option1Label;
                string label2 = string.IsNullOrEmpty(a.option2Label) ? null : a.option2Label;

                InteractionMenuUI.Instance.Show(title, label1, opt1, label2, opt2, () => { });
                break;

            case ActionType.EnterVehicle:
                if (a.vehicleRoot == null)
                {
                    Debug.LogWarning($"[InteractableActionHost] EnterVehicle missing vehicleRoot on {name}");
                    yield break;
                }

                // ���������� ���������: ���� ��������� VehicleSeatInteractable �� ���� �������
                var seat = a.vehicleRoot.GetComponentInChildren<VehicleSeatInteractable>();
                if (seat != null)
                {
                    // ���� �� �������� ��� ���� ������ � �������� � Interact().
                    seat.Interact();
                }
                else
                {
                    Debug.Log($"[InteractableActionHost] EnterVehicle requested for {a.vehicleRoot.name}, but VehicleSeatInteractable not found. Implement IVehicle for full behavior.");
                }
                break;

            case ActionType.CustomCallback:
                a.customCallback?.Invoke();
                break;

            default:
                Debug.LogWarning($"[InteractableActionHost] Unknown action type {a.type} on {name}");
                break;
        }

        yield break;
    }

    public override void Interact()
    {
        if (actions == null || actions.Count == 0)
        {
            Debug.Log($"[InteractableActionHost] Interact called but no actions assigned on {name}");
            return;
        }

        if (executionMode == ExecutionMode.Single)
        {
            int idx = Mathf.Clamp(activeIndex, 0, actions.Count - 1);
            StartCoroutine(ExecuteActionCoroutine(actions[idx]));
        }
        else
        {
            StartCoroutine(ExecuteAll());
        }
    }
}

[Serializable]
public enum ActionType
{
    None = 0,
    LoadScene,
    TeleportLocal,
    OpenMenu,
    EnterVehicle,
    CustomCallback
}

[Serializable]
public class ActionEntry
{
    [Tooltip("��� ��������")]
    public ActionType type = ActionType.None;

    [Header("Generic")]
    [Tooltip("�������� ����� ����������� (���)")]
    public float delayBefore = 0f;

    [Header("LoadScene")]
    [Tooltip("��� ����� ��� LoadScene")]
    public string sceneName;
    [Tooltip("���������� ��������� �����")]
    public bool useAsync = true;

    [Header("Teleport")]
    [Tooltip("����� ��������� ������ �����")]
    public Transform teleportTarget;

    [Header("Menu")]
    public string menuTitle;
    public string option1Label;
    public string option2Label;
    [Tooltip("������ ����� / �����")]
    public string sceneNameSecondary;

    [Header("Vehicle")]
    [Tooltip("������ �� ������ ����������/��������")]
    public GameObject vehicleRoot;

    [Header("Custom")]
    [Tooltip("Callback ��� ���������� (����������� � �������� �� ����)")]
    [NonSerialized] public Action customCallback;
}