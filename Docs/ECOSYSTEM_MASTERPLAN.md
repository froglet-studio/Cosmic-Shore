# Cosmic Shore — Living Ecosystem Master Plan

**North star:** vibrant living ecosystems that *emerge* from a small set of fundamental,
tunable, variable mechanics — performant within a hard collider budget, woven into the fun
of every game mode, and credible as **artificial life** by NASA's bar (a self-sustaining
system capable of Darwinian evolution), reached incrementally.

This is the strategic plan. The mechanics log lives in `Docs/ECOSYSTEM.md`; the locked
invariants live in `CLAUDE.md ▸ Ecosystem Design Principles`. This doc is the *why* and the
*sequence*.

---

## 1. The five pillars (every feature must serve all five)

> **Platform law (above the pillars): continuity of existence.** Nothing in Cosmic Shore ever
> pops in or out — every entity (prisms, crystals, flora, fauna, vessels, projectiles, UI) must
> grow / bloom / fade / suction / wither into and out of existence over a visible transition. A
> bare `Instantiate`-then-show or `Destroy` of anything the player can see is a bug. This is the
> *why* behind wither-to-crystal and mass conservation, applied game-wide.

1. **Fundamental & emergent.** Behaviour arises from composing the fundamentals (Domain,
   Mass/prisms, Cells, Elementals, Flora & Fauna, Vessels) — never hard-coded outcomes.
2. **Tunable & variable.** Every knob is SO-config; richness comes from *biome × intensity ×
   heritable traits*, not bespoke code per case.
3. **Performant — collider-bounded.** Active physics colliders per cell stay under a
   configurable threshold, maintained *by design* (§4). This is a hard gate, not an afterthought.
4. **Serves core gameplay.** Lifeforms feed the loop players already love (crystals as
   powerups, territory to contest, prey to hunt, canopy to cultivate) — not background dressing.
5. **Toward artificial life.** Each phase advances a concrete life-criterion (§3) so "NASA
   would call this alive" becomes a checklist we close, ending in open-ended evolution.

---

## 2. Where we are (grounded, June 2026)

Mechanics solid (foundation adopted from `bleeding-edge`, §10): the **`PrismSpatialIndex`**
collider-light spatial backbone, a two-tier **predator/herbivore Lotka–Volterra food web**,
**fauna reproduction** (feeds→births; the spawner is now a seeder), **Boid foragers**, the
sealed **mass-conserving crystal drop** on the fauna death path, **crystals → elemental
powerups**, and the **lifeform-crystal invariant** (`LifeFormCrystal` + validator: every
lifeform carries/drops one elemental crystal).

Open: all three LOCKED invariant re-assertions are now code-complete on the merge line —
wither-to-crystal (§10 item 1), no-domain-asymmetry spawn (item 2), and volume-as-the-spine
(item 3; volume thresholds auto-derive from legacy counts and still want a per-biome in-editor
retune). Remaining before the genome: in-editor verification of the wither/crystal path +
authoring crystals on fauna prefabs (item 1 follow-up) + the volume retune, collider budget
(telemetry + collider-LOD), the **genome/heredity** (the evolution substrate), and wiring
ecology into the ~9 bare modes. **The food web is now real; the remaining work is the
verification/retune pass, the budget, the genome, and the wiring.**

---

## 3. The artificial-life scorecard (the NASA bar)

NASA's working definition: *"a self-sustaining chemical system capable of Darwinian
evolution."* The classic seven characteristics + Koshland's pillars give a checklist. We are
already ~5/7 of the way — the gap is **heredity + reproduction + evolution**.

NASA's **Ladder of Life Detection** (Neveu et al. 2018) ranks features by how diagnostic they
are — **Darwinian evolution → growth & reproduction → metabolism → …** — and demands evidence be
*survivable* (the signal persists to be observed) and *reliable* (distinguishable from an abiotic
baseline), under the rule *"life is the hypothesis of last resort."* Koshland's **PICERAS** pillars
(Program, Improvisation, Compartmentalization, Energy, Regeneration, Adaptability, Seclusion) map
cleanly to a sim. The closest prior art is **Polyworld** (Yaeger 1992): a genome-driven,
energy-constrained agent ecology where *survival is selection* — our north-star analog. (Sources §9.)

