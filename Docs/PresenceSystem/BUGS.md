# Presence System — Open Bugs

Living tracker for presence-lobby-side issues found in MPPM testing.
Companion to `ARCHITECTURE.md` (current state), `REFACTOR.md` (active
refactor queue), and `../NetworkDiagnostics/ARCHITECTURE.md` (catch-block
diagnostics).

Party-side bugs (B2, B3, B5, B7 from the old tracker) moved to
`../PartySystem/BUGS.md`.

Statuses: 🔴 open · 🟡 investigating · 🟢 fixed (commit) · ⚪ deferred.

| ID | Title | Confidence | Status |
|----|-------|-----------|--------|
| B1 | `ArgumentOutOfRangeException` (LobbyPatcher) spam at game start | High (cause) | 🟢 (needs Editor retest) |
| B4 | TC1 second invite not delivered + party members vanish from 3rd player's panel | High, needs retest | 🔴 |
| B6 | TC3 NRE (`WrappedLobbyService`) + empty online/request lists | Medium | 🔴 |

> **Working order.** Diagnostics-first. The presence-lobby cluster (B4,
> B6) is the locked-design area — read `ARCHITECTURE.md` and
> `../PartySystem/ARCHITECTURE.md` before touching `PresenceLobbyService`
> or `HostConnectionService`. Do not reintroduce LAZY session creation.

---

## B1 — `ArgumentOutOfRangeException` in `LobbyPatcher.ApplyPatchesToLobby` at game start 🟡 (console noise silenced; underlying SDK defect persists)

**Symptom.** Every client logs, at game start, an
`ArgumentOutOfRangeException` from `LobbyPatcher.ApplyPatchesToLobby` →
`LobbyHandler.OnLobbyChanged` → `LobbyChannel.ProcessEvent` /
`HandleLobbyChanges`.

**Root cause (high confidence).** The UGS Lobby SDK applies a WebSocket
"lobby changed" delta that references a player/data index not present
in the local cache (stale index). The exception is thrown **and logged
by the SDK itself** (`Unity.Services.Multiplayer.Logger.LogException`,
inside `LobbyChannel.HandleLobbyChanges`) on the SDK's own async event
task — **before any of our `await`s**. Therefore our
`IsBenignLobbyPatcherError` classifier (`HostConnectionService.cs:1852`,
used only in the catch blocks at `:1023` and `:1297`, which wrap *our*
`RefreshAsync` calls) **cannot** see or suppress this particular log.
It is already known-benign and self-correcting; the problem is purely
console noise we cannot `try/catch`.

**Why "at game start".** Multiple clients join the presence lobby
near-simultaneously and write player properties rapidly, so the SDK
receives bursts of deltas that race its local cache. Our
`LobbyPropertyWriter.SaveWithRetryAsync` also does a post-save
`lobby.RefreshAsync()` (`LobbyPropertyWriter.cs:147-153`) to reduce
stale deltas — which may add to the churn.

**Fix (shipped — option 1, iterated).** `BenignLobbyLogFilter`
(`Assets/_Scripts/Utility/BenignLobbyLogFilter.cs`). A
`[RuntimeInitializeOnLoadMethod(BeforeSceneLoad)]` installs once
(idempotent) a decorator around `Debug.unityLogger.logHandler` that
drops **only** the benign `LobbyPatcher` `ArgumentOutOfRangeException`;
every other log is forwarded verbatim. Whole file is gated
`#if UNITY_EDITOR || DEVELOPMENT_BUILD`, so release is unchanged.

- **v1** intercepted only `ILogHandler.LogException` (the route for
  `Debug.LogException`).
- **Retest #1 (user, Editor):** the `[BenignLobbyLogFilter] Installed`
  line printed (decorator active) but the error **still appeared** —
  confirming the SDK logs it via the **`LogFormat`** route
  (`Debug.LogError` / `unityLogger.Log(LogType.Exception, e)`), not
  `LogException`. The `Logger.LogException` frame visible in the
  console is Unity's captured call-site stack, not the `ILogHandler`
  entry point our decorator overrode.
- **v2 (current)** also intercepts `LogFormat` for
  `LogType.Exception`/`Error`, matching either an `Exception` argument
  (via the shared `IsBenignLobbyPatcherError` stack classifier) or a
  pre-rendered message string containing both `LobbyPatcher` and
  `ArgumentOutOfRangeException`. Rendering is defensive (try/catch →
  forward on failure), so a real error is never suppressed.

