# Ecosystem Fundamentals — Next-Session Kickoff

**Mission:** advance Cosmic Shore's cell ecology from "two wired test scenes" into
**one consistent, config-driven ecosystem fundamental that serves the whole
platform** — so that authoring a brand-new ecosystem (a new biome, a new food web,
a new set of creatures) is a matter of creating ScriptableObject config assets, not
writing bespoke per-scene code. The end state: countless future ecosystems, all
composed from the same small set of fundamentals.

This doc is the brief. The **paste-in prompt** for a fresh session is the first
section; everything after it is the supporting context that prompt refers to.

---

## ► PASTE THIS INTO A NEW SESSION

> You are continuing the Cosmic Shore **ecosystem fundamentals** work. The goal of
> this phase is to turn the cell ecology into **one consistent, fully data-driven
> system that serves the entire platform** — every gameplay scene and every future
> biome — so that creating a new ecosystem means authoring config assets, never
> writing scene-specific code.
>
> **Before writing any code, read in this order:**
> 1. `CLAUDE.md` → "Design Philosophy: Favor Emergent Systems Over Bespoke
>    Solutions" (the fundamentals list + curation process) and the "Domain / Mass /
>    Cells / Elementals / Prisms / Flora & Fauna / Vessels" definitions.
> 2. `Docs/ECOSYSTEM.md` → §0 (mass-conservation invariant), §5 (locked decisions),
>    §7 (the 3-species food web as it stands), §10 (roadmap — your work extends
>    Phase 2).
> 3. `Docs/ECOSYSTEM_OVERNIGHT_LOG.md` → sessions 1–9 for the running history and
>    the "needs in-editor validation" items.
> 4. This file (`Docs/ECOSYSTEM_PLATFORM_KICKOFF.md`) end to end.
>
> **Hold these invariants as non-negotiable (do not relitigate — see ECOSYSTEM.md §0
> and CLAUDE.md):**
> - **Mass is conserved.** Prisms are removed ONLY by active forces — a vessel using
>   an ability, or fauna eating them. No passive decay, aging, lifespan, or timed
>   culler, ever. A large accumulation is a *valid* equilibrium, not a bug.
> - **The food web is the only down-force.** When a cell over-accumulates, the
>   correction is opposing-domain fauna grazing it down — or, when no fauna can
>   reach edible prey, fauna *starving*. Population homeostasis is the food web's
>   job, never artificial decay.
> - **Mass-seeking is emergent**, read from the density grid
>   (`Cell.GetDensestRegionAnyDomain` / explosion-target queries). Fauna must NOT
>   follow the track, read planted positions, or use any privileged shortcut.
> - **Foragers must never eat the race track.** Track prisms are shielded
>   (`IsShielded`/`IsSuperShielded`); edibility checks must skip shielded prisms and
>   other fauna's body prisms.
> - **Elementals are the single buff/debuff system.** Any ecology→gameplay effect
>   must flow through Elementals (Charge/Mass/Space/Time), not a bespoke buff.
> - If you are ever tempted to hard-code an outcome instead of letting it emerge
>   from the fundamentals, STOP and ask first (CLAUDE.md "Don't cheat emergence").
>
> **Your north-star deliverable:** a new ecosystem can be stood up by authoring
> `CellConfigDataSO` + `SpawnProfileSO` + `FaunaConfigurationSO`/`FloraConfigurationSO`
> + `CellPhaseThresholds` assets and dropping a `Cell` into a scene — with zero new
> C# required. Drive toward that. The ordered work items are in the "Platformization
> work plan" section below; start at the top, ship each independently, and add
> edit-mode tests + an `ECOSYSTEM.md` doc entry for every capability you add.
>
> Development branch: ask the operator for the branch to develop on. The prior
> phase's work is on `bleeding-edge`.

---

## Where things stand (shipped to `bleeding-edge`)

**The fundamentals in play:** Domain, Mass (= prisms, conserved), Cells (`CellType`
+ phases), Elementals, Prisms/Prismscapes, Flora & Fauna, Vessels. See CLAUDE.md.

