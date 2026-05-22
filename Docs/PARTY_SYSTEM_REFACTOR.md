# HostConnectionService Refactor — Iterative commits toward an unbreakable party system

## Context

Two open bugs in the party-invite flow:
- **Bug A**: pressing "+" to send an invite despawns the host's own menu vessel.
- **Bug B**: a joining client's splash overlay never fades after accept.

Both have the same root: transient code paths null `_partySessionService.ActiveSession`
while NM is still hosting, which causes downstream callers to shut NM down and respawn
every menu vessel.

**Ultimate goal**: an unbreakable party / invite / lobby system. No fatal failure (vessel
despawn, kicked clients, NRE crash) and no stuck UI (stale "in party" when there is no
party). We achieve it as an ongoing series of small commits, not a fixed-length pass.

**Working protocol** (per user request):

- The commits listed below are the working backlog, in priority order.
- **Before executing each commit**, we re-read the surrounding code, revise that
  commit's section in this plan with any new findings, and only then start coding.
- Each commit compiles, passes existing tests, and is independently buildable.
- We keep iterating until the "unbreakable exit criteria" at the bottom of this file
  are all met. The plan grows as we discover new issues during execution.

## Locked decisions (no more debate)

- **Eager Relay creation stays.** NM is a Netcode host with Relay transport from menu
  entry. Every "lazy Relay" comment gets removed.
- **`ActiveSession` is never nulled** outside an intentional leave.
- One public create-or-no-op surface: **`EnsurePartySessionAsync`** — idempotent (no-op
  if `IsHostingParty`, create otherwise). `RetryCreate*` wrapper deleted.
- One source of truth for the active session: `PartySessionService.ActiveSession`
  reads/writes `gameData.ActiveSession`. Single backing field on `GameDataSO`.
- `MultiplayerService.Instance` is always cached as a class-member field in services
  that call it. Pattern documented in `CLAUDE.md`.
- Every null guard logs `Debug.LogError` with field name and suspected cause. Loud,
  traceable failures.
- Every caught exception either escalates the state machine, restores state, or no-ops
  safely. **No catch silently drops `ActiveSession`. No catch leaves the system in a
  worse state than entry.**
- State machine is the authority for recovery, not nulls. Runtime nulls inside a
  service method imply an invariant violation → log + transition to a recoverable state
  (typically `Disconnected`), so the normal sign-in / retry path picks back up.

## Investigation answers — every question raised

### Q1. Why does `CreateOwnPartySessionAsync` call `ShutdownAsync` first?

