using System;
using System.Collections.Generic;
using MelonLoader;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

#if IL2CPP
using FishNetInstanceFinder = Il2CppFishNet.InstanceFinder;
using FishNetNetworkObject = Il2CppFishNet.Object.NetworkObject;
using S1LandVehicle = Il2CppScheduleOne.Vehicles.LandVehicle;
using S1VehicleManager = Il2CppScheduleOne.Vehicles.VehicleManager;
using S1VehicleSeat = Il2CppScheduleOne.Vehicles.VehicleSeat;
using S1Wheel = Il2CppScheduleOne.Vehicles.Wheel;
#else
using FishNetInstanceFinder = FishNet.InstanceFinder;
using FishNetNetworkObject = FishNet.Object.NetworkObject;
using S1LandVehicle = ScheduleOne.Vehicles.LandVehicle;
using S1VehicleManager = ScheduleOne.Vehicles.VehicleManager;
using S1VehicleSeat = ScheduleOne.Vehicles.VehicleSeat;
using S1Wheel = ScheduleOne.Vehicles.Wheel;
#endif

namespace S1Forklift.Runtime;

internal static class ForkliftPrefabFactory
{
    private static S1LandVehicle? _prefab;
    private static bool _loggedReady;

    internal static bool TryEnsureRegistered(MelonLogger.Instance logger)
    {
        try
        {
            var manager = S1VehicleManager.Instance;
            var spawnables = FishNetInstanceFinder.NetworkManager?.SpawnablePrefabs;
            if (manager == null || spawnables == null)
                return false;

            var managerPrefab = manager.GetVehiclePrefab(Constants.VehicleCode);
            if (managerPrefab != null)
            {
                _prefab = managerPrefab;
                EnsureSpawnable(managerPrefab.GetComponent<FishNetNetworkObject>());
                LogReady(logger);
                return true;
            }

            if (_prefab == null)
                _prefab = BuildPrefab(manager);

            if (_prefab == null)
                return false;

            EnsureSpawnable(_prefab.GetComponent<FishNetNetworkObject>());
            if (!ContainsVehicle(manager, _prefab))
                manager.VehiclePrefabs.Add(_prefab);

            LogReady(logger);
            return true;
        }
        catch (Exception ex)
        {
            logger.Warning($"Forklift prefab registration is not ready: {ex.Message}");
            return false;
        }
    }

    internal static S1LandVehicle? GetPrefab() => _prefab;

    private static S1LandVehicle BuildPrefab(S1VehicleManager manager)
    {
        var source = manager.GetVehiclePrefab(Constants.BaseVehicleCode)
                     ?? throw new InvalidOperationException(
                         $"Base vehicle '{Constants.BaseVehicleCode}' is unavailable.");
        var template = Resources.Load<GameObject>(Constants.ForkliftResourcePath)
                       ?? throw new InvalidOperationException(
                           $"Game resource '{Constants.ForkliftResourcePath}' was not found.");

        GameObject? clone = null;
        try
        {
            clone = InactiveObjectCloner.CloneGameObject(source.gameObject);
            clone.name = Constants.PrefabName;
            clone.transform.position = new Vector3(0f, -1000f, 0f);
            clone.transform.rotation = Quaternion.identity;
            clone.hideFlags = HideFlags.HideAndDontSave;
            Object.DontDestroyOnLoad(clone);

            var vehicle = clone.GetComponent<S1LandVehicle>()
                          ?? throw new InvalidOperationException("The base vehicle clone has no LandVehicle.");

            ForkliftShopLoadingRuntime.RemoveNativeVehicleStorage(vehicle);

            var visualRoot = Object.Instantiate(template, clone.transform, false);
            visualRoot.name = Constants.VisualRootName;
            visualRoot.transform.localPosition = Vector3.zero;
            visualRoot.transform.localRotation = Quaternion.identity;

            HideNativePresentation(clone, visualRoot);
            PrepareTemplateColliders(visualRoot);
            ConfigureReferenceTransforms(vehicle, visualRoot.transform);
            ConfigureWheels(vehicle, visualRoot.transform, out var wheelMap);

            var driverSeat = FindDriverSeat(vehicle)
                             ?? throw new InvalidOperationException("The base vehicle has no driver seat.");

            LandVehicleAccess.Configure(
                vehicle,
                visualRoot,
                driverSeat,
                wheelMap["wheelfl"].wheelCollider,
                wheelMap["wheelfr"].wheelCollider,
                wheelMap["wheelrl"].wheelCollider,
                wheelMap["wheelrr"].wheelCollider);

            return vehicle;
        }
        catch
        {
            if (clone != null)
                Object.DestroyImmediate(clone);
            throw;
        }
    }

