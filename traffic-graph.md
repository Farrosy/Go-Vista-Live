# Demo 1 — Graph Traffic: 40 Cars, Turns, U-Turns

## Goal

Replace the current straight-route traffic in
`Assets/Download_Assets/Houidisoft technology/Simple water/Demo/Demo 1.unity`
with a lane graph that lets **40 cars** drive dynamically: go straight, turn left/right at
intersections, loop around the road network, and U-turn at dead ends instead of disappearing.

This plan is written for multiple agents. Every task is mapped to concrete files below.

## Confirmed decisions

- **40 cars**.
- **Occupancy lock at intersections**: only one car crosses an intersection box at a time; other
  cars wait at the entry. No invisible traffic lights.
- **U-turn + avoid when possible**: cars prefer routes that continue through the network; if they
  end up on a dead-end branch they U-turn onto the opposite lane. No car is hidden or teleported
  to despawn.

## Read these first (current implementation)

| File | Current role | Notes for agents |
|---|---|---|
| `Assets/Script/Traffic/TrafficNetwork.cs` | Builds flat open routes from `RoadSection` splines, selects routes, spawns cars, adds road colliders, logs, gizmos | Key methods: `Start`, `AddRoadColliders`, `SelectRoutes`, `SpawnCars`, `SpawnCar`, `LogReport`, `OnDrawGizmos`, `BuildRoutes`, `BuildAndDescribe`, `Deduplicate`, `BuildChains`, `AddRoute`; private structs `LaneSegment`, `LinkCandidate`, `SegmentKey` |
| `Assets/Script/Traffic/TrafficCar.cs` | Follows one `TrafficRoute`, car-ahead spacing, player yield, wheel spin, hides/wraps at route end | Key methods: `Initialize`, `FixedUpdate`, `TryFindPlayerAhead`, `EvaluateTransform`, `Hide`, `SetVisible`, `AddFallbackCollider` |
| `Assets/Script/Traffic/TrafficRoute.cs` | Polyline route + car list + `GapAhead` | Superseded by the new `TrafficEdge`; remove after migration |
| `Assets/Script/Traffic/Editor/TrafficDemoSetup.cs` | `Go Vista/Setup Demo Traffic`, `Go Vista/Validate Traffic Routes`, adds `Traffic` root, `Cars`, player, camera, InputHub; sets defaults | Must be updated to set 40 cars / 25 m spacing and call the graph validator |
| `Assets/Script/PlayerScript/PlayerInputHub.cs` | Has `keyboardFallback` | Do not change for this work |
| `Assets/Script/PlayerScript/DemoKeyboardInput.cs` | Demo WASD/mouse/jump | Do not change |
| `Assets/Resources/Player.prefab` | Latest player prefab from `main`, Normcore components are stripped by the setup menu | Do not change |

Reference geometry (read-only):

- `Assets/Download_Assets/ithappy/Megacity/Traffic/Prefabs/Roads/Road_011.prefab` — 2-lane cross
  with turn splines. Its `Section_1A` is the straight lane `(-1.5, 0, 7.5) → (-1.5, 0, -7.5)`;
  `Section_1B` is `(-1.5, 0, 7.5) → (-7.5, 0, 1.5)` (right turn); `Section_3C` is
  `(-1.5, 0, 7.5) → (7.5, 0, -1.5)` (left turn). Tangents are ≈5.0–6.0. Use this as the turn
  connector template.
- `Assets/Download_Assets/ithappy/Megacity/Traffic/Prefabs/Roads/Road_012.prefab` — rounded-turn
  variant with the same turn geometry.
- `Assets/Download_Assets/ithappy/Megacity/Traffic/Prefabs/Roads/Road_009.prefab` — the 2-lane
  straight prefab used by Demo 1. Spline child offset `(-1.5, 0, +7.5)`; lane centers at ±1.5 m;
  15 m long.

Do **not** edit `Demo 1.unity` by hand. Scene changes are made by running
`Go Vista/Setup Demo Traffic` in Unity and saving.

## Current behaviour that must change

- `TrafficNetwork.BuildRoutes` links lane segments only when the direction dot is ≥ 0.9, so routes
  are **straight-only**. There are currently ~30 open routes ≥45 m; all are open (no closed loops).
- `TrafficCar` follows one route. At the end it sets `distance = 0`, calls `Hide()`, disables
  renderers/colliders for 0.4 s, and teleports to the start. That is the “disappearing” behaviour.
