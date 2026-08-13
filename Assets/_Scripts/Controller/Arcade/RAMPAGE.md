# Rampage — Technical Documentation

## Overview

Rampage is the **Dolphin-only demolition race**, and the destructive analog of Crystal
Capture ("Scurry"): every domain races to be the first to DESTROY **2,000 hostile
prisms**. A belt of cacti and other breakable flora rings the membrane, the arena core
is left open, and a **single contested crystal** roams it.

**The loop is the Dolphin's own economy, made into a sport.** Nothing here is scripted —
the mode simply arranges the arena so the vessel's existing spine becomes the game:

| the vessel already does this | Rampage makes it the game |
|---|---|
| Energy is banked **only by skimming** (+0.006667/skim, 150 skims fills it) | a cactus forest is the charging ground — and every prism you clip on the way through scores |
| Touching a **crystal** spends the whole meter as one conic jaw blast | the arena carries exactly **one** crystal, so cashing out is contested |
| Energy owns the blast's **GAPE** (4.76° empty → 23.43° full) | arriving charged is worth ~5× the swath of arriving empty |
| The cone reaches **2,400 units** down-range | from anywhere in the core, a blast aimed outward sweeps the whole belt |
| Ramming a prism **halves** the meter | flying *through* the thicket instead of *into* it is the skill |

So a round reads: **graze the belt to charge → break for the crystal → aim at the
thickest part of the forest → fire.** See `DOLPHIN_ENERGY_ECONOMY.md` §1 for the
economy itself; this file only arranges around it.

- **Only hostile mass scores.** The metric is `IRoundStats.HostilePrismsDestroyed`.
  "Hostile" means everything except your own team's **player-laid** mass: ALL
  environment mass scores regardless of colour (flora and fauna carry non-roster
  owner names — `DefaultPlayer`/`FaunaPrefab`/`flora` — so `StatsManager` classifies
  their destruction hostile), and opponents' trails score; your own and your
  teammates' trails never do (trails ARE rostered, so the domain check filters them).
  Shattering your own trail is worthless *by construction*, so there is no
  lay-and-smash farming loop — but every wild prism in the arena is fair game.
- **Destruction is the sanctioned mass sink.** The conserved-mass law says prisms are
  removed only by an *active* force — vessel abilities or fauna consumption. Rampage
  is that law played as a sport: every scoring act is a vessel ability consuming mass.
  No decay, no timers, no cullers anywhere in the mode.
- **The arena restocks itself.** As players carve the belt down, the cell drops below
  its phase thresholds and flora planting + growth resume — the food web and the
  demolition derby feed each other.

**Key architectural facts:**

- **Scene**: `Assets/_Scenes/Multiplayer Scenes/MinigameRampage.unity` (single unified
  scene — no separate singleplayer variant; solo play is a party of one + AI backfill)
- **GameMode enum**: `GameModes.Rampage = 2` — repurposed from the legacy
  single-player arcade entry (whose `MinigameRampage` scene never shipped; nothing
  playable depended on the old meaning)
- **Controller**: `RampageController : MultiplayerDomainGamesController` — structural
  clone of `MultiplayerCrystalCaptureController` (1 round / 1 turn, HasEndGame=false,
  server winner detection in `OnTurnEndedCustom`, snapshot `SyncFinalScores_ClientRpc`)
- **Scoring**: `RampageScoringRuleSO` (`metric = ScoringMetric.PrismsDestroyed`,
  golf-timed like HexRace/Scurry) — `TargetCount => GameDataSO.PrismTargetCount`;
  winning-domain players `Score = finish time` (displayed mm:ss:cs), losers the
  `GolfScoreSentinels` remaining-prisms sentinel (displayed "N Prisms Left", with
  individual prisms smashed on the secondary line); TEAM-major by construction
