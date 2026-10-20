using CosmicShore.Gameplay;
using NUnit.Framework;
using UnityEngine;

namespace CosmicShore.Tests
{
    /// <summary>
    /// The clearance contract every generated lay list is held to (<see cref="PrismClearance"/>,
    /// Docs/ECOSYSTEM.md §62): after the pass no two laid prisms interpenetrate; a prism that can be
    /// shrunk clear is shrunk, not cleared; one that cannot is cleared IN ITS SLOT (scale zero) so
    /// range-indexed lay lists keep their indices; a higher priority is never the one that yields;
    /// and no axis is shrunk below the prefab's scale floor, which the lay would only grow back.
    ///
    /// <para>The whole-fleet half of the proof is offline: <c>Tools/Build/audit_prism_clipping.py
    /// --check</c> runs every shipped Spawnable through this pass and fails on any pair it leaves.
    /// These cases pin the behaviours that audit relies on, on inputs small enough to reason about.
    /// </para>
    /// </summary>
    public class PrismClearanceTests
    {
        static readonly Vector3 Rail = new Vector3(2f, 2f, 4f);

        /// <summary>Two oriented boxes, separated by the 15 axes: true when they interpenetrate.</summary>
        static bool Clip(SpawnPoint a, SpawnPoint b)
        {
            Vector3[] A = { a.Rotation * Vector3.right, a.Rotation * Vector3.up, a.Rotation * Vector3.forward };
            Vector3[] B = { b.Rotation * Vector3.right, b.Rotation * Vector3.up, b.Rotation * Vector3.forward };
            Vector3 ha = 0.5f * a.Scale, hb = 0.5f * b.Scale, d = b.Position - a.Position;
            float R(Vector3[] ax, Vector3 h, Vector3 l) =>
                Mathf.Abs(Vector3.Dot(ax[0], l)) * h.x + Mathf.Abs(Vector3.Dot(ax[1], l)) * h.y + Mathf.Abs(Vector3.Dot(ax[2], l)) * h.z;
            bool Separates(Vector3 l) => l.sqrMagnitude > 1e-10f && Mathf.Abs(Vector3.Dot(d, l)) > R(A, ha, l) + R(B, hb, l);
            for (int i = 0; i < 3; i++)
            {
                if (Separates(A[i]) || Separates(B[i])) return false;
                for (int j = 0; j < 3; j++)
                    if (Separates(Vector3.Cross(A[i], B[j]))) return false;
            }
            return true;
        }

        static void AssertNoClipping(SpawnPoint[] points)
        {
            for (int i = 0; i < points.Length; i++)
                for (int j = i + 1; j < points.Length; j++)
                {
                    if (PrismClearance.IsCleared(points[i]) || PrismClearance.IsCleared(points[j])) continue;
                    Assert.IsFalse(Clip(points[i], points[j]), $"prisms {i} and {j} still interpenetrate");
                }
        }

        [Test]
        public void ARailWhosePitchIsShorterThanItsPrismsIsShortenedNotCleared()
        {
            // 4-long prisms every 3 units along +z: each overlaps the next by 1.
            var points = new SpawnPoint[10];
            for (int i = 0; i < points.Length; i++)
                points[i] = new SpawnPoint(new Vector3(0f, 0f, 3f * i), Quaternion.identity, Rail);

            var result = PrismClearance.Resolve(points);

            AssertNoClipping(points);
            Assert.AreEqual(0, result.Cleared, "a short pitch is a length to trim, not a prism to lose");
            Assert.AreEqual(10, result.Shrunk, "the whole run is trimmed, the first prism with the rest");
            foreach (var p in points)
            {
                Assert.AreEqual(2f, p.Scale.x, 1e-4f, "the trim is along the rail; the cross-section is kept");
                Assert.AreEqual(2f, p.Scale.y, 1e-4f);
                Assert.LessOrEqual(p.Scale.z, 3f, "no longer than the pitch");
                Assert.AreEqual(points[0].Scale.z, p.Scale.z, 1e-5f,
                    "ONE length for the whole rail - a yield pass alone would leave it striped long-short-long");
            }
            Assert.Greater(points[0].Scale.z, 2.9f, "trimmed to the pitch, not past it");
        }

