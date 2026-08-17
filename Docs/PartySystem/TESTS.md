# Party System — Manual Test Procedures

MPPM (Multiplayer Play Mode) scenarios used as the regression gate for
every party-side commit. All tests run in Unity Editor with 3-4 virtual
players.

> **Convention.** See `../README.md` § "MPPM test convention" for VP
> naming and NetDiag class references. In party tests, **VP1 = host**;
> VP2–VP4 = joining clients.

## MPPM prerequisites — REQUIRED before ANY multi-VP test

**Every virtual player MUST have a unique tag** assigned in the
Multiplayer Play Mode window (e.g. `P2`, `P3`, `P4`) **before entering
play mode.** The main editor needs no tag.

**Why.** `AuthenticationServiceFacade.SwitchMppmProfileIfNeeded()`
derives each clone's UGS auth profile from its MPPM tags
(`mppm-{tags}`). An **untagged** clone falls back to the shared profile
**`mppm-clone`** — so ALL untagged clones sign in as the **same
anonymous UGS account** (one PlayerId). The presence lobby then holds
only two identities (main editor + the shared clone id), and each
clone's join invalidates the previous clone's lobby membership
server-side.

**Symptom of forgetting (4-instance session, 2026-07-16):** main editor
sees exactly ONE other row (labelled with whichever clone last
published its name); ONE clone sees the main editor; the remaining
clones show EMPTY online lists (dead lobby handles → refresh errors).
The asymmetry (A sees B while B sees nobody) is the identity-collision
signature — a plain lobby split cannot produce it.

**Verify:** each clone's console must log
`MPPM: Switched to auth profile 'mppm-<tag>'` with a **distinct**
profile name, and the signed-in PlayerIds must differ across all
instances.

**Corollary — tags change accounts.** Adding or changing a clone's tag
switches it to a NEW anonymous UGS account: cloud profile, display
name, XP, and relationships reset (the display name reverts to a fresh
`Pilot####` default). Re-set per-instance display names after
(re)tagging; names saved under the old profile do not carry over.

**Caveat for older bug repros.** Multi-VP sessions run before this
prerequisite was documented may have executed with untagged clones —
treat pre-2026-07-16 3-4-VP findings (notably Presence **B4**, and the
environment of **B5**/B7 repros) as suspect until re-reproduced with
tagged VPs.

## Verification ORDER — cheapest gate first

Run these in order and **stop on the first failure**. Added after the B16
threading regression, which seven commits and a full MPPM plan missed but which
**step 2 alone would have caught in under a minute** — no virtual players, no
party, no invite.

| # | Gate | Cost | Catches |
|---|---|---|---|
| 0 | Unity recompiles clean; `Test Runner → EditMode → Run All` green | ~2 min | compile breaks, contract regressions |
| 1 | `python3 Tools/Build/check_conditional_compilation.py` | ~1 s | the `#if` guard class that only fails the Release build |
| 2 | **Single editor, enter play mode in `Menu_Main`, watch the console** for `EnsureRunningOnMainThread` and for the `SceneTransitionManager` main-thread canary | ~1 min | **every off-main-thread SOAP raise** (B16) |
| 3 | 3-VP MPPM smoke: S1–S4, S9 | ~10 min | the party lifecycle |
| 4 | Stress-1/2/3 (refactor commits only) | ~15 min | races, re-entrancy |
| 5 | The scenario the change actually alters (see S10/S11 below) | varies | the thing you meant to fix |

**Step 2 is not optional on any commit that adds or moves a SOAP raise.** It is
the only gate in this list that is cheap enough to run on every single commit,
and it is the one that failed to exist when it was needed.

## ⚠ Outstanding verification — roster-truth branch, merged 2026-08-06

The roster-truth branch (`REFACTOR.md` § "Shipped — roster-truth pass") was merged
into `Ys-bleeding-edge` on the owner's call, with positive but **not exhaustive**
testing. Confirmed at merge: it compiles, the `EnsureRunningOnMainThread` errors
are gone, and invite + accept + 4 VPs in one lobby work across multiple runs.