`Application.logMessageReceived` was rejected — it is a post-hoc
*notification* and cannot suppress. **Worst case the filter is a no-op
— no regression.**

**Needs Editor retest (v2 — `LogFormat` path).** Start a game with ≥2
VPs and confirm the `LobbyPatcher` `ArgumentOutOfRangeException` no
longer appears on **any** instance (the one-time
`[BenignLobbyLogFilter] Installed …` line confirms the decorator is
active; ordinary errors/warnings must still log). If it still leaks, it
is being logged as a plain message string with no type/stack in the
content — paste the exact one-line text and we add a literal-string
match.

**Evidence.** `HostConnectionService.cs:1852` (`IsBenignLobbyPatcherError`),
`:1023`, `:1297`; `LobbyPropertyWriter.cs:147-153`; `CSDebug.cs` (gates
our calls only); SDK stack in the report.

**MPPM Session 1 (2026-06-01) — confirmed still firing, two more leak
points found.** With the NetDiag overlay live, the B1 stale-index family
was observed firing continuously (~every 3 s) in solo Menu_Main, on two
SDK surfaces the `BenignLobbyLogFilter` does NOT cover:

1. **Write path** — `LobbyPropertyWriter.SaveWithRetryAsync` logs
   `Save failed (SessionException: Index was out of range … Parameter
   name: index) — retry 1/3…3/3`. The catch at `LobbyPropertyWriter.cs:158-160`
   already special-cases `"Index was out of range"` and retries, but the
   retry warning still reaches the console.
2. **Read path** — the subsequent `lobby.RefreshAsync()` /
   `PartySessionService.RefreshAsync()` NREs inside
   `WrappedLobbyService.GetLobbyAsync` (the B6 frame — see below), caught
   at `HostConnectionService.cs:1346` and logged + NetDiag-classified
   `Transient`.

Both are the **same SDK stale-index defect** as the `LobbyPatcher`
`ArgumentOutOfRangeException`, just on the Save and Get API surfaces
instead of the WebSocket-delta surface. `BenignLobbyLogFilter` matches
only the `LobbyPatcher` + `ArgumentOutOfRangeException` signature, so
neither of these is suppressed.

**Fix applied (option b — silence at the catch).** Chosen because the
catches were the closer fit: `LobbyPropertyWriter.SaveWithRetryAsync`
already explicitly filters to "Index was out of range" / "Too Many
Requests" via a `when` clause, so demoting the warning there is
surgical; and `HostConnectionService`'s two refresh catches already had
a `IsBenignLobbyPatcherError` discriminator branch, so adding a sibling
`IsBenignSdkStaleIndexNre` follows the existing pattern.

- **`LobbyPropertyWriter.cs:166`** — `Debug.LogWarning` → `CSDebug.Log`.
  The "Save failed (… Index was out of range …) — retry X/3" message
  now strips from release builds and respects runtime mute. Outer
  catch-on-exhaust path unchanged.
