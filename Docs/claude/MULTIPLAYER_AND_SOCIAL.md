# Multiplayer, Party, Friends, Player Count, SkimRace

> Moved verbatim from the root `CLAUDE.md`, which indexes every topic file. Paths in this file are relative to the repository root.

### Multiplayer / Netcode

The game uses Unity Netcode for GameObjects (`com.unity.netcode.gameobjects` 2.5.0) for multiplayer. Key files in `Assets/_Scripts/Controller/Multiplayer/`:

- `ServerPlayerVesselInitializer` — core server-side vessel spawner. Listens for `OnPlayerNetworkSpawnedUlong` SOAP events, waits for NetworkVariables to sync (`preSpawnDelayMs`), spawns the vessel prefab via `VesselPrefabContainer`, injects DI with `GameObjectInjector.InjectRecursive()`, then delegates initialization to `ClientPlayerVesselInitializer`. Tracks processed players by `NetworkObjectId` (not `OwnerClientId`, since AI shares the host's). Uses `NetcodeHooks` (not direct `NetworkBehaviour` inheritance) for spawn/despawn hooks. `ProcessPreExistingPlayers()` catches host Player objects spawned before the initializer loaded. The spawner never shuts down the NetworkManager on despawn — under the eager-Relay design the network/Relay persists across all scene transitions and is torn down only by explicit party-leave (`PartyInviteController`) or transport failure (`MultiplayerSetup.OnTransportFailure`).
- `ClientPlayerVesselInitializer` — common player-vessel pair initialization (extends `NetworkBehaviour`). Server path: called directly by `ServerPlayerVesselInitializer`. Client path: receives RPCs (`InitializeAllPlayersAndVessels_ClientRpc` for new clients, `InitializeNewPlayerAndVessel_ClientRpc` for existing clients). Queues pending `(playerNetId, vesselNetId)` pairs when RPCs arrive before objects replicate — resolved reactively via `OnPlayerNetworkSpawnedUlong` + `OnVesselNetworkSpawned` SOAP events (zero `WaitUntil` polling). `InitializePair()` calls `player.InitializeForMultiplayerMode(vessel)`, `vessel.Initialize(player)`, `ShipHelper.SetShipProperties()`, `gameData.AddPlayer()`, and fires `gameData.InvokeClientReady()` for the local user.
- `ServerPlayerVesselInitializerWithAI` — extends `ServerPlayerVesselInitializer`. Spawns server-owned AI players **before** `base.OnNetworkSpawn()` subscribes to events, so AI spawn events are harmlessly missed. Marks all AI players in `_processedPlayers` so the base class skips them. Picks AI vessel type from `SO_GameList` captains (falls back to Sparrow). Configures `AIPilot` with game-mode-aware seeking and skill level. **AI players and vessels are spawned with `destroyWithScene: false`** so they survive the client's end-of-frame scene-transition cleanup — without this the client's scene-load message batches with the AI spawn messages on the same network tick and the client destroys the just-spawned AI NetworkObjects (surfacing as `[Invalid Destroy]` errors on the host and invisible AI on clients). Human vessels are unaffected because `ServerPlayerVesselInitializer` delays spawn by `preSpawnDelayMs` (200 ms), pushing them into a later tick. Because AI no longer gets scene-unload cleanup for free, `MultiplayerMiniGameControllerBase.ExecuteSceneReloadReplay()` explicitly despawns all AI players and vessels before the scene reload; the existing cleanup paths (`SceneLoader.ClearPlayerVesselReferences` for Game→Menu, `NetworkManager.Shutdown` on disconnect) already explicit-despawn AI, so AI does not leak into Menu_Main.
- `MenuServerPlayerVesselInitializer` — extends `ServerPlayerVesselInitializer`. Overrides `OnPlayerReadyToSpawnAsync()` to first reset the player's domain server-side (`NetDomain.Value = menuVesselDomain`, Jade — the ONLY menu domain reset, before vessel spawn so the hull paints Jade at init; replicates to all peers, covering fresh entry, party join, and host-return), then call `base`, then `ActivateAutopilot()`: `player.StartPlayer()`, `Vessel.ToggleAIPilot(true)`, `InputController.SetPause(true)`, `CameraManager.SetupEndCameraFollow(vessel.CameraFollowTarget)`. Game data configuration (vessel class, player count, intensity) is handled by `MainMenuController` — this class only handles the network spawn chain, the menu domain reset, and autopilot activation. The Jade reset is on the **player-spawn** path (`OnPlayerReadyToSpawnAsync`) only; a runtime **vessel swap** (`RequestSwap` → `SwapVesselAsync`) does **not** touch domain — it despawns/respawns the vessel and the new hull keeps the player's current `NetDomain` (`ReInitializePair` re-syncs `Player.Domain` from `NetDomain` before repaint so it can't fall back to Jade / desync the domain-changer toy), and inherits the outgoing vessel's pose (`SetPose`) and speed (`SetInitialSpeed`, captured before despawn) for a seamless swap.
- `MenuCrystalClickHandler` — toggles between menu mode (autopilot + `MainMenuCameraController` vessel-framing rig) and gameplay mode (CM PlayerCam + player control) on Menu_Main. Tap crystal → fade out menu UI, disable autopilot, enable player input; the camera controller blends onto the gameplay pose and hands off to CM PlayerCam. Center tap → restore autopilot and menu UI. **The menu camera uses NO Cinemachine**: `MainMenuCameraController` drives the scene camera directly through `MenuCameraConfigSO` configurations. **What a config frames is decided by its `MenuCameraRigKind`, never by a target field** — a menu camera still cannot be authored to point at an arbitrary object. Orbit / trail / chase / top-down frame the LOCAL VESSEL; **`LavaLamp` frames the CELL** — the original ambience shot, a ~2-minute orbit of the cell centre aimed at the crystal, with the vessel just one of the things drifting through the shot. Being the only vessel-free rig it runs from scene load instead of waiting on the spawn chain, and it is the only kind that reads `CellRuntimeDataSO` (optional — it falls back to `Cell.FindNearestActiveCell` and to aiming at the cell centre). Its timing and damping reproduce the pre-2025 Cinemachine rig measured from the legacy `CM Main Menu` vCam still in `Bootstrap.unity` (2.83°/s, +30 lift, composer damping 10 → `rotationSharpness` 0.45), but **its radius is 686, not the legacy 350, because the nucleus roughly doubled** (`Node2.fbx` half-extent 0.9798 × `Nucleus.prefab` scale 400 ≈ **392**, vs ~200 then) — at 350 the camera now orbits *inside* the nucleus and it overflows the frame ~2×; 686 re-derives the legacy edge-to-edge framing against the bigger nucleus. The hard ceiling is the **toys**, which `ToyboxController` rings at `MembraneRadius × membraneFraction` (1200 × 0.82 = 984) with a 42-unit trigger, so any radius under 942 stays clear of them; re-derive it if any of those three change. **Roll comes only from `lavaLampPoleBlendStart`**: world-up gives an exactly level horizon, so every degree of roll is `ComputeLookUpHint`'s blend sliding the hint toward the orbit axis above `|dot(viewDir, up)| > start`. It is a ROLL dial, not a numerical-safety limit (`LookRotation` is fine to ~0.9999), which is why the original 0.85 was wrong here — it fired on 43% of crystal spawns for a median 5.3° tilt. Default **0.99** yields provably zero roll: the measured worst case is 0.9859 at R=686/45°. A pole-CROSSING orbit (the legacy `(0,1,-1)` cone) must lower it instead, and a future nucleus growth needs the worst case re-checked since the crystal ball scales with it. Full derivation + tables: `Docs/CameraMigrationReview.md`.
- `MultiplayerSetup` — bridges authentication → Netcode host lifecycle. `EnsureHostStarted()` registers the Netcode callbacks (via the public `EnsureNetcodeCallbacksWired()`, which the offline path reuses) and then **waits** — it does **not** call `nm.StartHost()`. The host comes up as a side effect of `HostConnectionService` creating a Relay-backed session, or, when UGS is unreachable, from `OfflineModeService` starting a plain `127.0.0.1` host (`Docs/OFFLINE_MODE.md`). An earlier version of this line claimed `EnsureHostStarted` called `StartHost` "exactly once"; it never did, and that phantom is why offline play was impossible for so long. For multiplayer games: shuts down local host, queries/creates/joins UGS Multiplayer sessions with Relay transport, handles race conditions on session joins. Stands down entirely while `GameDataSO.IsOfflineSession` is set. Session properties: `gameMode` (String1), `maxPlayers` (String2). Connection approval auto-creates player objects.
- `NetworkStatsManager` — network health monitoring via `NetworkMonitorData` SOAP type
- **`DomainAssigner` is DELETED** (with `DomainAssignerTests`) — this line documented a static team pool that assigned a domain at spawn, and an earlier version of the spawn chain below showed `Player.OnNetworkSpawn` calling it. **Nothing assigns a domain at spawn any more**, and `OnNetworkSpawn` says so in a comment. A human's domain has exactly two sources: the `NetDomain` initializer (**Jade**) and the owner's own pick through `Player.RequestSetDomain_ServerRpc`, which **rejects `Domains.Blue`**. `ServerPlayerVesselInitializerWithAI.NormalizeUnassignedHumans` may later move a human off a domain outside the match's active set, but only ever onto one IN it (`GetBalancedDomain`, whose error path returns `ActiveDomains[0]` = Jade). **So no code path can put a human on Blue** — if a player appears to be on the sentinel, look for a domain PICK that never reached the server, and note that **Jade's authored palette is teal-and-blue** (`TrailHighlightColor` (0.05, 0.75, 0.71), core (0.00, 0.39, 0.75)), so an unchanged Jade default reads on screen as "the blue domain".

Scene loading for multiplayer is handled by `SceneLoader` (`_Scripts/System/SceneLoader.cs`), which extends `MonoBehaviour` and drives a host/server Netcode scene load (with a defensive local fallback only when no NetworkManager is active). `SceneLoader` lives in Bootstrap (DontDestroyOnLoad) and subscribes to SOAP events in code. Game config sync to clients is handled by `MultiplayerMiniGameControllerBase.SyncGameConfigToClients_ClientRpc()` in `OnNetworkSpawn()`.

**MPPM / connected-client guard**: `LaunchGame`, `ReturnToMainMenu`, and `HandleActiveSessionEnd` all check `if (nm.IsListening && !nm.IsServer) return` after visual setup (fade-to-black, state transition, `OnClientReady` subscription) but before `LoadSceneAsync()`. In Multiplayer Play Mode, SOAP events on the shared `GameDataSO` fire on every virtual player, so without this guard a client's `SceneLoader` would call `SceneManager.LoadScene()` locally and race the server's Netcode scene load — destroying AI NetworkObjects before they replicate. The guard lets connected clients keep the smooth visual transitions while deferring the actual scene load to the server's Netcode scene management. **Each guard also ARMS a scene-follow watchdog** (`SceneLoader.ArmClientSceneFollowWatchdog`, 90 s, retired by the next SINGLE scene load): the guard covers the screen and returns, so a scene event that never arrives was a permanent black screen with no timeout and no way out — `Docs/PartySystem/BUGS.md` B19. **The guard is an MPPM construct and the watchdog's real call site is elsewhere**: a SOAP raise is LOCAL and does not cross the wire, so on separate machines a client never runs `LaunchGame` or `ReturnToMainMenu` at all — these three fire for MPPM virtual players sharing one process and one `GameDataSO`. What blacks out a SHIPPED client's screen is `MultiplayerMiniGameControllerBase.ShowReturnToMenuVeil_ClientRpc`, which arms it too. Arming only here would look correct in the editor and protect nobody in a build.

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
| `NetDomain` | Everyone | Server | Team assignment. Initialized to **Jade**; changed only by the owner's `RequestSetDomain_ServerRpc` (Blue rejected) or the active-set normalizer |
| `NetName` | Everyone | Owner | Display name (3-tier fallback: PlayerDataService → GameDataSO cache → UGS PlayerName) |
| `NetVesselId` | Everyone | Server | Linked vessel's `NetworkObjectId` |
| `NetIsAI` | Everyone | Server | AI flag |
| `NetAvatarId` | Everyone | Owner | Profile avatar ID |
| `NetArenaReady` | Everyone | Server | True once THIS player's machine has finished building the arena. Reported by the owner (`ReportArenaReady`), reset per scene, true by construction for an AI. The connecting panel's roster reads it |
| `NetSpectatorCount` | Everyone | Server | How many spectators are watching this pilot. Written only from `SpectatorSession`'s watch book; reset per scene |
| `NetUgsPlayerId` | Everyone | Owner | The owner's UGS auth PlayerId — the real identity behind `NetName`. Empty for AI |
| `NetRematchVote` | Everyone | Server | True once this player has asked for a REMATCH on the current scoreboard. Written only by the controller's rematch ServerRpc (keyed on the RPC's own sender, so a client can only vote for itself); reset per scene and on every replay. The scoreboard draws a FACE per vote from it — a tally that travels as a transient count says how many and never WHO |

**Every server-write row above is a fact only ONE machine can know, reported by the machine that
knows it.** `NetArenaReady`, `NetRematchVote` and the six stat-report RPCs on `Player`
(`ReportFaunaKill`, `ReportCombatHit`, `ReportPrismStolen`, `ReportEnvironmentPrismDestroyed`,
`ReportSwitchThreaded`, `ReportFusesBeaten` — all `_ServerRpc`) are one family: the owner detects,
the server records, everyone reads. Reach for it whenever a peer
knows something the server cannot see — and prefer replicated STATE over an announcement whenever a
peer that looks LATER still needs the answer.

**`IPlayer.IsLocalUser` vs `IPlayer.IsLocalPilot`.** `IsLocalUser` (= `IsMultiplayerOwner`) is the networked path's "locally-owned, non-AI player". `IsLocalPilot` is broader by exactly one case: the legacy NON-NETWORKED single-player spawn path (`PlayerSpawner` → `InitializeForSinglePlayerMode`, used today only by the `BenchmarkStressTest` scene) never network-spawns its Player, so `IsSpawned` is false there and `IsLocalUser` reports false for a human. **Anything that must hold in EVERY game mode binds on `IsLocalPilot`**, so a mode cannot escape a platform system by choosing the other spawn path — the prism occlusion corridor is the reference case.

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
| 2 | `MainMenuController` | `gameData.InitializeGame()` |
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

Two UGS sessions layer here: a **Presence Lobby** (lobby-only, no Relay, ≤100 players — discovery + invite property exchange) and a **Party Session** (Relay-backed, ≤4 — actual gameplay networking). Both coexist with an active NetworkManager; invites are per-player lobby properties, so no host privilege is needed. Full tables + rationale: `Docs/PresenceSystem/ARCHITECTURE.md` and `Docs/PartySystem/ARCHITECTURE.md`.

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

The UI-level click → send → detect → accept flow, plus the `invite_payloads`
per-property format, lives in **`Docs/PartySystem/UI.md`** (UI surface); the
service/SOAP happy path is in **`Docs/PartySystem/ARCHITECTURE.md`** § "SOAP
event flow — invite happy path".

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

Each client has its own camera following its own vessel (the scene camera driven by `MainMenuCameraController` in menu state, CM PlayerCam in freestyle). No network syncing of freestyle state is needed — each client independently toggles their own vessel via `MenuFreestyleEventsContainerSO` SOAP events.

**What works in multiplayer menu:**
- Both players spawn with network-owned vessels
- Both vessels visible and active on all clients' screens
- Each player independently toggles autopilot ↔ freestyle control
- Independent cameras per client — no conflicts
- Network ownership prevents cross-control of vessels

**Limitations:**
- Party size bounded by `HostConnectionDataSO.MaxPartySlots`
- No AI backfill in menu — `MenuServerPlayerVesselInitializer` does not pre-spawn AI opponents (unlike `ServerPlayerVesselInitializerWithAI` in game scenes)
- Freestyle state is local-only — other players cannot see whether you are in autopilot or freestyle mode (vessel behavior replicates, but the mode label does not)

#### UI Components

Party/social UI lives in `_Scripts/UI/Elements/`
(`PartyInviteNotificationPanel` is in `_Scripts/UI/Screens/`):
`ArcadeLobbyList` (4-slot party panel; host-only per-slot kick ✕) + `FriendInfoSlot`
(one slot), `FriendsListPanel` (combined Online + Requests, no tabs),
`OnlineInfoEntry` (online row: an Invite button when invitable + a ✕ that cancels a
pending outgoing invite or — host only — kicks an in-party member; "IN YOUR PARTY N/M"
for party members; Invite/cancel/kick share an anti-spam cooldown),
`RequestInfoEntry` (Accept/Decline — friend-request + party-invite),
and `PartyInviteNotificationPanel` (the
bottom-left **global invite popup** in Menu_Main — avatar + name + Accept/Decline,
3s auto-hide, latest-wins).

**The party panel's SEATING is synced, not local** — `PartyRoster` puts the host in the
first slot and the clients behind it in join order, the same on every device, by ordering on
Netcode's replicated `OwnerClientId` rather than on `HostConnectionDataSO.PartyMembers`,
which seeds whichever machine is reading it at index 0 and therefore cannot be drawn in
order. The local player is consequently **not** pinned to slot 0, and each occupied slot
wears its pilot's LIVE domain as a generated animated halo (`PartySlotDomainGlow`, ensured
by `FriendInfoSlot` itself so no scene wiring can omit it).
Full inventory + behaviour: **`Docs/PartySystem/UI.md`**.

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

