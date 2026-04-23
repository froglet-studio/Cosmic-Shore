# Party System Audit — Pre-Steam Launch

Status: documentation only. Based on `bleeding-edge` HEAD `9714dd753`. Changes
will be implemented in follow-up passes.

**Stack**
- UGS Multiplayer — two logical sessions (see below).
- UGS Relay — transport for the party session.
- Netcode for GameObjects (NGO) — in-game networking layer.

**Two-level session model**
- **Presence Lobby** — lobby-only UGS session, no Relay. Used for player
  discovery and invite exchange via per-player properties. Coexists with an
  active NetworkManager.
- **Party Session** — UGS session created with `.WithRelayNetwork()`. This is
  the transport the client actually joins after accepting an invite.

---

## 1. Critical Bug — Client accepts invite but never lands in host's lobby

**Symptom**: Client accepts invite → host sees client's profile in the `+`
invite slot (UGS presence/party-session membership updates) → client never
appears in host's freestyle lobby and the client itself is stuck on its
local Menu_Main.

**Root cause lives in the client accept flow** (Menu_Main invite path), not
in the arcade matchmaking path. The accept flow is:

```
PartyInviteController.AcceptInviteAsync            Controller/Party/PartyInviteController.cs:90
  ├─ ShutdownNetworkManagerAsync                   :281
  ├─ HostConnectionService.AcceptInviteAsync       Controller/Party/HostConnectionService.cs:557
  │    └─ MultiplayerService.JoinSessionByIdAsync  :568
  ├─ WaitForClientConnectionAsync                  :319  ← swallows timeout
  └─ connectionData.OnPartyJoinCompleted.Raise()   :147  ← fires even on timeout
```

Two distinct problems combine to produce the reported symptom:

### 1a. `WaitForClientConnectionAsync` silently swallows timeouts

`PartyInviteController.cs:319-338`:

```csharp
await UniTask.WaitUntil(
    () => {
        var nm = NetworkManager.Singleton;
        return nm != null && nm.IsConnectedClient;
    },
    cancellationToken: timeoutCts.Token);
}
catch (OperationCanceledException) when (!ct.IsCancellationRequested)
{
    Debug.LogWarning(
        $"[PartyInviteController] Client connection not confirmed after {connectionTimeoutSeconds}s — proceeding anyway.");
}
```

If NGO never finishes connecting (Relay allocation join fails, transport
hang, approval race, etc.), the wait times out, a warning is logged, and
the flow **continues as if the join succeeded** — `OnPartyJoinCompleted` is
raised at line 147, UI treats the accept as done, and the user has no
signal that anything went wrong.

This is the most likely direct cause of the reported "host sees client,
client doesn't load" symptom: UGS session join succeeded (hence host's
`+` slot update), NGO never connected, the controller pretended everything
was fine.

### 1b. `HostConnectionService.AcceptInviteAsync` does not verify transport

`HostConnectionService.cs:557-605` joins the party session via
`MultiplayerService.JoinSessionByIdAsync` (with `WithRelayNetwork()` since
the session was created that way at `:1440`) and returns as soon as the
UGS call resolves. It does **not** await NGO connection, does **not** fail
if the Relay allocation fails, and swallows any exception into a
`LogWarning` at line 603.

The UGS SDK is expected to auto-configure the NGO transport and start the
client when joining a Relay-backed session, but if that handshake fails —
or if the NetworkManager we just shut down in step 1 hasn't fully released
its transport — the failure surfaces only as the silent timeout in 1a.

### Suggested fix

- Treat the `WaitForClientConnectionAsync` timeout as a failure. Throw and
  fall through to `RecoverFromFailedTransitionAsync()` (already called by
  the `catch (Exception)` block at line 161), which restarts the local
  host so the user isn't stranded.
- Before raising `OnPartyJoinCompleted`, verify `NetworkManager.Singleton`
  is non-null, `IsConnectedClient == true`, and the party session's
  `HostId` matches an expected value.
- Surface a user-visible "could not join party" message so the player
  retries rather than silently reopening the presence lobby.
- In `HostConnectionService.AcceptInviteAsync`, replace
  `catch { LogWarning }` at line 601 with a rethrow so the
  `PartyInviteController` can see the failure and recover.

---

## 2. Connection approval is wide open

