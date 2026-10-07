# Parity harness (ROADMAP C1)

Both engines play the same **replay** and write the same **channels**; `engine_parity` diffs
Prisma's run against Unity's (the **goldens**) with the C9 tolerances.

```
Port/parity/
  manifest.json        seeds for the Random goldens + the replay cases
  tolerances.json      C9 bars (gate G1 calibrates them; ParityDiffTests plants the differences)
  replays/<case>.json  one replay per case
  goldens/random/random_<seed>.json      written by the Unity capture
  goldens/<case>/{state,events,transforms}.jsonl, frames/*.png
  results/latest.json  the last full engine_parity run against the goldens (feeds the scoreboard)
  subsystems.json      the scoreboard catalogue (Port/tools/gen_parity_scoreboard.py)
```

## Running it

| What | How |
|---|---|
| Diff the engine against the Unity goldens | `engine_parity` (MCP), or `prisma-mcp --call engine_parity '{}'` |
| Determinism (engine vs engine, run twice) | `engine_parity {"against":"self"}` |
| Against a hand-made golden (a planted difference) | `engine_parity {"golden_dir":"..."}` |
| With frames | `engine_parity {"frames":true}` (a window; xvfb-run on a display-less Linux) |
| One replay by hand | `CosmicShore --headless --replay Port/parity/replays/X.json --parity-out DIR` |
| Random goldens by hand | `CosmicShore --random-golden DIR --seeds 0,1,42` |
| Scoreboard | `python3 Port/tools/gen_parity_scoreboard.py` (`--check` in CI) |

A channel without a golden reports MISSING and does not fail; FAIL names the first divergence.

## Formats (both engines write exactly these)

**Replay** (`replays/<case>.json`, version 1):

```json
{ "version": 1, "scene": "Bootstrap", "seed": 1337, "frames": 4200, "checkpointEvery": 30,
  "record": "2400-4200:300",
  "do":     ["1500:arcade SkimRace", "2280:hold W 600"],
  "status": [ { "f": 2300, "XSum": 0.0, "YSum": 0.0, "Throttle": 1.0, "pressed": ["Button1Action"], "released": [] } ] }
```

- Fixed step 1/60 s for `frames` ticks; `Random.InitState(seed)` before the first scene loads.
- `do`: device-level steps in the engine's InputScript verbs (`click`, `key`, `hold`, `pad`,
  and the inspector verbs `arcade`, `score` ...). Prisma plays these today.
- `status`: per-frame `IInputStatus` snapshots plus the `InputEvents` pressed/released that
  frame. The game's own `ReplayPlayer` plays these (below), in both engines.
- `record`: frames FROM-TO every N captured as `frames/fNNNNN.png` (a window is needed).

**Channels**, JSON Lines:

