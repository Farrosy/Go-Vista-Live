# Venue-Travel / Scene-Bridge — Changelog & Findings

> Companion to [`venue-travel-plan.md`](./venue-travel-plan.md). This file records what was actually
> done per phase, the data gathered during inspection, decisions made, and anything that deviated
> from the plan. Newest entries on top.

---

## 2026-11-03 — Phase 4 implemented (dock → destination choice) + part of 6.1 pulled forward

**Branch:** `feat/spawnpoint-to-new-island` · **Scope:** owner chose the **full, testable slice**, so this
includes the WorldScene dock wiring that the plan nominally files under Phase 6.1. Result: compiles clean,
scene wiring verified on live objects. The dock interaction + choice panel + fade + scene-swap are
exercisable now, **but arrival onto the venue cube needs Phase 5** (the `VenueCube` spawn markers are placed
in 5.1). Until then, `TravelTo(venue, VenueCube)` loads/swaps scenes but `PlayerSpawnPoint.Find(..., VenueCube)`
returns null, so the avatar isn't repositioned (and WorldScene has unloaded) — don't expect a clean landing yet.

### New scripts (`Assets/Script/Travel/`)
- **`TravelChoiceUI.cs`** — reusable choice panel, built **procedurally at runtime** (own overlay Canvas,
  sortingOrder 25000 — above HUD, below the 30000 fade), so no scene-UI authoring. Dark backdrop + centered
  panel (VerticalLayoutGroup + ContentSizeFitter) + TMP title + one button per option + a "Batal" cancel.
  API: `Open(string title, (string label, Action onPick)[] options, Action onCancel = null)` / `Close()`.
  While open it locks input (`PlayerInputHub.SetPauseLocked(true)`) and suppresses the Interact button
  (`InteractButtonUI.SetSuppressed(true)`); a pick closes first then runs the action. Singleton + `ResetStatics`.
- **`DockTravelPoint.cs`** — `[RequireComponent(Interactable)]`. In `OnEnable` adds a listener to the
  `Interactable.onInteract` UnityEvent; on interact opens `TravelChoiceUI` with **Dance Club** →
  `TravelTo(DanceClub, VenueCube)` and **Rooftop Bar** → `TravelTo(RooftopBar, VenueCube)`. Serialized
  title/labels (Indonesian defaults).

### New asset
- **`Assets/NPCAction/Travel.asset`** — `InteractionAction`, verb **"Berlayar"** (no icon; the verb carries it),
  placed alongside the existing `Talk`/`Sit` actions.

### Scene edits
- **`MainMultiplayerScene.unity`** — added an empty **`TravelChoiceUI`** GameObject (the panel builds itself at
  runtime). Persistent, reachable from every venue.
- **`WorldScene.unity`** (the "pull 6.1 forward" part):
  - **`Docks SpawnPoint`** — `PlayerSpawnPoint` venue=World, **slot=Docks**, at **(230, 22.298, 380)** facing
    south (inland), spreadRadius 2. This is the return-from-venue spawn.
  - **`DockTravelPoint`** — a trigger zone on the pier beside the boat: `BoxCollider` (trigger, center
    (0,1.8,0), size 8×5×14) + `Interactable` (displayName "Dermaga", action `Travel.asset`, promptPrefab
    `Assets/MobilePrefabs/InteractPrompt.prefab`, offset (0,2.8,0)) + `DockTravelPoint`. At (229, 22.298, 382).

### Dock geometry mapping (raycast survey of WorldScene)
The mesh **pivots are misleading**: `marina_001` pivot y=15.4, `boat_002` pivot y=21.95 are NOT the deck.
A downward-raycast grid (x 226–246, z 356–388) found the real layout:
- Walkable **`marina_001` deck = the west strip x≈226–232**, running north–south, **surface y≈22.30**; plus a
  full east–west base row at z≈356 joining the land.
- **`boat_002`** occupies x≈234–240, z≈376–388 (moored on the east side of the north end); its collider top is
  y≈23–29 (bow/cabin taller).
- Everything else (x≥234 south of the boat, x≥242) is the **`water`** collider at y≈21.8.
So the player walks up the west strip to the north end next to the boat — where the trigger + Docks spawn sit.

