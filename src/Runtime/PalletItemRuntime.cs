using System;
using MelonLoader;
using S1API.Items;
using S1API.Items.Buildable;
using S1API.Rendering;
using S1API.Shops;
using UnityEngine;
using Object = UnityEngine.Object;

#if IL2CPP
using S1BuildableItemDefinition = Il2CppScheduleOne.ItemFramework.BuildableItemDefinition;
using S1Registry = Il2CppScheduleOne.Registry;
#else
using S1BuildableItemDefinition = ScheduleOne.ItemFramework.BuildableItemDefinition;
using S1Registry = ScheduleOne.Registry;
#endif

namespace S1Forklift.Runtime;

internal static class PalletItemRuntime
{
    private static S1API.Items.Buildable.BuildableItemDefinition? _definition;
    private static bool _shopsRegistered;
    private static bool _iconGenerated;
    private static int _inheritedIconInstanceId;
    private static int _iconAttempts;
    private static float _nextIconAttempt;

    internal static void OnPreLoad()
    {
        try
        {
            EnsureRegistered();
        }
        catch (Exception ex)
        {
            MelonLogger.Error($"[S1Forklift] Pallet registration failed before load: {ex}");
        }
    }

    internal static void OnLoadComplete()
    {
        try
        {
            EnsureRegistered();
            RegisterHardwareStoreListings();
            ScheduleIconGeneration();
            PalletCarryRuntime.RestoreSavedWorldPoses();
        }
        catch (Exception ex)
        {
            MelonLogger.Error($"[S1Forklift] Pallet world setup failed: {ex}");
        }
    }

    internal static void OnPreSceneChange()
    {
        _shopsRegistered = false;
        _iconAttempts = 0;
        _nextIconAttempt = 0f;
        PalletCarryRuntime.ClearSceneState();
    }

    internal static void Update()
    {
        if (_iconGenerated ||
            _definition == null ||
            _iconAttempts >= 3 ||
            Time.unscaledTime < _nextIconAttempt)
        {
            return;
        }

        _iconAttempts++;
        _nextIconAttempt = Time.unscaledTime + 2f;
        TryGenerateIcon();
    }

    internal static void Clear()
    {
        _definition = null;
        _shopsRegistered = false;
        _iconGenerated = false;
        _inheritedIconInstanceId = 0;
        _iconAttempts = 0;
        _nextIconAttempt = 0f;
    }

    internal static bool TryVerify(out string detail)
    {
        var native = S1Registry.GetItem(Constants.PalletItemId) as S1BuildableItemDefinition;
        var shops = ShopManager.FindShopsByItem(Constants.PalletItemId);
        var customIcon =
            native?.Icon != null &&
            native.Icon.GetInstanceID() != _inheritedIconInstanceId;
        var prefabReady = PalletPrefabFactory.TryVerifyPrefab(out var prefabDetail);
        var heldReady = PalletHeldPresentationRuntime.TryVerify(
            native?.Equippable,
            out var heldDetail);
        detail =
            $"Registered={native != null}|BuiltPrefab={native?.BuiltItem != null}|" +
            $"CustomIcon={customIcon}|Shops={shops.Length}|{prefabDetail}|" +
            heldDetail;
        return native != null &&
               native.BuiltItem != null &&
               customIcon &&
               prefabReady &&
               heldReady &&
               shops.Length >= 2;
    }

    private static void EnsureRegistered()
    {
        if (S1Registry.Instance == null)
            return;

        if (!ItemManager.IsItemRegistered(Constants.PalletItemId))
        {
            var equippable =
                PalletHeldPresentationRuntime.GetOrCreateEquippable();
            _definition = S1API.Items.Buildable.BuildableItemCreator
                .CloneFrom(Constants.PalletSourceItemId)
                .WithBasicInfo(
                    Constants.PalletItemId,
                    "Storage Pallet",
                    "A movable wooden pallet for staging and transporting items.",
                    ItemCategory.Furniture)
                .WithPricing(60f, 0.5f)
                .WithStackLimit(5)
                .WithBuildSound(S1API.Items.Buildable.BuildSoundType.Wood)
                .WithEquippable(equippable)
                .Build();

            ItemManager.PreserveRuntimeItem(_definition);
        }
        else
        {
            _definition = ItemManager.GetDefinition(Constants.PalletItemId)
                as S1API.Items.Buildable.BuildableItemDefinition;
        }

        var native = S1Registry.GetItem(Constants.PalletItemId) as S1BuildableItemDefinition;
        if (native == null)
            throw new InvalidOperationException("The registered pallet item is not buildable.");

        if (_inheritedIconInstanceId == 0 && native.Icon != null)
            _inheritedIconInstanceId = native.Icon.GetInstanceID();
        native.BuiltItem = PalletPrefabFactory.GetOrCreate();
    }

    private static void ScheduleIconGeneration()
    {
        if (_iconGenerated)
            return;

        _iconAttempts = 0;
        _nextIconAttempt = Time.unscaledTime + 0.5f;
    }

    private static void TryGenerateIcon()
    {
        if (_definition == null)
            return;

        GameObject? iconSource = null;
        try
        {
            iconSource = PalletPrefabFactory.CreateIconSource();
            var icon = IconFactory.GenerateIconSprite(
                iconSource.transform,
                size: 512,
                bakeSkinnedMeshes: true,
                fitToCamera: true,
                cameraFill: 0.8f);
            if (icon == null)
            {
                MelonLogger.Warning(
                    "[S1Forklift] Pallet icon generation is not ready; it will retry.");
                return;
            }

            _definition.Icon = icon;
            _iconGenerated = true;
            MelonLogger.Msg("[S1Forklift] Generated the Storage Pallet inventory icon.");
        }
        catch (Exception ex)
        {
            MelonLogger.Warning(
                $"[S1Forklift] Pallet icon generation failed and will retry: {ex.Message}");
        }
        finally
        {
            if (iconSource != null)
                Object.Destroy(iconSource);
        }
    }

    private static void RegisterHardwareStoreListings()
    {
        if (_shopsRegistered || _definition == null)
            return;

        var added = ShopManager.AddToShops(
            _definition,
            "Handy Hank's Hardware",
            "Dan's Hardware");
        _shopsRegistered = added > 0 ||
                           ShopManager.FindShopsByItem(Constants.PalletItemId).Length > 0;

        if (_shopsRegistered)
            MelonLogger.Msg("[S1Forklift] Storage Pallet is available at both hardware stores.");
        else
            MelonLogger.Warning("[S1Forklift] Hardware stores were loaded, but no pallet listing was added.");
    }
}
