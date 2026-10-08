# Branch archive: `claude/fix-wildlife-blitz-K6gY9`

_Snapshot 2026-10-08. Index: [README](../README.md)_

- **Last commit:** 2026-02-21 by Claude
- **Unmerged commits:** 3
- **Open pull request:** https://github.com/froglet-studio/Cosmic-Shore/pull/91
- **Forked from:** `d0dd7d5b2` (2026-02-21, [FIX] Use LocalPlayer Name for Game Feed)
- **Tip:** `5af238e95`
- **Files touched (16):**
  - `Assets/_Scripts/Game/Arcade/MultiplayerWildlifeBlitzMiniGame.cs`
  - `Assets/_Scripts/Game/Arcade/MultiplayerWildlifeBlitzScoreTracker.cs`
  - `Assets/_Scripts/Game/Arcade/MultiplayerWildlifeBlitzScoreTracker.cs.meta`
  - `Assets/_Scripts/Game/Arcade/Scoring/WildlifeBlitzEndGameStatsTracker.cs`
  - `Assets/_Scripts/Game/Multiplayer/DomainAssigner.cs`
  - `Assets/_Scripts/Game/UI/Animations/EllipsisTextLooper.cs`
  - `Assets/_Scripts/Game/UI/MiniGameHUD.cs`
  - `Assets/_Scripts/Game/UI/MultiplayerWildlifeBlitzHUD.cs`
  - `Assets/_Scripts/Game/UI/MultiplayerWildlifeBlitzHUD.cs.meta`
  - `Assets/_Scripts/Game/UI/MultiplayerWildlifeBlitzScoreboard.cs`
  - `Assets/_Scripts/Game/UI/MultiplayerWildlifeBlitzScoreboard.cs.meta`
  - `Assets/_Scripts/Game/UI/WildlifeBlitzStatsProvider.cs`
  - `Assets/_Scripts/MinigameHUD/View/MinigameHUDView.cs`
  - `Assets/_Scripts/Utility/DataContainers/MultiplayerWildlifeBlitzEndGameController.cs`
  - `Assets/_Scripts/Utility/DataContainers/MultiplayerWildlifeBlitzEndGameController.cs.meta`
  - `Assets/_Scripts/Utility/DataContainers/PreGameCinematicController.cs`

### `5171108a3` — Add co-op multiplayer support for Wildlife Blitz (up to 3 players)

_Claude, 2026-02-21 10:18:15 +0000_

```text
Refactored MultiplayerWildlifeBlitzMiniGame to extend
MultiplayerDomainGamesController (matching HexRace/Joust pattern)
with proper co-op score syncing, endgame flow, and reset support.

Key changes:
- Controller: Server-authoritative co-op results with score sync RPCs,
  per-player kill tracking via BlocksDestroyed, lifeform/time monitor
  reset on replay, golf rules scoring (lower time = better)
- EndGameController: Multiplayer endgame cinematic showing VICTORY/DEFEAT
  with co-op kill totals and finish time
- Scoreboard: Shows per-player kill contributions and finish time
- HUD: Real-time per-player kill count cards during gameplay
- ScoreTracker: Networked score tracking filtering local player events,
  reports kills to controller for server sync

All players spawn on Jade domain (co-op same team). 1 player = offline,
2-3 players = online co-op. Win = all lifeforms killed, score = time.
```

