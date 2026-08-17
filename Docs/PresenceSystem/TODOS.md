# Presence System — TODOs

Parking lot for minor improvements that don't rise to a refactor
commit or a bug. Each entry has enough context that it can be picked
up cold.

> **Read `PRESENCE_SYNC_PLAN.md` before picking anything up here.** It
> folds P2/P3/P6 into its commit order and **rejects P7 as written** —
> gating the roster diff on panel visibility manufactures the exact
> staleness bug we're fixing (`ArcadeLobbyList` never receives an enable
> event at all, because arcade modals hide by CanvasGroup).

## Priority as of 2026-08-04

| | Item | State |
|---|---|---|
| 1 | Finish the verification pass — Step 3 (`ArcadeLobbyList`) is the branch's stated goal and is still unrun | see `PRESENCE_SYNC_VERIFICATION.md` § Progress |
| 2 | **TODO-P2** — coalesce startup property writes | ⬆ promoted; now the highest-value code item, backed by two measurements. **Partial down-payment shipped** — `4129b932` removed six write-only keys from every party-session create/join (see B15 RC5). The startup burst is still unbatched; re-measure the skip counters before deciding what is left. |
| 3 | **NEW** — `HostConnectionService.Update()` is dead in-game (`IsOnMenuScene()` gate at the top), so party counts freeze at whatever was last published when the match started. Found during the B15 pass, deliberately left out of scope. Decide whether the roster should track in-match at all before changing it — `matchName` already covers the in-match *display*. | 🆕 unscoped |
| 3 | **TODO-P10** — editor departure hook | 🆕 unblocks the one B12 path no test can reach today |
| 4 | `LobbyMembershipMonitor` extraction (`REFACTOR.md`) | 🔓 unblocked by TODO-P5's answer |
| 5 | B4 / B6 retest with tagged VPs | pre-existing 🔴, may close for free |
| — | Safety poll 1.5 s → 10 s | ⛔ **BLOCKED** on TODO-P2 — see below |
| — | **TODO-P8** — presence heartbeat | ⛔ **CLOSED, do not build** — costed and rejected |

**⛔ Safety-poll relaxation (1.5 s → 10 s) — do not land this.** It is staged
in `PRESENCE_SYNC_PLAN.md` as a trivial prefab-only edit once push was
confirmed, so it will look free to whoever reads that next. It is not: with
the presence read voided ~12% of the time and the party-session read ~32%, a
10 s nominal poll is a ~11.4 s effective backstop and worse on the party path.
Land TODO-P2, re-measure with the Step 1c counters, then decide.

## Code health

### TODO-P1. Document `BuildLocalPlayerProperties` `invite_payloads` reset

**Why.** `PresenceLobbyService.BuildLocalPlayerProperties`
(~`:335-350`) intentionally resets `invite_payloads` to empty on every
rebuild. This is documented in a code comment but is a frequent source
of confusion for B4-style invite-vanish bugs.

**Action.** Add a short paragraph to `ARCHITECTURE.md` § "How invites
travel" explaining the reset semantics and why
(`BuildLocalPlayerProperties` is called on rejoin / reconnect, and
preserving stale invites across a rejoin would mis-deliver them).

### TODO-P2. Coalesce startup property writes — ⬆ **PROMOTED: highest-value item on this list**

**Why.** Multiple clients joining the presence lobby
near-simultaneously and writing player properties rapidly is one of
the contributors to B1 (`LobbyPatcher` exception spam). Coalescing the
property writes at startup would reduce SDK delta churn.

**Now backed by data, not speculation (2026-08-04).** Two independent
measurements of the benign-skip counters (`BUGS.md` § MEASURED runs 1 and 2)
show the stale-index fault voiding **~12% of presence reads and ~32% of
party-session reads** — and, decisively, show the rate **falling as the
window lengthens**: run 2 covers 2.2× the wall time of run 1 but carries
only 1.2× the presence skips. A defect whose per-tick rate decays with
session age is one that fires mostly in the opening seconds, which is
exactly the multi-client write burst this item targets.

**What it unblocks.** The safety-poll relaxation (1.5 s → 10 s) is blocked
solely on this number. A voided tick skips the roster diff, invite scan,
acceptance scan, member sync *and* the presence publish, so the fault is a
direct multiplier on the effective poll cadence — at ~32% the party-session
backstop is a third slower than it reads on the inspector.

