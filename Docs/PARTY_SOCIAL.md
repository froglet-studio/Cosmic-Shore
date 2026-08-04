# Party / Invite Lobby & Friend System - Reference

> Extracted verbatim from `CLAUDE.md` (2026-07-23) so the root file stays a lean
> rules-and-routing dictionary. This is the canonical home of this content now -
> update it here, and keep the corresponding CLAUDE.md digest in sync.

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
3s auto-hide, latest-wins). Full inventory + behaviour: **`Docs/PartySystem/UI.md`**.

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
