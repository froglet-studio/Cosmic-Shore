# Branch archive: `claude/astro-league-game-dPwTO`

_Snapshot 2026-10-08. Index: [README](../README.md). **Large branch — protected from automatic deletion.**_

<!-- SUMMARY -->

- **Last commit:** 2026-06-12 by Claude
- **Unmerged commits:** 14
- **Forked from:** `be4afb009` (2026-06-11, docs(ecosystem): lock in universality — one HyperSea rule set, no exempt con)
- **Tip:** `4c0f2817e`
- **Files touched (53):**
  - `Assets/_SO_Assets/Games/ArcadeGameAstroLeague.asset`
  - `Assets/_SO_Assets/Games/ArcadeGameAstroLeague.asset.meta`
  - `Assets/_SO_Assets/Games/AstroLeagueSettings.asset`
  - `Assets/_SO_Assets/Games/AstroLeagueSettings.asset.meta`
  - `Assets/_SO_Assets/Games/GameLists/AllGames.asset`
  - `Assets/_SO_Assets/Games/GameLists/ArcadeGames.asset`
  - `Assets/_SO_Assets/Games/GameLists/OrganicRematchGames.asset`
  - `Assets/_SO_Assets/SOAP/Event Channels/EventOnAstroLeagueScoreUpdated.asset`
  - `Assets/_SO_Assets/SOAP/Event Channels/EventOnAstroLeagueScoreUpdated.asset.meta`
  - `Assets/_Scenes/Singleplayer Scenes/MinigameAstroLeague.unity`
  - `Assets/_Scenes/Singleplayer Scenes/MinigameAstroLeague.unity.meta`
  - `Assets/_Scripts/Controller/AI/AIPilot.cs`
  - `Assets/_Scripts/Controller/Arcade/ASTROLEAGUE.md`
  - `Assets/_Scripts/Controller/Arcade/ASTROLEAGUE.md.meta`
  - `Assets/_Scripts/Controller/Arcade/AstroLeague/AstroLeagueArena.cs`
  - `Assets/_Scripts/Controller/Arcade/AstroLeague/AstroLeagueBall.cs`
  - `Assets/_Scripts/Controller/Arcade/AstroLeague/AstroLeagueBallIndicator.cs`
  - `Assets/_Scripts/Controller/Arcade/AstroLeague/AstroLeagueBallSettingsSO.cs`
  - `Assets/_Scripts/Controller/Arcade/AstroLeague/AstroLeagueGoal.cs`
  - `Assets/_Scripts/Controller/Arcade/AstroLeague/AstroLeagueMatchController.cs`
  - `Assets/_Scripts/Controller/Arcade/AstroLeague/AstroLeagueMatchController.cs.meta`
  - `Assets/_Scripts/Controller/Arcade/AstroLeague/AstroLeagueMatchMonitor.cs`
  - `Assets/_Scripts/Controller/Arcade/AstroLeague/AstroLeagueMatchMonitor.cs.meta`
  - `Assets/_Scripts/Controller/Arcade/AstroLeague/AstroLeagueMatchUI.cs`
  - `Assets/_Scripts/Controller/Arcade/AstroLeague/AstroLeagueMatchUI.cs.meta`
  - `Assets/_Scripts/Controller/Arcade/AstroLeague/AstroLeagueScoreManager.cs`
  - `Assets/_Scripts/Controller/Arcade/AstroLeague/AstroLeagueSettingsSO.cs`
  - `Assets/_Scripts/Controller/Arcade/AstroLeague/AstroLeagueSettingsSO.cs.meta`
  - `Assets/_Scripts/Controller/Arcade/AstroLeague/GoalScoredTurnMonitor.cs`
  - `Assets/_Scripts/Controller/Arcade/AstroLeague/SinglePlayerAstroLeagueController.cs`
  - `Assets/_Scripts/Game/AI/AIPilot.cs`
  - `Assets/_Scripts/Game/Arcade/AstroLeague.meta`
  - `Assets/_Scripts/Game/Arcade/AstroLeague/AstroLeagueArena.cs`
  - `Assets/_Scripts/Game/Arcade/AstroLeague/AstroLeagueArena.cs.meta`
  - `Assets/_Scripts/Game/Arcade/AstroLeague/AstroLeagueBall.cs`
  - `Assets/_Scripts/Game/Arcade/AstroLeague/AstroLeagueBall.cs.meta`
  - `Assets/_Scripts/Game/Arcade/AstroLeague/AstroLeagueBallIndicator.cs`
  - `Assets/_Scripts/Game/Arcade/AstroLeague/AstroLeagueBallIndicator.cs.meta`
  - `Assets/_Scripts/Game/Arcade/AstroLeague/AstroLeagueBallSettingsSO.cs`
  - `Assets/_Scripts/Game/Arcade/AstroLeague/AstroLeagueGoal.cs`
  - … and 13 more

### `a911f626f` — Add Astro League mini-game - Rocket League-inspired ship soccer

_Claude, 2026-02-21 21:57:38 +0000_

```text
New game mode where ships fly in a 3D arena and hit a physics ball into
goals to score. Core components:

- AstroLeagueBall: Physics-driven ball with collision-based impulse response
- AstroLeagueGoal: Trigger zones that detect ball entry and award goals
- AstroLeagueArena: Runtime arena wall generation with bouncy physics
- AstroLeagueScoreManager: Per-team goal tracking tied to round stats
- GoalScoredTurnMonitor: Ends match when a team reaches the goal limit
- SinglePlayerAstroLeagueController: Game flow controller using the
  existing MiniGameControllerBase template method pattern
- AstroLeagueBallIndicator: Off-screen ball direction indicator for HUD
- GameModes.AstroLeague (36) and ScoringModes.GoalsScored (17) enums

Scene setup (Unity editor) still needed: create scene, wire prefabs,
configure SO_ArcadeGame asset, and add to SO_GameList.
```