- Car-ahead checks use `TrafficRoute.GapAhead`, which only sees cars on the same route. There is no
  concept of intersections, turns, or cross-traffic.
- The scene has no junction prefabs (`Road_002/003/007/008/011/012`), so turn splines do not exist
  in the scene. Turns must be generated procedurally from the lane geometry.

## Target architecture

```
RoadSection (SplineContainer lane splines)
        │  sample ~2 m, world space
        ▼
LaneSegment list ──dedupe──► straight lane runs (current BuildChains logic)
        │
        │  + lane-crossing candidates + lane-end candidates
        ▼
TrafficIntersection clusters (center, box, arms, entries, exits)
        │
        ├── split lane runs at box edges
        ├── straight edges inside each box
        ├── turn edges (Bezier, left/right)
        └── U-turn edges at dead ends
        ▼
TrafficLaneGraph { Nodes, Edges, Intersections }
        │
        ▼
TrafficCar: current edge → next edge → intersection token → lane → U-turn if needed
```

### Data model

```csharp
public enum TrafficEdgeKind
{
    Lane,                 // normal road segment between intersections / dead ends
    IntersectionStraight, // straight through an intersection box
    Turn,                 // left/right turn inside an intersection box
    UTurn                 // dead-end turnaround
}

public sealed class TrafficEdge
{
    public int Id;
    public TrafficEdgeKind Kind;
    public float Length;
    public float SpeedLimit;          // m/s; lane = 999, turn = ~5, U-turn = ~3
    public TrafficNode StartNode;
    public TrafficNode EndNode;
    public TrafficIntersection Intersection; // null for Lane and UTurn
    public bool LeadsToDeadEnd;       // used by turn weighting
    public IReadOnlyList<Vector3> Points;
    public List<TrafficCar> Cars;     // cars currently on this edge

    public void Evaluate(float distance, out Vector3 position, out Vector3 forward, out Vector3 up);
    public float ClosestDistanceTo(Vector3 position);
}

public sealed class TrafficNode
{
    public int Id;
    public Vector3 Position;
    public TrafficIntersection Intersection; // entry/exit nodes; null for dead ends
    public bool IsEntry;                     // true = outgoing movement edges (straight/turn)
    public bool IsExit;                      // true = outgoing lane edge
    public bool IsDeadEnd;
    public List<TrafficEdge> Outgoing;
    public List<TrafficEdge> Incoming;
}

public sealed class TrafficIntersection
{
    public int Id;
    public Vector3 Center;
    public float HalfSize;                 // 6 m default (12 m box)
    public List<TrafficNode> Entries;
    public List<TrafficNode> Exits;
    public TrafficCar Occupant;
    public float OccupiedSince;

    public bool TryAcquire(TrafficCar car);
    public void Release(TrafficCar car);
    public bool BoxContains(Vector3 position);
}

public sealed class GraphSettings
{
    public float SampleStep = 2f;
    public float MergeDistance = 1f;
    public float MinTangentDot = 0.9f;
    public float IntersectionHalfSize = 6f;   // clamp to nearestIntersection/2 - 0.5
    public float ClusterRadius = 5f;
    public float TurnHandle = 5f;
    public float TurnSpeedLimit = 5f;
    public float UTurnRadius = 4f;
    public float UTurnSpeedLimit = 3f;
    public float StraightWeight = 0.5f;
    public float LeftWeight = 0.25f;
    public float RightWeight = 0.25f;
    public float DeadEndPenalty = 0.05f;
    public float MaxYDelta = 0.5f;             // ignore flyovers
}

public sealed class TrafficLaneGraph
{
    public List<TrafficNode> Nodes;
    public List<TrafficEdge> Edges;
    public List<TrafficIntersection> Intersections;

    public static TrafficLaneGraph Build(GraphSettings settings);
    public static string BuildAndDescribe(GraphSettings settings, Vector3 center);
    public TrafficEdge SelectNextEdge(TrafficCar car, System.Random random);
    public List<TrafficEdge> SelectSpawnEdges(Vector3 center, float radius, float spacing, int maxCars,
                                               System.Random random);
}
```

**Freeze these signatures before parallel work.** If an agent must change one, update this section
and notify the other agents first.

## File map / routing table

