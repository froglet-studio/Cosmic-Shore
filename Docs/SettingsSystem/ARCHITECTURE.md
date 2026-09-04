# Settings System + Benchmark Scene

PC/Steam-style settings panel (8 tabs) plus a runtime benchmark/stress-test scene reachable from
the panel. Designed around the fact that **Cosmic Shore is CPU-bound** — the GPU mostly idles, so
GPU eye-candy (MSAA, supersampling) is nearly free here, and the settings that actually move
framerate are the CPU/simulation ones.

> **Headless note:** the C# layer (engine, stores, controllers, benchmark scripts, editor tool) is
> committed. The Unity-side wiring — building the settings panel prefab, hooking controls to the
> controller methods, and finishing the cloned benchmark scene — is the editor checklist at the
> bottom (it could not be done from a headless environment).

## Why a "Performance" (CPU) tab

In most games the GPU is the bottleneck, so the graphics menu is where you reclaim FPS. Here the
CPU is the bottleneck (thousands of prisms, fauna AI, colliders, trail sim). Turning graphics down
barely helps; the **Performance tab** holds the knobs that genuinely raise framerate — ecosystem
density, AI crowd size, adaptive animation, collider detail, VFX density. None of them delete mass
(they tune *production*, not decay), so the conserved-mass law (CLAUDE.md ▸ Don't cheat emergence)
is respected.

## Tabs → store routing (shipped 4-tab layout)

| Tab | Settings | Store / method |
|---|---|---|
| **GENERAL** | Colorblind mode · Subtitles · Subtitle scale · Analytics+crash consent · Bug Report / Privacy Policy / Delete My Data · Version | `AccessibilitySettings`, `AnalyticsServiceFacade.SetConsent`, `Application.OpenURL` |
| **DISPLAY** | Display mode · Resolution · Frame Rate Limit · V-Sync · Field of View | `DisplayGraphicsSettings` (device-local) |
| **PERFORMANCE** | Quality preset · Anti-aliasing · Texture quality · Upscaling · Adaptive performance · Physics detail · Auto-Detect · Benchmark | `DisplayGraphicsSettings` |
| **OTHER** | Invert Y · Invert Throttle · Music+vol · SFX+vol · Haptics+vol | `GameSetting` (cloud-roaming) |

**Self-wiring:** `GameSettingsPanelController` exposes a serialized slot per control — drag each in and
it populates dropdown options (from the enums, so order can't drift), sets saved values, and attaches
listeners on `Start` (and refreshes on reopen). No per-control UnityEvent wiring or init. ON/OFF rows
are a separate ON + OFF button pair (`OnOffControl.onButton`/`offButton`) — drag both in; the controller
wires ON→Set(true)/OFF→Set(false) and tints the selected button's label (`selectedColor`/`unselectedColor`).
Backend fields NOT surfaced in this 4-tab UI
(still used by auto-detect / benchmark): render scale, monitor, refresh rate, ecosystem density, AI
crowd size, VFX density, camera shake, reduce-flashing.

**Device-local vs roaming:** display/graphics/performance are per-device and intentionally do NOT
cloud-sync (a phone and a PC must not share a resolution). They live in `DisplayGraphicsSettings`
(PlayerPrefs only). Audio/input keep cloud-roaming via the existing `GameSetting`/`PlayerSettingsCloudData`.

## Files

| Role | File |
|---|---|
| Enums (display/graphics/perf/a11y) | `_Scripts/Data/Enums/GraphicsSettingsEnums.cs` |
| Device-local settings data | `_Scripts/Controller/Settings/GraphicsSettingsData.cs` |
| Engine applier (QualitySettings/Screen/URP/camera) | `_Scripts/Controller/Settings/GraphicsSettingsApplier.cs` |
| Device-local manager (PlayerPrefs + apply + events) | `_Scripts/Controller/Settings/DisplayGraphicsSettings.cs` |
| Auto-detect heuristic (SystemInfo, CPU-weighted) | `_Scripts/Controller/Settings/SettingsAutoDetector.cs` |
| Accessibility store | `_Scripts/Controller/Settings/AccessibilitySettings.cs` |
| Benchmark scene launcher (Settings button) | `_Scripts/Controller/Settings/BenchmarkSceneLauncher.cs` |
| Endless sandbox controller | `_Scripts/Controller/Arcade/SandboxBenchmarkController.cs` |
| Settings panel controller (binds canvas → backend) | `_Scripts/UI/Modals/GameSettingsPanelController.cs` |
| Tab navigation (content + underline + scale) | `_Scripts/UI/Modals/SettingsTabBar.cs` |
| In-scene benchmark HUD controller | `_Scripts/UI/BenchmarkSceneHud.cs` |
| Camera FOV + post-AA consumer (drop-on) | `_Scripts/Controller/Camera/CameraSettingsApplier.cs` |

> The binding **scripts** are provided; the **visuals** (canvas, images, layout) are the UI author's.
> Build the window/HUD, drop the controller on its root, and wire each control's event to a controller
> method (the API contract below). Tabs are a visual grouping only — the controller is tab-agnostic,
> so rearranging settings between tabs needs no script change.

## API contract for the UI

Bind each control's event to these. All settings persist + apply + raise a change event on call.

**Display / Graphics / Performance** — `DisplayGraphicsSettings.Instance` (auto-created, never null
after first frame):
`SetDisplayMode(DisplayModeSetting)`, `SetResolution(w,h,refreshHz)`, `SetVSync(VSyncSetting)`,
`SetTargetFrameRate(int)` (≤0 = uncapped), `SetFieldOfView(float)`, `SetQualityPreset(QualityPresetSetting)`,
`SetAntiAliasing(AntiAliasingSetting)`, `SetRenderScalePercent(int)`, `SetUpscaling(UpscalingSetting)`,
`SetTextureQuality(int 0..3)`, `SetAdaptivePerformance(...)`, `SetEcosystemDensity(...)`,
`SetPhysicsDetail(...)`, `SetAiCrowdSize(int)`, `SetVfxDensityPercent(int)`, `ApplyAutoDetect()`,
`ResetToDefaults()`. Read current state via `.Current` (a `GraphicsSettingsData`). React via the
static events (`OnAnySettingChanged`, `OnEcosystemDensityChanged`, `OnFieldOfViewChanged`, …).

**Accessibility** — `AccessibilitySettings` (static): `ColorblindMode`, `Subtitles`, `SubtitleScale`,
`ReduceFlashing`, `CameraShakeIntensity` (each a property with a matching `On…Changed` event).

**Audio / Controls** — existing `GameSetting` (`[Inject]`): `ChangeMusicEnabledSetting()`,
`SetMusicLevel(float)`, `SetSFXLevel`, `SetHapticsLevel`, `ChangeSFX/Haptics/InvertY/InvertThrottle/JoystickVisualsStatus`.

**General** — `AnalyticsServiceFacade.SetConsent(bool)` (`[Inject]`; read `ConsentGranted`);
legal/support/delete-data via `Application.OpenURL(...)`.

**Benchmark** — hook the button to `BenchmarkSceneLauncher.LaunchBenchmark()`. In the scene, drive
the formal run with `PerformanceBenchmarkRunner` (`Configure(config, gameDataContainer: gameData)` →
`StartBenchmark()` → poll `IsRunning`/`Progress`/`LastReportPath`).
| Existing measurement layer (reused) | `_Scripts/Utility/PerformanceBenchmark/*` (author's tool) |

## Auto-Detect (Both: heuristic + offer benchmark)

`SettingsAutoDetector` gives an instant SystemInfo guess (CPU cores weighted heaviest, then RAM,
then VRAM) → a `QualityPresetSetting` + sensible display/CPU defaults. For accuracy the player runs
the in-scene **Benchmark**, which measures real frame cost via the author's
`PerformanceBenchmarkRunner` and saves a full report.

## Benchmark scene

- **The scene:** `BenchmarkStressTest.unity` (Singleplayer Scenes) is committed to the repo and
  registered in Build Settings — there is exactly ONE and it is never re-created (the one-shot
  creation tool has been removed). It was originally cloned from `MinigameWildlifeBlitz`
  (preserving NetworkObjects, ContainerScope, Cell + RandomLifeSpawner, crystal manager, camera);
  its Cell now runs the menu's Blob Cell Config on all four intensity slots.
- **Launch:** Settings → Run Benchmark calls `BenchmarkSceneLauncher.LaunchBenchmark()` → sets
  `GameDataSO` (Squirrel, single-player, WildlifeBlitz mode) → `InvokeGameLaunch()` → the always-on
  host loads it via Netcode scene management (the Relay just idles for a single-player scene).
- **Endless + flyable:** `SandboxBenchmarkController` (`HasEndGame=false`) auto-activates players;
  the human flies a Squirrel, AI Squirrels fly via the same `StartPlayer→ToggleAIPilot` path
  WildlifeBlitz uses. No win condition (no TurnMonitor).
- **Gradual spawn (no single-frame spike):** the existing `RandomLifeSpawner` already frame-spreads
  spawns via `yield return null` / `WaitForSeconds` (it has a comment about fixing a ~48% spike that
  way). A high-density benchmark `SpawnProfileSO` raises counts/lowers intervals — **production only,
  no decay/TTL** (conserved-mass law).
- **In-scene HUD:** `BenchmarkSceneHud` drives the live FPS / frame-time / 1%-low readout, quick-toggle
  graphics (quality, AA, VSync, frame cap, ecosystem density, auto-detect) so players "see which is
  best," a Run button (formal `PerformanceBenchmarkRunner` report), and Exit. You build its visuals
  and wire the labels/buttons to it.

## Camera consumers — WIRED (automatic)

`CameraManager` applies **FOV** and per-camera post-process **AA (FXAA/SMAA/TAA)** to every camera it
manages (player / death / end) on each camera setup AND live as settings change — **no per-camera
reference needed**, so it survives the runtime-spawned vessel (the cameras are children of the
manager; the vessel is just the follow target). MSAA is global on the URP asset.

`CameraSettingsApplier` remains an optional drop-on for any camera `CameraManager` does NOT own — e.g.
the menu Cinemachine brain camera if you want SMAA/TAA there, or the benchmark-scene camera. Turn
`applyFieldOfView` off on Cinemachine cameras (the vCam re-drives FOV each frame).

## Audio slider ranges

Music / SFX / Haptics volume sliders are **min 0, max 1, whole-numbers off, default 1.0**. The legacy
`AudioSource` path scales ×1/5 internally (the intentional "max .2" attenuation in `AudioSystem`), and
the FMOD path is `AudioVolumeMath.InstanceVolume` (mute → 0, else slider × per-emitter trim) — both
assume a 0–1 slider.

**Persistence is last-writer-wins between PlayerPrefs and the cloud** (`GameSetting.ShouldApplyCloud`,
stamped by `PlayerPrefKeys.SettingsModifiedUtc` ↔ `PlayerSettingsCloudData.ModifiedUtcTicks`). The
cloud snapshot used to be applied unconditionally on every launch, which is why a slider set to 0
came back at 1.0. The level keys are FLOATS — seeding them with `SetInt` made every fresh install read
0 (silent). A slider prefab talks to `GameSetting` only (`AudioLevelSlider`; the old `Mixer` wrote to
an FMOD VCA that controls no bus). Record: `Docs/AudioSystem/FMOD_AUDIT.md`.

## In-game restrictions (real-game pattern)

Opened **in the main menu**, everything is editable. Opened **in-game** (any state ≠ MainMenu, via
`ApplicationStateDataVariable`), the panel locks the **Performance** controls (Quality, AA, Texture,
Upscaling, Adaptive, Physics) + **Auto-Detect** + **Benchmark** (`interactable = false`) and shows the
optional `menuOnlyHint`. Live-safe settings (audio, controls, FOV, VSync, frame cap, accessibility)
stay editable everywhere. Renderer-level changes (Quality/AA/Texture/Upscaling) and Auto-Detect raise
the optional `restartRequiredNotice` ("some changes apply after a restart"), hidden again on open.
Auto-Detect also logs via `CSDebug`. Wire `menuOnlyHint` + `restartRequiredNotice` (both start hidden).

> Note: in Unity these renderer settings actually apply live; the restart notice is the AAA UX
> convention, not a technical necessity — drop the `FlagRestartNeeded()` calls if you'd rather not show it.

## Consumer wiring still TODO (values persist + raise events today)

These settings are stored and broadcast change events; the systems that *read* them need a small
hook (each is its own follow-up, some are ecology-sensitive — use the `/ecology` skill):

- **Colorblind / Reduce-flashing** → a screen-space color/flash post-process.
- **Subtitles / scale** → Dialogue System (`DialogueManager`) subtitle rendering.
- **Ecosystem density / physics detail / AI crowd / VFX density** → spawn profile scaling, collider
  LOD, AI count, explosion-VFX budget (ecology-sensitive: tune production, never add decay).
- **Adaptive performance** → `AdaptiveAnimationManager` aggressiveness.

## Deferred (per product decisions)

- **Live diagnostics readout** (CPU/RAM/VRAM/GPU + CPU-vs-GPU verdict) in Advanced — later update;
  `DiagnosticsHUD` already collects all of it, extract a shared service when wanted.
- **FPS overlay** — dropped.
- **Frame generation** (DLSS3/FSR3) — would help a CPU-bound game, but needs a vendor plugin.
- **Shadows / bloom toggles, raytracing** — not exposed (shader-driven look; handled by preset).
- **In-app data deletion** — chose the web/email request form (no irreversible backend op ships).

## Editor wiring checklist (Unity-side)

1. **Settings panel UI (you design the visuals):** build the panel, drop `GameSettingsPanelController`
   on its root, and **drag each control into its serialized slot** (grouped by tab in the inspector).
   The controller self-wires — options, saved values, and listeners — so there's no per-control
   UnityEvent wiring, no option authoring, and no init step. ON/OFF rows have two slots — drag the ON
   button into `onButton` and the OFF button into `offButton` (tune `selectedColor`/`unselectedColor`
   for the highlight). Set the FOV slot's min/max, drag in `benchmarkLauncher`, and fill the URL fields.
   Assign `optionsMenuContent` and wire the settings modal's open event → `Open()` and close → `Close()`
   so the panel shows/hides with the modal.
2. **Benchmark button:** add `BenchmarkSceneLauncher` and hook the button → `LaunchBenchmark()`.
3. **Benchmark scene:** `BenchmarkStressTest.unity` already exists in the repo (one only — never
   re-create it). Wiring changes (Squirrel vessel on the spawner's AI entries, endless controller,
   spawn profile, `BenchmarkSceneHud`, the Exit event) are edited directly in that scene.
4. **If you map dropdown indices to enums**, match the order in `GraphicsSettingsEnums.cs`: Display
   mode = Fullscreen/Borderless/Windowed; Quality = Very Low..Ultra; AA =
   Off/FXAA/SMAA/MSAA2x/4x/8x/TAA; frame cap your own list (pass the int to `SetTargetFrameRate`).
