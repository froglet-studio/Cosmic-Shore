# Party Invite Enhancements - Task Capture & Planning

Four related party-invite improvements, captured for planning. We will
plan and ship them **one at a time** (this doc is the shared capture; each
task gets its own design sign-off + commit sequence when we pick it up).

Read `ARCHITECTURE.md` (locked design), `BUGS.md` (open issues),
`UI.md` (current UI surface), and `../PresenceSystem/ARCHITECTURE.md`
(presence lobby) before touching any of this - Tasks 2 and 3 are in the
locked-design area.

Status legend: [red] not started * [yellow] in progress * [green] done * [white] deferred

| # | Task | Layer | Status |
|---|------|-------|--------|
| 0 | **Invite popup** - reuse the existing `PartyInviteNotificationPanel` (bottom-left, global in Menu_Main, 3s auto-hide, latest-wins) + 10s panel row + host revert; **0b:** cancel-invite (X) - **replanned: reuse the panel; generic popup system removed** | Party UI | [yellow] code done * Unity wiring pending |
| 1 | Disable inviting a player already in my party - **decided: disable + relabel** ("IN YOUR PARTY") | Party UI | [green] code done * verify |
| 2 | Make pending-invite / in-lobby status **responsive & live** (foreground/background poll, jitter, optimistic UI; push as the deep option) | Presence | [red] ready to plan |
| 3 | Party-merge on accept (a **host** who accepts brings its whole party; a **member** moves alone) | Party (Netcode + presence) | [blocked] discuss-only |