> **Stale reference:** this section used to point at a `Create Party Prefabs` editor tool. No such
> `[MenuItem]` exists anywhere in the project — create the party prefabs by hand, or write the tool
> under `FrogletTools/Interface/` (see `Docs/TOOLING.md`) if it is worth automating. SO data
> container references (`HostConnectionDataSO`, `FriendsDataSO`, `SO_ProfileIconList`) must be wired
> manually in the inspector either way.

#### Scene Setup Checklist (Menu_Main)

Persistent services (`HostConnectionService` + `PartyInviteController` +
`FriendsInitializer`) live on one Bootstrap `DontDestroyOnLoad` GameObject;
`AppManager` holds `HostConnectionData.asset`. The full Menu_Main UI wiring
checklist (panels, row prefabs, SO references) is in
**`Docs/PartySystem/UI.md`** § "Scene wiring checklist".

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

#### Party / Presence / NetDiag docs — start at `Docs/README.md`

Full engineering docs for these subsystems live under `Docs/` (the index +
shared conventions are in `Docs/README.md`). Route by task:

| If your task… | Read first |
|---|---|
| Touches `HostConnectionService` / `PartySessionService` / `NetworkTransitionService` / `PartyInviteController` | `Docs/PartySystem/ARCHITECTURE.md` (+ `BUGS.md`) |
| Touches `PresenceLobbyService` / `LobbyPropertyWriter` / `LobbyRefreshScheduler` / `InviteService` / `AcceptanceSignalService` | `Docs/PresenceSystem/ARCHITECTURE.md` |
| Classifies / logs a party·lobby·session·transition catch failure | `Docs/NetworkDiagnostics/ARCHITECTURE.md` |
| Run the MPPM regression before a commit | `Docs/PartySystem/TESTS.md` (S-series) + `Docs/PresenceSystem/TESTS.md` (P-series) |
| Validate the NetDiag overlay itself | `Docs/NetworkDiagnostics/TESTS.md` (Tests A–E) |
| Log / triage a bug | `Docs/PartySystem/BUGS.md` (B2/B3/B5/B7) · `Docs/PresenceSystem/BUGS.md` (B1/B4/B6) |
| Pick up refactor work | `Docs/PartySystem/REFACTOR.md` · `Docs/PresenceSystem/REFACTOR.md` |
| Read what was already tried (session history) | `Docs/PartySystem/MPPM_SESSION_LOG.md` |

