using HarmonyLib;
using S1Forklift.Runtime;
using UnityEngine;

#if IL2CPP
using S1CheckpointBehaviour = Il2CppScheduleOne.NPCs.Behaviour.CheckpointBehaviour;
using S1EStealthLevel = Il2CppScheduleOne.Product.Packaging.EStealthLevel;
using S1StorageDoorAnimation = Il2CppScheduleOne.Storage.StorageDoorAnimation;
#else
using S1CheckpointBehaviour = ScheduleOne.NPCs.Behaviour.CheckpointBehaviour;
using S1EStealthLevel = ScheduleOne.Product.Packaging.EStealthLevel;
using S1StorageDoorAnimation = ScheduleOne.Storage.StorageDoorAnimation;
#endif

namespace S1Forklift.Patches;

[HarmonyPatch(typeof(S1CheckpointBehaviour), "GetSearchPoint")]
internal static class ForkliftPalletSearchPointPatch
{
    [HarmonyPrefix]
    private static bool Prefix(S1CheckpointBehaviour __instance, ref Vector3 __result)
    {
        if (!PalletInteractionRuntime.TryGetPoliceSearchPoint(__instance, out var searchPoint))
            return true;

        __result = searchPoint;
        return false;
    }
}

[HarmonyPatch(typeof(S1CheckpointBehaviour), "DoesVehicleContainIllicitItems")]
internal static class ForkliftPalletContrabandPatch
{
    [HarmonyPostfix]
    private static void Postfix(S1CheckpointBehaviour __instance, ref bool __result)
    {
        if (__result || __instance?.CurrentSearchedVehicle == null)
            return;

        var maximumStealthLevel = __instance.Checkpoint == null
            ? (S1EStealthLevel)0
            : __instance.Checkpoint.MaxStealthLevel;
        __result = PalletInteractionRuntime.CarriedPalletContainsIllicitItems(
            __instance.CurrentSearchedVehicle,
            maximumStealthLevel);
    }
}

[HarmonyPatch(typeof(S1StorageDoorAnimation), nameof(S1StorageDoorAnimation.SetIsOpen))]
internal static class ForkliftPalletTrunkAnimationPatch
{
    [HarmonyPrefix]
    private static bool Prefix(S1StorageDoorAnimation __instance, bool open) =>
        !open || !PalletInteractionRuntime.ShouldSuppressPoliceTrunkOpen(__instance);
}

[HarmonyPatch(typeof(S1CheckpointBehaviour), nameof(S1CheckpointBehaviour.OnActiveTick))]
internal static class ForkliftPalletPoliceSearchTickPatch
{
    [HarmonyPrefix]
    private static void Prefix(S1CheckpointBehaviour __instance)
    {
        PalletInteractionRuntime.PreparePoliceSearchTick(__instance);
    }

    [HarmonyPostfix]
    private static void Postfix()
    {
        PalletInteractionRuntime.ClearPoliceSearchTick();
    }
}
