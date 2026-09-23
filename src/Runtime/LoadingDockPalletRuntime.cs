using System;
using System.Collections.Generic;
using MelonLoader;

#if IL2CPP
using S1ItemSlot = Il2CppScheduleOne.ItemFramework.ItemSlot;
using S1ITransitEntity = Il2CppScheduleOne.Management.ITransitEntity;
using S1LandVehicle = Il2CppScheduleOne.Vehicles.LandVehicle;
using S1LoadingDock = Il2CppScheduleOne.Delivery.LoadingDock;
using S1Npc = Il2CppScheduleOne.NPCs.NPC;
using S1PlaceableStorageEntity = Il2CppScheduleOne.ObjectScripts.PlaceableStorageEntity;
#else
using S1ItemSlot = ScheduleOne.ItemFramework.ItemSlot;
using S1ITransitEntity = ScheduleOne.Management.ITransitEntity;
using S1LandVehicle = ScheduleOne.Vehicles.LandVehicle;
using S1LoadingDock = ScheduleOne.Delivery.LoadingDock;
using S1Npc = ScheduleOne.NPCs.NPC;
using S1PlaceableStorageEntity = ScheduleOne.ObjectScripts.PlaceableStorageEntity;
#endif

namespace S1Forklift.Runtime;

internal static class LoadingDockPalletRuntime
{
    private sealed class DockState
    {
        internal readonly List<S1ItemSlot> ExposedSlots = new();
        internal int ActivePalletId;
        internal UnityEngine.Transform? SelectedAccessPoint;
    }

    private static readonly Dictionary<int, DockState> States = new();

    internal static void Refresh(S1LoadingDock dock)
    {
        if (dock == null)
            return;

        var activePallet = GetEligiblePallet(dock);
        Synchronize(dock, activePallet);
        States.TryGetValue(dock.GetInstanceID(), out var state);
        PalletInteractionRuntime.PositionLoadingDockAccessPoint(
            dock,
            state?.SelectedAccessPoint);
    }

    internal static bool TryGetReachableAccessPoint(
        S1ITransitEntity entity,
        S1Npc npc,
        out UnityEngine.Transform? accessPoint)
    {
        accessPoint = null;
        if (entity?.LinkOrigin == null || npc == null)
            return false;

        var dock = entity.LinkOrigin.GetComponentInParent<S1LoadingDock>();
        if (dock == null)
            return false;

        var pallet = GetEligiblePallet(dock);
        if (pallet == null)
            return false;

        PalletTransitRuntime.TryGetReachableAccessPoint(pallet, npc, out var palletAccessPoint);
        if (palletAccessPoint == null)
            return true;

        if (States.TryGetValue(dock.GetInstanceID(), out var state))
            state.SelectedAccessPoint = palletAccessPoint;
        PalletInteractionRuntime.PositionLoadingDockAccessPoint(dock, palletAccessPoint);
        accessPoint = dock.AccessPoints[0];
        return true;
    }

    internal static bool TryVerifyForSmoke(
        S1LoadingDock dock,
        S1LandVehicle vehicle,
        out S1PlaceableStorageEntity pallet,
        out string detail)
    {
        pallet = null!;
        if (dock == null || vehicle == null)
        {
            detail = "Loading dock or vehicle is unavailable.";
            return false;
        }

        if (!IsForklift(vehicle) ||
            !PalletCarryRuntime.TryGetCarriedPallet(vehicle, out pallet))
        {
            detail = "The loading dock occupant is not a forklift carrying a pallet.";
            return false;
        }

        var palletSlots = pallet.StorageEntity.ItemSlots;
        var matchingSlots = 0;
        for (var index = 0; index < palletSlots.Count; index++)
        {
            if (dock.InputSlots.Contains(palletSlots[index]))
                matchingSlots++;
        }

        detail =
            $"Dock={dock.Name}|Vehicle={vehicle.VehicleCode}|" +
            $"Pallet={pallet.GUID}|InputSlots={dock.InputSlots.Count}|" +
            $"PalletSlots={palletSlots.Count}|MatchingSlots={matchingSlots}";
        return dock.InputSlots.Count == palletSlots.Count &&
               matchingSlots == palletSlots.Count;
    }

    internal static void Clear()
    {
        States.Clear();
    }

    private static S1PlaceableStorageEntity? GetEligiblePallet(S1LoadingDock dock)
    {
        var vehicle = dock.DynamicOccupant;
        if (vehicle == null ||
            dock.StaticOccupant != null ||
            Math.Abs(vehicle.Speed_Kmh) >= 2f ||
            !IsForklift(vehicle) ||
            !PalletCarryRuntime.TryGetCarriedPallet(vehicle, out var pallet))
        {
            return null;
        }

        return pallet;
    }

    private static bool IsForklift(S1LandVehicle vehicle)
    {
        return string.Equals(
            vehicle.VehicleCode,
            Constants.VehicleCode,
            StringComparison.OrdinalIgnoreCase);
    }

    private static void Synchronize(
        S1LoadingDock dock,
        S1PlaceableStorageEntity? activePallet)
    {
        var dockId = dock.GetInstanceID();
        if (!States.TryGetValue(dockId, out var state))
        {
            if (activePallet == null)
                return;

            state = new DockState();
            States.Add(dockId, state);
        }

        var activeSlots = activePallet?.StorageEntity?.ItemSlots;
        for (var index = state.ExposedSlots.Count - 1; index >= 0; index--)
        {
            var slot = state.ExposedSlots[index];
            var stillActive = activeSlots != null && activeSlots.Contains(slot);
            if (stillActive)
                continue;

            if (slot == null || slot.ActiveLock == null)
            {
                dock.InputSlots.Remove(slot);
                state.ExposedSlots.RemoveAt(index);
            }
            else if (!dock.InputSlots.Contains(slot))
            {
                // Keep an in-flight employee reservation reachable until the
                // native behaviour releases its slot lock.
                dock.InputSlots.Add(slot);
            }
        }

        if (activeSlots != null)
        {
            for (var index = 0; index < activeSlots.Count; index++)
            {
                var slot = activeSlots[index];
                if (!state.ExposedSlots.Contains(slot))
                    state.ExposedSlots.Add(slot);
                if (!dock.InputSlots.Contains(slot))
                    dock.InputSlots.Add(slot);
            }
        }

        var palletId = activePallet == null ? 0 : activePallet.GetInstanceID();
        if (state.ActivePalletId != palletId)
        {
            state.SelectedAccessPoint = null;
            if (activePallet != null)
            {
                MelonLogger.Msg(
                    $"[S1Forklift] {dock.Name} will receive employee deliveries " +
                    $"into pallet {activePallet.GUID}.");
            }
            else if (state.ActivePalletId != 0)
            {
                MelonLogger.Msg(
                    $"[S1Forklift] {dock.Name} no longer has a forklift pallet available.");
            }

            state.ActivePalletId = palletId;
        }

        if (activePallet == null && state.ExposedSlots.Count == 0)
            States.Remove(dockId);
    }
}
