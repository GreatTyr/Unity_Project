using System;
using UnityEngine;

[Serializable]
public struct ResourceCostPerLiter
{
    public ResourcesStorage.ResourceIndex resourceType;
    public float gramsPerLiter;
}