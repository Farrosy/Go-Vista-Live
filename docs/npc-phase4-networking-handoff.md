# NPC Phase 4 — Networking Handoff (make the crowd multiplayer-compatible)

> **Purpose:** a self-contained brief so a fresh session / dev can take the single-client NPC crowd and make it
> **shared across all players in the Normcore room**, with one authority simulating and clean host migration.
> **Prereq state:** Phases 0–3 are DONE (see [`npc-pathing-plan.md`](./npc-pathing-plan.md) + [`npc-changelogs.md`](./npc-changelogs.md)).
> Movement is the **standard NavMeshAgent + built-in RVO** model (finalised 2026-11-09d). This phase adds networking
> **around** that movement with no change to how an NPC walks.
> **Cadence:** build in the task order below, compile-clean after each script (`read_console`, wait for
> `compilation.is_compiling == false`), and get owner go-ahead per the project's phase convention. Do not commit to git
> unless asked.

---

## 0. Orient first (read these before touching anything)

**The locked multiplayer model (from the plan, do not re-litigate):**
- NPCs are **shared** Normcore objects. **Exactly one client is the authority** = the client with the **lowest
  `clientID` among clients currently in the World venue**. Only it runs the `NavMeshAgent` + `NpcAgent` logic.
- Every other client (including World clients that aren't authority) **disables the agent** and lets
  `RealtimeTransform` drive the NPC transform; it derives the animation `Speed` from the synced value.
- **Authority migrates** when the current authority leaves World / disconnects: the new lowest-clientID World client
  `RequestOwnership()`s all NPCs and resumes simulation. NPCs hold their last synced pose in the gap.
- **Nobody in World → simulation pauses** (NPCs freeze; nobody can see them anyway — they're World-only). Resumes
  when a client returns to World.

**The code you will mirror (all verified present):**
| Need | Reference file | What to copy |
|---|---|---|
| `RealtimeComponent<TModel>` + source-gen model | `Assets/Script/Multiplayer/PlayerAnimSync.cs` + `PlayerAnimSyncModel.cs` | synced `Speed` (unreliable float), owner-writes / remote-reads split via `isOwnedLocallyInHierarchy` |
| Networked instantiate + ownership flags | `Assets/Script/Multiplayer/PlayerManager.cs` (`SpawnLocalPlayer`, line ~100) | `Realtime.Instantiate(prefabName, pos, rot, InstantiateOptions{…})`; prefab resolved **by name from Resources** |
| Venue sync + "am I this venue?" | `Assets/Script/Travel/PlayerVenueSync.cs` + `PlayerVenueModel.cs` | `[RealtimeModel]`, `OnRealtimeModelReplaced`, model-ready null-guards |
| Local venue state + change event | `Assets/Script/Travel/VenueTravelManager.cs` | `VenueTravelManager.LocalVenue` (static), `VenueTravelManager.VenueChanged` (static event), `VenueId.World` |
| Per-venue hide/show | `Assets/Script/Travel/VenueVisibilityFilter.cs` | toggle renderers+colliders on venue change |
| Static registry pattern | `Assets/Script/PlayerScript/PlayerSpawnPoint.cs` | OnEnable/OnDisable list (already used by `NpcAgent` historically) |

**Key facts about this project's Normcore wiring:**
- `Realtime` lives on an object in **`MainMultiplayerScene`** (with `PlayerManager` + `VenueTravelManager`). That scene
  is **never unloaded**. `WorldScene` is loaded **additively** and **unloaded when a client travels into a venue**.
- Normcore models are **source-generated** from `[RealtimeModel] partial class` — no "Compile Model" button needed.
- A `RealtimeComponent` on a prefab must be **registered in that prefab's `RealtimeView._components` list** (same as
  `PlayerAnimSync` / `RealtimeTransform` are on the avatar prefabs). Adding one is additive; keep existing order.
- Ownership check inside a `RealtimeComponent`: **`isOwnedLocallyInHierarchy`** (and it is only valid once `model != null`
  — read venue/ownership only after the model exists, see `PlayerVenueSync.IsLocalAvatar`).
- `Realtime.clientID` = this client's id. A `RealtimeView`'s owner id is **`ownerIDInHierarchy`** *(verify the exact
  member name against the installed Normcore version before relying on it)*.

**Normcore InstantiateOptions for NPCs (from Phase 0.4 — these differ from avatars on purpose):**
```csharp
new Realtime.InstantiateOptions {
    ownedByClient               = true,   // the elected authority owns it
    preventOwnershipTakeover    = false,  // next authority can RequestOwnership() on migration
    destroyWhenOwnerLeaves      = false,  // NPC survives when the authority disconnects / leaves World
    destroyWhenLastClientLeaves = true,   // clean up the roster when the room empties
    useInstance                 = realtime
};
```
*(Avatars use `preventOwnershipTakeover = true` — the opposite. There is NO single `destroyWhenOwnerOrLastClientLeaves`
flag; it's two booleans. Verify all four behaviourally in the migration test.)*

---

## ⚠️ 1. The three gotchas that will bite if ignored

1. **NPC GameObjects must survive `WorldScene` unloading.** A client in a venue has no WorldScene. If NPC GameObjects
   live in WorldScene, they're destroyed on that client when it travels → Normcore desync / ownership churn.
   **→ Instantiate NPCs into `MainMultiplayerScene` (persistent), not WorldScene** (move with
   `SceneManager.MoveGameObjectToScene` right after `Realtime.Instantiate` if needed). The baked NavMesh is **global**
   (works as long as WorldScene is loaded, regardless of which scene the agent object sits in), and the authority is
   always in World, so the agent still paths fine.
2. **`NavMeshAgent` and `RealtimeTransform` fight over the transform.** Only the authority may run the agent.
   **→ On non-authority clients: `navMeshAgent.enabled = false` and `npcAgent.enabled = false`; let RealtimeTransform
   drive.** When a client *becomes* authority: ensure the NPC is on the navmesh (`NavMesh.SamplePosition` + `agent.Warp`)
   **before** `navMeshAgent.enabled = true`, then re-enable `NpcAgent`.
3. **Read ownership/model state only after the model exists.** `isOwnedLocallyInHierarchy` throws / lies before
   `OnRealtimeModelReplaced`. Guard every authority check with `model != null` (mirror `PlayerVenueSync.IsLocalAvatar`).

---

## 2. Build order (each item is a discrete, checkable task)

### 4.1 — `NpcSyncModel.cs` + `NpcSync.cs` (synced speed for remote animation)
- **`Assets/Script/NPC/NpcSyncModel.cs`** — mirror `PlayerAnimSyncModel`:
  ```csharp
  [RealtimeModel]
  public partial class NpcSyncModel {
      [RealtimeProperty(1, false, false)] private float _speed; // unreliable: latest value only
  }
  ```
- **`Assets/Script/NPC/NpcSync.cs : RealtimeComponent<NpcSyncModel>`** — mirror `PlayerAnimSync`:
  - If `isOwnedLocallyInHierarchy` (authority): write `model.speed = agent.velocity.magnitude` (with an epsilon like
    `PlayerAnimSync.SpeedEpsilon` so idle NPCs send nothing).
  - Else (remote): drive the animator `Speed` from `model.speed` (this is the hook the Phase-3d animation plan needs —
    remotes animate without simulating). On the capsule prototype this is a no-op; keep the wiring.
  - Keep heavy per-NPC state (path, wander target) **local to the authority** — do not sync it.

### 4.2 — `NpcAgent.cs` authority gating
Add a small networked gate; keep all existing movement untouched.
- Add a cached `NavMeshAgent` + optional `NpcSync`/`RealtimeView` references.
- Define `bool ShouldSimulate = ownedLocally && VenueTravelManager.LocalVenue == VenueId.World` (ownedLocally via the
  `RealtimeView`/`NpcSync` once its model is ready; default **false** until known).
- When `ShouldSimulate` flips **true**: `SamplePosition`+`Warp` onto the navmesh, `navMeshAgent.enabled = true`, resume
  `Update` logic (pick targets etc.).
- When **false**: `navMeshAgent.enabled = false`; **do not** `SetDestination` or set velocity; let `RealtimeTransform`
  move the transform. (Simplest: early-return from `Update`'s movement logic when `!ShouldSimulate`.)
- Re-evaluate on `VenueTravelManager.VenueChanged` and on ownership change (`OnRealtimeModelReplaced` / a RealtimeView
  ownership callback — verify the exact callback name).
- **Single-client path must still work:** when there is no `Realtime` / no `RealtimeView` on the object (DevScene /
  `NpcDevSpawner`), treat `ShouldSimulate = true` so the current single-client behaviour is unchanged. Gate the
  networking code behind "is there a RealtimeView and is its model ready".

### 4.3 — `NpcAgent.prefab` networking components
- Add **`RealtimeView`**, **`RealtimeTransform`**, **`NpcSync`** to `Assets/Resources/NpcAgent.prefab`.
- **Register `RealtimeTransform` and `NpcSync` in `RealtimeView._components`** (same as the avatar prefab wires
  `RealtimeTransform` + `PlayerAnimSync`). Order is additive; don't reorder existing entries.
- Keep the prefab in **`Resources/`** (it already is) so `Realtime.Instantiate("NpcAgent", …)` resolves by name.
- Leave the `NavMeshAgent` + capsule as-is. (Prototype note: `RealtimeTransform` syncs position+rotation+scale;
  rotation sync means remotes see the authority's facing — good for the forward-walk animation plan.)

### 4.4 — `NpcDirector.cs` (+ `NpcDirectorModel.cs`) — roster + authority + migration
Networked singleton on a persistent object in **`MainMultiplayerScene`** (next to `PlayerManager`/`VenueTravelManager`).
- **`NpcDirectorModel`**: a synced "roster spawned" guard so only one client ever instantiates the crowd, e.g.
  `[RealtimeProperty(1, true, true)] private bool _rosterSpawned;` (or an int count). Reliable + change callback.
- **Authority election** (compute locally, no per-frame spam — re-run on `VenueTravelManager.VenueChanged`, on room
  connect, and when the avatar set changes):
  - `int myId = realtime.clientID;`
  - Gather all avatars in the room (they're `RealtimeView`s present on every client — find `PlayerVenueSync`
    components). For each, read its **venue** (`PlayerVenueSync.Venue`) and its **owner clientID**
    (`realtimeView.ownerIDInHierarchy` — verify name). `authorityId = min(ownerId where venue == World)`.
  - `bool iAmAuthority = (myId == authorityId) && LocalVenue == World`.
  - *(Alternative if reading other avatars' owner ids is awkward: give the director its own small RealtimeModel map of
    `clientId → inWorld` that each client writes for itself; then authority = min key with true. Pick whichever is
    cleaner against the installed Normcore API — the avatar-scan avoids a second model.)*
- **On becoming authority:**
  - If `!model.rosterSpawned`: `Realtime.Instantiate("NpcAgent", spawnPos, rot, npcOptions)` **N times** at Phase-0.5
    spawn points (random walkable `NavMesh.SamplePosition`, golden-angle spread like `PlayerSpawnPoint.GetPosition`),
    move each into `MainMultiplayerScene`, then set `model.rosterSpawned = true`. **Guard against double-spawn** — only
    the elected authority instantiates, and the synced flag blocks a second run during a migration race.
  - Else (roster already exists): `RequestOwnership()` each NPC's `RealtimeView` (find them via a static registry on
    `NpcSync`/`NpcAgent`, mirroring `PlayerSpawnPoint`), which flips their `ShouldSimulate` true and resumes agents.
- **On losing authority / leaving World:** stop simulating (ownership will move to the next authority when it requests).
  Do nothing destructive — NPCs persist (`destroyWhenOwnerLeaves = false`).
- **Expose `N` as a serialized tunable** (see the N risk in §4).

### 4.5 — `NpcVenueVisibility.cs` — hide NPCs from players inside a venue
NPCs are **World-only**. Mirror `VenueVisibilityFilter` but with a constant venue:
- Cache renderers+colliders; on `VenueTravelManager.VenueChanged` (and `OnEnable`), set
  `visible = (VenueTravelManager.LocalVenue == VenueId.World)` and toggle them.
- Add it to the `NpcAgent.prefab`. (This is independent of authority gating — it's about *this* client's venue, not
  ownership.)

### 4.6 — Neutralise the dev spawner in the networked flow
`NpcDevSpawner` already **stands down when a `Realtime` exists** (it does `FindAnyObjectByType<Realtime>()` and bails).
In the real flow `Realtime` is in `MainMultiplayerScene` → spawner is inert. **Keep it for single-client DevScene
testing**; it can be deleted once the director is trusted. Confirm it does NOT create a per-client ghost crowd in the
MainMultiplayerScene + WorldScene additive flow (it should bail — verify in the two-client test).

---

## 3. Verification (Phase 6.3 / 6.4 — do these live via Unity MCP, two editor instances or editor+build)

- **Two clients, both in World:** both see the **same NPCs at the same positions** (within interpolation); **no
  duplicate/ghost roster** (exactly N RealtimeViews of NpcAgent in the room). Only the lowest-clientID World client's
  agents are enabled; the other client's NPC agents are disabled and driven by `RealtimeTransform`.
- **Authority migration:** the authority client leaves World (travel to a venue) or disconnects → the other World
  client takes ownership and NPCs **resume within a frame or two**, **no duplication**, **no permanent freeze**.
- **Venue visibility:** a client inside DanceClub/RooftopBar does **not** see/collide with the World NPCs; returning to
  World, the crowd is visible again.
- **Nobody in World:** all clients in venues → NPCs **freeze** (hold last pose), no errors; a client returning to World
  resumes simulation (and becomes authority if lowest id).
- **No regression single-client:** open WorldScene standalone + Play → `NpcDevSpawner` still produces the crowd (networking
  code is bypassed when there's no RealtimeView), behaviour identical to Phase 3d.
- Compile clean + 0 console errors after each script.

---

## 4. Risks / open questions to confirm with the owner before/while building

1. **N for the networked prototype (bandwidth).** 120 NPCs = 120 `RealtimeView`s + `RealtimeTransform`s syncing every
   update — potentially heavy. The original Phase-0.5 draft was **N ≈ 20–30** for networked. **Recommend starting the
   networked roster at ~20–30** (expose `NpcDirector.N`), confirm bandwidth/perf, then scale in Phase 5. The 120 "packed"
   figure was chosen for single-client *feel*; it is an owner decision whether networked density should match. **Ask.**
2. **RealtimeTransform send rate** for 100+ transforms may need throttling (lower rate, or only send when moving). Phase-5
   tuning; note it.
3. **Authority-device cost:** one client simulates all agents + all RVO. Watch the weakest likely-authority device
   (Phase 5). Avoidance quality is already a per-agent tunable (`obstacleAvoidance`).
4. **Exact Normcore API member names** (`ownerIDInHierarchy`, the RealtimeView ownership-changed callback,
   `RequestOwnership` signature) must be verified against the installed `Normal.Realtime` version — don't assume.
5. **Spawn-into-persistent-scene mechanics:** confirm where `Realtime.Instantiate` places the object by default in this
   version and whether `MoveGameObjectToScene` is needed / safe for a networked object (test that a venue-travelling
   client doesn't destroy its NPC copies).

---

## 5. New files (targets)
- `Assets/Script/NPC/NpcSync.cs` + `NpcSyncModel.cs` — RealtimeComponent + synced Speed.
- `Assets/Script/NPC/NpcDirector.cs` + `NpcDirectorModel.cs` — roster/authority/migration singleton (MainMultiplayerScene).
- `Assets/Script/NPC/NpcVenueVisibility.cs` — World-only visibility (mirror `VenueVisibilityFilter`).
- **Edits:** `Assets/Script/NPC/NpcAgent.cs` (authority gating), `Assets/Resources/NpcAgent.prefab` (RealtimeView +
  RealtimeTransform + NpcSync + NpcVenueVisibility, registered in `_components`), `MainMultiplayerScene` (add
  `NpcDirector`). Reuse `VenueTravelManager`, `VenueId`, `PlayerSpawnPoint` (pattern), `PlayerAnimSync`/`PlayerVenueSync`
  (patterns) as-is.

---

## 6. Suggested first move for whoever picks this up
1. Read the three files in §0's table (`PlayerAnimSync(+Model)`, `PlayerManager.SpawnLocalPlayer`, `PlayerVenueSync`).
2. Build **4.1 + 4.3 + 4.2** first (NpcSync + prefab wiring + authority gate) and test with **one** client in the full
   MainMultiplayerScene flow: the crowd should spawn via… *(nothing yet — 4.1–4.3 alone won't spawn; use a temporary
   direct `Realtime.Instantiate` or a stub director to get ONE networked NPC moving and confirm a remote client sees it)*.
3. Then build **4.4** (director/election/migration) and **4.5** (visibility), then run the full §3 two-client matrix.
4. Confirm **N** with the owner (risk #1) before cranking the roster up.
