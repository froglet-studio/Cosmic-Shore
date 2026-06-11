# Party System — Open Bugs

Living tracker for party-side issues found in MPPM testing. Companion
to `ARCHITECTURE.md` (locked design), `REFACTOR.md` (active refactor
queue), and `../NetworkDiagnostics/ARCHITECTURE.md` (catch-block diagnostics).

Presence-lobby-specific bugs (B1, B4, B6 from the old tracker) moved
to `../PresenceSystem/BUGS.md`.

Statuses: 🔴 open · 🟡 investigating · 🟢 fixed (commit) · ⚪ deferred.

| ID | Title | Confidence | Status |
|----|-------|-----------|--------|
| B2 | `ObjectDisposedException` (semaphore) on Play-Mode abort / fast invite-accept | ~95% | 🔴 |
| B3 | TC4 bounce leaves 2 vessels + dead controls | ~85% | 🔴 |
| B5 | TC2/TC4 second joiner fails to join | Uncertain (diagnose first) | 🔴 |
| B7 | Client pair-init runs before remote identity replicates (`InitializePair Player=` empty, vessel-type `Random`) | Verified mostly benign | ⚪ |
| B9 | Host-return: one client's vessel stuck in autopilot drift + party domains not reset to menu (Jade) | Observed (not yet diagnosed) | 🔴 |
| B10 | Spurious "Connection lost. Tap retry." on the joiner's splash during invite-accept (stale party-refresh tick) | ~90% (code-traced) | 🟢 fixed (pending MPPM verify) |

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

**Fix landed (single commit).** `PartyInviteController.LeavePartyAndReturnToMenuAsync`
and `RecoverFromFailedTransitionAsync` both call
`gameData.LocalPlayer.Vessel.DestroyVessel()` between
`LeavePartyKeepHostAsync` (which spawns vessel 4) and the
`LoadScene("Menu_Main")` call. `VesselController.DestroyVessel()` does
`NetworkObject.Despawn(true)` for spawned server-owned vessels — the
exact teardown `GameDataSO.DestroyPlayerAndVessel` already uses
(`VesselController.cs:209-210`, `GameDataSO.cs:195`). After the
despawn the reload proceeds with no scene-survivor: the post-reload
initializer spawns **one** vessel (the equivalent of NetObjId 6), the
Player's `NetVesselId` re-pairs naturally via `OnNetVesselIdChanged`,
input controller wires to the single live vessel, AI seeks crystals
normally.

