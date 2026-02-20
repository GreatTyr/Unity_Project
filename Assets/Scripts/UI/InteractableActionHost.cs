using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Универсальный хост действий для интерактивных объектов.
/// Содержит список ActionEntry и выполняет их при Interact().
/// </summary>
[RequireComponent(typeof(Collider))]
public class InteractableActionHost : InteractableBase
{
    public enum ExecutionMode { Single, Sequence }

    [Header("Action Host")]
    [Tooltip("Режим выполнения: Single = одно действие, Sequence = все по порядку.")]
    public ExecutionMode executionMode = ExecutionMode.Single;

    [Tooltip("Индекс активного действия при режиме Single (0-based).")]
    public int activeIndex = 0;

    [Tooltip("Список действий, выполняемых при Interact().")]
    public List<ActionEntry> actions = new List<ActionEntry>();

    [Header("Настройки транспорта")]
    [Tooltip("Если true и игрок в транспорте — hover не срабатывает.")]
    public bool ignoreWhileInVehicle = false;

    private PlayerVehicleController ResolvePlayerVehicleController()
    {
        return PlayerLocator.VehicleController;
    }

    public override void OnHoverEnter()
    {
        if (ignoreWhileInVehicle)
        {
            var pvc = ResolvePlayerVehicleController();
            if (pvc != null && pvc.IsInVehicle)
                return;
        }

        base.OnHoverEnter();
    }

    public override void OnHoverExit()
    {
        if (ignoreWhileInVehicle)
        {
            var pvc = ResolvePlayerVehicleController();
            if (pvc != null && pvc.IsInVehicle)
                return;
        }

        base.OnHoverExit();
    }

    /// <summary>
    /// Выполнить действие по индексу.
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

        if (a.delayBefore > 0f)
            yield return new WaitForSeconds(a.delayBefore);

        switch (a.type)
        {
            case ActionType.LoadScene:
                if (string.IsNullOrEmpty(a.sceneName))
                {
                    Debug.LogWarning($"[InteractableActionHost] LoadScene: не указано имя сцены на {name}");
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
                    Debug.LogWarning($"[InteractableActionHost] TeleportLocal: не указана точка телепорта на {name}");
                    yield break;
                }
                var mover = UnityEngine.Object.FindFirstObjectByType<PlayerMover>();
                if (mover != null)
                {
                    mover.TeleportTo(a.teleportTarget.position);
                }
                else
                {
                    Debug.LogWarning($"[InteractableActionHost] PlayerMover не найден для TeleportLocal на {name}");
                }
                break;

            case ActionType.OpenMenu:
                var menuUI = UIServices.Get<InteractionMenuUI>();
                if (menuUI == null)
                {
                    Debug.LogWarning($"[InteractableActionHost] OpenMenu: InteractionMenuUI не найден на {name}");
                    yield break;
                }

                Action opt1 = null;
                Action opt2 = null;

                if (a.teleportTarget != null)
                    opt1 = () => {
                        var m = UnityEngine.Object.FindFirstObjectByType<PlayerMover>();
                        if (m != null) m.TeleportTo(a.teleportTarget.position);
                    };
                else if (!string.IsNullOrEmpty(a.sceneName))
                    opt1 = () => SceneManager.LoadScene(a.sceneName);
                else
                    opt1 = () => Debug.Log($"[InteractableActionHost] OpenMenu option1: действие не задано для {name}");

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

                menuUI.Show(title, label1, opt1, label2, opt2, () => { });
                break;


            case ActionType.EnterVehicle:
                if (a.vehicleRoot == null)
                {
                    Debug.LogWarning($"[InteractableActionHost] EnterVehicle: не указан vehicleRoot на {name}");
                    yield break;
                }

                var seat = a.vehicleRoot.GetComponentInChildren<VehicleSeatInteractable>();
                if (seat != null)
                {
                    seat.Interact();
                }
                else
                {
                    Debug.Log($"[InteractableActionHost] EnterVehicle: VehicleSeatInteractable не найден на {a.vehicleRoot.name}");
                }
                break;

            case ActionType.CustomCallback:
                a.customCallback?.Invoke();
                break;

            default:
                Debug.LogWarning($"[InteractableActionHost] Неизвестный тип действия {a.type} на {name}");
                break;
        }

        yield break;
    }

    public override void Interact()
    {
        if (actions == null || actions.Count == 0)
        {
            Debug.Log($"[InteractableActionHost] Interact: нет назначенных действий на {name}");
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
    [Tooltip("Тип действия.")]
    public ActionType type = ActionType.None;

    [Header("Общее")]
    [Tooltip("Задержка перед выполнением (сек).")]
    public float delayBefore = 0f;

    [Header("Загрузка сцены")]
    [Tooltip("Имя сцены для LoadScene.")]
    public string sceneName;
    [Tooltip("Асинхронная загрузка сцены.")]
    public bool useAsync = true;

    [Header("Телепорт")]
    [Tooltip("Точка назначения внутри сцены.")]
    public Transform teleportTarget;

    [Header("Меню")]
    public string menuTitle;
    public string option1Label;
    public string option2Label;
    [Tooltip("Вторая сцена для второй опции меню.")]
    public string sceneNameSecondary;

    [Header("Транспорт")]
    [Tooltip("Корневой объект транспорта.")]
    public GameObject vehicleRoot;

    [Header("Пользовательское действие")]
    [Tooltip("Callback для программного назначения (не сериализуется).")]
    [NonSerialized] public Action customCallback;
}