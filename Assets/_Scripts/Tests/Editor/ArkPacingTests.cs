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
