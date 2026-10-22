#if UNITY_EDITOR
using CosmicShore.Gameplay;
using CosmicShore.ScriptableObjects;
using CosmicShore.Utility;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Tier = CosmicShore.Gameplay.SpindleLodSystem.Tier;

namespace CosmicShore.Tests
{
    /// <summary>
    /// The CPU half of the spindle LOD's "the swap is never seen" contract (Docs/SPINDLE_LOD.md).
    /// The GPU half - the picture each tier draws either side of a boundary - is proven by
    /// <c>Tools/Shaders/verify_spindle_lod.py</c>, which compiles the shipped SpindleLod.hlsl. What
    /// is proven here is that the CPU only ever swaps inside the bands where that harness shows the
    /// two tiers are identical, and that it does not flap at a boundary.
    /// </summary>
    public class SpindleLodTests
    {
        static SpindleLodProfileSO Profile()
        {
            var p = ScriptableObject.CreateInstance<SpindleLodProfileSO>();
            p.nearEndGirths = 100f;
            p.midEndGirths = 420f;
            p.hysteresis = 0.08f;
            p.convergeStartGirths = 110f;
            p.farFadeStartGirths = 1300f;
            p.farFadeEndGirths = 1900f;
            return p;
        }

        static SpindleLodSystem.TierBounds Covert(SpindleLodProfileSO p) => SpindleLodSystem.BoundsFor(p, false, default, default);

        // Resources/IncorporealDepth's far side: hold the peak to norm 2.25 (~474 u), settled back by 4.9 (~700 u).
        static readonly Vector4 Recede = new(2.25f, 4.9f, 0f, 0f);

        static SpindleLodSystem.TierBounds Overt(SpindleLodProfileSO p) => SpindleLodSystem.BoundsFor(p, true, Danger, Recede);

        // The theme's danger prism (DangerBlockMaterial): _Spread (0.09, 0.29, 0.8), _SqrDistance 100000.
        static readonly Vector4 Danger = new(0.09f, 0.29f, 0.8f, 100000f);

        // The prisms' own depth rule (SpreadSubGraph), per axis, beyond rest - transcribed independently.
        static float PrismOpening(float spread, float gain, float norm)
        {
            float far = spread * gain;
            float eff = norm > 1f ? far : Mathf.Lerp(-7f, far, norm);
            return Mathf.Max(eff, spread) - spread;
        }

        static float MaxPrismOpening(float norm) => Mathf.Max(PrismOpening(Danger.x, 50f, norm),
            Mathf.Max(PrismOpening(Danger.y, 35f, norm), PrismOpening(Danger.z, 20f, norm)));

        [Test]
        public void Outward_SwapsExactlyAtEachBoundary()
        {
            var p = Profile();
            Assert.AreEqual(Tier.Near, SpindleLodSystem.TierFor(99.9f, Tier.Near, Covert(p)));
            Assert.AreEqual(Tier.Mid, SpindleLodSystem.TierFor(100.1f, Tier.Near, Covert(p)));
            Assert.AreEqual(Tier.Mid, SpindleLodSystem.TierFor(419.9f, Tier.Mid, Covert(p)));
            // Covert has no Far / Low steps: past midEnd the converged braid hands straight to the thread.
            Assert.AreEqual(Tier.Min, SpindleLodSystem.TierFor(420.1f, Tier.Mid, Covert(p)));
            Assert.AreEqual(Tier.Min, SpindleLodSystem.TierFor(2000f, Tier.Min, Covert(p)), "inside the cull band a thread stays drawn");
            Assert.AreEqual(Tier.Culled, SpindleLodSystem.TierFor(SpindleLodSystem.CullBoundFor(p) + 1f, Tier.Min, Covert(p)));
            Object.DestroyImmediate(p);
        }

        [Test]
        public void Inward_WaitsForTheHysteresisBand()
        {
            var p = Profile();
            float keep = 1f - p.hysteresis;
            Assert.AreEqual(Tier.Mid, SpindleLodSystem.TierFor(p.nearEndGirths * keep + 0.1f, Tier.Mid, Covert(p)), "mid holds inside the band");
            Assert.AreEqual(Tier.Near, SpindleLodSystem.TierFor(p.nearEndGirths * keep - 0.1f, Tier.Mid, Covert(p)));
            Assert.AreEqual(Tier.Min, SpindleLodSystem.TierFor(p.midEndGirths * keep + 0.1f, Tier.Min, Covert(p)), "the thread holds inside the band");
            Assert.AreEqual(Tier.Mid, SpindleLodSystem.TierFor(p.midEndGirths * keep - 0.1f, Tier.Min, Covert(p)));
            Object.DestroyImmediate(p);
        }

