using System;
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;

#if IL2CPP
using S1Avatar = Il2CppScheduleOne.AvatarFramework.Avatar;
using S1AvatarAnimation = Il2CppScheduleOne.AvatarFramework.Animation.AvatarAnimation;
using S1AvatarEquippable = Il2CppScheduleOne.AvatarFramework.Equipping.AvatarEquippable;
using S1LandVehicle = Il2CppScheduleOne.Vehicles.LandVehicle;
using S1Player = Il2CppScheduleOne.PlayerScripts.Player;
using S1VehicleSeat = Il2CppScheduleOne.Vehicles.VehicleSeat;
#else
using S1Avatar = ScheduleOne.AvatarFramework.Avatar;
using S1AvatarAnimation = ScheduleOne.AvatarFramework.Animation.AvatarAnimation;
using S1AvatarEquippable = ScheduleOne.AvatarFramework.Equipping.AvatarEquippable;
using S1LandVehicle = ScheduleOne.Vehicles.LandVehicle;
using S1Player = ScheduleOne.PlayerScripts.Player;
using S1VehicleSeat = ScheduleOne.Vehicles.VehicleSeat;
#endif

namespace S1Forklift.Runtime;

internal static class ForkliftDriverAvatarRuntime
{
    private sealed class SteeringRig
    {
        internal SteeringRig(
            S1Player player,
            S1LandVehicle vehicle,
            S1VehicleSeat seat,
            S1Avatar avatar,
            GameObject avatarProxy,
            Transform wheel,
            Quaternion wheelRestRotation,
            Transform leftUpperArm,
            Transform leftLowerArm,
            Transform leftHand,
            Transform rightUpperArm,
            Transform rightLowerArm,
            Transform rightHand,
            Transform leftHandTarget,
            Transform rightHandTarget)
        {
            Player = player;
            Vehicle = vehicle;
            Seat = seat;
            Avatar = avatar;
            AvatarProxy = avatarProxy;
            Wheel = wheel;
            WheelRestRotation = wheelRestRotation;
            LeftUpperArm = leftUpperArm;
            LeftLowerArm = leftLowerArm;
            LeftHand = leftHand;
            LeftLowerArmRestPosition = leftLowerArm.localPosition;
            LeftHandRestPosition = leftHand.localPosition;
            RightUpperArm = rightUpperArm;
            RightLowerArm = rightLowerArm;
            RightHand = rightHand;
            RightLowerArmRestPosition = rightLowerArm.localPosition;
            RightHandRestPosition = rightHand.localPosition;
            LeftHandTarget = leftHandTarget;
            RightHandTarget = rightHandTarget;
        }

        internal S1Player Player { get; }
        internal S1LandVehicle Vehicle { get; }
        internal S1VehicleSeat Seat { get; }
        internal S1Avatar Avatar { get; }
        internal GameObject AvatarProxy { get; }
        internal Transform Wheel { get; }
        internal Quaternion WheelRestRotation { get; }
        internal Transform LeftUpperArm { get; }
        internal Transform LeftLowerArm { get; }
        internal Transform LeftHand { get; }
        internal Vector3 LeftLowerArmRestPosition { get; }
        internal Vector3 LeftHandRestPosition { get; }
        internal Transform RightUpperArm { get; }
        internal Transform RightLowerArm { get; }
        internal Transform RightHand { get; }
        internal Vector3 RightLowerArmRestPosition { get; }
        internal Vector3 RightHandRestPosition { get; }
        internal Transform LeftHandTarget { get; }
        internal Transform RightHandTarget { get; }
        internal float LastLeftHandDistance { get; set; } = float.PositiveInfinity;
        internal float LastRightHandDistance { get; set; } = float.PositiveInfinity;
    }

    private const float DriverScale = 0.92f;
    private const float SittingOffset = -0.85f;
    private const float SteeringWheelRotation = 115f;
    private const float SteeringGripOffset = 0.13f;
    private const float SteeringGripDepth = 0.048f;
    private static readonly int SittingAnimation = Animator.StringToHash("Sitting");
    private static readonly Dictionary<int, SteeringRig> SteeringRigs = new();
    private static readonly List<int> DeadRigKeys = new();

    internal static bool IsForklift(S1LandVehicle? vehicle) =>
        vehicle != null &&
        string.Equals(vehicle.VehicleCode, Constants.VehicleCode, StringComparison.OrdinalIgnoreCase);

