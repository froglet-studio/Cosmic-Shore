# CLAUDE.md — Cosmic Shore / Froglet Inc.

## Prime Directive

You are expected to work autonomously and persistently. Complete the entire task before stopping. Do not pause to ask for confirmation, approval, or clarification unless you are genuinely blocked on ambiguous requirements. If you encounter an error, debug and fix it yourself — attempt at least 3 different approaches before reporting the issue. Do not checkpoint, summarize progress, or ask "should I continue?" mid-task. Continue until all steps are done or you hit a hard wall.

When a task spans multiple files or systems, complete ALL of them in a single pass. Do not stop after the first file and ask if you should proceed to the next.

## About This Project

Cosmic Shore is a multigenre space game ("the party game for pilots") developed by Froglet Inc., a Delaware C-corp based in Grand Rapids, MI. Different vessel classes embody gameplay from different genres to connect players across demographics.

### Vessel Classes

The game features 11 vessel class types (defined in `Assets/_Scripts/Data/Enums/VesselClassType.cs`):

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
│   ├── Controller/            # Gameplay systems (~536 files)
│   │   ├── Vessel/            # Vessel core: VesselStatus, Prism, Trail, VesselPrismController, VesselActions/, R_VesselActions/
│   │   ├── Environment/       # Cells, crystals, flora/fauna, flow fields, warp fields, spawning
│   │   ├── ImpactEffects/     # Impactors (11 types) + Effect SOs (20+ types)
│   │   ├── Arcade/            # Mini-game controllers, scoring, turn monitors
│   │   ├── Projectiles/       # Projectile systems, guns, mines, AOE effects
│   │   ├── Managers/          # PrismScaleManager, MaterialStateManager, PrismStateManager, ThemeManager
│   │   ├── IO/                # Input strategies (Keyboard, Gamepad, Touch)
│   │   ├── Animation/         # Per-vessel animation controllers
│   │   ├── Camera/            # CustomCameraController, CameraSettingsSO, ICameraController
│   │   ├── Multiplayer/       # Netcode: ServerPlayerVesselInitializer (+ WithAI, Menu variants), ClientPlayerVesselInitializer, MultiplayerSetup, MenuCrystalClickHandler, DomainAssigner, NetworkStatsManager
│   │   ├── Player/            # Player (NetworkBehaviour), PlayerSpawner, IPlayer, PlayerSpawnerAdapterBase, MiniGamePlayerSpawnerAdapter
│   │   ├── Prisms/            # PrismFactory
│   │   ├── Assemblers/        # Gyroid/wall assembly systems
│   │   ├── Party/             # HostConnectionService, PartyInviteController, FriendsInitializer
│   │   ├── AI/                # AIPilot, AIGunner
│   │   ├── FX/                # Visual effects controllers
│   │   ├── ECS/               # DOTS entity components
│   │   ├── XP/                # Experience point controllers
│   │   └── Settings/          # Runtime settings
│   ├── System/                # Application-level systems (~126 files)
│   │   ├── Bootstrap/         # BootstrapConfigSO, SceneTransitionManager, ApplicationLifecycleManager
│   │   ├── Playfab/           # PlayFab integration (Auth, Economy, Groups, PlayerData, PlayStream)
│   │   ├── Instrumentation/   # CSAnalyticsManager, Firebase analytics, data collectors
│   │   ├── Runtime/           # Dialogue runtime (DialogueManager, models, views, helpers)
│   │   ├── RewindSystem/      # Rewind/replay functionality
│   │   ├── Audio/             # Wwise audio management
│   │   ├── LoadOut/           # Vessel loadout configuration
│   │   ├── CallToAction/      # Promotional/CTA system
│   │   ├── Squads/            # Squad management
│   │   ├── Quest/             # Quest system
│   │   ├── UserAction/        # User action tracking
│   │   ├── UserJourney/       # Funnel analytics
│   │   ├── Favorites/         # Favorites system
│   │   ├── Xp/                # XP leveling
│   │   ├── Ads/               # Ad integration
│   │   └── Architectures/     # Shared architectural base classes
│   ├── UI/                    # Game & app UI (~188 files)
│   │   ├── Controller/        # VesselHUD controllers (Manta, Rhino, Serpent, Sparrow)
│   │   ├── View/              # VesselHUD views (all vessel types + Minigame, Multiplayer)
│   │   ├── Interfaces/        # IVesselHUDController, IVesselHUDView, IMinigameHUDController, IScreen
│   │   ├── Elements/          # Reusable UI components (NavLink, NavGroup, ProfileDisplayWidget, etc.)
│   │   ├── Views/             # Screen/view implementations (VesselSelection, XPTrack, Profile)
│   │   ├── Modals/            # Modal dialogs (Settings, Profile, PurchaseConfirmation)
│   │   ├── Screens/           # Screen containers
│   │   ├── ToastSystem/       # ToastService, ToastChannel, ToastAnimation
│   │   ├── Notification System/ # Push notification UI
│   │   ├── GameEventFeed/     # In-game event feed
│   │   ├── FX/                # UI visual effects
│   │   └── Animations/        # UI animations
│   ├── Data/                  # Models & enums (~29 files)
│   │   ├── Enums/             # VesselClassType, Domains, ResourceType, ShipActions, InputEvents, etc.
│   │   └── Structs/           # DailyChallenge, GameplayReward, TrainingGameProgress
│   ├── ScriptableObjects/     # SO definitions & SOAP types (~70 files)
│   │   ├── SOAP/              # Custom SOAP types (16 subdirectories)
│   │   └── SO_*.cs            # Game data SOs (Captain, Vessel, Game, ArcadeGame, Element, etc.)
│   ├── Utility/               # Effects, PoolsAndBuffers, DataContainers, DataPersistence, ClassExtensions
│   ├── DialogueSystem/        # Dialogue editor tools, animation, SO assets
│   ├── Editor/                # Editor tools (CopyTool, shader inspectors, scene utilities)
│   ├── Tests/                 # Edit-mode unit tests
│   ├── Integrations/          # PlayFab SDK integration
│   └── SSUScripts/            # Specialized subsystem scripts
├── _SO_Assets/                # ScriptableObject asset instances (48+ subdirectories)
├── _Prefabs/                  # CORE, Cameras, Characters, Environment, Pools, Projectile, Spaceships, Trails, UI Elements
├── _Scenes/                   # Game scenes organized by type
├── _Graphics/, _Models/, _Audio/, _Animations/
├── FTUE/                      # First-Time User Experience / Tutorial system
├── Plugins/                   # Obvious.Soap, Demigiant (DOTween), NativeShare, etc.
├── Wwise/                     # Audio middleware
├── Firebase/, PlayFabSDK/     # Backend SDKs
├── NiceVibrations/            # Haptic feedback
└── SerializeInterface/        # Custom [RequireInterface] attribute support
```

Note: A vestigial `_Scripts/Game/` directory exists containing only non-code assets (compute shaders, input action mappings, material files, and the `PRISM_PERFORMANCE_AUDIT.md`). All C# code has been reorganized into the directories listed above.

### Assembly Definitions

All first-party gameplay code compiles in Unity's default assembly (no runtime `.asmdef` files). Only test assemblies have explicit assembly definitions:

| Assembly | Scope |
|---|---|
| `CosmicShore.Bootstrap.Tests` | Bootstrap unit tests |
| `CosmicShore.Multiplayer.Tests` | Multiplayer unit tests |
| `CosmicShore.PlayFabTests` | PlayFab integration tests |
| `CosmicShore.Tests.EditMode` | General edit-mode tests |

Third-party assemblies: `Obvious.Soap`, `PlayFab`, `Lofelt.NiceVibrations`, `NativeShare.Runtime`

### Scene Inventory

See `Docs/SCENES.md` for the full scene and game mode reference. Summary below.

#### Core Application Scenes

| Scene | Build Order | Purpose |
|---|---|---|
| **Bootstrap** | 0 (must be first) | App entry: DI registration, platform config, auth start, splash |
| **Authentication** | 1 | Auth UI, cached session check, NetworkManager host start |
| **Menu_Main** | 2 | Main menu with networked autopilot vessel, screen navigation |

#### Single-Player Game Scenes

| Scene | Game Mode | Controller |
|---|---|---|
| `MinigameFreestyle` | `Freestyle (7)` | `SinglePlayerFreestyleController` |
| `MinigameCellularDuel` | `CellularDuel (8)` | `SinglePlayerCellularDuelController` |
| `MinigameWildlifeBlitz` | `WildlifeBlitz (26)` | `SinglePlayerWildlifeBlitzController` |

All in `Assets/_Scenes/Singleplayer Scenes/`.

#### Multiplayer Game Scenes

| Scene | Game Mode | Controller |
|---|---|---|
| `MinigameHexRace` | `HexRace (33)` | `HexRaceController` |
| `MinigameFreestyleMultiplayer_Gameplay` | `MultiplayerFreestyle (28)` | `MultiplayerFreestyleController` |
| `MinigameCrystalCaptureMultiplayer_Gameplay` | `MultiplayerCrystalCapture (35)` | `MultiplayerCrystalCaptureController` |
| `MinigameDuelForCellMultiplayer_Gameplay` | `MultiplayerCellularDuel (29)` | `MultiplayerCellularDuelController` |
| `MinigameJoust_Gameplay` | `MultiplayerJoust (34)` | `MultiplayerJoustController` |
| `MinigameWildlifeBlitzMultuplayerCoOp` | `MultiplayerWildlifeBlitzGame (32)` | `MultiplayerWildlifeBlitzMiniGame` |
| `ArcadeGameMultiplayer2v2CoOpVsAI` | `Multiplayer2v2CoOpVsAI (30)` | Domain games variant |

All in `Assets/_Scenes/Multiplayer Scenes/`.

#### Tool & Test Scenes

`Recording Studio`, `MattsRecording Studio`, `PhotoBooth` (in `_Scenes/Tools/`), `AudioTestSandbox` (in `_Scenes/Game_TestDesign/`).

### Game Modes & Controllers

#### GameModes Enum (`Assets/_Scripts/Data/Enums/GameModes.cs`)

36 game modes with explicit numeric IDs. Single-player: `Elimination(1)` through `ProtectMission(27)`. Multiplayer: `MultiplayerFreestyle(28)`, `MultiplayerCellularDuel(29)`, `Multiplayer2v2CoOpVsAI(30)`, `MultiplayerWildlifeBlitzGame(32)`, `HexRace(33)`, `MultiplayerJoust(34)`, `MultiplayerCrystalCapture(35)`. Meta: `Random(0)`. Note: ID 31 is skipped.

Many single-player modes (1-6, 9-25, 27) reference scenes that no longer exist on disk — their `SO_ArcadeGame` assets still exist and appear in the Arcade UI, but launching them would fail.

#### Controller Hierarchy

```
MiniGameControllerBase (abstract, NetworkBehaviour)
│   Template Method: rounds → turns → countdown → gameplay → end
│
├── SinglePlayerMiniGameControllerBase (abstract)
│   ├── SinglePlayerFreestyleController    — shape drawing + open-ended freestyle
│   ├── SinglePlayerCellularDuelController — vessel swap on turn end
│   ├── SinglePlayerSlipnStrideController  — procedural course with intensity scaling
│   ├── SinglePlayerWildlifeBlitzController — blitz scoring
│   └── WildlifeBlitzMiniGame             — minimal variant
│
└── MultiplayerMiniGameControllerBase (abstract, NetworkBehaviour)
    │   Server-authoritative turn/round/game flow via ClientRpc
    │
    ├── MultiplayerFreestyleController     — sandbox, per-player activation
    ├── MultiplayerWildlifeBlitzMiniGame    — co-op, own ready-sync
    │
    └── MultiplayerDomainGamesController
        ├── HexRaceController              — crystal race, deterministic track, golf scoring
        ├── MultiplayerJoustController      — collision tracking, golf scoring
        ├── MultiplayerCellularDuelController — vessel ownership swap between rounds
        └── MultiplayerCrystalCaptureController — minimal (1 round, 1 turn)
