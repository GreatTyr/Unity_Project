// InteractionHintUI.cs
// Управляет UI подсказкой, которая появляется на экране (например внизу или в центре).
// Предполагается один экземпляр (Singleton-like) в сцене: InteractionHintUI.Instance доступен другим скриптам.
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class InteractionHintUI : MonoBehaviour
{
    public static InteractionHintUI Instance;

    [Header("UI refs")]
    public CanvasGroup root;              // CanvasGroup для плавного включения/выключения
    public TextMeshProUGUI hintText;      // UI Text для строки подсказки
    public Image backgroundImage;         //  фон (чтобы пульсить)

    [Header("Pulse settings")]
    public Color baseColor = Color.white;
    public Color pulseColor = Color.yellow;
    public float pulseSpeed = 0.1f;

    bool visible = false;

    void Awake()
    {
        // Singleton-подобная инициализация
        if (Instance == null) Instance = this;
        else Destroy(gameObject);

        if (root == null) root = GetComponent<CanvasGroup>();
        SetVisible(false);
    }

    void Update()
    {
        // Если видимо — пульс фона
        if (!visible) return;
        float t = (Mathf.Sin(Time.time * pulseSpeed) + 1f) * 0.5f;
        if (backgroundImage != null) backgroundImage.color = Color.Lerp(baseColor, pulseColor, t);
    }

    public void SetVisible(bool v, string text = null)
    {
        // Включить/выключить подсказку, и (опционально) задать текст
        visible = v;
        if (root != null) root.alpha = v ? 1f : 0f;
        if (hintText != null && text != null) hintText.text = text;
        if (!v && backgroundImage != null) backgroundImage.color = baseColor;
    }
}

