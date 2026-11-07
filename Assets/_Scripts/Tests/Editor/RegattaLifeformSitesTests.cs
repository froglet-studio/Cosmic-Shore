#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using CosmicShore.Gameplay;
using NUnit.Framework;
using UnityEngine;

namespace CosmicShore.Tests
{
    /// <summary>
    /// The Regatta lifeform-site contract (Tools/Build/regatta_lifeforms.py is the other half): the defaults sow
    /// nothing, so a shipped course is the rails and nothing else; a course that asks for sites gets them beside
    /// the spine and never on a rail, never in a ring's mouth, never outside the cell's shell, each one's up
    /// pointing away from the racing line; and every peer sows the same sites from the same seed. Held on the
    /// four shipped circuits, for settings shaped like the Python presets, because a site rule is exactly the
    /// kind of thing that is fine on the leg you looked at.
    /// </summary>
    public class RegattaLifeformSitesTests
    {
        /// <summary>regatta_lifeforms.SITE_MARGIN: plants never need more than this share of the sites asked for.</summary>
        const float SiteMargin = 0.75f;

        static float LaneOffset => RegattaCourse.DefaultRails.LaneOffset;

        static IEnumerable<(int intensity, List<RaceGate> gates)> Shipped()
        {
            for (int i = 1; i <= 4; i++)
                yield return (i, RegattaCourse.BuildGates(RegattaCourse.DefaultSeed, i));
        }

        static RegattaLifeformSiteSettings Make(int perLeg, float min, float max, float side, float spread) => new()
        {
            sitesPerLeg = Enumerable.Repeat(perLeg, RegattaCourse.RingsPerLap).ToArray(),
            offsetMin = min, offsetMax = max, sideDegrees = side, sideSpreadDegrees = spread,
        };

        /// <summary>Shaped like the presets: groves (outside, beside), thicket (all round, close), reef (inward, far).</summary>
        static IEnumerable<(string name, RegattaLifeformSiteSettings s)> Settings()
        {
            yield return ("groves", Make(3, 110f, 180f, 0f, 70f));
            yield return ("thicket", Make(4, 60f, 120f, 0f, 180f));
            yield return ("reef", Make(3, 120f, 260f, 180f, 60f));
        }

        static List<FloraPlantingSite> Sow(List<RaceGate> gates, RegattaLifeformSiteSettings s, int seed = 1234)
        {
            var into = new List<FloraPlantingSite>();
            RegattaLifeformSites.Sow(gates, s, LaneOffset, seed, into);
            return into;
        }

        [Test]
        public void Defaults_SowNothing_AndAskForNoCorridor()
        {
            var s = new RegattaLifeformSiteSettings();
            Assert.IsFalse(s.HasSites);
            Assert.AreEqual(0, s.RequestedSites);
            Assert.AreEqual(0f, s.faunaCorridorRadius);
            foreach (var (_, gates) in Shipped())
                Assert.AreEqual(0, Sow(gates, s).Count);
        }

        [Test]
        public void EverySetting_KeepsEnoughSites_OnEveryShippedCircuit()
        {
            foreach (var (name, s) in Settings())
                foreach (var (i, gates) in Shipped())
                {
                    int sown = Sow(gates, s).Count;
                    Assert.GreaterOrEqual(sown, Mathf.CeilToInt(SiteMargin * s.RequestedSites),
                        $"{name} at intensity {i}: {sown} of {s.RequestedSites} sites survive the mouth / spine / shell guards");
                }
        }

        [Test]
        public void Sites_ClearTheRails_TheMouths_AndTheShell()
        {
            foreach (var (name, s) in Settings())
                foreach (var (i, gates) in Shipped())
                {
                    var rails = RegattaCourse.BuildRails(gates, RegattaCourse.DefaultRails);
                    var railPoints = rails.LanePositions.SelectMany(p => p).ToList();
                    foreach (var site in Sow(gates, s))
                    {
                        float r = site.Position.magnitude;
                        Assert.That(r, Is.InRange(RegattaLifeformSites.ShellInner, RegattaLifeformSites.ShellOuter),
                            $"{name} I{i}: a site {r:0} u from the centre is outside the shell");
                        foreach (var g in gates)
                            Assert.GreaterOrEqual((site.Position - g.Position).magnitude, g.Radius + s.ringClearance - 0.01f,
                                $"{name} I{i}: a site sits inside a ring's mouth clearance");
                        float nearestRail = railPoints.Min(p => (p - site.Position).magnitude);
                        Assert.GreaterOrEqual(nearestRail, RegattaLifeformSites.MinRailClearance - 4f,
                            $"{name} I{i}: a site roots {nearestRail:0.0} u from a rail prism");
                    }
                }
        }

        [Test]
        public void SiteUp_PointsAwayFromTheRacingLine()
        {
            foreach (var (name, s) in Settings())
                foreach (var (i, gates) in Shipped())
                {
                    var spine = RegattaLifeformSites.SpineSamples(gates, 64);
                    foreach (var site in Sow(gates, s))
                    {
                        var nearest = spine.OrderBy(p => (p - site.Position).sqrMagnitude).First();
                        Assert.Greater(Vector3.Dot(site.Up, site.Position - nearest), 0f,
                            $"{name} I{i}: a plant would grow toward the racing line");
                    }
                }
        }

        [Test]
        public void Sowing_IsDeterministic_AndTheSeedMoves_It()
        {
            var gates = RegattaCourse.BuildGates(RegattaCourse.DefaultSeed, 2);
            var s = Make(3, 90f, 160f, 0f, 180f);
            var a = Sow(gates, s, 77);
            var b = Sow(gates, s, 77);
            var c = Sow(gates, s, 78);
            Assert.AreEqual(a.Count, b.Count);
            for (int k = 0; k < a.Count; k++)
            {
                Assert.AreEqual(a[k].Position, b[k].Position);
                Assert.AreEqual(a[k].Up, b[k].Up);
            }
            Assert.IsTrue(a.Count != c.Count || a.Zip(c, (x, y) => x.Position != y.Position).Any(d => d),
                "a different seed sowed the identical sites");
        }

        [Test]
        public void AnOffsetInsideTheRails_IsLiftedClear_NegativeControl()
        {
            var s = Make(3, 5f, 10f, 0f, 180f);
            Assert.AreEqual(LaneOffset + RegattaLifeformSites.MinRailClearance,
                RegattaLifeformSites.EffectiveOffsetMin(s, LaneOffset), 1e-4f);
            var gates = RegattaCourse.BuildGates(RegattaCourse.DefaultSeed, 4);
            var spine = RegattaLifeformSites.SpineSamples(gates, 64);
            foreach (var site in Sow(gates, s))
                Assert.GreaterOrEqual(spine.Min(p => (p - site.Position).magnitude),
                    LaneOffset + RegattaLifeformSites.MinRailClearance - 4f);
        }

        [Test]
        public void ParameterHash_MovesWithEveryField()
        {
            var a = Make(3, 90f, 160f, 0f, 180f);
            int h = a.ParameterHash();
            var b = Make(3, 90f, 160f, 0f, 180f);
            Assert.AreEqual(h, b.ParameterHash());
            b.faunaCorridorRadius = 100f;
            Assert.AreNotEqual(h, b.ParameterHash());
            var c = Make(4, 90f, 160f, 0f, 180f);
            Assert.AreNotEqual(h, c.ParameterHash());
        }
    }
}
#endif
