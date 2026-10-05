# Ecology LOD: regions, a macro cohort ledger, and one conserved stomach (round 11f)

**Status:** headless-proven port of the research's *Recommended game architecture*
(`Tools/Ecology/DISCOVERIES.md` § "Hierarchical ecology" › "Recommended game architecture", research repo). The pure
C# cores and the swarm's macro body run in two harnesses; round 11f-2 adds the substrate's frozen blocks, the builder
colonies' roost and the threat flora's far cadence (§6), each in its own harness. The Unity glue (`CellEcologyLod`, the `SwarmFauna` side,
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
balance, so it is left for its own round. Round 11f-2 looked again and kept that decision (§6.4).

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

Round 11f-2 (`overnight/lod2`) implements both, plus a far cadence for the threat flora's network. The swarm
(§5) remains the reference.

### 6.1 Substrate fauna (round 11b, `Substrate/`): implemented, freeze route

A `SubstratePopulation` is a block of slots in **one shared core per cell**, so a collapsed population is a
**frozen block**. The stomach is the one the core already has. No second stomach was added:
- `Stock` is body volume;
- `Hunger` rises at `Metabolism`/s;
- starving at `Hunger ≥ 1 + StarveS × Metabolism`;
- the `MassIn` / `MassOut` ledger.

**Core** (`SubstrateCore.cs`):
- `SubstratePopulation.Frozen` makes `BeginStep` publish `LiveCount = 0` for that block. The moments, the gather, the
  agent pass and the Burst job then skip it. `SubstrateKernel` / `SubstrateAgentJob` are **unchanged**, so
  `check_burst_substrate.py` and group K's bit-match still apply to every unfrozen population.
- `EndStep` runs `FrozenPopulation` for a frozen block. It applies the kernel's own hunger rule
  (`Hunger += Metabolism × Dt`), clears danger and counts the alive. It never flags `Starving`: a death needs a body,
  so the owner expands first.
- `Fields.Update` is skipped when every population is frozen. That is where the cost was.
- `ReserveSeconds(q)` is the time until the hungriest agent starves. `ReserveVolume(q)` is Σ(starving line − Hunger)
  / HungerPerVol, the reserve expressed as volume.
- `Translate(q, d)` moves a block's `Pos` / `Home`. The job's next `Build` draws the move as a glide. **The caller
  keeps the block inside the membrane**, otherwise the clamp jumps it on the next step.
- `FreezeBug` holds planted bugs for the harness only.

**Host** (`SubstrateCellHost`): `Freeze(pop, bool)` and `Translate(pop, d)` are queued and applied in `ApplyLod()`
between ticks, while the job is idle.

**Glue** (`SubstrateFauna` implements `IMacroPopulation`; per species `macroLod` defaults on and
`thawReserveSeconds` is 5):
- `CanCollapse` requires all of:
  - no proxies;
  - no slot gone;
  - nothing engaged;
  - no agent starving;
  - reserve > 2 × thaw.
- `NeedsIndividuals` is any of: a proxy, a gone slot, a starving agent, or reserve < thaw.
- `Totals`: alive count, Σ stock, reserve volume.
- `MacroTick` is a **no-op**. A band population has no goal to drift toward. Its hunger advances in the frozen pass
  every step, at the micro rate, so the macro tick adds nothing. `Translate` is in place for a species that later
  gets a goal.
- **A hunt thaws its prey.** `MaterialiseForHit` calls `Thaw()` first, so a pack hunting a frozen locust block wakes
  it (harness: thawed after 7 ticks).

**Proof** (`substrate_harness` group `lod`; the core is run inline with the job; 900 slots, locust / pack / lurker
blocks, 900 food points; drift 0.02 u/tick through `Translate`):
- **Pack, frozen:** 735 frozen ticks.
  - Alive 7 → 7.
  - Stock / volume deviation 0, so mass is exact.
  - 0 agents moved except by `Translate`.
  - Glide error 2e-5.
  - Hunger error against the kernel rule 5.6e-6.
  - Minimum reserve 4.9 s, then 1 reserve thaw.
  - First step after thaw 10.63 u (bound 25.5, the species' own max).
  - Ledger 1.6e-9.
- **Lurkers** pass. First step 0.40 u.
- **Locusts** are thawed by the pack's hunt.
- **Director:** a `SubstratePopModel` behind the real `EcologyLodDirector` collapses 10 times, with 0 frames seen
  while frozen.
- **Cost:** 1.958 ms per tick running vs **0.006 ms** with all frozen.
- **Negative controls:**
  - Deactivating instead of freezing: volume deviation 1614, fails.
  - `NoMetabolism`: hunger error 0.96, fails.
- Group K (bit-match) and the Burst gate stay green.

### 6.2 Builders: fortress, thief nest and wearers (`Builders/`): implemented, roost route

A collapsed colony **roosts**:
- Members hold still where they are drawn.
- Every alive stomach burns at the species' `Torpor` once a second (`BuilderRoost.Burn`, booked as `Metabolised`).
- Structure, carried and worn prisms stay exactly where they are.
- Nobody is born, builds, steals or dies.

**Core** (`BuilderCore.cs` `BuilderRoost`; each core has `CanRoost`, `RoostSecondsLeft` and `Roost(dt)`):
- **Fortress (`BuilderColonyCore`):** `CanRoost` = nothing carried and no striker. `WindDown` (set by the glue while
  the director asks to collapse) makes a sated idle worker drop its forage goal and take no new one. A hungry worker
  still eats. Carriers finish depositing, so the colony reaches `CanRoost`. Without it, the fortress could roost on
  only ~0% of ticks; with it, 59%.
- **Thief nest:** `CanRoost` = nothing carried and nothing claimed.
- **Wearers (`WearerCore`):** a wearer is a builder colony whose body is worn prisms, so it is covered. `CanRoost` =
  every alive member's leader is in `Thief` phase: no body is approaching, rearing, lunging or recovering. The worn
  body is posed once at collapse and then left alone.
  - **Core fix:** a wearer in `Rear` whose target left (or whose body fell below `HuntAt`) used to hold `Rear`
    forever. It now stands down to `Thief` like `Approach` / `Recover`. The `wearers` group is unchanged and green.

**Glue** (`BuilderColonyFauna` implements `IMacroPopulation`; config `MacroLod` defaults on and `ThawReserveSeconds`
is 20):
- `CanCollapse` requires all of:
  - seeded;
  - no proxies;
  - nobody dying;
  - no vessel sensed;
  - no prism settling (`BuilderPrismWorld.Settling`);
  - core `CanRoost`;
  - torpor left > 2 × thaw.
- When only the carriers, the settles or a hunt block it, the query starts a 2.5 s wind-down. The director only asks
  a population it would collapse.
- `NeedsIndividuals` is a proxy, or torpor left < thaw. **The colony expands before a stomach empties**, so
  starvation stays an individual's death with its crystal.
- `Collapse` publishes one frame with Prev = Cur (`BuildFrame`, `Upload`, `SyncEntities`, `PoseBodies(1)`,
  `SyncIndex`). While collapsed, `Update` only draws.
- `MacroTick` = `Roost(1)` under the `BuilderColonyFauna.Roost` marker.
- `MaterialiseVirtualPrism` (a hit on a virtual member) expands first.
- `Totals`: members, members × |BodyScale| volume, `StomachTotal`.

**Proof** (`builders_harness` group `lod`):
- **The run:** 2 min with a pilot, the pilot leaves, the colony winds down to `CanRoost`, then 900 roost ticks at
  1 Hz, then it steps on.
- **Per species:**

  | | may roost | alive | members moved | world prisms changed | burn err | min torpor left | first step after (own max) |
  |---|---|---|---|---|---|---|---|
  | fortress | 59% of ticks | 48 → 48 | 0 | 0 | 0 | 152 s | 6.01 u (7.35) |
  | thieves | 72% | 8 → 8 | 0 | 0 | 0 | 1366 s | 4.07 u (17.91) |
  | wearers | 100% | 17 → 17 | 0 | 0 | 0 | 780 s | 8.11 u (85.89) |

- **World audit** drift 0. **Ledger** ≤ 3.1e-5 (float stomachs).
- **Quarter-full stomachs:** every species expands before empty (min torpor left 19-20 s) and starves 0 while
  roosting.
- **Negative controls:**
  - `NoBurn`: burn error 2e-2, fails.
  - `KillOnEmpty` with no thaw rule: wearers alive 17 → 0, fails.

### 6.3 Threat flora: never collapsed; a far cadence for the network

Flora is never LOD'd. A grove's mass is the world's, and its bodies are prisms.
- **Snap traps** step at `SnapTrapHz` 20, at 0.0084 ms per step for 60 traps. Cheap, so unchanged.
- **The physarum network** stepped at full rate wherever it was: 1.09 ms per 10 Hz step for the Swarm-cell grove,
  ≈ 11 ms CPU/s. It now runs on **slowed time** when nobody is near. `ThreatGrove.FarScale` passes
  `Advance(dt × FarTimeScale)`:
  - `FarTimeScale` is 0.25 in the config.
  - Full rate applies while any vessel is in the grove's own sense, or any vessel or the main camera is within
    reach + `FarMargin` (400 u, the LOD collapse radius). That wider check runs at 4 Hz.
  - The steps are the same mass-exact 10 Hz steps, with fewer of them per second. The grove grows, eats and beats a
    quarter as fast, so nothing pops.
- **Proof** (`threat_flora_harness` P6):
  - The `ThreatFloraMath.FarTimeScale` rule.
  - A near/far/near schedule over 60 s: network time 22.50 s vs 22.50 expected.
  - Ledger 0 every frame.
  - 178 tubes vs 183 at full rate, under the cap.
  - Far frames cost 25% of full rate.

### 6.4 The swarm's starvation stays on the round-9 clock (decision)

Moving it onto `FaunaStomach` is **not small and safe**:
- The swarm's food already goes to the per-element egg budget (`SwarmTickJob.Stomach`), which laying pays out.
- A stomach that also pays upkeep has to withdraw from that budget inside the tick job. That is a new job API, and
  the egg rate changes, so swarm balance changes.
- A sated swarm used to reset its clock on every bite. Under a burn it would starve unless the upkeep is balanced
  against its intake. That needs a tuning pass, not a mechanical migration.
- The swarm glue is in no headless run, only a type-check, so the change could not be proved tonight.

It stays a follow-up round with its own balance gate (§2.4).

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
| **substrate freeze** (`substrate_harness lod`, round 11f-2) | §6.1: pack frozen 735 ticks, alive 7 → 7, volume / stock deviation 0, hunger error 5.6e-6, first step 10.63 u (bound 25.5); hunt thaws prey; 0.006 ms per tick all frozen vs 1.958 | Deactivate: volume deviation 1614 · NoMetabolism: hunger error 0.96 |
| **builder roost** (`builders_harness lod`, round 11f-2) | §6.2: fortress / thieves / wearers, 900 roost ticks each: alive unchanged, 0 moved, 0 world prisms changed, burn error 0; expand before empty | NoBurn: 2e-2 · KillOnEmpty: 17 → 0 |
| **flora far cadence** (`threat_flora_harness` P6, round 11f-2) | §6.3: network time = dilated sum, ledger 0 every frame, far frames 25% of the cost | n/a (the core is unchanged; the gate is the ledger) |

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
- **Swarm micro starvation** remains the round-9 clock (§2.4, §6.4).
- **Round 11f-2 glue is not run either.** That covers `SubstrateFauna`'s `IMacroPopulation` (it is type-checked by
  `substrate_glue_typecheck`), `BuilderColonyFauna`'s roost and wind-down (type-checked by `swarm_glue_typecheck`),
  and `ThreatGrove.FarScale` (type-checked by `threat_flora_harness`'s stubs). The harnesses model the glue's rules
  around the real cores:
  - the substrate's queued freeze;
  - the builders' wind-down while waiting;
  - the director's collapse test.

  The real frame order, frozen-alpha draw and `Physics` sense are not exercised.
- **The substrate's macro tick does not move a block.** `Translate` is proved, but nothing calls it, because the band
  species have no goal. A frozen block's hunger also runs at the micro step rate, so the frozen pass still costs a
  loop over its slots (cheap: 0.006 ms for the whole core).
- **Builder wind-down feel.** For up to ~2.5 s after the director asks, a fortress's sated workers stop fetching.
  Nobody is near, but a camera far away could notice idle wandering.

## 9. Merging

**Round 11f-2 (`overnight/lod2`, from c6cd3d251, with `cece/swarm-fauna-game` c401af618 merged):**
- Shared core edits, all additive:
  - `SubstrateCore`: `Frozen`, `FrozenPopulation`, `ReserveSeconds`, `ReserveVolume`, `Translate`, `FreezeBug`.
  - `BuilderColonyCore.WindDown`, plus each builder core's `CanRoost` / `RoostSecondsLeft` / `Roost` / `RoostBug`.
  - `BuilderPrismWorld.Settling`.
  - `ThreatFloraMath.FarTimeScale`.
- **Behaviour change:** `WearerCore` stands a target-less `Rear` down to `Thief`.
- **New serialized fields:**
  - `SubstrateSpeciesSO`: `macroLod`, `thawReserveSeconds`.
  - `BuilderColonyConfigSO`: `MacroLod`, `ThawReserveSeconds`.
  - `ThreatGroveConfigSO`: `FarTimeScale`, `FarMargin`.

  The three author scripts write them; re-run them after merging.
- **Stubs:** `swarm_glue_typecheck` `Bounds.center` / `extents`; `substrate_glue_typecheck` compiles the swarm cores
  and `Ecology/*.cs` and gains `Transform.forward` / `Fauna.VesselSenseMask`; the threat-flora `GlueStubs` gains
  `Camera.main`.

**Round 11f:**

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
