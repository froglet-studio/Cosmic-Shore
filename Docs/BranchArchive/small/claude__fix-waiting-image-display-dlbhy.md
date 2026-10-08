# Branch archive: `claude/fix-waiting-image-display-dlbhy`

_Snapshot 2026-10-08. Index: [README](../README.md)_

- **Last commit:** 2026-03-06 by Claude
- **Unmerged commits:** 1
- **Forked from:** `fbb826bb6` (2026-03-05, Merge pull request #371 from froglet-studio/claude/fix-vessel-prism-reference-)
- **Tip:** `b6563d34f`
- **Files touched (2):**
  - `Assets/_Scripts/System/SceneLoader.cs`
  - `Assets/_Scripts/UI/Modals/ArcadeGameConfigureModal.cs`

### `b6563d34f` — fix(multiplayer): show splash overlay on clients during game scene transition

_Claude, 2026-03-06 02:06:14 +0000_

```text
Clients were not showing the loading screen when the host launched a game,
leaving Menu_Main UI visible until the network scene load completed. Added
splash overlay setup and app state transition to SyncGameConfigToClients_ClientRpc
so clients mirror the host's LaunchGame() preparation.

Also added a debug warning when waitingForOthersLabel is not wired in the
inspector, surfacing the missing reference instead of silently skipping it.
```

```text
 Assets/_Scripts/System/SceneLoader.cs                 | 11 +++++++++++
 Assets/_Scripts/UI/Modals/ArcadeGameConfigureModal.cs |  2 ++
 2 files changed, 13 insertions(+)
```

<details><summary>Patch (code/doc/text files)</summary>

```diff
diff --git a/Assets/_Scripts/System/SceneLoader.cs b/Assets/_Scripts/System/SceneLoader.cs
index b1f0ee8ac..808955db3 100644
--- a/Assets/_Scripts/System/SceneLoader.cs
+++ b/Assets/_Scripts/System/SceneLoader.cs
@@ -319,6 +319,17 @@ namespace CosmicShore.Core
             gameData.SelectedIntensity.Value = intensity;
             gameData.SelectedPlayerCount.Value = playerCount;
             gameData.RequestedAIBackfillCount = aiBackfillCount;
+
+            // Mirror host-side LaunchGame() preparation on the client:
+            // transition app state and show splash overlay to hide Menu_Main UI
+            // during the incoming network scene load.
+            _appStateMachine?.TransitionTo(ApplicationState.LoadingGame);
+            _sceneTransitionManager?.SetFadeImmediate(1f);
+            gameData.OnClientReady.OnRaised += FadeFromSplashOnReady;
+
+            Debug.Log($"<color=#FF8C00>[FLOW-3] [SceneLoader] Client received config sync — " +
+                      $"Scene={sceneName}, Mode={(GameModes)gameMode}, " +
+                      $"AppState→LoadingGame, splash overlay active.</color>");
         }
 
         #endregion
diff --git a/Assets/_Scripts/UI/Modals/ArcadeGameConfigureModal.cs b/Assets/_Scripts/UI/Modals/ArcadeGameConfigureModal.cs
index 95b820991..124278c30 100644
--- a/Assets/_Scripts/UI/Modals/ArcadeGameConfigureModal.cs
+++ b/Assets/_Scripts/UI/Modals/ArcadeGameConfigureModal.cs
@@ -664,6 +664,8 @@ namespace CosmicShore.UI
                 startGameButton.gameObject.SetActive(false);
             if (waitingForOthersLabel)
                 waitingForOthersLabel.SetActive(true);
+            else
+                Debug.LogWarning("[ArcadeConfigModal] waitingForOthersLabel is not assigned in the inspector.");
 
             // Tell the server this player is ready
             if (arcadeConfigSyncManager)
```

</details>
