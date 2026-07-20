# Rampage — Technical Documentation

## Overview

Rampage is the **destructive analog of Crystal Capture ("Scurry")**: a multiplayer
party game where every domain races to be the first to DESTROY the prism target.
Simple destructive fun — fly hard, smash mass, watch the counter fall.

- **Only hostile mass scores.** The metric is `IRoundStats.HostilePrismsDestroyed`.
  "Hostile" means everything except your own team's **player-laid** mass: ALL
  environment mass scores regardless of color (flora and fauna carry non-roster
  owner names — `DefaultPlayer`/`FaunaPrefab` — so `StatsManager` classifies their
  destruction hostile), and opponents' trails score; your own and your teammates'
  trails never do (trails ARE rostered, so the domain check filters them).
  Shattering your own trail is worthless *by construction*, so there is no
  lay-and-smash farming loop — but every wild prism in the arena is fair game.
- **Destruction is the sanctioned mass sink.** The conserved-mass law says prisms are
  removed only by an *active* force — vessel abilities or fauna consumption. Rampage
  is that law played as a sport: every scoring act is a vessel ability consuming mass.
  No decay, no timers, no cullers anywhere in the mode.
- **The arena restocks itself.** The Rampage Cell is flora-rich (Blob-class profile,
  all three domains seeded per the no-domain-asymmetry invariant). As players carve
  the prismscape down, the cell drops below its phase thresholds and flora growth
  resumes — the food web and the demolition derby feed each other.

**Key architectural facts:**

- **Scene**: `Assets/_Scenes/Multiplayer Scenes/MinigameRampage.unity` (single unified
  scene, cloned from Brood Rush's skeleton — no separate singleplayer variant; solo
  play is a party of one + AI backfill)
- **GameMode enum**: `GameModes.Rampage = 2` — repurposed from the legacy
  single-player arcade entry (whose `MinigameRampage` scene never shipped; nothing
  playable depended on the old meaning)
- **Controller**: `RampageController : MultiplayerDomainGamesController` — structural
  clone of `MultiplayerCrystalCaptureController` (1 round / 1 turn, HasEndGame=false,
  server winner detection in `OnTurnEndedCustom`, snapshot `SyncFinalScores_ClientRpc`)
- **Scoring**: `RampageScoringRuleSO` (`metric = ScoringMetric.PrismsDestroyed`,
  points not golf) — `TargetCount => GameDataSO.PrismTargetCount`; per-player
  `Score = HostilePrismsDestroyed`; TEAM-major results like Scurry
- **Turn monitor**: `RampagePrismTurnMonitor` — resolves the prism target from
  `EndConditionOverridesSO.GetRampagePrismTarget()` at StartMonitor (default **100**,
  Tools ▸ Cosmic Shore ▸ End Game Conditions — never a per-scene field), syncs it via
  NetworkVariable → `GameDataSO.PrismTargetCount`, ends the turn via
  `rule.IsObjectiveReached`
- **Domains**: free-for-all like Scurry (`MinDomainsAllowed`/`MaxDomainsAllowed`
  defaults 1/3); players 1–4 with AI backfill
- **Vessels**: Sparrow (guns + missiles), Rhino (ram), Dolphin
- **Config**: `_SO_Assets/Games/ArcadeGameRampage.asset` (registered in
  `GameLists/OrganicRematchGames.asset` + the pre-existing arcade lists)

## The destruction → score pipeline (zero bespoke tracking)

The stat was already fully plumbed platform-wide; Rampage adds only the metric
mapping and the race framing:

```
Vessel ability destroys a prism (gun / missile / AOE / ram)
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
  ├─ rule.ResolveWinner / AssignScores (Score = HostilePrismsDestroyed)
  └─ SyncFinalScores_ClientRpc → WinnerName/WinnerDomain, Results, MiniGameEnd
```

## Ecology configuration

`_SO_Assets/Cell Configs/Rampage Cell/`:

- **Rampage Cell Config** — Blob-class membrane/cytoplasm/nucleus, Blob phase
  thresholds (Restless 700/500, Frenzy 3600/3000; volume bands 11200/8000 and
  57600/48000). Standard collider-LOD by phase; **no new colliders or physics
  queries** — scoring rides the existing StatsManager SOAP channel, and a match
  *removes* ~100+ prisms, so Rampage sits below the Blob collider envelope.
- **Rampage Spawn Profile** — flora-rich: the four Blob flora species (Mass/Time/
  Space Gyroids + SchwarzP), plus tadpole + shark fauna (grazer + predator food
  web; both drop elemental crystals on death — skimmable powerups mid-rampage).
  Flora stock is gated by the Frenzy phase threshold (`FrenzyEnterVolume 57600` —
  planting pauses at Frenzy, resumes below `FrenzyExitVolume`; the profile's
  `FloraSpawnVolumeCeiling` field is legacy-inert). Species configs are referenced
  from the Blob folder (read-only species definitions); fork per-cell copies only
  when Rampage needs its own tuning deltas.

