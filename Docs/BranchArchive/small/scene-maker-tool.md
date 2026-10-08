# Branch archive: `scene-maker-tool`

_Snapshot 2026-10-08. Index: [README](../README.md)_

- **Last commit:** 2025-10-17 by Shombith03
- **Unmerged commits:** 2
- **Forked from:** `ef9958fd3` (2025-10-16, Merge branch 'Multiplayer-Duel-Cell' into ship-action-refactored)
- **Tip:** `b84c8e46e`
- **Files touched (58):**
  - `Assets/Resources/CosmicShore.meta`
  - `Assets/Resources/CosmicShore/MiniGameMaker.meta`
  - `Assets/Resources/CosmicShore/MiniGameMaker/ColorTheme.asset`
  - `Assets/Resources/CosmicShore/MiniGameMaker/ColorTheme.asset.meta`
  - `Assets/Resources/CosmicShore/MiniGameMaker/MiniGamePrefabLibrary.asset`
  - `Assets/Resources/CosmicShore/MiniGameMaker/MiniGamePrefabLibrary.asset.meta`
  - `Assets/Resources/CosmicShore/MiniGameMaker/MiniGameProfile.asset`
  - `Assets/Resources/CosmicShore/MiniGameMaker/MiniGameProfile.asset.meta`
  - `Assets/_Game.meta`
  - `Assets/_Game/MiniGames.meta`
  - `Assets/_Prefabs/CORE/Environment.prefab`
  - `Assets/_Prefabs/CORE/Environment.prefab.meta`
  - `Assets/_Prefabs/CORE/PlayerAndShipSpawner.prefab`
  - `Assets/_Prefabs/CORE/PlayerAndShipSpawner.prefab.meta`
  - `Assets/_Scenes/MinigameFreestyle.unity`
  - `Assets/_Scripts/Game/Player/MiniGamePlayerSpawnerAdapter.cs`
  - `Assets/_Scripts/MIniGameMaker.meta`
  - `Assets/_Scripts/MIniGameMaker/Editor.meta`
  - `Assets/_Scripts/MIniGameMaker/Editor/Authoring.meta`
  - `Assets/_Scripts/MIniGameMaker/Editor/ColorThemeSO.cs`
  - `Assets/_Scripts/MIniGameMaker/Editor/ColorThemeSO.cs.meta`
  - `Assets/_Scripts/MIniGameMaker/Editor/CosmicShoreMiniGameMakerWindow.cs`
  - `Assets/_Scripts/MIniGameMaker/Editor/CosmicShoreMiniGameMakerWindow.cs.meta`
  - `Assets/_Scripts/MIniGameMaker/Editor/Data.meta`
  - `Assets/_Scripts/MIniGameMaker/Editor/DependencySpawnerValidator.cs`
  - `Assets/_Scripts/MIniGameMaker/Editor/DependencySpawnerValidator.cs.meta`
  - `Assets/_Scripts/MIniGameMaker/Editor/GameRootValidator.cs`
  - `Assets/_Scripts/MIniGameMaker/Editor/GameRootValidator.cs.meta`
  - `Assets/_Scripts/MIniGameMaker/Editor/IToolView.cs`
  - `Assets/_Scripts/MIniGameMaker/Editor/IToolView.cs.meta`
  - `Assets/_Scripts/MIniGameMaker/Editor/IValidator.cs`
  - `Assets/_Scripts/MIniGameMaker/Editor/IValidator.cs.meta`
  - `Assets/_Scripts/MIniGameMaker/Editor/Markers.meta`
  - `Assets/_Scripts/MIniGameMaker/Editor/MiniGameControllerCodegen.cs`
  - `Assets/_Scripts/MIniGameMaker/Editor/MiniGameControllerCodegen.cs.meta`
  - `Assets/_Scripts/MIniGameMaker/Editor/MiniGameMakerView.cs`
  - `Assets/_Scripts/MIniGameMaker/Editor/MiniGameMakerView.cs.meta`
  - `Assets/_Scripts/MIniGameMaker/Editor/MiniGamePrefabLibrarySO.cs`
  - `Assets/_Scripts/MIniGameMaker/Editor/MiniGamePrefabLibrarySO.cs.meta`
  - `Assets/_Scripts/MIniGameMaker/Editor/MiniGameProfileSO.cs`
  - … and 18 more

### `2c7a1cd6c` — Game Tool Maker Part 1

_Shombith03, 2025-10-17 07:33:52 +0530_

