# Branch archive: `claude/add-tournament-mode-t0bWK`

_Snapshot 2026-10-08. Index: [README](../README.md)_

- **Last commit:** 2026-03-03 by Claude
- **Unmerged commits:** 1
- **Forked from:** `e43b80025` (2026-03-04, Merge pull request #342 from froglet-studio/claude/hex-race-unique-domains-7HB)
- **Tip:** `5bb074ed1`
- **Files touched (13):**
  - `Assets/_Scripts/Data/Enums/GameModes.cs`
  - `Assets/_Scripts/Data/Structs/TournamentRoundResult.cs`
  - `Assets/_Scripts/ScriptableObjects/SO_Tournament.cs`
  - `Assets/_Scripts/ScriptableObjects/SO_TournamentList.cs`
  - `Assets/_Scripts/ScriptableObjects/TournamentEventsContainerSO.cs`
  - `Assets/_Scripts/System/AppManager.cs`
  - `Assets/_Scripts/System/TournamentManager.cs`
  - `Assets/_Scripts/UI/Scoreboard.cs`
  - `Assets/_Scripts/UI/TournamentStandingRowUI.cs`
  - `Assets/_Scripts/UI/TournamentStandingsPanel.cs`
  - `Assets/_Scripts/UI/Views/TournamentConfigureView.cs`
  - `Assets/_Scripts/Utility/DataContainers/GameDataSO.cs`
  - `Assets/_Scripts/Utility/DataContainers/TournamentDataSO.cs`

### `5bb074ed1` — feat(tournament): add modular tournament mode architecture

_Claude, 2026-03-03 23:27:25 +0000_

```text
Tournament mode sequences multiple existing game modes (Crystal Capture,
Hex Race, Joust, etc.) into a single tournament session with cumulative
scoring. Players progress through rounds, see results between each, and
can change vessels between rounds.

Architecture:
- SO_Tournament: static config with ordered List<SO_ArcadeGame> rounds
- TournamentDataSO: SOAP runtime state (current round, standings)
- TournamentEventsContainerSO: SOAP event hub for lifecycle events
- TournamentManager: pure C# DI singleton orchestrating the lifecycle
- TournamentStandingsPanel: between-round UI with standings + vessel select
- TournamentConfigureView: tournament selection and launch UI

Key design decisions:
- Game controllers run completely unmodified — tournament layer sits above
- Placement-based scoring normalizes across golf/normal scoring systems
- Uses existing SceneLoader.LaunchGame() pipeline between rounds
- GameOver → LoadingGame transition already valid in ApplicationStateMachine

Modified: GameModes enum (+Tournament=36), AppManager (DI registration),
GameDataSO (+IsTournamentMode flag), Scoreboard (tournament-aware buttons)
```

```text
 Assets/_Scripts/Data/Enums/GameModes.cs                          |   1 +
 Assets/_Scripts/Data/Structs/TournamentRoundResult.cs            |  33 ++++++
 Assets/_Scripts/ScriptableObjects/SO_Tournament.cs               |  46 ++++++++
 Assets/_Scripts/ScriptableObjects/SO_TournamentList.cs           |  11 ++
 Assets/_Scripts/ScriptableObjects/TournamentEventsContainerSO.cs |  33 ++++++
 Assets/_Scripts/System/AppManager.cs                             |  18 +++
 Assets/_Scripts/System/TournamentManager.cs                      | 220 +++++++++++++++++++++++++++++++++++++
 Assets/_Scripts/UI/Scoreboard.cs                                 |  14 +++
 Assets/_Scripts/UI/TournamentStandingRowUI.cs                    |  28 +++++
 Assets/_Scripts/UI/TournamentStandingsPanel.cs                   | 203 ++++++++++++++++++++++++++++++++++
 Assets/_Scripts/UI/Views/TournamentConfigureView.cs              | 145 ++++++++++++++++++++++++
 Assets/_Scripts/Utility/DataContainers/GameDataSO.cs             |   4 +
 Assets/_Scripts/Utility/DataContainers/TournamentDataSO.cs       |  55 ++++++++++
 13 files changed, 811 insertions(+)
```

<details><summary>Patch (code/doc/text files, first 150 of 954 lines)</summary>

```diff
diff --git a/Assets/_Scripts/Data/Enums/GameModes.cs b/Assets/_Scripts/Data/Enums/GameModes.cs
index 62696f511..768b850fd 100644
--- a/Assets/_Scripts/Data/Enums/GameModes.cs
+++ b/Assets/_Scripts/Data/Enums/GameModes.cs
@@ -39,5 +39,6 @@ namespace CosmicShore.Data
         HexRace = 33,
         MultiplayerJoust = 34,
         MultiplayerCrystalCapture = 35,
+        Tournament = 36,
     }
 }
\ No newline at end of file
diff --git a/Assets/_Scripts/Data/Structs/TournamentRoundResult.cs b/Assets/_Scripts/Data/Structs/TournamentRoundResult.cs
new file mode 100644
index 000000000..a361eee49
--- /dev/null
+++ b/Assets/_Scripts/Data/Structs/TournamentRoundResult.cs
@@ -0,0 +1,33 @@
+using System;
+using System.Collections.Generic;
+
+namespace CosmicShore.Data
+{
+    [Serializable]
+    public struct TournamentPlayerScore
+    {
+        public string PlayerName;
+        public Domains Domain;
+        public float RawScore;
+        public int Placement;
+        public int PointsAwarded;
+    }
+
+    [Serializable]
+    public struct TournamentRoundResult
+    {
+        public int RoundIndex;
+        public GameModes GameMode;
+        public string GameDisplayName;
+        public List<TournamentPlayerScore> PlayerScores;
+    }
+
+    [Serializable]
+    public struct TournamentStanding
+    {
+        public string PlayerName;
+        public Domains Domain;
+        public int TotalPoints;
+        public List<int> PointsPerRound;
+    }
+}
diff --git a/Assets/_Scripts/ScriptableObjects/SO_Tournament.cs b/Assets/_Scripts/ScriptableObjects/SO_Tournament.cs
new file mode 100644
index 000000000..3c45bd919
--- /dev/null
+++ b/Assets/_Scripts/ScriptableObjects/SO_Tournament.cs
@@ -0,0 +1,46 @@
+using System.Collections.Generic;
+using UnityEngine;
+
+namespace CosmicShore.ScriptableObjects
+{
+    [CreateAssetMenu(fileName = "New Tournament", menuName = "ScriptableObjects/Game/Tournament")]
+    public class SO_Tournament : ScriptableObject
+    {
+        [Header("Tournament Config")]
+        [Tooltip("Display name shown in the tournament selection UI.")]
+        public string TournamentName;
+
+        [TextArea(2, 4)]
+        [Tooltip("Description shown in the tournament selection UI.")]
+        public string Description;
+
+        [Tooltip("Icon shown in the tournament selection UI.")]
+        public Sprite Icon;
+
+        [Header("Rounds")]
+        [Tooltip("Ordered list of game modes played in sequence. Each entry is an existing SO_ArcadeGame asset.")]
+        public List<SO_ArcadeGame> Rounds;
+
+        [Header("Scoring")]
+        [Tooltip("Points awarded per placement. Index 0 = 1st place, index 1 = 2nd place, etc. Default: [4, 2, 1, 0].")]
+        public List<int> PointsPerPlacement = new() { 4, 2, 1, 0 };
+
+        [Header("Defaults")]
+        [Range(1, 4)]
+        [Tooltip("Default total player count (humans + AI).")]
+        public int DefaultPlayerCount = 2;
+
+        [Range(1, 4)]
+        [Tooltip("Default intensity level for all rounds.")]
+        public int DefaultIntensity = 1;
+
+        public int GetPointsForPlacement(int placement)
+        {
+            if (PointsPerPlacement == null || PointsPerPlacement.Count == 0)
+                return 0;
+
+            int index = placement - 1;
+            return index >= 0 && index < PointsPerPlacement.Count ? PointsPerPlacement[index] : 0;
+        }
+    }
+}
diff --git a/Assets/_Scripts/ScriptableObjects/SO_TournamentList.cs b/Assets/_Scripts/ScriptableObjects/SO_TournamentList.cs
new file mode 100644
index 000000000..41563b9c2
--- /dev/null
+++ b/Assets/_Scripts/ScriptableObjects/SO_TournamentList.cs
@@ -0,0 +1,11 @@
+using System.Collections.Generic;
+using UnityEngine;
+
+namespace CosmicShore.ScriptableObjects
+{
+    [CreateAssetMenu(fileName = "New Tournament List", menuName = "ScriptableObjects/Game/TournamentList")]
+    public class SO_TournamentList : ScriptableObject
+    {
+        public List<SO_Tournament> Tournaments;
+    }
+}
diff --git a/Assets/_Scripts/ScriptableObjects/TournamentEventsContainerSO.cs b/Assets/_Scripts/ScriptableObjects/TournamentEventsContainerSO.cs
new file mode 100644
index 000000000..125a7ed90
--- /dev/null
+++ b/Assets/_Scripts/ScriptableObjects/TournamentEventsContainerSO.cs
@@ -0,0 +1,33 @@
+using Obvious.Soap;
+using UnityEngine;
+
+namespace CosmicShore.ScriptableObjects
+{
+    /// <summary>
+    /// SOAP event container for tournament lifecycle events.
+    /// Raised by <see cref="Core.TournamentManager"/> and consumable by any system via
+    /// inspector-wired EventListeners or code subscription.
+    /// </summary>
+    [CreateAssetMenu(
+        fileName = "TournamentEvents",
+        menuName = "ScriptableObjects/Data Containers/TournamentEvents")]
+    public class TournamentEventsContainerSO : ScriptableObject
+    {
+        [Header("Tournament Lifecycle")]
+        [Tooltip("Raised when a tournament session begins. Subscribers should disable menu navigation and prepare tournament UI.")]
+        public ScriptableEventNoParam OnTournamentStarted;
+
+        [Tooltip("Raised when a tournament round's scores have been captured and standings updated. Subscribers should display the TournamentStandingsPanel.")]
+        public ScriptableEventNoParam OnTournamentRoundCaptured;
+
+        [Tooltip("Raised when advancing to the next round (scene is about to load). Subscribers should show a loading/transition state.")]
+        public ScriptableEventNoParam OnTournamentAdvancing;
```

</details>
