#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using System.Text;
using CosmicShore.Editor.Froglet;
using CosmicShore.ScriptableObjects;
using UnityEditor;
using UnityEngine;

namespace CosmicShore.Editor
{
    /// <summary>
    /// Reconciles the Editor's LIVE build-scene list against the arcade cards that need it.
    ///
    /// WHY THIS EXISTS.
    ///
    /// <c>ProjectSettings/EditorBuildSettings.asset</c> is NOT in the AssetDatabase, so Unity
    /// loads it once when the project opens and never re-reads it. Every arcade-mode generator
    /// (<c>Tools/Build/author_*_assets.py</c> → <c>arcade_mode_lib.register_build_scene</c>)
    /// writes that file HEADLESSLY. So a generator run while the Editor is open is invisible to
    /// the running Editor: the file on disk is correct, <c>check_gamelist_scenes.py</c> passes,
    /// the card renders normally, and pressing it fails with
    /// <c>"couldn't be loaded because it has not been added to the build settings scenes in
    /// build list"</c> — a message about a list that, on disk, does contain the scene.
    ///
    /// It is worse than invisible: the next time Unity saves project settings it writes its own
    /// stale in-memory list back over the file, silently DELETING the registration from the
    /// working tree. So the window between a generator run and an Editor restart is one in which
    /// the mode cannot be played AND its registration can be lost.
    ///
    /// This tool closes both halves. It asks each arcade card which scene it names, resolves that
    /// to a scene asset, and compares against <see cref="EditorBuildSettings.scenes"/> — the LIVE
    /// list, which is what the game actually loads from. Repairing writes through the API, which
    /// both fixes the running Editor and persists.
    ///
    /// General rule: a settings file outside the AssetDatabase is a file the Editor owns for the
    /// whole session — an external write to one is not a change, it is a change that has not
    /// happened yet.
    /// </summary>
    [InitializeOnLoad]
    public static class BuildSceneListReconciler
    {
        const string MenuPath = "FrogletTools/Game Modes/Reconcile Build Scene List";

        // ── Automatic repair ─────────────────────────────────────────────────────
        //
        // The menu item is the deliberate path; this is the one that actually saves you, because
        // the failure it closes is one nobody knows to look for. A generator writes the file, the
        // Editor never re-reads it, and the only symptom is a card that fails at the moment a
        // player commits to it. So the repair runs on every domain reload: pulling the branch,
        // touching any script, or exiting play mode is enough.
        //
        // It is safe to run unconditionally because it is a no-op in the only state that matters
        // (every card's scene already in the live list) and converges in one write otherwise.
        // `delayCall` rather than the static constructor: the AssetDatabase is not queryable
        // during static init.
        static BuildSceneListReconciler()
        {
            EditorApplication.delayCall += AutoRepair;
        }

        static void AutoRepair()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || BuildPipeline.isBuildingPlayer)
                return;

            Build(out var missing);
            if (missing.Count == 0) return;

