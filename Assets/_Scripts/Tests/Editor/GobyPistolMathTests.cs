#if UNITY_EDITOR
using System.Collections.Generic;
using CosmicShore.Gameplay;
using CosmicShore.ScriptableObjects;
using NUnit.Framework;
using UnityEngine;

namespace CosmicShore.Tests
{
    /// <summary>
    /// The Goby &amp; Pistol port (<c>PistolRibbon</c>, <c>PistolBodyMath</c>, <c>VortexRing</c>, <c>PistolAI</c>) held to
    /// the numbers the LAB's own functions return on the same inputs. The lab
    /// (<c>Docs/Studios/VesselStudio/goby.html</c>) is the source of truth (/artifact-to-unity); every golden below was
    /// printed by <c>Tools/Build/goby_pistol_goldens.cjs</c>, which runs the page's <c>window.__lab.core</c> in headless
    /// Chromium. Regenerate them there (never by hand) when the lab's arithmetic changes, and this file with
    /// them. The inputs here must stay identical to that script's (see its header).
    ///
    /// <para>Tolerances: the lab computes in doubles and the port in floats, over 360 steps of laid path; 0.02 u is
    /// a few float ulps at 900 u of arc length and three orders below anything a player can see.</para>
    /// </summary>
    public class GobyPistolMathTests
    {
        const float Tol = 0.02f, BodyTol = 0.05f;

        static GobyPistolConfigSO Shipped() => ScriptableObject.CreateInstance<GobyPistolConfigSO>();

        struct Pose { public Vector3 Pos, F, U, R; }

        // pos(t) = (100 sin 0.7t, 40 sin 1.1t, -55 t); f = normalize(70 cos 0.7t, 44 cos 1.1t, -55); u = u0 made normal to f
        static Pose At(float t)
        {
            var pos = new Vector3(100f * Mathf.Sin(0.7f * t), 40f * Mathf.Sin(1.1f * t), -55f * t);
            var f = new Vector3(70f * Mathf.Cos(0.7f * t), 44f * Mathf.Cos(1.1f * t), -55f).normalized;
            var u0 = new Vector3(0.25f * Mathf.Sin(0.5f * t), 1f, 0f);
            var u = (u0 - f * Vector3.Dot(f, u0)).normalized;
            return new Pose { Pos = pos, F = f, U = u, R = Vector3.Cross(f, u).normalized };
        }

        static PistolRibbon Build(PistolUnroll mode, in PistolRibbon.LaySettings lay)
        {
            var rb = new PistolRibbon(mode); var p0 = At(0f);
            rb.Begin(p0.Pos, p0.F, p0.U, p0.R, lay);
            for (int k = 1; k <= 360; k++) { var a = At((k - 1) / 30f); var q = At(k / 30f); rb.Lay(a.Pos, q.Pos, q.F, q.U, q.R, lay); }
            return rb;
        }

        static void Near(float want, float got, float tol, string what) => Assert.AreEqual(want, got, tol, what);
        static void Near(Vector3 want, Vector3 got, float tol, string what)
        {
            Near(want.x, got.x, tol, what + ".x"); Near(want.y, got.y, tol, what + ".y"); Near(want.z, got.z, tol, what + ".z");
        }

        [Test]
        public void ShippedDefaults_MatchTheLabsShippedBlock()
        {
            var c = Shipped(); var b = c.Body; var v = c.Vortex; var l = c.Lay; var ai = c.AI;
            Assert.AreEqual(140.0f, b.pGravity, 1e-6f, "pGravity");
            Assert.AreEqual(3.2f, b.pDiveMul, 1e-6f, "pDiveMul");
            Assert.AreEqual(35.0f, b.pMinSpeed, 1e-6f, "pMinSpeed");
            Assert.AreEqual(0.04f, b.pDrag, 1e-6f, "pDrag");
            Assert.AreEqual(28.0f, b.pGoodLand, 1e-6f, "pGoodLand");
            Assert.AreEqual(240.0f, b.pLaunchRange, 1e-6f, "pLaunchRange");
            Assert.AreEqual(280.0f, b.pLaunchSpeed, 1e-6f, "pLaunchSpeed");
            Assert.AreEqual(1.0f, b.pLaunchCharges, 1e-6f, "pLaunchCharges");
            Assert.AreEqual(9.0f, b.pLaunchCd, 1e-6f, "pLaunchCd");
            Assert.AreEqual(2.5f, b.pGhostTime, 1e-6f, "pGhostTime");
            Assert.AreEqual(8.0f, b.pGhostCd, 1e-6f, "pGhostCd");
            Assert.AreEqual(4.0f, b.pVortexCd, 1e-6f, "pVortexCd");
            Assert.AreEqual(22.0f, v.pVortexR, 1e-6f, "pVortexR");
            Assert.AreEqual(120.0f, v.pVortexSpeed, 1e-6f, "pVortexSpeed");
            Assert.AreEqual(2.6f, v.pVortexLife, 1e-6f, "pVortexLife");
            Assert.AreEqual(24.0f, v.pVortexCapture, 1e-6f, "pVortexCapture");
            Assert.AreEqual(7.0f, v.pVortexSpin, 1e-6f, "pVortexSpin");
            Assert.AreEqual(30.0f, v.pVortexMax, 1e-6f, "pVortexMax");
            Assert.AreEqual(1.0f, v.pVortexCollapse, 1e-6f, "pVortexCollapse");
            Assert.AreEqual(4.0f, l.SampleGap, 1e-6f, "tSampleGap");
            Assert.AreEqual(16.0f, l.BlockGap, 1e-6f, "tBlockGap");
            Assert.AreEqual(4.0f, l.HillEvery, 1e-6f, "tHillEvery");
            Assert.AreEqual(26.0f, l.HillAmp, 1e-6f, "tHillAmp");
            Assert.AreEqual(120.0f, ai.aiHighDive, 1e-6f, "aiHighDive");
            Assert.AreEqual(2.4f, c.CloseMax, 1e-6f, "gCloseMax");
            Assert.AreEqual(320.0f, c.CloseRange, 1e-6f, "gCloseRange");
            Assert.AreEqual(260.0f, c.AiMaxGap, 1e-6f, "aiMaxGap");
            Assert.AreEqual(6.0f, c.PrimerCharges, 1e-6f, "gPrimerCharges");
            Assert.AreEqual(1.5f, c.PrimerCd, 1e-6f, "gPrimerCd");
            Assert.AreEqual(380.0f, c.PrimerRange, 1e-6f, "gPrimerRange");
            Assert.AreEqual(34.0f, c.PrimeSplash, 1e-6f, "gPrimeSplash");
            Assert.AreEqual(12.0f, c.PrimeLife, 1e-6f, "gPrimeLife");
            Assert.AreEqual(2.0f, c.FlareCharges, 1e-6f, "gFlareCharges");
            Assert.AreEqual(10.0f, c.FlareCd, 1e-6f, "gFlareCd");
            Assert.AreEqual(420.0f, c.FlareRange, 1e-6f, "gFlareRange");
            Assert.AreEqual(10.0f, c.FlareMax, 1e-6f, "gFlareMax");
            Assert.AreEqual(240.0f, c.FlareSpeed, 1e-6f, "gFlareSpeed");
            Assert.AreEqual(45.0f, c.Vision, 1e-6f, "pVision");
            Assert.AreEqual(5.0f, c.BodyR, 1e-6f, "pBodyR");
            Assert.AreEqual(460.0f, c.ViewWidth, 1e-6f, "pViewWidth");
            Assert.AreEqual(160.0f, c.Parallax, 1e-6f, "pParallax");
            Assert.AreEqual(40.0f, c.Chain, 1e-6f, "pChain");
            Assert.AreEqual(80.0f, c.HotRing, 1e-6f, "pHotRing");
            Assert.AreEqual(10.0f, c.HotLife, 1e-6f, "pHotLife");
            Object.DestroyImmediate(c);
        }

