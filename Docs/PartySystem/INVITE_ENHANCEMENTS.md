# Party Invite Enhancements — Task Capture & Planning

Three related party-invite improvements, captured for planning. We will
plan and ship them **one at a time** (this doc is the shared capture; each
task gets its own design sign-off + commit sequence when we pick it up).

Read `ARCHITECTURE.md` (locked design), `BUGS.md` (open issues), and
`../PresenceSystem/ARCHITECTURE.md` (presence lobby) before touching any of
this — Tasks 2 and 3 are in the locked-design area.

Status legend: 🔴 not started · 🟡 in progress · 🟢 done · ⚪ deferred

| # | Task | Layer | Status |
|---|------|-------|--------|
| 1 | Disable inviting a player already in my party | Party UI | 🔴 |
| 2 | Online-panel refresh: panel-gated cadence (faster while open, slower — not stopped — while closed) | Presence | 🔴 |
| 3 | Party-merge on accept: a **host** who accepts brings its whole party to the new host; a **member** moves alone | Party (Netcode + presence) | 🔴 |

**Suggested order: 1 → 2 → 3.** Task 1 is a small, self-contained UI guard.
Task 2 makes the online panel's lobby/status rows live and accurate, which
Task 3 *depends on* (players must see each other's live "in lobby N/M" state
for the merge to feel correct). Task 3 is the large one (voluntary host
migration) and should land last.

---

## Locked-design constraints (all three tasks must respect)

From `../README.md` and `ARCHITECTURE.md` — do not relitigate:

- **EAGER per-user Relay ("Always-InParty").** Every authenticated player
  hosts its own Relay-backed party session from menu entry. **Do not
  reintroduce LAZY / on-first-invite creation.** Task 3's cascade must work
  *within* the eager model.
- **Single-writer SOAP.** `HostConnectionService` is the **only** writer to
  `HostConnectionDataSO`. UI reads via SOAP lists/events.
- **Session is authoritative over presence.** The presence lobby is a
  discovery/signalling hint; the party **session** player list is the source
  of truth for membership (this is the B8 fix — `ScanPresenceForJoinedPartyMembers`
  cross-checks every presence claim against the session).
- **`.AsMainThread()` at every UGS / Netcode await** (`../THREADING.md`).
- **`PartyStateMachine` is the lifecycle authority** — no boolean-flag drift.
  States today: `Disconnected`, `HostingParty`, `InParty`, `Inviting`,
  `JoiningParty`.

---

## Current architecture recap (grounding for all three)

File:line references drift — re-grep before trusting (per `REFACTOR.md`).

### The refresh loop (one timer drives everything)

`HostConnectionService.Update()` calls `_scheduler.ShouldFireNow(...)` every
frame and fires `RefreshAsync().Forget()` when the interval elapses
(`HostConnectionService.cs` ~`:333-343`). HCS is `DontDestroyOnLoad` and
runs from auth sign-in onward, so **the loop is always on** while the app is
in the menu — it is *not* tied to any panel.

Cadence (`LobbyRefreshScheduler.cs`):
- Base `refreshIntervalSeconds = 1.5f` (serialized on HCS, ~`:63`).
- Boosted `BOOSTED_INTERVAL_SECONDS = 0.75f` for `BOOST_WINDOW_SECONDS = 15f`
  after invite events (`Boost()`), then falls back to base.
- UGS lobby reads are rate-limited to **~1/س per client** (SDK), which is the
  hard floor — going faster surfaces the B1/B6 stale-index churn.

`RefreshAsync()` (~`:965`) does, in one tick, **all** of:
1. `_lobbyService.RefreshAsync()` — pull presence-lobby state (1 network read).
2. `RefreshOnlinePlayersDiff()` (~`:1228`) — upsert `HostConnectionDataSO.OnlinePlayers` (SOAP list; cheap, no extra network).
3. Incoming-invite scan — `TryFindIncomingInvite` / `TryRaiseIncomingInvite` → `RaiseInviteReceived` (~`:1150-1226`).
4. `ScanPresenceForJoinedPartyMembers()` (~`:1321`) — host detects who joined its session.
5. Party-session refresh + member sync (`RefreshPartyMembersAsync` / `PartyMemberService.SyncFromSession`).
6. Reconnect watchdog (`MAX_REFRESH_ERRORS_BEFORE_RECONNECT`).

