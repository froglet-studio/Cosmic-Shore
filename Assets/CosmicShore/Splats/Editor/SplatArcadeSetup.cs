using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using CosmicShore.Data;
using CosmicShore.ScriptableObjects;
using CosmicShore.Splats;

namespace CosmicShore.Tools.SplatImport
{
    // Wires the GSplat minigame into the arcade so it's playable end-to-end:
    //   - bakes a synthetic SplatPrismSet asset
    //   - duplicates MinigameFreestyle.unity -> MinigameGSplat.unity
    //   - drops a SplatCloud GameObject into that new scene
    //   - adds the scene to build settings
    //   - creates ArcadeGameGSplat.asset (Squirrel-only) and adds it to the AllGames/ArcadeGames lists
    //
    // Idempotent — re-running only touches assets that don't already exist.
    // Auto-runs once on project open via [InitializeOnLoad] so opening the branch is enough.
    [InitializeOnLoad]
    public static class SplatArcadeSetup
    {
        const string GSplatSceneName = "MinigameGSplat";
        const string FreestyleScenePath = "Assets/_Scenes/Singleplayer Scenes/MinigameFreestyle.unity";
        const string GSplatScenePath   = "Assets/_Scenes/Singleplayer Scenes/MinigameGSplat.unity";
        const string SyntheticSetPath  = "Assets/CosmicShore/Splats/Baked/SplatPrismSet_Synthetic.asset";
        const string ArcadeGameAsset   = "Assets/_SO_Assets/Games/ArcadeGameGSplat.asset";
        const string SquirrelVessel    = "Assets/_SO_Assets/Classes/SO_Class_Squirrel.asset";
        // AppManager.gameList points at OrganicRematchGames in CORE/AppManager.prefab; that's the
        // list ArcadeExploreView injects. The other two are kept in sync as a safety net since
        // other UI surfaces may pull from them. Add to all that exist.
        static readonly string[] GameListPaths =
        {
            "Assets/_SO_Assets/Games/GameLists/OrganicRematchGames.asset",
            "Assets/_SO_Assets/Games/GameLists/ArcadeGames.asset",
            "Assets/_SO_Assets/Games/GameLists/AllGames.asset",
        };

        const int SyntheticSplatCount = 4000;
        const float SyntheticVoxelSize = 0.05f;
        // Synthetic positions are in [-1,1]^3 by the generator's contract. Inflate to a flyable
        // tunnel — XY narrow, Z elongated so Squirrel actually traverses the cloud rather than
        // passing through it in one frame.
        static readonly Vector3 SyntheticPositionSpread = new Vector3(15f, 15f, 60f);
        static readonly Vector3 SyntheticPositionOffset = new Vector3(0f, 0f, 0f);
        // Scale up individual prisms so they're not microscopic next to the inflated tunnel.
        const float SyntheticPrismScale = 4f;

        static SplatArcadeSetup()
        {
            // Defer until after asset import and compile finish so AssetDatabase calls don't trip.
            EditorApplication.delayCall += AutoRunIfNeeded;
        }

        private static void AutoRunIfNeeded()
        {
            if (EditorApplication.isUpdating || EditorApplication.isCompiling)
            {
                EditorApplication.delayCall += AutoRunIfNeeded;
                return;
            }
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            // Idempotency gate: assume done if the SO_ArcadeGame asset already exists.
            if (AssetDatabase.LoadAssetAtPath<SO_ArcadeGame>(ArcadeGameAsset) != null) return;

            try { Run(silent: true); }
            catch (Exception ex)
            {
                Debug.LogWarning($"[SplatArcadeSetup] Auto-setup failed: {ex.Message}. Run Tools/Splats/Setup GSplat Arcade Game manually.");
            }
        }

        [MenuItem("Tools/Splats/Setup GSplat Arcade Game")]
        public static void RunMenu() => Run(silent: false);

        public static void Run(bool silent)
        {
            EnsureFolderTree("Assets/CosmicShore/Splats/Baked");

            var splatSet = EnsureSyntheticSplatSet();
            EnsureGSplatScene(splatSet);
            EnsureSceneInBuildSettings(GSplatScenePath);
            var arcadeGame = EnsureArcadeGameAsset();
            foreach (var listPath in GameListPaths)
                EnsureGameListContains(listPath, arcadeGame);
            AssetDatabase.SaveAssets();

            if (!silent)
                EditorUtility.DisplayDialog("GSplat Arcade Game",
                    "Setup complete. Enter Play Mode, open the Arcade screen, pick GSplat, choose Squirrel, and fly.",
                    "OK");
            Debug.Log("[SplatArcadeSetup] GSplat is wired in. Play -> Arcade -> GSplat (Squirrel).");
        }

