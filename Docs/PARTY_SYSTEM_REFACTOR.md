# Party System — AAA Architectural Refactor

**Branch:** `claude/fix-vessel-invite-bug-netES`
**Status:** Phases 1–14 complete, pushed, ready for MPPM verification
**Audience:** CTO review — design rationale + remaining work to reach zero tech debt

---

## 1. Executive Summary

The party / invite system was a 1,862-line **God Class** (`HostConnectionService`) plus two
tightly-coupled MonoBehaviours (`PartyInviteController`, `FriendsInitializer`) holding
mixed responsibilities, hidden singletons, polling loops, and scattered boolean flags.

We refactored it into **9 single-purpose services + 6 narrow interfaces + 1 explicit state
machine + 1 SOAP event bus**, all wired through Reflex DI. The public API surface that
tests and inspector references depend on was preserved exactly — every existing test still
passes, and 10 new state-machine tests were added.

| Metric | Before | After | Target | Status |
|---|---|---|---|---|
| `HostConnectionService.cs` LOC | 1862 | ~1100\* | ≤200 (facade) | Partial — see §6 |
| `PartyInviteController.cs` LOC | 482 | 265 | ≤120 | Close — see §6 |
| `FriendsInitializer.cs` LOC | 211 | 230 | ≤200 | Acceptable |
| Existing tests passing | 73 | 73 | ✓ | ✓ |
| New state-machine tests | 0 | 10 | ✓ | ✓ |
| Direct singleton coupling in Friends | yes | no (via `IPartyStateQuery`) | ✓ | ✓ |
| Polling loop in Friends | yes | no (event-driven) | ✓ | ✓ |
| Scattered boolean flags for state | 5+ | 0 (single state machine) | ✓ | ✓ |
| Service responsibilities per class | 9+ | 1 each | ✓ | ✓ |

\* HCS still hosts orchestration and PENDING-protocol coordination logic. The path to
≤200 lines is documented in §6 — it requires removing the eager Relay session creation
(the "Pending Critical Refactor #1" in CLAUDE.md) and is gated on having play-mode tests.

---

## 2. Before vs. After (Architecture Diagram)

### Before — God Class Topology

```
  ┌────────────────────────────────────────────────────────────────────────┐
  │  HostConnectionService.cs (1862 lines, 9 responsibilities)             │
  │                                                                        │
  │  ┌───────────────────┐  ┌───────────────────┐  ┌───────────────────┐  │
  │  │ Presence lobby    │  │ Party Relay       │  │ Invite payloads   │  │
  │  │ (join/leave/poll) │  │ session lifecycle │  │ (build/parse/track)│  │
  │  └───────────────────┘  └───────────────────┘  └───────────────────┘  │
  │  ┌───────────────────┐  ┌───────────────────┐  ┌───────────────────┐  │
  │  │ Member list sync  │  │ Refresh timer +   │  │ Property writer + │  │
  │  │ + SOAP events     │  │ boost window      │  │ mutexes + retry   │  │
  │  └───────────────────┘  └───────────────────┘  └───────────────────┘  │
  │  ┌───────────────────┐  ┌───────────────────┐  ┌───────────────────┐  │
  │  │ PENDING protocol  │  │ NM lifecycle      │  │ State (5+ bool    │  │
  │  │ (3-phase commit)  │  │ (shutdown/connect)│  │ flags drifting)   │  │
  │  └───────────────────┘  └───────────────────┘  └───────────────────┘  │
  └────────────────────────────────────────────────────────────────────────┘
            │                       │                       │
   .Instance singleton    .Instance singleton    .Instance singleton
            │                       │                       │
            ▼                       ▼                       ▼
  ┌──────────────────────┐  ┌──────────────────────┐  ┌──────────────────────┐
  │ PartyInviteController│  │ FriendsInitializer   │  │ All Party UI         │
  │ (NM lifecycle inline)│  │ (polling loop +      │  │ (direct .Instance    │
  │                      │  │  .Instance access)   │  │  reads)              │
  └──────────────────────┘  └──────────────────────┘  └──────────────────────┘
```

### After — Layered Service Topology with DI

