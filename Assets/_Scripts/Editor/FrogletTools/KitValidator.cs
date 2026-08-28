using System;
using System.Collections.Generic;
using System.Linq;
using CosmicShore.ScriptableObjects;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CosmicShore.Editor.Froglet
{
    public enum KitSeverity { Info = 0, Warning = 1, Error = 2 }

    /// <summary>One finding about a kit entry, optionally with a one-click remedy.</summary>
    public sealed class KitIssue
    {
        public KitSeverity Severity;
        public string Message;
        public Action Ping;
        public Action Fix;
        public string FixLabel;
        public string FixTooltip;
        public bool NeedsConfirm;
    }

    public sealed class KitEntryReport
    {
        public readonly List<KitIssue> Issues = new();
        public List<ScenePrefabInstance> Instances = new();
        public List<string> UniformKeys = new();
        public List<string> DivergentKeys = new();
    }

    /// <summary>
    /// The Validate pass behind <see cref="GameModePrefabKitWindow"/>.
    ///
    /// Answers three questions, in order of how much trouble they cause:
    ///   1. Is the prefab asset itself healthy (assigned, loadable, no missing scripts)?
    ///   2. Is it in the scene you have open, exactly once?
    ///   3. <b>Is any OTHER scene running an edited copy that was never applied back?</b>
    ///
    /// (3) is the one that silently defeats the point of a shared prefab: an override parked in a
    /// scene always wins over the prefab, so a change made in the prefab never reaches that scene.
    /// The report separates overrides that are IDENTICAL across every scene (which belong in the
    /// prefab and can be consolidated in one action) from ones that genuinely differ per scene
    /// (real configuration, left alone).
    ///
    /// Nothing here writes. Every fix is an explicit button in the window.
    /// </summary>
    public static class KitValidator
    {
        public static KitEntryReport Validate(GameModePrefabEntry entry, GameModePrefabKitSO kit)
        {
            var report = new KitEntryReport();
            if (entry?.Prefab == null)
            {
                report.Issues.Add(new KitIssue
                {
                    Severity = KitSeverity.Error,
                    Message = "No prefab assigned to this entry.",
                });
                return report;
            }

            var assetPath = AssetDatabase.GetAssetPath(entry.Prefab);
            if (!AssetDatabase.TryGetGUIDAndLocalFileIdentifier(entry.Prefab, out var guid, out long _))
            {
                report.Issues.Add(new KitIssue
                {
                    Severity = KitSeverity.Error,
                    Message = $"Could not resolve a GUID for '{assetPath}'. Is the asset imported?",
                });
                return report;
            }

            CheckAssetHealth(entry, assetPath, report);
            CheckActiveScene(entry, report);
            CheckCrossSceneDrift(entry, kit, guid, assetPath, report);

            if (report.Issues.Count == 0)
            {
                report.Issues.Add(new KitIssue
                {
                    Severity = KitSeverity.Info,
                    Message = "Clean: asset healthy, present in the open scene, no unapplied overrides anywhere.",
                });
            }
            return report;
        }

        // ── 1. Asset health ──────────────────────────────────────────────────────

        static void CheckAssetHealth(GameModePrefabEntry entry, string assetPath, KitEntryReport report)
        {
            var missing = new List<string>();
            foreach (var t in entry.Prefab.GetComponentsInChildren<Transform>(true))
            {
                var comps = t.GetComponents<Component>();
                for (int i = 0; i < comps.Length; i++)
                    if (comps[i] == null)
                        missing.Add(PathOf(t, entry.Prefab.transform));
            }

            if (missing.Count > 0)
            {
                report.Issues.Add(new KitIssue
                {
                    Severity = KitSeverity.Error,
                    Message = $"{missing.Count} missing script reference(s) on the prefab " +
                              $"(first: {missing[0]}). Fix these by hand - a null component " +
                              "cannot be repaired automatically without guessing the type.",
                    Ping = () => EditorGUIUtility.PingObject(entry.Prefab),
                });
            }

            var variantOf = PrefabUtility.GetCorrespondingObjectFromSource(entry.Prefab);
            if (variantOf != null)
            {
                var basePath = AssetDatabase.GetAssetPath(variantOf);
                report.Issues.Add(new KitIssue
                {
                    Severity = KitSeverity.Info,
                    Message = $"This is a prefab variant of '{System.IO.Path.GetFileName(basePath)}' - " +
                              "changes to the base propagate here automatically.",
                    Ping = () => EditorGUIUtility.PingObject(variantOf),
                });
            }
        }

        // ── 2. The scene you have open ───────────────────────────────────────────

        static void CheckActiveScene(GameModePrefabEntry entry, KitEntryReport report)
        {
            var scene = SceneManager.GetActiveScene();
            if (!scene.IsValid() || !scene.isLoaded) return;

            var hits = GameModePrefabKitWindow.FindInActiveScene(entry.Prefab);

            if (hits.Count == 0)
            {
                report.Issues.Add(new KitIssue
                {
                    Severity = entry.Required ? KitSeverity.Warning : KitSeverity.Info,
                    Message = entry.Required
                        ? $"Not present in the open scene '{scene.name}'. Required entries should be there."
                        : $"Not present in the open scene '{scene.name}' (optional).",
                });
            }
            else if (entry.Singleton && hits.Count > 1)
            {
                report.Issues.Add(new KitIssue
                {
                    Severity = KitSeverity.Error,
                    Message = $"{hits.Count} instances in '{scene.name}' but this entry is marked singleton.",
                    Ping = () => EditorGUIUtility.PingObject(hits[1]),
                    FixLabel = "Delete extras",
                    FixTooltip = $"Delete {hits.Count - 1} duplicate instance(s) from '{scene.name}', keeping the first.",
                    NeedsConfirm = true,
                    Fix = () =>
                    {
                        for (int i = hits.Count - 1; i >= 1; i--)
                            Undo.DestroyObjectImmediate(hits[i]);
                    },
                });
            }
        }

        // ── 3. Cross-scene drift - the one that matters ──────────────────────────

        static void CheckCrossSceneDrift(GameModePrefabEntry entry, GameModePrefabKitSO kit,
                                         string guid, string assetPath, KitEntryReport report)
        {
            var excludes = new List<string>();
            if (kit.GloballyExcludedScenes != null) excludes.AddRange(kit.GloballyExcludedScenes);
            if (entry.ExcludeScenesContaining != null) excludes.AddRange(entry.ExcludeScenesContaining);

            var scenes = PrefabInstanceSceneScanner.FindScenes(kit.SceneSearchFolders, excludes);
            var instances = PrefabInstanceSceneScanner.ScanScenes(scenes, new HashSet<string> { guid });
            report.Instances = instances;

            var ignored = kit.IgnoredPropertyPaths;
            var drifted = instances
                .Select(i => (inst: i, count: PrefabInstanceSceneScanner.MeaningfulOverrideCount(i, ignored)))
                .Where(t => t.count > 0 || t.inst.StructuralChanges > 0)
                .OrderByDescending(t => t.count)
                .ToList();

            if (drifted.Count == 0) return;

            var (uniform, divergent) = PrefabInstanceSceneScanner.ClassifyOverrides(
                drifted.Select(t => t.inst).ToList(), ignored);
            report.UniformKeys = uniform;
            report.DivergentKeys = divergent;

            // Headline: the consolidation opportunity.
            if (uniform.Count > 0 && drifted.Count > 1)
            {
                var scenePaths = drifted.Select(t => t.inst.ScenePath).Distinct().ToList();
                var donor = scenePaths[0];
                report.Issues.Add(new KitIssue
                {
                    Severity = KitSeverity.Warning,
                    Message = $"{uniform.Count} override(s) are IDENTICAL in all {scenePaths.Count} scenes - " +
                              $"they belong in the prefab, not the scenes. {divergent.Count} genuinely differ per scene.",
                    FixLabel = "Consolidate",
                    NeedsConfirm = true,
                    FixTooltip =
                        $"Apply {uniform.Count} identical override(s) to '{System.IO.Path.GetFileName(assetPath)}' " +
                        $"from '{System.IO.Path.GetFileNameWithoutExtension(donor)}', then revert them in the other " +
                        $"{scenePaths.Count - 1} scene(s).\n\nEvery listed scene will be opened and SAVED. " +
                        "The per-scene values that genuinely differ are left untouched.",
                    Fix = () =>
                    {
                        if (!PrefabDriftFixer.PrepareForSceneWork()) return;
                        var res = PrefabDriftFixer.ConsolidateUniform(
                            guid, scenePaths, new HashSet<string>(uniform), donor);
                        Debug.Log($"[PrefabKit] Consolidated '{assetPath}': {res}");
                        foreach (var w in res.Warnings) Debug.LogWarning($"[PrefabKit] {w}");
                    },
                });
            }

            // Per-scene detail.
            foreach (var (inst, count) in drifted)
            {
                var scenePath = inst.ScenePath;
                var structural = inst.StructuralChanges > 0
                    ? $", {inst.AddedGameObjects} added GO, {inst.RemovedGameObjects} removed GO, " +
                      $"{inst.AddedComponents} added comp, {inst.RemovedComponents} removed comp"
                    : "";
                var renamed = inst.RootNameOverride != null && inst.RootNameOverride != entry.Prefab.name
                    ? $"  [renamed \"{inst.RootNameOverride}\"]"
                    : "";

                report.Issues.Add(new KitIssue
                {
                    Severity = KitSeverity.Warning,
                    Message = $"{System.IO.Path.GetFileNameWithoutExtension(scenePath)}: " +
                              $"{count} unapplied override(s){structural}{renamed}",
                    Ping = () =>
                    {
                        var asset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(scenePath);
                        if (asset != null) { EditorGUIUtility.PingObject(asset); Selection.activeObject = asset; }
                    },
                    FixLabel = "Revert scene",
                    NeedsConfirm = true,
                    FixTooltip = $"Open '{scenePath}', discard ALL local overrides on this instance so it matches " +
                                 "the prefab exactly, and save the scene.\n\nUse Consolidate instead if the scene's " +
                                 "values are the ones you want to keep.",
                    Fix = () =>
                    {
                        if (!PrefabDriftFixer.PrepareForSceneWork()) return;
                        var res = PrefabDriftFixer.RevertInstance(scenePath, guid);
                        Debug.Log($"[PrefabKit] Reverted '{scenePath}': {res}");
                        foreach (var w in res.Warnings) Debug.LogWarning($"[PrefabKit] {w}");
                    },
                });
            }
        }

        // ── Helpers ──────────────────────────────────────────────────────────────

        static string PathOf(Transform t, Transform root)
        {
            var parts = new List<string>();
            var cur = t;
            while (cur != null && cur != root) { parts.Add(cur.name); cur = cur.parent; }
            parts.Add(root.name);
            parts.Reverse();
            return string.Join("/", parts);
        }
    }
}