`ForceRefreshNow()` (debounced by `FORCE_REFRESH_COOLDOWN_SECONDS = 0.5f`) is
already called on panel open by `ArcadeLobbyList.OnEnable` and
`FriendsListPanel.OnEnable`.

### How invites travel (poll-based, per-player presence properties)

- **Send** (`SendInviteAsync` ~`:486`): writer ensures its own Relay session
  exists, then writes `invite_payloads` on **its own** presence-lobby player
  property — one line per target: `targetId|hostId|sessionId|hostName|avatarId`
  (`InviteService` owns the format). First invite moves `InParty → Inviting`.
- **Receive**: every client's `RefreshAsync` scans **all other players'**
  `invite_payloads` for a line whose `targetId == myId` → raises
  `OnInviteReceived` → `FriendsListPanel.HandlePartyInviteReceived` spawns a
  request row (and auto-opens the panel).
- **Accept** (`AcceptInviteAsync` ~`:599`, via `PartyInviteController.AcceptInviteAsync`):
  1. publish accept signal to the host (`AcceptanceSignalService`),
  2. **leave own session** (`_partySessionService.LeaveAsync()` ~`:635`),
  3. **join inviter's session** (`JoinByIdAsync(invite.PartySessionId)` ~`:638`),
  4. `IsPartyHost = false`, reseed `PartyMembers` with the host,
  5. `PublishJoinedPartyAsync(sessionId)` — write `joined_party = sessionId`
     so the host's scan admits us.

### The online-panel UI (note: CLAUDE.md's `OnlinePlayersPanel`/`OnlinePlayerEntry` names are stale)

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

## Task 1 — Disable inviting a player already in my party 🔴

**Goal.** When player B has joined my party, B's row in my Online list must
not be clickable as an invite (and ideally A's row in B's list too). Today
it still is.

**Current behavior (the bug).** A party member with others renders as
`InLobby` (their `PartyMemberCount > 1`), and `ResolveRemoteStatus` returns
`InLobby` for them **without** consulting `IsInSameParty`. `OnlineInfoEntry`
then sets `_invitable = true` for `InLobby`, so the row stays clickable —
re-inviting someone who is already in my party. (Re-clicking is largely
idempotent in `SendInviteAsync` via `_inviteService.Contains`, but the UI
should not offer it at all.)

Evidence: `FriendsListPanel.ResolveRemoteStatus` / `IsInSameParty`
(`FriendsListPanel.cs` ~`:321-353`); `OnlineInfoEntry.Populate` `_invitable`
rule (`OnlineInfoEntry.cs` ~`:111-127`).

**Proposed approach (small, UI-only — no service changes).** In
`FriendsListPanel.PopulateOnlineEntry`, when `IsInSameParty(player.PlayerId)`
is true, pass `onInvite: null` so the row renders non-invitable. The row then
takes `disabledTint` automatically. Optionally give it a clearer label
(e.g. `IN YOUR PARTY`) instead of `IN LOBBY N/M` — a new
`OnlineInfoEntry.Status` value or a small label override.

Re-render is already wired: `HandlePartyMemberChanged` calls
`PopulateOnlineSection()` on every party-member add/remove, so the guard
re-evaluates the moment B joins/leaves.

**Open questions.**
- Show the in-party member as a **disabled** row, or **hide** it from the
  Online list entirely (since they're already shown in `ArcadeLobbyList`
  slots)? The user asked to *disable*, so default to disabled + clear label.
- Add a dedicated `Status.InYourParty` (clean label/colour) or just reuse the
  disabled tint with the existing `InLobby` label?

**Acceptance.** With A+B partied: in A's FriendsListPanel, B's row is visibly
non-clickable (disabled tint, optional "in your party" label); clicking does
nothing; when B leaves, B's row becomes invitable again. Symmetric on B's
panel for A.

---

## Task 2 — Online-panel refresh: panel-gated cadence 🔴

