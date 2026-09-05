# Scene & Game Mode Reference

Comprehensive documentation of all Unity scenes, game modes, and their related systems in Cosmic Shore. Last updated June 2026.

---

## Naming: "Lava Lamp" and "Freestyle" are the same thing

The open-ended flying experience hosted in **Menu_Main** has two names depending on
perspective: when you are *viewing* it from the menu (autopilot vessels drifting behind
the UI) it is called the **lava lamp**; when you are *playing* it (tap the crystal,
take control) it is called **freestyle**. One system, two names — there is no separate
freestyle game.

A standalone arcade game called "Freestyle" (`GameModes.Freestyle = 7`,
`MinigameFreestyle.unity`, `SinglePlayerFreestyleController`) used to exist. It was a
vestige of the pre-lava-lamp era and has been removed — do not reintroduce it. Its
scored shape-drawing flow was **deleted 2026-08-25** (`ShapeDrawingManager` C15 —
unreachable after the scene went; migrating it would have shipped an untested clock
path). Recover from git if a scored minigame is wanted. `SegmentSpawner` + spawnable
shapes + `ShapeDefinition` remain (SkimRace live; painting toy is the successor).
`MultiplayerFreestyle (28)` is a separate multiplayer sandbox
game scene and still exists.

---

## Scene Inventory

### Core Application Scenes

| Scene | Build Order | Path | Purpose |
|---|---|---|---|
| **Bootstrap** | 0 (must be first) | `_Scenes/Bootstrap.unity` | App entry point: DI registration, platform config, auth start, splash |
| **Authentication** | 1 | `_Scenes/Authentication.unity` | Auth UI, cached session check, NetworkManager host start |
| **Menu_Main** | 2 | `_Scenes/Menu_Main.unity` | Main menu with networked autopilot vessel, screen navigation |
| **SplashScreen** | — | `_Scenes/SplashScreen.unity` | Optional splash screen (used by `SplashToAuthFlow`) |

### Single-Player Game Scenes

| Scene | Path | Game Mode | Controller |
|---|---|---|---|
| **MinigameDuelForTheCell** | `_Scenes/Singleplayer Scenes/` | `DuelForTheCell (8)` | `SinglePlayerDuelForTheCellController` |
| **MinigameWildlifeBlitz** | `_Scenes/Singleplayer Scenes/` | `WildlifeBlitz (26)` | `SinglePlayerWildlifeBlitzController` |

### Multiplayer Game Scenes

| Scene | Path | Game Mode | Controller |
|---|---|---|---|
| **MinigameSkimRace** | `_Scenes/Multiplayer Scenes/` | `SkimRace (33)` | `SkimRaceController` |
| **MinigameFreestyleMultiplayer_Gameplay** | `_Scenes/Multiplayer Scenes/` | `MultiplayerFreestyle (28)` | `MultiplayerFreestyleController` |
| **MinigameScurryMultiplayer_Gameplay** | `_Scenes/Multiplayer Scenes/` | `Scurry (35)` | `ScurryController` |
| **MinigameDuelForCellMultiplayer_Gameplay** | `_Scenes/Multiplayer Scenes/` | `OnlineDuelForTheCell (29)` | `OnlineDuelForTheCellController` |
| **MinigameJoust_Gameplay** | `_Scenes/Multiplayer Scenes/` | `Joust (34)` | `JoustController` |
| **MinigameWildlifeBlitzMultuplayerCoOp** | `_Scenes/Multiplayer Scenes/` | `CoOpWildlifeBlitz (32)` | `CoOpWildlifeBlitzMiniGame` |
| **MinigameAstroLeague** | `_Scenes/Multiplayer Scenes/` | `AstroLeague (37)` | `AstroLeagueController` |
| **MinigameBroodRush** | `_Scenes/Multiplayer Scenes/` | `BroodRush (38)` | `BroodRushController` |
| **MinigameRampage** | `_Scenes/Multiplayer Scenes/` | `Rampage (2)` | `RampageController` |
| **MinigamePeelTheCage** | `_Scenes/Multiplayer Scenes/` | `PeelTheCage (39)` | `PeelTheCageController` |
| **MinigameWildlifeLiberation** | `_Scenes/Multiplayer Scenes/` | `WildlifeLiberation (40)` | `WildlifeLiberationController` |
| **MinigameDogFight** | `_Scenes/Multiplayer Scenes/` | `DogFight (41)` | `DogFightController` |
| **MinigameBends** | `_Scenes/Multiplayer Scenes/` | `Bends (42)` | `BendsController` |
| **MinigameScarabScramble** | `_Scenes/Multiplayer Scenes/` | `ScarabScramble (43)` | `ScarabScrambleController` |
| **MinigameSalvo** | `_Scenes/Multiplayer Scenes/` | `Salvo (44)` | `SalvoController` |
| **MinigameTollway** | `_Scenes/Multiplayer Scenes/` | `Tollway (45)` | `TollwayController` |
| **ArcadeGameMultiplayer2v2CoOpVsAI** | `_Scenes/Multiplayer Scenes/` | `Multiplayer2v2CoOpVsAI (30)` | Variant of domain games controller |
| **MinigameMaelstromMultuplayer** | `_Scenes/Multiplayer Scenes/` | Maelstrom variant | Multi-round tournament format |

