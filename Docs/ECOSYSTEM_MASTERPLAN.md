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

Mechanics solid: the **volume spine** (`Cell.LiveVolume` → phase, hysteresis), **flora**
plant/grow gates, **fauna** controlling-color spawn + prey-linked **starvation → wither-to-
crystal**, **consumption** as the cell's down-force, **crystals → elemental powerups**, and the
**lifeform-crystal invariant** (`LifeFormCrystal`: every lifeform drops one elemental crystal).

Open: roadmap steps 2–7 unstarted (seams ready); ecology wired into only ~2–3 of ~11 modes; no
collider budget; no heredity/reproduction/evolution. **The architecture is ready; the work is
the food web, the genome, the budget, and the wiring.**

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
| Heredity (a "program"/genome) | traits passed to offspring | a small **trait genome** per lifeform, inherited | ❌ → **P3** |
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
The Burst `BlockDensityGrid` (per-domain spatial grid) and `PrismAOERegistry` already answer
spatial queries **without colliders** — so the budget is reachable. The contract:

- **`ColliderBudget` (new):** a configurable per-cell ceiling on active prism colliders
  (default to be tuned, target ≤ ~1,500). Exposed in `CellConfigDataSO`.
- **Collider-LOD by phase:** once a cell reaches **Frozen**, prism colliders are *disabled*
  (growth has stopped; collisions are no longer gameplay-critical there). AOE + fauna queries
  run off the Burst grids/registry, which need no colliders. (~60–75% reduction.)
- **Fauna queries off the grid:** replace `LightFauna`'s per-tick `Physics.OverlapSphere`
  with a `BlockDensityGrid` neighborhood query (saves 2–5 ms/frame *and* drops collider reliance).
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

---

## 6. The roadmap (each phase: emergence · collider cost · tunability · gameplay · life-criterion)

**Phase 1 — Foundations & budget** *(prereq for everything; mostly mechanical)*
- Lock invariants (`CLAUDE.md`), this plan, the `ecology` skill. ✅ *(this turn)*
- Build the **collider budget + LOD + grid-fauna-queries + telemetry** (§4). *Life: enables scale.*
- **`LifeformBootstrap`** + wire ecology into the ~9 bare modes; intensity→biome selection.

**Phase 2 — The food web (Lotka–Volterra)** *(roadmap step 2)*
- `FaunaConfigurationSO.SubType` (Herbivore/Predator); **diet = "what counts as prey"** in
  `Fauna.ResolveGoal`/consume; **two-tier starvation** (herbivores↔flora, predators↔herbivores).
- *Emergence:* genuine population oscillation. *Life:* metabolism + homeostasis. *Collider:* neutral
  (reuses consume path). *Gameplay:* predators players can bait; prey blooms to harvest.

**Phase 3 — Reproduction + the genome** *(roadmap step 3 + the evolution substrate)*
- Well-fed lifeforms **reproduce**; the spawner becomes a one-time **seeder**.
- A small **heritable trait genome** (e.g. speed, size, consume-radius, starvation-tolerance,
  diet-bias, reproduction-threshold, element); offspring inherit **with mutation**.
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

## 10. P1 execution detail — RESUME HERE

**Decided: P1 first** (the collider budget gate, before scaling life to every mode).

**The collider-LOD dependency (key insight — don't lose this):** you must NOT blanket-disable
prism colliders at Frozen. Prism colliders are needed by (a) the **player** colliding with the
canopy and (b) **fauna** finding prey via `Physics.OverlapSphere`. So the safe staged order is:

1. **Telemetry (safe, do first).** Add `CellConfigDataSO.ColliderBudget` (int) + an in-editor
   overlay showing per cell: phase, prism count (≈ active colliders today), `LiveVolume`, vs
   budget (red when over). Reads existing `Cell.LiveBlockCount` / `LiveVolume` / `Phase`. Makes
   the budget **observable** — the perf-regression guard. Lowest risk; ship + validate first.
2. **Fauna → density-grid queries.** Replace `LightFauna.UpdateBehavior`'s `Physics.OverlapSphere`
   (~LightFauna.cs:268) with a Burst `BlockDensityGrid` neighborhood query (new API, e.g.
   `GetPrismsInRadius`). Removes fauna's collider dependency + saves ~2–5 ms. **Prerequisite for LOD.**
3. **Proximity collider-LOD** (NOT a blanket phase-disable). Disable colliders on prisms far from
   any vessel; re-enable on approach → active colliders bounded to "prisms near a vessel"
   regardless of total prism count. Track the precise active count → feed the telemetry. Touch:
   `Prism.cs` collider toggles (~247/305/452/OnDisable) gated on a coarse vessel-proximity check.
4. **Per-cell active-prism budget.** Recycle oldest **trail** prisms when over budget — **never
   cull the flora canopy** (territorial permanence + mass conservation). Generalize the menu trail cap.
5. **`LifeformBootstrap`** prefab (Cell + CellConfig + budget + telemetry) → wire the ~9 bare modes.

**P1 correctness invariants:** player-prism collision must survive; fauna must still find prey
(via the grid); never cull flora mass to meet the budget (recycle trails only).

**To resume:** invoke the `/ecology` skill, read this §10, continue from the first unchecked
slice. All prior work is committed on `claude/epic-darwin-g0Ehi`.