- **Turn monitor**: `RampagePrismTurnMonitor` — resolves the prism target from
  `EndConditionOverridesSO.GetRampagePrismTarget()` at StartMonitor (default **2000**,
  FrogletTools ▸ Game Modes ▸ End Game Conditions — never a per-scene field), syncs it via
  NetworkVariable → `GameDataSO.PrismTargetCount`, ends the turn via
  `rule.IsObjectiveReached`
- **Domains**: free-for-all like Scurry (`MinDomainsAllowed`/`MaxDomainsAllowed`
  defaults 1/3); players 1–4 with AI backfill
- **Vessels**: **Dolphin only** — see "Why Dolphin-only" below
- **Objective arrow**: `RampageObjectiveProvider` — points at the contested crystal
- **Config**: `_SO_Assets/Games/ArcadeGameRampage.asset` (registered in
  `GameLists/OrganicRematchGames.asset` + the pre-existing arcade lists)

## Why Dolphin-only, and how it is enforced

`ArcadeGameRampage.Vessels` holds ONE entry (`SO_Class_Dolphin`). The restriction is
**not** implemented in this mode — it is the platform's two-place clamp, exactly as in
Ribcage / Dog Fight / Wildlife Liberation:

1. `GameDataSO.SyncFromArcadeGame` clamps `selectedVesselClass` into the game's allowed
   set. This covers the machine that pressed Start, on every route (modal, rematch,
   Tournament chain).
2. `ServerPlayerVesselInitializer.ResolveSpawnVesselType` re-clamps **server-side at
   spawn**. This is the one that matters in multiplayer: `Player.NetDefaultVesselType`
   is an OWNER-write NetworkVariable each client sets from its OWN local config, and
   `SyncFromArcadeGame` never runs on a client, so a joining client walks in still
   wearing the hull it last flew.

The scene's four AI backfill templates are `vesselClass: 2` (Dolphin) so the AI flies
the same ship — an AI class comes from `aiInitializeDatas`, not from the clamp.

**The mode is Dolphin-only because a mixed roster would break the premise, not to be
exclusive.** The single crystal is only a contested object if it is the only way to
discharge a blast. A Rhino or Sparrow in the arena would ignore it entirely and shoot
the forest down on its own clock, so the crystal would stop being worth fighting over
for anyone.

**The Dolphin can still make its own crystals, and that is deliberate.** Crystal
Seeding (its Charge ability) plants a TEAM crystal only the pilot's domain can collect,
on a **30 s** cooldown (→ ~15 s at Charge 10; two charges at Charge 5). So the arena
crystal is not the *only* trigger — it is the **free, immediate, uncontested-by-cooldown**
one, which is what makes taking it a tempo play rather than a necessity. Do not nerf the
seeding ability for this mode; the tension between "my crystal on a timer" and "the
crystal, right now, if I can get there first" is the interesting half.

## The destruction → score pipeline (zero bespoke tracking)

The stat was already fully plumbed platform-wide; Rampage adds only the metric
mapping and the race framing:

```
Dolphin blast / ram destroys a prism
  └─ Prism.Damage / Prism.Explode / Prism.Implode
      └─ SetupDestruction → onTrailBlockDestroyed.Raise(PrismStats{OwnName, Volume, AttackerName})
              │  (SOAP channel — StatsManager.prefab listener)
              ▼
StatsManager.PrismDestroyed                        [server-only via _allowRecord]
  ├─ attacker.BlocksDestroyed++ / TotalVolumeDestroyed += v
  ├─ victim rostered + same domain? → Friendly… stats (NEVER scores: own/teammate trails)
  └─ else (other domain OR environment) → HostilePrismsDestroyed++  (NetworkVariable → peers)
              │
              ▼
ScoringMetrics.Read(stats, PrismsDestroyed) → SumByDomain
  ├─ MultiplayerDomainGamesController.SyncDomainSumsRoutine → HUD domain panels
  ├─ RampagePrismTurnMonitor.CheckForEndOfTurn → rule.IsObjectiveReached  [server]
  └─ ElementalComebackSystem (source PrismsDestroyed) → trailing-team elemental buff
              │  turn end
              ▼
RampageController.OnTurnEndedCustom                [server]
  ├─ rule.ResolveWinner / AssignScores
  │    winners: Score = finish time (Time.time - TurnStartTime)
  │    losers:  Score = DnfThreshold + team prisms remaining
  └─ SyncFinalScores_ClientRpc → WinnerName/WinnerDomain, Results, MiniGameEnd
```

