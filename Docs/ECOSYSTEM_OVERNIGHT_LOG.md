# Ecosystem — Overnight Work Log

Running autonomously while you're away (~8h). Bias: **safe, non-regressive** work
only — I can't validate in-editor, so I avoid changes that could leave the test
scenes broken, and I commit each coherent chunk. This log is newest-first so you
can skim what changed and why on return.

**Branch:** `claude/quirky-meitner-bcos8u`. Everything is pushed.

## ⬅️ Validate on return (the things only you can check in-editor)

1. **Tadpoles now eat (menu + skim race).** Last code session before the loop:
   foragers Consume (suction) any **unshielded** prism of **any** domain except
   fauna bodies — so the dominant trail gets grazed. Watch: do tadpoles visibly
   suck in trail prisms? Does Skim Race FPS recover at late laps now?
   - If the **race track gets eaten** → the track isn't shielded; tell me and I'll
     gate foragers differently (the current safeguard is "skip shielded prisms").
   - If the **menu flora gets stripped** → reduce `Blob Tadpole` `PopulationSize`
     (currently 25); flora regrow, so it should breathe, not go barren.
2. **Tadpoles have a health prism now** (Boid.Initialize initializes the body).
3. **No predators in either scene** (I removed sharks earlier — they ate
   everything at co-spawn). Spawn-immunity is now built (dormant) so a balanced
   predator can be re-added safely; I did NOT re-add one (didn't want to muddy
   your foraging test). Say the word and I'll add a low-count shark to the menu.
4. **Run the new edit-mode tests** (CellPhaseRules + ecology enums) — they're
   pure-logic regression guards and should pass.

**State of the two test scenes (unchanged by overnight work unless noted):**
- **Menu (Blob):** flora + tadpole forager + brittlestar. No shark (removed
  earlier — it ate everything at co-spawn).
- **Skim Race:** tadpole forager + brittlestar, no flora, no shark.

---

## Session 1 — spawn-immunity foundation (predator can return safely)

**Why:** the "only sharks" bug was predators eating co-spawned herbivores
instantly (all fauna spawn at the cell centre). To ever re-add a balanced
predator (the menu's vibrant-ecosystem goal), prey need a post-spawn grace
window. Built the mechanism now; it's **dormant** (no predators are wired into
either test scene), so current behavior is unchanged.