        [Test]
        public void Ribbon_WorldUp_MatchesTheLab()
        {
            var c = Shipped(); var rb = Build(PistolUnroll.WorldUp, c.Lay); Object.DestroyImmediate(c);
            Assert.AreEqual(237, rb.Count, "samples");
            Assert.AreEqual(15, rb.CX.Count, "hinges (control points before the end at the Goby)");
            Assert.AreEqual(60, rb.Blocks.Count, "blocks");
            Near(863.817077f, rb.X(rb.Count - 1), Tol, "last x"); Near(865.20101f, rb.EndX, Tol, "end x"); Near(23.682941f, rb.EndY, Tol, "end y");
            Near(0.0f, rb.CX[0], Tol, "cp0 x"); Near(26.0f, rb.CY[0], Tol, "cp0 y");
            Near(58.099409f, rb.CX[1], Tol, "cp1 x"); Near(0.794004f, rb.CY[1], Tol, "cp1 y");
            Near(120.258242f, rb.CX[2], Tol, "cp2 x"); Near(65.873944f, rb.CY[2], Tol, "cp2 y");
            Near(178.204409f, rb.CX[3], Tol, "cp3 x"); Near(-10.849331f, rb.CY[3], Tol, "cp3 y");
            Near(231.503839f, rb.CX[4], Tol, "cp4 x"); Near(5.857503f, rb.CY[4], Tol, "cp4 y");
            Near(292.256088f, rb.CX[5], Tol, "cp5 x"); Near(-65.055008f, rb.CY[5], Tol, "cp5 y");
            { var m = rb.Map(new Vector3(65.11862f, 39.829555f, -51.0f), -1e9f, 1e9f); Assert.IsTrue(m.Ok); Near(82.415537f, m.X, Tol, "map0 x"); Near(39.829555f, m.H, Tol, "map0 h"); Near(3.308563f, m.D, Tol, "map0 d"); Assert.AreEqual(false, m.Ambiguous, "map0 ambiguous"); }
            { var m = rb.Map(new Vector3(79.492435f, 15.560211f, -100.5f), -1e9f, 1e9f); Assert.IsTrue(m.Ok); Near(133.671423f, m.X, Tol, "map1 x"); Near(15.560211f, m.H, Tol, "map1 h"); Near(-15.027997f, m.D, Tol, "map1 d"); Assert.AreEqual(false, m.Ambiguous, "map1 ambiguous"); }
            { var m = rb.Map(new Vector3(82.404434f, -29.026671f, -201.5f), -1e9f, 1e9f); Assert.IsTrue(m.Ok); Near(237.811586f, m.X, Tol, "map2 x"); Near(-29.026671f, m.H, Tol, "map2 h"); Near(19.689185f, m.D, Tol, "map2 d"); Assert.AreEqual(false, m.Ambiguous, "map2 ambiguous"); }
            { var m = rb.Map(new Vector3(-55.802725f, 0.644709f, -305.0f), -1e9f, 1e9f); Assert.IsTrue(m.Ok); Near(408.608095f, m.X, Tol, "map3 x"); Near(0.644709f, m.H, Tol, "map3 h"); Near(8.346817f, m.D, Tol, "map3 d"); Assert.AreEqual(false, m.Ambiguous, "map3 ambiguous"); }
            { var m = rb.Map(new Vector3(-64.396595f, 20.816897f, -423.5f), -1e9f, 1e9f); Assert.IsTrue(m.Ok); Near(559.183686f, m.X, Tol, "map4 x"); Near(20.816897f, m.H, Tol, "map4 h"); Near(10.339696f, m.D, Tol, "map4 d"); Assert.AreEqual(false, m.Ambiguous, "map4 ambiguous"); }
            { var m = rb.Map(new Vector3(48.974569f, -25.114309f, -567.0f), -1e9f, 1e9f); Assert.IsTrue(m.Ok); Near(742.888323f, m.X, Tol, "map5 x"); Near(-25.114309f, m.H, Tol, "map5 h"); Near(-23.829573f, m.D, Tol, "map5 d"); Assert.AreEqual(false, m.Ambiguous, "map5 ambiguous"); }
            Near(new Vector3(7.859707f, 5.0f, -6.182635f), rb.Unmap(10.0f, 5.0f, 0.0f), Tol, "unmap0");
            Near(new Vector3(103.960776f, -12.0f, -72.321999f), rb.Unmap(120.5f, -12.0f, 20.0f), Tol, "unmap1");
            Near(new Vector3(-0.275892f, 30.0f, -202.410441f), rb.Unmap(300.0f, 30.0f, -35.0f), Tol, "unmap2");
            Near(new Vector3(-51.410468f, 0.0f, -355.941732f), rb.Unmap(455.25f, 0.0f, 44.0f), Tol, "unmap3");
            Near(new Vector3(-28.935596f, -40.0f, -457.616321f), rb.Unmap(610.0f, -40.0f, 10.0f), Tol, "unmap4");
            Near(new Vector3(-2.69553f, 3.0f, 4.661986f), rb.Unmap(-5.0f, 3.0f, 2.0f), Tol, "unmap5");
            { float y = rb.Ground(0.0f, out float s); Near(26.0f, y, Tol, "ground0"); Near(0.0f, s, Tol, "slope0"); }
            { float y = rb.Ground(33.3f, out float s); Near(5.317395f, y, Tol, "ground1"); Near(-0.556913f, s, Tol, "slope1"); }
            { float y = rb.Ground(100.0f, out float s); Near(52.009862f, y, Tol, "ground2"); Near(1.26818f, s, Tol, "slope2"); }
            { float y = rb.Ground(187.5f, out float s); Near(-12.28419f, y, Tol, "ground3"); Near(0.180843f, s, Tol, "slope3"); }
            { float y = rb.Ground(250.0f, out float s); Near(-13.686056f, y, Tol, "ground4"); Near(-1.475729f, s, Tol, "slope4"); }
            { float y = rb.Ground(401.0f, out float s); Near(-34.649637f, y, Tol, "ground5"); Near(-0.434112f, s, Tol, "slope5"); }
            { float y = rb.Ground(512.25f, out float s); Near(22.313144f, y, Tol, "ground6"); Near(-0.917723f, s, Tol, "slope6"); }
            { float y = rb.Ground(860.817077f, out float s); Near(23.785455f, y, Tol, "ground7"); Near(-0.09474f, s, Tol, "slope7"); }
            { rb.FrameAt(50.0f, out var T, out var U, out var R); Near(new Vector3(0.761884f, 0.0f, -0.647713f), T, Tol, "T0"); Near(new Vector3(0.0f, 1.0f, 0.0f), U, Tol, "U0"); Near(new Vector3(0.647713f, 0.0f, 0.761884f), R, Tol, "R0"); }
            { rb.FrameAt(260.0f, out var T, out var U, out var R); Near(new Vector3(-0.734231f, 0.0f, -0.6789f), T, Tol, "T1"); Near(new Vector3(0.0f, 1.0f, 0.0f), U, Tol, "U1"); Near(new Vector3(0.6789f, 0.0f, -0.734231f), R, Tol, "R1"); }
            { rb.FrameAt(480.0f, out var T, out var U, out var R); Near(new Vector3(-0.165487f, 0.0f, -0.986212f), T, Tol, "T2"); Near(new Vector3(0.0f, 1.0f, 0.0f), U, Tol, "U2"); Near(new Vector3(0.986212f, 0.0f, -0.165487f), R, Tol, "R2"); }
        }