These four were **not** run. None is known to be broken; they are simply unproven.
Listed cheapest-first so they can be picked up in any spare five minutes. **Tick
them off here as they are done** so the next person knows what is actually covered.

| | Check | Cost | If it fails |
|---|---|---|---|
| ☐ 1 | **EditMode tests** — `Test Runner → EditMode → Run All`. 41 cases were added by that branch (`PartyRosterTests`, `PartyRosterEventTests`, `PartyLobbyKeysTests`) and have never been executed. | 2 min | Almost certainly a test bug, not a product bug — but they are the contract for the coalescing, the change-gate, the main-thread deferral and the frozen wire format, so a red one is worth reading carefully. |
| ☐ 2 | **S11 — full party you are NOT in stays non-invitable** (below). **The highest-value item on this list.** The ordinary 4-in-one-party session does NOT exercise it: those rows render through `InYourParty`, a different branch entirely. | 5 min | A live bug in the newest code on the branch. `PARTY FULL` used to carry the "cannot invite them" rule implicitly; `5b36156e` moved it into a derived `targetPartyFull` in `OnlineInfoEntry.Populate`. If that derivation is wrong, you can invite into a full party and the send fails at the service. |
| ☐ 3 | **Stress-1 / Stress-2 / Stress-3** (below). Required by `REFACTOR.md`'s per-commit gate for refactor commits; not run for this branch. | 15 min | Races and re-entrancy around rapid accept/leave. The branch added a push channel and changed the refresh error matrix, both of which are exercised hardest here. |
| ☐ 4 | **C4's error-matrix branches** (`791c6d04`) — rate-limit, definite-session-gone, transient, on both the presence and party-session read paths. **Normal play never reaches these**; they only fire under UGS faults. This commit is also the one that never received an adversarial review (the reviewing agent failed twice). | hard | Misclassification during a party transition. Degrades rather than breaks — worst case a benign SDK fault is treated as definite and recreates a solo session mid-join. **This is the first commit to revert** if something odd surfaces later: it is self-contained, and its benefit (the publish surviving a voided read) is real but not urgent. |

**How to provoke #4 if you want it covered.** Rate-limit is reachable by spamming
invite/cancel until UGS 429s — you should see
`Rate limited during refresh - backing off`, **not** a silently-absorbed benign
skip. The precedence between those two is the subtle part of that commit
(`../PresenceSystem/BUGS.md` B15 RC2). Definite-gone and transient are not
practically reachable without fault injection; treat them as reviewed-by-reading
until there is a reason to do more.

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

### S9. Host return to menu with full party (post-game)

Regression for the "clients stuck in the game scene when the host taps
Main Menu" bug. The host's deliberate return must keep the party on one
live Relay and carry every client back — it must NOT route through the
disconnect/`OnSessionEnded` path (which despawns the clients' persistent
`Player` objects).

**Setup.** Run S1 to completion (VP1 host + VP2 client together in
Menu_Main, both in autopilot).

**Steps.**
1. VP1 launches a multiplayer game (e.g. HexRace) from the Arcade menu;
   confirm VP2 follows into the game scene (launch regression).
2. Play to the end so the scoreboard appears.
3. On VP1 (host), tap **Main Menu** on the scoreboard.

**Pass criterion.**
- VP1 **and** VP2 both load Menu_Main and roam in autopilot (lavalamp),
  exactly like first entry.
- Each client logs `[FLOW-6] … Raising OnClientReady` then
  `[FLOW-8] [SceneLoader] FadeFromSplashOnReady` (splash clears).
- VP1's log shows **no** client `Player` despawn during the return
  (`DestroyPlayerAndVessel` must not run on the host-initiated return).
- The Relay session id is unchanged across game → menu (party intact);
  Party Area still shows 2/4 members.
- Non-host VP2 never sees the scoreboard's Main Menu button — only the
  host returns the whole party (VP2 has "Leave Lobby" instead, see S3).
