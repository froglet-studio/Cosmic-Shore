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

## Tabs → store routing

| Tab | Settings | Store |
|---|---|---|
| **General** | Analytics/crash/ads consent, Delete-my-data (web form), Report-a-bug (URL), Privacy/Terms, version | `AnalyticsServiceFacade.SetConsent`, `Application.OpenURL` |
| **Display** | Display mode, monitor/resolution, refresh, VSync, frame cap, **FOV** | `DisplayGraphicsSettings` (device-local) |
| **Graphics** | Quality preset, anti-aliasing, render scale, upscaling, texture quality, + Auto-Detect / Run Benchmark | `DisplayGraphicsSettings` |
| **Performance** | Adaptive performance, ecosystem density, physics/collider detail, AI crowd, VFX density | `DisplayGraphicsSettings` |
| **Audio** | Music / SFX / Haptics (existing) | `GameSetting` (cloud-roaming) |
| **Controls** | Invert Y / Throttle, Joystick visuals (existing) | `GameSetting` |
| **Accessibility** | Colorblind mode, subtitles + scale, reduce-flashing, camera shake | `AccessibilitySettings` |
| **Advanced** | Reset to defaults (live CPU/RAM/VRAM readout deferred to a later update) | `DisplayGraphicsSettings.ResetToDefaults` |

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
| Create-scene editor tool | `_Scripts/Editor/CreateBenchmarkSceneTool.cs` |
| **Settings panel UI + in-scene HUD** | **owned by the UI author — not in this branch** |

> The UI layer (the settings window prefab and the in-scene benchmark HUD) is intentionally NOT
> provided. This branch ships the **backend** the UI binds to; the API contract below is the surface
> to wire controls against.

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

- **Create it:** `Tools > Cosmic Shore > Create Benchmark Scene` clones `MinigameWildlifeBlitz`
  (preserving NetworkObjects, ContainerScope, Cell + RandomLifeSpawner, crystal manager, camera) →
  `BenchmarkStressTest.unity`, and registers it in Build Settings.
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
- **In-scene HUD (you build it):** show live FPS / frame-time / 1%-low, quick-toggle graphics
  (quality, AA, VSync, frame cap, ecosystem density, auto-detect) so players "see which is best,"
  a Run button (formal `PerformanceBenchmarkRunner` report), and Exit. It binds to the same API
  contract above.

## Consumer wiring still TODO (values persist + raise events today)

These settings are stored and broadcast change events; the systems that *read* them need a small
hook (each is its own follow-up, some are ecology-sensitive — use the `/ecology` skill):

- **FOV** → camera (`CameraSettingsSO` / `CustomCameraController`) via `OnFieldOfViewChanged`.
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

1. **Settings panel UI (you build it):** build the 8-tab window and wire each control to the API
   contract above (quality dropdown → `SetQualityPreset`, render-scale slider → `SetRenderScalePercent`,
   consent toggle → `SetConsent`, links → `Application.OpenURL`). If you build a resolution dropdown,
   populate it from `Screen.resolutions` and feed the choice to `SetResolution(w,h)`.
2. **Benchmark button:** add `BenchmarkSceneLauncher` and hook the button → `LaunchBenchmark()`.
3. **Benchmark scene:** run `Tools > Cosmic Shore > Create Benchmark Scene`, then follow the Console
   checklist (Squirrel vessel on the spawner's AI entries, endless controller or remove TurnMonitor,
   high-density `SpawnProfileSO`, add your in-scene HUD canvas, wire the Exit event).
4. **If you map dropdown indices to enums**, match the order in `GraphicsSettingsEnums.cs`: Display
   mode = Fullscreen/Borderless/Windowed; Quality = Very Low..Ultra; AA =
   Off/FXAA/SMAA/MSAA2x/4x/8x/TAA; frame cap your own list (pass the int to `SetTargetFrameRate`).