**The jaw blast credits its pilot.** `VesselExplosionByCrystalEffectSO` hands the firing
vessel to `AOEConicExplosion`, and `PrismSpatialIndex.ResolveDamage` reads
`vessel.VesselStatus.Player.Name` off it, so every prism a cone shatters lands on that
pilot's `HostilePrismsDestroyed`. (A blast constructed with a null vessel is *anonymous*
and credits `🔥GuyFawkes🔥` instead — that is the failure mode to check first if a mode
ever reports blasts scoring nothing.)

## The arena — a forest belt around the membrane, an open core

`_SO_Assets/Cell Configs/Rampage Cell/`. Membrane radius **1200** (`CapsuleMembrane`),
nucleus world radius **200** (`Nucleus.prefab` at scale 400).

### The belt

Five species, each planted on its own shell as a fraction of the membrane radius, so
the belt has DEPTH instead of reading as one soap bubble of plants:

| species | script | shell | world radius | plants seeded | prisms/plant | leaf prism vol |
|---|---|---|---|---|---|---|
| **Cacti** (hero) | `BranchingFlora` | 0.90 | 1080 | 50 | 55 | 5×5×3 = **75** |
| Rosette | `PhyllotacticFlora` | 0.94 | 1128 | 20 | 90 | ~15 |
| Spire | `PhyllotacticFlora` | 0.86 | 1032 | 24 | 80 | ~10 |
| Pine | `BranchingFlora` | 0.82 | 984 | 28 | 55 | 4×4×1 = 16 |
| Coral | `PhyllotacticFlora` | 0.76 | 912 | 14 | 110 | ~10.6 |

Seeded total ≈ **9,550 prisms** at full growth, and planting continues past the seed
batch until the cell tops out (below). Every config runs `SpreadElements` over the
species' four canonical element assets and `Levels {1..5, falloff 2}`, and
`CellLifeSpawnerBase.SpawnFlora` rolls the DOMAIN uniformly across all three — so the
belt is a genuine mixture of colours, elements and sizes, and there is hostile mass in
every direction for every pilot (no-domain-asymmetry invariant).

Each config's own `Variant` block is **disabled on purpose**: the element palette owns
every plant's identity (leaf prism shape, growth tempo, shield cadence), and the cell
asserts only its two layout facts — the shell and the per-plant budget — through
`PlantRadiusCellFractionOverride` / `MaxTotalSpawnedObjectsOverride`, which are applied
after the roll and therefore survive it. Authoring the shell in the `Variant` block
instead would be silently discarded by `SpreadElements` and the whole belt would collapse
onto each species' authored 0.5–0.6 fraction, i.e. into the middle of the arena.

Cacti are the hero for a reason: `BranchingFlora` at 85–95° branch angles gives the
right-angled arms, and its leaf prism is 5×5×3 = **75 volume, 4.7× nominal**, so a
single hit is a chunky, legible piece of destruction — while still counting exactly 1
toward the 2,000.

### The core is deliberately empty

No species plants below 0.76 R. The core is the crystal's contested ground and the
blast's firing line: the cone is 2,400 long, so a pilot who takes the crystal anywhere
in the core and turns outward sweeps a full radius of belt.

### The phase ladder rides the belt's real volume

