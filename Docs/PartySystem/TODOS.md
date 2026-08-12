# Party System — TODOs

Parking lot for minor improvements that don't rise to a refactor commit
or a bug. Each entry has enough context that it can be picked up cold.

> **Looking for the big-picture “what should I work on next?”** — the
> cross-cutting roadmap (host-loss resilience, multi-joiner reliability,
> push-vs-poll, scale/cost, production observability, CI) plus the
> strengths-to-preserve invariants live in
> `../MultiplayerArchitecture/ROADMAP.md`. This file is the granular
> party-side parking lot beneath it.

## Priority as of 2026-08-06 (post roster-truth branch)

The party system is **working and owner-verified** at 4 VPs: invite, accept,
party formation, panel agreement. Nothing below is urgent. Ordered by
value-per-risk, and every one of them is a *separate branch* — the roster-truth
branch's lesson (`REFACTOR.md` § "Read this before the next branch here") is that
bundling is what hurts here.

| | Item | Why now | Risk |
|---|---|---|---|
| 1 | **TODO-P2** — coalesce startup property writes (`../PresenceSystem/TODOS.md`) | The single highest-value lever on the B1/B6 stale-index defect, backed by two measurements. Partly paid down by `4129b932`; the startup burst itself is still unbatched. Unblocks the safety-poll relaxation. | medium |
| 2 | **TODO-10** — prove the PENDING sentinel dead, then delete | Removes a whole phase of the accept handshake. Instrument-first, so the risky half is gated on evidence. | low if instrumented first, high if not |
| 3 | **R1 (`PartyInviteController`)** — the long-standing top refactor | Still the most complex orchestrator in the system; NetDiag data now exists to plan against. | medium |
| 4 | **TODO-13** — tighten `IsHostConflictException` | One-line class of bug that already bit us once as the rate-limit-vs-benign ordering error. | low |
| 5 | **B12 graceful-leave retest** (`../PresenceSystem/BUGS.md`) | The named-id eviction path has never been exercised by a test; needs TODO-P10's editor hook. | low |
| 6 | **TODO-11 / TODO-12** — join-detection and invite-clearing consolidation | Cosmetic until measured; TODO-11 needs instrumentation to be safe at all. | medium |
| — | Safety poll 1.5 s → 10 s | ⛔ still **BLOCKED** on TODO-P2. Re-measure the skip counters first. | — |
| — | Presence heartbeat | ⛔ **CLOSED**, costed and rejected (TODO-P8). | — |

**Standing rule for all of the above:** follow the verification order at the top
of `TESTS.md`, cheapest gate first, and stop on the first failure. Step 2 —
single-editor play mode watching for `EnsureRunningOnMainThread` — is mandatory
on any commit that adds or moves a SOAP raise.

## Invite-system cleanup — audited 2026-08-06, NOT executed

Recorded during the status-cleanup pass. The invite flow is **working**
(4-VP MPPM: invite + accept green), so none of this is urgent, and none of
it should be bundled with unrelated work — the last time a "safe cleanup"
rode along with a structural change it shipped a threading regression
(`../PresenceSystem/BUGS.md` B15).

Each entry states the evidence AND what breaks if the evidence is wrong.

### TODO-10. Prove the PENDING sentinel is dead, then delete it — ⬆ best first pick

**Evidence it is dead.** Every reference to `PartyLobbyKeys.PendingSessionId`
either *patches an existing* PENDING entry
(`InviteService.cs:176-177`, `UpdatePayloadsWithRealSessionId`) or *checks a
value is not* PENDING (`AcceptanceSignalService.cs:294`). A grep found **no
site that writes PENDING into a payload**. That is exactly what the locked
EAGER per-user Relay design predicts: every player hosts a session from menu
entry, so `SendInviteAsync` always has a real `ActiveSession.Id` before it
publishes. This is `REFACTOR.md` **D1**, now with code evidence behind it.

**What would become dead:** `InviteService.UpdatePayloadsWithRealSessionId`,
`AcceptanceSignalService.WaitForRealSessionIdAsync` (a 400 ms poll with a 7 s
timeout) and `RepublishWithRealIdAsync`, plus the unused
`HostConnectionService.PENDING_SESSION_ID` constant and
`PartyLobbyKeys.PendingSessionId` itself. That collapses the acceptance
handshake from three phases to two.

