# PrismSpatialIndex — The Canonical Spatial Index of Prism Mass

`Assets/_Scripts/Controller/Managers/PrismSpatialIndex.cs`

**Read this before writing ANY new spatial query against prisms.** If you are
about to call `Physics.OverlapSphere`, `Physics.CheckBox`, or build a new
grid/registry/octree over prisms — stop. The capability either already exists
here or belongs here. The point of this system is that there is exactly **one**
answer to "what mass is where," fed by exactly **one** registration lifecycle.

## Why one system

Before unification (June 2026) there were three parallel spatial systems over
the same prism population, plus ad-hoc physics queries:

| System | What it stored | Fatal flaw |
|---|---|---|
| `PrismAOERegistry` | Flat NativeArrays: position + flags + domain/volume | No spatial acceleration (linear scan); registered 0.6s after spawn |
| `Cell.countGrids` (`BlockDensityGrid`) | 75m-voxel per-domain counts | Same 0.6s-late registration; too coarse for occupancy |
| Unity physics broadphase | Colliders | Prism colliders are **disabled for the first `Prism.waitTime` (0.6s)** after spawn — physics queries are structurally blind to fresh prisms |

The blindness window was the root cause of gyroid flora stacking prisms on
occupied sites: `Physics.CheckBox` in `GyroidAssembler.GetGrowthInfo` could not
see siblings spawned within the last 0.6s, so concurrent branches (and
concurrent floras) double-filled sites — hidden mass, wasted cost.

The unification keeps the proven query *math* of each system and consolidates
the *bookkeeping*: `PrismAOERegistry` was renamed to `PrismSpatialIndex` and
extended with an occupancy bucket grid and a reservation set. The cell density
grids remain (their coarse smoothing/argmax math is fit for purpose) but are
understood as a **view** of the same lifecycle, not an independent system.

## Architecture

```
                       ┌──────────────────────────────────────────────┐
   one lifecycle       │              PrismSpatialIndex               │
                       │                                              │
 TryReserve(pos) ────► │  _reservations  Dictionary<key, claim+TTL>   │  occupancy
 (growth decision,     │  _buckets       MultiHashMap<int3, index>    │  view
  BEFORE Instantiate)  │                                              │
                       │  _spatial[i]    16B hot: position + flags    │  AOE view
 Register(prism) ────► │  _damage[i]     8B cold: volume + domain     │  (Burst scan)
 (CreateBlockCoroutine,│  _prisms[i]     managed Prism refs           │
  consumes reservation)│                                              │
                       └──────────────────────────────────────────────┘
 MarkDestroyed(i)  → AOE skips it, bucket freed (site can be regrown)
 MarkRestored(i)   → re-enters AOE + bucket (trail restore mechanics)
 UpdatePosition(i) → rebuckets (gyroid bonding steers existing blocks)
 Unregister(i)     → slot freed (pool return / destroy)

 Cell.AddBlock / RemoveBlock (75m per-domain density grids, fauna targeting)
 — same lifecycle moments, coarse-density view. Phase 3 moves their call
   sites onto the index's events so the streams cannot diverge.
```

### Data structures

- **Hot array** `_spatial[i]` — `PrismSpatialData`, 16 bytes (float3 position +
  flags byte), 4 per cache line. Scanned by the Burst AOE job.
- **Cold array** `_damage[i]` — volume + domain, read on the main thread only
  for prisms that pass the spatial filter.
- **Bucket grid** `_buckets` — `NativeParallelMultiHashMap<int3, int>` mapping
  `floor(position / BucketSizeMeters)` → registry index. One entry per **live**
  (active, not destroyed) prism. Maintained incrementally — prisms are mostly
  static, so there is no per-frame rebucketing cost.
- **Reservation set** `_reservations` — managed `Dictionary` keyed by quantized
  position. A reservation is a *claim* on a site by a growth system, made
  synchronously with the grow decision. It is **bookkeeping, not mass** — it
  never appears in AOE queries, density grids, or scoring.

### Constants (tuning)

| Constant | Value | Rationale |
|---|---|---|
| `BucketSizeMeters` | 8 | Gyroid bond spacing (~8m = \|DeltaPosition\| ≈ 2.7 × separationDistance 3). An occupancy probe of radius ≤ half-spacing touches ≤ 8 buckets. |
| `ReservationQuantum` | 4 | Half bond spacing — key granularity for the claim set. |
| `ReservationTtlSeconds` | 5 | Safety net for claims never confirmed by a Register (spawn skipped, prism killed inside `Prism.waitTime`). Comfortably past waitTime (0.6s) + one flora grow cadence. |

## The reservation lifecycle (why overlaps are now impossible)

The old physics check ran at grow time but could only see colliders — and a
prism's collider turns on 0.6s after spawn. Two branches deciding within 0.6s
of each other both saw "empty" and both spawned. **No collider-based check can
close this race; the claim has to be synchronous with the decision.**

```
GyroidAssembler / WallAssembler / SchwarzPAssembler . GetGrowthInfo()
  └─ TryReserve(newPosition, clearRadius)        ← atomic check-and-claim
       ├─ live prism within clearRadius?   → false (site occupied → knit lattice)
       ├─ active reservation within range? → false (sibling already claimed it)
       └─ else: claim recorded, returns true → caller may Instantiate
AssembledFlora.Grow()
  └─ Instantiate(healthPrism, growthInfo.Position, ...)
       └─ Prism.Initialize → CreateBlockCoroutine (0.6s)
            └─ Register(prism)                    ← consumes the claim (matched
                                                    by proximity, ±2m, because
                                                    spindle re-parenting round-
                                                    trips the position through
                                                    parent matrices)
  [prism killed inside the 0.6s window / spawn never happens]
            └─ claim lapses after ReservationTtlSeconds — no leak
```

