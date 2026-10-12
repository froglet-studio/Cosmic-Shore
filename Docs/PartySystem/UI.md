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
4 slots) and the **combined social panel** (`FriendsListPanel`, Online + Add
Friend + Recent + Requests). They share the same component family:

| Component | Purpose |
|---|---|
| `ArcadeLobbyList` | The party panel: 4 slots in the **synced seating** (host first, then clients in join order — see "Seating" below; NOT local-player-first), a Leave button, and a live "N Players Online" counter. An empty slot's "+" opens `FriendsListPanel`. |
| `FriendInfoSlot` | A single slot in `ArcadeLobbyList` — one of three states: local player, occupied (member avatar + name; plus a **host-only kick ✕** on remote-member slots), or empty ("+" add button). On `FriendsInfo.prefab`. Also **ensures its own `PartySlotDomainGlow`** at `Awake` (see "Seating"). |
| `PartyRoster` | Pure static. THE party seating order, and the only place it is decided. No Unity, UGS or Netcode types — the caller supplies the client-id lookup, which is what makes it edit-mode testable (`PartyRosterTests`). |
| `PartySlotDomainGlow` | The animated halo behind a slot's avatar, tinted with that pilot's live domain. Generated (GameObject, sprite and rect) rather than authored, so it needs no scene wiring and no party surface can ship without it. |
| `FriendsListPanel` | Combined social panel — **no tabs; all four sections render at once**: **Online** (every presence-lobby player) + **Add Friend** (a `TMP_InputField` for a pilot's exact display name, a Send button and a one-line status label; see "Add Friend section" below) + **Recent** (the other humans of your last online matches from `RecentPlayersStore`, newest first, an add-friend button per row; see "Recent section" below) + **Requests** (incoming friend requests AND incoming party invites; the party-invite rows are the **invite inbox**, re-read from `HostConnectionService`'s `IncomingInviteInbox` every time the panel opens, see "Invite inbox and badge" below). Auto-opens when a party invite arrives; opening it marks the inbox seen, which clears the party panel's `PartyInviteBadge`. Reads `HostConnectionDataSO` + `FriendsDataSO` SOAP lists; the only thing it WRITES goes through `FriendsServiceFacade` (and `MarkIncomingInvitesSeen`). There are four scene-placed copies in Menu_Main (Arcade, Arena, Mission and Toybox screen modals), all wired alike. |
| `OnlineInfoEntry` | A row in the Online section with a small **Invite** button (shown only when the player is invitable) and a **✕** that cancels a pending outgoing invite or (host only) kicks an in-party member. Tints yellow + pulses while an invite is pending; Invite/cancel/kick share an anti-spam cooldown. Status label: ONLINE / IN PARTY N/M / PARTY FULL / IN A MATCH / IN YOUR PARTY N/M. A third button, **Join** (`joinButton` + `joinButtonIcon`), is one control with two faces resolved by `FriendsListPanel.ResolveJoinMode`: a blue doorway icon that walks you into that player's party with no invite (drawn disabled when the party is full), or — while the player is IN A MATCH — an amber eye icon that is the row's ONLY enabled button and **spectates** the match (`SPECTATOR.md`). On `OnlineFriendsInfo Variant.prefab` (a variant of `RequestsInfo`). |
| `RequestInfoEntry` | A row in the Requests section with Accept/Decline. `Kind { FriendRequest, PartyInvite, RecentPlayer }` — one row type serves all three (delegates to `FriendsServiceFacade` / `PartyInviteController`); in its `RecentPlayer` shape (`PopulateRecentPlayer`) the Accept glyph is the add-friend button, Decline is hidden, the label is the resolved state and nothing expires. Lives on `RequestsInfo.prefab`, the shared base for the row family (`OnlineFriendsInfo Variant` and `PartyInviteNotificationPanel Variant` are prefab variants of it). |
| `PartyInviteNotificationPanel` (`_Scripts/UI/Screens/`) | The **global invite popup**: a small bottom-left card (avatar + inviter name + a detail line + Accept/Decline) shown anywhere in Menu_Main when an invite arrives. Subscribes to `OnInviteReceived`, routes to `PartyInviteController`, and on `OnInviteResolved` asks the service whether the invite it shows is still open: if not it moves on to the next open invite or hides. **Auto-hide after `autoHideSeconds` (3 s, serialized)** hides only; the detail line says so: "Also in your friends list for 58 s", prefixed "2 invites waiting." when more than one is open (`Notice.Compose`, edit-mode tested). **Newest wins on the card only**: an earlier invite stays in the inbox and is counted on the card. Decline answers the shown sender's invite alone. Lives as **`PartyInviteNotificationPanel Variant.prefab`**, a **prefab variant of `RequestsInfo`** (the request-row layout reused: inherited `RequestInfoEntry` removed, a `CanvasGroup` + this component added and wired to the row's avatar/name/accept/decline; `detailText` is the row's status label). Instanced bottom-left under `Screens/HomeScreen` in Menu_Main. |
| `PartyInviteBadge` | The **unread-invite count**: a small amber disc (built-in `Knob` sprite) with a number, a child `InviteBadge` of every `ArcadeLobbyList` just right of its PARTY header. Reads `HostConnectionService.UnseenIncomingInviteCount`; faded to 0 by its own `CanvasGroup` while nothing is unread, never a raycast target. Repaints on `OnInviteReceived` / `OnInviteResolved` and on a 0.5 s tick (for the panel marking the inbox seen and for expiry, which raise nothing). Self-driven: `ArcadeLobbyList` does not reference it. Scene-authored with `cs-asset`, not a prefab. |

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


