# Nucleus Rush ("Brood Rush") — Technical Documentation

## Overview

Brood Rush is the nucleus-control domain minigame built directly on the cell
fundamentals — the first mode where the **ecosystem itself is the scoreboard**:

- **Node control is the nucleus.** A cell's controlling domain is decided ONLY by the
  per-domain ENVIRONMENT volume (trail + flora) laid **inside the nucleus**
  (`Cell.DominantDomain` reads the nucleus-interior tally when a nucleus control zone
  exists). Fly through the core and lay mass to claim it; out-lay the enemy to flip it.
- **The exterior is the feeding ground.** Everything outside the nucleus is voraciously
  edible — herbivores graze it **regardless of domain**, at every phase, and the
  targeting grids only ever hold exterior mass. Exterior trail never sways control;
  it only feeds the food web. Nucleus-interior mass is a sanctuary — fauna neither
  target nor consume it; only players (abilities + out-laying volume) contest the claim.
- **The fauna spawn cycle is the score clock.** Every `BaseFaunaSpawnTime` (30s) the
  cell births a fauna wave in the controlling color (the locked no-domain-asymmetry
  invariant). When that color is a **genuine nucleus claim**, the claiming team scores
  one **brood**. First domain to the wave target (default **3** — FrogletTools ▸ Game Modes ▸
  End Game Conditions) wins. With ticks at ~30/60/90/120/150s and two domains, a match
  runs **1.5–2.5 minutes** (3-0 fastest, 3-2 longest).

**Key architectural facts:**

- **Scene**: `Assets/_Scenes/Multiplayer Scenes/MinigameBroodRush.unity` (single
  unified scene, cloned from Astro League's — no separate singleplayer variant; solo
  play is a party of one + AI backfill)
- **GameMode enum**: `GameModes.BroodRush = 38` (display name "Brood Rush")
- **Controller**: `BroodRushController : MultiplayerDomainGamesController`
- **Scoring**: `BroodRushScoringRuleSO` (`metric = ScoringMetric.Goals`, points not
  golf) — a brood point lives on a representative player's `GoalsScored`
  (NetworkVariable), aggregated by domain like Astro League goals
- **Turn monitor**: `BroodRushWaveTurnMonitor` — resolves the wave target from
  `EndConditionOverridesSO` at StartMonitor (never a per-scene field), syncs it via
  NetworkVariable → `GameDataSO.GoalTargetCount`, ends the turn via
  `rule.IsObjectiveReached`
- **Domains**: exactly two (`SO_ArcadeGame.MinDomainsAllowed = MaxDomainsAllowed = 2`);
  players 2–4 (1v1 or 2v2, AI backfill)
- **Config**: `_SO_Assets/Games/ArcadeGameBroodRush.asset` (registered in
  `GameLists/OrganicRematchGames.asset`)

## The wave → point pipeline (zero bespoke ecology)

```
Cell (Nucleus Rush Cell Config)                    [every peer, client-local fauna]
  └─ RandomLifeSpawner.SpawnFaunaTypeLoop_Random   one species, ticks every 30s
      ├─ color = host.ControllingDomain            nucleus claimant (server-replicated
      │                                            to clients via CellNetworkSync)
      ├─ SeedFullWaveEveryTick → WaveSpawnCount    full fresh wave of PopulationSize,
      │                                            clamped by MaxLivePopulation
      └─ runtime.OnFaunaWaveSpawned.Raise(         SOAP: FaunaWaveData{cellId, domain,
             cellId, domain, spawned, nucleusControlled}   spawned, claim-is-real}
              │
              ▼
BroodRushController.HandleFaunaWave              [SERVER only scores]
  ├─ gate: turn live, results not sent, NucleusControlled, domain != Blue
  ├─ representative RoundStats on domain → GoalsScored++   (NetworkVariable → peers)
  └─ AnnounceWaveScored_ClientRpc → GameToastAPI   BroodWaveScored "<domain> brood hatched — n/target"
              │
              ▼
BroodRushWaveTurnMonitor.CheckForEndOfTurn       rule.IsObjectiveReached: any active
              │                                    domain's brood sum ≥ GoalTargetCount
              ▼
OnTurnEndedCustom (server) → AssignScores + Sort + CalculateDomainStats
  → SyncFinalScores_ClientRpc → WinnerName/WinnerDomain/Results on every peer
  → InvokeWinnerCalculated + InvokeMiniGameEnd → shared end-game + scoreboard
```

- **Wave clock alignment**: the cell's spawner starts when its bootstrap crystal
  registers (ready screen), so `BroodRushController.HandleTurnStarted` (raised on
  every peer at GO) calls `Cell.RestartSpawnerForMode()` — the 30s wave clock starts
  at the countdown's end. `InitialFaunaSpawnWaitTime = 30` puts wave 1 at ~30s.
- **Unclaimed nucleus scores nobody**: `FaunaWaveData.NucleusControlled` is false when
  no environment mass sits inside the nucleus (`Cell.TryGetNucleusClaim`) — the wave
  still spawns (ambience, fallback color) but no point is awarded.
- **One fauna species per profile**: the wave event fires once per species loop per
  tick, so a wave-scored profile authors exactly ONE `FaunaConfigurationSO`.
- **Server is the scoring authority**: fauna are client-local (no NetworkObject), so
  every peer raises its own local wave event; only the server's increments
  `GoalsScored`. Clients converge on the same wave color because `CellNetworkSync`
  pins the client Cell's `DominantDomain` to the server's replicated answer
  (`Cell.SetReplicatedDominantDomain`) — visuals may drift by a tick's timing, never
  by domain.