| File | Status | Owner | Purpose | Depends on |
|---|---|---|---|---|
| `Assets/Script/Traffic/TrafficEdge.cs` | **New** | Agent A | Edge polyline, evaluation, car list, speed limit, dead-end flag | `TrafficNode`, `TrafficIntersection`, `TrafficCar` |
| `Assets/Script/Traffic/TrafficNode.cs` | **New** | Agent A | Graph node (entry/exit/dead end), outgoing/incoming edges | `TrafficEdge`, `TrafficIntersection` |
| `Assets/Script/Traffic/TrafficIntersection.cs` | **New** | Agent A | Box, entries/exits, occupancy token, release timing | `TrafficNode`, `TrafficCar` |
| `Assets/Script/Traffic/TrafficLaneGraph.cs` | **New** | Agent A | Lane sampling, lane runs, intersection extraction, splitting, turn/U-turn edges, validation | The three files above |
| `Assets/Script/Traffic/TrafficRoute.cs` | **Delete after migration** | Agent A | Old polyline route; replaced by `TrafficEdge` | — |
| `Assets/Script/Traffic/TrafficCar.cs` | **Rewrite** | Agent B | Edge follower, next-edge choice, intersection wait/enter/release, car-following, player yield, U-turn speed | `TrafficEdge`, `TrafficNode`, `TrafficIntersection`, `TrafficLaneGraph` |
| `Assets/Script/Traffic/TrafficNetwork.cs` | **Refactor** | Agent C | Build graph, spawn 40 cars, road colliders, graph gizmos, stats | `TrafficLaneGraph`, `TrafficCar` |
| `Assets/Script/Traffic/Editor/TrafficDemoSetup.cs` | **Modify** | Agent C | Set 40/25 defaults, new settings, validation menu | `TrafficLaneGraph`, `TrafficNetwork` |
| `Assets/Script/Traffic/Editor/TrafficGraphDebugWindow.cs` | **New (optional but recommended)** | Agent D | Edit-mode graph overlay and stats; no runtime code | `TrafficLaneGraph` |
| `Assets/Script/PlayerScript/*` | **Do not touch** | — | Player rig/input/camera | — |
| `Assets/Download_Assets/Houidisoft technology/Simple water/Demo/Demo 1.unity` | **Do not hand-edit** | — | Scene; setup menu only | — |

### Current-code routing for Agent A

Move/replace these from `TrafficNetwork.cs` into `TrafficLaneGraph.cs`:

- `LaneSegment`, `SegmentKey`, `LinkCandidate` structs.
- `Deduplicate`, `BuildChains`, `AddRoute` (straight lane runs only).
- `BuildRoutes` → becomes the first phase of `TrafficLaneGraph.Build`.
- `BuildAndDescribe` → new graph report.

Keep in `TrafficNetwork.cs`:

- `AddRoadColliders`.
- `SpawnCars` / `SpawnCar` (rewritten to use `TrafficLaneGraph.SelectSpawnEdges`).
- `LogReport` / `OnDrawGizmos` (rewritten for graph).

Delete from `TrafficNetwork.cs` after migration:

- `SelectRoutes` (route-based spawn selection).
- The old `BuildRoutes`/`BuildChains`/`AddRoute`/`Deduplicate` and their structs.
- Any `TrafficRoute` references.

## Agent task board

Status: `[ ]` todo, `[~]` in progress, `[x]` done. Agents update their own rows.