- **`HostConnectionService.cs` new method `IsBenignSdkStaleIndexError`**
  — matches a `SessionException` whose **structured `Error` property ==
  `SessionError.Unknown`** (the `[Error: Unknown]` prefix in the log).
  **NOT the message string.** Message-matching was abandoned after it
  turned into whack-a-mole — three message variants of the *same* SDK
  defect appeared across three MPPM restarts:
  1. `"Object reference not set to an instance of an object"` (NRE form)
  2. `"Index was out of range. Must be non-negative and less than the size of the collection."`
  3. `"Index must be within the bounds of the List."`
  All three are wrapped in a `SessionException` whose structured
  `Error` is `Unknown`. The structured reason is the stable signal: a
  genuinely actionable `SessionException` carries a *specific* reason
  (`SessionNotFound`, `SessionDeleted`, `NotInLobby`, `RateLimited`, …),
  and those are handled by the `[definite]` / rate-limit branches that
  run **before** this benign check at both catch sites. Only
  unclassifiable SDK-internal failures land on `Unknown`, and for those
  "log-silent, retry next tick" is already the correct and only
  recovery. Implemented as `se.Error.ToString() == "Unknown"` to avoid
  pinning the exact enum member across SDK versions.

  **Stack deliberately NOT used.** A first attempt matched on
  `StackTrace.Contains("WrappedLobbyService")` AND the message, but that
  silently failed in MPPM: `Exception.StackTrace` is unreliable after
  the exception crosses several async `SetException` boundaries
  (UniTask + Task continuations) before our catch — the stack shown in
  the Unity console is Unity's *captured* stack, not the exception
  object's own `.StackTrace` string (often null/truncated
  post-propagation).

  **Trade-off (accepted).** Matching `Error == Unknown` is broader than
  a message match — a future genuinely-actionable failure that also
  surfaces as `Unknown` would be silenced at these two refresh catches.
  Mitigated by ordering (the `[definite]` + rate-limit branches catch
  every *classifiable* reason first) and by the nature of `Unknown`
  (the SDK couldn't classify it → no actionable recovery exists anyway).
  If a real failure is ever masked here, this is the first line to
  revisit.

  `LobbyPropertyWriter.SaveWithRetryAsync` handles the same defect on
  the write path via a message filter (`"Too Many Requests" || "Index
  was out of range"`) — it does not have a structured `Error` to
  inspect at that callsite, so message-matching is unavoidable there;
  the write path has only ever shown the IOOR string.

  Consumed at two catch sites:
  - `HCS:1069` outer presence-lobby refresh catch: silence as a sibling
    of `IsBenignLobbyPatcherError` (no log, no counter increment, no
    state change).
  - `HCS:1346` party-session refresh catch: silence as a sibling of
    `IsBenignLobbyPatcherError` (early return).

Option (a) — broadening `BenignLobbyLogFilter` — was rejected:
`BenignLobbyLogFilter` exists for SDK-emitted logs that fire before
our catch can run (`LobbyChannel.HandleLobbyChanges` etc.). The two new
signatures both go through our own `Debug.LogWarning` calls inside our
catches, where we have full control without needing to hook the global
log handler.

Discriminator behaves gracefully in IL2CPP if stack info is unavailable
(returns `false` → exception falls through to existing transient log
path; we just see the warning again).

See `../PartySystem/MPPM_SESSION_LOG.md` Session 1, Pre-flight finding #2
for the discovery context.

---

## B4 — TC1: second invite not delivered + party members vanish from 3rd player's online panel 🔴

**Symptom.** VP1 invites VP3 → accept → ok (party of 2). VP1 then
invites VP2 → **VP2 never gets the invite**, and VP1/VP3's rows (shown
"In Lobby 2/4") **vanish from VP2's online panel**.

**Root-cause hypotheses (high, pending retest).**
- Once a party forms (`PartyMembers.Count > 1`), convergence is
  **paused** (`HostConnectionService.cs:~945-958`), which can **freeze
  a presence-lobby split** so VP2 ends up on a different lobby than
  VP1/VP3.
- `RefreshOnlinePlayersDiff` (~`:1150-1196`) **removes** any player not
  in the local presence lobby → VP1/VP3 drop from VP2's
  `OnlinePlayers`.
- On any lobby rejoin, `BuildLocalPlayerProperties`
  (`PresenceLobbyService.cs:~335-350`) **resets `invite_payloads` to
  empty** (documented in a code comment), wiping VP1's outgoing invite
  to VP2 before VP2 reads it.

**Open question (user to retest, after B1).** Do VP1/VP3 rows **come
back on their own** (transient split) or **stay gone** (frozen split)?
Determines whether the fix targets convergence-pause or the
diff/property-reset.

**Diagnostic upgrade (post commit `aaba872`).** Any `JoinOrCreateAsync`
fallback now emits `NetDiag: class=… | …` — if VP2 ends up creating its
own lobby (the split scenario), the catch on the failed join will
classify the cause. `class=Transient` or `class=Unknown` would
strengthen the convergence-pause hypothesis; `class=Offline` would
suggest a different problem.

**Constraint.** This is the fragile, locked-design area — **read
`../PartySystem/ARCHITECTURE.md` before touching**
`HostConnectionService` / `PresenceLobbyService` / invite services.
Likely wants more diagnostics first.

**Evidence.** `HostConnectionService.cs:~945-958, ~964-970, ~1150-1196`;
`PresenceLobbyService.cs:~204-239 (converge), ~335-350 (property reset)`.

**Fix shipped (2026-07-16, invite-chain Task 4) — MPPM retest required.**
Owner decision: allow lobby convergence while partied. Implemented as a
**state-preserving rejoin** plus removal of the convergence pause:

