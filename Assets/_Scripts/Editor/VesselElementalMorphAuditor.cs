using System.Collections.Generic;
using System.Linq;
using System.Text;
using CosmicShore.Gameplay;
using CosmicShore.ScriptableObjects;
using UnityEditor;
using UnityEngine;
using CosmicShore.Editor.Froglet;

namespace CosmicShore.Editor
{
    /// <summary>
    /// Fleet-wide report of the elemental hull morphs: which vessel prefabs ship element-labeled
    /// blend shapes (charge / mass / space / time) that <see cref="VesselAnimation"/> will discover
    /// and glide between their extremes as element levels move through [0, 10]. Uses the exact
    /// runtime discovery (<see cref="VesselAnimation.CollectElementShapes"/>), so the report and the
    /// game can never disagree. Runs entirely on assets — no play mode.
    /// </summary>
    public static class VesselElementalMorphAuditor
    {
        const string VesselFolder = "Assets/_Prefabs/Spacevessels";

        [MenuItem("FrogletTools/Vessels/Audit Vessel Elemental Morphs")]
        [FrogletTool(FrogletToolCategory.Vessels, Importance = 4,
            Description = "Which vessel models carry element-labelled blend shapes.")]
        public static void Audit()
        {
            var report = new StringBuilder();
            report.AppendLine("=== Vessel elemental hull morphs ===");
            report.AppendLine("contract: skinned meshes label blend shapes by element name; " +
                              "VesselAnimation glides each between its extremes over element levels 0-10");
            report.AppendLine();

            int morphing = 0, total = 0;
            foreach (var path in AssetDatabase.FindAssets("t:Prefab", new[] { VesselFolder })
                         .Select(AssetDatabase.GUIDToAssetPath)
                         .Where(p => !string.IsNullOrEmpty(p))
                         .OrderBy(p => p))
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (!prefab || !prefab.GetComponentInChildren<VesselAnimation>(true)) continue;

                total++;
                var targets = new List<VesselAnimation.ElementShapeTarget>();
                VesselAnimation.CollectElementShapes(prefab.transform, targets);

                var boundElements = targets.Select(t => t.Element).Distinct().ToList();
                var missing = VesselElementalMorphConfigSO.MorphElements
                    .Where(e => !boundElements.Contains(e)).ToList();
                if (targets.Count > 0) morphing++;

                string status = targets.Count == 0 ? "NO ELEMENT SHAPES"
                    : missing.Count == 0 ? "all four elements"
                    : "partial";
                report.AppendLine($"--- {prefab.name}  {status}");
                foreach (var t in targets)
                    report.AppendLine($"      {t.Element,-6} '{t.ShapeName}' on " +
                                      $"{RelativePath(prefab.transform, t.Renderer.transform)} (extreme {t.FullWeight:0.#})");
                if (targets.Count > 0 && missing.Count > 0)
                    report.AppendLine($"      ! missing: {string.Join(", ", missing)}");
                report.AppendLine();
            }

            report.AppendLine($"{morphing}/{total} vessels ship element-labeled shape keys.");
            Debug.Log(report.ToString());
        }

        static string RelativePath(Transform root, Transform t)
        {
            var parts = new List<string>();
            for (var current = t; current && current != root; current = current.parent)
                parts.Insert(0, current.name);
            return string.Join("/", parts);
        }
    }
}
