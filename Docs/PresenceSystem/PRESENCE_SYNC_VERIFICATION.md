# Presence Sync — Verification Guide

Step-by-step in-editor verification for the presence-sync branch
(`claude/multiplayer-presence-lobby-sync-6j924k`). Plan:
`PRESENCE_SYNC_PLAN.md`.

**Read this first.** Every commit on this branch was authored in an environment
that **cannot open Unity or compile C#**. Nothing below has been run. Two files
were authored by hand with hand-generated GUIDs (`UgsErrorClassifier.cs`,
`EventOnAppQuitRequested.asset`) and two asset YAMLs were edited as raw text
(`PartyServices.prefab`, `ApplicationLifecycleEvents.asset`). Step 0 exists
because of that.

Work top to bottom. Stop at the first failure — later steps assume earlier ones
passed. Record outcomes in `../UNITY_VERIFICATION_CHECKLIST.md`.

---

## What shipped (commits 1–4 of 8)

| # | Commits | Effect |
|---|---|---|
| 1 | `44587a2f` `11559a93` `92ec00f7` | Observability: benign-skip counters, one shared chain-walking rate-limit classifier, NetDiag on every presence catch |
| 2 | `09381def` `084dce0b` `6a3a37a5` | Poll cadence: the interval field actually works, wall-clock accumulator, ±10% jitter, boost 0.75→1.1 |
| 3 | `52b8f5f6` | Explicit leave on quit / background |
| 4 | `b0adfa72` `8a146795` `2452a392` | **Push channel** — `ISession` events on the presence lobby |

Still to come: 5 (`PresenceStateMachine` + vessel-spawn broadcast), 6 (tombstones),
7 (UI binding), 8 (profile icons). **Symptom C (profile icons) is untouched so
far — do not test for it yet.**

---

## Step 0 — It compiles, and the hand-authored assets imported

Cheapest possible failure. Do this before launching anything.

1. Open the project. Wait for the import + compile to settle.
2. **Console must be free of compile errors.** Highest-risk files:
   - `Assets/_Scripts/Controller/Party/Services/UgsErrorClassifier.cs` — new file,
     hand-written `.meta` (GUID `517b66f6…`). Unity should adopt the committed
     GUID, not mint a new one.
   - `Assets/_Scripts/System/Bootstrap/ApplicationLifecycleManager.cs` — gained a
     `Cysharp.Threading.Tasks` using; confirm the assembly reference resolves.
3. **Select `_SO_Assets/Event Channels/Lifecycle/ApplicationLifecycleEvents.asset`.**
   The new **On App Quit Requested** field must show
   `EventOnAppQuitRequested`, not `None`. If it shows `None` the raw-YAML wiring
   failed — drag the asset in manually, and **the quit-leave in Step 4 cannot work
   until you do** (it will `NullReferenceException` on quit, by design: SOAP
   fields fail loud).
4. **Select `_Prefabs/CORE/PartyServices.prefab`.** `Refresh Interval Seconds`
   must read **1.5** (was 3). If it reads 3 the prefab edit did not import.

✅ Pass: no compile errors, both inspector fields correct.

---

## Step 1 — Solo Menu_Main baseline (single editor, ~2 minutes)

Enter play mode, reach Menu_Main, and just watch the console for two minutes.

**1a. Cadence.** Presence refresh activity should be roughly **1.5 s** apart, not
3 s. Commit 2 changed where the number comes from but not the number.

**1b. The benign-skip counter.** Watch for:

```
[HostConnectionService] Benign SDK fault on the presence read - refresh tick VOIDED …
  skips: presence=N, partySession=M
```

At most one line per 10 s. **Either outcome is informative:**

- **N climbing** — confirms RC-2. The SDK stale-index defect (`BUGS.md` B1/B6) is
  live and each occurrence silently discards an entire roster update. Record the
  rate in `BUGS.md`.
- **N stays 0 for the full two minutes** — **this is the more interesting
  result.** It means B1 is *not* firing on this path, RC-2 is not your staleness
  cause, and the remaining weight sits on RC-1 (no push) and RC-9 (the panel
  never re-reads). Say so — it changes how Commits 5–8 should be prioritised.

**1c. No new spam.** The original silence existed because B1 spammed every ~3 s.
If you see more than one benign line per 10 s, the throttle is broken.

**1d. The interval field is live now.** Stop, set
`PartyServices.prefab` → `Refresh Interval Seconds` to **6**, play, confirm the
cadence visibly slows, then **set it back to 1.5**. This is the single check that
Commit 2's wiring works — it was untestable before, because the field was inert.

---

## Step 2 — Push channel (2 editors — the core of the branch)

Needs **two Virtual Players with distinct UGS accounts.** MPPM clones sharing one
`PlayerId` will produce asymmetric online lists on their own and invalidate the
whole test — this is why `PartySystem/BUGS.md` flags the historical B4/B5 repros
as invalid.

**2a. Arrival latency — the headline.**
1. VP-A in Menu_Main, friends panel open.
2. Start VP-B; time from B reaching Menu_Main to B's row appearing on A.

| Result | Meaning |
|---|---|
| **< 1 s** | Push is working. This is the target. |
| ~1.5 s, consistent | Push is NOT firing; you are seeing the poll. Go to 2c. |