- Repeat the menu → game → menu cycle 2–3× with no leftover state.

### S10. Party-size agreement across every panel (the B15 gate)

**Why.** Three players in one party once rendered three *different* sizes
simultaneously. The count must now come from the local roster, so it is
identical on every machine by construction — this test proves that stays true.

**Setup.** 3+ tagged VPs. A invites B, B accepts. A invites C, C accepts.

**Pass criterion.**
- Open the friends list on **all three**. Every row reads `IN YOUR PARTY 3/4`.
- C's row on A's panel reaches `3/4` **within a frame** of C's vessel appearing —
  not after a poll interval. (The party-session push channel, `090f61a6`.)
- B leaves → all panels read `2/4` within one refresh tick.
- No `RaisePartyMemberJoined`/`Left` oscillation in the console (B8 regression).
- **`BenignPresenceSkips` / `BenignPartySessionSkips` still climb, and the counts
  stay correct anyway.** The SDK stale-index defect is untouched by design; that
  the numbers survive it is the proof the read/publish split works. Skips of zero
  means a lucky run — extend the session before concluding.

### S11. A full party you are NOT in stays non-invitable

**Why.** `PARTY FULL` was retired in favour of `IN PARTY 4/4`. That status was
also silently carrying the "you cannot invite them" rule, which now lives in
`OnlineInfoEntry.Populate` derived from the counts. This is the regression check
for that move — and note the ordinary 4-in-one-party case does **not** exercise
it, because those rows render through `InYourParty` instead.

**Setup.** 4 tagged VPs. Form a party of three (A+B+C); leave D solo.

**Steps + pass criterion.**
1. From **D's** panel, A/B/C read `IN PARTY 3/4` and the invite button **is**
   shown.
2. Add a fourth member so that party is at capacity.
3. From D's panel those rows now read **`IN PARTY 4/4`** — never `PARTY FULL`.
4. **The invite button is hidden and the row is dimmed.** This is the part that
   breaks if the derived rule is wrong.
5. The host's kick ✕ still appears on `IN YOUR PARTY` rows (it keys off a
   different status, but it is one line from the edit).

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

Same action as **S7** (User-cancel Accept) in the Failure-mode gate
below — run it here as the per-refactor stress check. Pass: VP2 returns
to solo Menu_Main cleanly and the log shows
`[PartyInviteController] Accept flow cancelled.` (see S7 for the full
diagnostic criterion).

## Failure-mode gate — run when investigating a bug

These intentionally provoke a failure to confirm the NetDiag overlay
labels it correctly (diagnostic-only — whether the system *responds*
differently per class is the "Possible solutions" menu in each test in
`../NetworkDiagnostics/TESTS.md`).

The four party failure scenarios are the **party-context runs** of the
diagnostic Tests A–E. The procedures live once in
`../NetworkDiagnostics/TESTS.md`; this table maps each party scenario to
its test and the expected joiner result — don't restate the steps here.

| Party scenario | Procedure | Expected on the joiner |
|---|---|---|
| **S5.** Offline during Accept (WiFi off right before Accept) | Test A | `NetDiag: class=Offline` + `[NetworkMonitor] Online → Offline` |
| **S6.** SessionGone (stop VP1 before VP2 accepts) | Test B | `NetDiag: class=SessionGone` (reachability fine) |
| **S7.** User-cancel (accept, then immediately Leave / stop the joiner VP mid-flow) | Test C | `[PartyInviteController] Accept flow cancelled.` — or `class=Cancelled` if it reaches the generic catch |
| **S8.** YS3 4-VP repro (VP1 invites VP2 + VP3 + VP4) | Test D | a `class=…` snapshot — triage per Test D (`Unknown`/`Transient` → bug; `Cancelled` from VP teardown → `TODOS.md`) |

The bounce-to-solo-menu behavior is the existing baseline, not the test
target.

## What success on these tests means

| Gate | Required for |
|---|---|
| S1, S2, S3, S4, S9 | Every party-system commit |
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
