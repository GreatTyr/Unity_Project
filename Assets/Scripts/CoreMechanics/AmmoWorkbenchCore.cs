using UnityEngine;

/// <summary>
/// ядро верстака боеприпасов: доступ к складам по пр€мым ссылкам.
/// </summary>
public class AmmoWorkbenchCore : MonoBehaviour
{
    [Header("—сылки на хранилища (ѕр€мые)")]
    [SerializeField] private ResourcesStorage resourcesStorage;
    [SerializeField] private AmmoStorage ammoStorage;

    public ResourcesStorage ResourcesStorage => resourcesStorage;
    public AmmoStorage AmmoStorage => ammoStorage;

    public bool IsReady
    {
        get
        {
            return resourcesStorage != null && ammoStorage != null;
        }
    }

    public string GetReadyError()
    {
        if (resourcesStorage == null) return "Ќе назначен/недоступен склад ресурсов (ResourcesStorage).";
        if (ammoStorage == null) return "Ќе назначен/недоступен склад боеприпасов (AmmoStorage).";
        return "";
    }
}