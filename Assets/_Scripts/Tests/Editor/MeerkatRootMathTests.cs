using System.Collections.Generic;
using CosmicShore.Gameplay;
using NUnit.Framework;
using D3 = CosmicShore.Gameplay.MeerkatRootMath.D3;

namespace CosmicShore.Tests
{
    /// <summary>
    /// The Meerkat's roots (<see cref="MeerkatRootMath"/>) held to the Vessel Studio page they were ported from
    /// (<c>Docs/Studios/VesselStudio/meerkat.html</c>, rounds 16-21). Every golden number here came from running the page's
    /// OWN <c>rng</c> / <c>labLayout</c> / <c>labBuild</c> / <c>polyAt</c> / <c>polyFrame</c> / <c>chanRadius</c> /
    /// <c>chanNearest</c> / <c>labTransit</c> / <c>forkPick</c> in node on the same inputs, at the page's shipped numbers
    /// (2026-10-28). A page change that moves one of these is a port to redo, not a test to loosen.
    /// </summary>
    [TestFixture]
    public class MeerkatRootMathTests
    {
        const double Tol = 1e-4;
        static readonly MeerkatRootMath.LayoutSettings Lay = new()
        {
            BranchPointsMin = 3.0, BranchPointsMax = 12.0, OpeningsMin = 5.0, OpeningsMax = 30.0,
            RadiusMin = 90.0, RadiusMax = 420.0, Spread = 360.0, Randomness = 0.45,
        };

        static readonly MeerkatRootMath.ChannelSettings Chan = new()
        {
            Diameter = 40.0, MouthDiameter = 46.0, Gnarl = 0.3, Tip = 0.6,
        };

        static readonly MeerkatRootMath.FlightSettings Fly = new()
        {
            Cruise = 45.0, Thrust = 90.0, Move = 55.0, Drag = 0.45, MaxSpeed = 90.0,
            Bounce = 0.45, Gravity = 0.0, Tip = 0.6, ExitMultiplier = 1.2,
        };

        static void Near(D3 want, D3 got, double tol, string what)
        {
            Assert.AreEqual((float)want.X, (float)got.X, (float)tol, what + ".x");
            Assert.AreEqual((float)want.Y, (float)got.Y, (float)tol, what + ".y");
            Assert.AreEqual((float)want.Z, (float)got.Z, (float)tol, what + ".z");
        }

        [Test]
        public void Rng_IsThePagesBitForBit()
        {
            var r = new MeerkatRootMath.Rng(4242);
            Assert.AreEqual(0.546706133522f, (float)r.Next(), 1e-7f);
            Assert.AreEqual(0.278608789202f, (float)r.Next(), 1e-7f);
            Assert.AreEqual(0.931236917153f, (float)r.Next(), 1e-7f);
            Assert.AreEqual(0.507222466404f, (float)r.Next(), 1e-7f);
            Assert.AreEqual(0.668778280262f, (float)r.Next(), 1e-7f);
        }

        [Test]
        public void Layout_4242_0_5_MatchesThePage()
        {
            var lay = MeerkatRootMath.BuildLayout(Lay, 4242u, 0.5, true);
            Assert.AreEqual(8, lay.Forks, "forks");
            Assert.AreEqual(18, lay.Openings, "openings");
            Assert.AreEqual(26, lay.Edges.Count, "edges");
            Assert.AreEqual(484.089597f, (float)lay.D, 1e-3f, "D");
            Assert.AreEqual(255f, (float)lay.Rad, 1e-3f, "Rad");
            var kids = new[] { 2, 2, 4, 0, 0, 0, 0, 3, 4, 0, 0, 0, 0, 3, 0, 0, 0, 0, 4, 0, 3, 0, 0, 0, 0, 0 };
            for (int i = 0; i < kids.Length; i++) Assert.AreEqual(kids[i], lay.Edges[i].Kids.Count, $"edge {i} kids");
            Near(new D3(94.733346, -15.455419, 287.245049), lay.Nodes[System.Math.Min(3, lay.Nodes.Count - 1)].P, 1e-3, "node 3");
            Near(new D3(-112.201318, 164.454542, -108.344181), lay.Nodes[lay.Nodes.Count - 1].P, 1e-3, "last node");
            Near(new D3(0.926856, 0.288002, 0.240815), lay.Nodes[System.Math.Min(2, lay.Nodes.Count - 1)].T, Tol, "node 2 heading");
            var za = new[] { 0.0, 38.25, 118.385538, 239.337362, 239.337362, 239.337362 };
            var zb = new[] { 38.25, 118.385538, 239.337362, 468.765851, 427.448615, 393.765236 };
            for (int i = 0; i < za.Length; i++) { Assert.AreEqual((float)za[i], (float)lay.Edges[i].Za, 1e-3f, $"za {i}"); Assert.AreEqual((float)zb[i], (float)lay.Edges[i].Zb, 1e-3f, $"zb {i}"); }
        }