```text
 Assets/_Scripts/Game/Arcade/AstroLeague/AstroLeagueArena.cs           | 119 ++++++++++++++++++++++++++++++++
 Assets/_Scripts/Game/Arcade/AstroLeague/AstroLeagueBall.cs            | 102 +++++++++++++++++++++++++++
 Assets/_Scripts/Game/Arcade/AstroLeague/AstroLeagueBallIndicator.cs   |  67 ++++++++++++++++++
 Assets/_Scripts/Game/Arcade/AstroLeague/AstroLeagueGoal.cs            |  43 ++++++++++++
 Assets/_Scripts/Game/Arcade/AstroLeague/AstroLeagueScoreManager.cs    |  65 +++++++++++++++++
 Assets/_Scripts/Game/Arcade/AstroLeague/GoalScoredTurnMonitor.cs      |  70 +++++++++++++++++++
 .../Game/Arcade/AstroLeague/SinglePlayerAstroLeagueController.cs      |  91 ++++++++++++++++++++++++
 Assets/_Scripts/Game/Arcade/ScoringModes.cs                           |   1 +
 Assets/_Scripts/Models/Enums/GameModes.cs                             |   1 +
 9 files changed, 559 insertions(+)
```

<details><summary>Patch (code/doc/text files, first 80 of 622 lines)</summary>

```diff
diff --git a/Assets/_Scripts/Game/Arcade/AstroLeague/AstroLeagueArena.cs b/Assets/_Scripts/Game/Arcade/AstroLeague/AstroLeagueArena.cs
new file mode 100644
index 000000000..87f156e4b
--- /dev/null
+++ b/Assets/_Scripts/Game/Arcade/AstroLeague/AstroLeagueArena.cs
@@ -0,0 +1,119 @@
+using UnityEngine;
+
+namespace CosmicShore.Game.Arcade.AstroLeague
+{
+    /// <summary>
+    /// Defines the Astro League arena boundaries and spawns walls at runtime.
+    /// The arena is a rectangular box in 3D space (no gravity).
+    /// Goals are placed at the +Z and -Z ends.
+    /// </summary>
+    public class AstroLeagueArena : MonoBehaviour
+    {
+        [Header("Arena Dimensions")]
+        [SerializeField] float arenaLength = 300f;
+        [SerializeField] float arenaWidth = 200f;
+        [SerializeField] float arenaHeight = 100f;
+        [SerializeField] float wallThickness = 5f;
+
+        [Header("Visuals")]
+        [SerializeField] Material wallMaterial;
+
+        public float ArenaLength => arenaLength;
+        public float ArenaWidth => arenaWidth;
+        public float ArenaHeight => arenaHeight;
+
+        public Vector3 Center => transform.position;
+
+        /// <summary>
+        /// Spawn position for the jade team (negative Z side).
+        /// </summary>
+        public Vector3 JadeSpawnPosition => Center + Vector3.back * (arenaLength * 0.3f);
+
+        /// <summary>
+        /// Spawn position for the ruby team (positive Z side).
+        /// </summary>
+        public Vector3 RubySpawnPosition => Center + Vector3.forward * (arenaLength * 0.3f);
+
+        void Awake()
+        {
+            CreateWalls();
+        }
+
+        void CreateWalls()
+        {
+            // Top wall
+            CreateWall("Wall_Top",
+                Center + Vector3.up * arenaHeight / 2f,
+                new Vector3(arenaWidth, wallThickness, arenaLength));
+
+            // Bottom wall
+            CreateWall("Wall_Bottom",
+                Center + Vector3.down * arenaHeight / 2f,
+                new Vector3(arenaWidth, wallThickness, arenaLength));
+
+            // Left wall
+            CreateWall("Wall_Left",
+                Center + Vector3.left * arenaWidth / 2f,
+                new Vector3(wallThickness, arenaHeight, arenaLength));
+
+            // Right wall
+            CreateWall("Wall_Right",
+                Center + Vector3.right * arenaWidth / 2f,
+                new Vector3(wallThickness, arenaHeight, arenaLength));
+
+            // Back wall (behind jade goal - ball passes through goal trigger first)
+            CreateWall("Wall_Back",
+                Center + Vector3.back * (arenaLength / 2f + wallThickness),
+                new Vector3(arenaWidth, arenaHeight, wallThickness));
+
+            // Front wall (behind ruby goal - ball passes through goal trigger first)
+            CreateWall("Wall_Front",
+                Center + Vector3.forward * (arenaLength / 2f + wallThickness),
+                new Vector3(arenaWidth, arenaHeight, wallThickness));
+        }
+
```

</details>

### `68efe6713` — Wire Astro League scene, SO asset, and game list integration

_Claude, 2026-02-21 23:06:57 +0000_

```text
- Create MinigameAstroLeague.unity scene with arena, ball, two goals,
  spawn points, Game controller, TurnMonitorController, TimeBasedTurnMonitor
  (5 min), GoalScoredTurnMonitor (first to 5), and ScoreManager
- Add .meta files for all AstroLeague scripts with stable GUIDs
- Create ArcadeGameAstroLeague SO asset (Mode=36, SceneName=MinigameAstroLeague)
- Add to ArcadeGames and AllGames game lists
- Add scene to EditorBuildSettings
```