**File**: `Controller/Multiplayer/MultiplayerSetup.cs:340-348`

```csharp
private void OnConnectionApprovalCallback(
    NetworkManager.ConnectionApprovalRequest request,
    NetworkManager.ConnectionApprovalResponse response)
{
    response.Approved           = true;
    response.CreatePlayerObject = true;
    response.Position           = Vector3.zero;
    response.Rotation           = Quaternion.identity;
    response.PlayerPrefabHash   = null;
}
```

Every incoming connection is approved unconditionally. No check that the
connecting client is actually a member of the UGS party session, no
authentication token validation, no reject path.

Low-risk today because Relay join codes are scoped to the session, but it
is a trivial uplift target once the game is on Steam.

**Suggested fix**
- Have the client attach `AuthenticationService.Instance.PlayerId` + the
  party session id to `ConnectionData`.
- In the callback, validate against `gameData.ActiveSession.Players` (host
  side) and return a reject reason code on mismatch.
- Rate-limit repeat connection attempts from the same transport address.

---

## 3. `ActiveSession != null` is set before NGO is live

**Files**:
- Host path: `MultiplayerSetup.cs:258-292` (`StartSessionAsHost`)
- Client matchmaking path: `MultiplayerSetup.cs:294-305` (`JoinSessionAsClientById`)
- Client invite path: `HostConnectionService.cs:568` (`_partySession = await JoinSessionByIdAsync...`)
- Storage: `GameDataSO.cs:159` (`public ISession ActiveSession { get; set; }`)

In all three paths `ActiveSession` becomes non-null the instant UGS returns,
before the Relay handshake and NGO spawn finish. Anywhere that reads
`ActiveSession != null` as "we're in the lobby" is looking at a false
positive during that window.

The party-invite path is the worst offender because
`PartyInviteController` stores `gameData.ActiveSession =
HostConnectionService.Instance.PartySession` at line 134 *before* awaiting
NGO connection at line 142.

**Suggested fix**
- Split the state into two flags (or derived properties):
  `SessionJoined` — UGS session membership established.
  `NetworkReady` — NGO `IsConnectedClient || IsHost` and the local
  Player NetworkObject has spawned.
- Gate lobby UI, vessel spawn, and game-launch buttons on `NetworkReady`,
  not on `ActiveSession != null`.

---

## 4. No disconnect / reconnect / host-migration / invite-TTL

Currently minimal handling; all four are table-stakes for Steam.

- `MultiplayerSetup.OnClientDisconnect` (`:350-369`) logs on the host side
  and invokes `OnSessionEnded` on the client side, but does **not**
  actively clean up the seat on the host's lobby roster (`PartyMembers`,
  the `+` slot UI) or invalidate the stale invite row.
- Host disconnect → party dies silently. No "host left" UI, no migration.
- Invites have no TTL and no single-use token. A client can accept a stale
  invite long after the host moved on. `PartyInviteData` is transported
  via per-player presence-lobby properties and cleared by the sender
  manually.
- No reconnect path inside a grace window after a transient drop.

**Suggested fix (prioritized for launch)**
1. On `OnClientDisconnectCallback`: free the lobby seat in
   `HostConnectionDataSO.PartyMembers`, refresh the presence property
   state, surface the drop in UI.
2. Add TTL + single-use token to `PartyInviteData`. Bake the
   `AuthenticationService.PlayerId` of the sender into it and reject
   invites where the presence-lobby player properties no longer match.
3. Detect host disconnect (`gameData.ActiveSession.Deleted` in
   `MultiplayerMiniGameControllerBase.cs:76` already wires this in-game;
   the menu path does not) and show a "host left — returning to menu"
   flow.
4. Optional post-launch: short-window reconnect using the last party
   session id. Full host migration is a deep change and should be
   post-launch.

---

## 5. Observability gaps

Ad-hoc Debug.Log instrumentation exists: roughly 50+ `[FLOW-N]` colored
markers span `MultiplayerSetup`, `ServerPlayerVesselInitializer`,
`MultiplayerMiniGameControllerBase`, and `ArcadeGameConfigureModal`. Two
gaps remain for pre-Steam:

1. `PartyInviteController` and `HostConnectionService` have **zero**
   `[FLOW-N]` markers and only scattered `Debug.Log` / `LogWarning`. The
   accept flow is exactly the code path that produces the reported bug,
   so this is the area where structured, queryable tracing is most
   needed.
