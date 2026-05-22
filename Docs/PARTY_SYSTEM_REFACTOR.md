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

### `[x]` Commit 1 — Dead-code removal (no behaviour change)

**Outcome**: `HostConnectionService.cs` shrunk from 1570 → 1425 lines (-145).
Four dead methods removed (`ClearStalePartySession`, `CreatePartySessionPublicAsync`,
`CreatePartySessionAsync`, `CreatePartySessionCoreAsync`). Lobby-patcher log-filter
plumbing removed (field, install/uninstall, nested class). `IsBenignLobbyPatcherError`
retained for the refresh-loop catch block at line 1052. Three tests in
`PartyAcceptFlowPlayModeTests` (`LobbyPatcherFilter_*`) deleted; one reflection
assertion in `PartyInviteSystemTests` removed. Stale comments referencing the
deleted methods cleaned up in `NetworkTransitionService.cs`,
`AcceptanceSignalService.cs`, `PartySessionService.cs`, and at two locations
in `HostConnectionService.cs` itself.

**Deviations from original plan**:
1. The "stop-gap NM-listening guard in `RefreshPartyMembersAsync`" the plan called
   for reverting **did not exist** in live source — the catch block was already
   simplified in surgical Bug A fix `edfa1be`. No revert needed.
2. Implication: Commit 2's primary work ("simplify `RefreshPartyMembersAsync` catch")
   is already shipped. Commit 2 reduces to verifying the catch shape and possibly
   adding the classification scaffolding for Commit 11. **Re-scope Commit 2 before
   executing it.**

**Pre-commit findings** (preserved for audit trail):


**Pre-commit findings** (re-read of live source on `claude/blissful-tesla-9nefa`):

Methods to delete (full source verified in `HostConnectionService.cs`):

- `ClearStalePartySession` (lines 643–656) — public. Callers (grepped): **none** in any `.cs` file. Used to be called from `SceneLoader`; the only remaining reference is a stale `<see cref>` in this file's own XML doc comment. Calls into `_partySessionService.ClearSession()`, `_memberService.ClearSilent()`, `ClearJoinedPartyAsync().Forget()`, conditional `CreateOwnPartySessionAsync()`. Pure dead code.
- `CreatePartySessionPublicAsync` (lines 658–665) — public. The doc comment explicitly says "Reserved; no current callers." Wraps `SyncLocalIdentity()` + `CreateOwnPartySessionAsync()`. Pure dead code, but **`PartyInviteSystemTests.cs:1085` asserts via reflection that this public method exists** — that assertion must be removed.
- `CreatePartySessionAsync` (lines 671–688) — private. Only caller: itself recursively + `CreatePartySessionCoreAsync`. Mutex-guarded thin wrapper around `CreatePartySessionCoreAsync`. Pure dead code.
- `CreatePartySessionCoreAsync` (lines 690–710) — private. Only caller: `CreatePartySessionAsync` (line 682). Calls into `_networkTransition.ShutdownAsync`, `_partySessionService.CreateAsync`, `_scheduler.ResetDeferred`. Pure dead code.

`LobbyPatcherLogFilter` plumbing:
- Field `_originalLogHandler` (line 177) — only used by `InstallLobbyLogFilter` / `UninstallLobbyLogFilter`. Delete.
- `InstallLobbyLogFilter()` (line 242 call, line 1497 def) — installs a global Unity log handler that suppresses noise. The global swap is heavy-handed; `IsBenignLobbyPatcherError` already gives us per-catch suppression at the only place that matters (`RefreshPartyMembersAsync`). Delete.
- `UninstallLobbyLogFilter()` (line 276 call, line 1503 def) — pairs with install. Delete.
- Nested class `LobbyPatcherLogFilter` (lines 1516–1549) — implements `ILogHandler`. Delete.
- **`PartyAcceptFlowPlayModeTests.cs` reflects into the nested class** to call `ContainsLobbyPatcherIndexError` from three tests (`LobbyPatcherFilter_MatchesLegacySdkMessage`, `LobbyPatcherFilter_MatchesCurrentSdkMessage`, `LobbyPatcherFilter_IgnoresUnrelatedMessage`). Those three tests + the `InvokeContainsLobbyPatcherIndexError` helper + the `_containsMethod` field must be deleted.
- `IsBenignLobbyPatcherError` (line 1558) — **kept**. Called at line 861 and line 1130 (`RefreshPartyMembersAsync` catch block). Four reflection-tests in `PartyAcceptFlowPlayModeTests.cs` continue to pass.

`RefreshPartyMembersAsync` catch block (lines 1113–1171):
- **The "stop-gap NM-listening guard" the original plan said to revert does NOT exist in current source.** The catch block was already simplified in commit `edfa1be` (Bug A surgical mitigation): benign → return; rate-limit → backoff; everything else → log and return without `ClearSession()`. Commit 1 has nothing to do here.
- The catch block does contain a multi-line comment (lines 1142–1158) that references "Bug A (Docs/PARTY_INVITE_DEBUGGING.md §2)" — that doc is now deleted. The reference must be updated to point at this doc (`Docs/PARTY_SYSTEM_REFACTOR.md`) or removed.
- **This makes Commit 2 a near-no-op too** — the simplification it was supposed to make is already shipped. Commit 2 reduces to (a) update or remove that stale doc reference, (b) confirm classification structure for later Commit 11.

