#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using CosmicShore.Gameplay;
using CosmicShore.ScriptableObjects;

namespace CosmicShore.Tests
{
    /// <summary>
    /// The automated gate for the SUPER-SHIELD DEFLECTION JIGGLE
    /// (Docs/PRISM_ANIMATION.md §5 C14).
    ///
    /// Why a test and not just the validator menu item: this effect's failure mode is exactly
    /// the state it was built to fix. If the graph wiring reverts, or a per-instance property
    /// exists on one side of the CPU/GPU contract but not the other, a super-shielded prism
    /// simply absorbs hits without moving — which is indistinguishable from the feature never
    /// having shipped, produces no error a screenshot would show, and reads to a player as the
    /// shot having missed. These assertions run from assets alone (no play mode).
    ///
    /// The CPU↔GPU count-match is the load-bearing one: a graph property with no component is
    /// a value stuck at its material default, and a component with no Hybrid-Per-Instance
    /// property is a silently ignored write. Both compile, both run, neither says anything.
    /// </summary>
    public class PrismSuperShieldJiggleTests
    {
        // The census is "every graph a LIVE prism can render with": super-shielded mass binds
        // the team block material (BlockGraph) or, when transparent, TransparentPrismMaterial,
        // which rests on ExplodingBlockGraph. SuctionGraph is excluded — it renders mass being
        // consumed, and consumed mass is by definition not deflecting a hit.
        static readonly string[] WiredGraphPaths =
        {
            "Assets/_Graphics/Materials/Graphs/BlockGraph.shadergraph",
            "Assets/_Graphics/Materials/Graphs/ExplodingBlockGraph.shadergraph",
        };

        const string HlslPath = "Assets/_Graphics/Materials/Graphs/PrismClockAnimation.hlsl";
        const string RenderServicePath =
            "Assets/_Scripts/Controller/ECS/Rendering/PrismRenderService.cs";
        const string FunctionName = "PrismJiggleClock";
        const string WiringTool = "Tools/Shaders/wire_prism_jiggle_clock.py";

        static readonly string[] PerInstanceProps =
        {
            "_JiggleStartTime", "_JiggleDuration", "_JiggleParams",
        };

        [Test]
        public void ClockHlsl_DeclaresTheJiggleFunction()
        {
            Assert.IsTrue(File.Exists(HlslPath), $"{HlslPath} is missing — the jiggle has no GPU half.");
            string hlsl = File.ReadAllText(HlslPath);
            Assert.IsTrue(hlsl.Contains($"void {FunctionName}_float("),
                $"{HlslPath} does not declare {FunctionName}_float — ShaderGraph appends the precision " +
                "suffix, so the name must match exactly or every prism graph fails to compile.");
            Assert.IsTrue(hlsl.Contains("if (Duration <= 0.0)"),
                $"{FunctionName} lost its unstamped early-out. Duration 0 MUST return identity, or every " +
                "prism in the game that has never deflected anything starts rendering a wobble.");
        }

        [Test]
        public void EveryWiredGraph_DeclaresTheJigglePropertiesPerInstance()
        {
            foreach (var graphPath in WiredGraphPaths)
            {
                Assert.IsTrue(File.Exists(graphPath), $"{graphPath} is missing.");
                // Normalise CRLF first: a Windows checkout otherwise collapses the whole file
                // into one block and every block-scoped check reads the wrong property.
                string text = File.ReadAllText(graphPath).Replace("\r\n", "\n");
                var blocks = text.Split(new[] { "\n\n" }, StringSplitOptions.RemoveEmptyEntries);

                foreach (var prop in PerInstanceProps)
                {
                    var block = blocks.FirstOrDefault(b =>
                        (b.Contains($"\"m_DefaultReferenceName\": \"{prop}\"") ||
                         b.Contains($"\"m_OverrideReferenceName\": \"{prop}\"")) &&
                        b.Contains("ShaderProperty"));
                    Assert.IsNotNull(block,
                        $"{graphPath} does not declare {prop} — run {WiringTool}.");
                    // The mirror of the corridor's globals rule: these are PER-PRISM initial
                    // conditions, so they must be exposed AND Hybrid Per Instance or DOTS
                    // cannot write them per prism.
                    Assert.IsTrue(block.Contains("\"m_GeneratePropertyBlock\": true"),
                        $"{graphPath}: {prop} is UNEXPOSED — a per-instance stamp cannot reach it.");
                    Assert.IsTrue(block.Contains("\"hlslDeclarationOverride\": 3"),
                        $"{graphPath}: {prop} is not Hybrid Per Instance — every prism would render the " +
                        "material default and the stamp would go nowhere.");
                }

                Assert.IsTrue(text.Contains($"\"m_FunctionName\": \"{FunctionName}\""),
                    $"{graphPath} has no {FunctionName} Custom Function node — prisms on it can never " +
                    $"deflect visibly. Run {WiringTool}.");
            }
        }

