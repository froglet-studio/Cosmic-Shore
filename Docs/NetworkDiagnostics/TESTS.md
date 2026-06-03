# Network Diagnostics — Manual Test Procedures

Verifies the NetDiag overlay correctly identifies failure classes. Each
test is **diagnostic-only** — it confirms the log line says the right
thing, NOT that the underlying behavior changes (it doesn't; the
overlay is observability-only).

For party-flow tests see `../PartySystem/TESTS.md`. For presence-side
tests see `../PresenceSystem/TESTS.md`.

> All tests run in Unity Editor with 3-4 VP MPPM unless noted.

## Test A — Diagnostic for the Offline case

**Setup.** Toggle WiFi off on the client machine right before pressing
Accept on the joining VP.

**Pass criterion (diagnostic only).** The log on the joiner contains
both:
- `[NetworkMonitor] Online → Offline (reach=NotReachable, t=…)` within
  5 s of the WiFi toggle.
- The eventual catch line:
  `NetDiag: class=Offline | reach=NotReachable | monitor=Offline | sinceChange=…`.

**Note on Editor.** The Editor often reads `reach=ReachableViaLAN` even
when WiFi is off. The `class=Offline` classification will still fire
correctly (the helper matches by exception type, not by reachability)
— it's the `reach=` field that may misreport. On a real device, `reach=`
is accurate.

**Possible solutions to discuss later (NOT part of this test).**
- **A.1 Dedicated Reconnecting UI** — route through
  `ApplicationStateMachine.Disconnected` (already wired) and show a
  "Reconnecting…" modal instead of bouncing to Menu_Main. Pro:
  preserves the in-flight Accept. Con: needs new UI; ASM has no
  visual today.
- **A.2 Offline-specific toast** — keep current bounce, but show
  "Internet connection lost — returned to your menu" instead of the
  generic toast. Smallest possible response; one branch in
  `BounceToSoloMenu`.
- **A.3 Auto-resume on recovery** — cache the invite payload across
  the bounce; when `OnNetworkFound` fires within N seconds, surface a
  one-tap "Retry join" toast. Higher-impact, higher-risk: requires
  invite-payload persistence and a recovery window.
- **A.4 Pre-flight reachability check** — before `AcceptInviteAsync`
  begins, check `NetworkMonitorData.IsOnline` and short-circuit with
  an offline toast. Pro: cheap, no in-flight state to unwind. Con:
  doesn't help when network drops mid-flow.

---

## Test B — Diagnostic for the SessionGone case

**Setup.** YS1 sends an invite, then YS1's VP is closed/stopped before
YS3 presses Accept. YS3 accepts the stale invite.

**Pass criterion (diagnostic only).** Catch log line contains:
`NetDiag: class=SessionGone | reach=ReachableViaLAN | monitor=Online | sinceChange=…`.

Distinguishable from Test A: reachability is fine, the session is
gone.

**Possible solutions to discuss later.**
- **B.1 Specific toast** — branch on `class=SessionGone` in
  `BounceToSoloMenu` and show "Host left the party" instead of the
  generic toast. Simplest possible change.
- **B.2 Auto-dismiss the stale invite** — when `class=SessionGone` is
  observed, raise a `OnInviteSessionGone(sessionId)` SOAP event;
  invite UI listens and removes the matching entry. Prevents the user
  from retrying a dead invite.
- **B.3 Pre-flight session existence check** — at the top of
  `AcceptInviteAsync`, do one cheap `SessionsService.GetSessionAsync`
  to verify the session exists before tearing down the local host.
  Pro: short-circuits the heavy transition for dead invites. Con:
  adds one network round-trip to every Accept (latency cost on the
  happy path).
- **B.4 Invite freshness window** — tag invites with timestamps;
  refuse to even show invites older than N seconds. Reduces incidence
  but doesn't fix the race entirely.

---

## Test C — Diagnostic for the Cancelled case

**Setup.** YS3 accepts an invite, then immediately presses Leave (or
the host stops their VP, triggering a cooperative cancel mid-Accept).

**Pass criterion (diagnostic only).** Either of these fires:
- `[PartyInviteController] Accept flow cancelled.` (the explicit
  `OperationCanceledException` catch — cleanest path).
