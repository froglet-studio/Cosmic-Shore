# CLAUDE.md — Cosmic Shore / Froglet Inc.

## Prime Directive

You are expected to work autonomously and persistently. Complete the entire task before stopping. Do not pause to ask for confirmation, approval, or clarification unless you are genuinely blocked on ambiguous requirements. If you encounter an error, debug and fix it yourself — attempt at least 3 different approaches before reporting the issue. Do not checkpoint, summarize progress, or ask "should I continue?" mid-task. Continue until all steps are done or you hit a hard wall.

When a task spans multiple files or systems, complete ALL of them in a single pass. Do not stop after the first file and ask if you should proceed to the next.

## About This Project

Cosmic Shore is a multigenre space game ("the party game for pilots") developed by Froglet Inc., a Delaware C-corp based in Grand Rapids, MI. Different vessel classes embody gameplay from different genres to connect players across demographics.

### Vessel Classes

The game features 11 vessel class types (defined in `Assets/_Scripts/Models/Enums/VesselClassType.cs`):

| Vessel | ID | Genre / Role |
|---|---|---|
| **Manta** | 1 | Feature-complete playable vessel |
| **Dolphin** | 2 | Feature-complete playable vessel |
| **Rhino** | 3 | Feature-complete playable vessel |
| **Urchin** | 4 | Playable vessel (AI in progress) |
| **Grizzly** | 5 | Playable vessel (AI in progress) |
| **Squirrel** | 6 | Racing/drift — vaporwave arcade racer, tube-riding along player-generated trails (F-Zero / Redout feel) |
| **Serpent** | 7 | Playable vessel with dedicated HUD |
| **Termite** | 8 | Planned |
| **Falcon** | 9 | Planned |
| **Shrike** | 10 | Planned |
| **Sparrow** | 11 | Shooter — arcade space combat with guns and missiles |

Meta values: `Any (-1)`, `Random (0)`

### Team Domains

Team ownership is tracked via the `Domains` enum: `Jade (1)`, `Ruby (2)`, `Blue (3)`, `Gold (4)`, `Unassigned (0)`, `None (-1)`.

### Tech Stack

- **Engine**: Unity 6+ with URP (Universal Render Pipeline) — `com.unity.render-pipelines.universal` 17.0.4
- **Language**: C# with UniTask (`com.cysharp.unitask`) for async
- **Architecture**: ScriptableObject-driven config separation + SOAP (Scriptable Object Architecture Pattern) for cross-system communication
- **Networking**: Unity Netcode for GameObjects (`com.unity.netcode.gameobjects` 2.5.0)
- **Camera**: Cinemachine 3.1.2 with per-vessel `CameraSettingsSO` assets
- **VFX**: VFX Graph 17.0.4, custom HLSL shaders, Shader Graph
- **Input**: Unity Input System 1.14.2 with strategy pattern (`IInputStrategy` → platform-specific implementations)
- **Audio**: Wwise integration
- **Haptics**: NiceVibrations for mobile haptic feedback
- **Animation**: Timeline 1.8.9, DOTween for procedural animation
- **DI**: Reflex (`com.gustavopsantos.reflex` 14.1.0) for dependency injection
- **Performance**: Unity Jobs + Burst Compiler, Adaptive Performance 5.1.6, DOTS Entities 1.4.2 (installed, incremental adoption)
- **Backend**: PlayFab SDK, Firebase, Unity Gaming Services (Analytics, CloudSave, Leaderboards, Multiplayer, Purchasing 4.12.2, Ads 4.12.0)
- **Testing**: Unity Test Framework 1.6.0 (NUnit-based)
- **Target**: Mobile-first with PC/console expansion

## Project Structure

