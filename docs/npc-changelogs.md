# NPC Pathing / Pedestrian-Crowd — Changelog & Findings

> Companion to [`npc-pathing-plan.md`](./npc-pathing-plan.md). This file records what was actually done
> per phase, the data gathered during inspection, decisions made, and anything that deviated from the
> plan. Newest entries on top. As-built reference images + re-bake scripts live in
> [`Assets/Screenshots/npc-phase0-1-results.md`](../Assets/Screenshots/npc-phase0-1-results.md).

---

## 2026-11-09 (d) — Reverted to standard NavMeshAgent + built-in RVO (simpler, robust, animation-ready)

**Branch:** `feat/npc-pathing` · **Unity:** 6 · live via Unity MCP. **Scope:** `NpcAgent.cs` only.
**Owner call:** the custom separation steering still produced the occasional stalemate "if you let it run long
enough", and the owner (a) wanted it done "without much stress" and (b) couldn't see how a **walking animation**
would integrate with the custom lateral-veer movement. Both point the same way → **drop the hand-rolled steering
and use Unity's built-in local avoidance (RVO).** Owner choices: revert to RVO; leave edge-clinging for the
placeholder.
**Result:** compiles clean, 0 errors. Worst-case knot (24 agents warped to full overlap, minPair 0.01, each sent
to the opposite side to cross through): **all untangle, 0 stalled, avg 7.7 m, minPair 0.01→0.36** (RVO lets them
brush past, as real crowds do). Full crowd (120): **0 stalled, 119/120 displaced >1.5 m (avg 15.1 m), 120/120 on
walkable, minPair 0.40 m** (2 transient close passes). Screenshots refreshed.

### Why revert (the key reasoning — don't re-litigate without re-reading this)
- **Robustness:** my custom separation is **not reciprocal/velocity-aware** like RVO, so push-forces can cancel to
  a standstill. I patched it twice (feeler-ignores-NPCs, guaranteed-forward-progress, stuck-net) and it *still*
  had rare emergent locks. RVO (ORCA-style) is purpose-built to resolve reciprocal collisions and does not
  permanently deadlock. Fighting that with heuristics was the "stress".
- **Animation:** the custom path **overrode `agent.velocity` and added lateral veer** — the NPC slid sideways while
  facing roughly forward. A plain forward walk cycle on top of that reads as **foot-sliding/skating**. With the
  standard agent, `updateRotation = true` makes the NPC **face its own velocity**, so it turns to walk *into* the
  gap → a forward walk clip matches with no sliding. **This is why the simple path is also the animation-ready
  path.**

### What `NpcAgent.cs` is now (roughly half the previous size)
- The **agent moves itself**: `SetDestination` + built-in path-following + **`obstacleAvoidance =
  GoodQualityObstacleAvoidance`** + per-NPC `avoidancePriority` 20–80 (breaks symmetric standoffs). No
  `agent.velocity` override, no separation circle, no feeler, no edge push, no registry, no minForward.
- **Kept:** natural roaming (heading-cone wander + `wideTurnChance`, targets preferring `targetEdgeMargin` off the
  edge, varied `pauseChance`/dwell), per-NPC `speedRange` pace, off-mesh snap-back, and the `Speed` animator hook.
- **Added (tiny):** `updateRotation = true` (face travel direction) and a light **stuck-net** — if an agent wants
  to move but is <0.06 m/s for `stuckResetTime` (2 s), it re-picks a target. RVO rarely needs it; it's a backstop.
- **Removed fields:** detectRadius/personalSpace/separationStrength/veerStrength, edgeInfluence/edgeStopMargin/
  edgePushStrength, perception/feeler*, minForwardFactor. (Prefab just drops the stale serialized values on load.)

### Animation integration (for when the real character art replaces the capsule — nothing to build now)
1. Put an `Animator` on the character with a **`Speed` float** parameter driving an **idle ↔ walk ↔ run blend
   tree**. `NpcAgent` already feeds it `agent.velocity.magnitude` (damped) via `speedParameter` — set the field or
   name the param `Speed`. No code change.
2. **Leave root motion OFF** — the NavMeshAgent drives position; the clip plays "in place". This is the standard
   crowd setup and avoids foot-sliding as long as the walk clip's natural speed ≈ `agent.speed` (tune the clip or
   the `speedRange` to match, or normalise Speed by `agent.speed` into the blend tree).
3. Agent already **faces its velocity** (`updateRotation`), so only a forward locomotion set is needed; a 2D
   strafe blend tree is optional polish, not required.
4. If the capsule's `baseOffset`/scale differ from the character, re-fit on the prefab (agent radius 0.3 / height
   1.8 / baseOffset 0.9 are capsule values).

### Files touched
- **Modified:** `Assets/Script/NPC/NpcAgent.cs` (reverted to standard agent + RVO), `docs/npc-pathing-plan.md`,
  `docs/npc-changelogs.md` (this entry). Screenshots refreshed.
- **Not modified:** scene/prefab (script-only; prefab NavMeshAgent already had RVO + Pedestrian type from Phase 2),
  everything else. Not committed to git.

### Note
The three 2026-11-09 (a/b/c) entries below document the custom-steering path that this entry **supersedes**. They're
kept for the reasoning/lessons (esp. the gridlock analysis), but the shipped movement is now the standard RVO agent.

## 2026-11-09 (c) — Deadlock fix: separation gridlock resolved (feeler ignores NPCs + guaranteed forward progress)

**Branch:** `feat/npc-pathing` · **Unity:** 6 · live via Unity MCP. **Scope:** `NpcAgent.cs` only.
**Owner report (with screenshot):** NPCs could lock into a stalemate — *"everyone tries to move but could not… just
stuck in place."* A separation-steering gridlock (my custom separation isn't reciprocal/velocity-aware like RVO,
so forces can cancel to a standstill).
**Result:** compiles clean, 0 errors. Stress-tested the worst case — **24 agents warped into a fully-overlapping
knot (minPair 0.02 m) and each sent to the OPPOSITE side so they must cross through one another: all 24 untangled
(minPair 0.02→1.33 m), 0 stalled, avg 8.2 m displacement**. Full crowd (120): 0 persistent stalled, 115–118
moving, edge clearance actually improved (avg 1.37→**1.59 m**, 87/120 >1.0 m off the border), spacing still clean
(minPair 1.31 m, 0 pairs <0.7 m).

### Root causes + fixes (both in `NpcAgent.cs`)
1. **The head-on feeler was braking for other NPCs.** Its mask was "everything", so two NPCs facing each other each
   saw the other and slowed to `feelerMinScale` (~12%) — neither could execute the veer, so they locked in a
   standoff. **Fix:** the feeler now casts with `Physics.SphereCastNonAlloc` and **skips any hit that is another
   `NpcAgent` (or self)** — it only brakes for walls/props/player; the separation circle owns NPC-vs-NPC.
