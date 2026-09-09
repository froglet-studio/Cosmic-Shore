using System.Collections.Generic;
using System.Text;
using CosmicShore.Editor.Froglet;
using CosmicShore.Gameplay;
using UnityEditor;
using UnityEngine;

namespace CosmicShore.Editor
{
    /// <summary>
    /// <b>FrogletTools ▸ Vessels ▸ Audit Vessel Tails and Jets.</b>
    ///
    /// A tail and a set of jets are STANDARD, EXPECTED parts of a vessel
    /// (<c>Docs/VESSEL_TAIL_AND_JETS.md</c>) — not per-hull decoration. This is the gate that
    /// makes "expected" checkable, because every way of getting it wrong is silent:
    ///
    /// <list type="bullet">
    /// <item>A vessel with a tail but no <see cref="VesselTailAndJets"/> flies one domain's
    ///       colour and streaks another. Nothing errors; it just looks like somebody else's
    ///       ship at range.</item>
    /// <item>A vessel with no tail has no beacon at all, which reads as a balance problem
    ///       ("nobody can find me") rather than as missing FX.</item>
    /// <item>A vessel at width scale 1 that is nothing like the Dolphin's size has a ribbon
    ///       that either engulfs it or vanishes — a TrailRenderer's width is world-space, so
    ///       one authored number cannot serve a fleet spanning a 40x range.</item>
    /// <item>A jet whose PLUME was never sized flies whatever scale its mount happened to
    ///       inherit. <see cref="VesselJet"/>'s <c>widthScale</c> reaches only the ribbon
    ///       inside a jet — <c>VesselFXWidth</c> scales TrailRenderers and nothing
    ///       else — while the three particle systems that are most of what a jet DRAWS scale
    ///       off the transform hierarchy instead. So the two halves of one jet are sized by
    ///       two unrelated numbers, and only one of them is authored per vessel. The Urchin
    ///       shipped its plumes at 8.75x the reference girth and 40x its length that way,
    ///       purely because its engine nodes carry a 1.75 scale.</item>
    /// </list>
    ///
    /// Asset-only, no play mode. It reads the merged prefab hierarchy via
    /// <c>LoadAssetAtPath</c>, so a tail or jet nested inside another prefab is counted
    /// exactly as the runtime would see it.
    ///
    /// READER tool: reports only, writes nothing — no change ledger, no ship panel.
    /// </summary>
    public static class VesselTailAndJetAuditor
    {
        const string VesselFolder = "Assets/_Prefabs/Spacevessels";

        // The reference PLUME, and it is a measurement rather than a preference: the Dolphin and
        // the Squirrel are the two hulls whose jets were actually hand-tuned, and both author
        // exactly (0.6, 0.6, 0.13) on the jet instance — girth 0.6, length 0.13, a short tight
        // puff. Every other jet-bearing vessel authors nothing and gets (1,1,1) times whatever
        // its mount inherited, which is an accident, not a decision.
        static readonly Vector2 ReferencePlume = new(0.6f, 0.13f);   // (girth, length)

        // The camera the reference was tuned against (the Dolphin's |followOffset.z|). A plume's
        // apparent size is scale / camera distance, so a hull's target is the reference scaled by
        // its own camera ratio — the same derivation widthScale already uses for the ribbon.
        const float ReferenceCamera = 20f;

        // Vessels with no CameraSettingsSO of their own inherit the fleet's mid value; see
        // Docs/VESSEL_TAIL_AND_JETS.md §4.
        const float InheritedCamera = 30f;

        // A plume between half and double its target is a look call, not a defect. Outside that
        // band it was not considered.
        const float PlumeBand = 2f;

        [MenuItem("FrogletTools/Vessels/Audit Vessel Tails and Jets", false, 63)]
        [FrogletTool(FrogletToolCategory.Vessels, Importance = 4,
            Description = "Which vessels carry a tail, jets, and the domain-tint component — and " +
                          "which would fly one colour and streak another.")]
        public static void Run()
        {
            var report = new StringBuilder();
            report.AppendLine("— Vessel tails and jets (Docs/VESSEL_TAIL_AND_JETS.md):");
            report.AppendLine("   tail = beacon for OTHER players · jets = engine plumes for THIS pilot");
            report.AppendLine();

            var rows = new List<(string name, int tails, int jets, float width, bool tint, string verdict)>();
            var plumes = new List<(string name, float cam, bool ownCam, float girth, float length,
                                  float tgtGirth, float tgtLength, bool authored)>();

            foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { VesselFolder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var root = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (root == null) continue;

                // VesselController is the discriminator: it is the component whose Initialize
                // binds the tail/jet pass, so a sub-prefab a vessel is BUILT from (Skimmer,
                // VesselTail, VesselJet themselves) is correctly skipped rather than reported
                // as a broken vessel.
                if (root.GetComponent<VesselController>() == null) continue;

                var tails = root.GetComponentsInChildren<VesselTail>(true);
                var jets  = root.GetComponentsInChildren<VesselJet>(true);
                bool tint = root.GetComponentInChildren<VesselTailAndJets>(true) != null;

                // The authored per-vessel ribbon width. Reported because it is the one number a
                // hull cannot inherit: a TrailRenderer's width is world-space, so a vessel far
                // from the Dolphin's size and still sitting at 1 has almost certainly not been
                // considered rather than deliberately left alone.
                float width = 0f;
                foreach (var tail in tails)
                {
                    var prop = new SerializedObject(tail).FindProperty("widthScale");
                    if (prop != null) width = prop.floatValue;
                }

                string verdict;
                if (tails.Length == 0 && jets.Length == 0) verdict = "NO FX — no beacon, no thrust read";
                else if (!tint) verdict = "UNTINTED — flies its domain, streaks the prefab colour";
                else if (tails.Length == 0) verdict = "NO TAIL — other players have nothing to spot";
                else if (jets.Length == 0) verdict = "NO JETS — pilot gets no thrust read";
                else verdict = "ok";

                rows.Add((root.name, tails.Length, jets.Length, width, tint, verdict));

                if (jets.Length > 0)
                {
                    var cam = root.GetComponentInChildren<VesselCameraCustomizer>(true);
                    bool ownCam = cam != null && cam.Settings != null;
                    float camDist = ownCam ? Mathf.Abs(cam.Settings.followOffset.z) : InheritedCamera;

                    // Take the LARGEST plume on the hull: a vessel that sizes most of its jets and
                    // forgets one is still wrong, and the biggest is the one that reads.
                    float girth = 0f, length = 0f;
                    bool authored = false;
                    foreach (var jet in jets)
                    {
                        Vector3 scale = EffectiveJetScale(jet, root.transform);
                        girth  = Mathf.Max(girth,  Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.y)));
                        length = Mathf.Max(length, Mathf.Abs(scale.z));
                        if (!Mathf.Approximately(jet.transform.localScale.x, 1f) ||
                            !Mathf.Approximately(jet.transform.localScale.z, 1f))
                            authored = true;
                    }

                    float r = camDist / ReferenceCamera;
                    plumes.Add((root.name, camDist, ownCam, girth, length,
                                ReferencePlume.x * r, ReferencePlume.y * r, authored));
                }
            }