**Touchpoint.** `LobbyPropertyWriter.SaveWithRetryAsync` (already does
post-save refresh — may need to batch), plus whatever fires multiple
distinct writes during `ApplyPostLobbyJoinState`.

**Risk.** Touches the fragile lobby property write path — the locked-design
area. Read `ARCHITECTURE.md` first. Measure before and after with the
existing counters; this item now has a numeric pass criterion, which it did
not before.

### TODO-P3. Add jitter to base refresh interval

**Why.** All clients refresh on the same 5 s tick, which clusters
property reads/writes at the SDK and contributes to rate-limit
incidence on the 429 hot window after invite-receive.

**Touchpoint.** `LobbyRefreshScheduler` — base interval is currently
fixed. Add a per-client uniform jitter of ±10% so refreshes spread out
across the wall-clock window.

**Risk.** Low; jitter is additive, doesn't change median cadence
materially.

## Liveness

### TODO-P8. Do NOT add a presence heartbeat — costed and rejected

Costed in full in `LIVENESS_COST_ANALYSIS.md`. Summary of why it is closed:

- **We are at 9 of the 10 player-data-values cap** (a hard UGS limit). A
  `lastSeen` key spends the last slot on a keepalive.
- To beat the ~30 s reap meaningfully the interval must be ~2 s; at a
  comfortable 10 s the threshold lands at ~25 s, i.e. no gain.
- At 2 s it breaches the read cap **even at N=4** as the code stands, and
  sustains exactly the `LobbyPatcher` delta churn behind B1/B6.
- The planned private friend list makes it moot: UGS Friends presence is
  server-tracked and push-based, costs no property slots, and fans out per
  friend rather than O(N²) over all 100 lobby members.

### TODO-P10. Editor hook to fire a graceful departure on demand

**Why.** The named-id eviction path added for B12
(`PresenceLobbyService.TryConsumeDepartedPlayerIds` →
`RefreshOnlinePlayersDiff` immediate evict) has **never been executed by a
test.** Every departure observed in MPPM so far went through the UGS reap,
because there is no way to make one virtual player quit gracefully while the
others keep watching:

- **Deactivating a virtual player kills the clone process** — no
  `Application.wantsToQuit`, no `EditorApplication.playModeStateChanged`, no
  code runs at all. This is a hard kill and correctly lands on the ~30–50 s
  reap floor. It is *not* a B12 regression, though it looks exactly like one
  (and did: `BUGS.md` § B12 retest 2026-08-04).
- **Stopping play mode in the main editor** does fire `ExitingPlayMode`, but
  it stops every virtual player at once, leaving no observer.

The only route that reaches `wantsToQuit` today is quitting a **standalone
build**, which means the fastest, most-used multiplayer test harness in the
project cannot verify one of its own fixes.

**Action.** An editor-only menu item / debug key that calls
`HostConnectionService.HandleAppQuitRequested` (or raises
`ApplicationLifecycleManager.OnAppQuitRequested` directly) **without**
quitting. The instance stays alive but leaves the presence lobby, so a peer
can watch the row vanish. `FrogletTools > Multiplayer > Simulate Departure` fits
the existing tool-menu convention.

**Value.** Turns a build-only, seldom-run test into a two-click one, on the
exact path most likely to silently rot — a leave that never fires is
indistinguishable from a leave that fires and is ignored, since both end at
the reap.

**Size.** ~20 lines, editor-only, no runtime behaviour change.

**Note.** Leaving without quitting is a state the app never reaches in
production, so the hook should log loudly and the instance should be treated
as expendable afterwards — do not fold it into a rejoin flow.

### TODO-P9. Re-diff from the in-memory roster instead of re-fetching on push — ✅ DONE

**Why.** `HostConnectionService.Update` drains the push flag by calling
`RefreshAsync()`, which issues a `GetLobby`. The SDK has already applied the
delta locally before the callback fires (Unity documents `PlayerJoined` as
firing "right after the session gets updated", and `LobbyPatcher` exists
precisely to patch local state from deltas), so the fetch is redundant.

**Value.** Removes one `GetLobby` per inbound delta per client — the dominant
read cost at any lobby size — and is what makes relaxing the safety poll from
1.5 s to 10 s safe.

**Introduced by** `8a146795`, which routed push through the existing
poll-shaped refresh because it was the smallest diff. Correct, but not cheap.

