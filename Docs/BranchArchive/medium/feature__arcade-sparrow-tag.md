# Branch archive: `feature/arcade-sparrow-tag`

_Snapshot 2026-10-08. Index: [README](../README.md#index--medium-branches-410-unmerged-commits)_

- **Last commit:** 2026-06-29 by Claude
- **Unmerged commits:** 4
- **Forked from:** `0d48ab5b5` (2026-02-25, Merge pull request #78 from froglet-studio/claude/add-missing-sounds-1ZgoJ)
- **Tip:** `56ee80cbd`
- **Files touched (6):**
  - `Assets/Editor/CreateSparrowTagScene.cs`
  - `Assets/_Scripts/Game/Arcade/BaseScoreTracker.cs`
  - `Assets/_Scripts/Game/Arcade/Scoring/JoustCollisionsScoring.cs`
  - `Assets/_Scripts/Game/Arcade/ScoringModes.cs`
  - `Assets/_Scripts/Game/Arcade/SparrowTagController.cs`
  - `CLAUDE.md`

### `6b05b5dfd` — docs: add CLAUDE.md with codebase guide for AI assistants

_Claude, 2026-03-04 23:51:23 +0000_

```text
Covers project structure, tech stack, namespace conventions, naming
rules, architecture patterns, git workflow, testing, and common gotchas.
```

```text
 CLAUDE.md | 274 ++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++
 1 file changed, 274 insertions(+)
```

<details><summary>Patch (code/doc/text files, first 150 of 280 lines)</summary>

```diff
diff --git a/CLAUDE.md b/CLAUDE.md
new file mode 100644
index 000000000..bdc5b2ef1
--- /dev/null
+++ b/CLAUDE.md
@@ -0,0 +1,274 @@
+# CLAUDE.md – Cosmic Shore AI Assistant Guide
+
+This file provides context, conventions, and workflows for AI assistants (Claude and others) working in this repository.
+
+---
+
+## Project Overview
+
+**Cosmic Shore** is a live-service mobile/PC game built on **Unity 6000.0.62f1**. It is a multiplayer arcade game with ships, elemental mechanics, minigames, and a social meta-layer (squads, quests, leaderboards).
+
+- **Primary targets**: PC (editor/dev), iOS, Android
+- **Secondary targets**: WebGL
+- **Engine**: Unity 6000.0.62f1
+- **Language**: C# (1,003+ scripts)
+- **License**: MIT
+- **Organization**: Froglet Games / froglet-studio
+
+---
+
+## Repository Layout
+
+```
+/
+├── Assets/                     # All game content and code
+│   ├── Scripts/
+│   │   ├── App/                # App-layer: UI screens, systems, services
+│   │   │   ├── Systems/        # Feature systems (Ads, Audio, Quests, Squads, XP, Loadout…)
+│   │   │   └── UI/             # Menu screens, modals, UI elements
+│   │   ├── Core/               # Core engine managers (GameManager, CameraManager, etc.)
+│   │   ├── Game/               # Gameplay logic
+│   │   │   ├── Arcade/         # Minigame modes
+│   │   │   ├── AI/             # Opponent AI
+│   │   │   ├── Animation/      # Animation controllers
+│   │   │   ├── Camera/         # Cinemachine-based camera system
+│   │   │   ├── FX/             # Visual and impact effects
+│   │   │   ├── Managers/       # Game-state managers (Arcade, Hangar…)
+│   │   │   ├── Multiplayer/    # Netcode game logic
+│   │   │   ├── Ship/           # Vessel mechanics and properties
+│   │   │   └── UI/             # In-game HUD controllers
+│   │   ├── Models/             # Data definitions (Enums, Structs, ScriptableObjects)
+│   │   ├── Utilities/          # Helpers (Pools, Network, Reporting, Effects)
+│   │   ├── Integrations/       # Third-party (Firebase, PlayFab, Analytics)
+│   │   ├── DialogueSystem/     # Dialogue management
+│   │   ├── Services/           # Auth and other service abstractions
+│   │   └── Soap/               # SOAP event-system utilities
+│   ├── Scenes/                 # Unity scene files
+│   ├── Prefabs/                # Reusable game object prefabs
+│   ├── ScriptableObjects/      # Data assets (ship configs, captain data, events…)
+│   ├── Shaders/                # HLSL / URP shader files
+│   └── Plugins/                # Third-party plugins (SOAP, etc.)
+├── ProjectSettings/            # Unity project configuration
+├── Packages/
+│   └── manifest.json           # Package Manager dependencies (76+ packages)
+├── Docs/                       # Technical documentation
+├── GIT_RULES.md                # Branching, commit, and PR standards
+└── README.md                   # Project overview
+```
+
+---
+
+## Technology Stack
+
+| Category | Technology |
+|---|---|
+| Engine | Unity 6000.0.62f1 |
+| Language | C# |
+| Async | Cysharp UniTask (async/await) |
+| DI | VContainer 1.6.3 |
+| Networking | Unity Netcode for GameObjects 2.5.0 + Transport 2.6.0 |
+| UI | Unity UGUI 2.0.0 + UIElements |
+| Rendering | Universal Render Pipeline (URP) 17.0.4 |
+| VFX | Unity Visual Effect Graph 17.0.4 |
+| Animation | Cinemachine 3.1.2, Animation Rigging 1.3.0 |
+| Events | SOAP (Scriptable Object As Property) |
+| Analytics | Firebase, Unity Analytics, PlayFab |
+| Audio | Wwise |
+| Ads | Unity Ads |
+| IAP | Unity In-App Purchasing 4.12.2 |
+| Testing | Unity Test Framework 1.6.0 |
+
+---
+
+## Namespace Conventions
+
+All code lives under the `CosmicShore.*` root namespace:
+
+```
+CosmicShore.App.*              App layer (UI, Systems)
+CosmicShore.Game.*             Core gameplay systems
+CosmicShore.Core.*             Core managers (GameManager, CameraManager)
+CosmicShore.Models.*           Data models (Enums, Structs, ScriptableObjects)
+CosmicShore.Utilities.*        Helper functions and utilities
+CosmicShore.Integrations.*     Third-party integrations (Firebase, PlayFab)
+CosmicShore.DialogueSystem.*   Dialogue management
+CosmicShore.Services.*         Service layer (Auth)
+CosmicShore.Soap.*             SOAP event-system utilities
+```
+
+---
+
+## Naming Conventions
+
+| Category | Convention | Example |
+|---|---|---|
+| Classes | PascalCase | `GameManager`, `DuelGameController` |
+| Methods | PascalCase | `RestartGame()`, `LaunchGameScene()` |
+| Private fields | camelCase with `_` or `m_` prefix | `_sceneNames`, `m_Profile` |
+| Public properties | PascalCase with accessors | `Profile { get; set; }` |
+| Constants | ALL_CAPS | `WAIT_FOR_SECONDS_BEFORE_SCENELOAD` |
+| Enums (type) | PascalCase | `enum Element` |
+| Enum values | PascalCase | `Charge`, `Mass`, `Space`, `Time`, `Omni` |
+| ScriptableObjects | `SO_` or `Scriptable` prefix | `SO_Captain`, `ScriptableEventBool` |
+| SerializeField | `[SerializeField] private Type _name` | `[SerializeField] SceneNameListSO _sceneNames;` |
+
+---
+
+## Architecture Patterns
+
+### 1. Manager Pattern (Singleton-like)
+Core systems use manager classes that act as singletons:
+- `GameManager` – game flow and scene loading
+- `CameraManager` – camera control
+- `StatsManager` – game statistics
+- `ThemeManager` – visual theming
+
+### 2. SOAP Event System
+ScriptableObject-based events for decoupled communication. Events are defined as assets:
+```csharp
+// Raise an event
+_onSceneTransition.Raise(true);
+
+// Subscribe in inspector or via code
+[SerializeField] ScriptableEventBool _onSceneTransition;
+```
+Event types: `ScriptableEventBool`, `ScriptableEventShipClassType`, etc.
+Base class: `ScriptableEvent<T>`
+
+### 3. EventBus Architecture
+Centralized event buses for cross-system communication (e.g., `LoginEventBus`).
+
+### 4. MVC/MVVM for UI
+UI components follow a Controller-View-Model split:
+- **Controllers**: input handling, logic (`DialogueUIController`, `VesselHUDController`)
+- **Views**: rendering (`MinigameHUDView`)
```

</details>

### `199abfe08` — feat(arcade): add Sparrow Tag single-player dogfight minigame

_Claude, 2026-06-26 21:51:14 +0000_

```text
- SparrowTagController: inherits SinglePlayerMiniGameControllerBase,
  configures AI opponents to seek players (dogfight behavior)
- JoustCollisionsScoring: new scoring class that awards points per
  ship-to-ship collision on the local player's RoundStats
- ScoringModes: add JoustCollisions = 17 enum value
- BaseScoreTracker: wire JoustCollisions into the scoring factory

Scene setup still required in Unity editor: duplicate an existing
single-player scene, swap controller, add TimeBasedTurnMonitor,
and set ScoreTracker ScoringConfig to JoustCollisions.
```

```text
 Assets/_Scripts/Game/Arcade/BaseScoreTracker.cs               |  1 +
 Assets/_Scripts/Game/Arcade/Scoring/JoustCollisionsScoring.cs | 33 +++++++++++++++++++++++++++++++++
 Assets/_Scripts/Game/Arcade/ScoringModes.cs                   |  1 +
 Assets/_Scripts/Game/Arcade/SparrowTagController.cs           | 39 +++++++++++++++++++++++++++++++++++++++
 4 files changed, 74 insertions(+)
```

<details><summary>Patch (code/doc/text files)</summary>

```diff
diff --git a/Assets/_Scripts/Game/Arcade/BaseScoreTracker.cs b/Assets/_Scripts/Game/Arcade/BaseScoreTracker.cs
index 59f42b95f..bd750939f 100644
--- a/Assets/_Scripts/Game/Arcade/BaseScoreTracker.cs
+++ b/Assets/_Scripts/Game/Arcade/BaseScoreTracker.cs
@@ -131,6 +131,7 @@ namespace CosmicShore.Game.Arcade
                 ScoringModes.OmniCrystalsCollected => new CrystalsCollectedScoring(this, gameData, multiplier, CrystalsCollectedScoring.CrystalType.Omni),
                 ScoringModes.ElementalCrystalsCollected => new CrystalsCollectedScoring(this, gameData, multiplier, CrystalsCollectedScoring.CrystalType.Elemental),
                 ScoringModes.CrystalsCollectedScaleWithSize => new CrystalsCollectedScoring(this, gameData, multiplier, CrystalsCollectedScoring.CrystalType.Elemental, true),
+                ScoringModes.JoustCollisions => new JoustCollisionsScoring(this, gameData, multiplier),
                 _ => throw new ArgumentException($"Unknown scoring mode: {mode}")
             };
         }
diff --git a/Assets/_Scripts/Game/Arcade/Scoring/JoustCollisionsScoring.cs b/Assets/_Scripts/Game/Arcade/Scoring/JoustCollisionsScoring.cs
new file mode 100644
index 000000000..7eea684d4
--- /dev/null
+++ b/Assets/_Scripts/Game/Arcade/Scoring/JoustCollisionsScoring.cs
@@ -0,0 +1,33 @@
+using CosmicShore.Soap;
+
+namespace CosmicShore.Game.Arcade.Scoring
+{
+    /// <summary>
+    /// Awards points each time the local player lands a joust (ship-to-ship) collision.
+    /// Used by Sparrow Tag mode.
+    /// </summary>
+    public class JoustCollisionsScoring : BaseScoring
+    {
+        IRoundStats _localStats;
+
+        public JoustCollisionsScoring(IScoreTracker tracker, GameDataSO gameData, float multiplier)
+            : base(tracker, gameData, multiplier) { }
+
+        public override void Subscribe()
+        {
+            if (GameData.TryGetLocalPlayerStats(out _, out _localStats))
+                _localStats.OnJoustCollisionChanged += OnJoustCollision;
+        }
+
+        public override void Unsubscribe()
+        {
+            if (_localStats != null)
+                _localStats.OnJoustCollisionChanged -= OnJoustCollision;
+        }
+
+        void OnJoustCollision(IRoundStats stats)
+        {
+            Score += scoreMultiplier;
+        }
+    }
+}
diff --git a/Assets/_Scripts/Game/Arcade/ScoringModes.cs b/Assets/_Scripts/Game/Arcade/ScoringModes.cs
index e81ea483b..e60ef10d2 100644
--- a/Assets/_Scripts/Game/Arcade/ScoringModes.cs
+++ b/Assets/_Scripts/Game/Arcade/ScoringModes.cs
@@ -21,5 +21,6 @@ namespace CosmicShore.Game.Arcade
         FriendlyPrismsDestroyed = 14,
         LifeFormsKilled = 15,                   
         ElementalCrystalsCollectedBlitz = 16,
+        JoustCollisions = 17,
     }
 }
\ No newline at end of file
diff --git a/Assets/_Scripts/Game/Arcade/SparrowTagController.cs b/Assets/_Scripts/Game/Arcade/SparrowTagController.cs
new file mode 100644
index 000000000..88136d2c8
--- /dev/null
+++ b/Assets/_Scripts/Game/Arcade/SparrowTagController.cs
@@ -0,0 +1,39 @@
+using UnityEngine;
+
+namespace CosmicShore.Game.Arcade
+{
+    /// <summary>
+    /// Sparrow Tag: single-player dogfight — score points by colliding with AI Sparrow ships.
+    /// Turn ends via TimeBasedTurnMonitor configured in the scene.
+    /// Scoring via JoustCollisions ScoringMode on the ScoreTracker.
+    /// </summary>
+    public class SparrowTagController : SinglePlayerMiniGameControllerBase
+    {
+        [Header("Sparrow Tag")]
+        [SerializeField] [Range(0f, 1f)] float _aiSkillLevel = 0.5f;
+
+        protected override bool HasEndGame => true;
+        protected override bool ShouldResetPlayersOnTurnEnd => true;
+
+        protected override void SetupNewRound()
+        {
+            RaiseToggleReadyButtonEvent(true);
+            base.SetupNewRound();
+        }
+
+        protected override void OnCountdownTimerEnded()
+        {
+            ConfigureAIOpponents();
+            base.OnCountdownTimerEnded();
+        }
+
+        void ConfigureAIOpponents()
+        {
+            foreach (var player in gameData.Players)
+            {
+                if (!player.IsInitializedAsAI) continue;
+                player.Vessel.VesselStatus.AIPilot.ConfigureForGameMode(gameData, shouldSeekPlayers: true, _aiSkillLevel);
+            }
+        }
+    }
+}
```

</details>

### `900b920e6` — feat(arcade): add editor tool to build MinigameSparrowTag scene

_Claude, 2026-06-29 13:52:35 +0000_

```text
Copies MinigameCellularDuel.unity, swaps SinglePlayerCellularDuelController
for SparrowTagController, and sets ScoreTracker config to JoustCollisions.
Run via FrogletTools > Create > Sparrow Tag Scene in Unity editor.
```

```text
 Assets/Editor/CreateSparrowTagScene.cs | 109 +++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++
 1 file changed, 109 insertions(+)
```

<details><summary>Patch (code/doc/text files)</summary>

```diff
diff --git a/Assets/Editor/CreateSparrowTagScene.cs b/Assets/Editor/CreateSparrowTagScene.cs
new file mode 100644
index 000000000..06dc575ab
--- /dev/null
+++ b/Assets/Editor/CreateSparrowTagScene.cs
@@ -0,0 +1,109 @@
+using UnityEditor;
+using UnityEditor.SceneManagement;
+using UnityEngine;
+using CosmicShore.Game.Arcade;
+
+namespace CosmicShore.Editor
+{
+    /// <summary>
+    /// Menu item: FrogletTools > Create > Sparrow Tag Scene
+    ///
+    /// Duplicates MinigameCellularDuel, then:
+    ///   1. Swaps SinglePlayerCellularDuelController → SparrowTagController
+    ///   2. Sets scoring config to JoustCollisions (ship-to-ship hits)
+    ///   3. Saves the result as MinigameSparrowTag.unity
+    ///
+    /// TimeBasedTurnMonitor is already present in the source scene and is
+    /// reused as-is (default 60 s — adjust duration in the Inspector after creation).
+    /// </summary>
+    public static class CreateSparrowTagScene
+    {
+        const string SourceScene = "Assets/_Scenes/Singleplayer Scenes/MinigameCellularDuel.unity";
+        const string DestScene   = "Assets/_Scenes/Singleplayer Scenes/MinigameSparrowTag.unity";
+
+        [MenuItem("FrogletTools/Create/Sparrow Tag Scene")]
+        static void Build()
+        {
+            if (!System.IO.File.Exists(SourceScene))
+            {
+                Debug.LogError($"[SparrowTag] Source scene not found: {SourceScene}");
+                return;
+            }
+
+            // ── 1. Duplicate source scene ─────────────────────────────────────
+            AssetDatabase.CopyAsset(SourceScene, DestScene);
+            AssetDatabase.Refresh();
+
+            // ── 2. Open new scene ─────────────────────────────────────────────
+            var scene = EditorSceneManager.OpenScene(DestScene, OpenSceneMode.Single);
+
+            // ── 3. Swap controller ────────────────────────────────────────────
+            var oldController = Object.FindFirstObjectByType<SinglePlayerCellularDuelController>();
+            if (oldController != null)
+            {
+                SwapController(oldController);
+            }
+            else
+            {
+                // Controller might live on a prefab override; fall back to base type
+                var baseController = Object.FindFirstObjectByType<MiniGameControllerBase>();
+                if (baseController is SinglePlayerCellularDuelController duelCtrl)
+                    SwapController(duelCtrl);
+                else
+                    Debug.LogWarning("[SparrowTag] Could not find SinglePlayerCellularDuelController — add SparrowTagController manually.");
+            }
+
+            // ── 4. Update ScoreTracker → JoustCollisions ──────────────────────
+            var scoreTracker = Object.FindFirstObjectByType<ScoreTracker>();
+            if (scoreTracker != null)
+            {
+                var so = new SerializedObject(scoreTracker);
+                var configs = so.FindProperty("scoringConfigs");
+                configs.arraySize = 1;
+                var entry = configs.GetArrayElementAtIndex(0);
+                entry.FindPropertyRelative("Mode").enumValueIndex = (int)ScoringModes.JoustCollisions;
+                entry.FindPropertyRelative("Multiplier").floatValue = 1f;
+                so.ApplyModifiedProperties();
+                Debug.Log("[SparrowTag] ScoreTracker → JoustCollisions x1");
+            }
+            else
+            {
+                Debug.LogWarning("[SparrowTag] ScoreTracker not found — set ScoringConfig to JoustCollisions manually.");
+            }
+
+            // ── 5. Save ───────────────────────────────────────────────────────
+            EditorSceneManager.SaveScene(scene, DestScene);
+            AssetDatabase.Refresh();
+
+            Debug.Log($"[SparrowTag] Scene created: {DestScene}");
+            Debug.Log("[SparrowTag] TODO: In the Inspector, set TimeBasedTurnMonitor.Duration to your desired match length (e.g. 120 seconds).");
+        }
+
+        static void SwapController(SinglePlayerCellularDuelController old)
+        {
+            var go = old.gameObject;
+
+            // Read shared base-class references before destroying
+            var oldSo            = new SerializedObject(old);
+            var gameDataRef      = oldSo.FindProperty("gameData").objectReferenceValue;
+            var countdownRef     = oldSo.FindProperty("countdownTimer").objectReferenceValue;
+            var toggleButtonRef  = oldSo.FindProperty("_onToggleReadyButton").objectReferenceValue;
+            var rounds           = oldSo.FindProperty("numberOfRounds").intValue;
+            var turnsPerRound    = oldSo.FindProperty("numberOfTurnsPerRound").intValue;
+
+            Object.DestroyImmediate(old);
+
+            var newCtrl = go.AddComponent<SparrowTagController>();
+            var newSo   = new SerializedObject(newCtrl);
+
+            newSo.FindProperty("gameData").objectReferenceValue             = gameDataRef;
+            newSo.FindProperty("countdownTimer").objectReferenceValue       = countdownRef;
+            newSo.FindProperty("_onToggleReadyButton").objectReferenceValue = toggleButtonRef;
+            newSo.FindProperty("numberOfRounds").intValue                   = rounds;
+            newSo.FindProperty("numberOfTurnsPerRound").intValue            = turnsPerRound;
+
+            newSo.ApplyModifiedProperties();
+            Debug.Log($"[SparrowTag] Replaced controller on '{go.name}'");
+        }
+    }
+}
```

</details>

### `56ee80cbd` — fix(arcade): auto-set SparrowTag match duration to 120s in scene builder

_Claude, 2026-06-29 14:55:47 +0000_

```text
 Assets/Editor/CreateSparrowTagScene.cs | 25 ++++++++++++++++++-------
 1 file changed, 18 insertions(+), 7 deletions(-)
```

<details><summary>Patch (code/doc/text files)</summary>

```diff
diff --git a/Assets/Editor/CreateSparrowTagScene.cs b/Assets/Editor/CreateSparrowTagScene.cs
index 06dc575ab..853743a47 100644
--- a/Assets/Editor/CreateSparrowTagScene.cs
+++ b/Assets/Editor/CreateSparrowTagScene.cs
@@ -11,10 +11,8 @@ namespace CosmicShore.Editor
     /// Duplicates MinigameCellularDuel, then:
     ///   1. Swaps SinglePlayerCellularDuelController → SparrowTagController
     ///   2. Sets scoring config to JoustCollisions (ship-to-ship hits)
-    ///   3. Saves the result as MinigameSparrowTag.unity
-    ///
-    /// TimeBasedTurnMonitor is already present in the source scene and is
-    /// reused as-is (default 60 s — adjust duration in the Inspector after creation).
+    ///   3. Sets TimeBasedTurnMonitor duration to 120 seconds
+    ///   4. Saves the result as MinigameSparrowTag.unity
     /// </summary>
     public static class CreateSparrowTagScene
     {
@@ -71,12 +69,25 @@ namespace CosmicShore.Editor
                 Debug.LogWarning("[SparrowTag] ScoreTracker not found — set ScoringConfig to JoustCollisions manually.");
             }
 
-            // ── 5. Save ───────────────────────────────────────────────────────
+            // ── 5. Set match duration ─────────────────────────────────────────
+            var turnMonitor = Object.FindFirstObjectByType<TimeBasedTurnMonitor>();
+            if (turnMonitor != null)
+            {
+                var so = new SerializedObject(turnMonitor);
+                so.FindProperty("duration").floatValue = 120f;
+                so.ApplyModifiedProperties();
+                Debug.Log("[SparrowTag] TimeBasedTurnMonitor.Duration → 120 s");
+            }
+            else
+            {
+                Debug.LogWarning("[SparrowTag] TimeBasedTurnMonitor not found — set Duration manually on the 'Game' GameObject.");
+            }
+
+            // ── 6. Save ───────────────────────────────────────────────────────
             EditorSceneManager.SaveScene(scene, DestScene);
             AssetDatabase.Refresh();
 
-            Debug.Log($"[SparrowTag] Scene created: {DestScene}");
-            Debug.Log("[SparrowTag] TODO: In the Inspector, set TimeBasedTurnMonitor.Duration to your desired match length (e.g. 120 seconds).");
+            Debug.Log($"[SparrowTag] Scene ready: {DestScene}");
         }
 
         static void SwapController(SinglePlayerCellularDuelController old)
```

</details>
