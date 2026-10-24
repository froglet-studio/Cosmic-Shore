#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using CosmicShore.ECS;
using CosmicShore.Utility;
using NUnit.Framework;
using Unity.Mathematics;
using Unity.Rendering;
using UnityEngine;
using Family = CosmicShore.ECS.PrismMeshRendererStamps.Family;

namespace CosmicShore.Tests
{
    /// <summary>
    /// The gate for the no-compute MeshRenderer stamp path (Docs/PRISM_ANIMATION.md §4.11): a WebGL2 browser
    /// animates prisms only if the property-block twin writes EXACTLY the names, packing and values the entity
    /// stamps write, into properties the shader actually exposes per instance. Every one of those can drift
    /// silently — a renamed component, a property flipped off Hybrid Per Instance, a float3 written as a float —
    /// and the only symptom is a browser where something quietly stops moving. These run from assets and pure
    /// functions alone (the path itself is play-mode only).
    /// </summary>
    public class PrismMeshRendererStampsTests
    {
        const string BlockGraph = "Assets/_Graphics/Materials/Graphs/BlockGraph.shadergraph";
        const string ExplodingBlockGraph = "Assets/_Graphics/Materials/Graphs/ExplodingBlockGraph.shadergraph";
        const string SuctionGraph = "Assets/_Graphics/Materials/Graphs/PrismGraphs/SuctionGraph.shadergraph";

        /// <summary>Every [MaterialProperty] name the entity path writes, with whether its component is a vector.</summary>
        static Dictionary<string, bool> EntityProperties()
        {
            var map = new Dictionary<string, bool>();
            foreach (var t in typeof(PrismRenderService).Assembly.GetTypes())
            {
                if (t.Namespace != "CosmicShore.ECS" || !t.IsValueType) continue;
                var attr = t.GetCustomAttribute<MaterialPropertyAttribute>();
                if (attr == null) continue;
                var value = t.GetField("Value");
                if (value == null) continue;
                map[attr.Name] = value.FieldType != typeof(float);
            }
            return map;
        }

        static IEnumerable<(string Name, bool Vector)> EffectProperties() => new[]
        {
            (PrismMeshRendererStamps.BrightColorName, true),
            (PrismMeshRendererStamps.DarkColorName, true),
            (PrismMeshRendererStamps.ExplodeStartTimeName, false),
            (PrismMeshRendererStamps.ExplodeSpeedName, false),
            (PrismMeshRendererStamps.ExplodeDurationName, false),
            (PrismMeshRendererStamps.VelocityName, true),
            (PrismMeshRendererStamps.FacePivotFromCentroidName, false),
            (PrismMeshRendererStamps.ImplosionStateName, false),
        };

        [Test]
        public void EveryPropertyWritten_IsOneTheEntityPathWrites_WithTheSamePacking()
        {
            var entity = EntityProperties();
            Assert.Greater(entity.Count, 30, "Reflection found too few [MaterialProperty] components — this " +
                "test is measuring the wrong assembly.");

            foreach (var family in PrismMeshRendererStamps.FamilyProperties.Concat(new[] { EffectProperties().ToArray() }))
            foreach (var (name, vector) in family)
            {
                Assert.IsTrue(entity.TryGetValue(name, out bool entityVector),
                    $"{name} is written to the property block but no [MaterialProperty] component carries it — " +
                    "the two paths have drifted (PrismRenderProperties.cs is the source of truth).");
                Assert.AreEqual(entityVector, vector,
                    $"{name}: the entity component is {(entityVector ? "a vector" : "a float")} but the property " +
                    "block writes it as the other — the shader would read garbage on one of the two paths.");
            }
        }

        [Test]
        public void EveryFamily_CoversEveryComponentOfItsEntityStamp()
        {
            // The live-prism stamps the entity path writes, by the presence key each family uses. A property
            // the entity stamp writes and the block omits would sit at its material default in a browser.
            var expected = new Dictionary<Family, string[]>
            {
                [Family.Grow] = new[] { "_GrowStartTime", "_GrowRate", "_GrowStartFrac" },
                [Family.Color] = new[] { "_ColorStartTime", "_ColorDuration", "_StartBrightColor", "_StartDarkColor", "_StartSpread" },
                [Family.Flight] = new[] { "_FlightStartTime", "_FlightDuration", "_FlightVelocity" },
                [Family.ShieldMorph] = new[] { "_ShieldMorphStartTime", "_ShieldMorphDuration", "_ShieldMorphDirection", "_ShieldMorphOffset" },
                [Family.Jiggle] = new[] { "_JiggleStartTime", "_JiggleDuration", "_JiggleParams" },
                [Family.Suction] = new[] { "_SuctionStartTime", "_SuctionDuration", "_SuctionDirection", "_SuctionGrowDelay", "_Location" },
                [Family.Sway] = new[] { "_SwaySpanX", "_SwaySpanY", "_SwayAxis", "_SwayTiming" },
                [Family.SuperShieldMark] = new[] { "_PrismSuperShielded" },
            };
            Assert.AreEqual(expected.Count, PrismMeshRendererStamps.FamilyProperties.Length);
            foreach (var kv in expected)
                CollectionAssert.AreEquivalent(kv.Value,
                    PrismMeshRendererStamps.FamilyProperties[(int)kv.Key].Select(p => p.Name).ToArray(),
                    $"{kv.Key} family drifted from its entity stamp.");
        }

