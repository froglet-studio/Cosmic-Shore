#!/usr/bin/env python3
"""Write Assets/_Scripts/Tests/Editor/GobyPistolMathTests.cs from the lab's goldens (never type a golden by hand):

    NODE_PATH=<playwright> node Tools/Build/goby_pistol_goldens.cjs --three <three.min.js> > goldens.json
    python3 Tools/Build/goby_pistol_goldens_to_tests.py goldens.json > Assets/_Scripts/Tests/Editor/GobyPistolMathTests.cs
"""
import json, sys
g = json.load(open(sys.argv[1]))
def f(v): 
    s = repr(float(v)); return s + 'f'
def v3(a): return 'new Vector3(%s, %s, %s)' % tuple(f(x) for x in a)
modes = {'world': 'PistolUnroll.WorldUp', 'arc': 'PistolUnroll.ArcLength', 'goby': 'PistolUnroll.GobyUp'}
o = []
w = o.append
w('''#if UNITY_EDITOR
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
            var c = Shipped(); var b = c.Body; var v = c.Vortex; var l = c.Lay; var ai = c.AI;''')
sh = g['shipped']
for k in ['pGravity','pDiveMul','pMinSpeed','pDrag','pGoodLand','pLaunchRange','pLaunchSpeed','pLaunchCharges','pLaunchCd','pGhostTime','pGhostCd','pVortexCd']:
    w('            Assert.AreEqual(%s, b.%s, 1e-6f, "%s");' % (f(sh[k]), k, k))
for k in ['pVortexR','pVortexSpeed','pVortexLife','pVortexCapture','pVortexSpin','pVortexMax','pVortexCollapse']:
    w('            Assert.AreEqual(%s, v.%s, 1e-6f, "%s");' % (f(sh[k]), k, k))
for k, n in [('tSampleGap','SampleGap'),('tBlockGap','BlockGap'),('tHillEvery','HillEvery'),('tHillAmp','HillAmp')]:
    w('            Assert.AreEqual(%s, l.%s, 1e-6f, "%s");' % (f(sh[k]), n, k))
w('            Assert.AreEqual(%s, ai.aiHighDive, 1e-6f, "aiHighDive");' % f(sh['aiHighDive']))
for k, n in [('gPrimerCharges','PrimerCharges'),('gPrimerCd','PrimerCd'),('gPrimerRange','PrimerRange'),('gPrimeSplash','PrimeSplash'),('gPrimeLife','PrimeLife'),('gFlareCharges','FlareCharges'),('gFlareCd','FlareCd'),('gFlareRange','FlareRange'),('gFlareMax','FlareMax'),('gFlareSpeed','FlareSpeed'),('pVision','Vision'),('pBodyR','BodyR'),('pViewWidth','ViewWidth'),('pParallax','Parallax'),('pChain','Chain'),('pHotRing','HotRing'),('pHotLife','HotLife')]:
    w('            Assert.AreEqual(%s, c.%s, 1e-6f, "%s");' % (f(sh[k]), n, k))
w('            Object.DestroyImmediate(c);\n        }\n')
for mode, cs in modes.items():
    r = g['ribbon'][mode]; name = {'world':'WorldUp','arc':'ArcLength','goby':'GobyUp'}[mode]
    w('        [Test]\n        public void Ribbon_%s_MatchesTheLab()\n        {' % name)
    w('            var c = Shipped(); var rb = Build(%s, c.Lay); Object.DestroyImmediate(c);' % cs)
    w('            Assert.AreEqual(%d, rb.Count, "samples");' % r['n'])
    w('            Assert.AreEqual(%d, rb.CX.Count, "hinges (control points before the end at the Goby)");' % r['cps'])
    w('            Assert.AreEqual(%d, rb.Blocks.Count, "blocks");' % r['blocks'])
    w('            Near(%s, rb.X(rb.Count - 1), Tol, "last x"); Near(%s, rb.EndX, Tol, "end x"); Near(%s, rb.EndY, Tol, "end y");' % (f(r['lastX']), f(r['endX']), f(r['endY'])))
    for i, (x, y) in enumerate(r['cp']):
        w('            Near(%s, rb.CX[%d], Tol, "cp%d x"); Near(%s, rb.CY[%d], Tol, "cp%d y");' % (f(x), i, i, f(y), i, i))
    for i, m in enumerate(r['map']):
        w('            { var m = rb.Map(%s, -1e9f, 1e9f); Assert.IsTrue(m.Ok); Near(%s, m.X, Tol, "map%d x"); Near(%s, m.H, Tol, "map%d h"); Near(%s, m.D, Tol, "map%d d"); Assert.AreEqual(%s, m.Ambiguous, "map%d ambiguous"); }' % (v3(m['q']), f(m['x']), i, f(m['h']), i, f(m['d']), i, 'true' if m['amb'] else 'false', i))
    for i, u in enumerate(r['unmap']):
        w('            Near(%s, rb.Unmap(%s, %s, %s), Tol, "unmap%d");' % (v3(u['w']), f(u['x']), f(u['h']), f(u['d']), i))
    for i, gr in enumerate(r['ground']):
        w('            { float y = rb.Ground(%s, out float s); Near(%s, y, Tol, "ground%d"); Near(%s, s, Tol, "slope%d"); }' % (f(gr['x']), f(gr['y']), i, f(gr['slope']), i))
    for i, fr in enumerate(r['frames']):
        w('            { rb.FrameAt(%s, out var T, out var U, out var R); Near(%s, T, Tol, "T%d"); Near(%s, U, Tol, "U%d"); Near(%s, R, Tol, "R%d"); }' % (f(fr['x']), v3(fr['T']), i, v3(fr['U']), i, v3(fr['R']), i))
    w('        }\n')
