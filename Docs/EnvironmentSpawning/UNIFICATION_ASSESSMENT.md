# Environment Spawning — Microscene vs. Spawnable Unification

Audit + decision record for unifying the freestyle **microscene conveyor** with the older
**Spawnable / Generator** environment system, and the microscene diversity expansion built on
that unification.

## TL;DR

Unifying is a **categorical improvement with no compromise to either use case** — provided you
unify the *substrate* (data model, prism-laying, geometry vocabulary, lifeform release) and leave
the *lifecycle / orchestration* polymorphic. The microscene's performance comes entirely from its
**lifecycle** (a closed, fixed prism stock it re-poses forever and never destroys), not from its
generator or data model — those are the cheap parts and are identical in cost to the old system.
So the shared substrate can be lifted out at zero performance cost to the toy, while each system
keeps the lifecycle strategy that fits it. The only thing that *would* be a compromise — forcing
the conveyor onto the old Instantiate/Destroy model, or forcing a static track into closed-stock
recycling — is exactly what the layered split avoids.

## The two systems

| | Environment spawner (old) | Microscene conveyor (new) |
|---|---|---|
| Data atom | `SpawnPoint` / `SpawnTrailData` | `SpawnPoint` (same struct) |
| Geometry | `Generators/*` (18 MonoBehaviours) + ~40 inline `Spawnable*` shapes | `MicroscenePatterns` (pure static recipes) |
| Prism lay-down | `SpawnableBase.SpawnPrismTrail` (+ `SpawnableShapeBase` coroutine copy) | `Microscene.PopulateAsync` (async, batched copy) |
| Lifecycle | one-shot: `Instantiate` at build → `Destroy` on `NukeTheTrails` | closed stock: Instantiate once → **re-pose forever, never destroy** |
| Orchestration | `SegmentSpawner` (static layout) | `MicrosceneConveyor` (streaming, speed-scaled, corridor frontier) |
| Runs in | HexRace track, Slip'n'Stride, shape-drawing | Wanderway freestyle toy |

## The overlap, layer by layer

- **Layer 1 — data vocabulary** (`SpawnPoint`/`SpawnTrailData`): already shared. No drift.
- **Layer 2 — prism lay-down primitive**: the sequence
  `Instantiate → ChangeTeam → ownerID → pose → TargetScale → Trail → Initialize → trail.Add`
  existed **three times** (`SpawnableBase.SpawnPrismTrail`, `SpawnableShapeBase.GradualSpawnCoroutine`,
  `Microscene.PopulateAsync`). A live drift surface — the conveyor was the first consumer to
  exercise a latent gap (`ResetState` not re-arming the scale animator that `SetupDestruction`
  disabled).
- **Layer 3 — geometry math**: `MicroscenePatterns` re-implements helices, tubes, rings, spirals,
  grids, walls that already exist as `Generators/`. But the producer contracts genuinely differ —
  generators are MonoBehaviours with `protected GeneratePoints()` behind a param-hash **cache
  designed to not regenerate**, whereas the conveyor needs a *fresh* budget-exact roll off an
  injected `System.Random` every arrival. That contract mismatch is *why* the parallel library was
  written; it can't be fixed by "call the existing generators."
- **Layer 4 — lifecycle**: three strategies — (a) `SpawnableBase` raw Instantiate/Destroy, no
  reuse; (b) `GenericPoolManager`/`PrismFactory` true object pool (player trails, VFX); (c) the
  conveyor's closed scoped stock (Instantiate ~420 once, re-pose forever via O(1) `UpdatePosition`).
  **This is where the performance story lives.**
- **Layer 5 — orchestration**: static `SegmentSpawner`, streaming `MicrosceneConveyor`, and the
  time-driven cell spawners are genuinely different concerns. Not merge candidates.
- **Layer 6 — lifeform release**: the microscene correctly *delegated* to Cell primitives but
  *duplicated* the spawner-level orchestration (`PreyAvailable` mirrored `RandomLifeSpawner`;
  `PickPlayableDomain` mirrored `PickRandomDomain`).

## Performance verdict

The microscene's speed is a **Layer-4** property (closed fixed stock, no per-arrival
Instantiate/Destroy, bounded steady-state mass, collider-LOD, zero physics queries). Layers 1–3
are performance-neutral between the two systems (pure math + the same Instantiate sequence), so
sharing them costs the conveyor nothing. "Make the old system conform to the microscene lifecycle"
is correct only for a *future streaming mode* (which should reuse `MicrosceneConveyor`), never for
a static track (its mass is already bounded by track length; recycling would be complexity for no
gain).