**Changes (`Fauna.cs`, `LightFauna.cs`):**
- `Fauna.predationImmunitySeconds` (default 6s) + `_spawnTime` stamped in a new
  `Awake` (runs during `Instantiate`, before any predator's first tick) +
  `IsPredationImmune`.
- `Fauna.Predated` now returns `bool` and refuses while `IsPredationImmune`
  (or already eaten). Predators feed (`NotifyFed`) only on a real kill.
- `LightFauna` predator branch updated to `if (… && prey.Predated(...)) NotifyFed()`.

**Not done (deliberately deferred):** actually re-adding a shark to the menu —
that's a behavior change I want to make only after the immunity is proven, so I'm
leaving the scenes predator-free for now.

---

## Session 2 — self-review of Boid.cs (found + fixed a real bug)

Reviewed the most-churned file holistically. **Bug found:** the any-domain
forager change made tadpoles eat *other fauna's body prisms* — brittlestar/shark
bodies are `HealthPrism`s but not `Boid`s, so they reached the prism-eat branch
and a tadpole would Consume them (herbivores eating fauna, which should be the
predator's job). **Fix:** the forager edible check now also excludes any prism
under a `Fauna` (`GetComponentInParent<Fauna>()`), so foragers eat only
trail/flora mass — never shielded structure (track) and never fauna bodies.

**Noted (not changed — consistent with LightFauna, minor):** initializing the
tadpole body prism (so it has a real health prism, per your note) also registers
it in the cell density grid, so fauna bodies count toward `LiveBlockCount`/phase.
LightFauna already does this for brittlestar/shark bodies. A future cleanup could
make `Prism.RegisterWithCell` skip fauna-owned bodies, but that touches LightFauna
too, so I left it.

---

## Session 3 — corrected the "IntensityWiseLifeSpawner is dead" docs

Checked spawner selection across all scenes. `IntensityWiseLifeSpawner` is **NOT
dead** — `MinigameWildlifeBlitz`, `MinigameWildlifeBlitzMultuplayerCoOp`, and
`MinigameMaelstromMultuplayer` select it (`cellTypeChoiceOptions: 1`). The docs
(ECOSYSTEM.md §1 root-causes, §8, §9, §10; the kickoff brief) claimed it was dead
and recommended deletion — that would have broken those scenes. Corrected all five
places to say it's live (used by WildlifeBlitz + Maelstrom), do not delete, and
note it has diverged from `RandomLifeSpawner` (no `FaunaFoodFloor` gate, 1/tick).
Doc-only; no code change.

---

## Session 4 — reviewed LightFauna predator/herbivore paths (clean)

Read LightFauna's consume logic end-to-end. Correct: herbivores eat opposing
**flora** (HealthPrism+LifeForm branch) AND opposing **trails** (plain-Prism
branch); the predator branch matches the Fauna base (eats any herbivore species),
respects post-spawn immunity, and feeds only on a real kill. No bug, no change.
Observation: brittlestars are still opposing-domain only (don't graze the dominant
trail) — only the tadpole does that now; acceptable since the tadpole is the
primary cleaner.

---

## Session 5 — edit-mode tests for CellPhaseRules (phase spine)

Added `Assets/_Scripts/Tests/EditMode/CellPhaseRulesTests.cs` (20 tests, guarded
`#if UNITY_EDITOR`, NUnit). Covers the pure phase-resolution function that drives
the whole ecology: climbing (keys off Enter), descending (keys off Exit, strict
`<`), **hysteresis band stability** (no chatter across an oscillating count),
multi-step spike/crash transitions resolving in one call, boundary cases, and the
Default threshold-table invariants (Enter>Exit per phase, monotonic Enters,
`IsAllZero` legacy-substitution guard). All expectations hand-traced against
`CellPhaseRules.Compute`. Safe (test-only).

---

## Session 6 — ecology enum drift guards

Added `EcologyEnumIntegrityTests.cs` (UNITY_EDITOR-guarded): locks the integer
values of `FaunaDiet` (Herbivore 0 / Predator 1 — now serialized on the fauna
prefabs as `diet:`), `CellPhase` (None 0 … Rabid 6), and `CellAggressionLevel`
(0/1/2), plus monotonic-ordering and uniqueness checks. Prevents a silent
reorder from corrupting prefab/asset fields. Test-only.

---

## Session 7 — ECOSYSTEM.md accuracy pass + loop wound down

The forager behavior changed several times (opposing-domain → any-domain →
any-domain-unshielded-non-fauna via Consume/suction), and sharks were removed, so
the bible had stale statements. Fixed §7 (herbivore description = the tadpole now
Consumes any unshielded non-fauna prism; brittlestar = opposing), §7 intro (only
the two herbivores are wired in; shark built-but-unwired), and §7.2-A (menu has no
shark; spawn-immunity is built, not a to-do).

**Loop wound down here.** I've completed the safe, high-value backlog (spawn-
immunity foundation, a real bug fix, the IntensityWise doc correction, code
review, two test files, validation checklist, doc accuracy). What remains is
either (a) behavior changes that need your in-editor validation (re-add a balanced
predator, per-scene forager diet, voracity/count tuning) — which I won't do blind
while you're away — or (b) low-value churn. Per the plan, I stopped rather than
manufacture risky changes to fill hours. The heartbeat monitor timed out on its
own; not re-armed.

**To resume the food web on your return:** the spawn-immunity makes re-adding a
predator safe — add the `Blob Shark` config back to the Blob profile (low
`PopulationSize`). Tell me your read on the foraging (does the tadpole graze
visibly? does Skim Race FPS recover?) and I'll tune from there.

---

## Backlog (planned for later sessions, safest first)
1. Doc consolidation — `ECOSYSTEM.md` has drifted across many edits; make it match
   the current code (cell-config spawn is live; scene-placed `*Population` via
   `fauna2` is dead; forager = any-domain-unshielded Consume; etc.).
2. Reconcile `IntensityWiseLifeSpawner` — likely dead now that SkimRace uses Random;
   document clearly (no blind deletion).
3. Edit-mode tests for testable logic (`FaunaDiet`, predation immunity timing,
   `GameDataSO` team counts) where they don't need Unity runtime.
4. Self-review pass over all fauna changes for latent bugs.
5. (Behavior, higher-risk — may leave for you) re-add a low-count shark to the
   *menu* with immunity, to restore the predator/prey layer; per-scene forager
   diet so the menu isn't stripped by any-domain foragers.

---

## Session 8 — Skim Race volume indicator (concentric phase hexagons)

Brought the main-menu volume gauge (`DomainVolumeIndicator` +
`DomainVolumeHexGraphic`) into Skim Race in a new **concentric-phase-rings** mode,
per the request "make it make concentric hexagons so we can see when each phase is
triggered and by which domain reaching the mass threshold."

What changed:
- `Cell.ResolvedThresholds` — new public read-only accessor over the private
  `ResolveThresholds()`, so the indicator can map each phase boundary to a ring.
- `DomainVolumeHexGraphic.SetPhaseState(...)` + `DrawPhaseRings(...)` — a second
  layout alongside the existing per-domain radial bands. Draws one hexagon ring per
  phase boundary, spaced **evenly** (not by raw threshold) so all five read clearly
  despite Skim Race's bunched thresholds (Quiet 100 … Rabid 2000). A translucent
  live-mass fill is mapped **piecewise** through the thresholds onto the even ring
  radii, so the fill edge reaches ring *i* exactly when summed mass crosses phase
  *i* — the disc sweeping past a ring **is** that phase tripping. Crossed rings glow
  in the dominant domain's color; uncrossed stay faint. Centre hex + spawn-cycle
  ring tint with the dominant domain too, so "which domain" reads straight off color.
- `DomainVolumeIndicator` — `concentricPhaseMode` toggle (+ `SetConcentricPhaseMode`
  for the AddComponent path), `_massNow/_massTarget` lerp, and per-phase frac
  computation from `cell.ResolvedThresholds`.
- `SkimRaceHUD` — auto-attaches a concentric indicator (top-left, tunable
  `indicatorAnchoredPos`/`indicatorSize`) and hands it the injected `GameDataSO`.
  Lives in `GameCanvas-SkimRace.prefab`, which is in `MinigameSkimRace.unity`, so it
  appears in Skim Race with no scene edit. A pre-placed indicator can be wired into
  the prefab to override the auto-created one.

**Needs your in-editor validation** (procedural UI I can't render): position/size
of the gauge in the Skim Race HUD, ring thickness/alpha legibility, and that the
dominant-domain color reads correctly against the track background. Tune via the
`SkimRaceHUD` fields and the `DomainVolumeHexGraphic` "Concentric phase rings"
header.

Requests 1/2/4 from this batch (lower mass-vs-crystal threshold, 10× hunt speed,
spawn-on-mass-concentration) shipped earlier in ff1529b4.

---

## Session 9 — Volume gauge made a UNIVERSAL pause button (corrected)

Session 8's Skim-Race-only corner widget was rejected ("scaled up inaccurate
remake"). The volume gauge is meant to be ONE recognizable element that trains
players as the in-game pause button across every gameplay scene — not a bespoke
per-scene re-implementation. Reworked accordingly (user-confirmed look + rollout):

**Look (one universal render, menu + all gameplay):** keep the radial domain wedges
(Jade top / Ruby lower-left / Gold lower-right, filling inward; centre = frenzy),
and overlay **concentric threshold rings** the wedges pass through. Each ring sits
at the radius a wedge reaches when its mass equals a cell phase threshold
(Quiet/Settled/Restless/Frozen/Rabid), so a wedge filling past a ring = that domain
pushing the cell into the next aggression zone; the crossed ring brightens. Folded
into the single radial path in `DomainVolumeHexGraphic` — the standalone concentric
mode from session 8 was removed. `SetState` now also takes the threshold fractions.

**Rollout:** base `MiniGameHUD.EnsureVolumeIndicator()` attaches the SAME
`DomainVolumeIndicator` to each scene's existing "Volume / Pause Button" (auto-found
by name under the HUD canvas, or wire `volumePauseButton`/`volumeIndicator` per
GameCanvas). Every gameplay HUD inherits it; the button keeps its authored onClick
(open PauseMenu) — the gauge only replaces the face. Mirrors what
`MenuMiniGameHUD.EnsureDomainVolumeIndicator()` already does for the menu, so the
gauge is now identical and present everywhere. `SkimRaceHUD` reverted to its minimal
original (no bespoke widget).

`DomainVolumeIndicator` now always computes the threshold fractions from
`cell.ResolvedThresholds` and feeds them to the graphic — no per-scene mode flag.

**Needs in-editor validation** (procedural UI): ring thickness/alpha legibility over
the colored wedges, and whether per-wedge ring SEGMENTS (coloring only the sectors a
domain has crossed) read better than the current full concentric rings + colored
wedge showing through. Tune via `DomainVolumeHexGraphic` "Phase threshold rings".

---

## Session 10 — Removed the biggest growth-side cheat: steady flora growth until Frenzy; phase ladder collapsed 6 → 3

**Directive (operator):** "continuing to remove the biggest cheats. The first cheat
… will also simplify our number of phases. Let's keep a steady growth and planting
rate until frenzy. Let our fauna do their jobs."

**The cheat removed.** Flora used to *self-limit* via two staggered phase gates —
planting stopped at `Settled`, growth stopped at `Frozen` — a hard-coded throttle
that capped the canopy well before the cell was full, faking the homeostasis the
food web is supposed to produce. Now **flora plant AND grow at a steady rate until
Frenzy** (`Cell.FloraGrowingEnabled = FloraPlantingEnabled = phase < Frenzy`). The
only down-force on flora is the food web (opposing-domain fauna grazing) or a vessel
ability. A cell with no active force on it climbs to Frenzy and stays there — a valid
equilibrium (§0), not a defect.

**Phases collapsed 6 → 3.** With flora no longer staggered on their own rungs, the
extra phases existed only to stage flora-vs-fauna events that no longer differ.
`CellPhase` is now `None / Calm / Restless / Frenzy`, mapping **1:1 onto the three
fauna aggression bands** (Calm→L0, Restless→L1, Frenzy→L2) — the phase *is* the
aggression band. `CellPhaseThresholds` dropped from 5 enter/exit pairs to **2**
(`RestlessEnter/Exit`, `FrenzyEnter/Exit`). A new biome now authors two numbers, not
five — and the HUD draws one intermediate ring (Restless) instead of five (directly
resolving the session-9 ring-legibility worry).

**Behavior preserved, density up.** The per-biome aggression boundaries are unchanged
in value (Blob `RestlessEnter 3000 / FrenzyEnter 5400`; Skim Race `600 / 2000`;
Default `8000 / 15000` = the old Restless/Rabid enters), so **fauna aggression
behavior is identical** — only the redundant middle rungs were dropped. The single
real behavior change: **flora fill denser** (they grow to `FrenzyEnter` instead of
stopping at the old mid-range growth cap).

**Files.** Enum `CellPhase`; `CellPhaseThresholds` (struct + `CellPhaseRules.Order`);
`Cell` (gates, `AggressionLevel`, `FrenzyEnterThreshold`, inits); `LightFauna`
(goal switch, danger-immune, drop-avoidance); `LightFaunaManager` (Quiet gates →
`FaunaSpawningEnabled`); `AssembledFlora`/`BranchingFlora`/`RandomLifeSpawner`
(comments); `DomainVolumeIndicator`/`DomainVolumeHexGraphic` (one ring); `CellNetworkSync`/
`CellRuntimeDataSO` inits; the density-partition sim runner + its editor;
`Blob Cell Config.asset` + `Skim Race Cell Config.asset` (threshold blocks rewritten,
same values). Tests `CellPhaseRulesTests` + `EcologyEnumIntegrityTests` rewritten for
the 3-phase model. Docs: ECOSYSTEM.md §0/§1/§2/§3/§4/§5/§5.1/§9/§10, both kickoff docs.

**Serialization-safe.** Verified no on-disk asset serializes a raw `CellPhase`
integer (only the threshold struct's *named* int fields, in two configs — both
rewritten), so collapsing/renumbering the enum can't drift any scene/prefab/SOAP ref.

### ⬅️ Validate on return (Session 10)
1. **Menu (Blob):** flora should fill noticeably denser now (grows to ~5400, not the
   old ~4200 plateau) and only freeze at frenzy. The tadpole/brittlestar food web
   should graze it and let it breathe; if it sits frozen, that's a *valid* state —
   tune the food web (forager `PopulationSize`, `starvationSeconds`) or lower
   `Blob Cell Config FrenzyEnter`, **never** add decay.
2. **Perf at the new density.** Steady-until-frenzy raises the steady-state prism
   count in every biome. If a target device dips, lower that biome's `FrenzyEnter`
   (one asset field). WildlifeBlitz now grows to 15000 (Default) vs the old ~10000 —
   watch it specifically.
3. **HUD ring.** The gauge now draws a single intermediate ring (Restless); confirm
   it reads cleanly and the wedge-crossing still communicates "entering the hunting
   band."
4. **Run the edit-mode tests** (`CellPhaseRulesTests`, `EcologyEnumIntegrityTests`) —
   rewritten for the 3-phase Default table (Restless 8000/7500, Frenzy 15000/14000).

---

## Session 11 — Fauna REPRODUCTION lands; spawner demoted to seeder; shark re-added; non-alloc perf

**Directive (operator):** "really go hard exploring how far you can take this to
become vibrant performant life." This session retires the LAST scaffolding cheat
and stands up the full 3-tier Lotka–Volterra web.

### 1. Reproduction — the population driver (ECOSYSTEM.md §6.1)
Feeds convert to births. Every `NotifyFed()` (prism consume; a kill for predators)
advances a per-individual counter; at `FeedsPerOffspring` feeds the fauna births
`OffspringPerBirth` offspring next to itself, gated by a per-individual
`ReproductionCooldownSeconds` and a hard per-cell, per-species `MaxLivePopulation`
cap (performance backstop, NOT the primary control — starvation is). All knobs on
`FaunaConfigurationSO`; `FeedsPerOffspring = 0` (default for un-authored assets,
incl. all WildlifeBlitz configs) = reproduction off. Offspring inherit domain +
lineage (`Fauna.AssignLineage` — host cell + species config) so they count in
`Cell.GetLiveFaunaCount` and can breed in turn. Spawn-immunity (session 1) covers
newborns automatically (stamped in `Awake`).

### 2. Spawner → SEEDER (the cheat retirement)
`RandomLifeSpawner`'s fauna loop now spawns only the *deficit* below the species'
seed floor (`PopulationSize`) each period — bootstrap + extinction recovery — and
stays out while the food web sustains the population (pure gating in
`FaunaReproductionRules.SeedSpawnCount`; prey-floor gate unchanged).
`IntensityWiseLifeSpawner` (WildlifeBlitz/Maelstrom) only gained lineage-binding
(counting + config-opt-in reproduction); its 1/tick cadence is unchanged.

### 3. Shark re-added to the Blob (menu) profile — full 3-tier web
flora → tadpole (floor 25 / cap 60 / births @10 feeds) + brittlestar (4 / 24 / @8)
→ shark (2 / 5 / births @3 kills, 30s cooldown). Skim Race stays predator-free on
purpose (predators remove the foragers that scene exists to test). Authored:
tadpole/brittlestar/shark configs in both biomes + `Blob Cell Spawn Profile`
SupportedFaunas.

### 4. Performance
- Both behavior ticks (`LightFauna.UpdateBehavior`, `Boid.CalculateBehavior`) now
  use `Physics.OverlapSphereNonAlloc` against a shared static 256-slot scratch on
  the `Fauna` base — the per-tick `Collider[]` allocation was pure GC churn at
  swarm scale (and reproduction makes swarms bigger).
- `MaxLivePopulation` is the per-species frame-budget ceiling; size it to perf,
  not to desired equilibrium.

### 5. Multi-cell correctness (latent bug fixed in passing)
`Fauna.cell` previously always read `cellData.Cell` — a SHARED runtime SO holding
only the LAST cell that initialized it (wrong cell in multi-cell scenes, e.g.
WildlifeBlitz's 4 cells). `Fauna.Initialize(cell)` now records the explicit host
cell and `cell` prefers it; the SO path remains the fallback for scene-placed
managers. `LightFauna`/`Boid` overrides call `base.Initialize`.

**Tests:** `FaunaReproductionRulesTests` (19 cases) pin `ShouldBirth` (feed
threshold, cooldown strictness, cap semantics incl. over-cap, 0 = disabled/uncapped)
and `SeedSpawnCount` (deficit, floor, cap clamp).

### ⬅️ Validate on return (Session 11)
1. **Menu:** the food web should now BREATHE — tadpole swarm grows while grazing
   (watch births: new tadpoles popping out of feeding ones), sharks pick off
   herbivores and multiply on kills, populations crash when prey runs out, the
   seeder re-seeds after a crash. If sharks dominate: raise their
   `FeedsPerOffspring` / lower `MaxLivePopulation` (5 now). If tadpole births feel
   spammy: raise `ReproductionCooldownSeconds` (6 now).
2. **Skim Race:** swarm should now keep itself sized to obstacle mass via births
   instead of the old +12-per-period drip. FPS at late laps is the metric.
3. **Perf:** watch fauna counts vs frame time; `MaxLivePopulation` (60/24/40/16/5)
   are first guesses — tune to budget.
4. **Run `FaunaReproductionRulesTests`** with the other edit-mode tests.

---

## Session 12 — Predators truly HUNT: prey-seeking via the fauna registry + diet-aware seeding

Resolves two documented v1 approximations in ECOSYSTEM.md §7 using the lineage
registry session 11 built:

1. **Real prey-seeking.** The per-species count registry is upgraded to track
   live `Fauna` INSTANCES (`Cell.LiveFauna` — the cell sensing its inhabitants,
   the fauna analogue of the prism density grid; sanctioned by §7's own "no
   central fauna registry exists yet" note, not a privileged shortcut). A
   predator's behavior tick now targets the **nearest live, non-immune
   herbivore**, falling back to the phase-based density goal when no prey exists
   (roam plausibly → starve). Skipping predation-immune newborns keeps sharks
   from camping fresh births.
2. **Diet-aware seeding.** A predator species now seeds on
   `GetLiveHerbivoreCount() >= FaunaFoodFloor` instead of the prism-mass proxy —
   no more churn of doomed sharks in a cell with mass but no herbivores.
   `FaunaFoodFloor` doubles as both floors (N prisms / N herbivores). Added the
   explicit `FaunaFoodFloor: 5` to the Blob profile (was relying on the
   deserialization default).

Registry hygiene: instances register in `AssignLineage`, unregister in
`OnDestroy`, cleared on cell reset/init; destroyed-but-pending fauna are skipped
by Unity-null checks in every scan. Manager-spawned fauna (no lineage) are
invisible to the registry — acceptable, those legacy populations never
instantiate (§7 dead `fauna2` note).

### ⬅️ Validate on return (Session 12)
1. **Menu:** sharks should now visibly CHASE tadpoles/brittlestars (not drift at
   mass centroids), and should not appear at all until ≥5 herbivores are alive.
2. If shark pursuit looks too lethal (herbivore population can't recover), the
   first levers are shark `MaxLivePopulation` (5) and `FeedsPerOffspring` (3);
   the herbivores' `predationImmunitySeconds` (6s, prefab/code default) is the
   newborn-survival lever.

---

## Session 13 — Menu perf (5 fps -> modeled ~77 fps) + a headless tuning LOOP

**Directive (operator):** "the main menu is dropping to a steady 5 fps; steady
state must be >60. Build a way to close the loop so you can run the game, observe
performance + population oscillations, and make changes WITHOUT a human in the loop."

### The diagnosis (and my own regression)
The steady-growth-to-Frenzy change (session 10) raised the menu's prism ceiling to
`FrenzyEnter 5400`, and reproduction (session 11) pushed fauna to their caps (~90).
That is the 5 fps: built a headless cost model and it shows the fauna
`Physics.OverlapSphere` term (each creature querying a 50–70 m sphere into ~5400
prism colliders, a few times/sec, ×90 fauna) ate ~70 % of the frame budget, and the
5400 prism GameObjects set a hard per-frame ceiling on top.

### The loop (the real deliverable) — `Tools/ecosim/`
No Unity / C# in the container, so I built a **dependency-free Python simulator**:
- `ecosim.py` reads the REAL Blob config assets, models the heavy steady state
  (prisms pin at Frenzy, fauna at caps), and estimates FPS from a physically-grounded
  cost model (`fps = (1000 − overlap_ms/s) / frame_fixed_ms`) calibrated to one real
  anchor (5400,90 → 5 fps). Prints per-lever sensitivity + named candidate configs.
- `calibration.csv` holds real `(prisms,fauna,fps)` samples; the first is the anchor.
- `EcosystemPerfProbe.cs` (in-Unity, read-only, never ships unless added) logs
  `[ECOSIM] prisms=… fauna=… fps=…` from the live `Cell` registry.
- The loop: edit config -> `python3 Tools/ecosim/ecosim.py` -> read predicted fps ->
  human plays menu -> paste probe line into calibration.csv -> ecosim recalibrates.
  See `Tools/ecosim/README.md` and ECOSYSTEM.md §12.

It is a lever-ranker, not an oracle (single calibration point + documented priors:
`CLUSTERING`, `OVERLAP_SHARE`, `cell_radius_m`); each real sample tightens it.

### The menu fix (Blob assets only — zero behavior risk)
`FrenzyEnter 5400→1000`, `RestlessEnter 3000→600`; caps tadpole 60→24, brittlestar
24→10, shark 5→3 (seed floors 25→12 / 4→3 / 2→2). Model: ~1000 prisms, ~37 fauna ->
**~77 fps predicted** (was 5). The model puts the per-frame prism ceiling at ~80 fps
for 1000 prisms, so Frenzy is now a PERFORMANCE budget, not a density dial.

### ⬅️ Validate on return (Session 13) — and feed the loop
1. **Capture the real number.** Add `EcosystemPerfProbe` to a Menu_Main GameObject
   (or set the `ECOSIM_PROBE` define), play, read the `[ECOSIM]` line. Paste the
   steady-state sample into `Tools/ecosim/calibration.csv` (most-trusted first) and
   re-run ecosim — that calibrates the model to YOUR hardware.
2. If the real menu is **still <60**: the model says cut `FrenzyEnter` further (it is
   the dominant lever) — try 800 (model ~93 fps). If it's **well above 60**, add life
   back (raise caps / FrenzyEnter) until it settles ~70–75 with margin.
3. The other scenes (Skim Race, gameplay) run the same ecosystem; the same probe +
   ecosim levers apply. I can add Skim Race as a second biome in ecosim on request.

### Structural wins deferred (need a real before/after capture, can't do blind)
Shrinking the shared `detectionRadius` (cubic on overlap) and driving grazing from
the density grid instead of per-fauna OverlapSphere — the latter is the only way to
make the food web dense AND cheap. Flagged in §12.

---

## Session 14 — Taming, not devouring: gentle the menu food web so gyroids stay sizable

**Directive (operator, in-editor observation):** before these changes it was fun to
fly around sizable gyroids; now the fauna eat too much of the gyroid, so there isn't
enough to fly through. "We want the fauna taming the environment, not devouring it."

### Diagnosis
This is the predator–prey equilibrium landing in the wrong basin. With the perf-cut
caps (herbivore cap 34: tadpole 24 + brittlestar 10) the standing swarm could
out-graze flora growth, so the gyroids got stripped (boom/bust) instead of holding
sizable. The reproduction layer made it worse — abundant gyroid → foragers feed →
breed → graze harder → strip it.

### Fix — the caps are the TAMING DIAL (menu/Blob only; Skim Race unchanged)
Keep the *summed herbivore cap below the flora's food-supported count* so the fauna
**cannot** out-graze flora; the gyroids then grow to `FrenzyEnter` and HOLD there,
fauna trimming the edges:
- tadpole (the voracious any-domain forager): floor 12→4, cap 24→6, slower births
  (FeedsPerOffspring 10→20).
- brittlestar: cap 10→5, births @8→16.
- shark: floor 2→1, cap 3→2.
- `FrenzyEnter 1000→1200` (gyroids a touch bigger — affordable now that fauna are
  fewer), tighter Frenzy band `FrenzyExit 700→950` so they stay near full.

Skim Race keeps its voracious foragers on purpose — there the goal is to DEVOUR the
AI trail-obstacle buildup. Same forager species, opposite role, set purely by the
per-biome cap. (No diet/behavior change — not a cheat; mass still conserved, food
web still the only down-force. ECOSYSTEM.md §6.2.)

### ecosim now models this
Added a **gyroid outcome** report (`TAMED` vs `DEVOURED`) from
`herbivore_cap` vs `food_supported = flora_growth/graze_rate`. The gentled config
reads **TAMED, gyroids hold ~950–1200**, perf **~70 fps**. The old caps (34) read
DEVOURED — matching what you saw. `FLORA_GROWTH_PER_S`/`GRAZE_PER_HERBIVORE_S` are
model assumptions (ratio calibrated so old=devoured/new=tamed); refine against real
probe gyroid observations.

### ⬅️ Validate on return (Session 14)
1. **Fly the menu.** Gyroids should now stay sizable and stable (held near Frenzy),
   with a small darting fauna presence trimming — not stripping — them.
2. If still over-grazed: cut herbivore caps further (tadpole is the main eater) — or
   tell me the gyroid prism count from `EcosystemPerfProbe` and I'll recalibrate the
   `flora_growth/graze` ratio so ecosim predicts your hardware/flora exactly.
3. If now too sparse/lifeless: raise tadpole `MaxLivePopulation` a few at a time
   (it's 6) until the swarm reads full again without stripping the gyroids.
4. Gyroid SIZE is perf-capped (~1200 prisms ≈ 70 fps); bigger needs the structural
   overlap/prism-cost fix (§12), not just a higher `FrenzyEnter`.
