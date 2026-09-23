using System;
using System.Linq;
using HarmonyLib;
using MelonLoader;
using S1Forklift.Runtime;

#if IL2CPP
using S1BuildStartGrid = Il2CppScheduleOne.Building.BuildStart_Grid;
using S1BuildableItem = Il2CppScheduleOne.EntityFramework.BuildableItem;
using S1BuildableItemDefinition = Il2CppScheduleOne.ItemFramework.BuildableItemDefinition;
using S1ConfigurationReplicator = Il2CppScheduleOne.Management.ConfigurationReplicator;
using S1GridItem = Il2CppScheduleOne.EntityFramework.GridItem;
using S1ItemInstance = Il2CppScheduleOne.ItemFramework.ItemInstance;
using S1ITransitEntity = Il2CppScheduleOne.Management.ITransitEntity;
using S1NavMeshUtility = Il2CppScheduleOne.DevUtilities.NavMeshUtility;
using S1Npc = Il2CppScheduleOne.NPCs.NPC;
using S1PlaceableStorageEntity = Il2CppScheduleOne.ObjectScripts.PlaceableStorageEntity;
using S1StorageEntity = Il2CppScheduleOne.Storage.StorageEntity;
using S1StorageMenu = Il2CppScheduleOne.UI.StorageMenu;
#else
using S1BuildStartGrid = ScheduleOne.Building.BuildStart_Grid;
using S1BuildableItem = ScheduleOne.EntityFramework.BuildableItem;
using S1BuildableItemDefinition = ScheduleOne.ItemFramework.BuildableItemDefinition;
using S1ConfigurationReplicator = ScheduleOne.Management.ConfigurationReplicator;
using S1GridItem = ScheduleOne.EntityFramework.GridItem;
using S1ItemInstance = ScheduleOne.ItemFramework.ItemInstance;
using S1ITransitEntity = ScheduleOne.Management.ITransitEntity;
using S1NavMeshUtility = ScheduleOne.DevUtilities.NavMeshUtility;
using S1Npc = ScheduleOne.NPCs.NPC;
using S1PlaceableStorageEntity = ScheduleOne.ObjectScripts.PlaceableStorageEntity;
using S1StorageEntity = ScheduleOne.Storage.StorageEntity;
using S1StorageMenu = ScheduleOne.UI.StorageMenu;
#endif

namespace S1Forklift.Patches;

[HarmonyPatch(typeof(S1NavMeshUtility), nameof(S1NavMeshUtility.GetReachableAccessPoint))]
internal static class PalletReachableAccessPointPatch
{
    [HarmonyPrefix]
    private static bool Prefix(
        S1ITransitEntity entity,
        S1Npc npc,
        ref UnityEngine.Transform __result)
    {
        if (LoadingDockPalletRuntime.TryGetReachableAccessPoint(
                entity,
                npc,
                out var loadingDockAccessPoint))
        {
            __result = loadingDockAccessPoint!;
            return false;
        }

        if (!PalletTransitRuntime.TryGetReachableAccessPoint(
                entity,
                npc,
                out var palletAccessPoint))
        {
            return true;
        }

        __result = palletAccessPoint!;
        return false;
    }
}

[HarmonyPatch(typeof(S1PlaceableStorageEntity), nameof(S1PlaceableStorageEntity.InitializeGridItem))]
internal static class PalletStorageInitializationPatch
{
#if !IL2CPP
    private static readonly System.Reflection.FieldInfo ConfigReplicatorField =
        AccessTools.Field(typeof(S1PlaceableStorageEntity), "_configReplicator")
        ?? throw new MissingFieldException(
            typeof(S1PlaceableStorageEntity).FullName,
            "_configReplicator");
#endif
    private static readonly System.Reflection.MethodInfo InitializedSetter =
        AccessTools.PropertySetter(typeof(S1BuildableItem), nameof(S1BuildableItem.Initialized))
        ?? throw new MissingMethodException(
            typeof(S1BuildableItem).FullName,
            $"set_{nameof(S1BuildableItem.Initialized)}");

    [HarmonyPrefix]
    private static void Prefix(S1PlaceableStorageEntity __instance, S1ItemInstance instance)
    {
        var isPalletPrefab = __instance.name.StartsWith(
            Constants.PalletPrefabName,
            StringComparison.Ordinal);
        var isPalletItem = instance != null &&
                           string.Equals(
                               instance.ID,
                               Constants.PalletItemId,
                               StringComparison.OrdinalIgnoreCase);
        if (!isPalletPrefab && !isPalletItem)
        {
            return;
        }

        if (__instance.Initialized)
            InitializedSetter.Invoke(__instance, new object[] { false });

#if IL2CPP
        if (__instance._configReplicator == null)
        {
            __instance._configReplicator =
                __instance.GetComponent<S1ConfigurationReplicator>()
                ?? throw new InvalidOperationException(
                    "The pallet storage prefab has no ConfigurationReplicator.");
        }
#else
        if (ConfigReplicatorField.GetValue(__instance) == null)
        {
            var replicator = __instance.GetComponent<S1ConfigurationReplicator>()
                             ?? throw new InvalidOperationException(
                                 "The pallet storage prefab has no ConfigurationReplicator.");
            ConfigReplicatorField.SetValue(__instance, replicator);
        }
#endif
    }

    [HarmonyPostfix]
    private static void Postfix(S1PlaceableStorageEntity __instance)
    {
        if (PalletCarryRuntime.IsPallet(__instance))
        {
            __instance.StorageEntity.DisplayRowCount = Constants.PalletDisplayRowCount;
            PalletPrefabFactory.RefreshPlacedTransitAccess(__instance);
        }
        PalletCarryRuntime.RegisterPallet(__instance);
    }
}

[HarmonyPatch]
internal static class PalletStorageMenuPatch
{
    private static System.Reflection.MethodBase TargetMethod() =>
        AccessTools.GetDeclaredMethods(typeof(S1StorageMenu))
            .FirstOrDefault(method =>
            {
                if (method.Name != nameof(S1StorageMenu.Open))
                    return false;
                var parameters = method.GetParameters();
                return parameters.Length == 2 &&
                       parameters[0].ParameterType == typeof(S1StorageEntity);
            })
        ?? throw new MissingMethodException(
            typeof(S1StorageMenu).FullName,
            nameof(S1StorageMenu.Open));

    [HarmonyPrefix]
    private static void Prefix(S1StorageMenu __instance, S1StorageEntity entity)
    {
        try
        {
            PalletStorageMenuRuntime.Prepare(__instance, entity);
        }
        catch (Exception ex)
        {
            MelonLogger.Error(
                $"[S1Forklift] Could not expand the pallet storage menu: {ex}");
        }
    }
}

[HarmonyPatch(typeof(S1BuildStartGrid), "CreateGhostModel")]
internal static class PalletPlacementGhostPatch
{
    [HarmonyPostfix]
    private static void Postfix(
        S1BuildableItemDefinition itemDefinition,
        S1GridItem __result)
    {
        if (itemDefinition == null ||
            !string.Equals(
                itemDefinition.ID,
                Constants.PalletItemId,
                StringComparison.OrdinalIgnoreCase) ||
            __result == null)
        {
            return;
        }

        try
        {
            PalletPrefabFactory.ConfigureGhost(__result);
        }
        catch (Exception ex)
        {
            MelonLogger.Error(
                $"[S1Forklift] Could not configure the pallet placement ghost: {ex}");
        }
    }
}