**Shipped.** `RefreshAsync(bool fetchFromServer)`; push ticks pass `false` and do
zero network I/O. The converge `QuerySessions` and the party-session refresh are
fetch-only too, and the reconnect watchdog is now fed *only* by fetch ticks (a
push tick can neither clear nor increment the counter — letting it increment
while only fetch ticks cleared would have been a one-way ratchet toward a false
reconnect during an invite burst). The scheduler is no longer `Reset()` on push:
that was there to avoid a redundant read, and with no read there is nothing
redundant — while suppressing the safety poll because push fired is backwards,
since the poll exists to catch what push misses.

## Diagnostics

### TODO-P4. Document `ConvergeToCanonicalAsync` race-detect semantics

**Why.** The 1500 ms settle-then-requery after creating a lobby is the
mechanism for detecting simultaneous-creation races. The exact value
and the merge-into-rival logic deserve a short doc-comment expansion
in `PresenceLobbyService` and a callout in `ARCHITECTURE.md`.

### TODO-P5. NetDiag class breakdown of presence-side failures — ✅ ANSWERED (2026-08-04)

**Why.** Once NetDiag data accumulates from MPPM runs, a count of
`PresenceLobbyService` catch-class occurrences (`Offline` /
`SessionGone` / `RateLimit` / `Transient` / `Unknown`) would directly
inform whether `LobbyMembershipMonitor` (see `REFACTOR.md`) needs the
`StaleReference` state, the `RemovedFromLobby` state, or both.

**Answer.** Two measured runs (`BUGS.md` § MEASURED) are dominated by a
single class: `class=Transient`, defect `SdkStaleIndex`, on both the
presence and party-session read paths, at ~12% / ~32% of fetch ticks. No
`SessionGone` and no `Offline` appeared in either run.

**Consequences — both actionable now:**

1. **`LobbyMembershipMonitor` is no longer blocked.** It was gated on exactly
   this data (`REFACTOR.md`).
2. **It must treat `SdkStaleIndex` as explicitly NOT membership loss.** At
   this rate an error-count watchdog that counted it would escalate to a
   false reconnect constantly. The `StaleReference` state is required; a
   `RemovedFromLobby` state has no observed evidence behind it and should
   wait for a run that actually produces one (the definite-loss pushes,
   `RemovedFromSession` / `Deleted`, already cover that case without a
   heuristic).

**Re-run** after TODO-P2 to confirm the class mix does not shift.

**Mechanism.** The generic class-count aggregation tool is tracked in
`../NetworkDiagnostics/TODOS.md` § "TODO-8. Aggregate NetDiag class
counts across MPPM runs"; this entry is the presence-specific *use* of
that data (deciding `LobbyMembershipMonitor` states).

## UI / UX

### TODO-P6. "Reconnecting…" indicator

**Why.** When `HostConnectionService` enters `Reconnecting` state, no
UI feedback today. Users see an empty online-player panel with no
explanation.

**Touchpoint.** `FriendsListPanel` and `ArcadeLobbyList` could
subscribe to `PartyStateMachine` state changes and overlay a small
spinner / text label during `Reconnecting`.

**Risk.** Cosmetic; UI-only.

## Performance / polish

### TODO-P7. Reduce online-player panel scan frequency — **WON'T DO**

**Original why.** `RefreshOnlinePlayersDiff` runs on every refresh tick,
recomputing the diff even when the panel is closed. Proposed action was
to gate the diff on panel visibility.

**Rejected.** Landing this manufactures the staleness bug it looks like
it would help:

- The diff is not the cost. `RefreshAsync` also carries invite
  detection, the acceptance handshake, the joined-member scan and the
  presence publish — `INVITE_ENHANCEMENTS.md:386` already warns "Do NOT
  stop the poll when closed."
- `ArcadeLobbyList` has no usable visibility signal today: arcade modals
  hide via `ModalWindowManager.SetCanvasGroupVisible`, never
  `SetActive(false)`, so its `OnEnable` fires once per **scene load**,
  not per open. Gating on visibility would freeze it permanently.

**Superseded by** the push channel in `PRESENCE_SYNC_PLAN.md` § 4.4,
which drops steady-state reads to ~0.1/s and makes the diff event-driven
rather than periodic — removing the cost this item was chasing.
