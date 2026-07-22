using System.Collections.Generic;
using System.Linq;
using CosmicShore.Data;
using CosmicShore.Gameplay;
using CosmicShore.ScriptableObjects;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CosmicShore.Editor
{
    /// <summary>
    /// Tools &gt; Cosmic Shore &gt; Setup Fake Artist Minigame - authors everything the
    /// Fake Artist mode (GameModes.FakeArtist = 39) needs, idempotently
    /// (ToyboxSetupTool pattern):
    ///
    ///  1. Assets: FakeArtistScoringRule.asset (metric=Goals, points),
    ///     FakeArtistConfig.asset, ArcadeGameFakeArtist.asset (card, 3-12 players,
    ///     comeback off) + registration in GameLists/OrganicRematchGames.asset.
    ///  2. Scene: clones MinigameNucleusRush.unity → MinigameFakeArtist.unity
    ///     (never hand-write scene YAML - Unity 6 rejects it), then swaps the mode
    ///     components on the Game object, removes the Cell + NetworkCrystalManager
    ///     (no ecology in v1: fauna would eat the gallery and CellItems auto-shield
    ///     prisms, corrupting brush identities), grows the spawn ring to 12, clears
    ///     the MultiplayerHUDView domain-panel wiring (free-for-all uses the
    ///     per-player HUD layout), and rewires Scoreboard/PauseMenu/CountdownTimer
    ///     references.
    ///  3. Registers the scene in EditorBuildSettings.
    ///
    /// Re-run safe: existing assets/scene are re-wired, not recreated. After running,
    /// verify in-editor per FAKEARTIST.md's checklist (card icons, Menu_Main card-grid
    /// slot count, spawn-ring placement).
    /// </summary>
    public static class FakeArtistSetupTool
    {
        const string TemplateScenePath = "Assets/_Scenes/Multiplayer Scenes/MinigameNucleusRush.unity";
        const string ScenePath = "Assets/_Scenes/Multiplayer Scenes/MinigameFakeArtist.unity";
        const string RulePath = "Assets/_SO_Assets/Scoring Rules/FakeArtistScoringRule.asset";
        const string ConfigPath = "Assets/_SO_Assets/Games/FakeArtistConfig.asset";
        const string CardPath = "Assets/_SO_Assets/Games/ArcadeGameFakeArtist.asset";
        const string NucleusCardPath = "Assets/_SO_Assets/Games/ArcadeGameNucleusRush.asset";
        const string GameListPath = "Assets/_SO_Assets/Games/GameLists/OrganicRematchGames.asset";

        const int SpawnPointCount = 12;

        [MenuItem("Tools/Cosmic Shore/Setup Fake Artist Minigame")]
        public static void Setup()
        {
            var summary = new List<string>();

            var rule = LoadOrCreateRule(summary);
            var config = LoadOrCreateConfig(summary);
            var card = LoadOrCreateCard(summary);
            RegisterInGameList(card, summary);

            bool sceneOk = SetupScene(rule, config, summary);
            if (sceneOk)
                RegisterSceneInBuildSettings(summary);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            EditorUtility.DisplayDialog("Fake Artist Setup",
                string.Join("\n", summary) +
                "\n\nManual follow-ups (see FAKEARTIST.md):" +
                "\n• Card icons/background on ArcadeGameFakeArtist.asset (placeholders copied from Brood Rush)." +
                "\n• Verify Menu_Main's Arcade grid has a free GameCard slot for an 8th game." +
                "\n• Optionally add mode 39 to ProgressionConfig.asset alwaysUnlockedModes." +
                "\n• Fly the scene once and adjust the 12-spawn ring to taste.",
                "OK");
        }

        // ── Assets ──────────────────────────────────────────────────────────

        static FakeArtistScoringRuleSO LoadOrCreateRule(List<string> summary)
        {
            var rule = AssetDatabase.LoadAssetAtPath<FakeArtistScoringRuleSO>(RulePath);
            if (rule == null)
            {
                EnsureFolder("Assets/_SO_Assets/Scoring Rules");
                rule = ScriptableObject.CreateInstance<FakeArtistScoringRuleSO>();
                AssetDatabase.CreateAsset(rule, RulePath);
                summary.Add($"Created {RulePath}");
            }
            else summary.Add($"Found {RulePath}");

            var so = new SerializedObject(rule);
            so.FindProperty("metric").intValue = (int)ScoringMetric.Goals;
            so.FindProperty("golfRules").boolValue = false;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(rule);
            return rule;
        }

        static FakeArtistConfigSO LoadOrCreateConfig(List<string> summary)
        {
            var config = AssetDatabase.LoadAssetAtPath<FakeArtistConfigSO>(ConfigPath);
            if (config == null)
            {
                EnsureFolder("Assets/_SO_Assets/Games");
                config = ScriptableObject.CreateInstance<FakeArtistConfigSO>();
                AssetDatabase.CreateAsset(config, ConfigPath);
                summary.Add($"Created {ConfigPath}");
            }
            else summary.Add($"Found {ConfigPath}");
            return config;
        }

        static SO_ArcadeGame LoadOrCreateCard(List<string> summary)
        {
            var card = AssetDatabase.LoadAssetAtPath<SO_ArcadeGame>(CardPath);
            if (card != null)
            {
                summary.Add($"Found {CardPath}");
                return card;
            }

            EnsureFolder("Assets/_SO_Assets/Games");
            card = ScriptableObject.CreateInstance<SO_ArcadeGame>();
            card.Mode = GameModes.FakeArtist;
            card.IsMultiplayer = true;
            card.DisplayName = "Fake Artist";
            card.Description =
                "One artwork. Twelve brushes. One fraud. Draw your strokes, study the " +
                "canvas, and vote: what are we painting - and who's faking it? " +
                "First artist to 8 points takes the gallery.";
            card.GolfScoring = false;
            card.SceneName = "MinigameFakeArtist";
            card.MinPlayersAllowed = 3;
            card.MaxPlayersAllowed = 12;
            card.MinDomainsAllowed = 3;   // cosmetic in FFA - pinned so the modal hides the choice
            card.MaxDomainsAllowed = 3;
            card.MinIntensity = 1;
            card.MaxIntensity = 4;
            card.ComebackRatePerScoreDeficit = 0f; // social deduction - no elemental comeback

            // Placeholder art + vessel list borrowed from the Brood Rush card so the
            // card renders on day one; replace the icons in a follow-up.
            var nucleusCard = AssetDatabase.LoadAssetAtPath<SO_ArcadeGame>(NucleusCardPath);
            if (nucleusCard != null)
            {
                card.IconActive = nucleusCard.IconActive;
                card.IconInactive = nucleusCard.IconInactive;
                card.CardBackground = nucleusCard.CardBackground;
                card.Vessels = nucleusCard.Vessels != null
                    ? new List<SO_Vessel>(nucleusCard.Vessels)
                    : new List<SO_Vessel>();
            }

            AssetDatabase.CreateAsset(card, CardPath);
            summary.Add($"Created {CardPath}");
            return card;
        }

        static void RegisterInGameList(SO_ArcadeGame card, List<string> summary)
        {
            var list = AssetDatabase.LoadAssetAtPath<SO_GameList>(GameListPath);
            if (list == null)
            {
                summary.Add($"WARNING: game list not found at {GameListPath} - card NOT registered.");
                return;
            }

            if (list.Games != null && list.Games.Contains(card))
            {
                summary.Add("Card already registered in OrganicRematchGames.");
                return;
            }

            var so = new SerializedObject(list);
            var games = so.FindProperty("Games");
            games.arraySize++;
            games.GetArrayElementAtIndex(games.arraySize - 1).objectReferenceValue = card;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(list);
            summary.Add("Registered card in OrganicRematchGames.");
        }

        // ── Scene ───────────────────────────────────────────────────────────

        static bool SetupScene(FakeArtistScoringRuleSO rule, FakeArtistConfigSO config, List<string> summary)
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(TemplateScenePath) == null &&
                AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) == null)
            {
                summary.Add($"ERROR: template scene missing at {TemplateScenePath} - scene NOT created.");
                return false;
            }

            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) == null)
            {
                if (!AssetDatabase.CopyAsset(TemplateScenePath, ScenePath))
                {
                    summary.Add($"ERROR: failed to copy {TemplateScenePath} → {ScenePath}.");
                    return false;
                }
                summary.Add($"Cloned scene → {ScenePath}");
            }
            else summary.Add($"Found {ScenePath}");

            bool wasOpen = false;
            Scene scene = default;
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                var open = SceneManager.GetSceneAt(i);
                if (open.path == ScenePath) { scene = open; wasOpen = true; break; }
            }
            if (!wasOpen)
                scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);

            try
            {
                WireScene(scene, rule, config, summary);
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
            }
            finally
            {
                if (!wasOpen)
                    EditorSceneManager.CloseScene(scene, true);
            }
            return true;
        }

        static void WireScene(Scene scene, FakeArtistScoringRuleSO rule, FakeArtistConfigSO config, List<string> summary)
        {
            var roots = scene.GetRootGameObjects();

            T Find<T>() where T : Component =>
                roots.Select(r => r.GetComponentInChildren<T>(true)).FirstOrDefault(c => c != null);

            var monitorController = Find<TurnMonitorController>();
            if (monitorController == null)
            {
                summary.Add("ERROR: no TurnMonitorController in scene - aborting scene wiring.");
                return;
            }
            var gameGO = monitorController.gameObject;

            // Capture the template's serialized references before removing its components.
            var oldController = gameGO.GetComponentInChildren<NucleusRushController>(true);
            var oldMonitor = gameGO.GetComponentInChildren<NucleusRushWaveTurnMonitor>(true);

            Object countdownTimer = null, toggleReadyEvent = null, gameDataAsset = null, displayEvent = null;
            if (oldController != null)
            {
                var so = new SerializedObject(oldController);
                countdownTimer = so.FindProperty("countdownTimer")?.objectReferenceValue;
                toggleReadyEvent = so.FindProperty("_onToggleReadyButton")?.objectReferenceValue;
            }
            if (oldMonitor != null)
            {
                var so = new SerializedObject(oldMonitor);
                gameDataAsset = so.FindProperty("gameData")?.objectReferenceValue;
                displayEvent = so.FindProperty("onUpdateTurnMonitorDisplay")?.objectReferenceValue;
            }

            // Mode components: add ours first, then remove the template's.
            var controller = gameGO.GetComponent<FakeArtistController>();
            if (controller == null)
            {
                controller = gameGO.AddComponent<FakeArtistController>();
                summary.Add("Added FakeArtistController.");
            }
            var monitor = gameGO.GetComponent<FakeArtistTurnMonitor>();
            if (monitor == null)
            {
                monitor = gameGO.AddComponent<FakeArtistTurnMonitor>();
                summary.Add("Added FakeArtistTurnMonitor.");
            }

            if (oldController != null) { Object.DestroyImmediate(oldController); summary.Add("Removed NucleusRushController."); }
            if (oldMonitor != null) { Object.DestroyImmediate(oldMonitor); summary.Add("Removed NucleusRushWaveTurnMonitor."); }

            var crystalManager = Find<NetworkCrystalManager>();
            if (crystalManager != null)
            {
                Object.DestroyImmediate(crystalManager);
                summary.Add("Removed NetworkCrystalManager (no cell/crystals in Fake Artist v1).");
            }

            // The Cell: fauna would graze the gallery and CellItems auto-shield prisms
            // (corrupting brush identities) - Fake Artist v1 runs cell-less.
            var cell = Find<Cell>();
            if (cell != null)
            {
                Object.DestroyImmediate(cell.gameObject);
                summary.Add("Removed the Cell (Fake Artist v1 is cell-less - see FAKEARTIST.md).");
            }

            // Controller wiring.
            {
                var so = new SerializedObject(controller);
                so.FindProperty("rule").objectReferenceValue = rule;
                so.FindProperty("config").objectReferenceValue = config;
                if (so.FindProperty("countdownTimer") != null)
                    so.FindProperty("countdownTimer").objectReferenceValue = countdownTimer;
                if (so.FindProperty("_onToggleReadyButton") != null)
                    so.FindProperty("_onToggleReadyButton").objectReferenceValue = toggleReadyEvent;
                so.ApplyModifiedPropertiesWithoutUndo();
            }

            // Monitor wiring.
            {
                var so = new SerializedObject(monitor);
                so.FindProperty("controller").objectReferenceValue = controller;
                if (so.FindProperty("gameData") != null)
                    so.FindProperty("gameData").objectReferenceValue = gameDataAsset;
                if (so.FindProperty("onUpdateTurnMonitorDisplay") != null)
                    so.FindProperty("onUpdateTurnMonitorDisplay").objectReferenceValue = displayEvent;
                so.ApplyModifiedPropertiesWithoutUndo();
            }

            // TurnMonitorController.monitors = [our monitor].
            {
                var so = new SerializedObject(monitorController);
                var monitors = so.FindProperty("monitors");
                monitors.ClearArray();
                monitors.arraySize = 1;
                monitors.GetArrayElementAtIndex(0).objectReferenceValue = monitor;
                so.ApplyModifiedPropertiesWithoutUndo();
                summary.Add("TurnMonitorController → FakeArtistTurnMonitor.");
            }

            // Spawn ring: grow to 12 points.
            var initializer = Find<ServerPlayerVesselInitializerWithAI>();
            if (initializer != null)
            {
                var so = new SerializedObject(initializer);
                var points = so.FindProperty("playerSpawnPoints");
                var existing = new List<Transform>();
                for (int i = 0; i < points.arraySize; i++)
                {
                    if (points.GetArrayElementAtIndex(i).objectReferenceValue is Transform t && t != null)
                        existing.Add(t);
                }

                if (existing.Count > 0 && existing.Count < SpawnPointCount)
                {
                    var parent = existing[0].parent;
                    var centroid = Vector3.zero;
                    foreach (var t in existing) centroid += t.position;
                    centroid /= existing.Count;
                    float radius = Mathf.Max(120f, existing.Average(t => Vector3.Distance(t.position, centroid)));

                    for (int i = existing.Count; i < SpawnPointCount; i++)
                    {
                        float angle = (i + 0.5f) * (360f / SpawnPointCount);
                        var dir = Quaternion.Euler(0f, angle, 0f) * Vector3.forward;
                        var go = new GameObject((i + 1).ToString());
                        go.transform.SetParent(parent, false);
                        go.transform.position = centroid + dir * radius;
                        go.transform.rotation = Quaternion.LookRotation(dir);
                        existing.Add(go.transform);
                    }
                    summary.Add($"Spawn ring grown to {existing.Count} points.");
                }

                points.arraySize = existing.Count;
                for (int i = 0; i < existing.Count; i++)
                    points.GetArrayElementAtIndex(i).objectReferenceValue = existing[i];
                so.ApplyModifiedPropertiesWithoutUndo();
            }
            else summary.Add("WARNING: ServerPlayerVesselInitializerWithAI not found - spawn points untouched.");

            // Free-for-all HUD: clear the domain-panel wiring so MultiplayerHUD falls
            // back to the per-player card layout (HasDomainPanelWiring == false).
            var hudView = Find<CosmicShore.UI.MultiplayerHUDView>();
            if (hudView != null)
            {
                var so = new SerializedObject(hudView);
                if (so.FindProperty("allyDomainContainer") != null)
                    so.FindProperty("allyDomainContainer").objectReferenceValue = null;
                if (so.FindProperty("opposingDomainsContainer") != null)
                    so.FindProperty("opposingDomainsContainer").objectReferenceValue = null;
                if (so.FindProperty("domainPanelPrefab") != null)
                    so.FindProperty("domainPanelPrefab").objectReferenceValue = null;
                so.ApplyModifiedPropertiesWithoutUndo();
                summary.Add("MultiplayerHUDView domain panels cleared (per-player layout).");
            }

            // Replay buttons must point at the live controller.
            RewireGameController<CosmicShore.UI.Scoreboard>(roots, controller, summary);
            RewireGameController<CosmicShore.UI.PauseMenu>(roots, controller, summary);
        }

        static void RewireGameController<T>(GameObject[] roots, MiniGameControllerBase controller, List<string> summary)
            where T : Component
        {
            foreach (var root in roots)
            {
                foreach (var component in root.GetComponentsInChildren<T>(true))
                {
                    var so = new SerializedObject(component);
                    var prop = so.FindProperty("gameController");
                    if (prop == null) continue;
                    prop.objectReferenceValue = controller;
                    so.ApplyModifiedPropertiesWithoutUndo();
                    summary.Add($"{typeof(T).Name}.gameController → FakeArtistController.");
                }
            }
        }

        static void RegisterSceneInBuildSettings(List<string> summary)
        {
            var scenes = EditorBuildSettings.scenes.ToList();
            if (scenes.Any(s => s.path == ScenePath))
            {
                summary.Add("Scene already in Build Settings.");
                return;
            }
            scenes.Add(new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
            summary.Add("Scene added to Build Settings.");
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            var parent = System.IO.Path.GetDirectoryName(path)?.Replace('\\', '/');
            var leaf = System.IO.Path.GetFileName(path);
            if (!string.IsNullOrEmpty(parent) && !AssetDatabase.IsValidFolder(parent))
                EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, leaf);
        }
    }
}