```
Assets/
├── _Scripts/                  # All first-party code (~1,100 C# files)
│   ├── App/                   # Application-level systems
│   │   ├── Systems/           # Audio, Auth, Favorites, LoadOut, Quest, Rewind, Squads, UserAction, UserJourney, Xp, Ads, IAP
│   │   └── UI/                # App UI elements, FX, modals
│   ├── Game/                  # Gameplay systems (~600 files)
│   │   ├── Ship/              # Vessel core: VesselStatus, Prism, Trail, VesselPrismController, ShipActions/
│   │   ├── Prisms/            # PrismFactory, PrismComponents (ECS), performance managers
│   │   ├── Projectiles/       # Projectile systems
│   │   ├── ImpactEffects/     # Impactors (7 types) + Effect SOs (10+ types)
│   │   ├── Animation/         # Per-vessel animation controllers
│   │   ├── Camera/            # CustomCameraController, CameraSettingsSO, CameraRigAnchor
│   │   ├── IO/                # Input strategies (Keyboard, Gamepad, Touch)
│   │   ├── Multiplayer/       # Netcode: ClientPlayerVesselInitializer, ServerPlayerVesselInitializer, NetworkStatsManager
│   │   ├── Arcade/            # Mini-game controllers and configurations
│   │   ├── Party/             # Party/social system
│   │   ├── Environment/       # Prism animators, mini-game objects, flora/fauna
│   │   ├── Managers/          # PrismScaleManager, MaterialStateManager, PrismStateManager, BlockDensityGrid
│   │   └── UI/                # Game UI, VesselHUD (per-vessel HUD controllers)
│   ├── Systems/
│   │   └── Bootstrap/         # BootstrapController, ServiceLocator, SceneTransitionManager, ApplicationLifecycleManager
│   ├── Models/Enums/          # VesselClassType, Domains, ResourceType, ShipActions, etc.
│   ├── Utility/               # SOAP types, Effects, PoolsAndBuffers, DataContainers, DataPersistence
│   ├── DialogueSystem/        # Custom dialogue system with editor tools
│   └── Integrations/          # PlayFab integration
├── _SO_Assets/                # ScriptableObject asset instances (48+ subdirectories)
├── _Prefabs/                  # CORE, Cameras, Characters, Environment, Pools, Projectile, Spaceships, Trails, UI Elements
├── _Scenes/                   # Game scenes organized by type
├── _Graphics/, _Models/, _Audio/, _Animations/
├── FTUE/                      # First-Time User Experience / Tutorial system
├── Plugins/                   # Obvious.Soap, Demigiant (DOTween), etc.
├── Wwise/                     # Audio middleware
├── Firebase/, PlayFabSDK/     # Backend SDKs
├── NiceVibrations/            # Haptic feedback
└── SerializeInterface/        # Custom [RequireInterface] attribute support
```

### Assembly Definitions

| Assembly | Scope |
|---|---|
| `CosmicShore.Runtime` | Main gameplay code |
| `CosmicShore.Core` | Core types (`Models/Enums`) |
| `CosmicShore.Utility` | SOAP types, Effects, PoolsAndBuffers, DataPersistence, DataContainers |
| `CosmicShore.Bootstrap` | Bootstrap/scene management system |
| `CosmicShore.DialogueSystem` | Dialogue runtime |
| `CosmicShore.SSU` | Specialized subsystem |
| `CosmicShore.Editor` | Editor tools |
| `CosmicShore.Bootstrap.Tests` | Bootstrap unit tests |

## Architecture Patterns

Follow these established patterns. Do not introduce alternative architectures without discussion.

### ScriptableObject Config Separation

All tunable gameplay parameters live in ScriptableObjects, not in MonoBehaviours. MonoBehaviours reference SO configs at runtime. Example pattern:

- `SkimmerAlignPrismEffectSO` (config) → referenced by the vessel's prism controller system
- `VesselExplosionByCrystalEffectSO` (config) → defines explosion parameters for crystal impacts
- `CameraSettingsSO` (config) → per-vessel camera follow/zoom settings
- `BootstrapConfigSO` (config) → bootstrap scene flow settings (target framerate, splash duration, timeouts)
- Use `[CreateAssetMenu]` with organized menu paths: `ScriptableObjects/Impact Effects/[Category]/[Name]`

### SOAP — Scriptable Object Architecture Pattern (Primary Architecture)

This project uses the **SOAP asset** (Obvious.Soap v2.7.0, installed at `Assets/Plugins/Obvious/Soap/`) as the backbone for modular, event-driven, and data-container-based architecture. **Use SOAP whenever possible** for cross-system communication and shared state — do not introduce singletons, static events, or direct references between systems when a SOAP variable or event can do the job.

**Fail-loud policy**: Do not add if-null guards on `ScriptableEvent` serialized fields. Missing references should produce immediate, obvious errors rather than silent failures.

#### Core SOAP Primitives

