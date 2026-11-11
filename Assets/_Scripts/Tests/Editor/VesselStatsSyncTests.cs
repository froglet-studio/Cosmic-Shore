using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using CosmicShore.Data;
using CosmicShore.Gameplay;
using CosmicShore.ScriptableObjects;
using CosmicShore.UI;

namespace CosmicShore.Tests
{
    /// <summary>
    /// The scoreboard's per-pilot telemetry (Docs/ScoringSystem/BUGS.md B21): every hull's three base stats resolve, a
    /// pilot's values are kept on its RoundStats, and the stat block reads every pilot's number.
    /// </summary>
    public class VesselStatsSyncTests
    {
        readonly List<Object> _made = new();

        [TearDown]
        public void TearDown()
        {
            foreach (var o in _made) if (o) Object.DestroyImmediate(o);
            _made.Clear();
        }

        VesselStatEventSO Stat(string name, ValueFormatType format = ValueFormatType.Integer)
        {
            var s = ScriptableObject.CreateInstance<VesselStatEventSO>();
            s.name = name;
            s.FormatType = format;
            _made.Add(s);
            return s;
        }

        RoundStats Stats()
        {
            var go = new GameObject("RoundStats");
            _made.Add(go);
            return go.AddComponent<RoundStats>();
        }

        // ── The profile hands every runtime-added telemetry its three base stats ─────────────────────────────

        [Test]
        public void Profile_NamesTheThreeBaseStats_TheDefaultListShows()
        {
            var profile = GameModeStatsProfileSO.Load();
            Assert.IsNotNull(profile, "Resources/GameModeStatsProfile");
            Assert.IsNotNull(profile.VesselLongestDrift, "longest drift");
            Assert.IsNotNull(profile.VesselMaxBoost, "max boost");
            Assert.IsNotNull(profile.VesselPrismsDamaged, "prisms damaged");
            CollectionAssert.AreEquivalent(
                new[] { profile.VesselLongestDrift, profile.VesselMaxBoost, profile.VesselPrismsDamaged },
                profile.DefaultStats, "the stats a telemetry raises are the ones the scoreboard shows");
        }

        // ── RoundStats keeps a pilot's values (offline: no network) ──────────────────────────────────────────

        [Test]
        public void ReportVesselStat_KeepsTheLatestValue()
        {
            var rs = Stats();
            rs.ReportVesselStat("PrismsDamaged", 3f);
            rs.ReportVesselStat("PrismsDamaged", 39f);

            Assert.IsTrue(rs.TryGetVesselStat("PrismsDamaged", out var v));
            Assert.AreEqual(39f, v);
            Assert.IsFalse(rs.TryGetVesselStat("MaxBoost", out _), "a stat never reported has no value");
        }

        [Test]
        public void ReportVesselStat_RaisesChanged()
        {
            var rs = Stats();
            int raised = 0;
            rs.OnVesselStatsChanged += _ => raised++;

            rs.ReportVesselStat("MaxBoost", 2.5f);

            Assert.AreEqual(1, raised);
        }

        [Test]
        public void ReportVesselStat_NameTooLongForTheWire_IsDropped()
        {
            var rs = Stats();
            var name = new string('x', RoundStats.MaxVesselStatNameBytes + 1);

            rs.ReportVesselStat(name, 1f);

            Assert.IsFalse(rs.TryGetVesselStat(name, out _));
        }

        [Test]
        public void ClearVesselStats_EmptiesThePilot()
        {
            var rs = Stats();
            rs.ReportVesselStat("PrismsDamaged", 39f);

            rs.ClearVesselStats();

            Assert.IsFalse(rs.TryGetVesselStat("PrismsDamaged", out _));
        }

        // ── The stat block reads every pilot ─────────────────────────────────────────────────────────────────

        [Test]
        public void FormatPerPilot_EveryPilotInOrder()
        {
            var stat = Stat("PrismsDamaged");
            var text = EventDrivenStatsProvider.FormatPerPilot(stat, new[]
            {
                new EventDrivenStatsProvider.PilotValue("Yash", true, 12f),
                new EventDrivenStatsProvider.PilotValue("Garrett", true, 39f),
            });

            Assert.AreEqual("Yash 12  ·  Garrett 39", text);
        }

        [Test]
        public void FormatPerPilot_PilotWithNoValue_ShowsADash()
        {
            var stat = Stat("MaxBoost", ValueFormatType.TimeSeconds);
            var text = EventDrivenStatsProvider.FormatPerPilot(stat, new[]
            {
                new EventDrivenStatsProvider.PilotValue("Yash", true, 1.5f),
                new EventDrivenStatsProvider.PilotValue("Garrett", false, 0f),
            });

            Assert.AreEqual("Yash 1.50s  ·  Garrett -", text);
        }
    }
}
