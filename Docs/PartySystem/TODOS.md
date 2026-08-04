# Party System — TODOs

Parking lot for minor improvements that don't rise to a refactor commit
or a bug. Each entry has enough context that it can be picked up cold.

> **Looking for the big-picture “what should I work on next?”** — the
> cross-cutting roadmap (host-loss resilience, multi-joiner reliability,
> push-vs-poll, scale/cost, production observability, CI) plus the
> strengths-to-preserve invariants live in
> `../MultiplayerArchitecture/ROADMAP.md`. This file is the granular
> party-side parking lot beneath it.

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
