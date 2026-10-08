# Branch archive: `code-scraper`

_Snapshot 2026-10-08. Index: [README](../README.md)_

- **Last commit:** 2024-05-20 by Emmanuel Eytan
- **Unmerged commits:** 3
- **Forked from:** `0e58795d9` (2024-05-16, Small changes)
- **Tip:** `7dc13a7b8`
- **Files touched (3):**
  - `Assets/_Scripts/Game/Arcade/CourseMiniGame.cs`
  - `Assets/_Scripts/Utility/Tools/CustomAttributes.cs`
  - `Assets/_Scripts/Utility/Tools/CustomAttributes.cs.meta`

### `aa05b79b7` — Attribute defined and callback started.

_Emmanuel Eytan, 2024-05-17 23:23:01 -0700_

```text
 Assets/_Scripts/Game/Arcade/CourseMiniGame.cs          | 113 ++++++++++++++++++++++++-----------------------
 Assets/_Scripts/Utility/Tools/CustomAttributes.cs      |  26 +++++++++++
 Assets/_Scripts/Utility/Tools/CustomAttributes.cs.meta |  11 +++++
 3 files changed, 95 insertions(+), 55 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 150 of 157 lines)</summary>

```diff
diff --git a/Assets/_Scripts/Game/Arcade/CourseMiniGame.cs b/Assets/_Scripts/Game/Arcade/CourseMiniGame.cs
index 1f45b5923..6cc5d9c96 100644
--- a/Assets/_Scripts/Game/Arcade/CourseMiniGame.cs
+++ b/Assets/_Scripts/Game/Arcade/CourseMiniGame.cs
@@ -1,61 +1,64 @@
 using CosmicShore.Environment.FlowField;
+using CosmicShore.Utility.Attributes;
 using UnityEngine;
 
 namespace CosmicShore.Game.Arcade
 {
-    public class CourseMiniGame : MiniGame
-    {
-        [SerializeField] Crystal Crystal;
-        [SerializeField] Vector3 CrystalStartPosition;
-        [SerializeField] SegmentSpawner SegmentSpawner;
-        [SerializeField] int numberOfSegments = 10;
-        [SerializeField] int straightLineLength = 400;
-        [SerializeField] bool ResetTrails = true;
-        [SerializeField] bool ScaleCrystalPositionWithIntensity = false; 
-        [SerializeField] bool ScaleLengthWithIntensity = true;
-        [SerializeField] bool ScaleNumberOfSegmentsWithIntensity = true;
-        [SerializeField] SpawnableHelix helix;
-
-
-        //public static virtual ShipTypes PlayerShipType = ShipTypes.Rhino;
-
-        protected override void Start()
-        {
-            base.Start();
-            SegmentSpawner.Seed = new System.Random().Next();
-            if (ScaleNumberOfSegmentsWithIntensity) numberOfSegments = numberOfSegments * IntensityLevel;
-
-            if (PlayerShipType == ShipTypes.Rhino)
-                ScoreTracker.ScoringMode = ScoringModes.HostileVolumeDestroyed;
-
-            if (helix) helix.firstOrderRadius = helix.secondOrderRadius = IntensityLevel / 1.3f;
-
-            if (!ResetTrails)
-            {
-                InitializeTrails();
-            }
-        }
-
-        protected override void SetupTurn()
-        {
-            base.SetupTurn();
-
-            if (ResetTrails)
-            {
-                InitializeTrails();
-            }
-        }
-
-        void InitializeTrails()
-        {
-            if (ScaleNumberOfSegmentsWithIntensity) SegmentSpawner.numberOfSegments = numberOfSegments;
-            if (ScaleLengthWithIntensity) SegmentSpawner.StraightLineLength = straightLineLength / IntensityLevel;
-
-            TrailSpawner.NukeTheTrails();
-            if (ScaleCrystalPositionWithIntensity) Crystal.transform.position = IntensityLevel * CrystalStartPosition;
-            else Crystal.transform.position = CrystalStartPosition;
-
-            SegmentSpawner.Initialize();
-        }
-    }
+	[MinigameName("Course")]
+	public class CourseMiniGame : MiniGame
+	{
+		[SerializeField] Crystal Crystal;
+		[SerializeField] Vector3 CrystalStartPosition;
+		[SerializeField] SegmentSpawner SegmentSpawner;
+		[SerializeField] int numberOfSegments = 10;
+		[SerializeField] int straightLineLength = 400;
+		[SerializeField] bool ResetTrails = true;
+		[SerializeField] bool ScaleCrystalPositionWithIntensity = false; 
+		[SerializeField] bool ScaleLengthWithIntensity = true;
+		[SerializeField] bool ScaleNumberOfSegmentsWithIntensity = true;
+		[SerializeField] SpawnableHelix helix;
+		
+
+
+		//public static virtual ShipTypes PlayerShipType = ShipTypes.Rhino;
+
+		protected override void Start()
+		{
+			base.Start();
+			SegmentSpawner.Seed = new System.Random().Next();
+			if (ScaleNumberOfSegmentsWithIntensity) numberOfSegments = numberOfSegments * IntensityLevel;
+
+			if (PlayerShipType == ShipTypes.Rhino)
+				ScoreTracker.ScoringMode = ScoringModes.HostileVolumeDestroyed;
+
+			if (helix) helix.firstOrderRadius = helix.secondOrderRadius = IntensityLevel / 1.3f;
+
+			if (!ResetTrails)
+			{
+				InitializeTrails();
+			}
+		}
+
+		protected override void SetupTurn()
+		{
+			base.SetupTurn();
+
+			if (ResetTrails)
+			{
+				InitializeTrails();
+			}
+		}
+
+		void InitializeTrails()
+		{
+			if (ScaleNumberOfSegmentsWithIntensity) SegmentSpawner.numberOfSegments = numberOfSegments;
+			if (ScaleLengthWithIntensity) SegmentSpawner.StraightLineLength = straightLineLength / IntensityLevel;
+
+			TrailSpawner.NukeTheTrails();
+			if (ScaleCrystalPositionWithIntensity) Crystal.transform.position = IntensityLevel * CrystalStartPosition;
+			else Crystal.transform.position = CrystalStartPosition;
+
+			SegmentSpawner.Initialize();
+		}
+	}
 }
