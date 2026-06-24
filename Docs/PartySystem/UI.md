# Party / Friends UI — Surface Reference

The current-state reference for the Menu_Main party + social UI: the component
inventory, the invite UX flow, the add-friend entry points, and the scene
wiring. This detail used to live in `CLAUDE.md`; it was moved here so the
always-loaded `CLAUDE.md` stays a high-signal map and the volatile UI detail
has one maintained home (it drifts — re-verify against code before trusting).

Service/SOAP-level mechanics live in `ARCHITECTURE.md`; this doc is the **UI
surface** only.

All components live in `Assets/_Scripts/UI/Elements/` unless noted
(`AddFriendPanel` is in `_Scripts/UI/Views/`, `PartyInviteNotificationPanel` in
`_Scripts/UI/Screens/`).

## Component inventory

There are two on-screen surfaces — the **party panel** (`ArcadeLobbyList`, the
4 slots) and the **combined social panel** (`FriendsListPanel`, Online +
Requests). They share the same component family:

| Component | Purpose |
|---|---|
| `ArcadeLobbyList` | The party panel: 4 slots (slot 0 = local player, slots 1-3 = remote `PartyMembers`), a Leave button, and a live "N Players Online" counter. An empty slot's "+" opens `FriendsListPanel`. |
| `FriendInfoSlot` | A single slot in `ArcadeLobbyList` — one of three states: local player, occupied (member avatar + name), or empty ("+" add button). |
| `FriendsListPanel` | Combined social panel — **no tabs; both sections render at once**: **Online** (every presence-lobby player) + **Requests** (incoming friend requests AND incoming party invites). Auto-opens when a party invite arrives. Reads `HostConnectionDataSO` + `FriendsDataSO` SOAP lists. |
| `OnlineInfoEntry` | A row in the Online section. The **whole row background is the invite button**; tints yellow + pulses while an invite is pending. Status label: ONLINE / IN LOBBY N/M / LOBBY FULL / IN A MATCH. |
| `RequestInfoEntry` | A row in the Requests section with Accept/Decline. `Kind { FriendRequest, PartyInvite }` — one row type serves both (delegates to `FriendsServiceFacade` / `PartyInviteController`). Lives on `RequestsInfo.prefab`, the shared base for the request/online/invite row family (`OnlineFriendsInfo Variant` is a prefab variant of it). |
| `AddFriendPanel` (`_Scripts/UI/Views/`) | Text input + [Send] to send a friend request by name → `FriendsServiceFacade.SendFriendRequestByNameAsync`. The only friend-request entry point in code. |
| `PartyInviteNotificationPanel` (`_Scripts/UI/Screens/`) | The **global invite popup** — a small bottom-left card (avatar + inviter name + Accept/Decline) shown anywhere in Menu_Main when an invite arrives. Subscribes to `OnInviteReceived`, routes to `PartyInviteController`, dismisses on `OnInviteResolved`. **3s auto-hide** (hides only — the invite stays in the `FriendsListPanel` Requests list); **latest-wins** (a newer invite replaces it). Lives as a **scene object in Menu_Main** (bottom-left, top-level canvas), with the component's fields wired in-scene — the standalone `PartyInviteNotificationPanel.prefab` was retired in favour of the shared `RequestsInfo` row layout. |

## Invite UX flow (UI-level)

The service/SOAP-level happy path is in `ARCHITECTURE.md` § "SOAP event flow —
invite happy path"; this is the UI-level view.

```
Sender opens the friends/online list and clicks a player row (whole row = invite button)
  ├─ ArcadeLobbyList empty-slot "+" → FriendsListPanel.Show() (Online + Requests sections)
  ├─ OnlineInfoEntry row click → FriendsListPanel.OnInviteClicked(playerId)
  └─ HostConnectionService.SendInviteAsync(targetPlayerId)
      ├─ EnsurePartySessionAsync() — idempotent; the Relay party session already
      │   exists under the eager "Always-InParty" model (created on menu entry),
      │   so this fast-paths rather than creating one on first invite
      ├─ Writes invite_payloads on the sender's OWN presence-lobby player property
      │   (one line per target: targetId|hostId|sessionId|hostName|avatarId)
      └─ OnInviteSent SOAP event; the row shows "PENDING REQUEST" + pulse

Recipient's refresh loop detects invite
  ├─ HostConnectionService.RefreshAsync() [base 1.5s; 0.75s while boosted]
  │   └─ Scans every OTHER lobby player's invite_payloads for a line whose targetId == local ID
  ├─ OnInviteReceived SOAP event raised
  ├─ FriendsListPanel auto-opens and spawns a RequestInfoEntry (Kind.PartyInvite) row
  └─ User presses Accept
      └─ PartyInviteController.AcceptInviteAsync(invite)
          ├─ CleanUpCurrentSession()
          ├─ ShutdownNetworkManagerAsync() — shutdown local host
          ├─ HostConnectionService.AcceptInviteAsync() — leave own session, join inviter's via Relay
          ├─ WaitForClientConnectionAsync() — poll nm.IsConnectedClient
          ├─ WaitForSceneLoadAsync() — wait for Menu_Main scene sync
          ├─ OnPartyJoinCompleted SOAP event
          └─ Host's MenuServerPlayerVesselInitializer spawns vessel + autopilot
```

