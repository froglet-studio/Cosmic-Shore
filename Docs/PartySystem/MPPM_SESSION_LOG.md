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

### Pre-flight finding #2 (post gap-closure restart) — sustained NRE every refresh tick

**Observation (after restart on `60c076c`).** The
`[HostConnectionService] Party session refresh error (SessionException): Object reference not set to an instance of an object — keeping session, will retry next tick`
warning and the newly-paired `NetDiag: class=Transient | ...` line now
fire **every ~3 seconds** (one full refresh tick). Not a one-shot.

**Not caused by the gap-closure fix.** The warning literal has existed
since before commit `aaba872`; `60c076c` added only the appended NetDiag
line. The fix made the *periodicity* visible because the `class=Tag`
draws the eye to the pattern; pre-fix the same warning was firing but
buried in the Console stream.

**Root call site.** `HostConnectionService.cs:1345`:
```csharp
try { await _partySessionService.RefreshAsync(); }
```
which is a 1-line passthrough to the UGS SDK:
```csharp
// PartySessionService.cs:289
public async UniTask RefreshAsync()
{
    await ActiveSession.RefreshAsync().AsMainThread();
}
```
So the NRE is thrown inside the SDK's `ISession.RefreshAsync()`, not in
our code.

**Working hypothesis.** SDK lobby-events deserializer chokes on a
solo (host-only) Relay session — the eager-per-user-Relay model means
every fresh boot has a session with exactly one player (the local host)
which may be a code path the SDK doesn't handle. Existing
`IsTransientSessionException` retry policy wraps `CreateAsync` and
`JoinByIdAsync` only; `RefreshAsync` falls through to the
`HCS:1346` catch every tick.

**Verification step (pending).** Need the full stack trace from the
first NRE occurrence (Unity Console detail pane on click). Will identify
the exact SDK method that NREs.

**Hypothesis confirmation step (pending).** Invite a second VP and have
them accept; observe whether the NRE stops once the session has 2
players. If yes → confirms hypothesis → fix is to filter this NRE on
solo sessions or skip refresh while solo. If no → reject hypothesis;
investigate further.

**Phase A and beyond — blocked.** Cannot validate Phase A.1 (happy-path
negative regression) baseline until this sustained NRE is either
silenced or characterized as known/benign. Every test downstream would
have polluted NetDiag baseline.

### Pre-flight finding #2 — RESOLVED: it's B1 + B6, not a new bug

**Stack trace captured.** The NRE bottoms out in the UGS SDK, not our
code (decoded, meaningful frames only):

```
HttpClient.MakeRequestAsync          HTTP GET lobby SUCCEEDED (TrySetResult at the very bottom)
LobbyApiClient.GetLobbyAsync         (LobbyApi.cs:424) got a response
WrappedLobbyService.TryCatchRequest  (WrappedLobbyService.cs:497) THREW while processing the response
WrappedLobbyService.GetLobbyAsync    (WrappedLobbyService.cs:170)
LobbyHandler.RefreshLobbyAsync       (LobbyHandler.cs:431)
PartySessionService.RefreshAsync     (our 1-line passthrough, cs:292)
HostConnectionService:1385           (our catch logs it)
```

**This invalidates the solo-session hypothesis.** The failing frame —
`WrappedLobbyService.GetLobbyAsync` (`WrappedLobbyService.cs:170`) — is
the **exact SDK frame already documented in bug B6**
(`Docs/PresenceSystem/BUGS.md`, recorded there as
`WrappedLobbyService.cs:165/462` — same methods, minor line drift across
SDK reads). The HTTP request itself succeeds; the SDK NREs while
*deserializing the lobby response* against a stale local cache.

**The side-note save-failures are B1.** Before each refresh NRE, the
user saw three:
```
[LobbyPropertyWriter] Save failed (SessionException: Index was out of range.
Must be non-negative and less than the size of the collection.
Parameter name: index) — retry 1/3 … 2/3 … 3/3
```
`LobbyPropertyWriter.SaveWithRetryAsync` (`cs:158-160`) **already**
explicitly catches `"Index was out of range"` and retries — this is the
write-path manifestation of the same SDK stale-index family as B1
(`LobbyPatcher.ApplyPatchesToLobby` ArgumentOutOfRangeException). The
property *write* trips the SDK's index bookkeeping; the subsequent
*read* (`GetLobbyAsync`) then NREs on the same corrupted cache. They are
the same underlying SDK defect on two API surfaces, firing in a
feedback loop:

