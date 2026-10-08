# Branch archive: `claude/fix-freestyle-loading-screen-mw4kA`

_Snapshot 2026-10-08. Index: [README](../README.md)_

- **Last commit:** 2026-05-11 by Claude
- **Unmerged commits:** 1
- **Forked from:** `a6c25b9f5` (2026-05-11, rhino sword)
- **Tip:** `b24790958`
- **Files touched (2):**
  - `Assets/_Scripts/Controller/Arcade/SinglePlayerMiniGameControllerBase.cs`
  - `Assets/_Scripts/System/SceneLoader.cs`

### `b24790958` — fix(loading): prevent stuck loading screen when singleplayer Start() throws

_Claude, 2026-05-11 22:30:17 +0000_

```text
Freestyle launched from the arcade was getting stuck on the loading screen
because the singleplayer initialization chain coupled the loading-screen
fade-out to InitializeGame's success.

Root cause: SinglePlayerMiniGameControllerBase.Start() ran
  gameData.InitializeGame();    // synchronous; many subscribers
  gameData.InvokeClientReady();  // raises OnClientReady -> SceneLoader fades out

InitializeGame fires OnInitializeGame synchronously to a list of subscribers
(Cell, MiniGamePlayerSpawnerAdapter, ScoreTracker, ...). If any single
subscriber threw, the exception propagated out of InitializeGame and
InvokeClientReady was never reached. SceneLoader's FadeFromSplashOnReady
listener never fired and the splash overlay stayed opaque -- a permanent
black screen.

Two-layer fix:

1. SinglePlayerMiniGameControllerBase.Start() now wraps InitializeGame and
   SetupNewRound in try/catch and calls InvokeClientReady unconditionally
   between them. Subscriber errors are logged loudly (fail-loud per CLAUDE.md)
   but no longer brick the loading-screen handoff. Also adds
   InvokeSessionStarted so the ApplicationStateMachine transitions
   LoadingGame -> InGame for solo games, matching
   MultiplayerMiniGameControllerBase.InitializeAfterDelay.

2. SceneLoader.LaunchGame() now schedules a watchdog (default 8s,
   inspector-tunable) that force-clears the splash overlay if OnClientReady
   never fires for any reason -- not just exceptions, but also missing
   scene wiring, scenes without a controller, or future regressions. The
   watchdog logs a warning identifying the scene/mode so the underlying
   cause is visible. ReturnToMainMenu cancels the watchdog so it can't
   raise a false alarm during a normal back-to-menu transition.
```

```text
 .../_Scripts/Controller/Arcade/SinglePlayerMiniGameControllerBase.cs  | 33 ++++++++++++++-
 Assets/_Scripts/System/SceneLoader.cs                                 | 71 +++++++++++++++++++++++++++++++++
 2 files changed, 102 insertions(+), 2 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 150 of 174 lines)</summary>

```diff
diff --git a/Assets/_Scripts/Controller/Arcade/SinglePlayerMiniGameControllerBase.cs b/Assets/_Scripts/Controller/Arcade/SinglePlayerMiniGameControllerBase.cs
index 113b0e650..b2ca0f1f3 100644
--- a/Assets/_Scripts/Controller/Arcade/SinglePlayerMiniGameControllerBase.cs
+++ b/Assets/_Scripts/Controller/Arcade/SinglePlayerMiniGameControllerBase.cs
@@ -1,3 +1,4 @@
+using System;
 using UnityEngine;
 using CosmicShore.Utility;
 
@@ -20,9 +21,37 @@ namespace CosmicShore.Gameplay
             gameData.OnMiniGameTurnEnd.OnRaised += EndTurn;
             gameData.OnResetForReplay.OnRaised += OnResetForReplay;
 
-            gameData.InitializeGame();
+            // Decouple OnClientReady (which clears the loading screen) from
+            // InitializeGame. InitializeGame fires OnInitializeGame synchronously
+            // to many subscribers (Cell, MiniGamePlayerSpawnerAdapter, ScoreTracker,
+            // etc); a single subscriber throwing would otherwise prevent
+            // InvokeClientReady from ever being called and leave the loading screen
+            // stuck. Fail loud, but always raise OnClientReady so the fade clears.
+            try
+            {
+                gameData.InitializeGame();
+            }
+            catch (Exception ex)
+            {
+                Debug.LogError($"[SinglePlayerMiniGameBase] InitializeGame threw: {ex}", this);
+            }
+
             gameData.InvokeClientReady();
-            SetupNewRound();
+
+            // Match MultiplayerMiniGameControllerBase.InitializeAfterDelay so the
+            // ApplicationStateMachine transitions LoadingGame → InGame for solo
+            // games too. Without this, AppState would stay at LoadingGame for the
+            // duration of the singleplayer session.
+            gameData.InvokeSessionStarted();
+
+            try
+            {
+                SetupNewRound();
+            }
+            catch (Exception ex)
+            {
+                Debug.LogError($"[SinglePlayerMiniGameBase] SetupNewRound threw: {ex}", this);
+            }
         }
 
         protected virtual void OnDisable()
