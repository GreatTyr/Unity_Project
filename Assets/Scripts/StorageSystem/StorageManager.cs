using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class StorageManager : MonoBehaviour
{
    public static StorageManager Instance { get; private set; }

    [Header("Policy")]
    [SerializeField] private bool autoCreatePolicy = true;

    private IStorageAccessPolicy policy;
    private readonly List<StorageNode> nodes = new List<StorageNode>();

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        if (autoCreatePolicy)
            policy = new DefaultStorageAccessPolicy();
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    public void SetPolicy(IStorageAccessPolicy newPolicy)
    {
        policy = newPolicy;
    }

    public void Register(StorageNode node)
    {
        if (node == null) return;
        if (!nodes.Contains(node)) nodes.Add(node);
    }

    public void Unregister(StorageNode node)
    {
        if (node == null) return;
        nodes.Remove(node);
    }

    public StorageNode GetPrimaryNode(StorageOwnerType ownerType)
    {
        for (int i = 0; i < nodes.Count; i++)
        {
            var n = nodes[i];
            if (n == null || !n.isActiveAndEnabled) continue;
            if (n.OwnerType == ownerType && n.IsPrimaryForOwnerType)
                return n;
        }

        for (int i = 0; i < nodes.Count; i++)
        {
            var n = nodes[i];
            if (n == null || !n.isActiveAndEnabled) continue;
            if (n.OwnerType == ownerType)
                return n;
        }

        return null;
    }

    public bool TryGetResourcesStorage(
        StorageNode preferredNode,
        Transform actorTransform,
        StorageAccessMode mode,
        out ResourcesStorage storage,
        out string reason)
    {
        bool ok = TryResolveStorage(StorageKind.Resources, preferredNode, actorTransform, mode, out object obj, out reason);
        storage = ok ? obj as ResourcesStorage : null;
        return ok && storage != null;
    }

    public bool TryGetAlloyStorage(
        StorageNode preferredNode,
        Transform actorTransform,
        StorageAccessMode mode,
        out AlloyStorage storage,
        out string reason)
    {
        bool ok = TryResolveStorage(StorageKind.Alloy, preferredNode, actorTransform, mode, out object obj, out reason);
        storage = ok ? obj as AlloyStorage : null;
        return ok && storage != null;
    }

    public bool TryGetModuleStorage(
        StorageNode preferredNode,
        Transform actorTransform,
        StorageAccessMode mode,
        out ModuleStorage storage,
        out string reason)
    {
        bool ok = TryResolveStorage(StorageKind.Module, preferredNode, actorTransform, mode, out object obj, out reason);
        storage = ok ? obj as ModuleStorage : null;
        return ok && storage != null;
    }

    public bool TryGetAmmoStorage(
        StorageNode preferredNode,
        Transform actorTransform,
        StorageAccessMode mode,
        out AmmoStorage storage,
        out string reason)
    {
        bool ok = TryResolveStorage(StorageKind.Ammo, preferredNode, actorTransform, mode, out object obj, out reason);
        storage = ok ? obj as AmmoStorage : null;
        return ok && storage != null;
    }

    private bool TryResolveStorage(
        StorageKind kind,
        StorageNode preferredNode,
        Transform actorTransform,
        StorageAccessMode mode,
        out object storageObject,
        out string reason)
    {
        storageObject = null;
        reason = "Storage not found";

        if (policy == null)
            policy = new DefaultStorageAccessPolicy();

        // Приоритет: Local preferred -> Player -> Vehicle
        var orderedNodes = new List<StorageNode>(3);

        if (preferredNode != null)
            orderedNodes.Add(preferredNode);

        var playerNode = GetPrimaryNode(StorageOwnerType.Player);
        if (playerNode != null && playerNode != preferredNode)
            orderedNodes.Add(playerNode);

        var vehicleNode = GetPrimaryNode(StorageOwnerType.Vehicle);
        if (vehicleNode != null && vehicleNode != preferredNode && vehicleNode != playerNode)
            orderedNodes.Add(vehicleNode);

        string lastReason = "";

        for (int i = 0; i < orderedNodes.Count; i++)
        {
            var node = orderedNodes[i];
            if (node == null) continue;

            if (!policy.CanAccess(node, kind, mode, actorTransform, out string accessReason))
            {
                lastReason = accessReason;
                continue;
            }

            object obj = node.GetStorageObject(kind);
            if (obj == null)
            {
                lastReason = $"Node '{node.name}' has null {kind} storage";
                continue;
            }

            storageObject = obj;
            reason = "";
            return true;
        }

        reason = string.IsNullOrEmpty(lastReason) ? "No accessible storage in priority chain" : lastReason;
        return false;
    }
}