```text
 Assets/_SO_Assets/Games/ArcadeGameAstroLeague.asset                   |  33 ++
 Assets/_SO_Assets/Games/ArcadeGameAstroLeague.asset.meta              |   8 +
 Assets/_SO_Assets/Games/GameLists/AllGames.asset                      |   1 +
 Assets/_SO_Assets/Games/GameLists/ArcadeGames.asset                   |   1 +
 Assets/_Scenes/Singleplayer Scenes/MinigameAstroLeague.unity          | 967 ++++++++++++++++++++++++++++++++
 Assets/_Scenes/Singleplayer Scenes/MinigameAstroLeague.unity.meta     |   7 +
 Assets/_Scripts/Game/Arcade/AstroLeague.meta                          |   8 +
 Assets/_Scripts/Game/Arcade/AstroLeague/AstroLeagueArena.cs.meta      |  11 +
 Assets/_Scripts/Game/Arcade/AstroLeague/AstroLeagueBall.cs.meta       |  11 +
 .../_Scripts/Game/Arcade/AstroLeague/AstroLeagueBallIndicator.cs.meta |  11 +
 Assets/_Scripts/Game/Arcade/AstroLeague/AstroLeagueGoal.cs.meta       |  11 +
 .../_Scripts/Game/Arcade/AstroLeague/AstroLeagueScoreManager.cs.meta  |  11 +
 Assets/_Scripts/Game/Arcade/AstroLeague/GoalScoredTurnMonitor.cs.meta |  11 +
 .../Game/Arcade/AstroLeague/SinglePlayerAstroLeagueController.cs.meta |  11 +
 ProjectSettings/EditorBuildSettings.asset                             |   3 +
 15 files changed, 1105 insertions(+)
```

### `c47a16e6b` — WIred up astroleeague to organic rematch games

_Garrett Milliron, 2026-02-23 14:55:54 -0500_

```text
 Assets/_SO_Assets/Games/ArcadeGameAstroLeague.asset         | 5 +++--
 Assets/_SO_Assets/Games/GameLists/OrganicRematchGames.asset | 9 +++++----
 2 files changed, 8 insertions(+), 6 deletions(-)
```

### `d227ee820` — Add missing camera and DependencySpawner prefabs to AstroLeague scene, fix teardown crash

_Claude, 2026-02-23 20:07:31 +0000_

```text
- Add Game Scene Main Camera prefab instance to scene (fixes no camera on load)
- Add DependencySpawner prefab instance to scene (matches other minigame scenes)
- Fix MissingReferenceException in R_VesselActionHandler.OnDisable when Player
  is destroyed before vessel during scene teardown
```

```text
 Assets/_Scenes/Singleplayer Scenes/MinigameAstroLeague.unity | 165 +++++++++++++++++++++++++++++++++++++++++
 Assets/_Scripts/Game/Ship/R_VesselActionHandler.cs           |   9 ++-
 2 files changed, 170 insertions(+), 4 deletions(-)
```

<details><summary>Patch (code/doc/text files)</summary>

```diff
diff --git a/Assets/_Scripts/Game/Ship/R_VesselActionHandler.cs b/Assets/_Scripts/Game/Ship/R_VesselActionHandler.cs
index 8684fdb03..d687a2b29 100644
--- a/Assets/_Scripts/Game/Ship/R_VesselActionHandler.cs
+++ b/Assets/_Scripts/Game/Ship/R_VesselActionHandler.cs
@@ -55,10 +55,11 @@ namespace CosmicShore.Game
         {
             if (!IsSpawned) ShipHelper.DestroyRuntimeActions(_runtimeInstances);
             UnsubscribeFromInputEvents();
-            
-            // TODO - These are not static events, so unsubscribe is not necessary,
-            // but better to do it for safety. but not on OnDisable, as few references will be missing,
-            // better to do it earlier.
+
+            // Guard: Player may already be destroyed during scene teardown
+            if (vesselStatus?.Player is not UnityEngine.Object playerObj || playerObj == null)
+                return;
+
             if (vesselStatus.IsLocalUser)
                 vesselStatus.InputStatus.OnToggleInputPaused -= OnToggleInputPaused;
         }
```

</details>

### `073c5c608` — Fix Ready button onClick wiring in AstroLeague scene

_Claude, 2026-02-24 19:38:10 +0000_

```text
The button's UnityEvent was missing critical serialized properties:
- m_CallState (must be 2 for RuntimeOnly - without it the callback is Off)
- m_Mode (1 = void method call)
- Array.size (to properly resize the calls array)
- m_Arguments.m_ObjectArgumentAssemblyTypeName
Also fixed m_Target to reference the controller component (110000004)
instead of the Game GameObject (110000001).
```

```text
 Assets/_Scenes/Singleplayer Scenes/MinigameAstroLeague.unity | 22 +++++++++++++++++++++-
 1 file changed, 21 insertions(+), 1 deletion(-)
```

### `a6fe5fcc0` — Add visible arena environment, glowing ball, and scene lighting

_Claude, 2026-02-24 22:49:47 +0000_

```text
Arena:
- Semi-transparent blue walls so players can see boundaries
- Wireframe edge lines on all 12 edges for spatial reference
- Colored goal markers (jade green / ruby red) at each end
- Center line divider
- Bouncy physics material on all walls

Ball:
- Emissive gold material with _EMISSION keyword
- Point light (range 40, intensity 2) illuminates nearby surfaces
- TrailRenderer shows ball movement path
- Trail clears on reset

Scene:
- Added directional light (soft blue-white, intensity 0.6)
- Bumped ambient lighting from pure black to subtle dark blue
- AmbientMode changed to gradient for better fill
```

