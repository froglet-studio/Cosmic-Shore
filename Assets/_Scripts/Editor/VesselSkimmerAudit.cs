using System.Collections.Generic;
using System.Text;
using CosmicShore.Editor.Froglet;
using CosmicShore.Gameplay;
using UnityEditor;
using UnityEngine;

namespace CosmicShore.Editor
{
    /// <summary>
    /// Audits every vessel prefab's skimmer wiring from ASSETS ALONE — no play mode.
    ///
    /// This exists because of a fault that is invisible in the inspector and silent at runtime:
    /// <see cref="VesselController"/> initializes ONLY the skimmers reachable through
    /// <c>VesselStatus.NearFieldSkimmer</c> / <c>FarFieldSkimmer</c>, and
    /// <see cref="SkimmerImpactor"/> drops every contact while
    /// <c>skimmer.IsInitialized</c> is false. So a vessel that carries a perfectly good skimmer —
    /// trigger sphere, rigidbody, effect container, all of it — but whose VesselStatus points at a
    /// DIFFERENT (or disabled) skimmer object skims nothing at all, with no error anywhere. The
    /// Dolphin shipped that way: an active EnergySkimmer doing the physics and a disabled legacy
    /// Skimmer holding the reference.
    ///
    /// Reports, per vessel: whether a skimmer is assigned, whether its GameObject (and every
    /// ancestor) is ACTIVE, whether it carries the SkimmerImpactor / ImpactCollider / trigger
    /// collider / Rigidbody the trigger path needs, and whether its effect container actually holds
    /// prism effects. Read-only — it never writes.
    /// </summary>
    public static class VesselSkimmerAudit
    {
        const string VesselPrefabFolder = "Assets/_Prefabs/Spacevessels";

        [MenuItem("FrogletTools/Vessels/Audit Vessel Skimmers")]
        [FrogletTool(FrogletToolCategory.Vessels, Importance = 4,
            Description = "Checks every vessel's near/far skimmer reference actually reaches the " +
                          "object doing the physics — an unreachable or inactive skimmer skims " +
                          "nothing, silently.")]
        static void Audit()
        {
            var report = new StringBuilder();
            report.AppendLine("Vessel skimmer audit — VesselStatus.NearFieldSkimmer / FarFieldSkimmer");
            report.AppendLine("(VesselController initializes ONLY these; SkimmerImpactor drops every");
            report.AppendLine(" contact until the skimmer it points at is initialized.)");
            report.AppendLine();

            int faults = 0;
            foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { VesselPrefabFolder }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var root = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (!root || !root.TryGetComponent<VesselStatus>(out var status)) continue;

                report.AppendLine($"── {root.name}");
                faults += AuditSlot(report, root, status.NearFieldSkimmer, "NearFieldSkimmer");
                faults += AuditSlot(report, root, status.FarFieldSkimmer, "FarFieldSkimmer", optional: true);
                report.AppendLine();
            }

            report.AppendLine(faults == 0
                ? "No faults."
                : $"{faults} fault(s) — a vessel listed with *** below does not skim.");
            Debug.Log(report.ToString());
        }

        /// <returns>1 when this slot is a genuine fault, 0 otherwise.</returns>
        static int AuditSlot(StringBuilder report, GameObject root, Skimmer skimmer, string slot,
                             bool optional = false)
        {
            if (!skimmer)
            {
                report.AppendLine(optional
                    ? $"   {slot}: (none)"
                    : $"   {slot}: (none) — this vessel has no skimmer at all");
                return 0;
            }

            var go = skimmer.gameObject;
            var problems = new List<string>();

            // Active in the PREFAB hierarchy: VesselController initializes the component, but an
            // inactive GameObject never receives a trigger callback, so the skim is still dead.
            for (var t = go.transform; t != null; t = t.parent)
                if (!t.gameObject.activeSelf)
                    problems.Add($"'{t.name}' is INACTIVE");

            if (!go.TryGetComponent<SkimmerImpactor>(out var impactor))
                problems.Add("no SkimmerImpactor");
            else
            {
                if (impactor.Skimmer != skimmer)
                    problems.Add("SkimmerImpactor.skimmer points at a different Skimmer");
                if (!impactor.EffectContainer)
                    problems.Add("SkimmerImpactor has no effect container");
                else if (impactor.EffectContainer.SkimmerPrismEffects is not { Length: > 0 })
                    problems.Add($"container '{impactor.EffectContainer.name}' has no prism effects");
            }

            if (!go.TryGetComponent<ImpactCollider>(out _))
                problems.Add("no ImpactCollider (the other side cannot resolve this impactor)");
            if (!go.TryGetComponent<Rigidbody>(out _))
                problems.Add("no Rigidbody (trigger callbacks need one on at least one side)");

            bool hasTrigger = false;
            foreach (var c in go.GetComponents<Collider>())
                if (c.isTrigger) { hasTrigger = true; break; }
            if (!hasTrigger) problems.Add("no trigger collider");

            // The reference resolving to an object OUTSIDE this prefab's own hierarchy is the exact
            // shape of the Dolphin fault, so call it out even when everything else checks out.
            if (!go.transform.IsChildOf(root.transform))
                problems.Add("is not part of this vessel's hierarchy");

            if (problems.Count == 0)
            {
                report.AppendLine($"   {slot}: '{go.name}' OK");
                return 0;
            }

            report.AppendLine($"   {slot}: '{go.name}' *** {string.Join("; ", problems)}");
            return 1;
        }
    }
}
