---
name: ecology
description: Use for ANY change to the Cosmic Shore cell ecosystem — flora, fauna, cells, crystals, spawn profiles (SpawnProfileSO / CellConfigDataSO), phase/volume (Cell.LiveVolume, CellPhaseThresholds), lifeform powerups (LifeFormCrystal / ElementalCrystal*), wither/starvation, predator-prey, reproduction, evolution, or biome/intensity tuning. Loads the locked invariants + the collider-budget gate + the change protocol so changes start from the correct design and stay performant. Trigger when editing Assets/_Scripts/Controller/Environment/** (Cell, RandomLifeSpawner, Flora*, Fauna, LightFauna), the ecology SOs, or Docs/ECOSYSTEM*.md.
---

# Ecology Change Protocol

You are changing the Cosmic Shore cell ecosystem — a **platform fundamental** on the path to
credible artificial life. The system's design intent has caused repeated rework when guessed at;
this protocol exists to prevent that. Follow it exactly.

## 1. Read the canon first
- `CLAUDE.md ▸ Ecosystem Design Principles (LOCKED)` — the invariants (authoritative).
- `Docs/ECOSYSTEM_MASTERPLAN.md` — north star, the artificial-life scorecard (§3), the **collider
  contract (§4)**, the platform-wiring plan (§5), the phased roadmap (§6), the orchestration (§7).
- `Docs/ECOSYSTEM.md` — the mechanics log (how the current system actually works).

## 2. Restate before you edit (this kills the #1 source of rework)
In one or two lines, state which invariants the change touches and confirm it violates **none**:
**continuity of existence** (nothing pops in/out — everything grows/fades/suctions/withers; PLATFORM-WIDE) ·
no imposed death/decay/lifespan · no domain asymmetry (controlling-color spawn only) ·
wither-to-crystal + mass conservation · volume is the spine (not count) · the lifeform→elemental-
crystal invariant · territorial permanence (don't cull the dominant canopy) · endogenous
selection only (survival = fitness, never a scripted fitness function) · the collider budget.
**If a change might violate one, STOP and ask (AskUserQuestion). Do not guess the design.**

## 2.5 When sign-off IS granted — landing a carve-out that neither leaks nor gets reverted

§2 ends at "STOP and ask." Sometimes the answer is **yes, break it** (the Wanderway rolling
tether, `Docs/ECOSYSTEM.md` §0, is the worked example: recycling trail mass to buy a truly
infinite runner at fixed memory). A granted exception is not a free hand — it is a *fenced*
one, and the fence is half code, half record. Do both.

**Fence it in code.** The exception must be impossible to invoke by accident:
- **One caller, reachable only from the feature.** The removal/override API gets exactly one
  call site, inside the feature's own runtime object. Grep and confirm it before you commit.
- **No knob on the shared system.** Do NOT add a cap/TTL/limit field to the general-purpose class
  (`VesselPrismController` grew no `maxTrailBlocks`). The feature reaches in from outside and only
  while it is live; the shared system stays innocent.
- **The API's own doc-comment names the exception, the sole caller, and the doc that records it** —
  so the next reader who finds it by grep learns it is fenced before they reuse it.
- **Waive only the invariant that was actually granted.** Continuity of existence is a *separate*
  law from mass conservation: a sanctioned removal still has to wither/suction/fade out. Check each
  invariant in the §2 list independently rather than treating "approved" as blanket.

**Record it in three places** (a carve-out recorded once reads as a bug the next time someone greps):
1. `Docs/ECOSYSTEM.md` §0, **beside the rejected version it resembles** — state what it does, the
   reason it was granted, and the fence. Without this, the next session finds the mechanism, matches
   it to the rejected cheat, and reverts it.
2. `CLAUDE.md`, on the invariant's own bullet — the absolute wording ("there is no context in
   which…") needs the exception attached to it or it reads as a contradiction.
3. The system's `Docs/<System>/ARCHITECTURE.md`.

**Frame it as an exception, never a precedent.** Say plainly that it holds *because it was asked
for*, that the protocol still stands, and that the next one needs its own sign-off. Then go find
what the carve-out silently broke — see the traps below.

## 2.6 Prism / trail traps (each of these cost real time)

- **A vessel lays TWO ribbons.** `VesselPrismController.Trail` is only half the trail; the
  double-trail spawn pattern puts every other prism in `SecondaryTrail` (`Trail2`). Anything
  reasoning about "the vessel's whole trail" — length, mass, cleanup, recycling — must walk both,
  or it silently misses half the mass and any budget it enforces never converges.
- **Cached trail indices go stale on front-removal.** `TrailFollower` caches `attachedBlockIndex`
  and advances it itself; removing from the head of a `Trail` shifts every survivor and the rider
  starts racing forward along the ribbon. `Trail.OnOldestRemoved` exists for exactly this — any new
  index-cacher must ride it (hold a prism reference instead, where you can).
- **`OnReturnToPool != null` is the canonical pooled-vs-instantiated test.** It is how `Cell` tells
  a vessel's loose trail mass from instantiated environment mass (flora health prisms, a toy
  conveyor's transported stock). Use it before recycling anything; an unpooled prism handed
  `ReturnToPool()` silently stays in the world as an invisible collider.
- **Continuity-preserving removal, the recipe:** stamp `prism.TargetScale = <near-zero>` (the
  setter IS the grow-clock stamp — one write, GPU runs it), wait the wither duration, *then*
  `ReturnToPool()`. Never pool-return a prism at full scale; that is a pop.
- **The Cell's own visuals are single-instance fields, and the spawn chain reads them too
  early.** Two traps, one root: `Cell` holds ONE `membrane` / `nucleus` / `spawnedCytoplasm`
  and every cleanup path reads only the field, so any *unguarded* re-`Instantiate` (a repeat
  `Initialize`, a lazy-init nudge) orphans the previous one — it renders on top of the real
  one and nothing can ever collect it. Guard each spawn on its own field. And do not size one
  by hand: **a new core size means a new `CellConfigDataSO` pointing at a resized prefab**,
  never a scene-placed copy, a `localScale` tweak on the shared prefab, or a scene override
  (`Docs/ECOSYSTEM.md` §13.1).
  Reading the radius is the second half: `CellRuntimeDataSO.Cell` is assigned *inside*
  `Cell.Initialize`, which runs on `OnInitializeGame` behind `InitDelayMs` (1000 ms), while
  vessels spawn at `preSpawnDelayMs` (200 ms) and AI at `OnNetworkSpawn` (t≈0). Anything
  placing objects relative to the core during the spawn chain must use
  `Cell.FindByRuntimeData` (static registry, joined in `OnEnable`) and
  `Cell.ExpectedNucleusWorldRadius` (measures the config's prefab asset, no instantiate) —
  `cellData.Cell` is null then and `NucleusWorldRadius` returns 0, and a fallback built on
  either silently placed every player *inside* the nucleus.
- **A visual state applied before `Prism.IsCreationComplete` is part of BIRTH and must snap.**
  Engaging a morph there holds the exotic-visual window across the creation reveal and eats the
  one-shot grow stamp, so the prism snaps in instead of blooming (`Docs/PRISM_ANIMATION.md` §4).

## 3. Implement (emergence first, surgically)
- **Favor emergence:** never hard-code an outcome that should emerge from the fundamentals
  (Domain · Mass/prisms · Cells · Elementals · Flora & Fauna · Vessels) interacting. A scripted
  outcome is the same bug as a scripted fitness function — it breaks the gameplay *and* the
  artificial-life claim. Order of preference: use a fundamental → tune it → extend it →
  (with sign-off) propose a new one → bespoke only as last resort.
- **Config-driven:** tunables in ScriptableObjects; cross-system comms via SOAP events/variables;
  no singletons/static events. Variety = biome × intensity × heritable traits, not bespoke code.
- **Surgical:** match surrounding style; three similar lines beat a premature abstraction.

## 4. Respect the collider budget (HARD GATE — perf is collider-bound)
- State the change's impact on **active colliders per cell**.
- Prefer the Burst `BlockDensityGrid` / `PrismSpatialIndex` (`QuerySphere` /
  `IsAnyPrismWithin` / `TryReserve` — see `Docs/SPATIAL_INDEX.md`) for spatial queries over
  `Physics.OverlapSphere` and over adding colliders. Fauna senses already ride the index.
- Honor collider-LOD-by-phase (prism colliders disabled at Frozen) and the per-cell budget.
- If a change adds colliders or queries, say explicitly how the budget stays met.

## 4.5 Cell-environment baselines: measure them, don't guess them

`CellConfigDataSO.PhaseThresholds` must ride the environment's MEASURED baseline
(count + volume) or the cell boots into the wrong phase. `FrogletTools > Ecology >
Measure Cell Environment Baselines` is the in-engine ground truth — but you do
NOT have to block on the human for it. Cell environments are deterministic by
contract (pure function of the serialized seed), so port the generator and
measure offline; the in-editor measurer then CONFIRMS rather than supplies.
Method + the validate-against-a-shipped-baseline rule that makes it trustworthy:
`/asset-surgery` §4.5. Author thresholds as baseline + the Blob deltas
(+700/+500/+3600/+3000 count, +11200/+8000/+57600/+48000 volume).

While you have the emitted points in hand, assert the spatial invariants too —
in particular that **nothing is laid inside the nucleus control radius** (~392u;
see `Docs/ECOSYSTEM.md` §13 + §18.1). An authored environment sitting in the
nucleus hands `DominantDomain` to whatever colour it favours before anyone flies.
That defect shipped undetected in Caldera (89% of its mass) until a one-line
check over the point cloud found it.

## 5. Hand back verification — you cannot run Unity; the human is the gate
- State the exact in-editor steps to verify, the scene to test, and the precise SO knobs to tune.
- Use the collider/volume telemetry overlay when it exists to make the budget observable.
- Never claim something works that you have not seen work. Report honestly (failures, skips, caveats).

## 6. Commit
One coherent step per commit; conventional-commit message; develop on the feature branch (never
`bleeding-edge`); open a PR only when asked. After lifeform-prefab changes, note to run
`FrogletTools ▸ Validation ▸ Validate Lifeform Crystals`.

## 7. Budgeting a new cell WITHOUT Unity (do this before you author thresholds)

You cannot run the measurer, but a generator's cost is analytic — transliterate its
loop structure to Python and compute the exact prism COUNT and the expected VOLUME:

- Counts are pure loop arithmetic (mind index-dependent skips like crenellation and
  `Scaled(n)` under the `density` knob).
- Volume is `Σ count × (x·y·z)` per structure. `Jit(s, a)` multiplies **all three axes
  by one** factor `k ~ U(1-a, 1+a)`, so `E[k³] = ((1+a)⁴ − (1−a)⁴) / (4a)` — ≈ **1.04**
  at the default `a = 0.2`. Noise-driven POSITION jitter never changes volume.
- Print the per-structure table. It shows you immediately which family is eating the
  budget (a thick "ground" slab band is the usual culprit) and is what the human
  checks the measurer's output against.

**Then author PhaseThresholds for what will GROW, not just for the baseline.** §18's
rule (measured baseline + the Blob deltas) assumes the mass above baseline is vessel
TRAIL. For a cell whose mass comes from **flora**, `FrenzyEnter` *is* the planting
budget — planting and growth stop there — so it must be set at
`baseline + the mature planting you actually want`, or the garden freezes while it
still looks bare. Size the planting as `Σ species (plants × maxTotalSpawnedObjects)`
and put Restless somewhere the fauna start hunting a partly-grown cell.

Always hand the numbers back as ESTIMATES with the measurer step attached — analytic
counts are exact, but only the editor proves the generator runs at all.
