using HarmonyLib;
using S1Forklift.Runtime;

#if IL2CPP
using S1LoadingDock = Il2CppScheduleOne.Delivery.LoadingDock;
#else
using S1LoadingDock = ScheduleOne.Delivery.LoadingDock;
#endif

namespace S1Forklift.Patches;

[HarmonyPatch(typeof(S1LoadingDock), "RefreshOccupant")]
internal static class LoadingDockPalletPatch
{
    [HarmonyPostfix]
    private static void Postfix(S1LoadingDock __instance)
    {
        LoadingDockPalletRuntime.Refresh(__instance);
    }
}
