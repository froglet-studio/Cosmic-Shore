using System.Collections.Generic;
using System.Linq;
using System.Text;
using CosmicShore.Gameplay;
using UnityEditor;
using UnityEngine;
using CosmicShore.Editor.Froglet;

namespace CosmicShore.Editor
{
    /// <summary>
    /// Prints the exact plan for swapping a vessel's placeholder art for its rigged,
    /// element-labeled model — the last step of the elemental hull-morph rollout.
    ///
    /// Dolphin, Urchin and Rhino are the fleet's only vessels that cannot morph, and it is not a
    /// wiring oversight: their prefabs wire fundamentally different models (Dolphin_Test = 17
    /// separate static part meshes, Urchan_Test = 14, and Rhino wires Rhino_Test = 7),
    /// none of which carries a single blend shape. Their *_shapekey_with_animations rigs are one
    /// skinned mesh on an armature plus the four element shapes, and each was authored FOR that
    /// vessel's animation script — so once the model is in place the animation re-binds itself by
    /// bone name (<see cref="VesselAnimation.ResolvePart"/>) with no inspector work.
    ///
    /// <para><b>The WRITER is <see cref="VesselRigSwapper"/></b> (FrogletTools ▸ Vessels ▸ Swap
    /// Vessel Rig), added 2026-08-26. Two things this planner asserts were re-measured against the
    /// rigs when it was written, and one of them was wrong here:</para>
    /// <list type="bullet">
    /// <item>The bone mapping below is CORRECT for gameplay volumes — the rig's skin clusters put
    /// 538 vertices on each of <c>jetT/jetm/jetB</c>, which is exactly an <c>Engine case</c> mesh,
    /// and top/mid/bottom line up with <c>.1/.2/.3</c>.</item>
    /// <item>But a JET does not belong on those bones. <c>jetint/jetinm/jetinb</c> skin 712 each —
    /// the six <c>Engine Left/Right.N</c> inner meshes the shipped hull carries at
    /// <c>localScale 0.01</c> and therefore never draws. On the rig those are real exhaust bells,
    /// and that is where a plume comes out of.</item>
    /// <item>"Colliders must be re-fitted" does NOT hold for the Dolphin: its rig is the shipped
    /// hull in the same place (bounds agree on all six faces to three decimals), so the volumes are
    /// re-homed onto bones with their world pose preserved and no number changes. It DOES still
    /// hold for the Rhino (offset 1.5545 in z, wings re-posed) and the Urchin (uniform 2.105x
    /// scale). <c>Docs/VESSEL_CONSTRUCTION.md</c> §4.3-§4.4.</item>
    /// </list>
    ///
    /// This tool REPORTS ONLY — it never writes. The swap itself is a hands-on editor pass: a
    /// SkinnedMeshRenderer's bone list, bindposes, bounds and imported mesh IDs are owned by
    /// Unity's FBX importer, the collider volumes were authored against the old silhouettes and
    /// have to be re-fitted by eye, and one wrong re-parent silently welds the placeholder ship to
    /// the new skeleton. What is mechanical is knowing WHICH object belongs on WHICH bone, and
    /// that is what this prints — checked against the real rig and the real prefab every run.
    /// </summary>
    public static class VesselRigSwapPlanner
    {
        const string VesselFolder = "Assets/_Prefabs/Spacevessels";
        const string ModelFolder = "Assets/_Models/Vessel Models";
        const string OrientationHandleName = "OrientationHandle";

        struct RigSwap
        {
            public string VesselPrefab;
            public string RigFbx;
            public string LegacyModelRoot;
            /// <summary>Legacy part name → the rig bone that should host its gameplay volume.</summary>
            public Dictionary<string, string> GameplayMigration;
        }

