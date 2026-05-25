# HostConnectionService Refactor — Iterative commits toward an unbreakable party system

## Status (last updated: Commit 17)

All planned commits landed on `claude/blissful-tesla-9nefa`. Commit 5 was merged into
Commit 4 for compile-atomicity, so the backlog is 14 commits + this doc.

| # | Commit | Status |
|---|---|---|
| 1 | Dead-code removal (4 methods + lobby-patcher log filter) | ✅ |
| 2 | Annotate `RefreshPartyMembersAsync` catch (Bug A fix was already shipped) | ✅ |
| 3 | `IsInitialized` / `IsInPresenceLobby` / `IsHostingParty` helpers; delete `_initialized` | ✅ |
| 4 (+5) | `EnsurePartySessionAsync` (idempotent create-or-no-op) + caller rewiring | ✅ |
| 6 | `KickPartyMemberAsync` catch diagnostics (wrap was already shipped) | ✅ |
| 7 | Event-driven `Start` + `WaitForProfileInit` (no polling) | ✅ |
| 8 | Single source of truth — `PartySessionService.ActiveSession` → `gameData.ActiveSession` | ✅ |
| 9 | Cache `IMultiplayerService` field in PartySessionService + PresenceLobbyService | ✅ |
| 10 | `LeavePartyKeepHostAsync` canonical leave-to-solo surface | ✅ |
| 11 | `IsDefiniteSessionGoneException` classification + `[definite]` catch branch | ✅ |
| 12 | `HandleDefiniteSessionGoneAsync` auto-recovery (UI events) | ✅ |
| 13 | `OnDestroy` null guards + duplicate-instance early-return | ✅ |
| 14 | Comment cleanup (eager-Relay plain language) | ✅ |
| 15 | This doc finalization + CLAUDE.md anti-pattern | ✅ |
| 16 | Client-pull roster bootstrap + terminal watchdog (splash-hang root fix) | ✅ |
| 17 | Host roster cleanup + invite clear on member leave (event-driven `ISession.PlayerLeaving` + Netcode backstop) | ✅ |

**Recurring discovery**: several of the plan's "fix" steps (Bug A catch simplification,
`KickPartyMemberAsync` try/catch wrap) were already shipped by earlier surgical commits,
so Commits 2 and 6 were re-scoped to documentation/diagnostics. The structurally novel
work landed in Commits 3, 4, 7–13.

**Verification gap**: edit-mode compile + brace-balance checks were done per commit, but
the Unity Editor / play-mode test suite and the 2-VP MPPM scenarios (exit criteria 6–8)
were **not** runnable from the development environment. They require a human/CI pass in
Unity. See the exit-criteria section.

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
through a lazily-resolved property:
`private IMultiplayerService _multiplayerService => MultiplayerService.Instance;`
on both `PartySessionService` and `PresenceLobbyService`. No constructor parameter,
no cached field — the property reads the live `Instance` at each call site.

The remaining 3 callsites in `MultiplayerSetup.cs` (game-launch path) are out of
the Commit 9 scope per plan.

> **⚠️ Post-merge bugfix (NRE at runtime)**: the original Commit 9 cached
> `MultiplayerService.Instance` in the constructor (`_multiplayer = multiplayerService
> ?? MultiplayerService.Instance`). Because both services are **lazy DI singletons
> constructed during Bootstrap DI resolution — before `UnityServices.InitializeAsync()`
> completes — `MultiplayerService.Instance` is null at construction**, so the field was
> pinned null forever. This surfaced as a `NullReferenceException` in
> `PartySessionService.CreateAsync` on the auth-scene initial party creation. Fixed by
> switching to a plain `_multiplayerService` property that reads `Instance` fresh at
> use time. The earlier test-injection ctor seam was dropped at the prompter's request
> — it's a plain property now, no DI parameter. The CLAUDE.md anti-pattern entry was
> corrected to prescribe this property form, not ctor caching.

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

### `[x]` Commit 10 — `LeavePartyKeepHostAsync` + adopt at every leave path

**Outcome**: new `LeavePartyKeepHostAsync` method added to HCS (positioned next to
`EnsurePartySessionAsync` and `ClearPartySessionRef`). Two callers rewired:

- `PartyInviteController.LeavePartyAndReturnToMenuAsync`: dropped explicit
  `_networkTransition.ShutdownAsync()`; now calls `hcs.LeavePartyKeepHostAsync()`
  which internally does `_partySessionService.LeaveAsync()` (proper UGS
  `DeleteAsync`/`session.LeaveAsync` per role) followed by `EnsurePartySessionAsync()`.
- `PartyInviteController.RecoverFromFailedTransitionAsync`: replaced
  `ClearPartySessionRef + EnsurePartySessionAsync` chain with a single
  `LeavePartyKeepHostAsync()` call — cleaner UGS-backend state on recovery.