        [Test]
        public void Ribbon_ArcLength_MatchesTheLab()
        {
            var c = Shipped(); var rb = Build(PistolUnroll.ArcLength, c.Lay); Object.DestroyImmediate(c);
            Assert.AreEqual(237, rb.Count, "samples");
            Assert.AreEqual(15, rb.CX.Count, "hinges (control points before the end at the Goby)");
            Assert.AreEqual(60, rb.Blocks.Count, "blocks");
            Near(943.934467f, rb.X(rb.Count - 1), Tol, "last x"); Near(945.505877f, rb.EndX, Tol, "end x"); Near(23.682941f, rb.EndY, Tol, "end y");
            Near(0.0f, rb.CX[0], Tol, "cp0 x"); Near(26.0f, rb.CY[0], Tol, "cp0 y");
            Near(63.999777f, rb.CX[1], Tol, "cp1 x"); Near(0.794004f, rb.CY[1], Tol, "cp1 y");
            Near(127.995664f, rb.CX[2], Tol, "cp2 x"); Near(65.873944f, rb.CY[2], Tol, "cp2 y");
            Near(191.983075f, rb.CX[3], Tol, "cp3 x"); Near(-10.849331f, rb.CY[3], Tol, "cp3 y");
            Near(255.979947f, rb.CX[4], Tol, "cp4 x"); Near(5.857503f, rb.CY[4], Tol, "cp4 y");
            Near(319.977397f, rb.CX[5], Tol, "cp5 x"); Near(-65.055008f, rb.CY[5], Tol, "cp5 y");
            { var m = rb.Map(new Vector3(65.11862f, 39.829555f, -51.0f), -1e9f, 1e9f); Assert.IsTrue(m.Ok); Near(89.676949f, m.X, Tol, "map0 x"); Near(39.829555f, m.H, Tol, "map0 h"); Near(3.308563f, m.D, Tol, "map0 d"); Assert.AreEqual(false, m.Ambiguous, "map0 ambiguous"); }
            { var m = rb.Map(new Vector3(79.492435f, 15.560211f, -100.5f), -1e9f, 1e9f); Assert.IsTrue(m.Ok); Near(141.554608f, m.X, Tol, "map1 x"); Near(15.560211f, m.H, Tol, "map1 h"); Near(-15.027997f, m.D, Tol, "map1 d"); Assert.AreEqual(false, m.Ambiguous, "map1 ambiguous"); }
            { var m = rb.Map(new Vector3(82.404434f, -29.026671f, -201.5f), -1e9f, 1e9f); Assert.IsTrue(m.Ok); Near(263.020634f, m.X, Tol, "map2 x"); Near(-29.026671f, m.H, Tol, "map2 h"); Near(19.689185f, m.D, Tol, "map2 d"); Assert.AreEqual(false, m.Ambiguous, "map2 ambiguous"); }
            { var m = rb.Map(new Vector3(-55.802725f, 0.644709f, -305.0f), -1e9f, 1e9f); Assert.IsTrue(m.Ok); Near(441.366156f, m.X, Tol, "map3 x"); Near(0.644709f, m.H, Tol, "map3 h"); Near(8.346817f, m.D, Tol, "map3 d"); Assert.AreEqual(false, m.Ambiguous, "map3 ambiguous"); }
            { var m = rb.Map(new Vector3(-64.396595f, 20.816897f, -423.5f), -1e9f, 1e9f); Assert.IsTrue(m.Ok); Near(606.946909f, m.X, Tol, "map4 x"); Near(20.816897f, m.H, Tol, "map4 h"); Near(10.339696f, m.D, Tol, "map4 d"); Assert.AreEqual(false, m.Ambiguous, "map4 ambiguous"); }
            { var m = rb.Map(new Vector3(48.974569f, -25.114309f, -567.0f), -1e9f, 1e9f); Assert.IsTrue(m.Ok); Near(804.914124f, m.X, Tol, "map5 x"); Near(-25.114309f, m.H, Tol, "map5 h"); Near(-23.829573f, m.D, Tol, "map5 d"); Assert.AreEqual(false, m.Ambiguous, "map5 ambiguous"); }
            Near(new Vector3(7.049029f, 5.0f, -5.544031f), rb.Unmap(10.0f, 5.0f, 0.0f), Tol, "unmap0");
            Near(new Vector3(98.909152f, -12.0f, -64.81258f), rb.Unmap(120.5f, -12.0f, 20.0f), Tol, "unmap1");
            Near(new Vector3(19.906226f, 30.0f, -185.612727f), rb.Unmap(300.0f, 30.0f, -35.0f), Tol, "unmap2");
            Near(new Vector3(-37.639194f, 0.0f, -337.585699f), rb.Unmap(455.25f, 0.0f, 44.0f), Tol, "unmap3");
            Near(new Vector3(-62.902539f, -40.0f, -425.679935f), rb.Unmap(610.0f, -40.0f, 10.0f), Tol, "unmap4");
            Near(new Vector3(-2.69553f, 3.0f, 4.661986f), rb.Unmap(-5.0f, 3.0f, 2.0f), Tol, "unmap5");
            { float y = rb.Ground(0.0f, out float s); Near(26.0f, y, Tol, "ground0"); Near(0.0f, s, Tol, "slope0"); }
            { float y = rb.Ground(33.3f, out float s); Near(7.022441f, y, Tol, "ground1"); Near(-0.554985f, s, Tol, "slope1"); }
            { float y = rb.Ground(100.0f, out float s); Near(42.359455f, y, Tol, "ground2"); Near(1.423822f, s, Tol, "slope2"); }
            { float y = rb.Ground(187.5f, out float s); Near(-7.980833f, y, Tol, "ground3"); Near(-0.801742f, s, Tol, "slope3"); }
            { float y = rb.Ground(250.0f, out float s); Near(7.290945f, y, Tol, "ground4"); Near(-0.068267f, s, Tol, "slope4"); }
            { float y = rb.Ground(401.0f, out float s); Near(-11.707987f, y, Tol, "ground5"); Near(-0.606193f, s, Tol, "slope5"); }
            { float y = rb.Ground(512.25f, out float s); Near(51.131435f, y, Tol, "ground6"); Near(0.34707f, s, Tol, "slope6"); }
            { float y = rb.Ground(940.934467f, out float s); Near(23.738564f, y, Tol, "ground7"); Near(-0.065617f, s, Tol, "slope7"); }
            { rb.FrameAt(50.0f, out var T, out var U, out var R); Near(new Vector3(0.765978f, 0.0f, -0.642867f), T, Tol, "T0"); Near(new Vector3(0.0f, 1.0f, 0.0f), U, Tol, "U0"); Near(new Vector3(0.642867f, 0.0f, 0.765978f), R, Tol, "R0"); }
            { rb.FrameAt(260.0f, out var T, out var U, out var R); Near(new Vector3(-0.678841f, 0.0f, -0.734285f), T, Tol, "T1"); Near(new Vector3(0.0f, 1.0f, 0.0f), U, Tol, "U1"); Near(new Vector3(0.734285f, 0.0f, -0.678841f), R, Tol, "R1"); }
            { rb.FrameAt(480.0f, out var T, out var U, out var R); Near(new Vector3(-0.565039f, 0.0f, -0.825064f), T, Tol, "T2"); Near(new Vector3(0.0f, 1.0f, 0.0f), U, Tol, "U2"); Near(new Vector3(0.825064f, 0.0f, -0.565039f), R, Tol, "R2"); }
        }

        [Test]
        public void Ribbon_GobyUp_MatchesTheLab()
        {
            var c = Shipped(); var rb = Build(PistolUnroll.GobyUp, c.Lay); Object.DestroyImmediate(c);
            Assert.AreEqual(237, rb.Count, "samples");
            Assert.AreEqual(15, rb.CX.Count, "hinges (control points before the end at the Goby)");
            Assert.AreEqual(60, rb.Blocks.Count, "blocks");
            Near(943.934467f, rb.X(rb.Count - 1), Tol, "last x"); Near(945.505877f, rb.EndX, Tol, "end x"); Near(0.0f, rb.EndY, Tol, "end y");
            Near(0.0f, rb.CX[0], Tol, "cp0 x"); Near(26.0f, rb.CY[0], Tol, "cp0 y");
            Near(63.999777f, rb.CX[1], Tol, "cp1 x"); Near(-26.0f, rb.CY[1], Tol, "cp1 y");
            Near(127.995664f, rb.CX[2], Tol, "cp2 x"); Near(26.0f, rb.CY[2], Tol, "cp2 y");
            Near(191.983075f, rb.CX[3], Tol, "cp3 x"); Near(-26.0f, rb.CY[3], Tol, "cp3 y");
            Near(255.979947f, rb.CX[4], Tol, "cp4 x"); Near(26.0f, rb.CY[4], Tol, "cp4 y");
            Near(319.977397f, rb.CX[5], Tol, "cp5 x"); Near(-26.0f, rb.CY[5], Tol, "cp5 y");
            { var m = rb.Map(new Vector3(65.11862f, 39.829555f, -51.0f), -1e9f, 1e9f); Assert.IsTrue(m.Ok); Near(90.950775f, m.X, Tol, "map0 x"); Near(4.846885f, m.H, Tol, "map0 h"); Near(3.017813f, m.D, Tol, "map0 d"); Assert.AreEqual(false, m.Ambiguous, "map0 ambiguous"); }
            { var m = rb.Map(new Vector3(79.492435f, 15.560211f, -100.5f), -1e9f, 1e9f); Assert.IsTrue(m.Ok); Near(150.13245f, m.X, Tol, "map1 x"); Near(-23.471335f, m.H, Tol, "map1 h"); Near(-11.645024f, m.D, Tol, "map1 d"); Assert.AreEqual(false, m.Ambiguous, "map1 ambiguous"); }
            { var m = rb.Map(new Vector3(82.404434f, -29.026671f, -201.5f), -1e9f, 1e9f); Assert.IsTrue(m.Ok); Near(265.106907f, m.X, Tol, "map2 x"); Near(0.149698f, m.H, Tol, "map2 h"); Near(20.384748f, m.D, Tol, "map2 d"); Assert.AreEqual(false, m.Ambiguous, "map2 ambiguous"); }
            { var m = rb.Map(new Vector3(-55.802725f, 0.644709f, -305.0f), -1e9f, 1e9f); Assert.IsTrue(m.Ok); Near(447.560289f, m.X, Tol, "map3 x"); Near(11.645038f, m.H, Tol, "map3 h"); Near(7.302808f, m.D, Tol, "map3 d"); Assert.AreEqual(false, m.Ambiguous, "map3 ambiguous"); }
            { var m = rb.Map(new Vector3(-64.396595f, 20.816897f, -423.5f), -1e9f, 1e9f); Assert.IsTrue(m.Ok); Near(610.400589f, m.X, Tol, "map4 x"); Near(-9.056451f, m.H, Tol, "map4 h"); Near(9.161277f, m.D, Tol, "map4 d"); Assert.AreEqual(false, m.Ambiguous, "map4 ambiguous"); }
            { var m = rb.Map(new Vector3(48.974569f, -25.114309f, -567.0f), -1e9f, 1e9f); Assert.IsTrue(m.Ok); Near(805.21722f, m.X, Tol, "map5 x"); Near(18.584991f, m.H, Tol, "map5 h"); Near(-21.043486f, m.D, Tol, "map5 d"); Assert.AreEqual(false, m.Ambiguous, "map5 ambiguous"); }
            Near(new Vector3(5.324162f, 8.909623f, -4.163037f), rb.Unmap(10.0f, 5.0f, 0.0f), Tol, "unmap0");
            Near(new Vector3(98.053047f, 26.137974f, -65.684188f), rb.Unmap(120.5f, -12.0f, 20.0f), Tol, "unmap1");
            Near(new Vector3(17.451223f, -0.387929f, -196.614921f), rb.Unmap(300.0f, 30.0f, -35.0f), Tol, "unmap2");
            Near(new Vector3(-39.242004f, -8.551488f, -339.046465f), rb.Unmap(455.25f, 0.0f, 44.0f), Tol, "unmap3");
            Near(new Vector3(-68.489446f, -7.287556f, -411.19654f), rb.Unmap(610.0f, -40.0f, 10.0f), Tol, "unmap4");
            Near(new Vector3(-3.334156f, 0.474589f, 5.163705f), rb.Unmap(-5.0f, 3.0f, 2.0f), Tol, "unmap5");
            { float y = rb.Ground(0.0f, out float s); Near(26.0f, y, Tol, "ground0"); Near(0.0f, s, Tol, "slope0"); }
            { float y = rb.Ground(33.3f, out float s); Near(-7.809265f, y, Tol, "ground1"); Near(-0.998117f, s, Tol, "slope1"); }
            { float y = rb.Ground(100.0f, out float s); Near(4.852652f, y, Tol, "ground2"); Near(1.19976f, s, Tol, "slope2"); }
            { float y = rb.Ground(187.5f, out float s); Near(-25.270016f, y, Tol, "ground3"); Near(-0.317684f, s, Tol, "slope3"); }
            { float y = rb.Ground(250.0f, out float s); Near(24.722772f, y, Tol, "ground4"); Near(0.412981f, s, Tol, "slope4"); }
            { float y = rb.Ground(401.0f, out float s); Near(16.918302f, y, Tol, "ground5"); Near(-0.951876f, s, Tol, "slope5"); }
            { float y = rb.Ground(512.25f, out float s); Near(25.997058f, y, Tol, "ground6"); Near(-0.021116f, s, Tol, "slope6"); }
            { float y = rb.Ground(940.934467f, out float s); Near(2.686242f, y, Tol, "ground7"); Near(-0.644291f, s, Tol, "slope7"); }
            { rb.FrameAt(50.0f, out var T, out var U, out var R); Near(new Vector3(0.702521f, 0.398528f, -0.58961f), T, Tol, "T0"); Near(new Vector3(-0.289899f, 0.915168f, 0.280045f), U, Tol, "U0"); Near(new Vector3(0.649602f, -0.026738f, 0.759804f), R, Tol, "R0"); }
            { rb.FrameAt(260.0f, out var T, out var U, out var R); Near(new Vector3(-0.611227f, -0.43507f, -0.661148f), T, Tol, "T1"); Near(new Vector3(-0.085261f, 0.8582f, -0.506185f), U, Tol, "U1"); Near(new Vector3(0.798336f, -0.245126f, -0.550067f), R, Tol, "R1"); }
            { rb.FrameAt(480.0f, out var T, out var U, out var R); Near(new Vector3(-0.475347f, 0.540625f, -0.694096f), T, Tol, "T2"); Near(new Vector3(0.384213f, 0.839678f, 0.383824f), U, Tol, "U2"); Near(new Vector3(0.780071f, -0.072877f, -0.621433f), R, Tol, "R2"); }
        }

