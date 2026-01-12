// InteractionMenuUI.cs
// Менеджер UI-меню интерации.
// Управляет видимостью панели, назначает обработчики для кнопок и поддерживает клавиатурные хоткеи (F = выбрать первую опцию, Esc = отмена).

using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;
using UnityEngine.InputSystem;

public class InteractionMenuUI : MonoBehaviour
{
    public static InteractionMenuUI Instance;

    [Header("UI refs")]
    public CanvasGroup root; // CanvasGroup на InteractionMenuPanel
    public TextMeshProUGUI titleText;   // TMP заголовок
    public Button buttonOption1;        // кнопка 1 (Переместиться)
    public Button buttonOption2;        // кнопка 2 (Загрузить сцену) — может быть скрыта
    public Button buttonCancel;         // кнопка отмены

    // internal callbacks (назначаются при Show)
    Action onOption1;
    Action onOption2;
    Action onCancel;

    void Awake()
    {
        // Singleton-подобная инициализация
        if (Instance == null) Instance = this;
        else { Destroy(gameObject); return; }

        // Если root не назначен в инспекторе — пытаемся получить на этом объекте
        if (root == null) root = GetComponent<CanvasGroup>();

        // Скрываем меню в начале
        SetVisible(false);
    }

    void Update()
    {
        // Обработка клавиатуры только когда меню видно
        if (!IsVisible()) return;

        if (Keyboard.current != null)
        {
            // Нажатие F — подтверждение первой опции
            if (Keyboard.current.fKey.wasPressedThisFrame)
            {
                Debug.Log("[InteractionMenuUI] F pressed -> execute option1");
                onOption1?.Invoke();
                Hide();
            }

            // Нажатие Escape — отмена/закрыть
            if (Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                Debug.Log("[InteractionMenuUI] Escape pressed -> cancel");
                onCancel?.Invoke();
                Hide();
            }
        }
    }

    // Показывает меню. option2Label может быть null -> вторая кнопка будет скрыта.
    public void Show(string title, string option1Label, Action option1Callback,
                     string option2Label = null, Action option2Callback = null, Action cancelCallback = null)
    {
        Debug.Log("[InteractionMenuUI] Show: " + title);

        if (titleText != null) titleText.text = title;
        if (buttonOption1 != null)
        {
            var tmp = buttonOption1.GetComponentInChildren<TextMeshProUGUI>();
            if (tmp != null) tmp.text = option1Label;
        }

        onOption1 = option1Callback;

        if (!string.IsNullOrEmpty(option2Label) && option2Callback != null)
        {
            if (buttonOption2 != null)
            {
                buttonOption2.gameObject.SetActive(true);
                var tmp2 = buttonOption2.GetComponentInChildren<TextMeshProUGUI>();
                if (tmp2 != null) tmp2.text = option2Label;
            }
            onOption2 = option2Callback;
        }
        else
        {
            if (buttonOption2 != null) buttonOption2.gameObject.SetActive(false);
            onOption2 = null;
        }

        onCancel = cancelCallback;

        // Очистка старых подписок, чтобы не было дублирующих вызовов
        if (buttonOption1 != null)
        {
            buttonOption1.onClick.RemoveAllListeners();
            buttonOption1.onClick.AddListener(() =>
            {
                Debug.Log("[InteractionMenuUI] Option1 clicked");
                onOption1?.Invoke();
                Hide();
            });
        }

        if (buttonOption2 != null && buttonOption2.gameObject.activeSelf)
        {
            buttonOption2.onClick.RemoveAllListeners();
            buttonOption2.onClick.AddListener(() =>
            {
                Debug.Log("[InteractionMenuUI] Option2 clicked");
                onOption2?.Invoke();
                Hide();
            });
        }

        if (buttonCancel != null)
        {
            buttonCancel.onClick.RemoveAllListeners();
            buttonCancel.onClick.AddListener(() =>
            {
                Debug.Log("[InteractionMenuUI] Cancel clicked");
                onCancel?.Invoke();
                Hide();
            });
        }

        SetVisible(true);

        // Дополнительно: можно заблокировать управление игрока пока меню открыто
        // var pc = FindObjectOfType<PlayerController>();
        // if (pc != null) pc.enabled = false;
    }

    // Скрывает меню
    public void Hide()
    {
        SetVisible(false);

        // Разблокировать управление игрока, если блокировали
        // var pc = FindObjectOfType<PlayerController>();
        // if (pc != null) pc.enabled = true;
    }

    // Возвращает true, если меню сейчас видно
    public bool IsVisible()
    {
        return root != null && root.gameObject.activeSelf;
    }

    // Включает/выключает видимость и взаимодействие (через CanvasGroup)
    void SetVisible(bool v)
    {
        if (root == null) return;

        // Чтобы корректно взаимодействовать с кнопками, ставим activeSelf и затем управляем через CanvasGroup
        root.gameObject.SetActive(v);
        root.alpha = v ? 1f : 0f;
        root.interactable = v;
        root.blocksRaycasts = v;
    }
}