    internal static void Enter(
        S1Player player,
        S1LandVehicle vehicle,
        S1VehicleSeat? seat,
        bool networkVisibility)
    {
        _ = networkVisibility;
        if (!IsForklift(vehicle) || player?.Avatar == null)
            return;

        var targetSeat = seat ?? GetDriverSeat(vehicle);
        if (targetSeat != null)
            EnsureSteeringRig(player, vehicle, targetSeat);
    }

    internal static void Exit(S1Player player)
    {
        RemoveSteeringRig(player);
    }

    internal static void UpdateLocalPlayer()
    {
        UpdateSteeringRigs();

        var player = S1Player.Local;
        var currentVehicle =
            player?.CurrentVehicle == null
                ? null
                : player.CurrentVehicle.GetComponent<S1LandVehicle>();
        if (!IsForklift(currentVehicle))
        {
            if (player != null)
                RemoveSteeringRig(player);
            return;
        }

        var seat = player!.CurrentVehicleSeat ?? GetDriverSeat(currentVehicle!);
        if (seat != null)
            EnsureSteeringRig(player, currentVehicle!, seat);
    }

    internal static S1Avatar? GetDriverProxyAvatar(S1Player? player)
    {
        if (player == null)
            return null;

        return SteeringRigs.TryGetValue(player.GetInstanceID(), out var rig)
            ? rig.Avatar
            : null;
    }

    internal static bool IsDriverProxyEquippableHidden(S1Player? player)
    {
        var avatar = GetDriverProxyAvatar(player);
        if (avatar == null)
            return true;

        var equippables = avatar.GetComponentsInChildren<S1AvatarEquippable>(true);
        for (var index = 0; index < equippables.Length; index++)
        {
            if (equippables[index].gameObject.activeSelf)
                return false;
        }

        return true;
    }

    internal static bool TryGetAppliedHandDistances(
        S1Player? player,
        out float leftDistance,
        out float rightDistance)
    {
        if (player != null &&
            SteeringRigs.TryGetValue(player.GetInstanceID(), out var rig))
        {
            leftDistance = rig.LastLeftHandDistance;
            rightDistance = rig.LastRightHandDistance;
            return true;
        }

        leftDistance = float.PositiveInfinity;
        rightDistance = float.PositiveInfinity;
        return false;
    }

    internal static void LateUpdate()
    {
        foreach (var pair in SteeringRigs)
        {
            var rig = pair.Value;
            if (rig.AvatarProxy == null || rig.LeftHandTarget == null || rig.RightHandTarget == null)
                continue;

            SolveArms(rig);
        }
    }

    internal static void ApplyArmPose(S1AvatarAnimation animation)
    {
        foreach (var pair in SteeringRigs)
        {
            var rig = pair.Value;
            if (rig.AvatarProxy == null || rig.Avatar.Animation != animation)
                continue;

            SolveArms(rig);
            return;
        }
    }

