# Party System — Open Bugs (test pass, 3 VPs)

Living tracker for issues found in MPPM testing with 3 virtual players (VP1/VP2/VP3).
Companion to `PARTY_SYSTEM_REFACTOR.md` (locked design) and `THREADING.md`. We work
these **one at a time**; each entry records the current hypothesis, not a committed fix.
Statuses: 🔴 open · 🟡 investigating · 🟢 fixed (commit) · ⚪ deferred.

**Tackle order:** B1 → B2 → B3 → B5 → B4 → B6 (crashes/cleanup first; the racy
presence-lobby cluster last, after diagnostics + retests). B1 is the agreed first target.

| ID | Title | Group | Confidence | Status |
|----|-------|-------|-----------|--------|
| B1 | `ArgumentOutOfRangeException` (LobbyPatcher) spam at game start | SDK-internal | High (cause) | 🔴 |
| B2 | `ObjectDisposedException` (semaphore) on Play-Mode abort / fast invite-accept | Teardown race | ~95% | 🔴 |
| B3 | TC4 bounce leaves 2 vessels + dead controls | Bounce cleanup | ~85% | 🔴 |
| B4 | TC1 second invite not delivered + party members vanish from 3rd player's panel | Presence-lobby race | High, needs retest | 🔴 |
| B5 | TC2/TC4 second joiner fails to join | Multi-client join | Uncertain | 🔴 |
| B6 | TC3 NRE (`WrappedLobbyService`) + empty online/request lists | SDK-internal | Medium | 🔴 |

---

## B1 — `ArgumentOutOfRangeException` in `LobbyPatcher.ApplyPatchesToLobby` at game start  🔴

**Symptom.** Every client logs, at game start, an `ArgumentOutOfRangeException` from
`LobbyPatcher.ApplyPatchesToLobby` → `LobbyHandler.OnLobbyChanged` → `LobbyChannel.ProcessEvent`/`HandleLobbyChanges`.

**Root cause (high confidence).** The UGS Lobby SDK applies a WebSocket "lobby changed"
delta that references a player/data index not present in the local cache (stale index).
The exception is thrown **and logged by the SDK itself** (`Unity.Services.Multiplayer.Logger.LogException`,
inside `LobbyChannel.HandleLobbyChanges`) on the SDK's own async event task — **before any
of our `await`s**. Therefore our `IsBenignLobbyPatcherError` classifier
(`HostConnectionService.cs:1852`, used only in the catch blocks at `:1023` and `:1297`,
which wrap *our* `RefreshAsync` calls) **cannot** see or suppress this particular log. It is
already known-benign and self-correcting; the problem is purely console noise we cannot
`try/catch`.

**Why "at game start".** Multiple clients join the presence lobby near-simultaneously and
write player properties rapidly, so the SDK receives bursts of deltas that race its local
cache. Our `LobbyPropertyWriter.SaveWithRetryAsync` also does a post-save
`lobby.RefreshAsync()` (`LobbyPropertyWriter.cs:147-153`) to reduce stale deltas — which may
add to the churn.

**Candidate approaches (decide together):**
1. **Tightly-scoped global log filter.** No global filter exists today (`CSDebug` only gates
   *our* calls). Add a small Bootstrap-time handler (`Application.logMessageReceived` or a
   wrapping `Debug.unityLogger.logHandler`) that drops **only** the exact benign signature
   (`ArgumentOutOfRangeException` whose stack contains `LobbyPatcher`). Reversible, isolated,
   doesn't touch the party handshake. Risk: must scope precisely so real errors aren't hidden.
2. **Reduce startup write churn.** Coalesce/throttle the player-property writes and avoid
   redundant rejoins/convergence at startup so the SDK sees fewer racing deltas. Reduces
   frequency, won't eliminate; riskier (touches fragile lobby code).

**Confirmed (user).** Fires **once** per game start, observed in the **Editor** (no build
made yet). A single, self-correcting occurrence reinforces that it's benign console noise, so
option 1 (a tightly-scoped log filter) is the low-risk fit; gating it to Editor/Development is
reasonable since release behavior is still untested.

**Evidence.** `HostConnectionService.cs:1852` (`IsBenignLobbyPatcherError`), `:1023`, `:1297`;
`LobbyPropertyWriter.cs:147-153`; `CSDebug.cs` (gates our calls only); SDK stack in the report.

