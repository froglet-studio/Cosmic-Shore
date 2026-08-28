using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CosmicShore.Editor.Froglet
{
    /// <summary>
    /// The WRITE half of the prefab-drift workflow. <see cref="PrefabInstanceSceneScanner"/> reads
    /// scene YAML directly because it is fast and read-only; every mutation goes back through
    /// <c>PrefabUtility</c> on a properly loaded scene, so Unity owns the serialization and the
    /// result is exactly what the Overrides dropdown would have produced by hand.
    ///
    /// Three operations, in increasing order of how much they change:
    ///   • <see cref="RevertInstance"/>      - the scene forgets its local edits.
    ///   • <see cref="ApplyInstance"/>       - the prefab absorbs this scene's edits.
    ///   • <see cref="ConsolidateUniform"/>  - the edits every scene agreed on move INTO the prefab
    ///                                         and disappear from all of them. This is the one that
    ///                                         turns N hand-maintained copies back into one source
    ///                                         of truth.
    /// </summary>
    public static class PrefabDriftFixer
    {
        public sealed class Result
        {
            public int ScenesTouched;
            public int PropertiesApplied;
            public int PropertiesReverted;
            public readonly List<string> Warnings = new();
            public bool Ok => Warnings.Count == 0;

            public override string ToString() =>
                $"{ScenesTouched} scene(s), {PropertiesApplied} applied, {PropertiesReverted} reverted" +
                (Warnings.Count > 0 ? $", {Warnings.Count} warning(s)" : "");
        }

        /// <summary>
        /// Ask the user to save anything dirty before we start opening scenes.
        /// Returns false if they cancel - callers must abort.
        /// </summary>
        public static bool PrepareForSceneWork()
            => EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo();

        // ── Single-instance operations ───────────────────────────────────────────

        public static Result RevertInstance(string scenePath, string prefabGuid)
            => ForEachInstance(new[] { scenePath }, prefabGuid, (root, res) =>
            {
                PrefabUtility.RevertPrefabInstance(root, InteractionMode.AutomatedAction);
                res.PropertiesReverted++;
            });

        public static Result ApplyInstance(string scenePath, string prefabGuid)
            => ForEachInstance(new[] { scenePath }, prefabGuid, (root, res) =>
            {
                if (string.IsNullOrEmpty(AssetDatabase.GUIDToAssetPath(prefabGuid)))
                {
                    res.Warnings.Add($"Prefab guid {prefabGuid} no longer resolves to an asset.");
                    return;
                }
                PrefabUtility.ApplyPrefabInstance(root, InteractionMode.AutomatedAction);
                res.PropertiesApplied++;
            });

        // ── The consolidation ────────────────────────────────────────────────────

        /// <summary>
        /// Moves every override in <paramref name="uniformKeys"/> out of the scenes and into the
        /// prefab: applied once from <paramref name="donorScenePath"/>, reverted everywhere else.
        ///
        /// Only keys the caller has already proven identical in every scene should be passed - see
        /// <see cref="PrefabInstanceSceneScanner.ClassifyOverrides"/>. Applying a value that is not
        /// uniform would silently impose one scene's choice on the others, which is the exact
        /// failure this whole workflow exists to undo.
        /// </summary>
        public static Result ConsolidateUniform(string prefabGuid, IReadOnlyList<string> scenePaths,
                                                ISet<string> uniformKeys, string donorScenePath)
        {
            var res = new Result();
            var assetPath = AssetDatabase.GUIDToAssetPath(prefabGuid);
            if (string.IsNullOrEmpty(assetPath))
            {
                res.Warnings.Add($"Prefab guid {prefabGuid} does not resolve to an asset.");
                return res;
            }
            if (uniformKeys == null || uniformKeys.Count == 0)
            {
                res.Warnings.Add("Nothing to consolidate - no uniform overrides were identified.");
                return res;
            }

            donorScenePath ??= scenePaths.FirstOrDefault();
            if (donorScenePath == null)
            {
                res.Warnings.Add("No scenes to consolidate.");
                return res;
            }

            // 1. Donor scene: push the uniform values into the prefab asset.
            RunInScene(donorScenePath, res, prefabGuid, root =>
            {
                res.PropertiesApplied += ForEachMatchingProperty(root, uniformKeys, (sp, _) =>
                {
                    try
                    {
                        PrefabUtility.ApplyPropertyOverride(sp, assetPath, InteractionMode.AutomatedAction);
                        return true;
                    }
                    catch (Exception e)
                    {
                        res.Warnings.Add($"{donorScenePath}: could not apply '{sp.propertyPath}' - {e.Message}");
                        return false;
                    }
                });
            });

            AssetDatabase.SaveAssets();

            // 2. Every other scene: drop the now-redundant local copies.
            foreach (var scene in scenePaths.Where(s => s != donorScenePath))
            {
                RunInScene(scene, res, prefabGuid, root =>
                {
                    res.PropertiesReverted += ForEachMatchingProperty(root, uniformKeys, (sp, _) =>
                    {
                        try
                        {
                            PrefabUtility.RevertPropertyOverride(sp, InteractionMode.AutomatedAction);
                            return true;
                        }
                        catch (Exception e)
                        {
                            res.Warnings.Add($"{scene}: could not revert '{sp.propertyPath}' - {e.Message}");
                            return false;
                        }
                    });
                });
            }

            AssetDatabase.SaveAssets();
            return res;
        }

        // ── Plumbing ─────────────────────────────────────────────────────────────

        static Result ForEachInstance(IEnumerable<string> scenePaths, string prefabGuid,
                                      Action<GameObject, Result> action)
        {
            var res = new Result();
            foreach (var scene in scenePaths)
                RunInScene(scene, res, prefabGuid, root => action(root, res));
            return res;
        }

        static void RunInScene(string scenePath, Result res, string prefabGuid, Action<GameObject> body)
        {
            Scene scene;
            try { scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single); }
            catch (Exception e)
            {
                res.Warnings.Add($"Could not open '{scenePath}': {e.Message}");
                return;
            }

            var roots = FindInstanceRoots(scene, prefabGuid);
            if (roots.Count == 0)
            {
                res.Warnings.Add($"No instance of the prefab found in '{scenePath}'.");
                return;
            }

            foreach (var root in roots)
                body(root);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            res.ScenesTouched++;
        }

        static List<GameObject> FindInstanceRoots(Scene scene, string prefabGuid)
        {
            var result = new List<GameObject>();
            if (string.IsNullOrEmpty(prefabGuid)) return result;
            var assetPath = AssetDatabase.GUIDToAssetPath(prefabGuid);
            if (string.IsNullOrEmpty(assetPath)) return result;

            foreach (var go in scene.GetRootGameObjects())
            {
                foreach (var t in go.GetComponentsInChildren<Transform>(true))
                {
                    var o = t.gameObject;
                    if (!PrefabUtility.IsAnyPrefabInstanceRoot(o)) continue;
                    if (PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(o) != assetPath) continue;
                    result.Add(o);
                }
            }
            return result;
        }

        /// <summary>
        /// Walks every overridden object under the instance and invokes <paramref name="visit"/> for
        /// each SerializedProperty whose (sourceGuid|sourceFileId|propertyPath) key is in
        /// <paramref name="keys"/>. Returns how many visits returned true.
        ///
        /// Both the ORIGINAL source (deepest nested prefab) and the immediate source are tried,
        /// because scene YAML addresses overrides against the original while
        /// <c>GetCorrespondingObjectFromSource</c> answers with the immediate one.
        /// </summary>
        static int ForEachMatchingProperty(GameObject instanceRoot, ISet<string> keys,
                                           Func<SerializedProperty, UnityEngine.Object, bool> visit)
        {
            int hits = 0;

            foreach (var oo in PrefabUtility.GetObjectOverrides(instanceRoot, false))
            {
                var inst = oo.instanceObject;
                if (inst == null) continue;

                var prefixes = SourceKeyPrefixes(inst);
                if (prefixes.Count == 0) continue;

                var so = new SerializedObject(inst);
                var it = so.GetIterator();
                bool changed = false;

                while (it.Next(true))
                {
                    if (!it.prefabOverride) continue;
                    if (!prefixes.Any(p => keys.Contains(p + it.propertyPath))) continue;
                    if (visit(it.Copy(), inst)) { hits++; changed = true; }
                }

                if (changed) so.ApplyModifiedPropertiesWithoutUndo();
            }

            return hits;
        }

        static List<string> SourceKeyPrefixes(UnityEngine.Object instanceObject)
        {
            var prefixes = new List<string>(2);
            AddPrefix(PrefabUtility.GetCorrespondingObjectFromOriginalSource(instanceObject), prefixes);
            AddPrefix(PrefabUtility.GetCorrespondingObjectFromSource(instanceObject), prefixes);
            return prefixes;

            static void AddPrefix(UnityEngine.Object src, List<string> into)
            {
                if (src == null) return;
                if (!AssetDatabase.TryGetGUIDAndLocalFileIdentifier(src, out var guid, out long fid)) return;
                var p = $"{guid}|{fid}|";
                if (!into.Contains(p)) into.Add(p);
            }
        }
    }
}
