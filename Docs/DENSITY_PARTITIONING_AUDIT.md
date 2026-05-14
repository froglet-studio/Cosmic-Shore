# Density Partitioning — Design Audit

**Status:** Audit + benchmark-driven redesign. **Fixes landed** in `c058663` — see §4.
**Branch:** `claude/audit-density-partitioning-2EvgR`
**Companion artifact:** `DensityPartitionBenchmarkRunner` (Edit-Mode scene + Toolbox "Density" tab).
**Acceptance criteria:** the curation rubric in `CLAUDE.md` → *Design Philosophy: Favor Emergent Systems Over Bespoke Solutions*.

> **Reading note.** Sections 1–3 and 5 are the original first-principles audit, preserved
> as written. Section 0 (this) and Section 4 (Recommendation) and Section 6 (Benchmark
> findings) were **rewritten after the benchmark ran** — the data falsified the original
> recommendation in §4 (a scene-wide `PrismAOERegistry` migration) and replaced it with
> three much smaller surgical fixes. Where §3.7 and the original §4 disagree with §4
> below, §4 wins; the earlier text is kept for the reasoning trail.

---

## 0. TL;DR

The audit started by suspecting the *algorithm* and the *storage architecture*. Five
benchmark iterations proved the real defects were simpler and more structural:

1. **The grid was the wrong size.** The shipped `BlockDensityGrid` is a hard-coded
   1000m cube anchored at world origin. The real Blob cell membrane is 1200m radius —
   a 2400m-diameter sphere. The grid saw ~14% of the cell's volume; every prism in the
   outer shell was silently dropped by the bounds check, invisible to `FindDensestRegion`.
   Fauna were never directed at outer-shell mass → it accumulated unconsumed → the
   "static equilibrium" symptom. **This is the root cause**, and no algorithm choice
   matters while the grid is blind to most of the cell.
2. **Raw count-argmax is noisy.** Even with a correctly-sized grid, argmax-over-raw-counts
   landed ~100m off the true peak (it locks onto single noise voxels). A separable
   3×3×3 box smoothing pass + sub-voxel parabolic interpolation drops that to ~28m —
   well inside the ~150m swarm consumption radius.
3. **Bucket staleness from `ChangeTeam`-after-`AddBlock`.** `HealthBlockTracker.Add` ran
   `cell.AddBlock` before `hp.ChangeTeam`, so pooled `HealthPrism`s were binned into the
   per-domain `countGrids` by their stale domain; the later `RemoveBlock` (correct domain)
   decremented different buckets, leaving phantom counts that drift the anti-domain answer
   over a long session.

**What was *not* needed:** the original §4 recommendation — replacing `countGrids` with a
scene-wide Burst job over `PrismAOERegistry` — turned out to be unjustified. The benchmark
showed the existing per-Cell `BlockDensityGrid` structure is fine; it was just mis-sized
and under-processed. The fix is three surgical changes to existing files, not a rewrite.
See §4.

**What's still open:** the original concerns about API shape (single-peak vs centroid vs
top-K, §1) and "anti-domain is a derived view, not a fundamental" (§2.3.4) remain valid
design observations but were *not* load-bearing for the static-equilibrium bug, so they're
deferred. The hierarchical fauna→swarm→population framing (§6) reframes "accuracy" — the
swarm covers a ~300m-diameter volume, so the algorithm only has to land within ~150m, which
all the smoothed variants now do.

---

## 1. What the consumers actually need

Every active consumer of the density API as of this branch. (Auditing this list is the *first* and most important step — if the API is the wrong shape, no amount of internal cleverness will help.)