        [Test]
        public void ADuplicateIsClearedInItsSlot()
        {
            var a = new SpawnPoint(new Vector3(5f, 0f, 0f), Quaternion.identity, Rail);
            var points = new[] { a, new SpawnPoint(new Vector3(50f, 0f, 0f), Quaternion.identity, Rail), a };

            var result = PrismClearance.Resolve(points);

            Assert.AreEqual(1, result.Cleared);
            Assert.IsFalse(PrismClearance.IsCleared(points[0]), "the first lay of a pose keeps it");
            Assert.IsTrue(PrismClearance.IsCleared(points[2]), "the second is cleared...");
            Assert.AreEqual(a.Position, points[2].Position, "...in its own slot, pose kept, so ranges stay valid");
            Assert.AreEqual(Rail, points[1].Scale, "a prism nothing touches is left exactly as authored");
        }

        [Test]
        public void AHigherPriorityNeverYields()
        {
            var plain = new SpawnPoint(Vector3.zero, Quaternion.identity, Rail);
            var danger = new SpawnPoint(new Vector3(0f, 0f, 1f), Quaternion.identity, Rail);
            var points = new[] { plain, danger };

            PrismClearance.Resolve(points, priority: new[] { 0, 1 });

            Assert.AreEqual(Rail, points[1].Scale, "the gameplay prism keeps its authored size although it was laid second");
            AssertNoClipping(points);
        }

        [Test]
        public void NoAxisIsShrunkBelowTheScaleFloor()
        {
            var floor = new Vector3(0.5f, 0.5f, 0.5f);
            var points = new SpawnPoint[6];
            for (int i = 0; i < points.Length; i++)
                points[i] = new SpawnPoint(new Vector3(0f, 0f, 0.6f * i), Quaternion.identity, new Vector3(1f, 1f, 1f));

            PrismClearance.Resolve(points, floor, new Vector3(100f, 100f, 100f));

            foreach (var p in points)
            {
                if (PrismClearance.IsCleared(p)) continue;
                Assert.GreaterOrEqual(p.Scale.x, floor.x - 1e-5f);
                Assert.GreaterOrEqual(p.Scale.y, floor.y - 1e-5f);
                Assert.GreaterOrEqual(p.Scale.z, floor.z - 1e-5f, "a size the clamp would grow back is not a fix");
            }
            AssertNoClipping(points);
        }

        [Test]
        public void CrossingRailsClearEachOther()
        {
            // Two rails crossing at right angles through one another, plus a third at 45 degrees.
            var points = new SpawnPoint[63];
            var turns = new[] { Quaternion.identity, Quaternion.Euler(0f, 90f, 0f), Quaternion.Euler(0f, 45f, 0f) };
            for (int r = 0; r < 3; r++)
                for (int i = 0; i < 21; i++)
                {
                    var along = turns[r] * Vector3.forward;
                    points[r * 21 + i] = new SpawnPoint(along * (3.5f * (i - 10)), turns[r], Rail);
                }

            PrismClearance.Resolve(points);

            AssertNoClipping(points);
        }

        [Test]
        public void TheSameInputResolvesTheSameWay()
        {
            SpawnPoint[] Make()
            {
                var rng = new System.Random(7);
                var p = new SpawnPoint[200];
                for (int i = 0; i < p.Length; i++)
                    p[i] = new SpawnPoint(
                        new Vector3((float)rng.NextDouble() * 30f, (float)rng.NextDouble() * 30f, (float)rng.NextDouble() * 30f),
                        Quaternion.Euler((float)rng.NextDouble() * 360f, (float)rng.NextDouble() * 360f, 0f),
                        new Vector3(1f + (float)rng.NextDouble() * 3f, 1f, 2f + (float)rng.NextDouble() * 6f));
                return p;
            }

            var a = Make();
            var b = Make();
            PrismClearance.Resolve(a);
            PrismClearance.Resolve(b);

            AssertNoClipping(a);
            for (int i = 0; i < a.Length; i++)
                Assert.AreEqual(a[i].Scale, b[i].Scale, "clients build environments locally - the pass must be deterministic");
        }
    }
}
