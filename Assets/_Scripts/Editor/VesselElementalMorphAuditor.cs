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
    /// <para><b>It measures MAGNITUDE, not the label — and that is the whole point.</b>
    /// `VesselAnimation` discovers shapes BY NAME, so a rig carrying four shapes correctly named
    /// charge/mass/space/time reports a green audit while the hull morphs by exactly nothing. Two
    /// of the fleet's three unreferenced rigs are in that state: the Rhino's and the Urchin's four
    /// element shapes each index ONE vertex and move it 0.0000 units. Swapping either in and
    /// reading a four-shape report as success is worse than the current honest zero, which is why
    /// this audit will not do it (<c>Docs/VESSEL_CONSTRUCTION.md</c> §4).</para>
    ///
    /// <para><b>Why the threshold is RELATIVE.</b> An absolute epsilon is not enough, because there
    /// is a third state between "empty" and "real": a historical <c>Sparrow Missile.fbx</c> carried
    /// Charge and Time shapes indexing 243 and 309 vertices and moving them by 4e-6 units —
    /// non-zero, and 0.00004% of the model. Measured over every shipped vessel model, the two
    /// populations separate cleanly: REAL element shapes move 2.46%..17.94% of their mesh's
    /// bounding-box diagonal, and the fake ones move 0.0000%. <see cref="MinShapeTravelFraction"/>
    /// is picked from inside that measured gap — 24x below the smallest real shape and orders of
    /// magnitude above the largest fake one — rather than guessed.</para>
    ///
    /// <para><b>A shape can also be inert for a second, independent reason: nobody can SEE it.</b>
    /// A vessel can morph PROCEDURALLY (<see cref="IProceduralElementMorphSource"/> — the Scarab
    /// re-blends baked geometry deltas instead of driving shape keys), and it can carry blend
    /// shapes that never reach the screen because they live on a hidden placeholder model (the
    /// Scarab wraps the Sparrow FBX renderers-off). Both are reported for what they ARE:
    /// procedural coverage counts toward the elements a vessel morphs on, and shapes under a
    /// declared hidden legacy root are marked INERT rather than counted — without this the Scarab
    /// reads "all four elements" via a model nobody can see. The two inert reasons are disjoint
    /// and both apply: a shape counts only if it MOVES the hull and is actually DRAWN.</para>
    /// </summary>
    public static class VesselElementalMorphAuditor
    {
        const string VesselFolder = "Assets/_Prefabs/Spacevessels";

        /// <summary>
        /// How far a shape must move its farthest vertex, as a fraction of the mesh's own
        /// bounding-box diagonal, before it counts as a real morph. See the class doc: measured,
        /// not guessed — real shapes land at 0.0246..0.1794, empty ones at exactly 0.
        /// </summary>
        public const float MinShapeTravelFraction = 0.001f;

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

            int morphing = 0, total = 0, inertShapes = 0;
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

                // A LABELLED shape is not a SHAPE, and a shape nobody DRAWS is not one either.
                // Both exclusions apply: a target counts as live only if it moves the hull by more
                // than the measured threshold AND does not sit under a procedural source's hidden
                // legacy model. A procedural source also CONTRIBUTES elements of its own (its
                // baked deltas are real morphs).
                var procedural = prefab.GetComponentInChildren<IProceduralElementMorphSource>(true);
                var hiddenRoot = procedural?.HiddenLegacyModelRoot;

                var hidden = hiddenRoot
                    ? targets.Where(t => t.Renderer && t.Renderer.transform.IsChildOf(hiddenRoot)).ToList()
                    : new List<VesselAnimation.ElementShapeTarget>();
                var stillShapes = targets.Except(hidden)
                    .Where(t => Travel(t) < MinShapeTravelFraction).ToList();
                var live = targets.Except(hidden).Except(stillShapes).ToList();

                var boundElements = live.Select(t => t.Element).Distinct().ToList();
                if (procedural != null)
                    foreach (var e in procedural.ProceduralMorphElements)
                        if (!boundElements.Contains(e)) boundElements.Add(e);
                var missing = VesselElementalMorphConfigSO.MorphElements
                    .Where(e => !boundElements.Contains(e)).ToList();
                if (boundElements.Count > 0) morphing++;
                if (stillShapes.Count > 0) inertShapes += stillShapes.Count;

                string status = targets.Count == 0 && procedural == null ? "NO ELEMENT MORPHS"
                    : boundElements.Count == 0 ? "LABELLED BUT INERT - the hull morphs by NOTHING"
                    : missing.Count == 0 ? "all four elements"
                    : "partial";
                if (procedural != null) status += "  [procedural]";
                report.AppendLine($"--- {prefab.name}  {status}");
                if (procedural != null)
                    report.AppendLine($"      procedural source: {procedural.GetType().Name} " +
                                      $"({string.Join(", ", procedural.ProceduralMorphElements)})");
                foreach (var t in targets.Except(hidden))
                {
                    float travel = Travel(t);
                    string verdict = travel < 0f ? "mesh not readable - CANNOT MEASURE"
                        : travel >= MinShapeTravelFraction ? $"moves {travel:P3} of the hull"
                        : $"INERT ({travel:P4} of the hull)";
                    report.AppendLine($"      {t.Element,-6} '{t.ShapeName}' on " +
                                      $"{RelativePath(prefab.transform, t.Renderer.transform)} " +
                                      $"(extreme {t.FullWeight:0.#}) — {verdict}");
                }
                foreach (var t in hidden)
                    report.AppendLine($"      INERT  {t.Element,-6} '{t.ShapeName}' on hidden legacy model " +
                                      $"{RelativePath(prefab.transform, t.Renderer.transform)} — never drawn");
                if (boundElements.Count > 0 && missing.Count > 0)
                    report.AppendLine($"      ! missing: {string.Join(", ", missing)}");
                report.AppendLine();
            }

            report.AppendLine($"{morphing}/{total} vessels morph for elements (shape keys or procedural).");
            if (inertShapes > 0)
                report.AppendLine($"! {inertShapes} labelled element shape(s) move less than " +
                                  $"{MinShapeTravelFraction:P2} of their hull and are reported as INERT. " +
                                  "A labelled shape is not a shape (Docs/VESSEL_CONSTRUCTION.md §4).");
            Debug.Log(report.ToString());
        }

        /// <summary>
        /// How far this shape's farthest vertex travels, as a fraction of the mesh's own
        /// bounding-box diagonal. Returns -1 when the mesh cannot be read, which is reported
        /// rather than silently counted as either state.
        /// </summary>
        public static float Travel(VesselAnimation.ElementShapeTarget target)
        {
            var mesh = target.Renderer ? target.Renderer.sharedMesh : null;
            if (!mesh || mesh.vertexCount == 0) return -1f;

            int frames = mesh.GetBlendShapeFrameCount(target.ShapeIndex);
            if (frames <= 0) return 0f;

            var deltaVertices = new Vector3[mesh.vertexCount];
            var deltaNormals = new Vector3[mesh.vertexCount];
            var deltaTangents = new Vector3[mesh.vertexCount];
            float farthest = 0f;
            // The LAST frame is the shape's authored extreme, which is the pose the morph reaches.
            //
            // Every vessel model imports with `isReadable: 0`. That flag governs RUNTIME access —
            // the editor keeps an imported model's full data — so this call is expected to work
            // here. It is guarded anyway, because the failure mode is ambiguous in the one
            // direction that matters: if the API ever returned zeros instead of throwing, a REAL
            // morph would be reported INERT, which is indistinguishable from a genuinely empty
            // shape. A throw becomes "CANNOT MEASURE" and is reported as such rather than silently
            // becoming a verdict.
            try
            {
                mesh.GetBlendShapeFrameVertices(target.ShapeIndex, frames - 1,
                                                deltaVertices, deltaNormals, deltaTangents);
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[MorphAudit] could not read blend shape '{target.ShapeName}' on " +
                                 $"'{mesh.name}': {e.Message}. Reported as CANNOT MEASURE, not as inert.");
                return -1f;
            }
            for (int i = 0; i < deltaVertices.Length; i++)
            {
                float m = deltaVertices[i].magnitude;
                if (m > farthest) farthest = m;
            }

            float diagonal = mesh.bounds.size.magnitude;
            return diagonal > 0f ? farthest / diagonal : -1f;
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
