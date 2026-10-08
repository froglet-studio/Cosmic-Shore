# Branch archive: `claude/fix-friend-party-invites-1NgTo`

_Snapshot 2026-10-08. Index: [README](../README.md)_

- **Last commit:** 2026-04-22 by Claude
- **Unmerged commits:** 2
- **Forked from:** `25e8dbd52` (2026-04-22, fix(party): make invite + accept flow feel instant instead of polled)
- **Tip:** `da5863077`
- **Files touched (5):**
  - `Assets/_Scripts/Controller/Multiplayer/ArcadeConfigSyncManager.cs`
  - `Assets/_Scripts/Controller/Party/HostConnectionService.cs`
  - `Assets/_Scripts/System/SceneLoader.cs`
  - `Assets/_Scripts/UI/Elements/FriendsListPanel.cs`
  - `Assets/_Scripts/UI/Elements/OnlineInfoEntry.cs`

### `54b46a376` — fix(party): collapse mutual invites into a single shared party

_Claude, 2026-04-22 18:04:43 +0000_

```text
When A invites B at the same time B invites A, the old code left two
parallel host sessions with both players pointing at empty parties.
Add a deterministic two-stage resolver so the click pair always ends in
a single shared party:

1. Send-time short-circuit (SendInviteAsync). Before writing our own
   invite properties, refresh the presence lobby and check whether the
   intended target has already set invite_target to our id. If so,
   convert the click into an Accept of their invite — skip creating a
   second party entirely.

2. Refresh-time tie-breaker (RefreshAsync). When the polling scan
   detects an incoming invite from a player we have an outgoing invite
   to (a true simultaneous race, where neither side saw the other's
   property when they clicked), apply lower-PlayerId-wins-as-host:
   - Lower local id: suppress their popup; keep our host role. The
     other side hits the same rule, sees they are higher, and
     auto-accepts our invite, so both converge into our party.
   - Higher local id: suppress the popup, clear our stale outgoing
     invite, and auto-accept theirs.

ResolveMutualInviteAsync drives the cleanup + Accept handoff outside
the lobby mutex so the Netcode shutdown/Relay-client transition doesn't
block the refresh loop. _lastFiredInvite/_lastInviteResolved are
re-seeded after RequestClearInviteAsync so the invite we are busy
accepting doesn't re-raise OnInviteReceived mid-flight.
```

```text
 Assets/_Scripts/Controller/Party/HostConnectionService.cs | 149 ++++++++++++++++++++++++++++++++++++++++++--
 1 file changed, 143 insertions(+), 6 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 150 of 209 lines)</summary>

```diff
diff --git a/Assets/_Scripts/Controller/Party/HostConnectionService.cs b/Assets/_Scripts/Controller/Party/HostConnectionService.cs
index 074437423..ee1585ea6 100644
--- a/Assets/_Scripts/Controller/Party/HostConnectionService.cs
+++ b/Assets/_Scripts/Controller/Party/HostConnectionService.cs
@@ -478,6 +478,8 @@ namespace CosmicShore.Gameplay
             while (_lobbyBusy)
                 await Task.Yield();
             _lobbyBusy = true;
+
+            PartyInviteData? mutualInviteToAccept = null;
             try
             {
                 SyncLocalIdentity();
@@ -485,6 +487,24 @@ namespace CosmicShore.Gameplay
                     $"[INVITE-SEND] LocalPlayerId: {connectionData.LocalPlayerId}, " +
                     $"DisplayName: {connectionData.LocalDisplayName}", Color.cyan);
 
+                // Pre-send mutual-invite check: if the target already invited us,
+                // there's no reason to start a second parallel session — both
+                // players would then be hosts of empty parties pointing at each
+                // other. Collapse the two clicks into a single accept of their
+                // invite so both players end up in the same lobby.
+                try { await _presenceLobby.RefreshAsync(); }
+                catch { /* non-fatal — we'll still attempt with whatever we have cached */ }
+
+                if (TryFindIncomingInviteFrom(targetPlayerId, out var incomingInvite))
+                {
+                    DebugExtensions.LogColored(
+                        $"[INVITE-SEND] Target '{targetPlayerId}' has already invited us — " +
+                        "converting this send into an Accept of their invite.",
+                        Color.cyan);
+                    mutualInviteToAccept = incomingInvite;
+                    return;
+                }
+
                 if (_partySession == null)
                 {
                     DebugExtensions.LogWarningColored(
@@ -501,12 +521,6 @@ namespace CosmicShore.Gameplay
                     $"[INVITE-SEND] Setting properties — invite_target: '{targetPlayerId}', " +
                     $"invite_data: '{inviteData}'", Color.cyan);
 
-                // Best-effort refresh to sync SDK player list cache before setting
-                // properties. Without this, SaveCurrentPlayerDataAsync can fail
-                // silently if the SDK's internal player index is stale.
-                try { await _presenceLobby.RefreshAsync(); }
-                catch { /* non-fatal — SaveWithRetryAsync handles stale state */ }
-
                 _presenceLobby.CurrentPlayer.SetProperty(INVITE_TARGET_KEY,
                     new PlayerProperty(targetPlayerId, VisibilityPropertyOptions.Public));
                 _presenceLobby.CurrentPlayer.SetProperty(INVITE_DATA_KEY,
@@ -552,6 +566,14 @@ namespace CosmicShore.Gameplay
 
                 _lobbyBusy = false;
             }
+
+            // Resolved outside the mutex: AcceptInviteAsync shuts down our
+            // NetworkManager and starts a Relay client. Leaving _lobbyBusy
+            // held across that transition would block the refresh loop for
+            // the duration of the shutdown + connect (~1s), and the accept
+            // flow does its own presence operations through this service.
+            if (mutualInviteToAccept.HasValue)
+                await ResolveMutualInviteAsync(mutualInviteToAccept.Value);
         }
 
         public async Task AcceptInviteAsync(PartyInviteData invite)
