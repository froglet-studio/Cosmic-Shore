# Architecture Patterns: Core

> Moved verbatim from the root `CLAUDE.md`, which indexes every topic file. Paths in this file are relative to the repository root.

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

Existing custom SOAP types (16 subdirectories): `AbilityStats`, `ApplicationState` (`ApplicationStateData` + `ApplicationStateDataVariable` + `ScriptableEventApplicationState` — written by `ApplicationStateMachine`), `AuthenticationData` (+ `NetworkMonitorData`), `ClassType` (VesselClassType + VesselImpactor + debuff events), `CrystalStats`, `FriendData` (`FriendData` struct + `FriendPresenceActivity` `[DataContract]` + `ScriptableEventFriendData` + `ScriptableListFriendData` + `EventListenerFriendData` — relationship & presence data for UGS Friends integration, written by `FriendsServiceFacade`), `GameplaySFX` (gameplay sound effect category events for decoupled audio), `InputEvents`, `PartyData` (PartyInviteData, PartyPlayerData + list variant), `PipData`, `PrismStats`, `Quaternion`, `VesselHUDData`, `Transform`, and `ScriptableEventWithReturn` (generic return channel + `PrismEventChannelWithReturnSO`). Also contains `VesselPrefabContainer.cs` for vessel-class-to-prefab mapping.

#### SOAP Anti-Patterns

- **Do not** use singletons or static events for cross-system communication — use `ScriptableEvent` instead
- **Do not** add direct MonoBehaviour-to-MonoBehaviour references for data sharing — use `ScriptableVariable` instead
- **Do not** use `FindObjectOfType` or service locators to get shared data — wire a `ScriptableVariable` in the inspector
- **Do not** create C# events or `Action` delegates on MonoBehaviours for things that multiple unrelated systems need to observe — use `ScriptableEvent`
- **Do not** duplicate SOAP types — check `Assets/_Scripts/ScriptableObjects/SOAP/` for existing types before creating new ones
- **Do not** type a parameter that receives a SOAP list as `IReadOnlyList<T>` — `Obvious.Soap`'s
  `ScriptableList<T>` implements `IList<T>` and **not** `IReadOnlyList<T>`, so the read-only type
  does not accept one. Take `IList<T>` and simply never write to it. This is an editor-only
  compile error: the base type lives in the monolith, so an out-of-editor syntax check cannot see
  it (see "a check that cannot resolve a type cannot see errors ABOUT that type")
- **Do not** put gameplay logic inside ScriptableVariable/ScriptableEvent classes — they are data containers and channels, not controllers
- **Do not** add if-null guards on ScriptableEvent serialize fields — fail loud on missing references
- **Do not** let a `ScriptableVariable` holding RUNTIME state start a session with whatever the last one left in it.** A SOAP variable is a ScriptableObject **asset**, so in the Editor its value survives play-mode exit — "it starts empty" is only true on a fresh install. Its **single writer must reset it at construction**. Shipped instance: every quit ends in `ApplicationState.ShuttingDown` (a terminal state), so the next play session began there and `ApplicationStateMachine` refused *every* transition for the whole run — `Invalid transition: ShuttingDown → MainMenu` at boot, on every session, for as long as the machine had existed.
- **Do not let a "we're already in the right state, skip the work" fast path skip the state's ANNOUNCEMENT.** Skipping the work and skipping the event look identical at the call site and are not: everything that subscribed is still waiting. Shipped instance: `AuthenticationSceneController`'s already-signed-in branch jumped past the facade, so `OnSignedIn` never re-raised on a reconnect — and that one event is the trunk `HostConnectionService`'s lobby + Relay session, `UGSDataService`'s cloud load and `MultiplayerSetup`'s Netcode wiring all hang off, **and nothing else**. Nothing initialised, and the failure surfaced three layers away as a Relay timeout.
- **Do not assume your listener runs LAST — two subscribers to one `ScriptableEvent` fire in SUBSCRIPTION order, and a Bootstrap object always beats a scene object.** A persistent `DontDestroyOnLoad` service subscribes at app start; anything in Menu_Main or a game scene subscribes when that scene loads, so it runs *after*. That is invisible at both call sites and it decides who wins when the two write the same global. Near-miss: `SceneLoader.LaunchGame` unpauses the game as its first statement on `OnLaunchGame`, and `ModePreviewSession` subscribes to the same event from Menu_Main — so a "restore what I changed" teardown there re-paused `PauseSystem` immediately after, and would have loaded every match launched out of a flying preview at `Time.timeScale = 0`. The fix is not to reorder: a teardown that runs while the app is LEAVING a scene must FORFEIT its restore rather than perform it, because the next loader owns that state. **When you write a restore, ask what else answers the same event and whether the thing you are restoring is still yours by the time you run.**

### Threading & Main-Thread Affinity

See `Docs/THREADING.md` for the full reference. The short version:

UGS SDK (`Unity.Services.*`) and Netcode methods return `System.Threading.Tasks.Task` whose
continuations complete on the .NET ThreadPool. From the ThreadPool, any `UnityEngine.Object`
access throws `EnsureRunningOnMainThread`, and any `Obvious.Soap` `ScriptableEvent.Raise()` runs
its listeners **inline on the off-thread**, surfacing the same crash one level deeper.

**The contract:** every `await` of a UGS / Netcode `Task` uses `.AsMainThread()`:

```csharp
ActiveSession = await MultiplayerService.Instance.CreateSessionAsync(opts).AsMainThread();
```

`.AsMainThread()` (in `Assets/_Scripts/Utility/ClassExtensions/UniTaskExtensions.cs`) awaits
the original task and then awaits `MainThreadDispatcher.SwitchToMainThreadAsync()`, which
marshals onto Unity's captured `SynchronizationContext`. Four overloads cover
`Task`, `Task<T>`, `UniTask`, `UniTask<T>`.

