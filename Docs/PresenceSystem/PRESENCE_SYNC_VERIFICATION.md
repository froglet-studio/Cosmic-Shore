# Presence Sync — Verification Guide

In-editor verification for the presence-sync branch
(`claude/multiplayer-presence-lobby-sync-6j924k`). Plan: `PRESENCE_SYNC_PLAN.md`.

**Read this first.** Every commit was authored in an environment that **cannot
open Unity or compile C#**. Nothing below has been run. Four files were authored
by hand with hand-generated GUIDs (`UgsErrorClassifier.cs`, `IModalPanel.cs`,
`PresenceState.cs`, `PresenceStateMachine.cs`), one asset was written as raw YAML
(`EventOnAppQuitRequested.asset`), and two asset YAMLs were edited as text
(`PartyServices.prefab`, `ApplicationLifecycleEvents.asset`). Step 0 exists
because of that.

Work top to bottom, stop at the first failure. Record outcomes in
`../UNITY_VERIFICATION_CHECKLIST.md`.

---

## Progress — updated 2026-08-04

| Step | State | Note |
|---|---|---|
| 0 — compile / assets | ✅ done | implied by everything below running |
| 1a — presence timeline | ✅ **pass** | B11 verified |
| 1c — benign-skip counter | ✅ **measured twice** | `presence=16, partySession=39` @ 206.5 s — see `BUGS.md` § MEASURED (run 2) |
| 2b — CONNECTING → ONLINE | ✅ **pass** | B14 verified; this was the headline symptom |
| 2d — departure | 🟡 **partial** | hard-kill case confirmed at ~30–50 s (correct); **graceful path never tested** — see the rewritten table below |
| B13 — Relay 500 boot | ✅ **pass** | 4 instances; wider run pending |
| 1b · 1d · 2a · 2c · 2e | ⬜ not run | |
| 3 — ArcadeLobbyList (RC-9) | ⬜ not run | **this is the branch's main goal — highest-value remaining step** |
| 4 — party smoke | ⬜ not run | |
| 5 — quit / background / in-match | ⬜ not run | 5a is what closes B12 |
| 6 — profile icons | ⬜ not run | blocked on the `Unknown Icon` editor action |
| 7 — rate-limit budget | ⬜ not run | |

**Recommended order for the next session:** Step 6's editor action → Step 3 →
Step 5 → Step 6 → Steps 1b/1d/2a/2e → Steps 4, 7.

---

## Two editor actions required before testing

Neither could be done from a headless environment. **Steps 1 and 6 will look
broken until you do these.**

1. **Assign `Unknown Icon`** on `_SO_Assets/SO_DefaultProfileIcons.asset`
   (new field under "Fallback"). Use something clearly *not* one of the 18 real
   avatars — a silhouette or "?" glyph. Unassigned, unresolved avatars render as
   nothing, which is honest but easy to mistake for a bug.
2. **Confirm `On App Quit Requested`** on
   `_SO_Assets/Event Channels/Lifecycle/ApplicationLifecycleEvents.asset` points
   at `EventOnAppQuitRequested`, not `None`. SOAP fields fail loud by policy, so a
   bad wire = `NullReferenceException` on quit.

---

## What shipped

| # | Commits | Effect |
|---|---|---|
| 1 | `44587a2f` `11559a93` `92ec00f7` | Benign-skip counters; one chain-walking rate-limit classifier; NetDiag on every presence catch |
| 2 | `09381def` `084dce0b` `6a3a37a5` | Interval field actually works; wall-clock accumulator; ±10% jitter; boost 0.75→1.1 |
| 3 | `52b8f5f6` | Explicit leave on quit / background |
| 4 | `b0adfa72` `8a146795` `2452a392` | **Push channel** — `ISession` events on the presence lobby |
| 5 | `641ec251` | **`PresenceStateMachine`** + vessel-spawn broadcast; `matchName` fixed |
| 6 | `c9c6db17` | Two-strike removal (no more single-read eviction) |
| 7 | `24a9b420` | **`IModalPanel`** — panels re-read every time the modal opens |
| 8 | `3b9a30fa` | Nine avatar resolvers collapsed to one; unknown ≠ icon #1 |

Landed after the first MPPM run, in response to what it found:

