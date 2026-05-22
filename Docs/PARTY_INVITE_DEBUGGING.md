# Party Invite — Outstanding Bugs

**Branch:** `claude/fix-party-invite-bugs-BDrFO` (Bug B fixed; Bug A surgically mitigated; structural fix queued)
**Status:** threading cascade resolved (see `Docs/THREADING.md`). Surgical fixes for Bug A + Bug B shipped. Structural lazy-session refactor still pending.
**Audience:** next session picking up the lazy party-session refactor (`CLAUDE.md` → "Pending Critical Refactors" #1).

---

## 1. Where things stand

The threading cascade (UGS continuations resuming on the .NET ThreadPool → SOAP raises
running off-thread → `EnsureRunningOnMainThread` crashes) is **fixed** as of commit
`6a544e30e`. Every UGS / Netcode `await` now goes through `.AsMainThread()`, which marshals
through Unity's own `SynchronizationContext` via `MainThreadDispatcher`. See
`Docs/THREADING.md` for the contract and the history of attempts.

Both follow-up bugs in the party invite flow now have surgical fixes shipped on
`claude/fix-party-invite-bugs-BDrFO`:

- **Bug A** (host vessel despawn on invite send) — surgical mitigation in `edfa1be`:
  `RefreshPartyMembersAsync` no longer clears the session on transient refresh failures,
  breaking the most common path into the `ClearSession → CreateOwnPartySessionAsync →
  ShutdownAsync → NetworkManager.Shutdown` cascade. The structural fix (lazy party-session
  creation, `CLAUDE.md` → "Pending Critical Refactors" #1) still removes the offending
  shutdown entirely and remains the recommended long-term fix.
- **Bug B** (joining client stuck on splash) — fixed in `a197efb`: extracted
  `SceneLoader.ArmSplashFadeOnNextClientReady()` and call it from
  `PartyInviteController.AcceptInviteAsync` so the `OnClientReady` → fade-out subscription
  exists on the joining client (which never sees a `Menu_Main` `OnSceneLoaded` because
  Netcode just syncs NetworkObjects against the host's already-loaded scene).

Sections §2 and §3 below describe the chains as they existed pre-fix and remain useful
context for the lazy refactor — both bugs trace back to the same root issue (eager
party-session creation + shutdown-and-recreate dance) that the lazy refactor eliminates.

---

## 2. Bug A — Host vessel despawns when "+ → invite" is pressed

### 2.1 Symptom

Host is in `Menu_Main` with their own autopilot vessel spawned and visible. They press "+"
on an empty party slot to open the online-players panel and send an invite. At some point
in the flow their own vessel `OnNetworkDespawn`s and disappears from the screen.

### 2.2 Suspected root cause

`HostConnectionService.CreateOwnPartySessionAsync` (`HostConnectionService.cs:724–765`)
**unconditionally** calls `_networkTransition.ShutdownAsync(...)` (line 740) before recreating
the Relay session. `_networkTransition.ShutdownAsync` calls `NetworkManager.Shutdown()`, which
despawns every `NetworkBehaviour` including the host's own Player + Vessel.

The race-guard added in commit `4d7ce98c5` lives in `SendInviteAsync`
(`HostConnectionService.cs:408–414`):

```csharp
if (_partySessionService.ActiveSession == null)
{
    await CreateOwnPartySessionAsync();
}
```

This guard is correct: if `ActiveSession` is non-null, we skip recreation. **But:**

- The check at line 408 runs **outside** `_sessionCreationMutex` (acquired inside
  `CreateOwnPartySessionAsync` at line 731).
- Between the null check and any mutex acquisition, another caller — or a stale
  `ActiveSession == null` from a transient refresh-failure path — could trigger recreation.
- Even more likely: there's another path that calls `CreateOwnPartySessionAsync` /
  `CreatePartySessionCoreAsync` during the invite flow. Find every call site.

### 2.3 Investigation checklist for next session

1. **Confirm the despawn timing.** Add a temporary `Debug.Log` with stack capture in
   `Player.OnNetworkDespawn` (`Player.cs:250`) and a matching log in
   `NetworkTransitionService.ShutdownAsync`. Press "+" with the host vessel visible and read
   the stack: is `ShutdownAsync` actually being called?

2. **Find every caller of `CreateOwnPartySessionAsync` and `CreatePartySessionCoreAsync`.**
   ```bash
   grep -rn "CreateOwnPartySessionAsync\|CreatePartySessionCoreAsync" Assets/_Scripts/
   ```
   Known callers as of `6a544e30e`:
   - `SendInviteAsync` (`HostConnectionService.cs:413`) — race-guarded, should skip if session exists.
   - `AcceptInviteAsync` (`HostConnectionService.cs:518`) — only when invite has no session ID.
   - `LeavePartyAsync` (`HostConnectionService.cs:584`) — after leaving party.
   - `EnsureInitializedAsync` (`HostConnectionService.cs` near `Start`) — at startup.
   - `RetryCreateOwnPartySessionAsync` — user-triggered retry.

3. **Verify `ActiveSession` doesn't transiently flip to null.** The refresh loop
   (`HostConnectionService.RefreshAsync` ~line 782) reads from the UGS SDK; if a refresh fails
   it may `ClearSession()` somewhere. Grep:
   ```bash
   grep -rn "ClearSession\|ActiveSession = null" Assets/_Scripts/Controller/Party/
   ```

4. **Consider the lazy-creation refactor.** `CLAUDE.md` already calls this out as the
   highest-leverage refactor: don't create a Relay-backed session at startup at all.
   Only create one when the first invite is sent. The accept flow becomes
   `JoinSessionByIdAsync` directly — no shutdown-and-recreate dance. See
   `CLAUDE.md` → "Pending Critical Refactors (next session)" item 1.

   The structural fix probably solves Bug A by removing the offending shutdown entirely.
   Whether to do the surgical fix (guard `CreatePartySessionCoreAsync` against double-shutdown)
   or the structural fix (lazy creation) is a judgement call. The structural fix also
   removes the source of many other bugs that have been papered over (`b74a311c`,
   `b5f13ca7`, `HOST_CONFLICT_MAX_RETRIES`, etc.).

### 2.4 Files

| File | Role |
|---|---|
| `Assets/_Scripts/Controller/Party/HostConnectionService.cs` | `SendInviteAsync` (381–493), `CreateOwnPartySessionAsync` (724–765), `CreatePartySessionCoreAsync` (690–715). |
| `Assets/_Scripts/Controller/Party/Services/NetworkTransitionService.cs` | `ShutdownAsync` — calls `NetworkManager.Shutdown()`. |
| `Assets/_Scripts/Controller/Player/Player.cs` | `OnNetworkDespawn` (~line 250). |
| `Assets/_Scripts/UI/Elements/PartyAreaPanel.cs` / `Assets/_Scripts/UI/Views/PartyArcadeView.cs` | `OnAddSlotPressed` — entry to the flow. |
| `Assets/_Scripts/UI/Views/FriendsListPanel.cs` / `OnlinePlayersPanel.cs` | `OnInviteClicked` — calls `SendInviteAsync`. |

---

## 3. Bug B — Client stuck on splash after accepting invite

### 3.1 Symptom

VP-A invites VP-B. VP-B accepts. VP-B's Netcode join succeeds (host shows the new client),
but VP-B's screen stays on the black splash overlay indefinitely. The fade never clears.

### 3.2 Architecture: who is responsible for fading the splash back?

The splash overlay's alpha is driven by `SceneTransitionManager`. Two paths bring it from
`alpha = 1f` (opaque) back to `0f`:

1. **`SceneLoader.FadeFromSplashOnReady`** (in `SceneLoader.cs`) — subscribes to
   `gameData.OnClientReady`. When that SOAP event fires, calls
   `_sceneTransitionManager.FadeFromBlack()`.

2. **`SceneTransitionManager.LoadSceneAsync`** — the explicit local-scene-load path. Drives
   its own fade-out / load / fade-in sequence. Not used on a network-driven scene sync.

So on a joining client, the fade is gated on `gameData.OnClientReady` firing.
`OnClientReady` is raised by `ClientPlayerVesselInitializer.InitializePair`
(`ClientPlayerVesselInitializer.cs:286–290`):

```csharp
if (player.IsLocalUser)
{
    Debug.Log("[FLOW-6] [ClientVesselInit] Raising OnClientReady (local player initialized)");
    gameData.InvokeClientReady();
}
```

For VP-B (the joining client), VP-B's own Player object IS owned by VP-B on VP-B's machine,
so `player.IsLocalUser` should be `true` and `InvokeClientReady` should fire. **In theory.**

### 3.3 Suspected root causes (ranked by likelihood)

1. **`InitializePair` is never called for VP-B's player on VP-B's machine.**
   The host calls `ClientPlayerVesselInitializer.InitializePlayerAndVessel` directly server-side,
   then RPCs `InitializeNewPlayerAndVessel_ClientRpc` to existing clients and
   `InitializeAllPlayersAndVessels_ClientRpc` to the new client. If the RPC arrives before the
   Player + Vessel NetworkObjects replicate, it gets queued in `_pendingPairs` and is supposed
   to resolve when the SOAP `OnPlayerNetworkSpawnedUlong` / `OnVesselNetworkSpawned` events
   fire. **Verify the resolution path on the joining client side.** Add logs:
   ```csharp
   Debug.Log($"[FLOW-6] Queued pair resolved: playerNetId={playerNetId}, vesselNetId={vesselNetId}");
   ```
   in `ClientPlayerVesselInitializer.ProcessPendingPairs`.

2. **`WaitForSceneSyncAsync` times out.** `PartyInviteController.AcceptInviteAsync`
   (`PartyInviteController.cs:176`) awaits `_networkTransition.WaitForSceneSyncAsync("Menu_Main", ...)`.
   It listens for `nm.SceneManager.OnLoadEventCompleted` with `LoadSceneMode.Single`. But the
   host **doesn't trigger a scene load** when a client joins a session where the scene is
   already loaded — Netcode just synchronises NetworkObjects. So this await times out after 5s
   and continues. That's a soft failure; flow proceeds. But it means VP-B's `OnSceneLoaded`
   handler in `SceneLoader` doesn't run for `Menu_Main` post-join. **Check whether
   `SceneLoader.FadeFromSplashOnReady` is still subscribed to `OnClientReady` after the
   shutdown-and-rejoin sequence.** It should be — `SceneLoader` is `DontDestroyOnLoad` — but
   verify with a `Debug.Log` in the subscription path.

3. **VP-B's `LocalPlayer` is stale.** `_networkTransition.ClearStaleReferences`
   (`PartyInviteController.cs:152`) is supposed to clear out `gameData.LocalPlayer`,
   `gameData.Vessels`, etc. left behind by the pre-shutdown solo session. If anything
   downstream (`InitializePair` → `gameData.AddPlayer`) thinks the local player is already
   set, the local-user check might mis-fire. Read `ClearStaleReferences` and confirm what
   it clears.

4. **Threading is still wrong somewhere.** The threading cascade is fixed for every UGS
   path, but if any new code added after `6a544e30e` `await`s a `Task` without
   `.AsMainThread()`, the canary will fire. **Read the console on the joining client.** If
   `[SceneTransitionManager] SetFadeImmediate called off main thread` appears, that's the
   line to fix.

5. **`FadeFromBlack` is called but no-ops.** `SetFadeImmediate(1f)` is set at the top of
   `AcceptInviteAsync`. If `FadeFromBlack` doesn't reach alpha 0 (e.g., because the
   `CanvasGroup` is null or because the coroutine is killed), the overlay stays opaque.
   Drop a `Debug.Log` in `SceneTransitionManager.FadeFromBlack` start AND end.

### 3.4 Investigation checklist for next session

1. **Instrument the client side.** Add log lines at:
   - `PartyInviteController.AcceptInviteAsync` start, after each step, end.
   - `HostConnectionService.AcceptInviteAsync` start, after JoinByIdAsync, end.
   - `ClientPlayerVesselInitializer.OnReceiveAllPairs_ClientRpc` / `OnReceiveNewPair_ClientRpc`.
   - `ClientPlayerVesselInitializer.ProcessPendingPairs` — pre and post.
   - `ClientPlayerVesselInitializer.InitializePair` — pre and post the `OnClientReady` raise.
   - `SceneLoader.FadeFromSplashOnReady` subscription + invocation.
   - `SceneTransitionManager.FadeFromBlack` start + end.

2. **Run a two-VP MPPM scenario.** VP-A invites VP-B. Read VP-B's console:
   - Does the `[FLOW-6] InitializePair` log fire for VP-B's own player?
   - Does the `OnClientReady` raise fire?
   - Does `FadeFromSplashOnReady` execute?
   - Does `FadeFromBlack` start and complete?

   The first log that **doesn't** appear marks the break.

3. **Fallback: synthesise the fade clearance.** If `OnClientReady` is genuinely the wrong
   trigger for a Relay-join scenario, add a secondary fade-from-black trigger on
   `OnPartyJoinCompleted` (raised at `PartyInviteController.cs:183`). That event fires when
   the accept flow is fully done. It's not as clean as `OnClientReady`, but it would unstick
   the splash if the InitializePair chain has a more complex issue.

### 3.5 Files

| File | Role |
|---|---|
| `Assets/_Scripts/Controller/Party/PartyInviteController.cs` | `AcceptInviteAsync` (~114–214). |
| `Assets/_Scripts/Controller/Party/HostConnectionService.cs` | `AcceptInviteAsync` (495–548). |
| `Assets/_Scripts/Controller/Party/Services/NetworkTransitionService.cs` | `WaitForClientConnectionAsync`, `WaitForSceneSyncAsync`, `ClearStaleReferences`, `ShutdownAsync`. |
| `Assets/_Scripts/Controller/Multiplayer/ClientPlayerVesselInitializer.cs` | `InitializePair` (263–291), `ProcessPendingPairs`, the two ClientRpcs. |
| `Assets/_Scripts/System/SceneLoader.cs` | `FadeFromSplashOnReady`, the `OnClientReady` subscription. |
| `Assets/_Scripts/System/Bootstrap/SceneTransitionManager.cs` | `FadeFromBlack`, `SetFadeImmediate`, the canary. |
| `Assets/_Scripts/Controller/Player/Player.cs` | `IsLocalUser` definition (~line 121). |

---

## 4. UGS-related quirks we already know about

Recording these here so the next session doesn't burn time re-discovering them.

| Quirk | Where you hit it | Mitigation |
|---|---|---|
| UGS Multiplayer SDK creates a Relay session even for a solo "party of one" → eagerly burns a Relay allocation + UGS session for every authenticated user | `HostConnectionService.EnsureInitializedAsync` / `CreatePartySessionAsync` | Lazy creation — see CLAUDE.md "Pending Critical Refactors". |
| `MultiplayerService.Instance.CreateSessionAsync(opts.WithRelayNetwork())` internally calls `NetworkManager.StartHost()` — if NM is already host, it fails with a host-conflict exception | `PartySessionService.CreateAsync` | Caller must `ShutdownAsync` first. Race-condition retry loop is `HOST_CONFLICT_MAX_RETRIES`. |
| UGS rate-limits property writes to ~1/s per client | `LobbyPropertyWriter.SaveWithRetryAsync` | Exponential back-off retry: 3 retries × 2s base. |
| UGS `LobbyPatcher` raises `ArgumentOutOfRangeException` when the SDK's player-index cache is stale | `LobbyPatcherLogFilter` in `HostConnectionService` | Refresh-before-save pattern in `LobbyPropertyWriter`. Filter swallows the noise. |
| UGS Friends SDK's `PresenceUpdated` callback fires on the SDK's HTTP pump thread | `FriendsServiceFacade.OnPresenceUpdated` | `SyncAllRelationships()` writes only to SOAP lists, which are inert containers; the SOAP `OnFriendAdded` etc. raises happen on the main thread because the methods that *mutate* relationships call `.AsMainThread()` on every SDK await. **If we ever raise SOAP events directly from `OnPresenceUpdated`, wrap.** |
| `nm.SceneManager.LoadScene("Menu_Main", LoadSceneMode.Single)` does **not** fire a scene-load event on clients whose scene is already loaded | `NetworkTransitionService.WaitForSceneSyncAsync` | Soft timeout (5s) → returns false → flow continues. Bug B may be related. |
| `Application.isPlaying` access inside UGS SDK call paths requires the main thread (this was the original `EnsureRunningOnMainThread` reproducer) | UGS SDK internals — not our code | Every UGS `await` uses `.AsMainThread()`. |

---

## 5. Verification scenarios

When investigating either bug, these are the scenarios to reproduce against:

### 5.1 Solo host (no invites)

1. Launch host (no MPPM clones).
2. Wait through splash → auth → Menu_Main.
3. Expect: vessel spawns and autopilots in `Menu_Main`. Splash overlay fades to clear.
   No `[SceneTransitionManager] SetFadeImmediate called off main thread` warning.
   No `EnsureRunningOnMainThread` exception.

### 5.2 Host sends invite (Bug A scenario)

1. Launch host. Wait for `Menu_Main` ready.
2. Press "+" on an empty party slot.
3. Click "+ Invite" on a target player in the online-players panel.
4. **Expect:** host's vessel stays spawned and visible. Invite appears in the target's UI.
5. **Bug A:** host's vessel disappears at some point in steps 2–3.

### 5.3 Two-VP invite/accept (Bug B scenario)

1. Launch VP-A as host. Wait for Menu_Main.
2. Launch VP-B as MPPM clone. Wait for VP-B's Menu_Main.
3. VP-A presses "+", picks VP-B, sends invite.
4. VP-B sees invite, accepts.
5. **Expect:** VP-B fades to black → joins VP-A's session → fade clears → both vessels visible
   on both clients.
6. **Bug B:** VP-B stays on black splash overlay indefinitely after step 4.

---

## 6. Where to start

If you're picking this up cold, start with **Bug B**. Reason: it's tractable with a single
log instrumentation pass (§3.4 step 1) and points you at exactly which lifecycle hook is
mis-firing. Bug A is more architectural — solving it well likely means doing the lazy
party-session refactor from `CLAUDE.md`'s "Pending Critical Refactors", which is a day of work
on its own.

The threading work in this branch (`6a544e30e`) is shipped and stable. Don't undo it.