### Bug fixed during build
`TMP_Text.enableWordWrapping` is now an **error** (CS0619) in this TMP version → switched to
`text.textWrappingMode = TextWrappingModes.Normal`.

### Verification
- Compile clean (0 errors) after the TMP fix.
- Live objects confirmed: `Docks SpawnPoint` serialized venue=0/slot=1; `DockTravelPoint` has
  BoxCollider(trigger)+Interactable(wired to Travel action + InteractPrompt)+DockTravelPoint; onInteract is
  empty in-scene (wired at runtime by `DockTravelPoint`).
- Both scenes saved; editor restored to MainMultiplayerScene (WorldScene unloaded).

### Decisions / notes
- **Procedural `TravelChoiceUI`** (not scene-authored) — consistent with the runtime fade overlay; robust and
  self-contained. Styling is a reasonable dark theme, not a pixel-match of the lobby; easy to restyle later.
- **Pulled Phase 6.1's WorldScene dock wiring forward** (owner's choice) so the flow is testable now. Phase 6.1
  still owns: confirming the CentralPlaza spawn (unchanged) and any polish; its DockTravelPoint/Docks-spawn
  boxes are effectively done.
- **CharacterController fires trigger events** without a Rigidbody (same as existing NPC Interactables), so the
  dock trigger needs none.
- **Testable now** (needs a human to drive): walk up the pier → "Berlayar" prompt appears → tap → choice panel
  (Dance Club / Rooftop Bar / Batal) → pick → fade + venue scene loads. **Arrival onto the cube does NOT work
  until Phase 5.1** adds the `VenueCube` `PlayerSpawnPoint` on each venue's `Cube`; without it the avatar isn't
  placed and falls (WorldScene unloaded). Cancel/return-arm behavior is also Phase 5.

### Files touched
- **New:** `Assets/Script/Travel/TravelChoiceUI.cs`, `DockTravelPoint.cs` (+ `.meta`),
  `Assets/NPCAction/Travel.asset` (+ `.meta`)
- **Modified:** `Assets/Scenes/MainMultiplayerScene.unity`, `Assets/Scenes/WorldScene.unity`

---

## 2026-11-03 — Phase 3 implemented (travel manager / the teleporter)

**Branch:** `feat/spawnpoint-to-new-island` · **Scope:** Phase 3 only. Result: compiles clean, scene wiring
verified on disk. **Not yet runtime-tested** — `TravelTo` has no caller until Phase 4/5 (dock + pad).

### New scripts (`Assets/Script/Travel/`)
- **`ISceneTransition.cs`** — seam with `IEnumerator Cover()` / `IEnumerator Reveal()`. `TravelTo` only calls
  `Cover()` before load/unload and `Reveal()` after the avatar is placed; it never touches a CanvasGroup or
  fade timing. This lets a future `LoadingScreenTransition` drop in with zero changes to `TravelTo`. Contract:
  `Cover()` holds the screen covered until `Reveal()`; `Reveal()` is called only once the target is ready, so a
  loading screen can treat that call as its "ready" signal.
- **`FadeToBlackTransition.cs`** — default `ISceneTransition`. Builds its **own** full-screen black overlay
  Canvas at runtime (ScreenSpaceOverlay, `sortingOrder 30000` above all UI, starts transparent &
  non-blocking) — so **no scene UI authoring is required**. `Cover()` fades alpha→1 and blocks raycasts;
  `Reveal()` fades →0 and unblocks. Black is held between the two calls by construction.

### Rewritten script
- **`VenueTravelManager.cs`** — keeps the Phase 2 static venue state; now also the Phase 3 teleporter:
  - Singleton (`Instance`), `VenueId CurrentVenue` (loaded venue scene), `bool IsTraveling`.
  - `ISceneTransition Transition` accessor: serialized `_transitionComponent` override, else an attached
    `FadeToBlackTransition`, else (runtime only) lazily adds one. Single swap point for the future loading screen.
  - `AdoptInitialVenue(VenueId)` — called by `LobyUISceneController` after first spawn to register World.
  - `TravelTo(VenueId, SpawnSlot)` → guarded coroutine implementing all 9 plan steps: input lock + hide mobile
    controls + `Cover()`; load target additive; disable target cameras/AudioListeners (same pattern as
    `DisableWorldCameras`); `SetActiveScene(target)`; unload the previous venue scene (skipped if
    `previous == target`; MainMultiplayerScene never unloaded); safe avatar teleport (disable
    CharacterController → `SetPositionAndRotation` at `PlayerSpawnPoint.Find(target, slot)` with golden-angle
    spread → `Physics.SyncTransforms()` → re-enable CC); `PlayerVenueSync.SetVenue(target)` (fires
    `VenueChanged`, re-evaluates visibility while black); re-assert `CameraOrbit` target; `Reveal()` + unlock +
    restore controls. Re-entry guarded; a load failure reveals + unlocks so it can't soft-lock on black.
  - Dependencies resolved at runtime (`PlayerController.Local`, `FindAnyObjectByType<Realtime>/<CameraOrbit>`),
    so the scene GameObject needs no inspector wiring.