**Cell phase spine.** `Cell` derives a `CellPhase` (Calm → Restless → Frenzy — a
3-rung ladder; the phase *is* the fauna aggression band) from its live prism count
via `CellPhaseRules.Compute(count, current, thresholds)` with up/down hysteresis.
Thresholds live per-biome on `CellConfigDataSO.PhaseThresholds` (`CellPhaseThresholds`
— now just `RestlessEnter/Exit` + `FrenzyEnter/Exit`), falling back to
`CellPhaseThresholds.Default` for legacy zeroed assets. Phase drives the single flora
gate (grow + plant until Frenzy) and fauna aggression — **not** spawn rate.

**3-species food web (`FaunaDiet` = Herbivore | Predator).**
- **Tadpole** (`Boid`, `forager=true`, herbivore): emergent forager. Seeks the
  densest region the cell senses (`ResolveGoal` → `GetDensestRegionAnyDomain`),
  `Consume`s any unshielded non-fauna prism (suction + health-prism body), 10×
  hunt-speed dash toward its goal, spawns *on* the mass concentration it will graze,
  starvation-bounded. Large self-sustaining murmuration.
- **Brittlestar** (`LightFauna`, herbivore): eats opposing-domain prisms.
- **Shark** (`LightFauna`, `diet=Predator`): eats herbivore *fauna* (via
  `Fauna.Predated`), gated by a `predationImmunitySeconds` spawn-immunity window so
  co-spawned herbivores aren't instantly wiped. **Built but currently unwired** in
  the test spawn profiles — re-add at low `PopulationSize` once balanced.

**Emergent-seeking plumbing.** Fauna body prisms are excluded from the density grid
(`Prism.RegisterWithCell` skips `HealthPrism` under a `Fauna`) so a swarm doesn't
seek itself. `CellConfigDataSO.SenseRadiusOverride` decouples grid coverage from the
membrane's visual radius (Skim Race uses 3000 so fauna sense the whole long track).

**Retired scaffolding.** The flora **regrowth pulse**, the flora **phase-gated
self-limit**, AND the **fixed-period spawner as population driver** are all gone.
Flora plant + grow at a **steady rate until Frenzy**
(`Cell.FloraGrowingEnabled = FloraPlantingEnabled = phase < Frenzy`); the food web
is the only down-force (removing the staggered self-limit collapsed the phase
ladder 6→3). **Reproduction is the population driver** (work item 4 — LANDED):
feeds convert to births (`FeedsPerOffspring`/`OffspringPerBirth`/cooldown/cap on
`FaunaConfigurationSO`), and the spawner is demoted to a seeder that only tops a
species up to its seed floor (bootstrap + extinction recovery). See ECOSYSTEM.md
§6.1.

**Two test scenes wired** (`Docs/ECOSYSTEM.md` §7.2):
- *Menu_Main* freestyle toy box (Blob Cell) — vibrant, indefinitely watchable.
- *Skim Race* / `MinigameSkimRace` (Skim Race Cell) — fauna graze AI trail-obstacle
  buildup so framerate recovers at late laps / high player counts.

**Universal ecosystem HUD.** `DomainVolumeHexGraphic` + `DomainVolumeIndicator` are
the in-game **pause button face** across the menu and every gameplay scene
(auto-attached to each HUD's "Volume / Pause Button" by
`MiniGameHUD.EnsureVolumeIndicator` and `MenuMiniGameHUD`). It shows the three
domain wedges filling inward toward frenzy, with concentric phase-threshold rings
the wedges pass through (each ring disappears once crossed). It reads any cell's
live per-domain counts + `Cell.ResolvedThresholds` — already platform-generic.

**Tests:** `CellPhaseRulesTests` (hysteresis spine), `EcologyEnumIntegrityTests`
(enum-drift guards for `FaunaDiet`/`CellPhase`/`CellAggressionLevel`).

**Two spawners coexist:** `RandomLifeSpawner` (most scenes) and
`IntensityWiseLifeSpawner` (WildlifeBlitz + Maelstrom, `cellTypeChoiceOptions: 1`).
They have diverged. Do not delete either; reconcile them (work item 1).

---

## Platformization work plan (ordered; each ships independently)

The lens for this phase: **lift every per-scene assumption into config, and replace
every cheat with an emergent force.** Start at the top.