- **`ScriptableVariable<T>`** — Persistent data containers that live as assets. Any system can read/write to them without knowing about other consumers. Use these for shared state (player health, score, vessel class, authentication data, etc.).
- **`ScriptableEvent<T>` / `ScriptableEventNoParam`** — Decoupled event channels. Raise events from any system; listeners subscribe via inspector-wired `EventListener` components or code. Use these for one-to-many notifications (game over, boost changed, crystal collected, etc.).
- **`EventListener<T>`** — MonoBehaviour that subscribes to a `ScriptableEvent` and exposes `UnityEvent` responses in the inspector. Preferred for UI and scene-bound reactions.

#### When to Use SOAP

| Scenario | SOAP Solution |
|---|---|
| Sharing state between unrelated systems | `ScriptableVariable<T>` asset |
| Broadcasting an event to multiple listeners | `ScriptableEvent<T>` asset |
| UI needs to react to gameplay changes | `EventListener<T>` on the UI GameObject |
| New system needs data from another system | Reference the existing `ScriptableVariable` — do not add a direct dependency |
| Request/response pattern between systems | `GenericEventChannelWithReturnSO<T, Y>` (custom extension at `Assets/_Scripts/Utility/SOAP/ScriptableEventWithReturn/`) |

#### Creating New SOAP Types

Custom SOAP types live in `Assets/_Scripts/Utility/SOAP/` organized by data type. When you need a new type:

1. Create a folder: `Assets/_Scripts/Utility/SOAP/Scriptable[TypeName]/`
2. Create the variable class: `[TypeName]Variable : ScriptableVariable<[TypeName]>`
3. Create the event class: `ScriptableEvent[TypeName] : ScriptableEvent<[TypeName]>`
4. Create the listener class: `EventListener[TypeName] : EventListenerGeneric<[TypeName]>`
5. Use namespace `CosmicShore.Soap` for all custom SOAP types

Existing custom SOAP types include: `AbilityStats`, `AuthenticationData`, `ClassType` (VesselClassType), `CrystalStats`, `InputEvents`, `PartyData` (PartyInviteData, PartyPlayerData + list variant), `PipData`, `PrismStats`, `Quaternion`, `ShipHUDData`, `SilhouetteData`, `Transform`, `NetworkMonitorData`, and `ScriptableEventWithReturn` (generic return channel + `PrismEventChannelWithReturnSO`).

#### SOAP Anti-Patterns

- **Do not** use singletons or static events for cross-system communication — use `ScriptableEvent` instead
- **Do not** add direct MonoBehaviour-to-MonoBehaviour references for data sharing — use `ScriptableVariable` instead
- **Do not** use `FindObjectOfType` or service locators to get shared data — wire a `ScriptableVariable` in the inspector
- **Do not** create C# events or `Action` delegates on MonoBehaviours for things that multiple unrelated systems need to observe — use `ScriptableEvent`
- **Do not** duplicate SOAP types — check `Assets/_Scripts/Utility/SOAP/` for existing types before creating new ones
- **Do not** put gameplay logic inside ScriptableVariable/ScriptableEvent classes — they are data containers and channels, not controllers
- **Do not** add if-null guards on ScriptableEvent serialize fields — fail loud on missing references

### Bootstrap & Scene Flow

The application uses an industry-standard bootstrap pattern (`Assets/_Scripts/Systems/Bootstrap/`):

1. **Bootstrap scene** (build index 0) → `BootstrapController` initializes all `IBootstrapService` implementations in order
2. **Authentication scene** → `SplashToAuthFlow` handles auth flow
3. **Menu_Main scene** → main menu entry point

Key classes:
- `BootstrapController` — top-level orchestrator (`[DefaultExecutionOrder(-100)]`), configures platform settings, initializes services, transitions to first scene
- `ServiceLocator` — lightweight service registry for bootstrap-time services (e.g., `SceneTransitionManager`)
- `SceneTransitionManager` — unified scene loading with fade transitions
- `ApplicationLifecycleManager` — application lifecycle events
- `BootstrapConfigSO` — configures: first scene, main menu scene, service init timeout, splash duration, framerate, screen sleep, vsync, verbose logging

### Input Strategy Pattern

Platform-agnostic input via `Assets/_Scripts/Game/IO/`:

- `IInputStrategy` — interface for all input handlers
- `BaseInputStrategy` — shared logic
- `KeyboardMouseInputStrategy`, `GamepadInputStrategy`, `TouchInputStrategy` — platform-specific implementations
- Input strategies are swappable per platform/context at runtime