        /// <summary>
        /// A property block can only reach a property the variant declares in UnityPerMaterial — which, in a
        /// variant without DOTS_INSTANCING_ON (all a no-compute device has), is exactly an EXPOSED Hybrid Per
        /// Instance property. Checked per graph the property is drawn with.
        /// </summary>
        [Test]
        public void EveryPropertyWritten_IsExposedHybridPerInstance_InTheGraphsThatDrawIt()
        {
            var live = PrismMeshRendererStamps.FamilyProperties.SelectMany(f => f).Select(p => p.Name).ToArray();
            AssertPerInstance(BlockGraph, live);
            AssertPerInstance(ExplodingBlockGraph, live.Concat(new[]
            {
                PrismMeshRendererStamps.BrightColorName, PrismMeshRendererStamps.DarkColorName,
                PrismMeshRendererStamps.ExplodeStartTimeName, PrismMeshRendererStamps.ExplodeSpeedName,
                PrismMeshRendererStamps.ExplodeDurationName, PrismMeshRendererStamps.VelocityName,
                PrismMeshRendererStamps.FacePivotFromCentroidName,
            }));
            AssertPerInstance(SuctionGraph, new[]
            {
                PrismMeshRendererStamps.BrightColorName, PrismMeshRendererStamps.DarkColorName,
                PrismMeshRendererStamps.ImplosionStateName, PrismMeshRendererStamps.SuctionStartTimeName,
                PrismMeshRendererStamps.SuctionDurationName, PrismMeshRendererStamps.SuctionDirectionName,
                PrismMeshRendererStamps.SuctionGrowDelayName, PrismMeshRendererStamps.LocationName,
            });
        }

        static void AssertPerInstance(string graphPath, IEnumerable<string> props)
        {
            Assert.IsTrue(File.Exists(graphPath), $"{graphPath} is missing.");
            string text = File.ReadAllText(graphPath).Replace("\r\n", "\n");
            var blocks = text.Split(new[] { "\n\n" }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var prop in props)
            {
                var block = blocks.FirstOrDefault(b =>
                    (b.Contains($"\"m_DefaultReferenceName\": \"{prop}\"") ||
                     b.Contains($"\"m_OverrideReferenceName\": \"{prop}\"")) &&
                    b.Contains("ShaderProperty"));
                Assert.IsNotNull(block, $"{graphPath} does not declare {prop}.");
                Assert.IsTrue(block.Contains("\"m_GeneratePropertyBlock\": true"),
                    $"{graphPath}: {prop} is unexposed — a property block cannot reach it.");
                Assert.IsTrue(block.Contains("\"hlslDeclarationOverride\": 3"),
                    $"{graphPath}: {prop} is not Hybrid Per Instance.");
            }
        }

        // ── The pure value computation ───────────────────────────────────────────────────────────

        [Test]
        public void GrowClearDelay_IsWhenTheRemainderFallsUnderTheClearFraction()
        {
            const float rate = 2.5f;
            var fromZero = new float3(0f);
            float t = PrismMeshRendererStamps.GrowClearDelay(rate, in fromZero);
            Assert.AreEqual(Mathf.Log(1f / PrismMeshRendererStamps.GrowClearFraction) / rate, t, 1e-4f);
            // The remainder at the clear is exactly the tolerance — the snap is that small and no larger.
            Assert.AreEqual(PrismMeshRendererStamps.GrowClearFraction, Mathf.Exp(-rate * t), 1e-6f);

            // The largest axis governs (an anisotropic retarget, including a shrink above 1).
            var mixed = new float3(0.5f, 1f, 1.8f);
            Assert.AreEqual(Mathf.Log(0.8f / PrismMeshRendererStamps.GrowClearFraction) / rate,
                PrismMeshRendererStamps.GrowClearDelay(rate, in mixed), 1e-4f);

            var settled = new float3(1f);
            Assert.AreEqual(0f, PrismMeshRendererStamps.GrowClearDelay(rate, in settled));
            Assert.AreEqual(0f, PrismMeshRendererStamps.GrowClearDelay(0f, in fromZero),
                "A zero rate is unstamped — nothing to clear later.");
        }

