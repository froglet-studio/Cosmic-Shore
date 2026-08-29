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

            Debug.Log(report.ToString());
        }
    }
}
