using System;
using System.IO;
using System.Text;
using HarmonyLib;
using MelonLoader;
using S1Forklift.Runtime;
using UnityEngine;
using UnityEngine.SceneManagement;

#if IL2CPP
using Il2CppInterop.Runtime.InteropTypes;
using FishNetNetworkObject = Il2CppFishNet.Object.NetworkObject;
using S1BuildStartGrid = Il2CppScheduleOne.Building.BuildStart_Grid;
using S1BuildManager = Il2CppScheduleOne.Building.BuildManager;
using S1BuildableItemDefinition = Il2CppScheduleOne.ItemFramework.BuildableItemDefinition;
using S1CashInstance = Il2CppScheduleOne.ItemFramework.CashInstance;
using S1DevUtilities = Il2CppScheduleOne.DevUtilities;
using S1Equippable = Il2CppScheduleOne.Equipping.Equippable;
using S1Grid = Il2CppScheduleOne.Tiles.Grid;
using S1GridItem = Il2CppScheduleOne.EntityFramework.GridItem;
using S1AdvancedTransitRoute = Il2CppScheduleOne.Management.AdvancedTransitRoute;
using S1ITransitEntity = Il2CppScheduleOne.Management.ITransitEntity;
using S1InteractableObject = Il2CppScheduleOne.Interaction.InteractableObject;
using S1LandVehicle = Il2CppScheduleOne.Vehicles.LandVehicle;
using S1LoadManager = Il2CppScheduleOne.Persistence.LoadManager;
using S1LoadingDock = Il2CppScheduleOne.Delivery.LoadingDock;
using S1LoadingScreen = Il2CppScheduleOne.UI.LoadingScreen;
using S1NavMeshUtility = Il2CppScheduleOne.DevUtilities.NavMeshUtility;
using S1CheckpointBehaviour = Il2CppScheduleOne.NPCs.Behaviour.CheckpointBehaviour;
using S1Packager = Il2CppScheduleOne.Employees.Packager;
using S1PackagerConfiguration = Il2CppScheduleOne.Management.PackagerConfiguration;
using S1PlaceableStorageEntity = Il2CppScheduleOne.ObjectScripts.PlaceableStorageEntity;
using S1Player = Il2CppScheduleOne.PlayerScripts.Player;
using S1PlayerMovement = Il2CppScheduleOne.PlayerScripts.PlayerMovement;
using S1Registry = Il2CppScheduleOne.Registry;
using S1StorageMenu = Il2CppScheduleOne.UI.StorageMenu;
using S1StorageEntityInteractable = Il2CppScheduleOne.Storage.StorageEntityInteractable;
using S1StorageEntityVisualizer = Il2CppScheduleOne.Storage.StorageEntityVisualizer;
using S1Coordinate = Il2CppScheduleOne.Tiles.Coordinate;
using S1ShopInterface = Il2CppScheduleOne.UI.Shop.ShopInterface;
using S1VehicleDetector = Il2CppScheduleOne.DevUtilities.VehicleDetector;
using S1VehicleManager = Il2CppScheduleOne.Vehicles.VehicleManager;
#else
using FishNetNetworkObject = FishNet.Object.NetworkObject;
using S1BuildStartGrid = ScheduleOne.Building.BuildStart_Grid;
using S1BuildManager = ScheduleOne.Building.BuildManager;
using S1BuildableItemDefinition = ScheduleOne.ItemFramework.BuildableItemDefinition;
using S1CashInstance = ScheduleOne.ItemFramework.CashInstance;
using S1DevUtilities = ScheduleOne.DevUtilities;
using S1Equippable = ScheduleOne.Equipping.Equippable;
using S1Grid = ScheduleOne.Tiles.Grid;
using S1GridItem = ScheduleOne.EntityFramework.GridItem;
using S1AdvancedTransitRoute = ScheduleOne.Management.AdvancedTransitRoute;
using S1ITransitEntity = ScheduleOne.Management.ITransitEntity;
using S1InteractableObject = ScheduleOne.Interaction.InteractableObject;
using S1LandVehicle = ScheduleOne.Vehicles.LandVehicle;
using S1LoadManager = ScheduleOne.Persistence.LoadManager;
using S1LoadingDock = ScheduleOne.Delivery.LoadingDock;
using S1LoadingScreen = ScheduleOne.UI.LoadingScreen;
using S1NavMeshUtility = ScheduleOne.DevUtilities.NavMeshUtility;
using S1CheckpointBehaviour = ScheduleOne.NPCs.Behaviour.CheckpointBehaviour;
using S1Packager = ScheduleOne.Employees.Packager;
using S1PackagerConfiguration = ScheduleOne.Management.PackagerConfiguration;
using S1PlaceableStorageEntity = ScheduleOne.ObjectScripts.PlaceableStorageEntity;
using S1Player = ScheduleOne.PlayerScripts.Player;
using S1PlayerMovement = ScheduleOne.PlayerScripts.PlayerMovement;
using S1Registry = ScheduleOne.Registry;
using S1StorageMenu = ScheduleOne.UI.StorageMenu;
using S1StorageEntityInteractable = ScheduleOne.Storage.StorageEntityInteractable;
using S1StorageEntityVisualizer = ScheduleOne.Storage.StorageEntityVisualizer;
using S1Coordinate = ScheduleOne.Tiles.Coordinate;
using S1ShopInterface = ScheduleOne.UI.Shop.ShopInterface;
using S1VehicleDetector = ScheduleOne.DevUtilities.VehicleDetector;
using S1VehicleManager = ScheduleOne.Vehicles.VehicleManager;
#endif

namespace S1Forklift;

internal sealed class ForkliftSmokeProbe
{
    private const float CashPerPalletSlot = 1000f;

    private enum Phase
    {
        AwaitMenu,
        AwaitGameplay,
        VerifyEmployeeTransit,
        PrepareLoadingDockTransit,
        VerifyLoadingDockTransit,
        MoveToMotel,
        SettleAtMotel,
        AwaitPrefab,
        VerifyPalletPresentation,
        CapturePalletPresentation,
        SettleVehicle,
        VerifyDriverAvatar,
        CaptureDriverAvatar,
        Drive,
        SettleCargo,
        RaiseCargo,
        CaptureRaised,
        LowerFork,
        SettlePallet,
        RelocateVisualAudit,
        CaptureFrontLeft,
        CaptureRearRight,
        CaptureSide,
    }

    private readonly string _outputDirectory;
    private readonly string _resultPath;
    private readonly bool _avatarOnly;
    private readonly bool _palletAlignmentOnly;
    private readonly bool _employeeTransitOnly;
    private readonly bool _loadingDockOnly;
    private readonly string? _sourceSavePath;
    private Phase _phase;
    private float _phaseStarted;
    private DateTime _phaseStartedUtc;
    private float _totalElapsed;
    private bool _loadRequested;
    private Transform? _motelTeleportPoint;
    private S1LandVehicle? _vehicle;
    private GameObject? _cargo;
    private S1PlaceableStorageEntity? _pallet;
    private string? _palletStoredItemId;
    private Vector3 _driveStart;
    private float _forkStartHeight;
    private float _cargoStartHeight;
    private float _nextPrefabAttempt;
    private bool _palletGhostVerified;
    private bool _palletPresentationVerified;
    private bool _palletStorageMenuOpen;
    private S1Equippable? _palletPresentationEquippable;
    private bool _finished;
    private GameObject? _avatarAuditCamera;
    private GameObject? _palletAuditCamera;
    private S1VehicleDetector? _vehicleDetectorProbe;
    private S1Packager? _loadingDockPackager;
    private S1ITransitEntity? _loadingDockSource;
    private S1LoadingDock? _loadingDockTarget;

    private ForkliftSmokeProbe(
        string outputDirectory,
        bool avatarOnly,
        bool palletAlignmentOnly,
        bool employeeTransitOnly,
        bool loadingDockOnly,
        string? sourceSavePath)
    {
        _outputDirectory = outputDirectory;
        _avatarOnly = avatarOnly;
        _palletAlignmentOnly = palletAlignmentOnly;
        _employeeTransitOnly = employeeTransitOnly;
        _loadingDockOnly = loadingDockOnly;
        _sourceSavePath = sourceSavePath;
        Directory.CreateDirectory(outputDirectory);
        _resultPath = Path.Combine(outputDirectory, "result.txt");
        if (File.Exists(_resultPath))
            File.Delete(_resultPath);
        Enter(Phase.AwaitMenu);
    }

    internal static ForkliftSmokeProbe? TryCreate()
    {
        var args = Environment.GetCommandLineArgs();
        var enabled = false;
        var avatarOnly = false;
        var palletAlignmentOnly = false;
        var employeeTransitOnly = false;
        var loadingDockOnly = false;
        string? sourceSavePath = null;
        var output = Path.Combine(Path.GetTempPath(), "S1Forklift.Smoke");
        for (var index = 0; index < args.Length; index++)
        {
            if (string.Equals(args[index], "--s1forklift-smoke", StringComparison.OrdinalIgnoreCase))
                enabled = true;
            else if (string.Equals(
                         args[index],
                         "--s1forklift-avatar-smoke",
                         StringComparison.OrdinalIgnoreCase))
            {
                enabled = true;
                avatarOnly = true;
            }
            else if (string.Equals(
                         args[index],
                         "--s1forklift-pallet-alignment-smoke",
                         StringComparison.OrdinalIgnoreCase))
            {
                enabled = true;
                palletAlignmentOnly = true;
            }
            else if (string.Equals(
                         args[index],
                         "--s1forklift-employee-transit-smoke",
                         StringComparison.OrdinalIgnoreCase))
            {
                enabled = true;
                employeeTransitOnly = true;
            }
            else if (string.Equals(
                         args[index],
                         "--s1forklift-loading-dock-smoke",
                         StringComparison.OrdinalIgnoreCase))
            {
                enabled = true;
                loadingDockOnly = true;
            }
            else if (string.Equals(
                         args[index],
                         "--s1forklift-smoke-source-save",
                         StringComparison.OrdinalIgnoreCase) &&
                     index + 1 < args.Length)
            {
                sourceSavePath = args[++index];
            }
            else if (string.Equals(args[index], "--s1forklift-smoke-dir", StringComparison.OrdinalIgnoreCase) &&
                     index + 1 < args.Length)
                output = args[++index];
        }

        if (!enabled)
            return null;

        MelonLogger.Msg(
            $"[S1ForkliftSmoke] Enabled; AvatarOnly={avatarOnly}; " +
            $"PalletAlignmentOnly={palletAlignmentOnly}; " +
            $"EmployeeTransitOnly={employeeTransitOnly}; " +
            $"LoadingDockOnly={loadingDockOnly}; output='{output}'.");
        return new ForkliftSmokeProbe(
            output,
            avatarOnly,
            palletAlignmentOnly,
            employeeTransitOnly,
            loadingDockOnly,
            sourceSavePath);
    }

    internal void Update()
    {
        if (_finished)
            return;

        _totalElapsed += Time.unscaledDeltaTime;
        if (_totalElapsed > 180f)
        {
            Finish(false, $"Timeout|Phase={_phase}|Scene={SceneManager.GetActiveScene().name}");
            return;
        }

        try
        {
            switch (_phase)
            {
                case Phase.AwaitMenu:
                    UpdateAwaitMenu();
                    break;
                case Phase.AwaitGameplay:
                    UpdateAwaitGameplay();
                    break;
                case Phase.VerifyEmployeeTransit:
                    UpdateVerifyEmployeeTransit();
                    break;
                case Phase.PrepareLoadingDockTransit:
                    UpdatePrepareLoadingDockTransit();
                    break;
                case Phase.VerifyLoadingDockTransit:
                    UpdateVerifyLoadingDockTransit();
                    break;
                case Phase.MoveToMotel:
                    UpdateMoveToMotel();
                    break;
                case Phase.SettleAtMotel:
                    UpdateSettleAtMotel();
                    break;
                case Phase.AwaitPrefab:
                    UpdateAwaitPrefab();
                    break;
                case Phase.VerifyPalletPresentation:
                    UpdateVerifyPalletPresentation();
                    break;
                case Phase.CapturePalletPresentation:
                    UpdateCapturePalletPresentation();
                    break;
                case Phase.SettleVehicle:
                    UpdateSettleVehicle();
                    break;
                case Phase.VerifyDriverAvatar:
                    UpdateVerifyDriverAvatar();
                    break;
                case Phase.CaptureDriverAvatar:
                    UpdateCaptureDriverAvatar();
                    break;
                case Phase.Drive:
                    UpdateDrive();
                    break;
                case Phase.SettleCargo:
                    UpdateSettleCargo();
                    break;
                case Phase.RaiseCargo:
                    UpdateRaiseCargo();
                    break;
                case Phase.CaptureRaised:
                    UpdateCaptureRaised();
                    break;
                case Phase.LowerFork:
                    UpdateLowerFork();
                    break;
                case Phase.SettlePallet:
                    UpdateSettlePallet();
                    break;
                case Phase.RelocateVisualAudit:
                    UpdateRelocateVisualAudit();
                    break;
                case Phase.CaptureFrontLeft:
                    UpdateCaptureFrontLeft();
                    break;
                case Phase.CaptureRearRight:
                    UpdateCaptureRearRight();
                    break;
                case Phase.CaptureSide:
                    UpdateCaptureSide();
                    break;
            }
        }
        catch (Exception ex)
        {
            MelonLogger.Error($"[S1ForkliftSmoke] EXCEPTION_STATE|Phase={_phase}|{ex}");
            Finish(false, $"Exception|Phase={_phase}|Type={ex.GetType().Name}|Message={ex.Message}");
        }
    }

