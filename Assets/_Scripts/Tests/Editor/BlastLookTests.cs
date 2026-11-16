using System.Collections.Generic;
using CosmicShore.Gameplay;
using CosmicShore.Utility;
using NUnit.Framework;
using UnityEngine;

namespace CosmicShore.Tests
{
    /// <summary>
    /// The blast look (BLAST_LOOK.md): its meshes are closed faceted volumes (never a sheet), its debris flies
    /// the same path at any frame rate or slow-motion step, decelerates and HANGS where the look says, cools and
    /// shrinks away, and the shipped look still has something on screen two seconds after the release - the
    /// critique's bar ("a 0.25x clip of a Grizzly Time blast holds for two seconds with no flat shape in it").
    /// Tested against a fresh <see cref="BlastLookSO"/> (its field initializers are the shipped asset's values,
    /// which author_blast_look_assets.py --check holds).
    /// </summary>
    [TestFixture]
    public class BlastLookTests
    {
        BlastLookSO _look;

        [SetUp] public void SetUp() => _look = ScriptableObject.CreateInstance<BlastLookSO>();
        [TearDown] public void TearDown() => Object.DestroyImmediate(_look);

        // ── Meshes ───────────────────────────────────────────────────────────

        static void AssertClosedOutwardVolume(Vector3[] verts, int[] tris, string what)
        {
            var centre = Vector3.zero;
            foreach (var v in verts) centre += v;
            centre /= verts.Length;

            var edges = new Dictionary<(int, int), int>();
            for (int t = 0; t < tris.Length; t += 3)
            {
                Vector3 a = verts[tris[t]], b = verts[tris[t + 1]], c = verts[tris[t + 2]];
                var n = Vector3.Cross(b - a, c - a);
                Assert.Greater(n.magnitude, 1e-6f, $"{what}: a degenerate facet");
                Assert.Greater(Vector3.Dot(n, (a + b + c) / 3f - centre), 0f, $"{what}: facet {t / 3} is wound inward");
                for (int k = 0; k < 3; k++)
                {
                    int p = tris[t + k], q = tris[t + (k + 1) % 3];
                    var key = p < q ? (p, q) : (q, p);
                    edges[key] = edges.TryGetValue(key, out int count) ? count + 1 : 1;
                }
            }
            foreach (var kv in edges)
                Assert.AreEqual(2, kv.Value, $"{what}: edge {kv.Key} is not shared by exactly two facets - the volume is open (a sheet)");
        }

        [Test]
        public void CoreAndShellAreClosedIcospheresOfTheBuiltInSpheresSize()
        {
            foreach (var (mesh, facets) in new[] { (BlastLookMeshes.Core, 80), (BlastLookMeshes.Shell, 320) })
            {
                Assert.AreEqual(facets * 3, mesh.triangles.Length, $"{mesh.name}: facet count");
                var verts = mesh.vertices;
                var normals = mesh.normals;
                for (int i = 0; i < verts.Length; i++)
                {
                    Assert.AreEqual(0.5f, verts[i].magnitude, 1e-4f, $"{mesh.name}: radius 0.5, as Unity's sphere");
                    Assert.AreEqual(1f, Vector3.Dot(normals[i], verts[i].normalized), 1e-4f,
                        $"{mesh.name}: normals must be radial so the bomb's _Spread push keeps its size");
                }
                AssertClosedOutwardVolume(verts, mesh.triangles, mesh.name);
            }
        }

        [Test]
        public void ShardIsAClosedChunkNotASheet()
        {
            Assert.AreEqual(BlastLookMeshes.ShardVertexCount, BlastLookMeshes.ShardVertices.Length);
            Assert.AreEqual(BlastLookMeshes.ShardIndexCount, BlastLookMeshes.ShardTriangles.Length);
            AssertClosedOutwardVolume(BlastLookMeshes.ShardVertices, BlastLookMeshes.ShardTriangles, "shard");
        }

