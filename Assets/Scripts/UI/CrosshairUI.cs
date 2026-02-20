using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class CrosshairUI : MonoBehaviour
{
    [Header("UI refs")]
    public Image crosshairImage;
    public Image backgroundImage;
    public float hoverPulseSpeed = 4f;
    public Color normalColor = Color.white;
    public Color hoverColor = new Color(1f, 0.85f, 0.2f);
    public float normalScale = 1f;
    public float hoverScale = 1.25f;

    bool isShown = true;
    bool isHover = false;

    void Awake()
    {
        UIServices.Register(this);

        if (crosshairImage == null)
            crosshairImage = GetComponentInChildren<Image>();

        SetVisible(true);
        SetHover(false);
    }

    void OnDestroy()
    {
        UIServices.Unregister(this);
    }

    void Update()
    {
        if (!isShown) return;

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
            if (backgroundImage != null)
                backgroundImage.color = Color.Lerp(backgroundImage.color, Color.clear, Time.deltaTime * 10f);
            crosshairImage.color = normalColor;
        }
    }

    public void SetVisible(bool v)
    {
        isShown = v;
        if (crosshairImage != null) crosshairImage.enabled = v;
        if (backgroundImage != null) backgroundImage.enabled = v;
    }

    public void SetHover(bool hover)
    {
        isHover = hover;
    }

    public void DoClickPulse()
    {
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