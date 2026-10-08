using CosmicShore.Data;
using CosmicShore.ScriptableObjects;
using NUnit.Framework;
using UnityEngine;

namespace CosmicShore.Tests
{
    /// <summary>
    /// The beat sequence and the shipped opt-in of a crystal fusing onto a hull
    /// (CRYSTAL_HULL_FUSION.md): the pickup sound must wait for the faces to be down, slow motion
    /// must slow every beat alike and never ship, and only the pair under test may fuse.
    /// </summary>
    public class CrystalHullFusionConfigTests
    {
        // ── Beats ─────────────────────────────────────────────────────────────────────────────

        [Test]
        public void Entry_ResolvesBeatsInOrder()
        {
            var e = new CrystalHullFusionConfigSO.Entry
            {
                peelSeconds = 0.2f, flightSeconds = 0.3f, mateSeconds = 0.2f, dissolveSeconds = 0.3f,
            };
            Assert.AreEqual(CrystalHullFusionConfigSO.Phase.Peel, e.Resolve(0.1f, out float u));
            Assert.AreEqual(0.5f, u, 1e-5f);
            Assert.AreEqual(CrystalHullFusionConfigSO.Phase.Flight, e.Resolve(0.35f, out u));
            Assert.AreEqual(0.5f, u, 1e-5f);
            Assert.AreEqual(CrystalHullFusionConfigSO.Phase.Mate, e.Resolve(0.6f, out _));
            Assert.AreEqual(CrystalHullFusionConfigSO.Phase.Dissolve, e.Resolve(0.85f, out _));
            Assert.AreEqual(CrystalHullFusionConfigSO.Phase.Done, e.Resolve(1.01f, out _));
            Assert.AreEqual(0.5f, e.MateSecondsFromStart, 1e-6f, "the pickup sound plays as the faces land");
        }

        [Test]
        public void Entry_PlaybackScale_SlowsEveryBeatAlike()
        {
            var e = new CrystalHullFusionConfigSO.Entry
            {
                peelSeconds = 0.2f, flightSeconds = 0.3f, mateSeconds = 0.2f, dissolveSeconds = 0.3f,
                playbackScale = 10f,
            };
            Assert.AreEqual(10f, e.TotalSeconds, 1e-5f);
            Assert.AreEqual(5f, e.MateSecondsFromStart, 1e-5f, "the sound waits for the slowed landing too");
            Assert.AreEqual(CrystalHullFusionConfigSO.Phase.Flight, e.Resolve(3.5f, out float u));
            Assert.AreEqual(0.5f, u, 1e-5f);
        }

        [Test]
        public void Entry_EveryFaceLands_ByTheEndOfTheFlight()
        {
            var e = new CrystalHullFusionConfigSO.Entry { flightStagger = 0.6f };
            for (float delay = 0f; delay <= 1f; delay += 0.125f)
            {
                Assert.AreEqual(1f, e.FaceFlightProgress(1f, delay), 1e-5f, $"delay {delay} must have landed by the mate");
                Assert.AreEqual(0f, e.FaceFlightProgress(delay * 0.6f, delay), 1e-5f, "and not move before its turn");
            }
        }

        [Test]
        public void ShippedConfig_DoesNotShipSlowMotion()
        {
            var config = Resources.Load<CrystalHullFusionConfigSO>(CrystalHullFusionConfigSO.ResourcePath);
            Assert.IsNotNull(config);
            foreach (var entry in config.Entries)
                Assert.AreEqual(1f, entry.playbackScale, $"{entry.vessel}/{entry.element} ships in slow motion");
        }

        [Test]
        public void ShippedConfig_FusesChargeOntoTheSquirrel()
        {
            var config = Resources.Load<CrystalHullFusionConfigSO>(CrystalHullFusionConfigSO.ResourcePath);
            Assert.IsNotNull(config, "Resources/CrystalHullFusionConfig is the opt-in - without it nothing fuses");
            Assert.IsTrue(config.TryGet(VesselClassType.Squirrel, Element.Charge, out var entry));
            Assert.Greater(entry.TotalSeconds, 0f);
            Assert.IsFalse(config.TryGet(VesselClassType.Squirrel, Element.Mass, out _),
                "only the pair under test fuses; every other pickup keeps the generic capture");
        }
    }
}
