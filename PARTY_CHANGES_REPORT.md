# Party Game Mode — Complete Change Report

**Branch:** `claude/fix-party-game-mode-aALL3`
**Generated:** 2026-02-26
**Commits:** 40+ commits from initial infrastructure through crystal spawning fixes

---

## Table of Contents

1. [New Files Created](#1-new-files-created)
2. [Modified Files](#2-modified-files)
3. [Scene & Asset Changes](#3-scene--asset-changes)
4. [Architecture Overview](#4-architecture-overview)
5. [Known Issues & Remaining Work](#5-known-issues--remaining-work)

---

## 1. New Files Created

### 1.1 `Assets/_Scripts/Game/Arcade/Party/PartyGameController.cs` (1305 lines)

**Purpose:** Main orchestrator for the party game session. Manages lobby, randomized mini-game rounds, scoring, and final results.

**Key Design Decisions:**
- Mini-game controllers have `IsPartyMode = true` → suppresses autonomous lifecycle, but gameplay mechanics (collisions, race finish, crystals) still work
- `PartyVesselSpawner` (on always-active PartyGameManager) handles vessel spawning so ClientRpcs are never called on disabled GameObjects
- Environment SPVIs are always `InertMode` — they only provide spawn origin data
- Environment `GameCanvas` stays enabled for HUD; `IsPartyMode` suppresses mini-game-specific UI
- Scene camera is disabled during gameplay, re-enabled between rounds

**Key Methods:**

| Method | Purpose |
|--------|---------|
| `OnNetworkSpawn()` | Subscribes to network variables, events, initializes lobby flow |
| `RunLobbyAsync()` | Fills AI slots after solo wait, transitions to WaitingForReady |
| `OnLocalPlayerReady()` | Handles ready button presses across different phases |
| `RunRoundAsync()` | Full round lifecycle: randomize → activate environment → spawn/reposition → countdown → gameplay → end-game cinematic → score |
| `ActivateMiniGameEnvironment()` | Enables correct env, disables others, sets `IsPartyMode` on controllers/crystal managers |
| `DeactivateMiniGameEnvironment()` | Cleans up env after round, resets mini-game controller state |
| `CompleteRound()` | Records round results, advances to next round or final results |
| `OnMiniGameWinnerCalculated()` | Captures winner from mini-game controllers (HexRace, Joust, CrystalCapture) |
| `GetPartyWinner()` | Returns the player with the most games won |
| `static GetMiniGameDisplayName()` | Maps `GameModes` enum to display names |
| `OnQuitParty()` | Shuts down NetworkManager and loads main menu |

**Network State:**
- `NetworkVariable<int> _netCurrentRound` — current round index
- `NetworkVariable<int> _netPhase` — current `PartyPhase` enum value
- `NetworkVariable<int> _netSelectedMiniGameIndex` — which mini-game is active

---

### 1.2 `Assets/_Scripts/Game/Arcade/Party/PartyGameConfigSO.cs` (69 lines)

**Purpose:** ScriptableObject configuration for party game parameters.

```csharp
[CreateAssetMenu(fileName = "PartyGameConfig", menuName = "ScriptableObjects/Party/PartyGameConfig")]
public class PartyGameConfigSO : ScriptableObject
{
    [Range(1, 3)] public int MinPlayers = 1;
    [Range(2, 3)] public int MaxPlayers = 3;
    [Range(1, 10)] public int TotalRounds = 5;
    public List<GameModes> AvailableMiniGames;       // HexRace, Joust, CrystalCapture
    public List<MiniGameEnvironmentEntry> EnvironmentPrefabs;
    public float LobbyWaitTimeSeconds = 120f;
    public float SoloLobbyWaitSeconds = 10f;
    public float PreRoundCountdownSeconds = 3f;
    public float PostCountdownDelaySeconds = 1f;
    public float PostRoundDelaySeconds = 2f;
    public float RoundDurationSeconds = 60f;
}
```

Also contains `MiniGameEnvironmentEntry` — maps `GameModes` to environment prefabs.

---

### 1.3 `Assets/_Scripts/Game/Arcade/Party/PartyPhase.cs` (33 lines)

**Purpose:** Enum for party game phases, synced across the network.

```csharp
public enum PartyPhase
{
    Lobby = 0,
    WaitingForReady = 1,
    Randomizing = 2,
    Countdown = 3,
    Playing = 4,
    RoundResults = 5,
    FinalResults = 6,
    MiniGameReady = 7,
}
```

---

### 1.4 `Assets/_Scripts/Game/Arcade/Party/PartyPlayerState.cs` (15 lines)

**Purpose:** Tracks cumulative party-level state for a single player across all rounds.

```csharp
[System.Serializable]
public class PartyPlayerState
{
    public string PlayerName;
    public Domains Domain;
    public int GamesWon;
    public bool IsAIReplacement;
    public bool IsReady;
}
```

---

### 1.5 `Assets/_Scripts/Game/Arcade/Party/PartyRoundResult.cs` (31 lines)

**Purpose:** Stores the result of a single party round.

```csharp
[System.Serializable]
public class PartyRoundResult
{
    public int RoundIndex;
    public GameModes MiniGameMode;
    public string WinnerName;
    public Domains WinnerDomain;
    public List<PartyRoundPlayerScore> PlayerScores = new();
    public bool IsCompleted => !string.IsNullOrEmpty(WinnerName);
}

[System.Serializable]
public class PartyRoundPlayerScore
{
    public string PlayerName;
    public Domains Domain;
    public float Score;
    public bool IsAIReplacement;
}
```

---

### 1.6 `Assets/_Scripts/Game/Arcade/Party/PartyVesselSpawner.cs` (324 lines)

**Purpose:** Lives on PartyGameManager (active at scene load → registered with Netcode). Handles vessel spawning for party mode so RPCs on NetworkBehaviours that were inactive during the initial spawn sweep are never called.

**Key Methods:**

| Method | Purpose |
|--------|---------|
| `SpawnVesselsForParty(Transform[] spawnOrigins)` | First round: spawns host player vessel + AI opponents |
| `RepositionForNewRound(Transform[] spawnOrigins)` | Round 2+: repositions existing vessels to new spawn origins |
| `SpawnPlayerThenAI()` | Async: spawns player → waits → spawns AI → calls `InitializeAllPlayersForParty_ClientRpc` |
| `SpawnAIOpponents()` | Creates AI player NetworkObjects, assigns domains/vessels/names |
| `InitializeAllPlayersForParty_ClientRpc()` | Safe ClientRpc (on always-active GO) that initializes all players on the client |
| `GetSceneNameForMode()` | Looks up scene name from SO_GameList for cinematic resolution |

---

### 1.7 `Assets/_Scripts/Game/UI/Party/PartyPausePanel.cs` (366 lines)

**Purpose:** Main UI controller for the Party Game pause/scoreboard panel. Shows round tabs, ready/quit buttons, and party state display. Shown instead of the normal pause panel during party mode.

**Key Methods:**

| Method | Purpose |
|--------|---------|
| `Initialize(totalRounds, players)` | Creates round tabs, sets initial state |
| `Show() / Hide() / ForceShow()` | Visibility control via CanvasGroup |
| `OnPhaseChanged(phase)` | Updates ready button text/interactable based on current phase |
| `UpdateRoundResult(roundIndex, result)` | Fills in a completed round tab with winner/scores |
| `SetActiveRound(roundIndex)` | Highlights the current round tab |
| `OnReadyClicked()` | Sends ready signal to `PartyGameController` |
| `OnQuitClicked()` | Forwards quit to `PartyGameController` |

---

### 1.8 `Assets/_Scripts/Game/UI/Party/PartyPauseButton.cs` (87 lines)

**Purpose:** Replaces normal pause button behavior during party mode. On pause: hands vessel control to AI, pauses input, shows party panel. On resume: takes back control. Does NOT pause the game (Time.timeScale stays at 1).

---

### 1.9 `Assets/_Scripts/Game/UI/Party/PartyRoundTab.cs` (237 lines)

**Purpose:** UI component for a single round tab in the Party Pause Panel. Displays game state, winner name, ready count, and per-player scores. Instantiated per round from `PartyPausePanelDataPrefab`.

**Key Methods:**

| Method | Purpose |
|--------|---------|
| `Initialize(roundIndex)` | Sets up empty tab with "Round N" text |
| `SetActive(active)` | Highlights as current round |
| `SetRoundResult(result)` | Shows winner, scores, positions, game name |
| `SetReadyCount(readyCount, totalCount)` | Updates "PLAYERS READY X/Y" text |
| `FormatScore(score, mode)` | Formats time for golf-scored modes, integer for others |

---

### 1.10 `Assets/_Scripts/Game/UI/Party/PartyScoreboard.cs` (62 lines)

**Purpose:** Final scoreboard for party mode. Shows games won per player instead of individual game scores. Displayed after all 5 rounds via the normal `OnShowGameEndScreen` event. Extends `Scoreboard`.

---

### 1.11 `Assets/_Scripts/Game/UI/Party/PartyStatsProvider.cs` (59 lines)

**Purpose:** Provides party-specific stats for the final scoreboard: games won per player, total rounds played, and per-round results. Extends `ScoreboardStatsProvider`.

---

## 2. Modified Files

### 2.1 `Assets/_Scripts/Models/Enums/GameModes.cs`

**Change:** Added `PartyGame = 36` enum value.

```csharp
PartyGame = 36,
```

---

### 2.2 `Assets/_Scripts/Game/Arcade/MiniGameControllerBase.cs`

**Changes:**
- **Added** `IsPartyMode` property: `public bool IsPartyMode { get; set; }`
- Used by all derived controllers to check if they're being orchestrated by PartyGameController

---

### 2.3 `Assets/_Scripts/Game/Arcade/MultiplayerMiniGameControllerBase.cs`

**Changes:**
- **Added** party mode guard in `OnNetworkSpawn()` — if `IsPartyMode`, skips all autonomous init
- **Added** `PartyMode_Activate()` — subscribes to turn-end events, calls `InitializeAfterDelay()`
- **Added** `PartyMode_Deactivate()` — unsubscribes from events
- **Modified** `HandleTurnEnd()` — in party mode, does turn-end work locally (no RPCs), then calls `ExecuteServerTurnEnd()`
- **Modified** `ExecuteServerRoundEnd()` — skips `SyncRoundEnd_ClientRpc` in party mode
- **Modified** `ExecuteServerGameEnd()` — in party mode, fires `WinnerCalculated` locally without `MiniGameEnd`
- **Modified** `SetupNewTurn()` / `SetupNewRound()` — in party mode, raises ready button event directly instead of via ClientRpc

---

### 2.4 `Assets/_Scripts/Game/Arcade/MultiplayerDomainGamesController.cs`

**Changes:**
- **Modified** `OnReadyClicked_()` — in party mode, starts countdown directly (bypassing ServerRpc) since host is both client and server. Uses local lambda callback instead of ClientRpc for `OnCountdownTimerEnded`.

---

### 2.5 `Assets/_Scripts/Game/Arcade/HexRaceController.cs`

**Changes:**
- **Added** party mode guard in `OnNetworkSpawn()` — skips autonomous track spawning when `IsPartyMode`
- **Added** `PartyMode_Activate()` override — resets race state, triggers `SpawnTrackEarly()`
- **Added** `PartyMode_Deactivate()` override — resets race state flags

---

### 2.6 `Assets/_Scripts/Game/Arcade/MultiplayerJoustController.cs`

**Changes:**
- **Added** `PartyMode_Activate()` override — resets collision tracking, score state, and `joustTurnMonitor`
- **Added** `PartyMode_Deactivate()` override — resets final results state

---

### 2.7 `Assets/_Scripts/Game/Arcade/MultiplayerCrystalCaptureController.cs`

**Changes:**
- Added comment noting that party mode activate/deactivate use base class defaults (no custom state to reset)

---

### 2.8 `Assets/_Scripts/Game/Arcade/NetworkScoreTracker.cs`

**Changes:**
- **Added** `OnEnable()` — re-subscribes to score events when environment is reactivated (party mode `SetActive` toggling)
- **Added** `OnDisable()` — unsubscribes from score events to prevent inactive environments' score trackers from responding

---

### 2.9 `Assets/_Scripts/Game/Arcade/NetworkTurnMonitorController.cs`

**Changes:**
- **Added** `OnEnable()` override — re-subscribes to events when environment is reactivated, guarded by `IsSpawned`
- **Added** `OnDisable()` override — calls base to unsubscribe + stop monitors

---

### 2.10 `Assets/_Scripts/Game/Environment/FlowField/CrystalManager.cs`

**Changes:**
- **Added** `IsPartyMode` property: `public bool IsPartyMode { get; set; }`
- This is the base class property checked by `NetworkCrystalManager` for all party mode bypasses

---

### 2.11 `Assets/_Scripts/Game/Environment/FlowField/NetworkCrystalManager.cs`

**Changes (critical fix — most recent commit):**

1. **`OnEnable()`** — Subscribes to crystal events if `IsSpawned || IsPartyMode` (previously only `IsSpawned`)

2. **`SubscribeToCrystalEvents()`** —
   - Now calls `UnsubscribeFromCrystalEvents()` first to prevent double-subscription
   - In party mode, always subscribes to `OnMiniGameTurnStarted` (not `OnClientReady`) to avoid timing issues where crystals spawn before `Cell.Initialize()` runs

3. **`UnsubscribeFromCrystalEvents()`** — Always unsubscribes from both `OnClientReady` and `OnMiniGameTurnStarted` paths

4. **`OnResetForReplay()`** — `IsPartyMode` check (was `IsPartyMode && !IsSpawned`). Also resets `serverBatchAnchorIndex = 0`

5. **`OnTurnEnded()`** — `IsPartyMode` check (was `IsPartyMode && !IsSpawned`)

6. **`OnTurnStarted()`** — `IsPartyMode` check (was `IsPartyMode && !IsSpawned`). Always uses `SpawnBatchIfMissing()` directly

7. **`RespawnCrystal()`** — `IsPartyMode` check (was `IsPartyMode && !IsSpawned`)

8. **`ExplodeCrystal()`** — `IsPartyMode` check (was `IsPartyMode && !IsSpawned`)

**Root cause fixed:** `IsSpawned` stays `true` after `SetActive(false)` (NGO doesn't despawn on disable), so all `IsPartyMode && !IsSpawned` fallbacks never triggered. Crystal spawning fell through to the unreliable NetworkList/RPC path.

---

### 2.12 `Assets/_Scripts/Game/Environment/Cytoplasm/SnowChangerManager.cs`

**Changes:**
- **Added** null safety in `OnCellItemsUpdated()`: checks `cellData.Config == null || cellData.CellTransform == null` before unsubscribing. Stays subscribed until Cell is fully initialized.
- **Added** null safety in `SpawnSnows()`: checks `cellData.Config`, `cellData.Config.CytoplasmPrefab`, and `cellData.CellTransform` before Instantiate.

**Root cause fixed:** In party mode, `OnClientReady` fired at 0.5s but `Cell.Initialize()` didn't run until 1.0s (via `InitializeAfterDelay`). Crystals spawning before Cell init caused null refs here.

---

### 2.13 `Assets/_Scripts/Game/Multiplayer/ServerPlayerVesselInitializer.cs`

**Changes:**
- **Added** `PartyModeState` enum: `Off`, `SpawnMode`, `InertMode`
- **Added** `PartyMode` property and `IsInPartyMode` convenience check
- **Added** `PlayerOrigins` read-only property for PartyGameController to access spawn origins
- **Modified** `OnNetworkSpawn()` — handles `InertMode` (skip everything, vessels exist) and `SpawnMode` (set positions, don't subscribe to OnClientConnected)
- **Modified** `OnNetworkDespawn()` — in party mode, does NOT shut down NetworkManager

---

### 2.14 `Assets/_Scripts/Game/Player/Player.cs`

**Changes:**
- **Added** null guards in `StartPlayer()` and `ResetForPlay()` against null `Vessel`

---

### 2.15 `Assets/_Scripts/Game/AI/AIPilot.cs`

**Changes:**
- **Added** `ConfigureForGameMode(gameData, shouldSeekPlayers, skill)` method for party mode AI configuration

---

### 2.16 `Assets/_Scripts/Game/UI/PauseMenu.cs`

**Changes:**
- **Added** null guard for `gameData.LocalPlayer` in `OnClickResumeGameButton()` and `OnClickPauseGameButton()`

---

### 2.17 `Assets/_Scripts/Game/UI/MiniGameHUD.cs`

**Changes:**
- **Added** pre-game cinematic system with auto-created skip button
- **Added** `ConnectingPanel` auto-discovery from CanvasGroup
- **Added** AI profile-based avatar resolution
- **Added** `RequireClientReady` virtual for multiplayer HUDs
- Comprehensive event subscribe/unsubscribe in `OnEnable()`/`OnDisable()`

---

### 2.18 `Assets/_Scripts/Game/UI/MultiplayerHUD.cs`

**Changes:**
- **Added** `RefreshAllPlayerCards()` on `OnMiniGameTurnStarted`
- **Added** `ResetAllCards()` on `OnResetForReplay`
- Full multiplayer card initialization with AI profile avatars

---

### 2.19 `Assets/_Scripts/MinigameHUD/View/MinigameHUDView.cs`

**Changes:**
- **Added** `ConnectingPanel` component auto-discovery
- **Added** `DoTweenTypewriterAnimator` and `ConnectingDotsAnimator` integration
- **Added** `ClearPlayerList()` method
- **Added** `GetColorForDomain()` with `DomainColorDef` struct
- **Added** `PlayerScoreContainer` and `PlayerScoreCardPrefab` properties

---

### 2.20 `Assets/_Scripts/Utility/DataContainers/EndGameCinematicController.cs`

**Changes:**
- **Added** `IsPartyMode` public field (set by PartyGameController)
- **Modified** `RunCompleteEndGameSequence()` — skips connecting panel when `IsPartyMode`, but still calls `ResetGameForNewRound()` so player regains input
- **Modified** `ResolveCinematicForThisScene()` — in party mode, falls back to `gameData.SceneName` (set per round by party controller) since the active scene is always the party scene, not the mini-game scene

---

### 2.21 `Assets/_Scripts/Game/Multiplayer/NetworkStatsManager.cs`

**Changes:**
- Minor: ensured `OnDisable` null-checks `_netcodeHooks` before unsubscribing

---

## 3. Scene & Asset Changes

### New Scenes
- `Assets/_Scenes/Multiplayer Scenes/MinigamePartyGame.unity` — Party game scene with PartyGameManager (always active), mini-game environments (HexRace, Joust, CrystalCapture), and party UI

### New Prefabs
- `Assets/_Prefabs/Minigame/CrystalCapture_Components.prefab`
- `Assets/_Prefabs/Minigame/HexRace_Components.prefab`
- `Assets/_Prefabs/Minigame/Joust_Components.prefab`
- `Assets/_Prefabs/UI Elements/PartyPausePanelDataPrefab.prefab`

### New ScriptableObject Assets
- `Assets/_SO_Assets/Games/ArcadeGamePartyGame.asset`
- `Assets/_SO_Assets/Games/PartyGameConfig.asset`

### Modified Assets
- `Assets/_SO_Assets/Games/GameLists/OrganicRematchGames.asset` — added PartyGame to the list
- `Assets/_Prefabs/Environment/RacingCellVariant.prefab` — minor changes
- `ProjectSettings/EditorBuildSettings.asset` — added PartyGame scene to build
- `Assets/_Graphics/UniversalRenderPipelineGlobalSettings.asset` — render graph enable

### Modified Existing Scenes
- `Assets/_Scenes/Multiplayer Scenes/MinigameCrystalCaptureMultiplayer_Gameplay.unity`
- `Assets/_Scenes/Multiplayer Scenes/MinigameHexRace.unity`
- `Assets/_Scenes/Multiplayer Scenes/MinigameJoust_Gameplay.unity`

---

## 4. Architecture Overview

```
┌─────────────────────────────────────────────────────────────────┐
│  MinigamePartyGame.unity                                        │
│                                                                 │
│  ┌──────────────────────────┐  ┌────────────────────────────┐  │
│  │ PartyGameManager         │  │ Mini-Game Environments     │  │
│  │ (always active)          │  │ (toggled via SetActive)    │  │
│  │                          │  │                            │  │
│  │ ├─ PartyGameController   │  │ ├─ HexRace_Components     │  │
│  │ │  (NetworkBehaviour)    │  │ │  ├─ HexRaceController   │  │
│  │ │                        │  │ │  ├─ NetworkCrystalMgr   │  │
│  │ │                        │  │ │  ├─ SegmentSpawner      │  │
│  │ ├─ PartyVesselSpawner   │  │ │  └─ GameCanvas (HUD)    │  │
│  │ │  (NetworkBehaviour)    │  │ │                          │  │
│  │ │  - Safe ClientRpcs    │  │ ├─ Joust_Components        │  │
│  │ │                        │  │ │  ├─ JoustController     │  │
│  │ ├─ MultiplayerSetup     │  │ │  ├─ JoustTurnMonitor    │  │
│  │ │                        │  │ │  └─ GameCanvas (HUD)    │  │
│  │ └─ PartyPausePanel (UI) │  │ │                          │  │
│  │                          │  │ └─ CrystalCapture_Comps   │  │
│  └──────────────────────────┘  │    ├─ CaptureController   │  │
│                                 │    ├─ NetworkCrystalMgr   │  │
│                                 │    └─ GameCanvas (HUD)    │  │
│                                 └────────────────────────────┘  │
└─────────────────────────────────────────────────────────────────┘
```

### Flow Per Round

1. **PartyGameController.RunRoundAsync()** randomizes a mini-game
2. Activates the correct environment via `SetActive(true)`
3. Sets `IsPartyMode = true` on the controller and all `CrystalManager` components
4. Sets `SPVI.PartyMode = InertMode` (round 2+) or provides spawn origins (round 1)
5. Calls `controller.PartyMode_Activate()` — subscribes to turn events, runs `InitializeAfterDelay()`
6. Mini-game's own ready button appears → player clicks → countdown → gameplay
7. Game-specific end condition fires `InvokeWinnerCalculated()` → `EndGameCinematicController` runs
8. After cinematic, `InvokeShowGameEndScreen()` fires → `PartyGameController.CompleteRound()` captures results
9. Environment is deactivated, party panel shows round results, waits for ready
10. Next round begins

### Crystal Spawning Path (Party Mode)

```
OnMiniGameTurnStarted (not OnClientReady!)
  → NetworkCrystalManager.OnTurnStarted()
    → if (IsPartyMode) → SpawnBatchIfMissing()  [direct local spawn]
    → else → IsServer → EnsureListSized → write to NetworkList → replication
```

---

## 5. Known Issues & Remaining Work

### Confirmed Working
- Round cycling through all 3 mini-games (HexRace, Joust, CrystalCapture)
- Crystal spawning on all mini-game types
- End-game cinematic per round with correct cinematic definition lookup
- Score tracking and winner calculation
- Party scoreboard with games-won tally
- AI opponents spawning and playing

### Known Issues / Potential Risks

1. **No online multiplayer support** — Party mode currently assumes host-only (solo + AI). The `PartyVesselSpawner` only spawns for the local host client. Online party mode would require extending the spawner to handle remote clients.

2. **Environment prefab references** — The `miniGameEnvironments` list on `PartyGameController` is wired in the Inspector. If environments are reordered or new ones added, the index-based lookup can silently fail.

3. **Track re-generation for HexRace** — `_trackSpawned` guard in `SpawnTrackLocally` prevents double-spawning within a round, but the previous round's track segments may not be fully cleaned up if the environment is deactivated mid-generation.

4. **NetworkVariable cleanup** — `NetworkVariable<int>` fields on `PartyGameController` (round, phase, mini-game index) are not explicitly reset between sessions. If the party game scene is reloaded without a clean NetworkManager restart, stale values may persist.

5. **Cinematic definition fallback** — `EndGameCinematicController.ResolveCinematicForThisScene()` falls back to `gameData.SceneName` in party mode. This requires the party controller to correctly set `gameData.SceneName` before the cinematic fires. If the timing is off, the fallback returns null and no cinematic plays.

6. **SnowChangerManager stays subscribed** — The null safety fix means `SnowChangerManager` stays subscribed to `OnCellItemsUpdated` until the cell is fully initialized. If `Cell.Initialize()` never runs (e.g., environment deactivated before init), the subscription leaks until `OnDisable`.

7. **Memory / object pooling** — Crystals are `Instantiate`/`DestroyCrystal` per round, not pooled. For a 5-round party session this is acceptable, but extending to more rounds or faster cycling could create GC pressure.

8. **UI polish** — The `PartyPausePanel` and `PartyRoundTab` use basic layout. Visual polish (animations, transitions, domain-colored highlights) is not yet implemented for GDC demo quality.

9. **Build verification** — Changes have not been verified against a clean Unity build. Serialization references in the party scene, prefabs, and SO assets should be validated in-editor.