    private static void HideNativePresentation(GameObject clone, GameObject visualRoot)
    {
        var nativeInteractionRoot = TransformUtility.FindDescendant(clone.transform, "IntObj");
        var renderers = clone.GetComponentsInChildren<Renderer>(true);
        for (var index = 0; index < renderers.Length; index++)
        {
            if (!renderers[index].transform.IsChildOf(visualRoot.transform))
                renderers[index].enabled = false;
        }

        var sourceColliders = clone.GetComponentsInChildren<Collider>(true);
        for (var index = 0; index < sourceColliders.Length; index++)
        {
            var collider = sourceColliders[index];
            if (collider.transform.IsChildOf(visualRoot.transform))
                continue;

            if (collider is WheelCollider)
                continue;

            var wheel = collider.GetComponentInParent<S1Wheel>();
            var isInteraction =
                nativeInteractionRoot != null &&
                (collider.transform == nativeInteractionRoot ||
                 collider.transform.IsChildOf(nativeInteractionRoot));
            collider.enabled = wheel != null || isInteraction;
        }
    }

    private static void PrepareTemplateColliders(GameObject visualRoot)
    {
        var behaviours = visualRoot.GetComponentsInChildren<MonoBehaviour>(true);
        for (var index = 0; index < behaviours.Length; index++)
            behaviours[index].enabled = false;

        var bodyRoot = TransformUtility.FindDirectChild(visualRoot.transform, "forklift")
                       ?? throw new InvalidOperationException("Forklift visual body root is missing.");
        var forkRoot = TransformUtility.FindDirectChild(visualRoot.transform, "Fork")
                       ?? throw new InvalidOperationException("Forklift fork carriage is missing.");
        AddProngCollider(forkRoot, "Fork");
        AddProngCollider(forkRoot, "Fork.001");

        var colliders = visualRoot.GetComponentsInChildren<Collider>(true);
        for (var index = 0; index < colliders.Length; index++)
        {
            var collider = colliders[index];
            var belongsToBody = collider.transform == bodyRoot || collider.transform.IsChildOf(bodyRoot);
            var belongsToFork = collider.transform == forkRoot || collider.transform.IsChildOf(forkRoot);
            collider.enabled = collider is not WheelCollider &&
                               !collider.isTrigger &&
                               (belongsToBody || belongsToFork);
        }
    }

    private static void AddProngCollider(Transform forkRoot, string childName)
    {
        var prong = TransformUtility.FindDirectChild(forkRoot, childName)
                    ?? throw new InvalidOperationException($"Forklift prong '{childName}' is missing.");
        var existingColliders = prong.GetComponentsInChildren<Collider>(true);
        for (var index = 0; index < existingColliders.Length; index++)
        {
            if (!existingColliders[index].isTrigger)
                return;
        }

        if (prong.GetComponent<Collider>() != null)
            return;

        var renderer = prong.GetComponent<Renderer>()
                       ?? throw new InvalidOperationException(
                           $"Forklift prong '{childName}' has no renderer for collider bounds.");
        var collider = prong.gameObject.AddComponent<BoxCollider>();
        collider.center = renderer.localBounds.center;
        collider.size = renderer.localBounds.size;
    }

