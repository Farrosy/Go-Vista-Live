# NPC Pathing / Pedestrian-Crowd System — Implementation Plan

> **Status:** **Phases 0–3 IMPLEMENTED** (NavMesh confinement baked; capsule agent + pathing + avoidance; **Phase 3 re-scoped 2026-11-09 to a fully dynamic, natural-wander crowd** — authored destination markers REMOVED per owner directive). All single-client / un-networked; WorldScene saved. **Phase 4 (networking) next.** Do not touch scenes/scripts for later phases until the owner gives go-ahead per phase.
> **2026-11-09 owner directive (current):** NPCs move **entirely on their own, dynamically** — no authored "pointers"; natural pedestrian feel; give way to each other; **crowd the city**. Scoped this pass to: **single-client** (Phase 4 still deferred), **~100+ packed** (`NpcDevSpawner.count = 120`), **per-island** (crosswalks stay deferred). **Movement FINALISED 2026-11-09d = standard NavMeshAgent + built-in RVO avoidance** (after a custom separation-steering detour that deadlocked and would foot-slide under animation — reverted). This is simpler, robust (RVO doesn't permanently gridlock — stress-verified), and **animation-ready** (agent faces its velocity; `Speed` param → blend tree). Edge-clinging left for the placeholder. See the 2026-11-09 (d) changelog entry; (a/b/c) document the superseded custom path + its lessons.
> **Progress log:** see [`npc-changelogs.md`](./npc-changelogs.md) for the per-phase record, and [`npc-phase0-1-results.md`](../Assets/Screenshots/npc-phase0-1-results.md) + screenshots in `Assets/Screenshots/` for the as-built confinement recipe, agent type, and verification images.
> **Audience:** Implementing agents/devs. Follow phases top-to-bottom; each `[ ]` is a discrete, checkable task.
> **Mode note:** This is a design/plan document. Build it phase-by-phase with owner go-ahead, same cadence as [`venue-travel-plan.md`](./venue-travel-plan.md).

---

## 0. Locked decisions (do not re-litigate)

Confirmed by the project owner on 2026-11-05. Build to them.

| Decision | Choice |
|---|---|
| **NPC visual** | **Prototype capsule.** A plain capsule (mesh + `CapsuleCollider`) stands in for the real character art. **The pathing/avoidance/networking logic is the deliverable** — the art is swapped in later with zero logic changes. |
| **Where NPCs may walk** | **Sidewalks and the park ONLY.** Nowhere else — not roads, plazas, parking, building interiors, water. Enforced by confining the walkable NavMesh (not by per-frame checks). |
| **Mutual avoidance** | NPCs **must not pile into each other**. When two get too close they **go around**, they do not clip through or freeze. (Delivered by NavMesh local avoidance — see §1.) |
| **Multiplayer model** | **Networked / shared.** Every player sees the same NPCs in the same spots. One client is the **authority** that simulates the agents; everyone else receives synced transforms. Ties into the existing one-room Normcore model and the venue system. |
| **Walk behavior** | **Fully dynamic wander (2026-11-09 revision).** Every NPC roams the walkable mesh **entirely on its own** — no authored destinations/markers, no waypoints. Motion is procedural and made to feel natural: purposeful heading-biased roaming, per-NPC pace, easing slow-down before obstacles ahead and before the mesh edge, and pedestrian-style give-way via NavMesh avoidance. *(Superseded the original "mix of wander + authored destinations"; the `PedestrianDestination` marker system was deleted.)* |
| **Walkable-area confinement** | **Specify a clean, explicit confinement from scratch.** Do not trust the current partial NavMesh bake — define a repeatable recipe (which meshes are walkable, which agent type, bake settings) and re-bake to it. |
| **Crosswalks** | **Deferred to a later phase.** Physically easy; the blocker is conflict with the uncoordinated car traffic and the strict "sidewalks + park only" rule. Documented as a future phase (Appendix A), **not implemented now.** |

### ⚠️ Warnings (read carefully — easy to get wrong)
- **Two `WorldScene.unity` files exist:** `Assets/Scenes/WorldScene.unity` (canonical — holds the baked `NavMeshSurface`, referenced by the venue system) and `Assets/Scenes/DevScene/WorldScene.unity` (dev copy). **All NPC work targets `Assets/Scenes/WorldScene.unity`.**
- **WorldScene is loaded additively and *unloaded* when a client travels into a venue** (see venue plan). So a client inside DanceClub/RooftopBar has **no World NavMesh loaded** and **cannot** simulate the pedestrians. The authority must be a client currently *in the World venue* (§4).
- **The car traffic system is separate and uncoordinated.** Cars run on spline `RoadSection` → `TrafficLaneGraph` → `TrafficCar`; there are **no traffic lights and no shared pedestrian signal**, and cars are not `NavMeshObstacle`s. NPCs and cars do not see each other. This is exactly why crosswalks are deferred.
- **Existing `NPC_Kota.prefab`** is a manually-placed *dialogue* NPC (CapsuleCollider + Animator + a placeholder `NewMonoBehaviourScript`). It has **no** `NavMeshAgent`/`RealtimeView` and is **not** a pathing NPC — do not confuse it with the crowd agents built here.

---

## 1. Architecture context every agent must read first

### What already exists (REUSE — do not reinvent)
| Need | Reuse |
|---|---|
| Pathfinding + **local avoidance** (go-around) | **Unity AI Navigation `2.0.16`** is installed (`com.unity.ai.navigation`). `NavMeshAgent` does RVO local avoidance for free. |
| Baked walkable surface | `Assets/Scenes/WorldScene.unity` already has **one `NavMeshSurface`** (`Collect Objects = All`, `Use Geometry = Render Meshes`, baked `NavMeshData` asset) **+ ~29 `NavMeshModifier`s**, some already set `Area = 1 (Not Walkable)`. We will **redefine** this cleanly (§2 / Phase 1), reusing the infra. |
| Agent wander/waypoint walker | `Assets/Script/NPC/NpcNavigation.cs` — already does RandomWander + Waypoints (Loop/PingPong/Random), NavMesh re-sampling, `Speed` animator param, dialogue stop/resume. Adapt, don't rewrite. |
| NPC facing / talk anim | `Assets/Script/PlayerScript/DialougeScript/NpcController.cs` + `DialogueTrigger.cs`. |
| Normcore synced property on a networked object | `RealtimeComponent<TModel>` + `[RealtimeModel]`/`[RealtimeProperty]` pattern — see `PlayerAnimSync(Model).cs` and `Travel/PlayerVenueSync.cs` + `PlayerVenueModel.cs`. |
| Transform sync over the network | `RealtimeTransform` (already on the avatar prefabs). |
| Networked instantiate + ownership | `Realtime.Instantiate(prefabName, pos, rot, InstantiateOptions{…})` — see `PlayerManager.SpawnLocalPlayer()`. |
| Per-venue visibility (hide World NPCs from players inside venues) | `Travel/VenueVisibilityFilter.cs` + `VenueTravelManager.LocalVenue` / `VenueChanged`. |
| "Local avatar" identity check | `PlayerController.Local`. |

### How the four hard requirements are satisfied
- **"Only sidewalks + park":** confine the *walkable* NavMesh to exactly those meshes. An agent physically cannot leave the mesh, so no per-frame rule is needed. (Phase 1.)
- **"Don't collide / go around each other":** `NavMeshAgent`'s built-in obstacle avoidance (set `obstacleAvoidanceType`, per-NPC `avoidancePriority`, correct `radius`). Because only the **authority** runs the agents, avoidance is computed **once on one machine** and the result is synced — no cross-client disagreement. (Phase 2 tunes it.)
- **Networked / shared:** authority-simulated agents + `RealtimeTransform`; a small networked "director" owns the roster. (Phase 4.)
- **Mix of wander + destinations:** reuse `NpcNavigation` modes, add authored destination markers. (Phase 3.)

### Authority / ownership model (the core multiplayer idea)
- NPCs are `Realtime.Instantiate`d **shared** objects (`preventOwnershipTakeover = false`, `destroyWhenOwnerOrLastClientLeaves` tuned so they survive host migration — exact flags verified in Phase 4).
- Exactly **one client is the NPC authority**: the client with the **lowest `clientID` among clients currently in the World venue**. Only it:
  - owns the NPC `RealtimeView`s,
  - runs the `NavMeshAgent` + destination/wander logic + avoidance.
- **Every other client** (including World clients that aren't the authority) **disables the `NavMeshAgent`** on each NPC and lets `RealtimeTransform` drive the transform; it derives the animation `Speed` locally from observed velocity.
- **Authority migration:** when the authority disconnects or leaves World, the new lowest-clientID World client requests ownership of all NPCs, enables agents, and resumes simulation. NPCs hold their last synced pose in the gap.
- **Nobody in World:** no authority → simulation pauses (NPCs freeze in place). Nobody can see them anyway (they're World-venue only). Simulation resumes when a client returns to World.

---

## 2. Full required flow (acceptance criteria)

1. In `WorldScene`, a crowd of **N** capsule NPCs walks **only on sidewalks and in the park** — never on roads, plazas, parking, water, or inside buildings.
2. When two NPCs approach each other, they **steer around** and continue; they never clip through each other and never get permanently stuck.
3. NPCs move **fully dynamically** — every one roams the walkable mesh on its own, with natural pacing: it eases off before an obstacle ahead and before the mesh edge (no marching to the brink / hard stop), and gives way to other pedestrians. *(No authored destinations — revised 2026-11-09.)*
4. In the shared room, **all players see the same NPCs at the same positions** (within interpolation). No per-client ghost crowds, no duplicated rosters.
5. One **authority** client simulates; others follow synced transforms. Authority **migrates** cleanly when the current authority leaves, with no duplication and no permanent freeze (while ≥1 client is in World).
6. Players **inside a venue** (DanceClub/RooftopBar) do **not** see or collide with World NPCs; returning to World, they see the crowd again.
7. NPCs never interfere with player avatars pathologically (they may avoid the player, but must not shove/trap them). Player avatars are **not** NavMeshAgents — decide how agents treat them (Phase 2/5).
8. Performance stays acceptable on the target device at the chosen N (Phase 5 sets N + avoidance quality budget).
9. **Crosswalks are NOT crossed** in this version — NPCs stay on their sidewalk/park "islands." (Appendix A is the future upgrade.)

---

## Phase 0 — Pre-flight ✅ DONE (2026-11-05, live via Unity MCP)
- [x] **0.1 Identify the walkable meshes.** No object is literally named "sidewalk." The ground is a grid of **15×15 m modular tiles** (ithappy Megacity kit), all flat at y≈23.2. Owner-confirmed classification:
  - **SIDEWALK = `building_platform_*`** — variants 002(2), 003(15), 004(8), 005(26), 009(3), 012(4), 013(2) = **60 tiles**. Pavement bordering roads & building bases.
  - **PARK = `grass_001`** (17 tiles, lawn) **+ `park_road_001`** (1) **+ `park_road_003`** (4) = **22 tiles**.
  - **Walkable total = 82 tiles.**
  - **NOT walkable:** `Road_009/010/011/012/013/019` (135 road tiles), `asphalt_001` (40 — owner ruled these plazas/lots, EXCLUDED), `parking_*`/`disabled_parking_*` (8), `water` (1), `Terrain*`, all buildings/props.
  - Tiles are flat individual GameObjects (many at scene root, some nested under groups like `benche`, `set`, `g`), **no clean sidewalk/park parent** → drove the Phase-1 recipe choice (per-tile modifiers, not parent grouping).
- [x] **0.2 Catalog the current NavMesh setup.** Was: **1 agent type** `Humanoid` (id 0, r0.5/h2/climb0.75/slope45). **1 `NavMeshSurface`** ("NavMesh Surface") `collectObjects=All, useGeometry=RenderMeshes, defaultArea=0 (Walkable), layerMask=Everything`, data `Assets/Scenes/WorldScene/NavMesh-NavMesh Surface 4.asset`. **28 `NavMeshModifier`s** — ALL `overrideArea→1 (NotWalkable), applyToChildren`, on props only (benches/trees/trash/fountains/playground/monument). Old approach = "all walkable, carve props" (opposite of goal). These 28 are now **redundant** (default is NotWalkable) but left in place (harmless, consistent).
- [x] **0.3 Capsule agent dimensions.** Owner-confirmed: **radius 0.3 m, height 1.8 m, step (climb) 0.4, max slope 45°.** New agent type **`Pedestrian`** created (see 1.1). (Existing `Humanoid` 0.5/2.0 left untouched.)
- [x] **0.4 Normcore flags** (reflected from `Normal.Realtime.Realtime+InstantiateOptions`; de-risks Phase 4). Fields are: `ownedByClient`, `preventOwnershipTakeover`, `destroyWhenOwnerLeaves`, `destroyWhenLastClientLeaves`, `useInstance`, `defaults`. Existing avatar pattern (`PlayerManager.cs:100`) = `ownedByClient=true, preventOwnershipTakeover=true` (owned, **non**-migratable). **NPCs need the opposite for authority migration:** `ownedByClient=true` (authority owns), **`preventOwnershipTakeover=false`** (next authority can `RequestOwnership()`), **`destroyWhenOwnerLeaves=false`** (NPC survives when authority disconnects/leaves World), `destroyWhenLastClientLeaves=true` (clean up roster when room empties), `useInstance=realtime`. *(Note: there is no single `destroyWhenOwnerOrLastClientLeaves` field — it's two separate flags.)* Verify behaviorally in Phase 4.
- [x] **0.5 N + spawn strategy (DRAFT, finalized Phase 5):** target **N ≈ 20–30** capsules for the prototype (small city; authority simulates all on one machine). Spawn at **random walkable NavMesh samples** (`NavMesh.SamplePosition` on jittered points within the walkable bounds `x[61.8..269.3] z[86.8..281.1]`), golden-angle spread like `PlayerSpawnPoint.GetPosition`, snapped to the Pedestrian navmesh; authored destinations added in Phase 3.

## Phase 1 — Walkable confinement ✅ DONE & BAKED (2026-11-05)
- [x] **1.1 Pedestrian Agent Type created.** Name **`Pedestrian`**, `agentTypeID = -1372625422`, radius 0.3, height 1.8, climb/step 0.4, slope 45, cell size = auto. Added to project NavMesh settings (`NavMeshProjectSettings` singleton → `m_Settings` / `m_SettingNames`). `Humanoid` (id 0) untouched.
- [x] **1.2 Confinement recipe (as built).** Chose the **area-override whitelist** (NOT parent-grouping or a layer) because the tiles are flat/individual with no clean sidewalk/park parent, and reparenting 82 scenery objects in the shared multiplayer scene risks breaking path references:
  - `NavMeshSurface` set to: **`agentTypeID = Pedestrian`**, `collectObjects = All`, `useGeometry = RenderMeshes`, **`defaultArea = 1 (Not Walkable)`**, `layerMask = Everything`, `minRegionArea = 2`, `ignoreNavMeshAgent/Obstacle = true`.
  - Added **`NavMeshModifier (overrideArea=true, area=0 Walkable, applyToChildren=true)` to each of the 82 walkable tiles** (matched by name prefix `building_platform` / `grass_001` / `park_road`).
  - Result: with `defaultArea = NotWalkable`, roads/asphalt/parking/water/buildings contribute geometry but bake as NotWalkable → **excluded**; only whitelisted tiles bake walkable; props still carve (default NotWalkable). The 28 old prop modifiers kept (now redundant, harmless).
- [x] **1.3 Baked** via `Unity.AI.Navigation.Editor.NavMeshAssetManager.instance.StartBakingSurfaces()` (same as the Bake button; persists the asset). Result: **4324 verts / 1932 tris, 100% area 0 (Walkable)**, bounds `x[61.8..269.3] y≈23.2 z[86.8..281.1]`. Asset: `Assets/Scenes/WorldScene/NavMesh-NavMesh Surface 4.asset`. Scene saved.
- [x] **1.4 Confinement verified.** Programmatic `NavMesh.SamplePosition` at every tile center (1.5 m radius): **ROAD 0/135, ASPHALT 0/40, PARKING 0/8, WATER 0/1 → ZERO non-walk violations.** WALK coverage 76/82 (the 6 misses are tiles whose *center* is under a structure — fountain, skyscraper, monument, supermarket, playground, ice-cream shop — surrounding pavement still bakes; correct). Visual: `Assets/Screenshots/npc_phase1_navmesh_topdown.png` shows blue navmesh only on sidewalks+park; islands correctly disconnected by roads (crosswalks deferred).
- [x] **1.5 Recipe recorded** above + in [`Assets/Screenshots/npc-phase0-1-results.md`](../Assets/Screenshots/npc-phase0-1-results.md). **Repeatable after scene edits:** re-run the tile-marking (any new `building_platform*`/`grass_001*`/`park_road*` tile needs the Walkable modifier) then re-bake the surface for the `Pedestrian` agent type.

## Phase 2 — Prototype capsule agent + pathing + avoidance (single-client first) ✅ DONE (2026-11-05)
> Build and tune the movement/avoidance **locally, un-networked** first, so the hard part is proven before Normcore is added.
- [x] **2.1 `NpcAgent.prefab`** (capsule): capsule `MeshRenderer` + `CapsuleCollider` + `NavMeshAgent` (Pedestrian, id `-1372625422`, r0.3/h1.8, `baseOffset=0.9` so the capsule sits on the mesh, `GoodQualityObstacleAvoidance`) + `NpcAgent.cs`. Capsule scaled `(0.6, 0.9, 0.6)` → world r0.3/h1.8. **Saved to `Assets/Resources/NpcAgent.prefab`** (Resources-reachable so Phase-4 `Realtime.Instantiate("NpcAgent")` resolves it, like the avatar prefabs — the plan's earlier "Assets/Prefab/" note is superseded by the Resources requirement).
- [x] **2.2 Movement script `Assets/Script/NPC/NpcAgent.cs`.** Self-contained adaptation of `NpcNavigation.cs` (kept NavMesh re-sampling, arrival/wait + random-dwell desync, wander-around-home, `Speed` animator wiring as a no-op on the capsule; dropped the DialogueTrigger/NpcController coupling since the prototype has none). Wander destinations validated with `CalculatePath` → `PathComplete` so NPCs never target a disconnected island.
- [x] **2.3 Mutual avoidance tuning (REQUIRED):** `obstacleAvoidanceType = GoodQualityObstacleAvoidance`, `radius 0.3`; **per-NPC `avoidancePriority` randomized 20–80 in `Awake`** (verified spread: 25/28/35/41/45/50/68/73/77/78 across 10 agents). Verified live: 10 agents spawned in a ~4 m cluster weave apart, min pairwise distance **3.74 m** (no clipping), all moved (avg 9.9 m over ~9 s) — **no deadlock**.
- [x] **2.4 Player-vs-NPC policy — chose (a) carving `NavMeshObstacle`.** Added a capsule `NavMeshObstacle` (`center (0,0.49,0)`, r0.5/h1.73 ≈ CharacterController footprint, `carving=true`, `carveOnlyStationary=false`, moveThreshold 0.25, timeToStationary 0.3) to **both** `Assets/Resources/Player.prefab` and `Player Female.prefab` so NPCs flow around a moving player. Additive component — does not disturb `RealtimeView._components` indices. Perf of dynamic carving to be confirmed in Phase 5.
- [x] **2.5 Edge case:** `NpcAgent` snaps back onto the mesh via `NavMesh.SamplePosition` both in `Start` **and** every `Update` (if `!agent.isOnNavMesh`, re-sample within `resampleRadius=3` and `Warp`).

**Verification (single client, Phase 6.2 subset):** 10/10 on-mesh, 10/10 on walkable area 0, **0** bad-area / **0** out-of-bounds (strict sidewalk+park confinement held), 0 compile errors, 0 runtime errors. Screenshot: `Assets/Screenshots/npc_phase2_crowd_topdown.png`. Test instances were play-mode only → auto-removed on Stop; `WorldScene.unity` left unmodified/not dirty.

## Phase 3 — Behavior: fully dynamic natural wander ✅ DONE (re-scoped 2026-11-09)
> Single-client / un-networked (networking is Phase 4). **Supersedes the original "wander + authored
> destinations" Phase 3** (implemented 2026-11-05, then removed): the owner directed NPCs to move entirely on
> their own with no pointers. See the 2026-11-09 changelog entry for full as-built detail, the deadlock-fix
> lesson, verification numbers, and the complete tuning-knob list.
- [x] **3.0 Removed the authored-destination system.** Deleted `Assets/Script/NPC/PedestrianDestination.cs`
  (+`.meta`) and the `PedestrianDestinations` root (12 markers) from `WorldScene`. `NpcAgent` dropped its
  `Destinations`/`Waypoints` modes, `destinationShare`, and the marker registry — it is now **wander-only**.
- [x] **3.1 `NpcAgent.cs` rewritten for natural motion** (same file/prefab/Pedestrian agent type; the art-swap
  and Phase-4 authority hooks are preserved):
  - **Natural roaming** — next target picked *ahead* within a heading cone (`headingConeDeg`/`wideTurnChance`)
    for purposeful walking; `minStepDistance…roamRadius`; **targets held ≥`targetEdgeMargin` off the mesh edge**
    (`NavMesh.FindClosestEdge`) so NPCs aim for open pavement; per-NPC speed `speedRange` (0.9–1.6); `autoBraking`
    + moderate `acceleration` for soft starts/stops; varied dwell (`pauseChance`/`pauseTime`/`strollPause`).
  - **Avoidance = Unity built-in RVO (FINAL, reverted 2026-11-09d).** The agent **moves itself** — `SetDestination`
    + built-in path-following + `obstacleAvoidance = GoodQualityObstacleAvoidance` + per-NPC `avoidancePriority`
    20–80. **No `agent.velocity` override, no custom separation/feeler/edge-push.** Plus `updateRotation = true` so
    the NPC faces its travel direction (animation-ready) and a light **stuck-net** (`stuckResetTime` 2 s re-pick) as
    a backstop. *Why:* the earlier custom separation steering (sub-bullets removed) was not reciprocal, still had
    rare emergent deadlocks, and its lateral veer would foot-slide under a walk animation. RVO is reciprocal (no
    permanent gridlock) and the agent facing its velocity is what makes a forward walk clip match. **Trade-off
    accepted (owner):** NPCs may hug the mesh edge a bit more; left for the placeholder. Stress-verified: 24 agents
    from total overlap forced to cross all untangle (0 stalled); full crowd 0 stalled, 119/120 roamed >1.5 m.
    → **Animation when real art lands:** Animator `Speed` float (already fed `agent.velocity.magnitude`) → idle/
    walk/run blend tree; root motion OFF (agent drives position); match clip speed to `agent.speed`/`speedRange`.
    (The three 2026-11-09 a/b/c changelog entries document the superseded custom-steering path + its gridlock
    lessons; the shipped movement is the standard RVO agent.)
- [x] **3.2 Packed crowd via `NpcDevSpawner`** — default `count` 20→**120** (+`minSpacing 1.2 m`); the scene
  instance's `count` was set to 120. Random bound-box samples land ~proportional to island area → whole
  populated city busy. Still self-guards (editor-only; stands down under Normcore `Realtime`). **Scene saved.**
- [x] **3.3 Live-verified** (120 agents): 120/120 on walkable area 0 / 0 bad-area (strict confinement);
  108 moving / 0 stalled, 119/120 displaced >1.5 m (avg 18.5 m); only 2 transient pairs <0.6 m (no clipping);
  0 stray agents after Stop. Screenshots `Assets/Screenshots/npc_dynamic_crowd_topdown.png` + `..._zoom.png`.

## Phase 4 — Networking (shared NPCs, authority + migration)
> **📋 HANDOFF READY (2026-11-09):** a full, self-contained implementation brief for this phase —
> grounded in the actual Normcore code it mirrors (PlayerAnimSync, PlayerManager, PlayerVenueSync,
> VenueTravelManager, VenueVisibilityFilter), with exact InstantiateOptions, the three must-know gotchas
> (persist NPCs in MainMultiplayerScene; NavMeshAgent-vs-RealtimeTransform gating; model-ready guards),
> ordered tasks, a two-client test matrix, and open questions (esp. networked **N** / bandwidth) — lives in
> **[`npc-phase4-networking-handoff.md`](./npc-phase4-networking-handoff.md)**. The checklist below is the summary;
> the handoff doc is the thing to execute.
- [ ] **4.1 `NpcSync : RealtimeComponent<NpcSyncModel>`** on the NPC prefab, next to `RealtimeView` + `RealtimeTransform` (register in `RealtimeView._components`, exactly like `PlayerAnimSync`/`PlayerVenueSync` are). Add a synced `Speed` (unreliable float) so remotes animate; keep heavy state local to the authority.
- [ ] **4.2 Authority gating in the movement script.** On each NPC: enable `NavMeshAgent` + wander/destination logic **only** when this client owns it (`isOwnedLocallyInHierarchy`) **and** `VenueTravelManager.LocalVenue == World`. Otherwise disable the agent and let `RealtimeTransform` drive the transform; derive `Speed` from observed velocity for animation.
- [ ] **4.3 `NpcDirector`** — a networked singleton (RealtimeComponent on a persistent object in `MainMultiplayerScene`, alongside `PlayerManager`/`VenueTravelManager`). Responsibilities:
  - Compute **who is authority** = lowest `clientID` among clients currently in World venue. (Needs a lightweight synced presence of "which clients are in World" — reuse the venue tag the players already sync, or a small director-owned set.)
  - On **this** client becoming authority: if the roster isn't instantiated yet, `Realtime.Instantiate` the N NPCs (shared, migratable flags from 0.4) at Phase-0.5 spawn points; else `RequestOwnership()` of the existing NPCs and resume their agents.
  - On losing authority / leaving World: stop simulating (ownership will move to the next authority).
  - Guard against double-spawn (only the elected authority ever instantiates; use a synced "roster spawned" flag/count).
- [ ] **4.4 Venue visibility.** Give each NPC the World venue so players inside DanceClub/RooftopBar don't see/collide with them. Reuse `VenueVisibilityFilter` (or its pattern) keyed to `VenueId.World` vs `VenueTravelManager.LocalVenue`.
- [ ] **4.5 Migration test hooks:** ensure NPCs persist (don't despawn) when the authority leaves, and resume within a frame or two under the new authority, with no duplicate roster.

## Phase 5 — Integration & polish
- [ ] **5.1 Finalize N + density** for the target device; expose as a `NpcDirector` tunable. Pick `obstacleAvoidanceType` quality vs. perf (agents only simulate on one machine, so cost is on the authority — watch the weakest likely-authority device).
- [ ] **5.2 Spawn placement:** NPCs start on valid sidewalk/park samples (not stacked, not on seams); use a golden-angle / jittered spread like `PlayerSpawnPoint.GetPosition`.
- [ ] **5.3 Animation hook:** capsule has none now — leave the `Speed`/`IsTalking` param wiring in place (no-op on the capsule) so the real character art drops in without code changes. Optional: a tiny idle/bob so the prototype reads as alive.
- [ ] **5.4 Venue load/unload resilience:** confirm nothing throws when World unloads under the authority mid-travel (agents disabled, ownership releases cleanly); confirm resume on return.
- [ ] **5.5 Interop with cars:** since NPCs stay on sidewalk/park islands and never enter roads, confirm there is **no** contact with the car system in this version (the only contact point is crosswalks → Appendix A).

## Phase 6 — Verify (Unity MCP, owner go-ahead)
- [ ] **6.1** Compile clean after each script create/edit (`read_console`; wait for `compilation.is_compiling == false`).
- [ ] **6.2 Single client:** crowd wanders sidewalks + park only; never on roads/plaza/water/buildings; some NPCs visit destinations; agents weave around each other and the player; none stuck.
- [ ] **6.3 Two clients:** both see identical NPCs/positions; no ghost/duplicate crowd; authority migration on host leave resumes cleanly; NPCs hidden while a client is in a venue and visible again on return.
- [ ] **6.4 Edge cases:** no client in World (NPCs freeze, resume on return); authority spam join/leave; NPC displaced off-mesh snaps back; perf at target N holds.

---

## New files (concrete targets)
`Assets/Script/NPC/`
- `NpcAgent.cs` — **dynamic wander-only** capsule movement: natural roaming + obstacle/edge anticipation +
  avoidance config (authority-gating hooks for Phase 4 preserved). *(No longer wraps `NpcNavigation`.)*
- ~~`PedestrianDestination.cs` — destination marker + registry.~~ **Deleted 2026-11-09** (markers removed;
  NPCs are fully dynamic). `NpcDevSpawner.cs` is the single-client crowd populator until Phase 4.
- `NpcDirector.cs` — networked roster/authority/migration controller.
- `NpcSync.cs` + `NpcSyncModel.cs` — `RealtimeComponent` + `[RealtimeModel]` (synced `Speed`, etc.).
- (maybe) `NpcVenueVisibility.cs` — thin reuse/alias of `VenueVisibilityFilter` keyed to World.

Assets: `Assets/Prefab/NpcAgent.prefab` (capsule) in a Resources-reachable path (so `Realtime.Instantiate` can find it by name, like the avatar prefabs).

Edits to: `Assets/Scenes/WorldScene.unity` (NavMesh re-bake + destination markers + NPC spawn points), `MainMultiplayerScene` (add `NpcDirector`), and the Navigation agent-type settings.

Reuse as-is: `NpcNavigation.cs`, `VenueVisibilityFilter.cs`, `VenueTravelManager`, `PlayerSpawnPoint` (pattern), `PlayerAnimSync`/`PlayerVenueSync` (pattern).

---

## Residual design notes
- **Why authority-only simulation is the clean choice:** running `NavMeshAgent`s on one machine means pathing *and* avoidance are computed once and synced via `RealtimeTransform`, so clients can never disagree about where an NPC is or how it dodged. Remotes do zero pathfinding.
- **Keep confinement in data, not code.** The "sidewalks + park only" guarantee lives entirely in the bake (Phase 1). No runtime `if (onRoad)` checks — those are fragile and were explicitly avoided.
- **Capsule is a stand-in only.** All param wiring (`Speed`, `IsTalking`, agent type, collider) must survive the art swap untouched.
- **Spawn spread** on the crowd so NPCs don't stack at spawn (same golden-angle idea as player spawns).

---

## Appendix A — Crosswalks (FUTURE phase, NOT in this plan)

**Owner asked: is letting NPCs cross at crosswalks hard / are there obvious conflicts?**

**Making them physically cross is easy.** Two supported options:
1. **Bake the crosswalk strips as walkable** — add just the crosswalk quads to the walkable whitelist so two sidewalk islands connect *only* across that strip. OR
2. **`NavMeshLink`** spanning each crosswalk (AI Navigation 2.0 `NavMeshLink` component) — explicit, easy to place, easy to disable.

**But there are two real conflicts — this is why it's deferred:**
1. **It breaks the "sidewalks + park only" rule.** A crosswalk is on the road; crossing is a deliberate, scoped exception that has to be explicitly allowed as a surface/link.
2. **No coordination with car traffic (the hard one).** Cars run on the spline lane graph with their own look-ahead and **no traffic lights / no pedestrian signal**, and cars are **not** `NavMeshObstacle`s — so by default NPCs and cars walk straight through each other. Doing it *properly* needs one of:
   - a **pedestrian/vehicle signal** system (shared light state both systems read), or
   - making cars **dynamic `NavMeshObstacle`s** so NPCs path around / stop (expensive, and fights the agents' baked islands), or
   - a lightweight **"don't step onto the link until no car is within X on this lane"** gate on the crosswalk link.
   Each also needs to be **deterministic or networked** (the authority decides go/wait so all clients agree).

**Recommendation when it's picked up:** start with the *minimal gated* version — NavMeshLinks at crosswalks, opened by the authority only when the lane is clear, accepting rare clipping — before investing in full signalized coordination. Revisit alongside the traffic-graph work (see the traffic-graph memory/plan; the lane graph could expose "is this crosswalk's lane clear?").
