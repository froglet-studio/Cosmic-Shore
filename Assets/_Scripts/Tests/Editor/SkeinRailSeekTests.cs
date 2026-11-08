using System.Collections.Generic;
using CosmicShore.Data;
using CosmicShore.Gameplay;
using NUnit.Framework;
using UnityEngine;

namespace CosmicShore.Tests
{
    /// <summary>
    /// Skein's free-flight rail seek (<see cref="SkeinRailSeek"/>, the model's <c>seek_candidates</c> and
    /// <c>race(seek=True)</c> in <c>Tools/Build/skein_urchin_sim.py</c>) and the Urchin's per-mode config.
    /// </summary>
    public class SkeinRailSeekTests
    {
        /// <summary>A straight rail along +X from x=0 to x=2000, a prism every 8 u, through a ring at x=1500.</summary>
        static (List<Vector3[]> rails, List<float[]> arcs) StraightRail()
        {
            var pts = new Vector3[251];
            for (int k = 0; k < pts.Length; k++) pts[k] = new Vector3(8f * k, 0f, 0f);
            return (new List<Vector3[]> { pts }, new List<float[]> { SkeinRailSeek.ArcLengths(pts) });
        }

        static readonly Vector3 Ring = new Vector3(1500f, 0f, 0f);

        [Test]
        public void Candidates_AreThePrismsWithinTheArcBeforeTheRing()
        {
            var (rails, arcs) = StraightRail();
            var c = SkeinRailSeek.Candidates(rails, arcs, Ring, 24f, 1600f, 2);
            Assert.IsNotEmpty(c);
            foreach (var cand in c)
            {
                Assert.AreEqual(0, cand.Prism % 2, "every second prism");
                Assert.LessOrEqual(cand.ArcToRing, 1600f);
                Assert.GreaterOrEqual(cand.ArcToRing, 0f);
            }
            // The first prism (x=0) is 1500 u of rail from the ring - inside the arc.
            Assert.IsTrue(c.Exists(x => x.Prism == 0));
        }

        [Test]
        public void Candidates_NoneWhenTheRailPassesOutsideTheMouth()
        {
            var (rails, arcs) = StraightRail();
            var c = SkeinRailSeek.Candidates(rails, arcs, Ring + new Vector3(0f, 40f, 0f), 24f, 1600f, 2);
            Assert.IsEmpty(c, "a rail 40 u off a 24 u capture does not thread it");
        }

        [Test]
        public void TryPick_FliesToTheRailWhenTheRideBeatsTheFlight()
        {
            var (rails, arcs) = StraightRail();
            var c = SkeinRailSeek.Candidates(rails, arcs, Ring, 24f, 1600f, 2);
            // Beside the rail's start, 1500 u from the ring: flying it at 65 u/s is 23 s, riding it at 300 is 5 s.
            var self = new Vector3(-100f, 50f, 0f);
            Assert.IsTrue(SkeinRailSeek.TryPick(c, rails, self, Ring, 65f, 300f, 0.9f, 0.3f, 3, -1, out var aim));
            Assert.Less(aim.x, Ring.x, "the seek point is on the rail before the ring");
            Assert.AreEqual(0f, aim.y, 1e-4f);
        }

        [Test]
        public void TryPick_FliesAtTheRingWhenItIsCloserThanAnyRail()
        {
            var (rails, arcs) = StraightRail();
            var c = SkeinRailSeek.Candidates(rails, arcs, Ring, 24f, 1600f, 2);
            var self = Ring + new Vector3(0f, 0f, -60f);
            Assert.IsFalse(SkeinRailSeek.TryPick(c, rails, self, Ring, 65f, 300f, 0.9f, 0.3f, 3, -1, out _));
        }

        [Test]
        public void TryPick_NeverJoinsARailAgainstItsHeading()
        {
            var (rails, arcs) = StraightRail();
            var c = SkeinRailSeek.Candidates(rails, arcs, Ring, 24f, 1600f, 2);
            // Ahead of the whole rail, so every candidate is reached flying against the race direction.
            var self = new Vector3(3000f, 0f, 0f);
            Assert.IsFalse(SkeinRailSeek.TryPick(c, rails, self, new Vector3(1500f, 0f, 0f), 1f, 300f, 1f, 0.3f, 3, -1,
                out _));
        }

        [Test]
        public void TryPick_SkipsTheRailJustLeft()
        {
            var (rails, arcs) = StraightRail();
            var c = SkeinRailSeek.Candidates(rails, arcs, Ring, 24f, 1600f, 2);
            var self = new Vector3(-100f, 50f, 0f);
            Assert.IsFalse(SkeinRailSeek.TryPick(c, rails, self, Ring, 65f, 300f, 0.9f, 0.3f, 3, 0, out _));
            Assert.AreEqual(0, SkeinRailSeek.RailAt(rails, new Vector3(16.5f, 0f, 0f), 2f));
            Assert.AreEqual(-1, SkeinRailSeek.RailAt(rails, new Vector3(16f, 30f, 0f), 2f));
        }

        [Test]
        public void UrchinConfig_SkeinHasItsOwnGenome_RegattaKeepsTheDefault()
        {
            var skein = UrchinAutopilotConfigSO.LoadForMode(GameModes.Skein);
            var regatta = UrchinAutopilotConfigSO.LoadForMode(GameModes.Regatta);
            var shared = UrchinAutopilotConfigSO.LoadDefault();
            Assert.AreSame(shared, regatta, "Regatta flies the shared config");
            Assert.AreNotSame(shared, skein, "Skein has its own file");
            // Genome W (Docs/Reports/SKEIN_URCHIN_AI.md): rides anything that closes at all, leaves dry crawls fast.
            Assert.AreEqual(0.1238f, skein.ClosingFraction, 1e-3f);
            Assert.AreEqual(1.3127f, skein.MaxDryCrawlSeconds, 1e-3f);
            Assert.AreEqual(0.7f, shared.ClosingFraction, 1e-4f, "the shared genome is unchanged");
        }
    }
}
