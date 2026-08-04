using CosmicShore.Utility;
using NUnit.Framework;
using UnityEngine;

namespace CosmicShore.Tests
{
    /// <summary>
    /// The spawn-ring contract: every player sits on a sphere of the requested radius around the
    /// cell centre, faces the centre, and the arrangement is symmetric for the counts that have a
    /// named symmetry (2 = one axis, 3 = equilateral triangle, 4 = tetrahedron).
    /// </summary>
    public class CellSpawnFormationTests
    {
        static readonly Vector3 Center = new(10f, -5f, 3f);
        const float Radius = 140f;
        const float Tolerance = 1e-3f;

        static Pose[] Build(int count) => CellSpawnFormation.Build(count, Center, Radius);

        [Test]
        public void Build_PlacesEveryPlayerOnTheRequestedSphere([Values(1, 2, 3, 4, 5, 8, 12)] int count)
        {
            foreach (var pose in Build(count))
                Assert.AreEqual(Radius, Vector3.Distance(pose.position, Center), Tolerance,
                    $"{count}-player formation put a spawn off the sphere.");
        }

        [Test]
        public void Build_OrientsEveryPlayerTowardTheCenter([Values(1, 2, 3, 4, 5, 8, 12)] int count)
        {
            foreach (var pose in Build(count))
            {
                Vector3 toCenter = (Center - pose.position).normalized;
                Assert.AreEqual(1f, Vector3.Dot(pose.rotation * Vector3.forward, toCenter), Tolerance,
                    $"{count}-player formation produced a spawn not facing the cell centre.");
            }
        }

        [Test]
        public void Build_ReturnsOnePosePerPlayer([Values(1, 2, 3, 4, 5, 12)] int count)
        {
            Assert.AreEqual(count, Build(count).Length);
        }

        [Test]
        public void Build_ClampsNonPositiveCountToOne([Values(0, -3)] int count)
        {
            Assert.AreEqual(1, CellSpawnFormation.Build(count, Center, Radius).Length);
        }

        [Test]
        public void TwoPlayers_ShareOneAxisThroughTheCenter()
        {
            var poses = Build(2);

            // Antipodal: the two offsets are exact negatives, so the segment between them
            // passes through the centre.
            Vector3 a = poses[0].position - Center;
            Vector3 b = poses[1].position - Center;
            Assert.AreEqual(-1f, Vector3.Dot(a.normalized, b.normalized), Tolerance);
            Assert.AreEqual(2f * Radius, Vector3.Distance(poses[0].position, poses[1].position), Tolerance);
        }

        [Test]
        public void ThreePlayers_FormAnEquilateralTriangle()
        {
            var poses = Build(3);

            float ab = Vector3.Distance(poses[0].position, poses[1].position);
            float bc = Vector3.Distance(poses[1].position, poses[2].position);
            float ca = Vector3.Distance(poses[2].position, poses[0].position);

            Assert.AreEqual(ab, bc, Tolerance);
            Assert.AreEqual(bc, ca, Tolerance);

            // A great-circle equilateral triangle has side radius * sqrt(3).
            Assert.AreEqual(Radius * Mathf.Sqrt(3f), ab, 1e-2f);
        }

        [Test]
        public void ThreePlayers_AreCoplanarWithTheCenter()
        {
            var poses = Build(3);
            Vector3 centroid = (poses[0].position + poses[1].position + poses[2].position) / 3f;
            Assert.AreEqual(0f, Vector3.Distance(centroid, Center), Tolerance,
                "An equilateral triangle on a great circle must be centred on the cell.");
        }

        [Test]
        public void FourPlayers_FormARegularTetrahedron()
        {
            var poses = Build(4);

            // Every one of the 6 edges is the same length - the definition of tetrahedral symmetry.
            float expected = Vector3.Distance(poses[0].position, poses[1].position);
            for (int i = 0; i < 4; i++)
            for (int j = i + 1; j < 4; j++)
                Assert.AreEqual(expected, Vector3.Distance(poses[i].position, poses[j].position), 1e-2f,
                    $"Edge {i}-{j} breaks tetrahedral symmetry.");

            // Regular tetrahedron inscribed in a sphere: edge = radius * sqrt(8/3).
            Assert.AreEqual(Radius * Mathf.Sqrt(8f / 3f), expected, 1e-2f);
        }

        [Test]
        public void Build_IsDeterministic()
        {
            var first = Build(4);
            var second = Build(4);

            for (int i = 0; i < first.Length; i++)
                Assert.AreEqual(0f, Vector3.Distance(first[i].position, second[i].position), Tolerance,
                    "Spawn slots must be stable - clients and server derive the same ring.");
        }

        [Test]
        public void ManyPlayers_AreSpreadOverTheWholeSphere()
        {
            var poses = Build(12);

            // A Fibonacci sphere has a centroid at (near) the centre - no hemisphere clumping.
            Vector3 sum = Vector3.zero;
            foreach (var pose in poses) sum += (pose.position - Center).normalized;
            Assert.Less(sum.magnitude / poses.Length, 0.1f,
                "12-player formation clumped to one side of the cell.");
        }
    }
}