# body
w('''        [Test]
        public void PistolBody_RideAndLaunch_MatchesTheLab()
        {
            var c = Shipped(); var rb = Build(PistolUnroll.WorldUp, c.Lay); var s = c.Body; Object.DestroyImmediate(c);
            // the lab's body golden: x = 40 on the ground at 60 u/s, the Goby 80 u/s; dive on a 1 s rhythm, a launch at step 100
            var p = new PistolBodyMath.State { x = 40f, y = rb.Ground(40f), v = 60f, launch = Mathf.RoundToInt(s.pLaunchCharges) };
            float t = 0f;
            for (int k = 0; k <= 240; k++)
            {
                switch (k)
                {''')
for b in g['body']:
    w('                    case %d: Near(%s, p.x, BodyTol, "x@%d"); Near(%s, p.y, BodyTol, "y@%d"); Assert.AreEqual(%s, p.grounded, "grounded@%d"); Near(%s, p.v, BodyTol, "v@%d"); Near(%s, p.vx, BodyTol, "vx@%d"); Near(%s, p.vy, BodyTol, "vy@%d"); Assert.AreEqual(%d, p.launch, "launch@%d"); break;' % (b['k'], f(b['x']), b['k'], f(b['y']), b['k'], 'true' if b['grounded'] else 'false', b['k'], f(b['v']), b['k'], f(b['vx']), b['k'], f(b['vy']), b['k'], b['launch'], b['k']))
w('''                }
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
                {''')
for vr in g['vring']:
    k = vr['k']
    w('                    case %d:' % k)
    w('                        Near(%s, ring.C, Tol, "c@%d"); Assert.AreEqual(%d, ring.Caps.Count, "carried@%d");' % (v3(vr['c']), k, vr['cap'], k))
    for j, cp in enumerate(vr['first']):
        w('                        Assert.AreEqual(%d, ring.Caps[%d].Id, "cap%d id@%d"); Near(%s, ring.Caps[%d].th, Tol, "th"); Near(%s, ring.Caps[%d].ph, Tol, "ph"); Near(%s, ring.Caps[%d].rho, Tol, "rho"); Near(%s, pos[%d], Tol, "p%d@%d");' % (cp['i'], j, j, k, f(cp['th']), j, f(cp['ph']), j, f(cp['rho']), j, v3(cp['p']), cp['i'], cp['i'], k))
    w('                        break;')
w('''                }
            }
        }

        [Test]
        public void Mulberry32_IsTheLabsBitForBit()
        {
            var r = new PistolAI.Mulberry32(31337u + 17u);''')
for i, x in enumerate(g['rngSeq']):
    w('            Assert.AreEqual(%s, r.Next(), 1e-6f, "draw %d");' % (f(x), i))