        static readonly RigSwap[] Swaps =
        {
            new()
            {
                VesselPrefab = "Dolphin",
                RigFbx = "dolphin_shapekey_with_animations",
                LegacyModelRoot = "Dolphin_Test",
                GameplayMigration = new Dictionary<string, string>
                {
                    { "Dolphin_Test",         "fuse"   },
                    { "TopNose",              "jaw.u"  },
                    { "bottomNose",           "jaw.b"  },
                    { "LeftWing",             "wing.l" },
                    { "RightWing.001",        "wing.r" },
                    { "Engine case Left.1",   "jetT.l" },
                    { "Engine case Right.1",  "jetT.r" },
                    { "Engine case Left.2",   "jetm.l" },
                    { "Engine case Right.2",  "jetm.r" },
                    { "Engine case Left.3",   "jetB.l" },
                    { "Engine case Right.3",  "jetB.r" },
                },
            },
            new()
            {
                VesselPrefab = "Urchin",
                RigFbx = "urchan_shapekey_with_animations",
                LegacyModelRoot = "Body",
                GameplayMigration = new Dictionary<string, string>
                {
                    { "Body",             "fuse"          },
                    { "GunContainer",     "fuse"          },
                    { "LeftGun",          "gunM.l"        },
                    { "RightGun",         "gunM.r"        },
                    { "JetTopLeft",       "jetT.l"        },
                    { "JetTopRight",      "jetT.r"        },
                    { "JetBottomLeft",    "jetB.l"        },
                    { "JetBottomRight",   "jetB.r"        },
                    { "ShroudTopLeft",    "wingconrotT.l" },
                    { "ShroudTopRight",   "wingconrotT.r" },
                    { "ShroudBottomLeft", "wingconrotB.l" },
                    { "ShroudBottomRight","wingconrotB.r" },
                    { "ShroudLeft",       "sheildconrot.l"},
                    { "ShroudRight",      "sheildconrot.r"},
                },
            },
            new()
            {
                VesselPrefab = "Rhino",
                RigFbx = "rhino_shapekey_with_animations",
                LegacyModelRoot = "Rhino_Test (1)",
                GameplayMigration = new Dictionary<string, string>
                {
                    { "Rhino_Test (1)",     "fuse"    },
                    { "Wing front left",    "wing1.l" },
                    { "Wing front right",   "wing1.r" },
                    { "Wing back Left",     "wing2.l" },
                    { "Wing back right",    "wing2.r" },
                    { "engine left",        "jet.l"   },
                    { "engine right",       "jet.r"   },
                    { "JetTest",            "jet.r"   },
                },
            },
        };

        [MenuItem("FrogletTools/Vessels/Plan Vessel Rig Swap")]
        [FrogletTool(FrogletToolCategory.Vessels, Importance = 2,
            Description = "Report-only plan for moving a vessel onto its rigged model.")]
        public static void Plan()
        {
            var report = new StringBuilder();
            report.AppendLine("=== Vessel rig swap plan (REPORT ONLY - nothing is written) ===");
            report.AppendLine("Goal: replace shape-less placeholder art with the rigged, element-labeled model,");
            report.AppendLine("so these vessels morph with their element levels like the rest of the fleet.");
            report.AppendLine();

            foreach (var swap in Swaps)
            {
                report.AppendLine($"--- {swap.VesselPrefab}: {swap.LegacyModelRoot} → {swap.RigFbx}");
                PlanVessel(swap, report);
                report.AppendLine();
            }

            report.AppendLine("Procedure per vessel (in the prefab):");
            report.AppendLine("  1. Drag the rig FBX under 'OrientationHandle' and match the old model's transform.");
            report.AppendLine("  2. Move each gameplay object onto the bone listed above, then RE-FIT its collider");
            report.AppendLine("     to the new silhouette - the volumes were authored against the old art.");
            report.AppendLine("  3. Disable/delete the old model's renderers. Anything left under the old root is");
            report.AppendLine("     disabled with it, so re-home the 'NO MAPPED BONE' objects first.");
            report.AppendLine("  4. Clear the animation component's part fields - they re-resolve to the rig's");
            report.AppendLine("     bones by name (VesselAnimation.ResolvePart), so leave them EMPTY.");
            report.AppendLine("  5. Point VesselCustomization's ship geometry list at the rig's skinned mesh,");
            report.AppendLine("     or the hull will not take its domain colour.");
            report.AppendLine("  6. Re-run FrogletTools > Vessels > Audit Vessel Elemental Morphs to confirm all");
            report.AppendLine("     four elements resolve, and fly it to check the animation.");

            Debug.Log(report.ToString());
        }

