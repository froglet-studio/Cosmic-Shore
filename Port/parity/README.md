# Parity harness (ROADMAP C1)

Both engines play the same **replay** and write the same **channels**; `engine_parity` diffs
Amoebius's run against Unity's (the **goldens**) with the C9 tolerances.

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
| Diff the engine against the Unity goldens | `engine_parity` (MCP), or `amoebius-mcp --call engine_parity '{}'` |
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
  and the inspector verbs `arcade`, `score` ...). Amoebius plays these today.
- `status`: per-frame `IInputStatus` snapshots plus the `InputEvents` pressed/released that
  frame. The game's own `ReplayPlayer` plays these (below), in both engines.
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
  differs between Unity's Input System and Amoebius's, so it cannot replay reliably across engines.
- `Port/CLAUDE.md`: gameplay code is a Unity PR; the engine compiles it live, so Amoebius gets it
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
  transitions to. A golden with no `fmod-snapshot` line therefore matches only an Amoebius run with none.

The FMOD kinds are compared only when the golden carries them, so a probe that cannot record
`fmod-stop` still gets a starts-only comparison. Amoebius runs parity with `COSMIC_SHORE_AUDIO=nrt`:
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
reseed, `ModePreviewPlantingModel` reseeds and restores); 6 unseeded `System.Random` (`AIPilot:1085`,
`ToyShuffle`, `ProfileModal`, `ScreenshotDirector`, and `SpawnableBase` /
`SpawnableCord` when their seed is 0) take `DeterministicSession`'s seed; 10 `Guid.NewGuid` sites are
identity only and must never reach a compared channel.

Until it lands, Amoebius plays the `do` stream and every Unity channel is MISSING.

## Render parity: the pixel loss (2026-10-29)

Garrett: *"make a tool in unity that will use the unity recorder in a constructed scene. it will save off some
standardized control video. then you will create the exact same scene and video in amoebius. your loss
function will be based sampling pixel discrepancies. this will allow you to iterate until you are able to
close the gaps in your rendering artifacts and other issues."*

One scene, both engines, frame for frame, scored pixel by pixel:

| Piece | Where | What |
|---|---|---|
| The control scene | `Assets/_Scenes/Game_TestDesign/RenderParity.unity`, written by `Tools/Build/author_render_parity_scene.py` (`--check` is the drift gate) | the HyperSea skybox and the game's fog, the game's PostProcessing volume and URP camera data, PrismManagers and the ThemeManager, the `[StandaloneScene]` marker (without it `AppManager`'s boot chain replaces any scene with Authentication after the splash, in Amoebius too), a 9x5x9 Jade prism grid laid by `PrismGridExplosionHarness` on a warm-up frame with the Dolphin's Ruby blast at take frame 150, twelve GyroidBranch spindles, the Manta hull at rest (mesh + its four materials, no vessel components), and the take |
| The take | `Assets/_Scripts/Utility/Parity/RenderParityTake.cs` (runtime; both engines compile it) | everything is a function of the frame index: 30 warm-up frames, 300 take frames at 60 fps, 1280x720; the camera stands on the first keyframe facing the other way through the warm-up and cuts onto its path at take frame 0 (the cut is what the comparison aligns on, never a frame number), smoothsteps between keyframes, fires the events (`spawn`, `explode`); every Canvas is switched off (overlays are not in a camera capture). Unity: `Time.captureFramerate` = fps; Amoebius: `--step-hz` |
| Unity records | `python3 Tools/Build/render_parity_golden.py --push` on the machine with the editor (finds Unity from ProjectVersion.txt, runs `-batchmode -executeMethod CosmicShore.Editor.Parity.RenderParityRecorder.RecordBatch`, commits and pushes the golden), or `FrogletTools > Amoebius > Render Parity: Record Control Take` in the open editor (`Assets/_Scripts/Editor/Parity/RenderParityRecorder.cs`, the Unity Recorder 5.1) | opens the scene, enters Play, records every frame from the first Play frame to the end of the take: `Port/parity/goldens/render/<take>/frames/f0000.png...` (camera input, the take's size), `take.mp4`, `take.json` (fps, warm-up, frames, size, the URP asset's MSAA, keyframes, Unity version, git head). Exits Play when done; `Cancel Recording` stops it |
| Amoebius records | `python3 Port/tools/render_parity.py record` | boots the player on the scene at the take's size, fps and MSAA, waits for the take to count, records from 10 frames before take frame 0 to its end into `Port/parity/results/render/<take>/frames/` |
| The loss | `python3 Port/tools/render_parity.py compare` | aligns the sequences on the jump, then per aligned pair: `rmse` of linear RGB, `luma_rmse`, `ssim` (luminance, gaussian window, half size), a 16x9 tile heat map; `report.json` (loss = mean rmse + 0.5 x (1 - mean ssim), the worst frames and tiles), `sheet.png` (unity \| amoebius \| 4x difference, every 30th frame), `heat.png` |
| Determinism | `python3 Port/tools/render_parity.py self` | Amoebius against itself twice: the floor the loss can reach. Measured 2026-10-29: two runs are bit-identical over all 301 pairs (loss 0.0000, ssim 1.000), so every non-zero number in a Unity comparison is a real difference |

The golden PNG frames are git-ignored (300 MB a take); `take.mp4` and `take.json` are committed, and `compare`
decodes the movie when the PNGs are absent (the encode is H.264 4:2:0 at 4.8 Mbps, so the floor is not ~0.005: chroma is halved on every edge; record from the PNGs when they are on hand). The loop: `compare` names
the worst tile and frame, the sheet shows what is there, the engine is fixed (a family, a graph node, a
state), `record` and `compare` again. Everything on both sides is the game's own code and assets; nothing in
the scene is a mock.

Measured while bringing the Amoebius side up (2026-10-29): the scene generator must write a bracketed object
name quoted (`m_Name: '[RenderParityTake]'`; bare, a YAML reader takes `[...]` as a flow sequence and the
object loads nameless), a tool scene needs the `[StandaloneScene]` marker or the sign-in screen is what gets
recorded, and the blast at take frame 150 does destroy the grid in Amoebius (405 prisms down to 140 by frame
350) while its shell draws nothing visible from 400 units away - the first candidate for the loss once the
Unity golden exists. `run.json` records where the cut landed (`cut`) as well as the intended lead.

### Scores, 2026-10-31 (control take, decoded from `take.mp4`)

| Change | loss | rmse | ssim |
|---|---|---|---|
| first score (the cloud's auto-detected Low preset, post on) | 0.1478 | | |
| Panini skipped | 0.1211 | | |
| post off (bloom and Panini) | 0.1075 | | |
| Scene Color node returns 0.5 instead of black | 0.1004 | | |
| Scene Color returns URP's unset-texture grey (sRGB 0.5 = 0.214 linear) | 0.0937 | | |
| quality pinned to High (`PINNED_PREFS`), post off, grey Scene Color | 0.0387 | 0.0189 | 0.960 |
| the prism destruction's fragments (`PrismCollisionBurst.shader`, family 12) fly instead of standing still | **0.0372** | 0.0192 | 0.964 |
| (tried, not kept) the built-in Sphere as Unity's 24x16 UV sphere instead of the icosphere | 0.0393 | 0.0199 | 0.961 |
| (tried, not kept, lossless golden) Unity's exact 439-vertex built-in Sphere | 0.0291 | 0.0162 | 0.974 |
| (lossless golden, from 0.0278) `UniTask.Delay` counts frames like upstream, so the blast starts on Unity's frame | **0.0199** | 0.0109 | 0.982 |

What each one is:
- **The golden has no post.** Stars fitted between the two engines (a KD-tree over detected stars, a similarity
  fit) land at scale 1.000 with post off and 1.16 with Panini, and under High post on scores 0.2765, Panini
  skipped 0.2451, post off 0.0387. The game itself shows Panini and bloom, so the Unity Recorder path drops the
  volume; that is a Unity-side question. `take.json` carries `"post": "off"` and `render_parity.py record`
  follows it (`--post on|off|bloom,panini` overrides), through the player's `COSMIC_SHORE_POST_OFF`
  (`PostPass.cs`; empty in play). Drop the field once a golden is recorded with post.
- **The quality preset is pinned.** A machine with no `gfx.*` prefs auto-detects; the cloud picked Low, which
  bakes the sky into the static panorama and turns HDR off. `record` writes `PINNED_PREFS` (High, MSAA 4,
  render scale 100, no adaptive, no tier override) into the parity profile before every run.
- **Scene Color.** The project leaves the opaque texture off (`URP_Asset` `m_RequireOpaqueTexture: 0`), so
  Unity's Scene Color node samples the unset-texture grey; Amoebius returned black. It now returns
  `sg_SceneColor` = 0.214 linear (`GraphProgramCache.SceneColorFallback`; a sweep of 0.12 / 0.17 / 0.21 / 0.35 /
  0.50 put the minimum there).
- **The destruction's fragments.** Close to the camera a prism dies as its detail level (`PrismInteractionLod`): the
  cut drawn by `CosmicShore/PrismCollisionBurst`, which Amoebius had no translation for, so every fragment stood at
  rest and the grid looked unbroken. `SceneRenderer` family 12 now carries the FRAGMENT kind line for line
  (`PrismBurstFragmentPose`, TEXCOORD2 bound at location 10); the roll, the suction and the lab kinds still draw at rest.
- **The shell's sphere.** The blast shell (RippleGraph) pushes each vertex out by noise of its position, so Unity's
  UV sphere gives it a spikier edge than Amoebius's icosphere. A 24x16 UV sphere matched the look but scored worse,
  since the spikes land on different vertices. Unity's exact built-in Sphere (439 vertices, exported from 6000.3.17f1
  on Gbox, branch `claude/unity-builtin-meshes-handoff`) scored worse too, 0.0291 against the lossless golden: the
  spike height is a legacy-sine hash of the vertex position times 500 (arguments near 20,000), so a GPU's `sin`
  precision decides it and no mesh can land the same spikes. The icosphere stays.
- **Against the lossless golden (2026-11-01).** The PNG frames were recorded again on Gbox (same commit, same
  settings) after the first set was lost, and scored without the H.264 decode: **loss 0.0278** (rmse 0.0157, ssim
  0.976) for the current engine, against 0.0329 before the fragments were drawn and 0.0302 with the UV sphere.
  The first 100 frames sit at rmse 0.005; frames 200-300 at 0.032 carry almost all of what is left, 89% of it inside
  the blast, mostly the debris streaks. The two clocks agree (Amoebius's take frame 0 is at 0.52 s, Unity's near
  0.47 s), so the shell's texture phase is not the gap.
- **The blast's start frame (2026-11-01).** The debris was a frame young: probing its age in the shader, one frame
  older scored 0.0206, two frames 0.0278. The cause was the compat `UniTask.Delay(DeltaTime)`, which waited for
  the clock to pass "now + delay": the float clock's rounding made 0.2 s take 13 frames at 60 Hz from about
  Time.time 0.4 on, so `AOEExplosion`'s `ExplosionDelay` started every blast a frame after Unity's. It now adds
  each frame's delta time from the frame after the call, as upstream's `DelayPromise` does, including its skip
  of the creation frame (the engine scheduler runs after Update, so a delay made in Update would otherwise count
  its own frame and land a frame early, which scored 0.0273). Two tests in `GameLoopAndTasksTests` hold both
  edges and fail without the fix. Shell and debris now start on Unity's frame: loss 0.0199.
- **What is left** is the shell, which is greener, more opaque and more jagged in Unity, and the debris's
  sub-pixel detail. The worst frames now sit near rmse 0.021, against 0.042 before the start frame was fixed.

### The flight take (2026-11-01)

The second take, `flight`: four Manta hulls (Jade, Ruby, Blue, Gold) fly closed-form paths of the frame index
through each other's prism trails, laid every two frames through `PrismTrailBuilder.LayOne`, and the camera chases
the Jade hull from take frame 0 (`Assets/_Scripts/Utility/Parity/RenderParityFlight.cs`; the scene
`Assets/_Scenes/Game_TestDesign/RenderParityFlight.unity`, written by `author_render_parity_scene.py`). It is the
footage highlight reels are cut from. Unity records it with `FrogletTools > Amoebius > Render Parity: Record Flight
Take`; the golden (`goldens/render/flight/`, MSAA 1, no post: `take.json` carries `"post": "off"`) is on branch
`claude/parity-flight-golden`, its lossless PNGs on Gbox. Scores on Gbox, lossless, post off:

| Change | flight loss | rmse | ssim | control loss |
|---|---|---|---|---|
| first score | 0.0373 | 0.0283 | 0.982 | 0.0199 |
| FBX submeshes ordered by the polygons' first use of each material, as Unity does | **0.0265** | 0.0207 | 0.988 | 0.0198 |

- **Submesh order.** Unity orders a model's submeshes by the order its polygons first use each material, not by
  material index. The Manta's polygons use `BASE` first, so Unity's submesh 0 is the body and the prefab's slot 0
  material (GreenAccent) paints it cyan; Amoebius sorted by index and painted it BlueBase grey, the take's two worst
  tiles (0.081, now under 0.008). Most of the fleet's FBXs use a material other than 0 first, so this repaints
  most hulls in Amoebius (`FbxModelImporter`, held by `ModelPrefabTests`).
- **What is left** is spread evenly (worst tiles 0.0077-0.0084): one-pixel differences along the trails' edges at
  MSAA 1, where the far ends of the trails are under a pixel wide. The worst frames, 141-145, are where the big
  orange trail sweeps through.