@@ -1042,6 +1064,7 @@ namespace CosmicShore.Gameplay
 
             _lobbyBusy = true;
             bool shouldReconnect = false;
+            PartyInviteData? mutualInviteToAccept = null;
             try
             {
                 await _presenceLobby.RefreshAsync();
@@ -1085,6 +1108,45 @@ namespace CosmicShore.Gameplay
                                 continue;
                             }
 
+                            // Mutual-invite race: both players clicked "invite" on each
+                            // other before either refresh detected the other's invite.
+                            // We have an outgoing invite to THIS sender and now see
+                            // their invite to us. Resolve deterministically so both
+                            // clients converge into a single party (the lower PlayerId
+                            // keeps their host role, the other auto-accepts).
+                            bool isMutualRace =
+                                !string.IsNullOrEmpty(_currentInviteTargetId) &&
+                                _currentInviteTargetId == p.Id;
+                            if (isMutualRace)
+                            {
+                                int cmp = string.CompareOrdinal(connectionData.LocalPlayerId, p.Id);
+                                if (cmp < 0)
+                                {
+                                    // Our PlayerId is lower — we stay as host. Suppress
+                                    // the popup; the other side will auto-accept ours
+                                    // on their next refresh and end up in our party.
+                                    _lastFiredInvite = invite;
+                                    _lastInviteResolved = true;
+                                    DebugExtensions.LogColored(
+                                        $"[INVITE-RECV] Mutual invite detected with '{p.Id}' — " +
+                                        "we are host (lower id), suppressing their invite.",
+                                        Color.yellow);
+                                    continue;
+                                }
+
+                                // Our PlayerId is higher — we become the client. Skip
+                                // the popup entirely and schedule an auto-accept of
+                                // their invite once the lobby mutex is released.
+                                _lastFiredInvite = invite;
+                                _lastInviteResolved = true;
+                                mutualInviteToAccept = invite;
+                                DebugExtensions.LogColored(
+                                    $"[INVITE-RECV] Mutual invite detected with '{p.Id}' — " +
+                                    "we are client (higher id), auto-accepting their invite.",
+                                    Color.yellow);
+                                continue;
+                            }
+
                             bool isDuplicate = _lastFiredInvite.HasValue &&
                                 _lastFiredInvite.Value.PartySessionId == invite.Value.PartySessionId;
 
@@ -1162,6 +1224,12 @@ namespace CosmicShore.Gameplay
             // Reconnect outside the try/finally so _lobbyBusy is released first.
             if (shouldReconnect)
                 await JoinPresenceLobbyAsync();
+
+            // Auto-accept a mutual-invite race winner outside the mutex too —
+            // ResolveMutualInviteAsync drives the full Netcode shutdown + Relay
+            // join path, which acquires _lobbyBusy itself.
+            if (mutualInviteToAccept.HasValue)
+                await ResolveMutualInviteAsync(mutualInviteToAccept.Value);
         }
 
         /// <summary>
@@ -1645,6 +1713,75 @@ namespace CosmicShore.Gameplay
             }
         }
 
