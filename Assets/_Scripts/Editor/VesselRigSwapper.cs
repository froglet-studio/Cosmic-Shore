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
    /// Performs a vessel's rig swap — the WRITER half of
    /// <see cref="VesselRigSwapPlanner"/>, which only ever reported.
    ///
    /// <para><b>Why a tool and not hand-edited YAML.</b> Everything about this swap is measurable
    /// offline except one thing: a nested model instance references the FBX's sub-objects by
    /// Unity's imported fileIDs, and those are not derivable outside the editor when a model's
    /// <c>.meta</c> carries an empty <c>internalIDToNameTable</c> — which every vessel model here
    /// does (<c>Docs/VESSEL_TAIL_AND_JETS.md</c> §3.1). So the numbers below were measured from the
    /// FBX and the prefab; the instantiation is left to the importer, which is the only thing that
    /// knows those ids.</para>
    ///
    /// <para><b>What Phase 0 changed about this job</b> (<c>Docs/VESSEL_CONSTRUCTION.md</c> §4.3):
    /// the Dolphin rig is the shipped hull, in the SAME PLACE — the two files' world bounds agree
    /// on all six faces to three decimals and 8,311 of 12,583 shipped vertices sit within 5.5e-5 of
    /// a rig vertex. That is why this tool re-homes each gameplay object onto its bone with
    /// <c>worldPositionStays: true</c> and does NOT re-fit a single collider: the volumes were
    /// authored against geometry the rig reproduces exactly. Do not generalise that to another
    /// vessel — the Rhino's rig is the same hull offset 1.5545 in z with its wings re-posed, and
    /// the Urchin's is a uniform 2.105x scale (§4.4). Both need their numbers re-measured, which is
    /// why only the Dolphin is enabled here.</para>
    ///
    /// <para><b>The jets mount on the NOZZLE bones, not the engine-case bones.</b> The rig's skin
    /// weights settle it: <c>jetT/jetm/jetB</c> skin 538 vertices each — the engine CASE meshes —
    /// while <c>jetint/jetinm/jetinb</c> skin 712 each, which is exactly the six
    /// <c>Engine Left/Right.N</c> inner meshes the shipped hull carries at <c>localScale 0.01</c>
    /// (0.00095 units in a 5.3-unit ship, i.e. never drawn). The rig restores them as real exhaust
    /// bells, and that is where a plume comes out of.</para>
    ///
    /// <para>Idempotent: it finds the rig instance by name and reports "already swapped" rather
    /// than stacking a second one. It validates before saving, and writes nothing if a single
    /// mapped bone or legacy object is missing.</para>
    /// </summary>
    public class VesselRigSwapper : EditorWindow
    {
        const string ToolName = "Vessel Rig Swapper";
        const string VesselFolder = "Assets/_Prefabs/Spacevessels";
        const string ModelFolder = "Assets/_Models/Vessel Models";
        const string OrientationHandleName = "OrientationHandle";

        static readonly FrogletToolShipContext Ship = new FrogletToolShipContext(ToolName)
        {
            ToolScriptPaths = new[] { "Assets/_Scripts/Editor/VesselRigSwapper.cs" },
            CommitType = "feat",
            CommitScope = "vessel",
            CommitSubject = _ => "feat(vessel): swap the Dolphin onto its rigged, morphing hull",
        };

        /// <summary>One gameplay object under the legacy model, and the bone that should host it.</summary>
        struct PartMove
        {
            public string LegacyObject;
            public string Bone;
            public PartMove(string legacyObject, string bone) { LegacyObject = legacyObject; Bone = bone; }
        }

        /// <summary>One FX mount: which jet, which bone, and the mouth centre measured off the rig.</summary>
        struct JetMount
        {
            public string LegacyParent;
            public string Bone;
            public Vector3 MouthInVesselSpace;
            public JetMount(string legacyParent, string bone, Vector3 mouth)
            { LegacyParent = legacyParent; Bone = bone; MouthInVesselSpace = mouth; }
        }

        struct RigSwap
        {
            public string VesselPrefab;
            public string RigFbx;
            public string LegacyModelRoot;
            public PartMove[] Parts;
            public JetMount[] Jets;
            /// <summary>Legacy objects that exist only to draw geometry the rig now draws itself.</summary>
            public string[] RedundantMeshObjects;
        }

        // ── The Dolphin, measured ────────────────────────────────────────────────
        //
        // Bones from the rig's own skin clusters; mouth centres from the rearmost lip band of each
        // nozzle bone's skinned geometry, converted to vessel space by (-x, y, z) / 100 — the map
        // proven three ways in §4.3 (the root BoxCollider reproduces the Chassis mesh extent
        // 0.65185 x 1.20920 x 3.44816 to five decimals, and two engine transforms agree on the sign
        // flip). The jets currently sit 0.031-0.055 forward of these, inside the case's rear face.
        static readonly RigSwap[] Swaps =
        {
            new()
            {
                VesselPrefab = "Dolphin",
                RigFbx = "dolphin_shapekey_with_animations",
                LegacyModelRoot = "Dolphin_Test",
                Parts = new[]
                {
                    new PartMove("Dolphin_Test",        "fuse"),
                    new PartMove("TopNose",             "jaw.u"),
                    new PartMove("bottomNose",          "jaw.b"),
                    new PartMove("LeftWing",            "wing.l"),
                    new PartMove("RightWing.001",       "wing.r"),
                    new PartMove("Engine case Left.1",  "jetT.l"),
                    new PartMove("Engine case Right.1", "jetT.r"),
                    new PartMove("Engine case Left.2",  "jetm.l"),
                    new PartMove("Engine case Right.2", "jetm.r"),
                    new PartMove("Engine case Left.3",  "jetB.l"),
                    new PartMove("Engine case Right.3", "jetB.r"),
                },
                Jets = new[]
                {
                    new JetMount("Engine case Left.1",  "jetint.l", new Vector3(-0.4153f,  0.5418f, -2.2867f)),
                    new JetMount("Engine case Left.2",  "jetinm.l", new Vector3(-0.5752f,  0.2409f, -2.2824f)),
                    new JetMount("Engine case Left.3",  "jetinb.l", new Vector3(-0.5408f, -0.0975f, -2.2777f)),
                    new JetMount("Engine case Right.1", "jetint.r", new Vector3( 0.4152f,  0.5420f, -2.2867f)),
                    new JetMount("Engine case Right.2", "jetinm.r", new Vector3( 0.5751f,  0.2411f, -2.2824f)),
                    new JetMount("Engine case Right.3", "jetinb.r", new Vector3( 0.5409f, -0.0973f, -2.2777f)),
                },
                RedundantMeshObjects = new[]
                {
                    "Engine Left.1", "Engine Left.2", "Engine Left.3",
                    "Engine Right.1", "Engine Right.2", "Engine Right.3",
                },
            },
        };

        int _selected;
        string _report = string.Empty;

        [MenuItem("FrogletTools/Vessels/Swap Vessel Rig", false, 21)]
        [FrogletTool(FrogletToolCategory.Vessels, Importance = 4,
            Description = "Move a vessel onto its rigged, element-morphing model — re-homes every " +
                          "gameplay volume onto its bone and mounts the jets on the nozzles.")]
        public static void Open()
        {
            var w = GetWindow<VesselRigSwapper>("Rig Swapper");
            w.minSize = new Vector2(460f, 480f);
            w.Show();
        }

        void OnGUI()
        {
            FrogletEditorPalette.Banner("Vessel Rig Swapper",
                "Replaces a vessel's shape-less part-per-mesh art with its rigged, element-labeled model.",
                FrogletEditorPalette.ColorFor(FrogletToolCategory.Vessels));

            _selected = Mathf.Clamp(_selected, 0, Swaps.Length - 1);
            _selected = EditorGUILayout.Popup("Vessel", _selected,
                Swaps.Select(s => s.VesselPrefab).ToArray());

            EditorGUILayout.HelpBox(
                "Only the Dolphin is enabled. Its rig is the shipped hull in the SAME PLACE, so no " +
                "collider needs re-fitting. The Rhino's rig is offset 1.5545 in z with its wings " +
                "re-posed and the Urchin's is a uniform 2.105x scale — both need re-measuring " +
                "before they can be added here (Docs/VESSEL_CONSTRUCTION.md §4.4).",
                MessageType.Info);

            EditorGUILayout.Space(6f);
            if (GUILayout.Button("Dry run (report only)", GUILayout.Height(24f)))
                _report = Run(Swaps[_selected], false);

            if (FrogletEditorPalette.ColorButton("Perform the swap", FrogletEditorPalette.Warn, 200f, 28f))
            {
                if (EditorUtility.DisplayDialog("Swap vessel rig",
                        $"Rewrite {Swaps[_selected].VesselPrefab}.prefab onto " +
                        $"{Swaps[_selected].RigFbx}?\n\nThe prefab is saved on success. Git is the undo.",
                        "Swap it", "Cancel"))
                    _report = Run(Swaps[_selected], true);
            }

            EditorGUILayout.Space(8f);
            if (!string.IsNullOrEmpty(_report))
                EditorGUILayout.TextArea(_report, GUILayout.ExpandHeight(true));

            FrogletToolShipPanel.Draw(Ship, this);
        }

        static string Run(RigSwap swap, bool write)
        {
            var log = new StringBuilder();
            log.AppendLine($"=== {swap.VesselPrefab}: {swap.LegacyModelRoot} → {swap.RigFbx} ===");
            log.AppendLine(write ? "MODE: WRITE" : "MODE: dry run (nothing is saved)");
            log.AppendLine();

            string prefabPath = $"{VesselFolder}/{swap.VesselPrefab}.prefab";
            string fbxPath = $"{ModelFolder}/{swap.RigFbx}.fbx";

            var rigAsset = AssetDatabase.LoadAssetAtPath<GameObject>(fbxPath);
            if (!rigAsset) return log.AppendLine($"! rig model not found at {fbxPath}").ToString();

            GameObject contents = PrefabUtility.LoadPrefabContents(prefabPath);
            try
            {
                var problems = new List<string>();

                // ── idempotency ──────────────────────────────────────────────────
                if (Find(contents.transform, swap.RigFbx))
                {
                    log.AppendLine($"Already swapped — '{swap.RigFbx}' is present under the vessel. " +
                                   "Nothing to do.");
                    return log.ToString();
                }

                var handle = Find(contents.transform, OrientationHandleName);
                if (!handle) problems.Add($"no '{OrientationHandleName}' under the vessel root");

                var legacyRoot = Find(contents.transform, swap.LegacyModelRoot);
                if (!legacyRoot) problems.Add($"legacy model root '{swap.LegacyModelRoot}' not found");

                // ── instantiate the rig, so its bones exist to validate against ──
                GameObject rig = (GameObject)PrefabUtility.InstantiatePrefab(rigAsset, handle ? handle : contents.transform);
                rig.name = swap.RigFbx;
                rig.transform.localPosition = Vector3.zero;
                rig.transform.localRotation = Quaternion.identity;
                rig.transform.localScale = Vector3.one;

                var bones = rig.GetComponentsInChildren<Transform>(true)
                    .GroupBy(t => t.name)
                    .ToDictionary(g => g.Key, g => g.First(), System.StringComparer.OrdinalIgnoreCase);

                foreach (var p in swap.Parts)
                {
                    if (!bones.ContainsKey(p.Bone)) problems.Add($"bone '{p.Bone}' is not on this rig");
                    if (!Find(contents.transform, p.LegacyObject)) problems.Add($"legacy object '{p.LegacyObject}' not found");
                }
                foreach (var j in swap.Jets)
                    if (!bones.ContainsKey(j.Bone)) problems.Add($"jet bone '{j.Bone}' is not on this rig");

                var rigSkin = rig.GetComponentsInChildren<SkinnedMeshRenderer>(true).FirstOrDefault();
                if (!rigSkin) problems.Add("the rig has no SkinnedMeshRenderer");

                var shapes = new List<VesselAnimation.ElementShapeTarget>();
                VesselAnimation.CollectElementShapes(rig.transform, shapes);
                int elements = shapes.Select(s => s.Element).Distinct().Count();
                log.AppendLine($"rig: {bones.Count} bones, {elements} element shape(s) discovered");
                if (elements < 4)
                    log.AppendLine("  NOTE: fewer than four element shapes — the hull will not morph on every element.");

                if (problems.Count > 0)
                {
                    log.AppendLine();
                    log.AppendLine("REFUSED — nothing written:");
                    foreach (var pr in problems) log.AppendLine("  ! " + pr);
                    return log.ToString();
                }

                // ── 1. re-home each gameplay volume onto its bone, world pose kept ─
                log.AppendLine();
                log.AppendLine("gameplay volumes → bones (world pose preserved; no collider is re-fitted):");
                foreach (var p in swap.Parts)
                {
                    var part = Find(contents.transform, p.LegacyObject);
                    var bone = bones[p.Bone];
                    string carried = string.Join(", ", part.GetComponents<Component>()
                        .Where(c => c && !(c is Transform) && !(c is MeshFilter) && !(c is MeshRenderer))
                        .Select(c => c.GetType().Name));
                    log.AppendLine($"  {p.LegacyObject,-20} → {p.Bone,-10} [{carried}]");

                    if (!write) continue;
                    if (part == legacyRoot)
                    {
                        // The legacy ROOT hosts the vessel's own body volume and cannot be moved
                        // without taking its whole subtree; strip its art and leave it in place.
                        StripArt(part.gameObject);
                        continue;
                    }
                    part.SetParent(bone, true);
                    StripArt(part.gameObject);
                }

                // ── 2. the sub-pixel inner meshes the rig now draws for real ──────
                log.AppendLine();
                log.AppendLine("redundant art the rig draws itself (removed):");
                foreach (var name in swap.RedundantMeshObjects)
                {
                    var obj = Find(contents.transform, name);
                    if (!obj) { log.AppendLine($"  {name,-20} (absent already)"); continue; }
                    log.AppendLine($"  {name,-20} localScale {obj.localScale.x:0.####}");
                    if (write) DestroyImmediate(obj.gameObject);
                }

                // ── 3. the jets, onto the nozzle bones at their measured mouths ───
                log.AppendLine();
                log.AppendLine("jets → nozzle bones (mountBone resolves by NAME at Awake):");
                foreach (var j in swap.Jets)
                {
                    var parent = Find(contents.transform, j.LegacyParent);
                    var jet = parent ? parent.GetComponentInChildren<VesselJet>(true) : null;
                    if (!jet) { log.AppendLine($"  ! no VesselJet under '{j.LegacyParent}'"); continue; }
                    log.AppendLine($"  {jet.name,-26} → {j.Bone,-10} mouth {j.MouthInVesselSpace}");
                    if (!write) continue;

                    var so = new SerializedObject(jet);
                    var prop = so.FindProperty("mountBone");
                    if (prop != null) { prop.stringValue = j.Bone; so.ApplyModifiedPropertiesWithoutUndo(); }

                    // Park it at the measured mouth in VESSEL space, then let SetParent keep that
                    // world pose — mountBone re-parents at runtime and preserves the local offset.
                    jet.transform.SetParent(bones[j.Bone], true);
                    jet.transform.position = contents.transform.TransformPoint(j.MouthInVesselSpace);
                }

                // ── 4. hand the hull's domain painting to the rig ────────────────
                var customization = contents.GetComponent<VesselCustomization>();
                if (customization)
                {
                    log.AppendLine();
                    log.AppendLine($"VesselCustomization geometry → '{rigSkin.name}' " +
                                   $"({rigSkin.sharedMaterials.Length} material slot(s))");
                    if (write)
                    {
                        var so = new SerializedObject(customization);
                        var list = so.FindProperty("_shipGeometries");
                        if (list != null && list.isArray)
                        {
                            list.ClearArray();
                            list.InsertArrayElementAtIndex(0);
                            list.GetArrayElementAtIndex(0).objectReferenceValue = rigSkin.gameObject;
                            so.ApplyModifiedPropertiesWithoutUndo();
                        }
                        else log.AppendLine("  ! could not find the ship-geometry list — wire it by hand");
                    }
                }

                // ── 5. the animation binds to bones BY NAME, so clear its fields ──
                var animation = contents.GetComponent<VesselAnimation>();
                if (animation)
                {
                    log.AppendLine();
                    log.AppendLine("VesselAnimation: clearing authored part fields so they re-resolve to bones, " +
                                   $"and pointing SkinnedMeshRenderer at '{rigSkin.name}'.");
                    if (write)
                    {
                        var so = new SerializedObject(animation);
                        var it = so.GetIterator();
                        bool enter = true;
                        while (it.NextVisible(enter))
                        {
                            enter = false;
                            if (it.propertyType != SerializedPropertyType.ObjectReference) continue;
                            if (it.objectReferenceValue is Transform) it.objectReferenceValue = null;
                        }
                        var smr = so.FindProperty("SkinnedMeshRenderer");
                        if (smr != null) smr.objectReferenceValue = rigSkin;
                        so.ApplyModifiedPropertiesWithoutUndo();
                    }
                }

                if (write)
                {
                    PrefabUtility.SaveAsPrefabAsset(contents, prefabPath);
                    FrogletToolChangeLedger.Record(ToolName, prefabPath);
                    log.AppendLine();
                    log.AppendLine($"WROTE {prefabPath}");
                    log.AppendLine("Next: FrogletTools ▸ Vessels ▸ Audit Vessel Elemental Morphs (expect 4 " +
                                   "shapes with NON-ZERO magnitude), then Audit Vessel Tails and Jets, then fly it.");
                }
                else
                {
                    log.AppendLine();
                    log.AppendLine("Dry run complete — every mapped bone and object resolved.");
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }
            return log.ToString();
        }

        /// <summary>
        /// Remove a legacy part's ART but keep its gameplay. A part carries its MeshRenderer
        /// alongside its collider, so re-parenting one onto a bone without stripping the renderer
        /// welds the old hull to the new skeleton (VesselRigSwapPlanner's standing warning).
        /// </summary>
        static void StripArt(GameObject go)
        {
            var mr = go.GetComponent<MeshRenderer>();
            if (mr) DestroyImmediate(mr);
            var mf = go.GetComponent<MeshFilter>();
            if (mf) DestroyImmediate(mf);
        }

        static Transform Find(Transform root, string name) =>
            root.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == name);
    }
}
