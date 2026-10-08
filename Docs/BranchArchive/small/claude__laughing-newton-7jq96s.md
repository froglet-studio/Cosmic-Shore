# Branch archive: `claude/laughing-newton-7jq96s`

_Snapshot 2026-10-08. Index: [README](../README.md)_

- **Last commit:** 2026-06-23 by Claude
- **Unmerged commits:** 2
- **Forked from:** `380e1db50` (2026-06-20, Update references)
- **Tip:** `d2d0e7a90`
- **Files touched (19):**
  - `Assets/_SO_Assets/GameModeQuest/ProgressionConfig.asset`
  - `Assets/_SO_Assets/Games/ArcadeGameMantaSprawl.asset`
  - `Assets/_SO_Assets/Games/ArcadeGameMantaSprawl.asset.meta`
  - `Assets/_SO_Assets/Games/GameLists/ArcadeGames.asset`
  - `Assets/_SO_Assets/Games/GameLists/LaunchPartyAllGames.asset`
  - `Assets/_SO_Assets/Games/GameLists/OrganicRematchGames.asset`
  - `Assets/_SO_Assets/Scoring Rules/MantaSprawlScoringRule.asset`
  - `Assets/_SO_Assets/Scoring Rules/MantaSprawlScoringRule.asset.meta`
  - `Assets/_Scenes/Multiplayer Scenes/MinigameMantaSprawl.unity`
  - `Assets/_Scenes/Multiplayer Scenes/MinigameMantaSprawl.unity.meta`
  - `Assets/_Scripts/Controller/Arcade/MantaSprawlController.cs`
  - `Assets/_Scripts/Controller/Arcade/MantaSprawlController.cs.meta`
  - `Assets/_Scripts/Controller/Arcade/Scoring/MantaSprawlScoringRuleSO.cs`
  - `Assets/_Scripts/Controller/Arcade/Scoring/MantaSprawlScoringRuleSO.cs.meta`
  - `Assets/_Scripts/Controller/Arcade/Scoring/ScoringMetrics.cs`
  - `Assets/_Scripts/Data/Enums/GameModes.cs`
  - `Assets/_Scripts/Data/Enums/ScoringMetric.cs`
  - `Assets/_Scripts/ScriptableObjects/SO_ProgressionConfig.cs`
  - `ProjectSettings/EditorBuildSettings.asset`

### `a6f3aeef0` — feat(arcade): add Manta "Sprawl" — time-boxed territory party game

_Claude, 2026-06-21 14:53:00 +0000_

```text
First per-vessel party game beyond the Squirrel trio. Sprawl is a time-boxed
land-grab that showcases Manta's signature verb (laying mass): every domain
soars an open arena blooming prisms, and when the 60s clock runs out the
domain with the most territory (summed BlocksCreated) wins. The missing 4th
party genre after race (Skim Race) / combat (Joust) / collect (Scurry).

Built entirely on the existing server-authoritative domain-games framework —
StatsManager records BlocksCreated server-side for every vessel, so the base
controller's per-domain metric sync handles the HUD with no per-prism RPC.

- GameModes.MantaSprawl = 37; ScoringMetric.Blocks = 4 (+ ScoringMetrics.Read)
- MantaSprawlController: mirrors CrystalCapture's end-game flow, keyed on Blocks
- MantaSprawlScoringRuleSO (+ asset): points scoring, "Territory" wording
- Reuses NetworkTimeBasedTurnMonitor (duration 60) — no custom monitor
- MinigameMantaSprawl scene: cloned from Joust, controller/monitor/rule retargeted
- ArcadeGameMantaSprawl card (Sprawl, Manta, 2-12 players, 2 domains, intensity 1-4)
- Registered: ArcadeGames list, EditorBuildSettings, ProgressionConfig (always-unlocked)
```