```

#### Game Launch Pipeline

1. **`SO_ArcadeGame` asset** — static config (mode, scene, captains, player/intensity ranges, scoring)
2. **`ArcadeGameConfigSO`** — ephemeral UI state (selected game + intensity + players + vessel)
3. **`GameDataSO`** — shared SOAP runtime state (all game params + SOAP events)
4. **`SceneLoader.LaunchGame()`** — subscribes to `OnLaunchGame`, loads scene. Game config is synced to clients by `MultiplayerMiniGameControllerBase.OnNetworkSpawn()` in the game scene
5. **Game controller** — scene-placed `MiniGameControllerBase` subclass drives turn/round/game lifecycle

### Documentation Index

| Document | Location | Content |
|---|---|---|
| `CLAUDE.md` | Project root | Architecture, patterns, systems reference |
| `SCENES.md` | `Docs/` | Complete scene inventory, game modes, launch pipeline |
| `CameraMigrationReview.md` | `Docs/` | Camera system migration tracking |
| `BOOTSTRAP_AUDIT.md` | `_Scripts/System/Bootstrap/` | Bootstrap scene audit, execution order, DI registration |
| `HEXRACE.md` | `_Scripts/Controller/Arcade/` | HexRace game mode technical reference |
| `CRYSTAL_CAPTURE.md` | `_Scripts/Controller/Arcade/` | Crystal Capture game mode technical reference |
| `JOUST.md` | `_Scripts/Controller/Arcade/` | Joust game mode technical reference |
| `PRISM_PERFORMANCE_AUDIT.md` | `_Scripts/Game/Prisms/` | Prism system performance analysis (vestigial location) |
| `UNIT_TESTING_GUIDE.md` | `_Scripts/Tests/` | Unit testing guidelines and inventory |
| `BENCHMARK_TEST_PROCEDURE.md` | `_Scripts/Utility/PerformanceBenchmark/` | Performance benchmarking procedures |
| `GIT_RULES.md` | Project root | Git commit conventions |

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
| Request/response pattern between systems | `GenericEventChannelWithReturnSO<T, Y>` (custom extension at `Assets/_Scripts/ScriptableObjects/SOAP/ScriptableEventWithReturn/`) |

#### Creating New SOAP Types

Custom SOAP types live in `Assets/_Scripts/ScriptableObjects/SOAP/` organized by data type. When you need a new type:

1. Create a folder: `Assets/_Scripts/ScriptableObjects/SOAP/Scriptable[TypeName]/`
2. Create the variable class: `[TypeName]Variable : ScriptableVariable<[TypeName]>`
3. Create the event class: `ScriptableEvent[TypeName] : ScriptableEvent<[TypeName]>`
4. Create the listener class: `EventListener[TypeName] : EventListenerGeneric<[TypeName]>`
5. Use namespace `CosmicShore.ScriptableObjects` for all custom SOAP types

Existing custom SOAP types (16 subdirectories): `AbilityStats`, `ApplicationState` (`ApplicationStateData` + `ApplicationStateDataVariable` + `ScriptableEventApplicationState` — written by `ApplicationStateMachine`), `AuthenticationData` (+ `NetworkMonitorData`), `ClassType` (VesselClassType + VesselImpactor + debuff events), `CrystalStats`, `FriendData` (`FriendData` struct + `FriendPresenceActivity` `[DataContract]` + `ScriptableEventFriendData` + `ScriptableListFriendData` + `EventListenerFriendData` — relationship & presence data for UGS Friends integration, written by `FriendsServiceFacade`), `GameplaySFX` (gameplay sound effect category events for decoupled audio), `InputEvents`, `PartyData` (PartyInviteData, PartyPlayerData + list variant), `PipData`, `PrismStats`, `Quaternion`, `VesselHUDData`, `SilhouetteData`, `Transform`, and `ScriptableEventWithReturn` (generic return channel + `PrismEventChannelWithReturnSO`). Also contains `VesselPrefabContainer.cs` for vessel-class-to-prefab mapping.

#### SOAP Anti-Patterns

- **Do not** use singletons or static events for cross-system communication — use `ScriptableEvent` instead
- **Do not** add direct MonoBehaviour-to-MonoBehaviour references for data sharing — use `ScriptableVariable` instead
- **Do not** use `FindObjectOfType` or service locators to get shared data — wire a `ScriptableVariable` in the inspector
- **Do not** create C# events or `Action` delegates on MonoBehaviours for things that multiple unrelated systems need to observe — use `ScriptableEvent`
- **Do not** duplicate SOAP types — check `Assets/_Scripts/ScriptableObjects/SOAP/` for existing types before creating new ones
- **Do not** put gameplay logic inside ScriptableVariable/ScriptableEvent classes — they are data containers and channels, not controllers
- **Do not** add if-null guards on ScriptableEvent serialize fields — fail loud on missing references

### Bootstrap & Scene Flow

The application uses a unified bootstrap pattern centered on `AppManager`, with `ApplicationStateMachine` tracking the top-level phase:

1. **Bootstrap scene** (build index 0) → `AppManager` configures platform, registers DI bindings, starts auth, transitions to Authentication scene. State: `None → Bootstrapping → Authenticating`.
2. **Authentication scene** → checks cached auth, signs in or shows auth UI. State: `Authenticating → MainMenu`.
3. **Menu_Main scene** → main menu entry point. State: `MainMenu`.

Key classes:
- `AppManager` (`_Scripts/System/AppManager.cs`) — top-level orchestrator and Reflex DI root (`[DefaultExecutionOrder(-100)]`, implements `IInstaller`). Handles platform configuration, DI registration of all persistent managers and SO assets, auth/network startup, splash fade, and scene transition. Lives on a `DontDestroyOnLoad` root.
- `ApplicationStateMachine` (`_Scripts/System/ApplicationStateMachine.cs`) — pure C# class (DI lazy singleton). Single-writer to `ApplicationStateDataVariable` (SOAP). Validates transitions via a table-driven state graph. Auto-subscribes to gameplay SOAP events (`OnSessionStarted`, `OnMiniGameEnd`) and lifecycle events (pause, quit, network loss) for automatic phase transitions. States: `None(0)`, `Bootstrapping(1)`, `Authenticating(2)`, `MainMenu(3)`, `LoadingGame(4)`, `InGame(5)`, `GameOver(6)`, `Paused(7)`, `Disconnected(8)`, `ShuttingDown(9)`.
- `SceneLoader` (`_Scripts/System/SceneLoader.cs`) — persistent scene-loading service. Extends `MonoBehaviour` (DontDestroyOnLoad). Lives in the Bootstrap scene and persists across all scene transitions. Subscribes to SOAP events in code (`OnLaunchGame`, `OnClickToMainMenuButton`, `OnActiveSessionEnd`, `OnClickToRestartButton`) — no per-scene EventListenerNoParam wiring needed. Handles launching gameplay scenes (auto-selects local vs network loading), returning to main menu, and local restart. Registered as a DI singleton via AppManager. Game config sync to clients is handled by `MultiplayerMiniGameControllerBase.SyncGameConfigToClients_ClientRpc()` in the game scene.
- `SceneNameListSO` (`_Scripts/Utility/DataContainers/SceneNameListSO.cs`) — centralized scene name registry (Bootstrap, Authentication, Menu_Main, Multiplayer). Registered in DI and injected where scene names are needed, replacing hardcoded strings.
- `SceneTransitionManager` — unified scene loading with fade transitions (`[DefaultExecutionOrder(-50)]`), creates its own full-screen fade overlay programmatically. Registered as a DI singleton.
- `ApplicationLifecycleManager` — application lifecycle events, bridges both static C# events (legacy) and SOAP events via `ApplicationLifecycleEventsContainerSO`
- `ApplicationLifecycleEventsContainerSO` (`_Scripts/ScriptableObjects/ApplicationLifecycleEventsContainerSO.cs`) — SO container bundling SOAP events for app lifecycle: `OnAppPaused`, `OnAppFocusChanged`, `OnAppQuitting`, `OnSceneLoaded`, `OnSceneUnloading`. Registered in DI.
- `BootstrapConfigSO` — configures: service init timeout, splash duration, framerate, screen sleep, vsync, verbose logging
- `FriendsServiceFacade` (`_Scripts/System/FriendsServiceFacade.cs`) — pure C# class (DI lazy singleton). Single-writer facade for UGS Friends service. Syncs relationship data into `FriendsDataSO`. Supports friend requests, management, presence, and refresh.

See `Assets/_Scripts/System/Bootstrap/BOOTSTRAP_AUDIT.md` for the bootstrap scene audit: root GameObjects, execution order map, applied fixes, and deferred issues. See `Docs/SCENES.md` for the complete scene inventory, game mode reference, and game launch pipeline documentation.

### Authentication & Session Flow

Authentication uses **Unity Gaming Services (UGS)** exclusively. Legacy PlayFab auth files exist under `_Scripts/System/Playfab/Authentication/` but are deprecated and inert.

#### Architecture

The auth system follows a **single-writer / multi-reader** pattern through SOAP:

- **`AuthenticationServiceFacade`** (plain C# singleton, Reflex DI) — the **sole writer** to `AuthenticationDataVariable`. Handles UGS initialization, anonymous sign-in, cached session restore, event wiring, and sign-out. Created by `AppManager.InstallBindings()` as a lazy singleton.
- **`AuthenticationDataVariable`** (SOAP `ScriptableVariable<AuthenticationData>`) — the **shared state**. All other systems read from this or subscribe to its events.
- **`AuthenticationController`** (MonoBehaviour) — thin adapter that delegates to the facade via `[Inject]`. Exists for scenes that need a GameObject entry point (e.g., inspector-driven `autoSignInAnonymously` toggle).
- **`AuthenticationSceneController`** (MonoBehaviour) — orchestrates the Authentication scene UI: auto-skip on cached auth, guest login button, username setup panel, navigation to main menu. All async work uses `CancellationToken` and `UniTask`.
- **`SplashToAuthFlow`** (MonoBehaviour) — placed on the splash scene. After splash display, reads `AuthenticationDataVariable` to decide: skip to `Menu_Main` (if signed in) or load the Authentication scene.

#### Execution Flow

```
Bootstrap Scene (build index 0)
│
├─ AppManager.Awake() [DefaultExecutionOrder(-100)]
│   ├─ DontDestroyOnLoad(gameObject)
│   ├─ ConfigurePlatform() (framerate, vsync, screen sleep via BootstrapConfigSO)
│   └─ TryResolveManagersEarly() (find 12 scene managers, mark DontDestroyOnLoad)
│
├─ AppManager.InstallBindings() (Reflex IInstaller)
│   ├─ RegisterValue: SceneNameListSO, GameDataSO, AuthenticationDataVariable,
│   │   NetworkMonitorDataVariable, FriendsDataSO, HostConnectionDataSO,
│   │   ApplicationLifecycleEventsContainerSO, ApplicationStateDataVariable
│   ├─ RegisterFactory (Lazy Singleton): GameSetting, AudioSystem, PlayerDataService,
│   │   UGSStatsManager, CaptainManager, IAPManager, SceneLoader, ThemeManager,
│   │   CameraManager, PostProcessingManager, StatsManager, SceneTransitionManager
│   └─ RegisterFactory (Lazy Singleton): AuthenticationServiceFacade, NetworkMonitor,
│       FriendsServiceFacade, ApplicationStateMachine
│
├─ AppManager.Start()
│   ├─ ApplicationStateMachine.TransitionTo(Bootstrapping)
│   ├─ ConfigureGameData()
│   ├─ StartNetworkMonitor()
│   ├─ StartAuthentication()  ← fire-and-forget
│   │   ├─ UnityServices.InitializeAsync()
│   │   ├─ WireAuthEventsOnce()
│   │   ├─ SignInAnonymouslyAsync()
│   │   └─ OnSignInSuccess() → AuthenticationData SOAP events
│   │       └─ OnSignedIn.Raise() ──► PlayerDataService.HandleSignedIn()
│   │                                  └─ CloudSave load/merge → IsInitialized = true
│   └─ RunBootstrapAsync().Forget()
│       ├─ Yield frames (let Awake/Start settle)
│       ├─ Enforce minimum splash duration
│       ├─ Fade out splash CanvasGroup
│       ├─ ApplicationStateMachine.TransitionTo(Authenticating)
│       └─ Load Authentication scene (via SceneTransitionManager or direct)
│
    ▼
Authentication Scene
│ AuthenticationSceneController.Start()
│ ├─ [1] Already signed in? → HandlePostAuthFlow → Menu_Main
│ ├─ [2] facade.TrySignInCachedAsync() succeeds? → HandlePostAuthFlow → Menu_Main
│ ├─ [3] Show auth panel (or auto-anonymous sign-in if no panel)
│ │   └─ Guest Login button → facade.EnsureSignedInAnonymouslyAsync()
│ ├─ OnSignedIn SOAP event ──► MultiplayerSetup.EnsureHostStartedAsync()
│ │   └─ Instantiates NetworkManager prefab → nm.StartHost()
│ ├─ HandlePostAuthFlow:
│ │   ├─ Wait for PlayerDataService.IsInitialized (with timeout)
│ │   ├─ Username needed? → Show username setup panel
│ │   └─ NavigateToMainMenu():
│ │       ├─ ApplicationStateMachine.TransitionTo(MainMenu)
│ │       ├─ Wait for NetworkManager.IsListening (3s timeout)
│ │       ├─ If host ready → nm.SceneManager.LoadScene(Menu_Main)
│ │       └─ Fallback → direct scene load via SceneTransitionManager
│ └─ Safety timeout (10s configurable) → force-navigate to Menu_Main
│
    ▼