```text
 Assets/_Scripts/MIniGameMaker/Editor/IToolView.cs.meta                |   3 +
 Assets/_Scripts/MIniGameMaker/Editor/IValidator.cs                    |  11 +
 Assets/_Scripts/MIniGameMaker/Editor/IValidator.cs.meta               |   3 +
 Assets/_Scripts/MIniGameMaker/Editor/Markers.meta                     |   8 +
 Assets/_Scripts/MIniGameMaker/Editor/MiniGameControllerCodegen.cs     |  42 +++
 .../_Scripts/MIniGameMaker/Editor/MiniGameControllerCodegen.cs.meta   |   3 +
 Assets/_Scripts/MIniGameMaker/Editor/MiniGameMakerView.cs             | 441 +++++++++++++++++++++++++++
 Assets/_Scripts/MIniGameMaker/Editor/MiniGameMakerView.cs.meta        |   3 +
 Assets/_Scripts/MIniGameMaker/Editor/MiniGamePrefabLibrarySO.cs       |  23 ++
 Assets/_Scripts/MIniGameMaker/Editor/MiniGamePrefabLibrarySO.cs.meta  |   3 +
 Assets/_Scripts/MIniGameMaker/Editor/MiniGameProfileSO.cs             |  21 ++
 Assets/_Scripts/MIniGameMaker/Editor/MiniGameProfileSO.cs.meta        |   3 +
 Assets/_Scripts/MIniGameMaker/Editor/MiniGameSceneAssembler.cs        | 361 ++++++++++++++++++++++
 Assets/_Scripts/MIniGameMaker/Editor/MiniGameSceneAssembler.cs.meta   |   3 +
 Assets/_Scripts/MIniGameMaker/Editor/MiniGameValidatorRunner.cs       |  17 ++
 Assets/_Scripts/MIniGameMaker/Editor/MiniGameValidatorRunner.cs.meta  |   3 +
 Assets/_Scripts/MIniGameMaker/Editor/PostCompileAttach.cs             |  66 +++++
 Assets/_Scripts/MIniGameMaker/Editor/PostCompileAttach.cs.meta        |   3 +
 Assets/_Scripts/MIniGameMaker/Editor/SceneUtil.cs                     |  12 +
 Assets/_Scripts/MIniGameMaker/Editor/SceneUtil.cs.meta                |   3 +
 Assets/_Scripts/MIniGameMaker/Editor/SpawnPointsValidator.cs          |  37 +++
 Assets/_Scripts/MIniGameMaker/Editor/SpawnPointsValidator.cs.meta     |   3 +
 Assets/_Scripts/MIniGameMaker/Editor/TypeResolver.cs                  |  14 +
 Assets/_Scripts/MIniGameMaker/Editor/TypeResolver.cs.meta             |   3 +
 Assets/_Scripts/MIniGameMaker/Editor/Validate.meta                    |   8 +
 Assets/_Scripts/MIniGameMaker/Editor/VesselMakerView.cs               |  37 +++
 Assets/_Scripts/MIniGameMaker/Editor/VesselMakerView.cs.meta          |   3 +
 Assets/_Scripts/MIniGameMaker/Editor/Views.meta                       |   8 +
 Assets/_Scripts/MIniGameMaker/Runtime.meta                            |   8 +
 55 files changed, 2381 insertions(+), 3 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 150 of 1739 lines)</summary>

```diff
diff --git a/Assets/_Scripts/MIniGameMaker/Editor/ColorThemeSO.cs b/Assets/_Scripts/MIniGameMaker/Editor/ColorThemeSO.cs
new file mode 100644
index 000000000..3b61825f9
--- /dev/null
+++ b/Assets/_Scripts/MIniGameMaker/Editor/ColorThemeSO.cs
@@ -0,0 +1,26 @@
+﻿using UnityEngine;
+
+namespace CosmicShore.Tools.MiniGameMaker
+{
+    [CreateAssetMenu(fileName = "ColorTheme", menuName = "CosmicShore/Editor/Color Theme")]
+    public sealed class ColorThemeSO : ScriptableObject
+    {
+        [Header("Text")]
+        [SerializeField] private Color titleText     = new(0.85f, 0.90f, 1f, 1f);
+        [SerializeField] private Color subtitleText  = new(0.70f, 0.78f, 0.95f, 1f);
+        [SerializeField] private Color bodyText      = Color.white;
+
+        [Header("Accents")]
+        [SerializeField] private Color accent        = new(0.25f, 0.65f, 1f, 1f);
+        [SerializeField] private Color warning       = new(1f, 0.6f, 0.2f, 1f);
+        [SerializeField] private Color success       = new(0.3f, 0.85f, 0.4f, 1f);
+
+        public Color TitleText    => titleText;
+        public Color SubtitleText => subtitleText;
+        public Color BodyText     => bodyText;
+
+        public Color Accent       => accent;
+        public Color Warning      => warning;
+        public Color Success      => success;
+    }
+}
\ No newline at end of file
diff --git a/Assets/_Scripts/MIniGameMaker/Editor/CosmicShoreMiniGameMakerWindow.cs b/Assets/_Scripts/MIniGameMaker/Editor/CosmicShoreMiniGameMakerWindow.cs
new file mode 100644
index 000000000..5283ac64a
--- /dev/null
+++ b/Assets/_Scripts/MIniGameMaker/Editor/CosmicShoreMiniGameMakerWindow.cs
@@ -0,0 +1,469 @@
+// Assets/_Tools/CosmicShore/MiniGameMaker/Editor/CosmicShoreMiniGameMakerWindow.cs
+
+using System;
+using System.IO;
+using System.Linq;
+using UnityEditor;
+using UnityEditor.SceneManagement;
+using UnityEngine;
+using UnityEngine.SceneManagement;
+
+namespace CosmicShore.Tools.MiniGameMaker
+{
+    public sealed class CosmicShoreMiniGameMakerWindow : EditorWindow
+    {
+        private enum MakerMode
+        {
+            Vessel,
+            MiniGame
+        }
+
+        private enum SubTab
+        {
+            Overview,
+            Config,
+            Validate,
+            Utilities
+        }
+
+        private enum Density
+        {
+            Compact,
+            Normal,
+            Relaxed
+        }
+
+        private enum ThemeMode
+        {
+            Auto,
+            Light,
+            Dark
+        }
+
+        [SerializeField] private ColorThemeSO theme;
+
+        private MakerMode _mode = MakerMode.Vessel;
+        private SubTab _subTab = SubTab.Overview;
+        private Density _density = Density.Normal;
+        private ThemeMode _themeMode = ThemeMode.Auto;
+
+        private IToolView _vesselView;
+        private IToolView _miniGameView;
+
+        private Vector2 _scroll;
+
+        // Cached styles & spacing
+        private GUIStyle _headerStyle;
+        private GUIStyle _sectionTitle;
+        private float _vSpace = 6f;
+        private float _hSpace = 8f;
+
+        // “Layout style” placeholder
+        private readonly string[] _styleOptions = { "Default", "Compact", "Spacious" };
+        private int _styleIndex = 0;
+        
+        const string kResBase = "CosmicShore/MiniGameMaker/";
+        const string kThemeResName = kResBase + "ColorTheme";
+        const string kLibResName   = kResBase + "MiniGamePrefabLibrary";
+        const string kPrefTheme    = "CS_MGM_LastThemePath";
+        const string kPrefLib      = "CS_MGM_LastLibPath";
+
+        [MenuItem("FrogletTools/Cosmic Shore Mini Game Maker %#m")]
+        public static void Open()
+        {
+            var window = GetWindow<CosmicShoreMiniGameMakerWindow>("Cosmic Shore Mini Game Maker");
+            window.minSize = new Vector2(720, 420);
+            window.Show();
+        }
+
+        private void OnEnable()
+        {
+            _vesselView   = new VesselMakerView();
+            _miniGameView = new MiniGameMakerView();
+
+            // after view exists, try Resources auto-load
+            if (_miniGameView is MiniGameMakerView mgv)
+            {
+                var lib = Resources.Load<MiniGamePrefabLibrarySO>(kLibResName);
+                if (lib) mgv.SetLibrary(lib);  
+            }
+
+            if (!theme) theme = Resources.Load<ColorThemeSO>("CosmicShore/MiniGameMaker/ColorTheme");
+
+            BuildStyles();
+        }
+
+        private void OnValidate()
+        {
+            EditorApplication.delayCall += () =>
+            {
+                if (this) BuildStyles();
+            };
+        }
+
+        private void BuildStyles()
+        {
+            if (EditorStyles.boldLabel == null) return;
+            
+            // Theme resolve
+            var editorDark = EditorGUIUtility.isProSkin;
+            var useDark =
+                _themeMode == ThemeMode.Auto ? editorDark :
```

</details>

### `b84c8e46e` — Game Tool Maker Part 2

_Shombith03, 2025-10-17 08:42:18 +0530_

```text
 .../{Player and Ship Spawner.prefab => PlayerAndShipSpawner.prefab}   |   2 +-
 ... and Ship Spawner.prefab.meta => PlayerAndShipSpawner.prefab.meta} |   0
 Assets/_Scripts/Game/Player/MiniGamePlayerSpawnerAdapter.cs           |   5 +-
 .../_Scripts/MIniGameMaker/Editor/CosmicShoreMiniGameMakerWindow.cs   |   2 +-
 Assets/_Scripts/MIniGameMaker/Editor/MiniGameProfileSO.cs             |  14 ++-
 Assets/_Scripts/MIniGameMaker/Editor/MiniGameSceneAssembler.cs        | 178 +++++++++++++++++++++++---------
 6 files changed, 147 insertions(+), 54 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 150 of 392 lines)</summary>

```diff
diff --git a/Assets/_Scripts/Game/Player/MiniGamePlayerSpawnerAdapter.cs b/Assets/_Scripts/Game/Player/MiniGamePlayerSpawnerAdapter.cs
index c3fb2e8ac..b6f151bfe 100644
--- a/Assets/_Scripts/Game/Player/MiniGamePlayerSpawnerAdapter.cs
+++ b/Assets/_Scripts/Game/Player/MiniGamePlayerSpawnerAdapter.cs
@@ -5,7 +5,8 @@ namespace CosmicShore.Game
 {
     public class MiniGamePlayerSpawnerAdapter : PlayerSpawnerAdapterBase
     {
-        [SerializeField] private bool _spawnAIAtStart = false;
+        // [SerializeField] private bool _spawnAIAtStart = false;
+        [SerializeField] private bool _spawnDefaultPlayerAndAI;
 
         private void OnEnable()
         {
@@ -15,7 +16,7 @@ namespace CosmicShore.Game
 
         private void Start()
         {
-            if (_spawnAIAtStart)
+            if (_spawnDefaultPlayerAndAI)
                 SpawnAIPlayersAndAddToGameData();
         }
 
diff --git a/Assets/_Scripts/MIniGameMaker/Editor/CosmicShoreMiniGameMakerWindow.cs b/Assets/_Scripts/MIniGameMaker/Editor/CosmicShoreMiniGameMakerWindow.cs
index 5283ac64a..b368a2e85 100644
--- a/Assets/_Scripts/MIniGameMaker/Editor/CosmicShoreMiniGameMakerWindow.cs
+++ b/Assets/_Scripts/MIniGameMaker/Editor/CosmicShoreMiniGameMakerWindow.cs
@@ -448,7 +448,7 @@ namespace CosmicShore.Tools.MiniGameMaker
             info.hasMiniGameCamera = roots.Any(r => r.name == "MiniGameMainCamera");
             info.hasEnvironment = roots.Any(r => r.name == "Environment");
             info.hasGameCanvas = roots.Any(r => r.name == "GameCanvas");
-            info.hasPlayerSpawner = roots.Any(r => r.name == "PlayerandShipSpawner");
+            info.hasPlayerSpawner = roots.Any(r => r.name == "PlayerAndShipSpawner");
             // info.hasShipSpawner = roots.Any(r => r.name == "ShipSpawner");
 
             // Spawn points
diff --git a/Assets/_Scripts/MIniGameMaker/Editor/MiniGameProfileSO.cs b/Assets/_Scripts/MIniGameMaker/Editor/MiniGameProfileSO.cs
index a3d9ec031..a313d6238 100644
--- a/Assets/_Scripts/MIniGameMaker/Editor/MiniGameProfileSO.cs
+++ b/Assets/_Scripts/MIniGameMaker/Editor/MiniGameProfileSO.cs
@@ -1,4 +1,6 @@
-﻿using UnityEngine;
+﻿// Assets/_Tools/.../Editor/Data/MiniGameProfileSO.cs
+using UnityEngine;
+using CosmicShore.Game; // for IPlayer
 
 namespace CosmicShore.Tools.MiniGameMaker
 {
@@ -6,16 +8,20 @@ namespace CosmicShore.Tools.MiniGameMaker
     public sealed class MiniGameProfileSO : ScriptableObject
     {
         [Header("Core Data")]
-        public ScriptableObject miniGameData; // MiniGameDataSO
+        public CosmicShore.SOAP.MiniGameDataSO miniGameData;
 
         [Header("Scoring")]
         public bool golfRules;
-        public ScriptableObject[] scoringConfigs; // use your ScoringConfig[] if the editor asm can reference it
+        public ScriptableObject[] scoringConfigs;
 
         [Header("Turn")]
         public float turnDurationSeconds = 60f;
 
+        [Header("Spawner")]
+        public bool spawnDefaultPlayerAndAI;            
+        public IPlayer.InitializeData[] initializeDatas; 
+
         [Header("Events (optional)")]
-        public ScriptableObject[] eventsToAssign; // e.g., ScriptableEvent assets
+        public ScriptableObject[] eventsToAssign;
     }
 }
\ No newline at end of file
diff --git a/Assets/_Scripts/MIniGameMaker/Editor/MiniGameSceneAssembler.cs b/Assets/_Scripts/MIniGameMaker/Editor/MiniGameSceneAssembler.cs
index 7cccf87ae..9b9ae712d 100644
--- a/Assets/_Scripts/MIniGameMaker/Editor/MiniGameSceneAssembler.cs
+++ b/Assets/_Scripts/MIniGameMaker/Editor/MiniGameSceneAssembler.cs
@@ -1,8 +1,12 @@
-﻿using Unity.Netcode;
+﻿using System;
+using System.Linq;
+using System.Reflection;
+using Unity.Netcode;
 using UnityEditor;
 using UnityEditor.SceneManagement;
 using UnityEngine;
 using UnityEngine.SceneManagement;
+using Object = UnityEngine.Object;
 
 namespace CosmicShore.Tools.MiniGameMaker
 {
@@ -15,7 +19,8 @@ namespace CosmicShore.Tools.MiniGameMaker
             public string controllerClassName;
         }
 
-        public static Result CreateAndSave(string sceneName,
+        public static Result CreateAndSave(
+            string sceneName,
             MiniGamePrefabLibrarySO lib,
             bool draft,
             out string assetPath,
@@ -46,17 +51,16 @@ namespace CosmicShore.Tools.MiniGameMaker
             game.transform.rotation = Quaternion.identity;
             game.transform.localScale = Vector3.one;
 
-            var net = Undo.AddComponent<NetworkObject>(game);
+            Undo.AddComponent<NetworkObject>(game);
 
-            // Generate controller
-// Generate controller file
+            // 3a) Generate controller file & attach desired components
             string folderPath = $"Assets/_Game/MiniGames/{sceneName}";
             string controllerPath = MiniGameControllerCodegen.Generate(sceneName, folderPath);
             AssetDatabase.ImportAsset(controllerPath);
 
             string controllerFullName = $"CosmicShore.Game.MiniGames.{Sanitize(sceneName)}Controller";
 
-// Components we want on Game
+            // Components we want on Game
             string[] wantedTypes =
             {
                 controllerFullName,
@@ -81,7 +85,6 @@ namespace CosmicShore.Tools.MiniGameMaker
                 }
             }
 
-
             // 3b) Children: SpawnPoints/1/2
             var spRoot = new GameObject("SpawnPoints");
             Undo.RegisterCreatedObjectUndo(spRoot, "Create SpawnPoints");
@@ -96,20 +99,68 @@ namespace CosmicShore.Tools.MiniGameMaker
                 sp2.transform.position = new Vector3(5, 0, 0);
             }
 
-            // 4) Locked camera/env/canvas
-            TryInstantiateLocked(lib?.miniGameCameraPrefab, "MiniGameMainCamera", scene);
+            // 4) Locked camera/env/canvas (consistent names)
+            TryInstantiateLocked(lib?.miniGameCameraPrefab, "MiniGameCamera", scene);
             TryInstantiateLocked(lib?.environmentPrefab, "Environment", scene);
             TryInstantiateLocked(lib?.gameCanvasPrefab, "GameCanvas", scene);
 
-            // 5) Visible spawners
+            // 5) Visible spawners (combined Player & Ship spawner)
             TryInstantiateConfig(lib?.playerSpawnerPrefab, "PlayerAndShipSpawner", scene);
-            // TryInstantiateConfig(lib?.shipSpawnerPrefab, "ShipSpawner", scene);
-            
+
+            // 5a) Ensure & configure MiniGamePlayerSpawnerAdapter on PlayerAndShipSpawner
+            var spawnerGO = SceneManager.GetActiveScene()
+                                        .GetRootGameObjects()
```

</details>
