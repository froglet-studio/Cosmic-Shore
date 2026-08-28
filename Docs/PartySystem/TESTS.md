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
