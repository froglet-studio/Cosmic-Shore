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

## B8 — Host-side phantom-rejoin loop after client leaves party (stale `joined_party` presence property) 🔴

**Symptom (observed in MPPM Session 1, Phase A.1, 2026-06-01).** On a
2-VP MPPM run: VP-B (Ys2) accepts VP-A's (Ys1) invite, joins the party,
flies in the lava-lamp, then presses Leave. The client (VP-B) leaves
cleanly and returns to its own solo Menu_Main. **On the host (VP-A)
the following log cycle repeats indefinitely at refresh-tick cadence
(~3 s):**

```
[SoapPartyEventBus] RaisePartyMemberLeft → Ys2 (<id>)
[PartyMemberService] Member left: Ys2 (<id>)
[SoapPartyEventBus] RaisePartyMemberJoined → Ys2 (<id>)
[INVITE-SEND] Presence scan detected joined member 'Ys2' (<id>)
[LobbyPropertyWriter] Save failed (SessionException: Index was out of range ...) — retry 1/3 in 2000ms
[SoapPartyEventBus] RaisePartyMemberJoined → Ys2 (<id>)
[INVITE-SEND] Presence scan detected joined member 'Ys2' (<id>)
```

UI manifestation on host: Ys2's profile icon appears in the `+` party
slot, disappears, reappears — flickering forever. UI on client: no log
spam, no icon flicker, no host's profile in their `+` slot (client is
fully disengaged).

**Root cause (code paths verified, hypothesis level: high).** The
`joined_party` presence-lobby property on the client is not reliably
cleared when the client leaves the party. The host's
`HostConnectionService.ScanPresenceForJoinedPartyMembers`
(`HCS:1303-1333`) reads each presence-lobby player's `JOINED_PARTY_KEY`
property; if it matches the host's current party session ID and the
player is not yet in `PartyMembers`, it adds them and raises
`OnPartyMemberJoined`. Meanwhile `PartyMemberService.SyncFromSession`
(`PartyMemberService.cs:106-146`) iterates the *party* session's
players and removes anyone from `PartyMembers` who is no longer in the
session. **The two scans disagree, every tick, forever:**

1. `SyncFromSession` removes Ys2 (correctly — they really did leave the
   party session) → `RaisePartyMemberLeft`.
2. `ScanPresenceForJoinedPartyMembers` then sees Ys2 in the presence
   lobby with `joined_party == hostSessionId` and re-adds them →
   `RaisePartyMemberJoined`.
3. Next refresh tick, repeat.

**Why is `joined_party` stale?** The client's leave path
(`HostConnectionService.LeavePartyAsync` at `HCS:684-708`) is:
```csharp
ClearJoinedPartyAsync().Forget();
await controller.LeavePartyAndReturnToMenuAsync();
```
`ClearJoinedPartyAsync` (`HCS:1595-1604`) is a `UniTaskVoid`
fire-and-forget that calls
`_propertyWriter.WriteAsync(... SetProperty(JOINED_PARTY_KEY, ""))`.
**Three suspected contributors** (any one is sufficient):

- **a) Race with leave teardown.** The fire-and-forget property write
  is launched, then `LeavePartyAndReturnToMenuAsync` immediately begins
  Netcode shutdown + session leave + scene reload. The write may not
  reach the server before the lobby reference is disrupted.
- **b) `LobbyPropertyWriter` retry exhausts on B1 stale-index churn.**
  `SaveWithRetryAsync` filters on "Too Many Requests" / "Index was out
  of range" and retries 3× with 2 s backoff. The cycle logs *include*
  a `Save failed (Index was out of range)` retry — confirming the
  write path is hitting B1. If three retries fail, the exception
  propagates *out* of the fire-and-forget (where it's logged but
  cannot be acted on by the now-departed leave flow).
