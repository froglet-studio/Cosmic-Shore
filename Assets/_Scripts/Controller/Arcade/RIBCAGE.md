# Ribcage — Technical Documentation

## Overview

Ribcage is the **Rhino-only cage race**. Domains race to be first to **destroy 2,000
hostile prisms**; a hollow sphere of shielded prism bone pens the cell's brood, and
breaking out of it is both the score and the trigger that arms the ecology.

**One axis, two consequences.** Destruction is the race — `HostilePrismsDestroyed`, the
same platform stat Rampage runs on. It is also the escalation clock: as the **leading**
domain passes a quarter and then half of the target, the brood is released, wearing the
**leader's** colour, and hunts every trailing team's mass. So the mode's pressure builds
on itself — the further ahead you get, the more of the cell fights for you, while the
comeback system feeds your rivals bigger buffs to compensate.

Scoring mass is everything that is not your own team's laid trail: the cage (environment
mass, non-roster owner ⇒ hostile whatever colour it wears), rival trails, and fauna
bodies. Your own and your teammates' trails never score, so there is no lay-and-smash
farming loop.

> **The honest cost of this metric** (chosen deliberately, and recorded in
> `RibcageScoringRuleSO`'s header): fauna are not rostered attackers, so a creature eating
> a player's trail credits nobody and does not directly move anyone's score. The swarm is
> an **obstacle**, not a scoring instrument — it costs the trailing teams time at the bone,
> and its multi-prism bodies are themselves hostile mass worth points to whoever kills
> them. `ScoringMetric.PrismsRemaining` (a live standing-mass stock, which *would* let the
> swarm un-score you) exists and is wired end-to-end; Ribcage was briefly authored on it
> and moved back. Do not switch the metric without asking — the trade was made with eyes
> open.

- **The cage is the arena and the objective.** **14,977 prisms** at radius **360** in
  sixty-eight meridian ribs, twenty-nine latitude hoops, a woven cross-lattice and two
  polar crowns. The grille opening is ~**33u × 35u** — a near-square weave (squareness
  1.05), not the wide stripes a sparse rib count gives. Every shielded bar takes **two
  hits** — the first sheds the shield, the second shatters it — unless the hit
  *devastates*, which is the mode's core skill surface.
- **476 of the bars are DANGER traps, and they are the SOFT ones.** A danger prism is
  not a tougher bar — danger is mutually exclusive with both shield tiers
  (`PrismStateManager.MakeDangerous` clears them), so a trap bar shatters in **one** hit
  with no shield to shed. What it costs is contact: the standard danger punishment
  (volume-independent full-stop slow, a 4 s all-element debuff, boost reset). The bar
  that breaks fastest is the one that hurts, so "ram everything" stops being the answer.
  (They are also the only cage prisms fauna *could* eat, which is why the pen radius
  sits inside the shell.)
- **Fauna cannot touch the cage, by construction.** Shielded mass is not food for any
  herbivore (`Docs/ECOSYSTEM.md` §16.2) and — since this branch — is not a fauna
  steering target either. So the race can neither be eaten out from under the players
  nor stall with a swarm parked on bars it cannot chew. **Nothing in the cage is
  super-shielded**: that tier is fully invulnerable (`Prism.Damage` returns early), so
  one such bar would be permanently unbreakable mass and enough of them would put the
  target out of reach.
- **The leader IS the cell's controlling domain.** That single publication is the
  whole fauna hook. Fauna already spawn in exactly one colour — the cell's controlling
  colour (the locked no-domain-asymmetry invariant) — and herbivores in a
  nucleus-less cell already eat **opposing-domain** mass. So the brood hatches wearing
  the leader's colours and hunts every trailing team's trails. **There is no
  "target the loser" code anywhere; the diet rule was always this.** When the lead
  changes hands the override re-colours the live swarm and its diet flips with it.
- **The brood starts penned, and it bites.** The cage is stocked from the first
  frame - visible through the bars - but *contained*: while penned, mass outside the
  cage is not food, so the brood cannot touch the match going on outside. Fly INTO
  the cage and your trail is on the menu. 25% opens the pen and floors the cell at
  Restless; 50% adds the predator and floors it at Frenzy. Aggression, steering,
  danger-immunity and speed all fall out of the existing
  `CellPhase → CellAggressionLevel` mapping.

**Key architectural facts:**

- **Scene**: `Assets/_Scenes/Multiplayer Scenes/MinigameRibcage.unity` (single unified
  scene, cloned from Rampage's skeleton — no separate singleplayer variant; solo play
  is a party of one + AI backfill)
- **GameMode enum**: `GameModes.Ribcage = 39`
- **Controller**: `RibcageController : MultiplayerDomainGamesController` — structural
  sibling of `RampageController` (1 round / 1 turn, `HasEndGame=false`, server winner
  detection in `OnTurnEndedCustom`, snapshot `SyncFinalScores_ClientRpc`), plus the
  fauna ladder
- **Scoring**: `RibcageScoringRuleSO` (`metric = ScoringMetric.PrismsDestroyed`, reads
  `IRoundStats.HostilePrismsDestroyed`; golf-timed like HexRace/Scurry/Rampage) —
  winning-domain players `Score = finish time`, losers the `GolfScoreSentinels` sentinel
  (displayed "N Bars Left")
- **Turn monitor**: `RibcagePrismTurnMonitor` — resolves the destruction target from
  `EndConditionOverridesSO.GetRibcagePrismTarget()` (default **2000**, FrogletTools ▸
  Game Modes ▸ End Game Conditions — never a per-scene field), syncs it via
  NetworkVariable → `GameDataSO.PrismTargetCount`. The fauna rungs are **fractions of that
  target** on `RibcageController` (0.25 / 0.5 ⇒ 500 / 1000), so moving the target moves the
  whole escalation ladder with it
- **Domains**: `MinDomainsAllowed = 2` (like Joust — a cage race with everyone on one
  team has no one to feed the swarm), `MaxDomainsAllowed = 3`; players **2–4** with AI
  backfill
- **Vessels**: **Rhino only** (`ArcadeGameRibcage.Vessels` has one entry). The mode is
  built around the ram and the two-hit shielded bar. `SO_ArcadeGame.Vessels` used to be
  only the UI's list of CHOICES - nothing validated the selection at launch, so a
  vessel picked in an earlier game persisted into a mode that does not allow it (a
  Dolphin flew Ribcage while its AI opponents correctly spawned Rhinos, whose class
  comes from the scene's own `aiInitializeDatas`). `GameDataSO.SyncFromArcadeGame` now
  clamps `selectedVesselClass` into the game's allowed set, so a single-vessel mode
  cannot be entered in the wrong hull by ANY route - modal, rematch, or the Tournament
  chain.
- **Config**: `_SO_Assets/Games/ArcadeGameRibcage.asset` (registered in
  `GameLists/OrganicRematchGames.asset`, `ProgressionConfig.alwaysUnlockedModes`)

## The pipeline (zero bespoke tracking)

The stat was already plumbed platform-wide (Rampage runs on it); the mode only picks it and
reads it twice — once to end the turn, once to drive the ladder.

```
Rhino shatters a bar (2nd hit; 1st sheds the shield, or 1 hit on a danger trap)
  └─ Prism.Damage → SetupDestruction → onTrailBlockDestroyed.Raise(PrismStats{…})
              ▼
StatsManager.PrismDestroyed → HostilePrismsDestroyed++   (cage mass is non-roster ⇒ hostile;
                                                          your own team's trail is filtered out)
              ▼
ScoringMetrics.Read(stats, PrismsDestroyed) → SumByDomain
  ├─ MultiplayerDomainGamesController.SyncDomainSumsRoutine → HUD domain panels
  ├─ RibcagePrismTurnMonitor.CheckForEndOfTurn → rule.IsObjectiveReached   [server]
  ├─ RibcageController.SampleLadder → leader + release rung                [server]
  └─ ElementalComebackSystem (source PrismsDestroyed) → trailing-team buff
              │  turn end
              ▼
RibcageController.OnTurnEndedCustom → AssignScores → SyncFinalScores_ClientRpc
```

The rung is keyed on the **leader's own** progress (`best / target`), not a cross-domain
total: the race and the trigger are one axis, so "the leader is a quarter of the way out"
is the signal — and it means the swarm arrives at a fixed point in the *race*, not at a
point that a busy lobby reaches four times faster than a duel.

## The fauna ladder (zero bespoke ecology)

The controller publishes **two facts** to the arena cell and lets the existing ecology
draw every consequence. It contains no fauna targeting code at all.

```
RibcageController.SampleLadder            [server, every ladderSampleSeconds = 0.5s]
  │  leader   = active domain with the highest HostilePrismsDestroyed sum
  │  progress = that leader's own sum / PrismTargetCount  (the race IS the clock)
  │
  ├─ PublishLeader_ClientRpc  → Cell.SetModeControlOverride(leader)   [EVERY peer]
  │     ├─ Cell.DominantDomain now returns the leader
  │     │     └─ Cell.ControllingDomain → RandomLifeSpawner spawns the wave in
  │     │        the leader's colour  (no-domain-asymmetry: ONE colour, the
  │     │        controller's — unchanged)
  │     ├─ Cell.IsPreyForHerbivore (no nucleus zone) → preyDomain != faunaDomain
  │     │     └─ the swarm eats every TRAILING team's trails. That is the whole
  │     │        "fauna hunt the losers" feature: the legacy diet rule, unmodified.
  │     └─ live swarm re-coloured via Fauna.SetTeam, so a lead change flips the
  │        targets of the creatures already in the air, not just the next wave
  │
  └─ PublishRelease_ClientRpc → RibcageController.ApplyStage                 [EVERY peer]
        one place sets all three levers, so they can never disagree:
        Cell.FaunaReleaseTier · Cell.FaunaContainmentRadius · Cell.ModePhaseFloor
        Caged  (progress < 0.25) : brood seeds (species tier 0) but
                          Cell.FaunaContainmentRadius pens it, so mass OUTSIDE is not prey
                          and every goal is clamped inside. No phase floor - Calm, so they
                          idle at the core. They eat the trail of anything that flies IN.
        Loosed (>= 0.25, i.e. 500 of 2000) : containment cleared; floor = Restless →
                          CellAggressionLevel.Level1 (steer at the opposing-colour centroid
                          = every trailing team's mass). The swarm pours out through the bone.
        Pack   (>= 0.50, i.e. 1000) : Ribcage Shark (ReleaseTier 1) joins; floor = Frenzy →
                          Level2 (any-colour steering, friendly avoidance off, danger-immune)
```

Both publications are ClientRpcs because **fauna are client-local** (no NetworkObject),
so every peer must run its own gate. The control pin would also replicate through
`CellNetworkSync` on its own 0.5s mirror; the RPC just makes the swarm change colour on
the event rather than on the next tick.

`ApplyRelease` calls `Cell.RestartSpawnerForMode()` when a tier opens, so the fauna
spawn clock realigns to the **release moment** — otherwise the gate opens mid-period
and the brood can take a full `BaseFaunaSpawnTime` to appear, which reads as the reward
simply not arriving. The profile authors no flora, so the "restart re-runs the initial
flora batch" caveat on that method does not apply here.

### Why the leader gets *helped* rather than punished

The swarm is a snowball, and the counterweight is already in the box:
`ElementalComebackSystem` runs on `ScoreDifferenceSource.PrismsDestroyed` — the same stat
that decides the race — so the further ahead the leader gets, the stronger every trailing
team's all-element buffs become. Pull too far ahead and you are fighting buffed Rhinos
whose comeback elements make them faster at the bone than you were.

**The rate tracks the target.** `ComebackRatePerScoreDeficit` is **0.01** — identical to
Rampage, which races to the same 2,000 destruction target, so the 10-level ceiling lands at
a deficit of 1,000 (half the target). If you re-tune the target, re-tune this with it:
`rate ≈ 10 / (target / 2)`.

## Ecology configuration

`_SO_Assets/Cell Configs/Ribcage Cell/`:

- **Ribcage Cell Config** — Blob-class membrane/cytoplasm, `EnvironmentPrefab` =
  `SpawnableRibcage.prefab`. **NO `NucleusPrefab`, and that is load-bearing:** a
  nucleus control zone switches herbivores to the spatial "eat anything outside the
  nucleus" diet (`Cell.IsPreyForHerbivore`), which would point the swarm at *every*
  team including the leader's and break the entire hook. Ribcage needs the legacy
  opposing-domain diet.
  `PhaseThresholds` ride the measured cage baseline + the standard Blob deltas
  (`Docs/ECOSYSTEM.md` §18): Restless 15,677/15,477 (volume 5,893,312/5,890,112),
  Frenzy 18,577/17,977 (volume 5,939,712/5,930,112). The cell therefore boots **Calm**,
  and since destruction only *lowers* volume the ladder never climbs on its own — the
  mode's phase floor is the only thing that raises it, which is exactly the intent.
- **Ribcage Spawn Profile** — **no flora** (the cage is the arena; flora would add
  unshielded mass that fauna erode and that dilutes the cage as the scoring target).
  `InitialFaunaReleaseTier 0` (the brood exists from the first tick - the CAGE contains
  it, not a spawn gate; authoring the start state as biome DATA is what keeps it
  independent of the controller's `OnNetworkSpawn` beating the cell's own bootstrap
  clock, a race the runtime-only seal lost), `InitialFaunaSpawnWaitTime 0`,
  `BaseFaunaSpawnTime 15`, `FaunaFoodFloor 0`. Herbivore ring: 4 points at radius
  **200** and predator ring 2 points at **250** — both **inside** the 338-unit pen (and so
  inside the 360-unit shell), so the brood hatches within the ribs and pours out through
  the bars the players break.
### The brood — five species

Four herbivore species share the pen and the predator joins at 50%. Seeds hatch
immediately; MaxLive is the per-species performance backstop the food web works under.

| species | prefab | tier | seed | MaxLive | role |
|---|---|---:|---:|---:|---|
| Tadpole | `TadPoleFauna` (Boid) | 0 | 39 | 72 | the shoal — fast, numerous, the "swarm" read |
| QuadFish | `QuadFish` (LightFauna) | 0 | 20 | 33 | mid-size rovers |
| Clawfish | `Clawfish` (QuadFish) | 0 | 14 | 24 | heavier, slower, most threatening silhouette |
| Brittlestar | `MassBrittlestarFauna` (LightFauna) | 0 | 12 | 20 | drifting arms — fills the volume |
| **caged total** | | | **85** | **149** | |
| Shark | `MassSharkFauna` (LightFauna) | 1 | 5 | 9 | the 50% **predator** — eats herbivores, not prisms |

All five drop elemental crystals on death like every lifeform, so a cleared cage is also
a powerup field.

Seeding 85 prism-bodied creatures on one tick would be a frame spike, so
`RandomLifeSpawner.SpawnFaunaPopulation` is now a coroutine that yields every
`FaunaSpawnBatchPerFrame` (6) — the same treatment the flora batch beside it already had.
That is a shared-spawner improvement, not a Ribcage special case: any densely-stocked
biome gets it.

### Intruder frenzy — why going inside is a mistake

`Cell.ContainmentIntruderFrenzy` (off by default; Ribcage turns it on): while the brood is
penned, a creature that DETECTS edible mass inside the pen sends the whole population to
**Frenzy** — `CellAggressionLevel.Level2`: any-colour steering, friendly avoidance off,
danger-immune, fastest cadence and widest consume radius. Flying in does not merely put
your trail on the menu; it turns ~150 creatures onto it at once, and they stay berserk
until you and your mass are gone.

Detection is `Cell.HasPreyInsideFaunaContainment`: one Burst `PrismSpatialIndex.QuerySphere`
on the **phase tick** (0.4 s cadence, shared buffer), never a physics query, and only while
a pen exists. Shielded mass is filtered out, and the pen radius (**338**) deliberately sits
inside the cage shell (**360**) so the cage's own bars — the shielded ones *and* the
unshielded danger traps — are outside the pen and can never register as an intruder or be
eaten.

Both species are Blob-lineage clones (same prefabs, element palettes and level spread),
so they drop elemental crystals on death like every other lifeform — skimmable
powerups mid-match.

## The cage

`SpawnableRibcage : CellEnvironmentSpawnableBase`, radius **360**, seed 39,
deterministic per seed like every cell environment. Analytic budget
(`Tools/Build/ribcage_budget.py`; confirm with FrogletTools ▸ Ecology ▸ Measure Cell
Environment Baselines):

| structure | count | vol/prism | volume | detail |
|---|---:|---:|---:|---|
| meridian ribs (shielded) | 8,568 | 431.3 | 3,695,454 | 68 ribs × 133, minus the traps |
| — of which DANGER traps | 476 | 431.3 | 205,303 | every 19th rib prism |
| latitude hoops | 2,701 | 431.3 | 1,164,965 | 29 hoops out to ±78° |
| cross-lattice | 1,224 | 131.8 | 161,309 | 68 × 6 bands × 3 |
| joints | 1,972 | 327.5 | 645,880 | 68 × 29 crossings |
| polar crowns | 36 | 255.6 | 9,201 | 2 × 18 at lat ±84 |
| **TOTAL** | **14,977** | | **5,882,112** | |

The grille opening is ~**33.3u × 35.0u** (squareness 1.05), so this is a **ribcage, not a
prison grille**: you fly between the bones freely. Sealing the sphere to vessel-tight
spacing would cost tens of thousands more prisms of always-on collider for no gameplay —
the goal is to smash the structure, never to be locked inside it.

**Collider-budget impact — read this before tuning anything else. This is the branch's
headline performance risk.** ~14,977 box colliders for the cage, plus the brood (~1
`HealthPrism` per creature, so the caged cap of 149 / 158 with sharks adds ~150 more).
Shielded prisms keep the authored **BoxCollider trigger** (`PrismOctahedronShield` changes
the LOOK only — a convex-mesh collider is invisible to one skimmer family or the other), so
a shielded bar costs exactly what a plain prism costs and the octahedron look is free.

That is **~10× the masterplan's ≤1,500 per-cell target** and **~1.5× Rampage's deliberate
10,000-prism arena gate** — precedent exists for a heavy arena, but Ribcage is now the
heaviest in the game by a clear margin. It is spent deliberately: density is what makes the
thing read as a cage, the cell authors **no flora**, and the mode's whole verb actively
removes colliders as the match runs. Mitigations are the standing ones (collider-LOD by
phase, Burst density-grid fauna queries, no new physics queries anywhere — fauna senses ride
`PrismSpatialIndex`, scoring rides the StatsManager SOAP channel, and the AI aims
analytically, see below).

**Measure it on device first** (DiagnosticsHUD / Benchmark tool). The cheapest dial is
`RibCount` — the ribs plus their joints are ~70% of the cage, and every rib removed takes
~220 prisms with it (133 bar + 29 joint + 18 strut, ×(1 − danger share)); after that,
`HoopCount`, then `BarStep`. Re-run **both** Python tools after any change:
`ribcage_budget.py` for the new baseline and `author_ribcage_assets.py` to push it into
`PhaseThresholds`.

## Spawning outside the cage

Players start on the computed cell spawn ring (`CellSpawnFormation` — symmetric, all
facing the cell), NOT on authored transforms: the donor scene's four points sat at ±50,
which is deep inside the cage, so everyone started penned in with the brood.

The ring normally measures off the cell's nucleus radius, and Ribcage's cell
deliberately has none — so it would have collapsed to the cell centre, i.e. the same
bug. `ServerPlayerVesselInitializer.spawnRingRadiusFloor` (new, default 0 = every
existing scene unchanged) gives the ring a floor for exactly this case: a cell whose
"core" is a structure rather than a nucleus. Ribcage authors **576** — outside the 360u
cage, well inside the 1200u membrane, and far enough back to see the whole cage and
line up a charge.

## AI cage-breakers

**Every AI station is OUTSIDE the shell. That is the whole fix.** `AIPilot` has no
arrive-and-stop behaviour — it steers at `_targetPosition` forever and simply flies
through on arrival — so *any* target inside the cage becomes a point the AI loops around
from within. That is what "the AI just stays inside" was, twice: first when stations sat
*on* the shell (it carries through, and the next station is across the middle), then again
when a two-waypoint approach/punch cycle put the punch waypoint at 0.55×R — an explicit
instruction to fly to a point inside the cage and hold there for 2 s.

Now there is one station per strike, always at **1.3×R**. Stations walk a golden-angle
spiral, so successive stations are ~137° apart and the **chord between them passes close
to the centre** — a full crossing of the cage, which is what shatters bars. The loitering
happens outside; the damage happens on the transit. Each AI is phased onto its own arc so
a full lobby spreads around the sphere.

**Every 4th strike is a RAID** on `Cell.GetExplosionTarget` — the densest mass hostile to
its domain, which since the shielded-grid change means opponents' trails and anything a
rival left inside the cage. That is where "sometimes it goes inside / hits opponent
prisms" comes from, and it is a real strategy rather than a scripted detour: the same
density query aggression-1 fauna use. The raid beat is offset by seat so the AIs never
all raid at once.

Deliberately **not** a pure density-grid mass hunt for the cage itself (Rampage's
pattern): the cage is shielded and shielded mass is kept out of the targeting grids, so
the grids would send every AI chasing vessels instead of breaking out. The shell is an
analytic sphere, so aiming at it needs no query at all.

Kept beatable on purpose: one station per `aiRetargetSeconds` (2 s), so it is methodical
rather than twitchy, and raids are a minority of strikes.

**Invariant for anyone re-tuning this:** `AiStationStandoff` must stay **> 1**. A value
≤ 1 puts the station on or inside the bone and the AI moves in permanently.

## Feedback — the alert shake

Reaching a release rung fires `HapticController.PlayAlert()` on every peer: ~1.2 s of hard
rattling. This is the game's **third** haptic feel and Ribcage's rungs are the only thing
that fires it — added deliberately under `Docs/HAPTICS.md` ▸ "Adding / changing a feel"
(dedicated method, gate extended so it outranks both the skim pulse and the punish thud
for its duration, rate-limited so it can never stack into a drone). It is safe to call on
every peer because `HapticController` gates on the local player's own haptics setting.

Toast copy for the rungs is still unauthored, so **right now the shake IS the release
feedback**. More is planned — this is the first layer, not the finished treatment.

## End condition

Authored ONLY through **FrogletTools ▸ Game Modes ▸ End Game Conditions**
(`EndConditionOverridesSO.ribcagePrismTarget`, 0 = default **2000**) — the number of
hostile prisms a domain must DESTROY to win, the same target Rampage races to. Live/Build
split + build auto-restore work like every other mode.

The fauna rungs **are** derived from it: `broodReleaseFraction` 0.25 and
`packReleaseFraction` 0.5 on `RibcageController`, evaluated against the **leader's** own
sum (⇒ 500 and 1,000 at the default target). Move the target and the whole escalation
ladder moves with it, which is the point — a fraction cannot drift out of step with the
race the way an absolute count did. 2,000 of a 14,977-prism cage is ~13% of the bone, so
rival trails and fauna bodies are a meaningful share of a realistic winning line.

## Shared-code touchpoints (added for this mode)

| Site | Change |
|---|---|
| `GameModes` | `Ribcage = 39` |
| `GameToastSituation` | `RibcageBroodReleased = 50`, `RibcagePackReleased = 51`, `RibcageLeaderChanged = 52` |
| `Cell` | `SetModeControlOverride` (+ live-swarm re-colour), `ModePhaseFloor`, `FaunaReleaseTier`, `FaunaContainmentRadius` / `IsInsideFaunaContainment` / `ClampToFaunaContainment`, `ContainmentIntruderFrenzy` + `HasPreyInsideFaunaContainment`, `NotifyBlockShieldStateChanged`, shielded mass excluded from the targeting grids, release tier seeded from the spawn profile at config-assign |
| `HapticController` | `PlayAlert()` — the third feel, gate extended (`s_alertBusyUntil` outranks skim + punish) per `Docs/HAPTICS.md` |
| `Fauna` | `Goal` becomes a PROPERTY so containment clamps at the one point every writer passes through (this class, Boid's override, LightFauna's direct writes, the spawner, reproduction inheritance) |
| `GameDataSO` | `SyncFromArcadeGame` clamps `selectedVesselClass` into `SO_ArcadeGame.Vessels` - enforces every restricted-vessel mode on every launch path |
| `ServerPlayerVesselInitializer` | `spawnRingRadiusFloor` - lets the computed spawn ring serve a cell whose core is a STRUCTURE rather than a nucleus |
| `SpawnProfileSO` | `InitialFaunaReleaseTier` - the biome's START tier, seeded before any spawner can tick |
| `IntensityWiseLifeSpawner` | honours `ReleaseTier` too, so which spawner a biome uses cannot decide whether a mode's gate holds |
| `PrismSpatialIndex` | `ForwardShieldChangeToCell` |
| `PrismStateManager` | `SyncAOERegistryShieldState` also re-files the prism in its cell's grids |
| `RandomLifeSpawner` | staged-release gate (`faunaCfg.ReleaseTier <= host.FaunaReleaseTier`) |
| `FaunaConfigurationSO` | `ReleaseTier` (default 0 — no shipped biome changes) |
| `EndConditionOverridesSO` (+ window + asset) | `ribcagePrismTarget` live/build/getter, default 2000 |
| `ElementalComebackSystem` | `GameModes.Ribcage` shares Rampage's `ScoreDifferenceSource.PrismsDestroyed` case |
| `ScoringMetric` / `ScoringMetrics.Read` | `PrismsRemaining = 6` → `stats.PrismsRemaining` — added for the standing-mass variant of this mode, kept as an available-but-unused metric rather than churning a shared serialized enum a second time |

### The one cross-mode behaviour change: shielded mass leaves the targeting grids

`Cell.AddBlock`'s own comment already stated the rule — *"fauna must never be led to
mass they cannot eat"* — and applied it only to nucleus-interior mass. `Docs/ECOSYSTEM.md`
§16.2 then removed shielded prisms from every herbivore's **diet**, but they stayed in
the **grids**, so density centroids kept steering swarms onto mass the creatures had
just been told they could not eat. That is the residue behind §16.3's Skim Race stall,
and it is fatal to a mode whose arena *is* a shielded structure.

This branch finishes the rule: shielded prisms are excluded from the targeting grids at
`AddBlock`, and `NotifyBlockShieldStateChanged` (called from the single funnel every
shield transition already passes through) re-files a prism when a shield engages or is
shed. It strictly *reduces* grid work and adds no query.

**It affects two other modes and both are improvements** — Skim Race's super-shielded
track and Astro League's super-shielded edge lining no longer pull fauna steering.
Verify both in-editor (below) rather than assuming.

## Assets

| Asset | Path |
|---|---|
| Arcade game config | `_SO_Assets/Games/ArcadeGameRibcage.asset` |
| Scoring rule | `_SO_Assets/Scoring Rules/RibcageScoringRule.asset` |
| Cell config | `_SO_Assets/Cell Configs/Ribcage Cell/Ribcage Cell Config.asset` |
| Spawn profile | `_SO_Assets/Cell Configs/Ribcage Cell/Ribcage Spawn Profile.asset` |
| Fauna species (5) | `…/Ribcage {Tadpole,QuadFish,Clawfish,Brittlestar,Shark} Fauna Config Data.asset` |
| Cage prefab | `_Prefabs/Spawnables/SpawnableRibcage.prefab` |
| Scene | `_Scenes/Multiplayer Scenes/MinigameRibcage.unity` (in `EditorBuildSettings`) |
| End conditions | `Assets/Resources/EndConditionOverrides.asset` (`ribcagePrismTarget`) |

Every asset above is authored by `Tools/Build/author_ribcage_assets.py` — deterministic
GUIDs, idempotent, validates before writing. **Re-tune there and re-run** rather than
hand-editing the YAML, so the generator stays the source of truth.
`Tools/Build/ribcage_budget.py` is the cage's analytic budget model; keep it in sync
with `SpawnableRibcage.cs` when the geometry changes.

## In-editor verification (authored headless — NOT yet run)

1. **Open** `MinigameRibcage.unity`. Every script reference resolves (no "Missing
   (Mono Script)"), the controller's inspector shows `rule` = RibcageScoringRule,
   `arenaCell` = the scene Cell, and the release fractions 0.25 / 0.5.
2. **Cage builds.** Enter play mode solo (party of one + 3 AI backfill). The connecting
   screen should hold until the cage is fully laid and grown
   (`PrismTrailBuilder.PollArenaReady`), then reveal a complete ribcage — **nothing
   should pop in after the countdown** (continuity of existence). If the cell's
   deferred build starts after the connecting screen releases, an `EnvironmentLoadVeil`
   takes over instead; either path is correct, but confirm which one you see.
3. **Baseline confirm.** FrogletTools ▸ Ecology ▸ Measure Cell Environment Baselines
   should report **14,977 prisms / 5,882,112 volume** for the Ribcage cell. If it
   disagrees, the generator and `ribcage_budget.py` have drifted — fix both.
4. **Bars are two-hit — except the traps.** Ram a rib: first contact sheds the shield
   (octahedron disengages), second shatters it and the HUD sum increments. Then find a
   **danger** bar (distinct material): it shatters in ONE hit but full-stops you, debuffs
   all four elements for 4 s and resets boost.
5. **Rhino only.** Pick a different vessel in an earlier game, then launch Ribcage —
   you should spawn a Rhino anyway, with a `clamping selected vessel` line in the log.
6. **Spawn outside.** All players start on a ring ~576u out, facing the cage, with the
   whole cage visible ahead — nobody starts inside it.
7. **The penned brood + intruder frenzy.** The cage is visibly full (~85 creatures of
   four species) and they stay inside. While penned they must NOT eat anything outside —
   fly around the outside laying trail; it should be ignored. Then fly IN: the cell
   should jump to **Frenzy** on the DiagnosticsHUD and the whole pen should converge on
   your trail. Leave, and it should settle back to Calm once your mass is gone.
8. **Smashing scores; laying does not.** The HUD domain sum should rise as you break bars
   and rise *not at all* from laying trail. Then shatter one of your OWN team's trail
   prisms — the sum must not move (rostered same-domain mass is filtered out), while a
   rival's trail and a fauna body both count.
9. **Brood rung.** When the LEADING domain reaches **500** destroyed (25% of 2,000) the pen
   opens and the swarm leaves wearing that leader's colour to eat the *trailing* domains'
   mass. **The device should shake hard for ~1.2 s** (the alert feel).
10. **Pack rung.** At **1,000** by the leader the pack toast fires, the device shakes again,
   and sharks join; the cell reads Frenzy on the DiagnosticsHUD.
11. **Lead change flips the swarm.** Let a second domain take the lead — the *live*
   creatures should re-colour and switch which trails they eat.
12. **Win + scoreboard.** First domain to **2,000 destroyed** ends the turn; winners show a
    time, losers "N Bars Left". Replay (scene reload) re-pens the brood and resets the
    ladder.
13. **AI stays outside.** Watch an AI Rhino for a minute: it should orbit outside, cross
    the cage on transits, and only be inside briefly — during a crossing or a raid. If it
    settles inside, `AiStationStandoff` has been set ≤ 1.
14. **Regression — the grid change.** Play **Skim Race** (intensity 3) and **Astro
    League**: fauna should behave normally and should no longer park against the
    super-shielded track / edge lining.
15. **Collider telemetry** on device via DiagnosticsHUD / the Benchmark tool — at ~15k
    colliders this is the most likely thing to force a retreat. If the cage is too heavy,
    lower `RibCount` (68) or `HoopCount` (29), or raise `BarStep` (17) in
    `SpawnableRibcage.cs`, then re-run **both** Python tools.

## Known limitations / follow-ups

- **Toast copy is unauthored.** The three `GameToastSituation` values exist but no
  `GameToastConfigSO` authors a definition for them, so they are silently skipped
  (which is how a mode opts out). Author a `GameToastConfig_Ribcage.asset` with
  `{0}`=domain, `{1}`=bars smashed, `{2}`=target to make them visible.
- **No objective-arrow provider**: like Rampage and Brood Rush,
  `MiniGameHUD.CreateObjectiveProviderForGameMode` has no Ribcage case — the cage
  surrounds you, so there is no single point to aim at.
- **No UGS stats reporter yet** (a "most bars smashed" leaderboard is a clean
  follow-up), and no dedicated end-game controller — the shared scoreboard handles it.
- **Danger bars are a first pass.** 476 of 14,977 (one in 19 rib prisms), evenly spread by
  a deterministic index walk. If they read as noise rather than as traps, cluster them
  instead (e.g. whole trap *segments* of a rib) — one constant,
  `SpawnableRibcage.DangerEveryNthRibPrism`.
- **Target 2,000 is inherited from Rampage, not measured for Ribcage.** Rampage's mass is
  unshielded; every cage bar here takes two hits, so the same number is a longer match.
  It is one editor field, and the release rungs follow it automatically.
- **The fauna do not move the score directly.** See the metric note at the top — this is a
  known, deliberate consequence of racing on destruction rather than on standing mass. If
  playtests say the swarm feels inert, the options are (a) switch the rule asset to
  `ScoringMetric.PrismsRemaining` (already wired end-to-end) — **ask first**, or (b) make
  fauna kills worth more by leaning on their multi-prism bodies.
- **`Cell.OpposingVolume` still counts shielded mass** as the fauna prey signal, so a
  shielded structure satisfies `FaunaFoodFloor` without being food. Ribcage sidesteps
  it (`FaunaFoodFloor 0` — the release tier is the real gate), but the honest fix is to
  net shielded volume out of that signal. Left alone deliberately: it is the population
  bound for every biome, so it deserves its own change and its own verification.