        [Test]
        public void GobySpeed_ClosenessAndAiGap_MatchTheLab()
        {
            var c = Shipped(); float cm = c.CloseMax, cr = c.CloseRange, mg = c.AiMaxGap; Object.DestroyImmediate(c);
            Near(2.4f, GobyMath.Closeness(0.0f, cm, cr), 1e-6f, "closeness at gap 0.0f"); Near(1.0f, GobyMath.AiGapThrottle(0.0f, mg), 1e-6f, "AI throttle at gap 0.0f");
            Near(2.18125f, GobyMath.Closeness(50.0f, cm, cr), 1e-6f, "closeness at gap 50.0f"); Near(1.0f, GobyMath.AiGapThrottle(50.0f, mg), 1e-6f, "AI throttle at gap 50.0f");
            Near(1.7f, GobyMath.Closeness(160.0f, cm, cr), 1e-6f, "closeness at gap 160.0f"); Near(1.0f, GobyMath.AiGapThrottle(160.0f, mg), 1e-6f, "AI throttle at gap 160.0f");
            Near(1.004375f, GobyMath.Closeness(319.0f, cm, cr), 1e-6f, "closeness at gap 319.0f"); Near(0.3f, GobyMath.AiGapThrottle(319.0f, mg), 1e-6f, "AI throttle at gap 319.0f");
            Near(1.0f, GobyMath.Closeness(320.0f, cm, cr), 1e-6f, "closeness at gap 320.0f"); Near(0.3f, GobyMath.AiGapThrottle(320.0f, mg), 1e-6f, "AI throttle at gap 320.0f");
            Near(1.0f, GobyMath.Closeness(400.0f, cm, cr), 1e-6f, "closeness at gap 400.0f"); Near(0.3f, GobyMath.AiGapThrottle(400.0f, mg), 1e-6f, "AI throttle at gap 400.0f");
            Near(1.0f, GobyMath.Closeness(800.0f, cm, cr), 1e-6f, "closeness at gap 800.0f"); Near(0.3f, GobyMath.AiGapThrottle(800.0f, mg), 1e-6f, "AI throttle at gap 800.0f");
        }

        [Test]
        public void PistolBody_RideAndLaunch_MatchesTheLab()
        {
            var c = Shipped(); var rb = Build(PistolUnroll.WorldUp, c.Lay); var s = c.Body; Object.DestroyImmediate(c);
            // the lab's body golden: x = 40 on the ground at 60 u/s, the Goby 80 u/s; dive on a 1 s rhythm, a launch at step 100
            var p = new PistolBodyMath.State { x = 40f, y = rb.Ground(40f), v = 60f, launch = Mathf.RoundToInt(s.pLaunchCharges) };
            float t = 0f;
            for (int k = 0; k <= 240; k++)
            {
                switch (k)
                {
                    case 0: Near(40.0f, p.x, BodyTol, "x@0"); Near(2.079765f, p.y, BodyTol, "y@0"); Assert.AreEqual(true, p.grounded, "grounded@0"); Near(60.0f, p.v, BodyTol, "v@0"); Near(0.0f, p.vx, BodyTol, "vx@0"); Near(0.0f, p.vy, BodyTol, "vy@0"); Assert.AreEqual(1, p.launch, "launch@0"); break;
                    case 30: Near(67.455556f, p.x, BodyTol, "x@30"); Near(7.128116f, p.y, BodyTol, "y@30"); Assert.AreEqual(true, p.grounded, "grounded@30"); Near(35.0f, p.v, BodyTol, "v@30"); Near(0.0f, p.vx, BodyTol, "vx@30"); Near(0.0f, p.vy, BodyTol, "vy@30"); Assert.AreEqual(1, p.launch, "launch@30"); break;
                    case 60: Near(78.519118f, p.x, BodyTol, "x@60"); Near(20.738965f, p.y, BodyTol, "y@60"); Assert.AreEqual(true, p.grounded, "grounded@60"); Near(35.0f, p.v, BodyTol, "v@60"); Near(0.0f, p.vx, BodyTol, "vx@60"); Near(0.0f, p.vy, BodyTol, "vy@60"); Assert.AreEqual(1, p.launch, "launch@60"); break;
                    case 90: Near(88.269402f, p.x, BodyTol, "x@90"); Near(35.286065f, p.y, BodyTol, "y@90"); Assert.AreEqual(true, p.grounded, "grounded@90"); Near(35.0f, p.v, BodyTol, "v@90"); Near(0.0f, p.vx, BodyTol, "vx@90"); Near(0.0f, p.vy, BodyTol, "vy@90"); Assert.AreEqual(1, p.launch, "launch@90"); break;
                    case 120: Near(148.699412f, p.x, BodyTol, "x@120"); Near(114.960093f, p.y, BodyTol, "y@120"); Assert.AreEqual(false, p.grounded, "grounded@120"); Near(306.109387f, p.v, BodyTol, "v@120"); Near(177.150545f, p.vx, BodyTol, "vx@120"); Near(209.974358f, p.vy, BodyTol, "vy@120"); Assert.AreEqual(0, p.launch, "launch@120"); break;
                    case 150: Near(237.274685f, p.x, BodyTol, "x@150"); Near(163.363939f, p.y, BodyTol, "y@150"); Assert.AreEqual(false, p.grounded, "grounded@150"); Near(306.109387f, p.v, BodyTol, "v@150"); Near(177.150545f, p.vx, BodyTol, "vx@150"); Near(11.641024f, p.vy, BodyTol, "vy@150"); Assert.AreEqual(0, p.launch, "launch@150"); break;
                    case 180: Near(325.849957f, p.x, BodyTol, "x@180"); Near(151.101117f, p.y, BodyTol, "y@180"); Assert.AreEqual(false, p.grounded, "grounded@180"); Near(306.109387f, p.v, BodyTol, "v@180"); Near(177.150545f, p.vx, BodyTol, "vx@180"); Near(-58.358976f, p.vy, BodyTol, "vy@180"); Assert.AreEqual(0, p.launch, "launch@180"); break;
                    case 210: Near(414.42523f, p.x, BodyTol, "x@210"); Near(65.338296f, p.y, BodyTol, "y@210"); Assert.AreEqual(false, p.grounded, "grounded@210"); Near(306.109387f, p.v, BodyTol, "v@210"); Near(177.150545f, p.vx, BodyTol, "vx@210"); Near(-256.692309f, p.vy, BodyTol, "vy@210"); Assert.AreEqual(0, p.launch, "launch@210"); break;
                    case 240: Near(451.902226f, p.x, BodyTol, "x@240"); Near(29.969781f, p.y, BodyTol, "y@240"); Assert.AreEqual(true, p.grounded, "grounded@240"); Near(35.0f, p.v, BodyTol, "v@240"); Near(177.150545f, p.vx, BodyTol, "vx@240"); Near(-282.358976f, p.vy, BodyTol, "vy@240"); Assert.AreEqual(0, p.launch, "launch@240"); break;
                }
                if (k == 240) break;
                t += 1f / 60f;
                var inp = new PistolBodyMath.Input { Dive = k % 60 < 25 ? 1f : 0f, HasLaunch = k == 100, Launch = new Vector2(120f, 90f) };
                PistolBodyMath.Step(p, rb, rb.GobyX, 80f, t, 1f / 60f, inp, s);
            }
        }

