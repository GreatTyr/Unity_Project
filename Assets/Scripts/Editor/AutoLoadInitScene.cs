#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class AutoLoadInitScene
{
    private const string InitScenePath = "Assets/Scenes/_Init.unity";
    private const string PrefKey_PreviousScene = "AutoLoad_PreviousScene";
    private const string PrefKey_TargetScene = "AutoLoad_TargetScene";

    static AutoLoadInitScene()
    {
        EditorApplication.playModeStateChanged += OnPlayModeChanged;
    }

    private static void OnPlayModeChanged(PlayModeStateChange state)
    {
        switch (state)
        {
            case PlayModeStateChange.ExitingEditMode:
                HandleExitingEditMode();
                break;

            case PlayModeStateChange.EnteredPlayMode:
                HandleEnteredPlayMode();
                break;

            case PlayModeStateChange.EnteredEditMode:
                HandleEnteredEditMode();
                break;
        }
    }

    private static void HandleExitingEditMode()
    {
        // Запоминаем текущую сцену
        string currentPath = EditorSceneManager.GetActiveScene().path;
        string currentName = EditorSceneManager.GetActiveScene().name;

        EditorPrefs.SetString(PrefKey_PreviousScene, currentPath);
        EditorPrefs.SetString(PrefKey_TargetScene, currentName);

        // Если мы уже в _Init — ничего не делаем
        if (currentPath == InitScenePath)
        {
            // При запуске из _Init — не загружать другую сцену
            EditorPrefs.SetString(PrefKey_TargetScene, "");
            return;
        }

        // Переключаемся на _Init
        if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
        {
            EditorSceneManager.OpenScene(InitScenePath);
        }
        else
        {
            // Пользователь отменил сохранение — отменяем запуск
            EditorApplication.isPlaying = false;
        }
    }

    private static void HandleEnteredPlayMode()
    {
        // Читаем какую сцену нужно загрузить после _Init
        string target = EditorPrefs.GetString(PrefKey_TargetScene, "");

        if (!string.IsNullOrEmpty(target) && target != "_Init")
        {
            // Даём PersistentUI время инициализироваться (он в Awake),
            // затем загружаем целевую сцену
            SceneManager.LoadScene(target);
        }
    }

    private static void HandleEnteredEditMode()
    {
        // Возвращаемся в ту сцену, из которой нажали Play
        string previous = EditorPrefs.GetString(PrefKey_PreviousScene, "");

        if (!string.IsNullOrEmpty(previous) && previous != InitScenePath)
        {
            EditorSceneManager.OpenScene(previous);
        }
    }
}
#endif