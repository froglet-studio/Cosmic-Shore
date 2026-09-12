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

**Follow-up bug work — DONE in this session (took 4 iterations).**
Chose option (b) — silence at the catch. The matcher took four attempts
to land correctly; recorded here so future SDK signature changes know
what was tried and why:

1. **First attempt** (`5a634c8`) — `IsBenignSdkStaleIndexNre` matched
   on `Exception.StackTrace.Contains("WrappedLobbyService")` AND the
   NRE message. **Silently failed in MPPM** — the NRE kept firing every
   3 s. `Exception.StackTrace` is unreliable after several async
   `SetException` boundaries; the Unity console shows Unity's *captured*
   stack at log time, NOT the exception's own `.StackTrace` string.
2. **Second attempt** (`d2288bd`) — dropped the stack check, matched on
   type (`SessionException`) + NRE message only. Silenced the NRE form,
   but a new MPPM run surfaced the **IOOR form** of the same defect
   ("Index was out of range") on the presence-lobby read path.
3. **Third attempt** (`959c495`) — broadened to match either NRE OR
   IOOR message strings. A *fourth* MPPM restart then surfaced a THIRD
   message variant: `"Index must be within the bounds of the List."` —
   same defect, same `[Error: Unknown]`, new string. Message-matching
   was confirmed to be whack-a-mole.
4. **Fourth attempt** (this commit) — **pivoted off message strings to
   the structured `SessionException.Error` property.** All three
   variants share `[Error: Unknown]`; a genuinely actionable
   `SessionException` carries a specific `SessionError` reason
   (`SessionNotFound` / `RateLimited` / …) handled by the `[definite]` +
   rate-limit branches that run *first*. Match is
   `se.Error.ToString() == "Unknown"` (ToString avoids pinning the enum
   member across SDK versions). Variant-proof — no future message string
   can slip past it.

The `IsDefiniteSessionGoneException` method (HCS:1894) already read
`se.Error is SessionError.SessionNotFound …`, so `.Error` was a known,
proven property — should have been the first approach, not the fourth.
Lesson: when the SDK exposes a structured discriminator, match on it
before reaching for message strings.

`LobbyPropertyWriter.cs:166` "Save failed (… Index was out of range …)
— retry X/3" was demoted to `CSDebug.Log` in `5a634c8` (release-stripped
+ runtime-mute); it matches on message because it has no structured
`Error` at that callsite. Write path has only ever shown the IOOR
string. See `Docs/PresenceSystem/BUGS.md` B1 "Fix applied (option b)"
for the locked rationale + the accepted broadening trade-off.

After Editor restart with the structured-Error matcher, *all*
`SessionException`-with-`Error==Unknown` failures on the presence-lobby
+ party-session refresh paths should be silenced regardless of inner
message. Phase A baseline becomes truly clean (no ongoing B1/B6 churn).

### Pre-flight finding #3 — PartySessionService boot-retry chatter (RESOLVED)

**Observation.** After the structured-Error matcher (`a8c7208`) silenced
the HCS refresh churn, one remaining line appeared at boot — twice:
```
[PartySessionService] Transient session error — retry 1/5 … then 2/5 …
(SessionException: [Error: Unknown] [Message: Object reference not set …])
```
Stack: `LobbyHandler.SubscribeToLobbyEventsAsync` (LobbyHandler.cs:808) →
`CreateLobbyAsync` → `SessionManager.CreateAsync` →
`WrappedMultiplayerService.CreateSessionAsync` →
`PartySessionService.CreateAsync:203`.

