using System;
using System.Collections.Generic;
using System.Text;
using HarmonyLib;
using MelonLoader;
using UnityEngine;

#if IL2CPP
using S1Guid = Il2CppSystem.Guid;
using S1GuidManager = Il2Cpp.GUIDManager;
using S1LandVehicle = Il2CppScheduleOne.Vehicles.LandVehicle;
using S1PlaceableStorageEntity = Il2CppScheduleOne.ObjectScripts.PlaceableStorageEntity;
using S1Player = Il2CppScheduleOne.PlayerScripts.Player;
using S1StorageEntityVisualizer = Il2CppScheduleOne.Storage.StorageEntityVisualizer;
#else
using S1Guid = System.Guid;
using S1GuidManager = GUIDManager;
using S1LandVehicle = ScheduleOne.Vehicles.LandVehicle;
using S1PlaceableStorageEntity = ScheduleOne.ObjectScripts.PlaceableStorageEntity;
using S1Player = ScheduleOne.PlayerScripts.Player;
using S1StorageEntityVisualizer = ScheduleOne.Storage.StorageEntityVisualizer;
#endif

namespace S1Forklift.Runtime;

internal static class PalletCarryRuntime
{
    private const float MaxSetDownDistance = 1.5f;
    private const float ForkDeckClearance = 0.0015f;

    private sealed class CarryState
    {
        internal S1LandVehicle Vehicle = null!;
        internal Transform Fork = null!;
        internal S1PlaceableStorageEntity Pallet = null!;
        internal Vector3 ForkLocalPosition;
        internal Quaternion ForkLocalRotation;
        internal readonly List<ColliderPair> IgnoredCollisions = new();
        internal float NextCollisionRefresh;
    }

    private sealed class DropState
    {
        internal S1PlaceableStorageEntity Pallet = null!;
        internal S1LandVehicle Vehicle = null!;
        internal Rigidbody Rigidbody = null!;
        internal readonly List<ColliderPair> IgnoredCollisions = new();
        internal float ReleasedAt;
        internal float StableSince = -1f;
    }

    private sealed class ColliderPair
    {
        internal Collider PalletCollider = null!;
        internal Collider VehicleCollider = null!;
    }

    private static readonly Dictionary<int, CarryState> CarriedByVehicle = new();
    private static readonly Dictionary<int, DropState> DroppingPallets = new();
    private static readonly List<S1PlaceableStorageEntity> KnownPallets = new();
    private static readonly Dictionary<string, PalletWorldPose> SavedWorldPoses =
        new(StringComparer.OrdinalIgnoreCase);
    private static readonly List<int> DeadVehicles = new();
    private static readonly System.Reflection.MethodInfo? ClearPositionDataMethod =
        AccessTools.Method(typeof(S1PlaceableStorageEntity).BaseType, "ClearPositionData");

