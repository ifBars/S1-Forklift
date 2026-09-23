using UnityEngine;

#if IL2CPP
using Il2CppInterop.Runtime.InteropTypes;
using S1CheckpointBehaviour = Il2CppScheduleOne.NPCs.Behaviour.CheckpointBehaviour;
using S1EStealthLevel = Il2CppScheduleOne.Product.Packaging.EStealthLevel;
using S1LegalStatus = Il2CppScheduleOne.Core.Items.Framework.ELegalStatus;
using S1LandVehicle = Il2CppScheduleOne.Vehicles.LandVehicle;
using S1LoadingDock = Il2CppScheduleOne.Delivery.LoadingDock;
using S1ProductItemInstance = Il2CppScheduleOne.Product.ProductItemInstance;
using S1StorageDoorAnimation = Il2CppScheduleOne.Storage.StorageDoorAnimation;
#else
using S1CheckpointBehaviour = ScheduleOne.NPCs.Behaviour.CheckpointBehaviour;
using S1EStealthLevel = ScheduleOne.Product.Packaging.EStealthLevel;
using S1LegalStatus = ScheduleOne.Core.Items.Framework.ELegalStatus;
using S1LandVehicle = ScheduleOne.Vehicles.LandVehicle;
using S1LoadingDock = ScheduleOne.Delivery.LoadingDock;
using S1ProductItemInstance = ScheduleOne.Product.ProductItemInstance;
using S1StorageDoorAnimation = ScheduleOne.Storage.StorageDoorAnimation;
#endif

namespace S1Forklift.Runtime;

internal static class PalletInteractionRuntime
{
    private static S1StorageDoorAnimation? _activePoliceSearchDoor;

    internal static void PositionLoadingDockAccessPoint(
        S1LoadingDock dock,
        Transform? preferredAccessPoint = null)
    {
        if (dock == null ||
            dock.DynamicOccupant == null ||
            dock.AccessPoints == null ||
            dock.AccessPoints.Length == 0 ||
            dock.AccessPoints[0] == null)
        {
            return;
        }

        var palletAccessPoint = preferredAccessPoint;
        if (palletAccessPoint == null &&
            !TryGetPalletAccessPoint(dock.DynamicOccupant, out palletAccessPoint))
        {
            return;
        }

        dock.AccessPoints[0].SetPositionAndRotation(
            palletAccessPoint.position,
            palletAccessPoint.rotation);
    }

    internal static bool TryGetPoliceSearchPoint(
        S1CheckpointBehaviour behaviour,
        out Vector3 searchPoint)
    {
        searchPoint = default;
        if (behaviour == null ||
            !PalletCarryRuntime.TryGetCarriedPallet(
                behaviour.CurrentSearchedVehicle,
                out var pallet))
        {
            return false;
        }

        PalletTransitRuntime.TryGetReachableAccessPoint(
            pallet,
            behaviour.Npc,
            out var reachableAccessPoint);
        if (reachableAccessPoint != null)
            return TrySetSearchPoint(reachableAccessPoint, out searchPoint);

        return TryGetPalletAccessPoint(
                   behaviour.CurrentSearchedVehicle,
                   out var fallbackAccessPoint) &&
               TrySetSearchPoint(fallbackAccessPoint, out searchPoint);
    }

    internal static bool CarriedPalletContainsIllicitItems(
        S1LandVehicle vehicle,
        S1EStealthLevel maximumStealthLevel)
    {
        if (!PalletCarryRuntime.TryGetCarriedPallet(vehicle, out var pallet))
            return false;

        var slots = pallet.StorageEntity.ItemSlots;
        for (var index = 0; index < slots.Count; index++)
        {
            var item = slots[index]?.ItemInstance;
            if (item == null)
                continue;

#if IL2CPP
            var product = item.TryCast<S1ProductItemInstance>();
#else
            var product = item as S1ProductItemInstance;
#endif
            if (product != null)
            {
                if (product.AppliedPackaging == null ||
                    (int)product.AppliedPackaging.StealthLevel <= (int)maximumStealthLevel)
                {
                    return true;
                }
            }
            else if (item.Definition.legalStatus != S1LegalStatus.Legal)
            {
                return true;
            }
        }

        return false;
    }

    internal static void PreparePoliceSearchTick(S1CheckpointBehaviour behaviour)
    {
        _activePoliceSearchDoor = null;
        var vehicle = behaviour?.CurrentSearchedVehicle;
        if (behaviour != null &&
            behaviour.IsSearching &&
            vehicle?.Trunk != null &&
            IsForklift(vehicle) &&
            PalletCarryRuntime.TryGetCarriedPallet(vehicle, out _))
        {
            _activePoliceSearchDoor = vehicle.Trunk;
        }
    }

    internal static void ClearPoliceSearchTick()
    {
        _activePoliceSearchDoor = null;
    }

    internal static bool ShouldSuppressPoliceTrunkOpen(S1StorageDoorAnimation door)
    {
        if (door == null || door != _activePoliceSearchDoor)
            return false;

        _activePoliceSearchDoor = null;
        return true;
    }

    internal static bool TryGetPalletAccessPoint(
        S1LandVehicle vehicle,
        out Transform accessPoint)
    {
        accessPoint = null!;
        if (!IsForklift(vehicle) ||
            !PalletCarryRuntime.TryGetCarriedPallet(vehicle, out var pallet) ||
            pallet.AccessPoints == null)
        {
            return false;
        }

        var bestForwardDistance = float.MinValue;
        for (var index = 0; index < pallet.AccessPoints.Length; index++)
        {
            var candidate = pallet.AccessPoints[index];
            if (candidate == null)
                continue;

            var forwardDistance = Vector3.Dot(
                candidate.position - vehicle.transform.position,
                vehicle.transform.forward);
            if (forwardDistance <= bestForwardDistance)
                continue;

            bestForwardDistance = forwardDistance;
            accessPoint = candidate;
        }

        return accessPoint != null;
    }

    private static bool IsForklift(S1LandVehicle vehicle) =>
        vehicle != null &&
        string.Equals(
            vehicle.VehicleCode,
            Constants.VehicleCode,
            System.StringComparison.OrdinalIgnoreCase);

    private static bool TrySetSearchPoint(Transform accessPoint, out Vector3 searchPoint)
    {
        searchPoint = accessPoint.position;
        return true;
    }
}
