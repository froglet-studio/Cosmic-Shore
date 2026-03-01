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

Existing custom SOAP types (16 subdirectories): `AbilityStats`, `ApplicationState` (`ApplicationStateData` + `ApplicationStateDataVariable` + `ScriptableEventApplicationState` — written by `ApplicationStateMachine`), `AuthenticationData` (+ `NetworkMonitorData`), `ClassType` (VesselClassType + VesselImpactor + debuff events), `CrystalStats`, `FriendData` (friend relationship data for UGS Friends integration), `GameplaySFX` (gameplay sound effect category events for decoupled audio), `InputEvents`, `PartyData` (PartyInviteData, PartyPlayerData + list variant), `PipData`, `PrismStats`, `Quaternion`, `VesselHUDData`, `SilhouetteData`, `Transform`, and `ScriptableEventWithReturn` (generic return channel + `PrismEventChannelWithReturnSO`). Also contains `VesselPrefabContainer.cs` for vessel-class-to-prefab mapping.

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
- `SceneLoader` (`_Scripts/System/SceneLoader.cs`) — persistent scene-loading and game-restart service. Extends `NetworkBehaviour` for multiplayer-aware scene loading. Handles launching gameplay scenes (local + network), restart/replay, and returning to main menu. Registered as a DI singleton via AppManager. Transitions app state to `LoadingGame` / `MainMenu` on scene changes.
- `SceneNameListSO` (`_Scripts/Utility/DataContainers/SceneNameListSO.cs`) — centralized scene name registry (Bootstrap, Authentication, Menu_Main, Multiplayer). Registered in DI and injected where scene names are needed, replacing hardcoded strings.
- `SceneTransitionManager` — unified scene loading with fade transitions (`[DefaultExecutionOrder(-50)]`), creates its own full-screen fade overlay programmatically. Registered as a DI singleton.
- `ApplicationLifecycleManager` — application lifecycle events, bridges both static C# events (legacy) and SOAP events via `ApplicationLifecycleEventsContainerSO`
- `ApplicationLifecycleEventsContainerSO` (`_Scripts/ScriptableObjects/ApplicationLifecycleEventsContainerSO.cs`) — SO container bundling SOAP events for app lifecycle: `OnAppPaused`, `OnAppFocusChanged`, `OnAppQuitting`, `OnSceneLoaded`, `OnSceneUnloading`. Registered in DI.
- `BootstrapConfigSO` — configures: service init timeout, splash duration, framerate, screen sleep, vsync, verbose logging
- `FriendsServiceFacade` (`_Scripts/System/FriendsServiceFacade.cs`) — pure C# class (DI lazy singleton). Single-writer facade for UGS Friends service. Syncs relationship data into `FriendsDataSO`. Supports friend requests, management, presence, and refresh.

See `Assets/_Scripts/System/Bootstrap/BOOTSTRAP_AUDIT.md` for the bootstrap scene audit: root GameObjects, execution order map, applied fixes, and deferred issues.

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

### Multiplayer / Netcode

The game uses Unity Netcode for GameObjects (`com.unity.netcode.gameobjects` 2.5.0) for multiplayer. Key files in `Assets/_Scripts/Controller/Multiplayer/`:

- `ServerPlayerVesselInitializer` — core server-side vessel spawner. Listens for `OnPlayerNetworkSpawnedUlong` SOAP events, waits for NetworkVariables to sync (`preSpawnDelayMs`), spawns the vessel prefab via `VesselPrefabContainer`, injects DI with `GameObjectInjector.InjectRecursive()`, then delegates initialization to `ClientPlayerVesselInitializer`. Tracks processed players by `NetworkObjectId` (not `OwnerClientId`, since AI shares the host's). Uses `NetcodeHooks` (not direct `NetworkBehaviour` inheritance) for spawn/despawn hooks. `ProcessPreExistingPlayers()` catches host Player objects spawned before the initializer loaded. `shutdownNetworkOnDespawn` toggle: `true` for game scenes, `false` for Menu_Main.
- `ClientPlayerVesselInitializer` — common player-vessel pair initialization (extends `NetworkBehaviour`). Server path: called directly by `ServerPlayerVesselInitializer`. Client path: receives RPCs (`InitializeAllPlayersAndVessels_ClientRpc` for new clients, `InitializeNewPlayerAndVessel_ClientRpc` for existing clients). Queues pending `(playerNetId, vesselNetId)` pairs when RPCs arrive before objects replicate — resolved reactively via `OnPlayerNetworkSpawnedUlong` + `OnVesselNetworkSpawned` SOAP events (zero `WaitUntil` polling). `InitializePair()` calls `player.InitializeForMultiplayerMode(vessel)`, `vessel.Initialize(player)`, `ShipHelper.SetShipProperties()`, `gameData.AddPlayer()`, and fires `gameData.InvokeClientReady()` for the local user.
- `ServerPlayerVesselInitializerWithAI` — extends `ServerPlayerVesselInitializer`. Spawns server-owned AI players **before** `base.OnNetworkSpawn()` subscribes to events, so AI spawn events are harmlessly missed. Marks all AI players in `_processedPlayers` so the base class skips them. Picks AI vessel type from `SO_GameList` captains (falls back to Sparrow). Configures `AIPilot` with game-mode-aware seeking and skill level.
- `MenuServerPlayerVesselInitializer` — extends `ServerPlayerVesselInitializer`. Overrides `OnPlayerReadyToSpawnAsync()` to call `base` then `ActivateAutopilot()`: `player.StartPlayer()`, `Vessel.ToggleAIPilot(true)`, `InputController.SetPause(true)`, `CameraManager.SetupEndCameraFollow(vessel.CameraFollowTarget)`. Game data configuration (vessel class, player count, intensity) is handled by `MainMenuController` — this class only handles the network spawn chain and autopilot activation.
- `MenuCrystalClickHandler` — toggles between menu mode (Cinemachine crystal camera + autopilot) and gameplay mode (Cinemachine follows vessel + player control) on Menu_Main. Tap crystal → fade out menu UI, disable autopilot, enable player input, retarget Cinemachine vCam to vessel follow target. Center tap → restore autopilot and menu UI.
- `MultiplayerSetup` — bridges authentication → Netcode host lifecycle. `EnsureHostStarted()` registers Netcode callbacks and calls `nm.StartHost()` exactly once (guarded by `_hostStartInProgress` flag). For multiplayer games: shuts down local host, queries/creates/joins UGS Multiplayer sessions with Relay transport, handles race conditions on session joins. Session properties: `gameMode` (String1), `maxPlayers` (String2). Connection approval auto-creates player objects.
- `NetworkStatsManager` — network health monitoring via `NetworkMonitorData` SOAP type
- `DomainAssigner` — static team pool manager. `Initialize()` fills pool with `[Jade, Ruby, Gold]` (excludes None, Unassigned, Blue). `GetDomainsByGameModes()` picks a random unique domain per player (returns `Domains.Jade` for co-op modes). **Must** be called per session start to prevent duplicate/swapped domains.

Scene loading for multiplayer is handled by `SceneLoader` (`_Scripts/System/SceneLoader.cs`), which extends `NetworkBehaviour` and auto-selects local vs network scene loading based on whether a host/server is running.

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

### Scene Script Inventories

Complete MonoBehaviour script references for each scene. Use these inventories to understand what systems are active in each scene and where to find or add components.

#### Bootstrap Scene (`Assets/_Scenes/Bootstrap.unity`)

**68 MonoBehaviours** | **35 unique script types** (23 first-party + 12 Unity/third-party) | **33 GameObjects** | **8 prefab instances**

The Bootstrap scene hosts all persistent manager/service GameObjects (marked `DontDestroyOnLoad`) that survive across scene loads, plus the splash screen UI and camera rig.

##### Root GameObjects & Prefab Instances

| Root GameObject | Source | Role |
|---|---|---|
| `AppManager` | Prefab (`_Prefabs/CORE/`) | DI root, orchestrator, `SceneTransitionManager` |
| `EventSystem` | Scene | `EventSystem` + `InputSystemUIInputModule` |
| `Camera` | Scene | Main camera with `CinemachineBrain` + URP data |
| `NetworkManager` | Prefab (`_Prefabs/CORE/`) | Netcode `NetworkManager` |
| `AudioSystem` | Prefab (`_Prefabs/CORE/`) | `AudioSystem` (Wwise integration) |
| `PrismManagers` | Scene | `PrismFactory`, `PrismScaleManager`, `MaterialStateManager` + 9 child pool objects |
| `ThemeManager` | Scene | `ThemeManager` |
| `CameraManager` | Scene | `CameraManager` + Cinemachine virtual cameras (CM PlayerCam, CM EndCam, CM DeathCam, CM Main Menu) + camera follow/look targets |
| `GameSettings` | Scene | `GameSetting` |
| `MultiplayerSetup` | Scene | `MultiplayerSetup` |
| `UGSStatsManager` | Scene | `UGSStatsManager` |
| `GamepadDebugger` | Scene | `GamepadDebugger` |
| `PlayerDataService` | Scene | `PlayerDataService` |
| `SceneLoader` | Scene | `SceneLoader` + `NetworkObject` |
| `CaptainManager` | Scene | `CaptainManager` |
| `IAPManager` | Scene | `IAPManager` |
| `Canvas - Splash Screen` | Scene | Splash UI: `CanvasScaler`, loading panel (`Image`), status text (`TextMeshProUGUI`, `LayoutElement`) |
| `PostProcessing` | Prefab (`_Prefabs/Environment/`) | `PostProcessingManager` |
| `CallToActionManager` | Prefab (`_Prefabs/CORE/`) | CTA system |
| `DailyChallengeSystem` | Prefab (`_Prefabs/CORE/`) | Daily challenge system |
| `PartyServices` | Prefab (`_Prefabs/CORE/`) | `HostConnectionService`, `PartyInviteController`, `FriendsInitializer` |
| `StatsManager` | Prefab (`_Prefabs/CORE/`) | `StatsManager` |

##### First-Party Scripts (by category)

**System & Bootstrap**

| Script | Path | GameObject | Instances |
|---|---|---|---|
| `DontDestroyOnLoad` | `_Scripts/Utility/DontDestroyOnLoad.cs` | 15 manager GameObjects | 15 |
| `SceneLoader` | `_Scripts/System/SceneLoader.cs` | SceneLoader | 1 |
| `SceneTransitionManager` | `_Scripts/System/Bootstrap/SceneTransitionManager.cs` | AppManager (prefab) | 1 |
| `AudioSystem` | `_Scripts/System/Audio/AudioSystem.cs` | AudioSystem (prefab) | 1 |
| `IAPManager` | `_Scripts/System/IAPManager.cs` | IAPManager | 1 |
| `CaptainManager` | `_Scripts/System/Playfab/Economy/CaptainManager.cs` | CaptainManager | 1 |
| `GameSetting` | `_Scripts/Controller/Settings/GameSetting.cs` | GameSettings | 1 |

**Managers**

| Script | Path | GameObject | Instances |
|---|---|---|---|
| `CameraManager` | `_Scripts/Controller/Managers/CameraManager.cs` | CameraManager | 1 |
| `ThemeManager` | `_Scripts/Controller/Managers/ThemeManager.cs` | ThemeManager | 1 |
| `PrismScaleManager` | `_Scripts/Controller/Managers/PrismScaleManager.cs` | PrismManagers | 1 |
| `MaterialStateManager` | `_Scripts/Controller/Managers/MaterialStateManager.cs` | PrismManagers | 1 |
| `PostProcessingManager` | `_Scripts/Controller/Managers/PostProcessingManager.cs` | PostProcessing (prefab) | 1 |
| `StatsManager` | `_Scripts/Controller/Managers/StatsManager.cs` | StatsManager (prefab) | 1 |

**Camera & Animation**

| Script | Path | GameObject | Instances |
|---|---|---|---|
| `CustomCameraController` | `_Scripts/Controller/Camera/CustomCameraController.cs` | CM PlayerCam, CM EndCam, CM DeathCam | 5 |
| `RotateAroundOrigin` | `_Scripts/Controller/Animation/RotateAroundOrigin.cs` | EndCam Follow/Look Target, Main Menu Follow Target | 3 |

**Gameplay & Pools**

| Script | Path | GameObject | Instances |
|---|---|---|---|
| `InteractivePrismPoolManager` | `_Scripts/Utility/Effects/InteractivePrismPoolManager.cs` | Per-vessel prism pools (Dolphin, Manta, Rhino, Serpent, Sparrow, Squirrel) + PrismInteractivePool | 7 |
| `PrismFactory` | `_Scripts/Controller/Prisms/PrismFactory.cs` | PrismManagers | 1 |
| `PrismExplosionPoolManager` | `_Scripts/Utility/Effects/PrismExplosionPoolManager.cs` | PrismExplosionPool | 1 |
| `PrismImplosionPoolManager` | `_Scripts/Utility/Effects/PrismImplosionPoolManager.cs` | PrismImplosionPool | 1 |
| `MultiplayerSetup` | `_Scripts/Controller/Multiplayer/MultiplayerSetup.cs` | MultiplayerSetup | 1 |

**Services & UI**

| Script | Path | GameObject | Instances |
|---|---|---|---|
| `PlayerDataService` | `_Scripts/UI/Views/PlayerDataService.cs` | PlayerDataService | 1 |
| `UGSStatsManager` | `_Scripts/UI/UGSStatsManager.cs` | UGSStatsManager | 1 |
| `GamepadDebugger` | `_Scripts/Utility/GamepadDebugger.cs` | GamepadDebugger | 1 |

##### Unity/Third-Party Components

| Component | Package | Instances |
|---|---|---|
| `UniversalAdditionalCameraData` | URP | 4 (Camera, CM PlayerCam, CM EndCam, CM DeathCam) |
| `CinemachineCamera` | Cinemachine | 3 (CM Main Menu, CM EndCam, CM DeathCam) |
| `CinemachineBrain` | Cinemachine | 1 (Camera) |
| `CinemachineFollow` | Cinemachine | 1 (CM Main Menu) |
| `CinemachineRotationComposer` | Cinemachine | 1 (CM Main Menu) |
| `NetworkObject` | Netcode | 1 (SceneLoader) |
| `EventSystem` | EventSystems | 1 |
| `InputSystemUIInputModule` | Input System | 1 |
| `CanvasScaler` | UnityEngine.UI | 1 |
| `Image` | UnityEngine.UI | 1 (LoadingPanel) |
| `LayoutElement` | UnityEngine.UI | 1 (Status Text) |
| `TextMeshProUGUI` | TextMeshPro | 1 (Status Text) |

#### Menu_Main Scene (`Assets/_Scenes/Menu_Main.unity`)

**1,282 MonoBehaviours** | **89 unique script types** (63 first-party + 26 Unity/third-party) | **386 GameObjects**

The Menu_Main scene hosts the main menu UI, the autopilot vessel, and the Cinemachine camera system. All persistent managers from Bootstrap are already alive via `DontDestroyOnLoad`.

##### Core Game Systems (on "Game" GameObject)

| Script | Path |
|---|---|
| `MainMenuController` | `_Scripts/System/MainMenuController.cs` |
| `MenuServerPlayerVesselInitializer` | `_Scripts/Controller/Multiplayer/MenuServerPlayerVesselInitializer.cs` |
| `ClientPlayerVesselInitializer` | `_Scripts/Controller/Multiplayer/ClientPlayerVesselInitializer.cs` |
| `MenuCrystalClickHandler` | `_Scripts/Controller/Multiplayer/MenuCrystalClickHandler.cs` |
| `MainMenuCameraController` | `_Scripts/Controller/Camera/MainMenuCameraController.cs` |
| `NetcodeHooks` | `_Scripts/Utility/Network/NetcodeHooks.cs` |
| `NetworkCrystalManager` | `_Scripts/Controller/Environment/FlowField/NetworkCrystalManager.cs` |
| `FlowFieldData` | `_Scripts/Controller/Environment/FlowField/FlowFieldData.cs` |

##### Screen Controllers

| Script | Path | GameObject |
|---|---|---|
| `ScreenSwitcher` | `_Scripts/UI/ScreenSwitcher.cs` | Screens |
| `HomeScreen` | `_Scripts/UI/Screens/HomeScreen.cs` | HomeScreen |
| `ArcadeScreen` | `_Scripts/UI/Screens/ArcadeScreen.cs` | ArcadeScreen |
| `StoreScreen` | `_Scripts/UI/Screens/StoreScreen.cs` | ArkScreen |
| `HangarScreen` | `_Scripts/UI/Screens/HangarScreen.cs` | Hangar Screen |
| `LeaderboardsMenu` | `_Scripts/UI/Screens/LeaderboardsMenu.cs` | PortScreen |
| `EpisodeScreen` | `_Scripts/UI/Screens/EpisodeScreen.cs` | EpisodeScreen |
| `ProfileScreen` | `_Scripts/UI/Views/ProfileScreen.cs` | ProfileScreen |

##### Modals

| Script | Path | GameObject |
|---|---|---|
| `ArcadeGameConfigureModal` | `_Scripts/UI/Modals/ArcadeGameConfigureModal.cs` | ArcadeGameConfigureModal |
| `DailyChallengeModal` | `_Scripts/UI/Modals/DailyChallengeModal.cs` | DailyChallengeModal |
| `HangarTrainingModal` | `_Scripts/UI/Modals/HangarTrainingModal.cs` | HangarTrainingModal |
| `FactionMissionModal` | `_Scripts/UI/Modals/FactionMissionModal.cs` | ProtectMissionModal |
| `PurchaseConfirmationModal` | `_Scripts/UI/Modals/PurchaseConfirmationModal.cs` | PurchaseItemConfirmationModal |
| `AppInitializationModal` | `_Scripts/UI/Modals/AppInitializationModal.cs` | InitializingScreen |

##### Sub-Views & Panels

| Script | Path | GameObject |
|---|---|---|
| `ArcadeExploreView` | `_Scripts/UI/Views/ArcadeExploreView.cs` | Explore |
| `ArcadeDPadNav` | `Assets/ArcadeDPadNav.cs` | Explore |
| `ArcadeLoadoutView` | `_Scripts/UI/Views/ArcadeLoadoutView.cs` | Loadout |
| `HangarOverviewView` | `_Scripts/UI/Views/HangarOverviewView.cs` | OverviewView |
| `HangarAbilitiesView` | `_Scripts/UI/Views/HangarAbilitiesView.cs` | AbilitiesView |
| `HangarCaptainsView` | `_Scripts/UI/Views/HangarCaptainsView.cs` | CaptainsView |
| `HangarGameplayParameterDisplayGroup` | `_Scripts/UI/Elements/Hangar/HangarGameplayParameterDisplayGroup.cs` | GameplayParameterDisplay |
| `HangarGameplayParameterDisplay` | `_Scripts/UI/Elements/Hangar/HangarGameplayParameterDisplay.cs` | ParameterSlider1-3 (x3) |
| `HangarTrainingGameButton` | `_Scripts/UI/Elements/Hangar/HangarTrainingGameButton.cs` | GameSelectButton1-2 (x2) |
| `PortSquadView` | `_Scripts/UI/Views/PortSquadView.cs` | SquadView (x2) |
| `PortSquadCaptainSelectionView` | `_Scripts/UI/Views/PortSquadCaptainSelectionView.cs` | CaptainSelect |
| `PortSquadMemberConfigureView` | `_Scripts/UI/Views/PortSquadMemberConfigureView.cs` | SquadMemberConfigureModal |
| `PortFactionView` | `_Scripts/UI/Views/PortFactionView.cs` | FactionView |
| `FactionMissionGameView` | `_Scripts/UI/Views/FactionMissionGameView.cs` | LeftColumn |
| `DailyChallengeGameView` | `_Scripts/UI/Views/DailyChallengeGameView.cs` | GameView |
| `DailyChallengeLeaderboardView` | `_Scripts/UI/Views/DailyChallengeLeaderboardView.cs` | LeaderboardView |
| `PartyArcadeView` | `_Scripts/UI/Views/PartyArcadeView.cs` | PartyArea |
| `AddFriendPanel` | `_Scripts/UI/Views/AddFriendPanel.cs` | AddFriendContent |
| `ProfileIconSelectView` | `_Scripts/UI/Views/ProfileIconSelectView.cs` | PlayerDataSelectModal |
| `XPTrackView` | `_Scripts/UI/Views/XPTrackView.cs` | ProfileScreen |
| `VesselSelectionView` | `_Scripts/UI/Views/VesselSelectionView.cs` | Border, Ship Select, ShipSelect (x4) |
| `VesselSelectionItemView` | `_Scripts/UI/Views/VesselSelectionItemView.cs` | Dolphin, Manta, Rhino, Serpent, Sparrow, Squirrel (x6) |

##### UI Elements & Cards

| Script | Path | Instances |
|---|---|---|
| `MenuAudio` | `_Scripts/UI/MenuAudio.cs` | **75** (on most interactive buttons/panels) |
| `ControllerButtonPress` | `_Scripts/Controller/IO/ControllerButtonPress.cs` | **25** (gamepad support on buttons) |
| `NavLink` | `_Scripts/UI/Elements/NavLink.cs` | **13** (tab navigation buttons) |
| `FlipUI` | `_Scripts/Controller/IO/FlipUI.cs` | **7** (screen flip animations) |
| `SquadMemberCard` | `_Scripts/UI/Elements/SquadMemberCard.cs` | **7** |
| `IconEmitter` | `_Scripts/UI/FX/IconEmitter.cs` | **6** (reward/purchase VFX) |
| `NavGroup` | `_Scripts/UI/Elements/NavGroup.cs` | **6** (tab group managers) |
| `CaptainUpgradeSelectionCard` | `_Scripts/UI/Elements/Buttons/CaptainUpgradeSelectionCard.cs` | 4 (Captain 1-4) |
| `GameplayRewardButton` | `_Scripts/UI/Elements/Buttons/GameplayRewardButton.cs` | 4 (tier reward buttons) |
| `CallToActionTarget` | `_Scripts/System/CallToAction/CallToActionTarget.cs` | 4 |
| `DailyChallengeCard` | `_Scripts/UI/Elements/DailyChallengeCard.cs` | 2 |
| `PurchaseGameplayTicketCard` | `_Scripts/UI/Elements/Buttons/PurchaseGameplayTicketCard.cs` | 2 |
| `DailyRewardCard` | `_Scripts/UI/Elements/Buttons/DailyRewardCard.cs` | 1 |
| `FavoriteIcon` | `_Scripts/UI/Elements/FavoriteIcon.cs` | 1 |
| `SliderInitializer` | `_Scripts/UI/Elements/SliderInitializer.cs` | 1 |
| `InfiniteScroll` | `_Scripts/UI/InfiniteScroll.cs` | 1 |
| `TooltipHandler` | `_Scripts/UI/TooltipHandler.cs` | 1 |

##### Input/IO

| Script | Path | Instances |
|---|---|---|
| `PhoneFlipDetector` | `_Scripts/Controller/IO/PhoneFlipDetector.cs` | 1 |
| `FlipUIScreen` | `_Scripts/Controller/IO/FlipUIScreen.cs` | 1 |

##### Unity/Third-Party Components (top counts)

| Component | Package | Instances |
|---|---|---|
| `Image` | UnityEngine.UI | 475 |
| `TextMeshProUGUI` | TextMeshPro | 248 |
| `Slider` | UnityEngine.UI | 162 |
| `LayoutElement` | UnityEngine.UI | 48 |
| `VerticalLayoutGroup` | UnityEngine.UI | 42 |
| `HorizontalLayoutGroup` | UnityEngine.UI | 24 |
| `Mask` | UnityEngine.UI | 11 |
| `ScrollRect` | UnityEngine.UI | 9 |
| `ContentSizeFitter` | UnityEngine.UI | 7 |
| `EventTrigger` | EventSystems | 6 |
| `RectMask2D` | UnityEngine.UI | 6 |
| `ToggleGroup` | UnityEngine.UI | 4 |
| `TMP_Dropdown` | TextMeshPro | 4 |
| `RawImage` | UnityEngine.UI | 3 |
| Others (1-2 each) | Various | `GridLayoutGroup`, `Scrollbar`, `Toggle`, `Button`, `CanvasScaler`, `GraphicRaycaster`, `TMP_InputField`, `EventSystem`, `InputSystemUIInputModule`, `CinemachineBrain`, `UniversalAdditionalCameraData`, `NetworkObject` |

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
| Impact effects | `ImpactorBase` + 11 impactor types, 20+ Effect SO types | `_Scripts/Controller/ImpactEffects/` |
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
| Scene management | `SceneLoader` (NetworkBehaviour, game launch + restart + return-to-menu), `SceneNameListSO` (centralized scene names, DI-registered) | `_Scripts/System/`, `_Scripts/Utility/DataContainers/` |
| Authentication | `AuthenticationServiceFacade` (facade/writer), `AuthenticationController` (MonoBehaviour adapter), `AuthenticationSceneController` (scene UI), `SplashToAuthFlow` (splash routing), `AuthenticationData` / `AuthenticationDataVariable` (SOAP state) | `_Scripts/System/`, `_Scripts/ScriptableObjects/SOAP/ScriptableAuthenticationData/` |
| Friends | `FriendsServiceFacade` (facade/writer for UGS Friends), `FriendsDataSO` (shared data) | `_Scripts/System/`, `_Scripts/Utility/DataContainers/` |
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
