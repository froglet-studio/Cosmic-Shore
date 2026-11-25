using System.Collections.Generic;
using CosmicShore.Gameplay;
using NUnit.Framework;
using UnityEngine;

namespace CosmicShore.Tests
{
    /// <summary>
    /// Skim Race's garden ruins (SKIMRACE.md §5b) on a ribbon built here (a tilted, wavy loop with a crystal marker
    /// every 39 blocks): only the hardest rung has them, seven ruins stand spread round the lap, the lane round the
    /// ribbon and round every straight crystal-to-crystal line stays clear of every STANDING ruin, nothing stands at a
    /// crystal or in the nucleus, a wheel is laid whole or not at all, and the lay is a pure function of the ribbon.
    /// The shipped I4 ribbon is held by <c>SkimRaceRuinsShippedTrackTests</c>.
    /// </summary>
    public class SkimRaceRuinsTests
    {
        const float Tolerance = 0.01f;

        internal sealed class Laid
        {
            public readonly List<Vector3> Ribbon = new();
            public readonly List<bool> Marker = new();
            public readonly List<RaceGate> Gates = new();
            public readonly List<SpearlineRuinPart> Parts = new();
            public readonly List<SpearlineRuinWheel> Wheels = new();
            public readonly List<bool> Keep = new();
            public Vector3 Core;
            public List<Vector3> Lines;

            public bool Turns(int part)
            {
                foreach (var wheel in Wheels)
                    if (part >= wheel.First && part < wheel.First + wheel.Count) return true;
                return false;
            }

            public HashSet<int> RuinLegs()
            {
                var legs = new HashSet<int>();
                for (int i = 0; i < Parts.Count; i++)
                    if (Keep[i]) legs.Add(Parts[i].Leg);
                return legs;
            }
        }

        /// <summary>A closed loop of 780 blocks 12 u apart (radius about 1,490 u), tilted and waved so no two legs are
        /// alike, a marker every 39 blocks (20 crystals, legs of about 470 u).</summary>
        static void Loop(Laid into)
        {
            const int blocks = 780;
            const float radius = 1490f;
            for (int i = 0; i < blocks; i++)
            {
                float a = 2f * Mathf.PI * i / blocks;
                float r = radius + 120f * Mathf.Sin(3f * a);
                into.Ribbon.Add(new Vector3(r * Mathf.Cos(a), 160f * Mathf.Sin(2f * a + 0.4f), r * Mathf.Sin(a)));
                into.Marker.Add(i % 39 == 5);
            }
        }

        internal static Laid Lay(int intensity, Vector3 core, System.Action<Laid> course = null)
        {
            var laid = new Laid { Core = core };
            (course ?? Loop)(laid);
            SkimRaceRuins.Lay(laid.Ribbon, laid.Marker, core, intensity, laid.Gates, laid.Parts, laid.Wheels, laid.Keep);
            laid.Lines = SkimRaceRuins.FlownLines(laid.Ribbon, laid.Gates);
            return laid;
        }

        /// <summary>The rules every laid ruin keeps, on any ribbon.</summary>
        internal static void AssertRules(Laid laid)
        {
            Assert.AreEqual(laid.Parts.Count, laid.Keep.Count, "one flag for each part");
            for (int i = 0; i < laid.Parts.Count; i++)
            {
                if (!laid.Keep[i] || laid.Turns(i)) continue;
                var part = laid.Parts[i];
                Assert.GreaterOrEqual(SkimRaceRuins.Clearance(part, laid.Lines), SkimRaceRuins.RibbonKeep - Tolerance,
                    $"part {i} (leg {part.Leg}) reaches into the lane round the ribbon or a crystal-to-crystal line");
                Assert.GreaterOrEqual(SpearlineRuins.PartRingClearance(laid.Gates, part), SpearlineRuins.RingMargin - Tolerance,
                    $"part {i} (leg {part.Leg}) stands too near a crystal");
                Assert.GreaterOrEqual(SkimRaceRuins.CoreClearance(part, laid.Core), SkimRaceRuins.CoreRadius - Tolerance,
                    $"part {i} (leg {part.Leg}) stands in the nucleus");
            }
            foreach (var wheel in laid.Wheels)
            {
                bool first = laid.Keep[wheel.First];
                for (int i = wheel.First; i < wheel.First + wheel.Count; i++)
                    Assert.AreEqual(first, laid.Keep[i], "a wheel is laid whole or not at all");
                if (!first) continue;
                Assert.IsTrue(SkimRaceRuins.WheelTurnsFree(wheel, laid.Lines, laid.Core),
                    "a laid wheel sweeps a flown line outside its hub, or the nucleus");
                Assert.GreaterOrEqual(SpearlineRuins.RingClearance(laid.Gates, wheel.Centre),
                    SpearlineRuins.RingMargin + SpearlineRuins.WheelRadius - Tolerance, "a wheel sweeps a crystal");
            }
        }