| File | One line per | Fields | Compared |
|---|---|---|---|
| `state.jsonl` | checkpoint (every `checkpointEvery` frames) | `frame`, `t`, `scene`, `stats{name:{domain,score,crystals}}`, `domainSums{}` | exact (all but `t`) |
| `events.jsonl` | event, in the order raised | `t`, `kind`, `name`: `fmod` = an FMOD event start (event path); `game` = `scene:<name>` on every scene load, or a `GameDataSO` match event by field name (`OnLaunchGame`, `OnSessionStarted`, `OnInitializeGame`, `OnMiniGameRoundStarted`, `OnMiniGameTurnStarted`, `OnMiniGameTurnEnd`, `OnMiniGameRoundEnd`, `OnMiniGameEnd`, `OnWinnerCalculated`, `OnResetForReplay`, `OnSessionEnded`); `contact` = a trigger contact that starts with a `VesselController` on either side, `name` = the two GameObject names sorted ordinally and joined by `\|`, all of one step written at the END of that step sorted ordinally (contact order inside a step is the physics engine's, not gameplay) | exact order and count over the kinds the golden carries (a kind the golden lacks is named, not compared); `t` within 0.04 s |
| `transforms.jsonl` | vessel per checkpoint, first 10 s of each scene | `frame`, `t` (since scene entered), `scene`, `id` (object name, `#n` for repeats), `p[3]`, `r[4]` | 1e-4 x distance from origin (floor 1 m), 0.1 deg |
| `random_<seed>.json` | seed | `value[1000]`, then `range[1000]` (`Range(0,1000)`), then `onUnitSphere[1000]`, all from one `InitState(seed)` | exact |
| `frames/*.png` | recorded frame | `frames/ui/` = UI screens; `frames/masks.json` = `{file:[[x,y,w,h]]}` blanked in both | SSIM >= 0.97 (UI 0.98) |

## The Unity PR (separate; a milestone session may not change `Assets/`)

The recorder/replayer must live in game code, because:
- Unity never compiles `Port/`, so a recorder there could not record or play a replay in Unity,
  and goldens need Unity to play the same replay.
- The game is the only place that can inject input at `IInputStatus`, between the strategy's
  `ProcessInput()` and the vessel reading it (`InputController.Update`). Device-level input
  differs between Unity's Input System and Prisma's, so it cannot replay reliably across engines.
- `Port/CLAUDE.md`: gameplay code is a Unity PR; the engine compiles it live, so Prisma gets it
  for free.

What it adds:

| File | What |
|---|---|
| `Assets/_Scripts/Utility/Replay/ReplayFile.cs` | The v1 format above (serialize/deserialize, `JsonUtility`-compatible) |
| `Assets/_Scripts/Utility/Replay/ReplayRecorder.cs` | Snapshots `IInputStatus` + `OnButtonPressed/Released` per frame after `ProcessInput()` |
| `Assets/_Scripts/Utility/Replay/ReplayPlayer.cs` | An `IInputStrategy` that writes recorded frames into `IInputStatus` and raises the recorded `InputEvents` |
| `Assets/_Scripts/Utility/Replay/DeterministicSession.cs` | `Random.InitState(seed)`, `Time.captureFramerate = 60`, a session seed for the seeded `System.Random` sites |
| `Assets/_Scripts/Utility/Replay/ParityProbe.cs` | Writes `state` / `events` / `transforms` in the formats above: FMOD starts through one wrapper, `game` events from `SceneManager.sceneLoaded` and the `GameDataSO` events listed above, `contact` events from an `OnTriggerEnter` relay on each vessel's colliders, flushed sorted at end of frame |
| `Assets/_Scripts/Controller/IO/InputController.cs` | One hook: when a `ReplayPlayer` is active it is the strategy |
| `Assets/_Scripts/Editor/Parity/ParityCapture.cs` | `[CliCommand]` + `FrogletTools/Parity/Capture Goldens`: for each manifest case, play it in Play mode and write `Port/parity/goldens/<case>/`; write `goldens/random/random_<seed>.json` per manifest seed |
| `Assets/_Scripts/Tests/Editor/ReplayFileTests.cs` | Format round trip; Random sequence per seed |

**Input the replay does not cover.** `ReplayPlayer` replays the `IInputStatus` path (`InputController.Update`
calls `currentStrategy.ProcessInput()`, 6 strategies). 36 scripts also read `Keyboard/Gamepad/Mouse/Touchscreen.current`
directly (menus, debug toggles, `DualMouseInputStrategy`); a replay case must not depend on them, or the PR routes the
ones it does through `IInputStatus`. The two cases here reach the match through the `do` stream's
inspector verb (`arcade`), so in Unity `ParityCapture` performs the same step through the same game API.

**Nondeterminism the PR must pin (measured 2026-10-08).** No `Random.InitState` at match start (178
`UnityEngine.Random` draws in runtime code; only `SegmentSpawner`, `FlowFieldView` and `WarpFieldView`
reseed, `ModePreviewPlantingModel` reseeds and restores); 7 unseeded `System.Random` (`AIPilot:1085`,
`ToyShuffle`, `ProfileModal`, `ScreenshotDirector`, `AnimationRecorderWindow`, and `SpawnableBase` /
`SpawnableCord` when their seed is 0) take `DeterministicSession`'s seed; 10 `Guid.NewGuid` sites are
identity only and must never reach a compared channel.

Until it lands, Prisma plays the `do` stream and every Unity channel is MISSING.