**Do NOT delete on the grep alone.** It rests on reading, not on a run, and
it sits inside the accept path that only just became reliable.

**Method — instrument first, delete second.** Ship one commit that logs
`Debug.LogError` if a PENDING payload is ever constructed, and adds a
`WaitForRealSessionIdAsync` entry log. Play a few multi-VP sessions
including the races that motivated the sentinel (two clients accepting the
same invite near-simultaneously; an invite sent during
`EnsurePartySessionAsync`). Only if neither log ever fires, delete the
machinery in a second commit. Cheap, and it converts a code-reading argument
into evidence.

**If the evidence is wrong:** an invite published before the Relay session
exists would carry a literal `"PENDING"` as its session id, and the accepting
client would try to join a session by that name and fail. Symptom would be an
accept that bounces to solo.

### TODO-11. Map the four join-detection paths, retire only what is provably redundant

A party join is currently detected four different ways:

| | Path | Reads |
|---|---|---|
| a | `TryFindIncomingInvite` | `invite_payloads` (presence lobby) |
| b | `AcceptanceSignalService.ScanForSignals` | `accepted_invite` (presence lobby) |
| c | `ScanPresenceForJoinedPartyMembers` (host only) | `joined_party` (presence lobby) |
| d | `PartyMemberService.SyncFromSession` | the party **session** roster |

They look redundant. **At least one pairing is not**: (c) cross-checks
`joined_party` against (d)'s session roster, and that cross-check *is* the
B8 fix for the host-side phantom-rejoin loop
(`BUGS.md` B8 — `RaisePartyMemberLeft`/`Joined` oscillating forever at ~3 s).
Removing (c) or its cross-check reintroduces a shipped bug.

(d) also gained a push channel in `090f61a6`, so it is now the fastest path
and the others are increasingly backstops rather than mechanisms. That is an
argument for *documenting* the hierarchy, not for cutting yet.

**Method.** Add a one-line `CSDebug` to each path naming which one first
observed a given join, run multi-VP sessions, and count. A path that never
wins in any session over several runs is a deletion candidate — with (c)'s
cross-check explicitly excluded from consideration.

### TODO-12. Consolidate the invite-clearing paths

`ClearOutgoingInviteIfPresentAsync` is called with at least four distinct
reasons (`"presence-join"`, `"presence-leave"`, `"party-join"`,
`"party-leave"`), alongside `CancelInviteAsync` and the
`OUTGOING_INVITE_TIMEOUT_SECONDS` expiry sweep in `ExpireOutgoingInvites`.
Several of these can fire for the same invite in quick succession.

It is not known to be *buggy* — the operations are idempotent — but it is
more mechanisms than the problem needs, and the reason strings suggest the
call sites were added one bug at a time. Worth a read-through to see whether
"the target is no longer invitable" can be expressed once rather than at each
discovery point. **Low priority; no known symptom.**

### TODO-13. `IsHostConflictException` is far too broad

`PartySessionService.cs` matches any exception whose message contains
`"host"` (case-insensitive), which will swallow unrelated failures — any
message mentioning a hostname, "host unreachable", etc. — into the
host-conflict retry. Noted while auditing; not observed causing a problem,
but it is the kind of over-broad classifier that produced the
rate-limit-vs-benign ordering bug (`../PresenceSystem/BUGS.md` B15 RC2).
Tighten to the specific NetworkManager-still-shutting-down signature.

## Code health

### TODO-1. Remove `HostConnectionService.Instance` static accessor

**Why.** After Refactor 1 (PIC, see `REFACTOR.md`) migrates PIC to
`[Inject] IHostConnectionService`, the only remaining consumer of the
`Instance` static accessor is `PartyInviteSystemTests.cs:1067`
(reflection). When that test migrates to DI, the static can be
deleted.

**Touchpoint.** `Assets/_Scripts/Controller/Party/HostConnectionService.cs`
(`Instance` static property + `Awake` assignment + `OnDestroy`
unassignment).

