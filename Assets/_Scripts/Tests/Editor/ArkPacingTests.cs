using System.Collections.Generic;
using CosmicShore.Gameplay;
using CosmicShore.ScriptableObjects;
using NUnit.Framework;
using UnityEngine;

namespace CosmicShore.Tests
{
    /// <summary>The Ark escort's cell-aware pace (<see cref="ArkSpeedProfile"/>) and its named overrides
    /// (<see cref="ArkEscortParams"/>): pure functions, no scene.</summary>
    public class ArkPacingTests
    {
        static ArkSpeedProfile P => ArkSpeedProfile.Default.Sanitized();

        [Test]
        public void OpenWaterCruises()
        {
            Assert.AreEqual(P.Cruise, P.SpeedAt(5000f, 5000f), 1e-3f);
            Assert.AreEqual(P.Cruise, P.SpeedAt(5000f, float.PositiveInfinity), 1e-3f);
        }

        [Test]
        public void MembraneIsEntryAndCytoplasmCrawls()
        {
            Assert.AreEqual(P.Entry, P.SpeedAt(0f, float.PositiveInfinity), 1e-3f);
            Assert.AreEqual(P.Crawl, P.SpeedAt(-P.MembraneBand, float.PositiveInfinity), 1e-3f);
            Assert.AreEqual(P.Crawl, P.SpeedAt(-1200f, float.PositiveInfinity), 1e-3f);
        }

        [Test]
        public void LeavingACellCrawlsUntilTheSternClears()
        {
            // Just retargeted at the next core, still inside the departed cell: crawl, not cruise.
            Assert.AreEqual(P.Crawl, P.SpeedAt(2000f, -1200f), 1e-3f);
            Assert.AreEqual(P.Crawl, P.SpeedAt(2000f, P.HullHalfLength), 1e-3f);
            Assert.AreEqual(P.Cruise, P.SpeedAt(2000f, P.HullHalfLength + P.MembraneBand), 1e-3f);
        }

        [Test]
        public void ContinuousAndNeverAboveTheCeiling()
        {
            var p = new ArkSpeedProfile { Cruise = 200f, Entry = 30f, Crawl = 10f, MembraneBand = 400f, Ceiling = 50f }.Sanitized();
            Assert.LessOrEqual(p.Cruise, 50f);
            float last = p.SpeedAt(-1500f, float.PositiveInfinity);
            for (float d = -1500f; d <= 1500f; d += 5f)
            {
                float s = p.SpeedAt(d, float.PositiveInfinity);
                Assert.LessOrEqual(s, 50f);
                Assert.Less(Mathf.Abs(s - last), 2f, $"jump at {d}");
                last = s;
            }
        }

        [Test]
        public void ParamsParseAndApplyToACopy()
        {
            var values = ArkEscortParams.ParseFlatJson(
                "{\"ark.crawlSpeed\": 19.5, \"tier.all.breachHull\": 0, \"tier.2.population\": 0.9, \"nope.x\": 3, \"ark.platesAreFood\": false}");
            Assert.AreEqual(19.5f, values["ark.crawlSpeed"], 1e-4f);
            Assert.AreEqual(0f, values["ark.platesAreFood"]);

            var escort = ScriptableObject.CreateInstance<ArkEscortSO>();
            try
            {
                var echo = ArkEscortParams.Apply(new ArkEscortParams.Target { Escort = escort }, values);
                var applied = new Dictionary<string, bool>();
                foreach (var e in echo) applied[e.Key] = e.Applied;
                Assert.IsTrue(applied["ark.crawlSpeed"]);
                Assert.IsTrue(applied["ark.platesAreFood"]);
                Assert.IsFalse(applied["nope.x"]);
                // A fresh SO authors no tiers, so tier keys are refused rather than thrown.
                Assert.IsFalse(applied["tier.2.population"]);
                Assert.AreEqual(19.5f, escort.ArkPacing.Crawl, 1e-4f);
                Assert.IsFalse(escort.PlatesAreFood);
            }
            finally
            {
                Object.DestroyImmediate(escort);
            }
        }

