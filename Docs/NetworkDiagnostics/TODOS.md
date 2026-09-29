# Network Diagnostics — TODOs

Deferred improvements to the overlay. Each entry has enough context
that it can be picked up cold.

## Polling cadence

### TODO-1. `NetworkMonitor.BoostPolling(int seconds)`

**Why.** `NetworkMonitor` polls every 5 s. An Offline transition that
happens mid-Accept can take up to 5 s to register in `monitor=`, so
`sinceChange=` may under-represent how recently the network actually
changed. The exception class is still correctly identified by
`ClassifyException` (pattern-matches on type), but the time-correlation
field is coarse.

**Outline.** Add a method `BoostPolling(int seconds)` to
`NetworkMonitor` that temporarily tightens the polling interval (e.g.
to 1 s) for the specified duration. Mirrors the existing
`LobbyRefreshScheduler.Boost()` pattern.

**Touchpoint.** Add the method to
`Assets/_Scripts/System/NetworkMonitor.cs`. Call it from
`PartyInviteController.AcceptInviteAsync` at the top of the flow (a
`TODO(NetworkMonitor.BoostPolling)` marker comment is already in place
at `PartyInviteController.cs:~136`).

**Risk.** Low. The boost is bounded by duration; reverts to base
cadence after. Polling cost is one `Application.internetReachability`
read per tick.

## Coverage

### TODO-2. Broader adoption — non-party UGS catches

**Why.** The helper exists; only party-side catches use it today.

**Candidate adoption sites:**

| File / area | Why it's a candidate |
|---|---|
| `Assets/_Scripts/System/AuthenticationServiceFacade.cs` | `SignInAnonymouslyAsync` catches — would distinguish offline auth from quota/UGS errors |
| `Assets/_Scripts/System/FriendsServiceFacade.cs` | Friend-request, presence-set catches — UGS Friends service can drop the same way |
| `Assets/_Scripts/System/Playfab/Authentication/*.cs` (legacy, deprecated) | Lower priority — auth is moving to UGS |
| `Assets/_Scripts/System/Instrumentation/CSAnalyticsManager.cs` | Analytics upload catches |
| `Assets/_Scripts/Integrations/PlayFabSDK/*` | PlayFab catalog / inventory / leaderboard catches |
| `Assets/_Scripts/System/IAPManager.cs` | Purchase failure paths |

**Pattern.** Identical to the party-side decoration — one appended log
line per catch. No DI needed. No constructor changes.

```csharp
catch (Exception e)
{
    Debug.LogWarning($"[<MyTag>] <existing message>: {e.Message}"); // pre-existing — untouched
    // ↓ one new line, appended — CSDebug.Log so it strips in release + mutes at runtime
    CosmicShore.Utility.CSDebug.Log($"[<MyTag>] NetDiag: class={CosmicShore.Utility.NetworkDiagnostics.ClassifyException(e)} | {CosmicShore.Utility.NetworkDiagnostics.GetSnapshot()}");
    // existing recovery / rethrow / etc
}
```

**Sequencing.** Mechanical follow-up; one PR per subsystem to keep
diffs small. Do this opportunistically when touching the file for
other reasons.

### TODO-3. Baseline NetDiag at flow entry

**Why.** Today's overlay logs only on catch. A baseline log at the
top of `AcceptInviteAsync` and `LeavePartyAndReturnToMenuAsync` would
let the catch-time log be diffed against the baseline to compute
"state changed mid-flow at …".

**Outline.** One `CSDebug.Log` (info-level, release-stripped) at the
top of each public flow method, matching the channel used by every
other line the overlay emits:

```csharp
public async UniTask AcceptInviteAsync(PartyInviteData invite)
{
    CosmicShore.Utility.CSDebug.Log($"[PartyInviteController] AcceptInviteAsync start | NetDiag: {CosmicShore.Utility.NetworkDiagnostics.GetSnapshot()}");
    // ... existing flow ...
}
```

**Trade-off.** Adds one info log per Accept / Leave on the happy
path. Currently we keep the happy path silent. Pick this up if a real
failure surfaces where the exception class alone doesn't tell us
whether the network state was bad at flow start vs. went bad mid-flow.

### TODO-4. Adoption inside HCS `RefreshAsync` non-benign branch — full coverage

**Why.** The catch-time NetDiag (single log line on the non-benign
else branch) is already in place. Extending into the retry /
escalation paths (`shouldReconnect`, `_consecutiveRefreshErrors`
increments) would multiply log noise without proportional diagnostic
value.

**When to pick this up.** Only if the catch-time signal proves
insufficient for a specific regression class. Until then, the lean
single-line decoration is the right call.

## Detection

### TODO-5. Active reachability probing

**Why.** A periodic lightweight HEAD or ICMP probe to a known endpoint
(e.g. `https://services.unity.com/health`) would prove
*application-layer* reachability, not just interface presence. Would
catch the case where `Application.internetReachability ==
ReachableViaLocalAreaNetwork` but no internet route exists (a common
Editor / split-tunnel case).

**Why deferred.** Active probing has policy implications: when does it
fire? what's the timeout? what's the back-off? does it count as a
privacy concern? Inferring from caught exceptions (`Offline` from
`WebException` / `SocketException`) gets us 80% of the signal at 0%
of the policy cost.

**Outline.** Add `NetworkMonitor.ProbeReachabilityAsync()` using
`HttpClient` with a 2-second timeout, called on the recovery-arm of
every NetDiag log (so only the failure path probes, not the happy
path).

**When to pick this up.** Only if the catch-based inference proves
insufficient. Today's classifier covers the cases we know about.

## Classifier extensions

### TODO-6. Extend `ClassifyException` as `Unknown` appears

**Why.** `class=Unknown` log lines are action items: extend
`ClassifyException`, then re-run.

**Process.** Each `Unknown` in a log gets:
1. The exception type captured from the existing log literal
   (which includes `e.GetType().Name`).
2. A new branch added to `ClassifyException` in the appropriate
   position (more specific cases first).
3. A documentation entry in `ARCHITECTURE.md` § "Classification rules".

**Open question.** Should `class=Unknown` itself escalate to an
error-level log? Currently it's Warning. Argument for Error: it
indicates the helper is incomplete. Argument against: noise.

### TODO-7. Distinguish UGS-degraded from offline-client

**Why.** Today `class=Transient` covers both "UGS is having a bad
minute" and "my SDK call failed mid-flow for unrelated reasons". These
are operationally different — UGS-degraded warrants no client action;
mid-flow failure may warrant a specific retry.

**Outline.** Add a `class=ServiceDegraded` class for HTTP 503 with
specific UGS error codes, distinct from generic `Transient`.

**When to pick this up.** After enough `Transient` data accumulates to
see whether the split is worth modeling.

## Data analysis

### TODO-8. Aggregate NetDiag class counts across MPPM runs

**Why.** The whole point of the overlay is to surface failure-class
frequencies. A simple script that scans recent log files and tallies
`NetDiag: class=*` occurrences would directly inform refactor
priority (see `../PartySystem/REFACTOR.md` § "Sequencing").

**Outline.** Editor menu item `FrogletTools > Validation > NetDiag Report`
that:
1. Reads the last N hours of Editor / Player logs.
2. Counts `NetDiag: class=*` per class, per source file.
3. Outputs a summary table to a markdown file or the Console.

**Risk.** None — read-only.