## Seating, and why it is not the `PartyMembers` list order

`HostConnectionDataSO.PartyMembers` is a **per-device** list and cannot be drawn in
order. `PartyMemberService.SeedLocalPlayer` puts the local player at index 0 on *every*
machine and `SyncFromSession` appends the rest in whatever order `ISession.Players`
enumerates — so the host saw itself first, each client saw itself first, and no two peers
agreed on the rest. Four players in one party read four different seatings.

`PartyRoster.Build` orders the party off something **already replicated**: Netcode's
`OwnerClientId`. The server assigns it in CONNECTION order and the host is always
`NetworkManager.ServerClientId`, so ascending order *is* "host first, then clients in join
order", identically on every peer — with nothing new on the wire and no new UGS property
for peers to converge on. The bridge from a UGS party member to their `Player` object is
`Player.NetUgsPlayerId`.

Three properties of that choice are worth keeping:

- **It degrades in-order.** A member whose `Player` has not network-spawned yet has no
  client id, so they sort LAST behind everyone who has one, tie-breaking on the ordinal
  player id — still the same answer on every device. `ArcadeLobbyList` re-seats itself when
  the id resolves (it fingerprints the `(ugs id, client id)` set rather than subscribing
  every step of the spawn chain).
- **The local player is not pinned to slot 0.** Anything that assumed it was is wrong:
  `HandleProfileChanged` used to repaint `slots[0]` on a resolved cloud profile, which is
  the host's seat on a client.
- **The seat count and the capacity differ on purpose.** The panel draws 4
  (`PartyDisplaySlots`) while `MaxPartySlots` carries one spare seat of anti-flicker
  headroom, so a transient fifth member can exist. If that would push the LOCAL player off
  the end they are moved into the last drawn slot instead — a panel that stops showing you
  your own party is a worse lie than a momentarily imperfect order.

**The domain glow rides the same lookup.** `PartySlotDomainGlow` paints each occupied slot
with `SO_ColorSet.GetDomainSignalColor` for that pilot's **live** `Player.Domain`, pushed on
a slow tick rather than snapshotted at population time — a pilot re-picks their domain from
the same modal the panel lives in, and the platform rule is to read the live mirror each
time. A pilot whose domain cannot be resolved gets **no** halo rather than a default-coloured
one: an unresolved pilot painted Jade is confident misinformation, where an absent halo reads
as "not known yet".

The halo is deliberately the same visual as the load screen's pilot chips
(`ConnectingPlayerRoster`) and the scoreboard's rematch faces (`RematchVoteRoster`) — same
breath rate, alpha range and size, adopted from them rather than re-invented. There are
consequently **three** implementations of one halo; folding them into one component is
TODO-12.

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
  ├─ IncomingInviteInbox.Observe (per-sender dedup; only a NEW invite is raised)
  ├─ OnInviteReceived SOAP event raised
  ├─ PartyInviteNotificationPanel shows the card; PartyInviteBadge counts it while unseen
  ├─ FriendsListPanel auto-opens (when its screen is up) and spawns a RequestInfoEntry
  │   (Kind.PartyInvite) row; on open it re-reads EVERY open invite from the inbox
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

