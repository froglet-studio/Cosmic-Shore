# Branch archive: `claude/fix-double-game-end-78h5q`

_Snapshot 2026-10-08. Index: [README](../README.md)_

- **Last commit:** 2026-03-03 by Claude
- **Unmerged commits:** 2
- **Forked from:** `c99fec59e` (2026-03-04, Update MinigameHexRace.unity)
- **Tip:** `c46306101`
- **Files touched (4):**
  - `Assets/_Scripts/Controller/Arcade/HexRaceController.cs`
  - `Assets/_Scripts/Controller/Arcade/MultiplayerDomainGamesController.cs`
  - `Assets/_Scripts/Controller/Arcade/MultiplayerJoustController.cs`
  - `Assets/_Scripts/Controller/Arcade/NetworkScoreTracker.cs`

### `47946a187` — fix(joust): remove duplicate game-end events from SyncJoustResults_ClientRpc

_Claude, 2026-03-03 20:38:46 +0000_

```text
InvokeWinnerCalculated() and InvokeMiniGameEnd() were firing twice on
all clients — once from SyncJoustResults_ClientRpc and again from the
base class SyncGameEnd_ClientRpc pipeline. Remove the calls from the
Joust RPC and let the base class fire them exactly once, matching the
pattern used by HexRace.
```

```text
 Assets/_Scripts/Controller/Arcade/MultiplayerJoustController.cs | 3 ---
 1 file changed, 3 deletions(-)
```

<details><summary>Patch (code/doc/text files)</summary>

```diff
diff --git a/Assets/_Scripts/Controller/Arcade/MultiplayerJoustController.cs b/Assets/_Scripts/Controller/Arcade/MultiplayerJoustController.cs
index c919814d0..1ce8ee5d1 100644
--- a/Assets/_Scripts/Controller/Arcade/MultiplayerJoustController.cs
+++ b/Assets/_Scripts/Controller/Arcade/MultiplayerJoustController.cs
@@ -170,9 +170,6 @@ namespace CosmicShore.Gameplay
 
             CSDebug.Log($"[JoustController] Client synced. Winner='{WinnerName}' " +
                       $"Order=[{string.Join(", ", gameData.RoundStatsList.Select(s => $"{s.Name}:{s.Score:F1}"))}]");
-
-            gameData.InvokeWinnerCalculated();
-            gameData.InvokeMiniGameEnd();
         }
 
         protected override void OnResetForReplayCustom()
```

</details>

### `c46306101` — fix(multiplayer): remove duplicate game-end events from HexRace, DomainGames, and NetworkScoreTracker

_Claude, 2026-03-03 21:46:46 +0000_

```text
Three sites besides the already-fixed Joust were firing InvokeWinnerCalculated/InvokeMiniGameEnd
redundantly with the base class SyncGameEnd_ClientRpc pipeline:

1. HexRaceController.SyncFinalScores_ClientRpc — removed duplicate event calls (data sync retained)
2. MultiplayerDomainGamesController.EndGame — removed dead code override that was never called by
   the multiplayer pipeline but would cause asymmetric event firing if invoked
3. NetworkScoreTracker.SendRoundStats_ClientRpc — replaced SortAndInvokeResults() with inline
   sort+calculate to preserve the 500ms delayed data re-sync without re-firing events
```

```text
 Assets/_Scripts/Controller/Arcade/HexRaceController.cs                |  4 ++--
 Assets/_Scripts/Controller/Arcade/MultiplayerDomainGamesController.cs | 18 ------------------
 Assets/_Scripts/Controller/Arcade/NetworkScoreTracker.cs              |  5 ++++-
 3 files changed, 6 insertions(+), 21 deletions(-)
```

<details><summary>Patch (code/doc/text files)</summary>

```diff
diff --git a/Assets/_Scripts/Controller/Arcade/HexRaceController.cs b/Assets/_Scripts/Controller/Arcade/HexRaceController.cs
index b5b1db056..db401d6a3 100644
--- a/Assets/_Scripts/Controller/Arcade/HexRaceController.cs
+++ b/Assets/_Scripts/Controller/Arcade/HexRaceController.cs
@@ -248,8 +248,8 @@ namespace CosmicShore.Gameplay
 
             gameData.SortRoundStats(UseGolfRules);
             gameData.CalculateDomainStats(UseGolfRules);
-            gameData.InvokeWinnerCalculated();
-            gameData.InvokeMiniGameEnd();
+            // InvokeWinnerCalculated + InvokeMiniGameEnd are fired by the base class
+            // via SyncGameEnd_ClientRpc() — do not duplicate here.
         }
 
         protected override void OnResetForReplayCustom()
diff --git a/Assets/_Scripts/Controller/Arcade/MultiplayerDomainGamesController.cs b/Assets/_Scripts/Controller/Arcade/MultiplayerDomainGamesController.cs
index bd248bbdf..fa1eea045 100644
--- a/Assets/_Scripts/Controller/Arcade/MultiplayerDomainGamesController.cs
+++ b/Assets/_Scripts/Controller/Arcade/MultiplayerDomainGamesController.cs
@@ -1,4 +1,3 @@
-using System.Collections;
 using System.Linq;
 using CosmicShore.UI;
 using Cysharp.Threading.Tasks;
@@ -102,23 +101,6 @@ namespace CosmicShore.Gameplay
             base.OnResetForReplay();
         }
 
-        protected override void EndGame()
-        {
-            if (!ShowEndGameSequence) return;
-            gameData.SortRoundStats(UseGolfRules);
-            gameData.InvokeWinnerCalculated();
-            if (IsServer)
-            {
-                StartCoroutine(EndGameSyncRoutine());
-            }
-        }
-
-        private IEnumerator EndGameSyncRoutine()
-        {
-            yield return new WaitForSeconds(0.25f);
-            gameData.InvokeMiniGameEnd();
-        }
-
         protected override void OnPlayerLeavingFromSession(string clientId)
         {
             if (ulong.TryParse(clientId, out var id) &&
diff --git a/Assets/_Scripts/Controller/Arcade/NetworkScoreTracker.cs b/Assets/_Scripts/Controller/Arcade/NetworkScoreTracker.cs
index dbfe9b4e0..95d249f32 100644
--- a/Assets/_Scripts/Controller/Arcade/NetworkScoreTracker.cs
+++ b/Assets/_Scripts/Controller/Arcade/NetworkScoreTracker.cs
@@ -43,7 +43,10 @@ namespace CosmicShore.Gameplay
         [ClientRpc]
         private void SendRoundStats_ClientRpc()
         {
-            SortAndInvokeResults();
+            // Sync sorted stats to all clients. Do not call InvokeWinnerCalculated() —
+            // the base controller already fired it via SyncGameEnd_ClientRpc().
+            gameData.SortRoundStats(golfRules);
+            gameData.CalculateDomainStats(golfRules);
         }
     }
 }
\ No newline at end of file
```

</details>
