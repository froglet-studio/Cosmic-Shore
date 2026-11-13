#if UNITY_EDITOR
using CosmicShore.Gameplay;
using NUnit.Framework;
using UnityEngine;

namespace CosmicShore.Tests
{
    /// <summary>
    /// The petal theft cue's pure half (Docs/ElementalAbilitySystem/PETAL_THEFT_CUE.md): a hit draws
    /// shards in proportion to what it is worth, a shard reaches a MOVING attacker within its travel
    /// time, a silenced hull shows fewer shards as its silence drains, and the shard is a faceted
    /// volume, never a sticker - each with a control that fails.
    /// </summary>
    public class PetalTheftShapesTests
    {
        [Test]
        public void ShardCount_ScalesWithWorth_AndClamps()
        {
            Assert.AreEqual(1, PetalTheftShapes.ShardCount(0.4f, 1.5f, 1, 18), "a bullet (0.4 petal) still shows one petal leaving");
            Assert.AreEqual(6, PetalTheftShapes.ShardCount(4f, 1.5f, 1, 18), "a shockwave (4 petals) draws six");
            Assert.AreEqual(18, PetalTheftShapes.ShardCount(12f, 1.5f, 1, 18), "a direct missile (12 petals) is clamped to the cap");
            Assert.AreEqual(0, PetalTheftShapes.ShardCount(0f, 1.5f, 1, 18), "a worthless hit draws nothing");
            Assert.Less(PetalTheftShapes.ShardCount(0.4f, 1.5f, 1, 18), PetalTheftShapes.ShardCount(4f, 1.5f, 1, 18),
                "a bigger hit draws more");
        }

        [Test]
        public void ShardSize_FollowsTheHull_WithinReadableBounds()
        {
            Assert.AreEqual(1.1f, PetalTheftShapes.ShardSize(3f, 0.16f, 1.1f, 7f), 1e-5f, "a tiny hull's shards keep a readable floor");
            Assert.AreEqual(3.2f, PetalTheftShapes.ShardSize(20f, 0.16f, 1.1f, 7f), 1e-5f, "a mid hull's shards are a share of it");
            Assert.AreEqual(7f, PetalTheftShapes.ShardSize(130f, 0.16f, 1.1f, 7f), 1e-5f, "a Shark's shards stop at the ceiling");
        }

        [Test]
        public void Stream_CatchesAMovingAttacker_InItsTravelTime()
        {
            // An attacker 150 u away flying sideways at 120 u/s (boosting), a shard launched the other way.
            float travel = PetalTheftShapes.TravelSeconds(150f, 240f, 0.55f, 1.4f);
            Assert.That(travel, Is.InRange(0.55f, 1.4f));

            Assert.IsTrue(Fly(steer: 7f, travel), "a steered shard reaches the attacker inside its travel time (+0.5 s grace)");
            Assert.IsFalse(Fly(steer: 0f, travel), "control: an unsteered shard never arrives");
        }

        static bool Fly(float steer, float travel)
        {
            const float dt = 1f / 60f;
            var target = new Vector3(150f, 0f, 0f);
            var targetVelocity = new Vector3(0f, 0f, 120f);
            var position = Vector3.zero;
            var velocity = new Vector3(-20f, 10f, 0f);
            for (float t = 0f; t < travel + 0.5f; t += dt)
            {
                target += targetVelocity * dt;
                var desired = PetalTheftShapes.DesiredVelocity(position, target, targetVelocity, travel - t);
                velocity = PetalTheftShapes.Steer(velocity, desired, steer, dt);
                position += velocity * dt;
                if ((target - position).sqrMagnitude <= 7f * 7f) return true;
            }
            return false;
        }

        [Test]
        public void SilenceShards_DrainWithTheSilence()
        {
            Assert.AreEqual(6, PetalTheftShapes.SilenceShardsShown(8f, 8f, 6), "the ceiling shows every shard");
            Assert.AreEqual(1, PetalTheftShapes.SilenceShardsShown(0.4f, 8f, 6), "a bullet's 0.4 s still shows one");
            Assert.AreEqual(3, PetalTheftShapes.SilenceShardsShown(4f, 8f, 6), "half the ceiling shows half");
            Assert.AreEqual(0, PetalTheftShapes.SilenceShardsShown(0f, 8f, 6), "no silence, no shards");
        }

        [Test]
        public void FlashStrength_HasAFloor_AndIsFullAtAShockwave()
        {
            Assert.AreEqual(0.3f, PetalTheftShapes.FlashStrength(0.4f, 0.3f), 1e-5f);
            Assert.AreEqual(1f, PetalTheftShapes.FlashStrength(4f, 0.3f), 1e-5f);
            Assert.AreEqual(1f, PetalTheftShapes.FlashStrength(12f, 0.3f), 1e-5f);
            Assert.AreEqual(0f, PetalTheftShapes.FlashStrength(0f, 0.3f), 1e-5f);
        }

        [Test]
        public void Shard_IsAFacetedVolume_NotAFlatSheet()
        {
            var mesh = PetalTheftShapes.BuildShardMesh();
            try
            {
                Assert.IsTrue(PetalTheftShapes.IsVolume(mesh), "the shard is a volume (Nothing in the world looks flat, LOCKED)");
                Assert.AreEqual(mesh.vertexCount, mesh.triangles.Length, "flat-shaded: every facet has its own vertices");

                // Every facet faces OUT of the solid, or the renderer culls it and the shard draws hollow.
                var verts = mesh.vertices;
                var normals = mesh.normals;
                var centre = mesh.bounds.center;
                for (int i = 0; i < verts.Length; i += 3)
                {
                    var c = (verts[i] + verts[i + 1] + verts[i + 2]) / 3f;
                    Assert.Greater(Vector3.Dot(normals[i], c - centre), 0f, $"facet {i / 3} faces outward");
                }

                // Control: flatten it onto its own plane and the same predicate must fail.
                var flat = Object.Instantiate(mesh);
                var v = flat.vertices;
                for (int i = 0; i < v.Length; i++) v[i].y = 0f;
                flat.vertices = v;
                flat.RecalculateNormals();
                flat.RecalculateBounds();
                Assert.IsFalse(PetalTheftShapes.IsVolume(flat), "control: the flattened shard is a sheet and fails");
                Object.DestroyImmediate(flat);
            }
            finally
            {
                Object.DestroyImmediate(mesh);
            }
        }
    }
}
#endif
