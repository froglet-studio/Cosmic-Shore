# Network Diagnostics — Overlay Architecture

Cross-cutting diagnostic overlay for party / lobby / session /
transition failures. Pure observability — adding a call site never
changes behavior.

**Logging channel: `CSDebug.Log` (not `Debug.*`).** Every line this
overlay emits — the per-catch `NetDiag` lines and the `NetworkMonitor`
transition lines — goes through `CSDebug.Log`
(`Assets/_Scripts/Utility/CSDebug.cs`). This is deliberate:

- **Stripped from release builds.** `CSDebug.Log` carries
  `[Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]`, so
  in a shipping (non-dev) build the compiler removes the entire call
  *including its argument evaluation* — `ClassifyException(e)` and
  `GetSnapshot()` are never invoked, and the interpolated string is
  never built. **Zero runtime cost, zero allocation in release.**
- **Muteable at runtime.** In Editor / development builds the line
  respects `CSDebug.LogEnabled` (and the `CSDebug.LogLevel` preset), so
  the diagnostic noise can be silenced from `LogControlWindow` without
  touching code.

Do **not** "promote" these to `Debug.Log` / `Debug.LogWarning` — that
would reintroduce release-build allocation and console noise that the
team explicitly does not want for diagnostics.

For party-system architecture see `../PartySystem/ARCHITECTURE.md`. For
presence-system see `../PresenceSystem/ARCHITECTURE.md`. For
main-thread affinity rules (which this overlay respects) see
`../THREADING.md`.

## What the overlay does

Every party / lobby / session / transition catch block now appends a
one-line `NetDiag` log alongside its existing log literal. Each
NetDiag line carries:

```
NetDiag: class=<category> | reach=<UnityReachability> | monitor=<state> | sinceChange=<seconds>
```

| Field | Meaning | Example |
|---|---|---|
| `class` | Exception category from `NetworkDiagnostics.ClassifyException` (or the hard-coded `Timeout` at the three `NetworkTransitionService` timeout sites) | `Offline`, `SessionGone`, `Cancelled`, `RateLimit`, `Transient`, `AuthRequired`, `Unknown`, `Timeout` |
| `reach` | `Application.internetReachability` value | `NotReachable`, `ReachableViaLocalAreaNetwork`, `ReachableViaCarrierDataNetwork` |
| `monitor` | Live state of `NetworkMonitor` (read from `NetworkMonitorData.IsOnline`) | `Online`, `Offline`, `Uninitialized` |
| `sinceChange` | Time since the last `NetworkMonitor` transition, in seconds | `0.0s`, `12.3s`, `N/A` |

The exception class lets you tell at a glance whether a party-flow
failure was caused by going offline, the host quitting, a user cancel,
a rate-limit, an auth drop, or something the helper doesn't yet
classify.

## Pieces

### `Assets/_Scripts/Utility/NetworkDiagnostics.cs`

Static helper. Three members:

```csharp
public static void Initialize(NetworkMonitorDataVariable netDataVar);
public static string GetSnapshot();
public static string ClassifyException(Exception e);
```

- `Initialize` is called once from `AppManager.StartNetworkMonitor()`.
  Pins the live `NetworkMonitorDataVariable` so `GetSnapshot()` can
  include monitor state. If `Initialize` is never called (e.g. in a
  test harness), `GetSnapshot()` still returns reachability — just
  with `monitor=Uninitialized`.
- `GetSnapshot()` returns the formatted snapshot string. Zero
  allocations beyond the returned string. Safe to call from any thread
  (just reads).
- `ClassifyException(e)` returns one of seven labels. Matches by type
  and by full type name (string) so SDK-version-sensitive UGS types
  compile cleanly when absent and fall through to `Transient` /
  `Unknown`.

### `Assets/_Scripts/System/NetworkMonitor.cs`

Polls `Application.internetReachability` every 5 s. On a transition,
writes the new state to `NetworkMonitorData.IsOnline` +
`LastTransitionUnscaledTime`, raises the SOAP event
(`OnNetworkLost` / `OnNetworkFound`), and emits an explicit
`CSDebug.Log` line:

```
[NetworkMonitor] Online → Offline (reach=NotReachable, t=12.4s)
[NetworkMonitor] Offline → Online (reach=ReachableViaLAN, t=18.9s)
```

Both lines are `CSDebug.Log` (info severity) — stripped from release
builds and runtime-muteable, same as the NetDiag catch lines. The
`reach=` + timestamp tag is the canary you pair with a subsequent
party-flow `NetDiag` line to see whether the network changed around the
failure. (Logs only fire on an actual Online↔Offline transition, so a
stable connection produces nothing.)

### `Assets/_Scripts/ScriptableObjects/SOAP/ScriptableAuthenticationData/NetworkMonitorData.cs`

Added two state mirrors:

```csharp
public bool IsOnline { get; internal set; }
public float LastTransitionUnscaledTime { get; internal set; }
```

`internal set` so only `NetworkMonitor` (same assembly) can write.
Read-only for everyone else. The existing SOAP events
(`OnNetworkFound`, `OnNetworkLost`) are unchanged.

## Where it's wired

Sixteen catch-block decorations across five files — every party /
lobby / session / transition catch that classifies a failure (the
party-session-refresh `[definite]` and `[transient]` sites were added
in a follow-up after a 2026-06-01 MPPM session surfaced a gap; see
`../PartySystem/MPPM_SESSION_LOG.md` Session 1):

