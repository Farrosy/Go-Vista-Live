# NPC Capsule → Finished Character Migration — Reference

> **Purpose:** turn the prototype **capsule** crowd (`NpcAgent.prefab`) into the real **animated
> characters** that `main` brought in (Kloe / Marie / Sallyy / Yavi / ZibZib), with **zero changes to
> the pathing / avoidance / networking logic**. The capsule was always a stand-in; this is the art swap
> the plan promised ([`npc-pathing-plan.md`](./npc-pathing-plan.md) §0, §5.3).
> **Status:** **RANDOM 4-WAY CROWD INTEGRATED & single-client VERIFIED (2026-11-10).** The crowd now
> spawns a random mix of **Jane / Sallyy / Fany / Chloe Mentor** (see §4-AS-BUILT). Live play-test: 120/120
> on the Pedestrian navmesh, even spread (Jane 36 / Chloe 30 / Sallyy 27 / Fany 27), ~111 walking (rest in
> dwell), correct scale/grounding/materials, **0 errors**. `NpcDevSpawner` is now in WorldScene (saved) and
> the `RealtimeView`-null dev spam is fixed. The earlier Jane-only single-prefab swap (capsule mesh removed,
> root scale 1, baseOffset 0, collider r0.3/h1.7, root motion off) is the per-variant recipe below.
> **Audience:** implementing agent/dev doing the swap in the Unity Editor.
> **Last updated:** 2026-11-10.
>
> **Two integration notes from the live test:**
> - **WorldScene has no `NpcDevSpawner`** — the post-merge scene is `main`'s, which never had it. To
>   single-client test, add a GameObject with `NpcDevSpawner` (Editor-only, stands down under Realtime)
>   and set its `count`; it is **not** currently saved into the scene.
> - **`main`'s dialogue NPCs throw** "Failed to create agent… not close enough to the NavMesh" /
>   "[NpcNavigation] NPC tidak berada di atas NavMesh". Those placed `NPC_*` characters use the **Humanoid**
>   agent type (id 0), which isn't baked on this scene (only **Pedestrian** is). Separate pre-existing issue
>   from the crowd — flag to the owner (bake a Humanoid surface, or move those NPCs onto the Pedestrian type).

---

## 0. TL;DR (the whole swap in six moves)

The movement + networking scripts already drive an Animator `Speed` float (see §2). The character
controller already **exposes** that exact param. So the swap is almost entirely prefab wiring:

1. Parent a character model (e.g. `Jane.prefab`, from the owner's set Sally/Fany/Chloe Mentor/Jane) **under** the `NpcAgent` prefab root as a child.
2. Delete the capsule's `MeshFilter` + `MeshRenderer` (keep the root, its scripts, the `CapsuleCollider`).
3. Reset the **root `Transform` scale to (1,1,1)** — the capsule root is squashed to `(0.6,0.9,0.6)`; a real model must not inherit that.
4. On the root `NavMeshAgent`: set **`Base Offset = 0`** (was `0.9` for the center-pivot capsule; the model pivot is at the feet).
5. On the character model's `Animator`: **root motion OFF** (the NavMeshAgent drives position — root motion would fight it and foot-slide).
6. Keep the prefab in **`Assets/Resources/`** under the name the spawner/director loads (default `NpcAgent`). Done — `NpcAgent.cs` and `NpcSync.cs` auto-find the child Animator and feed `Speed`.

No C# changes are required for the basic swap. Everything below is the detail, the gotchas, and the test.

---

## 1. Where everything lives

### 1a. The capsule crowd (the system being re-skinned — do NOT rewrite its logic)
| File | Role |
|---|---|
| `Assets/Resources/NpcAgent.prefab` | The crowd agent prefab. Capsule mesh + `CapsuleCollider` + `NavMeshAgent` (**Pedestrian** type) + `NpcAgent` + `RealtimeView` + `RealtimeTransform` + `NpcSync` + `NpcVenueVisibility`. **Must stay in `Resources/`** so `Realtime.Instantiate("NpcAgent")` resolves it. |
| `Assets/Script/NPC/NpcAgent.cs` | Dynamic RVO wander (authority-side). Auto-finds `Animator` in children, feeds `Speed` float (`agent.velocity.magnitude`). Animation hook is **already wired** (§2). |
| `Assets/Script/NPC/NpcSync.cs` (+`NpcSyncModel.cs`) | `RealtimeComponent` that syncs `Speed`. On **remote** clients (agent disabled) it drives the child Animator's `Speed` from the networked value. Also auto-finds `Animator` in children. |
| `Assets/Script/NPC/NpcDirector.cs` (+`NpcDirectorModel.cs`) | Networked authority election + roster spawn/migration. Instantiates the crowd prefab by **Resources name**. |
| `Assets/Script/NPC/NpcVenueVisibility.cs` | Hides World crowd from players inside venues. |
| `Assets/Script/NPC/NpcDevSpawner.cs` | **Single-client, un-networked** dev crowd (Editor-only; stands down when Normcore `Realtime` is present). This is the **no-multiplayer test harness** (§5). Default `count = 120`, loads `"NpcAgent"` from Resources. |

Agent facts baked into `NpcAgent.prefab` / set by `NpcAgent.Awake`:
- **Agent type `Pedestrian`**, `agentTypeID = -1372625422` — this is the confined **sidewalks + park** navmesh. **Keep it.** The dialogue NPCs use `Humanoid` (id 0), which is a *different* bake; do not swap the crowd onto it.
- `radius 0.3`, `height 1.8` (set in `Awake`), `baseOffset 0.9` (prefab — change to 0, §3), `GoodQualityObstacleAvoidance` (RVO), per-NPC `avoidancePriority 20–80`, `updateRotation = true` (agent faces its velocity → a forward walk clip matches with no foot-slide).

### 1b. The finished characters (the art coming in from `main`)
> **Owner-selected roster (2026-11-10): use ONLY Sally, Fany, Chloe Mentor, Jane** for the crowd for now.
> These are the *newer* characters from `origin/main` PR #87 ("feat/npc-character"). Your branch's earlier
> merge (`509b0af7`) merged an **older** main and did NOT include them — **Fany / Jane / Chloe Mentor were
> pulled from `origin/main` on 2026-11-10** (`git checkout origin/main -- …`); **Sally** was already in the
> tree. (The branch's own older set — Kloe, Marie, Yavi, ZibZib — is NOT what the owner wants here.)

Each name has a **base model prefab** (parent this under the crowd root) and an **`NPC_` dialogue variant**:

| Character | Base model (use this child) | Dialogue variant (reference only) |
|---|---|---|
| **Sally** | `Models/Npc/Sally/Sally.prefab` (note: different folder) — or the similar `NPC/Sallyy/Sallyy.prefab` | `NPC/Sallyy/NPC_Sallyy.prefab` |
| **Fany**  | `NPC/Fany/Fany.prefab`   | `NPC/Fany/NPC_Fany.prefab` |
| **Chloe Mentor** | `NPC/Chloe Mentor/Chloe Mentor.prefab` | `NPC/Chloe Mentor/NPC_Chloe Mentor.prefab` |
| **Jane**  | `NPC/Jane/Jane.prefab`   | `NPC/Jane/NPC_Jane.prefab` |

*(`origin/main` also carries Kiori, Lany, Maya — not pulled, not in scope per the owner.)* All four use the
same `NpcHumanoidController` and are self-contained BoZo models (mesh/materials/textures in-folder).

- The **base** `*.prefab` is just the BoZo humanoid model: skeleton + `SkinnedMeshRenderer`s + outfits + an **`Animator` using `NpcHumanoidController`** (guid `bf8e3f3877094764480d63477176afaf`). Pivot at the feet, scale 1. **This is what you parent under the crowd root.**
- The **`NPC_` variant** is the *dialogue* NPC (single-player): it adds `Interactable` + `DialogueTrigger` + `NpcController` + its own `NavMeshAgent` (type **Humanoid**, baseOffset 0, height 1.7) + `NpcNavigation` + `NpcObstacleAvoidance` + `NpcGroundSnap`. **Do NOT use the `NPC_` variant as the crowd body** — it carries a second navmesh agent + a whole dialogue stack that would fight the crowd scripts. Use the plain base model.

### 1c. The animation that already matches
`Assets/Characters/Animation/AnimationController/NpcHumanoidController.controller` — states **Idle / Walk / Talk**, parameters:
- **`Speed`** (float): `Idle → Walk` when `Speed > 0.1`, `Walk → Idle` when `Speed < 0.1` (0.15s transitions). *This is exactly what the crowd scripts feed.*
- **`IsTalking`** (bool): any → `Talk`. The crowd never sets it (defaults false) → crowd NPCs just Idle/Walk. Harmless.

Idle & Walk clips (from `main`): `Assets/Characters/Animation/NpcAnimations/` (`IdleFoldarms`, `IdleTalk`) and the walk/idle FBX under `Assets/Characters/Animation/{Male,Female}ChibiAnimation/` and `Assets/Characters/Models/Npc/`. The controller already references them — you don't need to re-assign clips for the basic swap.

> There is **no blend tree** — it's a discrete Idle↔Walk switch at `Speed = 0.1`. That's fine for a
> walk-only crowd. If you later add a run, add a blend tree keyed on the same `Speed` and match clip
> speed to `agent.speed` (the per-NPC `speedRange` is 0.9–1.6 m/s).

---

## 2. Why no code change is needed (the hook that's already there)

Both crowd scripts find the Animator the same way and drive the same param:

- `NpcAgent.cs:129` — `if (!animator) animator = GetComponentInChildren<Animator>();`
- `NpcAgent.cs:332` — `UpdateAnimation()` → `animator.SetFloat("Speed", agent.velocity.magnitude, 0.1, dt)` (authority / single-client).
- `NpcSync.cs:44` — `animator = GetComponentInChildren<Animator>();`
- `NpcSync.cs:69` — remote clients drive `Speed` from the **synced** value (agent is disabled there).

Both check the Animator actually has a float `Speed` before writing (`hasSpeedParam`). The character's
`NpcHumanoidController` has it → the hooks light up the moment a model with that Animator is a child.
The `animator` field on `NpcAgent` is serialized (`{fileID: 0}` = empty = auto-find); you can leave it
empty or explicitly drag the child Animator in. Either works.

---

## 3. Exact prefab wiring (do this in the Editor)

Work on `Assets/Resources/NpcAgent.prefab` (or a **variant** of it — see §4 for one-vs-many).

1. **Add the model as a child.** Drag a base model (e.g. `NPC/Jane/Jane.prefab`) into the prefab root. Child transform:
   `localPosition (0,0,0)`, `localRotation identity`, `localScale (1,1,1)`.
2. **Remove the capsule visual.** Delete the root `MeshFilter` and `MeshRenderer`. **Keep** the root
   `CapsuleCollider` (physics footprint for the player's carving obstacle to push against), but note its
   current radius/height (0.5/2 before root-scaling). After step 3 the root scale changes, so re-check
   the collider reads ~`radius 0.3`, `height 1.8` in world space — adjust `CapsuleCollider` center/size
   if needed so it hugs the character.
3. **Root scale → (1,1,1).** The capsule root is `(0.6,0.9,0.6)` to shrink a Unity primitive capsule.
   A real model is authored at scale 1 — leaving the squash on the root deforms the character **and**
   scales the NavMeshAgent. Set root `localScale = (1,1,1)`.
4. **NavMeshAgent → `Base Offset = 0`.** The capsule pivot was its center (offset 0.9). The model pivot
   is the feet, so 0 sits it on the mesh. (Leave `Agent Type = Pedestrian`, radius/height as-is — code
   re-asserts radius 0.3 / height 1.8 in `Awake`.)
5. **Animator → root motion OFF.** Select the model child's `Animator`, uncheck **Apply Root Motion**.
   The NavMeshAgent owns position; root motion would double-drive it.
6. **Keep it Resources-reachable.** The file must remain `Assets/Resources/NpcAgent.prefab` (or your
   variant must live in `Resources/` too) or `Realtime.Instantiate` / `Resources.Load` won't find it.
7. **Leave the networking components alone.** `RealtimeView._components` already registers `NpcSync` (and
   the transform). Adding a child model does **not** touch that list. Do not reorder/add networked
   components without reading the handoff
   ([`npc-phase4-networking-handoff.md`](./npc-phase4-networking-handoff.md)) — `_components` ordering is
   load-bearing and, in scenes, doesn't auto-register.

### Optional polish
- **Feet not napping to uneven pavement?** The dialogue system's `NpcGroundSnap.cs` raycasts to the
  ground collider and nudges `agent.baseOffset` each `LateUpdate`. You *can* add it to the crowd body,
  but it **writes `baseOffset` every frame**, so set the agent's baseOffset starting point to 0 and let
  it correct from there. Treat as a later nicety, not required for the first working swap.
- **Idle variety / bob:** the controller's single Idle is fine; add variety later via the blend/extra
  states, not via code.

---

## 4-AS-BUILT. Random four-way crowd (shipped 2026-11-10)

Chosen approach = **option (B): four Resources prefab *variants*, picked at random per NPC.** This is the
networking-correct choice: Normcore puts the chosen prefab name in the `Realtime.Instantiate` message, so
**every client spawns the same character for a given NPC with zero extra synced state** (no change to
`NpcSyncModel`), and migration stays prefab-agnostic.

**As built:**
- `Assets/Resources/NpcAgent.prefab` is now a **logic-only base** (no model child; the capsule mesh and the
  Jane child were removed). It is never spawned directly.
- Four **variants** of it, each adding one character model child (root motion off, `NpcHumanoidController`,
  full inherited networked stack, Pedestrian agent, baseOffset 0):
  `Assets/Resources/NpcAgent_Jane.prefab`, `NpcAgent_Sallyy.prefab`, `NpcAgent_Fany.prefab`, `NpcAgent_Chloe.prefab`.
- **`NpcDevSpawner`** (single-client): new `GameObject[] npcPrefabs` list; empty ⇒ auto-loads the default
  4-way set from Resources by name; picks a random one per NPC. Also now **disables
  `RealtimeView`/`RealtimeTransform`/`NpcSync`** on each dev-spawned NPC (they have no Normcore model in the
  single-client flow and would otherwise spam "RealtimeView model is null"). It is now **saved into
  WorldScene** as `NpcDevSpawner (DEV)` (count 120).
- **`NpcDirector`** (networked): `npcPrefabName` (single) → `string[] npcPrefabNames`; empty ⇒ the same
  default 4-way set; picks a random name per NPC in `SpawnRoster`. The pre-existing MainMultiplayerScene
  instance needs **no re-wiring** — its empty array falls back to the default set.
- Both scripts share the canonical list: `{ "NpcAgent_Jane", "NpcAgent_Sallyy", "NpcAgent_Fany", "NpcAgent_Chloe" }`.

**Character note:** "Sally" uses **`NPC/Sallyy/Sallyy.prefab`** (has `NpcHumanoidController` → the `Speed`
hook). The other `Models/Npc/Sally/Sally.prefab` uses the BoZo *CharacterCreator* controller (blend trees,
no `Speed` param) and would need its Animator reassigned to `NpcHumanoidController` to join the crowd.

**To add/remove a character:** make another `NpcAgent_<Name>` variant (§3 recipe) and add its name to the
two `DefaultPrefabNames` arrays (or assign the inspector lists). No other change.

**Crowd pedestrians carry NO dialogue** (per owner): the variants use the **base** character models, not the
`NPC_*` dialogue variants, so there is no `DialogueTrigger`/`NpcController`/name on the crowd. The dialogue
NPC system is untouched.

---

## 4-HISTORICAL. One prefab, or one per character? (decision record)

The crowd originally spawned a **single** Resources prefab (`NpcAgent`). Options that were weighed:

- **(A) Start with ONE character** (recommended for the first pass / test): swap one of the owner's set
  (e.g. `Jane`) into `NpcAgent.prefab` as above. Whole crowd becomes Jane. Proves the pipeline end-to-end
  with the least surface area. *Do this first.*
- **(B) Per-character prefab variants, random pick:** make `NpcAgent_Sally`, `NpcAgent_Fany`,
  `NpcAgent_Chloe`, `NpcAgent_Jane` …
  variants (each = the base NpcAgent prefab with a different model child), all in `Resources/`. Then:
  - Single-client: point `NpcDevSpawner.npcPrefab` at a list / pick randomly (small edit to the spawner,
    or add a few spawners).
  - Networked: `NpcDirector` must instantiate a **mix** by name. This is a real (small) code change —
    the director currently spawns one name. Scope it as a follow-up; see the handoff doc for where the
    instantiate call lives. **Each distinct networked prefab must be registered/known to Normcore by
    its Resources name.**
- **(C) One prefab, swap child model at runtime:** heavier and fights Normcore (the model must be
  identical across clients for a given NPC), so **not recommended** for the networked path.

**Recommendation:** ship (A), confirm it looks right in play mode, then decide if (B) is worth the
director change for visual variety.

---

## 5. Testing WITHOUT multiplayer (single client)

This is what the owner asked for ("test them without multiplayer"). The harness already exists:

1. Open **`Assets/Scenes/WorldScene.unity`** *by itself* (not under `MainMultiplayerScene`). The merge
   brought in `main`'s WorldScene; the Pedestrian navmesh may need a **re-bake** first — see the ⚠️
   caveat below.
2. Ensure a GameObject with **`NpcDevSpawner`** is in the scene (it's Editor-only and auto-stands-down
   when Normcore `Realtime` is present, so it's safe to leave in). Lower `count` to ~**15–25** for a
   first look so you can see individual characters clearly; raise back to 120 for the stress view.
3. Press **Play**. Expected:
   - Characters spawn only on sidewalks + park, roam on their own, face their travel direction.
   - They **animate**: standing = Idle, moving = Walk (because `Speed` crosses 0.1). If they **T-pose or
     slide**, the Animator isn't being found or root motion is on — recheck §3 steps 1/5 and that the
     model child's Animator has `NpcHumanoidController`.
   - They weave around each other (RVO), none permanently stuck.
4. `NpcSync`'s remote branch is **inert** with no Realtime, so single-client exercises exactly the
   authority/offline path — correct for this test.

> ⚠️ **Re-bake the Pedestrian navmesh after the merge before trusting the test.** The merge took
> `main`'s `WorldScene.unity`; the branch's baked confinement was preserved separately as
> `WorldScene_DUPE`. Until WorldScene is confirmed to carry the **Pedestrian** bake (sidewalks + park,
> agent `-1372625422`), NPCs may land on a Humanoid/all-walkable mesh or nowhere. Confirm/re-bake per
> [`npc-pathing-plan.md`](./npc-pathing-plan.md) Phase 1 (and the open integration TODO in memory
> `npc-branch-main-merge`). If NPCs spawn on roads or fail to spawn, this is almost always why.

---

## 6. Gotchas checklist (the things that actually break)

- [ ] **Agent type stays `Pedestrian`** (`-1372625422`), not Humanoid. Wrong type = wrong navmesh = NPCs on roads or unable to path.
- [ ] **Root scale reset to (1,1,1)** — else the character is squashed and the agent mis-sized.
- [ ] **`Base Offset = 0`** — else the character floats ~0.9 m or sinks.
- [ ] **Root motion OFF** on the model Animator — else foot-sliding / fighting the agent.
- [ ] **Use the base `Kloe.prefab`, not `NPC_Kloe.prefab`** — the `NPC_` variant has a second NavMeshAgent + dialogue stack.
- [ ] **Prefab stays in `Resources/`** under the name the director/spawner loads.
- [ ] **Don't disturb `RealtimeView._components`** ordering/registration (handoff doc).
- [ ] **Pedestrian navmesh re-baked** on the post-merge WorldScene.
- [ ] **Materials render** (URP) — BoZo chars use their own materials; watch for pink/magenta = shader not found.
- [ ] Per-character variants (if doing §4-B) each need a Resources name the **networked** director knows.

---

## 7. What this does NOT change

- `NpcAgent.cs`, `NpcSync.cs`, `NpcDirector.cs`, `NpcVenueVisibility.cs` logic — untouched (basic swap).
- The RVO movement/avoidance tuning — untouched (the owner finalized it 2026-11-09d).
- The authority / migration model (Phase 4) — untouched; it's already built, compile-clean, pending the
  two-client live test (memory `npc-pathing-plan`).
- The dialogue NPC system (`NPC_*` prefabs, `NpcNavigation`, `NpcController`, `DialogueTrigger`) — that
  is a **separate**, single-player system from `main`; the crowd does not use it. Left as-is.

---

## 8. Open questions for the owner
1. **One character or all five** for the crowd (§4)? Start with Kloe (A) unless you want variety now (B).
2. **Variety mechanism** if (B): per-character Resources variants + a small `NpcDirector` change to spawn a mix — OK to scope that follow-up?
3. **Collider footprint:** keep the root `CapsuleCollider` sized to the character, or drop to the model's own body collider? (Default: keep root capsule, size to char.)
4. **Live test environment:** the Unity MCP bridge was **down** (`ECONNREFUSED`) at write time, so this swap is **not yet play-tested**. Bring the bridge up (`:8080`, per memory `unity-mcp-bridge-setup`) for an agent to wire + play-test, or do the wiring by hand from §3.