### When the join fails: the notice reaches the menu it lands on (2026-10-10, corrected 2026-10-12)

Every failure arm of the accept flow ends in
`PartyInviteController.BounceToSoloMenuAsync`, which restarts the client's own solo host
(`RecoverFromFailedTransitionAsync`) and then shows "Couldn't join - returned to your menu."
(host loss uses the same bounce with "Connection to the party was lost - returned to your
menu.").

The first version of this raised the line on a `bounceToastChannel` field and held it on the
channel asset until a `ToastService` subscribed. It never reached anyone: the field was not
assigned on `PartyServices.prefab` (the only `PartyInviteController` in the project) or in
Bootstrap, and the only `ToastService` in the project lives inside `ToastHolder.prefab`, which is
nested in the Sparrow and Scarab vessel prefabs, not in Menu_Main. The field and the hold are
gone.

The notice now goes through `ToastNotificationAPI.Show`, the toast Menu_Main actually has (the
friends panel's feedback uses it): the API creates its persistent manager on demand and spawns
toasts under the menu's `ToastNotificationContainer` (`UI_Refactored/Safe Area`). The controller is
`DontDestroyOnLoad`, so the method carries the line across the reload itself. It waits, in real
time, for the scene-transition veil to lift (`SceneTransitionManager.IsVeiled`; at most
`bounceNoticeVeilWaitSeconds`, 15 s), because the veil sorts above every canvas and the menu
only lifts it once the local vessel is ready, plus a 1.5 s settle hold after a match. The wait is
not awaited by the bounce, so `_transitioning` is released with the recovery as before.
`BounceNoticeDeliveryTests` pins the three things the route depends on: no inspector-assigned
channel on the controller, the API's Resources assets, and the container in Menu_Main. Not yet
seen in an editor: force a join failure (accept an invite whose host has quit) and watch for the
line on the fresh menu once it is visible.

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

**Direct join and spectate** need no invite at all: the online row's Join button reads
the `partySession` presence property the other player publishes and joins that session
outright (`PartyInviteController.JoinPartyAsync`), or, when they are in a match, joins it
as a spectator with no Player object (`SpectateAsync`). Record: `SPECTATOR.md`.

### Add Friend section (the send half, 2026-10-10)

**Friend requests are sent from the `FriendsListPanel`'s Add Friend section**, the third
section between Online and Recent. It is by exact display name, which is what the retired
`AddFriendPanel` did and what the facade's by-name method supports; the RECENT section below
is the other source (a pilot you just played with, sent by id through
`.SendFriendRequestAsync(playerId)`), and the two share the gate, the in-flight lock and the
status line.

- **Objects** (children of each `FriendListPanel`, scene-authored, not a prefab):
  `AddFriendHeader` (the ONLINE / REQUESTS header bar and text treatment, reading ADD FRIEND),
  `AddFriendInput` (`Image` + `TMP_InputField` with Unity's `Text Area` / `Placeholder` / `Text`
  shape, 32-character limit, the ProfileModal callsign field's background), `AddFriendSendButton`
  (`Image` + `Button`: the Requests row's Accept glyph `icon_AddFriend` in the same green) and
  `AddFriendStatus` (a `TextMeshProUGUI` line, Online-row status size, ellipsis overflow).
- **Wiring**: `FriendsListPanel.addFriendNameInput` / `addFriendSendButton` / `addFriendStatusText`.
  The trio is optional as a whole, never in part: all three unwired means no send surface (the
  panel still works), a partial trio is reported by `ValidateSceneWiring` on enable.
- **Flow**: Send press or Enter in the field -> `TryBeginSendAction` (the same 0.4 s unscaled
  anti-spam gate `OnlineInfoEntry` puts on Invite / Cancel / Kick, `sendRequestCooldownSeconds`)
  -> empty name is refused on the status line -> facade not initialized (offline, not signed in)
  is refused on the status line ("Friends service isn't ready...") -> otherwise ONE request at a
  time: the button is disabled while `FriendsServiceFacade.SendFriendRequestByNameAsync(name)`
  is in flight (the facade's own UGS await carries `.AsMainThread()` and calls
  `SyncAllRelationships()` afterwards), then "Friend request sent to NAME." and the field clears,
  or "Couldn't send to NAME: <reason>" with a `CSDebug.LogWarning`. Traces go to the `Party`
  log channel. When no status label is wired the line goes to `ToastNotificationAPI`, the
  panel's existing Accept/Decline feedback path. The UI never touches the UGS SDK.
- **Layout**: the panel has no layout group; the section is anchored into the gap made by
  shrinking the Online scroll view (now 280 tall, top edge at the value the Arena panel
  already used, clear of the ONLINE header). Incoming friend requests still arrive in the
  Requests section as `RequestInfoEntry` rows (Accept/Decline).
- **Seen in Prisma** (xvfb, `COSMIC_SHORE_NET=off`): the section renders between ONLINE and
  REQUESTS, an empty Send only writes the nudge line, and a name reaches the facade (the Party
  channel logs `[UGS Friends] Sending friend request to 'NAME'...`) and comes back on the status
  line as "Couldn't send to NAME: No player named 'NAME' could be found." (Prisma's in-engine
  Friends stand-in answers that offline, so the not-ready refusal was not exercised there).
  **Not yet opened in the Unity Editor**: the scene objects were authored with `cs-asset`, so the
  first editor pass should open Menu_Main, confirm the four panels show the section, and send a
  real request between two signed-in accounts.

### Recent section (recently played with, 2026-10-10)

**The RECENT section is the path from a good match with a stranger to a friend request.** It
lists the other humans of the local player's completed multiplayer matches, newest first, with an
add-friend button per row. Nothing recorded this before, so the social graph could not grow from
play at all.

- **Store**: `RecentPlayersStore` (`_Scripts/System/`, static, like the preference stores). One
  `RecentPlayerRecord` per pilot (UGS player id, display name, avatar id, last-played UTC ticks),
  de-duplicated by id (a pilot met again moves to the front with fresh facts) and capped. Local
  JSON through `DataAccessor` under `persistentDataPath`: a memory of who you met, not a
  relationship, so it is not Cloud Save data. **Config**: `RecentPlayersConfigSO` at
  `Resources/RecentPlayersConfig.asset` holds the cap (20) and the base file name; a missing asset
  falls back to the same defaults.
- **Per account** (2026-10-12): the file is `recent_players_<signed-in UGS player id>.data`
  (`SaveFileNameFor`), reloaded whenever the signed-in id changes, and the owner's own id is dropped
  from whatever loads (`DropUnusable`). One shared `recent_players.data` was per INSTALL: an account
  switch, two instances on one PC and MPPM's virtual players share `persistentDataPath`, so one
  account saw another's pilots, and with two accounts saving in turn the last save won and an
  account could load a list holding itself, with a friend-request button on its own row. Signed
  out, the list is empty and a match is not recorded. The pre-account file is never read (its
  pilots cannot be attributed to an account). The panel also skips the local id when it draws the
  rows, as the Online section does.
- **Recording**: `GameDataSO.InvokeMiniGameEnd` calls `RecentPlayersStore.RecordMatchEnd(Players)`
  next to `FlightClock.EndGame()`, inside a try/catch: the record is a disk write, and a failure
  there (disk full, a locked or read-only file) logs a warning and costs the list one match, never
  the `OnMiniGameEnd` raise that brings up that peer's end screen. Every peer raises that (the modes sync results through a
  ClientRpc first), so every machine remembers the match. Only seats with a UGS id are kept; AI
  seats (flagged, no id) and the local pilot (by flag AND by id, so a stale second object for the
  same account cannot record "you played with yourself") are dropped, and a solo or AI-only game
  records nobody. The pure list shaping (`SelectOthers`, `Merge`, `FormatLastPlayed`) is covered
  by `RecentPlayersStoreTests` (cap, de-dup, newest first, local and AI excluded).
- **Objects** (children of each `FriendListPanel`, scene-authored with `cs-asset`): `RecentHeader`
  (the header bar treatment, reading RECENT), `Recent Scroll View` (the Requests scroll view's
  shape: `Image` + `ScrollRect` > `Viewport` (`Image` + `Mask`) > `Content`
  (`VerticalLayoutGroup`)) and, over the viewport, `RecentEmptyText` (a `TextMeshProUGUI` line
  the panel shows while the list is empty). To make room the Online scroll view is now 210 tall
  (two rows) and anchored by fraction like the other two, the Add Friend trio moved up and the
  Requests header, refresh icon and scroll view moved down; the Recent and Requests lists each
  show one row and the top of the next and scroll.
- **Wiring**: `FriendsListPanel.recentContent` (the Content `RectTransform`) and
  `recentEmptyText`. Both optional: no `recentContent` means no RECENT section; an empty-state
  line without a content is reported by `ValidateSceneWiring`. Rows are `requestInfoPrefab`
  (`RequestsInfo`) in its `RecentPlayer` shape, so no new row prefab exists.
- **Row state** (read from `FriendsDataSO`, the UI never touches the SDK): already in `Friends`
  reads FRIENDS, already in `OutgoingRequests` reads REQUEST SENT, both without a button; in
  `IncomingRequests` reads SENT YOU A REQUEST with the button (the facade's add on an incoming
  request is the accept that makes it mutual); otherwise PLAYED 5 MIN AGO / 3 HR AGO / 2 DAYS AGO
  with the button. Any change to those three lists re-renders the rows next frame (coalesced,
  since the facade rebuilds each list as Clear() + Add() per sync).
- **Flow**: add press -> the row disables its own button -> `TryBeginSendAction` (the Add Friend
  gate, `sendRequestCooldownSeconds`) and the shared `_sendRequestInFlight` lock -> facade not
  ready is refused on the status line -> `FriendsServiceFacade.SendFriendRequestAsync(playerId)`
  with the Add Friend send button disabled meanwhile -> "Friend request sent to NAME." or
  "Couldn't send to NAME: <reason>" on the same status line (toast when it is unwired) -> the
  rows are rebuilt, so the row now reads REQUEST SENT (or gets its button back after a refusal or
  a failure). Traces on the `Party` channel.
- **Seen in Prisma** (xvfb, `COSMIC_SHORE_NET=off`, the store file seeded with two pilots): the
  section renders between ADD FRIEND and REQUESTS with both rows (name, avatar, "PLAYED 1 HR AGO"
  and the green glyph); an add press reports "Friend request sent to Kestrel." on the status line
  and the row rebuilds as REQUEST SENT (the in-engine Friends stand-in records the outgoing
  request). Two Prisma engine quirks needed harness nudges and are not scene defects: the first
  layout pass after `Instantiate` under the `VerticalLayoutGroup` placed the rows 227 units low
  (any relayout puts them at the authored positions, and the authored Content block is identical
  to the Requests one), and the nested `AcceptRequest` / `AvatarIcon` composed with a non-zero
  `anchoredPosition` (the prefab authors 0,0). **Not yet opened in the Unity Editor**: first
  pass is a real online match on two accounts, then the panel on both.

### Invite inbox and badge (an invite you miss is not gone, 2026-10-10)

**Every open party invite lives in `HostConnectionService`'s `IncomingInviteInbox`**
(`_Scripts/Controller/Party/Services/`, pure C#, one record per SENDER, the service its single
writer), and the popup, the friends panel's Requests rows and the party panel's badge are three
views of that one list. Measured before the change: the service remembered ONE incoming invite
(`_lastFiredInvite` + a resolved flag), so with two friends inviting at once the refresh loop's
duplicate check compared each line against whichever it had seen LAST and the two took turns
being "new": `OnInviteReceived` re-fired for both every 0.75-1.5 s tick, the popup bounced between
the senders with its 3 s auto-hide timer reset each time, the invite sound replayed each tick, the
panel's rehydrate-on-open could only read the latest one, and a Decline (which resolved "the"
invite) dropped the other sender's as well. `IncomingInviteInboxTests` (26) pin every shape.

- **Inbox rules.** `Observe(line)` is per sender: the same sender's same session seen again is a
  duplicate (nothing raised); a resolved invite's line only refreshes the record, silently (the
  line of an invite we accepted or declined stays on the wire until the sender clears it; the eager
  model stamps the real session id at send, so the accept flow reads the id off the invite it was
  handed, never off this refresh); a line for the party we are already a client of is suppressed; the same sender with a NEW session id is a new invite (fresh clock, unread
  again). An invite is PENDING while unresolved and younger than `IncomingInviteLifetimeSeconds`,
  which is `HostConnectionService.outgoingInviteTimeoutSeconds` (serialized, default 60 s, on
  `PartyServices.prefab`): the SAME value the sender's outgoing invite expires on, so both sides
  clear together (a code constant until 2026-10-12). Accept, direct join, spectate
  and leave resolve ALL (the player is leaving for a different party); **Decline resolves ONE**
  (`DeclineInviteAsync(senderPlayerId)` on the service and the controller; the no-argument form
  still declines all). A resolved record is kept until the sender's line is gone, so the line
  still on the wire is not read as a new invite, and the sender can invite afresh afterwards.
  The per-tick `Sweep` drops a sender whose line has been gone for two ticks (a stale snapshot
  cannot flicker a live invite), ends one that outlived its lifetime, holds anything a join is in
  flight for, and raises `OnInviteResolved` once when an open invite ended that way.
- **Service surface** (read by the UI): `LastPendingInvite` (newest open), `PendingIncomingInviteCount`,
  `UnseenIncomingInviteCount`, `CopyPendingIncomingInvites(list)`, `IsIncomingInvitePending(id)`,
  `IncomingInviteSecondsRemaining(id)`, `IncomingInviteLifetimeSeconds`, `MarkIncomingInvitesSeen()`.
  `OnInviteResolved` stays a no-payload SOAP raise; every listener reconciles against
  `IsIncomingInvitePending` instead of clearing everything.
- **Requests rows (the inbox on screen).** On enable the panel rebuilds `_pendingPartyInvites`
  from `CopyPendingIncomingInvites` (it used to seed only `LastPendingInvite`), spawns one row per
  open invite, then `MarkIncomingInvitesSeen` **if the player can see it**: the four screen
  modals never deactivate (they hide by CanvasGroup alpha), so every copy of this panel is
  ENABLED whatever screen is up and receives every raise; "seen" is read off the owning
  `ModalWindowManager.IsOpen` (`IsShownToPlayer`), and the modal's `OnModalOpened` marks the inbox
  seen when a panel left open comes back into view. For the same reason `ArcadeLobbyList`'s
  invite auto-open now really is "while the arcade lobby is visible": a hidden lobby's copy no
  longer opens its friends panel (which counted the invite as seen before anyone saw it, and met
  the player with an open panel on their next visit); the badge announces it instead. A row's
  lifetime is exactly the inbox's remaining seconds (no expiry when the inbox never expires), so a
  panel opened 40 s after the invite shows the 20 s it has left rather than a fresh 60, and the row,
  the popup's countdown and the sender's timeout are one value; `partyInviteExpirationSeconds` is
  only the fallback for an invite the service has no record of (it was a cap until 2026-10-12, a
  second knob that could clip a row while its invite was still open). `HandleInviteResolved` removes only the rows whose sender
  the service no longer lists; the panel's Decline passes the sender id.
- **Badge.** `InviteBadge` (Image + CanvasGroup + `PartyInviteBadge` with a `Count` TMP child)
  under each of the four `ArcadeLobbyList`s, anchored by fraction at x 0.64-0.713, y 0.812-0.856
  of the party panel (a 40 x 40 disc right of the PARTY word). Shows `UnseenIncomingInviteCount`
  ("9+" past `maxShownCount`), hides at 0, clears the moment the friends panel opens. A player on
  the Home screen sees the popup; the badge waits on the Arcade, Arena, Mission and Toybox party
  panels, which is where the "+" that opens the inbox is.
- **Popup.** `autoHideSeconds` stays a serialized field. The card's detail line (`detailText`, the
  variant's `OnlineStatus` label, auto-size up to 18.24; the Menu_Main instance overrides the minimum to 1.2) reads "Also in your friends list for N s"
  from the inbox's remaining time, or "2 invites waiting. Also in your friends list for N s" when
  more than one is open, refreshed once per whole second while shown. A second invite takes the
  card without destroying the first's record; when the shown invite is answered (here or
  elsewhere), withdrawn or expired, the card moves on to the newest open one, or hides. Decline on
  the card answers that sender only and leaves the card on the declined invite until the resolve
  raise moves it on (it used to hide and clear the card first, which turned the raise into a no-op,
  so the card vanished with another invite still open; fixed 2026-10-12).
