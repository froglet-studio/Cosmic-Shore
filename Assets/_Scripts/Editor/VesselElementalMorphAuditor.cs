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
    ///
    /// HONESTY PASS: a vessel can also morph PROCEDURALLY (<see cref="IProceduralElementMorphSource"/>
    /// — the Scarab re-blends baked geometry deltas instead of driving shape keys), and a vessel
    /// can carry blend shapes that never reach the screen because they live on a hidden
    /// placeholder model (the Scarab wraps the Sparrow FBX renderers-off). Both are reported for
    /// what they ARE: procedural coverage counts, and shapes under a declared hidden legacy root
    /// are marked INERT rather than counted — without this the Scarab reads "all four elements"
    /// via a model nobody can see.
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

                // A procedural source both CONTRIBUTES elements (its baked deltas are real
                // morphs) and DISCLAIMS shapes (anything under the legacy root it hid draws
                // nothing, so those shapes must not count as coverage).
                var procedural = prefab.GetComponentInChildren<IProceduralElementMorphSource>(true);
                var hiddenRoot = procedural?.HiddenLegacyModelRoot;
                var inert = hiddenRoot
                    ? targets.Where(t => t.Renderer.transform.IsChildOf(hiddenRoot)).ToList()
                    : new List<VesselAnimation.ElementShapeTarget>();
                var live = targets.Except(inert).ToList();

                var boundElements = live.Select(t => t.Element).Distinct().ToList();
                if (procedural != null)
                    foreach (var e in procedural.ProceduralMorphElements)
                        if (!boundElements.Contains(e)) boundElements.Add(e);
                var missing = VesselElementalMorphConfigSO.MorphElements
                    .Where(e => !boundElements.Contains(e)).ToList();
                if (boundElements.Count > 0) morphing++;

                string status = boundElements.Count == 0 ? "NO ELEMENT MORPHS"
                    : missing.Count == 0 ? "all four elements"
                    : "partial";
                if (procedural != null) status += "  [procedural]";
                report.AppendLine($"--- {prefab.name}  {status}");
                if (procedural != null)
                    report.AppendLine($"      procedural source: {procedural.GetType().Name} " +
                                      $"({string.Join(", ", procedural.ProceduralMorphElements)})");
                foreach (var t in live)
                    report.AppendLine($"      {t.Element,-6} '{t.ShapeName}' on " +
                                      $"{RelativePath(prefab.transform, t.Renderer.transform)} (extreme {t.FullWeight:0.#})");
                foreach (var t in inert)
                    report.AppendLine($"      INERT  {t.Element,-6} '{t.ShapeName}' on hidden legacy model " +
                                      $"{RelativePath(prefab.transform, t.Renderer.transform)} — never drawn");
                if (boundElements.Count > 0 && missing.Count > 0)
                    report.AppendLine($"      ! missing: {string.Join(", ", missing)}");
                report.AppendLine();
            }

            report.AppendLine($"{morphing}/{total} vessels morph for elements (shape keys or procedural).");
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