            AddToLiveList(missing);
            Debug.LogWarning(
                "[BuildSceneListReconciler] the build-scene list this Editor session is holding was " +
                $"missing {missing.Count} scene(s) an arcade card names, so those cards could not be " +
                "launched. Added:\n  " + string.Join("\n  ", missing) +
                "\n\nThis happens because ProjectSettings/EditorBuildSettings.asset is not in the " +
                "AssetDatabase: a headless generator's write to it is invisible to an Editor that is " +
                "already open. Commit the change (FrogletTools > Build > Pending Tool Changes).");
        }

        [MenuItem(MenuPath)]
        [FrogletTool(FrogletToolCategory.GameModes, Importance = 4,
            Description = "Every arcade card's scene against the LIVE build list — catches a headless generator the running Editor never saw.")]
        static void Run()
        {
            var report = Build(out var missing);

            if (missing.Count == 0)
            {
                EditorUtility.DisplayDialog("Build Scene List", report, "OK");
                return;
            }

            bool add = EditorUtility.DisplayDialog(
                "Build Scene List",
                report + "\n\nAdd the missing scenes to the build list now?",
                "Add them", "Leave it");

            if (!add) return;

            AddToLiveList(missing);

            Debug.Log($"[BuildSceneListReconciler] added {missing.Count} scene(s):\n  " +
                      string.Join("\n  ", missing));
        }

        /// <summary>
        /// Builds the human-readable report and hands back the scene paths that are named by a
        /// card, exist on disk, and are absent from the live build list.
        /// </summary>
        static string Build(out List<string> missing)
        {
            missing = new List<string>();
            var disabled = new List<string>();
            var dangling = new List<string>();

            // The live list is what the game loads from. Disk may be ahead of it.
            var live = new Dictionary<string, EditorBuildSettingsScene>();
            foreach (var s in EditorBuildSettings.scenes)
                live[System.IO.Path.GetFileNameWithoutExtension(s.path)] = s;

            int cards = 0;
            foreach (var guid in AssetDatabase.FindAssets("t:SO_ArcadeGame"))
            {
                var card = AssetDatabase.LoadAssetAtPath<SO_ArcadeGame>(AssetDatabase.GUIDToAssetPath(guid));
                if (!card || string.IsNullOrWhiteSpace(card.SceneName)) continue;
                cards++;

                var name = card.SceneName.Trim();
                if (live.TryGetValue(name, out var entry))
                {
                    if (!entry.enabled) disabled.Add($"{name}  (card: {card.name})");
                    continue;
                }

                var path = ResolveScenePath(name);
                if (path == null) dangling.Add($"{name}  (card: {card.name})");
                else if (!missing.Contains(path)) missing.Add(path);
            }

            var sb = new StringBuilder();
            sb.AppendLine($"{cards} arcade cards checked against {live.Count} build-list entries.");
            sb.AppendLine();

            if (missing.Count == 0 && disabled.Count == 0 && dangling.Count == 0)
            {
                sb.AppendLine("Every card's scene is in the build list and enabled.");
                return sb.ToString();
            }

            if (missing.Count > 0)
            {
                sb.AppendLine($"MISSING from the live build list ({missing.Count}) —");
                sb.AppendLine("the scene exists on disk but this Editor session has never seen it");
                sb.AppendLine("registered. A headless generator writes EditorBuildSettings.asset,");
                sb.AppendLine("which Unity does not re-read; adding them here fixes the running");
                sb.AppendLine("Editor and persists.");
                foreach (var m in missing) sb.AppendLine("  " + m);
                sb.AppendLine();
            }

            if (disabled.Count > 0)
            {
                sb.AppendLine($"DISABLED ({disabled.Count}) — in the list but unchecked, so a build");
                sb.AppendLine("will not contain them:");
                foreach (var d in disabled) sb.AppendLine("  " + d);
                sb.AppendLine();
            }

            if (dangling.Count > 0)
            {
                sb.AppendLine($"NO SUCH SCENE ({dangling.Count}) — the card names a scene that does");
                sb.AppendLine("not exist on disk. This tool cannot fix it; fix the card or the scene:");
                foreach (var d in dangling) sb.AppendLine("  " + d);
            }

            return sb.ToString();
        }

        /// <summary>
        /// Appends to the LIVE list (Bootstrap keeps index 0) and records the write, because it
        /// lands in ProjectSettings/, which is a working-tree file like any other.
        /// </summary>
        static void AddToLiveList(IReadOnlyList<string> paths)
        {
            var scenes = EditorBuildSettings.scenes.ToList();
            foreach (var path in paths)
                scenes.Add(new EditorBuildSettingsScene(path, true));
            EditorBuildSettings.scenes = scenes.ToArray();

            FrogletToolChangeLedger.Record("Reconcile Build Scene List",
                                           "ProjectSettings/EditorBuildSettings.asset");
        }

        static string ResolveScenePath(string sceneName)
        {
            foreach (var guid in AssetDatabase.FindAssets($"t:Scene {sceneName}"))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (System.IO.Path.GetFileNameWithoutExtension(path) == sceneName) return path;
            }
            return null;
        }
    }
}
#endif