`HCS.LeavePartyAsync`: dropped its trailing `await EnsurePartySessionAsync()` —
`LeavePartyAndReturnToMenuAsync` now ensures it internally.

`ClearPartySessionRef` (added in Commit 4): no in-code callers remain. Kept in
place as a defensive escape hatch; XML doc updated to flag "currently unused" and
state the path that subsumed it (Commit 10).

Remaining `_networkTransition.ShutdownAsync` callsites in the codebase:
- `PartyInviteController.AcceptInviteAsync:158` — accept-flow shutdown (leave-to-join,
  not leave-to-host-solo). Out of Commit 10 scope.
- `HostConnectionService.EnsurePartySessionAsync:717` — internal to the create
  path. UGS `CreateSessionAsync(opts.WithRelayNetwork())` requires NM in a known
  state.

The plan's Q6 "no NM cycle" goal is partially achieved: leave paths are now
*consolidated* through one method, but the internal `EnsurePartySessionAsync`
still calls `ShutdownAsync` before `CreateAsync` (so NM is cycled once). Full
"zero cycle" requires teaching `EnsurePartySessionAsync` to skip Shutdown when
NM is already up and SDK lifecycle constraints permit it — tracked in the
Deferred section.

Brace balance verified (HCS 129/129, PartyInviteController 28/28).
HCS: 1483 → 1562 (+79, mostly XML doc on the new method).

**Pre-commit findings** (preserved for audit trail):


**Pre-commit findings** (live source post-Commit 9):

Leave paths today:

| Site | Currently does | Notes |
|---|---|---|
| `HCS.LeavePartyAsync:600` | Delegates to `controller.LeavePartyAndReturnToMenuAsync()`, then post-call `await EnsurePartySessionAsync()` | The trailing EnsurePartySession becomes redundant once the controller's flow ensures it internally. |
| `PartyInviteController.LeavePartyAndReturnToMenuAsync:240` | `_networkTransition.ShutdownAsync()` → `hcs.EnsurePartySessionAsync()` → `nm.SceneManager.LoadScene(Menu_Main)` | Target for replacement. Explicit Shutdown becomes implicit via LeavePartyKeepHostAsync (which calls `_partySessionService.LeaveAsync` → `EnsurePartySessionAsync`). |
| `PartyInviteController.RecoverFromFailedTransitionAsync:337` | `hcs.ClearPartySessionRef()` → `hcs.EnsurePartySessionAsync()` → `nm.SceneManager.LoadScene(Menu_Main)` | Target for replacement. Plan §Q3 site #4. |
| `PartyInviteController.AcceptInviteAsync:158` | `_networkTransition.ShutdownAsync()` → join the inviter's session | **Out of scope.** This is a leave-to-JOIN (not leave-to-host-solo). LeavePartyKeepHostAsync would recreate a solo Relay — wrong for accept. |
| `HCS.KickPartyMemberAsync:623` | Explicitly **rejects self-kicks** at line 631-633 | **No self-kick path exists.** The plan's "KickPartyMemberAsync self-kick path → use new path" bullet appears to refer to a code shape no longer present. No change. |

