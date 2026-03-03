using System;
using UnityEngine;

public static class WorkbenchPopup
{
    private static bool _showing;
    private static string[] _options;
    private static int _current;
    private static Action<int> _callback;
    private static Rect _popupRect;
    private static Vector2 _scrollPos;
    private static readonly int _windowId = 987655;
    private static int _showFrame;

    public static bool IsShowing => _showing;
    public static Rect PopupRect => _popupRect;

    public static void Show(string[] options, int current, Vector2 screenPos, Action<int> callback)
    {
        _options = options;
        _current = current;
        _callback = callback;
        _showing = true;
        _scrollPos = Vector2.zero;
        _showFrame = Time.frameCount;

        float itemHeight = 26f;
        float h = Mathf.Min((options != null ? options.Length : 0) * itemHeight + 10f, 400f);
        float w = 350f;

        float x = Mathf.Clamp(screenPos.x, 5f, Mathf.Max(5f, Screen.width - w - 5f));
        float y = Mathf.Clamp(screenPos.y, 5f, Mathf.Max(5f, Screen.height - h - 5f));
        _popupRect = new Rect(x, y, w, h);
    }

    public static void Hide()
    {
        _showing = false;
        _options = null;
        _callback = null;
    }

    public static void DrawPopup()
    {
        if (!_showing || _options == null) return;
        GUI.BringWindowToFront(_windowId);
        _popupRect = GUI.Window(_windowId, _popupRect, DrawPopupWindow, "", GUI.skin.box);
    }

    private static void DrawPopupWindow(int id)
    {
        bool canInteract = Time.frameCount > _showFrame;
        _scrollPos = GUILayout.BeginScrollView(_scrollPos);

        for (int i = 0; i < _options.Length; i++)
        {
            bool selected = (i == _current);
            if (GUILayout.Button(_options[i], selected ? GetSelectedStyle() : GetNormalStyle(), GUILayout.Height(24)))
            {
                if (!canInteract) continue;
                _callback?.Invoke(i);
                Hide();
                GUIUtility.ExitGUI();
                return;
            }
        }

        GUILayout.EndScrollView();
    }

    private static GUIStyle _normalStyle;
    private static GUIStyle _selectedStyle;

    private static GUIStyle GetNormalStyle()
    {
        if (_normalStyle == null)
        {
            _normalStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleLeft,
                padding = new RectOffset(8, 4, 2, 2),
                normal = { textColor = Color.white },
                hover = { textColor = Color.white, background = MakeTex(1, 1, new Color(0.3f, 0.3f, 0.3f, 0.8f)) }
            };
        }
        return _normalStyle;
    }

    private static GUIStyle GetSelectedStyle()
    {
        if (_selectedStyle == null)
        {
            _selectedStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleLeft,
                fontStyle = FontStyle.Bold,
                padding = new RectOffset(8, 4, 2, 2),
                normal = { textColor = Color.white, background = MakeTex(1, 1, new Color(0.25f, 0.25f, 0.25f, 0.9f)) }
            };
        }
        return _selectedStyle;
    }

    public static Texture2D MakeTex(int w, int h, Color col)
    {
        var pix = new Color[w * h];
        for (int i = 0; i < pix.Length; i++) pix[i] = col;
        var tex = new Texture2D(w, h);
        tex.SetPixels(pix);
        tex.Apply();
        return tex;
    }
}