| Life criterion | Requires | We satisfy it via | Status |
|---|---|---|---|
| Organization / compartmentalization | bounded structured units | Cells (membrane), lifeform prism-bodies, domains | ✅ have it |
| Metabolism / energy | matter/energy flux | the **mass economy**: flora produce prism mass, fauna consume opposing mass, crystals = stored energy | ✅ have it (consumption = metabolism) |
| Growth | individuals enlarge | flora grow prisms; fauna body scale | ✅ have it |
| Response to stimuli | react to environment | aggression-by-phase, prey-seeking, starvation clocks | ✅ have it |
| Homeostasis / regulation | self-bounding populations | prey-linked starvation + phase gates; full **Lotka–Volterra** after the predator/herbivore split | 🟡 partial → **P2** |
| Reproduction | individuals replicate | well-fed fauna breed; spawner becomes a seeder | ❌ → **P3** |
| Heredity (a "program"/genome) | traits passed to offspring | a small **trait genome** per lifeform, inherited | 🟡 **first heritable trait shipped** — the spawn variant (its **element**, and that element's tuning) is rolled once and inherited by offspring through `AssignLineage` (`Docs/ECOSYSTEM.md §17`; the pick also carried a hatch level until §39 retired lifeform levels entirely); that inheritance channel is the seat for the trait genome → **P3** |
| Variation / mutation | offspring differ heritably | mutation on inheritance | ❌ → **P3/P4** |
| Selection | differential survival/reproduction | the energy economy (starvation/predation) **already selects** — becomes *natural* selection once traits are heritable | 🟡 substrate exists → **P4** |
| **Adaptation / EVOLUTION (the bar)** | open-ended Darwinian evolution → novelty | reproduction + genome + mutation + selection; then speciation / predator-prey arms races | ❌ → **P4 (centerpiece)** |

**The minimal credible claim** — ship these four, wired together, and the system is defensibly
capable of *Darwinian evolution* (artificial life, minus the literal word *chemical*):
1. **Reproducing individuals with a genome that causally drives phenotype** — traits → real
   behaviour/stats, not a decorative tag.
2. **Heritable mutation** on reproduction, with **smooth, non-lethal mutational pathways**
   (small genome change → small phenotype change; Ray's anti-brittleness lesson).
3. **An energy/mass economy where survival & reproduction cost mass and failure = death**, so
   selection is **endogenous, never a scripted fitness function** — survival *is* fitness
   (Avida/Tierra/Polyworld). We already have this in starvation + predation.
4. **Evidence**: lineage tracking + trait-distribution-over-time, plus a **mutation-off /
   selection-off control run that shows no adaptation** — the "abiotic baseline." This is the
   cheapest high-credibility artifact and what turns "it looks alive" into "here is proof."

**The crux is endogenous selection — and it is the same thing as the team's law.** A
designer-scored fitness function is optimization, not life; our *"don't cheat emergence, never
hard-code an outcome"* rule forbids exactly that. And **wither-to-crystal already conserves
mass** (fauna return mass as collectible crystals instead of vanishing) → a closed,
self-sustaining economy — the "self-sustaining" half of NASA's definition. **The design
philosophy and the life criteria are one and the same.**

We already satisfy organization, metabolism, growth, response, and homeostasis. The gap to a
defensible claim is **Phases 3–4** (genome + reproduction + mutation + evidence). **Hedge
honestly on open-endedness:** claim *ongoing/bounded* novelty (emergent arms races, parasitism,
territorial speciation we did not hard-code), not unbounded rising-complexity OEE — a frontier
result even for dedicated ALife systems.

---

## 4. The performance contract — colliders below threshold

The hard constraint. Current reality: ~3–4k active `BoxCollider`s per full cell, uncapped.
**`PrismSpatialIndex`** (the adopted backbone — 16B-hot data, 8m-bucket
`NativeParallelMultiHashMap`; `QuerySphere`/`IsAnyPrismWithin`/`TryReserve`) and the per-domain
`BlockDensityGrid` already answer spatial queries **without colliders** — so the budget is
reachable; the remaining job is to route fauna behavior through them. The contract:

- **`ColliderBudget` (new):** a configurable per-cell ceiling on active prism colliders
  (default to be tuned, target ≤ ~1,500). Exposed in `CellConfigDataSO`.
- **Collider-LOD by phase:** once a cell reaches **Frozen**, prism colliders are *disabled*
  (growth has stopped; collisions are no longer gameplay-critical there). AOE + fauna queries
  run off the Burst grids/registry, which need no colliders. (~60–75% reduction.)
- **Fauna queries off the index:** replace `LightFauna`/`Boid`'s per-tick
  `Physics.OverlapSphereNonAlloc` with `PrismSpatialIndex.QuerySphere` (saves 2–5 ms/frame *and*
  drops collider reliance — prerequisite for collider-LOD).
- **Global active-prism budget:** recycle the oldest/farthest prism when the budget is
  exceeded (generalize the menu trail-cap). Crystals are already capped (16/cell wither).
- **Telemetry overlay (new):** an in-editor HUD showing active colliders / prism count / phase
  per cell, turning red over budget. Because changes are human-validated in-editor, this makes
  the budget **observable** — the regression guard the insights asked for.

**Rule:** no ecology feature ships without stating its collider-budget impact, and the budget
system lands (Phase 1) *before* ecology is scaled to every mode.

---

## 5. Lifeforms as a platform fundamental (in every game)

Make a Cell+ecology a *default* any scene gets, like a `ContainerScope`:

- A **`LifeformBootstrap` prefab** (Cell + CellConfig + the collider budget + telemetry) that
  drops into any game scene, wired through the existing `OnInitializeGame` SOAP path.
- **Per-mode biome configs** chosen by `intensity` (the kept `CellTypeChoiceOptions.IntensityWise`
  → CellConfig), so each mode/intensity gets a bespoke, tuned biome — the "variable complexity"
  axis. Intensity also scales spawn population / growth budget.
- Ecology must be **opt-out-able** per mode (a race may want sparse life) but **present by default**.

### 5.1 The Cell owns the environment — minigames must not build parallel systems (LOCKED best-practice)

When wiring ecology into a mode, the **Cell is the environment**. Everything atmospheric,
territorial, or boundary-defining is already a Cell responsibility, configured on the
`CellConfigDataSO` (and its `SpawnProfileSO`), not re-implemented per mode. This is the
"build systems once, use them everywhere" rule of **Universality** (`CLAUDE.md`) applied to
mode-wiring — a minigame that ships its own boundary/atmosphere/population system is the same
class of mistake as cheating emergence: a bespoke duplicate of a fundamental.

| Environment need | The Cell system that owns it | Do **not** build a mode-local… |
|---|---|---|
| Playfield boundary / "where the arena ends" read | `CellConfigDataSO.MembranePrefab` | wireframe edge cage, boundary box outline, perimeter shell |
| Drifting motes / atmosphere / speed-perception particles | `CellConfigDataSO.CytoplasmPrefab` (a `SnowChanger`) | plankton / dust / mote `ParticleSystem` |
| Core / centre marker, crystal anchor | `CellConfigDataSO.NucleusPrefab` | bespoke centre beacon |
| Population (which flora/fauna, how many, how often, prey floor) | `CellConfigDataSO.SpawnProfile` | per-mode spawner, hand-placed creatures, mode-local food gating |
| Phase / aggression / "excess of mass triggers a frenzy that grazes it down" | `CellConfigDataSO.PhaseThresholds` (volume spine; count backstop) | mode-local timers, cullers, or decay |

What a mode legitimately owns is only its **gameplay-bearing** structure — e.g. Astro League keeps
its physics walls (the ball must bounce), its goal portals, and its midfield ring, because those
*are* the soccer game, not environment dressing. Anything that would merely *re-show* a Cell
concept belongs on the Cell config.

**Tuning the Cell for a mode's prism profile (volume is the spine).** Phase thresholds are read in
VOLUME, and `CellPhaseThresholds.WithDerivedVolumeScale` derives missing volume fields from the
count fields × `NominalPrismVolume` (16, the 4×4×1 flora leaf). A mode whose vessel lays
**low-volume prisms** (e.g. Squirrel trail prisms ≈ **3.1** volume each, ~⅕ nominal) must author
**explicit `*EnterVolume` / `*ExitVolume`** values — otherwise the ×16 derivation sets the ladder
~5× too high, the volume gauge barely moves, and fauna never reach the hunting/frenzy phases. Set
`RestlessEnterVolume` low enough that fauna start grazing opposing trail **early**, `FrenzyEnterVolume`
at the "excess of mass" point where they graze *any* domain, and lower `SpawnProfileSO.FaunaFoodFloor`
(authored in nominal prisms, ×16 for the prey-volume check) so herbivores actually seed against the
mode's thinner prey. The `DomainVolumeIndicator` hex gauge reads the same `FrenzyEnterVolume`, so a
well-tuned ladder is also a readable gauge.

**A cell needs a crystal to start its spawner (the easy mode-wiring miss).** `Cell.cs` only calls
`StartSpawnerForMode()` from `InitilizePostFirstCellItem()`, which fires on the **first
`runtime.OnCellItemsUpdated` raise** — raised **only** when a crystal registers
(`CellRuntimeDataSO.AddCrystalToList` ← `LocalCrystalManager`/`NetworkCrystalManager`). A scene
that has a `Cell` + `SpawnProfile` but **no crystal manager** will track volume yet **never spawn
fauna** (Astro League and Joust both hit this). Every working fauna mode (WildlifeBlitz, HexRace,
Crystal Capture…) bootstraps via a crystal manager wired to the **same `CellRuntimeDataSO`** the
Cell uses. For a mode with no gameplay crystals, drop in a crystal manager configured for a single
neutral anchor crystal (`crystalCountMode = FixedCount`, `fixedCrystalCount = 1`,
`spawnCrystalWithPlayerDomain = false`) — `LocalCrystalManager` for a purely client-local cell, or
`NetworkCrystalManager` (slot-replicated; each peer still instantiates its own local crystal) when
you want the anchor power-up synced. It can ride the controller's existing `NetworkObject`.

---

## 6. The roadmap (each phase: emergence · collider cost · tunability · gameplay · life-criterion)

**Phase 1 — Foundations & budget** *(prereq for everything; mostly mechanical)*
- Lock invariants (`CLAUDE.md`), this plan, the `ecology` skill. ✅
- Adopt the `PrismSpatialIndex` spatial backbone (`QuerySphere`/`TryReserve`). ✅ *(merged from `bleeding-edge`)*
- Re-assert the three locked invariants on the foundation (§10): crystal-drop ✅, no-asymmetry
  spawn, volume spine. *Life: keeps mass conservation + the credible-alife economy intact.*
- Build the **collider budget + LOD + index-fauna-queries + telemetry** (§4). *Life: enables scale.*
- **`LifeformBootstrap`** + wire ecology into the ~9 bare modes; intensity→biome selection.

**Phase 2 — The food web (Lotka–Volterra)** ✅ *(adopted from `bleeding-edge`, §10)*
- `Fauna.diet` (Herbivore/Predator); **diet = "what counts as prey"** (herbivores eat opposing
  prism mass, predators hunt herbivore fauna via `Cell.LiveFauna`); **two-tier starvation** +
  post-spawn predation-immunity window. *Emergence:* genuine population oscillation. *Life:*
  metabolism + homeostasis. *Gameplay:* predators players can bait; prey blooms to harvest.

**Phase 3 — Reproduction + the genome** *(reproduction ✅ adopted; genome = the evolution substrate, TODO)*
- Well-fed lifeforms **reproduce** (`Fauna.NotifyFed`→`TryReproduce`; `FaunaReproductionRules`);
  the spawner is now a one-time **seeder**. ✅ *(adopted from `bleeding-edge`)*
- The **spawn variant is now heritable**: a cell spreads its species across its **element
  palette** — four elemental variations, which is the whole of what a lifeform varies by
  (`Docs/ECOSYSTEM.md §39`) — each spawn rolls one, and offspring inherit their parent's roll
  rather than re-rolling. That is the first trait riding the reproduction path and the seat the
  genome plugs into. ✅ (`Docs/ECOSYSTEM.md §17`)
- TODO: a small **heritable trait genome** (e.g. speed, size, consume-radius, starvation-tolerance,
  diet-bias, reproduction-threshold, element); offspring inherit **with mutation** — extend the
  inherited `LifeformVariantPick` channel rather than adding a second inheritance path.
- ~~TODO (follow-up from the spread): **phase-threshold retune for the new volume mix.**~~
  **CLOSED by `Docs/ECOSYSTEM.md` §39.** The level spread raised mean creature scale ~13% above
  what the thresholds were authored for; levels are retired, so the multiplier is exactly 1 and
  every ladder now reads what it was authored against. Nothing to retune, no collider change.
- TODO (follow-up from the spread): per-cell **element palettes**. Cells currently borrow the
  canonical `_SO_Assets/Lifeforms` per-element tuning; the score-tuned cells (Skim Race, Nucleus
  Rush, Astro League) therefore run element-only spread to protect their job-tuned swarms. Author
  cell-local palette assets if a biome wants both its own behavior tuning and full element identity.
- *Life:* reproduction + heredity + variation — the substrate for evolution. *Collider:* governed
  by the budget (reproduction can't exceed it). *Tunable:* mutation rate, trait ranges per biome.

**Phase 4 — Selection → open-ended evolution (the centerpiece)** *(NASA bar)*
- Selection emerges: the energy economy (starvation/predation/reproduction-threshold) acting on
  heritable traits = **natural selection**. Trait distributions drift and adapt.
- **Evolution telemetry**: trait histograms over time + data export — so we can *show* the
  system evolves. *Life:* Darwinian evolution. Later: speciation, predator-prey arms races
  (open-endedness) — the impressive-to-NASA frontier.

**Phase 5 — The living world** *(roadmap steps 5–7)*
- Domain **territory dynamics** (control ebbs/flows, no monoculture lock-in), **flora succession**
  (pioneers→canopy by phase), **cross-cell migration** (fauna chase prey between cells).
- *Emergence + variability:* the world visibly breathes and varies. *Collider:* migration must
  respect per-cell budgets.

**Phase 6 — Elemental integration & gameplay depth** *(roadmap step 4)*
- Flora/fauna express effects through **Elementals** (domain flora buff their vessels, fauna
  debuff opposing mass) so **vessels feel the ecosystem**. Make ecology a layer players
  cultivate/hunt/contest. *Gameplay:* the payoff that makes life part of the fun.

*(Phases 2–6 are largely parallelizable once Phase 1's budget exists; Phase 4 depends on Phase 3.)*

---

## 7. How we work (the orchestration)

- **`ecology` skill** *(created)* — every ecology change starts by restating the invariants
  (§ CLAUDE.md), confirms intent at design forks, implements surgically, then states its
  **collider-budget impact** + the exact in-editor verification. Kills the "wrong-model revert"
  and "perf regression" frictions at the source.
- **Exploration agents (parallel "batch")** before each phase — map the seam first (done for P1).
- **Parallel variant agents on worktrees** for tuning-sensitive mechanics (Lotka–Volterra params,
  genome shape, mutation rate): each agent implements a variant, we compare against the invariants
  + a sim metric, **pick the winner** instead of guessing serially.
- **`/loop`** — recurring autonomous guards: run the lifeform-crystal validator + a static
  collider-budget/anti-pattern audit on an interval; babysit a PR once opened.
- **Hooks** — a lightweight `PostToolUse` lint (flag re-introduced decay/lifespan, new
  `Physics.OverlapSphere` without a budget note, etc.) since full Unity perf can't run in-container;
  a `SessionStart` hook to run edit-mode tests where a Unity batchmode is available.
- **Human-in-the-loop is the gate.** I can't run Unity; the telemetry overlay + crisp
  validation steps + the skill's "state the knobs" rule make each change verifiable by you fast.

---

## 8. Risks / open questions

- **Budget vs. ubiquity tension:** lifeforms everywhere ↑ colliders; resolved by the budget +
  LOD landing *first* (Phase 1). Needs the threshold tuned on a target device.
- **Evolution legibility:** evolution that no one can *see* isn't fun or convincing — Phase 4's
  telemetry + visible trait expression (size/color/behaviour) is as important as the math.
- **Determinism / netcode:** heredity + RNG must stay server-authoritative + reproducible across
  clients (extend the existing `CellNetworkSync` discipline).
- **Scope creep into "fundamentals":** new mechanics must extend an existing fundamental
  (genome extends Flora/Fauna; budget extends Cells/Mass) before proposing a new one.

---

---

## 9. Sources (artificial-life grounding)

- NASA definition & Ladder of Life Detection: science.nasa.gov life-detection ·
  Neveu et al. 2018, *Astrobiology* (PMC6211372)
- Koshland "Seven Pillars of Life" (PICERAS, 2002) · classic 7 characteristics (MRS GREN)
- Digital evolution: Ray's **Tierra**, Adami/Ofria's **Avida** (alife.org / PMC7123229)
- Continuous CA: **Lenia** / Flow-Lenia (arXiv 1812.05433, 2212.07906)
- **Polyworld** (Yaeger 1992) — the game-ecology analog
- Open-ended evolution requirements: Taylor et al. 2016 (arXiv 1507.07403)

---

---

## 10. Execution — RESUME HERE (rebased onto bleeding-edge, June 2026)

**What changed (the ground-up reconsideration).** `epic-darwin` had drifted 148 commits
behind `bleeding-edge` and was independently reinventing the collider-light spatial backbone
the team already shipped (**`PrismSpatialIndex`** — 16B-hot / 8m-bucket index; `QuerySphere`
replaces `Physics.OverlapSphere`, `TryReserve` replaces `Physics.CheckBox`, drives the per-cell
density grids), plus a parallel food web. We stopped the divergence: **merged `bleeding-edge`
and adopted its superior foundation wholesale** (PrismSpatialIndex + predator/herbivore
Lotka–Volterra food web + fauna reproduction + Boid foragers + headless `EcosystemPerfProbe`).
So roadmap **Phases 2 (food web) and 3 (reproduction) are DONE** at the foundation level, and the
old §10 collider-budget plan is **superseded by PrismSpatialIndex** for the spatial-query half.

**But `bleeding-edge` drifted from three LOCKED invariants** (it had no `/ecology` skill or
locked CLAUDE.md section to hold the line). The remaining work is to **re-assert them as clean
deltas on top of the adopted foundation** — not to rebuild anything:

1. **Mass-conserving crystal drop + wither (continuity) on the fauna death path.** ✅ *(done)*
   `bleeding-edge` fauna `Die()` just `Destroy`ed — they **popped out of existence**, dropping no
   crystal (violates both the mass-conservation invariant *and* the platform-wide **continuity
   law**: nothing pops in/out). Fixed in two layers: (a) sealed the crystal drop into the `Fauna`
   base — non-virtual `Die` drops the elemental crystal then calls a `protected virtual OnDeath`
   hook, so no subclass can die without conserving mass; (b) restored the **extremity-first
   wither** — `LightFauna.OnDeath` collapses spindle rings farthest-from-centre first (a shark's
   fins / a brittlestar's arms evaporate before the core body — emergent from geometry, tunable
   via `LightFaunaDataSO.witherRingInterval`), `Boid.OnDeath` shrinks out; both then remove the
   spent husk. `LightFauna`/`Boid` provision the crystal in `Initialize` via
   `LifeFormCrystal.EnsureElementalCrystal`.
   *Follow-up:* author one elemental crystal on each fauna prefab + run **FrogletTools ▸ Validation ▸
   Validate Lifeform Crystals**, so `EnsureElementalCrystal` is a no-op fast path (budget-neutral).

2. **No domain asymmetry in spawning.** ✅ *(done — adopted onto the merge line)*
   `SpawnProfileSO.FloraExcludeLocalDomain` / `FaunaExcludeLocalDomain` defaulted `true`, and
   the spawners' `GetExcludedDomain` roll excluded the local domain from flora — the asymmetry
   the invariant forbids ("all three domains seed flora; fauna spawn in the controlling color
   only"). Re-asserted: the exclusion roll is gone from `RandomLifeSpawner` AND
   `IntensityWiseLifeSpawner` (flora pick uniformly over Jade/Ruby/Gold regardless of the
   serialized flag, so legacy SpawnProfile assets with the old `true` value are inert), and the
   flags default `false` and are marked deprecated. Fauna were already controlling-color only.

3. **Volume as the spine.** ✅ *(re-asserted on the merge line — retune still owed in-editor)*
   `Cell.LiveVolume` / `GetDomainVolume` now exist: per-domain live volume recomputed from
   live prism state (`Prism.CurrentVolume` × live `Domain`) on a 0.25s cadence, fed by EVERY
   prism — trail, flora, AND fauna bodies (volume-only cell binding via
   `PrismSpatialIndex.BindCell`; per the prompter, all prisms add to volume regardless of
   source). Phase (`CellPhaseRules.Compute(volume, count, …)`), `DominantDomain`, the prey
   signal (`OpposingVolume` — ENVIRONMENT volume only: fauna bodies aren't edible), and
   `DomainVolumeIndicator` all key off volume; the serialized COUNT thresholds remain as the
   Frenzy perf backstop exactly as the invariant allows. The 3-phase enum is kept. Legacy
   CellConfig assets auto-derive their volume ladder (count × `NominalPrismVolume` = 16, the
   4×4×1 leaf) via `CellPhaseThresholds.WithDerivedVolumeScale`, so no asset breaks — but the
   derivation assumes nominal prism volume; **retune each biome's volume thresholds in-editor**
   (watch the cell breathe Calm→Restless→Frenzy with real average prism volumes; the
   `RestlessEnterVolume`/`FrenzyEnterVolume` fields override the derivation once authored).

**Collider budget (PrismSpatialIndex backbone now present; remaining):**
- ✅ **Fauna queries ride the index** *(done on the merge line — SPATIAL_INDEX Phase 2)*:
  `Boid`'s scan is fully `PrismSpatialIndex.QuerySphere`; `LightFauna` takes prisms (incl.
  fauna bodies and predator prey detection) from the index and keeps one layer-masked
  physics overlap for **vessels only**. Fauna body prisms uphold the movers contract
  (`Fauna.NotifyBodyPrismsMoved` per frame) so index data stays honest. Prerequisite for
  collider-LOD is met. See `Docs/SPATIAL_INDEX.md`.
- ✅ **Proximity collider-LOD** *(shipped on the merge line — in-editor verification owed)*:
  `PrismColliderLodManager` culls prism colliders beyond `lodRadiusMeters` (200m default) of
  any focus and restores them as foci move; vessels and in-flight projectiles self-register as
  foci. Never blanket-disables (focus-less scenes idle), never fights the lifecycle
  (`Prism.SetColliderCulledByLod` snapshots/restores pre-cull state), never touches the
  unregistered Mound-layer blocks. Verify: fly toward a distant structure (collision works on
  arrival), shoot a distant structure (projectile connects — projectiles are foci), and watch
  `colliders=near/live` in the probe line stay bounded as a cell fills. Kill switch:
  `lodEnabled` on a scene-placed manager.
- ✅ **Telemetry** *(shipped)*: `EcosystemPerfProbe` line is now
  `[ECOSIM] prisms= volume= colliders=near/live fauna= phase= fps=` — the §4 budget and the
  volume spine observable in one line; use it for the per-biome threshold retune.
- ~~**Per-cell active-prism budget** — recycle oldest **trail** prisms~~ **Rejected upstream:**
  age-based trail recycling is the same passive-removal cheat as the reverted menu trail cap
  (`bleeding-edge` commit `44a1f264`; see CLAUDE.md ▸ "Universality — one HyperSea, one rule
  set" and `Docs/ECOSYSTEM.md` ▸ "Rejected cheat"). Idle accumulation is managed by the
  universal systems only: fauna cleanup (foragers) or pausing/throttling the spawner.

**Merged to `bleeding-edge` (June 12, 2026)** after the prompter's in-editor review. All three
locked invariant re-assertions, the spatial-index Phases 2–3, proximity collider-LOD, and the
budget telemetry now ride the mainline. Historical note for adopters: `claude/epic-darwin-g0Ehi`
remains the drift-sanity reference but predates the trail-cap revert (`44a1f264`) and the
universality lock (`be4afb00`) — its residual `Trail.RemoveOldest` / `maxTrailBlocks` / menu-cap
code was deliberately **not** adopted.

### What's next (the post-merge plan — work top to bottom)

**Phase A — land & tune (in-editor, near-term).**
1. **Crystal authoring sweep:** one **active** elemental crystal child on every fauna prefab,
   `cellData` wired (`ActivateCrystal` reads `cellData.Cell`; a missing wire is an NRE on first
   death), check the carried crystal's collider/component start state, then
   **FrogletTools ▸ Validation ▸ Validate Lifeform Crystals** until clean — makes
   `EnsureElementalCrystal` the no-op fast path (budget-neutral).
2. **Per-biome retune with the probe** (`[ECOSIM] prisms= volume= colliders=near/live fauna=
   phase= fps=`): volume thresholds per CellConfig (author `*Volume` fields to override the ×16
   derivation), LOD radius/tick if any collider consumer pops, flora growth/planting knobs if
   structures still read small, shark/brittlestar starvation balance (45s/30s first guess).
3. **Confirm the food-web loop end-to-end:** starvation AND predation wither extremities-first,
   never blink out, drop a collectible crystal that buffs the collector's element; sharks thin
   the brittlestar school then starve back (Lotka–Volterra oscillation visible).
3a. **`SegmentSpawner.SuperShieldSpawnedPrisms` still bypasses the state machine** — it pokes
   `prismProperties.IsSuperShielded` directly instead of calling `Prism.ActivateSuperShield()`
   (which would also route `PrismStateManager.SyncAOERegistryShieldState` → `UpdateShieldState`).
   It is correct *today* only because `SegmentSpawner.Initialize` lays the whole track
   synchronously, before any prism registers with `PrismSpatialIndex`, so the Register-time
   `ComputeEnvironmentMass` read already sees the flag. The moment `SpawnableWaypointTrack`
   opts into `layAcrossFrames`, prisms laid after the pass would silently miss the super-shield
   and land in the targeting grids as ordinary prey. Route it through `ActivateSuperShield`
   (needs an `instant` pass-through for `superShieldEngageInstant`) before that happens.
3b. **Skim Race has no herbivore spawn ring** (`Skim Race Spawn Profile.HerbivoreSpawnPointCount`
   is 0, so its herbivores still seed on the densest sensed mass). Blob runs 3 points at radius
   400; decide whether the racing cell wants the same spread now that the ring rides the wave
   clock (`Docs/ECOSYSTEM.md §16.1`).
3c. **No edit-mode test covers the wave→ring mapping.** `HerbivoreSpawnPoint` takes a `Cell`, so
   it is not reachable from the engine-free `FaunaReproductionRulesTests` surface. Extracting the
   angle math to a pure static would make "N points, N waves, no repeat" a unit test.

**Phase B — collider budget completion (perf).**
4. **`ColliderBudget` per cell** (CellConfigDataSO, target ≤ ~1,500 — §4): the probe warns when
   `colliders=near/live` approaches it; LOD radius auto-tightens or flags for retune. This turns
   the budget from a convention into an observable contract.
5. **Bucket-accelerated AOE** only if profiling demands it (SPATIAL_INDEX Phase 4 — profiling
   first, never assume).
6. **`UpdateDomain` known gap**, its own tested change: wire steals into the AOE cold data so a
   stolen prism's friend/foe in batch explosions matches its live domain.

**Phase C — genome & heredity (P3 — the artificial-life centerpiece, §3).**
7. **Trait genome:** a small heritable struct on creature fauna (multipliers over the species
   SO base values — e.g. speed, starvation clock, consume radius, reproduction cost), applied at
   Initialize so traits CAUSALLY drive phenotype. Inherited in `Fauna.SpawnOffspring` with
   bounded mutation (small genome change → small phenotype change). **Endogenous selection
   only** — survival/reproduction is the only fitness; no designer scoring (locked).
8. **Evidence pipeline:** lineage + trait-distribution telemetry (extend the ECOSIM line or a
   sibling log) and the **mutation-off control run** — the abiotic baseline that turns "looks
   alive" into "demonstrably adapts" (§3 item 4).

**Phase D — wire ecology into the ~9 bare modes (§5):** cells + spawn profiles per mode,
intensity → biome/population mapping, powerup crystals feeding each mode's loop. Variety =
biome × intensity × (Phase C) heritable traits — never bespoke per-mode code.
