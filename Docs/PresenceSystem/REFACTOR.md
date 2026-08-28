# Presence System — Refactor Backlog

`PresenceLobbyService` (`Assets/_Scripts/Controller/Party/Services/PresenceLobbyService.cs`)
is the remaining presence-side refactor target.

> **Per-commit risk gate.** Same protocol as the party-side refactors —
> see `../README.md` § "Per-commit refactor protocol" (canonical detail
> in `../PartySystem/REFACTOR.md`). The
> `../NetworkDiagnostics/ARCHITECTURE.md` overlay makes any regression
> interpretable.

## Refactor — `PresenceLobbyService`

**Why.** Lobby join/refresh/leave logic, including the false-positive
presence reconnect that triggered the YS2 catch-guard (commit
`a1a8eb9`). Currently mixes two distinct concerns:

1. **Am I still in the right lobby?** (membership monitor)
2. **Should I reconnect to a different lobby?** (reconnect decision)

These are entangled in `JoinOrCreateAsync` + `ConvergeToCanonicalAsync`
+ the host-side `RefreshAsync` watchdog. The watchdog escalates based
on error-count, but the *reason* to escalate (e.g. host saw the lobby
disappear from server-side) is implicit.

**Outline.** Extract two services:

| Extracted service | Responsibility |
|---|---|
| `LobbyMembershipMonitor` | "Am I in the right lobby?" — single source of truth for current membership. Reads `_activeLobby` + UGS state; exposes a `MembershipState` enum (`Active`, `StaleReference`, `RemovedFromLobby`, `LobbyDeleted`). |
| `LobbyRefreshScheduler` (already exists, scope expansion) | "When do I poll?" — keep the existing class but make the reconnect decision a pure function of `MembershipState`, not error-count alone. |

**Make the reconnect decision purely a function of monitor state — no
implicit reads of unrelated state.** Today's
`MAX_REFRESH_ERRORS_BEFORE_RECONNECT` threshold is a heuristic; the
membership monitor would give us a definite signal (the lobby actually
went away vs. the connection had transient errors).

**Pre-requisite signal.** Wait for NetDiag data from real MPPM runs to
tell us how often the existing watchdog escalates falsely vs. truly.
The NetDiag overlay (commit `aaba872`) added classification to every
`PresenceLobbyService` catch — `class=Offline` vs `class=SessionGone`
vs `class=Transient` will tell us what the watchdog should actually
escalate on.

## Cross-system concern — coordinate with `PartySystem/REFACTOR.md`

`PresenceLobbyService` and `PartySessionService` both have retry
classifiers. After their independent refactors, the cross-class
refactor in `../PartySystem/REFACTOR.md` ("Cross-class refactor")
makes `leave → reset → join` a single owned operation across both
services. Don't fold that cross-class work into the
`PresenceLobbyService` refactor — sequence it after both internal
refactors stabilize.

## Sequencing

1. **Diagnostics first (DONE, commit `aaba872`).** NetDiag overlay
   added so the refactor can be planned against real log data.
2. **Wait for data.** Run MPPM smokes over the next few iterations.
   NetDiag classes from `PresenceLobbyService` catches indicate
   which membership-loss flavors are most common.
3. **Plan the extraction.** Specifically, decide what membership
   states to model. The four candidates above (`Active`,
   `StaleReference`, `RemovedFromLobby`, `LobbyDeleted`) are a
   starting point; the data may suggest a different cut.
4. **Extract `LobbyMembershipMonitor`.** One commit, behavior-
   preserving.
5. **Rewire the reconnect decision.** Replace the error-count
   threshold with a `MembershipState`-driven decision. One commit.
6. **Cross-class refactor (later, per `../PartySystem/REFACTOR.md`).**

## Deferred items touching presence (owned by `../PartySystem/REFACTOR.md`)

Two deferred items from the 17-commit pass touch the presence-lobby refresh
path. They are tracked in `../PartySystem/REFACTOR.md` (Deferred items) but
coordinate here:

- **D1 — PENDING-sentinel protocol removal.** `LobbyRefreshScheduler`'s
  PENDING-republish boost window is presence-side. Removing the three-phase
  acceptance protocol changes when the presence lobby fast-refreshes.
- **D5 — event-driven `EnsureInitializedAsync`.** Adds a `JoiningPresenceLobby`
  state to `PartyStateMachine` — the presence-lobby join step becomes an
  observable state transition instead of an inline await.

## Per-refactor commit cadence

Same cadence and 6-step "read source fresh" revision protocol as the
party-side refactors — canonical in `../PartySystem/REFACTOR.md`
(§ "Per-refactor commit cadence" + § "Per-commit revision protocol"),
summarized in `../README.md` § "Per-commit refactor protocol". 3-VP MPPM
smoke per commit (see `TESTS.md`).

## Related docs

- `ARCHITECTURE.md` — current state
- `BUGS.md` — open bugs to consider during refactor
- `TESTS.md` — manual test procedures
- `../PartySystem/REFACTOR.md` — companion refactor backlog for the party (Relay) layer
- `../NetworkDiagnostics/ARCHITECTURE.md` — diagnostic overlay every refactor commit ships behind