### Scene / integration edits
- **`MainMultiplayerScene.unity`** — added an empty `VenueTravelManager` GameObject (root, next to
  `PlayerManager`) with the `VenueTravelManager` component. Verified on disk: GameObject `m_Name:
  VenueTravelManager` → MonoBehaviour guid `3e866188eefa6114da8dc4ce2132cf4b`. The fade is created at runtime,
  so nothing else was added to the scene.
- **`LobyUISceneController.cs`** — one hook in `LoadWorldAndBeginGame` right after `playerManager.BeginGame()`:
  `VenueTravelManager.Instance?.AdoptInitialVenue(VenueId.World)`. "Leave Room" → lobby path left unchanged.

### Bug caught & fixed during wiring
The first `Transition` getter did `AddComponent<FadeToBlackTransition>()` unconditionally — a **mutating side
effect in a property getter**. Inspecting the component via MCP (which reads properties) triggered it and added
a stray `FadeToBlackTransition` to the scene in **edit mode**. Fixed: the getter now only creates the fade when
`Application.isPlaying`; in edit mode it returns the serialized/attached one or null, never mutates. Removed the
stray component and re-verified the scene has only the manager.

### Verification
- Compile clean (0 errors) after each step.
- Scene saved; on-disk `MainMultiplayerScene.unity` confirmed to reference the manager script and nothing else.
- Live editor: `VenueTravelManager` reports `CurrentVenue = World (0)`, `IsTraveling = false`, `Transition =
  null` in edit mode (no edit-mode pollution).

### Decisions / notes
- **Runtime-created fade overlay** (not scene-authored) — avoids fragile scene-UI edits and guarantees it sits
  above everything; self-contained and swappable.
- **Avatar velocity:** relying on CharacterController disable→move→enable (+ `Physics.SyncTransforms`) to avoid
  a fling; did not add a `PlayerController.ResetMotion()` since `cc.isGrounded` self-corrects `verticalVel`
  next frame. Revisit in Phase 7.4 if teleport-fling shows up.
