using System.Collections.Generic;
using UnityEngine;

namespace CosmicShore.Utility
{
    /// <summary>
    /// Fail-loud diagnostics for the clock-material law's STRICT MODE
    /// (Docs/PRISM_ANIMATION.md — no legacy fallback, locked 2026-08-01).
    /// A prism whose material/graph is not wired for the clock properties does
    /// NOT fall back to a CPU animation: gameplay state still goes final at the
    /// stamp (law-correct), the visual snaps to its end state, and these
    /// helpers scream ONCE per offender naming exactly what to wire (§4.4).
    /// Per the project's fail-loud policy: an obvious error beats a silent
    /// fallback that hides the missing wiring forever.
    /// </summary>
    public static class PrismClockDiagnostics
    {
        static readonly HashSet<int> _warnedMaterials = new();
        static readonly HashSet<string> _warnedContexts = new();

        // Warn-once means once per PLAY SESSION: a strict-mode fail-loud that only fires once
        // per editor session hides regressions, and material instance IDs get reused across
        // sessions, falsely suppressing new offenders.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetWarnings()
        {
            _warnedMaterials.Clear();
            _warnedContexts.Clear();
        }

        // Edit-mode tooling (baseline measurers, track previews, prefab spawns)
        // legitimately runs the prism lifecycle without the instanced service or
        // a play-mode clock — strict-mode screaming is a RUNTIME contract. The
        // visual still settles immediately either way.
        static bool Muted => !Application.isPlaying;

        /// <summary>The bound material's graph lacks a clock property — visuals
        /// snap until it is wired. Logged once per material asset.</summary>
        public static void WarnUnwiredMaterial(Material material, string propertyName, Object context)
        {
            if (Muted || material == null || !_warnedMaterials.Add(material.GetInstanceID())) return;
            Debug.LogError(
                $"[PrismClock] STRICT MODE: material '{material.name}' does not declare '{propertyName}' — " +
                $"its ShaderGraph is not wired for clock-material animation, so this visual SNAPS to its end state " +
                $"(no legacy fallback exists). Wire the graph per Docs/PRISM_ANIMATION.md §4.4 " +
                $"(add the property with this exact reference name, Hybrid Per Instance, + the PrismClockAnimation.hlsl node).",
                context);
        }

        /// <summary>No usable companion render entity to stamp — the clock path
        /// REQUIRES the instanced renderer. Logged once per reason key. Pass a
        /// detail line (e.g. PrismRenderService.DescribeGrowStampTarget) so the
        /// error names the exact broken gate instead of a generic pointer.</summary>
        public static void WarnNoRenderEntity(string reasonKey, Object context, string detail = null)
        {
            if (Muted || string.IsNullOrEmpty(reasonKey) || !_warnedContexts.Add(reasonKey)) return;
            Debug.LogError(
                $"[PrismClock] STRICT MODE: no companion render entity to stamp ({reasonKey}) — " +
                $"clock-material animation rides the instanced render path (PrismRenderService), and no legacy " +
                $"fallback exists. " +
                (string.IsNullOrEmpty(detail) ? "Check PrismRenderService.StatusLine() / the PrismRenderConfig asset. "
                                              : $"DIAGNOSIS: {detail}. ") +
                $"Gameplay state is final (law-correct); the visual transition is skipped.",
                context);
        }
    }
}
