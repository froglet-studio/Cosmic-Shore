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

### `[x]` Commit 3 — Guard helper properties + delete `_initialized`

**Outcome**: `_initialized` field deleted (declaration + 2 writes + 4 reads).
Three helper properties added (`IsInitialized`, `IsInPresenceLobby`, `IsHostingParty`).
`using Unity.Netcode;` added for `NetworkManager.Singleton` access in `IsHostingParty`.
`HostConnectionService.cs`: 1425 → 1467 lines (+42, from helper-property block + XML
doc; net code-line reduction from field removal). Brace balance verified.

The `CreateOwnPartySessionAsync` double-check (state == InParty && ActiveSession != null)
deferred to Commit 4 per the pre-commit decision — it would change behavior in edge
cases (state-says-InParty but NM externally shut down) and belongs with the
`EnsurePartySessionAsync` introduction.

`IsHostingParty` is unused in this commit but defined now — Commits 4 and 10 will be
its first callers. Defining it here keeps Commit 3 a single conceptual unit (helper
predicates) and avoids fragmenting the property block across multiple commits.

**Pre-commit findings** (preserved for audit trail):


`_initialized` field surface in `HostConnectionService.cs`:

| Line | Site | Current code | Maps to |
|---|---|---|---|
| 167 | declaration | `private bool _initialized;` | DELETE |
| 257 | `Update()` guard | `if (!_initialized \|\| _lobbyService.ActiveLobby == null) return;` | `if (!IsInPresenceLobby) return;` |
| 316 | `HandleSignedOutEvent` write | `_initialized = false;` | DELETE (next line transitions to Disconnected — same effect on `IsInitialized`) |
| 337 | `EnsureInitializedAsync` guard | `if (_initialized \|\| _joining) return;` | `if (IsInPresenceLobby \|\| _joining) return;` |
| 358 | `EnsureInitializedAsync` write | `_initialized = true;` | DELETE (next line transitions to InPresenceLobby — same effect on `IsInitialized`) |
| 622 | `ForceRefreshNow` guard | `if (!_initialized \|\| _lobbyService.ActiveLobby == null) return;` | `if (!IsInPresenceLobby) return;` |

State-machine double-check at `CreateOwnPartySessionAsync` lines 652-654:
```csharp
if (_stateMachine.CurrentState == PartyState.InParty &&
    _partySessionService.ActiveSession != null)
    return;
```
Not replaced in this commit. The locked-design `IsHostingParty` adds NM `IsListening`
and `IsServer` checks, which would change behavior in edge cases (state-machine says
InParty but NM externally shut down → old returns no-op, new would proceed to recreate).
**Deferred to Commit 4** where `EnsurePartySessionAsync` introduces `if (IsHostingParty) return;`
as the canonical idempotent guard.

Other `_stateMachine.CurrentState ==` reads (lines 443, 656, 1087) are transition
guards, not double-checks — they decide *which* transition to fire, not whether to
short-circuit. Left alone.

External readers of HCS `_initialized`: **none**. Other classes (`FriendsInitializer`,
`NetworkVolumeUIController`, `FriendsServiceFacade`) have unrelated fields with the
same name. Safe to delete.

`Unity.Netcode` namespace: not currently imported in `HostConnectionService.cs`.
Need to add `using Unity.Netcode;` for `NetworkManager.Singleton` access in
`IsHostingParty`.

**Execution plan**:

1. Add `using Unity.Netcode;` to the imports.
2. Add three helper properties near the existing read-only state region (around line 206 where `PartySession` and `StateMachine` live):
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
3. Replace the four reads (lines 257, 337, 622) per the table.
4. Delete the two writes (lines 316, 358).
5. Delete the field declaration (line 167).
6. Compile, run existing edit-mode tests (no test changes expected).

### `[x]` Commit 4 — `EnsurePartySessionAsync` introduction (merged with original Commit 5)