**Suggested order: 0/1 -> 2 -> 3.** Task 0 (popup) is standalone UI infra and
Task 1 is a small self-contained UI guard - either can go first (Task 0 also
improves the invite UX that Task 1 touches). Task 2 makes the online panel's
lobby/status rows live and accurate, which Task 3 *depends on* (players must
see each other's live "in lobby N/M" state for the merge to feel correct).
Task 3 is the large one (voluntary host migration) and should land last.

---

## Implementation status (2026-06-19) - REPLANNED: reuse `PartyInviteNotificationPanel`

**Replan decision (owner):** the invite popup reuses the existing, complete
`PartyInviteNotificationPanel` (avatar + inviter name + Accept/Decline; already
subscribes to `OnInviteReceived`, routes to `PartyInviteController`, and dismisses on
`OnInviteResolved`) - it was an **orphaned prefab** (`_Prefabs/UI Elements/Panels/Party/
PartyInviteNotificationPanel.prefab`), in no scene and spawned by nothing. The generic
`PopupChannel`/`PopupModels`/`InvitePopupController` + the `PopupPanel`/`PopupManager`
extensions were **removed** (PopupPanel/PopupManager restored to their original
single-button form).

**Task 1 (disable + relabel)** - [x] done. `OnlineInfoEntry.Status.InYourParty` ("IN YOUR
PARTY N/M"), + status relabel "IN LOBBY"->"IN PARTY", "LOBBY FULL"->"PARTY FULL". Inspector:
optionally set `inYourPartyColor`. No prefab structure change.

**In-a-match invite disable (except lava-lamp freestyle)** - [x] already implemented, no
code. `ResolveRemoteStatus` -> `InMatch` when the remote's presence `matchName` is set;
`InMatch` is not invitable. `HostConnectionService.ResolveCurrentMatchName()` returns
empty on the menu scene (`IsOnMenuScene()`, incl. lava-lamp/freestyle) and only the game
mode in a real multiplayer game - so freestyle players stay invitable, real-match players
don't. Verify in-engine only.

**Task 0 (invite popup)** - [x] code done (`PartyInviteNotificationPanel`: 30s auto-decline
-> **3s auto-hide** that does NOT decline; latest-wins is inherent - `OnInviteReceived`
overwrites the pending invite + resets the timer). (!) needs Unity wiring (below).

**Task 0b (cancel-invite) + timing retune** - [x] done (`HostConnectionService.CancelInviteAsync`,
`OnlineInfoEntry` cancel-X, `OUTGOING_INVITE_TIMEOUT_SECONDS` + `partyInviteExpirationSeconds`
30->10). Unchanged by the replan.

### Unity wiring checklist
1. **Place `PartyInviteNotificationPanel.prefab` in Menu_Main** at a **top-level canvas**
   (so it shows on any screen - the "global" requirement), anchored **bottom-left**.
   Wire its inspector refs: `connectionData` = `HostConnectionData.asset`,
   `profileIcons` = `SO_ProfileIconList`, and the `inviterNameText` / `inviterAvatarImage` /
   `acceptButton` / `declineButton` / `canvasGroup` (the prefab already has these).
   Confirm `autoHideSeconds` shows **3** (the field was renamed from `autoDeclineSeconds`,
   so the prefab's old `30` is dropped and the 3s default applies - verify).
2. **`OnlineInfoEntry` prefab** - add a small cancel **X** `Button` child -> wire the new
   `cancelButton` field (auto-shows only while an invite is pending).
3. **`FriendsListPanel`** - set `partyInviteExpirationSeconds = 10` in the inspector (the
   code default only applies to fresh instances; an existing serialized 30 wins).

### Known follow-up (scoped out)
- **Per-host `OnInviteResolved`.** Resolve rides the existing broad `OnInviteResolved`
  (NoParam): accept/decline clears the popup AND all `FriendsListPanel` invite rows.
  Correct for the common single-inviter case; with simultaneous invites from two
  *different* hosts it also clears the other's row. Per-host needs the event to carry the
  host id (SOAP asset re-type + threading through `DeclineInviteAsync`) - deferred.
- B10 "Host disconnected" notice stays on the existing toast (the generic popup that would
  have served it was removed).

---

## Locked-design constraints (all three tasks must respect)

From `../README.md` and `ARCHITECTURE.md` - do not relitigate:

- **EAGER per-user Relay ("Always-InParty").** Every authenticated player
  hosts its own Relay-backed party session from menu entry. **Do not
  reintroduce LAZY / on-first-invite creation.** Task 3's cascade must work
  *within* the eager model.
- **Single-writer SOAP.** `HostConnectionService` is the **only** writer to
  `HostConnectionDataSO`. UI reads via SOAP lists/events.
- **Session is authoritative over presence.** The presence lobby is a
  discovery/signalling hint; the party **session** player list is the source
  of truth for membership (this is the B8 fix - `ScanPresenceForJoinedPartyMembers`
  cross-checks every presence claim against the session).
- **`.AsMainThread()` at every UGS / Netcode await** (`../THREADING.md`).
- **`PartyStateMachine` is the lifecycle authority** - no boolean-flag drift.
  States today: `Disconnected`, `HostingParty`, `InParty`, `Inviting`,
  `JoiningParty`.

---

## Current architecture recap (grounding for all three)

File:line references drift - re-grep before trusting (per `REFACTOR.md`).

### The refresh loop (one timer drives everything)

`HostConnectionService.Update()` calls `_scheduler.ShouldFireNow(...)` every
frame and fires `RefreshAsync().Forget()` when the interval elapses
(`HostConnectionService.cs` ~`:333-343`). HCS is `DontDestroyOnLoad` and
runs from auth sign-in onward, so **the loop is always on** while the app is
in the menu - it is *not* tied to any panel.

Cadence (`LobbyRefreshScheduler.cs`):
- Base `refreshIntervalSeconds = 1.5f` (serialized on HCS, ~`:63`).
- Boosted `BOOSTED_INTERVAL_SECONDS = 0.75f` for `BOOST_WINDOW_SECONDS = 15f`
  after invite events (`Boost()`), then falls back to base.
- UGS lobby reads are rate-limited to **~1/s per client** (SDK), which is the
  hard floor - going faster surfaces the B1/B6 stale-index churn.

`RefreshAsync()` (~`:965`) does, in one tick, **all** of:
1. `_lobbyService.RefreshAsync()` - pull presence-lobby state (1 network read).
2. `RefreshOnlinePlayersDiff()` (~`:1228`) - upsert `HostConnectionDataSO.OnlinePlayers` (SOAP list; cheap, no extra network).
3. Incoming-invite scan - `TryFindIncomingInvite` / `TryRaiseIncomingInvite` -> `RaiseInviteReceived` (~`:1150-1226`).
4. `ScanPresenceForJoinedPartyMembers()` (~`:1321`) - host detects who joined its session.
5. Party-session refresh + member sync (`RefreshPartyMembersAsync` / `PartyMemberService.SyncFromSession`).
6. Reconnect watchdog (`MAX_REFRESH_ERRORS_BEFORE_RECONNECT`).

`ForceRefreshNow()` (debounced by `FORCE_REFRESH_COOLDOWN_SECONDS = 0.5f`) is
already called on panel open by `ArcadeLobbyList.OnEnable` and
`FriendsListPanel.OnEnable`.

### How invites travel (poll-based, per-player presence properties)

- **Send** (`SendInviteAsync` ~`:486`): writer ensures its own Relay session
  exists, then writes `invite_payloads` on **its own** presence-lobby player
  property - one line per target: `targetId|hostId|sessionId|hostName|avatarId`
  (`InviteService` owns the format). First invite moves `InParty -> Inviting`.
- **Receive**: every client's `RefreshAsync` scans **all other players'**
  `invite_payloads` for a line whose `targetId == myId` -> raises
  `OnInviteReceived` -> `FriendsListPanel.HandlePartyInviteReceived` spawns a
  request row (and auto-opens the panel).
- **Accept** (`AcceptInviteAsync` ~`:599`, via `PartyInviteController.AcceptInviteAsync`):
  1. publish accept signal to the host (`AcceptanceSignalService`),
  2. **leave own session** (`_partySessionService.LeaveAsync()` ~`:635`),
  3. **join inviter's session** (`JoinByIdAsync(invite.PartySessionId)` ~`:638`),
  4. `IsPartyHost = false`, reseed `PartyMembers` with the host,
  5. `PublishJoinedPartyAsync(sessionId)` - write `joined_party = sessionId`
     so the host's scan admits us.

### The online-panel UI

- **`FriendsListPanel`** (`UI/Elements/FriendsListPanel.cs`) renders the
  Online section (one `OnlineInfoEntry` per `OnlinePlayers` entry, local
  player excluded) and the Requests section (party invites + friend
  requests). It subscribes to the SOAP lists/events in `OnEnable`,
  unsubscribes in `OnDisable`, and `ForceRefreshNow()`s on open.
- **`OnlineInfoEntry`** (`UI/Elements/OnlineInfoEntry.cs`) is the row; the
  **whole row background is the invite button**. `_invitable` is set in
  `Populate`: `onInvite != null && (status == Online || status == InLobby)`.
- **`ArcadeLobbyList`** (`UI/Elements/ArcadeLobbyList.cs`) is the 4-slot
  party panel (slot 0 = local, slots 1-3 = remote `PartyMembers`).
- `FriendsListPanel.ResolveRemoteStatus` maps a remote player to
  `Online / InLobby / LobbyFull / InMatch` from their advertised
  `PartyMemberCount` / `PartyMaxSlots` / `MatchName`. It already has an
  `IsInSameParty(playerId)` helper (used only for the LobbyFull branch today).

---

## Task 0 - SOAP confirm-popup (1/2-button, event-channel-driven) [red]

> **(!) SUPERSEDED (2026-06-19).** This generic-popup design was built then **removed** -
> see "Implementation status" above. The invite popup now reuses the existing
> `PartyInviteNotificationPanel`. The section below is kept only as the original design
> record (and as a reference if a generic confirm/OK popup is wanted later).

**Goal.** A small reusable popup with **1 or 2 dynamic-label buttons**, raised
and answered through **SOAP event channels**, used to surface party invites
(Accept / Decline) and generic OK / confirm dialogs everywhere else.

**Decision (locked): SOAP-drive the existing `PopupPanel`.** Reuse
`PopupPanel`'s lightweight visual + `PopupManager`'s pooling; add a second
button + dynamic labels; drive it via a request/result `ScriptableEvent` pair.
Do **not** add a third parallel popup system, and do **not** build it on the
heavy `ModalWindowManager` stack.

**Current state (what exists, why each is insufficient).**
- `PopupManager` + `PopupPanel` (`_Scripts/Utility/`) - title + body + **one**
  confirm/close button; static-instance API (`ShowPopupPanel(title, body,
  closeable)`); **not SOAP**, **1 button**. The base we extend.
- `PurchaseConfirmationModal : ModalWindowManager` - 2-button confirm but
  **purchase-specific** (`VirtualItem` + `Action` callback), rides the heavy
  `ScreenSwitcher.PushModal` stack. Wrong weight class.
- Two toast systems (`UI/ToastSystem/` **and** `UI/ToastNotification/`) -
  transient, **non-interactive**. Good for fire-and-forget "okay" toasts, not
  for Accept/Decline. (Their duplication is a separate cleanup.)
- `GenericEventChannelWithReturnSO<T,Y>` - **synchronous** `Func<T,Y>`; a popup
  result comes back *later* (user clicks), so a sync return channel can't carry
  it. Use a request event + a result event instead.

**Design sketch (ratify in planning).**
- **`PopupRequest`** payload: `Title`, `Message`, `PrimaryLabel`,
  `SecondaryLabel` (null/empty -> render a single OK button), `RequestId`.
- **`ScriptableEventPopupRequest`** - any system raises it to show a popup.
- **`ScriptableEventPopupResult`** - the popup raises it with
  `{ RequestId, PopupResult: Primary | Secondary | Dismissed }`; requesters
  correlate by `RequestId`.
- A persistent **`PopupPresenter`** (or evolve `PopupManager`) subscribes to the
  request channel, pulls a pooled `PopupPanel`, shows it, and raises the result
  channel on click. `PopupManager.ShowPopupPanel` stays working (routes through
  the same path or remains a thin info-only wrapper).
- **Party-invite adapter:** subscribe to `OnInviteReceived` -> raise
  `PopupRequest("Party Invite", "<name> invited you", "Accept", "Decline")`; on
  result `Primary` -> `PartyInviteController.AcceptInviteAsync(invite)`,
  `Secondary`/`Dismissed` -> `DeclineInviteAsync`. The vestigial
  `PartyInviteNotificationPanel` is then repurposed (its prefab becomes the
  invite `PopupPanel`) or deleted.
- **SOAP hygiene:** fail-loud on missing event refs (no if-null guards on
  `ScriptableEvent` fields); the presenter listens to a channel rather than
  exposing a new singleton.

**Decided - invite popup placement, style & timing.**
- **Small, bottom-left, non-modal.** The invite popup is a small card anchored
  bottom-left so it never covers the player's main UI. It does **not** block
  raycasts - the user can ignore it and keep using the menu. (The per-request
  `Modal` flag still exists for *confirm* dialogs elsewhere - leave-party,
  purchases - which ARE modal. Invites set `Modal = false`.)
- **Popup auto-hides after 3s.** Purely a visual teaser; hiding the popup does
  NOT decline the invite.
- **Latest-wins (not FIFO queue).** The popup always shows the **newest** invite.
  If invite B arrives while invite A's popup is showing (say at 1.5s), the popup
  **instantly swaps to B** and restarts its 3s timer. Preempted invites are NOT
  lost - they remain in the `FriendsListPanel` Requests list (below) for their
  full lifetime; only the single popup surface is latest-wins.

**Decided - unified ~10s invite lifetime across all three surfaces.**
- **Recipient panel row: 10s.** The invite's `RequestInfoEntry` row in
  `FriendsListPanel` lives **10s**, then auto-removes (today `partyInviteExpirationSeconds = 30f`
  -> retune to 10).
- **Host side clears at the same expiry.** When the invite expires (or is
  resolved), the **host's** "Pending Invite" row reverts to the invitee's normal
  **"online"** status and the host can **send a new invite** again. UGS gives the
  sender no decline signal, so this is the host's own outgoing timeout
  (`OUTGOING_INVITE_TIMEOUT_SECONDS = 30f` -> retune to ~10 so all three surfaces
  clear together) firing `OutgoingInviteCleared` -> `HandleOutgoingInviteCleared`
  resets the row (path already exists).
- So one logical invite life ~ 10s: popup visible 3s, panel row 10s, host pending
  10s - all converging.

**Decided - invite shows in BOTH places, dedup on resolve.** An incoming invite
surfaces as *both* the popup *and* a `FriendsListPanel` Requests row. When the
invite is accepted/declined from **either** surface, **both** must clear - no
leftover / stacked invite row for the same inviting host. This rides the existing
`OnInviteResolved` SOAP event: the popup's accept/decline goes through
`PartyInviteController.AcceptInviteAsync`/`DeclineInviteAsync` (which raise
`OnInviteResolved`), and the popup adapter *also subscribes* to `OnInviteResolved`
to dismiss itself - so resolving on the panel kills the popup and vice-versa.
**Design note:** today `OnInviteResolved` is parameterless and
`FriendsListPanel.HandleInviteResolved` clears *all* party-invite rows (fine while
there's one pending invite). With latest-wins meaning multiple invites can sit in
the panel at once, this needs a **per-host resolved signal** so accepting host A's
invite doesn't wipe host B's row.

### Task 0b - Cancel-invite affordance (host side) [red]

Tied to Task 0 + Task 1's row states. After a host sends an invite, the target's
row shows **"Pending Invite"** (`OnlineInfoEntry.SetInvitePending` - yellow pulse,
invite button disabled). **Add a cancel (X) icon on that pending row** so the host
can retract the outgoing invite manually (instead of only waiting for the 10s
timeout).

- On cancel -> a new host-side `HostConnectionService.CancelInviteAsync(targetId)`:
  remove the target from `InviteService`, **re-publish `invite_payloads` without
  that line** (so the recipient's invite/popup/row disappear too), and fire
  `OutgoingInviteCleared` -> the row reverts to the invitee's "online" status,
  re-invitable.
- There's no public cancel path today (only `SendInviteAsync`); `InviteService`
  already has `Remove(targetId)` + the timeout machinery to build on.

**Open questions.**
- Result delivery: a `PopupResult` **event channel keyed by `RequestId`** (fully
  SOAP - matches "through event channels") vs. callbacks carried in the request
  payload (simpler). Lean to the channel; confirm.
- Per-host `OnInviteResolved` (needed once multiple invites can coexist in the
  panel - see design note): add a `hostPlayerId` to the resolved signal, or key
  the clear by the resolved invite's host.
- Exact placement/size of the bottom-left popup vs. existing bottom-left HUD
  elements (avoid overlap).

**Acceptance.**
- A 2-label `PopupRequest` shows a 2-button popup; clicking either raises the
  matching `PopupResult`; a 1-label request shows a single OK button; missing
  event refs fail loud.
- An incoming invite shows a small **bottom-left** popup that **auto-hides after
  3s** and does not block the menu; a second invite **instantly replaces** it.
- The same invite sits in the `FriendsListPanel` Requests list for **10s** then
  auto-removes; at that point the **host** sees the invitee as **"online"** again
  and can re-invite.
- Accept/decline from either popup or panel **clears both** for that host.
- The host's **"Pending Invite"** row has a **cancel (X)** that retracts the
  invite (recipient's invite vanishes; host row reverts to online).

---

## Task 1 - Disable inviting a player already in my party [red]

**Goal.** When player B has joined my party, B's row in my Online list must
not be clickable as an invite (and ideally A's row in B's list too). Today
it still is.

**Current behavior (the bug).** A party member with others renders as
`InLobby` (their `PartyMemberCount > 1`), and `ResolveRemoteStatus` returns
`InLobby` for them **without** consulting `IsInSameParty`. `OnlineInfoEntry`
then sets `_invitable = true` for `InLobby`, so the row stays clickable -
re-inviting someone who is already in my party. (Re-clicking is largely
idempotent in `SendInviteAsync` via `_inviteService.Contains`, but the UI
should not offer it at all.)

Evidence: `FriendsListPanel.ResolveRemoteStatus` / `IsInSameParty`
(`FriendsListPanel.cs` ~`:321-353`); `OnlineInfoEntry.Populate` `_invitable`
rule (`OnlineInfoEntry.cs` ~`:111-127`).

**Proposed approach (small, UI-only - no service changes).** In
`FriendsListPanel.PopulateOnlineEntry`, when `IsInSameParty(player.PlayerId)`
is true, pass `onInvite: null` so the row renders non-invitable. The row then
takes `disabledTint` automatically. Optionally give it a clearer label
(e.g. `IN YOUR PARTY`) instead of `IN LOBBY N/M` - a new
`OnlineInfoEntry.Status` value or a small label override.

Re-render is already wired: `HandlePartyMemberChanged` calls
`PopulateOnlineSection()` on every party-member add/remove, so the guard
re-evaluates the moment B joins/leaves.

**Decision (locked): Disable + Relabel.** Keep the in-party member visible in
the Online list but render the row **non-invitable** (disabled) **and
relabel** it so the state is explicit - e.g. `IN YOUR PARTY` - instead of the
misleading `IN LOBBY N/M`. Do NOT hide the row. Implementation: add a
dedicated `OnlineInfoEntry.Status.InYourParty` value (own label text + colour)
so the label/colour are clean and self-documenting rather than overloading the
`InLobby`/`disabledTint` combo; `_invitable` stays false for it. The party
member is still shown in `ArcadeLobbyList`'s slots as well - that's fine and
intended (the Online row now reads as a status indicator, not an action).

**Acceptance.** With A+B partied: in A's FriendsListPanel, B's row is visibly
non-clickable (disabled tint, optional "in your party" label); clicking does
nothing; when B leaves, B's row becomes invitable again. Symmetric on B's
panel for A.

---

## Task 2 - Make pending-invite / in-lobby status responsive & live [red]

**The question (user's framing).** "What can we do to make the status of
pending invite / in lobby more responsive and live?"

**Why there's lag today.** Everything is **pure poll**. The presence lobby is
read by an explicit GET (`PresenceLobbyService.RefreshAsync` -> `lobby.RefreshAsync()`)
on the `HostConnectionService` tick - base **1.5s**, **0.75s** while boosted
(15s window after invite events). There is **no event subscription** on the
presence lobby, so any remote status change (a player goes "IN LOBBY 2/4", an
invite is accepted, a member leaves) is only seen on *your next GET tick*.
Total visible latency = the other client's write-propagation + your poll
interval. So "more live" = some mix of (a) poll faster when it matters,
(b) stop the poll from being *delayed* by backoff, (c) update locally without
waiting for a round-trip, and ultimately (d) switch from poll to push.

### Levers (cheap -> deep). Recommend shipping L1-L4 now; evaluate L5 (push) as the true-live follow-up.

**L1 - Optimistic local UI (mostly already in; extend).** The sender's row
already flips to `PENDING REQUEST` instantly on click
(`OnlineInfoEntry.SetInvitePending`), and an accepted incoming invite row is
removed optimistically (`FriendsListPanel.OnAccept...`). Extend the same idea to
every *local* state change (e.g. when *I* join/leave, update my own rows
immediately from the SOAP event rather than waiting for the next GET). Zero
network cost, instant feel for the acting user.

**L2 - Boost on the events that matter (already exists; broaden).**
`LobbyRefreshScheduler.Boost()` drops the interval to 0.75s for 15s after
invite send/receive/accept-signal. Confirm every status-relevant transition
boosts (invite sent, invite received, member joined, member left, kick) so
the *other* side's change is picked up within ~0.75s during the active window.

**L3 - Panel-open "foreground" cadence (the main near-term win).** Hold a fast
interval (~1.0s, at the rate-limit floor) **while `FriendsListPanel` /
`ArcadeLobbyList` is open**, and fall back to a slower interval (~3-5s) when
no such panel is open. Add a sticky `HostConnectionService.SetPanelForeground(bool)`
(refcounted for two panels open at once), toggled from the existing
`OnEnable`/`OnDisable` hooks that already call `ForceRefreshNow()`. UI calls
it; HCS owns the scheduler (single-writer-safe).

> **(!) Do NOT stop the poll when closed.** Invite *detection*, party-member
> sync, the accept handshake, and the joined-member scan all run **inside the
> same `RefreshAsync`**. If the loop stops while panels are closed, a recipient
> never detects an incoming invite (the `OnInviteReceived` raise that
> auto-opens the panel is *inside* the loop), the host misses joins/leaves,
> and the handshake stalls. Closed = **slower**, never **off**.

**L4 - Don't let the poll get *delayed* (jitter + coalesce).** Rate-limit
backoff and stale-index retries (B1/B6) can stretch the effective interval far
beyond the nominal one - that *is* a responsiveness bug. Add per-client
refresh **jitter +/-10%** (`../PresenceSystem/TODOS.md` **P3**) so clients don't
cluster reads on the same wall-clock tick and trip 429s, and coalesce startup
writes (**P2**). Net: fewer backoffs -> the nominal cadence is actually
honoured. (`../PresenceSystem/TODOS.md` **P7** - gate the online-list diff on
visibility - folds in here too, though the diff itself is cheap.)

**L5 - Switch presence to PUSH (the real "live" answer; bigger, riskier).**
UGS Lobby supports `SubscribeToLobbyEventsAsync` (WebSocket `LobbyChanged`
callbacks). The presence lobby does **not** subscribe today - it only polls.
Subscribing would deliver remote changes in *real time* (raise our SOAP
updates on the pushed delta) and let us cut the poll to a slow safety net.
This is the ROADMAP **push-based invites/presence** item.
**Risk:** this is the *same* SDK delta path that B1/B6 stale-index churn comes
from (`LobbyPatcher.ApplyPatchesToLobby` fires on those very callbacks), so
adopting push may amplify B1/B6. Gate behind `BenignLobbyLogFilter`, prove it
in MPPM, and treat it as its own project - not part of the L1-L4 pass.

**Floor constraint.** UGS lobby reads are rate-limited to ~1/s per client, and
the *effective* read rate is already above the tick (a refresh does a lobby
read, often a session read, plus writes on change - the B1/B6 source). So the
fast/foreground interval can't go below ~1s without risking 429s. "Decrease
the delay" is bounded by this floor; the real lever for *closed* idle is L3,
and for *true* liveness is L5.

**Open questions.**
- Which panels count as "foreground" - `FriendsListPanel` only, or also
  `ArcadeLobbyList`?
- Foreground vs background interval values: ~1.0s open / ~3-5s closed?
- Do we pursue L5 (push) now, or only after B1/B6 are hardened?

**Acceptance.** With a panel open: a remote's status change ("ONLINE" -> "IN
LOBBY 2/4"), a newly-online player, and a sender's `PENDING REQUEST` clearing
on accept all appear within ~1 refresh (~1s). With all panels closed: an
incoming invite still pops within the slow interval (no missed invites) and
idle SDK read volume drops measurably. No increase in B1/B6 429/stale-index
log frequency.

---

## Task 3 - Party-merge on accept (voluntary host migration) [red]

> **[blocked] DISCUSS-ONLY - do NOT implement yet.** Per owner direction, this needs
> detailed, cautious design discussion before any code. This section is the
> problem statement + an options sketch to argue from, not an agreed plan. It
> touches the locked party core, voluntary host migration, concurrent
> multi-join (the B5 failure surface), and Netcode session teardown - all of
> which we will work through together before a single commit. Everything below
> is provisional.

**Goal.** Consider a party: host **A** + member **B**. Player **C** sees A
and B in its online panel as "IN LOBBY 2/4".
- If **C invites A (the host)** and A accepts -> A joins C **and B is brought
  along** (B force-joins C). Result: **C is host**, party = {C, A, B}.
- If **C invites B (a member)** and B accepts -> **only B** leaves A and joins
  C. Result: A's party = {A}, C's party = {C, B}.

Generalised: **a host accepting an invite migrates its whole party to the new
host; a non-host accepting moves alone.**

### Current behavior vs the gap

`AcceptInviteAsync` already does "leave my current session, join the
inviter's." So:

- **Non-host accept (C invites B) - likely already works, needs verification.**
  B is a guest in A's session (in the eager model B left its own session when
  it joined A). On accepting C, B's `LeaveAsync` leaves **A's** session and
  `JoinByIdAsync` joins C's; A's host-side `SyncFromSession` then drops B from
  A's roster. Net: B moves alone. [x] in principle - must confirm a *guest*
  (not its own host) accepting cleanly leaves A's party and updates A's slots.

- **Host accept (C invites A) - NOT handled. This is the new work.** When A
  accepts, A leaves **A's own session (the one B is in)** and joins C's. **B
  is left in A's now-abandoned session** with no signal to follow. There is
  **no host-migration / "bring my members" path today** (the ROADMAP lists
  host-loss/migration as unaddressed). The cascade has to be built.

Evidence: `AcceptInviteAsync` leave-own->join-inviter (`HostConnectionService.cs`
~`:635-659`); host admits joiners via `joined_party` +
`ScanPresenceForJoinedPartyMembers` (~`:1321`); no consumer reads a
"migrate" signal anywhere.

### Design sketch (to be ratified before coding)

Branch the accept on `connectionData.IsPartyHost && RemotePartyMemberCount > 0`:

- **Member accept (not host, or solo host):** existing flow unchanged.
- **Host accept (cascade):** A must hand C's session id to each current
  member so they migrate. Candidate mechanisms (the presence lobby is the
  right vehicle - it's independent of the party session, so it survives A
  tearing its own session down):

  - **Option A - reuse `invite_payloads` + auto-accept.** Before leaving, A
    writes invite lines for each member (B) pointing at **C's** session, and
    members auto-accept (no UI) because the redirect comes from their current
    host. Pro: reuses all existing invite plumbing. Con: "A advertises C's
    session" is slightly odd; needs an auto-accept-when-already-partied path.
  - **Option B - dedicated `party_migrate` presence property (recommended to
    evaluate first).** A writes `party_migrate = {newSessionId, newHostId}`
    on its presence entry; members watch their current host's entry and call a
    new `MigrateToSessionAsync(newSessionId)` (leave current, join new, no UI).
    Cleaner semantics; one new key + handler; composes with the existing
    `joined_party` admit-scan on C's side.

**Sequencing / race safety (the hard part).** Avoid a window where A's
session is gone but B hasn't learned C's id:
1. A writes the migrate signal to the **presence lobby** (persists
   independent of A's party session, and A stays in the presence lobby).
2. A leaves A's session and joins C (`joined_party = C`).
3. B's refresh reads the migrate signal, leaves A's (now-dead) session, joins
   C (`joined_party = C`).
4. C's `ScanPresenceForJoinedPartyMembers` admits A then B against C's
   authoritative session list (the B8 cross-check already guards stale
   claims).

This **requires Task 2's fast/accurate refresh** so the migrate signal and
the resulting roster changes propagate within ~1s, not 1.5-5s - otherwise the
merge feels broken (B lingers in a dead session; C's slots fill slowly).

### Open questions (must resolve before coding)

- **Vehicle:** Option A (reuse invites) vs Option B (`party_migrate` key) vs a
  session-broadcast. Recommend prototyping Option B.
- **Auto-accept policy:** members migrate **without** a prompt (user said
  "force join") - confirm. What if a member is mid-`PartyInviteController`
  transition, or is themselves hosting sub-members (3-level chains)? Cap to
  the 2-level model first.
- **Capacity:** C's party + A + B must fit `MaxPartySlots (4)`. If the merge
  would overflow, what happens (reject A's accept? partial merge? toast)?
- **Failure mid-cascade:** if B fails to join C, does B fall back to a fresh
  solo host (the existing bounce/`RecoverFromFailedTransitionAsync` path)? A
  is already in C - B must not get stuck in the dead session.
- **Netcode transition:** each migrating member runs the full PIC
  shutdown->join sequence; confirm N members migrating concurrently don't trip
  B5 (second-joiner) - Task 3 likely **blocked on B5** (`BUGS.md`).
- **State machine:** add explicit states (e.g. `MigratingParty`) vs reuse
  `JoiningParty`.

### Dependencies & acceptance

- **Depends on:** Task 2 (live refresh) and is **gated on B5** (multi-joiner
  reliability) - a host-accept cascade is exactly the concurrent-multi-join
  case B5 currently fails.
- **Acceptance.** {A,B} partied; C invites A; A accepts -> within ~1-2s all of
  A & B are in C's party with C as host, no orphan vessel/session, every
  peer's panel shows {C,A,B} "IN LOBBY 3/4". Separately, C invites B; B
  accepts -> B alone joins C, A's panel shows A solo. No console
  `[Invalid Destroy]` / stale-session errors; MPPM-verified per the S-series
  in `TESTS.md` (new cases to be added).

---

## Cross-references

- `ARCHITECTURE.md` - locked design, error-handling matrix, exit criteria.
- `BUGS.md` - **B5** (multi-joiner - Task 3 blocker), B8 (presence-vs-session
  authority - the cross-check Task 3 reuses).
- `../PresenceSystem/TODOS.md` - **P3** (jitter), **P7** (panel-gated diff) -
  folded into Task 2.
- `../PresenceSystem/ARCHITECTURE.md` - presence lobby, `joined_party` /
  `invite_payloads` property semantics.
- `../MultiplayerArchitecture/ROADMAP.md` - push-based invites (Task 2
  long-term), host-loss/migration (Task 3 is the *voluntary* sibling).