        [Test]
        public void VortexRing_ToroidalFlow_MatchesTheLab()
        {
            var c = Shipped(); var s = c.Vortex; Object.DestroyImmediate(c);
            // one ring from the origin along -z through 24 prisms on a circle of 22 at z ~ -40
            var pos = new Vector3[24]; var taken = new bool[24];
            for (int i = 0; i < 24; i++) { float a = i / 24f * Mathf.PI * 2f; pos[i] = new Vector3(Mathf.Cos(a) * 22f + (i % 2) * 5f, Mathf.Sin(a) * 22f, -40f + ((i % 3) - 1) * 6f); }
            var ring = new VortexRing(Vector3.zero, new Vector3(0f, 0f, -1f), s.pVortexLife);
            for (int k = 1; k <= 60; k++)
            {
                ring.Advance(1f / 60f, s);
                for (int i = 0; i < 24 && !ring.Full(s); i++) if (!taken[i] && ring.TryCapture(i, pos[i], s)) taken[i] = true;
                for (int j = 0; j < ring.Caps.Count; j++) pos[ring.Caps[j].Id] = ring.Flow(j, 1f / 60f, s);
                switch (k)
                {
                    case 20:
                        Near(new Vector3(0.0f, 0.0f, -40.0f), ring.C, Tol, "c@20"); Assert.AreEqual(24, ring.Caps.Count, "carried@20");
                        Assert.AreEqual(2, ring.Caps[0].Id, "cap0 id@20"); Near(-0.310265f, ring.Caps[0].th, Tol, "th"); Near(3.437463f, ring.Caps[0].ph, Tol, "ph"); Near(14.268342f, ring.Caps[0].rho, Tol, "rho"); Near(new Vector3(7.952866f, 2.54985f, -35.839744f), pos[2], Tol, "p2@20");
                        Assert.AreEqual(8, ring.Caps[1].Id, "cap1 id@20"); Near(-1.881062f, ring.Caps[1].th, Tol, "th"); Near(3.437463f, ring.Caps[1].ph, Tol, "ph"); Near(14.268342f, ring.Caps[1].rho, Tol, "rho"); Near(new Vector3(-2.54985f, 7.952866f, -35.839744f), pos[8], Tol, "p8@20");
                        Assert.AreEqual(14, ring.Caps[2].Id, "cap2 id@20"); Near(2.831327f, ring.Caps[2].th, Tol, "th"); Near(3.437463f, ring.Caps[2].ph, Tol, "ph"); Near(14.268342f, ring.Caps[2].rho, Tol, "rho"); Near(new Vector3(-7.952866f, -2.54985f, -35.839744f), pos[14], Tol, "p14@20");
                        break;
                    case 40:
                        Near(new Vector3(0.0f, 0.0f, -80.0f), ring.C, Tol, "c@40"); Assert.AreEqual(24, ring.Caps.Count, "carried@40");
                        Assert.AreEqual(2, ring.Caps[0].Id, "cap0 id@40"); Near(-0.043599f, ring.Caps[0].th, Tol, "th"); Near(5.770796f, ring.Caps[0].ph, Tol, "ph"); Near(8.462603f, ring.Caps[0].rho, Tol, "rho"); Near(new Vector3(29.347885f, 1.280343f, -75.851117f), pos[2], Tol, "p2@40");
                        Assert.AreEqual(8, ring.Caps[1].Id, "cap1 id@40"); Near(-1.614395f, ring.Caps[1].th, Tol, "th"); Near(5.770796f, ring.Caps[1].ph, Tol, "ph"); Near(8.462603f, ring.Caps[1].rho, Tol, "rho"); Near(new Vector3(-1.280343f, 29.347885f, -75.851117f), pos[8], Tol, "p8@40");
                        Assert.AreEqual(14, ring.Caps[2].Id, "cap2 id@40"); Near(3.097994f, ring.Caps[2].th, Tol, "th"); Near(5.770796f, ring.Caps[2].ph, Tol, "ph"); Near(8.462603f, ring.Caps[2].rho, Tol, "rho"); Near(new Vector3(-29.347885f, -1.280343f, -75.851117f), pos[14], Tol, "p14@40");
                        break;
                    case 60:
                        Near(new Vector3(0.0f, 0.0f, -120.0f), ring.C, Tol, "c@60"); Assert.AreEqual(24, ring.Caps.Count, "carried@60");
                        Assert.AreEqual(2, ring.Caps[0].Id, "cap0 id@60"); Near(0.223068f, ring.Caps[0].th, Tol, "th"); Near(8.10413f, ring.Caps[0].ph, Tol, "ph"); Near(5.939439f, ring.Caps[0].rho, Tol, "rho"); Near(new Vector3(20.02105f, -4.541634f, -125.754579f), pos[2], Tol, "p2@60");
                        Assert.AreEqual(8, ring.Caps[1].Id, "cap1 id@60"); Near(-1.347728f, ring.Caps[1].th, Tol, "th"); Near(8.10413f, ring.Caps[1].ph, Tol, "ph"); Near(5.939439f, ring.Caps[1].rho, Tol, "rho"); Near(new Vector3(4.541634f, 20.02105f, -125.754579f), pos[8], Tol, "p8@60");
                        Assert.AreEqual(14, ring.Caps[2].Id, "cap2 id@60"); Near(3.364661f, ring.Caps[2].th, Tol, "th"); Near(8.10413f, ring.Caps[2].ph, Tol, "ph"); Near(5.939439f, ring.Caps[2].rho, Tol, "rho"); Near(new Vector3(-20.02105f, 4.541634f, -125.754579f), pos[14], Tol, "p14@60");
                        break;
                }
            }
        }

        [Test]
        public void Mulberry32_IsTheLabsBitForBit()
        {
            var r = new PistolAI.Mulberry32(31337u + 17u);
            Assert.AreEqual(0.061401f, r.Next(), 1e-6f, "draw 0");
            Assert.AreEqual(0.247076f, r.Next(), 1e-6f, "draw 1");
            Assert.AreEqual(0.468126f, r.Next(), 1e-6f, "draw 2");
            Assert.AreEqual(0.594488f, r.Next(), 1e-6f, "draw 3");
        }

        // the lab's pistolScan for the AI goldens: in reach of the Pistol, on its screen, in its band (or primed)
        static void Scan(List<PistolAI.Seen> into, PistolRibbon rb, PistolBodyMath.State p, Vector3[] w, bool[] primed, bool[] friendly, in PistolAI.Settings s)
        {
            float reach = s.pLaunchRange + s.pVision + 60f;
            for (int i = 0; i < w.Length; i++) if (PistolAI.TrySee(rb, p, i, w[i], primed[i], friendly[i], reach, s, out var seen)) into.Add(seen);
        }

