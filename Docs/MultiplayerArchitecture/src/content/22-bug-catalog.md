<div class="sec-eyebrow">Part II · The record</div>

# The bug catalogue

The party and presence layers keep living bug trackers, found through Multiplayer Play Mode (MPPM)
testing. Statuses follow the project legend.

| ID | Layer | Title | Status |
|---|---|---|---|
| B1 | Presence | `LobbyPatcher` stale-index `ArgumentOutOfRangeException` spam at game start | <span class="badge investigating">🟡 Mitigated</span> |
| B2 | Party | `ObjectDisposedException` (semaphore) on play-mode abort / fast accept | <span class="badge open">🔴 Open</span> |
| B3 | Party | Bounce leaves two vessels + dead controls | <span class="badge open">🔴 Open</span> |
| B3.b | Party | Same symptom on the clean-leave path | <span class="badge fixed">🟢 Fixed</span> |
| B4 | Presence | Second invite not delivered + members vanish from 3rd player | <span class="badge open">🔴 Open</span> |
| B5 | Party | Second joiner fails to join | <span class="badge open">🔴 Open</span> |
| B6 | Presence | `WrappedLobbyService` NRE + empty online/request lists | <span class="badge investigating">🟡 Mitigated</span> |
| B7 | Party | Client pair-init runs before remote identity replicates | <span class="badge deferred">⚪ Deferred (benign)</span> |
| B8 | Party | Host phantom-rejoin loop from stale `joined_party` | <span class="badge fixed">🟢 Fixed</span> |

## The instructive fixes

::: bug B8 — host phantom-rejoin from divergent truth {fixed}
After a client left, the host re-added them every refresh tick (~3 s) from a stale `joined_party`
presence property, even as the authoritative session scan correctly removed them — a join/leave flicker
forever. **Fix 1 (load-bearing):** the presence scan now cross-checks the live session player set and
skips anyone not in it — the session is truth, the lobby is a hint. **Fix 2 (hygiene):** the leave path
now *awaits* the property clear (bounded to 3 s) so the wire is correct too. Shipped as separate commits
so a regression can be bisected.
:::

::: bug B3.b — two vessels + dead controls on leave {fixed}
Leaving a party recreated the solo session (spawning a vessel) **before** the `Menu_Main` reload
finished; the reload's fresh initializer spawned a *second* vessel, and the first survived as an orphan
(`destroyWithScene=false`) with no player pairing — uncontrollable, AI idle. An earlier band-aid was
**rejected** in favour of the real fix: despawn the surviving vessel before the reload and sequence the
flow to mirror cold-boot exactly. Root cause was confirmed from a sequential MPPM trace showing the two
spawns bracketing the scene-reload boundary.
:::

::: bug B1 — benign SDK stale-index noise {investigating}
The UGS Lobby SDK logs an `ArgumentOutOfRangeException` from `LobbyPatcher` when a WebSocket delta
references a stale local index — thrown and logged *by the SDK* before any of our `await`s, so a
try/catch can't suppress it. A `BenignLobbyLogFilter` (editor/dev only) drops exactly that signature;
the same defect on the save/get API surfaces is silenced at our own catches by matching the
**structured** `Error == Unknown` reason (message-matching was abandoned after three message variants
of the same defect appeared across three restarts). It is self-correcting — pure console hygiene.
:::

## The open ones (with reproductions)

- **B2** — `RefreshAsync` holds `_lobbyMutex` while an awaited UGS save completes frames later;
  `OnDestroy` disposes the semaphore in between, so the late `finally` releases a disposed semaphore.
  Candidate fix: a `_destroyed` guard + cancellation, or simply not disposing the semaphore.
- **B5** — the *second* of two joiners times out at `WaitForClientReadyAsync` (the host's server-side
  vessel-spawn / roster path never reaches `OnClientReady`), then bounces. Diagnose-first via NetDiag.
- **B4 / B6** — presence-lobby discovery edge cases under simultaneous joins (convergence pause freezing
  a lobby split; a stale-ref NRE emptying lists). These are the fragile, locked-design area — read the
  presence architecture before touching it.

::: insight How bugs are worked
One at a time, in priority order, each on its own commit with a risk table and a status update. Root
cause is confirmed via NetDiag log capture *before* a fix is written — the project's strong bias is to
understand the mechanism, not to suppress the symptom. The rejected B3.b band-aid is the canonical
example of that bias in action.
:::