        // 1) Bake (or reuse) a synthetic SplatPrismSet ----------------------------------------------
        private static SplatPrismSet EnsureSyntheticSplatSet()
        {
            var existing = AssetDatabase.LoadAssetAtPath<SplatPrismSet>(SyntheticSetPath);
            if (existing != null && existing.points != null && existing.points.Length > 0) return existing;

            var (raw, _) = SplatSyntheticTest.MakeSyntheticSplats(SyntheticSplatCount);
            var settings = SplatDecimateSettings.Default;
            settings.voxelSize = SyntheticVoxelSize;
            settings.maxPrisms = SplatPrismSet.MaxPrisms;
            var points = SplatDecimator.DecimateToPoints(raw, settings, out var report);
            InflateToTunnel(points, SyntheticPositionSpread, SyntheticPositionOffset, SyntheticPrismScale);
            Debug.Log($"[SplatArcadeSetup] Baked synthetic set. {report.ToLogString()}");
            return SplatBakeWindow.SaveBakedAsset(SyntheticSetPath, points, report, settings, sourcePath: "<synthetic>");
        }

        private static void InflateToTunnel(SplatPoint[] points, Vector3 spread, Vector3 offset, float prismScale)
        {
            // SH-decoded color of the synthetic generator is uniform magenta — replace with a
            // depth-and-radius hue ramp so the prototype reads as a structured cloud, not a blob.
            // (Discarded as soon as a real .ply is baked since real SH gives real colors.)
            for (int i = 0; i < points.Length; i++)
            {
                var p = points[i];
                p.position = new Vector3(p.position.x * spread.x, p.position.y * spread.y, p.position.z * spread.z) + offset;
                p.scale = p.scale * prismScale;

                float depthT = Mathf.InverseLerp(-spread.z, spread.z, p.position.z - offset.z);
                float radius = new Vector2(p.position.x - offset.x, p.position.y - offset.y).magnitude;
                float radialT = Mathf.Clamp01(radius / Mathf.Max(spread.x, spread.y));
                float hue = Mathf.Repeat(depthT * 0.85f + 0.05f, 1f);
                float sat = Mathf.Lerp(0.4f, 0.95f, radialT);
                float val = Mathf.Lerp(1f, 0.6f, radialT);
                p.color = Color.HSVToRGB(hue, sat, val);

                points[i] = p;
            }
        }

        // 2) Duplicate freestyle scene + drop a SplatCloud GameObject in it ------------------------
        private static void EnsureGSplatScene(SplatPrismSet splatSet)
        {
            bool sceneAlreadyExisted = File.Exists(GSplatScenePath);
            if (!sceneAlreadyExisted)
            {
                if (!File.Exists(FreestyleScenePath))
                    throw new FileNotFoundException($"Freestyle template scene not found at {FreestyleScenePath}.");
                if (!AssetDatabase.CopyAsset(FreestyleScenePath, GSplatScenePath))
                    throw new InvalidOperationException($"Failed to copy {FreestyleScenePath} -> {GSplatScenePath}.");
                AssetDatabase.ImportAsset(GSplatScenePath);
                Debug.Log($"[SplatArcadeSetup] Duplicated freestyle scene -> {GSplatScenePath}.");
            }

            // Open additively so we don't disturb whatever the user already has loaded.
            var scene = EditorSceneManager.OpenScene(GSplatScenePath, OpenSceneMode.Additive);
            try
            {
                bool added = TryAddSplatCloud(scene, splatSet);
                if (added)
                {
                    EditorSceneManager.MarkSceneDirty(scene);
                    EditorSceneManager.SaveScene(scene);
                }
            }
            finally
            {
                if (scene.IsValid() && EditorSceneManager.loadedSceneCount > 1)
                    EditorSceneManager.CloseScene(scene, removeScene: true);
            }
        }

