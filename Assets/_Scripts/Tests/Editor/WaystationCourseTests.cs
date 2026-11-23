using CosmicShore.Gameplay;
using NUnit.Framework;
using UnityEngine;

namespace CosmicShore.Tests
{
    /// <summary>
    /// Waystation's exit gates face SQUARE to the next cluster (Garrett, 2026-10-29: "the last ring in a cluster is
    /// oriented so you can keep your flow by dropping to the next cluster"): thread the gate and the next cluster is
    /// abeam, under the belly once the hull rolls, so the Tiger Moth's Drop falls at it. The exact verdicts (square to
    /// the cluster's centre within half a degree, the twist, the corners, the shell) are
    /// <c>Tools/Build/waystation_course.py --check</c>, which mirrors <see cref="WaystationCourse"/> and compiles the
    /// shipped file to compare gate for gate; this pins the property on the shipped C# with what it outputs: the next
    /// cluster's FIRST RING, which sits a coil radius off the centre, is near abeam of every exit gate.
    /// </summary>
    [TestFixture]
    public class WaystationCourseTests
    {
        static WaystationCourseSettings Settings(int intensity)
        {
            var s = WaystationCourseSettings.ForIntensity(intensity);
            s.RingTarget = 24;
            s.InnerRadius = 478f;
            s.OuterRadius = 1080f;
            return s;
        }

        [Test]
        public void EveryExitGate_HasTheNextClusterAbeam()
        {
            for (int intensity = 1; intensity <= 4; intensity++)
            {
                var s = Settings(intensity);
                for (int seed = 1; seed <= 40; seed++)
                {
                    var gates = WaystationCourse.Generate(seed, s);
                    int per = s.RingsPerCluster;
                    for (int exit = per - 1; exit + 1 < gates.Count; exit += per)
                    {
                        var gate = gates[exit];
                        float off = Vector3.Angle(gate.Axis, gates[exit + 1].Position - gate.Position);
                        // square to the centre exactly; the first ring is up to a coil radius (160) off it at 500+ u
                        Assert.AreEqual(90f, off, 25f, $"i{intensity} seed {seed} exit {exit}: the next cluster is not abeam");
                    }
                }
            }
        }

        [Test]
        public void TheCourseIsTheSameForTheSameSeed()
        {
            var a = WaystationCourse.Generate(7, Settings(3));
            var b = WaystationCourse.Generate(7, Settings(3));
            Assert.AreEqual(a.Count, b.Count);
            for (int i = 0; i < a.Count; i++)
            {
                Assert.AreEqual(a[i].Position, b[i].Position);
                Assert.AreEqual(a[i].Axis, b[i].Axis);
            }
        }
    }
}