| Commits | Effect |
|---|---|
| `a510bd51` | B11 latch + reconcile; B12 named-id eviction; `ExitingPlayMode` departure |
| `40226752` | Push ticks do zero network I/O |
| `c3dbf682` | B13 — a Relay 500 on boot no longer bricks the splash |
| `c49c8c91` | B14 — `HasSameDisplayDataAs` so a `presenceState` change repaints the row |
| `dd1df6de` | Stackless benign-skip line (`CSDebug.LogNoStack`) |

---

## Step 0 — It compiles and the hand-authored files imported

1. Open the project; let import + compile settle. **Console must have no compile
   errors.** New files, all with hand-written `.meta`s — Unity should adopt the
   committed GUIDs rather than minting new ones:
   `Controller/Party/Services/UgsErrorClassifier.cs`,
   `Controller/Party/StateMachine/PresenceState.cs`,
   `Controller/Party/StateMachine/PresenceStateMachine.cs`,
   `UI/Interfaces/IModalPanel.cs`.
2. `_Prefabs/CORE/PartyServices.prefab` → `Refresh Interval Seconds` reads
   **1.5** (was 3).
3. Do the two editor actions above.

---

## Retest after the B11 / B12 / B14 fixes — ✅ mostly closed 2026-08-04

| Was | Result |
|---|---|
| 3 of 4 instances stuck on `CONNECTING…` forever | ✅ **fixed** — every instance reaches `Present`, every peer shows ONLINE (B11 + B14 together) |
| turning an instance off left its row for ~30 s | 🟡 **still ~30–50 s, and that is the correct answer** — deactivating an MPPM virtual player is a *process kill*, not a graceful leave. See the corrected Step 2d. The graceful path is still untested. |

If an instance is ever stuck at `Announced` again, the console says so explicitly
(`Presence reconciled to Present …` appears when the one-shot was missed and the
per-tick reconcile caught it). That line means the race is real and the reconcile
is doing its job — not a failure.

---

## Step 1 — Solo Menu_Main baseline (~2 min)

**1a. Presence state timeline.** The new machine logs every transition. Expect:

```
[PresenceStateMachine] Offline → Joining
[PresenceStateMachine] Joining → Announced
[PresenceStateMachine] Announced → Present     ← the vessel-spawn broadcast
```

If it stalls at `Announced`, `GameDataSO.OnClientReady` is not reaching
`HandleLocalVesselReady` — every peer will show you as `CONNECTING…` forever.

**1b. Cadence** ~1.5 s, not 3 s.

**1c. Benign-skip counter.** ✅ **Answered — measured twice.** The line appears
at most once per 10 s, carries no stack trace (`CSDebug.LogNoStack`), and N
climbs: the SDK stale-index defect (B1/B6) is live, ~12% of presence reads and
~32% of party-session reads. Full numbers and consequences in `BUGS.md`
§ MEASURED (runs 1 and 2).

Re-run this **after TODO-P2 lands** — that is the change expected to move the
number, and the safety-poll relaxation is gated on it.

**1d. The interval field is live.** Set it to 6, play, confirm the cadence
follows, set it back to **1.5**. Untestable before Commit 2 — the field was inert.

---

## Step 2 — Push channel, 2 editors (the core)

Needs **two Virtual Players with distinct UGS accounts.** MPPM clones sharing one
`PlayerId` produce asymmetric lists on their own — this is why `PartySystem/BUGS.md`
flags the historical B4/B5 repros as invalid.

**2a. Arrival latency.** VP-A in Menu_Main with the friends panel open; start
VP-B and time until B's row appears.

| Result | Meaning |
|---|---|
| **< 1 s** | Push works. Target. |
| ~1.5 s consistently | Push not firing — you are seeing the poll. Go to 2c. |

**2b. `CONNECTING…` then promotion.** Watch B's row closely as it arrives: it
should appear as a greyed non-invitable **CONNECTING…** row and promote to
**ONLINE** the moment B's vessel spawns. That transition *is* the broadcast. If B
appears immediately as ONLINE, the `presenceState` property is not being read —
check the compatibility default in `ReadOnlinePlayerData` (absent parses as
`Present`, so an unpublished property looks exactly like this).