### Tool & Test Scenes

| Scene | Path | Purpose |
|---|---|---|
| **Recording Studio** | `_Scenes/Tools/` | Asset recording/capture |
| **MattsRecording Studio** | `_Scenes/Tools/` | Variant recording studio |
| **PhotoBooth** | `_Scenes/Tools/` | Screenshot/photo capture |
| **AudioTestSandbox** | `_Scenes/Game_TestDesign/` | Audio system testing |

---

## Scene Flow

```
Bootstrap (build index 0)
  │
  ├─ AppManager [DefaultExecutionOrder(-100)]
  │   ├─ DontDestroyOnLoad, platform config, DI registration
  │   ├─ ApplicationStateMachine → Bootstrapping
  │   ├─ StartAuthentication() (fire-and-forget)
  │   └─ RunBootstrapAsync()
  │       ├─ Minimum splash duration
  │       ├─ ApplicationStateMachine → Authenticating
  │       └─ Load Authentication scene
  │
  ▼
Authentication Scene
  │
  ├─ Check cached session → skip if already signed in
  ├─ Guest login or auto-anonymous sign-in
  ├─ Wait for PlayerDataService initialization
  ├─ Username setup if needed
  ├─ ApplicationStateMachine → MainMenu
  ├─ Ensure NetworkManager host started
  └─ Load Menu_Main as networked scene
  │
  ▼
Menu_Main Scene (networked)
  │
  ├─ MainMenuController configures game data
  ├─ DomainAssigner.Initialize()
  ├─ MenuServerPlayerVesselInitializer spawns autopilot vessels
  ├─ Menu sub-state: None → Initializing → Ready ↔ Freestyle
  │
  ├─ [Player selects game from Arcade screen]
  │   └─ ArcadeGameConfigureModal → configure intensity/players/vessel
  │       └─ OnStartGameClicked() → gameData.InvokeGameLaunch()
  │
  ▼
SceneLoader.LaunchGame()  [MonoBehaviour, Bootstrap DontDestroyOnLoad]
  │
  ├─ ApplicationStateMachine → LoadingGame
  ├─ SetFadeImmediate(1f) — black screen
  ├─ gameData.ResetRuntimeData()
  └─ Load game scene (network or local)
  │
  ▼
Game Scene (e.g., MinigameSkimRace)
  │
  ├─ MultiplayerMiniGameControllerBase.OnNetworkSpawn()
  │   └─ [Server] SyncGameConfigToClients_ClientRpc — syncs game config to clients
  ├─ Controller.OnNetworkSpawn() / Start()
  ├─ InitializeGame() → spawn players + vessels
  ├─ ApplicationStateMachine → InGame
  ├─ SetupNewRound() → show Ready button
  ├─ Gameplay loop (turns, rounds)
  ├─ EndGame() → ApplicationStateMachine → GameOver
  └─ Replay or ReturnToMainMenu
```

---

## Core Application Scenes — Detailed

### Bootstrap Scene

**File**: `Assets/_Scenes/Bootstrap.unity`
**Controller**: `AppManager` (`Assets/_Scripts/System/AppManager.cs`)
**See also**: `Assets/_Scripts/System/Bootstrap/BOOTSTRAP_AUDIT.md`

The Bootstrap scene is always build index 0. It initializes the application platform, registers all Reflex DI bindings, starts authentication, and transitions to the Authentication scene.

**Key GameObjects**: AppManager, SceneTransitionManager, AudioSystem, GameSetting, ThemeManager, CameraManager, CaptainManager, PrismPools, SceneLoader

**Persistent managers** (marked `DontDestroyOnLoad`): AppManager, SceneTransitionManager, AudioSystem, GameSetting, ThemeManager, CameraManager, CaptainManager, SceneLoader, and party services (HostConnectionService, PartyInviteController, FriendsInitializer).

### Authentication Scene

**File**: `Assets/_Scenes/Authentication.unity`
**Controller**: `AuthenticationSceneController` (`Assets/_Scripts/System/AuthenticationSceneController.cs`)

Handles UGS authentication with auto-skip for cached sessions. Timeouts: cached auth (3s), player data (5s), safety (10s), network host (3s).

**Flow**:
1. Already signed in? → HandlePostAuthFlow → Menu_Main
2. Try cached sign-in → success? → Menu_Main
3. Show auth panel (guest login button)
4. Wait for PlayerDataService initialization
5. Username setup if needed
6. Ensure NetworkManager host is running
7. Navigate to Menu_Main via Netcode scene management

### Menu_Main Scene

**File**: `Assets/_Scenes/Menu_Main.unity`
**Controller**: `MainMenuController` (`Assets/_Scripts/System/MainMenuController.cs`)
**See also**: CLAUDE.md sections on Menu Screen Navigation, Lava-Lamp Mode, Party/Invite Lobby System

