using System;
using UnityEngine;

[Serializable]
public struct OperationalResourceCostPerLiterPerSecond
{
    public ResourcesStorage.ResourceIndex resourceType;
    public float gramsPerLiterPerSecond;
}

[Serializable]
public struct OperationalResourceUsagePerSecond
{
    public ResourcesStorage.ResourceIndex resourceType;
    public float gramsPerSecond;
}