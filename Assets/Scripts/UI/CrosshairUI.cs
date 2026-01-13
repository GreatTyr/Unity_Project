using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// CrosshairUI — отвечает за отображение прицела в центре экрана.
/// Поддерживает состояния: Normal, HoverInteractive, Hidden.
/// Пулящий/пульсирующий фон при наведении на интерактивный объект.
/// </summary>
[DisallowMultipleComponent]
public class CrosshairUI : MonoBehaviour
{
    public static CrosshairUI Instance;

    [Header("UI refs")]
    public Image crosshairImage;            // основное изображение прицела
    public Image backgroundImage;           // опциональный круг позади прицела (для пульса)
    public float hoverPulseSpeed = 4f;      // скорость пульса при наведении
    public Color normalColor = Color.white;
    public Color hoverColor = new Color(1f, 0.85f, 0.2f); // теплый желтый
    public float normalScale = 1f;
    public float hoverScale = 1.25f;

    bool isShown = true;
    bool isHover = false;

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);

        if (crosshairImage == null)
            crosshairImage = GetComponentInChildren<Image>();

        SetVisible(true);
        SetHover(false);
    }

    void Update()
    {
        // Если hover — пульсируем фон и немного масштабируем прицел
        if (isHover)
        {
            float t = (Mathf.Sin(Time.time * hoverPulseSpeed) + 1f) * 0.5f;
            float s = Mathf.Lerp(normalScale, hoverScale, t);
            crosshairImage.transform.localScale = Vector3.one * s;
            if (backgroundImage != null)
            {
                backgroundImage.color = Color.Lerp(Color.clear, hoverColor, t * 0.6f);
                backgroundImage.transform.localScale = Vector3.one * (0.9f + 0.15f * t);
            }
            crosshairImage.color = hoverColor;
        }
        else
        {
            crosshairImage.transform.localScale = Vector3.one * normalScale;
            if (backgroundImage != null) backgroundImage.color = Color.Lerp(backgroundImage.color, Color.clear, Time.deltaTime * 10f);
            crosshairImage.color = normalColor;
        }
    }

    /// <summary>
    /// Показывает/скрывает прицел.
    /// </summary>
    public void SetVisible(bool v)
    {
        isShown = v;
        if (crosshairImage != null) crosshairImage.enabled = v;
        if (backgroundImage != null) backgroundImage.enabled = v;
    }

    /// <summary>
    /// Включить/выключить визуальное состояние наведения на интерактивный объект.
    /// </summary>
    public void SetHover(bool hover)
    {
        isHover = hover;
    }

    /// <summary>
    /// Быстро всплеск при клике/взаимодействии (короткая анимация).
    /// </summary>
    public void DoClickPulse()
    {
        // Для простоты: небольшой временный масштаб
        StartCoroutine(ClickPulseCoroutine());
    }

    System.Collections.IEnumerator ClickPulseCoroutine()
    {
        Vector3 orig = crosshairImage.transform.localScale;
        Vector3 target = orig * 1.3f;
        float t = 0f;
        while (t < 0.12f)
        {
            t += Time.deltaTime;
            crosshairImage.transform.localScale = Vector3.Lerp(orig, target, t / 0.12f);
            yield return null;
        }
        t = 0f;
        while (t < 0.12f)
        {
            t += Time.deltaTime;
            crosshairImage.transform.localScale = Vector3.Lerp(target, orig, t / 0.12f);
            yield return null;
        }
    }
}