`clearRadius` at the call sites is `max(2, 0.4 × bond spacing)` — below
half-spacing so legitimate neighbor lattice sites are never blocked, above any
positional drift so a same-site duplicate always is.

**Callers' contract:** if you call `TryReserve` and then decide *not* to spawn,
either call `ReleaseReservation(pos)` or accept the TTL lapse (the site is
blocked for up to 5s). `AssembledFlora` orders its random-skip *before*
`GetGrowthInfo` for exactly this reason.

## API

| Method | Caller | Purpose |
|---|---|---|
| `TryReserve(pos, clearRadius)` | Growth systems at the grow decision | Atomic occupancy check + claim |
| `IsPositionOccupied(pos, clearRadius)` | Anyone (read-only) | Live prism or active claim in range? |
| `IsAnyPrismWithin(pos, radius)` | Anyone (read-only) | Live prism in range (claims excluded) |
| `ReleaseReservation(pos)` | A claimant that changed its mind | Explicit cancel |
| `Register(prism)` | `Prism.CreateBlockCoroutine` **only** | Enter the index; consumes matching claim |
| `Unregister(index)` | `Prism` OnDisable/OnDestroy/ResetState **only** | Leave the index |
| `MarkDestroyed(index)` | `Prism.SetupDestruction` **only** | AOE skips; occupancy frees |
| `MarkRestored(index)` | `Prism.Restore` **only** | Re-enter AOE + occupancy |
| `UpdatePosition(index, pos)` | `Prism.NotifyPositionChanged` (movers) | Keep stored position honest |
| `UpdateShieldState(index, ...)` | `PrismStateManager` **only** | Shield flags for AOE |
| `ProcessExplosionFrame(...)` | `ExplosionImpactor` **only** | Batch AOE damage (Burst) |

All methods are **main-thread only**. The Burst job inside
`ProcessExplosionFrame` is scheduled and completed synchronously.

## Mass-conservation alignment

Per CLAUDE.md's design philosophy: mass is conserved — prisms are removed only
by **active forces** (vessel abilities, fauna consumption). The index respects
this exactly: a bucket entry frees only on `MarkDestroyed`/`Unregister`, both
of which are driven by active-force code paths. Reservations are the only
TTL-expiring entries, and they represent *intent to create mass*, not mass.
Nothing in this system decays, culls, or auto-corrects prism populations.

## What NOT to use it for

- **Raycasts / narrow-phase geometry** (`AIPilot` obstacle rays, skimmer
  `Collider.ClosestPoint`, arch-burst placement rays) — the index stores
  points, not extents+rotations. Physics is the right tool there.
- **Fauna-vs-fauna flocking** — different population (moving every frame). If
  boid neighbor queries need acceleration, instantiate the same *structure*
  over fauna (Phase 4); do not register fauna bodies as prism mass (see
  `Prism.RegisterWithCell`'s fauna-body exclusion for the same reason).
- **Cross-network queries** — the index is local sim state, not replicated.

A note on **lattice bookkeeping**: an assembler may keep its own *graph-side*
state about its lattice — the gyroid's bond-site flags, the Schwarz P frame's
param-space registry (`SchwarzPSurfaceFrame.occupied`). That is fine: it
encodes adjacency semantics in the assembler's own coordinate system, which a
world-space index cannot express. The rule is that **world-space occupancy** —
"is physical space free, across all structures" — goes through this index, and
only this index. `SchwarzPAssembler.GetGrowthInfo` shows the pattern: frame
registry for its own lattice, `TryReserve` for the world.

## Known gaps (intentional, tracked)

- `UpdateDomain` / `UpdateVolume` have **no callers**: a stolen prism keeps its
  registration-time domain in the AOE cold data, and a grown prism keeps its
  spawn-time volume. Pre-existing behavior, preserved through the rename.
  Wiring `PrismTeamManager` → `UpdateDomain` changes AOE friend/foe results and
  must be its own tested change.
- Occupancy treats prisms as points with a clearance radius, not oriented
  boxes. A fat trail block whose *center* is outside `clearRadius` can still
  visually intersect a grown gyroid block — same tolerance the game already
  has for trails crossing trails.
- `Cell.AddBlock`/`RemoveBlock` call sites are still in `Prism` (Phase 3 moves
  them onto index events).

## Roadmap

| Phase | Scope | Status |
|---|---|---|
| 1 | Bucket grid + reservations; `GyroidAssembler`/`WallAssembler`/`SchwarzPAssembler` switch from `Physics.CheckBox` to `TryReserve`; lifecycle holes fixed (`Restore` re-entry, pool-return staleness, mover positions) | **Shipped** |
| 2 | `QuerySphere`/`FindNearest` views replace remaining physics queries against prisms: `GyroidAssembler.FindClosestMate` + `WallAssembler` mate-finding (also fixes the stale `OverlapSphereNonAlloc` array bug), `ScoutTrailPrismScaler`, `LightFauna` prism scan, `Boid` prism-attraction scan | Planned |
| 3 | `Cell.AddBlock`/`RemoveBlock` driven by index registration events — one stream feeds both the fine (occupancy) and coarse (density) views | Planned |
| 4 | Optional: second index instance over fauna for boid flocking neighbors; bucket-accelerated AOE if profiling ever demands it | Candidate |

## Adding a consumer (checklist)

1. Does an existing query method answer your question? Use it.
2. Need a new query shape? Add it **to this class** as a method over `_buckets`
   / `_spatial` — do not build a parallel store.
3. Never cache registry indices across frames outside `Prism.SpatialIndexId`.
4. Never call `Register`/`Unregister`/`Mark*` from outside the `Prism`
   lifecycle methods listed above — single-writer per lifecycle event is what
   keeps the views consistent.
