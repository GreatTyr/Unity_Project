using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// InteractableActionHost — расширяемый компонент для интерактивных объектов.
/// Позволяет в инспекторе задать список действий (ActionEntry) и выполнить их при Interact().
/// Наследует поведение InteractableBase (подсветка, hintText и т.п.).
///
/// Особенности:
/// - два режима выполнения: Single (выполнить одну выбранную action) и Sequence (выполнить все по порядку).
/// - реализованы базовые действия: LoadScene, TeleportLocal, OpenMenu, EnterVehicle, CustomCallback.
/// - задержки (delayBefore) и асинхронная загрузка сцен поддерживаются.
/// - CustomCallback может быть назначен в рантайме (action.customCallback = ()=>{ ... } ).
///
/// Применение:
/// - Добавь компонент на интерактивный объект (или замени старые Interactable*).
/// - Заполни список actions через инспектор.
/// - PlayerInteractionManager вызывает Interact() как раньше и всё сработает.
/// </summary>
[RequireComponent(typeof(Collider))]
public class InteractableActionHost : InteractableBase
{
    public enum ExecutionMode { Single, Sequence }

    [Header("Action Host")]
    [Tooltip("Режим выполнения: Single = выполнить только выбранную (activeIndex), Sequence = выполнить все по порядку")]
    public ExecutionMode executionMode = ExecutionMode.Single;

    [Tooltip("Если executionMode == Single, какой индекс в списке выбирать (0-based).")]
    public int activeIndex = 0;

    [Tooltip("Список действий, которые можно выполнить при Interact()")]
    public List<ActionEntry> actions = new List<ActionEntry>();

    /// <summary>
    /// Выполнить действие по индексу (из внешнего кода).
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

        // Универсальная задержка перед action
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
                var mover = FindObjectOfType<PlayerMover>();
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

                // option 1 — teleport local if provided, else sceneName, else no-op
                if (a.teleportTarget != null)
                    opt1 = () => {
                        var m = FindObjectOfType<PlayerMover>();
                        if (m != null) m.TeleportTo(a.teleportTarget.position);
                    };
                else if (!string.IsNullOrEmpty(a.sceneName))
                    opt1 = () => SceneManager.LoadScene(a.sceneName);
                else
                    opt1 = () => Debug.Log($"[InteractableActionHost] OpenMenu option1 no-op for {name}");

                // option 2 — secondary scene or custom callback if provided
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

                // Простейшее поведение: ищем компонент VehicleSeatInteractable на этом объекте
                // или вызываем интерфейс IVehicle (если будет реализован)
                var seat = a.vehicleRoot.GetComponentInChildren<VehicleSeatInteractable>();
                if (seat != null)
                {
                    // Если на штурвале уже есть логика — вызываем её Interact().
                    seat.Interact();
                }
                else
                {
                    // В будущем: реализовать IVehicle и передачу управления
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

/// <summary>
/// Типы действий — расширяй по мере необходимости.
/// </summary>
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

/// <summary>
/// ActionEntry — serializable запись для одного действия.
/// Добавляй сюда поля, которые понадобятся новым типам.
/// </summary>
[Serializable]
public class ActionEntry
{
    [Tooltip("Тип действия")]
    public ActionType type = ActionType.None;

    [Header("Generic")]
    [Tooltip("Задержка перед выполнением (сек)")]
    public float delayBefore = 0f;

    [Header("LoadScene")]
    [Tooltip("Имя сцены для LoadScene")]
    public string sceneName;
    [Tooltip("Асинхронно загружать сцену")]
    public bool useAsync = true;

    [Header("Teleport")]
    [Tooltip("Точка телепорта внутри сцены")]
    public Transform teleportTarget;

    [Header("Menu")]
    public string menuTitle;
    public string option1Label;
    public string option2Label;
    [Tooltip("Вторая сцена / опция")]
    public string sceneNameSecondary;

    [Header("Vehicle")]
    [Tooltip("Ссылка на корень транспорта/штурвала")]
    public GameObject vehicleRoot;

    [Header("Custom")]
    [Tooltip("Callback для выполнения (назначается в рантайме из кода)")]
    [NonSerialized] public Action customCallback;
}