        [Test]
        public void Layout_7_0_MatchesThePage()
        {
            var lay = MeerkatRootMath.BuildLayout(Lay, 7u, 0.0, true);
            Assert.AreEqual(3, lay.Forks, "forks");
            Assert.AreEqual(5, lay.Openings, "openings");
            Assert.AreEqual(8, lay.Edges.Count, "edges");
            Assert.AreEqual(125.23749f, (float)lay.D, 1e-3f, "D");
            Assert.AreEqual(90f, (float)lay.Rad, 1e-3f, "Rad");
            var kids = new[] { 2, 2, 0, 0, 3, 0, 0, 0 };
            for (int i = 0; i < kids.Length; i++) Assert.AreEqual(kids[i], lay.Edges[i].Kids.Count, $"edge {i} kids");
            Near(new D3(-57.686515, -18.968507, -48.426363), lay.Nodes[System.Math.Min(3, lay.Nodes.Count - 1)].P, 1e-3, "node 3");
            Near(new D3(57.075409, 57.135555, -21.723118), lay.Nodes[lay.Nodes.Count - 1].P, 1e-3, "last node");
            Near(new D3(0.371647, 0.760006, 0.53317), lay.Nodes[System.Math.Min(2, lay.Nodes.Count - 1)].T, Tol, "node 2 heading");
            var za = new[] { 0.0, 13.5, 46.849977, 46.849977, 13.5, 45.46314 };
            var zb = new[] { 13.5, 46.849977, 116.17628, 113.818369, 45.46314, 125.23749 };
            for (int i = 0; i < za.Length; i++) { Assert.AreEqual((float)za[i], (float)lay.Edges[i].Za, 1e-3f, $"za {i}"); Assert.AreEqual((float)zb[i], (float)lay.Edges[i].Zb, 1e-3f, $"zb {i}"); }
        }

        [Test]
        public void Layout_123456789_1_MatchesThePage()
        {
            var lay = MeerkatRootMath.BuildLayout(Lay, 123456789u, 1.0, true);
            Assert.AreEqual(12, lay.Forks, "forks");
            Assert.AreEqual(30, lay.Openings, "openings");
            Assert.AreEqual(42, lay.Edges.Count, "edges");
            Assert.AreEqual(743.122258f, (float)lay.D, 1e-3f, "D");
            Assert.AreEqual(420f, (float)lay.Rad, 1e-3f, "Rad");
            var kids = new[] { 3, 2, 3, 0, 0, 0, 4, 4, 0, 4, 0, 0, 0, 0, 0, 0, 4, 0, 0, 0, 0, 0, 0, 4, 3, 0, 0, 0, 4, 4, 2, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 };
            for (int i = 0; i < kids.Length; i++) Assert.AreEqual(kids[i], lay.Edges[i].Kids.Count, $"edge {i} kids");
            Near(new D3(-106.860759, -72.451573, 154.226033), lay.Nodes[System.Math.Min(3, lay.Nodes.Count - 1)].P, 1e-3, "node 3");
            Near(new D3(97.235177, 69.970401, -318.553678), lay.Nodes[lay.Nodes.Count - 1].P, 1e-3, "last node");
            Near(new D3(0.52281, 0.568364, 0.63532), lay.Nodes[System.Math.Min(2, lay.Nodes.Count - 1)].T, Tol, "node 2 heading");
            var za = new[] { 0.0, 63.0, 158.359788, 307.010189, 307.010189, 307.010189 };
            var zb = new[] { 63.0, 158.359788, 307.010189, 588.774056, 595.573614, 612.098154 };
            for (int i = 0; i < za.Length; i++) { Assert.AreEqual((float)za[i], (float)lay.Edges[i].Za, 1e-3f, $"za {i}"); Assert.AreEqual((float)zb[i], (float)lay.Edges[i].Zb, 1e-3f, $"zb {i}"); }
        }

