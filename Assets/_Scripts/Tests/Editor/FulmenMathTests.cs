using CosmicShore.Gameplay;
using NUnit.Framework;
using UnityEngine;
using Segment = CosmicShore.Gameplay.FulmenMath.Segment;

namespace CosmicShore.Tests
{
    /// <summary>
    /// FULMEN's bolt generator (<see cref="FulmenMath"/>): the same seed grows the same bolt, the caps hold, no point leaves
    /// the range sphere, and every stream is connected (a child starts where its parent ends).
    /// </summary>
    [TestFixture]
    public class FulmenMathTests
    {
        const float Tol = 1e-3f;
        static readonly Vector3 Origin = new(12f, -40f, 300f);

        static int Grow(in FulmenMath.Settings s, int seed, Segment[] buf) => FulmenMath.Generate(in s, seed, Origin, buf);

        [Test]
        public void TheSameSeedGrowsTheSameBolt()
        {
            var s = FulmenMath.Settings.Default(250f);
            var a = new Segment[256];
            var b = new Segment[256];
            int na = Grow(in s, 1234, a), nb = Grow(in s, 1234, b);
            Assert.Greater(na, 0);
            Assert.AreEqual(na, nb);
            for (int i = 0; i < na; i++)
            {
                Assert.AreEqual(a[i].Start, b[i].Start, $"start {i}");
                Assert.AreEqual(a[i].End, b[i].End, $"end {i}");
                Assert.AreEqual(a[i].Parent, b[i].Parent, $"parent {i}");
                Assert.AreEqual(a[i].Depth, b[i].Depth, $"depth {i}");
            }
        }

        [Test]
        public void ADifferentSeedGrowsADifferentBolt()
        {
            var s = FulmenMath.Settings.Default(250f);
            var a = new Segment[256];
            var b = new Segment[256];
            Grow(in s, 1, a);
            Grow(in s, 2, b);
            Assert.AreNotEqual(a[0].End, b[0].End);
        }

        [Test]
        public void TheCapsHold([Values(1, 7, 42, 999)] int seed)
        {
            var s = FulmenMath.Settings.Default(400f);
            s.MaxSegments = 60; s.MaxBranches = 9; s.MaxDepth = 1; s.ForkChance = 0.9f;
            var buf = new Segment[256];
            int n = Grow(in s, seed, buf);
            Assert.LessOrEqual(n, s.MaxSegments);
            int branches = 0;
            for (int i = 0; i < n; i++)
            {
                Assert.LessOrEqual(buf[i].Depth, s.MaxDepth, $"depth {i}");
                branches = Mathf.Max(branches, buf[i].Branch + 1);
            }
            Assert.LessOrEqual(branches, s.MaxBranches);
        }

        [Test]
        public void TheBufferCapsTheCount()
        {
            var s = FulmenMath.Settings.Default(400f);
            var buf = new Segment[10];
            Assert.LessOrEqual(Grow(in s, 5, buf), 10);
        }

        [Test]
        public void NoPointLeavesTheRange([Values(3, 77, 2024)] int seed)
        {
            var s = FulmenMath.Settings.Default(180f);
            var buf = new Segment[512];
            s.MaxSegments = 512;
            int n = Grow(in s, seed, buf);
            for (int i = 0; i < n; i++)
            {
                Assert.LessOrEqual((buf[i].Start - Origin).magnitude, s.Range + Tol, $"start {i}");
                Assert.LessOrEqual((buf[i].End - Origin).magnitude, s.Range + Tol, $"end {i}");
            }
        }

        [Test]
        public void EveryStreamIsConnected([Values(11, 12, 13)] int seed)
        {
            var s = FulmenMath.Settings.Default(300f);
            var buf = new Segment[256];
            int n = Grow(in s, seed, buf);
            for (int i = 0; i < n; i++)
            {
                var seg = buf[i];
                float len = (seg.End - seg.Start).magnitude;
                Assert.AreEqual(seg.StartDistance + len, seg.EndDistance, Tol, $"length {i}");
                if (seg.Parent < 0)
                {
                    Assert.AreEqual(0, seg.Depth);
                    Assert.Less((seg.Start - Origin).magnitude, Tol, $"root {i} starts at the centre");
                    Assert.AreEqual(0f, seg.StartDistance, Tol);
                    continue;
                }
                Assert.Less(seg.Parent, i, $"parent before child {i}");
                var parent = buf[seg.Parent];
                Assert.Less((parent.End - seg.Start).magnitude, Tol, $"segment {i} starts at its parent's end");
                Assert.AreEqual(parent.EndDistance, seg.StartDistance, Tol, $"distance {i}");
                Assert.That(seg.Depth == parent.Depth || seg.Depth == parent.Depth + 1, $"depth step {i}");
            }
        }

        [Test]
        public void StreamsReachTheSphere()
        {
            var s = FulmenMath.Settings.Default(200f);
            s.MaxSegments = 1024;
            var buf = new Segment[1024];
            int n = Grow(in s, 8, buf);
            float far = 0f;
            for (int i = 0; i < n; i++) far = Mathf.Max(far, (buf[i].End - Origin).magnitude);
            Assert.AreEqual(s.Range, far, 0.5f);
        }

        [Test]
        public void ANullOrEmptyBufferGrowsNothing()
        {
            var s = FulmenMath.Settings.Default(100f);
            Assert.AreEqual(0, FulmenMath.Generate(in s, 1, Origin, null));
            Assert.AreEqual(0, FulmenMath.Generate(in s, 1, Origin, new Segment[0]));
        }
    }
}
