# Living Ecology — discoveries

Program: `Tools/Ecology/PROGRAM.md`. Shared harness: `Tools/Ecology/common/` (arena + scripted pilots +
conserved mass, threat/feel scorecard, playback viewer). Template: `examples/demo_pack.py`.

## 2026-10-02 03:50 UTC — program opened
- Harness smoke: a naive 24-agent chase pack at 100 u/s never catches a 120 u/s wandering pilot (0 hits/min),
  and a hunting pilot kills 15.5/min. That's the baseline any real pack-hunter has to beat: a pack that only
  chases is no threat to anything faster than it. A real threat needs coordination (cut-offs, encirclement,
  ambush), not raw speed.

## Hierarchical ecology

*Direction E. Code: `Tools/Ecology/hierarchy/`. Branch `cece/eco-hierarchy`.*

A cell runs fauna at two levels. Far from every pilot they are **macro cohorts**: integer counts per region,
advanced once a second. Near a pilot a region **expands** into individual agents, sampled from that state, and
**collapses** back into it when the pilot has gone and nobody can see the result. **Flora is never LOD'd.** It
is one voxel field both levels graze, so the plants a pilot flies past are the ones the far herds ate.

### The answer, in one paragraph
The macro level is a **cohort model**, the Escalator Boxcar Train from physiologically structured population
ecology, and not a grid of stomach bins. Per region and species it keeps up to 10 cohorts. Each cohort holds:
- a count per element (int64);
- **Σstomach** and **Σstomach²**, which are additive moments, so a merge is an addition and mass is exact;
- the cohort's real **min and max stomach**.

Every count moves by a binomial, hypergeometric or multinomial draw: births from the part of the cohort above
`e_birth`, starvation from the part below 0, predation by a Holling II rate, migration by per-neighbour hops.
The rates are **fitted from the agent level, not chosen**: predation, migration, the grazing occupancy field
and the sprint cost. Three gates prove the result, and each one **fails on planted bugs**:
- conservation to the unit;
- no popping where a pilot can see;
- "macro for T, then expand" matches "micro for T".

On the enriched parameters the whole 1200-u cell (~50k individuals) **cycles for 8 hours with no extinction**.

### Architecture (what the code does)
- **Closed mass ledger.** Mass lives in:
  - flora F;
  - the skeleton K (prism mass left behind when a creature starves);
  - soil nutrient N;
  - the bodies and stomachs of macro cohorts, plus an inbox of migrants bound for a hot region;
  - the bodies and stomachs of agents.

  The flows:

  | Flow | From | To |
  |---|---|---|
  | metabolism | stomach | N |
  | flora growth | N | F (logistic × nutrient limit) |
  | grazing | F / K | stomach |
  | predation | the prey's body and stomach | the predator's stomach |
  | birth | the parent's stomach | the offspring's body and stomach |
  | starvation | body | K |

  There is **no timer and no lifespan anywhere**; the only sinks are starvation and predation, the locked law.
  The ledger is checked to the unit every step (relative drift ~1e-14 over 8 h).
- **Regions.** 912 cubes of 200 u, each holding 64 flora voxels of 50 u.
- **Macro rates fitted from micro** (`calibrate.py`):
  - **Predation:** Holling II, `a = 5.0e-5`, handling `h = 357 s`. Fitted on steady state with the first 100 s
    burned in; 149k predator-seconds, 548 kills.
  - **Migration:** hop rates per face-neighbour, grazer 0.0016–0.010/s and predator 0.003–0.014/s depending on
    hunger (sated, foraging, hungry).
  - **Occupancy.** Grazing at region scale is not uniform over the voxels. Agents bunch on food, so the macro
    level relaxes a per-voxel occupancy field toward `food^0.5`. The fit gave θ = 0.5, with 4.3% grazing error
    against 18.6% for uniform occupancy.
  - **Sprint cost:** `κ × P(encounter)`, with κ measured from agent sprint time: grazers 0.36, hunters 0.019.
- **LOD** (`sim.py`):
  - A region turns hot within `expand_radius` (280) of a pilot, or within 400 ahead inside the forward cone.
  - It collapses only past `collapse_radius` (450, which gives hysteresis), and only when **neither the agent's
    simulated position nor its drawn position** is visible to any pilot.
  - An agent budget (4.5k) scales the radii by the cube root of budget/agents.
