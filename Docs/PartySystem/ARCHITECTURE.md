# Party System — Architecture Snapshot

The party / invite / lobby system has been hardened through a 17-commit
refactor (formerly tracked in `PARTY_SYSTEM_REFACTOR.md`, now archived
via git history). This doc captures the **current state** — the locked
design, the moving parts, and the exit criteria. For the active refactor
queue see `REFACTOR.md`; for known bugs see `BUGS.md`; for manual test
procedures see `TESTS.md`.

## Locked design — do not relitigate

These decisions have been made and shipped. Re-opening them is the most
common way recurring party-invite bugs return.

- **EAGER per-user Relay party session.** Every authenticated player
  hosts their own Relay-backed party session from the moment they enter
  `Menu_Main` ("Always InParty" model). **Do not reintroduce LAZY /
  on-first-invite Relay creation** — the shutdown-and-recreate cascade
  it caused is the root of every recurring party-invite bug. If a
  future bug appears to argue for lazy creation, re-examine the root
  cause through the unbreakable-exit-criteria lens below first.
- **`ActiveSession` is never nulled outside an intentional leave.**
  `PartySessionService.ActiveSession` reads/writes
  `gameData.ActiveSession`. Single backing field on `GameDataSO`.
- **One public create-or-no-op surface: `EnsurePartySessionAsync`** —
  idempotent (no-op if `IsHostingParty`, create otherwise). All other
  `RetryCreate*` wrappers were deleted.
- **`MultiplayerService.Instance` is always resolved at use time**, never
  cached in a constructor. Lazy DI singletons are constructed during
  Bootstrap DI resolution — *before* `UnityServices.InitializeAsync()`
  completes — so a constructor-time read would pin null. Expose via a
  private property: `private IMultiplayerService _multiplayerService => MultiplayerService.Instance;`.
- **Every null guard logs `Debug.LogError`** with field name and
  suspected cause. Loud, traceable failures.
- **Every caught exception** either escalates the state machine,
  restores state, or no-ops safely. **No catch silently drops
  `ActiveSession`. No catch leaves the system in a worse state than
  entry.**
- **State machine is the authority for recovery, not nulls.** Runtime
  nulls inside a service method imply an invariant violation → log +
  transition to a recoverable state (typically `Disconnected`), so the
  normal sign-in / retry path picks back up.
- **Main-thread affinity is mandatory at every UGS / Netcode await.**
  See `Docs/THREADING.md`. Use `.AsMainThread()` — never
  `UniTask.SwitchToMainThread()` or
  `UniTask.Yield(PlayerLoopTiming.Update)` as a thread-marshaling fix.

## Two-level session architecture

The party system layers two distinct UGS sessions:

| Layer | Purpose | Relay? | Max Players | Service |
|---|---|---|---|---|
| **Presence Lobby** | Player discovery, invite property exchange | No (lobby-only) | 100 | `PresenceLobbyService` |
| **Party Session** | Actual gameplay networking via Relay | Yes (`WithRelayNetwork()`) | 4 (configurable) | `PartySessionService` |

The presence lobby is a lobby-only UGS session that coexists safely with
an active NetworkManager. Players set their own player properties to
send invites — no host privilege required.

See `../PresenceSystem/ARCHITECTURE.md` for presence-lobby details. This
doc focuses on the party (Relay-backed) layer.

## Core services

### `HostConnectionService` (DontDestroyOnLoad MonoBehaviour, singleton)

The orchestrator. Auto-creates its own party session on auth sign-in,
auto-joins the presence lobby for discovery, runs the
`MAX_REFRESH_ERRORS_BEFORE_RECONNECT` watchdog, and exposes the
party-level operations (`AcceptInviteAsync`, `LeavePartyKeepHostAsync`,
`KickPartyMemberAsync`, `EnsurePartySessionAsync`, `ResetPartyLayerAsync`).

**Single writer to `HostConnectionDataSO`** — every other system reads
through SOAP events and lists on that data container.

#### Offline sessions stand the party layer down

When `GameDataSO.IsOfflineSession` is set (`OfflineModeService` started a plain
`127.0.0.1` host because UGS was unreachable — `Docs/OFFLINE_MODE.md`):

- **`EnsurePartySessionAsync` no-ops for the whole session.** Auth can succeed while
  Relay keeps failing, and this method retries with backoff long after the boot flow
  has already fallen back — a late success would `ShutdownAsync` the local host out
  from under a live offline game. Re-entering online is a deliberate re-boot
  (`ReconnectService`), never an in-place promotion.