**Outcome**: `CreateOwnPartySessionAsync` (private) renamed to `EnsurePartySessionAsync`
(public). Idempotent guard `if (IsHostingParty) return;` added at the fast-path AND
post-mutex positions. `RetryCreateOwnPartySessionAsync` deleted. `ClearPartySessionRef`
added as the single explicit clear escape hatch (used only by
`PartyInviteController.RecoverFromFailedTransitionAsync`). All 8 callsites (5 internal
to HCS, 2 in `PartyInviteController`, 1 in `AuthenticationSceneController`) updated.
Stale comments at lines 437/443, 862/866, 1110/1113 in HCS rewritten. Tooltip on
`bootStatusRetryRequestedEvent` updated. Comment in
`AuthenticationSceneController.cs:478` updated.

Behavior change (intentional, endorsed by locked decisions): the post-mutex
double-check is now `IsHostingParty` (which requires `NM.IsListening && NM.IsServer`
in addition to `state == InParty && ActiveSession != null`). This catches the edge
case where the state machine thinks we're InParty but NM was externally shut down.
Three callsites that previously called `RetryCreateOwnPartySessionAsync`
(implicit `ClearSession` + recreate) now call `EnsurePartySessionAsync` directly
(idempotent) — they were never the recovery path, so dropping their implicit clear
is correct behavior under the locked invariant "`ActiveSession` is never nulled
outside an intentional leave."

The `CancellationToken` parameter on the old `RetryCreate...` wrapper was dead weight
(never threaded into the actual work) and is dropped at all callsites.

`HostConnectionService.cs`: 1467 → 1477 lines (+10 from expanded XML doc on
`EnsurePartySessionAsync` and the new `ClearPartySessionRef` method). Brace balance
123/123. Zero remaining references to old method names anywhere under `Assets/_Scripts/`.

**Pre-commit findings** (preserved for audit trail):


**Decision**: original Commits 4 and 5 merged into a single atomic change. Reason:
deleting `RetryCreateOwnPartySessionAsync` (Commit 4) without simultaneously updating
its three external callers in `PartyInviteController` and `AuthenticationSceneController`
breaks the build, which violates the locked protocol's "each commit compiles" rule.
Commits 4 and 5 are conceptually two cognitive units (rename + idempotency, then
caller rewiring) but at the source-of-truth level they are one atomic change.

The numbering is preserved — Commit 5 is now empty / merged here.

**Pre-commit findings** (live source post-Commit 3):

`CreateOwnPartySessionAsync` callers (internal, all in HCS):

| Line | Method | Treatment |
|---|---|---|
| 404 | `EnsureInitializedAsync` | rename → `EnsurePartySessionAsync()` |
| 445 | `SendInviteAsync` (fallback when session lost) | rename |
| 550 | JoiningParty → HostingParty transition | rename |
| 616 | `LeavePartyAsync` | rename |

`RetryCreateOwnPartySessionAsync` callers:

| Site | Caller | Currently does | New behavior |
|---|---|---|---|
| HCS:322 | `HandleBootStatusRetryRequested` | `RetryCreate...Forget()` | `EnsurePartySessionAsync().Forget()` |
| PartyInviteController:277 | `LeavePartyAndReturnToMenuAsync` | `hcs.RetryCreate...(ct).AsMainThread()` | `hcs.EnsurePartySessionAsync().AsMainThread()` |
| PartyInviteController:350 | `RecoverFromFailedTransitionAsync` | `hcs.RetryCreate...().AsMainThread()` | `hcs.ClearPartySessionRef();` then `await hcs.EnsurePartySessionAsync().AsMainThread()` |
| AuthenticationSceneController:465 | initial create retry loop | `hcs.RetryCreate...(ct).AsMainThread()` | `hcs.EnsurePartySessionAsync().AsMainThread()` |

The `CancellationToken` parameter on `RetryCreateOwnPartySessionAsync` was effectively
a no-op — the wrapper never threaded it into `CreateOwnPartySessionAsync` (which had
no ct param). New `EnsurePartySessionAsync()` keeps the no-param signature.

**Idempotent guard placement**:
```csharp
public async UniTask EnsurePartySessionAsync()
{
    if (IsHostingParty) return;       // fast path

    await _sessionCreationMutex.WaitAsync();
    try
    {
        if (IsHostingParty) return;   // double-check after mutex serialises concurrent callers
        // ... existing creation body, with the old (state==InParty && ActiveSession!=null) check removed
    }
    finally { _sessionCreationMutex.Release(); }
}
```

