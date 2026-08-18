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

> **AUTHORIZED EXCEPTION (2026-08-03): the Wanderway rolling tether.** The one
> sanctioned place trail mass is recycled. During a live `WanderwayRun` — and
> ONLY then — the local vessel's trail is held at a fixed length
> (`ConveyorConfig.TetherPrisms`, 100): as the vessel lays at the head, the
> oldest prism at the tail withers and returns to the pool it came from, and the
> return station rides that tail so the way home is always one tether-length
> behind you. This is mechanically the same thing as the reverted cap above, and
> it is here **by explicit sign-off**, for a reason the cap never had: the
> Wanderway is a *truly infinite runner*, and recycling everything is what buys
> an endless world at fixed memory. Turn around and your trail is there; fly on
> and a little flying lays a fresh path home.
>
> Its scope is the fence — do not widen it, and do not "fix" it by reverting:
>
> - **Live-run only.** `WanderwayRun.RollTether` is the sole caller of
>   `Trail.RemoveOldest`. Outside a run — everywhere else in freestyle, every game
>   mode, the menu lava lamp — the trail is untouched and §0 holds in full.
> - **No length limit on the trail itself.** `VesselPrismController` grew no
>   `maxTrailBlocks` field; nothing about laying a prism consults a cap. The run
>   reaches in from outside and only while it exists.
> - **Recycle, not decay.** Prisms go back to the pool the next lay draws from —
>   the same closed-stock idea as the belt, which is why memory is bounded.
> - **Continuity of existence is NOT waived** (a separate law): a retiring prism
>   withers on the GPU clock — one grow-clock re-stamp toward a near-zero scale,
>   the belt's own collapse (`Docs/PRISM_ANIMATION.md` §5 C8) — and returns to the
>   pool only once it has shrunk away. Nothing pops.
>
> Detail: `Docs/ToySystem/ARCHITECTURE.md` § "The run".

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

