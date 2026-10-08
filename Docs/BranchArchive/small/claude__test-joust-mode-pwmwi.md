# Branch archive: `claude/test-joust-mode-pwmwi`

_Snapshot 2026-10-08. Index: [README](../README.md)_

- **Last commit:** 2026-03-03 by Claude
- **Unmerged commits:** 1
- **Forked from:** `c99fec59e` (2026-03-04, Update MinigameHexRace.unity)
- **Tip:** `cc36847c2`
- **Files touched (3):**
  - `Assets/_SO_Assets/Games/ArcadeGameMultiplayerJoust.asset`
  - `Assets/_Scripts/Controller/Arcade/MultiplayerJoustController.cs`
  - `Assets/_Scripts/Controller/Arcade/TurnMonitors/TurnMonitor.cs`

### `cc36847c2` — fix(joust): remove double game-end fire and clean up turn monitor

_Claude, 2026-03-03 21:03:52 +0000_

```text
- Remove InvokeWinnerCalculated + InvokeMiniGameEnd from
  SyncJoustResults_ClientRpc — the base class SyncGameEnd_ClientRpc
  already fires both once. The Joust RPC now only syncs data
  (WinnerName, ResultsReady, scores) before the base fires events.
- Clean up TurnMonitor.Update() — remove commented-out code blocks
  and stale inline comments.
- Remove commented-out UniTask.Delay in TurnMonitor async loop.
- Fix GolfScoring mismatch in ArcadeGameMultiplayerJoust.asset:
  set to true to match the controller's UseGolfRules override,
  fixing leaderboard score display.
```

```text
 Assets/_SO_Assets/Games/ArcadeGameMultiplayerJoust.asset        |  2 +-
 Assets/_Scripts/Controller/Arcade/MultiplayerJoustController.cs |  5 +++--
 Assets/_Scripts/Controller/Arcade/TurnMonitors/TurnMonitor.cs   | 17 +++--------------
 3 files changed, 7 insertions(+), 17 deletions(-)
```

<details><summary>Patch (code/doc/text files)</summary>

```diff
diff --git a/Assets/_Scripts/Controller/Arcade/MultiplayerJoustController.cs b/Assets/_Scripts/Controller/Arcade/MultiplayerJoustController.cs
index c919814d0..f74ddf31e 100644
--- a/Assets/_Scripts/Controller/Arcade/MultiplayerJoustController.cs
+++ b/Assets/_Scripts/Controller/Arcade/MultiplayerJoustController.cs
@@ -171,8 +171,9 @@ namespace CosmicShore.Gameplay
             CSDebug.Log($"[JoustController] Client synced. Winner='{WinnerName}' " +
                       $"Order=[{string.Join(", ", gameData.RoundStatsList.Select(s => $"{s.Name}:{s.Score:F1}"))}]");
 
-            gameData.InvokeWinnerCalculated();
-            gameData.InvokeMiniGameEnd();
+            // Game-end events (InvokeWinnerCalculated + InvokeMiniGameEnd) are NOT raised here.
+            // The base class fires them once via SyncGameEnd_ClientRpc, which arrives after this RPC.
+            // Data (WinnerName, ResultsReady, scores) is already set above for subscribers to read.
         }
 
         protected override void OnResetForReplayCustom()
diff --git a/Assets/_Scripts/Controller/Arcade/TurnMonitors/TurnMonitor.cs b/Assets/_Scripts/Controller/Arcade/TurnMonitors/TurnMonitor.cs
index 3093595aa..74361e127 100644
--- a/Assets/_Scripts/Controller/Arcade/TurnMonitors/TurnMonitor.cs
+++ b/Assets/_Scripts/Controller/Arcade/TurnMonitors/TurnMonitor.cs
@@ -39,18 +39,11 @@ namespace CosmicShore.Gameplay
 
         private void Update()
         {
-            if (!isRunning) 
+            if (!isRunning)
                 return;
-            if (!CheckForEndOfTurn()) return; // <-- REMOVE the 'return' on the next line
+            if (!CheckForEndOfTurn()) return;
             OnTurnEnded();
-            StopMonitor(); // Stop the async loop
-            // // End-of-turn check
-            // if (CheckForEndOfTurn())  // <-- REMOVE the 'return' on the next line
-            // {
-            //     OnTurnEnded();
-            //     StopMonitor(); // Stop the async loop
-            // }
-            // Pause(); // exits loop on next iteration
+            StopMonitor();
         }
 
         /// <summary>Stops the monitor loop (safe to call multiple times).</summary>
@@ -121,10 +114,6 @@ namespace CosmicShore.Gameplay
                     // Wait for next tick (game-time)
                     if (_updateInterval > 0f)
                         await UniTask.WaitForSeconds(_updateInterval, cancellationToken: token);
-                        /*await UniTask.Delay(TimeSpan.FromSeconds(_updateInterval),
-                                            DelayType.DeltaTime,
-                                            PlayerLoopTiming.Update,
-                                            token);*/
                     else
                         await UniTask.Yield(PlayerLoopTiming.Update, token);
                 }
```

</details>
