# Vessel Test Range — plan and recommendation

**The ask (the user, 2026-10-09).** Test any vessel, its AI, its flight and its look in one place:
first as a quick prototype in the web studios, then in the **real game**, with real prisms, flora,
fauna, shards and background. The AI flies Squirrels and Stoats while you watch from a free-roam
camera. You pick the Skim Race intensity map (1–4). A switch goes between a light "block" look for
weak devices (mobile web, PC web, low phones) and the full game graphics.

**Recommendation in one line.** Keep the **web studios** for fast design. Add **one dev-only harness**
that drops into the game's **real mode scenes**. Do not build a new environment scene. Open it the
same way from Unity, from Amoebius on Windows, and later from Amoebius on Android.

Status: **plan only**. Nothing in §3–§5 is built yet. Every "exists" below was read on 2026-10-09
(branch `vessel-studio`).

---

## 1. Three tiers, one catalog

| Tier | What flies | Graphics | Runs on | Use it for | State |
|---|---|---|---|---|---|
| **A. Web studio** | A JavaScript copy of the vessel and its AI, built from the shipped numbers | Blocks (three.js) | Any browser: PC, Android, iPhone; Amoebius opens it as its own window | Design: try a mechanic, a play style or a difficulty in minutes | **Exists**: Squirrel (round 2 AI lab), Stoat (round 14) in `Docs/Studios/VesselStudio/` |
| **B. Test Range in Amoebius** | The game's own vessel, AI and mode, from this repo's `Assets/` | The game's, at the chosen device tier | Amoebius on Windows now; Amoebius player on Android later (`Port/docs/milestones.json` C8) | Checking what is built, on a phone-class budget, without Unity | PLAY IN ENGINE **exists** (`--arcade MODE`); the harness is §3 |
| **C. Test Range in Unity** | The same harness, the same scenes | Full, plus every Unity instrument (Profiler, Frame Debugger, `diag`, `prof`) | Unity Editor; development builds | Diagnosing: profiling, AI debugging, art review | §3 |

**B and C are the same work.** Amoebius runs the game's own Unity content unchanged. So a harness
written once under `Assets/` works in the Editor, in a development player, and in Amoebius. That is
why the Test Range is Unity content and not an Amoebius page.

The catalog (`Docs/Studios/VesselStudio/studios.json`) stays the single list. Each vessel gets its
web page (A), its `engineMode` (B, exists), and **range presets** (B and C, §4).

## 2. What the game already has (reused, not rebuilt)