**Goal (user's intent).** Don't poll fast forever. Refresh fast while the
relevant panel (FriendsListPanel / ArcadeLobbyList) is open so the online +
lobby-status rows are live; slow the cadence when no such panel is open.

**⚠️ Critical caveat — "stop when closed" is unsafe as-is.** Invite
*detection*, party-member sync, the acceptance handshake, and the
joined-member scan **all run inside the same `RefreshAsync`** (see "The
refresh loop" above). If we stop the loop when the panel is closed:
- a recipient with the panel closed would **never detect an incoming invite**
  (the `OnInviteReceived` raise that auto-opens the panel happens *inside*
  the loop) — invites break;
- the host would not notice members joining/leaving;
- the accept handshake would stall.

So the loop must **keep running when closed, just slower** — not stop.

**Proposed approach.** Make the base cadence a function of "is a party/online
panel open":
- **Panel open →** fast cadence (e.g. ~1.0s, at the rate-limit floor) for
  live status. Reuse the existing boost machinery or add a sticky
  `SetForeground(bool)` that holds the fast interval while open (unlike
  `Boost()`'s fixed 15s window).
- **Panel closed →** slow cadence (e.g. 3-5s) — still polling so invites +
  party sync work, just cheaper. This is the lever that cuts idle SDK churn
  (B1/B6) and Relay/lobby cost.

Wiring: `FriendsListPanel` / `ArcadeLobbyList` already have `OnEnable` /
`OnDisable` and already call `ForceRefreshNow()` on open — add a
`HostConnectionService.SetOnlinePanelOpen(bool)` (single-writer-safe; the UI
calls it, HCS owns the scheduler) toggled from those same hooks. Guard for
two panels open at once (refcount, not a bool).

**Folds in existing TODOs.**
- `../PresenceSystem/TODOS.md` **TODO-P7** (gate the online diff on
  visibility) — the diff itself is cheap, but the cadence change covers its
  intent.
- `../PresenceSystem/TODOS.md` **TODO-P3** (refresh jitter ±10%) — add when
  changing cadence so multiple clients don't cluster reads on the same tick.

**"Decrease the refresh delay."** Base is already 1.5s, boosted 0.75s. The
UGS read cap is ~1/s; the *effective* read rate is higher than the tick
(refresh does a lobby read and often a session read, plus writes on change),
which is the source of B1/B6 churn. So "faster" is only safe up to the floor,
and only worth doing while a panel is open. Quantify against 429s in MPPM
before committing a number.

**Long-term alternative (out of scope here, noted).** The ROADMAP's
**push-based invites/presence** item (`../MultiplayerArchitecture/ROADMAP.md`)
would replace the invite/party-sync poll with lobby subscription events —
*that* would let us genuinely stop polling when closed. Until then, keep a
slow always-on poll.

**Open questions.**
- Which panels count as "open" for the fast cadence — FriendsListPanel only,
  or also ArcadeLobbyList (the party slots / "N Players Online")?
- Closed-state interval value (invite latency vs cost): 3s? 5s?
- Open-state interval value vs the 429 floor: 1.0s? keep 0.75s?

**Acceptance.** With a panel open, a remote player's status change ("ONLINE"
→ "IN LOBBY 2/4") and a newly-online player appear within ~1 refresh. With
all panels closed, an incoming invite still pops within the slow interval
(no missed invites), and idle SDK read volume drops measurably.

---

## Task 3 — Party-merge on accept (voluntary host migration) 🔴

**Goal.** Consider a party: host **A** + member **B**. Player **C** sees A
and B in its online panel as "IN LOBBY 2/4".
- If **C invites A (the host)** and A accepts → A joins C **and B is brought
  along** (B force-joins C). Result: **C is host**, party = {C, A, B}.
- If **C invites B (a member)** and B accepts → **only B** leaves A and joins
  C. Result: A's party = {A}, C's party = {C, B}.

Generalised: **a host accepting an invite migrates its whole party to the new
host; a non-host accepting moves alone.**

### Current behavior vs the gap

`AcceptInviteAsync` already does "leave my current session, join the
inviter's." So:

- **Non-host accept (C invites B) — likely already works, needs verification.**
  B is a guest in A's session (in the eager model B left its own session when
  it joined A). On accepting C, B's `LeaveAsync` leaves **A's** session and
  `JoinByIdAsync` joins C's; A's host-side `SyncFromSession` then drops B from
  A's roster. Net: B moves alone. ✅ in principle — must confirm a *guest*
  (not its own host) accepting cleanly leaves A's party and updates A's slots.

- **Host accept (C invites A) — NOT handled. This is the new work.** When A
  accepts, A leaves **A's own session (the one B is in)** and joins C's. **B
  is left in A's now-abandoned session** with no signal to follow. There is
  **no host-migration / "bring my members" path today** (the ROADMAP lists
  host-loss/migration as unaddressed). The cascade has to be built.

Evidence: `AcceptInviteAsync` leave-own→join-inviter (`HostConnectionService.cs`
~`:635-659`); host admits joiners via `joined_party` +
`ScanPresenceForJoinedPartyMembers` (~`:1321`); no consumer reads a
"migrate" signal anywhere.

### Design sketch (to be ratified before coding)

Branch the accept on `connectionData.IsPartyHost && RemotePartyMemberCount > 0`:

- **Member accept (not host, or solo host):** existing flow unchanged.
- **Host accept (cascade):** A must hand C's session id to each current
  member so they migrate. Candidate mechanisms (the presence lobby is the
  right vehicle — it's independent of the party session, so it survives A
  tearing its own session down):

  - **Option A — reuse `invite_payloads` + auto-accept.** Before leaving, A
    writes invite lines for each member (B) pointing at **C's** session, and
    members auto-accept (no UI) because the redirect comes from their current
    host. Pro: reuses all existing invite plumbing. Con: "A advertises C's
    session" is slightly odd; needs an auto-accept-when-already-partied path.
  - **Option B — dedicated `party_migrate` presence property (recommended to
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
the resulting roster changes propagate within ~1s, not 1.5-5s — otherwise the
merge feels broken (B lingers in a dead session; C's slots fill slowly).

### Open questions (must resolve before coding)

- **Vehicle:** Option A (reuse invites) vs Option B (`party_migrate` key) vs a
  session-broadcast. Recommend prototyping Option B.
- **Auto-accept policy:** members migrate **without** a prompt (user said
  "force join") — confirm. What if a member is mid-`PartyInviteController`
  transition, or is themselves hosting sub-members (3-level chains)? Cap to
  the 2-level model first.
- **Capacity:** C's party + A + B must fit `MaxPartySlots (4)`. If the merge
  would overflow, what happens (reject A's accept? partial merge? toast)?
- **Failure mid-cascade:** if B fails to join C, does B fall back to a fresh
  solo host (the existing bounce/`RecoverFromFailedTransitionAsync` path)? A
  is already in C — B must not get stuck in the dead session.
- **Netcode transition:** each migrating member runs the full PIC
  shutdown→join sequence; confirm N members migrating concurrently don't trip
  B5 (second-joiner) — Task 3 likely **blocked on B5** (`BUGS.md`).
- **State machine:** add explicit states (e.g. `MigratingParty`) vs reuse
  `JoiningParty`.

### Dependencies & acceptance

- **Depends on:** Task 2 (live refresh) and is **gated on B5** (multi-joiner
  reliability) — a host-accept cascade is exactly the concurrent-multi-join
  case B5 currently fails.
- **Acceptance.** {A,B} partied; C invites A; A accepts → within ~1-2s all of
  A & B are in C's party with C as host, no orphan vessel/session, every
  peer's panel shows {C,A,B} "IN LOBBY 3/4". Separately, C invites B; B
  accepts → B alone joins C, A's panel shows A solo. No console
  `[Invalid Destroy]` / stale-session errors; MPPM-verified per the S-series
  in `TESTS.md` (new cases to be added).

---

## Cross-references

- `ARCHITECTURE.md` — locked design, error-handling matrix, exit criteria.
- `BUGS.md` — **B5** (multi-joiner — Task 3 blocker), B8 (presence-vs-session
  authority — the cross-check Task 3 reuses).
- `../PresenceSystem/TODOS.md` — **P3** (jitter), **P7** (panel-gated diff) —
  folded into Task 2.
- `../PresenceSystem/ARCHITECTURE.md` — presence lobby, `joined_party` /
  `invite_payloads` property semantics.
- `../MultiplayerArchitecture/ROADMAP.md` — push-based invites (Task 2
  long-term), host-loss/migration (Task 3 is the *voluntary* sibling).