        [Test]
        public void SpikeBasesStayInsideTheFacetedCore()
        {
            // The faceted core's flat faces sit inside the 0.5 sphere; the spikes' base rings must sit inside
            // those faces, or swapping the bomb onto the icosphere shows a floating cone base.
            var core = BlastLookMeshes.Core;
            var verts = core.vertices;
            var tris = core.triangles;
            float inscribed = float.MaxValue;
            for (int t = 0; t < tris.Length; t += 3)
            {
                Vector3 a = verts[tris[t]], b = verts[tris[t + 1]], c = verts[tris[t + 2]];
                var n = Vector3.Cross(b - a, c - a).normalized;
                inscribed = Mathf.Min(inscribed, Vector3.Dot(n, a));
            }
            foreach (var v in GrizzlyBombVisual.SpikeMesh.vertices)
            {
                if (Mathf.Abs(v.magnitude - GrizzlyBombVisual.SpikeTipRadius) < 1e-4f) continue;
                Assert.Less(v.magnitude, inscribed, "a spike base pokes out of the faceted core");
            }
        }

        // ── Flight ───────────────────────────────────────────────────────────

        static BlastDebris OnePiece(BlastLookSO look, int index = 3) =>
            BlastLookMath.Seed(BlastLookMath.SeedFor(new Vector3(10f, -4f, 250f)), index, Vector3.zero, 100f, look.Shards);

        [Test]
        public void SlowMotionFliesTheSamePath()
        {
            var full = OnePiece(_look);
            var slow = full;
            for (int i = 0; i < 60; i++) BlastLookMath.Step(ref full, 1f / 60f);       // one second at 60 fps
            for (int i = 0; i < 480; i++) BlastLookMath.Step(ref slow, 1f / 480f);     // the director's slowmo 8
            Assert.AreEqual(0f, Vector3.Distance(full.Position, slow.Position), 1e-2f, "a slowed step lands elsewhere");
            Assert.AreEqual(full.Age, slow.Age, 1e-4f);
        }

        [Test]
        public void DragStopsAPieceAtItsReachAndItHangs()
        {
            const float radius = 100f;
            var spec = _look.Shards;
            for (int index = 0; index < 40; index++)
            {
                var d = BlastLookMath.Seed(77u, index, Vector3.zero, radius, spec);
                float launch = d.Velocity.magnitude;
                float reach = launch / spec.Drag;
                Assert.LessOrEqual(reach, radius * spec.ReachShare.y + 1e-3f, "a shard flies past the look's reach");
                Assert.GreaterOrEqual(reach, radius * spec.ReachShare.x - 1e-3f);
                for (int i = 0; i < 60; i++) BlastLookMath.Step(ref d, 1f / 60f);
                Assert.Less(d.Velocity.magnitude, launch * 0.1f, "after a second a shard should have all but stopped: it HANGS");
                Assert.AreEqual(BlastLookMath.DistanceAt(launch, spec.Drag, 1f), d.Position.magnitude, 0.05f + radius * 0.01f);
            }
        }

        [Test]
        public void TheSameBlastThrowsTheSameShards()
        {
            var a = OnePiece(_look, 7);
            var b = OnePiece(_look, 7);
            Assert.AreEqual(a.Velocity, b.Velocity);
            Assert.AreEqual(a.Size, b.Size);
            Assert.AreNotEqual(OnePiece(_look, 8).Velocity, a.Velocity, "two pieces of one blast must differ");
        }

        [Test]
        public void ShardsAreChunksOfEverySizeWithinTheClamp()
        {
            var spec = _look.Shards;
            float smallest = float.MaxValue, largest = 0f;
            for (int i = 0; i < 200; i++)
            {
                var d = BlastLookMath.Seed(5u, i, Vector3.zero, 100f, spec);
                Assert.GreaterOrEqual(d.Size.z, spec.SizeClamp.x - 1e-4f);
                Assert.LessOrEqual(d.Size.z, spec.SizeClamp.y + 1e-4f);
                Assert.GreaterOrEqual(Mathf.Min(d.Size.x, d.Size.y) / d.Size.z, 0.29f, "a shard this thin reads as a sheet");
                smallest = Mathf.Min(smallest, d.Size.z);
                largest = Mathf.Max(largest, d.Size.z);
            }
            Assert.Greater(largest / smallest, 3f, "one size of shard reads as a particle system, not a break");
        }

