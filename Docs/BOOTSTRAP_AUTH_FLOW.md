# Bootstrap, Authentication & App State - Full Flow

> Extracted verbatim from `CLAUDE.md` (2026-07-23) so the root file stays a lean
> rules-and-routing dictionary. This is the canonical home of this content now -
> update it here, and keep the corresponding CLAUDE.md digest in sync.

### Bootstrap & Scene Flow

The application uses a unified bootstrap pattern centered on `AppManager`, with `ApplicationStateMachine` tracking the top-level phase:

1. **Bootstrap scene** (build index 0) → `AppManager` configures platform, registers DI bindings, starts auth, transitions to Authentication scene. State: `None → Bootstrapping → Authenticating`.
2. **Authentication scene** → checks cached auth, signs in or shows auth UI. State: `Authenticating → MainMenu`.
3. **Menu_Main scene** → main menu entry point. State: `MainMenu`.

Key classes:
- `AppManager` (`_Scripts/System/AppManager.cs`) — top-level orchestrator and Reflex DI root (`[DefaultExecutionOrder(-100)]`, implements `IInstaller`). Handles platform configuration, DI registration of all persistent managers and SO assets, auth/network startup, splash fade, and scene transition. Lives on a `DontDestroyOnLoad` root.
- `ApplicationStateMachine` (`_Scripts/System/ApplicationStateMachine.cs`) — pure C# class (DI lazy singleton). Single-writer to `ApplicationStateDataVariable` (SOAP). Validates transitions via a table-driven state graph. Auto-subscribes to gameplay SOAP events (`OnSessionStarted`, `OnMiniGameEnd`) and lifecycle events (pause, quit, network loss) for automatic phase transitions. States: `None(0)`, `Bootstrapping(1)`, `Authenticating(2)`, `MainMenu(3)`, `LoadingGame(4)`, `InGame(5)`, `GameOver(6)`, `Paused(7)`, `Disconnected(8)`, `ShuttingDown(9)`.
- `SceneLoader` (`_Scripts/System/SceneLoader.cs`) — persistent scene-loading service. Extends `MonoBehaviour` (DontDestroyOnLoad). Lives in the Bootstrap scene and persists across all scene transitions. Subscribes to SOAP events in code (`OnLaunchGame`, `OnClickToMainMenuButton`, `OnActiveSessionEnd`, `OnClickToRestartButton`) — no per-scene EventListenerNoParam wiring needed. Handles launching gameplay scenes (host-driven Netcode scene load, with a defensive local fallback only when no NetworkManager is active), returning to main menu, and local restart. Registered as a DI singleton via AppManager. Game config sync to clients is handled by `MultiplayerMiniGameControllerBase.SyncGameConfigToClients_ClientRpc()` in the game scene.
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
│     ├─ player.NetDomain.Value = menuVesselDomain (Jade) — server-authoritative
│     │   menu domain reset, BEFORE base so the vessel paints Jade at init
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