The main menu runs as a networked scene. A vessel flies on autopilot in the background. The player navigates between screens (Store, Ark/Arcade, Home, Port/Leaderboards, Hangar) via `ScreenSwitcher`.

**Sub-state machine** (`MainMenuState`):
```
None(0) → Initializing(1) → Ready(2) ⇄ Freestyle(4)
                               │            │
                               ▼            ▼
                          LaunchingGame(3) ←─┘
```

**Key systems in Menu_Main**:
- `MenuServerPlayerVesselInitializer` — spawns autopilot vessel for menu background
- `MenuCrystalClickHandler` — toggles between autopilot and freestyle control
- `MenuVesselSelectionPanelController` — network-aware vessel swapping
- `ScreenSwitcher` — horizontal sliding panel navigation
- `ArcadeLobbyList` / `FriendsListPanel` — party + social (invite) UI
- `MenuMiniGameHUD` — freestyle HUD with vessel change trigger
- Game UI container with `CanvasGroup` for freestyle fade in/out

---

## Game Mode Controller Hierarchy

```
MiniGameControllerBase (abstract, NetworkBehaviour)
│   Template Method: rounds → turns → countdown → gameplay → end
│   Properties: numberOfRounds, numberOfTurnsPerRound, UseGolfRules, HasEndGame
│
├── SinglePlayerMiniGameControllerBase (abstract)
│   │   Start(): subscribe to SOAP events, InitializeGame(), InvokeClientReady()
│   │
│   ├── SinglePlayerDuelForTheCellController — vessel swap on turn end (2-player vs AI)
│   ├── SinglePlayerSlipnStrideController  — procedural course with intensity scaling
│   ├── SinglePlayerWildlifeBlitzController — blitz scoring with wildlife turn monitor
│   └── WildlifeBlitzMiniGame             — minimal variant of wildlife blitz
│
└── MultiplayerMiniGameControllerBase (abstract, NetworkBehaviour)
    │   OnNetworkSpawn(): server-authoritative setup + InitDelayMs (1000ms)
    │   Server-driven turn/round/game flow via ClientRpc synchronization
    │   Replay + Rematch systems via ServerRpc/ClientRpc
    │
    ├── MultiplayerFreestyleController     — per-player activation, player removal protocol
    ├── CoOpWildlifeBlitzMiniGame    — own ready-sync (not domain-based)
    │
    └── MultiplayerDomainGamesController
        │   Ready synchronization: all players must click Ready before countdown
        │   Domain (team) stat calculation on game end
        │   Player disconnect handling via session events
        │
        ├── SkimRaceController              — deterministic track, crystal race, golf scoring
        ├── JoustController      — collision tracking, server-authoritative winner, golf scoring
        ├── OnlineDuelForTheCellController — vessel ownership swap between rounds
        ├── ScurryController — minimal subclass (1 round, 1 turn)
        ├── AstroLeagueController             — hypersea soccer, server-simulated ball, golden goal
        ├── BroodRushController             — nucleus-control fauna-wave race, brood scoring
        └── RampageController                 — destruction race (Scurry's destructive analog), prisms-destroyed scoring
```

---

## Game Modes — Complete Reference

### GameModes Enum (`Assets/_Scripts/Data/Enums/GameModes.cs`)

