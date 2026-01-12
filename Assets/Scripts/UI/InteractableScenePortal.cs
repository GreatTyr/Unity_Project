// InteractableScenePortal.cs
// Простой пример интерактивного объекта: при Interact() загружает указанную сцену.

using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;

public class InteractableScenePortal : InteractableBase
{
    [Header("Scene")]
    [Tooltip("Имя сцены для загрузки (указано в Build Settings)")]
    public string sceneToLoad;

    [Tooltip("Задержка перед загрузкой (в секундах) для проигрывания эффектов")]
    public float delayBeforeLoad = 0.3f;

    // Реализация активации — запускаем Coroutine для задержки
    public override void Interact()
    {
        if (string.IsNullOrEmpty(sceneToLoad))
        {
            Debug.LogWarning($"[{name}] sceneToLoad is empty.");
            return;
        }
        StartCoroutine(DoLoad());
    }

    IEnumerator DoLoad()
    {
        // Здесь можно добавить звук/анимацию/fade
        yield return new WaitForSeconds(delayBeforeLoad);
        SceneManager.LoadScene(sceneToLoad);
    }
}