| ID | Agent | Task | Route / files | Depends on | Acceptance |
|---|---|---|---|---|---|
| T1 | A | Data model: `TrafficEdge`, `TrafficNode`, `TrafficIntersection`, `GraphSettings` | `TrafficEdge.cs`, `TrafficNode.cs`, `TrafficIntersection.cs` | — | Compiles; public signatures match this plan |
| T2 | A | Lane sampling + lane runs (port current logic) | `TrafficLaneGraph.cs` | T1 | Finds ~105 `RoadSection`s, ~1,664 unique segments, ~30 lane runs ≥45 m; no `TrafficRoute` refs |
| T3 | A | Intersection extraction: crossings + lane ends, clustering, T/4-way detection, box fitting | `TrafficLaneGraph.cs` | T2 | Logs intersection centers, arm counts (3/4), no false dead ends at T-junctions |
| T4 | A | Split lane runs at box edges; build straight edges; left/right turn Beziers; U-turn edges; nodes; dead-end flags; validation | `TrafficLaneGraph.cs` | T3 | Every lane end has an outgoing edge or a U-turn; turn/U-turn counts logged; no zero-length edges |
| T5 | B | Car edge follower: `Initialize(TrafficLaneGraph, TrafficEdge, distance, …)`, advance distance, transition at node, choose next edge, remove `Hide`/`SetVisible` | `TrafficCar.cs` | T1, T4 interfaces | Cars traverse lane → intersection → lane; no disappearing |
| T6 | B | Intersection occupancy: stop at entry, `TryAcquire`, exit-space check, hold until the box is cleared, release | `TrafficCar.cs`, `TrafficIntersection.cs` (coordinate with A) | T5 | Only one car in a box; queues behind; no overlap |
| T7 | B | Speed limits / look-ahead braking, turn and U-turn speeds, player yield on all edges, wheel spin | `TrafficCar.cs` | T5 | Cars slow before turns; U-turn at 3 m/s; player still blocks/yields |
| T8 | C | Graph build + 40-car spawn with 25 m spacing, spawn only on lane edges near the player, prefer non-dead-end edges | `TrafficNetwork.cs` | T4, T5 | 40 cars spawn; every car sits on a valid lane edge; log graph/car counts |
| T9 | C | Graph gizmos and stats: lane/turn/U-turn colors, intersection boxes, dead ends, `BuildAndDescribe` report | `TrafficNetwork.cs` | T8 | Scene view shows the graph; menu report lists counts |
| T10 | C | Editor setup defaults: `maxCars 40`, `carSpacing 25`, `trafficRadius 300`, turn/U-turn settings; validation menu calls the new report | `Traffic/Editor/TrafficDemoSetup.cs` | T8 | Re-running `Go Vista/Setup Demo Traffic` applies the new values; validation menu works |
| T11 | D | Graph debug overlay: edit-mode build, edge/intersection drawing, toggle, selected-edge stats | `Traffic/Editor/TrafficGraphDebugWindow.cs` | T4 | Graph visible without Play; no scene changes |
| T12 | D | QA: compile, setup, play, regression, performance, edge cases | none | T5–T11 | Checklist below passes |

Dependency graph:

```
T1 → T2 → T3 → T4 ─┬→ T5 → T6
                    │        └→ T7
                    ├→ T8 → T9 → T10
                    └→ T11
T5/T6/T7/T8/T9/T10/T11 → T12
```

Parallel work:

- **Agent A** works T1–T4 first. B and C can start against the frozen interfaces as soon as T1
  exists; A should commit stub methods that return empty graphs so B/C compile before T4 lands.
- **Agent B** owns only `TrafficCar.cs`; coordinate with A for any `TrafficIntersection` method
  changes.
- **Agent C** owns `TrafficNetwork.cs` and `TrafficDemoSetup.cs`; do not let C touch
  `TrafficCar.cs` or the graph files.
- **Agent D** owns only the new debug editor window and the QA checklist.

## Algorithms

### 1. Lane sampling and lane runs (T2)

Port the existing logic:

- `RoadSection[]` → `SplineContainer.Splines` → sample every `SampleStep` using
  `container.Evaluate(index, t, out position, out tangent, out up)` (world space).
- Deduplicate quantized endpoints (0.05 m).
- Link segment ends to starts when distance ≤ `MergeDistance` and direction dot ≥ `MinTangentDot`.
- Build straight lane runs (the old `BuildChains`). Keep the robust 1-to-1 greedy linking.
- Expected on Demo 1: ~105 road sections, ~1,664 unique segments, ~30 lane runs ≥45 m.

Do **not** create turns in this phase. Lane runs are the raw material for intersections.

### 2. Intersection extraction (T3)

Two candidate sources:

1. **Lane crossings** — for each pair of lane segments from different lane runs:
   - skip if `abs(dot(dirA, dirB)) > 0.3` (parallel/same road),
   - skip if `abs(yA - yB) > MaxYDelta`,
   - compute the 2D (`xz`) segment intersection; keep points inside both segments.
2. **Lane run ends** — add each run’s start/end point as a candidate.

Cluster candidates with a spatial hash using cell size `ClusterRadius` (default 5 m). For each
cluster:

- Gather lane runs whose geometry comes within `ClusterRadius + 1 m`.
- **Dead end**: exactly the two opposite parallel lane runs of one road (directions dot ≤ −0.9,
  lateral separation ≈ 3 m), no perpendicular run. Mark the lane end as a dead end; do not create
  an intersection.
- **Intersection**: at least one pair of runs with `abs(dot) < 0.5` (perpendicular/adjacent), or a
  lane run ends against a perpendicular run (T-junction). The box center is the centroid of the
  lane crossing points; if there are none (T), use the nearest point on the through lane to the
  side lane end.