## What was implemented (unification — substrate merged, lifecycle left alone)

- **Layer 2 — one prism-laying primitive.** New `PrismTrailBuilder` (`Environment/Spawning/`) with
  sync / gradual-coroutine / batched-async lay modes over one `LayOne` step. `SpawnableBase`,
  `SpawnableShapeBase`, and `Microscene` all delegate to it. Zero behaviour change; the drift
  surface is gone.
- **Layer 3 — shared geometry library.** New `PrismGeometry` (`Environment/Spawning/`): the pure
  geometry vocabulary (hoops, arches, vortices, corridors, grids, torus rings, fans, scatters, wave
  sheets…) + a richer scale palette (10 named scales). The microscene recipes now compose it via
  `using static`; the 16 original recipes keep their exact geometry (flyability untouched).
  Available for the `Generators/` to adopt too (mechanical follow-up — not migrated here to avoid
  risking untested shipped shapes; see Deferred).
- **Layer 6 — lifeform delegation.** The prey gate is now one shared static
  (`FaunaReproductionRules.PreyAvailable`) called by both `RandomLifeSpawner` and the microscene.
  Four `CellLifeSpawnerBase` helpers (`SpawnFlora`, `SpawnFaunaWithDomain`, `PickRandomDomain`,
  `RegisterSpawned`) were promoted to `public static` so the microscene reuses the ONE canonical
  Instantiate→Initialize→Register sequence instead of its inline copy.

## What was implemented (diversity expansion, built on the unified substrate)

Themed separately from geometry so the recipes stay pure shape (`MicroscenePlan` now carries a
geometry layer + a themed `Prisms`/`Crystals` layer; `MicroscenePatterns.ApplyTheming` applies theming
from a `MicroscenePalette`, config-authored on `ConveyorToyDefinitionSO`):

- **Bigger recipe library** — 16 → **28 recipes** (archway, vortex with an open convergence +
  inviting crystal, slot corridor to roll through, cube field, torus gate, pillar hall, turbine,
  asteroid field, plus new living recipes: rolling plains, grove, aviary, preserve). The existing
  16 are byte-identical.
- **Richer scales** — 10 named scales (shard → boulder / mote → beam) plus a per-scene **scale
  mood** that scales a whole scene grand or delicate.
- **Prism kinds** — plain / **danger** / **shielded** / **supershielded**, applied reversibly via
  the state-machine path (`PrismKinds`), sprinkled sparsely under capped per-scene kind schemes.
- **All three domains + Blue** — per-prism domain under a **coherent per-scene scheme** (mono /
  banded-by-structure / accented / neutral-veined-with-Blue), read live each draw (Domain Changer
  toy compatible). Never per-prism confetti — most scenes stay one colour.
- **Omnicrystals in the mix** — the body-collected jackpot (`Crystal.prefab`, fuel + speed buff),
  minted alongside the elemental skims via `OmniCrystalChance`. Real omni collection is made
  manager-less-safe by four defensive null-guards on `Crystal` / `OmniCrystalImpactor` that only
  change behaviour when there is no `CrystalManager` (the local-toy case) — real gameplay is
  byte-identical.

## Locked invariants — none violated

