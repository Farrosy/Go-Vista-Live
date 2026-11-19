# Building Interiors — fixed top-down camera (Persona / Synty diorama style)

> **Status:** Barber v1 BUILT 2026-11-19 (branch `feat/barber`). Compile-clean. Camera mode was tested in play mode
> (enters, follows, clamps, restores). The full door → interior → door loop is **NOT yet play-tested** in a real
> Normcore session. References and angle renders: `_temp/barber-interior-refs/` (temporary).

## Locked decisions (owner, 2026-11-19)
| Topic | Choice |
|---|---|
| Entry | **Fade + teleport** through the existing `VenueTravelManager` (not a same-scene walk-in) |
| Where the interior lives | **Own venue scene** per building (`Interior_Barber`) + its own `VenueId` |
| Outside while inside | **Dark void** (solid camera background, no skybox) |
| Camera | **Fixed angle, follows the player, clamped to the room**: pitch 48°, yaw 120°, FOV 28°, distance 13 m |
| Cutaway | The roof and camera-side walls on the interior copy have their **renderers disabled permanently**. Colliders stay, so players can't walk out. A dither fade isn't needed while entry is behind a fade. |

## How it works
1. WorldScene `Barbershop_Entrance/EnterDoor` (trigger + `Interactable` "Masuk" + `DoorTravelPoint`) →
   `VenueTravelManager.TravelTo(Barber, InteriorEntrance)`.
2. Travel = fade to black, load `Interior_Barber` additively, unload WorldScene, teleport the avatar to
   `Spawn_InteriorEntrance`, set the synced venue tag to `Barber`, fade in. Interiors keep **avatar scale 1**
   (the KPOP venues use 0.9).
3. `InteriorCameraZone` on the interior root sets `InteriorCameraZone.Active`. `CameraOrbit.UpdateInteriorZone()` then
   saves FOV/clear flags/background, switches to the fixed pose, ignores look input, and skips wall collision.
   Unloading the scene clears `Active` and `CameraOrbit` restores everything. The orbit camera snaps behind the
   avatar when entering or leaving an interior (`SnapBehindTarget`).
4. Inside: `ExitDoor` strip along the shop front ("Keluar") → `TravelTo(World, BarberFront)` → you spawn
   outside the barber door, facing the street.

Multiplayer needs nothing new. The venue tag means you only see players in the same building. The minimap shows its
placeholder (no scan in the interior scene). NPCs and cars pause their authority while you're out of World, as in the KPOP venues.

## Files
- Code: `Assets/Script/Interior/InteriorCameraZone.cs`, `Assets/Script/Travel/DoorTravelPoint.cs`; edits in
  `CameraOrbit.cs`, `VenueId.cs` (`Barber = 3`, `IsInterior`), `PlayerSpawnPoint.cs` (`BarberFront = 3`,
  `InteriorEntrance = 4`), `VenueTravelManager.cs`, `LocalPlayerVitals.cs` (respawn inside), `TravelPad.cs` (label).
- Content: `Assets/Scenes/Interiors/Interior_Barber.unity` (in Build Settings), `Assets/NPCAction/EnterBuilding.asset`,
  `ExitBuilding.asset`, and `Barbershop_Entrance` in WorldScene (front of `barbershop_001`, door at z ≈ 307.5).
- The interior uses `Assets/Synty/PolygonShops/` (**currently untracked in git**; it must be committed or shared).

## Recipe: add another shop
1. Add a `VenueId` (append only, never renumber) + scene name in `VenueMap` + `IsInterior` + a `SpawnSlot` for its front door.
2. New scene under `Assets/Scenes/Interiors/`: a root at an empty spot (barber uses y = −200) with an `InteriorCameraZone`,
   the shop prefab centred on it, a key light + 1–2 point lights, flat ambient, no skybox, no fog.
