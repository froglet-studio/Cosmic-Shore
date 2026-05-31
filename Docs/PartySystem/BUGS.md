# Party System — Open Bugs

Living tracker for party-side issues found in MPPM testing. Companion
to `ARCHITECTURE.md` (locked design), `REFACTOR.md` (active refactor
queue), and `../NetworkDiagnostics/README.md` (catch-block diagnostics).

Presence-lobby-specific bugs (B1, B4, B6 from the old tracker) moved
to `../PresenceSystem/BUGS.md`.

Statuses: 🔴 open · 🟡 investigating · 🟢 fixed (commit) · ⚪ deferred.

| ID | Title | Confidence | Status |
|----|-------|-----------|--------|
| B2 | `ObjectDisposedException` (semaphore) on Play-Mode abort / fast invite-accept | ~95% | 🔴 |
| B3 | TC4 bounce leaves 2 vessels + dead controls | ~85% | 🔴 |
| B5 | TC2/TC4 second joiner fails to join | Uncertain (diagnose first) | 🔴 |
| B7 | Client pair-init runs before remote identity replicates (`InitializePair Player=` empty, vessel-type `Random`) | Verified mostly benign | ⚪ |

---

## B2 — `ObjectDisposedException`: "The semaphore has been disposed" 🔴

**Symptom.** `HostConnectionService.RefreshAsync()` at `:1052` →
`SemaphoreSlim.Release()` → "The semaphore has been disposed." Triggered
by stopping Play Mode (Editor abort) while players are partied/roaming,
or by clicking invite / accepting too fast.

**Root cause (~95%).** `RefreshAsync` acquires `_lobbyMutex` (`WaitAsync(0)`
at `:927`), then awaits `PublishPartyStateIfChangedAsync` (`:1592`) →
`SaveWithRetryAsync` (`LobbyPropertyWriter.cs:147`, a UGS save that can
complete frames later). Meanwhile `OnDestroy` runs and **disposes**
`_propertyWriter.LobbyMutex` / `SessionCreationMutex` (~`:374-375`). The
late save completes, the `finally` calls `_lobbyMutex.Release()` on the
disposed semaphore → throw. No `CancellationToken` aborts in-flight
refreshes before disposal; no `_destroyed` guard on the `Release`;
`OnDestroy` disposes *before* its `await _lobbyService.LeaveAsync()`
settles.

**Candidate approaches.** Don't dispose the semaphores at all
(`SemaphoreSlim.Dispose` is only required if `AvailableWaitHandle` was
used — confirm it isn't); **or** add a `_destroyed` flag + cancellation
that stops `Update`→`RefreshAsync` re-entry and guards the `finally`
`Release`; **or** reorder `OnDestroy` so disposal happens only after
in-flight work is cancelled/awaited. Pick the simplest that fully closes
the race.

**Diagnostic upgrade (post commit `aaba872`).** Any future occurrence
will now carry a NetDiag tag on its log line — likely `class=Cancelled`
(if it fires during Play Mode stop) or `class=Unknown` (if it fires
during fast-accept). The class will help confirm the trigger.

**Evidence.** `HostConnectionService.cs:927, 1052, ~340-385` (OnDestroy),
`:1592`; `LobbyPropertyWriter.cs:141-171`.

---

## B3 — TC4: bounce leaves two vessels + broken control toggle 🔴

**Symptom.** VP1 has VP3 partied (ok). VP1 invites VP2; VP2 accepts but
can't connect and bounces to its own solo lava-lamp. There, **two
vessels** spiral on autopilot (no target crystal); tapping to take
control yields a vessel that **stays in AI mode / won't steer**.

**Root cause — two vessels (~85%).**
`PartyInviteController.RecoverFromFailedTransitionAsync` (~`:410-437`)
calls `LeavePartyKeepHostAsync` + reloads `Menu_Main` but does **not**
call `gameData.DestroyPlayerAndVessel()` — unlike its sibling
`LeavePartyAndReturnToMenuAsync` (~`:292`). Menu vessels spawn with
`DestroyVesselWithScene = false` (`MenuServerPlayerVesselInitializer.cs:40`),
so a vessel from the failed session survives the solo-host restart while
`Menu_Main` reload spawns a fresh one → two vessels.

**Root cause — dead controls (medium).** Likely coupled to the
duplicate: the toggle may act on a stale `LocalPlayer`/vessel, or
`MenuCrystalClickHandler` state (`_isInFreestyle`, `_isTransitioning`,
`_cts`) isn't reset after the reload, and/or
`MainMenuController.ActivateLocalPlayerAutopilot` (~`:249-267`, sets AI
on + input paused) races the toggle. Needs confirmation.

**Candidate approach.** Mirror the working
`LeavePartyAndReturnToMenuAsync` cleanup in the bounce path (explicit
despawn/`DestroyPlayerAndVessel` before solo restart); verify toggle
state resets on the reload. Possibly add brief diagnostics to confirm
which vessel the toggle targets.

**Diagnostic upgrade.** The bounce path itself now emits a NetDiag log
in `RecoverFromFailedTransitionAsync` — confirming the trigger class for
the bounce.

**Evidence.** `PartyInviteController.cs:~410-437` vs `~267-332` (`:292`);
`MenuServerPlayerVesselInitializer.cs:40`; `MenuCrystalClickHandler.cs`
(toggle + OnEnable); `MainMenuController.cs:~198-267`.

---

## B5 — TC2/TC4: the second joiner fails to join 🔴

**Symptom.** VP1 invites VP2 and VP3. VP3 accepts first → joins ok. VP2
accepts second (invite **was** received — confirmed) → **join fails**
(→ Commit-16 watchdog bounce, hence B3).

**Notes.** Invite carries the **real** session id (eager creation,
`HostConnectionService.cs:524`), so this is **not** the `PENDING` path.
Connection approval is unconditional and capacity is fine
(`MaxPartySlots` ≥ 4). So the most likely failure is **host-side**: the
second late client never reaches `OnClientReady` (host vessel-spawn /
client-pull roster path), so `PartyInviteController` times out
`WaitForClientReadyAsync` and bounces.

**Approach.** Diagnose first — NetDiag now classifies the timeout via
the `WaitForClientReadyAsync` or `WaitForClientConnectionAsync` catch.
Look for `class=Cancelled` on the joiner with `monitor=Online` to
confirm the host's server-side spawn never completed (vs. a network
event).

