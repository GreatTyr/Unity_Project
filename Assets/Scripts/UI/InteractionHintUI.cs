using UnityEngine;
using UnityEngine.UI;
using TMPro;

[DisallowMultipleComponent]
public class InteractionHintUI : MonoBehaviour
{
    [Header("UI refs")]
    public CanvasGroup root;
    public TextMeshProUGUI keyText;
    public TextMeshProUGUI hintText;
    public Image backgroundImage;

    [Header("Appearance")]
    public float fadeSpeed = 10f;
    public float pulseSpeed = 2f;
    public Color baseBackgroundColor = new Color(0f, 0f, 0f, 0.6f);
    public Color pulseBackgroundColor = new Color(0.9f, 0.8f, 0.25f, 0.9f);

    [Header("Layout")]
    public bool hideKeyIfEmpty = true;

    bool targetVisible = false;
    float currentAlpha = 0f;

    void Awake()
    {
        UIServices.Register(this);

        if (root == null) root = GetComponent<CanvasGroup>();
        if (root != null)
        {
            root.alpha = 0f;
            root.interactable = false;
            root.blocksRaycasts = false;
        }

        if (backgroundImage != null)
            backgroundImage.color = baseBackgroundColor;
    }

    void OnDestroy()
    {
        UIServices.Unregister(this);
    }

    void Update()
    {
        if (root != null)
        {
            float targetAlpha = targetVisible ? 1f : 0f;
            currentAlpha = Mathf.MoveTowards(currentAlpha, targetAlpha, fadeSpeed * Time.deltaTime);
            root.alpha = currentAlpha;

            bool visibleNow = root.alpha > 0.01f;
            root.interactable = visibleNow;
            root.blocksRaycasts = visibleNow;
        }

        if (targetVisible && backgroundImage != null && pulseSpeed > 0f)
        {
            float t = (Mathf.Sin(Time.time * pulseSpeed) + 1f) * 0.5f;
            backgroundImage.color = Color.Lerp(baseBackgroundColor, pulseBackgroundColor, t * 0.6f);
        }
        else if (backgroundImage != null)
        {
            backgroundImage.color = Color.Lerp(backgroundImage.color, baseBackgroundColor, Time.deltaTime * fadeSpeed);
        }
    }

    public void SetVisible(bool visible, string key = null, string text = null)
    {
        bool sameKey = (keyText == null && key == null) || (keyText != null && keyText.text == key);
        bool sameText = (hintText == null && text == null) || (hintText != null && hintText.text == text);
        if (targetVisible == visible && sameKey && sameText) return;

        targetVisible = visible;

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
            hintText.text = text;

        if (!visible && backgroundImage != null)
            backgroundImage.color = baseBackgroundColor;
    }

    public void SetVisible(bool visible, string combinedText)
    {
        if (string.IsNullOrEmpty(combinedText))
        {
            SetVisible(visible, null, null);
            return;
        }

        string key = null;
        string hint = combinedText;

        if (combinedText.StartsWith("["))
        {
            int closing = combinedText.IndexOf(']');
            if (closing > 1)
            {
                key = combinedText.Substring(0, closing + 1);
                hint = combinedText.Substring(closing + 1).TrimStart();
            }
        }

        SetVisible(visible, key, hint);
    }

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