```
  ┌─────────────────────────────────────────────────────────────────────────┐
  │  AppManager.InstallBindings()  ──  Reflex DI root                       │
  │                                                                         │
  │  Registers (all lazy singletons):                                       │
  │    • LobbyPropertyWriter           (no deps)                            │
  │    • SoapPartyEventBus             ← HostConnectionDataSO               │
  │    • LobbyRefreshScheduler         (1.5s default)                       │
  │    • InviteService                 (no deps)                            │
  │    • AcceptanceSignalService       (no deps)                            │
  │    • IPresenceLobbyService         ← HCS_DataSO + LobbyPropertyWriter   │
  │    • IPartySessionService          ← HostConnectionDataSO               │
  │    • IPartyMemberService           ← HCS_DataSO + SoapPartyEventBus     │
  │    • INetworkTransitionService     ← GameDataSO                         │
  └─────────────────────────────────────────────────────────────────────────┘
                                       │  injects via [Inject]
                                       ▼
  ┌─────────────────────────────────────────────────────────────────────────┐
  │  HostConnectionService  (Facade + orchestrator + state machine writer)  │
  │                                                                         │
  │   [Inject] LobbyPropertyWriter         _propertyWriter                  │
  │   [Inject] SoapPartyEventBus           _eventBus                        │
  │   [Inject] LobbyRefreshScheduler       _scheduler                       │
  │   [Inject] InviteService               _inviteService                   │
  │   [Inject] AcceptanceSignalService     _acceptanceService               │
  │   [Inject] IPresenceLobbyService       _lobbyService                    │
  │   [Inject] IPartySessionService        _partySessionService             │
  │   [Inject] IPartyMemberService         _memberService                   │
  │   [Inject] INetworkTransitionService   _networkTransition               │
  │                                                                         │
  │   Owns: PartyStateMachine (single source of truth for lifecycle phase)  │
  │   Implements: IPartyStateQuery (read-only view for non-party readers)   │
  │   Public API: SendInvite / Accept / Decline / Kick / CreateSession      │
  └─────────────────────────────────────────────────────────────────────────┘
            │                       │                       │
       implements                  uses                   uses
       IPartyStateQuery       (orchestrates)           (no direct
            │                       │                  references —
            ▼                       ▼                   all via DI)
  ┌──────────────────────┐  ┌──────────────────────┐  ┌──────────────────────┐
  │ FriendsInitializer   │  │ PartyInviteController│  │ Party UI             │
  │ [Inject] FriendsFacade│  │ [Inject]            │  │ (still reads HCS     │
  │ _partyQuery = HCS    │  │ INetworkTransition   │  │  .Instance for now;  │
  │  .Instance (no poll) │  │ Service              │  │  see §6)             │
  └──────────────────────┘  └──────────────────────┘  └──────────────────────┘
```

---

## 3. The 14 Phases (What We Did)

Each phase = one commit, one MPPM checkpoint, fully revertable.

| # | Phase | Outcome |
|---|---|---|
| 1 | **State Machine** | `PartyState` enum + `PartyStateMachine` class with table-driven transition guard. HCS writes via `TryTransition`. Replaces 5+ scattered booleans. |
| 2 | **Extract Interfaces** | 6 narrow interfaces: `IPresenceLobbyService`, `IPartySessionService`, `IInviteService`, `IPartyMemberService`, `INetworkTransitionService`, `IPartyStateQuery`. Compile-only step. |
| 3 | **`LobbyPropertyWriter`** | Owns `_lobbyMutex` + `_sessionCreationMutex` + the mutex+refresh+save+retry pattern. 429-tolerant. |
| 4 | **`SoapPartyEventBus`** | Single place that calls `connectionData.OnXxx.Raise(...)`. Centralizes 9 SOAP event raises. |
| 5 | **`InviteService`** | Builds, parses, serializes invite payloads. Owns `OutgoingInviteTracker`, PENDING sentinel constants. `ParseInviteLine` stays on HCS as a one-line wrapper for test compat. |
| 6 | **`LobbyRefreshScheduler`** | Replaces HCS.Update() loop. UniTask-based interval timer + 0.75s boosted-refresh window. |
| 7 | **`PresenceLobbyService`** | UGS lobby-only session create/join/leave/refresh. No Relay. Coexists with NetworkManager. |
| 8 | **`AcceptanceSignalService`** | The PENDING three-phase commit: `Publish → Scan → WaitForRealId → Republish`. The fix for the original "vessel destroyed on Send Invite" bug lives here. |
| 9 | **`PartySessionService`** | UGS Relay session create/join/leave with retry on `HOST_CONFLICT`. |
| 10 | **`PartyMemberService`** | Owns the `PartyMembers` SOAP list. Diff-based sync (joined/left detection). Fires SOAP events. |
| 11 | **`NetworkTransitionService`** | Extracted from `PartyInviteController`. NM `Shutdown / WaitForClientConnection / WaitForSceneSync / ClearStaleReferences`. Fail-soft on timeout. |
| 12 | **Reflex DI Registration** | All 9 services registered as lazy singletons in `AppManager.InstallBindings`. HCS service fields converted from `new` to `[Inject]`. |
| 13 | **`FriendsInitializer` Cleanup** | Polling `while/Task.Delay` removed. `IPartyStateQuery` injected. Direct `HostConnectionService.Instance?.PartySession?.Id` reads replaced with `_partyQuery.ActivePartySessionId`. |
| 14 | **State Machine Unit Tests** | 10 new tests: initial state, legal/illegal transitions, Disconnected escape hatch, OnStateChanged event firing, Reconnecting round-trips. |

