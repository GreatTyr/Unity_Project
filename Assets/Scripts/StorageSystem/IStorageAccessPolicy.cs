using UnityEngine;

public interface IStorageAccessPolicy
{
    bool CanAccess(
        StorageNode node,
        StorageKind kind,
        StorageAccessMode mode,
        Transform actorTransform,
        out string reason);
}