- **Expansion is sampled, not invented.**
  - A cohort expands into exactly N agents with stomachs drawn from its Normal, clipped to its [lo, hi] and then
    shifted so their sum is exact.
  - Predators are placed uniformly. Grazers are rejection-sampled outside the predators' flee radius, because
    the macro state is a steady state and a fresh uniform scatter is not: it massacred 114 grazers in 30 s
    where the steady state kills 11.
  - Agents **emerge** from the region's impostor representatives rather than appearing.
  - Migrants arriving from a cold region spawn at the source region's representative.
- **Impostors.** 6 drawn representatives per (region, species), sized `r_body·(n/k)^(1/3)` so the cloud carries
  the region's volume. Expansion lerps agents out of them, and collapse is gated off-screen.

### Gates (each fails on planted bugs)
| gate | clean | planted bugs (all fail) |
|---|---|---|
| **conservation** (`test_conservation`) | ledger drift 5.8e-14 rel, LOD mass mismatch 2e-10, count mismatch 0, 182k expands / 185k absorbs / 4.9k arrivals | `absorb_drop_stomach` (drift 3.6e-3), `birth_free_body` (1.7e-3), `expand_lose_pool` (1.6e-2): each run 300 s and exercised |
| **continuity** (`test_continuity`, 2 pilots, final code) | **0** pop-in / 0 pop-out / 0 teleport over 572k pilot-seen agent-ticks (97k expands, 98k absorbs); 1.38M in an earlier wider-LOD run | `absorb_ignores_visibility` 74 pop-outs, `expand_no_emerge` 9.1k pop-ins + 6.3k teleports |
| **consistency** (`test_consistency`, 4 seeds, T = 300 s, 600-u world, **calibrated regime** flora cap 120) | herb count 0.2%, mean stomach 0.5%, phase TV 0.040; pred count 2.8%; kills 8.8%; flora 0.4%. **Switch arm** (pilot arrives half way): 0.3% / 0.5% / 1.5% | `macro_graze_x1.5` (herb count 63%), `expand_mean_field` (stomach SD 78%), `macro_attack_x2` (kills 49%) |

The consistency gate runs three arms per seed: all-micro for T; all-macro for T, then expanded; and a pilot
sweeping through. It compares, per species: headcount, mean and SD of stomach, the hunger-phase histogram
(sated / foraging / hungry), regional flora, and kills over the window. Tolerances are 10% count, 10% mean,
25% SD, 0.08 phase total variation, 5% flora and 30% kills.

**Consistency in the enriched regime (flora cap 240) is NOT established: an honest gap.**
(`--regime enriched`, `results/gate_consistency_enriched.json`.)

| Measure | Result |
|---|---|
| switch arm (pilot arrives half way) | passes everywhere: herbivore count 1.0%, mean stomach 1.5% |
| herbivore count, all predator measures, kills, flora | pass, with predators at 2.2% and kills at 1.3% |
| herbivore mean stomach | **fails at 16%** |
| herbivore phase total variation | **fails at 0.145** |

**Why.** With food abundant, the whole herd fills toward `e_birth` together and breeds in **one
synchronised burst** inside the window. In micro, births run 1 → 11 → 58 → 224 → 721 → 1758 → 3101 at
t = 120 … 300. The macro level starts earlier and rises slower: 21 → … → 2240. At t = 300 the snapshot
therefore lands mid-burst, where a 15 s timing error is a 16% stomach error.

**What was measured:**
- The macro mean lags micro by ~1% fill rate (−0.03 → −0.19 by 120 s).
- Macro's total stomach spread contracts less: 1.53 → 1.39, against micro's 1.10, which is exactly the
  satiation contraction ×0.77.