`PartySessionService.LeaveAsync` (lines 213-227) details:
```csharp
public async UniTask LeaveAsync()
{
    if (ActiveSession == null) return;
    var session = ActiveSession;
    ClearSession();          // clears ref BEFORE the network call
    try { ... await session.AsHost().DeleteAsync() OR session.LeaveAsync() ... }
    catch (Exception e) { Debug.LogWarning(...); }  // swallows
}
```
- `ClearSession()` runs first, so even if the network call throws, the ref is cleared.
- Internal catch swallows — `LeaveAsync` never propagates exceptions to its caller.
- This means the outer try/catch in `LeavePartyKeepHostAsync` is defensive (good practice
  for future-proofing if `LeaveAsync` semantics change, but currently can't fire).

`EnsurePartySessionAsync` internal behavior:
- Fast-paths on `IsHostingParty`. After `LeaveAsync` clears `ActiveSession`, IsHostingParty
  is false → goes into the create path.
- Inside the create path, calls `_networkTransition.ShutdownAsync` at line 717. If
  `LeaveAsync` already left NM in a "down" state, this Shutdown is a no-op. If NM is
  still up (UGS SDK didn't tear it down), it shuts it down.
- Either way: ONE effective NM cycle per leave, not two.

**Caveat on "no NM cycle" promise**: the plan Q1 says "the recovery path no longer
cycles NM at all" once LeavePartyKeepHostAsync replaces the unconditional ShutdownAsync.
That literal "no cycle" goal requires `EnsurePartySessionAsync` to skip its internal
`ShutdownAsync` when NM is already up and `CreateSessionAsync` would conflict. The current
SDK behavior probably forces a cycle (CreateSessionAsync needs NM to be in a known state).
Commit 10 as written achieves **consolidation** of the leave pattern (one method, one
place) but does NOT yet achieve "literally zero NM cycles." Tracking that residual
optimization in §Deferred at the bottom of this doc.

**Execution plan**:

1. Add `public async UniTask LeavePartyKeepHostAsync()` to `HostConnectionService`,
   positioned near `EnsurePartySessionAsync` and `ClearPartySessionRef`:
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
           Debug.LogError($"[HostConnectionService] LeavePartyKeepHostAsync: " +
                          $"LeaveAsync threw ({ex.GetType().Name}): {ex.Message} — " +
                          "proceeding to recreate solo session.");
       }
       await EnsurePartySessionAsync();
   }
   ```
2. `PartyInviteController.LeavePartyAndReturnToMenuAsync` (lines 260-285):
   - Drop the explicit `await _networkTransition.ShutdownAsync(...)` call.
   - Replace `await hcs.EnsurePartySessionAsync().AsMainThread()` with
     `await hcs.LeavePartyKeepHostAsync().AsMainThread()`.
   - Update surrounding comments.
3. `PartyInviteController.RecoverFromFailedTransitionAsync` (lines 344-357):
   - Replace `hcs.ClearPartySessionRef();` + `await hcs.EnsurePartySessionAsync()...`
     with `await hcs.LeavePartyKeepHostAsync().AsMainThread();`.
   - Update comment.
4. `HCS.LeavePartyAsync:620`: remove the trailing `await EnsurePartySessionAsync()` —
   `LeavePartyAndReturnToMenuAsync` now ensures it internally via
   `LeavePartyKeepHostAsync`.
5. `ClearPartySessionRef` (added in Commit 4): no longer called by anyone after step 3.
   Leave it in place for now as a defensive escape hatch; mark "currently unused" in
   its XML doc. Commit 14 (comment cleanup) or a follow-up can delete it if it stays
   unreferenced.

`KickPartyMemberAsync` and `AcceptInviteAsync`: no change (justification in findings
table above).

Expected behavior change: leave paths now go through `PartySessionService.LeaveAsync`
which properly calls `DeleteAsync` (host) or `session.LeaveAsync` (client) on the UGS
session — replacing the prior "Shutdown + recreate" pattern that just dropped the
session ref locally without telling UGS. Cleaner backend state; same NM cycle count.

### `[x]` Commit 11 — Refresh error classification (transient vs definite)

**Outcome**: `RefreshPartyMembersAsync` now distinguishes a definite server-side
session loss from a transient refresh failure. New `IsDefiniteSessionGoneException`
predicate (structured `SessionError` enum match + HTTP-404 `RequestFailedException`
+ narrow message fallback, walking the InnerException chain). New `[definite]` catch
branch between `[rate-limit]` and `[transient]` routes to `HandleDefiniteSessionGoneAsync`,
which (Commit-11 minimal body) re-entrancy-guards and calls `LeavePartyKeepHostAsync`
to recover into a fresh solo session. Re-entrancy guard field
`_handlingDefiniteSessionGone` added.

Functional recovery is complete in Commit 11 (no more infinite retry on a dead
session). Commit 12 adds the UI-clearing events so stale party slots clear
immediately rather than on the next member sync.

`Unity.Services.Multiplayer` already imported (covers `SessionException`/`SessionError`);
`RequestFailedException` used fully-qualified (no new import). Brace balance 135/135.
HCS: 1562 → 1648 (+86 from predicate + recovery method + XML doc).

**Pre-commit findings** (preserved for audit trail):

UGS exception surface (confirmed against SDK 1.1.8 docs):
- `SessionException.Error` is a `SessionError` enum. Relevant values:
  `SessionError.SessionNotFound`, `SessionError.SessionDeleted`,
  `SessionError.NotInLobby`. All three mean "the session/lobby is gone for us."
- `Unity.Services.Core.RequestFailedException.ErrorCode` carries HTTP-ish codes
  (existing rate-limit check uses `== 429`). 404 ≈ not found.
- Both `SessionException` and `RequestFailedException` are in namespaces already
  reachable (HCS imports `Unity.Services.Multiplayer`; `RequestFailedException` is
  used fully-qualified elsewhere as `Unity.Services.Core.RequestFailedException`).

Existing classifiers in `PartySessionService` (lines 303-328) for reference:
- `IsRateLimitException` → `RequestFailedException.ErrorCode == 429`.
- `IsTransientSessionException` → `SessionException` + message heuristics
  (`Object reference`, `23006`, `valid Lobby ID`).

Current `RefreshPartyMembersAsync` catch (lines 1155-1187): benign → rate-limit →
transient (log + retry). The `[transient]` branch already carries a TODO breadcrumb
(added in Commit 2) for the definite split.

Grace period at the top of `RefreshPartyMembersAsync` (post-creation window) means
definite-gone detection only fires on an *established* session, not a freshly-
provisioned one — avoids tearing down a session that just hasn't propagated yet.

`HandleDefiniteSessionGoneAsync` recovery: `LeavePartyKeepHostAsync` (Commit 10)
already does the leave-and-recreate, so Commit 11 can deliver functional recovery
on its own. Commit 12 enriches it with UI-clearing events.

**Predicate design** — structured-first, message-fallback:
```csharp
private static bool IsDefiniteSessionGoneException(Exception e)
{
    for (var current = e; current != null; current = current.InnerException)
    {
        if (current is SessionException se &&
            se.Error is SessionError.SessionNotFound
                     or SessionError.SessionDeleted
                     or SessionError.NotInLobby)
            return true;

        if (current is Unity.Services.Core.RequestFailedException rfe && rfe.ErrorCode == 404)
            return true;

        // Narrow message fallback: require "session" co-occurrence to avoid
        // misclassifying generic "not found" transients as definite.
        var msg = current.Message;
        if (!string.IsNullOrEmpty(msg) &&
            msg.IndexOf("session", StringComparison.OrdinalIgnoreCase) >= 0 &&
            (msg.IndexOf("not found",      StringComparison.OrdinalIgnoreCase) >= 0 ||
             msg.IndexOf("deleted",        StringComparison.OrdinalIgnoreCase) >= 0 ||
             msg.IndexOf("does not exist", StringComparison.OrdinalIgnoreCase) >= 0))
            return true;
    }
    return false;
}
```
Walks the InnerException chain (UGS / UniTask wrap exceptions), matching the existing
`IsBenignLobbyPatcherError` walk pattern.

**Execution plan**:

1. Add `IsDefiniteSessionGoneException(Exception)` static predicate next to the
   existing `IsRateLimitException` / `IsBenignLobbyPatcherError` helpers in HCS.
2. Add `private bool _handlingDefiniteSessionGone;` re-entrancy guard field.
3. Add `private async UniTask HandleDefiniteSessionGoneAsync()` — Commit-11 minimal
   body: re-entrancy guard + `await LeavePartyKeepHostAsync()`. Commit 12 enriches
   with the member snapshot + per-member `OnPartyMemberLeft` + `OnHostConnectionLost`.
4. In `RefreshPartyMembersAsync` catch, insert the `[definite]` branch between
   `[rate-limit]` and `[transient]`: if definite → log + `HandleDefiniteSessionGoneAsync().Forget()` → return.
5. Replace the Commit-2 TODO breadcrumb with a comment pointing at the now-real branch.

Behavior change: a server-side session deletion (404 / SessionNotFound / SessionDeleted
/ NotInLobby) now triggers auto-recovery into a fresh solo session instead of an
infinite log-and-retry loop. The re-entrancy guard prevents overlapping recoveries
from the 1.5s refresh cadence.

### `[x]` Commit 12 — Auto-recover on definite session-gone

**Outcome**: `HandleDefiniteSessionGoneAsync` enriched with UI-clearing steps. On a
definite server-side session loss the recovery now: snapshots remote-member presence,
drops the dead ref (`ClearSession`), clears member slots + raises `OnPartyMemberLeft`
per member (`_memberService.ClearWithEvents`), conditionally raises `OnHostConnectionLost`
(only if a real party dropped), then recreates a fresh solo session
(`EnsurePartySessionAsync`). Re-entrancy guard from Commit 11 retained.

Reused `IPartyMemberService.ClearWithEvents` (interface member, line 95) — the existing
tested utility — instead of re-implementing the snapshot+raise loop. Verified all
called members resolve: `_eventBus` (`SoapPartyEventBus`) → `RaiseHostConnectionLost`;
`_memberService` (`IPartyMemberService`) → `ClearWithEvents`; `_partySessionService`
→ `ClearSession`. Brace balance 135/135. HCS: 1648 → 1675 (+27).

This completes the "no stuck UI" exit criterion: a server-side session deletion now
clears stale party slots within one refresh tick (≤~1.5s) and returns the user to a
solo party with no manual action.

**Pre-commit findings** (preserved for audit trail):

`HandleDefiniteSessionGoneAsync` (added in Commit 11) currently: re-entrancy guard +
`await LeavePartyKeepHostAsync()`. Commit 12 inserts the UI-clearing steps.

Existing utilities that collapse the plan's manual steps:
- `PartyMemberService.ClearWithEvents(localPlayerId)` (lines 175-188) — iterates
  `_connectionData.PartyMembers`, skips the local player, removes each other member
  AND raises `OnPartyMemberLeft` per member via `_eventBus`. This **is** the plan's
  steps 1 (snapshot) + 3 (per-member raise) combined into one tested call. Already
  used by `LeavePartyAsync:615`.
- `SoapPartyEventBus.RaiseHostConnectionLost()` (line 91) → `_data.OnHostConnectionLost?.Raise()`.
- `SoapPartyEventBus.RaisePartyMemberLeft(PartyPlayerData)` (line 167).

`OnHostConnectionLost` is an established event (also raised by `HandleSignedOutEvent:357`
and the refresh-reconnect path), so listeners already exist.

`connectionData.PartyMembers` supports Linq (`OnlinePlayers.ToList()` used at line 501);
`using System.Linq;` is imported. `PartyPlayerData` has `.PlayerId` / `.DisplayName`.

**Deviations from the plan's literal 5 steps** (both justified):

1. Steps 1+3 (snapshot + per-member raise) → single `_memberService.ClearWithEvents(localId)`
   call. Reuses the existing tested utility instead of re-implementing the loop.
2. Step 5 `LeavePartyKeepHostAsync()` → direct `EnsurePartySessionAsync()`. Because step 2
   explicitly `ClearSession()`s first, the `LeaveAsync` inside `LeavePartyKeepHostAsync`
   would see `ActiveSession == null` and no-op — calling `EnsurePartySessionAsync`
   directly avoids issuing a doomed UGS `DeleteAsync` on a session we already know is gone.

**Refinement** beyond the plan: only raise `OnHostConnectionLost` when there were
actually remote members (snapshot a `hadRemoteMembers` bool before clearing). A solo
player whose solo session was reaped recovers invisibly — no spurious "Party connection
lost" toast. Still honors the locked invariant (nothing stale to clear for a solo player).

**Execution plan** — enrich `HandleDefiniteSessionGoneAsync` body:
```csharp
bool hadRemoteMembers = connectionData.PartyMembers != null &&
    connectionData.PartyMembers.Any(m => m.PlayerId != connectionData.LocalPlayerId);

