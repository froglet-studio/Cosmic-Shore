<div class="sec-eyebrow">Part II · Verification</div>

# Network diagnostics

Every party / lobby / session / transition catch block appends a one-line **`NetDiag`** record next
to its existing log. It is pure observability — adding a call site never changes behaviour — and it is
what turned "the invite sometimes fails" into "the invite failed because the host's server-side spawn
never completed".

## The record

```text
NetDiag: class=<category> | reach=<UnityReachability> | monitor=<state> | sinceChange=<seconds>
```

| Field | Meaning |
|---|---|
| `class` | Exception category from `NetworkDiagnostics.ClassifyException` (or a hard-coded `Timeout` at the three transition timeouts) |
| `reach` | `Application.internetReachability` |
| `monitor` | Live `NetworkMonitor` state (`Online` / `Offline` / `Uninitialized`) |
| `sinceChange` | Seconds since the last monitor transition |

## Classification rules (first match wins)

| Input | `class=` |
|---|---|
| `OperationCanceledException` / `TaskCanceledException` | `Cancelled` |
| `AuthenticationException` | `AuthRequired` |
| `SessionException` containing NotFound / NotInLobby | `SessionGone` |
| `SessionException` otherwise | `Transient` |
| `LobbyServiceException` RateLimited / 429 | `RateLimit` |
| `LobbyServiceException` LobbyNotFound / 404 | `SessionGone` |
| `RequestFailedException` 429 / 404 / 5xx / {−1,0} | `RateLimit` / `SessionGone` / `Transient` / `Offline` |
| `WebException` / `SocketException` / `HttpRequestException` | `Offline` |
| anything else | `Unknown` |

## Two design choices worth noting

::: decision Diagnostics log on a strippable channel
NetDiag lines go through `CSDebug.Log`, which carries `[Conditional("UNITY_EDITOR")]` /
`[Conditional("DEVELOPMENT_BUILD")]`. In a shipping build the compiler removes the call *and its
argument evaluation* — `ClassifyException` and `GetSnapshot` are never invoked, the interpolated string
is never built. **Zero runtime cost, zero allocation in release.** It is also runtime-muteable in the
editor.
:::

::: pitfall A log classifier is not a retry predicate
`ClassifyException` decides *what to log*; `PartySessionService.IsTransientSessionException` decides
*what to retry*. They are kept separate on purpose so log format never silently drives retry policy —
a `PaymentRequired` class could be interesting to log yet must not trigger a retry. If you change retry
behaviour, change the retry predicate, not the log classifier.
:::

`NetworkMonitor` polls reachability every 5 s and emits an explicit transition line
(`Online → Offline (reach=…, t=…)`) so a party-flow failure can be cross-referenced against whether the
network actually changed around it. Sixteen catch sites across five files carry the NetDiag
decoration; the three `NetworkTransitionService` timeouts hard-code `class=Timeout` so a real timeout
isn't buried under the `Cancelled` its token would otherwise produce.
