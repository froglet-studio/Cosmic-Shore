using System.Collections.Generic;
using System.Linq;
using System.Text;
using CosmicShore.Gameplay;
using UnityEditor;
using UnityEngine;

namespace CosmicShore.Editor
{
    /// <summary>
    /// Swaps a vessel prefab's placeholder art for its rigged, element-labeled model — the one
    /// step of the elemental hull-morph rollout that cannot be hand-authored in the prefab YAML,
    /// because a SkinnedMeshRenderer's bone list, bindposes, bounds and imported mesh IDs are all
    /// owned by Unity's FBX importer.
    ///
    /// Dolphin, Urchin and Rhino ship a rigged model with charge/mass/space/time blend shapes
    /// (Assets/_Models/Vessel Models/*_shapekey_with_animations.fbx) but their prefabs still wire
    /// test/placeholder meshes that carry no shapes, so they are the three vessels that cannot
    /// morph. Each rig was authored FOR that vessel's animation script — the dolphin rig has the
    /// six jets and two jaws RiptideAnimation drives, the rhino rig the wings and engines
    /// RhinoAnimation drives — so once the model is in place the animation re-binds itself by
    /// bone name (VesselAnimation.ResolvePart) with no inspector work.
    ///
    /// What this tool does, per vessel:
    ///   1. Instantiates the rig under OrientationHandle at the old model's transform.
    ///   2. Re-parents gameplay carriers (impact colliders, guns, particles) onto the mapped bone,
    ///      preserving world pose — settings and references survive, volumes still need re-fitting.
    ///   3. Clears the animation component's part references so they resolve to the rig's bones.
    ///   4. Re-points VesselCustomization's ship geometry list at the new skinned mesh.
    ///   5. Deactivates (never deletes) the old model root, so the swap is reversible.
    ///
    /// Report first, apply second — and re-run "Audit Vessel Elemental Morphs" afterwards to
    /// confirm all four elements resolve on each vessel.
    /// </summary>
    public static class VesselRigWiringTool
    {
        const string VesselFolder = "Assets/_Prefabs/Spacevessels";
        const string ModelFolder = "Assets/_Models/Vessel Models";
        const string OrientationHandleName = "OrientationHandle";