        [Test]
        public void SpeciesKeysNormalize()
        {
            Assert.AreEqual("shark", FaunaRuntimeTuning.SpeciesKey("MassSharkFauna(Clone)"));
            Assert.AreEqual("brittlestar", FaunaRuntimeTuning.SpeciesKey("MassBrittlestarFauna"));
            Assert.AreEqual("quadfish", FaunaRuntimeTuning.SpeciesKey("QuadFish"));
            Assert.AreEqual("tadpole", FaunaRuntimeTuning.SpeciesKey("TadPoleFauna (Clone)"));
            Assert.AreEqual("wormcolony", FaunaRuntimeTuning.SpeciesKey("WormColony"));
        }

        [Test]
        public void FaunaKeysReachTheRunTuning()
        {
            var target = new ArkEscortParams.Target();
            var echo = ArkEscortParams.Apply(target, new Dictionary<string, float>
            {
                ["fauna.shark.maxSpeed"] = 40f,
                ["fauna.all.shieldBite"] = 0.3f,
                ["fauna.quadfish.capScale"] = 1.5f,
                ["fauna.2.populationScale"] = 0.5f,
                ["fauna.all.floorScale"] = 1.2f,
                ["fauna.shark.populationScale"] = 2f,   // species take floor and cap, not population
                ["fauna.shark.maxSpeed2"] = 1f,         // unknown field
                ["fauna.shark.minSpeed"] = 9999f,       // out of range
            });
            var applied = new Dictionary<string, bool>();
            foreach (var e in echo) applied[e.Key] = e.Applied;
            Assert.IsTrue(applied["fauna.shark.maxSpeed"]);
            Assert.IsTrue(applied["fauna.all.shieldBite"]);
            Assert.IsTrue(applied["fauna.quadfish.capScale"]);
            Assert.IsTrue(applied["fauna.2.populationScale"]);
            Assert.IsTrue(applied["fauna.all.floorScale"]);
            Assert.IsFalse(applied["fauna.shark.populationScale"]);
            Assert.IsFalse(applied["fauna.shark.maxSpeed2"]);
            Assert.IsFalse(applied["fauna.shark.minSpeed"]);

            Assert.IsTrue(target.Fauna.TryGet("shark", "maxSpeed", out float v));
            Assert.AreEqual(40f, v);
            Assert.IsTrue(target.Fauna.TryGet("brittlestar", "shieldBite", out v));   // via "all"
            Assert.AreEqual(0.3f, v, 1e-5f);
            Assert.AreEqual(1.5f, target.Fauna.CapScale("quadfish"), 1e-5f);
            Assert.AreEqual(0.5f, target.WorldValue(2, "populationScale", 1f), 1e-5f);
            Assert.AreEqual(1f, target.WorldValue(3, "populationScale", 1f), 1e-5f);
            Assert.AreEqual(1.2f, target.WorldValue(4, "floorScale", 1f), 1e-5f);
        }

        [Test]
        public void LightDataIsCopiedNotEdited()
        {
            var source = ScriptableObject.CreateInstance<LightFaunaDataSO>();
            var tuning = new FaunaRuntimeTuning();
            tuning.Set("shark", "maxSpeed", 50f);
            try
            {
                var copy = tuning.ResolveLightData(source, "shark");
                Assert.AreNotSame(source, copy);
                Assert.AreEqual(50f, copy.maxSpeed);
                Assert.AreEqual(6f, source.maxSpeed);
                Assert.AreSame(copy, tuning.ResolveLightData(source, "shark"));
                Assert.AreSame(source, tuning.ResolveLightData(source, "quadfish"));
                Object.DestroyImmediate(copy);
            }
            finally
            {
                Object.DestroyImmediate(source);
            }
        }

        [Test]
        public void AiKeysGoToTheExternalSetter()
        {
            var before = ArkEscortParams.ExternalSetter;
            string seen = null;
            ArkEscortParams.ExternalSetter = (k, v) => { seen = k; return true; };
            try
            {
                var echo = ArkEscortParams.Apply(new ArkEscortParams.Target(),
                    new Dictionary<string, float> { ["ai.sparrow.engageRadius"] = 400f });
                Assert.AreEqual("ai.sparrow.engageRadius", seen);
                Assert.IsTrue(echo[0].Applied);
            }
            finally
            {
                ArkEscortParams.ExternalSetter = before;
            }
        }
    }
}