## End condition

Authored ONLY through **Tools ▸ Cosmic Shore ▸ End Game Conditions**
(`EndConditionOverridesSO.rampagePrismTarget`, 0 = default 100). Applies wherever
the mode runs. Live/Build split + build auto-restore work like every other mode.

## Comeback

`ArcadeGameRampage.asset` sets `ComebackRatePerScoreDeficit: 0.2` (not the default
1.0): prism deficits run ~5× larger than Scurry's crystal deficits (target 100 vs
20), so 0.2 keeps the buff curve proportionate — a ~50-prism team deficit maxes the
comeback ceiling the way a ~10-crystal deficit does in Scurry. The scene-authored
`ElementalComebackSystem` uses `ScoreDifferenceSource.PrismsDestroyed` (Score only
lands at game end in this mode, so the Score source would be inert live).

## Strategy surface (why it's a race, not a grind)

- **Target choice is the skill.** Dense flora clusters score fastest (any color —
  environment mass is bounty for everyone); opposing trails score too and
  simultaneously deny the opponent skim/boost infrastructure. Only your own
  team's trails are dead weight.
- **Destruction feeds the enemy comeback.** Pull far ahead and the trailing domains
  get all-element buffs (stronger AOE, faster) — rubber-banding without scripting.
- **Fauna are jackpots with teeth.** Opposing fauna are multi-prism bodies worth
  several points that fight back; their crystal drop pays an elemental buff on top.
- **Regrowth keeps late game honest.** A picked-clean arena regrows flora below the
  phase thresholds, so the endgame never starves of targets.

## Shared-Code Touchpoints (added for this mode)

| Site | Change |
|---|---|
| `ScoringMetric` | `PrismsDestroyed = 5` |
| `ScoringMetrics.Read` | `PrismsDestroyed => stats.HostilePrismsDestroyed` |
| `GameDataSO` | `PrismTargetCount` (+ both runtime resets) |
| `EndConditionOverridesSO` (+ window + asset) | `rampagePrismTarget` live/build/getter, default 100 |
| `ElementalComebackSystem` | `ScoreDifferenceSource.PrismsDestroyed` + `GameModes.Rampage` default-source case |
| `GameModes` | doc comment on the repurposed `Rampage = 2` |

## Assets

| Asset | Notes |
|---|---|
| `ArcadeGameRampage.asset` | `Mode 2`, `IsMultiplayer 1`, players 1–4, Sparrow/Rhino/Dolphin, `SceneName MinigameRampage`, comeback rate 0.2 |
| `RampageScoringRule.asset` | `metric 5 (PrismsDestroyed)`, `golfRules 0` |
| `MinigameRampage.unity` | cloned from `MinigameNucleusRush.unity`; in `EditorBuildSettings` |
| `Rampage Cell Config.asset` / `Rampage Spawn Profile.asset` | flora-rich Blob-class arena |
| `GameLists/OrganicRematchGames.asset` | Rampage added (party-games list) |

## Known limitations / follow-ups

- **Legacy training asset**: `SO_TrainingGame_Rampage.asset` was **de-listed** from
  `TrainingGames.asset` (the daily-challenge pool) and Rhino's hangar training
  slots (repointed to WildlifeBlitz, matching Sparrow) — `Arcade.LaunchTrainingGame`
  launches scenes unconfigured (no GameMode/player-count/backfill), which is wrong
  for a multiplayer mode. The training SO itself remains on disk with pre-rework
  score tiers (10000+); re-tune to prism counts if that surface returns.
- **Menu unlock**: `Rampage(2)` was added to `ProgressionConfig.asset`
  `alwaysUnlockedModes` (previously only Tournament 36) so the card is clickable
  on fresh accounts. Astro League(37)/Brood Rush(38) are NOT in that list — they
  rely on account progression state; align them deliberately if they should be
  always-open too.
- **No objective-arrow provider**: like Brood Rush, `MiniGameHUD.
  CreateObjectiveProviderForGameMode` has no Rampage case (there is no single
  objective point to point at — the arena is the objective).
- **No UGS stats reporter yet**: Scurry has `CrystalCaptureStatsReporter`; a
  `RampageStatsReporter` (most-prisms-smashed leaderboard) is a clean follow-up.
- **In-editor verification pending** (authored headless): see the verification
  checklist in the PR/commit — scene open, script refs resolve, solo launch with AI
  backfill, destruction increments the HUD sum, target ends the game, scoreboard +
  replay.