**Volume is the spine, and this belt is NOT made of nominal prisms** — the cacti alone
are 4.7× nominal each, so the inherited `count × 16` derivation was wrong by ~3× and
would have pinned the cell at Frenzy (planting frozen) almost immediately, leaving a
sparse arena that never regrew. Authored explicitly:

```
RestlessEnterVolume  34000   RestlessExitVolume  24000
FrenzyEnterVolume   480000   FrenzyExitVolume   370000
RestlessEnter 700  RestlessExit 500  FrenzyEnter 10000  FrenzyExit 8000   (count backstop)
```

- **Frenzy volume ≈ the full-grown belt** (est. ~471,000: Σ plants × prisms × leaf
  volume × 1.605, the level-spread's expected volume multiplier at falloff 2). Planting
  and growth freeze there — a growth gate, never a culler, so mass stays conserved.
- **Frenzy exit at ~77%** of enter: regrowth resumes once roughly a quarter of the belt
  is gone, which at these numbers is a few hundred prisms of destruction — fast enough
  that the endgame never starves of targets.
- **Restless at ~7% of Frenzy**, the same proportion Blob uses, so fauna start hunting
  early rather than waiting for a full arena.
- **The count fields stay the perf backstop**: 10,000 prisms forces Frenzy regardless of
  volume. The seeded belt lands just under it on purpose, so player trails are what push
  the arena into its cap.

**These volume numbers are an estimate and must be checked in-editor** — phyllotactic
prisms are shaped per role (stem spans its segment, leaf spans its reach), so their
volume cannot be read off a single authored field. Watch `Cell.LiveVolume` on the
DiagnosticsHUD through the first minute: if the belt tops out well under
`FrenzyEnterVolume` the arena will keep planting past its intended density (watch the
prism count and the collider budget), and if it hits Frenzy before the belt looks full,
raise `FrenzyEnterVolume`/`FrenzyExitVolume` together, keeping the ~77% ratio.

**Collider-budget note:** 10,000 prisms is ~2.8× the Blob envelope (3,600) and well
above the masterplan's ≤1500 active-collider target — deliberate design headroom for a
demolition arena, unchanged from the previous Rampage. Mitigations: collider-LOD by
phase, Burst density-grid queries (no new physics queries — scoring rides the
StatsManager SOAP channel and AI targeting rides the density grid), and the mode's whole
verb actively removes mass. The 136 seeded flora instantiate one-per-frame
(`FloraSpawnIntervalSeconds: 0`), so the opening batch costs ~2.3 s of spread-out spawn
rather than one hitch.

### Fauna

Unchanged: tadpole (grazer) + shark (predator), referenced from the Blob folder. They
are the food web, and both drop elemental crystals on death — skimmable powerups
mid-rampage, and one more thing worth shooting.

## The contested crystal

`NetworkCrystalManager` on the scene's Game object:

- `crystalCountMode: FixedCount`, `fixedCrystalCount: 1` — **one** crystal, always.
- `spawnCrystalWithPlayerDomain: 0` — it is neutral (`Domains.Blue`), so
  `Crystal.CanBeCollected` lets **any** pilot take it. That is the contest.
- `anchorlessSpawnRadius: 900` — the roam volume. With no authored anchors the manager
  draws `Random.insideUnitSphere * radius` about the cell centre, so the crystal ranges
  across the open core and out to the belt's inner fringe, and each collection moves it.
  Left at 0 this falls back to the **nucleus** radius (200) and the crystal would rattle
  around a small ball at the exact centre — always in the same place, never a chase.

Because the meter is spent on contact regardless of how full it was, taking the crystal
empty is a legitimate **denial** play: it costs you nothing and teleports the prize away
from a rival who is fully charged. That falls out of the existing rules; nothing
implements it.

### Spawn ring

`arrangeSpawnPointsAroundCell: 1`, `spawnFormation: Symmetric`,
`spawnDistanceOutsideNucleus: 500` → pilots start on a sphere of radius **700**
(`ExpectedNucleusWorldRadius` 200 + 500), symmetric by count, all facing the cell.
Previously the four authored transforms put everyone inside a ±50 box at the arena
centre — four Dolphins nose to nose, 1,000 units from anything worth hitting. The
authored transforms remain as the fallback the platform uses if the cell can't be
resolved.

## The AI

`RampageController.ArmDolphinHunters()` (server, at countdown end — mirroring Astro
League's `ArmStrikers`) gives each AI pilot the same two-phase loop the mode asks of a
human, via `AIPilot.SetExternalTargetProvider`:

| phase | condition | target |
|---|---|---|
| **charging** | Energy < `aiCrystalRunEnergy` (0.6) | `Cell.GetExplosionTarget(aiDomain)` — the densest region of mass hostile to its domain, the same density-grid query aggression-1 fauna use. Sampled every `aiRetargetSeconds` (1.5 s); the Burst job never runs per-frame. |
| **cashing** | Energy ≥ threshold | the live crystal, from the in-memory `Crystal.Active` registry (skipping one already exploding) |

Both single-phase alternatives were tried and are wrong:

- **Mass hunting only** (what shipped when this mode was Rhino-flavoured) — the AI banks
  a full meter and never fires it, because nothing but a crystal discharges the blast.
- **The `AIPilot` default** (crystal seeking, no external provider) — the AI sprints to
  every crystal at zero charge, dumps an empty meter on arrival, and never lights up the
  forest.

The energy read is a float off the pilot's own `ResourceSystem` (slot 0 = Energy, slot
1 = Boost), so it stays correct through every drain the economy applies. Ramming still
scores on the way through, so a charging AI is never idle.

## End condition

Authored ONLY through **FrogletTools ▸ Game Modes ▸ End Game Conditions**
(`EndConditionOverridesSO.rampagePrismTarget`, 0 = default 2000). Applies wherever
the mode runs. Live/Build split + build auto-restore work like every other mode.

## Comeback

`ArcadeGameRampage.asset` sets `ComebackRatePerScoreDeficit: 0.01` (not the default
1.0): prism deficits run ~100× larger than Scurry's crystal deficits (target 2000 vs
20), so 0.01 keeps the buff curve proportionate — a ~1000-prism team deficit maxes
the comeback ceiling the way a ~10-crystal deficit does in Scurry. The scene-authored
`ElementalComebackSystem` uses `ScoreDifferenceSource.PrismsDestroyed` (Score only
lands at game end in this mode, so the Score source would be inert live).

