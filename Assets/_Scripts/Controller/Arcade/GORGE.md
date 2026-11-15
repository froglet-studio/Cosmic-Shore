# Gorge - the Jewel Wasp's Space game

`GameModes.Gorge = 127`, hint toast `GameToastSituation.GorgeHint = 202` (the ids
[`GAME_KIT_ROSTER.md`](../../../../Docs/ElementalLoop/GAME_KIT_ROSTER.md) rows 36-37 reserved for the Jewel Wasp). Jewel
Wasp only. Built 2026-11-15 beside [Marionette](MARIONETTE.md) (Charge).

**Status: authored headless, NOT yet run in the Unity editor.** It was checked by its generator's `--check` and the
static gates. Nobody has played it, and every number below is a first cut.

> *Sting a creature, ride it to a shoal and suck the reef dry. Most prisms off the board in 90 seconds wins.*

## 1. What it scores

The wasp's Space verb is the host's suction (JEWEL_WASP.md §2): in a host, **RT held at prisms** pulls up to 8 of the
nearest rival-domain prisms into the creature's mouth every 0.45 s (`JewelWaspConfig.suctionBites`, `suctionInterval`).
Space widens the reach (x1 to x3 the creature's own), and **Space 5** (Maw) pulls from every side. A possessed
creature eats shields, so the crusted shoals on I3-I4 are food too.

The metric is `ScoringMetric.PrismsDestroyed`: every prism the pilot's hull breaks, as every mode counts it, plus every
reef prism a creature they ride sucks in. `Fauna.PossessedSuction` names its rider on the bite, and
`GorgeController.CreditBite` books the meal. It adds to `HostilePrismsDestroyed` only where StatsManager did not already
credit it (`SimulatedHere`: an AI, or the local player), so nothing is counted twice. Timed, **90 s**
(`gorgeRoundSeconds`, `EndConditionOverridesSO`), highest wins. The comeback rate is **0.025**: a domain a quarter of
the reference meal behind (48 prisms) buys 1.2 Space levels, which is more suction reach.

## 2. The arena is Chum's, referenced

Chum's four cells are referenced read-only: the Barren nucleus, tameable tadpole herds of 6, 9, 12 and 15 grazing the
reef, and 4 to 2 omni crystals. The reef is Chum's own (`ChumReef.ForIntensity`, laid by `GorgeController` on every
peer):

| Rung | Shoals | Shells | Band | Crusted |
|---|---|---|---|---|
| I1 | 8 | 2 | 0.30 | no |
| I2 | 12 | 2 | 0.45 | no |
| I3 | 12 | 3 | 0.45 | yes |
| I4 | 16 | 3 | 0.60 | yes |

The wild herds graze the same reef, so the creatures race you for the meal. The pilots spawn on Chum's 450 u ring.

## 3. The kit stays exactly as it is

Nothing under the wasp's code, prefab or config was edited (the LOCKED vessel rule). The levers are the reef, the
herds, the crystals and the AI.

## 4. The AI: a glutton that only steers the free wasp

In a host the wasp's own AI flies the creature (JEWEL_WASP.md §6). `GorgeGlutton` (on the shared `WaspGames/WaspSeats`)
answers `AIPilot.SetExternalTargetProvider` only while the wasp is free:

1. Every retarget, it picks a shoal, weighing nearness against how full it still is (`aiFullShoalWeight`).
2. With the rung's choosiness, it takes the creature nearest that shoal; otherwise any creature in the cell.
3. The nose aims at that creature, off by the rung's aim error.

| Ladder (AI difficulty Easy to Legendary) | Values |
|---|---|
| `aiRetargetSeconds` | 6, 4.5, 3.5, 2.5 |
| `aiHostChoosiness` | 0.25, 0.5, 0.75, 0.95 |
| `aiAimError` (u) | 60, 35, 15, 5 |
| `aiFullShoalWeight` | 0, 0.3, 0.6, 0.9 |

Difficulty is the seat's AI level (`gameData.AIDifficultyFor`), never the intensity. The generator fails if the AI
reads the intensity, presses anything or steers a wasp that is in a host.

## 5. Files

| What | Where |
|---|---|
| Controller, settings, rule, AI | `Gorge/GorgeController.cs`, `GorgeSettingsSO.cs`, `GorgeScoringRuleSO.cs`, `GorgeGlutton.cs` |
| Turn monitor | `TurnMonitors/GorgeTimeTurnMonitor.cs` (`NetworkTimeBasedTurnMonitor`) |
| Shared wasp AI seat | `WaspGames/WaspSeats.cs` |
| Generator | `Tools/Build/author_gorge_assets.py` (`--check`), shared reads in `wasp_games_common.py` |
| Scene | `MinigameGorge.unity`, cloned from Chum's |

## 6. What only the editor can tell

- Whether the wasp's own host AI feeds at all: in a host it flies at the nearest rival within 400 u, so it may chase
  pilots instead of shoals. If it does, the fix belongs on the wasp (its owner's call), not in this mode.
- Multiplayer: possessed creatures feed on the server, and the reef is laid per peer, so a client's local reef may not
  visibly shrink while the score still counts.
- Whether 90 s is long enough to ride, eat, leave and ride again, and whether the I4 herds of 15 out-eat the pilots.