- Region-to-region spread is *not* the cause: macro is tighter there (sd 0.16–0.24 against micro's 0.21–0.28).

**Tried and failed to recover the sharp burst:**

| Change | Result |
|---|---|
| occupancy θ 0–1 | θ = 1 matches the mean, but births leak 90 at 120 s |
| stomach diffusion 0–0.0009 | still 14 early births |
| merge tolerance 0.06 → 0.02, cohorts 10 → 16 | no change |
| bounded cohorts | no change |

**Read it as:** a near-deterministic threshold crossing is the worst case for any moment-closed population
model. The Normal-within-cohort closure leaks an upper tail that the real, contracted distribution does not
have. The cure, not yet tried, is to track a third moment (skew) or to represent the top cohort's upper edge
explicitly. In the game the exposure is small: the burst is a regional event far from pilots, and the
long-run statistics (cycle amplitude, period, persistence) are what a pilot sees.

### Living dynamics
- **8 h at the first calibration (flora cap 120): a stable equilibrium, recorded as a negative.**
  - After a transient the cell settles at ~34k grazers / 12k predators: cv 0.25 / 0.24, regional sync 0.73,
    no detectable period.
  - Alive, but it does not breathe.
- **Enrichment turns it into cycles** (`sweep_d`, then 8 h at flora cap 240; `results/longrun_cycles8h_*.png`, reproduced on the final bounded-cohort code as `longrun_cycles8h_final_*` with grazers 18.8k–163k, predators 3.7k–34k, cv 0.68 / 0.42 and ledger drift 5.3e-15).
  This is the paradox of enrichment, in the cell.
  - Grazers 19k–164k, predators 3.8k–34k, cv 0.69 / 0.43.
  - **No extinction**, either cell-wide or in any region (0 local extinctions).
  - A ~2.3 h cycle with predators lagging grazers.
  - Flora and soil nutrient oscillate in counter-phase: the grazed-down cell regrows, which is emergent
    succession and seasons with no clock in the model.
  - About 2,600 region hops/s of migration; ledger drift 6.7e-15 over 8 h.
- **Cost of that liveliness: the cycle is cell-wide** (regional sync 0.95), not a patchwork. In the game that
  means a whole cell goes boom-and-bust together. It is a design choice: smaller flora_r, or weaker migration,
  desynchronises regions at the cost of amplitude. That trade-off was not swept.
- **Sweeps** (macro-only, 1 h, `results/sweep_*.json`):
  - Predators crash at metabolism ≥ 0.06, and are viable at ≤ 0.025 with h_half 90.
  - Cap 240 with predator metabolism 0.03 nearly kills predators (min 43). The window is narrow; keep 0.02.

### Cost (`cost/measure.py`, `results/cost.json`)
50k+ individuals in a 1200-u cell; pilots wander at 120–210 u/s.

| pilots | expanded agents mean / p95 | compiled micro per 60 fps frame (10 Hz) | macro per frame (1 Hz) |
|---|---|---|---|
| 1 | 1.9k / 2.2k | 0.15 ms | 0.036 ms |
| 2 | 3.3k / 4.0k | 0.31 ms | 0.036 ms |
| 4 | 5.4k / 6.4k | 0.58 ms | 0.036 ms |

- **Micro** is a compiled C kernel standing in for Burst: ~390–520 ns per agent per tick, off the main thread.
- **Macro** is 2.1 ms per 1 Hz step (117 ns per cohort slot over 912 × 10 × 2 slots plus 58k voxels). That is
  noise next to the micro.
- The Python reference is 3.6–8.6 ms per micro tick and ~31 ms per macro step.
- **The budget is the lever:** without it, 4 pilots expanded up to 18k agents (`cost_no_budget.json`).

### Negatives (what failed and by how much)
1. **Stomach-bin (Eulerian) macro level** (`macro_bins.py`). Upwind advection of a stomach histogram smears it
   by numerical diffusion. The hunger-phase TV against micro was 0.37 with 8 bins and still 0.27 with 32. The
   cohort rewrite fixed this outright, because a cohort's moments are transported exactly.
2. **Herbivores could never breed.** The satiation cap sat below `e_birth`. Phases are now defined relative to
   `e_birth`.
3. **Grazing credit leak** of ~0.5 volume per step. Gain was credited after predation had removed the grazers;
   it is now credited inside the grazing step.
4. **Uniform occupancy:** 18.6% grazing error. A fixed exponent was regime-dependent, so occupancy became a
   relaxing field.
5. **Poisson sprint cost** overestimated how long hunters chase by ~50×. κ is now measured.
6. **Predation fitted from probes, and from the start-up transient,** was biased. Steady-state burn-in fixed it.
7. **Fresh uniform expansion is a massacre:** 114 vs 11 kills in 30 s. Grazers are now rejection-sampled.
8. **Absorb that checks only the sim position** popped one agent out. Emerging agents are drawn elsewhere than
   they are simulated; absorb now checks both.
9. **Normal tails without bounds** (a partial fix; see the enriched-regime gap above). In the enriched regime macro births started ~90–150 s early: the Normal tail
   extends past the cohort's real maximum stomach, so a cohort that cannot yet breed "breeds". The fix is
   **bounded cohorts**: two floats per cohort, moved by the same drift as the mean. Births and starvation are
   then drawn only from the part of the Normal inside [lo, hi]. `macro_unbounded_cohorts` reproduces the old
   behaviour.
10. **Negative controls that did not bite.**
    - `birth_free_body` **passed** when its run lasted only 120 s, because no agent had bred yet: a vacuous
      control. Every bug run now uses the clean run's 300 s horizon and must be exercised.
    - `expand_flat_energy` was harmless, and became `expand_mean_field`.
    - `macro_no_sprint_cost` turned harmless once the sprint cost was measured as tiny. It became
      `macro_attack_x2`, plus a kills (flow) metric, because stocks over 300 s barely move under a predation bug
      while flows do.
11. **The first LOD radii** expanded 6.3k–18k agents with 1–4 pilots.

### Recommended game architecture
This plugs into what the Cell already owns (`Docs/ECOSYSTEM.md` §0–§15, `Tools/ecosim/`):
1. **Regions live inside `Cell`.**
   - Partition the membrane volume into ~200-u regions; this is the macro grid.
   - Prism mass stays real everywhere: flora grows as prisms in every region, hot or not, so the shared flora
     field is the prism field itself, and `PrismSpatialIndex` is the voxel lookup.
   - Only **fauna** are LOD'd.
2. **The volume spine reads both levels.**
   - `Cell.LiveVolume` (§13) is already per-domain prism volume, and it stays exact because flora and skeleton
     are real prisms.
   - Fauna body volume adds the macro ledger: count × body per cohort, which is exact and needs no agents.
   - The phase ladder therefore sees the same number whether or not anyone is near.
3. **One stomach replaces the starvation clock.**
   - `Fauna.starvationSeconds` measures time since the last feed. It is a timer standing in for an energy
     budget.
   - Give each creature a stomach that metabolism drains and feeding fills, and starve it at zero.
   - This is the same prey-linked starvation (§6 option C), but it is **conserved**: what is burned goes to
     nutrient, and the body goes to a skeleton.
   - It is also what lets a cohort summarise many creatures exactly.
4. **Spawn profiles become initial conditions and caps, not producers.**
   - `SpawnProfileSO.FaunaPopulationScale` / `MaxLivePopulation` / `Cell.ResolveFaunaCap` seed the macro counts
     and remain a hard cap on births. That is production gating, which is allowed.
   - The seeder survives only as extinction recovery.
   - Births come from the stomach tail, at the birth threshold the species already authors.
   - `CurrentFaunaSpawnPeriod` becomes the macro tick's breeding cadence, which is a biome property as §23.9
     already says.
5. **Jobs.**
   - **Macro:** a 1 Hz Burst job over regions × cohorts. Pure integer counts and moments, deterministic per seed
     and cheap enough to run on a server.
   - **Micro:** a 10 Hz Burst job over expanded agents, interpolated per frame.
   - **Impostors:** GPU-instanced, 6 per region and species.
6. **Networking.**
   - Replicate the **macro state** (counts per region, cohort, element; a few KB per second).
   - Each peer expands locally around its own pilots from the shared state.
   - Only the hot regions near *someone's* pilot need agent-level authority. This sidesteps today's per-peer
     fauna divergence (`CellNetworkSync`) for everything off-screen.
7. **The dials that shape the experience**, all fitted or swept here:

   | Dial | Effect |
   |---|---|
   | flora cap / growth rate | equilibrium vs cycles (paradox of enrichment) |
   | predator metabolism | predator persistence; narrow window |
   | migration hop rate | cell-wide vs patchy cycles |
   | `agent_budget` | LOD radii and cost |

### Open
- **Enriched-regime consistency** (above): close the synchronised-burst timing with a skew moment per cohort,
  then promote `--regime enriched` to a gate.
- The cycle is cell-synchronous. Desynchronising patches (cell-scale metapopulation) was not swept.
- Consistency is proven at T = 300 s on a 600-u world. Longer horizons rely on the 8 h macro run plus
  continuity, not on a direct micro-vs-macro 8 h comparison, which is too slow in Python.
- The C kernels are a proxy for Burst; the real numbers need a profiler in the editor.
