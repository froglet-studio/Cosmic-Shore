using System.Collections.Generic;
using CosmicShore.Gameplay;
using NUnit.Framework;
using UnityEngine;

namespace CosmicShore.Tests
{
    /// <summary>
    /// The silence perimeter's layout and shard geometry (Docs/ElementalAbilitySystem/SILENCE.md §5). The lab's
    /// JavaScript twin is held to the same code by Tools/Build/silence_vignette_parity; these pin what the
    /// perimeter must DO: hidden beyond the edge until a silence lands, in on the edge (and out of the middle)
    /// while it holds, jolted inward by a hit, and every shard a closed, outward-wound, solid volume.
    /// </summary>
    public class SilencePerimeterTests
    {
        const float Wide = 16f / 9f;
        const float Fov = 60f;

        static SilencePerimeter.Settings Shipped(int shape = SilencePerimeter.ShapeCrystal) => new()
        {
            Shape = shape, Count = 22, Seed = 7, Size = 0.26f, Reach = 0.26f, Spin = 0.5f,
        };

        // The shard's centre on screen, -1..1 on both axes (0 = the middle, |1| = the screen's edge).
        static Vector2 Ndc(in SilencePerimeter.Pose p)
        {
            float h = p.Position.z * Mathf.Tan(Fov * 0.5f * Mathf.Deg2Rad);
            return new Vector2(p.Position.x / (h * Wide), p.Position.y / h);
        }

        static float EdgeDistance(Vector2 ndc) => Mathf.Max(Mathf.Abs(ndc.x), Mathf.Abs(ndc.y));

        [Test]
        public void WithNoSilenceEveryShardWaitsBeyondTheScreenEdge()
        {
            var s = Shipped();
            for (int i = 0; i < s.Count; i++)
            {
                var p = SilencePerimeter.Layout(i, s, 0f, 0f, 0f, Wide, Fov);
                Assert.AreEqual(0f, p.Presence, "shard " + i);
                Assert.GreaterOrEqual(EdgeDistance(Ndc(p)), 1f, "shard " + i + "'s centre is on screen with no silence");
            }
        }

        [Test]
        public void AtFullSilenceEveryShardIsInOnTheEdgeAndOutOfTheMiddle()
        {
            var s = Shipped();
            for (int i = 0; i < s.Count; i++)
            {
                var p = SilencePerimeter.Layout(i, s, 1f, 0f, 0f, Wide, Fov);
                Assert.AreEqual(1f, p.Presence, 1e-5f, "shard " + i);
                float e = EdgeDistance(Ndc(p));
                Assert.Less(e, 1f, "shard " + i + "'s centre is still off screen");
                Assert.Greater(e, 0.55f, "shard " + i + " sits in the middle of the screen");
            }
        }

        [Test]
        public void AHitJoltsTheShardsInward()
        {
            var s = Shipped();
            for (int i = 0; i < s.Count; i++)
            {
                float calm = EdgeDistance(Ndc(SilencePerimeter.Layout(i, s, 1f, 0f, 1f, Wide, Fov)));
                float hit = EdgeDistance(Ndc(SilencePerimeter.Layout(i, s, 1f, 1f, 1f, Wide, Fov)));
                Assert.Less(hit, calm, "shard " + i);
            }
        }

        [Test]
        public void TheArrangementIsStableAndTheSeedReshufflesIt()
        {
            var s = Shipped();
            var a = SilencePerimeter.Layout(3, s, 1f, 0f, 2f, Wide, Fov);
            var b = SilencePerimeter.Layout(3, s, 1f, 0f, 2f, Wide, Fov);
            Assert.AreEqual(a.Position, b.Position);
            s.Seed = 8;
            var c = SilencePerimeter.Layout(3, s, 1f, 0f, 2f, Wide, Fov);
            Assert.AreNotEqual(a.Position, c.Position);
            for (int k = 0; k < 200; k++)
            {
                float r = SilencePerimeter.Rand(7, k, k * 3);
                Assert.That(r, Is.GreaterThanOrEqualTo(0f).And.LessThan(1f));
            }
        }

