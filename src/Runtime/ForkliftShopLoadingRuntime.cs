using System;
using MelonLoader;

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

namespace S1Forklift.Runtime;

internal static class ForkliftShopLoadingRuntime
{
    private const string VehicleOverflowWarning =
        "Vehicle won't fit everything. Some items will be placed on the pallets.";

    internal static void RemoveNativeVehicleStorage(S1LandVehicle vehicle)
    {
        if (!ForkliftDriverAvatarRuntime.IsForklift(vehicle) || vehicle.Storage == null)
            return;

        vehicle.Storage.SlotCount = 0;
        if (vehicle.Storage.ItemSlots.Count == 0)
            return;

        vehicle.Storage.ItemSlots.Clear();
        MelonLogger.Msg("[S1Forklift] Removed the forklift's inherited vehicle storage slots.");
    }

    internal static void FilterLoadingBayVehicle(ref S1LandVehicle? vehicle)
    {
        if (!ForkliftDriverAvatarRuntime.IsForklift(vehicle))
            return;

        if (!PalletCarryRuntime.TryGetCarriedPallet(vehicle!, out _))
            vehicle = null;
    }

    internal static void ReplaceAvailableVehicleSlots(
        S1ShopInterface shop,
        S1ItemSlotList availableSlots)
    {
        if (shop == null ||
            availableSlots == null ||
            shop.Cart == null ||
            shop.Cart.LoadVehicleToggle == null ||
            !shop.Cart.LoadVehicleToggle.isOn)
        {
            return;
        }

        var vehicle = shop.GetLoadingBayVehicle();
        if (!TryGetPalletSlots(vehicle, out var palletSlots))
            return;

        var nativeSlots = vehicle!.Storage?.ItemSlots;
        if (nativeSlots != null)
        {
            for (var index = nativeSlots.Count - 1; index >= 0; index--)
                availableSlots.Remove(nativeSlots[index]);
        }

        // Vehicle slots precede the shop's delivery bays in the native ordering.
        // Preserve that priority so purchases fill the carried pallet first.
        for (var index = palletSlots.Count - 1; index >= 0; index--)
            availableSlots.Insert(0, palletSlots[index]);
    }

    internal static bool TryGetWarning(
        S1Cart cart,
        out bool hasWarning,
        out string warning)
    {
        hasWarning = false;
        warning = string.Empty;

        if (cart == null ||
            cart.Shop == null ||
            cart.LoadVehicleToggle == null ||
            !cart.LoadVehicleToggle.isOn)
        {
            return false;
        }

        var vehicle = cart.Shop.GetLoadingBayVehicle();
        if (!TryGetPalletSlots(vehicle, out var palletSlots))
            return false;

        hasWarning = !cart.Shop.WillCartFit(palletSlots);
        if (hasWarning)
            warning = VehicleOverflowWarning;
        return true;
    }

    private static bool TryGetPalletSlots(
        S1LandVehicle? vehicle,
        out S1ItemSlotList palletSlots)
    {
        palletSlots = null!;
        if (!ForkliftDriverAvatarRuntime.IsForklift(vehicle) ||
            !PalletCarryRuntime.TryGetCarriedPallet(vehicle!, out var pallet) ||
            pallet.StorageEntity == null)
        {
            return false;
        }

        palletSlots = pallet.StorageEntity.ItemSlots;
        return true;
    }
}
