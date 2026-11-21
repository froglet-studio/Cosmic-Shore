# The Meerkat's arcade games (Sentry, Warren, Cave-in)

Built 2026-11-15 from `Docs/ElementalLoop/GAME_KIT_ROSTER.md` rows 15-17 (Garrett: "make all the missing games you
can"). The Meerkat already had its Time game (Burrow Run). These three give it one game per remaining element:

| Row | Element | Card | `GameModes` | Hint toast | Built on | Arena (read-only) | Doc |
|---|---|---|---|---|---|---|---|
| 15 | Charge | Sentry | 105 | 180 `SentryHint` | Sentinel Mode | Corona's shells | [SENTRY.md](SENTRY.md) |
| 16 | Mass | Warren | 106 | 181 `WarrenHint` | Exit Portal | the bare Barren cell | [WARREN.md](WARREN.md) |
| 17 | Space | Cave-in | 107 | 182 `CaveInHint` | Soliton swallow and fire | Shatterplane's plates | [CAVEIN.md](CAVEIN.md) |

## What the three share

- **Nothing about the Meerkat changes** (the LOCKED vessel rule). No vessel file, prefab or action SO is touched. Each
  game picks an arena and a scoring metric, and steers the AI. Every assumption a game makes about the kit (the zap is a
  Bullet-class combat hit, a portal is laid through the hull's own prism channel, a fired prism annihilates what it
  hits and credits the maker) is a needle its generator checks against the shipped kit, so a kit change that breaks a
  game fails `--check` by name.
- **One AI hook, steering only**: `MeerkatGames/MeerkatArcadeSteer.cs`. It gives every AI Meerkat an
  `AIPilot.SetExternalTargetProvider` and asks the mode's picker for a new point every `aiRetargetSeconds`. The hull's
  own autopilot (`MeerkatSolitonExecutor.Autopilot`) already plans a soliton toward the AIPilot's target and presses RT,
  and `MeerkatSolitonKit` already turns Sentinel Mode on for an AI. So the hook presses nothing.
  `meerkat_games_common.steer_errors` fails if the hook or any picker writes an input or reads the intensity.
- **Difficulty is the seat's AI level** (`GameDataSO.AIDifficultyFor`), never the intensity. Every AI ladder has four
  rungs (Easy, Medium, Hard, Legendary) and is monotone toward harder. The generator asserts both and that the C#
  initializers match the authored asset.
- **Generators**: `Tools/Build/author_{sentry,warren,cavein}_assets.py`, sharing `Tools/Build/meerkat_games_common.py`
  (self-test: `python3 Tools/Build/meerkat_games_common.py`). Each has been watched failing `--check` on a mutated
  file and on a drifted C# ladder.

## Status

None of the three has been opened or played in Unity. Code was checked by the reference compile and the generators'
own validation only. Every target, ladder and comeback rate is a first estimate that needs a timed match.