### Impact Effects Architecture

The collision/impact system (`Assets/_Scripts/Game/ImpactEffects/`) uses a matrix of impactors and effect SOs:

**Impactor types** (all extend `ImpactorBase`): `VesselImpactor`, `PrismImpactor`, `ProjectileImpactor`, `SkimmerImpactor`, `MineImpactor`, `ExplosionImpactor`, `CrystalImpactor`

**Effect SO pattern**: `[Impactor][Target]EffectSO` — e.g., `VesselExplosionByCrystalEffectSO`, `SkimmerAlignPrismEffectSO`, `SparrowDebuffByRhinoDangerPrismEffectSO`. Per-vessel effect asset instances exist for each vessel class.

Key interfaces: `IImpactor` / `IImpactCollider`

### Multiplayer / Netcode

The game uses Unity Netcode for GameObjects (`com.unity.netcode.gameobjects` 2.5.0) for multiplayer. Key files in `Assets/_Scripts/Game/Multiplayer/`:

- `ClientPlayerVesselInitializer` / `ServerPlayerVesselInitializer` — vessel spawning on client/server
- `ServerPlayerVesselInitializerWithAI` — AI opponent spawning
- `MultiplayerSetup` — lobby/connection setup
- `NetworkStatsManager` — network health monitoring via `NetworkMonitorData` SOAP type
- `DomainAssigner` — team assignment

`VesselStatus` extends `NetworkBehaviour`. Multiplayer game modes can also run solo with AI opponents via the AI Profile system.

### FTUE (First-Time User Experience)

Tutorial system at `Assets/FTUE/` using adapter pattern with clean interface separation:

- **Interfaces**: `IFlowController`, `ITutorialExecutor`, `ITutorialStepHandler`, `ITutorialUIView`
- **Adapters**: `TutorialExecutorAdapter`, `FTUEIntroAnimatorAdapter`, `TutorialUIViewAdapter`
- **Data models**: `TutorialStep`, `TutorialPhase`, `TutorialSection`, `FTUEProgress`
- **Step handlers**: `FreestylePromptHandler`, `IntroWelcomeHandler`, `LockModesExceptFreestyleHandler`, `OpenArcadeMenuHandler`

### Dialogue System

Custom dialogue system at `Assets/_Scripts/DialogueSystem/` (namespace: `CosmicShore.DialogueSystem`):

- Editor tools: `DialogueEditorWindow`, `DialogueSetEditorView`
- Runtime: `DialogueManager`, `DialogueEventChannel`, `DialogueUIAnimator`, `DialogueViewResolver`, `DialogueAudioBatchLinker`

### AI Opponent System

Runtime-configurable AI opponents with `MainAIProfileList.asset`:
- `AIPilot` controls AI vessel behavior
- AI profiles used for score cards and multiplayer backfill
- Configurable AI ship selection and behavior at runtime

### Namespace Convention

All game code lives under `CosmicShore.*`:

- `CosmicShore.Core` — foundational systems, ship status, transformers
- `CosmicShore.Game` — gameplay systems (Ship, Prisms, Projectiles, ImpactEffects, Animation, Camera, IO, Multiplayer, Party, Arcade, Environment, Managers, UI)
- `CosmicShore.Game.Projectiles` — projectile-specific classes
- `CosmicShore.Systems.Bootstrap` — bootstrap/scene flow
- `CosmicShore.Services` — service layer
- `CosmicShore.Models.Enums` — all game enumerations
- `CosmicShore.UI` / `CosmicShore.VesselHUD` — UI systems
- `CosmicShore.Soap` — custom SOAP types
- `CosmicShore.Utility` — utilities (SOAP types, Effects, PoolsAndBuffers, DataContainers, DataPersistence)
- `CosmicShore.DialogueSystem` — dialogue management

### Key Systems & Classes

