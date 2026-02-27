# Bootstrap Scene Audit

## Summary

Audit of all root GameObjects in `Bootstrap.unity`, mapping every script's execution order (Awake → OnEnable → Start → async) and identifying bugs, anti-patterns, and optimization opportunities.

**Original branch**: `claude/scan-bootstrap-scripts-CYLk8`
**Original commit**: `f3b503f2` — `fix(bootstrap): fix bugs and optimize bootstrap scene startup flow`

---

## Refactors Applied

### BootstrapController merged into AppManager

`BootstrapController` was a separate MonoBehaviour (`[DefaultExecutionOrder(-100)]`) responsible for persistent root setup, platform configuration, `IBootstrapService` initialization, splash fade, and scene transition. `AppManager` (`[DefaultExecutionOrder(0)]`) handled DI registration, manager resolution, and service startup.

These two classes shared the same lifecycle (bootstrap scene, build index 0) with no cross-references, and no `IBootstrapService` implementations existed. The execution-order dance (-100 vs 0) added fragility without benefit. All BootstrapController logic now lives in AppManager at `[DefaultExecutionOrder(-100)]`, eliminating one MonoBehaviour and one GameObject from the bootstrap scene.

**Migration note**: In the Bootstrap scene, the serialized fields from the old BootstrapController GameObject (BootstrapConfigSO, persistent root, splash CanvasGroup) need to be wired to AppManager's new fields in the Unity Inspector. The old BootstrapController GameObject can be removed.

---

## Fixes Applied (7 files, original audit)

| # | File | Issue | Fix |
|---|---|---|---|
| 1 | `SceneLoader.cs` | Extended `MonoBehaviour` but used `[ServerRpc]` (requires `NetworkBehaviour`) | Changed base class to `NetworkBehaviour` |
| 2 | `BootstrapController.cs` | Unused `using DG.Tweening` | Removed (file now deleted — merged into AppManager) |
| 3 | `GameSetting.cs` | `PlayerPrefs.Save()` in `Awake()` — sync disk I/O blocking bootstrap | Removed (in-memory reads work immediately without explicit Save) |
| 4 | `Singleton.cs` | `print()` calls (unfiltered, GC-heavy); no app-quit guard on `Destroy()` | Replaced with `CSDebug.Log`; added `ApplicationLifecycleManager.IsQuitting` guard |
| 5 | `CameraManager.cs` | `Invoke("LookAtCrystal", 1f)` — reflection-based, fragile, not cancellable | Replaced with cancellable `UniTask.Delay` + `CancellationTokenSource` |
| 6 | `CaptainManager.cs` | `OnDisable` unsubscribed from events never subscribed in `OnEnable`; line 72 used `+=` instead of `-=` (subscribing during cleanup) | Cleared body to match empty `OnEnable` |
| 7 | `AppManager.cs` | `ResolvePersistentSystems()` ran twice (Awake + InstallBindings), 6× `FindFirstObjectByType` each time | Added `_resolved` guard flag |

---

## Known Issues — NOT Fixed (Deferred)

These require larger cross-cutting refactors and are documented here for future work.

### 1. GameSetting uses static C# events instead of SOAP ScriptableEvent

**Violates**: SOAP architecture pattern (CLAUDE.md anti-pattern: "Do not use singletons or static events for cross-system communication")
**Impact**: `AudioSystem`, `Jukebox`, and all consumers subscribe to static events on `GameSetting`
**Effort**: Medium-large — requires creating SOAP `ScriptableEvent` assets, rewiring all subscribers, testing audio pipeline
**Risk**: Audio regressions if subscribers are missed during migration

### 2. CM DeathCam has 3 duplicate CustomCameraController components

**Type**: Scene file issue
**Impact**: Redundant component overhead, potential conflicts in camera behavior
**Fix**: Open `Bootstrap.unity` in Unity Editor, select CM DeathCam, remove duplicate `CustomCameraController` components (keep one)
**Note**: Cannot be safely fixed via scene file text editing — use the Editor

### 3. Missing prism pools for Urchin, Grizzly, Termite, Falcon, Shrike

**Type**: Feature gap, not a bug
**Context**: Prism pools exist for Manta, Dolphin, Rhino — vessels that are feature-complete. Missing pools correspond to vessels still in development or planned.
**Fix**: Create pool entries when those vessel classes reach playable state

