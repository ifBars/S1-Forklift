using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;
using UnityEngine.AI;
using Object = UnityEngine.Object;

#if IL2CPP
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using Il2CppGameObjectList = Il2CppSystem.Collections.Generic.List<UnityEngine.GameObject>;
using FishNetInstanceFinder = Il2CppFishNet.InstanceFinder;
using FishNetNetworkObject = Il2CppFishNet.Object.NetworkObject;
using S1BuildableItem = Il2CppScheduleOne.EntityFramework.BuildableItem;
using S1InteractableObject = Il2CppScheduleOne.Interaction.InteractableObject;
using S1GridItem = Il2CppScheduleOne.EntityFramework.GridItem;
using S1PlaceableStorageEntity = Il2CppScheduleOne.ObjectScripts.PlaceableStorageEntity;
using S1StorageDoorAnimation = Il2CppScheduleOne.Storage.StorageDoorAnimation;
using S1StorageEntityVisualizer = Il2CppScheduleOne.Storage.StorageEntityVisualizer;
using S1StorageEntityInteractable = Il2CppScheduleOne.Storage.StorageEntityInteractable;
using S1StorageGrid = Il2CppScheduleOne.Storage.StorageGrid;
using S1StorageTile = Il2CppScheduleOne.Storage.StorageTile;
using S1Coordinate = Il2CppScheduleOne.Tiles.Coordinate;
using S1CoordinateFootprintTilePair = Il2CppScheduleOne.Tiles.CoordinateFootprintTilePair;
using S1FootprintTile = Il2CppScheduleOne.Tiles.FootprintTile;
using S1NavMeshUtility = Il2CppScheduleOne.DevUtilities.NavMeshUtility;
#else
using FishNetInstanceFinder = FishNet.InstanceFinder;
using FishNetNetworkObject = FishNet.Object.NetworkObject;
using S1BuildableItem = ScheduleOne.EntityFramework.BuildableItem;
using S1InteractableObject = ScheduleOne.Interaction.InteractableObject;
using S1GridItem = ScheduleOne.EntityFramework.GridItem;
using S1PlaceableStorageEntity = ScheduleOne.ObjectScripts.PlaceableStorageEntity;
using S1StorageDoorAnimation = ScheduleOne.Storage.StorageDoorAnimation;
using S1StorageEntityVisualizer = ScheduleOne.Storage.StorageEntityVisualizer;
using S1StorageEntityInteractable = ScheduleOne.Storage.StorageEntityInteractable;
using S1StorageGrid = ScheduleOne.Storage.StorageGrid;
using S1StorageTile = ScheduleOne.Storage.StorageTile;
using S1Coordinate = ScheduleOne.Tiles.Coordinate;
using S1CoordinateFootprintTilePair = ScheduleOne.Tiles.CoordinateFootprintTilePair;
using S1FootprintTile = ScheduleOne.Tiles.FootprintTile;
using S1NavMeshUtility = ScheduleOne.DevUtilities.NavMeshUtility;
#endif

namespace S1Forklift.Runtime;

internal static class PalletPrefabFactory
{
    private static S1BuildableItem? _prefab;
#if !IL2CPP
    private static readonly System.Reflection.FieldInfo OutlineRenderersField =
        AccessTools.Field(typeof(S1BuildableItem), "OutlineRenderers")
        ?? throw new MissingFieldException(typeof(S1BuildableItem).FullName, "OutlineRenderers");
    private static readonly System.Reflection.FieldInfo IncludeOutlineChildrenField =
        AccessTools.Field(typeof(S1BuildableItem), "IncludeOutlineRendererChildren")
        ?? throw new MissingFieldException(
            typeof(S1BuildableItem).FullName,
            "IncludeOutlineRendererChildren");
    private static readonly System.Reflection.FieldInfo OutlineEffectField =
        AccessTools.Field(typeof(S1BuildableItem), "OutlineEffect")
        ?? throw new MissingFieldException(typeof(S1BuildableItem).FullName, "OutlineEffect");
#endif

