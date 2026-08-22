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

        const string ToolName = "Audit Vessel Skimmers";

        [MenuItem("FrogletTools/Vessels/Audit Vessel Skimmers")]
        [FrogletTool(FrogletToolCategory.Vessels, Importance = 4,
            Description = "Checks every vessel's near/far skimmer reference actually reaches the " +
                          "object doing the physics — an unreachable or inactive skimmer skims " +
                          "nothing, silently.",
            DocPath = "Assets/_Scripts/Controller/Vessel/R_VesselActions/DOLPHIN_ENERGY_ECONOMY.md")]
        static void Audit()
        {
            var report = new StringBuilder();
            report.AppendLine("Vessel skimmer audit — VesselStatus.NearFieldSkimmer / FarFieldSkimmer");
            report.AppendLine("(VesselController initializes ONLY these; SkimmerImpactor drops every");
            report.AppendLine(" contact until the skimmer it points at is initialized.)");
            report.AppendLine();

            int faults = 0;
            var findings = new List<(string title, string notes)>();
            foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { VesselPrefabFolder }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var root = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (!root || !root.TryGetComponent<VesselStatus>(out var status)) continue;

                report.AppendLine($"── {root.name}");
                faults += AuditSlot(report, root, status.NearFieldSkimmer, "NearFieldSkimmer", findings);
                faults += AuditSlot(report, root, status.FarFieldSkimmer, "FarFieldSkimmer", findings, optional: true);
                report.AppendLine();
            }

            report.AppendLine(faults == 0
                ? "No faults."
                : $"{faults} fault(s) — a vessel listed with *** below does not skim.");
            Debug.Log(report.ToString());

            // Bug Ledger integration (reference shape for auditors — Docs/DIAGNOSTICS.md):
            // findings dedupe by (tool, title), so a re-run refreshes issues instead of
            // duplicating them, and a FULL clean run auto-resolves the validating ones — the
            // auditor that filed a finding is the one authority on whether it is gone. A clean
            // run credits silently; filing new findings is the human's explicit call, because
            // the issue files are committable data.
            if (faults == 0)
            {
                BugLedger.ReportToolFindings(ToolName, findings);   // empty — resolves validated fixes
            }
            else if (EditorUtility.DisplayDialog("Vessel skimmer audit",
                         $"{faults} fault(s) found (details in the console report).\n\n" +
                         "File/refresh them in the Bug Ledger so they are tracked and " +
                         "auto-validated by the next clean audit run?",
                         "File in Bug Ledger", "Skip"))
            {
                BugLedger.ReportToolFindings(ToolName, findings);
                DiagnosticsWindow.OpenBugLedger();
            }
        }

        /// <returns>1 when this slot is a genuine fault, 0 otherwise.</returns>
        static int AuditSlot(StringBuilder report, GameObject root, Skimmer skimmer, string slot,
                             List<(string title, string notes)> findings, bool optional = false)
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
                else
                    AuditCrackle(problems, go, impactor.EffectContainer);
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

            var joined = string.Join("; ", problems);
            report.AppendLine($"   {slot}: '{go.name}' *** {joined}");
            findings.Add(($"{root.name}: {slot} does not skim",
                          $"'{go.name}' — {joined}\n\nReported by FrogletTools ▸ Vessels ▸ {ToolName}; " +
                          "a full clean re-run of the audit auto-resolves this issue."));
            return 1;
        }

        /// <summary>
        /// The forcefield crackle needs three pieces that live in different files — the effect in
        /// the container, a <see cref="ForcefieldCrackleController"/> on the impactor's own
        /// GameObject, and an overlay MeshRenderer for it to push the property block into. Miss any
        /// one and <c>SkimmerForcefieldCracklePrismEffectSO.Execute</c> just returns: no crackle, no
        /// error. Only checked when the container actually asks for it.
        /// </summary>
        static void AuditCrackle(List<string> problems, GameObject go,
                                 SkimmerImpactorDataContainerSO container)
        {
            bool wantsCrackle = false;
            foreach (var effect in container.SkimmerPrismEffects)
                if (effect is SkimmerForcefieldCracklePrismEffectSO) { wantsCrackle = true; break; }
            if (!wantsCrackle) return;

            if (!go.TryGetComponent<ForcefieldCrackleController>(out var crackle))
            {
                problems.Add("container asks for the forcefield crackle but the skimmer has no " +
                             "ForcefieldCrackleController");
                return;
            }

            var overlay = new SerializedObject(crackle).FindProperty("overlayRenderer");
            if (overlay is { objectReferenceValue: null })
                problems.Add("ForcefieldCrackleController has no overlayRenderer (crackle draws nothing)");
        }
    }
}