        [Test]
        public void PistolAI_Hard_MatchesTheLab()
        {
            var c = Shipped(); var rb = Build(PistolUnroll.WorldUp, c.Lay); var s = c.AI; Object.DestroyImmediate(c);
            float px = 100f, py = rb.Ground(px), gy = rb.Ground(200f);
            Near(52.009862f, py, Tol, "pistol y");
            // a cluster of 8 at x ~ 200 (prism 5 primed), and a friendly block (the Goby's trail) just ahead of the Pistol
            var w = new Vector3[9]; var primed = new bool[9]; var friendly = new bool[9];
            for (int i = 0; i < 8; i++) { w[i] = rb.Unmap(200f + (i % 4) * 6f, gy + 20f + (i / 4) * 6f, ((i % 3) - 1) * 10f); primed[i] = i == 5; }
            w[8] = rb.Unmap(130f, py + 5f, 0f); friendly[8] = true;
            Near(new Vector3(81.676056f, 13.642745f, -153.209681f), w[0], Tol, "prism0");
            Near(new Vector3(87.495381f, 13.642745f, -163.104903f), w[1], Tol, "prism1");
            Near(new Vector3(92.447326f, 13.642745f, -173.823068f), w[2], Tol, "prism2");
            Near(new Vector3(72.7843f, 13.642745f, -166.933402f), w[3], Tol, "prism3");
            Near(new Vector3(90.500795f, 19.642745f, -157.913296f), w[4], Tol, "prism4");
            Near(new Vector3(96.014729f, 19.642745f, -168.341382f), w[5], Tol, "prism5");
            Near(new Vector3(76.000227f, 19.642745f, -162.443572f), w[6], Tol, "prism6");
            Near(new Vector3(80.727372f, 19.642745f, -173.008565f), w[7], Tol, "prism7");
            Near(new Vector3(91.565137f, 57.009862f, -90.971223f), w[8], Tol, "prism8");
            var p = new PistolBodyMath.State { x = px, y = py, v = 60f, launch = 2 }; p.wpos = rb.Unmap(px, py, 0f);
            var ai = new PistolAI(31337u + 17u); int scans = 0;
            {
                var o = ai.Think(p, rb, 0.05f, PistolAI.Difficulty.Hard, s, l => { scans++; Scan(l, rb, p, w, primed, friendly, s); });
                Assert.AreEqual(9, ai.Vis.Count, "seen@0");
                Assert.AreEqual(0.0f, o.Dive, 1e-6f, "dive@0");
                Assert.IsTrue(o.HasLaunch, "launch@0"); Near(106.0f, o.Launch.x, Tol, "launch dx@0"); Near(-32.367117f, o.Launch.y, Tol, "launch dy@0");
                Assert.AreEqual(true, o.Ghost, "ghost@0");
                Assert.IsTrue(o.Snap, "snap@0"); Near(100.0f, o.SnapAim.x, Tol, "snap dx@0"); Near(-38.367117f, o.SnapAim.y, Tol, "snap dy@0");
            }
            {
                var o = ai.Think(p, rb, 0.4f, PistolAI.Difficulty.Hard, s, l => { scans++; Scan(l, rb, p, w, primed, friendly, s); });
                Assert.AreEqual(9, ai.Vis.Count, "seen@1");
                Assert.AreEqual(0.0f, o.Dive, 1e-6f, "dive@1");
                Assert.IsTrue(o.HasLaunch, "launch@1"); Near(106.0f, o.Launch.x, Tol, "launch dx@1"); Near(-32.367117f, o.Launch.y, Tol, "launch dy@1");
                Assert.AreEqual(false, o.Ghost, "ghost@1");
                Assert.IsFalse(o.Snap, "snap@1");
            }
            {
                var o = ai.Think(p, rb, 1.0f, PistolAI.Difficulty.Hard, s, l => { scans++; Scan(l, rb, p, w, primed, friendly, s); });
                Assert.AreEqual(9, ai.Vis.Count, "seen@2");
                Assert.AreEqual(0.0f, o.Dive, 1e-6f, "dive@2");
                Assert.IsTrue(o.HasLaunch, "launch@2"); Near(106.0f, o.Launch.x, Tol, "launch dx@2"); Near(-32.367117f, o.Launch.y, Tol, "launch dy@2");
                Assert.AreEqual(false, o.Ghost, "ghost@2");
                Assert.IsFalse(o.Snap, "snap@2");
            }
            Assert.AreEqual(3, scans, "one scan per thought (0.1 s apart or more)");
        }

        [Test]
        public void PistolAI_Medium_MatchesTheLab()
        {
            var c = Shipped(); var rb = Build(PistolUnroll.WorldUp, c.Lay); var s = c.AI; Object.DestroyImmediate(c);
            float px = 100f, py = rb.Ground(px), gy = rb.Ground(200f);
            Near(52.009862f, py, Tol, "pistol y");
            // a cluster of 8 at x ~ 200 (prism 5 primed), and a friendly block (the Goby's trail) just ahead of the Pistol
            var w = new Vector3[9]; var primed = new bool[9]; var friendly = new bool[9];
            for (int i = 0; i < 8; i++) { w[i] = rb.Unmap(200f + (i % 4) * 6f, gy + 20f + (i / 4) * 6f, ((i % 3) - 1) * 10f); primed[i] = i == 5; }
            w[8] = rb.Unmap(130f, py + 5f, 0f); friendly[8] = true;
            Near(new Vector3(81.676056f, 13.642745f, -153.209681f), w[0], Tol, "prism0");
            Near(new Vector3(87.495381f, 13.642745f, -163.104903f), w[1], Tol, "prism1");
            Near(new Vector3(92.447326f, 13.642745f, -173.823068f), w[2], Tol, "prism2");
            Near(new Vector3(72.7843f, 13.642745f, -166.933402f), w[3], Tol, "prism3");
            Near(new Vector3(90.500795f, 19.642745f, -157.913296f), w[4], Tol, "prism4");
            Near(new Vector3(96.014729f, 19.642745f, -168.341382f), w[5], Tol, "prism5");
            Near(new Vector3(76.000227f, 19.642745f, -162.443572f), w[6], Tol, "prism6");
            Near(new Vector3(80.727372f, 19.642745f, -173.008565f), w[7], Tol, "prism7");
            Near(new Vector3(91.565137f, 57.009862f, -90.971223f), w[8], Tol, "prism8");
            var p = new PistolBodyMath.State { x = px, y = py, v = 60f, launch = 2 }; p.wpos = rb.Unmap(px, py, 0f);
            var ai = new PistolAI(31337u + 17u); int scans = 0;
            {
                var o = ai.Think(p, rb, 0.05f, PistolAI.Difficulty.Medium, s, l => { scans++; Scan(l, rb, p, w, primed, friendly, s); });
                Assert.AreEqual(9, ai.Vis.Count, "seen@0");
                Assert.AreEqual(0.0f, o.Dive, 1e-6f, "dive@0");
                Assert.IsFalse(o.HasLaunch, "launch@0");
                Assert.AreEqual(true, o.Ghost, "ghost@0");
                Assert.IsTrue(o.Snap, "snap@0"); Near(100.0f, o.SnapAim.x, Tol, "snap dx@0"); Near(-38.367117f, o.SnapAim.y, Tol, "snap dy@0");
            }
            {
                var o = ai.Think(p, rb, 0.4f, PistolAI.Difficulty.Medium, s, l => { scans++; Scan(l, rb, p, w, primed, friendly, s); });
                Assert.AreEqual(9, ai.Vis.Count, "seen@1");
                Assert.AreEqual(0.0f, o.Dive, 1e-6f, "dive@1");
                Assert.IsTrue(o.HasLaunch, "launch@1"); Near(106.0f, o.Launch.x, Tol, "launch dx@1"); Near(-32.367117f, o.Launch.y, Tol, "launch dy@1");
                Assert.AreEqual(false, o.Ghost, "ghost@1");
                Assert.IsFalse(o.Snap, "snap@1");
            }
            {
                var o = ai.Think(p, rb, 1.0f, PistolAI.Difficulty.Medium, s, l => { scans++; Scan(l, rb, p, w, primed, friendly, s); });
                Assert.AreEqual(9, ai.Vis.Count, "seen@2");
                Assert.AreEqual(0.0f, o.Dive, 1e-6f, "dive@2");
                Assert.IsTrue(o.HasLaunch, "launch@2"); Near(106.0f, o.Launch.x, Tol, "launch dx@2"); Near(-32.367117f, o.Launch.y, Tol, "launch dy@2");
                Assert.AreEqual(false, o.Ghost, "ghost@2");
                Assert.IsFalse(o.Snap, "snap@2");
            }
            Assert.AreEqual(3, scans, "one scan per thought (0.1 s apart or more)");
        }

        [Test]
        public void PistolAI_Easy_MatchesTheLab()
        {
            var c = Shipped(); var rb = Build(PistolUnroll.WorldUp, c.Lay); var s = c.AI; Object.DestroyImmediate(c);
            float px = 100f, py = rb.Ground(px), gy = rb.Ground(200f);
            Near(52.009862f, py, Tol, "pistol y");
            // a cluster of 8 at x ~ 200 (prism 5 primed), and a friendly block (the Goby's trail) just ahead of the Pistol
            var w = new Vector3[9]; var primed = new bool[9]; var friendly = new bool[9];
            for (int i = 0; i < 8; i++) { w[i] = rb.Unmap(200f + (i % 4) * 6f, gy + 20f + (i / 4) * 6f, ((i % 3) - 1) * 10f); primed[i] = i == 5; }
            w[8] = rb.Unmap(130f, py + 5f, 0f); friendly[8] = true;
            Near(new Vector3(81.676056f, 13.642745f, -153.209681f), w[0], Tol, "prism0");
            Near(new Vector3(87.495381f, 13.642745f, -163.104903f), w[1], Tol, "prism1");
            Near(new Vector3(92.447326f, 13.642745f, -173.823068f), w[2], Tol, "prism2");
            Near(new Vector3(72.7843f, 13.642745f, -166.933402f), w[3], Tol, "prism3");
            Near(new Vector3(90.500795f, 19.642745f, -157.913296f), w[4], Tol, "prism4");
            Near(new Vector3(96.014729f, 19.642745f, -168.341382f), w[5], Tol, "prism5");
            Near(new Vector3(76.000227f, 19.642745f, -162.443572f), w[6], Tol, "prism6");
            Near(new Vector3(80.727372f, 19.642745f, -173.008565f), w[7], Tol, "prism7");
            Near(new Vector3(91.565137f, 57.009862f, -90.971223f), w[8], Tol, "prism8");
            var p = new PistolBodyMath.State { x = px, y = py, v = 60f, launch = 2 }; p.wpos = rb.Unmap(px, py, 0f);
            var ai = new PistolAI(31337u + 17u); int scans = 0;
            {
                var o = ai.Think(p, rb, 0.05f, PistolAI.Difficulty.Easy, s, l => { scans++; Scan(l, rb, p, w, primed, friendly, s); });
                Assert.AreEqual(9, ai.Vis.Count, "seen@0");
                Assert.AreEqual(0.0f, o.Dive, 1e-6f, "dive@0");
                Assert.IsFalse(o.HasLaunch, "launch@0");
                Assert.AreEqual(true, o.Ghost, "ghost@0");
                Assert.IsTrue(o.Snap, "snap@0"); Near(100.0f, o.SnapAim.x, Tol, "snap dx@0"); Near(-38.367117f, o.SnapAim.y, Tol, "snap dy@0");
            }
            {
                var o = ai.Think(p, rb, 0.4f, PistolAI.Difficulty.Easy, s, l => { scans++; Scan(l, rb, p, w, primed, friendly, s); });
                Assert.AreEqual(9, ai.Vis.Count, "seen@1");
                Assert.AreEqual(0.0f, o.Dive, 1e-6f, "dive@1");
                Assert.IsTrue(o.HasLaunch, "launch@1"); Near(106.0f, o.Launch.x, Tol, "launch dx@1"); Near(-32.367117f, o.Launch.y, Tol, "launch dy@1");
                Assert.AreEqual(false, o.Ghost, "ghost@1");
                Assert.IsFalse(o.Snap, "snap@1");
            }
            {
                var o = ai.Think(p, rb, 1.0f, PistolAI.Difficulty.Easy, s, l => { scans++; Scan(l, rb, p, w, primed, friendly, s); });
                Assert.AreEqual(9, ai.Vis.Count, "seen@2");
                Assert.AreEqual(0.0f, o.Dive, 1e-6f, "dive@2");
                Assert.IsTrue(o.HasLaunch, "launch@2"); Near(106.0f, o.Launch.x, Tol, "launch dx@2"); Near(-32.367117f, o.Launch.y, Tol, "launch dy@2");
                Assert.AreEqual(false, o.Ghost, "ghost@2");
                Assert.IsFalse(o.Snap, "snap@2");
            }
            Assert.AreEqual(3, scans, "one scan per thought (0.1 s apart or more)");
        }