The post-mutex `IsHostingParty` check REPLACES the old `(state == InParty && ActiveSession != null)`
double-check at lines 687-690. This is a deliberate semantic strengthening — `IsHostingParty`
also requires `NM.IsListening && NM.IsServer`, so it catches the edge case where the state
machine says InParty but NM was externally shut down. Endorsed by the locked decision:
"One public create-or-no-op surface: `EnsurePartySessionAsync` — idempotent (no-op if
`IsHostingParty`, create otherwise)."

**`ClearPartySessionRef` accessor**:

`RecoverFromFailedTransitionAsync` needs to drop a stale `ActiveSession` ref before
creating fresh. `_partySessionService` is private in HCS, so a narrow public method is
added:
```csharp
public void ClearPartySessionRef() => _partySessionService.ClearSession();
```
Documented as the single escape hatch from a stale ref, intended for the recovery path
only. Commit 10 (`LeavePartyKeepHostAsync`) will subsume this pattern and may make the
method internal again or delete it.

**Stale-comment updates** (in-line, not behavior):
- HCS:437, 443 — comments mentioning `CreateOwnPartySessionAsync` by name → update to `EnsurePartySessionAsync`
- HCS:852, 856 — comments mentioning `RetryCreateOwnPartySessionAsync` → update to `EnsurePartySessionAsync`
- HCS:1100, 1103 — comments in `RefreshPartyMembersAsync` referencing `CreateOwnPartySessionAsync` and `RetryCreateOwnPartySessionAsync` chain
- HCS:53 — tooltip on `bootStatusRetryRequestedEvent`
- AuthenticationSceneController:478 — comment mentioning `RetryCreateOwnPartySessionAsync`

**Execution plan**:

1. Rename `private async UniTask CreateOwnPartySessionAsync()` → `public async UniTask EnsurePartySessionAsync()`.
2. Add fast-path idempotent guard `if (IsHostingParty) return;` before the mutex wait.
3. Replace the post-mutex double-check (state == InParty && ActiveSession != null) with `if (IsHostingParty) return;`.
4. Delete `RetryCreateOwnPartySessionAsync` (lines 725-734).
5. Add `public void ClearPartySessionRef() => _partySessionService.ClearSession();`.
6. Update all 5 internal HCS call sites (line 322 fire-and-forget, lines 404/445/550/616 awaited).
7. Update `PartyInviteController.cs` line 277 (drop ct) and line 350 (clear-then-ensure).
8. Update `AuthenticationSceneController.cs` line 465 (drop ct).
9. Stale-comment fixes across the 5 listed sites + tooltip + AuthenticationSceneController comment.

### `[merged]` Commit 5 — Update `EnsurePartySessionAsync` callsites

Merged into Commit 4 above. See "Decision" note at the top of Commit 4.

### `[x]` Commit 6 — Improve `KickPartyMemberAsync` catch diagnostics (re-scoped)

**Outcome**: catch-block log message expanded to include the target `playerId` and
the exception type name, aligning with the rest of the file's catch-log convention.
A short comment ties the catch to the error-handling matrix. No behavior change —
the wrap itself was already shipped (the plan's matrix entry assumed it was
unguarded, which is stale).

`HostConnectionService.cs`: 1477 → 1483 lines (+6 for the expanded message + comment).
Brace balance 123/123.

**Pre-commit findings** (preserved for audit trail):


**Pre-commit findings** (live source post-Commit 4):

`KickPartyMemberAsync` at `HostConnectionService.cs:618-645`:
```csharp
public async UniTask KickPartyMemberAsync(string playerId)
{
    if (!connectionData.IsPartyHost) { LogWarning("Only the party host..."); return; }
    if (playerId == connectionData.LocalPlayerId) { LogWarning("Cannot kick yourself..."); return; }

    connectionData.RemovePartyMember(playerId);

    if (_partySessionService.ActiveSession != null)
    {
        try
        {
            await _partySessionService.ActiveSession.AsHost().RemovePlayerAsync(playerId).AsMainThread();
            Debug.Log($"[HostConnectionService] Kicked {playerId} from party session.");
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[HostConnectionService] KickPartyMember session error: {e.Message}");
        }
    }
}
```