- Discard clusters with fewer than two distinct lane runs.
- Merge clusters whose centers are within 1 m.

Log every cluster: center, number of arms (3-way/4-way), lane runs. The debug gizmo (T11) should
draw the centers and boxes.

### 3. Box, entry/exit points, lane offset (T4)

For each intersection and lane run:

```
half   = min(settings.IntersectionHalfSize, nearestIntersectionCenterDistance / 2 - 0.5)
dir    = lane run direction at the intersection (world xz, normalized)
right  = new Vector3(dir.z, 0, -dir.x)
offset = dot(right, laneCenterPoint - intersectionCenter)   // ±1.5 for the 2-lane roads
entry  = center - dir * half + right * offset
exit   = center + dir * half + right * offset
```

- A lane run that **passes through** gets both entry and exit.
- A lane run that **ends** at the intersection gets only an entry (incoming arm).
- A lane run that **starts** at the intersection gets only an exit (outgoing arm).
- Trim/split the lane run polyline at `entry` and `exit`; insert the exact points so the lane edge
  and box edge connect exactly.
- The lane edges outside the box remain normal `Lane` edges. The segment from `entry` to `exit`
  becomes an `IntersectionStraight` edge **only when the run passes through**.

The kit reference `Road_011` uses half = 7.5 m, lane offset 1.5 m, handles ≈5–6 m. The default
6 m half keeps a lane gap between intersections that are 15 m apart; expose the value.

### 4. Turn edges (T4)

For each incoming entry at an intersection, create a movement edge for each outgoing exit:

- Skip the outgoing exit of the same lane run’s own straight (that is the `IntersectionStraight`).
- Skip U-turn directions: `dot(incomingDir, outgoingDir) < -0.5`.
- Straight-through: `IntersectionStraight` from the entry to the same run’s exit.
- Left/right: `Turn` edge from the entry to the other outgoing exit.

Bezier:

```
P0 = entry
P3 = exit
handle = min(TurnHandle, half * 0.75)
P1 = P0 + incomingDir * handle
P2 = P3 - outgoingDir * handle
sample 8–12 points along the cubic
```

- `SpeedLimit = TurnSpeedLimit` (default 5 m/s).
- `Intersection = this`.
- `StartNode = entry node`, `EndNode = exit node`.
- The curve must lie inside the box; the debug window draws it. If a curve bulges outside,
  reduce `handle`.

Classify left vs right for weighting using the sign of the 2D cross product
`incomingDir.x * outgoingDir.z - incomingDir.z * outgoingDir.x`.

### 5. U-turns and dead-end avoidance (T4)

At each dead end (cluster of the two opposite lane runs):

- Incoming lane end `P0`; opposite lane start `P3`.
- `outgoingDir = -incomingDir` (the opposite lane).
- `P1 = P0 + incomingDir * UTurnRadius`; `P2 = P3 + incomingDir * UTurnRadius`.
- Sample the cubic; `Kind = UTurn`; `SpeedLimit = UTurnSpeedLimit` (default 3 m/s); no
  intersection.
- `StartNode = dead-end incoming node`, `EndNode = opposite lane start node`.
- If the opposite lane is missing (one-way), mark the dead end as an orphan; the graph validation
  fails loudly. Do not hide/despawn; the car stops with an error if it ever reaches it.

**Dead-end avoidance:**

- After the graph is built, reverse-traverse from every dead-end node and mark lane runs/edges
  whose only continuation is a U-turn: `LeadsToDeadEnd = true`.
- When a car chooses a movement edge at an intersection, multiply the weight by
  `DeadEndPenalty` (0.05) if the chosen exit lane leads to a dead end.
- Cars already inside a dead-end branch still take the U-turn at the end.
- Spawn preference: choose lane edges with `LeadsToDeadEnd == false` first; allow the others only
  if the main network cannot fit 40 cars.

### 6. Node/edge assembly and validation (T4)

- Create one node per physical connection point. Reuse nodes when a lane edge ends at the same
  point as an intersection entry/exit.
- Every lane edge: `StartNode` → `EndNode`.
- Entry nodes: outgoing `IntersectionStraight` + `Turn` edges.
- Exit nodes: outgoing `Lane` edge.
- Dead-end nodes: outgoing `UTurn` edge.
- No node should have zero outgoing edges. If one does, log an error with its position.
- No zero-length edges (merge or skip).
- No duplicate edges (same start/end/kind/intersection).
- Compute the strongly connected components (optional but recommended) and log the largest
  component’s edge count; cars should spawn inside the largest component.