diff --git a/Assets/_Scripts/System/SceneLoader.cs b/Assets/_Scripts/System/SceneLoader.cs
index f07f7b40b..4aaddd11f 100644
--- a/Assets/_Scripts/System/SceneLoader.cs
+++ b/Assets/_Scripts/System/SceneLoader.cs
@@ -1,4 +1,5 @@
 using System;
+using System.Threading;
 using CosmicShore.Data;
 using CosmicShore.Gameplay;
 using CosmicShore.Utility;
@@ -32,6 +33,12 @@ namespace CosmicShore.Core
     {
         [SerializeField] float waitBeforeLoading = 0.5f;
 
+        [SerializeField, Tooltip("Maximum seconds to wait for OnClientReady after a game scene starts loading. " +
+            "If the game scene's controller never raises OnClientReady (e.g. an exception in its " +
+            "Start chain), the loading overlay is force-cleared after this timeout so the user " +
+            "isn't permanently stuck on a black screen.")]
+        float fadeFromBlackTimeoutSeconds = 8f;
+
         [Header("SOAP Events (wired in Bootstrap inspector)")]
         [SerializeField] ScriptableEventNoParam _onClickToMainMenuButton;
         [SerializeField] ScriptableEventNoParam _onActiveSessionEnd;
@@ -41,6 +48,8 @@ namespace CosmicShore.Core
         [Inject] ApplicationStateMachine _appStateMachine;
         [Inject] SceneTransitionManager _sceneTransitionManager;
 
+        CancellationTokenSource _fadeTimeoutCts;
+
         #region Unity Lifecycle
 
         void OnEnable()
@@ -70,6 +79,8 @@ namespace CosmicShore.Core
         void OnDisable()
         {
             SceneManager.sceneLoaded -= OnSceneLoaded;
+            CancelFadeTimeout();
+
             if (!gameData) return;
 
             gameData.OnLaunchGame.OnRaised -= LaunchGame;
@@ -115,6 +126,13 @@ namespace CosmicShore.Core
             _sceneTransitionManager?.SetFadeImmediate(1f);
             gameData.OnClientReady.OnRaised += FadeFromSplashOnReady;
 
+            // Safety net: if the new scene's controller never raises OnClientReady
+            // (e.g. an exception in its Start chain or a missing scene reference),
+            // force-clear the overlay after a timeout so the player isn't stuck on
+            // a black screen. The handler logs a warning so the underlying cause is
+            // still visible in the console.
+            ScheduleFadeFromBlackTimeout();
+
             var nm = NetworkManager.Singleton;
 
             // In multiplayer, only the server initiates scene loads.
@@ -138,9 +156,57 @@ namespace CosmicShore.Core
         {
             Debug.Log("<color=#FFFFFF><b>[FLOW-8] [SceneLoader] FadeFromSplashOnReady — OnClientReady fired!</b></color>");
             gameData.OnClientReady.OnRaised -= FadeFromSplashOnReady;
+            CancelFadeTimeout();
+            _sceneTransitionManager?.FadeFromBlack().Forget();
+        }
+
+        void ScheduleFadeFromBlackTimeout()
+        {
+            CancelFadeTimeout();
+
+            if (fadeFromBlackTimeoutSeconds <= 0f)
+                return;
+
+            _fadeTimeoutCts = new CancellationTokenSource();
+            FadeFromBlackTimeoutWatchdog(_fadeTimeoutCts.Token).Forget();
+        }
+
+        async UniTaskVoid FadeFromBlackTimeoutWatchdog(CancellationToken ct)
+        {
+            try
+            {
+                await UniTask.Delay(
+                    TimeSpan.FromSeconds(fadeFromBlackTimeoutSeconds),
+                    DelayType.UnscaledDeltaTime,
+                    cancellationToken: ct);
+            }
+            catch (OperationCanceledException)
+            {
+                return;
+            }
+
+            // OnClientReady never fired. Unhook our subscription, log loudly so
+            // the root cause is visible, and fade the overlay to transparent so
+            // the player can see the loaded scene instead of staring at a black
+            // screen.
+            if (gameData != null && gameData.OnClientReady != null)
+                gameData.OnClientReady.OnRaised -= FadeFromSplashOnReady;
+
+            Debug.LogWarning($"[SceneLoader] OnClientReady was not raised within " +
+                $"{fadeFromBlackTimeoutSeconds:0.0}s of LaunchGame. Force-clearing the " +
+                $"loading overlay. Scene='{gameData?.SceneName}', Mode={gameData?.GameMode}.");
+
             _sceneTransitionManager?.FadeFromBlack().Forget();
         }
```

</details>