        static void PlanVessel(RigSwap swap, StringBuilder report)
        {
            string prefabPath = $"{VesselFolder}/{swap.VesselPrefab}.prefab";
            string fbxPath = $"{ModelFolder}/{swap.RigFbx}.fbx";

            var rig = AssetDatabase.LoadAssetAtPath<GameObject>(fbxPath);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (!rig) { report.AppendLine($"      ! rig model not found at {fbxPath}"); return; }
            if (!prefab) { report.AppendLine($"      ! vessel prefab not found at {prefabPath}"); return; }

            var bones = rig.GetComponentsInChildren<Transform>(true)
                .ToDictionary(t => t.name, t => t, System.StringComparer.OrdinalIgnoreCase);

            var legacy = Find(prefab.transform, swap.LegacyModelRoot);
            if (!legacy) { report.AppendLine($"      ! legacy model root '{swap.LegacyModelRoot}' not found"); return; }

            if (!Find(prefab.transform, OrientationHandleName))
                report.AppendLine($"      ! no '{OrientationHandleName}' - place the rig under the vessel root instead");

            // Every object under the old model that owns behaviour, not just art. Each one has to
            // land somewhere on the rig, or it goes dark when the old model does.
            var orphans = new List<string>();
            foreach (var part in legacy.GetComponentsInChildren<Transform>(true))
            {
                if (!part || !CarriesGameplay(part.gameObject)) continue;

                string label = $"'{part.name}' [{Components(part.gameObject)}]";
                if (swap.GameplayMigration.TryGetValue(part.name, out var boneName))
                {
                    report.AppendLine(bones.ContainsKey(boneName)
                        ? $"      {label} → bone '{boneName}'"
                        : $"      ! {label} → bone '{boneName}' DOES NOT EXIST on this rig");
                }
                else
                {
                    report.AppendLine($"      ! {label} → NO MAPPED BONE - re-home it before disabling the old model");
                    orphans.Add(part.name);
                }
            }

            if (orphans.Count > 0)
                report.AppendLine($"      ({orphans.Count} object(s) would go dark with the old model: {string.Join(", ", orphans)})");

            var shapes = new List<VesselAnimation.ElementShapeTarget>();
            VesselAnimation.CollectElementShapes(rig.transform, shapes);
            report.AppendLine($"      rig element shapes: {string.Join(", ", shapes.Select(s => s.Element).Distinct())}"
                              + $" on {shapes.Select(s => s.Renderer.name).Distinct().Count()} skinned mesh(es)");

            var renderers = rig.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            if (renderers.Length > 0)
                report.AppendLine($"      ship geometry list → '{renderers[0].name}'"
                                  + $" ({renderers[0].sharedMaterials.Length} material slots)");

            var animation = prefab.GetComponent<VesselAnimation>();
            if (animation && !animation.SkinnedMeshRenderer)
                report.AppendLine("      OPTIONAL: VesselAnimation.SkinnedMeshRenderer is empty, so FlareEngine/"
                                  + "FlareBody do nothing on this vessel; the rig finally supplies one "
                                  + "(FlareEngine indexes material slot 3).");
        }

        static string Components(GameObject go) =>
            string.Join(", ", go.GetComponents<Component>()
                .Where(c => c && !(c is Transform))
                .Select(c => c.GetType().Name));

        /// <summary>True when this object owns behaviour the swap must not drop on the floor.</summary>
        static bool CarriesGameplay(GameObject go) =>
            go.GetComponents<Component>().Any(c =>
                c && !(c is Transform) && !(c is MeshFilter) && !(c is MeshRenderer));

        static Transform Find(Transform root, string name) =>
            root.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == name);
    }
}