2. Every caught exception in `HostConnectionService.AcceptInviteAsync`
   (line 601), `SendInviteAsync`, and the session-create retry loops
   becomes `LogWarning` and is discarded. No telemetry counter, no
   user-visible failure reason.

**Suggested fix**
- Centralize a `PartyLog` helper with structured fields: flow phase,
  session id, player id, elapsed ms.
- Emit at every boundary: invite sent, invite received, accept started,
  local host shutdown, UGS session joined, Relay allocated, NGO
  connected, player object spawned, join completed, timeout / error.
- Keep a rolling last-N-events buffer on `HostConnectionDataSO` that a
  debug panel or crash report can surface.
- Replace each `LogWarning-and-swallow` with either a rethrow or a
  structured error raised to a SOAP event that the UI can bind to.

---

## Items the original audit flagged that are already addressed

For future reference — these claims appeared in the previous draft of this
document and do **not** need new work:

- **`ClientPlayerVesselInitializer` waits on `gameData.Players` forever**.
  Superseded: the file now uses SOAP events
  (`OnPlayerNetworkSpawnedUlong`, `OnVesselNetworkSpawned`) to drive
  pending-pair resolution. The class comment at
  `Controller/Multiplayer/ClientPlayerVesselInitializer.cs:25` explicitly
  states **"zero WaitUntil polling"**.
- **`MultiplayerMiniGameControllerBase` subscribers to `OnSessionStarted`
  are never notified on the client**. Misread: the subscription at
  `Controller/Arcade/MultiplayerMiniGameControllerBase.cs:36` is gated by
  `if (IsServer)` by design — it is server-only session lifecycle
  wiring, not something the client is supposed to receive.
- **Host can launch before clients finish handshake (ArcadeGameConfigureModal)**.
  Mostly addressed: `Controller/Multiplayer/ArcadeConfigSyncManager.cs`
  gates launch on per-player `ConfirmLocalPlayerReady` with server-side
  `_expectedHumanCount` bookkeeping. `OnStartGameClicked` at
  `UI/Modals/ArcadeGameConfigureModal.cs:891` routes through this
  manager when present. Residual: no timeout on stuck-ready clients and
  no host-side kick/retry. Not critical for launch.

---

## Suggested order of work

1. **#1a + #1b** — fix the silent timeout swallow and propagate transport
   errors through the accept flow. This is the reported bug. Single PR.
2. **#3** — split `SessionJoined` vs `NetworkReady`. Prerequisite for any
   UI work that needs a reliable "in lobby" signal.
3. **#4.1 + #4.2** — disconnect seat cleanup and invite TTL. Steam
   requirement.
4. **#2** — connection approval validation. Security-sensitive; needs
   its own QA cycle because it can reject legitimate clients.
5. **#4.3** — host-left UI on menu-stage party disconnect.
6. **#5** — structured logging. Do it last so the new fixes above are
   what's actually instrumented.
7. **#4.4** (reconnect) and full host migration — post-launch.

---

## Key files (reference)

| Role | File |
|---|---|
| Client accept-invite orchestrator | `Assets/_Scripts/Controller/Party/PartyInviteController.cs` |
| UGS session management | `Assets/_Scripts/Controller/Party/HostConnectionService.cs` |
| NGO host + matchmaking | `Assets/_Scripts/Controller/Multiplayer/MultiplayerSetup.cs` |
| Ready gate for game launch | `Assets/_Scripts/Controller/Multiplayer/ArcadeConfigSyncManager.cs` |
| Server vessel spawner | `Assets/_Scripts/Controller/Multiplayer/ServerPlayerVesselInitializer.cs` |
| Client pair initializer | `Assets/_Scripts/Controller/Multiplayer/ClientPlayerVesselInitializer.cs` |
| Multiplayer game controller base | `Assets/_Scripts/Controller/Arcade/MultiplayerMiniGameControllerBase.cs` |
| Shared session/run state | `Assets/_Scripts/Utility/DataContainers/GameDataSO.cs` |
| Party/lobby SOAP container | `Assets/_Scripts/Utility/DataContainers/HostConnectionDataSO.cs` |
| Game launch modal | `Assets/_Scripts/UI/Modals/ArcadeGameConfigureModal.cs` |
