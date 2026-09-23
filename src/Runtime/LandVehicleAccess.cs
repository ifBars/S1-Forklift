using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

#if IL2CPP
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using S1LandVehicle = Il2CppScheduleOne.Vehicles.LandVehicle;
using S1VehicleSeat = Il2CppScheduleOne.Vehicles.VehicleSeat;
#else
using S1LandVehicle = ScheduleOne.Vehicles.LandVehicle;
using S1VehicleSeat = ScheduleOne.Vehicles.VehicleSeat;
#endif

namespace S1Forklift.Runtime;

internal static class LandVehicleAccess
{
    internal static void Configure(
        S1LandVehicle vehicle,
        GameObject visualRoot,
        S1VehicleSeat driverSeat,
        WheelCollider frontLeft,
        WheelCollider frontRight,
        WheelCollider rearLeft,
        WheelCollider rearRight)
    {
#if IL2CPP
        vehicle.vehicleName = Constants.VehicleName;
        vehicle.vehicleCode = Constants.VehicleCode;
        vehicle.vehiclePrice = Constants.VehiclePrice;
        vehicle.vehicleModel = visualRoot;
        vehicle.maxSteeringAngle = 38f;
        vehicle.steerRate = 70f;
        vehicle.flipSteer = true;
        vehicle.diffGearing = Constants.ForkliftDifferentialGearing;
        vehicle.motorTorque = ScaleMotorTorqueCurve(
            vehicle.motorTorque);
        vehicle.handBrakeForce = Constants.ForkliftHandBrakeForce;
        vehicle.BrakeForceMultiplier = Constants.ForkliftBrakeForceMultiplier;
        vehicle.downforce = 2f;
        vehicle.reverseMultiplier = 1f;

        var driveWheels = new Il2CppReferenceArray<WheelCollider>(2);
        driveWheels[0] = frontLeft;
        driveWheels[1] = frontRight;
        vehicle.driveWheels = driveWheels;

        var steerWheels = new Il2CppReferenceArray<WheelCollider>(2);
        steerWheels[0] = rearLeft;
        steerWheels[1] = rearRight;
        vehicle.steerWheels = steerWheels;

        var handbrakeWheels = new Il2CppReferenceArray<WheelCollider>(2);
        handbrakeWheels[0] = rearLeft;
        handbrakeWheels[1] = rearRight;
        vehicle.handbrakeWheels = handbrakeWheels;

        var seats = new Il2CppReferenceArray<S1VehicleSeat>(1);
        seats[0] = driverSeat;
        vehicle.Seats = seats;
#else
        SetField(vehicle, "vehicleName", Constants.VehicleName);
        SetField(vehicle, "vehicleCode", Constants.VehicleCode);
        SetField(vehicle, "vehiclePrice", Constants.VehiclePrice);
        SetField(vehicle, "vehicleModel", visualRoot);
        SetField(vehicle, "maxSteeringAngle", 38f);
        SetField(vehicle, "steerRate", 70f);
        SetField(vehicle, "flipSteer", true);
        SetField(vehicle, "diffGearing", Constants.ForkliftDifferentialGearing);
        SetField(
            vehicle,
            "motorTorque",
            ScaleMotorTorqueCurve(
                GetField<AnimationCurve>(vehicle, "motorTorque")));
        SetField(vehicle, "handBrakeForce", Constants.ForkliftHandBrakeForce);
        SetField(vehicle, "BrakeForceMultiplier", Constants.ForkliftBrakeForceMultiplier);
        SetField(vehicle, "downforce", 2f);
        SetField(vehicle, "reverseMultiplier", 1f);
        SetField(vehicle, "driveWheels", new[] { frontLeft, frontRight });
        SetField(vehicle, "steerWheels", new[] { rearLeft, rearRight });
        SetField(vehicle, "handbrakeWheels", new[] { rearLeft, rearRight });
        vehicle.Seats = new[] { driverSeat };
#endif

        vehicle.TopSpeed = Constants.ForkliftTopSpeed;
        vehicle.SpawnAsPlayerOwned = false;
        vehicle.Rb.mass = Constants.ForkliftMassKg;
        var centerOfMass = new Vector3(
            0f,
            Constants.ForkliftCenterOfMassHeightMeters,
            Constants.ForkliftCenterOfMassLongitudinalMeters);
        var centerOfMassTransform = vehicle.transform.Find("CenterOfMass");
        if (centerOfMassTransform != null)
            centerOfMassTransform.localPosition = centerOfMass;
        vehicle.Rb.centerOfMass = centerOfMass;
        vehicle.Rb.drag = Constants.ForkliftLinearDrag;
    }