- **`SendInviteAsync` returns early.** There is no presence lobby and no Relay session
  to invite into; without the guard the call fell through to the no-op above and
  dereferenced a null session ref.

**This does not relitigate the locked EAGER per-user Relay design.** Offline is the
*absence* of Relay, entered only when Relay is provably unreachable — not lazy or
on-first-invite creation. When online, the eager session is created exactly as before.

#### `ResetPartyLayerAsync()` — the clean-slate primitive

Leaves the Relay party session **and** the presence lobby, and returns the state
machine to `Disconnected`. Two callers, one need: `ReconnectService` before the boot
chain re-runs, and `OfflineModeService` when an offline session starts.

Leaving the **presence lobby** is the half that is easy to miss and fatal to skip:
UGS membership is *server-side*, so tearing down `NetworkManager` does not release it,
and a re-join while still a member is refused with *"player is already a member of the
lobby"* — HCS then never finishes initialising and no Relay session is ever created.
It must run **before** the Netcode shutdown, because the leave calls need a live
transport to reach UGS.

It deliberately does **not** raise `HostConnectionLost` — that drives the boot panel's
"tap retry" surface, and this teardown is a step inside a transition already covered by
the loading veil. Fail-soft at every step, and a no-op on a cold offline boot that never
joined anything.

#### One added state transition

`Reconnecting → InPresenceLobby` is legal. The refresh watchdog
(`MAX_REFRESH_ERRORS_BEFORE_RECONNECT`) can drop the machine into `Reconnecting` at any
moment — including while a reconnect or an offline↔online switch is re-running HCS init,
whose first move is `InPresenceLobby`. Without it that transition was refused and
initialisation stopped dead. Re-entering through the front door after a drop is a
legitimate recovery, not a bug to log. (Pre-existing gap; the mode switch made it
reachable every time.)

Already refactored (the 17-commit work). The remaining party-side
refactor work targets the orchestrator above it (`PartyInviteController`)
and the two extracted services (`PartySessionService`,
`NetworkTransitionService`) — see `REFACTOR.md`.

### `PartySessionService` (pure C#, constructor-injected)

Owns the Relay-backed UGS session lifecycle: `CreateAsync`,
`JoinByIdAsync`, `LeaveAsync`. Both create and join run inside retry
loops keyed on three exception classifiers:

| Classifier | Behavior |
|---|---|
| `IsHostConflictException` | Retry up to `HOST_CONFLICT_MAX_RETRIES`, no backoff |
| `IsRateLimitException` (HTTP 429) | Retry up to `RATE_LIMIT_MAX_RETRIES` with exponential backoff |
| `IsTransientSessionException` | Retry up to `TRANSIENT_MAX_RETRIES` with exponential backoff — covers SDK `SessionException` NRE / lobby-events 23006 / non-fatal session-state collisions |

Non-transient errors propagate to `HostConnectionService.AcceptInviteAsync`,
which logs and rethrows so `PartyInviteController` fails fast.

### `NetworkTransitionService` (pure C#, constructor-injected)