    private void UpdateAwaitMenu()
    {
        var loadManager = S1LoadManager.Instance;
        if (loadManager == null || loadManager.IsLoading || _loadRequested)
            return;

        var save = DisposableSmokeSave.Create(_outputDirectory, _sourceSavePath);
        DisposableSmokeSave.EnableDebugMode(loadManager);
        _loadRequested = true;
        loadManager.StartGame(save, false, false);
        MelonLogger.Msg("[S1ForkliftSmoke] STATE|LoadRequested");
        Enter(Phase.AwaitGameplay);
    }

    private void UpdateAwaitGameplay()
    {
        var loadManager = S1LoadManager.Instance;
        var loadingScreenOpen =
            S1DevUtilities.Singleton<S1LoadingScreen>.InstanceExists &&
            S1DevUtilities.Singleton<S1LoadingScreen>.Instance.IsOpen;
        if (SceneManager.GetActiveScene().name != "Main" ||
            loadManager == null ||
            loadManager.IsLoading ||
            !loadManager.IsGameLoaded ||
            !loadManager.IsInGameScene ||
            loadingScreenOpen ||
            S1Player.Local == null)
        {
            return;
        }

        Enter(
            _loadingDockOnly
                ? Phase.PrepareLoadingDockTransit
                : _employeeTransitOnly
                    ? Phase.VerifyEmployeeTransit
                    : Phase.MoveToMotel);
    }

    private void UpdatePrepareLoadingDockTransit()
    {
        if (PhaseElapsed < 2f)
            return;
        if (!PalletItemRuntime.TryVerify(out var palletDetail))
        {
            if (PhaseElapsed < 10f)
                return;
            throw new InvalidOperationException($"Pallet integration is not ready: {palletDetail}");
        }
        if (!ForkliftPrefabFactory.TryEnsureRegistered(new MelonLogger.Instance("S1ForkliftSmoke")))
            return;

        var manager = S1VehicleManager.Instance
                      ?? throw new InvalidOperationException("VehicleManager is unavailable.");
        for (var index = 0; index < manager.AllVehicles.Count; index++)
        {
            var candidate = manager.AllVehicles[index];
            if (candidate != null &&
                string.Equals(
                    candidate.VehicleCode,
                    Constants.VehicleCode,
                    StringComparison.OrdinalIgnoreCase))
            {
                _vehicle = candidate;
                break;
            }
        }
        if (_vehicle == null)
            throw new InvalidOperationException("The source save has no forklift near its loading dock.");

        var packagers = Resources.FindObjectsOfTypeAll<S1Packager>();
        for (var packagerIndex = 0; packagerIndex < packagers.Length && _pallet == null; packagerIndex++)
        {
            var packager = packagers[packagerIndex];
            var configuration = GetPackagerConfiguration(packager);
            if (configuration?.Routes == null)
                continue;

            var routes = configuration.Routes.Routes;
            for (var routeIndex = 0; routeIndex < routes.Count; routeIndex++)
            {
                var route = routes[routeIndex];
                if (route?.GetItemReadyToMove() == null ||
                    !TryGetPallet(route.Destination, out var destinationPallet))
                {
                    continue;
                }

                _loadingDockPackager = packager;
                _loadingDockSource = route.Source;
                _pallet = destinationPallet;
                break;
            }
        }

        if (_loadingDockPackager == null || _loadingDockSource == null || _pallet == null)
        {
            throw new InvalidOperationException(
                "The source save has no ready packager route ending at an empty pallet.");
        }

        var docks = Resources.FindObjectsOfTypeAll<S1LoadingDock>();
        for (var index = 0; index < docks.Length; index++)
        {
            var dock = docks[index];
            if (dock != null &&
                dock.ParentProperty == _loadingDockPackager.ParentProperty &&
                dock.StaticOccupant == null &&
                dock.Parking != null &&
                dock.Parking.ParkingSpots.Count > 0)
            {
                _loadingDockTarget = dock;
                break;
            }
        }
        if (_loadingDockTarget == null)
        {
            throw new InvalidOperationException(
                "The packager's property has no available loading dock parking spot.");
        }

        _vehicle.Rb.velocity = Vector3.zero;
        _vehicle.Rb.angularVelocity = Vector3.zero;
        var parkingSpot = _loadingDockTarget.Parking.ParkingSpots[0];
        _loadingDockTarget.VehicleDetector.Clear();
        _vehicle.AlignTo(parkingSpot.AlignmentPoint, parkingSpot.Alignment);
        Physics.SyncTransforms();
        _loadingDockTarget.VehicleDetector.SetIgnoreNewCollisions(false);
        if (!PalletCarryRuntime.TryPositionAndPickupForSmoke(
                _vehicle,
                _pallet,
                out var pickupDetail))
        {
            throw new InvalidOperationException(
                $"Could not stage the route pallet on the saved forklift: {pickupDetail}");
        }

        PalletCarryRuntime.SynchronizeForSmoke(_vehicle);
        Physics.SyncTransforms();
        MelonLogger.Msg(
            $"[S1ForkliftSmoke] STATE|LoadingDockPalletStaged|" +
            $"Dock={_loadingDockTarget.Name}|Vehicle={_vehicle.GUID}|" +
            $"Pallet={_pallet.GUID}|DetectedVehicles=" +
            $"{_loadingDockTarget.VehicleDetector.vehicles.Count}|{pickupDetail}");
        Enter(Phase.VerifyLoadingDockTransit);
    }

    private void UpdateVerifyLoadingDockTransit()
    {
        if (PhaseElapsed < 2f ||
            _vehicle == null ||
            _pallet == null ||
            _loadingDockPackager == null ||
            _loadingDockSource == null ||
            _loadingDockTarget == null)
        {
            return;
        }

        if (_loadingDockTarget.DynamicOccupant != _vehicle)
        {
            if (PhaseElapsed < 10f)
                return;
            throw new InvalidOperationException(
                $"The staged forklift was not detected in {_loadingDockTarget.Name}. " +
                $"DetectorVehicles={_loadingDockTarget.VehicleDetector.vehicles.Count}.");
        }

        var occupiedDock = _loadingDockTarget;

        if (!LoadingDockPalletRuntime.TryVerifyForSmoke(
                occupiedDock,
                _vehicle,
                out var boundPallet,
                out var bindingDetail))
        {
            throw new InvalidOperationException(
                $"The loading dock did not expose the carried pallet: {bindingDetail}");
        }
        if (boundPallet != _pallet)
            throw new InvalidOperationException("The loading dock exposed the wrong carried pallet.");

        var dockTransit = GetLoadingDockTransitEntity(occupiedDock);
        var route = new S1AdvancedTransitRoute(_loadingDockSource, dockTransit);
        var readyItem = route.GetItemReadyToMove()
                        ?? throw new InvalidOperationException(
                            "The loading-dock route found no item ready to move.");
        if (!_loadingDockPackager.MoveItemBehaviour.IsTransitRouteValid(
                route,
                readyItem,
                out var invalidReason))
        {
            throw new InvalidOperationException(
                $"The employee route to the occupied loading dock is invalid: {invalidReason}");
        }

        var quantityBefore = GetStoredQuantity(_pallet, readyItem.ID);
        dockTransit.InsertItemIntoInput(readyItem.GetCopy(1), _loadingDockPackager);
        var quantityAfter = GetStoredQuantity(_pallet, readyItem.ID);
        if (quantityAfter != quantityBefore + 1)
        {
            throw new InvalidOperationException(
                $"Loading through the dock changed pallet quantity from " +
                $"{quantityBefore} to {quantityAfter}; expected {quantityBefore + 1}.");
        }

        var dockAccessPoint = occupiedDock.AccessPoints[0];
        var nativeRearSearchPoint =
            _vehicle.transform.position -
            _vehicle.transform.forward * (_vehicle.BoundingBoxDimensions.z / 2f + 0.75f);
        AssertPalletInteractionPoint(
            "loading dock",
            _vehicle,
            _pallet,
            dockAccessPoint.position,
            nativeRearSearchPoint);

        var illicitDefinition = S1Registry.GetItem("cocaine")
                                ?? throw new InvalidOperationException(
                                    "Police smoke item 'cocaine' is unavailable.");
        var illicitItem = illicitDefinition.GetDefaultInstance();
        var illicitSlotFound = false;
        for (var index = 0; index < _pallet.StorageEntity.ItemSlots.Count; index++)
        {
            var slot = _pallet.StorageEntity.ItemSlots[index];
            if (slot.ItemInstance != null)
                continue;

            slot.SetStoredItem(illicitItem);
            illicitSlotFound = true;
            break;
        }
        if (!illicitSlotFound)
            throw new InvalidOperationException("The carried pallet has no slot for the police probe item.");

        var checkpointBehaviours = Resources.FindObjectsOfTypeAll<S1CheckpointBehaviour>();
        if (checkpointBehaviours.Length == 0)
            throw new InvalidOperationException("No checkpoint behaviour exists for the police search probe.");

        var checkpointBehaviour = checkpointBehaviours[0];
        var searchedVehicleSetter = AccessTools.PropertySetter(
            typeof(S1CheckpointBehaviour),
            nameof(S1CheckpointBehaviour.CurrentSearchedVehicle))
            ?? throw new MissingMethodException(
                typeof(S1CheckpointBehaviour).FullName,
                $"set_{nameof(S1CheckpointBehaviour.CurrentSearchedVehicle)}");
        var getSearchPoint = AccessTools.Method(typeof(S1CheckpointBehaviour), "GetSearchPoint")
                             ?? throw new MissingMethodException(
                                 typeof(S1CheckpointBehaviour).FullName,
                                 "GetSearchPoint");
        var containsIllicitItems = AccessTools.Method(
                                       typeof(S1CheckpointBehaviour),
                                       "DoesVehicleContainIllicitItems")
                                   ?? throw new MissingMethodException(
                                       typeof(S1CheckpointBehaviour).FullName,
                                       "DoesVehicleContainIllicitItems");
        var previousSearchedVehicle = checkpointBehaviour.CurrentSearchedVehicle;
        Vector3 policeSearchPoint;
        bool policeDetectedPalletContents;
        try
        {
            searchedVehicleSetter.Invoke(checkpointBehaviour, new object[] { _vehicle });
            policeSearchPoint = (Vector3)(getSearchPoint.Invoke(checkpointBehaviour, null)
                                ?? throw new InvalidOperationException(
                                    "Police search point returned null."));
            policeDetectedPalletContents =
                (bool)(containsIllicitItems.Invoke(checkpointBehaviour, null)
                       ?? throw new InvalidOperationException(
                           "Police contraband check returned null."));
        }
        finally
        {
            searchedVehicleSetter.Invoke(
                checkpointBehaviour,
                new object?[] { previousSearchedVehicle });
        }

        AssertPalletInteractionPoint(
            "police search",
            _vehicle,
            _pallet,
            policeSearchPoint,
            nativeRearSearchPoint);
        if (!policeDetectedPalletContents)
        {
            throw new InvalidOperationException(
                $"Police did not detect '{illicitItem.ID}' in the carried pallet.");
        }
        var wasSearching = checkpointBehaviour.IsSearching;
        var searchingSetter = AccessTools.PropertySetter(
            typeof(S1CheckpointBehaviour),
            nameof(S1CheckpointBehaviour.IsSearching))
            ?? throw new MissingMethodException(
                typeof(S1CheckpointBehaviour).FullName,
                $"set_{nameof(S1CheckpointBehaviour.IsSearching)}");
        try
        {
            searchingSetter.Invoke(checkpointBehaviour, new object[] { true });
            searchedVehicleSetter.Invoke(checkpointBehaviour, new object[] { _vehicle });
            PalletInteractionRuntime.PreparePoliceSearchTick(checkpointBehaviour);
            _vehicle.Trunk.SetIsOpen(true);
            if (_vehicle.Trunk.IsOpen)
            {
                throw new InvalidOperationException(
                    "Police pallet search still opened the forklift trunk.");
            }
        }
        finally
        {
            PalletInteractionRuntime.ClearPoliceSearchTick();
            searchingSetter.Invoke(checkpointBehaviour, new object[] { wasSearching });
            searchedVehicleSetter.Invoke(
                checkpointBehaviour,
                new object?[] { previousSearchedVehicle });
        }

        var shopDetail = VerifyNativeShopPalletSlots(_vehicle, _pallet);

        MelonLogger.Msg(
            $"[S1ForkliftSmoke] STATE|LoadingDockEmployeeRouteReady|" +
            $"{bindingDetail}|Item={readyItem.ID}|" +
            $"Quantity={quantityBefore}->{quantityAfter}|" +
            $"DockAccess={dockAccessPoint.position}|PoliceAccess={policeSearchPoint}|" +
            $"PoliceItem={illicitItem.ID}|" +
            $"PoliceDetectedPalletContents={policeDetectedPalletContents}|" +
            $"TrunkOpened={_vehicle.Trunk.IsOpen}|{shopDetail}");
        Finish(
            true,
            $"Scene=Main|LoadingDockPalletTransit=Passed|Dock={occupiedDock.Name}|" +
            $"InputSlots={occupiedDock.InputSlots.Count}|Item={readyItem.ID}|" +
            $"PalletQuantity={quantityBefore}->{quantityAfter}|" +
            $"PalletAccess=Passed|PolicePalletSearch=Passed|NativeShopPalletLoading=Passed");
    }

