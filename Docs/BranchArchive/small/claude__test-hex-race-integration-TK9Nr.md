# Branch archive: `claude/test-hex-race-integration-TK9Nr`

_Snapshot 2026-10-08. Index: [README](../README.md)_

- **Last commit:** 2026-02-26 by Claude
- **Unmerged commits:** 2
- **Forked from:** `438283015` (2026-02-26, Merge pull request #159 from froglet-studio/claude/test-scene-transitions-QSEq)
- **Tip:** `66f5e6537`
- **Files touched (5):**
  - `Assets/_Scripts/Game/Arcade/HexRaceController.cs`
  - `Assets/_Scripts/Game/Arcade/HexRaceStatsProvider.cs`
  - `Assets/_Scripts/Game/UI/HexRaceHUD.cs`
  - `Assets/_Scripts/Game/UI/HexRaceHUDView.cs`
  - `Assets/_Scripts/Utility/DataContainers/HexRaceEndGameController.cs`

### `d1f658abb` — fix(hex-race): resolve integration issues with project architecture

_Claude, 2026-02-26 03:07:55 +0000_

```text
- Add missing `using CosmicShore.Game.Arcade` to HexRaceEndGameController
  so it can resolve the HexRaceController type reference
- Add missing `using CosmicShore.Utility` to HexRaceController for CSDebug
- Add missing `using CosmicShore.Models.Enums` to HexRaceHUD for IRoundStats
- Remove duplicate fields/methods/types from HexRaceHUDView that shadowed
  base MiniGameHUDView members (playerScoreContainer, playerScoreCardPrefab,
  domainColors, ClearPlayerList, GetColorForDomain, DomainColorDef) — these
  caused inspector serialization conflicts where the base class fields
  accessed by MultiplayerHUD would be null
- Implement HexRaceStatsProvider.GetStats() which previously returned null,
  causing NRE in Scoreboard.PopulateDynamicStats(). Now pulls drift time,
  clean streak, and jousts won from HexRaceScoreTracker telemetry data
```

```text
 Assets/_Scripts/Game/Arcade/HexRaceController.cs                   |  2 +-
 Assets/_Scripts/Game/Arcade/HexRaceStatsProvider.cs                | 47 ++++++++++++++++++++++++++++++++----
 Assets/_Scripts/Game/UI/HexRaceHUD.cs                              |  1 +
 Assets/_Scripts/Game/UI/HexRaceHUDView.cs                          | 44 ++++++---------------------------
 Assets/_Scripts/Utility/DataContainers/HexRaceEndGameController.cs |  1 +
 5 files changed, 52 insertions(+), 43 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 150 of 155 lines)</summary>

```diff
diff --git a/Assets/_Scripts/Game/Arcade/HexRaceController.cs b/Assets/_Scripts/Game/Arcade/HexRaceController.cs
index b34f94fde..cc55b950a 100644
--- a/Assets/_Scripts/Game/Arcade/HexRaceController.cs
+++ b/Assets/_Scripts/Game/Arcade/HexRaceController.cs
@@ -4,8 +4,8 @@ using Unity.Collections;
 using Unity.Netcode;
 using UnityEngine;
 using CosmicShore.Game.Environment;
-using CosmicShore.Utility.Recording;
 using CosmicShore.Models.Enums;
+using CosmicShore.Utility;
 
 namespace CosmicShore.Game.Arcade
 {
diff --git a/Assets/_Scripts/Game/Arcade/HexRaceStatsProvider.cs b/Assets/_Scripts/Game/Arcade/HexRaceStatsProvider.cs
index ebdc240a0..6ad2a9e59 100644
--- a/Assets/_Scripts/Game/Arcade/HexRaceStatsProvider.cs
+++ b/Assets/_Scripts/Game/Arcade/HexRaceStatsProvider.cs
@@ -1,4 +1,4 @@
-﻿using System.Collections.Generic;
+using System.Collections.Generic;
 using CosmicShore.Game.UI;
 using UnityEngine;
 
@@ -13,11 +13,48 @@ namespace CosmicShore.Game.Arcade
         [SerializeField] Sprite cleanStreakIcon;
         [SerializeField] Sprite driftIcon;
         [SerializeField] Sprite joustIcon;
-        
+
         public override List<StatData> GetStats()
         {
-    
-            return null;
+            if (!scoreTracker) return new List<StatData>();
+
+            var exposed = scoreTracker.GetExposedStats();
+            if (exposed.Count == 0) return new List<StatData>();
+
+            var stats = new List<StatData>();
+
+            if (exposed.TryGetValue("Longest Drift", out var drift))
+            {
+                float driftVal = drift is float f ? f : 0f;
+                stats.Add(new StatData
+                {
+                    Label = "Longest Drift",
+                    Value = $"{driftVal:F1}s",
+                    Icon = driftIcon
+                });
+            }
+
+            if (exposed.TryGetValue("Max Clean Streak", out var streak))
+            {
+                stats.Add(new StatData
+                {
+                    Label = "Clean Streak",
+                    Value = $"{streak}",
+                    Icon = cleanStreakIcon
+                });
+            }
+
+            if (exposed.TryGetValue("Jousts Won", out var jousts))
+            {
+                stats.Add(new StatData
+                {
+                    Label = "Jousts Won",
+                    Value = $"{jousts}",
+                    Icon = joustIcon
+                });
+            }
+
+            return stats;
         }
     }
-}
\ No newline at end of file
+}
diff --git a/Assets/_Scripts/Game/UI/HexRaceHUD.cs b/Assets/_Scripts/Game/UI/HexRaceHUD.cs
index 7ff2de592..ac35d716a 100644
--- a/Assets/_Scripts/Game/UI/HexRaceHUD.cs
+++ b/Assets/_Scripts/Game/UI/HexRaceHUD.cs
@@ -1,3 +1,4 @@
+using CosmicShore.Models.Enums;
 
 namespace CosmicShore.Game.UI
 {
diff --git a/Assets/_Scripts/Game/UI/HexRaceHUDView.cs b/Assets/_Scripts/Game/UI/HexRaceHUDView.cs
index ed17073fa..911b77fe2 100644
--- a/Assets/_Scripts/Game/UI/HexRaceHUDView.cs
+++ b/Assets/_Scripts/Game/UI/HexRaceHUDView.cs
@@ -1,43 +1,13 @@
-using System;
-using System.Collections.Generic;
-using System.Linq;
-using CosmicShore.Models.Enums;
-using UnityEngine;
-using CosmicShore.Game.UI;
 using CosmicShore.MinigameHUD.View;
+
 namespace CosmicShore.Game.UI
 {
+    /// <summary>
+    /// HexRace-specific HUD view. All shared multiplayer HUD functionality
+    /// (player score cards, domain colors, player list management) is
+    /// inherited from <see cref="MiniGameHUDView"/>.
+    /// </summary>
     public class HexRaceHUDView : MiniGameHUDView
     {
-        [Header("Multiplayer Elements")]
-        [SerializeField] private Transform playerScoreContainer;
-        [SerializeField] private PlayerScoreCard playerScoreCardPrefab;
-
-        [Header("Domain Styling")]
-        [SerializeField] private List<DomainColorDef> domainColors;
-
-        public Transform PlayerScoreContainer => playerScoreContainer;
-        public PlayerScoreCard PlayerScoreCardPrefab => playerScoreCardPrefab;
-
-        public void ClearPlayerList()
-        {
-            foreach (Transform child in playerScoreContainer)
-            {
-                Destroy(child.gameObject);
-            }
-        }
-
-        public Color GetColorForDomain(Domains domain)
-        {
-            var def = domainColors.FirstOrDefault(d => d.Domain == domain);
-            return def.Equals(default(DomainColorDef)) ? Color.white : def.Color;
-        }
-
-        [Serializable]
-        public struct DomainColorDef
-        {
-            public Domains Domain;
-            public Color Color;
-        }
     }
-}
\ No newline at end of file
+}
diff --git a/Assets/_Scripts/Utility/DataContainers/HexRaceEndGameController.cs b/Assets/_Scripts/Utility/DataContainers/HexRaceEndGameController.cs
index 199cb69d6..0f1766d41 100644
--- a/Assets/_Scripts/Utility/DataContainers/HexRaceEndGameController.cs
+++ b/Assets/_Scripts/Utility/DataContainers/HexRaceEndGameController.cs
@@ -1,6 +1,7 @@
 ﻿using System.Collections;
 using System.Linq;
```

</details>

_Also contains 1 unmerged merge commit(s) (branch-sync merges; no authored changes of their own)._
