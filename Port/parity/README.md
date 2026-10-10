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
  profile/             the returning-user profile every engine replay run starts from (player "parity")
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
| The status-frame case by hand | `CosmicShore --headless --replay Port/parity/replays/skimrace-status.json --parity-out DIR` (run.json says `"input":"ReplayPlayer"`) |
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
  frame. The game's own `ReplayPlayer` plays these, in both engines: Unity through the
  `COSMIC_SHORE_REPLAY` hook, Prisma through `ParityRun.BeginSession` (below).
- `record`: frames FROM-TO every N captured as `frames/fNNNNN.png` (a window is needed).

**Channels**, JSON Lines:

| File | One line per | Fields | Compared |
|---|---|---|---|
| `state.jsonl` | checkpoint (every `checkpointEvery` frames) | `frame`, `t`, `scene`, `stats{name:{domain,score,crystals}}`, `domainSums{}` | exact (all but `t`) |
| `events.jsonl` | event, in the order raised | `t`, `kind`, `name`: `fmod` = an FMOD event start or restart (event path; a GUID-only reference is named from `Cosmic Shore/Build/GUIDs.txt`); `fmod-stop` = an explicit `EventInstance.stop()`, `PATH\|ALLOWFADEOUT` or `PATH\|IMMEDIATE` (not a one-shot ending by itself); `fmod-snapshot` = a mixer snapshot, `start:PATH` / `stop:PATH` for an FMOD snapshot (never also written as `fmod`/`fmod-stop`), `start:mixer:MIXER/SNAPSHOT` for a Unity `AudioMixerSnapshot.TransitionTo`; `game` = `scene:<name>` on every scene load, or a `GameDataSO` match event by field name (`OnLaunchGame`, `OnSessionStarted`, `OnInitializeGame`, `OnMiniGameRoundStarted`, `OnMiniGameTurnStarted`, `OnMiniGameTurnEnd`, `OnMiniGameRoundEnd`, `OnMiniGameEnd`, `OnWinnerCalculated`, `OnResetForReplay`, `OnSessionEnded`); `contact` = a trigger contact that starts with a `VesselController` on either side, `name` = the two GameObject names sorted ordinally and joined by `\|`, all of one step written at the END of that step sorted ordinally (contact order inside a step is the physics engine's, not gameplay); `collision` = an `OnCollisionEnter` pair (any colliders, C3), named and ordered like `contact` | exact order and count over the kinds the golden carries (a kind the golden lacks is named, not compared); `t` within 0.04 s |
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
| `Assets/_Scripts/Utility/Replay/ParityProbe.cs` | Writes `state` / `events` / `transforms` in the formats above: the FMOD kinds at the game's FMOD seams (below), `game` events from `SceneManager.sceneLoaded` and the `GameDataSO` events listed above, `contact` events from an `OnTriggerEnter` relay on each vessel's colliders and `collision` events from an `OnCollisionEnter` relay on each dynamic Rigidbody (the Astro League ball), flushed sorted at end of frame |
| `Assets/_Scripts/Controller/IO/InputController.cs` | One hook: when a `ReplayPlayer` is active it is the strategy |
| `Assets/_Scripts/Editor/Parity/ParityCapture.cs` | `[CliCommand]` + `FrogletTools/Parity/Capture Goldens`: for each manifest case, play it in Play mode and write `Port/parity/goldens/<case>/`; write `goldens/random/random_<seed>.json` per manifest seed |
| `Assets/_Scripts/Tests/Editor/ReplayFileTests.cs` | Format round trip; Random sequence per seed |

**The FMOD seams (measured 2026-10-08, C4).** Every start, wherever it comes from, is one
`EventDescription.setCallback(STARTED | RESTARTED)` on each loaded event: that is `fmod` (and
`fmod-snapshot start:` for a snapshot description). Explicit stops have exactly three sources in
the game, and the probe writes `fmod-stop` at each without touching the vendor plugin:
- `FmodSafe.StopAndRelease` (7 calls). `FmodSafe.TryCreateInstance` is the game's only
  `RuntimeManager.CreateInstance` (8 callers: music, ship engine and element layers, drift,
  proximity boost, flora ambient, the one-shot helper).
- `FMODOneShotVolumeHelper`, which plays every one-shot (create, volume, 3D/attach, `start`,
  `release`) and rejects a LOOPING event (`isOneshot` false) with `stop(IMMEDIATE)`.
- `StudioEventEmitter.Stop`: 9 emitters in 7 prefabs (7 play on Object Start, 6 stop on Object
  Destroy, all `AllowFadeout`) plus the ones `SwarmFauna` adds; a relay on each emitter whose stop
  trigger is Object Destroy writes the stop from its own `OnDestroy`. A destroyed emitter WITHOUT a
  stop trigger is only detached (the vendor's `OnDestroy`), so no stop.
Do not take stops from the `STOPPED` callback: it also fires when a one-shot ends by itself.
- Snapshots: none today. The FMOD project's snapshot list is empty (`GUIDs.txt`: 3 banks, 2 buses,
  61 events, 0 snapshots), and `Main_AudioMixer` has only its default snapshot, which no script
  transitions to. A golden with no `fmod-snapshot` line therefore matches only a Prisma run with none.

The FMOD kinds are compared only when the golden carries them, so a probe that cannot record
`fmod-stop` still gets a starts-only comparison. Prisma runs parity with `COSMIC_SHORE_AUDIO=nrt`:
the FMOD runtime and the project's banks, non-real-time with no output, so `isOneshot` and the
other event descriptions answer exactly as they do in Unity. Without the runtime the run's
`run.json` says `"audio":"silent"` and `engine_parity` flags the FMOD channel as approximate.

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

The PR landed on this branch (`1a397c3e3`, the probe's reflection-free read in `646d599e0`). What
is still owed for goldens is the editor capture run (`FrogletTools > Parity > Capture Goldens`):
until `Port/parity/goldens/` exists every Unity channel is MISSING and `engine_parity` passes
vacuously; the determinism check (`against: self`) is the one that bites today.

## How Prisma plays a replay (the engine side, 2026-10-10)

`--replay FILE` is read with the game's own `ReplayFile` (compiled live), so both engines parse
one format and a version other than 1 is loud in both. The engine plays the `do` steps itself
(InputScript verbs) and then HANDS THE FILE TO THE GAME in `ParityRun.BeginSession`, called by
`PlayerBoot` right before the game's `BeforeSceneLoad` hooks, the phase in which a Unity player
build's `COSMIC_SHORE_REPLAY` hook runs, so the seed lands at the same point in both engines:

- `DeterministicSession.Begin(seed)` always: `Random.InitState`, the seven seeded
  `System.Random` sites, `Time.captureFramerate`.
- `ReplayPlayer.Start(file)` when the file carries `status` frames; `InputController.SelectStrategy`
  then hands the strategy slot to the replay and the frames reach `IInputStatus` where a device's
  would. A file with an empty `status` (boot-menu, skimrace-fly, astroleague-strike, bloomrush-i4)
  is driven by its `do` stream through the device strategies, exactly as before.

**One writer per directory.** In a Prisma run the engine's `ParityRun` writes `--parity-out`; the
game's `ParityProbe` stays inert unless `COSMIC_SHORE_PARITY_OUT` is set. The two must never write
the same files: `CosmicShore` refuses to start (exit 2) when `COSMIC_SHORE_PARITY_OUT` names the
`--parity-out` directory, and when `COSMIC_SHORE_REPLAY` names a file other than `--replay` (the
game's hook would start a second replay). Pointing the probe at a DIFFERENT directory is allowed:
that is how the two writers are compared on one run. `run.json` records which writer made a
directory (`"engine":"prisma"` / `"unity"`), the audio mode, and for a replay its seed, status-frame
count and `"input":"ReplayPlayer"` or `"do"`.

**The profile a run plays as.** Every `--replay` run starts from a fresh copy of
`Port/parity/profile/` (`prefs.json` with the age and consent prompts answered, `ugs-cloudsave.json`
with a player named `parity`, a fixed `ugs-player-id`), placed under `--parity-out/profile` and
made the process's persistent data path (`ParityRun.PrepareProfile`). Two reasons: a first-time
profile stops in the Authentication scene at the username prompt and never reaches the menu the
replays press (measured: `engine_parity`'s `COSMIC_SHORE_PROFILE=parity` was such a profile, so
every case ended with 3 events and 0 transforms), and the display name is the KEY of the state
channel's `stats`, so every run, and the Unity capture, must play as the same name. **The Unity
capture therefore has to run as a returning user named `parity`**, or `state.jsonl` differs on the
first checkpoint of every match. Without the template the machine's profile plays, with a line
saying so.

**`game` events are hooked at asset load**, as the probe hooks them: every loaded `GameDataSO`
(`Resources.FindObjectsOfTypeAll`, which the engine's content bridge now answers for every
ScriptableObject it reads, as Unity does), re-scanned on each scene load and checkpoint. Measured on
skimrace-fly: the menu's `OnInitializeGame` (t 1.57 s) and the Start press's `OnLaunchGame`
(t 27.0 s) joined the engine's `events.jsonl`; nothing else changed.

**FMOD: an event no loaded bank carries does not start.** `RuntimeManager.CreateInstance` and
`GetEventDescription` resolve an `EventReference` by GUID (the serialized path is the editor's
label) and throw `EventNotFoundException` for a GUID the banks do not carry, which the game's
`FmodSafe.TryCreateInstance` catches: nothing starts, no `fmod` line, one engine warning per GUID
naming the stale path. With the vendor runtime up (`COSMIC_SHORE_AUDIO=nrt`) the banks answer;
without it the engine reads the GUID index of `Cosmic Shore/Build/Desktop/Master.strings.bank`
(`FmodGuids.LoadStringsBank`), not `GUIDs.txt`: the file is only as current as the last manual
File > Export GUIDs, the bank is rebuilt on every Build. Measured 2026-10-10: GUIDs.txt lists 75
entries, the strings bank 89; the Bootstrap music `{03de9ea9-9b51-400a-b0cb-8bcc12a12697}`
(`event:/Music/Music`) and seven other serialized references (Rhino's `event:/Engine`, the Drift,
Goal, Mass, Time and Gameplay loops) are in the bank and NOT in the file, and `Mass brittle star` is
in the file and not in the bank. So the music plays in Unity and in Prisma, and the boot-menu
`events.jsonl` is identical before and after the gate (bar the Authentication load-time drift,
board B-1); the gate's effect is proved by `FmodBankGateTests`, not by a shipped reference. A
path-only reference is refused only through a GUID the table knows; the index is cross-checked
against GUIDs.txt at load and dropped (with a line on stderr) if it misses most of the file.

**The status-frame case `skimrace-status`** (seed 2026, 2760 frames): the skimrace-fly `do` steps
with `arcade ready` at frame 1800, then 600 status frames shaped as a keyboard writes them: XDiff
0.5 (cruise, the dual-stick speed term at rest) with XSum sweeps (-1 to 1 and back, a held 0.6
bank), a full-speed stretch (XDiff 1 with the E key, Throttle 1, frames 300 to 449), a YSum pitch,
one `Button1Action` press/release, and a neutral last frame (the player holds it). Authoring
note: **XDiff is the speed term** (`VesselTransformer.ThrottleAxis`), so a frame with XDiff 0 is a
full stop, not neutral; the first draft of this case carried XDiff 0 and the vessel yawed with the
sweep without moving a metre. The turn starts 3.6 s after `ready`; with `ready` at 2100
(skimrace-fly) that is 11.1 s after the scene loads, outside the 10 s transforms window, which is
why skimrace-fly's transforms show the parked vessel only. The game's `ParityCapture` plays this
case through the same `COSMIC_SHORE_REPLAY` hook, so a Unity golden for it is a straight
`engine_parity` diff of the replayed flight.