        /// <summary>A vessel whose art should be the rigged, element-labeled model.</summary>
        struct RigSwap
        {
            public string VesselPrefab;
            public string RigFbx;
            public string LegacyModelRoot;
            /// <summary>Legacy part name → rig bone that should host its gameplay components.</summary>
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
                    { "Body",             "fuse"   },
                    { "GunContainer",     "fuse"   },
                    { "LeftGun",          "gunM.l" },
                    { "RightGun",         "gunM.r" },
                    { "JetTopLeft",       "jetT.l" },
                    { "JetTopRight",      "jetT.r" },
                    { "JetBottomLeft",    "jetB.l" },
                    { "JetBottomRight",   "jetB.r" },
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
                },
            },
        };

        [MenuItem("Tools/Cosmic Shore/Report Vessel Rig Swap (dry run)")]
        public static void Report() => Run(apply: false);

        [MenuItem("Tools/Cosmic Shore/Wire Vessel Rigs (applies changes)")]
        public static void Apply()
        {
            if (!EditorUtility.DisplayDialog(
                    "Wire vessel rigs",
                    "This rewrites the Dolphin, Urchin and Rhino prefabs: it swaps in their rigged " +
                    "element-labeled models, migrates gameplay components onto the new bones and " +
                    "deactivates the old model.\n\nCollider volumes will still need re-fitting to the " +
                    "new silhouettes by hand.\n\nCommit or back up first. Continue?",
                    "Wire them", "Cancel"))
                return;

            Run(apply: true);
        }

        static void Run(bool apply)
        {
            var report = new StringBuilder();
            report.AppendLine(apply ? "=== Wiring vessel rigs ===" : "=== Vessel rig swap - DRY RUN (nothing written) ===");
            report.AppendLine();

            foreach (var swap in Swaps)
            {
                report.AppendLine($"--- {swap.VesselPrefab}  ({swap.RigFbx})");
                try
                {
                    ProcessVessel(swap, apply, report);
                }
                catch (System.Exception e)
                {
                    report.AppendLine($"      ! FAILED: {e.Message}");
                }
                report.AppendLine();
            }

            if (apply)
            {
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                report.AppendLine("Next: run Tools > Cosmic Shore > Audit Vessel Elemental Morphs to confirm all "
                                  + "four elements resolve, then re-fit collider volumes to the new silhouettes.");
            }
            else
            {
                report.AppendLine("Dry run only. Use Tools > Cosmic Shore > Wire Vessel Rigs to apply.");
            }

            Debug.Log(report.ToString());
        }

        static void ProcessVessel(RigSwap swap, bool apply, StringBuilder report)
        {
            string prefabPath = $"{VesselFolder}/{swap.VesselPrefab}.prefab";
            string fbxPath = $"{ModelFolder}/{swap.RigFbx}.fbx";

            var rigAsset = AssetDatabase.LoadAssetAtPath<GameObject>(fbxPath);
            if (!rigAsset) { report.AppendLine($"      ! rig model not found at {fbxPath}"); return; }
            if (!AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath))
            {
                report.AppendLine($"      ! vessel prefab not found at {prefabPath}"); return;
            }

            var root = PrefabUtility.LoadPrefabContents(prefabPath);
            try
            {
                var handle = FindChild(root.transform, OrientationHandleName) ?? root.transform;
                var legacy = FindChild(root.transform, swap.LegacyModelRoot);
                var existingRig = FindChild(root.transform, swap.RigFbx);

                if (existingRig)
                {
                    report.AppendLine("      already wired - rig instance present, skipping (idempotent)");
                    return;
                }
                if (!legacy)
                {
                    report.AppendLine($"      ! legacy model root '{swap.LegacyModelRoot}' not found - skipping");
                    return;
                }

                report.AppendLine($"      rig → under '{handle.name}', replacing '{legacy.name}'");

                if (!apply)
                {
                    ReportPlan(swap, legacy, rigAsset, report);
                    return;
                }

                // 1. Instantiate the rig where the old model sat.
                var rig = (GameObject)PrefabUtility.InstantiatePrefab(rigAsset, handle);
                rig.name = swap.RigFbx;
                rig.transform.SetLocalPositionAndRotation(legacy.localPosition, legacy.localRotation);
                rig.transform.localScale = legacy.localScale;
                PrefabUtility.UnpackPrefabInstance(rig, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);

                var bones = rig.GetComponentsInChildren<Transform>(true)
                    .GroupBy(t => t.name)
                    .ToDictionary(g => g.Key, g => g.First(), System.StringComparer.OrdinalIgnoreCase);

                // 2. Migrate gameplay carriers onto their mapped bone, preserving world pose.
                int migrated = 0, unmapped = 0;
                foreach (var part in legacy.GetComponentsInChildren<Transform>(true).ToArray())
                {
                    if (!part || part == legacy || !CarriesGameplay(part.gameObject)) continue;
                    if (!swap.GameplayMigration.TryGetValue(part.name, out var boneName) ||
                        !bones.TryGetValue(boneName, out var bone))
                    {
                        report.AppendLine($"      ! '{part.name}' carries gameplay components but maps to no bone - left on the old model");
                        unmapped++;
                        continue;
                    }
                    part.SetParent(bone, worldPositionStays: true);
                    migrated++;
                }
                report.AppendLine($"      migrated {migrated} gameplay object(s)" + (unmapped > 0 ? $", {unmapped} unmapped" : ""));

                // 3. Clear the animation's part references so they re-resolve to the rig's bones.
                int cleared = ClearAnimationParts(root);
                report.AppendLine($"      cleared {cleared} animation part reference(s) - they resolve by bone name at Initialize");

                // 4. Point the ship geometry list at the new skinned mesh.
                int painted = RepointShipGeometries(root, rig);
                report.AppendLine($"      ship geometry list → {painted} skinned mesh renderer(s)");

                // 5. Retire the old model without destroying it.
                legacy.gameObject.SetActive(false);
                legacy.name = legacy.name + " (retired art)";
                report.AppendLine("      old model deactivated (kept for reference/undo)");

                var shapes = new List<VesselAnimation.ElementShapeTarget>();
                VesselAnimation.CollectElementShapes(rig.transform, shapes);
                report.AppendLine($"      element shapes on rig: {string.Join(", ", shapes.Select(s => s.Element).Distinct())}");
                report.AppendLine("      MANUAL FOLLOW-UP: re-fit collider volumes to the new silhouette.");

                // These three vessels never had a skinned mesh, so the base class's
                // SkinnedMeshRenderer field (what FlareEngine/FlareBody drive) is empty and those
                // flares silently do nothing. The rig finally supplies one - but wiring it turns
                // effects on, and FlareEngine indexes materials[3], so leave it to a human.
                var animation = root.GetComponent<VesselAnimation>();
                if (animation && !animation.SkinnedMeshRenderer)
                {
                    var renderer = rig.GetComponentInChildren<SkinnedMeshRenderer>(true);
                    report.AppendLine($"      OPTIONAL: VesselAnimation.SkinnedMeshRenderer is unwired - assign " +
                                      $"'{(renderer ? renderer.name : "?")}' to enable FlareEngine/FlareBody " +
                                      $"(FlareEngine needs materials[3]; this rig has " +
                                      $"{(renderer ? renderer.sharedMaterials.Length : 0)}).");
                }

                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        static void ReportPlan(RigSwap swap, Transform legacy, GameObject rigAsset, StringBuilder report)
        {
            var bones = rigAsset.GetComponentsInChildren<Transform>(true)
                .Select(t => t.name)
                .ToHashSet(System.StringComparer.OrdinalIgnoreCase);

            foreach (var part in legacy.GetComponentsInChildren<Transform>(true))
            {
                if (!part || !CarriesGameplay(part.gameObject)) continue;
                bool mapped = swap.GameplayMigration.TryGetValue(part.name, out var bone) && bones.Contains(bone);
                report.AppendLine(mapped
                    ? $"      '{part.name}' [{Components(part.gameObject)}] → bone '{bone}'"
                    : $"      ! '{part.name}' [{Components(part.gameObject)}] → NO MAPPED BONE");
            }

            var shapes = new List<VesselAnimation.ElementShapeTarget>();
            VesselAnimation.CollectElementShapes(rigAsset.transform, shapes);
            report.AppendLine($"      rig carries element shapes: {string.Join(", ", shapes.Select(s => s.Element).Distinct())}");
        }

        static string Components(GameObject go) =>
            string.Join(", ", go.GetComponents<Component>()
                .Where(c => c && !(c is Transform))
                .Select(c => c.GetType().Name));

        /// <summary>True when this object owns behaviour the swap must not drop on the floor.</summary>
        static bool CarriesGameplay(GameObject go) =>
            go.GetComponents<Component>().Any(c =>
                c && !(c is Transform) && !(c is MeshFilter) && !(c is MeshRenderer));

        static int ClearAnimationParts(GameObject root)
        {
            var animation = root.GetComponent<VesselAnimation>();
            if (!animation) return 0;

            int cleared = 0;
            var so = new SerializedObject(animation);
            var prop = so.GetIterator();
            bool enterChildren = true;
            while (prop.NextVisible(enterChildren))
            {
                enterChildren = false;
                if (prop.propertyType != SerializedPropertyType.ObjectReference) continue;
                if (prop.objectReferenceValue is not Transform) continue;
                // The drift handle is scene scaffolding, not model art - it must survive the swap.
                if (prop.name.ToLowerInvariant().Contains("drifthandle")) continue;
                prop.objectReferenceValue = null;
                cleared++;
            }
            if (cleared > 0) so.ApplyModifiedPropertiesWithoutUndo();
            return cleared;
        }

        static int RepointShipGeometries(GameObject root, GameObject rig)
        {
            var customization = root.GetComponent<VesselCustomization>();
            if (!customization) return 0;

            var renderers = rig.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            if (renderers.Length == 0) return 0;

            var so = new SerializedObject(customization);
            var list = so.FindProperty("_shipGeometries");
            if (list == null || !list.isArray) return 0;

            list.ClearArray();
            for (int i = 0; i < renderers.Length; i++)
            {
                list.InsertArrayElementAtIndex(i);
                list.GetArrayElementAtIndex(i).objectReferenceValue = renderers[i].gameObject;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
            return renderers.Length;
        }

        static Transform FindChild(Transform root, string name) =>
            root.GetComponentsInChildren<Transform>(true)
                .FirstOrDefault(t => t.name == name);
    }
}