        // ---- the view: the lab's own lap of radius 29 (1.2 laps, 400 steps laid 4 u a sample), its boxes, zones and parallax
        static PistolRibbon LabLap()
        {
            const float R = 29f; var lay = Shipped().Lay;
            Vector3 On(float th) => new(R * Mathf.Cos(th), 0f, R * Mathf.Sin(th));
            Vector3 Tan(float th) => new(-Mathf.Sin(th), 0f, Mathf.Cos(th));
            var rb = new PistolRibbon(PistolUnroll.WorldUp); var f0 = Tan(0f);
            rb.Begin(On(0f), f0, Vector3.up, Vector3.Cross(f0, Vector3.up).normalized, lay);
            const int steps = 400; float total = 2f * Mathf.PI * 1.2f;
            for (int i = 1; i <= steps; i++) { float a = (i - 1) * total / steps, b = i * total / steps; var f = Tan(b); rb.Lay(On(a), On(b), f, Vector3.up, Vector3.Cross(f, Vector3.up).normalized, lay); }
            return rb;
        }

        [Test]
        public void View_LabLap_MatchesTheLab()
        {
            var rb = LabLap();
            Assert.AreEqual(55, rb.Count, "samples"); Near(215.825622f, rb.X(rb.Count - 1), Tol, "last x"); Near(218.477233f, rb.GobyX, Tol, "the Goby's x");
        }

        [Test]
        public void View_Box0_r6_MatchesTheLab()
        {
            var rb = LabLap(); var lay = Shipped().Lay; const float hs = 2f; float hd = hs * Mathf.Sqrt(3f);
            var corners = new Vector3[8]; var xhd = new Vector3[8]; Vector3 c = new(-6f, 0f, 0f);
            PistolWarp.BoxCorners(c, Vector3.right * hs, Vector3.up * hs, Vector3.forward * hs, corners);
            var m = rb.Map(c, -1e9f, 1e9f); Assert.IsTrue(m.Ok);
            Near(90.161794f, m.X, Tol, "centre x"); Near(0.0f, m.H, Tol, "centre h"); Near(22.934777f, m.D, Tol, "centre d"); Assert.AreEqual(22, m.K, "centre segment");
            Near(0.034548f, rb.Curvature(m.K), 2e-4f, "curvature");
            var kind = PistolWarp.Classify(hd, rb.Curvature(m.K), m.D, lay.SampleGap, rb.Count, out int window);
            Assert.AreEqual(PistolWarp.Kind.Bend, kind); Assert.AreEqual(7, window, "window");
            Near(57.36531f, PistolWarp.TearBound(hd, rb.Curvature(m.K), m.D, lay.SampleGap), 0.05f, "tear bound");
            Assert.IsTrue(PistolWarp.WarpBox(rb, m.K, corners, -1e9f, 1e9f, window, xhd));
            Near(new Vector3(97.975587f, -2.0f, 20.684325f), xhd[0], 0.05f, "corner 0");
            Near(new Vector3(105.692696f, -2.0f, 24.463406f), xhd[1], 0.05f, "corner 1");
            Near(new Vector3(97.975587f, 2.0f, 20.684325f), xhd[2], 0.05f, "corner 2");
            Near(new Vector3(105.692696f, 2.0f, 24.463406f), xhd[3], 0.05f, "corner 3");
            Near(new Vector3(82.502038f, -2.0f, 20.703313f), xhd[4], 0.05f, "corner 4");
            Near(new Vector3(77.890655f, -2.0f, 24.458575f), xhd[5], 0.05f, "corner 5");
            Near(new Vector3(82.502038f, 2.0f, 20.703313f), xhd[6], 0.05f, "corner 6");
            Near(new Vector3(77.890655f, 2.0f, 24.458575f), xhd[7], 0.05f, "corner 7");
        }

        [Test]
        public void View_Box1_r10_MatchesTheLab()
        {
            var rb = LabLap(); var lay = Shipped().Lay; const float hs = 2f; float hd = hs * Mathf.Sqrt(3f);
            var corners = new Vector3[8]; var xhd = new Vector3[8]; Vector3 c = new(-10f, 0f, 0f);
            PistolWarp.BoxCorners(c, Vector3.right * hs, Vector3.up * hs, Vector3.forward * hs, corners);
            var m = rb.Map(c, -1e9f, 1e9f); Assert.IsTrue(m.Ok);
            Near(90.31313f, m.X, Tol, "centre x"); Near(0.0f, m.H, Tol, "centre h"); Near(18.937641f, m.D, Tol, "centre d"); Assert.AreEqual(22, m.K, "centre segment");
            Near(0.034548f, rb.Curvature(m.K), 2e-4f, "curvature");
            var kind = PistolWarp.Classify(hd, rb.Curvature(m.K), m.D, lay.SampleGap, rb.Count, out int window);
            Assert.AreEqual(PistolWarp.Kind.Bend, kind); Assert.AreEqual(5, window, "window");
            Near(44.038744f, PistolWarp.TearBound(hd, rb.Curvature(m.K), m.D, lay.SampleGap), 0.05f, "tear bound");
            Assert.IsTrue(PistolWarp.WarpBox(rb, m.K, corners, -1e9f, 1e9f, window, xhd));
            Near(new Vector3(94.718663f, -2.0f, 16.790302f), xhd[0], 0.05f, "corner 0");
            Near(new Vector3(97.975587f, -2.0f, 20.684325f), xhd[1], 0.05f, "corner 1");
            Near(new Vector3(94.718663f, 2.0f, 16.790302f), xhd[2], 0.05f, "corner 2");
            Near(new Vector3(97.975587f, 2.0f, 20.684325f), xhd[3], 0.05f, "corner 3");
            Near(new Vector3(86.058427f, -2.0f, 16.765687f), xhd[4], 0.05f, "corner 4");
            Near(new Vector3(82.502038f, -2.0f, 20.703313f), xhd[5], 0.05f, "corner 5");
            Near(new Vector3(86.058427f, 2.0f, 16.765687f), xhd[6], 0.05f, "corner 6");
            Near(new Vector3(82.502038f, 2.0f, 20.703313f), xhd[7], 0.05f, "corner 7");
        }

        [Test]
        public void View_Box2_r20_MatchesTheLab()
        {
            var rb = LabLap(); var lay = Shipped().Lay; const float hs = 2f; float hd = hs * Mathf.Sqrt(3f);
            var corners = new Vector3[8]; var xhd = new Vector3[8]; Vector3 c = new(-20f, 0f, 0f);
            PistolWarp.BoxCorners(c, Vector3.right * hs, Vector3.up * hs, Vector3.forward * hs, corners);
            var m = rb.Map(c, -1e9f, 1e9f); Assert.IsTrue(m.Ok);
            Near(90.691471f, m.X, Tol, "centre x"); Near(0.0f, m.H, Tol, "centre h"); Near(8.944801f, m.D, Tol, "centre d"); Assert.AreEqual(22, m.K, "centre segment");
            Near(0.034548f, rb.Curvature(m.K), 2e-4f, "curvature");
            var kind = PistolWarp.Classify(hd, rb.Curvature(m.K), m.D, lay.SampleGap, rb.Count, out int window);
            Assert.AreEqual(PistolWarp.Kind.Bend, kind); Assert.AreEqual(4, window, "window");
            Near(34.026719f, PistolWarp.TearBound(hd, rb.Curvature(m.K), m.D, lay.SampleGap), 0.05f, "tear bound");
            Assert.IsTrue(PistolWarp.WarpBox(rb, m.K, corners, -1e9f, 1e9f, window, xhd));
            Near(new Vector3(93.721674f, -2.0f, 6.840125f), xhd[0], 0.05f, "corner 0");
            Near(new Vector3(94.12047f, -2.0f, 10.820196f), xhd[1], 0.05f, "corner 1");
            Near(new Vector3(93.721674f, 2.0f, 6.840125f), xhd[2], 0.05f, "corner 2");
            Near(new Vector3(94.12047f, 2.0f, 10.820196f), xhd[3], 0.05f, "corner 3");
            Near(new Vector3(88.768571f, -2.0f, 6.870564f), xhd[4], 0.05f, "corner 4");
            Near(new Vector3(87.110751f, -2.0f, 10.85869f), xhd[5], 0.05f, "corner 5");
            Near(new Vector3(88.768571f, 2.0f, 6.870564f), xhd[6], 0.05f, "corner 6");
            Near(new Vector3(87.110751f, 2.0f, 10.85869f), xhd[7], 0.05f, "corner 7");
        }