| File | Catch sites decorated |
|---|---|
| `Controller/Party/HostConnectionService.cs` | `RefreshAsync` non-benign else branch, `AcceptInviteAsync` catch, party-session-refresh `[definite]` branch, party-session-refresh `[transient]` branch |
| `Controller/Party/PartyInviteController.cs` | `AcceptInviteAsync` generic catch, `LeavePartyAndReturnToMenuAsync` generic catch, `RecoverFromFailedTransitionAsync` catch |
| `Controller/Party/Services/PresenceLobbyService.cs` | `JoinOrCreateAsync` catch, `CreateAsync` catch, `LeaveAsync` catch |
| `Controller/Party/Services/PartySessionService.cs` | `CreateAsync` transient retry catch, `JoinByIdAsync` transient retry catch, `LeaveAsync` catch |
| `Controller/Party/Services/NetworkTransitionService.cs` | `ShutdownAsync` timeout catch, `WaitForClientConnectionAsync` timeout catch, `WaitForSceneSyncAsync` timeout catch |

Existing log literals are preserved verbatim. The NetDiag line is
appended (via `CSDebug.Log`) — never replaces. The three
`NetworkTransitionService` sites fire only on a timeout
(`OperationCanceledException` when the flow's own token was *not*
cancelled), so they hard-code `class=Timeout` rather than running it
through `ClassifyException` (which would always return `Cancelled`
there and bury the real meaning).

## Classification rules

Pattern in order; first match wins:

| Input | `class=` |
|---|---|
| `OperationCanceledException` / `TaskCanceledException` | `Cancelled` |
| `Unity.Services.Authentication.AuthenticationException` | `AuthRequired` |
| `Unity.Services.Multiplayer.SessionException` with message containing `NotFound`/`not found`/`NotInLobby` | `SessionGone` |
| `Unity.Services.Multiplayer.SessionException` otherwise | `Transient` |
| `Unity.Services.Lobbies.LobbyServiceException` with `Reason=RateLimited` or message `429` | `RateLimit` |
| `Unity.Services.Lobbies.LobbyServiceException` with `Reason=LobbyNotFound` or message `404` | `SessionGone` |
| `Unity.Services.Lobbies.LobbyServiceException` otherwise | `Transient` |
| `Unity.Services.Core.RequestFailedException` `ErrorCode=429` | `RateLimit` |
| `Unity.Services.Core.RequestFailedException` `ErrorCode=404` | `SessionGone` |
| `Unity.Services.Core.RequestFailedException` `ErrorCode 500-599` | `Transient` |
| `Unity.Services.Core.RequestFailedException` `ErrorCode in {-1, 0}` | `Offline` |
| `Unity.Services.Core.RequestFailedException` otherwise | `Transient` |
| `System.Net.WebException` / `SocketException` / `HttpRequestException` | `Offline` |
| Anything else | `Unknown` |

`AggregateException` is unwrapped one layer before matching, so UGS
`Task.WhenAll` wrappings classify by their innermost cause.

## Important limits

### Editor `Application.internetReachability` lies

On Unity Editor, `Application.internetReachability` often reads
`ReachableViaLocalAreaNetwork` even when the WiFi adapter is off. On
real devices it's accurate. The helper still emits the value verbatim;
pair it with `monitor=` for cross-checking. Tests A and D in
`TESTS.md` cover this.

### Not a retry-control predicate

`ClassifyException` is for **logs**, not retry decisions.
`PartySessionService.IsTransientSessionException` is the
source-of-truth predicate for retry-loop control. They are
intentionally separate to avoid coupling log format to retry policy.

If you change the retry policy, update `IsTransientSessionException` —
NOT `ClassifyException`. They can diverge legitimately (e.g. a future
`PaymentRequired` class is interesting for logs but should not trigger
a retry).

### Polling rate is 5 s

`NetworkMonitor` polls every 5 s. A network drop during an Accept can
take up to 5 s to register in `monitor=`, so `sinceChange=` may
under-represent how recently the network actually changed. The
exception classifier still gets it right by matching on the
caught exception type. A `BoostPolling(int s)` hook is the obvious
future improvement — see `TODOS.md`.

## Adopting NetDiag in non-party catches

The same pattern applies elsewhere. Use `CSDebug.Log` for the appended
NetDiag line so it strips from release and stays muteable — leave the
pre-existing log literal on whatever channel it already uses:

```csharp
catch (Exception e)
{
    Debug.LogWarning($"[<MyTag>] <existing message>: {e.Message}"); // pre-existing — untouched
    // ↓ one new line, appended — CSDebug.Log so it strips in release + mutes at runtime
    CosmicShore.Utility.CSDebug.Log($"[<MyTag>] NetDiag: class={CosmicShore.Utility.NetworkDiagnostics.ClassifyException(e)} | {CosmicShore.Utility.NetworkDiagnostics.GetSnapshot()}");
    // existing recovery / rethrow / etc
}
```

No injection needed — `NetworkDiagnostics` is a static helper. See
`TODOS.md` for the candidate non-party adoption list.

## Related docs

- `TESTS.md` — Tests A-E for verifying the diagnostic accuracy
- `TODOS.md` — `BoostPolling`, active probing, baseline-entry log,
  broader adoption
- `../PartySystem/ARCHITECTURE.md` — party system that the overlay sits behind
- `../PresenceSystem/ARCHITECTURE.md` — presence system that the overlay sits behind
- `../THREADING.md` — main-thread affinity rules respected by the catches
