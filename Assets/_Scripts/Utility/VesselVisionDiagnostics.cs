using System.Collections.Generic;
using UnityEngine;

namespace CosmicShore.Utility
{
    /// <summary>
    /// Fail-loud enforcement for the VESSEL VISION BAND (Docs/VESSEL_VISION.md — a platform law,
    /// not a per-vessel or per-mode feature).
    ///
    /// The law's failure mode is SILENT, and that is the whole reason this file exists: a vessel
    /// that cannot wear the mark simply renders as a vessel. Nothing errors, nothing looks broken,
    /// and the aid is missing on exactly the ship somebody could not find. That is the
    /// opt-in-by-omission the platform laws were built to abolish, so — per the project's
    /// fail-loud policy, and mirroring <see cref="PrismOcclusionDiagnostics"/> — an unmarkable
    /// vessel screams ONCE, by name, naming the fix.
    ///
    /// Once per VESSEL for the lifetime of the process, not per frame and not per renderer: the
    /// stamp is re-asserted round-robin by the law's own publisher, so a per-call warning would
    /// repeat forever at a twelfth of the frame rate.
    /// </summary>
    public static class VesselVisionDiagnostics
    {
        static readonly HashSet<int> _warnedVessels = new();

        /// <summary>
        /// A vessel carries no renderer whose material can wear <c>_VesselVisionTint</c>.
        ///
        /// There are exactly two ways to get here, and the message names both because they need
        /// different fixes: the hull is painted with a material outside
        /// <c>VesselVisionShading.WiredShaderName</c> (an authoring problem — repaint it), or the
        /// wired graph has lost its splice (a source problem — re-run the wirer).
        /// </summary>
        public static void WarnUnmarkableVessel(Transform vessel)
        {
            if (vessel == null) return;
            if (!_warnedVessels.Add(vessel.GetInstanceID())) return;

            CSDebug.LogWarning(
                $"[VesselVisionShading] '{vessel.name}' has no renderer material exposing " +
                $"_VesselVisionTint, so it can never wear the distance vision mark and will be " +
                $"the one vessel other pilots cannot pick out at range. Either its hull is " +
                $"painted with a material outside {VesselVisionShading.WiredShaderName} " +
                $"(check VesselCustomization's domain material roles), or that graph has lost " +
                $"its splice — re-run 'python3 Tools/Shaders/wire_vessel_vision_shading.py' and " +
                $"audit with FrogletTools > Vessels > Validate Vessel Vision Band.",
                vessel);
        }

        /// <summary>Forget the warn set (editor tooling / test isolation).</summary>
        public static void Reset() => _warnedVessels.Clear();
    }
}
