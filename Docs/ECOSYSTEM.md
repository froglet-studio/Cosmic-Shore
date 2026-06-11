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
*emerge* from the predator–prey loop. The mirror cheat on the growth side is the
flora **regrowth pulse** (§5.1), which is likewise flagged for retirement, not
extension. If a cell "freezes," the fix is to give an active force a reason or
ability to consume that mass (tune fauna diet/reach/spawning, or vessel
abilities) — **never** to add decay. See CLAUDE.md → "Mass" fundamental and
"Don't cheat emergence."

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
| Flora planting | **+** | new flora instantiated (`RandomLifeSpawner`) → health prisms → `AddBlock` |
| Flora growth | **+** | existing flora grow new prisms (`Flora.Grow`) → `AddBlock` |
| Fauna consumption | **−** | fauna seek & detonate opposing-domain prisms → `RemoveBlock` |
| Vessel abilities (combat) | **−** | vessel impacts / ability use → prism death → `RemoveBlock` |

> The **−** column is exhaustive: fauna consumption and vessel abilities are the
> *only* prism sinks. There is no decay/aging row — mass is conserved (§0).

Prism count → **Cell Phase** (`Sprout→Quiet→Settled→Restless→Frozen→Rabid`,
computed with enter/exit **hysteresis** so the cell doesn't chatter on the
boundary). Phase is the single dial the rest of the ecology reads.

---

## 2. Full ecosystem diagram

```mermaid
flowchart TD
    TRAIL["Vessel trails (prisms)"] -->|+| COUNT
    FPLANT["Flora planting"] -->|+| COUNT
    FGROW["Flora growth"] -->|+| COUNT

    COUNT["PRISM COUNT<br/>LiveBlockCount + per-domain counts<br/>(MASS x DOMAIN, inside a CELL)"]
    COUNT --> PHASE["CELL PHASE<br/>Sprout-Quiet-Settled-Restless-Frozen-Rabid<br/>(hysteresis)"]

    %% Flora is self-limiting via phase gates (negative feedback)
    PHASE --> GPLANT{"Phase &lt; Settled?"}
    GPLANT -->|yes| FPLANT
    PHASE --> GGROW{"Phase &lt; Frozen?"}
    GGROW -->|yes| FGROW

    %% Aggression is the ONLY thing prism count feeds into fauna (per redesign)
    PHASE --> AGGRO["FAUNA AGGRESSION<br/>L0 calm / L1 / L2 frenzied<br/>(derived from Phase)"]

    %% Fauna spawn is timer-driven, fixed period + fixed population
    TIMER(["Spawn timer<br/>FIXED long period"]) --> SPAWN
    DOM["Controlling domain<br/>(DominantDomain)"] --> SPAWN
    SPAWN["FAUNA SPAWN<br/>fixed-size population<br/>in controlling color"]

    SPAWN --> POP["Live fauna population"]
    AGGRO --> BEHAVE
    POP --> BEHAVE["FAUNA BEHAVIOR<br/>L0 seek crystal /<br/>L1 opposing centroid /<br/>L2 densest any-domain"]
    BEHAVE --> CONSUME["Consume opposing prisms"]
    CONSUME -->|−| COUNT
    CONSUME --> DOM

    %% The missing negative feedback on population
    POP -.->|UNBOUNDED today| CULL["CULL / STOP-PRODUCING<br/>(MISSING — see §6)"]
    CULL -.->|should bound| SPAWN
    CULL -.->|should remove| POP

    classDef missing fill:#fee,stroke:#c00,stroke-width:2px;
    classDef state fill:#eef,stroke:#33c,stroke-width:2px;
    class CULL missing;
    class COUNT,PHASE state;
```

### The three feedback loops (this is where "vibrant" lives or dies)

1. **Flora self-limit (negative, working).** `count↑ → phase↑ → planting stops at
   Settled, growth stops at Frozen → count stops rising from flora.` Keeps flora
   from filling the cell forever. ✅
2. **Predator–prey (negative, the heartbeat).** `count↑ → aggression↑ → fauna hunt
   harder/closer → consume more → count↓ → aggression↓ → …` This is the
   oscillation that should make the cell feel alive. Per the latest decision this
   loop runs **through aggression (behavior)**, not through spawn rate. ✅ (once
   §6 lands)
3. **Population control (negative, MISSING).** Nothing removes fauna or throttles
   production. Fixed-period spawning with no death ⇒ fauna accumulate without
   bound. This is the loop we must add. ❌

