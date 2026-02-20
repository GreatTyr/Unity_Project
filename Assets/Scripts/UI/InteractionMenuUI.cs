using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using TMPro;
using System;

public class InteractionMenuUI : MonoBehaviour
{
    [Header("UI refs")]
    public CanvasGroup root;
    public TextMeshProUGUI titleText;
    public Button buttonOption1;
    public Button buttonOption2;
    public Button buttonCancel;

    [Header("Input")]
    [SerializeField] private InputActionReference confirmAction;
    [SerializeField] private InputActionReference cancelAction;

    Action onOption1;
    Action onOption2;
    Action onCancel;

    void Awake()
    {
        UIServices.Register(this);

        if (root == null) root = GetComponent<CanvasGroup>();
        SetVisible(false);
    }

    void OnDestroy()
    {
        UIServices.Unregister(this);
    }

    void OnEnable()
    {
        InputActionHelper.Subscribe(confirmAction, OnConfirmPerformed);
        InputActionHelper.Subscribe(cancelAction, OnCancelPerformed);
    }

    void OnDisable()
    {
        InputActionHelper.Unsubscribe(confirmAction, OnConfirmPerformed);
        InputActionHelper.Unsubscribe(cancelAction, OnCancelPerformed);
    }

    private void OnConfirmPerformed(InputAction.CallbackContext ctx)
    {
        if (!ctx.performed || !IsVisible()) return;
        onOption1?.Invoke();
        Hide();
    }

    private void OnCancelPerformed(InputAction.CallbackContext ctx)
    {
        if (!ctx.performed || !IsVisible()) return;
        onCancel?.Invoke();
        Hide();
    }

    public void Show(string title, string option1Label, Action option1Callback,
                     string option2Label = null, Action option2Callback = null, Action cancelCallback = null)
    {
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

        if (buttonOption1 != null)
        {
            buttonOption1.onClick.RemoveAllListeners();
            buttonOption1.onClick.AddListener(() => { onOption1?.Invoke(); Hide(); });
        }

        if (buttonOption2 != null && buttonOption2.gameObject.activeSelf)
        {
            buttonOption2.onClick.RemoveAllListeners();
            buttonOption2.onClick.AddListener(() => { onOption2?.Invoke(); Hide(); });
        }

        if (buttonCancel != null)
        {
            buttonCancel.onClick.RemoveAllListeners();
            buttonCancel.onClick.AddListener(() => { onCancel?.Invoke(); Hide(); });
        }

        SetVisible(true);
    }

    public void Hide() => SetVisible(false);

    public bool IsVisible() => root != null && root.gameObject.activeSelf;

    void SetVisible(bool v)
    {
        if (root == null) return;
        root.gameObject.SetActive(v);
        root.alpha = v ? 1f : 0f;
        root.interactable = v;
        root.blocksRaycasts = v;
    }
}