| Consumer | File / line | Calls | What it does with the answer | Real shape it needs |
|---|---|---|---|---|
| **Fauna (legacy)** Level 1 (Restless / Frozen) | `Fauna.cs:108` | `cell.GetExplosionTarget(domain) + _goalOrbitOffset` | Each pack member seeks the returned point, but the orbit offset is what actually keeps them from clumping. | A **seek goal** that biases toward enemy mass but does not need to be the global max. A *region* (centroid of a cluster) would be more stable than a peak cell. |
| **Fauna (legacy)** Level 2 (Rabid) | `Fauna.cs:105` | `cell.GetDensestRegionAnyDomain()` | Pack converges tightly onto the densest region of any color. No orbit offset — the spec explicitly wants tight convergence. | A **single point** at the densest region across all domains. Peak is correct here. |
| **LightFauna** Restless / Frozen | `LightFauna.cs:137-138` | `cell.GetExplosionTarget(domain)` | Same as Fauna Level 1, but with per-frame avoidance via `Physics.OverlapSphere`. | Same — seek goal biased toward enemy mass. |
| **LightFauna** Rabid | `LightFauna.cs:139` | `cell.GetDensestRegionAnyDomain()` | Same as Fauna Level 2. | Single point at densest region across all domains. |
| **WormManager** | `WormManager.cs:88` | `cell.GetExplosionTarget(domain)` | Sets one worm-head target for the whole worm chain. | Single point. Stability matters more than precision — if the worm retargets every 0.5s onto a wildly different point, the worm thrashes. |
| **ShardToggleAction** | `ShardToggleAction.cs:45` | `cell.GetExplosionTarget(domain)` | `shardFieldBus.BroadcastPointAtPosition()` — every shard in the field points at the returned location. | Single point. A *direction* would be acceptable too — the consumer translates the point into a direction internally. |
| **AIPilot** | `AIPilot.cs:406` *(commented out)* | `cell.GetExplosionTarget(activeCell.ControllingTeam)` | Was: "pick a target near the winning team's mass." Currently disabled. | Single point (when re-enabled). |
| **TestHarnessOctreeDensitySearch** | `TestHarnessOctreeDensitySearch.cs:36-38` | `cell.GetExplosionTarget(t)` for each team | Smoke-test prints. | N/A — diagnostic. |

### Observations from the consumer audit

1. **Every active consumer wants exactly one Vector3.** "Top-K" and "gradient field" are speculative future needs the audit shouldn't over-design for. Three similar lines is better than a premature abstraction (CLAUDE.md).
2. **Three of seven callers** (`Fauna` L1, `LightFauna` L1/2) operate inside a per-fauna behavior tick that *also* runs `Physics.OverlapSphere` for separation/avoidance. Those callers pay the per-Cell-grid cost on top of a per-fauna physics scan — the density query is not the bottleneck for their frame budget, *but* its update cadence matters: if the goal flips between two equally-dense regions on adjacent ticks, the pack oscillates.
3. **No consumer needs the per-Cell scoping** for correctness. They all happen to live on `Cell` because that's where the grid happens to be allocated. Worms and shards already have a `domain` (not a cell), and the `cellData.Cell` lookup is incidental.
4. **Fauna L1's `+ _goalOrbitOffset`** is the system's actual answer to "don't pull the whole pack onto one point." The peak-finding code below isn't doing pack-spreading; the orbit offset is. So if the audit shows we can return *a stable region* instead of *the absolute peak*, fauna behavior is no worse.

### Implication for the API

A correct minimum-viable API is **two methods**:

```
Vector3 GetEnemyMassTarget(Domains seeker)       // anti-D peak, fauna L1 + worms + shards
Vector3 GetAllMassTarget()                        // any-D peak,   fauna L2
```

That's exactly the existing `Cell.GetExplosionTarget` / `GetDensestRegionAnyDomain` signature, so consumers don't change. What changes is **where the answers come from** and **how reliable they are**.

---

## 2. What the current system actually computes — and where it fails

### 2.1 The substrate that exists on this branch

- `BlockDensityGrid.cs` defines a 17³ regular grid (`Stride = 60`, `totalLength = 1000`, `nGridPointsPerDimension = 1000/60 + 1 ≈ 17`).
- `BlockCountDensityGrid : BlockDensityGrid` overrides `AddBlock` / `RemoveBlock` to increment/decrement a `byte` count per voxel.
- `FindDensestRegionJob` (Burst, single-thread `IJob`) does a linear scan of all 17³ ≈ 4913 voxels and returns `argmax`.
- `Cell` owns a `Dictionary<Domains, BlockCountDensityGrid>` keyed by `{Jade, Ruby, Gold, Blue}`. Where `D ∈ {Jade,Ruby,Gold}`, `countGrids[D]` accumulates every prism **not** in domain `D`. `countGrids[Blue]` accumulates every prism regardless of domain. (`Cell.cs:399-411`.)

### 2.2 What the system gets right

- **Bounded work per query.** `FindDensestRegion` scans a fixed 4913-cell grid. Cost is independent of prism count.
- **Bounded write cost.** `Add/RemoveBlock` is O(1) (one index lookup + byte ±= 1).
- **Burst-compiled inner loop.** The argmax job is `[BurstCompile]`.
- **Sensible fallback.** Empty grids return `GetCellAnchorPosition()` (`Cell.cs:529-575`) — fauna head toward the crystal instead of the world-space −X/−Y/−Z corner.

