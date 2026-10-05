# Ecology LOD: regions, a macro cohort ledger, and one conserved stomach (round 11f)

**Status:** headless-proven port of the research's *Recommended game architecture*
(`Tools/Ecology/DISCOVERIES.md` § "Hierarchical ecology" › "Recommended game architecture", research repo). The pure
C# cores and the swarm's macro body run in two harnesses. The Unity glue (`CellEcologyLod`, the `SwarmFauna` side,
the `Fauna` stomach) is type-checked only: **it has not run in the editor.** QA: **QA-SWARM-ROUND11-6**. Linked from
`Docs/SWARM_FAUNA.md` §24.

Code: `Assets/_Scripts/Controller/Environment/FloraAndFauna/Ecology/`.

| File | What it is | Unity? |
|---|---|---|
| `FaunaStomach.cs` | the conserved stomach that replaces `Fauna.starvationSeconds` (§2) | no |
| `EcologyRng.cs` | xoshiro256** sampler: normal, Poisson, binomial, multivariate hypergeometric | no |
| `EcologyWorld.cs` | species + every research constant (`EcologyParams`), regions, flora/nutrient voxels | no |
| `EcologyMacroCore.cs` | the 1 Hz cohort ledger (Escalator Boxcar Train) per region × species (§3) | no |
| `EcologyMicroCore.cs` | the 10 Hz agent sim the cohorts expand into (the research's `micro.py`) | no |
| `EcologyLodSim.cs` | the LOD rules (`EcologyLodRules`), expand/absorb, impostor reps, the harness world | no |
| `MacroPopulation.cs` | `IMacroPopulation`, `EcologyLodDirector`, `MacroFaunaLedger`, the swarm's `SwarmMacroBody` (§4, §5) | no |
| `CellEcologyLod.cs` | per-cell host: senses pilots, runs the director each frame and at 1 Hz | yes |

## 1. What maps to what

The research recommends six things. This round does these:

| # | Research recommendation | This round |
|---|---|---|
| 1 | Regions live inside `Cell`; only fauna are LOD'd | **Pure core, not wired to a Cell.** `EcologyWorld` partitions the 1200-u membrane into 200-u regions (912 inside) with 4-u flora/nutrient voxels. It runs in the harness. The game's flora stays real prisms. No game species is a cohort population yet (§6). |
| 2 | The volume spine reads both levels | **Done.** `Cell.LiveVolume` adds `MacroFaunaLedger` for populations that release entries. The swarm keeps its entries, so it is exact by construction (§4.3). |
| 3 | One stomach replaces the starvation clock | **Done for `Fauna`** (Boid, LightFauna, WormFauna and every subclass) (§2). The swarm's starvation is round 9's pooled egg budget. It is unchanged (§2.4). |
| 4 | Spawn profiles become initial conditions | Not this round. |
| 5 | Macro 1 Hz job / micro 10 Hz job / impostors | The macro and micro cores are pure, Burst-shaped C# (flat arrays, no allocation in the step). They are not yet Burst jobs. |
| 6 | Replicate the macro state | Not this round. The macro core is deterministic per seed, so it is ready for it. |

## 2. The stomach (recommendation 3)

`FaunaStomach` is a struct with a level at a timestamp and a constant drain. It costs nothing per frame. The level is
read when a behaviour tick asks for it and settled on each feed.

- **Metabolism** drains it at `Metabolism` volume/s. What it burns goes back to the cell's soil.
- **A feed** fills it with the volume actually eaten. Overflow beyond `Capacity` also goes to the soil.
- **Starvation** happens at zero. `IsStarving` is true, and the creature dies through its existing wither: skeleton,
  one crystal. Nothing new kills it.
- **A predator** that eats it takes `SurrenderAll` (its stomach) plus its body (`BodyMealVolume`, one nominal prism).
  The eater feeds on `SurrenderedMeal`.
- **Death by any path** closes the books: what is left, plus upkeep burned since the last settle, goes to the soil.

### 2.1 The migration: how existing species behave the same

Nothing in any asset changes. The new field `Fauna.stomachCapacity` defaults to `-1`, which means one nominal prism.

1. `Capacity = CellPhaseThresholds.NominalPrismVolume` = **16**. That is the 4×4×1 flora leaf, the same anchor the
   phase thresholds use.
2. `Metabolism = Capacity / starvationSeconds`, so a full stomach lasts exactly the authored `starvationSeconds`.
3. A creature is **born full** (`StartStomach` in `Start`), as the clock started at birth.
4. **A meal of at least one nominal prism** (a standard leaf, or any prey: its surrendered meal is ≥ 16) refills the
   stomach. The creature then starves exactly `starvationSeconds` after its last such meal. That is the clock's
   behaviour, to 3e-5 s over 2000 random feeding histories.
5. **Only small meals behave differently.** A meal under 16 buys proportionally less time; half a leaf buys half the
   clock. The clock let any bite, however small, reset the whole budget. That was the leak.
6. `starvationSeconds = 0` still means "never starves" (`Metabolism = 0`).
7. `ApplyVariantTuning` overriding `StarvationSeconds` re-derives the upkeep. This runs at spawn, before anything has
   been eaten.

Call sites: every `NotifyFed()` that consumed a prism now passes the prism's `Volume`, read before the `Consume`:
- Boid: grazing, feed target, bite;
- LightFauna: two sites;
- WormFauna.

The predation sites pass `f.SurrenderedMeal`. `Nourish` (an own-domain pilot's feed) keeps `NotifyFed()`, which is
one nominal meal.

### 2.2 The soil

`Cell.DepositSoilNutrient` / `Cell.SoilNutrientVolume` is a ledger bucket. Nothing in the game draws from it yet:
game flora growth is phase-driven, not nutrient-limited as in the research. It is the seam where research `N → F`
would attach.

Stomach volume is a **budget**, not index mass. It is never in `LiveVolume`, so a predator's meal is not a double
count of the prey's body prisms.

### 2.3 Proof (`ecology_lod_harness` group `stomach`)

- Migration: 2000/2000 histories starve at the clock's time (worst 3.1e-5 s).
- Half a meal buys 15 s of 30.
- 0 means never.
- Conservation: worst |in − out| / in = 2.9e-8 over 500 histories of feeds, upkeep, overflow and surrender.
- **Negative control:** a clock-reset stomach conjures 119% of its intake, and the gate fails.

### 2.4 Out of scope: the swarm's starvation

`SwarmFauna` already has a conserved stomach: round 9's per-element egg budget (`SwarmTickJob.Stomach`,
`QueueDeposit`, paid out by laying). Its starvation shed still runs on `StarvationSeconds` since the last bite, plus
`ShedIntervalSeconds`.

The follow-up is to drain the egg budget by a per-member metabolism and shed when it is empty. That changes swarm
balance, so it is left for its own round.

## 3. Regions and the macro cohort ledger (recommendation 1, as a pure core)

`EcologyMacroCore` is the research `macro.py`, ported with its laws.

- **Cohorts.** Each region × species has up to 10 cohort slots: an Escalator Boxcar Train with 4 element counts
  (`long`), Σstomach `S`, Σstomach² `Q` and bounds `[Lo, Hi]`. A cohort is Normal within its bounds. The moments are
  additive, so merge (tolerance 0.06), split and migration are exact.
- **Tick.** 1 Hz (`dtMacro` 1). It is deterministic per seed (`EcologyRng`).
- **Grazing** is Holling II. Intake is 0.25 × F/(F + 90) per grazer, capped by the region's flora.
- **Predation** is Holling II:
  - attack 5e-5, handling 357 s;
  - predators hunt below 0.8 of eMax;
  - kills are drawn from the multivariate hypergeometric over prey cohorts;
  - the prey's stomach and body go to the predator.
- **Occupancy.** Grazers flee into cover with the fitted constants θ 0.5 and τ 1 s. Predator sense 25 u, catch 4 u,
  grazer flee 40 u, sprint κ (0.36, 0.019).
- **Energetics.** Metabolism goes to nutrient: herbivore 0.04/s (sprint 0.04), predator 0.02/s (sprint 0.06).
  Stomach diffusion is (0.0009, 0.002).
- **Births** come from the stomach tail. The truncated-normal mass above `eBirth` (herbivore 20, predator 100) splits
  into a newborn at `e0` (6 / 30). The parent pays body + stomach.
- **Deaths.** Starvation is the tail below 0: body to skeleton, one crystal.
- **Migration hops** by occupancy. Herbivore (0.00144, 0.00482, 0.0112), predator (0.0028, 0.0107, 0.0144). Food
  bias 0.3.
- **Ledger.** The ledger is closed: flora + nutrient + bodies + stomachs + skeletons is constant.

The other constants (`EcologyParams`, with the research's `params.py` values quoted at each field) are:
- herbivore body 8, eMax 32, speed 18 / sprint 32;
- predator body 40, eMax 120, speed 20 / sprint 36;
- flora r 0.006, cap 240 (calibrated 120 for the consistency gate), nutrient half-saturation 4000, seed rain 0.02,
  nutrient diffusion 0.002;
- LOD: expand 280 u, ahead 450 u (cone cos 0.5), collapse 400 u (+80 ahead keep), visible 330 u, near-see 60 u;
- agent budget 4500, 6 impostor reps per region and species.

**One discrepancy:** DISCOVERIES prose says ahead 400 / collapse 450, but `params.py` (what the gates ran on) says
ahead 450 / collapse 400. The port uses `params.py`.

**LOD** (`EcologyLodSim`):
- Hot and cold regions have hysteresis.
- **Expand** samples individuals from a cohort. Stomachs are clipped to `[Lo, Hi]` and shifted, so ΣS is exact.
  Grazers are rejection-sampled outside a predator's flee radius. Agents emerge from the impostor reps.
- **Absorb** happens only if the agent is unseen at both its sim position and its drawn position.
- **Migrants** that enter a hot region spawn at the source rep.

## 4. The population interface (recommendation 2 and the contract for every LOD'd species)

`IMacroPopulation` has these members:
- `MacroCentre` / `MacroExtent` (world);
- `IsCollapsed`, `CanCollapse`, `NeedsIndividuals`;
- `Totals` (individuals, Σbody, Σstomach);
- `Collapse()`, `Expand()`, `MacroTick(dt)`.

`EcologyLodDirector` applies `EcologyLodRules` to every registered population:
- **`Guard()` runs every frame.** It expands anything collapsed that a pilot wants or can see.
- **`Tick(1 s)` runs at 1 Hz.** It collapses what is far and unseen, and macro-ticks what is collapsed.

### 4.1 The contract

The swarm harness asserts each clause.

1. **Collapse and expand are atomic and exact.** Individuals per element × lineage, Σbody and Σstomach are unchanged,
   and so is the cell's `LiveVolume`. A population either keeps its index entries (bound with `Cell.BindVirtualMass`)
   or releases them and books the same volume with `Cell.BookMacroFaunaVolume` **in the same call**.
2. **Nothing pops.** The director collapses only what no pilot can see. "See" uses the sim and drawn position,
   widened by the extent. It expands on the prefetch radii, before a pilot can see. New bodies bloom in or emerge
   from what was drawn.
3. **No imposed death in the macro.** A death needs a body to wither and a crystal to drop. A tick that would kill
   (starvation, a hit) reports `NeedsIndividuals` and is expanded so the death happens properly.
4. **One rule set.** The macro tick is the same biology at a coarse cadence: the same edibility, the same stomach,
   births from the same stomach tail.

### 4.2 The Unity host (`CellEcologyLod`)

`CellEcologyLod` is one plain class per cell (the `SubstrateCellHost` pattern). It is made on the first `Register`
and retired with the last `Unregister`. Members call `Advance()` from `Update`; the first call in a frame does the
work.

- **Pilots** come from:
  - a cell-wide `Physics.OverlapSphereNonAlloc` with `Fauna.VesselSenseMask`, every 0.1 s, giving each vessel's
    position and `Course × Speed`, extrapolated per frame;
  - **the main camera** as an extra pilot looking down its forward axis. The research's visibility test uses the
    pilot's heading as its view, which a free-look camera does not share.
- **Each frame** it calls `Director.Guard()`. **Every 1 s** (staggered per cell, a hitch never banks ticks) it calls
  `Director.Tick(1)`.
- **Profiler markers:**
  - `CellEcologyLod.Sense`
  - `CellEcologyLod.Guard`
  - `CellEcologyLod.MacroTick`
  - `SwarmFauna.MacroTick`

### 4.3 `LiveVolume` collapsed or expanded (recommendation 2)

`Cell.cs` gains `MacroFaunaLedger _macroFauna`.

- `BookMacroFaunaVolume(domain, ±volume)` books into it.
- The ledger is **latched** when the cell schedules its async index volume sum (`EnsureVolumeFresh`).
- `PublishVolumeSums` adds the latched copy per domain slot and to `liveVolumeTotal`.

So a population that moves volume from the index to the ledger between schedule and publish is counted once, never
twice and never zero times. `ResetVolumeAccounting` clears the ledger.

The swarm does not use the ledger: it keeps its entries. The ledger is there for populations that release theirs
(§6).

## 5. The swarm as an `IMacroPopulation`

`SwarmMacroBody` (pure) is wrapped by `SwarmFauna`. The swarm opts in with `SwarmFaunaConfigSO.MacroLod` (default
**on**; it needs the GPU member draw). Existing assets get `true` through the field initializer, so no asset edit is
needed.

### 5.1 What a collapsed swarm is

- **The formation is frozen** in the core: the body plan it re-expands into. Its 10 Hz worker tick stops (no `Kick`).
- **Its index entries stay.** Weapons, predators, `LiveVolume` and the phase ladder see the same prisms.
- **Once per macro tick it drifts rigidly** toward its `Goal` at cruise (`Cruise × UnitScale × TickHz`).
  `SwarmTickJob.Translate` moves the following together, which is why nothing jumps on expansion:
  - the core: `ISwarmCore.Translate` (all four cores: positions, anchor, swim target);
  - the published Prev/Cur frames;
  - the index points;
  - the anchor.
- **The same tick then:**
  - re-uploads the instance buffer;
  - re-syncs the index entries (`SyncIndex`, points only: no register, release or re-shape) and the body entities;
  - poses them once.
- **Between ticks it is drawn still** at the display alpha it froze at.
- **It keeps grazing** at the macro cadence: `BitersPerStep × TickHz × dt` bites through the same `IsFood`. Food is
  banked by `SwarmMacroBody.Bank`, the same `QueueDeposit`. `StomachFill` reads the macro state, so a collapsed swarm
  stops eating when sated.
- **Counts never change while collapsed.** Births are laid on expansion from the banked stomach, by the core's own
  laying. A due starvation shed sets `NeedsIndividuals` and expands it.
- **It expands immediately when:**
  - a weapon or predator materialises a member (`MaterialiseForHit`);
  - a proxy exists;
  - a kill is waiting for its tick;
  - a shed is due.

### 5.2 When it collapses and resumes

- **`Collapse`.** Collapse happens only with the job Idle. Otherwise the request is remembered and taken at the next
  published tick, which is the formation it freezes into.
- **`CanCollapse`** requires all of:
  - GPU draw;
  - no worker error;
  - no proxies;
  - no pending kills;
  - no shed due.
- **Expand** resumes the tick at the frozen display alpha.

**Network.** The swarm is client-local, like every freestyle creature. Each peer runs its own LOD.

### 5.3 Proof (`swarm_core_harness` mode `lod`, whale plan, sort core, SWARM_DENSITY 5)

The run: collapse, then 120 macro ticks (2 min) drifting 274 u along the swarm's shell at game cruise, then expand.

- **Counts.** Counts per element × lineage and Σbody are identical.
- **LiveVolume** stays 30147.9 → 30147.9, with max deviation 0 while collapsed.
- **Index churn** while collapsed: 0.
- **Rigid drift.** Formation error is 8.2e-5 u.
- **No pop.** The first micro step after expansion is 1.75 u (an ordinary step; bound 4.17).
- **Stomach.** Food banked while collapsed is in the stomach after expansion (error 0).
- **Negative control `TranslateCoreOnly`.** Moving the core but not the drawn frame jumps 841 u on expansion. The gate
  fails.
- **Negative control `ReseedOnExpand`.** Re-growing from the counts alone throws away the drawn body: members are
  displaced up to 152 u.
  - Finding: the counts and Σbody stay **exact**. A peer without the formation can rebuild it from the macro state,
    but it re-grows from a knot. This is recommendation 6's path.
- **Director.** A 260 u/s pilot fly-by gives 2 collapses, 1 expand, 20 macro ticks, **0 frames seen while collapsed**
  and 0 drifts while seen.
  - Negative control: without the per-frame `Guard`, the pilot sees the collapsed swarm for 12 frames. A 260 u/s
    pilot crosses the prefetch margin in under a 1 Hz tick.

## 6. How the other populations adopt it

### 6.1 Substrate fauna (round 11b, `Substrate/`)

**Reuse `SubstrateCore`'s stomach. Do not duplicate it.**

Per agent the core already holds:
- `Stock`: body volume, food → stock 1:1, births split stock;
- `Hunger`: dimensionless, rises at `Metabolism`/s, a feed lowers it by `volume × HungerPerVol`; starving when
  `Hunger ≥ 1 + StarveS × Metabolism`;
- the `MassIn` / `MassOut` ledger.

A `SubstratePopulation` is a block of slots in **one shared core per cell**, so it can be collapsed by either of two
routes.

**Route A: freeze (recommended first; it is what the swarm does).**
1. Add `bool Frozen` to `SubstratePopulation`.
2. Skip frozen populations in the kernel's agent pass and in `SubstrateCore.Step`'s other passes. One `if
   (!pop.Active || pop.Frozen) continue` per pass. `SubstrateTickJob` is shared, so this is the only core change.
3. Add `SubstrateCore.Translate(pop, d)`, which moves that block's positions and their published Prev/Cur. Then
   `SubstrateFauna.MacroTick` drifts the block toward its anchor's goal once per second and re-syncs its entries
   (`ISwarmEntrySink`, already bound with `Cell.BindVirtualMass`). LiveVolume is exact by construction.
4. Macro feeding: a per-second `Feed(i, volume)` on a few agents, through the existing `SenseFood` path, at the
   macro cadence.
5. `NeedsIndividuals` is true when any agent's `Hunger` crosses the starving reserve (the core's own `Starving`
   test). The block expands and the agent dies through the existing shed path.
6. `Totals`:
   - individuals = the block's alive count;
   - Σbody = Σ`Stock`;
   - Σstomach = Σ(1 + StarveS·Metabolism − Hunger) / HungerPerVol, the reserve expressed as volume.

**Route B: cohorts.** Use this once a population must be cheaper than a frozen block. Run it through
`EcologyMacroCore`:
- count per element = cohort `N[e]`;
- body = `Stock` (the cohort's body is per-individual, so carry Σstock beside S, Q);
- the stomach variable is the reserve above. A cohort's S and Q are Σ and Σ² of it.
- Hunger rises linearly, so the cohort law is a pure shift: `S −= n·Metabolism·dt / HungerPerVol`, `Lo`/`Hi` shift
  too.
- Starvation is the tail below 0 → `NeedsIndividuals`. Births come from the stomach tail at the species' birth
  threshold.
- Release the block's entries and book Σstock with `Cell.BookMacroFaunaVolume` in the same call.

### 6.2 Builders (round 11e, `Builders/`)

`BuilderColonyCore` and `ThiefNestCore` hold a per-member `Stomach[]` (`BuilderStomachParams`):
- Capacity 40;
- burn `Metabolism` 0.02/s active, `Torpor` 0.02/s roosting;
- starvation only at empty;
- births paid from the parent at `BirthAbove` 0.9 × Capacity, cost 16, half to the newborn's stomach.

That is already the conserved stomach this round asks for, so builders adopt `IMacroPopulation` with **no new
stomach**.

- **A colony is anchored** (its core or plant does not move), so its collapsed form needs no drift.
  `BuilderColonyFauna` implements:
  - `Collapse`: when no proxies are out and no prism is mid-flight (`BuilderPrismWorld` has no settle in flight) and
    no member is carrying. A carried prism is real mass with a position, so it must be placed or dropped first.
  - Freeze the members' tick. Keep their index entries (they are already `SwarmEntryLedger` entries).
  - `MacroTick(dt)`, per member: `Stomach[k] −= Torpor × dt`, roosting rate, because a collapsed colony is
    unwatched. If any stomach would reach 0, set `NeedsIndividuals` and expand, so the death runs through `Kill(k,
    StarvedBy)` with its crystal.
  - Building, stealing and births wait for expansion. They move or create real prisms and need individuals.
  - `Totals`: alive count, Σbody (the member bodies' entry volumes), `StomachTotal` (already a property on both
    cores).
- **Thieves** steal from vessels' trails, so they only act near a pilot. Collapsing a nest far from every pilot loses
  no behaviour.

## 7. Proof: every gate and its result

`bash Tools/Build/ecology_lod_harness/run.sh` (all groups, about 12 min). It compiles `Ecology/*.cs` (minus the glue)
against netstandard2.1 with `-warnaserror` first, the way Unity would.

| Gate | Clean result | Negative controls (each must fail and be exercised) |
|---|---|---|
| **conservation** (1200-u cell, 30k + 2.5k, 2 pilots at 220/260 u/s, 300 s) | ledger drift 6.85e-14; LOD pass mass 8.15e-10, count 0; 168564 expands / 170687 absorbs / 4243 arrivals; births and kills in both levels | AbsorbDropStomach 3.44e-3 · BirthFreeBody 2.59e-4 · ExpandLosePool 1.66e-2 |
| **continuity / no pop** (45k + 3k, 140/200 u/s pilots, 150 s) | 644205 seen agent-ticks: pop-in 0, pop-out 0, teleport 0 | AbsorbIgnoresVisibility: 78 pop-outs · ExpandNoEmerge: 9598 pop-ins |
| **consistency: macro-then-expand ≈ micro** (600-u world, flora cap 120, T 300 s, 4 seeds) | herb count 0.003, pred count 0.035, herb e-mean 0.007, pred e-mean 0.038, herb sd 0.052, pred sd 0.013, phases 0.052/0.040, flora 0.004, kills 0.165; the switch arm (a pilot arrives half way) also passes | MacroGrazeX15: herb count 0.62 · ExpandMeanField: sd 0.75 · MacroAttackX2: kills 0.61 |
| **cycles persist** (macro only, full cell, cap 240, 45k + 3k, 8 h) | grazers 18591–163267 (cv 0.68), predators 3736–34073 (cv 0.43). Research: 18.8k–163k, 3.7k–34k, cv 0.69/0.43. 4 predator reversals ≥ 10% after 2 h (swings 188%, 33%, 45%, 26%), period ≈ 2.25 h (macro 14.8 ms per tick averaged over the run, with grazers up to 163k); last-half cv 0.32/0.11; no extinction; drift 5.6e-14 | none (this is a dynamics check, not a law) |
| **cost** | macro 8.3 ms per 1 Hz tick (912 regions × 10 cohorts × 2 species) = **0.14 ms per 60 fps frame**; flora + soil 0.35 ms per tick; micro 1.2 ms per 10 Hz tick for ~1600 expanded agents. Managed and single-threaded: the research's C kernel did 2.1 ms. | n/a |
| **stomach** | §2.3 | clock reset: 119% conjured |

Separately: `SWARM_DENSITY=5 bash Tools/Build/swarm_core_harness/run.sh "Assets/_SO_Assets/Swarm Fauna/Plans" lod`
(§5.3). Every earlier gate stays green: the swarm default modes, tickjob, lineage, substrate harness and type-check,
builders harness, the swarm glue type-check (now including `Ecology/*.cs` and `CellEcologyLod.cs`), the three
`author_*.py --check` scripts, and `check_console_logging.py`.

The cycles gate's first criterion was "upward crossings of the run's mean", and it failed with 1. The 8 h series
showed why: the opening transient (grazers to 163k in the first 20 min) pulls the mean above the level the cycle
settles around. The gate now counts reversals with 10% hysteresis after the opening quarter, and requires the last
half still to breathe.

## 8. What is NOT proved

- **Nothing here has run in Unity:**
  - `CellEcologyLod`;
  - the `SwarmFauna` collapse, macro tick and expand;
  - the `Fauna` stomach call sites;
  - the `Cell` ledger latch.

  They are type-checked against stubs (`swarm_glue_typecheck`), which proves signatures, not behaviour. Notably
  unproven: the unified body entities re-posed once per macro tick (`SyncEntities` + one pose), and the hearts'
  instanced draw at the frozen alpha.
- **The `Fauna` stomach glue** (Fauna.cs, Boid, LightFauna, WormFauna) is in no headless compile. `FaunaStomach`
  itself is run. The call sites were reviewed by hand.
- **The `Cell.BookMacroFaunaVolume` latch** has no caller yet: the swarm keeps its entries, so it is unexercised.
- **The cohort core is not wired to a game population.** The game's fauna are not cohorts yet; the core is proven
  against its own micro sim (consistency gate), not against Boid or LightFauna behaviour.
- **The soil is a sink.** Nothing draws from `SoilNutrientVolume`.
- **Pilot visibility** is the research's cone test plus the main camera's forward axis. A wide-FOV or zoomed-out
  camera can see further than 330 u. For a swarm that is safe (it is drawn while collapsed and only moves at the 1 Hz
  tick), but the gate cannot see a real frustum.
- **Swarm micro starvation** remains the round-9 clock (§2.4).

## 9. Merging

- **Branch.** `overnight/lod` has `cece/swarm-fauna-game` merged at 307a983f2 (round 11b, 11b-2 and 11e included).
- **Shared edits:**
  - `ISwarmCore` gains `Translate(Vector3)`. All four cores and the harness's `ThrowingCore` implement it. Any other
    `ISwarmCore` implementation in another branch must add it.
  - `Fauna.NotifyFed()` keeps its signature. `NotifyFed(float)` is new.
  - `Fauna._lastFedTime` is gone. Any branch that reads it must use `IsStarving` or `StomachVolume`.
  - `Cell.cs` gains a self-contained section and two lines in the volume publish.
- **Stubs:**
  - `Tools/Build/swarm_glue_typecheck/Stubs.cs` gains `Fauna.VesselSenseMask` and `Transform.forward`.
  - `substrate_glue_typecheck` is unaffected.
