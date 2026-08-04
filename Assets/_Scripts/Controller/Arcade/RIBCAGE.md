# Ribcage — Technical Documentation

## Overview

Ribcage is the **Rhino-only cage race**. Domains race to be first to hold **300 prisms
standing**; a hollow sphere of shielded prism bone pens the cell's brood, and smashing it
**scores nothing** — it arms the ecology.

**The two axes are the mode.** *Creation* is the race: `PrismsRemaining`, a LIVE stock
that rises as you lay trail and falls whenever anything destroys one of your prisms.
*Destruction* of the cage is the trigger: pass a rung and the brood is released, wearing
the **race leader's** colour, and it eats every trailing team's standing mass — which is
their score. So breaking the cage is a real decision, not a chore: you stop laying (and
fall behind) to arm a swarm that then serves whoever is ahead. Break it too early and you
have fed the leader's pets your own trail.

The metric is a live stock rather than a cumulative "prisms created" counter for exactly
this reason — a cumulative counter only ever goes up, so nothing the swarm did could set
anyone back and the whole ecology would be decoration.

- **The cage is the arena and the objective.** ~3,175 prisms at radius **360** in
  sixteen meridian ribs, seven latitude hoops, a woven cross-lattice and two polar
  crowns. Every shielded bar takes **two hits** — the first sheds the shield, the second
  shatters it — unless the hit *devastates*, which is the mode's core skill surface.
- **112 of the bars are DANGER traps, and they are the SOFT ones.** A danger prism is
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
- **Scoring**: `RibcageScoringRuleSO` (`metric = ScoringMetric.PrismsRemaining` — new,
  reads `IRoundStats.PrismsRemaining`; golf-timed like HexRace/Scurry) — winning-domain
  players `Score = finish time`, losers the `GolfScoreSentinels` sentinel (displayed
  "N To Go")
- **Turn monitor**: `RibcagePrismTurnMonitor` — resolves the STANDING-prism target from
  `EndConditionOverridesSO.GetRibcagePrismTarget()` (default **300**, FrogletTools ▸
  Game Modes ▸ End Game Conditions — never a per-scene field), syncs it via
  NetworkVariable → `GameDataSO.PrismTargetCount`. The fauna rungs are *separate*,
  absolute cage-destruction counts on `RibcageController` (150 / 350)
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

## The two pipelines (zero bespoke tracking)

Both stats were already plumbed platform-wide; the mode only picks which drives what.

**Creation → score.** `PrismsRemaining` is maintained by `StatsManager`: `++` when you lay
a prism, `--` when *anything* destroys it — a rival's ram, an AOE, or a fauna's bite. That
last one is why the swarm is a scoring force rather than an annoyance.

```
Rhino lays a trail prism → StatsManager … PrismsRemaining++
a fauna eats one of yours → StatsManager.PrismDestroyed → victim PrismsRemaining--
              ▼
ScoringMetrics.Read(stats, PrismsRemaining) → SumByDomain
  ├─ MultiplayerDomainGamesController.SyncDomainSumsRoutine → HUD domain panels
  ├─ RibcagePrismTurnMonitor.CheckForEndOfTurn → rule.IsObjectiveReached   [server]
  ├─ RibcageController.SampleLadder → who the brood serves                 [server]
  └─ ElementalComebackSystem (source PrismsRemaining) → trailing-team buff
              │  turn end
              ▼
RibcageController.OnTurnEndedCustom → AssignScores → SyncFinalScores_ClientRpc
```

**Destruction → the fauna trigger.** Smashing bone feeds `HostilePrismsDestroyed`, which
scores nothing here and only advances the ladder:

```
Rhino shatters a bar (2nd hit; 1st sheds the shield, or 1 hit on a danger trap)
  └─ Prism.Damage → SetupDestruction → onTrailBlockDestroyed.Raise(PrismStats{…})
              ▼
StatsManager.PrismDestroyed → HostilePrismsDestroyed++   (cage mass is non-roster ⇒ hostile)
              ▼
RibcageController.SampleLadder: SUM across all domains → release rung   [server]
```

The rung is keyed on the **total** across domains because the cage is one shared
structure — it does not matter who broke which bar, only that the bone is open.

