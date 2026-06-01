using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using CosmicShore.Data;
using CosmicShore.ScriptableObjects;
using CosmicShore.Splats;

namespace CosmicShore.Tools.SplatImport
{
    // Wires the GSplat minigame into the arcade so it's playable end-to-end:
    //   - bakes a synthetic SplatPrismSet asset under Resources/ for runtime loading
    //   - creates ArcadeGameGSplat.asset (Squirrel-only, single-player) pointing at the
    //     stock MinigameFreestyle scene
    //   - adds the asset to every arcade-shaped SO_GameList found in the project
    //   - cleans up any leftover MinigameGSplat.unity clone from the earlier approach
    //
    // The splat cloud itself is layered onto the freestyle scene at runtime by
    // SplatCloudRuntimeLoader — see that class. Reusing the stock scene keeps Reflex DI
    // and freestyle's player spawning intact, which the scene-clone approach broke.
    //
    // Idempotent — re-running only touches assets that don't already exist.
    // Auto-runs once on project open via [InitializeOnLoad] so opening the branch is enough.
    [InitializeOnLoad]
    public static class SplatArcadeSetup
    {
        // GSplat reuses the stock MinigameFreestyle scene to keep Reflex DI and player spawning
        // intact — the splat cloud is layered on at runtime by SplatCloudRuntimeLoader when it
        // detects GameMode == GSplat. The earlier scene-clone approach broke DI in the duplicate.
        const string FreestyleSceneName = "MinigameFreestyle";
        const string LegacyGSplatScenePath = "Assets/_Scenes/Singleplayer Scenes/MinigameGSplat.unity";
        // SplatPrismSet lives under a Resources folder so SplatCloudRuntimeLoader can find it at
        // runtime without serialized refs in the scene file.
        const string SyntheticSetPath  = "Assets/CosmicShore/Splats/Resources/SplatPrismSet_Synthetic.asset";
        const string LegacyBakedFolder = "Assets/CosmicShore/Splats/Baked";
        const string ArcadeGameAsset   = "Assets/_SO_Assets/Games/ArcadeGameGSplat.asset";
        const string SquirrelVessel    = "Assets/_SO_Assets/Classes/SO_Class_Squirrel.asset";
        // Different surfaces of the arcade UI bind to different SO_GameList assets — AppManager
        // registers one for DI but several screen prefabs have their own serialized references.
        // Add to every arcade-shaped list we can find so the game appears regardless of which one
        // the consumer actually reads from at runtime.
        const string GameListsFolder = "Assets/_SO_Assets/Games/GameLists";
        // Lists that should NOT receive GSplat (training programs, leaderboards) — kept as a
        // small skip list rather than an allow list so newly-added lists default to inclusion.
        static readonly HashSet<string> GameListsToSkip = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "TrainingGames",
            "MissionGames",
            "LeaderboardGames",
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
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorApplication.delayCall += AutoRunIfNeeded;
                return;
            }

            // Self-heal gate: if every artifact is already in place, skip silently. Otherwise,
            // run the full Run() — each Ensure* step is individually idempotent so this is cheap.
            if (IsFullyWired()) return;