- The generic catch with `NetDiag: class=Cancelled | …`.

**Possible solutions to discuss later.**
- **C.1 ~~Demote Cancelled to Info~~ — superseded by the CSDebug.Log
  routing (commit `70ae31b`).** All NetDiag lines, regardless of class,
  now log at info severity via `CSDebug.Log` and strip from release
  builds entirely, so Cancelled no longer produces Warning noise in dev
  or any cost in release. No further action needed for this option.
- **C.2 Suppress toast on Cancelled** — branch in `BounceToSoloMenu`
  to skip the "Couldn't join" toast when the cause is a user cancel.
  The user already knows they pressed Leave.
- **C.3 Catch `OperationCanceledException` earlier** — add an
  explicit `catch (OperationCanceledException)` ahead of the generic
  `catch (Exception e)` in PIC so cancellation never reaches the
  generic error path at all. Cleanest. Requires care to still trigger
  recovery (Menu_Main reload) without the error toast.
  *Already partially done* — the explicit catches at
  `PartyInviteController.cs:233` and `:316` are this. The
  generic-catch decoration covers the case where the cancellation
  reaches that path anyway (e.g. wrapped in an `AggregateException`).
- **C.4 Distinguish user-cancel vs framework-cancel** — only C.3 for
  user-driven Leave; let framework-driven cancels (e.g. scene-unload)
  take the existing path. Requires a flag on the CTS or a sentinel
  exception.

---

## Test D — Diagnostic for the original YS3 4-VP scenario

**Setup.** Reproduce the exact 4-VP MPPM scenario from the diagnostics
overlay plan: YS1 invites YS2 + YS3 + YS4 concurrently / staggered.

**Pass criterion (diagnostic only).** If YS3 (or any joiner) fails
again, the catch line carries a `NetDiag: class=…` snapshot that lets
us assign the failure to one of the seven classes. **This is the test
that decides whether subsequent investigation goes into the bug column
or the environment column.**

- `class=Unknown` → the helper needs extending. Capture the exception
  type from the existing log literal and add the case to
  `ClassifyException`. File the gap as a TODO in this folder.
- `class=Offline` → reframes the bug as environmental. Note the
  reclassification in `../PartySystem/BUGS.md` (or
  `../PresenceSystem/BUGS.md` if it's a presence-side failure).
- `class=Cancelled` → reframes as MPPM VP teardown. Note the
  reclassification.
- `class=SessionGone` or `class=Transient` → real bug. Investigate
  with the specific UGS interaction in mind. Possible solutions
  follow the menu in Tests A-E above (whichever class).
- `class=AuthRequired` → flow into auth recovery; see Issue 6 in
  `../PartySystem/BUGS.md` (forthcoming).
- `class=RateLimit` → see Issue 4 in `../PartySystem/BUGS.md`
  (forthcoming).

---

## Test E — Negative regression check (happy path)

**Setup.** 4-VP accept / decline / leave smoke with no induced
failures.

**Pass criterion.** No new logs at all on the happy path — no new
Warning, no new Error, no new Info from the NetDiag overlay. All
catches stay silent because they weren't triggered.

The NetworkMonitor only logs on actual Offline ↔ Online transitions,
which the happy path does not produce.

**Possible solutions to discuss later.**
None required for the happy path. Adjacent ideas worth noting:
- **E.1** If we ever add a baseline entry log (see `TODOS.md` §
  "Baseline NetDiag at flow entry"), this test would assert it appears
  exactly once per Accept and once per Leave on the happy path.
- **E.2** If a structured logger is adopted (JSON-formatted log lines
  for ingestion by an external dashboard), the NetDiag string format
  becomes the seed for the structured payload schema.

---

## How to use these tests

| Situation | Run |
|---|---|
| Verifying the overlay still works after a refactor | Test E (negative check) |
| Investigating a specific MPPM failure | Whichever test matches the suspected class (A, B, C) |
| Investigating an unclassified failure | Test D — let the helper tell you which class it is |
| Validating a `ClassifyException` extension | The test for the new class (add a new test letter F+ as needed) |

These tests are **not** required per commit — they're diagnostic
validation. The per-commit gate is in `../PartySystem/TESTS.md` (S1-S4)
and `../PresenceSystem/TESTS.md` (P1-P3).
