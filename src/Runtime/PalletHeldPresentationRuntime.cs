using System;
using S1API.Items;
using S1ItemCreator = S1API.Items.Storable.ItemCreator;
using UnityEngine;
using Object = UnityEngine.Object;

#if IL2CPP
using Il2CppInterop.Runtime.InteropTypes;
using S1AvatarEquippable =
    Il2CppScheduleOne.AvatarFramework.Equipping.AvatarEquippable;
using S1Equippable = Il2CppScheduleOne.Equipping.Equippable;
using S1EquippableBuildableItem =
    Il2CppScheduleOne.Equipping.Equippable_BuildableItem;
#else
using S1AvatarEquippable =
    ScheduleOne.AvatarFramework.Equipping.AvatarEquippable;
using S1Equippable = ScheduleOne.Equipping.Equippable;
using S1EquippableBuildableItem =
    ScheduleOne.Equipping.Equippable_BuildableItem;
#endif

namespace S1Forklift.Runtime;

internal static class PalletHeldPresentationRuntime
{
    private static Equippable? _equippable;
    private static GameObject? _cacheRoot;

    internal static Equippable GetOrCreateEquippable()
    {
        EnsureAvatarRegistered();
        return _equippable ??=
            S1ItemCreator.CreateEquippableBuilder()
                .CreateEquippable<S1EquippableBuildableItem>(
                    "S1Forklift_StoragePallet_Equippable")
                .WithInteraction(
                    canInteract: true,
                    canPickup: true)
                .WithAvatarEquippable(
                    Constants.PalletAvatarResourcePath,
                    AvatarHand.Right,
                    "RightArm_Hold_OpenHand")
                .Build();
    }

    internal static bool TryVerify(
        S1Equippable? equippable,
        out string detail)
    {
        var avatar =
            equippable?.GetComponentInChildren<S1AvatarEquippable>(true);
        var registered =
            AvatarEquippableRegistry.IsRegistered(
                Constants.PalletAvatarResourcePath);
        var buildable =
#if IL2CPP
            equippable?.TryCast<S1EquippableBuildableItem>() != null;
#else
            equippable is S1EquippableBuildableItem;
#endif
        detail =
            $"BuildableEquippable={buildable}|" +
            $"AvatarEquippable={avatar != null}|" +
            $"AvatarRegistered={registered}|" +
            $"AvatarPath={avatar?.AssetPath ?? "<none>"}";
        return buildable &&
               avatar != null &&
               registered &&
               string.Equals(
                   avatar.AssetPath,
                   Constants.PalletAvatarResourcePath,
                   StringComparison.Ordinal);
    }

    private static void EnsureAvatarRegistered()
    {
        if (AvatarEquippableRegistry.IsRegistered(
                Constants.PalletAvatarResourcePath))
        {
            return;
        }

        var source =
            Resources.Load<GameObject>(
                AvatarEquippablePaths.TrashBag)
            ?? throw new InvalidOperationException(
                "The native trash-bag avatar equippable is unavailable.");
        var root = Object.Instantiate(source);
        root.name = "S1Forklift_StoragePallet_AvatarEquippable";

        var renderers = root.GetComponentsInChildren<Renderer>(true);
        for (var index = 0; index < renderers.Length; index++)
            renderers[index].enabled = false;

        var visual = PalletPrefabFactory.AddPalletPresentation(
            root.transform);
        visual.localPosition = new Vector3(0f, -0.24f, 0f);
        visual.localRotation = Quaternion.Euler(-90f, 0f, 0f);
        visual.localScale = Vector3.one * 0.45f;
        visual.gameObject.SetActive(true);

        var avatar =
            root.GetComponent<S1AvatarEquippable>()
            ?? throw new InvalidOperationException(
                "The trash-bag avatar scaffold has no AvatarEquippable.");
        avatar.AssetPath = Constants.PalletAvatarResourcePath;
        avatar.AnimationTrigger = "RightArm_Hold_OpenHand";

        Cache(root);
        if (!AvatarEquippableRegistry.RegisterAvatarEquippable(
                Constants.PalletAvatarResourcePath,
                root))
        {
            throw new InvalidOperationException(
                "S1API could not register the pallet avatar equippable.");
        }
    }

    private static void Cache(GameObject prefab)
    {
        if (_cacheRoot == null)
        {
            _cacheRoot = new GameObject(
                "S1Forklift_PresentationPrefabCache")
            {
                hideFlags = HideFlags.HideAndDontSave,
            };
            _cacheRoot.SetActive(false);
            Object.DontDestroyOnLoad(_cacheRoot);
        }

        prefab.hideFlags = HideFlags.HideAndDontSave;
        prefab.transform.SetParent(_cacheRoot.transform, false);
        prefab.SetActive(true);
    }
}
