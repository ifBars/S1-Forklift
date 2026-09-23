# Forklift & Pallets

Forklift & Pallets adds a forklift and storage pallets to Schedule I, so you can load goods, move them around your properties, and set them down where you need them.

## What it is

Storage Pallets are placeable furniture with native storage. Fill one, line it up with the forklift, lift it, drive it to its destination, and set it down again. Pallets remain in the world and their placement is restored after save/load.

## Features

- Adds a drivable forklift that uses Schedule I's normal vehicle controls.
- Buy the **Forklift** for **$20,000** from Jeremy at **Hyland Auto**.
- Adds the **Storage Pallet**, a placeable wooden pallet with native storage.
- Buy Storage Pallets for **$60** from **Handy Hank's Hardware** and **Dan's Hardware**.
- Fill a pallet, move it on the forks, and set it down again.
- Stored items remain accessible while a pallet is being carried.
- Carry a Storage Pallet into a shop loading bay to use Schedule I's **Load vehicle** checkout option; purchases load onto the pallet, while a forklift without one exposes no vehicle storage.
- Property gates and police checkpoints recognize the forklift as a vehicle.
- Assign placed Storage Pallets as employee supplies or route storage so workers can retrieve seeds and other items.
- Assign an employee route to a property **Loading Dock**, then park a stopped forklift carrying a Storage Pallet there to have the employee walk to and load the pallet on the forks.
- Police checkpoint officers search the carried pallet at the forks, including its contents in the contraband check, instead of searching a car-style trunk at the rear.
- Increased motor torque makes hill climbing easier without changing the forklift's 22 km/h speed setting.
- Held items are hidden from the seated driver avatar and return normally after exiting.
- Forklift handling uses front-wheel drive and rear-wheel steering.
- Forklift and free-world pallet placement persist through save/load.
- Lift keys, lift speed, and maximum lift height are configurable through MelonPreferences.

## Basic install

- Install **MelonLoader** for Schedule I.
- Install the matching **S1API** release for your game runtime.
- Download the matching Forklift & Pallets DLL.
- Put that DLL in Schedule I's `Mods` folder.
- Launch the game.

## Important: pick the right file

Install only the DLL that matches your game runtime.

- **IL2CPP:** `ForkliftandPallets_Il2Cpp.dll`
- **Mono:** `ForkliftandPallets_Mono.dll`

## Controls

Use Schedule I's normal vehicle controls to drive, brake, enter, and exit.

- `Page Up`: Pick up an aligned pallet, then raise the forks.
- `Page Down`: Lower the forks. Near level ground and below 5 km/h, this releases the carried pallet.

## Employee loading docks

Set the employee route's source to the storage holding the items and its destination to the property's **Loading Dock**. Park a forklift carrying a Storage Pallet in that dock and stop the vehicle. The employee will walk to a reachable side of the pallet on the forks and load routed items into it; the route waits whenever the forklift or pallet is absent.

## Configuration

Open MelonPreferences and look for **ForkliftandPallets** to change the lift keys, maximum lift height, and lift speed.

## Requirements and compatibility

- Schedule I
- MelonLoader
- S1API
- The matching Forklift & Pallets build for Mono or IL2CPP

The mod ships separate Mono and IL2CPP builds. Vehicle driving and spawning use Schedule I's native networking systems.

## Multiplayer note

The forklift lift state and carried-pallet synchronization are currently local to the controlling player. Multiplayer pallet lifting should be treated as experimental.

## Notes

- Set pallets down close to level ground; they drop and settle physically before becoming stable.
- Storage Pallets use Schedule I's native placeable-storage interaction and save behavior.
