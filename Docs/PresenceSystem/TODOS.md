# Presence System — TODOs

Parking lot for minor improvements that don't rise to a refactor
commit or a bug. Each entry has enough context that it can be picked
up cold.

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

### TODO-P7. Reduce online-player panel scan frequency

**Why.** `RefreshOnlinePlayersDiff` runs on every refresh tick (~3-5
s), recomputing the diff for the panel even when the panel is closed.

**Touchpoint.** Gate the diff computation on whether the panel is
visible, or compute lazily on next panel open.

**Risk.** Low; doesn't affect the underlying lobby state, only the
SOAP list update frequency.
