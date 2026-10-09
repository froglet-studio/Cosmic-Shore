# Evolution — the heritable genome, and the evidence that the HyperSea evolves

**Status (2026-10-09, branch `cece/friendly-goodall-woddjw`):** code-complete and headless-proven; **not yet run in
the Unity Editor** (§9 is the human gate). Masterplan Phase 3's open TODO (the genome) is landed and Phase 4's
evidence exists for the model; the in-game ledger has compiled through Prisma's live compile and booted the menu
headless with no new error, but no human has watched a cell evolve on screen.

`Docs/ECOSYSTEM_MASTERPLAN.md` §3 names the gap to a defensible artificial-life claim: *reproducing individuals with
a genome that causally drives phenotype; heritable mutation with smooth, non-lethal pathways; an economy where
survival is fitness; and EVIDENCE — lineage tracking, trait distributions over time, and a mutation-off / selection-off
control run that shows no adaptation.* This document is that work. The one-line claim it earns:

> **Under the shipped fauna rules, a population's heritable traits move in opposite directions in different
> environments (slower under famine, faster on rich food), and do not move when either mutation or selection is
> switched off.** That is natural selection, not a ratchet, and the neutral locus every run carries drifts
> instead of adapting.

The mechanics log for the ecology is `Docs/ECOSYSTEM.md` (§59 is this feature's entry); the locked invariants are
`Docs/claude/ECOSYSTEM_DESIGN_PRINCIPLES.md`. The Darwin Lab, where a designer tunes a biome's knobs by watching:
https://claude.ai/artifact/5axngABeaGGT2ev4ZR2ZaL (source `Tools/Evolution/`, §7).

---

## 1. The design: four loci, every one a trade-off, none of them a size

A genome is a **fixed vector of four genes in [-1, 1]** (`LifeformGenome`). Gene 0 is the species exactly as its
element authored it; the sign is which way the trait leans and the magnitude how far. It rides the **inheritance
channel that already existed** — `LifeformVariantPick`, the element-and-tuning pick a parent hands its offspring
(`Docs/ECOSYSTEM.md` §17) — so there is one heredity path, not two, which is what the masterplan asked for.

| Locus | What +1 does | What it COSTS | Why that pairing |
|---|---|---|---|
| **Tempo** | speed band × `TempoPaceRange` (1.5) | stomach upkeep × `TempoUpkeepRange` (1.84 ≈ 1.5^1.5) | a faster creature reaches food sooner and burns it faster: the optimum depends on how dense the food is |
| **Reach** | graze / interaction radius × `ReachRadiusRange` (1.5) | upkeep × `ReachUpkeepRange` (1.36) | sensing further finds more mass and costs more to run |
| **Fecundity** | feeds per birth ÷ `FecundityRange` (2) | each child born with its stomach ÷ `FecundityProvisionRange` (2) | the r/K axis: many hungry children against few full ones. One-sided — below 0 the locus only costs, so evolution leaves the founder on this axis only when more births pay |
| **Cohesion** | cohesion radius × `CohesionRange` (1.6) | (none in the arena) | the flock-tightness knob the boid already has; in the evidence model it has **no mechanism**, which makes it the built-in **neutral control** every run carries |

**Why these four and not size.** `Docs/ECOSYSTEM.md` §40 retired every per-individual size axis: *a lifeform is its
species and its element, and the element states everything about itself exactly once — body, leaf, heart.* The
masterplan's Phase 3 list named size as a candidate trait, and §40 postdates it. This genome keeps §40: every locus is
**behaviour or metabolism, read off the creature's motion and never off its silhouette**, and the heart is untouched.
Whether a size locus is wanted at all is a design call (§8), not something this branch takes.

**Why the costs are authored multipliers rather than exponents.** The cost of a trait is a tunable biome parameter
(masterplan §1: tunable and variable), and `pow()` is not bit-identical across runtimes while the lab's parity gate
holds the C# and its JavaScript port to the same trajectory (§7). So each cost is its own multiplier at gene +1,
through the same map as the benefit.

**What a gene never does.** It never assigns a fitness. There is no scoring, no scripted outcome, no selector:
the economy (starvation, predation, reproduction) decides who leaves descendants, which is the whole of the
*endogenous selection only* invariant. `EvolutionLedger` records what the economy did and influences nothing.

## 2. Expression: genotype → phenotype

`GenomeExpression.Multiplier(gene, range)` is the piecewise-rational

```
f(g) = 1 + (R − 1) g           g ≥ 0
f(g) = 1 / (1 + (R − 1)(−g))   g < 0
```

with `f(0) = 1`, `f(+1) = R`, `f(−1) = 1/R`, `f(−g) · f(g) = 1` exactly, C1 at 0, monotonic for every `R ≥ 1`,
and only `+ − × ÷` (no transcendental function, so the JavaScript port is exact). A range below 1 or NaN reads as 1:
a hand-edited asset can make a locus inert, never invert it.

`Express(genome, settings)` returns `LifeformPhenotype { Pace, Reach, Upkeep, Fecundity, Provision, Cohesion }`:

- `Upkeep = Multiplier(tempo, TempoUpkeepRange) × Multiplier(reach, ReachUpkeepRange)` — fast AND far pays both;
- `Provision = min(1, 1 / Multiplier(fecundity, FecundityProvisionRange))` for `fecundity > 0`, else 1, floored at
  `MinProvision` 0.1 (a child is never born starving);
- `FeedsPerOffspring(authored, p)` = `round(authored / Fecundity)`, never below 1, and 0 stays 0 (a species that does
  not reproduce cannot be bought a birth);
- `StarvationSeconds(authored, p)` = `authored / Upkeep`, and 0 (never starves) stays 0.

With `EvolutionSettings.Enabled` off, `Express` returns `Neutral` for every genome, and every number below is the
shipped number exactly.

## 3. Mutation and the random stream

`GenomeMutation.Mutate(parent, settings, rng)`: per locus, with probability `MutationRate` (1), add a normal step of
`MutationSigma` (0.08) and clamp to the band. `Founder(settings, rng)`: a seeder-spawned creature's genes, each a
normal draw of `FounderSpread` (0.1) — standing variation for selection to act on from the first death; 0 makes every
founder the authored species. With the switch off, or rate / sigma / spread 0, no draw is made at all, so a biome
without mutation does not disturb a stream a biome with it would see.

The normal is **Irwin–Hall** (twelve uniforms summed, minus six): bounded to ±6σ — a mutation is never a six-sigma
outlier, which is the non-lethal pathway the masterplan asks for — and exact across runtimes. The stream is
**xoshiro128\*\*** (`GenomeRng`, 32-bit state, 24-bit-mantissa uniforms): the port is plain `uint32` arithmetic.

**Server-authoritative, reproducible (masterplan §8).** Only the ONE simulation rolls a child's genome
(`Fauna.TryReproduce` is `IsSimAuthority`-gated); the result rides the spawn payload (`FaunaNetworkSync.FaunaIdentity.Genome`,
one `uint`, one signed byte per locus, ±1/254 resolution — finer than any mutation sigma a biome authors) so peers
never roll. Each `Cell` owns one `GenomeRng` seeded from its ID and the session's `UnityEngine.Random` stream, so a
deterministic session replays the same lineages.

## 4. Where it lives in the game

Core (pure C#, `Assets/_Scripts/Controller/Environment/FloraAndFauna/Evolution/`, compiled and RUN headless by the
harness and ported exactly to the lab):

| File | What |
|---|---|
| `LifeformGenome.cs` | `GenomeLocus`, the immutable four-gene struct, the wire format (`Pack`/`Unpack`) |
| `EvolutionSettings.cs` | the biome block on `CellConfigDataSO` (`Enabled` **false** by default, mutation, the seven ranges, the ledger cadence) |
| `GenomeExpression.cs` | the map, `LifeformPhenotype`, `FeedsPerOffspring`, `StarvationSeconds` |
| `GenomeMutation.cs` | `IGenomeRng`, `GenomeRng`, `Normal`, `Mutate`, `Founder` |
| `EvolutionLedger.cs` | lineages, generations, births, deaths by `LifeformDeathCause`, census rows, `ToJson` |

The seams (each one line or one block, all inert while `Evolution.Enabled` is off):

| Seam | Where | What it does |
|---|---|---|
| the pick carries a genome | `LifeformVariantPick<T>.Genome`, `WithGenome` | the founder value unless something rolls one |
| founders roll | `Fauna.AssignLineage` (no `inherit`), `CellLifeSpawnerBase.SpawnFlora` (no `inherit`) | `Cell.GenomeForFounder()` |
| offspring mutate | `Fauna.SpawnOffspring`, `Flora.SpawnOffspring` | `Cell.OffspringPick(parentPick)` — the ONE place a lineage's genome changes |
| a split does not mutate | `WormFauna` passes `VariantPick` verbatim | the two halves are the same animal |
| a puppet never rolls | `Fauna.ApplyReplicatedIdentity(..., packedGenome)` | the wire's genome arrives as the inherit pick |
| expression | `Fauna.ApplyPhenotype` (upkeep → `starvationSeconds`), `Boid.ApplyPhenotype` (speed band, graze radius, cohesion radius), `LightFauna.ApplyPhenotype` (per-instance multipliers on the shared `LightFaunaDataSO`'s speed band and consume radius) | runs once at lineage bind, AFTER `ApplyVariantTuning`, so gene 0 is the element exactly |
| births | `Fauna.TryReproduce` → `GenomeExpression.FeedsPerOffspring` | the Fecundity locus |
| provisioning | `Fauna.StartStomach` → `FaunaStomach.FillFraction` | a child born part full; the parent pays nothing, as today |
| the ledger | `Fauna.RecordInLedger` / `RecordDeathInLedger`, `Cell.RecordLifeform*`, `Cell.EvolutionCensusLoop` | authority only; a creature destroyed without dying is a `Teardown`, never selection |

`Cell.ResetEvolution()` clears the ledger at every registry reset (Initialize's fresh pass, `ResetCell`, both of the
swap's bookkeeping resets, `OnDestroy`) — the same sites the colony frontiers are retired from, because a book a
population owns is the cell's to drop (`/ecology` §2.6).

**Flora** carry and inherit the genome and mutate it, but **express nothing** yet: a plant's genome is a neutral
lineage marker until a plant trait with a real cost is chosen (§8).

## 5. The ledger and what it shows

`Cell.EvolutionLedger` records every founder (generation 0, its own lineage), every birth (its parent's lineage,
generation + 1), every death with its cause (`Starvation`, `Predation`, `Vessel`, `Joust`, `Teardown`, `Other`), and
a census row per species every `SnapshotIntervalSeconds` (30): population, per-locus mean / sd / min / max, mean and
max generation, distinct lineages still represented, births and deaths since the previous row. Bounded: the living
set, cumulative counters, and at most `MaxSnapshotsPerSpecies` rows.

- **FrogletTools ▸ Ecology ▸ Evolution Monitor** (`Assets/_Scripts/Editor/Ecology/EvolutionMonitorWindow.cs`) draws
  it live in Play mode — per species: a histogram per locus of the living genes, the mean's trace with a ± sd band
  over the census rows, the drift since the first row, lineages, generations, deaths by cause — and exports the JSON.
  A reader tool: it writes no asset and carries no ship panel. **Not yet opened in an Editor** (§9).
- The **`Ecology` log channel** (off by default; FrogletTools ▸ Toolbox ▸ Logging) gets one line per species per
  census — a periodic population fact, never per frame.
- `EvolutionLedger.ToJson()` is the file the doc tables and the lab's future "from the game" tab read.

## 6. The evidence

`Tools/Build/evolution_harness/run.sh` compiles the shipped core first against **netstandard2.1 / C# 9 with warnings
as errors** (Unity's profile — a core that would not compile in the Editor fails here), then runs:

- **`core`** — 46 gates on the core with planted-bug negative controls (a linear map must fail the reciprocal test, a
  biased uniform must shift the normal, a corrupted wire byte must not be the same genome, a census that ignored
  births must read the founder);
- **`arena`** — the evidence model: same seed → same trajectory draw for draw; the selection-off control is EXACTLY
  the run whose every range is 1; mutation off with no founder spread never leaves the authored species; the caps hold,
  and a cap the reproduction path ignores is caught; a stomach that never drains is caught;
- **`evidence`** — the runs below, written to `Tools/Evolution/results/results.json` (+ `shipped.json`, every number's
  provenance);
- **`golden`** — the parity trajectories for the lab (§7).

The 26 NUnit edit-mode tests in `Assets/_Scripts/Tests/Editor/LifeformGenomeTests.cs` pin the same contract for the
Editor (run here under dotnet with NUnit 3.14: 26 passed).

### 6.1 The model

`EvolutionArena.cs` runs the **shipped fauna rules** — feeds-counted births with a cooldown and a cap, a stomach that
drains at `Capacity / starvationSeconds` and refills on a feed, predators that take one prey per hunt interval, a
seeder that tops a species back up to its floor — as a **well-mixed cell**, over the shipped genome core. Every
authored number comes from the Blob cell's assets (tadpole: 20 feeds per birth, 10 s cooldown, cap 6, floor 4, 90 s
starvation; shark: 6 kills per birth, 30 s, cap 2, floor 1, 45 s; wave 15 s; food ceiling 12000 / 16 = 750 prisms);
three are model assumptions and say so (flora regrowth 20 prisms/s and graze rate 1.15 /s from `Tools/ecosim`,
half-saturation 150). It is not the game: no space ("reach" is an encounter multiplier), predators pick prey
uniformly, the flora is a pool. It IS the economy the genome is selected by.

The shipped Blob cell caps its tadpoles at **6**, and at 6 drift beats selection. The runs use the shipped population
lever (`SpawnProfileSO.FaunaPopulationScale`) at ×20 (cap 120, floor 80; sharks ×5) and, for the economy-limited
regimes, ×50 (cap 300, floor 200), and say so.

### 6.2 Results — 9 regimes × 5 seeds × 4 hours of cell time (generated 2026-10-09, ~8 s)

Δ = time-averaged mean gene over the last quarter minus the first census; mean ± sd across seeds.

| Regime | population | Δ tempo | Δ reach | Δ fecundity | Δ cohesion (neutral) | max gen | starved / eaten per run |
|---|---|---|---|---|---|---|---|
| **blob** — as authored ×20, sharks ×5 (cap-bound) | 119.9 | **+0.710** ±0.074 | **+0.870** ±0.016 | **+0.692** ±0.126 | −0.177 ±0.276 | 61 | 1382 / 6878 |
| **famine** — ×50 on the shipped food (economy-bound) | 274.8 | **−0.318** ±0.043 | **+0.790** ±0.030 | +0.193 ±0.045 | −0.108 ±0.165 | 31 | 5743 / 6939 |
| **sparse** — ×50, a third of the food | 200.1 | −0.006 ±0.013 | +0.017 ±0.013 | +0.019 ±0.014 | −0.002 ±0.010 | 11 | 2495 / 6856 |
| **rich** — twice the food | 120.0 | **+0.747** ±0.071 | **+0.856** ±0.019 | +0.276 ±0.201 | −0.096 ±0.436 | 52 | 96 / 6882 |
| **nopred** — no sharks | 120.0 | +0.026 ±0.049 | +0.050 ±0.036 | +0.025 ±0.052 | −0.014 ±0.023 | 6 | 0 / 719 |
| **control: mutation off** (founder spread kept) | 120.0 | +0.130 ±0.075 | +0.156 ±0.057 | +0.136 ±0.055 | +0.023 ±0.113 | 61 | 1 / 6897 |
| **control: selection off** (inert phenotype) | 120.0 | −0.082 ±0.231 | −0.053 ±0.199 | −0.076 ±0.197 | −0.027 ±0.135 | 59 | 0 / 6883 |
| **control: selection off, famine** | 300.0 | +0.044 ±0.093 | −0.006 ±0.026 | +0.031 ±0.050 | −0.034 ±0.057 | 24 | 952 / 7039 |
| **closed** — famine with no seeder | 284.1 | **−0.447** ±0.120 | **+0.827** ±0.061 | +0.175 ±0.045 | −0.068 ±0.220 | 29 | 5574 / 6945 |

### 6.3 What the numbers say

1. **The direction of adaptation depends on the environment.** Tempo goes **down** under famine (−0.32; −0.45 with
   the seeder off) and **up** on rich food (+0.75): a slow creature burns its stomach slower and outlasts a famine; a
   fast one reaches abundant food first. The same locus, the same code, opposite optima. A ratchet (a trait that is
   simply better) would move one way everywhere.
2. **The controls do not adapt.** With selection off the means wander ±0.1 with a seed-to-seed spread of ±0.2 — a
   random walk with no direction. With mutation off they move about +0.14 and stop: selection on the standing
   variation only, until it fixes. Both are the "abiotic baseline" the masterplan's item 4 asks for, and the measured
   **noise band** a real shift has to clear: a shift inside ±0.2 is a plateau.
3. **The neutral locus drifts but does not adapt.** Cohesion, which has no mechanism in the model, lands at −0.18 to
   +0.02 with the widest spreads in the table (±0.28, ±0.44): genetic draft — a lineage that wins on reach carries its
   cohesion value along — not selection. Every run carries this control.
4. **No turnover, no evolution.** Without predators (and with food too rich to starve anyone) nothing dies, six
   generations are born in four hours, and nothing moves (+0.03 ±0.05).
5. **A seeder that tops up to a high floor is an immigration pressure that erases adaptation.** `sparse` sits exactly
   at its floor (200.1) with 6,807 founders injected per run against 2,744 births, and shows no shift at all; the same
   famine with the seeder off (`closed`) shows the largest shifts in the table. **For evolution to be visible in a
   biome, its seed floor must sit well below what the cell carries** — a tuning rule for any biome that turns the switch
   on (§8).
6. **Selection lowered the carrying capacity.** The selected famine population sits at 275 while its inert twin sits
   at the cap of 300: individuals that evolve a longer reach feed faster AND burn more, and the population pays for
   the individual's advantage. That is the textbook result (evolution does not maximise population), and it is the
   kind of emergent consequence a designer will want to see before switching a biome on.
7. **Fecundity leans r wherever turnover is high** (+0.69 cap-bound with sharks, +0.19 in famine) and barely moves
   without it. Where slots keep opening, the lineage that fills them fastest wins.

### 6.4 The NASA scorecard, after this branch (masterplan §3)

| Criterion | Before | Now |
|---|---|---|
| Heredity | 🟡 the element and its tuning | ✅ a four-locus genome, inherited through the same pick |
| Variation / mutation | ❌ | ✅ bounded Gaussian steps on every birth, standing variation on seeding |
| Selection | 🟡 substrate | ✅ endogenous: the economy decides; nothing scores |
| **Adaptation / evolution** | ❌ | 🟡 **demonstrated in the shipped-rules model with controls** (§6.2); in-game ledger built, **not yet observed in the Editor** (§9); open-endedness (speciation, arms races) untouched |

## 7. The Darwin Lab

`Tools/Evolution/` — a browser studio on the `/labmaker` contract, published at
https://claude.ai/artifact/5axngABeaGGT2ev4ZR2ZaL (private until shared).

- **`sim.js`** is the EXACT JavaScript port of the core and the arena. **`gate_parity.cjs`** holds it to the C#:
  `node Tools/Evolution/gate_parity.cjs` replays the four golden cases (`results/golden.json`: blob, sparse,
  selection-off, conserved stomach; up to 2,400 steps each) and requires every census row identical `===`, the RNG's
  first draws, eight mutation samples, a phenotype and a packed genome bit for bit — then plants three defects (a
  1e-9 nudge off the float32 lattice, one extra RNG draw, the cost of pace dropped) and requires each to be caught.
  The third taught the gate something: in the cap-bound Blob balance nobody starves inside twenty minutes, so a
  defect in upkeep is invisible there; the control runs where the stomach decides. The page re-runs the cases itself
  (Parity tab).
- **`build_lab.py`** bakes `lab_template.html` + `sim.js` + `results/*.json` into `DarwinLab.html` (standalone,
  `file://`) and `DarwinLab.artifact.html` (the body the Artifact tool publishes). It **re-reads every shipped number
  from the real assets** (the two fauna configs, the two prefabs, the spawn profile, the C# defaults) and bakes any
  mismatch into the page as an on-screen "ASSET MOVED" flag; `--check` fails on drift or a stale page.
- The page: a live arena (population and food, four trait traces with ± sd bands and histograms, the living genomes
  as a tempo × reach scatter) at 10–1000×; the Biome / Species / Mutation / Expression / Controls sliders, every one
  naming its source and showing shipped against yours; regime presets (the §6.2 rows); a **Scorecard** that runs fresh
  arenas headless for your settings and its two controls (3 seeds × 1 h, about a second) and reports the shifts; the
  baked **Evidence** with a sparkline per regime; the **Parity** proof; a **Decisions** log (shared through the
  artifact's db, with a browser fallback); and an **About** with what the lab does not model.
- `node .claude/skills/labmaker/verify_lab.cjs Tools/Evolution/DarwinLab.html` passes (desktop 1600 × 900 and iPhone
  13: no console error, no overflow, the `__lab` hook, SPEC within SHIPPED, the manual clock, a deterministic batch,
  a drawn stage).

A round: read the decision log (`ArtifactData list decisions`), apply the decisions, re-run the harness if the model
moved, rebuild, re-verify, republish to the same URL, write the round up in `Tools/Evolution/README.md`.

## 8. Open — decisions needed (designer), and what is deliberately not built

- **Which biome turns the switch on first, and with what numbers.** Every shipped biome ships `Enabled: false`. The
  evidence says a biome needs turnover (predators or famine) and a seed floor well below its carrying capacity to show
  anything; the shipped Blob cell (cap 6, floor 4) shows drift. Candidates: a cell with the conserved stomach (the
  Swarm cell) where upkeep bites, or a Wildlife cell with a real predator tier. *Decision needed.*
- **A size locus.** §40 keeps size per species × element; the masterplan's Phase 3 list named size. The genome keeps
  §40 and expresses nothing visible on the silhouette — which is also the masterplan's §8 worry (*evolution legibility*).
  The honest options: a size locus with §40 re-opened, or a visible non-size expression (a tint on the spindle
  material's rim, a sway amplitude). *Decision needed.*
- **Flora expression.** The genome is carried and mutates on plants; no plant trait has a cost yet. Growth tempo
  against prism budget would be the plant's r/K. *Not built.*
- **Predator genomes.** Sharks carry none in the model; in the game a `LightFauna` predator inherits like any fauna
  and the Tempo / Reach loci express on it (speed, consume radius), so sharks DO evolve in the game once a biome is
  on. The arena's predators are fixed to keep the first claim clean. *Model gap, stated on the page.*
- **The provisioning is paid by nobody.** A child is born `Provision` full and the parent's stomach is untouched, as
  today's births are. Under the conserved stomach the honest rule is a transfer. *Not built; the arena matches the
  game.*
- **The `Cohesion` locus has no mechanism in the arena** and is the neutral control; in the game it scales the boid's
  cohesion radius, which has real flocking consequences the arena cannot see.
- **Open-endedness** (speciation, arms races) is the frontier the masterplan hedges on; nothing here claims it.

## 9. Verify in-editor (the human is the gate — none of this has been run in Unity)

1. **Compile.** Open the project; the new folder `FloraAndFauna/Evolution/` and the window must compile. Run the
   edit-mode suite: `LifeformGenomeTests` (26) green, and the ecology suites unchanged.
2. **Switch off = shipped.** Menu_Main freestyle as today: no new log line, no behaviour change (every biome is off).
3. **Switch on.** On `Blob Cell Config` (or the biome chosen in §8) set `Evolution ▸ Enabled`, keep the defaults;
   open **FrogletTools ▸ Ecology ▸ Evolution Monitor**; Play. Expect: species cards appear with the seeder's first wave
   (founders, generation 0, gene histograms spread by `FounderSpread`); after a feed-fed birth, generation 1 and a
   lineage count; after a starvation or a shark kill, the deaths-by-cause row moves. The `Ecology` channel prints one
   census line per species every 30 s.
4. **A birth inherits.** Watch one tadpole's brood: a child's histogram bar sits near its parent's (±0.1), never a
   fresh founder spread.
5. **Expression is visible on the creature.** Spawn Matrix a tadpole, then raise `TempoPaceRange` to 3 and
   `FounderSpread` to 0.5 on that biome: the spread of swimming speeds must widen visibly; set `Enabled` off: uniform
   again.
6. **Nothing pops.** A newborn with `Provision` < 1 is born part full and still blooms in; a starved one withers
   extremities-first and drops its heart as before.
7. **Network.** Host + client on a `NetworkSynced` species: a client's puppet shows the same element AND the same
   speed band as the server's creature (the genome rode the payload); a client never prints a census line.
8. **Export.** Export JSON from the monitor; open it in the lab's Evidence tab (next round's "from the game" tab).

## 10. Invariants (the `/ecology` §2 restatement)

- **Continuity of existence** — untouched: the genome is applied at lineage bind, before the creature is established;
  nothing changes mid-life; a part-full newborn still blooms in.
- **No imposed death** — untouched: no clock, no lifespan; a faster upkeep is still starvation, which is still a feed
  away.
- **No domain asymmetry** — untouched: the genome has no domain.
- **Wither-to-crystal + mass conservation** — untouched: the sealed `Die` runs as before; the ledger only reads it.
- **Volume is the spine** — untouched.
- **Every lifeform drops one crystal; a colony is a population** — untouched; a worm split passes its pick verbatim.
- **Endogenous selection only** — *this is the feature*: there is no fitness function anywhere, and the ledger cannot
  steer. There is still **no lifeform level**: a gene is a lean, not an achievement, and nothing a creature does is
  written back into its genome.
- **The collider budget — zero delta.** No collider, no prism, no object. The cost is 16 bytes of genes per pick, a
  phenotype struct per fauna, and a ledger entry per living lifeform in a biome that is on.

## 11. Files and commands

| | |
|---|---|
| Core | `Assets/_Scripts/Controller/Environment/FloraAndFauna/Evolution/*.cs` |
| Seams | `LifeformVariantSpread.cs`, `CellConfigDataSO.cs`, `Cell.cs`, `Fauna.cs`, `Boid.cs`, `LightFauna.cs`, `CellLifeSpawnerBase.cs`, `Flora.cs`, `FaunaNetworkSync.cs`, `Ecology/FaunaStomach.cs` |
| Window | `Assets/_Scripts/Editor/Ecology/EvolutionMonitorWindow.cs` — FrogletTools ▸ Ecology ▸ Evolution Monitor |
| Tests | `Assets/_Scripts/Tests/Editor/LifeformGenomeTests.cs` |
| Harness | `bash Tools/Build/evolution_harness/run.sh [core,arena,evidence,golden]` (`EVO_SEEDS`, `EVO_HOURS`) |
| Lab | `python3 Tools/Evolution/build_lab.py [--check]` · `node Tools/Evolution/gate_parity.cjs` · `node .claude/skills/labmaker/verify_lab.cjs Tools/Evolution/DarwinLab.html` |
| Page | https://claude.ai/artifact/5axngABeaGGT2ev4ZR2ZaL |