```
LobbyPropertyWriter.SaveWithRetryAsync  → SaveCurrentPlayerDataAsync corrupts/races SDK index
  → post-save lobby.RefreshAsync()      → GetLobbyAsync NREs on the stale cache
  → retry writes again (×3)             → more deltas → more stale-index churn
  → PartySessionService.RefreshAsync()  → GetLobbyAsync NREs again every 3 s
```

**Why it's continuous, not one-shot (corrected baseline).** Earlier in
this log I wrote "one-shot at boot is acceptable, repeats are a real
problem." The repeats ARE the known B1/B6 SDK churn — they are *already
classified as known-benign-but-noisy* in the bug docs. The overlay
correctly classifies them `Transient`. So:

- **The NRE itself is a known SDK defect (B1/B6 family), not a
  regression and not caused by any commit in this branch.** The
  warning literals predate the overlay.
- **The overlay is working as designed** — it surfaced a known-benign
  churn pattern that was previously buried.
- **The real defect** is that the B1 `BenignLobbyLogFilter` only
  suppresses the `LobbyPatcher` `ArgumentOutOfRangeException` signature
  — it does NOT cover (a) the `WrappedLobbyService.GetLobbyAsync` NRE on
  the read path, nor (b) the `LobbyPropertyWriter` "Index was out of
  range" `SessionException` on the write path. Both leak to the console.

**Decision: Phase A UNBLOCKED.** This is documented known noise, not a
live regression. The test plan's "silent pre-flight" expectation was
wrong for a branch with active B1/B6 churn. Proceed with Phase A, but
record the per-test NetDiag baseline as "+ ongoing B1/B6 `class=Transient`
churn every ~3 s" so happy-path deltas are still readable (look for NEW
classes — Offline / SessionGone / Timeout — not the steady Transient
hum).

**Follow-up bug work — DONE in this session (took 3 iterations).**
Chose option (b) — silence at the catch. The matcher took three
attempts to land correctly; recorded here so future SDK signature
changes know what was tried and why:

1. **First attempt** (`5a634c8`) — `IsBenignSdkStaleIndexNre` matched
   on `Exception.StackTrace.Contains("WrappedLobbyService")` AND the
   NRE message. **Silently failed in MPPM** — the NRE kept firing every
   3 s. `Exception.StackTrace` is unreliable after several async
   `SetException` boundaries; the Unity console shows Unity's *captured*
   stack at log time, NOT the exception's own `.StackTrace` string.
2. **Second attempt** (`d2288bd`) — dropped the stack check, matched on
   type (`SessionException`) + NRE message only. Silenced the NRE form,
   but a new MPPM run surfaced the **IOOR form** of the same defect
   ("Index was out of range") on the presence-lobby read path. Same SDK
   bug, different message; not caught by the NRE-only match.
3. **Third attempt** (this commit) — broadened to match either NRE OR
   IOOR message strings on a `SessionException`. Renamed
   `IsBenignSdkStaleIndexNre` → `IsBenignSdkStaleIndexError` since it
   no longer matches only NRE-form. Symmetric with
   `LobbyPropertyWriter`'s existing two-string filter
   (`"Too Many Requests" || "Index was out of range"`).

`LobbyPropertyWriter.cs:166` "Save failed (… Index was out of range …)
— retry X/3" was demoted to `CSDebug.Log` in `5a634c8` (release-stripped
+ runtime-mute) and that was message-based all along, so it was
unaffected by the stack-vs-message trap. See
`Docs/PresenceSystem/BUGS.md` B1 "Fix applied (option b)" for the
locked rationale.

After Editor restart with the third-attempt matcher, both NRE-form and
IOOR-form `SessionException`s on the presence-lobby + party-session
refresh paths should be silenced. Phase A baseline becomes truly clean
(no ongoing B1/B6 churn in console).

### Phase A — Overlay validation

| Test | Status | NetDiag observed | Notes |
|---|---|---|---|
| A.1 — Test E happy path | _ready to run_ | _tbd_ | Baseline = ongoing B1/B6 Transient churn; look for NEW classes only |
| A.2 — Test C user-cancel | _ready to run_ | _tbd_ | |

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