| ID | Mode | Category | Has Scene | Has Controller |
|---|---|---|---|---|
| 0 | `Random` | Meta | — | — |
| 1 | `Elimination` | SP Arcade | Shared | Scene-configured |
| 2 | `Rampage` | MP | MinigameRampage | `RampageController` (repurposed from legacy SP arcade; destruction race — see `RAMPAGE.md`) |
| 3 | `DolphinDarts` | SP Arcade | Shared | Scene-configured |
| 4 | `ShootingGallery` | SP Arcade | Shared | Scene-configured |
| 5 | `BlockBandit` | SP Arcade | Shared | Scene-configured |
| 6 | `RiskyDriftness` | SP Arcade | Shared | Scene-configured |
| 8 | `DuelForTheCell` | SP Competitive | MinigameDuelForTheCell | `SinglePlayerDuelForTheCellController` |
| 9 | `DashNGrab` | SP Arcade | Shared | Scene-configured |
| 10 | `CellularBrawl` | SP Competitive | Shared | Scene-configured |
| 11 | `Denial` | SP Arcade | Shared | Scene-configured |
| 12 | `CatNMouse` | SP Arcade | Shared | Scene-configured |
| 13 | `SlipNStride` | SP Arcade | Shared | `SinglePlayerSlipnStrideController` |
| 14 | `PumpNDump` | SP Arcade | Shared | Scene-configured |
| 15 | `MasterExploder` | SP Arcade | Shared | Scene-configured |
| 16 | `Soar` | SP Arcade | Shared | Scene-configured |
| 17 | `ObstacleCourse` | SP Arcade | Shared | Scene-configured |
| 18 | `Distraction` | SP Arcade | Shared | Scene-configured |
| 19 | `RhinoRun` | SP Arcade | Shared | Scene-configured |
| 20 | `KickinMass` | SP Arcade | Shared | Scene-configured |
| 21 | `Sidewinder` | SP Arcade | Shared | Scene-configured |
| 22 | `Multipass` | SP Arcade | Shared | Scene-configured |
| 23 | `BotDuel` | SP Competitive | Shared | Scene-configured |
| 24 | `Curvatious` | SP Arcade | Shared | Scene-configured |
| 25 | `MazeRun` | SP Arcade | Shared | Scene-configured |
| 26 | `WildlifeBlitz` | SP Arcade | MinigameWildlifeBlitz | `SinglePlayerWildlifeBlitzController` |
| 27 | `ProtectMission` | SP Mission | Shared | Scene-configured |
| 28 | `MultiplayerFreestyle` | MP | MinigameFreestyleMultiplayer_Gameplay | `MultiplayerFreestyleController` |
| 29 | `OnlineDuelForTheCell` | MP | MinigameDuelForCellMultiplayer_Gameplay | `OnlineDuelForTheCellController` |
| 30 | `Multiplayer2v2CoOpVsAI` | MP | ArcadeGameMultiplayer2v2CoOpVsAI | Variant |
| 32 | `CoOpWildlifeBlitz` | MP | MinigameWildlifeBlitzMultuplayerCoOp | `CoOpWildlifeBlitzMiniGame` |
| 33 | `SkimRace` | MP Racing | MinigameSkimRace | `SkimRaceController` |
| 34 | `Joust` | MP | MinigameJoust_Gameplay | `JoustController` |
| 35 | `Scurry` | MP | MinigameScurryMultiplayer_Gameplay | `ScurryController` |
| 37 | `AstroLeague` | MP | MinigameAstroLeague | `AstroLeagueController` |
| 38 | `BroodRush` | MP | MinigameBroodRush | `BroodRushController` |
| 39 | `PeelTheCage` | MP | MinigamePeelTheCage | `PeelTheCageController` ("Peel the Cage" — see `PEEL_THE_CAGE.md`) |
| 40 | `WildlifeLiberation` | MP | MinigameWildlifeLiberation | `WildlifeLiberationController` (see `WILDLIFE_LIBERATION.md`) |
| 41 | `DogFight` | MP | MinigameDogFight | `DogFightController` (Sparrow gun duel — see `DOGFIGHT.md`) |
| 44 | `Salvo` | MP | MinigameSalvo | `SalvoController` (Sparrow demolition race — see `SALVO.md`) |
| 45 | `Tollway` | MP | MinigameTollway | `TollwayController` (Scarab ring race — see `TOLLWAY.md`) |

Note: IDs 7 and 31 are skipped in the enum. 31 was never assigned; 7 was the retired standalone arcade Freestyle game (freestyle now lives in Menu_Main as the lava lamp — see the naming note at the top of this document). Many single-player arcade modes (1, 3-6, 9-25, 27) share scenes configured by `SO_ArcadeGame` assets rather than having dedicated scene files; they use the same underlying scene infrastructure with different turn monitors, scoring, and environment configurations. `Rampage(2)` left this set — it is now a multiplayer destruction race with its own `MinigameRampage` scene (see `_Scripts/Controller/Arcade/RAMPAGE.md`).

---

## Game Mode Details

### Freestyle (Menu_Main lava lamp)

Freestyle is not a standalone game — it is the playable side of the Menu_Main lava lamp
(see the naming note at the top of this document and the "Lava-Lamp Mode" section of
CLAUDE.md). Tap the crystal in Menu_Main to take control of your vessel; tap the center
to return to autopilot/menu.

The retired standalone game's scored shape-drawing flow (collision → freeze player →
nuke environment → preview → countdown → draw → evaluate → restore) was **deleted
2026-08-25** with `ShapeDrawingManager` (C15 / Prompt 15). It was unreachable after
the scene went; the painting toy is the scoreless successor. Still in the tree:

- `SegmentSpawner.cs` — SkimRace live; also lays trail segments that can carry `ShapeCollisionTrigger`
- `ShapeDefinition` / spawnable shapes / `ShapeSign` — painting toy + SkimRace
- `SinglePlayerFreestyleController.cs` — removed; recover the scored flow from git history if a scored minigame is wanted

### Cellular Duel (Single-Player)

**Scene**: `MinigameDuelForTheCell.unity`
**Controller**: `SinglePlayerDuelForTheCellController`
**Base**: `SinglePlayerMiniGameControllerBase`

Two-player duel where the player alternates between two vessels (playing both sides against AI). Vessel swap happens on turn end.

**Key features**:
- `ShouldResetPlayersOnTurnEnd => true`
- `gameData.SwapVessels()` on turn end — player plays from both perspectives
- Ready button shown at start of each round

### Wildlife Blitz (Single-Player)

**Scene**: `MinigameWildlifeBlitz.unity`
**Controller**: `SinglePlayerWildlifeBlitzController`
**Base**: `SinglePlayerMiniGameControllerBase`