- **Initial WorldScene stays non-active** (unchanged from today's first-spawn flow); a round-trip via
  `TravelTo(World, Docks)` will `SetActiveScene(WorldScene)`. Minor inconsistency, kept to minimize churn.
- `TravelTo` is the entry point Phase 4 (`DockTravelPoint`) and Phase 5 (`TravelPad`) will call:
  `VenueTravelManager.Instance.TravelTo(venue, slot)`.

### Files touched
- **New:** `Assets/Script/Travel/ISceneTransition.cs`, `FadeToBlackTransition.cs` (+ `.meta`)
- **Modified:** `Assets/Script/Travel/VenueTravelManager.cs`, `Assets/Scenes/LobyUISceneController.cs`,
  `Assets/Scenes/MainMultiplayerScene.unity`

---

## 2026-11-03 — Phase 2 implemented (venue tag sync, one-room visibility)

**Branch:** `feat/spawnpoint-to-new-island` · **Scope:** Phase 2 only. Result: compiles clean, prefab wiring
verified on disk.

### New scripts (`Assets/Script/Travel/`)
- **`PlayerVenueModel.cs`** — `[RealtimeModel]` partial with `[RealtimeProperty(1, true, true)] int _venue`
  (reliable + change-callback; `0 = VenueId.World`). Source-generated by Normcore like `PlayerAnimSyncModel`.
- **`PlayerVenueSync.cs`** — `RealtimeComponent<PlayerVenueModel>` (requires `PlayerController`):
  - `VenueId Venue` getter (World when model null / offline), `bool IsLocalAvatar` (`isOwnedLocallyInHierarchy`).
  - `event Action<PlayerVenueSync, VenueId> VenueChanged` — fires on `venueDidChange` and on model-replace
    (so avatars already in the room when we join are evaluated).
  - `SetVenue(VenueId)` — local-owner-only writer; called by the travel manager in Phase 3.
  - When the **local** avatar's venue changes, mirrors it into `VenueTravelManager.SetLocalVenue(...)`.
- **`VenueVisibilityFilter.cs`** — `RequireComponent(PlayerVenueSync)`. Enables/disables all child `Renderer`s
  **and** `Collider`s so an avatar is shown only when `Venue == VenueTravelManager.LocalVenue`. The **local
  avatar is always visible to itself** (its CharacterController/renderers are never disabled). Re-evaluates on
  (a) its own `PlayerVenueSync.VenueChanged` and (b) the static `VenueTravelManager.VenueChanged`. Caches last
  state to avoid redundant toggles.
- **`VenueTravelManager.cs`** — **Phase 2 slice only**: static `VenueId LocalVenue` (default World),
  `static event Action VenueChanged`, `static SetLocalVenue(VenueId)` (fires on change), and `ResetStatics`
  via `[RuntimeInitializeOnLoadMethod(SubsystemRegistration)]`. File header documents that the Phase 3
  singleton + `TravelTo(...)` coroutine land in this same class later.

### Data flow (how one-room visibility works)
Local avatar is the source of truth for its venue → `PlayerVenueSync.SetVenue` writes the synced model →
`venueDidChange` fires locally and remotely → each avatar's `PlayerVenueSync.VenueChanged` updates its own
filter, and the local one also pushes `VenueTravelManager.SetLocalVenue` → static `VenueChanged` → **every**
filter re-evaluates. Net: you see/collide only with avatars whose `venue == your venue`.

### Prefab wiring (owner chose "wire + verify now")
Attached `PlayerVenueSync` + `VenueVisibilityFilter` to **both** avatar prefabs and registered the sync
component in each `RealtimeView._components`. A `RealtimeComponent` must be registered in the `RealtimeView`
(not just attached) or it never syncs — `RealtimeView` exposes no public registration API, so this was done
headless via `PrefabUtility.LoadPrefabContents` + `SerializedObject` on `_components`, then verified in the
on-disk YAML.

| Prefab | RealtimeView `_components` after |
|---|---|
| `Assets/Resources/Player.prefab` | 1→RealtimeTransform, 2→PlayerAnimSync, **3→PlayerVenueSync** |
| `Assets/Resources/Player Female.prefab` | 1→RealtimeTransform, 2→PlayerAnimSync, **3→PlayerVenueSync** |

Verified: both GameObjects' `m_Component` lists include the two new components; `PlayerVenueSync` block has
script guid `837631caffc075844b9ce71bf0f1fc28`, `VenueVisibilityFilter` block has `b50922f18471ae44da56e2b0e3036c73`.

### Verification
- Compile clean (no errors; the only warnings are pre-existing `FindFirstObjectByType` obsoletions in other
  files + the transient MCP WebSocket notice). Absence of errors confirms the Normcore source generator
  produced `PlayerVenueModel` (`model.venue` / `model.venueDidChange` resolve).
- Prefab reimport after save logged **0 errors**.

### Decisions / notes
- **Created `VenueTravelManager` now** (not Phase 3) because 2.2/2.3 reference its static `LocalVenue` /
  `VenueChanged`. Only the static state is implemented; the singleton + `TravelTo` are deferred to Phase 3.
- **Offline/DevScene safety:** `PlayerController` disables `RealtimeView`/`RealtimeTransform` when there's no
  `Realtime` in the scene, so the model stays null. `PlayerVenueSync.Venue` returns `World`, `LocalVenue` is
  `World`, so the filter keeps everything visible — no offline regressions.
- **`componentID` = max existing + 1 (= 3)**, matching how Normcore's inspector assigns IDs.
- Not yet runtime-tested with two clients (that's Phase 7.3); logic is wired and compiles.

### Files touched
- **New:** `Assets/Script/Travel/PlayerVenueModel.cs`, `PlayerVenueSync.cs`, `VenueVisibilityFilter.cs`,
  `VenueTravelManager.cs` (+ their `.meta`)
- **Modified:** `Assets/Resources/Player.prefab`, `Assets/Resources/Player Female.prefab`

---

## 2026-11-03 — Phase 0 + Phase 1 implemented

**Branch:** `feat/spawnpoint-to-new-island`
**Unity:** 6000.6.6f1 · **Editor instance:** `Go-Vista-Live@12411e9c3aee10d2`
**Scope:** Phase 0 (pre-flight) and Phase 1 (core data & spawn refactor) only. No scenes were modified or
saved (scene wiring is Phase 6). Result: compiles clean.

### Phase 0 — Pre-flight

#### 0.1 Build Settings
Added both venue scenes (enabled) via an idempotent append that preserved the existing entries and their
enabled flags — importantly, `GameScene` stayed **disabled**. Flushed to disk (`AssetDatabase.SaveAssets()`),
so `ProjectSettings/EditorBuildSettings.asset` is modified.

Final build scene list (6):

| # | Scene | Enabled |
|---|---|---|
| 0 | `Assets/Scenes/MainMultiplayerScene.unity` | ✅ |
| 1 | `Assets/Scenes/WorldScene.unity` | ✅ |
| 2 | `Assets/Scenes/DevScene/GameScene.unity` | ⬜ (left disabled) |
| 3 | `Assets/Scenes/Multiplayer/LobbyReturnScene.unity` | ✅ |
| 4 | `Assets/Download_Assets/PolygonNightclubs/Scenes/Demo_DanceClub_01.unity` | ✅ (new) |
| 5 | `Assets/Download_Assets/PolygonNightclubs/Scenes/Demo_RooftopBar_01.unity` | ✅ (new) |

#### 0.2 Dock inspection (WorldScene)
All top-level objects, rotation `(0,0,0)`, scale `(1,1,1)`:

| Object | World position | Components | Notes |
|---|---|---|---|
| `marina_001` | (233.906, 15.403, 364.859) | Transform, MeshFilter, MeshRenderer, MeshCollider | The L-shaped pier/dock |
| `boat_002` | (236.983, 21.953, 385.805) | Transform, MeshFilter, MeshRenderer, MeshCollider | Moored at the north end of the pier |
| `PlayerSpawnPoint` (existing CentralPlaza) | (247.5, 23.2, 158.74) | Transform, PlayerSpawnPoint | Plaza walkable ground ≈ y 23.2; `spreadRadius` = 10 |

Visual check (temp camera above the dock): the pier is an **L-shape** with the boat moored at the north
end and open deck to the south — plenty of room for a return-spawn.

**Recommendation for the Phase 6.1 `Docks` return spawn:** place it on the open pier deck a few metres
south of `boat_002` (≈ x 234, z 378), facing inland (south, toward the island). Set the exact **Y by a
ground raycast at wiring time** — the mesh pivots (marina 15.4, boat 21.95) are *not* the walkable deck
height, so don't copy them directly.

#### 0.3 Venue `Cube` inspection
Each venue scene has a clean, **root-level, axis-aligned default `Cube`** (Unity default cube mesh, URP Lit
material, 1×1×1 `BoxCollider`, non-trigger). Good anchor to reuse as the `VenueCube` spawn marker in Phase 6.

| Venue scene | `Cube` local/world pos | Rotation | Floor base (pos.y − 0.5) |
|---|---|---|---|
| `Demo_DanceClub_01` | (-11.352, -14.611, 14.032) | (0,0,0) | ≈ y -15.11 |
| `Demo_RooftopBar_01` | (11.153, 0.745, 3.916) | (0,0,0) | ≈ y 0.25 |

**Reuse vs replace:** reuse the existing `Cube` as the anchor (position + collider are fine). Phase 5.2 still
requires turning it into a proper-looking walkable pad (glow ring / beam / label), but there's no need to add
a brand-new object — attach `TravelPad` + `PlayerSpawnPoint(slot = VenueCube)` to it.

### Phase 1 — Core data & spawn refactor

#### 1.1 `Assets/Script/Travel/VenueId.cs` (new)
- `enum VenueId { World = 0, DanceClub = 1, RooftopBar = 2 }` — explicit numeric values with a "don't
  reorder/renumber" note, because these get serialized on components and synced over Normcore in Phase 2.
- `static class VenueMap`:
  - `string SceneName(VenueId)` → `"WorldScene"` / `"Demo_DanceClub_01"` / `"Demo_RooftopBar_01"` (by name,
    since scenes load additively and must be in Build Settings).
  - `VenueId? FromScene(string)` → accepts a scene name **or** a full asset path (strips folders + `.unity`);
    returns `null` for an unknown scene.
  - Scene-name constants exposed as `WorldSceneName` / `DanceClubSceneName` / `RooftopBarSceneName`.

#### 1.2 `Assets/Script/PlayerScript/PlayerSpawnPoint.cs` (extended — chose **extend**, not a sibling)
- Added `enum SpawnSlot { CentralPlaza = 0, Docks = 1, VenueCube = 2 }`.
- Added serialized fields `venue` (default `World`) and `slot` (default `CentralPlaza`), plus `Venue`/`Slot`
  getters.
- Replaced the single `static Active` with a `static Dictionary<(VenueId, SpawnSlot), PlayerSpawnPoint>`
  registry — populated in `OnEnable`, removed in `OnDisable`, warns on duplicate `(venue, slot)` keys.
- Added `static PlayerSpawnPoint Find(VenueId venue, SpawnSlot slot)` (null if none loaded).
- Kept `GetPosition(clientID)` golden-angle spread and `Rotation` unchanged.

#### 1.3 `Assets/Script/Multiplayer/PlayerManager.cs` (edited)
- `SpawnLocalPlayer()` now resolves the first spawn via
  `PlayerSpawnPoint.Find(VenueId.World, SpawnSlot.CentralPlaza)` instead of `PlayerSpawnPoint.Active`.
- Kept the existing fallback-warning scatter path (message updated to name World/CentralPlaza).

### Verification
- **Compile:** clean. Only console output after the final forced recompile + domain reload was a transient
  MCP WebSocket warning (`WebSocket is not initialised`) from the reload itself — unrelated to this code.
- **Backward compat confirmed live:** the pre-existing WorldScene spawn point deserialized to
  `venue = World (0)` / `slot = CentralPlaza (0)` and preserved its `spreadRadius = 10`. So
  `Find(World, CentralPlaza)` resolves it at runtime with no re-setup needed.
- **Blast radius of dropping `Active`:** the only code reference was `PlayerManager` (now updated); all other
  mentions are comments.

### Decisions made
- **Phase 1.2 — extend vs sibling:** chose to **extend `PlayerSpawnPoint`**. The default enum values
  (`0/0 = World/CentralPlaza`) give backward compatibility for free, and the plan's sub-bullets describe
  modifying `PlayerSpawnPoint` directly.
- **`SpawnSlot` location:** defined in `PlayerSpawnPoint.cs` (tightly coupled to the spawn component), while
  `VenueId` lives in `Travel/VenueId.cs`. No asmdefs in the project — everything is `Assembly-CSharp`, global
  namespace — so cross-folder references need no wiring.

### Gotchas worth remembering
- **`find_gameobjects` searches only the ACTIVE scene**, not all loaded scenes. An additively-loaded venue's
  objects won't appear until you `set_active_scene` to it. This briefly made the venue `Cube`s look missing.
- **WorldScene also has an object named `Cube`** at the dock — but it's a *tilted* child of a leftover
  `Main Camera` (`Main Camera/Cube`, world ≈ (230.6, 22.8, 379.4), rotation ≈ (5.6, 356.7, 8.6)). It is **not**
  a spawn marker; don't confuse it with the venue cubes.
- The plan's note that "`Demo_DanceClub_01` is the scene currently open in the editor" was stale — the active
  scene at implementation time was `WorldScene`.

### Files touched
- **New:** `Assets/Script/Travel/VenueId.cs` (+ `.meta`), `Assets/Script/Travel.meta`
- **Modified:** `Assets/Script/PlayerScript/PlayerSpawnPoint.cs`,
  `Assets/Script/Multiplayer/PlayerManager.cs`, `ProjectSettings/EditorBuildSettings.asset`
- **Not modified:** any `.unity` scene (scene wiring deferred to Phase 6)

### Loose ends
- Two inspection screenshots were generated under `Assets/Screenshots/` during 0.2 and could not be auto-removed
  (filesystem delete was denied by the permission layer). They're untracked — remove manually if unwanted.