The `RemovePlayerAsync` call **is already wrapped** in try/catch (lines 635-643).
The catch logs a warning and leaves state unchanged — exactly the behavior the
locked design's error-handling matrix specifies. **The plan's matrix entry**
("Currently unwrapped — propagates. Wrap in try/catch...") **is stale** — likely
shipped in an earlier surgical fix.

Callers: invoked from UI (`KickPartyMemberAsync` is part of HCS's public API per
`PartyInviteSystemTests.cs:1083`). Per matrix: "Log, state unchanged. Host can retry."

What's left to improve:
1. The log omits the target `playerId`. Plan says explicitly "Log error with target id."
2. The log omits `e.GetType().Name`, inconsistent with the rest of the file (e.g.
   line 1078: `({e.GetType().Name}): {e.Message}`).

`connectionData.RemovePartyMember(playerId)` runs unconditionally before the
UGS call. That's a local-only SOAP mutation — UI sees the member gone immediately,
even if the UGS-side kick fails. If the target is still in the session, they'll
reappear on the next refresh tick. Best-effort semantics, correct for kick.

**Execution plan**:

1. Improve the catch log: include `playerId` and exception type name. Single-line edit.

No structural change. No new methods. No state-machine work.

### `[x]` Commit 7 — Event-driven trim (`Start` + `WaitForProfileInit`)

**Outcome**: two polling loops in `HostConnectionService.cs` replaced with
event-driven equivalents. No behavior change; both paths reach the same downstream
state, and the profile-wait timeout semantics are preserved.

1. `Start()`: dropped `while (!IsAuthSignedInAndHasId) await UniTask.Delay(300)`.
   Now `void Start() => HandleSignedInEvent()` (plus an explanatory comment). The
   SOAP `OnSignedIn` event (wired via inspector `EventListener`) handles the
   signed-in-after-Start case; the direct `HandleSignedInEvent()` call handles
   the signed-in-before-Start case. Both paths are idempotent through
   `EnsureInitializedAsync`'s `IsInPresenceLobby || _joining` guard.

2. `WaitForProfileInitAsync`: dropped `while (!IsInitialized && elapsed < timeoutMs)
   await UniTask.Delay(100)`. Now subscribes to `playerDataService.OnProfileChanged`
   and waits on a `UniTaskCompletionSource` with `AttachExternalCancellation` for
   the timeout (linked `CancellationTokenSource(timeoutMs)`). Race-free because
   `PlayerDataService.HandleDataServiceReady` sets `IsInitialized = true` *before*
   raising `OnProfileChanged`. Re-checks `IsInitialized` after subscription to
   close the early-return/subscribe race window.

`HostConnectionService.cs`: 1483 → 1509 lines (+26). Brace balance 126/126.

**Pre-commit findings** (preserved for audit trail):


**Pre-commit findings** (live source post-Commit 6):

`Start()` at `HostConnectionService.cs:282-289`:
```csharp
async void Start()
{
    while (!IsAuthSignedInAndHasId())
        await UniTask.Delay(300);
    await EnsureInitializedAsync();
}
```
Polls auth every 300ms until signed in. The polling exists because auth can complete
either BEFORE or AFTER HCS's `Start` runs. If before: poll catches it immediately.
If after: an `EventListener` SOAP component (wired in inspector to
`HandleSignedInEvent`) catches the event. So the poll exists only for the
already-signed-in case.

`HandleSignedInEvent` at `HostConnectionService.cs:344-348`:
```csharp
public async void HandleSignedInEvent()
{
    if (!IsAuthSignedInAndHasId()) return;
    await EnsureInitializedAsync();
}
```
Public method. No internal C# caller — invoked by an `EventListenerNoParam`
wired in the inspector that subscribes to the SOAP `OnSignedIn` event.
Idempotent: gates on `IsAuthSignedInAndHasId()` and delegates to
`EnsureInitializedAsync`, which itself guards `IsInPresenceLobby || _joining`.

