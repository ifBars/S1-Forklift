# S1 Forklift

S1 Forklift turns Schedule I's installed, unused forklift resource into a native
`LandVehicle`. It builds a custom FishNet spawnable by cloning the game's
Shitbox network shell, replacing its presentation and collision geometry with
the locally installed forklift resource, and registering the result as vehicle
code `forklift`.

The mod does not include or redistribute the prefab, game assemblies,
AssetRipper exports, generated IL2CPP wrappers, saves, or other proprietary game
files. The forklift is loaded at runtime from:

```text
Resources/vehicles/unused/forklift/Forklift
```

## Controls

- `Page Up`: pick up an aligned pallet, then raise the fork carriage.
- `Page Down`: lower the fork carriage; near the ground and below 5 km/h,
  release the carried pallet.
- Normal Schedule I vehicle controls drive, steer, brake, enter, and exit.

Lift keys, lift speed, and maximum lift height are configurable in MelonPreferences
under `ForkliftandPallets`.

## Forklift

- Buy the **Forklift** for `$20,000` from Jeremy at **Hyland Auto**.
- The purchase uses Hyland Auto's native cash or online-payment flow and vehicle
  spawn points.
- Carry a pallet into a shop loading bay to use the native **Load vehicle**
  checkout option. Purchases fill the pallet on the forks; a forklift without a
  pallet exposes no vehicle storage.
- Property gates and police checkpoints detect it as an occupied vehicle.
- The inherited torque curve is strengthened for hills without changing the
  forklift's 22 km/h speed setting.
- Held items stay hidden on the seated driver avatar and return normally after
  exiting.

## Pallets

- Buy the **Forklift Pallet** for `$60` from either hardware store.
- Place and fill its native 6x6 storage grid like other placeable storage.
- Assign a placed pallet as employee supplies or route storage; its four access
  points allow workers to retrieve items such as seeds through native behavior.
- To have an employee load a carried pallet, assign the employee route's
  destination to a property **Loading Dock**, then park a forklift carrying a
  Storage Pallet in that dock. While the forklift is stopped, deliveries to the
  dock go directly into the pallet. Employees walk to a reachable side of the
  pallet on the forks instead of the rear of the forklift. The route waits when
  no eligible pallet is parked there.
- At police checkpoints, officers search the carried pallet at the forks and
  include its contents in the contraband check instead of opening the
  forklift's car-style trunk.
- Stored items remain interactable while the pallet is carried.
- Align the fork beneath the pallet and press `Page Up` to attach it.
- Lower it close to level ground and press `Page Down` while moving slower than
  5 km/h to set it down. The pallet falls and settles physically before becoming
  stable, and its saved world pose is updated.

### Mod integration

`S1Forklift.API.PalletApi` exposes the stable `pallet` item ID, settled-pallet
queries, storage access, and host-authoritative world-pallet creation for quest
mods. Settled queries exclude pallets that are still on a forklift or physically
dropping, so integrations can treat a pallet inside a loading zone as committed
world cargo.

## Architecture

- Uses a native `LandVehicle`, `Wheel`, `VehicleSeat`, Rigidbody, and FishNet
  `NetworkObject` shell.
- Uses front-wheel drive and rear-wheel steering, matching forklift handling.
- Reuses the unused prefab's local wheel geometry and colliders without
  packaging it.
- Registers the prefab in both `VehicleManager.VehiclePrefabs` and FishNet's
  spawnable-prefab registry, so native saves resolve the `forklift` vehicle
  code.
- Registers pallets through S1API and uses the native placeable-storage
  interaction and save systems.
- Searches around teleport anchors for a nearby, unoccupied, walkable surface;
  roofs, deep collision layers, walls, and occupied poses are rejected.
- Keeps lift state local to the controlling client in this first version.
  Driving and vehicle spawning use the native network stack; multiplayer lift
  and carried-pallet synchronization remain known limitations.

## Build

Copy `local.build.props.example` to `local.build.props`, set the two local game
paths, and build each backend separately:

```powershell
dotnet restore .\S1Forklift.csproj -p:Configuration=Mono
dotnet build .\S1Forklift.csproj -c Mono --no-restore -p:AutomateLocalDeployment=false

dotnet restore .\S1Forklift.csproj -p:Configuration=Il2cpp
dotnet build .\S1Forklift.csproj -c Il2cpp --no-restore -p:AutomateLocalDeployment=false
```

Run the dependency-free lift verifier with:

```powershell
dotnet run --project .\tests\S1Forklift.LiftMathVerifier\S1Forklift.LiftMathVerifier.csproj
```

## Runtime validation

For each backend:

1. Install only the matching `ForkliftandPallets_*.dll` in `Mods`.
2. Load a disposable or existing single-player save.
3. Confirm the log contains the `Registered native vehicle code 'forklift'`
   marker.
4. Enter a registered forklift, drive forward/reverse, and verify rear-wheel
   steering.
5. Drive through a property gate and checkpoint, then carry a pallet into a
   shop loading bay. Confirm **Load vehicle** fills the carried pallet and that
   the option is unavailable when the forklift has no pallet.
6. Raise and lower the fork through its full travel.
7. Buy and place a pallet, fill several storage slots, pick it up, drive with it
   elevated, set it down, and verify it settles without floating.
8. Assign the placed pallet as employee supplies and confirm a worker retrieves
   a required item from it.
9. Assign an employee route from a stocked storage entity to a property Loading
   Dock, park a stopped forklift carrying a pallet there, and confirm the worker
   loads the routed item into the carried pallet.
10. Save and reload, then confirm the forklift resolves from vehicle code
   `forklift` and free-world pallets restore their poses.
11. Capture and inspect screenshots for chassis alignment, wheel placement,
   driver position, fork travel, and collision behavior.
