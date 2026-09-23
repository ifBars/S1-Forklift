using S1API.Items;
using S1API.Storages;
using S1Forklift.Runtime;
using UnityEngine;

#if IL2CPP
using S1BuildManager = Il2CppScheduleOne.Building.BuildManager;
using S1Coordinate = Il2CppScheduleOne.Tiles.Coordinate;
using S1Grid = Il2CppScheduleOne.Tiles.Grid;
using S1PlaceableStorageEntity = Il2CppScheduleOne.ObjectScripts.PlaceableStorageEntity;
using S1Registry = Il2CppScheduleOne.Registry;
#else
using S1BuildManager = ScheduleOne.Building.BuildManager;
using S1Coordinate = ScheduleOne.Tiles.Coordinate;
using S1Grid = ScheduleOne.Tiles.Grid;
using S1PlaceableStorageEntity = ScheduleOne.ObjectScripts.PlaceableStorageEntity;
using S1Registry = ScheduleOne.Registry;
#endif

namespace S1Forklift.API;

/// <summary>
/// Stable integration surface for mods that treat Storage Pallets as physical cargo.
/// </summary>
public static class PalletApi
{
    /// <summary>The registered item ID used by Forklift &amp; Pallets.</summary>
    public const string PalletItemId = "pallet";

    /// <summary>Returns whether the Storage Pallet definition is registered.</summary>
    public static bool IsRegistered => ItemManager.IsItemRegistered(PalletItemId);

    /// <summary>
    /// Finds the nearest active pallet that has been set down and settled inside a
    /// horizontal staging radius. Pallets still carried by a forklift are excluded.
    /// </summary>
    public static bool TryFindNearestSettledPallet(
        Vector3 center,
        float horizontalRadius,
        out GameObject palletRoot,
        out StorageInstance storage,
        out float horizontalDistance)
    {
        palletRoot = null!;
        storage = null!;
        horizontalDistance = float.PositiveInfinity;

        if (horizontalRadius <= 0f)
        {
            return false;
        }

        var pallets = Resources.FindObjectsOfTypeAll<S1PlaceableStorageEntity>();
        for (var index = 0; index < pallets.Length; index++)
        {
            var candidate = pallets[index];
            if (!PalletCarryRuntime.IsSettledPallet(candidate))
            {
                continue;
            }

            var delta = candidate.transform.position - center;
            delta.y = 0f;
            var distance = delta.magnitude;
            if (distance > horizontalRadius || distance >= horizontalDistance)
            {
                continue;
            }

            var candidateStorage = StorageInstance.FromGameObject(candidate.gameObject);
            if (candidateStorage == null)
            {
                continue;
            }

            palletRoot = candidate.gameObject;
            storage = candidateStorage;
            horizontalDistance = distance;
        }

        return palletRoot != null;
    }

    /// <summary>
    /// Creates a persistent pallet and moves it into a free-world pose. This is intended
    /// for host-authoritative quest or scenario setup; ordinary players should buy and
    /// place pallets through the native build flow.
    /// </summary>
    public static bool TryCreateWorldPallet(
        Vector3 position,
        Quaternion rotation,
        out GameObject palletRoot,
        out string detail)
    {
        palletRoot = null!;
        detail = string.Empty;
        try
        {
            var definition = S1Registry.GetItem(PalletItemId);
            if (definition == null)
            {
                detail = "The Storage Pallet item definition is not registered.";
                return false;
            }

            var grid = FindCreationGrid(out var origin);
            var manager = S1BuildManager.Instance;
            if (grid == null || manager == null)
            {
                detail = "No native build grid is available for pallet creation.";
                return false;
            }

            var created = manager.CreateGridItem(
                definition.GetDefaultInstance(),
                grid,
                new Vector2(origin.x, origin.y),
                0);
            var pallet = created as S1PlaceableStorageEntity;
            if (pallet == null)
            {
                detail = "The native build manager did not create a Storage Pallet entity.";
                return false;
            }

            PalletCarryRuntime.PlaceAtWorldPose(pallet, position, rotation);
            palletRoot = pallet.gameObject;
            detail = $"Guid={pallet.GUID}|Position={pallet.transform.position}";
            return true;
        }
        catch (Exception exception)
        {
            detail = exception.Message;
            return false;
        }
    }

    private static S1Grid? FindCreationGrid(out S1Coordinate origin)
    {
        origin = new S1Coordinate();
        var grids = Resources.FindObjectsOfTypeAll<S1Grid>();
        for (var gridIndex = 0; gridIndex < grids.Length; gridIndex++)
        {
            var grid = grids[gridIndex];
            if (grid == null || !grid.gameObject.scene.IsValid())
            {
                continue;
            }

            for (var pairIndex = 0; pairIndex < grid.CoordinateTilePairs.Count; pairIndex++)
            {
                var candidate = grid.CoordinateTilePairs[pairIndex].coord;
                var hasFullFootprint = true;
                for (var x = 0; x < Constants.PalletPlacementFootprintSize && hasFullFootprint; x++)
                {
                    for (var y = 0; y < Constants.PalletPlacementFootprintSize; y++)
                    {
                        if (grid.GetTile(new S1Coordinate(candidate.x + x, candidate.y + y)) != null)
                        {
                            continue;
                        }

                        hasFullFootprint = false;
                        break;
                    }
                }

                if (!hasFullFootprint)
                {
                    continue;
                }

                origin = new S1Coordinate(candidate.x, candidate.y);
                return grid;
            }
        }

        return null;
    }
}