`NetworkTransitionService.ShutdownAsync` (line 83) guards `if (nm == null || !nm.IsListening)`
— no-op whenever NM is down. It's real work only when NM is hosting. Real work currently
happens only in the `RecoverFromFailedTransitionAsync` path. The commit-by-commit plan
replaces the unconditional `ShutdownAsync` with `LeavePartyKeepHostAsync` (commit #10) so
the recovery path no longer cycles NM at all.

### Q2. Is `IsListening` the strongest possible guard?

For `nm.Shutdown()` specifically — yes. Netcode treats `Shutdown()` as a no-op when
`!IsListening`. For the broader "do we have a host?" question, the canonical project-wide
predicate is `IsHostingParty`:

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
| `AuthenticationSceneController:465` (auto-retry) | Initial create failed | None | No |
| `AuthenticationSceneController:478` (manual retry button) | Tap retry on `BootStatusPanel` | None | No |
| `PartyInviteController:277` (`LeavePartyAndReturnToMenuAsync`) | Caller left party | None (LeaveAsync cleared it) | No |
| `PartyInviteController:350` (`RecoverFromFailedTransitionAsync`) | Accept transition failed mid-flow | Possibly stale | Yes |

3 of 4 sites are first-time creates; 1 is true recovery. `EnsurePartySessionAsync` covers
all 4 (idempotent). Only site #4 explicitly calls `ClearSession()` first.

### Q4. Source of truth: `gameData.ActiveSession` vs `partySessionService.ActiveSession`

Both `ISession`. Consolidate to one backing field on `GameDataSO`:

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

Not duplicative. The wrapper adds host-conflict retry, rate-limit handling, transient SDK
NRE handling, identity properties, grace-period tracking, idempotency. `JoinByIdAsync`
adds identity properties only.

### Q6. Can we leave a party without shutting down NM?

Yes — `PartySessionService.LeaveAsync` only touches the UGS SDK; NM shutdown is the
**caller's** choice. Today every leave path shuts NM down → menu-vessel respawn. We add a
new `LeavePartyKeepHostAsync` path that leaves the UGS session and immediately calls
`EnsurePartySessionAsync` to create a fresh solo session, **without cycling NM**.
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

Implemented in commits #11–12.

### Q8. Awake / OnDestroy null-safety — recovery action for each branch

Awake (after `LobbyPatcherLogFilter` removal) touches no `[Inject]` fields. Safe.

OnDestroy is best-effort cleanup. We can't recover during destruction — the gameobject
is going away — but we log loudly so missing prefab references / DI failures surface:

| Null field | Cause | Action |
|---|---|---|
| `bootStatusRetryRequestedEvent` | SOAP event asset not wired in prefab | `LogError`, skip unsubscribe (we never subscribed if it was null at Awake time too) |
| `_lobbyService` | Reflex DI never populated it | `LogError`, skip presence lobby leave. Other users see this player as "online" for ~30s until UGS reaps |
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

Constructor-injected `IMultiplayerService` field with `?? MultiplayerService.Instance`
fallback. Documented in `CLAUDE.md` as a project-wide pattern (any UGS / Netcode singleton
`.Instance` access in a service class should be cached).

## Error handling matrix — recovery action for every catch site

Every catch in `HostConnectionService` / `PartySessionService` / `PresenceLobbyService`
maps to one of these recovery actions. No catch silently drops state.

| Catch site | Failure class | Recovery |
|---|---|---|
| `RefreshPartyMembersAsync` benign Lobby-patcher noise | Spurious SDK NRE | Swallow silently (known SDK bug) |
| `RefreshPartyMembersAsync` `RateLimitedException` | UGS rate limit | Set `_rateLimitBackoffUntil`, skip this tick, retry next interval. State unchanged. |
| `RefreshPartyMembersAsync` 404 / SessionNotFound | Server-side session deleted | Classify as **definite**: call `LeavePartyKeepHostAsync` → fresh solo session. UI updates via existing `OnHostConnectionLost` + per-member `OnPartyMemberLeft`. |
| `RefreshPartyMembersAsync` other `SessionException` | Transient | Log warning, increment `_consecutiveRefreshErrors`, retry next tick. After threshold (3), promote to definite. |
| `PartySessionService.LeaveAsync` inner UGS throw | Session already gone | Already wrapped (`LeaveAsync:208`); ref cleared regardless. Caller ends in clean state. No change. |
| `KickPartyMemberAsync` UGS throw | Dead session / disconnected target | Currently unwrapped — propagates. Wrap in try/catch, log, state unchanged. Host can retry. |
| `CreateAsync` host-conflict | Concurrent host on same account | Existing retry policy. No change. |
| `CreateAsync` `RateLimitedException` | UGS rate limit | Existing backoff. No change. |
| `CreateAsync` other | Permanent failure | Bubble to `EnsurePartySessionAsync`, which raises retry event for `BootStatusPanel`. User-visible recovery action. |
| `SendInviteAsync` UGS throw | Lobby gone / target offline | Wrap, log, return false. UI shows error toast (already wired). |
| `AcceptInviteAsync` UGS throw on join | Inviter session gone | Caller (`PartyInviteController.AcceptInviteAsync`) catches → `RecoverFromFailedTransitionAsync` (existing path). |
| `OnDestroy` null fields | Missing inspector ref / Reflex DI failure | `Debug.LogError`, skip the dependent cleanup. Loud failure → visible in editor. |

## Helper properties (commit #3)

Replace `_initialized` field + state-machine double-checks across the file:

```csharp
private bool IsInitialized => _stateMachine.CurrentState != PartyState.Disconnected;
private bool IsInPresenceLobby => IsInitialized && _lobbyService.ActiveLobby != null;
private bool IsHostingParty
{
    get
    {
        var nm = NetworkManager.Singleton;
        return nm != null && nm.IsListening && nm.IsServer && PartySession != null;
    }
}
```

## Commit backlog (priority order)

Each commit is small, focused, independently shippable. **Before executing any commit,
re-read the live code, update that commit's section here with what changed, and only
then start coding.** Status keys: `[ ]` planned, `[~]` in progress, `[x]` done.

### `[ ]` Commit 1 — Dead-code removal (no behaviour change)

- Delete `ClearStalePartySession`, `CreatePartySessionPublicAsync`, `CreatePartySessionAsync`,
  `CreatePartySessionCoreAsync` (4 dead methods in `HostConnectionService.cs`).
- Delete `LobbyPatcherLogFilter` plumbing (field, install/uninstall, inner class,
  Awake + OnDestroy calls). Keep `IsBenignLobbyPatcherError` — commit 2 uses it.
- Revert the previous stop-gap NM-listening guard in `RefreshPartyMembersAsync`.

### `[ ]` Commit 2 — Simplify `RefreshPartyMembersAsync` catch (Bug A primary fix)

- Replace broad catch with: benign noise → swallow; `RateLimitedException` → set
  backoff; everything else → log warning + return. No `ClearSession()` anywhere.
- This single change is what closes Bug A (host vessel despawn on invite send).

### `[ ]` Commit 3 — Guard helper properties + delete `_initialized`

- Add `IsInitialized`, `IsInPresenceLobby`, `IsHostingParty` private properties.
- Replace every `_initialized` read / state-machine double-check with helpers.
- Delete `_initialized` field + its two writes.

### `[ ]` Commit 4 — `EnsurePartySessionAsync` introduction

- Rename `CreateOwnPartySessionAsync` → `EnsurePartySessionAsync` (still private for now).
- Add idempotent guard at top: `if (IsHostingParty) return;`
- Delete `RetryCreateOwnPartySessionAsync` wrapper. Make `EnsurePartySessionAsync` public.
- Update tooltip on `bootStatusRetryRequestedEvent` at `HostConnectionService.cs:54`.

### `[ ]` Commit 5 — Update `EnsurePartySessionAsync` callsites

- `AuthenticationSceneController:465, 478`: call `EnsurePartySessionAsync()`.
- `PartyInviteController:277`: call `EnsurePartySessionAsync()`.
- `PartyInviteController:350` (`RecoverFromFailedTransitionAsync`): explicit
  `_partySessionService.ClearSession();` immediately followed by
  `await _hostConnectionService.EnsurePartySessionAsync();`.
- This is the only site that intentionally drops state to escape a stale ref.

### `[ ]` Commit 6 — Wrap unguarded UGS calls (`KickPartyMemberAsync`)

- Wrap `session.AsHost().RemovePlayerAsync(targetId)` in try/catch.
- Log error with target id. State machine unchanged. Host can retry.

### `[ ]` Commit 7 — Event-driven trim (`Start` + `WaitForProfileInit`)

- `Start()` becomes `void Start() => HandleSignedInEvent();` (`HandleSignedInEvent` is
  already idempotent; deletes the awaitable polling there).
- `WaitForProfileInitAsync` switches from 100ms polling to one-shot `OnProfileChanged`
  subscribe + linked CTS timeout.

### `[ ]` Commit 8 — Single source of truth for `ActiveSession`

- Add `GameDataSO` to `PartySessionService` constructor (Reflex injection).
- Replace `PartySessionService.ActiveSession` private auto-property with property that
  reads/writes `_gameData.ActiveSession`. Backing field on `GameDataSO`.
- Update `HostConnectionService` where `_partySessionService` is constructed to pass
  `_gameData`.

### `[ ]` Commit 9 — `MultiplayerService.Instance` → class member

- `PartySessionService`: ctor takes optional `IMultiplayerService` (default
  `MultiplayerService.Instance`); stored as `_multiplayerService`; used at the
  current 2 callsites.
- `PresenceLobbyService`: same pattern; used at the current 3 callsites.

### `[ ]` Commit 10 — `LeavePartyKeepHostAsync` + adopt at every leave path

New method on `HostConnectionService` (or `PartySessionService` — TBD when reading
surrounding code; revise this commit's section before executing):

```csharp
public async UniTask LeavePartyKeepHostAsync()
{
    try
    {
        if (_partySessionService.ActiveSession != null)
            await _partySessionService.LeaveAsync();
    }
    catch (Exception ex)
    {
        Debug.LogError($"[HostConnectionService] LeavePartyKeepHostAsync: LeaveAsync threw: {ex}");
        // ref cleared inside LeaveAsync regardless; proceed.
    }
    await EnsurePartySessionAsync();
}
```

Caller updates:
- `LeavePartyAndReturnToMenuAsync` → use new path; drop `ShutdownAsync` call.
- `KickPartyMemberAsync` self-kick path → use new path.
- `RecoverFromFailedTransitionAsync` → use new path (replaces explicit `ClearSession` +
  `EnsurePartySessionAsync` chain from commit #5; revise commit #5 if this lands first).

### `[ ]` Commit 11 — Refresh error classification (transient vs definite)

- Add helper `IsDefiniteSessionGoneException(Exception)`:
  - HTTP 404 from UGS
  - `SessionException` with code/message indicating "not found" / "deleted"
  - Inner `WebRequestException` with 404
- Read surrounding SDK code before finalising the predicate; revise this commit's
  section before executing.
- In `RefreshPartyMembersAsync` catch:
  - If `IsDefiniteSessionGoneException` → call `HandleDefiniteSessionGoneAsync()` (commit #12).
  - Otherwise → existing transient path (log + retry next tick).

### `[ ]` Commit 12 — Auto-recover on definite session-gone

New `HandleDefiniteSessionGoneAsync()`:
1. Snapshot current `_connectionData.PartyMembers` list.
2. Call `_partySessionService.ClearSession()` (state escape — invariant exception #7).
3. Raise `OnPartyMemberLeft` for each member except the local player (UI clears slots).
4. Raise `OnHostConnectionLost` (UI toast: "Party connection lost").
5. Call `LeavePartyKeepHostAsync()` (creates fresh solo session, no NM cycle).

Result: UI never shows stale "in party". No manual action required.

### `[ ]` Commit 13 — `OnDestroy` null guards + `Debug.LogError`

- Replace `OnDestroy` with the version from Q8 above. Every nullable branch logs an
  error naming the field + suspected cause.

### `[ ]` Commit 14 — Comment cleanup

- Rewrite every "lazy Relay" / "Phase 15 Always InParty" comment in
  `HostConnectionService.cs` (lines 333–338, 810–815, 826–827, 894–903, 1142–1148) to
  describe the current eager architecture in plain language.

### `[ ]` Commit 15 — Documentation roadmap + `CLAUDE.md` additions

- This doc IS the roadmap. Update its status sections (completed commits log, deferred
  items, exit-criteria check) to reflect the final state.
- Update `CLAUDE.md`:
  - Add to "Anti-patterns to avoid": *"Calling a UGS / Netcode singleton `*.Instance`
    repeatedly inside a service class — cache once as a constructor-injected field,
    fall back to `?? *.Instance` so non-DI callers still work."*
  - Confirm `PARTY_SYSTEM_REFACTOR.md` is in the documentation index.

## Deferred (captured here, future commits)

- Full event-driven `EnsureInitializedAsync` refactor (replace sequential awaits with
  state-machine-driven SOAP transitions, add `WaitingForProfile` /
  `JoiningPresenceLobby` states, delete `_joining` flag).
- `PartyStateMachine` expansion — more observable conditions for UI direct-subscribe.
- Extract `RefreshErrorPolicy` helper (fold `_rateLimitBackoffUntil`,
  `_consecutiveRefreshErrors`, classification predicate).
- `GameDataSO` Single-Responsibility split — pull session ownership into dedicated SOAP
  container.
- MPPM-driven play-mode integration tests for accept / decline / leave / refresh-fail /
  session-gone-auto-recovery.

## Unbreakable exit criteria — when do we stop?

**All of these hold simultaneously in editor + 2-VP MPPM verification:**

1. **No fatal failures.** Inviting, accepting, leaving, kicking, refresh failures
   never:
   - Despawn the host's menu vessel mid-session.
   - Crash with an NRE in `OnDestroy`.
   - Kick joined clients due to a host-side transient refresh error.
2. **No stuck UI.** A user never has to explicitly "leave" to clear stale state. If the
   server deletes the session, the UI updates within one refresh tick (≤3s) and the
   user is back in a solo party slot.
3. **One source of truth.** `_partySessionService.ActiveSession` and
   `gameData.ActiveSession` are reference-equal at every observation point.
4. **No silent failures.** Every catch site either restores state, transitions the state
   machine, or logs a `Debug.LogError` with stack and context. No `catch (Exception) {}`.
5. **No `ActiveSession = null` outside intentional leave.** Verified by `grep` across
   the three service files.
6. **All existing edit-mode tests green** (`CosmicShore.Multiplayer.Tests`,
   `CosmicShore.Tests.EditMode`).
7. **2-VP MPPM happy path**: VP-A creates party, VP-B accepts invite. Both vessels
   replicate. Both clients can fly. No `[Player] OnNetworkDespawn` on host during invite
   send. VP-B fade-overlay clears within pair init.
8. **2-VP MPPM failure path**: Force VP-A to delete the session via UGS dashboard mid-
   session. VP-A's UI returns to solo state within 3s. VP-B's UI shows party-lost toast
   and reverts to solo state. No crash on either client.

Once all 8 hold, the unbreakable goal is met for this surface. This doc records any
remaining deferred items; it lives on as the source of truth for ongoing
refinement.

## Per-commit revision protocol

Before starting any commit N:

1. **Read the relevant source files fresh** (`HostConnectionService.cs`,
   `PartySessionService.cs`, `PresenceLobbyService.cs`,
   `PartyInviteController.cs`, etc.).
2. **Present the current state of every method this commit touches**:
   - The full method source (verbatim from the file).
   - Every caller / callsite (file:line + the calling method's name).
   - Every method this method calls into (file:line + the called method's name).
   - A 1-2 sentence explanation of what the method currently does and what we'll
     change.
3. **Re-check whether the assumptions in this commit's section still hold** (line numbers,
   method signatures, surrounding catch behavior).
4. **Update this file**: rewrite commit N's section to reflect any new findings, including
   the method dumps from step 2. Note anything that affects later commits.
5. **Then start coding.** Commit. Update commit N's status to `[x]`. Note any unexpected
   behavior in the section.
6. **Re-evaluate the exit criteria.** If satisfied, stop and update the deferred-items
   section. Otherwise, continue to commit N+1.

This is how we keep the plan accurate as the codebase changes underneath us, and how
the user keeps visibility into every method we touch.