Blitz-mode wildlife collection with dedicated score tracking and turn monitoring.

**Key features**:
- `SinglePlayerWildlifeBlitzScoreTracker` — dedicated score tracker
- `SingleplayerWildlifeBlitzTurnMonitor` — wildlife-specific end condition
- `TimeBasedTurnMonitor` — time-based alternative (currently commented out)
- Score reset on initialization and replay

### SlipNStride (Single-Player)

**Controller**: `SinglePlayerSlipnStrideController`
**Base**: `SinglePlayerMiniGameControllerBase`

Procedurally generated trail-based course with intensity-driven difficulty scaling. Ported from the deprecated `CourseMiniGame`.

**Key features**:
- Procedural course via `SegmentSpawner` with configurable seed
- Intensity scaling: `numberOfSegments = base * Intensity`, `straightLineLength = base / Intensity`
- Optional `SpawnableHelix` for spiral geometry: `radius = Intensity / 1.3`
- `resetEnvironmentOnEachTurn` — configurable course regeneration per turn
- Deterministic replay via fixed seed field

### SkimRace (Multiplayer)

**Scene**: `MinigameSkimRace.unity`
**Controller**: `SkimRaceController`
**Base**: `MultiplayerDomainGamesController`
**See also**: `Assets/_Scripts/Controller/Arcade/SKIMRACE.md`

Competitive 1-4 player crystal-collection racing. Single unified scene — no separate singleplayer scene. Solo play uses AI backfill via `ServerPlayerVesselInitializerWithAI`.

**Key features**:
- Server-authoritative winner determination with `_raceEnded` guard
- Deterministic track: server seed → `_netTrackSeed` NetworkVariable → identical tracks on all clients
- Crystal target: 39 default, synced via `_netCrystalsToFinish` NetworkVariable
- Golf scoring: winner = race time (seconds), loser = `10000 + crystalsRemaining`
- `ElementalComebackSystem` — buffs losing players based on crystal deficit
- Replay: `OnResetForReplayCustom()` generates new track seed

**NetworkVariables**: `_netTrackSeed`, `_netCrystalsToFinish`, `_netCrystalCollisions`
**Server-synced properties**: `WinnerName`, `RaceResultsReady` (via ClientRpc)

**Key files**:
| Role | File | Location |
|---|---|---|
| Game controller | `SkimRaceController.cs` | `_Scripts/Controller/Arcade/` |
| Score tracker | `SkimRaceScoreTracker.cs` | `_Scripts/Controller/Arcade/` |
| Turn monitor | `NetworkCrystalCollisionTurnMonitor.cs` | `_Scripts/Controller/Arcade/TurnMonitors/` |
| Track spawner | `SegmentSpawner.cs` | `_Scripts/Controller/Arcade/` |
| End game | `SkimRaceEndGameController.cs` | `_Scripts/Utility/DataContainers/` |
| HUD | `SkimRaceHUD.cs` | `_Scripts/UI/` |
| Scoreboard | `SkimRaceScoreboard.cs` | `_Scripts/UI/` |
| Comeback | `ElementalComebackSystem.cs` | `_Scripts/Controller/Arcade/` |
| Full docs | `SKIMRACE.md` | `_Scripts/Controller/Arcade/` |

### Multiplayer Joust

**Scene**: `MinigameJoust_Gameplay.unity`
**Controller**: `JoustController`
**Base**: `MultiplayerDomainGamesController`

Collision-based competitive duel. Players collide with each other; first to reach the collision threshold wins.

**Key features**:
- `UseGolfRules => true` — lower score = better
- Server-authoritative collision tracking: clients report via ServerRpc, server monotonically updates counts
- Winner score = elapsed time; loser score = 99999
- `JoustCollisionTurnMonitor` with `CollisionsNeeded` threshold
- Atomic results sync via `FixedString64Bytes[]` / `float[]` / `int[]` arrays in ClientRpc
- `_finalResultsSent` guard prevents duplicate end-game processing

### Multiplayer Cellular Duel

**Scene**: `MinigameDuelForCellMultiplayer_Gameplay.unity`
**Controller**: `OnlineDuelForTheCellController`
**Base**: `MultiplayerDomainGamesController`

Networked vessel-swapping duel for exactly 2 players. Between rounds, players swap vessels via Netcode `ChangeOwnership()`.

**Key features**:
- Vessel ownership swap via `NetworkObject.ChangeOwnership()` + `gameData.SwapVessels()`
- Hardcoded for 2 players (`gameData.Players[0]` and `Players[1]`)
- Vessels swapped back on replay

### Multiplayer Crystal Capture

**Scene**: `MinigameScurryMultiplayer_Gameplay.unity`
**Controller**: `ScurryController`
**Base**: `MultiplayerDomainGamesController`

Minimal domain games subclass — 1 round, 1 turn. Crystal collection goal. All game logic is inherited from base classes + scene-placed turn monitors.

