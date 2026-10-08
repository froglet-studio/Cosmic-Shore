# Branch archive: `claude/restore-block-bandit-2ejW2`

_Snapshot 2026-10-08. Index: [README](../README.md)_

- **Last commit:** 2026-03-27 by Claude
- **Unmerged commits:** 3
- **Open pull request:** https://github.com/froglet-studio/Cosmic-Shore/pull/444
- **Forked from:** `ec58e431a` (2026-03-27, Add meta files)
- **Tip:** `62ba04f59`
- **Files touched (11):**
  - `Assets/_Scenes/MinigameBlockBandit.unity`
  - `Assets/_Scenes/MinigameBlockBandit.unity.meta`
  - `Assets/_Scripts/Game/Arcade/SinglePlayerBlockBanditController.cs`
  - `Assets/_Scripts/Game/Arcade/SinglePlayerBlockBanditController.cs.meta`
  - `Assets/_Scripts/Game/UI/BlockBanditHUD.cs`
  - `Assets/_Scripts/Game/UI/BlockBanditHUD.cs.meta`
  - `Assets/_Scripts/Game/UI/BlockBanditScoreboard.cs`
  - `Assets/_Scripts/Game/UI/BlockBanditScoreboard.cs.meta`
  - `Assets/_Scripts/Utility/DataContainers/BlockBanditEndGameController.cs`
  - `Assets/_Scripts/Utility/DataContainers/BlockBanditEndGameController.cs.meta`
  - `ProjectSettings/EditorBuildSettings.asset`

### `8b35d70cc` — Restore Block Bandit scene from pre-deletion commit

_Claude, 2026-03-27 17:19:13 +0000_

```text
The MinigameBlockBandit.unity scene was accidentally removed in commit
dedbeff4 ("Refactor Cell System by removing CellControlManager"). This
restores the scene file and its meta from the parent of that commit and
re-adds it to EditorBuildSettings so it is included in builds.

The BlockBandit game mode enum, SO assets, and all code references were
never removed and still point to this scene name.
```

```text
 Assets/_Scenes/MinigameBlockBandit.unity      | 1973 +++++++++++++++++++++++++++++++++++++++++++++++++++++++
 Assets/_Scenes/MinigameBlockBandit.unity.meta |    7 +
 ProjectSettings/EditorBuildSettings.asset     |    3 +
 3 files changed, 1983 insertions(+)
```

### `260dcc70c` — Add Block Bandit game mode controller and fix scene

_Claude, 2026-03-27 17:44:12 +0000_

```text
- Create SinglePlayerBlockBanditController extending the base
  SinglePlayerMiniGameControllerBase. Handles ready button, timed
  turns, and triggers the scoreboard on game end.
- Replace broken SinglePlayerFreestyleController reference in the
  scene with the new BlockBanditController.
- Remove deleted CellControlManager from the Environment object
  (was causing missing script errors).
- Add Cell prefab instance to the Environment so nucleus, membrane,
  and flora/fauna spawn at runtime (fixes missing cell/colliders).
- Add BlocksStolen scoring mode alongside HostileVolumeDestroyed.
- Fix Ready button onClick to target the new controller class.
- Scoreboard is already provided by the GameCanvas prefab.
```

```text
 Assets/_Scenes/MinigameBlockBandit.unity                              | 147 ++++++++++++++++++++++++++------
 Assets/_Scripts/Game/Arcade/SinglePlayerBlockBanditController.cs      |  23 +++++
 Assets/_Scripts/Game/Arcade/SinglePlayerBlockBanditController.cs.meta |   8 ++
 3 files changed, 150 insertions(+), 28 deletions(-)
```

<details><summary>Patch (code/doc/text files)</summary>