        [Test]
        public void Layout_99_0_3_MatchesThePage()
        {
            var lay = MeerkatRootMath.BuildLayout(Lay, 99u, 0.3, false);
            Assert.AreEqual(6, lay.Forks, "forks");
            Assert.AreEqual(13, lay.Openings, "openings");
            Assert.AreEqual(19, lay.Edges.Count, "edges");
            Assert.AreEqual(385.953524f, (float)lay.D, 1e-3f, "D");
            Assert.AreEqual(189f, (float)lay.Rad, 1e-3f, "Rad");
            var kids = new[] { 4, 2, 2, 2, 0, 0, 0, 4, 0, 0, 0, 0, 4, 0, 0, 0, 0, 0, 0 };
            for (int i = 0; i < kids.Length; i++) Assert.AreEqual(kids[i], lay.Edges[i].Kids.Count, $"edge {i} kids");
            Near(new D3(-47.222677, -56.708638, 143.25729), lay.Nodes[System.Math.Min(3, lay.Nodes.Count - 1)].P, 1e-3, "node 3");
            Near(new D3(174.450077, 34.153562, 86.999496), lay.Nodes[lay.Nodes.Count - 1].P, 1e-3, "last node");
            Near(new D3(0.296078, 0.1682, 0.940238), lay.Nodes[System.Math.Min(2, lay.Nodes.Count - 1)].T, Tol, "node 2 heading");
            var za = new[] { 0.0, 56.7, 131.184676, 198.339322, 235.617706, 235.617706 };
            var zb = new[] { 56.7, 131.184676, 198.339322, 235.617706, 347.264138, 358.406984 };
            for (int i = 0; i < za.Length; i++) { Assert.AreEqual((float)za[i], (float)lay.Edges[i].Za, 1e-3f, $"za {i}"); Assert.AreEqual((float)zb[i], (float)lay.Edges[i].Zb, 1e-3f, $"zb {i}"); }
        }

        static readonly D3 A = new(10, 20, 30), T0 = new(0, 0.6, 0.8), Right = new(1, 0, 0), Up = new(0, 0.8, -0.6);

        static List<MeerkatRootMath.Channel> BuiltRoots() =>
            MeerkatRootMath.Build(MeerkatRootMath.BuildLayout(Lay, 4242u, 0.5, true), Chan, A, T0, Right, Up);

