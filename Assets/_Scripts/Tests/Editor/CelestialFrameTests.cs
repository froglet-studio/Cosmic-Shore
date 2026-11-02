using System;
using NUnit.Framework;
using CosmicShore.Gameplay;

namespace CosmicShore.Tests
{
    /// <summary>A celestial lifeform's carried up (the /fauna-creation skill, law L6; <see cref="CelestialFrame"/>): it stays
    /// unit and square to the heading through any turn, never flips at the poles, and never snaps to the world's +y.</summary>
    [TestFixture]
    public class CelestialFrameTests
    {
        static double Dot(double[] a, double[] b) => a[0] * b[0] + a[1] * b[1] + a[2] * b[2];
        static double Len(double[] a) => Math.Sqrt(Dot(a, a));

        static double[] Carry(double[] up, double[] from, double[] to)
        {
            CelestialFrame.CarryUp(up[0], up[1], up[2], from[0], from[1], from[2], to[0], to[1], to[2], out double x, out double y, out double z);
            return new[] { x, y, z };
        }

        static double[] Unit(double x, double y, double z)
        {
            double l = Math.Sqrt(x * x + y * y + z * z);
            return new[] { x / l, y / l, z / l };
        }

        [Test]
        public void TheCarriedUp_StaysUnitAndSquareToTheHeading_OnARandomFlight()
        {
            var rnd = new Random(7);
            var fwd = new[] { 0.0, 0.0, 1.0 };
            var up = new[] { 0.0, 1.0, 0.0 };
            for (int k = 0; k < 20000; k++)
            {
                var next = Unit(fwd[0] + (rnd.NextDouble() - 0.5) * 0.3, fwd[1] + (rnd.NextDouble() - 0.5) * 0.3, fwd[2] + (rnd.NextDouble() - 0.5) * 0.3);
                var nu = Carry(up, fwd, next);
                Assert.AreEqual(1.0, Len(nu), 1e-9, $"step {k}: the up is not unit");
                Assert.AreEqual(0.0, Dot(nu, next), 1e-9, $"step {k}: the up is not square to the heading");
                Assert.Greater(Dot(nu, up), 0.8, $"step {k}: the up jumped on a small turn");
                fwd = next;
                up = nu;
            }
        }

        [Test]
        public void ThroughThePoles_TheUpNeverFlips()
        {
            // climb straight up over the top and down the other side, in small steps: LookRotation(f, +y) flips here
            var up = new[] { 0.0, 1.0, 0.0 };
            var fwd = new[] { 0.0, 0.0, 1.0 };
            for (int k = 1; k <= 360; k++)
            {
                double a = k * Math.PI / 180.0;
                var next = new[] { 0.0, Math.Sin(a), Math.Cos(a) };
                var nu = Carry(up, fwd, next);
                Assert.Greater(Dot(nu, up), 0.99, $"degree {k}: the up flipped");
                Assert.AreEqual(0.0, Dot(nu, next), 1e-9);
                fwd = next;
                up = nu;
            }
            // a full loop in one plane brings the up back to where it began (no twist in a plane)
            Assert.AreEqual(1.0, up[1], 1e-6, "a full vertical loop did not return the up");
        }

        [Test]
        public void HeadingStraightUp_StillHasAValidUp()
        {
            var nu = Carry(new[] { 0.0, 1.0, 0.0 }, new[] { 0.0, 0.0, 1.0 }, new[] { 0.0, 1.0, 0.0 });
            Assert.AreEqual(1.0, Len(nu), 1e-9);
            Assert.AreEqual(0.0, nu[1], 1e-9, "heading +y, the carried up cannot be +y");
            Assert.AreEqual(-1.0, nu[2], 1e-9, "the up turned with the climb (it was +y, now it faces back)");
        }

        [Test]
        public void TheUp_IsNotRightedToTheWorld()
        {
            // a body rolled on its side keeps its roll through a level turn: nothing pulls it back to +y
            var up = new[] { 1.0, 0.0, 0.0 };
            var nu = Carry(up, new[] { 0.0, 0.0, 1.0 }, Unit(0.0, 0.3, 1.0));
            Assert.Greater(Dot(nu, up), 0.99, "the turn righted the body toward the world's up");
        }

        [Test]
        public void AReversal_KeepsTheUp_AndADegenerateUpGetsASquareOne()
        {
            var nu = Carry(new[] { 0.0, 1.0, 0.0 }, new[] { 0.0, 0.0, 1.0 }, new[] { 0.0, 0.0, -1.0 });
            Assert.AreEqual(1.0, nu[1], 1e-9);
            var deg = Carry(new[] { 0.0, 0.0, 1.0 }, new[] { 0.0, 0.0, -1.0 }, new[] { 0.0, 0.0, 1.0 });
            Assert.AreEqual(1.0, Len(deg), 1e-9);
            Assert.AreEqual(0.0, deg[2], 1e-9);
        }
    }
}
