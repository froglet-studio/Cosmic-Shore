using System.Collections.Generic;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// How the Goby's path is unrolled into the Pistol's 2D world (the duo brief §4.1). The lab's names.
    /// </summary>
    public enum PistolUnroll
    {
        /// <summary>A vertical curtain hung through the path: unrolls exactly (x = ground-track distance, h = height).</summary>
        WorldUp = 0,
        /// <summary>x = distance flown, h = height: no walls, the ground stretches where the path climbs.</summary>
        ArcLength = 1,
        /// <summary>The ribbon follows the Goby's own (filtered) up and twists with its roll; the path is flat in it.</summary>
        GobyUp = 2,
    }

    /// <summary>
    /// The Pistol's world: the Goby's path, sampled, and unrolled into (x, h, d), plus the ground the Pistol rides
    /// (a Catmull-Rom curve through the hinge blocks of the Goby's trail, ending at the Goby).
    ///
    /// Ported LINE FOR LINE from the Goby &amp; Pistol lab (<c>Docs/Studios/VesselStudio/goby.html</c>: <c>makeRibbon</c>,
    /// <c>ribAdd</c>, <c>ribIdx</c>, <c>ribUnmap</c>, <c>pathH</c>, <c>segD2</c>, <c>ribMap</c>, <c>ribFrame</c>,
    /// <c>cpCount</c>, <c>cpTan</c>, <c>groundAt</c>, <c>addBlock</c>, <c>layPath</c>, <c>gobyEnds</c>). No scene, no time:
    /// <c>GobyPistolMathTests</c> hold it to the numbers the lab's own functions return
    /// (<c>Tools/Build/goby_pistol_goldens.cjs</c>). The arithmetic is handedness-free; in Unity, R = Cross(T, U) points
    /// to the path's LEFT (the lab is right-handed), which only the harness's side camera has to know.
    /// </summary>
    public sealed class PistolRibbon
    {
        /// <summary>The trail-laying numbers (the lab's Trail and hills section).</summary>
        public struct LaySettings
        {
            public float SampleGap;   // tSampleGap
            public float BlockGap;    // tBlockGap
            public float HillEvery;   // tHillEvery
            public float HillAmp;     // tHillAmp
        }

        /// <summary>One block of the Goby's trail, as laid (for drawing; every Nth is a hinge).</summary>
        public struct Block
        {
            public float X; public bool Hinge; public int Sign; public Vector3 Pos, F, U, R;
        }

        /// <summary>What <see cref="Map"/> found: the point's (x, h, d), the segment and fraction it mapped to, and
        /// whether a different pass of the path was nearly as close (the fold the lab's Unroll test counts).</summary>
        public struct MapResult
        {
            public bool Ok; public float X, H, D; public bool Ambiguous; public int K; public float T;
        }

        public const int MaxBlocks = 2600;

        public readonly PistolUnroll Mode;
        readonly List<Vector3> _p = new(), _u = new(), _r = new();
        readonly List<float> _s = new(), _sig = new(), _hx = new(), _hz = new();
        Vector3 _upF = Vector3.up;
        public readonly List<float> CX = new(), CY = new();
        public readonly List<Block> Blocks = new();

        // the Goby's laying state (the lab's g.acc, g.blockAcc, g.blockN) and where it is on the ribbon
        float _acc, _blockAcc; int _blockN;
        public float GobyX { get; private set; }
        public float EndX { get; private set; }
        public float EndY { get; private set; }

        public PistolRibbon(PistolUnroll mode) { Mode = mode; }

        public int Count => _p.Count;
        public float X(int k) => Mode == PistolUnroll.WorldUp ? _sig[k] : _s[k];
        public Vector3 Sample(int k) => _p[k];

        static Vector3 Norm(Vector3 a) { float l = a.magnitude; return l > 0f ? a / l : a; }

        /// <summary>The lab's <c>ribAdd</c>: one sample of the path, its arc lengths, heading and (filtered) frame.</summary>
        public void AddSample(Vector3 p, Vector3 gobyUp, Vector3 fwd)
        {
            int n = _p.Count; float s = 0f, sig = 0f, hx, hz; Vector3 t = fwd;
            if (n > 0)
            {
                Vector3 prev = _p[n - 1]; float dx = p.x - prev.x, dy = p.y - prev.y, dz = p.z - prev.z;
                float l3 = Mathf.Sqrt(dx * dx + dy * dy + dz * dz), lh = Mathf.Sqrt(dx * dx + dz * dz);
                s = _s[n - 1] + l3; sig = _sig[n - 1] + lh;
                if (l3 > 1e-6f) t = new Vector3(dx / l3, dy / l3, dz / l3);
                if (lh > 1e-3f) { hx = dx / lh; hz = dz / lh; } else { hx = _hx[n - 1]; hz = _hz[n - 1]; }
            }
            else { float lh = Mathf.Sqrt(fwd.x * fwd.x + fwd.z * fwd.z); if (lh > 1e-3f) { hx = fwd.x / lh; hz = fwd.z / lh; } else { hx = 0f; hz = -1f; } }
            _upF = Norm(Vector3.LerpUnclamped(_upF, gobyUp, 0.2f));
            Vector3 up = _upF - t * Vector3.Dot(_upF, t);
            if (up.magnitude < 1e-4f) up = n > 0 ? _u[n - 1] : Vector3.up;
            up = Norm(up); Vector3 right = Norm(Vector3.Cross(t, up));
            _p.Add(p); _s.Add(s); _sig.Add(sig); _hx.Add(hx); _hz.Add(hz); _u.Add(up); _r.Add(right);
        }

        /// <summary>The lab's <c>ribIdx</c>: the segment k (0..n-2) whose x-range holds x.</summary>
        public int Index(float x)
        {
            int lo = 0, hi = _p.Count - 1; if (hi < 1) return 0;
            if (x <= X(0)) return 0; if (x >= X(hi)) return hi - 1;
            while (hi - lo > 1) { int m = (lo + hi) >> 1; if (X(m) <= x) lo = m; else hi = m; }
            return lo;
        }

        /// <summary>The lab's <c>ribUnmap</c>: (x, h, d) back to a world point.</summary>
        public Vector3 Unmap(float x, float h, float d)
        {
            if (_p.Count < 2) return _p.Count == 1 ? new Vector3(_p[0].x, h, _p[0].z) : new Vector3(0f, h, 0f);
            int k = Index(x); float X0 = X(k), X1 = X(k + 1);
            float t = X1 > X0 + 1e-9f ? (x - X0) / (X1 - X0) : 0f, extra = 0f;
            if (t > 1f) { extra = x - X1; t = 1f; } else if (t < 0f) { extra = x - X0; t = 0f; }
            Vector3 a = _p[k], b = _p[k + 1];
            if (Mode == PistolUnroll.GobyUp)
            {
                float L = (b - a).magnitude; if (L <= 0f) L = 1f; Vector3 tan = (b - a) / L;
                Vector3 up = Norm(Vector3.LerpUnclamped(_u[k], _u[k + 1], t)), rt = Norm(Vector3.LerpUnclamped(_r[k], _r[k + 1], t));
                return Vector3.LerpUnclamped(a, b, t) + tan * extra + up * h + rt * d;
            }
            float hx = _hx[k + 1], hz = _hz[k + 1];   // the segment's own heading (as Map uses it)
            return new Vector3(Mathf.LerpUnclamped(a.x, b.x, t) + hx * extra - hz * d, h, Mathf.LerpUnclamped(a.z, b.z, t) + hz * extra + hx * d);
        }

        /// <summary>The lab's <c>pathH</c>: the path's height at x (0 in Goby's-up, where the path is flat).</summary>
        public float PathHeight(float x)
        {
            if (Mode == PistolUnroll.GobyUp) return 0f;
            if (_p.Count < 2) return _p.Count == 1 ? _p[0].y : 0f;
            int k = Index(x); float X0 = X(k), X1 = X(k + 1);
            return Mathf.LerpUnclamped(_p[k].y, _p[k + 1].y, X1 > X0 ? Mathf.Clamp01((x - X0) / (X1 - X0)) : 1f);
        }

        float SegD2(int k, Vector3 q, bool goby, out float t)
        {
            Vector3 a = _p[k], b = _p[k + 1];
            if (goby)
            {
                Vector3 ab = b - a; float L2 = ab.sqrMagnitude;
                t = L2 > 1e-9f ? Mathf.Clamp01(Vector3.Dot(q - a, ab) / L2) : 0f;
                return (a + ab * t - q).sqrMagnitude;
            }
            float abx = b.x - a.x, abz = b.z - a.z, L2h = abx * abx + abz * abz;
            t = L2h > 1e-9f ? Mathf.Clamp01(((q.x - a.x) * abx + (q.z - a.z) * abz) / L2h) : 0f;
            float cx = a.x + abx * t - q.x, cz = a.z + abz * t - q.z; return cx * cx + cz * cz;
        }

        /// <summary>The lab's <c>ribMap</c>: a world point to (x, h, d), searching only the path with x in
        /// [<paramref name="xLo"/>, <paramref name="xHi"/>] (the Pistol's screen: the ribbon only exists there).</summary>
        public MapResult Map(Vector3 q, float xLo, float xHi)
        {
            var r = new MapResult();
            if (_p.Count < 2) return r;
            bool goby = Mode == PistolUnroll.GobyUp; int k0 = Index(xLo), k1 = Mathf.Min(_p.Count - 2, Index(xHi));
            int bk = -1; float bt = 0f, bd = float.PositiveInfinity;
            for (int k = k0; k <= k1; k++) { float d2 = SegD2(k, q, goby, out float t); if (d2 < bd) { bd = d2; bk = k; bt = t; } }
            if (bk < 0) return r;
            float sd = float.PositiveInfinity; int sk = -1;
            for (int k = k0; k <= k1; k++) { if (Mathf.Abs(k - bk) <= 6) continue; float d2 = SegD2(k, q, goby, out _); if (d2 < sd) { sd = d2; sk = k; } }
            Resolve(ref r, q, bk, bt, goby);
            r.Ambiguous = sk >= 0 && Mathf.Sqrt(sd) < Mathf.Sqrt(bd) * 1.25f + 4f && Mathf.Abs(X(sk) - r.X) > 60f;
            return r;
        }

        /// <summary>(x, h, d) of q against segment bk at fraction bt: the tail of the lab's <c>ribMap</c>.</summary>
        void Resolve(ref MapResult r, Vector3 q, int bk, float bt, bool goby)
        {
            float X0 = X(bk), X1 = X(bk + 1);
            r.X = X0 + (X1 - X0) * bt; r.K = bk; r.T = bt;
            Vector3 a = _p[bk], b = _p[bk + 1];
            if (goby)
            {
                Vector3 c = Vector3.LerpUnclamped(a, b, bt), rel = q - c;
                Vector3 up = Norm(Vector3.LerpUnclamped(_u[bk], _u[bk + 1], bt)), rt = Norm(Vector3.LerpUnclamped(_r[bk], _r[bk + 1], bt));
                r.H = Vector3.Dot(rel, up); r.D = Vector3.Dot(rel, rt);
            }
            else
            {
                float cx = Mathf.LerpUnclamped(a.x, b.x, bt), cz = Mathf.LerpUnclamped(a.z, b.z, bt), hx = _hx[bk + 1], hz = _hz[bk + 1];
                r.H = q.y; r.D = (q.x - cx) * -hz + (q.z - cz) * hx;
            }
            r.Ok = true;
        }

        /// <summary>The map for DRAWING: the same nearest segment as <see cref="Map"/> away from a fold, found coarse-to-fine
        /// (every <see cref="CoarseStep"/>th segment, then the segments round the best) and without the ambiguity pass — a
        /// third of the cost, so hundreds of prisms can be mapped a frame. At a fold (two passes of the path within ~12 u of
        /// each other from q) it may pick the other pass; the gameplay asks <see cref="Map"/>, which never does.</summary>
        public MapResult MapFast(Vector3 q, float xLo, float xHi, bool beyondEnd = false)
        {
            var r = new MapResult();
            if (_p.Count < 2) return r;
            bool goby = Mode == PistolUnroll.GobyUp; int k0 = Index(xLo), k1 = Mathf.Min(_p.Count - 2, Index(xHi));
            if (k1 < k0) return r;
            int ck = -1; float cd = float.PositiveInfinity;
            for (int k = k0; k <= k1; k += CoarseStep) { float d2 = SegD2(k, q, goby, out _); if (d2 < cd) { cd = d2; ck = k; } }
            if (ck < 0) return r;
            { float d2 = SegD2(k1, q, goby, out _); if (d2 < cd) { cd = d2; ck = k1; } }
            int bk = -1; float bt = 0f, bd = float.PositiveInfinity;
            for (int k = Mathf.Max(k0, ck - CoarseStep); k <= Mathf.Min(k1, ck + CoarseStep); k++)
            { float d2 = SegD2(k, q, goby, out float t); if (d2 < bd) { bd = d2; bk = k; bt = t; } }
            if (beyondEnd) bt = BeyondEnd(bk, q, goby, bt);
            Resolve(ref r, q, bk, bt, goby);
            return r;
        }

        /// <summary>For DRAWING what lies ahead of the Goby: on the ribbon's last segment the projection is not clamped,
        /// so a point past the end maps to x = the end plus its distance along the Goby's heading (a straight
        /// forecast of the world to come), instead of piling up at the Goby's x as the lab's map (rightly) has it.</summary>
        float BeyondEnd(int bk, Vector3 q, bool goby, float bt)
        {
            if (bk != _p.Count - 2 || bt < 1f - 1e-6f) return bt;
            Vector3 a = _p[bk], b = _p[bk + 1];
            if (goby) { Vector3 ab = b - a; float L2 = ab.sqrMagnitude; return L2 > 1e-9f ? Mathf.Max(1f, Vector3.Dot(q - a, ab) / L2) : bt; }
            float abx = b.x - a.x, abz = b.z - a.z, L2h = abx * abx + abz * abz;
            return L2h > 1e-9f ? Mathf.Max(1f, ((q.x - a.x) * abx + (q.z - a.z) * abz) / L2h) : bt;
        }

        public const int CoarseStep = 3;

        /// <summary>The nearest segment to q among the <paramref name="halfWindow"/> segments either side of
        /// <paramref name="kCentre"/>, inside [xLo, xHi]: how a prism's CORNERS are mapped once its centre is, so the
        /// corners stretch along the path exactly as far as the unroll says and never jump to another pass of it.</summary>
        public MapResult MapNear(Vector3 q, int kCentre, int halfWindow, float xLo, float xHi, bool beyondEnd = false)
        {
            var r = new MapResult();
            if (_p.Count < 2) return r;
            bool goby = Mode == PistolUnroll.GobyUp;
            int k0 = Mathf.Max(Index(xLo), kCentre - halfWindow), k1 = Mathf.Min(Mathf.Min(_p.Count - 2, Index(xHi)), kCentre + halfWindow);
            if (k1 < k0) return r;
            int bk = -1; float bt = 0f, bd = float.PositiveInfinity;
            for (int k = k0; k <= k1; k++) { float d2 = SegD2(k, q, goby, out float t); if (d2 < bd) { bd = d2; bk = k; bt = t; } }
            if (beyondEnd) bt = BeyondEnd(bk, q, goby, bt);
            Resolve(ref r, q, bk, bt, goby);
            return r;
        }

        /// <summary>The path's signed curvature at segment k (1/radius; 0 on a straight), positive when the path bends
        /// toward +R (the centre of curvature at d = +1/κ), negative toward -R. Measured over the headings of the segments
        /// either side of k, so one noisy sample does not read as a hairpin.</summary>
        public float Curvature(int k)
        {
            int n = _p.Count; if (n < 3) return 0f;
            int ka = Mathf.Clamp(k - 1, 0, n - 2), kb = Mathf.Clamp(k + 1, 0, n - 2); if (kb <= ka) return 0f;
            float arc = X(kb) - X(ka); if (arc <= 1e-4f) return 0f;   // segment ka's heading to segment kb's: their midpoints are X(kb) - X(ka) apart
            if (Mode == PistolUnroll.GobyUp)
            {
                Vector3 ta = Norm(_p[ka + 1] - _p[ka]), tb = Norm(_p[kb + 1] - _p[kb]);
                float ang = Mathf.Atan2(Vector3.Cross(ta, tb).magnitude, Vector3.Dot(ta, tb));
                return Mathf.Sign(Vector3.Dot(tb - ta, _r[k + 1 < n ? k + 1 : k])) * ang / arc;
            }
            float hxa = _hx[ka + 1], hza = _hz[ka + 1], hxb = _hx[kb + 1], hzb = _hz[kb + 1];
            // R = (-hz, 0, hx): the bend is toward +R when the heading turns that way
            float cross = hxa * hzb - hza * hxb, dot = hxa * hxb + hza * hzb;
            float a2 = Mathf.Atan2(Mathf.Abs(cross), dot);
            float toR = (hxb - hxa) * -hza + (hzb - hza) * hxa;
            return (toR >= 0f ? 1f : -1f) * a2 / arc;
        }

        /// <summary>The lab's <c>ribFrame</c>: T along the unrolled path, U its up, R across (the screen's depth).</summary>
        public void Frame(int k, float t, out Vector3 T, out Vector3 U, out Vector3 R)
        {
            k = Mathf.Clamp(k, 0, Mathf.Max(0, _p.Count - 2));
            if (Mode == PistolUnroll.GobyUp)
            {
                T = Norm(_p[k + 1] - _p[k]);
                U = Norm(Vector3.LerpUnclamped(_u[k], _u[k + 1], t)); R = Norm(Vector3.LerpUnclamped(_r[k], _r[k + 1], t));
            }
            else { float hx = _hx[k + 1], hz = _hz[k + 1]; T = new Vector3(hx, 0f, hz); U = Vector3.up; R = new Vector3(-hz, 0f, hx); }
        }

        /// <summary>The lab's <c>ribFrameAt</c>.</summary>
        public void FrameAt(float x, out Vector3 T, out Vector3 U, out Vector3 R)
        {
            int k = Index(x); float X0 = X(k), X1 = X(k + 1);
            Frame(k, X1 > X0 ? Mathf.Clamp01((x - X0) / (X1 - X0)) : 0f, out T, out U, out R);
        }

        // ---- the ground: a Catmull-Rom curve through the hinges, ending at the Goby (it is still being laid) ----
        float CpX(int i) => i < CX.Count ? CX[i] : EndX;
        float CpY(int i) => i < CX.Count ? CY[i] : EndY;
        public int ControlPointCount { get { int n = CX.Count; return n > 0 && EndX > CX[n - 1] + 1f ? n + 1 : n; } }
        float CpTan(int i, int m) { int a = Mathf.Max(0, i - 1), b = Mathf.Min(m - 1, i + 1); float dx = CpX(b) - CpX(a); return dx > 1e-6f ? (CpY(b) - CpY(a)) / dx : 0f; }

        /// <summary>The lab's <c>groundAt</c>: the ground's height at x and its slope there.</summary>
        public float Ground(float x, out float slope)
        {
            int m = ControlPointCount;
            if (m == 0) { slope = 0f; return EndY; }
            if (m == 1 || x <= CpX(0)) { slope = 0f; return CpY(0); }
            if (x >= CpX(m - 1)) { slope = 0f; return CpY(m - 1); }
            int lo = 0, hi = m - 1; while (hi - lo > 1) { int md = (lo + hi) >> 1; if (CpX(md) <= x) lo = md; else hi = md; }
            float xa = CpX(lo), xb = CpX(lo + 1), ya = CpY(lo), yb = CpY(lo + 1), h = xb - xa;
            if (h < 1e-6f) { slope = 0f; return yb; }
            float ma = CpTan(lo, m), mb = CpTan(lo + 1, m), t = (x - xa) / h, t2 = t * t, t3 = t2 * t;
            slope = ((6 * t2 - 6 * t) * ya + (3 * t2 - 4 * t + 1) * h * ma + (-6 * t2 + 6 * t) * yb + (3 * t2 - 2 * t) * h * mb) / h;
            return (2 * t3 - 3 * t2 + 1) * ya + (t3 - 2 * t2 + t) * h * ma + (-2 * t3 + 3 * t2) * yb + (t3 - t2) * h * mb;
        }
        public float Ground(float x) => Ground(x, out _);

        // ---- laying the path: the lab's addBlock, layPath, gobyEnds ----
        static int RoundJs(float v) => Mathf.FloorToInt(v + 0.5f);   // JS Math.round (C#'s rounds half to even)

        /// <summary>The lab's <c>addBlock</c>: a trail block at p; every Nth is a hinge that lifts or drops the ground.</summary>
        public void AddBlock(Vector3 p, Vector3 f, Vector3 u, Vector3 r, in LaySettings s)
        {
            int every = Mathf.Max(1, RoundJs(s.HillEvery)), idx = _blockN++;
            bool hinge = idx % every == 0; float x = X(_p.Count - 1); int sign = (idx / every) % 2 == 0 ? 1 : -1;
            if (hinge) { int n = CX.Count; float H = Mode == PistolUnroll.GobyUp ? 0f : p.y; if (n == 0 || x - CX[n - 1] >= 2f) { CX.Add(x); CY.Add(H + sign * s.HillAmp); } }
            Blocks.Add(new Block { X = x, Hinge = hinge, Sign = sign, Pos = p, F = f, U = u, R = r });
            if (Blocks.Count > MaxBlocks) Blocks.RemoveAt(0);
        }

        /// <summary>The first sample and block, where the Goby starts (the lab's makeWorld does this once).</summary>
        public void Begin(Vector3 p, Vector3 f, Vector3 u, Vector3 r, in LaySettings s)
        {
            AddSample(p, u, f); AddBlock(p, f, u, r, s); Ends(p, f);
        }

        /// <summary>The lab's <c>layPath</c>: the Goby moved a -> b; a sample every SampleGap, a block every BlockGap.</summary>
        public void Lay(Vector3 a, Vector3 b, Vector3 f, Vector3 u, Vector3 r, in LaySettings s)
        {
            float L = (b - a).magnitude; if (L <= 0f) { Ends(b, f); return; }
            float gap = s.SampleGap, d = gap - _acc;
            while (d <= L)
            {
                Vector3 p = a + (b - a) * (d / L); AddSample(p, u, f); _blockAcc += gap;
                if (_blockAcc >= s.BlockGap) { _blockAcc -= s.BlockGap; AddBlock(p, f, u, r, s); }
                d += gap;
            }
            _acc = L - (d - gap);
            Ends(b, f);
        }

        /// <summary>The lab's <c>gobyEnds</c>: where the Goby is on the ribbon, the end of the Pistol's ground.</summary>
        void Ends(Vector3 gobyPos, Vector3 f)
        {
            GobyX = X(_p.Count - 1) + _acc * (Mode == PistolUnroll.WorldUp ? Mathf.Sqrt(f.x * f.x + f.z * f.z) : 1f);
            EndX = GobyX; EndY = Mode == PistolUnroll.GobyUp ? 0f : gobyPos.y;
        }
    }
}