- Log: lane runs, intersections (3-way/4-way), straight edges, left/right turns, U-turns, dead
  ends, orphan nodes, total edge length.

### 7. Car follower (T5–T7)

State:

```csharp
TrafficLaneGraph graph;
TrafficEdge currentEdge;
float distanceOnEdge;
float speed;
TrafficEdge plannedEdge;     // chosen at the end of the current lane edge
bool holdsIntersection;
```

Per `FixedUpdate`:

1. **Player yield** — keep the existing `OverlapBoxNonAlloc` check (12 m look-ahead, 4 m stop).
2. **Car ahead** — check cars on `currentEdge` with greater distance; if `plannedEdge` exists,
   also check its cars and the next lane edge after it. Gap is the sum of edge remainders plus
   the other car’s distance.
3. **Target speed** — `min(maxSpeed, currentEdge.SpeedLimit)`. If `plannedEdge` is a turn/U-turn
   and the remaining distance is within the braking distance, target the planned edge’s speed
   limit. Keep the old acceleration/brake values.
4. **Advance** — `distanceOnEdge += speed * dt`.
5. **Decision point** — when `distanceOnEdge > currentEdge.Length - decisionDistance` (≈8 m),
   choose `plannedEdge`:
   - If `currentEdge` ends at a lane entry node: call `graph.SelectNextEdge(this, random)`.
   - If `currentEdge` ends at an exit/dead-end node: the only outgoing edge is the lane/U-turn.
6. **Transition** — at `distanceOnEdge >= currentEdge.Length`:
   - If `plannedEdge` is an `IntersectionStraight` or `Turn`, request
     `intersection.TryAcquire(this)`. If it fails, clamp `distanceOnEdge` to
     `currentEdge.Length - stopLineOffset` (0.5–1 m), set speed 0, and wait.
   - Before acquiring, check exit space: the lane edge after the movement edge must have no car
     within `exitClearance` (≈12 m) of its start. If blocked, wait outside the box.
   - On success: remove from `currentEdge.Cars`, set `currentEdge = plannedEdge`,
     `distanceOnEdge = 0`, add to the new edge’s list, `holdsIntersection = true`.
   - For a U-turn or a plain lane transition, no token is needed; transition directly.
   - Remove the car from the old edge list and add it to the new one every time.
7. **Release** — after transitioning from the movement edge to the outgoing lane edge, keep the
   intersection token until `distanceOnEdge > carLength` (≈4.5 m) so the rear clears the box, then
   `intersection.Release(this)`.
8. **No hide / no teleport** — delete `Hide()`, `SetVisible()`, `hidden`, `hideTimer`,
   `k_HideDuration`, and the wrap branch. `EvaluateTransform(true)` remains only for spawning.

`TrafficNetwork.SelectNextEdge` weights:

- Straight: `StraightWeight`
- Left: `LeftWeight`
- Right: `RightWeight`
- Multiply by `DeadEndPenalty` when the chosen exit lane has `LeadsToDeadEnd`.
- If the sum is zero, take straight; if no straight, take any.

### 8. Intersection occupancy (T6)

`TrafficIntersection`:

```csharp
public bool TryAcquire(TrafficCar car)
{
    if (Occupant != null && Occupant != car) return false;
    Occupant = car;
    OccupiedSince = Time.time;
    return true;
}

public void Release(TrafficCar car)
{
    if (Occupant == car) Occupant = null;
}
```

- Only the car that acquired may hold; the waiting car never enters the box.
- The occupant releases after clearing the box (step 7), not at the exit node.
- **Exit-space check** before acquiring prevents a car from blocking the box behind a queue.
- **Deadlock breaker (optional):** if a car waits more than `maxWait` (12 s) and the occupant has
  held the box longer than `maxHold` (15 s), log a warning and force-release. This should never
  trigger if the exit-space check works.

### 9. Car-following and edge lists (T5–T7)

- `TrafficEdge.Cars` is the authoritative list. Add/remove on every transition.
- `GetCarAhead(car, edge)` returns the nearest car on the same edge with a greater distance.
- The look-ahead can span up to two edges: current movement edge + outgoing lane edge. This is
  enough for the 6–7.5 m intersection boxes.
- `carSpacing` (25 m) is both the spawn minimum and the following gap; the car slows below
  `followGap` and stops at `stopGap` (3 m), as today.

### 10. Spawn and density (T8)