### 2.3 Where the system fails (in order of severity)

#### 2.3.1 Bucket staleness from `ChangeTeam`-after-`AddBlock` (correctness bug)

`HealthBlockTracker.cs:30-41`:

```csharp
public void Add(HealthPrism hp, LifeForm owner, Domains domain)
{
    if (!hp) return;
    if (healthBlocks.Add(hp) && cell)
        cell.AddBlock(hp);     // <-- AddBlock runs first
    hp.ChangeTeam(domain);     // <-- domain is set AFTER the block is registered
    ...
}
```

Inside `Cell.AddBlock` (`Cell.cs:481-501`):

```csharp
Domains[] teams = { Domains.Jade, Domains.Ruby, Domains.Gold };
foreach (var t in teams)
    if (t != block.Domain) countGrids[t].AddBlock(block);
```

At Add time, `block.Domain` is whatever the prism currently reports — which for a pooled `HealthPrism` is typically `Domains.Blue` (the "no team" sentinel returned by `PrismTeamManager` before `SetInitialTeam` runs). Since `Blue ∉ {Jade, Ruby, Gold}`, **the prism is incremented into all three buckets**.

`hp.ChangeTeam(domain)` then sets the true domain. Later, when the prism dies, `Cell.RemoveBlock` runs the same loop but with the *new* domain, so it decrements only two of the three buckets. **The third bucket — the one corresponding to the prism's eventual domain — is incremented but never decremented.**

For a long-running ecosystem (Menu_Main idle), this is exactly the failure mode described in the brief: "the three colored anti-domain markers still drift to locations that don't correlate with where the actual mass of opposing teams is." Anti-Jade ends up biased toward wherever Jade prisms used to be and were never removed from the Jade-keyed bucket.

The fix is independent of the larger algorithm choice: either snapshot the domain at AddBlock time (`Dictionary<Prism, Domains> _addTimeDomains` so RemoveBlock uses the same key), or reorder `HealthBlockTracker.Add` to call `ChangeTeam` *before* `cell.AddBlock`. **The benchmark scene must reproduce this with a deterministic `ChangeTeam`-after-Add scenario so any algorithm proposal is also tested against the staleness path.**

#### 2.3.2 Count vs. mass

The grid increments by `1` per prism regardless of `prismProperties.volume`. Two failure modes:

- **Stage-2 health prisms** (large body prisms on mature fauna) count the same as **stage-0 sprouting cytoplasm prisms** that are still animating from scale 0 to their target.
- **A cluster of 10 small prisms** outweighs **1 huge prism** even when the volume balance is reversed.

For "where is the enemy *mass*?" — which is what consumers actually care about (fauna seeking enemy biomass to consume, shards pointing at threat density, worms targeting the densest enemy cluster) — count is the wrong primitive. `PrismAOERegistry._damage[idx].Volume` is already cached and updated on `UpdateVolume(index, volume)`, so volume-weighting is one swap away.

#### 2.3.3 Single-best-cell aggregation is brittle at the boundary

`FindDensestRegionJob` returns `argmax` over 4913 voxels. If two voxels are within ±1 of each other, the answer flips between them as prisms are added/removed — with no kernel smoothing in this branch's substrate, the peak can jitter by `Stride = 60m` per tick.

When the task brief talks about "kernel-smoothed peak search" that already exists in a sibling branch, what it means is presumably a 3×3×3 box smoothing pass before argmax. That's a reasonable cushion against single-cell jitter but does not solve the **two-cluster** case: if `cluster A` has 5 prisms and `cluster B` has 4 prisms and they're 200m apart, the peak picks A, and a fauna pack heading to A misses the equally-relevant B. The orbit offset (`_goalOrbitOffset = 60m`) can't close that gap.

#### 2.3.4 Per-Cell scoping is incidental, not fundamental

The grid is allocated inside `Cell.SetupDensityGrids()` because that's where it's been since the original prototype (`TestHarnessOctreeDensitySearch.cs` referenced `targetNode.countGrids[t]` on a per-Cell basis). But the **prism population is scene-wide**: a prism in Cell A can be visible from Cell B, and `Cell.ContainsPosition()` only checks "is the world-space point inside the membrane radius" — there is no enforcement that `Cell.AddBlock` is only called with prisms inside that cell.