3. Disable renderers (not colliders) for the roof and the walls between the camera and the room.
4. Add a `PlayerSpawnPoint` (venue/InteriorEntrance) and `ExitDoor` (trigger + Interactable(ExitBuilding) + DoorTravelPoint → World/front slot).
5. In WorldScene, at the facade: `EnterDoor` (Interactable(EnterBuilding) + DoorTravelPoint) and a `PlayerSpawnPoint` (World/front slot).
6. Add the scene to Build Settings.

## Clothing Shop (v1, 2026-11-19)
- Door: `ClothingShop_Entrance` in WorldScene, in front of `supermarket_001` (x ≈ 235.1, z ≈ 259.8, facing +x).
  `EnterDoor` → `TravelTo(ClothingShop, InteriorEntrance)`; leaving the shop spawns you at `Spawn_ClothingShopFront`.
- Code: `VenueId.ClothingShop = 4` (scene `Interior_ClothingShop`, `IsInterior`), `SpawnSlot.ClothingShopFront = 5`.
- Scene `Interior_ClothingShop` (in Build Settings): the full upstairs clothing store from the PolygonShops `Demo.unity`
  (Demo x 10–22.5, z −20…−10, y 6), centred on the root at y = −200. Room is 12.5 × 10 m, shopfront/glass door at local x = −6.25.
  Includes the roof (with rooftop AC and ducts) and the balcony in front of the shop (floor, railings, flags, facade sign).
  Hierarchy: `Shop/Structure/{Floor,Walls,Shopfront,FittingRooms,Ceiling,Roof}`, `Shop/Props/{Displays,Mannequins,WallRacks,Checkout,FittingRooms,Signs}`, `Shop/Front`,
  `Lighting` (key + 4 ceiling point lights), `Gameplay` (`Spawn_InteriorEntrance`, `ExitDoor`).
- Every renderer is on (full copy, nothing hidden).
- Camera: **normal third-person orbit, as on the main map** (owner, 2026-11-19). The `InteriorCameraZone` on the root is
  **disabled**, so `InteriorCameraZone.Active` stays null and `CameraOrbit` orbits with wall collision (distance 4 m).
  To go back to the fixed diorama view, enable the component and hide `Roof`/`Ceiling` plus the camera-side walls.

## Entrance markers (2026-11-19)
Every enterable door in WorldScene (`*_Entrance` with a `DoorTravelPoint` to an interior) gets a **Doormat**:
- a teal floor mat (`Assets/Art/Entrance/Doormat.mat`, arrows point at the door), sized to the flat floor in front of the
  door so it never runs over a curb;
- a short glow (1.1 m, fades upward, `DoormatGlow.mat`) along its front and side edges, so the entrance reads from about
  20 m and never pokes into the building from any angle;
- `BuildingEntranceMarker` on the entrance root: always visible (no distance fade), pulses, and turns **brighter and
  faster** while the local player stands on the mat.
The door's `Interactable.promptOffset` is moved to just above the door opening, in front of the facade.
(A floating name sign was tried first and dropped because, seen from the side, it clipped into the building.)
Run **Go Vista > Interior > Pasang Penanda Pintu Gedung** after adding an entrance. It probes the facade and the floor
with raycasts and can be re-run safely. The same menu rebuilds `InteractPrompt.prefab`, which **every** Interactable uses
(navy glass, gold verb badge, fade + pop, always drawn on top via `WorldUiOnTop`). Art in `Assets/Art/Entrance` is
code-drawn placeholder art: replace the PNGs to restyle.

## TODO / open
- [ ] **Play-test** the full loop with one client, then two (both inside; one inside, one outside).
- [ ] **Exit cost:** leaving reloads the whole WorldScene. If that's too slow on mobile, keep WorldScene loaded for
      interiors: skip the unload in `VenueTravelManager` when the target is an interior, and turn off World's sun/audio while inside.
      That's why the interior already sits at y = −200.
- [ ] Optional: a minimap blip at the barber door; a dither reveal when the fade lifts; barber interaction (haircut → customization).