`WaitForProfileInitAsync` at `HostConnectionService.cs:1353-1369`:
```csharp
if (playerDataService == null || playerDataService.IsInitialized) return;
int elapsed = 0;
const int stepMs = 100;
while (!playerDataService.IsInitialized && elapsed < timeoutMs)
{
    await UniTask.Delay(stepMs);
    elapsed += stepMs;
}
if (!playerDataService.IsInitialized) Debug.LogWarning(...);
```
Polls `IsInitialized` every 100ms. Only caller: `EnsureInitializedAsync` at line 380.

`PlayerDataService.HandleDataServiceReady` (line 72-82 of `PlayerDataService.cs`):
```csharp
IsInitialized = true;
OnProfileChanged?.Invoke(CurrentProfile);
```
**`IsInitialized` flips true IMMEDIATELY before `OnProfileChanged` fires.** So
subscribing to `OnProfileChanged` and completing when `IsInitialized == true`
is equivalent to waiting for the init flag to flip. Race-free.

`OnProfileChanged` signature: `event Action<PlayerProfileData>`. Subsequent
invocations (profile mutations) also occur with `IsInitialized == true`, so a
"complete on next event where IsInitialized" handler still works post-init.

Canonical event-driven pattern in the codebase (`AuthenticationSceneController.cs:520`):
```csharp
var tcs = new UniTaskCompletionSource();
void OnEvent() { if (condition) tcs.TrySetResult(); }
soapEvent.OnRaised += OnEvent;
try { await tcs.Task.AttachExternalCancellation(ct); }
finally { soapEvent.OnRaised -= OnEvent; }
```
`AttachExternalCancellation` makes a `UniTask` cancellation-aware so a linked
`CancellationTokenSource(timeoutMs)` produces the timeout cleanly.

**Execution plan**:

1. Replace `Start()` polling loop with `void Start() => HandleSignedInEvent();`.
   Fire-and-forget call. If auth isn't yet signed in, `HandleSignedInEvent` returns
   immediately; the SOAP `OnSignedIn` event later wakes HCS via the inspector-wired
   `EventListener`. If auth IS signed in, init runs now. Q9 in this doc confirms safety.

2. Rewrite `WaitForProfileInitAsync` as:
   - Early-return on null service or already-initialized.
   - Linked `CancellationTokenSource(timeoutMs)` for timeout.
   - `UniTaskCompletionSource` + `OnProfileChanged` handler that completes when
     `IsInitialized == true`.
   - Subscribe → re-check (closes the race between early-return and subscribe) →
     `await tcs.Task.AttachExternalCancellation(cts.Token)` → unsubscribe.
   - On `OperationCanceledException`: log warning (preserved verbatim from old code).

No additional callsites change. Behavior change: zero — both paths reach
`EnsureInitializedAsync` exactly once, and the profile-wait timeout semantics
are preserved.

### `[x]` Commit 8 — Single source of truth for `ActiveSession`

**Outcome**: `PartySessionService.ActiveSession` is now a derived property over
`_gameData.ActiveSession` — one backing field, two access surfaces, reference-equal
at every observation point. The "ActiveSession is never nulled outside an
intentional leave" invariant now actually holds.

Eight files touched:

| File | Change |
|---|---|
| `PartySessionService.cs` | + `GameDataSO _gameData` field, ctor param. `ActiveSession` property reads/writes `_gameData.ActiveSession`. |
| `AppManager.cs` | Factory at 413 resolves `GameDataSO` and passes it to the ctor. |
| `PartyInviteController.cs` | Removed tautological sync at lines 174-179 (`gameData.ActiveSession = HCS.PartySession`). |
| `QuickPlayButton.cs` | Removed tautological sync at lines 67-70. |
| `ArcadeGameConfigureModal.cs` | Removed tautological sync at lines 1108-1113. |
| `MultiplayerSetup.cs` | Removed invariant-violating `gameData.ActiveSession = null;` in `LeaveSession()`. Updated comment. |
| `GameDataSO.cs` | Removed `ActiveSession = null;` from `ResetAllData()`. Added comment explaining the invariant. |
| `SceneLoader.cs` | Updated stale comment that claimed `LeaveSession` nulls the ref. |