A scene with N cells therefore has N × 4 grids × 4913 voxels = O(80,000) voxels' worth of bookkeeping that is roughly redundant (every prism is incremented into at most 4 of those N grids — the one cell it's "inside" — but the per-Cell allocation pays the same memory cost regardless). And consumers all answer cell-local queries by routing through `cell.GetExplosionTarget(...)`, which only looks at *one* cell's grid — so prisms in adjacent cells are invisible to a fauna that should be aware of them.

CLAUDE.md lists *Cells (with `CellType`)* as a fundamental. Cells *as territorial units* are fundamental. But "the partition grid for spatial-density queries lives inside the cell" is *not* a fundamental — it's an implementation choice. A scene-wide partition that *answers cell-local queries* by intersecting with the cell's membrane preserves the cell fundamental while removing the redundant per-cell storage.

#### 2.3.5 Parallel storage system — duplicates `PrismAOERegistry`

`PrismAOERegistry` already maintains:

- `_spatial[i].Position` — float3, updated... actually **never** updated after `Register`. ⚠️ Open question (§5).
- `_spatial[i].Flags` — `IsActive`, `Destroyed`, shield bits.
- `_damage[i].Volume` — current volume (refreshed by `UpdateVolume`).
- `_damage[i].Domain` — current domain (refreshed by `UpdateDomain`).

It is a flat `NativeArray<PrismSpatialData>` + `NativeArray<PrismDamageData>` with a free list. The Burst job `AOESpatialQueryJob` already scans the hot array with a position predicate (`distSq <= radiusSq`) in parallel.

A density query is **exactly the same kind of computation** as an AOE hit query — both want "for every active prism, do a predicate against its position." The cost of running a Burst job over `_highWaterMark` prisms once per density tick is negligible compared to the bookkeeping cost of maintaining a separate per-Cell-per-Domain count grid that *also* has to track the same lifecycle (register, change-team, destroy).

**Per the CLAUDE.md curation rubric (point 4: prefer extension over addition):** the partition grid is a special case of "scan the prism registry with a predicate." It should be expressed as such, not as a parallel storage system.

### 2.4 Static-equilibrium ecosystem (downstream symptom)

The brief notes: *"The ecosystem reaches a static, uninteresting equilibrium instead of oscillating through aggression / growth phases."*

This is a symptom of the failure modes above interacting with `LightFauna`'s feedback loop:

- Fauna seek the (wrong, stale) anti-domain target → land near a region with no actual enemy mass → `Physics.OverlapSphere` finds no consumable prisms → cell phase never escalates from Quiet → fauna are stuck at Level 0 cadence.
- Cells that *do* have phase escalate to Restless get the right target, briefly consume the enemy mass, drop back to Quiet, and the cycle plateaus.

The audit doesn't need to verify that explicitly — the benchmark proves the proximate failure (target accuracy), and the proximate failure is sufficient to explain the equilibrium symptom.

---

## 3. Alternatives, evaluated

Each candidate is evaluated on: which fundamental it leans on, whether it duplicates state, the cost vs. accuracy profile, and how it interacts with the existing fundamentals (Domain, Mass, Cells, Elementals, Prisms, Flora & Fauna, Vessels).

### 3.1 Regular grid + argmax (current)

| Aspect | Verdict |
|---|---|
| Fundamental leaned on | None — bespoke storage. |
| Duplicates state | Yes — every prism is in both `PrismAOERegistry` and `Cell.countGrids`. |
| Add/remove cost | O(1). |
| Query cost | O(V) where V = voxel count (4913 fixed). Burst single-threaded. |
| Accuracy | Cell-quantized; ±Stride/2 = ±30m on a single peak; brittle on multi-cluster. |
| Correctness | Currently broken by 2.3.1; even after that fix, fails 2.3.2 (count vs. mass). |

### 3.2 Regular grid + argmax + 3×3×3 smoothing kernel

| Aspect | Verdict |
|---|---|
| Fundamental leaned on | None — same bespoke storage. |
| Add/remove cost | O(1). |
| Query cost | O(V) for smoothing pass + O(V) for argmax. Still bounded, still Burst-able. |
| Accuracy | Smoothing reduces single-cell jitter; ±Stride/2 still. Multi-cluster still broken. |
| Correctness | Same staleness + count-vs-mass issues as 3.1. |

This is presumably what the sibling branch implemented. It's a *symptom-patch* on top of 3.1, not a root fix.

### 3.3 Regular grid + centroid (weighted)