2. **Separation forces could cancel the path velocity to ~0.** Unlike RVO, a naive separation sum can equal
   `-pathVel` in a symmetric cluster → blended velocity ≈ 0 → frozen, with nothing to break it. **Fix: guaranteed
   forward progress** — in `ApplySteering`, whenever the agent is travelling, the steered velocity is forced to
   keep at least `minForwardFactor` (0.4) × walk speed **along the path direction** (which always follows the
   walkable NavMesh path, so it never drives into geometry). The speed clamp is also floored at that value, so the
   feeler can *slow* the crowd but never *stall* it. This is the core gridlock killer.
3. **Safety net:** an `stuckTimer` — if an NPC still wants to move but is near-stationary for `stuckResetTime`
   (1.2 s), it re-picks a fresh target to break any residual symmetry. (With #1+#2 it rarely triggers.)

### Why this holds
Forward progress is now a hard floor, so no combination of neighbour/edge forces can produce a standstill, and the
one remaining speed brake (the feeler) can't drop below that floor or react to other pedestrians. The knot test is
the strongest proof: starting from total overlap and forced head-on crossing — the exact failure in the owner's
screenshot — the crowd untangles every time instead of locking. Side benefit: keeping everyone flowing nudged the
average edge clearance *up* (fewer NPCs pinned against a border mid-manoeuvre). Interaction `GetComponentInParent`
check runs only on the throttled perception tick, into a shared `RaycastHit[8]` scratch buffer (no per-frame alloc).

### Files touched
- **Modified:** `Assets/Script/NPC/NpcAgent.cs` (feeler skips NPCs; `minForwardFactor` guaranteed progress;
  `stuckResetTime` unstick net). `docs/npc-pathing-plan.md` + `docs/npc-changelogs.md` (this entry).
- **Not modified:** scene/prefab (script-only), everything else. Not committed to git.

## 2026-11-09 (b) — Crowd traversal + edge-clinging fix: separation circle + edge-repulsion steering

**Branch:** `feat/npc-pathing` · **Unity:** 6 · live via Unity MCP. **Scope:** `NpcAgent.cs` only (no scene/prefab
change). Follow-up to the dynamic-wander entry below, from owner feedback: *"NPCs still struggle traversing
between themselves"* and *"most NPCs cling onto the edge of the navmesh."*
**Owner feel choices (this pass):** edge = **spread, off the edge** (mild inward push, not a center-line conga);
passing = **veer around, keep pace**; personal space = **medium ~1.1 m**.
**Result:** compiles clean, 0 errors. Neighbour spacing went from **minPair 0.35 m → 1.54 m with 0 pairs <0.7 m**
(was 2 sub-0.6 passes) — no clipping; edge clearance **avg 1.37 m, 73/120 comfortably >1.0 m off the border**
(was clinging); **0 stalled, 110 moving, 117/120 roamed >1.5 m** (avg 18.6 m). Screenshots refreshed
(`npc_dynamic_crowd_topdown.png`, `..._zoom.png`).

### What changed (the key shift: steer, don't just slow)
The previous pass only **slowed** NPCs for neighbours/edges; it never **steered** them, which is why crowd
traversal struggled and NavMesh paths let them hug walls. `NpcAgent` now adds a steering layer:
- **Separation circle** — each NPC keeps a static registry (`List<NpcAgent> All`, OnEnable/OnDisable) and, every
  frame, sums a push away from neighbours within `detectRadius 2.4 m` (ramping hardest inside `personalSpace
  1.1 m`). For a neighbour **ahead** it adds a **sideways veer toward the OPEN side** (picked via the sign of
  `dot(right, toNeighbour)`) so two walkers pass instead of doing the "after-you" standoff. Weights
  `separationStrength 1.2`, `veerStrength 1.0` (as fractions of the NPC's walk speed). Registry-based (positions),
  not physics — no layer setup, ~14 400 cheap distance checks/frame at 120 agents.
- **Edge repulsion** — a mild inward push (`NavMesh.FindClosestEdge`) when within `edgeInfluence 1.6 m` of the
  border, strongest at `edgeStopMargin 0.4 m`, `edgePushStrength 0.9`. Replaces the old edge *speed-brake*
  (direction fix, not a slow-down) so NPCs drift off the kerb and use open pavement.
- **Built-in RVO turned OFF** (`obstacleAvoidanceType = NoObstacleAvoidance`): we now own neighbour avoidance, and
  two avoidance systems writing the same velocity just jitter. The agent still **pathfinds**; we drive it by
  **setting `agent.velocity`** each frame to a smoothed blend of `agent.desiredVelocity` (path + autoBraking) +
  separation + edge push, **clamped to `agent.speed`** so NPCs veer *without* speeding up → they keep pace.
- **Head-on brake kept but demoted** to a last resort: the forward `SphereCast` feeler shrank to `feelerDistance
  1.5 m` and only eases speed when something is right in front with no room to veer (separation handles the rest).
  (Removed the `obstacleAvoidance`/`avoidancePriorityRange` fields — irrelevant with RVO off.)

### Why this architecture
"How fast" (feeler speed-cap) and "where" (path + separation + edge, driving `agent.velocity`) are now cleanly
separate. Basing the blend on `desiredVelocity` preserves path-following AND arrival deceleration, and keeps the
earlier deadlock lesson intact: the one remaining speed clamp (the feeler) derives its heading from
`desiredVelocity`, never from actual `velocity`. Clamping the steered vector to `agent.speed` is what makes "veer
around, keep pace" literal — lateral avoidance trades against forward progress instead of adding speed.

### Tuning notes for next time
- `minPair 1.54 m` (> personalSpace) means separation is slightly on the generous side — if the owner wants a
  **denser/tighter** crowd, lower `separationStrength`/`detectRadius` or raise `NpcDevSpawner.count`; the crowd
  still reads busy but is politely spread right now.
- 21/120 were within 0.6 m of an edge — mostly on genuinely narrow park paths/thin sidewalks where more clearance
  isn't possible; raising `edgePushStrength` reduces it but risks the center-line "conga" the owner didn't want.
- Not committed to git.

## 2026-11-09 — Phase 3 REPLACED: fully dynamic natural wander (markers removed) + packed crowd (~120), single-client

**Branch:** `feat/npc-pathing` · **Unity:** 6 · live via Unity MCP (`execute_code` + play-mode test).
**Owner directive (this pass):** make NPCs move **entirely on their own** — *remove the authored "pointers"*,
make the pathing feel natural (slow before an obstacle ahead, ease off before the navmesh edge instead of
marching to the brink, give way to each other like real pedestrians), and **crowd the city**. Owner choices
for this pass: **single-client** (defer Phase 4 networking), **~100+ packed**, **keep per-island** (crosswalks
stay deferred).
**Result:** compiles clean, 0 runtime errors. 120 NPCs spawn across the populated islands and roam dynamically;
**108/120 moving / 0 stalled**, **119/120 travelled >1.5 m** in the sample window (avg 18.5 m), strict
sidewalk+park confinement held (**120/120 on walkable area 0, 0 bad-area**), only **2 transient tight passes
<0.6 m** out of ~7 000 pairs (no clipping). **`WorldScene.unity` edited + saved** (12 markers removed; dev
spawner count 20→120). Screenshots: `Assets/Screenshots/npc_dynamic_crowd_topdown.png` (city-wide) +
`npc_dynamic_crowd_zoom.png` (capsules strolling a sidewalk strip, none on the road).

### What changed vs. the old Phase 3 (authored destinations)
- **The "pointers" are gone.** Deleted `Assets/Script/NPC/PedestrianDestination.cs` (+`.meta`) and removed the
  `PedestrianDestinations` root (all 12 markers) from `WorldScene`. `NpcAgent` no longer has a `Destinations`
  or `Waypoints` mode, no `destinationShare`, no marker registry — it is **wander-only and fully procedural**.
- **`NpcAgent.cs` rewritten for natural motion** (same file, same prefab, same Pedestrian agent type — art/
  Animator swap-in path and Phase-4 authority-gating hooks preserved):
  - **Natural roaming** — picks the next spot *ahead of itself* inside a heading cone (`headingConeDeg 70°`,
    `wideTurnChance 0.15`) so walking looks purposeful, not jittery; distance `minStepDistance 4 … roamRadius
    14`; **targets kept ≥ `targetEdgeMargin 1.5 m` from the mesh edge** (via `NavMesh.FindClosestEdge`) so an NPC
    aims for open pavement, never the brink. Per-NPC **speed rolled 0.9–1.6 m/s** so the crowd isn't a lockstep
    march; `acceleration 3.5`, `autoBraking` on → soft starts/stops. Varied dwell (`pauseChance 0.35`, a real
    rest of 1.5–5 s, else a 0–0.6 s corner-pause).
  - **Anticipation layer** (the "slow before you hit it" feel), on a **staggered perception tick**
    (`perceptionInterval 0.12 s`, phase-offset per NPC so 120 agents don't all raycast on one frame); the speed
    *cap* is then lerped every frame so it reads smooth:
    - **Forward feeler** — a `SphereCast` (`feelerDistance 2.2`, `feelerRadius 0.45`, origin pushed clear of the
      agent's own capsule, chest height so it clears the flat ground) eases speed toward `feelerMinScale 0.12`
      when a wall/prop/other-NPC/player is right ahead; ramps back once clear.
    - **Edge brake** — `NavMesh.FindClosestEdge`; when heading *toward* a kerb within `edgeSlowStart 1.3 m`,
      ease toward `edgeMinScale 0.25` (kept **> 0** — see the deadlock fix below).
  - **Avoidance** unchanged in spirit: `GoodQualityObstacleAvoidance`, per-NPC `avoidancePriority` 20–80.
- **`NpcDevSpawner.cs`** — default `count` 20→**120**, added a **min-separation (1.2 m)** so NPCs don't start
  stacked; still self-guards (editor-only, stands down if a Normcore `Realtime` is present). Random bound-box
  samples land ~proportional to island area, so the whole populated city reads as busy. **The scene instance's
  `count` field was set to 120** (the serialized value overrides the code default).

### Critical bug found + fixed during the live test (worth remembering)
- **First pass froze the crowd: 105/120 "wants-to-move but velocity ≈ 0".** Cause was a **feedback deadlock in
  the edge brake**: heading was taken from *current velocity*, falling back to `transform.forward` when stopped,
  with an edge-brake **floor of 0**. An NPC that stopped near a kerb facing it got braked to 0 → stayed at 0 →
  heading stayed `forward` into the edge → never restarted. Narrow walkable corridors made this common.
- **Fix:** drive anticipation from **`agent.desiredVelocity`** (the *intended* path direction, which points at
  the edge-safe target, not the stalled actual velocity); **return scale 1 when `desiredVelocity ≈ 0`** (arrived
  / no path → nothing to brake); give the **edge brake a non-zero floor (`edgeMinScale 0.25`)** so a paused NPC
  can always set off again; and push the **feeler origin clear of the own capsule** (`agentRadius + feelerRadius
  + 0.1`) so it never self-hits. Re-test: **108 moving / 0 stalled**, speed caps avg 1.18 (near full pace).
  *Lesson for anyone tuning this:* any per-frame speed clamp that can reach 0 must derive its heading from
  `desiredVelocity`, not `velocity`, or it can latch a stopped agent off permanently.

### Live verification (play mode, WorldScene, 120 agents)
- Spawn: `[NpcDevSpawner] Spawned 120/120`. Confinement: **120/120 `isOnNavMesh`, 120/120 on walkable area 0,
  0 bad-area** (sampled at ground y≈23.2, `hit.mask & 1`). Never on road/asphalt/parking/water.
- Liveness: **108 moving / 0 stalled / 12 dwelling** at an instant; over a window **119/120 displaced >1.5 m**
  (avg 18.5 m, max 51.9 m) → the whole crowd is actually roaming, not a few.
- No clipping / no deadlock: min pairwise distance 0.35 m with only **2 pairs <0.6 m** (transient squeezes RVO
  resolves) across ~7 000 pairs. Speed caps spread (min 0.19 / avg 1.18 / max 1.58) → anticipation is modulating
  pace (not everyone at max), exactly the intended natural variation.
- Test instances were **play-mode only** → gone on Stop (verified **0 stray `NpcAgent`** after stop). Saved
  scene deltas = 12 markers removed + spawner count 120. EditorPrefs scratch keys cleaned up.

### Files touched
- **Deleted:** `Assets/Script/NPC/PedestrianDestination.cs` (+`.meta`).
- **Modified:** `Assets/Script/NPC/NpcAgent.cs` (rewritten: wander-only + natural roaming + anticipation),
  `Assets/Script/NPC/NpcDevSpawner.cs` (count 120 + min-separation + comment cleanup),
  `Assets/Scenes/WorldScene.unity` (removed `PedestrianDestinations`/12 markers; dev spawner count → 120; saved),
  `docs/npc-pathing-plan.md` (Phase 3 re-scoped to dynamic wander), `docs/npc-changelogs.md` (this entry).
- **New:** `Assets/Screenshots/npc_dynamic_crowd_topdown.png`, `Assets/Screenshots/npc_dynamic_crowd_zoom.png`.
- **Not modified:** `NpcAgent.prefab` (NavMeshAgent/Pedestrian settings + baseOffset unchanged; the removed
  serialized fields like `mode`/`destinationShare` are simply dropped on load), `NpcNavigation.cs` (separate
  dialogue-NPC walker — untouched), the NavMesh/agent-type/whitelist (Phase 1 locked), player carving (2.4).

### Tuning knobs (all on `NpcAgent`, exposed in the Inspector for the owner)
`speedRange`, `roamRadius`/`minStepDistance`, `headingConeDeg`/`wideTurnChance`, `targetEdgeMargin`,
`pauseChance`/`pauseTime`/`strollPause`, `perceptionInterval`, `feelerDistance`/`feelerRadius`/`feelerMinScale`/
`anticipationMask`, `edgeSlowStart`/`edgeStopMargin`/`edgeMinScale`, `avoidancePriorityRange`. Crowd size is
`NpcDevSpawner.count` (and `minSpacing`). All take code defaults on the prefab; the scene spawner carries 120.

### Open / deferred (unchanged intent)
- **Phase 4 (networking) is still next** and un-started — `NpcSync`/`NpcDirector`/authority gating wrap around
  this movement script with no logic change. `NpcDevSpawner` remains the single-client populator until then
  (it stands down when Normcore `Realtime` is present).
- **Perf at 120 with GoodQuality avoidance + per-agent perception** looked fine in-editor but was not profiled
  on a target device — revisit in Phase 5 (drop avoidance quality or `count`, or widen `perceptionInterval`, if
  the weakest likely-authority device struggles). Perception is already staggered to spread the cost.
- Crosswalks still deferred (Appendix A); NPCs roam **per-island** only, which is why the crowd is spread by
  island area rather than flowing between them.
- Not committed to git (scene saved, uncommitted) — owner to review/commit on `feat/npc-pathing`.

## 2026-11-05 — Walkability fix: sub-tile marker-driven correction at `business_center_001` (one building only)

**Branch:** `feat/npc-pathing` · **Unity:** 6 · live via Unity MCP (`execute_code` + Pedestrian re-bake).
**Scope:** `business_center_001`'s NE-corner plaza ONLY (owner directive). Generalizing to other buildings and
the whole-scene allowed/not-allowed map remain **deferred**. Phase 4 (networking) untouched.
**Result:** all 4 owner markers now match intent; 0 console errors; **`WorldScene.unity` edited + saved**;
Pedestrian surface re-baked (`Assets/Scenes/WorldScene/NavMesh-NavMesh Surface 4.asset`). After-shot:
[`Assets/Screenshots/bugs_screenshots/business_center_walkability_after.png`](../Assets/Screenshots/bugs_screenshots/business_center_walkability_after.png).

### Spec (owner's 3D markers in WorldScene = source of truth)
Cylinder = **cannot** walk, Sphere = **can** walk. Four markers at the plaza/curb:

| Marker | World (x,z) | On tile | Before | After |
|---|---|---|---|---|
| `Cylinder (1)` | (200.0, 135.95) | `building_platform_005 (14)` walkable | walkable | **blocked** ✓ |
| `Cylinder` | (210.43, 143.74) | `building_platform_005 (14)` walkable | walkable | **blocked** ✓ |
| `Sphere (1)` | (208.23, 133.82) | `building_platform_005 (14)` walkable | walkable | **walkable** ✓ |
| `Sphere` | (213.31, 133.82) | `Road_010 (13)` (road) | no navmesh | **walkable + connected** ✓ |

### Key mechanism findings
- **The whitelist is `NavMeshSurface.defaultArea = 1 (NotWalkable)` + per-walkable-tile `NavMeshModifier
  (overrideArea, area 0)`** (82 Walkable tile modifiers). Roads have no modifier → default NotWalkable → not
  baked. This is why sub-tile control is needed (three markers share tile `building_platform_005 (14)`).
- **A `NavMeshModifierVolume(area=NotWalkable)` CAN carve a hole in an already-walkable tile**, but a
  `NavMeshModifierVolume(area=Walkable)` CANNOT *add* navmesh over a default-NotWalkable road: with
  `defaultArea=NotWalkable`, those spans are filtered out early, so a Walkable volume has nothing to
  reclassify. (Carving/removal works; resurrection does not.)
- **The marker meshes themselves carve the bake.** Sphere/Cylinder primitives sit on the ground; once the
  surface is re-baked with them present (`collectObjects=All`, `RenderMeshes`), each rasterizes as a
  NotWalkable obstacle and punches a ~1 m hole — including under `Sphere (1)`, which must stay walkable. Fix:
  **`NavMeshModifier{ignoreFromBuild=true}` on all 4 markers** (kept in place as the visible spec, excluded
  from the bake). The baseline asset predated the markers, so the holes only appeared on re-bake.
- **Adding walkable navmesh on the road sub-tile** needs real geometry with its own walkable modifier, not a
  volume. Used a thin **helper cube** (`NavMeshModifier overrideArea area 0`); gotchas: (a) the surface
  ignores **disabled** MeshRenderers, so keep the renderer enabled and hide it with
  `MeshRenderer.forceRenderingOff = true`; (b) a slab thinner than the 0.1 m voxel height doesn't rasterize;
  (c) the road surface (top ≈23.3) **masks** any helper at/below it, so the helper top must sit **above** the
  road — set to 23.40 (≈0.1 m lip; patch bakes at y≈23.45, traversable, `CalculatePath = PathComplete`).

### What was added (all under `WorldScene` → `WalkabilityVolumes_business_center_001`)
- `Block_Cylinder1` — `NavMeshModifierVolume` area 1, pos (200.00, 23.20, 135.95), size 2.5×4×2.5.
- `Block_Cylinder` — `NavMeshModifierVolume` area 1, pos (210.43, 23.20, 143.74), size 2.5×4×2.5.
- `Allow_SphereRoad_helper` — invisible cube (renderer `forceRenderingOff`), `NavMeshModifier` area 0,
  pos (212.80, 23.15, 133.82), scale 4×0.5×2.6 (top 23.40), biased to bridge the x≈212 sidewalk↔road seam.
- `NavMeshModifier{ignoreFromBuild=true}` on the 4 owner markers (`Cylinder`, `Cylinder (1)`, `Sphere`,
  `Sphere (1)`).

### Verification (live, Pedestrian mesh, `NavMesh.AllAreas`)
- `SamplePosition` r=0.6 at the 4 spots → both cylinders **none**, both spheres **walkable area 0**.
- `CalculatePath` sidewalk interior (204,136) → curb sphere (213.31,133.82) = **PathComplete** (reachable).
- No regression: road interior (220,139)/(218,142)/(216,136) still **none**; plaza/sidewalk interior still
  walkable; every change is local to this corner (same surface settings) so other islands are unchanged.
  Total navmesh ≈ 15203 m² (1956 tris).

### Open / deferred
- The curb patch is a **deliberate local exception** to the "sidewalks + park only, never roads" rule (owner
  marked it). Flagged for owner awareness. The ~0.1 m raised lip is a side effect of the helper technique.
- A **general rule** for accessible-vs-blocked spots was NOT invented — built strictly to the 4 marker points.
  Generalizing to all buildings + reconciling `allowedVsNotAllowed.png` is still the owner's call.
- Not committed to git (awaiting owner).

## 2026-11-05 — Phase 3 implemented (authored destinations: marker + registry + mixed wander/destination picking, un-networked)

**Branch:** `feat/npc-pathing` · **Unity:** 6 · live via Unity MCP (`execute_code` + play-mode test).
**Scope:** Phase 3 only (single-client, no Normcore). Phase 4 (networking) not started.
**Result:** compiles clean, 0 runtime errors; some NPCs path to markers → dwell → re-pick, the rest wander,
all stay on sidewalk/park navmesh, none stuck. **`WorldScene.unity` edited + saved** (12 markers added).

### What was built
1. **`Assets/Script/NPC/PedestrianDestination.cs`** (new) — marker component for an empty GameObject on a
   walkable spot. Fields: `Kind { Generic, Bench, Shop, ParkSpot }` (flavor/filtering only — no behavior
   difference yet) and `Vector2 dwell` (per-visit lingering, min-max). **Static `List<PedestrianDestination>
   Active` registry** populated in `OnEnable` / removed in `OnDisable`, mirroring `PlayerSpawnPoint`'s
   registry pattern (WorldScene is additive → agents find markers via the static list, not references).
   Static `TryGetRandomReachable(NavMeshAgent, NavMeshPath buffer, out marker)` scans the registry in a
   randomized order and returns the first marker the agent can `CalculatePath`→`PathComplete` to (no LINQ /
   no alloc → CodeDom-safe; the caller passes a pre-allocated path so no `NavMeshPath` field initializer).
   `RollDwell()` returns a random dwell within range. `OnDrawGizmos` color-codes by kind.
2. **`Assets/Script/NPC/NpcAgent.cs`** (extended) — added **`Destinations`** to `enum MoveMode` (appended =2,
   so serialized RandomWander=0 / Waypoints=1 stay stable). A per-NPC **`[Range(0,1)] destinationShare`**
   (default 0.5) is rolled once in `Awake`: when the serialized mode is `RandomWander` it promotes to
   `Destinations` with that probability (never overrides an explicitly `Waypoints` NPC) — the wander/
   destination split lives per-NPC until the Phase-4 `NpcDirector`. New `Destinations` branch in
   `TryGetNextDestination` uses `PedestrianDestination.TryGetRandomReachable` (reusing the existing
   `SampleReachable` for the final nav point); on no reachable marker it **falls through to wander** that turn.
   Dwell now comes from the chosen target: a new `nextDwell` field is set in `TryGetNextDestination`
   (marker's `RollDwell()` in Destinations mode, else `Random(waitTime)`) and consumed as the arrival
   `waitTimer` — the off-by-one is intentional and correct (waitTimer isn't ticked while travelling, so the
   value set on departure is the dwell spent on arrival at that target).
3. **12 `PedestrianDestination` markers placed in `WorldScene`** under a `PedestrianDestinations` root,
   spread across the **6 populated navmesh islands** (the walkable mesh is disconnected into 9 islands by
   roads; crosswalks deferred). Each marker was placed at a `NavMesh.SamplePosition` snap on the Pedestrian
   mesh and verified on **walkable area 0** (`hit.mask & 1`). Weighted toward the Phase-2 test island (C3,
   x[110..206] z[103..143], 4 markers) so Destinations-mode NPCs there have several targets; kinds: Park/
   Bench on park, Shop/Generic on sidewalks. Dwell by kind: Bench (4-8), ParkSpot (3-7), Shop (2-4),
   Generic (2-5).

### Island map (from a Pedestrian-navmesh grid scan + reachability clustering)
9 disconnected islands; the 6 markered ones: **C3** `centroid(156,123)` (Phase-2 test island, 4 markers),
**C4** `(162,198)`, **C7** `(193,259)`, **C0** `(74,131)` (west sidewalk strip), **C8** `(244,156)`,
**C2** `(114,251)`. (3 tiny islands left unmarkered.)

### Live verification (play mode, WorldScene)
Spawned 10 `NpcAgent` at the C3 cluster `(126,122)`, **forced 5 into Destinations mode and 5 RandomWander**
(via reflection, to make the test deterministic rather than relying on the 0.5 share roll), ran ~40 s:
- **Registry:** 12 markers registered (`PedestrianDestination.ActiveCount == 12`) on scene load.
- **Destinations behavior:** 5/5 Destinations agents had `agent.destination` **exactly on a marker**
  (dist 0.0) across every snapshot. Observed **arrival → dwell → re-pick**: e.g. NPC_1 Plaza_A → Bench_B,
  NPC_3 Plaza_A → Park_A (target changed to a *different* reachable marker after dwelling).
- **Wander behavior:** 5/5 RandomWander agents targeted **non-marker** points (nearest marker 6-20 m from
  their destination) — distinct behavior, no accidental marker-seeking.
- **Confinement / liveness:** 10/10 `isOnNavMesh` at every snapshot; all moved (vel ~1.4) or were in a
  legitimate dwell (vel 0 at a reached target); none permanently stuck. 0 console errors.
- Screenshot `Assets/Screenshots/npc_phase3_destinations_topdown.png` — capsules spread across the park
  lawn (walkable area only). "Before" (wander-only) = the Phase-2 `npc_phase2_crowd_topdown.png`.
- Test instances were **play-mode only** (`NPC_TEST_ROOT`) → gone on Stop; after stop: 0 stray `NpcAgent`,
  12 markers remain, `NPC_TEST_ROOT` absent. The only saved scene change is the 12 markers.

### Gotchas (learned this phase)
- **`NavMesh.SamplePosition(pos, out hit, dist, int)` — the int is `areaMask`, NOT an agent-type id.** Passing
  the Pedestrian id (`-1372625422`, an even number → bit 0 clear) masked out area 0 (Walkable) and returned
  0 hits everywhere. Use `NavMesh.AllAreas` (or `1<<0`) to sample walkable. Agent type is irrelevant to
  `SamplePosition` (it samples all navmeshes); only one Pedestrian surface is baked, so AllAreas is correct.
  For per-island reachability use a `NavMeshQueryFilter { agentTypeID = Pedestrian, areaMask = AllAreas }`
  with `NavMesh.CalculatePath`.
- **Markers sit at ground height (y≈23.2) with no `baseOffset`**, so `SamplePosition(transform.position, …,
  1.5)` verifies them directly — unlike agents, whose transform is +0.9 above the mesh (sample at ground).
- **Private `[SerializeField]` fields on created components are set via `SerializedObject` + `FindProperty`**
  (`enumValueIndex` for the kind enum, `vector2Value` for dwell) + `ApplyModifiedPropertiesWithoutUndo`.
- **Forcing a play-mode agent's mode must happen *after* Instantiate** (Awake — and its `destinationShare`
  roll — runs during `Instantiate`). Set the private `mode` field by reflection post-spawn; `Update` reads
  it live, so a mid-run change takes effect next frame. Zeroing `waitTimer` makes it pick immediately.

### Dev tooling (for single-client testing until Phase 4)
Added **`Assets/Script/NPC/NpcDevSpawner.cs`** (new) + a **`NpcDevSpawner (DEV)`** GameObject in WorldScene
(saved) so the owner can hit Play and watch the crowd without the networked director. It scatters N (default
20) `NpcAgent` prefabs onto random Pedestrian-navmesh samples across the Phase-1 walkable bounds on `Start`.
**Self-guarding so it's safe to leave in the shared scene:** it `Destroy`s itself in a player build
(`!Application.isEditor`) and stands down if a Normcore `Realtime` is present in the loaded scenes (the real
flow loads WorldScene additively under MainMultiplayerScene, which carries Realtime) — so it never creates
per-client ghost crowds. **It is replaced by `NpcDirector` in Phase 4** and can be deleted then. Smoke-tested:
opening WorldScene standalone + Play → 20/20 spawned on-mesh, a 10/10 Destinations/Wander split from the 0.5
`destinationShare` roll, 16/20 moving, 0 errors.

### Files touched
- **New:** `Assets/Script/NPC/PedestrianDestination.cs` (+`.meta`), `Assets/Script/NPC/NpcDevSpawner.cs`
  (+`.meta`, dev-only), `Assets/Screenshots/npc_phase3_destinations_topdown.png`.
- **Modified:** `Assets/Script/NPC/NpcAgent.cs` (Destinations mode + share roll + marker-driven dwell),
  `Assets/Scenes/WorldScene.unity` (12 markers under `PedestrianDestinations` + a dev-only
  `NpcDevSpawner (DEV)` object, saved), `docs/npc-pathing-plan.md` (Phase 3 checked off),
  `docs/npc-changelogs.md` (this entry).
- **Not modified:** `NpcAgent.prefab` (Destinations share is a live roll from the C# default 0.5 — no prefab
  value needed), the NavMesh/agent-type/whitelist (Phase 1 locked), player carving (Phase 2.4), venue/traffic.

### Loose ends / next
- **Phase 4** next: networking — `NpcSync : RealtimeComponent<NpcSyncModel>`, authority gating
  (`isOwnedLocallyInHierarchy` + `LocalVenue == World`), `NpcDirector` (roster/authority/migration),
  venue visibility. Markers are plain scene data and need no networking.
- Nothing committed to git (scene saved, uncommitted) — owner to review/commit on `feat/npc-pathing`.

---

## 2026-11-05 — Phase 2 implemented (prototype capsule agent + pathing + mutual avoidance, un-networked)

**Branch:** `feat/npc-pathing` · **Unity:** 6 · live via Unity MCP (`execute_code` + play-mode test).
**Scope:** Phase 2 only (single-client, no Normcore). Phases 3–6 not started.
**Result:** compiles clean, 0 runtime errors, crowd confined + weaving + no deadlock, player carving in place.
`WorldScene.unity` **not modified** (all changes are new/edited assets); nothing committed to git.

### What was built
1. **`Assets/Script/NPC/NpcAgent.cs`** (new) — self-contained pedestrian adapted from `NpcNavigation.cs`.
   Keeps: NavMesh re-sampling, arrival/wait with randomized dwell desync, wander-around-home, `Speed`
   animator wiring (no-op on the capsule). Adds: avoidance config, **per-NPC `avoidancePriority` randomized
   20–80 in `Awake`**, and a **runtime off-mesh snap-back** (re-sample + `Warp` in `Update` when
   `!isOnNavMesh`, not just in `Start`). Destinations validated with `CalculatePath`→`PathComplete`
   (respects disconnected islands). Dropped the DialogueTrigger/NpcController coupling (prototype has none).
2. **`Assets/Resources/NpcAgent.prefab`** (new) — capsule (`MeshRenderer`+`CapsuleCollider`), scaled
   `(0.6,0.9,0.6)` → world r0.3/h1.8, + `NavMeshAgent` (**Pedestrian `-1372625422`**, r0.3/h1.8,
   **`baseOffset=0.9`** so the capsule bottom sits on the mesh, speed 1.4, stoppingDistance 0.2,
   `GoodQualityObstacleAvoidance`) + `NpcAgent`. **Placed under `Resources/`** (not `Assets/Prefab/`) so
   Phase-4 `Realtime.Instantiate("NpcAgent")` resolves it by name like the avatar prefabs.
3. **Player carving (2.4)** — added a capsule `NavMeshObstacle` (`center (0,0.49,0)`, r0.5/h1.73,
   `carving=true`, `carveOnlyStationary=false`, moveThreshold 0.25, timeToStationary 0.3) to **both**
   `Assets/Resources/Player.prefab` and `Player Female.prefab`. Additive — does not disturb
   `RealtimeView._components` ordering. NPCs flow around a walking player.

### Live verification (play mode, WorldScene)
Spawned 10 `NpcAgent` prefab instances in a ~4 m cluster at the densest walkable island center
`(126, 23.25, 122)` (found by scanning the Pedestrian navmesh), ran ~9 s:
- **On-mesh:** 10/10 `isOnNavMesh`. **Confinement:** 10/10 on walkable **area 0**, **0** bad-area, **0**
  out-of-bounds (bounds `x[61.8..269.3] z[86.8..281.1]`). Never on road/asphalt/water.
- **Avoidance:** priorities spread 25/28/35/41/45/50/68/73/77/78; **min pairwise distance 3.74 m** (no clip).
- **No deadlock:** all 10 displaced (avg **9.9 m**) between two snapshots; 8–9 at full speed, the rest in
  normal dwell.
- Screenshot `Assets/Screenshots/npc_phase2_crowd_topdown.png` — capsules spread across the park lawn,
  separated, walking.
- Test instances were **play-mode only** → gone on Stop; scene not dirtied.

### Gotchas (learned this phase)
- **`NavMeshPath` cannot be built in a MonoBehaviour field initializer** (`new NavMeshPath()` as a field
  throws `InitializeNavMeshPath is not allowed ... call it in Awake or Start`). First run spammed that +
  cascading `NullReferenceException`s. Fix: allocate `reusablePath` in `Awake`. (Applies to any reused path.)
- `NavMeshAgent.baseOffset` must be **half the capsule height (0.9)** or the capsule half-sinks into the mesh
  (agent logical position sits on the navmesh; transform = navmesh + up*baseOffset).
- Sampling an agent's position to verify its area must sample at **ground height (y≈23.2)** with a generous
  radius — sampling at `transform.position` (which is +0.9 from baseOffset) with a small radius misses the
  mesh ~0.95 m below and gives false "off-mesh" results. `isOnNavMesh` is the authoritative on-mesh check.
- Entering Play on `WorldScene` alone does **not** auto-connect Normcore (no `Realtime` in this scene) — safe
  to play-test agents in isolation.

### Files touched
- **New:** `Assets/Script/NPC/NpcAgent.cs`, `Assets/Resources/NpcAgent.prefab` (+`.meta`),
  `Assets/Screenshots/npc_phase2_crowd_topdown.png`.
- **Modified:** `Assets/Resources/Player.prefab`, `Assets/Resources/Player Female.prefab` (carving obstacle),
  `docs/npc-pathing-plan.md` (Phase 2 checked off), `docs/npc-changelogs.md` (this entry).
- **Not modified:** `WorldScene.unity`, the NavMesh/agent-type/whitelist (Phase 1 locked), venue/traffic.

### Loose ends / next
- **Phase 3** next: `PedestrianDestination` markers + mixed wander/destination picking.
- Phase 4 networking (`NpcSync`/`NpcDirector`/authority) still pending; the carving obstacle + prefab already
  use Phase-4-ready choices (Resources path, migratable-friendly).

---

## 2026-11-05 — Phase 0 + Phase 1 implemented (NavMesh walkable confinement, baked)

**Branch:** `feat/npc-pathing`
**Unity:** 6 · **Editor instance:** `Go-Vista-Live` (live via Unity MCP `execute_code`)
**Scope:** Phase 0 (pre-flight) and Phase 1 (walkable confinement + bake) only. Phases 2–6 not started.
**Result:** compiles clean, 0 console errors, confinement verified, `WorldScene.unity` saved. Not yet
committed to git.

### Phase 0 — Pre-flight

#### 0.1 Walkable meshes (WorldScene)
**There is no object named "sidewalk."** The ground is a grid of **15×15 m modular tiles** (ithappy
Megacity kit), all flat at **y ≈ 23.2**. Built a temporary color-coded top-down overlay to classify every
tile type, then confirmed with the owner. 497 MeshRenderers total.

| Class | Tiles (GameObject-name prefix) | Count | Walkable? |
|---|---|---|---|
| **Sidewalk** | `building_platform_*` (002/003/004/005/009/012/013) | 60 | ✅ |
| **Park (lawn)** | `grass_001` | 17 | ✅ |
| **Park (path)** | `park_road_001` + `park_road_003` | 5 | ✅ |
| Roads | `Road_009/010/011/012/013/019` | 135 | ❌ |
| Plazas/lots | `asphalt_001` | 40 | ❌ (owner ruled these plazas/lots) |
| Parking | `parking_*` / `disabled_parking_*` | 8 | ❌ |
| Water | `water` | 1 | ❌ |
| Terrain / buildings / props | `Terrain*`, buildings, benches, trees… | — | ❌ |

**Walkable total = 82 tiles.** Tiles are individual flat GameObjects (many at scene root, some nested under
groups like `benche`/`set`/`g`) — **no clean sidewalk/park parent object**. That single fact drove the
Phase-1 recipe choice (per-tile modifiers, not parent grouping; see 1.2).

#### 0.2 Current NavMesh setup (before)
- **1 agent type:** `Humanoid` (id 0, r0.5 / h2 / climb0.75 / slope45).
- **1 `NavMeshSurface`** ("NavMesh Surface"): `collectObjects=All`, `useGeometry=RenderMeshes`,
  `defaultArea=0 (Walkable)`, `layerMask=Everything`, data `Assets/Scenes/WorldScene/NavMesh-NavMesh Surface 4.asset`.
- **28 `NavMeshModifier`s** — ALL `overrideArea → 1 (Not Walkable)`, `applyToChildren`, on props only
  (benches/trees/trash/fountains/playground/monument). Old approach = **"all walkable, carve props"** — the
  opposite of the goal. These 28 are now **redundant** (default is NotWalkable after 1.2) but left in place
  (harmless, consistent with intent).

#### 0.3 Capsule / agent dimensions (owner-confirmed)
**radius 0.3 m, height 1.8 m, step (climb) 0.4 m, max slope 45°.** → new `Pedestrian` agent type (see 1.1).
Sidewalk tiles are a full 15 m wide, so fit is not tight; a slimmer agent hugs road/building edges more
fully. Existing `Humanoid` left untouched.

#### 0.4 Normcore flags (de-risk Phase 4)
Reflected `Normal.Realtime.Realtime+InstantiateOptions` → fields are: `ownedByClient`,
`preventOwnershipTakeover`, `destroyWhenOwnerLeaves`, `destroyWhenLastClientLeaves`, `useInstance`,
`defaults`. **There is NO combined `destroyWhenOwnerOrLastClientLeaves` field** (the plan's shorthand) — it's
two separate flags. Existing avatar pattern (`PlayerManager.cs:100`) = `ownedByClient=true,
preventOwnershipTakeover=true` (owned, **non**-migratable). NPCs need the **opposite** for authority
migration:

```
ownedByClient            = true    // the elected authority owns it
preventOwnershipTakeover = false   // next authority can RequestOwnership() on migration
destroyWhenOwnerLeaves   = false   // NPC survives when the authority disconnects/leaves World
destroyWhenLastClientLeaves = true // clean up roster when the room empties
useInstance              = realtime
```
Verify behaviorally in Phase 4.

#### 0.5 N + spawn strategy (DRAFT, finalized Phase 5)
Target **N ≈ 20–30** capsules for the prototype (small city; one authority simulates all). Spawn at **random
walkable NavMesh samples** (`NavMesh.SamplePosition` on jittered points within the walkable bounds
`x[61.8..269.3] z[86.8..281.1]`), golden-angle spread like `PlayerSpawnPoint.GetPosition`, snapped to the
`Pedestrian` navmesh. Authored destinations added in Phase 3.

### Phase 1 — Walkable confinement (built + baked)

#### 1.1 `Pedestrian` NavMesh Agent Type (new)
Created by appending to the `NavMeshProjectSettings` singleton
(`Unsupported.GetSerializedAssetInterfaceSingleton("NavMeshProjectSettings")` → `m_Settings` /
`m_SettingNames`), seeded with a valid id via `NavMesh.CreateSettings()`.
- name **`Pedestrian`**, `agentTypeID = -1372625422`, radius 0.3, height 1.8, climb/step 0.4, slope 45,
  cell size = auto (manualCellSize=0). `Humanoid` (id 0) untouched.

#### 1.2 Confinement recipe — **area-override whitelist** (as built)
Chose this over the plan's primary "parent grouping" option and over the "Walkable layer" alternative,
because the tiles are flat/individual with no clean parent, and **reparenting 82 scenery objects in the
shared multiplayer scene risks breaking path references** (venue/spawn/traffic systems). Per-tile modifiers
are guaranteed non-destructive to hierarchy and fully reversible.

- `NavMeshSurface` reconfigured: `agentTypeID = Pedestrian`, `collectObjects = All`,
  `useGeometry = RenderMeshes`, **`defaultArea = 1 (Not Walkable)`**, `layerMask = Everything`,
  `minRegionArea = 2`, `ignoreNavMeshAgent/Obstacle = true`.
- Added `NavMeshModifier(overrideArea=true, area=0 Walkable, applyToChildren=true)` to **each of the 82
  walkable tiles** (matched by name prefix `building_platform` / `grass_001` / `park_road`).
- Net effect: roads/asphalt/parking/water/buildings still contribute geometry (Collect=All → correct
  occlusion/height) but bake as NotWalkable → **excluded**; only whitelisted tiles bake walkable; props
  carve automatically (default NotWalkable).

#### 1.3 Bake
Via `Unity.AI.Navigation.Editor.NavMeshAssetManager.instance.StartBakingSurfaces()` (the same path as the
Navigation window's Bake button — it persists the data asset, unlike a bare `surface.BuildNavMesh()`).
- Result: **4324 verts / 1932 tris, 100% area 0 (Walkable)**, bounds `x[61.8..269.3] y≈23.2 z[86.8..281.1]`.
- Asset: `Assets/Scenes/WorldScene/NavMesh-NavMesh Surface 4.asset` (same asset, re-baked).

#### 1.4 Confinement verification
Programmatic `NavMesh.SamplePosition` at **every** tile center (1.5 m sample radius, AllAreas):

| Class | Tiles with navmesh at center | Expected |
|---|---|---|
| ROAD | **0 / 135** | 0 ✅ |
| ASPHALT | **0 / 40** | 0 ✅ |
| PARKING | **0 / 8** | 0 ✅ |
| WATER | **0 / 1** | 0 ✅ |
| WALK (sidewalk+park) | 76 / 82 | ≤100% (building-covered centers OK) |

**Zero non-walk violations.** The 6 "missing" walkable tiles have their *center* under a structure
(fountain_001, skyscraper_009, monument_001, supermarket_001, children_playground_001, ice_cream_shop_001) —
the surrounding pavement still bakes; correct, not a gap. Visual: blue-navmesh top-down screenshot confirms
navmesh only on sidewalks+park, roads/plazas/parking bare, building footprints carved, and walkable areas
are **disconnected islands** split by roads (expected — crosswalks are deferred to Appendix A).

#### 1.5 Recipe recorded
In the plan (Phase 1) + the re-runnable scripts in
[`Assets/Screenshots/npc-phase0-1-results.md`](../Assets/Screenshots/npc-phase0-1-results.md). **Repeatable
after scene edits:** re-run tile-marking (any new `building_platform*`/`grass_001*`/`park_road*` needs the
Walkable modifier) → re-bake the surface for the `Pedestrian` agent type → save the scene.

### Screenshots (in `Assets/Screenshots/`)
| File | Shows |
|---|---|
| `npc_phase0_topdown_raw.png` | Whole scene from above; city is a small central block, surrounded by non-walkable Terrain/water. |
| `npc_phase0_classification_map.png` | Tile classification overlay (cyan=sidewalk, magenta=asphalt/excluded, green=lawn, yellow=park path, red=roads, dark-red=parking). |
| `npc_phase1_navmesh_topdown.png` | Baked `Pedestrian` navmesh rendered blue — confinement proof. |

> The overlay/navmesh-visualization objects (`NPC_OVL*`, `NPC_NAVVIS`) were **temporary**, deleted after
> each screenshot; they are NOT in the saved scene.

### Verification
- Compile/console clean (only the transient `MCP-FOR-UNITY … WebSocket is not initialised` notice).
- `NavMesh.GetSettingsByIndex` confirms `[0] Humanoid`, `[1] Pedestrian (r0.3 h1.8 climb0.4 slope45)`.
- 82 Walkable modifiers added (added=82, updated=0); surface config read back as set; bake stats + sampling
  as above.
- `WorldScene.unity` saved.

### Decisions / notes
- **`asphalt_001` excluded** as plazas/lots (owner call) — strict "sidewalks + park only" rule.
- **Per-tile modifiers, not reparenting** — safest given the flat hierarchy in a shared scene (see 1.2).
- **28 legacy prop modifiers kept** — now redundant (default NotWalkable) but harmless; left to minimize churn.
- **Walkable = disconnected islands** separated by roads. An NPC can only wander/route within the island it
  spawns on until crosswalks (Appendix A) connect them. Phase 2/3 spawn + destination logic must respect
  per-island reachability.
- Did **not** touch git (scene saved, uncommitted).

### Gotchas worth remembering
- **`execute_code` runs CodeDom / C# 6 here** (Roslyn not installed): **no `using` directives** in the body,
  **no LINQ** — use fully-qualified types. (First attempt failed on `using`.)
- **Baking must go through `NavMeshAssetManager.StartBakingSurfaces`** to persist the asset; a bare
  `NavMeshSurface.BuildNavMesh()` builds in memory only.
- **No single `destroyWhenOwnerOrLastClientLeaves`** Normcore flag — two separate booleans (see 0.4).
- Any NPC prefab's `NavMeshAgent.agentTypeID` **must be `Pedestrian` (`-1372625422`)**, not Humanoid, or
  paths won't match the baked surface.

### Files touched
- **New:** `docs/npc-changelogs.md` (this file), `Assets/Screenshots/npc-phase0-1-results.md`,
  `Assets/Screenshots/npc_phase0_topdown_raw.png`, `npc_phase0_classification_map.png`,
  `npc_phase1_navmesh_topdown.png`
- **Modified:** `Assets/Scenes/WorldScene.unity` (82 NavMeshModifiers + surface config),
  `Assets/Scenes/WorldScene/NavMesh-NavMesh Surface 4.asset` (re-baked),
  `ProjectSettings/NavMeshAreas.asset` (new `Pedestrian` agent type), `docs/npc-pathing-plan.md`
  (Phase 0/1 checked off + recipe)
- **Not modified:** any script (Phase 1 is data-only; new `.cs` files begin in Phase 2)

### Loose ends / next
- **Phase 2** is next: `NpcAgent.prefab` (capsule + `NavMeshAgent` set to `Pedestrian`) + movement script
  (adapt `NpcNavigation.cs`) + avoidance tuning, single-client/un-networked first.
- `WorldScene.unity` is saved but **not committed** — owner to review/commit on `feat/npc-pathing`.
