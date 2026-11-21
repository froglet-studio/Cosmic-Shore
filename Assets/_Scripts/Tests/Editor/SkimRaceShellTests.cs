#if UNITY_EDITOR
using CosmicShore.Gameplay;
using CosmicShore.Utility;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine;

namespace CosmicShore.Tests
{
    /// <summary>
    /// <see cref="SkimRaceShell.StellaDistance"/> must be the EXACT distance to a super-shielded track
    /// prism's stella, because the Skim Race pilot's guards and the offline simulator both treat it as
    /// the contact test. The landmark cases use the track plate's real shell (half-extents
    /// 15 x 1.5 x 4.5) and points whose distance follows from the bounding box alone; the
    /// cross-check holds the pilot's geometry in agreement with the game's own shell tier
    /// (<see cref="ShieldShellMath"/>).
    /// </summary>
    public class SkimRaceShellTests
    {
        static readonly Vector3 Plate = new(15f, 1.5f, 4.5f);

        [Test]
        public void AboveTheFace_IsTheHeightAboveTheShell()
        {
            // Every stella point has |y| <= 1.5 and (0, 1.5, 0) lies on tetrahedron A's edge.
            foreach (float d in new[] { 0.25f, 1f, 3f, 8f })
                Assert.AreEqual(d, SkimRaceShell.StellaDistance(new Vector3(0f, 1.5f + d, 0f), Plate), 1e-3f, $"d={d}");
        }

        [Test]
        public void BesideTheLongEdge_IsTheLateralGap()
        {
            // Every stella point has |x| <= 15 and (15, 0, 0) lies on A's edge (15,1.5,4.5)-(15,-1.5,-4.5).
            foreach (float d in new[] { 0.5f, 2f, 6f, 11f })
                Assert.AreEqual(d, SkimRaceShell.StellaDistance(new Vector3(15f + d, 0f, 0f), Plate), 1e-3f, $"d={d}");
        }

        [Test]
        public void BeyondASpikeTip_IsTheDistanceToTheCorner()
        {
            var off = new Vector3(2f, 1f, 3f);
            Assert.AreEqual(off.magnitude, SkimRaceShell.StellaDistance(Plate + off, Plate), 1e-3f);
            // Tetrahedron B's tip on the opposite corner.
            Assert.AreEqual(off.magnitude, SkimRaceShell.StellaDistance(new Vector3(-15f, -1.5f, 4.5f) + new Vector3(-2f, -1f, 3f), Plate), 1e-3f);
        }

        [Test]
        public void Inside_IsZero()
        {
            Assert.AreEqual(0f, SkimRaceShell.StellaDistance(Vector3.zero, Plate));
            Assert.AreEqual(0f, SkimRaceShell.StellaDistance(new Vector3(7f, 0.3f, 1f), Plate));
        }

        [Test]
        public void NegativeControl_TheRetiredFacePlaneBoundFailsBesideTheEdge()
        {
            const float d = 11f;
            float old = SkimRaceShell.FacePlaneBound(new Vector3(15f + d, 0f, 0f), Plate);
            Assert.Less(old, 0.2f * d, "the face-plane bound under-reports beside the edge (~d/10.6)");
            Assert.AreEqual(d, SkimRaceShell.StellaDistance(new Vector3(15f + d, 0f, 0f), Plate), 1e-3f);
        }

        [Test]
        public void FacePlaneBound_NeverExceedsTheExactDistance()
        {
            var rng = new System.Random(5);
            for (int i = 0; i < 2000; i++)
            {
                var p = new Vector3(R(rng, 40f), R(rng, 12f), R(rng, 20f));
                Assert.LessOrEqual(SkimRaceShell.FacePlaneBound(p, Plate), SkimRaceShell.StellaDistance(p, Plate) + 1e-3f, p.ToString());
            }
        }

        [Test]
        public void AgreesWithTheGamesShellTier()
        {
            var rng = new System.Random(11);
            int checkedCases = 0;
            for (int i = 0; i < 3000; i++)
            {
                var half = new Vector3(1f + (float)rng.NextDouble() * 20f, 0.5f + (float)rng.NextDouble() * 5f, 1f + (float)rng.NextDouble() * 10f);
                var p = new Vector3(R(rng, half.x * 2.5f), R(rng, half.y * 4f), R(rng, half.z * 2.5f));
                float d = SkimRaceShell.StellaDistance(p, half);
                var frame = ShieldShellMath.CreateFrame(float3.zero, quaternion.identity, new float3(half.x, half.y, half.z));
                var c = new float3(p.x, p.y, p.z);
                if (d <= 0f)
                {
                    Assert.IsTrue(ShieldShellMath.SphereOverlapsStella(frame, c, 1e-4f), $"inside disagreement at {p} half {half}");
                    continue;
                }
                if (d < 0.05f) continue;
                Assert.IsTrue(ShieldShellMath.SphereOverlapsStella(frame, c, d * 1.01f + 1e-3f), $"reach disagreement at {p} half {half} d={d}");
                Assert.IsFalse(ShieldShellMath.SphereOverlapsStella(frame, c, d * 0.99f - 1e-3f), $"gap disagreement at {p} half {half} d={d}");
                checkedCases++;
            }
            Assert.Greater(checkedCases, 1000);
        }

        static float R(System.Random r, float span) => (float)(r.NextDouble() * 2.0 - 1.0) * span;
    }
}
#endif
