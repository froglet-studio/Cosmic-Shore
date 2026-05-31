# Presence System — Open Bugs

Living tracker for presence-lobby-side issues found in MPPM testing.
Companion to `ARCHITECTURE.md` (current state), `REFACTOR.md` (active
refactor queue), and `../NetworkDiagnostics/README.md` (catch-block
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

## B1 — `ArgumentOutOfRangeException` in `LobbyPatcher.ApplyPatchesToLobby` at game start 🟢 (needs Editor retest)

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

---

## B6 — TC3: `NullReferenceException` (`WrappedLobbyService.GetLobbyAsync`) + empty online/request lists 🔴

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

---

## How we work bugs

- One bug at a time, in priority order (B1 retest → B4 → B6).
- For each: confirm root cause via NetDiag log capture if possible →
  agree the approach → implement on `claude/blissful-tesla-9nefa` as
  its own commit with risk table → update status.
- This is the fragile, locked-design area. Read `ARCHITECTURE.md` and
  `../PartySystem/ARCHITECTURE.md` first. Do not reintroduce LAZY
  session creation.