**Why UniTask's own primitives don't work on this version (`com.cysharp.unitask@86b6e6a2e286`):**

UniTask 2.x intentionally bypasses `SynchronizationContext` and `ExecutionContext`:

> *"UniTask always works like `Task.ConfigureAwait(false)` and is not guaranteed that the thread
> before awaiting may match the thread after awaiting."* — UniTask docs.

Consequence:

- `UniTask.SwitchToMainThread()` — awaiter's `IsCompleted` reports `true` from ThreadPool →
  continuation runs **inline** on ThreadPool. Switch is a no-op. ([Cysharp/UniTask#319](https://github.com/Cysharp/UniTask/issues/319), [#151](https://github.com/Cysharp/UniTask/issues/151))
- `UniTask.Yield(PlayerLoopTiming.Update)` — yields, but the resumption is *not* guaranteed on
  main thread because UniTask's `ContinuationQueue` doesn't capture the SyncContext.
  ([Cysharp/UniTask#561](https://github.com/Cysharp/UniTask/discussions/561) — exact symptom we hit.)

Neither primitive is a reliable main-thread switch on this version. The `MainThreadDispatcher` +
`.AsMainThread()` boundary helper bypasses UniTask's bypass by using Unity's own
`SynchronizationContext`, which IS properly main-thread-bound.

**The canary** lives in `SceneTransitionManager.SetFadeImmediate`
(`Assets/_Scripts/System/Bootstrap/SceneTransitionManager.cs`). It reads
`MainThreadDispatcher.IsOnMainThread` and logs `Debug.LogError` with the call stack if a future
UGS call site forgets `.AsMainThread()`. Both the canary and the helper share one main-thread-ID
source — no risk of divergent capture sites.

**When to use which primitive:**

| Situation | Use |
|---|---|
| `await` a UGS / Netcode / cross-thread `Task` | `.AsMainThread()` |
| `await` a `UniTask` you wrote that internally awaits UGS with `.AsMainThread()` | nothing extra at the caller |
| Need main thread without a Task to attach to (e.g., top of a `catch` block) | `await MainThreadDispatcher.SwitchToMainThreadAsync()` |
| Yield one frame for PlayerLoop processing (NOT thread marshaling) | `await UniTask.Yield(PlayerLoopTiming.Update)` — fine for sequencing, not for affinity |
| Assert main thread (debug) | `MainThreadDispatcher.IsOnMainThread` |

The three remaining `Yield(PlayerLoopTiming.Update)` calls in
`Controller/Party/PartyInviteController.cs` (in catch / recovery blocks) are intentional —
they are "wait for the next PlayerLoop tick before handling this exception" semantics, not
threading.

**Anti-patterns to avoid:**

- **Do not** add `await UniTask.SwitchToMainThread()` or `await UniTask.Yield(PlayerLoopTiming.Update)` as a thread-marshaling fix — neither works on this UniTask version. Use `.AsMainThread()`.
- **Do not** raise a SOAP `ScriptableEvent` from a UGS / Netcode callback continuation without ensuring the continuation has resumed on the main thread first — SOAP raises invoke listeners inline.
- **Do not** touch a `UnityEngine.Object` (incl. `== null` checks) in a `Task` continuation without `.AsMainThread()` upstream.
- **Do not** capture `Thread.CurrentThread.ManagedThreadId` in random places to make per-class main-thread checks — read `MainThreadDispatcher.IsOnMainThread` instead, single source of truth.

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

Authentication uses **Unity Gaming Services (UGS)** exclusively. The legacy PlayFab auth path (`AuthenticationManager`, `AuthenticationView`, `PlayFabAccount`) is **deleted**, not merely inert — `Docs/PLAYFAB_RETIREMENT.md`.

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
│ ├─ OnSignedIn SOAP event ──► MultiplayerSetup.EnsureHostStarted()
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
│ └─ gameData.InitializeGame() → raises OnInitializeGame
│
│ Player Spawning Chain (network-driven):
│ ├─ Player.OnNetworkSpawn() [host's Player object, spawned in Auth scene]
│ │   ├─ gameData.Players.Add(this)
│ │   ├─ Raise OnPlayerNetworkSpawnedUlong(OwnerClientId)
│ │   ├─ Resolve display name (PlayerDataService → GameDataSO → UGS fallback)
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
│ │   └─ MainMenuCameraController blends the scene camera onto the gameplay pose, then hands off to CM PlayerCam
│ └─ Center tap → TransitionToMenu:
│     ├─ InputController.SetPause(true), Vessel.ToggleAIPilot(true)
│     ├─ MainMenuCameraController takes over at the player-cam pose and eases back to the menu framing
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
       │                 ──► MultiplayerSetup.EnsureHostStarted()
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
- `GamepadInputStrategy`, `TouchInputStrategy`, `KeyboardInputStrategy` (dual-WASD, the **desktop default**), `DualMouseInputStrategy` (opt-in two-mice flight) — platform-specific implementations. `KeyboardInputStrategy` maps two digital "sticks" (WASD left, P/;/L/' right; Left/Right Shift = the two triggers) and mixes them through `DualStickMix` (the yaw/pitch/speed/roll formulas shared with — and unit-tested against — `GamepadInputStrategy.Reparameterize`). The legacy `KeyboardMouseInputStrategy` remains in the project but is no longer selected.
- `InputController` — manages active strategy and input state. Flight input is gated on `Player.IsLocalPilot` (AI and remote `Player` replicas carry an `InputController` but must not consume local WASD/sticks).
- `IInputStatus` / `InputStatus` — input state container
- Input strategies are swappable per platform/context at runtime