    private static string VerifyNativeShopPalletSlots(
        S1LandVehicle vehicle,
        S1PlaceableStorageEntity pallet)
    {
        if (vehicle.Storage == null)
            throw new InvalidOperationException("The forklift has no native Storage component.");
        if (vehicle.Storage.SlotCount != 0 || vehicle.Storage.ItemSlots.Count != 0)
        {
            throw new InvalidOperationException(
                $"The forklift still exposes inherited vehicle storage. " +
                $"SlotCount={vehicle.Storage.SlotCount}|Slots={vehicle.Storage.ItemSlots.Count}.");
        }

        var shops = Resources.FindObjectsOfTypeAll<S1ShopInterface>();
        S1ShopInterface? shop = null;
        for (var index = 0; index < shops.Length; index++)
        {
            var candidate = shops[index];
            if (candidate != null &&
                candidate.LoadingBayDetector != null &&
                candidate.Cart != null &&
                candidate.Cart.LoadVehicleToggle != null)
            {
                shop = candidate;
                break;
            }
        }
        if (shop == null)
            throw new InvalidOperationException("No native shop with a vehicle loading bay is available.");
        if (!vehicle.IsPlayerOwned)
            throw new InvalidOperationException("The smoke forklift is not player-owned.");

        var detector = shop.LoadingBayDetector;
        var previousClosestVehicle = detector.closestVehicle;
        var previousToggle = shop.Cart.LoadVehicleToggle.isOn;
        try
        {
            detector.closestVehicle = vehicle;
            shop.Cart.LoadVehicleToggle.SetIsOnWithoutNotify(true);

            if (shop.GetLoadingBayVehicle() != vehicle)
            {
                throw new InvalidOperationException(
                    "The native shop did not accept the pallet-carrying forklift as its loading target.");
            }

            var availableSlots = shop.GetAvailableSlots();
            var palletSlots = pallet.StorageEntity.ItemSlots;
            if (availableSlots.Count < palletSlots.Count)
            {
                throw new InvalidOperationException(
                    $"The shop exposed {availableSlots.Count} slots for a " +
                    $"{palletSlots.Count}-slot carried pallet.");
            }

            for (var index = 0; index < palletSlots.Count; index++)
            {
                if (availableSlots[index] != palletSlots[index])
                {
                    throw new InvalidOperationException(
                        $"Native shop slot {index} does not reference the carried pallet.");
                }
            }

            return $"Shop={shop.name}|ForkliftSlots=0|PalletShopSlots={palletSlots.Count}";
        }
        finally
        {
            detector.closestVehicle = previousClosestVehicle;
            shop.Cart.LoadVehicleToggle.SetIsOnWithoutNotify(previousToggle);
        }
    }

    private static void AssertPalletInteractionPoint(
        string context,
        S1LandVehicle vehicle,
        S1PlaceableStorageEntity pallet,
        Vector3 interactionPoint,
        Vector3 nativeRearPoint)
    {
        var forwardDistance = Vector3.Dot(
            interactionPoint - vehicle.transform.position,
            vehicle.transform.forward);
        var palletDistance = Vector3.Distance(interactionPoint, pallet.transform.position);
        var nativeRearDistance = Vector3.Distance(nativeRearPoint, pallet.transform.position);
        if (forwardDistance <= 0f || palletDistance >= nativeRearDistance)
        {
            throw new InvalidOperationException(
                $"The {context} point is not at the pallet on the forks. " +
                $"Point={interactionPoint}|ForwardDistance={forwardDistance:F2}|" +
                $"PalletDistance={palletDistance:F2}|RearDistance={nativeRearDistance:F2}.");
        }
    }

    private static S1ITransitEntity GetLoadingDockTransitEntity(S1LoadingDock dock)
    {
#if IL2CPP
        return dock.TryCast<S1ITransitEntity>()
               ?? throw new InvalidOperationException(
                   "Loading dock does not expose its ITransitEntity interface.");
#else
        return dock;
#endif
    }

    private static int GetStoredQuantity(
        S1PlaceableStorageEntity pallet,
        string itemId)
    {
        var quantity = 0;
        var slots = pallet.StorageEntity.ItemSlots;
        for (var index = 0; index < slots.Count; index++)
        {
            var item = slots[index].ItemInstance;
            if (item != null &&
                string.Equals(item.ID, itemId, StringComparison.OrdinalIgnoreCase))
            {
                quantity += item.Quantity;
            }
        }

        return quantity;
    }

    private void UpdateVerifyEmployeeTransit()
    {
        if (PhaseElapsed < 2f)
            return;

        var packagers = Resources.FindObjectsOfTypeAll<S1Packager>();
        var configuredPackagers = 0;
        var palletRoutes = 0;
        var reachableEndpoints = 0;
        var outlinedPallets = 0;
        var validReadyRoutes = 0;
        for (var packagerIndex = 0; packagerIndex < packagers.Length; packagerIndex++)
        {
            var packager = packagers[packagerIndex];
            var configuration = GetPackagerConfiguration(packager);
            if (configuration == null || configuration.Routes == null)
                continue;

            configuredPackagers++;
            var routes = configuration.Routes.Routes;
            for (var routeIndex = 0; routeIndex < routes.Count; routeIndex++)
            {
                var route = routes[routeIndex];
                if (route == null ||
                    (!IsPalletTransitEntity(route.Source) &&
                     !IsPalletTransitEntity(route.Destination)))
                {
                    continue;
                }

                palletRoutes++;
                reachableEndpoints += AssertReachablePalletEndpoint(
                    packager,
                    route.Source,
                    "Source",
                    routeIndex);
                reachableEndpoints += AssertReachablePalletEndpoint(
                    packager,
                    route.Destination,
                    "Destination",
                    routeIndex);

                outlinedPallets += AssertPalletOutline(route.Source);
                outlinedPallets += AssertPalletOutline(route.Destination);

                var readyItem = route.GetItemReadyToMove();
                if (readyItem != null)
                {
                    if (!packager.MoveItemBehaviour.IsTransitRouteValid(
                            route,
                            readyItem,
                            out var invalidReason))
                    {
                        throw new InvalidOperationException(
                            $"Packager route {routeIndex} remains invalid: {invalidReason}");
                    }

                    validReadyRoutes++;
                    MelonLogger.Msg(
                        $"[S1ForkliftSmoke] STATE|EmployeePalletRouteValid|" +
                        $"Route={routeIndex}|Item={readyItem.ID}|Quantity={readyItem.Quantity}");
                }
            }
        }

        if (configuredPackagers == 0)
            throw new InvalidOperationException("The source save did not load a configured packager.");
        if (palletRoutes == 0)
            throw new InvalidOperationException("The source save did not load any packager pallet routes.");
        if (validReadyRoutes == 0)
            throw new InvalidOperationException("No pallet route had an item ready to move.");

        MelonLogger.Msg(
            $"[S1ForkliftSmoke] STATE|EmployeePalletTransit|" +
            $"Packagers={configuredPackagers}|Routes={palletRoutes}|" +
            $"ValidReadyRoutes={validReadyRoutes}|ReachableEndpoints={reachableEndpoints}|" +
            $"OutlinedPalletEndpoints={outlinedPallets}");
        Finish(
            true,
            $"Scene=Main|EmployeeTransit=Passed|Packagers={configuredPackagers}|" +
            $"PalletRoutes={palletRoutes}|ValidReadyRoutes={validReadyRoutes}|" +
            $"ReachableEndpoints={reachableEndpoints}|" +
            $"OutlinedPalletEndpoints={outlinedPallets}");
    }

    private static S1PackagerConfiguration? GetPackagerConfiguration(S1Packager packager)
    {
#if IL2CPP
        return packager.Configuration?.TryCast<S1PackagerConfiguration>();
#else
        return packager.Configuration as S1PackagerConfiguration;
#endif
    }

    private static int AssertReachablePalletEndpoint(
        S1Packager packager,
        S1ITransitEntity? entity,
        string endpointName,
        int routeIndex)
    {
        if (!IsPalletTransitEntity(entity))
            return 0;

        var accessPoint = S1NavMeshUtility.GetReachableAccessPoint(entity, packager);
        if (accessPoint == null)
        {
            throw new InvalidOperationException(
                $"Packager route {routeIndex} cannot reach its pallet {endpointName.ToLowerInvariant()}.");
        }

        MelonLogger.Msg(
            $"[S1ForkliftSmoke] STATE|EmployeePalletEndpointReachable|" +
            $"Route={routeIndex}|Endpoint={endpointName}|Access={accessPoint.position}");
        return 1;
    }

    private static int AssertPalletOutline(S1ITransitEntity? entity)
    {
        if (!TryGetPallet(entity, out var pallet))
            return 0;
        if (!PalletPrefabFactory.TryVerifyOutlineRuntime(pallet, out var detail))
            throw new InvalidOperationException($"Loaded pallet outline failed: {detail}");

        MelonLogger.Msg($"[S1ForkliftSmoke] STATE|LoadedPalletTransitOutline|{detail}");
        return 1;
    }

    private static bool IsPalletTransitEntity(S1ITransitEntity? entity)
    {
        return TryGetPallet(entity, out _);
    }

    private static bool TryGetPallet(
        S1ITransitEntity? entity,
        out S1PlaceableStorageEntity pallet)
    {
        pallet = null!;
        if (entity?.LinkOrigin == null)
            return false;

        var candidate = entity.LinkOrigin.GetComponentInParent<S1PlaceableStorageEntity>();
        if (!PalletCarryRuntime.IsPallet(candidate))
            return false;

        pallet = candidate!;
        return true;
    }

    private void UpdateMoveToMotel()
    {
        _motelTeleportPoint = ForkliftSpawnPlacement.FindTeleportLocation("Motel");
        if (_motelTeleportPoint == null ||
            !S1DevUtilities.PlayerSingleton<S1PlayerMovement>.InstanceExists)
        {
            return;
        }
        if (!ForkliftSpawnPlacement.TryFindClearPlayerPose(
                _motelTeleportPoint.position,
                _motelTeleportPoint.forward,
                out var safePosition,
                out var safeRotation))
        {
            throw new InvalidOperationException(
                "No clear grounded player pose was found near the Motel teleport anchor.");
        }

        var movement = S1DevUtilities.PlayerSingleton<S1PlayerMovement>.Instance;
        movement.Teleport(safePosition, false);
        movement.SetPlayerRotation(safeRotation);
        MelonLogger.Msg(
            $"[S1ForkliftSmoke] STATE|TeleportedToMotel|" +
            $"Anchor={_motelTeleportPoint.position}|SafePosition={safePosition}");
        Enter(Phase.SettleAtMotel);
    }

    private void UpdateSettleAtMotel()
    {
        if (PhaseElapsed < 2f || _motelTeleportPoint == null)
            return;

        Enter(Phase.AwaitPrefab);
    }