## Ecology configuration

| Asset | Values |
|---|---|
| `Nucleus Rush Cell Config` | Astro League biome visuals (membrane / **nucleus** / cytoplasm), `SenseRadiusOverride 1000`, low volume ladder (Restless 400 / Frenzy 1500 — trail prisms are low-volume) |
| `Nucleus Rush Spawn Profile` | **no flora** (control = player trails only; center-planted flora would randomize the nucleus claim), `InitialFaunaSpawnWaitTime 30`, `BaseFaunaSpawnTime 30`, **`SeedFullWaveEveryTick 1`**, `FaunaFoodFloor 0` (the score clock never stalls on a prey gate) |
| `Nucleus Rush Tadpole Fauna Config Data` | tadpole (Boid forager) ×6 per wave, `MaxLivePopulation 24`, `FeedsPerOffspring 0` (**reproduction off** — every spawn is a wave tick, keeping the score clock pure) |

**Collider-budget impact**: no new colliders and no new physics queries. Waves add ≤24
tadpole bodies (1 HealthPrism each) under the existing per-species cap; the nucleus
checks are O(1) squared-distance tests inside loops that already touch the prism, and
the targeting grids hold *fewer* blocks (interior mass excluded). Fauna senses still
ride `PrismSpatialIndex`.

## End condition

Authored ONLY via **FrogletTools ▸ Game Modes ▸ End Game Conditions**
(`Resources/EndConditionOverrides.asset` → `nucleusRushWaveTarget`, 0 = default 3).
`BroodRushWaveTurnMonitor.StartMonitor` resolves it server-side and publishes
`GameDataSO.GoalTargetCount` to every peer. See the `/EndGameConditions` skill.

## Strategy surface (why it's a race, not a stalemate)

- Claiming is cheap early (an empty nucleus flips on a single pass) but expensive late
  (you must out-lay the standing claim; vessel abilities that destroy enemy prisms work
  inside the nucleus — fauna don't).
- Exterior trail is bait: it feeds the *controller's* brood (any-domain voracious
  grazing) and pulls the swarm around the map, but never scores. Time in the nucleus
  is time not spent harvesting crystals from withered fauna (elemental powerups).
- The comeback system (`ElementalComebackSystem`, `ScoreDifferenceSource.Goals`) buffs
  the trailing team's elementals, sized to the brood deficit.

## Shared-Code Touchpoints (added for this mode)

| Change | File |
|---|---|
| `BroodRush = 38` | `_Scripts/Data/Enums/GameModes.cs` |
| Nucleus control zone + voracious exterior + `IsPreyForHerbivore` / `TryGetNucleusClaim` / `RestartSpawnerForMode` / replicated-dominant pin | `_Scripts/Controller/Environment/Cell.cs` |
| Client dominant-domain pin | `_Scripts/Controller/Environment/CellNetworkSync.cs` |
| Wave event raise + full-wave seeding | `_Scripts/Controller/Environment/RandomLifeSpawner.cs` |
| `SeedFullWaveEveryTick`, 30s default cadence | `_Scripts/Utility/DataContainers/SpawnProfileSO.cs` |
| `WaveSpawnCount` (tested) | `_Scripts/Utility/DataContainers/FaunaReproductionRules.cs` (+ EditMode tests) |
| `OnFaunaWaveSpawned` channel | `_Scripts/Utility/DataContainers/CellRuntimeDataSO.cs` |
| `FaunaWaveData` SOAP type (event + listener) | `_Scripts/ScriptableObjects/SOAP/ScriptableFaunaWave/` |
| `nucleusRushWaveTarget` (+build field, window rows) | `_Scripts/ScriptableObjects/EndConditionOverridesSO.cs`, `_Scripts/Editor/EndConditionOverridesWindow.cs` |
| Calm-phase exterior hunting | `_Scripts/Controller/Environment/FloraAndFauna/Fauna.cs`, `LightFauna.cs` |
| Forager nucleus sanctuary | `_Scripts/Controller/Environment/FloraAndFauna/Boid.cs` |

## Assets

| Asset | Path |
|---|---|
| Arcade game config | `_SO_Assets/Games/ArcadeGameBroodRush.asset` (in `GameLists/OrganicRematchGames.asset`) |
| Scoring rule | `_SO_Assets/Scoring Rules/BroodRushScoringRule.asset` |
| Cell config (biome) | `_SO_Assets/Cell Configs/Nucleus Rush Cell/Nucleus Rush Cell Config.asset` |
| Spawn profile | `_SO_Assets/Cell Configs/Nucleus Rush Cell/Nucleus Rush Spawn Profile.asset` |
| Fauna species | `_SO_Assets/Cell Configs/Nucleus Rush Cell/Nucleus Rush Tadpole Fauna Config Data.asset` |
| Wave SOAP event | `_SO_Assets/Cell Data/Event_OnFaunaWaveSpawned.asset` (wired on `Runtime Cell Data.asset`) |
| End conditions | `Assets/Resources/EndConditionOverrides.asset` (`nucleusRushWaveTarget`) |

## Known limitations / follow-ups

- No HUD objective provider yet (`MiniGameHUD.CreateObjectiveProviderForGameMode`
  returns null for this mode) — a nucleus-pointing arrow would help new players.
- The scene keeps the Astro League comeback profile asset; author a Brood Rush profile
  if the buff curve needs its own tuning.
- Fauna waves are client-local visuals: a client's wave may hatch a beat later than
  the server's scored tick (domain always matches via the replicated control pin).