Stale comments in other files referencing now-deleted methods (clean up here, since they directly name methods that no longer exist):
- `Assets/_Scripts/Controller/Party/Services/NetworkTransitionService.cs:12` — "(used by `HostConnectionService.CreatePartySessionCoreAsync`)".
- `Assets/_Scripts/Controller/Party/Services/AcceptanceSignalService.cs:157` — "CreatePartySessionAsync can take 2-3s..."
- `Assets/_Scripts/Controller/Party/Services/PartySessionService.cs:7` — "the CreatePartySessionCoreAsync / JoinSessionByIdAsync logic lived in".
- `Assets/_Scripts/Controller/Party/HostConnectionService.cs:33` — `<see cref="LobbyPatcherLogFilter"/>` in the XML doc summary list.
- `Assets/_Scripts/Controller/Party/HostConnectionService.cs:814` — "only after CreatePartySessionAsync succeeds, and lazy creation".

**Execution plan**:

1. `HostConnectionService.cs` edits:
   - Delete `_originalLogHandler` field (line 177).
   - Delete `InstallLobbyLogFilter()` call from `Awake()` (line 242).
   - Delete `UninstallLobbyLogFilter()` call from `OnDestroy()` (line 276).
   - Delete `ClearStalePartySession`, `CreatePartySessionPublicAsync`, `CreatePartySessionAsync`, `CreatePartySessionCoreAsync` methods.
   - Delete `InstallLobbyLogFilter` / `UninstallLobbyLogFilter` definitions + `LobbyPatcherLogFilter` nested class.
   - Update the catch-block comment in `RefreshPartyMembersAsync` to drop the deleted-doc reference.
   - Update the `<see cref="LobbyPatcherLogFilter"/>` line in the class XML doc summary (line 33) to just remove the line.
   - Update the misleading "lazy creation" comment at line 814 to describe current eager architecture (or simply remove the parenthetical, since Commit 14 handles comment cleanup fully).
2. `PartyInviteSystemTests.cs` — delete the single `Assert.IsNotNull(...CreatePartySessionPublicAsync...)` line (1085–1086) and tighten the surrounding test.
3. `PartyAcceptFlowPlayModeTests.cs` — delete the three `LobbyPatcherFilter_*` tests and the `InvokeContainsLobbyPatcherIndexError` helper + `_containsMethod` field + the related Fix-1 doc comment block.
4. Stale-comment fixes in `NetworkTransitionService.cs`, `AcceptanceSignalService.cs`, `PartySessionService.cs` — drop the dead-method names.
5. Compile, run edit-mode tests (`CosmicShore.Multiplayer.Tests`, `CosmicShore.Tests.EditMode`) — both should pass.
6. Commit with `chore(party): remove dead methods + lobby-patcher log filter plumbing`.

### `[x]` Commit 2 — Annotate `RefreshPartyMembersAsync` catch (re-scoped)

**Outcome**: catch block already matches the locked-design shape — Bug A's primary
fix shipped in `edfa1be` (verified during Commit 1 re-read). This commit is a
small in-code documentation pass that connects the catch to the doc's
error-handling matrix and seeds a breadcrumb for Commits 11/12.

**Pre-commit findings** (live source on `claude/blissful-tesla-9nefa`, post-Commit 1):

`RefreshPartyMembersAsync` catch (`HostConnectionService.cs:1044-1071`):
- Three branches already present: benign → swallow; rate-limit → backoff; everything
  else → log + return without `ClearSession()`.
- No `ClearSession()` calls anywhere in this catch. ✓ locked decision: `ActiveSession`
  is never nulled outside intentional leave.

Outer `RefreshAsync` catch (`HostConnectionService.cs:775-799`):
- Same three-branch shape for the presence-lobby refresh path. Threshold
  reconnect (`_consecutiveRefreshErrors >= 3 → _lobbyService.ForceReset()`)
  touches the presence lobby only — does NOT clear `ActiveSession`. Safe.

**Changes**:
1. In-code: replaced the catch block comments in `RefreshPartyMembersAsync` with
   `[benign]` / `[rate-limit]` / `[transient]` markers tied to the error-handling
   matrix in this doc, and a `TODO (Commits 11/12)` breadcrumb noting where the
   transient → definite split will land.
2. Doc: this section rewritten to reflect actual scope.

The original Commit 2 scope ("replace broad catch with three-branch shape") was
already shipped; the locked design's promotion from transient → definite on
threshold is deferred to Commits 11/12 where the new `IsDefiniteSessionGoneException`
predicate and `HandleDefiniteSessionGoneAsync` recovery action are designed.

**No tests added** — this is in-code documentation only; the catch behavior is
unchanged and existing reflection tests for `IsBenignLobbyPatcherError` already
cover the surface that matters.

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