```diff
diff --git a/Assets/_Scripts/Game/Arcade/SinglePlayerBlockBanditController.cs b/Assets/_Scripts/Game/Arcade/SinglePlayerBlockBanditController.cs
new file mode 100644
index 000000000..31d1d2f20
--- /dev/null
+++ b/Assets/_Scripts/Game/Arcade/SinglePlayerBlockBanditController.cs
@@ -0,0 +1,23 @@
+namespace CosmicShore.Game.Arcade
+{
+    /// <summary>
+    /// Block Bandit game mode — timed single-player mode where the player
+    /// steals blocks to score points before time runs out.
+    /// </summary>
+    public class SinglePlayerBlockBanditController : SinglePlayerMiniGameControllerBase
+    {
+        protected override void SetupNewRound()
+        {
+            RaiseToggleReadyButtonEvent(true);
+            base.SetupNewRound();
+        }
+
+        protected override void EndGame()
+        {
+            base.EndGame();
+            // No EndGameCinematicController in this scene, so trigger
+            // the scoreboard directly after the base EndGame flow.
+            gameData.InvokeShowGameEndScreen();
+        }
+    }
+}
```

</details>

### `62ba04f59` — Add Block Bandit end-game, scoreboard, and HUD following OrganicRematch patterns

_Claude, 2026-03-27 18:00:06 +0000_

```text
- BlockBanditEndGameController: extends EndGameCinematicController with
  PrismStolen-based score reveal (like CrystalCapture/HexRace/Joust)
- BlockBanditScoreboard: extends Scoreboard, sorts by blocks stolen descending
- BlockBanditHUD: extends MultiplayerHUD, tracks PrismStolen stat changes
- Fix SinglePlayerBlockBanditController: remove direct InvokeShowGameEndScreen
  call — EndGameCinematicController handles the full post-game flow
```

```text
 Assets/_Scripts/Game/Arcade/SinglePlayerBlockBanditController.cs      |  9 +----
 Assets/_Scripts/Game/UI/BlockBanditHUD.cs                             | 55 ++++++++++++++++++++++++++++++
 Assets/_Scripts/Game/UI/BlockBanditHUD.cs.meta                        |  8 +++++
 Assets/_Scripts/Game/UI/BlockBanditScoreboard.cs                      | 44 ++++++++++++++++++++++++
 Assets/_Scripts/Game/UI/BlockBanditScoreboard.cs.meta                 |  8 +++++
 .../_Scripts/Utility/DataContainers/BlockBanditEndGameController.cs   | 59 +++++++++++++++++++++++++++++++++
 .../Utility/DataContainers/BlockBanditEndGameController.cs.meta       |  8 +++++
 7 files changed, 183 insertions(+), 8 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 150 of 202 lines)</summary>

```diff
diff --git a/Assets/_Scripts/Game/Arcade/SinglePlayerBlockBanditController.cs b/Assets/_Scripts/Game/Arcade/SinglePlayerBlockBanditController.cs
index 31d1d2f20..f2ad64207 100644
--- a/Assets/_Scripts/Game/Arcade/SinglePlayerBlockBanditController.cs
+++ b/Assets/_Scripts/Game/Arcade/SinglePlayerBlockBanditController.cs
@@ -3,6 +3,7 @@ namespace CosmicShore.Game.Arcade
     /// <summary>
     /// Block Bandit game mode — timed single-player mode where the player
     /// steals blocks to score points before time runs out.
+    /// EndGameCinematicController handles the post-game cinematic and scoreboard trigger.
     /// </summary>
     public class SinglePlayerBlockBanditController : SinglePlayerMiniGameControllerBase
     {
@@ -11,13 +12,5 @@ namespace CosmicShore.Game.Arcade
             RaiseToggleReadyButtonEvent(true);
             base.SetupNewRound();
         }
-
-        protected override void EndGame()
-        {
-            base.EndGame();
-            // No EndGameCinematicController in this scene, so trigger
-            // the scoreboard directly after the base EndGame flow.
-            gameData.InvokeShowGameEndScreen();
-        }
     }
 }
