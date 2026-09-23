using System;
using UnityEngine;

namespace S1Forklift.Runtime;

internal static class ForkliftSpawnPlacement
{
    private const string TeleportLocationsName = "Teleport Locations";
    private static readonly Vector3 ClearanceHalfExtents = new(1.1f, 1.3f, 1.8f);
    private const float PlayerRadius = 0.38f;
    private const float PlayerHeight = 1.75f;

    internal static Transform? FindTeleportLocation(string locationName)
    {
        var container = GameObject.Find(TeleportLocationsName);
        if (container == null)
            return null;

        for (var index = 0; index < container.transform.childCount; index++)
        {
            var child = container.transform.GetChild(index);
            if (string.Equals(child.name, locationName, StringComparison.OrdinalIgnoreCase))
                return child;
        }

        return null;
    }

    internal static bool TryFindClearPose(
        Vector3 anchor,
        Vector3 preferredForward,
        float minimumDistance,
        out Vector3 position,
        out Quaternion rotation,
        out float groundDistance)
    {
        preferredForward.y = 0f;
        if (preferredForward.sqrMagnitude < 0.01f)
            preferredForward = Vector3.forward;
        preferredForward.Normalize();

        var right = Vector3.Cross(Vector3.up, preferredForward).normalized;
        rotation = Quaternion.LookRotation(preferredForward, Vector3.up);

        var distance = Mathf.Max(4f, minimumDistance);
        var candidates = new[]
        {
            preferredForward * distance,
            preferredForward * (distance + 4f),
            preferredForward * distance + right * 5f,
            preferredForward * distance - right * 5f,
            right * distance,
            -right * distance,
            -preferredForward * distance,
            preferredForward * (distance + 8f),
        };

        for (var index = 0; index < candidates.Length; index++)
        {
            var probe = anchor + candidates[index] + Vector3.up * 12f;
            var hits = Physics.RaycastAll(
                    probe,
                    Vector3.down,
                    30f,
                    Physics.DefaultRaycastLayers,
                    QueryTriggerInteraction.Ignore);
            var bestPoint = default(Vector3);
            var bestVerticalDistance = float.MaxValue;

            for (var hitIndex = 0; hitIndex < hits.Length; hitIndex++)
            {
                var hit = hits[hitIndex];
                if (hit.collider == null ||
                    Vector3.Dot(hit.normal, Vector3.up) < 0.9f ||
                    hit.point.y > anchor.y + 2f ||
                    hit.point.y < anchor.y - 4f)
                {
                    continue;
                }

                var clearanceCenter = hit.point + Vector3.up * (ClearanceHalfExtents.y + 0.15f);
                if (Physics.CheckBox(
                        clearanceCenter,
                        ClearanceHalfExtents,
                        rotation,
                        Physics.DefaultRaycastLayers,
                        QueryTriggerInteraction.Ignore))
                {
                    continue;
                }

                var verticalDistance = Math.Abs(hit.point.y - anchor.y);
                if (verticalDistance < bestVerticalDistance)
                {
                    bestVerticalDistance = verticalDistance;
                    bestPoint = hit.point;
                }
            }

            if (bestVerticalDistance == float.MaxValue)
            {
                continue;
            }

            position = bestPoint + Vector3.up * 0.02f;
            groundDistance = Vector3.Distance(anchor, bestPoint);
            return true;
        }

        position = default;
        groundDistance = 0f;
        return false;
    }

    internal static bool TryFindClearPlayerPose(
        Vector3 anchor,
        Vector3 preferredForward,
        out Vector3 position,
        out Quaternion rotation)
    {
        preferredForward.y = 0f;
        if (preferredForward.sqrMagnitude < 0.01f)
            preferredForward = Vector3.forward;
        preferredForward.Normalize();

        var right = Vector3.Cross(Vector3.up, preferredForward).normalized;
        rotation = Quaternion.LookRotation(preferredForward, Vector3.up);
        var offsets = new[]
        {
            Vector3.zero,
            preferredForward * 1.5f,
            -preferredForward * 1.5f,
            right * 1.5f,
            -right * 1.5f,
            preferredForward * 3f,
            -preferredForward * 3f,
            right * 3f,
            -right * 3f,
            preferredForward * 5f,
            -preferredForward * 5f,
            right * 5f,
            -right * 5f,
        };

        for (var offsetIndex = 0; offsetIndex < offsets.Length; offsetIndex++)
        {
            var probe = anchor + offsets[offsetIndex] + Vector3.up * 6f;
            var hits = Physics.RaycastAll(
                probe,
                Vector3.down,
                12f,
                Physics.DefaultRaycastLayers,
                QueryTriggerInteraction.Ignore);
            var bestPoint = default(Vector3);
            var bestVerticalDistance = float.MaxValue;

            for (var hitIndex = 0; hitIndex < hits.Length; hitIndex++)
            {
                var hit = hits[hitIndex];
                if (hit.collider == null ||
                    Vector3.Dot(hit.normal, Vector3.up) < 0.9f ||
                    hit.point.y > anchor.y + 2f ||
                    hit.point.y < anchor.y - 4f)
                {
                    continue;
                }

                var capsuleBottom =
                    hit.point + Vector3.up * (PlayerRadius + 0.05f);
                var capsuleTop =
                    hit.point + Vector3.up * (PlayerHeight - PlayerRadius);
                if (Physics.CheckCapsule(
                        capsuleBottom,
                        capsuleTop,
                        PlayerRadius,
                        Physics.DefaultRaycastLayers,
                        QueryTriggerInteraction.Ignore))
                {
                    continue;
                }

                var verticalDistance = Math.Abs(hit.point.y - anchor.y);
                if (verticalDistance < bestVerticalDistance)
                {
                    bestVerticalDistance = verticalDistance;
                    bestPoint = hit.point;
                }
            }

            if (bestVerticalDistance < float.MaxValue)
            {
                position = bestPoint + Vector3.up * 0.05f;
                return true;
            }
        }

        position = default;
        return false;
    }
}