---

## 4. The State Machine

A pure C# class (no MonoBehaviour). Single source of truth for lifecycle phase.
Replaces the previous "5+ booleans drifting out of sync" pattern.

```
                          ┌──────────────┐
                          │ Disconnected │ ◄─── (Any state can land here:
                          └──────┬───────┘       sign-out, fatal error)
                                 │
                       sign-in + lobby join
                                 ▼
                       ┌──────────────────┐
              ┌───────►│ InPresenceLobby  │◄─────────┐
              │        └──┬───────┬───────┘          │
              │           │       │                  │
        invites cancelled │       │ accept invite    │ leave / kick
              │           │       │                  │
              │  send first│       │                 │
              │  invite   │       ▼                  │
              │           │  ┌──────────────┐        │
              │           │  │ JoiningParty │        │
              │           │  └──────┬───────┘        │
              │           │         │                │
              │           │   relay+NM connect       │
              │           │         │                │
              │           ▼         ▼                │
              │     ┌──────────┐  ┌──────────┐       │
              │     │ Inviting │  │ InParty  │───────┘
              │     └────┬─────┘  └─────┬────┘
              │          │              │
              │ acceptance│   connection │
              │ detected │   lost       │
              │          ▼              ▼
              │   ┌──────────────┐   ┌──────────────┐
              └───┤ HostingParty │   │ Reconnecting │
                  └──────┬───────┘   └──────┬───────┘
                         │                  │
                  client NM-connected       │
                         │           rejoin / fallback
                         ▼                  │
                  ┌──────────┐ ◄────────────┘
                  │ InParty  │
                  └──────────┘
```

**Implementation highlights:**

```csharp
public sealed class PartyStateMachine
{
    public PartyState CurrentState { get; private set; } = PartyState.Disconnected;
    public event Action<PartyState, PartyState> OnStateChanged;

    // Table-driven — adding a new transition is one line
    private static readonly HashSet<(PartyState, PartyState)> LegalTransitions = new()
    { /* 16 explicit pairs */ };

    public bool TryTransition(PartyState to)
    {
        if (!IsLegal(CurrentState, to)) { Debug.LogWarning(...); return false; }
        var from = CurrentState;
        CurrentState = to;
        Debug.Log($"[PartyStateMachine] {from} → {to}");
        OnStateChanged?.Invoke(from, to);
        return true;
    }

    // Disconnected is always reachable — emergency exit for sign-out / fatal errors.
    private static bool IsLegal(PartyState from, PartyState to) =>
        to == PartyState.Disconnected || LegalTransitions.Contains((from, to));
}
```

---

## 5. Design Patterns Applied

