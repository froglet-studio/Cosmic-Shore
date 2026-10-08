# Branch archive: `claude/stoic-dirac-cBNyV`

_Snapshot 2026-10-08. Index: [README](../README.md)_

- **Last commit:** 2026-05-30 by Claude
- **Unmerged commits:** 1
- **Forked from:** `e1d2096b2` (2026-05-29, Merge pull request #532 from froglet-studio/claude/vibrant-thompson-TTp1e)
- **Tip:** `8d2b7e591`
- **Files touched (1):**
  - `Docs/GAME_FLOW.md`

### `8d2b7e591` — docs(flow): add game flow diagram + multiplayer sync analysis

_Claude, 2026-05-30 07:50:03 +0000_

```text
Diagrams the scene/application-state lifecycle, bootstrap→auth→menu
sequence, game-launch path via SceneLoader, and the server/client
player-vessel spawn handshake.

Analyzes the multiplayer sync issue: the fixed preSpawnDelayMs/
postSpawnDelayMs delays + IsReadyToSpawn retry loop are latency guesses
that race against owner-written NetworkVariable replication, while the
client pending-pair queue already does the correct event-driven thing.
Corrects a false-positive 'missing InvokeClientReady' finding and flags
the vestigial _signalClientReadyWhenDone block.
```

```text
 Docs/GAME_FLOW.md | 277 ++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++
 1 file changed, 277 insertions(+)
```

<details><summary>Patch (code/doc/text files, first 150 of 283 lines)</summary>

```diff
diff --git a/Docs/GAME_FLOW.md b/Docs/GAME_FLOW.md
new file mode 100644
index 000000000..b6b3adadd
--- /dev/null
+++ b/Docs/GAME_FLOW.md
@@ -0,0 +1,277 @@
+# Cosmic Shore — Game Flow & Multiplayer Sync
+
+This document diagrams how the game currently runs: the scene/state lifecycle, the
+script-to-script flow inside each phase, and the multiplayer player/vessel spawn
+handshake. The final section focuses on the **multiplayer sync issue** — where the
+current handshake is fragile and why.
+
+All diagrams are Mermaid. View them in any Mermaid-aware Markdown renderer
+(GitHub, VS Code with the Mermaid extension, etc.).
+
+---
+
+## 1. Top-Level Scene & Application-State Flow
+
+`ApplicationStateMachine` (single writer to `ApplicationStateDataVariable`) tracks
+the top-level phase. Scenes are driven by SOAP events, not direct calls.
+
+```mermaid
+stateDiagram-v2
+    [*] --> Bootstrapping : AppManager.Awake()
+
+    Bootstrapping --> Authenticating : RunBootstrapAsync()<br/>splash done → load Authentication scene
+    Authenticating --> MainMenu : AuthenticationSceneController<br/>NavigateToMainMenu()
+    MainMenu --> LoadingGame : OnLaunchGame → SceneLoader.LaunchGame()
+    LoadingGame --> InGame : OnSessionStarted
+    InGame --> GameOver : OnMiniGameEnd
+    GameOver --> MainMenu : ReturnToMainMenu()<br/>(scene reload)
+    GameOver --> LoadingGame : OnClickToRestartButton (replay)
+
+    state "Special transitions (from any active state)" as special
+    InGame --> Paused : OnAppPaused
+    Paused --> InGame : resume → previous state
+    InGame --> Disconnected : OnNetworkLost
+    Disconnected --> MainMenu : recover
+    MainMenu --> ShuttingDown : OnAppQuitting
+```
+
+| App State | Scene | Driven by |
+|---|---|---|
+| `Bootstrapping` | Bootstrap (build 0) | `AppManager.Awake/Start` |
+| `Authenticating` | Authentication (build 1) | `RunBootstrapAsync()` |
+| `MainMenu` | Menu_Main (build 2) | `AuthenticationSceneController.NavigateToMainMenu()` |
+| `LoadingGame` | (transition) | `SceneLoader.LaunchGame()` on `OnLaunchGame` |
+| `InGame` | Minigame scene | `GameDataSO.OnSessionStarted` |
+| `GameOver` | Minigame scene | `GameDataSO.OnMiniGameEnd` |
+
+---
+
+## 2. Bootstrap → Auth → Menu (script-level sequence)
+
+```mermaid
+sequenceDiagram
+    participant AM as AppManager
+    participant ASM as ApplicationStateMachine
+    participant Auth as AuthenticationServiceFacade
+    participant ASC as AuthenticationSceneController
+    participant MP as MultiplayerSetup
+    participant NM as NetworkManager
+    participant MMC as MainMenuController
+    participant GD as GameDataSO (SOAP)
+
+    AM->>AM: Awake() — DontDestroyOnLoad, ConfigurePlatform()
+    AM->>ASM: TransitionTo(Bootstrapping)
+    AM->>AM: InstallBindings() (Reflex DI)
+    AM->>Auth: StartAuthentication() (fire-and-forget)
+    AM->>AM: RunBootstrapAsync() — splash, fade
+    AM->>ASM: TransitionTo(Authenticating)
+    AM->>AM: Load Authentication scene
+
+    Note over ASC: Authentication scene
+    ASC->>ASC: RunAuthFlowCoreAsync()
+    ASC->>Auth: TrySignInCached / EnsureSignedInAnonymously
+    Auth-->>MP: OnSignedIn SOAP event
+    MP->>NM: EnsureHostStarted() → StartHost() (once)
+    ASC->>ASC: HandlePostAuthFlowAsync() (username, wait PlayerDataService)
+    ASC->>ASM: TransitionTo(MainMenu)
+    ASC->>NM: SceneManager.LoadScene(Menu_Main) [networked]
+
+    Note over MMC: Menu_Main scene
+    MMC->>MMC: ConfigureMenuGameData() (vessel=Squirrel, players=3)
+    MMC->>GD: InitializeGame() → OnInitializeGame
+    GD-->>MMC: OnClientReady (autopilot vessel spawned)
+    MMC->>MMC: TransitionTo(Ready) — menu interactive
+```
+
+The **host NetworkManager starts in the Authentication scene** (`MultiplayerSetup`
+on `OnSignedIn`), then Menu_Main is loaded as a *networked* scene. This is why the
+host's `Player` object already exists before Menu_Main's spawner loads — a fact
+the spawn handshake (§4) has to compensate for.
+
+---
+
+## 3. Game Launch & Return (SceneLoader)
+
+`SceneLoader` (DontDestroyOnLoad, subscribes to SOAP events in code) is the single
+place that loads gameplay scenes and auto-selects local vs. network loading.
+
+```mermaid
+flowchart TD
+    A["UI: select game mode"] -->|OnLaunchGame| B[SceneLoader.LaunchGame]
+    B --> C[ASM → LoadingGame]
+    C --> D{nm.IsListening<br/>&& !nm.IsServer?}
+    D -->|yes: connected client| E["return — defer to server's<br/>Netcode scene load"]
+    D -->|no: host/server/solo| F["useNetworkSceneLoading =<br/>nm != null && nm.IsServer"]
+    F --> G[ClearPlayerVesselReferences<br/>despawn AI + vessels]
+    G --> H[gameData.ResetRuntimeData]
+    H --> I{network?}
+    I -->|yes| J["NetworkManager.SceneManager<br/>.LoadScene(Single)"]
+    I -->|no| K["SceneManager.LoadScene"]
+    J --> L[Game scene:<br/>MultiplayerMiniGameControllerBase.OnNetworkSpawn]
+    L --> M[SyncGameConfigToClients_ClientRpc]
+    M --> N[ServerPlayerVesselInitializerWithAI<br/>spawns humans + AI]
+    N --> O[OnClientReady → FadeFromBlack]
+    O --> P[OnSessionStarted → ASM InGame]
+```
+
+> **MPPM / connected-client guard:** `LaunchGame`, `ReturnToMainMenu`, and
+> `HandleActiveSessionEnd` all `return` early if `nm.IsListening && !nm.IsServer`
+> *after* the visual transition but *before* `LoadSceneAsync()`. SOAP events fire on
+> every virtual player on the shared `GameDataSO`; without this guard a client would
+> race the server's Netcode scene load and destroy AI NetworkObjects before they
+> replicate.
+
+---
+
+## 4. Multiplayer Player/Vessel Spawn Handshake (the sync-critical path)
+
+This is the core of the multiplayer sync behavior. Server spawns the vessel and
+notifies clients; clients queue pairs and resolve them when objects replicate.
+
+```mermaid
+sequenceDiagram
+    autonumber
+    participant P as Player (NetworkBehaviour)
+    participant GD as GameDataSO (SOAP)
+    participant SVI as ServerPlayerVesselInitializer (SERVER)
+    participant CVI as ClientPlayerVesselInitializer (CLIENT)
+
+    Note over P: Player.OnNetworkSpawn()
+    P->>GD: Players.Add(this)
+    P->>GD: Raise OnPlayerNetworkSpawnedUlong(OwnerClientId)
+    P->>P: write NetDefaultVesselType / NetName / NetDomain
+
+    GD-->>SVI: OnPlayerNetworkSpawnedUlong
```

</details>