    private static AnimationCurve ScaleMotorTorqueCurve(AnimationCurve curve)
    {
        if (curve == null)
            throw new InvalidOperationException("The base vehicle has no motor torque curve.");

        var keys = curve.keys;
        if (keys.Length == 0)
            throw new InvalidOperationException("The base vehicle motor torque curve has no keys.");

        var sampleTimes = new SortedSet<float>
        {
            Constants.ForkliftGovernorStartSpeedKmh,
            Constants.ForkliftTopSpeed,
        };
        for (var index = 0; index < keys.Length; index++)
            sampleTimes.Add(keys[index].time);
        for (var speedKmh = 0f;
             speedKmh <= Constants.ForkliftTopSpeed;
             speedKmh += 0.25f)
        {
            sampleTimes.Add(speedKmh);
        }

        var minimumTime = keys[0].time;
        var maximumTime = keys[keys.Length - 1].time;
        var shapedKeys = new Keyframe[sampleTimes.Count];
        var shapedIndex = 0;
        foreach (var time in sampleTimes)
        {
            var leftTime = Math.Max(minimumTime, time - 0.01f);
            var rightTime = Math.Min(maximumTime, time + 0.01f);
            var tangent = rightTime <= leftTime ||
                          time >= Constants.ForkliftTopSpeed
                ? 0f
                : (EvaluateTorque(curve, rightTime) - EvaluateTorque(curve, leftTime)) /
                  (rightTime - leftTime);
            shapedKeys[shapedIndex++] = new Keyframe(
                time,
                EvaluateTorque(curve, time),
                tangent,
                tangent);
        }

        var scaledCurve = new AnimationCurve(shapedKeys)
        {
            preWrapMode = curve.preWrapMode,
            postWrapMode = curve.postWrapMode,
        };
        return scaledCurve;
    }

    private static float EvaluateTorque(AnimationCurve curve, float speedKmh)
    {
        return DrivetrainMath.CalculateMotorCurveTorque(
            speedKmh,
            Constants.ForkliftMaximumDrawbarPullNewtons,
            Constants.ForkliftMaximumDrivePowerWatts,
            Constants.ForkliftDriveWheelRadiusMeters,
            Constants.ForkliftDifferentialGearing,
            Constants.ForkliftGovernorStartSpeedKmh,
            Constants.ForkliftTopSpeed);
    }

    internal static AnimationCurve GetMotorTorqueCurve(S1LandVehicle vehicle)
    {
#if IL2CPP
        return vehicle.motorTorque;
#else
        return GetField<AnimationCurve>(vehicle, "motorTorque");
#endif
    }

    internal static float GetBrakeForceMultiplier(S1LandVehicle vehicle)
    {
#if IL2CPP
        return vehicle.BrakeForceMultiplier;
#else
        return GetField<float>(vehicle, "BrakeForceMultiplier");
#endif
    }

    internal static AnimationCurve GetBrakeForceCurve(S1LandVehicle vehicle)
    {
#if IL2CPP
        return vehicle.brakeForce;
#else
        return GetField<AnimationCurve>(vehicle, "brakeForce");
#endif
    }

    internal static float GetDifferentialGearing(S1LandVehicle vehicle)
    {
#if IL2CPP
        return vehicle.diffGearing;
#else
        return GetField<float>(vehicle, "diffGearing");
#endif
    }

    internal static float GetHandBrakeForce(S1LandVehicle vehicle)
    {
#if IL2CPP
        return vehicle.handBrakeForce;
#else
        return GetField<float>(vehicle, "handBrakeForce");
#endif
    }

#if !IL2CPP
    private static T GetField<T>(S1LandVehicle vehicle, string name)
    {
        var field = AccessTools.Field(typeof(S1LandVehicle), name)
                    ?? throw new MissingFieldException(typeof(S1LandVehicle).FullName, name);
        return (T)(field.GetValue(vehicle)
                   ?? throw new InvalidOperationException($"Vehicle field '{name}' is null."));
    }

    private static void SetField<T>(S1LandVehicle vehicle, string name, T value)
    {
        var field = AccessTools.Field(typeof(S1LandVehicle), name)
                    ?? throw new MissingFieldException(typeof(S1LandVehicle).FullName, name);
        field.SetValue(vehicle, value);
    }
#endif
}
