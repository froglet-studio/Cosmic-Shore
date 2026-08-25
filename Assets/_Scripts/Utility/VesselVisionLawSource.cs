#if UNITY_EDITOR
using System;

namespace CosmicShore.Utility
{
    /// <summary>
    /// The predicates that decide whether the VESSEL VISION BAND platform law is still enforced by
    /// the SOURCE and the ASSETS (Docs/VESSEL_VISION.md § "The four layers").
    ///
    /// They live here — in the runtime assembly, editor-only — for one reason: the gates that need
    /// them must not be allowed to drift apart. The FrogletTools validator and the edit-mode test
    /// ask the SAME method, so an asset that passes the audit cannot fail the test and vice versa
    /// (the <see cref="SpeedTunnelLawSource"/> / <c>PrismOcclusionDiagnostics.IsCorridorCapable</c>
    /// pattern).
    ///
    /// Pure string analysis, no UnityEditor and no UnityEngine — the whole file is guarded rather
    /// than living under an Editor/ folder so the runtime tests can reach it
    /// (Docs/CONDITIONAL_COMPILATION.md pattern 2).
    /// </summary>
    public static class VesselVisionLawSource
    {
        /// <summary>The custom function the graph must call.</summary>
        public const string FunctionName = "VesselVisionShade";

        /// <summary>GUID of VesselVisionShading.hlsl — what the graph actually stores.</summary>
        public const string HlslGuid = "6862450db5b346df96c3355ca0543f93";

        /// <summary>The exposed per-vessel property the stamp writes and the shader gates on.</summary>
        public const string TintReferenceName = "_VesselVisionTint";

        /// <summary>The one stamp call the law is allowed to have.</summary>
        public const string StampCall = "VesselVisionShading.Stamp";

        /// <summary>
        /// What a census must actually search for: the call, WITH its opening parenthesis.
        ///
        /// Searching for <see cref="StampCall"/> alone finds this file — the declaration above is
        /// itself an occurrence of the string — so the gate would report two call sites and fail
        /// on a perfectly correct tree, forever. The general trap: <b>a source census that names
        /// its own needle counts itself</b>. Adding the parenthesis distinguishes an invocation
        /// from a mention, which is the distinction the rule was always about.
        ///
        /// It also does a second job that is easy to lose: it keeps the census off
        /// <see cref="DisplayStampInvocation"/>, whose name STARTS with the stamp's. Without the
        /// parenthesis every toy mini hull would be counted as a second owner of the vessel
        /// channel and the law would read as broken.
        /// </summary>
        public const string StampInvocation = StampCall + "(";

        /// <summary>
        /// The display-only sibling: a mini hull in a toy matrix, marked so the band shades it but
        /// deliberately NOT joining the heal roster (see
        /// <c>VesselVisionShading.StampDisplayModel</c>). Unlimited call sites — it takes ownership
        /// of nothing, so there is nothing for a second caller to fight over.
        /// </summary>
        public const string DisplayStampInvocation = "VesselVisionShading.StampDisplayModel(";

        /// <summary>The method that call must sit in.</summary>
        public const string StampHost = "SetShipProperties";

        /// <summary>
        /// The graph still calls the law: the custom function node is present, it points at the
        /// right HLSL asset, and the tint property is EXPOSED.
        ///
        /// Exposure is checked because it is the one property whose loss is completely silent: an
        /// unexposed ShaderGraph property is declared outside <c>UnityPerMaterial</c>, so no
        /// MaterialPropertyBlock can reach it and no material can be censused for it — the shader
        /// would compile, the graph would look wired, every vessel would read tint alpha 0, and
        /// the entire law would render as "off" with nothing to say so.
        /// </summary>
        public static bool GraphIsWired(string graphJson, out string reason)
        {
            if (string.IsNullOrEmpty(graphJson))
            {
                reason = "VesselGraph.shadergraph is empty or unreadable.";
                return false;
            }

            if (graphJson.IndexOf($"\"m_FunctionName\": \"{FunctionName}\"", StringComparison.Ordinal) < 0)
            {
                reason = $"VesselGraph carries no {FunctionName} custom function node — every vessel " +
                         "renders unmarked. Run: python3 Tools/Shaders/wire_vessel_vision_shading.py";
                return false;
            }

            if (graphJson.IndexOf(HlslGuid, StringComparison.Ordinal) < 0)
            {
                reason = $"VesselGraph's {FunctionName} node does not point at VesselVisionShading.hlsl " +
                         $"(guid {HlslGuid}) — the splice is present but sourced from the wrong file.";
                return false;
            }

            if (graphJson.IndexOf($"\"m_DefaultReferenceName\": \"{TintReferenceName}\"",
                                  StringComparison.Ordinal) < 0)
            {
                reason = $"VesselGraph declares no {TintReferenceName} property — the per-vessel " +
                         "domain colour has no channel to travel on.";
                return false;
            }

            if (!TintPropertyIsExposed(graphJson))
            {
                reason = $"{TintReferenceName} is not EXPOSED (m_GeneratePropertyBlock). An unexposed " +
                         "property lands outside UnityPerMaterial, so no MaterialPropertyBlock can " +
                         "reach it and every vessel silently reads tint alpha 0 — the law renders as " +
                         "off with nothing reporting it.";
                return false;
            }

            reason = null;
            return true;
        }