    private static void ConfigureReferenceTransforms(S1LandVehicle vehicle, Transform visualRoot)
    {
        ConfigureInteraction(vehicle, visualRoot);
        CopyNamedPose(visualRoot, "CenterOfMass", vehicle.transform, "CenterOfMass");
        CopyNamedPose(visualRoot, "CameraOrigin", vehicle.transform, "CameraOrigin");

        var sourceExitRoot = TransformUtility.FindDescendant(vehicle.transform, "ExitPoints");
        var templateExitRoot = TransformUtility.FindDescendant(visualRoot, "ExitPoints");
        if (sourceExitRoot != null && templateExitRoot != null)
        {
            var count = Math.Min(sourceExitRoot.childCount, templateExitRoot.childCount);
            for (var index = 0; index < count; index++)
                TransformUtility.CopyWorldPose(templateExitRoot.GetChild(index), sourceExitRoot.GetChild(index));
        }

        var driverSeat = FindDriverSeat(vehicle);
        var templateSeat = visualRoot.Find("Seats/GameObject");
        if (driverSeat != null && templateSeat != null)
        {
            var localPosition = visualRoot.InverseTransformPoint(templateSeat.position);
            var localRotation = Quaternion.Inverse(visualRoot.rotation) * templateSeat.rotation;
            driverSeat.transform.SetParent(vehicle.transform, false);
            driverSeat.transform.localPosition = localPosition;
            driverSeat.transform.localRotation = localRotation;
            driverSeat.transform.localScale = Vector3.one;
        }

        var templateBounds = TransformUtility.FindDescendant(visualRoot, "BoundingBox")
            ?.GetComponent<BoxCollider>();
        if (templateBounds != null && vehicle.boundingBox != null)
        {
            TransformUtility.CopyWorldPose(templateBounds.transform, vehicle.boundingBox.transform);
            vehicle.boundingBox.transform.localScale = templateBounds.transform.localScale;
            vehicle.boundingBox.center = templateBounds.center;
            vehicle.boundingBox.size = templateBounds.size;
            vehicle.boundingBox.isTrigger = true;
            vehicle.boundingBox.enabled = true;
        }
    }

    private static void ConfigureInteraction(S1LandVehicle vehicle, Transform visualRoot)
    {
        var sourceRoot = TransformUtility.FindDescendant(visualRoot, "IntObj")
                         ?? throw new InvalidOperationException("Forklift interaction root is missing.");
        var destinationRoot = TransformUtility.FindDescendant(vehicle.transform, "IntObj")
                              ?? throw new InvalidOperationException("Base vehicle interaction root is missing.");

        destinationRoot.SetParent(vehicle.transform, false);
        destinationRoot.localPosition = visualRoot.InverseTransformPoint(sourceRoot.position);
        destinationRoot.localRotation = Quaternion.Inverse(visualRoot.rotation) * sourceRoot.rotation;
        destinationRoot.localScale = sourceRoot.localScale;

        var sourceDisplay = TransformUtility.FindDirectChild(sourceRoot, "r")
                            ?? throw new InvalidOperationException("Forklift interaction display anchor is missing.");
        var destinationDisplay = TransformUtility.FindDirectChild(destinationRoot, "r")
                                 ?? throw new InvalidOperationException("Base interaction display anchor is missing.");
        destinationDisplay.localPosition = sourceDisplay.localPosition;
        destinationDisplay.localRotation = sourceDisplay.localRotation;
        destinationDisplay.localScale = sourceDisplay.localScale;

        var rootCollider = destinationRoot.GetComponent<BoxCollider>()
                           ?? throw new InvalidOperationException("Base interaction root collider is missing.");
        var displayCollider = destinationDisplay.GetComponent<BoxCollider>()
                              ?? throw new InvalidOperationException("Base interaction display collider is missing.");
        rootCollider.enabled = true;
        rootCollider.isTrigger = true;
        displayCollider.enabled = true;
        displayCollider.isTrigger = true;
    }

    private static void CopyNamedPose(
        Transform sourceRoot,
        string sourceName,
        Transform destinationRoot,
        string destinationName)
    {
        var source = TransformUtility.FindDescendant(sourceRoot, sourceName);
        var destination = TransformUtility.FindDescendant(destinationRoot, destinationName);
        if (source != null && destination != null)
            TransformUtility.CopyWorldPose(source, destination);
    }

    private static void ConfigureWheels(
        S1LandVehicle vehicle,
        Transform visualRoot,
        out Dictionary<string, S1Wheel> wheelMap)
    {
        wheelMap = new Dictionary<string, S1Wheel>(StringComparer.Ordinal);
        var wheels = vehicle.GetComponentsInChildren<S1Wheel>(true);
        for (var index = 0; index < wheels.Length; index++)
        {
            var wheel = wheels[index];
            var key = TransformUtility.NormalizeWheelName(wheel.transform.name);
            if (key is not ("wheelfl" or "wheelfr" or "wheelrl" or "wheelrr"))
                continue;

            var templateName = key switch
            {
                "wheelfl" => "Wheel_FL",
                "wheelfr" => "Wheel_FR",
                "wheelrl" => "Wheel_RL",
                _ => "Wheel_RR",
            };

            var visualWheel = TransformUtility.FindDirectChild(visualRoot, templateName)
                              ?? throw new InvalidOperationException(
                                  $"Forklift visual wheel '{templateName}' is missing.");

            var vehicleLocalPosition = vehicle.transform.InverseTransformPoint(visualWheel.position);
            wheel.transform.SetParent(vehicle.transform, false);
            wheel.transform.localPosition = vehicleLocalPosition;
            wheel.transform.localRotation = Quaternion.identity;
            wheel.transform.localScale = Vector3.one;
            ConfigureWheelColliderFromVisual(vehicle, visualWheel, wheel.wheelCollider);

            wheel.wheelModel = visualWheel;
            wheel.modelContainer = visualWheel;
            wheel.DriftParticlesEnabled = false;
            wheel.DriftAudioEnabled = false;
            wheelMap[key] = wheel;
        }

        foreach (var expected in new[] { "wheelfl", "wheelfr", "wheelrl", "wheelrr" })
        {
            if (!wheelMap.ContainsKey(expected))
                throw new InvalidOperationException($"Base vehicle wheel '{expected}' is missing.");
        }
    }