**Locked design (do not relitigate):** EAGER per-user Relay — every player
hosts their own Relay-backed party session on entering `Menu_Main`. **Do not
reintroduce LAZY / on-first-invite creation** (the shutdown-and-recreate
cascade it caused is the root of every recurring party-invite bug). Full rule
+ rationale: `Docs/README.md` § "Locked design" and
`Docs/PartySystem/ARCHITECTURE.md` § "Locked design" / "Unbreakable exit
criteria".

**Threading prerequisite (shipped):** the UGS / Netcode `Task` continuation → SOAP
off-thread → `EnsureRunningOnMainThread` cascade is resolved by `MainThreadDispatcher`
+ `.AsMainThread()` at every UGS / Netcode `await`. See `Docs/THREADING.md`.
**Do not** introduce `UniTask.SwitchToMainThread()` or
`UniTask.Yield(PlayerLoopTiming.Update)` as a thread-marshaling fix — both have
been tried and proven unreliable on this UniTask version.

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
      ├─ OnFriendAdded         ──► FriendsListPanel refreshes friend list
      ├─ OnFriendRemoved       ──► FriendsListPanel refreshes friend list
      ├─ OnFriendRequestReceived ──► FriendsListPanel spawns the new request row
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

Friends see presence updates via UGS SDK's `PresenceUpdated` event → `FriendsServiceFacade.OnPresenceUpdated()` → `SyncAllRelationships()` → `FriendData.Availability` updated in SOAP lists → `OnlineInfoEntry` rows update their online status indicator color.