+        /// <summary>
+        /// Scans the presence lobby for a player with id <paramref name="senderId"/>
+        /// whose <c>invite_target</c> points at the local player, and returns the
+        /// parsed invite payload if found. Used by <see cref="SendInviteAsync"/> to
+        /// collapse a mutual-invite click pair into a single accept, and callable
+        /// from any context that already has fresh presence data.
+        /// </summary>
+        private bool TryFindIncomingInviteFrom(string senderId, out PartyInviteData invite)
+        {
+            invite = default;
+            if (_presenceLobby == null || string.IsNullOrEmpty(senderId)) return false;
+            if (string.IsNullOrEmpty(connectionData?.LocalPlayerId)) return false;
+
```

</details>

### `da5863077` — fix(party): unstick pending invites and keep clients with the host on start

_Claude, 2026-04-22 18:14:12 +0000_

```text
Two related reliability fixes:

1. Pending-invite row no longer sticks forever

When UGS silently drops an invite save (most common in 3+ player parties
under rate-limit pressure), the sender's row was left pulsing on
"PENDING REQUEST" with no way to retry.

- OnlineInfoEntry now drives a 30s countdown ("PENDING REQUEST 30s" →
  "0s") while the invite is in flight. On expiry it resets the row
  and invokes an onInviteExpired callback.
- FriendsListPanel wires that callback to clear its outgoing
  bookkeeping AND call HostConnectionService.CancelOutgoingInviteAsync,
  which blanks the invite_target / invite_data presence properties so
  the recipient stops seeing a ghost invite on their next refresh.
- HandleOnlinePlayerRemoved now also cancels the outgoing invite when
  the target drops from the presence lobby, so the row clears
  immediately instead of waiting for the timer.

2. Host no longer plays alone when clients fail to load the game

- SceneLoader.waitBeforeLoading dropped from 0.5s to 0.1s so the
  pre-scene-swap window (during which a client's Relay transport can
  silently die) is minimized.
- NetworkSceneManager.OnSceneEvent is watched for the current load:
  any client that doesn't report LoadComplete before
  LoadEventCompleted — or before a 12s hard deadline — is logged by
  id. This surfaces the exact clients left behind instead of silently
  letting the host play solo.
- ArcadeConfigSyncManager now subscribes to OnClientDisconnectCallback
  during the ready-up flow. If a client drops mid-flow the expected
  human count shrinks and the ready gate re-evaluates, so the host
  can still launch instead of waiting indefinitely for a confirmation
  that will never arrive.
```

```text
 Assets/_Scripts/Controller/Multiplayer/ArcadeConfigSyncManager.cs | 55 +++++++++++++++++++++
 Assets/_Scripts/Controller/Party/HostConnectionService.cs         | 29 +++++++++++
 Assets/_Scripts/System/SceneLoader.cs                             | 95 ++++++++++++++++++++++++++++++++++++-
 Assets/_Scripts/UI/Elements/FriendsListPanel.cs                   | 31 +++++++++++-
 Assets/_Scripts/UI/Elements/OnlineInfoEntry.cs                    | 93 ++++++++++++++++++++++++++++++++----
 5 files changed, 292 insertions(+), 11 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 150 of 481 lines)</summary>

