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

        // Resources/IncorporealDepth's far side: hold the peak to norm 2.25, settled back by 42.25.
        static readonly Vector4 Recede = new(2.25f, 42.25f, 0f, 0f);

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
            // Far side: the 144 -> 72 step comes as the recede starts; the 72 -> thread step's inward edge
            // is the recede's END (the braid fully converged into the thread's own construction); the
            // thread fades and is culled only after that.
            var p = Profile();
            var b = Overt(p);
            Assert.AreEqual(Recede.x, b.lowStart, 1e-5f);
            Assert.AreEqual(Recede.y, b.minStart * b.keep, 1e-4f, "the thread takes over before the braid has settled back");
            Assert.Less(b.midEnd, b.lowStart * b.keep, "the peak's band overlaps the recede's");
            Assert.Greater(p.overtFadeStartNorm, b.minStart, "the thread fades before it has taken over");
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
            Assert.AreEqual(0.35f, p.StrengthFor(true), 1e-6f);
            Assert.AreEqual(0.1f, p.StrengthFor(false), 1e-6f);
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
    }
}
#endif