Owns the Netcode lifecycle transitions: `ShutdownAsync` (wait for full
NM reset with timeout), `WaitForClientConnectionAsync` (poll
`IsConnectedClient` with timeout), `WaitForSceneSyncAsync` (await the
client's first `SceneEvent` after host scene-load). All three use
linked-CTS-with-timeout patterns. Each timeout path emits a NetDiag
log line per the diagnostics overlay (see
`../NetworkDiagnostics/ARCHITECTURE.md`).

### `PartyInviteController` (MonoBehaviour, singleton)

The user-facing flow orchestrator. `AcceptInviteAsync`,
`DeclineInviteAsync`, `LeavePartyAndReturnToMenuAsync`. Reads
`HostConnectionService.Instance` (today) and delegates to
`INetworkTransitionService` for Netcode work. Recovery on failure runs
through `RecoverFromFailedTransitionAsync` →
`gameData.DestroyPlayerAndVessel` + `LeavePartyKeepHostAsync` +
Menu_Main reload.

`IsTransitioning` is a public predicate read by HCS as the catch-guard
that prevents in-flight presence refreshes from escalating during a
transition. (Both the entry guard at the top of `RefreshAsync` and the
second-layer guard inside the catch consult it.)

## SOAP event flow — invite happy path

```
Sender's HCS lobby property write (invite_target, invite_data)
        │
        ▼  (next refresh tick, 3-5 s)
Recipient's HCS.RefreshAsync detects invite property
        │
        ▼
OnInviteReceived SOAP event ──► PartyInviteNotificationPanel.Show()
        │
        ▼  (user presses Accept)
PartyInviteController.AcceptInviteAsync
        │
        ├─ TransitionVisuals: SetFadeImmediate(1) + ArmSplashFadeOnNextClientReady + Unpause
        ├─ NetworkTransition.ShutdownAsync (with 8s timeout)
        ├─ HostConnectionService.AcceptInviteAsync (publishes acceptance signal, then JoinByIdAsync)
        ├─ NetworkTransition.WaitForClientConnectionAsync (with 8s timeout)
        ├─ NetworkTransition.WaitForSceneSyncAsync (with 8s timeout)
        ├─ WaitForClientReadyAsync (with 10s timeout — waits for local vessel to spawn)
        └─ Raise OnPartyJoinCompleted ──► Party Area UI refreshes
```

Each `await` is `.AsMainThread()`-wrapped per `Docs/THREADING.md`. Each
catch block now appends a NetDiag log line classifying the failure (see
`../NetworkDiagnostics/ARCHITECTURE.md`).

## Unbreakable exit criteria

A party system is "unbreakable" when **all** of the following hold under
adversarial conditions (network drops, client crashes, fast input,
concurrent invites):

1. **No fatal failure.** No vessel despawn outside an intentional leave;
   no kicked clients; no NRE crash; no UGS exception surfaces to the
   user uncaught.
2. **No stuck UI.** Party UI always reflects ground truth — no stale
   "in party" when there is no party; no missing party members; no
   undismissable invite popups.
3. **No silent state divergence.** Host's view of party membership
   matches every client's view within one refresh tick. `ActiveSession`
   on the local client matches the UGS session ID the user is actually
   in.
4. **All transitions are reversible.** An accepted invite that fails
   mid-flow returns the user cleanly to solo Menu_Main with no
   leftover NetworkObjects, no stale Cinemachine targets, and no
   half-initialized vessel.
5. **Idempotent retries.** Tapping Accept twice does not start two
   transitions. Tapping Leave during an Accept cancels cleanly. The
   user cannot wedge the system into an unrecoverable state by rapid
   input.
6. **3-VP MPPM accept / decline / leave smoke** is green on every
   commit.
7. **3-VP MPPM stress** (5 consecutive accepts with random
   declines/leaves interleaved) is green.
8. **4-VP MPPM concurrent invites** (host invites 2-3 clients
   simultaneously) — all clients either join cleanly or bounce
   cleanly with no leftover state.

Criteria 1-5 are passing as of the 17-commit refactor + the YS2
catch-guard fix (commit `a1a8eb9`). Criteria 6-8 are the active
verification gate per commit.

## Key files

| Role | File |
|---|---|
| Orchestrator (singleton, single writer to data SO) | `Assets/_Scripts/Controller/Party/HostConnectionService.cs` |
| User-facing flow controller | `Assets/_Scripts/Controller/Party/PartyInviteController.cs` |
| Relay session lifecycle | `Assets/_Scripts/Controller/Party/Services/PartySessionService.cs` |
| Netcode transitions | `Assets/_Scripts/Controller/Party/Services/NetworkTransitionService.cs` |
| Lobby property writes (mutex + retry) | `Assets/_Scripts/Controller/Party/Services/LobbyPropertyWriter.cs` |
| Invite-receive detection | `Assets/_Scripts/Controller/Party/Services/InviteService.cs` |
| Acceptance signal (sender ↔ receiver handshake) | `Assets/_Scripts/Controller/Party/Services/AcceptanceSignalService.cs` |
| Refresh cadence (boost + base) | `Assets/_Scripts/Controller/Party/Services/LobbyRefreshScheduler.cs` |
| SOAP event bus | `Assets/_Scripts/Controller/Party/Services/SoapPartyEventBus.cs` |
| Party member sync | `Assets/_Scripts/Controller/Party/Services/PartyMemberService.cs` |
| State machine | `Assets/_Scripts/Controller/Party/StateMachine/PartyStateMachine.cs` |
| SOAP data container | `Assets/_Scripts/Utility/DataContainers/HostConnectionDataSO.cs` |

## Investigation answers — design Q&A

These are the load-bearing design questions resolved during the 17-commit
refactor. Kept verbatim because several code comments and future
refactors reference the specific reasoning (not just the conclusion).

### Q1. Why does `CreateOwnPartySessionAsync` call `ShutdownAsync` first?

`NetworkTransitionService.ShutdownAsync` guards `if (nm == null || !nm.IsListening)`
— no-op whenever NM is down. It's real work only when NM is hosting. Real work
currently happens only in the `RecoverFromFailedTransitionAsync` path. The
commit-by-commit plan replaced the unconditional `ShutdownAsync` with
`LeavePartyKeepHostAsync` (Commit 10) so the recovery path no longer cycles NM at
all.

### Q2. Is `IsListening` the strongest possible guard?

For `nm.Shutdown()` specifically — yes. Netcode treats `Shutdown()` as a no-op when
`!IsListening`. For the broader "do we have a host?" question, the canonical
project-wide predicate is `IsHostingParty`:

```csharp
private bool IsHostingParty
{
    get
    {
        var nm = NetworkManager.Singleton;
        return nm != null && nm.IsListening && nm.IsServer && PartySession != null;
    }
}
```

We use `IsHostingParty` everywhere the question is "am I a live party host?", and
`IsListening` only where the question is "is Netcode up at all?".

### Q3. The four `RetryCreate*` callsites — none of them are really "retries"

| Site | What just happened | Prior `ActiveSession`? | Needs `ClearSession()` first? |
|---|---|---|---|
| `AuthenticationSceneController` (auto-retry) | Initial create failed | None | No |
| `AuthenticationSceneController` (manual retry button) | Tap retry on `BootStatusPanel` | None | No |
| `PartyInviteController` (`LeavePartyAndReturnToMenuAsync`) | Caller left party | None (LeaveAsync cleared it) | No |
| `PartyInviteController` (`RecoverFromFailedTransitionAsync`) | Accept transition failed mid-flow | Possibly stale | Yes |

3 of 4 sites are first-time creates; 1 is true recovery. `EnsurePartySessionAsync`
covers all 4 (idempotent). Only site #4 explicitly calls `ClearSession()` first.

### Q4. Source of truth: `gameData.ActiveSession` vs `partySessionService.ActiveSession`

Both `ISession`. Consolidated to one backing field on `GameDataSO`:

```csharp
// In PartySessionService:
public ISession ActiveSession
{
    get => _gameData.ActiveSession;
    private set => _gameData.ActiveSession = value;
}
```

`PartySessionService` ctor takes `GameDataSO`. All existing readers keep working.

### Q5. `PartySessionService.CreateAsync` vs `MultiplayerService.Instance.CreateSessionAsync`

Not duplicative. The wrapper adds host-conflict retry, rate-limit handling, transient
SDK NRE handling, identity properties, grace-period tracking, idempotency.
`JoinByIdAsync` adds identity properties only.

### Q6. Can we leave a party without shutting down NM?

Yes — `PartySessionService.LeaveAsync` only touches the UGS SDK; NM shutdown is the
**caller's** choice. Today every leave path shuts NM down → menu-vessel respawn. We
added a new `LeavePartyKeepHostAsync` path that leaves the UGS session and immediately
calls `EnsurePartySessionAsync` to create a fresh solo session, **without cycling NM**.
Removes the entire shutdown-and-recreate antipattern.

### Q7. Stale UI when server deleted the session — is that a bug?

Yes. The unbreakable invariant says **the UI must never show "in party" when there is
no party**. Achieved by classifying refresh errors:
- **Transient** (timeout, rate-limit, generic SessionException): keep ref, log, retry
  on next tick. State unchanged.
- **Definite** (404 / not-found / "session does not exist"): treat as server-side leave.
  Call `LeavePartyKeepHostAsync` automatically, raise `OnPartyMemberLeft` for each
  remaining member so UI updates, raise `OnHostConnectionLost`. State transitions to
  `HostingParty` (solo) → user sees an empty party slot, no manual action required.

Implemented in Commits 11–12.

### Q8. Awake / OnDestroy null-safety — recovery action for each branch

Awake (after `LobbyPatcherLogFilter` removal) touches no `[Inject]` fields. Safe.

OnDestroy is best-effort cleanup. We can't recover during destruction — the gameobject
is going away — but we log loudly so missing prefab references / DI failures surface:

| Null field | Cause | Action |
|---|---|---|
| `bootStatusRetryRequestedEvent` | SOAP event asset not wired in prefab | `LogError`, skip unsubscribe |
| `_lobbyService` | Reflex DI never populated it | `LogError`, skip presence lobby leave. Others see this player "online" ~30s until UGS reaps |
| `_lobbyMutex` | Ctor failed or double-destroy | `LogError`, skip dispose |
| `_sessionCreationMutex` | Ctor failed or double-destroy | `LogError`, skip dispose |

For runtime null guards inside service methods (rare — most cases are handled via
state-machine predicates), the action is: `LogError` + transition state to `Disconnected`
+ raise `OnHostConnectionLost` so the normal recovery loop picks back up.

### Q9. Is the Start-polling deletion safe?

HCS is `DontDestroyOnLoad` in Bootstrap. Auth completes later, in the Authentication
scene. Worst race: auth completes between HCS-Awake and HCS-Start — `Start()` then
calls `HandleSignedInEvent()`, which is **idempotent** (sees `_joining == true` or
already-initialized state and no-ops). Safe.

### Q10. `MultiplayerService.Instance` as a class member

Rationale for the locked-design rule above ("`MultiplayerService.Instance`
is always resolved at use time"): expose it via a private property
(`private IMultiplayerService _multiplayerService => MultiplayerService.Instance;`)
and never cache it in a constructor. Lazy DI singletons are constructed during
Bootstrap DI resolution, *before* `UnityServices.InitializeAsync()` completes, so a
constructor-time read would pin null forever. Also a project-wide anti-pattern in
`CLAUDE.md`.

## Error-handling matrix — recovery action for every catch site

Every catch in `HostConnectionService` / `PartySessionService` /
`PresenceLobbyService` maps to one of these recovery actions. **No catch silently
drops state.** This is the recovery-policy spec; it is distinct from the NetDiag
log classifier (see `../NetworkDiagnostics/ARCHITECTURE.md`, "Not a retry-control
predicate") — the matrix decides *what to do*, NetDiag only decides *what to log*.

| Catch site | Failure class | Recovery |
|---|---|---|
| `RefreshPartyMembersAsync` benign Lobby-patcher noise | Spurious SDK NRE | Swallow silently (known SDK bug) |
| `RefreshPartyMembersAsync` `RateLimitedException` | UGS rate limit | Set `_rateLimitBackoffUntil`, skip this tick, retry next interval. State unchanged. |
| `RefreshPartyMembersAsync` 404 / SessionNotFound | Server-side session deleted | Classify **definite**: call `LeavePartyKeepHostAsync` → fresh solo session. UI updates via `OnHostConnectionLost` + per-member `OnPartyMemberLeft`. |
| `RefreshPartyMembersAsync` other `SessionException` | Transient | Log warning, increment `_consecutiveRefreshErrors`, retry next tick. After threshold (3), promote to definite. |
| `PartySessionService.LeaveAsync` inner UGS throw | Session already gone | Already wrapped; ref cleared regardless. Caller ends in clean state. No change. |
| `KickPartyMemberAsync` UGS throw | Dead session / disconnected target | Wrap in try/catch, log, state unchanged. Host can retry (target reappears on next refresh). |
| `CreateAsync` host-conflict | Concurrent host on same account | Existing retry policy. No change. |
| `CreateAsync` `RateLimitedException` | UGS rate limit | Existing backoff. No change. |
| `CreateAsync` other | Permanent failure | Bubble to `EnsurePartySessionAsync`, which raises retry event for `BootStatusPanel`. User-visible recovery. |
| `SendInviteAsync` UGS throw | Lobby gone / target offline | Wrap, log, return false. UI shows error toast (already wired). |
| `AcceptInviteAsync` UGS throw on join | Inviter session gone | Caller (`PartyInviteController.AcceptInviteAsync`) catches → `RecoverFromFailedTransitionAsync`. |
| `OnDestroy` null fields | Missing inspector ref / Reflex DI failure | `Debug.LogError`, skip the dependent cleanup. Loud failure → visible in editor. |

## Related docs

- `REFACTOR.md` — the active refactor backlog (PIC + PartySessionService + NetworkTransitionService)
- `BUGS.md` — open party-side bugs
- `TESTS.md` — manual MPPM test procedures
- `TODOS.md` — minor parking-lot items
- `../PresenceSystem/ARCHITECTURE.md` — presence-lobby layer
- `../NetworkDiagnostics/ARCHITECTURE.md` — NetDiag overlay used by all party catches
- `../THREADING.md` — main-thread affinity rules (mandatory for every UGS / Netcode await)