**Key features**:
- `UseGolfRules => false` — higher score = better
- 1 round, 1 turn (set in `OnNetworkSpawn`)
- Near-empty subclass — all flow inherited from `MultiplayerDomainGamesController`

### Astro League

**Scene**: `MinigameAstroLeague.unity`
**Controller**: `AstroLeagueController`
**Base**: `MultiplayerDomainGamesController`

Hypersea soccer (Rocket League-inspired) — two domains slam a server-simulated billiard ball through the opposing goal portal. Match clock with mercy rule and golden-goal overtime. See `Assets/_Scripts/Controller/Arcade/ASTROLEAGUE.md` for the full technical reference.

**Key features**:
- `UseGolfRules => false` — domain with the highest goal sum wins; per-player `Score` = personal `GoalsScored`
- Exactly 2 domains, pinned via `SO_ArcadeGame.MinDomainsAllowed/MaxDomainsAllowed = 2`
- Server-authoritative ball (`AstroLeagueBall` NetworkVariables + client dead reckoning), goal attribution by last non-defending striker (own goals credit the opponent)
- `UseSceneReloadForReplay => true`
- AI strikers via `AIPilot.SetExternalTargetProvider` (billiard approach behind the ball)

### Multiplayer Freestyle

**Scene**: `MinigameFreestyleMultiplayer_Gameplay.unity`
**Controller**: `MultiplayerFreestyleController`
**Base**: `MultiplayerMiniGameControllerBase` (NOT domain games)

Lobby/freestyle sandbox mode. Open-ended multiplayer flying with per-player activation.

**Key features**:
- No scoring, no natural end (`numberOfRounds = int.MaxValue`)
- Per-player countdown activation (each player starts individually, not synchronized)
- Player removal protocol: removes player data from all clients before leaving the session
- Subscribes to `OnClientReady` to handle late-joining clients

### Multiplayer Wildlife Blitz Co-op

**Scene**: `MinigameWildlifeBlitzMultuplayerCoOp.unity`
**Controller**: `CoOpWildlifeBlitzMiniGame`
**Base**: `MultiplayerMiniGameControllerBase` (NOT domain games)

Co-op wildlife blitz with its own ready synchronization pattern.

**Key features**:
- Own ready-sync pattern (not via `MultiplayerDomainGamesController`)
- Server-side `readyClientCount` for synchronization
- Round setup broadcast via ClientRpc

---

## Game Launch Pipeline

### Data Flow Layers

1. **`SO_ArcadeGame` asset** — static config: mode, scene name, multiplayer flag, captains, min/max players/intensity, scoring rules
2. **`ArcadeGameConfigSO`** — ephemeral runtime state: player's chosen game + intensity + player count + vessel
3. **`GameDataSO`** — shared SOAP runtime state: scene name, game mode, vessel class, player count, AI backfill, intensity, all SOAP events
4. **`SceneLoader`** — MonoBehaviour singleton in Bootstrap (DontDestroyOnLoad). Subscribes to `OnLaunchGame`, `OnClickToMainMenuButton`, `OnActiveSessionEnd`, `OnClickToRestartButton` via SOAP code subscriptions. Game config sync to clients handled by `MultiplayerMiniGameControllerBase.OnNetworkSpawn()`
5. **Game controller** — scene-placed `MiniGameControllerBase` subclass drives the turn/round/game lifecycle

### SO_ArcadeGame Configuration

| Field | Type | Purpose |
|---|---|---|
| `Mode` | `GameModes` | Game mode identifier |
| `SceneName` | `string` | Unity scene to load |
| `IsMultiplayer` | `bool` | Whether to use Netcode pipeline |
| `GolfScoring` | `bool` | Lower score = better |
| `Captains` | `List<SO_Captain>` | Available vessel configs |
| `MinPlayers` / `MaxPlayers` | `int` | Player count range (MaxPlayers range 1-3) |
| `MinIntensity` / `MaxIntensity` | `int` | Intensity/difficulty range (MaxIntensity range 1-4) |
| `DisplayName` / `Description` | `string` | UI display text |

SO_ArcadeGame assets are registered in `SO_GameList` ScriptableObject assets at `_SO_Assets/Games/GameLists/` (e.g., `AllGames.asset`, `ArcadeGames.asset`, `LeaderboardGames.asset`). Individual game assets live at `_SO_Assets/Games/` (e.g., `ArcadeGameSkimRace.asset`).

### Launch Sequence