**Risk.** Low if done after the test migration. Anything else still
reading `HostConnectionService.Instance` would surface at compile time.

### TODO-2. `Docs/PARTY_OPEN_BUGS.md` reference updates — DONE

**Status.** Resolved. The old file is deleted; its 7 bugs were split into
`Docs/PartySystem/BUGS.md` (B2, B3, B5, B7) and
`Docs/PresenceSystem/BUGS.md` (B1, B4, B6). The codebase had 0 inline
references to `PARTY_OPEN_BUGS` (verified by grep), so no code comments
needed updating.

### TODO-3. `Docs/PARTY_SYSTEM_REFACTOR.md` reference updates — DONE

**Status.** Resolved. The old file is deleted; its content was migrated in
full (locked design, investigation Q&A, error-handling matrix, exit
criteria → `ARCHITECTURE.md`; per-commit protocol + deferred items D1-D5 →
`REFACTOR.md`). All 20 inline code comments across 9 files were repointed to
`Docs/PartySystem/ARCHITECTURE.md` (with the specific Q-anchor where the
comment referenced one). Verified 0 remaining `PARTY_SYSTEM_REFACTOR`
references in `Assets/`.

## Diagnostics

### TODO-4. Adopt NetDiag in non-party catch sites → see NetworkDiagnostics

Canonical: `../NetworkDiagnostics/TODOS.md` § "TODO-2. Broader adoption
— non-party UGS catches" (pattern + candidate site list:
`AuthenticationServiceFacade`, `FriendsServiceFacade`, PlayFab, IAP,
leaderboards-write). Not duplicated here.

## UI / UX (deferred — needs design pass)

### TODO-5. Specific toast messages per NetDiag class

**Why.** Today's bounce-to-solo-menu always shows "Couldn't join —
returned to your menu." Once NetDiag log data tells us which classes
fire in practice, we can pick which classes deserve a specific message.

**Candidate matrix (sketch — needs design sign-off):**

| Class | Possible specific toast |
|---|---|
| `Offline` | "Internet connection lost — returned to your menu" |
| `SessionGone` | "Host left the party" |
| `Cancelled` (user-driven) | Suppress toast entirely |
| `RateLimit` | Generic "Couldn't join — please try again in a moment" |
| `AuthRequired` | "Signed out — please log in again" |
| `Transient` | Generic (existing) |
| `Unknown` | Generic (existing) — also a signal to extend `ClassifyException` |

**Touchpoint.** `PartyInviteController.RecoverFromFailedTransitionAsync`
or its successor `TransitionRecoveryService` after Refactor 1.

### TODO-6. Auto-dismiss stale invites on SessionGone

**Why.** When `class=SessionGone` is observed during an Accept, the
invite the user is trying to accept is provably stale. The invite
notification panel could auto-dismiss the matching entry so the user
isn't tempted to retry.

**Touchpoint.** New SOAP event `OnInviteSessionGone(sessionId)` raised
from the bounce path; `PartyInviteNotificationPanel` subscribes and
removes the matching entry.

### TODO-7. Invite freshness window

**Why.** A short timestamp on outgoing invites lets the receiver refuse
to display invites older than N seconds, reducing incidence of
SessionGone-on-accept.

**Touchpoint.** `invite_data` lobby player property already exists;
extending it with a timestamp field is additive.

**Risk.** Clock skew between MPPM VPs and across real devices. Use a
generous window (e.g. 60 s) to avoid false rejections.

## Performance / polish

### TODO-8. Coalesce startup property writes → see PresenceSystem

This is a presence-lobby write-path concern (the `LobbyPropertyWriter`
startup churn that contributes to B1). Tracked canonically in
`../PresenceSystem/TODOS.md` § "TODO-P2. Coalesce startup property
writes" — not duplicated here.

### TODO-9. Document `LobbyRefreshScheduler.Boost()` semantics

**Why.** Boost is called on invite-receive to tighten the refresh
cadence so the joiner sees state changes faster. The exact timing
(`+15 s`, `2 s` interval) is encoded in the scheduler but not
documented.

**Touchpoint.** Inline doc comment on `LobbyRefreshScheduler.Boost()`
or short note in `ARCHITECTURE.md`.
