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

## Barber camera reverted (2026-11-23)
Owner decision: the clothing shop setup (normal third-person orbit) is the standard for **all** interiors. In
`Interior_Barber` the `InteriorCameraZone` is now **disabled**, and every renderer override on the `barber` prefab
instance was removed (only its name and position remain), so the roof and walls render again. The sections above that
describe the fixed camera and the cutaway are kept for reference only.

## Barber chair camera (2026-11-23, first pass, NOT play-tested)
Owner decisions: the swing starts the moment "Duduk" is pressed and arrives early, so the avatar walks into the
shot. Seated idle = **timed cuts** between shot A (front, from the mirror) and shot C (3/4 front), every 6 s, with the
camera **locked**. The hair / facial hair editor = a **zoomed A**, locked. Standing up swings back along the **same arc**.
- `ChairSeatCamera` (on `Barber Chair Seat`) implements `CameraOrbit.ICameraShot`: `CameraOrbit.SetShot`/`ClearShot`
  hand it the normal orbit pose each frame; look input is ignored while it's set, and the FOV is restored on clear.
- Arc = cylindrical coordinates around `Seat + 1.0 m` (yaw, flat radius, height). The radius closes in first (done by
  70% of the swing) and is clamped against walls; the chair and mirror colliders (`ChairSeat.ChairColliders`) are
  ignored, so shot A may sit inside the mirror unit (its back faces aren't drawn). If the camera starts roughly
  behind the chair (>135° from A), the swing goes round the C side (window side), away from the avatar's walk-in path.
- Shots are child transforms under `Barber Chair Seat/Camera Shots` (`Cam Front (A)`, `Cam Side (C)`, `Cam Editor`);
  move them to re-frame. FOVs (60 / 60 / editor 38), swing 1.5 s, editor glide 0.6 s, hold 6 s are on the component.
- The hair editor should call `GetComponent<ChairSeatCamera>().SetEditorShot(true/false)` on the chair.

### Cleanup pass (2026-11-25, NOT play-tested)
- **Idle shots move:** each shot moves for 5 s (`moveSeconds`, smoothstep), stops for 1.5 s (`stopSeconds`), then cuts.
  A moves from `Cam Front (A)` to `Cam Front (A) End` (a 12 cm push-in plus a 20 cm slide to the room side).
  C moves from `Cam Side (C) Start` to `Cam Side (C)` (a ~15° arc in toward the face that ends on the original C).
  The first cut waits until the avatar is seated. The cycle pauses while the editor is open and restarts at A afterwards.
- **Chair moved back 15 cm** (only `SM_Prop_Barber_Chair_01 (2)`, a prefab position override). Stepping sideways along
  the footrest, the hands span ±0.22 m, but the old seat-to-drawer gap was ~0.39 m, so `SeatIK` pushed the front hand
  ~13 cm into the hip. The builder now **measures** the drawer front from renderer bounds (the mirror's collider
  doesn't cover the drawers): drawers 0.89 → hand window up to 0.30, Stand Spot 0.58 (centre of the hand window).
  Sampled standing pose: hands now only 2 cm outside the window.
- **Ring** (`Ring` + 12 `Glow` renderers, `ChairSeat.hideWhileBusy`) is hidden from pressing Duduk until the avatar is
  back on the floor.
- **Third idle shot D** (owner, 2026-11-25): a 3/4 front from the room side (the original screenshot 165621). The
  shots are now a list `ChairSeatCamera.shots` (From → To + FOV), cut in order **A → C → D → A**. D arcs ~15° in
  toward the face (`Cam Room (D) Start` → `Cam Room (D)`). The swing-in/out only goes round `swingVia` (C, the window
  side) when the destination is frontal (A / editor). Leaving from D takes the short way via the room side (122°).
- **"Berdiri" floating label removed** (the Stand Up `Interactable` has no prompt prefab): the close shots clipped
  it at the top of the frame. The HUD Interact button still shows the stand icon.

## Barber hair editor (2026-11-25, offline Play-tested)
Owner picks: entry = extra HUD button **"Gaya Rambut"** next to the Interact (Berdiri) button while seated; scope =
**hair (Depan/Belakang) + beard (Jenggot) + their colours**; **free**; preview = **seated in the chair**. Berdiri is
not inside the editor: close the editor first.
- `BarberEditorStation` (on `Barber Chair Seat`, added by Pasang Kursi Barber) shows `HudActionButton` on
  `ChairSeat.Seated`. Open: lock input + hide HUD → `ChairSeatCamera.SetEditorShot(true)` → wait for the glide → fade →
  `CharacterSceneLoader.OpenFittingRoom(view with WorldEditorKind.Barber)` → hide the real avatar, main camera mask 0 →
  reveal once the preview is built. Close (KEMBALI / Back, after SIMPAN or discard) reverses it and glides back to A;
  the player stays seated. Interior unloaded mid-edit → everything restored at once (`OnDisable`).
- `HudActionButton` (Assets/Script/UI) clones the Interact button's look at runtime, follows its position (left of it,
  right if it doesn't fit) and hides/dims with it.
- CharacterScene: `EditorCategories.Barber` (Rambut + Jenggot), title BARBERSHOP, no Badan/Wajah toggle;
  `CharacterPreviewStage.EnterWorldMode(..., seatedPose: true)` holds `ChairSitIdle` + `SeatIK`, no face zoom; drag
  orbits the camera (below). Subject = the real avatar's model pose, view = `Cam Editor` + `editorFov`, so the preview matches within ~3 cm.
- Camera: drag on the character area orbits the preview camera around the head (vertical axis through a pivot on the
  `Cam Editor` view axis at head depth), up to +/-110 deg, eased with SmoothDamp (`seatOrbitSmoothTime` 0.28 s), stays
  where it is left. Pulled in (min 0.5 m) if a wall/prop is in the way; colliders the editor shot already looks
  through (mirror counter, chair) are ignored. Tunables on `CharacterPreviewStage` ("Kursi barber" header).
- SIMPAN = fitting-room path (template → new slot, full → overwrite choice, `Activate` + `PlayerAppearanceSync.RefreshLocal`).
- Untested: a 2nd Normcore client seeing the new hair; the real avatar re-baking (needs a room). A hat hides the front hair
  and can't be taken off here.

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
- [ ] Optional: a minimap blip at the barber door; a dither reveal when the fade lifts. (Haircut → customization: built, see Barber hair editor.)
