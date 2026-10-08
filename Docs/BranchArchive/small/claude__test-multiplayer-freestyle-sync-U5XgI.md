# Branch archive: `claude/test-multiplayer-freestyle-sync-U5XgI`

_Snapshot 2026-10-08. Index: [README](../README.md)_

- **Last commit:** 2026-02-26 by Claude
- **Unmerged commits:** 2
- **Forked from:** `438283015` (2026-02-26, Merge pull request #159 from froglet-studio/claude/test-scene-transitions-QSEq)
- **Tip:** `164a0e69d`
- **Files touched (3):**
  - `Assets/_Scripts/Game/Arcade/MultiplayerMiniGameControllerBase.cs`
  - `Assets/_Scripts/Game/Multiplayer/MultiplayerSetup.cs`
  - `Assets/_Scripts/Game/Party/PartyGameLauncher.cs`

### `69720f90b` — fix(multiplayer): sync multiplayer freestyle with project architecture

_Claude, 2026-02-26 03:02:14 +0000_

```text
- Replace bare Debug.Log/LogWarning/LogError with CSDebug in
  PartyGameLauncher to ensure log stripping in release builds
- Add null-guard for networkManager in MultiplayerSetup OnEnable/OnDisable
  to prevent NRE when NetworkManager is missing from the scene
- Add CancellationToken to UniTask.Delay calls in
  MultiplayerMiniGameControllerBase (InitializeAfterDelay and
  ResetServerRoundAfterDelay) so async operations respect object lifecycle
```

```text
 Assets/_Scripts/Game/Arcade/MultiplayerMiniGameControllerBase.cs | 11 ++++++-----
 Assets/_Scripts/Game/Multiplayer/MultiplayerSetup.cs             |  2 ++
 Assets/_Scripts/Game/Party/PartyGameLauncher.cs                  |  9 +++++----
 3 files changed, 13 insertions(+), 9 deletions(-)
```

<details><summary>Patch (code/doc/text files)</summary>

```diff
diff --git a/Assets/_Scripts/Game/Arcade/MultiplayerMiniGameControllerBase.cs b/Assets/_Scripts/Game/Arcade/MultiplayerMiniGameControllerBase.cs
index a338c9f06..32b5702d6 100644
--- a/Assets/_Scripts/Game/Arcade/MultiplayerMiniGameControllerBase.cs
+++ b/Assets/_Scripts/Game/Arcade/MultiplayerMiniGameControllerBase.cs
@@ -84,13 +84,14 @@ namespace CosmicShore.Game.Arcade
         {
             try
             {
-                await UniTask.Delay(InitDelayMs, DelayType.UnscaledDeltaTime);
-                
+                await UniTask.Delay(InitDelayMs, DelayType.UnscaledDeltaTime,
+                    cancellationToken: this.GetCancellationTokenOnDestroy());
+
                 gameData.InitializeGame();
-                
+
                 if (!IsServer)
                     return;
-                
+
                 SetupNewRound();
             }
             catch (OperationCanceledException)
@@ -285,7 +286,7 @@ namespace CosmicShore.Game.Arcade
 
         async UniTaskVoid ResetServerRoundAfterDelay()
         {
-            await UniTask.Delay(100); 
+            await UniTask.Delay(100, cancellationToken: this.GetCancellationTokenOnDestroy());
             SetupNewRound();
         }
 
diff --git a/Assets/_Scripts/Game/Multiplayer/MultiplayerSetup.cs b/Assets/_Scripts/Game/Multiplayer/MultiplayerSetup.cs
index d5ae2b90f..0143e364c 100644
--- a/Assets/_Scripts/Game/Multiplayer/MultiplayerSetup.cs
+++ b/Assets/_Scripts/Game/Multiplayer/MultiplayerSetup.cs
@@ -40,6 +40,7 @@ namespace CosmicShore.Game.Multiplayer
         private void OnEnable()
         {
             authenticationData.OnSignedIn.OnRaised += OnAuthenticationSignedIn;
+            if (networkManager == null) return;
             networkManager.ConnectionApprovalCallback += OnConnectionApprovalCallback;
             networkManager.OnClientDisconnectCallback += OnClientDisconnect;
             networkManager.OnTransportFailure         += OnTransportFailure;
@@ -48,6 +49,7 @@ namespace CosmicShore.Game.Multiplayer
         private void OnDisable()
         {
             authenticationData.OnSignedIn.OnRaised -= OnAuthenticationSignedIn;
+            if (networkManager == null) return;
             networkManager.ConnectionApprovalCallback -= OnConnectionApprovalCallback;
             networkManager.OnClientDisconnectCallback -= OnClientDisconnect;
             networkManager.OnTransportFailure         -= OnTransportFailure;
diff --git a/Assets/_Scripts/Game/Party/PartyGameLauncher.cs b/Assets/_Scripts/Game/Party/PartyGameLauncher.cs
index bc7625b83..894e709f9 100644
--- a/Assets/_Scripts/Game/Party/PartyGameLauncher.cs
+++ b/Assets/_Scripts/Game/Party/PartyGameLauncher.cs
@@ -2,6 +2,7 @@ using System.Collections.Generic;
 using System.Threading.Tasks;
 using CosmicShore.Models.Enums;
 using CosmicShore.Utility.DataContainers;
+using CosmicShore.Utility.Recording;
 using Obvious.Soap;
 using UnityEngine;
 
@@ -44,14 +45,14 @@ namespace CosmicShore.Game.Party
         {
             if (!connectionData.IsHost)
             {
-                Debug.LogWarning("[PartyGameLauncher] Only the host can launch a game.");
+                CSDebug.LogWarning("[PartyGameLauncher] Only the host can launch a game.");
                 return;
             }
 
             var game = FindGame(GameModes.MultiplayerFreestyle);
             if (game == null)
             {
-                Debug.LogError("[PartyGameLauncher] MultiplayerFreestyle game not found in game list.");
+                CSDebug.LogError("[PartyGameLauncher] MultiplayerFreestyle game not found in game list.");
                 return;
             }
 
@@ -80,7 +81,7 @@ namespace CosmicShore.Game.Party
             onPartyGameReady?.Raise();
 
             // ── Launch ───────────────────────────────────────────────────────
-            Debug.Log($"[PartyGameLauncher] Launching {game.DisplayName}: " +
+            CSDebug.Log($"[PartyGameLauncher] Launching {game.DisplayName}: " +
                       $"players={playerCount}, intensity={intensity}, " +
                       $"humans={connectionData.RemotePartyMemberCount + 1}, ai={aiSlots}");
 
@@ -126,7 +127,7 @@ namespace CosmicShore.Game.Party
             for (int i = remoteIds.Count - 1; i >= 0 && toKick > 0; i--, toKick--)
             {
                 string kickId = remoteIds[i];
-                Debug.Log($"[PartyGameLauncher] Kicking excess member: {kickId}");
+                CSDebug.Log($"[PartyGameLauncher] Kicking excess member: {kickId}");
 
                 if (HostConnectionService.Instance != null)
                     await HostConnectionService.Instance.KickPartyMemberAsync(kickId);
```

</details>

_Also contains 1 unmerged merge commit(s) (branch-sync merges; no authored changes of their own)._