        [Test]
        public void Build_AtFrameRadiusNearest_MatchThePage()
        {
            var net = BuiltRoots();
            {
                var c = net[0];
                Assert.AreEqual(38.401888f, (float)c.L, 1e-3f, "L 0");
                Assert.AreEqual(true, c.MouthIn, "mouthIn 0");
                Assert.AreEqual(false, c.MouthOut, "mouthOut 0");
                var p = MeerkatRootMath.At(c.Path, 0.37 * c.L, out var t);
                MeerkatRootMath.Frame(c.Path, 0.37 * c.L, out var n, out var bn);
                Near(new D3(10.695237, 28.197609, 41.579644), p, 1e-3, "p 0"); Near(new D3(0.068564, 0.567398, 0.820584), t, Tol, "t 0"); Near(new D3(-0.004691, 0.822694, -0.568465), n, Tol, "n 0"); Near(new D3(-0.997636, 0.035127, 0.059069), bn, Tol, "b 0");
                Near(new D3(11.043476, 37.935653, 54.889032), c.Path.P[20], 1e-3, "P[20] 0");
                Assert.AreEqual(23f, (float)MeerkatRootMath.Radius(c, 0, Chan.Tip), 1e-3f, "r0 0");
                Assert.AreEqual(19.766118f, (float)MeerkatRootMath.Radius(c, 0.37 * c.L, Chan.Tip), 1e-3f, "r37 0");
                Assert.AreEqual(19.367886f, (float)MeerkatRootMath.Radius(c, c.L, Chan.Tip), 1e-3f, "rL 0");
                Assert.AreEqual(14.099943f, (float)MeerkatRootMath.Nearest(c.Path, p + new D3(3, -2, 1)), 1e-3f, "nearest 0");
                Assert.AreEqual(-5f, (float)MeerkatRootMath.Nearest(c.Path, MeerkatRootMath.At(c.Path, -5, out _)), 1e-3f, "nearest before 0");
            }
            {
                var c = net[1];
                Assert.AreEqual(89.845122f, (float)c.L, 1e-3f, "L 1");
                Assert.AreEqual(false, c.MouthIn, "mouthIn 1");
                Assert.AreEqual(false, c.MouthOut, "mouthOut 1");
                var p = MeerkatRootMath.At(c.Path, 0.37 * c.L, out var t);
                MeerkatRootMath.Frame(c.Path, 0.37 * c.L, out var n, out var bn);
                Near(new D3(-14.496126, 40.534205, 69.889901), p, 1e-3, "p 1"); Near(new D3(-0.840602, -0.492645, -0.225145), t, Tol, "t 1"); Near(new D3(-0.471877, 0.87013, -0.142149), n, Tol, "n 1"); Near(new D3(0.265931, -0.013244, -0.963901), bn, Tol, "b 1");
                Near(new D3(-50.014157, 27.598855, 70.700478), c.Path.P[20], 1e-3, "P[20] 1");
                Assert.AreEqual(19.367886f, (float)MeerkatRootMath.Radius(c, 0, Chan.Tip), 1e-3f, "r0 1");
                Assert.AreEqual(18.877891f, (float)MeerkatRootMath.Radius(c, 0.37 * c.L, Chan.Tip), 1e-3f, "r37 1");
                Assert.AreEqual(18.043576f, (float)MeerkatRootMath.Radius(c, c.L, Chan.Tip), 1e-3f, "rL 1");
                Assert.AreEqual(31.501722f, (float)MeerkatRootMath.Nearest(c.Path, p + new D3(3, -2, 1)), 1e-3f, "nearest 1");
                Assert.AreEqual(-5f, (float)MeerkatRootMath.Nearest(c.Path, MeerkatRootMath.At(c.Path, -5, out _)), 1e-3f, "nearest before 1");
            }
            {
                var c = net[2];
                Assert.AreEqual(135.406338f, (float)c.L, 1e-3f, "L 2");
                Assert.AreEqual(false, c.MouthIn, "mouthIn 2");
                Assert.AreEqual(false, c.MouthOut, "mouthOut 2");
                var p = MeerkatRootMath.At(c.Path, 0.37 * c.L, out var t);
                MeerkatRootMath.Frame(c.Path, 0.37 * c.L, out var n, out var bn);
                Near(new D3(-68.681754, 67.129375, 89.360924), p, 1e-3, "p 2"); Near(new D3(0.649945, 0.745966, 0.145281), t, Tol, "t 2"); Near(new D3(0.507856, -0.284086, -0.813251), n, Tol, "n 2"); Near(new D3(-0.565181, 0.602245, -0.563801), bn, Tol, "b 2");
                Near(new D3(-33.677031, 107.249263, 109.852623), c.Path.P[20], 1e-3, "P[20] 2");
                Assert.AreEqual(18.043576f, (float)MeerkatRootMath.Radius(c, 0, Chan.Tip), 1e-3f, "r0 2");
                Assert.AreEqual(17.304008f, (float)MeerkatRootMath.Radius(c, 0.37 * c.L, Chan.Tip), 1e-3f, "r37 2");
                Assert.AreEqual(16.044743f, (float)MeerkatRootMath.Radius(c, c.L, Chan.Tip), 1e-3f, "rL 2");
                Assert.AreEqual(50.789587f, (float)MeerkatRootMath.Nearest(c.Path, p + new D3(3, -2, 1)), 1e-3f, "nearest 2");
                Assert.AreEqual(-5f, (float)MeerkatRootMath.Nearest(c.Path, MeerkatRootMath.At(c.Path, -5, out _)), 1e-3f, "nearest before 2");
            }
            {
                var c = net[3];
                Assert.AreEqual(250.876447f, (float)c.L, 1e-3f, "L 3");
                Assert.AreEqual(false, c.MouthIn, "mouthIn 3");
                Assert.AreEqual(true, c.MouthOut, "mouthOut 3");
                var p = MeerkatRootMath.At(c.Path, 0.37 * c.L, out var t);
                MeerkatRootMath.Frame(c.Path, 0.37 * c.L, out var n, out var bn);
                Near(new D3(-11.352796, 109.81909, 192.603099), p, 1e-3, "p 3"); Near(new D3(0.172477, -0.620664, 0.764871), t, Tol, "t 3"); Near(new D3(0.420504, 0.748596, 0.512622), n, Tol, "n 3"); Near(new D3(-0.890657, 0.233373, 0.390214), bn, Tol, "b 3");
                Near(new D3(-7.144251, 49.000197, 280.967241), c.Path.P[20], 1e-3, "P[20] 3");
                Assert.AreEqual(16.044743f, (float)MeerkatRootMath.Radius(c, 0, Chan.Tip), 1e-3f, "r0 3");
                Assert.AreEqual(14.641886f, (float)MeerkatRootMath.Radius(c, 0.37 * c.L, Chan.Tip), 1e-3f, "r37 3");
                Assert.AreEqual(14.091224f, (float)MeerkatRootMath.Radius(c, c.L, Chan.Tip), 1e-3f, "rL 3");
                Assert.AreEqual(95.277177f, (float)MeerkatRootMath.Nearest(c.Path, p + new D3(3, -2, 1)), 1e-3f, "nearest 3");
                Assert.AreEqual(-5f, (float)MeerkatRootMath.Nearest(c.Path, MeerkatRootMath.At(c.Path, -5, out _)), 1e-3f, "nearest before 3");
            }
        }

