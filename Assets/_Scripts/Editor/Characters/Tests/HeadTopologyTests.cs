using CosmicShore.Gameplay;
using NUnit.Framework;
using UnityEngine;

namespace CosmicShore.Tests
{
    /// <summary>Integer settings decide topology; axes move vertices and never change counts.</summary>
    public class HeadTopologyTests
    {
        static HeadShape RandomShape(ref CharacterRandom rng, float amplitude)
        {
            var s = HeadShape.Neutral;
            for (int i = 0; i < HeadShape.AxisCount; i++) s[(HeadAxis)i] = rng.Range(-amplitude, amplitude);
            return s;
        }

        [Test]
        public void ShapeNeverChangesTopology()
        {
            foreach (var detail in new[] { HeadDetail.Runtime, HeadDetail.Portrait })
            {
                var head = new ProceduralBaseHead(detail);
                var topo = head.Topology;
                Assert.AreEqual((detail.Rings + 1) * (detail.Segments + 1), topo.VertexCount);
                Assert.AreEqual(detail.Rings * detail.Segments * 6, topo.Triangles.Length);
                var rng = new CharacterRandom(1);
                for (int i = 0; i < 40; i++)
                {
                    var verts = head.Evaluate(RandomShape(ref rng, 1.3f));
                    Assert.AreEqual(topo.VertexCount, verts.Length);
                }
                foreach (var extreme in new[] { -1f, 1f })
                {
                    var s = HeadShape.Neutral;
                    for (int i = 0; i < HeadShape.AxisCount; i++) s[(HeadAxis)i] = extreme;
                    Assert.AreEqual(topo.VertexCount, head.Evaluate(s).Length);
                }
            }
        }

        [Test]
        public void EveryAxisMovesSomething()
        {
            var head = new ProceduralBaseHead(HeadDetail.Runtime);
            var neutral = head.Evaluate(HeadShape.Neutral);
            for (int i = 0; i < HeadShape.AxisCount; i++)
            {
                var axis = (HeadAxis)i;
                var moved = head.Evaluate(HeadShape.Neutral.With(axis, 1f));
                float maxDelta = 0f;
                for (int v = 0; v < neutral.Length; v++) maxDelta = Mathf.Max(maxDelta, (moved[v] - neutral[v]).magnitude);
                Assert.Greater(maxDelta, 1e-3f, $"{axis} at +1 moves no vertex");
            }
        }

        [Test]
        public void ProceduralHeadIsBoundedAndHeadSized()
        {
            var head = new ProceduralBaseHead(HeadDetail.Runtime);
            var verts = head.Evaluate(HeadShape.Neutral);
            float top = float.MinValue, chinish = float.MaxValue, width = 0f;
            foreach (var v in verts) { top = Mathf.Max(top, v.y); width = Mathf.Max(width, Mathf.Abs(v.x)); if (v.z > 0.2f) chinish = Mathf.Min(chinish, v.y); }
            Assert.That(top, Is.InRange(0.40f, 0.60f), "crown height");
            Assert.That(width, Is.InRange(0.30f, 0.60f), "half width (incl. collar)");
            Assert.Less(chinish, -0.35f, "the face front reaches down to a chin");
        }

        [Test]
        public void SurfaceSamplerAgreesWithTheMesh()
        {
            var head = new ProceduralBaseHead(HeadDetail.Portrait);
            var shape = HeadShape.Neutral.With(HeadAxis.MuzzleLength, 0.6f);
            var surface = head.Surface(shape);
            foreach (var spec in head.Sites)
            {
                var dir = head.SiteDirection(spec, shape);
                var p = surface.Sample(dir);
                Assert.IsFalse(float.IsNaN(p.x) || float.IsNaN(p.y) || float.IsNaN(p.z), spec.Name);
                Assert.That(p.magnitude, Is.InRange(0.2f, 1.2f), spec.Name);
                var uv = surface.Uv(dir);
                var back = surface.Direction(uv);
                Assert.Greater(Vector3.Dot(back.normalized, dir.normalized), 0.999f, $"{spec.Name}: UV ↔ direction round trip");
            }
        }
    }
}