- `maxCars = 40`, `carSpacing = 25`, `trafficRadius = 300`.
- Candidate edges: `Kind == Lane`, length ≥ `carSpacing`, within `trafficRadius` of
  `trafficCenter` (the player).
- Sort by distance to the player, then length; prefer `LeadsToDeadEnd == false`.
- Allocate cars round-robin by capacity `floor(edge.Length / carSpacing)`, minimum 1.
- Place slots evenly inside each edge, with a small jitter, at least `carSpacing` apart.
- Instantiate a random prefab from `carPrefabs`; add `TrafficCar`; `Initialize(graph, edge,
  distance, maxSpeed, …)`; add to `edge.Cars`.
- Keep the existing car prefab palette and the `ITHappy` script stripping.

### 11. Speed limits / turn feel (T7)

- Lane edges: `SpeedLimit = 999` (the car’s own 6–9 m/s max applies).
- Intersection straight: `999`.
- Turn: `TurnSpeedLimit = 5`.
- U-turn: `UTurnSpeedLimit = 3`.
- Look-ahead braking: when the remaining distance to the edge end is less than
  `(speed² - plannedSpeed²) / (2 * brake) + 2 m`, target the planned edge’s speed limit.
- Wheel spin stays as-is.

### 12. Debug and validation (T9, T11)

- `TrafficNetwork.OnDrawGizmos`:
  - Lane edges: green.
  - Intersection straight: cyan.
  - Turns: yellow.
  - U-turns: red.
  - Intersection boxes: magenta wire cubes (`2 * half`).
  - Dead ends: red spheres.
  - Entry/exit nodes: small spheres.
- `Go Vista/Validate Traffic Routes` (TrafficDemoSetup) calls
  `TrafficLaneGraph.BuildAndDescribe(settings, center)` and logs:
  lane runs, intersections, 3/4-way counts, straight/turn/U-turn edges, dead ends, orphans,
  total edge length, largest SCC size, nearest edges to the player.
- `TrafficGraphDebugWindow` (Agent D): rebuilds the graph in edit mode and draws the same
  information via `SceneView.duringSceneGui`; a toggle in `Go Vista` or a window menu. This is
  the fastest way to tune `ClusterRadius`, `IntersectionHalfSize`, `TurnHandle`, and U-turn
  radius before Play.

## Defaults (set by `TrafficDemoSetup`)

| Setting | Value |
|---|---|
| `maxCars` | 40 |
| `carSpacing` | 25 m |
| `trafficRadius` | 300 m |
| `speedRange` | 6–9 m/s |
| `acceleration` / `brake` | 25 / 35 m/s² |
| `lookAhead` / `stopDistance` | 12 m / 4 m |
| `sampleStep` / `mergeDistance` / `minTangentDot` | 2 m / 1 m / 0.9 |
| `IntersectionHalfSize` | 6 m (clamped to nearest intersection) |
| `ClusterRadius` | 5 m |
| `TurnHandle` | 5 m |
| `TurnSpeedLimit` | 5 m/s |
| `UTurnRadius` | 4 m |
| `UTurnSpeedLimit` | 3 m/s |
| `StraightWeight` / `LeftWeight` / `RightWeight` | 0.5 / 0.25 / 0.25 |
| `DeadEndPenalty` | 0.05 |
| `exitClearance` | 12 m |
| `stopLineOffset` | 0.75 m |

The scene already has a `Traffic` object from the previous setup. Re-run
`Go Vista/Setup Demo Traffic` after the code lands so the serialized `maxCars`/`carSpacing` and new
fields are applied, then save the scene.

## Verification

### Static / compile

1. Unity imports the new scripts and regenerates `Assembly-CSharp.csproj` /
   `Assembly-CSharp-Editor.csproj`.
2. `dotnet build Assembly-CSharp-Editor.csproj -v minimal` → 0 errors.
3. `grep -R "TrafficRoute" Assets/Script` → only the deleted/migrated references; no runtime
   `TrafficRoute` usage.

### Edit-mode graph validation

Run `Go Vista/Validate Traffic Routes` on `Demo 1.unity`:

- Finds ~105 road sections and ~30 straight lane runs.
- Finds intersections (expect 3-way/4-way clusters; exact count depends on the scene).
- Creates left/right turn edges and U-turn edges.
- No orphan lane ends; every lane end has an outgoing edge or a U-turn.
- No duplicate or zero-length edges.
- The largest SCC contains the road network; spawn edges are inside it.
- The report lists the nearest edges to the player.