        // the page's script: (frames, RT, left stick side, rise) at 1/60 s, entering at the source with 60 u/s
        [Test]
        public void Flight_MatchesThePage()
        {
            var net = BuiltRoots();
            var g = MeerkatRootMath.Enter(net, 0, A, 60, Up);
            var script = new (int n, double th, double side, double rise)[] { (90, 0, 0, 0), (60, 0, 1, 0), (60, 1, 0, 0.6), (90, 0, -0.7, -0.4), (200, 1, 0.3, 0) };
            bool done = false;
            for (int k = 0; k < script.Length; k++)
            {
                for (int j = 0; j < script[k].n && !done; j++)
                    done = MeerkatRootMath.Step(g, net, null, Fly, script[k].th, script[k].side, script[k].rise, 1.0 / 60, out _) != MeerkatRootMath.FlightResult.Riding;
                Check(k, g, net, done);
            }
        }

        static void Check(int k, MeerkatRootMath.Flight g, List<MeerkatRootMath.Channel> net, bool done)
        {
            switch (k)
            {
                case 0:
                    Assert.AreEqual(false, done, "out 0");
                    Assert.AreEqual(18, g.Chan, "chan 0");
                    Assert.AreEqual(1, g.Forks, "forks 0");
                    Assert.AreEqual(1, g.Bumps, "bumps 0");
                    Assert.AreEqual(28.736165f, (float)g.S, 1e-2f, "s 0");
                    Assert.AreEqual(75.150458f, (float)g.Dist, 1e-2f, "dist 0");
                    Near(new D3(14.329004, 66.803893, 82.490952), g.Pos, 1e-2, "pos 0");
                    Near(new D3(21.223939, 30.80386, -10.27698), g.Vel, 1e-2, "vel 0");
                    Near(new D3(0.694563, 0.672068, 0.256724), g.H, 1e-4, "nose 0");
                    Assert.AreEqual(19, MeerkatRootMath.ForkPick(g, net), "fork pick 0");
                    break;
                case 1:
                    Assert.AreEqual(false, done, "out 1");
                    Assert.AreEqual(18, g.Chan, "chan 1");
                    Assert.AreEqual(1, g.Forks, "forks 1");
                    Assert.AreEqual(3, g.Bumps, "bumps 1");
                    Assert.AreEqual(62.248723f, (float)g.S, 1e-2f, "s 1");
                    Assert.AreEqual(116.473695f, (float)g.Dist, 1e-2f, "dist 1");
                    Near(new D3(40.331829, 96.411652, 81.14948), g.Pos, 1e-2, "pos 1");
                    Near(new D3(37.034313, 19.119143, -0.073054), g.Vel, 1e-2, "vel 1");
                    Near(new D3(0.777988, 0.605119, -0.169015), g.H, 1e-4, "nose 1");
                    Assert.AreEqual(19, MeerkatRootMath.ForkPick(g, net), "fork pick 1");
                    break;
                case 2:
                    Assert.AreEqual(false, done, "out 2");
                    Assert.AreEqual(25, g.Chan, "chan 2");
                    Assert.AreEqual(2, g.Forks, "forks 2");
                    Assert.AreEqual(6, g.Bumps, "bumps 2");
                    Assert.AreEqual(50.009387f, (float)g.S, 1e-2f, "s 2");
                    Assert.AreEqual(193.451203f, (float)g.Dist, 1e-2f, "dist 2");
                    Near(new D3(105.371333, 123.97506, 55.777719), g.Pos, 1e-2, "pos 2");
                    Near(new D3(70.616649, 6.126351, -55.447968), g.Vel, 1e-2, "vel 2");
                    Near(new D3(0.745083, 0.266775, -0.611295), g.H, 1e-4, "nose 2");
                    Assert.AreEqual(-1, MeerkatRootMath.ForkPick(g, net), "fork pick 2");
                    break;
                case 3:
                    Assert.AreEqual(false, done, "out 3");
                    Assert.AreEqual(25, g.Chan, "chan 3");
                    Assert.AreEqual(2, g.Forks, "forks 3");
                    Assert.AreEqual(11, g.Bumps, "bumps 3");
                    Assert.AreEqual(131.808531f, (float)g.S, 1e-2f, "s 3");
                    Assert.AreEqual(305.455874f, (float)g.Dist, 1e-2f, "dist 3");
                    Near(new D3(99.481586, 129.486585, -25.092994), g.Pos, 1e-2, "pos 3");
                    Near(new D3(-4.382653, 51.493772, -37.671469), g.Vel, 1e-2, "vel 3");
                    Near(new D3(-0.091539, 0.338795, -0.936397), g.H, 1e-4, "nose 3");
                    Assert.AreEqual(-1, MeerkatRootMath.ForkPick(g, net), "fork pick 3");
                    break;
                case 4:
                    Assert.AreEqual(true, done, "out 4");
                    Assert.AreEqual(25, g.Chan, "chan 4");
                    Assert.AreEqual(2, g.Forks, "forks 4");
                    Assert.AreEqual(12, g.Bumps, "bumps 4");
                    Assert.AreEqual(234.345205f, (float)g.S, 1e-2f, "s 4");
                    Assert.AreEqual(415.096382f, (float)g.Dist, 1e-2f, "dist 4");
                    Near(new D3(132.79824, 182.120514, -112.935492), g.Pos, 1e-2, "pos 4");
                    Near(new D3(41.140388, 34.036771, -72.449753), g.Vel, 1e-2, "vel 4");
                    Near(new D3(0.398168, 0.359526, -0.843922), g.H, 1e-4, "nose 4");
                    break;
            }
        }