**Why not the "reorder so reload precedes solo restart" path.** Would
require shutting NM down as a client and using
`UnityEngine.SceneManagement.SceneManager.LoadScene` (not Netcode's),
which is a much larger surface area and crosses state-machine
boundaries. The surgical despawn is the right size for the fix and
keeps the existing flow intact.

**Risk gate accepted.** Single-file +37 lines, only the leave + bounce
paths. Despawn fires `OnNetworkDespawn` like any normal teardown
(paths exercised every game session). `gameData?.LocalPlayer?.Vessel`
null-guarded. If anything regresses, removing the despawn blocks
reverts to current behavior.

**Evidence (file:line).**
- `PartyInviteController.cs:274-340` — `LeavePartyAndReturnToMenuAsync`
  (calls `DestroyPlayerAndVessel` at `:299`, then `LeavePartyKeepHostAsync`
  at `:312`, then `LoadScene` at `:318`).
- `GameDataSO.cs:188` — `DestroyPlayerAndVessel` (clears `Players`,
  destroys vessels).
- `ServerPlayerVesselInitializer.cs:130-145` — `ConnectedClients`
  re-kick of persistent Players after a scene load.
- `ServerPlayerVesselInitializer.cs:188-193` — the NULL early-return.
- `MenuServerPlayerVesselInitializer.cs:40` — `DestroyVesselWithScene=false`.
- `MainMenuController.cs:198,249` — `HandleMenuReady` /
  `ActivateLocalPlayerAutopilot` (fires per `OnClientReady`, twice here).
- `MainMenuCameraController.cs:647` — secondary NRE.
- `ExplosionHelper.cs:79` — secondary `GameDataSO` resolve failure
  (leftover-vessel evidence).

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

## B9 — Host-return: one client's vessel stuck in autopilot drift + party domains not reset to menu (Jade) 🔴

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

**Hypothesis (one shared root — not yet investigated).** The menu's
per-client autopilot **and** domain reset are driven by
`MainMenuController.HandleMenuReady → ActivateLocalPlayerAutopilot()`,
which for the **local/owned** vessel calls `ApplyMenuDomain(player)`
(resets `NetDomain` to Jade), then `StartPlayer()`,
`Vessel.ToggleAIPilot(true)`, `InputController.SetPause(true)`. Non-local
pairs go through `HandlePlayerPairInitialized`, which re-activates
autopilot but does **not** call `ApplyMenuDomain`. If a returning
client's local pair-init / `OnClientReady` doesn't complete cleanly on
the game→menu network reload (suspect a timing/race in
`ClientPlayerVesselInitializer`'s roster-pull on this path, distinct from
the initial party-join path), that client gets **neither** the autopilot
re-activation **nor** the Jade reset — explaining both symptoms at once.
The straight-line drift also points at stale gameplay input/throttle
leaking into the menu without a hard reset.

**For next session.**
- Confirm whether the stuck client ever fired `HandleMenuReady` /
  `ActivateLocalPlayerAutopilot` on return (look for the FLOW-6
  `OnClientReady` + autopilot logs). If not, fix the per-client re-init
  reliability on the game→menu reload.
- Apply the menu-domain (Jade) reset to **every** returning party member,
  not just the locally-owned vessel — likely drive it server-side per
  `Player` in the `Menu_Main` respawn so it can't depend on each client's
  local activation firing.
- Hard-reset vessel input / AI-pilot state on return so no held
  throttle/heading carries from the game into the menu autopilot.

**Repro.** 1 host + 3 MPPM clients → party up in `Menu_Main` → host
launches a domain game (e.g. Skim Race) → play to the scoreboard → host
taps **Main Menu** → watch each client's vessel control + domain color.

**Status.** 🔴 Open — deferred to a future session (logged Session 2).

---

## B10 — Spurious "Connection lost. Tap retry." on the joiner's splash during invite-accept 🟢 (fixed, pending MPPM verify)

**Symptom.** A client accepts a host's invite: the join splash goes
opaque and the boot-status retry button appears with "Connection lost.
Tap retry." even though the join is progressing normally and completes.
The Retry mode then sticks (nothing re-raises a Status/Hide on that
client), invisible while the splash is transparent in the menu, and
resurfaces on the next opaque splash — e.g. the all-players-ready game
launch — looking like "retry button shows on the loading splash".

**Root cause (~90%, code-traced).** `AcceptInviteAsync` (HCS)
deliberately leaves the joiner's own solo Relay session
(`_partySessionService.LeaveAsync()`) before `JoinByIdAsync`. A
party-refresh tick already in flight against that old session
(`RefreshPartyMembersAsync` → `await _partySessionService.RefreshAsync()`)
lands with `SessionDeleted`/`NotInLobby` *after* the join has populated
`PartyMembers` with the host. The catch classified that as
`IsDefiniteSessionGoneException` → `HandleDefiniteSessionGoneAsync` →
`hadRemoteMembers == true` → spurious `OnHostConnectionLost` (rendered as
Retry by `BootStatusBroadcaster`), plus a `ClearSession()` +
`EnsurePartySessionAsync()` recovery racing the just-joined session. The
presence-lobby refresh path (`RefreshAsync` outer catch) has an
`IsTransitioning` guard for exactly this transition-teardown noise; the
party-session path did not.

**Fix (this branch — `claude/zealous-keller-uecdu1`).**
1. `RefreshPartyMembersAsync` captures the session it polled and its
   catch now early-outs when (a) that session is no longer
   `ActiveSession` (stale-tick — the error describes the old session) or
   (b) `PartyInviteController.IsTransitioning` (mirrors the lobby-path
   guard).
2. `BootStatusBroadcaster` policy hardening: connection-lost raises are
   suppressed while a party transition is in flight and during the
   launch window (`OnLaunchGame` → next `OnClientReady`); `OnLaunchGame`
   and `OnPartyJoinCompleted` raise `Hide` so a stale Retry can never
   resurface on a transition splash. Retry remains for idle/boot
   contexts (auth-scene exhaustion, lobby escalation in menu).
3. Runtime self-heal (scene-independent): the panel itself was lost from
   `Bootstrap.unity` once via a script-GUID break (`BootStatusPanel.cs`
   was committed without its `.meta`; a later "Add meta files" commit
   minted a different GUID), leaving the authored retry button
   orphaned-ACTIVE on every splash with nothing driving it — and scene
   merges can silently resurrect that broken state even after the scene
   repair. `BootStatusBroadcaster.EnsureStatusPanelWired` (Awake + Start)
   now recreates the panel at runtime if missing;
   `BootStatusPanel.Bind`/`EnsureWired` auto-find the status text +
   retry button among the splash canvas children, take the outbound
   retry channel from `HostConnectionService.BootStatusRetryRequestedEvent`,
   and force-hide the button on bind regardless of its authored active
   state. `AuthenticationSceneController` also clears its latched Retry
   once the relay session confirms after exhaustion.

**Repro.** 1 host + 1 MPPM client → client accepts invite → watch the
join splash for the retry button; then both ready-up a game and watch
the launch splash.

**Evidence.** `HostConnectionService.cs` (`RefreshPartyMembersAsync`
catch, `HandleDefiniteSessionGoneAsync`, `AcceptInviteAsync` leave→join),
`BootStatusBroadcaster.cs`, `PartyInviteController.AcceptInviteAsync`.

---

## How we work bugs

Method: see `../README.md` § "How we work bugs". Party-side priority
order: **B2 → B5 → B3 → B7** (B8 fixed). The presence-lobby cluster
(B1, B4, B6) is the locked-design area and lives in
`../PresenceSystem/BUGS.md`.
