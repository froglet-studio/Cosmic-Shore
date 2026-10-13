# Replay and parity probe (the game half of the parity harness)

The spec is `Port/parity/README.md` (ROADMAP C1, board T-3). Both engines play the same replay
and write the same channels; `engine_parity` diffs Prisma's run against the Unity goldens. This
folder is everything the Unity side needs, in game code, because only the game can inject input
at `IInputStatus` and because Prisma compiles these files live and gets them for free.

| File | What it is |
|---|---|
| `ReplayFile.cs` | The version-1 replay (`version`, `scene`, `seed`, `frames`, `checkpointEvery`, `record`, `do`, `status`), JsonUtility in and out. A `status` frame carries the spec's `f`, `XSum`, `YSum`, `Throttle`, `pressed[]`, `released[]` plus the other four floats the vessel reads (`XDiff`, `YDiff`, `LeftTriggerAnalog`, `RightTriggerAnalog`) and four stick vectors (`EasedLeftJoystickPosition`, which the six `SingleStickVesselTransformer` hulls and the Scarab steer from, `EasedRightJoystickPosition`, `LeftNormalizedJoystickPosition`, `RightNormalizedJoystickPosition`). The vectors were added still as version 1: a frame without them reads them as zero, which is what playback wrote before, and the engine ignores `status`, so every existing file stays compatible. |
| `ReplayRecorder.cs` | `ReplayRecorder.Start()`; `InputController.Update` then calls `Listen(status)` before the strategy runs (so the edges of the first recorded frame are heard) and snapshots `IInputStatus` after every `ProcessInput()` with the `InputEvents` raised that frame; `Stop()` / `StopAndSave(path)` returns the replay. |
| `ReplayPlayer.cs` | An `IInputStrategy`. `ReplayPlayer.Start(file)` / `StartFromFile(path)` / `Stop()` / `Active` / `Current`. While active, `InputController.SelectStrategy` hands it the strategy slot; it writes one recorded frame per `ProcessInput()` call (the floats and the stick vectors), raises the frame's events through the status' own event assets, holds the last frame when the recording ends and releases any held button on stop (`IsHolding(e)` reads what it still holds). It writes nothing but `IInputStatus`. |
| `DeterministicSession.cs` | `Begin(seed)`: `Random.InitState(seed)` and `Time.captureFramerate = 60`. `NewRandom(site)`: the seeded `System.Random` for the game's otherwise unseeded sites (`AIPilot`, `ToyShuffle`, `ProfileModal`, `ScreenshotDirector`, `AnimationRecorderWindow`, and, when their seed is 0, `SpawnableBase`, `SpawnableCord`, `SpawnableRaceTrack` and the Arkway and Wanderway conveyors `CellConveyor` / `MicrosceneConveyor`); with no session it is `new System.Random()`, as unseeded as each of those sites was (the three that seeded from `Environment.TickCount` included). `Reset()` stops the replay and any recording and ends the session; it runs at `RuntimeInitializeOnLoadMethod(SubsystemRegistration)`, the start of every run, because Play mode here keeps statics (domain reload is off). Also the environment hook below. |
| `ParityProbe.cs` | Writes `state.jsonl`, `events.jsonl`, `transforms.jsonl` (and `run.json`) in the spec's formats: `game` events (scene loads, the eleven `GameDataSO` events), `contact` / `collision` from relays on vessel colliders and dynamic Rigidbodies (sorted at frame end), `fmod` starts through `EventDescription.setCallback(STARTED | RESTARTED)`, `fmod-stop` at `FmodSafe.StopAndRelease`, `FMODOneShotVolumeHelper`'s looping refusal and an Object-Destroy emitter relay, state every `checkpointEvery` frames, vessel transforms for the first 10 s of each scene. `WriteRandomGolden(dir, seed)` writes `random_<seed>.json`. |

