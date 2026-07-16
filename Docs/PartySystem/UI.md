# Party / Friends UI — Surface Reference

The current-state reference for the Menu_Main party + social UI: the component
inventory, the invite UX flow, the add-friend entry points, and the scene
wiring. This detail used to live in `CLAUDE.md`; it was moved here so the
always-loaded `CLAUDE.md` stays a high-signal map and the volatile UI detail
has one maintained home (it drifts — re-verify against code before trusting).

Service/SOAP-level mechanics live in `ARCHITECTURE.md`; this doc is the **UI
surface** only.

All components live in `Assets/_Scripts/UI/Elements/` unless noted
(`PartyInviteNotificationPanel` is in `_Scripts/UI/Screens/`).

## Component inventory

There are two on-screen surfaces — the **party panel** (`ArcadeLobbyList`, the
4 slots) and the **combined social panel** (`FriendsListPanel`, Online +
Requests). They share the same component family:

| Component | Purpose |
|---|---|
| `ArcadeLobbyList` | The party panel: 4 slots (slot 0 = local player, slots 1-3 = remote `PartyMembers`), a Leave button, and a live "N Players Online" counter. An empty slot's "+" opens `FriendsListPanel`. |
| `FriendInfoSlot` | A single slot in `ArcadeLobbyList` — one of three states: local player, occupied (member avatar + name; plus a **host-only kick ✕** on remote-member slots), or empty ("+" add button). On `FriendsInfo.prefab`. |
| `FriendsListPanel` | Combined social panel — **no tabs; both sections render at once**: **Online** (every presence-lobby player) + **Requests** (incoming friend requests AND incoming party invites). Auto-opens when a party invite arrives. Reads `HostConnectionDataSO` + `FriendsDataSO` SOAP lists. |
| `OnlineInfoEntry` | A row in the Online section with a small **Invite** button (shown only when the player is invitable) and a **✕** that cancels a pending outgoing invite or (host only) kicks an in-party member. Tints yellow + pulses while an invite is pending; Invite/cancel/kick share an anti-spam cooldown. Status label: ONLINE / IN PARTY N/M / PARTY FULL / IN A MATCH / IN YOUR PARTY N/M. On `OnlineFriendsInfo Variant.prefab` (a variant of `RequestsInfo`). |
| `RequestInfoEntry` | A row in the Requests section with Accept/Decline. `Kind { FriendRequest, PartyInvite }` — one row type serves both (delegates to `FriendsServiceFacade` / `PartyInviteController`). Lives on `RequestsInfo.prefab`, the shared base for the row family (`OnlineFriendsInfo Variant` and `PartyInviteNotificationPanel Variant` are prefab variants of it). |
| `PartyInviteNotificationPanel` (`_Scripts/UI/Screens/`) | The **global invite popup** — a small bottom-left card (avatar + inviter name + Accept/Decline) shown anywhere in Menu_Main when an invite arrives. Subscribes to `OnInviteReceived`, routes to `PartyInviteController`, dismisses on `OnInviteResolved`. **3s auto-hide** (hides only — the invite stays in the `FriendsListPanel` Requests list); **latest-wins** (a newer invite replaces it). Lives as **`PartyInviteNotificationPanel Variant.prefab`** — a **prefab variant of `RequestsInfo`** (the request-row layout reused: inherited `RequestInfoEntry` removed, a `CanvasGroup` + this component added and wired to the row's avatar/name/accept/decline). Instanced bottom-left on a top-level canvas in Menu_Main. |

**Live identity (names/avatars) in these panels.** Rows and slots render
from the SOAP lists and repaint on the lists' item events, so a player's
mid-session rename propagates without any UI code: online rows via
`RefreshOnlinePlayersDiff`'s change-detect (RemoveAt+Insert), party slots
via `PartyMemberService.SyncFromSession`'s identity refresh (same pattern —
and deliberately WITHOUT raising the member-joined/left SOAP events), and
the local player's own slot via `HostConnectionService.RefreshLocalPartyMemberEntry`.
End-to-end pipeline + latency:
`../PresenceSystem/ARCHITECTURE.md` § "Identity propagation"; manual test:
`../PresenceSystem/TESTS.md` **P7**.

## Invite UX flow (UI-level)

The service/SOAP-level happy path is in `ARCHITECTURE.md` § "SOAP event flow —
invite happy path"; this is the UI-level view.

```
Sender opens the friends/online list and clicks a player row (whole row = invite button)
  ├─ ArcadeLobbyList empty-slot "+" → FriendsListPanel.Show() (Online + Requests sections)
  ├─ OnlineInfoEntry row click → FriendsListPanel.OnInviteClicked(playerId)
  └─ HostConnectionService.SendInviteAsync(targetPlayerId)
      ├─ Refuses when the local party is full (no open slots) — throws so the
      │   optimistic "PENDING REQUEST" row resets; rows also render
      │   non-invitable while the LOCAL party is full
      ├─ EnsurePartySessionAsync() — only when ActiveSession is null AND the
      │   sender is not a guest in someone else's party (a guest with a null
      │   session is broken state → abort, never self-eject); under the eager
      │   "Always-InParty" model the session already exists, so this is a
      │   startup-race fallback only
      ├─ Writes invite_payloads on the sender's OWN presence-lobby player property
      │   (one line per target: targetId|senderId|sessionId|senderName|senderAvatarId).
      │   senderId is the SENDER, not necessarily the party host — a party MEMBER
      │   can invite too; sessionId is always the sender's CURRENT party session,
      │   so a member's invite lands the acceptor in the member's party
      │   (invite chain — INVITE_ENHANCEMENTS.md Task 4)
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

## Friend requests vs. party invites

Two separate systems — don't conflate them:

| Action | System | Persistence | SDK |
|---|---|---|---|
| Add Friend | `FriendsServiceFacade` → UGS Friends SDK | Persistent relationship (survives sessions) | `FriendsService.AddFriendAsync` / `AddFriendByNameAsync` |
| Invite to Party | `HostConnectionService` → UGS Sessions SDK | Ephemeral (session-scoped, lobby player properties) | Presence-lobby player property: `invite_payloads` (one line per target) |

**Party invites** are surfaced on the **online** row (`OnlineInfoEntry`): an Invite
button when the player can be invited, plus a ✕ to cancel a pending outgoing invite
or (host only) kick an in-party member. The party panel's `FriendInfoSlot` carries
the same host-only kick ✕ per occupied member slot.

**Friend requests have no UI entry point today.** The by-name `AddFriendPanel` and
the confirmed-friend row `FriendInfoEntry` were both retired — `FriendsListPanel`
now renders only the Online + Requests sections. The facade capability remains for
when an add-friend affordance is re-introduced: `FriendsServiceFacade.SendFriendRequestByNameAsync(name)`
(by name) and `.SendFriendRequestAsync(playerId)` (by ID) are both live single-writer
entry points — wire a new control to either. Incoming friend requests still arrive in
the Requests section as `RequestInfoEntry` rows (Accept/Decline).

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