```text
 Assets/_Scenes/Singleplayer Scenes/MinigameAstroLeague.unity | 102 +++++++++++++++++++-
 Assets/_Scripts/Game/Arcade/AstroLeague/AstroLeagueArena.cs  | 208 ++++++++++++++++++++++++++++++-----------
 Assets/_Scripts/Game/Arcade/AstroLeague/AstroLeagueBall.cs   |  54 ++++++++++-
 3 files changed, 302 insertions(+), 62 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 80 of 371 lines)</summary>

```diff
diff --git a/Assets/_Scripts/Game/Arcade/AstroLeague/AstroLeagueArena.cs b/Assets/_Scripts/Game/Arcade/AstroLeague/AstroLeagueArena.cs
index 87f156e4b..5c1ce7415 100644
--- a/Assets/_Scripts/Game/Arcade/AstroLeague/AstroLeagueArena.cs
+++ b/Assets/_Scripts/Game/Arcade/AstroLeague/AstroLeagueArena.cs
@@ -4,8 +4,8 @@ namespace CosmicShore.Game.Arcade.AstroLeague
 {
     /// <summary>
     /// Defines the Astro League arena boundaries and spawns walls at runtime.
-    /// The arena is a rectangular box in 3D space (no gravity).
-    /// Goals are placed at the +Z and -Z ends.
+    /// Creates visible, semi-transparent boundary walls with edge wireframes
+    /// so players can see the arena extents in space.
     /// </summary>
     public class AstroLeagueArena : MonoBehaviour
     {
@@ -13,92 +13,193 @@ namespace CosmicShore.Game.Arcade.AstroLeague
         [SerializeField] float arenaLength = 300f;
         [SerializeField] float arenaWidth = 200f;
         [SerializeField] float arenaHeight = 100f;
-        [SerializeField] float wallThickness = 5f;
+        [SerializeField] float wallThickness = 2f;
 
         [Header("Visuals")]
         [SerializeField] Material wallMaterial;
+        [SerializeField] Color wallColor = new(0.15f, 0.4f, 0.8f, 0.08f);
+        [SerializeField] Color edgeColor = new(0.3f, 0.6f, 1f, 0.5f);
+        [SerializeField] Color jadeGoalColor = new(0.1f, 1f, 0.5f, 0.25f);
+        [SerializeField] Color rubyGoalColor = new(1f, 0.2f, 0.3f, 0.25f);
+
+        [Header("Center Line")]
+        [SerializeField] Color centerLineColor = new(1f, 1f, 1f, 0.15f);
 
         public float ArenaLength => arenaLength;
         public float ArenaWidth => arenaWidth;
         public float ArenaHeight => arenaHeight;
-
         public Vector3 Center => transform.position;
-
-        /// <summary>
-        /// Spawn position for the jade team (negative Z side).
-        /// </summary>
         public Vector3 JadeSpawnPosition => Center + Vector3.back * (arenaLength * 0.3f);
-
-        /// <summary>
-        /// Spawn position for the ruby team (positive Z side).
-        /// </summary>
         public Vector3 RubySpawnPosition => Center + Vector3.forward * (arenaLength * 0.3f);
 
+        Material _generatedWallMat;
+        Material _jadeGoalMat;
+        Material _rubyGoalMat;
+        Material _centerLineMat;
+
         void Awake()
         {
+            CreateMaterials();
             CreateWalls();
+            CreateGoalMarkers();
+            CreateCenterLine();
+            CreateEdgeFrame();
+        }
+
+        void CreateMaterials()
+        {
+            var shader = Shader.Find("Universal Render Pipeline/Unlit");
+            if (shader == null) shader = Shader.Find("Unlit/Color");
+
+            _generatedWallMat = CreateTransparentMat(shader, wallColor);
+            _jadeGoalMat = CreateTransparentMat(shader, jadeGoalColor);
+            _rubyGoalMat = CreateTransparentMat(shader, rubyGoalColor);
+            _centerLineMat = CreateTransparentMat(shader, centerLineColor);
+        }
+
+        static Material CreateTransparentMat(Shader shader, Color color)
+        {
+            var mat = new Material(shader) { color = color };
+            mat.SetFloat("_Surface", 1); // Transparent
+            mat.SetFloat("_Blend", 0);   // Alpha
+            mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
+            mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
```

</details>

### `34cd16a64` — Add AI opponent, score event, and tune match settings for testing

_Claude, 2026-02-24 23:02:09 +0000_

```text
- Add Ruby AI opponent to arena via _initializeDatas
- Create EventOnAstroLeagueScoreUpdated ScriptableEventString asset
- Wire onScoreUpdated on AstroLeagueScoreManager to new event
- Reduce timer from 300s to 120s for faster test cycles
- Reduce goal limit from 5 to 3 for quicker matches
```

```text
 Assets/_SO_Assets/SOAP/Event Channels/EventOnAstroLeagueScoreUpdated.asset      | 18 ++++++++++++++++++
 Assets/_SO_Assets/SOAP/Event Channels/EventOnAstroLeagueScoreUpdated.asset.meta |  8 ++++++++
 Assets/_Scenes/Singleplayer Scenes/MinigameAstroLeague.unity                    | 14 ++++++++++----
 3 files changed, 36 insertions(+), 4 deletions(-)
```

### `44207c939` — Remove AI opponent to fix loading screen hang

_Claude, 2026-02-25 00:41:49 +0000_

```text
The AI spawn in _initializeDatas likely caused an exception during
InitializeGame(), preventing InvokeClientReady() from firing, which
left the connecting panel stuck visible indefinitely.

Reverted _initializeDatas to empty. Timer (120s) and goal limit (3)
changes are kept.
```

```text
 Assets/_Scenes/Singleplayer Scenes/MinigameAstroLeague.unity | 8 +-------
 1 file changed, 1 insertion(+), 7 deletions(-)
```

### `51c35b10f` — Fix AstroLeague loading hang caused by null cellData.Cell in AIPilot

_Claude, 2026-02-25 02:25:02 +0000_

```text
AIPilot.UpdateCellContent() crashed when cellData.Cell was null during
Initialize(), which killed Start() before InvokeClientReady() could run.
The MiniGameHUD connecting panel was never dismissed, causing the hang.

Also guard playerScoreContainer in MiniGameHUDView.ClearPlayerList()
to prevent the secondary UnassignedReferenceException.
```

```text
 Assets/_Scripts/Game/AI/AIPilot.cs                  | 5 ++++-
 Assets/_Scripts/MinigameHUD/View/MinigameHUDView.cs | 1 +
 2 files changed, 5 insertions(+), 1 deletion(-)
```

<details><summary>Patch (code/doc/text files)</summary>

```diff
diff --git a/Assets/_Scripts/Game/AI/AIPilot.cs b/Assets/_Scripts/Game/AI/AIPilot.cs
index 098c63fdd..7f83c7830 100644
--- a/Assets/_Scripts/Game/AI/AIPilot.cs
+++ b/Assets/_Scripts/Game/AI/AIPilot.cs
@@ -169,7 +169,10 @@ namespace CosmicShore.Game.AI
                 }
             }
 
-            _targetPosition = !closestItem ? activeCell.transform.position : closestItem.transform.position;
+            if (closestItem)
+                _targetPosition = closestItem.transform.position;
+            else if (activeCell != null)
+                _targetPosition = activeCell.transform.position;
         }
 
         IEnumerator UpdatePlayerTarget()
