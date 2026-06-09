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
`MinigameTournamentMultuplayer` select it (`cellTypeChoiceOptions: 1`). The docs
(ECOSYSTEM.md §1 root-causes, §8, §9, §10; the kickoff brief) claimed it was dead
and recommended deletion — that would have broken those scenes. Corrected all five
places to say it's live (used by WildlifeBlitz + Tournament), do not delete, and
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
2. Reconcile `IntensityWiseLifeSpawner` — likely dead now that HexRace uses Random;
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
- `HexRaceHUD` — auto-attaches a concentric indicator (top-left, tunable
  `indicatorAnchoredPos`/`indicatorSize`) and hands it the injected `GameDataSO`.
  Lives in `GameCanvas-HexRace.prefab`, which is in `MinigameHexRace.unity`, so it
  appears in Skim Race with no scene edit. A pre-placed indicator can be wired into
  the prefab to override the auto-created one.

**Needs your in-editor validation** (procedural UI I can't render): position/size
of the gauge in the Skim Race HUD, ring thickness/alpha legibility, and that the
dominant-domain color reads correctly against the track background. Tune via the
`HexRaceHUD` fields and the `DomainVolumeHexGraphic` "Concentric phase rings"
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
gauge is now identical and present everywhere. `HexRaceHUD` reverted to its minimal
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
