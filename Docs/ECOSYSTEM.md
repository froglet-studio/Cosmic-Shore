# Cell Ecosystem — Workings, Analysis & Redesign

**Status:** living design doc. Created to map the cell ecology end-to-end so we
can see which parts *produce* and which parts *block* the goal — a dynamic,
vibrant ecosystem players engage with — and redesign the blockers. Built to be
extended as flora and fauna split into sub-categories (e.g. predator /
herbivore).

> Diagrams are [Mermaid](https://mermaid.js.org/) — they render on GitHub. An
> ASCII fallback of the core loop is inline in §3 for quick reading.

---

## 0. Invariant — mass is conserved (no passive decay)

**Read this first; it constrains every solution below.** A prism — whether a
lifeform's health-prism or vessel-spawned — is **only ever removed by an active
force**:

1. **A vessel using an ability** (combat), or
2. **Fauna eating it** (consumption).

There is **no passive decay, aging, lifespan, timed culler, or growth/decay
oscillator** anywhere in the prism pipeline. Population homeostasis is the job of
the **food web**, not of artificial removal:

- A **large accumulation of prisms is a valid state**, not a defect to
  auto-correct. It persists until an active force consumes it.
- The only outcomes for a big accumulation are: **fauna consume it** (opposing-
  domain fauna graze it down), **or** the fauna that depend on that prey **starve
  and the population crashes** (because the mass is the wrong domain for them, or
  out of reach). Either is correct emergent behavior.

This is why **prism decay / mortality was considered and rejected** (it was the
original Phase 2 "step 1"). A timed culler is just the flora regrowth pulse
inverted — a hard-coded oscillator that manufactures the "breathing" we want to
*emerge* from the predator–prey loop. If a cell "freezes," the fix is to give an
active force a reason or ability to consume that mass (tune fauna diet/reach/
spawning, or vessel abilities) — **never** to add decay. See CLAUDE.md → "Mass"
fundamental and "Don't cheat emergence."

**Universality — this invariant has no exempt contexts.** The HyperSea has one
rule set and everything follows it: game scenes, Menu_Main's lava-lamp/freestyle,
tools and test scenes. There is **no "cosmetic," "menu-only," or "perf special
case" exemption** — the menu autopilot vessel *is* the freestyle gameplay vessel
(lava lamp == freestyle; see CLAUDE.md → "Lava-Lamp Mode"), so any removal
mechanism attached to it is gameplay decay.

> **Rejected cheat (reverted): the menu trail cap.** Commit `64d8f0c8` added a
> per-trail ring-buffer cap (`VesselPrismController.maxTrailBlocks` +
> `Trail.RemoveOldest`) that silently recycled the oldest trail prism on every
> new spawn, set to 200 on the Menu_Main vessel to bound idle-time prism growth.
> It was rationalized as cosmetic and "gameplay unaffected" — but the cap
> followed the player into freestyle flight as an age-based trail limit, the
> exact passive-removal cheat this section rejects. Reverted. If menu-idle
> accumulation is a perf problem, use the universal systems: **fauna cleanup**
> (cleanup is one of the fauna's many jobs — foragers consume trail mass through
> the food web, §6–§7) or **pause/throttle the spawner** while idling (not creating
> mass is allowed; aging it out is not).

**Growth-side cheats — all retired.** Two artificial throttles used to fake the
homeostasis the food web is meant to produce, both now gone:

1. The flora **regrowth pulse** (a periodic growth window above the freeze
   threshold) — retired earlier.
2. The flora **phase-gated self-limit**: planting stopped at a low phase and
   growth stopped at a mid phase, so the canopy capped itself well before the
   cell was full. **Retired now** — flora plant *and* grow at a **steady rate
   until Frenzy** (the top phase), and the only down-force is the food web
   (opposing-domain fauna grazing the prisms) or a vessel ability. A cell with no
   active force on it climbs to Frenzy and stays there (a *valid* equilibrium,
   §0) until something eats its mass back below the Frenzy exit threshold, at
   which point the existing hysteresis resumes growth on its own. Retiring this
   staggered self-limit is what **collapsed the phase ladder from six rungs to
   three** (Calm → Restless → Frenzy) — the extra rungs only existed to stage the
   flora-vs-fauna events that no longer differ. See §1, §5.

---

## 1. The spine: everything keys off **prism count**

One state variable drives the whole system: the cell's live prism count
(`Cell.LiveBlockCount` = `trackedBlocks.Count`, plus per-domain counts via
`GetDomainBlockCount`). Prisms are the **mass** fundamental made concrete; the
cell is the **cell** fundamental; the color on each prism is the **domain**
fundamental. Everything else is a projection of, or a force on, prism count.

| Force on prism count | Sign | Source |
|---|---|---|
| Vessel trails | **+** | players/AI flying through the cell → `Cell.AddBlock` |
| Flora planting | **+** | new flora instantiated (`RandomLifeSpawner`) → health prisms → `AddBlock`. Steady rate until Frenzy. |
| Flora growth | **+** | existing flora grow new prisms (`Flora.Grow`) → `AddBlock`. Steady rate until Frenzy. |
| Fauna consumption | **−** | fauna seek & detonate opposing-domain prisms → `RemoveBlock` |
| Vessel abilities (combat) | **−** | vessel impacts / ability use → prism death → `RemoveBlock` |

> The **−** column is exhaustive: fauna consumption and vessel abilities are the
> *only* prism sinks. There is no decay/aging row — mass is conserved (§0). And
> the **+** column has no self-limit: flora plant + grow at a steady rate until
> Frenzy (no early planting cap, no mid-range growth cap — those staggered phase
> gates were a growth-side cheat, retired in §0).

Prism count → **Cell Phase** (`Calm → Restless → Frenzy`, computed with enter/exit
**hysteresis** so the cell doesn't chatter on the boundary). The three phases map
1:1 onto the three fauna aggression bands — the phase *is* the aggression band.
Phase is the single dial the rest of the ecology reads:

- **Calm** — low mass. Flora grow + plant freely; fauna idle toward the crystal (L0).
- **Restless** — mid mass. Fauna hunt the nearest opposing-color centroid (L1).
- **Frenzy** — frenzy ceiling. Flora **stop**; fauna seek any-domain density, drop
  friendly avoidance, ignore danger prisms (L2). The cell only leaves Frenzy when
  an active force eats its mass back below the Frenzy exit threshold.

---

## 2. Full ecosystem diagram

```mermaid
flowchart TD
    TRAIL["Vessel trails (prisms)"] -->|+| COUNT
    FPLANT["Flora planting"] -->|+| COUNT
    FGROW["Flora growth"] -->|+| COUNT

    COUNT["PRISM COUNT<br/>LiveBlockCount + per-domain counts<br/>(MASS x DOMAIN, inside a CELL)"]
    COUNT --> PHASE["CELL PHASE<br/>Calm - Restless - Frenzy<br/>(hysteresis; phase == aggression band)"]

    %% Flora grow + plant at a steady rate until Frenzy (ONE gate, no self-limit)
    PHASE --> GGROW{"Phase &lt; Frenzy?"}
    GGROW -->|yes| FPLANT
    GGROW -->|yes| FGROW

    %% Aggression is the ONLY thing prism count feeds into fauna (per redesign)
    PHASE --> AGGRO["FAUNA AGGRESSION<br/>L0 calm / L1 / L2 frenzied<br/>(derived from Phase)"]

    %% The seeder only tops the species up to its seed floor (bootstrap/recovery)
    TIMER(["Seed timer<br/>fixed period"]) --> SEED
    DOM["Controlling domain<br/>(DominantDomain)"] --> SEED
    SEED["SEEDER<br/>top up to seed floor<br/>(bootstrap + crash recovery)"]

    SEED --> POP["Live fauna population"]
    AGGRO --> BEHAVE
    POP --> BEHAVE["FAUNA BEHAVIOR<br/>L0 seek crystal /<br/>L1 opposing centroid /<br/>L2 densest any-domain"]
    BEHAVE --> CONSUME["Consume prey<br/>(prisms; herbivores for predators)"]
    CONSUME -->|−| COUNT
    CONSUME --> DOM

    %% Population control — the food web closes the loop both ways
    CONSUME -->|feeds convert to births| REPRO["REPRODUCTION<br/>FeedsPerOffspring per birth<br/>cooldown + MaxLivePopulation cap"]
    REPRO -->|+| POP
    POP --> STARVE["STARVATION<br/>no feed in starvationSeconds<br/>=> despawn"]
    STARVE -->|−| POP
    POP -.->|live count ≥ floor| SEED

    classDef state fill:#eef,stroke:#33c,stroke-width:2px;
    class COUNT,PHASE state;
```

### The three feedback loops (this is where "vibrant" lives or dies)

1. **Flora freeze at Frenzy (hard ceiling, NOT a self-limit).** `count↑ → phase↑ →
   at Frenzy, planting + growth stop.` Flora plant + grow at a **steady rate** the
   whole way up — there is no early planting cap and no mid-range growth cap (those
   staggered self-limit gates were a growth-side cheat, retired §0). Frenzy is just
   the top of the hysteresis band, not a homeostatic throttle: a cell that reaches
   Frenzy stays full until the **food web** (loop #2) grazes its mass back down, so
   the down-force on flora is the predator–prey loop, not flora throttling itself. ✅
2. **Predator–prey (negative, the heartbeat).** `count↑ → aggression↑ → fauna hunt
   harder/closer → consume more → count↓ → aggression↓ → …` This is the
   oscillation that should make the cell feel alive. Per the latest decision this
   loop runs **through aggression (behavior)**, not through spawn rate. ✅
3. **Population control (the Lotka–Volterra coupling).** Feeds convert to births
   (`FeedsPerOffspring` per offspring, per-individual cooldown, hard
   `MaxLivePopulation` performance backstop); going `starvationSeconds` without a
   feed despawns the creature. Population is therefore a true function of prey:
   rich prey ⇒ the population grows past the seed floor; scarce prey ⇒ it crashes.
   The periodic spawner is demoted to a **seeder** — it only tops a species back up
   to its seed floor (bootstrap + extinction recovery), never drives growth. ✅

### ASCII core loop (quick read)

```
        +-----------------------------------------------------+
        |                                                     |
        v                                                     |
   PRISM COUNT --> PHASE --> AGGRESSION --> fauna hunt --> CONSUME (−)
        ^   ^                                              |        |
        |   |                              feeds => births |        | no feeds
   flora +  +-- planting + growth freeze ONLY at Frenzy    v        v
   trail +      (ceiling, not a throttle)            REPRODUCE   STARVE
                                                        (+)        (−)
                                                          \        /
   SEEDER (timer, top-up to floor, controlling color) --> POPULATION
```

---

## 3. Fauna lifecycle

```mermaid
flowchart LR
    T(["seed timer<br/>fixed period"]) --> S["top up to seed floor<br/>domain = controlling color"]
    S --> A["assign aggression<br/>from cell phase"]
    A --> H["hunt: resolve goal by aggression<br/>L0 crystal / L1 opposing / L2 densest"]
    H --> E["reach prey -> consume (−count)"]
    E --> H
    E -->|feeds accumulate| R["reproduce<br/>(FeedsPerOffspring, cooldown, cap)"]
    R --> A
    H -->|no prey reachable| ST["starve -> despawn"]
    E -.->|predator reaches it first| P["predated -> despawn"]
```

Every arrow is implemented: spawn (seeding), reproduction (births from feeds),
starvation, and predation all run through the same `Fauna` base.

**Vessel predation & husbandry — the Crystal Joust (Squirrel).** Every living lifeform's
elemental crystal is its **heart**: `Crystal.SetEmbeddedIn(lifeform)` (fauna wire it in
`LightFauna`/`Boid` after `LifeFormCrystal.EnsureElementalCrystal`; flora in
`LifeForm.Initialize`) enables the heart's SphereCollider so a vessel can JOUST it. The
embedded heart is never a pickup — skim-collect and skimmer vacuum both gate on
`Crystal.IsEmbedded` — and the vessel-side chain routes to the container's
`VesselLifeformCrystalEffects` instead of the collect chain. The Squirrel's
`VesselWitherLifeformByCrystalEffectSO`:
- **Speed gate (both branches)**: the joust lands only while the vessel moves FASTER
  than the lifeform (`ILifeFormEntity.CurrentSpeed` + authored margin). Rooted flora sit
  at 0 — trivially joustable; fast fauna must genuinely be overtaken.
- **BASE ability (ungated)**: an **opposing-domain** lifeform is destroyed —
  `ILifeFormEntity.Jousted` routes fauna through the sealed `Predated→Die` (wither +
  crystal drop, spawn immunity respected) and flora through `LifeForm.Die` (spindle
  wither + crystal drop). An ACTIVE force; mass conserved, continuity honored.
- **Space level-5 'Shepherd' upgrade**: an **own-domain** lifeform is NOURISHED instead —
  `ILifeFormEntity.LevelUp()` grows body + heart one level (below the unlock an ally
  joust does nothing; an ally is never killed).
Collider cost: **+1 active SphereCollider per live lifeform heart** (fauna bounded by
`MaxLivePopulation`; flora by the profile's spawn counts).

**The lifeform elemental contract (element × level).** Mirroring the vessel contract,
every lifeform answers `ILifeFormEntity.Element` and `.Level` (1..`Fauna.MaxLifeformLevel`
= 5): **one base prefab, 20 data-defined variants** (4 elements × 5 levels) instead of a
prefab per element. The element is data on `FaunaConfigurationSO.Element` — at
`AssignLineage` the heart is provisioned from `ElementalCrystalSet` for that element
(`LifeFormCrystal.EnsureElementalCrystal(owner, element)` replaces a disagreeing authored
crystal; `None` keeps the legacy per-variant-prefab path). Level scales the creature via
config (`InitialLevel`, `BodyScalePerLevel`, `CrystalScalePerLevel`, `LevelGrowSeconds`):
spawns arrive AT size; in-world level-ups **grow** over `LevelGrowSeconds` (continuity —
never a pop) with `NotifyBodyPrismsMoved` keeping the spatial index honest, and the heart
grows a step per level so a higher-level creature drops a **bigger** elemental powerup on
death (mass rewarded, still conserved). Flora answer the contract at fixed level 1 until
flora leveling lands.

**Variant expression (`FaunaVariantTuning` on the config).** The full diff between the
authored Mass/Space/Time tadpole prefab variants was hoisted into config so one base
prefab can express all of it as data (sentinels keep the prefab's authored value):
body scale (0.4/0.7/0.4) · body PRISM target scale (Mass/Time author 0.8×0.8×7 tail
prisms, Space keeps the spindle default) · spindle body material · starvation seconds
(90/30/30) · cohesion radius (50/20/20) · behavior tick (1.5/3/3) · graze radius
(45/15/15) · goal weight (3/0.3/0.3) · speed band (10-15/10-15/15-20) · forager flag
(on/off/off) · FMOD loop event + attenuation (Mass Tadpole 0-200 / silent / Time
Tadpole). Applied by `Fauna.ApplyVariantTuning` (base: scale/prism-scale/material/
starvation/audio) + `Boid.ApplyVariantTuning` (flocking numbers) at `AssignLineage`,
before the level curve seeds. Population-level knobs (`numberOfBoids`, `spawnRadius` on
the drone BoidManager population prefabs) stay on that separate system; the spawner path
already owns them via `PopulationSize`/`MaxLivePopulation`.

**Flora variant expression (`FloraConfigurationSO.Element` + `FloraVariantTuning`).**
Same move for flora, captured from the real Charge/Mass/Space/Time GyroidFlora diff —
the per-element identity is largely the PRISM: leaf prism size (9×3.4×1.5 / 7×4.5×3.5 /
20×1×1 needles / 9×3.4×1.5) · grow period (0.5 / 0.3 / 0.8 / **0.15** — Time grows
fastest) · shield period (**1** — Charge ships shielded leaves / 0 / 0 / 0) · live-prism
budget `maxTotalSpawnedObjects` (1000 / **1500** / **800** / 1000) · plant radius
fraction · crystal element. `CellLifeSpawnerBase.SpawnFlora` now takes the config and
applies element + tuning BEFORE `Initialize` (leaf size and the crystal lookup are
consumed there); `LifeForm.ApplyVariantTuning` (shield cadence) → `Flora` (leaf/tempo/
radius) → `AssembledFlora` (prism budget) layer the fields where they live.

**Level → crystal size.** `CrystalScalePerLevel` makes the level curve monotone in the
heart: level 1 = authored size, level 5 = ×(CrystalScalePerLevel)⁴ (≈2.07× at the 1.2
default) — the level-5 creature always carries, and drops, the largest crystal. Flora
level the same way (`FloraConfigurationSO.InitialLevel` + `LeafScalePerLevel` /
`CrystalScalePerLevel`, applied via `LifeForm.ApplyLevel` before Initialize).

**Unification (SHIPPED) — one base prefab per species, variants are config.** The
per-element prefab variants were retired: `TadPoleFauna.prefab` (formerly
MassTadPoleFauna) and `GyroidFlora.prefab` (formerly MassGyroidFlora) are the single
base prefabs; Space/Time tadpoles, Charge/Space/Time gyroids, and the unused
TimeTadpolePopulation were deleted with every reference migrated (the variant prefabs
were literal copies sharing fileIDs, so guid swaps were reference-safe). The canonical
per-element configs live in `Assets/_SO_Assets/Lifeforms/` (Tadpole Fauna
Charge/Mass/Space/Time + Gyroid Flora Charge/Mass/Space/Time — Charge tadpole is NEW
and untuned, authored from the Space baseline); the existing Cell Config assets carry
their element's Variant block explicitly. Legacy note: the drone-population prefabs
(BoidManager path) now all spawn the base tadpole - per-element identity there awaits
that system's own config pass.

**Lifeform Matrix toy (the tuning bench).** `Toy_LifeformMatrix` (in the freestyle
toybox): fly through it → a station per species blooms in; fly a species → its variant
matrix (4 element columns × level rows {1, 3, 5} — the extremes and middle of the 4×5
contract; station spheres tinted per element and sized by level); fly a variant → that
exact lifeform spawns live into the containing cell through the canonical spawn paths
on a runtime clone of its config (assets never mutated; spawns are ordinary food-web
citizens). Files: `LifeformMatrixToyDefinitionSO`, `LifeformMatrixToy` (+ station).
Collider impact: transient trigger spheres only (species count + ≤12), Menu freestyle
only, torn down with the matrix.

---

## 4. Part-by-part analysis

| # | Part | Driver | Current behavior | Desired | Gap / action |
|---|---|---|---|---|---|
| 1 | Prism count (state) | trail + flora − fauna/combat | tracked via Add/RemoveBlock, per-domain | same | ✅ the spine, leave alone |
| 2 | Cell phase | prism count + hysteresis | Calm→Frenzy (3 phases) | same | ✅ 2 thresholds tunable per biome |
| 3 | Flora **planting** | `Phase < Frenzy` | steady rate until Frenzy | prism-count driven | ✅ steady-until-frenzy (cheat removed) |
| 4 | Flora **growth** | `Phase < Frenzy` | steady rate until Frenzy | prism-count driven | ✅ `AssembledFlora`/`BranchingFlora`; same gate as planting |
| 5 | Fauna **aggression** | Phase → L0/L1/L2 | seek crystal→opposing→densest | prism-count driven | ✅ works; extension seam for a 4th tier / per-subtype |
| 6 | Fauna **spawn timing** | fixed-period seed timer | seeds at `BaseFaunaSpawnTime` | same | ✅ timer-only, fixed period |
| 7 | Fauna **spawn count** | deficit below seed floor | tops species up to `PopulationSize` | same | ✅ seeder (reproduction drives growth, §6.1) |
| 8 | Fauna **domain** | `host.ControllingDomain` | controlling color | same | ✅ (was the "no Jade fauna" bug) |
| 9 | Spawn-cycle HUD ring | `CurrentFaunaSpawnPeriod` (base period) | fixed sweep | same | ✅ no aggression scaling |
| 10 | Fauna **population bound** | starvation + reproduction + `MaxLivePopulation` | prey-linked rise and crash | same | ✅ §6 + §6.1 (full Lotka–Volterra) |
| 11 | Fauna **consume → −prisms** | aggression behavior → impact | reduces opposing prisms | same | ✅ this is the prey side of loop #2 |

**Root causes of what you saw:**
- *Dead spawn-cycle ring* → `RandomLifeSpawner` never called `RecordFaunaSpawn` (fixed in the retrofit). **Correction:** NOT all scenes run `RandomLifeSpawner` — the WildlifeBlitz scenes (`MinigameWildlifeBlitz`, `MinigameWildlifeBlitzMultuplayerCoOp`) and `MinigameTournamentMultuplayer` select `IntensityWiseLifeSpawner` (`cellTypeChoiceOptions: 1`). Menu, Skim Race, and the rest use `Random` (0).
- *No fauna in menu* → fauna were gated on scored team volume (~0 in Menu_Main). Retrofit moved them to a phase gate; the redesign moves them to **timer only**.
- *No Jade fauna when Jade controls* → row 8: the controlling/local color is **excluded by construction**.

---

## 5. Redesign — locked decisions

From the latest direction ("keep period and swarm size fixed, rely on modifying
the aggression levels of all fauna" + "do what's best for basic functionality to
first approximations, build to extend"):

- **Aggression is the lever.** `prism count → phase → aggression level → fauna
  behavior`. Aggression does **not** feed spawn rate/size — only behavior. This
  makes loop #2 the heartbeat.
- **Fauna domain = controlling color** (`host.ControllingDomain`). Fixes the Jade
  bug; the dominant domain's fauna proliferate and hunt the minority. Trivial,
  certain change — folded into the spawn rewrite.
- **Spawner = SEEDER** *(supersedes the original "fixed N per tick" decision —
  roadmap step 3 landed)*. The timer still ticks at the fixed `BaseFaunaSpawnTime`,
  but each tick only tops the species back up to its **seed floor**
  (`PopulationSize`): bootstrap at scene start, recovery after a crash. Above the
  floor, **reproduction** is the population driver (see §6) — the spawner never
  races the food web.
- **HUD ring = base fixed period** (remove the aggression scaling the retrofit
  added to `ScaleFaunaInterval` / `CurrentFaunaSpawnPeriod`).
- **Keep 3 aggression tiers** — and they are now the **same thing as the phases**.
  The 3-phase collapse made `CellPhase` (Calm / Restless / Frenzy) map 1:1 onto
  `CellAggressionLevel` (L0 / L1 / L2). Frenzy = top tier; a 4th "berserk" tier or
  per-subtype aggression curves slot into the existing `CellAggressionLevel` switch
  when the predator/herbivore split deepens (§7).
- **The phase ladder is the aggression ladder.** Because flora are no longer
  staggered on their own rungs (steady until Frenzy, §0), a cell needs only **two
  thresholds** to author: `RestlessEnter` (fauna start hunting) and `FrenzyEnter`
  (flora freeze + max aggression). Down from five. The per-biome boundaries are
  unchanged in value by the collapse — only the redundant middle rungs were dropped —
  so existing fauna aggression behavior is identical; only flora now fills denser
  (it grows to Frenzy instead of stopping at the old mid-range growth cap).

### 5.1 Density & the steady-until-frenzy model (cells were sparse / froze solid)

- **Capacity is a PERFORMANCE budget, not a "fill it up" dial.** `FrenzyEnter` is
  the steady-state prism count (the cell pins there, §6/§12), and prism count is the
  dominant frame cost — so it is tuned against the frame budget, not for visual
  density. The menu's `Blob Cell Config` is `RestlessEnter 600 / FrenzyEnter 1000`
  (was 3000/5400, which sat the menu at ~5 fps — see §12). `FrenzyEnter` also sets
  the `DomainVolumeIndicator` volume scale. Other biomes use the high code `Default`
  (`FrenzyEnter 15000`); Skim Race is `RestlessEnter 600 / FrenzyEnter 2000`.
- **Flora grow steadily until Frenzy.** The old "stop planting at a low phase, stop
  growing at a mid phase" staggered self-limit (and, before it, the periodic
  **regrowth pulse**) are **both retired** — they were growth-side cheats faking the
  breathing the food web is meant to produce (§0). `Cell.FloraGrowingEnabled` /
  `FloraPlantingEnabled` are now simply `phase < Frenzy`: flora plant + grow at a
  steady rate the whole way up, then freeze at Frenzy.

> The honest model: a frozen-solid cell at Frenzy is a **valid state**, not a defect
> to auto-correct. It stays frozen until an active force — opposing-domain fauna
> grazing it, or a vessel ability — removes mass and the existing `phase < Frenzy`
> hysteresis resumes growth on its own. Mass is conserved; the down-force is the
> **food web**, never decay or a growth/decay oscillator. See §0 and §10.

---

## 6. Decision — fauna bounded by **prey-linked starvation** (option C)

This is loop #3, and it's the one thing that, missing, prevents the ecosystem
from breathing. Three ways to add the negative feedback, smallest→most emergent:

| Option | Mechanism | Pros | Cons |
|---|---|---|---|
| **A. Population cap (stop-producing)** | Track live fauna per cell (we already have `spawnedLifeForms` / `LifeFormsInCell`); timer skips spawning while count ≥ `MaxFaunaPerCell`, resumes when it drops | Dead simple, deterministic, prevents runaway today | Doesn't itself remove fauna — needs a removal source to recover |
| **B. Lifespan (natural cull)** | Each fauna despawns after `T` seconds | Self-bounding (pop ≈ rate × T), simple, predictable | Decoupled from prey — not yet "emergent", just a timer |
| **C. Prey-linked / starvation (emergent)** | Fauna persist only while prey (opposing prisms) is reachable; they **starve & despawn** when prism food is scarce, and production pauses when food is low | Ties population to prism count → genuine predator–prey oscillation; this is where the **predator/herbivore** split naturally lives (herbivores eat flora prisms, predators eat herbivores) | Most work; needs a hunger/last-fed state on fauna |

**Decision: C (prey-linked).** Implemented now rather than the A+B interim — it's
the emergent north star and the seam the predator/herbivore split plugs into.

**Implemented:**
- `Cell.OpposingVolume(domain)` = live ENVIRONMENT volume not of `domain` — the prey
  signal (volume is the spine; fauna bodies are excluded — they aren't edible prey).
- **Production pauses** when `OpposingVolume(controllingColor) < FaunaFoodFloor × 16`
  (the floor is authored in nominal prisms, converted by `NominalPrismVolume`)
  (`SpawnProfileSO`, default 5): the timer keeps ticking but no population spawns.
- **Starvation cull** on `Fauna`/`LightFauna`: a creature that hasn't consumed a
  prism in `starvationSeconds` (default 30, `Fauna` field) despawns; `NotifyFed()`
  resets the clock on every `Consume`.
- Net: population self-bounds to prey. Because fauna only *hunt* opposing prisms
  at higher aggression (L1+), and aggression rises with prism count, **survival
  tracks prism count** — low mass ⇒ fauna can't find food ⇒ they thin out; high
  mass ⇒ they feed and multiply. That coupling is the oscillation.

### 6.1 Reproduction — the population driver (roadmap step 3, LANDED)

The fixed-period spawner-as-population-source was the last scaffolding cheat; it
is now retired. **Feeds convert to births**:

- Every `NotifyFed()` (prism consume; a kill for predators) advances a per-individual
  birth counter. At `FeedsPerOffspring` feeds the fauna births `OffspringPerBirth`
  offspring next to itself (post-spawn predation immunity gives them time to
  disperse), subject to a per-individual `ReproductionCooldownSeconds` and a hard
  per-cell, per-species `MaxLivePopulation` cap — a **performance backstop**, not
  the primary control (starvation is). All four knobs live on
  `FaunaConfigurationSO`; `FeedsPerOffspring = 0` (the default for un-authored
  assets) disables reproduction for the species.
- Offspring inherit the parent's domain and **lineage** (`Fauna.AssignLineage`:
  host cell + species config), so they count toward the cell's per-species live
  population (`Cell.GetLiveFaunaCount`) and can reproduce in turn.
- The **spawner is demoted to a seeder**: each fixed period it spawns only the
  *deficit* below the species' seed floor (`PopulationSize`) — bootstrap at scene
  start, recovery after extinction — and stays out entirely while the food web
  sustains the population at or above the floor
  (`FaunaReproductionRules.SeedSpawnCount`). The seeder is acknowledged residual
  scaffolding: real ecosystems get immigration; ours gets a floor so a crash is
  never a permanently-dead scene.
- The pure gating lives in `FaunaReproductionRules` (`ShouldBirth` /
  `SeedSpawnCount`) with edit-mode tests pinning the Lotka–Volterra coupling.

**Tuning knobs** (watch in Menu_Main, expect to adjust): `starvationSeconds` (too
low ⇒ fauna starve before reaching prey; raise it), `FaunaFoodFloor` (min prey
before seeding), `PopulationSize` (seed floor), `BaseFaunaSpawnTime` (seed period),
`FeedsPerOffspring` (lower ⇒ steeper population upswing on rich prey),
`ReproductionCooldownSeconds` (birth burst throttle), `MaxLivePopulation` (the
**taming dial**, §6.2 — *and* a performance ceiling, §12).

### 6.2 Taming vs devouring — the caps dial (gameplay balance)

The food web has two qualitatively different equilibria, and which one a cell sits
in is the difference between "fun to fly through" and "stripped bare". It is set by
one comparison:

> **food-supported herbivores** = `flora_growth_rate / per-herbivore_graze_rate`
> — the standing herbivore count whose total grazing exactly equals flora growth.

- **Summed herbivore `MaxLivePopulation` ≤ food-supported** → fauna *cannot*
  out-graze flora. Mass grows to `FrenzyEnter` and **holds** (breathing in the
  `[FrenzyExit, FrenzyEnter]` band). The fauna **tame** the edges; the environment
  stays sizable. This is what freestyle/menu wants (fly through gyroids).
- **Summed herbivore cap > food-supported** → fauna out-graze flora, eat the mass
  to the ground, then starve and let it regrow — a boom/bust that keeps the
  environment **stripped**. This is what an aggressive trail-cleanup wants (Skim
  Race: graze the AI obstacle buildup down) but ruins a fly-through scene.

So the **same** forager species is *taming* or *devouring* purely by its **cap per
biome** — no behavior/diet change needed. The menu holds its herbivore cap low
(below food-supported) to preserve the gyroids; Skim Race keeps it high to clear
the track. `Tools/ecosim/ecosim.py` reports this outcome (`TAMED` vs `DEVOURED`)
per config; refine the `FLORA_GROWTH_PER_S` / `GRAZE_PER_HERBIVORE_S` ratio against
real `EcosystemPerfProbe` gyroid observations.

> This does **not** reintroduce a cheat: mass is still conserved and the only
> down-force is the food web (§0). "Taming" just means sizing the predator so the
> predator–prey fixed point lands at a sizable prey level instead of a stripped
> one — a parameter choice, not a hard-coded culler.

---

## 7. Predator / herbivore split — IMPLEMENTED + wired (3 species)

The diet split is in code (`FaunaDiet` enum + `Fauna.diet` + `LightFauna`
consume branch + `Fauna.Predated`) **and wired into the Blob (menu) test cell**
with the team's three real species:

| Species | Prefab | Component | Diet | Role |
|---|---|---|---|---|
| **Tadpole** | `MassTadPoleFauna` | `Boid` (`forager: 1`) | Herbivore | flocking forager swarm |
| **Brittlestar** | `MassBrittlestarFauna` | `LightFauna` | Herbivore | grazer |
| **Shark** | `MassSharkFauna` | `LightFauna` | **Predator** (`diet: 1`) | apex; eats *both* herbivores |

All three are **spawnable** by the cell config (`SpawnProfileSO.SupportedFaunas`,
via `RandomLifeSpawner`). Two herbivore species + one predator. **The shark is
wired into the Blob (menu) profile** at apex-tier numbers (seed floor 2, cap 3,
births on 3 kills) — safe now that spawn immunity gives co-spawned herbivores a
dispersal window. It stays **out of Skim Race** deliberately: predators remove
foragers, which is counterproductive to that scene's trail-cleanup perf goal.

> **The live spawn path is the cell config — NOT the scene-placed populations.**
> The `MassTadpolePopulation` / `MassBrittlestarPopulation` etc. objects in scenes
> are wired through a `Cell` field named `fauna2` that **no longer exists** on
> `Cell.cs` — a dead/stale prefab override Unity ignores. So those `BoidManager`/
> `LightFaunaManager` populations **never instantiate**. Every working fauna comes
> from a `FaunaConfigurationSO` in the cell's `SpawnProfileSO`. (This is why
> removing the tadpole config = no tadpoles, regardless of the placed population.)

- **Diet = "what counts as prey"** is a `FaunaDiet diet` field on the `Fauna`
  base (`Herbivore` / `Predator`), defaulting to **Herbivore**:
  - **Herbivore** — eats prism MASS, but the two herbivore species differ
    (neither eats **shielded or super-shielded** mass — the shared rule, §15):
    - `LightFauna` (brittlestar) `Consume`s **opposing-domain** flora/trail prisms
      within `consumeRadius`.
    - `Boid` (tadpole forager) `Consume`s (implode → **suction shader**) any
      **unshielded** prism of **any domain** that is **not a fauna body** — so it
      grazes the *dominant* trail too (the bulk of the obstacle mass), while
      skipping the shielded race track and other creatures. Detect/eat radius =
      `cohesionRadius`/`trailBlockInteractionRadius` (currently 50/45). (Boid's
      Attach/mound effect is unused by tadpoles but stays — drone abilities use it.)
  - **Predator** — consumes **herbivore fauna of any species** via
    `GetComponentInParent<Fauna>()` on nearby colliders (matches the `Fauna` base,
    so a shark eats both `LightFauna` brittlestars and `Boid` tadpoles) →
    `prey.Predated()`. Predators **never** eat prism mass. Predation **ignores
    domain** (a diet relationship, not a team fight) so predators have prey even in
    a single-domain cell — the food web bounds them, not the domain split.
- **Population bounds (per species) — all starvation-linked:**
  - *Brittlestar* (`LightFauna`) — `Fauna.IsStarving → Die` + shark predation.
  - *Tadpole* (`Boid`, `forager: 1`) — feeds (`NotifyFed`) when it grazes any
    edible prism, and starves (`IsStarving → Die`) after `starvationSeconds` (90 on
    the prefab) without feeding. So the swarm **self-limits to available prey**: it
    grows where there's mass to eat and thins out (dropping its CPU cost) once the
    obstacles are cleared. The `forager` flag is OFF on the drone `Boid` path
    (BoidController/mound), which must not starve.
  - *Shark* (`LightFauna`) — `IsStarving → Die` when no herbivores are reachable.
  Net: a self-bounding food web — the spawner keeps adding `PopulationSize` per
  period while there's prey; starvation + predation remove them when there isn't.
- **Targeting (v2 — real prey-seeking):** predators hunt the **nearest live
  herbivore** via the cell's fauna registry (`Cell.LiveFauna` — the fauna analogue
  of the prism density grid: the cell sensing its inhabitants, not a privileged
  shortcut). Predation-immune newborns are skipped so a shark doesn't camp a fresh
  birth. With no herbivores alive, the predator falls back to the shared
  phase-based density goal (roams plausibly, then starves). Herbivores still swarm
  opposing-mass density. **v3 layers intentional consumption on top — see §7.3.**
- **Spawn gating (by diet):** `RandomLifeSpawner` seeds a herbivore species when
  `OpposingVolume >= FaunaFoodFloor × 16` (prism prey, in volume) and a predator species when
  `GetLiveHerbivoreCount() >= FaunaFoodFloor` (real food, not the old prism-mass
  proxy) — so sharks never churn-spawn-and-starve in a cell with mass but no
  herbivores. `FaunaFoodFloor` doubles as both floors (N prisms / N herbivores);
  split it into two knobs only if a biome needs them to differ.
- **Aggression tiers** stay the behavior dial per diet (a 4th tier or per-diet
  curves slot into the existing `CellAggressionLevel` switch points:
  `Cell.AggressionLevel`, `Fauna.ResolveGoal` / `LightFauna.UpdateBehavior`).

### 7.1 Wiring recipe
- **Predator diet:** `MassSharkFauna`'s `LightFauna` has `diet: 1` (Predator);
  `MassTadPoleFauna`'s `Boid` has `forager: 1`. Herbivore brittlestar needs nothing.
- **One `FaunaConfigurationSO` per species**, pointing at the **creature** prefab's
  Fauna component (Boid for tadpole, LightFauna for brittlestar/shark) — *not* the
  Population/manager prefab — listed in the cell's `SpawnProfileSO.SupportedFaunas`.
  `PopulationSize` = boids spawned per period (tadpole bigger for the swarm,
  predator smaller for the apex tier).
- **Seed floor = `PopulationSize`** (Blob tadpole 25, Skim Race tadpole 12). The
  seeder tops the species back up to this each `BaseFaunaSpawnTime`; above it the
  population is reproduction-driven (`FeedsPerOffspring` etc., §6.1) and bounded by
  starvation + the `MaxLivePopulation` performance cap. A denser standing swarm
  wants a lower `FeedsPerOffspring` (faster births) *and* enough prey to keep it fed.

> **Don't rely on the scene-placed `*Population` objects** — they're wired through
> the dead `Cell.fauna2` field (removed) and never spawn (see the §7 note). The
> `Boid` mound code stays in the class (drone abilities use it via BoidController);
> the tadpole prefab just never invokes it (Explode-only, `forager: 1`).

### 7.2 Trail-management test deployments (two scenes)

The food web is wired into two scenes to test the ecosystem's ability to manage
**trail** prisms (player/AI mass), not just flora:

**A. Menu_Main freestyle toy box** (`Blob Cell Config → Blob Cell Spawn Profile`).
The goal here is **flying through sizable gyroids**, so the food web is tuned to
**TAME, not devour** (see §6.2): a small 3-tier presence — tadpole forager (seed
floor 4, cap 6, slow births @20 feeds) + brittlestar (floor 3, cap 5, @16) +
**shark** (floor 1, cap 2, @6 kills). Summed herbivore cap (11) is held **below
the flora's food-supported count** so the fauna cannot out-graze flora growth —
the gyroids grow to `FrenzyEnter` (1200) and **hold** there (breathing in the
~950–1200 band), with the fauna trimming the edges. A bigger or faster-breeding
swarm flips it to *devouring* (gyroids stripped, boom/bust) — exactly the
over-grazing the perf-cut numbers first caused. The tadpole grazes any unshielded
non-fauna mass (incl. the dominant gyroid), the brittlestar grazes opposing mass,
the shark eats both herbivores. Caps are also **performance-bounded** (§12: each
fauna's per-tick `OverlapSphere` is a top frame cost). Levers: per-species
`MaxLivePopulation` (the taming dial, §6.2) + reproduction knobs (§6.1),
`FrenzyEnter` (gyroid size, perf-capped), `starvationSeconds` / `consumeRadius`.

**B. Skim Race** (`MinigameHexRace`, dedicated `Skim Race Cell Config → Skim Race
Spawn Profile`, isolated from the 6 other scenes that share the Barren config).
**No flora**; only the herbivore forager swarm (tadpole `PopulationSize` 12 +
brittlestar), **Random** spawner (`cellTypeChoiceOptions = 0`) so spawning is
prey-linked. Hypothesis: at late laps / high player counts, AI orbiting crystals
leave an excess of **trail-prism obstacles**; the forager swarm grazes them →
fewer prisms → better perf; foragers self-limit (starve) once the obstacles are
cleared.

> **Shark status: IN the Blob (menu) profile, OUT of Skim Race.** The original
> "only sharks" wipe (predators eating every herbivore at co-spawn, before the
> swarm dispersed) is covered by spawn immunity
> (`Fauna.predationImmunitySeconds`, default 6s, stamped in `Awake`; `Predated`
> refuses during the window), so the menu now runs the full apex tier at low
> numbers (seed floor 2, cap 3). Skim Race stays predator-free deliberately —
> predators remove foragers, which is counterproductive to that scene's
> trail-cleanup perf goal. If the menu sharks still overgraze in practice, lower
> their `MaxLivePopulation`/`PopulationSize` or raise `FeedsPerOffspring` before
> considering removal.

> **Other caveats to validate in-editor (I can't run Unity):**
> 2. **Sense coverage (addressed — tune `SenseRadiusOverride`).** Registration +
>    density targeting used to be capped at the ~1200 membrane while the track runs
>    ~4000 long, so foragers only sensed/cleaned the central bubble. Now
>    `Cell.SenseRadius` (a `CellConfig.SenseRadiusOverride`, **3000** on Skim Race)
>    decouples sensing from the visual membrane, so the cell registers + builds its
>    density grid across the whole track and the forager's `ResolveGoal` (=
>    `GetDensestRegionAnyDomain`) sends the swarm to the densest trail buildup
>    track-wide — emergent, not track-following. If foragers still don't reach a
>    far end, raise `SenseRadiusOverride`; if the grid feels too coarse, lower it.
> 3. **Domain.** Foragers eat *opposing*-domain mass, so the dominant domain's own
>    trail isn't grazed by its own fauna. At multi-domain player counts most trail
>    mass is still "opposing" to *some* school, but the single dominant trail is the
>    one accumulation the food web won't touch.
> 4. **Client-local fauna.** Fauna + trail prisms have **no `NetworkObject`** —
>    client-local (trails reconstructed from networked vessel movement; cell phase
>    synced via `CellNetworkSync`). Fine for a per-client **perf** test and the
>    menu; **diverges across clients**, so not yet fair for competitive play.
> 5. **Net perf.** Fauna cost CPU (per-tick `OverlapSphere` per creature). Test
>    whether trail savings beat fauna cost: start modest and profile before/after;
>    scale `PopulationSize` only if net-positive.

### 7.3 Intentional consumption & the mouth-driven predator (v3)

Consumption is no longer an instant vacuum inside a radius — both diets now *act
out* their feeding, using the systems that already exist (the suction implosion,
the danger prisms, `Cell.LiveFauna`). No new colliders; tunables live on
`LightFaunaDataSO` (brittlestar/shark) and the `Boid` prefab (tadpole).

**Herbivores (brittlestar `LightFauna`, tadpole `Boid` forager) — approach →
face → suction → watch:**
- The behavior tick only **selects** the nearest edible prism (same edibility
  rules as before, factored into `IsEdibleForHerbivore` / `IsEdibleForForager`);
  the creature then steers toward it.
- Feeding starts only once inside the **minimum feeding distance**
  (`consumeRadius` / `trailBlockInteractionRadius`) — the creature never has to
  be right on the prisms. There it brakes to a hover and **turns to face the
  meal**; the suction begins only within `feedingFacingAngle`.
- One bite = the faced prism plus edible prisms within `feedingClusterRadius`
  (capped by `maxClusterBites`), all imploding toward the creature — a
  deliberate mouthful instead of a radius-wide vacuum, at comparable throughput.
- The creature **holds facing for `consumeHoldSeconds`** (default 2s = the
  suction shader's travel time) so it visibly watches its meal all the way in.

**Predators (shark `LightFauna`) — pursue → strike at the mouth → devour:**
- The tick-selected nearest prey is held as a live reference; per-frame homing
  (`pursuitAgility`) tracks the fleeing target between ticks and
  `pursuitSpeedMultiplier` makes the chase read as a chase. Separation from
  environment prisms (flora/trails) still applies each tick — the shark
  maneuvers around obstacles — but **prey bodies never repel the predator**.
- The **mouth** is a lightweight transform at the danger-prism centroid, created
  at Initialize. **Attack range = `attackRange`** (flat world units; default 15
  ≈ the shark's danger-prism length — a tuning starting point, not a derived
  value).
- Every frame the predator checks the cell's small `LiveFauna` registry: any
  live, non-immune herbivore within attack range of the mouth is devoured. Pure
  math, no physics, no contact — the kill is deterministic, and the danger
  prisms are **never disturbed by prey being eaten** while staying fully
  vulnerable to vessels/projectiles (they're ordinary body HealthPrisms; all
  fauna diets already exclude fauna bodies).
- Devoured prey **breaks apart**: `Predated(name, mouth)` routes the sealed
  crystal-dropping `Die`, then the body prisms suction (implode) **into the
  mouth**, nearest-first, a few per frame — the suction sink follows the
  swimming shark. Residual structure (spindles) evaporates via `CheckForLife`;
  starvation deaths keep the classic extremities-first wither.

**v3.1 — territorial predators + herbivore breathing room** (sharks were too
effective; herbivores got eaten before they could graze). Three levers, all
O(1) per tick:
- **Tiger-shark territoriality** (`LightFaunaDataSO.territoryRadius` /
  `territoryAnchorDistance`; 0 = legacy cell-wide hunting): each predator rolls
  a fixed **den** point at spawn (random direction × anchor distance from the
  cell centre — spreads the 2-3 concurrent sharks apart with zero
  coordination). Prey selection keys off distance to the DEN and ignores prey
  outside the territory; an empty patch means **patrolling home**, not roaming
  the shared density goal — so any herbivore group faces at most one predator
  and distant groups feed unmolested. Same single registry loop as before.
  The per-frame mouth check is unchanged — a shark still eats anything that
  swims into its jaws.
- **Centre focus** (`FaunaConfigurationSO.CenterFocusBias`, per-deployment,
  default 0): lerps the herbivore/forager roaming goal toward the cell centre
  so the species lingers on the central canopy (the gyroids around the
  nucleus). Edibility untouched — a nucleus claim stays protected. Blob
  brittlestar + tadpole run 0.35; **leave 0 on far-ranging deployments** (the
  Skim Race cleanup swarm must reach the whole track).
- **Herbivore spawn-point ring** (`SpawnProfileSO.HerbivoreSpawnPointCount` /
  `HerbivoreSpawnRadius`; 0-1 = legacy densest-mass spawn): successive
  herbivore waves rotate between N points spaced evenly on a circle around the
  cell centre (equidistant from each other and the centre), so each new group
  gets its own feeding ground and a head start before a territorial predator's
  patch reaches it. Computed once per 30s wave; predators keep the
  densest-mass spawn. Blob runs 3 points at radius 400. **The rotation is keyed
  to the wave clock, not to a spawn counter — see §15.**

**v3.2 — polar predator ring + feeding-consistency fixes** (from in-editor
observation of v3.1):
- **Predator spawn ring** (`SpawnProfileSO.PredatorSpawnPointCount` /
  `PredatorSpawnRadius`; 0 = legacy): a VERTICAL circle starting at +Y,
  orthogonal to the equatorial herbivore ring — 2 points sit exactly on the
  poles. While active, at most **one predator spawns per interval**
  (alternating points), and each predator's **den lands in the hemisphere it
  spawned in** (the random den direction is mirrored if it points into the
  opposite half — one dot product at spawn). Blob runs 2 points at radius 600.
- **Boid dash oscillation (BUG, fixed):** the forager dash was a binary 10×
  whenever the goal was beyond the interaction radius, re-checked only once
  per 1.5s behavior tick — at dash speed a tadpole covered ~200+ units per
  tick, overshot the goal, reversed at 10×, and oscillated rapidly across it
  without ever settling into feeding range (observed as "back-and-forth
  between two distant points, never engaging mass"). Now **arrival-capped**:
  dash speed ≤ distance/tick, decelerating smoothly on approach (one sqrt per
  tick). Tadpole feeding distance (`trailBlockInteractionRadius`) also tuned
  45 → 20 on the prefab.
- **Brittlestar "swims past its food" (fixed):** flora are HealthPrisms, and
  ALL HealthPrisms within `separationRadius` (70) repelled the brittlestar —
  including edible ones — while feeding required closing to `consumeRadius`
  (40); approach geometry decided whether it ever ate. Now **edible prisms
  attract and never repel** (one edibility check per prism decides both
  roles; non-edible mass — own canopy, nucleus claim, fauna bodies — still
  separates). Plus **mouthful chaining**: when a suction hold ends, one small
  index query re-targets the nearest edible still inside feeding range, so a
  creature parked at a buildup eats mouthful after mouthful — it feeds more
  than it swims — resuming roaming only when the local patch is clear.

**v3.3 — predator hunt pulses** (sharks still dominated even split into
hemispheres): predators now hunt in **periodic windows**
(`LightFaunaDataSO.huntIntervalSeconds` / `huntDurationSeconds`, default
20/10 → alternating 10s rest / 10s hunt; interval 0 = always hunting,
legacy). Outside the window the predator carries **no prey target** — no
targeting, no pursuit boost, no per-frame homing, and the mouth is closed
(`TryDevourPreyAtMouth` skipped), so even prey swimming straight into its
jaws survives until the next window; it just cruises its territory. The
window can close mid-chase (breaks off immediately). Implementation is pure
clock math — one `Mathf.Repeat` per check, no state, no coroutine — and each
predator's cycle starts with the REST stretch at spawn, layered on the
prey's spawn immunity. Starvation still applies across rest windows, so a
predator that can't convert its hunt windows into kills thins out — the
duty cycle caps predation *rate*, the food web still owns population.
**Presentation:** `SharkJawDriver` (on the prefab's `Shark_model`) blends the
two mouth MultiAimConstraint weights 0 (closed, FBX pose) ↔ 1 (open, aimed at
`MawTarget`) from `LightFauna.IsActivelyHunting` — the mouth yawns open in
0.6s entering a hunt window, eases shut in 1.8s on rest/wither. The rig
already evaluated every frame, so the only added cost is one float compare
per frame while settled.

**Two consume models coexist (merge reconciliation, read before touching either
`LightFauna` or `Boid`).** bleeding-edge landed a frame-paced *grazing queue*
(`maxConsumesPerFrame` / `_pendingMeals` / `EatPrism` / `DrainPendingMeals`)
that spreads a consume/damage cascade across frames so a dense cluster melts
instead of popping. The intentional-feeding model above (approach → face →
bounded mouthful → hold) is already frame-bounded by `maxClusterBites` + the
facing hold, so the two would double-drive consumption if both ran. Resolution:
- **`LightFauna` (brittlestar + shark)** uses intentional feeding / mouth-devour
  ONLY — the grazing queue is intentionally absent here (a brittlestar eats
  mouthfuls; a shark devours at the mouth). Do not re-add `_pendingMeals` to
  `LightFauna`.
- **`Boid` (tadpole forager)** uses intentional feeding for the FORAGER path;
  the paced queue survives ONLY on the non-forager **drone** combat path (its
  `Damage` cascade can hit many prisms at once and still wants pacing).
- Shared bleeding-edge wins kept on both: `HealthPrism.ResolveOwnerFauna`
  stamping (no per-neighbor `GetComponentInParent`), cached attribution
  strings, the elemental-contract crystal + `FaunaVariantTuning` (which is why
  the tadpole's `trailBlockInteractionRadius = 20` lives in the Blob tadpole
  config's `Variant`, not just the prefab).

---

## 8. Build order

1. ✅ Map + agree the redesign and the §6 bound.
2. ✅ Spawn rewrite in `RandomLifeSpawner`: timer-only, fixed period, fixed
   population N, `ControllingDomain`; `CurrentFaunaSpawnPeriod` simplified to base
   period. (`IntensityWiseLifeSpawner` left as-is — it is STILL USED by the
   WildlifeBlitz + Tournament scenes via `cellTypeChoiceOptions: 1`; do NOT delete.)
3. ✅ §6 bound = option C: opposing-mass prey signal (now `OpposingVolume`) + `FaunaFoodFloor`
   production gate + `starvationSeconds` despawn. Config on `SpawnProfileSO` /
   `FaunaConfigurationSO` / `Fauna`.
4. ⏳ Validate in Menu_Main: Jade fauna appear when Jade controls; populations
   appear, hunt, and thin out as prey runs low; ring sweeps at the fixed period.
   Tune the §6 knobs.
5. ✅/⏳ Predator/herbivore diet split — **code landed** (`FaunaDiet` + `Fauna.diet`
   + `LightFauna` consume branch + `Fauna.Predated`); two-tier starvation =
   Lotka–Volterra. Remaining: author a predator prefab/config and wire it into a
   `SpawnProfileSO`, then tune in-editor (§7.1).
6. ✅ Retire the regrowth pulse AND the flora phase-gated self-limit — **done**
   (`FloraGrowingEnabled = FloraPlantingEnabled = phase < Frenzy`; steady growth +
   planting until frenzy). This collapsed the phase ladder 6→3 (Calm/Restless/Frenzy).

---

## 9. Key files

| Concern | File |
|---|---|
| Prism count, phase, gates, aggression, controlling domain, live-fauna registry (`LiveFauna`/`GetLiveFaunaCount`/`GetLiveHerbivoreCount`) | `Assets/_Scripts/Controller/Environment/Cell.cs` |
| Phase thresholds + hysteresis | `Assets/_Scripts/.../CellPhaseRules.cs`, `CellPhase` enum, Blob Cell Config asset |
| Spawner all scenes run | `Assets/_Scripts/Controller/Environment/RandomLifeSpawner.cs` |
| Regulated spawner — USED by WildlifeBlitz + Tournament (`cellTypeChoiceOptions: 1`) | `Assets/_Scripts/Controller/Environment/IntensityWiseLifeSpawner.cs` |
| Spawn helpers (`SpawnFaunaWithDomain`, `PickRandomDomain`) | `Assets/_Scripts/Controller/Environment/CellLifeSpawnerBase.cs` |
| Fauna base: domain, goal, diet, starvation, `Predated`, lineage + reproduction (`AssignLineage`/`NotifyFed`→`TryReproduce`) | `Assets/_Scripts/Controller/Environment/FloraAndFauna/Fauna.cs` |
| Reproduction + seeding gating (pure, tested) | `Assets/_Scripts/Utility/DataContainers/FaunaReproductionRules.cs` |
| Creature behavior + diet-branched consume (herbivore prisms / predator fauna) | `Assets/_Scripts/Controller/Environment/FloraAndFauna/LightFauna.cs` |
| Diet enum (Herbivore / Predator) | `Assets/_Scripts/Data/Enums/FaunaDiet.cs` |
| Flora plant + growth gate (now just `phase < Frenzy`) | `AssembledFlora.cs`, `BranchingFlora.cs`, `Cell.FloraGrowingEnabled` / `FloraPlantingEnabled` |
| Spawn tuning (seed period/floor, food floor) + per-species reproduction knobs | `SpawnProfileSO.cs`, `FaunaConfigurationSO.cs` |
| Aggression enum + tier behaviors | `Assets/_Scripts/Data/Enums/CellAggressionLevel.cs` |
| Indicator (hex gauge + spawn ring, no numbers) | `Assets/_Scripts/UI/DomainVolumeIndicator.cs` |
| Headless perf+ecology tuner (no Unity) | `Tools/ecosim/ecosim.py` (+ `calibration.csv`, `README.md`) — see §12 |
| In-Unity perf probe (emits calibration samples) | `Assets/_Scripts/Controller/Environment/EcosystemPerfProbe.cs` |

---

## 10. Roadmap

### Phase 1 — stabilize & ship to bleeding-edge

Goal: everything on `keen-newton` is correct, safe across **all** scenes (every
scene runs `RandomLifeSpawner`, so these changes are global), and mergeable so the
work is saved and Phase 2 can be picked up.

1. **In-editor validation (the gate — needs a human; I can't run Unity).**
   - *Menu_Main:* dense flora, flora visibly resume growing in pulses, fauna spawn
     in the controlling color (Jade appears when Jade leads), hunt, and thin out as
     prey runs low; spawn ring sweeps; **no numeric readout**.
   - *One gameplay scene* (e.g. `MinigameWildlifeBlitz` / `MinigameHexRace`): confirm
     the prey-linked fauna + flora regrowth pulse don't break gameplay — fauna
     still appear, nothing runs away, framerate holds.
2. **Perf pass** at the new menu density (~4200 prisms steady). If it dips on a
   target device, lower `Blob Cell Config` `FrenzyEnter` (one asset).
3. **`IntensityWiseLifeSpawner` is LIVE, not dead** (earlier notes were wrong).
   The WildlifeBlitz + Tournament scenes select it (`cellTypeChoiceOptions: 1`);
   Menu/Skim Race/etc. use `Random`. **Do NOT delete it.** It has diverged from
   `RandomLifeSpawner` (no prey-linked `FaunaFoodFloor` gate, spawns 1/tick not a
   population), so if those scenes ever wire fauna into their `SupportedFaunas`
   they'll behave differently — reconcile the two spawners then, don't remove one.
4. **Confirm the global defaults are wanted in gameplay**, not just the menu:
   steady-until-frenzy flora growth (`phase < Frenzy`) and prey-linked fauna
   (controlling-color + starvation) now apply to every biome. The steady-growth
   change makes every cell fill **denser** (flora grow to `FrenzyEnter` instead of
   stopping at the old mid-range cap) — for WildlifeBlitz that's growth to 15000 vs
   the old ~10000. If a gameplay biome wants a lower ceiling, lower its
   `FrenzyEnter` (one asset). No per-biome "old hard-freeze" switch exists — the
   model is uniform now.
5. **Merge `keen-newton` → bleeding-edge.** Needs an explicit go-ahead (different
   branch); I can open the PR and write the summary on request.

### Phase 2 — toward the ultimate dynamic, emergent ecosystem

North star: a *small* set of fundamentals (Domain, Mass/prisms, Cells, Flora &
Fauna, Elementals, Vessels) whose interactions produce rich, self-balancing,
surprising behavior — and progressively **retire the scaffolding** (the regrowth
pulse, the fixed-period spawner) as real emergent forces replace them. Ordered
highest-impact / lowest-risk first; each step ships independently and composes
with the others.

1. **~~Prism mortality / decay~~ — REJECTED (see §0).** The original plan was
   that flora prisms age and die so the count falls on its own, retiring the
   regrowth-pulse cheat. **This is itself a cheat** and is not the path: mass is
   conserved, prisms are removed only by active forces (vessel abilities + fauna
   consumption). The real down-force on a dominant accumulation is the **food
   web** — opposing-domain fauna grazing it, or, failing that, fauna starving (a
   crash on the predator side, not a vanish on the prism side). So the work here
   is **not** to add a culler; it is to make the food web strong enough that
   accumulations get eaten — and to **retire the regrowth pulse outright**,
   accepting that a cell with no active force on it stays full (a valid state).
   That makes step 2 (predator/herbivore) the real first lever.

2. **Predator / herbivore split** — *the centerpiece, and the actual first step.*
   Sub-type on `FaunaConfigurationSO` (Herbivore / Predator). "Diet = what counts
   as prey" parameterizes the existing `Fauna.ResolveGoal`/consume + starvation
   hooks: herbivores eat flora prisms, predators eat herbivore fauna. Two-tier
   starvation → genuine Lotka–Volterra oscillation (flora→herbivores→predators→…).

3. **Fauna reproduction — ✅ LANDED (see §6.1).** Well-fed fauna reproduce
   (`FeedsPerOffspring` etc. on `FaunaConfigurationSO`); the spawner is demoted to
   a *seeder* that only tops a species up to its seed floor. Population is a true
   function of the food web; `FaunaReproductionRules` + edit-mode tests pin the
   gating. The 3-tier Blob web (flora → tadpole/brittlestar → shark) is authored.

4. **Elemental integration** — *ties the ecology to gameplay.*
   Flora/fauna express their effects through **Elementals** (Charge/Mass/Space/
   Time) rather than bespoke buffs: a domain's flora buff its vessels, fauna debuff
   opposing mass. Vessels start to *feel* the ecosystem. Composes with Domain,
   Vessels, Elementals.

5. **Domain territory dynamics.**
   As fauna cull opposing prisms and flora regrow, a cell's controlling domain
   shifts over time, and flora/fauna domains follow — visible territorial ebb and
   flow instead of monoculture lock-in.

6. **Flora succession & variety.**
   Different flora favor different phases (pioneers at Calm, canopy at Restless),
   so a maturing cell visibly changes character.

7. **Cross-cell ecology (migration).**
   Fauna migrate to adjacent cells chasing prey; crowded/empty cells rebalance —
   isolated cells become one connected biome.

**Scaffolding cheat scorecard:** the flora **regrowth pulse**, the flora
**phase-gated self-limit**, and the **fixed-period spawner as population driver**
are all **retired** (§0/§5/§6.1). What deliberately remains is the **seeder** — the
same timer, demoted to topping a species up to its seed floor (bootstrap +
extinction recovery). It is acknowledged residual scaffolding, kept because a food
web with no immigration makes extinction permanent and a dead scene is worse than a
small non-emergent floor; revisit if cross-cell migration (step 7) ever provides a
real immigration force. **Note:** none of these retirements is replaced by prism
decay (rejected, §0) — mass is conserved, and a cell only comes back down when an
active force (fauna grazing / vessel abilities) eats its mass.

---

## 11. Authoring a new ecosystem — config-completeness checklist

The platform goal (kickoff): **a new ecosystem = new config assets + a Cell in a
scene, with zero new C#.** This section is the audit of how close we are and the
exact recipe. As of the 3-phase collapse, standing up a biome is fully data-driven.

### What defines an ecosystem (the assets — no code)

| Layer | Asset | Authors |
|---|---|---|
| **Biome** | `CellConfigDataSO` | membrane/nucleus/cytoplasm prefabs, `CellModifiers`, the `SpawnProfile` ref, `SenseRadiusOverride` (grid coverage vs. visual membrane), and the **2 phase thresholds** `PhaseThresholds` (`RestlessEnter/Exit`, `FrenzyEnter/Exit`) |
| **Food web roster + cadence** | `SpawnProfileSO` | `SupportedFloras[]`, `SupportedFaunas[]`, `BaseFaunaSpawnTime` (fixed spawn period), `FaunaFoodFloor` (prey floor for production), initial delays/intervals, `FloraExcludeLocalDomain` |
| **Flora species** (1 per type) | `FloraConfigurationSO` | `FloraPrefab`, `SpawnProbability`, `InitialSpawnCount`, plant-period override |
| **Fauna species** (1 per type) | `FaunaConfigurationSO` | `FaunaPrefab`, `PopulationSize` (seed floor), `InitialSpawnCount`, `SpawnProbability`, and the reproduction knobs: `FeedsPerOffspring` (0 = off), `OffspringPerBirth`, `ReproductionCooldownSeconds`, `MaxLivePopulation` (perf cap) |
| **Per-creature tuning** | the flora/fauna **prefabs** | diet (`FaunaDiet`), `starvationSeconds`, `predationImmunitySeconds`, `forager` (Boid), consume/detection radii (`LightFaunaDataSO`), aggression-curve multipliers, body `HealthPrism`s |

**Recipe for a brand-new biome (zero code):**
1. Author the creature/flora **prefabs** (or reuse Tadpole / Brittlestar / Shark /
   the gyroid floras), each carrying its own diet + starvation + radii.
2. Create one `FaunaConfigurationSO` per fauna species and one
   `FloraConfigurationSO` per flora species, pointing at the prefabs.
3. Create a `SpawnProfileSO` listing those configs + the cadence/floor knobs.
4. Create a `CellConfigDataSO`: visuals + `SpawnProfile` + `SenseRadiusOverride` +
   the two `PhaseThresholds` (`RestlessEnter` = where fauna start hunting,
   `FrenzyEnter` = flora freeze + max aggression).
5. Drop a `Cell` into the scene, add the `CellConfigDataSO` to its `CellConfigs`
   list, leave `cellTypeChoiceOptions = Random`. Done — no C#.

> **Proof-of-platform validation (needs the editor):** author a *third* biome from
> assets only (no code) and confirm it spawns, phases, and breathes. The two test
> biomes (Blob, Skim Race) and the WildlifeBlitz cells already exercise this path.

### Still hardcoded (flagged for future lift — NOT yet "author-only")

These don't block a new biome but are coded constants a future biome can't retune
from assets. Lift them onto a config SO when a biome actually needs to vary them:

- **Aggression curves** are **static arrays** shared by all fauna:
  `LightFauna.CadenceByAggression / ConsumeRadiusByAggression / SpeedByAggression`
  and the `IntensityWiseLifeSpawner.FaunaSpawnIntervalByAggression`. A biome can't
  make its fauna ramp differently. Lift to `FaunaConfigurationSO` (per species) or
  `SpawnProfileSO` (per biome) when needed.
- **`RandomLifeSpawner.FaunaSpawnJitter` (150)** and **`Fauna.OffspringSpawnJitter`
  (25)** — spawn/birth spread radii. Consts; could be `SpawnProfileSO` /
  `FaunaConfigurationSO` fields.
- **`Boid.forager`** is a prefab bool, so the *same* prefab can't be a forager in
  one biome and a drone in another. Authorable per-prefab today; lift to
  `FaunaConfigurationSO` only if a biome needs the dual role.
- **Two spawners** (`RandomLifeSpawner` vs `IntensityWiseLifeSpawner`) selected by
  the Cell's `cellTypeChoiceOptions`. They have diverged (only Random is
  prey-linked). Unify behind config (roadmap / kickoff item 2) so behavior is
  data-selected, not spawner-class-selected.
- **Diet is a 2-value enum** (`Herbivore` / `Predator`). "What counts as prey" is
  not yet a composable selector (by domain relationship / prism provenance / target
  species), so arbitrary multi-tier food webs still need the enum's two branches.
  Generalize on `FaunaConfigurationSO` (kickoff item 3) to unlock "countless webs."

The domain set ({Jade, Ruby, Gold} + the Blue wildcard) is intentionally fixed —
Domain is a fundamental, not a per-biome knob.

---

## 12. Performance & the headless tuning loop

The ecology's cost is dominated by two terms, and a config can drive the menu from
5 fps to 60+ by moving them. This section is the model, the fix, and the loop that
lets the *agent* tune perf without a human watching the profiler.

### What costs frames

1. **Prism count (per-frame ceiling).** Each prism is a full GameObject (renderer +
   collider + MonoBehaviours). Because flora grow steadily to `FrenzyEnter` (§0) and
   the cell pins there (fauna rarely out-graze flora, §6), **`FrenzyEnter` ≈ the
   steady-state prism count**, and that count sets a hard per-frame ceiling
   regardless of anything else.
2. **Fauna `OverlapSphere` queries (fixed-rate).** Every fauna runs a
   `Physics.OverlapSphere(detectionRadius)` each behavior tick, touching the prism
   colliders in its radius. Cost ≈ `Σ_species (count/period)·prisms·(radius/cellR)³`.
   It scales with fauna **count**, with prism count, and with **radius cubed** — and
   because fauna seek dense regions they sample local (not mean) density. At the old
   menu steady state (5400 prisms, ~90 fauna, 70 m radii) this term alone ate ~70 %
   of the frame budget — that was the 5 fps.

### The three levers (highest leverage first)

| Lever | Where | Effect |
|---|---|---|
| **Prism ceiling** `FrenzyEnter` | `CellConfigDataSO.PhaseThresholds` | Linear on the per-frame ceiling **and** on every fauna's collider count. The big one. |
| **Fauna caps** `MaxLivePopulation` / `PopulationSize` | `FaunaConfigurationSO` | Linear on overlap cost. Per-species, per-biome. |
| **Query radius** `detectionRadius` (LightFauna data SO), `cohesionRadius` (Boid) | prefab/data SO | **Cubic** on overlap — but SHARED across scenes, so a riskier global lever. |

### Menu fix (shipped)

`Blob Cell Config` `FrenzyEnter 5400 → 1200`, `RestlessEnter 3000 → 700`; fauna caps
cut hard — first for perf, then further for **taming** (§6.2) when the first cut
still over-grazed the gyroids: tadpole 60→6, brittlestar 24→5, shark 5→2 (summed
herbivore cap 11, held below the flora's food-supported count so the gyroids hold
sizable). Menu-scoped (Blob assets only — zero behavior / shared-data-SO risk).
Modeled steady state: ~1200 prisms, ~13 fauna → predicted **~70 fps** (was ~5), with
the gyroids **TAMED** (held ~950–1200, not stripped). `MaxLivePopulation` does
double duty here: the §6.2 taming dial *and* the per-frame `OverlapSphere` budget.

### Consume pacing (shipped — Boid + LightFauna)

`ReproductionCooldownSeconds` throttles the BIRTH side of a population burst; the
same idea applied to the CONSUMPTION side is the `_pendingMeals` queue +
`maxConsumesPerFrame` (serialized, default 8, ≤0 = legacy unpaced burst) on **Boid**
and **LightFauna**. The behavior tick still *finds* every edible prism in range —
it just *enqueues* them, and a per-frame drain executes the consumes, re-checking
the scan's edibility predicate at drain time (destroyed / shielded / domain-stolen
/ owner-died can all change inside the pacing window; uneaten queued meals on the
eater's death stay in the world — mass conserved; only ACTUAL consumes spend the
frame budget, so stale entries never throttle real grazing). **This is pacing,
NOT a grazing cap**: nothing decided is ever lost — Boid's slow tick (~1.5 s)
drains its whole queue between ticks, and LightFauna (which can tick every few
frames at Frenzy cadence) REBUILDS its queue from the live scan each tick, so
anything undrained is simply re-found. Grazing throughput — the food web's
population regulator — is unchanged.
The win is that each consume's death cascade (implosion VFX, pool churn, spindle
teardown, cell volume updates) lands spread across frames instead of 15+ in one
(the measured 13.8 ms LightFauna tick): a dense cluster visibly *melts* instead of
popping in a single frame — which also reads better under the continuity law. Do
not mistake the queue for a consumption limiter, and do not "fix" a slow-looking
graze by raising `maxConsumesPerFrame` before checking whether prey density simply
dropped.

### The closed loop (agent-runnable, no Unity)

There is no Unity or C# toolchain in the autonomy container, so perf is tuned
through a headless model:

- **`Tools/ecosim/ecosim.py`** — reads the real config assets, models the heavy
  steady state, and estimates FPS from a cost model (the two terms above) calibrated
  to one real measurement. Prints per-lever sensitivity + named candidate configs.
  Run: `python3 Tools/ecosim/ecosim.py`. See `Tools/ecosim/README.md`.
- **`EcosystemPerfProbe`** (`_Scripts/Controller/Environment/EcosystemPerfProbe.cs`)
  — drop on a Menu_Main GameObject (or set the `ECOSIM_PROBE` define to auto-spawn).
  Logs `[ECOSIM] prisms=… fauna=… fps=…` from the live `Cell` registry. Read-only,
  never ships unless added.

The loop: **edit Blob config → `python3 ecosim.py` (predicted fps + levers) → human
plays the menu → paste the probe's steady-state line into
`Tools/ecosim/calibration.csv` → ecosim recalibrates to the real numbers → repeat.**
The model is a lever-ranker, not an oracle (one calibration point + documented
priors); each real sample tightens its prism-vs-overlap cost split.

### Structural wins still on the table (not yet done — need in-editor validation)

- **Decouple detection from consume radius**, or shrink `detectionRadius` toward
  `consumeRadius` (the consume loop only needs colliders within consume range). A
  brittlestar `detectionRadius 70→45` cuts its overlap ~3.7× with little behavior
  change — but it is a shared data SO (affects every scene's brittlestar), so it
  wants a real before/after capture.
- **Stop OverlapSphere-ing prisms for grazing at all.** Fauna already seek the
  densest region via the (Burst) density grid; grazing could be driven from the cell
  instead of a per-fauna physics query. Bigger refactor; the highest ceiling-raiser
  if the food web is ever to be dense AND cheap.

---

## 13. Nucleus control zone & the voracious exterior (July 2026 redesign)

**The volume tracking split (prompter-directed change to the control fundamental).**
A cell with a nucleus now has TWO spatial volume regimes, measured in the same
0.25s `EnsureVolumeFresh` pass ("volume is the spine" — unchanged measure, new
spatial split):

| Region | Role | Who removes mass here |
|---|---|---|
| **Inside the nucleus** (world radius from the nucleus renderer bounds) | **Node control.** `Cell.DominantDomain` = leader by per-domain ENVIRONMENT volume (trail + flora; fauna bodies excluded) inside the nucleus. This is the territorial claim — fauna neither target nor consume it. | Players only (vessel abilities; out-laying the standing claim) |
| **Outside the nucleus** | **The feeding ground.** Voraciously edible: herbivores graze it REGARDLESS of domain (extends the Boid forager's any-domain grazing to all herbivores), at every phase (even Calm fauna hunt the densest sensed exterior region), and the targeting grids only ever hold exterior mass. | Fauna consumption + vessel abilities |

Cells **without** a nucleus keep every legacy behavior (whole-cell control,
opposing-domain diet) — the split activates only where a `NucleusPrefab` exists.

**What this does and does not touch:**

- **Mass is conserved — unchanged.** No decay was added anywhere; the exterior is
  eaten faster because *more of it counts as prey*, not because anything ages out.
  The nucleus sanctuary removes a sink (fauna) from interior mass — accumulation
  there is a valid, player-contested state.
- **No domain asymmetry — unchanged on the spawn side.** Fauna still spawn only in
  the controlling color. The *diet* is now spatial rather than domain-keyed in
  nucleus cells: outside = everything, inside = nothing. (Precedents: Boid foragers
  were already domain-blind; Frenzy-phase seeking was already any-domain.)
- **Territorial permanence — re-seated on the nucleus.** "Take a cell, leave, it
  stays yours" now means the *nucleus claim* is permanent against fauna. Exterior
  canopy/trail is explicitly contested churn — by design, that is what makes the
  30s wave cycle readable.
- **The prey signal follows the diet.** `Cell.OpposingVolume(domain)` returns ALL
  exterior environment volume in nucleus cells (it is what a herbivore can actually
  eat), legacy opposing-domain volume otherwise.
- **Fauna spawn cadence: 30s platform-wide.** `SpawnProfileSO.BaseFaunaSpawnTime`
  default and every authored profile now tick at 30s — the ecosystem heartbeat that
  Brood Rush scores on (`Assets/_Scripts/Controller/Arcade/NUCLEUSRUSH.md`).
- **The wave event.** `RandomLifeSpawner` raises
  `CellRuntimeDataSO.OnFaunaWaveSpawned` (SOAP, `FaunaWaveData{cellId, domain,
  spawnedCount, nucleusControlled}`) once per species loop per tick. Wave-scored
  modes author ONE fauna species. `SpawnProfileSO.SeedFullWaveEveryTick` switches
  the tick from deficit-seeding to a full fresh wave (cap-clamped) so every cycle
  visibly births a brood; population remains starvation/cap-bounded.
- **Collider budget.** Zero new colliders/physics queries; nucleus checks are O(1)
  squared-distance tests inside existing loops; targeting grids shrink (interior
  mass excluded).

**Client sync note.** `CellNetworkSync` now PINS the client Cell's `DominantDomain`
to the server's replicated value (`Cell.SetReplicatedDominantDomain`), so fauna
spawn color — and anything scoring off node control — can't drift from the server
on connected clients.

**Menu/Blob and other nucleus biomes inherit the split**: flora that plant at the
cell centre now hold the nucleus claim (fauna can't graze the core), and exterior
gyroid fringes are grazed domain-blind. If a biome's equilibrium shifts too far
toward stripped exteriors, the levers are the same as §6.2 (per-species caps,
reproduction knobs) — never decay.

## 14. Super-shielded structure binds volume-only (July 2026, Astro League edge lining)

Astro League lines its court edges with **super-shielded (fully invulnerable) neutral
prisms** — permanent structure no active force can consume (`Prism.Damage`/`Consume`
no-op on super-shielded mass; ways to break it may come later). That surfaced a signal
question the fauna-body precedent already answered: mass that **cannot be contested**
must not drive the signals that are *about* contestable mass.

**The rule (in `PrismSpatialIndex.ComputeEnvironmentMass`, one classification for both
streams):** fauna bodies AND super-shielded prisms bind to their cell **VOLUME-ONLY** —
they feed `Cell.LiveVolume` ("volume is the spine": ALL prisms count, unchanged) but stay
out of the targeting grids, per-domain counts, `DominantDomain`/nucleus-claim reads and
the prey-volume signal. Fauna are never led to mass they cannot eat, and a permanent
neutral lining can never sway node control. Super-shield state is applied post-bloom, so
`PrismSpatialIndex.UpdateShieldState` re-files the cell classification on every
engage/disengage transition (a popped super shield returns the prism to ordinary
environment mass).

**The phase ladder keeps its pure measure — biomes budget for structure in config.**
Rather than carving structure out of `LiveVolume` (which would fork the spine's measure),
the biome that lays a known structural budget raises its `PhaseThresholds` volume fields
by exactly that budget. Astro League: lining = `edgePrismCount 240 × vol(2.5·2.5·10) =
15000`, so `Astro League Cell Config` runs Restless 15400/15300 and Frenzy 16500/16200
(gameplay headroom above the floor identical to the pre-lining 400/300 · 1500/1200).
Count backstops are untouched — volume-only mass never enters `LiveBlockCount`.

- **Mass is conserved — unchanged.** The lining blooms in via the standard pooled spawn
  and is only ever removed by the animated `Damage` teardown (arena rebuild); no decay.
- **No domain asymmetry.** The lining is `Domains.Blue` (the neutral-entity sentinel) and
  excluded from control reads — it cannot tint fauna spawns.
- **The super-shield state IS the stellated octahedron.**
  `PrismStateManager.ActivateSuperShield` engages `PrismStellatedOctahedronShield` (the
  Stella Octangula, the Skim Race track look) with the OPAQUE team material — the
  transparent super-shield material hid the stellation — and `DeactivateShields`
  disengages it, so the state machine stays the single reversible shield path
  (`PrismKinds` remarks updated). The component is added lazily on first engage; only
  super-shielded prisms pay its mesh cost.
- **Collider budget.** A super-shielded prism keeps its authored primitive `BoxCollider`
  trigger (the stellation is a look-only change; no convex `MeshCollider`, no convex cook),
  so it stays collider-LOD-reclaimable like any other prism — the earlier always-on-MeshCollider
  budget line is gone. The `AstroLeagueSettingsSO.edgePrismCount` cap (240) still bounds the
  lining as a spawn count, not a collider-cost floor; zero new physics queries (the ball resolves
  prisms via `PrismSpatialIndex.QuerySphere` and skips super-shielded entirely). Collision is at
  authored box size for now; shape-precise (stellated) collision is the planned three-LOD follow-up.

---

## 15. Herbivore spawn rotation + the shielded-mass diet rule (July 2026, Lobby observation)

Two independent defects observed in `Menu_Main` (Blob Cell) and Skim Race. Both are
targeting bugs, not population bugs — neither changes how many creatures live, only
*where* they hatch and *what* they will bite.

### 15.1 The herbivore ring rotated on spawns, not on the clock

**Symptom.** Every brittlestar and tadpole in the Lobby hatched at the *same* one of
the Blob profile's three ring points, so the whole species — and the elemental crystals
its creatures eventually drop — piled into one patch of a cell whose ring was authored
to spread them over three.

**Cause.** `RandomLifeSpawner` advanced `_herbivoreSpawnPointIndex` inside
`SpawnFaunaPopulation`, i.e. **once per wave that actually hatched**. The tick is a
SEEDER (§6): it only spawns while a species is under its `PopulationSize` floor, and
reproduction holds a fed population at its `MaxLivePopulation` cap. So after the
bootstrap wave the index could sit on one point for an entire session — every later
seed pinned to the same spot.

**Fix.** The ring is now a pure function of the **wave number on the fixed
`BaseFaunaSpawnTime` clock** (`RandomLifeSpawner.HerbivoreSpawnPoint(host, profile,
wave)`); the per-spawn index is gone. Each species loop carries its own `wave` counter,
and because `StartFaunaLoops` starts every loop in the same frame with the same
`InitialFaunaSpawnWaitTime` + period, all herbivore species agree on the wave number —
so a wave's species share a point and the point steps once per period. A 3-point ring at
30s therefore walks all three points in 90s and repeats, whether or not any given tick
had a deficit to fill. (The predator ring is unchanged: it still alternates per spawn,
which is its authored "solitary predators alternate poles" behavior.)

**Not changed — deliberately.** The ring says *where* a wave lands; the food web still
says *whether* one hatches. A tick that finds the species at its cap hatches nothing, at
any point on the ring. Guaranteeing a brood every 30s regardless of population would
need either uncapped spawning or imposed death, and imposed death is locked out. If a
deployment wants a visible brood on every tick, that is the authored pair
`SpawnProfileSO.SeedFullWaveEveryTick` (the Brood Rush wave mode) + enough
`FaunaConfigurationSO.MaxLivePopulation` headroom to hold it — a population/collider
decision, made per profile.

**Blob now runs full-wave.** `Blob Cell Spawn Profile` carries
`SeedFullWaveEveryTick: 1` so every 30s tick hatches a brood at that wave's ring point
(still clamped by each species' `MaxLivePopulation` — a tick with the species at cap
hatches nothing). The profile is shared by `Menu_Main` **and** `BenchmarkStressTest`, so
the benchmark now runs a fuller average fauna population; re-baseline before reading it
against older numbers (`Docs/PERFORMANCE_OPTIMIZATION.md`).

### 15.2 Shielded mass is not food for any herbivore

**Symptom.** Brittlestars in Skim Race parked on the super-shielded track prisms and
never moved on.

**Cause.** `LightFauna.IsEdibleForHerbivore` tested domain and nucleus but not shield
state. `Prism.Consume` is a **no-op on a super-shielded prism** (fully invulnerable) and
only sheds the shield on a shielded one, so a brittlestar that adopted a track prism as
its feed target approached it, faced it, "ate" it (incrementing `bites` and calling
`NotifyFed()` on a bite that removed nothing), held facing for `consumeHoldSeconds`, then
re-acquired the *same* prism through `FindNearestEdibleInFeedingRange` — a feed-hold loop
it could never finish. `Boid`'s forager path already excluded shields; `LightFauna` never
did.

**Fix.** One canonical rule on the base — `Fauna.IsShieldedMass(Prism)` — routed through
by every herbivore edibility predicate (`LightFauna.IsEdibleForHerbivore`,
`Boid.IsEdibleForForager`, `Boid.EatPrism`), so a new grazer species cannot re-acquire the
bug. A shielded prism is now never adopted as a feed target and never selected by the
mouthful-chaining query, so the creature skips to the next normal prism on the same
behavior tick. Both bite sites re-check the predicate before consuming, which also closes
the case where a shield engages between selection and the bite.

**Consequence, and it is the correct one.** The Skim Race cleanup swarm still grazes what
it was there to graze — vessel trails and flora, all unshielded — it just no longer treats
the track as a meal. Where the *only* mass in reach is shielded, a herbivore now finds
nothing edible and starves on its normal clock, withering to a crystal per the sealed
`Fauna.Die` (mass conserved). That is the food web doing its job, not a regression: the
previous behavior only looked stable because the creature was fake-feeding — every bite on
an invulnerable prism reset its starvation clock and advanced its birth counter without
removing any mass, so a stuck brittlestar was also an immortal, still-reproducing one.

**Collider budget: unchanged.** Neither fix adds a collider, a physics query, or an index
query. 15.1 replaces an `int++` with an `int % n`; 15.2 adds two bool reads to predicates
that already ran per candidate prism, and strictly *reduces* work (shielded prisms drop out
before the `Cell.IsPreyForHerbivore` call, and stuck creatures stop re-running the
mouthful-chaining `QuerySphere` every `consumeHoldSeconds`).

### 15.3 The permanent steering stall §15.2 exposed

**Symptom.** After 15.2 shipped, brittlestars in Skim Race (intensity 3) still parked
against the super-shielded track — no longer feeding on it, just motionless beside it.

**Cause — two defects that only bite together.**

1. `LightFauna.UpdateBehavior` recomputes `Goal` every behavior tick and did so
   **without `GoalOrbitOffset`**, silently clobbering the offset `Fauna.ResolveGoal`
   applies on the goal coroutine. That offset is the anti-convergence term: without it
   every creature seeks the *identical* point (the crystal at Calm, a density centroid at
   Restless/Frenzy) and arrives exactly on it.
2. `Vector3.normalized` returns **zero** for a ~zero vector. On arrival `goalDirection`
   is zero; with no separation nearby (the track is plain prisms, which contribute none)
   the steering sum is zero, so `desiredDirection` is zero and `currentVelocity` is zeroed.
   A motionless creature then recomputes the *identical* zero from the identical position
   on every later tick — **the stall is permanent**, and `desiredRotation` freezes with it.

This predates 15.2 but was masked: the Skim Race crystal sits on the track, so an arriving
brittlestar always had a track prism as `_feedTarget`, and `goalDirection` was overwritten
with the direction to that prism — never zero. Removing shielded mass from the diet (15.2)
took the mask away and the latent stall surfaced.

**Fix.** `GoalOrbitOffset` is now `protected` on `Fauna` and documented as mandatory for any
subclass that recomputes `Goal` on its own cadence; `LightFauna` applies it at Calm/Restless
(and on the origin fallback), skipping it at Frenzy exactly as `ResolveGoal` does. On top of
that, both steering sites — `LightFauna.UpdateBehavior` and `Boid.CalculateBehavior`, plus
Boid's attached-target path — test the steering sum against `Fauna.DegenerateSteeringSqr`
and hold the last heading instead of publishing a zero direction. The offset makes arrival
stalls rare; the guard makes them non-permanent, so no future steering term can reintroduce
a frozen creature.

**Collider budget: unchanged.** One `sqrMagnitude` compare per behavior tick, replacing an
unconditional `normalized` (which computes the same magnitude anyway).