**2c. If push isn't firing.** The `ISession` event names were verified against
Unity's published API reference for `com.unity.services.multiplayer@1.1`, but not
against the compiled assembly. Add a temporary `Debug.Log` in
`PresenceLobbyService.OnPushPlayerJoined`.
- Never fires → fall back to subscribing **`Changed` alone**; it fires on every
  lobby delta.
- Fires but the row is late → the drain in `HostConnectionService.Update` is
  blocked by one of its four gates; check the lobby mutex.

**2d. Departure.** Three distinct cases, three different expectations.

> **Corrected 2026-08-04.** The previous version of this table put "toggle the
> virtual player off" and "stop play mode" in the same row at **< 1 s**. That
> was wrong and it caused a false-negative retest of B12: deactivating an MPPM
> virtual player **kills the clone process**, so it belongs in the bottom row
> with the other hard kills, where ~30–50 s is the *correct* answer.

| How B goes away | What runs | Expected removal on A |
|---|---|---|
| In-game quit button / alt-F4 / window close **(build)** | `Application.wantsToQuit` → 1.5 s drain → **awaited** leave | **< 1 s** |
| Editor: stop play mode | `ExitingPlayMode` → leave dispatched, **not awaited** | **< 1 s** if the request reached the wire, else reap |
| Hard kill · crash · OS kill · **MPPM virtual-player deactivation** | *nothing* | **~30–50 s** (UGS reap) |

The first two emit an explicit UGS leave, which pushes `PlayerLeaving` carrying
B's id; A evicts that id on the spot rather than waiting out the two-strike
rule. The third cannot notify anyone. **That asymmetry is correct and
unavoidable** — there is no transport between non-party lobby members, so a hard
kill can only be caught by the UGS reap.

**MPPM cannot test row 1 or row 2 in isolation.** Deactivating a VP is row 3;
stopping play mode stops *every* VP simultaneously, so no observer survives to
watch the removal. To test the graceful path, run a **standalone build** next to
the editor and quit the build. `TODOS.md` § TODO-P10 tracks the editor-only hook
that would make this testable inside MPPM.

`TESTS.md` P5 has been rewritten to match this table.

**2e. No flicker.** Both VPs idle two minutes: no rows appearing and
disappearing, no 429 warnings, no `Reconnecting`. Commit 6's two-strike rule
targets exactly this.

---

## Step 3 — ArcadeLobbyList, the RC-9 fix

**This is the main goal — the party panel showing correct members.**

**3a. Re-read on open.** With A and B partied: open the arcade panel, close it,
have B change something (or leave), reopen. The panel must reflect current state
**on open**. Before this branch its `OnEnable` fired once per scene load and never
again, so it rendered whatever snapshot existed at scene load.

**3b. Slot 0 identity.** Change your display name / avatar in the profile modal,
then open the arcade panel. Slot 0 must show the new name and icon. These are
plain field writes that raise no SOAP event, so before Commit 7 they were
invisible to this panel indefinitely.

**3c. Online counter.** "N Players Online" must track reality across party
join/leave, not just presence changes.

**3d. Presence reconnect must not empty the party.** With A and B partied, force
a presence-layer reconnect (drop the network briefly). **B's slot must stay** and
the Leave button must remain interactable. Before this branch any presence rejoin
cleared the roster wholesale while the party was perfectly alive.

**3e. No duplicate self-row.** After 3d, A appears **once** in its own party list.
`SeedLocalPlayer` was made idempotent for exactly this.

---

## Step 4 — Party smoke (3 VPs)

Run `PartySystem/TESTS.md` S-series with uniquely tagged VPs: accept · decline ·
leave · second accept after leave.

The one real behavior change to watch: `UgsErrorClassifier` now walks
`InnerException`, so a retry engages for a wrapped 429 where it previously did
not — this touches `CreateAsync` / `JoinByIdAsync`.

---

## Step 5 — Quit, background, and in-match status

**5a. Graceful quit.** Expect a **~1.5 s pause** before the app closes — that is
`QUIT_DRAIN_SECONDS` holding the quit open so the UGS leave completes. Console:
`Departure leave complete (leaveParty=True)`.

**5b. Play-mode stop.** `Application.wantsToQuit` behavior on play-mode exit
varies by Unity version. No departure log = known limitation, not a bug; rely on
5a in a build. A 1.5 s hang is the drain working; a *longer* hang means something
is not completing.

