# Astro League Game Mode — Technical Documentation

## Overview

Astro League is hypersea soccer — the spirit of Rocket League translated to Cosmic
Shore, rebuilt on the multiplayer domain-games stack. Two domains (Jade defends -Z,
Ruby defends +Z) fight to slam a glowing billiard-physics payload through the
opposing goal portal inside a wireframe arena suspended in the HyperSea. 1-6 players
(2v2/3v3 with AI backfill) through the same single unified Netcode scene as HexRace /
Joust / Crystal Capture — solo play is just a party of one plus AI backfill.

**Key architectural facts:**

- **Scene**: `Assets/_Scenes/Multiplayer Scenes/MinigameAstroLeague.unity` (single
  unified scene — no separate singleplayer variant)
- **GameMode enum**: `GameModes.AstroLeague = 36`
- **Controller**: `AstroLeagueController : MultiplayerDomainGamesController`
- **Scoring**: `AstroLeagueScoringRuleSO` (`metric = ScoringMetric.Goals`, points not
  golf), assigned to `GameDataSO.ScoringRule` in `OnNetworkSpawn`
- **Config**: every gameplay number lives in `AstroLeagueSettingsSO`
  (`Assets/_SO_Assets/Games/AstroLeagueSettings.asset`) — match rules, kickoff
  pacing, billiard physics, replication smoothing, juice, AI tuning, arena palette
- **Domains**: exactly two. `SO_ArcadeGame.MinDomainsAllowed = MaxDomainsAllowed = 2`
  pins the configure modal's DC stepper, so the standard pipeline (DomainAssigner →
  `ServerPlayerVesselInitializerWithAI` balancing) always produces Jade vs Ruby
- **Featured vessel**: Squirrel (drift class). `SO_ArcadeGame.Vessels` = Squirrel only

## Class Inventory (`_Scripts/Controller/Arcade/AstroLeague/`)

| Class | Role |
|---|---|
| `AstroLeagueController` | Match director (server-authoritative): kickoffs, goal attribution, celebrations, golden-goal overtime, winner banner, AI striker arming, final-score sync (HexRace/Joust/CC `SyncFinalScores_ClientRpc` pattern) |
| `AstroLeagueBall` | Server-simulated billiard payload (`NetworkBehaviour`). Server owns the rigidbody; clients dead-reckon from replicated position+velocity NetworkVariables. Strike velocity comes from server-side per-vessel transform sampling (vessels are transform-driven, so rigidbody velocity and remote `VesselStatus.Speed` are useless). Impact juice replicates via ClientRpc |
| `AstroLeagueMatchMonitor` | `TurnMonitor` match clock, server-authoritative ("M:SS"/"OT" pushed by ClientRpc on the shared display channel). Pauses during celebrations; the controller decides full-time vs overtime; turn ends only on `ForceEnd()` |
| `AstroLeagueGoal` | Goal-mouth trigger (server-gated); reports to `AstroLeagueController.HandleGoalServer` — attribution lives in the controller |
| `AstroLeagueArena` | Runtime HyperSea stadium, built identically on every peer (no networking): invisible 1.0-restitution walls, pulsing edge cage, portal goal rings with ball-proximity anticipation flare, center ring, drifting plankton motes |
| `AstroLeagueMatchUI` | Runtime overlay canvas: domain score (reads the server-synced `GameDataSO.GetDomainMetricSum`), announcer banners, off-screen ball arrow |
| `AstroLeagueSettingsSO` | All tunables |
| `AstroLeagueScoringRuleSO` | Scoring strategy: mercy-rule end condition over per-domain `GoalsScored` sums, Score = personal goals, "WON BY N GOALS" reveal |

## Match Flow (server-authoritative)

```
SetupNewTurn            ball frozen at center, clock configured (server)
Ready (all humans) → shared 3-2-1 countdown = first kickoff count-in
OnCountdownTimerEnded   ParkVesselsForKickoff_ClientRpc (each peer parks vessels it
                        owns — vessels replicate owner-authoritatively) → players
                        active, clock runs, ball unfrozen, strikers armed, GO!
GOAL (server trigger)   attribution: most recent striker NOT on the defending domain
                        (own goals credit the opponent; unattributed → kickoff, no
                        score) → scorer.RoundStats.GoalsScored++ (NetworkVariable)
                        → ball detonates (ClientRpc juice) → celebration → kickoff
Mercy / golden goal     rule.IsObjectiveReached (domain goal sum ≥ GoalTargetCount),
                        or any goal during overtime → FinishMatch
Clock expires           tied + goldenGoalOvertime → OVERTIME (sudden death, "OT")
                        else → FinishMatch(rule.ResolveWinner)
FinishMatch             winner banner (real time) → matchMonitor.ForceEnd()
                        → OnTurnEndedCustom (server): AssignScores + Sort +
                        CalculateDomainStats → SyncFinalScores_ClientRpc
                        → WinnerName/WinnerDomain/Results on every peer
                        → InvokeWinnerCalculated + InvokeMiniGameEnd → shared
                        end-game cinematic + scoreboard
```

## Scoring & Stats

- **Metric**: `ScoringMetric.Goals = 4` reading the new `IRoundStats.GoalsScored`
  (full `RoundStats` NetworkVariable pattern, same as `JoustCollisions`).
- **Domain aggregation**: the base `MultiplayerDomainGamesController` domain-sum
  NetworkVariables replicate per-domain goal sums to every peer — `MultiplayerHUD`
  domain boxes and `AstroLeagueMatchUI`'s score line both read
  `GameDataSO.GetDomainMetricSum` and can never diverge from the host.
- **Goal target**: `GameDataSO.GoalTargetCount` (mercy rule), published by the
  controller from `AstroLeagueSettingsSO.goalLimit` and synced by ClientRpc.