Continuity (prisms bloom / crystals FadeIn / kinds transition; supershield reverses cleanly) ·
no imposed death/decay (closed pool, no caps/TTLs) · no domain asymmetry (fauna still
`ControllingDomain`-only via the delegated spawn; multi-domain applies to *prisms* = neutral mass) ·
mass conservation / volume-spine (kinds still register volume and ride the transport pool) ·
Cell owns the environment (lifeforms via the cell's own `SpawnProfile` + canonical spawn path).

## Collider-budget hand-back (HARD GATE)

- **Danger** = same LOD-cullable `BoxCollider` as plain — free.
- **Shielded / SuperShielded** now KEEP the same LOD-cullable authored `BoxCollider` trigger as
  plain — the octahedron / stellation is a look-only change (no convex `MeshCollider`, no convex
  cook), so the earlier always-on-MeshCollider budget line is gone. The palette caps
  (`MaxShielded = 3`, `MaxSuperShielded = 1` per scene, ~11% of scenes carry any, locked by
  `MicroscenePatternsTests`) now bound spawn variety rather than collider cost. (Collision is at
  authored box size; shape-precise shielded collision is the planned three-LOD follow-up.)
- Belt BoxColliders stay pool-bounded (`poolSize × prismBudget` ≈ 420) and collider-LOD-culled.
  Placement remains pure arithmetic — zero added physics queries.

## In-editor verification (I cannot run Unity)

1. Run **FrogletTools > Scene Setup > Setup Freestyle Toybox** (wires the omni prefab + palette on
   `Toy_Conveyor.asset`), enter freestyle in Menu_Main, fly the Wanderway toy.
2. Confirm: most scenes read one coherent colour with occasional accent/Blue-vein/banded scenes;
   the new recipes appear (arches, vortices with a crystal at the open mouth, roll-through slot
   corridors, torus gates, pillar halls, turbines, asteroid fields, living plains/groves/aviaries);
   scenes vary in scale (grand vs. delicate).
3. Kinds: occasional danger prisms (Squirrel danger-skim boost + slam on contact), rarer shielded
   accents, rare supershield landmarks; watch `[ECOSIM] colliders=near/live` stays bounded.
4. Omnicrystals: a few scenes carry the big omni crystal; fly *into* it (body-collect) → fuel +
   speed buff + spent-crystal VFX, no error (manager-less guards).
5. Recycle: fly on — passed scenes suction and re-bloom re-coloured/re-themed; no prism pops.
6. Run the EditMode suite (`MicroscenePatternsTests`) — budget exactness, determinism, crystal
   clamp, living-recipe counts, extent bounds, collider caps, domain-set containment.

## Deferred (transparent)

- **Layer 4 — routing the old `SpawnableBase` path through `GenericPoolManager`.** The one item I
  called "optional/low-hanging" in the assessment. It touches shipped modes (HexRace track,
  shape-drawing) and can't be play-verified here, so it is *not* done blind. Recommended as its own
  focused, in-editor-verified branch. The conveyor already enjoys the equivalent win via its own
  closed stock.
- **Migrating the `Generators/` + `Spawnable*` shapes to call `PrismGeometry`.** Mechanical dedup;
  left for a follow-up so untested shipped shapes aren't perturbed. The shared library now exists
  for them to adopt incrementally.
- A `MicrosceneRecipeSO` for hand-authored set pieces in the shuffle bag (the palette already gives
  designers full theming control; authored geometry is the next step).

## File index

| Role | File |
|---|---|
| Prism-kind enum | `_Scripts/Data/Enums/PrismKind.cs` |
| Reversible kind application | `_Scripts/Controller/Environment/Spawning/PrismKinds.cs` |
| Shared prism-lay primitive (Layer 2) | `_Scripts/Controller/Environment/Spawning/PrismTrailBuilder.cs` |
| Shared geometry library (Layer 3) | `_Scripts/Controller/Environment/Spawning/PrismGeometry.cs` |
| Plan model (geometry + themed) | `_Scripts/Controller/Toys/MicroscenePlan.cs` |
| Theming palette (config) | `_Scripts/Controller/Toys/MicroscenePalette.cs` |
| Recipes + theming | `_Scripts/Controller/Toys/MicroscenePatterns.cs` |
| Scene lay / transport | `_Scripts/Controller/Toys/Microscene.cs` |
| Belt runner | `_Scripts/Controller/Toys/MicrosceneConveyor.cs` |
| Toy config | `_Scripts/ScriptableObjects/Toys/ConveyorToyDefinitionSO.cs` |
| Prey gate (shared) | `_Scripts/Utility/DataContainers/FaunaReproductionRules.cs` |
| Canonical spawn helpers (public static) | `_Scripts/Controller/Environment/CellLifeSpawnerBase.cs` |
| Omni manager-less guards | `_Scripts/Controller/Environment/FlowField/Crystal.cs`, `_Scripts/Controller/ImpactEffects/Impactors/OmniCrystalImpactor.cs` |
| Env builders → shared primitive | `_Scripts/Controller/Environment/Spawning/SpawnableBase.cs`, `_Scripts/Controller/Environment/MiniGameObjects/SpawnableShapeBase.cs` |
| Tests | `_Scripts/Tests/EditMode/MicroscenePatternsTests.cs` |