        static readonly Vector3 FarCore = new(0f, 0f, 0f);   // the loop's centre: 1,300 u from the ribbon

        [Test]
        public void BelowI4_NoRuins()
        {
            for (int intensity = 1; intensity <= 3; intensity++)
            {
                var laid = Lay(intensity, FarCore);
                Assert.AreEqual(0, laid.Parts.Count, $"I{intensity} lays no ruin");
                Assert.AreEqual(0, laid.Keep.Count);
                Assert.AreEqual(0, laid.Wheels.Count);
            }
        }

        [Test]
        public void I4_SevenRuins_SpreadRoundTheLap()
        {
            var laid = Lay(4, FarCore);
            var legs = laid.RuinLegs();
            Assert.AreEqual(7, legs.Count, "seven ruins a lap");
            Assert.AreEqual(20, laid.Gates.Count, "one gate for each crystal marker");

            // Spread: no two ruins on neighbouring legs, and the lap's first and last third both carry one.
            int legCount = laid.Gates.Count - 1;
            foreach (int leg in legs) Assert.IsFalse(legs.Contains(leg + 1), $"ruins on legs {leg} and {leg + 1}");
            bool early = false, late = false;
            foreach (int leg in legs)
            {
                early |= leg < legCount / 3;
                late |= leg >= legCount - legCount / 3;
            }
            Assert.IsTrue(early && late, "the ruins are bunched in one part of the lap");
        }

        [Test]
        public void I4_EachRuinReadsAsARuin()
        {
            var laid = Lay(4, FarCore);
            var perLeg = new Dictionary<int, int>();
            for (int i = 0; i < laid.Parts.Count; i++)
            {
                if (!laid.Keep[i]) continue;
                perLeg.TryGetValue(laid.Parts[i].Leg, out int n);
                perLeg[laid.Parts[i].Leg] = n + 1;
            }
            foreach (var pair in perLeg)
                Assert.GreaterOrEqual(pair.Value, SkimRaceRuins.MinParts, $"the ruin on leg {pair.Key} is a few loose stones");
        }

        [Test]
        public void I4_LaneCrystalsAndNucleusStayClear()
        {
            AssertRules(Lay(4, FarCore));
        }

        [Test]
        public void I4_NucleusOnTheRibbon_TakesItsRuinsAway()
        {
            // Put the nucleus on a leg that carries a ruin: that ruin goes, the rules still hold, others remain.
            var open = Lay(4, FarCore);
            int leg = -1;
            foreach (int l in open.RuinLegs()) { leg = l; break; }
            Vector3 onLeg = 0.5f * (open.Gates[leg].Position + open.Gates[leg + 1].Position);

            var laid = Lay(4, onLeg);
            AssertRules(laid);
            Assert.IsFalse(laid.RuinLegs().Contains(leg), "a ruin stands on the leg the nucleus sits on");
            Assert.GreaterOrEqual(laid.RuinLegs().Count, 5, "the other legs lost their ruins too");
        }

        [Test]
        public void Lay_IsAPureFunctionOfTheRibbon()
        {
            var a = Lay(4, FarCore);
            var b = Lay(4, FarCore);
            Assert.AreEqual(a.Parts.Count, b.Parts.Count);
            for (int i = 0; i < a.Parts.Count; i++)
            {
                Assert.AreEqual(a.Keep[i], b.Keep[i]);
                Assert.AreEqual(a.Parts[i].Position, b.Parts[i].Position);
                Assert.AreEqual(a.Parts[i].Scale, b.Parts[i].Scale);
            }
        }

        [Test]
        public void TooFewCrystals_NoRuins()
        {
            var laid = Lay(4, FarCore, l =>
            {
                Loop(l);
                for (int i = 0; i < l.Marker.Count; i++) l.Marker[i] = i == 5 || i == 400;
            });
            Assert.AreEqual(0, laid.Parts.Count, "two crystals make no lap of legs");
        }
    }
}
