using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

/// <summary>
/// InteractionHintUI - улучшённый менеджер подсказки при наведении на интерактивные объекты.
/// Поддерживает:
/// - Раздельные поля для "key" (клавиши, например [F]) и основного hint-текста.
/// - Плавное появление/исчезновение через CanvasGroup.alpha.
/// - Автоадаптацию фонового изображения под размер текста (при включении ContentSizeFitter на background).
/// - Пульсацию background при видимости (опционально).
/// - Защиту от наложений и множественных вызовов SetVisible.
/// 
/// Привязки в Inspector:
/// - root: CanvasGroup на панели подсказки
/// - keyText: TMP поле для отображения клавиши (опционально)
/// - hintText: TMP поле для основного сообщения
/// - backgroundImage: Image фон (опционально)
/// 
/// Использование:
/// InteractionHintUI.Instance.SetVisible(true, keyLabel, hint);
/// InteractionHintUI.Instance.SetVisible(false);
/// </summary>
[DisallowMultipleComponent]
public class InteractionHintUI : MonoBehaviour
{
    public static InteractionHintUI Instance;

    [Header("UI refs")]
    [Tooltip("CanvasGroup на панели подсказки (позволяет плавный fade и блокировку взаимодействий)")]
    public CanvasGroup root;

    [Tooltip("Маленькое поле для отображения клавиши/иконки (например: F) — опционально")]
    public TextMeshProUGUI keyText;

    [Tooltip("Основное текстовое поле подсказки (например: \"Сесть за штурвал\")")]
    public TextMeshProUGUI hintText;

    [Tooltip("Фоновое изображение панели — может иметь ContentSizeFitter для авто-подгонки")]
    public Image backgroundImage;

    [Header("Appearance")]
    [Tooltip("Скорость fade in/out")]
    public float fadeSpeed = 10f;

    [Tooltip("Пульсация фона когда подсказка видима (0 = off)")]
    public float pulseSpeed = 2f;

    [Tooltip("Пульсация цвета фона (линейно интерполируется между base и pulse)")]
    public Color baseBackgroundColor = new Color(0f, 0f, 0f, 0.6f);

    [Tooltip("Цвет пульсации (добавляется к base)")]
    public Color pulseBackgroundColor = new Color(0.9f, 0.8f, 0.25f, 0.9f);

    [Header("Layout / TMP fallback")]
    [Tooltip("Если true — при пустом keyText поле будет скрыто и hintText займет всю ширину")]
    public bool hideKeyIfEmpty = true;

    // internal state
    bool targetVisible = false;
    float currentAlpha = 0f;

    void Awake()
    {
        // Singleton-like
        if (Instance == null) Instance = this;
        else Destroy(gameObject);

        // Ensure references
        if (root == null) root = GetComponent<CanvasGroup>();
        if (root != null)
        {
            // Start hidden
            root.alpha = 0f;
            root.interactable = false;
            root.blocksRaycasts = false;
        }

        if (backgroundImage != null)
            backgroundImage.color = baseBackgroundColor;
    }

    void Update()
    {
        // Smooth fade of alpha
        if (root != null)
        {
            float targetAlpha = targetVisible ? 1f : 0f;
            currentAlpha = Mathf.MoveTowards(currentAlpha, targetAlpha, fadeSpeed * Time.deltaTime);
            root.alpha = currentAlpha;

            bool visibleNow = root.alpha > 0.01f;
            root.interactable = visibleNow;
            root.blocksRaycasts = visibleNow;
        }

        // Pulse background if visible
        if (targetVisible && backgroundImage != null && pulseSpeed > 0f)
        {
            float t = (Mathf.Sin(Time.time * pulseSpeed) + 1f) * 0.5f; // 0..1
            backgroundImage.color = Color.Lerp(baseBackgroundColor, pulseBackgroundColor, t * 0.6f);
        }
        else if (backgroundImage != null)
        {
            // Smoothly return to base color
            backgroundImage.color = Color.Lerp(backgroundImage.color, baseBackgroundColor, Time.deltaTime * fadeSpeed);
        }
    }

    /// <summary>
    /// Установить видимость подсказки.
    /// Если text или key не null — обновляем поля.
    /// Для удобства: можно вызвать SetVisible(true, "[F]", "Переместиться") или SetVisible(true, null, "Переместиться") если клавиши нет.
    /// </summary>
    public void SetVisible(bool visible, string key = null, string text = null)
    {
        // Если видимость не меняется и текст тот же — ничего не делаем
        bool sameKey = (keyText == null && key == null) || (keyText != null && keyText.text == key);
        bool sameText = (hintText == null && text == null) || (hintText != null && hintText.text == text);
        if (targetVisible == visible && sameKey && sameText) return;

        targetVisible = visible;

        // Update content immediately so layout can recalc while fading in/out
        if (keyText != null)
        {
            if (key == null)
            {
                keyText.text = "";
                keyText.gameObject.SetActive(!hideKeyIfEmpty);
            }
            else
            {
                keyText.text = key;
                keyText.gameObject.SetActive(true);
            }
        }

        if (hintText != null && text != null)
        {
            hintText.text = text;
        }

        // If hiding — reset background color quickly
        if (!visible && backgroundImage != null)
        {
            backgroundImage.color = baseBackgroundColor;
        }
    }

    /// <summary>
    /// Удобный wrapper старого API — если у вас вызывают SetVisible(bool, string)
    /// (где string содержит уже формат [F] text), мы попытаемся разбить на key+hints.
    /// Формат ожидаемый: \"[F] Some text\" или просто \"Some text\".
    /// </summary>
    public void SetVisible(bool visible, string combinedText)
    {
        if (string.IsNullOrEmpty(combinedText))
        {
            SetVisible(visible, null, null);
            return;
        }

        // Пробуем распарсить [KEY] в начале
        string key = null;
        string hint = combinedText;

        if (combinedText.StartsWith("["))
        {
            int closing = combinedText.IndexOf(']');
            if (closing > 1)
            {
                key = combinedText.Substring(0, closing + 1); // including brackets
                hint = combinedText.Substring(closing + 1).TrimStart();
            }
        }

        SetVisible(visible, key, hint);
    }

    /// <summary>
    /// Простая утилита: немедленно скрыть подсказку и очистить текст.
    /// </summary>
    public void HideImmediate()
    {
        targetVisible = false;
        if (root != null)
        {
            root.alpha = 0f;
            root.interactable = false;
            root.blocksRaycasts = false;
        }

        if (keyText != null) { keyText.text = ""; keyText.gameObject.SetActive(!hideKeyIfEmpty); }
        if (hintText != null) hintText.text = "";
        if (backgroundImage != null) backgroundImage.color = baseBackgroundColor;
    }
}