    private static void EnsureSteeringRig(
        S1Player player,
        S1LandVehicle vehicle,
        S1VehicleSeat seat)
    {
        var key = player.GetInstanceID();
        if (SteeringRigs.TryGetValue(key, out var existing))
        {
            if (existing.Vehicle == vehicle)
                return;
            RemoveSteeringRig(player);
        }

        var visualRoot = TransformUtility.FindDescendant(vehicle.transform, Constants.VisualRootName);
        var steeringWheel = visualRoot == null
            ? null
            : TransformUtility.FindDescendant(visualRoot, "SteeringWheel");
        if (steeringWheel == null)
            return;

        var avatarProxy = Object.Instantiate(player.Avatar.gameObject, vehicle.transform, false);
        avatarProxy.name = "S1Forklift_DriverAvatarProxy";
        avatarProxy.hideFlags = HideFlags.DontSave;
        avatarProxy.SetActive(false);

        var avatar = avatarProxy.GetComponent<S1Avatar>();
        if (avatar == null)
        {
            Object.Destroy(avatarProxy);
            return;
        }

        var clonedEquippables = avatarProxy.GetComponentsInChildren<S1AvatarEquippable>(true);
        for (var index = 0; index < clonedEquippables.Length; index++)
            clonedEquippables[index].gameObject.SetActive(false);

        DisableProxyPhysics(avatarProxy);
        SetLayerRecursively(avatarProxy.transform, LayerMask.NameToLayer("Default"));
        avatarProxy.transform.localScale *= DriverScale;
        AlignAvatar(avatar.transform, seat.transform);
        avatarProxy.SetActive(true);
        avatar.SetVisible(true);
        if (avatar.LookController != null)
            avatar.LookController.enabled = false;

        var animator = AvatarAccess.GetAnimator(avatar.Animation);
        if (animator != null)
        {
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            animator.Rebind();
            animator.Update(0f);
            animator.SetBool(SittingAnimation, true);
            animator.Update(0f);
        }

        var leftUpperArm = animator?.GetBoneTransform(HumanBodyBones.LeftUpperArm);
        var leftLowerArm = animator?.GetBoneTransform(HumanBodyBones.LeftLowerArm);
        var leftHand = animator?.GetBoneTransform(HumanBodyBones.LeftHand);
        var rightUpperArm = animator?.GetBoneTransform(HumanBodyBones.RightUpperArm);
        var rightLowerArm = animator?.GetBoneTransform(HumanBodyBones.RightLowerArm);
        var rightHand = animator?.GetBoneTransform(HumanBodyBones.RightHand);
        if (leftUpperArm == null ||
            leftLowerArm == null ||
            leftHand == null ||
            rightUpperArm == null ||
            rightLowerArm == null ||
            rightHand == null)
        {
            Object.Destroy(avatarProxy);
            return;
        }

        var leftTarget = CreateHandTarget(
            steeringWheel,
            "S1Forklift_LeftHandTarget",
            new Vector3(-SteeringGripOffset, SteeringGripDepth, 0f),
            Quaternion.Euler(0f, 90f, -90f));
        var rightTarget = CreateHandTarget(
            steeringWheel,
            "S1Forklift_RightHandTarget",
            new Vector3(SteeringGripOffset, SteeringGripDepth, 0f),
            Quaternion.Euler(0f, -90f, 90f));

        AvatarAccess.GetIKController(avatar.Animation)?.SetIKActive(false);
        SteeringRigs[key] = new SteeringRig(
            player,
            vehicle,
            seat,
            avatar,
            avatarProxy,
            steeringWheel,
            steeringWheel.localRotation,
            leftUpperArm,
            leftLowerArm,
            leftHand,
            rightUpperArm,
            rightLowerArm,
            rightHand,
            leftTarget,
            rightTarget);
    }

    private static void DisableProxyPhysics(GameObject avatarProxy)
    {
        var colliders = avatarProxy.GetComponentsInChildren<Collider>(true);
        for (var index = 0; index < colliders.Length; index++)
            colliders[index].enabled = false;

        var rigidbodies = avatarProxy.GetComponentsInChildren<Rigidbody>(true);
        for (var index = 0; index < rigidbodies.Length; index++)
        {
            rigidbodies[index].isKinematic = true;
            rigidbodies[index].detectCollisions = false;
        }
    }

    private static void SetLayerRecursively(Transform root, int layer)
    {
        if (layer < 0)
            return;

        root.gameObject.layer = layer;
        for (var index = 0; index < root.childCount; index++)
            SetLayerRecursively(root.GetChild(index), layer);
    }

    private static void AlignAvatar(Transform avatar, Transform seat)
    {
        avatar.SetPositionAndRotation(
            seat.position + seat.up * SittingOffset,
            seat.rotation);
    }

    private static Transform CreateHandTarget(
        Transform parent,
        string name,
        Vector3 localPosition,
        Quaternion localRotation)
    {
        var target = new GameObject(name);
        target.hideFlags = HideFlags.HideAndDontSave;
        target.transform.SetParent(parent, false);
        target.transform.localPosition = localPosition;
        target.transform.localRotation = localRotation;
        return target.transform;
    }

    private static void UpdateSteeringRigs()
    {
        DeadRigKeys.Clear();
        foreach (var pair in SteeringRigs)
        {
            var rig = pair.Value;
            if (rig.Player == null ||
                rig.Vehicle == null ||
                rig.AvatarProxy == null ||
                rig.Wheel == null ||
                rig.LeftHandTarget == null ||
                rig.RightHandTarget == null)
            {
                DeadRigKeys.Add(pair.Key);
                continue;
            }

            var currentVehicle = rig.Player.CurrentVehicle == null
                ? null
                : rig.Player.CurrentVehicle.GetComponent<S1LandVehicle>();
            if (currentVehicle != rig.Vehicle)
            {
                ResetRig(rig);
                DeadRigKeys.Add(pair.Key);
                continue;
            }

            AlignAvatar(rig.Avatar.transform, rig.Seat.transform);

            var maximumAngle = Math.Max(1f, rig.Vehicle.ActualMaxSteeringAngle);
            var normalizedSteer = Mathf.Clamp(
                rig.Vehicle.CurrentSteerAngle / maximumAngle,
                -1f,
                1f);
            rig.Wheel.localRotation =
                rig.WheelRestRotation *
                Quaternion.AngleAxis(-normalizedSteer * SteeringWheelRotation, Vector3.up);
        }

        for (var index = 0; index < DeadRigKeys.Count; index++)
            SteeringRigs.Remove(DeadRigKeys[index]);
    }