```text
 Assets/_Scripts/Game/Arcade/MultiplayerWildlifeBlitzMiniGame.cs       | 193 ++++++++++++++++++++++++++------
 Assets/_Scripts/Game/Arcade/MultiplayerWildlifeBlitzScoreTracker.cs   |  97 ++++++++++++++++
 .../_Scripts/Game/Arcade/MultiplayerWildlifeBlitzScoreTracker.cs.meta |   3 +
 Assets/_Scripts/Game/UI/MultiplayerWildlifeBlitzHUD.cs                |  39 +++++++
 Assets/_Scripts/Game/UI/MultiplayerWildlifeBlitzHUD.cs.meta           |   3 +
 Assets/_Scripts/Game/UI/MultiplayerWildlifeBlitzScoreboard.cs         |  76 +++++++++++++
 Assets/_Scripts/Game/UI/MultiplayerWildlifeBlitzScoreboard.cs.meta    |   3 +
 .../DataContainers/MultiplayerWildlifeBlitzEndGameController.cs       |  73 ++++++++++++
 .../DataContainers/MultiplayerWildlifeBlitzEndGameController.cs.meta  |   3 +
 9 files changed, 455 insertions(+), 35 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 150 of 546 lines)</summary>

```diff
diff --git a/Assets/_Scripts/Game/Arcade/MultiplayerWildlifeBlitzMiniGame.cs b/Assets/_Scripts/Game/Arcade/MultiplayerWildlifeBlitzMiniGame.cs
index b3e6a4952..bb8ffc311 100644
--- a/Assets/_Scripts/Game/Arcade/MultiplayerWildlifeBlitzMiniGame.cs
+++ b/Assets/_Scripts/Game/Arcade/MultiplayerWildlifeBlitzMiniGame.cs
@@ -1,73 +1,196 @@
-﻿using Cysharp.Threading.Tasks;
+using System.Linq;
+using CosmicShore.Game.Arcade.Scoring;
+using Unity.Collections;
 using Unity.Netcode;