```diff
diff --git a/Assets/_Scripts/Controller/Multiplayer/ArcadeConfigSyncManager.cs b/Assets/_Scripts/Controller/Multiplayer/ArcadeConfigSyncManager.cs
index 71c4a7b87..717b0b8f6 100644
--- a/Assets/_Scripts/Controller/Multiplayer/ArcadeConfigSyncManager.cs
+++ b/Assets/_Scripts/Controller/Multiplayer/ArcadeConfigSyncManager.cs
@@ -28,6 +28,7 @@ namespace CosmicShore.Gameplay
 
         readonly HashSet<ulong> _readyClients = new();
         int _expectedHumanCount;
+        bool _disconnectCallbackWired;
 
         /// <summary>
         /// Raised on clients when the host opens the arcade config modal.
@@ -79,9 +80,61 @@ namespace CosmicShore.Gameplay
             // to guard against stale PartyMembers data.
             _expectedHumanCount = Mathf.Max(humanCount, NetworkManager.Singleton.ConnectedClientsIds.Count);
 
+            WireDisconnectCallback();
             OpenConfigOnClients_ClientRpc(gameMode, intensity, playerCount, maxPlayers);
         }
 
+        /// <summary>
+        /// Subscribes (once) to Netcode disconnect events so a client dropping
+        /// during the ready flow doesn't leave the host waiting forever for a
+        /// confirmation that'll never come. On disconnect we shrink the
+        /// expected count and re-evaluate the ready gate.
+        /// </summary>
+        void WireDisconnectCallback()
+        {
+            if (_disconnectCallbackWired) return;
+            var nm = NetworkManager.Singleton;
+            if (nm == null) return;
+            nm.OnClientDisconnectCallback += HandleClientDisconnected;
+            _disconnectCallbackWired = true;
+        }
+
+        void UnwireDisconnectCallback()
+        {
+            if (!_disconnectCallbackWired) return;
+            var nm = NetworkManager.Singleton;
+            if (nm != null)
+                nm.OnClientDisconnectCallback -= HandleClientDisconnected;
+            _disconnectCallbackWired = false;
+        }
+
+        void HandleClientDisconnected(ulong clientId)
+        {
+            if (!IsServer) return;
+            if (_expectedHumanCount <= 0) return;
+
+            _readyClients.Remove(clientId);
+            _expectedHumanCount = Mathf.Max(1, _expectedHumanCount - 1);
+
+            Debug.Log($"[ArcadeConfigSync] Client {clientId} disconnected during ready flow — " +
+                      $"expected count shrunk to {_expectedHumanCount}, ready={_readyClients.Count}");
+
+            SyncReadyCount_ClientRpc(_readyClients.Count, _expectedHumanCount);
+
+            // A disconnection might have been the last blocker. Re-evaluate.
+            if (_readyClients.Count >= _expectedHumanCount && _readyClients.Count > 0)
+            {
+                Debug.Log("[ArcadeConfigSync] Ready threshold met after disconnect — launching game");
+                AllPlayersReady_ClientRpc();
+            }
+        }
+
+        public override void OnNetworkDespawn()
+        {
+            UnwireDisconnectCallback();
+            base.OnNetworkDespawn();
+        }
+
         /// <summary>
         /// Called by ArcadeGameConfigureModal on the host when the modal closes
         /// (back button or cancel — NOT game start).
@@ -90,6 +143,8 @@ namespace CosmicShore.Gameplay
         {
             if (!IsServer) return;
             _readyClients.Clear();
+            _expectedHumanCount = 0;
+            UnwireDisconnectCallback();
             CloseConfigOnClients_ClientRpc();
         }
 
diff --git a/Assets/_Scripts/Controller/Party/HostConnectionService.cs b/Assets/_Scripts/Controller/Party/HostConnectionService.cs
index ee1585ea6..4bfbd0fad 100644
--- a/Assets/_Scripts/Controller/Party/HostConnectionService.cs
+++ b/Assets/_Scripts/Controller/Party/HostConnectionService.cs
@@ -692,6 +692,35 @@ namespace CosmicShore.Gameplay
             await RequestClearInviteAsync();
         }
 
+        /// <summary>
+        /// Cancels the local player's outgoing party invite. Clears the
+        /// <c>invite_target</c> / <c>invite_data</c> presence properties and
+        /// resets <see cref="_currentInviteTargetId"/>, so the row on the
+        /// sender side can return to a clean state and the recipient stops
+        /// seeing a stale invite on their next presence poll.
+        ///
+        /// The <paramref name="targetPlayerId"/> is a best-effort consistency
+        /// check: if we are already targeting someone else (race with a fresh
+        /// invite click), skip the clear so we don't nuke an in-flight invite
+        /// the UI doesn't know about. Pass null to force-clear unconditionally.
+        ///
+        /// Safe to call from UI code; honors the existing lobby mutex.
+        /// </summary>
+        public async Task CancelOutgoingInviteAsync(string targetPlayerId = null)
+        {
+            if (_presenceLobby == null) return;
+
+            if (targetPlayerId != null &&
+                !string.IsNullOrEmpty(_currentInviteTargetId) &&
+                _currentInviteTargetId != targetPlayerId)
+            {
+                return;
+            }
+
+            _currentInviteTargetId = null;
+            await RequestClearInviteAsync();
+        }
+
         /// <summary>
         /// Leaves the current party and returns the local player to Menu_Main.
         /// Delegates to <see cref="PartyInviteController"/> which handles the
diff --git a/Assets/_Scripts/System/SceneLoader.cs b/Assets/_Scripts/System/SceneLoader.cs
index 6ad843f0e..05a44f0f5 100644
--- a/Assets/_Scripts/System/SceneLoader.cs
+++ b/Assets/_Scripts/System/SceneLoader.cs
@@ -1,4 +1,5 @@
 using System;
+using System.Collections.Generic;
 using CosmicShore.Data;
 using CosmicShore.Gameplay;
 using CosmicShore.Utility;
@@ -30,7 +31,19 @@ namespace CosmicShore.Core
     /// </summary>
     public class SceneLoader : MonoBehaviour
     {
-        [SerializeField] float waitBeforeLoading = 0.5f;
+        [Tooltip("Pre-load delay giving clients a tick to process the fade-to-black SOAP event " +
+                 "before the scene swaps. Kept small on purpose — every millisecond here is a " +
+                 "window where a client's Relay transport can silently drop, and if we load the " +
+                 "scene after they've gone we end up with the 'host plays alone' symptom.")]
+        [SerializeField] float waitBeforeLoading = 0.1f;
+
+        [Tooltip("Seconds the server will wait for every connected client to confirm they " +
+                 "finished loading the game scene before logging a warning. The scene flow " +
+                 "still proceeds regardless — this only surfaces the 'host plays alone' " +
+                 "symptom with a concrete list of clients that never arrived.")]
+        [SerializeField] float clientSceneLoadTimeoutSeconds = 12f;
+
```

</details>