        [Test]
        public void View_Box3_r0_MatchesTheLab()
        {
            var rb = LabLap(); var lay = Shipped().Lay; const float hs = 3f; float hd = hs * Mathf.Sqrt(3f);
            var corners = new Vector3[8]; var xhd = new Vector3[8]; Vector3 c = new(0f, 0f, 0f);
            PistolWarp.BoxCorners(c, Vector3.right * hs, Vector3.up * hs, Vector3.forward * hs, corners);
            var m = rb.Map(c, -1e9f, 1e9f); Assert.IsTrue(m.Ok);
            Near(28.929904f, Mathf.Abs(m.D), Tol, "the centre is R beside the path");
            Near(0.034446f, rb.Curvature(m.K), 2e-4f, "curvature");
            var kind = PistolWarp.Classify(hd, rb.Curvature(m.K), m.D, lay.SampleGap, rb.Count, out int window);
            Assert.AreEqual(PistolWarp.Kind.Bar, kind); Assert.AreEqual(rb.Count, window, "the window is everything");
            float rho = 1f / Mathf.Abs(rb.Curvature(m.K));
            rb.LapExtent(c, m.K, rho + hd + 2f, -1e9f, 1e9f, out int kLo, out int kHi, out bool loByDist, out bool hiByDist);
            Assert.AreEqual(0, kLo); Assert.AreEqual(53, kHi); Assert.AreEqual(false, loByDist); Assert.AreEqual(false, hiByDist);
            PistolWarp.BarExtent(rb, c, m.K, rho, hd, corners, -1e9f, 1e9f, out float xA, out float xB);
            Near(0.0f, xA, Tol, "bar xA"); Near(215.825622f, xB, Tol, "bar xB");
        }

        [Test]
        public void View_Zones_MatchTheLab()
        {
            var c = Shipped(); float band = c.Vision + 4f, vision = c.Vision, D = c.Parallax; Object.DestroyImmediate(c);
            { var z = PistolWarp.ZoneOf(0f, band, vision, D, false); Assert.IsTrue(z.Drawn, "d=0 drawn"); Near(1.0f, z.Reach, 1e-4f, "reach@0"); Near(0.0f, z.Fog, 1e-4f, "fog@0"); Near(1.0f, z.Alpha, 1e-4f, "alpha@0"); Assert.AreEqual(true, z.Solid, "solid@0"); Assert.AreEqual(true, z.Warped, "warped@0"); }
            { var z = PistolWarp.ZoneOf(49f, band, vision, D, false); Assert.IsTrue(z.Drawn, "d=49 drawn"); Near(1.0f, z.Reach, 1e-4f, "reach@49"); Near(0.0f, z.Fog, 1e-4f, "fog@49"); Near(1.0f, z.Alpha, 1e-4f, "alpha@49"); Assert.AreEqual(true, z.Solid, "solid@49"); Assert.AreEqual(true, z.Warped, "warped@49"); }
            { var z = PistolWarp.ZoneOf(55f, band, vision, D, false); Assert.IsTrue(z.Drawn, "d=55 drawn"); Near(0.5f, z.Reach, 1e-4f, "reach@55"); Near(0.024419f, z.Fog, 1e-4f, "fog@55"); Near(1.0f, z.Alpha, 1e-4f, "alpha@55"); Assert.AreEqual(true, z.Solid, "solid@55"); Assert.AreEqual(true, z.Warped, "warped@55"); }
            { var z = PistolWarp.ZoneOf(61f, band, vision, D, false); Assert.IsTrue(z.Drawn, "d=61 drawn"); Near(0.0f, z.Reach, 1e-4f, "reach@61"); Near(0.048837f, z.Fog, 1e-4f, "fog@61"); Near(1.0f, z.Alpha, 1e-4f, "alpha@61"); Assert.AreEqual(true, z.Solid, "solid@61"); Assert.AreEqual(true, z.Warped, "warped@61"); }
            { var z = PistolWarp.ZoneOf(100f, band, vision, D, false); Assert.IsTrue(z.Drawn, "d=100 drawn"); Near(0.0f, z.Reach, 1e-4f, "reach@100"); Near(0.207558f, z.Fog, 1e-4f, "fog@100"); Near(1.0f, z.Alpha, 1e-4f, "alpha@100"); Assert.AreEqual(true, z.Solid, "solid@100"); Assert.AreEqual(true, z.Warped, "warped@100"); }
            { var z = PistolWarp.ZoneOf(135f, band, vision, D, false); Assert.IsTrue(z.Drawn, "d=135 drawn"); Near(0.0f, z.Reach, 1e-4f, "reach@135"); Near(0.35f, z.Fog, 1e-4f, "fog@135"); Near(1.0f, z.Alpha, 1e-4f, "alpha@135"); Assert.AreEqual(true, z.Solid, "solid@135"); Assert.AreEqual(true, z.Warped, "warped@135"); }
            { var z = PistolWarp.ZoneOf(136f, band, vision, D, false); Assert.IsTrue(z.Drawn, "d=136 drawn"); Near(0.0f, z.Reach, 1e-4f, "reach@136"); Near(0.351449f, z.Fog, 1e-4f, "fog@136"); Near(0.998406f, z.Alpha, 1e-4f, "alpha@136"); Assert.AreEqual(false, z.Solid, "solid@136"); Assert.AreEqual(false, z.Warped, "warped@136"); }
            { var z = PistolWarp.ZoneOf(480f, band, vision, D, false); Assert.IsTrue(z.Drawn, "d=480 drawn"); Near(0.0f, z.Reach, 1e-4f, "reach@480"); Near(0.85f, z.Fog, 1e-4f, "fog@480"); Near(0.45f, z.Alpha, 1e-4f, "alpha@480"); Assert.AreEqual(false, z.Solid, "solid@480"); Assert.AreEqual(false, z.Warped, "warped@480"); }
            { var z = PistolWarp.ZoneOf(-49f, band, vision, D, false); Assert.IsTrue(z.Drawn, "d=-49 drawn"); Near(1.0f, z.Reach, 1e-4f, "reach@-49"); Near(0.0f, z.Fog, 1e-4f, "fog@-49"); Near(1.0f, z.Alpha, 1e-4f, "alpha@-49"); Assert.AreEqual(true, z.Solid, "solid@-49"); Assert.AreEqual(true, z.Warped, "warped@-49"); }
            { var z = PistolWarp.ZoneOf(-80f, band, vision, D, false); Assert.IsTrue(z.Drawn, "d=-80 drawn"); Near(0.0f, z.Reach, 1e-4f, "reach@-80"); Near(0.15f, z.Fog, 1e-4f, "fog@-80"); Near(0.08f, z.Alpha, 1e-4f, "alpha@-80"); Assert.AreEqual(false, z.Solid, "solid@-80"); Assert.AreEqual(true, z.Warped, "warped@-80"); }
        }

        [Test]
        public void View_Parallax_MatchesTheLab()
        {
            Near(new Vector3(-92.0f, 37.0f, -80.0f), PistolWarp.Parallax(100f, 27f, -80f, 192f, 40f, 100f, 17f, 160f), 1e-3f, "parallax@-80");
            Near(new Vector3(-92.0f, 31.414414f, -49.0f), PistolWarp.Parallax(100f, 27f, -49f, 192f, 40f, 100f, 17f, 160f), 1e-3f, "parallax@-49");
            Near(new Vector3(-92.0f, 27.0f, 0.0f), PistolWarp.Parallax(100f, 27f, 0f, 192f, 40f, 100f, 17f, 160f), 1e-3f, "parallax@0");
            Near(new Vector3(-92.0f, 24.655502f, 49.0f), PistolWarp.Parallax(100f, 27f, 49f, 192f, 40f, 100f, 17f, 160f), 1e-3f, "parallax@49");
            Near(new Vector3(-92.0f, 22.0f, 160.0f), PistolWarp.Parallax(100f, 27f, 160f, 192f, 40f, 100f, 17f, 160f), 1e-3f, "parallax@160");
            Near(new Vector3(-92.0f, 19.5f, 480.0f), PistolWarp.Parallax(100f, 27f, 480f, 192f, 40f, 100f, 17f, 160f), 1e-3f, "parallax@480");
        }

    }
}
#endif