## Add-friend entry points

A player sends a friend request **by display name** via `AddFriendPanel`, which
calls `FriendsServiceFacade.SendFriendRequestByNameAsync(name)` — the single
writer. This is the **only friend-request entry point in code today**: the
former per-row "+" add-friend button on online rows was removed, so online rows
are now invite-only (the whole row is the party-invite button).

| Entry Point | Input | Facade Method |
|---|---|---|
| `AddFriendPanel` | Player name (text input) | `SendFriendRequestByNameAsync(name)` |

**`AddFriendPanel` behavior:**
- Send button disabled until input is non-empty (`OnInputChanged` validates)
- Button disabled during async request (re-enabled in `finally`)
- Feedback text color: green for success, red for errors
- Input field cleared on success, preserved on failure
- Catches `FriendsServiceException` specifically for SDK errors

> **Note:** `AddFriendPanel` has no in-code opener (scene-wired, or currently
> unsurfaced), and `FriendsServiceFacade.SendFriendRequestAsync(playerId)` (by
> ID) still exists on the facade but has no UI caller. If an add-friend
> affordance returns to the online/friends rows, wire it to that by-ID method.

**Friend request vs. party invite — separate systems:**

| Action | System | Persistence | SDK |
|---|---|---|---|
| Add Friend | `FriendsServiceFacade` → UGS Friends SDK | Persistent relationship (survives sessions) | `FriendsService.AddFriendAsync` / `AddFriendByNameAsync` |
| Invite to Party | `HostConnectionService` → UGS Sessions SDK | Ephemeral (session-scoped, lobby player properties) | Presence-lobby player property: `invite_payloads` (one line per target) |

The party-invite affordance lives on the **online** row (`OnlineInfoEntry`):
an Invite button when the player can be invited, a ✕ to cancel a pending invite
or (host only) kick an in-party member. Friend *requests* are sent separately,
by name, via `AddFriendPanel`. (The separate confirmed-friend row,
`FriendInfoEntry`, was retired — `FriendsListPanel` renders only Online +
Requests sections.)

## Scene wiring checklist (Menu_Main)

1. **Persistent GameObjects** (Bootstrap scene, `DontDestroyOnLoad`):
   `HostConnectionService` + `PartyInviteController` + `FriendsInitializer` on
   one GameObject; wire `HostConnectionDataSO`, `AuthenticationDataVariable`.
2. **AppManager** (Bootstrap): assign `HostConnectionData.asset` to
   `hostConnectionData`.
3. **Menu_Main UI:**
   - `ArcadeLobbyList` (party panel) as child of the Arcade screen; empty slots' "+" opens `FriendsListPanel`.
   - `FriendsListPanel` (Online + Requests) as child of the party area (start inactive); auto-opens on an incoming invite.
   - Wire `HostConnectionData.asset` into `ArcadeLobbyList` and `FriendsListPanel`.
   - Wire `FriendsData.asset` into `FriendsListPanel`.
   - Wire the `OnlineInfoEntry` (`OnlineFriendsInfo Variant`) and `RequestInfoEntry` (`RequestsInfo`) row prefabs into `FriendsListPanel` (`onlineInfoPrefab` + `requestInfoPrefab` — the only two row prefabs it spawns).
   - Wire `SO_ProfileIconList` into `ArcadeLobbyList` / `FriendsListPanel` for avatars.

Prefabs live in `_Prefabs/UI Elements/Panels/Party/`. SO assets
(`HostConnectionData.asset` + the event/list assets) live in
`_SO_Assets/Host Connection Data/`; `FriendsData.asset` in
`_SO_Assets/Friends Data/`.

## Cross-references

- `ARCHITECTURE.md` — services, locked design, SOAP event flow (service-level), error-handling matrix.
- `INVITE_ENHANCEMENTS.md` — planned UI work (in-party invite guard, live-status refresh, the SOAP confirm-popup).
- `../PresenceSystem/ARCHITECTURE.md` — presence lobby, `invite_payloads` / `joined_party` property semantics.