| Need | Already in the repo | Where |
|---|---|---|
| The real environment (cell, prisms, flora, fauna, shards, background) | Every mode scene builds its own cell at the chosen intensity | `Assets/_Scenes/Multiplayer Scenes/*.unity`, `Assets/_SO_Assets/Cell Configs/` |
| The four Squirrel race maps | `MinigameSkimRace.unity`, intensity 1–4 (four tracks, the same data the web studio reads) | `Docs/SKIM_RACE_AI.md`, `Tools/Build/skimrace_track_fingerprint.py` |
| Other Squirrel maps | Arcade cards that fly the Squirrel: **AstroLeague, Broadside, BroodRush, Joust, Maelstrom, Regatta, Scurry, SkimRace** | `Assets/_SO_Assets/Games/ArcadeGame*.asset` (Vessels = `SO_Class_Squirrel`) |
| Stoat map | **Slingshot** (the only card with `SO_Class_Stoat`) | `ArcadeGameSlingshot.asset` |
| AI on every seat | `AIPilot` (platform autopilot, incl. the Stoat's autopilot sling); `SkimRacePilot` / `SkimRaceDriver` for Squirrel in Skim Race and Regatta; Easy/Medium/Hard via `SkimRaceDifficultySO` | `Assets/_Scripts/Controller/AI/`, `Docs/AI_SYSTEM/` |
| AI flying **your own** hull | `Vessel.ToggleAIPilot(true/false)` (Mode Preview uses it: "plays under AI, tap to take the stick") | `ModePreviewSession.cs` |
| AI seat counts and difficulty | `GameDataSO.ConfigurePlayerCounts`, the launch panel's difficulty row | `BenchmarkSceneLauncher.cs` shows the whole launch path in 15 lines |
| Watch another pilot | `SpectatorController` re-points the follow rig at any vessel, plus a slow **dolly** orbit, and moves the occlusion corridor and vision shading onto the watched hull | `Controller/Multiplayer/SpectatorController.cs`, `Docs/PartySystem/SPECTATOR.md` |
| A free camera for test scenes | `MouseOrbitCamera`: pan, orbit, zoom about a pivot, WASD, runs on unscaled time; config in an SO | `Controller/Camera/MouseOrbitCamera.cs` (used by `BlackHoleTestHarness`) |
| A camera that does not disturb gameplay | `CameraManager.BeginManualReplayCamera()` (AstroLeague replays, spectator dolly) | `Controller/Managers/CameraManager.cs` |
| A test-rig pattern | `BlackHoleTestHarness`: dev-only (`#if DEVELOPMENT_BUILD \|\| UNITY_EDITOR`), a `DiagnosticsHUD` section, a console command, a setup tool | `Utility/Tools/BlackHoleTestHarness.cs`, `Docs/BLACK_HOLE.md` §7 |
| Weak-device simulation | `DeviceTier` (Desktop / MobileHigh / MobileLow), simulated from **FrogletTools ▸ Performance ▸ Device Tier** | `System/Platform/DeviceTierClassifier.cs`, `Editor/DeviceTierWindow.cs` |
| A cheap look at a whole cell | `CellMiniatureBuilder`: the cell as one mesh, no prisms (Mode Preview's scale model) | `Docs/ModePreview/ARCHITECTURE.md` §1.1 |
| Time scale for AI runs | The training runner scales `Time.timeScale` and restores it | `Utility/AITraining/Runner/TrainingAutoLauncher.cs` |
| Engine-side launch | `--arcade MODE` opens a card and presses Start | `Port/src/CosmicShore.Player/ArcadeAutoStart.cs` |

**What is missing:** a free-**fly** camera (MouseOrbitCamera orbits a pivot and cannot fly along a
course), one place to set seats, hulls and difficulty without going through the menu, a pilot board,
time controls, and a graphics-fidelity switch.

## 3. The Test Range harness (to build)

`VesselTestRange` is a dev-only `MonoBehaviour`. Like `BlackHoleTestHarness` it compiles only in
the Editor and development builds, so it never ships (`Docs/CONDITIONAL_COMPILATION.md`, and
`check_conditional_compilation.py` first). It installs into **any** gameplay scene after the mode has
started, so every real map is a test range with no copied scenes.

| Feature | How | Reuses |
|---|---|---|
| **Seats** | N seats: hull per seat (Squirrel, Stoat, any class) and difficulty per seat (Skim Race: Easy/Medium/Hard; elsewhere the platform autopilot). Your own seat can be AI-flown. | `ConfigurePlayerCounts`, `ToggleAIPilot`, `SkimRaceDifficultySO` |
| **Map** | The mode scene plus intensity 1–4. Squirrel presets: Skim Race I1–I4. Stoat: Slingshot I1–I4. | the launch path (`gameData.SceneName`, `SelectedIntensity`, `InvokeGameLaunch`) |
| **Cameras** | **Chase** (your hull, the game's own camera), **Follow** (any pilot, the spectator re-point), **Dolly** (the spectator orbit), **Free fly** (new: WASD + mouse look, Q/E down/up, Shift fast, gamepad sticks; on the manual replay rig; holds the occlusion corridor like `ScreenshotDirector`). C or a pad button cycles them. | `SpectatorController`, `CameraManager.BeginManualReplayCamera`, `MouseOrbitCamera` config style |
| **Time** | 0.25× / 0.5× / 1× / 2× / 4×, pause, single-step. AI is input-only, so it scales cleanly. | `TrainingAutoLauncher`'s save/restore, `PauseSystem` |
| **Pilot board** | A `DiagnosticsHUD` "Range" section, one row per seat: hull, difficulty, speed, boost, crystals or score, AI state ("not noticed" / "misjudged" for the handicap), resets. The same columns as the web studio, so A and B/C read alike. | `DiagnosticsHUD`, `SkimRaceDriver` debug state |
| **AI thinking** | Gizmo lines from each AI to its believed and real target (the web studio's "Show AI thinking"). Editor and dev builds only. | `SkimRaceHandicap` |
| **Fidelity switch** | **Full** = shipped. **Mobile-low** = simulate `DeviceTier.MobileLow` (the real reduced profile). **Block** = the cell's ecology OFF at spawn (no flora, fauna, shards or background dressing; prisms and track only) through a runtime override on the cell's copy, never the shared SO. **Model** = the `CellMiniatureBuilder` scale model for a whole-map overview. | `DeviceTierWindow`, cell config, `CellMiniatureBuilder` |
| **Console** | `range` command: `range squirrel skimrace 3 hard x3`, `range cam free`, `range speed 4`, `range look block`. Amoebius and CI drive it the same way. | the console pattern of `bhtest` / `blackhole` |
| **Logs** | One `CSLogChannel.VesselTestRange` channel (`LogVerbose`), off by default. | `CSDebug` |

**Rules it must keep** (CLAUDE.md):
- The AI writes inputs only.
- No runtime write to a shared ScriptableObject.
- Warnings and errors stay loud; nothing is logged per frame.
- Menus live under `FrogletTools/` only.
- Any asset the setup tool writes is recorded in `FrogletToolChangeLedger` and shipped with `FrogletToolShipPanel`.
- `/verify-unity` passes before every commit.

## 4. Opening it: one click from each place

| From | How |
|---|---|
| **Unity** | **FrogletTools ▸ Vessels ▸ Vessel Test Range**: a window with the presets from `studios.json` (vessel, map, intensity, seats, difficulty, camera, look). **Play** enters Play mode through the normal boot, then applies the preset. |
| **Amoebius (Windows, later Android)** | STUDIOS ▸ a vessel card ▸ **TEST RANGE** (next to PLAY IN ENGINE). It starts the game with `--range <preset>`, the same way `--arcade` works today. |
| **Web studio** | A "Try this in the game" line naming the matching preset, so a design decided in A is checked in B or C with the same settings. |

Each vessel's presets live in its `studios.json` entry:

```json
"ranges": [
  { "name": "Skim Race I3, 3 Hard rivals", "mode": "SkimRace", "intensity": 3,
    "seats": [ { "hull": "Squirrel", "ai": "Hard" }, { "hull": "Squirrel", "ai": "Hard", "count": 3 } ],
    "camera": "Free", "look": "Full" }
]
```

## 5. Phases

| Phase | Work | Done when |
|---|---|---|
| R0 | This plan; the Squirrel web studio round 2 (AI lab, the same four maps) | **done 2026-10-09** |
| R1 | Free-fly camera on the manual replay rig, plus the `range cam` / `range speed` commands, in any running mode | Unity: fly freely around a live Skim Race I3 with 3 AI and time at 4×; `/verify-unity` green; an edit-mode test for the camera math |
| R2 | Seats and presets: `range <vessel> <mode> <intensity> <difficulty> xN`, your hull on AI, the pilot board | Unity: "Squirrel Skim Race I1–I4, Hard ×4" and "Stoat Slingshot I2 ×3" each run from one command with the board filled |
| R3 | Fidelity switch (Full / Mobile-low / Block / Model) | The same race at each look; `diag` frame times recorded per look in this doc |
| R4 | Unity window and `studios.json` `ranges` | One click from FrogletTools; presets read from the catalog |
| R5 | Amoebius: STUDIOS ▸ **TEST RANGE** and `--range` | Amoebius on Windows runs a preset end to end (screenshot) |
| R6 | Android: the Amoebius player APK runs a preset on a phone | After C8 (first device run) |

R1 alone already answers "watch the AI skim through with a free camera". Each later phase is useful
by itself.

## 6. Decisions for the user

1. **"Block" look in the game**: ecology off (prisms and track only) as proposed, or a flat-shaded prism material as well?
   The second is a shader change and needs `/verify-unity` plus a render check.
2. **Stoat in the game**: the game ships the round-4 orbit sling. The web studio's dipole sling is not
   built yet. Should the Test Range test the shipped Stoat now, or wait for the new sling?
3. **Which branch builds R1–R3**: `Ys-bleeding-edge`, the home of all AI work since 2026-10-09 (`Docs/AI_SYSTEM/BRANCH_WORKFLOW.md`). Decided by the user's move off `ai-system`.
