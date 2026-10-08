# Branch archive: `claude/add-party-invite-panel-K5pah`

_Snapshot 2026-10-08. Index: [README](../README.md)_

- **Last commit:** 2026-02-28 by Claude
- **Unmerged commits:** 1
- **Forked from:** `bad3295bf` (2026-02-28, Merge pull request #274 from froglet-studio/claude/configure-camera-switching-)
- **Tip:** `b39fd8af9`
- **Files touched (4):**
  - `Assets/_Scripts/Controller/Party/HostConnectionService.cs`
  - `Assets/_Scripts/Tests/EditMode/HostConnectionDataSOTests.cs`
  - `Assets/_Scripts/UI/Views/InviteNotificationUI.cs`
  - `Assets/_Scripts/Utility/DataContainers/HostConnectionDataSO.cs`

### `b39fd8af9` — feat(party): implement network transition for party invite accept flow

_Claude, 2026-02-28 06:37:13 +0000_

```text
- Fix presence lobby to work without relay (remove WithRelayNetwork) so
  it no longer conflicts with the existing local NetworkManager host.
  The lobby now uses UGS sessions purely for player discovery and invite
  coordination via session properties.

- Add NetworkManager transition to CreatePartySessionAsync (host side):
  shuts down local host, creates relay-backed party session, reloads
  Menu_Main via Netcode scene manager so clients can connect.

- Add NetworkManager transition to AcceptInviteAsync (client side):
  shuts down local host, joins relay party session as client, Menu_Main
  loads automatically via Netcode scene sync. Host's
  MenuServerPlayerVesselInitializer spawns joiner's vessel with autopilot.

- Add OnNetworkTransitioning / OnNetworkTransitionComplete SOAP events
  to HostConnectionDataSO for UI loading state awareness.

- Update InviteNotificationUI to disable buttons and show "Joining..."
  during the async network transition.

- Add RecoverLocalHostAsync for graceful fallback if transition fails.

- Add tests for new HostConnectionDataSO network transition fields.
```

```text
 Assets/_Scripts/Controller/Party/HostConnectionService.cs      | 197 +++++++++++++++++++++++++++++++++------
 Assets/_Scripts/Tests/EditMode/HostConnectionDataSOTests.cs    |  17 ++++
 Assets/_Scripts/UI/Views/InviteNotificationUI.cs               |  24 ++++-
 Assets/_Scripts/Utility/DataContainers/HostConnectionDataSO.cs |  11 +++
 4 files changed, 222 insertions(+), 27 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 150 of 445 lines)</summary>

```diff
diff --git a/Assets/_Scripts/Controller/Party/HostConnectionService.cs b/Assets/_Scripts/Controller/Party/HostConnectionService.cs
index 886bd81bc..e8c2902d1 100644
--- a/Assets/_Scripts/Controller/Party/HostConnectionService.cs
+++ b/Assets/_Scripts/Controller/Party/HostConnectionService.cs
@@ -6,15 +6,24 @@ using CosmicShore.Utility;
 using Cysharp.Threading.Tasks;
 using Obvious.Soap;
 using Reflex.Attributes;
+using Unity.Netcode;
 using Unity.Services.Multiplayer;
 using UnityEngine;
+using UnityEngine.SceneManagement;
 using CosmicShore.ScriptableObjects;
+
 namespace CosmicShore.Gameplay
 {
     /// <summary>
     /// Single-responsibility service that establishes and maintains the host
     /// connection (presence lobby) at the main-menu stage.
     ///
+    /// The presence lobby is a non-relay UGS session used purely for player
+    /// discovery and invite coordination via session properties.
+    ///
+    /// Party sessions use relay networking so the accepting player can shut
+    /// down their local host and join the inviter's NetworkManager as a client.
+    ///
     /// Writes all state into <see cref="HostConnectionDataSO"/> so every UI
     /// consumer can react via SOAP events / lists without coupling to this class.
     /// </summary>
@@ -39,6 +48,7 @@ namespace CosmicShore.Gameplay
         [SerializeField] private float refreshIntervalSeconds = 3f;
 
         [Inject] private PlayerDataService playerDataService;
+        [Inject] private SceneNameListSO sceneNames;
 
         // ─────────────────────────────────────────────────────────────────────
         // Static access
@@ -56,6 +66,7 @@ namespace CosmicShore.Gameplay
         private bool _initialized;
         private bool _joining;
         private bool _leaving;
+        private bool _transitioning;
         private PartyInviteData? _lastFiredInvite;
 
         private const string PRESENCE_LOBBY_GAME_MODE = "PRESENCE_LOBBY";
@@ -86,7 +97,7 @@ namespace CosmicShore.Gameplay
 
         void Update()
         {
-            if (!_initialized || _presenceLobby == null) return;
+            if (!_initialized || _presenceLobby == null || _transitioning) return;
 
             _refreshTimer += Time.deltaTime;
             if (_refreshTimer >= refreshIntervalSeconds)
@@ -144,6 +155,12 @@ namespace CosmicShore.Gameplay
                 return;
             }
 
+            if (_transitioning)
+            {
+                Debug.LogWarning("[HostConnectionService] Cannot send invite — network transition in progress.");
+                return;
+            }
+
             try
             {
                 SyncLocalIdentity();
@@ -176,31 +193,71 @@ namespace CosmicShore.Gameplay
             }
         }
 
+        /// <summary>
+        /// Accepts an incoming party invite: shuts down the local NetworkManager host,
+        /// joins the inviter's relay-backed party session as a client, and lets Netcode
+        /// scene sync load Menu_Main from the host.
+        /// The host's <see cref="MenuServerPlayerVesselInitializer"/> will spawn the
+        /// joiner's vessel with autopilot automatically.
+        /// </summary>
         public async Task AcceptInviteAsync(PartyInviteData invite)
         {
+            if (_transitioning)
+            {
+                Debug.LogWarning("[HostConnectionService] Cannot accept invite — network transition in progress.");
+                return;
+            }
+
+            connectionData.OnNetworkTransitioning?.Raise();
+            _transitioning = true;
+
             try
             {
                 SyncLocalIdentity();
 
+                var nm = NetworkManager.Singleton;
+
+                // Shutdown existing local NetworkManager host so we can
+                // restart as a client connected to the inviter's relay host.
+                if (nm != null && nm.IsListening)
+                {
+                    nm.Shutdown();
+                    await UniTask.WaitUntil(() => !nm.IsListening);
+                }
+
+                // Join the relay-backed party session — UGS Multiplayer
+                // automatically configures transport and starts NetworkManager
+                // as client via WithRelayNetwork on the session.
                 _partySession = await MultiplayerService.Instance.JoinSessionByIdAsync(
                     invite.PartySessionId,
                     new JoinSessionOptions { PlayerProperties = BuildLocalPlayerProperties() });
 
                 connectionData.IsHost = false;
 
-                // Add self + host to party members
+                // Update party members
                 connectionData.PartyMembers?.Clear();
                 connectionData.PartyMembers?.Add(connectionData.LocalPlayerData);
                 var hostData = new PartyPlayerData(invite.HostPlayerId, invite.HostDisplayName, invite.HostAvatarId);
                 connectionData.PartyMembers?.Add(hostData);
                 connectionData.OnPartyMemberJoined?.Raise(hostData);
 
-                Debug.Log($"[HostConnectionService] Joined party {_partySession.Id}");
+                // Menu_Main loads automatically via Netcode scene sync from host.
+                // The host's MenuServerPlayerVesselInitializer will spawn our
+                // vessel with autopilot when our Player object is created via
+                // connection approval.
+
+                Debug.Log($"[HostConnectionService] Accepted invite — joined party {_partySession.Id} as client");
                 await RequestClearInviteAsync();
             }
             catch (Exception e)
             {
                 Debug.LogWarning($"[HostConnectionService] AcceptInvite error: {e.Message}");
+                await RecoverLocalHostAsync();
+            }
+            finally
+            {
+                _transitioning = false;
+                connectionData.OnNetworkTransitionComplete?.Raise();
             }
         }
 
@@ -260,6 +317,12 @@ namespace CosmicShore.Gameplay
 
         public ISession PartySession => _partySession;
 
+        /// <summary>
+        /// True while the service is transitioning the NetworkManager
+        /// (shutting down local host and restarting with relay).
+        /// </summary>
+        public bool IsTransitioning => _transitioning;
```

</details>
