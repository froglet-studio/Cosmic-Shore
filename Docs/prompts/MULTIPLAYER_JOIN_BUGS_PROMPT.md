# Prompt — fix the open multiplayer join failures

Paste everything below into a fresh session.

---

The invite program's entire distribution mechanic is **friend invites** — one to three per tester,
compounding, which is the loop the whole Deadlock-shaped milestone is built on. Definition of Done
#2 is *"Playtest build approved; Wave 0 and Wave 1 granted; friend invites enabled at one to three
per tester."*

The party-join path currently carries **two open red bugs, both of them join failures**, plus a
reverted fix and two partially-silenced defects. A cohort that grows by inviting friends cannot
grow through a broken join.

This is the engineering half of the multiplayer work. The *verification* half — D4's four-player
thirty-minute disconnect and rejoin scenarios — is tracked separately as H10 and needs a human with
several machines.

Read `Docs/README.md` (the index and the locked design), `Docs/PartySystem/ARCHITECTURE.md`,
`Docs/PresenceSystem/ARCHITECTURE.md`, and both `BUGS.md` files before touching anything.

## The locked design — do not relitigate it

**EAGER per-user Relay.** Every player hosts their own Relay-backed party session on entering
`Menu_Main`. Do **not** reintroduce lazy / on-first-invite session creation: the
shutdown-and-recreate cascade it caused is recorded as the root of every recurring party-invite
bug. `Docs/PartySystem/ARCHITECTURE.md` § "Locked design" and § "Unbreakable exit criteria" hold
the full rule.

**Threading is settled too.** Every `await` of a UGS or Netcode `Task` uses `.AsMainThread()`.
`UniTask.SwitchToMainThread()` and `UniTask.Yield(PlayerLoopTiming.Update)` have both been tried
and proven unreliable on this UniTask version — see `Docs/THREADING.md`. If a fix seems to need a
thread hop, it needs `.AsMainThread()` at the call site, not a new primitive.

## Open defects, measured 10 Sep 2026 — re-verify status before starting

| # | Defect | State |
|---|---|---|
| **Party B2** | `ObjectDisposedException` — "The semaphore has been disposed" | 🔴 open |
| **Party B5** | TC2/TC4 — **the second joiner fails to join** | 🔴 open |
| **Presence B4** | TC1 — **second invite not delivered**; party members vanish from a third player's online panel | 🔴 open |
| **Party B11** | Idle Relay allocation goes stale; every later join bounces at step 3 | ⚪ **fix REVERTED 2026-09-01** — see B14 |
| **Party B7** | Client pair-init runs before remote identity replicates | ⚪ deferred, judged mostly benign |
| **Presence B1** | `ArgumentOutOfRangeException` in `LobbyPatcher.ApplyPatchesToLobby` | 🟡 console noise silenced, **underlying SDK defect persists** |
| **Presence B6** | `NullReferenceException` in `WrappedLobbyService.GetLobbyAsync`; empty online/request lists | 🟡 refresh-path noise silenced; the empty-lists symptom is **untested since the fix** |

**B5 and Presence B4 are the two that matter most for this milestone.** Both are second-player
failures, and a compounding invite loop is by definition a sequence of second players.

A great deal has been fixed recently and should not be re-broken — B8, B9, B10, B12, B13, B14, B15,
B16 and B17 are all green, several live-verified in early September. Read the fixed entries before
changing anything near them; B16 in particular (un-spawned fauna `NetworkObject`s breaking
synchronisation for every guest) was the root cause behind B14, and the guard it left behind
(`NetworkSceneObjectGuard`) must keep running.

## What is deliberately still cut

The checkpoint cut **host migration and rejoin-in-progress**, on the reasoning that *"disconnect
handling exists. Migration was a multi-week task with an absence acceptable to an invite cohort,
and to Early Access after it."* That reasoning still holds. **Do not build host migration in this
branch.** If your diagnosis of B2 or B5 says migration is the only real fix, stop and say so rather
than starting a multi-week task inside a bug fix.

## What to do

1. **Reproduce B5 and Presence B4 in MPPM** before changing code. `Docs/PartySystem/TESTS.md` holds
   the S-series and `Docs/PresenceSystem/TESTS.md` the P-series; TC1/TC2/TC4 are named there.
   `Docs/PartySystem/MPPM_SESSION_LOG.md` records what has already been tried — read it, so the
   session does not re-walk a path that is already known not to work.
2. **Root-cause before patching.** The fixed entries in these files are mostly architectural fixes
   that closed whole classes of symptom; the reverted B11 fix is what a symptom-level patch looks
   like here.
3. **Decide B11 deliberately.** Its fix was reverted when B16 turned out to be the real cause. Either
   the stale-allocation problem is now gone (say so, close it) or it is not (re-open it with what
   you saw). Leaving a reverted fix undocumented is how it gets re-attempted.
4. **Classify every catch you touch** through the NetDiag helper — `Docs/NetworkDiagnostics/ARCHITECTURE.md`
   holds the classification rules. A swallowed party failure is indistinguishable from a working one.
5. **Re-run the S-series and P-series** and record the results, so H10 starts from a known state
   rather than from scratch.

## Constraints

- **Never write domain state from client code.** `NetDomain` is server-write; clients request via
  `Player.RequestSetDomain_ServerRpc`. A local overwrite desyncs that machine until the next
  `NetDomain` delta — `Docs/ScoringSystem/BUGS.md` B10/B11.
- **A spectator is a Netcode client with no Player object.** Both ready gates subtract it via
  `SpectatorSession.CountHumanClients`. If you touch connection approval or anything that counts
  `ConnectedClientsIds` as pilots, read `Docs/PartySystem/SPECTATOR.md` first.
- **Do not `Instantiate` a prefab carrying a `NetworkObject` without spawning it** — that is B16,
  and it makes a host unable to synchronise any guest for the rest of the session. Use
  `NetworkSceneObjectGuard.NeutralizeStray` / `Sweep`.
- Party size is bounded by `HostConnectionDataSO.MaxPartySlots` (4); the presence lobby holds 100.
  Those are different limits for different things.

## Definition of done

1. B5 and Presence B4 are either fixed and MPPM-verified, or root-caused with the fix written down
   and a reason it is not being taken now.
2. B2 is fixed or its lifetime problem is stated precisely enough to fix next.
3. B11 is explicitly closed or re-opened.
4. No new lazy-Relay, no host migration, no new threading primitive.
5. `Docs/PartySystem/BUGS.md` and `Docs/PresenceSystem/BUGS.md` reflect reality, and the session log
   records what was tried.
6. `Docs/STEAM_RELEASE_TASKS.md` R13 is ticked and H10 notes the state it inherits.