+using UnityEngine;
 
 namespace CosmicShore.Game.Arcade
 {
-    public class MultiplayerWildlifeBlitzMiniGame : MultiplayerMiniGameControllerBase
+    /// <summary>
+    /// Multiplayer co-op Wildlife Blitz controller (up to 3 players, same team).
+    /// All players cooperate to kill all lifeforms. Win condition: all lifeforms dead.
+    /// Score = elapsed time (golf rules — lower is better).
+    /// </summary>
+    public class MultiplayerWildlifeBlitzMiniGame : MultiplayerDomainGamesController
     {
-        int readyClientCount;
+        [Header("Wildlife Blitz")]
+        [SerializeField] private AllLifeFormsDestroyedTurnMonitor lifeFormMonitor;
+        [SerializeField] private TimeBasedTurnMonitor timeMonitor;
 
-        public void OnClickReturnToMainMenu()
+        private bool _resultsSent;
+        private float _turnStartTime;
+
+        // Single source of truth — set by server, read by EndGameController
+        public bool DidCoOpWin { get; private set; }
+        public float FinishTime { get; private set; }
+        public bool ResultsReady { get; private set; }
+
+        protected override bool UseGolfRules => true;
+
+        public override void OnNetworkSpawn()
         {
-            CloseSession_ServerRpc();
+            base.OnNetworkSpawn();
+            numberOfRounds = 1;
+            numberOfTurnsPerRound = 1;
+            _resultsSent = false;
         }
 
-        protected override void OnReadyClicked_()
+        protected override void OnCountdownTimerEnded()
         {
-            RaiseToggleReadyButtonEvent(false);
-            OnReadyClicked_ServerRpc();
+            if (!IsServer) return;
+
+            _turnStartTime = Time.time;
+            OnCountdownTimerEnded_ClientRpc();
         }
 
-        [ServerRpc(RequireOwnership = false)]
-        void OnReadyClicked_ServerRpc(ServerRpcParams rpcParams = default)
+        [ClientRpc]
+        void OnCountdownTimerEnded_ClientRpc()
         {
-            readyClientCount++;
+            _turnStartTime = Time.time;
+            gameData.SetPlayersActive();
+            gameData.StartTurn();
+        }
 
-            // Keep the same pattern you used in MultiplayerCellularDuelController
-            if (!readyClientCount.Equals(gameData.SelectedPlayerCount))
-                return;
+        /// <summary>
+        /// Called when the turn ends (all lifeforms killed or timer expired).
+        /// Server calculates co-op results and syncs to all clients.
+        /// </summary>
+        protected override void OnTurnEndedCustom()
+        {
+            base.OnTurnEndedCustom();
+            if (!IsServer) return;
+            if (_resultsSent) return;
 
-            readyClientCount = 0;
-            OnReadyClicked_ClientRpc();
+            CalculateCoOpResults_Server();
+            SyncCoOpResults_Authoritative();
+            _resultsSent = true;
         }
 
-        [ClientRpc]
-        void OnReadyClicked_ClientRpc()
+        void CalculateCoOpResults_Server()
         {
-            StartCountdownTimer();
+            float elapsed = Time.time - _turnStartTime;
+            bool allKilled = lifeFormMonitor != null && lifeFormMonitor.CheckForEndOfTurn();
+
+            DidCoOpWin = allKilled;
+            FinishTime = elapsed;
+
+            // In co-op, all players share the same score (team score)
+            float teamScore = allKilled ? elapsed : 999f;
+
+            foreach (var stats in gameData.RoundStatsList)
+            {
+                stats.Score = teamScore;
+            }
+
+            gameData.SortRoundStats(UseGolfRules);
+            gameData.CalculateDomainStats(UseGolfRules);
+
+            Debug.Log($"[WildlifeBlitzMP] Co-op results: Win={allKilled} Time={elapsed:F2}s " +
+                      $"Players=[{string.Join(", ", gameData.RoundStatsList.Select(s => $"{s.Name}:{s.Score:F1}"))}]");
         }
 
-        protected override void OnCountdownTimerEnded()
+        void SyncCoOpResults_Authoritative()
         {
-            if (!IsServer)
-                return;
+            var list = gameData.RoundStatsList;
+            int count = list.Count;
 
-            OnCountdownTimerEnded_ClientRpc();
+            var names = new FixedString64Bytes[count];
+            var scores = new float[count];
+            var domains = new int[count];
+
+            for (int i = 0; i < count; i++)
+            {
+                names[i] = new FixedString64Bytes(list[i].Name);
+                scores[i] = list[i].Score;
+                domains[i] = (int)list[i].Domain;
+            }
+
+            SyncCoOpResults_ClientRpc(names, scores, domains, DidCoOpWin, FinishTime);
         }
 
         [ClientRpc]
-        void OnCountdownTimerEnded_ClientRpc()
+        void SyncCoOpResults_ClientRpc(
+            FixedString64Bytes[] names,
+            float[] scores,
+            int[] domains,
+            bool didWin,
+            float finishTime)
         {
-            gameData.SetPlayersActive();
-            gameData.StartTurn();
+            for (int i = 0; i < names.Length; i++)
```

</details>

### `5f4ab3cec` — Merge Wildlife Blitz into one unified game mode (like HexRace)

_Claude, 2026-02-21 10:28:52 +0000_

```text
Removes the separate single-player / multiplayer split. Now there is
ONE unified controller (WildlifeBlitzController) that handles both:
  - 1 player selected → solo mode (IsMultiplayerMode = false, AI spawns)
  - 2-3 players selected → online co-op (same team, Jade domain)

Changes:
- Renamed MultiplayerWildlifeBlitzMiniGame → WildlifeBlitzController
  Added UGS analytics reporting from the old single-player tracker
- Renamed MultiplayerWildlifeBlitzEndGameController →
  WildlifeBlitzEndGameController — merged single-player features
  (Sparrow prism stop, high-score check, boost reset, AI cleanup)
- Renamed MultiplayerWildlifeBlitzScoreTracker → WildlifeBlitzScoreTracker
- Renamed MultiplayerWildlifeBlitzScoreboard → WildlifeBlitzScoreboard
- Renamed MultiplayerWildlifeBlitzHUD → WildlifeBlitzCoOpHUD
- Updated DomainAssigner to use GameModes.WildlifeBlitz (26) for co-op
  domain assignment instead of the old MultiplayerWildlifeBlitzGame (32)
- Updated WildlifeBlitzStatsProvider to reference WildlifeBlitzScoreTracker
- Updated WildlifeBlitzEndGameStatsTracker to use unified types

Old single-player-only files (SinglePlayerWildlifeBlitzController,
WildlifeBlitzMiniGame, SinglePlayerWildlifeBlitzScoreTracker,
WildlifeBlitzEndGameCinematicController) are now obsolete — the unified
scene should use the new classes exclusively.
```

```text
 Assets/_Scripts/Game/Arcade/MultiplayerWildlifeBlitzMiniGame.cs       |  87 ++++++++++++++++--------
 Assets/_Scripts/Game/Arcade/MultiplayerWildlifeBlitzScoreTracker.cs   |  15 ++---
 .../_Scripts/Game/Arcade/Scoring/WildlifeBlitzEndGameStatsTracker.cs  |  32 ++++-----
 Assets/_Scripts/Game/Multiplayer/DomainAssigner.cs                    |   4 +-
 Assets/_Scripts/Game/UI/MultiplayerWildlifeBlitzHUD.cs                |   7 +-
 Assets/_Scripts/Game/UI/MultiplayerWildlifeBlitzScoreboard.cs         |  18 ++---
 Assets/_Scripts/Game/UI/WildlifeBlitzStatsProvider.cs                 |   2 +-
 .../DataContainers/MultiplayerWildlifeBlitzEndGameController.cs       | 115 +++++++++++++++++++++++++-------
 8 files changed, 187 insertions(+), 93 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 150 of 609 lines)</summary>

```diff
diff --git a/Assets/_Scripts/Game/Arcade/MultiplayerWildlifeBlitzMiniGame.cs b/Assets/_Scripts/Game/Arcade/MultiplayerWildlifeBlitzMiniGame.cs
index bb8ffc311..239fdecc2 100644
--- a/Assets/_Scripts/Game/Arcade/MultiplayerWildlifeBlitzMiniGame.cs
+++ b/Assets/_Scripts/Game/Arcade/MultiplayerWildlifeBlitzMiniGame.cs
@@ -1,5 +1,6 @@
 using System.Linq;
 using CosmicShore.Game.Arcade.Scoring;
+using CosmicShore.Game.Analytics;
 using Unity.Collections;
 using Unity.Netcode;
 using UnityEngine;
@@ -7,11 +8,16 @@ using UnityEngine;
 namespace CosmicShore.Game.Arcade
 {
     /// <summary>
-    /// Multiplayer co-op Wildlife Blitz controller (up to 3 players, same team).
-    /// All players cooperate to kill all lifeforms. Win condition: all lifeforms dead.
-    /// Score = elapsed time (golf rules — lower is better).
+    /// Unified Wildlife Blitz controller — handles both solo (1 player) and co-op multiplayer
+    /// (up to 3 players, same team). Follows the HexRace/Joust pattern:
+    ///   1 player selected  → IsMultiplayerMode = false → ServerPlayerVesselInitializer runs
+    ///                         locally with no AI opponents (solo blitz)
+    ///   2-3 players selected → online co-op, all Jade domain
+    ///
+    /// Win condition : all lifeforms in the cell destroyed.
+    /// Score         : elapsed time (golf rules — lower is better). 999 on timeout.
     /// </summary>
-    public class MultiplayerWildlifeBlitzMiniGame : MultiplayerDomainGamesController
+    public class WildlifeBlitzController : MultiplayerDomainGamesController
     {
         [Header("Wildlife Blitz")]
         [SerializeField] private AllLifeFormsDestroyedTurnMonitor lifeFormMonitor;
@@ -20,8 +26,8 @@ namespace CosmicShore.Game.Arcade
         private bool _resultsSent;
         private float _turnStartTime;
 
-        // Single source of truth — set by server, read by EndGameController
-        public bool DidCoOpWin { get; private set; }
+        // Authoritative results — set by server, read by EndGameController & Scoreboard
+        public bool DidWin { get; private set; }
         public float FinishTime { get; private set; }
         public bool ResultsReady { get; private set; }
 
@@ -51,9 +57,11 @@ namespace CosmicShore.Game.Arcade
             gameData.StartTurn();
         }
 
+        #region Turn End / Result Calculation
+
         /// <summary>
-        /// Called when the turn ends (all lifeforms killed or timer expired).
-        /// Server calculates co-op results and syncs to all clients.
+        /// Called when the turn ends (all lifeforms killed OR timer expired).
+        /// Server calculates results and syncs to all clients.
         /// </summary>
         protected override void OnTurnEndedCustom()
         {
@@ -61,35 +69,52 @@ namespace CosmicShore.Game.Arcade
             if (!IsServer) return;
             if (_resultsSent) return;
 
-            CalculateCoOpResults_Server();
-            SyncCoOpResults_Authoritative();
+            CalculateResults_Server();
+            SyncResults_Authoritative();
             _resultsSent = true;
         }
 
-        void CalculateCoOpResults_Server()
+        void CalculateResults_Server()
         {
             float elapsed = Time.time - _turnStartTime;
             bool allKilled = lifeFormMonitor != null && lifeFormMonitor.CheckForEndOfTurn();
 
-            DidCoOpWin = allKilled;
+            DidWin = allKilled;
             FinishTime = elapsed;
 
-            // In co-op, all players share the same score (team score)
+            // All players share the same team score
             float teamScore = allKilled ? elapsed : 999f;
 
             foreach (var stats in gameData.RoundStatsList)
-            {
                 stats.Score = teamScore;
-            }
 
             gameData.SortRoundStats(UseGolfRules);
             gameData.CalculateDomainStats(UseGolfRules);
 
-            Debug.Log($"[WildlifeBlitzMP] Co-op results: Win={allKilled} Time={elapsed:F2}s " +
+            // Report stats to UGS analytics
+            if (UGSStatsManager.Instance)
+            {
+                var localName = gameData.LocalPlayer?.Name;
+                var localStats = gameData.RoundStatsList.FirstOrDefault(s => s.Name == localName);
+                if (localStats != null)
+                {
+                    int kills = localStats.BlocksDestroyed;
+                    int crystals = localStats.ElementalCrystalsCollected;
+                    int finalScore = (int)localStats.Score;
+
+                    UGSStatsManager.Instance.ReportBlitzStats(
+                        GameModes.WildlifeBlitz,
+                        gameData.SelectedIntensity.Value,
+                        crystals, kills, finalScore
+                    );
+                }
+            }
+
+            Debug.Log($"[WildlifeBlitz] Results: Win={allKilled} Time={elapsed:F2}s " +
                       $"Players=[{string.Join(", ", gameData.RoundStatsList.Select(s => $"{s.Name}:{s.Score:F1}"))}]");
         }
 
-        void SyncCoOpResults_Authoritative()
+        void SyncResults_Authoritative()
         {
             var list = gameData.RoundStatsList;
             int count = list.Count;
@@ -105,11 +130,11 @@ namespace CosmicShore.Game.Arcade
                 domains[i] = (int)list[i].Domain;
             }
 
-            SyncCoOpResults_ClientRpc(names, scores, domains, DidCoOpWin, FinishTime);
+            SyncResults_ClientRpc(names, scores, domains, DidWin, FinishTime);
         }
 
         [ClientRpc]
-        void SyncCoOpResults_ClientRpc(
+        void SyncResults_ClientRpc(
             FixedString64Bytes[] names,
             float[] scores,
             int[] domains,
@@ -122,32 +147,33 @@ namespace CosmicShore.Game.Arcade
                 var stat = gameData.RoundStatsList.FirstOrDefault(s => s.Name == sName);
                 if (stat == null)
                 {
-                    Debug.LogError($"[WildlifeBlitzMP] Client could not match RoundStats for '{sName}'. " +
-                                   $"Available: {string.Join(", ", gameData.RoundStatsList.Select(s => $"'{s.Name}'"))}");
+                    Debug.LogWarning($"[WildlifeBlitz] Client could not match RoundStats for '{sName}'.");
                     continue;
                 }
                 stat.Score = scores[i];
                 stat.Domain = (Domains)domains[i];
             }
 
-            // Authoritative results — EndGameController reads these
-            DidCoOpWin = didWin;
+            DidWin = didWin;
             FinishTime = finishTime;
```

</details>

### `5af238e95` — Add connecting panel animations and pre-game cinematic infrastructure

_Claude, 2026-02-21 10:59:41 +0000_

```text
MiniGameHUDView:
- Added hackerTextAnimator (DoTweenTypewriterAnimator) field — plays
  hacker-style type-in when connecting panel shows, type-out when hiding
- Added dotsAnimator (ConnectingDotsAnimator) field — enables/disables
  the dots loop animator with the connecting panel

ConnectingDotsAnimator:
- Renamed EllipsisTextLooper → ConnectingDotsAnimator (clearer name)
- Same behavior: loops "CONNECTING TO THE SHORE..." with animated dots

PreGameCinematicController (new):
- Orbit mode: circles camera around a focus point at configurable
  radius/height/speed for a set duration
- Waypoints mode: lerps camera through placed Transform waypoints
- Skip button support — cancels cinematic immediately
- Fully async (UniTask), cancellation-safe

MiniGameHUD:
- Added preGameCinematic (PreGameCinematicController) field
- If assigned, runs the cinematic in parallel with the minimum
  connecting timer during RunConnectingMinimum()

Unity Editor setup still needed per scene:
- Assign hackerTextAnimator / dotsAnimator on MiniGameHUDView
- Place PreGameCinematicController GO, configure orbit, assign skip button
- Wire preGameCinematic on MiniGameHUD
```

```text
 Assets/_Scripts/Game/UI/Animations/EllipsisTextLooper.cs             |   7 +-
 Assets/_Scripts/Game/UI/MiniGameHUD.cs                               |  29 ++++-
 Assets/_Scripts/MinigameHUD/View/MinigameHUDView.cs                  |  36 +++++-
 Assets/_Scripts/Utility/DataContainers/PreGameCinematicController.cs | 193 +++++++++++++++++++++++++++++++++
 4 files changed, 258 insertions(+), 7 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 150 of 341 lines)</summary>

```diff
diff --git a/Assets/_Scripts/Game/UI/Animations/EllipsisTextLooper.cs b/Assets/_Scripts/Game/UI/Animations/EllipsisTextLooper.cs
index ba38f9e53..13542360c 100644
--- a/Assets/_Scripts/Game/UI/Animations/EllipsisTextLooper.cs
+++ b/Assets/_Scripts/Game/UI/Animations/EllipsisTextLooper.cs
@@ -4,8 +4,13 @@ using UnityEngine;
 
 namespace CosmicShore.Game.UI
 {
+    /// <summary>
+    /// Animates "CONNECTING TO THE SHORE..." with progressively appearing dots.
+    /// Attach to the TMP_Text that shows the connecting message.
+    /// Starts looping on enable, stops on disable.
+    /// </summary>
     [RequireComponent(typeof(TMP_Text))]
-    public sealed class EllipsisTextLooper : MonoBehaviour
+    public sealed class ConnectingDotsAnimator : MonoBehaviour
     {
         [Header("Settings")]
         [SerializeField] private string baseText = "CONNECTING TO THE SHORE";
diff --git a/Assets/_Scripts/Game/UI/MiniGameHUD.cs b/Assets/_Scripts/Game/UI/MiniGameHUD.cs
index 847d8b4dd..86a6e2464 100644
--- a/Assets/_Scripts/Game/UI/MiniGameHUD.cs
+++ b/Assets/_Scripts/Game/UI/MiniGameHUD.cs
@@ -2,6 +2,7 @@ using System;
 using System.Collections.Generic;
 using System.Globalization;
 using System.Threading;
+using CosmicShore.Game.Cinematics;
 using CosmicShore.Soap;
 using CosmicShore.Utilities;
 using Cysharp.Threading.Tasks;
@@ -31,6 +32,7 @@ namespace CosmicShore.Game.UI
 
         [Header("Intro / Connecting")]
         [SerializeField] private float minConnectingSeconds = 5f;
+        [SerializeField] private PreGameCinematicController preGameCinematic;
 
         [Header("AI Tracking")]
         [SerializeField] protected bool isAIAvailable;
@@ -191,11 +193,28 @@ namespace CosmicShore.Game.UI
 
             try
             {
-                await UniTask.Delay(
-                    TimeSpan.FromSeconds(minConnectingSeconds),
-                    DelayType.DeltaTime,
-                    PlayerLoopTiming.PreUpdate,
-                    ct);
+                // Run pre-game cinematic alongside the minimum connecting timer
+                if (preGameCinematic)
+                {
+                    var cam = Camera.main;
+                    // Run cinematic and minimum timer in parallel — whichever is longer wins
+                    await UniTask.WhenAll(
+                        preGameCinematic.PlayAsync(cam, ct),
+                        UniTask.Delay(
+                            TimeSpan.FromSeconds(minConnectingSeconds),
+                            DelayType.DeltaTime,
+                            PlayerLoopTiming.PreUpdate,
+                            ct)
+                    );
+                }
+                else
+                {
+                    await UniTask.Delay(
+                        TimeSpan.FromSeconds(minConnectingSeconds),
+                        DelayType.DeltaTime,
+                        PlayerLoopTiming.PreUpdate,
+                        ct);
+                }
 
                 if (RequireClientReady)
                 {
diff --git a/Assets/_Scripts/MinigameHUD/View/MinigameHUDView.cs b/Assets/_Scripts/MinigameHUD/View/MinigameHUDView.cs
index 15e62aa67..fa92786c0 100644
--- a/Assets/_Scripts/MinigameHUD/View/MinigameHUDView.cs
+++ b/Assets/_Scripts/MinigameHUD/View/MinigameHUDView.cs
@@ -4,6 +4,8 @@ using UnityEngine.UI;
 using System;
 using System.Collections.Generic;
 using System.Linq;
+using System.Threading;
+using Cysharp.Threading.Tasks;
 
 namespace CosmicShore.Game.UI
 {
@@ -19,10 +21,14 @@ namespace CosmicShore.Game.UI
         [SerializeField] private GameObject pip;
         [SerializeField] private GameObject silhouette;
         [SerializeField] private GameObject trailDisplay;
-        [SerializeField] private CanvasGroup connectingPanelCanvasGroup; 
+        [SerializeField] private CanvasGroup connectingPanelCanvasGroup;
         [SerializeField] private CanvasGroup canvasGroup;
         [SerializeField] private TMP_Text lifeFormCounter;
 
+        [Header("Connecting Panel Animations")]
+        [SerializeField] private DoTweenTypewriterAnimator hackerTextAnimator;
+        [SerializeField] private ConnectingDotsAnimator dotsAnimator;
+
         [Header("Player/AI Score Cards")]
         [SerializeField] private Transform playerScoreContainer;
         [SerializeField] private PlayerScoreCard playerScoreCardPrefab;
@@ -46,11 +52,39 @@ namespace CosmicShore.Game.UI
             canvasGroup.blocksRaycasts = active;
         }
 
+        private CancellationTokenSource _hackerCts;
+
         public void ToggleConnectingPanel(bool active)
         {
             connectingPanelCanvasGroup.alpha = active ? 1 : 0;
             connectingPanelCanvasGroup.interactable = active;
             connectingPanelCanvasGroup.blocksRaycasts = active;
+
+            // Drive connecting-panel animations when present
+            if (active)
+            {
+                if (dotsAnimator) dotsAnimator.gameObject.SetActive(true);
+
+                if (hackerTextAnimator)
+                {
+                    _hackerCts?.Cancel();
+                    _hackerCts?.Dispose();
+                    _hackerCts = new CancellationTokenSource();
+                    hackerTextAnimator.PlayIn(null, _hackerCts.Token).Forget();
+                }
+            }
+            else
+            {
+                if (hackerTextAnimator)
+                {
+                    _hackerCts?.Cancel();
+                    _hackerCts?.Dispose();
+                    _hackerCts = new CancellationTokenSource();
+                    hackerTextAnimator.PlayOut(_hackerCts.Token).Forget();
+                }
+
+                if (dotsAnimator) dotsAnimator.gameObject.SetActive(false);
+            }
         }
 
         public void ClearPlayerList()
diff --git a/Assets/_Scripts/Utility/DataContainers/PreGameCinematicController.cs b/Assets/_Scripts/Utility/DataContainers/PreGameCinematicController.cs
new file mode 100644
index 000000000..8bffdc131
--- /dev/null
+++ b/Assets/_Scripts/Utility/DataContainers/PreGameCinematicController.cs
@@ -0,0 +1,193 @@
+using System;
+using System.Collections.Generic;
```

</details>
