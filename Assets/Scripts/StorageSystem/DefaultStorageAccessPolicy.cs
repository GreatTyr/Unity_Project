using UnityEngine;

public class DefaultStorageAccessPolicy : IStorageAccessPolicy
{
    public bool CanAccess(
        StorageNode node,
        StorageKind kind,
        StorageAccessMode mode,
        Transform actorTransform,
        out string reason)
    {
        reason = "";

        if (node == null)
        {
            reason = "StorageNode == null";
            return false;
        }

        if (!node.NodeEnabled)
        {
            reason = "StorageNode disabled";
            return false;
        }

        if (!node.HasStorage(kind))
        {
            reason = $"Node does not contain storage kind: {kind}";
            return false;
        }

        if (!node.IsModeAllowed(kind, mode))
        {
            reason = $"Access mode '{mode}' is not allowed for '{kind}'";
            return false;
        }

        if (!PassGate(node, actorTransform, out reason))
            return false;

        return true;
    }

    private bool PassGate(StorageNode node, Transform actorTransform, out string reason)
    {
        reason = "";

        bool distancePass = IsDistancePass(node, actorTransform);
        bool interactionPass = node.InteractionGranted;

        switch (node.GateMode)
        {
            case StorageAccessGateMode.None:
                return true;

            case StorageAccessGateMode.DistanceOnly:
                if (!distancePass) reason = "Distance check failed";
                return distancePass;

            case StorageAccessGateMode.InteractionOnly:
                if (!interactionPass) reason = "Interaction gate not granted";
                return interactionPass;

            case StorageAccessGateMode.DistanceOrInteraction:
                if (!(distancePass || interactionPass))
                    reason = "Distance/interaction gate failed";
                return distancePass || interactionPass;

            case StorageAccessGateMode.DistanceAndInteraction:
                if (!(distancePass && interactionPass))
                    reason = "Distance+interaction gate failed";
                return distancePass && interactionPass;

            default:
                reason = "Unknown gate mode";
                return false;
        }
    }

    private bool IsDistancePass(StorageNode node, Transform actorTransform)
    {
        if (node.MaxUseDistance <= 0f) return true;
        if (actorTransform == null) return false;

        float dist = Vector3.Distance(actorTransform.position, node.transform.position);
        return dist <= node.MaxUseDistance;
    }
}