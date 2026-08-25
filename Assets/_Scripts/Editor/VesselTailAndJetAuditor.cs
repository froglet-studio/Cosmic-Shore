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
    ///       ship at range. Four of the eleven hulls shipped that way.</item>
    /// <item>A jet with no <see cref="VesselJet"/> marker is drawn on every screen instead of
    ///       its pilot's, and nothing distinguishes that from a deliberate Serpent-style
    ///       telegraph.</item>
    /// <item>A vessel with neither has no beacon at all, which reads as a balance problem
    ///       ("nobody can find me") rather than as missing FX.</item>
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

            var rows = new List<(string name, int tails, int jets, int shown, bool tint, string verdict)>();

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

                int shownToOthers = 0;
                foreach (var jet in jets)
                {
                    var so = new SerializedObject(jet);
                    var prop = so.FindProperty("visibleToOtherPilots");
                    if (prop != null && prop.boolValue) shownToOthers++;
                }

                string verdict;
                if (tails.Length == 0 && jets.Length == 0) verdict = "NO FX — no beacon, no thrust read";
                else if (!tint) verdict = "UNTINTED — flies its domain, streaks the prefab colour";
                else if (tails.Length == 0) verdict = "NO TAIL — other players have nothing to spot";
                else if (jets.Length == 0) verdict = "NO JETS — pilot gets no thrust read";
                else verdict = "ok";

                rows.Add((root.name, tails.Length, jets.Length, shownToOthers, tint, verdict));
            }

            rows.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
            report.AppendLine($"   {"vessel",-12} {"tails",5} {"jets",5} {"shown",5}  {"tint",-5} verdict");
            foreach (var r in rows)
                report.AppendLine($"   {r.name,-12} {r.tails,5} {r.jets,5} {r.shown,5}  {(r.tint ? "yes" : "NO"),-5} {r.verdict}");

            int ok = 0;
            foreach (var r in rows) if (r.verdict == "ok") ok++;
            report.AppendLine();
            report.AppendLine($"   {ok} of {rows.Count} vessels are on the standard.");
            report.AppendLine("   'shown' counts jets deliberately revealed to other pilots — the Serpent's case. " +
                              "Every other jet is drawn on its own pilot's screen only.");

            Debug.Log(report.ToString());
        }
    }
}