diff --git a/Assets/_Scripts/MinigameHUD/View/MinigameHUDView.cs b/Assets/_Scripts/MinigameHUD/View/MinigameHUDView.cs
index 94cdd45b6..a9e94dc3a 100644
--- a/Assets/_Scripts/MinigameHUD/View/MinigameHUDView.cs
+++ b/Assets/_Scripts/MinigameHUD/View/MinigameHUDView.cs
@@ -125,6 +125,7 @@ namespace CosmicShore.Game.UI
 
         public void ClearPlayerList()
         {
+            if (playerScoreContainer == null) return;
             foreach (Transform child in playerScoreContainer)
             {
                 Destroy(child.gameObject);
```

</details>

### `7208eb148` — Fix AstroLeague goal visibility, ball physics, and ball material

_Claude, 2026-02-25 08:44:44 +0000_

```text
Goals: Disable backface culling (_Cull 0) on goal marker materials so
they render from inside the arena, not just outside.

Ball physics: Drop linearDamping from 0.3 to 0.01 (was causing the
"massive friction" feel), add a zero-friction high-bounce PhysicsMaterial
to the SphereCollider, increase hitForceMultiplier from 2 to 8, and
set mass to 2 for satisfying momentum transfer.

Ball visuals: Metallic chrome sphere with three-way color-cycling
emission (gold → blue → pink), breathing pulse, orbiting particle
aura, and a wider vivid trail. Designed as a "special payload" object.
```

```text
 Assets/_Scripts/Game/Arcade/AstroLeague/AstroLeagueArena.cs |   4 +
 Assets/_Scripts/Game/Arcade/AstroLeague/AstroLeagueBall.cs  | 175 ++++++++++++++++++++++++++++++++++++------
 2 files changed, 154 insertions(+), 25 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 80 of 252 lines)</summary>

```diff
diff --git a/Assets/_Scripts/Game/Arcade/AstroLeague/AstroLeagueArena.cs b/Assets/_Scripts/Game/Arcade/AstroLeague/AstroLeagueArena.cs
index 5c1ce7415..8d92688cf 100644
--- a/Assets/_Scripts/Game/Arcade/AstroLeague/AstroLeagueArena.cs
+++ b/Assets/_Scripts/Game/Arcade/AstroLeague/AstroLeagueArena.cs
@@ -55,6 +55,10 @@ namespace CosmicShore.Game.Arcade.AstroLeague
             _jadeGoalMat = CreateTransparentMat(shader, jadeGoalColor);
             _rubyGoalMat = CreateTransparentMat(shader, rubyGoalColor);
             _centerLineMat = CreateTransparentMat(shader, centerLineColor);
+
+            // Goal markers must be visible from both sides of the quad
+            _jadeGoalMat.SetFloat("_Cull", 0);
+            _rubyGoalMat.SetFloat("_Cull", 0);
         }
 
         static Material CreateTransparentMat(Shader shader, Color color)
diff --git a/Assets/_Scripts/Game/Arcade/AstroLeague/AstroLeagueBall.cs b/Assets/_Scripts/Game/Arcade/AstroLeague/AstroLeagueBall.cs
index 711b38434..b46ca2bba 100644
--- a/Assets/_Scripts/Game/Arcade/AstroLeague/AstroLeagueBall.cs
+++ b/Assets/_Scripts/Game/Arcade/AstroLeague/AstroLeagueBall.cs
@@ -6,7 +6,8 @@ namespace CosmicShore.Game.Arcade.AstroLeague
     /// <summary>
     /// Physics-driven ball for Astro League.
     /// Ships collide with it to push it toward goals.
-    /// Self-illuminates with a point light and leaves a speed trail.
+    /// Self-illuminates with a point light, animated emission, and a speed trail.
+    /// Designed as a "special payload" — visually distinctive and satisfying to hit.
     /// </summary>
     [RequireComponent(typeof(Rigidbody))]
     [RequireComponent(typeof(SphereCollider))]
@@ -14,25 +15,37 @@ namespace CosmicShore.Game.Arcade.AstroLeague
     {
         [Header("Physics")]
         [SerializeField] float maxSpeed = 120f;
-        [SerializeField] float hitForceMultiplier = 2f;
-        [SerializeField] float drag = 0.3f;
-        [SerializeField] float bounciness = 0.8f;
+        [SerializeField] float hitForceMultiplier = 8f;
+        [SerializeField] float drag = 0.01f;
+        [SerializeField] float angularDrag = 0.02f;
+        [SerializeField] float ballBounciness = 0.95f;
+        [SerializeField] float mass = 2f;
 
         [Header("Reset")]
         [SerializeField] float resetDelay = 1.5f;
 
         [Header("Visuals")]
-        [SerializeField] Color ballColor = new(1f, 0.85f, 0.3f, 1f);
-        [SerializeField] float lightRange = 40f;
-        [SerializeField] float lightIntensity = 2f;
-        [SerializeField] float trailTime = 0.4f;
-        [SerializeField] float trailWidth = 2f;
+        [SerializeField] Color primaryColor = new(1f, 0.6f, 0.1f, 1f);
+        [SerializeField] Color secondaryColor = new(0.2f, 0.5f, 1f, 1f);
+        [SerializeField] Color tertiaryColor = new(1f, 0.15f, 0.6f, 1f);
+        [SerializeField] float emissionIntensity = 4f;
+        [SerializeField] float pulseSpeed = 1.2f;
+        [SerializeField] float lightRange = 50f;
+        [SerializeField] float lightIntensity = 3f;
+        [SerializeField] float trailTime = 0.6f;
+        [SerializeField] float trailWidth = 3f;
+
+        static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");
+        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
 
         Rigidbody rb;
         Vector3 spawnPosition;
         bool isResetting;
         Light ballLight;
         TrailRenderer trail;
+        Material ballMat;
+        Renderer ballRenderer;
+        ParticleSystem auraParticles;
 
         public event Action<Domains> OnGoalScored;
 
@@ -43,10 +56,22 @@ namespace CosmicShore.Game.Arcade.AstroLeague
             rb = GetComponent<Rigidbody>();
             rb.useGravity = false;
             rb.linearDamping = drag;
-            rb.angularDamping = 0.5f;
```

</details>

### `322b4d70f` — Billiard-physics ball with impact juice for Rocket League game feel

_Claude, 2026-02-25 09:24:14 +0000_

```text
Root cause: ships move via transform.position (not rigidbody), so the
ball's OnCollisionEnter read near-zero velocity and applied near-zero
force. Rewrote collision response to read VesselStatus.Speed/Course
directly for accurate momentum transfer.

Physics: billiard-style deflection with configurable directional bias,
speed-dependent drag curve (fast coast / slow settle / stop threshold),
0.98 ball restitution, zero-friction arena walls.

Impact juice: camera shake (Perlin-based decay), emission flash on hit,
burst particles at contact point, hitstop (brief timescale dip on hard
hits), haptic feedback, speed-reactive trail/emission/light/particles.

All tuning extracted to AstroLeagueBallSettingsSO for inspector tweaking.
```

```text
 Assets/_Scripts/Game/Arcade/AstroLeague/AstroLeagueArena.cs          |   6 +-
 Assets/_Scripts/Game/Arcade/AstroLeague/AstroLeagueBall.cs           | 415 +++++++++++++++++++++++++++------
 Assets/_Scripts/Game/Arcade/AstroLeague/AstroLeagueBallSettingsSO.cs | 106 +++++++++
 Assets/_Scripts/Game/Camera/CustomCameraController.cs                |  34 +++
 4 files changed, 483 insertions(+), 78 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 80 of 745 lines)</summary>

```diff
diff --git a/Assets/_Scripts/Game/Arcade/AstroLeague/AstroLeagueArena.cs b/Assets/_Scripts/Game/Arcade/AstroLeague/AstroLeagueArena.cs
index 8d92688cf..9cf747c87 100644
--- a/Assets/_Scripts/Game/Arcade/AstroLeague/AstroLeagueArena.cs
+++ b/Assets/_Scripts/Game/Arcade/AstroLeague/AstroLeagueArena.cs
@@ -109,11 +109,11 @@ namespace CosmicShore.Game.Arcade.AstroLeague
             col.isTrigger = false;
             col.material = new PhysicsMaterial("ArenaBounce")
             {
-                bounciness = 0.9f,
+                bounciness = 1f,
                 bounceCombine = PhysicsMaterialCombine.Maximum,
                 frictionCombine = PhysicsMaterialCombine.Minimum,
-                dynamicFriction = 0.05f,
-                staticFriction = 0.05f
+                dynamicFriction = 0f,
+                staticFriction = 0f
             };
         }
 
