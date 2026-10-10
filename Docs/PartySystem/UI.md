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
Friend + Requests). They share the same component family:

| Component | Purpose |
|---|---|
| `ArcadeLobbyList` | The party panel: 4 slots in the **synced seating** (host first, then clients in join order — see "Seating" below; NOT local-player-first), a Leave button, and a live "N Players Online" counter. An empty slot's "+" opens `FriendsListPanel`. |
| `FriendInfoSlot` | A single slot in `ArcadeLobbyList` — one of three states: local player, occupied (member avatar + name; plus a **host-only kick ✕** on remote-member slots), or empty ("+" add button). On `FriendsInfo.prefab`. Also **ensures its own `PartySlotDomainGlow`** at `Awake` (see "Seating"). |
| `PartyRoster` | Pure static. THE party seating order, and the only place it is decided. No Unity, UGS or Netcode types — the caller supplies the client-id lookup, which is what makes it edit-mode testable (`PartyRosterTests`). |
| `PartySlotDomainGlow` | The animated halo behind a slot's avatar, tinted with that pilot's live domain. Generated (GameObject, sprite and rect) rather than authored, so it needs no scene wiring and no party surface can ship without it. |
| `FriendsListPanel` | Combined social panel — **no tabs; all three sections render at once**: **Online** (every presence-lobby player) + **Add Friend** (a `TMP_InputField` for a pilot's exact display name, a Send button and a one-line status label; see "Add Friend section" below) + **Requests** (incoming friend requests AND incoming party invites). Auto-opens when a party invite arrives. Reads `HostConnectionDataSO` + `FriendsDataSO` SOAP lists; the only thing it WRITES goes through `FriendsServiceFacade`. There are four scene-placed copies in Menu_Main (Arcade, Arena, Mission and Toybox screen modals), all wired alike. |
| `OnlineInfoEntry` | A row in the Online section with a small **Invite** button (shown only when the player is invitable) and a **✕** that cancels a pending outgoing invite or (host only) kicks an in-party member. Tints yellow + pulses while an invite is pending; Invite/cancel/kick share an anti-spam cooldown. Status label: ONLINE / IN PARTY N/M / PARTY FULL / IN A MATCH / IN YOUR PARTY N/M. A third button, **Join** (`joinButton` + `joinButtonIcon`), is one control with two faces resolved by `FriendsListPanel.ResolveJoinMode`: a blue doorway icon that walks you into that player's party with no invite (drawn disabled when the party is full), or — while the player is IN A MATCH — an amber eye icon that is the row's ONLY enabled button and **spectates** the match (`SPECTATOR.md`). On `OnlineFriendsInfo Variant.prefab` (a variant of `RequestsInfo`). |
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

### When the join fails: the notice survives the reload (2026-10-10)

Every failure arm of the accept flow ends in
`PartyInviteController.BounceToSoloMenuAsync`, which restarts the client's own solo host
(`RecoverFromFailedTransitionAsync`) and then raises "Couldn't join - returned to your menu."
on the `bounceToastChannel`. That notice used to be best-effort: `ToastService` is a
scene-bound MonoBehaviour that subscribes to the channel in `OnEnable`, so a line raised in
the gap around the Menu_Main reload had no subscriber and vanished, and the player saw the
menu come back with no explanation. The controller now raises it with
`ToastChannel.ShowPrefixOrHold`: delivered at once when a service is listening, otherwise
parked on the channel asset (which outlives the scene) for up to 45 s and drained by the
next `ToastService.OnEnable`. The hold is a mailbox, not a queue: a later held line replaces
an earlier one, and the plain `ShowPrefix` keeps its drop-when-unheard contract for gameplay
toasts. Pinned by `ToastChannelHoldTests`. Not yet seen in an editor: force a join failure
(accept an invite whose host has quit) and watch for the line on the fresh menu.

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
section between Online and Requests. It is by exact display name, which is what the retired
`AddFriendPanel` did and what the facade's by-name method supports; a recently-played-with
list (QoL item 3) would be the better source and is a separate decision. The by-ID method
`.SendFriendRequestAsync(playerId)` is still a live single-writer entry point with no caller.

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

## Scene wiring checklist (Menu_Main)

1. **Persistent GameObjects** (Bootstrap scene, `DontDestroyOnLoad`):
   `HostConnectionService` + `PartyInviteController` + `FriendsInitializer` on
   one GameObject; wire `HostConnectionDataSO`, `AuthenticationDataVariable`.
2. **AppManager** (Bootstrap): assign `HostConnectionData.asset` to
   `hostConnectionData`.
3. **Menu_Main UI:**
   - `ArcadeLobbyList` (party panel) as child of the Arcade screen; empty slots' "+" opens `FriendsListPanel`.
   - `FriendsListPanel` (Online + Add Friend + Requests) as child of the party area (start inactive); auto-opens on an incoming invite.
   - Wire `HostConnectionData.asset` into `ArcadeLobbyList` and `FriendsListPanel`.
   - Wire `FriendsData.asset` into `FriendsListPanel`.
   - Wire the `OnlineInfoEntry` (`OnlineFriendsInfo Variant`) and `RequestInfoEntry` (`RequestsInfo`) row prefabs into `FriendsListPanel` (`onlineInfoPrefab` + `requestInfoPrefab` — the only two row prefabs it spawns).
   - Wire the Add Friend trio into `FriendsListPanel`: `addFriendNameInput` (the `TMP_InputField` on `AddFriendInput`), `addFriendSendButton` (the `Button` on `AddFriendSendButton`), `addFriendStatusText` (the `TextMeshProUGUI` on `AddFriendStatus`). All three or none; every FriendListPanel copy in the scene (Arcade, Arena, Mission, Toybox) carries them.
   - Wire `SO_ProfileIconList` into `ArcadeLobbyList` / `FriendsListPanel` for avatars.

Prefabs live in `_Prefabs/UI Elements/Panels/Party/`. SO assets
(`HostConnectionData.asset` + the event/list assets) live in
`_SO_Assets/Host Connection Data/`; `FriendsData.asset` in
`_SO_Assets/Friends Data/`.

## Cross-references

- `ARCHITECTURE.md` — services, locked design, SOAP event flow (service-level), error-handling matrix.
- `INVITE_ENHANCEMENTS.md` — planned UI work (in-party invite guard, live-status refresh, the SOAP confirm-popup).
- `../PresenceSystem/ARCHITECTURE.md` — presence lobby, `invite_payloads` / `joined_party` property semantics.