        static readonly string[] ComponentTypeNames =
        {
            "PrismJiggleStartTimeOverride", "PrismJiggleDurationOverride", "PrismJiggleParamsOverride",
        };

        [Test]
        public void GraphProperties_AndEntityComponents_MatchOneForOne()
        {
            // The component types must EXIST and be IComponentData — proved by reflection, which
            // is what makes this stronger than a text scan: a type that failed to compile, or one
            // that quietly stopped being a component, fails here.
            //
            // The attribute's own member shape is deliberately NOT inspected. Whether Entities
            // Graphics exposes MaterialProperty.Name as a field or a property is the package's
            // business and has changed across versions; the thing under test is OUR contract, and
            // binding the gate to their internals would make it fail on a package bump.
            var assembly = typeof(CosmicShore.ECS.PrismRenderService).Assembly;
            foreach (var typeName in ComponentTypeNames)
            {
                var type = assembly.GetTypes().FirstOrDefault(t => t.Name == typeName);
                Assert.IsNotNull(type, $"{typeName} does not exist — StampJiggle cannot compile.");
                Assert.IsTrue(typeof(Unity.Entities.IComponentData).IsAssignableFrom(type),
                    $"{typeName} is not an IComponentData — it can never reach the GPU.");
                Assert.IsTrue(type.GetCustomAttributes(false)
                        .Any(a => a.GetType().Name == "MaterialProperty"),
                    $"{typeName} has lost its [MaterialProperty] attribute — Entities Graphics will " +
                    "not upload it, so the stamp writes into nothing and the deflection is silent.");
            }

            // The declared reference names must match the graph one-for-one. A graph property with
            // no component is stuck at its material default; a component with no Hybrid-Per-Instance
            // property is a silently ignored write. Neither errors at runtime, which is why this is
            // count-matched rather than eyeballed.
            const string propsPath = "Assets/_Scripts/Controller/ECS/Rendering/PrismRenderProperties.cs";
            Assert.IsTrue(File.Exists(propsPath), $"{propsPath} is missing.");
            string propsSrc = File.ReadAllText(propsPath);
            var declared = new HashSet<string>();
            foreach (Match m in Regex.Matches(propsSrc, @"\[MaterialProperty\(""(_\w+)""\)\]"))
                if (m.Groups[1].Value.StartsWith("_Jiggle", StringComparison.Ordinal))
                    declared.Add(m.Groups[1].Value);

            CollectionAssert.AreEquivalent(PerInstanceProps, declared.ToArray(),
                "The jiggle's CPU and GPU halves have drifted: the [MaterialProperty] reference names " +
                $"in {propsPath} do not match the Hybrid-Per-Instance properties on the prism graphs.");

            // ...and the components must be added on the PROTOTYPE. Adding one to a live entity is
            // a per-prism archetype move — the exact cost the prototype pattern exists to kill —
            // so a stamp of a component the prototype never added simply returns false forever.
            Assert.IsTrue(File.Exists(RenderServicePath), $"{RenderServicePath} is missing.");
            string service = File.ReadAllText(RenderServicePath);
            foreach (var t in ComponentTypeNames)
            {
                Assert.IsTrue(service.Contains($"em.AddComponentData(prototype, new {t}"),
                    $"{t} is not added on the Prism prototype in PrismRenderService.GetPrototype — " +
                    "StampJiggle's HasComponent probe will fail for every prism, forever.");
            }
        }

        [Test]
        public void ClearPrismStamps_AlsoClearsTheJiggle()
        {
            // Pool hygiene: a prism pulled from the pool must not inherit the previous life's
            // deflection. Initialize routes through ClearPrismStamps, so the jiggle has to be in it.
            string service = File.ReadAllText(RenderServicePath);
            int idx = service.IndexOf("public static void ClearPrismStamps", StringComparison.Ordinal);
            Assert.Greater(idx, 0, "ClearPrismStamps not found in PrismRenderService.");
            string body = service.Substring(idx, Math.Min(400, service.Length - idx));
            Assert.IsTrue(body.Contains("ClearJiggleStamp"),
                "ClearPrismStamps does not clear the jiggle — a pooled prism can inherit the " +
                "previous life's wobble.");
        }