        // ── Heat, shrink, beats ──────────────────────────────────────────────

        [Test]
        public void HeatCoolsAndAPieceShrinksAwayAtTheEndOfItsLife()
        {
            Assert.AreEqual(1f, BlastLookMath.Heat(0f, 0.5f), 1e-5f);
            Assert.Less(BlastLookMath.Heat(1f, 0.5f), BlastLookMath.Heat(0.5f, 0.5f));
            Assert.AreEqual(1f, BlastLookMath.Shrink(0.5f, 3f, 0.35f));
            Assert.AreEqual(0f, BlastLookMath.Shrink(3f, 3f, 0.35f), 1e-5f);
            Assert.Less(BlastLookMath.Shrink(2.8f, 3f, 0.35f), BlastLookMath.Shrink(2.4f, 3f, 0.35f));
        }

        [Test]
        public void CoreFlashesOutHoldsAndCollapses()
        {
            float grow = _look.CoreGrowSeconds, hold = _look.CoreHoldSeconds, fade = _look.CoreFadeSeconds;
            Assert.AreEqual(0f, BlastLookMath.CoreScale(0f, grow, hold, fade), 1e-5f);
            Assert.AreEqual(1f, BlastLookMath.CoreScale(grow + hold * 0.5f, grow, hold, fade), 1e-5f);
            Assert.AreEqual(1f, BlastLookMath.CoreHeat(grow + hold * 0.5f, grow, hold, fade), 1e-5f, "white-hot through the hold");
            Assert.AreEqual(0f, BlastLookMath.CoreScale(grow + hold + fade, grow, hold, fade), 1e-5f);
            Assert.Less(BlastLookMath.CoreHeat(grow + hold + fade * 0.5f, grow, hold, fade), 0.5f, "it cools as it collapses");
        }

        [Test]
        public void TheShockFrontIsWhereTheSweepIs()
        {
            const float delay = 0.05f, duration = 1.2f;
            for (float t = 0f; t <= duration; t += 0.1f)
                Assert.AreEqual(SphereBlastMorphGeometry.EaseAt(t + delay, delay, duration),
                    BlastLookMath.ShellGrowth(t, duration), 1e-5f,
                    "the shell must ride AOEExplosion's own curve from the release");
        }

        [Test]
        public void SomethingIsStillOnScreenTwoSecondsAfterTheRelease()
        {
            // The critique's bar: a slowed blast holds for two seconds. Half the casing must still be there.
            const int pieces = 200;
            int alive = 0;
            var spec = _look.Shards;
            for (int i = 0; i < pieces; i++)
            {
                var d = BlastLookMath.Seed(11u, i, Vector3.zero, 100f, spec);
                for (int k = 0; k < 120; k++) BlastLookMath.Step(ref d, 1f / 60f);
                if (BlastLookMath.Alive(d) && BlastLookMath.Shrink(d.Age, d.Life, _look.ShrinkShare) > 0.5f) alive++;
            }
            Assert.Greater(alive, pieces / 2, "the aftermath is gone before a slow-motion shot can linger on it");
            Assert.Greater(_look.LongestLife(1.2f), 2f);
        }

        [Test]
        public void EmbersStreakWhileFastAndAreSmallerThanShards()
        {
            var embers = _look.Embers;
            var shards = _look.Shards;
            Assert.IsTrue(embers.Streaks);
            Assert.IsFalse(shards.Streaks);
            Assert.Less(embers.SizeClamp.y, shards.SizeClamp.y);
            Assert.Greater(_look.EmberStreakSeconds, 0f);
        }
    }
}