**2b. Departure latency.** Stop VP-B via the in-game quit path. B's row should
leave A's panel in **under a second** (Commit 3 leaves explicitly, Commit 4
delivers it). Then repeat with a **hard kill** (kill the process / force-stop the
VP) — that one is expected to take up to ~30 s. **This asymmetry is correct and
unavoidable**, not a bug: there is no transport between non-party lobby members,
so a hard kill can only be detected by the UGS reap. `TESTS.md` P5's "within 5 s"
criterion is wrong for this case and should be rewritten to
"≤1 s graceful / ≤35 s hard kill".

**2c. If push isn't firing.** The `ISession` event names were verified against
Unity's published API reference for `com.unity.services.multiplayer@1.1`, but not
against the compiled assembly. In `PresenceLobbyService.WireSessionEvents`,
temporarily add a `Debug.Log` inside `OnPushPlayerJoined` and re-run 2a.
- Log never fires → the SDK is not raising it as documented. Fall back to
  subscribing **`Changed` alone**, which fires on every lobby delta.
- Log fires but the row is late → the drain in `HostConnectionService.Update` is
  being blocked by one of its four gates. Check the lobby mutex.

**2d. Nothing regressed.** With both VPs idle in Menu_Main for a minute: no 429
warnings, no `Reconnecting`, no rows flickering in and out.

---

## Step 3 — Party smoke (3 VPs) — the highest-risk regression check

The **only real behavior change** in Commits 1–2 is that a retry now engages for
a wrapped 429 where it previously did not (`UgsErrorClassifier` walks
`InnerException`; the old `PartySessionService` classifier matched only the outer
exception). That touches `CreateAsync` / `JoinByIdAsync` retries, so the party
flow needs its normal gate.

Run `PartySystem/TESTS.md` S-series with **uniquely tagged** VPs: accept ·
decline · leave · second accept after leave.

Then specifically for this branch:

**3a. Presence reconnect must not empty the party (RC-8, pulled into Commit 4).**
With A and B in a party, force a presence-layer reconnect (pull the network
briefly, or stop/start the lobby). **A's ArcadeLobbyList must keep B's slot** and
the Leave button must stay interactable. Before this branch the roster was
cleared unconditionally on any presence rejoin, blanking slots 1–3 while the
party was perfectly alive.

**3b. No duplicate self-row.** After that reconnect, A must appear **once** in its
own party list. `SeedLocalPlayer` was made idempotent for exactly this — a
regression shows as the local player occupying two arcade slots.

---

## Step 4 — Quit and background

**4a. Graceful quit.** Quit VP-B via the in-game quit. Expect a **~1.5 s pause**
before the app closes — that is `QUIT_DRAIN_SECONDS` holding the quit open so the
UGS leave can complete. Console should show
`[HostConnectionService] Departure leave complete (leaveParty=True)`. A's panel
should drop B in under a second.

**4b. Play-mode stop.** `Application.wantsToQuit` behavior on editor play-mode
exit varies by Unity version. If stopping play mode does **not** produce the
departure log, that is a known limitation, not a bug — note it and rely on 4a in a
build. If stopping play mode **hangs for 1.5 s**, that is the drain working; if it
hangs *longer*, something is not completing and `DEPARTURE_LEAVE_TIMEOUT_MS`
needs looking at.

**4c. Mobile background** (device or a mobile-platform build). Background the app:
the player should vanish from peers' lists within a second. Foreground it: they
should reappear, and — critically — **still be in their party**. Pause leaves the
presence lobby only, by design. If they come back invisible and never rejoin, the
`_leftPresenceForBackground` flag is not round-tripping.

---

## Step 5 — Rate-limit budget

With 3 VPs idle in Menu_Main for two minutes, count `429` / `Too Many Requests` /
`Rate limited` lines. Expect **zero**. Commit 2 raised the boosted interval from
0.75 s (1.33 reads/s, over the ~1/s cap) to 1.1 s specifically to stop this.

If invites now feel sluggish, the fix is **not** lowering the boost back under
1 s — it is relaxing the safety poll now that push carries the load (see below).

---

## After verification passes

Two follow-ups are deliberately gated on the results:

1. **Relax the safety poll.** Once Step 2a confirms push works, raise
   `PartyServices.prefab` → `Refresh Interval Seconds` from **1.5 → 10**. This is
   where the rate-limit budget is actually reclaimed (~0.1 reads/s steady state).
   It was left at 1.5 deliberately so push could be additive and risk-free until
   proven. **Prefab-only change, no code.**
2. **Report the Step 1b counter.** Whether it climbed or stayed at zero decides
   how much of the remaining plan is worth doing, and it is the data
   `REFACTOR.md`'s `LobbyMembershipMonitor` extraction has been blocked on.

---

## Related

- `PRESENCE_SYNC_PLAN.md` — root causes, design, remaining commits 5–8
- `../UNITY_VERIFICATION_CHECKLIST.md` — per-commit entries to tick off
- `../PartySystem/TESTS.md` — S-series party smoke
- `TESTS.md` — P-series presence tests (P5 needs the rewrite noted in 2b)
- `BUGS.md` — B1 / B4 / B6, where the Step 1b outcome gets recorded
