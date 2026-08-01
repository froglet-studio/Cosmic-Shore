# Multiplayer / Netcode - Components, Player Spawning & AI Backfill

> Extracted verbatim from `CLAUDE.md` (2026-07-23) so the root file stays a lean
> rules-and-routing dictionary. This is the canonical home of this content now -
> update it here, and keep the corresponding CLAUDE.md digest in sync.

### Multiplayer / Netcode

The game uses Unity Netcode for GameObjects (`com.unity.netcode.gameobjects` 2.5.0) for multiplayer. Key files in `Assets/_Scripts/Controller/Multiplayer/`:

- `ServerPlayerVesselInitializer` — core server-side vessel spawner. Listens for `OnPlayerNetworkSpawnedUlong` SOAP events, waits for NetworkVariables to sync (`preSpawnDelayMs`), spawns the vessel prefab via `VesselPrefabContainer`, injects DI with `GameObjectInjector.InjectRecursive()`, then delegates initialization to `ClientPlayerVesselInitializer`. Tracks processed players by `NetworkObjectId` (not `OwnerClientId`, since AI shares the host's). Uses `NetcodeHooks` (not direct `NetworkBehaviour` inheritance) for spawn/despawn hooks. `ProcessPreExistingPlayers()` catches host Player objects spawned before the initializer loaded. The spawner never shuts down the NetworkManager on despawn — under the eager-Relay design the network/Relay persists across all scene transitions and is torn down only by explicit party-leave (`PartyInviteController`) or transport failure (`MultiplayerSetup.OnTransportFailure`).
- `ClientPlayerVesselInitializer` — common player-vessel pair initialization (extends `NetworkBehaviour`). Server path: called directly by `ServerPlayerVesselInitializer`. Client path: receives RPCs (`InitializeAllPlayersAndVessels_ClientRpc` for new clients, `InitializeNewPlayerAndVessel_ClientRpc` for existing clients). Queues pending `(playerNetId, vesselNetId)` pairs when RPCs arrive before objects replicate — resolved reactively via `OnPlayerNetworkSpawnedUlong` + `OnVesselNetworkSpawned` SOAP events (zero `WaitUntil` polling). `InitializePair()` calls `player.InitializeForMultiplayerMode(vessel)`, `vessel.Initialize(player)`, `ShipHelper.SetShipProperties()`, `gameData.AddPlayer()`, and fires `gameData.InvokeClientReady()` for the local user.
- `ServerPlayerVesselInitializerWithAI` — extends `ServerPlayerVesselInitializer`. Spawns server-owned AI players **before** `base.OnNetworkSpawn()` subscribes to events, so AI spawn events are harmlessly missed. Marks all AI players in `_processedPlayers` so the base class skips them. Picks AI vessel type from `SO_GameList` captains (falls back to Sparrow). Configures `AIPilot` with game-mode-aware seeking and skill level. **AI players and vessels are spawned with `destroyWithScene: false`** so they survive the client's end-of-frame scene-transition cleanup — without this the client's scene-load message batches with the AI spawn messages on the same network tick and the client destroys the just-spawned AI NetworkObjects (surfacing as `[Invalid Destroy]` errors on the host and invisible AI on clients). Human vessels are unaffected because `ServerPlayerVesselInitializer` delays spawn by `preSpawnDelayMs` (200 ms), pushing them into a later tick. Because AI no longer gets scene-unload cleanup for free, `MultiplayerMiniGameControllerBase.ExecuteSceneReloadReplay()` explicitly despawns all AI players and vessels before the scene reload; the existing cleanup paths (`SceneLoader.ClearPlayerVesselReferences` for Game→Menu, `NetworkManager.Shutdown` on disconnect) already explicit-despawn AI, so AI does not leak into Menu_Main.
- `MenuServerPlayerVesselInitializer` — extends `ServerPlayerVesselInitializer`. Overrides `OnPlayerReadyToSpawnAsync()` to first reset the player's domain server-side (`NetDomain.Value = menuVesselDomain`, Jade — the ONLY menu domain reset, before vessel spawn so the hull paints Jade at init; replicates to all peers, covering fresh entry, party join, and host-return), then call `base`, then `ActivateAutopilot()`: `player.StartPlayer()`, `Vessel.ToggleAIPilot(true)`, `InputController.SetPause(true)`, `CameraManager.SetupEndCameraFollow(vessel.CameraFollowTarget)`. Game data configuration (vessel class, player count, intensity) is handled by `MainMenuController` — this class only handles the network spawn chain, the menu domain reset, and autopilot activation. The Jade reset is on the **player-spawn** path (`OnPlayerReadyToSpawnAsync`) only; a runtime **vessel swap** (`RequestSwap` → `SwapVesselAsync`) does **not** touch domain — it despawns/respawns the vessel and the new hull keeps the player's current `NetDomain` (`ReInitializePair` re-syncs `Player.Domain` from `NetDomain` before repaint so it can't fall back to Jade / desync the domain-changer toy), and inherits the outgoing vessel's pose (`SetPose`) and speed (`SetInitialSpeed`, captured before despawn) for a seamless swap.
- `MenuCrystalClickHandler` — toggles between menu mode (Cinemachine crystal camera + autopilot) and gameplay mode (Cinemachine follows vessel + player control) on Menu_Main. Tap crystal → fade out menu UI, disable autopilot, enable player input, retarget Cinemachine vCam to vessel follow target. Center tap → restore autopilot and menu UI.
- `MultiplayerSetup` — bridges authentication → Netcode host lifecycle. `EnsureHostStarted()` registers Netcode callbacks and calls `nm.StartHost()` exactly once (guarded by `_hostStartInProgress` flag). For multiplayer games: shuts down local host, queries/creates/joins UGS Multiplayer sessions with Relay transport, handles race conditions on session joins. Session properties: `gameMode` (String1), `maxPlayers` (String2). Connection approval auto-creates player objects.
- `NetworkStatsManager` — network health monitoring via `NetworkMonitorData` SOAP type
- `DomainAssigner` — static team pool manager. `Initialize()` fills pool with `[Jade, Ruby, Gold]` (excludes Blue, the "no team" sentinel). `GetDomainsByGameModes()` picks a random unique domain per player (returns `Domains.Jade` for co-op modes; returns `Domains.Blue` if the pool is exhausted). **Must** be called per session start to prevent duplicate/swapped domains.

Scene loading for multiplayer is handled by `SceneLoader` (`_Scripts/System/SceneLoader.cs`), which extends `MonoBehaviour` and drives a host/server Netcode scene load (with a defensive local fallback only when no NetworkManager is active). `SceneLoader` lives in Bootstrap (DontDestroyOnLoad) and subscribes to SOAP events in code. Game config sync to clients is handled by `MultiplayerMiniGameControllerBase.SyncGameConfigToClients_ClientRpc()` in `OnNetworkSpawn()`.

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
| Vessel prefab mapping | `VesselPrefabContainer.cs` | `_Scripts/ScriptableObjects/SOAP/` |
| NetcodeHooks adapter | `NetcodeHooks.cs` | `_Scripts/Utility/Network/` |
| Game data + SOAP events | `GameDataSO.cs` | `_Scripts/Utility/DataContainers/` |
| Menu scene controller | `MainMenuController.cs` | `_Scripts/System/` |
| Menu sub-state enum | `MainMenuState.cs` | `_Scripts/Data/Enums/` |


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
