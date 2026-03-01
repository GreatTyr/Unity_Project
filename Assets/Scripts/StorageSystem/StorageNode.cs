using System;
using System.Collections.Generic;
using UnityEngine;

public enum StorageAccessGateMode
{
    None = 0,
    DistanceOnly = 1,
    InteractionOnly = 2,
    DistanceOrInteraction = 3,
    DistanceAndInteraction = 4
}

[Serializable]
public class StoragePermissionEntry
{
    public StorageKind kind = StorageKind.Resources;
    public StorageAccessMode allowedModes =
        StorageAccessMode.Read |
        StorageAccessMode.Write |
        StorageAccessMode.TransferIn |
        StorageAccessMode.TransferOut |
        StorageAccessMode.CraftConsume |
        StorageAccessMode.CraftProduce;
}

[DisallowMultipleComponent]
public class StorageNode : MonoBehaviour
{
    [Header("Identity")]
    [SerializeField] private string ownerId = "";
    [SerializeField] private StorageOwnerType ownerType = StorageOwnerType.WorldObject;
    [SerializeField] private bool isPrimaryForOwnerType = false;

    [Header("Availability")]
    [SerializeField] private bool nodeEnabled = true;

    [Header("Access Gate")]
    [SerializeField] private StorageAccessGateMode gateMode = StorageAccessGateMode.DistanceOnly;
    [SerializeField] private float maxUseDistance = 3.5f;
    [SerializeField] private bool interactionGranted = false;

    [Header("Storages")]
    [SerializeField] private ResourcesStorage resourcesStorage;
    [SerializeField] private AlloyStorage alloyStorage;
    [SerializeField] private ModuleStorage moduleStorage;
    [SerializeField] private AmmoStorage ammoStorage;

    [Header("Permissions")]
    [SerializeField]
    private List<StoragePermissionEntry> permissions = new List<StoragePermissionEntry>
    {
        new StoragePermissionEntry { kind = StorageKind.Resources },
        new StoragePermissionEntry { kind = StorageKind.Alloy },
        new StoragePermissionEntry { kind = StorageKind.Module },
        new StoragePermissionEntry { kind = StorageKind.Ammo }
    };

    private readonly Dictionary<StorageKind, StorageAccessMode> permissionMap = new Dictionary<StorageKind, StorageAccessMode>();

    public string OwnerId => ownerId;
    public StorageOwnerType OwnerType => ownerType;
    public bool IsPrimaryForOwnerType => isPrimaryForOwnerType;
    public bool NodeEnabled => nodeEnabled;
    public StorageAccessGateMode GateMode => gateMode;
    public float MaxUseDistance => maxUseDistance;
    public bool InteractionGranted => interactionGranted;

    public ResourcesStorage ResourcesStorage => resourcesStorage;
    public AlloyStorage AlloyStorage => alloyStorage;
    public ModuleStorage ModuleStorage => moduleStorage;
    public AmmoStorage AmmoStorage => ammoStorage;

    private void Awake()
    {
        AutoResolveMissingStorageRefs();
        RebuildPermissionMap();
    }

    private void OnEnable()
    {
        StorageManager.Instance?.Register(this);
    }

    private void OnDisable()
    {
        StorageManager.Instance?.Unregister(this);
    }

    private void OnValidate()
    {
        maxUseDistance = Mathf.Max(0f, maxUseDistance);
        AutoResolveMissingStorageRefs();
        RebuildPermissionMap();
    }

    public void SetInteractionGranted(bool value) => interactionGranted = value;
    public void GrantInteraction() => interactionGranted = true;
    public void RevokeInteraction() => interactionGranted = false;

    public bool HasStorage(StorageKind kind)
    {
        return GetStorageObject(kind) != null;
    }

    public object GetStorageObject(StorageKind kind)
    {
        switch (kind)
        {
            case StorageKind.Resources: return resourcesStorage;
            case StorageKind.Alloy: return alloyStorage;
            case StorageKind.Module: return moduleStorage;
            case StorageKind.Ammo: return ammoStorage;
            default: return null;
        }
    }

    public bool IsModeAllowed(StorageKind kind, StorageAccessMode requestedMode)
    {
        if (!permissionMap.TryGetValue(kind, out var allowed))
            return false;

        return (allowed & requestedMode) == requestedMode;
    }

    private void AutoResolveMissingStorageRefs()
    {
        if (resourcesStorage == null) resourcesStorage = GetComponent<ResourcesStorage>();
        if (alloyStorage == null) alloyStorage = GetComponent<AlloyStorage>();
        if (moduleStorage == null) moduleStorage = GetComponent<ModuleStorage>();
        if (ammoStorage == null) ammoStorage = GetComponent<AmmoStorage>();
    }

    private void RebuildPermissionMap()
    {
        permissionMap.Clear();
        for (int i = 0; i < permissions.Count; i++)
        {
            var p = permissions[i];
            permissionMap[p.kind] = p.allowedModes;
        }
    }
}