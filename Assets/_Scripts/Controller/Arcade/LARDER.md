# Larder - the Shrike's Charge game

`GameModes.Larder = 102`, hint toast `GameToastSituation.LarderHint = 177` (the ids
[`GAME_KIT_ROSTER.md`](../../../../Docs/ElementalLoop/GAME_KIT_ROSTER.md) row 12 reserved). Shrike only. Built 2026-11-15
on `claude/shrike-games`, beside [Thornbush](THORNBUSH.md) (Mass) and [Lancefall](LANCEFALL.md) (Space).

**Status: authored headless, NOT yet run in the Unity editor.** It was checked by its generator's `--check` (and one
watched failure) and the static gates. Nobody has played it, and every number below is a first cut.

> *A duel in the wreckage. Every crystal you take becomes a missile plant. Paint a rival and every plant fires at them.
> The first team to land the target of missile hits wins.*

## 1. What it scores

A shrike keeps a larder: prey hung on thorns. Here the thorns are the Boneyard's wreckage and the prey is a rival pilot.

- Every crystal the Shrike takes becomes a **missile plant** where it was taken, and every nest is crowned with one
  (`SHRIKE.md` §3.5).
- Hold the trigger in **Charge mode** and the Targeting Array paints the rival in its cone; every plant with a ready
  missile launches at them.
- A missile that reaches a rival hull is a missile-class combat hit (`CombatHitClass.MissileBlast`, the hull's own
  `missileHitClass`), which the platform already counts as `MissileHitsLanded`.

The metric is `ScoringMetric.CombatPoints` through `LarderScoringRuleSO.PointsForCombatHit`: a missile hit is
`missilePoints` (1), any other hit `otherHitPoints` (0), so a lance through a rival's hull is a petal hit but no point.
First DOMAIN to `larderMissileTarget` (**16**, `EndConditionOverridesSO`) wins, golf-timed. The comeback rate is
**0.3**: a quarter of the target behind (4 hits) buys 1.2 element levels.

## 2. The arena is the Boneyard, referenced

The four Boneyard cells (Dog Fight, Salvo and Breakwater fly them too) are referenced read-only: hollow hulks, spires,
girder cages and overpasses inside r = 520, more wreckage per rung, danger traps on the torn ends. The Shrike needs
exactly what they author: mass to dock on (a nest is left where you dock), cover a missile blows up on, and sight lines
between the cover for the paint. The pilots spawn on Dog Fight's 700 u ring; the crystals (6, 5, 4, 3 per intensity)
float among the wreckage within 420 u.

## 3. The kit stays exactly as it is

Nothing under the Shrike's code, prefab or config was edited (the LOCKED vessel rule). The levers are the wreckage, the
crystals and the AI.

## 4. The AI: a hunter that only steers

`LarderHunter` (on the shared `ShrikeSeats`) answers `AIPilot.SetExternalTargetProvider` and nothing else. The Shrike's
own autopilot picks Charge mode and holds the trigger while a rival is in its lock cone and a plant is ready, so the
hunter only says where the nose goes:

1. **Plants dry** (fewer missiles ready than the rung's floor) and a crystal within 700 u: to the crystal.
2. **Otherwise** the chosen rival (a human counts as 1.5x nearer), led by its velocity and off by the rung's aim error.

| Ladder (AI difficulty Easy to Legendary) | Values |
|---|---|
| `aiRetargetSeconds` | 2.5, 1.8, 1.2, 0.8 |
| `aiLeadSeconds` | 0, 0.2, 0.4, 0.6 |
| `aiAimError` (u) | 70, 40, 18, 6 |
| `aiReloadBelowMissiles` | 1, 2, 3, 4 |

Difficulty is the seat's AI level (`gameData.AIDifficultyFor`), never the intensity; the generator fails if the AI
reads the intensity or presses anything.

## 5. Files

| What | Where |
|---|---|
| Controller, settings, rule, AI | `Larder/LarderController.cs`, `LarderSettingsSO.cs`, `LarderScoringRuleSO.cs`, `LarderHunter.cs` |
| Turn monitor | `TurnMonitors/LarderMissileTurnMonitor.cs` (`CombatPointTurnMonitorBase`) |
| Shared Shrike AI seat | `ShrikeGames/ShrikeSeats.cs` |
| Generator | `Tools/Build/author_larder_assets.py` (`--check`), shared reads in `shrike_games_common.py` |
| Scene | `MinigameLarder.unity`, cloned from Kelpwood's |

## 6. What only the editor can tell

- Whether 16 hits is a 2-3 minute match, and whether a 1-point missile against a 0-point lance reads as fair.
- Whether the hunter's crystal runs keep enough plants growing, or the AI spends the match dry.
- Whether missiles find a rival through the wreckage often enough, or the cover eats them all.