**Who actually spawns the 20.** A config authors ONE point of the matrix, so the live world
only showed the whole grid once cells were allowed to *spread* across it: `SpreadElements` +
`ElementPalette` (element) and `LifeformLevelSpread` (level, higher levels rarer) roll a
variant per spawn, and offspring inherit their parent's roll. See **§17** — that is where the
spread mechanism, its rarity curve, the palette rule and the per-cell settings live.

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
    (neither eats **shielded or super-shielded** mass — the shared rule, §16):
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
  to the wave clock, not to a spawn counter — see §16.**

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
| Domain fauna buff (living hearts empower their domain's vessels) — see §15 | `Assets/_Scripts/Controller/Environment/DomainFaunaBuffSystem.cs`, `Fauna.LiveHeart`, `ResourceSystem.SetFaunaBuffModifier` |

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
   Vessels, Elementals. **Fauna half LANDED (see §15):** every living fauna's
   embedded heart grants its elemental value to all vessels of its domain, revoked
   at death when the same heart drops as the collectible crystal. Flora hearts are
   the natural follow-up (same `LiveHeart`-style seam on `LifeForm`).

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

## 13.1 Sizing the control zone — a cell per core size (August 2026, Scurry)

The nucleus radius is not a tuning knob on the Cell; it is whatever the config's
`NucleusPrefab` measures. So **to change a cell's control-zone size you author a new
`CellConfigDataSO` pointing at a resized nucleus prefab** — never a scene override, a
`localScale` tweak on a shared prefab, or a scene-placed copy (see §13's radius source and
CLAUDE.md's "The Cell owns the environment" corollary).

Worked example — Crystal Capture ("Scurry") shared `Barren Cell Config` with five other
scenes, so its core was not its own to tune. It now has `Scurry Cell Config`, a clone of
Barren differing in exactly one reference: `NucleusPrefab` → `HalfNucleus.prefab`
(`localScale 200`, a flat copy of `Nucleus.prefab` following the `BigNucleus` /
`BrightNucleus` convention in that folder). Barren's `SpawnProfile` is deliberately shared —
the ecology is identical, only the core size differs.

| | Barren (400) | Scurry (200) |
|---|---|---|
| `NucleusWorldRadius` | 391.911 | **195.956** |
| node-control zone volume | 2.52e8 | **3.15e7** (×⅛) |
| crystal spawn ball | 391.911 | **195.956** (8× crystal density) |
| player spawn ring | 431.911 | **235.956** |
| membrane (`CapsuleMembrane`) | 1200 | 1200 (unchanged) |

All of these derive from `Node2.fbx`'s ~0.97977875u mesh half-extent — the same figure
behind the `const float NucleusR = 392f` that `SpawnableCaldera` / `SpawnableOurobor` lay
against (§18.1, §18.2). A *smaller* nucleus keeps their "lay nothing inside the control
radius" invariant satisfied with room to spare; a larger one would not, which is why
`Nucleus.prefab`'s 400 must not move.

**Invariants: none violated.** Node control still reads per-domain environment volume inside
the nucleus, the herbivore diet is still spatialized on `IsInsideNucleus` (interior sanctuary
/ exterior feeding ground), `HasNucleusControlZone` stays true, and a centred sphere stays
domain-neutral. It is a size tune, not a semantics change. Second-order: the territorial claim
and the fauna sanctuary both shrink to ⅛ volume, and the ⅞ of the old interior that is now
exterior becomes voraciously grazeable.

**Collider budget: zero delta** — `Nucleus.prefab` carries no collider. But note the
*density-grid* side, which is the real cost: `Cell.AddBlock` grid-registers a prism only when
it is OUTSIDE the nucleus, so mass in the freed shell now takes up to four
`BlockCountDensityGrid.AddBlock` calls it previously skipped.

**Reading the radius during the SPAWN CHAIN is a trap.** `CellRuntimeDataSO.Cell` is assigned
inside `Cell.Initialize`, which runs on `OnInitializeGame` behind `InitDelayMs` (1000 ms),
while vessels spawn at `preSpawnDelayMs` (200 ms) and AI at `OnNetworkSpawn` (t≈0). Both the
field and `NucleusWorldRadius` are empty then. Use `Cell.FindByRuntimeData` (static registry,
joined in `OnEnable`) and `Cell.ExpectedNucleusWorldRadius` (measures the config's prefab
asset, no instantiate) instead. This shipped wrong once: the player spawn ring silently fell
back to authored points 70.7u from the centre — inside the nucleus.

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

## 15. Domain fauna buff — living hearts empower their domain (July 2026, roadmap item 4 fauna half)

**The mechanic.** Every LIVING fauna's embedded elemental heart grants its element's value to
**all vessels of the fauna's domain**; the power is **lost the moment the fauna dies** — at
which point the very same heart drops as the collectible crystal (the locked wither-to-crystal
invariant). The economy this creates:

- **Kill + collect your own domain's fauna → net zero for you, pure loss for allies.** You
  re-earn exactly the buff you destroyed (crystal collect adds the same value to your base);
  every teammate who doesn't collect just loses it.
- **Kill an opposing domain's fauna → deny AND steal.** Their whole domain loses the buff, and
  the drop is domain-agnostic, so you can collect it for yourself.
- **Nourish your own fauna (Shepherd joust `LevelUp`) → grow your whole domain's buff** — the
  heart grows a level step, and the buff tracks the heart's live world scale.
- **Territorial stakes:** fauna spawn in the controlling color, so holding cells now feeds your
  domain standing elemental power — and wave kills strip it.

**Value symmetry is structural, not tuned.** Each living heart contributes
`SkimmerAdjustElementLevelByCrystalEffectSO.ComputeLevelGain(heart.lossyScale.x, …)` — the
exact collect formula, with the parameters read from the effect array wired on the heart's
**own** `ElementalCrystalImpactor` (the EXACT effects `AcceptImpactee` executes at collect
time; a heart whose drop cannot repay the value — no impactor, or no level effect wired —
grants **nothing**; multiple wired level effects are summed exactly as collection executes
them). The buff keys off **`Fauna.LiveHeart`**, which nulls at the precise
`ActivateCrystal()` moment inside the sealed `Fauna.Die` — so the buff ends exactly when the
crystal becomes collectible, with the same world scale carrying the same value on both sides
(`transform.parent = cell` preserves world scale on the drop, and `GrowCrystalWithPop` now
freezes if the heart is freed mid-level-up so the drop keeps its death-moment scale — a
mid-flare death drops at the pop's transient scale, bounded by the ×1.6 overshoot and the
per-crystal gain cap). **Zero
new tunables**: the existing knobs (`levelPerUnitScale`, `maxLevelGainPerCrystal`,
`CrystalScalePerLevel`, `InitialLevel`, per-species population caps) govern both the standing
buff and the pickup.

**Mechanism (SOAP-evented + reconcile sweep, no cheat).** `DomainFaunaBuffSystem`
(auto-created by the first `Cell.Initialize` via `EnsureExists` — so it exists wherever fauna
do, Menu_Main freestyle included; one HyperSea, one rule set) re-sums
`Cell.ActiveCellsSnapshot → cell.LiveFauna → fauna.LiveHeart` into per-domain, per-element
pools and applies them via `ResourceSystem.SetFaunaBuffModifier` on every `gameData.Players`
vessel of that domain. Two triggers share the one sweep:
`CellRuntimeDataSO.OnFaunaHeartsChanged` (raised by `Fauna.AssignLineage` and `Fauna.Die`
**through the host cell's runtime SO** — several fauna prefabs author their own `cellData`
wire null or dangling, so the per-prefab wire is only the hostless fallback)
lands spawn grants and death revocations **within a frame**, and the periodic reconcile sweep
(`updateInterval`, 1s) tracks heart growth, late-spawning vessels, vessel swaps (access is
hardened against the destroyed-but-referenced vessel window during a menu swap), and domain
re-picks (`player.Domain` read live). The fauna buff is a **dedicated composited layer** on
`ResourceSystem` (like the comeback layer, its own single writer) that never touches the
crystal-earned base, so revocation is exact — and it obeys the **maintained-mechanism law**
(`ResourceSystem.SustainedCeiling`): *no sustained mechanism holds an element above level 10;
the 10..15 overcharge band belongs to transients, and everything in it drains back to (at
most) 10.* Concretely: the held layer fills only the room between the base and level 10, and
the part of a pool INCREASE above that (a wave spawning into a saturated pool, a heart
growing) is converted by `SetFaunaBuffModifier` into a standard temporary elemental effect —
a felt spike up to the 15 clamp that drains at the elemental recovery rate, restoring the
headroom so the **next** wave is felt too. Base crystal overcharge already drains the same
way (`RecoverBaseLevels`) and comeback already fills-to-10, so after this every channel obeys
one law. Compositing is pure (`CompositeEffectiveLevel` + `HeldFaunaContribution` +
`ComputeUnfeltIncrease`) and pinned by `DomainFaunaBuffTests` (net-zero own-kill-collect
below and at saturation, exact revocation, sustained-cap, spike-rides-above, clamps). HUD:
petal bars animate automatically off `OnElementLevelChange` — one level-1 tadpole heart
(scale 1) = one petal tick for the whole domain, and each 30s wave at a saturated pool reads
as a petal surge that settles back to 10.

**Scope + caveats:**
- **Fauna only** for now; flora hearts are the follow-up seam (`LifeForm` would grow the same
  `LiveHeart` accessor; roadmap item 4's "flora buff its vessels").
- **Net-zero is exact at the moment of collection.** Over time the resting-band drift
  (`ResourceSystem.RecoverBaseLevels`) applies to the collected BASE value — overcharge above
  level 10 bleeds back to 10, deficits refill to 0 — and the held fauna layer sustains at
  most level 10 by the maintained-mechanism law, so neither side of the swap can park power
  in the overcharge band. Killing your own domain's fauna is **never profitable** —
  break-even at best. At a saturated pool the swap is absorbed by the buffer (the held fill
  re-balances around the collected gain; sustained level stays 10 on both sides), so
  stripping a saturated domain's standing power takes sustained overkill, not one pick.
- **Manager-spawned fauna** (`LightFaunaManager.SpawnGroup`, `BoidManager.SpawnBoids` — the
  dead scene-population paths wired through the removed `Cell.fauna2` field, §7) never enter
  `Cell.LiveFauna`, so they would drop collectibles without having granted a buff — acceptable
  while those paths stay dead; fold them into `AssignLineage` if they ever revive. Worm colony
  segments (§21): body segments carry no crystal (body parts, not lifeforms — no buff, no
  drop); head/tail capital segments DO carry and drop hearts but are not lineage-registered,
  so they grant no buff either — drop-without-buff is a deliberate §21 ruling (a kaiju must
  not destabilize the elemental economy), not the manager-fauna accident described above.
- **Client-local divergence:** fauna have no NetworkObject and element levels don't replicate,
  so peers can disagree on exact buff values — the same accepted divergence the fauna sim
  itself has (§7 caveat 4). Each client is self-consistent. Server-authoritative pools are the
  follow-up if the buff enters strict competitive modes.
- **Collider budget: zero.** No colliders, no physics queries — a 1 Hz walk of the existing
  registries (menu steady state: ~13 fauna, ≤4 players) plus event-driven re-sums on the 30s
  wave heartbeat and on deaths.

**In-editor verification (Menu_Main):** enter freestyle, watch your petal bars — they should
tick up as controlling-color fauna spawn (30s waves) and drop when fauna starve/are predated,
with the dropped crystal granting the lost amount back on collection. Once the domain's pool
is rich enough to hold level 10, each new wave should read as a **temporary surge above 10
that drains back to 10** (never a parked 11+); temporary effects (overtake buff, danger
debuff) still ride on top. Toggle `debugLogging` on the auto-created `DomainFaunaBuffSystem`
(on the Cell's GameObject) for per-player pool logs.

---

## 16. Herbivore spawn rotation + the shielded-mass diet rule (July 2026, Lobby observation)

Two independent defects observed in `Menu_Main` (Blob Cell) and Skim Race. Both are
targeting bugs, not population bugs — neither changes how many creatures live, only
*where* they hatch and *what* they will bite.

### 16.1 The herbivore ring rotated on spawns, not on the clock

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
`BaseFaunaSpawnTime` covers the whole ring in `N × BaseFaunaSpawnTime` and repeats,
whether or not any given tick had a deficit to fill (Blob: 3 points at 15s ⇒ a lap every
45s). (The predator ring is unchanged: it still alternates per spawn,
which is its authored "solitary predators alternate poles" behavior.)

**Not changed — deliberately.** The ring says *where* a wave lands; the food web still
says *whether* one hatches. A tick that finds the species at its cap hatches nothing, at
any point on the ring. Guaranteeing a brood every tick regardless of population would
need either uncapped spawning or imposed death, and imposed death is locked out. If a
deployment wants a visible brood on every tick, that is the authored pair
`SpawnProfileSO.SeedFullWaveEveryTick` (the Brood Rush wave mode) + enough
`FaunaConfigurationSO.MaxLivePopulation` headroom to hold it — a population/collider
decision, made per profile.

**Blob now runs full-wave.** `Blob Cell Spawn Profile` carries
`SeedFullWaveEveryTick: 1` so every wave tick hatches a brood at that wave's ring point
(still clamped by each species' `MaxLivePopulation` — a tick with the species at cap
hatches nothing). The profile is shared by `Menu_Main` **and** `BenchmarkStressTest`, so
the benchmark now runs a fuller average fauna population; re-baseline before reading it
against older numbers (`Docs/PERFORMANCE_OPTIMIZATION.md`).

### 16.2 Shielded mass is not food for any herbivore

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
query. 16.1 replaces an `int++` with an `int % n`; 16.2 adds two bool reads to predicates
that already ran per candidate prism, and strictly *reduces* work (shielded prisms drop out
before the `Cell.IsPreyForHerbivore` call, and stuck creatures stop re-running the
mouthful-chaining `QuerySphere` every `consumeHoldSeconds`).

### 16.3 The permanent steering stall §16.2 exposed

**Symptom.** After 16.2 shipped, brittlestars in Skim Race (intensity 3) still parked
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

This predates 16.2 but was masked: the Skim Race crystal sits on the track, so an arriving
brittlestar always had a track prism as `_feedTarget`, and `goalDirection` was overwritten
with the direction to that prism — never zero. Removing shielded mass from the diet (16.2)
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

---

## 17. Spawning the whole matrix — element × level spread (July 2026)

**What was wrong.** The element × level contract (§3) was fully implemented but almost
nothing used it: every cell config authored ONE element and `InitialLevel: 1`, so a
session only ever showed a few element variants at level 1. The 4 × 5 matrix existed in
code and in the Lifeform Matrix bench, and nowhere in the live world.

**The two halves spread differently, on purpose.**

- **Level is a pure scale curve** (body/leaf prisms + the heart crystal), so it spreads in
  code: `LifeformLevelSpread` (`MinLevel`/`MaxLevel`/`RarityFalloff`) on both config SOs.
  Higher levels are *rarer* — weight of level n is `falloff^-(n - min)`, so the default 2
  makes level 5 about 1 in 31. A level-5 kill drops the biggest crystal in the cell
  (`CrystalScalePerLevel` compounding, ≈2.07× at level 1's size), which is exactly the
  thing worth hunting for; making it common would have made it worth nothing.
- **Element is an identity**, not a tint: per the §3 variant inventory, an element is its
  leaf/body PRISM shape, growth tempo, shield cadence, starvation clock, flocking numbers
  and audio. So a config that spreads elements resolves each roll through an
  `ElementPalette` — the four canonical per-element assets in `_SO_Assets/Lifeforms` for
  that species — and applies **only** their `Element` + `Variant`. Population size, seed
  floor, reproduction, probability and planting cadence stay on the cell's own config, so
  a biome keeps its density tuning. An **empty palette** still rolls the element but keeps
  the cell's authored `Variant` — used by the score-tuned cells (Skim Race, Nucleus Rush,
  Astro League) whose swarms are tuned for a job (track cleanup, the wave clock) and must
  not inherit another element's graze radius or starvation clock.

**Heredity.** The roll happens once per spawn and is then **inherited**: `Fauna` remembers
its `LifeformVariantPick` (element + tuning + hatch level) and passes it to its offspring in
`AssignLineage`, so a lineage breeds true instead of re-rolling an identity every birth.
In-world level-ups (the Shepherd joust) are deliberately *not* inherited — acquired growth
is not heritable, which keeps selection endogenous rather than Lamarckian. This is the first
heritable trait to ride the reproduction path and is the natural seat for the P3 genome.

**Collider budget: unchanged — this is the reason the design is shaped this way.** Element
spread rolls *which* variant a creature is, never *how many* exist (counts stay on the cell
config, so the alternative — one config per element per cell — would have quadrupled the
population). Level spread changes scale only: the same prism count, the same one heart
collider per lifeform. Expected volume drift: mean level ≈1.94 at the default falloff, so
average body/leaf scale rises ~13% — `LiveVolume` reads a little heavier per creature, which
is worth a look during the phase-threshold retune (masterplan §2) but adds no colliders.

**Spread is off by default.** `SpreadElements` and `Levels.Enabled` are opt-in per config;
with both off the spawn path returns the authored `Element` / `Variant` / `InitialLevel`
exactly as before. Enabled in the shipped cells: Blob (and Rampage, which shares its
assets), Wildlife Blitz 1–4 with full palettes; Skim Race, Nucleus Rush and Astro League
with element-only spread. The Lifeform Matrix toy pins both off on its runtime clones — the
bench must spawn the exact variant its station shows.

**Verify in-editor.** Menu_Main (Blob Cell) is the fastest read: fly freestyle and watch a
few fauna waves. Expect mixed element crystals (all four crystal MODELS, not just recoloured
ones) inside a single species' brood, visibly mixed body sizes, and the occasional
conspicuously large creature. Confirm a level-5 creature drops a visibly larger crystal on
death, and that a brood born from reproduction matches its parent's element. Knobs:
`Levels.RarityFalloff` (2 → 1 for uniform, higher for rarer giants), `Levels.MaxLevel` to
cap a biome's size band, and `ElementPalette` to give a cell its own per-element tuning
instead of the canonical assets.

---

## 18. Prepopulated cell environments + the freestyle seven (July 2026)

A cell config can now carry an authored structural environment that spawns WITH the cell:
`CellConfigDataSO.EnvironmentPrefab` (any `SpawnableBase` prefab) + `EnvironmentIntensity`
(passed to `Spawn()`; fixed structures ignore it). `Cell.SpawnVisuals` calls the prefab
asset's `Spawn()` exactly the way `SegmentSpawner` does and parents the returned container
under the cell, so the environment lives and dies with it. Every prism flows through the
canonical `PrismTrailBuilder` lay path — big structures stream budgeted and bloom in
(continuity of existence holds), and the mass registers with the cell's volume/density
bookkeeping like any other prism.

**Prepopulation is a head start, not a parallel system.** The spawned mass is ordinary
conserved mass: herbivores graze it (voraciously outside the nucleus), players smash and
consume it, and nothing ever ages it out. A prepopulated garden slowly weathers into
whatever the food web makes of it — which is the point.

**Phase thresholds must ride the baseline.** The phase ladder reads TOTAL `LiveVolume`
(plus the count backstop), so a config that prepopulates hundreds of thousands of volume
must author `PhaseThresholds` above that baseline or the cell boots straight into Frenzy.
Each of the freestyle seven authors the Blob ladder's deltas (+700/+500/+3600/+3000 count,
+11.2k/+8k/+57.6k/+48k volume) on top of its own measured baseline (FrogletTools > Ecology >
Measure Cell Environment Baselines). Re-baselined 2026-08-02 post clock-material migration
(volume is final at spawn now) — e.g. Yggdra measures 34,340 prisms / 541,156 volume →
Restless at 552,356 / 549,156, Frenzy at 598,756 / 589,156, counts
35,040/34,840/37,940/37,340. As grazing wears the garden down the cell relaxes deeper
into Calm — an emergent "aging" of the biome with no clock anywhere.

**The Yggdra cell** (`_SO_Assets/Cell Configs/Yggdra Cell/Yggdra Cell Config.asset`) is
the first user: a Blob-family cell (same membrane/nucleus/cytoplasm/modifiers/spawn
profile) whose `EnvironmentPrefab` is `SpawnableYggdra` — the world-tree distilled from
the ~69k-prism Atlantis garden (which itself stays Scurry-intensity-4 exclusive; see
`CRYSTAL_CAPTURE.md`) at roughly half the weight for the freestyle rotation. Registered
in Menu_Main's freestyle Cell `CellConfigs` list (choice mode Random) alongside the rest
of the freestyle seven below. Danger thorn prisms ride along — the autopilot vessel can
clip one occasionally; that is the environment being real, not a bug.

**Collider budget:** the environment's plain/danger prisms ride the LOD-cullable
BoxCollider (active count bounded by `PrismColliderLodManager` radius, not population);
its 225 shielded/super-shielded landmarks carry always-on convex MeshColliders (~0.3%,
same ration as the Scurry arena). The Yggdra menu roll has NOT yet been device-profiled —
soak Menu_Main with the Yggdra config forced before shipping (see
`Docs/PERFORMANCE_OPTIMIZATION.md`); the Atlantis prefab's `density` knob (0.5–1.3) is
the fallback lever.

**Replay + re-init constraints.** `Cell.AssignConfig` is sticky per scene (first Initialize
pass rolls, repeat passes keep the roll) so the acknowledged double-Initialize path can never
re-label the cell while a prepopulated environment is streaming in. `ResetCell` (in-place
replay) neither destroys nor respawns the environment — environment-bearing configs are for
scene-lifetime cells only: use them in `UseSceneReloadForReplay` modes (all current ones) or
Menu_Main, not in-place-reset modes. **Every environment build is gated AND deferred past boot**: game scenes hold
their connecting screen over a quiescent, fully-loaded scene; gate-less scenes (Menu_Main
freestyle) wait until the scene reports ready (local player pair initialized, 12s deadline)
and then raise an `EnvironmentLoadVeil` - the connecting-screen status idiom, a gentler 80ms
lay slice so Netcode/Relay/audio keep breathing under the veil, released only when every
prism is laid and settled. Building DURING boot shared the engine's async budget with the
vessel-spawn chain, session setup, and audio banks - audio underruns plus a clone batch
wedged mid-integration (a 4s watchdog in PrismTrailBuilder.CloneBatchAsync now force-
completes any stalled batch as a second line of defense). The original design let the menu
world bloom in under live play at 8ms/frame; that built for minutes under gameplay
(clone-integration spikes + physics churn against a half-built world) and crashed the menu
reliably, so live blooming is retired - the 8ms ungated slice remains only as a last-resort
fallback.

**The freestyle seven (July–August 2026).** Atlantis (~69k) stays Scurry-intensity-4 exclusive;
the freestyle rotation runs at roughly half that weight per cell, split across seven
environments so the lava lamp deals a different world each load — Blob (empty baseline)
plus: **Yggdra** (the world-tree, distilled from Atlantis: trunk/roots/canopy/vines/kelp/
fireflies), **Daedala** (Atlantis's built half expanded into an Escher road-city: four ring
terraces, twin counter-chiral Möbius causeways, arches, aqueducts, minarets, lanterns),
**Orrery** (a celestial clock: sun shell, seven tilted orbit rings with planets and moons,
zodiac band, pendulums, a danger-tailed comet), **Zephyr** (a painted sky: braided wind
rivers, twin cyclones, cloud banks with one lightning thunderhead, Van Gogh sun/moon discs,
a swell sea), **Caldera** (the danger-led forge — see §18.1: four floating volcanic massifs
in tetrahedral symmetry around the nucleus, each aimed inward, with TRUE danger spillways/
curtains/crust rivers, basalt column collars, ember plumes, fumaroles, obsidian edge-arcs),
**Geode** (the angular, serene pole: a cracked crystal cathedral — husk hemispheres,
inward crystal linings, super-shielded druse tips, agate bands, dust, light shafts; zero
danger), and **Ourobor** (see §18.2 — the pastoral pole: three interlocked ULTRAWIDE Möbius
bands carrying rolling countryside with a cityscape on both faces; zero danger). All extend
`CellEnvironmentSpawnableBase` (one deterministic lay/stream/noise
contract, per-cell fixed seed); per-cell PhaseThresholds ride each baseline measured with
a bit-exact simulation of the C# noise (count/volume): Yggdra 34.3k/541k, Daedala
33.9k/638k, Orrery 34.6k/197k, Zephyr 36.1k/427k, Caldera 41.4k/1,211k, Geode 34.4k/561k,
Ourobor 37.9k/751k —
confirm in-engine via FrogletTools > Ecology > Measure Cell Environment Baselines before
retuning any ladder. Same soak-before-ship rule as §17 above; each prefab's
`density` knob (0.5-1.3) is the per-cell fallback lever.

**Follow-ups (cell environments).**
1. **Field-verify the deferred menu build** — the defer-past-boot + 80ms veil slice + clone
   watchdog fix shipped after the 10,496 freeze but has not yet been confirmed in-editor.
2. **Device soak per cell + Scurry Atlantis** — record steady-state numbers in
   `Docs/PERFORMANCE_OPTIMIZATION.md`; per-prefab `density` (0.5-1.3) is the fallback lever.
3. **Confirm simulated baselines in-engine** (FrogletTools > Ecology > Measure Cell Environment
   Baselines) and re-author any PhaseThresholds off by more than a few hundred count / few
   thousand volume.
4. ~~**Menu load-time UX call**~~ — **SHIPPED, see §19.** The veil hold *was* long for a menu,
   and it was paid on every entry (boot *and* every return from an arcade game). Resolved by
   the second option: Menu_Main now boots the environment-free config and the six worlds are
   opt-in through the **Cell Selector** toy.
5. **Danger tuning after playtests** — Caldera (1,503 danger prisms, 6.0% of its mass) is
   deliberately the spicy cell; tune per feel. The pre-rework build laid 858 (2.8%) despite this
   line long claiming ~1.6k; §18.1 lists the six dials that set it.
6. **Future archetypes** (diversity headroom before hybrids/dynamics take over): Abyss, Mycel,
   Hive, Glacier, Reliquary, Mesa.
7. **The garden archetype landed** — `SpawnableHesperides`, the cell whose world is the
   *planting* rather than the lay (~12k authored + ~21k grown). Different budgeting rule, so it
   has its own section: **§21**.

---

## 18.1 Caldera, de-gravitized — the tetrahedral forge (August 2026)

The shipped Caldera was a **landscape**: a `Base = -180f` slab plain with a 255-unit cone rising
out of it along +Y, a flat ash layer at one altitude, and a magma river meandering across the
floor. Every family keyed off a world-space ground plane and a world "up" — legible, but wrong for
something floating in a cell. Two measured consequences beyond the look:

- **89% of its mass (27,803 prisms / 371,602 volume) sat INSIDE the nucleus** (`Cell`'s
  node-control radius, ~392u — `Nucleus.prefab` localScale 400 × the Node mesh's ~0.98u radius).
  Per §13 the nucleus interior *is* the territorial claim, so the cell booted with node control
  pre-awarded to whatever colour the landscape happened to favour (Blue, 27k of it), and true-
  danger prisms sat inside the fauna sanctuary.
- The composition had no relationship to the nucleus at all — the cone simply engulfed it.

**The rework.** There is no ground plane and no world `up` anywhere in the file. Four volcanic
massifs hang at the vertices of a *roughly* regular tetrahedron (each axis nudged a few degrees off
true) around the nucleus, each aimed **inward**: broad shield base outward at the rim, crater mouth
facing the core. Every family is authored in a per-massif radial `Frame` (`Ax` outward radial, `U`/
`V` across it), so the cell's only "down" is the radial pull toward the nucleus — and the geometry
states it: spillways drain the flanks *inward* into the vent, the vent drips a molten curtain
*inward* across the gap, and the four curtains land on a shared magma crust riding the nucleus
shell (impact basins joined by great-circle rivers along the tetrahedron's edges). Six obsidian
knife-arcs span the same six edges, making the symmetry legible from inside the cell.

**The crust stays outside the nucleus by construction.** `CrustR = NucleusR + CrustClearance` and
`VentR = CrustR + FallDrop` — the nucleus radius is load-bearing, not a comment, so moving the
nucleus moves the whole composition. Measured minimum prism radius is **402.7** against the 392
control radius: **zero prisms inside the nucleus**, node control unclaimed at boot, no danger in
the sanctuary.

**The four are different creatures**, which is the point — silhouette, activity, palette, girth,
reach, basis roll, and chirality all vary per massif (`Specs`):

| # | silhouette | vent | stone / trim | girth × reach | notes |
|---|---|---|---|---|---|
| 0 | Shingled (plate rings) | Erupting | Blue / Ruby | 184 × 1.12 | the signature: molten mouth disc, 5-strand curtain |
| 1 | Terraced (stepped ziggurat) | Degassing | Gold / Blue | 152 × 0.90 | gas strands, 10 lip chimneys under shielded caps, no lava |
| 2 | Fluted (organ pipes on a groove floor) | Collapsed | Blue / Gold | 172 × 1.00 | wide (100u) sunken mouth, 5 secondary vents each with its own fall |
| 3 | Shattered (phyllotaxis glass plates) | Cooled | Ruby / Blue | 140 × 0.84 | frozen tongue, **super-shielded heart**, near-zero danger — the safe approach |

Massif proportions are `MassifLength 304 × Reach` long against a 44u vent mouth (100u on the
collapsed one) — a ~1.7:1 stratovolcano taper matching the old cone's. (An intermediate pass at
120 long × 106 girth measured squat enough to read as a blob rather than a mountain; a second pass
doubled the silhouette on request.)

**Doubling a massif is not doubling its numbers.** Two rules keep the 2× pass honest:

- **The silhouette scales; the FURNITURE does not.** A column bundle's two-ring gaps and a
  fumarole's chimneys are sized to the *vessel*, not to the mountain — doubling them turns the
  weave-through slalom into open air. So a bigger massif carries **more** bundles (8 → 14
  clusters) and **more** chimney clusters (4 → 7), never bigger ones.
- **`PlateDetail` (1.45) scales flank sampling spacing AND plate footprint by the same factor**,
  which holds surface *coverage* exactly constant (count × footprint / area = 1) while paying
  ~1.9× the prisms for 4× the area instead of 4×. Note the one family whose plate count is
  explicit rather than spacing-derived (the shattered flank) must have its count set to
  `base × 4 / PlateDetail²` or it silently over-covers.

At constant coverage and constant plate thickness a 2× massif costs exactly **4× flank volume** —
that is geometry, not a tuning miss. The levers are coverage (holier mountains) or thickness.

**Baseline** (offline sim, validated bit-exact against the shipped build's authored thresholds —
it reproduced 31,194 / 430,691 to the unit before any edit): **41,353 prisms / 1,210,753 volume**
(the de-gravitized pass alone measured 25,055 / 374,907 before the 2× scale-up). Outer extent
460 → **903**. PhaseThresholds re-authored as baseline + Blob deltas: 42053/41853/44953/44353
count, 1221953/1218753/1268353/1258753 volume. This is now the heaviest cell in the rotation by
volume — `density` (0.5–1.3) on `SpawnableCaldera.prefab` is the fallback lever, and the
soak-before-ship rule in §18 applies double.

**Danger dials** (6 knobs, if the cell plays too hot or too cold): spillway `t < 0.45f` glow
cutoff · basin `u < 0.34f` · river `hot` noise threshold `> 0.36f` · collapsed floor `u < 0.5f` ·
secondary-vent count (5) · erupting mouth disc (285) + curtain strands (5).

**Collider budget:** plain/danger ride the LOD-cullable BoxCollider (bounded by
`PrismColliderLodManager` radius, not population). Always-on convex MeshColliders (shielded +
super-shielded landmarks) go 16 → **36**: 35 shielded (fumarole caps + degassing lip caps, both
families multiplied by the 2× pass's cluster counts) and 1 super-shielded (the cooled massif's
frozen heart), still ~0.09% of the cell's prisms and well under the 225 the Yggdra roll carries.

---

## 18.2 Ourobor — the one-sided country (August 2026)

§18.1 removed the pre-tetrahedral Caldera's ground plane, and with it two things that were
genuinely good: the **pleasant rolling landscape** its floor made, and the **fun cityscape feel**
of the basalt-column fields at its base. Both were casualties of *how* they were built (a flat
plane at `y = -180` and towers standing along +Y), not of *what* they felt like. Ourobor is the
new cell that keeps the feel and throws away the gravity.

**The idea.** Three **ultrawide Möbius bands**, interlocked on the three coordinate planes around
the nucleus. Each is ~290 units across, so at flight scale the ground under you is as flat and
rolling as a landscape and the towers around you stand as straight as a skyline — the local feel
is preserved exactly. Only when you keep going does the surface curve out from under the idea of a
single up. And because each band carries an **odd** number of half twists it is genuinely
one-sided: follow the countryside far enough and you return to your own starting patch standing
upside down on the other face. **The stalagmites you flew out between are the stalactites you fly
back between. They were never different towers.**

**The math** lives in the `Band` struct — `E1`/`E2` span the loop plane, `E3` is its normal, and
the width direction rotates out of the plane as it goes round:

```
Width(u) = Radial(u)·cos(Phase + TwistRate·u) + E3·sin(Phase + TwistRate·u)
At(u, v) = Radial(u)·Radius + Width(u)·v
```

`TwistRate = HalfTwists / 2`, so after a lap the width direction has rotated by `π·HalfTwists` —
for odd counts it has *flipped sign*, which is the whole trick. `AlongSurface` is the exact
∂P/∂u (the loop tangent stretched by the width term plus the twist's own contribution) and
`Normal = cross(AlongSurface, Width)` is the local "up" that only exists locally.

| band | radius × halfwidth | half twists | country / fields | city stone / crowns |
|---|---|---|---|---|
| 0 "the homeland" | 620 × 145 | 1 | Jade / Gold | Blue / Gold |
| 1 "the wringer" | 700 × 160 | 3 | Jade / Blue | Gold / Ruby |
| 2 "the narrow" | 780 × 130 | 5 | Gold / Jade | Blue / Ruby |

The three are **not** kept apart: where two bands pass they cross, and a crossing is a multi-level
interchange with country and city on every deck. That is the point of a cell with no up, not an
artefact to fix.

**Families.** *Rolling ground* — the old floor idiom moved onto a ribbon: plates laid flat on the
surface (thin axis along the local normal), lifted by two octaves of low-frequency noise into
swells and hollows, with a noise cull for ponds and broken ground and a second noise field
painting gold field patches and blue outcrops. *Cityscape* — Caldera's Giant's-Causeway bundle
(solid pipes on two rings whose gaps fit the vessel) seated across the country and grown along
**±normal**, the sign taken from a noise field so districts *clump* rather than alternate; heights
spread by `tall²` so it reads as a skyline, and every third district carries a spire under a
shielded crown. *Cornice* — the band's boundary, which needs `u` to run **0 → 4π** to close,
because a Möbius band has one edge; fly it and you have flown both "edges" of the country without
ever crossing one. Its far end carries the band's super-shielded **keystone**, the one fixed point
in the cell. Plus a centreline *road* and drifting *motes*.

**Baseline** (same offline sim): **37,889 prisms / 751,449 volume**, extent 422 → 982, **zero
prisms inside the nucleus** (every `BandSpec` is authored so `Radius − HalfWidth − RollAmp −
TowerDepth` clears `NucleusR`). PhaseThresholds 38589/38389/41489/40889 count,
762649/759449/809049/799449 volume.

**Zero danger** — the pastoral pole, alongside Geode. Its risk is disorientation, not damage, and
that is the deliberate contrast with Caldera sitting next to it in the rotation. It is also NOT
Daedala: Daedala is *built* everywhere and gravity-coherent (terraces climb, minarets stand up);
Ourobor is landscape with towers on both faces and no global up at all.

**Collider budget:** 27 always-on convex MeshColliders (24 shielded spire crowns + 3
super-shielded keystones), ~0.07% of the cell's prisms. Everything else is plain and rides the
LOD-cullable BoxCollider.

**Follow-ups.** (a) Not yet flown — confirm the band width really does read as "locally flat" at
vessel speed, and that a crossing is legible rather than confusing. (b) Ourobor shares the generic
cell icon with every other config; it wants its own art. (c) Device soak, same rule as §18.
(d) Ourobor's assets (prefab, cell config, metas, and the Menu_Main `CellConfigs` array entry)
were hand-authored as YAML and have never had an editor import pass — the checks below cover it.

---

## 18.3 In-editor verification for §18.1 / §18.2 (the human is the gate)

Neither cell has been opened in Unity. Run these in order; each has a specific failure it catches.

1. **Import.** Pull the branch and let Unity reimport. `SpawnableOurobor.prefab` and
   `Ourobor Cell Config.asset` must both open with **no "Missing (Mono Script)"** row and no
   `None` reference — the prefab's `prism` field in particular must show the prism prefab, or the
   cell builds zero prisms silently. (Their GUIDs were minted offline and checked for collisions,
   and the prefab's serialized field set is byte-identical to `SpawnableCaldera.prefab`'s, but an
   import pass is the only real proof.)
2. **Baselines.** `FrogletTools > Ecology > Measure Cell Environment Baselines`. Expect
   `SpawnableCaldera` **41,353 / 1,210,753** and `SpawnableOurobor` **37,889 / 751,449**. These
   came from an offline bit-exact port of the generators (validated by reproducing the shipped
   Caldera's 31,194 / 430,691 to the unit), so a divergence of more than a few hundred count /
   few thousand volume means the port drifted — re-author the two `PhaseThresholds` blocks from
   the measurer's numbers + the Blob deltas, don't keep the authored ones.
3. **Console on load.** `SpawnableOurobor.BuildBands` carries two fail-loud authoring guards
   (a band reaching inside the node-control radius; an even half-twist count, which would make an
   ordinary two-sided annulus instead of a Möbius band). Either firing is a red console error and
   an authoring bug, not a runtime one.
4. **Phase at rest.** Menu_Main → Cell Selector → each cell. Both must idle in **Calm**, not
   Restless/Frenzy. Frenzy-at-boot means the ladder is under the baseline (step 2 failed).
5. **Node control unclaimed.** With the cell freshly built and no player mass laid,
   `Cell.TryGetNucleusClaim` must return **false**. Both generators are authored to lay nothing
   inside the 392u control radius (measured minima: Caldera 405.9, Ourobor 422.1); a claim at
   boot means something reached in.
6. **The things only flying can answer.** Caldera: do the 2× massifs still feel like four
   distinct mountains rather than a wall, and is the danger (1,503 prisms, 6.0% of mass, much of
   it on the crust you orbit) fun or punishing? Ourobor: does ~290 units of band width actually
   read as "locally flat" — that is the whole premise — and is a band crossing legible or
   confusing? Both: the `EnvironmentLoadVeil` hold is now longer than any previous world; time it.

Per-cell fallback lever for all of the above: `density` (0.5–1.3) on the two Spawnable prefabs.

---

## 19. Opt-in worlds — the environment-free boot + the Cell Selector toy (July 2026)

§18 gave the freestyle rotation six authored worlds of ~31–36k prisms each. It also gave
Menu_Main a **multi-second `EnvironmentLoadVeil` hold on every entry** — the first boot *and*
every return from an arcade game — because `AssignConfig` rolled one of the seven configs at
random and six of them build a world. §18 follow-up 4 named the two ways out; this is the one
that shipped.

### The boot half — `CellTypeChoiceOptions.EnvironmentFree`

A third choice mode on `Cell`: **boot on the first config in `CellConfigs` that authors no
`EnvironmentPrefab`** (falls back to index 0, loudly, if every config has one). Menu_Main's
Cell is set to it, so the menu opens on **Blob** — no prepopulated build, no veil, no wait.
The other six stay in the list; they are simply not paid for until asked for.

This is not a special case bolted onto the menu: it is a config knob on the Cell, so any scene
that wants a cheap entry with heavy worlds available on demand gets the same behaviour with no
code.

### The opt-in half — `Cell.RequestCellSwap` + the Cell Selector toy

`Cell.RequestCellSwap(config, clearLooseTrailMass)` is the one runtime entry point:

| Step | What happens |
|---|---|
| 1 | `StopSpawner()` — nothing new seeds into a world that is leaving. |
| 2 | Every vessel drops its trail bookkeeping (`ClearTrails`, `AttachedPrism = null`) and pens up (`SetSpawnerPaused`). `Trail.LookAhead`/`Project` and `TrailFollower` dereference their prisms **without null guards**, so this must precede the teardown. `ClearTrails` drops bookkeeping only — it removes no prism. |
| 3 | Everything the cell owns — the environment container, the lifeforms, the old membrane/nucleus/cytoplasm, and (optionally) the pooled trail prisms — is gathered under **one root** that **suctions to a point** over `retireSuctionSeconds`. The authored environment is a single container, so the 35k-prism case costs one re-parent. |
| 4 | Pooled prisms `ReturnToPool()` (destroying one corrupts the pool's accounting); the rest are destroyed in **500-per-frame slices** while the root is already invisible — a 35k-prism teardown in one frame is a multi-second freeze. |
| 5 | Bookkeeping reset (the same set `ResetCell` clears), then `runtime.Config = config` — the one sanctioned bypass of `AssignConfig`'s deliberate stickiness. |
| 6 | Membrane + nucleus → **then** `SetupDensityGrids()` (grids are sized off the membrane) → cytoplasm → modifiers → **then** `BuildEnvironmentNow()`. Ordering matters: an immediate build before the grids would file its first prisms into grids that are about to be disposed. On boot this cannot happen because the build is deferred past scene start. |
| 7 | The standard `EnvironmentLoadVeil` holds the screen while the world streams in; the spawner restarts and the trails un-pen only once the lay has drained, so flora/fauna seed into a **finished** world. |

`CellSelectorToy` is the player-facing surface — a toy, so no score, no end condition, no
timer. Fly it and a matrix of **mini-cells** blooms outward, well clear of the toy (the
Lifeform Matrix's "fly at a wall of choices" pattern, now sharing `ToyMatrixStation`). Fly a
mini-cell and the cell becomes it. **Fly the mini-cell of the world you are already in and you
get the same cycle on the same config — that is the reset.**

The toy authors **no cell list of its own**: it reads `Cell.AvailableConfigs`, the Cell's own
rotation. The Cell owns the environment (`ECOSYSTEM_MASTERPLAN.md §5.1`), so there is exactly
one source of truth for what a scene's cell can be and the toy cannot drift from it.

Each mini-cell is three gyroscopic rings (membrane) + a nucleus dot, holding a genuine **scale
model of the world that config creates**. `CellMiniatureBuilder` reads the generator's own
output — `SpawnableBase.GetTrailData()` plus the new `CellEnvironmentSpawnableBase.CachedLays`
(the per-*prism* domain, which `SpawnTrailData` flattens to one domain per trail) — strides it
to a ~1.2k point budget and emits one box per sample into a single mesh with a submesh per
domain, painted in the real domain prism materials. So the thumbnail is the world's real
silhouette, structure, and domain composition. **No prism is ever spawned for a model**:
generation is pure math, and the ~97%-of-a-build per-prism `Instantiate` never happens. Models
stream in one per frame behind the shells (each blooming in), the meshes are cached for the
session, and `ReleaseGeneratedData()` drops the generator's point data right after sampling —
retaining seven 34k-entry lay lists so the menu can show seven thumbnails is the wrong trade on
mobile, and re-generating on load is a small fraction of the lay cost. A config with no
environment has nothing to model and draws **visibly empty**, so the picture tells you the entry
is free before you read the `RESET` / `LOAD` / `INSTANT` label.

### Invariants — what this does and does not touch

- **Continuity of existence (upheld).** The old world **suctions** away over a visible
  transition — the same sanctioned transport the microscene conveyor uses — and the new one
  **blooms** in prism by prism through the canonical `PrismTrailBuilder` lay path. Nothing pops
  in or out at either end.
- **Mass is conserved (upheld — this is not decay).** Nothing here is on a clock. No prism ages
  out, no lifespan expires, no population is culled to hit a number, and no cell "tidies itself
  up". A cell swap is an **explicit, player-initiated world change** — the same class of event
  as the scene load that has always ended a cell's mass — and it is the *only* thing that
  removes this mass. The distinction §0 draws is between *passive* removal (rejected) and
  *active* removal (the whole point); a toy the player must fly into is as active as it gets.
- **Every lifeform drops a crystal (not violated).** The invariant binds `Fauna.Die` — death by
  starvation or predation. Retiring a world is an un-load, not a death, so lifeforms leave
  without dropping crystals — exactly what `ResetCell` and every scene transition already do.
- **No imposed death / no domain asymmetry / volume is the spine / territorial permanence** —
  untouched. The new cell runs its own authored `SpawnProfile` and `PhaseThresholds` from the
  first frame, so a swapped-in Yggdra gets Yggdra's ladder, not Blob's.
- **Toy-owned closed systems are left alone.** The reset retires **pooled** prisms (the
  vessels' trail); instantiated toy mass — the Wanderway conveyor transports its own fixed,
  conserved stock — has no pool handler and is never touched, so a cell swap cannot break the
  conveyor's conservation.
- **Collider budget: net negative.** The default menu cell is now the *empty* one, so the
  steady-state active-collider count in Menu_Main drops from "one of six 31–36k-prism worlds"
  to "trail + spawned life only". A loaded world costs exactly what §18 measured for that
  config — nothing new. The toy's own stations are transient trigger spheres (one per config,
  freestyle only) torn down with the matrix.

### Known limitation — party clients pick independently

Cell selection is **local**, like every other toy effect that has no server-authoritative path.
In a party each client would run its own cell. This is not a regression: environments are
already built locally with no seed sync, and `AssignConfig`'s `Random.Range` roll already gave
each client a *different* cell. A deliberate pick is strictly more consistent than a random
one. Making it authoritative means an RPC on the menu's cell — tracked in
`Docs/ToySystem/BACKLOG.md`.

---

## 20. Prism destruction velocity & the mass-report defect (July 2026, Rhino sword branch)

Landed while making the Rhino sword hand a destroyed prism the velocity of the *part of the
blade* that hit it (`Docs`-side reference: `_Scripts/Controller/Vessel/R_VesselActions/RHINO_SHIELD_SWIPE.md`
§ "Swing velocity model"). Two things here touch ecology.

### 20.1 Fauna debris leaves at the creature's own speed

`Boid` knocking a prism loose used to call

```csharp
prism.Damage(currentVelocity * embeddedHealthPrism.Volume, ...)
```

The `* Volume` was there to cancel `Prism.Explode`'s `impactVector / prismProperties.volume`.
It cancelled nothing, for two independent reasons:

- The factor was the volume of the **boid's own health prism**, not the victim's.
- That divide is a **no-op**. `SetupDestruction` runs first and stands the scale animator
  **down before reading the volume**; `PrismScaleAnimator.GetCurrentVolume()` gates on
  `enabled` and returns 0 once it is off, so `Mathf.Max(0f, 1f)` pins
  `prismProperties.volume` to **exactly 1 for every prism** at the moment of the divide.

So creature debris was scaled by an unrelated quantity. The multiply is gone; debris now leaves
at the creature's own speed. **No ecology invariant is touched** — this is destruction VFX
velocity only. Mass conservation, diet, starvation, spawn cadence and phase are all unchanged;
nothing about *whether* a prism dies moved, only how fast its debris flies.

### 20.2 OPEN — the destroyed/created mass events misreport volume

The same ordering breaks mass **reporting**, and "volume is the spine":

| channel | raised with | actual value |
|---|---|---|
| `OnTrailBlockDestroyed` | `Volume = prismProperties.volume` | **always exactly 1**, every prism, every size |
| `OnTrailBlockCreated` | `Volume = prismProperties.volume` | read while the prism is still scaled to **zero** (`BeginGrowthAnimation` runs after) |

`PrismScaleAnimator` later writes the true target volume (`TargetScale.x*y*z`) on growth
completion, so `prismProperties.volume` is correct *during* a prism's life — it is only the two
lifecycle **events** that carry a wrong number.

**`Cell.LiveVolume` is NOT affected** — it aggregates `Prism.CachedVolume` through
`PrismSpatialIndex`, a separate path that reads the live transform. So phase, dominant domain,
prey selection and the HUD are all correct. What is suspect is anything keying off the
created/destroyed **event** payloads (destroyed-mass stats, scoring that sums prism volume).

Deliberately **not** fixed on that branch: moving the read above the animator shutdown changes
the numbers those channels report, which is a scoring/stats behaviour change with its own blast
radius and needs its own verification pass. Fix shape, when someone takes it: capture the volume
*before* `scaleAnimator.enabled = false` in `SetupDestruction`, and seed the creation event from
`authoredTargetScale` rather than the pre-growth transform.

Do not "fix" this by pre-multiplying an impact vector by volume somewhere else — that is the
trap this section exists to document (see §20.1).

---

## 21. Hesperides — the garden cell, and flora as the world (August 2026)

The freestyle seven (§18) are worlds you **fly through**: ~34–41k authored prisms laid behind a
veil, with flora and fauna seeded on top afterwards. **Hesperides** is the first cell where the
world is the **planting**. It authors only ~12k prisms of *architecture* and then hands the
cell's ordinary flora spawner a list of **prepared ground**; everything else — the canopy, the
climbers, the bed cover — is grown by living flora that the food web can eat.

Mature, that is ~33k prisms / ~985k volume: Yggdra's weight, reached by growth rather than by
lay. A stripped Hesperides is a *correct* Hesperides, and the beds are still prepared ground
when the pressure lifts.

### 21.1 Audit — which flora actually work

Eight flora prefabs exist. What each one is, and whether it can be planted today:

| prefab | script | growth | verdict |
|---|---|---|---|
| `GyroidFlora` | `AssembledFlora` + `GyroidAssembler` | gyroid minimal surface from bonded lattice sites | **works** — the shipping species (Blob plants Mass/Space/Time) |
| `SchwarzPFlora` | `AssembledFlora` + `SchwarzPAssembler` | Schwarz P minimal surface | **works** — shipping (Blob) |
| `BranchingFlora` | `BranchingFlora` | crystaltropic random branch scribble | **works**, unused by any cell config |
| `CactiFlora` | `BranchingFlora` | same, non-crystaltropic, `leafChance -2` | **works**, unused |
| `PineFlora` | `BranchingFlora` | same, `leafChance -3` | **was broken** — see below |
| `NerveFlora` | `BranchingFlora` | same + `SecondaryNerveFlora` secondary spawn | **was broken** |
| `WallFlora` | `AssembledFlora` + `WallAssembler` | wall lattice | **was broken** |
| `SeaweedFlora` | *(none)* | — | **dead prefab**: carries no `Flora` component at all, so nothing can plant it. Left in place; it is referenced by no config. |

**The break.** `PineFlora`, `NerveFlora`, `WallFlora` and `SecondaryNerveFlora` pointed their
`cellData` field at guid `16d80244d807ac84493fff643826a0a0` — a `CellRuntimeDataSO` that does
not exist in the project. `Flora.Plant()` dereferences `cellData.CrystalTransform` on every
unpinned plant and `LifeForm.Start()` reads `cellData.Cell`, so any attempt to plant one threw.
Repointed at the live `Runtime Cell Data.asset` (`8d4e8398…`), which is what every working flora
and fauna prefab uses. That restores three species and makes their eight existing
`_SO_Assets/Lifeforms/` configs usable.

> **Wider finding, deliberately NOT fixed here.** The same dangling guid is referenced by
> `Clawfish`, `QuadFish`, `TermiteDrone`, the three `Worm*` prefabs, `oldWallFlora`, both
> cytoplasm prefabs, and three scenes including `Menu_Main`. Those are live shipping objects, so
> a blanket rewrite of scenes and fauna prefabs is its own change with its own verification —
> flagged, not swept in. Worth a dedicated pass.

**How they grow (the shape of the seam).** Both existing models are *surfaces*, not plants.
`AssembledFlora` asks an `Assembler` for the next bonded lattice site, claims it in
`PrismSpatialIndex`, and crystallises a triply-periodic minimal surface; `BranchingFlora` grows
a random branch scribble that only reads as structure in bulk. Both plant themselves on a random
shell of the membrane (`plantRadiusCellFraction × MembraneRadius`), grow one step per
`growPeriod` while `Cell.FloraGrowingEnabled`, hold at most `maxTotalSpawnedObjects` LIVE prisms
(consumption frees budget, so a grazed flora regrows), and re-sprout branches when every active
branch has been eaten or exhausted.

### 21.2 The new species — `PhyllotacticFlora`

A garden needs plants with a **silhouette**, and it needs them to be one species varying by
parameter rather than three bespoke behaviours. `PhyllotacticFlora` is one growth model:
a set of growing **tips**, each advancing along its heading, pulled toward a growth axis,
wandering, occasionally forking, and past a depth opening **whorls** of leaves at the golden
angle. Three prefabs express it:

| prefab | tips | tropism / wander / droop | whorls | budget | prefers | role |
|---|---|---|---|---|---|---|
| `ArborFlora` | 1, forks to 10 | 0.60 / 0.16 / 0.05 | 5 leaves every 3 nodes from depth 6, flaring, big terminal head | 260 | Bed | the canopy tree |
| `RosetteFlora` | 1, no forking | 0.90 / 0.05 / 0 | 8 steeply-cupped leaves at **every** node | 90 | Bed | the bed carpet |
| `FrondFlora` | 4, no forking | 0.45 / 0.10 / **0.35** | paired leaflets the whole way along an arching stem | 150 | Bed·Water | the fern |
| `CoralFlora` | 3, forks to 14 | 0.30 / 0.34 / 0 | **none** — stubby forking only | 200 | Bed·Water | the low thicket |
| `SpireFlora` | 1, forks to 3 | 0.92 / 0.05 / 0 | small whorls corkscrewing (twist 26°), huge terminal head | 170 | Ledge·Bed | the accent mast |
| `TendrilFlora` | 3, forks to 8 | 0.12 / 0.50 / 0.18 | 2 leaves every 3 nodes | 120 | Climb | the climber |
| `ReedFlora` | 5, no forking | 0.95 / 0.07 / 0.08 | one blade pair every 6 nodes, near the top | 110 | Water | the pool margin |
| `LanternFlora` | 1, no forking | 0.85 / 0.08 / 0 | one big **down-cupped** head (pitch −55°) | 70 | Basket | the hanging bell |

Two shipping species round it out as **topiary** — `GyroidFlora` and `SchwarzPFlora` on small
prism budgets, planted sparsely on bed ground, so they read as clipped specimen pieces among the
grown plants. The garden borrows the platform's flora rather than making everything new.

**The prisms themselves.** Every prism used to be the one `leafSize` box, which is what made the
first pass read as stamped rather than grown. Now shape follows role:

- **Stem prisms** take their cross-section from the element's leaf identity and their LENGTH from
  the actual segment (`stemScale.z` is a *fraction of the segment*), so successive segments meet
  into a continuous stalk instead of a string of beads.
- **Leaf prisms** span their own reach and are placed at **half their own length out from the
  node**, so a leaf runs from the stalk outward and is attached. Placing them *at* the reach —
  the first pass — left every leaf floating at the end of an invisible stem, which is the single
  biggest reason the whorls read as a wheel of chips.
- **Whorls are cupped, not flat** (`leafPitchDegrees`) and **alternate long/short**
  (`whorlAlternateScale`), giving a head an inner and an outer rank. A flat wheel of equal leaves
  reads as a gear.
- **Depth taper + per-prism jitter** (`depthTaper`, `prismJitter`) — a mature trunk is heavy at
  the base and fine at the crown, and nothing in the garden is machined.
- **Gravity droop and spiral twist** (`gravityDroop`, `spiralTwist`) bend and corkscrew the stem
  without competing with the growth axis — an arching frond, a spiralling spire.
- **A terminal whorl** (`terminalWhorlScale`) opens at the end of a stalk whatever the whorl
  cadence says: the bloom.

Because prism lengths are now structural, this flora reads `LeafSize.x/y` (the element's
cross-section — a Space garden is wiry, a Mass garden thick) and not `LeafSize.z`. The assembled
species keep using `LeafSize.z` as their thin axis, unchanged.

Everything else is inherited and unchanged: prisms are conserved mass laid through the ordinary
health-prism path, growth is gated only on `Cell.FloraGrowingEnabled` (steady until Frenzy, no
self-limit), sites are claimed with `PrismSpatialIndex.TryReserve` before the spawn (colliders
are blind for a prism's first 0.6s), instantiation drains at `maxSpawnsPerFrame` so a grow tick
is never a burst, death withers spindle-by-spindle from the extremities and drops the elemental
crystal, and the heart is joustable while it lives. **No clock removes anything.**

Thirty-two canonical configs (8 species × Charge/Mass/Space/Time) live in `_SO_Assets/Lifeforms/`,
following the gyroid convention that an element's identity is its leaf PRISM and its growth
TEMPO — matching the authored gyroid ordering (Space: long thin needles, slowest, smallest
budget; Mass: fat slabs, biggest budget; Charge: ships shielded leaves; Time: the baseline shape,
fastest). The cell's own configs `SpreadElements` across
that palette, so a Hesperides garden carries all four elemental crystals.

### 21.3 Seeding — the environment prepares ground, the Cell plants it

The garden's architecture and its planting are one composition, so they cannot be authored
apart. But an environment must not spawn lifeforms — **the Cell owns the ecology**. So the
environment publishes **sites** and the ordinary spawner uses them:

```
SpawnableHesperides.BuildEnvironment()
  ├─ Emit(...)  →  _cachedLays               (prisms, exactly as every environment does)
  └─ Sow(pos, up, kind) →  PlantingSites     (prepared ground + normal + FloraSiteKind)
                          │
Cell.BuildEnvironmentNow() ─ AdoptPlantingSites()   copy, seeded shuffle, bucket by kind
                          │
RandomLifeSpawner.PlantOne()
  └─ Cell.TryTakePlantingSite(cfg.PreferredSites, out pos, out up)   per-kind round-robin, WRAPS
        └─ CellLifeSpawnerBase.SpawnFlora(..., pos, up)
              └─ Flora.SetPlantPositionOverride(pos, up)   →  Flora.GrowthUp
```

**Ground has a kind.** `FloraSiteKind` is a flags enum — `Bed`, `Climb`, `Basket`, `Water`,
`Ledge` — the environment tags each site with, and `FloraConfigurationSO.PreferredSites` is what
a species declares it wants. Reeds go to the pool, climbers to the column feet, bells to the
baskets, and a tree never ends up in a hanging basket. It is a *preference*, not a requirement:
a garden with none of the preferred ground falls back to any prepared site, and a cell with no
prepared ground at all disperses across the membrane exactly as before, so nothing new can mute
a species. Each kind carries its own cursor, so two species preferring different ground never
advance each other's rotation.

Four properties worth naming:

- **Same spawn path.** A garden gets no privileged spawner — only better-chosen ground. The
  plants are ordinary food-web citizens from the first frame: grazeable, joustable, starvable,
  crystal-dropping.
- **The ring wraps.** Sites are never consumed. A bed whose plant was grazed to nothing is
  prepared ground again, so the garden regrows *where it was planted*. This is emergent
  recovery, not a respawn timer — planting still only happens below Frenzy.
- **The normal is load-bearing.** `FloraPlantingSite.Up` is why the hanging baskets work: their
  normal points **down**, so what roots in them trails toward the floor. `Flora.GrowthUp` falls
  back to "away from the cell centre" for unstructured ground, which is what the legacy shell
  dispersal already implied.
- **Flora wait for the world.** `Cell.IsEnvironmentBuildPending` is true from Initialize until
  the deferred boot build lands (§18); the flora loop waits on it (25s ceiling) and then honors
  the profile's `FloraInitialDelaySeconds` — which `RandomLifeSpawner` had been ignoring
  outright. Without this the entire initial batch disperses over empty space seconds before the
  world arrives underneath it.

Every existing environment sows nothing, so `PlantingSites` is empty for the freestyle seven and
`TryTakePlantingSite` returns false — the legacy shell dispersal is untouched.

### 21.4 The garden

`SpawnableHesperides` (seed 137), a Blob-family cell — same membrane / nucleus / cytoplasm /
modifiers as Yggdra:

| structure | prisms | volume | notes |
|---|---|---|---|
| terrace beds (5 rings × 3 courses) | 1,830 | 83k | deliberately thin slabs — the bed is the stage |
| terrace kerbs | 610 | 71k | the readable step between terraces |
| outer wall (8 courses, crenellated) | 1,464 | 110k | gaps to fly through, not a sealed drum |
| pergola columns + arches (12 × 8 bays) | 2,496 | 118k | fly under; every column foot is sown |
| fruit lanterns | 48 | 4k | **shielded** |
| trellis towers (9) | 1,656 | 25k | woven uprights + rungs; sown at foot, mid, top |
| aqueduct ring + 6 cascades | 800 | 36k | |
| central pool | 500 | 20k | phyllotaxis disc |
| hanging baskets (14) | 700 | 10k | planting normal points **down** |
| vine dome (10 ribs + crown) | 640 | 19k | the frame a mature garden roofs over |
| orchard gate | 96 | 5k | **super-shielded** — the permanent bones |
| brambles (2 arcs) | 320 | 4k | **true danger prisms** — a garden has thorns |
| pollen | 900 | 2k | curl-field drift; the air is not empty |
| **authored total** | **12,060** | **~507k** | |
| mature planting (~140 plants) | ~21,000 | ~478k | grown, not laid |
| **mature total** | **~33,000** | **~985k** | ≈ Yggdra (34.3k / 541k) |

563 planting sites, tagged by ground: **306 Bed** (terraces), **210 Climb** (192 pergola column
feet + 18 trellis foot/mid), **24 Water** (pool rim), **14 Basket**, **9 Ledge** (trellis crowns).

**PhaseThresholds ride the baseline** (§18's rule), but with the headroom sized for *growth*
rather than for a trail: Restless at 16,300 / 602k (fauna start hunting once the garden is
perhaps a fifth grown), Frenzy at 33,000 / 985k. **Frenzy is therefore the garden's planting
budget** — flora plant and grow at a steady rate until the mature figure above, then freeze, and
resume on their own when grazing or a vessel brings the mass back down. The ladder is the only
thing bounding the canopy; there is no cap, TTL or culler anywhere in it.

### 21.5 Invariants

- **Continuity of existence** — upheld. Architecture blooms in prism-by-prism through
  `PrismTrailBuilder`; plants grow leaf by leaf through the health-prism path; death withers
  from the extremities inward and drops a crystal. Nothing pops.
- **No imposed death** — nothing here is on a clock. The garden is bounded by the phase ladder
  on the way up and by the food web on the way down, and by nothing else.
- **No domain asymmetry** — flora seed in all three playable domains (`PickRandomDomain`);
  fauna spawn in the controlling colour. The garden's own architecture is laid across Jade /
  Gold / Ruby.
- **Wither-to-crystal + mass conservation** — inherited unchanged from `LifeForm.Die`.
- **Volume is the spine** — the ladder is authored in volume with the count backstop tracking
  it; the thin bed slabs exist so authored mass does not eat the headroom the planting fills.
- **Territorial permanence** — the orchard gate is super-shielded, so no force in the food web
  can take it: the gate still stands whatever happens to the planting. Everything else is
  deliberately contested.
- **Endogenous selection** — untouched; no fitness function anywhere.

### 21.6 Collider budget

Per-prism colliders are the same LOD-cullable `BoxCollider` every prism carries (active count
bounded by `PrismColliderLodManager` radius, not by population), and the mature garden's ~33k
prisms sits *at* Yggdra's count, not above it. The always-on convex `MeshCollider` tier is **144**
(96 super-shielded gate + 48 shielded lanterns) against Yggdra's 225 — comfortably inside the
same ration. The one genuinely new cost is the lifeform **heart**: +1 always-on `SphereCollider`
per live plant, ~140 at maturity (flora hearts are bounded by the profile's planting counts and
the Frenzy ceiling, exactly as fauna hearts are bounded by `MaxLivePopulation`). No new spatial
query type is introduced — growth uses `PrismSpatialIndex.TryReserve`, the same claim the
gyroid assembler already makes, and no `Physics.OverlapSphere` is added anywhere.

### 21.7 Verification (in-editor — NOT yet run)

**Compile status (August 2026): the C# is compiler-verified, not just inspected.** Using the
offline `mcs` + stubs harness (`/asset-surgery` §4), `PhyllotacticFlora`, `SpawnableHesperides`
and `FloraPlantingSite` compile clean — and `PhyllotacticFlora` was compiled against the **real**
`Flora.cs` and `LifeForm.cs` sources (not stubs of them), so every base member it touches
(`AddSpindle`, `AddHealthBlock`, `healthTracker`, `LeafSize`, `TryGetPlantPositionOverride`,
`ResolvePlantRadius`, `GrowthUp`, `Die`, `RemoveSpindle`) is verified against the actual
declarations. What that does NOT cover: the 65 hand-authored prefab/SO assets (Unity import is
still the first proof), and behaviour of any kind.

The prism/volume figures below are analytic (exact loop counts × authored scales × the 1.04
expected `Jit` volume factor), not measured — nothing has been observed running.

1. **Baseline.** FrogletTools ▸ Ecology ▸ **Measure Cell Environment Baselines** with
   `SpawnableHesperides`. Expect ≈ 12,060 prisms / ≈ 507k volume. If it lands more than a few
   hundred count / few thousand volume off, re-author `PhaseThresholds` on the same rule:
   Restless = baseline + ~4.2k count / +95k volume, Frenzy = baseline + ~21k count / +478k volume.
2. **Lifeform crystals.** FrogletTools ▸ Validation ▸ **Validate Lifeform Crystals** — the eight new
   flora prefabs must pass (each carries an authored elemental crystal; configs replace it per
   element at spawn).
3. **Menu_Main.** Boot the menu (it still opens on Blob — `EnvironmentFree`, index 0, unchanged),
   enter freestyle, fly the **Cell Selector**. Hesperides is the 8th mini-cell and must draw a
   real scale model (terraces + wall + dome) with a `LOAD` label. Select it: the old world
   suctions, the garden blooms in behind the veil.
4. **The planting is the test.** Within ~30s of the swap, ~93 plants should appear on the ground
   each species prefers — arbors/rosettes/ferns/corals in the beds, tendrils on the pergola and
   trellis feet, reeds at the pool rim, lanterns hanging *downward* under the baskets, spires on
   the trellis crowns. Nothing on a random sphere. The trailing lanterns are the direct check
   that the site normal is honoured; a reed in a basket means the kind tagging is wrong.
5. **Growth + grazing.** Watch a few minutes: the canopy should thicken toward the Frenzy ceiling
   and then stop; tadpoles/quadfish should graze it and the architecture back down and growth
   should resume on its own. Confirm no plant ever vanishes — a grazed one withers and drops a
   crystal.
6. **Perf.** Soak Menu_Main on Hesperides and record steady-state numbers in
   `Docs/PERFORMANCE_OPTIMIZATION.md`. Levers, in order: the profile's planting counts /
   `PlantPeriod`, the per-species `maxTotalSpawnedObjects`, then the prefab's `density`
   (0.5–1.3). The Frenzy ceiling is the hard budget dial.

### 21.8 Known gaps

- The three repaired flora (`Pine`, `Nerve`, `Wall`) are structurally complete and now point at
  the live runtime data, but have not been planted in-editor since the repair.
- The wider dangling-`cellData` finding in §21.1 is unaddressed.
- Hesperides authors no `Icon`; the Cell Selector uses the scale model, so this only matters if
  a future surface wants a sprite.
- The eight forms' parameters are authored blind — they are geometrically reasoned, not looked
  at. Expect a tuning pass on `whorlRadius` / `segmentLength` / `leafScale` per species once
  they can be seen growing.
- `Tools/ecosim/gen_hesperides_assets.py` regenerates the whole asset set deterministically —
  retune there rather than hand-editing twelve configs.

---

## 22. Ribcage — a mode redefining "control", and the shielded-steering finish (August 2026)

> **STATUS (2026-08, later the same month): Ribcage no longer has fauna.** The brood was removed
> from the level on request, and with it the controller's ladder. Everything §22.1–§22.2b describes
> is therefore a record of a SHIPPED-THEN-RETIRED consumer, not live behaviour — but the **platform
> capabilities it drove all remain** (`Cell.SetModeControlOverride` / `ModePhaseFloor` /
> `FaunaReleaseTier` / `FaunaContainmentRadius` / `ContainmentIntruderFrenzy`,
> `SpawnProfileSO.InitialFaunaReleaseTier`, `FaunaConfigurationSO.ReleaseTier`, the batched fauna
> seeding), several now with no caller. They are kept deliberately: the design work below is the
> reusable part, and re-adding a brood to any mode is a data change against these APIs.
> **§22.3 (shielded mass leaves the targeting grids) is live and cross-mode — it is unaffected.**

Ribcage (`GameModes.Ribcage = 39`, display name "Peel the Cage",
`_Scripts/Controller/Arcade/RIBCAGE.md`) is the Rhino-only cage-breaking race: concentric hollow
shells of prism bone that domains race to smash their way out of — the bone IS the score
(`ScoringMetric.PrismsDestroyed`, target 2,000), and intensity picks how many shells there are
(2–5, one `CellConfigDataSO` each via `CellTypeChoiceOptions.IntensityWise`). Its bars are now
plain one-hit prisms, so §22.3 no longer applies to its own arena.

While it HAD fauna it was ecologically interesting for one reason — **the whole "the fauna hunt
whoever is losing" feature was written in zero lines of fauna code**, and getting there needed one
honest generalization. That reasoning is preserved below because it is the template for the next
mode that wants it.

### 22.1 The leader IS the controlling domain

`Cell.SetModeControlOverride(Domains?)` pins the cell's `DominantDomain`. Ribcage's
controller sets it to whichever domain leads the destruction race. Everything else is
existing machinery:

- `Cell.ControllingDomain` → `RandomLifeSpawner` spawns the wave in that colour. The
  **no-domain-asymmetry invariant is untouched**: still exactly ONE colour, still the
  cell's controller. The mode changed what "control" *means*, not how many colours
  spawn — the same authority move Brood Rush made when it declared node control to be
  the nucleus claim (§13).
- `Cell.IsPreyForHerbivore` in a **nucleus-less** cell is the legacy rule
  `preyDomain != faunaDomain`. So the leader's swarm eats every *trailing* team's
  mass. That is the entire feature. There is no targeting code, no per-player fauna
  steering, no "find the loser" query — the diet rule was always this, and the mode
  merely arranged for the fauna to wear the right colour.
- Ribcage's cell config therefore has **no `NucleusPrefab`**, and that is load-bearing:
  a nucleus control zone switches herbivores to the spatial "eat anything outside the
  nucleus" diet, which would point the swarm at every team including the leader's.

The setter also re-colours the **live** swarm (`Fauna.SetTeam` over `Cell.LiveFauna`),
so a lead change flips the targets of creatures already in the air rather than only the
next wave — and so a cell can never hold two fauna colours at once, which is what the
invariant actually forbids.

### 22.1b What the swarm is actually FOR (the axis inversion)

The mode's race is **creation** — first domain to hold `PrismTargetCount` prisms STANDING
(`ScoringMetric.PrismsRemaining`, a live stock). Smashing the cage scores nothing; it only
advances the fauna rungs. That inversion is what makes the ecology load-bearing instead of
decorative: the swarm eats standing mass, standing mass IS the score, so releasing the
brood directly un-scores every team the leader is ahead of.

It also puts a genuine cost on the trigger — time spent breaking bone is time not spent
laying, so you fall behind to arm a swarm that then serves whoever is ahead. A cumulative
"prisms created" counter would have killed all of this: it only ever rises, so nothing a
creature did could set anyone back.

Note the leader the cell is pinned to is the **race** leader (creation), not the
destruction leader. `Cell.SetModeControlOverride` does not care which stat decided it —
that is the point of the override being a domain rather than a rule.

### 22.2 Escalation rides the phase ladder, not a new system

`Cell.ModePhaseFloor` (nullable, default null) lets a mode hold the cell at or above a
phase. The volume ladder still runs every tick; the floor only ever **raises** the
answer. Ribcage floors the cell at Restless once the LEADING domain reaches 25% of the win
target and Frenzy at 50%, so fauna aggression, steering, danger-immunity and speed all come
from the existing `CellPhase → CellAggressionLevel` mapping. Keying the rungs to the
leader's own progress rather than a cross-domain total is what keeps the escalation
arriving at a fixed point in the RACE, independent of lobby size.

This is **not** the growth/decay oscillator §0 rejects: it is monotonic in an ACTIVE
player force (mass destroyed by vessel abilities), it removes no prism, and it starts no
clock. Note the direction of travel — destruction *lowers* the cell's volume, so the
ordinary ladder would only ever descend here; the floor is the sole thing that climbs.

`Cell.FaunaReleaseTier` + `FaunaConfigurationSO.ReleaseTier` stage which species may
seed (Ribcage: the four grazer species from the first tick — penned, not gated — and the
predator at 50%). Defaults — config tier 0, cell `int.MaxValue` — leave every shipped
biome released from the first tick.
Gating **production** is the explicitly-allowed lever ("not creating mass is allowed;
aging it out is not"); nothing here culls.

### 22.2b Containment — a pen is a spatial diet, not a wall

The cage is stocked from the first frame (the fiction needs a visible brood, not empty
scenery) but the brood must not join the match going on outside it. `Cell.
FaunaContainmentRadius` (0 = none, the default everywhere else) expresses that with the
two rules fauna already run on:

- **Diet.** `IsPreyForHerbivore` returns false for anything outside the radius, checked
  before the domain/nucleus rules. A penned creature has nothing to eat out there
  whatever colour it wears — so flying INTO the cage puts your trail on the menu, and
  that is the only way to feed them before the release.
- **Steering.** `Fauna.Goal` became a PROPERTY whose setter clamps through
  `Cell.ClampToFaunaContainment`. That matters more than it looks: goals are written
  from six places (Fauna.ResolveGoal, Boid's override, LightFauna's direct writes on its
  own behavior tick, the spawner's initial goal, reproduction inheritance), and clamping
  in each of them would be a rule the next grazer could forget. Clamping in the setter
  is a rule that cannot be bypassed.

It is deliberately **not a wall**: nothing is teleported, no collider is added, and a
creature can still drift out on its own momentum — it just has no reason to and nothing
to eat there.

**The intruder response.** `Cell.ContainmentIntruderFrenzy` (opt-in) raises the pen to
**Frenzy** while `HasPreyInsideFaunaContainment` is true — a confined population that
detects food goes berserk on it. That is the same phase floor a mode could set by hand,
driven by the pen instead of by mode progress, so it adds no new ladder. Detection is one
Burst `PrismSpatialIndex.QuerySphere` on the PHASE tick (0.4 s, shared buffer, shielded
mass filtered) — never a physics query, and only while a pen exists.

The pen radius deliberately sits INSIDE the structure that visually encloses it (Ribcage:
338 vs a 360 shell), so the enclosure's own prisms are outside the pen. That is what stops
a penned brood from quietly eating its own cage — which matters because a cage may
legitimately contain unshielded prisms (Ribcage's danger traps) that would otherwise be
food, and would also read as a permanent "intruder".

Collider budget: unchanged by the containment mechanism itself. Containment adds two
squared-distance compares on paths that already ran; the intruder probe is one
existing-index sphere query per 0.4 s. The CELL it is used in is another matter — Ribcage's
cage is ~10,229 prisms (Rampage's deliberate arena gate) plus ~150 creature bodies, which
is the branch's headline perf risk and is stated as such in RIBCAGE.md.

**The start state is authored as biome DATA, not set at runtime.** `SpawnProfileSO.
InitialFaunaReleaseTier` seeds `Cell.FaunaReleaseTier` in `AssignConfig`, upstream of
`StartSpawnerForMode` by construction. The first version set the gate from the mode
controller's `OnNetworkSpawn` and lost the race against the cell's own bootstrap clock,
so the brood spawned ungated. A mode's *escalation* is a runtime concern; a biome's
*starting* state is data, and treating it as data is what makes it race-free.
`IntensityWiseLifeSpawner` honours the tier too, so which spawner a biome happens to use
can never decide whether the gate holds.

### 22.3 The shielded-steering finish (the generalization §16 left half-done)

**Symptom this would have caused.** Ribcage's arena is a huge shielded structure. Under
the pre-existing rules the cage sat in the cell's density grids, so every density
centroid — the goal at aggression Level1 and Level2 — pointed at mass §16.2 had already
declared inedible. The swarm would have flown to the cage and found nothing to eat.

**Cause.** `Cell.AddBlock`'s own comment states the rule — *"fauna must never be led to
mass they cannot eat"* — but applied it only to nucleus-interior mass. §16.2 removed
shielded prisms from every herbivore's **diet**; nobody removed them from the
**grids**. That gap is the residue behind §16.3's Skim Race stall: the stall itself was
fixed with the orbit offset and the degenerate-steering guard, but swarms were still
being *aimed* at track prisms they could never consume.

**Fix.** Shielded prisms are excluded from the targeting grids at `AddBlock`, and
`Cell.NotifyBlockShieldStateChanged` re-files a prism when a shield engages or is shed
(shield state is runtime-mutable, so the classification has to be able to change). It is
routed from `PrismStateManager.SyncAOERegistryShieldState` — the single funnel every
shield transition already passes through — via
`PrismSpatialIndex.ForwardShieldChangeToCell`, mirroring the existing
`ForwardDomainChangeToCell` steal path exactly.

"Not food" and "not a steering target" are now one rule with one predicate on each side
(`Fauna.IsShieldedMass` for the diet, `Cell.IsShieldedMass` for the grids), which is why
a future grazer cannot re-acquire either half of the bug.

**Cross-mode effect, and it is the correct one.** Skim Race's super-shielded track and
Astro League's super-shielded edge lining no longer pull fauna steering. Both need an
in-editor regression pass (RIBCAGE.md § verification, step 10).

**Collider budget: unchanged, and strictly less work.** No collider, no physics query,
no index query is added. Shielded prisms are *removed* from the grids, so every density
query scans fewer entries; `NotifyBlockShieldStateChanged` costs one bool compare on the
common "shield re-applied" path and a grid remove/add only on a genuine transition. The
cage itself is ~2,721 box colliders — shielded prisms keep the authored BoxCollider
trigger, so the octahedron look is free — which is ~1.8× the masterplan's ≤1500 target
and ~3.7× *under* Rampage's deliberate 10,000-prism arena gate, in a cell with no flora.

**Known gap, left deliberately.** `Cell.OpposingVolume` still counts shielded mass as
the fauna prey signal, so a shielded structure satisfies `FaunaFoodFloor` without being
food. Ribcage sidesteps it (`FaunaFoodFloor 0` — the release tier is the real gate), but
the honest fix is to net shielded volume out of that signal. It is the population bound
for every biome, so it deserves its own change and its own verification rather than
riding along here.

## 23. The worm colony kaiju — a connected population as a boss fight (Aug 2026)

The worm returns as what it was always meant to be: a **colony fauna** — head, body
segment, and tail are three fauna types forming one connected population — rebuilt from
scratch on the modern `Fauna` substrate as a cooperative **kaiju boss**. The 2024 trio
(`Worm`/`WormManager`/`BodySegmentFauna`) and its ten orphaned prefabs were audited across
every prior attempt (shipped shell, ancient commits, the `Sharks-and-worms` branch) and
**deleted**: movement had been commented out since Aug 2024, growth ran on a wall clock,
segments died crystal-less into immortal zombies, and the parent-chained transforms made
slither structurally impossible. What survived is the *design*: the three-type colony
decomposition, split-on-mid-death, regrow-the-missing-end, danger-armed extremities (the
danger-block system was literally born for this worm in 2024), and the follow-the-leader
movement model — plus the `Sharks-and-worms` branch's telegraph→burst attack grammar.

### 23.1 The creature

- **`WormFauna`** (colony brain, `FloraAndFauna/WormFauna.cs`) — the lineage-registered
  Fauna the spawner sees (`WormColonyFaunaConfig.asset` → `WormColony.prefab`). One
  behavior tick and one movement pass drive the whole chain. Classified **Predator** so
  the food web never targets it (nothing eats a kaiju); `Predated` is sealed to false —
  the segments are the killable surface. Its `ResolveGoal` inheritance means the boss
  hunts the same density targets every fauna does, phase-escalated by the cell.
- **`WormSegmentFauna`** (`WormSegmentRole` Head/Body/Tail, three prefabs:
  `WormHeadSegment`/`WormBodySegment`/`WormTailSegment.prefab`) — each segment is a
  genuine fauna: body `HealthPrism`s under a `Spindle` (LifeForm deliberately null — a
  creature body, not consumable cell mass), registered in `PrismSpatialIndex` and synced
  per frame (movers contract). Head and tail author **danger prisms** (`DangerBlock`
  instances — the standard domain-blind danger effect chain does all contact damage) and
  carry an elemental **heart** provisioned to the authored element
  (`LifeFormCrystal.EnsureElementalCrystal(this, heartElement)` — the element-as-data
  channel). Body segments carry one high-volume core prism (volume is the spine — big
  volume, ONE collider).

### 23.2 The fight (all of it emergent from the rules)

- **Kill a BODY segment** (its core prism) → the worm **splits in two**; both halves
  begin regrowing their missing ends. Mid-body kills multiply the problem.
- **Kill an END** (strip its danger prisms, or joust its heart — hearts are joustable,
  and `CurrentSpeed` is the live head speed, so out-race the kaiju to joust it) → the
  heart drops as a collectible (mass conserved), and the wound's neighbor
  **differentiates** into the missing role after `EndRegrowSeconds` — danger prisms
  engage through `MakeDangerous` (a state change of existing mass, the same legal class
  as shield regen; the worm still net-shrank by one segment).
- **The optimal strategy emerges**: chain end-kills faster than the differentiation
  window and you always face soft tissue; slower, and every kill is armored. This is
  the "best killed tail-to-head or head-to-tail, and fast" rule — never scripted,
  purely a consequence of split + differentiation timing.
- **An APEX OMNIVORE that also hunts pilots.** The head is the colony's mouth and it
  works three ways at once: it **grazes prism mass** by the canonical herbivore rule
  (`Cell.IsPreyForHerbivore` + `Fauna.IsShieldedMass` — shielded mass is never food);
  it **devours creatures** whose root comes within `FaunaBiteRange` of the jaws (the
  head's fang centroid) — the shark's own break-apart-and-suction kill via
  `Predated(name, mouth)`, and unlike the shark it is not limited to herbivores: an
  apex kaiju eats sharks too (it skips its own segments, other worm colonies, and
  predation-immune newborns); and it **hunts players** (below). All three feed the same
  clock, so hunting and grazing alike fund growth. Nothing in the food web preys on it
  in return: the colony root is classified Predator and its `Predated` is sealed false,
  and segments are Predator too so no shark can pick one as dinner. A headless worm
  cannot feed at all — regrow the head or starve.
- **Growth is feeding-funded ONLY**: every `FeedsPerSegment` feeds (prisms grazed or
  creatures eaten), one body segment **blooms in** behind the head. Length is a
  readable record of consumption.
- **Starvation digests the colony tail-first** (one segment per
  `StarvationShedIntervalSeconds`): deny the kaiju food and it shrinks; keep denying and
  it dies. Population bounded by consumption, never a lifespan. A starving worm also
  cannot differentiate its wounds — denial is a real co-op strategy.
- **The pilot hunt**: inside a hunt window, a vessel within `AggroRadius` (220) is
  **pursued** — the head goes nose-on, faster (`PursuitSpeedMultiplier`) and turning
  harder (`PursuitTurnMultiplier`) so it tracks a juking pilot. Closing inside
  `StrikeRange` (90) triggers the wind-up. Lose it, or let the window close, and the
  kaiju drops back to grazing.
- **Souls-like attack grammar** (hunt pulses, rest-first, same clock math as the
  shark): telegraph (head rears back, coiling, near-stopped — `TelegraphSeconds` of
  readable wind-up) → lunge (point locked at telegraph end, so dodging works) →
  recovery (slow, straightened — the punish window). A vessel loitering at the rear
  provokes a **tail whip** (rear follow-points swing laterally; the danger stinger does
  the rest). All contact damage is the existing danger-prism impact pipeline.

### 23.3 Invariant review (the rulings, recorded)

- **Continuity**: segments bloom in (prism growth stamps + root scale bloom), husks
  wither out (prisms suction inward, spindles evaporate, bounded-wait husk removal).
  Nothing pops, either direction.
- **No imposed death**: the only clocks are differentiation (state change of existing
  mass, gated on being fed) and starvation shedding (the standard
  consumption-bounded-population channel). Growth has NO clock — feeds only.
- **Crystal contract**: the colony's hearts live on its capital segments (head + tail,
  one each; a split provisions the new worm's ends as they differentiate). Body
  segments are connective tissue — body parts, not lifeforms — per the §15 stance,
  which this section supersedes in part: worm capital segments now DO carry and drop
  hearts. Colony hearts deliberately do not join the §15 domain buff pool in v1
  (segments are not lineage-registered), so a kaiju can't destabilize the elemental
  economy — revisit deliberately if wanted.
- **No domain asymmetry**: the colony spawns through the standard controlling-color
  pipeline (`RandomLifeSpawner` → `SpawnFaunaWithDomain`); nothing special-cases color.
- **Fauna senses**: prism sensing via `PrismSpatialIndex.QuerySphere`; vessel sensing
  via the shared `OverlapScratch` + `NonPrismOverlapMask` physics path on the behavior
  tick; colony-vs-colony sensing via the cell's fauna registry — never a physics query
  against prisms.

### 23.3.1 Boid separation + mass-seeking (Aug 2026, playtest round 3)

Two things the first passes left out, both found in play:

- **Worms didn't repel each other.** Colonies are boids like everything else in the
  cell: `TickSeparation` walks the cell's fauna registry for other `WormFauna` and
  pushes this worm's HEAD away from each neighbour's **nearest segment** (a worm is
  long — head-to-head distance is the wrong read), inverse-square weighted, summed
  into the steering alongside the goal pull (`ColonySeparationRadius` /
  `ColonySeparationWeight`). Separation applies while free-steering (Cruise, Pursue,
  Recover) but **not** during Telegraph or Lunge: a committed strike must stay
  readable and dodgeable-by-moving, not get deflected by a neighbour. The per-instance
  `GoalOrbitOffset` is kept in the goal (below) so two colonies never seek the
  identical point — separation and anti-convergence are complementary, not redundant.
- **The kaiju idled at the crystal instead of hunting mass.** The base fauna goal
  parks a Calm creature at the cell crystal; an apex forager should hunt food.
  `WormFauna.ResolveGoal` now returns the **densest sensed region at every phase**
  (`Cell.GetDensestRegionAnyDomain`, which falls back to the cell anchor in an empty
  cell) plus the orbit offset — so a worm is drawn to the cell's mass, and one
  dropped outside the membrane comes home instead of drifting in empty space.
- **The Lifeform Matrix hatched creatures into the void.** The bench's variant
  stations are layered outward and can sit hundreds of units BEYOND the membrane, and
  `SpawnFaunaVariant` hatched the population AT the station — in empty space, with
  nothing to graze, which defeats the bench's purpose. Fauna now hatch on the cell's
  densest sensed mass (the same target every forager seeks), jittered like a spawner
  wave. Flora still plant at their station: a rooted structure is placed deliberately,
  a creature roams anyway.

### 23.4 Collider budget (the hard gate, stated)

Per segment: body = 1 BoxCollider (one high-volume core prism); head = 11 (the 8
recovered armor plates + 3 danger fangs); tail = 8 (the recovered two-tier stinger:
4 blades + 4 tip spikes); + 1 heart SphereCollider on each capital segment. A
spawn-size-8 worm = 12+6×1+9 = **27 active colliders**; at the
`MaxSegmentsPerWorm=16` growth cap = **35**. Splits conserve segment totals (never
exceed the cap) and add at most one heart per differentiated end. Against the
~1,500/cell target this is negligible — the deleted 2024 worm cost 28 colliders per
worm *and grew unboundedly on a timer*.

### 23.4.1 The recovered 2024 geometry (Aug 2026 second pass)

The first rebuild carried the design but invented its geometry; the prompter called
it: the ORIGINAL authoring had the good bones. Recovered verbatim from git history
(`f065c8f76^`) into the new prefabs:

- **Head armor cage**: the 8 mirrored plates of `WormHeadSpindle` (4 z-stations,
  ±y pairs, angled quaternions, 4.7→6.2 widths) wrap the head's rear — now authored
  as GENUINELY shielded prisms (`prismProperties.IsShielded=1` + the segment's
  `shieldArmor` engage — the old asset only had the *naming*): each plate takes one
  hit to shed its shield and a second to destroy. The 3 danger fangs sit at the
  mouth. The **heart nests inside the cage** at the authored (0,0,−13.14), scale 2.5
  (`WormSegmentFauna.heartLocalPosition/Scale`).
- **Chain proportions, measured off the model** (Aug 2026 correction — the first pass
  authored `SegmentSpacing = 14` and the worm read as beads on a string). The
  invariant is **gap ÷ model scale**: the 2024 chain rendered its body model at
  localScale 1 with authored gaps of 8.05 / 8.39 / 8.63 / 8.71, so `SegmentSpacing`
  is **8.4 model units** (× `KaijuScale` × taper) and the segments nearly touch.
  Head-gap = 2.56× the body gap (`HeadGapMultiplier`, from the authored 21.5 ÷ 8.4),
  into-tail gap = 1.79× (`TailGapMultiplier`, 15 ÷ 8.4), and the authored
  **0.9-per-segment taper**
  (`TaperPerSegment`) — segment scale AND link spacing shrink down the chain, so the
  head is the biggest thing on the worm and the tail trails away. Segments GLIDE to
  their taper targets when topology changes (growth, splits) — the worm visibly
  re-proportions, never snaps; a grown segment blooms from zero through the same
  glide (which replaced the bloom coroutine).
- **Tail stinger**: `ParentTailSpindle`'s four giant X-blades (20×2×3.75 at ±7.6
  x/y) plus `ChildTailSpindle`'s four tip spikes as a nested spindle tier at
  (0,0,−2.15) — the tip withers before the blades (extremity-inward). The old asset
  authored the child tier at scale ZERO (invisible — a bug); recovered at scale 1.
- **Natural-scale visuals**: the worm meshes render at their authored natural size
  (the first pass over-scaled them 4×); `KaijuScale` remains the one size dial.

### 23.5 Deployment + tuning

Species entries in `_SO_Assets/Lifeforms/`: `WormColonyFaunaConfig.asset`
(Element=None — keeps the prefab-authored Mass hearts) plus the menagerie-convention
four `Worm Colony Charge/Mass/Space/Time.asset` (Element authored; the colony root
forwards the pick to its capital segments' hearts via the `Fauna.ProvisionHeart`
override — the root itself stays heartless, and wounds differentiate into the picked
element). All are `PopulationSize=1` (a lone kaiju; the seed floor sees split-children
via lineage registration, so it never re-seeds while any worm lives).

**Spawnable NOW from the Lifeform Matrix toy** (freestyle): the four element configs
are wired as the "Worm Colony" species in `Toy_LifeformMatrix.asset` — fly the toy →
fly "Worm Colony" → fly an element/level station and the kaiju spawns live into the
cell in your domain. (Level is inert for the colony in v1 — `SetLevel` scales only the
empty root anchor, so L1/L3/L5 stations spawn the same-size worm; size lives on
`KaijuScale`.)

**Deliberately wired into no SpawnProfile** — a boss is opt-in. To deploy ambiently:
add a worm config to a cell's `SpawnProfileSO.SupportedFaunas`. Natural host for the
co-op fight: `MinigameWildlifeBlitzMultuplayerCoOp` (note §10.3: that scene uses
`IntensityWiseLifeSpawner`, which spawns 1/tick — fine for a PopulationSize-1 boss).
All feel/fight tuning lives on `WormColonyConfig.asset` (`WormColonyConfigSO`).

### 23.6 In-editor verification (the human is the gate)

Nothing here has run in Unity — the whole branch is machine-validated only (see §23.7).
First pass, in Menu_Main freestyle:

1. **Import clean.** Pull, let Unity reimport, confirm zero compile errors and that the
   four new prefabs open without "Missing (Mono Script)" rows. Run
   **FrogletTools > Validation > Validate Lifeform Crystals** — head/tail hearts are
   runtime-provisioned by design, so it should stay quiet about the worm.
2. **Spawn**: freestyle → Lifeform Matrix toy → "Worm Colony" → any element station.
   Expect 8 segments hatching **on the cell's densest mass** in your domain: a plated
   head, 6 tapering bodies, a bladed tail — segments nearly touching, tapering to the
   tail, with a wide head gap.
3. **Swim**: head seeks mass and slithers; the body follows the wave. It should GRAZE
   (prisms suction into the head) and DEVOUR creatures that stray into its jaws.
4. **Fight**: fly near it during a hunt window → it pursues nose-on, rears back and
   coils (~1.2s), lunges at the locked point (dodgeable by moving), then drifts slow
   through recovery. Loiter at the tail for the whip.
5. **Kill**: shoot a mid-body core prism → the worm splits in two. Strip a head plate
   twice (shield sheds, then the plate dies) — kill all 11 head prisms, or joust the
   caged heart, and the head drops its crystal; ~18s later the next segment hardens
   into a new danger head.
6. **Two worms**: spawn a second — they should visibly repel and orbit the same
   buildup from different sides rather than interpenetrating.

Dials if it reads wrong, all on `WormColonyConfig.asset`: size `KaijuScale`;
spacing `SegmentSpacing`/`TaperPerSegment`; aggression `AggroRadius`/`StrikeRange`/
`HuntIntervalSeconds`; appetite `MouthRadius`/`FaunaBiteRange`/`FeedsPerSegment`;
crowding `ColonySeparationRadius`/`ColonySeparationWeight`.

### 23.7 Known gaps + follow-ups (scoped, not blockers)

- **Not play-verified.** No Unity in the authoring environment: everything is
  compile-reviewed and machine-validated (YAML structure, every GUID resolves, every
  serialized key matches a real C# field, brace/token balance, the conditional-
  compilation CI gate). First in-editor pass is §23.6.
- **Client-local.** Fauna have no NetworkObject (§7 caveat 4), so in multiplayer each
  client fights its own worm until fauna sync lands. A co-op kaiju eventually needs
  server-authoritative colony state (NucleusRush's SOAP-over-NetworkVariable pattern).
- **Segment kills raise no scoring event.** Fauna deaths are invisible to the
  `LifeForm.OnLifeFormDeath`-based WildlifeBlitz scoring; a boss-hunt mode needs its own
  SOAP channel (model: `CellRuntimeDataSO.OnFaunaWaveSpawned`).
- **Level is inert for the colony.** `SetLevel` scales the empty root anchor, so the
  matrix's L1/L3/L5 stations all spawn the same-size worm; size lives on `KaijuScale`.
  Wiring level → `KaijuScale`/segment count is a clean follow-up.
- **A differentiated end keeps its body-segment mesh** (a battle-scarred stump head —
  the danger prisms and behavior carry the read; a mesh swap would be the polish).
- **Wither/bloom ride per-frame CPU** like all fauna today (C6 in the clock-material
  tracker covers that migration; the worm added no new CPU animation tier).
- **The Lifeform Matrix station for the colony is an anonymous labeled sphere** — the
  root prefab carries no renderer for `ToyModelBuilder` to sample. A mini-worm station
  model is cosmetic follow-up.

---

## 24. Wildlife Liberation — the creatures become killable, and a pen becomes a band (Aug 2026)

`GameModes.WildlifeLiberation = 40` is the Sparrow-only hunt: three concentric cages at
1050 / 600 / 200 pen three tiers of wildlife, and the first PLAYER to kill 500 creatures wins.
Full mode reference: `_Scripts/Controller/Arcade/WILDLIFE_LIBERATION.md`. Two of its changes are
**platform ecology** and belong here.

### 24.1 A creature dies when its last body prism is destroyed

**Before this branch, no creature in the game could be killed by shooting it.** Destroying a
fauna's body prisms removed prisms and left the creature swimming with a thinner body. The only
kill paths were starvation, predation, and the crystal joust
(`VesselWitherLifeformByCrystalEffectSO` → `Fauna.Predated`). `WormSegmentFauna` was the sole
exception — §23 gave it `OnBodyPrismExploded`, and that stayed a worm-only rule.

The consequence was invisible until a mode needed it: the **Sparrow**, whose entire verb set is
guns and missiles, could not kill wildlife at all. A "hunt the wildlife" mode was therefore
impossible to build without either a bespoke damage path (a cheat) or this fix.

`Fauna.OnBodyPrismExploded` is now the base behaviour: when the last body prism is gone the
creature dies through the sealed `Fauna.Die`. Guarded once per creature (`_diedFromBodyLoss`),
because a missile's AOE can strip the last several prisms inside one frame and every one of them
calls back.

**Why this is not a new sink in the §0 sense.** The conserved-mass law says a prism is only ever
removed by an ACTIVE force — a vessel using an ability, or fauna eating it. A player shooting a
creature is the first of those. Nothing here is a timer, a lifespan, or a cull: a creature nobody
shoots still only ever dies to starvation or predation, and the population is still bounded by
the food web. What changed is that an active force can now finish what it started.

Invariants, checked one by one:

| invariant | status |
|---|---|
| Continuity of existence | **Held** — `Boid.OnDeath` / `LightFauna.OnDeath` wither or suction the remains; both skip already-destroyed prisms, so a shot creature's surviving structure still leaves visibly rather than popping. |
| No imposed death | **Held** — no clock added anywhere. |
| Starvation = wither-to-crystal | **Held** — the kill path is the same sealed `Die`, so it withers from the extremities inward exactly like starvation. |
| Every lifeform drops one elemental crystal | **Held** — `Die` drops it before `OnDeath` runs. Sealed, so no subclass can bypass it. |
| No domain asymmetry | **Held** — nothing in the path reads domain. |
| Mass is conserved | **Held** — the prisms were destroyed by the player through the ordinary destruction pipeline and accounted there; the creature's heart becomes a collectible. |

**It affects every mode**, and in every case as an improvement: wildlife in Skim Race, Brood
Rush, freestyle and the Wanderway are now killable by any vessel that can destroy a prism.
Verify rather than assume (`WILDLIFE_LIBERATION.md` checklist item 17).

**Attribution and scoring.** `Die` publishes PLAYER-attributed deaths only, on
`CellRuntimeDataSO.OnFaunaKilled` (a `ScriptableEventString` carrying the killer's name — a SOAP
channel, not a static event, and on the runtime SO rather than each fauna prefab so no creature
prefab needed a new wire). Engine attribution (`Fauna.StarvationKiller`, a predator's name, a
colony wither reason) is filtered there, and `StatsManager.LifeformKilled` filters again against
the player roster. So **the ecology dying of its own accord can never move a scoreboard** — which
is what keeps a hunt mode from being farmable by waiting.

This is the fauna twin of `LifeForm.OnLifeFormDeath`, which has fed the flora side of
WildlifeBlitz's scoring all along and answers §23's "segment kills raise no scoring event"
follow-up.

**One consequence of §7 caveat 4 lands here and is worth flagging for any future fauna-scored
mode.** Because fauna have no `NetworkObject` and every peer simulates its own swarm, a creature
a CLIENT just killed may not exist on the server at all — so recording server-side (the way every
other stat here works, because a prism exists identically on every peer and the server's own
physics sees a client's ram) would mean only the host could ever score. `StatsManager` therefore
grew its only client branch: a client forwards its own kill through its own `Player` object
(`Player.ReportFaunaKill_ServerRpc`), the same owner-detects → server-records round-trip
`NetworkVesselImpactor` uses for jousts, with identity taken from RPC ownership rather than a
name string.

**Fauna network sync is in flight on a separate branch**, and when it lands the divergence
retires - but this RPC does not become wrong, it becomes redundant-but-harmless: it is an
owner-reports-to-server round-trip keyed on ownership, which stays correct whether or not the
creature also exists on the server. Until then, any mode that scores on the ecology needs this
shape, and needs to understand that a DOMAIN sum over client-local fauna is two independent
hunts added together rather than one swarm hunted twice - so a shared domain converges on a
target faster than a solo one, and per-domain targets tuned before the merge will need
re-measuring after it.

### 24.2 A pen becomes a band

§22 gave a mode one pen: `Cell.FaunaContainmentRadius`, a single radius for the whole cell.
Three nested cages need three pens, so the capability is generalized to an **annulus authored per
species** — `FaunaConfigurationSO.BandInnerRadius` / `BandOuterRadius`.

Same contract as the cell pen, for the same reason: **a spatial DIET + STEERING rule, never a
wall.** Nothing is teleported, no collider is added, nothing is culled for crossing a boundary. A
creature can drift out on its own momentum — it simply has no reason to and nothing to eat there.
`0 = no band` is the default and what every shipped biome authors.

Applied at three points, all of them existing chokepoints rather than new ones:

- **`Fauna.Goal`'s setter** — the single point every goal writer already passes through (§22's
  reason for making `Goal` a property). The cell pen clamps first, then the band.
- **`Fauna.IsPreyForMe`** — a new shared edibility predicate the three grazers now route through
  (`LightFauna.IsEdibleForHerbivore`, `WormFauna.IsEdiblePrism`, `Boid.IsEdibleForForager`),
  composing the band with `Cell.IsPreyForHerbivore`. Same reasoning as `Fauna.IsShieldedMass`
  (§16.2): *"a creature must never be led to mass it cannot reach or eat"* is ONE rule, and a
  per-subclass copy is a rule you can forget to apply in the next grazer.
- **`CellLifeSpawnerBase.SpawnFaunaBanded`** — a banded species HATCHES inside its room,
  SCATTERED across it (independent direction + radius per creature, for spawn position and
  initial goal). Unbanded species are untouched.

  **It is on the BASE for a reason worth remembering.** `Cell.StartSpawnerForMode` picks
  `IntensityWiseLifeSpawner` whenever the cell is on `CellTypeChoiceOptions.IntensityWise` —
  which is also the only way to vary a cell by intensity. So a mode that wants per-intensity
  cells AND penned fauna gets the intensity spawner whether or not it asked for it, and
  placement written into the *other* spawner is dead code. That shipped: Wildlife Liberation's
  entire population spawned at the cell centre, because `IntensityWiseLifeSpawner` passed no
  spawn position (so `SpawnFaunaWithDomain` defaulted to `host.transform.position`) and used the
  crystal as the goal. Two smaller centre-collapses went with it — `Fauna.ClampToBand` clamped a
  degenerate goal radially and pinned every creature in a room to its inner wall, and
  `IntensityWiseLifeSpawner` never honoured `MaxLivePopulation` at all.

**The band is also a collider-budget device, and that is worth stating.** Wildlife Liberation's
bands stop 60u short of every wall, so a creature's own cage is outside its band and therefore
not food. Without that the grazers would eat two thirds of their own jail (the bars are painted
across the domain triad and the legacy diet eats opposing-domain mass), and the alternative —
shielding the bars — would swap ~9,000 LOD-cullable BoxColliders for always-on convex
MeshColliders (`PrismKinds`). A steering rule bought what a shield would have cost.

Offspring inherit their parent's band for free: they bind the same config.

### 24.3 Collider budget

The mode's arena is 9,206–12,870 cage prisms plus **349–593 live creatures** (up to 868 at the
population caps, 1,436–2,426 body prisms). That creature count is ~6× any shipped biome and is
the branch's headline performance risk — every fauna body prism is a MOVER that re-buckets in
`PrismSpatialIndex` each frame, and every creature runs a behaviour coroutine. It is an explicit
product decision ("very heavy", requested 2026-08), not an accident of the roster. Full table,
the tuning dials in order of bluntness, and the on-device measurement step:
`WILDLIFE_LIBERATION.md` § "Collider-budget impact".

---

## 25. Astro League — a nucleus that is a WALL, and a pen with an inner wall (Aug 2026)

Astro League's cell shipped with a trail-grazing food web (§14) that could not remove a single
prism. The mode is soccer: fauna are there to eat the trail mass that accumulates until the pitch
is unflyable. In play the arena silted up regardless of how the biome was tuned, and the creatures
starved beside a court packed with food. This section records the root cause, the mechanism that
fixes it, and the one new capability the mode needed.

### 25.1 The nucleus was eating the food web

**Node control is the nucleus** (CLAUDE.md ▸ locked invariants): in a cell with a nucleus,
`Cell.IsPreyForHerbivore` returns `!IsInsideNucleus(position)` — the interior is the territorial
claim and a fauna **sanctuary**, which is exactly right for a cell whose nucleus is a core players
contest.

Astro League has **no node control at all**. It scores goals, and it borrowed the nucleus as its
ricochet **court boundary** (`AstroLeagueArena` morphs it with `Cell.SetNucleusMesh` /
`SetNucleusWorldRadius` so the cage you see is the wall the ball banks off — §14). But
`RefreshNucleusControlRadius` measures the nucleus renderer's bounds, so the control radius became
the **court's circumscribing radius**. Every prism in the match was "inside the nucleus":

- `Cell.IsPreyForHerbivore` → `!IsInsideNucleus` → **false everywhere on the pitch**.
- `Boid.IsEdibleForForager` ends on the same test → **false everywhere on the pitch**.

So no herbivore, forager or otherwise, could eat anything in the arena. The only edible mass was
outside the court, where nobody flies. Tuning phase thresholds, food floors or populations could
never have fixed it — the diet predicate was returning false before any of them were consulted.

**The fix is a declaration, not an exception.** `Cell.NucleusIsControlZone` (default **true**, so
every shipped biome is untouched) lets a mode say *this nucleus is play geometry, not a claim*.
False collapses the control radius to zero and the cell falls back to its whole-cell semantics —
exactly the state a cell with no `NucleusPrefab` is already in: herbivores eat opposing-domain
mass anywhere, `DominantDomain` reads whole-cell volume. `AstroLeagueController.ApplyIntensityScale`
sets it false after morphing the nucleus (the setter re-measures, so order matters and the flag
wins on every later refresh).

This does not relitigate "node control is the nucleus". It says this cell **has no control zone**,
which the ecology already supports. A mode that genuinely contests a core (Brood Rush) leaves the
flag alone. Note the practical delta to control is nil here: with the nucleus spanning the whole
court, `nucleusEnvVolumeByDomain` and `liveVolumeByDomain` were already almost the same set.

**Watch for this whenever a mode repurposes a Cell-owned visual.** The Cell's visuals carry
*semantics*, not just geometry — borrowing the nucleus silently borrowed the sanctuary rule with it.

### 25.1a The mirror trap — losing the GEOMETRY with the semantics (Aug 2026)

§25.1 is about a mode INHERITING semantics it did not want along with a Cell-owned visual. The
mirror bit later, on the same declaration: `RefreshNucleusControlRadius` returns early when
`NucleusIsControlZone` is false, so in Astro League `Cell.NucleusWorldRadius` reports **0** — the
arena is right there, hundreds of units across, and the canonical "how big is the nucleus" accessor
says zero. Anything asking a GEOMETRIC question through that property gets a plausible-looking
wrong answer with no error: a soft play boundary keyed off it would have treated every position in
the world as outside the nucleus.

`Cell.NucleusVisualWorldRadius` is the fix: the same renderer-bounds measurement, taken BEFORE the
control-zone branch. The control radius is now *derived* from it, so existing behaviour is
bit-identical.

**The rule to carry forward:** `NucleusWorldRadius` answers *who owns this cell*;
`NucleusVisualWorldRadius` answers *how big is the core, in metres*. Placement, boundaries, camera
framing and anything else spatial want the second. During the SPAWN CHAIN both are still empty —
use `ExpectedNucleusWorldRadius` there (§18's rule is unchanged).

First consumer: the Astro League ball's off-pitch drag ramp (`AstroLeagueBall.EffectiveDrag`),
which bleeds a ball's speed increasingly fast once it leaves the nucleus — a soft boundary, never
a wall, so nothing is teleported, culled or reflected.

### 25.2 A pen gains an inner wall

The design ask was "aggressive little creatures that stay OUT of the arena until it starts to get
crowded, then come in and eat it clean". Three existing pieces cover almost all of it:

| Need | Existing fundamental |
|---|---|
| Voracious any-domain grazing | `FaunaVariantTuning.Forager` (the Skim Race trail-cleanup template) |
| "Keep out of a region" | a pen — but `Cell.FaunaContainmentRadius` is an OUTER wall only |
| "The arena is getting crowded" | the **volume phase ladder** — Calm below `RestlessEnterVolume`, Restless above it |

The missing quadrant is the inner wall. `FaunaConfigurationSO.BandInner/BandOuterRadius` (§24.2)
already proves an ANNULUS, but it is authored per-species data and cannot open mid-match;
`Cell.FaunaContainmentRadius` already proves runtime control, but it is one-sided. So
**`Cell.FaunaExclusionRadius`** is the mirror of the containment radius, applied to the same two
rules and carrying the same contract:

- **Diet** — `IsInsideFaunaContainment` now means "inside the outer wall AND outside the inner
  one", and `IsPreyForHerbivore` already routes through it, as does `Boid.IsEdibleForForager`.
- **Steering** — `ClampToFaunaContainment` pushes a goal OUT past the inner wall as well as IN past
  the outer one, from the one setter (`Fauna.Goal`) that no grazer can bypass. It takes the
  creature's own position for the degenerate centre-goal case, for the same reason
  `Fauna.ClampToBand` does: otherwise a whole unfed population collapses onto one point on the wall.
- **Birth** — `CellLifeSpawnerBase.SpawnFaunaBanded` clamps the spawn POSITION through the same
  method, at the one call both spawners share (§24.2's lesson). A creature born inside a closed pen
  would read as the pen leaking.

It is **not a wall**: nothing is teleported, no collider is added, nothing is culled for crossing
it. A creature can drift in on its own momentum — it just has nothing to eat there and every goal
pulls it back out. Both walls default to 0, so every biome that is not a mode's pen is unchanged
(the common path is two compares against zero).

**The mode drives it off the spine, not off a new signal.** `AstroLeagueController.UpdateFaunaExclusion`
sets the radius to the court's `MaxExtent` while `Cell.Phase == Calm` and to 0 at Restless or above.
"The pitch is silting up" IS `LiveVolume` crossing `RestlessEnterVolume`; the ladder's own
Enter/Exit hysteresis debounces the edge for free, so the wall cannot flutter. The wall SWEEPS over
`faunaExclusionSweepSeconds` rather than snapping — continuity of existence applies to the pen's
boundary too. It runs on every peer because fauna and trail prisms are per-peer local objects, the
same as the goal-reset prism sweep — no RPC.

The species itself is `Astro League Piranha Fauna Config Data`: the tadpole prefab at
`BaseBodyScale 0.22`, `Forager` on (any-domain diet), `MinSpeed/MaxSpeed 45/70`, a 60-unit graze
radius, a 0.6 s behaviour tick and `StarvationSeconds 40` — small, fast, and always hungry, which
is what makes it aggressive without a single bespoke behaviour. `CenterFocusBias 0.35` pulls the
released swarm toward midfield, where the play is. Population `8` seed floor / `22` cap, alongside
the existing tadpole (8) and brittlestar (4).

### 25.3 Invariant review

- **Continuity of existence** — unaffected: the pen removes nothing. Creatures still bloom in,
  wither to crystal on death. The wall itself sweeps rather than snapping.
- **No imposed death** — unaffected. Nothing culls a creature for being on the wrong side; an
  excluded creature that cannot feed starves on the ordinary clock, and the release is what feeds it.
- **No domain asymmetry** — unaffected. Fauna still spawn in the cell's one controlling colour. The
  piranha's any-domain DIET is the existing forager rule, and the forager path deliberately does not
  go through the domain leg (§24.2, `Boid.IsEdibleForForager`).
- **Mass conserved** — unaffected. Fauna consumption is an ACTIVE force and the only new sink here
  is that the pitch's mass is now reachable at all. No decay, no timer, no cull was added: §25.1 is
  a bug fix that *restores* an active sink, which is the opposite of the rejected timed culler.
- **Volume is the spine** — reinforced. The release gate reads `Cell.Phase`, which is the volume
  ladder; no count, no bespoke "crowdedness" metric.
- **Territorial permanence** — this cell has no nucleus claim by declaration (§25.1), so the rule's
  nucleus-cell branch does not apply; the nucleus-less branch (fauna eat opposing mass) is what it
  now runs, exactly as the Skim Race biome it was cloned from.
- **Every lifeform drops a crystal** — untouched (the piranha binds the standard tadpole prefab).
- **Collider budget** — see below.

### 25.4 Collider budget

| Item | Before | After |
|---|---|---|
| Super-shielded edge lining (always-on convex MeshColliders) | 240 | **480** |
| Live fauna cap (bodies) | 12 (tadpole 8 + brittlestar 4) | **34** (+ piranha 22) |
| New physics queries | — | **none** |

The lining doubles because the court is ~2.4× larger in each axis and 240 prisms would read as a
dotted rim; it stays a fixed, deterministic count and its volume budget (`480 × 62.5 = 30000`) is
carried straight into the cell config's phase-volume thresholds — **change either and retune the
other**. The piranha is a small Boid, and every fauna sense already rides
`PrismSpatialIndex.QuerySphere`, never `Physics.OverlapSphere`. The exclusion pen adds one squared
compare to paths that already ran the containment compare. The ball still excludes the
`TrailBlocks` layer, so it never collides with what the fauna graze.

### 25.5 Phase thresholds (retuned for the lining budget and for Rhino trail)

| Field | Value | Why |
|---|---|---|
| `RestlessEnterVolume` | 30600 | 30000 structural floor + **600** of trail |
| `RestlessExitVolume` | 30450 | floor + 450 |
| `FrenzyEnterVolume` | 32000 | floor + **2000** of trail |
| `FrenzyExitVolume` | 31600 | floor + 1600 |
| `RestlessEnter` / `FrenzyEnter` (count) | 900 / 3000 | perf backstop only — the lining is volume-only and never enters `LiveBlockCount` |
| `SenseRadiusOverride` | 2000 | covers the intensity-4 court (max extent ≈ 1280) with margin |

The headroom is authored in Rhino trail: a Rhino prism is **≈ 0.75 volume** (`BaseScale (3,3,0.5)`,
`Gap 2` → a `(0.5, 3, 0.5)` sliver) and it lays two per spawn, so +600 volume ≈ **800 prisms** on
the pitch before the crew is released and +2000 ≈ 2700 before Frenzy. This is the mode's primary
pacing dial and the first thing to move after a playtest. **It is vessel-specific**: the previous
values were authored for Squirrel's ≈3.1-volume prisms, and the mode is now Rhino-only.

---

## 26. The two withers — a joust takes the heart, starvation exposes it (Aug 2026)

**Prompter's ask, verbatim in shape:** *"when a squirrel jousts a life form it shouldn't explode. it
should wither. the squirrel should auto collect the crystal. its spindles should wither from the
crystal outward, leaving the prisms behind as a fossil or skeleton. when fauna starve they also
wither but this should be loosing spindles from the outside in until the crystal becomes collectable
by all vessels. so starvation moves in the opposite direction to the squirrel joust, but should also
leave behind prisms."*

Two deaths, one geometry, opposite directions — and the direction is not a style knob. It is the
force that did the killing, read back at the moment the body comes apart.

### 26.1 The two directions

|  | **Joust** (a vessel took the heart) | **Starvation** (nobody took it) |
|---|---|---|
| Heart | freed **first**, at the strike, and **auto-collected** by the jouster | freed **last**, when the wither reaches the core — then collectable by **any** vessel |
| Spindles | wither **nearest-the-heart first**, unravelling **outward** around the hole | wither **farthest-from-the-heart first**, spending the extremities **inward** |
| Body prisms | left standing as a **skeleton** | left standing as a **skeleton** |
| Detonation | none | none |

They are the same operation sorted the other way: order the spindles by distance from the heart,
ascending for a joust, descending for starvation. A shark's fins and a brittlestar's arms still go
before the core body on the starvation death — emergent from geometry, with nothing authored per
prefab — and on a joust the same geometry runs backwards.

Predation is deliberately **neither**: a devoured creature breaks apart and suctions into the
predator's mouth, because there the mass genuinely *transfers to the eater* rather than being left
in place. `LifeformDeathStyle` (`Withered` / `Jousted` / `Consumed`) is the one enum that carries
this, stamped by the killing force and read by the death animation.

### 26.2 The skeleton — mass conservation taken at its word

Before this, a creature's whole frame left the world when it died: the husk was destroyed and its
body prisms went with it, so the *only* thing conserved was the heart. That was a passive removal of
mass hiding inside a death animation. Now the body prisms **stay exactly where the creature died**,
as ordinary cell mass:

- `HealthPrism.LeaveAsSkeleton` drops the body-part links (spindle, `LifeForm`, `OwnerFauna`),
  re-homes the prism to the host cell, and re-files it with `PrismSpatialIndex.NotifyOwnershipChanged`.
- That re-file is what **promotes** it: `ComputeEnvironmentMass` reads `OwnerFauna` to keep a LIVE
  swarm out of the targeting grids (a forager must not read as its own mass concentration). With the
  owner cleared, the skeleton graduates from volume-only body mass to full environment mass —
  grazeable, steerable, counted, contested.
- So the sink is the food web, exactly as `§0` demands: a skeleton is removed only by an **active**
  force (a grazer eating it, a vessel destroying it), never by a clock. A skeleton nothing eats is a
  valid equilibrium, not a defect.

**Ordering is load-bearing.** A body prism is parented to a *spindle*, so the skeleton must be
detached **before** any spindle withers — evaporating a spindle first destroys the very mass the
skeleton is conserving.

### 26.3 Why the spindles had to be isolated first

Two couplings in the ordinary spindle lifecycle make an ordered wither impossible, and both are
structural rather than cosmetic:

1. `Spindle.ForceWither` **recurses into child spindles**. Withering an inner spindle first —
   which is the whole point of the joust direction — would collapse the entire creature in one step.
2. Destroying a spindle GameObject **destroys its child spindles with it**, for the same reason.

`Spindle.IsolateForOrderedWither` breaks both up front: every spindle is detached from its parent
and children, logically *and* in the hierarchy, so the caller can spend them in any order. It also
suspends `CheckForLife`, because handing a spindle's prisms to the skeleton empties it and would
otherwise evaporate it out of turn. The outside-in death happened to work before this only because
it destroys leaves first; nothing about it was general.

### 26.4 The crystal invariant is still sealed — it just moved

"Every lifeform drops one elemental crystal on death" is unchanged; **when** it drops became part of
how the creature died. `Fauna.Die` releases the heart outright for `Jousted` and `Consumed`. Only a
subclass that opts in via `DefersHeartRelease` (today `LightFauna`) holds it through an outside-in
wither.

**A deferral is only safe if the thing being deferred can survive being interrupted**, and a crystal
parented to the husk cannot: destroy the husk and the child goes with it, and reparenting a child
out of a hierarchy that is already being torn down cannot be relied on to rescue it. So the deferral
is two-stage, and the first stage runs at the *top* of the death:

1. **`StashHeart`** (`Crystal.DetachHeartToCell`) re-homes the crystal onto the cell immediately, but
   leaves it **`IsEmbedded`** — so it stays uncollectable and keeps the neutral heart tint, and the
   wither still has a heart to unravel around. Reparenting preserves world pose and a withering
   creature holds still, so nothing appears to move.
2. **`ReleaseHeart`** frees it for real (`ActivateCrystal`) when the wither reaches the core — the
   ask's *"until the crystal becomes collectable by all vessels"*.

With stage 1 done, every later exit is a genuine recovery rather than a hopeful one: `RemoveHusk`
(the terminal every LightFauna death path funnels through) releases unconditionally, and
`Fauna.OnDestroy` releases anything an interrupted wither left — a cell drain, a manager pulling the
husk, a turn ending. `OnDestroy` skips the release during **scene unload**, where the cascade must
not run at all (the rule `Spindle.OnDisable` already follows) and nothing survives to collect anyway.

One consequence worth knowing: a stashed heart is *still embedded*, so any guard written as "has the
crystal stopped being embedded in me?" no longer fires at death. `GrowCrystalWithPop` — the level-up
flare, whose local scale divides out the body's scale and would land at the wrong WORLD scale on a
reparented crystal — was exactly such a guard, and now tests the death itself.

One live consequence, and it is the right one: `Fauna.LiveHeart` (which the domain fauna buff keys
off) now stays non-null through a starvation wither. The heart is the last thing standing, so a
starving creature keeps powering its domain until the wither reaches its core.

### 26.5 Auto-collect

`ElementalCrystalImpactor.CollectBy(SkimmerImpactor)` is the auto-collect entry point — the identical
chain a skim runs (collection effects, flight to the vessel, spend), reachable without a skim
contact. `AcceptImpactee` now delegates to it, so there is one collection path, not two. Its sole
caller is the joust: `VesselWitherLifeformByCrystalEffectSO.TakeHeart` resolves the jousting
vessel's near-field skimmer (far-field as fallback) and awards the crystal the kill just freed. With
no usable skimmer it degrades to the ordinary drop — the crystal simply sits there as a collectible,
which is the starvation behaviour and therefore never a lost crystal.

### 26.6 Scope, honestly stated

- **Fauna**: `LightFauna` (the spindled creatures — shark, brittlestar, clawfish) gets both
  directions plus the deferred heart. `Boid` (the tadpole) has no spindle rings to order, so it
  leaves its skeleton and fades the empty husk out.
- **Flora**: `LifeForm.Jousted` withers heart-outward and leaves a skeleton. **Every other flora
  death keeps the existing destruction** (`DamageAll` + `ForceWitherAll`) — a plant grazed down to
  its lethal threshold has been actively eaten, and the prompter's ask was specifically about the
  joust.
- **The worm colony is deliberately excluded.** Its segments keep the authored suction death
  (`WormSegmentFauna.WitherHuskCoroutine`). A kaiju-scale skeleton would be a wall, and its capital
  segments carry **danger prisms** — leaving those standing would strew permanent hazards through
  the cell on every colony death. Revisit only with a decision about what happens to danger prisms
  in a skeleton.

### 26.7 Collider budget — the real cost, stated

This is the one invariant that **pays** for the change: nothing is added at the moment of death (a
live creature's body prisms already carry colliders), but they now **persist** instead of being
destroyed with the husk. A cell running a 30 s fauna wave clock therefore accumulates skeleton mass
over a match at roughly *(deaths × body prisms per creature)*.

The mitigation is the canon's own answer and needs no new mechanism: a skeleton is ordinary
environment mass, so it enters the targeting grids and **herbivores graze it** — dead creatures
become food. It also inherits the standard collider-LOD-by-phase treatment that every cell prism
gets, and it feeds `Cell.LiveVolume`, so a cell that fills with skeletons climbs its own phase
ladder and its fauna get hungrier and faster. **No new physics queries were added.**

Two things to watch in a playtest, in this order:
1. **Prism count in a long round.** If skeletons outpace grazing, the lever is the diet/spawn tuning
   that already exists (`SpawnProfile.FaunaFoodFloor`, per-species populations) — *never* a timer.
2. **Legacy (nucleus-less) cells.** There, herbivores eat only *opposing* mass, so a skeleton of the
   dominant domain has no predator — the same standing condition as the dominant canopy
   (`§0` territorial permanence), now with one more contributor.

### 26.8 In-editor verification (the human is the gate)

Scene: **Menu_Main** freestyle (Squirrel is the menu vessel, so the joust is one flight away), and
**MinigameWildlifeBlitz** for a populated cell.

1. **Joust a fauna.** Fly the Squirrel faster than a brittlestar/shark and clip its heart. Expect:
   no explosion; the crystal flies to *your* vessel and grants its element; the arms/fins evaporate
   **from the body outward**; a skeleton of prisms is left hanging in space.
2. **Joust a flora.** Same, on any planted flora. Expect the same — specifically **no detonation**,
   which is the visible before/after.
3. **Starve a fauna.** Let a creature run past `starvationSeconds` with no prey (or lower it on the
   `LightFaunaDataSO`). Expect the mirror: extremities first, inward; the crystal becomes collectable
   only when the wither reaches the core; skeleton left behind.
4. **Devour.** Let a predator catch prey. Expect the *unchanged* behaviour — body suctions into the
   mouth, **no** skeleton.
5. **The skeleton is food.** Watch a herbivore approach and eat skeleton prisms. If it ignores them,
   the re-file did not land — check `PrismSpatialIndex.NotifyOwnershipChanged`.
6. **Watch the console for the heart alarm** (`was destroyed with its heart unreleased`). It must
   never fire.

Tuning knobs: `LightFaunaDataSO.witherRingInterval` (fauna ring cadence) and the new
`LifeForm.witherRingInterval` (flora). Both are seconds per ring; keep them above zero or the body
collapses in a single frame, which reads as a pop. The flora knob is also overridable per
element from `FloraVariantTuning.WitherRingInterval` (`-1` = keep the prefab's), the same shape
`ShieldPeriod` uses — a denser plant wants a shorter ring so the whole wither still reads at flight
speed.

### 26.9 Follow-ups (open, recorded rather than done)

1. **The worm colony's danger prisms vs. the skeleton.** The colony is excluded from §26.2 because
   its capital segments carry danger prisms and a kaiju skeleton is a wall. If the colony should
   leave *something* behind, the question to answer first is what a danger prism does in a skeleton
   — stay dangerous forever, shed its danger state on detach, or be the one prism kind the skeleton
   drops. Do not "just enable it".
2. **Flora deaths other than the joust still detonate** (`DamageAll` + `ForceWitherAll`). That is
   deliberate for now — a plant grazed to its lethal threshold has been actively eaten — but if the
   skeleton reads well in play, making it universal for flora is a one-line change to the branch in
   `LifeForm.Die` and worth a deliberate decision rather than drift.
3. **Skeleton accumulation over a long round** is the §26.7 budget risk and can only be answered by
   a playtest. If skeletons outpace grazing, the levers are the existing diet/spawn dials
   (`SpawnProfile.FaunaFoodFloor`, per-species populations) — never a timer, never a cap.

---

## 27. Rampage — a planting shell belongs to the CELL, not to the crystal (Aug 2026)

The Dolphin rework of Rampage (`_Scripts/Controller/Arcade/RAMPAGE.md`) needed one thing
from the ecology: **a belt of breakable flora ringing the membrane, with the core left
open** for a single roaming contested crystal. Three latent defects stood between the
config and that arrangement, and all three are general — none is a Rampage special case.

### 27.1 The planting shell was measured from the CRYSTAL

All three `Flora.Plant` implementations dispersed a new plant about
`cellData.CrystalTransform.position`:

```csharp
float radius = ResolvePlantRadius(legacyRadius: plantRadius);   // "fraction of the cell's membrane radius"
transform.position = cellData.CrystalTransform.position + radius * Random.onUnitSphere;
```

`ResolvePlantRadius` is documented — and named — as *a fraction of the **cell's** membrane
radius*, and every one of those three call sites carried a comment saying "disperse across
the cell". The two only agree while a mode's crystals sit in the cell core, which was true
of every cell that had shipped, so nothing surfaced it.

Rampage's crystal roams to radius 900 in a cell whose membrane is 1200. A plant on the
0.90 shell would therefore have landed at up to `900 + 1080 = 1980` — **outside the
membrane**, where `Cell.ContainsPosition` rejects its prisms: not in `LiveVolume`, invisible
to the phase ladder, and untargetable by the fauna density grids. A belt of food the food
web cannot see is worse than no belt.

**Fixed:** `Flora.ResolvePlantCenter()` — cell centre, falling back to the crystal (legacy)
and then to the plant's own position. That last fallback also removes a real crash: the
`CrystalTransform` property logs and returns **null** in a cell with no crystal at all, so
`.position` on it threw. `BranchingFlora.Initialize` had the same unguarded dereference for
its look-rotation and now resolves once, falling back to the plant's growth axis.

**Rule:** *a planting radius is a fraction of the CELL, so it is measured from the cell.*
Anything a mode moves at runtime — crystals above all — must not be able to drag the
ecology's geometry with it.

### 27.2 A live-prism budget only worked on one flora family

`FloraVariantTuning.MaxTotalSpawnedObjects` was read **only** by `AssembledFlora`.
`BranchingFlora` and `PhyllotacticFlora` declared their own `maxTotalSpawnedObjects` and
ignored the config's, so a cell could author a per-plant budget, save, see nothing change,
and silently get the prefab's — **5000** for both CactiFlora and PineFlora. A handful of
plants can eat a whole arena's phase ladder at that budget.

45 authored assets were already writing into this field expecting it to work: the canonical
`_SO_Assets/Lifeforms/<Species> Flora <Element>` set carries a deliberate per-element density
identity (Charge ×0.85, Mass ×1.2, Space ×0.7, Time ×1.0 of the prefab), and Hesperides'
per-cell configs mirror it. Every one of them was inert.

**Fixed:** `BranchingFlora` and `PhyllotacticFlora` now override `ApplyVariantTuning` and
read it, matching `AssembledFlora`. **This changes existing cells** — Hesperides and the
Wildlife Blitz cells now get the per-element budgets they always authored. The average
effect is ≈ −6% prisms per plant (the four element multipliers average 0.9375), well inside
every phase-hysteresis band, and the *variety* it restores is the point. Re-check Hesperides'
`LiveVolume` against its thresholds on the next pass through that cell.

**Rule:** *a tuning field that appears on every flora config must mean the same thing on
every flora.* A field that silently does nothing on 2 of 3 families is worse than an absent
one, because the author gets no signal.

### 27.3 SpreadElements ate the cell's own layout decisions

`FloraConfigurationSO.RollVariant` replaces this config's whole `Variant` with the palette
sibling's when `SpreadElements` is on — while the field's tooltip claims "planting counts,
periods and probability stay on THIS config, so the cell keeps its own density tuning". Both
`PlantRadiusCellFraction` and `MaxTotalSpawnedObjects` live in that block, so with spread on
a cell could not use the canonical per-element assets **and** choose its own planting shell:
Rampage's belt would have collapsed back onto each species' authored 0.5–0.6, i.e. the middle
of the arena.

Composing the two blocks (cell wins on non-sentinel fields) was considered and **rejected**:
Blob's gyroid configs carry a full duplicate of the Mass element's Variant alongside their
palette, so cell-wins composition would flatten all four gyroid elements into Mass and
destroy exactly the per-element identity §17 exists to express.

**Fixed:** two explicitly-named cell-level overrides on `FloraConfigurationSO` —
`PlantRadiusCellFractionOverride` and `MaxTotalSpawnedObjectsOverride`, both default −1 (off,
so no existing cell changes) — applied **after** the roll via
`TryBuildCellOverrideTuning` → the existing `Flora.ApplyVariantTuning` path, reusing its
"sentinel = keep" semantics rather than inventing a second application mechanism.

**Rule, and the split worth remembering:** *the ELEMENT owns identity* (leaf prism shape,
growth tempo, shield cadence, per-element density) *and the CELL owns layout* (where a
species plants, how big one plant may get in THIS arena). They were in one block because
they were authored together, not because they are the same kind of fact.

### 27.4 A cell whose prisms are not nominal must author its volume ladder

Rampage's hero species is the cactus, whose leaf prism is 5×5×3 = **75 volume — 4.7×
`NominalPrismVolume` (16)**. The cell inherited volume thresholds derived the standard way
(`count × 16`), so its Frenzy ceiling was ~3× too low for the belt it now grows: the cell
would have pinned at Frenzy within seconds, frozen planting, and held a sparse arena that
never regrew — the failure looking exactly like "flora don't spawn", with nothing in the
config pointing at the cause.

Authored explicitly against the belt's estimated volume (~471k at full growth):
`RestlessEnter/Exit 34000/24000`, `FrenzyEnter/Exit 480000/370000`, count backstop unchanged
at 10000/8000.

**Rule (already stated in `ECOSYSTEM_MASTERPLAN.md §5.1` for the low-volume direction, now
with a high-volume instance):** the `×16` derivation is a migration convenience, not a
default. Any cell whose prisms are meaningfully off nominal — a Squirrel trail at ⅕, a
cactus leaf at 4.7× — must author `*EnterVolume` / `*ExitVolume` itself. Verify against
`Cell.LiveVolume` on the DiagnosticsHUD; the estimate cannot be trusted for phyllotactic
species, whose prisms are sized per role and have no single authored volume to read.

### Collider budget

Unchanged from the previous Rampage: the count backstop holds the arena at **10,000
prisms** (~2.8× the Blob envelope, deliberate demolition-arena headroom). The belt seeds
136 plants at ~9,550 prisms, one instantiation per frame (~2.3 s spread, no hitch), leaving
the rest of the budget for player trails. No new physics queries: scoring rides the
`StatsManager` SOAP channel and the AI rides `Cell.GetExplosionTarget`'s Burst density grid.

### Invariants checked

- **Continuity of existence** — untouched; plants still bloom in and wither out.
- **No imposed death** — no decay, lifespan or despawn timer added. The Frenzy ceiling is a
  *growth* gate (planting/growth pause), never a culler; mass stays conserved.
- **No domain asymmetry** — the belt rolls its domain uniformly across all three via
  `CellLifeSpawnerBase.SpawnFlora`; fauna remain controlling-colour only.
- **Every lifeform drops one elemental crystal** — untouched; the belt uses the canonical
  element palettes, so each plant carries its element's heart.
- **Volume is the spine** — reinforced: §27.4 is the whole point of the threshold rework.
- **The Cell owns the environment** — the mode builds no parallel spawner, culler or arena
  edge. Everything above is `CellConfigDataSO` + `SpawnProfileSO` + flora configs.

### 27.5 A planting SHELL is not a forest — the band (Aug 2026)

`plantRadiusCellFraction` gave a species exactly one radius, and `Plant` picked a random
direction on that sphere. Stacking several species at staggered fractions approximates depth,
but each species still reads as a soap bubble, and no cell could put plants *near its core*
without moving the whole species in.

`Flora.plantRadiusCellFractionMin` makes it a **band**, and the draw is uniform by
**VOLUME**, not by radius:

```csharp
r = cbrt( lerp(inner³, outer³, Random.value) )
```

A shell's available space grows as r², so a uniform-in-radius draw crowds plants onto the
inner edge and leaves the outer band — most of the cell — looking empty. Volume-uniform gives
even spatial density through the whole band, which naturally puts most plants in the outer
reaches (that is where the space is) while still landing some in close.

**The inner edge is clamped outside the nucleus**, in code, so an author can write `0` and
get "from the nucleus outward" rather than plants in the core. Three separate reasons make
that a rule and not a nicety: nucleus-interior mass is the territorial CLAIM, it is excluded
from the fauna targeting grids (so a plant there is food the web can never be steered to),
and §27.6 puts the standard crystal respawn in exactly that volume.

Default `min = 0` collapses the band to the legacy single shell, so no existing cell changes.

### 27.6 The crystal volume IS the nucleus — platform coupling (Aug 2026)

`CrystalManager.GetAnchorlessSpawnRadius()` used to resolve **serialized override → nucleus →
crystal SphereRadius**, i.e. any scene could decouple its crystals from its core with one
field. Rampage did exactly that (a 900-unit roam radius, to make the crystal a chase) and it
was wrong for a reason that generalises:

> **The nucleus is the visible marker of the cell's core** — the thing a player reads as "the
> middle". A crystal that respawns anywhere else makes that marker a lie, and every mode that
> contests a crystal then has to teach its own answer to "where do I look".

The precedence is now **nucleus → `noNucleusSpawnRadius` → crystal SphereRadius** (the field
renamed to say what it is). A cell WITH a nucleus always spawns its crystals inside it and no
per-scene field can override that. The fallback exists only for a cell with genuinely no core
(Dog Fight's Boneyard, 420 — and note CLAUDE.md's existing warning that a nucleus-less cell
MUST author it, or the crystal falls through to its own `SphereRadius` and lands on the exact
centre).

**A mode that wants a different crystal volume resizes its NUCLEUS** — author a
`CellConfigDataSO` pointing at a resized `NucleusPrefab`, exactly as Scurry does with
`HalfNucleus.prefab`. That moves both together and keeps them coupled, which is the whole
point. Do not reintroduce a per-scene override.

Note the coupling composes with §27.5: crystals inside the nucleus, flora strictly outside it,
so the two never fight for the same volume and the core stays legible.

### 27.7 The AI's drift look-direction is a mass cluster, not a 180° flip (Aug 2026)

`AIPilot` has a genuinely good idea in it: once the AI has its objective lined up, it DRIFTS —
`VesselStatus.Course` stays locked on the target while the nose swings elsewhere, which is how
a drifting vessel lays trail, skims and fires along an axis that is not its heading. What it
pointed at was `desiredDirection *= -1`: a flat 180° flip away from the objective. That aims
at nothing in particular and reads as the AI spinning on the spot.

It now aims at a **cluster of hostile mass** via `Cell.GetExplosionTarget(myDomain)` — the
exact Burst density-grid query aggression-1 fauna hunt prey with. Two things make that the
right call rather than a new behaviour:

- It is **one system**. "Go where the mass is" already exists on this platform, is already
  Burst, already excludes nucleus-interior and shielded mass (so it can only point at mass the
  AI may attack), and is already sampled on a cadence rather than per frame. A mode-local
  re-derivation of it is the mistake §0 warns about.
- It makes the drift **productive in every mode**, not just the one that prompted it.

Sampled on `massClusterRetargetInterval` (1.5 s), cached in between. Falls back to the legacy
flip when there is no cell, no mass, or the cluster lies within 0.9 dot of the objective (where
the drift would not turn the vessel at all).

**Corollary for mode authors:** do NOT install an `AIPilot.SetExternalTargetProvider` hook in a
mode whose objective is a crystal. The hook overrides crystal seeking outright — Rampage shipped
a two-phase "graze until charged, then break for the crystal" provider and it was removed,
because the platform default (seek the crystal + drift onto mass) already IS that loop.

### 27.8 A client scored nothing for the living world — environment mass is per-peer (Aug 2026)

A 2-player Rampage test: the host scored off everything, the client could only ever score
off the **other pilot's trail** — never off a single cactus it flew through and shattered.

`StatsManager` records prism destruction **server-only** (`_allowRecord`), and two of its own
doc comments state the assumption that justifies it:

> "a prism sits at the same place on the server, so the server's own physics sees a client's
> ram and records it"

That is true of a TRAIL prism — laid from replicated vessel motion, so both peers have one in
the same place — which is exactly why trail kills were the only thing that worked. **It is
false of flora and fauna**, and `CellNetworkSync`'s own class doc has said so all along:

> "Flora and fauna spawning is non-deterministic per-side (each client runs its own
> IntensityWiseLifeSpawner with local Random.value rolls)"

So the server's copy of the cactus a client just shredded is somewhere else entirely. The
client destroys a tree on its screen and nothing is recorded anywhere; whatever the server's
own physics happened to knock over in the same cone is credited instead, uncorrelated with
what that pilot did. In a mode whose entire score is destroyed environment mass, a client is
playing a slot machine.

**Fixed the way the platform already fixes this class**, for the third time:
`Player.ReportEnvironmentPrismDestroyed_ServerRpc`, joining `ReportFaunaKill_ServerRpc`
(fauna have no NetworkObject) and `ReportCombatHit_ServerRpc` (projectiles are not networked).
Same owner-detects → server-records round-trip, same rule that identity comes from RPC
ownership rather than a name string.

**The other half is who must NOT credit.** A client forwarding its own environment kills
would double-count against the server's own simulation, so crediting is split by who
simulates the attacker: `StatsManager.OwnsAttacker` lets the server credit only players it
owns (the host's own, and every AI — both server-owned NetworkObjects) and drop environment
kills it observed a *remote* player make. Rostered victims are untouched: a trail exists
identically on every peer, so it stays server-recorded exactly as before. Each kill lands
exactly once on both paths.

**The rule that surfaced with it:** environment mass was hostile to EVERY domain, because the
only hostility test was the owner-name/roster comparison and a cactus has no roster entry.
`PrismStats` now carries the prism's `OwnDomain` and `StatsManager.IsFriendlyEnvironmentPrism`
applies to the world the same rule trails always had — **your own colour is worth nothing** —
with `Domains.Blue` (the "no team" sentinel) staying hostile to everyone so neutral structure
still scores. A third of a mixed-domain forest is now yours and worthless, which makes domain
a real targeting decision instead of decoration. Ribcage rides the same metric and is
unaffected in practice: its cage is painted across the full triad plus Blue joints, so a team
can still reach a 2,000 target out of ~10,620 prisms.

### 27.9 Corollary — the collecting pilot must run their own crystal effects

Chasing §27.8 turned up why the client's blast was missing entirely:
`OmniCrystalImpactor.AcceptImpactee` opens with `if (IsNetworkClient()) return;`, so a crystal
collection resolves **server-only** — for every vessel, including one a remote client is
flying. Collection *should* be server-authoritative (one machine must decide who got it and
where it goes next), but the **effects** of a pickup are what the pilot sees and feels, and
they were landing only on the server: a client's Dolphin collected the crystal and the jaw
blast, the spent energy meter and the elemental level all happened on a machine that pilot was
not looking at. Their meter never emptied, no cone ever appeared, and — being the mode's only
damage verb — they had almost nothing to report under §27.8 either.

`CrystalManager.ReplayVesselCrystalEffects` (no-op) → `NetworkCrystalManager`'s targeted
ClientRpc now replays the same effect list on the vessel's OWNER. Targeted rather than
broadcast because these effects mutate ONE vessel's state and spawn its blast; every other peer
would be applying them to a vessel it does not own. The server keeps sole authority over
collection, respawn and every stat — this is additive, and the effect list is shared
(`OmniCrystalImpactor.RunVesselEffects`) so the two sides cannot drift.

### 27.10 An objective arrow in a living cell must filter to MANAGED crystals

`Crystal.Active` is every live crystal on the machine, and in a cell with a food web that is
mostly *not* the objective: every flora and fauna carries a heart and drops it on death (the
every-lifeform-drops-a-crystal invariant), and a Dolphin seeds a team crystal every 30 s. In a
mode whose verb is killing flora, the arena rains elemental crystals continuously.

So a nearest-live-crystal objective provider points at the objective almost never. The
discriminator is **`Crystal.CrystalManager`** - non-null only for a crystal spawned by the
cell's `CrystalManager` (`SpawnWithDomain` → `InjectDependencies`, the single writer). Hearts
and seeded crystals are plain `Instantiate`s and carry none, so one test separates them all,
and it is the same test that means "this is the crystal that respawns inside the nucleus
forever" (§27.6). Follow it with `Crystal.CanBeCollected` so a mode that spawns per-domain
managed crystals still only names one the reading pilot may take.

Corollary for any crystal-tracking UI: do **not** blank out on `Crystal.IsExploding`. The flag
stays true for 0.5 s *after* the respawn has already repositioned the crystal, so honouring it
hides the arrow for half a second while the crystal sits at exactly the place it was pointing
to. A collection does not invalidate the target at all - the manager MOVES the same Crystal
object (`UpdateCrystalPos`), so the cached transform follows it to its new home.

---

## 28. Per-intensity forests, and the sticky config choice a client makes too early (Aug 2026)

Rampage gained four intensity levels. Two general capabilities and one platform BUG came out of
it; the mode-specific numbers live in `_Scripts/Controller/Arcade/RAMPAGE.md`.

### 28.1 A cell scales its forest with two scalars, not twenty forked assets

`SpawnProfileSO.FloraPopulationScale` (how many plants — multiplies each species'
`InitialSpawnCount`) and `FloraPlantBudgetScale` (how big each gets — multiplies the live-prism
budget that survives the variant roll and the cell override). Both default 1, so every existing
profile is unchanged and no asset needed migrating.

A SpawnProfile is referenced **from** `CellConfigDataSO`, so it already forks per intensity for
free under `CellTypeChoiceOptions.IntensityWise`. That makes it the natural home for "how much
arena is there", and it keeps the split §27.3 established: **the element owns identity, the cell
owns layout** — now also *quantity*. Forking Rampage's five species four ways would have been 20
assets whose only deltas are two integers each.

Three implementation rules, each learned the hard way:

- **BOTH spawners, or it is dead code.** `Cell.StartSpawnerForMode` picks
  `IntensityWiseLifeSpawner` for exactly the cells that use IntensityWise, and
  `RandomLifeSpawner` for everyone else. A population scalar implemented in only one of them
  does nothing in the very modes that need it. (`CellLifeSpawnerBase`'s own class doc already
  warned about this split; Wildlife Liberation hit it once.)
- **The budget scalar rides `Flora.ApplyVariantTuning`**, as a new
  `FloraVariantTuning.MaxTotalSpawnedObjectsScale` applied AFTER the absolute — one application
  path, so it reaches all three flora families and cannot drift from the overrides it composes
  with. It is a MULTIPLIER because the families ship budgets an order of magnitude apart (400 /
  1000 / 5000); no single absolute could serve them. Sentinel is **-1**, and 0 also means keep:
  a nested serialized class can zero-initialize, and "budget 0" must never be something an
  absent key can mean.
- **Round half UP explicitly** (`Mathf.FloorToInt(x + 0.5f)`). `Mathf.RoundToInt` is banker's
  rounding, which sends an authored 10 × 0.85 to 8 on one species and 9 on the next.

**The scalar and the phase thresholds are ONE change.** The scalar scales the SEED batch — the
fill rate and the opening density — while the Frenzy volume gate is what actually bounds the
standing population. Move one without the other and the forest either tops out at the wrong size
or takes the whole match to get there. Rampage's four ladders are therefore generated, not
hand-authored: `Tools/Build/rampage_intensity.py` computes each intensity's volume from the same
numbers the game reads and self-tests by reproducing the shipped intensity-4 ladder to the digit.

### 28.2 A client could pick a DIFFERENT intensity's cell than the host — silently, permanently

`Cell.AssignConfig` is **sticky** by design (`if (runtime && runtime.Config) return;` — a re-roll
could swap the config out from under a streaming environment). Its IntensityWise arm reads
`gameData.SelectedIntensity`, which on a client arrives **only** in
`MultiplayerMiniGameControllerBase.SyncGameConfigToClients_ClientRpc`. And a client's cell does
not wait for that: it bootstraps off its FIRST CRYSTAL —
`OnCellItemsUpdated` → `InitilizePostFirstCellItem` → lazy `Initialize()` → `AssignConfig()` —
roughly 400 ms after scene load, versus `OnInitializeGame` at `InitDelayMs` 1000 ms.

Lose that race and the SOAP variable still reads its default **0**, `Clamp(0 - 1, 0, n)` yields
index 0, and the client builds intensity 1's arena while the host builds the chosen one. For the
whole match. With no error — the clamp is silent and the default is legal.

This was **already live in every IntensityWise scene** (Dog Fight, Ribcage, Wildlife Liberation,
both Wildlife Blitz cells) before Rampage went near it.

Fixed with three pieces that only work together:

1. **`GameDataSO.GameConfigSynced`** — true immediately on the server, and set as the LAST line
   of the config ClientRpc on a client.
2. **`Cell.IntensityChoiceReady`** gates `AssignConfig`, which now returns **without latching**
   when a connected client cannot yet know its intensity, and warns.
3. **The deferral must be retryable.** `InitilizePostFirstCellItem` used to set
   `postInitilized = true` on its FIRST line, so a deferred bootstrap was permanent — the cell
   would have ended up with no cytoplasm and no spawner at all. The latch moved below the config
   check, `Initialize()` bails before `SpawnVisuals` when the config is still unassigned, and its
   tail finishes any deferred bootstrap. `OnInitializeGame` fires on EVERY peer (the
   `if (!IsServer) return` in `InitializeAfterDelay` comes after it), so the retry always lands.

Fail-safe by construction: if the broadcast never arrives, the cell warns on every attempt and
never silently starts a spawner on the wrong arena.

**Rule for any future per-intensity cell:** a choice that is sticky AND derived from replicated
state must be gated on that state having replicated. `Cell.IntensityIndex` also floors at 1 and
warns when the selected intensity exceeds the authored config count — a mode offering four
intensities over two configs would otherwise serve the same arena for 3 and 4 in silence.

## 29. Intensity as SCARCITY, not size — the fauna density scalar (Aug 2026, Rampage)

Rampage's intensity ladder was rebuilt. It used to thin the FOREST (§28.1: intensity 1 grew half
the plants of intensity 4). It no longer touches the forest at all — **every intensity now grows
intensity 4's arena, prism for prism** — and instead moves two things in opposite directions:

| | I1 | I2 | I3 | I4 |
|---|---|---|---|---|
| omni crystals | 2 × players | players | players − 1 (min 1) | **1** |
| wildlife (`FaunaPopulationScale`) | 1× | 2× | 3× | **4×** |
| forest | 9,830 seeded prisms — **identical at every intensity** | | | |

The mode-specific reasoning is in `_Scripts/Controller/Arcade/RAMPAGE.md`; two platform
capabilities and one general rule came out of it.

### 29.1 `SpawnProfileSO.FaunaPopulationScale` — the fauna twin of §28.1

One scalar, defaulting to 1, multiplying every species' `InitialSpawnCount`, `PopulationSize`
**and `MaxLivePopulation`**. Same argument as the flora scalar: a SpawnProfile forks per
intensity for free under `CellTypeChoiceOptions.IntensityWise`, so it is the natural home for
"how much wildlife is there", while the species assets keep owning what each creature IS.

Rampage makes that argument unavoidable rather than merely tidy: its two species are the
**shared Blob assets**, referenced straight out of `Blob Cell/`. Editing them to stock Rampage
would restock Menu\_Main's lava lamp with it. There was no per-mode asset to tune even if forking
four ways had been acceptable.

**It scales the CAP, and that is the load-bearing half.** `MaxLivePopulation` is documented as a
performance backstop rather than the primary control, which makes it easy to leave alone — but it
is what actually bounds a standing population. The tadpole floor is 4 and its cap is 6, so a
scalar that moved only the floor would be clamped away above ~1.5× and read as doing nothing at
all. Floor and cap move together or the lever is inert.

**Nothing is culled.** Lowering a scale gates PRODUCTION — the seeder stops topping up and
reproduction stops filling — and existing creatures live until starvation or predation takes
them. That is the same permission `FaunaConfigurationSO.ReleaseTier` already has (§0: "gating
production is allowed by the conserved-mass law; culling is not"). A cell that drops its scale
mid-match does not lose a creature to the change itself.

### 29.2 Route population reads through the CELL, not the config

§28.1's rule was "implement it in BOTH spawners or it is dead code in exactly the modes that
asked for it". Fauna has **four** producers, not two — `RandomLifeSpawner`,
`IntensityWiseLifeSpawner`, `Fauna.TryReproduce` (reproduction is the actual population driver)
and the freestyle `Microscene` conveyor — so "remember to apply it in each" is a rule that will
be forgotten. Worse, splitting it is not merely incomplete but *incoherent*: a seeder filling to
24 while reproduction stops at 6 is two ceilings for one number.

So the resolution lives on `Cell`, which all four already hold:

```
Cell.ResolveFaunaPopulation(authored)   // seed counts and caps alike
Cell.ResolveFaunaCap(config)            // MaxLivePopulation, 0 still means uncapped
Cell.IsFaunaAtCap(config)               // the one place the comparison is written
```

There is now no direct read of `cfg.MaxLivePopulation` anywhere outside the config and the
profile. A fifth producer that asks the cell gets the scalar for free; one that reads the config
opts a species out of it silently, which is why `IsFaunaAtCap` exists rather than leaving each
caller to write `cap > 0 && live >= cap` correctly.

**Generalizes:** any future per-cell modifier of a per-species number belongs on `Cell`, for the
same reason. The cell is the only object every producer of life in a biome has in hand.

### 29.3 `CrystalManager.CrystalCountMode.IntensityScaled`

`max(1, round(players × CrystalsPerPlayer) + ExtraCrystals)`, one entry per intensity, list order
= intensity (index 0 is intensity 1) — the same convention as `Cell.CellConfigs`. Two numbers
because the useful answers are not one shape: "twice as many as players" is a multiplier,
"exactly one whatever the roster" is a flat count, and "one fewer than players" is both. Rounds
half UP explicitly, for the §28.1 reason.

Three notes for the next mode that reaches for it:

- **No replication gate is needed, unlike §28.2's sticky cell config.** Both intensity readers on
  `CrystalManager` are server-side: the count is resolved only inside `NetworkCrystalManager`'s
  `IsServer` paths and reaches clients as the replicated slot-list LENGTH. A client never derives
  it. That is the difference between a value a client *computes* and one it *receives*.
- **The roster is `gameData.Players.Count`, and it is allowed to be incomplete.** Rampage spawns
  crystals on client-ready, before everyone has arrived; `NetworkCrystalManager` re-asks on every
  `OnPlayerAdded` and again at turn start, growing the slot list as the roster fills. AI backfill
  counts — an AI is a player holding a Dolphin.
- **The crystals still respawn in the NUCLEUS.** `GetAnchorlessSpawnRadius` is untouched and the
  §27.6 coupling stands: N crystals means N crystals sharing the core, not a crystal roaming to
  make room. A mode that wants them spread out resizes its nucleus.

`CurrentIntensity` also unified the two intensity reads on that class — the serialized SOAP
variable when a scene wires one, `GameDataSO.SelectedIntensity` (the same asset) otherwise. The
anchor lookup previously fell back to intensity 1 whenever the field was unwired.

### 29.4 Collider budget

**Flora:** intensities 1–3 rise to intensity 4's forest — 9,830 seeded prisms, the arena that was
already play-tested and already documented at 2.8× the Blob envelope as deliberate headroom. The
worst case is unchanged; three intensities that used to sit below it no longer do, and the count
backstop (`FrenzyEnter` 10,000) is the same at all four.

**Fauna:** 8 → 32 creatures at cap between intensity 1 and 4 (tadpole 6→24, shark 2→8). That is
the cheap dimension: a tadpole is one body prism plus its heart, a shark a small spindled body,
so the top of the ladder adds tens of prisms against a forest of 9,830, and creature sensing
rides the Burst density grid rather than physics. No new colliders, no new queries.

**Crystals:** at most `2 × players` = 8 at intensity 1, each a single trigger collider.


## 30. A living heart is BLUE, and the crossing to lime TRAVELS (Aug 2026)

**The rule (it was never in doubt): a crystal's ELEMENT is its shape, its COLOUR is who may
collect it.** `Crystal.ApplyColorSetTint` resolves one of three pairs from the live `SO_ColorSet`
and writes them per renderer through a `MaterialPropertyBlock`:

| State | Pair | Reads as |
|---|---|---|
| domain-owned (Jade/Ruby/Gold) | that domain's `BrightCrystalColor` / `DullCrystalColor` | only that domain collects |
| **embedded lifeform heart** (`Crystal.IsEmbedded`) | `BlueColors.BrightCrystalColor` / `DullCrystalColor` | **blue** — it is alive, nobody collects it |
| free pickup (drop / omni / cell) | `EnvironmentColors.BrightCTA` / `DarkCTA`, elementals dimmed | **lime** — anyone collects it |

**None of it reached the screen until 2026-08-15**, because `FadeIn` — which every crystal model
carries, and which is what blooms a crystal into existence per the continuity law — drove
`_opacity` through the *same* per-renderer block and ended the bloom by clearing it. That is
diagnosed and fixed in `Docs/PALETTE.md §2.2` (a palette-wide defect: no crystal in the game ever
showed its resolved colour). What it cost the ECOLOGY specifically is worth recording, because the
symptom was asymmetric and therefore invisible: the fallback was each prefab's authored material,
and of the four elemental crystals Mass and Space author the *Blue* material and looked correct
**by accident**, while `ChargeCrystalMaterial` is literally the CTA pair and Time's Fringe
materials carry a lime dull face. Species assets are split evenly across the four elements — 21
each — so **half the ecosystem's living hearts advertised themselves as free pickups**, and the
half that worked gave no signal anything was wrong.

### 30.1 The crossing is a state change, so it travels

The blue→lime crossing at `ActivateCrystal` is the pickup affordance: it is the moment the §26
wither reaches the core, or the moment a joust frees the heart, and it says *you can take this
now*. It runs the same clock-stamped shape as a prism domain change
(`MaterialPropertyAnimator.ClockColorTransition`, `Docs/PRISM_ANIMATION.md`) — the state goes
final at the start (the crystal is collectable the instant it drops; colour is only how it reads),
the start pair is stamped once against `PrismClock`, the pairs between are computed analytically
from that stamp, and `PrismTimerManager` fires ONE settle at the analytically-known end, which is
what makes the final colour independent of the driver. Duration is
`Crystal.colorTransitionSeconds`, 0.8 s to match the prism transition.

Three rules it depends on, each a bug first — full contract in `Docs/PALETTE.md §2.3`:

- **Paint the flip explicitly.** `ActivateCrystal` repaints itself rather than leaving it to
  `Start` (which fires only because a heart's `Crystal` component is authored **disabled**) or to
  the material lerp's tail (skipped outright when a model has no target material). Rely on either
  and a collectable crystal keeps wearing heart blue.
- **Read the start pair BEFORE anything disturbs it** — before `EmbeddedIn` is cleared and before
  any material lerp drops the block.
- **A cleared block no longer describes the screen**, so `ClearColorSetTint` forgets the resting
  pair.

### 30.2 Collider budget

**Zero.** Colours only — no colliders, no spatial queries, no spawn or consumption behaviour, no
change to what is edible or steerable. The per-frame cost is one property-block write per model
for the 0.8 s a crystal is actually crossing, and only crystals cross.

## 31. The crystal capture — a pickup is a beat, not a journey (Aug 2026)

**Prompter's ask, verbatim in shape:** *"when elemental crystals are captured it looks terrible, and
takes far too long. we need a more satisfying capture effect."*

Every lifeform drops exactly one elemental crystal (§0, the locked invariant), so this is the moment
the whole food web pays out — and it was the weakest frame in the game. What shipped before:

- the crystal **dragged** to the vessel over **3 seconds** (1s on two fauna prefabs, 3s on eleven
  flora prefabs — the duration was authored per prefab, so the same pickup had two speeds);
- it lerped from a **frozen start point** toward a **moving** vessel with a smoothstep, which reads
  as the crystal chasing the ship rather than being pulled into it;
- it flew at **full scale, unlit, unspinning** (the collect disabled nothing, so the idle tumble kept
  running and there was nothing to see change);
- it then **stopped existing** — a bare `Destroy`, except on the Space crystal, which had a 0.6 s
  blendshape shrink bolted on the END of the flight, so that element parked a full-size crystal on
  the hull for over half a second before vanishing;
- **no burst and no pickup SFX.** The omni crystal has played a spent-husk burst + `CrystalCollect`
  since forever (`Crystal.Explode`); the elemental path never called it.

Total: **3.6 s** for the Space crystal, **3.0 s** for the other three, ending in a pop-out.

### 31.1 The shape of a capture

Three beats, and the beats are what make it read as a grab rather than a drag. All feel lives in
**one** asset — `Resources/CrystalCaptureConfig` (`CrystalCaptureConfigSO`) — because a per-prefab
duration is exactly how the old one drifted:

| Beat | Default | What it does |
|---|---|---|
| **Snatch** | 0.08 s | Scale pops to 1.5× and the crystal kicks **away** from the vessel. Anticipation: the recoil is what sells the pull. |
| **Suction** | 0.26 s | Homes on the vessel's **live** position, **accelerating** (`u^2.6`, never linear), swinging in on an arc, spinning up 2.25 revolutions, shrinking to 0.55×, flaring to 3× brightness. |
| **Absorb** | 0.10 s | Rides the hull, collapses to zero scale and dissolves `_opacity` out, and **fires the element's spent-crystal husk into the vessel's wake** — the same burst and the same `CrystalCollect` SFX an omni pickup plays. |

**0.44 s total**, versus 3.0–3.6 s. Everything is sized in **crystal radii** (recoil 0.9, arc 1.6),
so a grown flora heart and a tiny fauna drop capture identically.

Three rules came out of it and generalize:

1. **A flourish must never outlast its own payoff.** The element level lands at *contact* — it is
   applied by `SkimmerAdjustElementLevelByCrystalEffectSO` in the impact frame, and the element's
   petal flower has already ticked over before the crystal has moved. A three-second animation over
   an instantaneous reward does not read as a reward; it reads as lag. `OnCrystalCollected` (the
   scoring event four modes count) now also fires at contact rather than inside the flight loop, so
   a mode's objective can never wait on a visual.
2. **Homing on a moving target is a function of DURATION.** The old lerp was toward
   `vesselTransform.position` read live, which is correct — over 3 seconds a vessel travels far
   enough that "correct" still looks like chasing. Shortening the flight fixed more of the look than
   any easing change could, and the acceleration curve does the rest: the crystal hangs, then snaps.
3. **Continuity of existence applies to a crystal, not just to prisms.** The platform law says
   nothing the player can see may pop in or out. A crystal blooms in through `FadeIn`
   (`_opacity` 0→1) and now leaves the same way — scale to zero *and* `_opacity` back to 0, spent as
   screen-door coverage by the crystal shaders, so it composes with the project's
   dither-not-blend transparency rather than introducing a second kind of fade.

### 31.2 What it composes with, and what it does not add

Nothing new was invented. The burst is `Crystal.Explode`, the existing pooled spent-husk path
(`SpentCrystalPoolManager` → `Impact`), which the elemental crystals were already authored for —
all four prefabs carry a per-element `SpentCrystalPrefab` that had never been reached from a skim.
The flare rides `Crystal.ApplyCaptureVisual`, one MaterialPropertyBlock over the shared material, and
it scales the crystal's **own** colours (RGB only, alpha preserved) rather than washing toward white:
a colour is a rate in linear HDR, so a gain brightens without shifting hue, and washing to white
would read as a *different* crystal (`Docs/PALETTE.md`). No new FMOD event was added — the pickup now
reaches the shared `CrystalCollect` category it always should have.

**The flare composes with the omni/elemental brightness split** (the CTA-lime pass that landed
alongside this branch): `ApplyCaptureVisual` scales whatever the crystal currently *wears*, and an
elemental's resting colour is now the CTA dimmed by `EnvironmentColors.ElementalCrystalDimming`
(0.45). So a captured elemental flares 3× **relative to itself** — which is the ratio the eye reads
over a 0.44 s beat — peaking at ~1.35× the CTA, i.e. just above the omni's resting brightness rather
than the 3× absolute the gain was first chosen against. That is the intended relationship (a crystal
being taken briefly outshines the hero pickup), but it means `flareGain` and
`ElementalCrystalDimming` are coupled: **move one and re-judge the other.** Both scale RGB only
through the shared `Color.ScaleRGB`, so neither can shift hue.

**And that reach was itself broken, on far more than this branch's path.** `Crystal.PlayExplosionAudio`
guarded on its `[Inject] AudioSystem` field, which is null on **every crystal that was not part of a
loaded scene**: a lifeform's heart is `Instantiate`d by the cell's spawners, and *nothing* under
`Controller/Environment` calls `GameObjectInjector.InjectRecursive`. So the pickup sound was a silent
no-op for the entire ecology's crystal drops — and for the conveyor toy's local mints — while reading
as correctly wired, because the guard is exactly what a correct guard looks like. It now falls back
to `AudioSystem.Instance`, the same accessor `SkimmerAdjustElementLevelByCrystalEffectSO` already uses
one frame earlier on the very same pickup. **The general lesson: `[Inject]` on a prefab that some
system spawns at runtime is a REQUEST, not a guarantee** — before relying on an injected field in
anything spawned outside a scene load or a `GameObjectInjector` call site, find the injector. There
may not be one.

`Crystal.Explode` grew one optional argument, `huskScale`: the burst fires at the end of a flight
that has already shrunk the crystal into the hull, and the payoff must be sized by the crystal the
pilot *picked up*, not by the flourish that preceded it. The networked path takes the default and is
unchanged.

The `moveToVesselDuration` / `easeMoveToVessel` fields were removed from the impactor **and** their
now-dead serialized keys stripped from the 15 prefabs that authored them — a value that is read by
nothing but still shows in the inspector is worse than no field at all.

### 31.3 Collider budget

**Zero.** No collider is created; the capture *disables* the crystal's own trigger at contact (as it
always did) and the husk burst is the pre-existing pooled `Impact` path with no colliders at all. The
per-frame cost is one transform write + one MaterialPropertyBlock write on a single crystal for
0.44 s, down from 3.6 s — a ~8× reduction in the live window, and captures are individually rare.
Prisms are untouched, so the clock-material law (`Docs/PRISM_ANIMATION.md`) is not in scope: a
crystal is a handful of objects, not the 2,000-instance surface that law exists to protect.

### 31.4 In-editor verification (the human is the gate)

1. Any scene with fauna — Wildlife Blitz is the fastest. Kill a creature and skim its dropped heart.
   The capture must complete in **under half a second**, ending in a husk burst at your hull with the
   `CrystalCollect` sound. Nothing should linger on the ship. (That sound is a **regression test** as
   much as a feature — it never played for a lifeform drop before this branch; see §31.2.)
2. Do it at **top speed** (Squirrel, boosting). The crystal must land *on* the ship, not trail behind
   it — that is the test the old 3-second lerp failed.
3. Do it on a **Space** crystal specifically: its blendshape pulse now runs *alongside* the flight,
   not after it. There must be no full-size crystal parked on the hull.
4. Joust a lifeform with the Squirrel (`ElementalCrystalImpactor.CollectBy`, §26) — the auto-collect
   runs the identical capture, so it must look the same as a skim.
5. Tune by editing `Resources/CrystalCaptureConfig` **only**. If a capture feels wrong on one
   lifeform and right on another, that is a bug (something is sizing off world scale rather than
   crystal radii), not a reason to re-add a per-prefab duration.

---

## 32. Flora get POPULATIONS — and the gyroid becomes a colony (Aug 2026)

Fauna have had a population pipeline since §6.1: a seed floor, a hard cap, and **reproduction as the
actual driver** — a creature that feeds converts prey into offspring, and the food web bounds the
result. Flora had none of it. A flora species had `InitialSpawnCount` and a plant period, and the
spawner planted **one more plant every period, forever**, bounded only by the cell's Frenzy gate
(`RandomLifeSpawner.SpawnFloraTypeLoop_Random`, `IntensityWiseLifeSpawner.SpawnFloraTypeLoop`).
There was no per-species live count anywhere in the codebase — `Cell` tracked fauna only.

This section adds the plant-side half, and converts the first species to it.

### 32.1 Growth is a plant's feeding

The one design question is what a plant's reproduction is *funded by*. A creature is funded by prey.
A plant is funded by **growth**: the prisms it managed to lay into the space around it. That single
choice is what makes the model work without breaking §0:

> A plant at its live-prism budget **has stopped growing**, so it has stopped funding children. It
> only funds another one after the food web grazes it and it regrows.

So the population is bounded by **grazing**, exactly as the fauna population is bounded by
starvation — and there is no decay clock, no lifespan, no TTL and nothing culled. A lowered cap
stops *production*; it never removes a live plant.

The knobs are on `FloraConfigurationSO` and mirror the fauna block field for field:

| Flora | Fauna counterpart | Meaning |
|---|---|---|
| `PopulationSize` | same | seed floor — **0 keeps the legacy unbounded planting**, so the model is opt-in per species |
| `MaxLivePopulation` | same | hard per-cell plant cap (a performance backstop) |
| `GrowthPerOffspring` | `FeedsPerOffspring` | prisms grown per birth |
| `OffspringPerBirth`, `ReproductionCooldownSeconds` | same | burst throttles |
| `MaturityFraction` | *(no counterpart)* | fraction of its own budget a plant must hold to seed |
| `OffspringSpread` | `OffspringSpawnJitter` (const) | how far a child is planted |

`Flora.AssignLineage` / `SourceConfig` / `NotifyGrew` / `TryReproduce` mirror `Fauna`'s, and the
decision lives in `FloraReproductionRules` (pure, engine-free, edit-mode tested) beside
`FaunaReproductionRules`. The spawners are **demoted to seeders** in both classes — they now fill
only the deficit below the floor, which is bootstrap plus recovery after the food web grazes a
species out, so extinction is never permanent.

**The cap resolves on the `Cell`, never on the config** (`Cell.ResolveFloraPopulation` /
`ResolveFloraCap` / `IsFloraAtCap`) — the §29.2 rule, and flora needs it more than fauna did: there
are **five** flora producers (both spawners, `Flora.TryReproduce`, the freestyle `Microscene`
conveyor, the Lifeform Matrix toy). A cap honoured by one producer is two ceilings for one number.
The initial-batch `FloraPopulationScale` scaling that both spawners used to inline was routed
through the same accessor for the same reason.

### 32.2 The gyroid: one plant became a colony, and the frontier does the connecting

> **Superseded in part by §32.7 (same branch, later passes).** The 27-prism BFS-patch unit cell
> and the single-donor frontier handoff described below shipped first; the octagon colony —
> crystal at the centre of each danger-prism ring, territory ownership, table-driven
> reproduction — replaced them the same week, after the user supplied the tiling design, and a
> later pass moved reproduction itself from the PER-PLANT quota described below to a
> POPULATION cycle (one birth per fauna wave, random open octagon). For a lattice species the
> `GrowthPerOffspring` / `OffspringPerBirth` / `ReproductionCooldownSeconds` machinery below is
> therefore inert — it still governs every NON-lattice flora. The measured bond-table geometry
> below is still the foundation everything §32.7 does is computed from.

The gyroid was one plant that grew forever — a single `AssembledFlora` crystallising a minimal
surface out of 1,500 bonded prisms, carrying **one** heart. It is now a **population of unit cells**,
each with its own crystal.

The size is measured, not chosen by feel. Walking the assembler's own bond table (48 entries, 12
block types) exactly as `AssembledFlora.PreviewGyroid` does: bonds are ~7.8u long, the smallest
closed ring is 3 prisms (the doubled A–B strut junction) with the gyroid's characteristic 8/9/10
rings above it, and the lattice is **BCC with a ~119.6u conventional cube ≈ 584 prisms**. Three bonds
out from a seed is **27 prisms** and is the smallest patch containing **all twelve block types** —
one of everything the assembler can say, ~40 × 42 × 12 world units. That is the unit cell.
(A crystallographic primitive cell, ~292 prisms, is far too big to make a population out of.)

**What makes the pieces add up to a gyroid is that reproduction reuses the growth frontier.**
`AssembledFlora.TryResolveOffspringPlacement` does not scatter a child near its parent. It asks the
parent's own assembler for a growth order — the same `GetGrowthInfo()` call, against the same bond
table, with the same `PrismSpatialIndex.TryReserve` claim that stops two growers filling one site —
and hands that exact position, rotation and `GyroidBlockType` to the daughter
(`ConfigureOffspring` → `SeedFromGrowth` → `CreateNewAssembler`). The daughter's first prism lands
precisely where the parent's next prism would have. **Nothing in the code describes a gyroid**; the
superstructure is emergent from each plant continuing its parent's lattice, which is the whole
point — a scripted superstructure would be the same class of cheat as a scripted fitness function.

Two consequences worth stating:

- **The daughter gets a FRESH depth budget.** `depth` used to be the lattice's global size bound;
  now the per-plant prism budget bounds a plant and the **population cap** bounds the colony.
  Inheriting the parent's remaining depth would make every generation smaller until the colony
  stalled.
- **A blocked plant stays ARMED.** A plant that has banked its quota but is blocked by the cap keeps
  its quota (`Flora.TryReproduce` spends it only on a birth that happened). A full plant is never
  re-armed by another growth tick, so without this it could never fill the gap left when a
  neighbour is grazed out. With it, gap-filling is automatic.

### 32.3 What the conversion preserves, and what it costs

Numbers are authored by `Tools/Build/author_flora_populations.py` (`--check` verifies the assets
still match the model), not by hand. The lattice rule is `cap = old_single_plant_budget / 24` (the
octagon patch — it was `/ 27`, the BFS unit cell, before §32.7), so **total prism mass is preserved
to within a rounding step and a clamp** — Blob's Mass gyroid goes from 1 plant × 1500 to 60 plants ×
24 = 1,440 (its unclamped cap would be 63). Leaf size and the level spread are untouched, so
**per-prism volume is unchanged.**

> **One later correction (§32.7 seventh pass):** this section originally added "and no cell's
> volume phase ladder needs re-authoring". That was true of the CONVERSION and false of the
> colony: preserving per-prism volume says nothing about the ladder being right in the first
> place, and Blob's was set so low that its seeded floor alone was 87% of Frenzy. Blob's ladder
> is now authored ×5. Per-prism volume is still unchanged by anything in this section.

**The cost is crystals, and it is the collider line to watch.** Every plant is a lifeform, so it
carries one heart whose collider is always on and is *not* phase-LOD culled (§21.6). Blob's three
gyroid species go from **3 crystals to 135** at cap (60 + 42 + 33); Blob's profile is shared by the
freestyle seven, so the same figure applies to Caldera / Daedala / Geode / Orrery / Ourobor / Yggdra
/ Zephyr. The cap is a backstop, not a prediction — with Blob's ×5 ladder the cell reaches Frenzy
at **~69 plants**, which is the figure to hold against the budget. Against the masterplan's
~1,500-collider per-cell target that is ~5% realized (~9% at the cap), and
`MaxLivePopulation` is the dial — the authoring script clamps every species to
`MAX_PLANTS_PER_SPECIES = 60` for exactly this reason. Prism colliders are unchanged in count.

There is a real gameplay consequence, and it is the interesting one: a 27-prism plant can be grazed
to death, where a 1,500-prism plant could only ever be dented. The gyroid becomes a **crop** — fauna
clear a unit cell, it dies and drops its elemental crystal, and its neighbours colonise the hole.
That is the food web finally having a visible effect on the cell's canopy rather than nibbling it.

*(Watch on the first playtest: `GyroidFlora.prefab` authors `minHealthBlocks: 5`, so a plant dies with
5 prisms still standing and `LifeForm.DestroyStructure` detonates them. At 1,500 prisms that leak was
0.3% of a plant; at 24 it is 21%. It is pre-existing behaviour and out of scope here, but if the
colony visibly loses mass over a long session, that is where it is going — the fix is
`minHealthBlocks: 0`, not a change to the population model.)*

### 32.4 Collider budget

- **Prism colliders: unchanged.** Mass is preserved by construction (§32.3); the same prisms are
  simply owned by more plants.
- **Crystal colliders: +132 per gyroid cell at the cap** (3 → 135), ~66 realized under Blob's
  ×5 volume ladder (§32.7), +~1 per plant elsewhere. Bounded by
  `MaxLivePopulation`, clamped to ≤ 60 per species by the authoring script, and scaled with the rest
  by `SpawnProfileSO.FloraPopulationScale` (which now moves the floor **and** the cap — a scalar that
  moved only the floor would be clamped away by the cap and read as doing nothing, §29.2).
- **No new queries.** Reproduction reuses the growth `TryReserve` the assembler already performed;
  no `Physics.OverlapSphere` is added anywhere. (The 2026-04 attempt on
  `claude/gyroid-seed-danger-prism-U3MnJ` used `Physics.OverlapSphere` to find a neighbouring flora
  by crystal proximity — banned by `Docs/SPATIAL_INDEX.md`, and structurally blind besides, since
  prism colliders are disabled for the first 0.6 s after spawn.)
- **Per-frame CPU:** one `TryReproduce` call per plant per grow tick, which fails on the first
  integer compare for any species that authors no reproduction. A lattice plant additionally runs
  `TickOctagonPopulation` per grow tick: a bool, a clock compare, and — once in its life, on the
  tick it matures — one projection of its ring through the neighbour tables. The reproduction
  cycle itself is one dictionary lookup per plant per tick and a random pop once per fauna wave.

### 32.5 Invariant review (the rulings, recorded)

- **Mass is conserved / no imposed death** — nothing is removed. Reproduction only *creates*, and it
  is gated on `Cell.FloraPlantingEnabled` so it freezes with planting at Frenzy. A cap stops
  production; §0 explicitly permits gating production. Both retired growth-side cheats stay retired:
  no regrowth pulse, no timed culler.
- **Continuity of existence** — an offspring is an ordinary plant spawned through the one canonical
  path (`CellLifeSpawnerBase.SpawnFlora`), so its prisms bloom in on the existing grow path. Nothing
  new pops.
- **No domain asymmetry** — the *spawner* still seeds uniformly across Jade/Ruby/Gold
  (`PickRandomDomain`). Within a lineage a plant's children are its own colour, which is exactly the
  fauna rule (§6.1) and not a per-domain bias.
- **Every lifeform drops one elemental crystal** — each unit cell is a full lifeform with its own
  heart. This is the invariant the change *leans into*: it is what the user asked for, and it is why
  §32.3 states the crystal cost so plainly.
- **Volume is the spine** — untouched. Prism count and per-prism volume are both preserved, so no
  threshold moves.
- **Endogenous selection only** — a colony survives by growing and dies by being eaten. No fitness
  function anywhere; offspring inherit their founder's variant pick (element + hatch level), and
  in-world level-ups are not inherited, matching §17 and the fauna rule.

### 32.6 In-editor verification (the human is the gate)

**Status: RUN, across five Menu_Main playtests (2026-08-15/16).** Steps 1-3 below passed on the
final pass — the surface reads as one continuous gyroid, daughters mate with their parents, and
each completed window holds exactly one non-growing crystal. The numbers in this section are the
ORIGINAL unit-cell model's; the shipped model is the octagon colony (§32.7), whose own verification
steps and heartbeat decode live at the end of that section — **use those.** Steps 4-7 here were
NOT re-run after the octagon conversion and remain open:

4. **Graze test.** Let the tadpoles work a patch. A grazed octagon should die, drop its crystal,
   and the population should recolonise the hole (its centre returns to the claim book on death,
   and neighbouring plants re-offer it). Nothing should vanish without being eaten.
5. **Wildlife Blitz Cell 4** — the same three species under `IntensityWiseLifeSpawner`. This is the
   check that the seeder change landed in *both* spawner classes; if Cell 4's gyroids behave
   differently from Blob's, one of them is running the other code path. **Note Cell 4 did NOT get
   the ×5 volume ladder** (that was authored on Blob alone, §32.7 seventh pass), so its colonies
   will still stop at the old ceiling — expected, not a defect.
6. **Hesperides** — the gyroid topiary is now 8 small plants instead of one 190-prism specimen.
   Confirm it still reads as topiary in the garden.
7. Re-run `python3 Tools/Build/author_flora_populations.py --check` after any asset tuning,
   `python3 Tools/Build/verify_gyroid_octagon_tables.py` after any bond-table or octagon-table
   edit, and `FrogletTools ▸ Validation ▸ Validate Lifeform Crystals` (every octagon is a lifeform
   now, so the one-crystal rule is being asserted far more often than before).

### 32.7 The octagon colony — a crystal in every window (Aug 2026, second pass)

The first pass (§32.2) made the gyroid a population, but its unit was arbitrary — "3 bonds out
from a seed" — and its reproduction handed a daughter one donor site. The design that replaced
it came from the tiling itself:

**The gyroid's 12 block types are a non-Euclidean tile** — 6 prisms and their 6 mirror images
(the conjugate structure: DE↔EsD, EG↔GEs are the danger pairs). The four danger types close
into rings of exactly **eight danger prisms** — measured, not assumed: the danger-only bond
subgraph contains ONLY 8-cycles (120 of 120 in a 4,000-prism walk), each ring 2×DE + 2×EG +
2×EsD + 2×GEs, radius 10.03u. Since danger types are 4 of 12 equidistributed types, each ring
owns **24 prisms** of the surface: 8 ring + 16 between. Adjacent ring centres sit 35.9–42.4u
apart, and each danger type sees exactly **four** neighbouring rings at fixed local offsets
with a deterministic seed pose each (measured purity 1.00, position std ≤ 0.25u).

> **The spec said 48 prisms per lifeform (4 tiles); the lattice says 24 (2 tiles).** This is
> forced, not chosen: with a crystal in EVERY octagon and no overlapping prisms, prisms per
> lifeform = total ÷ octagons = 8 ÷ ⅓ = 24 exactly. A 48-prism lifeform is only possible with
> crystals in every OTHER octagon. The build follows the stronger constraints (crystal per
> octagon, no overlap); flag if the other trade was meant.

**A gyroid plant IS an octagon-owner.** Its crystal sits at the ring's centre and **never
grows** (`crystalGrowth 0` + a code guard); its root is the centre, so "a crystal at local
(0,0,0)". A founder discovers its centre from the first danger prism it grows
(`GyroidOctagonData.TryGetOwnCenterOffset` — each danger type knows its ring centre in its own
local frame) and claims it in `GyroidOctagonRegistry`; a daughter is handed hers, pre-claimed
by the parent so siblings cannot race.

**Territory makes the tiling.** A plant grows a site only if it lies within `TerritoryRadius`
(26.5u) of its own centre AND no other claimed centre is meaningfully nearer
(`AssembledFlora.OwnsLatticeSite`, epsilon 0.75 — boundary prisms sit EXACTLY equidistant, so
both owners contest and the spatial-index reservation keeps whichever grew first; patches
measure 22–28). A declined site is marked filled on the assembler
(`GyroidAssembler.DeclineGrowthSite`) so the branch moves on — the neighbouring plant lays the
same world position from its own lattice.

**Reproduction is the neighbour table.** A full plant projects, from each ring prism it grew,
the four neighbouring ring centres; for each UNCLAIMED one whose seed site is free it plants a
daughter — root at that centre, first prism a real member of that ring, block type and pose
from the table (`OctagonNeighbor`). "Calculate where the neighbouring crystals belong, check
if one is already there, plant where there is not." `OffspringPerBirth 8` covers the whole
neighbourhood per birth; the armed quota retries the rest.

**Proof before Unity.** The exact algorithm (bond-table growth + ownership + registry +
neighbour-table reproduction) was simulated end-to-end in Python: from one founder, 273 plants
/ 5,547 prisms — a **single connected component**, **zero overlaps** (min pairwise distance
7.17u vs 6.6u lattice minimum), **bijective on the reference lattice** (max deviation 0.74u at
radius 95, no double-filled site), and **175 of 175 complete octagons holding exactly one
claimed crystal centre**. Float drift off the bond table accumulates ~0.3u per 100u of lattice
— absorbed by the reservation clear radius (~3.1u) out to radius ~1,000.

**The Gyroid Lab was the test chamber, and is RETIRED (2026-08-16).** It was a Cell-Selector
station on an environment-free config with every guardrail off — uncapped population, no
fauna to graze the specimen, a phase ladder that could never reach Frenzy — so one founder
colonised indefinitely and the growth rule could be watched in isolation. It earned its keep
(it found the daughter-stall bug, the premature-reproduction cascade and the twinning
geography below), and then it stopped being informative: **a cell with no gates only ever
answers questions about itself.** Its last playtest read as a runaway shell precisely because
guardrail-free was its whole design — while the same code in the shipped freestyle biome grew
correctly. Removed from Menu_Main's `CellConfigs` and deleted; the shipped biomes are the
honest test. If a future rule change needs a chamber again, `author_flora_populations.py`'s
`EXCLUDE` hook is still there for it.

**First playtest (2026-08-15) found the daughter-stall bug.** Daughters planted (crystal +
seed prism visible) but never grew. Root cause: `CreateNewAssembler` ran
`SetParent(spindle, worldPositionStays: false)` on the seed prism WITHOUT zeroing the locals —
`worldPositionStays: false` keeps the local values, which at that point are the world
coordinates `Instantiate` assigned, so the prism landed at `spindle.pos + spindle.rot × worldPos`
(~2x its own distance from the origin, in empty space). The legacy code only ever worked
because a spawner flora ran this while still parked at the cell centre (world ≈ 0, stale local
≈ 0); an octagon daughter is created AT her centre, so her seed prism was thrown into space,
the ownership gate declined every garbage site, and the plant reseed-looped forever.
`ExecuteGrowOrder` always zeroed the locals; the fix copies it. The same fix repairs the
Lifeform Matrix toy's pinned-station assembled flora, broken the same way for as long as the
toy has existed. `Docs/PRISM_ANIMATION.md`-style lesson: a parenting call's semantics
(`worldPositionStays`) are load-bearing — audit both spawn paths whenever one changes.

**The Lab is tuned as a speed chamber** (same playtest's request): ONE **Time** gyroid
(the fastest authored tempo, pushed further: GrowPeriod 0.1) seeded at the **cell centre** —
the config removes its `NucleusPrefab`, because "never plant inside the nucleus" is exactly
the clamp that keeps a plant off the centre, and a cell with no nucleus HAS no such zone (a
supported state, §25.1's declaration, not a rule exception). Every pacing guard is opened in
CONFIG, not on the shared prefab: `FloraVariantTuning` grew `ItemsPerGrow` / `RandomItems` /
`MaxSpawnsPerFrame` overrides (sentinel -1 = keep prefab) so the Lab authors 8 / 0 / 3 while
every other biome's plants keep the shipped pacing. Reproduction: quota 12 (plants colonise
while half-grown, so the frontier expands ahead of completion), cooldown 0.25s, maturity 0,
seeder delay 0. The Frenzy gate was already unreachable. Expansion is frontier-limited by
design: interior plants complete and stop, so the active grower count tracks the colony's
surface, not its volume.

**Second playtest (2026-08-15): the crystal cloud, and the maturity gate.** With reproduction
armed at quota 12 ("colonise while half-grown"), each half-grown plant minted 8
crystal-bearing daughters per birth - the population octupled per ~half-second generation
while growth lagged behind, and the cell filled with an exponential cloud of hearts and seed
spindles far beyond any grown surface. The rule that fixes it is the one the playtest asked
for: **a gyroid reproduces only when it has fully grown all its spindles and health prisms.**
`AssembledFlora.OctagonMature` = a real patch (count ≥ 18) AND an exhausted frontier (a run of
grow ticks that decided nothing, with no orders pending; budget-full ticks count as idle).
Deliberately NOT a fixed prism count or ring-complete test - patch sizes legitimately vary
22-28+, and a plant whose near ring-arc was pre-grown by its parent under the boundary epsilon
would stall forever against either. The banked quota simply waits at the gate. Re-simulated
with the exact rule: 167 plants / 3,301 prisms, 19.8 prisms per crystal (was a seed-plant
flood), single connected lattice, zero overlaps, immature plants = exactly the one-generation
frontier shell.

**Third playtest (2026-08-15): "full size" means the BLOOM, and the overlap suspects.** Two
observations: generations still cascaded before prisms reached full size, and real overlapping
prisms accumulated at the centre. The first is a units mismatch now fixed: the maturity gate's
frontier-idle test settles in fractions of a second (2 grow ticks), while a prism's grow-in
bloom takes SECONDS (`Prism.growthRate` 0.01) - so a plant read as "fully grown" while every
prism was mid-bloom. `FloraVariantTuning.MaturationSeconds` (default 4, Lab authors 5) now
requires the plant's YOUNGEST prism to be older than the bloom before it may reproduce -
fully-formed plants begetting fully-formed plants, paced to the animation the player watches.
Three overlap/hole suspects were closed or instrumented in the same pass: (1) the claim/reserve
ORDER in reproduction - a seed reservation abandoned on a lost claim race sat until TTL while
every neighbouring branch that probed the site marked it PERMANENTLY skipped (reserve-fail
reads as "occupied for real"), a transient race punching a lasting hole; centres now claim
first and release on failure. (2) The founder's seed prism registered with the spatial index at
its pre-`Plant()` position and was then dragged to the planting point - occupancy reads the
STORED position, so its real location read as empty space another grower could fill
(`NotifyPositionChanged` after the move; pre-dated this branch for every dispersed assembled
flora). (3) `GetGrowthInfo` grows UNCHECKED when the spatial index is unavailable - the one
path that can double-fill a site at scale; now counted (`UNRESERVED` in the heartbeat), because
a non-zero count alongside overlapping prisms is the diagnosis and the fix is index
availability, not growth logic.

**Fourth playtest (2026-08-15): growth pacing confirmed excellent; lattice "twinning"
defects.** Fully-formed plants now beget fully-formed plants, but the surface showed defects
"like twinning in crystallography" - prisms reading as ~90° misrotated, errors compounding,
and crystals appearing outside some octagonal rings. Offline analysis exonerated the
mathematical suspects (LookRotation degeneracy margins ≥0.9999 across all 48 bond entries;
float32 walk bit-equivalent to float64; `ToGlobal` is scale-free; the quaternion bake in the
E-table verified against Unity's convention branch by branch), so the defect enters through a
Unity-runtime interaction the simulation could not see. Two responses shipped:

1. **The lattice-defect auditor** (`GyroidColonyDiagnostics`): every grow decision and every
   daughter seed handoff is checked for a prism 3.1-5.5u away (TryReserve already cleared
   ~3.1u; the healthy lattice's minimum non-bonded spacing is 6.6u, so a hit means a
   misaligned lattice domain being minted), counted separately for GROWN sites (bond-table
   continuation - drift or seam defects) and SEED sites (the E-table handoff - one bad
   handoff twins a whole subtree), with the first 24 logged at their world positions.
   `MaxRingCoherenceError` tracks the worst computed-vs-claimed ring-centre disagreement
   (healthy < 1u), and `RotationFallbacks` counts `SafeLookRotation` failures (offline says
   the table never produces one, so non-zero fingers a post-spawn transform write). All in
   the 5s colony heartbeat line.

2. **The orphan-reservation seam race - found by reading, fixed to match the sim.** The
   validated colony simulation used perfect-information reservations; Unity's had a 5s TTL,
   and three paths abandoned live reservations into it. The systematic one: the ownership
   gate DECLINES a foreign boundary site with the reservation `GetGrowthInfo` just made
   still live. The site's true owner - a sister plant growing at 0.1-1s cadence - probes the
   same world position within the window with near-certainty, its `TryReserve` fails, and
   `GetGrowthInfo` treats reserve-failure as "occupied for real": the owner's bond site is
   marked bonded PERMANENTLY. Roughly every seam site whose first prober was the non-owner
   became a lasting hole - missing danger prisms, open ring arcs, and crystals apparently
   sitting outside their octagons, concentrated exactly where plants meet. All abandoning
   paths now release explicitly: the decline itself, an age-dropped grow order (which
   otherwise fails against its OWN stale claim on re-decision), a died plant's pending
   queue, and a stranded offspring seed (both the octagon and the generic donated-growth
   flavours, plus `OnDestroy`). Reserve-failure still means "someone real is there" - that
   is what makes marking-bonded-on-failure correct again.

**Fifth playtest (2026-08-16): the auditor attributed it - a CHIRALITY corruption in the
baked tables, plus frame-poisoned rings.** The heartbeat read `DEFECTS grown=482 seed=140
ringErrMax=11.79` with `claims=3` before any birth, and the user's diagnosis ("there is
subtle chirality at play... double check everything is the correct handedness") was exactly
right. The numeric end-to-end check (reconstruct daughter seed poses from the BAKED C#
quaternions under Unity's exact composition, then test them against the reference lattice)
convicted the table: **12 of the 16 baked `SeedRotation` quaternions - all of the EG, EsD
and GEs rows - were never the measurement.** When the emitted table was transcribed into
`GyroidOctagonData.cs`, only the DE row came from the emit; the other three types were
constructed by a z-mirror symmetry ansatz (centres z-negated, quaternions (x,w)-negated).
The gyroid's enantiomer conjugation does NOT act on the LookRotation frames that way, so
seed rotations were wrong by up to 179° - a daughter seeded through DE mated perfectly,
one seeded through the other three types grew an internally-perfect lattice that could not
mate with its parent ("good looking lifeforms not matching up with other good looking
lifeforms"). The simulation had validated the measured MATRICES; the quaternion bake was
the one unvalidated link. Resolution, four parts:

1. **The table is re-baked from the verified emit** - all 16 entries measured, none derived.
   Re-running the end-to-end check against the shipped file: self-centre coherence 0.01u,
   every seed pose lands on a reference-lattice prism (worst 0.37u / 0.74°), subtrees mate
   to 0.63u. `Tools/Build/measure_gyroid_octagons.py` now emits the COMPLETE C# block
   itself (exact per-class sample poses - never averaged rotations, where a det -1
   reflection can hide - with quaternions and a per-entry self-centre assertion), so
   regeneration is paste-verbatim and hand-derivation has no step left to slip into.
2. **A daughter asserts her handoff at birth** (`IncoherentHandoffs` / `MaxSeedHandoffError`
   in the heartbeat): her seed pose must recompute the centre she adopted to <1u, else a
   loud error names the table. A future table regression costs one birth, not five playtests.
3. **Ring membership is a COHERENCE test** (`GyroidOctagonData.RingMemberToleranceRadius`,
   2.5u), separate from claim identity (`CenterDedupeRadius`, 12u). The playtest recorded an
   11.79u admission - a foreign-frame danger prism joining a ring, whose pose reproduction
   then projected the neighbour tables from: a chimera lineage. Poison-band admissions are
   now rejected and counted (`RingPoisonRejected`).
4. **The misalignment auditor became a GATE**: a grow decision or daughter seed site with
   standing mass 3.1-5.5u away (coherent minimum is 6.6u) is DECLINED - reservation and
   claim released - instead of grown. Lattice frames that were never projected from one
   another cannot mate (`claims=3` before any birth = three independent founders, the third
   playtest's centre chaos ball), so where independent frames meet, the colonies now stop
   at a clean interface instead of interpenetrating. The FOUNDER log names each frame's
   origin (`lineage=` config, or `NONE/toy` for a Lifeform Matrix planting).

**Sixth pass (2026-08-16, chirality confirmed fixed): reproduction became a POPULATION
event - the organic-growth model.** With the lattice mating correctly ("everything is
perfect now"), the per-plant reproduction drive was retired for the octagon colony: every
mature plant independently planting all its neighbours produced a breadth-first spherical
wavefront, where the old single-plant gyroid grew organically - wandering prism by prism.
The colony now wanders the same way at the level of whole flora:

- **Completing growth earns a place in the reproduction pool.** The first grow tick on
  which a plant reads fully grown (`OctagonMature`), it contributes every unclaimed
  neighbouring ring centre - with the full seed pose projected from its measured ring - to
  **`GyroidColonyFrontier`**, the population's per-species book of open octagons (deduped
  against the claim book and against duplicate offers; several plants border the same
  window).
- **One new lifeform for the whole population per cycle.** The cycle rides the cell's
  fauna-wave cadence (`Cell.CurrentFaunaSpawnPeriod` - the ecosystem's one heartbeat),
  staggered ~0.35s so a birth never shares a frame with a wave's instantiation burst. Any
  living plant's grow tick may cross the clock boundary; the first one owns the cycle -
  main-thread, one at a time, **no race by construction**. Missed cycles under a hold
  (Frenzy, cap) are skipped, never burst-fired, and skipping burns no frontier entries.
- **The site is a uniformly random pop across every complete plant's frontier** - the
  de-sphering. The popped entry's contributor is the lineage donor (domain + variant breed
  true); if the food web took it since it offered, the ticking plant stands in - "the
  chosen one can seed off ANY complete plant". Validation per birth is a point lookup
  against the crystal claim book plus the one seed-site reservation + misalignment gate -
  a rare, cheap event, not a per-prism occupancy sweep.
- The per-plant quota machinery (`Flora.TryReproduce`) is untouched for every other
  species and harmlessly inert here: octagon placement only ever returns a target staged
  by the population cycle (`TrySpawnFrontierDaughter` → `Flora.TrySpawnOneOffspring`, the
  new one-offspring entry point that still passes the universal Frenzy + cap gates).
  `GrowthPerOffspring` / `OffspringPerBirth` / `ReproductionCooldownSeconds` no longer
  drive this species; the cadence dial is the spawn profile's `BaseFaunaSpawnTime`.

**Seventh pass (2026-08-16): the colony's ceiling is the CELL'S VOLUME LADDER, not its
population cap.** With growth and mating both correct, the freestyle colonies still stopped
early, and the instinct — raise `MaxLivePopulation` — would have been **dead tuning**. The
arithmetic says why. The Blob cell (Menu_Main's freestyle / lava-lamp cell, and the Wanderway
host) authored `FrenzyEnterVolume 57,600`, while its three gyroid species carry prisms far
above the nominal 16: **Mass 7×4.5×3.5 = 110 volume (6.9× nominal)**, Time 45.9, Space 20,
all multiplied again by the level spread (`LeafScalePerLevel 1.15` on each axis = ×1.52 per
level; ×2.74 averaged over levels 1-5). One settled plant is therefore ~7,900 (Mass) /
~3,300 (Time) / ~1,400 (Space) volume, so the **seeded floor alone — 4 founders × 3 species —
is ~50,200 volume, 87% of the Frenzy threshold before a single birth.** The colony froze
after roughly one wave. Its population caps (60/42/33 = 135 plants) sat ~19× further out and
were never in play.

The ladder is now authored ×5 (`RestlessEnterVolume 56,000` / exit 40,000,
`FrenzyEnterVolume 288,000` / exit 240,000), which is ~69 plants and ~1,790 prisms of mixed
colony — so the caps still sit above it and remain what they were designed to be, a crystal
(collider) backstop rather than the growth bound. The count backstop moved 3,600 → 5,400
(exit 3,000 → 4,500) for one reason only: the new volume ceiling filled entirely with the
THINNEST gyroid species would be ~5,255 prisms, so a 3,600 count would have preempted the
volume spine in that case; above ~5,400 is no longer the ladder, it is a runaway.
**Collider impact:** ~69 crystals (one always-on heart collider each) where the cell
previously reached ~14, and ~1,790 prisms where it reached ~360 — a lava-lamp-only change
(Blob config; no other biome touched). Fauna aggression also becomes a real ladder here
rather than a pin: the cell used to sit at Frenzy (Level2 berserk) from its seeded floor
onward, and now climbs Calm → Restless → Frenzy as the colony actually fills.

**The general rule, and it has now bitten twice** (Rampage's cactus leaves, §27; this):
**a cell whose prisms are not nominal-sized must author its volume ladder against MEASURED
prism volume, and the level spread is part of that measurement.** A species whose leaf is 7×
nominal and whose levels multiply it another 2.7× reaches a count-derived ladder ~19× too
early, and the symptom is never "the ladder is wrong" — it is "my population stopped growing",
which sends you to the population dial, which is not connected.

In-editor verification (the human is the gate): enter freestyle in Menu_Main (the lava lamp
IS the test now). Watch: (1) the founder's first danger prism moves the
crystal to the ring centre; (2) ONE new plant blooms per fauna-wave period, at a random
edge of the colony - the surface should visibly WANDER, not inflate as a ball; (3) the
surface stays ONE continuous gyroid with no doubled prisms, a non-growing crystal in each
completed window; (4) growth continues to roughly 5× the old standing colony before the cell
reaches Frenzy and freezes (an active force - grazing, a vessel ability - resumes it). Read the heartbeat: `frontier=` is the population's
open-site pool (grows by ~10-14 per maturation, shrinks by one per birth); `HANDOFF bad=`
MUST stay 0 (non-zero = the tables are wrong in-engine - regenerate with
`Tools/Build/measure_gyroid_octagons.py` and paste its emit verbatim); `BLOCKED grown/seed`
and `poison` count misaligned-frame contacts (expected only where independent founders'
colonies meet); `ringErrMax` should sit well under 1; `UNRESERVED` non-zero → the spatial
index was unavailable and growth ran unchecked.

## 33. Schwarz P grows on its own TILE — the hyperbolic {6,4}, which is one half-period cube (Aug 2026)

`SchwarzPFlora` crystallises the Schwarz P minimal surface —
`f(x,y,z) = cos x + cos y + cos z = 0` — one prism at a time. It always did. What changed is
**what it thinks the surface's neighbourhood structure is**.

**What it was doing.** The original `SchwarzPAssembler` marched a *quasi-square array*: from
each prism, step a tangent direction by `separationDistance`, Newton-project back onto the
zero level set, orient to the gradient, parallel-transport the heading, repeat. It works —
it shipped, and it produces a surface — but it is an approximation of a lattice the surface
does not have. **Schwarz P is intrinsically HYPERBOLIC** (K ≤ 0 everywhere), so it admits no
Euclidean lattice at all, and a square-ish array on it can only ever be a fit. The
consequences were structural, not cosmetic:

- every position was computed from the previous one, so the walk **accumulated drift**;
- two growth fronts arriving at the same place from different directions **did not agree**,
  so occupancy had to be a **quantized float key** (`RoundToInt(param / (step/2))`) to paper
  over the mismatch;
- there was **no repeat unit**, so nothing about the growth could be baked, measured, or
  verified — only played and eyeballed.

**What it does now.** The surface does carry an exact non-Euclidean tiling: the hyperbolic
**{6,4}** — hexagons with 90° corners, four to a vertex — and on this surface it turns out to
be startlingly concrete:

> **The tile is the patch of surface inside one half-period cube.**

The {100} mirror planes (`x, y, z ∈ πZ`) cut space into cubes of side π. Each cube holds
exactly one **flat point** (K = 0, normal along a body diagonal), and the patch inside it is
one hyperbolic hexagon: **six edges**, one on each cube face, every one a planar geodesic
because the face is a mirror; **six corners**, on the six cube edges whose ends straddle the
surface, every one a 4-fold point of the surface lying *exactly* in the flat point's tangent
plane at the vertices of a regular hexagon of circumradius `π/√2`; and **six neighbours** —
the six face-adjacent cubes.

So **tile adjacency is simple-cubic adjacency**. A prism's address is a `Vector3Int` plus a
site index, occupancy is exact integer bookkeeping, and a site's position is arithmetic on a
measured offset. No Newton iteration and no quantization survive anywhere in the growth path.

Each hexagon is **12 copies of the *246 Schwarz triangle** — the measuring script gets
(30°, 45°, 90°) to nine decimals, the signature of the triangle group the P surface's symmetry
quotients onto — with corners at the flat point (order 6), a tile corner (order 4) and a tile
edge midpoint (order 2), and edges on the mirrors `{y = π/2, x+z = π}` (a **straight line
lying in the surface**), `{y = z}` and `{x = 0}`. Per cubic unit cell the tiling closes at
**F = 8, E = 24, V = 12, χ = −4** — genus 3, exactly what the P surface must be.

**Adjacent tiles are mirror images across their shared cube face**, so tile `(i,j,k)` is the
canonical tile carried by `T_ijk`, acting one axis at a time: `x → x + πi` when `i` is even,
`x → π(i+1) − x` when `i` is odd. `f` is invariant under every `T_ijk`, so the whole surface
is one baked patch plus a sign flip per odd axis.

### 33.1 What is measured, and what is proven

The tile's **combinatorics are exact and proven**, not fitted. The one fitted quantity is how
finely the tile is filled with prisms — a hyperbolic patch admits no uniform lattice, so a
covering has to be measured. Each level is seeded from a triangular lattice on the flat
point's 6-fold axis, lifted onto the patch, then equalized by a **centroidal Voronoi
relaxation in which every site competes with every image of every site under the full symmetry
group — including its own mirror image across each tile seam**. Uniform spacing inside a tile
and uniform spacing across a tile boundary therefore fall out of one computation, with nothing
tuned for the seam (measured: seam-to-intra ratio **1.00×** at every level).

Levels land on **complete hexagonal shells around the flat point** — 6, 18, 36, 60, 90 sites,
i.e. `3n(n+1)`, the centered hexagonal numbers with the centre removed (the flat point is the
plant's CRYSTAL seat, §33.6, not a prism site). `separationDistance` stays the authored field
and now *selects* a level (`ResolveLevel`) rather than setting a step; at the shipped
`SchwarzPFlora` (`separation 6`, `periodScale 60`) that is **level 2, 36 sites per tile at
5.25 world units** — so no asset needed re-authoring, and the orphaned
`overlapProbeScale: 0.45` already sitting in `SchwarzPBlock Variant.prefab` became a live field
again.

**No rotation is baked.** Half the `T_ijk` are reflections, and a baked quaternion carried
through one is silently wrong — the failure that cost the gyroid's seed rotations five
playtests (§32.7). Positions and tangents are *vectors* and transform correctly under a
reflection; the surface normal is recomputed from the closed-form gradient at the transformed
point. Orientation is derived, never carried.

### 33.2 The bug the simulation caught, and why nothing else would have

`SchwarzPTileData.NeighbourTile` exists because **bond deltas do not add**. A bond is measured
in the canonical tile; carrying one into tile `(i,j,k)` composes tile transforms, and per axis
`T_a(T_b(x))` is `T_(a+b)` when `a` is even but **`T_(a−b)` when `a` is odd** — an odd tile is
a mirror image, and a mirror reverses the step through it. So a bond delta must be negated on
exactly the axes `AxisSigns` negates.

The first implementation used `tile + delta`. That is wrong on every odd-indexed tile, and it
is **silent**: the offsets are still exact, every prism still lands on the surface to 6e−8,
occupancy still keys cleanly, it compiles, and it passes every static check. It shows up only
as *geometry*. Simulating a plant's growth to its authored 800-prism budget made it obvious in
one line — the grown plant sprayed across **113 tiles** with a maximum nearest-neighbour gap of
**49.5 units** where the spacing is 5.3. With the fix: **41 tiles, max gap 5.9**, zero
duplicate positions, and a render that is unmistakably Schwarz P in all three projections.

The lesson is the general one: *a tiling defect can be invisible to every check that examines
one tile.* Both scripts now gate it, and the verifier asserts the naive rule is **provably
wrong** at every level that can discriminate — a gate nobody has watched fail is not a gate.
(Level 0's single site sits on the tile's centre of symmetry, so `T + δ` and `T − δ` are mirror
images at equal distance and it genuinely cannot discriminate; that exemption is stated in the
output rather than hidden.)

### 33.3 Invariant review

- **Mass is conserved.** Growth is still one prism per site, claimed through
  `PrismSpatialIndex.TryReserve`; occupancy is still *weak* (a site frees when its resident is
  eaten), so a grazed plant regrows into its own wound. No decay, no timer, no cull.
- **Continuity of existence.** Untouched — prisms still bloom in and wither out through the
  standard `AssembledFlora` path.
- **Flora populations.** *Superseded by §33.6* — Schwarz P became a lattice-colony species in
  the pass that followed this one. At the time of writing it kept the ordinary per-plant
  budget and reseeding.
- **Collider budget: unchanged, one-for-one** *at this pass*. The plant held the same
  `maxTotalSpawnedObjects` live prisms with the same colliders; only *where* they are placed
  changed. Site spacing moved 6 → 5.25 world units. The bond table is built once per level
  (~144k distance computations at the largest level, lazily, cached) and never again.
  (§33.6 restates the budget for the colony: prism colliders unchanged, crystal colliders
  3 → 22 in Blob.)

### 33.4 Tooling and verification

| | |
|---|---|
| `Tools/Build/measure_schwarz_p_tile.py` | Proves the tile and measures the layouts. `--check` verifies, `--write` regenerates the C# table. Independently reproduces the literature surface area **2.3451 a²** per cubic cell (measured 2.3464, 0.06%). |
| `Tools/Build/verify_schwarz_p_tile_tables.py` | Re-derives every claim **from the shipped `SchwarzPTileData.cs`**, by parsing it. Run after any edit to the table or the tile arithmetic. |

The second script is not redundant with the first, and §32.7 is why: the transcription between
a proven measurement and the shipped asset is exactly the step that neither the measurement nor
code review can see.

**In-editor verification (the human is the gate).** Plant a `SchwarzPFlora` (Blob cell in
freestyle, or the Hesperides topiary) and watch: (1) it grows as a *patch spreading outward*,
not a tendril; (2) the plates meet edge to edge with no visible seam where one tile meets the
next — the seam is where the old marcher's drift showed; (3) at full budget the six-way tunnel
network reads clearly; (4) let fauna graze it and confirm the wound regrows.

### 33.5 The prisms — sized to the tile, per element (Aug 2026, second pass)

A prism is an oriented box, and the tile fixes its frame: local **+z is the surface
normal** (the thin axis), **+y is the site's baked tangent**, **+x is
`cross(tangent, normal)`** — Unity's `LookRotation(forward: normal, up: tangent)`. So
`leafSize` is a *footprint in the surface's tangent plane*, and "do these plates sit
flush without overlapping?" is an exact OBB question about a known point set, not a
matter of taste. `Tools/Build/fit_schwarz_p_leaf_sizes.py` answers it: it reads the
shipped table, builds every prism of a 3×3×3 tile block at the authored `periodScale`,
and runs a separating-axis test over every neighbouring pair — **including pairs across
a tile seam**, which is exactly where a size fitted inside one tile would be wrong.

**The reference was measured, not eyeballed.** The brief was "flush like the Time and
Charge gyroids", so the gyroid was measured first. Its prisms sit **7.825 world units**
apart (not 3 — `separationDistance` is a bond-delta scale, not the spacing), and:

| gyroid | size | span | contact |
|---|---|---|---|
| Charge / Time | 9 × 3.4 × 1.5 | 1.15 spacings | 33% of prisms graze, max penetration **0.19u** (2% of the plate) |
| Mass | 7 × 4.5 × 3.5 | 0.89 spacings | 49% graze, max 0.31u |
| Space | 20 × 1 × 1 | 2.56 spacings | 99% interpenetrate, max 1.14u |

So the family's Charge/Time look is a plate about one spacing long that just touches its
neighbours, and its **Space is deliberately a strut that spans two and a half spacings
and passes through everything** — that is what makes it skeletal.

**The Schwarz P fit.** At the shipped flora (level 3, 37 sites/tile) sites sit **4.667
min / 5.263 mean** world units apart. Sweeping aspect against the largest footprint that
still has *zero* overlaps:

| aspect | x | y | coverage |
|---|---|---|---|
| 1.0 | 3.69 | 3.69 | 46.4% |
| 1.3 | 4.30 | 3.31 | **48.5%** |
| 1.618 | 4.72 | 2.92 | 47.1% |
| 2.0 | 5.10 | 2.55 | 44.3% |
| 3.4 | 5.61 | 1.65 | 31.6% |
| 5.0 | 5.85 | 1.17 | 23.3% |

Coverage is broad and flat near square, so the aspect can be chosen for looks at almost
no cost — which is what the four elements do:

| element | leaf size | aspect | span | result |
|---|---|---|---|---|
| **Charge / Time** | **4.72 × 2.92 × 1** | 1.618:1 (golden) | 0.90 spacings | flush, **zero overlaps**, 47.0% coverage |
| **Mass** | **4.09 × 3.14 × 2** | 1.3:1 | 0.78 | chunkier — squarer footprint, twice the slab; **zero overlaps**, 43.8% |
| **Space** | **13.4 × 0.7 × 0.7** | 19.1:1 | 2.55 | the strut: skeletal, largest bounds, interpenetrating by design |

Every plate is thin in z. The gyroid's Charge/Time runs a thickness of 0.19 spacings and
its Mass 0.45; these are 0.19 and 0.38 — the same family. **Space is the one element that
is not a flush plate**, matched deliberately to the gyroid's Space (2.57 spacings against
its 2.56): a strut spanning the lattice is what takes the largest bounds and reads
skeletal, and it cannot do that and avoid its neighbours at the same time.

**The level trap — a lattice species must pin `LeafScalePerLevel` at 1.** `ApplyLevel`
multiplies the leaf by `LeafScalePerLevel^(Level-1)`, and the Blob cell rolls this species
at **Levels 1..5**. It scales the *prism* but not the *lattice*, so at the inherited 1.15 a
level-5 plant's prisms are **1.749×** the size fitted flush and the plant interpenetrates
itself — measured at **144 overlapping pairs at level 3 and 204 at level 5**, against zero
at level 1. Pinned to 1, every level is clear. Nothing is lost: the crystal still grows
with level (`CrystalScalePerLevel`), and budget and lineage are untouched. **The prism size
belongs to the lattice, not to the plant.**

**All six producers were authored, not four.** The species has six config sites and a
size applied to four of them shows up wrong in two cells: the four `SchwarzP Flora
<Element>` assets, the **Hesperides topiary** (Element 2 / Mass — it carried a 4.2 × 4.2
square, wider than the 4.667 minimum spacing, so it was overlapping) and the **Blob**
config (no `Variant` of its own — it delegates to the element palette — but it is the
config whose `LeafScalePerLevel` the spawner actually reads). `SchwarzPFlora.prefab`'s own
fallback `leafSize` was the same overlapping 5 × 5 square and now carries the fitted
Charge/Time plate, so the variant-less path and the Lifeform Matrix preview are correct too.

Regenerate with `--render` for the preview sheet, `--write` to re-author. The writer emits
**every** `FloraVariantTuning` field explicitly and asserts the key set against the C#
class, because the keep-the-prefab sentinel is **−1**, not 0 — writing
`MaxTotalSpawnedObjects: 0` would not mean "keep", it would set the plant's live-prism
budget to zero and it would never grow a prism.

### 33.6 The tile colony — one plant, one tile, one crystal (Aug 2026, third pass)

The Schwarz P flora was one large plant sprawling across many tiles. It is now a **population
of plants that each own exactly one tile** — the same conversion the gyroid got in §32.7, and
the tile makes it markedly simpler.

**The flat point is the crystal's seat.** Each tile's centre used to carry a prism; it now
carries the plant's **heart**, one crystal per tile, never growing. The layouts were
re-measured with the centre excluded, so a level is a set of complete hexagonal shells —
**6, 18, 36, 60, 90** sites (`3n(n+1)`) — and the shipped flora resolves to **36 prisms per
plant**. The hole this leaves is not incidental: the centre took part in the relaxation and was
dropped afterwards, so the innermost shell sits where a real neighbour would hold it and the
gap is crystal-sized by construction. `verify_schwarz_p_tile_tables.py` asserts the seat is
empty at every level.

**Territory is one line, because a tile is an exact integer address.** A plant owns tile `T`;
a bond leading out of `T` belongs to its neighbour, and `SchwarzPAssembler.GetGrowthInfo`
declines it. That is the whole ownership question. What the gyroid needs for the same job —
and does *not* need here — is worth listing, because every item exists to paper over float
drift in a lattice that has no addressing:

| gyroid mechanism | why the tile colony has none |
|---|---|
| octagon discovery from danger prisms | a prism is stamped with its tile at birth |
| `RingMemberToleranceRadius` (2.5u) | membership is an integer, not a coherence test |
| the "poison band" (2.5–12u) | two plants either agree on an integer or are in different lattices |
| `TerritoryRadius` (26.5u) | a site belongs to exactly one tile |
| `OwnershipEpsilon` (0.75u) + contested boundary prisms | there is no boundary to contest |
| `NearestForeignClaimSqr` spatial-hash scan | the claim book is a dictionary hit |
| a baked seed **pose** carried through the frontier | the seed is *derived* at birth from the tile address |
| the per-birth handoff-coherence assert | there is no transcribed rotation to be wrong |

The claim book (`SchwarzPTileRegistry`) is therefore `Dictionary<(frame, tile), plant>` rather
than a binned spatial hash, and a frontier entry is just `(frame, tile)`.

**The lattice frame is the colony.** A founder anchors a `SchwarzPSurfaceFrame` on its own
seed prism; every daughter is `Program`med with her mother's frame **by reference**, and
`EnsureSeeded` early-returns on a non-null frame. So one lineage shares one world anchor, one
level and one occupancy book, and two plants of a colony cannot disagree about where a site
is — the gyroid needs a whole registry class to get a weaker version of that. Independent
founders in one cell hold *different* frames and simply never collide in the book; their
prisms still cannot overlap, because `PrismSpatialIndex.TryReserve` gates every site, exactly
as it does for any two floras that meet.

**Reproduction is a population event.** A plant that fills every site of its tile contributes
its unclaimed **face-adjacent** tiles to `SchwarzPColonyFrontier`; the population then births
**one** plant per fauna-wave period (`Cell.CurrentFaunaSpawnPeriod`) at a **uniformly random**
open tile. Random choice across every complete plant is what de-spheres the colony — it
wanders the way the old single plant wandered prism by prism, now at the level of whole flora.

*Six neighbours, not twelve.* The bond graph also reaches six edge-diagonal tiles, which touch
this one only at a 4-fold corner by a single bond. The {6,4} tiling's adjacency is the six
shared **edges** — the six faces of the half-period cube — so the colony grows through faces
and the surface it builds stays the tiling the tile is defined by. Iterating bonds blindly
would have offered the diagonals too; that would be an accident, not a choice.

**Completion is exact**, not a fudged prism count: every site of the tile occupied, plus the
gyroid's pacing conjuncts (no queued orders, two idle ticks, the maturation window) so a plant
does not parent mid-bloom. The gyroid's `PatchPrisms - 6` slack exists for a 22–28 patch
spread that cannot happen here.

**Mass is preserved, and the numbers are authored, never typed.**
`Tools/Build/author_flora_populations.py` now carries both lattice species with **separate**
unit cells (`LATTICE_PATCH`: gyroid 24 owned / 30 budget, Schwarz P **36 / 36** — exact, with
no headroom, because there are no boundary prisms to win). Blob: `800 → 22 plants × 36 = 792`.
Hesperides topiary: `150 → 4 × 36 = 144`.

**Collider budget.** Prism colliders are **unchanged** — mass is preserved and they are still
phase-LOD managed. The cost is **crystals**: one always-on heart collider per plant, so Blob
goes **3 → 22** for this species (~9% of `MAX_PLANTS_PER_SPECIES`, which is the dial).
Hesperides is unchanged at 4.

**Two traps this pass fixed, both the §33.5 lesson repeated for other fields.**
`SchwarzPFlora.prefab` authored `crystalGrowth: 0.1` and nothing gated it, so a Schwarz
crystal grew **+0.1 every grow tick, unbounded, forever** — now gated in code *and* authored 0.
And `CrystalScalePerLevel: 1.2` against Blob's Levels 1..5 gives crystal scales
3.0 / 3.6 / 4.32 / 5.18 / 6.22 against a hole of about 4.2 units, so **from level 3 up the
heart burst its own seat** — pinned at 1, exactly as `LeafScalePerLevel` was. On a lattice
species the geometry owns the size; the plant's level does not.

**Invariants.** *Mass conserved* — no timer, decay or culler; a lowered cap stops production
and never culls; `cap × 36 ≈ the old budget`. *Continuity of existence* — daughters bloom
through the standard spawn path, deaths use the existing wither. *One crystal per lifeform* —
one plant, one tile, one heart, which is exactly what §23.3 requires. *Volume is the spine* —
the Frenzy gates are checked before every production site, including the population cycle
(before the pop, so a frozen colony burns no frontier entries). *Territorial permanence* — a
claim is released only when the plant is destroyed, never by a clock.

**Known follow-ups, deliberately not swept in.** (1) The Hesperides topiary lands at
`floor = cap = 4`, so it is planted at its ceiling and never reproduces — correct for a clipped
specimen, inert as a colony; raise its recorded source budget if it should spread. (2) The
colony machinery is now duplicated between `OctagonMode` and `TileColonyMode`; the honest fix
is one `ILatticeColony` abstraction, filed rather than done because the gyroid path had just
shipped. (3) `minHealthBlocks: 5` was 0.6% of an 800-prism plant and is 14% of a 36-prism one.

### 33.7 Space gets its own lattice — Schwarz P (Aug 2026, fourth pass)

Space is the skeletal element on both lattice species. This pass gave the **Schwarz P** Space a
lattice of its own. The gyroid was given one too, and it regressed — that half is §33.8.

**The dial: `FloraVariantTuning.LatticeScale`** (sentinel **−1** = keep the prefab's). It scales
an element's whole lattice — every distance between prisms — while leaving the plant's
**topology and prism count identical to its elemental peers**. `AssembledFlora.ApplyLatticeSpacing`
pushes it onto a freshly created assembler at all three creation sites (founder, daughter,
re-seed), because the assembler reads it *before* its first growth probe and a value that arrives
later is a value the seed never saw. Both species have it, but each scales a different thing and
each is exact for its own reason — the gyroid's took two attempts, §33.8.

On Schwarz P it scales `periodScale` **and** `separationDistance`, together, and *together* is the
whole trick. `ResolveLevel` picks the subdivision whose `MeanParamSpacing × periodScale / 2π` is
nearest `separationDistance`; scaling both sides by the same factor leaves the argmin invariant, so
the level — and with it the mesh and the prism count — cannot move. `k = 5/3` takes spacing
`5.25 → 8.75` at **level 2, 36 sites**, exactly its peers'.

**The correction this pass made.** The first attempt scaled `separationDistance` alone. That
re-resolved to **level 0 — 6 sites per tile instead of 36** — and shipped a plant with visibly
fewer subdivisions: a *different* plant, not a bigger one. The tell was downstream and loud once
seen: `author_flora_populations.py` needed a per-config override to say a Space plant owned 6
prisms, and its cap ran to the 60-plant ceiling while its peers sat at 22. That override is now
**deleted**, and its absence is the evidence the topology is back. `assert_level_invariant()`
proves it on every run of the fitter rather than trusting the arithmetic.

**The result.**

| | before this pass | after |
|---|---|---|
| **Schwarz P Space** | 13.4 × 0.7 × 0.7, 72 overlaps, level 2 | **30 × 0.5 × 0.5**, 60:1, 1.14 spans, `LatticeScale 5` (spacing `5.25 → 26.25`), **level 2 / 36 sites, unchanged**, flush with no overlaps |

The prism is sized in multiples of its own lattice's spacing (`SPACE_SPANS`,
`SPACE_THICK_RATIO` in `fit_schwarz_p_leaf_sizes.py`) rather than as absolute numbers, so the
strut and the lattice can never drift apart. Those two ratios were originally derived from a
gyroid Space that §33.8 then reverted; their provenance is recorded at the constants, and they
are now the Schwarz element's own.

**`LeafScalePerLevel` pinned at 1** — the §33.5 trap, unchanged here: it scales the prism and
leaves the lattice put.

**Populations and the collider budget.** Schwarz Space sits at its peers' `cap 22 / 792 prisms at
cap`, uniform across all four elements. Per §4.6 the binding ceiling is unchanged: per-prism
volume moves 2.93 → 2.77, so the species' whole standing mass at cap is ~2.2k against the Blob
cell's `FrenzyEnterVolume 288,000`. The Blob's Mass gyroid (~137k at cap) is still what binds,
exactly as §32.7 recorded.

**Invariants.** Authored size and spacing data plus one scale read: *mass is conserved*,
*continuity of existence*, *no imposed death*, *one crystal per lifeform* and *volume is the
spine* all stand as §33.6 left them. A prism-count change per plant moves production only;
nothing is culled.

### 33.8 Scaling the gyroid — the dislocation, and what it took to fix (Aug 2026)

The gyroid Space lattice was scaled, it grew **offset parallel surfaces**, it was reverted, and
then it was done properly. The failure is the more useful half.

**Attempt 1 — scale `separationDistance`, ship the dislocation.** Two things went wrong at once:

*It did not look stretched.* Scaling the lattice *with* the prism cancels the stretch. The strut
went 2.56 → 3.52 spans, but everything grew 1.667× together, so at any viewing distance nothing
was longer — the same plant, bigger. **A prism only reads as stretched against a lattice that
stayed put.**

*It dislocated.* A gyroid plant's coherence is decided by distances written in ABSOLUTE world
units, every one sized against the separationDistance-3 lattice:

| where | value at scale 1 | what it decides |
|---|---|---|
| `GyroidAssembler.snapDistance` | 0.3, compared to **squared** distances → 1.73u | is this prism THE one at my bond site, or a second one beside it |
| `GyroidAssembler.radius` | 40u | how far the mate search looks for it at all |
| `AssembledFlora.MisalignmentRadius` | 5.5u, at **both** the grown-site and seed-site checks | rejects a site whose neighbour belongs to a MISALIGNED frame |
| reservation `clearRadius` floor | 2u | the floor under an otherwise-proportional radius |

Scaling only the bond offsets moved every *real* distance out from under all four at once. The
misalignment gate is the one that bit: the healthy closest pair grew with the lattice while the
gate did not, so **the gate written to catch twins stopped catching them** and the plant grew the
domains it exists to prevent. Every constant was individually correct, measured and commented,
and each still fired — the defect was a *relationship*, which is why no static check saw it.

**Attempt 2 — scale the family, and assert the relationship.** `GyroidAssembler.ApplyLatticeScale`
now moves all of them together: bond offsets via `separationDistance`, `radius` linearly,
`snapDistance` by **scale²** (it is compared against squared distances, so that is what holds the
same *linear* tolerance), the `clearRadius` floor, and — through `AssembledFlora.LatticeScale` —
the octagon tables and the misalignment gate. The bare `5.5f` that appeared at two call sites is
now the single `MisalignmentRadius` property, because a literal repeated at two sites is exactly
how one of them gets missed.

The invariant that actually matters is an **ordering**, and it is asserted rather than assumed:

    reservation clearRadius  <  misalignment gate  <  healthy closest pair

Below the gate a neighbour is a duplicate to reject; above the healthy pair everything is a
legitimate neighbour. Drift the gate up and it rejects real growth; drift it down and twins are
born. `Tools/Build/verify_gyroid_lattice_scale.py` walks the SHIPPED bond table at scales
1 / 1.5 / 2 / 3, measures the healthy pair, reads the tolerances out of the shipped C#, and fails
unless the ordering holds and the ratio stays constant:

| scale | sep | bond | reserve | gate | healthy | gate/healthy |
|---|---|---|---|---|---|---|
| 1.0 | 3.0 | 7.84 | 3.13 | 5.50 | 7.52 | 73% |
| 1.5 | 4.5 | 11.75 | 4.70 | 8.25 | 11.29 | 73% |
| 2.0 | 6.0 | 15.67 | 6.27 | 11.00 | 15.05 | 73% |
| 3.0 | 9.0 | 23.51 | 9.40 | 22.57 | 22.57 | 73% |

**What shipped.** The strut is stretched on the native lattice to `30 × 1 × 1` and then the whole
structure — prisms, spacing, and the spindles between them — is scaled **2×**, giving
`60 × 1 × 1` at `LatticeScale 2` (separation 3 → 6, spacing 7.83 → 15.66). The span was **3.83
spacings before and after**, which is the check that the LENGTH is a pure scale-up rather than a
reshape; the cross-section was then thinned by hand from the 2 a uniform scale would give to
**1**, which is a deliberate reshape — Space is the skeletal element and a 60:1 needle reads
thinner than a 30:1 bar at the same length. (§33.10 later opened the spacing to `LatticeScale 4`
and §33.11 shortened the strut to 40; the numbers in this section are that pass's, not the
shipped ones.)
The octagon colony's populations are unchanged (`MaxTotalSpawnedObjects 30`, cap 33).

**Spindles scale; crystals do not — and the spindle scale goes on the CHILD, not the root.**
The spindle is visible branch geometry spanning the gap between two prisms, so a widened lattice
with unscaled branches leaves them visibly short. `AssembledFlora.ScaleSpindleToLattice` applies
the scale at both spawn sites, to the spindle's own **children**.

Putting it on the spindle root instead is a runaway, and it shipped for one build. Two facts make
it so, either one sufficient: **spindles NEST** — every grown spindle is instantiated as a child
of its parent branch's spindle (`Instantiate(spindle, order.parent.gameObject.transform)`), so a
root scale multiplies down the whole chain as `scale^depth`, which at scale 2 and ten generations
is 1024× — and **prisms parent to the spindle root**, so that compounding factor also multiplies
every prism's authored `leafSize`, and the number in the config stops describing the prism at all.
The result was prisms that grew visibly larger the further a branch got from its seed. Scaling the
children is safe because a child is a leaf of that chain and prisms are never among them (the call
happens before the prism is parented).

The crystal is deliberately excluded from the scale entirely: octagon centres move apart with the
lattice, so the hearts spread out while each stays its authored size. Spindle scaling is also
**gyroid-only** — the Schwarz P Space element's proportions were judged good at its shipped scale
*with* unscaled spindles, and changing them now would regress an approved look for no request.

**The general rule.** Before scaling anything in a hierarchy, ask what else *inherits* that
transform. A scale applied to a node that is both a parent of its own successors and a parent of
the thing whose size is authored elsewhere is wrong twice over, and neither error shows up in a
compile or in any static check — only in geometry, and only some distance from the seed.

**The volume consequence, and why the thin cross-section matters more than it looks.** A uniform
2× would be an **8× per-prism volume**, which is what makes this the §4.6 trap: at `60 × 2 × 2` the
prism is 240 units and the species' ceiling reaches **155%** of the Blob cell's
`FrenzyEnterVolume 288,000` on its own. Holding the cross-section at **1** instead lands it at
`60 × 1 × 1 = 60` per prism (**112.7** after the 1.88 level spread) and **39%** — heavier than the
20 × 1 × 1 it replaced (13%), lighter than its own Mass sibling (71%), and comfortably inside the
budget. (§33.11's shortening to 40 takes it further down, to 75.1 and 26%.) A lattice species' thickness is therefore a *volume* dial with cubic leverage, not only a
look dial: it is the cheapest correction available when a scale-up overshoots the ladder. If the
freestyle cell still reads sparse or freezes early, the levers in order remain the **cell's volume
ladder** first and `MaxLivePopulation` last (§32.7 seventh pass, /ecology §4.6); neither is changed
here, because cell pacing is a design call rather than a consequence of this one.

**The general rule.** A coherence tolerance written as an absolute distance is an *unstated
dependency on the lattice it was measured against*. Before scaling any lattice, enumerate every
test that decides *sameness* — snap, dedupe, reserve, twin-detect — and either make it
proportional or scale it, then assert the ORDERING between them rather than the values. Schwarz P
never needed this: its sameness test is an integer tile address, so no tolerance exists to
invalidate.

### 33.9 The clamp — an authored prism size was never reaching the screen (Aug 2026)

Three passes of §33.5–§33.8 fitted, measured and argued about Space prism sizes. **None of
them reached the engine.** Every one was silently trimmed to a 10-unit long axis.

**The mechanism.** `PrismScaleAnimator.SetTargetScale` clamps PER AXIS into
`[minScale, maxScale]`, whose serialized defaults are `(0.5,0.5,0.5)` and `(10,10,10)`:

```csharp
newTarget.x = Mathf.Clamp(newTarget.x, minScale.x, maxScale.x);   // and y, z
```

`Flora.AddHealthBlock` states `healthPrism.TargetScale = leafSize`, and `Prism.TargetScale`'s
setter routes straight into that clamp. The flora health-prism prefabs
(`MassGyroidBlock Variant` — which the Space flora also uses — and `SchwarzPBlock Variant`)
carry no override, so the window is the default. An authored `60 × 1 × 1` at Level 2 is
`69 × 1.15 × 1.15`, and `Clamp` returns exactly **`(10, 1.15, 1.15)`** — the value read off the
live scene, to the float.

**What it hid, which is the whole lesson.** The clamp is inside a setter, with no log, no
warning and no return value. The config said 60 and the prism was 10, and *nothing anywhere
reported the difference*:

| authored | rendered | authored | rendered |
|---|---|---|---|
| 20 × 1 × 1 (pre-branch) | 10 × 1 × 1 | 60 × 2 × 2 | 10 × 2 × 2 |
| 22.96 × 0.45 × 0.45 | 10 × **0.5** × **0.5** | 60 × 1 × 1 | 10 × 1 × 1 |
| 45.92 × 0.45 × 0.45 | 10 × **0.5** × **0.5** | Schwarz 30.79 × 0.3 | 10 × **0.5** |

Cross-sections under 0.5 were clamped **up**, so the "thin" struts were never thin either.
Consequences that were all misread as other problems:

- *"This pass didn't stretch the prisms"* — correct, and it could not. 22.96, 45.92 and 60 all
  render identically. The only thing any of those passes changed on screen was lattice spacing.
- *The wrong spacing* — the spacing was right; the prisms were pinned at 10 while the lattice
  widened to 15.66, so the structure read as sparse and disconnected.
- **Every overlap and volume measurement in §33.5–§33.8 was computed against geometry the
  engine never used.** The OBB fits, the saturating crossing counts, the volume table — all
  phantom. The numbers are correct *for the sizes named*; those sizes just were not what ran.

**The fix, and its principle: a size that is AUTHORED widens the window; a size that is GROWN
keeps it.** `PrismScaleAnimator.AdmitTargetScale(Vector3)` raises `maxScale` / lowers `minScale`
to admit the target, and both flora paths (`Flora.AddHealthBlock`,
`PhyllotacticFlora.AddHealthBlock`) call it before stating the size. Trail prisms, which grow
into the bound through `Grow()`, are untouched — the bound is meaningful there.

This is the general form of a workaround the project already had: `SpawnablePrism` and
`ShieldedSpawnablePrism` serialize max **100**, `Manta Prism` max x **40**, `Dolphin Prism` max
z **100**. The trap has been hit and patched per-prefab before; it was simply never applied to
flora. **363 of 404 prefabs still fall through to `[0.5, 10]`.**

**Corrected effective volumes** (with the level spread; §33.7's table was pre-clamp-fix):

| | Charge | Mass | Space (was → now) | Time |
|---|---|---|---|---|
| **Gyroid** | 86.2 | 207.0 | **18.8 → 112.7** (→ 75.1 at §33.11's 40) | 86.2 |
| **Schwarz P** | 13.8 | 25.7 | **2.5 → 7.5** | 13.8 |

**Not swept in.** About fifteen other call sites author `TargetScale` directly
(`PrismTrailBuilder`, `Fauna` body prisms, the AOE block creators, `Microscene`,
`PaintingRunner`, the `Spawnable*` environment builders). Most draw from the max-100 spawnable
prefabs and are unaffected; changing them all would move geometry across many shipped modes on
no evidence of a defect. Flagged, deliberately not touched.

**The history, which closes the loop on "six months ago it was great".** `SpaceGyroidBlock
Variant.prefab` exists and overrides exactly one property: **`maxScale.x = 20`** — authored so a
20-unit Space needle would survive the clamp. **Nothing references it any more.** The Space flora
runs on `MassGyroidBlock Variant.prefab`, which carries no such override. So the element rendered
at its full 20 until a per-element-prefab → config consolidation moved it onto the Mass block, at
which point the needle silently halved to 10 and stayed there. That regression predates this
branch entirely, and `AdmitTargetScale` is the general form of what that retired prefab did by
hand — the per-prefab override is no longer needed by anything.

**A second instance of the same ordering mistake.** `GyroidAssembler.ConvertBlock` assigned
`prism.TargetScale = scale` and only *then* `prism.MaxScale = Prism.MaxScale` — widening after the
clamp had already bitten, so a converted prism was pinned at the victim's own ceiling however long
the lattice's prisms are. Fixed to widen first (and it now uses `AdmitTargetScale`, which also
lowers `minScale` — the bare `MaxScale` assignment never did, so a thin lattice prism was clamped
up regardless). Not on the flora growth path (`ConvertBlock` is reached only from
`FindClosestMate` under `StartBonding`, which `AssembledFlora` never calls), so this was latent
rather than active — but it is the same bug and would have bitten the first caller that hit it.

**One more thing worth knowing when authoring these configs.** The Blob Space gyroid config has
`SpreadElements` ON with a 4-asset `ElementPalette`, so the `Variant` that actually reaches the
plant is the palette SIBLING's — `Assets/_SO_Assets/Lifeforms/Gyroid Flora Space.asset` — not the
cell config's own `Variant` block. Editing only the cell config would be a silent no-op. Author
both (the fitters do).

**The general rule.** A silent clamp inside a setter is indistinguishable from a config that
was never applied — and it defeats every offline measurement, because the measurement models
the authored number while the engine uses another. When a fitted size does not read on screen,
verify what the engine actually stored **before** re-fitting: one look at the live Transform
would have saved three passes of measuring phantom geometry.

### 33.10 Spacing and prism size are INDEPENDENT dials (Aug 2026)

The two Space elements were opened up: gyroid `LatticeScale 2 → 4` (spacing `15.66 → 31.32`) and
Schwarz P `1.667 → 5` (spacing `8.75 → 26.25`), with **both prisms unchanged** at `60 × 1 × 1`
and `30 × 0.5 × 0.5` (the gyroid strut was shortened to 40 immediately after — §33.11). Spans
fall accordingly — gyroid `3.83 → 1.92`, Schwarz `3.43 → 1.14` — and
the Schwarz strut, which had 108 crossings, is now **flush with none**.

**What this pass had to undo.** `fit_schwarz_p_leaf_sizes.py` sized the Space prism as RATIOS to
its own lattice spacing (`SPACE_SPANS`, `SPACE_THICK_RATIO`), so the strut tracked the lattice
automatically. That coupling was right while the two moved together and became actively wrong the
moment they were tuned separately: tripling the spacing would have tripled the strut to
`90 × 1.5 × 1.5` with nobody asking. It is now an authored `SPACE_LEAF = (30.0, 0.5, 0.5)`.

**The rule.** Derive a value from another only while they are genuinely one decision. The moment
a human tunes them independently, the derivation stops being a safeguard and becomes a silent
edit — and it fires in the direction nobody is looking, because the field they *did* change looks
correct afterwards.

**Verified at the new scales, not assumed.** `verify_gyroid_lattice_scale.py` now covers scale 4
and the ordering still holds with the ratio constant:

| scale | sep | bond | reserve | gate | healthy | gate/healthy |
|---|---|---|---|---|---|---|
| 2.0 | 6.0 | 15.67 | 6.27 | 11.00 | 15.05 | 73% |
| **4.0** | **12.0** | **31.35** | **12.54** | **22.00** | **30.10** | **73%** |

`assert_level_invariant()` confirms Schwarz stays at level 2 / 36 sites at `k = 5`
(`sep 30`, `period 300` → the argmin is unmoved), so topology and prism count are its peers'.
`GyroidOctagonRegistry`'s deliberately-unscaled `CenterDedupeRadius` (12u) is still correctly
bracketed at `k = 4`: distinct octagon centres are `35.87 × 4 = 143.5` apart, half of that is
71.7, and drift is ~1–2u — and 12 remains under `BinSize` 17.935, so the 3³ scan still covers it
(§33.8's stated bound was 0.67×–40×; 4 is inside it).

**Mass is unchanged, footprint is not.** Prism sizes and counts did not move, so per-prism volume,
the species ceilings and the cell's Frenzy ladder are all exactly as §33.9 left them. What grows is
the **bounds**: a gyroid plant's octagon ring radius goes 20u → 40u and its territory 53u → 106u,
and a Schwarz plant's tile goes 50u → 150u across. Same mass, spread over ~4× and ~3× the linear
extent — worth an eye in the editor for plants reaching past the membrane or into the nucleus,
which is a spatial question no offline check here answers.

### 33.11 The Space gyroid strut, shortened to 40 (Aug 2026)

`60 × 1 × 1 → 40 × 1 × 1` on the unchanged `LatticeScale 4` lattice (spacing 31.32), so the span
falls **1.92 → 1.28** spacings. Prism only; spacing, topology, prism count and populations are
untouched, which is why nothing in §33.10's verification needed re-running — the coherence family,
the level invariance and the registry bound are all properties of the *lattice*, and the lattice
did not move.

Volume falls with it: `60 → 40` per prism, **112.7 → 75.1** effective after the 1.88 level spread,
and the species' ceiling in the Blob cell **39% → 26%** of `FrenzyEnterVolume`. Schwarz P Space
keeps `30 × 0.5 × 0.5` at `LatticeScale 5`, judged good as shipped.

The current state of both Space elements:

| | prism | LatticeScale | spacing | span | eff. volume | % of Blob Frenzy |
|---|---|---|---|---|---|---|
| **Gyroid Space** | 40 × 1 × 1 | 4 | 31.32 | 1.28 | 75.1 | 26% |
| **Schwarz P Space** | 30 × 0.5 × 0.5 | 5 | 26.25 | 1.14 | 7.5 | 2% |