_partySessionService.ClearSession();          // drop the dead ref (known gone)
_memberService.ClearWithEvents(connectionData.LocalPlayerId);  // steps 1+3
if (hadRemoteMembers)
    _eventBus.RaiseHostConnectionLost();      // step 4 (gated)
await EnsurePartySessionAsync();              // step 5 (direct — ref already cleared)
```
Re-entrancy guard from Commit 11 stays wrapping the whole body.

### `[x]` Commit 13 — `OnDestroy` null guards + `Debug.LogError`

**Outcome**: `OnDestroy` rewritten with an `Instance != this` early-return (fixes the
duplicate-instance shared-service teardown bug) plus null guards on each teardown
dependency, every guard logging `Debug.LogError` with the field name + suspected cause:
- `bootStatusRetryRequestedEvent` null → LogError (SOAP asset unwired), skip unsubscribe.
- `_lobbyService` null → LogError (DI failure + stale-presence consequence), skip leave.
- `_propertyWriter` null → LogError (DI failure), skip mutex disposal; otherwise dispose
  `LobbyMutex`/`SessionCreationMutex` with `?.`.

The `Instance != this` guard is the higher-leverage fix: a duplicate HCS destroyed by
Awake's singleton guard no longer tears down the shared DI singletons (`_lobbyService`,
`_propertyWriter`) of the live instance, and no longer NREs on its own un-injected
fields.

Confirmed `LobbyMutex`/`SessionCreationMutex` are `public readonly` on
`LobbyPropertyWriter` — accessible from HCS. Brace balance 137/137.
HCS: 1675 → 1701 (+26).

**Pre-commit findings** (preserved for audit trail):

Current `OnDestroy` (lines 310-326):
```csharp
async void OnDestroy()
{
    SceneManager.sceneLoaded -= OnSceneLoaded;
    UnsubscribeFromProfileChanges();
    UnsubscribeFromGameLaunch();
    if (bootStatusRetryRequestedEvent != null)
        bootStatusRetryRequestedEvent.OnRaised -= HandleBootStatusRetryRequested;
    await _lobbyService.LeaveAsync();          // NRE if DI failed
    _lobbyMutex.Dispose();                       // NRE if _propertyWriter null
    _sessionCreationMutex.Dispose();             // NRE if _propertyWriter null
    if (Instance == this) Instance = null;
}
```

Field reality (Q8 table needs correcting):
- `_lobbyMutex` / `_sessionCreationMutex` are **properties** (lines 151-152) delegating
  to `_propertyWriter.LobbyMutex` / `.SessionCreationMutex`. The semaphores are
  `public readonly SemaphoreSlim = new(1,1)` on `LobbyPropertyWriter` (lines 70, 78) —
  never null if `_propertyWriter` is non-null. So the real nullable thing is
  `_propertyWriter` (`[Inject] LobbyPropertyWriter`, line 118), not the mutexes.
- `_lobbyService` is `[Inject] IPresenceLobbyService` (line 130) — null if DI failed.
- `bootStatusRetryRequestedEvent` is the serialized SOAP event — null if unwired.

**Latent bug discovered** (worth fixing here, aligns with "no NRE in OnDestroy"):
Awake's singleton guard `if (Instance != null && Instance != this) { Destroy(gameObject); return; }`
destroys a *duplicate* HCS. That duplicate's `OnDestroy` still fires — and currently runs
the full cleanup, including `await _lobbyService.LeaveAsync()` (leaves the shared lobby)
and `_lobbyMutex.Dispose()` (disposes the shared DI singleton's semaphores) — **corrupting
the live instance's services.** A duplicate may also have null `[Inject]` fields (Reflex
may not have injected before the Awake-time Destroy), so it would NRE in OnDestroy too.

Fix: `if (Instance != this) return;` at the top of OnDestroy — a duplicate (or
already-replaced) instance does no cleanup. This both prevents the shared-service
teardown AND avoids spurious null-guard logs from an un-injected duplicate.

**Execution plan** — rewrite `OnDestroy`:
1. `if (Instance != this) return;` — duplicate/replaced instance is a clean no-op.
2. Keep the harmless unsubscribes (sceneLoaded, profile, gameLaunch — all no-op if never
   subscribed).
3. `bootStatusRetryRequestedEvent`: keep `!= null` unsubscribe; add `else` LogError
   naming the field + "SOAP event asset not wired on the prefab."
4. `_lobbyService`: guard `!= null`; LogError on null naming DI failure + the
   ~30s-stale-presence consequence; skip the leave.
5. `_propertyWriter`: guard `!= null`; dispose `LobbyMutex` + `SessionCreationMutex`
   (with `?.`); LogError on null naming DI failure; skip disposal.
6. `Instance = null;` at the end (the top guard already established `Instance == this`).

Each null branch logs `Debug.LogError` with the field name + suspected cause, per the
locked decision "Every null guard logs Debug.LogError with field name and suspected
cause. Loud, traceable failures." OnDestroy is best-effort cleanup — we cannot recover
during teardown, but loud logs surface missing prefab refs / DI failures in the editor.

### `[x]` Commit 14 — Comment cleanup

**Outcome**: replaced the `"Always InParty" model` design-jargon lead-ins with
plain-language "eager per-user Relay" descriptions at five comment sites:
- `HostConnectionService.cs:881` (acceptance-signal scan)
- `MultiplayerSetup.cs:401` (LeaveSession)
- `SceneLoader.cs:311` (HandleActiveSessionEnd)
- `PartyInviteSystemTests.cs:1109` (ParseInviteLine test) + `:1359` (transition test header)

The `lazy`-creation comments the original plan targeted were already removed in
Commits 1 and 4, so no `lazy Relay` references remained to rewrite. Verified zero
`"Always InParty"` / `lazy Relay` references remain anywhere under `Assets/_Scripts/`
(outside `.md` docs). Brace balance unchanged on all touched files.

**Out of scope (noted for a future commit)**: the PENDING-sentinel three-phase
acceptance protocol (`InviteService`, `AcceptanceSignalService`, `LobbyRefreshScheduler`,
interfaces) is still present even though eager creation means invites use the real
session ID directly. Auditing/removing PENDING spans 5+ files and is a separate
surgical task, not comment cleanup. Added to the Deferred section.

### `[x]` Commit 15 — Documentation roadmap + `CLAUDE.md` additions

**Outcome**:
- Added the Status table + recurring-discovery note + verification-gap note at the top
  of this doc.
- Annotated every exit criterion with ✅ (addressed in code) / 🔬 (needs Unity-runtime
  or MPPM verification). Criteria 3, 4, 5 and the OnDestroy part of 1 are done by
  inspection; 6, 7, 8 (and timing/integration parts of 1, 2) remain gated on a Unity
  Editor + MPPM pass the dev environment can't run.
- Added to `CLAUDE.md` "Anti-Patterns to Avoid": cache UGS/Netcode `*.Instance` as a
  constructor-injected field with `?? *.Instance` fallback (cites
  `PartySessionService`/`PresenceLobbyService` caching `IMultiplayerService`).
- `PARTY_SYSTEM_REFACTOR.md` was already in the CLAUDE.md documentation index (added in
  Phase 0); confirmed present.

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
- **Audit/remove the PENDING-sentinel three-phase acceptance protocol.** With eager
  per-user Relay, invites carry the real session ID directly, so the PENDING handshake
  (`InviteService.PENDING_SESSION_ID`, `AcceptanceSignalService.PublishSignalAsync` /
  `WaitForRealSessionIdAsync` / `RepublishWithRealIdAsync`, `LobbyRefreshScheduler`'s
  PENDING-republish boost window) may be dead. Confirm no live path writes PENDING,
  then remove the protocol across `InviteService`, `AcceptanceSignalService`,
  `LobbyRefreshScheduler`, and their interfaces. Spans 5+ files — a dedicated commit,
  not comment cleanup.

### `[x]` Commit 16 — Client-pull roster bootstrap + terminal watchdog (splash-hang root fix)

**Problem.** Accepting an invite left the joining client stuck on the splash
~25-50% of the time. Root cause: the joiner's bootstrap was a host-push, one-shot,
unacknowledged `InitializeAllPlayersAndVessels_ClientRpc` (`ServerPlayerVesselInitializer.NotifyClients`).
Netcode 2.x silently drops a ClientRpc whose target NetworkObject hasn't spawned on
that client yet; the host's `postSpawnDelayMs` waits for the *vessel* to replicate, not
for the *joiner's scene-sync* (when its scene-placed `ClientPlayerVesselInitializer`
spawns). Under jitter the push landed before the receiver existed → dropped → `_pendingPairs`
never populated → `OnClientReady` never fired → the splash (armed only on `OnClientReady`,
no timeout) hung forever. `AcceptInviteAsync` also ignored its connection / scene-sync
wait results, and `WaitForSceneSyncAsync` is itself unreliable for late joiners.

This supersedes the earlier **Bug B** understanding (root was the dropped one-shot RPC,
not only the `ActiveSession`-null cascade).

**Fix — invert host-push → client-pull, make resolution event-independent, add a watchdog.**
Mirrors the already-reliable `MultiplayerMiniGameControllerBase.SyncGameConfigToClients_ClientRpc`
pattern (driven from a scene object's `OnNetworkSpawn`).

- `ClientPlayerVesselInitializer`: new `RequestRosterFromHost_ServerRpc` (mirrors
  `RequestVesselSwap_ServerRpc`) called from the client's own `OnNetworkSpawn` — the
  receiver provably exists, so the host's reply cannot be dropped. A bounded retry loop
  (`RosterPullRetryLoop`, 4 × 1.5s) re-asks until the local pair resolves, cancelled in
  `InitializePair` (local user) / `OnNetworkDespawn`. Objects self-register into
  `gameData.Players`/`Vessels` on spawn, so a delivered tuple resolves against current
  state without depending on a transient SOAP event.
- `ServerPlayerVesselInitializer`: extracted `SendFullRosterToClient`; new
  `HandleRosterRequest` (wired to `ClientPlayerVesselInitializer.OnRosterRequested`) —
  idempotent ensure-then-send (kicks the spawn chain if the requester's vessel is
  missing, then resends the roster). Existing-client delta push unchanged.
- `PartyInviteController.AcceptInviteAsync`: honors `WaitForClientConnectionAsync`'s
  result; replaces the ignored scene-sync gate with `WaitForClientReadyAsync` (waits on
  `OnClientReady` with a `joinReadyTimeoutSeconds` budget). On any failure →
  `BounceToSoloMenuAsync` → `RecoverFromFailedTransitionAsync` (leave party, restart solo
  host, reload Menu_Main → fresh `OnClientReady` clears the splash) + a best-effort toast.
  The splash can never stay stuck. All cross-thread awaits keep `.AsMainThread()`.

Respects the locked design (eager Relay, single `ActiveSession`, state-machine recovery).

**Verification gap (🔬):** needs 2-VP MPPM confirmation — accept ×20+, every run either
roams in the host's lava-lamp or cleanly bounces to the joiner's own menu; never a black
hang. `bounceToastChannel` must be wired in the Bootstrap prefab for the toast to appear.

### `[x]` Commit 17 — Host roster cleanup + invite clear on member leave (event-driven)

**Problem.** After Commit 16's bounce returns a failed-join client to its own solo host,
the **inviting host's roster stayed stale**: the departed client's `PartyPlayerData`
lingered in `HostConnectionDataSO.PartyMembers` (the party slot still showed their avatar),
and any outgoing invite to them lingered until the `ExpireOutgoingInvites` timeout. Root
cause: host-side member removal was **poll-only**. `PartyMemberService.SyncFromSession`
(which correctly removes anyone gone from `session.Players` and raises `OnPartyMemberLeft`)
ran only from `RefreshPartyMembersAsync` on the ≤1.5s poll, and that method returns early
for 4s after session creation (`SESSION_CREATION_GRACE_PERIOD_SECONDS`). Nothing reconciled
the roster the moment a client left.

**Fix — event-driven reconcile + invite cleanup, with a Netcode backstop.**

- `PartySessionService` (+ `IPartySessionService`): re-broadcast the party
  `ISession.PlayerLeaving` (carries the UGS `PlayerId`) as a service-level
  `event Action<string> PlayerLeaving`, wired immediately after each `ActiveSession`
  assignment (`CreateAsync` / `JoinByIdAsync`) and unwired in `ClearSession` — the single
  point that nulls the reference, reached by both `LeaveAsync` and
  `HandleDefiniteSessionGoneAsync` — so no handler leaks across session reassignment.
- `HostConnectionService.ReconcilePartyMembersNow()`: host-only on-demand reconcile that
  reuses `RefreshPartyMembersAsync(bypassGraceGate: true)` (new parameter skips the 4s gate —
  it protects a *joining* client, not a *leaving* one), with a short bounded retry
  (`RECONCILE_MAX_ATTEMPTS` × `RECONCILE_RETRY_DELAY_MS`) to absorb leave-propagation lag,
  serialised with the poll via `_lobbyMutex` + `_insideRefreshCycle`.
- `HostConnectionService.OnPartySessionPlayerLeaving`: subscribed to the new event via the
  guarded subscribe/teardown idiom (`_partyLeaveSubscribed`, wired in
  `EnsureInitializedAsync`, torn down in `OnDestroy`). Clears any outgoing invite to the
  departing player via the existing `ClearOutgoingInviteIfPresentAsync(playerId, "party-leave")`
  and calls `ReconcilePartyMembersNow`.
- `MultiplayerSetup.OnClientDisconnect` (host branch): calls `ReconcilePartyMembersNow` as a
  Netcode backstop for hard drops (client crash) that may beat the graceful UGS event. It
  carries only the Netcode `clientId` (no UGS `PlayerId`), so it reconciles the roster and
  leaves invite cleanup to the `PlayerLeaving` handler / poll. Both triggers are idempotent.

Implements the two follow-ups deferred earlier (event-driven `ISession` player-left
subscription; host-side outgoing-invite cleanup on member leave). Respects the locked design
(eager Relay, single `ActiveSession`); the poll remains the ultimate backstop, and
`SyncFromSession` keys off `session.Players`, so a transient blip never falsely drops a member.

**Verification gap (🔬):** needs 2-VP MPPM — host idles in Menu_Main; client accepts and is
forced down the bounce path. Confirm on the host: the slot clears effectively immediately
(UGS `PlayerLeaving`, not the ~1.5s poll), `PartyMembers` returns to `[host]`, the outgoing
invite is gone (`invite_payloads` republished), and a transient blip where the client is
still in `session.Players` does not drop them. Also verify a hard client-process kill
reconciles via the `OnClientDisconnect` backstop, and repeated invite→bounce cycles don't
accumulate `PlayerLeaving` handlers.

## Unbreakable exit criteria — when do we stop?

**All of these hold simultaneously in editor + 2-VP MPPM verification.** Status after
the 14-commit pass — ✅ addressed in code / 🔬 needs Unity-runtime or MPPM verification:

1. 🔬 **No fatal failures.** Inviting, accepting, leaving, kicking, refresh failures
   never:
   - Despawn the host's menu vessel mid-session — *addressed*: refresh catch never
     clears the session (Commits 2, 8); leave goes through `LeavePartyKeepHostAsync`
     (Commit 10). Needs MPPM confirmation.
   - Crash with an NRE in `OnDestroy` — ✅ guarded + duplicate early-return (Commit 13).
   - Kick joined clients due to a host-side transient refresh error — *addressed*:
     transient vs definite split (Commit 11). Needs MPPM confirmation.
2. ✅/🔬 **No stuck UI.** Server-side session deletion → `HandleDefiniteSessionGoneAsync`
   clears slots + recreates solo within one refresh tick (Commits 11, 12). Logic in
   place; the ≤3s timing needs MPPM confirmation.
3. ✅ **One source of truth.** `PartySessionService.ActiveSession` is a derived view of
   `gameData.ActiveSession` (Commit 8) — reference-equal by construction.
4. ✅ **No silent failures.** Every catch logs / classifies / recovers (Commits 2, 6,
   11, 12, 13). No `catch (Exception) {}` introduced.
5. ✅ **No `ActiveSession = null` outside intentional leave.** Commit 8 removed the two
   offending nulls (`MultiplayerSetup.LeaveSession`, `GameDataSO.ResetAllData`); the
   only remaining null is post-Delete/Leave in `OnTransportFailure` + the property
   setter. `grep` clean.
6. 🔬 **All existing edit-mode tests green** (`CosmicShore.Multiplayer.Tests`,
   `CosmicShore.Tests.EditMode`). Per-commit changes preserved test compatibility
   (reflection assertions updated in Commit 1), but the suite was **not run** — needs
   a Unity Test Runner pass.
7. 🔬 **2-VP MPPM happy path**: VP-A creates party, VP-B accepts. Both vessels replicate,
   both fly, no host `OnNetworkDespawn` on invite send, VP-B fade clears. **Not run.**
8. 🔬 **2-VP MPPM failure path**: Force-delete VP-A's session mid-session → both clients
   return to solo within 3s, no crash. **Not run.**

Criteria 3, 4, 5, and the OnDestroy part of 1 are satisfiable by inspection and are
done. Criteria 6, 7, 8 (and the timing/integration parts of 1, 2) require a Unity
Editor + MPPM verification pass that the development environment can't run — these are
the remaining gate before declaring the surface "unbreakable."

This doc records remaining deferred items; it lives on as the source of truth for
ongoing refinement.

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