- **Seen in Prisma** (xvfb, 2026-10-12, the directory-backed session stand-in with pilots seeded
  into the presence lobby's roster from outside the process; `COSMIC_SHORE_NET=off` cannot do it,
  its `LocalMultiplayerService` roster is the local player alone): the card for the first invite,
  its auto-hide, the second arriving as "2 invites waiting", the badge at 2 on the Arcade party
  panel, both Requests rows with the badge cleared, a Decline on the card moving it on to the next
  open invite, and both lingering lines expiring once each at 60 s. See the verification checklist
  entry. The Requests rows needed the same relayout nudge as the RECENT section (Prisma quirk,
  above).
  **Not yet opened in the Unity Editor**: the badge objects and the variant's `detailText` were
  authored with `cs-asset`; the first editor pass should open Menu_Main, send two invites from two
  other accounts and watch the card, the badge and the rows.

## Scene wiring checklist (Menu_Main)

1. **Persistent GameObjects** (Bootstrap scene, `DontDestroyOnLoad`):
   `HostConnectionService` + `PartyInviteController` + `FriendsInitializer` on
   one GameObject; wire `HostConnectionDataSO`, `AuthenticationDataVariable`.
2. **AppManager** (Bootstrap): assign `HostConnectionData.asset` to
   `hostConnectionData`.