        /// <summary>
        /// True when the block of JSON declaring the tint property also sets
        /// <c>m_GeneratePropertyBlock</c>. Scoped to the window after the reference name rather
        /// than asked of the whole file, because every other property in the graph is exposed too
        /// and a whole-file substring would answer "yes" for all of them.
        /// </summary>
        static bool TintPropertyIsExposed(string graphJson)
        {
            int at = graphJson.IndexOf($"\"m_DefaultReferenceName\": \"{TintReferenceName}\"",
                                       StringComparison.Ordinal);
            if (at < 0) return false;

            // The property object continues for a few hundred characters past its reference name;
            // this window reaches the end of that object and no further.
            int end = Math.Min(graphJson.Length, at + 600);
            string window = graphJson.Substring(at, end - at);
            return window.IndexOf("\"m_GeneratePropertyBlock\": true", StringComparison.Ordinal) >= 0;
        }

        /// <summary>
        /// The shipped HLSL still declares the law's uniforms, its entry point, and the two
        /// expressions the C# transcription in <c>VesselVisionShadingConfigSO</c> mirrors.
        ///
        /// The band is checked as a SHAPE (a min of a rising and a falling edge) rather than by
        /// re-deriving it: the two are written the same way on purpose, and the failure this
        /// catches is somebody "simplifying" one side into a single smoothstep, which silently
        /// deletes either the near cutoff — marking the pilot's own hull — or the far one.
        /// </summary>
        public static bool HlslDeclaresLaw(string hlslText, out string reason)
        {
            if (string.IsNullOrEmpty(hlslText))
            {
                reason = "VesselVisionShading.hlsl is empty or unreadable.";
                return false;
            }

            foreach (var needed in new[]
                     {
                         "float4 _VesselVisionBand;",
                         "float4 _VesselVisionShape;",
                         "float4 _VesselVisionRim;",
                         $"void {FunctionName}_float",
                     })
            {
                if (hlslText.IndexOf(needed, StringComparison.Ordinal) < 0)
                {
                    reason = $"VesselVisionShading.hlsl no longer contains '{needed}'.";
                    return false;
                }
            }

            if (hlslText.IndexOf("min(rise, fall)", StringComparison.Ordinal) < 0)
            {
                reason = "the band is no longer a min() of a rising and a falling edge — one of the " +
                         "two cutoffs has been lost. The near one excludes the pilot's own hull and " +
                         "the far one stops a ship reading as a crystal; neither is optional.";
                return false;
            }

            if (hlslText.IndexOf("min(floor(ndv * steps), steps - 1.0)", StringComparison.Ordinal) < 0)
            {
                reason = "the cel quantizer has lost its min() guard — floor(ndv * steps) lands on " +
                         "`steps` itself at ndv == 1, pushing the brightest tone past 1 by " +
                         "1/(steps-1) with nothing in the config able to see it.";
                return false;
            }

            if (hlslText.IndexOf("lerp(BaseColor, cel, amount * mix)", StringComparison.Ordinal) < 0)
            {
                reason = "the centre break-up no longer modulates the BLEND AMOUNT. A dither applied " +
                         "to the cel colour instead drives the interior toward black and punches " +
                         "holes in the ship; applied to the blend it can only ever hand a fragment " +
                         "back to the hull's own shading.";
                return false;
            }

            if (hlslText.IndexOf("max(VesselVisionBreakup01(", StringComparison.Ordinal) < 0 ||
                hlslText.IndexOf(", rim01)", StringComparison.Ordinal) < 0)
            {
                reason = "the silhouette rim is no longer exempt from the centre break-up. The rim " +
                         "is the part of the mark that survives at range and the part a pilot " +
                         "actually reads; dithering it trades the aid for the decoration.";
                return false;
            }

            reason = null;
            return true;
        }

        /// <summary>
        /// The stamp has exactly ONE call site across the searched sources, and it sits inside
        /// <c>SetShipProperties</c>.
        ///
        /// This is the layer that makes the law un-authorable. A second stamp site is not a
        /// harmless duplicate: it is a second owner of the per-vessel channel, and the moment one
        /// exists a vessel's mark can be set from somewhere that does not know about a domain
        /// change — which is exactly the silent, per-vessel wrongness the platform-law shape
        /// exists to prevent.
        /// </summary>
        public static bool StampHasExactlyOneCallSite(string helperSource, int callSitesAcrossProject,
                                                      out string reason)
        {
            if (callSitesAcrossProject != 1)
            {
                reason = $"{StampCall} has {callSitesAcrossProject} call sites; the law allows exactly " +
                         "one, in VesselHelper.SetShipProperties. A second owner of the per-vessel " +
                         "channel can set a mark that does not follow a domain change.";
                return false;
            }

            if (string.IsNullOrEmpty(helperSource))
            {
                reason = "VesselHelper source is empty or unreadable.";
                return false;
            }

            int host = helperSource.IndexOf(StampHost, StringComparison.Ordinal);
            int call = helperSource.IndexOf(StampCall, StringComparison.Ordinal);
            if (host < 0 || call < 0 || call < host)
            {
                reason = $"{StampCall} is not inside VesselHelper.{StampHost} — it must sit in the one " +
                         "method every vessel's domain flows through, or a pilot who changes domain " +
                         "keeps the old colour.";
                return false;
            }

            reason = null;
            return true;
        }
    }
}
#endif