1. `IPresenceLobbyService.LivePropertySource` — a provider hook
   (`Func<IReadOnlyDictionary<string,string>>`) set once by
   `HostConnectionService` (`BuildLivePresenceProperties`). Every lobby
   (re)join path — initial join, reconnect, converge migration — now
   overlays LIVE values onto the property dict in
   `PresenceLobbyService.BuildLocalPlayerProperties`: outgoing
   `invite_payloads` (`InviteService.SerializeAll`), a guest's
   `joined_party` (current session id when `!IsPartyHost`), and
   `matchName`. The rejoin no longer wipes in-flight invites or a
   guest's party advertisement. `accepted_invite` is deliberately NOT
   preserved (fast-path hint only; the session member sync covers it,
   and carrying it across rejoins would make stale signals permanent).
   HCS remains the single writer of the values.
2. The `inActiveInviteOrParty` pause in `HostConnectionService.RefreshAsync`
   is **removed** — convergence now runs on its normal throttle even
   mid-invite / mid-party, so the frozen-split (this bug's scenario:
   partied players stuck in a non-canonical lobby, third player never
   receives the invite) self-heals.

**Retest (MPPM):** the B4 TC1 repro (VP1+VP3 partied, VP1 invites VP2),
plus the invite-chain S10 (member-sent invite) with a deliberately
split lobby; confirm the pending invite survives a converge migration
(sender's `invite_payloads` non-empty after "Converged to canonical"
log) and no B1/B6 stale-index regression from the extra rejoin writes.

**⚠ Repro validity caveat (2026-07-16).** A 4-instance session with
**untagged** MPPM clones reproduced B4-family symptoms (one-sided rows,
empty online lists on some clones) whose actual root cause was the
shared `mppm-clone` auth profile — all untagged clones sign in as ONE
UGS PlayerId, and each clone's lobby join invalidates the previous
clone's membership (dead handle → refresh errors → empty lists). Rows
appeared correct as soon as unique tags were assigned. The original
B4 TC1 session predates the tag prerequisite
(`../PartySystem/TESTS.md` § "MPPM prerequisites"), so this entry's
convergence-freeze hypothesis must be re-confirmed with **tagged** VPs
before any further B4-specific work — the identity collision may
account for part or all of the historical symptom.

---

## B6 — TC3: `NullReferenceException` (`WrappedLobbyService.GetLobbyAsync`) + empty online/request lists 🟡 (refresh-path noise silenced in MPPM Session 1; TC3 empty-lists symptom untested since fix)

**Symptom.** A variant of TC3: VP2 logs a UGS `NullReferenceException`
from `WrappedLobbyService.TryCatchRequest` / `GetLobbyAsync` during
`LobbyChannel.ProcessEvent`, and VP2's online list **and** request list
both go empty.

**Root-cause hypothesis (medium).** Same family as B1 — SDK-internal,
logged by the SDK before our catch — triggered when a lobby
subscription event fires against a stale/torn-down lobby reference
(premature `LeaveAsync`/`ForceReset` during the accept handshake). The
empty-lists symptom is likely our `OnlinePlayers`/requests going stale
when `ActiveLobby` becomes null and refresh early-returns.

**Approach.** Treat the NRE as trigger-reduction (don't leave/rejoin
mid-event; guard against stale refs). Investigate the empty-lists
recovery separately (does the UI repopulate after the next successful
refresh?). Likely bundle with B4 diagnostics.

**Evidence.** SDK stack (`WrappedLobbyService.cs:165/462`,
`LobbyChannel.cs:197`); our lobby leave / `ForceReset` /
refresh-early-return paths in `HostConnectionService` /
`PresenceLobbyService`.

**MPPM Session 1 (2026-06-01) — same SDK frame seen on the refresh
path.** The `WrappedLobbyService.GetLobbyAsync` NRE
(`WrappedLobbyService.cs:170` / `TryCatchRequest` `:497`) was captured
firing every ~3 s from `PartySessionService.RefreshAsync` →
`HostConnectionService.cs:1346`, not only during the accept-handshake
leave/rejoin this entry originally described. The HTTP GET succeeds; the
SDK NREs deserializing the response against a stale cache seeded by the
B1 write-path churn (see B1's Session 1 note). So B6's NRE and B1's
stale-index are the same SDK defect — B6 is the read-path symptom, B1
the write/delta-path symptom. Overlay classifies the read-path NRE
`Transient` and recovers (keeps session, retries next tick).

---

## How we work bugs

Method: see `../README.md` § "How we work bugs". This is the fragile,
locked-design area — read `ARCHITECTURE.md` and
`../PartySystem/ARCHITECTURE.md` first. Presence-side priority order:
**B1 retest → B4 → B6**.