Everything is inert by default. `ReplayRecorder.Recording`, `ReplayPlayer.Active` and
`ParityProbe.Active` are false until something calls `Start` / `Begin`, and every run starts
with them false again (`DeterministicSession.Reset` at SubsystemRegistration). The only hooks in
gameplay code are the three calls in `InputController` (the replay's strategy slot, the
recorder's `Listen` before the strategy runs and its `Capture` after), the two `NoteFmodStop`
calls at the stop seams, and `NewRandom` at the seeded sites, all of which do nothing extra when
nothing is running.

This folder is inside the input-only gate, `python3 Tools/Build/check_ai_no_state_writes.py
--check`, with the pilots: a replay that wrote a pose, a course, a speed, a crystal or a score,
or reached any of them through reflection, would record a result the vessel never flew. The
probe therefore reads the eleven `GameDataSO` events through a direct accessor table rather than
`GetField`, and finds the asset with `Resources.FindObjectsOfTypeAll` (a controller's injected
reference is that same asset).

## Recording and playing in Unity

In the editor, three Play-mode entries do this by hand: **FrogletTools > Parity > Record Input**
starts the recorder, **Replay Recording** stops it, saves the file as
`Logs/Parity/recording-<yyyyMMdd-HHmmss>.json` (gitignored) and replays it from that file through
`ReplayPlayer.StartFromFile`, and **Stop Replay** hands input back to the devices. The replay
holds its last frame until Stop Replay or the end of Play. All three are greyed out outside Play
and while a capture is queued. Recording a turn on a single-stick hull (Sparrow, in the Dog Fight
card) and replaying it is the editor check that the stick vectors reach the vessel. The file's
header is the recorder's default (the active scene, seed 0 outside a deterministic session), so
it replays from wherever the vessel is when playback starts; it is not a parity case as it stands.

From code:

```csharp
ReplayRecorder.Start();                       // from a debug console, a tool, or a test rig
// ... fly ...
var file = ReplayRecorder.StopAndSave("Port/parity/replays/my-case.json");

DeterministicSession.Begin(file.seed);        // before the scene that matters loads
ReplayPlayer.StartFromFile("Port/parity/replays/my-case.json");
// ... the next InputController frame selects the player ...
ReplayPlayer.Stop();
```

The recorder captures what the live strategy wrote, so it records whichever device the pilot
used. v1 carries no device; the player sets `ActiveInputDevice = Keyboard` on activation.

## Driving a player build or Prisma with no editor

The hook in `DeterministicSession` runs at `RuntimeInitializeOnLoadMethod(BeforeSceneLoad)` in
Unity and in Prisma (which invokes the same attribute before its first scene):

| Variable | Effect |
|---|---|
| `COSMIC_SHORE_REPLAY=path/to/case.json` | `DeterministicSession.Begin(file.seed)` before the first scene loads, and `ReplayPlayer.Start(file)` when the file carries status frames. A replay whose `status` is empty (every manifest case today) is driven by its `do` stream through the device strategies, so the player stays out of the slot. |
| `COSMIC_SHORE_PARITY_OUT=dir` | `ParityProbe.Begin(dir, file.checkpointEvery)` (30 with no replay). |
| `COSMIC_SHORE_FMOD_GUIDS=path` | Where GUID-only FMOD references get their names (default `Cosmic Shore/Build/GUIDs.txt` next to `Assets/`). |

Both unset: nothing happens. Prisma's own `--parity-out` is the engine's `ParityRun`, a separate
writer; set one or the other for a run, not both into one directory.

```bash
COSMIC_SHORE_REPLAY=Port/parity/replays/boot-menu.json COSMIC_SHORE_PARITY_OUT=/tmp/parity_game \
  Port/src/CosmicShore.Player/bin/Debug/net10.0/CosmicShore --headless --frames 600
```

## The capture tool

**FrogletTools > Parity > Capture Goldens** (`Assets/_Scripts/Editor/Parity/ParityCapture.cs`,
`ParityCapture.CaptureAll()` for a CLI wrapper) reads `Port/parity/manifest.json`, writes
`Port/parity/goldens/random/random_<seed>.json` for every seed, then for each case sets the two
variables above, enters Play mode on the replay's scene and plays the `do` stream: `arcade <Mode>`,
`arcade intensity N`, `arcade start`, `arcade ready` through the same game API Prisma's inspector
calls (`ArcadeExploreView.SelectGame`, the modal's intensity handler, `OnStartGameClicked`,
`MiniGameControllerBase.OnReadyClicked`); `key`, `hold`, `click`, `move`, `type` through Input
System state events on a virtual keyboard and mouse. `pad`, `score`, `domain`, `party`,
`timescale` and the engine's inspection verbs are not played and are named in the console when a
case uses them. The probe writes `Port/parity/goldens/<case>/`. Output lands outside `Assets/`, so
the tool records nothing in the tool ledger and draws no ship panel.

A case ends when the driver exits Play itself: at the replay's frame count, or on an abort (the
probe never began, the replay would not load). Only then does the next case start. A case that
cannot start (missing or unparseable replay, scene not in the build) is counted as failed and
skipped, and a `do` step that throws marks its case failed, so the closing `done: N/M` counts only
clean cases. **Pressing Stop mid-case cancels the whole run**, as does **FrogletTools > Parity >
Cancel Capture** (between cases, or after anything left a run stuck). When the interrupted
case's probe had begun writing, its `run.json` is deleted and the console names the partial
directory, to recapture or restore with `git checkout`; `engine_parity` does not check for
`run.json`, so do not commit a partial directory. Every exit, cancelled or finished, runs
`DeterministicSession.Reset()` and clears the two environment variables and the forced start
scene, so the next ordinary Play flies on the devices.

## What this does not do (and what only an editor run can prove)

- Nothing here was run in a Unity editor: this checkout has none. The runtime files were
  compile-proved against the Unity 6 reference assemblies and by Prisma's live compile, and the
  probe was exercised in a headless Prisma run. The capture tool's Play-mode flow (domain reload
  survival through `SessionState`, Input System event injection reaching `Keyboard.current`,
  `playModeStartScene`) and the edit-mode tests are written, not witnessed. Soap's `Raise` does
  nothing outside Play mode, so the edit-mode tests assert the player's and recorder's own state
  (`IsHolding`, `IsListeningTo`), never an event delivered; an edge reaching a vessel is a
  Play-mode check.
- FMOD starts arrive on FMOD's update thread and are flushed at frame end, so their position
  relative to a `game` event in the same frame may differ from Prisma's, which writes a start at
  the `start()` call. `EventDescription.setCallback` reaches instances created after the hook,
  which is set on the first scene load and re-checked every frame as banks load; an instance
  created inside the same Awake that initialised FMOD is not seen. Prisma's pseudo bank lists the
  events `Cosmic Shore/Build/GUIDs.txt` names, so a reference whose GUID no bank carries (the
  Bootstrap music, today) is started by the engine from its stale serialized path but never
  hooked here, exactly as a real bank would refuse it. A relay is attached at frame end, so a contact or an emitter stop inside an
  object's first frame of life is not seen. Unity reports a child collider's trigger contact to
  the Rigidbody's object as well; those duplicates are dropped by looking for the child's own
  report in the same frame.
- Prisma's `ParityRun` hooks the `GameDataSO` events only once a `MiniGameControllerBase` exists;
  this probe hooks the asset as soon as it is loaded, so a Unity golden carries the menu's
  `OnLaunchGame` where the engine's channel does not until `ParityRun` hooks earlier.
- The `[CliCommand]` wrapper is one attribute away (see the class comment in `ParityCapture`).