The two null removals (MultiplayerSetup line 410, ResetAllData line 313) were
side effects of the consolidation: under the old dual-field design those nulls
only cleared the game-side ref and HCS held onto the Relay through its own
field; under the unified design they would orphan the live Relay session. The
fix preserves the original comment intent ("Relay stays alive") and the locked
invariant.

`MultiplayerSetup.cs:442` (OnTransportFailure null after Delete/Leave) is a
legitimate intentional-leave and is preserved.

Brace balance verified across all 8 files. `PartySessionService.cs`: 285 → 315
lines (+30, mostly XML doc).

**Pre-commit findings** (preserved for audit trail):


**Pre-commit findings** (live source post-Commit 7):

`PartySessionService.cs`:
- Field declaration at line 92: `public ISession ActiveSession { get; private set; }`
  — independent backing field.
- Ctor at line 108: `public PartySessionService(HostConnectionDataSO connectionData)`.
- 4 internal writes (`CreateAsync:144`, `JoinByIdAsync:184`, `LeaveAsync:198` via
  `ClearSession`, `ClearSession:240`).
- ~9 internal reads (130, 144, 146, 184, 188, 197, 198, 221, 222, 238, 239).

`GameDataSO.cs:160`: `public ISession ActiveSession { get; set; }` — already exists as
a separate backing field; the consolidation target.

`AppManager.cs:413-417`: `PartySessionService` factory currently:
```csharp
builder.RegisterFactory<IPartySessionService>(
    _ => new PartySessionService(hostConnectionData),
    lifetime: Lifetime.Singleton,
    resolution: Resolution.Lazy);
```
Pattern for adding `GameDataSO` resolution mirrors `NetworkTransitionService.cs:426`:
`c => new NetworkTransitionService(c.Resolve<GameDataSO>())`.

**Three external manual-sync sites that become tautologies** (`HCS.PartySession`
already proxies `_partySessionService.ActiveSession`, which post-Commit-8 IS
`_gameData.ActiveSession`):
- `PartyInviteController.cs:176-177` — sync after `AcceptInviteAsync`. Delete.
- `QuickPlayButton.cs:68-69` — sync before launch. Delete.
- `ArcadeGameConfigureModal.cs:1111-1112` — sync before launch. Delete.

**Two `gameData.ActiveSession = null` writes that violate the locked invariant
post-consolidation**:

1. `MultiplayerSetup.LeaveSession:410`. The surrounding comment (lines 401-409)
   explicitly says: *"Phase 15 'Always InParty': gameData.ActiveSession IS the
   party Relay session. Do NOT delete or leave — HCS owns the session lifetime.
   Just clear the game reference; the Relay stays alive..."* Under the OLD dual-
   field design, "clear the game reference" only nulled `gameData.ActiveSession`
   while `_partySessionService.ActiveSession` stayed live — so HCS still held a
   valid ref. **Post-Commit-8 both refs are the same field — nulling here would
   orphan the live UGS Relay session.** The comment's intent is preserved by
   simply removing the null assignment.

2. `GameDataSO.ResetAllData:313`. Same hazard. Called from `SceneLoader.cs:319`
   (HandleActiveSessionEnd) and `AppManager.cs:508` (ConfigureMenuGameData) +
   bootstrap. Removing it preserves the Relay reference across game-end and menu
   resets, aligning with "Always InParty."

`MultiplayerSetup.cs:440` (OnTransportFailure) DOES legitimately null after
calling `DeleteAsync`/`LeaveAsync` — that's an intentional leave. Preserve.

`SceneLoader.cs:311-316` comment says "MultiplayerSetup.LeaveSession() already
nulled gameData.ActiveSession" — that observation becomes false after the fix in
step 4. Update comment to match.

**Execution plan**:

1. `PartySessionService.cs`:
   - Add `private readonly GameDataSO _gameData;` field.
   - Ctor: `public PartySessionService(HostConnectionDataSO connectionData, GameDataSO gameData)`, assign `_gameData = gameData;`.
   - Replace `public ISession ActiveSession { get; private set; }` with property that reads/writes `_gameData.ActiveSession`.
   - The 9 internal reads + 4 internal writes via the property work unchanged.

2. `AppManager.cs:413-417`: change factory to `c => new PartySessionService(hostConnectionData, c.Resolve<GameDataSO>())`.