```text
 Assets/_SO_Assets/GameModeQuest/ProgressionConfig.asset               |    2 +
 Assets/_SO_Assets/Games/ArcadeGameMantaSprawl.asset                   |   35 +
 Assets/_SO_Assets/Games/ArcadeGameMantaSprawl.asset.meta              |    8 +
 Assets/_SO_Assets/Games/GameLists/ArcadeGames.asset                   |    1 +
 Assets/_SO_Assets/Scoring Rules/MantaSprawlScoringRule.asset          |   16 +
 Assets/_SO_Assets/Scoring Rules/MantaSprawlScoringRule.asset.meta     |    8 +
 Assets/_Scenes/Multiplayer Scenes/MinigameMantaSprawl.unity           | 2489 +++++++++++++++++++++++++++++++
 Assets/_Scenes/Multiplayer Scenes/MinigameMantaSprawl.unity.meta      |    7 +
 Assets/_Scripts/Controller/Arcade/MantaSprawlController.cs            |  176 +++
 Assets/_Scripts/Controller/Arcade/MantaSprawlController.cs.meta       |   11 +
 Assets/_Scripts/Controller/Arcade/Scoring/MantaSprawlScoringRuleSO.cs |   61 +
 .../Controller/Arcade/Scoring/MantaSprawlScoringRuleSO.cs.meta        |   11 +
 Assets/_Scripts/Controller/Arcade/Scoring/ScoringMetrics.cs           |    1 +
 Assets/_Scripts/Data/Enums/GameModes.cs                               |    4 +
 Assets/_Scripts/Data/Enums/ScoringMetric.cs                           |    1 +
 Assets/_Scripts/ScriptableObjects/SO_ProgressionConfig.cs             |    4 +-
 ProjectSettings/EditorBuildSettings.asset                             |    3 +
 17 files changed, 2836 insertions(+), 2 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 150 of 309 lines)</summary>

```diff
diff --git a/Assets/_Scripts/Controller/Arcade/MantaSprawlController.cs b/Assets/_Scripts/Controller/Arcade/MantaSprawlController.cs
new file mode 100644
index 000000000..71686471f
--- /dev/null
+++ b/Assets/_Scripts/Controller/Arcade/MantaSprawlController.cs
@@ -0,0 +1,176 @@
+using System.Linq;
+using Unity.Collections;
+using Unity.Netcode;
+using UnityEngine;
+using CosmicShore.Utility;
+using CosmicShore.Data;
+
+namespace CosmicShore.Gameplay
+{
+    /// <summary>
+    /// Manta "Sprawl" — a time-boxed territory land-grab. Every domain soars through open
+    /// space laying mass (prisms); when the clock runs out, the domain that has bloomed the
+    /// most territory (summed <c>BlocksCreated</c>) wins. Points, not golf: each player's
+    /// Score IS their blocks created.
+    ///
+    /// Mirrors <see cref="MultiplayerCrystalCaptureController"/>'s server-authoritative
+    /// end-game flow, but keys off the Blocks metric instead of Crystals and ends on a
+    /// <see cref="NetworkTimeBasedTurnMonitor"/> rather than an objective count. Block counts
+    /// are recorded server-side by <c>StatsManager</c> (the server's local sim spawns every
+    /// vessel's trail), so no per-prism RPC sync is needed — the base controller already
+    /// replicates each domain's metric sum to clients for the HUD.
+    ///
+    /// "Mass is the spine" — see Docs/ECOSYSTEM_MASTERPLAN.md. Sprawl is the territory genre
+    /// expressed directly through conserved mass: you win by creating the most of it.
+    /// </summary>
+    public class MantaSprawlController : MultiplayerDomainGamesController
+    {
+        [Header("Scoring")]
+        [Tooltip("Drag MantaSprawlScoringRule.asset — points scoring keyed on BlocksCreated.")]
+        [SerializeField] ScoringRuleSO rule;
+
+        private bool _finalResultsSent;
+
+        protected override bool UseGolfRules => false;
+        protected override bool UseSceneReloadForReplay => true;
+
+        // Sprawl handles end-game through OnTurnEndedCustom (server-side winner detection) →
+        // SyncFinalScores_ClientRpc, which calls InvokeWinnerCalculated + InvokeMiniGameEnd.
+        // Suppress the base controller's turn→round→game flow so we don't get a duplicate
+        // InvokeWinnerCalculated from SyncGameEnd_ClientRpc.
+        protected override bool HasEndGame => false;
+
+        public override void OnNetworkSpawn()
+        {
+            base.OnNetworkSpawn();
+            gameData.ScoringRule = rule;
+            numberOfRounds = 1;
+            numberOfTurnsPerRound = 1;
+            _finalResultsSent = false;
+        }
+
+        // ── Server-authoritative game end ─────────────────────────────────
+
+        /// <summary>
+        /// Server-side winner detection, mirroring HexRace/Joust/CrystalCapture. Called from
+        /// SyncTurnEnd_ClientRpc BEFORE ExecuteServerTurnEnd → SetupNewRound, so
+        /// _finalResultsSent is set in time to suppress the Ready button. The turn was ended
+        /// by the time monitor, so there is always a winning domain (the highest block sum).
+        /// </summary>
+        protected override void OnTurnEndedCustom()
+        {
+            base.OnTurnEndedCustom();
+            if (!IsServer || _finalResultsSent) return;
+            if (gameData.RoundStatsList == null || gameData.RoundStatsList.Count == 0) return;
+
+            // Winning domain = highest block sum (Jade → Ruby → Gold tie-break) via the rule.
+            var winningDomain = rule.ResolveWinner(gameData);
+            if (winningDomain == Domains.Blue) return;
+
+            // Representative winner-name = best individual contributor on that domain (legacy
+            // display field — victory/defeat attribution uses WinnerDomain).
+            var winnerRep = gameData.RoundStatsList
+                .Where(s => s.Domain == winningDomain)
+                .OrderByDescending(s => s.BlocksCreated)
+                .FirstOrDefault();
+            if (winnerRep == null) return;
+
+            // Per-player Score = blocks created (the rule owns this); domain aggregation in
+            // CalculateDomainStats determines team standing.
+            rule.AssignScores(gameData, winningDomain, 0f);
+
+            gameData.SortRoundStats(UseGolfRules);
+            gameData.CalculateDomainStats(UseGolfRules);
+
+            _finalResultsSent = true;
+            SyncFinalScoresSnapshot(winnerRep.Name, winningDomain);
+        }
+
+        /// <summary>
+        /// Suppress the base flow's SetupNewRound when the game just ended. HasEndGame=false
+        /// causes ExecuteServerRoundEnd to call SetupNewRound instead of ExecuteServerGameEnd —
+        /// this override prevents the Ready button from reappearing.
+        /// </summary>
+        protected override void SetupNewRound()
+        {
+            if (_finalResultsSent) return;
+            base.SetupNewRound();
+        }
+
+        // ── Score sync ───────────────────────────────────────────────────
+
+        void SyncFinalScoresSnapshot(string winnerName, Domains winnerDomain)
+        {
+            var statsList = gameData.RoundStatsList;
+            int count = statsList.Count;
+
+            var nameArray = new FixedString64Bytes[count];
+            var scoreArray = new float[count];
+            var domainArray = new int[count];
+            var blocksArray = new int[count];
+
+            for (int i = 0; i < count; i++)
+            {
+                nameArray[i] = new FixedString64Bytes(statsList[i].Name);
+                scoreArray[i] = statsList[i].Score;
+                domainArray[i] = (int)statsList[i].Domain;
+                blocksArray[i] = statsList[i].BlocksCreated;
+            }
+
+            SyncFinalScores_ClientRpc(nameArray, scoreArray, domainArray, blocksArray,
+                new FixedString64Bytes(winnerName), (int)winnerDomain);
+        }
+
+        [ClientRpc]
+        void SyncFinalScores_ClientRpc(
+            FixedString64Bytes[] names,
+            float[] scores,
+            int[] domains,
+            int[] blocksCreated,
+            FixedString64Bytes winnerName,
+            int winnerDomain)
+        {
+            for (int i = 0; i < names.Length; i++)
+            {
+                string sName = names[i].ToString();
+                var stat = gameData.RoundStatsList.FirstOrDefault(s => s.Name == sName);
+                if (stat == null)
+                {
+                    CSDebug.LogError($"[MantaSprawl] Client could not match RoundStats for '{sName}'. " +
+                                   $"Available: {string.Join(", ", gameData.RoundStatsList.Select(s => $"'{s.Name}'"))}");
+                    continue;
+                }
+                stat.Score = scores[i];
+                stat.Domain = (Domains)domains[i];
```

</details>

### `d2d0e7a90` — fix(arcade): register Sprawl in the live game list (OrganicRematchGames)

_Claude, 2026-06-23 15:14:59 +0000_

```text
The arcade card list is DI-injected (ArcadeExploreView's [Inject] SO_GameList),
and AppManager.prefab registers OrganicRematchGames.asset — the 4 cards shown in
the arcade (Skim Race, Joust, Scurry, Maelstrom). The previous commit added
Sprawl to ArcadeGames.asset, which is NOT the injected list, so the card never
appeared. Add ArcadeGameMantaSprawl to OrganicRematchGames (the live list) and to
LaunchPartyAllGames (the Arcade Screen prefab's serialized fallback). The grid has
10 pre-placed card slots, so the 5th game renders fine.
```

```text
 Assets/_SO_Assets/Games/GameLists/LaunchPartyAllGames.asset | 1 +
 Assets/_SO_Assets/Games/GameLists/OrganicRematchGames.asset | 1 +
 2 files changed, 2 insertions(+)
```