| Aspect | Verdict |
|---|---|
| Fundamental leaned on | None — bespoke. |
| Add/remove cost | O(1) per voxel; centroid is `sum(pos * count) / sum(count)`. |
| Query cost | O(V) per axis = O(3V). Burst-able. |
| Accuracy | Stable to cell quantization. **Fails on bimodal distributions** — returns the empty middle between two clusters. |

Worth implementing as a comparison baseline. Cheap. Will be visibly wrong on `MultiCluster` scenarios in the benchmark.

### 3.4 Octree

| Aspect | Verdict |
|---|---|
| Fundamental leaned on | None — bespoke. |
| Duplicates state | Yes — separate octree nodes. |
| Add/remove cost | O(log N). |
| Query cost | O(log N) for nearest, O(N) for densest-region. Burst-friendly only for fixed-depth pre-allocated octrees. |
| Accuracy | Higher resolution where prisms cluster; wasted nodes where they don't. |
| Memory | Dynamic — can blow up under bursty growth. |

There is already a `TestHarnessOctreeDensitySearch.cs` in `_Scripts/Utility/ChoppingBlock/` — a prior octree exploration that was abandoned. It calls the *existing* `GetExplosionTarget`, so the harness is just a smoke test, not an octree implementation. The chopping-block name signals the team already considered and rejected this direction.

### 3.5 KD-tree

| Aspect | Verdict |
|---|---|
| Fundamental leaned on | None — bespoke. |
| Add/remove cost | KD-trees are notoriously bad at mutable insert — typically rebuilt periodically. |
| Query cost | O(log N) nearest; O(N) for global densest-region. |
| Burst-friendliness | Native trees are pointer-heavy → Burst-hostile without a manual array-backed implementation. |

KD-tree is **the wrong tool** for "densest region of a moving point cloud." It's a *nearest-neighbor* structure. Reject.

### 3.6 Mean-shift on points

| Aspect | Verdict |
|---|---|
| Fundamental leaned on | None — bespoke. |
| Add/remove cost | Zero (no index). |
| Query cost | O(N × k × iter) where k = local neighborhood, iter = convergence (3-10). |
| Accuracy | Returns *the* local maximum on a smoothed density field; correct on multi-cluster (pick one seed near each cluster). |
| Burst-friendliness | Parallel-for over points. Excellent fit. |
| Notes | Need a seed strategy (random sampling, last-tick result). |

This is a strong candidate. The cost scales with prism count rather than voxel count, so at low N (early game) it's cheap; at high N (late game) it's bounded by k and iter, both small. Crucially: **mean-shift naturally handles bimodal distributions** if seeded with K starts.

### 3.7 Scene-wide Burst job over `PrismAOERegistry` (no grid storage)

| Aspect | Verdict |
|---|---|
| Fundamental leaned on | **`PrismAOERegistry`** — the existing scene-wide registry. |
| Duplicates state | **No.** |
| Add/remove cost | Zero — registry already maintains. |
| Query cost | O(N) per density tick. With N = 2000 and Burst SIMD over 16B-packed `PrismSpatialData`, this is ~0.3-1.0ms estimated. |
| Accuracy | Whatever the in-job reduction picks. Can be a peak, a centroid, top-K, or a gradient field — the choice is independent of storage. |
| Burst-friendliness | Native. The hot array already exists. |

**Strongest candidate by CLAUDE.md curation criteria** — it leans on an existing fundamental, doesn't duplicate state, and the reduction step is where the per-consumer flexibility lives.

The job design splits into two phases, mirroring `AOESpatialQueryJob`:

1. **Phase A (Burst, parallel):** For each prism in `_spatial`, if `Flags & JobSkipMask == JobPassValue` and (optional) `_damage[i].Domain ∈ allowedSet`, contribute `_damage[i].Volume` to a coarse 3D histogram (e.g. 32³ voxels over a fixed world bound). Use `NativeArray<float>` with `[NativeDisableContainerSafetyRestriction]` + atomic-add (or per-thread buffers + a reduce pass).
2. **Phase B (Burst, single-thread `IJob`):** Argmax (or kernel-smoothed argmax, or top-K) over the histogram.

For a per-cell answer, the consumer passes the cell's `transform.position` and `MembraneRadius`; the reduction-job ignores buckets outside that sphere.

