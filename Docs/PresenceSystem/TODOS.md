# Presence System — TODOs

Parking lot for minor improvements that don't rise to a refactor
commit or a bug. Each entry has enough context that it can be picked
up cold.

> **Read `PRESENCE_SYNC_PLAN.md` before picking anything up here.** It
> folds P2/P3/P6 into its commit order and **rejects P7 as written** —
> gating the roster diff on panel visibility manufactures the exact
> staleness bug we're fixing (`ArcadeLobbyList` never receives an enable
> event at all, because arcade modals hide by CanvasGroup).

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

### TODO-P2. Coalesce startup property writes

**Why.** Multiple clients joining the presence lobby
near-simultaneously and writing player properties rapidly is one of
the contributors to B1 (`LobbyPatcher` exception spam). Coalescing the
property writes at startup would reduce SDK delta churn.

**Touchpoint.** `LobbyPropertyWriter.SaveWithRetryAsync` (already does
post-save refresh — may need to batch).

**Risk.** Touches the fragile lobby property write path. Worth doing
*only* if B1 returns after the `BenignLobbyLogFilter` proves
insufficient.

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

### TODO-P9. Re-diff from the in-memory roster instead of re-fetching on push

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

## Diagnostics

### TODO-P4. Document `ConvergeToCanonicalAsync` race-detect semantics

**Why.** The 1500 ms settle-then-requery after creating a lobby is the
mechanism for detecting simultaneous-creation races. The exact value
and the merge-into-rival logic deserve a short doc-comment expansion
in `PresenceLobbyService` and a callout in `ARCHITECTURE.md`.

### TODO-P5. NetDiag class breakdown of presence-side failures

**Why.** Once NetDiag data accumulates from MPPM runs, a count of
`PresenceLobbyService` catch-class occurrences (`Offline` /
`SessionGone` / `RateLimit` / `Transient` / `Unknown`) would directly
inform whether `LobbyMembershipMonitor` (see `REFACTOR.md`) needs the
`StaleReference` state, the `RemovedFromLobby` state, or both.

**Action.** When investigating a presence-side bug, capture the
NetDiag log lines from the previous 24 hours of MPPM runs and tally
the class frequencies. File the tally in `BUGS.md` under the relevant
bug.

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