#### Friend UI Components

The friends UI shares the party UI family (`FriendsListPanel` combined Online +
Requests, `RequestInfoEntry`) — inventory +
behaviour in **`Docs/PartySystem/UI.md`**. File locations are in the Key Files
table below.

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
| Combined friends/online panel UI | `FriendsListPanel.cs` | `_Scripts/UI/Elements/` |
| Online row UI (invite / cancel / kick) | `OnlineInfoEntry.cs` | `_Scripts/UI/Elements/` |
| Request row UI (friend request + party invite) | `RequestInfoEntry.cs` | `_Scripts/UI/Elements/` |
| SO asset instance | `FriendsData.asset` | `_SO_Assets/Friends Data/` |

#### Friend Requests (no UI entry point today)

The by-name `AddFriendPanel` and the confirmed-friend row `FriendInfoEntry` were
retired, so there is currently **no UI control to send a friend request** —
`FriendsListPanel` renders only the Online + Requests sections. The single-writer
facade methods remain for re-introducing one: `FriendsServiceFacade.SendFriendRequestByNameAsync(name)`
(by name) and `.SendFriendRequestAsync(playerId)` (by ID). Incoming requests still
arrive as `RequestInfoEntry` rows (Accept/Decline). Friend-request (persistent UGS
relationship) and party-invite (ephemeral session property) stay separate systems.
Detail: **`Docs/PartySystem/UI.md`** § "Friend requests vs. party invites".