        [Test]
        public void EverySwapHappensWhereBothTiersDrawTheSamePicture()
        {
            var p = Profile();
            Assert.IsTrue(p.BandsAreSane(out string why), why);
            float keep = 1f - p.hysteresis;
            // near <-> mid: the whole band [nearEnd x keep, nearEnd] lies below convergeStart, where
            // the mid graph returns SpindleGraph's lace exactly.
            Assert.LessOrEqual(p.nearEndGirths, p.convergeStartGirths);
            // mid <-> far: the whole band [midEnd x keep, midEnd] lies at or past convergeEnd, where
            // the mid braid has fully converged into the far thread.
            Assert.AreEqual(p.ConvergeEndGirths, p.midEndGirths * keep, 1e-4f);
            // far <-> culled: the inward edge is farFadeEnd itself, where the thread's alpha is zero.
            Assert.AreEqual(p.farFadeEndGirths, SpindleLodSystem.CullBoundFor(p) * keep, 1e-3f);
            Assert.AreEqual(SpindleLodSystem.CullBoundFor(p), Covert(p).cull, 1e-4f);
            Object.DestroyImmediate(p);
        }

        [Test]
        public void ATierIsAPureFunctionOfDistanceOnceSettled()
        {
            // Walking out and back in visits every tier in order and returns to the start: no
            // distance strands a limb in a tier the band does not allow.
            var p = Profile();
            var t = Tier.Near;
            Tier previous = t;
            for (float m = 1f; m < 3000f; m *= 1.01f)
            {
                t = SpindleLodSystem.TierFor(m, t, Covert(p));
                Assert.GreaterOrEqual((int)t, (int)previous, $"tier went backwards walking out at {m}");
                previous = t;
            }
            Assert.AreEqual(Tier.Culled, t);
            for (float m = 3000f; m > 1f; m /= 1.01f)
            {
                t = SpindleLodSystem.TierFor(m, t, Covert(p));
                Assert.LessOrEqual((int)t, (int)previous, $"tier went backwards walking in at {m}");
                previous = t;
            }
            Assert.AreEqual(Tier.Near, t);
            Object.DestroyImmediate(p);
        }

        [Test]
        public void Overt_SwapsOnlyOnTheFlatEndsOfThePrismsRamp()
        {
            // Lockstep: near and mid swap only where the prisms' ramp has opened nothing yet; mid and far
            // only where it has opened everything. Inside both bands the two tiers draw the same braid.
            var p = Profile();
            var b = Overt(p);
            Assert.IsTrue(b.overt);
            Assert.AreEqual(0f, MaxPrismOpening(b.nearEnd), 1e-4f, "the braid has opened at the near/mid boundary");
            Assert.AreEqual(0f, MaxPrismOpening(b.nearEnd * b.keep), 1e-4f);
            Assert.Greater(MaxPrismOpening(b.nearEnd * 1.05f), 0f, "the boundary is not where opening STARTS");
            Assert.AreEqual(1f, b.midEnd * b.keep, 1e-5f, "the far tier's inward edge must be the prisms' over-distance");
            Assert.AreEqual(Danger.z * 20f - Danger.z, MaxPrismOpening(b.midEnd * b.keep), 1e-4f, "not fully open at the far edge");
            Assert.AreEqual(p.overtFadeEndNorm, b.cull * b.keep, 1e-3f, "the cull's inward edge is the fade's end");
            Object.DestroyImmediate(p);
        }

        [Test]
        public void Overt_StepsDownAsTheEffectSettlesBack()
        {
            // Far side (round 5): the 144-triangle braid carries the whole recede, and the 144 -> 72 step's
            // inward edge is the recede's END (both braids fully converged into the thread's own
            // construction); the 72 -> thread step comes at overtThreadStart (~1,000 u), after the
            // 72-braid's band; the thread fades only once it has taken over and is gone by ~1,500 u.
            var p = Profile();
            var b = Overt(p);
            Assert.AreEqual(Recede.y, b.lowStart * b.keep, 1e-4f, "the 72-triangle braid takes over before the recede has ended");
            Assert.AreEqual(p.overtThreadStartNorm, b.minStart, 1e-5f);
            Assert.Greater(b.minStart * b.keep, b.lowStart, "the thread's band overlaps the 72-braid's");
            Assert.Less(b.midEnd, b.lowStart * b.keep, "the peak's band overlaps the 72-braid's");
            Assert.GreaterOrEqual(p.overtFadeStartNorm, b.minStart, "the thread fades before it has taken over");
            Assert.AreEqual(1500f, Mathf.Sqrt(p.overtFadeEndNorm * Danger.w), 1f, "the spindles are not gone by ~1,500 units");
            Assert.IsFalse(p.OvertBandsAreSane(new Vector4(2.25f, 9.5f, 0, 0), out _), "a thread band inside the 72-braid's");
            Assert.IsTrue(p.OvertBandsAreSane(Recede, out string why), why);
            Assert.IsFalse(p.OvertBandsAreSane(new Vector4(1.02f, 16f, 0, 0), out _), "a recede inside the over-distance band");
            Object.DestroyImmediate(p);
        }