## The fauna ladder (zero bespoke ecology)

The controller publishes **two facts** to the arena cell and lets the existing ecology
draw every consequence. It contains no fauna targeting code at all.

```
RibcageController.SampleLadder            [server, every ladderSampleSeconds = 0.5s]
  │  leader    = active domain with the highest PrismsRemaining sum (the RACE leader,
  │              NOT the destruction leader - the swarm serves whoever is winning)
  │  cageBroken = HostilePrismsDestroyed summed across ALL domains
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
        Caged  (cageBroken < 150) : brood seeds (species tier 0) but
                          Cell.FaunaContainmentRadius pens it, so mass OUTSIDE is not prey
                          and every goal is clamped inside. No phase floor - Calm, so they
                          idle at the core. They eat the trail of anything that flies IN.
        Loosed (>= 150)  : containment cleared; floor = Restless → CellAggressionLevel
                          .Level1 (steer at the opposing-colour centroid = every trailing
                          team's standing mass). The swarm pours out through the bone.
        Pack   (>= 350)  : Ribcage Shark (ReleaseTier 1) joins; floor = Frenzy → Level2
                          (any-colour steering, friendly avoidance off, danger-immune)
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

The swarm is a snowball, and the counterweight is already in the box: destruction feeds
`ElementalComebackSystem` (`ScoreDifferenceSource.PrismsDestroyed`), so the further
ahead the leader gets the stronger the trailing teams' all-element buffs become. Pull
too far ahead and you are fighting buffed Rhinos while your own swarm chews mass that
no longer scores for you. `ComebackRatePerScoreDeficit` is **0.03** (vs Rampage's 0.01
at a 2000 target) so a ~300-bar deficit against a 600 target reaches the same buff
ceiling a ~10-crystal deficit does in Scurry.

## Ecology configuration

`_SO_Assets/Cell Configs/Ribcage Cell/`:

- **Ribcage Cell Config** — Blob-class membrane/cytoplasm, `EnvironmentPrefab` =
  `SpawnableRibcage.prefab`. **NO `NucleusPrefab`, and that is load-bearing:** a
  nucleus control zone switches herbivores to the spatial "eat anything outside the
  nucleus" diet (`Cell.IsPreyForHerbivore`), which would point the swarm at *every*
  team including the leader's and break the entire hook. Ribcage needs the legacy
  opposing-domain diet.
  `PhaseThresholds` ride the measured cage baseline + the standard Blob deltas
  (`Docs/ECOSYSTEM.md` §18): Restless 3421/3221 (volume 1,080,580/1,077,380),
  Frenzy 6321/5721 (volume 1,126,980/1,117,380). The cell therefore boots **Calm**,
  and since destruction only *lowers* volume the ladder never climbs on its own — the
  mode's phase floor is the only thing that raises it, which is exactly the intent.
- **Ribcage Spawn Profile** — **no flora** (the cage is the arena; flora would add
  unshielded mass that fauna erode and that dilutes the cage as the scoring target).
  `InitialFaunaReleaseTier 0` (the brood exists from the first tick - the CAGE contains
  it, not a spawn gate; authoring the start state as biome DATA is what keeps it
  independent of the controller's `OnNetworkSpawn` beating the cell's own bootstrap
  clock, a race the runtime-only seal lost), `InitialFaunaSpawnWaitTime 0`,
  `BaseFaunaSpawnTime 15`, `FaunaFoodFloor 0`. Herbivore ring: 3 points at radius
  **180** and predator ring 2 points at **220** — both **inside** the 300-unit cage, so
  the brood hatches within the ribs and pours out through the bars the players break.
### The brood — five species

Four herbivore species share the pen and the predator joins at 50%. Seeds hatch
immediately; MaxLive is the per-species performance backstop the food web works under.

| species | prefab | tier | seed | MaxLive | role |
|---|---|---:|---:|---:|---|
| Tadpole | `TadPoleFauna` (Boid) | 0 | 26 | 48 | the shoal — fast, numerous, the "swarm" read |
| QuadFish | `QuadFish` (LightFauna) | 0 | 13 | 22 | mid-size rovers |
| Clawfish | `Clawfish` (QuadFish) | 0 | 9 | 16 | heavier, slower, most threatening silhouette |
| Brittlestar | `MassBrittlestarFauna` (LightFauna) | 0 | 8 | 13 | drifting arms — fills the volume |
| **caged total** | | | **56** | **99** | |
| Shark | `MassSharkFauna` (LightFauna) | 1 | 3 | 6 | the 50% **predator** — eats herbivores, not prisms |

All five drop elemental crystals on death like every lifeform, so a cleared cage is also
a powerup field.

### Intruder frenzy — why going inside is a mistake

`Cell.ContainmentIntruderFrenzy` (off by default; Ribcage turns it on): while the brood is
penned, a creature that DETECTS edible mass inside the pen sends the whole population to
**Frenzy** — `CellAggressionLevel.Level2`: any-colour steering, friendly avoidance off,
danger-immune, fastest cadence and widest consume radius. Flying in does not merely put
your trail on the menu; it turns ~100 creatures onto it at once, and they stay berserk
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

`SpawnableRibcage : CellEnvironmentSpawnableBase`, radius **300**, seed 39,
deterministic per seed like every cell environment. Analytic budget
(`Tools/Build/ribcage_budget.py`; confirm with FrogletTools ▸ Ecology ▸ Measure Cell
Environment Baselines):

| structure | count | vol/prism | volume | detail |
|---|---:|---:|---:|---|
| meridian ribs (shielded) | 2016 | 431.3 | 869,519 | 16 ribs × 133, minus the traps |
| — of which DANGER traps | 112 | 431.3 | 48,307 | every 19th rib prism |
| latitude hoops | 611 | 431.3 | 263,530 | lats 0, ±26, ±52, ±74 |
| cross-lattice | 288 | 131.8 | 37,955 | 16 pairs × 6 bands × 3 |
| joints | 112 | 327.5 | 36,683 | 16 × 7 crossings |
| polar crowns | 36 | 255.6 | 9,201 | 2 × 18 at lat ±84 |
| **TOTAL** | **3,175** | | **1,265,194** | |

The rib-to-rib gap at the equator is ~141u, so this is a **ribcage, not a prison
grille**: you fly between the bones freely. Sealing the sphere to vessel-tight spacing
would cost ~6k prisms of always-on collider for no gameplay — the goal is to smash the
structure, never to be locked inside it.

**Collider-budget impact.** ~3,175 box colliders for the cage, plus up to ~62 caged creatures (66 once the shark rung lands), whose bodies are prism-bodied. Shielded prisms keep the
authored **BoxCollider trigger** (`PrismOctahedronShield` changes the LOOK only — a
convex-mesh collider is invisible to one skimmer family or the other), so a shielded
bar costs exactly what a plain prism costs and the octahedron look is free. That is
~2.1× the masterplan's ≤1500 per-cell target and **~3× *under* Rampage's deliberate
10,000-prism arena gate**, with no flora in the cell and no new physics queries
anywhere — fauna senses ride `PrismSpatialIndex`, scoring rides the StatsManager SOAP
channel, and the AI aims analytically (below). Destruction actively removes colliders
as the match runs. Watch the collider/prism telemetry (DiagnosticsHUD / Benchmark tool)
on device; `RibCount` / `BarStep` are the two numbers to turn down.

## Spawning outside the cage

Players start on the computed cell spawn ring (`CellSpawnFormation` — symmetric, all
facing the cell), NOT on authored transforms: the donor scene's four points sat at ±50,
which is deep inside the 300u cage, so everyone started penned in with the brood.

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
(`EndConditionOverridesSO.ribcagePrismTarget`, 0 = default **300**) — the number of
prisms a domain must hold STANDING to win. Live/Build split + build auto-restore work
like every other mode.

The fauna rungs are deliberately **not** derived from it. Creation and destruction are
different axes now, so the rungs are absolute cage-destruction counts serialized on
`RibcageController` (`broodReleasePrisms` 150, `packReleasePrisms` 350, out of a
3,175-prism cage). Tune them against how fast a Rhino actually chews bone.

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
| `EndConditionOverridesSO` (+ window + asset) | `ribcagePrismTarget` live/build/getter, default 600 |
| `ElementalComebackSystem` | `ScoreDifferenceSource.PrismsRemaining` + `GameModes.Ribcage` default-source case |
| `ScoringMetric` / `ScoringMetrics.Read` | `PrismsRemaining = 6` → `stats.PrismsRemaining` (the live stock) |

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
   should report **2,721 prisms / ~1,069,380 volume** for the Ribcage cell. If it
   disagrees, the generator and `ribcage_budget.py` have drifted — fix both.
4. **Bars are two-hit — except the traps.** Ram a rib: first contact sheds the shield
   (octahedron disengages), second shatters it and the HUD sum increments. Then find a
   **danger** bar (distinct material): it shatters in ONE hit but full-stops you, debuffs
   all four elements for 4 s and resets boost.
5. **Rhino only.** Pick a different vessel in an earlier game, then launch Ribcage —
   you should spawn a Rhino anyway, with a `clamping selected vessel` line in the log.
6. **Spawn outside.** All players start on a ring ~576u out, facing the cage, with the
   whole cage visible ahead — nobody starts inside it.
7. **The penned brood + intruder frenzy.** The cage is visibly full (~56 creatures of
   four species) and they stay inside. While penned they must NOT eat anything outside —
   fly around the outside laying trail; it should be ignored. Then fly IN: the cell
   should jump to **Frenzy** on the DiagnosticsHUD and the whole pen should converge on
   your trail. Leave, and it should settle back to Calm once your mass is gone.
8. **Laying scores; smashing does not.** The HUD domain sum should rise as you lay trail
   and rise *not at all* from breaking bars. Have a rival ram your trail (or wait for the
   swarm) and watch your sum go back DOWN — that is the whole point of the live stock.
9. **Brood rung.** At **150 total cage prisms destroyed** (any domain) the pen opens and
   the swarm leaves wearing the **race leader's** colour to eat the *trailing* domains'
   standing mass. **The device should shake hard for ~1.2 s** (the alert feel).
10. **Pack rung.** At **350** destroyed the pack toast fires, the device shakes again, and
   sharks join; the cell reads Frenzy on the DiagnosticsHUD.
11. **Lead change flips the swarm.** Let a second domain take the lead — the *live*
   creatures should re-colour and switch which trails they eat.
12. **Win + scoreboard.** First domain to **300 standing prisms** ends the turn; winners
    show a time, losers "N To Go". Replay (scene reload) re-pens the brood and resets both
    axes.
13. **AI stays outside.** Watch an AI Rhino for a minute: it should orbit outside, cross
    the cage on transits, and only be inside briefly — during a crossing or a raid. If it
    settles inside, `AiStationStandoff` has been set ≤ 1.
14. **Regression — the grid change.** Play **Skim Race** (intensity 3) and **Astro
    League**: fauna should behave normally and should no longer park against the
    super-shielded track / edge lining.
15. **Collider telemetry** on device via DiagnosticsHUD / the Benchmark tool; if the
    cage is too heavy, lower `RibCount` (16) or raise `BarStep` (17) in
    `SpawnableRibcage.cs` and re-run both Python tools.

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
- **Danger bars are a first pass.** 112 of 3,175 (one in 19 rib prisms), evenly spread by
  a deterministic index walk. If they read as noise rather than as traps, cluster them
  instead (e.g. whole trap *segments* of a rib) — one constant,
  `SpawnableRibcage.DangerEveryNthRibPrism`.
- **Target 600 is a first guess.** Nobody has measured how fast a Rhino clears
  two-hit bars. It is one editor field; expect to tune it on the first playtest, and
  the release rungs follow it automatically.
- **`Cell.OpposingVolume` still counts shielded mass** as the fauna prey signal, so a
  shielded structure satisfies `FaunaFoodFloor` without being food. Ribcage sidesteps
  it (`FaunaFoodFloor 0` — the release tier is the real gate), but the honest fix is to
  net shielded volume out of that signal. Left alone deliberately: it is the population
  bound for every biome, so it deserves its own change and its own verification.