diff --git a/Assets/_Scripts/Game/Arcade/AstroLeague/AstroLeagueBall.cs b/Assets/_Scripts/Game/Arcade/AstroLeague/AstroLeagueBall.cs
index b46ca2bba..d13c61355 100644
--- a/Assets/_Scripts/Game/Arcade/AstroLeague/AstroLeagueBall.cs
+++ b/Assets/_Scripts/Game/Arcade/AstroLeague/AstroLeagueBall.cs
@@ -1,39 +1,34 @@
 using System;
+using Cysharp.Threading.Tasks;
+using CosmicShore.Game.CameraSystem;
+using CosmicShore.Game.IO;
 using UnityEngine;
 
 namespace CosmicShore.Game.Arcade.AstroLeague
 {
     /// <summary>
-    /// Physics-driven ball for Astro League.
-    /// Ships collide with it to push it toward goals.
-    /// Self-illuminates with a point light, animated emission, and a speed trail.
-    /// Designed as a "special payload" — visually distinctive and satisfying to hit.
+    /// Billiard-physics ball for Astro League.
+    /// Ships transfer momentum on contact using VesselStatus velocity (not rigidbody velocity,
+    /// because ships move via transform.position). Wall bounces are handled by Unity physics
+    /// with a high-restitution material. Impact juice (hitstop, camera shake, emission flash,
+    /// burst particles) scales with hit intensity for Rocket-League-grade game feel.
     /// </summary>
     [RequireComponent(typeof(Rigidbody))]
     [RequireComponent(typeof(SphereCollider))]
     public class AstroLeagueBall : MonoBehaviour
     {
-        [Header("Physics")]
-        [SerializeField] float maxSpeed = 120f;
-        [SerializeField] float hitForceMultiplier = 8f;
-        [SerializeField] float drag = 0.01f;
-        [SerializeField] float angularDrag = 0.02f;
-        [SerializeField] float ballBounciness = 0.95f;
-        [SerializeField] float mass = 2f;
-
-        [Header("Reset")]
-        [SerializeField] float resetDelay = 1.5f;
+        [Header("Settings")]
+        [SerializeField] AstroLeagueBallSettingsSO settings;
 
         [Header("Visuals")]
         [SerializeField] Color primaryColor = new(1f, 0.6f, 0.1f, 1f);
         [SerializeField] Color secondaryColor = new(0.2f, 0.5f, 1f, 1f);
         [SerializeField] Color tertiaryColor = new(1f, 0.15f, 0.6f, 1f);
-        [SerializeField] float emissionIntensity = 4f;
         [SerializeField] float pulseSpeed = 1.2f;
-        [SerializeField] float lightRange = 50f;
-        [SerializeField] float lightIntensity = 3f;
-        [SerializeField] float trailTime = 0.6f;
-        [SerializeField] float trailWidth = 3f;
+        [SerializeField] float baseLightIntensity = 3f;
+
+        [Header("Reset")]
+        [SerializeField] float resetDelay = 1.5f;
 
         static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");
         static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
@@ -41,31 +36,43 @@ namespace CosmicShore.Game.Arcade.AstroLeague
         Rigidbody rb;
         Vector3 spawnPosition;
```

</details>

### `4c0f2817e` — feat(arcade): rebuild Astro League as AAA hypersea soccer with billiard physics

_Claude, 2026-06-12 01:35:40 +0000_

```text
Redesign AstroLeague from a rough prototype into a polished Rocket-League-
inspired 3D soccer game played by Squirrel drift vessels inside a wireframe
HyperSea arena.

Ball physics: custom billiard collision response reading VesselStatus.Speed
and Course (vessels use transform-based movement, not rigidbody velocity),
Slerp-blended deflection with directional bias, speed-dependent drag curve,
MaterialPropertyBlock emission animation, impact particles, hitstop, and
camera shake.

Match flow: kickoff freeze → countdown → live play → goal celebration with
slow-mo → re-kickoff → full time → golden-goal overtime if tied → winner
banner → end-game scoreboard. All timescale restoration uses constants
(not captured values) to prevent race conditions between concurrent hitstop
and celebration coroutines.

AI striker: AIPilot gains SetExternalTargetProvider hook; AstroLeague uses
it for billiard-thinking AI that approaches the ball from its own-goal side
so contact drives goalward, swinging wide to recover position.

Arena: invisible physics walls, pulsing wireframe edge cage (12
LineRenderers with breathing alpha), portal-style goal rings with
anticipation flare, center ring, drifting plankton particles, HyperSea
skybox.

UI: runtime-built canvas overlay with score display, announcer banners
(punch-in scale + fade on unscaled time), and edge-clamped ball arrow
indicator.

Renames: SinglePlayerAstroLeagueController → AstroLeagueMatchController,
GoalScoredTurnMonitor → AstroLeagueMatchMonitor, AstroLeagueBallIndicator
→ AstroLeagueMatchUI, AstroLeagueBallSettingsSO → AstroLeagueSettingsSO.

New SO asset: AstroLeagueSettings with all tuning parameters extracted per
the SO config separation pattern.
```

```text
 Assets/_SO_Assets/Games/ArcadeGameAstroLeague.asset                   |  14 +-
 Assets/_SO_Assets/Games/AstroLeagueSettings.asset                     |  56 ++++
 Assets/_SO_Assets/Games/AstroLeagueSettings.asset.meta                |   8 +
 Assets/_Scenes/Singleplayer Scenes/MinigameAstroLeague.unity          | 402 ++++++-----------------
 Assets/_Scripts/Controller/AI/AIPilot.cs                              |  17 +
 Assets/_Scripts/Controller/Arcade/ASTROLEAGUE.md                      |  84 +++++
 Assets/_Scripts/Controller/Arcade/ASTROLEAGUE.md.meta                 |   7 +
 Assets/_Scripts/Controller/Arcade/AstroLeague/AstroLeagueArena.cs     | 362 ++++++++++++---------
 Assets/_Scripts/Controller/Arcade/AstroLeague/AstroLeagueBall.cs      | 557 +++++++++++++++-----------------
 .../Controller/Arcade/AstroLeague/AstroLeagueBallIndicator.cs         |  67 ----
 .../Controller/Arcade/AstroLeague/AstroLeagueBallSettingsSO.cs        | 106 ------
 Assets/_Scripts/Controller/Arcade/AstroLeague/AstroLeagueGoal.cs      |  36 +--
 .../Controller/Arcade/AstroLeague/AstroLeagueMatchController.cs       | 375 +++++++++++++++++++++
 ...troLeagueController.cs.meta => AstroLeagueMatchController.cs.meta} |   0
 .../_Scripts/Controller/Arcade/AstroLeague/AstroLeagueMatchMonitor.cs |  87 +++++
 ...{GoalScoredTurnMonitor.cs.meta => AstroLeagueMatchMonitor.cs.meta} |   0
 Assets/_Scripts/Controller/Arcade/AstroLeague/AstroLeagueMatchUI.cs   | 232 +++++++++++++
 .../{AstroLeagueBallIndicator.cs.meta => AstroLeagueMatchUI.cs.meta}  |   0
 .../_Scripts/Controller/Arcade/AstroLeague/AstroLeagueScoreManager.cs |  53 +--
 .../_Scripts/Controller/Arcade/AstroLeague/AstroLeagueSettingsSO.cs   | 110 +++++++
 .../Controller/Arcade/AstroLeague/AstroLeagueSettingsSO.cs.meta       |  11 +
 .../_Scripts/Controller/Arcade/AstroLeague/GoalScoredTurnMonitor.cs   |  70 ----
 .../Arcade/AstroLeague/SinglePlayerAstroLeagueController.cs           |  91 ------
 23 files changed, 1606 insertions(+), 1139 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 80 of 2698 lines)</summary>