        [Test]
        public void Overt_WalkVisitsEveryTierInOrder()
        {
            var p = Profile();
            var b = Overt(p);
            var t = Tier.Near;
            var seen = new System.Collections.Generic.List<Tier> { t };
            for (float n = 0.01f; n < 200f; n *= 1.01f)
            {
                var next = SpindleLodSystem.TierFor(n, t, b);
                Assert.GreaterOrEqual((int)next, (int)t, $"tier went backwards walking out at norm {n}");
                if (next != t) seen.Add(next);
                t = next;
            }
            CollectionAssert.AreEqual(new[] { Tier.Near, Tier.Mid, Tier.Far, Tier.Low, Tier.Min, Tier.Culled }, seen);
            for (float n = 200f; n > 0.01f; n /= 1.01f)
            {
                var next = SpindleLodSystem.TierFor(n, t, b);
                Assert.LessOrEqual((int)next, (int)t, $"tier went backwards walking in at norm {n}");
                t = next;
            }
            Assert.AreEqual(Tier.Near, t);
            Object.DestroyImmediate(p);
        }

        [Test]
        public void ChargeLimbsPartHardest()
        {
            var p = Profile();
            Assert.AreEqual(0.01f, p.StrengthFor(true), 1e-6f);
            Assert.AreEqual(0.005f, p.StrengthFor(false), 1e-6f);
            Assert.Greater(p.StrengthFor(true), p.StrengthFor(false));
            Object.DestroyImmediate(p);
        }

        [Test]
        public void Overt_OpeningStart_IsTheFirstAxisToOpen()
        {
            // z (spread 0.8, gain 20) opens first for the danger tier: (0.8 + 7) / (16 + 7).
            Assert.AreEqual((0.8f + 7f) / (16f + 7f), IncorporealDepth.OpeningStartNorm(Danger), 1e-5f);
            Assert.AreEqual(1f, IncorporealDepth.OpeningStartNorm(Vector4.zero), "nothing published opens nothing");
        }

        [Test]
        public void ShippedIncorporealDepth_ReadsTheDangerPrism()
        {
            var config = AssetDatabase.LoadAssetAtPath<IncorporealDepthSO>("Assets/Resources/IncorporealDepth.asset");
            Assert.IsNotNull(config, "Resources/IncorporealDepth is missing");
            Assert.IsNotNull(config.tierMaterialSet, "IncorporealDepth has no tier material set");
            var danger = config.tierMaterialSet.DangerousBlockMaterial;
            Assert.IsNotNull(danger);
            Vector4 s = config.TierSpread();
            Vector4 want = danger.GetVector("_Spread") * config.strength;
            Assert.AreEqual(want.z, s.z, 1e-5f, "the spread is not the danger prism's");
            Assert.AreEqual(danger.GetFloat("_SqrDistance"), s.w, 1e-3f, "the ramp is not the prisms'");
        }

        [Test]
        public void BrokenBandOrder_IsRejected()
        {
            var p = Profile();
            p.convergeStartGirths = 90f;   // the mid tier would start converging before it exists
            Assert.IsFalse(p.BandsAreSane(out _));
            p.convergeStartGirths = 110f;
            p.farFadeStartGirths = 400f;   // the thread would fade before it is a thread
            Assert.IsFalse(p.BandsAreSane(out _));
            Object.DestroyImmediate(p);
        }

        [Test]
        public void ShippedProfile_IsSaneAndMeasured()
        {
            var shipped = AssetDatabase.LoadAssetAtPath<SpindleLodProfileSO>("Assets/Resources/SpindleLodProfile.asset");
            Assert.IsNotNull(shipped, "Resources/SpindleLodProfile is missing - run Tools/Build/author_spindle_lod_meshes.py");
            Assert.IsTrue(shipped.IsSane(out string why), why);
            Assert.IsNotEmpty(shipped.meshSets);
            foreach (var set in shipped.meshSets)
                Assert.IsTrue(set.MatchesImport(out string mismatch),
                    $"the LOD meshes no longer match the imported braid: {mismatch}");
        }