**5c. Mobile background.** Background: the player vanishes from peers within a
second. Foreground: they reappear **and are still in their party** — pause leaves
the presence lobby only, by design. Coming back invisible means
`_leftPresenceForBackground` is not round-tripping.

**5d. IN A MATCH.** A launches an arcade game; on B's friends panel A's row must
read **IN A MATCH — <MODE>** and be non-invitable. Returning to the menu clears
it. This status was **dead code** before Commit 5 — `matchName` was never
published at all, so a player in a match rendered as idle and invitable.

---

## Step 6 — Profile icons

**6a. Unknown ≠ icon #1.** With `Unknown Icon` assigned (see top), a player whose
avatar has not resolved shows the placeholder — never a real avatar. Easiest
check: temporarily force `avatarId` to 0 somewhere and confirm the placeholder
appears in the friends panel, arcade slots, and configure-modal chips.

**6b. Late avatar repaints the chip.** Open the arcade configure modal, then have
another player join. Their chip must show their real avatar, not the placeholder.
Before Commit 8 the sprite was sampled once at chip spawn and never updated — and
the spawn event is gated on name + vessel type, **not** avatar.

**6c. Friend-request rows** show the placeholder (they carry no avatar id), not
icon #1.

**6d. No handler leak.** Open and close the configure modal several times, then
change domains. No warnings about destroyed chips; chip count stays correct. The
host close path previously never despawned chips.

---

## Step 7 — Rate-limit budget

3 VPs idle two minutes: expect **zero** `429` / `Too Many Requests` /
`Rate limited` lines.

---

## After verification passes

1. ~~**Relax the safety poll** 1.5 → 10.~~ **STILL BLOCKED.** Measured twice
   (`BUGS.md` § MEASURED runs 1 and 2). The presence read is voided ~12% of the
   time by the SDK stale-index fault and the party-session read ~32% — improved
   from ~20% / ~43%, but a 10 s nominal poll would still be a ~11.4 s effective
   backstop. Revisit only **after TODO-P2** lands and is re-measured.
2. ~~**Report the Step 1c counter.**~~ **DONE, twice.** Consequences:
   `LobbyMembershipMonitor` (`REFACTOR.md`) is **no longer blocked**, and it must
   treat `SdkStaleIndex` as explicitly *not* membership loss.
3. ~~**Rewrite `TESTS.md` P5** per Step 2d.~~ **DONE** — and Step 2d itself was
   the thing that was wrong; both are corrected.
4. ~~**Re-measure the fault rate** after `40226752`.~~ **DONE** — run 2. The rate
   fell by ~⅓ on both paths, and the *shape* of the fall (2.2× the window, only
   1.2× the skips) localises the fault to **startup**, which promotes TODO-P2 to
   the highest-value lever against it.
5. **NEW — close B12 properly.** The named-id eviction path has never been
   exercised: every departure observed so far went through the reap. Needs a
   standalone build (Step 5a) or TODO-P10.

## Known gaps (deliberate, not oversights)

- **Tombstone *rendering*** (dimmed `Unconfirmed` rows) is not implemented — only
  the eviction delay. Rows look normal during the 2-read grace window.
- **`PresenceState` has no SOAP channel.** The machine exposes a C#
  `OnStateChanged`, matching `PartyStateMachine`'s established shape. Add the SOAP
  triple in-editor if inspector-wired listeners are wanted.
- **`LobbyMembershipMonitor`** (`REFACTOR.md`) is still not extracted — it is
  gated on the Step 1c data. Commit 4 wired the *definite* membership-loss push
  (`RemovedFromSession` / `Deleted`), which needed no heuristic.
- **Granular roster SOAP events** (`OnOnlinePlayerJoined/Left/Updated`) were not
  added; the panels still use `ScriptableList` item events, so an in-place field
  change still arrives as remove-then-add and reorders a row to the bottom.

## Related

- `PRESENCE_SYNC_PLAN.md` — root causes, design, per-commit detail and deviations
- `../UNITY_VERIFICATION_CHECKLIST.md` · `../PartySystem/TESTS.md` · `TESTS.md` · `BUGS.md`