#### Friend System Patterns to Follow

- **Single writer**: Only `FriendsServiceFacade` writes to `FriendsDataSO`. UI components read via SOAP lists and events — they never call UGS SDK directly.
- **Sync after mutate**: Every facade method that changes relationship state calls `SyncAllRelationships()` after the SDK call to keep SOAP lists in sync.
- **Event-driven UI**: `FriendsListPanel` and entry views subscribe to SOAP list events (`OnItemAdded`, `OnItemRemoved`, `OnCleared`) for reactive updates. No polling.
- **Presence via FriendsInitializer**: Scene transition presence is managed by `FriendsInitializer` — do not set presence from other MonoBehaviours.
- **DI access**: UI components access `FriendsServiceFacade` via `[Inject]`, not by finding it in the scene.
- **Bridge between Party and Friends**: the online row (`OnlineInfoEntry`) invite button calls `HostConnectionService.SendInviteAsync()` — the friend system feeds into the party system for social gameplay.

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
       │   humanCount = CurrentPartyHumanCount
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

**`CurrentPartyHumanCount` is answered ONE way, and on a guest that way is the HOST's answer.**
The host reads `SpectatorSession.CountHumanClients` (Netcode's connected clients less the
spectators, who have no Player object and must not eat an AI seat) and publishes it into the
replicated `ArcadeConfigSyncManager.LobbySnapshot.HumanCount`; a guest reads that back. It used to
derive its own from `HostConnectionDataSO.PartyMembers` — the presence-lobby list, polled every 3s
and very often 1 on a guest — and since the roster draws `seats - humans = AI`, a guest that
believed it was alone in a four-seat match drew **three AI avatars nobody placed and nobody
spawns** (the real backfill is `RequestedAIBackfillCount`, computed host-side). The presence list
survives only as the pre-lobby fallback, where nothing authoritative exists yet. General rule:
**when two peers must agree on a number, one of them owns it and the other reads it** — a second
derivation is a second answer, and the disagreement surfaces as UI nobody can trace back to a
count. Record: `Docs/ArcadeLaunch/ARCHITECTURE.md §3.1.2`, `Docs/PartySystem/BUGS.md` B23.

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