diff --git a/Assets/_Scripts/Game/UI/BlockBanditHUD.cs b/Assets/_Scripts/Game/UI/BlockBanditHUD.cs
new file mode 100644
index 000000000..c8d90ac57
--- /dev/null
+++ b/Assets/_Scripts/Game/UI/BlockBanditHUD.cs
@@ -0,0 +1,55 @@
+using System;
+using System.Collections.Generic;
+using System.Linq;
+
+namespace CosmicShore.Game.UI
+{
+    public class BlockBanditHUD : MultiplayerHUD
+    {
+        private readonly Dictionary<IRoundStats, Action<IRoundStats>> _stolenChangeHandlers = new();
+
+        protected override int GetInitialCardValue(IRoundStats stats)
+        {
+            return stats.PrismStolen;
+        }
+
+        protected override void SubscribeToPlayerStats(IRoundStats stats)
+        {
+            if (stats == null) return;
+
+            Action<IRoundStats> handler = s => UpdatePlayerCard(s.Name, s.PrismStolen);
+            _stolenChangeHandlers[stats] = handler;
+
+            stats.OnPrismsStolenChanged += handler;
+        }
+
+        protected override void UnsubscribeFromPlayerStats(IRoundStats stats)
+        {
+            if (stats == null || !_stolenChangeHandlers.TryGetValue(stats, out var handler)) return;
+            stats.OnPrismsStolenChanged -= handler;
+            _stolenChangeHandlers.Remove(stats);
+        }
+
+        protected override void SubscribeToGameSpecificEvents()
+        {
+            if (gameData != null)
+                gameData.OnMiniGameTurnStarted.OnRaised += RefreshAllPlayerCards;
+        }
+
+        protected override void UnsubscribeFromGameSpecificEvents()
+        {
+            if (gameData != null)
+                gameData.OnMiniGameTurnStarted.OnRaised -= RefreshAllPlayerCards;
+        }
+
+        void RefreshAllPlayerCards()
+        {
+            if (gameData?.RoundStatsList == null) return;
+
+            foreach (var stats in gameData.RoundStatsList.Where(stats => stats != null))
+            {
+                UpdatePlayerCard(stats.Name, stats.PrismStolen);
+            }
+        }
+    }
+}
diff --git a/Assets/_Scripts/Game/UI/BlockBanditScoreboard.cs b/Assets/_Scripts/Game/UI/BlockBanditScoreboard.cs
new file mode 100644
index 000000000..9318ee778
--- /dev/null
+++ b/Assets/_Scripts/Game/UI/BlockBanditScoreboard.cs
@@ -0,0 +1,44 @@
+using CosmicShore.Game.UI;
+
+namespace CosmicShore.Game.Arcade
+{
+    public class BlockBanditScoreboard : Scoreboard
+    {
+        protected override void ShowMultiplayerView()
+        {
+            if (gameData.RoundStatsList is { Count: > 0 })
+            {
+                // Sort by blocks stolen descending — most blocks stolen wins
+                gameData.RoundStatsList.Sort((a, b) => b.PrismStolen.CompareTo(a.PrismStolen));
+                SetBannerForDomain(gameData.RoundStatsList[0].Domain);
+            }
+            else if (BannerText) BannerText.text = "GAME OVER";
+
+            DisplayPlayerScores();
+
+            if (SingleplayerView) SingleplayerView.gameObject.SetActive(false);
+            if (MultiplayerView) MultiplayerView.gameObject.SetActive(true);
+        }
+
+        protected override void DisplayPlayerScores()
+        {
+            var playerScores = gameData.RoundStatsList;
+
+            for (var i = 0; i < playerScores.Count && i < PlayerScoreTextFields.Count; i++)
+            {
+                if (PlayerNameTextFields[i])
+                    PlayerNameTextFields[i].text = playerScores[i].Name;
+
+                if (PlayerScoreTextFields[i])
+                    PlayerScoreTextFields[i].text = $"{playerScores[i].PrismStolen} Blocks Stolen";
+            }
+
+            for (var i = playerScores.Count; i < PlayerNameTextFields.Count; i++)
+            {
+                if (PlayerNameTextFields[i]) PlayerNameTextFields[i].text = "";
+                if (i < PlayerScoreTextFields.Count && PlayerScoreTextFields[i])
+                    PlayerScoreTextFields[i].text = "";
+            }
+        }
+    }
+}
diff --git a/Assets/_Scripts/Utility/DataContainers/BlockBanditEndGameController.cs b/Assets/_Scripts/Utility/DataContainers/BlockBanditEndGameController.cs
new file mode 100644
index 000000000..fa0fb8bd7
--- /dev/null
+++ b/Assets/_Scripts/Utility/DataContainers/BlockBanditEndGameController.cs
@@ -0,0 +1,59 @@
+using System.Collections;
+using System.Linq;
+using CosmicShore.Game.Cinematics;
+using UnityEngine;
+using CosmicShore.Utility;
+
+namespace CosmicShore.Game.Arcade
```

</details>