---

## B2 — `ObjectDisposedException`: "The semaphore has been disposed" 🔴

**Symptom.** `HostConnectionService.RefreshAsync()` at `:1052` → `SemaphoreSlim.Release()` →
"The semaphore has been disposed." Triggered by stopping Play Mode (Editor abort) while
players are partied/roaming, or by clicking invite / accepting too fast.

**Root cause (~95%).** `RefreshAsync` acquires `_lobbyMutex` (`WaitAsync(0)` at `:927`),
then awaits `PublishPartyStateIfChangedAsync` (`:1592`) → `SaveWithRetryAsync`
(`LobbyPropertyWriter.cs:147`, a UGS save that can complete frames later). Meanwhile
`OnDestroy` runs and **disposes** `_propertyWriter.LobbyMutex` / `SessionCreationMutex`
(~`:374-375`). The late save completes, the `finally` calls `_lobbyMutex.Release()` on the
disposed semaphore → throw. No `CancellationToken` aborts in-flight refreshes before
disposal; no `_destroyed` guard on the `Release`; `OnDestroy` disposes *before* its
`await _lobbyService.LeaveAsync()` settles.

**Candidate approaches.** Don't dispose the semaphores at all (`SemaphoreSlim.Dispose` is
only required if `AvailableWaitHandle` was used — confirm it isn't); **or** add a `_destroyed`
flag + cancellation that stops `Update`→`RefreshAsync` re-entry and guards the `finally`
`Release`; **or** reorder `OnDestroy` so disposal happens only after in-flight work is
cancelled/awaited. Pick the simplest that fully closes the race.

**Evidence.** `HostConnectionService.cs:927, 1052, ~340-385` (OnDestroy), `:1592`;
`LobbyPropertyWriter.cs:141-171`.

---

## B3 — TC4: bounce leaves two vessels + broken control toggle 🔴

**Symptom.** VP1 has VP3 partied (ok). VP1 invites VP2; VP2 accepts but can't connect and
bounces to its own solo lava-lamp. There, **two vessels** spiral on autopilot (no target
crystal); tapping to take control yields a vessel that **stays in AI mode / won't steer**.

**Root cause — two vessels (~85%).** `PartyInviteController.RecoverFromFailedTransitionAsync`
(~`:410-437`) calls `LeavePartyKeepHostAsync` + reloads `Menu_Main` but does **not** call
`gameData.DestroyPlayerAndVessel()` — unlike its sibling `LeavePartyAndReturnToMenuAsync`
(~`:292`). Menu vessels spawn with `DestroyVesselWithScene = false`
(`MenuServerPlayerVesselInitializer.cs:40`), so a vessel from the failed session survives the
solo-host restart while `Menu_Main` reload spawns a fresh one → two vessels.

**Root cause — dead controls (medium).** Likely coupled to the duplicate: the toggle may act
on a stale `LocalPlayer`/vessel, or `MenuCrystalClickHandler` state (`_isInFreestyle`,
`_isTransitioning`, `_cts`) isn't reset after the reload, and/or
`MainMenuController.ActivateLocalPlayerAutopilot` (~`:249-267`, sets AI on + input paused)
races the toggle. Needs confirmation.

**Candidate approach.** Mirror the working `LeavePartyAndReturnToMenuAsync` cleanup in the
bounce path (explicit despawn/`DestroyPlayerAndVessel` before solo restart); verify toggle
state resets on the reload. Possibly add brief diagnostics to confirm which vessel the toggle
targets.

**Evidence.** `PartyInviteController.cs:~410-437` vs `~267-332` (`:292`);
`MenuServerPlayerVesselInitializer.cs:40`; `MenuCrystalClickHandler.cs` (toggle + OnEnable);
`MainMenuController.cs:~198-267`.

---

## B4 — TC1: second invite not delivered + party members vanish from 3rd player's online panel 🔴

**Symptom.** VP1 invites VP3 → accept → ok (party of 2). VP1 then invites VP2 → **VP2 never
gets the invite**, and VP1/VP3's rows (shown "In Lobby 2/4") **vanish from VP2's online panel**.