w('        }\n')
# AI
ai = g['ai']
w('''        // the lab's pistolScan for the AI goldens: in reach of the Pistol, on its screen, in its band (or primed)
        static void Scan(List<PistolAI.Seen> into, PistolRibbon rb, PistolBodyMath.State p, Vector3[] w, bool[] primed, bool[] friendly, in PistolAI.Settings s)
        {
            float reach = s.pLaunchRange + s.pVision + 60f;
            for (int i = 0; i < w.Length; i++) if (PistolAI.TrySee(rb, p, i, w[i], primed[i], friendly[i], reach, s, out var seen)) into.Add(seen);
        }
''')
lv = {'Hard': 'Hard', 'Medium': 'Medium', 'Easy': 'Easy'}
for name, a in ai.items():
    w('        [Test]\n        public void PistolAI_%s_MatchesTheLab()\n        {' % name)
    w('            var c = Shipped(); var rb = Build(PistolUnroll.WorldUp, c.Lay); var s = c.AI; Object.DestroyImmediate(c);')
    w('            float px = 100f, py = rb.Ground(px), gy = rb.Ground(200f);')
    w('            Near(%s, py, Tol, "pistol y");' % f(a['p'][1]))
    w('            // a cluster of 8 at x ~ 200 (prism 5 primed), and a friendly block (the Goby\'s trail) just ahead of the Pistol')
    w('            var w = new Vector3[9]; var primed = new bool[9]; var friendly = new bool[9];')
    w('            for (int i = 0; i < 8; i++) { w[i] = rb.Unmap(200f + (i % 4) * 6f, gy + 20f + (i / 4) * 6f, ((i % 3) - 1) * 10f); primed[i] = i == 5; }')
    w('            w[8] = rb.Unmap(130f, py + 5f, 0f); friendly[8] = true;')
    for i, pr in enumerate(a['prisms']):
        w('            Near(%s, w[%d], Tol, "prism%d");' % (v3(pr), i, i))
    w('            var p = new PistolBodyMath.State { x = px, y = py, v = 60f, launch = 2 }; p.wpos = rb.Unmap(px, py, 0f);')
    w('            var ai = new PistolAI(31337u + 17u); int scans = 0;')
    for i, out in enumerate(a['out']):
        w('            {')
        w('                var o = ai.Think(p, rb, %s, PistolAI.Difficulty.%s, s, l => { scans++; Scan(l, rb, p, w, primed, friendly, s); });' % (f(out['t']), lv[name]))
        w('                Assert.AreEqual(%d, ai.Vis.Count, "seen@%d");' % (out['vis'], i))
        w('                Assert.AreEqual(%s, o.Dive, 1e-6f, "dive@%d");' % (f(out['dive']), i))
        if out['launch']: w('                Assert.IsTrue(o.HasLaunch, "launch@%d"); Near(%s, o.Launch.x, Tol, "launch dx@%d"); Near(%s, o.Launch.y, Tol, "launch dy@%d");' % (i, f(out['launch'][0]), i, f(out['launch'][1]), i))
        else: w('                Assert.IsFalse(o.HasLaunch, "launch@%d");' % i)
        w('                Assert.AreEqual(%s, o.Ghost, "ghost@%d");' % ('true' if out['ghost'] else 'false', i))
        if out['vortex']: w('                Assert.IsTrue(o.Snap, "snap@%d"); Near(%s, o.SnapAim.x, Tol, "snap dx@%d"); Near(%s, o.SnapAim.y, Tol, "snap dy@%d");' % (i, f(out['vortex'][0]), i, f(out['vortex'][1]), i))
        else: w('                Assert.IsFalse(o.Snap, "snap@%d");' % i)
        w('            }')
    w('            Assert.AreEqual(3, scans, "one scan per thought (0.1 s apart or more)");')
    w('        }\n')
