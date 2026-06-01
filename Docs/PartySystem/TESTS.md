# Party System — Manual Test Procedures

MPPM (Multiplayer Play Mode) scenarios used as the regression gate for
every party-side commit. All tests run in Unity Editor with 3-4 virtual
players.

> **Convention.** "VP1" = the host's virtual player; "VP2", "VP3", "VP4"
> = joining clients. Each VP is a separate Editor instance under MPPM.
> Tests reference NetDiag log classes (`class=Offline`, `class=SessionGone`,
> etc.) — see `../NetworkDiagnostics/README.md` for the helper.

## Smoke gate — run on every commit

### S1. Accept invite (happy path)

**Setup.** Start 3 VPs. VP1 enters Menu_Main → its presence lobby
populates with VP2, VP3.

**Steps.**
1. VP1 taps the "+" slot, picks VP2 → invite sent.
2. VP2 sees invite popup → Accept.
3. Wait up to 15 s for completion.

**Pass criterion.**
- VP2's `[PartyInviteController] Accept flow completed successfully.`
  appears in the log.
- VP2 spawns into VP1's Menu_Main scene (sees VP1's autopilot vessel).
- Party Area on both VP1 and VP2 shows 2/4 members.
- No NetDiag log lines fire (clean catches).

### S2. Decline invite

**Setup.** S1 setup.

**Steps.**
1. VP1 invites VP2.
2. VP2 sees popup → Decline.

**Pass criterion.**
- VP2's popup dismisses; no transition starts.
- VP1's invite slot frees within 5 s.
- No bounce, no log warnings.

### S3. Leave party

**Setup.** Run S1 to completion.

**Steps.** VP2 taps Leave.

**Pass criterion.**
- VP2's `[PartyInviteController] Leave-lobby flow completed.` appears.
- VP2 returns to its own solo Menu_Main with one autopilot vessel.
- VP1's party slot for VP2 frees within 5 s.

### S4. Second accept after leave

**Setup.** Run S3 to completion.

**Steps.** VP1 re-invites VP2. VP2 accepts.

**Pass criterion.** Same as S1 — proves the leave path leaves no
state divergence.

## Stress gate — run on every refactor commit

### Stress-1. Five-accept smoke

VP1 invites VP2 five times in a row, with VP2 leaving and re-accepting
between each. Pass: all five complete cleanly per S1 / S3 criteria, no
NetDiag log lines.

### Stress-2. Concurrent invites (4-VP MPPM)

**Setup.** 4 VPs. VP1 invites VP2, VP3, VP4 within 1-2 s of each other.

**Pass criterion.**
- All three joiners either complete S1's success path, or bounce
  cleanly per S5's criterion.
- No leftover vessels on any client's solo menu after a bounce
  (see Bug B3 in `BUGS.md`).
- No NetDiag `class=Unknown` log lines (those indicate the helper
  needs extension).

### Stress-3. Mid-accept Leave

VP2 accepts an invite, then immediately taps Leave (during the
transition before `Accept flow completed successfully`).

**Pass criterion.**
- VP2 returns to solo Menu_Main cleanly.
- Log shows `[PartyInviteController] Accept flow cancelled.` (not
  the generic catch path).
- If the generic catch fires instead, NetDiag should classify as
  `class=Cancelled`.

## Failure-mode gate — run when investigating a bug

These tests intentionally provoke failures. Each pass criterion is
**diagnostic-only** — the NetDiag log must correctly identify the
failure class. Whether the system *responds* differently to each class
is `../NetworkDiagnostics/README.md` "Possible solutions" territory.

### S5. Offline-during-Accept

**Setup.** VP1 invites VP2.

**Steps.** Toggle the client machine's WiFi off **right before** VP2
presses Accept.

**Diagnostic pass.** VP2's log contains:
- `[NetworkMonitor] Online → Offline (reach=NotReachable, t=…)` within 5 s.
- The eventual catch line carries `NetDiag: class=Offline | …`.

The bounce-to-solo-menu behavior is the existing baseline — not the
test target.

### S6. SessionGone (host quits mid-accept)

**Setup.** VP1 invites VP2.

**Steps.**
1. Wait for VP2 to see the invite popup.
2. Stop VP1's editor instance.
3. VP2 presses Accept.

**Diagnostic pass.** VP2's catch log carries
`NetDiag: class=SessionGone | reach=ReachableViaLAN | monitor=Online | …`.
Distinguishable from S5: reachability is fine, session is gone.

### S7. User-cancel Accept

**Setup.** VP1 invites VP2.

**Steps.** VP2 presses Accept, then immediately Leave (while the accept
flow is in-flight).

**Diagnostic pass.** Log shows
`[PartyInviteController] Accept flow cancelled.` — the
`OperationCanceledException` branch. If the generic catch fires instead,
NetDiag should classify as `class=Cancelled`.

### S8. YS3 4-VP repro

**Setup.** 4 VPs. VP1 invites VP2, VP3, VP4 (concurrent or staggered).

**Steps.** Trigger the original YS3 failure scenario from the
diagnostics overlay plan.

**Diagnostic pass.** If any joiner fails, the catch line carries a
`NetDiag: class=…` snapshot. The class determines whether YS3 stays on
the bug list (`class=Unknown` / `class=Transient` → investigate) or
gets reclassified as environment (`class=Cancelled` from MPPM VP
teardown → file under `TODOS.md`).

## What success on these tests means

| Gate | Required for |
|---|---|
| S1, S2, S3, S4 | Every party-system commit |
| Stress-1, Stress-2, Stress-3 | Every refactor commit (Refactor 1, 2, 3 from `REFACTOR.md`) |
| S5, S6, S7, S8 | Run when investigating a specific bug; not a per-commit gate |

The exit criteria in `ARCHITECTURE.md` § "Unbreakable exit criteria"
list what passing these gates means at the system level.

## Session journal

Per-session results — what plan was scheduled, what was observed,
what changed as a result — live in `MPPM_SESSION_LOG.md`. This file
(`TESTS.md`) holds the test *procedures*; the session log is the
*journal* across runs.

## Automation (deferred — D4 in `REFACTOR.md`)

These procedures are manual today. Deferred item **D4** in `REFACTOR.md`
tracks automating accept / decline / leave / refresh-fail /
session-gone-auto-recovery as MPPM-driven play-mode integration tests, so
exit criteria 6-8 stop depending on a human MPPM pass. The manual
procedures above are the spec those automated tests would implement.
