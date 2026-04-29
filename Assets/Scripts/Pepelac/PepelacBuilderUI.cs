using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Временный UI для режима строительства. 
/// Читает ModuleStorage и позволяет выбрать модуль для установки на сетку.
/// </summary>
public class PepelacBuilderUI : MonoBehaviour // ИСПРАВЛЕНО: Было PepelacGridBuilder : gridBuilder
{
    [Header("References")]
    [Tooltip("Отсюда берем скрафченные модули")]
    public ModuleStorage moduleStorage;

    // Сюда будем передавать выбранный модуль
    public PepelacGridBuilder gridBuilder; // ИСПРАВЛЕНО: Тип изменен с MonoBehaviour на PepelacGridBuilder

    private Rect windowRect;
    private Vector2 scrollPos;
    private bool isVisible = false;

    // Временная заглушка: просто храним код выбранного модуля, 
    // чтобы понимать, что мы кликнули по кнопке
    public string SelectedModuleCode { get; private set; }

    private void Awake()
    {
        // Панель справа
        windowRect = new Rect(Screen.width - 320, 50, 300, Screen.height - 100);
    }

    private void OnEnable()
    {
        isVisible = true;
        SelectedModuleCode = null;

        // Подстраховка на случай ресайза окна
        windowRect.x = Screen.width - 320;
        windowRect.height = Screen.height - 100;
    }

    private void OnDisable()
    {
        isVisible = false;
        SelectedModuleCode = null;
    }

    private void OnGUI()
    {
        if (!isVisible) return;

        windowRect = GUI.Window(889900, windowRect, DrawWindow, "Склад Модулей (Module Storage)");
    }

    private void DrawWindow(int id)
    {
        if (moduleStorage == null)
        {
            GUILayout.Label("<color=red>ModuleStorage не назначен!</color>");
            return;
        }

        if (moduleStorage.Count == 0)
        {
            GUILayout.Label("Склад модулей пуст.\nСкрафтите что-нибудь на верстаке.");
            return;
        }

        scrollPos = GUILayout.BeginScrollView(scrollPos);

        for (int i = 0; i < moduleStorage.Count; i++)
        {
            var entry = moduleStorage.GetEntryByIndex(i);
            var data = CraftedModule.DeserializeData(entry.dataTypeName, entry.json);

            if (data == null) continue;

            GUILayout.BeginVertical("box");

            // Заголовок
            GUILayout.Label($"<b>{data.moduleType} T{data.moduleTier}</b>", new GUIStyle(GUI.skin.label) { richText = true });

            // Инфо
            GUILayout.Label($"Фракция: {data.faction}");
            GUILayout.Label($"Габариты: {data.length:F1}x{data.width:F1}x{data.height:F1} м");
            GUILayout.Label($"В наличии: <b>{entry.quantity} шт.</b>", new GUIStyle(GUI.skin.label) { richText = true });

            // Кнопка "Взять в руки"
            Color oldColor = GUI.backgroundColor;
            if (SelectedModuleCode == entry.moduleCode)
                GUI.backgroundColor = Color.green; // Подсвечиваем выбранный

            if (GUILayout.Button(SelectedModuleCode == entry.moduleCode ? "ВЫБРАН" : "Выбрать для установки", GUILayout.Height(30)))
            {
                SelectModule(entry.moduleCode, data);
            }

            GUI.backgroundColor = oldColor;

            GUILayout.EndVertical();
            GUILayout.Space(5);
        }

        GUILayout.EndScrollView();

        // Кнопка отмены выбора в самом низу
        if (!string.IsNullOrEmpty(SelectedModuleCode))
        {
            GUILayout.Space(10);
            if (GUILayout.Button("ОТМЕНИТЬ ВЫБОР", GUILayout.Height(40)))
            {
                SelectModule(null, null);
            }
        }
    }

    private void SelectModule(string moduleCode, ModuleCommonData data)
    {
        SelectedModuleCode = moduleCode;
        Debug.Log($"[BuilderUI] Выбран модуль: {moduleCode}");

        if (gridBuilder != null)
        {
            gridBuilder.SetSelectedModule(data, moduleCode);
        }
        else
        {
            Debug.LogError("[BuilderUI] Не назначена ссылка на PepelacGridBuilder!");
        }
    }
}