    internal static S1BuildableItem GetOrCreate()
    {
        if (_prefab != null)
            return _prefab;

        var source = Resources.Load<GameObject>(Constants.PalletSourceResourcePath)
                     ?? throw new InvalidOperationException(
                         $"Pallet storage source '{Constants.PalletSourceResourcePath}' was not found.");
        var visualSourceRoot = Resources.Load<GameObject>(Constants.PalletVisualResourcePath)
                               ?? throw new InvalidOperationException(
                                   $"Pallet visual source '{Constants.PalletVisualResourcePath}' was not found.");
        var visualSource = TransformUtility.FindDescendant(visualSourceRoot.transform, "Pallet")
                           ?? throw new InvalidOperationException("The game pallet mesh was not found.");

        var clone = InactiveObjectCloner.CloneGameObject(source);
        clone.name = Constants.PalletPrefabName;
        clone.hideFlags = HideFlags.HideAndDontSave;
        clone.transform.position = Vector3.zero;
        clone.transform.rotation = Quaternion.identity;
        clone.transform.localScale = Vector3.one;
        Object.DontDestroyOnLoad(clone);

        var entity = clone.GetComponent<S1PlaceableStorageEntity>()
                     ?? throw new InvalidOperationException("The pallet storage clone has no PlaceableStorageEntity.");
        entity.StorageEntity.StorageEntityName = Constants.PalletDisplayName;
        entity.StorageEntity.SlotCount = Constants.PalletSlotCount;
        entity.StorageEntity.DisplayRowCount = Constants.PalletDisplayRowCount;
        entity.StorageEntity.MaxAccessDistance = Constants.PalletInteractionRange;

        DisableSourcePresentation(clone);
        AddPalletPresentation(clone.transform, visualSource);
        ConfigureOutline(entity);
        ConfigurePlacementAnchors(entity);
        ConfigureTransitAccess(entity);
        ConfigureBounds(entity);
        ConfigureStorageSurface(clone);
        ConfigureInteraction(clone);

        clone.transform.position = new Vector3(0f, -1000f, 0f);
        clone.transform.rotation = Quaternion.identity;
        // BuildManager initializes GridItems immediately after Instantiate. Keeping the
        // template active ensures Unity invokes Awake first, matching native built prefabs.
        clone.SetActive(true);
        EnsureSpawnable(clone.GetComponent<FishNetNetworkObject>());
        _prefab = entity;
        return _prefab;
    }

    internal static GameObject CreateIconSource()
    {
        var root = new GameObject("S1Forklift_PalletIconSource");
        AddPalletPresentation(root.transform);
        root.transform.position = Vector3.zero;
        root.transform.rotation = Quaternion.Euler(45f, 0f, 0f);
        return root;
    }

    internal static Transform AddPalletPresentation(Transform root)
    {
        AddPalletPresentation(root, GetVisualSource());
        return TransformUtility.FindDirectChild(root, "PalletVisual")
               ?? throw new InvalidOperationException(
                   "The pallet visual could not be created.");
    }

    internal static void ConfigureGhost(S1GridItem ghost)
    {
        if (ghost == null)
            return;

        ghost.gameObject.SetActive(true);
        var visual = TransformUtility.FindDirectChild(ghost.transform, "PalletVisual");
        if (visual == null)
        {
            AddPalletPresentation(ghost.transform, GetVisualSource());
            visual = TransformUtility.FindDirectChild(ghost.transform, "PalletVisual");
        }

        var renderers = ghost.GetComponentsInChildren<Renderer>(true);
        for (var index = 0; index < renderers.Length; index++)
        {
            var renderer = renderers[index];
            var isPalletVisual =
                visual != null &&
                (renderer.transform == visual ||
                 renderer.transform.IsChildOf(visual));
            renderer.enabled = isPalletVisual;
            if (!isPalletVisual)
                continue;

            var current = renderer.transform;
            while (current != null)
            {
                current.gameObject.SetActive(true);
                if (current == ghost.transform)
                    break;
                current = current.parent;
            }
        }
        if (visual != null)
            visual.gameObject.SetActive(true);

        var interactables = ghost.GetComponentsInChildren<S1InteractableObject>(true);
        for (var index = 0; index < interactables.Length; index++)
            interactables[index].enabled = false;
    }

    internal static void DisableStoredDisplayColliders(S1PlaceableStorageEntity entity)
    {
        var visualizer = entity.GetComponent<S1StorageEntityVisualizer>();
        if (visualizer == null || visualizer.ItemContainer == null)
            return;

        var colliders = visualizer.ItemContainer.GetComponentsInChildren<Collider>(true);
        for (var index = 0; index < colliders.Length; index++)
            colliders[index].enabled = false;
    }