Per-domain histograms are pre-built by running phase A four times (Jade, Ruby, Gold, Blue=all) — or, smarter, in one pass with one histogram per active domain (4×32³ = 131,072 floats = 512 KB; fits in L2). Anti-D is then `hist_Blue - hist_D`, a one-pass subtraction. This is the "build mass once, derive anti-D as a view" structure that §0 referred to.

### 3.8 Hierarchical multi-resolution

A coarse 8³ grid + a fine 32³ grid where the coarse grid points to fine sub-grids. Useful when N is so large that the fine grid is too expensive to scan in full each tick. At N ≤ 5000 (the practical Menu_Main range), unjustified complexity. Defer.

### Summary table

| Approach | Fundamental | Dup state | Accuracy on multi-cluster | Burst cost (est., N=2000) | Verdict |
|---|---|---|---|---|---|
| 3.1 Grid + argmax (current) | bespoke | yes | ❌ | <0.1ms | broken (2.3.1, 2.3.2) |
| 3.2 Grid + smoothed argmax | bespoke | yes | ⚠️ partial | <0.2ms | symptom-patch |
| 3.3 Grid + centroid | bespoke | yes | ❌ (empty middle) | <0.1ms | benchmark baseline |
| 3.4 Octree | bespoke | yes | ✅ | varies | considered, rejected |
| 3.5 KD-tree | bespoke | yes | ❌ (wrong tool) | varies | reject |
| 3.6 Mean-shift on points | bespoke | no | ✅ | ~0.5-1.0ms | strong candidate |
| **3.7 Job over `PrismAOERegistry`** | **registry** | **no** | ✅ (peak / centroid / top-K all derivable) | **~0.3-1.0ms** | **recommended** |
| 3.8 Hierarchical | bespoke | yes | ✅ | <0.5ms | defer |

---

## 4. Recommendation — what was actually done

> The original §4 recommended replacing `countGrids` with a scene-wide Burst job over
> `PrismAOERegistry`. **The benchmark falsified that as unnecessary.** The per-Cell
> `BlockDensityGrid` structure is sound; it was mis-sized and under-processed. The actual
> fix is three surgical changes to existing files — landed in commit `c058663` — not a
> rewrite, not a new singleton, not a `PrismAOERegistry` migration.

### 4.1 Fix 1 — size the grid to the cell (`BlockDensityGrid` + `Cell`)

The shipped `BlockDensityGrid` hard-coded `totalLength = 1000f`, `Stride = 60f`, and
`origin = -totalLength/2` (world origin). On a 1200m-radius cell that covered ~14% of the
volume and was off-centre besides. **Changed:**

- `BlockDensityGrid.Init(Domains, Vector3 cellCenter, float worldDiameter)` — the grid now
  covers a cube of side `worldDiameter` centred on `cellCenter`. Resolution stays fixed at
  17³ (`GridPointsPerDimension`); the **stride is derived** (`totalLength / 16`) so the grid
  scales with the cell instead of the cell having to fit the grid.
- `Cell.SetupDensityGrids()` passes `transform.position` + `MembraneRadius * 2` (fallback
  2400m if the membrane prefab is missing).
- `Cell.Initialize()` reordered: `SpawnVisuals()` now runs **before** `SetupDensityGrids()`
  so the membrane GameObject exists when `MembraneRadius` is read.
- Existing grids are `Dispose()`d before recreation (they hold persistent NativeArrays).

The benchmark's `GridSmoothedInterp17` row proved 17³ at cell-derived stride (~150m for the
Blob cell) is sufficient — ~28m median error, inside the swarm radius. No 32³ bump, no
finer grid.

### 4.2 Fix 2 — smoothing + sub-voxel interpolation (`FindDensestRegionJob`)

Raw `argmax` over byte counts locked onto single noise voxels (~100m error even with a
right-sized grid). The Burst job now runs the proven pipeline:

1. **Separable 3D box filter** (sliding-window, O(N³) per pass — independent of kernel
   width). Kernel half-width derived from `SmoothingRadiusMeters = 150f` ÷ stride.
2. **Argmax** over the smoothed field.
3. **Parabolic sub-voxel interpolation** around the peak — the answer is no longer
   quantized to the (now coarse, cell-sized) stride.

Output is a world-space `float3`. `Cell.GetExplosionTarget` / `GetDensestRegionAnyDomain`
are unchanged — they still call `FindDensestRegion()` and still validate via
`GetDensityAtPosition` (now bounds-guarded, since the result is sub-voxel).

### 4.3 Fix 3 — `HealthBlockTracker.Add` ordering (§2.3.1)

