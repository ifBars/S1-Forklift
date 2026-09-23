using UnityEngine;
using UnityEngine.AI;

#if IL2CPP
using S1ITransitEntity = Il2CppScheduleOne.Management.ITransitEntity;
using S1NavMeshUtility = Il2CppScheduleOne.DevUtilities.NavMeshUtility;
using S1Npc = Il2CppScheduleOne.NPCs.NPC;
using S1PlaceableStorageEntity = Il2CppScheduleOne.ObjectScripts.PlaceableStorageEntity;
#else
using S1ITransitEntity = ScheduleOne.Management.ITransitEntity;
using S1NavMeshUtility = ScheduleOne.DevUtilities.NavMeshUtility;
using S1Npc = ScheduleOne.NPCs.NPC;
using S1PlaceableStorageEntity = ScheduleOne.ObjectScripts.PlaceableStorageEntity;
#endif

namespace S1Forklift.Runtime;

internal static class PalletTransitRuntime
{
    internal static bool TryGetReachableAccessPoint(
        S1ITransitEntity entity,
        S1Npc npc,
        out Transform? accessPoint)
    {
        accessPoint = null;
        if (entity == null || npc == null)
            return false;

        var pallet = entity.LinkOrigin == null
            ? null
            : entity.LinkOrigin.GetComponentInParent<S1PlaceableStorageEntity>();
        if (!PalletCarryRuntime.IsPallet(pallet))
            return false;

        TryGetReachableAccessPoint(pallet!, npc, out accessPoint);
        return true;
    }

    internal static void TryGetReachableAccessPoint(
        S1PlaceableStorageEntity pallet,
        S1Npc npc,
        out Transform? accessPoint)
    {
        accessPoint = null;
        if (pallet == null || npc == null)
            return;

        var shortestPath = float.MaxValue;
        var accessPoints = pallet.AccessPoints;
        for (var index = 0; index < accessPoints.Length; index++)
        {
            var candidate = accessPoints[index];
            if (candidate == null ||
                !npc.Movement.CanGetTo(candidate.position, 1f, out NavMeshPath path))
            {
                continue;
            }

            var pathLength = path == null
                ? Vector3.Distance(npc.transform.position, candidate.position)
                : S1NavMeshUtility.GetPathLength(path);
            if (pathLength >= shortestPath)
                continue;

            shortestPath = pathLength;
            accessPoint = candidate;
        }

    }
}