\ No newline at end of file
diff --git a/Assets/_Scripts/Utility/Tools/CustomAttributes.cs b/Assets/_Scripts/Utility/Tools/CustomAttributes.cs
new file mode 100644
index 000000000..c64240af7
--- /dev/null
+++ b/Assets/_Scripts/Utility/Tools/CustomAttributes.cs
@@ -0,0 +1,26 @@
+using System;
+using UnityEditor;
+using UnityEditor.Build.Reporting;
+using UnityEngine;
+
+namespace CosmicShore.Utility.Attributes
+{
+	[AttributeUsage(System.AttributeTargets.Class)]
+	class MinigameNameAttribute : System.Attribute
+	{
+		private String Name;
+		
+		public MinigameNameAttribute(String name)
+		{ 
+			Name = name;
+		}
+	}
+	
+	class MetedataWriter: AssetPostprocessor
```

</details>

### `f47bc56b3` — The attribute is picked up and collected.

_Emmanuel Eytan, 2024-05-20 13:58:41 -0700_

```text
 Assets/_Scripts/Utility/Tools/CustomAttributes.cs | 28 ++++++++++++++++++++++++----
 1 file changed, 24 insertions(+), 4 deletions(-)
```

<details><summary>Patch (code/doc/text files)</summary>

```diff
diff --git a/Assets/_Scripts/Utility/Tools/CustomAttributes.cs b/Assets/_Scripts/Utility/Tools/CustomAttributes.cs
index c64240af7..72ce157f7 100644
--- a/Assets/_Scripts/Utility/Tools/CustomAttributes.cs
+++ b/Assets/_Scripts/Utility/Tools/CustomAttributes.cs
@@ -1,14 +1,18 @@
 using System;
+using System.Reflection;
 using UnityEditor;
-using UnityEditor.Build.Reporting;
 using UnityEngine;
+using CosmicShore.Game.Arcade;
+using Mono.CSharp;
+using System.Linq;
+using QFSW.QC.Utilities;
 
 namespace CosmicShore.Utility.Attributes
 {
 	[AttributeUsage(System.AttributeTargets.Class)]
 	class MinigameNameAttribute : System.Attribute
 	{
-		private String Name;
+		public String Name { get; set; }
 		
 		public MinigameNameAttribute(String name)
 		{ 
@@ -18,9 +22,25 @@ namespace CosmicShore.Utility.Attributes
 	
 	class MetedataWriter: AssetPostprocessor
 	{
-		public void OnPostprocessAllAssets()
+		static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets, string[] movedAssets, string[] movedFromAssetPaths, bool didDomainReload)
 		{
-			Debug.Log("Before or not");
+			Type[] minigameTypes = typeof(MiniGame).Assembly.GetTypes();
+			
+			foreach (Type gameType in minigameTypes)
+			{
+				if (!gameType.IsSubclassOf(typeof(MiniGame)) || gameType == typeof(MiniGame))
+				{
+					continue;
+				}
+				// gameType is certain to derive from MiniGame.
+
+				MinigameNameAttribute attr = gameType.GetTypeInfo().GetCustomAttribute<MinigameNameAttribute>(true);
+				if (attr == null)
+				{
+					continue;
+				}
+				Debug.Log($"Name of minigame: {attr.Name}") ;
+			}
 		}
 	}
 }
```

</details>

### `7dc13a7b8` — Added docs. They should have been written earlier. Oops.

_Emmanuel Eytan, 2024-05-20 14:39:53 -0700_

```text
 Assets/_Scripts/Utility/Tools/CustomAttributes.cs | 24 +++++++++++++++++++++++-
 1 file changed, 23 insertions(+), 1 deletion(-)
```

<details><summary>Patch (code/doc/text files)</summary>

```diff
diff --git a/Assets/_Scripts/Utility/Tools/CustomAttributes.cs b/Assets/_Scripts/Utility/Tools/CustomAttributes.cs
index 72ce157f7..84967c919 100644
--- a/Assets/_Scripts/Utility/Tools/CustomAttributes.cs
+++ b/Assets/_Scripts/Utility/Tools/CustomAttributes.cs
@@ -7,8 +7,18 @@ using Mono.CSharp;
 using System.Linq;
 using QFSW.QC.Utilities;
 
+
 namespace CosmicShore.Utility.Attributes
 {
+	/// <summary>
+	/// A class-only attribute that adds the name of a minigame as metadata.
+	/// <example>
+	/// [MinigameName("FunGame")]
+	/// public class FunGame: Minigame
+	/// {
+	///    /* ... */
+	/// }
+	/// </summary>
 	[AttributeUsage(System.AttributeTargets.Class)]
 	class MinigameNameAttribute : System.Attribute
 	{
@@ -19,9 +29,21 @@ namespace CosmicShore.Utility.Attributes
 			Name = name;
 		}
 	}
-	
+
+	/// <summary>
+	/// A Unity post-processor that will run every time the asset library is refreshed
+	/// and look for every instance of the MinigameName attribute.
+	/// </summary>
 	class MetedataWriter: AssetPostprocessor
 	{
+		/// <summary>
+		/// An implementetion of the <c cref="https://docs.unity3d.com/2021.3/Documentation/ScriptReference/AssetPostprocessor">AssetPostprocessor</c>'s
+		/// <c cref="https://docs.unity3d.com/2021.3/Documentation/ScriptReference/AssetPostprocessor.OnPostprocessAllAssets.html">OnPostprocessAllAssets</c> member.
+		/// Looks for every class that extends <c cref="CosmicShore.Game.Arcade.MiniGame"/> (but not <c>Minigame</c> itself) and
+		/// collects all of the <c>name</c> arguments added to them through instances of the <c cref="MinigameNameAttribute" /> attribute.
+		///
+		/// None of the arguments of the method are used in this implementation.
+		/// </summary>
 		static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets, string[] movedAssets, string[] movedFromAssetPaths, bool didDomainReload)
 		{
 			Type[] minigameTypes = typeof(MiniGame).Assembly.GetTypes();
```

</details>