Two-line reorder: `hp.ChangeTeam(domain)` now runs **before** `cell.AddBlock(hp)`, so
`Cell.AddBlock` reads the correct domain when binning the prism into per-domain grids.
Eliminates the phantom-count drift described in §2.3.1.

### 4.4 What this deliberately did *not* do

- **No `PrismAOERegistry` migration.** The benchmark showed the per-Cell grid is fine once
  sized correctly. Migrating would have been weight for no measured benefit — exactly the
  "don't add a parallel fundamental" caution from CLAUDE.md's curation rubric, applied in
  reverse: the existing structure *is* the right fundamental, it was just broken.
- **No API change.** Every fauna / worm / shard caller compiles and behaves the same; only
  the answers got better.
- **No new `DensityPartitionSystem` singleton, no per-consumer reduction enum.** Deferred —
  see §5. The single kernel-smoothed peak is good enough for every current consumer once
  the grid sees the whole cell.
- **No network sync.** Still out of scope; host-authoritative local compute, as before.

### 4.5 Per-consumer reduction shape — deferred, not rejected

§4.3 of the original audit proposed per-consumer reductions (centroid for fauna L1, argmax
for L2, etc.). The benchmark's hierarchical analysis (§6) showed this is **lower priority
than it looked**: the swarm's emergent consumption volume (~300m diameter) dwarfs the
difference between "peak" and "centroid" answers (~10-30m apart). Revisit only if a future
consumer (e.g. a vessel ability that needs a precise point, or top-K for multi-pack
splitting) actually needs it. For now, one smoothed-peak answer serves everyone.

---

## 5. Open questions

These surfaced during the audit and should be answered before / during implementation.

