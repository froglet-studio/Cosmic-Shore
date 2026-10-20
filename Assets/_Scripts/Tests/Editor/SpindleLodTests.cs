#if UNITY_EDITOR
using CosmicShore.Gameplay;
using CosmicShore.ScriptableObjects;
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

        [Test]
        public void Outward_SwapsExactlyAtEachBoundary()
        {
            var p = Profile();
            Assert.AreEqual(Tier.Near, SpindleLodSystem.TierFor(99.9f, Tier.Near, p));
            Assert.AreEqual(Tier.Mid, SpindleLodSystem.TierFor(100.1f, Tier.Near, p));
            Assert.AreEqual(Tier.Mid, SpindleLodSystem.TierFor(419.9f, Tier.Mid, p));
            Assert.AreEqual(Tier.Far, SpindleLodSystem.TierFor(420.1f, Tier.Mid, p));
            Assert.AreEqual(Tier.Far, SpindleLodSystem.TierFor(2000f, Tier.Far, p), "inside the cull band a thread stays drawn");
            Assert.AreEqual(Tier.Culled, SpindleLodSystem.TierFor(SpindleLodSystem.CullBoundFor(p) + 1f, Tier.Far, p));
            Object.DestroyImmediate(p);
        }

        [Test]
        public void Inward_WaitsForTheHysteresisBand()
        {
            var p = Profile();
            float keep = 1f - p.hysteresis;
            Assert.AreEqual(Tier.Mid, SpindleLodSystem.TierFor(p.nearEndGirths * keep + 0.1f, Tier.Mid, p), "mid holds inside the band");
            Assert.AreEqual(Tier.Near, SpindleLodSystem.TierFor(p.nearEndGirths * keep - 0.1f, Tier.Mid, p));
            Assert.AreEqual(Tier.Far, SpindleLodSystem.TierFor(p.midEndGirths * keep + 0.1f, Tier.Far, p), "far holds inside the band");
            Assert.AreEqual(Tier.Mid, SpindleLodSystem.TierFor(p.midEndGirths * keep - 0.1f, Tier.Far, p));
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
                t = SpindleLodSystem.TierFor(m, t, p);
                Assert.GreaterOrEqual((int)t, (int)previous, $"tier went backwards walking out at {m}");
                previous = t;
            }
            Assert.AreEqual(Tier.Culled, t);
            for (float m = 3000f; m > 1f; m /= 1.01f)
            {
                t = SpindleLodSystem.TierFor(m, t, p);
                Assert.LessOrEqual((int)t, (int)previous, $"tier went backwards walking in at {m}");
                previous = t;
            }
            Assert.AreEqual(Tier.Near, t);
            Object.DestroyImmediate(p);
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