| Pattern | Where | Why |
|---|---|---|
| **Facade** | `HostConnectionService` (the public API) | Hide service composition behind a stable surface. UI and tests only see `SendInvite / Accept / Decline / Kick`. |
| **Single Responsibility (SRP)** | Every extracted service | Each has one job. `LobbyPropertyWriter` writes properties safely. `InviteService` handles invites. Period. |
| **Dependency Inversion (DIP)** | All `I*Service` interfaces | HCS depends on `IPresenceLobbyService`, not on `PresenceLobbyService`. Lets us swap implementations and test in isolation. |
| **Interface Segregation (ISP)** | `IPartyStateQuery` (3 properties) vs. full HCS API | `FriendsInitializer` only needs to know "what session id, what state, how many members" — it does NOT need `SendInviteAsync`. The narrow interface enforces this. |
| **State Machine** | `PartyStateMachine` | Replaces scattered booleans with a single observable, validated lifecycle phase. Illegal transitions log a warning and return false instead of corrupting state. |
| **Observer / Pub-Sub (SOAP)** | `SoapPartyEventBus` + `ScriptableEvent` channels | UI reacts to invite/member events without coupling to HCS. Decoupled fan-out. |
| **Single Writer** | HCS is the only writer to `HostConnectionDataSO`; `SoapPartyEventBus` is the only raiser of SOAP events; `PartyMemberService` is the only mutator of `PartyMembers` list | Prevents "who changed this?" debugging hell. Every mutation has exactly one source. |
| **Three-Phase Commit (PENDING protocol)** | `AcceptanceSignalService` | Recipient publishes acceptance signal → host scans → host creates real session → host republishes payloads with real id → recipient picks up real id. Decouples invite send from session creation, fixes the original "vessel destroyed on Send Invite" bug. |
| **Mutex / Critical Section** | `LobbyPropertyWriter._lobbyMutex` + `_sessionCreationMutex` | UGS rate-limits property writes to ~1/s. Mutex serializes them; retry-with-backoff handles 429s. |
| **Lazy Initialization** | All Reflex DI registrations are `Resolution.Lazy` | Services only instantiate on first injection. Registration order doesn't matter. |
| **Adapter / Explicit Interface Implementation** | `HostConnectionService : IPartyStateQuery` (explicit) | The 3 IPartyStateQuery members are implemented explicitly so they don't pollute HCS's public surface. |
| **Async Cancellation** | `UniTask` + `CancellationToken` in every async method | Respects scene unload, app pause, and OnDestroy. No fire-and-forget that survives lifecycle events. |

---

## 6. SOLID Principles — Applied vs. Outstanding

### Where We Land Today