1. **Config-completeness audit → "new ecosystem = new assets, no code."**
   Sweep `Cell`, the spawners, `Fauna`/`LightFauna`/`Boid`, and the SO configs for
   anything a new biome would have to *code* rather than *author*: hardcoded radii,
   species assumptions, scene-name checks, `forager` as a prefab bool instead of a
   config field, etc. Lift them onto `CellConfigDataSO` / `SpawnProfileSO` /
   `FaunaConfigurationSO`. Deliverable: a documented checklist in `ECOSYSTEM.md` of
   exactly which assets define an ecosystem, and proof (a third config-only biome)
   that no code is needed.

2. **Reconcile the two spawners into one config-driven spawner.**
   Fold `IntensityWiseLifeSpawner`'s intensity behavior into `RandomLifeSpawner`
   (or a shared base) behind config flags, so all scenes run one spawner whose
   behavior is data-selected. Keep WildlifeBlitz/Maelstrom behavior intact; migrate
   them onto the unified path. Tests for both behaviors.

3. **Generalize "diet" into a composable prey-selector.**
   Today `FaunaDiet` is a two-value enum. Make "what counts as prey" a parameterized
   selector on `FaunaConfigurationSO` — by domain relationship (own/opposing/any),
   by prism provenance (flora vs vessel-spawned vs fauna-body), by target species —
   so arbitrary multi-tier food webs compose from config. This is the key that
   unlocks "countless ecosystems."

4. **Fauna reproduction → retire the fixed-period spawner cheat. ✅ LANDED.**
   Well-fed fauna reproduce (`FaunaConfigurationSO` reproduction knobs;
   `Fauna.NotifyFed → TryReproduce`; `FaunaReproductionRules` + tests); the spawner
   is a *seeder* that only tops a species up to its seed floor. Population is a
   true function of the food web — genuine Lotka–Volterra with the
   predator/herbivore tiers (the 3-tier Blob web incl. the shark is authored).
   See ECOSYSTEM.md §6.1.

5. **Elemental integration (ties ecology to gameplay through the right fundamental).**
   Flora/fauna express their effects via **Elementals** (Charge/Mass/Space/Time),
   config-driven: a domain's flora buff its vessels, fauna debuff opposing mass.
   Vessels begin to *feel* the ecosystem. Must compose with Domain + Vessels +
   Elementals, not bypass them.

6. **Domain territory dynamics + flora succession + cross-cell migration.**
   (ECOSYSTEM.md §10 steps 5–7.) As fauna cull and flora regrow, controlling domain
   ebbs and flows; different flora favor different phases; fauna migrate to adjacent
   cells chasing prey. All config-tunable, all emergent.

7. **The HUD as the platform's ecosystem read-out.**
   `DomainVolumeIndicator` already reads any cell generically. As new phase/territory
   dynamics land, make sure the gauge surfaces them without per-scene wiring. Validate
   the ring legibility / per-wedge-segment question flagged in session 9.

**For every item:** add edit-mode tests for the pure logic, update `ECOSYSTEM.md`
(and `CLAUDE.md` if a fundamental's reach changes), and append a session entry to
`ECOSYSTEM_OVERNIGHT_LOG.md`. UI/visual and in-Unity behavior need the operator's
in-editor validation — build the solid starting point and hand over precise steps.

---

## Curation gate for any new fundamental (from CLAUDE.md)

If a work item tempts you to introduce a *new* fundamental rather than extend an
existing one, run the gate first and get explicit operator sign-off: (1) name it
precisely with the canonical term; (2) show its reach across ≥3 features; (3) show
how it composes with each existing fundamental; (4) prefer extension over addition;
(5) budget the weight. Prefer, in order: use an existing fundamental → tune its
params → extend it → (only then, with sign-off) propose a new one → bespoke as last
resort.

---

## Canonical references

| Doc | What it locks |
|---|---|
| `CLAUDE.md` → Design Philosophy | The fundamentals set, curation process, "don't cheat emergence" |
| `Docs/ECOSYSTEM.md` | §0 mass-conservation invariant, §5 locked decisions, §7 food web, §9 key files, §10 roadmap |
| `Docs/ECOSYSTEM_OVERNIGHT_LOG.md` | Running session history + in-editor validation backlog |
| `Docs/ECOSYSTEM_PHASE2_KICKOFF.md` | Phase-2 framing this brief extends |
