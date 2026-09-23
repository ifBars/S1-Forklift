using HarmonyLib;
using MelonLoader;
using S1Forklift.Runtime;

#if IL2CPP
using FishNetNetworkObject = Il2CppFishNet.Object.NetworkObject;
using S1AvatarAnimation = Il2CppScheduleOne.AvatarFramework.Animation.AvatarAnimation;
using S1LandVehicle = Il2CppScheduleOne.Vehicles.LandVehicle;
using S1Player = Il2CppScheduleOne.PlayerScripts.Player;
using S1VehicleSeat = Il2CppScheduleOne.Vehicles.VehicleSeat;
#else
using FishNetNetworkObject = FishNet.Object.NetworkObject;
using S1AvatarAnimation = ScheduleOne.AvatarFramework.Animation.AvatarAnimation;
using S1LandVehicle = ScheduleOne.Vehicles.LandVehicle;
using S1Player = ScheduleOne.PlayerScripts.Player;
using S1VehicleSeat = ScheduleOne.Vehicles.VehicleSeat;
#endif

namespace S1Forklift.Patches;

[HarmonyPatch(typeof(S1AvatarAnimation), "LateUpdate")]
internal static class ForkliftDriverArmPosePatch
{
    [HarmonyPostfix]
    private static void Postfix(S1AvatarAnimation __instance)
    {
        ForkliftDriverAvatarRuntime.ApplyArmPose(__instance);
    }
}

[HarmonyPatch(typeof(S1LandVehicle), "Start")]
internal static class AttachForkliftPatch
{
    [HarmonyPostfix]
    private static void Postfix(S1LandVehicle __instance)
    {
        try
        {
            ForkliftShopLoadingRuntime.RemoveNativeVehicleStorage(__instance);
            ForkliftRuntime.Attach(__instance);
        }
        catch (Exception ex)
        {
            MelonLogger.Warning($"[S1Forklift] Could not attach lift controls: {ex.Message}");
        }
    }
}

[HarmonyPatch(typeof(S1Player), nameof(S1Player.EnterVehicle))]
internal static class ShowForkliftDriverPatch
{
    [HarmonyPostfix]
    private static void Postfix(
        S1Player __instance,
        S1LandVehicle vehicle,
        S1VehicleSeat seat)
    {
        try
        {
            ForkliftDriverAvatarRuntime.Enter(
                __instance,
                vehicle,
                seat,
                networkVisibility: true);
        }
        catch (System.Exception ex)
        {
            MelonLogger.Warning($"[S1Forklift] Could not show the seated driver avatar: {ex.Message}");
        }

        if (__instance != S1Player.Local)
            return;

        try
        {
            ForkliftControlHintsRuntime.Enter(vehicle);
        }
        catch (System.Exception ex)
        {
            MelonLogger.Warning($"[S1Forklift] Could not show lift control hints: {ex.Message}");
        }
    }
}

[HarmonyPatch(typeof(S1Player), nameof(S1Player.ExitVehicle))]
internal static class ResetForkliftDriverAnimationPatch
{
    [HarmonyPostfix]
    private static void Postfix(S1Player __instance)
    {
        if (__instance == S1Player.Local)
            ForkliftControlHintsRuntime.Exit();
        ForkliftDriverAvatarRuntime.Exit(__instance);
    }
}

[HarmonyPatch(typeof(S1Player), "CurrentVehicleChanged")]
internal static class ShowRemoteForkliftDriverPatch
{
    [HarmonyPostfix]
    private static void Postfix(
        S1Player __instance,
        FishNetNetworkObject oldVeh,
        FishNetNetworkObject newVeh)
    {
        try
        {
            var newVehicle = newVeh == null ? null : newVeh.GetComponent<S1LandVehicle>();
            if (ForkliftDriverAvatarRuntime.IsForklift(newVehicle))
            {
                ForkliftDriverAvatarRuntime.Enter(
                    __instance,
                    newVehicle!,
                    null,
                    networkVisibility: false);
                return;
            }

            var oldVehicle = oldVeh == null ? null : oldVeh.GetComponent<S1LandVehicle>();
            if (ForkliftDriverAvatarRuntime.IsForklift(oldVehicle))
                ForkliftDriverAvatarRuntime.Exit(__instance);
        }
        catch (System.Exception ex)
        {
            MelonLogger.Warning($"[S1Forklift] Could not update a remote driver avatar: {ex.Message}");
        }
    }
}