3. **Menu_Main UI:**
   - `ArcadeLobbyList` (party panel) as child of the Arcade screen; empty slots' "+" opens `FriendsListPanel`.
   - `FriendsListPanel` (Online + Add Friend + Recent + Requests) as child of the party area (start inactive); auto-opens on an incoming invite.
   - Wire `HostConnectionData.asset` into `ArcadeLobbyList` and `FriendsListPanel`.
   - Wire `FriendsData.asset` into `FriendsListPanel`.
   - Wire the `OnlineInfoEntry` (`OnlineFriendsInfo Variant`) and `RequestInfoEntry` (`RequestsInfo`) row prefabs into `FriendsListPanel` (`onlineInfoPrefab` + `requestInfoPrefab` — the only two row prefabs it spawns).
   - Wire the Add Friend trio into `FriendsListPanel`: `addFriendNameInput` (the `TMP_InputField` on `AddFriendInput`), `addFriendSendButton` (the `Button` on `AddFriendSendButton`), `addFriendStatusText` (the `TextMeshProUGUI` on `AddFriendStatus`). All three or none; every FriendListPanel copy in the scene (Arcade, Arena, Mission, Toybox) carries them.
   - Wire the Recent pair into `FriendsListPanel`: `recentContent` (the `Content` `RectTransform` under `Recent Scroll View/Viewport`) and `recentEmptyText` (the `TextMeshProUGUI` on `Recent Scroll View/RecentEmptyText`). Every FriendListPanel copy in the scene (Arcade, Arena, Mission, Toybox) carries them. The store needs no scene wiring: `Resources/RecentPlayersConfig.asset` is loaded by name.
   - Wire `SO_ProfileIconList` into `ArcadeLobbyList` / `FriendsListPanel` for avatars.
   - The invite badge: an `InviteBadge` child (Image on the built-in `Knob` sprite + `CanvasGroup` + `PartyInviteBadge`, with a `Count` `TextMeshProUGUI` child) under every `ArcadeLobbyList` (Arcade, Arena, Mission, Toybox); wire `PartyInviteBadge.connectionData` (`HostConnectionData.asset`), `canvasGroup` (its own) and `countText` (the `Count` TMP). `ArcadeLobbyList` needs no reference to it.
   - The popup's `detailText` on `PartyInviteNotificationPanel Variant.prefab` is the variant's `OnlineStatus` label (wired on the prefab, not the scene instance).

Prefabs live in `_Prefabs/UI Elements/Panels/Party/`. SO assets
(`HostConnectionData.asset` + the event/list assets) live in
`_SO_Assets/Host Connection Data/`; `FriendsData.asset` in
`_SO_Assets/Friends Data/`.

## Cross-references

- `ARCHITECTURE.md` — services, locked design, SOAP event flow (service-level), error-handling matrix.
- `INVITE_ENHANCEMENTS.md` — planned UI work (in-party invite guard, live-status refresh, the SOAP confirm-popup).
- `../PresenceSystem/ARCHITECTURE.md` — presence lobby, `invite_payloads` / `joined_party` property semantics.
