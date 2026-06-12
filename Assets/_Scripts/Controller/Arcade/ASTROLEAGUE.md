# Astro League Game Mode — Technical Documentation

## Overview

Astro League is hypersea soccer — the spirit of Rocket League translated to Cosmic
Shore. Two domains (Jade vs Ruby) fight to slam a glowing payload through the
opposing goal portal inside a wireframe arena suspended in the HyperSea. Solo play
pits the player (Jade, Squirrel-first vessel select) against an AI striker (Ruby).

**Key architectural facts:**

- **Scene**: `Assets/_Scenes/Singleplayer Scenes/MinigameAstroLeague.unity`
- **GameMode enum**: `GameModes.AstroLeague = 36`
- **Controller**: `AstroLeagueMatchController : SinglePlayerMiniGameControllerBase`
- **Config**: every gameplay number lives in `AstroLeagueSettingsSO`
  (`Assets/_SO_Assets/Games/AstroLeagueSettings.asset`) — match rules, billiard
  physics, juice, AI tuning, arena palette
- **Featured vessel**: Squirrel (drift class). `SO_ArcadeGame.Vessels` = Squirrel only

## Class Inventory (`_Scripts/Controller/Arcade/AstroLeague/`)

| Class | Role |
|---|---|
| `AstroLeagueMatchController` | Match director: kickoffs, goal celebrations (slow-mo), golden-goal overtime, winner banner, team/domain assignment, AI striker arming |
| `AstroLeagueBall` | Billiard-physics payload. Reads `VesselStatus.Speed/Course` from the striking vessel (vessels move via transform, so rigidbody velocity is useless). Hitstop, camera shake, emission flash (MaterialPropertyBlock), burst particles, haptics |
| `AstroLeagueMatchMonitor` | `TurnMonitor` match clock ("M:SS" on the shared turn-monitor display channel). Pauses during celebrations; the controller decides full-time vs overtime; turn ends only on `ForceEnd()` |
| `AstroLeagueScoreManager` | Goals per domain → mirrors into `RoundStats.Score` (keeps the shared scoreboard flow intact) → raises `EventOnAstroLeagueScoreUpdated` ("J - R") |
| `AstroLeagueGoal` | Goal-mouth trigger; awards `ScoringDomain` via `ball.NotifyGoalScored` |
| `AstroLeagueArena` | Runtime HyperSea stadium: invisible 1.0-restitution walls, pulsing edge cage, portal goal rings with ball-proximity anticipation flare, center ring, drifting plankton motes. Scene skybox is `HyperSeaSkybox.mat` |
| `AstroLeagueMatchUI` | Runtime overlay canvas: score, announcer banners (GOAL! / count-in / OVERTIME / winner), off-screen ball arrow |
| `AstroLeagueSettingsSO` | All tunables |

## Match Flow

```
SetupNewTurn            ball frozen at center, score reset, clock configured, Ready shown
Ready → 3-2-1 canvas    the shared CountdownTimer doubles as the first kickoff count-in
OnCountdownTimerEnded   players parked at team spawns → SetPlayersActive + StartTurn
                        → clock runs, ball unfrozen, GO! banner
GOAL                    ball detonates (burst + big shake + haptic) → ball hidden
                        → celebration slow-mo (unscaled-time delay, timeScale 0.35)
                        → kickoff: vessels re-parked, ball frozen center, 3-2-1 banners → GO
Mercy / golden goal     goalLimit reached, or any goal during overtime → FinishMatch
Clock expires           tied + goldenGoalOvertime → OVERTIME (sudden death, clock shows "OT")
                        else → FinishMatch
FinishMatch             winner banner (2s real time) → matchMonitor.ForceEnd()
                        → TurnMonitorController → turn end → round end → shared end-game
```

## Solo Team Assignment

`IPlayer.InitializeData` carries no domain. After `base.Start()` spawns players,
`AssignTeams()` sets humans → Jade / AI → Ruby via `Player.SetDomain` and mirrors
`RoundStats.Domain` (scoreboard + score manager both read it), parks vessels at
their team spawn, and arms each AI striker.

## AI Striker

`AIPilot` gained a minimal extension: `SetExternalTargetProvider(Func<Vector3>)`,
sampled once per frame in `Update`, overriding crystal/player seeking. The
controller's provider implements striker logic:

- **Attack**: when on the own-goal side of the ball, aim at a point
  `strikerApproachLead` behind the ball along the ball→enemy-goal line, so contact
  drives the ball goalward (billiard thinking).
- **Recover**: when caught on the wrong side, swing wide around the ball
  (`strikerRecoverDistance`, perpendicular offset) instead of own-goaling.
- **Kickoff hold**: while the ball is frozen, orbit the team spawn.

## Ball Physics Notes

- Vessels move via `transform.position +=` (`VesselTransformer.MoveShip`), so
  `collision.rigidbody` velocity on a vessel is ~0. `ResolveStrikerVelocity` reads
  `IVesselStatus.Course * Speed` from `GetComponentInParent<IVesselStatus>()`.
- Strike direction = `Slerp(deflectionDir, vesselHeading, directionalBias)` where
  deflectionDir points from the contact point through the ball center.
- Speed-dependent drag curve: `lowSpeedDrag` near rest (no creeping),
  `highSpeedDrag` at speed (billiard coast), hard stop under `stopThreshold`.
- Walls and ball both use zero-friction, max-bounce `PhysicsMaterial`s.

## Replay

`OnResetForReplay` → `ResetEnvironmentForReplay` (scores, ball, clock) →
`AssignTeams()` (re-park + re-arm strikers) → base flow re-shows Ready.
