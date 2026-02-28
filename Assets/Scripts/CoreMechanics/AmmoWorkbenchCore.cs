// AmmoWorkbenchCore.cs
using UnityEngine;

/// <summary>
/// Ядро верстака: хранит ссылки на склады.
/// В будущем — индивидуальные параметры верстака (уровень, модификаторы и т.д.).
/// Каждый экземпляр верстака в сцене имеет свой Core.
/// </summary>
public class AmmoWorkbenchCore : MonoBehaviour
{
    [Header("Склад ресурсов")]
    [SerializeField] private ResourcesStorage resourcesStorage;

    [Header("Склад боеприпасов")]
    [SerializeField] private AmmoStorage ammoStorage;

    public ResourcesStorage ResourcesStorage => resourcesStorage;
    public AmmoStorage AmmoStorage => ammoStorage;

    /// <summary>
    /// Проверка готовности верстака.
    /// </summary>
    public bool IsReady => resourcesStorage != null && ammoStorage != null;

    /// <summary>
    /// Сообщение если верстак не готов.
    /// </summary>
    public string GetReadyError()
    {
        if (resourcesStorage == null) return "Не назначен склад ресурсов (ResourcesStorage).";
        if (ammoStorage == null) return "Не назначен склад боеприпасов (AmmoStorage).";
        return "";
    }
}