**Root-cause hypotheses (high, pending retest).**
- Once a party forms (`PartyMembers.Count > 1`), convergence is **paused**
  (`HostConnectionService.cs:~945-958`), which can **freeze a presence-lobby split** so VP2
  ends up on a different lobby than VP1/VP3.
- `RefreshOnlinePlayersDiff` (~`:1150-1196`) **removes** any player not in the local presence
  lobby → VP1/VP3 drop from VP2's `OnlinePlayers`.
- On any lobby rejoin, `BuildLocalPlayerProperties` (`PresenceLobbyService.cs:~335-350`)
  **resets `invite_payloads` to empty** (documented in a code comment), wiping VP1's
  outgoing invite to VP2 before VP2 reads it.

**Open question (user to retest, after B1).** Do VP1/VP3 rows **come back on their own**
(transient split) or **stay gone** (frozen split)? Determines whether the fix targets
convergence-pause or the diff/property-reset.

**Constraint.** This is the fragile, locked-design area — **read `PARTY_SYSTEM_REFACTOR.md`
before touching** `HostConnectionService` / `PresenceLobbyService` / invite services. Likely
wants diagnostics first.

**Evidence.** `HostConnectionService.cs:~945-958, ~964-970, ~1150-1196`;
`PresenceLobbyService.cs:~204-239 (converge), ~335-350 (property reset)`.

---

## B5 — TC2/TC4: the second joiner fails to join 🔴

**Symptom.** VP1 invites VP2 and VP3. VP3 accepts first → joins ok. VP2 accepts second
(invite **was** received — confirmed) → **join fails** (→ Commit-16 watchdog bounce, hence B3).

**Notes.** Invite carries the **real** session id (eager creation, `HostConnectionService.cs:524`),
so this is **not** the `PENDING` path. Connection approval is unconditional and capacity is
fine (`MaxPartySlots` ≥ 4). So the most likely failure is **host-side**: the second late
client never reaches `OnClientReady` (host vessel-spawn / client-pull roster path), so
`PartyInviteController` times out `WaitForClientReadyAsync` and bounces.

**Approach.** Diagnose first — structured logging on the host's spawn/roster path for a
second joiner and on the joiner's `AcceptInviteAsync` awaits (`WaitForClientConnectionAsync`
/ `WaitForClientReadyAsync`), then a focused retest to find where VP2 stalls.

**Evidence.** `PartyInviteController.cs` (AcceptInviteAsync awaits); Commit-16 roster-pull in
`ClientPlayerVesselInitializer` / `ServerPlayerVesselInitializer`; `MultiplayerSetup.cs`
(approval).

---

## B6 — TC3: `NullReferenceException` (`WrappedLobbyService.GetLobbyAsync`) + empty online/request lists 🔴

**Symptom.** A variant of TC3: VP2 logs a UGS `NullReferenceException` from
`WrappedLobbyService.TryCatchRequest`/`GetLobbyAsync` during `LobbyChannel.ProcessEvent`, and
VP2's online list **and** request list both go empty.

**Root-cause hypothesis (medium).** Same family as B1 — SDK-internal, logged by the SDK
before our catch — triggered when a lobby subscription event fires against a stale/torn-down
lobby reference (premature `LeaveAsync`/`ForceReset` during the accept handshake). The
empty-lists symptom is likely our `OnlinePlayers`/requests going stale when `ActiveLobby`
becomes null and refresh early-returns.

**Approach.** Treat the NRE as trigger-reduction (don't leave/rejoin mid-event; guard
against stale refs). Investigate the empty-lists recovery separately (does the UI repopulate
after the next successful refresh?). Likely bundle with B4 diagnostics.

**Evidence.** SDK stack (`WrappedLobbyService.cs:165/462`, `LobbyChannel.cs:197`);
our lobby leave/`ForceReset`/refresh-early-return paths in `HostConnectionService` /
`PresenceLobbyService`.

---

## How we'll work
- One bug at a time, in tackle order. For each: confirm root cause (logs/retest if needed) →
  agree the approach → implement on `claude/blissful-tesla-9nefa` as its own commit → update
  this tracker's status.
- The presence-lobby cluster (B4/B6) is the locked-design area: re-read
  `PARTY_SYSTEM_REFACTOR.md` first, prefer diagnostics before edits, do not reintroduce LAZY
  session creation.