### SkimRace Game Mode

SkimRace is a competitive crystal-collection racing mode (1-4 players) using a **single unified scene** (`MinigameSkimRace.unity`). There is no separate singleplayer scene — all games run through Netcode regardless of player count. Solo play uses AI backfill via `ServerPlayerVesselInitializerWithAI`. See `Assets/_Scripts/Controller/Arcade/SKIMRACE.md` for the full technical reference.

#### Architecture

```
MiniGameControllerBase (MonoBehaviour + NetworkBehaviour)
  └── MultiplayerMiniGameControllerBase
      └── MultiplayerDomainGamesController
          └── SkimRaceController
```

**SO config**: `SO_ArcadeGame` asset — `Mode=SkimRace(33)`, `IsMultiplayer=true`, `MinPlayers=1`, `MaxPlayers=4`, `GolfScoring=true`

#### Execution Flow

```
ArcadeGameConfigureModal.OnStartGameClicked()
  ├─ SyncAllGameDataForLaunch():
  │   ├─ gameData.SceneName = "MinigameSkimRace"
  │   ├─ gameData.GameMode = GameModes.SkimRace
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

Server generates a random seed (after 1500ms delay for intensity sync) → writes to `_netTrackSeed` NetworkVariable → all clients spawn identical track via `SegmentSpawner.Initialize()`. Clients receive the seed through three redundant paths: immediate read at spawn, `OnValueChanged` callback, or poll fallback (100ms × 50 attempts). `SkimRaceController` sets `segmentSpawner.ExternalResetControl = true` to own the track lifecycle.

| Parameter | Formula | Base |
|---|---|---|
| Segments | `base * Intensity` | 10 |
| Straight Line Length | `base / Intensity` | 400 |
| Helix Radius | `Intensity / 1.3` | — |

#### Race Rules

- **Crystal target**: Resolved by `CrystalCollisionTurnMonitor.GetCrystalCollisionCount()`: `EndConditionOverridesSO` (FrogletTools > Game Modes > End Game Conditions; SkimRace entry non-zero) > the `SpawnableWaypointTrack`'s crystals per lap (`crystalsPerLap[intensity]` when authored, else its waypoint count) × laps > default 39. Intensity 4 is **Relativity** (five distinct lobes, five chords crossing the nucleus at different places, one snaking pass, 26 crystals/lap with marker blocks only at the crystals — `SKIMRACE.md` §5a, authored by `Tools/Build/author_skimrace_relativity_track.py`). Laps are per-intensity (`lapsPerIntensity`, a `List<int>` matched to the waypoint sets by index, falling back to the scalar `optionalLaps`) — SkimRace runs 3/3/2/2 so the long high-intensity tracks don't demand as many laps as the short ones. There is no per-scene `CrystalCollisions` field (removed on purpose — see the `/EndGameConditions` skill). Synced to all clients via `NetworkCrystalCollisionTurnMonitor._netCrystalCollisions` NetworkVariable → `gameData.CrystalTargetCount`
- **Turn monitor (domain-aggregated)**: `NetworkCrystalCollisionTurnMonitor` calls `gameData.ScoringRule.IsObjectiveReached(gameData, out _)` every frame (server only) — the turn ends when any active domain's summed CrystalsCollected (`ScoringMetrics.SumByDomain`) reaches the target, so AI and human teammates finish the race together
- **Winner detection (domain-aggregated)**: Server-authoritative via `SkimRaceController.OnTurnEndedCustom()` — finds the first active domain whose summed crystals reach the target (Jade → Ruby → Gold tie-break), sets `_raceEnded=true`, picks the best individual contributor on that domain as the representative `WinnerName`, calculates all scores, broadcasts via `SyncFinalScores_ClientRpc`
- **Scoring**: Every player on the winning domain gets `Score = finishTime` (seconds). Losing-domain players get `Score = 10000 + domainCrystalsRemaining` — the penalty reflects the team's deficit, so teammates on the same losing domain tie on Score. Golf rules (`UseGolfRules=true`): lower = better
- **Score sync**: `SyncFinalScores_ClientRpc()` broadcasts all player scores + winner name to all clients, then calls `InvokeWinnerCalculated()` + `InvokeMiniGameEnd()`
- **HasEndGame=false**: Prevents base controller from calling `SyncGameEnd_ClientRpc` (which would duplicate `InvokeMiniGameEnd`). `SetupNewRound()` is overridden to return when `_raceEnded=true`, suppressing the Ready button
- **Comeback**: `ElementalComebackSystem` reads `gameData.SumCrystalsCollectedByDomain` for the leader and the player's own domain — buffs are sized to the **team** deficit, so players on the leading domain don't get a buff even when they personally trail their teammates

#### End Game

- `SkimRaceEndGameController` reads `gameData.WinnerName` (set by server via `SyncFinalScores_ClientRpc`)
- Winner sees "VICTORY" + race time (formatted mm:ss:cs); losers see "DEFEAT" + crystals remaining
- `SkimRaceScoreboard` displays all players ranked by score (golf rules — sorts ascending)
- **Replay**: Full network scene reload (`UseSceneReloadForReplay=true`). `OnResetForReplayCustom()` was removed — all race state, track, and environment are destroyed with the scene and re-initialized fresh via `OnNetworkSpawn`. Fade to black → scene reload → fade from black on `OnClientReady`

#### Shared State & NetworkVariables

| Variable | Owner | Purpose |
|---|---|---|
| `SkimRaceController._netTrackSeed` | Server | Deterministic track seed (NetworkVariable) |
| `NetworkCrystalCollisionTurnMonitor._netCrystalCollisions` | Server | Crystal target synced to clients (NetworkVariable); writes to `gameData.CrystalTargetCount` |
| `gameData.WinnerName` | Server (via ClientRpc) | Authoritative winner identity; non-empty = results ready |
| `gameData.CrystalTargetCount` | Server (via `_netCrystalCollisions.OnValueChanged`) | Crystal target readable by any system |

#### Key Files — SkimRace

| Role | File | Location |
|---|---|---|
| Game controller | `SkimRaceController.cs` | `_Scripts/Controller/Arcade/` |
| Domain games base | `MultiplayerDomainGamesController.cs` | `_Scripts/Controller/Arcade/` |
| Score tracker | `SkimRaceScoreTracker.cs` | `_Scripts/Controller/Arcade/` |
| Crystal turn monitor | `NetworkCrystalCollisionTurnMonitor.cs` | `_Scripts/Controller/Arcade/TurnMonitors/` |
| Track spawner | `SegmentSpawner.cs` | `_Scripts/Controller/Environment/MiniGameObjects/` |
| End game controller | `SkimRaceEndGameController.cs` | `_Scripts/Utility/DataContainers/` |
| In-game HUD | `SkimRaceHUD.cs` | `_Scripts/UI/` |
| Scoreboard | `SkimRaceScoreboard.cs` | `_Scripts/UI/` |
| Elemental comeback | `ElementalComebackSystem.cs` | `_Scripts/Controller/Arcade/` |
| Stats provider | `SkimRaceStatsProvider.cs` | `_Scripts/Controller/Arcade/` |
| Player stats profile | `SkimRacePlayerStatsProfile.cs` | `_Scripts/UI/` |
| Full documentation | `SKIMRACE.md` | `_Scripts/Controller/Arcade/` |

#### SkimRace Patterns to Follow

- **Server authority via OnTurnEndedCustom**: Winner detection runs on the server in `OnTurnEndedCustom()`. `SkimRaceScoreTracker` only handles local elapsed-time tracking and UGS stats reporting — it does not participate in winner determination.
- **Deterministic track**: All clients spawn identical tracks from shared seed + intensity. `SegmentSpawner` uses `Random.InitState(seed)`. Three redundant sync paths (immediate, OnValueChanged, poll fallback) ensure reliability.
- **Golf scoring**: `UseGolfRules = true` — lower score = better rank. Winner time (seconds) always ranks above loser penalty (10000+).
- **Scene reload for replay**: Use `UseSceneReloadForReplay = true` — do not implement in-place reset. Flora/fauna/environment don't fully reset in-place.
- **Comeback system**: `ElementalComebackSystem` reads the deficit from the mode's own `ScoringRuleSO.DomainValue` (SkimRace: summed crystals, never Score, which tracks elapsed time equally for all), so comeback buffs scale with the **team** deficit in exactly the quantity the mode scores. There is no per-scene comeback source to author — that setting (`ScoreDifferenceSource`) was retired in 2026-09 after eight cloned scenes shipped reading their donor's stat.
- **Single scene**: Do not create separate singleplayer/multiplayer scenes. AI backfill handles solo play within the same Netcode pipeline.
- **Crystal target sync**: Server writes target to `NetworkCrystalCollisionTurnMonitor._netCrystalCollisions` NetworkVariable, which syncs to `gameData.CrystalTargetCount` on all clients.
- **Domain-aggregated scoring**: SkimRace, Joust, and Crystal Capture all end on a **per-domain** sum via the mode's `ScoringRuleSO.IsObjectiveReached` (over `ScoringMetrics.SumByDomain`). At most three scores ever exist (Jade / Ruby / Gold); teammates contribute to the same domain total. The in-game `MultiplayerHUD` draws those sums as **one centred row divided into a column per domain** — team score over that team's player icons over a 3px team-coloured accent, local domain first, no names (`Docs/GAME_MODE_TOPBAR.md` §2). The layout is chosen by the view's wiring and needs no branch in the HUD: with `domainBarContainer` set, `AllyDomainContainer` and `OpposingDomainsContainer` both resolve to that one transform, so the existing "local first, then opposing in enum order" build lays the columns out; with only the legacy `allyDomainContainer` / `opposingDomainsContainer` pair set, the old two-groups-flanking-a-player-card layout still works; with neither, it falls back to the per-player layout in `PlayerScoreContainer`.