It lands especially well on this vessel: Space widens the cone's reach, Charge shortens
the seeding cooldown, and Mass fattens the drift trail — so a trailing Dolphin visibly
hits harder rather than just moving faster.

## Strategy surface (why it's a race, not a grind)

- **Arrive charged.** Empty is a 4.76° gape, full is 23.43° — the same trip, ~5× the
  swath. The whole skill expression is judging whether you have time for one more pass
  through the thicket before someone else takes the crystal.
- **Fly through, not into.** A skim banks energy; a ram halves it. The belt rewards
  threading gaps at speed and punishes bulldozing — and a ram still scores, so the
  greedy line is genuinely tempting.
- **Aim before you touch.** The blast fires along the hull's gape axis at the moment of
  contact, so which way you are pointing when you take the crystal decides whether the
  cone eats 400 units of empty core or a full radius of forest.
- **Target choice.** Dense flora clusters score fastest (any colour — environment mass
  is bounty for everyone); opposing trails score too and simultaneously deny the
  opponent skim/boost infrastructure. Only your own team's trails are dead weight.
- **Denial is real.** Taking the crystal at zero charge costs you nothing and moves it.
- **Destruction feeds the enemy comeback.** Pull far ahead and the trailing domains
  get all-element buffs — rubber-banding without scripting.
