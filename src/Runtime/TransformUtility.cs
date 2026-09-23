using System;
using UnityEngine;

namespace S1Forklift.Runtime;

internal static class TransformUtility
{
    internal static Transform? FindDescendant(Transform root, string name)
    {
        if (string.Equals(root.name, name, StringComparison.Ordinal))
            return root;

        for (var index = 0; index < root.childCount; index++)
        {
            var match = FindDescendant(root.GetChild(index), name);
            if (match != null)
                return match;
        }

        return null;
    }

    internal static Transform? FindDirectChild(Transform root, string name)
    {
        for (var index = 0; index < root.childCount; index++)
        {
            var child = root.GetChild(index);
            if (string.Equals(child.name, name, StringComparison.Ordinal))
                return child;
        }

        return null;
    }

    internal static Transform GetVehicleChildRoot(Transform transform, Transform vehicleRoot)
    {
        var current = transform;
        while (current.parent != null && current.parent != vehicleRoot)
            current = current.parent;

        return current;
    }

    internal static void CopyWorldPose(Transform source, Transform destination)
    {
        destination.position = source.position;
        destination.rotation = source.rotation;
    }

    internal static string NormalizeWheelName(string name) =>
        name.Replace("_", string.Empty)
            .Replace(" ", string.Empty)
            .ToLowerInvariant();
}
