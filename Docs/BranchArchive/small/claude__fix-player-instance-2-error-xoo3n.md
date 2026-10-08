# Branch archive: `claude/fix-player-instance-2-error-xoo3n`

_Snapshot 2026-10-08. Index: [README](../README.md)_

- **Last commit:** 2026-02-28 by Claude
- **Unmerged commits:** 1
- **Forked from:** `2a1f08e5f` (2026-02-28, Merge pull request #285 from froglet-studio/claude/fix-camera-crystal-target-h)
- **Tip:** `40a3dbace`
- **Files touched (2):**
  - `Assets/_Scripts/Controller/Multiplayer/MultiplayerSetup.cs`
  - `Assets/_Scripts/System/AuthenticationSceneController.cs`

### `40a3dbace` — fix(multiplayer): resolve port conflict and duplicate callback for multi-instance testing

_Claude, 2026-02-28 12:01:18 +0000_

```text
When running two player instances on the same machine, Instance 2 failed
because both tried to bind port 7777. The transport failure then cascaded
into a duplicate ConnectionApprovalCallback registration, causing the auth
scene to fall back to direct scene load (breaking player spawning).

- Add port retry loop (ports 7777–7786) in MultiplayerSetup.EnsureHostStarted()
- Suppress OnTransportFailure during port retry to prevent premature shutdown
- Remove duplicate ConnectionApprovalCallback registration from AuthenticationSceneController
- Add matching port retry in AuthenticationSceneController fallback path
```

```text
 Assets/_Scripts/Controller/Multiplayer/MultiplayerSetup.cs | 54 ++++++++++++++++++++++++++++++++++++++++++--
 Assets/_Scripts/System/AuthenticationSceneController.cs    | 53 ++++++++++++++++++++++++++++++-------------
 2 files changed, 89 insertions(+), 18 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 150 of 174 lines)</summary>

```diff
diff --git a/Assets/_Scripts/Controller/Multiplayer/MultiplayerSetup.cs b/Assets/_Scripts/Controller/Multiplayer/MultiplayerSetup.cs
index e92a71526..2a0cfb84f 100644
--- a/Assets/_Scripts/Controller/Multiplayer/MultiplayerSetup.cs
+++ b/Assets/_Scripts/Controller/Multiplayer/MultiplayerSetup.cs
@@ -4,6 +4,7 @@ using System.Collections.Generic;
 using UnityEngine;
 using Cysharp.Threading.Tasks;
 using Unity.Netcode;
+using Unity.Netcode.Transports.UTP;
 using Unity.Services.Authentication;
 using Unity.Services.Multiplayer;
 using CosmicShore.Utility;
@@ -25,6 +26,7 @@ namespace CosmicShore.Gameplay
 
         private NetworkManager networkManager;
         private bool _hostStartInProgress;
+        private bool _suppressTransportFailure;
 
         private void Start()
         {
@@ -83,6 +85,8 @@ namespace CosmicShore.Gameplay
         /// starts the host exactly once. The NetworkManager lives in the
         /// Bootstrap scene as DontDestroyOnLoad and must already exist.
         /// Subsequent calls are no-ops while the host is already listening.
+        /// If the default port is already in use (e.g. another editor instance),
+        /// retries on incrementing ports.
         /// </summary>
         void EnsureHostStarted()
         {
@@ -123,8 +127,50 @@ namespace CosmicShore.Gameplay
                     return;
                 }
 
-                CSDebug.Log("[MultiplayerSetup] Starting as Host.");
-                nm.StartHost();
+                var transport = nm.GetComponent<UnityTransport>();
+                if (transport == null)
+                {
+                    CSDebug.LogError("[MultiplayerSetup] UnityTransport not found on NetworkManager.");
+                    return;
+                }
+
+                ushort basePort = transport.ConnectionData.Port;
+                const int maxPortAttempts = 10;
+
+                // Suppress OnTransportFailure during port retry loop so it
+                // doesn't shut down the NetworkManager between attempts.
+                _suppressTransportFailure = true;
+                try
+                {
+                    for (int i = 0; i < maxPortAttempts; i++)
+                    {
+                        ushort port = (ushort)(basePort + i);
+                        if (i > 0)
+                        {
+                            transport.SetConnectionData(
+                                transport.ConnectionData.Address,
+                                port,
+                                transport.ConnectionData.ServerListenAddress);
+                        }
+
+                        CSDebug.Log($"[MultiplayerSetup] Starting as Host on port {port}...");
+                        bool started = nm.StartHost();
+
+                        if (started && nm.IsListening)
+                        {
+                            CSDebug.Log($"[MultiplayerSetup] Host started on port {port}.");
+                            return;
+                        }
+
+                        CSDebug.LogWarning($"[MultiplayerSetup] StartHost failed on port {port}. Trying next port...");
+                    }
+
+                    CSDebug.LogError($"[MultiplayerSetup] Failed to start host after {maxPortAttempts} port attempts ({basePort}–{(ushort)(basePort + maxPortAttempts - 1)}).");
+                }
+                finally
+                {
+                    _suppressTransportFailure = false;
+                }
             }
             finally
             {
@@ -366,6 +412,10 @@ namespace CosmicShore.Gameplay
         // --------------------------
         private async void OnTransportFailure()
         {
+            // During EnsureHostStarted port-retry loop, suppress so the handler
+            // doesn't shut down the NetworkManager between attempts.
+            if (_suppressTransportFailure) return;
+
             try
             {
                 CSDebug.LogWarning("[Net] Transport failure. Recreating session/join…");
diff --git a/Assets/_Scripts/System/AuthenticationSceneController.cs b/Assets/_Scripts/System/AuthenticationSceneController.cs
index 3ee4bb67c..94655db7b 100644
--- a/Assets/_Scripts/System/AuthenticationSceneController.cs
+++ b/Assets/_Scripts/System/AuthenticationSceneController.cs
@@ -8,6 +8,7 @@ using Cysharp.Threading.Tasks;
 using Reflex.Attributes;
 using TMPro;
 using Unity.Netcode;
+using Unity.Netcode.Transports.UTP;
 using Unity.Services.Authentication;
 using UnityEngine;
 using UnityEngine.SceneManagement;
@@ -463,6 +464,8 @@ namespace CosmicShore.Core
         /// First waits for MultiplayerSetup to start the host (via OnSignedIn).
         /// If that doesn't happen within <see cref="networkHostTimeout"/>, starts
         /// the host directly on the existing NetworkManager.
+        /// Does NOT register its own ConnectionApprovalCallback — MultiplayerSetup
+        /// handles that registration during its Start() which runs before this path.
         /// </summary>
         async UniTask EnsureHostStartedAsync(CancellationToken ct)
         {
@@ -503,25 +506,43 @@ namespace CosmicShore.Core
             if (nm.IsListening)
                 return;
 
-            // Register connection approval so the host's player object is created.
-            nm.ConnectionApprovalCallback += OnConnectionApproval;
+            // Try starting the host with port fallback for multi-instance testing.
+            // ConnectionApprovalCallback is already registered by MultiplayerSetup.
+            var transport = nm.GetComponent<UnityTransport>();
+            if (transport == null)
+            {
+                CSDebug.LogError("[AuthScene] UnityTransport not found on NetworkManager.");
+                return;
+            }
 
-            CSDebug.Log("[AuthScene] Starting network host...");
-            nm.StartHost();
+            ushort basePort = transport.ConnectionData.Port;
+            const int maxPortAttempts = 10;
 
-            // Wait one frame for the host to finish starting.
-            await UniTask.Yield(ct);
-        }
+            for (int i = 0; i < maxPortAttempts; i++)
+            {
+                ushort port = (ushort)(basePort + i);
+                if (i > 0)
+                {
+                    transport.SetConnectionData(
+                        transport.ConnectionData.Address,
+                        port,
+                        transport.ConnectionData.ServerListenAddress);
+                }
 
-        static void OnConnectionApproval(
-            NetworkManager.ConnectionApprovalRequest request,
```

</details>