    internal static void Update()
    {
        Prune();
        UpdateDrops();
        var player = S1Player.Local;
        var vehicle = player?.CurrentVehicle == null
            ? null
            : player.CurrentVehicle.GetComponent<S1LandVehicle>();
        if (vehicle == null ||
            !vehicle.LocalPlayerIsDriver ||
            !string.Equals(vehicle.VehicleCode, Constants.VehicleCode, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var vehicleKey = vehicle.GetInstanceID();
        if (CarriedByVehicle.TryGetValue(vehicleKey, out var carried))
        {
            SynchronizeCarriedPose(carried);
            if (Time.unscaledTime >= carried.NextCollisionRefresh)
                RefreshCarryCollisionIsolation(carried);
            UpdateSavedPose(carried.Pallet);
            if (ShouldRelease(carried))
                Release(carried);
            return;
        }

        if (!Input.GetKeyDown(ForkliftPreferences.RaiseLiftKey.Value))
            return;

        var fork = ForkliftRuntime.GetFork(vehicle);
        if (fork == null)
            return;

        var candidate = FindAlignedPallet(vehicle, fork);
        if (candidate != null)
            Attach(vehicle, fork, candidate);
    }

    internal static void LateUpdate()
    {
        foreach (var state in CarriedByVehicle.Values)
        {
            if (state.Pallet != null && state.Fork != null)
                SynchronizeCarriedPose(state);
        }
    }

    internal static void SynchronizeForSmoke(S1LandVehicle vehicle)
    {
        if (CarriedByVehicle.TryGetValue(vehicle.GetInstanceID(), out var state))
        {
            SynchronizeCarriedPose(state);
            Physics.SyncTransforms();
        }
    }

    internal static bool TryGetCarriedPallet(
        S1LandVehicle vehicle,
        out S1PlaceableStorageEntity pallet)
    {
        pallet = null!;
        if (vehicle == null)
            return false;

        if (CarriedByVehicle.TryGetValue(vehicle.GetInstanceID(), out var state) &&
            state.Pallet != null)
        {
            pallet = state.Pallet;
            return true;
        }

        var fork = ForkliftRuntime.GetFork(vehicle);
        if (fork == null)
            return false;

        for (var index = 0; index < KnownPallets.Count; index++)
        {
            var candidate = KnownPallets[index];
            if (candidate != null && candidate.transform.IsChildOf(fork))
            {
                pallet = candidate;
                return true;
            }
        }

        return false;
    }

    internal static void Prune()
    {
        DeadVehicles.Clear();
        foreach (var pair in CarriedByVehicle)
        {
            if (pair.Value.Vehicle == null || pair.Value.Pallet == null || pair.Value.Fork == null)
                DeadVehicles.Add(pair.Key);
        }

        foreach (var key in DeadVehicles)
        {
            if (CarriedByVehicle.TryGetValue(key, out var state))
                RestoreCollisionPairs(state.IgnoredCollisions);
            CarriedByVehicle.Remove(key);
        }

        DeadVehicles.Clear();
        foreach (var pair in DroppingPallets)
        {
            if (pair.Value.Pallet == null || pair.Value.Rigidbody == null)
                DeadVehicles.Add(pair.Key);
        }
        foreach (var key in DeadVehicles)
            DroppingPallets.Remove(key);
    }

    internal static void SetSavedWorldPoses(IEnumerable<PalletWorldPose>? poses)
    {
        SavedWorldPoses.Clear();
        if (poses == null)
            return;

        foreach (var pose in poses)
        {
            if (!string.IsNullOrWhiteSpace(pose.Guid))
                SavedWorldPoses[pose.Guid] = pose;
        }
    }

    internal static void RestoreSavedWorldPoses()
    {
        foreach (var pose in SavedWorldPoses.Values)
        {
            try
            {
                var pallet = S1GuidManager.GetObject<S1PlaceableStorageEntity>(
                    new S1Guid(pose.Guid));
                if (!IsPallet(pallet))
                    continue;

                RegisterPallet(pallet);
                ClearGridOccupancy(pallet);
                pallet.transform.SetParent(null, true);
                pallet.transform.position = new Vector3(pose.X, pose.Y, pose.Z);
                pallet.transform.rotation = Quaternion.Euler(0f, pose.RotationY, 0f);
                PalletPrefabFactory.RefreshPlacedTransitAccess(pallet);
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[S1Forklift] Could not restore pallet {pose.Guid}: {ex.Message}");
            }
        }
    }

    internal static void ClearSceneState()
    {
        foreach (var state in CarriedByVehicle.Values)
            RestoreCollisionPairs(state.IgnoredCollisions);
        foreach (var state in DroppingPallets.Values)
            RestoreDropCollisions(state);
        CarriedByVehicle.Clear();
        DroppingPallets.Clear();
        KnownPallets.Clear();
        DeadVehicles.Clear();
        ForkliftControlHintsRuntime.SetPalletState(false);
    }

    internal static void Clear()
    {
        ClearSceneState();
        SavedWorldPoses.Clear();
    }

    internal static bool TryPositionAndPickupForSmoke(
        S1LandVehicle vehicle,
        S1PlaceableStorageEntity pallet,
        out string detail)
    {
        var fork = ForkliftRuntime.GetFork(vehicle);
        if (fork == null || !TryGetForkSupportBounds(fork, out var forkBounds))
        {
            detail = "Fork bounds are unavailable.";
            return false;
        }

        ClearGridOccupancy(pallet);
        pallet.transform.SetParent(null, true);
        pallet.transform.rotation = Quaternion.Euler(0f, vehicle.transform.eulerAngles.y, 0f);
        var palletBounds = GetPalletBounds(pallet);
        var targetCenter = forkBounds.center + vehicle.transform.forward * 0.12f;
        pallet.transform.position += new Vector3(
            targetCenter.x - palletBounds.center.x,
            forkBounds.max.y + 0.025f - palletBounds.min.y,
            targetCenter.z - palletBounds.center.z);
        Physics.SyncTransforms();

        var candidate = FindAlignedPallet(vehicle, fork);
        if (candidate != pallet)
        {
            palletBounds = GetPalletBounds(pallet);
            var localDelta =
                vehicle.transform.InverseTransformPoint(palletBounds.center) -
                vehicle.transform.InverseTransformPoint(forkBounds.center);
            detail = candidate == null
                ? $"Aligned pallet search returned no candidate.|" +
                  $"IsPallet={IsPallet(pallet)}|Ghost={pallet.isGhost}|" +
                  $"Active={pallet.gameObject.activeInHierarchy}|" +
                  $"SceneValid={pallet.gameObject.scene.IsValid()}|" +
                  $"Accessor={pallet.StorageEntity.CurrentPlayerAccessor != null}|" +
                  $"ItemId={pallet.ItemInstance?.ID ?? "<null>"}|" +
                  $"DefinitionId={pallet.ItemInstance?.Definition?.ID ?? "<null>"}|" +
                  $"ForkTop={forkBounds.max.y:F3}|PalletBottom={palletBounds.min.y:F3}|" +
                  $"LocalDelta={localDelta}"
                : $"Aligned pallet search selected '{candidate.name}'.";
            return false;
        }

        Attach(vehicle, fork, pallet);
        detail = $"Guid={pallet.GUID}|Parent={pallet.transform.parent?.name}";
        return IsAlreadyCarried(pallet);
    }

    internal static bool TryReleaseForSmoke(S1LandVehicle vehicle, out string detail)
    {
        if (!CarriedByVehicle.TryGetValue(vehicle.GetInstanceID(), out var state))
        {
            detail = "The forklift is not carrying a pallet.";
            return false;
        }
        if (!CanRelease(state))
        {
            detail =
                $"No valid ground under pallet or speed is too high ({vehicle.Speed_Kmh:F2} km/h).|" +
                DescribeGroundProbe(state);
            return false;
        }

        Release(state);
        detail = $"Guid={state.Pallet.GUID}|Parent={state.Pallet.transform.parent?.name ?? "<world>"}";
        return !IsAlreadyCarried(state.Pallet);
    }

    internal static bool TryPickupSettledForSmoke(
        S1LandVehicle vehicle,
        S1PlaceableStorageEntity pallet,
        out string detail)
    {
        var fork = ForkliftRuntime.GetFork(vehicle);
        if (fork == null || !TryGetForkSupportBounds(fork, out var forkBounds))
        {
            detail = "Fork bounds are unavailable.";
            return false;
        }

        var candidate = FindAlignedPallet(vehicle, fork);
        if (candidate != pallet)
        {
            var palletBounds = GetPalletBounds(pallet);
            var localDelta =
                vehicle.transform.InverseTransformPoint(palletBounds.center) -
                vehicle.transform.InverseTransformPoint(forkBounds.center);
            var accessor = pallet.StorageEntity.CurrentPlayerAccessor;
            detail =
                $"Candidate={candidate?.name ?? "<none>"}|" +
                $"IsPallet={IsPallet(pallet)}|Ghost={pallet.isGhost}|" +
                $"Active={pallet.gameObject.activeInHierarchy}|" +
                $"Dropping={DroppingPallets.ContainsKey(pallet.GetInstanceID())}|" +
                $"Accessor={(accessor == null ? "<none>" : accessor == S1Player.Local ? "Local" : "Other")}|" +
                $"ForkTop={forkBounds.max.y:F3}|PalletBottom={palletBounds.min.y:F3}|" +
                $"LocalDelta={localDelta}";
            return false;
        }

        Attach(vehicle, fork, pallet);
        detail =
            $"Guid={pallet.GUID}|Parent={pallet.transform.parent?.name}|" +
            $"Accessor={(pallet.StorageEntity.CurrentPlayerAccessor == null ? "<none>" : "Local")}";
        return IsAlreadyCarried(pallet);
    }

    internal static bool IsSettledForSmoke(S1PlaceableStorageEntity pallet) =>
        pallet != null && !DroppingPallets.ContainsKey(pallet.GetInstanceID());

    internal static bool IsSettledPallet(S1PlaceableStorageEntity? pallet) =>
        IsPallet(pallet) &&
        pallet != null &&
        !pallet.isGhost &&
        pallet.gameObject.scene.IsValid() &&
        pallet.gameObject.activeInHierarchy &&
        !IsAlreadyCarried(pallet) &&
        !DroppingPallets.ContainsKey(pallet.GetInstanceID());

    internal static void PlaceAtWorldPose(
        S1PlaceableStorageEntity pallet,
        Vector3 position,
        Quaternion rotation)
    {
        ClearGridOccupancy(pallet);
        pallet.transform.SetParent(null, true);
        pallet.transform.SetPositionAndRotation(position, rotation);
        Physics.SyncTransforms();
        PalletPrefabFactory.RefreshPlacedTransitAccess(pallet);
        RegisterPallet(pallet);
        UpdateSavedPose(pallet);
    }

    internal static bool TryVerifyCollisionIsolationForSmoke(
        S1LandVehicle vehicle,
        S1PlaceableStorageEntity pallet,
        out string detail)
    {
        if (!CarriedByVehicle.TryGetValue(vehicle.GetInstanceID(), out var state) ||
            state.Pallet != pallet)
        {
            detail = "Pallet is not registered as carried by this forklift.";
            return false;
        }

        PalletPrefabFactory.DisableStoredDisplayColliders(pallet);
        RefreshCarryCollisionIsolation(state);
        var palletColliders = pallet.GetComponentsInChildren<Collider>(true);
        var vehicleColliders = vehicle.GetComponentsInChildren<Collider>(true);
        var enabledDisplayColliders = 0;
        var visualizer = pallet.GetComponent<S1StorageEntityVisualizer>();
        if (visualizer != null && visualizer.ItemContainer != null)
        {
            var displayColliders =
                visualizer.ItemContainer.GetComponentsInChildren<Collider>(true);
            for (var index = 0; index < displayColliders.Length; index++)
            {
                if (displayColliders[index].enabled)
                    enabledDisplayColliders++;
            }
        }

        var missingIgnoredPairs = 0;
        var expectedPairs = 0;
        for (var palletIndex = 0; palletIndex < palletColliders.Length; palletIndex++)
        {
            var palletCollider = palletColliders[palletIndex];
            if (palletCollider == null || !palletCollider.enabled || palletCollider.isTrigger)
                continue;
            for (var vehicleIndex = 0; vehicleIndex < vehicleColliders.Length; vehicleIndex++)
            {
                var vehicleCollider = vehicleColliders[vehicleIndex];
                if (vehicleCollider == null ||
                    !vehicleCollider.enabled ||
                    vehicleCollider.isTrigger ||
                    vehicleCollider.transform == pallet.transform ||
                    vehicleCollider.transform.IsChildOf(pallet.transform))
                {
                    continue;
                }
                expectedPairs++;
                if (!Physics.GetIgnoreCollision(palletCollider, vehicleCollider))
                    missingIgnoredPairs++;
            }
        }

        detail =
            $"Pairs={expectedPairs}|MissingIgnored={missingIgnoredPairs}|" +
            $"EnabledStoredVisualColliders={enabledDisplayColliders}";
        return expectedPairs > 0 &&
               missingIgnoredPairs == 0 &&
               enabledDisplayColliders == 0;
    }

    internal static bool TryGetSupportContactForSmoke(
        S1LandVehicle vehicle,
        S1PlaceableStorageEntity pallet,
        out float deckGap,
        out float visualBottomOffset,
        out string detail)
    {
        var fork = ForkliftRuntime.GetFork(vehicle);
        if (fork == null || !TryGetForkSupportBounds(fork, out var supportBounds))
        {
            deckGap = 0f;
            visualBottomOffset = 0f;
            detail = "Fork-prong support bounds are unavailable.";
            return false;
        }
        if (!TryGetPalletVisualBounds(pallet, out var visualBounds))
        {
            deckGap = 0f;
            visualBottomOffset = 0f;
            detail = "Pallet visual bounds are unavailable.";
            return false;
        }

        deckGap = GetPalletDeckSupportY(pallet) - supportBounds.max.y;
        visualBottomOffset = visualBounds.min.y - supportBounds.max.y;
        detail =
            $"TineTop={supportBounds.max.y:F3}|" +
            $"DeckBottom={GetPalletDeckSupportY(pallet):F3}|DeckGap={deckGap:F3}|" +
            $"VisualBottom={visualBounds.min.y:F3}|ForkInsertion={-visualBottomOffset:F3}";
        return true;
    }

    internal static void RegisterPallet(S1PlaceableStorageEntity? pallet)
    {
        if (!IsPallet(pallet))
            return;

        for (var index = 0; index < KnownPallets.Count; index++)
        {
            if (KnownPallets[index] == pallet)
                return;
        }

        KnownPallets.Add(pallet!);
    }

    private static S1PlaceableStorageEntity? FindAlignedPallet(
        S1LandVehicle vehicle,
        Transform fork)
    {
        if (!TryGetForkSupportBounds(fork, out var forkBounds))
            return null;

        RefreshKnownPallets();
        S1PlaceableStorageEntity? best = null;
        var bestDistance = float.MaxValue;
        for (var index = KnownPallets.Count - 1; index >= 0; index--)
        {
            var pallet = KnownPallets[index];
            if (pallet == null)
            {
                KnownPallets.RemoveAt(index);
                continue;
            }
            if (!IsPallet(pallet) || pallet!.isGhost || IsAlreadyCarried(pallet))
                continue;
            if (!pallet.gameObject.scene.IsValid() || !pallet.gameObject.activeInHierarchy)
                continue;
            var accessor = pallet.StorageEntity.CurrentPlayerAccessor;
            if (accessor != null && accessor != S1Player.Local)
                continue;

            var palletBounds = GetPalletBounds(pallet);
            var forkTop = forkBounds.max.y;
            if (palletBounds.min.y <
                    forkTop - Constants.PalletPickupLowerVerticalTolerance ||
                palletBounds.min.y > forkTop + 0.28f)
            {
                continue;
            }

            var localDelta = vehicle.transform.InverseTransformPoint(palletBounds.center) -
                             vehicle.transform.InverseTransformPoint(forkBounds.center);
            if (Math.Abs(localDelta.x) > 0.42f || Math.Abs(localDelta.z) > 0.65f)
                continue;

            var distance = localDelta.sqrMagnitude;
            if (distance < bestDistance)
            {
                best = pallet;
                bestDistance = distance;
            }
        }

        return best;
    }

    private static void RefreshKnownPallets()
    {
        var pallets = Resources.FindObjectsOfTypeAll<S1PlaceableStorageEntity>();
        for (var index = 0; index < pallets.Length; index++)
            RegisterPallet(pallets[index]);
    }

    private static void Attach(
        S1LandVehicle vehicle,
        Transform fork,
        S1PlaceableStorageEntity pallet)
    {
        ClearGridOccupancy(pallet);
        if (!TryGetForkSupportBounds(fork, out var forkBounds))
            return;
        var palletBounds = GetPalletBounds(pallet);
        var targetCenter = forkBounds.center + vehicle.transform.forward * 0.12f;
        var targetDeckBottom = forkBounds.max.y + ForkDeckClearance;

        pallet.transform.SetParent(fork, true);
        pallet.transform.rotation = Quaternion.Euler(0f, vehicle.transform.eulerAngles.y, 0f);
        pallet.transform.position += new Vector3(
            targetCenter.x - palletBounds.center.x,
            targetDeckBottom - GetPalletDeckSupportY(pallet),
            targetCenter.z - palletBounds.center.z);
        Physics.SyncTransforms();
        var rigidbody = pallet.GetComponent<Rigidbody>();
        if (rigidbody != null)
        {
            rigidbody.velocity = Vector3.zero;
            rigidbody.angularVelocity = Vector3.zero;
            rigidbody.isKinematic = true;
            rigidbody.interpolation = RigidbodyInterpolation.None;
            rigidbody.position = pallet.transform.position;
            rigidbody.rotation = pallet.transform.rotation;
            UnityEngine.Object.Destroy(rigidbody);
        }
        Physics.SyncTransforms();
        if (DroppingPallets.TryGetValue(pallet.GetInstanceID(), out var drop))
        {
            RestoreDropCollisions(drop);
            DroppingPallets.Remove(pallet.GetInstanceID());
        }

        CarriedByVehicle[vehicle.GetInstanceID()] = new CarryState
        {
            Vehicle = vehicle,
            Fork = fork,
            Pallet = pallet,
            ForkLocalPosition = pallet.transform.localPosition,
            ForkLocalRotation = pallet.transform.localRotation,
        };
        RefreshCarryCollisionIsolation(CarriedByVehicle[vehicle.GetInstanceID()]);
        ForkliftControlHintsRuntime.SetPalletState(true);
        UpdateSavedPose(pallet);
        MelonLogger.Msg($"[S1Forklift] Picked up pallet {pallet.GUID}.");
    }

    private static void SynchronizeCarriedPose(CarryState state)
    {
        state.Pallet.transform.localPosition = state.ForkLocalPosition;
        state.Pallet.transform.localRotation = state.ForkLocalRotation;

        var rigidbody = state.Pallet.GetComponent<Rigidbody>();
        if (rigidbody == null)
            return;

        rigidbody.position = state.Pallet.transform.position;
        rigidbody.rotation = state.Pallet.transform.rotation;
    }

    private static bool ShouldRelease(CarryState state)
    {
        if (!Input.GetKey(ForkliftPreferences.LowerLiftKey.Value))
            return false;
        return CanRelease(state);
    }

    private static bool CanRelease(CarryState state)
    {
        if (Math.Abs(state.Vehicle.Speed_Kmh) > 5f)
            return false;
        if (!ForkliftRuntime.TryGetLiftFraction(
                state.Vehicle,
                out var liftFraction) ||
            liftFraction > Constants.PalletReleaseMaximumLiftFraction)
        {
            return false;
        }

        var bounds = GetPalletBounds(state.Pallet);
        return HasValidGroundHit(
            state.Pallet,
            state.Vehicle,
            bounds,
            bounds.extents.y + MaxSetDownDistance);
    }

    private static string DescribeGroundProbe(CarryState state)
    {
        var bounds = GetPalletBounds(state.Pallet);
        var rayDistance = bounds.extents.y + MaxSetDownDistance;
        var hits = Physics.RaycastAll(
            bounds.center,
            Vector3.down,
            rayDistance,
            ~0,
            QueryTriggerInteraction.Ignore);
        var detail = new StringBuilder();
        detail.AppendFormat(
            "LiftFraction={0:F3}|ReleaseThreshold={1:F3}|" +
            "PalletBounds={2}/{3}|RayDistance={4:F3}|Hits={5}",
            ForkliftRuntime.TryGetLiftFraction(
                state.Vehicle,
                out var liftFraction)
                ? liftFraction
                : -1f,
            Constants.PalletReleaseMaximumLiftFraction,
            bounds.center,
            bounds.size,
            rayDistance,
            hits.Length);
        for (var index = 0; index < hits.Length; index++)
        {
            var hit = hits[index];
            detail.AppendFormat(
                "|H{0}={1}:{2:F3}:N{3}",
                index,
                hit.collider == null ? "<null>" : hit.collider.name,
                hit.distance,
                hit.normal);
        }

        return detail.ToString();
    }

    private static void Release(CarryState state)
    {
        RestoreCollisionPairs(state.IgnoredCollisions);
        state.Pallet.transform.SetParent(null, true);
        state.Pallet.transform.rotation =
            Quaternion.Euler(0f, state.Vehicle.transform.eulerAngles.y, 0f);
        var rigidbody = state.Pallet.GetComponent<Rigidbody>()
                        ?? state.Pallet.gameObject.AddComponent<Rigidbody>();
        rigidbody.mass = 50f;
        rigidbody.useGravity = true;
        rigidbody.detectCollisions = true;
        rigidbody.interpolation = RigidbodyInterpolation.Interpolate;
        rigidbody.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        rigidbody.constraints =
            RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
        rigidbody.isKinematic = false;
        rigidbody.WakeUp();

        CarriedByVehicle.Remove(state.Vehicle.GetInstanceID());
        ForkliftControlHintsRuntime.SetPalletState(false);
        var drop = new DropState
        {
            Pallet = state.Pallet,
            Vehicle = state.Vehicle,
            Rigidbody = rigidbody,
            ReleasedAt = Time.unscaledTime,
        };
        IgnoreVehicleCollisions(drop);
        DroppingPallets[state.Pallet.GetInstanceID()] = drop;
        UpdateSavedPose(state.Pallet);
        MelonLogger.Msg($"[S1Forklift] Released pallet {state.Pallet.GUID} to settle.");
    }

    private static void UpdateDrops()
    {
        DeadVehicles.Clear();
        foreach (var pair in DroppingPallets)
        {
            var state = pair.Value;
            if (state.Pallet == null || state.Rigidbody == null)
            {
                DeadVehicles.Add(pair.Key);
                continue;
            }

            UpdateSavedPose(state.Pallet);
            if (Time.unscaledTime - state.ReleasedAt < 0.15f)
                continue;

            var bounds = GetPalletBounds(state.Pallet);
            var grounded = HasValidGroundHit(
                state.Pallet,
                state.Vehicle,
                bounds,
                bounds.extents.y + 0.15f);
            var stable = grounded &&
                         state.Rigidbody.velocity.sqrMagnitude < 0.01f &&
                         state.Rigidbody.angularVelocity.sqrMagnitude < 0.01f;
            if (!stable)
            {
                state.StableSince = -1f;
                continue;
            }

            if (state.StableSince < 0f)
            {
                state.StableSince = Time.unscaledTime;
                continue;
            }
            if (Time.unscaledTime - state.StableSince < 0.25f)
                continue;

            state.Rigidbody.velocity = Vector3.zero;
            state.Rigidbody.angularVelocity = Vector3.zero;
            state.Rigidbody.isKinematic = true;
            RestoreDropCollisions(state);
            PalletPrefabFactory.RefreshPlacedTransitAccess(state.Pallet);
            UpdateSavedPose(state.Pallet);
            DeadVehicles.Add(pair.Key);
            MelonLogger.Msg($"[S1Forklift] Set down pallet {state.Pallet.GUID}.");
        }

        foreach (var key in DeadVehicles)
            DroppingPallets.Remove(key);
    }

    private static void IgnoreVehicleCollisions(DropState state)
    {
        IgnorePalletVehicleCollisions(
            state.Pallet,
            state.Vehicle,
            state.IgnoredCollisions);
    }

    private static void RefreshCarryCollisionIsolation(CarryState state)
    {
        PalletPrefabFactory.DisableStoredDisplayColliders(state.Pallet);
        IgnorePalletVehicleCollisions(
            state.Pallet,
            state.Vehicle,
            state.IgnoredCollisions);
        state.NextCollisionRefresh = Time.unscaledTime + 0.5f;
    }

    private static void IgnorePalletVehicleCollisions(
        S1PlaceableStorageEntity pallet,
        S1LandVehicle vehicle,
        List<ColliderPair> ignoredCollisions)
    {
        var palletColliders = pallet.GetComponentsInChildren<Collider>(true);
        var vehicleColliders = vehicle.GetComponentsInChildren<Collider>(true);
        for (var palletIndex = 0; palletIndex < palletColliders.Length; palletIndex++)
        {
            var palletCollider = palletColliders[palletIndex];
            if (palletCollider == null || !palletCollider.enabled || palletCollider.isTrigger)
                continue;

            for (var vehicleIndex = 0; vehicleIndex < vehicleColliders.Length; vehicleIndex++)
            {
                var vehicleCollider = vehicleColliders[vehicleIndex];
                if (vehicleCollider == null ||
                    !vehicleCollider.enabled ||
                    vehicleCollider.isTrigger ||
                    vehicleCollider.transform == pallet.transform ||
                    vehicleCollider.transform.IsChildOf(pallet.transform))
                {
                    continue;
                }

                var alreadyIgnored = false;
                for (var pairIndex = 0; pairIndex < ignoredCollisions.Count; pairIndex++)
                {
                    var existing = ignoredCollisions[pairIndex];
                    if (existing.PalletCollider == palletCollider &&
                        existing.VehicleCollider == vehicleCollider)
                    {
                        alreadyIgnored = true;
                        break;
                    }
                }
                if (alreadyIgnored)
                    continue;

                Physics.IgnoreCollision(palletCollider, vehicleCollider, true);
                ignoredCollisions.Add(new ColliderPair
                {
                    PalletCollider = palletCollider,
                    VehicleCollider = vehicleCollider,
                });
            }
        }
    }

    private static void RestoreDropCollisions(DropState state)
    {
        RestoreCollisionPairs(state.IgnoredCollisions);
    }

    private static void RestoreCollisionPairs(List<ColliderPair> collisionPairs)
    {
        for (var index = 0; index < collisionPairs.Count; index++)
        {
            var pair = collisionPairs[index];
            if (pair.PalletCollider != null && pair.VehicleCollider != null)
                Physics.IgnoreCollision(pair.PalletCollider, pair.VehicleCollider, false);
        }
        collisionPairs.Clear();
    }

    private static bool HasValidGroundHit(
        S1PlaceableStorageEntity pallet,
        S1LandVehicle? vehicle,
        Bounds bounds,
        float distance)
    {
        var hits = Physics.RaycastAll(
            bounds.center,
            Vector3.down,
            distance,
            ~0,
            QueryTriggerInteraction.Ignore);
        for (var index = 0; index < hits.Length; index++)
        {
            var hit = hits[index];
            if (hit.collider == null ||
                hit.collider.transform == pallet.transform ||
                hit.collider.transform.IsChildOf(pallet.transform) ||
                (vehicle != null &&
                 (hit.collider.transform == vehicle.transform ||
                  hit.collider.transform.IsChildOf(vehicle.transform))))
            {
                continue;
            }

            if (hit.normal.y >= 0.75f)
                return true;
        }

        return false;
    }

    private static void ClearGridOccupancy(S1PlaceableStorageEntity pallet)
    {
        if (ClearPositionDataMethod == null)
            throw new MissingMethodException("GridItem.ClearPositionData was not found.");
        ClearPositionDataMethod.Invoke(pallet, null);
    }

    internal static bool IsPallet(S1PlaceableStorageEntity? entity) =>
        entity != null &&
        entity.ItemInstance != null &&
        string.Equals(entity.ItemInstance.ID, Constants.PalletItemId, StringComparison.OrdinalIgnoreCase);

    private static bool IsAlreadyCarried(S1PlaceableStorageEntity pallet)
    {
        foreach (var state in CarriedByVehicle.Values)
        {
            if (state.Pallet == pallet)
                return true;
        }

        return false;
    }

    private static bool TryGetForkSupportBounds(Transform fork, out Bounds bounds)
    {
        var leftProng = TransformUtility.FindDirectChild(fork, "Fork");
        var rightProng = TransformUtility.FindDirectChild(fork, "Fork.001");
        var found = false;
        bounds = default;

        AddSupportBounds(leftProng, ref bounds, ref found);
        AddSupportBounds(rightProng, ref bounds, ref found);
        return found;
    }

    private static void AddSupportBounds(
        Transform? prong,
        ref Bounds bounds,
        ref bool found)
    {
        if (prong == null)
            return;

        var tine = TransformUtility.FindDirectChild(prong, "r") ?? prong;
        var colliders = tine.GetComponentsInChildren<Collider>(true);
        for (var index = 0; index < colliders.Length; index++)
        {
            var collider = colliders[index];
            if (collider == null || !collider.enabled || collider.isTrigger)
                continue;
            if (!found)
            {
                bounds = collider.bounds;
                found = true;
            }
            else
            {
                bounds.Encapsulate(collider.bounds);
            }
        }
    }

    private static bool TryGetPalletVisualBounds(
        S1PlaceableStorageEntity pallet,
        out Bounds bounds)
    {
        var visual = TransformUtility.FindDirectChild(pallet.transform, "PalletVisual");
        var renderers = visual?.GetComponentsInChildren<Renderer>(true);
        var found = false;
        bounds = default;
        if (renderers == null)
            return false;

        for (var index = 0; index < renderers.Length; index++)
        {
            var renderer = renderers[index];
            if (renderer == null || !renderer.enabled || !renderer.gameObject.activeInHierarchy)
                continue;
            if (!found)
            {
                bounds = renderer.bounds;
                found = true;
            }
            else
            {
                bounds.Encapsulate(renderer.bounds);
            }
        }

        return found;
    }

    private static float GetPalletDeckSupportY(S1PlaceableStorageEntity pallet) =>
        pallet.BoundingCollider != null
            ? pallet.BoundingCollider.bounds.min.y
            : GetPalletBounds(pallet).min.y;

    private static Bounds GetPalletBounds(S1PlaceableStorageEntity pallet)
    {
        var colliders = pallet.GetComponentsInChildren<Collider>(true);
        var found = false;
        var bounds = new Bounds(pallet.transform.position, Vector3.zero);
        foreach (var collider in colliders)
        {
            if (collider == null || !collider.enabled || collider.isTrigger)
                continue;
            if (!found)
            {
                bounds = collider.bounds;
                found = true;
            }
            else
            {
                bounds.Encapsulate(collider.bounds);
            }
        }

        return bounds;
    }

    private static void UpdateSavedPose(S1PlaceableStorageEntity pallet)
    {
        var key = pallet.GUID.ToString();
        var pose = new PalletWorldPose
        {
            Guid = key,
            X = pallet.transform.position.x,
            Y = pallet.transform.position.y,
            Z = pallet.transform.position.z,
            RotationY = pallet.transform.eulerAngles.y,
        };
        SavedWorldPoses[key] = pose;

        var save = PalletWorldSave.Instance;
        if (save == null)
            return;

        var list = save.Poses;
        var index = list.FindIndex(item =>
            string.Equals(item.Guid, key, StringComparison.OrdinalIgnoreCase));
        if (index >= 0)
            list[index] = pose;
        else
            list.Add(pose);
    }
}