    private static void ConfigureWheelColliderFromVisual(
        S1LandVehicle vehicle,
        Transform visualWheel,
        WheelCollider destination)
    {
        var renderers = visualWheel.GetComponentsInChildren<MeshRenderer>(true);
        if (renderers.Length == 0)
            throw new InvalidOperationException($"Visual wheel '{visualWheel.name}' has no renderers.");

        var bounds = renderers[0].bounds;
        for (var index = 1; index < renderers.Length; index++)
            bounds.Encapsulate(renderers[index].bounds);

        destination.center = Vector3.zero;
        destination.radius = Mathf.Clamp(
            bounds.extents.y / Math.Max(0.0001f, Math.Abs(destination.transform.lossyScale.y)),
            0.12f,
            0.6f);
        var verticalScale = Math.Max(0.0001f, Math.Abs(destination.transform.lossyScale.y));
        var worldRadius = destination.radius * verticalScale;
        var wheelCenterHeight = Vector3.Dot(
            destination.transform.position - vehicle.transform.position,
            vehicle.transform.up);
        var requiredWorldSuspension = Mathf.Max(
            worldRadius * 0.5f,
            wheelCenterHeight - worldRadius + 0.05f);
        destination.suspensionDistance = Mathf.Clamp(
            requiredWorldSuspension / verticalScale,
            destination.radius * 0.5f,
            0.4f);

        var suspensionSpring = destination.suspensionSpring;
        suspensionSpring.spring = Constants.ForkliftWheelSpring;
        suspensionSpring.damper = Constants.ForkliftWheelSpringDamper;
        destination.suspensionSpring = suspensionSpring;
    }

    private static S1VehicleSeat? FindDriverSeat(S1LandVehicle vehicle)
    {
        if (vehicle.Seats == null)
            return null;

        for (var index = 0; index < vehicle.Seats.Length; index++)
        {
            var seat = vehicle.Seats[index];
            if (seat != null && seat.isDriverSeat)
                return seat;
        }

        return vehicle.Seats.Length > 0 ? vehicle.Seats[0] : null;
    }

    private static bool ContainsVehicle(S1VehicleManager manager, S1LandVehicle vehicle)
    {
        for (var index = 0; index < manager.VehiclePrefabs.Count; index++)
        {
            if (manager.VehiclePrefabs[index] == vehicle)
                return true;
        }

        return false;
    }

    private static void EnsureSpawnable(FishNetNetworkObject? networkObject)
    {
        if (networkObject == null)
            throw new InvalidOperationException("Forklift prefab has no FishNet NetworkObject.");

        var spawnables = FishNetInstanceFinder.NetworkManager?.SpawnablePrefabs
                         ?? throw new InvalidOperationException("FishNet spawnables are unavailable.");
        for (var index = 0; index < spawnables.GetObjectCount(); index++)
        {
            var candidate = spawnables.GetObject(true, index);
            if (candidate == networkObject ||
                (candidate?.gameObject != null &&
                 string.Equals(candidate.gameObject.name, networkObject.gameObject.name, StringComparison.Ordinal)))
            {
                return;
            }
        }

        spawnables.AddObject(networkObject);
    }

    private static void LogReady(MelonLogger.Instance logger)
    {
        if (_loggedReady)
            return;

        _loggedReady = true;
        logger.Msg(
            $"Registered native vehicle code '{Constants.VehicleCode}' from local game resource " +
            $"'{Constants.ForkliftResourcePath}'.");
    }
}