# ---- the VIEW golden: Unity's warp held to the LAB's numbers (PistolWarp / PistolRibbon's drawing mappers)
view = g.get('view')
if view:
    w("""        // ---- the view: the lab's own lap of radius 29 (1.2 laps, 400 steps laid 4 u a sample), its boxes, zones and parallax
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
""")
    lap = view['lap']
    w('        [Test]\n        public void View_LabLap_MatchesTheLab()\n        {')
    w('            var rb = LabLap();')
    w('            Assert.AreEqual(%d, rb.Count, "samples"); Near(%s, rb.X(rb.Count - 1), Tol, "last x"); Near(%s, rb.GobyX, Tol, "the Goby\'s x");' % (lap['n'], f(lap['lastX']), f(lap['gx'])))
    w('        }\n')
    for bi, bx in enumerate(view['boxes']):
        c = bx['centre']; hs = bx['halfSize']
        w('        [Test]\n        public void View_Box%d_r%d_MatchesTheLab()\n        {' % (bi, int(bx['r'])))
        w('            var rb = LabLap(); var lay = Shipped().Lay; const float hs = %sf; float hd = hs * Mathf.Sqrt(3f);' % hs)
        w('            var corners = new Vector3[8]; var xhd = new Vector3[8]; Vector3 c = new(%sf, 0f, 0f);' % (-bx['r']))
        w('            PistolWarp.BoxCorners(c, Vector3.right * hs, Vector3.up * hs, Vector3.forward * hs, corners);')
        w('            var m = rb.Map(c, -1e9f, 1e9f); Assert.IsTrue(m.Ok);')
        if bx['kind'] != 'BAR':
            w('            Near(%s, m.X, Tol, "centre x"); Near(%s, m.H, Tol, "centre h"); Near(%s, m.D, Tol, "centre d"); Assert.AreEqual(%d, m.K, "centre segment");' % (f(c['x']), f(c['h']), f(c['d']), c['k']))
            w('            Near(%s, rb.Curvature(m.K), 2e-4f, "curvature");' % f(bx['curvature']))
            w('            var kind = PistolWarp.Classify(hd, rb.Curvature(m.K), m.D, lay.SampleGap, rb.Count, out int window);')
            w('            Assert.AreEqual(PistolWarp.Kind.%s, kind); Assert.AreEqual(%d, window, "window");' % (bx['kind'].capitalize(), bx['window']))
            w('            Near(%s, PistolWarp.TearBound(hd, rb.Curvature(m.K), m.D, lay.SampleGap), 0.05f, "tear bound");' % f(bx['tearBound']))
            w('            Assert.IsTrue(PistolWarp.WarpBox(rb, m.K, corners, -1e9f, 1e9f, window, xhd));')
            for i, cc in enumerate(bx['corners']):
                w('            Near(%s, xhd[%d], 0.05f, "corner %d");' % (v3(cc), i, i))
        else:
            # the centre's segment is a nearest-by-noise pick on a perfect circle: the run and the bar do not depend on it
            w('            Near(%s, Mathf.Abs(m.D), Tol, "the centre is R beside the path");' % f(abs(c['d'])))
            w('            Near(%s, rb.Curvature(m.K), 2e-4f, "curvature");' % f(bx['curvature']))
            w('            var kind = PistolWarp.Classify(hd, rb.Curvature(m.K), m.D, lay.SampleGap, rb.Count, out int window);')
            w('            Assert.AreEqual(PistolWarp.Kind.Bar, kind); Assert.AreEqual(rb.Count, window, "the window is everything");')
            L = bx['lap']; B = bx['bar']
            w('            float rho = 1f / Mathf.Abs(rb.Curvature(m.K));')
            w('            rb.LapExtent(c, m.K, rho + hd + 2f, -1e9f, 1e9f, out int kLo, out int kHi, out bool loByDist, out bool hiByDist);')
            w('            Assert.AreEqual(%d, kLo); Assert.AreEqual(%d, kHi); Assert.AreEqual(%s, loByDist); Assert.AreEqual(%s, hiByDist);' % (L['kLo'], L['kHi'], 'true' if L['loByDist'] else 'false', 'true' if L['hiByDist'] else 'false'))
            w('            PistolWarp.BarExtent(rb, c, m.K, rho, hd, corners, -1e9f, 1e9f, out float xA, out float xB);')
            w('            Near(%s, xA, Tol, "bar xA"); Near(%s, xB, Tol, "bar xB");' % (f(B['xA']), f(B['xB'])))
        w('        }\n')
    w('        [Test]\n        public void View_Zones_MatchTheLab()\n        {')
    w('            var c = Shipped(); float band = c.Vision + 4f, vision = c.Vision, D = c.Parallax; Object.DestroyImmediate(c);')
    for z in view['zones']:
        w('            { var z = PistolWarp.ZoneOf(%sf, band, vision, D, false); Assert.IsTrue(z.Drawn, "d=%s drawn"); Near(%s, z.Reach, 1e-4f, "reach@%s"); Near(%s, z.Fog, 1e-4f, "fog@%s"); Near(%s, z.Alpha, 1e-4f, "alpha@%s"); Assert.AreEqual(%s, z.Solid, "solid@%s"); Assert.AreEqual(%s, z.Warped, "warped@%s"); }'
          % (z['d'], z['d'], f(z['reach']), z['d'], f(z['fog']), z['d'], f(z['alpha']), z['d'], 'true' if z['solid'] else 'false', z['d'], 'true' if z['warped'] else 'false', z['d']))
    w('        }\n')
    w('        [Test]\n        public void View_Parallax_MatchesTheLab()\n        {')
    for pt in view['parallax']:
        w('            Near(%s, PistolWarp.Parallax(100f, 27f, %sf, 192f, 40f, 100f, 17f, 160f), 1e-3f, "parallax@%s");' % (v3(pt['p']), pt['d'], pt['d']))
    w('        }\n')
w('    }\n}\n#endif')
print('\n'.join(o))