```diff
diff --git a/Assets/_Scripts/Controller/AI/AIPilot.cs b/Assets/_Scripts/Controller/AI/AIPilot.cs
index efbd9fad1..3ebc2dcc1 100644
--- a/Assets/_Scripts/Controller/AI/AIPilot.cs
+++ b/Assets/_Scripts/Controller/AI/AIPilot.cs
@@ -104,6 +104,20 @@ namespace CosmicShore.Gameplay
         Vector3 _distance;
         bool LookingAtCrystal;
 
+        // Optional external steering hook. When set, the provider is sampled every
+        // frame and overrides crystal/player seeking entirely. Used by game modes
+        // that need bespoke AI objectives (e.g. Astro League ball striking).
+        Func<Vector3> _externalTargetProvider;
+
+        /// <summary>
+        /// Routes all steering toward positions supplied by <paramref name="provider"/>.
+        /// Pass the freshest position each call — it is sampled once per frame.
+        /// </summary>
+        public void SetExternalTargetProvider(Func<Vector3> provider) => _externalTargetProvider = provider;
+
+        /// <summary>Restores default crystal/player target seeking.</summary>
+        public void ClearExternalTargetProvider() => _externalTargetProvider = null;
+
         Dictionary<Corner, AvoidanceBehavior> CornerBehaviors;
 
         #region Avoidance Stuff
@@ -279,6 +293,9 @@ namespace CosmicShore.Gameplay
             if (VesselStatus.IsStationary)
                 return;
 
+            if (_externalTargetProvider != null)
+                _targetPosition = _externalTargetProvider();
+
             _distance = _targetPosition - transform.position;
             Vector3 desiredDirection = _distance.normalized;
 
diff --git a/Assets/_Scripts/Controller/Arcade/ASTROLEAGUE.md b/Assets/_Scripts/Controller/Arcade/ASTROLEAGUE.md
new file mode 100644
index 000000000..1df981711
--- /dev/null
+++ b/Assets/_Scripts/Controller/Arcade/ASTROLEAGUE.md
@@ -0,0 +1,84 @@
+# Astro League Game Mode — Technical Documentation
+
+## Overview
+
+Astro League is hypersea soccer — the spirit of Rocket League translated to Cosmic
+Shore. Two domains (Jade vs Ruby) fight to slam a glowing payload through the
+opposing goal portal inside a wireframe arena suspended in the HyperSea. Solo play
+pits the player (Jade, Squirrel-first vessel select) against an AI striker (Ruby).
+
+**Key architectural facts:**
+
+- **Scene**: `Assets/_Scenes/Singleplayer Scenes/MinigameAstroLeague.unity`
+- **GameMode enum**: `GameModes.AstroLeague = 36`
+- **Controller**: `AstroLeagueMatchController : SinglePlayerMiniGameControllerBase`
+- **Config**: every gameplay number lives in `AstroLeagueSettingsSO`
+  (`Assets/_SO_Assets/Games/AstroLeagueSettings.asset`) — match rules, billiard
+  physics, juice, AI tuning, arena palette
+- **Featured vessel**: Squirrel (drift class). `SO_ArcadeGame.Vessels` = Squirrel only
+
+## Class Inventory (`_Scripts/Controller/Arcade/AstroLeague/`)
+
+| Class | Role |
+|---|---|
+| `AstroLeagueMatchController` | Match director: kickoffs, goal celebrations (slow-mo), golden-goal overtime, winner banner, team/domain assignment, AI striker arming |
+| `AstroLeagueBall` | Billiard-physics payload. Reads `VesselStatus.Speed/Course` from the striking vessel (vessels move via transform, so rigidbody velocity is useless). Hitstop, camera shake, emission flash (MaterialPropertyBlock), burst particles, haptics |
+| `AstroLeagueMatchMonitor` | `TurnMonitor` match clock ("M:SS" on the shared turn-monitor display channel). Pauses during celebrations; the controller decides full-time vs overtime; turn ends only on `ForceEnd()` |
+| `AstroLeagueScoreManager` | Goals per domain → mirrors into `RoundStats.Score` (keeps the shared scoreboard flow intact) → raises `EventOnAstroLeagueScoreUpdated` ("J - R") |
+| `AstroLeagueGoal` | Goal-mouth trigger; awards `ScoringDomain` via `ball.NotifyGoalScored` |
+| `AstroLeagueArena` | Runtime HyperSea stadium: invisible 1.0-restitution walls, pulsing edge cage, portal goal rings with ball-proximity anticipation flare, center ring, drifting plankton motes. Scene skybox is `HyperSeaSkybox.mat` |
+| `AstroLeagueMatchUI` | Runtime overlay canvas: score, announcer banners (GOAL! / count-in / OVERTIME / winner), off-screen ball arrow |
+| `AstroLeagueSettingsSO` | All tunables |
+
+## Match Flow
+
+```
+SetupNewTurn            ball frozen at center, score reset, clock configured, Ready shown
+Ready → 3-2-1 canvas    the shared CountdownTimer doubles as the first kickoff count-in
+OnCountdownTimerEnded   players parked at team spawns → SetPlayersActive + StartTurn
+                        → clock runs, ball unfrozen, GO! banner
```

</details>

_Also contains 2 unmerged merge commit(s) (branch-sync merges; no authored changes of their own)._