            rows.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
            report.AppendLine($"   {"vessel",-12} {"tails",5} {"jets",5} {"width",7}  {"tint",-5} verdict");
            foreach (var r in rows)
                report.AppendLine($"   {r.name,-12} {r.tails,5} {r.jets,5} {r.width,7:0.###}  {(r.tint ? "yes" : "NO"),-5} {r.verdict}");

            int ok = 0;
            foreach (var r in rows) if (r.verdict == "ok") ok++;
            report.AppendLine();
            report.AppendLine($"   {ok} of {rows.Count} vessels are on the standard.");
            report.AppendLine("   'width' is the vessel's authored ribbon width scale (camera distance / 20, the " +
                              "Dolphin being 1). Tails and jets are both drawn on every machine — a jet is TUNED " +
                              "for its own pilot, not hidden from anybody.");

            report.AppendLine();
            report.AppendLine("— Jet PLUME scale (the particle systems, which widthScale does NOT reach):");
            report.AppendLine($"   {"vessel",-12} {"cam",6} {"girth",6} {"len",7}  {"target g",8} {"target l",8}  {"g x",6} {"l x",7} verdict");

            plumes.Sort((a, b) => a.cam.CompareTo(b.cam));
            int sized = 0;
            foreach (var p in plumes)
            {
                float gx = p.tgtGirth  > 0f ? p.girth  / p.tgtGirth  : 0f;
                float lx = p.tgtLength > 0f ? p.length / p.tgtLength : 0f;
                bool inBand = gx <= PlumeBand && gx >= 1f / PlumeBand && lx <= PlumeBand && lx >= 1f / PlumeBand;
                if (inBand) sized++;

                string verdict = !p.authored
                    ? "UNSIZED — inherits its mount's scale"
                    : inBand ? "ok" : "off the band";
                report.AppendLine($"   {p.name,-12} {p.cam,6:0.##}{(p.ownCam ? " " : "*")}{p.girth,6:0.###} {p.length,7:0.####}  " +
                                  $"{p.tgtGirth,8:0.###} {p.tgtLength,8:0.####}  {gx,6:0.##} {lx,7:0.##} {verdict}");
            }

            report.AppendLine();
            report.AppendLine($"   {sized} of {plumes.Count} jet-bearing vessels are inside {1f / PlumeBand:0.##}x-{PlumeBand:0.##}x of their target plume. " +
                              "* = no CameraSettingsSO of its own, using the fleet's inherited 30.");
            report.AppendLine("   A plume is scaled by the TRANSFORM (its particle systems are Hierarchy-scaled), " +
                              "the ribbon by widthScale. They are two dials on one jet, so a jet mounted on a " +
                              "node that carries a scale draws a plume nobody chose — author m_LocalScale on the " +
                              "jet instance, as the Dolphin and Squirrel do. Docs/VESSEL_TAIL_AND_JETS.md §3.");

            Debug.Log(report.ToString());
        }

        /// <summary>
        /// The world scale a jet's particle systems will actually render at — which is NOT
        /// <c>jet.transform.lossyScale</c> whenever the jet declares a <c>mountBone</c>, because
        /// <see cref="VesselJet"/> re-parents onto that bone at Awake and keeps its authored local
        /// TRS. Resolving the bone by name here is the same lookup the runtime does, so the number
        /// reported is the number that ships rather than the number the prefab happens to store.
        /// </summary>
        static Vector3 EffectiveJetScale(VesselJet jet, Transform vesselRoot)
        {
            Vector3 local = jet.transform.localScale;

            var mount = new SerializedObject(jet).FindProperty("mountBone");
            string boneName = mount != null ? mount.stringValue : null;
            if (string.IsNullOrEmpty(boneName)) return jet.transform.lossyScale;

            Transform bone = FindDescendant(vesselRoot, boneName);
            if (bone == null) return jet.transform.lossyScale;   // runtime logs this; not our job

            Vector3 b = bone.lossyScale;
            return new Vector3(b.x * local.x, b.y * local.y, b.z * local.z);
        }

        static Transform FindDescendant(Transform root, string childName)
        {
            if (root.name == childName) return root;
            for (int i = 0; i < root.childCount; i++)
            {
                Transform hit = FindDescendant(root.GetChild(i), childName);
                if (hit != null) return hit;
            }
            return null;
        }
    }
}