- **c) Host cache not refreshing the cleared property.** Even if the
  client's write succeeds server-side, the host's
  `_lobbyService.ActiveLobby` cache must refresh to see the update. If
  the host's refresh is hitting B1/B6 read-path churn (now silenced by
  `IsBenignSdkStaleIndexError` — silenced *but still failing* on the
  inside), the cached lobby data may stay stale indefinitely.

**Functional impact.**
- Host UI flickers: `+` slot shows/hides Ys2 every ~3 s.
- Cascading B1 write retries (`partyCount` / `partyMax` updates after
  the phantom join trigger the SDK stale-index churn on the write
  path).
- Console spam — every cycle adds ~7 lines on the host. The
  user-facing `RaisePartyMemberJoined` / `RaisePartyMemberLeft` SOAP
  events fire indefinitely, which any listener (UI badges, audio cues,
  notifications) will react to forever.
- **Not crashing, not blocking other testing**, but the host's view of
  party membership is fundamentally unreliable after any party leave.

**Reproduction.** 2-VP MPPM. VP-A invites VP-B → VP-B accepts → VP-B
leaves. Observe VP-A's console: the cycle starts within one refresh
tick of the leave and continues until something resets the host's
presence lobby (returning to authentication, restarting Play, etc.).

**Evidence (file:line).**
- Cycle producers:
  - `HostConnectionService.cs:1303-1333` — `ScanPresenceForJoinedPartyMembers`
    (re-adds based on stale `JOINED_PARTY_KEY`).
  - `PartyMemberService.cs:106-146` — `SyncFromSession` (removes based
    on authoritative party session).
- The clear path that should have prevented this:
  - `HostConnectionService.cs:705` — `ClearJoinedPartyAsync().Forget()`
    in `LeavePartyAsync`.
  - `HostConnectionService.cs:1595-1604` — `ClearJoinedPartyAsync`
    implementation (fire-and-forget property write).
- The write path that retries on B1:
  - `LobbyPropertyWriter.cs:158-181` — `when ("Index was out of range")`
    retry filter.
- Property semantics:
  - `HostConnectionService.cs:653` — `PublishJoinedPartyAsync(realSessionId)`
    called on accept.
  - `HostConnectionService.cs:1310-1316` — read predicate
    (`joinedProp.Value == sessionId`).

**Fix paths under consideration (no commit yet — discussion stage).**

1. **Defensive host scan (preferred — simplest, most robust).** In
   `ScanPresenceForJoinedPartyMembers`, before raising
   `OnPartyMemberJoined`, cross-check that `p.Id` is also present in
   `_partySessionService.ActiveSession.Players`. If not in both, skip.
   The party session is the authoritative source of truth for party
   membership; the presence lobby is a discovery signal that should
   never override the session. Pro: 2-3 lines, no race window, no
   timing dependency, robust against any presence-property staleness
   regardless of cause. Con: leaves the stale property in lobby data
   (cosmetically wrong but functionally irrelevant once the host stops
   trusting it).
2. **Await `ClearJoinedPartyAsync` in `LeavePartyAsync`.** Change the
   `Forget()` to `await` and change the return type to `UniTask`. Pro:
   guarantees the property write completes before leave teardown
   begins. Con: introduces a leave-time delay (the write + refresh is
   1-3 s in good conditions); blocks the leave if the write fails on
   B1 (current behavior is "leave anyway"); doesn't help if the host
   cache fails to refresh.
3. **Both 1 + 2.** Belt + suspenders. Cost: small.
4. **Host raises a leave from the party session as the authoritative
   trigger.** Already does this via the `PlayerLeaving` event (Commit
   17). This is the path that fires `SyncFromSession` removal. The
   bug is that the *scanner* keeps adding back. So fix 4 is "rely on
   the PlayerLeaving event and disable the presence-scan add-back for
   any player who recently raised PlayerLeaving" — but this requires
   adding event-history bookkeeping. More complex than 1.

**Status.** 🔴 Open — analysis complete, fix not yet implemented.
Awaiting user decision on fix path before any code change.

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