        [Test]
        public void PeakTilt_IsClampedAndMonotone()
        {
            var config = ScriptableObject.CreateInstance<PrismSuperShieldJiggleConfigSO>();
            try
            {
                float floorTilt = config.PeakTiltRadians(0f);
                float ceilTilt = config.PeakTiltRadians(float.MaxValue);

                Assert.Greater(floorTilt, 0f,
                    "A hit that cannot describe its own magnitude (Consume, an unattributed blast) " +
                    "must still deflect visibly — otherwise those hits stay silent, which is the bug.");
                Assert.LessOrEqual(ceilTilt, config.MaxTiltRadians + 1e-6f,
                    "The tilt must be CAPPED — an unbounded amplitude turns a track lining inside out " +
                    "when a big blast lands.");

                float previous = -1f;
                for (int i = 0; i <= 20; i++)
                {
                    float tilt = config.PeakTiltRadians(i * 40f);
                    Assert.GreaterOrEqual(tilt, previous - 1e-6f,
                        "Peak tilt must be monotone in impact speed — a harder hit may not wobble less.");
                    previous = tilt;
                }
            }
            finally { UnityEngine.Object.DestroyImmediate(config); }
        }

        // Peak displacement as a multiple of (radius x tilt), measured against the SHIPPED
        // PrismJiggleClock_float by compiling it with clang and sweeping the whole animation
        // window over the stella's 24 face normals (/asset-surgery §4.5c). The first cut of the
        // envelope was sized off the uniform row alone and under-covered a (1,1,20) trail slab
        // by 16x — the prism frustum-culls away mid-wobble. These are the numbers the padding
        // has to beat, so they are pinned here rather than left in a comment.
        static readonly (Vector3 scale, float measured)[] MeasuredDisplacement =
        {
            (new Vector3(1f, 1f, 1f), 0.982f),
            (new Vector3(3f, 3f, 10f), 2.728f),
            (new Vector3(12f, 2f, 2f), 4.643f),
            (new Vector3(1f, 1f, 20f), 15.972f),
        };

        [Test]
        public void CullingPadding_CoversTheMeasuredDisplacement_IncludingAnisotropicPrisms()
        {
            const float radius = 1.5f;   // a stella tip, in object units
            const float tilt = 0.0785f;  // ~4.5 degrees

            foreach (var (scale, measured) in MeasuredDisplacement)
            {
                float padding = PrismSuperShieldJiggle.CullingPadding(radius, tilt, scale);
                float required = radius * tilt * measured;
                Assert.GreaterOrEqual(padding, required,
                    $"Culling envelope under-covers the wobble at lossyScale {scale}: padding " +
                    $"{padding} < measured displacement {required}. A prism whose bounds do not " +
                    "contain its own animation frustum-culls away mid-wobble at the screen edge. " +
                    "The shader rotates in the world-proportioned frame and maps back through " +
                    "1/scale, so the padding MUST carry the scale ratio.");
            }
        }

        [Test]
        public void CullingPadding_SurvivesADegenerateScale()
        {
            // A prism mid-birth sits at localScale zero; the padding must not divide by it.
            float padding = PrismSuperShieldJiggle.CullingPadding(1.5f, 0.0785f, Vector3.zero);
            Assert.IsTrue(float.IsFinite(padding) && padding >= 0f,
                $"CullingPadding returned {padding} at zero scale — a NaN or infinite extent " +
                "poisons the entity's RenderBounds and can drop the prism from culling entirely.");
        }

        [Test]
        public void PackedParams_CarryTiltAndBothRatesInRadians()
        {
            var config = ScriptableObject.CreateInstance<PrismSuperShieldJiggleConfigSO>();
            try
            {
                Vector3 packed = config.PackParams(60f);
                Assert.AreEqual(config.PeakTiltRadians(60f), packed.x, 1e-6f,
                    "_JiggleParams.x must be the peak tilt the shader reads as `amplitude`.");
                Assert.Greater(packed.y, 0f, "_JiggleParams.y (precession rate) must be positive.");
                Assert.Greater(packed.z, 0f, "_JiggleParams.z (nutation rate) must be positive.");
                // Non-commensurate on purpose: equal or harmonically-related rates make the wobble
                // repeat inside one deflection, which reads as a mechanical buzz rather than a jiggle.
                float ratio = packed.z / packed.y;
                Assert.Greater(Mathf.Abs(ratio - Mathf.Round(ratio)), 0.05f,
                    "The nutation and precession rates are a near-integer ratio, so the wobble repeats " +
                    "inside one deflection. Keep them deliberately non-commensurate.");
            }
            finally { UnityEngine.Object.DestroyImmediate(config); }
        }
    }
}
#endif
