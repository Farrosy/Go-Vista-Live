# Venue-Travel / Scene-Bridge System — Implementation Plan

> **Status:** In progress. Written 2026-11-02. **Phases 0–5 done 2026-11-03** (branch `feat/spawnpoint-to-new-island`); see [`venue-changelogs.md`](./venue-changelogs.md). Phases 4–5 pulled 6.1/6.2/6.3 scene wiring forward (owner's choice), so **the full loop is now runtime-testable** (dock → venue → switch/return). Remaining: 5.2 polish extras (label/particles/audio), Phase 6 leftovers (6.4/6.5), and Phase 7 verification. 2-client visibility (7.3) untested.
> **Audience:** Implementing agents/devs. Follow phases top-to-bottom; each `[ ]` is a discrete, checkable task.
> **Mode note:** This document is a plan. Phases 0–5 have been implemented with owner go-ahead; continue (Phase 6 leftovers / Phase 7 verify) with owner go-ahead.

---

## 0. Locked decisions (do not re-litigate)

These were confirmed by the project owner. Build to them.

| Decision | Choice |
|---|---|
| **Multiplayer model** | **One Normcore room for everything.** Each avatar carries a synced `venue` value; a player only sees/collides with avatars in the *same* venue. Friends can travel together and meet up when co-located. |
| **Where the venue is chosen** | **At the docks.** Player interacts at the dock/boat in WorldScene → prompt offers the two venues → loads straight into the chosen venue's cube. No intermediate "island hub" scene. |
| **Who travels on Switch/Return** | **Only the player who picks.** Others standing on the same pad stay put. |
| **Prompt cancel behavior** | **Stays closed until re-enter.** If a player opens the cube prompt and cancels while still on the pad, it stays closed until they step off and step back on. |
| **Walkable-area polish** | **In core** (not a later phase). The placeholder cube must become a proper-feeling walkable pad as part of the main work. |

### ⚠️ Scene-naming warnings (read carefully — easy to get wrong)
- The two travel venues are **`Demo_DanceClub_01`** (the dance club) and **`Demo_RooftopBar_01`**.
- **`Demo_NightClub_01` is NOT used.** It appeared in the original request by mistake. Do not wire it.
- `Demo_DanceClub_01` is also the scene **currently open in the editor** — be careful not to confuse editor state with build wiring.

---

## 1. Architecture context every agent must read first

- **Networking = Normcore (Normal Realtime), room-based.** `MainMultiplayerScene` permanently holds `Realtime`, `PlayerManager`, `LobyUISceneController`, and the single gameplay **Camera**. World/venue scenes are loaded **additively** on top.
- **The local avatar** is `Realtime.Instantiate`d into `MainMultiplayerScene` (owned by the local client, ownership-takeover prevented) and **persists across additive scene swaps**. Travel = swap the additive scene and **reposition the same avatar** — do NOT destroy/respawn it.
- **Spawn today:** a single static `PlayerSpawnPoint.Active`, read once in `PlayerManager.SpawnLocalPlayer()` at `BeginGame()`. `WorldSceneName = "WorldScene"` is **hardcoded** in `LobyUISceneController`. Only WorldScene currently has a spawn point.

### Existing systems to REUSE (do not reinvent)
| Need | Reuse |
|---|---|
| Trigger zone + interact button + floating prompt | `Assets/Script/PlayerScript/Interactable.cs`, `InteractButtonUI.cs`, `InteractPromptWorld.cs` |
| Local-player trigger filtering | `Interactable.IsLocalPlayer()` pattern → `PlayerController.Local` |
| Normcore synced property on the avatar | `Assets/Script/Multiplayer/PlayerAnimSync.cs` + `PlayerAnimSyncModel.cs` (`RealtimeComponent<Model>`) — copy this pattern for the venue tag |
| Disable cameras/audio on an additive scene | `DisableWorldCameras()` in `LobyUISceneController.cs` (~line 1569) |
| Input lock during transitions | `PlayerInputHub.SetPauseLocked(bool)` |
| Additive world load/unload reference | `LoadWorldAndBeginGame` / `UnloadWorldScene` in `LobyUISceneController.cs` (~lines 1587, 1578) |

### Venue map (authoritative)
| Venue | Scene path | Spawn slot on arrival |
|---|---|---|
| `World` | `Assets/Scenes/WorldScene.unity` | `CentralPlaza` (first spawn) / `Docks` (return) |
| `DanceClub` | `Assets/Download_Assets/PolygonNightclubs/Scenes/Demo_DanceClub_01.unity` | `VenueCube` |
| `RooftopBar` | `Assets/Download_Assets/PolygonNightclubs/Scenes/Demo_RooftopBar_01.unity` | `VenueCube` |

### Known in-scene objects
- WorldScene contains the dock **`marina_001`** and boat **`boat_002`** (already placed).
- `Demo_DanceClub_01` and `Demo_RooftopBar_01` each contain an object named **`Cube`** — likely the placeholder spawn markers. Inspect and reuse/replace rather than blindly adding new ones.

---

## 2. Full required flow (acceptance criteria)

1. Player enters game → spawns at **CentralPlaza** in WorldScene.
2. Player walks to the dock (`marina_001` / `boat_002`) → interacts → prompt offers **DanceClub** or **RooftopBar**.
3. Choosing a venue → load that venue additively, set it active, unload WorldScene, reposition the existing avatar onto the venue's **cube**, set `venue` tag. Player spawns **on the cube**.
4. The cube's travel prompt is **disarmed** at spawn (standing on it does nothing).
5. Player must **step off** the cube, then **step back on**, to **arm** the prompt.
6. On armed re-entry, prompt shows **two choices**: *Switch to the other venue* and *Return to main island*.
7. *Switch* → load the other venue, spawn on its cube (again disarmed until step-away).
8. *Return to main island* → load WorldScene, spawn at the **Docks** (NOT CentralPlaza), `venue = World`.
9. In the shared room, a player only sees/collides with others in their current venue; co-located players see each other.
10. If a player opens any travel prompt and cancels while still on the pad, it stays closed until they step off and back on.
11. Only the picking player travels on Switch/Return; others on the pad are unaffected.

---

## Phase 0 — Pre-flight (no code) ✅ DONE 2026-11-03
- [x] **0.1 Add venue scenes to Build Settings:** `Demo_DanceClub_01`, `Demo_RooftopBar_01` (both must be loadable by name; WorldScene already is). → Added as enabled (GameScene left disabled); saved to disk.
- [x] **0.2 Inspect `marina_001` / `boat_002`** in WorldScene: record transforms; pick the interact position and a nearby open spot for the `Docks` return spawn. → `marina_001` (233.91,15.40,364.86), `boat_002` (236.98,21.95,385.81); L-shaped pier; proposed Docks spawn ≈ (234,?,378) facing inland, Y via ground raycast. See changelog.
- [x] **0.3 Inspect the `Cube` object** in each venue scene: confirm it's a placeholder spawn marker, decide reuse-vs-replace, record its transform. → Clean root-level 1×1×1 default cubes w/ BoxCollider: DanceClub (-11.35,-14.61,14.03), RooftopBar (11.15,0.745,3.92). Decision: **reuse** as the spawn anchor.

## Phase 1 — Core data & spawn refactor ✅ DONE 2026-11-03
- [x] **1.1 `VenueId` + scene map** — `Assets/Script/Travel/VenueId.cs`: `enum VenueId { World, DanceClub, RooftopBar }` with static `SceneName(VenueId)` and `FromScene(string)`. → Done; statics live on `static class VenueMap`; `FromScene` returns `VenueId?` and accepts name-or-path.
- [x] **1.2 Multi-spawn support** — extend `PlayerSpawnPoint` (keep backward compat) or add a sibling: → Chose **extend**.
  - [x] Add a slot id: `enum SpawnSlot { CentralPlaza, Docks, VenueCube }`.
  - [x] Replace the single `Active` static with a **registry** keyed by `(VenueId, SpawnSlot)`, populated in `OnEnable`, cleared in `OnDisable`.
  - [x] Add `static PlayerSpawnPoint Find(VenueId venue, SpawnSlot slot)`.
  - [x] Keep `GetPosition(clientID)` golden-angle spread + `Rotation`.
- [x] **1.3 Preserve initial spawn** — `PlayerManager.BeginGame()` resolves `World`/`CentralPlaza` via the new registry; keep the existing fallback-warning path. → Done; backward compat verified live (existing spawn deserialized to World/CentralPlaza, spreadRadius preserved).

## Phase 2 — Venue tag sync (Normcore, one-room visibility) ✅ DONE 2026-11-03
- [x] **2.1 `PlayerVenueModel` + `PlayerVenueSync : RealtimeComponent<PlayerVenueModel>`** in `Assets/Script/Travel/`, copying the `PlayerAnimSync` / `PlayerAnimSyncModel` pattern. Synced property: `int venue` (a `VenueId`). Add to the avatar prefab(s) in `Resources` alongside `PlayerController` / `PlayerAnimSync`. → Done; both scripts created. Attached to **both** `Player.prefab` and `Player Female.prefab` and **registered in each `RealtimeView._components` as componentID 3** (verified in on-disk YAML).
- [x] **2.2 Local venue state** — static `VenueTravelManager.LocalVenue` getter + `event Action VenueChanged`. The local player's `PlayerVenueSync` writes the model when a travel completes. → Done; `VenueTravelManager` created with the Phase-2 static slice only (`LocalVenue` + `VenueChanged` + `SetLocalVenue` + `ResetStatics`). Singleton/`TravelTo` deferred to Phase 3 (same class). Local `PlayerVenueSync` mirrors its venue into `SetLocalVenue`.
- [x] **2.3 `VenueVisibilityFilter`** on each avatar: enable renderers + colliders only when `thisAvatar.venue == LocalVenue`. Re-evaluate on (a) its own model `venue` change and (b) `VenueChanged`. The **local** avatar is always visible to itself. → Done; attached to both avatar prefabs. Toggles child `Renderer`s + `Collider`s; local avatar never hidden. (Not yet two-client runtime-tested — Phase 7.3.)

## Phase 3 — Travel manager (the teleporter) ✅ DONE 2026-11-03 (wired, not yet runtime-tested)
- [x] **3.1 `VenueTravelManager`** — `Assets/Script/Travel/VenueTravelManager.cs`, singleton on a persistent object in `MainMultiplayerScene` (next to `PlayerManager`). Holds `CurrentVenue`. → Done; empty `VenueTravelManager` GameObject added to `MainMultiplayerScene` (verified on disk), deps resolved at runtime (no inspector wiring).
- [x] **3.2 `TravelTo(VenueId target, SpawnSlot slot)` coroutine:** → Done; all 9 steps implemented, re-entry guarded, load-failure reveals/unlocks (no soft-lock).
  1. Guard against re-entry while a travel is in progress.
  2. `PlayerInputHub.SetPauseLocked(true)`, hide mobile controls, **run the transition's "cover" step** (fade to black today — see 3.4). Await it before loading.
  3. Load target scene additive if not already loaded; `yield`.
  4. **Disable the target scene's cameras + AudioListeners** (reuse `DisableWorldCameras` pattern — the only live camera is in MainMultiplayerScene).
  5. `SceneManager.SetActiveScene(target)` so its lighting/skybox/reflection (and NavMesh for NPCs) apply.
  6. Unload the previous venue scene.
  7. Resolve spawn via `PlayerSpawnPoint.Find(target, slot)`; **safely teleport the existing avatar**: disable CharacterController/physics, set position+rotation (golden-angle spread for multiplayer), zero velocity, re-enable.
  8. Set local player's `venue` via `PlayerVenueSync` → fires `VenueChanged`.
  9. Keep the `CameraOrbit` target on the same avatar; **run the transition's "reveal" step** (fade in); unlock input; restore mobile controls.
- [x] **3.3 Transition abstraction (fade-to-black now, loading screen later) — REQUIRED.** → Done; `ISceneTransition` seam (`Cover`/`Reveal`) + `FadeToBlackTransition` (runtime-built full-screen overlay). `TravelTo` only calls `Cover()`/`Reveal()`; swap point is `_transitionComponent` / the `Transition` accessor. A loading scene is planned for a later milestone, so the fade must NOT be hard-coded inline in `VenueTravelManager`. Build it behind a small seam so the loading screen can drop in with no change to `TravelTo`:
  - Define an interface, e.g. `ISceneTransition { IEnumerator Cover(); IEnumerator Reveal(); }` (or an async equivalent). `TravelTo` only ever calls `Cover()` before unload/load and `Reveal()` after the avatar is placed — it never touches a `CanvasGroup` or fade timing directly.
  - Ship a `FadeToBlackTransition : ISceneTransition` implementation now (full-screen `CanvasGroup` on the persistent MainMultiplayerScene UI, sits above all gameplay/venue UI, starts transparent & non-blocking).
  - Resolve the active transition via a single accessor (serialized field or a `VenueTravelManager.Transition` property defaulting to the fade) so the future `LoadingScreenTransition` can replace it in one place.
  - Keep `Cover`/`Reveal` able to **hold black across the full load/unload** (not a fixed-duration tween that ends early), and let `Reveal` optionally wait on a "ready" signal — the loading screen will need that hook to stay up until the target scene is ready.
- [x] **3.4 Integrate with `LobyUISceneController`** — set `CurrentVenue = World` after the existing first-spawn load, register the already-loaded WorldScene with the manager, and route all subsequent hops through `VenueTravelManager`. Keep churn minimal; pause-menu "Leave Room" → lobby stays unchanged. → Done; one call `VenueTravelManager.Instance?.AdoptInitialVenue(VenueId.World)` after `BeginGame()`. Subsequent hops routed via `TravelTo` (wired from Phase 4/5 callers). "Leave Room" untouched.

## Phase 4 — Dock → destination choice (at the docks) ✅ DONE 2026-11-03 (runtime-testable)
- [x] **4.1 `TravelChoiceUI`** — reusable choice panel (title + 2–3 buttons), styled like the lobby/dialogue panels. API: `Open(string title, (string label, Action onPick)[] options)` / `Close()`. (The existing `InteractButtonUI` is single-verb only, so a dedicated multi-choice panel is needed.) Likely lives on the persistent MainMultiplayerScene UI. → Done; procedural runtime-built panel (+ optional `onCancel`, + "Batal"). `TravelChoiceUI` GameObject added to `MainMultiplayerScene`.
- [x] **4.2 `DockTravelPoint`** on `marina_001` / `boat_002`: an `Interactable` (trigger + on-screen Interact button). `onInteract` → `TravelChoiceUI.Open(...)`. → Done; script + placed in WorldScene as a trigger zone on the pier beside the boat (BoxCollider trigger + Interactable w/ new `Travel`/"Berlayar" action + `InteractPrompt` prefab). Title "Berlayar ke pulau seberang" → Dance Club / Rooftop Bar.

## Phase 5 — Venue cube: spawn pad + switch zone + re-arm rule ✅ DONE 2026-11-03 (5.2 basic)
- [x] **5.1 `TravelPad`** — `Assets/Script/Travel/TravelPad.cs` on the cube in each venue (reuse/replace the existing `Cube`). Serialized: `VenueId thisVenue`. → Done; placed on both venue cubes (DanceClub + RooftopBar) with VenueCube `PlayerSpawnPoint` + trigger. Disarm-on-arrival via `OnEnable` (scene reloads per arrival); exit arms; cancel→`_consumedUntilExit`; local-only. Dynamic Switch/Return options.
  - Trigger volume = the walkable pad. Also hosts the `VenueCube` spawn marker (attach `PlayerSpawnPoint` with slot `VenueCube`).
  - On every travel-arrival it starts **disarmed** (`_armed = false`).
  - Local-player `OnTriggerExit` → `_armed = true`.
  - Local-player `OnTriggerEnter` while `_armed` → open `TravelChoiceUI` with **dynamic options**:
    - DanceClub pad → *Switch to Rooftop Bar* `TravelTo(RooftopBar, VenueCube)` + *Return to main island* `TravelTo(World, Docks)`.
    - RooftopBar pad → *Switch to Dance Club* `TravelTo(DanceClub, VenueCube)` + *Return to main island* `TravelTo(World, Docks)`.
  - Use the `PlayerController.Local` check so remote avatars never trigger it.
  - **Cancel rule:** cancelling the panel sets a `_consumedUntilExit = true` flag; the prompt will not reopen until an `OnTriggerExit` clears it (step off + back on).
  - Only the local picker travels (per locked decision) — the pad never force-moves other players.
- [x] **5.2 Walkable-area polish (required):** turn the raw cube into a proper **ground pad** — flat collider pad + emissive/glow ring material or projected decal, a subtle upward light beam or particles, and a floating `InteractPromptWorld`-style label ("Travel"). Add on-enter visual/audio feedback. The zone must read as an obviously walkable area, not a floating box. → **Done:** glowing emissive disc (`VenuePadGlow.mat`) + cyan point light + a floating billboarded "Berlayar" label (TMP 3D + new `Billboard.cs`) + a thin 2m emissive beam + on-enter light pulse (`TravelPad.PulseLight`). Audio feedback not added (no clip) — optional later.

## Phase 6 — Scene wiring
- [~] **6.1 WorldScene:** keep the `CentralPlaza` spawn; add a `Docks` spawn point beside `marina_001`; add `DockTravelPoint` to `marina_001` / `boat_002`. → **Mostly done in Phase 4** (owner pulled it forward): `Docks SpawnPoint` at (230,22.298,380) facing inland + `DockTravelPoint` trigger zone on the pier beside the boat are placed & saved in WorldScene; CentralPlaza spawn unchanged. Remaining: optional placement polish only.
- [x] **6.2 `Demo_DanceClub_01`:** put `TravelPad(thisVenue = DanceClub)` + `VenueCube` spawn on its `Cube`; ensure a Camera + Directional Light exist (per Unity-MCP scene rules) but the camera stays disabled at runtime (manager handles it). → Done in Phase 5 (TravelPad + VenueCube spawn on the ex-`Cube`). The demo scene already has a Main Camera (disabled at runtime by `TravelTo`) + URP lighting.
- [x] **6.3 `Demo_RooftopBar_01`:** same with `thisVenue = RooftopBar`. → Done in Phase 5.
- [ ] **6.4 Avatar prefab(s) in `Resources`:** add `PlayerVenueSync` + `VenueVisibilityFilter`; verify they still carry `RealtimeView` / `RealtimeTransform` / `PlayerController` / `PlayerAnimSync`.
- [ ] **6.5 Persistent UI:** ensure one `TravelChoiceUI` (+ the existing `InteractButtonUI`) is reachable from every venue — put it on MainMultiplayerScene UI, not per-venue.

## Phase 7 — Verify (via Unity MCP, with owner's go-ahead)
- [ ] **7.1** Compile clean: `read_console` after each script create/edit; wait for domain reload (`editor/state → compilation.is_compiling == false`).
- [ ] **7.2** Single client: CentralPlaza → dock choice → DanceClub cube (disarmed) → step off/on → switch to Rooftop → step off/on → return → **spawns at Docks, not CentralPlaza**.
- [ ] **7.3** Two clients: venue visibility filter (see each other only when co-located), no ghost avatars, no pad overlap, only-picker-travels holds.
- [ ] **7.4** Edge cases: spam-interact during fade; cancel-then-stay-on-pad stays closed until exit; leaving room mid-venue returns to lobby cleanly; per-venue lighting/skybox correct; teleport doesn't fling the CharacterController.

---

## New files (concrete targets)
`Assets/Script/Travel/`
- `VenueId.cs`
- `VenueTravelManager.cs`
- `PlayerVenueSync.cs`
- `PlayerVenueModel.cs`
- `VenueVisibilityFilter.cs`
- `TravelPad.cs`
- `TravelChoiceUI.cs`
- `DockTravelPoint.cs`
- `ISceneTransition.cs` + `FadeToBlackTransition.cs` (the swappable fade; a `LoadingScreenTransition` drops in here later)

Edits to: `PlayerSpawnPoint.cs`, `PlayerManager.cs`, `LobyUISceneController.cs`, and the avatar prefab(s) in `Resources`.

---

## Residual design notes
- **Screen fade** during transitions prevents a one-frame flash of the wrong scene/position. **Build it behind the `ISceneTransition` seam (Phase 3.3), not inline** — a dedicated loading scene is planned for a later milestone, and it must be able to replace the fade with zero changes to `VenueTravelManager.TravelTo`. The fade implementation must be able to hold black across the entire load/unload and reveal only on a "ready" signal, so the loading screen can reuse the same `Cover()`/`Reveal()` contract. Put the full-screen `CanvasGroup` on the persistent MainMultiplayerScene UI, above all gameplay/venue UI.
- **Spawn spread:** keep the golden-angle spread on the venue cube so multiple co-located players don't stack on the pad.
- **NavMesh/NPCs:** if a venue has NPCs, setting the venue scene active (step 3.2.5) is required for its NavMesh to be the active one.
