using UnityEngine;

/// <summary>
/// Статические утилиты для UI.
/// Общие методы, используемые несколькими UI-компонентами.
/// </summary>
public static class UIUtils
{
    /// <summary>
    /// Не позволяет RectTransform выйти за границы экрана.
    /// Сдвигает позицию так, чтобы все четыре угла оставались видимыми.
    /// </summary>
    public static void ClampToScreen(RectTransform rt)
    {
        if (rt == null) return;

        Vector3[] corners = new Vector3[4];
        rt.GetWorldCorners(corners);

        float screenW = Screen.width;
        float screenH = Screen.height;

        Vector3 pos = rt.position;

        // Правая граница
        if (corners[2].x > screenW)
            pos.x -= (corners[2].x - screenW);

        // Левая граница
        if (corners[0].x < 0)
            pos.x -= corners[0].x;

        // Нижняя граница
        if (corners[0].y < 0)
            pos.y -= corners[0].y;

        // Верхняя граница
        if (corners[2].y > screenH)
            pos.y -= (corners[2].y - screenH);

        rt.position = pos;
    }
}