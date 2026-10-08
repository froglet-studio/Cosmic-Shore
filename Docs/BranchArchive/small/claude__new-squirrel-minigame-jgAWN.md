# Branch archive: `claude/new-squirrel-minigame-jgAWN`

_Snapshot 2026-10-08. Index: [README](../README.md)_

- **Last commit:** 2026-02-20 by Claude
- **Unmerged commits:** 1
- **Open pull request:** https://github.com/froglet-studio/Cosmic-Shore/pull/81
- **Forked from:** `14817c028` (2026-02-18, Merge pull request #72 from froglet-studio/development)
- **Tip:** `e1b04d51d`
- **Files touched (5):**
  - `Assets/_Scripts/Game/Arcade/AcornHoardMiniGame.cs`
  - `Assets/_Scripts/Game/Arcade/MultiplayerAcornHoardController.cs`
  - `Assets/_Scripts/Game/Arcade/TurnMonitors/NetworkVolumeStolenTurnMonitor.cs`
  - `Assets/_Scripts/Game/Arcade/TurnMonitors/VolumeStolenTurnMonitor.cs`
  - `Assets/_Scripts/Models/Enums/GameModes.cs`

### `e1b04d51d` — Add Acorn Hoard minigame for the Squirrel vessel

_Claude, 2026-02-20 16:39:58 +0000_

```text
New game mode that leverages the Squirrel's core steal, drift, and
boost mechanics. Players race through a dense arena of neutral prism
clusters, competing to steal the most volume. First to reach the
threshold wins (scored by time), losers penalized by remaining volume.

- AcornHoardMiniGame: single-player controller with arena generation
- MultiplayerAcornHoardController: networked controller with
  server-authoritative scoring and ClientRpc snapshot sync
- VolumeStolenTurnMonitor: ends turn when local player hits threshold
- NetworkVolumeStolenTurnMonitor: server-side check across all players
- GameModes enum: AcornHoard = 36, MultiplayerAcornHoard = 37
```

```text
 Assets/_Scripts/Game/Arcade/AcornHoardMiniGame.cs                     |  46 ++++++++
 Assets/_Scripts/Game/Arcade/MultiplayerAcornHoardController.cs        | 181 ++++++++++++++++++++++++++++++++
 .../Game/Arcade/TurnMonitors/NetworkVolumeStolenTurnMonitor.cs        |  58 ++++++++++
 Assets/_Scripts/Game/Arcade/TurnMonitors/VolumeStolenTurnMonitor.cs   |  66 ++++++++++++
 Assets/_Scripts/Models/Enums/GameModes.cs                             |   2 +
 5 files changed, 353 insertions(+)
```

<details><summary>Patch (code/doc/text files, first 150 of 387 lines)</summary>

```diff
diff --git a/Assets/_Scripts/Game/Arcade/AcornHoardMiniGame.cs b/Assets/_Scripts/Game/Arcade/AcornHoardMiniGame.cs
new file mode 100644
index 000000000..60edcfa62
--- /dev/null
+++ b/Assets/_Scripts/Game/Arcade/AcornHoardMiniGame.cs
@@ -0,0 +1,46 @@
+using UnityEngine;
+
+namespace CosmicShore.Game.Arcade
+{
+    /// <summary>
+    /// Acorn Hoard (Single Player): Fly through a dense arena of neutral prism clusters,
+    /// stealing as much volume as possible. Uses the Squirrel's drift and steal mechanics.
+    /// Turn ends when the volume stolen threshold is reached.
+    /// Score = time to reach the threshold (lower is better).
+    /// </summary>
+    public class AcornHoardMiniGame : SinglePlayerMiniGameControllerBase
+    {
+        [Header("Arena")]
+        [SerializeField] SegmentSpawner segmentSpawner;
+        [SerializeField] int baseNumberOfSegments = 20;
+        [SerializeField] bool scaleSegmentsWithIntensity = true;
+
+        int Intensity => Mathf.Max(1, gameData.SelectedIntensity.Value);
+
+        protected override bool UseGolfRules => true;
+
+        protected override void Start()
+        {
+            InitializeArena();
+            base.Start();
+        }
+
+        void InitializeArena()
+        {
+            if (!segmentSpawner) return;
+
+            segmentSpawner.Seed = new System.Random().Next();
+            segmentSpawner.NumberOfSegments = scaleSegmentsWithIntensity
+                ? baseNumberOfSegments * Intensity
+                : baseNumberOfSegments;
+
+            segmentSpawner.Initialize();
+        }
+
+        protected override void OnResetForReplay()
+        {
+            InitializeArena();
+            base.OnResetForReplay();
+        }
+    }
+}
diff --git a/Assets/_Scripts/Game/Arcade/MultiplayerAcornHoardController.cs b/Assets/_Scripts/Game/Arcade/MultiplayerAcornHoardController.cs
new file mode 100644
index 000000000..5fc140b7b
--- /dev/null
+++ b/Assets/_Scripts/Game/Arcade/MultiplayerAcornHoardController.cs
@@ -0,0 +1,181 @@
+// =======================================================
+// MultiplayerAcornHoardController.cs
+// Acorn Hoard: Players race to steal the most prism volume.
+// - Environment is a dense arena of neutral prism clusters
+// - Win condition: first player to steal enough volume, OR
+//   highest volume stolen when time expires
+// - Leverages the Squirrel's steal, drift, and boost mechanics
+// =======================================================
+
+using System.Linq;
+using Unity.Collections;
+using Unity.Netcode;
+using UnityEngine;
+
+namespace CosmicShore.Game.Arcade
+{
+    public class MultiplayerAcornHoardController : MultiplayerDomainGamesController
+    {
+        [Header("Arena")]
+        [SerializeField] SegmentSpawner segmentSpawner;
+        [SerializeField] int baseNumberOfSegments = 20;
+        [SerializeField] int baseStraightLineLength = 200;
+        [SerializeField] bool scaleSegmentsWithIntensity = true;
+
+        [Header("Seed")]
+        [SerializeField] int seed = 0;
+
+        [Header("Acorn Hoard Rules")]
+        [SerializeField] NetworkVolumeStolenTurnMonitor volumeMonitor;
+
+        int Intensity => Mathf.Max(1, gameData.SelectedIntensity.Value);
+
+        private bool _gameEnded;
+
+        protected override bool UseGolfRules => true; // lower time = better
+
+        public override void OnNetworkSpawn()
+        {
+            base.OnNetworkSpawn();
+            numberOfRounds = 1;
+            numberOfTurnsPerRound = 1;
+        }
+
+        protected override void OnCountdownTimerEnded()
+        {
+            if (!IsServer) return;
+
+            int currentSeed = (seed != 0) ? seed : Random.Range(int.MinValue, int.MaxValue);
+            InitializeArena_ClientRpc(currentSeed);
+
+            base.OnCountdownTimerEnded();
+        }
+
+        [ClientRpc]
+        void InitializeArena_ClientRpc(int syncedSeed)
+        {
+            if (!segmentSpawner) return;
+
+            segmentSpawner.Seed = syncedSeed;
+            segmentSpawner.NumberOfSegments = scaleSegmentsWithIntensity
+                ? baseNumberOfSegments * Intensity
+                : baseNumberOfSegments;
+            segmentSpawner.StraightLineLength = baseStraightLineLength;
+
+            segmentSpawner.Initialize();
+        }
+
+        /// <summary>
+        /// Called when a player reaches the volume threshold first.
+        /// Server scores all players: winner gets finish time, losers get penalty + remaining volume.
+        /// </summary>
+        public void ReportLocalPlayerFinished(float finishTimeSeconds)
+        {
+            string myName = gameData.LocalPlayer.Vessel.VesselStatus.PlayerName;
+            ReportPlayerFinished_ServerRpc(finishTimeSeconds, myName);
+        }
+
+        [ServerRpc(RequireOwnership = false)]
+        void ReportPlayerFinished_ServerRpc(float finishTimeSeconds, string playerName)
+        {
+            if (_gameEnded) return;
+            _gameEnded = true;
+
+            float threshold = volumeMonitor != null ? volumeMonitor.VolumeThreshold : 5000f;
+
+            // Winner: finish time as score
+            var winnerStats = gameData.RoundStatsList.FirstOrDefault(s => s.Name == playerName);
+            if (winnerStats != null)
+                winnerStats.Score = finishTimeSeconds;
+
+            // Losers: penalty + remaining volume to steal
+            foreach (var stats in gameData.RoundStatsList)
```

</details>