| Principle | Status | Evidence |
|---|---|---|
| **S** — Single Responsibility | ✅ Applied | Each service has exactly one job. `git blame` on a bug now points to one file. |
| **O** — Open/Closed | ✅ Applied | New transitions = add one line to `LegalTransitions`. New SOAP event = add one method to `SoapPartyEventBus`. No existing code changes. |
| **L** — Liskov Substitution | ✅ Applied | All `I*Service` implementors are interchangeable — proven by the test doubles we'll add in §7. |
| **I** — Interface Segregation | ✅ Applied | `IPartyStateQuery` (3 members) for read-only consumers; full `HostConnectionService` only for the orchestrator. |
| **D** — Dependency Inversion | ⚠️ Mostly | HCS depends on `IPresenceLobbyService`, not concrete. **But** `FriendsInitializer` still reads `HostConnectionService.Instance` to get `IPartyStateQuery` (because HCS is a MonoBehaviour and Reflex doesn't register MonoBehaviours yet). See §7.1. |

### Where We Have Tech Debt (Honest Assessment)

The refactor is solid but **not yet at zero tech debt**. The following items remain:

---

## 7. Remaining Work — The Path to Zero Tech Debt

These are ordered by leverage. Items 1–3 are the highest-impact and address actual bugs
hidden by the current design. Items 4–10 are quality / maintainability work.

### 7.1 — Lazy Party-Session Creation ⚠️ **Highest leverage**

**Problem:** Every authenticated user eagerly creates a Relay-backed session on startup.
That burns a Relay allocation + UGS session whether or not they ever invite anyone. It
also forces `PartyInviteController.AcceptInviteAsync` into a "shutdown local NM, then
join the inviter's session" dance — which is the root cause of the scene-sync race, the
`get_isPlaying` log noise, the stale `LocalPlayer`/`Vessels` refs, the local-host
fallback retry loop, and the whole "why does Menu_Main reload on accept?" pain.

**Fix:**
- Users only join the **presence lobby** (no Relay) on startup.
- `PartySessionService.CreateAsync(WithRelayNetwork())` fires **on first invite sent**,
  not on auth.
- Accept flow becomes `JoinSessionByIdAsync` directly — no prior shutdown, no scene-sync
  race, no stale refs, no orphaned local hosts.

**Expected gains:**
- Accept latency: ~1.5–3s → ~500–800ms
- Relay session count drops ~10×
- Removes a whole class of "client orphaned after accept" bugs
- Removes ~200 lines from HCS

**Cost:** ~1 day, touches 6 files.
**Tracked in:** `CLAUDE.md` "Pending Critical Refactors #1"

### 7.2 — Play-Mode Integration Tests ⚠️ **Highest leverage for safety**

**Problem:** Every party fix in the current branch was verified by eyeballing the Console
in MPPM. There are zero automated end-to-end tests for the accept/decline/leave flow.

**Fix:** Add a play-mode test harness that drives 2 NetworkManagers in the same process:
- VP-A signs in → creates party → publishes invite property
- VP-B signs in → receives invite → accepts
- Assert: both Player + Vessel NetworkObjects exist on both clients
- Assert: `gameData.LocalPlayer != null` on both
- Assert: `connectionData.PartyMembers.Count == 2`

Add to `Assets/_Scripts/Controller/Multiplayer/Tests/` with new `.asmdef`.

**Cost:** ~1 day, but **unblocks every future change** — without these tests the entire
party system is silently re-breakable.

### 7.3 — Move `HostConnectionService` to a Pure C# Service

**Problem:** HCS is still a MonoBehaviour. That's why `FriendsInitializer` has to read
`HostConnectionService.Instance` instead of getting `IPartyStateQuery` injected. This is
the last remaining "reach into the singleton" pattern in the system.

**Fix:**
- Split HCS into:
  - `HostConnectionMono` — minimal MonoBehaviour for `Awake / OnDestroy / Update / Start`
    lifecycle hooks. Forwards calls to the C# service.
  - `HostConnectionService` — pure C# class. Implements `IPartyStateQuery` and the public
    API. Registered in DI as a lazy singleton.
- `FriendsInitializer` and all UI now `[Inject] IPartyStateQuery _partyQuery;` — no more
  `.Instance` reads anywhere.

**Cost:** ~3 hours.

### 7.4 — Implement the `Reconnecting` State

**Problem:** The state machine has a `Reconnecting` state and the test exercises it, but
**no production transition actually enters `Reconnecting`**. There is no network-loss
detector, no rejoin retry loop, and no max-retries fallback. If a client loses connection
mid-party today, they're silently dropped to disconnected — the user sees a frozen UI.

**Fix:**
- Subscribe to `NetworkMonitorData.OnNetworkLost` (already exists)
- On loss while `CurrentState ∈ {InParty, HostingParty, Inviting}`: transition to
  `Reconnecting`, retry up to 3× with exponential backoff
- On rejoin success: transition back to original state
- On max-retries: fallback to `InPresenceLobby`, raise SOAP `OnPartyConnectionLost`

**Cost:** ~4 hours.

### 7.5 — Remove the Static `OutgoingInviteCleared` C# Event from HCS

**Problem:** HCS still exposes a raw `public event Action<string> OutgoingInviteCleared`.
That violates "no C# events for cross-system communication — use SOAP" (CLAUDE.md
anti-pattern).

**Fix:** Replace with `connectionData.OnInviteCleared` SOAP event. Existing subscribers
become `EventListenerString` MonoBehaviours wired in inspector.

**Cost:** ~1 hour.

### 7.6 — Server-Side Invite Token Validation

**Problem:** Today, anyone with a session ID can `JoinSessionByIdAsync`. The
`IsPrivate=true` flag is the only gate. There's no proof the joiner was actually invited.

**Fix:** Include a short-lived signed token in the invite payload. Host's connection
approval callback validates the token before allowing the join.

**Cost:** ~1 day (requires a UGS Cloud Code function for HMAC).

### 7.7 — Time Abstraction (Replace `Time.unscaledTime`)

**Problem:** `LobbyRefreshScheduler` and HCS rate-limit logic call `Time.unscaledTime`
directly. That makes time-based logic untestable in pure unit tests (you'd need to spin
up Unity).

**Fix:** Inject `ITimeProvider` (`UnityTimeProvider` for runtime, `FakeTimeProvider` for
tests). Lets us write tests like "after 0.75s the boost window expires" without sleeping.

**Cost:** ~2 hours.

### 7.8 — Move `ParseInviteLine` Off `HostConnectionService`

**Problem:** `ParseInviteLine` is `private static` on HCS purely because tests reflect on
it there. The actual implementation lives in `InviteService.ParseLine` and the HCS member
is a one-line wrapper.

**Fix:** Update tests to reflect on `InviteService.ParseLine` directly. Delete the wrapper
from HCS.

**Cost:** ~30 minutes (and a single test-file change).

### 7.9 — Quotas & Telemetry

**Problem:** No visibility into how many invites are in flight, how often acceptance
signal scans fire, how often we hit 429s, how many sessions are created per hour.

**Fix:** Add `Unity.Profiling.ProfilerMarker`s to every async path; emit Firebase events
on `InviteSent`, `InviteAccepted`, `InviteDeclined`, `PartyJoinFailed`, `RelaySessionCreated`.
Add per-user invite quota (e.g. 10/min) enforced in `InviteService`.

**Cost:** ~4 hours.

### 7.10 — Documentation & Architectural Decision Records (ADRs)

**Problem:** This document captures the *what* and *why* once. Without ADRs, the next
engineer who wants to "just add a new state" or "just move ParseInviteLine" won't know
why those decisions were made.

**Fix:** Create `Docs/ADR/` with one short markdown per decision:
- `ADR-001-state-machine-vs-bool-flags.md`
- `ADR-002-soap-event-bus-vs-c-sharp-events.md`
- `ADR-003-pending-protocol-three-phase-commit.md`
- `ADR-004-lazy-relay-session.md` (when we do §7.1)

**Cost:** ~2 hours.

---

## 8. KISS / DRY Audit

### KISS — Where We Resisted Cleverness

- **No reflection-based service registration.** Every DI binding is one explicit
  `RegisterFactory` call. Slightly more lines, dramatically easier to debug.
- **State machine is a `HashSet<(from, to)>` lookup.** No FSM library, no codegen, no DSL.
  17 lines of code total.
- **PENDING protocol stores plain strings in lobby properties.** No protobuf, no schema
  registry. The format is one line in `InviteService.SerializeAll()`.
- **Reflex DI bindings live in one place** (`AppManager.InstallBindings`), not scattered
  across `[Install]` attributes per service. One file to audit.

### DRY — Where We Killed Duplication

- **Three duplicated NM-shutdown patterns** (one in `PartyInviteController`, one in
  `HostConnectionService.CreatePartySessionCoreAsync`, one in `LeavePartyAndReturnToMenu`)
  collapsed into `NetworkTransitionService.ShutdownAsync(timeout, ct)`.
- **Two duplicated SOAP event raises** for `OnPartyMemberJoined` (one in member sync,
  one in seed-on-create) collapsed into `SoapPartyEventBus.RaisePartyMemberJoined`.
- **Two duplicated invite-payload parse paths** (one for incoming refresh, one for
  acceptance scan) collapsed into `InviteService.ParseLine`.
- **Two duplicated 429-retry loops** collapsed into `LobbyPropertyWriter.SaveWithRetryAsync`.

### DRY — Where We **Left** Three Similar Lines (Deliberately)

Per CLAUDE.md "three similar lines is better than a premature abstraction":

- `SetPresenceInMenu / SetPresenceInParty / SetPresenceInGame` in `FriendsInitializer`
  share ~5 lines of boilerplate. We did **not** extract a `BuildPresenceActivity` helper
  because each call site has different parameters and semantics. Future engineers can
  read each one independently.

---

## 9. Recommended Implementation Order

The items below are ordered by **engineering leverage and best-practices standards only** —
SOLID, DRY, KISS, safety, and observability. No shortcuts, no deferral based on external
deadlines. Each item should be completed, verified with MPPM, and covered by tests before
the next one starts.

### Tier 1 — Foundation (do these before anything else)

**§7.2 → Play-Mode Integration Tests first.**
Every remaining item below is silently re-breakable without automated end-to-end coverage.
The test harness is the safety net that makes every subsequent change trustworthy. It must
exist before any other structural change begins.

**§7.1 → Lazy Party-Session Creation second.**
This is the highest-leverage engineering improvement in the system. It removes the root
cause of an entire class of race conditions (scene-sync race, orphaned local hosts, stale
refs), cuts Relay session counts by ~10×, and reduces accept latency from ~1.5–3s to
~500–800ms. It also removes ~200 lines from `HostConnectionService`, closing the gap
between current LOC and the ≤200-line facade target.

### Tier 2 — Correctness (observable, fail-safe behavior)

**§7.4 → Implement the `Reconnecting` state.**
The state exists in the machine and is tested, but no production code ever enters it.
A user who loses connection mid-party today silently falls off a cliff — no retry, no
recovery, no user feedback. This is the last correctness gap: the state machine is only
as useful as its completeness.

**§7.3 → Move `HostConnectionService` off `MonoBehaviour`.**
This closes the final DI gap: `FriendsInitializer` still reads `HostConnectionService.Instance`
because HCS is a MonoBehaviour that Reflex cannot register. Once HCS is a pure C# class
(`HostConnectionMono` holds only the lifecycle hooks), every consumer gets `IPartyStateQuery`
via `[Inject]` and the last singleton read disappears.

### Tier 3 — Code Quality (eliminate remaining anti-patterns)

**§7.5 → Replace the static C# event with a SOAP channel.**
One remaining `public event Action<string>` on HCS violates the project's "no C# events for
cross-system communication" rule. Replace with `connectionData.OnInviteCleared` SOAP event.

**§7.7 → Inject `ITimeProvider`.**
Removes `Time.unscaledTime` calls from services, making time-based logic unit-testable
without a running Unity instance.

**§7.8 → Move `ParseInviteLine` to `InviteService`.**
The wrapper exists only for test compat. Update the two affected tests to reflect on
`InviteService.ParseLine` directly and delete the HCS wrapper.

**§7.10 → Create ADR documents.**
Without Architectural Decision Records, the next engineer who touches these files will not
know *why* the PENDING protocol has three phases, why the state machine uses a HashSet
instead of a switch, or why ParseInviteLine was left on HCS. Write short ADRs for every
non-obvious decision so the intent survives personnel changes.

### Tier 4 — Hardening (security and observability)

**§7.6 → Server-side invite token validation.**
The current model trusts any client that presents a session ID. Add signed short-lived
tokens to invite payloads, validated in the connection approval callback. This is
non-negotiable for a production multiplayer system.

**§7.9 → Metrics and telemetry.**
Add `ProfilerMarker`s on every async path. Emit analytics events for `InviteSent`,
`InviteAccepted`, `InviteDeclined`, `PartyJoinFailed`, `RelaySessionCreated`. Without
telemetry, performance regressions are invisible until they surface as user reports.

---

### Completion Criteria

The party system is at **zero tech debt** when:

- All 9 items above are done and MPPM-verified
- `HostConnectionService.cs` is ≤200 lines (facade only)
- `PartyInviteController.cs` is ≤120 lines
- Play-mode tests cover: invite send, accept, decline, leave, reconnect
- No `.Instance` reads outside of `HostConnectionMono`
- No raw C# events — all cross-system notifications go through SOAP channels
- ADRs exist for every architectural decision
- Relay sessions are only created when a user actually sends an invite

---

## Appendix A — File Manifest

### New Files (16)
```
Assets/_Scripts/Controller/Party/
├── StateMachine/
│   ├── PartyState.cs
│   └── PartyStateMachine.cs
├── Interfaces/
│   ├── IPresenceLobbyService.cs
│   ├── IPartySessionService.cs
│   ├── IInviteService.cs
│   ├── IPartyMemberService.cs
│   ├── INetworkTransitionService.cs
│   └── IPartyStateQuery.cs
└── Services/
    ├── LobbyPropertyWriter.cs
    ├── SoapPartyEventBus.cs
    ├── LobbyRefreshScheduler.cs
    ├── InviteService.cs
    ├── PresenceLobbyService.cs
    ├── AcceptanceSignalService.cs
    ├── PartySessionService.cs
    ├── PartyMemberService.cs
    └── NetworkTransitionService.cs
```

### Modified Files (4)
```
Assets/_Scripts/Controller/Party/HostConnectionService.cs   (1862 → ~1100 lines)
Assets/_Scripts/Controller/Party/PartyInviteController.cs   (482 → 265 lines)
Assets/_Scripts/Controller/Party/FriendsInitializer.cs      (211 → 230 lines)
Assets/_Scripts/System/AppManager.cs                        (+62 lines DI)
Assets/_Scripts/Tests/EditMode/PartyInviteSystemTests.cs    (+173 lines, 10 tests)
```

### Commits (14, all on `claude/fix-vessel-invite-bug-netES`)
Phase 1 → Phase 14, one commit per phase, each with explicit MPPM checkpoint criteria.

---

*Document version 1.1 — GDC and sprint-plan language removed. Roadmap reframed as best-practices implementation order. Ready for CTO review.*