        [Test]
        public void ShippedProfile_CarriesTheDepthTemplate()
        {
            // The creatures' bodies and the switch rings are cloned from it (Docs/SPINDLE_LOD.md section 8):
            // SpindleMaterial on SpindleDepthGraph, its three depth floats at their pass-through 0.
            var shipped = AssetDatabase.LoadAssetAtPath<SpindleLodProfileSO>("Assets/Resources/SpindleLodProfile.asset");
            Assert.IsNotNull(shipped);
            var template = shipped.depthTemplate;
            Assert.IsNotNull(template, "depthTemplate is unset - run Tools/Build/author_spindle_lod_meshes.py");
            foreach (var property in new[] { "_SpindleLodStrength", "_SpindleDepthReach", "_SpindleDepthForm" })
            {
                Assert.IsTrue(template.HasProperty(property), $"SpindleDepth.mat's shader has no {property}");
                Assert.AreEqual(0f, template.GetFloat(property), $"{property} must default to the pass-through 0");
            }
            foreach (var property in new[] { "_DullColor", "_BrightColor", "_CellDensity", "_SwayAmplitude", "_PrismClock" })
                Assert.IsTrue(template.HasProperty(property), $"SpindleDepthGraph lost SpindleGraph's {property}");
            Assert.That(shipped.switchRingStrength, Is.InRange(0f, 2f));
            Assert.Greater(shipped.switchRingDistance, 2000000f, "a switch ring's lace must outlast a branch's");
        }

        [Test]
        public void ShippedProfile_CarriesTheSyntheticWire()
        {
            // The switch rings' cable (Docs/SPINDLE_LOD.md section 9): one synthetic set whose mid tier IS
            // its near mesh (the near tier already draws on SpindleWireGraph), and the wire template.
            var shipped = AssetDatabase.LoadAssetAtPath<SpindleLodProfileSO>("Assets/Resources/SpindleLodProfile.asset");
            Assert.IsNotNull(shipped);
            var wire = shipped.SyntheticSet;
            Assert.IsNotNull(wire, "no synthetic mesh set - run Tools/Build/author_spindle_lod_meshes.py");
            Assert.AreSame(wire.source, wire.mid, "the wire's mid tier must be its near mesh");
            Assert.AreNotSame(wire.source, wire.low);
            Assert.IsTrue(wire.MatchesImport(out string why), why);
            Assert.IsNotNull(shipped.wireTemplate, "wireTemplate is unset");
            foreach (var property in new[] { "_SpindleLodGirth", "_SpindleLodStrand", "_SpindleLodStrength", "_DullColor", "_Distance" })
                Assert.IsTrue(shipped.wireTemplate.HasProperty(property), $"SpindleWire.mat's shader has no {property}");
            int synthetic = 0;
            foreach (var set in shipped.meshSets) if (set.synthetic) synthetic++;
            Assert.AreEqual(1, synthetic, "exactly one synthetic set");
        }

        [Test]
        public void SyntheticWire_IsNeverCulled()
        {
            // A switch is a signal you fly toward: past the cull a ring stays the thread (which on
            // SpindleWireGraph never fades); a limb leaves.
            Assert.AreEqual(Tier.Min, SpindleLodSystem.Kept(Tier.Culled, synthetic: true));
            Assert.AreEqual(Tier.Culled, SpindleLodSystem.Kept(Tier.Culled, synthetic: false));
            foreach (Tier t in System.Enum.GetValues(typeof(Tier)))
                if (t != Tier.Culled) Assert.AreEqual(t, SpindleLodSystem.Kept(t, synthetic: true));
        }

        [Test]
        public void BodyReach_IsAHalfOctaveCeiling()
        {
            // Bodies of a size share a depth material: reach rounds UP to the next power of sqrt(2), so a
            // body never blooms its extremities short of the full travel.
            float prev = 0f;
            for (float r = 0.5f; r < 300f; r *= 1.07f)
            {
                float b = SpindleLodSystem.ReachBucket(r);
                Assert.GreaterOrEqual(b, Mathf.Max(r, 1f) - 1e-3f, $"reach {r} rounded down to {b}");
                Assert.LessOrEqual(b, Mathf.Max(r, 1f) * 1.4143f, $"reach {r} rounded up past half an octave to {b}");
                Assert.GreaterOrEqual(b, prev, "the bucket is not monotone");
                prev = b;
            }
        }

        [Test]
        public void BloomTravel_IsTheDangerFaceCappedByReach()
        {
            // The culling allowance for a bloomed body: the danger face's 15.2 x strength, never more than
            // the bloom's cap (a share of the reach), plus the wave's breathing.
            IncorporealDepth.Publish();
            var flow = IncorporealDepthSO.Instance ? IncorporealDepthSO.Instance.Flow : Vector4.zero;
            float breathe = 1f + Mathf.Max(flow.x, 0f);
            Assert.AreEqual(IncorporealDepth.MaxOpening * 0.35f, IncorporealDepth.BloomTravel(0.35f, 1000f), 1e-3f);
            Assert.AreEqual(IncorporealDepth.BloomMaxShare * 4f * breathe, IncorporealDepth.BloomTravel(0.35f, 4f), 1e-4f);
        }
    }
}
#endif