```
ArcadeExploreView.SelectGame(game)
  └─ ArcadeGameConfigureModal.SetSelectedGame(game)
      ├─ Build available vessels from game.Captains
      ├─ Initialize config defaults (intensity, player count)
      └─ Show configuration screens

Player configures and clicks "Start Game"
  └─ ArcadeGameConfigureModal.OnStartGameClicked()
      ├─ SyncAllGameDataForLaunch():
      │   ├─ gameData.SceneName = selectedGame.SceneName
      │   ├─ gameData.GameMode = selectedGame.Mode
      │   ├─ gameData.IsMultiplayerMode = selectedGame.IsMultiplayer
      │   ├─ gameData.SelectedPlayerCount = humanCount (party members)
      │   ├─ gameData.RequestedAIBackfillCount = max(0, configPlayerCount - humanCount)
      │   │   └─ If multiplayer + solo + aiBackfill < 1 → force aiBackfill = 1
      │   └─ gameData.ActiveSession = HostConnectionService.PartySession
      └─ gameData.InvokeGameLaunch() → OnLaunchGame SOAP event

SceneLoader.LaunchGame() [MonoBehaviour, Bootstrap DontDestroyOnLoad, subscribed to OnLaunchGame]
  ├─ PauseSystem.TogglePauseGame(false)
  ├─ ApplicationStateMachine → LoadingGame
  ├─ SceneTransitionManager.SetFadeImmediate(1f) — black screen
  └─ LoadSceneAsync(sceneName)
      Note: Game config sync to clients now handled by
      MultiplayerMiniGameControllerBase.OnNetworkSpawn() in the game scene
      ├─ gameData.ResetRuntimeData()
      ├─ Wait 0.5s for RPC delivery
      └─ Host-driven Netcode scene load (local fallback only if no NetworkManager)
```

---

## Turn Monitor System

Turn monitors determine when a turn ends. They are scene-placed components managed by `TurnMonitorController` (unified class — handles both singleplayer via `OnEnable` and multiplayer via `OnNetworkSpawn`).

| Monitor | File | Trigger Condition |
|---|---|---|
| `TimeBasedTurnMonitor` | `TurnMonitors/` | Elapsed time ≥ duration |
| `NetworkTimeBasedTurnMonitor` | `TurnMonitors/` | Network-aware time-based |
| `CrystalCollisionTurnMonitor` | `TurnMonitors/` | Player collects N crystals |
| `NetworkCrystalCollisionTurnMonitor` | `TurnMonitors/` | Network-aware crystal collection |
| `JoustCollisionTurnMonitor` | `TurnMonitors/` | Player completes N jousts |
| `NetworkJoustCollisionTurnMonitor` | `TurnMonitors/` | Network-aware joust tracking |
| `VesselCollisionTurnMonitor` | `TurnMonitors/` | Player vessel collides with object |
| `CellControlTurnMonitor` | `TurnMonitors/` | Cell ownership conditions met |
| `VolumeCreatedTurnMonitor` | `TurnMonitors/` | Player creates N prisms |
| `VolumeDestroyedTurnMonitor` | `TurnMonitors/` | Player destroys N prisms |
| `AllLifeFormsDestroyedTurnMonitor` | `TurnMonitors/` | All enemies defeated |
| `DistanceTurnMonitor` | `TurnMonitors/` | Player travels N units |
| `ResourceAccumulationTurnMonitor` | `TurnMonitors/` | Player collects N resources |
| `RampagePrismTurnMonitor` | `TurnMonitors/` | A domain's summed hostile-prism destruction reaches the Rampage target |
| `PeelTheCagePrismTurnMonitor` | `TurnMonitors/` | A domain's summed cage destruction reaches the PeelTheCage target |
| `WildlifeKillTurnMonitor` | `TurnMonitors/` | A domain's summed creature kills reach the Wildlife Liberation target |
| `DogFightPointTurnMonitor` | `TurnMonitors/` | A domain's summed gunnery points reach the Dog Fight target |
| `SalvoPrismTurnMonitor` | `TurnMonitors/` | A domain's summed hostile-prism destruction reaches the Salvo target |
| `TollwayTollTurnMonitor` | `TurnMonitors/` | A domain's summed TOLLS reach the Tollway target |

All turn monitors live in `Assets/_Scripts/Controller/Arcade/TurnMonitors/`.

---

## Scoring

### Standard vs Golf Scoring

| Aspect | Standard (`UseGolfRules=false`) | Golf (`UseGolfRules=true`) |
|---|---|---|
| Higher score | Wins (better) | Loses (worse) |
| Used by | Scurry, WildlifeBlitz, most SP modes | SkimRace, Joust |

### Game-Specific Scoring

| Game | Winner Score | Loser Score |
|---|---|---|
| SkimRace | Race time (seconds) | `10000 + crystalsRemaining` |
| Joust | Elapsed time (seconds) | `99999` |
| Crystal Capture | Crystals collected (higher = better) | Crystals collected |
| Astro League | Goals scored (higher = better) | Goals scored |
| Cellular Duel | Standard scoring | Standard scoring |

---

## Player Spawning by Scene Type

### Multiplayer Game Scenes

```
ServerPlayerVesselInitializerWithAI.OnNetworkSpawn()
  ├─ Pre-spawn AI players (RequestedAIBackfillCount)
  └─ Subscribe to OnPlayerNetworkSpawnedUlong
      └─ HandlePlayerNetworkSpawned() → SpawnVesselForPlayer()
          └─ ClientPlayerVesselInitializer.InitializePlayerAndVessel()
```

### Menu_Main Scene