- **Fauna are jackpots with teeth.** Opposing fauna are multi-prism bodies worth
  several points that fight back; their crystal drop pays an elemental buff on top —
  and, being a crystal, it *also* discharges your blast, so a shark kill can hand you a
  free trigger at the wrong moment.
- **Regrowth keeps late game honest.** A picked-clean belt regrows below the phase
  thresholds, so the endgame never starves of targets.

## Shared-Code Touchpoints

Added when the mode first shipped:

| Site | Change |
|---|---|
| `ScoringMetric` | `PrismsDestroyed = 5` |
| `ScoringMetrics.Read` | `PrismsDestroyed => stats.HostilePrismsDestroyed` |
| `GameDataSO` | `PrismTargetCount` (+ both runtime resets) |
| `EndConditionOverridesSO` (+ window + asset) | `rampagePrismTarget` live/build/getter, default 2000 |
| `ElementalComebackSystem` | `ScoreDifferenceSource.PrismsDestroyed` + `GameModes.Rampage` default-source case |
| `GameModes` | doc comment on the repurposed `Rampage = 2` |

Added by the Dolphin rework. The three ecology items are **platform fixes, not mode
special-cases** — full record and rulings in `Docs/ECOSYSTEM.md §27`:

| Site | Change |
|---|---|
| `Flora.ResolvePlantCenter()` | New shared helper — the planting shell is measured from the **cell centre**, not the crystal. All three `Plant` implementations call it. (`§27.1`) |
| `BranchingFlora.Initialize` | Resolve `CrystalTransform` once and fall back to the growth axis — it returns null in a crystal-less cell and `.position` on it threw. |
| `BranchingFlora` / `PhyllotacticFlora` `.ApplyVariantTuning` | Honour `FloraVariantTuning.MaxTotalSpawnedObjects`, which only `AssembledFlora` read. **Changes existing cells** — 45 authored assets were writing into an inert field. (`§27.2`) |
| `FloraConfigurationSO.PlantRadiusCellFractionOverride` / `MaxTotalSpawnedObjectsOverride` (+ `TryBuildCellOverrideTuning`, called from `CellLifeSpawnerBase.SpawnFlora`) | Cell-level overrides applied AFTER the variant roll, so a cell can use the canonical per-element assets and still choose its own planting shell and plant size. Both default −1 = off. (`§27.3`) |
| `MiniGameHUD.CreateObjectiveProviderForGameMode` | `GameModes.Rampage` → `RampageObjectiveProvider`. |

The short version of why all three were needed at once: the belt is authored as
"canonical element identity, **this cell's** layout". The planting anchor decided *where
the shell is measured from*, the budget fix decided *whether a per-plant cap works at all
on branching/phyllotactic species*, and the overrides decided *whether a cell's layout
survives `SpreadElements`*. Any one of them missing puts the forest in the middle of the
arena, or lets fifty cacti grow to 5,000 prisms each.

The element/cell split worth carrying forward: **the ELEMENT owns identity** (leaf prism
shape, growth tempo, shield cadence, per-element density) and **the CELL owns layout**
(where a species plants, how big one plant may get in this arena). They shared one
`Variant` block because they were authored together, not because they are the same kind
of fact.

## Assets

| Asset | Notes |
|---|---|
| `ArcadeGameRampage.asset` | `Mode 2`, `IsMultiplayer 1`, players 1–4, **Dolphin only**, `SceneName MinigameRampage`, comeback rate 0.01 |
| `RampageScoringRule.asset` | `metric 5 (PrismsDestroyed)`, `golfRules 1` (finish-time scoring) |
| `MinigameRampage.unity` | Dolphin AI templates, cell spawn ring, crystal roam radius 900; in `EditorBuildSettings` |
| `Rampage Cell Config.asset` | belt-tuned `PhaseThresholds` (volume authored, not derived) |
| `Rampage Spawn Profile.asset` | the five belt species + tadpole/shark fauna |
| `Rampage {Cacti,Rosette,Spire,Pine,Coral} Flora Config Data.asset` | per-shell planting configs (new) |
| `GameLists/OrganicRematchGames.asset` | Rampage listed (party-games list) |

