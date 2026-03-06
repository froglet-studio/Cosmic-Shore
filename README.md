# Cosmic Shore

![Cosmic Shore Logo](./README-images/image-5.png)

This is the source code repository for [Cosmic Shore](https://www.froglet.games/), a multigenre space game — "the party game for pilots" — developed by [Froglet Inc.](https://www.froglet.games/), a Delaware C-corp based in Grand Rapids, MI.

Different vessel classes embody gameplay from different genres to connect players across demographics: from casual, hotseat minigames and rewarded dailies to team missions and a structured esport. It's your game and you can play it your way.

*Please don't forget to play and give feedback!*

[![Google Play](./README-images/image-2.png)](https://frogletgames.itch.io/cosmic-shore) [![App Store](./README-images/image-3.png)](https://testflight.apple.com/join/9ReKxeGf)

*Also don't forget to join our community!*

[![Discord](./README-images/image-7.png)](https://discord.com/invite/84TCx5ERjY) [![Reddit](./README-images/image-8.png)](https://www.reddit.com/r/CosmicShore/)

---

## Vessel Classes

Each vessel embodies a different genre of gameplay:

| Vessel | Genre | Description |
|---|---|---|
| **Squirrel** | Racing / Drift | Vaporwave arcade racer — tube-riding along player-generated trails (F-Zero / Redout feel) |
| **Sparrow** | Shooter | Arcade space combat with guns and missiles |
| **Manta** | — | Feature-complete playable vessel |
| **Dolphin** | — | Feature-complete playable vessel |
| **Rhino** | — | Feature-complete playable vessel |
| **Serpent** | — | Playable vessel |
| **Urchin** | — | Playable vessel (AI in development) |
| **Grizzly** | — | Playable vessel (AI in development) |
| **Termite** | — | In development |
| **Falcon** | — | In development |
| **Shrike** | — | In development |

---

## Gameplay

### Create

![Create](./README-images/image.png)

Every HyperSea-worthy vessel is capable of enhancing its colored mass output. Pacifying or controlling biomes is only possible if your color is the majority of mass in the biome. Producing mass is the fundamental way to win a majority and refuel along your journey.

### Disrupt

![Disrupt](./README-images/image-1.png)

Every vessel is also equipped with the ability to destroy, steal, shrink or otherwise disrupt other colors of mass. Filling populated biomes with additional mass can make them more hostile. Sometimes reducing hostile mass is your only option.

### Maneuver

![Maneuver](./README-images/image-9.png)

Maneuvering through the HyperSea is usually a thrilling delight. Knowing where and when to create your own or disrupt enemy mass is pivotal to victory. Each vessel has its own unique abilities to get where they need to be or otherwise extend their reach.

---

## Game Modes

- **Get the Crystal** (2-4 players) — Inspired by games like Ultimate Chicken Horse and Jenga, players progressively increase the challenge until a winner remains.
- **Dolphin Darts** (1-2 players) — Accurately drift into crystals to blow up more of the dartboards than your opponent.
- **Ransack Rally** (1-4 players) — Skim past trails on your way to a biome. Whoever steals more along the way wins.
- **Freestyle Toybox** (1 player) — No rules, time, or score. Do what you want for as long as you like.
- **Duel for the Cell** (1v1) — Create mass as trail blocks and disrupt your opponent's. Greatest mass volume wins.
- **HexRace** (1-4 players) — Crystal-collection racing on procedurally generated tracks. First player to collect all crystals wins. Supports solo with AI opponents, multiplayer with friends, or mixed human+AI lobbies. Intensity scales track complexity (segment count, helix radius). Features elemental comeback mechanics and server-authoritative scoring with golf rules (lower time = better rank).
- **Wildlife Blitz** (1-4 players) — Co-op and competitive variants.
- **Joust** (multiplayer) — Head-to-head combat mode.

## Missions

![Missions](./README-images/image-10.png)

Couriers, refugees, colonists, and more all need safe passage across the galaxy. Navigating the HyperSea is the only practical way to turn 100 light years into a five minute journey. Join the Cosmic Shore's roster of elite guides, leading and protecting the galaxy's travelers as they brave the hazards of the HyperSea.

## Sport

![Sport](./README-images/image-11.png)

A stepping stone to our future dreams of a multi-biome esport, Duel for the Cell provides replayability with different teammates, opponents, and difficulties in every match, all of which can be enjoyed in a variety of environments with their own unique flora, fauna, and effects. Duel for the Cell is a 1v1 match with every player creating mass in the form of trail blocks, and disrupting mass by shielding, stealing or otherwise affecting them. The main objective is to have the greatest mass volume by the end of the match. This adds a third dimension to the classic strategy game focus on area control.

---

## Tech Stack

- **Engine**: Unity 6+ with URP (Universal Render Pipeline)
- **Language**: C#
- **Architecture**: ScriptableObject-driven configuration + SOAP (Scriptable Object Architecture Pattern) for event-driven, decoupled communication
- **Async**: UniTask with CancellationToken throughout
- **DI**: Reflex dependency injection — `AppManager` as root `IInstaller`, lazy singleton factories, `[Inject]` across gameplay and UI systems
- **App state**: `ApplicationStateMachine` — table-driven phase tracking (Bootstrap → Auth → Menu → Game → GameOver) via SOAP `ApplicationStateDataVariable`
- **Auth**: Unity Gaming Services (UGS) Authentication — anonymous sign-in, cached sessions, SOAP-driven state via `AuthenticationDataVariable`
- **Friends**: UGS Friends — `FriendsServiceFacade` single-writer facade, relationship data via `FriendsDataSO`
- **Networking**: Unity Netcode for GameObjects (multiplayer with AI backfill)
- **Camera**: Cinemachine 3.1.2 with per-vessel settings
- **VFX**: VFX Graph, custom HLSL shaders, Shader Graph, procedural skybox
- **Input**: Unity Input System with platform-specific strategy pattern (keyboard/mouse, gamepad, touch)
- **Audio**: Wwise integration + SOAP gameplay SFX events for decoupled audio triggers
- **Haptics**: NiceVibrations (mobile)
- **Animation**: Timeline, DOTween
- **Performance**: Unity Jobs + Burst Compiler, Adaptive Performance, DOTS Entities (incremental adoption)
- **Backend**: Unity Gaming Services (Analytics, CloudSave, Leaderboards, Multiplayer, IAP, Ads), PlayFab (legacy, migrating off), Firebase
- **Testing**: Unity Test Framework (NUnit)
- **Tutorial**: Custom FTUE system with adapter pattern
- **Dialogue**: Custom dialogue system with editor tools
- **Target platforms**: Mobile-first (iOS/Android) with PC/console expansion

---

## Development

See [`CLAUDE.md`](./CLAUDE.md) for architecture patterns, coding standards, and detailed project structure.

See [`GIT_RULES.md`](./GIT_RULES.md) for branching model, commit conventions, and PR standards.

### Application Flow

```
Bootstrap Scene → AppManager (DI root + orchestrator, persists across scenes)
    ├─ Reflex DI: registers all managers, SO assets, services
    ├─ ApplicationStateMachine: None → Bootstrapping → Authenticating
    ├─ AuthenticationServiceFacade → UGS sign-in → SOAP state
    ├─ FriendsServiceFacade → UGS Friends init on auth
    ├─ MultiplayerSetup → starts network host on sign-in
    ├─ SceneLoader → game launch, restart, return-to-menu (NetworkBehaviour)
    └─ SceneTransitionManager → Authentication → Menu_Main (networked)
                                                    │
                                                    ▼
                                        ApplicationState: MainMenu
                                              ScreenSwitcher
                                    ┌────┬────┬────┬────┬────┐
                                    │Store│Arcade│Home│Port│Hangar│
                                    └────┴────┴────┴────┴────┘
                                    ← slide left / right →
                                    + MenuServerPlayerVesselInitializer
                                      (autopilot Squirrel in background)
```

The app boots through a Bootstrap scene where `AppManager` serves as both the top-level orchestrator (`[DefaultExecutionOrder(-100)]`) and the Reflex DI root (`IInstaller`). It configures the platform, registers all persistent managers and SO assets (including `GameDataSO`, `SceneNameListSO`, `ApplicationLifecycleEventsContainerSO`, `ApplicationStateDataVariable`, and `FriendsDataSO`), starts authentication and network monitoring, then transitions to the Authentication scene.

An `ApplicationStateMachine` (pure C# DI singleton) tracks the top-level application phase (`Bootstrapping → Authenticating → MainMenu → LoadingGame → InGame → GameOver`) with special states for `Paused`, `Disconnected`, and `ShuttingDown`. It validates transitions via a table-driven state graph and auto-subscribes to gameplay SOAP events for automatic phase changes. All phase data is written to `ApplicationStateDataVariable` (SOAP), allowing any system to read current state or subscribe to `OnStateChanged`.

Authentication is handled by the `AuthenticationServiceFacade` which writes to a shared SOAP `AuthenticationDataVariable`. On sign-in, `MultiplayerSetup` starts the network host, a `FriendsServiceFacade` initializes the UGS Friends service (syncing into `FriendsDataSO`), and the menu is loaded as a networked scene. Scene loading is managed by `SceneLoader`, a `NetworkBehaviour` that auto-selects local vs network scene loading and handles game restarts.

The Menu_Main scene uses a `ScreenSwitcher` that manages horizontal sliding navigation between five screen panels. A `MenuServerPlayerVesselInitializer` spawns an autopilot Squirrel vessel in the background with camera tracking. Screens implement the `IScreen` interface for lifecycle callbacks (`OnScreenEnter`/`OnScreenExit`), allowing the switcher to notify screens without hard-coded references. See the [Menu Screen Navigation](./CLAUDE.md#menu-screen-navigation-menu_main-scene) and [Authentication & Session Flow](./CLAUDE.md#authentication--session-flow) sections in CLAUDE.md for details.

### Architecture Audits

- **[Bootstrap Scene Audit](./Assets/_Scripts/System/Bootstrap/BOOTSTRAP_AUDIT.md)** — All 16 root GameObjects, execution order map, applied fixes, and deferred refactoring issues
- **[Prism Performance Audit](./Assets/_Scripts/Game/Prisms/PRISM_PERFORMANCE_AUDIT.md)** — Per-prism component stack, Jobs+Burst optimizations, and remaining main-thread bottlenecks

### Project Structure

```
Assets/
├── _Scripts/                  # All first-party C# code (~1,100 files)
│   ├── Controller/            # Gameplay systems (~536 files)
│   │   ├── Vessel/            # Vessel core, actions, prisms, trails
│   │   ├── Environment/       # Cells, crystals, flora/fauna, spawning
│   │   ├── ImpactEffects/     # Impactors + Effect SOs
│   │   ├── Arcade/            # Mini-game controllers, scoring
│   │   ├── Multiplayer/       # Netcode: vessel init, lobby, network stats, menu autopilot
│   │   ├── Camera/            # Per-vessel camera system
│   │   ├── AI/                # AIPilot, AIGunner
│   │   └── ...                # Projectiles, IO, FX, Managers, etc.
│   ├── System/                # Application-level systems (~126 files)
│   │   ├── Bootstrap/         # BootstrapConfigSO, SceneTransitionManager, ApplicationLifecycleManager
│   │   ├── Systems/Auth/      # AuthenticationController (MonoBehaviour adapter)
│   │   ├── Playfab/           # Legacy PlayFab integration (deprecated auth)
│   │   ├── Instrumentation/   # Analytics, Firebase
│   │   ├── Runtime/           # Dialogue runtime
│   │   ├── AppManager.cs      # Top-level orchestrator + Reflex DI root
│   │   ├── ApplicationStateMachine.cs  # App phase state machine (SOAP single-writer)
│   │   ├── SceneLoader.cs     # Scene loading, restart, return-to-menu (NetworkBehaviour)
│   │   ├── AuthenticationServiceFacade.cs
│   │   ├── AuthenticationSceneController.cs
│   │   ├── FriendsServiceFacade.cs  # UGS Friends single-writer facade
│   │   ├── SplashToAuthFlow.cs
│   │   ├── NetworkMonitor.cs
│   │   └── ...                # Audio, LoadOut, Quest, Ads, etc.
│   ├── UI/                    # Game & app UI (~188 files)
│   │   ├── Screens/           # Menu screens (Home, Arcade, Store, Hangar, Leaderboards, Episodes)
│   │   ├── Interfaces/        # IScreen, IVesselHUDController, IVesselHUDView
│   │   ├── Elements/          # Reusable components (NavLink, NavGroup, ProfileDisplayWidget)
│   │   ├── Views/             # PlayerDataService, screen views
│   │   ├── Controller/        # HUD controllers
│   │   ├── Modals/            # ModalWindowManager, Settings, Profile, Purchase dialogs
│   │   ├── ScreenSwitcher.cs  # Central menu navigation (slide + IScreen lifecycle)
│   │   └── ...                # FX, Toast, Animations
│   ├── Data/                  # Enums & data structs
│   ├── ScriptableObjects/     # SO definitions & SOAP types
│   │   └── SOAP/              # Custom SOAP types (16 subdirectories)
│   ├── Utility/               # Effects, pooling, data persistence
│   └── Tests/                 # Edit-mode unit tests
├── _SO_Assets/                # ScriptableObject asset instances
├── _Prefabs/                  # Prefabs organized by category
├── _Scenes/                   # Game scenes (singleplayer, multiplayer, test)
├── FTUE/                      # Tutorial / first-time user experience
└── Plugins/                   # Third-party (SOAP, DOTween, etc.)
```

---

## Copyright and License

Copyright 2022 - 2026 Froglet Inc. Code released under the [MIT](./LICENSE) license.

![Froglet Games](./README-images/image-6.png)