        private static bool TryAddSplatCloud(Scene scene, SplatPrismSet splatSet)
        {
            foreach (var root in scene.GetRootGameObjects())
                if (root.name == "SplatCloud") return false; // already wired

            var go = new GameObject("SplatCloud");
            SceneManager.MoveGameObjectToScene(go, scene);
            var spawner = go.AddComponent<SplatPrismSpawner>();

            // Wire the private serialized [SerializeField] private SplatPrismSet set;
            var serialized = new SerializedObject(spawner);
            var setProp = serialized.FindProperty("set");
            if (setProp == null)
            {
                Debug.LogError("[SplatArcadeSetup] SplatPrismSpawner.set field not found via SerializedObject.");
                UnityEngine.Object.DestroyImmediate(go);
                return false;
            }
            setProp.objectReferenceValue = splatSet;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            // Lift the cloud root in front of the spawn so the Squirrel actually flies into it
            // instead of starting buried inside it. Adjust on the GameObject's transform if needed.
            go.transform.position = new Vector3(0f, 0f, 30f);
            Debug.Log("[SplatArcadeSetup] Added SplatCloud GameObject to MinigameGSplat scene.");
            return true;
        }

        // 3) Register the new scene in EditorBuildSettings ------------------------------------------
        private static void EnsureSceneInBuildSettings(string path)
        {
            var current = EditorBuildSettings.scenes;
            if (current.Any(s => s.path == path && s.enabled)) return;

            var updated = new List<EditorBuildSettingsScene>(current);
            var existing = updated.FindIndex(s => s.path == path);
            if (existing >= 0)
            {
                updated[existing] = new EditorBuildSettingsScene(path, true);
            }
            else
            {
                var guid = AssetDatabase.AssetPathToGUID(path);
                if (string.IsNullOrEmpty(guid))
                    throw new InvalidOperationException($"No asset GUID for {path} — was the scene copy committed?");
                updated.Add(new EditorBuildSettingsScene(path, true));
            }
            EditorBuildSettings.scenes = updated.ToArray();
            Debug.Log($"[SplatArcadeSetup] Added {path} to build settings.");
        }

        // 4) Create the SO_ArcadeGame asset --------------------------------------------------------
        private static SO_ArcadeGame EnsureArcadeGameAsset()
        {
            var existing = AssetDatabase.LoadAssetAtPath<SO_ArcadeGame>(ArcadeGameAsset);
            if (existing != null) return existing;

            var squirrel = AssetDatabase.LoadAssetAtPath<SO_Vessel>(SquirrelVessel);
            if (squirrel == null)
                throw new FileNotFoundException($"Squirrel vessel SO not found at {SquirrelVessel}.");

            var arcadeGame = ScriptableObject.CreateInstance<SO_ArcadeGame>();
            arcadeGame.name = "ArcadeGameGSplat";
            arcadeGame.Mode = GameModes.GSplat;
            arcadeGame.IsMultiplayer = false;
            arcadeGame.DisplayName = "GSplat";
            arcadeGame.Description = "Fly the Squirrel through a HyperSea oddity reconstructed from a 3D Gaussian Splat. No score, no timer — just structure and color.";
            arcadeGame.GolfScoring = false;
            arcadeGame.SceneName = GSplatSceneName;
            arcadeGame.Vessels = new List<SO_Vessel> { squirrel };
            arcadeGame.MinPlayersAllowed = 1;
            arcadeGame.MaxPlayersAllowed = 1;
            arcadeGame.MinIntensity = 1;
            arcadeGame.MaxIntensity = 1;

            AssetDatabase.CreateAsset(arcadeGame, ArcadeGameAsset);
            Debug.Log($"[SplatArcadeSetup] Created {ArcadeGameAsset}.");
            return arcadeGame;
        }

        // 5) Add the SO_ArcadeGame to the appropriate game lists ----------------------------------
        private static void EnsureGameListContains(string listPath, SO_ArcadeGame game)
        {
            var list = AssetDatabase.LoadAssetAtPath<SO_GameList>(listPath);
            if (list == null)
            {
                Debug.LogWarning($"[SplatArcadeSetup] Game list not found at {listPath} — skipping.");
                return;
            }
            if (list.Games == null) list.Games = new List<SO_ArcadeGame>();
            if (list.Games.Contains(game)) return;

            list.Games.Add(game);
            EditorUtility.SetDirty(list);
            Debug.Log($"[SplatArcadeSetup] Added GSplat to {listPath}.");
        }

        private static void EnsureFolderTree(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder)) return;
            var parts = folder.Split('/');
            string current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }
    }
}