```
MenuServerPlayerVesselInitializer.OnNetworkSpawn()
  └─ ProcessPreExistingPlayers() (catches host Player from Auth scene)
      └─ Override: ActivateAutopilot() after base spawn
          ├─ player.StartPlayer()
          ├─ Vessel.ToggleAIPilot(true)
          └─ InputController.SetPause(true)
```

### Single-Player Scenes

```
MiniGamePlayerSpawnerAdapter.InitializeGame() [on OnInitializeGame]
  └─ PlayerSpawner.SpawnPlayerAndShip(data)
      ├─ Instantiate player + DI inject
      ├─ VesselSpawner.SpawnShip(vesselClass)
      └─ player.InitializeForSinglePlayerMode(data, vessel)
```

---

## Scene Name Registry

All scene names are centralized in `SceneNameListSO` (`Assets/_Scripts/Utility/DataContainers/SceneNameListSO.cs`), registered in DI via `AppManager`. Systems access scene names via `[Inject] SceneNameListSO` instead of hardcoded strings.

| Property | Default Value |
|---|---|
| `BootstrapScene` | `"Bootstrap"` |
| `AuthenticationScene` | `"Authentication"` |
| `MainMenuScene` | `"Menu_Main"` |
| `MultiplayerScene` | `"MinigameFreestyleMultiplayer_Gameplay"` |

Game scene names are stored in `SO_ArcadeGame.SceneName` assets, not in `SceneNameListSO`.

---

## Key File Reference

### Scene Management

| Role | File | Location |
|---|---|---|
| DI root / bootstrap orchestrator | `AppManager.cs` | `_Scripts/System/` |
| App state machine | `ApplicationStateMachine.cs` | `_Scripts/System/` |
| Scene loader (NetworkBehaviour) | `SceneLoader.cs` | `_Scripts/System/` |
| Scene transitions (fade) | `SceneTransitionManager.cs` | `_Scripts/System/Bootstrap/` |
| Scene name registry | `SceneNameListSO.cs` | `_Scripts/Utility/DataContainers/` |
| Auth scene controller | `AuthenticationSceneController.cs` | `_Scripts/System/` |
| Menu scene controller | `MainMenuController.cs` | `_Scripts/System/` |
| Menu state enum | `MainMenuState.cs` | `_Scripts/Data/Enums/` |

### Game Controllers

| Role | File | Location |
|---|---|---|
| Base controller | `MiniGameControllerBase.cs` | `_Scripts/Controller/Arcade/` |
| SP base | `SinglePlayerMiniGameControllerBase.cs` | `_Scripts/Controller/Arcade/` |
| MP base | `MultiplayerMiniGameControllerBase.cs` | `_Scripts/Controller/Arcade/` |
| Domain games base | `MultiplayerDomainGamesController.cs` | `_Scripts/Controller/Arcade/` |
| SkimRace | `SkimRaceController.cs` | `_Scripts/Controller/Arcade/` |
| Joust | `JoustController.cs` | `_Scripts/Controller/Arcade/` |
| Cellular Duel (MP) | `OnlineDuelForTheCellController.cs` | `_Scripts/Controller/Arcade/` |
| Crystal Capture | `ScurryController.cs` | `_Scripts/Controller/Arcade/` |
| Astro League | `AstroLeagueController.cs` | `_Scripts/Controller/Arcade/AstroLeague/` |
| Nucleus Rush (Brood Rush) | `BroodRushController.cs` | `_Scripts/Controller/Arcade/` |
| Rampage | `RampageController.cs` | `_Scripts/Controller/Arcade/` |
| Freestyle (MP) | `MultiplayerFreestyleController.cs` | `_Scripts/Controller/Arcade/` |
| Wildlife Blitz (MP) | `CoOpWildlifeBlitzMiniGame.cs` | `_Scripts/Controller/Arcade/` |
| Cellular Duel (SP) | `SinglePlayerDuelForTheCellController.cs` | `_Scripts/Controller/Arcade/` |
| Wildlife Blitz (SP) | `SinglePlayerWildlifeBlitzController.cs` | `_Scripts/Controller/Arcade/` |
| SlipNStride | `SinglePlayerSlipnStrideController.cs` | `_Scripts/Controller/Arcade/` |
| Countdown timer | `CountdownTimer.cs` | `_Scripts/Controller/Arcade/` |

### Game Data & Configuration

| Role | File | Location |
|---|---|---|
| Game data + SOAP events | `GameDataSO.cs` | `_Scripts/Utility/DataContainers/` |
| Arcade game definition | `SO_ArcadeGame.cs` | `_Scripts/ScriptableObjects/` |
| Game base definition | `SO_Game.cs` | `_Scripts/ScriptableObjects/` |
| Game list registry | `SO_GameList.cs` | `_Scripts/ScriptableObjects/` |
| Runtime config state | `ArcadeGameConfigSO.cs` | `_Scripts/UI/Modals/` |
| Game modes enum | `GameModes.cs` | `_Scripts/Data/Enums/` |
| Application state enum | `ApplicationState.cs` | `_Scripts/Data/Enums/` |