If the intersection count is 0, the cluster/crossing detection is wrong. Tune `ClusterRadius`,
`MaxYDelta`, and the crossing filter before Play.

### Play-mode checklist

1. `Go Vista/Setup Demo Traffic` → save → Play.
2. 40 cars exist under `Traffic/Cars`; the console report shows 40 spawned.
3. Cars drive straight, turn left/right at intersections, and loop around the network.
4. No car disappears, hides, or teleports from a route end; a car reaching a dead end U-turns.
5. Only one car is inside an intersection box at a time; cars queue at the entry.
6. Cars stop for the player and the player cannot walk through them.
7. The player can stand in an intersection; cars stop and wait; when the player leaves, they
   proceed.
8. No cars overlap each other inside intersections; queues behave.
9. Performance is acceptable with 40 cars (no per-frame allocations in `FixedUpdate`; the
   `OverlapBoxNonAlloc` buffer is reused).
10. No console errors/exceptions; no null `currentEdge`/`plannedEdge`.

### Regression checklist

- Player rig (`PlayerController`, `CameraOrbit`, `PlayerInputHub`, `DemoKeyboardInput`) unchanged
  and working.
- Road colliders still added at Play; the player stands on the road.
- The showcase cameras remain disabled; one `MainCamera`/`AudioListener`.
- Traffic remains local/client-side; not Normcore-synced.
- The scene can be saved and reopened without missing-script warnings.

## Risks and edge cases

| Risk | Mitigation |
|---|---|
| Intersection detection misses T-junctions or false-positives on parallel lanes | Use both lane crossings and lane ends; require a perpendicular/adjacent run pair; log clusters; tune `ClusterRadius` |
| Intersection boxes overlap because intersections are 15 m apart | Clamp `half` to `nearestCenterDistance/2 - 0.5`; use 6 m default; merge centers <1 m |
| Turn Beziers bulge outside the box or clip props | `handle = min(TurnHandle, half*0.75)`; debug draw; tune `TurnHandle`; kit reference uses 5–6 m handles |
| U-turn curve runs off the road end or clips terrain/props | `UTurnRadius` configurable; dead-end avoidance reduces entries; debug draw; fallback to a tighter curve |
| Deadlock at an intersection | Exit-space check + release after clearing + optional `maxWait`/`maxHold` force release |
| Car has no outgoing edge (orphan dead end) | Graph validation fails loudly; add U-turn pair; car stops with an error rather than disappearing |
| 40 cars cause gridlock | 25 m spacing, 300 m radius, 40 cars; exit-space checks; tune `maxCars`/`carSpacing`; occupancy log |
| Cars visibly clip through each other outside intersections | Per-edge car-following + spawn spacing; intersections only where cross-traffic exists |
| Flyovers/overpasses | `MaxYDelta = 0.5 m` excludes different-height roads; all Demo 1 roads are flat |
| Player stands in an intersection for a long time | Cars stop for the player and hold the token; other cars queue; the player can leave |
| Runtime graph build hitch | Build once in `Start`; O(n²) crossing detection on ~1,664 segments is acceptable; can add a spatial hash if needed |
| Agent merge conflicts | File ownership is strict; freeze interfaces; each agent owns only the files in the task board |

## Out of scope

- Adding junction prefabs or rebuilding the road meshes.
- Traffic lights/visible signal poles.
- Networked/Normcore synchronization.
- Pedestrians, car audio, headlights, turn signals.
- Changing the player rig, camera, input, or road colliders.
- Adding Demo 1 to `EditorBuildSettings`.

## Handoff checklist

- [ ] Agent A: T1–T4 complete; `TrafficLaneGraph.BuildAndDescribe` reports expected counts.
- [ ] Agent B: T5–T7 complete; no `Hide`/`SetVisible`/wrap code remains.
- [ ] Agent C: T8–T10 complete; `maxCars 40`, `carSpacing 25`; setup menu applies them.
- [ ] Agent D: T11 graph debug window + T12 QA checklist complete.
- [ ] `dotnet build Assembly-CSharp-Editor.csproj -v minimal` → 0 errors.
- [ ] `Go Vista/Validate Traffic Routes` → no orphan nodes, turns/U-turns present.
- [ ] `Go Vista/Setup Demo Traffic` → save → Play → 40 cars, turns, U-turns, no disappearing,
      occupancy lock, player yield, no regressions.
- [ ] Update this plan’s checkboxes and leave a short note per agent with the exact files changed.