Menu_Main Scene (loaded as networked scene when host is running)
│
│ MainMenuController.Start()  [Game GameObject]
│ ├─ ConfigureMenuGameData():
│ │   ├─ gameData.SetSpawnPositions(_playerOrigins)
│ │   ├─ gameData.selectedVesselClass = Squirrel (configurable)
│ │   ├─ gameData.SelectedPlayerCount = 3
│ │   └─ gameData.SelectedIntensity = 1
│ ├─ Subscribe to OnClientReady → HandleMenuReady (transitions to Ready state)
│ ├─ Subscribe to OnLaunchGame → HandleLaunchGame (transitions to LaunchingGame)
│ ├─ TransitionTo(Initializing)
│ ├─ DomainAssigner.Initialize()
│ └─ gameData.InitializeGame() → raises OnInitializeGame
│
│ Player Spawning Chain (network-driven):
│ ├─ Player.OnNetworkSpawn() [host's Player object, spawned in Auth scene]
│ │   ├─ gameData.Players.Add(this)
│ │   ├─ Raise OnPlayerNetworkSpawnedUlong(OwnerClientId)
│ │   ├─ Resolve display name (PlayerDataService → GameDataSO → UGS fallback)
│ │   ├─ NetDomain = DomainAssigner.GetDomainsByGameModes(gameMode)
│ │   └─ NetDefaultVesselType = gameData.selectedVesselClass (Squirrel)
│ │
│ ├─ ServerPlayerVesselInitializer.OnNetworkSpawn() [via NetcodeHooks]
│ │   ├─ Subscribe to OnPlayerNetworkSpawnedUlong
│ │   └─ ProcessPreExistingPlayers() — catches host Player already spawned
│ │
│ ├─ HandlePlayerNetworkSpawnedAsync(ownerClientId):
│ │   ├─ Wait preSpawnDelayMs (200ms) for NetworkVariables to sync
│ │   ├─ FindUnprocessedPlayerByOwnerClientId()
│ │   ├─ IsReadyToSpawn() — checks valid vessel type + non-empty name
│ │   └─ OnPlayerReadyToSpawnAsync(player) [virtual — Menu overrides]
│ │
│ ├─ ServerPlayerVesselInitializer.OnPlayerReadyToSpawnAsync():
│ │   ├─ SpawnVesselForPlayer():
│ │   │   ├─ vesselPrefabContainer.TryGetShipPrefab(vesselType)
│ │   │   ├─ Instantiate(shipNetworkObject)
│ │   │   ├─ GameObjectInjector.InjectRecursive() — Reflex DI
│ │   │   ├─ networkVessel.SpawnWithOwnership(clientId, destroyWithScene: true)
│ │   │   └─ player.NetVesselId = networkVessel.NetworkObjectId
│ │   ├─ ClientPlayerVesselInitializer.InitializePlayerAndVessel():
│ │   │   ├─ player.InitializeForMultiplayerMode(vessel)
│ │   │   ├─ vessel.Initialize(player)
│ │   │   ├─ ShipHelper.SetShipProperties(themeManagerData, vessel)
│ │   │   ├─ gameData.AddPlayer(player) — sets LocalPlayer, assigns spawn pose
│ │   │   ├─ CameraManager.SnapPlayerCameraToTarget() (if local user)
│ │   │   └─ gameData.InvokeClientReady() → raises OnClientReady
│ │   ├─ Wait postSpawnDelayMs (200ms) for vessel to replicate
│ │   └─ NotifyClients() — RPCs to non-host clients (N/A for menu)
│ │
│ └─ MenuServerPlayerVesselInitializer.OnPlayerReadyToSpawnAsync() [override]:
│     ├─ await base.OnPlayerReadyToSpawnAsync() — full chain above
│     └─ ActivateAutopilot(player):
│         ├─ player.StartPlayer() — activates vessel, enables input
│         ├─ player.Vessel.ToggleAIPilot(true)
│         ├─ player.InputController.SetPause(true)
│         └─ CameraManager.SetupEndCameraFollow(vessel.CameraFollowTarget)
│
│ MainMenuController.HandleMenuReady() [on OnClientReady]:
│ ├─ TransitionTo(Ready)  — menu is now fully interactive
│ └─ gameData.InitializeGame()
│
│ MenuCrystalClickHandler (optional play-from-menu):
│ ├─ Tap crystal → TransitionToGameplay:
│ │   ├─ Fade out menu UI
│ │   ├─ Vessel.ToggleAIPilot(false), InputController.SetPause(false)
│ │   └─ Retarget Cinemachine vCam to vessel follow target
│ └─ Center tap → TransitionToMenu:
│     ├─ InputController.SetPause(true), Vessel.ToggleAIPilot(true)
│     ├─ Restore Cinemachine to original menu targets
│     └─ Fade in menu UI
│
│ ScreenSwitcher
│ ├─ Caches IScreen components, lays out panels to viewport width
│ ├─ Navigates to HOME (or persisted ReturnToScreen)
│ └─ Screens: STORE(0), ARK(1), HOME(2), PORT(3), HANGAR(4)
```

#### Application State Machine

The `ApplicationStateMachine` (pure C# DI singleton) tracks the top-level application phase via `ApplicationStateDataVariable` (SOAP). Transitions are validated against a table; invalid transitions log warnings.

```
None → Bootstrapping → Authenticating → MainMenu → LoadingGame → InGame → GameOver
                                           ↑          ↑              ↑        │
                                           │          └──────────────┘        │
                                           └──────────────────────────────────┘
Special states (from any active state):
  Paused → (previous state)     — driven by ApplicationLifecycleManager.OnAppPaused
  Disconnected → MainMenu | Authenticating  — driven by NetworkMonitor.OnNetworkLost
  ShuttingDown                   — terminal, always allowed
```

Auto-wired SOAP transitions:
- `GameDataSO.OnSessionStarted` → `InGame`
- `GameDataSO.OnMiniGameEnd` → `GameOver`
- `ApplicationLifecycleManager.OnAppPaused` → `Paused` / restore
- `ApplicationLifecycleManager.OnAppQuitting` → `ShuttingDown`
- `NetworkMonitorData.OnNetworkLost` → `Disconnected`

#### SOAP Data Flow

```
AuthenticationServiceFacade (single writer)
        │ writes to
        ▼
AuthenticationDataVariable (ScriptableObject asset)
  └─ AuthenticationData
       ├─ .State        (NotInitialized → Initializing → Ready → SigningIn → SignedIn | Failed)
       ├─ .IsSignedIn   (bool)
       ├─ .PlayerId     (string)
       ├─ .OnSignedIn   ──► PlayerDataService.HandleSignedIn()
       │                 ──► MultiplayerSetup.EnsureHostStartedAsync()
       ├─ .OnSignedOut  ──► (listeners clear session state)
       └─ .OnSignInFailed ──► (listeners handle error UI)

ApplicationStateMachine (single writer)
        │ writes to
        ▼
ApplicationStateDataVariable (ScriptableObject asset)
  └─ ApplicationStateData
       ├─ .State         (ApplicationState enum)
       ├─ .PreviousState (ApplicationState enum)
       └─ .OnStateChanged ──► (ScriptableEventApplicationState — any subscriber)
```

Readers of auth state: `SplashToAuthFlow`, `AuthenticationSceneController`, `PlayerDataService`, `AuthenticationController`, `MultiplayerSetup`, `FriendsServiceFacade`.

Readers of app state: any system via `[Inject] ApplicationStateDataVariable` or `ApplicationStateData.OnStateChanged` SOAP event.

#### Key Files

| Role | File | Location |
|---|---|---|
| DI root / bootstrap orchestrator | `AppManager.cs` | `_Scripts/System/` |
| App state machine (single writer) | `ApplicationStateMachine.cs` | `_Scripts/System/` |
| Auth facade (single writer) | `AuthenticationServiceFacade.cs` | `_Scripts/System/` |
| Friends facade (single writer) | `FriendsServiceFacade.cs` | `_Scripts/System/` |
| Auth scene controller | `AuthenticationSceneController.cs` | `_Scripts/System/` |
| MonoBehaviour auth adapter | `AuthenticationController.cs` | `_Scripts/System/Systems/Authentication/` |
| Splash → auth routing | `SplashToAuthFlow.cs` | `_Scripts/System/` |
| Network monitor | `NetworkMonitor.cs` | `_Scripts/System/` |
| SOAP auth state | `AuthenticationData.cs` | `_Scripts/ScriptableObjects/SOAP/ScriptableAuthenticationData/` |
| SOAP auth variable | `AuthenticationDataVariable.cs` | `_Scripts/ScriptableObjects/SOAP/ScriptableAuthenticationData/` |
| SOAP network state | `NetworkMonitorData.cs` | `_Scripts/ScriptableObjects/SOAP/ScriptableAuthenticationData/` |
| SOAP app state | `ApplicationStateData.cs` | `_Scripts/ScriptableObjects/SOAP/ScriptableApplicationState/` |
| SOAP app state variable | `ApplicationStateDataVariable.cs` | `_Scripts/ScriptableObjects/SOAP/ScriptableApplicationState/` |
| ApplicationState enum | `ApplicationState.cs` | `_Scripts/Data/Enums/` |
| Friends data SO | `FriendsDataSO.cs` | `_Scripts/Utility/DataContainers/` |
| Player profile service | `PlayerDataService.cs` | `_Scripts/UI/Views/` |
| Auth SO asset instance | `AuthenticationData.asset` | `_SO_Assets/Authentication Data/` |
| Legacy PlayFab auth (deprecated) | `AuthenticationManager.cs` | `_Scripts/System/Playfab/Authentication/` |
| Legacy PlayFab UI (deprecated) | `AuthenticationView.cs` | `_Scripts/System/Playfab/Authentication/` |

#### Auth Patterns to Follow

- **Single writer**: Only `AuthenticationServiceFacade` writes to `AuthenticationData`. Scene controllers and UI read state and subscribe to SOAP events — they never mutate auth state directly.
- **UniTask + CancellationToken**: All auth async paths use `UniTask` with `CancellationTokenSource` tied to `OnEnable`/`OnDisable` lifecycle. No raw `Task.Delay` or manual elapsed-time polling.
- **Timeout via linked CTS**: Use `CancellationTokenSource.CreateLinkedTokenSource(ct)` + `CancelAfter()` for timeouts, not polling loops.
- **Button interactability**: Disable buttons during async operations instead of boolean `_isProcessing` guards.
- **Facade via DI**: Scene scripts get the facade via `[Inject]`, not by creating their own `AuthenticationController` GameObjects at runtime.

### Dependency Injection (Reflex)

The project uses Reflex DI with `AppManager` as the root `IInstaller`. All persistent services and shared assets are registered in `AppManager.InstallBindings()`:

**SO asset registration** (`RegisterValue`): `SceneNameListSO`, `GameDataSO`, `AuthenticationDataVariable`, `NetworkMonitorDataVariable`, `FriendsDataSO`, `HostConnectionDataSO`, `ApplicationLifecycleEventsContainerSO`, `ApplicationStateDataVariable`. These are project-level assets wired via inspector on AppManager.

**MonoBehaviour singleton registration** (`RegisterFactory`, Lazy): `GameSetting`, `AudioSystem`, `PlayerDataService`, `UGSStatsManager`, `CaptainManager`, `IAPManager`, `SceneLoader`, `ThemeManager`, `CameraManager`, `PostProcessingManager`, `StatsManager`, `SceneTransitionManager`. These use a lazy factory that prefers the serialized reference and falls back to a scene search at first injection time.

**Pure C# singleton registration** (`RegisterFactory`, Lazy): `AuthenticationServiceFacade`, `NetworkMonitor`, `FriendsServiceFacade`, `ApplicationStateMachine`.

#### DI Patterns to Follow

- **Use `[Inject]` for shared assets**: `GameDataSO`, `SceneNameListSO`, and other DI-registered assets should be accessed via `[Inject]`, not `[SerializeField]`. This eliminates manual inspector wiring and serialization drift.
- **Injection timing**: `[Inject]` fields are populated after `Awake()` but before `Start()`. Access injected fields in `Start()` or later — never in `Awake()`. If you need to subscribe to events in `OnEnable()`, use a deferred pattern: attempt in `OnEnable()`, retry with duplicate guards in `Start()`.
- **ContainerScope per scene**: Each scene that uses `[Inject]` must have a Reflex `ContainerScope` component (via the `ContainerScope.prefab` in `_Prefabs/CORE/`). The Bootstrap scene's scope is the root; other scenes get child scopes.

### Input Strategy Pattern

Platform-agnostic input via `Assets/_Scripts/Controller/IO/`:

- `IInputStrategy` — interface for all input handlers
- `BaseInputStrategy` — shared logic
- `KeyboardMouseInputStrategy`, `GamepadInputStrategy`, `TouchInputStrategy` — platform-specific implementations
- `InputController` — manages active strategy and input state
- `IInputStatus` / `InputStatus` — input state container
- Input strategies are swappable per platform/context at runtime

### Impact Effects Architecture

The collision/impact system (`Assets/_Scripts/Controller/ImpactEffects/`) uses a matrix of impactors and effect SOs:

**Impactor types** (all extend `ImpactorBase`): `VesselImpactor`, `NetworkVesselImpactor`, `PrismImpactor`, `ProjectileImpactor`, `SkimmerImpactor`, `MineImpactor`, `ExplosionImpactor`, `CrystalImpactor`, `ElementalCrystalImpactor`, `OmniCrystalImpactor`, `TeamCrystalImpactor`

**Effect SO pattern**: `[Impactor][Target]EffectSO` — e.g., `VesselExplosionByCrystalEffectSO`, `SkimmerAlignPrismEffectSO`, `SparrowDebuffByRhinoDangerPrismEffectSO`. Per-vessel effect asset instances exist for each vessel class. Organized into subdirectories: `Vessel Crystal Effects/`, `Vessel Prism Effects/`, `Vessel Explosion Effects/`, `Vessel Projectile Effects/`, `Vessel Skimmer Effects/`, `Skimmer Prism Effects/`, `Projectile Crystal Effects/`, `Projectile Prism Effects/`, `Projectile Mine Effects/`, `Projectile End Effects/`.

Key interfaces: `IImpactor` / `IImpactCollider`

**Forcefield Crackle (Skimmer)**: `SkimmerForcefieldCracklePrismEffectSO` (at `_Scripts/Controller/ImpactEffects/EffectsSO/Skimmer Prism Effects/`) is a shader-driven alternative to `SkimmerFXPrismEffectSO` that visualizes the Skimmer's invisible sphere collider on prism impacts. It computes the impact point via `Collider.ClosestPoint` between the prism box and skimmer sphere, projects it onto the sphere surface, and forwards the event (position + duration + intensity + radius) to a `ForcefieldCrackleController` MonoBehaviour on the vessel (`_Scripts/Controller/Vessel/ForcefieldCrackleController.cs`). The controller owns all visual parameters (colors, arc density/sharpness, ring thickness, ripple speed, fresnel) as serialized fields and feeds a ring buffer of up to 16 simultaneous impacts to the shader via MaterialPropertyBlock arrays each frame. `[ExecuteAlways]` allows edit-mode preview via `ForcefieldCrackleControllerEditor` (at `_Scripts/Editor/`). The shader's custom-function HLSL file `ForcefieldCrackle.hlsl` (at `Assets/Materials/Graphs/`) uses FBM-based electrical arcs with expanding wavefronts on a geodesic distance metric so arcs follow the sphere's curvature. All three code files use the `CosmicShore.Gameplay` namespace.

### Multiplayer / Netcode

The game uses Unity Netcode for GameObjects (`com.unity.netcode.gameobjects` 2.5.0) for multiplayer. Key files in `Assets/_Scripts/Controller/Multiplayer/`:

- `ServerPlayerVesselInitializer` — core server-side vessel spawner. Listens for `OnPlayerNetworkSpawnedUlong` SOAP events, waits for NetworkVariables to sync (`preSpawnDelayMs`), spawns the vessel prefab via `VesselPrefabContainer`, injects DI with `GameObjectInjector.InjectRecursive()`, then delegates initialization to `ClientPlayerVesselInitializer`. Tracks processed players by `NetworkObjectId` (not `OwnerClientId`, since AI shares the host's). Uses `NetcodeHooks` (not direct `NetworkBehaviour` inheritance) for spawn/despawn hooks. `ProcessPreExistingPlayers()` catches host Player objects spawned before the initializer loaded. `shutdownNetworkOnDespawn` toggle: `true` for game scenes, `false` for Menu_Main.
- `ClientPlayerVesselInitializer` — common player-vessel pair initialization (extends `NetworkBehaviour`). Server path: called directly by `ServerPlayerVesselInitializer`. Client path: receives RPCs (`InitializeAllPlayersAndVessels_ClientRpc` for new clients, `InitializeNewPlayerAndVessel_ClientRpc` for existing clients). Queues pending `(playerNetId, vesselNetId)` pairs when RPCs arrive before objects replicate — resolved reactively via `OnPlayerNetworkSpawnedUlong` + `OnVesselNetworkSpawned` SOAP events (zero `WaitUntil` polling). `InitializePair()` calls `player.InitializeForMultiplayerMode(vessel)`, `vessel.Initialize(player)`, `ShipHelper.SetShipProperties()`, `gameData.AddPlayer()`, and fires `gameData.InvokeClientReady()` for the local user.
- `ServerPlayerVesselInitializerWithAI` — extends `ServerPlayerVesselInitializer`. Spawns server-owned AI players **before** `base.OnNetworkSpawn()` subscribes to events, so AI spawn events are harmlessly missed. Marks all AI players in `_processedPlayers` so the base class skips them. Picks AI vessel type from `SO_GameList` captains (falls back to Sparrow). Configures `AIPilot` with game-mode-aware seeking and skill level. **AI players and vessels are spawned with `destroyWithScene: false`** so they survive the client's end-of-frame scene-transition cleanup — without this the client's scene-load message batches with the AI spawn messages on the same network tick and the client destroys the just-spawned AI NetworkObjects (surfacing as `[Invalid Destroy]` errors on the host and invisible AI on clients). Human vessels are unaffected because `ServerPlayerVesselInitializer` delays spawn by `preSpawnDelayMs` (200 ms), pushing them into a later tick. Because AI no longer gets scene-unload cleanup for free, `MultiplayerMiniGameControllerBase.ExecuteSceneReloadReplay()` explicitly despawns all AI players and vessels before the scene reload; the existing cleanup paths (`SceneLoader.ClearPlayerVesselReferences` for Game→Menu, `NetworkManager.Shutdown` on disconnect) already explicit-despawn AI, so AI does not leak into Menu_Main.
- `MenuServerPlayerVesselInitializer` — extends `ServerPlayerVesselInitializer`. Overrides `OnPlayerReadyToSpawnAsync()` to call `base` then `ActivateAutopilot()`: `player.StartPlayer()`, `Vessel.ToggleAIPilot(true)`, `InputController.SetPause(true)`, `CameraManager.SetupEndCameraFollow(vessel.CameraFollowTarget)`. Game data configuration (vessel class, player count, intensity) is handled by `MainMenuController` — this class only handles the network spawn chain and autopilot activation.
- `MenuCrystalClickHandler` — toggles between menu mode (Cinemachine crystal camera + autopilot) and gameplay mode (Cinemachine follows vessel + player control) on Menu_Main. Tap crystal → fade out menu UI, disable autopilot, enable player input, retarget Cinemachine vCam to vessel follow target. Center tap → restore autopilot and menu UI.
- `MultiplayerSetup` — bridges authentication → Netcode host lifecycle. `EnsureHostStarted()` registers Netcode callbacks and calls `nm.StartHost()` exactly once (guarded by `_hostStartInProgress` flag). For multiplayer games: shuts down local host, queries/creates/joins UGS Multiplayer sessions with Relay transport, handles race conditions on session joins. Session properties: `gameMode` (String1), `maxPlayers` (String2). Connection approval auto-creates player objects.
- `NetworkStatsManager` — network health monitoring via `NetworkMonitorData` SOAP type
- `DomainAssigner` — static team pool manager. `Initialize()` fills pool with `[Jade, Ruby, Gold]` (excludes None, Unassigned, Blue). `GetDomainsByGameModes()` picks a random unique domain per player (returns `Domains.Jade` for co-op modes). **Must** be called per session start to prevent duplicate/swapped domains.

Scene loading for multiplayer is handled by `SceneLoader` (`_Scripts/System/SceneLoader.cs`), which extends `MonoBehaviour` and auto-selects local vs network scene loading based on whether a host/server is running. `SceneLoader` lives in Bootstrap (DontDestroyOnLoad) and subscribes to SOAP events in code. Game config sync to clients is handled by `MultiplayerMiniGameControllerBase.SyncGameConfigToClients_ClientRpc()` in `OnNetworkSpawn()`.

**MPPM / connected-client guard**: `LaunchGame`, `ReturnToMainMenu`, and `HandleActiveSessionEnd` all check `if (nm.IsListening && !nm.IsServer) return` after visual setup (fade-to-black, state transition, `OnClientReady` subscription) but before `LoadSceneAsync()`. In Multiplayer Play Mode, SOAP events on the shared `GameDataSO` fire on every virtual player, so without this guard a client's `SceneLoader` would call `SceneManager.LoadScene()` locally and race the server's Netcode scene load — destroying AI NetworkObjects before they replicate. The guard lets connected clients keep the smooth visual transitions while deferring the actual scene load to the server's Netcode scene management.

`VesselStatus` extends `NetworkBehaviour`. Multiplayer game modes can also run solo with AI opponents via the AI Profile system.

#### Player Spawning Architecture

The player spawning system uses a unified multiplayer-first pipeline — menu vessels spawn through the same Netcode + SOAP pipeline as gameplay vessels.

**Spawning class hierarchy:**

```
ServerPlayerVesselInitializer (MonoBehaviour + NetcodeHooks)
├── MenuServerPlayerVesselInitializer (Menu_Main: adds autopilot)
└── ServerPlayerVesselInitializerWithAI (game scenes: pre-spawns AI)

ClientPlayerVesselInitializer (NetworkBehaviour)
└── Used by all ServerPlayerVesselInitializer variants

PlayerSpawner / VesselSpawner (single-player, non-networked path)
└── PlayerSpawnerAdapterBase → MiniGamePlayerSpawnerAdapter, VolumeTestPlayerSpawnerAdapter
```

**Player (`NetworkBehaviour`) NetworkVariables:**

| Variable | Read | Write | Purpose |
|---|---|---|---|
| `NetDefaultVesselType` | Everyone | Owner | Vessel class selection |
| `NetDomain` | Everyone | Server | Team assignment (via `DomainAssigner`) |
| `NetName` | Everyone | Owner | Display name (3-tier fallback: PlayerDataService → GameDataSO cache → UGS PlayerName) |
| `NetVesselId` | Everyone | Server | Linked vessel's `NetworkObjectId` |
| `NetIsAI` | Everyone | Server | AI flag |
| `NetAvatarId` | Everyone | Owner | Profile avatar ID |

**Player identity resolution** (`Player.OnNetworkSpawn()`):
1. `PlayerDataService.CurrentProfile.displayName` (live Cloud Save profile)
2. `GameDataSO.LocalPlayerDisplayName` (cached by `PlayerDataService.HandleProfileChanged`)
3. `AuthenticationService.PlayerName` with `#XXXX` suffix stripped (last resort)

**SOAP event flow for spawning:**

```
Player.OnNetworkSpawn()
  ├─ gameData.Players.Add(this)
  ├─ Raise OnPlayerNetworkSpawnedUlong(OwnerClientId)
  │   └─ ServerPlayerVesselInitializer.HandlePlayerNetworkSpawned()
  │       ├─ Wait preSpawnDelayMs (200ms) for NetworkVariables
  │       ├─ SpawnVesselForPlayer():
  │       │   ├─ vesselPrefabContainer.TryGetShipPrefab(vesselType)
  │       │   ├─ Instantiate + GameObjectInjector.InjectRecursive()
  │       │   ├─ SpawnWithOwnership(clientId)
  │       │   └─ player.NetVesselId = vessel.NetworkObjectId
  │       ├─ ClientPlayerVesselInitializer.InitializePlayerAndVessel()
  │       │   ├─ player.InitializeForMultiplayerMode(vessel)
  │       │   ├─ vessel.Initialize(player)
  │       │   ├─ ShipHelper.SetShipProperties()
  │       │   ├─ gameData.AddPlayer() → sets LocalPlayer, assigns spawn pose
  │       │   └─ gameData.InvokeClientReady() (if IsLocalUser)
  │       ├─ Wait postSpawnDelayMs (200ms) for replication
  │       └─ NotifyClients() → RPCs to non-host clients
  │
  └─ [Client side: SOAP events drive pending pair resolution]
      ├─ OnPlayerNetworkSpawnedUlong → ProcessPendingPairs()
      └─ OnVesselNetworkSpawned → ProcessPendingPairs()
```

**Menu_Main spawning specifics** (via `MainMenuController` + `MenuServerPlayerVesselInitializer`):

**Host path (initial menu load):**

| Step | Actor | Action |
|---|---|---|
| 1 | `MainMenuController.Start()` | Configure game data: vessel=Squirrel, players=3, intensity=1, spawn positions |
| 2 | `MainMenuController` | `DomainAssigner.Initialize()`, `gameData.InitializeGame()` |
| 3 | `Player.OnNetworkSpawn()` | Host Player (spawned in Auth scene) fires `OnPlayerNetworkSpawnedUlong` |
| 4 | `ServerPlayerVesselInitializer` | `ProcessPreExistingPlayers()` catches the already-spawned host Player |
| 5 | `ServerPlayerVesselInitializer` | Spawns vessel, initializes pair |
| 6 | `MenuServerPlayerVesselInitializer` | Override: `ActivateAutopilot()` — AI on, input paused |
| 7 | `ClientPlayerVesselInitializer` | `InvokeClientReady()` for local user |
| 8 | `MainMenuController` | `HandleMenuReady()` → `TransitionTo(Ready)` — menu interactive |

**Client path (joining via party invite):**

| Step | Actor | Action |
|---|---|---|
| 1 | `PartyInviteController` | `AcceptInviteAsync()` — shutdown local host, join Relay party session |
| 2 | `PartyInviteController` | `WaitForClientConnectionAsync()` + `WaitForSceneLoadAsync()` — Menu_Main syncs from host |
| 3 | `Player.OnNetworkSpawn()` | Client Player fires `OnPlayerNetworkSpawnedUlong(clientId)` |
| 4 | Host `ServerPlayerVesselInitializer` | `HandlePlayerNetworkSpawned(clientId)` — spawns vessel, initializes pair |
| 5 | Host `MenuServerPlayerVesselInitializer` | `ActivateAutopilot()` — AI on, input paused on host side |
| 6 | Host `ServerPlayerVesselInitializer` | `NotifyClients()` — RPCs all player-vessel pairs to new client |
| 7 | Client `ClientPlayerVesselInitializer` | Receives `InitializeAllPlayersAndVessels_ClientRpc`, queues pairs |
| 8 | Client `ClientPlayerVesselInitializer` | SOAP events resolve pairs → `InitializePair()` → `InvokeClientReady()` for local user |
| 9 | Client `MainMenuController` | `HandleMenuReady()` → `SetNonOwnerPlayersActiveInNewClient()` activates host's vessel |
| 10 | Client `MainMenuController` | `ActivateLocalPlayerAutopilot()` — ensures client vessel starts in autopilot |

**`MainMenuController` sub-state machine** (`MainMenuState` enum):

```
None(0) → Initializing(1) → Ready(2) → LaunchingGame(3)
                ↑                            │
                └────────────────────────────┘
```

- `None → Initializing`: `Start()` — configures game data, fires `OnInitializeGame`
- `Initializing → Ready`: `OnClientReady` SOAP event (autopilot vessel spawned and active)
- `Ready → LaunchingGame`: `OnLaunchGame` SOAP event (player selected a game mode)

**Single-player spawning path** (arcade/campaign, non-networked):

```
MiniGamePlayerSpawnerAdapter.InitializeGame() [on OnInitializeGame]
  ├─ PlayerSpawner.SpawnPlayerAndShip(data):
  │   ├─ Instantiate player prefab + DI inject
  │   ├─ VesselSpawner.SpawnShip(vesselClass) → Instantiate + DI inject
  │   ├─ player.InitializeForSinglePlayerMode(data, vessel)
  │   └─ vessel.Initialize(player)
  ├─ gameData.AddPlayer(player)
  └─ SpawnDefaultPlayersAndAddToGameData() (AI opponents)
```

#### Key Files — Player Spawning

| Role | File | Location |
|---|---|---|
| Server vessel spawner (base) | `ServerPlayerVesselInitializer.cs` | `_Scripts/Controller/Multiplayer/` |
| Client pair initializer | `ClientPlayerVesselInitializer.cs` | `_Scripts/Controller/Multiplayer/` |
| Server AI spawner | `ServerPlayerVesselInitializerWithAI.cs` | `_Scripts/Controller/Multiplayer/` |
| Menu autopilot spawner | `MenuServerPlayerVesselInitializer.cs` | `_Scripts/Controller/Multiplayer/` |
| Menu play-from-menu toggle | `MenuCrystalClickHandler.cs` | `_Scripts/Controller/Multiplayer/` |
| NetworkManager lifecycle | `MultiplayerSetup.cs` | `_Scripts/Controller/Multiplayer/` |
| Team assignment | `DomainAssigner.cs` | `_Scripts/Controller/Multiplayer/` |
| Player NetworkBehaviour | `Player.cs` | `_Scripts/Controller/Player/` |
| Player interface | `IPlayer.cs` | `_Scripts/Controller/Player/` |
| Single-player spawner | `PlayerSpawner.cs` | `_Scripts/Controller/Player/` |
| Single-player adapter base | `PlayerSpawnerAdapterBase.cs` | `_Scripts/Controller/Player/` |
| Arcade spawn adapter | `MiniGamePlayerSpawnerAdapter.cs` | `_Scripts/Controller/Player/` |
| Vessel instantiation | `VesselSpawner.cs` | `_Scripts/Controller/Vessel/` |
| Vessel prefab mapping | `VesselPrefabContainer.cs` | `_Scripts/ScriptableObjects/SOAP/` |
| NetcodeHooks adapter | `NetcodeHooks.cs` | `_Scripts/Utility/Network/` |
| Game data + SOAP events | `GameDataSO.cs` | `_Scripts/Utility/DataContainers/` |
| Menu scene controller | `MainMenuController.cs` | `_Scripts/System/` |
| Menu sub-state enum | `MainMenuState.cs` | `_Scripts/Data/Enums/` |

### Party / Invite Lobby System

The invite lobby system enables multiplayer freestyle roaming in Menu_Main. Players discover each other via a shared **presence lobby** (UGS session without Relay) and send invites. Accepting an invite transitions the recipient from local host to Relay client, connecting to the inviter's party session. The host's `MenuServerPlayerVesselInitializer` spawns a vessel for the joining client with autopilot enabled.

#### Two-Level Session Architecture

| Layer | Purpose | Relay? | Max Players |
|---|---|---|---|
| **Presence Lobby** | Player discovery, invite property exchange | No (lobby-only) | 100 |
| **Party Session** | Actual gameplay networking via Relay | Yes (`WithRelayNetwork()`) | 4 |

The presence lobby is a lobby-only UGS session (no Relay transport) that coexists safely with an active NetworkManager. Players set their own player properties to send invites — no host privilege required.

#### Core Services

- **`HostConnectionService`** (`_Scripts/Controller/Party/`) — Singleton + `DontDestroyOnLoad`. Single-writer to `HostConnectionDataSO`. Auto-joins the presence lobby on auth sign-in. Periodically refreshes (3s) to sync online player list and detect incoming invites. Manages party session creation (with Relay) for actual gameplay.
- **`PartyInviteController`** (`_Scripts/Controller/Party/`) — Singleton + `DontDestroyOnLoad`. Orchestrates Netcode transitions: host→client for accepting invites, local→Relay for sending first invite. Uses `UniTask` + `CancellationToken` with configurable timeouts. Recovers from failed transitions by restarting local host.
- **`FriendsInitializer`** (`_Scripts/Controller/Party/`) — MonoBehaviour bridge. Initializes `FriendsServiceFacade` on auth sign-in. Manages presence updates for scene transitions.

#### SOAP Data Containers

- **`HostConnectionDataSO`** (`_Scripts/Utility/DataContainers/`) — Central data container for all party/lobby state. SOAP events: `OnHostConnectionEstablished`, `OnHostConnectionLost`, `OnPartyMemberJoined`, `OnPartyMemberLeft`, `OnPartyMemberKicked`, `OnInviteReceived`, `OnInviteSent`, `OnPartyJoinCompleted`. SOAP lists: `OnlinePlayers`, `PartyMembers`. Registered in AppManager DI.
- **`FriendsDataSO`** (`_Scripts/Utility/DataContainers/`) — Friends service state. SOAP lists: `Friends`, `IncomingRequests`, `OutgoingRequests`, `BlockedPlayers`. SOAP events: `OnFriendAdded`, `OnFriendRemoved`, `OnFriendRequestReceived`, `OnFriendsServiceReady`.

#### SOAP Types (PartyData)

Location: `_Scripts/ScriptableObjects/SOAP/ScriptablePartyData/`

| Type | Purpose |
|---|---|
| `PartyInviteData` | Immutable invite payload: hostPlayerId, partySessionId, hostDisplayName, hostAvatarId |
| `PartyPlayerData` | Immutable player identity: playerId, displayName, avatarId (equality by playerId) |
| `ScriptableEventPartyInviteData` | SOAP event for invite notifications |
| `ScriptableEventPartyPlayerData` | SOAP event for party member changes |
| `ScriptableListPartyPlayerData` | SOAP reactive list for online players / party members |
| `EventListenerPartyInviteData` | MonoBehaviour listener for invite events |
| `EventListenerPartyPlayerData` | MonoBehaviour listener for party member events |

#### Invite Flow

```
Sender presses "+" on empty party slot
  ├─ PartyAreaPanel.OnAddSlotPressed() / PartyArcadeView.OnAddSlotPressed()
  ├─ PartyInviteController.TransitionToPartyHostAsync() [if first invite]
  │   ├─ CleanUpCurrentSession() — despawn menu vessels
  │   ├─ ShutdownNetworkManagerAsync() — shutdown local host
  │   ├─ HostConnectionService.CreatePartySessionPublicAsync() — Relay party session
  │   └─ Load Menu_Main as network scene
  ├─ OnlinePlayersPanel.Show() — display all online players
  └─ User clicks "+" on a player entry
      └─ HostConnectionService.SendInviteAsync(targetPlayerId)
          ├─ Sets own player properties: invite_target, invite_data
          └─ OnInviteSent SOAP event

Recipient's refresh loop detects invite
  ├─ HostConnectionService.RefreshAsync() [every 3s]
  │   └─ Scans all lobby players' properties for invite_target matching local ID
  ├─ OnInviteReceived SOAP event raised
  ├─ PartyInviteNotificationPanel shows Accept/Decline
  └─ User presses Accept
      └─ PartyInviteController.AcceptInviteAsync(invite)
          ├─ CleanUpCurrentSession()
          ├─ ShutdownNetworkManagerAsync() — shutdown local host
          ├─ HostConnectionService.AcceptInviteAsync() — join party session via Relay
          ├─ WaitForClientConnectionAsync() — poll nm.IsConnectedClient
          ├─ WaitForSceneLoadAsync() — wait for Menu_Main scene sync
          ├─ OnPartyJoinCompleted SOAP event
          └─ Host's MenuServerPlayerVesselInitializer spawns vessel + autopilot
```

#### Multiplayer Freestyle Flight in Menu_Main

After a client joins via party invite, both host and client spawn with vessels and can fly together. The system uses a unified Netcode + SOAP pipeline — no special-case code for menu multiplayer.

**Client join vessel spawn chain:**

```
Client joins party session via Relay
  │
  ├─ Client's Player.OnNetworkSpawn()
  │   ├─ gameData.Players.Add(this)
  │   ├─ Raise OnPlayerNetworkSpawnedUlong(clientId)
  │   └─ Set NetDefaultVesselType, NetName, NetDomain
  │
  ├─ Host's ServerPlayerVesselInitializer receives OnPlayerNetworkSpawnedUlong(clientId)
  │   ├─ Wait preSpawnDelayMs (200ms) for NetworkVariables to sync
  │   ├─ SpawnVesselForPlayer(clientId) → vessel spawned + DI injection
  │   ├─ ClientPlayerVesselInitializer.InitializePlayerAndVessel()
  │   ├─ MenuServerPlayerVesselInitializer.ActivateAutopilot(player)
  │   │   ├─ player.StartPlayer()
  │   │   ├─ player.Vessel.ToggleAIPilot(true)
  │   │   └─ player.InputController.SetPause(true)
  │   ├─ Wait postSpawnDelayMs (200ms) for replication
  │   └─ NotifyClients():
  │       ├─ InitializeAllPlayersAndVessels_ClientRpc → new client (all pairs)
  │       └─ InitializeNewPlayerAndVessel_ClientRpc → existing clients (new pair only)
  │
  ├─ Client's ClientPlayerVesselInitializer receives RPC
  │   ├─ Queues pending (playerNetId, vesselNetId) pairs
  │   ├─ SOAP events (OnPlayerNetworkSpawnedUlong, OnVesselNetworkSpawned) → ProcessPendingPairs()
  │   ├─ InitializePair() for each resolved pair
  │   └─ gameData.InvokeClientReady() for local user → fires OnClientReady
  │
  └─ Client's MainMenuController.HandleMenuReady()
      ├─ TransitionTo(Ready)
      ├─ ActivateMenuCamera()
      ├─ ActivateLocalPlayerAutopilot() — ensures client vessel starts in autopilot
      └─ gameData.SetNonOwnerPlayersActiveInNewClient() — activates host's vessel on client screen
```

**Freestyle toggle (autopilot ↔ player control):**

`MenuCrystalClickHandler.ToggleTransition()` lets each player independently switch between autopilot and freestyle flight:

| Guard | Purpose |
|---|---|
| `localPlayer.IsLocalUser` | Only the locally-owned vessel can be toggled |
| `IsMultiplayerSession()` (`ConnectedClientsIds.Count > 1`) | Skips `Time.timeScale` changes in multiplayer to avoid freezing remote players |
| `_isTransitioning` | Prevents concurrent toggle transitions |

Each client has its own Cinemachine camera following its own vessel. No network syncing of freestyle state is needed — each client independently toggles their own vessel via `MenuFreestyleEventsContainerSO` SOAP events.

**What works in multiplayer menu:**
- Both players spawn with network-owned vessels
- Both vessels visible and active on all clients' screens
- Each player independently toggles autopilot ↔ freestyle control
- Independent Cinemachine cameras per client — no conflicts
- Network ownership prevents cross-control of vessels

**Limitations:**
- Party size bounded by `HostConnectionDataSO.MaxPartySlots`
- No AI backfill in menu — `MenuServerPlayerVesselInitializer` does not pre-spawn AI opponents (unlike `ServerPlayerVesselInitializerWithAI` in game scenes)
- Freestyle state is local-only — other players cannot see whether you are in autopilot or freestyle mode (vessel behavior replicates, but the mode label does not)

#### UI Components

| Component | File | Purpose |
|---|---|---|
| `PartyAreaPanel` | `_Scripts/UI/Elements/` | 3-slot party panel for Home screen |
| `PartyArcadeView` | `_Scripts/UI/Views/` | 4-slot party panel for Arcade screen with friends button |
| `PartySlotView` | `_Scripts/UI/Views/` | Single slot: occupied (avatar + name) or empty ("+" button) |
| `OnlinePlayersPanel` | `_Scripts/UI/Views/` | Modal listing all online players with invite + add friend buttons |
| `OnlinePlayerEntry` | `_Scripts/UI/Views/` | Individual row in online players panel |
| `FriendsPanel` | `_Scripts/UI/Views/` | Tabbed panel: friends list, requests, add friend |
| `FriendEntryView` | `_Scripts/UI/Views/` | Friend row with online status, invite, remove buttons |
| `FriendRequestEntryView` | `_Scripts/UI/Views/` | Request row: incoming (accept/decline) or outgoing (cancel) |
| `AddFriendPanel` | `_Scripts/UI/Views/` | Text input for friend requests by name |
| `PartyInviteNotificationPanel` | `_Scripts/UI/Screens/` | Invite popup with accept/decline + auto-decline timeout |

#### SO Assets

Location: `_SO_Assets/Host Connection Data/`

| Asset | Type |
|---|---|
| `HostConnectionData.asset` | `HostConnectionDataSO` |
| `Event_HostConnectionEstablished.asset` | `ScriptableEventNoParam` |
| `Event_HostConnectionLost.asset` | `ScriptableEventNoParam` |
| `Event_InviteReceived.asset` | `ScriptableEventPartyInviteData` |
| `Event_InviteSent.asset` | `ScriptableEventPartyPlayerData` |
| `Event_PartyMemberJoined.asset` | `ScriptableEventPartyPlayerData` |
| `Event_PartyMemberLeft.asset` | `ScriptableEventPartyPlayerData` |
| `Event_PartyMemberKicked.asset` | `ScriptableEventPartyPlayerData` |
| `Event_PartyJoinCompleted.asset` | `ScriptableEventNoParam` |
| `List_OnlinePlayers.asset` | `ScriptableListPartyPlayerData` |
| `List_PartyMembers.asset` | `ScriptableListPartyPlayerData` |

#### Prefabs

Location: `_Prefabs/UI Elements/Panels/Party/`

Run `Tools > Cosmic Shore > Create Party Prefabs` in Unity Editor to generate missing prefabs with auto-wired component references. SO data container references (`HostConnectionDataSO`, `FriendsDataSO`, `SO_ProfileIconList`) must be wired manually in the inspector after creation.

#### Scene Setup Checklist (Menu_Main)

1. **Persistent GameObjects** (in Bootstrap scene, `DontDestroyOnLoad`):
   - `HostConnectionService` + `PartyInviteController` + `FriendsInitializer` on same GameObject
   - Wire `HostConnectionDataSO`, `AuthenticationDataVariable` in inspector
2. **AppManager** (Bootstrap scene):
   - Assign `HostConnectionData.asset` to `hostConnectionData` field
3. **Menu_Main scene UI**:
   - `PartyAreaPanel` or `PartyArcadeView` as child of Home/Arcade screen
   - `OnlinePlayersPanel`, `FriendsPanel`, `PartyInviteNotificationPanel` as children of party area (start inactive)
   - Wire `HostConnectionData.asset` into all party UI components
   - Wire `FriendsData.asset` into `FriendsPanel` and `PartyArcadeView`
   - Wire `OnlinePlayerEntry` prefab into `OnlinePlayersPanel.playerEntryPrefab`
   - Wire `FriendEntryView` prefab into `FriendsPanel.friendEntryPrefab`
   - Wire `FriendRequestEntryView` prefab into `FriendsPanel.friendRequestEntryPrefab`

#### Party System Patterns to Follow

- **Single writer**: Only `HostConnectionService` writes to `HostConnectionDataSO`. UI reads via SOAP events/lists.
- **Player properties for invites**: Use per-player properties (not session properties) so any lobby member can send invites.
- **Lobby-only session**: Presence lobby uses no Relay — coexists with active NetworkManager.
- **UniTask + CancellationToken**: All async transitions use `UniTask` with linked CTS for timeouts.
- **Dedup guard**: `_lastFiredInvite` prevents re-firing the same invite on repeated refreshes.
- **Client autopilot**: `MainMenuController.HandleMenuReady()` calls `ActivateLocalPlayerAutopilot()` for the local player's vessel, ensuring both host and joining clients start in autopilot mode. For hosts this is redundant with `MenuServerPlayerVesselInitializer.ActivateAutopilot()`, but for remote clients it is the primary activation path.
- **Non-owner vessel activation**: `MainMenuController.HandleMenuReady()` calls `gameData.SetNonOwnerPlayersActiveInNewClient()` so joining clients see and render existing players' vessels.
- **Local-only freestyle toggle**: `MenuCrystalClickHandler` toggles autopilot ↔ freestyle per-client with `IsLocalUser` guard. No network RPC needed — vessel behavior replicates automatically via Netcode.
- **TimeScale safety**: `MenuCrystalClickHandler.IsMultiplayerSession()` (`ConnectedClientsIds.Count > 1`) prevents `Time.timeScale` changes in multiplayer, which would freeze all local rendering including other players' vessels.

### Friend System

The friend system uses **Unity Gaming Services (UGS) Friends SDK** for relationship management and presence. It follows the same single-writer / multi-reader SOAP pattern as auth and party systems.

#### Architecture

```
FriendsServiceFacade (single writer, pure C# DI singleton)
        │ writes to
        ▼
FriendsDataSO (ScriptableObject asset)
  ├─ Lists:
  │   ├─ Friends              (ScriptableListFriendData)
  │   ├─ IncomingRequests      (ScriptableListFriendData)
  │   ├─ OutgoingRequests      (ScriptableListFriendData)
  │   └─ BlockedPlayers        (ScriptableListFriendData)
  │
  └─ Events:
      ├─ OnFriendAdded         ──► FriendsPanel refreshes friend list
      ├─ OnFriendRemoved       ──► FriendsPanel refreshes friend list
      ├─ OnFriendRequestReceived ──► FriendsPanel shows request tab badge
      └─ OnFriendsServiceReady ──► (subscribers know the service is usable)
```

#### Initialization Flow

```
Auth Sign-In (OnSignedIn SOAP event)
       │
       ▼
FriendsInitializer.HandleSignedInEvent()
       │
       └─► FriendsServiceFacade.InitializeAsync()
            ├─ UGS FriendsService.InitializeAsync()
            ├─ WireEvents():
            │   ├─ RelationshipAdded → OnRelationshipAdded()
            │   ├─ RelationshipDeleted → OnRelationshipDeleted()
            │   └─ PresenceUpdated → OnPresenceUpdated()
            ├─ SyncAllRelationships() → populate all 4 SOAP lists
            ├─ FriendsDataSO.IsInitialized = true
            ├─ OnFriendsServiceReady.Raise()
            └─ SetPresence(Online, "In Menu")
```

#### SOAP Types (FriendData)

Location: `_Scripts/ScriptableObjects/SOAP/ScriptableFriendData/`

| Type | Purpose |
|---|---|
| `FriendData` | Immutable struct: `PlayerId`, `DisplayName`, `Availability` (int), `ActivityStatus` (string). Identity + presence for a single friend. |
| `FriendPresenceActivity` | `[DataContract]` class for rich UGS presence payload: `Status`, `Scene`, `VesselClass`, `PartySessionId`. Serialized by the Friends SDK. |
| `ScriptableEventFriendData` | SOAP event channel for friend added/removed notifications |
| `ScriptableListFriendData` | SOAP reactive list backing `Friends`, `IncomingRequests`, `OutgoingRequests`, `BlockedPlayers` in `FriendsDataSO` |
| `EventListenerFriendData` | Inspector-wirable MonoBehaviour listener for `ScriptableEventFriendData` |

#### FriendsServiceFacade API

The facade (`_Scripts/System/FriendsServiceFacade.cs`) exposes these operations. All mutating methods call `SyncAllRelationships()` after the UGS SDK call to update SOAP lists.

| Method | UGS SDK Call | Effect |
|---|---|---|
| `InitializeAsync()` | `FriendsService.InitializeAsync()` | Wire events, sync all lists, raise `OnFriendsServiceReady` |
| `SendFriendRequestByNameAsync(name)` | `AddFriendByNameAsync(name)` | Adds to `OutgoingRequests` list |
| `SendFriendRequestAsync(playerId)` | `AddFriendAsync(playerId)` | Adds to `OutgoingRequests` list |
| `AcceptFriendRequestAsync(playerId)` | `AddFriendAsync(playerId)` | Moves from `IncomingRequests` to `Friends`, raises `OnFriendAdded` |
| `DeclineFriendRequestAsync(playerId)` | `DeleteIncomingFriendRequestAsync(playerId)` | Removes from `IncomingRequests` |
| `CancelFriendRequestAsync(playerId)` | `DeleteOutgoingFriendRequestAsync(playerId)` | Removes from `OutgoingRequests` |
| `RemoveFriendAsync(playerId)` | `DeleteFriendAsync(playerId)` | Removes from `Friends`, raises `OnFriendRemoved` |
| `BlockPlayerAsync(playerId)` | `AddBlockAsync(playerId)` | Removes any relationship, adds to `BlockedPlayers` |
| `UnblockPlayerAsync(playerId)` | `DeleteBlockAsync(playerId)` | Removes from `BlockedPlayers` |
| `SetPresenceAsync(availability, activity)` | `SetPresenceAsync(...)` | Updates local player's presence for friends to see |
| `SetAvailabilityAsync(availability)` | `SetPresenceAvailabilityAsync(...)` | Updates availability only |
| `RefreshAsync()` | `ForceRelationshipsRefreshAsync()` | Full server refresh of all lists |
| `IsFriend(playerId)` | (local query) | Checks `FriendsDataSO.Friends` list |
| `IsBlocked(playerId)` | (local query) | Checks `FriendsDataSO.BlockedPlayers` list |

#### Presence Management

`FriendsInitializer` (`_Scripts/Controller/Party/FriendsInitializer.cs`) manages the local player's presence state across scene transitions:

| Trigger | Availability | Activity Status |
|---|---|---|
| Auth sign-in / enter menu | `Online` | `"In Menu"` (scene: `Menu_Main`) |
| Enter game scene | `Busy` | `"In Game"` (scene name, vessel class, party session ID) |
| App shutdown / `OnDestroy` | `Offline` | — |

Friends see presence updates via UGS SDK's `PresenceUpdated` event → `FriendsServiceFacade.OnPresenceUpdated()` → `SyncAllRelationships()` → `FriendData.Availability` updated in SOAP lists → `FriendEntryView` updates online status indicator color.

#### Friend UI Components

| Component | File | Purpose |
|---|---|---|
| `FriendsPanel` | `_Scripts/UI/Views/FriendsPanel.cs` | Tabbed panel with 3 tabs: Friends List, Requests (incoming + outgoing), Add Friend. Reads `FriendsDataSO` SOAP lists. |
| `FriendEntryView` | `_Scripts/UI/Views/FriendEntryView.cs` | Single friend row: display name, online status color indicator, [Invite to Party] button (→ `HostConnectionService.SendInviteAsync`), [Remove] button (→ `FriendsServiceFacade.RemoveFriendAsync`). |
| `FriendRequestEntryView` | `_Scripts/UI/Views/FriendRequestEntryView.cs` | Single request row: incoming shows [Accept]/[Decline], outgoing shows [Cancel]. Delegates to `FriendsServiceFacade`. |
| `AddFriendPanel` | `_Scripts/UI/Views/AddFriendPanel.cs` | Text input + [Send] button. Uses `[Inject] FriendsServiceFacade` to call `SendFriendRequestByNameAsync`. |

#### Friend System Key Files

| Role | File | Location |
|---|---|---|
| Friends facade (single writer) | `FriendsServiceFacade.cs` | `_Scripts/System/` |
| MonoBehaviour bridge / presence | `FriendsInitializer.cs` | `_Scripts/Controller/Party/` |
| SOAP data container | `FriendsDataSO.cs` | `_Scripts/Utility/DataContainers/` |
| Friend identity struct | `FriendData.cs` | `_Scripts/ScriptableObjects/SOAP/ScriptableFriendData/` |
| Rich presence payload | `FriendPresenceActivity.cs` | `_Scripts/ScriptableObjects/SOAP/ScriptableFriendData/` |
| SOAP event channel | `ScriptableEventFriendData.cs` | `_Scripts/ScriptableObjects/SOAP/ScriptableFriendData/` |
| SOAP reactive list | `ScriptableListFriendData.cs` | `_Scripts/ScriptableObjects/SOAP/ScriptableFriendData/` |
| SOAP MonoBehaviour listener | `EventListenerFriendData.cs` | `_Scripts/ScriptableObjects/SOAP/ScriptableFriendData/` |
| Tabbed friends panel UI | `FriendsPanel.cs` | `_Scripts/UI/Views/` |
| Friend row UI | `FriendEntryView.cs` | `_Scripts/UI/Views/` |
| Friend request row UI | `FriendRequestEntryView.cs` | `_Scripts/UI/Views/` |
| Add friend input UI | `AddFriendPanel.cs` | `_Scripts/UI/Views/` |
| SO asset instance | `FriendsData.asset` | `_SO_Assets/Friends Data/` |

#### Add Friend Entry Points

There are **two distinct ways** a player can send a friend request. Both ultimately call `FriendsServiceFacade` — the single writer — but they use different SDK methods depending on whether the caller has a player ID or only a display name.

| Entry Point | Input | Facade Method | UI Location |
|---|---|---|---|
| `AddFriendPanel` | Player name (text input) | `SendFriendRequestByNameAsync(name)` | FriendsPanel → "Add Friend" tab |
| `OnlinePlayerEntry.addFriendButton` | Player ID (from presence lobby) | `SendFriendRequestAsync(playerId)` | OnlinePlayersPanel → per-row "+" button |

**User navigation paths to reach "Add Friend":**

```
Path A — Friends Panel (by name):
  PartyArcadeView.friendsButton → FriendsPanel.Show()
    → Tab 2 ("Add Friend") → AddFriendPanel
    → User types name → [Send] → FriendsServiceFacade.SendFriendRequestByNameAsync()
    → Success: green feedback text "Request sent to 'Name'!"
    → Failure: red feedback text with error message

Path B — Online Players Panel (by ID):
  PartyArcadeView "+" slot / PartyAreaPanel "+" slot → OnlinePlayersPanel.Show()
    → Per-row [+] addFriendButton → OnlinePlayersPanel.OnAddFriendClicked()
    → FriendsServiceFacade.SendFriendRequestAsync(playerId)
    → Button disabled + friendRequestSentIndicator shown
    → (addFriendButton hidden if already friends — checked via FriendsServiceFacade.IsFriend())
```

**`AddFriendPanel` behavior** (`_Scripts/UI/Views/AddFriendPanel.cs`):
- Send button disabled until input is non-empty (`OnInputChanged` validates)
- Button disabled during async request (re-enabled in `finally`)
- Feedback text color: green (0.2, 0.9, 0.3) for success, red (0.9, 0.3, 0.3) for errors
- Input field cleared on success, preserved on failure
- Catches `FriendsServiceException` specifically for SDK errors

**`OnlinePlayerEntry.addFriendButton` behavior** (`_Scripts/UI/Views/OnlinePlayerEntry.cs`):
- Visibility controlled by `OnlinePlayersPanel.SpawnEntry()`: hidden if `friendsService` is null or player is already a friend
- On press: button disabled + `friendRequestSentIndicator` shown (no undo)
- Callback (`_onAddFriend`) set by `OnlinePlayersPanel` → calls `FriendsServiceFacade.SendFriendRequestAsync(playerId)`

**Friend request vs. party invite** — these are separate systems:

| Action | System | Persistence | SDK |
|---|---|---|---|
| Add Friend | `FriendsServiceFacade` → UGS Friends SDK | Persistent relationship (survives sessions) | `FriendsService.AddFriendAsync` / `AddFriendByNameAsync` |
| Invite to Party | `HostConnectionService` → UGS Sessions SDK | Ephemeral (session-scoped, lobby player properties) | Session player properties: `invite_target`, `invite_data` |

Both actions can appear on the same UI row: `OnlinePlayerEntry` has both `inviteButton` (party invite) and `addFriendButton` (friend request). `FriendEntryView` has `inviteButton` (party invite) but no add-friend button (they're already friends).

#### Friend System Patterns to Follow

- **Single writer**: Only `FriendsServiceFacade` writes to `FriendsDataSO`. UI components read via SOAP lists and events — they never call UGS SDK directly.
- **Sync after mutate**: Every facade method that changes relationship state calls `SyncAllRelationships()` after the SDK call to keep SOAP lists in sync.
- **Event-driven UI**: `FriendsPanel` and entry views subscribe to SOAP list events (`OnItemAdded`, `OnItemRemoved`, `OnCleared`) for reactive updates. No polling.
- **Presence via FriendsInitializer**: Scene transition presence is managed by `FriendsInitializer` — do not set presence from other MonoBehaviours.
- **DI access**: UI components access `FriendsServiceFacade` via `[Inject]`, not by finding it in the scene.
- **Bridge between Party and Friends**: `FriendEntryView`'s invite button calls `HostConnectionService.SendInviteAsync()` — the friend system feeds into the party system for social gameplay.

### Player Count & AI Backfill Pipeline

The player count system is fully data-driven from `SO_ArcadeGame` assets through the UI stepper, into `GameDataSO`, and finally into AI spawning. No hardcoded limits exist in the pipeline.

#### Data Flow

```
SO_ArcadeGame asset (MinPlayersAllowed, MaxPlayersAllowed)
       │
       ▼
ArcadeGameConfigureModal.InitializeScreen1Controls()
       │ effectiveMin = Max(game.MinPlayersAllowed, CurrentPartyHumanCount)
       │ playerCountStepper.Initialize(effectiveMin, game.MaxPlayersAllowed, config.PlayerCount)
       ▼
PlayerCountStepper (±1 stepper, range 1-12, fires OnValueChanged)
       │
       ▼
ArcadeGameConfigureModal.HandlePlayerCountSelected(playerCount)
       │ Clamp(playerCount, effectiveMin, MaxPlayersAllowed) → config.PlayerCount
       ▼
ArcadeGameConfigureModal.OnStartGameClicked()
       │ SyncAllGameDataForLaunch():
       │   humanCount = Max(1, hostConnectionData.PartyMembers.Count)
       │   gameData.ConfigurePlayerCounts(config.PlayerCount, humanCount)
       ▼
GameDataSO.ConfigurePlayerCounts(totalDesired, humanCount)
       │ SelectedPlayerCount.Value = totalDesired
       │ RequestedAIBackfillCount = Max(0, totalDesired - humanCount)
       ▼
gameData.InvokeGameLaunch() → OnLaunchGame SOAP event
       │
       ▼
SceneLoader.LaunchGame()
       │ AppState → LoadingGame, network scene load
       ▼
MultiplayerMiniGameControllerBase.OnNetworkSpawn() [game scene]
       │ [Server] SyncGameConfigToClients_ClientRpc (intensity, player count, AI backfill, etc.)
       ▼
ServerPlayerVesselInitializerWithAI.OnNetworkSpawn() [game scene]
       │ SpawnAIs():
       │   aiCount = gameData.RequestedAIBackfillCount
       │   teamCounts = gameData.BuildTeamCounts()  ← counts existing human players per team
       │   For each AI:
       │     domain = GetBalancedDomain(teamCounts)  ← picks team with fewest players
       │     teamCounts[domain]++
       │     Spawn AI player + vessel with that domain
       ▼
MultiplayerSetup.CreateOrJoinSession()
       │ MaxPlayers = gameData.SelectedPlayerCount.Value  ← no hardcoded cap
```

#### Player Count Examples

| Humans in Party | Selected Total | AI Backfill | Teams (Jade/Ruby/Gold) |
|---|---|---|---|
| 1 (solo) | 1 | 0 | 1/0/0 |
| 1 (solo) | 4 | 3 | 2/1/1 (balanced) |
| 1 (solo) | 12 | 11 | 4/4/4 (balanced) |
| 2 (both Jade) | 6 | 4 | 2/2/2 → 4/4/4 with AI fill |
| 3 (J/R/G) | 9 | 6 | 3/3/3 (balanced) |

#### Team Balancing Algorithm

`ServerPlayerVesselInitializerWithAI.GetBalancedDomain()` assigns each AI to the team with the fewest players. Ties break by enum order (Jade → Ruby → Gold). `GameDataSO.BuildTeamCounts()` initializes a `Dictionary<Domains, int>` with {Jade=0, Ruby=0, Gold=0} and counts existing non-AI players.

#### PlayerCountStepper

`PlayerCountStepper` (`_Scripts/UI/Elements/PlayerCountStepper.cs`) is a ±1 stepper control with three serialized fields:

| Field | Type | Purpose |
|---|---|---|
| `decrementButton` | `Button` | "-" button, auto-disables at min |
| `incrementButton` | `Button` | "+" button, auto-disables at max |
| `countText` | `TMP_Text` | Displays current count |

The modal initializes it via `playerCountStepper.Initialize(effectiveMin, game.MaxPlayersAllowed, config.PlayerCount)`. The stepper fires `OnValueChanged` on button press, which the modal handles via `HandlePlayerCountSelected`.

A legacy `playerCountButtons` list (4 fixed buttons for counts 1-4) coexists as fallback. Both UIs share the same `HandlePlayerCountSelected` callback. The stepper is required for ranges above 4.

#### Separate Limits

| System | Limit | Purpose |
|---|---|---|
| `SO_ArcadeGame.MaxPlayersAllowed` | Per-game (e.g., 12) | Total players (human + AI) in a game session |
| `HostConnectionDataSO.MaxPartySlots` | 4 | Human players in Menu_Main party lobby |
| UGS Presence Lobby | 100 | Player discovery (no Relay) |

These are independent — a party of 2 humans can launch a 12-player game with 10 AI.

#### Key Files — Player Count

| Role | File | Location |
|---|---|---|
| Per-game min/max config | `SO_ArcadeGame.cs` | `_Scripts/ScriptableObjects/` |
| Configure modal (UI) | `ArcadeGameConfigureModal.cs` | `_Scripts/UI/Modals/` |
| Player count stepper | `PlayerCountStepper.cs` | `_Scripts/UI/Elements/` |
| Player count computation | `GameDataSO.ConfigurePlayerCounts()` | `_Scripts/Utility/DataContainers/` |
| Team count builder | `GameDataSO.BuildTeamCounts()` | `_Scripts/Utility/DataContainers/` |
| AI spawner + team balancing | `ServerPlayerVesselInitializerWithAI.cs` | `_Scripts/Controller/Multiplayer/` |
| Session creation | `MultiplayerSetup.cs` | `_Scripts/Controller/Multiplayer/` |

### HexRace Game Mode

HexRace is a competitive crystal-collection racing mode (1-4 players) using a **single unified scene** (`MinigameHexRace.unity`). There is no separate singleplayer scene — all games run through Netcode regardless of player count. Solo play uses AI backfill via `ServerPlayerVesselInitializerWithAI`. See `Assets/_Scripts/Controller/Arcade/HEXRACE.md` for the full technical reference.

#### Architecture

```
MiniGameControllerBase (MonoBehaviour + NetworkBehaviour)
  └── MultiplayerMiniGameControllerBase
      └── MultiplayerDomainGamesController
          └── HexRaceController
```

**SO config**: `SO_ArcadeGame` asset — `Mode=HexRace(33)`, `IsMultiplayer=true`, `MinPlayers=1`, `MaxPlayers=4`, `GolfScoring=true`

#### Execution Flow

```
ArcadeGameConfigureModal.OnStartGameClicked()
  ├─ SyncAllGameDataForLaunch():
  │   ├─ gameData.SceneName = "MinigameHexRace"
  │   ├─ gameData.GameMode = GameModes.HexRace
  │   ├─ gameData.IsMultiplayerMode = true
  │   ├─ gameData.SelectedPlayerCount = humanCount
  │   └─ gameData.RequestedAIBackfillCount = max(0, config.PlayerCount - humanCount)
  └─ gameData.InvokeGameLaunch() → OnLaunchGame SOAP event
      └─ SceneLoader.LaunchGame()
          ├─ AppState → LoadingGame
          ├─ Network scene load (host always active from Menu_Main)
          └─ Game config synced to clients by MultiplayerMiniGameControllerBase.OnNetworkSpawn()
```

#### Player Count & AI Backfill

| Humans in Party | Selected Players | AI Backfill | Total |
|---|---|---|---|
| 1 (solo) | 1 | 0 | 1 |
| 1 (solo) | 2 | 1 | 2 |
| 1 (solo) | 4 | 3 | 4 |
| 2 (party) | 2 | 0 | 2 |
| 2 (party) | 4 | 2 | 4 |
| 3 (party) | 3 | 0 | 3 |

#### Track Spawning

Server generates a random seed (after 1500ms delay for intensity sync) → writes to `_netTrackSeed` NetworkVariable → all clients spawn identical track via `SegmentSpawner.Initialize()`. Clients receive the seed through three redundant paths: immediate read at spawn, `OnValueChanged` callback, or poll fallback (100ms × 50 attempts). `HexRaceController` sets `segmentSpawner.ExternalResetControl = true` to own the track lifecycle.

| Parameter | Formula | Base |
|---|---|---|
| Segments | `base * Intensity` | 10 |
| Straight Line Length | `base / Intensity` | 400 |
| Helix Radius | `Intensity / 1.3` | — |

#### Race Rules

- **Crystal target**: Resolved by `CrystalCollisionTurnMonitor.GetCrystalCollisionCount()`: inspector `CrystalCollisions` field (if non-zero) > `SpawnableWaypointTrack` waypoints > default 39. Synced to all clients via `NetworkCrystalCollisionTurnMonitor._netCrystalCollisions` NetworkVariable → `gameData.CrystalTargetCount`
- **Turn monitor**: `NetworkCrystalCollisionTurnMonitor` checks `gameData.RoundStatsList.Any(s => s.CrystalsCollected >= target)` every frame (server only)
- **Winner detection**: Server-authoritative via `HexRaceController.OnTurnEndedCustom()` — finds first player with `CrystalsCollected >= target`, sets `_raceEnded=true`, calculates all scores, broadcasts via `SyncFinalScores_ClientRpc`
- **Scoring**: Winner score = race time (seconds); Loser score = `10000 + crystalsRemaining`. Golf rules (`UseGolfRules=true`): lower = better
- **Score sync**: `SyncFinalScores_ClientRpc()` broadcasts all player scores + winner name to all clients, then calls `InvokeWinnerCalculated()` + `InvokeMiniGameEnd()`
- **HasEndGame=false**: Prevents base controller from calling `SyncGameEnd_ClientRpc` (which would duplicate `InvokeMiniGameEnd`). `SetupNewRound()` is overridden to return when `_raceEnded=true`, suppressing the Ready button
- **Comeback**: `ElementalComebackSystem` buffs losing players based on crystal deficit (e.g., Space element +4 for 4 crystals behind)

#### End Game

- `HexRaceEndGameController` reads `gameData.WinnerName` (set by server via `SyncFinalScores_ClientRpc`)
- Winner sees "VICTORY" + race time (formatted mm:ss:cs); losers see "DEFEAT" + crystals remaining
- `HexRaceScoreboard` displays all players ranked by score (golf rules — sorts ascending)
- **Replay**: Full network scene reload (`UseSceneReloadForReplay=true`). `OnResetForReplayCustom()` was removed — all race state, track, and environment are destroyed with the scene and re-initialized fresh via `OnNetworkSpawn`. Fade to black → scene reload → fade from black on `OnClientReady`

#### Shared State & NetworkVariables

| Variable | Owner | Purpose |
|---|---|---|
| `HexRaceController._netTrackSeed` | Server | Deterministic track seed (NetworkVariable) |
| `NetworkCrystalCollisionTurnMonitor._netCrystalCollisions` | Server | Crystal target synced to clients (NetworkVariable); writes to `gameData.CrystalTargetCount` |
| `gameData.WinnerName` | Server (via ClientRpc) | Authoritative winner identity; non-empty = results ready |
| `gameData.CrystalTargetCount` | Server (via `_netCrystalCollisions.OnValueChanged`) | Crystal target readable by any system |

#### Key Files — HexRace

| Role | File | Location |
|---|---|---|
| Game controller | `HexRaceController.cs` | `_Scripts/Controller/Arcade/` |
| Domain games base | `MultiplayerDomainGamesController.cs` | `_Scripts/Controller/Arcade/` |
| Score tracker | `HexRaceScoreTracker.cs` | `_Scripts/Controller/Arcade/` |
| Crystal turn monitor | `NetworkCrystalCollisionTurnMonitor.cs` | `_Scripts/Controller/Arcade/TurnMonitors/` |
| Track spawner | `SegmentSpawner.cs` | `_Scripts/Controller/Environment/MiniGameObjects/` |
| End game controller | `HexRaceEndGameController.cs` | `_Scripts/Utility/DataContainers/` |
| In-game HUD | `HexRaceHUD.cs` | `_Scripts/UI/` |
| Scoreboard | `HexRaceScoreboard.cs` | `_Scripts/UI/` |
| Elemental comeback | `ElementalComebackSystem.cs` | `_Scripts/Controller/Arcade/` |
| Stats provider | `HexRaceStatsProvider.cs` | `_Scripts/Controller/Arcade/` |
| Player stats profile | `HexRacePlayerStatsProfile.cs` | `_Scripts/UI/` |
| Full documentation | `HEXRACE.md` | `_Scripts/Controller/Arcade/` |

#### HexRace Patterns to Follow

- **Server authority via OnTurnEndedCustom**: Winner detection runs on the server in `OnTurnEndedCustom()`. `HexRaceScoreTracker` only handles local elapsed-time tracking and UGS stats reporting — it does not participate in winner determination.
- **Deterministic track**: All clients spawn identical tracks from shared seed + intensity. `SegmentSpawner` uses `Random.InitState(seed)`. Three redundant sync paths (immediate, OnValueChanged, poll fallback) ensure reliability.
- **Golf scoring**: `UseGolfRules = true` — lower score = better rank. Winner time (seconds) always ranks above loser penalty (10000+).
- **Scene reload for replay**: Use `UseSceneReloadForReplay = true` — do not implement in-place reset. Flora/fauna/environment don't fully reset in-place.
- **Comeback system**: Use `ElementalComebackSystem` with `ScoreDifferenceSource.CrystalsCollected` for HexRace (not Score, since Score tracks elapsed time equally for all).
- **Single scene**: Do not create separate singleplayer/multiplayer scenes. AI backfill handles solo play within the same Netcode pipeline.
- **Crystal target sync**: Server writes target to `NetworkCrystalCollisionTurnMonitor._netCrystalCollisions` NetworkVariable, which syncs to `gameData.CrystalTargetCount` on all clients.

### FTUE (First-Time User Experience)

Tutorial system at `Assets/FTUE/` (25 C# files) using adapter pattern with clean interface separation:

- **Interfaces**: `IFlowController`, `ITutorialExecutor`, `ITutorialStepHandler`, `ITutorialUIView`, `IAnimator`, `IOutroHandler`, `ITutorialStepExecutor`
- **Adapters**: `TutorialExecutorAdapter`, `FTUEIntroAnimatorAdapter`, `TutorialUIViewAdapter`
- **Data models**: `TutorialStep`, `TutorialPhase`, `TutorialSection`, `TutorialSequenceSet`, `TutorialStepPayload`, `TutorialStepType`, `FTUEProgress`
- **Drivers**: `FTUEIntroAnimator`, `TutorialFlowController`
- **Step handlers**: `FreestylePromptHandler`, `IntroWelcomeHandler`, `LockModesExceptFreestyleHandler`, `OpenArcadeMenuHandler`
- **UI**: `TutorialUIView`, `InGameTutorialFlowView`
- **Events**: `FTUEEventManager` (SOAP-based event broadcasting)

### Dialogue System

Custom dialogue system spanning two locations:

- **Editor & assets**: `Assets/_Scripts/DialogueSystem/` — animation controllers, shader graphs (SpriteAnimation, UI_NoiseDissolve), SO dialogue data assets, prefab
- **Runtime code**: `Assets/_Scripts/System/Runtime/` — `DialogueManager`, `DialogueEventChannel`, `DialogueUIAnimator`, `DialogueViewResolver`, `DialogueAudioBatchLinker`
- **Models**: `Assets/_Scripts/System/Runtime/Models/` — `DialogueLine`, `DialogueSet`, `DialogueSetLibrary`, `DialogueSpeaker`, `DialogueVisuals`, `DialogueModeType`, `IDialogueService`, `IDialogueView`, `IDialogueViewResolver`
- **Views**: `InGameRadioDialogueView`, `MainMenuDialogueView`, `RewardDialogueView`
- **Editor tools**: `DialogueEditorWindow`, `DialogueLineDrawer` (in `_Scripts/Editor/`)

### AI Opponent System

Runtime-configurable AI opponents at `Assets/_Scripts/Controller/AI/`:
- `AIPilot` controls AI vessel behavior
- `AIGunner` controls AI targeting/shooting
- AI profiles configured via `SO_AIProfileList` (`MainAIProfileList.asset`)
- AI profiles used for score cards and multiplayer backfill
- Configurable AI ship selection and behavior at runtime

### Menu Screen Navigation (Menu_Main Scene)

The main menu uses a horizontal sliding panel system managed by `ScreenSwitcher`. Screen panels are laid out side-by-side and the container slides left/right to reveal each screen.

#### IScreen Interface

All menu screens that need lifecycle notifications implement `IScreen` (`Assets/_Scripts/UI/Interfaces/IScreen.cs`):

```csharp
public interface IScreen
{
    void OnScreenEnter();  // Called when this screen becomes active
    void OnScreenExit();   // Called when navigating away from this screen
}
```

`ScreenSwitcher` discovers `IScreen` components on screen root GameObjects (via `GetComponentInChildren<IScreen>`) at startup and caches them in a dictionary. On navigation, it calls `OnScreenExit()` on the outgoing screen and `OnScreenEnter()` on the incoming screen automatically — no hard-coded screen references needed.

**Current `IScreen` implementors**: `HangarScreen`, `LeaderboardsMenu`

#### Screen Inventory

| Screen | Class | Extends `IScreen` | Init Pattern |
|---|---|---|---|
| Home | `HomeScreen` | No | `Start()` |
| Arcade (ARK) | `ArcadeScreen` | No | `Start()` |
| Store | `StoreScreen` (extends `View`) | No | `Start()` + `OnEnable()` events |
| Port (Leaderboards) | `LeaderboardsMenu` | Yes | `OnScreenEnter()` → `LoadView()` |
| Hangar | `HangarScreen` | Yes | `OnScreenEnter()` → `LoadView()` |
| Episodes | `EpisodeScreen` | No | Lazy `LoadView()` on panel toggle |

#### ScreenSwitcher

`ScreenSwitcher` (`Assets/_Scripts/UI/ScreenSwitcher.cs`) is the central navigation hub:

- Maps `MenuScreens` enum values to screen panel `RectTransform`s via inspector-configured `ScreenEntry` list
- Handles horizontal slide animations between screens
- Manages a modal window stack (`PushModal`/`PopModal`) for overlay modals
- Persists return-to-screen/modal state via `PlayerPrefs` across scene reloads
- Notifies `IScreen` implementors on navigation transitions
- Supports gamepad left/right trigger navigation

**Adding a new screen**: Create a `MonoBehaviour` implementing `IScreen` if it needs enter/exit lifecycle. Add a `ScreenEntry` in the `ScreenSwitcher` inspector mapping. The switcher will discover and call the `IScreen` automatically.

#### Reusable UI Components

- **`ProfileDisplayWidget`** (`Assets/_Scripts/UI/Elements/ProfileDisplayWidget.cs`) — Displays player name + avatar. Uses `[Inject] PlayerDataService` and subscribes to `OnProfileChanged`. Drop onto any menu screen that needs profile display — replaces inline profile display logic.
- **`NavLink` / `NavGroup`** (`Assets/_Scripts/UI/Elements/`) — Tab navigation within a screen. `NavGroup` discovers child `NavLink` components and manages selection state with crossfade animations.
- **`ModalWindowManager`** (`Assets/_Scripts/UI/Modals/ModalWindowManager.cs`) — Base class for modal windows. Caches `ScreenSwitcher` reference at startup. Handles open/close animations, audio, and modal stack integration.

#### Menu Screen Patterns to Follow

- **Implement `IScreen`** for any screen that needs to refresh data when navigated to — do not add direct screen references to `ScreenSwitcher`
- **Use `ProfileDisplayWidget`** for profile display instead of duplicating `PlayerDataService` subscription logic
- **Cache component lookups** — use `Start()` or `Awake()` for `GetComponent` calls, not per-frame or per-event
- **Unsubscribe from events** — always pair event subscriptions in `OnEnable`/`OnDisable` or `Start`/`OnDestroy`
- **Use `[Inject]` for audio** — prefer `[Inject] AudioSystem` via Reflex DI over `[RequireComponent(typeof(MenuAudio))]` + `GetComponent` for new code

### Lava-Lamp Mode (Menu Freestyle Merge)

Lava-lamp mode merges freestyle gameplay directly into Menu_Main. Instead of launching a separate freestyle scene, the autopilot vessel becomes playable when the player enters freestyle mode. Game UI panels from the freestyle scenes (MiniGameHUD, Scoreboard, Vessel Selection, Vessel HUDs, PlayerScoreCards, EndShapeDetailHUD) live under Menu_Main's "Game UI" container and fade in/out with the freestyle toggle.

#### Design Principles

- **Individual panels, not GameCanvas prefab**: Extract needed UI panels as scene-level objects under "Game UI" — do not instantiate the full `GameCanvas.prefab`. The GameCanvas prefab bundles a `Canvas` + `CanvasScaler` + `GraphicRaycaster` root that would conflict with Menu_Main's existing Canvas.
- **Reuse existing SOAP pipeline**: `MenuCrystalClickHandler` already toggles autopilot↔freestyle with CanvasGroup fading. "Game UI" `CanvasGroup` is already wired into its `freestyleCanvasGroups[]` array. `MainMenuController` already has `MainMenuState.Freestyle`. No new states or SOAP events needed.
- **Network-aware vessel selection**: Use `MenuVesselSelectionPanelController` (not the singleplayer `VesselSelectionPanelController`) — it delegates vessel swaps to `MenuServerPlayerVesselInitializer` via the Netcode despawn/spawn/RPC pipeline so changes replicate to all clients.
- **Phased rollout**: Phase 1 (core HUD + vessel selection), Phase 2 (shape drawing), Phase 3 (scoring).

#### Current "Game UI" Container

The existing "Game UI" in Menu_Main has two children:

```
Game UI [RectTransform, CanvasGroup]                    ← already in freestyleCanvasGroups[]
├── MiniGameHUD [RectTransform, CanvasGroup, MenuMiniGameHUD]
│   └── Volume / Pause Button [Image, Button, MenuAudio]
│       └── MenuMiniGameHUD.Awake() wires onClick → vesselSelectionPanel.Open() + Hide()
│
└── Vessel Selection Panel [CanvasGroup, VesselSelectionPanelUI, MenuVesselSelectionPanelController]
    ├── Buttons (Resume, Close) → onClick includes MenuMiniGameHUD.Show()
    └── Menu [GridLayout, 6× ShipCardView]
```

`MenuMiniGameHUD` (`_Scripts/UI/MenuMiniGameHUD.cs`) is a slim alternative to the full `MiniGameHUD` for menu freestyle mode. It provides the Volume/Pause icon button (matching the MinigameFreestyle scene pattern) that opens the `MenuVesselSelectionPanelController` panel, vessel HUD reparenting via the `onShipHUDInitialized` SOAP event, and runtime PauseMenu prefab instantiation. The button is visible when Game UI fades in during freestyle, hidden when returning to menu. The full `MiniGameHUD` can replace this when Phase 2/3 features (shape drawing, scoring) are needed.

#### Phase 1: Core Freestyle HUD (target hierarchy)

```
Game UI [RectTransform, CanvasGroup]
├── MiniGameHUD [CanvasGroup, MiniGameHUD, MiniGameHUDView, SOAP listeners]
│   ├── ReadyButton [INACTIVE — no countdown in lava-lamp]
│   ├── Volume / Pause Button
│   ├── Scoreboard (inline score TMP)
│   ├── RoundTime (rotating circles + countdown TMP)
│   ├── LifeFormCounter (rotating circles + counter TMP)
│   ├── ThumbCursors (LeftCursor, RightCursor — ThumbCursor)
│   ├── NotificationUI [GameEventFeed]
│   └── PlayerScoreContainer [Transform — for dynamically instantiated PlayerScoreCards]
│
├── Vessel Selection Panel [CanvasGroup, VesselSelectionPanelUI, MenuVesselSelectionPanelController]
│   ├── Buttons (Resume, Close)
│   └── Menu [GridLayout, 6× ShipCardView]
│
├── ScoreboardController [Scoreboard.cs — hidden by default, no OnShowGameEndScreen in basic freestyle]
│   ├── SinglePlayerView
│   ├── MultiplayerView (4 player rows, winner banner)
│   └── Buttons (PlayAgain, Home)
│
└── EndGameShapePanel [EndShapeDetailHUD — INACTIVE, Phase 2]
    ├── Shape stats (name, time, par, accuracy, star rating)
    ├── ScreenShotButton
    └── ExitShapeButton
```

#### MiniGameHUD Configuration for Menu

| Setting | Value | Rationale |
|---|---|---|
| `enablePreGameCinematic` | `false` | No cinematic in menu freestyle |
| `isAIAvailable` | `false` | No AI score tracking in basic lava-lamp (Phase 3) |
| `minConnectingSeconds` | `0` | No connecting panel delay |
| `preGameCinematic` | `null` | Not needed |
| `onMoundDroneSpawned` | `null` | No drones in menu |
| `onQueenDroneSpawned` | `null` | No drones in menu |
| `scoreboard` | Wire to ScoreboardController | Present but hidden |

**SOAP events to wire on MiniGameHUD GO:**
- `EventListenerPipData` → `onShipHUDInitialized` (vessel HUD reparenting)
- `EventListenerBool` → optional, for turn visibility toggling

#### Vessel HUD Lifecycle in Menu

Vessel HUDs reparent into "Game UI" automatically through the existing SOAP pipeline — no code changes needed:

```
Vessel spawned (MenuServerPlayerVesselInitializer)
  └─ ShipHUD.Start() [on vessel prefab]
      └─ onShipHUDInitialized.Raise(ShipHUDData)
          └─ MiniGameHUD.OnShipHUDInitialized()
              └─ Reparents HUD children under transform.parent (= "Game UI")
```

HUD children persist across freestyle toggles. Their visibility is controlled by the "Game UI" `CanvasGroup.alpha` that `MenuCrystalClickHandler` already fades.

Per-vessel HUD controllers (`IVesselHUDController` implementors):

| Vessel | Controller | View |
|---|---|---|
| Manta | `MantaHUDController` | `MantaHUDView` |
| Rhino | `RhinoHUDController` | `RhinoHUDView` |
| Serpent | `SerpentHUDController` | `SerpentHUDView` |
| Sparrow | `SparrowHUDController` | `SparrowHUDView` |
| Dolphin | — | `DolphinHUDView` |
| Squirrel | — | `SquirrelHUDView` |

HUD prefab variants at `_Prefabs/UI Elements/VesselHUD/` (e.g., `MantaHUDVariant.prefab`, `DolphinHUDVariant.prefab`).

#### Vessel Selection Panel (Network-Aware)

The Vessel Selection Panel in Menu_Main already uses `MenuVesselSelectionPanelController` (network-aware). For reference, here is how it differs from the singleplayer variant:

| Aspect | Singleplayer (`VesselSelectionPanelController`) | Menu (`MenuVesselSelectionPanelController`) |
|---|---|---|
| Vessel swap | `VesselSpawner.SpawnShip()` — local instantiate | `MenuServerPlayerVesselInitializer.RequestSwap()` — Netcode pipeline |
| Multiplayer | Not supported | Replicates to all clients |
| Autopilot | Snapshots & restores AI/input state | Restores freestyle control after swap delay |
| References | `VesselSpawner`, `ThemeManagerDataContainerSO` | `MenuServerPlayerVesselInitializer`, `MenuCrystalClickHandler`, `MenuFreestyleEventsContainerSO` |

The panel opens from a button in the freestyle HUD. While open, the vessel flies on autopilot. On "Resume", if a different vessel is selected, it requests a network swap and waits `restoreFreestyleDelayMs` (600ms) before restoring player control.

#### SOAP Event Flow (Freestyle Toggle with Game UI)

```
Player taps freestyle button
  └─ MenuCrystalClickHandler.ToggleTransition()
      ├─ TransitionToFreestyle():
      │   ├─ Vessel.ToggleAIPilot(false), InputController.SetPause(false)
      │   ├─ freestyleEvents.OnEnterFreestyle.Raise()
      │   │   └─ MainMenuController → TransitionTo(Freestyle)
      │   ├─ FadeBetweenStates(menuAlpha=0, freestyleAlpha=1)
      │   │   ├─ menuCanvasGroups[] → fade to 0 (menu screens, nav bar)
      │   │   └─ freestyleCanvasGroups[] → fade to 1 ("Game UI" + contents)
      │   │       └─ MiniGameHUD, Vessel HUD children, Vessel Selection Button all become visible
      │   └─ Wait cameraTransitionDuration (parallel with fade)
      │
      └─ TransitionToMenu():
          ├─ InputController.SetPause(true), Vessel.ToggleAIPilot(true)
          ├─ freestyleEvents.OnExitFreestyle.Raise()
          │   └─ MainMenuController → TransitionTo(Ready)
          │   └─ MenuVesselSelectionPanelController → ui.Hide() (auto-close panel)
          ├─ FadeToSavedMenuAlphas()
          │   ├─ menuCanvasGroups[] → restore to saved alphas
          │   └─ freestyleCanvasGroups[] → fade to 0 ("Game UI" hidden)
          └─ Wait cameraTransitionDuration
```

#### Scoreboard in Menu Context

The `Scoreboard` component is present but hidden in basic lava-lamp mode. It subscribes to `OnShowGameEndScreen` to show and `OnResetForReplay` to hide. Since no game controller raises `OnShowGameEndScreen` during basic freestyle, the scoreboard stays inactive.

When scoring is enabled (Phase 3), a game controller can raise `OnShowGameEndScreen` to display results. The scoreboard supports both `SinglePlayerView` and `MultiplayerView` automatically based on `gameData.IsMultiplayerMode`.

#### Phase 2: Shape Drawing (Deferred)

Shape drawing requires additional scene infrastructure beyond UI panels:

| Dependency | Purpose | Current Location |
|---|---|---|
| `ShapeDrawingManager` | Orchestrates shape preview → draw → score flow | Freestyle scene (Game GO) |
| `SegmentSpawner` | Spawns trail segments with shape triggers | Freestyle scene (Game GO) |
| `ShapeDrawingCrystalManager` | Manages crystals during shape mode | Freestyle scene (Game GO) |
| `Spawnable*` objects | Shape definitions (Arrow, Circle, Diamond, etc.) | Freestyle scene (12 prefab instances) |
| `EndShapeDetailHUD` | Shows shape results (name, time, accuracy, stars) | Freestyle scene (scene-level UI) |

The `SinglePlayerFreestyleController` manages the freestyle↔shape-drawing transitions (collision detection, environment teardown/restore, camera swaps). For lava-lamp, a `MenuFreestyleController` would adapt this flow for the menu context.

**Shape Drawing State Flow:**
```
Freestyle → ShapeCollision → FreezePlayer → NukeEnvironment → ShapePreview
  → ReadyButton → Countdown → DrawingMode → ShapeComplete → EndShapeDetailHUD
  → ExitButton → RestoreEnvironment → ConnectingFlow → ReadyButton → Freestyle
```

#### Phase 3: Scoring & PlayerScoreCards (Deferred)

`PlayerScoreCard`s are instantiated dynamically by `MiniGameHUD` when `OnMiniGameTurnStarted` fires:

- `SetupLocalPlayerCard()` — creates a card for the local player with name, score, domain color, avatar
- `SetupAICards()` — creates cards for AI opponents (when `isAIAvailable=true`)

For lava-lamp scoring, set `isAIAvailable=true` on MiniGameHUD and ensure `gameData.RoundStatsList` is populated. Cards are destroyed on `OnMiniGameTurnEnd`.

#### Lava-Lamp Key Files

| Role | File | Location |
|---|---|---|
| Menu MiniGameHUD (freestyle HUD + vessel change trigger) | `MenuMiniGameHUD.cs` | `_Scripts/UI/` |
| Freestyle toggle (autopilot↔control) | `MenuCrystalClickHandler.cs` | `_Scripts/Controller/Multiplayer/` |
| Menu state machine | `MainMenuController.cs` | `_Scripts/System/` |
| Menu vessel spawner (base) | `MenuServerPlayerVesselInitializer.cs` | `_Scripts/Controller/Multiplayer/` |
| Vessel selection (network-aware) | `MenuVesselSelectionPanelController.cs` | `_Scripts/Controller/Multiplayer/` |
| Vessel selection UI (show/hide) | `VesselSelectionPanelUI.cs` | `_Scripts/UI/` |
| Vessel card (per-vessel button) | `VesselCardView.cs` (class: `ShipCardView`) | `_Scripts/UI/` |
| Minigame HUD controller | `MiniGameHUD.cs` | `_Scripts/UI/` |
| Minigame HUD view | `MiniGameHUDView.cs` | `_Scripts/UI/View/` |
| Scoreboard (end-game results) | `Scoreboard.cs` | `_Scripts/UI/` |
| Player score card (per-player) | `PlayerScoreCard.cs` | `_Scripts/UI/` |
| Shape results panel | `EndShapeDetailHUD.cs` | `_Scripts/UI/` |
| Vessel HUD reparenting bridge | `VesselHUD.cs` (class: `ShipHUD`) | `_Scripts/Controller/Vessel/` |
| Freestyle SOAP events container | `MenuFreestyleEventsContainerSO.cs` | `_Scripts/ScriptableObjects/` |
| Vessel selection (singleplayer, legacy) | `VesselSelectionPanelController.cs` | `_Scripts/UI/` |
| Freestyle controller (singleplayer ref) | `SinglePlayerFreestyleController.cs` | `_Scripts/Controller/Arcade/` |
| VesselHUD prefab variants | `*HUDVariant.prefab` | `_Prefabs/UI Elements/VesselHUD/` |
| PlayerScoreCard prefab | `PlayerScoreCard.prefab` | `_Prefabs/UI Elements/In Game/` |

#### Lava-Lamp Patterns to Follow

- **No new `MainMenuState` values** — `Freestyle` already exists and covers the lava-lamp gameplay phase
- **"Game UI" CanvasGroup controls all game panel visibility** — individual panels should not manage their own top-level visibility during freestyle toggles; the parent CanvasGroup handles fade in/out
- **Vessel HUD reparenting is automatic** — do not manually instantiate or position vessel HUDs; the `onShipHUDInitialized` → `MiniGameHUD.OnShipHUDInitialized()` pipeline handles it
- **Network-aware vessel selection only** — always use `MenuVesselSelectionPanelController` in Menu_Main, never the singleplayer `VesselSelectionPanelController`
- **Scoreboard hidden until needed** — do not show the scoreboard in basic freestyle; let the SOAP event system activate it when a game controller raises `OnShowGameEndScreen`
- **Phase 2/3 panels start inactive** — `EndShapeDetailHUD` GO starts with `SetActive(false)`, activated only by `ShapeDrawingManager` (Phase 2). PlayerScoreCards are dynamically instantiated only when turns are active (Phase 3)

### Namespace Convention

All game code lives under `CosmicShore.*` with 8 primary namespaces:

- `CosmicShore.Core` — foundational systems: PlayFab integration, authentication, bootstrap, rewind, FTUE, dialogue runtime
- `CosmicShore.Gameplay` — all gameplay controllers: vessel, input, multiplayer, camera, impact effects, arcade, projectiles, environment, player, AI
- `CosmicShore.Data` — enums (VesselClassType, Domains, ResourceType, ShipActions, InputEvents, etc.) and data structs
- `CosmicShore.ScriptableObjects` — SO definitions (SO_Captain, SO_Vessel, SO_Game, etc.) and all custom SOAP types
- `CosmicShore.UI` — all UI: vessel HUD controllers/views, modals, screens, toast system, scoreboards, elements
- `CosmicShore.Utility` — utilities: Effects, PoolsAndBuffers, DataContainers, DataPersistence, ClassExtensions, interactive SSU components
- `CosmicShore.Editor` — editor tools: dialogue editor, shader inspectors, copy tools, scene utilities
- `CosmicShore.Tests` — edit-mode unit tests

### Key Systems & Classes

| System | Key Classes | Location |
|---|---|---|
| Vessel core | `VesselStatus` (extends `NetworkBehaviour`), `VesselTransformer`, `VesselController`, `VesselPrismController` | `_Scripts/Controller/Vessel/` |
| Vessel actions | `VesselActionSO` (base config), `VesselActionExecutorBase`, `ActionExecutorRegistry` + 40+ action SOs | `_Scripts/Controller/Vessel/R_VesselActions/`, `VesselActions/` |
| Prism lifecycle | `Prism`, `PrismFactory`, `Trail`, `TrailFollower` | `_Scripts/Controller/Vessel/`, `_Scripts/Controller/Prisms/` |
| Prism performance | `PrismScaleManager`, `MaterialStateManager`, `AdaptiveAnimationManager`, `PrismStateManager`, `PrismTimerManager`, `BlockDensityGrid` | `_Scripts/Controller/Managers/` |
| Octahedron shield | `PrismOctahedronShield` (per-face bloom engage + shatter-overlay disengage, swaps BoxCollider ↔ convex MeshCollider, mass scales with volume), `PrismOctahedronShieldTester` (Input System-driven manual tester), `OctahedronMeshGenerator` (`PopulateMesh`, `PopulateMeshFaceScale`, `PopulateMeshFaceShatter`) | `_Scripts/Controller/Vessel/`, `_Scripts/Utility/` |
| Impact effects | `ImpactorBase` + 11 impactor types, 20+ Effect SO types | `_Scripts/Controller/ImpactEffects/` |
| Forcefield crackle | `SkimmerForcefieldCracklePrismEffectSO` (computes impact points via `Collider.ClosestPoint`), `ForcefieldCrackleController` (`[ExecuteAlways]`, 16-impact ring buffer + MaterialPropertyBlock arrays, owns all visual params), `ForcefieldCrackle.hlsl` (FBM electrical arcs on geodesic sphere), `ForcefieldCrackleControllerEditor` (edit-mode preview) | `_Scripts/Controller/ImpactEffects/EffectsSO/Skimmer Prism Effects/`, `_Scripts/Controller/Vessel/`, `Assets/Materials/Graphs/`, `_Scripts/Editor/` |
| Camera | `CustomCameraController`, `VesselCameraCustomizer`, `CameraSettingsSO`, `ICameraController`, `ICameraConfigurator` | `_Scripts/Controller/Camera/` |
| Vessel HUD | `IVesselHUDController`, `IVesselHUDView`, per-vessel controllers & views (Sparrow, Squirrel, Serpent, Manta, Rhino, Dolphin) | `_Scripts/UI/Controller/`, `_Scripts/UI/View/`, `_Scripts/UI/Interfaces/` |
| Arcade games | `MiniGameControllerBase`, `SinglePlayerMiniGameControllerBase`, `MultiplayerMiniGameControllerBase`, `CompositeScoring` | `_Scripts/Controller/Arcade/` |
| Resource system | `ResourceSystem`, `R_VesselActionHandler`, `R_VesselElementStatsHandler` | `_Scripts/Controller/Vessel/` |
| Object pooling | `GenericPoolManager` (Unity `ObjectPool<T>` with async buffer maintenance) | `_Scripts/Utility/PoolsAndBuffers/` |
| Player system | `Player` (NetworkBehaviour, `IPlayer`), `PlayerSpawner`, `PlayerSpawnerAdapterBase`, `MiniGamePlayerSpawnerAdapter`, `VolumeTestPlayerSpawnerAdapter` | `_Scripts/Controller/Player/` |
| Menu navigation | `ScreenSwitcher`, `IScreen`, `ModalWindowManager`, `ProfileDisplayWidget`, `NavLink`/`NavGroup` | `_Scripts/UI/`, `_Scripts/UI/Interfaces/`, `_Scripts/UI/Elements/`, `_Scripts/UI/Modals/` |
| Menu screens | `HomeScreen`, `ArcadeScreen`, `StoreScreen`, `HangarScreen`, `LeaderboardsMenu`, `EpisodeScreen` | `_Scripts/UI/Screens/` |
| UI | Elements, FX, Modals, Screens, Views + `ToastService` / `ToastChannel` | `_Scripts/UI/` |
| Telemetry | `VesselTelemetryBootstrapper`, `VesselTelemetry` (abstract) + per-vessel subclasses, `VesselStatsCloudData` | `_Scripts/Controller/Vessel/` |
| Analytics | `CSAnalyticsManager`, Firebase + Unity Analytics, 7 data collectors | `_Scripts/System/Instrumentation/` |
| Bootstrap / DI | `AppManager` (orchestrator + IInstaller), `BootstrapConfigSO`, `SceneTransitionManager`, `ApplicationLifecycleManager`, `ApplicationLifecycleEventsContainerSO` | `_Scripts/System/`, `_Scripts/System/Bootstrap/`, `_Scripts/ScriptableObjects/` |
| App state machine | `ApplicationStateMachine` (single-writer phase tracker), `ApplicationStateData` / `ApplicationStateDataVariable` (SOAP state), `ApplicationState` enum | `_Scripts/System/`, `_Scripts/ScriptableObjects/SOAP/ScriptableApplicationState/`, `_Scripts/Data/Enums/` |
| Scene management | `SceneLoader` (MonoBehaviour, DontDestroyOnLoad in Bootstrap, game launch + restart + return-to-menu, SOAP code subscriptions), `SceneNameListSO` (centralized scene names, DI-registered) | `_Scripts/System/`, `_Scripts/Utility/DataContainers/` |
| Authentication | `AuthenticationServiceFacade` (facade/writer), `AuthenticationController` (MonoBehaviour adapter), `AuthenticationSceneController` (scene UI), `SplashToAuthFlow` (splash routing), `AuthenticationData` / `AuthenticationDataVariable` (SOAP state) | `_Scripts/System/`, `_Scripts/ScriptableObjects/SOAP/ScriptableAuthenticationData/` |
| Friends | `FriendsServiceFacade` (facade/single-writer for UGS Friends SDK), `FriendsInitializer` (MonoBehaviour bridge + presence), `FriendsDataSO` (SOAP container: 4 lists + 4 events), `FriendData`/`FriendPresenceActivity` (SOAP data types) | `_Scripts/System/`, `_Scripts/Controller/Party/`, `_Scripts/Utility/DataContainers/`, `_Scripts/ScriptableObjects/SOAP/ScriptableFriendData/` |
| Friends UI | `FriendsPanel` (tabbed: list + requests + add), `FriendEntryView` (friend row with invite/remove), `FriendRequestEntryView` (accept/decline/cancel), `AddFriendPanel` (name input) | `_Scripts/UI/Views/` |
| Player data | `PlayerDataService` (cloud profile, XP, rewards), `PlayerProfileData` | `_Scripts/UI/Views/` |
| Network monitoring | `NetworkMonitor` (polling), `NetworkMonitorData` / `NetworkMonitorDataVariable` (SOAP events) | `_Scripts/System/`, `_Scripts/ScriptableObjects/SOAP/ScriptableAuthenticationData/` |
| Multiplayer | `MultiplayerSetup` (NetworkManager lifecycle + UGS sessions), `ServerPlayerVesselInitializer` (base spawner), `ClientPlayerVesselInitializer` (pair initializer + RPCs), `ServerPlayerVesselInitializerWithAI` (AI pre-spawner), `MenuServerPlayerVesselInitializer` (menu autopilot), `MenuCrystalClickHandler` (play-from-menu), `DomainAssigner` (team pool) | `_Scripts/Controller/Multiplayer/` |
| Party / Invite | `HostConnectionService` (presence lobby + party sessions, single-writer to `HostConnectionDataSO`), `PartyInviteController` (Netcode host↔client transitions), `FriendsInitializer` (Friends service bridge) | `_Scripts/Controller/Party/` |
| Party UI | `PartyAreaPanel` (3-slot), `PartyArcadeView` (4-slot), `PartySlotView`, `OnlinePlayersPanel`, `OnlinePlayerEntry`, `FriendsPanel`, `FriendEntryView`, `AddFriendPanel`, `PartyInviteNotificationPanel` | `_Scripts/UI/Views/`, `_Scripts/UI/Elements/`, `_Scripts/UI/Screens/` |
| Menu scene controller | `MainMenuController` (sub-state machine: None→Initializing→Ready→LaunchingGame), `MainMenuState` enum | `_Scripts/System/`, `_Scripts/Data/Enums/` |
| Audio | `AudioSystem` (DI singleton), `ScriptableEventGameplaySFX` / `EventListenerGameplaySFX` (decoupled gameplay SFX via SOAP) | `_Scripts/System/Audio/`, `_Scripts/ScriptableObjects/SOAP/ScriptableGameplaySFX/` |
| App systems | Favorites, LoadOut, Quest, Rewind, Squads, UserAction, UserJourney, Xp, Ads, IAP, DailyChallenge, TrainingGameProgress | `_Scripts/System/` |
| ScriptableObjects | `SO_Vessel`, `SO_Captain`, `SO_Game`, `SO_ArcadeGame`, `SO_Element`, `SO_Mission`, etc. | `_Scripts/ScriptableObjects/` |

### Async Pattern

- Prefer UniTask over coroutines for new code
- For ScriptableObjects that need async: use a `CoroutineRunner` singleton proxy or async/await with cancellation tokens
- Always include `CancellationToken` for anything non-trivial — UniTask respects play mode lifecycle better than raw `Task`
- Bootstrap uses `UniTaskVoid` with `CancellationTokenSource` for the async startup sequence
- Prefer SOAP event channels (`ScriptableEvent`) over `UniTask.WaitUntil` polling for waiting on state changes from other systems. Subscribe to the relevant event and react when it fires, rather than polling a condition every frame

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

The prism system is the most performance-critical gameplay system. See `Assets/_Scripts/Game/Prisms/PRISM_PERFORMANCE_AUDIT.md` for the full audit (note: audit doc remains in the vestigial `Game/` directory). Key facts:

- Each prism is a full GameObject with 5-6 MonoBehaviours + BoxCollider + MeshRenderer
- At 2,000 prisms: ~12,000 MonoBehaviour instances + 2,000 colliders
- Scale and material animation are already Jobs + Burst optimized
- Main bottlenecks: explosion/implosion VFX (per-object UniTask), physics colliders, material instancing leaks
- Active optimization: `PrismTimerManager`, per-frame explosion VFX cap, `EventListenerBase` GC elimination

## Testing

### Test Infrastructure

- **Framework**: Unity Test Framework 1.6.0 (NUnit-based)
- **Edit-mode tests**: `Assets/_Scripts/Tests/EditMode/` — 17 test files covering enums, data SOs, geometry utils, party data, resource collection, disposable groups, camera settings, etc.
- **Bootstrap tests**: `Assets/_Scripts/System/Bootstrap/Tests/` — `AppManagerBootstrapTests` (file: `BootstrapControllerTests.cs`), `BootstrapConfigSOTests`, `SceneTransitionManagerTests`, `ApplicationLifecycleManagerTests`, `ApplicationStateMachineTests`, `SceneFlowIntegrationTests`
- **Multiplayer tests**: `Assets/_Scripts/Controller/Multiplayer/Tests/` — `DomainAssignerTests`
- **PlayFab tests**: `Assets/_Scripts/System/Playfab/PlayFabTests/` — `PlayFabCatalogTests`
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