    internal static bool TryVerifyPrefab(out string detail)
    {
        var prefab = GetOrCreate();
        var entity = prefab.GetComponent<S1PlaceableStorageEntity>();
        if (entity == null)
        {
            detail = "Prefab is not a PlaceableStorageEntity.";
            return false;
        }

        var visual = TransformUtility.FindDirectChild(entity.transform, "PalletVisual");
        var storageInteractables =
            entity.GetComponentsInChildren<S1StorageEntityInteractable>(true);
        var enabledStorageInteractables = 0;
        BoxCollider? interactionCollider = null;
        for (var index = 0; index < storageInteractables.Length; index++)
        {
            if (storageInteractables[index].enabled)
            {
                enabledStorageInteractables++;
                interactionCollider =
                    storageInteractables[index].GetComponent<BoxCollider>();
            }
        }

        var anchorY = entity.BuildPoint == null
            ? float.PositiveInfinity
            : entity.BuildPoint.localPosition.y;
        var footprintY =
            entity.CoordinateFootprintTilePairs.Count == 0
                ? float.PositiveInfinity
                : entity.CoordinateFootprintTilePairs[0]
                    .footprintTile.transform.parent.localPosition.y;
        var footprintX = entity.FootprintX;
        var footprintZ = entity.FootprintY;
        var transitAccessReady = TryVerifyTransitAccess(entity, out var transitAccessDetail);
        var outlineReady = TryVerifyOutlineConfiguration(entity, out var outlineDetail);
        detail =
            $"Visual={visual != null}|StoragePrompts={enabledStorageInteractables}|" +
            $"InteractionCollider={interactionCollider != null}|" +
            $"InteractionSize={(interactionCollider == null
                ? "<none>"
                : $"{interactionCollider.size.x:F2}x" +
                  $"{interactionCollider.size.y:F2}x" +
                  $"{interactionCollider.size.z:F2}")}|" +
            $"PlacementFootprint={footprintX}x{footprintZ}|" +
            $"BuildAnchorY={anchorY:F3}|FootprintY={footprintY:F3}|" +
            transitAccessDetail + "|" + outlineDetail;
        return visual != null &&
               enabledStorageInteractables == 1 &&
               interactionCollider != null &&
               interactionCollider.isTrigger &&
               interactionCollider.size.x >= 1.49f &&
               interactionCollider.size.z >= 1.49f &&
               footprintX == Constants.PalletPlacementFootprintSize &&
               footprintZ == Constants.PalletPlacementFootprintSize &&
               entity.CoordinateFootprintTilePairs.Count ==
                   Constants.PalletPlacementFootprintSize *
                   Constants.PalletPlacementFootprintSize &&
               Math.Abs(anchorY - Constants.PalletPlacementAnchorY) < 0.001f &&
               Math.Abs(footprintY - Constants.PalletPlacementAnchorY) < 0.001f &&
               transitAccessReady &&
               outlineReady;
    }

    private static Transform GetVisualSource()
    {
        var visualSourceRoot = Resources.Load<GameObject>(Constants.PalletVisualResourcePath)
                               ?? throw new InvalidOperationException(
                                   $"Pallet visual source '{Constants.PalletVisualResourcePath}' was not found.");
        return TransformUtility.FindDescendant(visualSourceRoot.transform, "Pallet")
               ?? throw new InvalidOperationException("The game pallet mesh was not found.");
    }

    private static void DisableSourcePresentation(GameObject clone)
    {
        foreach (var renderer in clone.GetComponentsInChildren<Renderer>(true))
            renderer.enabled = false;

        foreach (var collider in clone.GetComponentsInChildren<Collider>(true))
        {
            if (!collider.isTrigger)
                collider.enabled = false;
        }
    }

    private static void AddPalletPresentation(Transform root, Transform source)
    {
        var visual = Object.Instantiate(source.gameObject, root, false);
        visual.name = "PalletVisual";
        visual.transform.localPosition = Vector3.zero;
        visual.transform.localRotation = Quaternion.identity;
        visual.transform.localScale = Vector3.one;

        foreach (var renderer in visual.GetComponentsInChildren<Renderer>(true))
            renderer.enabled = true;
        foreach (var collider in visual.GetComponentsInChildren<Collider>(true))
            collider.enabled = false;

        if (!TryGetBounds(visual, out var bounds))
            return;

        var offset = new Vector3(-bounds.center.x, -bounds.min.y - 0.025f, -bounds.center.z);
        visual.transform.position += offset;
    }

    private static void ConfigurePlacementAnchors(S1PlaceableStorageEntity entity)
    {
        if (entity.BuildPoint == null)
            throw new InvalidOperationException("The pallet scaffold has no build point.");
        var buildPointPosition = entity.BuildPoint.localPosition;
        buildPointPosition.y = Constants.PalletPlacementAnchorY;
        entity.BuildPoint.localPosition = buildPointPosition;

        if (entity.CoordinateFootprintTilePairs.Count == 0)
            throw new InvalidOperationException("The pallet scaffold has no footprint tiles.");
        ExpandPlacementFootprint(entity);
        var footprintRoot =
            entity.CoordinateFootprintTilePairs[0].footprintTile.transform.parent;
        var footprintPosition = footprintRoot.localPosition;
        var footprintHalfOffset =
            (Constants.PalletPlacementFootprintSize - 1) * 0.25f;
        footprintPosition.x = -footprintHalfOffset;
        footprintPosition.y = Constants.PalletPlacementAnchorY;
        footprintPosition.z = -footprintHalfOffset;
        footprintRoot.localPosition = footprintPosition;
    }

    private static void ConfigureTransitAccess(S1PlaceableStorageEntity entity)
    {
        var positions = new[]
        {
            new Vector3(0f, Constants.PalletTransitAccessHeight, Constants.PalletTransitAccessOffset),
            new Vector3(0f, Constants.PalletTransitAccessHeight, -Constants.PalletTransitAccessOffset),
            new Vector3(Constants.PalletTransitAccessOffset, Constants.PalletTransitAccessHeight, 0f),
            new Vector3(-Constants.PalletTransitAccessOffset, Constants.PalletTransitAccessHeight, 0f),
        };

#if IL2CPP
        var accessPoints = new Il2CppReferenceArray<Transform>(positions.Length);
#else
        var accessPoints = new Transform[positions.Length];
#endif
        for (var index = 0; index < positions.Length; index++)
        {
            var accessPoint = new GameObject($"PalletAccessPoint_{index + 1}").transform;
            accessPoint.SetParent(entity.transform, false);
            accessPoint.localPosition = positions[index];
            accessPoint.localRotation = Quaternion.LookRotation(-positions[index].normalized, Vector3.up);
            accessPoints[index] = accessPoint;
        }
        entity.accessPoints = accessPoints;

        var linkOrigin = new GameObject("PalletLinkOrigin").transform;
        linkOrigin.SetParent(entity.transform, false);
        linkOrigin.localPosition = new Vector3(0f, Constants.PalletTransitLinkHeight, 0f);
        linkOrigin.localRotation = Quaternion.identity;
#if IL2CPP
        entity._linkOrigin = linkOrigin;
#else
        AccessTools.Field(typeof(S1PlaceableStorageEntity), "_linkOrigin")
            ?.SetValue(entity, linkOrigin);
#endif
    }

    internal static void RefreshPlacedTransitAccess(S1PlaceableStorageEntity entity)
    {
        if (entity == null || entity.AccessPoints == null)
            return;

        var localDirections = new[]
        {
            Vector3.forward,
            Vector3.back,
            Vector3.right,
            Vector3.left,
        };
        var candidateRadii = new[]
        {
            Constants.PalletTransitAccessOffset,
            0.85f,
            0.7f,
        };

        for (var index = 0; index < entity.AccessPoints.Length; index++)
        {
            var accessPoint = entity.AccessPoints[index];
            if (accessPoint == null)
                continue;

            var direction = localDirections[index % localDirections.Length];
            var found = false;
            var hasReachableFallback = false;
            var reachableFallback = default(Vector3);
            for (var radiusIndex = 0; radiusIndex < candidateRadii.Length; radiusIndex++)
            {
                var localTarget =
                    direction * candidateRadii[radiusIndex] +
                    Vector3.up * Constants.PalletTransitAccessHeight;
                var worldTarget = entity.transform.TransformPoint(localTarget);
                if (!S1NavMeshUtility.SamplePosition(
                        worldTarget,
                        out var hit,
                        Constants.PalletTransitNavMeshSampleDistance,
                        -1,
                        false))
                {
                    continue;
                }

                var snappedPosition =
                    hit.position + Vector3.up * Constants.PalletTransitNavMeshOffset;
                if (!hasReachableFallback)
                {
                    hasReachableFallback = true;
                    reachableFallback = snappedPosition;
                }
                if (entity.ParentProperty != null &&
                    !entity.ParentProperty.DoBoundsContainPoint(snappedPosition))
                {
                    continue;
                }

                accessPoint.position = snappedPosition;
                accessPoint.rotation = Quaternion.LookRotation(
                    entity.transform.position - snappedPosition,
                    Vector3.up);
                found = true;
                break;
            }

            if (!found && hasReachableFallback)
            {
                accessPoint.position = reachableFallback;
                accessPoint.rotation = Quaternion.LookRotation(
                    entity.transform.position - reachableFallback,
                    Vector3.up);
            }
            else if (!found)
            {
                var fallback =
                    direction * Constants.PalletTransitAccessOffset +
                    Vector3.up * Constants.PalletTransitAccessHeight;
                accessPoint.localPosition = fallback;
                accessPoint.localRotation = Quaternion.LookRotation(-direction, Vector3.up);
            }
        }
    }

    internal static bool TryVerifyPlacedTransitAccess(
        S1PlaceableStorageEntity entity,
        out string detail)
    {
        var accessPoints = entity.AccessPoints;
        var validCount = 0;
        var sampledCount = 0;
        var propertyContainedCount = 0;
        var maximumSampleDelta = 0f;
        for (var index = 0; index < accessPoints.Length; index++)
        {
            var accessPoint = accessPoints[index];
            if (accessPoint == null)
                continue;

            validCount++;
            if (S1NavMeshUtility.SamplePosition(
                    accessPoint.position,
                    out var hit,
                    0.25f,
                    -1,
                    false))
            {
                sampledCount++;
                maximumSampleDelta = Math.Max(
                    maximumSampleDelta,
                    Vector3.Distance(accessPoint.position, hit.position));
            }
            if (entity.ParentProperty == null ||
                entity.ParentProperty.DoBoundsContainPoint(accessPoint.position))
            {
                propertyContainedCount++;
            }
        }

        detail =
            $"TransitPlaced={validCount}/{accessPoints.Length}|" +
            $"NavMesh={sampledCount}/{accessPoints.Length}|" +
            $"PropertyBounds={propertyContainedCount}/{accessPoints.Length}|" +
            $"MaxSampleDelta={maximumSampleDelta:F3}";
        return validCount == accessPoints.Length &&
               sampledCount == accessPoints.Length &&
               propertyContainedCount == accessPoints.Length &&
               maximumSampleDelta <= 0.1f;
    }

    internal static bool TryVerifyOutlineRuntime(
        S1PlaceableStorageEntity entity,
        out string detail)
    {
        if (!TryVerifyOutlineConfiguration(entity, out var configurationDetail))
        {
            detail = configurationDetail;
            return false;
        }

        entity.ShowOutline(new Color(0f, 0.8f, 1f, 1f));
#if IL2CPP
        var outlineEffect = entity.OutlineEffect;
#else
        var outlineEffect = OutlineEffectField.GetValue(entity) as Behaviour;
#endif
        var created = outlineEffect != null;
        var enabled = outlineEffect != null && outlineEffect.enabled;
        entity.HideOutline();
        var hidden = outlineEffect != null && !outlineEffect.enabled;
        detail =
            configurationDetail +
            $"|OutlineCreated={created}|OutlineEnabled={enabled}|OutlineHidden={hidden}";
        return created && enabled && hidden;
    }

    private static void ConfigureOutline(S1PlaceableStorageEntity entity)
    {
        var visual = TransformUtility.FindDirectChild(entity.transform, "PalletVisual")
                     ?? throw new InvalidOperationException("The pallet visual is unavailable for outlining.");
#if IL2CPP
        var renderers = new Il2CppGameObjectList();
        renderers.Add(visual.gameObject);
        entity.OutlineRenderers = renderers;
        entity.IncludeOutlineRendererChildren = true;
        entity.OutlineEffect = null;
#else
        OutlineRenderersField.SetValue(
            entity,
            new System.Collections.Generic.List<GameObject> { visual.gameObject });
        IncludeOutlineChildrenField.SetValue(entity, true);
        OutlineEffectField.SetValue(entity, null);
#endif
    }

    private static bool TryVerifyOutlineConfiguration(
        S1PlaceableStorageEntity entity,
        out string detail)
    {
        var visual = TransformUtility.FindDirectChild(entity.transform, "PalletVisual");
#if IL2CPP
        var renderers = entity.OutlineRenderers;
        var rendererCount = renderers == null ? 0 : renderers.Count;
        var visualBound = rendererCount == 1 && renderers![0] == visual?.gameObject;
        var includesChildren = entity.IncludeOutlineRendererChildren;
#else
        var renderers =
            OutlineRenderersField.GetValue(entity) as System.Collections.Generic.List<GameObject>;
        var rendererCount = renderers?.Count ?? 0;
        var visualBound = rendererCount == 1 && renderers![0] == visual?.gameObject;
        var includesChildren =
            IncludeOutlineChildrenField.GetValue(entity) is true;
#endif
        detail =
            $"OutlineRenderers={rendererCount}|" +
            $"OutlineVisualBound={visualBound}|OutlineChildren={includesChildren}";
        return visual != null && visualBound && includesChildren;
    }

    private static bool TryVerifyTransitAccess(
        S1PlaceableStorageEntity entity,
        out string detail)
    {
        var accessPoints = entity.AccessPoints;
        var accessPointCount = accessPoints == null ? 0 : accessPoints.Length;
        var accessPointsValid = accessPointCount == 4;
        for (var index = 0; index < accessPointCount; index++)
        {
            var accessPoint = accessPoints![index];
            if (accessPoint == null)
            {
                accessPointsValid = false;
                continue;
            }

            var localPosition = entity.transform.InverseTransformPoint(accessPoint.position);
            var radialOffset = Math.Max(Math.Abs(localPosition.x), Math.Abs(localPosition.z));
            accessPointsValid &= Math.Abs(
                                     localPosition.y - Constants.PalletTransitAccessHeight) < 0.001f &&
                                 radialOffset >= Constants.PalletTransitAccessOffset - 0.001f;
        }

        var linkOrigin = entity.LinkOrigin;
        var linkHeight = linkOrigin == null
            ? float.PositiveInfinity
            : entity.transform.InverseTransformPoint(linkOrigin.position).y;
        var linkOriginValid =
            linkOrigin != null &&
            Math.Abs(linkHeight - Constants.PalletTransitLinkHeight) < 0.001f;
        var storageSlotCount = entity.StorageEntity.ItemSlots.Count;
        var inputSlotCount = entity.InputSlots.Count;
        var outputSlotCount = entity.OutputSlots.Count;
        var transitSlotsReady =
            storageSlotCount == Constants.PalletSlotCount &&
            inputSlotCount == storageSlotCount &&
            outputSlotCount == storageSlotCount;
        detail =
            $"TransitAccessPoints={accessPointCount}|" +
            $"TransitAccessReady={accessPointsValid}|" +
            $"TransitAccessHeight={Constants.PalletTransitAccessHeight:F3}|" +
            $"LinkOriginHeight={linkHeight:F3}|" +
            $"TransitSlots={inputSlotCount}/{outputSlotCount}/{storageSlotCount}";
        return accessPointsValid && linkOriginValid && transitSlotsReady;
    }

    private static void ExpandPlacementFootprint(S1PlaceableStorageEntity entity)
    {
        var pairs = entity.CoordinateFootprintTilePairs;
        var template = pairs[0].footprintTile
                       ?? throw new InvalidOperationException(
                           "The pallet footprint template is null.");
        var root = template.transform.parent;

        for (var x = 0; x < Constants.PalletPlacementFootprintSize; x++)
        {
            for (var y = 0; y < Constants.PalletPlacementFootprintSize; y++)
            {
                if (FindFootprintTile(pairs, x, y) != null)
                    continue;

                var tileObject = Object.Instantiate(
                    template.gameObject,
                    root,
                    false);
                tileObject.name = $"PalletFootprintTile_{x}_{y}";
                tileObject.transform.localPosition =
                    new Vector3(x * 0.5f, 0f, y * 0.5f);
                tileObject.transform.localRotation = Quaternion.identity;
                var tile = tileObject.GetComponent<S1FootprintTile>()
                           ?? throw new InvalidOperationException(
                               "A cloned pallet footprint tile has no FootprintTile.");
                tile.X = x;
                tile.Y = y;
                pairs.Add(new S1CoordinateFootprintTilePair
                {
                    coord = new S1Coordinate(x, y),
                    footprintTile = tile,
                });
            }
        }
    }

    private static S1FootprintTile? FindFootprintTile(
#if IL2CPP
        Il2CppSystem.Collections.Generic.List<S1CoordinateFootprintTilePair> pairs,
#else
        System.Collections.Generic.List<S1CoordinateFootprintTilePair> pairs,
#endif
        int x,
        int y)
    {
        for (var index = 0; index < pairs.Count; index++)
        {
            var pair = pairs[index];
            if (pair.coord.x == x && pair.coord.y == y)
                return pair.footprintTile;
        }
        return null;
    }

    private static void ConfigureBounds(S1PlaceableStorageEntity entity)
    {
        var bounding = entity.BoundingCollider;
        bounding.transform.SetParent(entity.transform, false);
        bounding.transform.localPosition = Vector3.zero;
        bounding.transform.localRotation = Quaternion.identity;
        bounding.transform.localScale = Vector3.one;
        bounding.enabled = true;
        bounding.isTrigger = false;
        bounding.center = new Vector3(0f, 0.19f, 0f);
        bounding.size = new Vector3(1.5f, 0.1f, 1.5f);

        AddRunner(entity.transform, -0.62f);
        AddRunner(entity.transform, 0f);
        AddRunner(entity.transform, 0.62f);
    }

    private static void AddRunner(Transform root, float localX)
    {
        var runner = new GameObject($"PalletRunner_{localX:+0.00;-0.00;0.00}");
        runner.transform.SetParent(root, false);
        runner.transform.localPosition = Vector3.zero;
        runner.transform.localRotation = Quaternion.identity;
        runner.layer = root.gameObject.layer;

        var collider = runner.AddComponent<BoxCollider>();
        collider.center = new Vector3(localX, 0.07f, 0f);
        collider.size = new Vector3(0.18f, 0.14f, 1.5f);
    }

    private static void ConfigureStorageSurface(GameObject clone)
    {
        var grids = clone.GetComponentsInChildren<S1StorageGrid>(true);
        if (grids.Length == 0)
            throw new InvalidOperationException("The pallet storage clone has no StorageGrid.");
        ExpandPrimaryStorageGrid(grids[0]);

        var visual = TransformUtility.FindDirectChild(clone.transform, "PalletVisual")
                     ?? throw new InvalidOperationException(
                         "The pallet storage clone has no pallet visual.");
        if (!TryGetBounds(visual.gameObject, out var visualBounds))
            throw new InvalidOperationException(
                "The pallet visual has no renderer bounds.");
        var storageSurfaceY = clone.transform.InverseTransformPoint(
            new Vector3(
                visualBounds.center.x,
                visualBounds.max.y,
                visualBounds.center.z)).y +
            Constants.PalletStorageSurfaceClearance;

        for (var gridIndex = 0; gridIndex < grids.Length; gridIndex++)
        {
            var grid = grids[gridIndex];
            grid.transform.SetParent(clone.transform, false);
            grid.transform.localPosition = gridIndex == 0
                ? new Vector3(-0.575f, storageSurfaceY, -0.575f)
                : new Vector3(0f, -5f, 0f);
            grid.transform.localRotation = Quaternion.identity;

            for (var tileIndex = 0; tileIndex < grid.storageTiles.Count; tileIndex++)
            {
                var tile = grid.storageTiles[tileIndex];
                tile.transform.localPosition =
                    new Vector3(tile.x * 0.23f, 0f, tile.y * 0.23f);
                tile.transform.localRotation = Quaternion.identity;
            }
        }

        var visualizer = clone.GetComponent<S1StorageEntityVisualizer>();
        if (visualizer != null && visualizer.ItemContainer != null)
        {
            visualizer.ItemContainer.SetParent(clone.transform, false);
            visualizer.ItemContainer.localPosition = Vector3.zero;
            visualizer.ItemContainer.localRotation = Quaternion.identity;
            visualizer.ItemContainer.gameObject.SetActive(true);
        }
        DisableStoredDisplayColliders(
            clone.GetComponent<S1PlaceableStorageEntity>()
            ?? throw new InvalidOperationException("Pallet storage entity disappeared."));

        var doorAnimation = clone.GetComponent<S1StorageDoorAnimation>();
        if (doorAnimation != null)
            doorAnimation.enabled = false;
    }

    private static void ExpandPrimaryStorageGrid(S1StorageGrid grid)
    {
        if (grid.storageTiles.Count == 0)
            throw new InvalidOperationException("The pallet StorageGrid has no template tile.");

        var template = grid.storageTiles[0];
        for (var x = 0; x < 6; x++)
        {
            for (var y = 0; y < 6; y++)
            {
                var exists = false;
                for (var index = 0; index < grid.storageTiles.Count; index++)
                {
                    var existing = grid.storageTiles[index];
                    if (existing.x == x && existing.y == y)
                    {
                        exists = true;
                        break;
                    }
                }
                if (exists)
                    continue;

                var tileObject = Object.Instantiate(template.gameObject, grid.transform, false);
                tileObject.name = $"PalletStorageTile_{x}_{y}";
                var tile = tileObject.GetComponent<S1StorageTile>()
                           ?? throw new InvalidOperationException(
                               "A cloned pallet storage tile has no StorageTile component.");
                tile.x = x;
                tile.y = y;
                grid.RegisterTile(tile);
            }
        }
    }

    private static void ConfigureInteraction(GameObject clone)
    {
        var interactables = clone.GetComponentsInChildren<S1InteractableObject>(true);
        for (var index = 0; index < interactables.Length; index++)
            interactables[index].enabled = false;

        var storageInteractables =
            clone.GetComponentsInChildren<S1StorageEntityInteractable>(true);
        if (storageInteractables.Length == 0)
            throw new InvalidOperationException("The pallet scaffold has no storage interaction.");

        var storageInteractable = storageInteractables[0];
        var interactionTransform = storageInteractable.transform;
        interactionTransform.SetParent(clone.transform, false);
        interactionTransform.localPosition = Vector3.zero;
        interactionTransform.localRotation = Quaternion.identity;
        interactionTransform.localScale = Vector3.one;

        var interactionCollider =
            storageInteractable.GetComponent<BoxCollider>()
            ?? storageInteractable.gameObject.AddComponent<BoxCollider>();
        interactionCollider.enabled = true;
        interactionCollider.isTrigger = true;
        interactionCollider.center =
            new Vector3(0f, Constants.PalletInteractionHeight * 0.5f, 0f);
        interactionCollider.size =
            new Vector3(1.5f, Constants.PalletInteractionHeight, 1.5f);

        storageInteractable.enabled = true;
        storageInteractable.MaxInteractionRange = Constants.PalletInteractionRange;
        storageInteractable.SetMessage(Constants.PalletInteractionMessage);
#if IL2CPP
        storageInteractable.displayLocationCollider = interactionCollider;
#else
        AccessTools.Field(typeof(S1InteractableObject), "displayLocationCollider")
            ?.SetValue(storageInteractable, interactionCollider);
#endif
    }

    private static bool TryGetBounds(GameObject root, out Bounds bounds)
    {
        var renderers = root.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0)
        {
            bounds = default;
            return false;
        }

        bounds = renderers[0].bounds;
        for (var index = 1; index < renderers.Length; index++)
            bounds.Encapsulate(renderers[index].bounds);
        return true;
    }

    private static void EnsureSpawnable(FishNetNetworkObject? networkObject)
    {
        if (networkObject == null)
            throw new InvalidOperationException("Pallet prefab has no FishNet NetworkObject.");

        var spawnables = FishNetInstanceFinder.NetworkManager?.SpawnablePrefabs
                         ?? throw new InvalidOperationException("FishNet spawnables are unavailable.");
        for (var index = 0; index < spawnables.GetObjectCount(); index++)
        {
            var candidate = spawnables.GetObject(true, index);
            if (candidate == networkObject ||
                (candidate?.gameObject != null &&
                 string.Equals(candidate.gameObject.name, networkObject.gameObject.name, StringComparison.Ordinal)))
            {
                return;
            }
        }

        spawnables.AddObject(networkObject);
    }
}