**Diagnosis — NOT a bug, the retry loop working.** This is the
"lobby-events 23006" wire-subscription transient documented in
`PartySessionService.IsTransientSessionException` ("originate in
LobbyHandler.SubscribeToLobbyEventsAsync after the lobby is created
server-side, so retrying CreateSessionAsync is safe"). It fired at
attempt 1, retried, and recovered by attempt 3 — it did NOT reach
`retry 5/5` + propagate, which would be the genuine-failure case.
One-shot at session creation, not steady-state churn.

**Fix — demote the four retry-chatter lines to `CSDebug.Log`.** Same
category and same policy as the `LobbyPropertyWriter` "Save failed —
retry X/3" demotion in `5a634c8`: a retry that recovers is info, not a
warning. Demoted all four `Debug.LogWarning` retry lines in
`PartySessionService` (CreateAsync: host-conflict :192, rate-limit :197,
transient :203; JoinByIdAsync: rate-limit :247, transient :253) to
`CSDebug.Log` (release-stripped + runtime-mute). **The genuine-failure
path is untouched** — when any retry loop exhausts, the `when` filter
(`attempt < MAX`) fails and the exception propagates to the outer caller
(`HostConnectionService.AcceptInviteAsync` / `EnsurePartySessionAsync`),
which still logs loudly. We only quieted the "still retrying, expected
to recover" chatter, never the "gave up" signal.

This is the last category of pre-flight noise. After restart, pre-flight
should be fully clean and Phase A is ready to run for real.

### Phase A — Overlay validation

| Test | Status | NetDiag observed | Notes |
|---|---|---|---|
| A.1 — Test E happy path | **functional pass + new bug B8 surfaced** | `class=Transient` (one-shot boot retry, recovered); `class=Unknown` (1×, on Leave — ObjectDisposedException at SDK teardown, ignored per user — they Stop-ed Play mid-session) | Invite → Accept → fly → Leave cycle functionally complete. **Bug B8 discovered:** host-side phantom-rejoin loop after client leaves — see `BUGS.md` B8 for full diagnosis. Not blocking further testing. |
| A.2 — Test C user-cancel | _ready to run (next session)_ | _tbd_ | B3.b clean-leave verified-fixed on `74cde70`; A.2 is the next test in sequence. |

### Phase A.1 — Detailed findings

**Functional outcome.** Invite/Accept/Leave cycle worked. Host saw
client's vessel, both could fly, client successfully left and returned
to solo Menu_Main.

**Diagnostic findings.**
1. **`class=Transient`** at boot — known lobby-events 23006 retry,
   recovered. Now at info level via `19a380d`.
2. **`class=Unknown`** at leave — `ObjectDisposedException` from SDK
   Wire subscription teardown. User noted this was caused by stopping
   Play mid-session (manual stop interrupted leave teardown ordering).
   Ignore as test-environment artifact. **Action item carried forward:
   extend `NetworkDiagnostics.ClassifyException` to recognise
   `ObjectDisposedException` → `Disposed` (NetworkDiagnostics/TODOS.md
   TODO-6).**
3. **🔴 New bug surfaced: B8 — host-side phantom-rejoin loop after
   client leaves party.** Stale `joined_party` presence-lobby property
   on the client causes host's `ScanPresenceForJoinedPartyMembers` to
   re-add the departed client every refresh tick. Documented in
   `BUGS.md` B8 with three contributory hypotheses (fire-and-forget
   race, B1 write-retry exhaust, host-cache staleness) and four fix
   paths. **No code change yet — awaiting user decision on fix
   approach.**

B8 takes priority over advancing to Phase A.2 — it's a real
party-flow regression that affects every leave, not just Phase A.

### Phase A.1 re-verify after B8 fixes (cb65cf3 + 59fda81) — 2026-06-02

**B8 result: FIXED.** Invite + accept verified perfect on both VPs —
each controls its own vessel and flies. The host-side phantom-rejoin
flicker is gone. B8 status → 🟢 (pending only the residual-under-churn
note).

**But the clean-leave path surfaced a new bug: B3.b.** When the client
presses Leave Party, it returns to its solo host but with **two vessels
+ one Player**, the vessel won't steer, and its AI no longer seeks
crystals. Full trace + 3 root-cause hypotheses documented in `BUGS.md`
B3.b. Key points:
- This is the SAME symptom as the registered B3 but on the CLEAN-LEAVE
  path (`LeavePartyAndReturnToMenuAsync`), which B3 described as "the
  working one" — so it's a distinct variant. The leave path DOES call
  `DestroyPlayerAndVessel` (PIC:299), so B3's original root cause does
  not apply.
- Smoking guns: vessel spawns TWICE (NetObjId 4 then 6, around the
  `Container Menu_Main disposed` / `Scene Bindings Installed`
  boundary); `FindUnprocessedPlayerByOwnerClientId(0) returned NULL`
  fires twice (spawn-chain desync → no input pairing → dead controls);
  a leftover old-session vessel processes a crystal-impact RPC AFTER
  its DI container disposed (`UnknownContractException: GameDataSO` via
  `AOEExplosion` / `ExplosionHelper.cs:79`) — direct evidence a vessel
  survived the leave.
- The user confirmed `RaiseInviteResolved` (HCS:705) is the benign
  Leave-button entry, NOT the bug.

**B3.b is the new priority.** No code change yet — analysis done from
one trace; likely needs 1-2 more targeted captures to confirm the
spawn-vs-reload ordering before fixing.

### Phase A.1 second leave attempt — full sequential trace captured (2026-06-02)

User provided the complete sequential block from `[PartyInviteController]
Starting leave-lobby flow...` through `OnNetworkSpawn NetObjId=6`,
confirming hypothesis 1 (spawn-vs-reload ordering) and **invalidating**
my prior "dead controls = NULL find" claim. Two key signals from the trace:

1. **`[PLAYER] OnNetVesselIdChanged prev=4, new=6`** — the Player class
   only tracks one vessel; vessel 4 is *orphaned* the moment vessel 6
   spawns. That is the dead-controls cause.
2. **`FindUnprocessedPlayerByOwnerClientId(0) returned NULL`** fires on
   the SECOND `HandlePlayerNetworkSpawnedAsync` per cycle, *after* the
   Player was added to `_processedPlayers` by the first. Benign noise,
   would fire on every cycle. Not a bug signal. Corrected in `BUGS.md`
   B3.b root-cause section.

**Fix landed:** despawn the just-spawned vessel before the scene reload
in both `LeavePartyAndReturnToMenuAsync` (clean leave) and
`RecoverFromFailedTransitionAsync` (bounce path). Same single-file
commit. B3.b → 🟡 awaiting MPPM re-verify.

### Phase A.1 B3.b architectural refactor — band-aid reverted (2026-06-02)

User rejected the despawn-before-reload band-aid as "nasty temp fix"
and directed a clean architectural fix. After verifying that vessel
spawn already has ONE canonical Netcode call site
(`ServerPlayerVesselInitializer.SpawnVesselForPlayer`), the smell was
identified as: the leave path sequenced `LeavePartyKeepHostAsync`
(which recreates the Relay session and via UGS SDK auto-starts NM →
spawns vessel #1 in the doomed scene) **before**
`nm.SceneManager.LoadScene("Menu_Main")` (which mounts a fresh
ServerVesselInit instance → spawns vessel #2). Cold-boot doesn't have
this problem because the scene-placed ServerVesselInit only mounts
once, in a fresh Menu_Main.

Refactor (one commit):
- New bare-leave primitive `HostConnectionService.LeavePartySessionAsync()`
  — only does `PartySessionService.LeaveAsync`. No NM lifecycle, no
  session recreate.
- Old combined `HostConnectionService.LeavePartyKeepHostAsync` deleted
  (no callers remain).
- `PartyInviteController.LeavePartyAndReturnToMenuAsync` and
  `RecoverFromFailedTransitionAsync` rewritten to the explicit
  cold-boot-mirroring sequence: tear down → leave session → NM shutdown →
  clear stale refs → load Menu_Main locally via Unity SceneManager →
  recreate solo via `EnsurePartySessionAsync`. The new
  `EnsurePartySessionAsync` call now runs against a freshly-loaded
  Menu_Main where the scene-placed ServerVesselInit is also fresh →
  catches the persistent Player exactly once → ONE vessel.
- `SceneNameListSO` injected into PIC; both `"Menu_Main"` string
  literals replaced with `_sceneNames.MainMenuScene`.
- Band-aid blocks from `3e0c5bc` deleted.
- B3.b → 🟢 (fixed via refactor) — needs MPPM re-verify.

Net effect: -42 lines, no new spawn paths, no band-aid, no dead methods,
no string literals. Single canonical spawn pipeline preserved.

**MPPM-verified 2026-06-02 (commit `74cde70`).** User ran the 2-VP
leave repro and confirmed the fix:
- VP-B leaves → exactly ONE vessel in solo Menu_Main, controllable, AI
  seeks crystals.
- No `[PLAYER] OnNetVesselIdChanged prev=N, new=M` during the leave flow
  (that log line was the orphan-vessel signature — its absence is the
  proof).
- No `UnknownContractException: GameDataSO` (no orphan vessel ticking
  against a disposed DI container).
- Cold-boot smoke clean — the untouched cold-boot path did not regress.

B3.b clean-leave → 🟢 verified. The bounce/recovery path shares the same
decomposed sequence (fixed-by-construction); its independent repro is
Phase C.2 below. **Next session advances to Phase A.2 (Test C —
user-driven cancel).**

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

## Session 2 — 2026-06-09 (host-return-with-party fix + cleanup + bleeding-edge merge)

**Branch:** `claude/upbeat-dijkstra-kpu4e2` (PR #545).
**Goal:** fix the reproduced MPPM bug "party-member clients stay stuck in
the game scene when the host taps **Main Menu**", then clean up the dead
code the fix exposed and merge the latest `Ys-bleeding-edge` scoring work.

### What landed (in order)

1. **Host-return-with-party fix** (`f33abbb6`). Root cause: the host's
   scoreboard **Main Menu** button was wired (in the game scenes) into the
   *session-ended* path — `OnClickReturnToMainMenu → CloseSession_ServerRpc
   → MultiplayerSetup.LeaveSession → OnSessionEnded →
   SceneLoader.HandleActiveSessionEnd → gameData.ResetAllData →
   DestroyPlayerAndVessel`. On the host that **despawns the clients'
   persistent `Player` NetworkObjects** mid-return, racing the network
   scene load, so clients land in `Menu_Main` with nothing to rebuild from
   and hang on the splash. Fix: unplug the host button from the
   session-ended path — it now goes only through
   `SceneLoader.ReturnToMainMenu`, which keeps the live Relay and carries
   everyone. Deleted the Model-1 path (`OnClickReturnToMainMenu` +
   `CloseSession_ServerRpc` from `MultiplayerDomainGamesController`,
   `CoOpWildlifeBlitzMiniGame`, `MultiplayerFreestyleController`
   + its `RemovePlayer_*` RPCs, and `MultiplayerSetup.LeaveSession`). Also
   removed the spawner-owned network shutdown
   (`ServerPlayerVesselInitializer.shutdownNetworkOnDespawn` +
   `IsReturnToMenuTransition`) — under eager-Relay the network persists
   across scene transitions; teardown stays with `PartyInviteController`
   / `OnTransportFailure`. `HandleActiveSessionEnd`/`ResetAllData` are kept
   for genuine disconnect/transport-failure. Added regression test **S9**
   to `TESTS.md`.

2. **Host-only buttons** (`781314cf`). **Main Menu** + **Play Again** are
   host-only on both the scoreboard and the pause menu (a non-host client
   sees "Leave Lobby"). The Scoreboard gating already existed but its
   button references were unwired in the prefabs — wired `GameCanvas`,
   `GameCanvas-SkimRace`, `GameOverPanel`, `R_Pause_Menu_Panel`, and added
   the same gating to `PauseMenu`.

3. **Single-player dead-code cleanup** (`41912dc5`, `52c822ec`,
   `7d27c08b`). Solo play = a host alone in its own party session, so the
   non-networked / `IsMultiplayerMode==false` branches in the touched files
   are dead: removed `SceneLoader`'s local-vs-network split (always
   host-driven Netcode load + defensive fallback); collapsed `isMultiplayer`
   in `PauseMenu`/`Scoreboard` gating; removed `Scoreboard.SinglePlayerBannerColor`.
   (`GameDataSO.IsMultiplayerMode` kept — read project-wide.)

4. **Dead-ref cleanup** (`42b09d36`). Removed the now-unused
   `[Inject] MultiplayerSetup` from `MultiplayerMiniGameControllerBase` and
   the unused `using System.Linq` from `MultiplayerFreestyleController`;
   corrected the `shutdownNetworkOnDespawn` line in `CLAUDE.md`.

5. **Scene un-wiring** (editor commits `8b24f08d`, `e52ab68e`, `297e8853`,
   `ef6488ab`, `7b1c2880`, `ff74881c`). Removed the dead
   `OnClickReturnToMainMenu` `EventListenerNoParam` response from all 6
   game scenes that had it (SkimRace, Maelstrom, Freestyle-MP, DuelForCell,
   WildlifeBlitz-CoOp, 2v2). Verified the large YAML diffs were benign —
   prefab-instance `stripped`-stub re-serialization + cleanup of a
   pre-existing missing-script placeholder; **no functional component loss**
   (GameObject counts unchanged; the "removed" `MultiplayerSetup`/TMP
   entries live in the instanced CORE prefab).

6. **NetworkManager.prefab** (`3fa4d2b0`). Removed an accidental duplicate
   `DontDestroyOnLoad` component that rode along in an editor save
   (the same pass's `MaxPacketQueueSize 128→512` left as-is).

7. **Docs** (`d256f60b`). Fixed three reference-doc spots still describing
   `SceneLoader` "auto-selecting local vs network loading".

8. **Merge `origin/Ys-bleeding-edge`** (`ce4dc721`). Two conflicts, resolved
   with the rule *never resurrect what this branch deleted; take only
   genuinely-new features*:
   - `MultiplayerDomainGamesController` — kept Ys's new server-authoritative
     per-domain HUD score sync (`n_DomainSum0..2` + `SyncDomainSumsRoutine`);
     did **not** re-add the deleted `OnClickReturnToMainMenu`/`CloseSession_ServerRpc`.
   - `Scoreboard` — took Ys's scoring SSOT refactor in both conflict regions
     (hardcoded banner colors + `domainColorPalette` gone in favor of
     `ThemeManagerData.GetDomainUIColor`; `Results`-based cards; authoritative
     `WinnerDomain`). Our host-only button gating + unwrapped Play Again
     guard auto-merged outside the conflicts and were kept.
   Verified whole-tree: zero references to anything either side deleted; all
   of Ys's new compile deps present.

### MPPM test result (2026-06-09)

**1 host + 3 clients.** Host played a domain game to the scoreboard and
tapped **Main Menu**. ✅ **All three clients returned to `Menu_Main`
together** — the original bug (clients stuck in the game scene) is **fixed**;
S9 satisfied for the return itself.

**Two new defects surfaced on arrival → logged as B9 (deferred):**
- One client's vessel **roamed in one direction, uncontrollable**.
- Party domains were **not reset to the menu domain (Jade)** — vessels kept
  their in-game domains.

### Open items

- **B9** — returning-client autopilot-drift + missing Jade-domain reset on
  host-return (see `BUGS.md` B9). Deferred to a future session.

---

## Session 3 — 2026-07-16 (4-instance presence sanity, invite-chain branch)

**Setup.** 4 instances (main + 3 clones) on
`claude/multiplayer-invite-chain-hyocfx`, first-ever 4-way simultaneous
launch. Goal: verify online lists ahead of invite-chain (Task 4) S10
testing.

**Run 1 — FAILED (untagged clones).** Online lists broken and
asymmetric: main saw exactly one row ("B"), one clone saw only the
main editor, two clones saw EMPTY lists. Root cause: the virtual
players had **no MPPM tags**, so `SwitchMppmProfileIfNeeded()` put all
three clones on the shared `mppm-clone` auth profile → one anonymous
UGS PlayerId for all clones → the lobby held only two identities, and
each clone's join killed the previous clone's membership (dead handles
→ refresh errors → empty lists). Ruled out: the invite-chain / B4 / B5
commits (all traced inert for fresh solo instances) and the em-dash
sweep (log text only). Now a documented prerequisite:
`TESTS.md` § "MPPM prerequisites".

**Run 2 — PASS (unique tags).** With `P2/P3/P4` tags, all four
instances showed the full online list. ✅ Presence discovery verified
4-wide for the first time.

**Residual observation → next task (diagnosed, fix pending owner
confirmation).** One instance's row showed a default `Pilot####` name
instead of its custom name. Explanation: (re)tagging switches the clone
to a NEW anonymous account with a fresh cloud profile — names set under
the old profile don't carry over (see TESTS.md corollary). The live
rename pipeline itself is complete (SetDisplayName → OnProfileChanged →
RepublishLocalIdentityAsync → remote RefreshOnlinePlayersDiff
change-detect), but it is one-shot/event-driven with a silent no-op
when the lobby ref is null and no later reconciliation — hardening plan
captured in the session notes.

### Open items

- Re-set per-instance display names after tagging (expected UGS
  behavior, documented).
- ~~Name-sync hardening~~ ✅ SHIPPED (owner-confirmed, same day): (1)
  displayName/avatarId folded into the change-gated per-tick presence
  publish (guaranteed reconciliation; the event push stays for speed);
  (2) party-session player record re-published on rename
  (`PartySessionService.UpdateLocalPlayerPropertiesAsync`) + roster
  identity refresh in `PartyMemberService.SyncFromSession` + local
  party-slot entry refresh; (3) live `RoundStats.Name` mirror in
  `Player.OnNetNameValueChanged` for in-game names/scoreboards.
  Verify via `../PresenceSystem/TESTS.md` **P7**.
- B4 / B5 historical repros need re-validation with tagged VPs (see
  the caveats appended to both bug entries).

---

## Session 4 — 2026-09-11 (SOURCE AUDIT — no MPPM, no editor)

**Setup.** None. This session ran in a remote container with no Unity, no MPPM and
no live UGS, so **nothing here was executed**. It is a source audit of the two red
join bugs, recorded in this log because its conclusions change where the next real
session should start.

**Why an audit was worth doing first.** Both red bugs were last OBSERVED on
2026-07-16 or earlier. Between then and now, five separate fixes landed that each
independently produce their exact symptom: the MPPM unique-tag prerequisite (untagged
clones share one UGS `PlayerId`), B12 (a re-invite to a guest who once accepted or
declined was swallowed forever), `ScanForSignals` returning every accepter instead of
the first, the premature-`OnClientReady` fix in `ProcessPendingPairs`, and B16 (the
root cause of every synchronization failure, live-verified 2026-09-02). Re-running a
four-instance repro against a two-month-old record risks reproducing something else
and filing it under an old name.

### Findings

1. **Party B2 is fixed, and has been since 2026-08-20.** `HostConnectionService.OnDestroy`
   no longer disposes either `SemaphoreSlim`; `AvailableWaitHandle` is never read
   anywhere in `Assets/_Scripts`, so there is no handle to leak. Closed 🟢. A future
   `ObjectDisposedException` here is a different object (most likely a
   `CancellationTokenSource`) and wants its own entry.

2. **Party B11 closed as superseded.** Every symptom is B16's, and the reverted
   recycle manufactured B16's trigger every four minutes — the fix and the cause were
   the same action. It was parked ⚪ with a reverted fix, which is the one state that
   invites somebody to re-land it. `BUGS.md` B11 now states what evidence would
   reopen it (a step-3 bounce against a host that has NOT restarted its NM in-process,
   with no `PopulateScenePlacedObjects` exception).

3. **Party B5: every named cause is closed in source; the record is stale.** Traced
   one by one — see the table now in `BUGS.md` B5. Two further hazards it never named
   are also closed: the `WaitForClientReadyAsync` subscribe race (re-checked on both
   sides of the subscribe) and `RosterPullRetryLoop`'s budget, now 60 s so it outlives
   the watchdog that bounces the player. Nothing second-joiner-specific survives on
   the join path: approval is unconditional, `_processedPlayers` is keyed by
   `NetworkObjectId`, `HandleRosterRequest` is idempotent per requester, and the only
   single-slot state in the area (`_lastFiredInvite`) is one-per-inviter on the
   recipient. Moved to 🟡 — an audit cannot tell "fixed" from "broken for an unnamed
   reason", so it needs the retest, not more code.

4. **Presence B4: a real cause found and FIXED — the converge was evicting people.**
   `ConvergeToCanonicalAsync` released its non-canonical lobby through
   `DeleteOwnLobbyQuietlyAsync`, which deletes when we are host. Written for the
   simultaneous-create race (empty, seconds old), reused by the 4-second periodic
   converge on a lobby that may hold anybody — so a migrating host destroyed the lobby
   its occupants were in, freezing their online list and marching them toward the
   false `ForceReset` this system calls its main historical failure surface. Both
   halves of B4's symptom are one fact (the third player is in a different lobby), and
   this is a mechanism that both creates and prolongs exactly that. Fixed by deferring
   the migration while the hosted lobby still has other players in it. Commit
   `0c1f747b`. Left 🟡: proved from source, never run.

### What this session did NOT do

- Did not run S-series or P-series. Both suites need MPPM; this is **H10's** work and
  it now inherits a materially different starting state (see `Docs/STEAM_RELEASE_TASKS.md`
  R16).
- Did not build host migration or rejoin-in-progress — still deliberately cut.
- Did not touch the locked design: no lazy Relay, no new threading primitive, no
  change to the eager per-user session model.
- Did not open the editor. The converge fix is verified only by a Roslyn syntax parse
  (with a negative control on the edited line) and the six out-of-editor gates.

### For the next real session

Run the two retests as written in `BUGS.md` B5 and `PresenceSystem/BUGS.md` B4, with
**uniquely tagged** VPs (`TESTS.md` § "MPPM prerequisites") — the historical repros
for both bugs predate that requirement and a shared `PlayerId` reproduces both
symptoms on its own. If either still fails, it is a NEW root cause: capture both
`Player.log`s and open a fresh entry rather than re-walking the closed tables.

---

## Session 5 — 2026-09-11 (CLIENT-EXPERIENCE SCAN — no MPPM, no editor)

**Trigger.** Owner report from live play, same day: *"the multiplayer experience is not good at
all… even 4 players from US have the problem. Once you get off the happy path the game is bugged,
and the experience is not good for clients. Not in Maelstrom, not in the whole game. Connecting is
a challenge, leaving is a challenge."* Explicitly not a latency problem.

**That report outranks this tracker,** which had drop/recover marked green and engine-verified. The
bugs it names are real and were simply not in the file, because every entry here came from MPPM join
testing and nobody had audited what a client can DO once it is in.

**Method.** Source scan of every client-side transition and every screen a client can be on. Again
**nothing was run** — no Unity in this environment.

### Findings — both now fixed, both need a playtest

1. **B18 — a client could not leave.** Every exit was gated on `IsServer`. Pause menu: Main Menu
   hidden. Maelstrom scoreboard: all four buttons hidden. Maelstrom hub: only READY. So a client was
   held for the whole match, and in a Maelstrom for the whole race-to-N, with alt-F4 as the only
   exit. The correct behaviour already existed in exactly one place —
   `MaelstromSceneView.OnMainMenuPressed`, whose comment names the trap — and had not been
   propagated. Now it is.

2. **B19 — nothing watched a client's scene transition.** Every defer-to-server path covers the
   screen and returns with no timeout, so a scene event that never arrives is a permanent black
   screen. Already known in one direction (`MultiplayerSetup` routes host-loss around
   `SceneLoader` because "the defer-to-server guard hangs the client when the server is gone") but
   only for a host that is definitively GONE. A watchdog now bounces the client to its own menu.

### The finding that generalises past both

**A SOAP raise is local; it does not cross the wire.** `SceneLoader`'s three defer guards are
reached through SOAP events, so on separate machines a client never runs them at all — they fire for
MPPM virtual players, which share one `GameDataSO` in one process. What blacks out a *shipped*
client's screen is `ShowReturnToMenuVeil_ClientRpc`. The watchdog arms there too.

Two consequences worth carrying:

- **A fix armed only at a SOAP call site is an editor-only fix.** It would have demoed correctly in
  MPPM and protected nobody in a build.
- **MPPM and real hardware are not the same topology**, so an MPPM-only test plan is structurally
  blind to this whole class. Some of "it works in the editor but not for real players" is this.

### What this did NOT do

- No playtest, no editor pass. Both fixes are verified only by Roslyn syntax parse (with negative
  controls) and the six out-of-editor gates.
- Did not touch the locked design, the join handshake, or anything B5/B4 depend on.
- Did not add a status surface to the join splash. A client sees an opaque screen for up to ~60s
  during a join with no explanation, which is a real part of "connecting is a challenge" — but
  `BootStatusBroadcaster` deliberately suppresses status during expected transitions to avoid a
  misleading "tap retry", so changing it is a design call plus prefab wiring. Recorded for **R13**
  (multiplayer QoL), not taken here.
- Did not prune a departed player's `RoundStats` mid-match. `PruneDestroyedRosterEntries` runs only
  from `AddPlayer`, so a player who leaves leaves a frozen row behind until someone joins. Checked
  and deliberately left: it cannot hang an end condition (domain sums still reach their target), and
  removing a `RoundStats` mid-match has real risk with no proven benefit.

### Self-check on the change

Mid-match client leave was previously unreachable, so this makes a new path live. The host side was
verified to handle it: `OnClientDisconnect` → `ServerUnregister` + `ReconcilePartyMembersNow`, and
`Player.OnNetworkDespawn` removes the player from the roster. It is the same path a client CRASH has
always taken; the change makes it deliberate rather than only accidental.

### For the next real session

Playtest B18 and B19 with a host and at least one client — a human pass, not MPPM, since B19's real
call site is the one MPPM cannot exercise faithfully. Steps are in each entry.

---

## Session 6 — 2026-09-11 (CLIENT AGENCY + DEPARTURE HANDLING — no MPPM, no editor)

**Trigger.** Owner, continuing the same live-play report: *"at the end of each game the
client cannot go to main menu, the button is closed… they can vote for rematch… once one
client leaves mid game the vessel should be replaced by an AI and the score of the client
should still count… the vessel should not just disappear."*

Still **nothing was run** — no Unity here.

### Shipped

1. **B20 — two ready-gate hangs.** Both the MATCH gate and the LAUNCH LOBBY gate were
   evaluated only inside the press RPC, so a player leaving mid-wait left the gate
   satisfied-but-unchecked and the whole party stuck forever, with nobody able to press
   again. Now a set of WHO, re-evaluated on every press *and* every disconnect. The match
   gate had been written TWICE with both defects in each copy, so it moved to the base
   class.

2. **B21 — a departed pilot's ship is taken over by the AI, and the score survives.** Both
   the vessel and the `Player` (which carries `RoundStats`) were owned by the departing
   client and destroyed with it. Both are now `DontDestroyWithOwner` at spawn — survival is
   arranged BEFORE the disconnect so it never races Netcode's cleanup — and the disconnect
   handler only switches the AI pilot on. Announced on every peer.

3. **Rematch vote.** A client's Play Again is now a vote: recorded server-side keyed on the
   sender, toasted to everyone, and shown as a live tally on the HOST's button (the only
   peer whose press does anything).

4. **Maelstrom hub gets MAIN MENU.** Between rounds the hub offered only READY.

### The finding worth carrying

**An unauthored toast situation renders NOTHING, silently** (`GameToastController.Show`
early-returns when the library cannot resolve the mode+situation). So both new announcements
would have been dead on arrival with perfectly correct code behind them. Entries were
authored into `GameToastConfig_Shared.asset` in the same pass. *Adding a situation to the
enum is half the work; the other half is in an asset, and skipping it fails silently.*

### Editor work this branch CANNOT do (all optional — nothing is broken without it)

| What | Why | Consequence if skipped |
|---|---|---|
| Wire `PauseMenu.mainMenuButtonLabel` | Prefab field | A client's exit reads "MAIN MENU" instead of "LEAVE PARTY". Works. |
| ~~Wire `Scoreboard.playAgainLabel`~~ | Prefab field | **DONE in Session 7** — wired out-of-editor by prefab YAML surgery. It was also hiding a defect: `"REMATCH ✓"` cannot render in that font. |
| Check the Maelstrom hub's `mainMenuButton` parent | It was authored for the summary screen; if it sits under `summaryRoot` it is inert in the hub | `MaelstromSceneView` LOGS AN ERROR naming the fix at runtime, so this cannot ship silently. |

### Still not done

- No playtest. Item 2 touches the spawn/despawn path and wants a two-machine pass.
- The "warn players before they leave a game" design the owner mentioned is deliberately
  not started — it is a design conversation, and everything above makes leaving *possible*,
  which has to come first.

---

<!-- Append future sessions below this divider as ## Session 4 — date, etc. -->

---

## Session 7 — 2026-09-11 (WHO VOTED FOR A REMATCH — no MPPM, no editor)

**Trigger.** Owner: *"in a multiplayer game the client can press play again and the host would not
know, and the game is not started until the host presses the button."* Then: *"wire the
playAgainLabel and leave lobby button in the place of the continue button."*

Still **nothing was run** — no Unity here. Every asset edit was out-of-editor YAML surgery,
validate-before-write.

### Shipped

1. **The rematch vote moved onto the VOTER.** Session 6 recorded the tally as a server-side
   `HashSet<ulong>` and broadcast only the COUNT — which answers "how many" and never "who", and
   arrives as a transient RPC that is gone the moment it lands. `Player.NetRematchVote` is now a
   server-write, everyone-read `NetworkVariable` (the fifth sibling of the owner-detects /
   server-records family, after `NetArenaReady` and the three report RPCs), so the identity of
   every vote is replicated STATE any peer can read at any time. The HashSet is retired:
   `RematchVoteCount` derives from the flags and cannot drift from them, and a leaver drops out for
   free when their `Player` despawns out of the roster.

2. **`RematchVoteRoster`** draws a face per voter under the Play Again button, modelled on
   `ConnectingPlayerRoster`. Adopts a descendant named `PlayerAvatars` as its strip and that
   strip's first child as the chip TEMPLATE, so a prefab carrying only the art lights up with no
   wiring; ENSURED from `Scoreboard`, never required.

3. **B22** — `leaveLobbyButton` and `playAgainLabel` were both `{fileID: 0}`, so Session 6's
   propagated client exit was a no-op on the one screen B18's table recorded as already working.
   Both wired; `LeaveLobbyButton` cloned from Continue into Continue's own slot.

### The findings worth carrying

- **A guarded call site reads exactly like a working feature when the reference is null** — the
  `if (leaveLobbyButton)` guard is what a careful author writes, and it is also what makes a
  missing prefab reference silent. Grep the PREFAB for the field, not just the call site.
- **An authored strip is not a safe default** — `PlayerAvatars` ships INACTIVE with a sprite-less,
  raycast-target `Image` (the component Unity attaches to a layout object). Switching it on as
  authored would paint a white slab across the button and eat the press.
- **Borrowing a template's sprite as a halo only works if the template is a FRAME.** The
  connecting-panel roster does exactly that; here the template's sprite is a profile icon, so the
  halo is generated instead.
- **Wiring a display can expose the strings it was hiding** — `"REMATCH ✓"` had been in the code
  since Session 6 and could never have rendered.
- **The "editor work this branch CANNOT do" table is a punt to re-examine, not a standing fact.**
  Both of Session 6's prefab-field rows were doable out-of-editor; one is closed here.

### Still not done

- No playtest. Nothing on this branch has been opened in Unity.
- `PauseMenu.mainMenuButtonLabel` (Session 6's other prefab-field row) is still unwired.
- The host's tally reads `(n/humans)` where `humans` INCLUDES the host, who cannot vote — so it can
  never reach `(n/n)`. Pre-existing; the face row is now the primary read.