            try { Run(silent: true); }
            catch (Exception ex)
            {
                Debug.LogError($"[SplatArcadeSetup] Auto-setup threw: {ex}\nRun Tools/Splats/Setup GSplat Arcade Game manually for a verbose retry.");
            }
        }

        private static bool IsFullyWired()
        {
            var arcadeGame = AssetDatabase.LoadAssetAtPath<SO_ArcadeGame>(ArcadeGameAsset);
            if (arcadeGame == null) return false;
            if (arcadeGame.SceneName != FreestyleSceneName) return false;
            if (AssetDatabase.LoadAssetAtPath<SplatPrismSet>(SyntheticSetPath) == null) return false;
            foreach (var listPath in FindArcadeGameLists())
            {
                var list = AssetDatabase.LoadAssetAtPath<SO_GameList>(listPath);
                if (list != null && (list.Games == null || !list.Games.Contains(arcadeGame))) return false;
            }
            return true;
        }

        [MenuItem("Tools/Splats/Setup GSplat Arcade Game")]
        public static void RunMenu() => Run(silent: false);

        public static void Run(bool silent)
        {
            Debug.Log("[SplatArcadeSetup] Starting setup ...");
            EnsureFolderTree("Assets/CosmicShore/Splats/Resources");

            EnsureSyntheticSplatSet();
            CleanupLegacyArtifacts();
            var arcadeGame = EnsureArcadeGameAsset();

            var lists = FindArcadeGameLists();
            if (lists.Count == 0)
                Debug.LogWarning($"[SplatArcadeSetup] No SO_GameList assets found under {GameListsFolder}.");
            foreach (var listPath in lists)
                EnsureGameListContains(listPath, arcadeGame);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            string summary = SummarizeWiring(arcadeGame, lists);
            Debug.Log($"[SplatArcadeSetup] GSplat wiring complete:\n{summary}");
            if (!silent)
                EditorUtility.DisplayDialog("GSplat Arcade Game", "Setup complete.\n\n" + summary + "\n\nEnter Play Mode, open the Arcade, pick GSplat, choose Squirrel, and fly.", "OK");
        }

        // Removes scene/build-settings artifacts left behind by the earlier scene-clone approach.
        // The cloned MinigameGSplat.unity scene broke Reflex DI for the components inside it — we
        // now layer the splat cloud onto the stock freestyle scene at runtime instead.
        private static void CleanupLegacyArtifacts()
        {
            if (File.Exists(LegacyGSplatScenePath))
            {
                AssetDatabase.DeleteAsset(LegacyGSplatScenePath);
                Debug.Log($"[SplatArcadeSetup] Removed legacy clone {LegacyGSplatScenePath}.");
            }
            var scenes = EditorBuildSettings.scenes;
            if (scenes.Any(s => s.path == LegacyGSplatScenePath))
            {
                EditorBuildSettings.scenes = scenes.Where(s => s.path != LegacyGSplatScenePath).ToArray();
                Debug.Log("[SplatArcadeSetup] Removed legacy clone from build settings.");
            }
            // Move any baked synthetic asset out of the legacy non-Resources path.
            string legacyPath = LegacyBakedFolder + "/SplatPrismSet_Synthetic.asset";
            if (File.Exists(legacyPath) && !File.Exists(SyntheticSetPath))
            {
                EnsureFolderTree(Path.GetDirectoryName(SyntheticSetPath).Replace('\\', '/'));
                AssetDatabase.MoveAsset(legacyPath, SyntheticSetPath);
                Debug.Log($"[SplatArcadeSetup] Moved synthetic splat set into Resources: {SyntheticSetPath}");
            }
            else if (File.Exists(legacyPath))
            {
                AssetDatabase.DeleteAsset(legacyPath);
            }
        }

        [MenuItem("Tools/Splats/Diagnose GSplat Setup")]
        public static void Diagnose()
        {
            var arcadeGame = AssetDatabase.LoadAssetAtPath<SO_ArcadeGame>(ArcadeGameAsset);
            var splatSet = AssetDatabase.LoadAssetAtPath<SplatPrismSet>(SyntheticSetPath);
            var lists = FindArcadeGameLists();
            int containingCount = arcadeGame == null ? 0 :
                lists.Count(p =>
                {
                    var l = AssetDatabase.LoadAssetAtPath<SO_GameList>(p);
                    return l != null && l.Games != null && l.Games.Contains(arcadeGame);
                });
            string sceneOnAsset = arcadeGame != null ? arcadeGame.SceneName : "<no asset>";

            string report =
                $"ArcadeGameGSplat.asset:   {(arcadeGame != null ? "OK" : "MISSING")}\n" +
                $"  -> SceneName:           {sceneOnAsset} (expected {FreestyleSceneName})\n" +
                $"SplatPrismSet (Resources): {(splatSet != null ? $"OK ({(splatSet.points?.Length ?? 0)} pts)" : "MISSING")}\n" +
                $"Game lists found:         {lists.Count}\n" +
                $"Lists containing GSplat:  {containingCount}/{lists.Count}\n" +
                $"Legacy clone file:        {(File.Exists(LegacyGSplatScenePath) ? "PRESENT (run setup to delete)" : "absent")}";
            Debug.Log("[SplatArcadeSetup] Diagnosis:\n" + report);
            EditorUtility.DisplayDialog("GSplat Diagnosis", report + "\n\nIf any line is MISSING/wrong, run Tools/Splats/Setup GSplat Arcade Game.", "OK");
        }

        private static List<string> FindArcadeGameLists()
        {
            var results = new List<string>();
            if (!AssetDatabase.IsValidFolder(GameListsFolder)) return results;
            foreach (var guid in AssetDatabase.FindAssets("t:SO_GameList", new[] { GameListsFolder }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var name = Path.GetFileNameWithoutExtension(path);
                if (GameListsToSkip.Contains(name)) continue;
                results.Add(path);
            }
            return results;
        }

        private static string SummarizeWiring(SO_ArcadeGame game, List<string> lists)
        {
            var sb = new System.Text.StringBuilder();
            sb.Append("  arcade game asset: ").AppendLine(ArcadeGameAsset);
            sb.Append("  scene (reused):   ").AppendLine(FreestyleSceneName);
            sb.Append("  splat set:        ").AppendLine(SyntheticSetPath);
            sb.AppendLine("  added to:");
            foreach (var p in lists)
            {
                var l = AssetDatabase.LoadAssetAtPath<SO_GameList>(p);
                bool has = l != null && l.Games != null && l.Games.Contains(game);
                sb.Append("    ").Append(has ? "[+] " : "[ ] ").AppendLine(Path.GetFileNameWithoutExtension(p));
            }
            return sb.ToString();
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

        // 2) Create / refresh the SO_ArcadeGame asset (pointing at the stock freestyle scene)
        private static SO_ArcadeGame EnsureArcadeGameAsset()
        {
            var existing = AssetDatabase.LoadAssetAtPath<SO_ArcadeGame>(ArcadeGameAsset);
            var squirrel = AssetDatabase.LoadAssetAtPath<SO_Vessel>(SquirrelVessel);
            if (squirrel == null)
                throw new FileNotFoundException($"Squirrel vessel SO not found at {SquirrelVessel}.");

            if (existing != null)
            {
                bool dirty = false;
                if (existing.SceneName != FreestyleSceneName) { existing.SceneName = FreestyleSceneName; dirty = true; }
                if (existing.Mode != GameModes.GSplat) { existing.Mode = GameModes.GSplat; dirty = true; }
                if (existing.Vessels == null || existing.Vessels.Count != 1 || existing.Vessels[0] != squirrel)
                {
                    existing.Vessels = new List<SO_Vessel> { squirrel };
                    dirty = true;
                }
                if (dirty)
                {
                    EditorUtility.SetDirty(existing);
                    Debug.Log($"[SplatArcadeSetup] Refreshed fields on {ArcadeGameAsset} (SceneName -> {FreestyleSceneName}).");
                }
                return existing;
            }

            var arcadeGame = ScriptableObject.CreateInstance<SO_ArcadeGame>();
            arcadeGame.name = "ArcadeGameGSplat";
            arcadeGame.Mode = GameModes.GSplat;
            arcadeGame.IsMultiplayer = false;
            arcadeGame.DisplayName = "GSplat";
            arcadeGame.Description = "Fly the Squirrel through a HyperSea oddity reconstructed from a 3D Gaussian Splat. No score, no timer — just structure and color.";
            arcadeGame.GolfScoring = false;
            arcadeGame.SceneName = FreestyleSceneName;
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