    private static void SolveArm(
        Transform upperArm,
        Transform lowerArm,
        Transform hand,
        Transform target,
        Vector3 lowerArmRestPosition,
        Vector3 handRestPosition)
    {
        lowerArm.localPosition = lowerArmRestPosition;
        hand.localPosition = handRestPosition;

        var shoulder = upperArm.position;
        var upperLength = Vector3.Distance(shoulder, lowerArm.position);
        var lowerLength = Vector3.Distance(lowerArm.position, hand.position);
        var toTarget = target.position - shoulder;
        var targetDistance = Mathf.Clamp(
            toTarget.magnitude,
            Mathf.Abs(upperLength - lowerLength) + 0.001f,
            upperLength + lowerLength - 0.001f);
        if (toTarget.sqrMagnitude < 0.000001f)
            return;

        var targetDirection = toTarget.normalized;
        var bendNormal = Vector3.Cross(lowerArm.position - shoulder, targetDirection);
        if (bendNormal.sqrMagnitude < 0.000001f)
            bendNormal = Vector3.Cross(upperArm.up, targetDirection);
        bendNormal.Normalize();
        var bendDirection = Vector3.Cross(targetDirection, bendNormal).normalized;
        var elbowForward =
            (upperLength * upperLength -
             lowerLength * lowerLength +
             targetDistance * targetDistance) /
            (2f * targetDistance);
        var elbowSide = Mathf.Sqrt(Mathf.Max(
            0f,
            upperLength * upperLength - elbowForward * elbowForward));
        var elbowPosition =
            shoulder +
            targetDirection * elbowForward +
            bendDirection * elbowSide;

        RotateJointToward(upperArm, lowerArm.position, elbowPosition);
        RotateJointToward(lowerArm, hand.position, target.position);
        lowerArm.position = elbowPosition;
        hand.position = target.position;

        hand.rotation = target.rotation;
    }

    private static void SolveArms(SteeringRig rig)
    {
        SolveArm(
            rig.LeftUpperArm,
            rig.LeftLowerArm,
            rig.LeftHand,
            rig.LeftHandTarget,
            rig.LeftLowerArmRestPosition,
            rig.LeftHandRestPosition);
        SolveArm(
            rig.RightUpperArm,
            rig.RightLowerArm,
            rig.RightHand,
            rig.RightHandTarget,
            rig.RightLowerArmRestPosition,
            rig.RightHandRestPosition);
        rig.LastLeftHandDistance =
            Vector3.Distance(rig.LeftHand.position, rig.LeftHandTarget.position);
        rig.LastRightHandDistance =
            Vector3.Distance(rig.RightHand.position, rig.RightHandTarget.position);
    }

    private static void RotateJointToward(Transform joint, Vector3 handPosition, Vector3 targetPosition)
    {
        var currentDirection = handPosition - joint.position;
        var targetDirection = targetPosition - joint.position;
        if (currentDirection.sqrMagnitude < 0.000001f || targetDirection.sqrMagnitude < 0.000001f)
            return;

        joint.rotation =
            Quaternion.FromToRotation(currentDirection, targetDirection) *
            joint.rotation;
    }

    private static void RemoveSteeringRig(S1Player? player)
    {
        if (player == null)
            return;

        var key = player.GetInstanceID();
        if (!SteeringRigs.TryGetValue(key, out var rig))
            return;

        ResetRig(rig);
        SteeringRigs.Remove(key);
    }

    private static void ResetRig(SteeringRig rig)
    {
        if (rig.Wheel != null)
            rig.Wheel.localRotation = rig.WheelRestRotation;

        AvatarAccess.GetIKController(rig.Avatar?.Animation)?.SetIKActive(false);

        if (rig.LeftHandTarget != null)
            Object.Destroy(rig.LeftHandTarget.gameObject);
        if (rig.RightHandTarget != null)
            Object.Destroy(rig.RightHandTarget.gameObject);
        if (rig.AvatarProxy != null)
            Object.Destroy(rig.AvatarProxy);
    }

    private static S1VehicleSeat? GetDriverSeat(S1LandVehicle vehicle)
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
}
