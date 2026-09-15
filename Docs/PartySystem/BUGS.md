# Party System — Open Bugs

Living tracker for party-side issues found in MPPM testing. Companion
to `ARCHITECTURE.md` (locked design), `REFACTOR.md` (active refactor
queue), and `../NetworkDiagnostics/ARCHITECTURE.md` (catch-block diagnostics).

Presence-lobby-specific bugs (B1, B4, B6 from the old tracker) moved
to `../PresenceSystem/BUGS.md`.

Statuses: 🔴 open · 🟡 investigating · 🟢 fixed (commit) · ⚪ deferred.

| ID | Title | Confidence | Status |
|----|-------|-----------|--------|
| B2 | `ObjectDisposedException` (semaphore) on Play-Mode abort / fast invite-accept | Root-caused & fixed | 🟢 |
| B3 | TC4 bounce leaves 2 vessels + dead controls | Fixed-by-construction | 🟢 |
| B5 | TC2/TC4 second joiner fails to join | Every named cause fixed; repro is stale | 🟡 |
| B7 | Client pair-init runs before remote identity replicates (`InitializePair Player=` empty, vessel-type `Random`) | Verified mostly benign | ⚪ |
| B8 | Host-side phantom-rejoin loop after a client leaves (stale `joined_party`) | Fixed & MPPM-verified | 🟢 |
| B9 | Host-return: one client's vessel stuck in autopilot drift + party domains not reset to menu (Jade) | Root-caused & fixed | 🟢 |
| B10 | Host leaves/disconnects mid-party → client stuck (no bounce-to-solo + "Host disconnected") | Fixed & verified | 🟢 |
| B11 | Idle Relay allocation goes stale; every later join bounces at step 3 | Superseded by B16; recycle reverted | 🟢 |
| B12 | A host can never re-invite a guest who once accepted or declined | Fixed & live-verified | 🟢 |
| B13 | Open-lobby ClientRpc dropped on a syncing / late-joining client | Fixed & live-verified | 🟢 |
| B14 | A host whose NetworkManager was restarted in-process cannot get a new guest through synchronization | Root cause = B16; fixed & live-verified | 🟢 |
| B15 | Nobody is ever shown "in game"; friends list only moves on return to menu | Fixed; live retest pending | 🟢 |
| B16 | Un-spawned fauna NetworkObjects break synchronization for every guest | Root cause; fixed & live-verified | 🟢 |
| B17 | Boot parks forever on a blank Authentication panel when the sign-in loses the race to the splash timer | Root-caused & fixed | 🟢 |
| B18 | A client cannot leave a match at all, and cannot leave a Maelstrom tournament until it ends | Root-caused & fixed | 🟡 |
| B19 | Nothing watches a client's scene transition, so a lost one is a permanent black screen | Root-caused & fixed | 🟡 |
| B20 | A player leaving mid-wait strands the whole party at the ready screen, forever (match AND lobby gate) | Root-caused & fixed | 🟡 |
| B21 | A pilot who leaves mid-match takes their ship AND their score out of the arena | Root-caused & fixed | 🟡 |
| B22 | The Scoreboard's client exit and rematch caption were never wired (`{fileID: 0}`), so B18's propagated fix was a no-op here | Root-caused & fixed | 🟡 |
| B23 | The arcade card lobby does not follow the host: a flying guest is never pulled in, a guest who missed it once never gets it, a host who changes card cannot move the party, and guests draw phantom AI | Root-caused & fixed | 🟡 |

*(The table used to list only seven of these. B8 and B11–B16 had entries below
but no index row, so the index read as "seven bugs, two of them red" while the
file held sixteen — which is how B2 and B11 stayed red in the index long after
the code moved. Keep the row and the entry in step.)*

---

## B2 — `ObjectDisposedException`: "The semaphore has been disposed" 🟢 (fixed 2026-08-20; closed on a source audit 2026-09-11)

**Closed.** The first of the entry's three candidate approaches was taken and is in
the shipped code: `HostConnectionService.OnDestroy` no longer disposes either
semaphore. The block says so where it used to dispose them
(`HostConnectionService.cs`, immediately after the `_lobbyService.LeaveAsync()`
await), and it cites the same evidence this entry does — the crash-detector journal
of 2026-08-20.

The reasoning that made it safe is the one this entry flagged for confirmation, now
confirmed by audit: `SemaphoreSlim.Dispose()` is only *required* when
`AvailableWaitHandle` has been read, because that is what allocates the OS handle.
The string `AvailableWaitHandle` appears nowhere in `Assets/_Scripts` except in that
explanatory comment, and no `.Dispose()` call on `LobbyMutex` or
`SessionCreationMutex` survives anywhere in the tree. Both semaphores are async
mutexes over main-thread continuations only, so letting the GC collect them leaks
nothing.

Note what that fix does and does not cover. It removes the DISPOSAL, so the race
itself — an in-flight `SaveWithRetryAsync` completing after `OnDestroy` and hitting
`_lobbyMutex.Release()` — is now harmless rather than prevented: the late `Release`
runs against a live semaphore on an object that is going away. That is the correct
trade for teardown. **A future `ObjectDisposedException` on this path is therefore a
DIFFERENT bug** (a `CancellationTokenSource` most likely — `PartyInviteController`
disposes four) and should be filed as one rather than reopening this.

*Historical record of the original diagnosis follows.*

### Original entry

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

## B3 — TC4: bounce leaves two vessels + broken control toggle 🟢 (fixed-by-construction)

**Symptom (historical).** VP1 has VP3 partied (ok). VP1 invites VP2; VP2
accepts but can't connect and bounces to its own solo lava-lamp. There,
**two vessels** spiral on autopilot (no target crystal); tapping to take
control yields a vessel that **stays in AI mode / won't steer**.

**Original root cause — two vessels.** The bounce path
`PartyInviteController.RecoverFromFailedTransitionAsync` reloaded
`Menu_Main` but did **not** despawn the failed-session vessel before the
solo-host restart — unlike its sibling `LeavePartyAndReturnToMenuAsync`.
Menu vessels spawn `DestroyVesselWithScene = false`
(`MenuServerPlayerVesselInitializer.cs`), so the leftover vessel survived
the restart while the reload spawned a fresh one → two vessels (the
dead-control half was the orphan/half-paired vessel, same mechanism as
B3.b).

**Status. 🟢 Fixed-by-construction.** The same architectural refactor that
fixed the clean-leave path (B3.b, commit `74cde70`) also rewrote the
bounce path: `RecoverFromFailedTransitionAsync` now mirrors
`LeavePartyAndReturnToMenuAsync` **exactly** — `gameData.DestroyPlayerAndVessel()`
→ `gameData.ResetRuntimeData()` → `HostConnectionService.LeavePartySessionAsync()`
→ `NetworkTransitionService.ShutdownAsync` + `ClearStaleReferences()` →
`SceneManager.LoadSceneAsync(_sceneNames.MainMenuScene, Single)` →
`HostConnectionService.EnsurePartySessionAsync()`. The scene is fresh
before the solo session recreates, so the post-reload initializer spawns
exactly one paired vessel — no orphan, controls wire normally. The
deleted `LeavePartyKeepHostAsync` is gone; the hardcoded `"Menu_Main"`
literal is replaced by `_sceneNames.MainMenuScene`.

**Verification note.** The clean-leave path (B3.b) was MPPM-verified
2026-06-02. The bounce path shares the **identical** decomposed sequence,
so it is fixed-by-construction; the dedicated TC4 bounce repro
(Phase C.2 in `MPPM_SESSION_LOG.md`) is the only outstanding confirmation.

**Diagnostic.** The bounce path emits a NetDiag log in
`RecoverFromFailedTransitionAsync` — classifies the trigger if it ever
recurs.

**Evidence.** `PartyInviteController.cs` `RecoverFromFailedTransitionAsync`
(currently ~`:424-455`) vs `LeavePartyAndReturnToMenuAsync` (~`:275-330`)
— both call `DestroyPlayerAndVessel()` + `LeavePartySessionAsync()` +
`LoadSceneAsync(_sceneNames.MainMenuScene)`; `MenuServerPlayerVesselInitializer.cs`
(`DestroyVesselWithScene = false`).

### B3.b — same symptom on the CLEAN-LEAVE path (MPPM Session 1, 2026-06-02)

**This is a distinct variant of B3.** The original B3 (above) is the
*bounce* path (`RecoverFromFailedTransitionAsync` after a failed join).
B3.b reproduces the **identical symptom — two vessels + a vessel that
won't steer and whose AI no longer seeks crystals — on the deliberate
Leave Party button**, which goes through
`LeavePartyAndReturnToMenuAsync`, the path B3 calls "the working one".
So the original B3 root cause (missing `DestroyPlayerAndVessel` in the
bounce path) does **not** explain B3.b — the leave path *does* call
`gameData.DestroyPlayerAndVessel()` (`PartyInviteController.cs:299`).

**Reproduction (2-VP, clean happy path until leave).** VP-A (Ys1, host)
invites VP-B (Ys2, client). VP-B accepts, both fly, all good. **VP-B
presses Leave Party.** VP-B returns to its own solo host session but
ends with **two vessels** (NetObjId 4 and 6 both re-spawn) and one
`Player`; the local vessel cannot be controlled and its AI wanders
without seeking crystals. Host (VP-A) side is clean. No log repetition
on the client (unlike B8).

**Log trace (client / VP-B, sequential key lines).**
```
[SoapPartyEventBus] RaiseInviteResolved                          ← LeavePartyAsync entry (HCS:705, benign UI clear — NOT the bug)
[SoapPartyEventBus] RaisePartyMemberLeft → Ys2
[PartyMemberService] Party members cleared with Left events.
[LobbyPropertyWriter] ClearJoinedParty error (SessionException: [Error: Unknown] Object reference not set ...)   ← B8 fix-2 clear write hits B1/B6 SDK NRE
[PartyInviteController] Starting leave-lobby flow...
[PartySessionService] Clearing session reference xifykg7...
[VESSEL] OnNetworkDespawn 'Squirrel(Clone)' NetObjId=6  IsOwner=True   ← client's own vessel despawns
[VESSEL] OnNetworkDespawn 'Squirrel(Clone)' NetObjId=4  IsOwner=False  ← host's replicated vessel despawns
[MultiplayerSetup] Disconnected from host. Returning to menu
[SceneLoader] HandleActiveSessionEnd deferring to server — IsListening=True, IsServer=False, IsClient=True
[VESSEL] OnDestroy NetObjId=6
[VESSEL] OnDestroy NetObjId=4
[PartySessionService] Left party session xifykg7...
[PartyStateMachine] InParty → HostingParty
[NetworkTransitionService] NetworkManager not running — skipping shutdown.
... (solo restart) ...
[VESSEL] OnNetworkSpawn 'Squirrel(Clone)' NetObjId=4  IsServer=True IsOwner=True   ← FIRST respawn
[FLOW-6] [ClientVesselInit] Raising OnClientReady (local player initialized)
[FLOW-5] [ServerVesselInit] FindUnprocessedPlayerByOwnerClientId(0) returned NULL  ← ⚠ spawn-chain desync
[HostConnectionService] Solo party session ready: TR8irv... — InParty, vessel will spawn
[PartyInviteController] Leave-lobby flow completed.
[CellRuntimeDataSO] Runtime data reset complete
Container Menu_Main (-4960322) disposed
NullReferenceException: MainMenuCameraController.StartRandomSwitchLoopIfEnabled (MainMenuCameraController.cs:647) ← via OnValidate during teardown
Scene (Menu_Main) Bindings Installed
[VESSEL] OnNetworkSpawn 'Squirrel(Clone)' NetObjId=6  IsServer=True IsOwner=True   ← SECOND respawn = two vessels
[FLOW-6] [ClientVesselInit] AddPlayer done. Players.Count=1, LocalPlayer=Ys1
[FLOW-6] [ClientVesselInit] Raising OnClientReady
[FLOW-8] [SceneLoader] FadeFromSplashOnReady — OnClientReady fired!
[FLOW-5] [ServerVesselInit] FindUnprocessedPlayerByOwnerClientId(0) returned NULL  ← ⚠ again
UnknownContractException: Cannot resolve contract 'CosmicShore.Utility.GameDataSO'  ← AOEExplosion DI inject fails post-teardown
```