**Evidence.** `PartyInviteController.cs` (AcceptInviteAsync awaits);
Commit-16 roster-pull in `ClientPlayerVesselInitializer` /
`ServerPlayerVesselInitializer`; `MultiplayerSetup.cs` (approval).

---

## B7 — Client pair-init runs before remote identity replicates ⚪

**Symptom.** Observed on 3-VP MPPM with YS1/YS2/YS3 after the YS2/YS3
join succeeded. YS2's first `[ClientPlayerVesselInitializer]
InitializePair` log shows `Player=` (empty); YS3 spawns on YS2 as
`Name=, VesselType=Random`. Self-heals within a few ticks once
NetworkVariables replicate.

**Root cause.** `player.Name` is `NetName.Value` snapshotted at
`Player.OnNetworkSpawn` (`Player.cs:148`); `NetName` and
`NetDefaultVesselType` are **owner-written** `NetworkVariable`s
(`Player.cs:30,32`) that replicate a tick *after* the `NetworkObject`
spawns. The server mirrors the same issue with an `IsSpawnReady` gate
(`Player.cs:486-488`) before it processes a player — but the client
`InitializePair` path in `ClientPlayerVesselInitializer` (re-triggered
by spawn events `OnPlayerNetworkSpawnedUlong` /
`OnVesselNetworkSpawned`) does *not* gate on identity, so a remote pair
can wire through with empty `Name` / `Random` vessel-type.

**Verified mostly benign (don't escalate, document and revisit).**
- **Vessel GameObject correct.** Server spawns the right prefab from
  the authoritative vessel type; the client attaches via `NetVesselId`.
- **Party-member UI correct.** Names come from
  `PartyPlayerData.DisplayName` (resolved from the lobby/invite via
  `RaisePartyMemberJoined`), not `player.Name`.
- **Scoreboard / score cards correct.** `Scoreboard` reads names from
  `RoundStats.Name` (server-written only after `IsSpawnReady` gate).
- **Vessel visuals / HUD correct.** HUD/icon/customization key off the
  spawned vessel's own `vesselType` field and live `Domain`, not the
  player's `NetDefaultVesselType`.
- **Residual risk (thin, not menu-reachable).** `GameFeedAPI`
  joust/disconnect feed text *could* read empty if the relevant event
  fired inside the sub-replication window. Not reachable in the menu —
  there's no joust/disconnect feed there.

**Two candidate approaches** (when this is tackled):

1. **Gate (mirror server `IsSpawnReady`).** Defer client `InitializePair`
   until `NetName` is non-empty *and* `NetDefaultVesselType` is valid.
   Touches the fragile spawn-critical path; needs a new identity-
   replication trigger + timeout fallback.
2. **Notify-on-change (lighter).** Make the client branch of those two
   handlers raise a SOAP notification so any live/cached consumer
   refreshes; don't gate spawn. Smaller blast radius.

**Status.** Deferred — verified mostly benign. Revisit if a real
consumer (joust feed, disconnect feed, scoreboard pre-spawn read)
becomes reachable in a flow where the timing window matters.

**Evidence.** `Player.cs:148` (snapshot), `Player.cs:30,32` (owner-
written NetworkVariables), `Player.cs:152-156,486-488` (server-only
`RoundStats.Name` + spawn gate), `Player.cs:446-464` (server-only
deferred-spawn raise from handlers),
`ClientPlayerVesselInitializer.cs:35,284,344-349` (pending queue,
re-trigger, pair init), `Scoreboard.cs:307,315` (uses `RoundStats.Name`).

---

## How we work bugs

- One bug at a time, in priority order (B2 → B5 → B3 → B7).
- For each: confirm root cause via NetDiag log capture if possible →
  agree the approach → implement on `claude/blissful-tesla-9nefa` as
  its own commit with risk table → update status.
- The presence-lobby cluster (B1, B4, B6) is the locked-design area
  and lives in `../PresenceSystem/BUGS.md` — read
  `ARCHITECTURE.md` and `../PresenceSystem/ARCHITECTURE.md` before
  touching `HostConnectionService` / `PresenceLobbyService` / invite
  services. Do not reintroduce LAZY session creation.