### 4. Singleton base classes use singleton pattern

**Violates**: CLAUDE.md anti-pattern favoring SOAP over singletons
**Impact**: `GameSetting`, `ThemeManager`, `CameraManager`, `CaptainManager` inherit from `Singleton<T>`
**Effort**: Large — full migration to SOAP `ScriptableVariable` / DI container, touching most gameplay systems
**Risk**: High — singletons are load-bearing throughout the codebase; incremental migration recommended

---

## Bootstrap Execution Order

### Phase 0: Static Initialization

```
[RuntimeInitializeOnLoadMethod] → AppManager static setup (reset _hasBootstrapped)
```

### Phase 1: Awake() — ordered by [DefaultExecutionOrder]

```
-100  AppManager                 DontDestroyOnLoad, platform config, manager resolution
 -50  SceneTransitionManager    Fade overlay setup, ServiceLocator registration
  -1  AudioSystem               Audio middleware initialization
   0  All others                ThemeManager, CameraManager, GameSetting, CaptainManager, etc.
```

### Phase 2: Start()

```
AppManager.Start()  → ConfigureGameData, StartNetworkMonitor, StartAuthentication, RunBootstrapAsync().Forget()
```

### Phase 3: Async Bootstrap

```
AppManager.RunBootstrapAsync()
  → Yield frame (let all Awake() complete)
  → Yield frame (let all Start() settle)
  → Enforce minimum splash duration
  → Splash screen fade (CanvasGroup)
  → Load "Authentication" scene via SceneTransitionManager (or direct SceneManager fallback)
```

### Phase 4: Scene Flow

```
Authentication scene → auth flow completes → Menu_Main scene
```

---

## Bootstrap Scene GameObject Map

| GameObject | Key Scripts | Notes |
|---|---|---|
| AppManager | `AppManager` | Top-level orchestrator + DI root, `[DefaultExecutionOrder(-100)]` |
| SceneTransitionManager | `SceneTransitionManager` | Fade overlay, `[DefaultExecutionOrder(-50)]` |
| AudioSystem | `AudioSystem` | Wwise integration, `[DefaultExecutionOrder(-1)]` |
| GameSetting | `GameSetting` | PlayerPrefs wrapper, static events (see deferred issue #1) |
| ThemeManager | `ThemeManager` | Visual theme management |
| CameraManager | `CameraManager` | Camera lifecycle, LookAtCrystal |
| CaptainManager | `CaptainManager` | Captain data management |
| CM DeathCam | `CustomCameraController` (×3) | Has duplicate components (see deferred issue #2) |
| PrismPools | Pool configurations | Manta, Dolphin, Rhino pools present |
| EventSystem | Unity EventSystem | UI input |
| Canvas | UI Canvas | Bootstrap UI elements |
| SplashScreen | Splash visual | Fade-out during async bootstrap |
| DirectionalLight | Light | Scene lighting |
| Camera | Main Camera | Bootstrap camera |
| SceneLoader | `SceneLoader` | Network scene loading (extends NetworkBehaviour) |

---

## File Reference

Key bootstrap files and their locations:

```
Assets/_Scripts/System/Bootstrap/
├── BootstrapConfigSO.cs
├── ServiceLocator.cs
├── SceneTransitionManager.cs
├── ApplicationLifecycleManager.cs
├── BOOTSTRAP_AUDIT.md              ← this file
└── Tests/
    ├── BootstrapControllerTests.cs  (renamed class: AppManagerBootstrapTests)
    ├── ServiceLocatorTests.cs
    ├── SceneTransitionManagerTests.cs
    ├── ApplicationLifecycleManagerTests.cs
    └── SceneFlowIntegrationTests.cs
```

Related files outside the Bootstrap directory:

```
Assets/_Scripts/System/AppManager.cs         ← top-level orchestrator + DI root
Assets/_Scripts/System/SceneLoader.cs        ← persistent scene loading (NetworkBehaviour)
Assets/_Scripts/Utility/DataContainers/SceneNameListSO.cs  ← centralized scene names
Assets/_Scripts/Controller/Settings/GameSetting.cs
Assets/_Scripts/Controller/Managers/CameraManager.cs
Assets/_Scripts/Controller/Managers/CaptainManager.cs
Assets/_Scripts/Controller/Managers/ThemeManager.cs
Assets/_Scripts/Utility/Singleton.cs
```
