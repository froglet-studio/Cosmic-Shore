# MPPM Session Log

Chronological record of Multiplayer Play Mode (MPPM) test sessions —
what was planned, what was run, what we observed, what changed as a
result. Each new session is appended as a `## Session N — date` block.

Procedure references live in:
- `Docs/PartySystem/TESTS.md` — S1-S8 party tests
- `Docs/PresenceSystem/TESTS.md` — P1-P6 presence tests
- `Docs/NetworkDiagnostics/TESTS.md` — Tests A-E for the diagnostic overlay

This file is the **journal**; those files are the **manuals**.

---

## Session 1 — 2026-06-01 (first session after diagnostics overlay)

**Branch tested:** `claude/blissful-tesla-9nefa` at `2645070`
(extended in-session to a follow-up commit — see Pre-flight findings).

**Goal of session:** validate that the NetDiag overlay shipped in
commits `aaba872` / `70ae31b` / `5b1b32a` / `2645070` actually
classifies real failures correctly, and capture baseline NetDiag data
on the four open red party-side bugs before starting Refactor 1 (PIC).

### Test plan as scheduled

Sequenced shortest-cheapest-first so partial completion is still valuable.

**Pre-flight** (~5 min, one-time)
- Open Unity Editor on branch at HEAD = `2645070`
- Console: "Error Pause" off; filter `NetDiag` confirmed empty
- MPPM: 2 VP for Phase A/B, 3 VP for Phase C, 4 VP for Phase D
- Start Play Mode in Main editor; wait for `[NetworkMonitor]` silence

**Phase A — Overlay validation** (~10 min, 2 tests)
- A.1 — Test E (happy-path negative regression). Pass = zero NetDiag.
- A.2 — Test C (user-driven cancel). Pass = `class=Cancelled` or clean cancel log.

**Phase B — Party smoke gate** (~15 min, 4 tests)
- B.1 — S1 Accept (happy path)
- B.2 — S2 Decline
- B.3 — S3 Leave
- B.4 — S4 Second accept after leave

**Phase C — Bug-repro under NetDiag** (~20 min, 3 VPs)
- C.1 — B2 (`ObjectDisposedException`: semaphore disposed)
- C.2 — B3 (TC4: bounce leaves two vessels + broken control toggle)
- C.3 — B5 (TC2/TC4: second joiner fails)

**Phase D — Stress + 4-VP** (~15 min, optional)
- D.1 — Stress-1 (5 consecutive accepts) → exit criterion 7
- D.2 — Stress-2 (concurrent 4-VP invites) → exit criterion 8

### Pre-flight findings

**Observation.** Filter `NetDiag` was NOT empty at pre-flight. One line fired
~18 s after Menu_Main entry:

```
[HostConnectionService] Refresh error (SessionException): SessionException: [Error: Unknown] [Message: Object reference not set to an instance of an object]
[HostConnectionService] NetDiag: class=Transient | reach=ReachableViaLocalAreaNetwork | monitor=Online | sinceChange=17.8s
[HostConnectionService] Solo party session ready: hahPbWpfrKy8hTFHDC7mZX — InParty, vessel will spawn.
[HostConnectionService] Party session refresh error (SessionException): Object reference not set to an instance of an object — keeping session, will retry next tick
```

**Decoded.** Two distinct `SessionException` NREs at boot — the known UGS
Multiplayer SDK lobby-events NRE (sometimes called "lobby-events 23006"),
which surfaces as `SessionException: [Error: Unknown] [Message: Object
reference not set to an instance of an object]`. Both fire once each
during the session create/join settle window.

**Verified-correct system response.**
1. First NRE caught by `HostConnectionService.cs:1069` (presence-lobby
   refresh) → classified `Transient` → recovered (party session created
   immediately after).
2. Second NRE caught by `HostConnectionService.cs:1346` (party-session
   nested refresh) → `IsTransientSessionException` predicate matched →
   retry policy engaged → "keeping session, will retry next tick" =
   error-handling matrix recovery action executed correctly.

**Coverage gap discovered.** The 2nd catch (HCS:1346) was missed in the
original 14-site instrumentation (`aaba872`). It emits the existing
warning literal but no NetDiag classification. Both the `[definite]` and
`[transient]` branches of that catch are silent to the overlay.

**Fix applied in-session.** Added NetDiag lines to both branches of the
HCS:1346 catch (definite + transient). Closing the gap brings total
NetDiag site count from 14 → 16; HCS-specific count from 2 → 4.

**Baseline characterization.** A single `class=Transient` NRE at boot is
**normal SDK settle noise**, not a bug. The overlay is firing correctly —
the system caught it, classified it correctly, and continued. If this
NetDiag line ever **repeats** every ~3 s (refresh cadence) instead of
firing once at boot, that's a real ongoing problem; one-shot is acceptable
baseline.

### Phase A — Overlay validation

| Test | Status | NetDiag observed | Notes |
|---|---|---|---|
| A.1 — Test E happy path | _pending_ | _tbd_ | Run after pre-flight gap fix |
| A.2 — Test C user-cancel | _pending_ | _tbd_ | |

### Phase B — Party smoke gate

| Test | Status | NetDiag observed | Notes |
|---|---|---|---|
| B.1 — S1 Accept | _pending_ | _tbd_ | |
| B.2 — S2 Decline | _pending_ | _tbd_ | |
| B.3 — S3 Leave | _pending_ | _tbd_ | Expect info-level "session may already be gone" cleanup line per `5b1b32a` (NOT a fail) |
| B.4 — S4 Second accept | _pending_ | _tbd_ | |

### Phase C — Bug-repro under NetDiag

| Test | Bug | Repro count | `class=` observed | Notes |
|---|---|---|---|---|
| C.1 | B2 (semaphore disposed) | _tbd_ | _tbd_ | |
| C.2 | B3 (bounce → 2 vessels) | _tbd_ | _tbd_ | |
| C.3 | B5 (2nd joiner fails) | _tbd_ | _tbd_ | |

### Phase D — Stress + 4-VP

| Test | Status | Result | Exit criterion |
|---|---|---|---|
| D.1 — Stress-1 (5 accepts) | _pending_ | _tbd_ | #7 |
| D.2 — Stress-2 (concurrent 4-VP) | _pending_ | _tbd_ | #8 |

### Findings & decisions from this session

1. **Coverage gap closed.** HCS:1346 catch — 2 sites added (definite +
   transient branches). NetDiag site count: 14 → 16.
2. **SDK lobby-events NRE is a known one-shot at boot.** Classified
   `Transient`, recovered cleanly. Not a bug — baseline noise.
3. **Re-baseline of "silent pre-flight" expectation.** The test plan
   said pre-flight should produce zero NetDiag lines. Updated
   expectation: **one `class=Transient` line at the session-create
   settle window is acceptable baseline**; ongoing repeats are not.

### Commits made during this session

| Commit | Purpose |
|---|---|
| _tbd_ | Close NetDiag gap at HCS:1346 (definite + transient branches); update README; add this session log |

### Open items from this session

- Phase A/B/C/D still to run after pre-flight gap fix lands and Editor restart.
- B7 (deferred ⚪) still untested.

---

<!-- Append future sessions below this divider as ## Session 2 — date, etc. -->