## In-editor verification checklist

Authored headless; every item below needs a play-mode pass.

1. **Open `MinigameRampage.unity`** — no missing script refs on the Game or Cell objects;
   `RampageController.arenaCell` still resolves.
2. **Solo launch with AI backfill** (1 human + 3 AI). Confirm every hull is a **Dolphin**,
   including the AI, and that pilots start spread on a ~700-radius sphere facing the cell
   rather than clustered at the centre.
3. **The belt** — after ~60 s, flora form a ring at roughly 900–1130 units in all
   directions, mixed colours and sizes, with the core clear. Nothing planted outside the
   membrane.
4. **The ladder** — watch `Cell.LiveVolume` and the live prism count on the
   DiagnosticsHUD as the belt fills. It should approach ~470k volume / ~9.5k prisms and
   stop growing at Frenzy, not top out early. Retune `FrenzyEnterVolume` /
   `FrenzyExitVolume` together (keeping ~77%) if it lands far off.
5. **The economy** — skim the belt and watch the HUD jaw gauge / hull jaws open; take the
   crystal and confirm the cone fires at the gape the meter had earned, and that the
   crystal respawns somewhere else in the roam volume.
6. **Scoring** — prisms destroyed by the blast increment the local pilot's domain panel
   (i.e. the blast is credited, not anonymous). Own-trail prisms score nothing.
7. **Objective arrow** — points at the crystal and re-acquires after each collection.
8. **The AI** — an AI grazes the belt, then breaks for the crystal once charged, and fires.
9. **End + replay** — reaching the target ends the turn, the scoreboard ranks by finish
   time with "N Prisms Left" for the losing domains, and replay reloads the scene clean.
10. **Regression: other cells' flora density.** Honouring `MaxTotalSpawnedObjects` on
    branching/phyllotactic flora switches on 45 previously-inert authored budgets. Take a
    pass through **Hesperides** (the densest flora cell) and Wildlife Blitz cells 1–3 and
    confirm `Cell.LiveVolume` still sits inside its thresholds. Expected effect is ≈ −6%
    prisms per plant on average with restored per-element variety — see
    `Docs/ECOSYSTEM.md §27.2`.

## Known limitations / follow-ups

- **Legacy training asset**: `SO_TrainingGame_Rampage.asset` was **de-listed** from
  `TrainingGames.asset` (the daily-challenge pool) and Rhino's hangar training
  slots (repointed to WildlifeBlitz, matching Sparrow) — `Arcade.LaunchTrainingGame`
  launches scenes unconfigured (no GameMode/player-count/backfill), which is wrong
  for a multiplayer mode. The training SO itself remains on disk with pre-rework
  score tiers (10000+); re-tune to prism counts if that surface returns.
- **Menu unlock**: `Rampage(2)` is in `ProgressionConfig.asset` `alwaysUnlockedModes`
  so the card is clickable on fresh accounts.
- **No UGS stats reporter yet**: Scurry has `CrystalCaptureStatsReporter`; a
  `RampageStatsReporter` (most-prisms-smashed leaderboard) is a clean follow-up.
- **No Rampage `GameToastConfigSO`** — no mode-specific toast copy for "crystal taken",
  "belt regrowing", or milestone rungs. Ribcage's progress-milestone pattern would port
  cleanly if the race wants more mid-match texture.
- **The belt is a shell, not a volume.** Each species plants on one radius with random
  direction, so the belt's depth comes from stacking five species' shells. A per-species
  planting BAND (min/max fraction) would give genuinely volumetric thickets and is the
  natural next extension of `Flora.ResolvePlantRadius` — deliberately not added here,
  since staggered shells reach the same read with no new authoring surface.
