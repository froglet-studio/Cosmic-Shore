using System.Collections.Generic;
using CosmicShore.Gameplay;
using NUnit.Framework;
using UnityEngine;

namespace CosmicShore.Tests
{
    /// <summary>
    /// The silence perimeter's layout and shard geometry (Docs/ElementalAbilitySystem/SILENCE.md §5). The lab's
    /// JavaScript twin is held to the same code by Tools/Build/silence_vignette_parity; these pin what the
    /// perimeter must DO: nothing drawn until a silence lands; while it holds, every shard's centre on the
    /// screen's edge (half of it beyond) and none in the middle; a hit sinks them inward; the shards on the
    /// pull's side are the longest (the maw); and every shard a closed, outward-wound, solid volume.
    /// </summary>
    public class SilencePerimeterTests
    {
        const float Wide = 16f / 9f;
        const float Fov = 60f;

        static SilencePerimeter.Settings Shipped(int shape = SilencePerimeter.ShapeCrystal) => new()
        {
            Shape = shape, Count = 110, Seed = 7, Size = 0.34f, Reach = 0.12f, Spin = 0.35f, PullDrift = 0.12f,
        };

        // The shard's centre on screen, -1..1 on both axes (0 = the middle, |1| = the screen's edge).
        static Vector2 Ndc(in SilencePerimeter.Pose p)
        {
            float h = p.Position.z * Mathf.Tan(Fov * 0.5f * Mathf.Deg2Rad);
            return new Vector2(p.Position.x / (h * Wide), p.Position.y / h);
        }

        static float EdgeDistance(Vector2 ndc) => Mathf.Max(Mathf.Abs(ndc.x), Mathf.Abs(ndc.y));

        [Test]
        public void WithNoSilenceNoShardIsDrawn()
        {
            var s = Shipped();
            for (int i = 0; i < s.Count; i++)
                Assert.AreEqual(0f, SilencePerimeter.Layout(i, s, 0f, 0f, 0f, Wide, Fov).Presence, "shard " + i);
        }

        // Garrett, 2026-10-21: "show only half the faceted object that is poking into the screen".
        [Test]
        public void AtFullSilenceEveryShardSitsHalfBuriedOnTheEdge()
        {
            var s = Shipped();
            for (int i = 0; i < s.Count; i++)
            {
                var p = SilencePerimeter.Layout(i, s, 1f, 0f, 0f, Wide, Fov);
                Assert.AreEqual(1f, p.Presence, 1e-5f, "shard " + i);
                float e = EdgeDistance(Ndc(p));
                Assert.Less(e, 1.08f, "shard " + i + "'s centre is beyond the edge: none of it pokes in");
                Assert.Greater(e, 0.6f, "shard " + i + " sits in the middle of the screen, not on its edge");
            }
        }

        [Test]
        public void TheShardsOnThePullsSideAreTheLongest()
        {
            var s = Shipped();
            float pull = SilencePerimeter.PullAngle(s, 0f);
            float near = 0f, far = 0f;
            int nearCount = 0, farCount = 0;
            for (int i = 0; i < s.Count; i++)
            {
                var p = SilencePerimeter.Layout(i, s, 1f, 0f, 0f, Wide, Fov);
                var ndc = Ndc(p);
                float closeness = Mathf.Cos(Mathf.Atan2(ndc.y, ndc.x) - pull);
                float length = p.Up.magnitude / p.Position.z;
                if (closeness > 0.7f) { near += length; nearCount++; }
                else if (closeness < -0.7f) { far += length; farCount++; }
            }
            Assert.Greater(nearCount, 0);
            Assert.Greater(farCount, 0);
            Assert.Greater(near / nearCount, 1.5f * (far / farCount), "the maw does not follow the pull");
        }

        [Test]
        public void AHitSinksTheShardsInward()
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
