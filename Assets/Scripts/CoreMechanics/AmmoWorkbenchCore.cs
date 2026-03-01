using UnityEngine;

/// <summary>
/// ядро верстака: доступ к складам через StorageManager с fallback.
/// ѕриоритет: Local -> Player -> Vehicle.
/// </summary>
public class AmmoWorkbenchCore : MonoBehaviour
{
    [Header("Legacy fallback (optional)")]
    [SerializeField] private ResourcesStorage resourcesStorage;
    [SerializeField] private AmmoStorage ammoStorage;

    [Header("Storage Manager integration")]
    [SerializeField] private bool useStorageManager = true;
    [SerializeField] private StorageNode localStorageNode;
    [SerializeField] private Transform actorTransform;

    public ResourcesStorage ResourcesStorage
    {
        get
        {
            if (TryResolveResources(out var rs)) return rs;
            return resourcesStorage; // legacy fallback
        }
    }

    public AmmoStorage AmmoStorage
    {
        get
        {
            if (TryResolveAmmo(out var a)) return a;
            return ammoStorage; // legacy fallback
        }
    }

    public bool IsReady
    {
        get
        {
            return ResourcesStorage != null && AmmoStorage != null;
        }
    }

    public string GetReadyError()
    {
        if (ResourcesStorage == null) return "Ќе назначен/недоступен склад ресурсов (ResourcesStorage).";
        if (AmmoStorage == null) return "Ќе назначен/недоступен склад боеприпасов (AmmoStorage).";
        return "";
    }

    private void Awake()
    {
        if (localStorageNode == null)
            localStorageNode = GetComponent<StorageNode>();

        if (actorTransform == null && PlayerLocator.PlayerObject != null)
            actorTransform = PlayerLocator.PlayerObject.transform;
    }

    private bool TryResolveResources(out ResourcesStorage result)
    {
        result = null;

        if (!useStorageManager) return false;
        if (StorageManager.Instance == null) return false;

        Transform actor = ResolveActorTransform();

        bool ok = StorageManager.Instance.TryGetResourcesStorage(
            localStorageNode,
            actor,
            StorageAccessMode.CraftConsume | StorageAccessMode.Read,
            out var storage,
            out _);

        if (ok && storage != null)
        {
            result = storage;
            return true;
        }

        return false;
    }

    private bool TryResolveAmmo(out AmmoStorage result)
    {
        result = null;

        if (!useStorageManager) return false;
        if (StorageManager.Instance == null) return false;

        Transform actor = ResolveActorTransform();

        bool ok = StorageManager.Instance.TryGetAmmoStorage(
            localStorageNode,
            actor,
            StorageAccessMode.CraftProduce | StorageAccessMode.Write,
            out var storage,
            out _);

        if (ok && storage != null)
        {
            result = storage;
            return true;
        }

        return false;
    }

    private Transform ResolveActorTransform()
    {
        if (actorTransform != null) return actorTransform;
        if (PlayerLocator.PlayerObject != null) return PlayerLocator.PlayerObject.transform;
        return null;
    }
}