3. Delete tautological syncs at `PartyInviteController.cs:174-179`, `QuickPlayButton.cs:67-70`, `ArcadeGameConfigureModal.cs:1108-1113`. Update surrounding comments.

4. Delete `gameData.ActiveSession = null;` at `MultiplayerSetup.cs:410`. Update the surrounding comment to drop the now-incorrect "Just clear the game reference" phrasing.

5. Delete `ActiveSession = null;` at `GameDataSO.cs:313`. Add a comment noting why (the locked invariant; session lifetime owned by HCS / explicit leave paths).

6. Update stale comment at `SceneLoader.cs:311-316`.

Behavior change summary: post-Commit-8, the Relay session reference survives all
non-leave operations (game-end, menu reset, scene transitions). HCS's
`IsHostingParty` predicate stays true across these transitions, which is the
correct "Always InParty" behavior. This is a fix for a latent regression that
the plan's source-of-truth consolidation exposes.

### `[x]` Commit 9 — `MultiplayerService.Instance` → class member

**Outcome**: 5 of 8 `MultiplayerService.Instance` callsites in the codebase now go
through cached fields. `PartySessionService` and `PresenceLobbyService` each have a
`private readonly IMultiplayerService _multiplayer` field set at construction with
an optional ctor parameter (defaults to `MultiplayerService.Instance`). Tests can
substitute a fake without DI changes. AppManager DI factories are unchanged — the
optional `null` parameter flows through and the ctors apply the default.

The remaining 3 callsites in `MultiplayerSetup.cs` (game-launch path) are out of
the Commit 9 scope per plan.

Brace balance verified for both touched files (`PartySessionService` 30/30,
`PresenceLobbyService` 47/47).

**Pre-commit findings** (preserved for audit trail):


**Pre-commit findings** (live source post-Commit 8):

`PartySessionService.cs` — 2 callsites:
- `CreateAsync:161` — `MultiplayerService.Instance.CreateSessionAsync(opts)`
- `JoinByIdAsync:201` — `MultiplayerService.Instance.JoinSessionByIdAsync(...)`

`PresenceLobbyService.cs` — 3 callsites:
- `QuerySessionsAsync:299` — `MultiplayerService.Instance.QuerySessionsAsync(...)`
- `JoinSessionByIdAsync:320` — `MultiplayerService.Instance.JoinSessionByIdAsync(...)`
- `CreateSessionAsync:368` — `MultiplayerService.Instance.CreateSessionAsync(opts)`

Other `MultiplayerService.Instance` callsites in the codebase (out of Commit 9 scope):
- `MultiplayerSetup.cs:275, 300, 319` — game-launch path, owned by a separate flow.

Both services are constructed via Reflex DI factories in `AppManager`:
- `PartySessionService` at `AppManager.cs:413` — `new PartySessionService(hostConnectionData, c.Resolve<GameDataSO>())`
- `PresenceLobbyService` at `AppManager.cs:407` — `new PresenceLobbyService(hostConnectionData, c.Resolve<LobbyPropertyWriter>())`

UGS Multiplayer SDK 1.1.8. `MultiplayerService.Instance` is the canonical accessor;
backing interface is `IMultiplayerService` (matches UGS naming convention used by
`LobbyService.Instance` / `ILobbyService`, `AuthenticationService.Instance` /
`IAuthenticationService`).

**Execution plan**:

1. `PartySessionService.cs`:
   - Add `private readonly IMultiplayerService _multiplayer;` field.
   - Ctor adds optional `IMultiplayerService multiplayerService = null` parameter; assigns
     `_multiplayer = multiplayerService ?? MultiplayerService.Instance;`.
   - Replace 2 `MultiplayerService.Instance.` calls with `_multiplayer.`.
2. `PresenceLobbyService.cs`:
   - Same field + ctor pattern.
   - Replace 3 `MultiplayerService.Instance.` calls with `_multiplayer.`.
3. AppManager factories: **no change required** — the optional null param defaults to
   `MultiplayerService.Instance` at construction time. Production behavior identical.
4. The optional ctor parameter gives tests a seam to pass a fake/mock without DI changes.

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
