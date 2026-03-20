using System;
using UnityEngine;

/// <summary>
/// Общие IMGUI helper-методы для всех workbench UI.
/// Первая безопасная итерация: только секция кода, разделитель и прогресс-бар.
/// </summary>
public static class WorkbenchUICommon
{
    private static Texture2D _textAreaBgTex;
    private static Texture2D _progressBgTex;
    private static Texture2D _progressFillTex;

    public static void DrawCompactCodeSection(
        string title,
        ref string text,
        bool readOnly,
        string button1Label,
        Action button1Action,
        GUIStyle panelStyle,
        GUIStyle boldStyle,
        string button2Label = null,
        Action button2Action = null)
    {
        GUILayout.BeginVertical(panelStyle);
        GUILayout.Label($"<color=#E0E0E0>{title}</color>", boldStyle);

        GUILayout.BeginHorizontal();

        GUIStyle textAreaStyle = CreateCodeTextAreaStyle(readOnly);

        if (readOnly) GUI.enabled = false;
        text = GUILayout.TextArea(text, textAreaStyle, GUILayout.Height(55));
        if (readOnly) GUI.enabled = true;

        GUILayout.BeginVertical(GUILayout.Width(110));

        if (GUILayout.Button(button1Label, GUILayout.Height(25)))
            button1Action?.Invoke();

        if (!string.IsNullOrEmpty(button2Label) && GUILayout.Button(button2Label, GUILayout.Height(25)))
            button2Action?.Invoke();

        GUILayout.EndVertical();
        GUILayout.EndHorizontal();
        GUILayout.EndVertical();
    }

    public static void DrawSeparator(Texture2D separatorTexture)
    {
        GUILayout.Space(5);
        GUILayout.Box(
            GUIContent.none,
            new GUIStyle { normal = { background = separatorTexture } },
            GUILayout.Height(2),
            GUILayout.ExpandWidth(true));
        GUILayout.Space(5);
    }

    public static void DrawProgressBar(Rect rect, float progress, string text)
    {
        progress = Mathf.Clamp01(progress);

        if (_progressBgTex == null)
            _progressBgTex = WorkbenchPopup.MakeTex(1, 1, new Color(0.12f, 0.12f, 0.12f, 1f));

        if (_progressFillTex == null)
            _progressFillTex = WorkbenchPopup.MakeTex(1, 1, new Color(0.2f, 0.65f, 0.3f, 1f));

        GUI.DrawTexture(rect, _progressBgTex);

        Rect fillRect = new Rect(rect.x, rect.y, rect.width * progress, rect.height);
        GUI.DrawTexture(fillRect, _progressFillTex);

        GUIStyle centered = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleCenter,
            fontStyle = FontStyle.Bold,
            normal = { textColor = Color.white }
        };

        GUI.Label(rect, $"{text} {(progress * 100f):F0}%", centered);
    }

    private static GUIStyle CreateCodeTextAreaStyle(bool readOnly)
    {
        if (_textAreaBgTex == null)
            _textAreaBgTex = WorkbenchPopup.MakeTex(1, 1, new Color(0.1f, 0.1f, 0.1f, 1f));

        return new GUIStyle(GUI.skin.textArea)
        {
            fontSize = 13,
            normal =
            {
                textColor = readOnly ? new Color(0.8f, 0.9f, 0.8f) : Color.white,
                background = _textAreaBgTex
            }
        };
    }
}