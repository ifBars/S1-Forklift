namespace S1Forklift;

internal static class Constants
{
    internal const string ModName = "Forklift & Pallets";
    internal const string ModVersion = "1.2.0";
    internal const string ModAuthor = "Bars";

    internal const string VehicleCode = "forklift";
    internal const string PalletItemId = "pallet";
    internal const string PalletPrefabName = "S1Forklift_Pallet";
    internal const string PalletSourceItemId = "safe";
    internal const string PalletSourceResourcePath = "storage/safe/Safe_Built";
    internal const string PalletVisualResourcePath = "furniture/garbagethrone/GarbageThrone_Built";
    internal const string PalletAvatarResourcePath =
        "S1Forklift/Items/StoragePallet/Held";
    internal const int PalletSlotCount = 24;
    internal const int PalletDisplayRowCount = 4;
    internal const int PalletPlacementFootprintSize = 4;
    internal const float PalletStorageUiScale = 0.8f;
    internal const float PalletStorageUiVerticalOffset = 60f;
    internal const string PalletDisplayName = "Storage Pallet";
    internal const string PalletInteractionMessage = "Open Storage Pallet";
    internal const float PalletInteractionRange = 4f;
    internal const float PalletInteractionHeight = 0.5f;
    internal const float PalletStorageSurfaceClearance = 0.002f;
    internal const float PalletPickupLowerVerticalTolerance = 0.2f;
    internal const float PalletReleaseMaximumLiftFraction = 0.2f;
    internal const float PalletPlacementAnchorY = -0.025f;
    internal const float PalletTransitAccessOffset = 1f;
    internal const float PalletTransitAccessHeight = -0.85f;
    internal const float PalletTransitNavMeshOffset = 0.05f;
    internal const float PalletTransitNavMeshSampleDistance = 1.25f;
    internal const float PalletTransitLinkHeight = 0.25f;
    // The cloned Shitbox wheel colliders use 6,000, which is under-damped for
    // the forklift chassis and causes the body to oscillate.
    internal const float ForkliftMassKg = 3560f;
    internal const float ForkliftCenterOfMassHeightMeters = 0.49f;
    internal const float ForkliftCenterOfMassLongitudinalMeters = -0.42f;
    internal const float ForkliftMaximumDrawbarPullNewtons = 8500f;
    internal const float ForkliftMaximumDrivePowerWatts = 30000f;
    internal const float ForkliftDriveWheelRadiusMeters = 0.265f;
    internal const float ForkliftDifferentialGearing = 7f;
    internal const float ForkliftGovernorStartSpeedKmh = 16.5f;
    internal const float ForkliftTopSpeed = 18f;
    internal const float ForkliftBrakeForceMultiplier = 0.18f;
    internal const float ForkliftHandBrakeForce = 900f;
    internal const float ForkliftLinearDrag = 0.04f;
    internal const float ForkliftWheelSpring = 70000f;
    internal const float ForkliftWheelSpringDamper = 14000f;
    internal const string VehicleName = "Forklift";
    internal const float VehiclePrice = 20000f;
    internal const string DealershipChoiceLabel = "FORKLIFT";
    internal const string DealershipChoiceText = "The Forklift (<PRICE>)";
    internal const string DealershipPageAnchorChoiceLabel = "HOTBOX";
    internal const string DealershipChoiceGuid = "s1forklift-hyland-auto-choice";
    internal const string BaseVehicleCode = "shitbox";
    internal const string ForkliftResourcePath = "vehicles/unused/forklift/Forklift";
    internal const string PrefabName = "S1Forklift_NetworkPrefab";
    internal const string VisualRootName = "S1Forklift_Visual";
}