1. **`PrismAOERegistry._spatial[i].Position` is never updated after `Register`.** Is this intentional (prisms are mostly stationary trail blocks, and per-frame position-sync would cost more than it's worth) or a bug (drifting prisms / fauna body prisms have stale registry positions)? Affects accuracy: if positions drift, the partition histogram is wrong by however far each prism has moved. The benchmark scenarios use stationary prisms so this isn't tested.

2. **Should histograms use a fixed world-bound or follow the active set?** A fixed `±500m` bound (matching `BlockCountDensityGrid.totalLength = 1000`) is simple and the same as today. An adaptive bound (recompute from min/max of active positions each tick) is more accurate at low N but adds a reduction pass.

3. **Histogram resolution.** 32³ = 32,768 floats × 4 domains × 4 bytes = 512 KB. Comfortable. 64³ = 4 MB — overkill, but if we want to match `BlockCountDensityGrid`'s 60m stride at the same world bound (1000/60 = 17), 32³ at 1000m gives ~31m stride, finer than today. Good.

4. **Reduction per-consumer or globally?** §4.3 suggests per-consumer. The alternative is "the system always returns the kernel-smoothed argmax and the consumer post-processes if needed." Lower API surface but pushes complexity outward. Decide before implementing.

5. **What replaces `Cell.LiveBlockCount`?** That's a different signal — number of unique prisms tracked through Add/RemoveBlock — and it drives `CellPhase` transitions. The recommended changes affect the *spatial query* part of `Cell.countGrids`, not the *count* part. We could keep `trackedBlocks` (a `HashSet<Prism>` on Cell) as-is and only retire `countGrids`. Cleaner: that `HashSet` is independent of the spatial-query bookkeeping.

6. **`Cell.DominantDomain`** depends on `domainBlockCounts` (per-Cell domain → int). That's also independent of `countGrids` and can stay. The audit does not touch it.

7. **`addTimeDomains` referenced in the brief.** The brief mentions a snapshot-fix already applied; this branch does not contain it. Either it was applied on the absent `claude/density-partitioning-sync-6OXTO` branch and didn't merge here, or it's a planned fix the brief is forecasting. The audit's recommendation makes that snapshot moot — under the new model, `Cell.AddBlock` doesn't touch any per-domain grid, so the staleness window goes away. **But** the ordering fix in §4.4 step 1 is still required as long as `Cell.AddBlock`/`RemoveBlock` exist and other consumers (Prism.cs:226 has a commented-out CellControlManager path that still references `targetCell.countGrids[t].AddBlock(this)` — confirm dead code before removal).

---

## 6. What the benchmark proved

The companion `DensityPartitionBenchmarkRunner` ran five iterations. Each iteration
advanced the tool along three axes — diagnostic capability, expected-vs-measured accuracy,
and cost — and the data repeatedly overturned the audit's assumptions. The trail:

**Iteration 0 — first numbers, wrong scale.** Surfaced that the original §4 recommendation
(`MassHistogram` over `PrismAOERegistry`) was *worse* than the current grid, and that the
sibling-branch's smoothing was *better* than the audit claimed. First sign the audit's
"needs a rewrite" premise was off.

**Iterations 1–3 — sharpening the tool.** Faster histogram-based ground truth (~100×),
sub-voxel interpolation, a Peaked/Diffuse scenario split, a box-kernel `mass%` fix, a
stability metric, mean-shift variants, and a jitter (`worstMs`) column. Net finding: at the
benchmark's *then-current* 500m world scale, the algorithm choice barely mattered — every
smoothed+interp variant clustered around 10-35m. The audit's algorithm anxiety was
misplaced.

**Iteration 4 — the real cell scale.** Re-anchored every scenario to `worldHalfExtent =
1200m` (the actual Blob membrane radius) and added `ProductionFixedGrid17` — the *literal*
shipped algorithm including the hard-coded ±500m bounds. **This is where the root cause
showed up:**

- Grid-coverage diagnostic: the shipped ±500m grid sees **2-37%** of a cell-scale
  scenario's prisms. `OuterShellCluster` → 2%.
- `OuterShellCluster_r150`, anti-Jade: `ProductionFixedGrid17` Δ = **350.6m, mass 7%**
  (found a 2-prism noise voxel). `GridArgmax17` — *identical algorithm, grid sized to the
  cell* — Δ = **16.8m, mass 80%**. A 20× collapse purely from grid sizing.
- The headline median *hid* this (102.5m vs 102.6m) because raw count-argmax fails
  everywhere anyway. The bug is invisible in the median, lethal per-scenario — which is
  exactly how it stayed hidden in the running game.

**The two-problems verdict.** Per-knob deltas at cell scale:

| Change | medianΔ | |
|---|---|---|
| `ProductionFixedGrid17` (shipped) | 102.5m | grid blind to 63%+ of cell |
| → size grid to cell | 102.6m | median unchanged, but fixes catastrophic per-scenario failures |
| → + 3³ smoothing | 68.7m | −34m |
| → + sub-voxel interp | **27.6m** | −41m — biggest single win |
| → + mass-weighting | 27.6m | negligible on its own |
| → + 32³ resolution | 17.7m | −10m, 5× the cost — not worth it |

Both fixes are independently necessary: sizing the grid stops the catastrophic
per-scenario blindness; smoothing+interp fixes the raw-argmax noise. Together =
`GridSmoothedInterp17` at **27.6m / 0.22ms** — 3.7× more accurate than shipped, 2× the
cost (trivial at fauna's 1-5s query cadence), 17³ resolution unchanged. That is exactly
what §4 landed.

**Hierarchical reframing (the accuracy target).** The fauna→swarm→population analysis
established that the algorithm's accuracy budget is the *swarm-effective consumption
volume*, not per-fauna precision: `consumeRadius` 40m + `goalOrbitRadius` 60m + boid
separation spread ≈ a 150m-radius region, ~300m diameter. The algorithm only has to land
within ~150m of enemy mass; the swarm sweeps the rest. 27.6m clears that with margin. This
is also why the original §1/§4.3 "peak vs centroid vs top-K" question is deferred — the
swarm volume dwarfs the difference.

**§2.3.1 staleness — partially reproduced.** A static re-tag didn't move the anti-D answer
(any non-X prism counts regardless of tag). A *dynamic* model — phantom Blue duplicates at
real prism positions — does show drift on the accurate algorithms (anti-Ruby ~500m on
`DomainSegregated_K3_stale30`), confirming the ordering fix is worth doing. The benchmark
can't fully model the running-game Add/Remove cycle, so the ordering fix rests partly on
the static code analysis in §2.3.1 — but it's a two-line, independently-correct change, so
the bar for shipping it is low.

**What the benchmark could not settle** — see §5: `PrismAOERegistry`'s stale positions, the
`TwoEqualClusters` cluster-flip (resolution-bound, and arguably *beneficial* pack
distribution at the population level, not a bug), and whether the two-frequency oscillation
actually emerges — that last one needs a temporal ecology simulation, queued as the next
benchmark iteration.

The benchmark scene remains the place to slot in future variants without touching
production code; the report format (deterministic, every number has units + a target,
diff-friendly) is the falsifiable contract for any further change.