### ASCII core loop (quick read)

```
        +-----------------------------------------------------+
        |                                                     |
        v                                                     |
   PRISM COUNT --> PHASE --> AGGRESSION --> fauna hunt --> CONSUME (−)
        ^   ^                                                  |
        |   |                                                  |
   flora +  +-- planting/growth gates close as phase rises (−) |
   trail +                                                     |
                                                               |
   SPAWN (timer, fixed N, controlling color) --> POPULATION ---+
                                                     |
                                                     X  no cull / no cap  (MISSING)
```

---

## 3. Fauna lifecycle (today vs. target)

```mermaid
flowchart LR
    T(["timer tick<br/>fixed period"]) --> S["spawn population N<br/>domain = controlling color"]
    S --> A["assign aggression<br/>from cell phase"]
    A --> H["hunt: resolve goal by aggression<br/>L0 crystal / L1 opposing / L2 densest"]
    H --> E["reach prisms -> detonate (−count)"]
    E --> H
    E -.->|MISSING| D["death / cull"]
    H -.->|MISSING| ST["starve / despawn when no prey"]
```

Dashed = not implemented. Fauna currently spawn, hunt forever, and never leave.

---

## 4. Part-by-part analysis

| # | Part | Driver | Current behavior | Desired | Gap / action |
|---|---|---|---|---|---|
| 1 | Prism count (state) | trail + flora − fauna/combat | tracked via Add/RemoveBlock, per-domain | same | ✅ the spine, leave alone |
| 2 | Cell phase | prism count + hysteresis | Sprout→Rabid | same | ✅ thresholds tunable per biome |
| 3 | Flora **planting** | `Phase < Settled` | plants while below Settled | prism-count driven | ✅ done (retrofit) |
| 4 | Flora **growth** | `Phase < Frozen` | grows while below Frozen | prism-count driven | ✅ already wired in `AssembledFlora`/`BranchingFlora` |
| 5 | Fauna **aggression** | Phase → L0/L1/L2 | seek crystal→opposing→densest | prism-count driven | ✅ works; extension seam for a 4th tier / per-subtype |
| 6 | Fauna **spawn timing** | timer + `Phase≥Quiet` gate + aggression-scaled interval | gated + variable period | **timer only, FIXED period** | 🔧 drop phase gate; drop aggression interval scaling |
| 7 | Fauna **spawn count** | 1 per tick | single fauna | **fixed-size population** | 🔧 spawn N per tick |
| 8 | Fauna **domain** | `PickRandomDomain(excluded = local)` + `FaunaExcludeLocalDomain=true` | never the controller's color | **controlling color** | 🔧 use `host.ControllingDomain` — **this is the "no Jade fauna" bug** |
| 9 | Spawn-cycle HUD ring | `CurrentFaunaSpawnPeriod` (aggression-scaled) | period varies | base fixed period | 🔧 ring reads base period (no aggression scaling) |
| 10 | Fauna **population bound** | none | unbounded (no death/cap) | bounded | ❌ **MISSING — §6 decision** |
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
  behavior`. Spawn **period and population size are FIXED** (config values, tuned
  "much longer" than today). Aggression does **not** feed spawn rate/size — only
  behavior. This makes loop #2 the heartbeat.
- **Fauna domain = controlling color** (`host.ControllingDomain`). Fixes the Jade
  bug; the dominant domain's fauna proliferate and hunt the minority. Trivial,
  certain change — folded into the spawn rewrite.
- **Spawn = timer-driven**, no phase gate, **population of fixed N** per tick.
- **HUD ring = base fixed period** (remove the aggression scaling the retrofit
  added to `ScaleFaunaInterval` / `CurrentFaunaSpawnPeriod`).
- **Keep 3 aggression tiers for now** (first approximation). Rabid = top tier
  today; a 4th "berserk" tier and per-subtype aggression are natural extensions
  when the predator/herbivore split lands (§7).

### 5.1 Density & flora regrowth (cells were sparse / froze solid)

Two follow-on changes so cells feel full and keep breathing:

- **Scaled-up capacity.** The menu's `Blob Cell Config` phase thresholds were tiny
  (Quiet 100 … Frozen 700 … Rabid 900 — the "widened for visibility" values), so
  flora stopped planting at 300 prisms and growing at 700. Scaled ~6× (Quiet 600 …
  Frozen 4200 … Rabid 5400), keeping the 0.6 hysteresis ratio. Raising the
  thresholds raises both the prism-count ceiling *and* the `DomainVolumeIndicator`
  volume scale (it ranges against `RabidEnter`). Other biomes already use the high
  code `Default` (Rabid 15000) — left as-is; scale there too if gameplay feels
  sparse.
- **Flora regrowth pulse.** Growth was a hard stop at Frozen, and in the menu the
  dominant domain's flora have no down-force (fauna only eat *opposing* prisms), so
  the canopy froze and never resumed. `Cell.FloraGrowingEnabled` now = `phase <
  Frozen` **OR** (`phase < Rabid` AND in a periodic regrowth window). So below
  Frozen flora grow freely; once full they resume growing in brief periodic pulses
  (cell-global, all flora breathe together); Rabid stays the hard ceiling.
  Config: `SpawnProfileSO.FloraRegrowthPulsePeriod` (15s) /
  `FloraRegrowthPulseDuration` (4s); `<= 0` falls back to those defaults so the
  pulse is on across the board.

> The pulse is a **growth-side cheat** — a hard-coded oscillator that fakes the
> "breathing" we want to *emerge* from the predator–prey loop. Its mirror,
> **prism decay**, was considered as the "real" down-force (flora shed aged
> prisms → count falls → growth resumes via hysteresis) and **rejected**: mass is
> conserved, prisms are only removed by active forces (§0). The honest model is
> that a frozen-solid cell is a *valid* state — it stays frozen until an active
> force (opposing-domain fauna grazing it, or vessel abilities) removes mass, at
> which point the existing `phase < Frozen` hysteresis resumes growth on its own.
> The pulse is therefore flagged for **retirement, not replacement-by-decay**;
> retiring it means accepting frozen accumulations until the food web (or a
> vessel) eats them. See §0 and §10.

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
- `Cell.OpposingBlockCount(domain)` = live prisms not of `domain` — the prey signal.
- **Production pauses** when `OpposingBlockCount(controllingColor) < FaunaFoodFloor`
  (`SpawnProfileSO`, default 5): the timer keeps ticking but no population spawns.
- **Starvation cull** on `Fauna`/`LightFauna`: a creature that hasn't consumed a
  prism in `starvationSeconds` (default 30, `Fauna` field) despawns; `NotifyFed()`
  resets the clock on every `Consume`.
- Net: population self-bounds to prey, no hard cap. Because fauna only *hunt*
  opposing prisms at higher aggression (L1+), and aggression rises with prism
  count, **survival tracks prism count** — low mass ⇒ fauna can't find food ⇒ they
  thin out; high mass ⇒ they feed and multiply. That coupling is the oscillation.

**Tuning knobs** (watch in Menu_Main, expect to adjust): `starvationSeconds` (too
low ⇒ fauna starve before reaching prey; raise it), `FaunaFoodFloor` (min prey
before a burst), `PopulationSize` (`FaunaConfigurationSO`, swarm size),
`BaseFaunaSpawnTime` (fixed period). No hard population cap was added; if a
prey-rich cell ever spikes fauna enough to hurt frame-rate, add a high safety cap
as a backstop (not the primary control).

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
via `RandomLifeSpawner`). Two herbivore species + one predator. **Currently only
the two herbivores are wired into the test profiles** — the shark is built and
balanced (spawn-immunity exists) but left out of both scenes for now (see §7.2);
its config asset still exists, so re-adding it is one line in a profile.

> **The live spawn path is the cell config — NOT the scene-placed populations.**
> The `MassTadpolePopulation` / `MassBrittlestarPopulation` etc. objects in scenes
> are wired through a `Cell` field named `fauna2` that **no longer exists** on
> `Cell.cs` — a dead/stale prefab override Unity ignores. So those `BoidManager`/
> `LightFaunaManager` populations **never instantiate**. Every working fauna comes
> from a `FaunaConfigurationSO` in the cell's `SpawnProfileSO`. (This is why
> removing the tadpole config = no tadpoles, regardless of the placed population.)

- **Diet = "what counts as prey"** is a `FaunaDiet diet` field on the `Fauna`
  base (`Herbivore` / `Predator`), defaulting to **Herbivore**:
  - **Herbivore** — eats prism MASS, but the two herbivore species differ:
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
- **Targeting (v1):** predators reuse the shared phase-based goal (density-grid
  centroids). Herbivores swarm opposing-flora density; predators seek the same
  centroids and so converge on the herbivores feeding there, eating them on
  contact. Explicit "seek nearest herbivore" steering is a future refinement (no
  central fauna registry exists yet).
- **Spawn gating (known approximation):** `RandomLifeSpawner` gates every fauna
  population on `OpposingBlockCount >= FaunaFoodFloor` (prism prey). For predators
  this is a *proxy* (dense prisms ⇒ herbivores present ⇒ predator food); the real
  bound is starvation. Refinement: gate predator spawn on herbivore count.
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
- **Swarm size = tadpole `PopulationSize`** (Blob 25, Skim Race 12). The spawner
  adds that many per `BaseFaunaSpawnTime`; starvation caps the standing count to
  available prey, so a denser swarm needs a higher `PopulationSize` *and* enough
  prey to keep it fed.

> **Don't rely on the scene-placed `*Population` objects** — they're wired through
> the dead `Cell.fauna2` field (removed) and never spawn (see the §7 note). The
> `Boid` mound code stays in the class (drone abilities use it via BoidController);
> the tadpole prefab just never invokes it (Explode-only, `forager: 1`).

### 7.2 Trail-management test deployments (two scenes)

The food web is wired into two scenes to test the ecosystem's ability to manage
**trail** prisms (player/AI mass), not just flora:

**A. Menu_Main freestyle toy box** (`Blob Cell Config → Blob Cell Spawn Profile`).
Flora + two herbivores: tadpole forager (`PopulationSize` 25, the swarm) +
brittlestar. **No shark** (removed — see the predator note below). The
autopilot/player vessel lays trails; the tadpole grazes any unshielded non-fauna
mass (incl. the dominant trail + flora), the brittlestar grazes opposing mass.
Goal: a self-sustaining scene that stays visually interesting and playable
**indefinitely**. Levers: tadpole `PopulationSize` (swarm density), `Blob Cell
Spawn Profile` `BaseFaunaSpawnTime` / `FaunaFoodFloor`, `starvationSeconds` /
`consumeRadius`.

**B. Skim Race** (`MinigameHexRace`, dedicated `Skim Race Cell Config → Skim Race
Spawn Profile`, isolated from the 6 other scenes that share the Barren config).
**No flora**; only the herbivore forager swarm (tadpole `PopulationSize` 12 +
brittlestar), **Random** spawner (`cellTypeChoiceOptions = 0`) so spawning is
prey-linked. Hypothesis: at late laps / high player counts, AI orbiting crystals
leave an excess of **trail-prism obstacles**; the forager swarm grazes them →
fewer prisms → better perf; foragers self-limit (starve) once the obstacles are
cleared.

> **Sharks (predators) are currently REMOVED from both test scenes.** Why: all
> fauna spawn co-located at the cell centre, and once predator detection was
> generalized to the `Fauna` base (so sharks eat `Boid` tadpoles, not just
> `LightFauna`), the sharks ate **every** herbivore the instant it spawned —
> leaving "only sharks" and nothing to graze trails. Predators are also
> counterproductive to the perf goal (they remove foragers). **Spawn immunity is
> now built** (`Fauna.predationImmunitySeconds`, default 6s, stamped in `Awake`;
> `Predated` refuses during the window) so a balanced predator CAN be re-added
> safely — just add the `Blob Shark` config back to the menu profile (keep
> `PopulationSize` low). I left it out so it doesn't muddy the foraging test; say
> the word and I'll wire it in.

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

---

## 8. Build order

1. ✅ Map + agree the redesign and the §6 bound.
2. ✅ Spawn rewrite in `RandomLifeSpawner`: timer-only, fixed period, fixed
   population N, `ControllingDomain`; `CurrentFaunaSpawnPeriod` simplified to base
   period. (`IntensityWiseLifeSpawner` left as-is — it is STILL USED by the
   WildlifeBlitz + Tournament scenes via `cellTypeChoiceOptions: 1`; do NOT delete.)
3. ✅ §6 bound = option C: `OpposingBlockCount` prey signal + `FaunaFoodFloor`
   production gate + `starvationSeconds` despawn. Config on `SpawnProfileSO` /
   `FaunaConfigurationSO` / `Fauna`.
4. ⏳ Validate in Menu_Main: Jade fauna appear when Jade controls; populations
   appear, hunt, and thin out as prey runs low; ring sweeps at the fixed period.
   Tune the §6 knobs.
5. ✅/⏳ Predator/herbivore diet split — **code landed** (`FaunaDiet` + `Fauna.diet`
   + `LightFauna` consume branch + `Fauna.Predated`); two-tier starvation =
   Lotka–Volterra. Remaining: author a predator prefab/config and wire it into a
   `SpawnProfileSO`, then tune in-editor (§7.1).
6. ⏳ Retire the regrowth pulse — **done** (`FloraGrowingEnabled = phase < Frozen`).

---

## 9. Key files

| Concern | File |
|---|---|
| Prism count, phase, gates, aggression, controlling domain | `Assets/_Scripts/Controller/Environment/Cell.cs` |
| Phase thresholds + hysteresis | `Assets/_Scripts/.../CellPhaseRules.cs`, `CellPhase` enum, Blob Cell Config asset |
| Spawner all scenes run | `Assets/_Scripts/Controller/Environment/RandomLifeSpawner.cs` |
| Regulated spawner — USED by WildlifeBlitz + Tournament (`cellTypeChoiceOptions: 1`) | `Assets/_Scripts/Controller/Environment/IntensityWiseLifeSpawner.cs` |
| Spawn helpers (`SpawnFaunaWithDomain`, `PickRandomDomain`) | `Assets/_Scripts/Controller/Environment/CellLifeSpawnerBase.cs` |
| Fauna base: domain, goal, diet, starvation, `Predated` | `Assets/_Scripts/Controller/Environment/FloraAndFauna/Fauna.cs` |
| Creature behavior + diet-branched consume (herbivore prisms / predator fauna) | `Assets/_Scripts/Controller/Environment/FloraAndFauna/LightFauna.cs` |
| Diet enum (Herbivore / Predator) | `Assets/_Scripts/Data/Enums/FaunaDiet.cs` |
| Flora growth gate (now just `phase < Frozen`) | `AssembledFlora.cs`, `BranchingFlora.cs`, `Cell.FloraGrowingEnabled` |
| Spawn tuning (period, population, food floor) | `SpawnProfileSO.cs`, `FaunaConfigurationSO.cs` |
| Aggression enum + tier behaviors | `Assets/_Scripts/Data/Enums/CellAggressionLevel.cs` |
| Indicator (hex gauge + spawn ring, no numbers) | `Assets/_Scripts/UI/DomainVolumeIndicator.cs` |

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
   target device, lower `Blob Cell Config` `FrozenEnter`/`RabidEnter` (one asset).
3. **`IntensityWiseLifeSpawner` is LIVE, not dead** (earlier notes were wrong).
   The WildlifeBlitz + Tournament scenes select it (`cellTypeChoiceOptions: 1`);
   Menu/Skim Race/etc. use `Random`. **Do NOT delete it.** It has diverged from
   `RandomLifeSpawner` (no prey-linked `FaunaFoodFloor` gate, spawns 1/tick not a
   population), so if those scenes ever wire fauna into their `SupportedFaunas`
   they'll behave differently — reconcile the two spawners then, don't remove one.
4. **Confirm the global defaults are wanted in gameplay**, not just the menu: the
   flora regrowth pulse (`SpawnProfileSO`, code-default ON) and prey-linked fauna
   (controlling-color + starvation) now apply to every biome. If a gameplay biome
   wants the old hard-freeze, set its `FloraRegrowthPulseDuration = 0`… (add an
   explicit off switch if we want one).
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

3. **Fauna reproduction** — *retires the fixed-period-spawner cheat.*
   Well-fed fauna reproduce; the spawner becomes a one-time *seeder*, not the
   population driver. Population becomes a true function of the food web.

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
   Different flora favor different phases (pioneers at Sprout, canopy at Settled),
   so a maturing cell visibly changes character.

7. **Cross-cell ecology (migration).**
   Fauna migrate to adjacent cells chasing prey; crowded/empty cells rebalance —
   isolated cells become one connected biome.

**Cheats currently in place, to retire as Phase 2 lands:** the flora regrowth
pulse and the fixed-period fauna spawner (→ step 3, reproduction). Both are
honest first-approximation scaffolding, flagged so we remove them rather than
build on them. **Note:** retiring the regrowth pulse is *not* replaced by prism
decay (that was the rejected step 1 — see §0). It is replaced by *nothing* on the
removal side — mass is conserved — so once the pulse is gone a cell only comes
back down when an active force (fauna grazing / vessel abilities) eats its mass.
The down-force we strengthen is the **food web** (step 2), not a culler.