        [Test]
        public void Life_DigsStaysCollapsesFades()
        {
            var net = BuiltRoots();
            var c = net[1];
            var w = MeerkatRootMath.NewLife(c, 0.8);
            Assert.AreEqual((float)(0.8 * c.Za / c.D), (float)w.Wait, 1e-6f, "waits for the front");
            double t = 0;
            while (w.Phase == MeerkatRootMath.Phase.Form && t < 5) { MeerkatRootMath.Step(ref w, c.L, 10, 0.8, 1.0 / 60); t += 1.0 / 60; }
            Assert.AreEqual(MeerkatRootMath.Phase.Open, w.Phase, "dug, open while ridden");
            Assert.AreEqual((float)c.L, (float)w.S1, 1e-6f, "dug to its end");
            MeerkatRootMath.RiderOut(ref w);
            Assert.AreEqual(MeerkatRootMath.Phase.Stay, w.Phase, "the rider is out: it stays");
            for (int i = 0; i < 60 * 10 + 2; i++) MeerkatRootMath.Step(ref w, c.L, 10, 0.8, 1.0 / 60);
            Assert.AreEqual(MeerkatRootMath.Phase.Collapse, w.Phase, "then collapses");
            bool alive = true;
            for (int i = 0; i < 600 && alive; i++) alive = MeerkatRootMath.Step(ref w, c.L, 10, 0.8, 1.0 / 60);
            Assert.IsFalse(alive, "and is gone after its fade");
        }
    }
}