**Root-cause hypotheses (ordered by confidence).**

1. **Two-respawn / scene-reload race (high).** The vessel spawns
   **twice** (NetObjId 4 then 6, both `IsServer=True IsOwner=True`). The
   sequence shows a vessel respawn (NetObjId 4) and an `OnClientReady`
   *before* `Container Menu_Main disposed` + `Scene (Menu_Main) Bindings
   Installed`, then a *second* respawn (NetObjId 6) + second
   `OnClientReady` after. So the solo host's `EnsurePartySessionAsync`
   spawn-chain fires a vessel **before** the `Menu_Main` network scene
   reload completes, and the scene reload spawns **another**. The first
   vessel (NetObjId 4) is a scene-survivor (menu vessels are
   `DestroyVesselWithScene=false`, `MenuServerPlayerVesselInitializer.cs:40`)
   that is *not* cleaned up by the reload — same root mechanism as the
   original B3, but reached via the ordering of solo-restart-vs-reload
   on the leave path rather than the missing-cleanup-call of the bounce
   path.

2. **`FindUnprocessedPlayerByOwnerClientId(0) returned NULL` →
   spawn-chain desync (high).** Fires twice. The host's
   `ServerPlayerVesselInitializer.HandlePlayerNetworkSpawnedAsync`
   (`:188-193`) early-returns when it can't find an unprocessed Player
   for clientId 0 (the host's own id after solo restart). The Player
   list was cleared by `ResetRuntimeData()` and the persistent Player
   NetworkObject's `OnNetworkSpawn` doesn't re-fire (it survives scene
   loads, `DestroyWithScene=false`), so `ProcessPreExistingPlayers` /
   the `ConnectedClients` re-trigger path (`:130-145`) is supposed to
   re-kick it — but the NULL return means the pairing between the
   respawned vessel (NetObjId 4/6) and the Player did not complete.
   **A vessel with no completed player-pairing = no input controller
   wired = "wanders on AI, won't steer"** — this is almost certainly the
   dead-control half of the symptom.

3. **`MainMenuController.ActivateLocalPlayerAutopilot` races the
   double-spawn (medium).** `HandleMenuReady` (`MainMenuController.cs:198`)
   → `ActivateLocalPlayerAutopilot` (`:249`) fires on `OnClientReady`.
   `OnClientReady` fires **twice** in the trace (once per respawn). The
   autopilot/input state is set against whichever `LocalPlayer` is
   current at each fire; with two vessels and a NULL-paired one, the
   toggle/input ends up bound to the wrong or a half-initialized vessel.

**Secondary errors in the same trace (likely consequences, not causes).**
- `MainMenuCameraController.StartRandomSwitchLoopIfEnabled` NRE
  (`MainMenuCameraController.cs:647`) — fires via `OnValidate` during
  `Container Menu_Main disposed`. Editor-time validation running against
  a half-disposed scene; probably benign teardown noise but worth a
  null-guard.
- `UnknownContractException: GameDataSO` on `AOEExplosion.gameData`
  inject (`ExplosionHelper.cs:79` via a crystal-impact RPC) — a vessel
  from the *old* session is still alive and processing a crystal-impact
  `ClientRpc` after its DI `Container Menu_Main` was disposed, so the
  recursive injector can't resolve `GameDataSO`. **This is direct
  evidence of hypothesis 1** — a leftover vessel from the pre-leave
  session is still live and interacting with crystals after teardown.

**Distinction from B8.** B8 was host-side, fixed by the session
cross-check + awaited clear. B3.b is **client-side**, on the leaving
client's own solo restart, and is about vessel/scene lifecycle ordering
— a different subsystem. The `ClearJoinedParty error` line in the trace
is the B8 fix-2 write hitting the B1/B6 SDK NRE (expected; non-fatal —
the await is bounded and the host ignores the property via B8 fix-1).

**Candidate approaches (discussion stage — NO code yet).**

- **A. Order solo-restart AFTER the scene reload settles.** The leave
  flow currently does `LeavePartyKeepHostAsync` (which calls
  `EnsurePartySessionAsync` → fresh solo Relay + spawn) *then*
  `LoadScene(Menu_Main)`. If the spawn chain is kicked before the scene
  reload completes, we get the double-spawn. Investigate gating the
  solo-session vessel spawn on scene-load completion (mirror how the
  initial Menu_Main load sequences it).
- **B. Explicit despawn of scene-surviving vessels before the reload.**
  The original B3 fix idea (explicit despawn before solo restart)
  applied here too — ensure NetObjId 4 (the scene-survivor) is despawned
  so only the reload's fresh vessel exists.
- **C. Fix the `FindUnprocessedPlayerByOwnerClientId` NULL on solo
  restart.** Ensure the persistent host Player is re-registered into
  `gameData.Players` and re-processed after `ResetRuntimeData()` clears
  it, so the respawned vessel completes its player-pairing (restores
  input control). This likely fixes the dead-control half independently
  of the duplicate.
- **D. All three** — they target different facets (duplicate, lifecycle
  ordering, pairing). Likely need B+C at minimum.

**Status.** 🟢 Fixed via architectural refactor — **clean-leave path
MPPM-verified 2026-06-02 (commit `74cde70`).** User confirmed: after
VP-B leaves the party, exactly ONE vessel in solo Menu_Main, controllable,
AI seeks crystals; no `[PLAYER] OnNetVesselIdChanged prev=N, new=M`
during the leave flow (the bug's signature); no `UnknownContractException:
GameDataSO`. Cold-boot smoke also clean (untouched path, no regression).
The bounce/recovery path (`RecoverFromFailedTransitionAsync`) shares the
**identical** decomposed sequence, so it is fixed-by-construction — but it
has not been independently exercised yet; its dedicated repro is Phase C.2
(B3 original / TC4 bounce) in `MPPM_SESSION_LOG.md`.

**Note on the fix path.** An earlier band-aid (commit `3e0c5bc`) added a
`gameData.LocalPlayer.Vessel.DestroyVessel()` call between
`LeavePartyKeepHostAsync` and `LoadScene("Menu_Main")` to despawn one of
the two vessels. **That band-aid has been reverted** and replaced with
the architectural fix described below — the user (correctly) rejected
the band-aid as a smelly temp fix.

**Architectural fix (active).** The leave flow's two methods —
`PartyInviteController.LeavePartyAndReturnToMenuAsync` and
`RecoverFromFailedTransitionAsync` — were rewritten to sequence the
Menu_Main scene reload **before** the solo Relay session is recreated,
mirroring cold-boot exactly: tear down vessel/Player/SOAP refs → leave
UGS session (new bare-leave primitive `HostConnectionService.LeavePartySessionAsync`)
→ shut down NM via `NetworkTransitionService.ShutdownAsync` → clear
stale refs → load Menu_Main locally via `UnityEngine.SceneManagement.SceneManager.LoadSceneAsync`
(NM is down, so not via Netcode's SceneManager) → recreate solo session
via `HostConnectionService.EnsurePartySessionAsync`. Because the scene
is already fresh by the time `EnsurePartySessionAsync` auto-starts NM
and the persistent host Player respawns, the scene-placed
`ServerPlayerVesselInitializer` (now also fresh) catches the Player
exactly once → one vessel, no orphan. The old combined
`LeavePartyKeepHostAsync` primitive was deleted (no callers remain). The
hardcoded `"Menu_Main"` string literals in PIC were replaced with
`SceneNameListSO.MainMenuScene` (injected). No spawn paths were touched —
`ServerPlayerVesselInitializer.SpawnVesselForPlayer` remains the single
canonical Netcode vessel spawn site.

**Root cause confirmed via a second, sequential MPPM trace
(2026-06-02).** The trace bracketed `[PartyInviteController] Starting
leave-lobby flow...` through the second `OnNetworkSpawn NetObjId=6` and
showed:

```
... LeavePartyKeepHostAsync runs ...
[FLOW-4] [Player] OnNetworkSpawn — persistent host Player (NetObjId=1)
[FLOW-5] [ServerVesselInit] HandlePlayerNetworkSpawnedAsync ownerClientId=0
[FLOW-5] Found player Ys1
[VESSEL] OnNetworkSpawn NetObjId=4           ← FIRST vessel, BEFORE the reload
[PLAYER] OnNetVesselIdChanged prev=0, new=4
[FLOW-6] OnClientReady #1
[PartySessionService] Created party session nEAQ
[HostConnectionService] Solo party session ready
[PartyInviteController] Leave-lobby flow completed.
Container Menu_Main disposed
Scene (Menu_Main) Bindings Installed         ← scene reload completes
[FLOW-5] [ServerVesselInit] OnNetworkSpawn  ← NEW scene-placed initializer instance
[FLOW-5] HandlePlayerNetworkSpawnedAsync ownerClientId=0
[FLOW-5] Found player Ys1                    ← persistent Player, found again
[VESSEL] OnNetworkSpawn NetObjId=6           ← SECOND vessel, AFTER the reload
[PLAYER] OnNetVesselIdChanged prev=4, new=6  ← Player ditches vessel 4 → 6
[FLOW-6] OnClientReady #2
```

**The mechanism (definitive):** the post-reload `ServerPlayerVesselInitializer`
is a **brand-new scene-placed instance** with an **empty `_processedPlayers`
HashSet**. The persistent host `Player` (DestroyWithScene=false) survives
the reload, so the new initializer iterates `nm.ConnectedClients`
(`ServerPlayerVesselInitializer.cs:130-145`), finds the Player still
connected and unprocessed, runs `HandlePlayerNetworkSpawned`, and spawns
a second vessel. `Player.NetVesselId` updates `4→6`. **Vessel 4 is left
alive but orphaned** — no Player pairing, AI ticks against a disposed
`Menu_Main` DI container (proven by the `UnknownContractException:
GameDataSO` on crystal-impact RPC seen in the prior trace).

**Correction to earlier hypothesis 2.** Yesterday I claimed
`FindUnprocessedPlayerByOwnerClientId(0) returned NULL` was the
"dead-controls cause." On a re-read of the trace, that NULL is benign
noise: `HandlePlayerNetworkSpawnedAsync` is invoked twice for the same
`ownerClientId` (visible in both the buggy *and* working cycles); the
second call lands after the Player was added to `_processedPlayers` by
the first, returns NULL, early-returns harmlessly. It fires every cycle,
not just buggy ones. **Hypothesis 1 (orphan vessel from spawn-vs-reload
ordering) is the sole root cause** — fixed below.

> **Superseded history (kept for the audit trail).** An earlier writeup of
> this entry described a band-aid "single commit" fix — a
> `gameData.LocalPlayer.Vessel.DestroyVessel()` call wedged between
> `LeavePartyKeepHostAsync` and `LoadScene("Menu_Main")` — as if it had
> landed. It had **not**: that band-aid (`3e0c5bc`) was reverted (see "Note
> on the fix path" above) and replaced by the decomposed architectural fix
> (`74cde70`, "Architectural fix (active)" above). The shipped code uses
> `LeavePartySessionAsync` (not the deleted `LeavePartyKeepHostAsync`) and
> `_sceneNames.MainMenuScene` (not the `"Menu_Main"` literal).

**Evidence (file:line — current code, drifts; re-grep before trusting).**
- `PartyInviteController.cs` — `LeavePartyAndReturnToMenuAsync` (~`:275-330`):
  `DestroyPlayerAndVessel()` (~`:309`) → `LeavePartySessionAsync()` (~`:315`)
  → `LoadSceneAsync(_sceneNames.MainMenuScene)` (~`:320`) →
  `EnsurePartySessionAsync()` (~`:324`).
- `PartyInviteController.cs` — `RecoverFromFailedTransitionAsync` (~`:424-455`):
  the **identical** sequence (`DestroyPlayerAndVessel()` ~`:439`,
  `LeavePartySessionAsync()` ~`:445`, `LoadSceneAsync` ~`:450`,
  `EnsurePartySessionAsync()` ~`:454`).
- `GameDataSO.DestroyPlayerAndVessel` — clears `Players`, destroys vessels.
- `ServerPlayerVesselInitializer` — `ConnectedClients` re-kick of persistent
  Players after a scene load (the post-reload single-spawn path).
- `MenuServerPlayerVesselInitializer` — `DestroyVesselWithScene = false`.
- Secondary teardown noise from the original trace (now moot): the
  `MainMenuCameraController` NRE via `OnValidate`, and the `ExplosionHelper`
  `GameDataSO` resolve failure that was the leftover-vessel evidence.

---

## B5 — TC2/TC4: the second joiner fails to join 🟡 (every named cause is fixed; the repro is stale — needs a retest, not more code)

**Status after a source audit, 2026-09-11.** This entry is still open on the
strength of a repro from before the fixes that were written to close it. Nothing
below is new code; it is a statement of what the audit could and could not
establish, so the retest starts from the right place.

**Every cause this entry names is closed in source.** Traced individually:

| Named cause | Where it stands |
|---|---|
| Premature `OnClientReady` with no local vessel, which also cancelled `RosterPullRetryLoop` | Fixed. `ClientPlayerVesselInitializer.ProcessPendingPairs` returns without completing the batch while `gameData.LocalPlayer?.Vessel == null`, keeping the flag armed and the retry loop alive. The comment names this bug. |
| `AcceptanceSignalService.ScanForSignals` returning the FIRST accepter only, so with two invites out the first accepter masked the second's signal | Fixed. It returns every accepter, and the host's scan handles them as a set. |
| A re-invite to a guest who once accepted or declined, swallowed forever | Fixed — B12, live-verified 2026-09-02. |
| A joiner whose owner-written name / vessel type land late, stranded by the spawn latch | Fixed. `ServerPlayerVesselInitializer` re-arms up to `MaxSpawnReArms` (6). |
| Host-side synchronization never completing for a guest | Fixed — B16, the root cause, live-verified 2026-09-02. |

**Two more that this entry never named, both also closed.** The `WaitForClientReadyAsync`
subscribe race (`OnClientReady` firing between the accept and the subscribe) is
closed by the `gameData.LocalPlayer?.Vessel` re-check on *both* sides of the
subscribe. And `RosterPullRetryLoop`'s budget was raised to 40 × 1500 ms = 60 s
specifically because it used to expire while the watchdog that bounces the player
was still counting — the two clocks do not start together, so the loop has to
outlive the whole connect + ready budget.

**Nothing second-joiner-specific remains on the join path.** Connection approval is
unconditional (`MultiplayerSetup.OnConnectionApprovalCallback` sets
`Approved = true` with no capacity or count test); `_processedPlayers` is keyed on
`NetworkObjectId`, so two joiners cannot collide; `HandleRosterRequest` is an
idempotent ensure-then-send per requester; and `_inviteService` is keyed by target.
The one single-slot piece of state in the area, `_lastFiredInvite`, is on the
RECIPIENT and holds one inviter — which is one per joiner, not one per party.

**So the honest reading is that this record is stale, not that the bug is fixed.**
The last observation predates all of the above *and* the MPPM tag prerequisite —
and untagged clones share one UGS `PlayerId`, which corrupts concurrent joins on
its own and is a sufficient cause of exactly this symptom (see the caveat at the
end of this entry, and Session 3 in `MPPM_SESSION_LOG.md`). An audit cannot
distinguish "fixed" from "still broken for a reason nobody has named yet", so this
stays 🟡 rather than 🟢.

**What the retest has to do** (it is H10's, and it needs MPPM or several machines):
three UNIQUELY TAGGED players, VP1 invites VP2 and VP3, accept in BOTH orders. On
the joiner, the `[FLOW-6]` `OnClientReady` raise must FOLLOW the local
`InitializePair` log, never precede it. A failure now is a NEW root cause and wants
a fresh entry with both `Player.log`s — do not re-walk the table above.

*Original diagnosis and the fix history follow.*

### Original entry

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

**Code audit (2026-07-16, invite-chain Task 4) — candidate root cause
found + fixed; MPPM retest required.** The client-pull roster request
fires from the joiner's own `ClientPlayerVesselInitializer.OnNetworkSpawn`,
so the host's `InitializeAllPlayersAndVessels_ClientRpc` reply
legitimately arrives BEFORE the host has spawned the requester's vessel
(`HandleRosterRequest` kicks the spawn chain and replies immediately;
the spawn takes preSpawnDelay 200ms + spawn + postSpawnDelay 200ms).
That reply therefore contains every pair EXCEPT the joiner's own — and
`ProcessPendingPairs` treated "no pending pairs + `_signalClientReadyWhenDone`"
as batch-complete: it raised `OnClientReady` with **no local vessel**,
set `_localPairResolved = true`, and **cancelled `RosterPullRetryLoop`**
— defeating the exact self-heal the pull loop exists for. If the host's
follow-up `NotifyClients` push is then lost or stalls, the joiner is
stranded vessel-less with the retry loop dead, and (when the premature
raise landed before `WaitForClientReadyAsync` subscribed) PIC times out
→ the B5 bounce. The window is per-join and widens under load, which
fits "second joiner" (host is busier; two admits in flight). **Fix:**
the completion branch now defers (keeps the flag armed + the retry loop
alive) until `gameData.LocalPlayer?.Vessel != null` — the local pair
must actually resolve before the client declares ready. Failure
semantics preserved: if the vessel truly never spawns, the retry loop
expires and `WaitForClientReadyAsync` times out → clean bounce.
**Retest (MPPM):** TC2 (VP1 invites VP2+VP3, accepts in both orders) and
the invite-chain S10 (VP2 invites VP3 from inside VP1's party); confirm
no premature `OnClientReady` (FLOW-6 raise must follow the local
`InitializePair` log) and no `[FLOW-5]`/roster-pull stall.

**Update (2026-09-01).** Two more second-joiner-specific holes closed, both on the
INVITE side rather than the spawn side — see B12 (a re-invite to a guest who once
accepted/declined was swallowed forever) and the `AcceptanceSignalService.ScanForSignals`
change (it returned the FIRST accepter only, so with two invites out the first accepter
masked the second's signal until the first join was corroborated or the invite expired).
The spawn-side latch also gained a bounded re-arm (`ServerPlayerVesselInitializer`,
`MaxSpawnReArms`) so a joiner whose owner-written name/vessel type lands late is not
stranded. Retest with three uniquely-tagged players, both accept orders.

**⚠ Repro validity caveat (2026-07-16).** The original TC2/TC4 sessions
predate the MPPM tag prerequisite (`TESTS.md` § "MPPM prerequisites").
With untagged clones, VP2 and VP3 shared ONE UGS PlayerId — which by
itself corrupts concurrent joins (two "players" with the same id in one
session). Retest with uniquely-tagged VPs; the audit fix above stands
on its own merits either way.

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
- **Residual risk (thin, not menu-reachable).** `GameFeedAPI` (now
  `GameToastAPI`)
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

## B8 — Host-side phantom-rejoin loop after client leaves party (stale `joined_party` presence property) 🟢 (fixed + MPPM-verified 2026-06-02)

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

**Status.** 🟢 Both fixes landed — needs MPPM re-verify (the
MemberLeft/MemberJoined cycle must stop after a client leaves).

- **Fix 1 (DONE, commit `cb65cf3`).** `ScanPresenceForJoinedPartyMembers`
  now builds a `HashSet` of the authoritative party-session player IDs
  and skips any presence-lobby player not in it
  (`if (!sessionPlayerIds.Contains(p.Id)) continue;`). The host can no
  longer be tricked into re-adding a departed client by a stale
  `joined_party` presence property. Makes the host's party-membership
  view depend solely on the authoritative session — race-free and
  robust against any presence-staleness cause.
- **Fix 2 (DONE).** `ClearJoinedPartyAsync` changed from `UniTaskVoid`
  to `UniTask`; `LeavePartyAsync` now waits for it (bounded by
  `CLEAR_JOINED_PARTY_TIMEOUT_SECONDS = 3s` via `UniTask.WhenAny` +
  `UniTask.Delay`) before starting leave teardown, so the stale
  `joined_party` property is actually removed on the wire rather than
  just ignored by the host. `WriteAsync` swallows its own exceptions
  (so the clear can only be slow, never throw); on timeout the leave
  proceeds anyway — Fix 1 already protects the host, so a slow/failed
  clear is non-fatal. Bounded await chosen over a raw `await` precisely
  because B1 stale-index churn can stretch the write's retries; a clean
  leave must never hang on a flaky property write.

**Why both, in this order.** Fix 1 is the load-bearing fix (host stops
*trusting* stale data — race-proof). Fix 2 is hygiene (host stops
*receiving* stale data — the property is correct on the wire). With Fix
1 alone the bug is functionally dead; Fix 2 keeps the lobby data honest
for any other consumer and prevents the stale property from confusing
future features. Separate commits so a regression can be bisected.

**Residual / follow-up.** Fix 2's effectiveness still depends on the
clear-write succeeding within 3s; under heavy B1 churn it may time out
and leave the stale property on the wire — but Fix 1 makes that
harmless. The deeper B1/B6 SDK stale-index defect (the thing making the
write flaky) is tracked separately in `../PresenceSystem/BUGS.md` B1.

---

## B9 — Host-return: one client's vessel stuck in autopilot drift + party domains not reset to menu (Jade) 🟢

**Observed (MPPM, 1 host + 3 clients, 2026-06-09 — Session 2).** The S9
host-return fix works: the host taps **Main Menu** on the scoreboard and
all three clients return to `Menu_Main` together on the same live Relay.
But two defects surface on arrival:

1. **One client's vessel roams in a single direction and is
   uncontrollable.** It drifts straight — autopilot doesn't fly it
   normally and the player can't take control. Reproduced on "client 3"
   (one of the three); the other two roamed normally.
2. **Party domains are not reset to the menu domain (Jade).** Returning
   vessels keep their in-game domains (Jade/Ruby/Gold) instead of all
   re-syncing to Jade the way a fresh `Menu_Main` entry does.

**Root cause (confirmed 2026-06-11).** The old `MainMenuController.
ApplyMenuDomain` (since deleted) was the only menu domain "reset", and it
ran **client-locally** on each machine's own vessel:

1. **Domains not reset (symptom 2):** `Player.NetDomain` is
   `WritePermission.Server`; a client cannot write it, so the reset never
   reached the server — the server (and every other peer) kept each
   client's in-game domain. The method's comment claiming "NetDomain is
   owner-writable" was wrong. No server-side menu reset existed anywhere.
2. **Drift-stuck vessel (symptom 1):** for a returning client whose
   in-game domain ≠ Jade, the illegal `NetDomain.Value = Jade` write is
   rejected by Netcode; when the rejection throws, the exception aborts
   the rest of `ActivateLocalPlayerAutopilot` — `StartPlayer()`,
   `ToggleAIPilot(true)`, `SetPause(true)` never ran. That selects
   exactly the clients on non-Jade domains, matching "one of three stuck".
   The method's local `Player.Domain` / `RoundStats.Domain` stamps also
   desynced that machine's mirrors from the replicated truth
   (`../ScoringSystem/BUGS.md` B11).

**Fix (verified in code; granular commits squashed into the bleeding-edge
merge `0ea12370`, so the original `53294068` / `65d4da96` / `c073636e`
hashes no longer resolve — the surviving host-return-together hardening is
`f33abbb6`).** The menu domain
reset is now **server-authoritative**: `MenuServerPlayerVesselInitializer.
OnPlayerReadyToSpawnAsync` writes `NetDomain = menuVesselDomain` (Jade)
for every human BEFORE the vessel spawns, on every menu entry path
(fresh start, party join, host-return); the delta replicates and every
peer's mirrors + vessel paint follow via `Player.OnNetDomainChanged`
(whose repaint was completed via the init-aware
`ShipHelper.SetShipProperties`). `ApplyMenuDomain` and its field were
deleted — client code never writes domain state. Activation is now
exception-free and idempotent.

**Related.** The same session also fixed the party-join stale
`RoundStats` shadow (`../ScoringSystem/BUGS.md` B12; its `52923bf8` /
`6400eca0` hashes were likewise squashed into `0ea12370`) — a
returning/joined client's name-keyed stat lookups resolved a destroyed
pre-party component (frozen Jade).

**Repro (for verification).** 1 host + 3 MPPM clients → party up in
`Menu_Main` → host launches a domain game (e.g. Skim Race) → play to the
scoreboard → host taps **Main Menu** → every vessel on every peer
renders Jade, all clients roam on autopilot and can toggle freestyle; no
"not allowed to write" / `InvalidOperationException` in any console.

**Status.** 🟢 Fixed — domain-pick + ready-feed flow verified in a
2-human MPPM test (2026-06-11); full 4-player host-return sweep still to
be re-run.

---

## B10 — Host leaves/disconnects mid-party → client stuck (no bounce-to-solo) 🟢 (fixed; MPPM-verify)

**Symptom (reported 2026-06-18).** Host + client(s) in multiplayer freestyle
(Menu_Main lava lamp) — or any game mode. The host leaves mid-session or
disconnects. The client(s) **freeze / get stuck**: no return to a working menu,
no fresh solo host, no notice. Expected: each client cleanly exits the dead
party and **re-starts Menu_Main as its own host** (like a fresh launch), with a
**"Host disconnected"** notice.

**Root cause (confirmed in code).** A client whose host drops gets a genuine
per-machine `NetworkManager.OnClientDisconnectCallback`
(`MultiplayerSetup.OnClientDisconnect`, local-client branch ~`:362`), which raised
`gameData.InvokeOnSessionEnded()` → `SceneLoader.HandleActiveSessionEnd` (~`:302`).
That handler's **defer-to-server guard** (`IsListening && !IsServer`, ~`:308`)
returns early "deferring to server" — but the host/server is **gone**, so nothing
drives the transition and the client hangs. Even when it fell through, the path
(`ReturnToMainMenu` → `LoadSceneAsync` local fallback) **never recreated the
client's own solo Relay host** (`EnsurePartySessionAsync`), so the client would
land in a hostless menu (no autopilot vessel, can't invite/play). No notice either.

**Why the guard exists (and why it's wrong here).** The guard stops a
still-connected MPPM client from running `ResetAllData()` on the *shared*
`GameDataSO` when `OnSessionEnded` (a shared SOAP event) fires spuriously. But
`OnClientDisconnect` is a **real per-machine Netcode callback**, not a shared SOAP
event — so host-loss must be handled at *that* source, not routed through the
shared-SOAP-guarded SceneLoader path.

**Fix (this commit).** Route host-loss from the genuine per-machine signals
(`MultiplayerSetup.OnClientDisconnect` local branch + `OnTransportFailure`) into a
new `PartyInviteController.HandleHostLossAsync(reason)`, which reuses the proven,
MPPM-verified self-rescue `RecoverFromFailedTransitionAsync` (the B3.b sequence):
destroy local player+vessel → reset → leave UGS session → **NM shutdown** → load
`Menu_Main` locally → **`EnsurePartySessionAsync`** (recreate own solo host) — plus a
"Host disconnected" bounce toast. Works uniformly from the lava-lamp menu AND any
game scene (recovery always returns to Menu_Main). Idempotent via `_transitioning`
so the `OnClientDisconnect` + `OnTransportFailure` double-fire on a hard drop runs a
single recovery.

**Does NOT affect graceful host-return.** The host's deliberate "Main Menu /
return whole party together" (S9) keeps the Relay alive and uses a Netcode scene
load, so clients **never receive `OnClientDisconnect`** — this fix only fires when
the host actually leaves/drops (Relay torn down). So there's no false "Host
disconnected" on a normal host-led return.

**Notice — interim.** Uses the existing bounce **toast** ("Host disconnected").
Because `ToastService` is a **scene-bound** MonoBehaviour (subscribes to the
channel in `OnEnable`, no `DontDestroyOnLoad`), a toast raised *before* the
recovery's Menu_Main reload is silently dropped (the renderer is destroyed by
the reload, and there is no `ToastService` at all in a game scene). So
`BounceToSoloMenuAsync` was changed to recover **first**, then raise the toast on
the fresh menu's live `ToastService`. Once Task 0 (the SOAP confirm-popup,
`INVITE_ENHANCEMENTS.md`) ships, upgrade the notice to a 1-button "OK" popup
(same post-recovery timing).

**Recommendation (the "better thing").** Bounce-to-solo is the right call now —
reliable, and it reuses proven machinery. True **host migration** (promote a
remaining client to host and keep the party alive) is the fancier alternative but a
much larger project: Relay has no native re-host, Netcode has no native host
migration, and it needs full game-state transfer. Tracked as the
`../MultiplayerArchitecture/ROADMAP.md` "Host-loss resilience / migration (High)"
item — revisit when the party core is otherwise stable.

**Evidence (file:line — drifts).** `MultiplayerSetup.cs` `OnClientDisconnect`
(~`:344-379`), `OnTransportFailure` (~`:396-431`); `SceneLoader.cs`
`HandleActiveSessionEnd` defer-trap (~`:302-323`), `ReturnToMainMenu` (~`:190-222`),
`LoadSceneAsync` (~`:224-257`); `PartyInviteController.cs` `HandleHostLossAsync` +
`BounceToSoloMenuAsync` + `RecoverFromFailedTransitionAsync`.

**Status. 🟢 Fixed — verified in engine (2026-06-19).** The core bounce-to-solo,
the post-recovery notice timing, and the `joined_party` hygiene clear are all
confirmed working: a client whose host leaves reforms as its own solo host and can
invite + play normally. The cases below stand as the regression checklist (re-run
the 4-VP + hard-drop variants on any change to the recovery path):
- **Menu freestyle (2-VP):** host + client in the lava lamp; host leaves → client
  shows "Host disconnected", returns to its OWN Menu_Main as solo host (autopilot
  vessel spawns, can invite again). No hang.
- **Game mode:** host + client in SkimRace / Joust / etc.; host quits mid-game →
  client bounces to solo Menu_Main with the notice + working solo host.
- **3-4 VP:** host drops → every client bounces to its own solo menu.
- **Regression:** host's graceful "Main Menu" return still brings the whole party
  back together — no false "Host disconnected".
- **Hard drop:** kill the host process (not graceful) → clients still recover
  (covers the `OnTransportFailure` path + the double-fire idempotency).
- **Unrestricted after recovery (key acceptance).** The recreated solo host must be
  a *full* host, not a degraded state — verify the bounced client can then, with no
  app restart: (a) **invite** another player and have them join (client is now the
  host of the new party); (b) **launch every game mode** (solo + AI backfill, and a
  2-human game after re-inviting); (c) be **seen as invitable** by others again.
  Guaranteed by construction: `EnsurePartySessionAsync` sets `IsPartyHost = true`,
  `PartyState.InParty`, a fresh Relay session (`IsServer = true`), and
  `SeedLocalPlayer(clearFirst:true)`; the presence lobby is never left (separate UGS
  WebSocket on the always-alive HCS) — i.e. the **identical end-state as the
  deliberate "Leave Party" flow** (`LeavePartyAndReturnToMenuAsync`), where play +
  invite already work normally.

> **Hygiene (done).** `HandleHostLossAsync` now also fires
> `HostConnectionService.ClearJoinedPartyAsync()` (exposed public) so the client's
> own stale `joined_party` — still pointing at the dead host's session — is cleared
> on recovery, mirroring the deliberate `LeavePartyAsync`. Fire-and-forget: the
> presence lobby is independent of the torn-down party session / NM, so the write
> lands; and B8 fix 1 already makes a stale value inert, so recovery never blocks on
> it. This is a real (not just cosmetic) clear because `BuildLocalPlayerProperties`
> only resets `joined_party` on a presence-lobby *rejoin*, which recovery does not do
> — so without the explicit clear the stale value would persist until the next
> join/leave.

---

## B11 — Idle Relay allocation goes stale; every later join bounces at step 3 🟢 (CLOSED 2026-09-11 — the symptom was B16; the recycle stays reverted)

**Ruling (2026-09-11).** Closed as superseded. It was left ⚪ with a reverted fix,
which is the one state a bug must not be parked in: the next person reads "the fix
was reverted" and re-lands it.

**Why closed.** Every symptom in this entry is B16's, and B16 was root-caused and
LIVE-verified on the reporter's two machines with this exact sequence — party up,
play, return, leave, re-invite — on 2026-09-02. The chain: an in-place NetworkManager
restart loads `Menu_Main` locally, so the lava lamp's fauna are already swimming when
the host starts; Netcode adopts them as in-scene placed objects; two instances of one
prefab share a `GlobalObjectIdHash`, so `PopulateScenePlacedObjects` throws; the scene
manager is left half-built and no guest can ever synchronize. The guest's 30 s
watchdog then bounces them at step 3, which is what this entry recorded. "Restart the
game" worked because a cold boot starts the NM before `Menu_Main` exists — not because
it minted a fresh Relay allocation.

**The stale-allocation theory itself is not supported.** B11's own retest note
already says so: with `runInBackground` on, UTP keeps a bound host's Relay allocation
alive with its own pings, and the `player timed out due to inactivity` lines in the
host log are as likely the OLD transport's allocation after a restart as a live one
going stale. No independent evidence for idle-allocation death was ever produced.

**The recycle stays reverted, and re-landing it is the specific mistake to avoid.**
It made things worse for a reason that is now fully understood: recreating the session
runs `EnsurePartySessionAsync`, which restarts the NetworkManager — i.e. it
manufactured B16's trigger every four minutes. This bug's fix and this bug's cause
were the same action.

**What would reopen it.** A guest bouncing at step 3 against a host that has NOT
restarted its NetworkManager in-process since boot (no party leave, no host-loss
bounce), with the host's log showing `Relay allocation is invalid` and NO
`PopulateScenePlacedObjects` exception. That is a different bug and wants a new entry;
the fix would be to keep the allocation alive (a transport-level keepalive), never to
recycle the session.

*Original record follows — the symptom description is still the best one we have.*

### Original entry

**Symptom.** Host has been sitting in Menu_Main for a few minutes. Guest accepts an
invite, sees the splash for ~30s, is bounced ("Couldn't join - returned to your menu").
Host's log shows `player timed out due to inactivity` / `Relay allocation is invalid`;
the guest's log shows `[Deferred OnSpawn]` for every scene NetworkObject and
`IsConnectedClient` never turns true. Reported as "players across the globe cannot
connect" and "invites keep failing, need to restart the game" — both are elapsed
COORDINATION TIME, not distance: the further apart two players are, the longer the
host idles before the invite lands.

**Root cause.** The locked EAGER per-user Relay design allocates a Relay slot the moment
a player enters Menu_Main. Relay reclaims an allocation whose host sends nothing for a
few minutes — and a host with zero peers has no connection to send on, so its
allocation dies on the shelf. The UGS session keeps advertising the dead join code:
the guest joins the session fine, connects to a Relay allocation nobody is listening on,
Netcode synchronization never completes, and the 30s connect watchdog bounces them.
Nothing in the project observed `RelayConnectionStatus.AllocationInvalid` and nothing
kept the allocation alive. "Restart the game" worked because it minted a fresh one.

**Reverted.** The recycle (leave + recreate the solo session every 240s of idling) was
shipped and then REVERTED the same day: recreating the session goes through
`EnsurePartySessionAsync`, which shuts down and restarts the NetworkManager — so every
idle host got the "restarted host" state B14 describes every four minutes, plus a visible
menu-vessel despawn/respawn. The live retest also showed the theory was at best partial:
with `runInBackground` on, UTP keeps a bound host's Relay allocation alive with its own
pings, so the "player timed out" lines in the host log are as likely the OLD transport's
allocation after a restart as a live one going stale. Keep the symptom record; do not
re-land the recycle.

**What the fix was.** `HostConnectionService.RecycleIdlePartySessionIfStaleAsync` (refresh tick,
before the acceptance scan): a host session that has sat `IDLE_SESSION_RECYCLE_SECONDS`
(240s) with no remote members, no outgoing invites, no connected Netcode clients and no
transition in flight is left and recreated, and the party state republished — so the
advertised join code is always one the Relay still honours. Skipped the moment anyone is
in or on their way in.

**Retest.** Host idles > 4 minutes in Menu_Main, then invites; guest accepts and lands
in the party without a bounce. Watch for one `Recycling idle party session` line per
4 minutes of solo idling and nothing else.

---

## B12 — A host can never re-invite a guest who once accepted or declined 🟢 (fixed 2026-09-01, live-verified 2026-09-02)

**Symptom.** Guest B accepts A's invite and bounces (B11, or any join failure). A
invites B again: B's popup never appears, forever. Same if B DECLINED. Only a host
restart (new session id) clears it. This is one leg of "the 3rd player can never get
in": the third player tried once, failed, and every re-invite was silently eaten.

**Root cause.** `HostConnectionService.TryRaiseIncomingInvite` keyed its dedup on the
SENDER and kept `_lastFiredInvite` forever. `_lastInviteResolved` is set at the TOP of
`AcceptInviteAsync` (before the join is attempted) and by `DeclineInviteAsync`, after
which every later invite from that host matched the "PENDING → real id transition of
an already-resolved invite" branch — same host, same session id — and was swallowed
without a log line (the same pair also satisfies the `isDuplicate` test).

**Fix.** `ForgetWithdrawnInvite`: the record is dropped two refresh ticks after the
host's `invite_payloads` line for us is gone — which happens exactly when the invite is
over (cleared on our corroborated join, cancelled by the host, or the 60s timeout) —
so the next line from that host surfaces as a NEW invite. Not while a join is in
flight (the line vanishes as the corroboration lands) and not while we are inside
that host's party (the in-session guard answers a stale re-appearance there). An
UNRESOLVED invite whose line vanished was withdrawn by the host, so `OnInviteResolved`
is raised and the popup goes too — which also gives the host's ✕-cancel its recipient
half. Two consecutive misses are required so one stale lobby snapshot (a presence
converge mid-tick) cannot flicker a live invite.

**Retest.** A invites B → B declines → A invites B again → popup appears. A invites B →
B accepts → B is bounced (pull the cable) → A waits for the invite to expire (60s) or
cancels it → A invites B again → popup appears and the join completes.

---

## B13 — Open-lobby ClientRpc dropped on a syncing / late-joining client 🟢 (fixed 2026-09-01, live-verified 2026-09-02)

**Symptom.** Host opens a card; the guest stays on the lava lamp. "If the host comes
out of the card and clicks again the client should be pulled in" — and even that only
worked when the guest happened to be fully synchronized at the instant of the click.

**Root cause.** `ArcadeConfigSyncManager` announced open / close / intensity / roster
with one-shot ClientRpcs. A ClientRpc reaches the clients that are synchronized when it
is SENT: a guest inside Netcode scene synchronization has it deferred then dropped
(`[Deferred OnSpawn]`), and a guest who joins after the host opened the card is never
told at all. "Come out and click again" only worked when the retry happened to land
after sync — and after B11 it never did, because sync never completed.

**Fix.** The open lobby is one server-written `NetworkVariable<LobbySnapshot>` (card,
intensity, seats, domain count, placed AI domains, and a generation that climbs on every
open). A late joiner receives it with the spawn and applies it in `OnNetworkSpawn`;
every other change is diffed against the previous value and raised through the SAME
C# events the modal already listened to, so the modal's handlers did not change. The
modal also asks for a replay when it subscribes (re-enabled mid-lobby). The ready-up
head-count is read live off the connected clients, so a member who joins mid-lobby is
waited for and one who leaves stops being waited for.
`Docs/ArcadeLaunch/ARCHITECTURE.md` §3.1.

**Retest.** Host opens a card BEFORE the guest finishes joining → guest lands directly in
the lobby. Host closes and reopens → guest follows both. Host changes intensity / places
an AI with a guest in the lobby → guest's row and chips follow.

---

## B14 — A host whose NetworkManager was RESTARTED in-process cannot get a new guest through synchronization 🟢 (root cause = B16; fixed + LIVE-VERIFIED 2026-09-02)

**Symptom (live retest, 2026-09-01, post-fix branch).** First run: A and B form a party,
play a game, come back. Then (a) A leaves the lobby and quits, B stays; A relaunches; B
invites; A accepts and gets the step-3 bounce every time. (b) Both leave the lobby; either
invites the other; the accept bounces every time. Invites themselves are instant.

**What the failing cases share.** The INVITER's NetworkManager had been shut down and
restarted in-process at least once — by a party leave (`LeavePartyAndReturnToMenuAsync`), by
a host-loss bounce (`RecoverFromFailedTransitionAsync`), or by the now-reverted idle recycle
(B11). Every restart path is the same shape: NM shutdown → LOCAL `SceneManager.LoadSceneAsync
(Menu_Main)` → `EnsurePartySessionAsync` (new session, SDK `StartHost` on top of a
locally-loaded scene). The one host that has always worked is the cold-booted one: NM started
in the Authentication scene, Menu_Main loaded THROUGH `NetworkSceneManager`. The joiner's
state does not matter — (a)'s joiner was a fresh process. The game being played first is
incidental: it is what put both players through a leave.

**Ruled out from source.** Presence/invite layer (invites arrive and are accepted instantly);
`MultiplayerSetup`'s legacy matchmaking path (it lives in Bootstrap, runs once at boot);
dependency injection on NGO-spawned Players (`gameData` is a serialized field); party
state-machine gates (nothing on the join path checks `CurrentState`); capacity.

**Not yet ruled out (needs the logs).** NGO scene bookkeeping on a host that starts with a
LOCALLY loaded scene (`ScenesLoaded` / in-scene object scene handles → the Synchronize
payload a new client is sent); the Multiplayer SDK's network-handler rebinding after a
session delete + create on the same NetworkManager; an exception on the host during the
new client's approval/synchronize that NGO swallows.

**RESOLVED by B16 (2026-09-01).** The restart WAS the trigger, and the mechanism is the
stray-NetworkObject adoption described in B16 - a restarted host loads Menu_Main locally, so
the lava lamp's fauna are already swimming when it starts, and Netcode adopts them. Everything
below stands as the investigation record; the fix is B16's.

**Trace (shipped).** `MultiplayerSetup` now logs `[NetTrace]` lines on BOTH peers: network
start/stop with the active scene, every `NetworkSceneManager` scene event (Synchronize,
SynchronizeComplete, Load, LoadComplete, Unload…) with client id, and every connected client.
A failing join now shows which half stalled — the host never sending Synchronize, or the
client never completing its scene load.

**Discriminating test (run this before anything else).**
1. Fresh boot both. A invites B → B joins. **B leaves** (client leave; A's NM untouched).
   A re-invites B → expect JOIN OK (A is still a cold-booted host).
2. Now **A leaves** (host leave; A's NM restarts; B is bounced and restarts too).
   A invites B → expect the bounce. B invites A → expect the bounce.
3. Send both `Player.log`s from step 2 — the `[NetTrace]` lines around the accept are the
   evidence.
If step 1 passes and step 2 fails, the restart is the trigger and the fix is to bring a
restarted host back to the cold-boot shape (start the host first, then load Menu_Main
through `NetworkSceneManager`) — a structural change to the leave/bounce paths that should
land on evidence, not on this hypothesis.

---

## B15 — Nobody is ever shown "in game"; friends list only moves when someone returns to the menu 🟢 (fixed 2026-09-01, live retest pending)

**Symptom.** A player who launches or joins a match keeps showing as plain "online" for
everyone, and the online list of a player IN a match freezes until somebody comes back to
Menu_Main.

**Root cause (two halves).** `HostConnectionService.Update` returned outside Menu_Main, so
the presence lobby was never refreshed, no invite expired and nothing was published for the
whole match; and the one publish that did fire, `HandleGameLaunch`, ran on `OnLaunchGame`
BEFORE the scene changed, so `ResolveCurrentMatchName` still saw Menu_Main and published an
EMPTY match name. Separately, `FriendsInitializer.SetPresenceInGame` / `SetPresenceInMenu`
existed but were called by nothing outside the tests, so UGS Friends presence read "In Menu"
forever.

**Fix.** The presence lobby keeps ticking in a game scene at a tenth of the menu cadence
(`IN_GAME_REFRESH_INTERVAL_SECONDS`, 10s); `HostConnectionService.OnSceneLoaded` publishes
presence again once a game scene is active (match name now real); `FriendsInitializer`
follows `sceneLoaded` into In Game / In Party / In Menu.

---

## B16 — Un-spawned fauna NetworkObjects break synchronization for every guest 🟢 (root cause; fixed + LIVE-VERIFIED 2026-09-02)

**Symptom.** A guest accepts an invite, sits on the splash for 30s and is bounced
("Couldn't join"). The guest's log shows `[Netcode] [Deferred OnSpawn] Messages were received
for a trigger of type NetworkVariableDeltaMessage / ClientRpcMessage associated with id (1/3/4),
but the NetworkObject was not received within the timeout period 10 second(s)`, then
`[NetworkTransitionService] Client connection not confirmed after 30s`. The HOST's log carries
an exception whose stack runs through **`Unity.Netcode.NetworkSceneManager.PopulateScenePlacedObjects`**.
Once a machine is in this state, no invite it sends or accepts can ever complete, and only an
application restart clears it — which is the whole "invites keep failing, restart the game"
report, and (through elapsed coordination time, never distance) the "players across the globe
cannot connect" one.

**Root cause.** When a NetworkManager starts as a server, Netcode sweeps the loaded scenes and
adopts every NetworkObject that is not already spawned, treating each one as an IN-SCENE PLACED
object. It then indexes in-scene objects by `(GlobalObjectIdHash, sceneHandle)` — and every
instance of one prefab carries the SAME hash, so the second un-spawned instance of a prefab in
one scene makes `PopulateScenePlacedObjects` **throw**. That leaves the scene manager
half-built; the host then advertises scene objects a guest cannot resolve, the guest's
synchronization never completes, and the deferred messages above are the far end of it.

The prefabs supplying those instances are the **fauna**: `QuadFish`, `TadPoleFauna`,
`MassSharkFauna` and `MassBrittlestarFauna` each carry a root NetworkObject as their
replication opt-in (`FaunaConfigurationSO.NetworkSynced`, `Docs/ECOSYSTEM_NETWORK_SYNC.md` §5),
and every shipped fauna config leaves that opt-in **off**. So each creature the lava lamp
spawns is an un-spawned NetworkObject, and Menu_Main holds a dozen of them.

**Why a COLD-BOOTED host never noticed.** It starts in the Authentication scene, before
Menu_Main and its fauna exist, and Menu_Main is then loaded THROUGH `NetworkSceneManager` — the
index is built while the scene is still empty of fauna (they arrive behind `Cell.InitDelayMs`).
Every in-place restart inverts that order: `LeavePartyAndReturnToMenuAsync` and
`RecoverFromFailedTransitionAsync` shut the NM down, load Menu_Main **locally**, and only then
call `EnsurePartySessionAsync` — so the host starts into a scene already full of identical-hash
strays. That is why the failures always followed a leave or a bounce, why they looked like they
followed "playing a game" (a game is what you leave), and why the reverted idle-session recycle
(B11) made it worse: it restarted the NM every four minutes.

**Fix.**
- `NetworkSceneObjectGuard.NeutralizeStray` strips the network layer (NetworkBehaviours, then
  the NetworkObject) from anything that will never be network-spawned, so strays never
  accumulate. `FaunaNetworkSync.ServerSpawn` calls it on both of its declining branches — the
  species is not `NetworkSynced`, or this peer is not a live server — which covers every
  creature both spawners and reproduction produce.
- `NetworkSceneObjectGuard.Sweep` is the backstop for anything else that instantiates a
  networked prefab without spawning it: it strips the surplus of any group of two or more
  un-spawned NetworkObjects sharing a `(hash, scene)` pair, keeping the first (a genuine
  in-scene object is legitimately un-spawned before the host starts). It runs immediately
  before each of the four calls that start a NetworkManager: party session create, party
  session join, the offline `StartHost`, and the game-scene matchmaking path.
- `FrogletTools ▸ Validation ▸ Audit Stray Network Objects` reports the authoring combination
  (a fauna config whose prefab carries a NetworkObject with replication off) so the guard never
  has to fire.

`GlobalObjectIdHash` is `internal` in Netcode, so the sweep reads it by reflection — resolved
once, at transition boundaries only, and degrading to a no-op with one warning if a future
Netcode renames it. The birth-time strip needs no reflection and is the primary defence.

**Verified live (2026-09-02).** The sequence that had failed every time - party up, play a
game, return, leave, re-invite - now joins cleanly on the reporter's two machines, and the
audit reports the fauna prefabs as the staged-rollout state with no unstaged strays. The
invite/accept path also stayed fast, so B12 and B13 are confirmed with it.

**Retest.** (1) A and B fresh-boot, party up, play a game, return, **A leaves**, then A invites
B → the join should complete. (2) Repeat with B leaving, and with both leaving. (3) With a
guest in the party, watch for `[NetworkSceneObjectGuard]` warnings — one per prefab is expected
and harmless the first time each species spawns; a `stripped N stray NetworkObject(s)` line
from the sweep means something other than fauna is instantiating a networked prefab and should
be tracked down with the audit.

---

## B17 — Boot stalls on a blank screen: the sign-in loses the race to the splash timer 🟢 (root-caused + fixed 2026-09-08)

**Symptom.** "Sometimes on bleeding edge the game does not load and gets stuck at the loading
screen." No error, no exception, no bounce. The console shows the whole online stack coming up
perfectly — QoS, `[PartySessionService] Created party session …`, `[PartyStateMachine]
HostingParty → InParty`, `[SoapPartyEventBus] RaiseHostConnectionEstablished`,
`[HostConnectionService] Solo party session ready … InParty, vessel will spawn` — and then
**absolute silence** until the tester stops play mode ~38 s later. Menu_Main is never loaded:
there is no `[NetTrace] SceneEvent` and no `[AuthScene] Loading Menu_Main …`.

**The tell is what is MISSING, not what is there.** Every failure path in
`AuthenticationSceneController` logs — including its 10 s `safetyTimeout`, which exists exactly
to force-navigate out of a hang. So a run with *zero* `[AuthScene]` lines and no safety-timeout
warning is not a hang at all: it is the flow having **completed**, into a state that logs
nothing.

**Root cause — three defects in a row, each harmless alone.**

1. **The race.** `AppManager.RunBootstrapAsync` loads the Authentication scene after
   `MinimumSplashDuration` and does **not** wait for authentication.
   `AuthenticationServiceFacade.StartAuthentication` is fired from `AppManager.Start` and never
   awaited. Those are two independent clocks, so on a slow UGS round trip the scene load wins
   and `AuthenticationSceneController.Start` runs while `SignInAnonymouslyAsync` is still in
   flight. `IsAlreadySignedIn()` is therefore false, and the happy path is skipped.

2. **The second sign-in.** `TrySignInCachedAsync` then issued a *competing*
   `SignInAnonymouslyAsync` against the one already running. The SDK refuses it
   (`ClientInvalidUserState` — "already signing in"); the catch stamped `AuthState.Failed` over a
   sign-in that was in fact succeeding and returned `false`. (A machine with no cached session
   token reached the same `false` even faster, with no log at all.) The scene concluded "no
   session" for an account that was a heartbeat from being signed in.

3. **The dead end.** `RunAuthFlowCoreAsync` fell to its last branch — `HideLoading();
   ShowAuthPanel();` — and **returned**. That return is what retires the safety timeout:
   `UniTask.WhenAny(core, Delay(safetyTimeout))` completes on index 0 and the watchdog is
   dropped. Nothing was ever going to navigate again. And the panel it parked on was
   unusable: `AuthPanel` has exactly two children, `LoginStatusText` (blanked by
   `ClearStatusMessages` / `HideLoading`) and **`GuestLoginButton`, authored `m_IsActive: 0` in
   `Authentication.unity` with no code anywhere activating it**. So the player was left looking
   at a bare `Background` — indistinguishable from a stuck loading screen, with nothing to press.

Seconds later the original sign-in completed and raised `OnSignedIn`, so the presence lobby, the
party session and the Relay host all came up **behind a screen that was never going to move**.
That is the log above, in full.

**Fix.**
- `AuthenticationServiceFacade` now **coalesces concurrent sign-ins** onto one attempt
  (`_signInTask` / `SharedSignInAsync` / `SignInAnonymouslyCore`), the sign-in twin of the
  `_initTask` coalescing that was already there for initialization. `TrySignInCachedAsync`
  JOINS an in-flight attempt instead of racing it and reports what is true once it lands; the
  session token now decides only whether to *start* a sign-in, never whether to *wait* for one.
  The spurious `AuthState.Failed` write is gone with the competing call.
- `AuthenticationSceneController` gained a **resume net**: it subscribes to
  `AuthenticationData.OnSignedIn` and, if a sign-in lands while the boot is parked, resumes
  `HandlePostAuthFlowAsync` instead of leaving the player behind the rest of the app. (Marshalled
  through `MainThreadDispatcher` — the SDK's own `SignedIn` event can raise off-thread and SOAP
  raises listeners inline.)
- A parked boot now **announces itself** (`ParkForPlayerInput`), and the watchdog force-navigates
  if the flow ever finishes without navigating *and* without leaving the player a surface.
- `EnsureGuestLoginVisible` activates the guest-login button when the panel is shown and warns if
  it was inactive or unwired; `Authentication.unity` now authors it active.

**The general lesson.** *A terminal state that logs nothing is indistinguishable from a hang* —
this one cost a whole investigation because the hardest fact to establish was an ABSENCE. And its
sibling: *finishing is not the same as succeeding* — a watchdog armed on "did the work finish?"
is retired by the very branch it exists to rescue. Arm it on "did we get where we were going?".

**Retest.** (1) Boot repeatedly on a slow/throttled connection — the first frame after the splash
should reach Menu_Main every time, never a blank screen. (2) Force the parked path (offline at
boot, or a build with no cached session token): the guest-login button must be visible and
pressable, and `[AuthScene] Waiting for the player: …` must appear in the console. (3) With the
panel parked, restore the network — the boot should resume on its own and log
`[AuthScene] Sign-in landed while the boot was waiting for the player - resuming.`

---

## B18 — A client cannot leave a match at all, and cannot leave a Maelstrom tournament until it ends 🟡 (root-caused & fixed 2026-09-11; needs a playtest)

**Symptom (owner report, live play, 2026-09-11).** "Once you get off the happy path the game is
bugged, and the experience is not good for clients… leaving is a challenge. Not in Maelstrom, not in
the whole game." Reported against 4 players all in the US, so it is not latency.

**Root cause — the exit was hidden, not broken.** Every screen a client can be on during a party
match gated its way out behind `IsServer`:

| Where a client is | Exit before this fix |
|---|---|
| Mid-match, pause menu | ❌ `PauseMenu.ConfigureHostOnlyButtons` set `mainMenuButton` inactive for any non-host |
| Per-game Scoreboard, normal mode | ❌ — see **B22**. `leaveLobbyButton` was `{fileID: 0}` in the prefab, so the branch that fixed B18 propagated an exit that was itself a no-op here |
| Per-game Scoreboard, **Maelstrom** | ❌ the Maelstrom branch set **all four** buttons inactive for a client |
| Maelstrom hub between games | ❌ only READY is shown (`ShowActive` never activates the end buttons) |
| Maelstrom final summary | ✅ the one screen that was right |

So in a normal game a client could not leave until the match ended; **in a Maelstrom a client could
not leave until the entire race-to-N ended.** The only exit was killing the application — which is
exactly what "leaving is a challenge" describes, and why it was reported as worst in Maelstrom.

**Why hiding it looked right.** Raising the Main Menu event on a client genuinely does not work:
`SceneLoader.ReturnToMainMenu` defers scene loads to the server, so a client that raised it would
fade to black and wait on the host forever (that is B19). Faced with a button that hangs, hiding it
is a reasonable local call. The mistake was stopping there rather than asking what a client's press
*should* mean.

**The answer was already in the tree, applied once.** `MaelstromSceneView.OnMainMenuPressed` has the
correct shape and its comment names this exact trap: a client LEAVES THE PARTY instead —
`PartyInviteController.LeavePartyAndReturnToMenuAsync` disconnects, loads Menu_Main locally and
restarts a solo Relay. That is the same proven path as the Scoreboard's Leave Lobby. It was
implemented on the summary screen and nowhere else.

**Fix.** Propagate it. `PauseMenu` shows Main Menu to everyone and routes a client's press to the
leave-party path (host's press is unchanged); `Scoreboard`'s Maelstrom branch shows `leaveLobbyButton`
to clients. Replay stays host-only — the host's Play Again forces everyone to replay, so offering it
to a client would be misleading, which is the distinction the original gate was reaching for.

**General rule.** *When one screen solves a cross-cutting problem correctly, the fix is not done
until every screen with that problem uses it.* The client's ability to leave depended on which screen
they happened to be looking at, and nothing in the code said it was supposed to be uniform.

**Known gap (not a blocker).** `PauseMenu.mainMenuButtonLabel` is optional and unwired in the shipped
prefabs, so a client's button still reads "MAIN MENU" rather than "LEAVE PARTY". It works either way
and the player does land in their main menu; wiring the label is a prefab edit for the editor pass.

**Retest.** Host + at least one client. (1) Client opens the pause menu mid-match → Main Menu is
present → press → client lands in its own menu, host's match continues. (2) Same in a Maelstrom game,
and from the per-game scoreboard. (3) Host is unaffected on both: its Main Menu still takes the whole
party back. (4) Watch the host for clean roster removal (`ReconcilePartyMembersNow`).

---

## B19 — Nothing watches a client's scene transition, so a lost one is a permanent black screen 🟡 (root-caused & fixed 2026-09-11; needs a playtest)

**Symptom.** The client half of the same report: transitions that sometimes never complete, leaving
the player on an opaque screen with no error and no way out. Amplified by Maelstrom, because a
tournament is a chain of host-driven scene loads and every link is another chance to hang.

**Root cause.** Every path where a client follows the host into a scene covers the screen and then
waits with **no timeout**:

- `SceneLoader.LaunchGame` / `ReturnToMainMenu` / `HandleActiveSessionEnd` each call
  `SetFadeImmediate(1f)` (or arrive already covered) and then `return` at the defer-to-server guard.
- `MultiplayerMiniGameControllerBase.ShowReturnToMenuVeil_ClientRpc` blacks out every client's screen
  ahead of the host's teardown.

The veil then lifts only when the new scene loads. If the host's scene event never reaches this
client, nothing ever lifts it.

**This was already known in one direction.** `MultiplayerSetup.OnClientDisconnect` deliberately
routes host-loss *around* `SceneLoader.HandleActiveSessionEnd` because — in its own comment — "the
defer-to-server guard hangs the client when the server is gone". That covers a host that is
definitively gone. It does nothing for a host that is alive and simply never got us into the scene,
because there is no event to react to: **the absence of an event is the failure**, so the only
possible detector is a timeout.

**Fix.** A client scene-follow watchdog in `SceneLoader`, generation-counted so a transition that
lands (or one a newer transition supersedes) retires its own watchdog, and disarmed by the scene
actually loading. On expiry it takes the same self-rescue as host loss
(`PartyInviteController.HandleHostLossAsync`): back to the player's own working menu, with the toast
raised after recovery so it lands on the fresh menu's live `ToastService`.

**Why the timeout is 90s and not 10.** A false positive costs a player their party and leaves them
somewhere they can rejoin from. The black screen it replaces costs them the application. Those are
not close, so the bound is set to be certain the transition is never coming rather than to react
quickly.

**The load-bearing detail — the call site that matters is NOT in `SceneLoader`.** Its three defer
guards are reached through SOAP events, and **a SOAP raise is local; it does not cross the wire.** On
separate machines a client never runs `LaunchGame` or `ReturnToMainMenu` at all — those guards exist
for MPPM virtual players, which share one `GameDataSO` in one process. What blacks out a *shipped*
client's screen is the `ShowReturnToMenuVeil_ClientRpc`, so the watchdog arms there too. Arming only
the `SceneLoader` guards would have looked correct in the editor and protected nobody in the build.

**General rule.** *A guard whose trigger is a local event only fires for peers that share the
process.* When a fix has to hold on real hardware, find the path that runs on real hardware — which
for anything cross-machine means an RPC, not a SOAP raise.

**Retest.** Host + client, in a game scene. (1) Host presses Main Menu → client follows normally, no
watchdog line. (2) Force the failure (suspend the host process, or pull its network, after the veil
lands) → the client should bounce to its own menu with a toast instead of holding black. (3) Confirm
no spurious bounce on a slow but legitimate load of the heaviest arena.


---

## B20 — A player leaving mid-wait strands the whole party at the ready screen, forever 🟡 (root-caused & fixed 2026-09-11; needs a playtest)

**Symptom.** Everyone sits at a ready screen that never advances. No error, no timeout,
no way forward. Two separate gates, same shape — the MATCH gate (per turn/round) and the
LAUNCH LOBBY gate (before the scene loads).

**Root cause.** Both gates were evaluated **only inside the press RPC**, comparing a
running tally against a human count read live at that instant. So when a player left,
dropped or crashed while the others were waiting on them, the comparison that would now
pass was never run again:

> Three humans. A presses (1/3). B presses (2/3). C leaves. The head count is now 2 and the
> tally is 2 — the gate is satisfied — and nothing evaluates it. A and B wait forever.

Nobody can press again either: neither gate is a toggle, so a player who already pressed
early-returns. In the lobby the host has to close the card and start over; in a match
(before B18) there was no way out at all.

**A count is a snapshot of an answer; the ROSTER is the question, and it keeps changing.**

**Second defect, match gate only.** It kept a bare `int`, so a double-press — a rebound
tap, or a Ready button not yet hidden on a laggy client — satisfied the gate on behalf of
somebody who had not pressed, and the match started without them. Keying on the sender
makes a press idempotent.

**Fix.** A set of WHO, re-evaluated on every Ready press **and** every client disconnect,
with departed clients pruned rather than trusted (a stale id would let the gate pass on
behalf of somebody who is gone). `humanCount <= 0` holds rather than launching for nobody.

**Written twice, so fixed once.** The match gate existed in two copies —
`MultiplayerDomainGamesController` and `CoOpWildlifeBlitzMiniGame` — with *both* defects in
each. It now lives on `MultiplayerMiniGameControllerBase` (`MarkClientReady` /
`EvaluateReadyGate` / `OnAllPlayersReady` / `ResetReadyGate`), so a third mode cannot write
a third copy. **Two copies of a rule is how the second one gets forgotten.**

**The lobby's own reasoning, and why it was overridden.** Its disconnect handler removed the
client and re-announced the count but deliberately did NOT re-decide — *"a launch is
something a PRESS causes, never a departure."* That is a fair instinct and it left the gate
satisfied-but-unchecked. Launching there is not a surprise: every remaining player has
explicitly pressed Ready, and the departure does not CAUSE the launch, it removes the last
thing blocking one the others already asked for. Reversible in one place if playtest
disagrees (`EvaluateLobbyReadyGate`).

**Retest.** 3 humans in a match and in a launch lobby. Two press Ready, third leaves →
the turn/launch should proceed within a tick. Also: press Ready twice quickly → the match
must NOT start until everyone has pressed.

---

## B21 — A pilot who leaves mid-match takes their ship AND their score out of the arena 🟡 (root-caused & fixed 2026-09-11; needs a playtest)

**Symptom (owner report).** *"Once one client leaves mid game the vessel should be replaced
by an AI and the score of the client should still count… The vessel should not just
disappear."*

**Root cause.** Netcode destroys a client's owned objects when that client disconnects, and
BOTH the vessel and the `Player` are owned by that client. `Player` is where `RoundStats`
lives — the score. So a departure removed a ship from the arena mid-flight with no
explanation, and silently subtracted that player's contribution from their domain's total:
every surviving player was finishing a different match than the one they started.

**Fix — the AI takes the ship.** Both objects are flagged `DontDestroyWithOwner` **at
spawn**, so Netcode keeps them and reassigns ownership to the server itself; the disconnect
handler then only has to switch the pilot on. That split is deliberate: the callback runs
inside Netcode's own cleanup, so a handler that tried to rescue objects there would be
racing it. By the time we run they are already safe, and switching on a pilot is safe
whenever it happens.

The result is exactly an AI backfill bot reached from the other direction — a server-owned
`Player` marked `NetIsAI`, flying a server-owned vessel under `AIPilot`, configured to the
same standard as a backfill bot (mode-aware seeking, skill from intensity). `RoundStats` is
untouched, so the score earned still counts, and so does anything the AI earns after.

**Announced on every peer** (`GameToastSituation.PilotHandedToAI`). A ship flying under new
management is *less* confusing than one that vanishes — but only if everyone is told,
otherwise a pilot who suddenly flies differently reads as a cheat.

**Identifying who left needs its own map.** `_humanPlayersByOwner`, recorded at spawn:
Netcode may already have reassigned ownership by the time the callback runs, so asking "who
owned this?" then is asking a question whose answer has been erased. AI players share the
HOST's owner id, so only remote humans are recorded.

**The menu opts out** (`ConvertDepartedPlayersToAI => false`): an abandoned lava-lamp vessel
has no match to finish and would just accumulate autopilot ships.

**Retest.** Host + 2 clients in a scored match. One client leaves mid-match (Leave, and
separately a hard kill). Expect: the ship keeps flying under AI, a toast names the departed
pilot, the scoreboard still shows their score, and their domain's total still includes it.
Watch that the host's roster does not double-count them.


---

## How we work bugs

Method: see `../README.md` § "How we work bugs". Party-side priority order as of
2026-09-11: **B5 RETEST → B7**. Everything above B5 is closed; B7 stays deferred as
verified-mostly-benign.

**The one thing to get right here is that B5's next step is a TEST, not a code
change.** Its every named cause traces closed in source (see the table in its entry),
and its last observation predates all of them *and* the MPPM unique-tag prerequisite.
Reading the code again will re-derive that table; only a tagged three-player MPPM run
can tell "fixed" from "broken for a reason nobody has named". Same for Presence B4,
which now carries a fix nobody has executed.

Three bugs are 🟢 but carry an unrun retest in their entry — B3 (its dedicated TC4
bounce repro), B10 (the host-loss MPPM sweep) and B15 (the in-game presence retest).
They are fixed on evidence; the retests are confirmation, and they belong to the same
MPPM pass.

The presence-lobby cluster (B1, B4, B6) is the locked-design area and lives in
`../PresenceSystem/BUGS.md`.

---

---

## B22 — The Scoreboard's client exit and its rematch caption were never wired, so B18's propagated fix was a no-op on the one screen it claimed already worked 🟡 (root-caused & fixed 2026-09-11; needs a playtest)

**Symptom (owner report).** *"In a multiplayer game the client can press play again and the host
would not know, and the game is not started until the host presses the button."* Reported as a
missing signal; it was three separate gaps stacked.

**Root cause.** Two serialized fields on `Scoreboard` were `{fileID: 0}` in
`_Prefabs/CORE/GameCanvas.prefab`:

| Field | What was dead |
|---|---|
| `leaveLobbyButton` | `ConfigureLobbyButtons` calls `leaveLobbyButton.SetActive(isClient)` behind an `if (leaveLobbyButton)` guard, so a null field makes the whole branch a silent no-op. A client had NO exit on the per-game scoreboard, in any mode. |
| `playAgainLabel` | The rematch caption and the live tally (`REMATCH?`, `PLAY AGAIN (2/3)`) had never drawn on any peer. |

**Why it survived B18.** B18's table recorded the normal-mode Scoreboard as ✅ — the one screen
that already had a working exit — and the fix propagated that shape to the pause menu and the
Maelstrom branch. The claim was read off the CODE, which is correct and complete; the exit did not
exist because the PREFAB never pointed at a button. This is `/ship` §2's producer rule reached from
the other side: *a guarded call site reads exactly like a working feature when the reference is
null, because the guard is what a careful author writes.* Grep the prefab for the field, not just
the call site.

**Fix.** `LeaveLobbyButton` is a clone of the Continue button inserted as its next sibling in the
`Buttons` row (`HorizontalLayoutGroup`), which is the one slot where the two can share: Continue is
host-only Maelstrom and Leave is client-only, so no peer ever needs both. Text `CONTINUE` → `LEAVE
LOBBY`, `onClick` → `Scoreboard.OnLeaveLobbyButtonPressed`, ships INACTIVE so `ConfigureLobbyButtons`
decides. `playAgainLabel` points at the `PlayAgain` TMP.

**A defect the wiring exposed.** `"REMATCH ✓"` cannot render: the label's font
(`ChakraPetch-Regular SDF`) carries 97 glyphs — printable ASCII plus NBSP and an ellipsis — with no
fallback asset on the font, none in TMP Settings, and `m_missingGlyphCharacter: 0`. U+2713 would
have drawn a blank, and LiberationSans (the fallback if one were ever configured) does not carry it
either. It reads `REMATCH SENT`. The tally stays because it was measured against that font's own
metrics: the widest string the label can produce, `PLAY AGAIN (4/4)`, is 301.9 of the button's 326.4
units, and the label has wrapping ENABLED — a wrapped caption in an 86.4-tall button reads as two
captions.

**Open.** `PauseMenu.mainMenuButtonLabel` is still unwired (a client's exit there reads MAIN MENU
rather than LEAVE PARTY — it works, it just misnames itself). Untested in the editor.

## B23 — The arcade card lobby does not follow the host 🟡 (root-caused & fixed 2026-09-11; needs a three-machine playtest)

**Symptom (owner report).** Four, reported together as *"the worst feature in the party
experience"*:

| # | Reported | Root cause |
|---|---|---|
| 1 | A guest in FREESTYLE stays flying when the host opens a card | `HandleEnterFreestyle` closes every modal, fades the screens `CanvasGroup` and engages the input gate, so `NavigateTo` refuses (`FollowHostToArcadeScreen` is a no-op) and `ModalWindowIn` refuses to open. The open ran to completion and drew nothing. |
| 2 | A guest who missed the card *"can never see the card again"* | Every path that turns the replicated lobby into a modal is an EDGE. With the host sitting in the card no further change was coming; `OnEnable`'s replay never re-ran (`ModalWindowOut` fades a CanvasGroup, it never deactivates); and tapping the card registers a game PICK. |
| 3 | The host backs out, picks another card, guests do not move | Only the host's ✕ called `NotifyConfigClosed`. Gamepad B, `ScreenSwitcher.CloseAllModals` (raised on freestyle **entry**) and `ForceCloseImmediate` all ended at `ModalWindowOut`, leaving `_isCommitted` latched with `IsOpen` true — so the next card returned on `CommitConfiguration`'s first line and nothing replicated. |
| 4 | Extra AI avatars pre-fill the lobby, client-side only, never in the game | The roster draws `seats − humans = AI`, and the two peers answered *humans* differently: the host from Netcode's connected clients, a guest from `HostConnectionDataSO.PartyMembers` (presence poll, 3s, usually 1 on a guest). |

**The shape.** B13 made the lobby replicated STATE, which fixed every peer that was not
*listening*. All four of these are peers that could not *act on it* — so state was necessary and
not sufficient. **A value being replicated does not make it delivered.**

**Fix.**

- **(1)** `ScreenSwitcher.RequestExitFreestyle(onExited)` toggles the guest out and runs the caller
  on `OnMenuStateTransitionEnd` — never the start event, which fires another `CloseAllModals` that
  would close whatever was opened. `HandleConfigOpenedOnClient` defers through it.
- **(2)** `ArcadeGameConfigureModal.ReconcileClientLobby`, once a second: which lobby GENERATION
  has this guest actually DRAWN, against the one the host is broadcasting? A mismatch opens the
  card. Same shape as `ScreenSwitcher`'s self-healing input gate. It declines while the guest is
  flying or mid-blend (catching up must never take the ship off someone using it), and a guest can
  no longer dismiss the host's lobby (`AllowGamepadBClose` false in client mode), so it is never
  fighting a deliberate choice.
- **(3)** Two independent ends. The modal notifies on its OWN `OnModalClosed` — the lesson
  `HandleSelfClosed` already carried for the preview teardown, *one subscription instead of one
  rule per caller* — with the launch naming itself (`_launching`) rather than being inferred. And
  the sync guard is keyed on the CARD, so a different one can never be swallowed whatever a future
  close route forgets.
- **(4)** `LobbySnapshot.HumanCount` carries the host's count, republished on join/leave; the
  guest reads it back. The host's own read moves to `SpectatorSession.CountHumanClients`, so a
  spectator stops silently eating an AI seat.

**Verification.** Roslyn syntax-parse clean; all five `Tools/Build` gates clean.
`ArcadeLobbySnapshotTests` (new, unrun) holds every `LobbySnapshot` field inside `Equals` by
reflection — a field missing there never dirties the NetworkVariable, and the omission only shows
on a second machine. **Nothing has been through the Editor.** Needs three machines: host opens a
card with one guest flying and one guest cold-joining, host backs out and picks another.
**MPPM is not that test** — virtual players share one process and one `GameDataSO`, so this
branch's failure mode (a peer that cannot act on replicated state) is exactly the class MPPM
flatters.

**Open / accepted.**
- A guest cannot dismiss the host's lobby, by design. A legitimate "leave this lobby" has to be a
  real request to the host, not a window close, or it is symptom 2 again.
- If `MenuCrystalClickHandler.ToggleTransition` no-ops (already transitioning), the deferred open
  waits for whenever the guest next leaves freestyle. The reconcile is the backstop.
- `RepublishHumanCount` reads the connected clients inside `OnClientDisconnectCallback` and so
  inherits whatever that callback's ordering guarantees are — the same dependency
  `ExpectedHumanCount` already had. A transient over-count costs one AI chip for a tick.
