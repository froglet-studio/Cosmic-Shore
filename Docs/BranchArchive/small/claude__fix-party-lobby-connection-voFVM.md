# Branch archive: `claude/fix-party-lobby-connection-voFVM`

_Snapshot 2026-10-08. Index: [README](../README.md)_

- **Last commit:** 2026-04-23 by Claude
- **Unmerged commits:** 1
- **Open pull request:** https://github.com/froglet-studio/Cosmic-Shore/pull/499
- **Forked from:** `9714dd753` (2026-04-23, Update Menu_Main.unity)
- **Tip:** `b0eb1024b`
- **Files touched (1):**
  - `Docs/PartySystemAudit.md`

### `b0eb1024b` — Document party system audit for pre-Steam launch

_Claude, 2026-04-23 13:29:07 +0000_

```text
Captures root cause of the invite-accepted-but-not-joined bug (missing
InvokeSessionStarted + StartClient in MultiplayerSetup.JoinSessionAsClientById)
plus prioritized fix list covering connection approval, readiness gating,
disconnect/reconnect handling, and observability gaps.
```

```text
 Docs/PartySystemAudit.md | 121 +++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++
 1 file changed, 121 insertions(+)
```

<details><summary>Patch (code/doc/text files)</summary>

```diff
diff --git a/Docs/PartySystemAudit.md b/Docs/PartySystemAudit.md
new file mode 100644
index 000000000..4970981c4
--- /dev/null
+++ b/Docs/PartySystemAudit.md
@@ -0,0 +1,121 @@
+# Party System Audit — Pre-Steam Launch
+
+Status: documentation only. Changes will be implemented in follow-up passes.
+
+Stack: UGS Lobby (session/matchmaking) + Netcode for GameObjects (in-game) + UGS Relay (transport).
+
+---
+
+## 1. Critical Bug — Client never connects to host's lobby after accepting invite
+
+**File:** `Assets/_Scripts/Game/Multiplayer/MultiplayerSetup.cs`
+
+**Symptom:** Client accepts invite, host sees client's profile in the `+` invite slot (UGS session membership updated), but client never loads into host's freestyle lobby.
+
+**Root cause:** `JoinSessionAsClientById()` (lines 180–191) sets `gameData.ActiveSession` but:
+- Does **not** call `InvokeSessionStarted()` (host path does this at line 175).
+- Does **not** start the Netcode client (no `networkManager.StartClient()` after Relay is wired via `.WithRelayNetwork()`).
+
+**Fix:**
+- After `JoinSessionByIdAsync()` returns, call `gameData.InvokeSessionStarted()` and `networkManager.StartClient()`.
+- Await Relay allocation join before starting the client; fail fast with a user-facing error if either step times out.
+
+---
+
+## 2. Downstream state stuck in limbo
+
+Because the client-side `OnSessionStarted` never fires and Netcode never connects:
+
+- `Assets/_Scripts/Game/Arcade/MultiplayerMiniGameControllerBase.cs:29,49` — subscribers waiting on `OnSessionStarted` are never notified on the client.
+- `Assets/_Scripts/Game/Multiplayer/ClientPlayerVesselInitializer.cs:27–35` — waits on `gameData.Players` forever (no timeout, no retry, no error surface).
+- `Assets/_Scripts/Game/Multiplayer/ServerPlayerVesselInitializer.cs:75–76` — server vessel spawn only runs on Netcode spawn, which never occurs for this client.
+
+**Fix:**
+- Add explicit timeouts (e.g., 10s) with cancellation + user-facing "connection failed, retry?" UI.
+- Route both host and client through a single `SessionReady` event once UGS + Netcode + Players-populated are all true.
+
+---
+
+## 3. Connection approval is wide-open
+
+**File:** `MultiplayerSetup.cs:214–222`
+
+`OnConnectionApprovalCallback` unconditionally sets `response.Approved = true`. No check that the connecting client is a member of the UGS session.
+
+**Fix:**
+- Pass the UGS player id / join code in the `ConnectionData` payload.
+- Validate against `ActiveSession.Players` before approving. Reject with a clear reason code on mismatch.
+- Required for Steam launch — prevents uninvited joins and trivial spoofing.
+
+---
+
+## 4. Session state set before network is live
+
+**File:** `Assets/_Scripts/Utility/DataContainers/GameDataSO.cs:76`
+
+`ActiveSession` becomes non-null the instant UGS returns, before Netcode handshake completes. UI and gameplay code using `ActiveSession != null` as "we're in the lobby" read a false positive.
+
+**Fix:**
+- Split into two flags: `SessionJoined` (UGS) and `NetworkReady` (NGO connected + spawned). Gate lobby UI and launch on `NetworkReady`.
+
+---
+
+## 5. Launch-before-ready race
+
+**File:** `Assets/_Scripts/UI/Menus/ArcadeGameConfigureModal.cs:446–450`
+
+Host can start the match before all invited clients have completed Netcode handshake. No pre-launch readiness check.
+
+**Fix:**
+- Per-player "ready" state synced over the lobby. Start button disabled until all session members report `NetworkReady`.
+- Host-side kick + retry option for members stuck > N seconds.
+
+---
+
+## 6. No disconnect / reconnect / migration story
+
+Currently unhandled — all are table-stakes for a Steam release:
+
+- **Client disconnect mid-lobby:** no cleanup of session seat, stale profile stays in `+` slot.
+- **Client disconnect mid-game:** no rejoin path.
+- **Host disconnect:** no host migration; party dies silently.
+- **Invite spam / expired invites:** no TTL, no rate limit.
+
+**Fix (prioritized for launch):**
+1. Heartbeat + `OnClientDisconnectCallback` → free the lobby seat, notify host UI.
+2. Invite TTL (e.g., 60s) + single-use tokens.
+3. Graceful "host left — return to menu" flow (host migration can be post-launch).
+4. Reconnect-to-lobby within a short grace window using cached session id.
+
+---
+
+## 7. Observability gaps
+
+Hard to diagnose player bug reports without logs.
+
+**Fix:**
+- Structured logging around every state transition in `MultiplayerSetup` (join requested / UGS joined / Relay allocated / Netcode connected / spawned).
+- Surface the last failure reason to the player instead of a silent hang.
+
+---
+
+## Suggested Order of Work
+
+1. **#1 + #2** — unblock the actual reported bug. Single PR.
+2. **#4** — split session vs network ready flags. Prereq for clean #5.
+3. **#5** — host launch gating.
+4. **#3** — connection approval validation. Security-sensitive, needs QA.
+5. **#6.1 + #6.2 + #6.3** — disconnect handling + invite TTL + host-left flow.
+6. **#7** — logging pass. Do last so the new paths are what's instrumented.
+7. **#6.4** (reconnect) and full host migration — post-launch if time is short.
+
+---
+
+## Key Files (reference)
+
+- `Assets/_Scripts/Game/Multiplayer/MultiplayerSetup.cs`
+- `Assets/_Scripts/Game/Multiplayer/ClientPlayerVesselInitializer.cs`
+- `Assets/_Scripts/Game/Multiplayer/ServerPlayerVesselInitializer.cs`
+- `Assets/_Scripts/Game/Arcade/MultiplayerMiniGameControllerBase.cs`
+- `Assets/_Scripts/Utility/DataContainers/GameDataSO.cs`
+- `Assets/_Scripts/UI/Menus/ArcadeGameConfigureModal.cs`
```

</details>
