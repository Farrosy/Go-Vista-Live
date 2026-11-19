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

## TODO / open
- [ ] **Play-test** the full loop with one client, then two (both inside; one inside, one outside).
- [ ] **Exit cost:** leaving reloads the whole WorldScene. If that's too slow on mobile, keep WorldScene loaded for
      interiors: skip the unload in `VenueTravelManager` when the target is an interior, and turn off World's sun/audio while inside.
      That's why the interior already sits at y = −200.
- [ ] Optional: a minimap blip at the barber door; a dither reveal when the fade lifts; barber interaction (haircut → customization).
