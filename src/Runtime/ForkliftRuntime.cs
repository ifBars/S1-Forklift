using System;
using System.Collections.Generic;
using MelonLoader;
using UnityEngine;

#if IL2CPP
using S1LandVehicle = Il2CppScheduleOne.Vehicles.LandVehicle;
#else
using S1LandVehicle = ScheduleOne.Vehicles.LandVehicle;
#endif

namespace S1Forklift.Runtime;

internal static class ForkliftRuntime
{
    private sealed class State
    {
        internal State(S1LandVehicle vehicle, Transform fork, float minimumHeight)
        {
            Vehicle = vehicle;
            Fork = fork;
            MinimumHeight = minimumHeight;
        }

        internal S1LandVehicle Vehicle { get; }
        internal Transform Fork { get; }
        internal float MinimumHeight { get; }
    }

    private static readonly Dictionary<int, State> States = new();
    private static readonly List<int> DeadKeys = new();

    internal static void Attach(S1LandVehicle vehicle)
    {
        if (vehicle == null ||
            !string.Equals(vehicle.VehicleCode, Constants.VehicleCode, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var key = vehicle.GetInstanceID();
        if (States.ContainsKey(key))
            return;

        var visualRoot = TransformUtility.FindDescendant(vehicle.transform, Constants.VisualRootName);
        var fork = visualRoot == null ? null : TransformUtility.FindDirectChild(visualRoot, "Fork");
        if (fork == null)
        {
            MelonLogger.Warning("[S1Forklift] Spawned forklift is missing its fork carriage.");
            return;
        }

        States[key] = new State(vehicle, fork, fork.localPosition.y);
    }

    internal static void Update()
    {
        DeadKeys.Clear();
        foreach (var pair in States)
        {
            var state = pair.Value;
            if (state.Vehicle == null || state.Fork == null)
            {
                DeadKeys.Add(pair.Key);
                continue;
            }

            if (!state.Vehicle.LocalPlayerIsDriver)
                continue;

            var direction = 0f;
            if (Input.GetKey(ForkliftPreferences.RaiseLiftKey.Value))
                direction += 1f;
            if (Input.GetKey(ForkliftPreferences.LowerLiftKey.Value))
                direction -= 1f;
            if (direction == 0f)
                continue;

            Move(state, direction, Time.deltaTime);
        }

        for (var index = 0; index < DeadKeys.Count; index++)
            States.Remove(DeadKeys[index]);
    }

    internal static bool TryMoveForSmoke(
        S1LandVehicle vehicle,
        float direction,
        float deltaTime,
        out float height)
    {
        Attach(vehicle);
        if (!States.TryGetValue(vehicle.GetInstanceID(), out var state) || state.Fork == null)
        {
            height = 0f;
            return false;
        }

        Move(state, direction, deltaTime);
        height = state.Fork.localPosition.y;
        return true;
    }

    internal static Transform? GetFork(S1LandVehicle vehicle)
    {
        Attach(vehicle);
        return States.TryGetValue(vehicle.GetInstanceID(), out var state) ? state.Fork : null;
    }

    internal static bool TryGetLiftFraction(
        S1LandVehicle vehicle,
        out float fraction)
    {
        Attach(vehicle);
        if (!States.TryGetValue(vehicle.GetInstanceID(), out var state) ||
            state.Fork == null)
        {
            fraction = 0f;
            return false;
        }

        var maximumHeight =
            Math.Max(state.MinimumHeight, ForkliftPreferences.MaximumLiftHeight.Value);
        fraction = Mathf.InverseLerp(
            state.MinimumHeight,
            maximumHeight,
            state.Fork.localPosition.y);
        return true;
    }

    internal static void Prune()
    {
        DeadKeys.Clear();
        foreach (var pair in States)
        {
            if (pair.Value.Vehicle == null || pair.Value.Fork == null)
                DeadKeys.Add(pair.Key);
        }

        for (var index = 0; index < DeadKeys.Count; index++)
            States.Remove(DeadKeys[index]);
    }

    internal static void Clear()
    {
        States.Clear();
        DeadKeys.Clear();
    }

    private static void Move(State state, float direction, float deltaTime)
    {
        var position = state.Fork.localPosition;
        position.y = LiftMath.Advance(
            position.y,
            direction,
            deltaTime,
            ForkliftPreferences.LiftSpeed.Value,
            state.MinimumHeight,
            Math.Max(state.MinimumHeight, ForkliftPreferences.MaximumLiftHeight.Value));
        state.Fork.localPosition = position;
    }
}