| System | Key Classes | Location |
|---|---|---|
| Vessel core | `VesselStatus` (extends `NetworkBehaviour`), `ShipTransformer`, `VesselPrismController` | `_Scripts/Game/Ship/` |
| Prism lifecycle | `Prism`, `PrismFactory`, `Trail`, `TrailBlock` | `_Scripts/Game/Ship/`, `_Scripts/Game/Prisms/` |
| Prism performance | `PrismScaleManager`, `MaterialStateManager`, `AdaptiveAnimationManager`, `PrismStateManager`, `PrismTimerManager`, `BlockDensityGrid` | `_Scripts/Game/Managers/` |
| Impact effects | `ImpactorBase` + 7 impactor types, 10+ Effect SO types | `_Scripts/Game/ImpactEffects/` |
| Camera | `CustomCameraController`, `ShipCameraCustomizer`, `CameraRigAnchor`, `CameraSettingsSO`, `ICameraController`, `ICameraConfigurator` | `_Scripts/Game/Camera/` |
| Vessel HUD | `IVesselHUDController`, `IShipHUDView`, per-vessel controllers (Sparrow, Squirrel, Serpent, Manta, Rhino, Dolphin) | `_Scripts/Game/UI/` |
| Arcade games | `MiniGameControllerBase`, `SinglePlayerMiniGameControllerBase`, `MultiplayerMiniGameControllerBase` | `_Scripts/Game/Arcade/` |
| Resource system | `ResourceSystem`, `R_VesselActionHandler`, `R_ShipElementStatsHandler` | `_Scripts/Game/Ship/` |
| Object pooling | `GenericPoolManager` (Unity `ObjectPool<T>` with async buffer maintenance) | `_Scripts/Utility/PoolsAndBuffers/` |
| UI | Elements, FX, Modals, Screens, Views + `ToastService` / `ToastChannel` | `_Scripts/App/UI/`, `_Scripts/Game/UI/` |
| Telemetry | `VesselTelemetryBootstrapper`, `VesselStats` SO, UGS Cloud Save upload | `_Scripts/Game/Ship/` |
| App systems | Audio, Auth, Favorites, LoadOut, Quest, Rewind, Squads, UserAction, UserJourney, Xp, Ads, IAP | `_Scripts/App/Systems/` |

### Async Pattern

- Prefer UniTask over coroutines for new code
- For ScriptableObjects that need async: use a `CoroutineRunner` singleton proxy or async/await with cancellation tokens
- Always include `CancellationToken` for anything non-trivial — UniTask respects play mode lifecycle better than raw `Task`
- Bootstrap uses `UniTaskVoid` with `CancellationTokenSource` for the async startup sequence

### Anti-Patterns to Avoid

- `FindObjectOfType` / `GameObject.Find` in hot paths
- `Instantiate`/`Destroy` in gameplay loops — use object pooling
- Excessive `GetComponent` calls — cache references
- Mixed coroutine/async patterns in the same system
- Singletons, static events, or direct references for cross-system communication — use SOAP `ScriptableVariable` and `ScriptableEvent` instead
- C# `event Action` / delegates on MonoBehaviours for broadcast patterns — use SOAP `ScriptableEvent` channels
- `renderer.material` (clones material) — use `renderer.sharedMaterial` + MaterialPropertyBlock instead
- Per-object coroutines at scale — use centralized timer/manager systems (see Prism Performance Audit)

## Shader & Visual Development

### HLSL / Shader Graph

- Custom Function nodes use HLSL files stored in a consistent location
- Function signatures must follow Shader Graph conventions (proper `_float` suffix usage, sampler declarations)
- Blend shapes are converted to textures for shader-driven animation (no controller scripts — animation is entirely GPU-driven for performance)
- Edge detection, prism rendering, Shepard tone effects, and speed trail scaling are active shader systems
- Procedural HyperSea skybox shader with Andromeda galaxy, domain-warped nebulae, and configurable star density

### Performance Standards

- Use `Unity.Profiling.ProfilerMarker` with `using (marker.Auto())` for profiling, not manual `Begin`/`EndSample`
- Watch for `Gfx.WaitForPresentOnGfxThread` bottlenecks — usually indicates GPU sync issues, not CPU
- Static batching, object pooling, and draw call management are always priorities
- Test with profiler before and after optimization changes — don't assume improvement
- GPU instancing enabled on all prism and VFX materials
- Jobs + Burst used for prism scale/material animation batching (`PrismScaleManager`, `MaterialStateManager`)
- `AdaptiveAnimationManager` provides dynamic frame-skipping (1x-12x) based on performance pressure
- Burst-compiled spatial queries replace Physics-based AOE prism damage
- Cache-line-aware data layouts with hot/cold splitting and bit-packed flags (`PrismAOEData`)

### Prism System Performance

The prism system is the most performance-critical gameplay system. See `Assets/_Scripts/Game/Prisms/PRISM_PERFORMANCE_AUDIT.md` for the full audit. Key facts:

- Each prism is a full GameObject with 5-6 MonoBehaviours + BoxCollider + MeshRenderer
- At 2,000 prisms: ~12,000 MonoBehaviour instances + 2,000 colliders
- Scale and material animation are already Jobs + Burst optimized
- Main bottlenecks: explosion/implosion VFX (per-object UniTask), physics colliders, material instancing leaks
- Active optimization: `PrismTimerManager`, per-frame explosion VFX cap, `EventListenerBase` GC elimination

## Testing

### Test Infrastructure

- **Framework**: Unity Test Framework 1.6.0 (NUnit-based)
- **Bootstrap tests**: `Assets/_Scripts/Systems/Bootstrap/Tests/` — `BootstrapControllerTests`, `ServiceLocatorTests`, `SceneTransitionManagerTests`, `ApplicationLifecycleManagerTests`, `SceneFlowIntegrationTests`, `BootstrapConfigSOTests`
- **PlayFab tests**: `Assets/_Scripts/Integrations/Playfab/PlayFabTests/`
- **SOAP framework tests**: `Assets/Plugins/Obvious/Soap/Core/Editor/Tests/`
- **Test scenes**: `Assets/_Scenes/TestInput/`, `Assets/_Scenes/Game_TestDesign/`

### Build & CI

No automated CI/CD pipeline is currently configured. Builds are manual. Build profiles live in `Assets/Settings/Build Profiles/`.

## Code Style

- Clean, maintainable C# — favor readability over cleverness
- Use `[Header("Section Name")]` and `[Tooltip("...")]` attributes generously on serialized fields
- Use `[SerializeField]` with private fields, not public fields
- Pattern match where it improves clarity: `effects is { Length: > 0 }`
- Use `TryGetComponent` over `GetComponent` + null check
- Prefer expression-bodied members for simple accessors: `public Transform Transform => transform;`
- Anti-spam / cooldown patterns belong in the SO config, not hardcoded
- Always assign static numeric values to enum members to prevent Unity serialization drift
- Commit messages follow conventional commits: `type(scope): summary` (see `GIT_RULES.md`)

## Debugging Methodology

When investigating issues, follow this systematic approach:

1. Reproduce the issue consistently
2. Add `ProfilerMarker`s to isolate the hot path
3. Check the call stack in Timeline view for self-time
4. Narrow to the specific derived class (base class profiling often hides the real culprit)
5. Fix, profile again, confirm improvement with data

Do not guess at performance problems. Profile first.

## Current Priority Context

### GDC 2026 (March 9-13)

Active build target is a 15-minute investor demo for MeetToMatch pitch meetings. The demo must showcase:

- Squirrel vessel (racing/drift gameplay)
- Sparrow vessel (shooter gameplay)
- Party game mechanics and how vessel classes connect players
- Multiplayer with AI opponents for solo demo capability
- Polish level that communicates production readiness

Every technical decision should be weighed against: **does this help the GDC demo?**

### Build Priority Stack (in order)

1. Core gameplay loop stability for both demo vessels
2. Visual polish that communicates quality to investors
3. Performance — must be smooth during live demo
4. UI/UX clarity for first-time players watching a pitch
5. Multiplayer stability (with AI backfill for reliable demos)
6. Everything else

## Communication Preferences

- Be direct and technical. Skip preamble and motivational framing.
- When presenting solutions, lead with the code, then explain if needed.
- If you need to make a judgment call between two valid approaches, pick the one that's simpler to maintain and mention the tradeoff briefly.
- When refactoring, preserve the existing naming conventions and folder structure unless explicitly asked to reorganize.
- For shader work: always specify which render pipeline stage and what Shader Graph node types are involved.
- Don't repeat back what I just told you. Acknowledge briefly and move to the solution.

## What Claude Code Should Never Do

- Stop to ask "would you like me to continue?" after completing one of several related files
- Introduce new packages or dependencies without flagging it first
- Restructure folder organization or namespaces without explicit instruction
- Use `Debug.Log` as a fix — it's a diagnostic tool, not a solution
- Leave TODO comments as a substitute for completing the work
- Generate code that compiles but ignores the established architecture patterns above
- Add if-null guards on SOAP ScriptableEvent serialized fields — fail loud
- Use `renderer.material` when `renderer.sharedMaterial` + MaterialPropertyBlock works