        [Test]
        public void ExpandedBounds_MatchesTheEntityEnvelopeArithmetic()
        {
            var mesh = new Bounds(new Vector3(0.1f, 0f, -0.2f), new Vector3(1f, 2f, 3f));
            var disp = new float3(4f, -6f, 0f);
            var b = PrismMeshRendererStamps.ExpandedBounds(mesh, in disp, 0.5f);
            // ExpandBoundsForClockAnimation: centre += disp/2, extents += |disp/2| + padding.
            AssertNear(new Vector3(2.1f, -3f, -0.2f), b.center);
            AssertNear(new Vector3(0.5f + 2f + 0.5f, 1f + 3f + 0.5f, 1.5f + 0f + 0.5f), b.extents);
        }

        static void AssertNear(Vector3 expected, Vector3 actual)
        {
            Assert.AreEqual(expected.x, actual.x, 1e-5f);
            Assert.AreEqual(expected.y, actual.y, 1e-5f);
            Assert.AreEqual(expected.z, actual.z, 1e-5f);
        }

        [Test]
        public void PressuredDuration_IsFullUntilHalfTheCap_ThenEasesToTheFloor()
        {
            const float full = 7.5f;
            Assert.AreEqual(full, PrismMeshRendererDebris.PressuredDuration(full, 0, 96));
            Assert.AreEqual(full, PrismMeshRendererDebris.PressuredDuration(full, 48, 96));
            Assert.AreEqual(PrismMeshRendererDebris.MinPressuredDuration,
                PrismMeshRendererDebris.PressuredDuration(full, 96, 96), 1e-5f);
            float prev = full;
            for (int live = 48; live <= 96; live++)
            {
                float d = PrismMeshRendererDebris.PressuredDuration(full, live, 96);
                Assert.LessOrEqual(d, prev + 1e-5f, "Pressure must never lengthen a piece.");
                prev = d;
            }
            Assert.AreEqual(0.2f, PrismMeshRendererDebris.PressuredDuration(0.2f, 96, 96), 1e-6f,
                "A piece already shorter than the floor is never stretched up to it.");
        }

        // ── The block rebuild a clear performs ───────────────────────────────────────────────────

        [Test]
        public void ClearingOneFamily_KeepsTheOthersExactly_AndEmptiesWhenItWasTheLast()
        {
            var from = new MaterialPropertyBlock();
            var to = new MaterialPropertyBlock();

            from.SetFloat("_GrowStartTime", 12.5f);
            from.SetFloat("_GrowRate", 2f);
            from.SetVector("_GrowStartFrac", new Vector4(0f, 0f, 0f, 0f));
            from.SetVector("_SwaySpanX", new Vector4(0.1f, 0.2f, 0.3f, 0f));
            from.SetVector("_SwaySpanY", new Vector4(0.4f, 0.5f, 0.6f, 0f));
            from.SetVector("_SwayAxis", new Vector4(0f, 0f, 1f, 0f));
            from.SetVector("_SwayTiming", new Vector4(3f, 0.25f, 1.5f, 0f));
            from.SetFloat("_PrismSuperShielded", 1f);

            Assert.IsTrue(PrismMeshRendererStamps.RebuildWithout(from, to, Family.Grow));
            Assert.IsFalse(PrismMeshRendererStamps.IsPresent(to, Family.Grow), "The cleared family must go.");
            Assert.IsTrue(PrismMeshRendererStamps.IsPresent(to, Family.Sway));
            Assert.AreEqual(new Vector4(3f, 0.25f, 1.5f, 0f), to.GetVector("_SwayTiming"));
            Assert.AreEqual(1f, to.GetFloat("_PrismSuperShielded"), "A rebuild must keep the super-shield mark.");

            var only = new MaterialPropertyBlock();
            only.SetFloat("_FlightStartTime", 3f);
            only.SetFloat("_FlightDuration", 0.5f);
            only.SetVector("_FlightVelocity", new Vector4(1f, 2f, 3f, 0f));
            Assert.IsFalse(PrismMeshRendererStamps.RebuildWithout(only, to, Family.Flight),
                "Clearing the last family must report empty so the renderer gets a NULL block and rejoins " +
                "the SRP Batcher.");
            Assert.IsTrue(to.isEmpty);

            Assert.IsTrue(PrismMeshRendererStamps.RebuildKeeping(from, to, Family.SuperShieldMark),
                "Pool reuse keeps only the mark (its owner re-asserts it).");
            Assert.IsFalse(PrismMeshRendererStamps.IsPresent(to, Family.Sway));
        }
    }
}
#endif