        [Test]
        public void EveryShardIsAClosedVolumeWoundOutward([Values(SilencePerimeter.ShapeCrystal, SilencePerimeter.ShapeSlab)] int shape)
        {
            var s = Shipped(shape);
            var pos = new List<Vector3>(); var nor = new List<Vector3>(); var bar = new List<Vector3>();
            for (int i = 0; i < s.Count; i++)
            {
                pos.Clear(); nor.Clear(); bar.Clear();
                SilencePerimeter.Geometry(s, i, pos, nor, bar);
                Assert.AreEqual(0, pos.Count % 3);
                var edges = new Dictionary<string, int>();
                for (int t = 0; t < pos.Count; t += 3)
                {
                    Vector3 a = pos[t], b = pos[t + 1], c = pos[t + 2];
                    var n = Vector3.Cross(b - a, c - a);
                    Assert.Greater(Vector3.Dot(n, a + b + c), 0f, $"shard {i} triangle {t / 3} faces inward");
                    Assert.AreEqual(1f, nor[t].magnitude, 1e-4f);
                    for (int j = 0; j < 3; j++)
                    {
                        string ka = Key(pos[t + j]), kb = Key(pos[t + (j + 1) % 3]);
                        string key = string.CompareOrdinal(ka, kb) < 0 ? ka + "|" + kb : kb + "|" + ka;
                        edges[key] = edges.TryGetValue(key, out int count) ? count + 1 : 1;
                    }
                }
                foreach (var kv in edges)
                    Assert.AreEqual(2, kv.Value, $"shard {i}: edge {kv.Key} is not shared by exactly two facets (a hole)");
            }
        }

        // Docs/claude/SHADERS_AND_PERFORMANCE.md: nothing in the world looks flat. A shard stands well out of
        // every plane: thick on both cross axes, its facets facing many ways.
        [Test]
        public void AShardIsAVolume_NotAFlatSheet([Values(SilencePerimeter.ShapeCrystal, SilencePerimeter.ShapeSlab)] int shape)
        {
            var s = Shipped(shape);
            var pos = new List<Vector3>(); var nor = new List<Vector3>(); var bar = new List<Vector3>();
            SilencePerimeter.Geometry(s, 0, pos, nor, bar);
            Vector3 min = pos[0], max = pos[0];
            foreach (var p in pos) { min = Vector3.Min(min, p); max = Vector3.Max(max, p); }
            var size = max - min;
            Assert.Greater(size.x, 0.15f * size.y, "too thin across x");
            Assert.Greater(size.z, 0.15f * size.y, "too thin across z");
            var directions = new HashSet<string>();
            foreach (var n in nor) directions.Add(Key(n));
            Assert.GreaterOrEqual(directions.Count, 6, "the facets face too few ways to read as a solid");
        }

        [Test]
        public void TheShaderDeclaresItsPassesInTheOrderThePassDrawsThem()
        {
            string shader = System.IO.File.ReadAllText("Assets/_Graphics/Materials/Graphs/SilencePerimeter.shader");
            int warp = shader.IndexOf("Name \"SilencePerimeterWarp\"", System.StringComparison.Ordinal);
            int shard = shader.IndexOf("Name \"SilencePerimeter\"", System.StringComparison.Ordinal);
            Assert.GreaterOrEqual(warp, 0, "the warp pass is gone");
            Assert.GreaterOrEqual(shard, 0, "the shard pass is gone");
            Assert.AreEqual(SilencePerimeterPass.WarpPass == 0, warp < shard,
                "SilencePerimeterPass draws pass indices the shader no longer declares in that order");
        }

        static string Key(Vector3 v) =>
            Mathf.RoundToInt(v.x * 1e4f) + "," + Mathf.RoundToInt(v.y * 1e4f) + "," + Mathf.RoundToInt(v.z * 1e4f);
    }
}
