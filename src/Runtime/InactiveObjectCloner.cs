using System;
using UnityEngine;
using Object = UnityEngine.Object;

namespace S1Forklift.Runtime;

internal static class InactiveObjectCloner
{
    internal static GameObject CloneGameObject(GameObject source)
    {
        if (source == null)
            throw new ArgumentNullException(nameof(source));

        var stagingRoot = new GameObject("S1Forklift_InactiveCloneStaging");
        stagingRoot.SetActive(false);
        try
        {
            var clone = Object.Instantiate(source, stagingRoot.transform, false);
            clone.SetActive(false);
            clone.transform.SetParent(null, false);
            return clone;
        }
        finally
        {
            Object.DestroyImmediate(stagingRoot);
        }
    }
}
