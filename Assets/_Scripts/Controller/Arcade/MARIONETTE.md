# Marionette - the Jewel Wasp's Charge game

`GameModes.Marionette = 126`, hint toast `GameToastSituation.MarionetteHint = 201` (the ids
[`GAME_KIT_ROSTER.md`](../../../../Docs/ElementalLoop/GAME_KIT_ROSTER.md) rows 36-37 reserved for the Jewel Wasp). Jewel
Wasp only. Built 2026-11-15 beside [Gorge](GORGE.md) (Space).

**Status: authored headless, NOT yet run in the Unity editor.** It was checked by its generator's `--check` and the
static gates. Nobody has played it, and every number below is a first cut.

> *Sting a creature and ride it into a rival pilot. Every attack that lands is a point. The first team to the target
> wins.*

## 1. What it scores

The wasp's Charge verb is the attack it makes through a creature (JEWEL_WASP.md §2):

- In a host, **RT at a rival vessel** plays the creature's attack and lands a **Strike** (`CombatHitClass.Strike`,
  `JewelWaspConfig.attackHitClass`). Charge is the attack's recharge, 3 s at Charge 0 to 0.8 s at Charge 10.
- With **Charge 5** (Burst), leaving a creature bursts it: a `MissileBlast`-class hit (`burstHitClass`) on every rival
  vessel within 60 u.

The metric is `ScoringMetric.CombatPoints` through `MarionetteScoringRuleSO.PointsForCombatHit`: a Strike is
`strikePoints` (1), a burst hit `burstPoints` (1), anything else `otherHitPoints` (0). First DOMAIN to
`marionetteStrikeTarget` (**12**, `EndConditionOverridesSO`) wins, golf-timed. The comeback rate is **0.4**: a quarter of
the target behind (3 strikes) buys 1.2 element levels, which the wasp spends on a faster attack.

## 2. The arena is Kelpwood's, referenced

Kelpwood's four cells (the Angler's Charge game) are referenced read-only: kelp strands (28, 44, 64, 88; danger tips
on I3 and danger collars on I4), herds of tadpoles grazing among them (8, 6, 5, 4) and omni crystals (3, 2, 2, 1). The
wasp needs exactly what they author: creatures to take, cover between the pilots, and an omni crystal, which hatches a
predator the wasp takes at once (JEWEL_WASP.md §2). The ladder is more kelp and fewer creatures. The pilots spawn on
Kelpwood's ring.

## 3. The kit stays exactly as it is

Nothing under the wasp's code, prefab or config was edited (the LOCKED vessel rule). The levers are the kelp, the herds,
the crystals and the AI.

## 4. The AI: a puppeteer that only steers the free wasp

The wasp flies itself (JEWEL_WASP.md §6): free, it stings the creature ahead of its nose; in a host, it flies at the
nearest rival with RT held and leaves after 14 s. `MarionettePuppeteer` (on the shared `WaspGames/WaspSeats`) answers
`AIPilot.SetExternalTargetProvider` while the wasp is free, so the creature ahead is the one it picked:

1. Every retarget, it picks a rival (a human counts as 1.5x nearer).
2. With the rung's choosiness, it takes the creature nearest that rival; otherwise any creature in the cell.
3. The nose aims at that creature, off by the rung's aim error.

| Ladder (AI difficulty Easy to Legendary) | Values |
|---|---|
| `aiRetargetSeconds` | 3, 2.2, 1.5, 1 |
| `aiHostChoosiness` | 0.25, 0.5, 0.75, 0.95 |
| `aiAimError` (u) | 60, 35, 15, 5 |

Difficulty is the seat's AI level (`gameData.AIDifficultyFor`), never the intensity. The generator fails if the AI
reads the intensity, presses anything or steers a wasp that is in a host.

## 5. Files

| What | Where |
|---|---|
| Controller, settings, rule, AI | `Marionette/MarionetteController.cs`, `MarionetteSettingsSO.cs`, `MarionetteScoringRuleSO.cs`, `MarionettePuppeteer.cs` |
| Turn monitor | `TurnMonitors/MarionetteStrikeTurnMonitor.cs` (`CombatPointTurnMonitorBase`) |
| Shared wasp AI seat | `WaspGames/WaspSeats.cs` |
| Generator | `Tools/Build/author_marionette_assets.py` (`--check`), shared reads in `wasp_games_common.py` |
| Scene | `MinigameMarionette.unity`, cloned from Kelpwood's |

## 6. What only the editor can tell

- Whether 12 strikes is a 2-3 minute match, and whether a tadpole's 22 u attack reach reaches a rival often enough.
- Whether the herds last: a host keeps its starvation clock, and the upper rungs have fewer creatures.
- Whether the AI's host choice matters, since the wasp's own AI in a host goes for the nearest rival within 400 u.
