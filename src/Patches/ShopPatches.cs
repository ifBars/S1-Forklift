using HarmonyLib;
using MelonLoader;
using S1Forklift.Runtime;

#if IL2CPP
using S1Cart = Il2CppScheduleOne.UI.Shop.Cart;
using S1ItemSlotList = Il2CppSystem.Collections.Generic.List<Il2CppScheduleOne.ItemFramework.ItemSlot>;
using S1LandVehicle = Il2CppScheduleOne.Vehicles.LandVehicle;
using S1ShopInterface = Il2CppScheduleOne.UI.Shop.ShopInterface;
#else
using S1Cart = ScheduleOne.UI.Shop.Cart;
using S1ItemSlotList = System.Collections.Generic.List<ScheduleOne.ItemFramework.ItemSlot>;
using S1LandVehicle = ScheduleOne.Vehicles.LandVehicle;
using S1ShopInterface = ScheduleOne.UI.Shop.ShopInterface;
#endif

namespace S1Forklift.Patches;

[HarmonyPatch(typeof(S1ShopInterface), nameof(S1ShopInterface.GetLoadingBayVehicle))]
internal static class ForkliftShopVehiclePatch
{
    [HarmonyPostfix]
    private static void Postfix(ref S1LandVehicle? __result)
    {
        ForkliftShopLoadingRuntime.FilterLoadingBayVehicle(ref __result);
    }
}

[HarmonyPatch(typeof(S1ShopInterface), nameof(S1ShopInterface.GetAvailableSlots))]
internal static class ForkliftShopAvailableSlotsPatch
{
    [HarmonyPostfix]
    private static void Postfix(S1ShopInterface __instance, S1ItemSlotList __result)
    {
        try
        {
            ForkliftShopLoadingRuntime.ReplaceAvailableVehicleSlots(__instance, __result);
        }
        catch (System.Exception ex)
        {
            MelonLogger.Error($"[S1Forklift] Could not route shop purchases to the carried pallet: {ex}");
        }
    }
}

[HarmonyPatch(typeof(S1Cart), "GetWarning")]
internal static class ForkliftShopCartWarningPatch
{
    [HarmonyPrefix]
    private static bool Prefix(S1Cart __instance, ref bool __result, ref string warning)
    {
        try
        {
            if (!ForkliftShopLoadingRuntime.TryGetWarning(
                    __instance,
                    out var hasWarning,
                    out var forkliftWarning))
            {
                return true;
            }

            __result = hasWarning;
            warning = forkliftWarning;
            return false;
        }
        catch (System.Exception ex)
        {
            MelonLogger.Error($"[S1Forklift] Could not evaluate carried-pallet shop capacity: {ex}");
            return true;
        }
    }
}
