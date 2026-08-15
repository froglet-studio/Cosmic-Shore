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
 (growth decision,     │  _buckets       MultiHashMap<int3, index>    │  + neighborhood
  BEFORE Instantiate)  │                                              │  views
                       │  _spatial[i]    16B hot: position + flags    │  AOE view
 Register(prism) ────► │  _damage[i]     8B cold: volume + domain     │  (Burst scan)
 (CreateBlockCoroutine,│  _prisms[i]     managed Prism refs           │
  consumes reservation)│                                              │
                       └──────────────────────────────────────────────┘
 MarkDestroyed(i)  → AOE skips it, bucket freed (site can be regrown)
 MarkRestored(i)   → re-enters AOE + bucket (trail restore mechanics)
 UpdatePosition(i) → rebuckets (gyroid/wall bonding steers existing blocks;
                     fauna body prisms swim — movers keep positions honest)
 Unregister(i)     → slot freed (pool return / destroy)

 QuerySphere / QuerySegment / IsAnyPrismWithin (neighborhood views over _buckets/_spatial)
 — fauna senses (LightFauna, Boid), assembler mate-finding (Gyroid, Wall),
   trail passives (ScoutTrailPrismScaler). Replaced the Physics.OverlapSphere
   calls those systems used to make against prism colliders.

 _cells[i] (Cell.AddBlock / RemoveBlock — 75m per-domain density grids,
 fauna targeting, cell phase LiveBlockCount)
 — the coarse-density view, driven by the SAME lifecycle since Phase 3:
   Register/MarkRestored bind the containing cell, MarkDestroyed/Unregister
   release it, ForwardDomainChangeToCell re-files steals. One stream feeds
   both the fine and coarse views, so they cannot diverge. Fauna bodies are
   bound VOLUME-ONLY in this view (BindCell files them into the cell's
   volume accounting — volume is the spine, every prism counts — but keeps
   them out of the targeting grids and counts). The flora ownership
   stream (HealthBlockTracker → Cell.AddBlock for the LifeForm's host cell)
   remains a second, idempotent contributor.

 _cellData[i] (8B: live volume + cell id + live domain slot + env flag)
 — the cell-volume SUMMATION view. Cell.EnsureVolumeFresh runs ONE Burst
   pass (CellVolumeSumJob via SumCellVolumes) over this array every 0.25s
   instead of a managed loop over the prism object graph (the old
   8000-prisms-per-frame slice cost the first volume reader ~10 ms per
   slice frame at high prism counts — the reader-attributed spike behind
   "DomainVolumeIndicator.Update 10.31 ms"). Freshness contracts:
     • CellId/EnvMass — written ONLY by Cell.AddBlock/RemoveBlock
       (SetCellBinding/ClearCellBinding); both membership streams funnel
       through them, so the packed view mirrors the cell's bookkeeping by
       construction. Cell bulk clears (Initialize/ResetCell) call
       ClearAllCellBindings. With dual membership (host-cell vs containing
       cell) the LAST binder owns the slot — a prism sums into exactly one
       cell, where the old massTracked could double-count across two.
     • Volume — pushed by Prism.RefreshVolumeCache (UpdateCellVolume),
       O(growing)/frame; Register seeds from CachedVolume.
     • DomainSlot — refreshed by ForwardDomainChangeToCell on every steal /
       ChangeTeam. The AOE damage view's Domain is refreshed on the same
       event: Prism.HandleTeamChangedForCell pairs the forward with
       UpdateDomain so explosion friend/foe reads the live domain (the
       Charge-5 "spare own domain" unlock requires it — the former
       stale-steal gap is closed).
```

### Data structures

- **Hot array** `_spatial[i]` — `PrismSpatialData`, 16 bytes (float3 position +
  flags byte), 4 per cache line. Scanned by the Burst AOE job.
- **Cold array** `_damage[i]` — volume + domain, read on the main thread only
  for prisms that pass the spatial filter. Volume is a registration-time
  snapshot (see Known gaps); Domain is refreshed on steal / ChangeTeam via
  `UpdateDomain` (Prism.HandleTeamChangedForCell).
- **Summation array** `_cellData[i]` — `PrismCellData`, 8 bytes (live volume,
  cell id, live domain slot, env-mass flag), 8 per cache line. Scanned by
  `CellVolumeSumJob` on each cell's 0.25s volume recompute. LIVE by contract —
  see the architecture diagram for the three freshness streams.
- **Bucket grid** `_buckets` — `NativeParallelMultiHashMap<int3, int>` mapping
  `floor(position / BucketSizeMeters)` → registry index. One entry per **live**
  (active, not destroyed) prism. Maintained incrementally — prisms are mostly
  static, so there is no per-frame rebucketing cost. (Fauna body prisms are the
  moving minority: their `Fauna.NotifyBodyPrismsMoved` calls only rebucket when
  a body crosses an 8m bucket boundary.)
- **Adaptive query strategy** — `QuerySphere`/`QuerySegment`/`IsAnyPrismWithin` walk the bucket
  AABB for tight radii, but fall back to one linear pass over the 16B hot array
  when the AABB covers more buckets than there are slots (a 100m Scout probe is
  26³ ≈ 17k bucket lookups vs one ~64KB scan).
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
| `QuerySphere(pos, radius, results)` | Anyone (read-only) | Gather live prisms in range into a caller scratch list — the replacement for `Physics.OverlapSphere` against prisms |
| `QuerySegment(a, b, radius, results)` | Fast projectiles (`Projectile.SweepPrismsAlong`) | **Swept** counterpart of `QuerySphere`: live prisms within `radius` of the SEGMENT a→b. A projectile is a *teleport*, not a sweep — the mover writes `position += Velocity·Δt` and PhysX samples its trigger once per physics step, so a Sparrow round at 375 u/s tests only ~26% of its own path (~3% at high SPACE). This is how the ground BETWEEN the samples gets tested, without inflating the collider. Degenerate segment (a == b) reduces to `QuerySphere` exactly |
| `CopyLivePrisms(results)` | `PrismColliderLodManager`, telemetry | Whole-population sweep: one linear pass copying every live prism into a caller scratch list |
| `ReleaseReservation(pos)` | A claimant that changed its mind | Explicit cancel |
| `Register(prism)` | `Prism.CreateBlockCoroutine` (+ `Prism.Restore` for spawn-window-killed prisms) | Enter the index; consumes matching claim; binds the containing cell's density grids |
| `Unregister(index)` | `Prism` OnDisable/OnDestroy/ResetState **only** | Leave the index; releases the cell binding |
| `MarkDestroyed(index)` | `Prism.SetupDestruction` **only** | AOE skips; occupancy frees; leaves the cell grids |
| `MarkRestored(index)` | `Prism.Restore` **only** | Re-enter AOE + occupancy + cell grids |
| `ForwardDomainChangeToCell(index)` | `Prism.HandleTeamChangedForCell` **only** | Re-file a stolen prism in its cell's per-domain grids + refresh the summation view's live domain (does NOT touch AOE cold data) |
| `UpdatePosition(index, pos)` | `Prism.NotifyPositionChanged` (movers) | Keep stored position honest (also refreshes the shell view's pose for shielded movers) |
| `UpdateShieldState(index, ...)` | `PrismStateManager` **only** | Shield flags for AOE + publishes/clears the shell view entry |
| `UpdateShellTransform(index)` | `Prism.RefreshVolumeCache` (growers) + `Prism.NotifyPositionChanged` (movers) | Re-capture a shielded slot's world shell pose; O(1) single-byte no-op for the unshielded majority |
| `CollectShellContacts(probes, count, hits)` | `PrismShellContactManager` **only** | Shell-contact tier: one synchronous Burst pass testing every shield-flagged slot's analytic shell (octahedron / stella two-tet union, exact) against the frame's probe set |
| `GetRegisteredPrism(index)` | `PrismShellContactManager` (same-frame resolve) | Managed back-reference for a query-result slot — same parallel-array resolve the explosion path uses |
| `ProcessExplosionFrame(center, radius, blastOrigin, impulse, …, alreadyHit, pending)` | `ExplosionImpactor` **only** | Batch AOE damage for the **spherical** explosion (Burst). `center` is stationary and `radius` grows, so each frame's query volume strictly CONTAINS the previous frame's. `blastOrigin` is the emission point every impact vector radiates from; the job emits `AOEHit {index, unit direction}` pairs, normalizing in-job via `math.rsqrt` so the main-thread damage pass never pays a per-hit managed sqrt. `impulse` (`ExplosionImpulse`) carries speed x inertia AND the debris speed ceiling as one value — see "Impulse" below |
| `ProcessExplosionConeFrame(apex, axis, gapeAxis, sliceMin, sliceMax, tanCoreHalfAngle, tanGapePerUnit, …)` | `ExplosionImpactor` **only** | Batch AOE damage for the **conic** explosion (Burst, `AOEConicSweepQueryJob`). An EXACT test against the swept blast over the axial slab `[sliceMin, sliceMax]` it newly covers this frame; successive slabs tile the sweep, so coverage is frame-rate independent and never reaches past the visible tip. The cross-section is a **capsule** (a 2D stadium), not a disc: radius `tanCoreHalfAngle * s`, half-length `tanGapePerUnit * s` along `gapeAxis` (the axis the emitting vessel's jaws open across). Both tangents are invariant as the self-similar blast grows, and their SUM is the rendered cone's base radius — so the capsule is inscribed in the visible cone, touching it exactly along the gape. `tanGapePerUnit == 0` collapses the test to the original circular cone term for term. The apex is both sweep origin and blast origin |
| `DrainPendingExplosionDamage(pending, impulse, …)` | `ExplosionImpactor` **only** | Resolves budget-deferred damage without a new query. Called after the visual ends so a blast dense enough to exceed the per-frame budget still damages everything it enclosed |
| `SetCellBinding(index, cellId, envMass, domain)` | `Cell.AddBlock` **only** | Bind a slot into a cell's summation view |
| `ClearCellBinding(index, cellId)` | `Cell.RemoveBlock` **only** | Release a slot from the owning cell's summation view (no-op for non-owners) |
| `ClearAllCellBindings(cellId)` | `Cell.Initialize` / `Cell.ResetCell` **only** | Bulk-release a cell's summation-view bindings (packed counterpart of the old massTracked.Clear) |
| `UpdateCellVolume(index, volume)` | `Prism.RefreshVolumeCache` **only** | Mirror the live volume cache into the summation view |
| `SumCellVolumes(cellId, centre, nucleusRadiusSqr, results)` | Tests / benchmarks (sync reference path) | One synchronous `.Run()` pass producing the cell's per-domain volume / env-volume / nucleus-env-volume sums + totals |
| `TryScheduleCellVolumeSum(cellId, centre, nucleusRadiusSqr, results, out handle)` | `Cell.EnsureVolumeFresh` **only** | The production path: snapshots `_spatial`+`_cellData` (one per-frame-shared memcpy) and `Schedule()`s the same job to a worker thread; the caller harvests with `IsCompleted` on a later read and must keep `results` quiescent until then. Result-equivalent to the sync path (pinned by an edit-mode test) |

All methods are **main-thread only**. The Burst jobs inside
`ProcessExplosionFrame` and `ProcessExplosionConeFrame` are scheduled and
completed synchronously.

`QuerySphere` / `QuerySegment` results are an unordered **snapshot**: the caller's own side
effects (consume, predate, steal/convert) can destroy entries mid-iteration,
so iterate with a `!prism || prism.destroyed` guard — the same contract
collider snapshots had. Reuse a static scratch list per call site
(`Fauna.PrismScratch`, the assemblers' `s_mateScratch`); ticks are
main-thread and consume the list within one call, so one list per site is
allocation-free and safe.

**The movers contract**: anything that moves a registered prism must call
`Prism.NotifyPositionChanged()` after moving it — the index stores positions,
and AOE damage, occupancy, and all neighborhood queries read the *stored*
position. Current movers: gyroid bond steering (`GyroidAssembler`), wall bond
pulling (`WallAssembler`), and swimming fauna bodies
(`Fauna.NotifyBodyPrismsMoved`, called per-frame by `LightFauna`/`Boid`
`Update`). Before fauna upheld this contract, batch AOE hit creatures at
their spawn point instead of where they actually were.

## Impulse — what a blast hands the mass it destroys

Every explosion entry point takes one `ExplosionImpulse`
(`_Scripts/Controller/Projectiles/ExplosionImpulse.cs`) rather than a loose
`(speed, inertia)` pair, because the two are meaningless without the third
number that used to travel separately: the **debris speed ceiling**.

Debris speed is `min(Speed * Inertia, ceiling)`. When an explosion supplies no
ceiling of its own, the ceiling is `PrismExplosion.prefab`'s authored
`maxSpeed` (**33.33 u/s**) — a guard sized for the legacy
`impactVector / volume` gain, not a physical bound. Every AOE magnitude sits
far above it (the Dolphin cone's wavefront is `height / (duration * 4)` ≈
**222 u/s**, 6.7x over), so on that contract *every* blast saturates to the
same 33.33 and `Inertia` is dead tuning — turning it up moves nothing on
screen. This is the same trap documented for the hull-ram path in
`VesselDamagePrismEffectSO`.

`AOEExplosion.proportionalDebris` opts a blast onto the true-velocity contract
that `PrismEffectHelper.DamageProportional` already defines: the impact vector
IS the debris velocity (`speed * debrisRestitution * Inertia`) and the blast
passes a matching ceiling, so `Inertia` scales what the player sees, linearly.
`debrisRestitution` defaults to **1/3**, matching the physical read the vessel
and skimmer damage paths ship at — so the AOE, hull, and sword paths stay one
tuning group. Off by default; **on** for `AOEConicExplosion.prefab` (the
Dolphin crystal blast).

Both prism paths carry the ceiling — the Burst resolve
(`ResolveExplosionHit`) and the Physics-trigger fallback
(`ExplosionImpactor.ExecuteCommonPrismCommands`) — so a blast throws mass at
the same speed whether or not the index is available.

## Mass-conservation alignment

Per CLAUDE.md's design philosophy: mass is conserved — prisms are removed only
by **active forces** (vessel abilities, fauna consumption). The index respects
this exactly: a bucket entry frees only on `MarkDestroyed`/`Unregister`, both
of which are driven by active-force code paths. Reservations are the only
TTL-expiring entries, and they represent *intent to create mass*, not mass.
Nothing in this system decays, culls, or auto-corrects prism populations.

## Shell view — the shielded-prism analytic collision tier

A shielded prism's visible shell (octahedron for SHIELDED, the non-convex
stellated octahedron for SUPER-SHIELDED) is 3× its authored box, but its PhysX
collider stays the authored box trigger (a convex-mesh trigger is invisible to
trigger skimmers; a convex-mesh solid is invisible to solid swipes). The
**shell view** makes the shell the interaction surface without touching PhysX:

- `PrismShellData` (cold, 48B): world rotation + shell center + world semi-axes
  (`shieldScale · authoredHalfExtents ⊙ lossyScale`) + bounding radius + kind.
  Populated only for shield-flagged slots; `Kind = None` otherwise. Refreshed at
  engage/disengage (`UpdateShieldState`, `Register`), on growth steps
  (`RefreshVolumeCache → UpdateShellTransform`), and for movers
  (`NotifyPositionChanged → UpdateShellTransform`). Cleared on `Unregister` so
  slot reuse can never alias a stale shell.
- `ShellContactQueryJob` (Burst): the AOE-style linear scan with the flag-byte
  early-out, plus an **exact** narrowphase from `ShieldShellMath`
  (`_Scripts/Utility/ShieldShellMath.cs`): sphere / capsule / oriented-box
  probes vs the octahedron (25-axis SAT for boxes, world-space triangle
  distances for sphere/capsule) and vs the stella as the **union of its two
  tetrahedra** — a probe touching a spike tip collides; a probe threaded
  between spikes inside the bounding box does not. The math was validated
  against independent QP/LP ground truth over 7,200 randomized poses +
  landmark cases with zero disagreements (edit-mode pins:
  `ShieldShellMathTests`).
- `PrismShellContactManager` (`_Scripts/Controller/Managers/`): the Update-rate
  pump. Vessel hulls and skimmers register their collider sets as probes
  (world poses re-read every frame, so elemental/driver skimmer resizes need no
  events); enter transitions dispatch through the same
  `AcceptImpactee` effect chain as triggers
  (`ImpactorBase.AcceptImpacteeFromShellContact`), exits mirror the skim
  bookkeeping (`NotifyShellContactExit`). While a prism's shell owns contact
  (`ShellOwnsContact`), Skimmer/VesselImpactor suppress their box-trigger prism
  dispatch so a pair can never double-fire; a pop clears the flags the same
  frame, and a genuine later box contact re-enters through PhysX (one-swing
  pop-then-destroy stays emergent). `ForceLegacyBoxInteraction` is the A/B
  switch back to authored-box behavior (ExplosionImpactor.ForceLegacyPhysics
  precedent). Markers: `ShellContact.Build` / `ShellContact.Query` /
  `ShellContact.Dispatch`.

### Shell view — in-editor verification (the human gate)

The math is pinned by `ShieldShellMathTests` (edit-mode) and was validated
against independent QP/LP ground truth pre-port; the INTEGRATION needs a human
at the editor:

1. **Squirrel on the Skim Race track** (all-super-shielded lining): skims
   register at the **stella surface** — spike-tip grazes count, threading the
   gap between spikes does not, and nothing fires at the old bare-box reach.
   Boost accrual (+0.1/hit) should feel per-shell-touch.
2. **Rhino swipe vs a shielded prism**: the pop lands at the octahedron
   surface (~3× reach), not point-blank; a swing that follows through past the
   popped shell may still destroy the inner prism (emergent, accepted).
3. **Crystal auto-shield churn** (fly a crystal through a dense trail): the 2 s
   shield windows engage/disengage without console errors and without prisms
   becoming untouchable afterwards.
4. **Profiler** (HexRace, Deep Profile off): `ShellContact.Build/Query` sub-ms;
   `Physics.SendEvents` flat vs bleeding-edge; no new spike train (§5 protocol
   in Docs/PERFORMANCE_OPTIMIZATION.md).
5. **A/B**: toggling `PrismShellContactManager.ForceLegacyBoxInteraction` at
   runtime reverts to authored-box interaction cleanly (pairs drop with exit
   bookkeeping, box triggers resume).

### Shell view — follow-ups (recorded, not blocking)

- **Projectiles/mines stay box-authoritative** vs shielded prisms (status
  quo). Promoting `ProjectileImpactor` to a probe owner is mechanical
  (register on `Projectile.OnEnable`/`OnDisable`, which already exist) once
  the feel is wanted.
- **The AOE tier ignores shells**: both explosion entry points test the stored
  POINT against their query volume (`ProcessExplosionFrame` a sphere,
  `ProcessExplosionConeFrame` a cone slab), so explosions and the shell tier
  disagree about where a shielded prism's surface is. Point the AOE hit test at
  `ShieldShellMath` when tier consistency matters.
- **`AOEDangerHemisphereBlocks.MakeDangerousAsync`** writes
  `prismProperties.IsShielded = true` directly (no `ActivateShield()`): a
  prism registered with that flag publishes a shell with **no shell visual**.
  Inert in shipped config (`markShielded: 0`) but the serialized default is
  true — route through `prism.ActivateShield()` when touched.
- **Re-engage re-dispatch**: a shield engaging while a probe already overlaps
  the new shell dispatches the shielded-hit chain immediately ("the shell
  materialized onto you"). Feels right on paper; confirm during the in-editor
  pass, and if it spams under crystal auto-shield churn, gate enters on
  pair age.
- **`MakeDangerous` still never disengages the shell visual** (pre-existing):
  the octahedron stays rendered while flags + shell interaction correctly
  retire. Visual-only mismatch, owned by PrismStateManager.

## What NOT to use it for

- **Raycasts / general narrow-phase geometry** (`AIPilot` obstacle rays,
  skimmer `Collider.ClosestPoint`, arch-burst placement rays) — the index
  stores points (plus the shell view's pose for shielded slots), not arbitrary
  extents+rotations. Physics is the right tool there. The one sanctioned
  narrowphase is the shell view above — added via checklist item 2 as a query
  method on this class, never as a parallel store.
- **Non-prism fauna proxies** — fauna senses (LightFauna, Boid) DO ride the
  index, because fauna bodies *are* HealthPrisms: registered mass, kept honest
  by the movers contract. What stays forbidden is registering anything that
  isn't a prism (a synthetic "boid marker" entry, a vessel, a crystal) just to
  get neighbor queries — that would corrupt the AOE and occupancy views, which
  assume every entry is damageable, consumable mass. (The cell *density* view
  still excludes fauna bodies from its TARGETING grids/counts —
  `PrismSpatialIndex.BindCell` binds them volume-only — because a forager
  swarm must not read as its own mass concentration; their volume still feeds
  `Cell.LiveVolume`, the phase spine.)
- **Unregistered mound blocks** — `Boid.NewBlock` builds mound blocks without
  `Prism.Initialize`, so they never register. The two queries that must find
  them stay physics-based on the dedicated `Mound` layer:
  `GyroidAssembler.FindClosestMate`'s supplemental Mound probe and
  `Boid.AddToMoundCoroutine`'s naked-edge scan. Tiny population, narrow layer
  — physics is fine there.
- **Cross-network queries** — the index is local sim state, not replicated.

A note on **lattice bookkeeping**: an assembler may keep its own *graph-side*
state about its lattice — the gyroid's bond-site flags, the Schwarz P frame's
param-space registry (`SchwarzPSurfaceFrame.occupied`). That is fine: it
encodes adjacency semantics in the assembler's own coordinate system, which a
world-space index cannot express. The rule is that **world-space occupancy** —
"is physical space free, across all structures" — goes through this index, and
only this index. `SchwarzPAssembler.GetGrowthInfo` shows the pattern: frame
registry for its own lattice, `TryReserve` for the world.

## The AOE damage budget — bounds COST, never COVERAGE

`MAX_NEW_HITS_PER_FRAME` (48) caps how many prisms one explosion may **damage**
per frame, because destroying prisms is the expensive half (2000 in one frame
measured 426 ms). It is a throughput limit, and it must never turn into a
coverage limit. Two rules keep that true:

1. **Over-budget hits are deferred, not dropped.** They are claimed into the
   explosion's `alreadyHit` set *and* pushed onto its `Queue<AOEHit>` backlog
   (`ExplosionImpactor._batchPending`), which drains FIFO at the top of every
   later frame and — via `DrainPendingExplosionDamage` — past the end of the
   visual. A prism's fate is decided by whether the blast **contained** it,
   never by how long the VFX happened to run.
2. **The budget is spent only on a real `Prism.Damage` call.** Dead slots,
   super-shield blocks and same-domain shield activations resolve for free
   (still claimed in `alreadyHit`), so friendly mass sharing a blast can no
   longer starve enemy mass out of the budget. "Free" is about the damage
   BUDGET, not about doing nothing: since 2026-08-15 a super-shield block
   also routes through `Prism.AbsorbSuperShieldHit`, which stamps the
   deflection wobble (`Docs/PRISM_ANIMATION.md` §4.9) so the blast visibly
   rocks the shield instead of stopping dead against nothing. That stamp is
   rate-limited per prism, charges no budget, and changes no gameplay state —
   the branch still returns `false` and still sets `shouldContinue = false`.

> **Why this matters — the bug it fixed.** The original contract skipped
> over-budget prisms *without* claiming them, on the comment "the Burst job will
> re-find these prisms next frame". That holds only while the query volume is
> **nested** frame-to-frame. It is true for the spherical explosion (stationary
> centre, growing radius) and **false for the conic explosion**, whose volume
> *translates*: nesting would need `MaxScale >= 2 * height` (4800 for the
> Dolphin, whose actual range is 400–1600), so the slab advanced past every
> deferred prism and never returned. Those prisms sat inside the cone the player
> saw and took zero damage. **Any new query volume that translates rather than
> grows must pass a backlog queue.**

Residual, by design: a blast containing more prisms than `48 × frames` keeps
damaging for extra frames after its visual ends (≈ 0.06 s for 8k prisms at
60 fps, seconds at the extremes). That is the deliberate trade — latency, not
lost coverage. The lever for shortening it is the per-prism destruction cost
(`Prism.Damage` → `Explode` VFX), not the budget.

## Known gaps (intentional, tracked)

- `UpdateVolume` has **no callers**: a grown prism keeps its spawn-time volume
  in the AOE cold data. Pre-existing behavior, preserved through the rename.
  (`UpdateDomain` is no longer a gap: `Prism.HandleTeamChangedForCell` calls it
  on every steal / ChangeTeam alongside `ForwardDomainChangeToCell`, so AOE
  friend/foe reads the live domain — required by the Charge-5 "spare own
  domain" unlock.)
- Occupancy treats prisms as points with a clearance radius, not oriented
  boxes. A fat trail block whose *center* is outside `clearRadius` can still
  visually intersect a grown gyroid block — same tolerance the game already
  has for trails crossing trails.
- The cell density binding is **registration-time**: `UpdatePosition` does not
  re-resolve the containing cell when a mover (gyroid steering, fauna body —
  the latter excluded from this view anyway) crosses a cell membrane. Same
  behavior the old `Prism.RegisterWithCell` had; revisit only if movers start
  crossing cells at scale.
- The conic AOE tier is a **one-pass sweep over a mutating population**: `sweptTo`
  advances monotonically, so a depth band is queried exactly once. A prism that
  spawns into — or moves into — a band the cone already passed is never tested.
  (The spherical tier re-tests its whole ball each frame and does catch these.)
  Deliberate: re-querying the full cone every frame costs an emitted hit +
  `alreadyHit` probe for every prism in it, every frame, and the window is one
  blast long. Revisit if mass starts appearing inside live blasts at scale.
- The explosion backlog is the **one sanctioned holder of registry indices across
  frames**. It is safe only because `PendingExplosionHit` also captures
  `_slotGeneration[index]` (bumped by every `Register`) and the drain drops any
  entry whose stamp no longer matches. Any future consumer that outlives the frame
  needs the same guard — the raw index is not a stable handle, because the free
  list recycles slots LIFO. Note an object reference is **not** sufficient here:
  prisms are pooled, so the same instance can re-enter the same slot for a new
  life, and a Unity-destroyed reference compares fake-null, which would disable
  the check in precisely the case it exists for. Compare generations, not refs.
- `Boid.NewBlock` mound blocks bypass `Prism.Initialize` and therefore never
  register (no AOE, no occupancy, no neighborhood visibility) — mound
  mate-finding compensates with a Mound-layer collider probe (see "What NOT to
  use it for"). Routing mound blocks through the real `Initialize` lifecycle
  would retire that probe, but changes their layer/collider/grow behavior and
  must be its own tested change.
- The conic explosion's **vessel** hit volume is still a single leading
  cross-section, not the whole swept solid. Prisms go through the exact
  `ProcessExplosionConeFrame` slab, but explosion->vessel effects resolve through
  `AOEConicExplosion`'s trigger collider, which rides the leading BASE PLANE with
  the cross-section the Burst query is using there — since the capsule change, a
  `CapsuleCollider` of the core radius extended along the gape axis
  (`UpdateCapsuleTrigger`), so its SHAPE now matches the sweep instead of
  contradicting it. What it still does not cover is the volume BEHIND that plane:
  a vessel the wavefront already passed is only hit on the frame the plane
  reached it. The impact VECTOR is already apex-radial
  (`CalculateImpactVector` overrides to the cone container), so only WHO gets hit
  is affected. Fixing it means a full sweep containment test on the vessel path —
  a gameplay change to the blast's reach, so it wants its own branch and a play
  test, not a drive-by.

## Roadmap

| Phase | Scope | Status |
|---|---|---|
| 1 | Bucket grid + reservations; `GyroidAssembler`/`WallAssembler`/`SchwarzPAssembler` switch from `Physics.CheckBox` to `TryReserve`; lifecycle holes fixed (`Restore` re-entry, pool-return staleness, mover positions) | **Shipped** |
| 2 | `QuerySphere` neighborhood view replaces the remaining physics queries against prisms: `GyroidAssembler.FindClosestMate` (+ fixes its stale `OverlapSphereNonAlloc` array bug, keeps a Mound-layer probe for unregistered mound blocks), `WallAssembler` mate-finding (was allocating `OverlapSphere`; its `MoveMateToSite` now also upholds the movers contract), `ScoutTrailPrismScaler` (adaptive `IsAnyPrismWithin`), `LightFauna` (prisms via index, vessels via prism-masked physics), `Boid` prism-attraction + boid-neighbor scan (fully index-based). Fauna bodies uphold the movers contract via `Fauna.NotifyBodyPrismsMoved` — also fixes batch AOE hitting creatures at their spawn point. A planned `FindNearest` view was folded into callers' own scoring loops (every caller filters candidates with custom logic). | **Shipped** |
| 3 | Cell density grids driven by the index lifecycle: `Register`/`MarkRestored` bind the containing cell (`Cell.AddBlock`), `MarkDestroyed`/`Unregister` release it, `ForwardDomainChangeToCell` re-files steals. `Prism` lost `_registeredCell`/`RegisterWithCell`/`UnregisterFromCell` — every lifecycle moment makes ONE index call and the coarse view follows. Bonus consistency fix: restoring a prism that was killed inside its spawn window (never registered) now does a full `Register`, where the old code put it in the cell grids but left it invisible to AOE/occupancy. The flora ownership stream (`HealthBlockTracker`) remains a second, idempotent `Cell.AddBlock` contributor. | **Shipped** |
| 4 | Optional: bucket-accelerated AOE if profiling ever demands it. (A second index instance over fauna is no longer needed for current populations — fauna bodies are registered prisms and their senses already ride this index; it would only return if a non-prism fauna population appears.) | Candidate |
| 5 | Shell view: shape-precise shielded/super-shielded collision as a Burst query tier (`PrismShellData` + `ShellContactQueryJob` + `PrismShellContactManager`) — vessels/skimmers interact at the visible shell (exact octahedron / non-convex stella union) instead of the authored box, with the box trigger suppressed for shell-owned pairs. Replaces the abandoned trigger-resize + managed-narrowphase approach (`claude/skimmers-shielded-prisms-hek21c`), whose per-pair `OnTriggerStay` re-tests inside `Physics.SendEvents` were unbounded in shielded-prism density. | **Shipped** |

## Adding a consumer (checklist)

1. Does an existing query method answer your question? Use it.
2. Need a new query shape? Add it **to this class** as a method over `_buckets`
   / `_spatial` — do not build a parallel store.
3. Never cache registry indices across frames outside `Prism.SpatialIndexId`.
4. Never call `Register`/`Unregister`/`Mark*` from outside the `Prism`
   lifecycle methods listed above — single-writer per lifecycle event is what
   keeps the views consistent.