- **Final scores**: every player's `Score` = personal `GoalsScored`; the winning
  DOMAIN is the highest goal sum (golden goal guarantees no tie when enabled; with
  overtime disabled, full-time ties break by `ActiveDomains` order).
- **Comeback**: `ElementalComebackSystem` with `ScoreDifferenceSource.Goals` — buffs
  scale with the TEAM goal deficit (Elementals are the buff fundamental; no bespoke
  rubber-banding).

## Networking Model

| Concern | Owner | Mechanism |
|---|---|---|
| Ball physics + strikes | Server | Rigidbody sim; `OnCollisionEnter` (server-gated) |
| Ball position/velocity | Server → all | `NetworkVariable<Vector3>` ×2, client dead reckoning + smoothing |
| Ball frozen/hidden | Server → all | `NetworkVariable<bool>` ×2 |
| Strike velocity | Server | Per-vessel transform sampling each FixedUpdate (`gameData.Vessels`) — correct for host, remote and AI vessels alike |
| Goal detection + attribution | Server | Trigger + last-striker ring buffer |
| GoalsScored | Server → all | `RoundStats.n_GoalsScored` NetworkVariable |
| Match phase / clock | Server | Controller fields + monitor; display via ClientRpc |
| Kickoff parking | Owning peer | `ParkVesselsForKickoff_ClientRpc`; deterministic slots (domain members sorted by name) |
| Announcer beats / juice | All peers | ClientRpcs → C# events → `AstroLeagueMatchUI` / camera shake / haptics |
| Hitstop & celebration slow-mo | Solo sessions ONLY | Local `Time.timeScale` desyncs connected peers (see `MenuCrystalClickHandler` precedent) — gated on `ConnectedClientsIds.Count <= 1` |

## AI Striker

`AIPilot.SetExternalTargetProvider(Func<Vector3>)` (sampled once per frame in
`Update`, overrides crystal/player seeking). The controller arms every AI on the
server with billiard thinking:

- **Attack**: when on the own-goal side of the ball, aim `strikerApproachLead`
  behind the ball along the ball→enemy-goal line, so contact drives it goalward.
- **Recover**: when caught on the wrong side, swing wide around the ball
  (`strikerRecoverDistance`, perpendicular offset) instead of own-goaling.
- **Kickoff hold**: while the ball is frozen, hold at the team's kickoff anchor.

## Ball Physics Notes

- Vessels move via `transform.position +=` (`VesselTransformer`), so neither
  `collision.rigidbody` velocity nor (for remote vessels) `VesselStatus.Speed` is
  trustworthy on the server. `AstroLeagueBall` samples every vessel root's position
  per physics tick and uses the delta as strike velocity (`Course * Speed` is the
  first-tick fallback).
- Strike direction = `Slerp(deflectionDir, strikerHeading, directionalBias)` where
  deflectionDir points from the contact point through the ball center.
- Speed-dependent drag curve: `lowSpeedDrag` near rest (no creeping),
  `highSpeedDrag` at speed (billiard coast), hard stop under `stopThreshold`.
- Walls and ball both use zero-friction, max-bounce `PhysicsMaterial`s.
- **Trails are live obstacles.** Mass is conserved everywhere (no menu/mode
  exemptions): Squirrel trails laid during a match persist and the ball bounces off
  prisms — walling off your goal is legal, emergent defense; vessel abilities are
  the active counterforce. Replay is a full scene reload, which is the standard
  domain-games replay path (not a decay mechanism).

## Replay

`UseSceneReloadForReplay = true` — Play Again performs a full network scene reload
(HexRace/CC pattern). All match state, ball, arena, and accumulated trail mass are
destroyed with the scene and re-initialized fresh via `OnNetworkSpawn`.

## Shared-Code Touchpoints (added for this mode)

| Change | File |
|---|---|
| `AstroLeague = 36` | `_Scripts/Data/Enums/GameModes.cs` |
| `Goals = 4` metric | `_Scripts/Data/Enums/ScoringMetric.cs` + `Scoring/ScoringMetrics.cs` |
| `GoalsScored` stat (+ event, Cleanup, NetworkVariable) | `_Scripts/Data/Enums/IRoundStats.cs`, `RoundStats.cs` |
| `GameDataSO.GoalTargetCount` | `_Scripts/Utility/DataContainers/GameDataSO.cs` |
| `AIPilot.SetExternalTargetProvider` | `_Scripts/Controller/AI/AIPilot.cs` |
| `CustomCameraController.Shake` | `_Scripts/Controller/Camera/CustomCameraController.cs` |
| `SO_ArcadeGame.Min/MaxDomainsAllowed` (+ modal DC bounds) | `_Scripts/ScriptableObjects/SO_ArcadeGame.cs`, `_Scripts/UI/Modals/ArcadeGameConfigureModal.cs` |
| `ScoreDifferenceSource.Goals` | `_Scripts/Controller/Arcade/ElementalComebackSystem.cs` |

## Assets

| Asset | Path |
|---|---|
| Arcade game config | `_SO_Assets/Games/ArcadeGameAstroLeague.asset` (registered in `GameLists/OrganicRematchGames.asset`) |
| Settings | `_SO_Assets/Games/AstroLeagueSettings.asset` |
| Scoring rule | `_SO_Assets/Scoring Rules/AstroLeagueScoringRule.asset` |
| Comeback profile | `_SO_Assets/ComebackProfiles/AstroLeagueComebackProfile.asset` |
| End-game cinematic | `_SO_Assets/Cinematics/MinigameAstroLeagueCinematicDefinition.asset` (registered in `SceneCinematicLibrary.asset`) |
