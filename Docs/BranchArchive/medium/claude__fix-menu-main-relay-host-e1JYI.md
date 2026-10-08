# Branch archive: `claude/fix-menu-main-relay-host-e1JYI`

_Snapshot 2026-10-08. Index: [README](../README.md#index--medium-branches-410-unmerged-commits)_

- **Last commit:** 2026-03-04 by Claude
- **Unmerged commits:** 4
- **Forked from:** `e43b80025` (2026-03-04, Merge pull request #342 from froglet-studio/claude/hex-race-unique-domains-7HB)
- **Tip:** `986fe9d46`
- **Files touched (4):**
  - `Assets/_Scripts/Controller/Multiplayer/MultiplayerSetup.cs`
  - `Assets/_Scripts/Controller/Party/HostConnectionService.cs`
  - `Assets/_Scripts/Controller/Party/PartyInviteController.cs`
  - `Assets/_Scripts/System/AuthenticationSceneController.cs`

### `1017fc925` — fix(multiplayer): start Relay host directly, eliminate local host fallback

_Claude, 2026-03-04 06:03:56 +0000_

```text
Remove the wasteful local-host-then-Relay-host sequence that caused
Menu_Main to load twice. AuthenticationSceneController no longer starts
a local host as fallback — it waits up to 15s for HostConnectionService
to start the Relay host via UGS CreateSessionAsync(WithRelayNetwork).

- AuthenticationSceneController: remove nm.StartHost() fallback and
  OnConnectionApproval callback; increase timeout from 3s to 15s
- HostConnectionService: remove nm.Shutdown() + ReloadMenuSceneIfActive
  conflict-handling in CreatePartySessionAsync; remove dead code
  (HOST_CONFLICT_MAX_RETRIES, IsHostConflictException, ReloadMenuSceneIfActive)
- PartyInviteController: replace local nm.StartHost() in error recovery
  with Relay-based recovery via HostConnectionService.CreatePartySessionPublicAsync
- MultiplayerSetup: update comment to reflect new architecture
```

```text
 Assets/_Scripts/Controller/Multiplayer/MultiplayerSetup.cs |  4 +--
 Assets/_Scripts/Controller/Party/HostConnectionService.cs  | 67 ++++----------------------------------------
 Assets/_Scripts/Controller/Party/PartyInviteController.cs  | 26 ++++++++++++-----
 Assets/_Scripts/System/AuthenticationSceneController.cs    | 56 +++++++++---------------------------
 4 files changed, 40 insertions(+), 113 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 150 of 268 lines)</summary>

```diff
diff --git a/Assets/_Scripts/Controller/Multiplayer/MultiplayerSetup.cs b/Assets/_Scripts/Controller/Multiplayer/MultiplayerSetup.cs
index 1e421007b..e334a2496 100644
--- a/Assets/_Scripts/Controller/Multiplayer/MultiplayerSetup.cs
+++ b/Assets/_Scripts/Controller/Multiplayer/MultiplayerSetup.cs
@@ -167,8 +167,8 @@ namespace CosmicShore.Gameplay
 
                 // Host startup is delegated to HostConnectionService which creates a
                 // Relay-backed party session (via CreateSessionAsync + WithRelayNetwork).
-                // AuthenticationSceneController.EnsureHostStartedAsync provides a local
-                // host fallback if the Relay allocation times out.
+                // AuthenticationSceneController waits for the Relay host with a 15s
+                // timeout; if Relay fails, Menu_Main loads without networking.
                 Debug.Log("<color=#00FFFF>[FLOW-1] [MultiplayerSetup] Callbacks wired. Waiting for HostConnectionService to start Relay host.</color>");
                 CSDebug.Log("[MultiplayerSetup] Callbacks wired. Waiting for HostConnectionService to start Relay host.");
             }
diff --git a/Assets/_Scripts/Controller/Party/HostConnectionService.cs b/Assets/_Scripts/Controller/Party/HostConnectionService.cs
index 797f90fff..3d7a85d54 100644
--- a/Assets/_Scripts/Controller/Party/HostConnectionService.cs
+++ b/Assets/_Scripts/Controller/Party/HostConnectionService.cs
@@ -10,7 +10,6 @@ using Reflex.Attributes;
 using Unity.Netcode;
 using Unity.Services.Multiplayer;
 using UnityEngine;
-using UnityEngine.SceneManagement;
 using CosmicShore.ScriptableObjects;
 namespace CosmicShore.Gameplay
 {
@@ -808,20 +807,10 @@ namespace CosmicShore.Gameplay
         // Party Session
         // ─────────────────────────────────────────────────────────────────────
 
-        private const int HOST_CONFLICT_MAX_RETRIES = 2;
-
-        private static bool IsHostConflictException(Exception e)
-        {
-            return e.Message != null &&
-                   e.Message.Contains("Failed to start NetworkManager component as host");
-        }
-
         private async Task CreatePartySessionAsync()
         {
             if (_partySession != null) return;
 
-            bool hadToShutdown = false;
-
             var opts = new SessionOptions
             {
                 MaxPlayers = connectionData.MaxPartySlots,
@@ -832,46 +821,17 @@ namespace CosmicShore.Gameplay
 
             for (int attempt = 0; ; attempt++)
             {
-                // The UGS Multiplayer SDK calls NetworkManager.StartHost()
-                // internally when creating a Relay-backed session. If a local
-                // host is already running (started by AuthenticationSceneController
-                // as a fallback), that call fails. Shut it down before each attempt.
-                var nm = NetworkManager.Singleton;
-                if (nm != null && nm.IsListening)
-                {
-                    Debug.Log("[HostConnectionService] Shutting down local host before Relay party session creation...");
-                    nm.Shutdown();
-                    hadToShutdown = true;
-
-                    var sw = System.Diagnostics.Stopwatch.StartNew();
-                    while (nm != null && nm.IsListening && sw.ElapsedMilliseconds < 5000)
-                        await Task.Delay(100);
-
-                    // Allow transport cleanup to settle.
-                    await Task.Delay(200);
-                }
-
                 try
                 {
+                    // The UGS Multiplayer SDK calls NetworkManager.StartHost()
+                    // internally when creating a Relay-backed session.
+                    // AuthenticationSceneController waits for nm.IsListening
+                    // before loading Menu_Main — no local host fallback.
                     _partySession = await MultiplayerService.Instance.CreateSessionAsync(opts);
                     connectionData.IsHost = true;
-                    Debug.Log($"[HostConnectionService] Created party session {_partySession.Id}");
-
-                    // If we shut down a running host, reload Menu_Main as a
-                    // network scene so the new Relay host serves it properly
-                    // for clients that accept the invite.
-                    if (hadToShutdown)
-                        ReloadMenuSceneIfActive();
-
+                    Debug.Log($"[HostConnectionService] Created Relay party session {_partySession.Id}");
                     return;
                 }
-                catch (Exception e) when (attempt < HOST_CONFLICT_MAX_RETRIES && IsHostConflictException(e))
-                {
-                    // A local host was started by another system (e.g.
-                    // AuthenticationSceneController) during Relay allocation.
-                    // The next iteration's pre-check will shut it down.
-                    Debug.LogWarning($"[HostConnectionService] Host conflict during Relay session creation — retry {attempt + 1}/{HOST_CONFLICT_MAX_RETRIES}");
-                }
                 catch (Exception e) when (attempt < RATE_LIMIT_MAX_RETRIES && IsRateLimitException(e))
                 {
                     int delay = RATE_LIMIT_BASE_DELAY_MS * (1 << attempt);
@@ -881,23 +841,6 @@ namespace CosmicShore.Gameplay
             }
         }
 
-        /// <summary>
-        /// After transitioning from local host to Relay host, reloads Menu_Main
-        /// via Netcode scene management so it is properly served to joining clients.
-        /// </summary>
-        private void ReloadMenuSceneIfActive()
-        {
-            var nm = NetworkManager.Singleton;
-            if (nm == null || !nm.IsListening || nm.SceneManager == null) return;
-
-            var activeScene = SceneManager.GetActiveScene();
-            if (activeScene.name == "Menu_Main")
-            {
-                Debug.Log("[HostConnectionService] Reloading Menu_Main as network scene (Relay host)...");
-                nm.SceneManager.LoadScene("Menu_Main", LoadSceneMode.Single);
-            }
-        }
-
         /// <summary>
         /// Clears the SENDER's own invite properties after the invited player
         /// joins the party. This stops receivers from seeing stale invite data.
diff --git a/Assets/_Scripts/Controller/Party/PartyInviteController.cs b/Assets/_Scripts/Controller/Party/PartyInviteController.cs
index 295527890..019153a43 100644
--- a/Assets/_Scripts/Controller/Party/PartyInviteController.cs
+++ b/Assets/_Scripts/Controller/Party/PartyInviteController.cs
@@ -279,27 +279,39 @@ namespace CosmicShore.Gameplay
         // ─────────────────────────────────────────────────────────────────────
 
         /// <summary>
-        /// If the transition fails, restart the local NetworkManager host so the
-        /// user returns to a functional menu state.
+        /// If the transition fails, recreate the Relay party session via
+        /// <see cref="HostConnectionService"/> so the user returns to a
+        /// functional menu state with networking.
         /// </summary>
         private async UniTask RecoverFromFailedTransitionAsync()
         {
-            Debug.Log("[PartyInviteController] Attempting recovery — restarting local host...");
+            Debug.Log("[PartyInviteController] Attempting recovery — restarting Relay host...");
 
             try
             {
-                var nm = NetworkManager.Singleton;
-                if (nm != null && !nm.IsListening)
+                if (HostConnectionService.Instance != null)
                 {
-                    nm.StartHost();
```

</details>

### `c16a86baf` — fix(multiplayer): restore local host startup, defer Relay to invite time

_Claude, 2026-03-04 06:34:55 +0000_

```text
MultiplayerSetup.EnsureHostStarted() was delegating host startup entirely
to HostConnectionService's Relay session creation, which requires joining
a UGS presence lobby + allocating Relay (multiple network round-trips).
This took >15s or failed, causing AuthenticationSceneController to timeout
and load Menu_Main without networking — breaking player spawning.

Changes:
- MultiplayerSetup: restore nm.StartHost() for immediate local host
- HostConnectionService: remove eager CreatePartySessionAsync() from
  Start() and HandleSignedInEvent() to avoid conflicting with local host
- PartyInviteController: implement TransitionToPartyHostAsync() with
  fade-to-black screen for clean local→Relay transition when first
  party invite is sent. Recovery falls back to local host on failure.

Menu_Main now loads exactly once with a local host. Relay session is
created on-demand via TransitionToPartyHostAsync when invites are needed,
with a fade screen covering the transition so no double-load is visible.
```

```text
 Assets/_Scripts/Controller/Multiplayer/MultiplayerSetup.cs |  21 ++++++---
 Assets/_Scripts/Controller/Party/HostConnectionService.cs  |  52 +++++++++-----------
 Assets/_Scripts/Controller/Party/PartyInviteController.cs  | 110 +++++++++++++++++++++++++++++++++----------
 3 files changed, 123 insertions(+), 60 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 150 of 296 lines)</summary>

```diff
diff --git a/Assets/_Scripts/Controller/Multiplayer/MultiplayerSetup.cs b/Assets/_Scripts/Controller/Multiplayer/MultiplayerSetup.cs
index e334a2496..73bc8c205 100644
--- a/Assets/_Scripts/Controller/Multiplayer/MultiplayerSetup.cs
+++ b/Assets/_Scripts/Controller/Multiplayer/MultiplayerSetup.cs
@@ -165,12 +165,21 @@ namespace CosmicShore.Gameplay
                 }
 #endif
 
-                // Host startup is delegated to HostConnectionService which creates a
-                // Relay-backed party session (via CreateSessionAsync + WithRelayNetwork).
-                // AuthenticationSceneController waits for the Relay host with a 15s
-                // timeout; if Relay fails, Menu_Main loads without networking.
-                Debug.Log("<color=#00FFFF>[FLOW-1] [MultiplayerSetup] Callbacks wired. Waiting for HostConnectionService to start Relay host.</color>");
-                CSDebug.Log("[MultiplayerSetup] Callbacks wired. Waiting for HostConnectionService to start Relay host.");
+                // Start a local host immediately so the menu scene can spawn
+                // player vessels without waiting for Relay. HostConnectionService
+                // will create a Relay-backed party session on demand when an
+                // invite is sent (via TransitionToPartyHostAsync).
+                bool started = nm.StartHost();
+                if (started)
+                {
+                    Debug.Log("<color=#00FFFF>[FLOW-1] [MultiplayerSetup] Local host started successfully.</color>");
+                    CSDebug.Log("[MultiplayerSetup] Local host started.");
+                }
+                else
+                {
+                    Debug.LogError("<color=#FF0000>[FLOW-1] [MultiplayerSetup] StartHost() failed!</color>");
+                    CSDebug.LogError("[MultiplayerSetup] StartHost() returned false — networking will not work.");
+                }
             }
             finally
             {
diff --git a/Assets/_Scripts/Controller/Party/HostConnectionService.cs b/Assets/_Scripts/Controller/Party/HostConnectionService.cs
index 3d7a85d54..8297b0281 100644
--- a/Assets/_Scripts/Controller/Party/HostConnectionService.cs
+++ b/Assets/_Scripts/Controller/Party/HostConnectionService.cs
@@ -121,19 +121,11 @@ namespace CosmicShore.Gameplay
             SyncLocalIdentity();
             await JoinPresenceLobbyAsync();
 
-            // Create Relay-backed party session so the NetworkManager starts as a
-            // Relay host. Wrapped in try-catch so a Relay failure does not block
-            // _initialized — the presence lobby and refresh loop must always work.
-            // SendInviteAsync has a lazy-creation fallback if _partySession is null.
-            try
-            {
-                await CreatePartySessionAsync();
-            }
-            catch (Exception e)
-            {
-                Debug.LogWarning($"[HostConnectionService] Party session creation failed (Relay may be unavailable). " +
-                    $"Invite send will retry on demand. Error: {e.Message}");
-            }
+            // Relay party session is NOT created here — MultiplayerSetup starts a
+            // local host for the menu scene. The Relay session is created on demand
+            // when the first party invite is sent (via TransitionToPartyHostAsync →
+            // CreatePartySessionPublicAsync). SendInviteAsync also has a lazy
+            // creation fallback if _partySession is null at invite time.
 
             _initialized = true;
             DebugExtensions.LogColored(
@@ -185,18 +177,8 @@ namespace CosmicShore.Gameplay
             {
                 await JoinPresenceLobbyAsync();
 
-                // Create Relay-backed party session so the NetworkManager starts as a
-                // Relay host. Failure is non-fatal — the presence lobby and refresh loop
-                // must always work. SendInviteAsync has a lazy-creation fallback.
-                try
-                {
-                    await CreatePartySessionAsync();
-                }
-                catch (Exception e)
-                {
-                    Debug.LogWarning($"[HostConnectionService] Party session creation failed (Relay may be unavailable). " +
-                        $"Invite send will retry on demand. Error: {e.Message}");
-                }
+                // Relay party session deferred to invite time — local host is
+                // started by MultiplayerSetup. See CreatePartySessionPublicAsync().
 
                 _initialized = true;
                 DebugExtensions.LogColored(
@@ -249,8 +231,11 @@ namespace CosmicShore.Gameplay
 
                 if (_partySession == null)
                 {
+                    // The proper path is TransitionToPartyHostAsync() → CreatePartySessionPublicAsync(),
+                    // which shuts down the local host before creating a Relay session. This lazy
+                    // fallback may fail if a local host is still running.
                     DebugExtensions.LogWarningColored(
-                        "[INVITE-SEND] _partySession is null — creating on demand...", Color.yellow);
+                        "[INVITE-SEND] _partySession is null — creating on demand (local host must already be shut down)...", Color.yellow);
                     await CreatePartySessionAsync();
                 }
 
@@ -807,6 +792,17 @@ namespace CosmicShore.Gameplay
         // Party Session
         // ─────────────────────────────────────────────────────────────────────
 
+        /// <summary>
+        /// Creates a Relay-backed party session. The UGS Multiplayer SDK calls
+        /// <c>NetworkManager.StartHost()</c> internally when creating a session
+        /// with <c>WithRelayNetwork()</c>.
+        ///
+        /// <b>Caller must ensure the local host is shut down first</b> — if a
+        /// local host is already running (started by <see cref="MultiplayerSetup"/>),
+        /// the SDK's internal <c>StartHost()</c> will conflict. Use
+        /// <see cref="PartyInviteController.TransitionToPartyHostAsync"/> which
+        /// handles the full shutdown → Relay → reload flow.
+        /// </summary>
         private async Task CreatePartySessionAsync()
         {
             if (_partySession != null) return;
@@ -823,10 +819,6 @@ namespace CosmicShore.Gameplay
             {
                 try
                 {
-                    // The UGS Multiplayer SDK calls NetworkManager.StartHost()
-                    // internally when creating a Relay-backed session.
-                    // AuthenticationSceneController waits for nm.IsListening
-                    // before loading Menu_Main — no local host fallback.
                     _partySession = await MultiplayerService.Instance.CreateSessionAsync(opts);
                     connectionData.IsHost = true;
                     Debug.Log($"[HostConnectionService] Created Relay party session {_partySession.Id}");
diff --git a/Assets/_Scripts/Controller/Party/PartyInviteController.cs b/Assets/_Scripts/Controller/Party/PartyInviteController.cs
index 019153a43..3670411e9 100644
--- a/Assets/_Scripts/Controller/Party/PartyInviteController.cs
+++ b/Assets/_Scripts/Controller/Party/PartyInviteController.cs
@@ -1,11 +1,13 @@
 using System;
 using System.Threading;
+using CosmicShore.Core;
 using CosmicShore.ScriptableObjects;
 using CosmicShore.Utility;
 using Cysharp.Threading.Tasks;
 using Reflex.Attributes;
 using Unity.Netcode;
 using UnityEngine;
+using UnityEngine.SceneManagement;
 
 namespace CosmicShore.Gameplay
 {
@@ -36,6 +38,8 @@ namespace CosmicShore.Gameplay
         [SerializeField] private float sceneLoadTimeoutSeconds = 15f;
 
         [Inject] private GameDataSO gameData;
+        [Inject] private SceneTransitionManager _sceneTransitionManager;
+        [Inject] private SceneNameListSO _sceneNames;
 
         private CancellationTokenSource _cts;
```

</details>

### `60e1f78e0` — fix(party): use splash screen instead of FadeToBlack for Relay transition

_Claude, 2026-03-04 06:39:48 +0000_

```text
Replace FadeToBlack with SetFadeImmediate(1f) + OnClientReady one-shot
listener pattern (matching SceneLoader.LaunchGame). Splash screen covers
the local→Relay host transition and fades out when the menu vessel is
spawned and ready on the new Relay host.
```

```text
 Assets/_Scripts/Controller/Party/PartyInviteController.cs | 39 +++++++++++++++++++++++++++++----------
 1 file changed, 29 insertions(+), 10 deletions(-)
```

<details><summary>Patch (code/doc/text files)</summary>

```diff
diff --git a/Assets/_Scripts/Controller/Party/PartyInviteController.cs b/Assets/_Scripts/Controller/Party/PartyInviteController.cs
index 3670411e9..846f9cd07 100644
--- a/Assets/_Scripts/Controller/Party/PartyInviteController.cs
+++ b/Assets/_Scripts/Controller/Party/PartyInviteController.cs
@@ -173,10 +173,9 @@ namespace CosmicShore.Gameplay
         /// Transitions from the local host (started by <see cref="MultiplayerSetup"/>)
         /// to a Relay-backed party session so remote players can join via invite.
         ///
-        /// Flow: fade to black → despawn vessels → shutdown local host →
-        /// create Relay party session → reload Menu_Main via Netcode.
-        /// The fade screen covers the entire transition so the user never sees
-        /// Menu_Main loading twice.
+        /// Flow: show splash → despawn vessels → shutdown local host →
+        /// create Relay party session → reload Menu_Main via Netcode →
+        /// splash fades out on <c>OnClientReady</c> (vessel spawned).
         /// </summary>
         public async UniTask TransitionToPartyHostAsync()
         {
@@ -197,9 +196,12 @@ namespace CosmicShore.Gameplay
 
             try
             {
-                // Fade to black so the user doesn't see the scene reload
-                if (_sceneTransitionManager != null)
-                    await _sceneTransitionManager.FadeToBlack();
+                // Show splash screen immediately (same pattern as SceneLoader.LaunchGame)
+                _sceneTransitionManager?.SetFadeImmediate(1f);
+
+                // Fade out splash when menu vessel is spawned and ready
+                if (gameData != null)
+                    gameData.OnClientReady.OnRaised += OnRelayMenuReady;
 
                 // 1. Despawn menu vessels and reset game data
                 CleanUpCurrentSession();
@@ -228,6 +230,8 @@ namespace CosmicShore.Gameplay
                 {
                     Debug.LogError("[PartyInviteController] Relay host not running after transition. Falling back to direct load.");
                     SceneManager.LoadScene(menuScene);
+                    // No OnClientReady in non-networked mode — fade out manually
+                    _sceneTransitionManager?.FadeFromBlack().Forget();
                 }
             }
             catch (Exception e)
@@ -241,6 +245,18 @@ namespace CosmicShore.Gameplay
             }
         }
 
+        /// <summary>
+        /// One-shot listener: fades the splash screen away once the menu vessel
+        /// is spawned on the new Relay host. Mirrors <c>SceneLoader.FadeFromSplashOnReady</c>.
+        /// </summary>
+        private void OnRelayMenuReady()
+        {
+            if (gameData != null)
+                gameData.OnClientReady.OnRaised -= OnRelayMenuReady;
+
+            _sceneTransitionManager?.FadeFromBlack().Forget();
+        }
+
         // ─────────────────────────────────────────────────────────────────────
         // Internal: Cleanup & Shutdown
         // ─────────────────────────────────────────────────────────────────────
@@ -344,6 +360,10 @@ namespace CosmicShore.Gameplay
         {
             Debug.Log("[PartyInviteController] Attempting recovery — restarting local host...");
 
+            // Unsubscribe the one-shot listener in case it was registered
+            if (gameData != null)
+                gameData.OnClientReady.OnRaised -= OnRelayMenuReady;
+
             try
             {
                 var nm = NetworkManager.Singleton;
@@ -372,9 +392,8 @@ namespace CosmicShore.Gameplay
                     }
                 }
 
-                // Fade from black so the menu is visible again
-                if (_sceneTransitionManager != null)
-                    await _sceneTransitionManager.FadeFromBlack();
+                // Dismiss the splash screen so the menu is visible
+                _sceneTransitionManager?.FadeFromBlack().Forget();
             }
             catch (Exception e)
             {
```

</details>

### `986fe9d46` — fix(party): route SendInviteAsync through TransitionToPartyHostAsync

_Claude, 2026-03-04 07:53:25 +0000_

```text
SendInviteAsync's lazy fallback called CreatePartySessionAsync() directly
when _partySession was null, but the local host was still running. The UGS
SDK's internal StartAsHost() conflicted with the active NetworkManager,
causing "Failed to start NetworkManager component as host".

Replace the direct CreatePartySessionAsync() call with
PartyInviteController.TransitionToPartyHostAsync(), which properly handles:
host shutdown, splash screen, Relay session creation, and scene reload.
```

```text
 Assets/_Scripts/Controller/Party/HostConnectionService.cs | 34 +++++++++++++++++++++++++++++-----
 1 file changed, 29 insertions(+), 5 deletions(-)
```

<details><summary>Patch (code/doc/text files)</summary>

```diff
diff --git a/Assets/_Scripts/Controller/Party/HostConnectionService.cs b/Assets/_Scripts/Controller/Party/HostConnectionService.cs
index 8297b0281..780bf35fa 100644
--- a/Assets/_Scripts/Controller/Party/HostConnectionService.cs
+++ b/Assets/_Scripts/Controller/Party/HostConnectionService.cs
@@ -232,11 +232,35 @@ namespace CosmicShore.Gameplay
                 if (_partySession == null)
                 {
                     // The proper path is TransitionToPartyHostAsync() → CreatePartySessionPublicAsync(),
-                    // which shuts down the local host before creating a Relay session. This lazy
-                    // fallback may fail if a local host is still running.
-                    DebugExtensions.LogWarningColored(
-                        "[INVITE-SEND] _partySession is null — creating on demand (local host must already be shut down)...", Color.yellow);
-                    await CreatePartySessionAsync();
+                    // which shuts down the local host before creating a Relay session.
+                    // Direct CreatePartySessionAsync() fails when a local host is still running
+                    // because the UGS SDK's internal StartAsHost() conflicts with the active NetworkManager.
+                    var pic = PartyInviteController.Instance;
+                    if (pic != null)
+                    {
+                        if (pic.IsTransitioning)
+                        {
+                            DebugExtensions.LogWarningColored(
+                                "[INVITE-SEND] Waiting for ongoing Relay transition...", Color.yellow);
+                            while (pic.IsTransitioning)
+                                await Task.Yield();
+                        }
+
+                        if (_partySession == null)
+                        {
+                            DebugExtensions.LogWarningColored(
+                                "[INVITE-SEND] _partySession is null — transitioning to Relay host...", Color.yellow);
+                            _lobbyBusy = false; // Release lock during scene-reloading transition
+                            await pic.TransitionToPartyHostAsync().AsTask();
+                        }
+                    }
+
+                    if (_partySession == null)
+                    {
+                        DebugExtensions.LogErrorColored(
+                            "[INVITE-SEND] Party session still null after transition — cannot send invite.", Color.red);
+                        return;
+                    }
                 }
 
                 DebugExtensions.LogColored(
```

</details>