    private void UpdateAwaitPrefab()
    {
        if (PhaseElapsed < 2f)
            return;
        if (Time.unscaledTime < _nextPrefabAttempt)
            return;
        _nextPrefabAttempt = Time.unscaledTime + 0.5f;

        if (!PalletItemRuntime.TryVerify(out var palletDetail))
        {
            if (PhaseElapsed < 10f)
                return;
            throw new InvalidOperationException($"Pallet integration is not ready: {palletDetail}");
        }
        MelonLogger.Msg($"[S1ForkliftSmoke] STATE|PalletIntegrationReady|{palletDetail}");
        if (!_palletGhostVerified)
        {
            AssertPalletPlacementGhost();
            _palletGhostVerified = true;
        }
        if (!_palletPresentationVerified)
        {
            BeginPalletPresentationTest();
            Enter(Phase.VerifyPalletPresentation);
            return;
        }

        if (!ForkliftPrefabFactory.TryEnsureRegistered(new MelonLogger.Instance("S1ForkliftSmoke")))
            return;

        var manager = S1VehicleManager.Instance;
        var player = S1Player.Local;
        if (manager == null || player == null)
            return;

        if (_motelTeleportPoint == null)
            throw new InvalidOperationException("Motel teleport point was lost.");
        if (!ForkliftSpawnPlacement.TryFindClearPose(
                _motelTeleportPoint.position,
                _motelTeleportPoint.forward,
                8f,
                out var position,
                out var rotation,
                out var groundDistance))
        {
            return;
        }

        var viewDirection = position - player.transform.position;
        viewDirection.y = 0f;
        if (viewDirection.sqrMagnitude > 0.01f)
        {
            S1DevUtilities.PlayerSingleton<S1PlayerMovement>.Instance.SetPlayerRotation(
                Quaternion.LookRotation(viewDirection.normalized, Vector3.up));
        }

        _vehicle = manager.SpawnAndReturnVehicle(
            Constants.VehicleCode,
            position,
            rotation,
            true);
        if (_vehicle == null)
            throw new InvalidOperationException("SpawnAndReturnVehicle returned null.");

        var networkObject = _vehicle.GetComponent<FishNetNetworkObject>();
        if (networkObject == null || !networkObject.IsSpawned)
            throw new InvalidOperationException("Forklift did not enter the FishNet spawned state.");
        if (!string.Equals(_vehicle.GetVehicleData().VehicleCode, Constants.VehicleCode, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("VehicleData did not preserve vehicle code 'forklift'.");

        _vehicleDetectorProbe = CreateVehicleDetectorProbe(_vehicle);

        _driveStart = _vehicle.transform.position;
        MelonLogger.Msg(
            $"[S1ForkliftSmoke] STATE|VehicleSpawnedOutsideMotel|" +
            $"Position={position}|ClearGroundDistance={groundDistance:F2}");
        Enter(Phase.SettleVehicle);
    }

    private void UpdateSettleVehicle()
    {
        if (PhaseElapsed < 4f || _vehicle == null)
            return;

        var manager = S1VehicleManager.Instance
                      ?? throw new InvalidOperationException("VehicleManager disappeared after spawn.");
        AssertSingleForkliftVisual(_vehicle, manager);
        AssertVisibleWheelRenderers(_vehicle);
        AssertVehicleDetectionCollider(_vehicle);
        AssertVehicleDetectorRecognition(_vehicle);
        AssertDrivetrainConfiguration(_vehicle, manager);
        if (_palletAlignmentOnly)
        {
            PreparePalletTest();
            Enter(Phase.SettleCargo);
            return;
        }
        if (_avatarOnly)
        {
            _vehicle.State.PushToDefaultParent();
            _vehicle.overrideControls = true;
            _vehicle.steerOverride = 0.65f;
            Enter(Phase.VerifyDriverAvatar);
            return;
        }

        _driveStart = _vehicle.transform.position;
        _vehicle.IsOccupied = true;
        _vehicle.overrideControls = true;
        _vehicle.throttleOverride = 0.45f;
        _vehicle.steerOverride = 0.05f;
        FrameVehicle(_vehicle);
        ScreenCapture.CaptureScreenshot(Path.Combine(_outputDirectory, "forklift-spawned.png"));
        Enter(Phase.Drive);
    }

    private void BeginPalletPresentationTest()
    {
        var item = S1Registry.GetItem(Constants.PalletItemId)
                   ?? throw new InvalidOperationException(
                       "Pallet definition disappeared before presentation testing.");
        var definition =
#if IL2CPP
            item.TryCast<S1BuildableItemDefinition>()
#else
            item as S1BuildableItemDefinition
#endif
            ?? throw new InvalidOperationException(
                "Pallet definition is not buildable.");
        if (definition.Equippable == null)
            throw new InvalidOperationException(
                "Pallet definition has no equippable.");

        var equippableObject = UnityEngine.Object.Instantiate(
            definition.Equippable.gameObject);
        equippableObject.name =
            "S1ForkliftSmoke_PalletPresentationEquippable";
        equippableObject.SetActive(true);
        _palletPresentationEquippable =
            equippableObject.GetComponent<S1Equippable>()
            ?? throw new InvalidOperationException(
                "Pallet equippable clone has no Equippable component.");
        _palletPresentationEquippable.Equip(
            definition.GetDefaultInstance());
    }

    private void UpdateVerifyPalletPresentation()
    {
        if (PhaseElapsed < 1f)
            return;

        var player = S1Player.Local
                     ?? throw new InvalidOperationException(
                         "Local player disappeared during pallet presentation testing.");
        var avatarEquippable = player.Avatar?.CurrentEquippable
                              ?? throw new InvalidOperationException(
                                  "Equipping the pallet did not create an avatar equippable.");
        if (!string.Equals(
                avatarEquippable.AssetPath,
                Constants.PalletAvatarResourcePath,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Unexpected pallet avatar path '{avatarEquippable.AssetPath}'.");
        }

        var ghosts = Resources.FindObjectsOfTypeAll<S1GridItem>();
        S1GridItem? palletGhost = null;
        Transform? visual = null;
        for (var index = 0; index < ghosts.Length; index++)
        {
            var candidate = ghosts[index];
            if (candidate == null || !candidate.isGhost)
                continue;

            var candidateVisual =
                TransformUtility.FindDirectChild(
                    candidate.transform,
                    "PalletVisual");
            if (candidateVisual == null)
                continue;

            palletGhost = candidate;
            visual = candidateVisual;
            break;
        }
        if (palletGhost == null || visual == null)
            throw new InvalidOperationException(
                "Equipping the pallet did not create its live placement ghost.");

        var visibleRenderers = 0;
        var bounds = default(Bounds);
        var renderers = visual.GetComponentsInChildren<Renderer>(true);
        for (var index = 0; index < renderers.Length; index++)
        {
            var renderer = renderers[index];
            if (!renderer.enabled ||
                !renderer.gameObject.activeInHierarchy)
            {
                continue;
            }

            if (visibleRenderers == 0)
                bounds = renderer.bounds;
            else
                bounds.Encapsulate(renderer.bounds);
            visibleRenderers++;
        }
        if (visibleRenderers == 0)
            throw new InvalidOperationException(
                "The live pallet placement ghost has no active renderers.");

        var camera = Camera.main
                     ?? throw new InvalidOperationException(
                         "Main camera is unavailable during pallet presentation testing.");
        var viewport = camera.WorldToViewportPoint(bounds.center);
        if (viewport.z <= 0f ||
            viewport.x < -0.1f || viewport.x > 1.1f ||
            viewport.y < -0.1f || viewport.y > 1.1f)
        {
            throw new InvalidOperationException(
                $"The pallet ghost is outside the camera view: {viewport}.");
        }

        ScreenCapture.CaptureScreenshot(
            Path.Combine(
                _outputDirectory,
                "pallet-live-placement-ghost.png"));
        MelonLogger.Msg(
            $"[S1ForkliftSmoke] STATE|PalletPresentationReady|" +
            $"AvatarPath={avatarEquippable.AssetPath}|" +
            $"GhostRenderers={visibleRenderers}|Viewport={viewport}");
        Enter(Phase.CapturePalletPresentation);
    }

    private void UpdateCapturePalletPresentation()
    {
        if (PhaseElapsed < 1f)
            return;

        var screenshotPath = Path.Combine(
            _outputDirectory,
            "pallet-live-placement-ghost.png");
        if (!File.Exists(screenshotPath) ||
            new FileInfo(screenshotPath).Length == 0)
        {
            throw new InvalidOperationException(
                "The live pallet placement screenshot was not written.");
        }
        _palletPresentationEquippable?.Unequip();
        _palletPresentationEquippable = null;
        _palletPresentationVerified = true;
        Enter(Phase.AwaitPrefab);
    }

    private static void AssertPalletPlacementGhost()
    {
        var item = S1Registry.GetItem(Constants.PalletItemId)
                   ?? throw new InvalidOperationException("Pallet definition disappeared.");
        var definition =
#if IL2CPP
            item.TryCast<S1BuildableItemDefinition>()
#else
            item as S1BuildableItemDefinition
#endif
            ?? throw new InvalidOperationException("Pallet definition is not buildable.");
        if (definition.BuiltItem == null || definition.BuiltItem.BuildHandler == null)
            throw new InvalidOperationException("Pallet build handler is unavailable.");

        GameObject? handlerObject = null;
        S1GridItem? ghost = null;
        try
        {
            handlerObject = UnityEngine.Object.Instantiate(definition.BuiltItem.BuildHandler);
            handlerObject.SetActive(false);
            var handler = handlerObject.GetComponent<S1BuildStartGrid>()
                          ?? throw new InvalidOperationException(
                              "Pallet build handler is not grid-based.");
            var createGhost = AccessTools.Method(
                                  typeof(S1BuildStartGrid),
                                  "CreateGhostModel")
                              ?? throw new MissingMethodException(
                                  typeof(S1BuildStartGrid).FullName,
                                  "CreateGhostModel");
            var ghostResult = createGhost.Invoke(handler, new object[] { definition });
            ghost =
#if IL2CPP
                (ghostResult as Il2CppSystem.Object)?.TryCast<S1GridItem>()
#else
                ghostResult as S1GridItem
#endif
                    ?? throw new InvalidOperationException(
                        "Native pallet ghost creation returned no GridItem.");
            var visual = TransformUtility.FindDirectChild(ghost.transform, "PalletVisual")
                         ?? throw new InvalidOperationException(
                             "Pallet placement ghost has no pallet visual.");
            var visibleRenderers = 0;
            var renderers = visual.GetComponentsInChildren<Renderer>(true);
            for (var index = 0; index < renderers.Length; index++)
            {
                if (renderers[index].enabled)
                    visibleRenderers++;
            }
            if (visibleRenderers == 0)
                throw new InvalidOperationException(
                    "Pallet placement ghost has no enabled pallet renderers.");

            MelonLogger.Msg(
                $"[S1ForkliftSmoke] STATE|PalletGhostReady|" +
                $"Visual={visual.name}|Renderers={visibleRenderers}");
        }
        finally
        {
            if (ghost != null)
                UnityEngine.Object.Destroy(ghost.gameObject);
            if (handlerObject != null)
                UnityEngine.Object.Destroy(handlerObject);
        }
    }

    private void UpdateVerifyDriverAvatar()
    {
        if (PhaseElapsed < 2f || _vehicle == null)
            return;

        var player = S1Player.Local
                     ?? throw new InvalidOperationException("Local player disappeared after vehicle entry.");
        if (player.CurrentVehicle != _vehicle.NetworkObject)
            throw new InvalidOperationException("Native entry did not assign the forklift as CurrentVehicle.");
        if (!ForkliftControlHintsRuntime.IsVisible)
            throw new InvalidOperationException("Forklift lift-control HUD prompts are not active.");
        MelonLogger.Msg(
            "[S1ForkliftSmoke] STATE|LiftControlPromptsVisible|" +
            $"Raise={ForkliftPreferences.RaiseLiftKey.Value}|" +
            $"Lower={ForkliftPreferences.LowerLiftKey.Value}");
        var avatar = ForkliftDriverAvatarRuntime.GetDriverProxyAvatar(player)
                     ?? throw new InvalidOperationException("Driver avatar proxy was not created.");
        if (!avatar.BodyContainer.gameObject.activeInHierarchy)
            throw new InvalidOperationException("Driver avatar proxy BodyContainer is inactive.");
        if (!ForkliftDriverAvatarRuntime.IsDriverProxyEquippableHidden(player))
            throw new InvalidOperationException("Driver avatar proxy retained a visible held item.");
        MelonLogger.Msg("[S1ForkliftSmoke] STATE|DriverHeldItemHidden=True");

        var visibleRenderers = 0;
        var nearbyRenderers = 0;
        var compactBodyRendererFound = false;
        var invisibleLayer = LayerMask.NameToLayer("Invisible");
        var seat = _vehicle.Seats[0];
        var diagnostics = new StringBuilder();
        diagnostics.AppendFormat(
            "Vehicle={0}|Seat={1}|Player={2}|Avatar={3}|AvatarLocal={4}|" +
            "AvatarScale={5}|Body={6}|Hip={7}|AnimatorEnabled={8}|" +
            "AnimatorCull={9}|ThirdPerson={10}|",
            _vehicle.transform.position,
            seat.transform.position,
            player.transform.position,
            avatar.transform.position,
            avatar.transform.localPosition,
            avatar.transform.lossyScale,
            avatar.BodyContainer.position,
            avatar.HipBone.position,
            avatar.Animation.animator.enabled,
            avatar.Animation.animator.cullingMode,
            player.ThirdPersonMeshesVisibleToLocalPlayer);
        var renderers = avatar.GetComponentsInChildren<SkinnedMeshRenderer>(true);
        for (var index = 0; index < renderers.Length; index++)
        {
            var renderer = renderers[index];
            var distanceToSeat = Vector3.Distance(renderer.bounds.center, seat.transform.position);
            diagnostics.AppendFormat(
                "R{0}[Name={1},Enabled={2},Active={3},Layer={4},ForceOff={5}," +
                "Center={6},Size={7},SeatDist={8:F2}]|",
                index,
                renderer.name,
                renderer.enabled,
                renderer.gameObject.activeInHierarchy,
                LayerMask.LayerToName(renderer.gameObject.layer),
                renderer.forceRenderingOff,
                renderer.bounds.center,
                renderer.bounds.size,
                distanceToSeat);
            if (renderer.enabled &&
                renderer.gameObject.activeInHierarchy &&
                renderer.gameObject.layer != invisibleLayer &&
                !renderer.forceRenderingOff &&
                renderer.bounds.size.sqrMagnitude > 0.001f)
            {
                visibleRenderers++;
                if (distanceToSeat < 3f)
                    nearbyRenderers++;
                if (string.Equals(renderer.name, "Body_LOD0", StringComparison.Ordinal) &&
                    distanceToSeat < 3f &&
                    renderer.bounds.size.magnitude < 5f &&
                    renderer.bounds.size.y > renderer.bounds.size.x)
                {
                    compactBodyRendererFound = true;
                }
            }
        }
        MelonLogger.Msg($"[S1ForkliftSmoke] AVATAR_STATE|{diagnostics}");

        if (visibleRenderers == 0)
            throw new InvalidOperationException("No active avatar mesh renderer is on a visible layer.");
        if (nearbyRenderers == 0)
        {
            throw new InvalidOperationException(
                $"Avatar renderers are enabled but none are near the driver seat; " +
                $"Avatar={avatar.transform.position}, Seat={seat.transform.position}.");
        }
        if (!compactBodyRendererFound)
        {
            throw new InvalidOperationException(
                "Body_LOD0 is not compact, upright, and centered near the forklift seat.");
        }

        var visualRoot = TransformUtility.FindDescendant(_vehicle.transform, Constants.VisualRootName);
        var roof = visualRoot == null
            ? null
            : TransformUtility.FindDescendant(visualRoot, "Roof");
        var roofRenderer = roof?.GetComponent<MeshRenderer>();
        var avatarTop = float.NegativeInfinity;
        for (var index = 0; index < renderers.Length; index++)
        {
            if (renderers[index].enabled && renderers[index].gameObject.activeInHierarchy)
                avatarTop = Mathf.Max(avatarTop, renderers[index].bounds.max.y);
        }
        MelonLogger.Msg(
            "[S1ForkliftSmoke] AVATAR_ROOF_STATE|" +
            $"AvatarTop={avatarTop:F3}|" +
            $"RoofBottom={(roofRenderer == null ? float.NaN : roofRenderer.bounds.min.y):F3}|" +
            $"Clearance={(roofRenderer == null ? float.NaN : roofRenderer.bounds.min.y - avatarTop):F3}");

        var steeringWheel = visualRoot == null
            ? null
            : TransformUtility.FindDescendant(visualRoot, "SteeringWheel");
        var steeringContainer = steeringWheel?.parent;
        var wheelLocalPosition = steeringWheel?.localPosition ?? Vector3.positiveInfinity;
        var wheelLocalRotation = steeringWheel?.localRotation ?? Quaternion.identity;
        wheelLocalRotation.ToAngleAxis(out var steeringAngle, out var steeringAxis);
        if (steeringAngle > 180f)
        {
            steeringAngle = 360f - steeringAngle;
            steeringAxis = -steeringAxis;
        }
        var axisAlignment = Mathf.Abs(Vector3.Dot(steeringAxis.normalized, Vector3.up));
        MelonLogger.Msg(
            "[S1ForkliftSmoke] STEERING_WHEEL_STATE|" +
            $"Parent={steeringContainer?.name ?? "<null>"}|" +
            $"HubLocalPosition={wheelLocalPosition}|" +
            $"SteeringAngle={steeringAngle:F2}|" +
            $"SteeringAxis={steeringAxis}|" +
            $"AxisAlignment={axisAlignment:F4}");
        if (!string.Equals(
                steeringContainer?.name,
                "SteeringWheelContainer",
                StringComparison.Ordinal) ||
            wheelLocalPosition.sqrMagnitude > 0.000025f ||
            steeringAngle < 5f ||
            axisAlignment < 0.999f)
        {
            throw new InvalidOperationException(
                "Steering wheel did not remain on its native hub and rotate around its local Y axle.");
        }

        var animation = avatar.Animation;
        if (animation?.animator == null ||
            !animation.animator.GetBool(Animator.StringToHash("Sitting")))
        {
            throw new InvalidOperationException("Avatar Sitting animator state is not active.");
        }

        var leftHand = animation.animator.GetBoneTransform(HumanBodyBones.LeftHand);
        var rightHand = animation.animator.GetBoneTransform(HumanBodyBones.RightHand);
        var leftTarget = TransformUtility.FindDescendant(_vehicle.transform, "S1Forklift_LeftHandTarget");
        var rightTarget = TransformUtility.FindDescendant(_vehicle.transform, "S1Forklift_RightHandTarget");
        if (!ForkliftDriverAvatarRuntime.TryGetAppliedHandDistances(
                player,
                out var leftDistance,
                out var rightDistance))
        {
            throw new InvalidOperationException("Steering arm pose did not report an applied result.");
        }
        var bodyIk = animation.IKController?.BodyIK;
        var gripSeparation = leftTarget == null || rightTarget == null
            ? 0f
            : Vector3.Distance(leftTarget.position, rightTarget.position);
        MelonLogger.Msg(
            "[S1ForkliftSmoke] AVATAR_IK_STATE|" +
            $"ControllerEnabled={animation.IKController?.enabled}|" +
            $"BodyIKPresent={bodyIk != null}|" +
            $"BodyIKEnabled={bodyIk?.enabled}|" +
            $"LeftHand={leftHand?.position.ToString() ?? "<null>"}|" +
            $"LeftTarget={leftTarget?.position.ToString() ?? "<null>"}|" +
            $"LeftDistance={leftDistance:F3}|" +
            $"RightHand={rightHand?.position.ToString() ?? "<null>"}|" +
            $"RightTarget={rightTarget?.position.ToString() ?? "<null>"}|" +
            $"RightDistance={rightDistance:F3}|" +
            $"GripSeparation={gripSeparation:F3}");
        CreateAvatarAuditCamera(_vehicle);
        ScreenCapture.CaptureScreenshot(Path.Combine(_outputDirectory, "forklift-driver-avatar.png"));
        if (leftDistance > 0.16f || rightDistance > 0.16f || gripSeparation < 0.24f)
        {
            throw new InvalidOperationException(
                $"Steering arm pose missed the wheel targets: left={leftDistance:F3}m, " +
                $"right={rightDistance:F3}m, separation={gripSeparation:F3}m.");
        }

        MelonLogger.Msg(
            $"[S1ForkliftSmoke] STATE|DriverAvatarVisible|Renderers={visibleRenderers}|" +
            $"Layer={LayerMask.LayerToName(avatar.gameObject.layer)}");
        Enter(Phase.CaptureDriverAvatar);
    }

    private void UpdateCaptureDriverAvatar()
    {
        if (PhaseElapsed < 1f || _vehicle == null)
            return;

        _vehicle.steerOverride = 0f;
        _vehicle.overrideControls = false;
        if (_avatarAuditCamera != null)
            UnityEngine.Object.Destroy(_avatarAuditCamera);
        Finish(
            true,
            $"Scene=Main|VehicleCode={_vehicle.VehicleCode}|DriverAvatar=Visible|" +
            "Sitting=Passed|SteeringIK=Passed|LiftPrompts=Passed");
    }

    private void CreateAvatarAuditCamera(S1LandVehicle vehicle)
    {
        var mainCamera = Camera.main
                         ?? throw new InvalidOperationException("Main camera is unavailable.");
        _avatarAuditCamera = new GameObject("S1Forklift_AvatarAuditCamera");
        var camera = _avatarAuditCamera.AddComponent<Camera>();
        camera.CopyFrom(mainCamera);
        camera.cullingMask = ~0;
        camera.depth = mainCamera.depth + 10f;
        var visualRoot = TransformUtility.FindDescendant(vehicle.transform, Constants.VisualRootName);
        var steeringWheel = visualRoot == null
            ? null
            : TransformUtility.FindDescendant(visualRoot, "SteeringWheel");
        var towardWheel = steeringWheel == null
            ? vehicle.transform.forward
            : steeringWheel.position - vehicle.Seats[0].transform.position;
        towardWheel.y = 0f;
        if (towardWheel.sqrMagnitude < 0.01f)
            towardWheel = Vector3.forward;
        towardWheel.Normalize();
        var side = Vector3.Cross(Vector3.up, towardWheel).normalized;
        camera.transform.position =
            vehicle.Seats[0].transform.position +
            side * 3f +
            Vector3.up * 0.55f;
        camera.fieldOfView = 50f;
        var target =
            vehicle.Seats[0].transform.position +
            towardWheel * 0.15f +
            Vector3.up * 0.15f;
        camera.transform.rotation =
            Quaternion.LookRotation((target - camera.transform.position).normalized, Vector3.up);
    }

    private void UpdateDrive()
    {
        if (_vehicle == null)
            throw new InvalidOperationException("Forklift was destroyed during drive.");
        if (PhaseElapsed < 2f)
            return;

        _vehicle.throttleOverride = 0f;
        _vehicle.steerOverride = 0f;
        _vehicle.overrideControls = false;
        _vehicle.IsOccupied = false;
        var distance = Vector3.Distance(_driveStart, _vehicle.transform.position);
        if (distance < 0.5f)
        {
            var grounded = 0;
            for (var index = 0; index < _vehicle.wheels.Count; index++)
            {
                if (_vehicle.wheels[index].IsWheelGrounded())
                    grounded++;
            }

            throw new InvalidOperationException(
                $"Forklift drive distance was only {distance:F3}m; " +
                $"Speed={_vehicle.Speed_Kmh:F3}; Grounded={grounded}/{_vehicle.wheels.Count}; " +
                $"Kinematic={_vehicle.Rb.isKinematic}; Simulated={_vehicle.IsPhysicallySimulated}; " +
                DescribeWheels(_vehicle));
        }

        _vehicle.Rb.velocity = Vector3.zero;
        _vehicle.Rb.angularVelocity = Vector3.zero;
        PreparePalletTest();
        FrameVehicle(_vehicle);
        ScreenCapture.CaptureScreenshot(Path.Combine(_outputDirectory, "forklift-driven.png"));
        MelonLogger.Msg($"[S1ForkliftSmoke] STATE|DrivePassed|Distance={distance:F3}");
        Enter(Phase.SettleCargo);
    }

    private void PreparePalletTest()
    {
        if (_vehicle == null)
            throw new InvalidOperationException("Forklift disappeared before the pallet test.");

        var docks = ForkliftSpawnPlacement.FindTeleportLocation("Docks");
        if (docks == null ||
            !ForkliftSpawnPlacement.TryFindClearPose(
                docks.position,
                docks.forward,
                8f,
                out var palletTestPosition,
                out var palletTestRotation,
                out var palletTestGroundDistance))
        {
            throw new InvalidOperationException("Could not find an open docks pose for the pallet test.");
        }
        _vehicle.transform.SetPositionAndRotation(palletTestPosition, palletTestRotation);
        Physics.SyncTransforms();
        MelonLogger.Msg(
            $"[S1ForkliftSmoke] STATE|RelocatedForPalletTest|" +
            $"Position={palletTestPosition}|ClearGroundDistance={palletTestGroundDistance:F2}");
        PrepareCargo();
    }

    private void PrepareCargo()
    {
        if (_vehicle == null)
            throw new InvalidOperationException("Forklift is unavailable.");

        var fork = ForkliftRuntime.GetFork(_vehicle)
                   ?? throw new InvalidOperationException("Fork carriage was not attached.");
        var prong = TransformUtility.FindDirectChild(fork, "Fork")
                    ?? throw new InvalidOperationException("Left fork prong is missing.");
        var otherProng = TransformUtility.FindDirectChild(fork, "Fork.001")
                         ?? throw new InvalidOperationException("Right fork prong is missing.");
        var prongTine = TransformUtility.FindDirectChild(prong, "r") ?? prong;
        var otherProngTine = TransformUtility.FindDirectChild(otherProng, "r") ?? otherProng;
        var prongCollider = prongTine.GetComponent<BoxCollider>()
                            ?? throw new InvalidOperationException("Left fork prong collider is missing.");
        var otherProngCollider = otherProngTine.GetComponent<BoxCollider>()
                                 ?? throw new InvalidOperationException("Right fork prong collider is missing.");
        if (!prongCollider.enabled ||
            prongCollider.isTrigger ||
            !otherProngCollider.enabled ||
            otherProngCollider.isTrigger)
        {
            throw new InvalidOperationException("Fork prong collider is not a solid enabled collider.");
        }

        var definition = S1Registry.GetItem(Constants.PalletItemId)
                         ?? throw new InvalidOperationException("Registered pallet definition disappeared.");
        var grid = FindPalletSpawnGrid(out var origin)
                   ?? throw new InvalidOperationException(
                       "No 4x4 native grid area was available for pallet creation.");
        var manager = S1BuildManager.Instance
                      ?? throw new InvalidOperationException("BuildManager is unavailable.");
        var created = manager.CreateGridItem(
            definition.GetDefaultInstance(),
            grid,
            new Vector2(origin.x, origin.y),
            0);
        _pallet = created as S1PlaceableStorageEntity
                  ?? throw new InvalidOperationException("BuildManager did not create a PlaceableStorageEntity.");
        _cargo = _pallet.gameObject;
        if (_pallet.FootprintX != Constants.PalletPlacementFootprintSize ||
            _pallet.FootprintY != Constants.PalletPlacementFootprintSize ||
            _pallet.CoordinateFootprintTilePairs.Count !=
                Constants.PalletPlacementFootprintSize *
                Constants.PalletPlacementFootprintSize)
        {
            throw new InvalidOperationException(
                $"Pallet placement footprint is {_pallet.FootprintX}x{_pallet.FootprintY} " +
                $"with {_pallet.CoordinateFootprintTilePairs.Count} tiles.");
        }
        MelonLogger.Msg(
            $"[S1ForkliftSmoke] STATE|PalletPlacementFootprint|" +
            $"Size={_pallet.FootprintX}x{_pallet.FootprintY}|" +
            $"Tiles={_pallet.CoordinateFootprintTilePairs.Count}");
        AssertPlacedPalletContact(_pallet, grid, origin);
        PalletPrefabFactory.RefreshPlacedTransitAccess(_pallet);
        if (!PalletPrefabFactory.TryVerifyPlacedTransitAccess(
                _pallet,
                out var transitAccessDetail))
        {
            throw new InvalidOperationException(
                $"Pallet transit access is not navigable: {transitAccessDetail}");
        }
        MelonLogger.Msg(
            $"[S1ForkliftSmoke] STATE|PalletTransitAccess|{transitAccessDetail}");
        if (!PalletPrefabFactory.TryVerifyOutlineRuntime(
                _pallet,
                out var outlineDetail))
        {
            throw new InvalidOperationException(
                $"Pallet transit outline failed: {outlineDetail}");
        }
        MelonLogger.Msg(
            $"[S1ForkliftSmoke] STATE|PalletTransitOutline|{outlineDetail}");

        var palletNetworkObject = _pallet.GetComponent<FishNetNetworkObject>();
        if (palletNetworkObject == null || !palletNetworkObject.IsSpawned)
            throw new InvalidOperationException("Pallet did not enter the FishNet spawned state.");
        if (_pallet.StorageEntity.ItemSlots.Count != Constants.PalletSlotCount)
        {
            throw new InvalidOperationException(
                $"Pallet initialized {_pallet.StorageEntity.ItemSlots.Count} storage slots " +
                $"instead of {Constants.PalletSlotCount}.");
        }
        var storageMenu = S1StorageMenu.Instance
                          ?? throw new InvalidOperationException("StorageMenu is unavailable.");
        if (!PalletStorageMenuRuntime.TryVerify(
                storageMenu,
                _pallet.StorageEntity,
                out var storageLayoutDetail))
        {
            throw new InvalidOperationException(
                $"Pallet storage layout failed: {storageLayoutDetail}");
        }
        MelonLogger.Msg(
            $"[S1ForkliftSmoke] STATE|PalletStorageLayout|{storageLayoutDetail}");

        var storedDefinition = S1Registry.GetItem("cash")
                               ?? throw new InvalidOperationException("Smoke storage item 'cash' is unavailable.");
        _palletStoredItemId = storedDefinition.ID;
        for (var slotIndex = 0; slotIndex < _pallet.StorageEntity.ItemSlots.Count; slotIndex++)
        {
            var storedItem = storedDefinition.GetDefaultInstance();
            var cash =
#if IL2CPP
                storedItem.TryCast<S1CashInstance>()
#else
                storedItem as S1CashInstance
#endif
                       ?? throw new InvalidOperationException(
                           "The registered 'cash' item did not create a CashInstance.");
            cash.SetBalance(CashPerPalletSlot, true);
            _pallet.StorageEntity.ItemSlots[slotIndex].SetStoredItem(storedItem);
        }
        MelonLogger.Msg(
            $"[S1ForkliftSmoke] STATE|CashPalletFilled|" +
            $"Slots={_pallet.StorageEntity.ItemSlots.Count}|" +
            $"BalancePerSlot={CashPerPalletSlot:F0}|" +
            $"Total={CashPerPalletSlot * _pallet.StorageEntity.ItemSlots.Count:F0}");
        var storageVisualizer = _pallet.GetComponent<S1StorageEntityVisualizer>()
                                ?? throw new InvalidOperationException(
                                    "The pallet storage visualizer is unavailable.");
        storageVisualizer.RefreshVisuals();

        // Reproduce a pallet that has already gone through a physical set-down.
        var droppedRigidbody = _pallet.GetComponent<Rigidbody>()
                               ?? _pallet.gameObject.AddComponent<Rigidbody>();
        droppedRigidbody.mass = 50f;
        droppedRigidbody.interpolation = RigidbodyInterpolation.Interpolate;
        droppedRigidbody.isKinematic = true;

        var interactables = _pallet.GetComponentsInChildren<S1InteractableObject>(true);
        var activeInteractableCount = 0;
        var activeStorageInteractableCount = 0;
        for (var index = 0; index < interactables.Length; index++)
        {
            if (interactables[index].enabled && interactables[index].gameObject.activeInHierarchy)
            {
                activeInteractableCount++;
                if (
#if IL2CPP
                    interactables[index].TryCast<S1StorageEntityInteractable>() != null
#else
                    interactables[index] is S1StorageEntityInteractable
#endif
                )
                    activeStorageInteractableCount++;
            }
        }
        if (activeInteractableCount != 1 || activeStorageInteractableCount != 1)
        {
            throw new InvalidOperationException(
                $"Pallet interaction sanitization failed: active={activeInteractableCount}, " +
                $"storage={activeStorageInteractableCount}.");
        }
        var storageInteractable =
            _pallet.GetComponentInChildren<S1StorageEntityInteractable>(true)
            ?? throw new InvalidOperationException(
                "The pallet storage interactable disappeared.");
        var interactionCollider = storageInteractable.GetComponent<BoxCollider>();
        if (interactionCollider == null ||
            !interactionCollider.enabled ||
            !interactionCollider.isTrigger ||
            interactionCollider.size.x < 1.49f ||
            interactionCollider.size.z < 1.49f)
        {
            throw new InvalidOperationException(
                "The pallet does not have full-mesh interaction coverage.");
        }
        MelonLogger.Msg(
            $"[S1ForkliftSmoke] STATE|PalletInteractionCoverage|" +
            $"Size={interactionCollider.size}|Range=" +
            $"{storageInteractable.MaxInteractionRange:F2}");

        if (!PalletCarryRuntime.TryPositionAndPickupForSmoke(_vehicle, _pallet, out var pickupDetail))
            throw new InvalidOperationException($"Pallet pickup failed: {pickupDetail}");
        MelonLogger.Msg($"[S1ForkliftSmoke] STATE|PalletPickedUp|{pickupDetail}");
        if (droppedRigidbody != null &&
            droppedRigidbody.interpolation != RigidbodyInterpolation.None)
        {
            throw new InvalidOperationException(
                "A re-picked pallet retained Rigidbody interpolation while carried.");
        }
        AssertPalletSupportedByForks("Pickup");
        AssertPalletCollisionIsolation();

        _forkStartHeight = fork.localPosition.y;
        _cargoStartHeight = _cargo.transform.position.y;

        storageMenu.Open(_pallet.StorageEntity);
        _palletStorageMenuOpen = true;
        ScreenCapture.CaptureScreenshot(
            Path.Combine(_outputDirectory, "pallet-storage-menu.png"));
    }

    private static S1Grid? FindPalletSpawnGrid(out S1Coordinate origin)
    {
        origin = new S1Coordinate();
        var grids = Resources.FindObjectsOfTypeAll<S1Grid>();
        for (var gridIndex = 0; gridIndex < grids.Length; gridIndex++)
        {
            var grid = grids[gridIndex];
            if (grid == null || !grid.gameObject.scene.IsValid())
                continue;

            for (var pairIndex = 0; pairIndex < grid.CoordinateTilePairs.Count; pairIndex++)
            {
                var candidate = grid.CoordinateTilePairs[pairIndex].coord;
                var hasFullFootprint = true;
                for (var x = 0;
                     x < Constants.PalletPlacementFootprintSize && hasFullFootprint;
                     x++)
                {
                    for (var y = 0;
                         y < Constants.PalletPlacementFootprintSize;
                         y++)
                    {
                        if (grid.GetTile(
                                new S1Coordinate(
                                    candidate.x + x,
                                    candidate.y + y)) != null)
                        {
                            continue;
                        }

                        hasFullFootprint = false;
                        break;
                    }
                }
                if (!hasFullFootprint)
                    continue;

                origin = new S1Coordinate(candidate.x, candidate.y);
                return grid;
            }
        }

        return null;
    }

    private static void AssertPlacedPalletContact(
        S1PlaceableStorageEntity pallet,
        S1Grid grid,
        S1Coordinate origin)
    {
        var visual = TransformUtility.FindDirectChild(pallet.transform, "PalletVisual")
                     ?? throw new InvalidOperationException("Placed pallet visual is missing.");
        var renderers = visual.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0)
            throw new InvalidOperationException("Placed pallet visual has no renderers.");

        var bounds = renderers[0].bounds;
        for (var index = 1; index < renderers.Length; index++)
            bounds.Encapsulate(renderers[index].bounds);
        var tile = grid.GetTile(new S1Coordinate(origin.x, origin.y))
                   ?? throw new InvalidOperationException("Placed pallet origin tile disappeared.");
        var gap = bounds.min.y - tile.transform.position.y;
        MelonLogger.Msg(
            $"[S1ForkliftSmoke] STATE|PalletPlacedContact|" +
            $"VisualBottom={bounds.min.y:F3}|TileSurface={tile.transform.position.y:F3}|Gap={gap:F3}");
        if (gap > 0.01f)
            throw new InvalidOperationException($"Placed pallet floats {gap:F3}m above its tile.");
        if (gap < -0.02f)
            throw new InvalidOperationException($"Placed pallet sinks {-gap:F3}m into its tile.");
    }

    private void AssertPalletCollisionIsolation()
    {
        if (_vehicle == null || _pallet == null)
            throw new InvalidOperationException("Forklift or pallet is unavailable.");
        if (!PalletCarryRuntime.TryVerifyCollisionIsolationForSmoke(
                _vehicle,
                _pallet,
                out var detail))
        {
            throw new InvalidOperationException(
                $"Pallet collision isolation failed: {detail}");
        }
        MelonLogger.Msg($"[S1ForkliftSmoke] STATE|PalletCollisionIsolation|{detail}");
    }

    private void UpdateSettleCargo()
    {
        if (PhaseElapsed < 1f)
            return;
        if (_palletStorageMenuOpen)
        {
            S1StorageMenu.Instance?.Close();
            _palletStorageMenuOpen = false;
            return;
        }
        if (_vehicle != null && _pallet != null)
        {
            AssertStoredItemSurfaceAlignment();
            AssertPalletContentsAndInteraction();
            FramePallet(_vehicle, _pallet);
            ScreenCapture.CaptureScreenshot(
                Path.Combine(_outputDirectory, "forklift-cash-pallet-picked-up.png"));
        }
        Enter(Phase.RaiseCargo);
    }

    private void AssertStoredItemSurfaceAlignment()
    {
        if (_pallet == null)
            throw new InvalidOperationException("The pallet is unavailable.");

        var palletVisual =
            TransformUtility.FindDirectChild(_pallet.transform, "PalletVisual")
            ?? throw new InvalidOperationException("The pallet visual is unavailable.");
        var visualizer = _pallet.GetComponent<S1StorageEntityVisualizer>()
                         ?? throw new InvalidOperationException(
                             "The pallet storage visualizer is unavailable.");
        if (visualizer.ItemContainer == null)
            throw new InvalidOperationException(
                "The pallet stored-item container is unavailable.");

        if (!TryGetRendererBounds(palletVisual, out var palletBounds) ||
            !TryGetRendererBounds(visualizer.ItemContainer, out var itemBounds))
        {
            throw new InvalidOperationException(
                "Pallet or stored-item renderer bounds are unavailable.");
        }

        var gap = itemBounds.min.y - palletBounds.max.y;
        MelonLogger.Msg(
            $"[S1ForkliftSmoke] STATE|StoredItemSurface|" +
            $"Gap={gap:F4}|PalletTop={palletBounds.max.y:F4}|" +
            $"ItemBottom={itemBounds.min.y:F4}");
        if (gap < -0.005f || gap > 0.008f)
        {
            throw new InvalidOperationException(
                $"Stored items are not seated on the pallet surface: gap={gap:F4}m.");
        }
    }

    private static bool TryGetRendererBounds(Transform root, out Bounds bounds)
    {
        var renderers = root.GetComponentsInChildren<Renderer>(true);
        var found = false;
        bounds = default;
        for (var index = 0; index < renderers.Length; index++)
        {
            var renderer = renderers[index];
            if (!renderer.enabled || !renderer.gameObject.activeInHierarchy)
                continue;
            if (!found)
            {
                bounds = renderer.bounds;
                found = true;
            }
            else
            {
                bounds.Encapsulate(renderer.bounds);
            }
        }
        return found;
    }

    private void UpdateRaiseCargo()
    {
        if (_vehicle == null || _cargo == null)
            throw new InvalidOperationException("Forklift or cargo disappeared during lift.");

        if (!ForkliftRuntime.TryMoveForSmoke(
                _vehicle,
                1f,
                Time.deltaTime,
                out var forkHeight))
        {
            throw new InvalidOperationException("Lift runtime rejected the forklift.");
        }
        PalletCarryRuntime.SynchronizeForSmoke(_vehicle);

        if (PhaseElapsed < 1.2f)
            return;

        var forkDelta = forkHeight - _forkStartHeight;
        var cargoDelta = _cargo.transform.position.y - _cargoStartHeight;
        if (forkDelta < 0.5f)
            throw new InvalidOperationException($"Fork raised only {forkDelta:F3}m.");
        if (cargoDelta < 0.25f)
            throw new InvalidOperationException($"Cargo rose only {cargoDelta:F3}m.");
        AssertPalletContentsAndInteraction();
        AssertPalletSupportedByForks("Raised");
        if (PalletCarryRuntime.TryReleaseForSmoke(
                _vehicle,
                out var prematureReleaseDetail))
        {
            throw new InvalidOperationException(
                "The pallet released above the configured lower-lift threshold.");
        }
        MelonLogger.Msg(
            $"[S1ForkliftSmoke] STATE|PalletReleaseGuard|" +
            $"{prematureReleaseDetail}");

        CreatePalletAuditCamera(_vehicle, _pallet);
        ScreenCapture.CaptureScreenshot(
            Path.Combine(_outputDirectory, "forklift-raised-pallet-alignment.png"));
        MelonLogger.Msg(
            $"[S1ForkliftSmoke] STATE|PalletLiftPassed|ForkDelta={forkDelta:F3}|PalletDelta={cargoDelta:F3}");
        Enter(Phase.CaptureRaised);
    }

    private void AssertPalletSupportedByForks(string phase)
    {
        if (_vehicle == null || _pallet == null)
            throw new InvalidOperationException("Forklift or pallet is unavailable.");
        if (!PalletCarryRuntime.TryGetSupportContactForSmoke(
                _vehicle,
                _pallet,
                out var deckGap,
                out var visualBottomOffset,
                out var detail))
        {
            throw new InvalidOperationException(
                $"Could not measure pallet-to-fork support gap during {phase}: {detail}");
        }

        MelonLogger.Msg(
            $"[S1ForkliftSmoke] STATE|PalletForkContact|Phase={phase}|{detail}");
        if (deckGap > 0.01f)
            throw new InvalidOperationException(
                $"Pallet deck is floating {deckGap:F3}m above the fork prongs during {phase}.");
        if (deckGap < -0.01f)
            throw new InvalidOperationException(
                $"Fork prongs penetrate the pallet deck by {-deckGap:F3}m during {phase}.");
        if (visualBottomOffset > -0.08f)
            throw new InvalidOperationException(
                $"Fork prongs enter the pallet only {-visualBottomOffset:F3}m during {phase}; " +
                "they should sit inside the pallet opening.");
    }

    private void UpdateCaptureRaised()
    {
        if (PhaseElapsed < 1f)
            return;
        if (_palletAuditCamera != null)
        {
            UnityEngine.Object.Destroy(_palletAuditCamera);
            _palletAuditCamera = null;
        }
        Enter(Phase.LowerFork);
    }

    private void CreatePalletAuditCamera(
        S1LandVehicle vehicle,
        S1PlaceableStorageEntity? pallet)
    {
        if (pallet == null)
            throw new InvalidOperationException("Pallet disappeared before alignment capture.");
        if (_palletAuditCamera != null)
            UnityEngine.Object.Destroy(_palletAuditCamera);

        var mainCamera = Camera.main
                         ?? throw new InvalidOperationException("Main camera is unavailable.");
        _palletAuditCamera = new GameObject("S1Forklift_PalletAuditCamera");
        var camera = _palletAuditCamera.AddComponent<Camera>();
        camera.CopyFrom(mainCamera);
        camera.cullingMask = ~0;
        camera.depth = mainCamera.depth + 10f;
        camera.fieldOfView = 34f;

        var fork = ForkliftRuntime.GetFork(vehicle)
                   ?? throw new InvalidOperationException("Fork disappeared before alignment capture.");
        var leftProng = TransformUtility.FindDirectChild(fork, "Fork")
                        ?? throw new InvalidOperationException("Left fork prong is unavailable.");
        var target = Vector3.Lerp(leftProng.position, pallet.transform.position, 0.45f);
        camera.transform.position =
            target +
            vehicle.transform.forward * 2.8f +
            vehicle.transform.right * 2.1f +
            vehicle.transform.up * 0.18f;
        camera.transform.rotation =
            Quaternion.LookRotation((target - camera.transform.position).normalized, vehicle.transform.up);
    }

    private void UpdateLowerFork()
    {
        if (_vehicle == null)
            throw new InvalidOperationException("Forklift disappeared while lowering.");

        if (!ForkliftRuntime.TryMoveForSmoke(
                _vehicle,
                -1f,
                Time.deltaTime,
                out var forkHeight))
        {
            throw new InvalidOperationException("Lift runtime rejected the forklift while lowering.");
        }

        if (forkHeight > _forkStartHeight + 0.05f)
            return;

        if (!PalletCarryRuntime.TryReleaseForSmoke(_vehicle, out var releaseDetail))
            throw new InvalidOperationException($"Pallet set-down failed: {releaseDetail}");
        MelonLogger.Msg($"[S1ForkliftSmoke] STATE|PalletReleased|{releaseDetail}");
        Enter(Phase.SettlePallet);
    }

    private void UpdateSettlePallet()
    {
        if (_pallet == null)
            throw new InvalidOperationException("Pallet disappeared while settling.");
        if (!PalletCarryRuntime.IsSettledForSmoke(_pallet))
        {
            if (PhaseWallElapsed > 5f)
            {
                var rigidbody = _pallet.GetComponent<Rigidbody>();
                throw new InvalidOperationException(
                    $"Pallet did not settle within 5 seconds; " +
                    $"Position={_pallet.transform.position}|Velocity={rigidbody?.velocity}.");
            }
            return;
        }

        AssertPalletContentsAndInteraction();
        AssertPalletGroundContact(_pallet);
        if (_vehicle != null)
            FramePallet(_vehicle, _pallet);
        ScreenCapture.CaptureScreenshot(
            Path.Combine(_outputDirectory, "forklift-cash-pallet-set-down.png"));
        MelonLogger.Msg(
            $"[S1ForkliftSmoke] STATE|PalletSetDown|Position={_pallet.transform.position}");

        if (_vehicle == null)
            throw new InvalidOperationException("Forklift disappeared before the pallet repick test.");
        if (!PalletCarryRuntime.TryPickupSettledForSmoke(
                _vehicle,
                _pallet,
                out var repickDetail))
        {
            throw new InvalidOperationException(
                $"Settled pallet repick failed: {repickDetail}");
        }
        MelonLogger.Msg(
            $"[S1ForkliftSmoke] STATE|PalletRepicked|{repickDetail}");
        AssertPalletSupportedByForks("Repick");
        AssertPalletCollisionIsolation();
        AssertPalletContentsAndInteraction();

        if (_palletAlignmentOnly)
        {
            Finish(
                true,
                "Scene=Main|VehicleCode=forklift|PalletAlignment=Passed|" +
                "PalletRepick=Passed|StorageLayout=Passed");
            return;
        }
        Enter(Phase.RelocateVisualAudit);
    }

    private void AssertPalletContentsAndInteraction()
    {
        if (_pallet == null)
            throw new InvalidOperationException("Pallet disappeared during the carry test.");
        if (_pallet.StorageEntity.ItemSlots.Count != Constants.PalletSlotCount)
            throw new InvalidOperationException("Pallet storage slot count changed during transport.");

        for (var slotIndex = 0; slotIndex < _pallet.StorageEntity.ItemSlots.Count; slotIndex++)
        {
            var stored = _pallet.StorageEntity.ItemSlots[slotIndex].ItemInstance;
            if (stored == null ||
                !string.Equals(stored.ID, _palletStoredItemId, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    $"Pallet contents changed during transport at slot {slotIndex}.");
            }
            var cash =
#if IL2CPP
                stored.TryCast<S1CashInstance>();
#else
                stored as S1CashInstance;
#endif
            if (cash == null || Math.Abs(cash.Balance - CashPerPalletSlot) > 0.01f)
            {
                throw new InvalidOperationException(
                    $"Pallet cash balance changed during transport at slot {slotIndex}.");
            }
        }

        var interactables = _pallet.GetComponentsInChildren<S1InteractableObject>(true);
        for (var index = 0; index < interactables.Length; index++)
        {
            if (interactables[index].enabled && interactables[index].gameObject.activeInHierarchy)
                return;
        }

        throw new InvalidOperationException("Pallet storage interaction became unavailable during transport.");
    }

    private static void AssertPalletGroundContact(S1PlaceableStorageEntity pallet)
    {
        var visual = TransformUtility.FindDirectChild(pallet.transform, "PalletVisual")
                     ?? throw new InvalidOperationException("Pallet visual is missing.");
        var renderers = visual.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0)
            throw new InvalidOperationException("Pallet visual has no renderers.");

        var visualBounds = renderers[0].bounds;
        for (var index = 1; index < renderers.Length; index++)
            visualBounds.Encapsulate(renderers[index].bounds);

        var origin = visualBounds.center + Vector3.up * 0.1f;
        var hits = Physics.RaycastAll(
            origin,
            Vector3.down,
            visualBounds.extents.y + 0.65f,
            ~0,
            QueryTriggerInteraction.Ignore);
        var groundY = float.NegativeInfinity;
        for (var index = 0; index < hits.Length; index++)
        {
            var hit = hits[index];
            if (hit.collider == null ||
                hit.collider.transform == pallet.transform ||
                hit.collider.transform.IsChildOf(pallet.transform) ||
                hit.normal.y < 0.75f)
            {
                continue;
            }
            groundY = Mathf.Max(groundY, hit.point.y);
        }
        if (float.IsNegativeInfinity(groundY))
            throw new InvalidOperationException("No ground was found beneath the settled pallet visual.");

        var gap = visualBounds.min.y - groundY;
        MelonLogger.Msg(
            $"[S1ForkliftSmoke] STATE|PalletGroundContact|" +
            $"VisualBottom={visualBounds.min.y:F3}|Ground={groundY:F3}|Gap={gap:F3}");
        if (gap > 0.01f)
            throw new InvalidOperationException($"Settled pallet is floating {gap:F3}m above ground.");
        if (gap < -0.05f)
            throw new InvalidOperationException($"Settled pallet penetrates ground by {-gap:F3}m.");
    }

    private void UpdateRelocateVisualAudit()
    {
        if (_vehicle == null)
            throw new InvalidOperationException("Forklift disappeared before the visual audit.");

        var docks = ForkliftSpawnPlacement.FindTeleportLocation("Docks");
        if (docks == null ||
            !ForkliftSpawnPlacement.TryFindClearPose(
                docks.position,
                docks.forward,
                8f,
                out var position,
                out var rotation,
                out _))
        {
            return;
        }

        _vehicle.Rb.velocity = Vector3.zero;
        _vehicle.Rb.angularVelocity = Vector3.zero;
        _vehicle.transform.SetPositionAndRotation(position, rotation);
        _vehicle.Rb.position = position;
        _vehicle.Rb.rotation = rotation;
        Physics.SyncTransforms();
        AssertVisibleWheelRenderers(_vehicle);
        FrameVehicle(_vehicle, 5f, -4f);
        MelonLogger.Msg($"[S1ForkliftSmoke] STATE|RelocatedForVisualAudit|Position={position}");
        Enter(Phase.CaptureFrontLeft);
    }

    private void UpdateCaptureFrontLeft()
    {
        if (PhaseElapsed < 1f || _vehicle == null)
            return;

        AssertVisibleWheelRenderers(_vehicle);
        FrameVehicle(_vehicle, 5f, -4f);
        ScreenCapture.CaptureScreenshot(Path.Combine(_outputDirectory, "forklift-front-left.png"));
        Enter(Phase.CaptureRearRight);
    }

    private void UpdateCaptureRearRight()
    {
        if (PhaseElapsed < 1f || _vehicle == null)
            return;

        AssertVisibleWheelRenderers(_vehicle);
        FrameVehicle(_vehicle, -5f, 4f);
        ScreenCapture.CaptureScreenshot(Path.Combine(_outputDirectory, "forklift-rear-right.png"));
        Enter(Phase.CaptureSide);
    }

    private void UpdateCaptureSide()
    {
        if (PhaseElapsed < 1f || _vehicle == null)
            return;

        AssertVisibleWheelRenderers(_vehicle);
        FrameVehicle(_vehicle, 0f, -6f);
        ScreenCapture.CaptureScreenshot(Path.Combine(_outputDirectory, "forklift-side.png"));
        Finish(
            true,
            $"Scene=Main|VehicleCode={_vehicle.VehicleCode}|Drive=Passed|Lift=Passed|" +
            $"VisualAudit=Passed|ForkReturned={ForkliftRuntime.GetFork(_vehicle)?.localPosition.y:F3}");
    }

    private float PhaseElapsed => Time.unscaledTime - _phaseStarted;
    private float PhaseWallElapsed =>
        (float)(DateTime.UtcNow - _phaseStartedUtc).TotalSeconds;

    private static void AssertDrivetrainConfiguration(
        S1LandVehicle vehicle,
        S1VehicleManager manager)
    {
        if (!Mathf.Approximately(vehicle.TopSpeed, Constants.ForkliftTopSpeed))
        {
            throw new InvalidOperationException(
                $"Forklift top speed changed to {vehicle.TopSpeed:F3} km/h.");
        }
        var brakeForceMultiplier = LandVehicleAccess.GetBrakeForceMultiplier(vehicle);
        if (!Mathf.Approximately(
                brakeForceMultiplier,
                Constants.ForkliftBrakeForceMultiplier))
        {
            throw new InvalidOperationException(
                $"Forklift brake force multiplier changed to {brakeForceMultiplier:F3}.");
        }

        var baseVehicle = manager.GetVehiclePrefab(Constants.BaseVehicleCode)
                          ?? throw new InvalidOperationException("Base vehicle disappeared during drivetrain audit.");
        var baseCurve = LandVehicleAccess.GetMotorTorqueCurve(baseVehicle);
        var forkliftCurve = LandVehicleAccess.GetMotorTorqueCurve(vehicle);
        if (baseCurve == null || forkliftCurve == null)
            throw new InvalidOperationException("Motor torque curve is unavailable during drivetrain audit.");
        if (ReferenceEquals(baseCurve, forkliftCurve))
            throw new InvalidOperationException("Forklift torque scaling mutated the shared base-vehicle curve.");

        AssertMotorCurveTorqueAtSpeed(forkliftCurve, 0f);
        AssertMotorCurveTorqueAtSpeed(forkliftCurve, 10f);
        AssertMotorCurveTorqueAtSpeed(forkliftCurve, 15f);
        AssertMotorCurveTorqueAtSpeed(forkliftCurve, 17.25f);
        AssertMotorCurveTorqueAtSpeed(forkliftCurve, Constants.ForkliftTopSpeed);
        var expectedCenterOfMass = new Vector3(
            0f,
            Constants.ForkliftCenterOfMassHeightMeters,
            Constants.ForkliftCenterOfMassLongitudinalMeters);
        if (!Mathf.Approximately(vehicle.Rb.mass, Constants.ForkliftMassKg) ||
            Vector3.Distance(vehicle.Rb.centerOfMass, expectedCenterOfMass) > 0.01f ||
            !Mathf.Approximately(vehicle.Rb.drag, Constants.ForkliftLinearDrag))
        {
            throw new InvalidOperationException(
                $"Forklift body physics changed to mass={vehicle.Rb.mass:F1}, " +
                $"drag={vehicle.Rb.drag:F3}.");
        }

        MelonLogger.Msg(
            $"[S1ForkliftSmoke] STATE|DrivetrainConfigured|" +
            $"TopSpeed={vehicle.TopSpeed:F1}|" +
            $"BrakeForceMultiplier={brakeForceMultiplier:F2}|" +
            $"DrawbarPull={Constants.ForkliftMaximumDrawbarPullNewtons:F0}N|" +
            $"DrivePower={Constants.ForkliftMaximumDrivePowerWatts:F0}W|" +
            $"TorqueKeys={forkliftCurve.keys.Length}");
        MelonLogger.Msg(
            $"[S1ForkliftSmoke] PHYSICS|Mass={vehicle.Rb.mass:F1}|" +
            $"CenterOfMass={vehicle.Rb.centerOfMass}|Drag={vehicle.Rb.drag:F3}|" +
            $"AngularDrag={vehicle.Rb.angularDrag:F3}|" +
            $"DiffGearing={LandVehicleAccess.GetDifferentialGearing(vehicle):F3}|" +
            $"HandBrakeForce={LandVehicleAccess.GetHandBrakeForce(vehicle):F3}|" +
            $"BrakeCurve={DescribeCurve(LandVehicleAccess.GetBrakeForceCurve(vehicle))}|" +
            $"MotorCurve={DescribeCurve(forkliftCurve)}");
        for (var index = 0; index < vehicle.wheels.Count; index++)
        {
            var collider = vehicle.wheels[index].wheelCollider;
            var spring = collider.suspensionSpring;
            var forward = collider.forwardFriction;
            var sideways = collider.sidewaysFriction;
            MelonLogger.Msg(
                $"[S1ForkliftSmoke] PHYSICS_WHEEL|Index={index}|Name={collider.name}|" +
                $"LocalPosition={vehicle.transform.InverseTransformPoint(collider.transform.position)}|" +
                $"Radius={collider.radius:F4}|Mass={collider.mass:F2}|" +
                $"Suspension={collider.suspensionDistance:F4}|" +
                $"Spring={spring.spring:F1}|Damper={spring.damper:F1}|" +
                $"Target={spring.targetPosition:F3}|" +
                $"Forward={forward.extremumSlip:F3},{forward.extremumValue:F3}," +
                $"{forward.asymptoteSlip:F3},{forward.asymptoteValue:F3},{forward.stiffness:F3}|" +
                $"Sideways={sideways.extremumSlip:F3},{sideways.extremumValue:F3}," +
                $"{sideways.asymptoteSlip:F3},{sideways.asymptoteValue:F3},{sideways.stiffness:F3}");
        }
    }

    private static string DescribeCurve(AnimationCurve curve)
    {
        var keys = curve.keys;
        var parts = new string[keys.Length];
        for (var index = 0; index < keys.Length; index++)
            parts[index] = $"{keys[index].time:F2}:{keys[index].value:F2}";
        return string.Join(",", parts);
    }

    private static void AssertMotorCurveTorqueAtSpeed(
        AnimationCurve forkliftCurve,
        float speedKmh)
    {
        var expected = DrivetrainMath.CalculateMotorCurveTorque(
            speedKmh,
            Constants.ForkliftMaximumDrawbarPullNewtons,
            Constants.ForkliftMaximumDrivePowerWatts,
            Constants.ForkliftDriveWheelRadiusMeters,
            Constants.ForkliftDifferentialGearing,
            Constants.ForkliftGovernorStartSpeedKmh,
            Constants.ForkliftTopSpeed);
        var actual = forkliftCurve.Evaluate(speedKmh);
        var tolerance = Mathf.Max(0.1f, Mathf.Abs(expected) * 0.02f);
        if (Mathf.Abs(expected - actual) <= tolerance)
            return;

        throw new InvalidOperationException(
            $"Forklift torque at {speedKmh:F1} km/h is {actual:F3}; " +
            $"expected {expected:F3} from the drawbar/power model.");
    }

    private static void AssertSingleForkliftVisual(
        S1LandVehicle vehicle,
        S1VehicleManager manager)
    {
        var visualCount = 0;
        var descendants = vehicle.GetComponentsInChildren<Transform>(true);
        for (var index = 0; index < descendants.Length; index++)
        {
            if (string.Equals(descendants[index].name, Constants.VisualRootName, StringComparison.Ordinal))
                visualCount++;
        }

        var vehicleCount = 0;
        for (var index = 0; index < manager.AllVehicles.Count; index++)
        {
            var candidate = manager.AllVehicles[index];
            if (candidate != null &&
                string.Equals(candidate.VehicleCode, Constants.VehicleCode, StringComparison.OrdinalIgnoreCase) &&
                Vector3.Distance(candidate.transform.position, vehicle.transform.position) < 1f)
            {
                vehicleCount++;
            }
        }

        if (visualCount != 1 || vehicleCount != 1)
        {
            throw new InvalidOperationException(
                $"Expected one forklift visual and one spawned vehicle, found Visuals={visualCount}, Vehicles={vehicleCount}.");
        }
    }

    private static void AssertVisibleWheelRenderers(S1LandVehicle vehicle)
    {
        var visualRoot = TransformUtility.FindDescendant(vehicle.transform, Constants.VisualRootName)
                         ?? throw new InvalidOperationException("Forklift visual root is missing.");
        var wheelNames = new[] { "Wheel_FL", "Wheel_FR", "Wheel_RL", "Wheel_RR" };
        for (var wheelIndex = 0; wheelIndex < wheelNames.Length; wheelIndex++)
        {
            var wheel = TransformUtility.FindDirectChild(visualRoot, wheelNames[wheelIndex])
                        ?? throw new InvalidOperationException(
                            $"Visual wheel '{wheelNames[wheelIndex]}' is missing.");
            var renderers = wheel.GetComponentsInChildren<Renderer>(true);
            var visibleRendererFound = false;
            for (var rendererIndex = 0; rendererIndex < renderers.Length; rendererIndex++)
            {
                var renderer = renderers[rendererIndex];
                if (renderer.enabled &&
                    renderer.gameObject.activeInHierarchy &&
                    renderer.bounds.size.sqrMagnitude > 0.001f)
                {
                    visibleRendererFound = true;
                    break;
                }
            }

            if (!visibleRendererFound)
            {
                throw new InvalidOperationException(
                    $"Visual wheel '{wheelNames[wheelIndex]}' has no enabled renderer with non-zero bounds.");
            }
        }
    }

    private static void AssertVehicleDetectionCollider(S1LandVehicle vehicle)
    {
        var boundingBox = vehicle.boundingBox;
        if (boundingBox == null || !boundingBox.enabled || !boundingBox.isTrigger)
        {
            throw new InvalidOperationException(
                "Forklift vehicle detection requires an enabled trigger bounding box.");
        }

        MelonLogger.Msg(
            "[S1ForkliftSmoke] STATE|VehicleDetectionColliderReady|" +
            $"Name={boundingBox.name}|Layer={boundingBox.gameObject.layer}|" +
            $"Size={boundingBox.size}");
    }

    private S1VehicleDetector CreateVehicleDetectorProbe(S1LandVehicle vehicle)
    {
        var detectorObject = new GameObject("S1Forklift_VehicleDetectorProbe");
        detectorObject.transform.position = vehicle.boundingBox.bounds.center;
        var detectorCollider = detectorObject.AddComponent<BoxCollider>();
        detectorCollider.isTrigger = true;
        detectorCollider.size = vehicle.boundingBox.bounds.size + Vector3.one;
        var detector = detectorObject.AddComponent<S1VehicleDetector>();
        Physics.SyncTransforms();
        return detector;
    }

    private void AssertVehicleDetectorRecognition(S1LandVehicle vehicle)
    {
        var detector = _vehicleDetectorProbe
                       ?? throw new InvalidOperationException(
                           "Vehicle detector probe disappeared before verification.");
        var detected = false;
        for (var index = 0; index < detector.vehicles.Count; index++)
        {
            if (detector.vehicles[index] == vehicle)
            {
                detected = true;
                break;
            }
        }

        var wasOccupied = vehicle.IsOccupied;
        vehicle.IsOccupied = true;
        var gateEligible = detector.AreAnyVehiclesOccupied();
        vehicle.IsOccupied = wasOccupied;
        var shopLoadingEligible =
            detector.closestVehicle == vehicle &&
            vehicle.IsPlayerOwned &&
            vehicle.Storage != null;

        UnityEngine.Object.Destroy(detector.gameObject);
        _vehicleDetectorProbe = null;
        if (!detected || !gateEligible || !shopLoadingEligible)
        {
            throw new InvalidOperationException(
                "The forklift did not satisfy the native vehicle detector consumer contract: " +
                $"Detected={detected}, GateEligible={gateEligible}, " +
                $"ShopLoadingEligible={shopLoadingEligible}.");
        }

        MelonLogger.Msg(
            "[S1ForkliftSmoke] STATE|VehicleDetectorRecognizedForklift|" +
            $"VehicleCode={vehicle.VehicleCode}|GateEligible={gateEligible}|" +
            $"ShopLoadingEligible={shopLoadingEligible}");
    }

    private static void FrameVehicle(
        S1LandVehicle vehicle,
        float forwardOffset = -5f,
        float rightOffset = 4f)
    {
        if (!S1DevUtilities.PlayerSingleton<S1PlayerMovement>.InstanceExists)
            return;

        var target = vehicle.transform.position + vehicle.transform.up * 1.1f;
        var viewPosition =
            vehicle.transform.position +
            vehicle.transform.forward * forwardOffset +
            vehicle.transform.right * rightOffset +
            vehicle.transform.up * 1.2f;
        var movement = S1DevUtilities.PlayerSingleton<S1PlayerMovement>.Instance;
        movement.Teleport(viewPosition, false);
        movement.SetPlayerRotation(
            Quaternion.LookRotation((target - viewPosition).normalized, Vector3.up));
    }

    private static void FramePallet(
        S1LandVehicle vehicle,
        S1PlaceableStorageEntity? pallet)
    {
        if (pallet == null || !S1DevUtilities.PlayerSingleton<S1PlayerMovement>.InstanceExists)
            return;

        var target = pallet.transform.position + Vector3.up * 0.25f;
        var viewPosition =
            target +
            vehicle.transform.forward * 2.5f +
            vehicle.transform.right * 3f +
            Vector3.up * 1.4f;
        var movement = S1DevUtilities.PlayerSingleton<S1PlayerMovement>.Instance;
        movement.Teleport(viewPosition, false);
        movement.SetPlayerRotation(
            Quaternion.LookRotation((target - viewPosition).normalized, Vector3.up));
    }

    private static string DescribeWheels(S1LandVehicle vehicle)
    {
        var description = new StringBuilder("Wheels=");
        for (var index = 0; index < vehicle.wheels.Count; index++)
        {
            if (index > 0)
                description.Append(';');

            var wheel = vehicle.wheels[index];
            var collider = wheel.wheelCollider;
            var axisAlignment = Vector3.Dot(collider.transform.up, vehicle.transform.up);
            var rayDistance = -1f;
            var rayOrigin = collider.transform.position + vehicle.transform.up * 0.05f;
            if (Physics.Raycast(
                    rayOrigin,
                    -vehicle.transform.up,
                    out var hit,
                    2f,
                    Physics.DefaultRaycastLayers,
                    QueryTriggerInteraction.Ignore))
            {
                rayDistance = hit.distance;
            }

            description.AppendFormat(
                "{0}[Y={1:F3},R={2:F3},S={3:F3},Axis={4:F2},Ray={5:F3}]",
                wheel.name,
                collider.transform.position.y,
                collider.radius,
                collider.suspensionDistance,
                axisAlignment,
                rayDistance);
        }

        return description.ToString();
    }

    private void Enter(Phase phase)
    {
        _phase = phase;
        _phaseStarted = Time.unscaledTime;
        _phaseStartedUtc = DateTime.UtcNow;
        MelonLogger.Msg($"[S1ForkliftSmoke] STATE|Phase={phase}");
    }

    private void Finish(bool passed, string details)
    {
        if (_finished)
            return;
        _finished = true;
        if (_avatarAuditCamera != null)
            UnityEngine.Object.Destroy(_avatarAuditCamera);
        if (_palletAuditCamera != null)
            UnityEngine.Object.Destroy(_palletAuditCamera);
        if (_vehicleDetectorProbe != null)
            UnityEngine.Object.Destroy(_vehicleDetectorProbe.gameObject);
        var backend =
#if IL2CPP
            "Il2Cpp";
#else
            "Mono";
#endif
        var result = $"{(passed ? "PASS" : "FAIL")}|Backend={backend}|{details}";
        File.WriteAllText(_resultPath, result);
        if (passed)
            MelonLogger.Msg($"[S1ForkliftSmoke] {result}");
        else
            MelonLogger.Error($"[S1ForkliftSmoke] {result}");
        Application.Quit();
    }
}
