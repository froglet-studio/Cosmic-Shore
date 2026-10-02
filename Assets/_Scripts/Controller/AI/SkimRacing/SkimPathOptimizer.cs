using System;
using System.Collections.Generic;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Shapes the line a skim racer flies from where it is to past the crystal it is after: a dense
    /// offset path in the ribbon's own frame, solved so that at the speed the ship will be doing it
    /// never asks the stick for more than it has, it clears the plates, it passes within reach of the
    /// crystal, it keeps off the rails, and — as far as all of that allows — it stays in skimming
    /// reach of the plates.
    ///
    /// <para><b>Why a solver and not a few keys.</b> The first planner built a line out of a handful
    /// of keys (where the ship is, a pass point, an edge, a lane) joined by quintics and picked the
    /// best of a dozen such lines. Every one of them put its bends where its keys happened to be, so
    /// at 300 u/s the line it chose routinely asked for two to four times the stick the ship has, the
    /// throttle came off and the ship lost a third of its speed for two seconds after almost every
    /// crystal. The bend a crystal needs is not where a key falls; it is wherever spreading it costs
    /// least, and that is a property of the whole line.</para>
    ///
    /// <para><b>What is minimised.</b> The ship turns at <c>k</c> = 1.5 / s times the rotation
    /// between where it points and where the stick has commanded it, so a line costs
    /// <c>|v κ + (v² / k) dκ/ds|</c> of stick (across the path). That is linear in the path's points,
    /// read between every two grid points off a staggered stencil. Above the budget it is a stiff
    /// squared hinge; everywhere it is a light squared penalty that picks the smoothest of the lines
    /// the constraints leave open. Leaving the skimmer's reach is a squared penalty weighted by how
    /// much skimming is worth right now (a lot on the ramp, little at full boost). The plates, the
    /// crystal's pass radius and the rails are hard constraints.</para>
    ///
    /// <para><b>How: ADMM.</b> Every term is a simple function of an AFFINE image of the line — the
    /// demand at a stencil, a point's offset, a point of a segment near a rail — so each gets a copy
    /// variable, and the solver alternates a least-squares fit of the line to the copies (one banded
    /// system whose matrix never changes during a solve: factored once, then a back-substitution per
    /// iteration) with the closed-form proximal step of each term on its own copy (shrink a demand
    /// onto the budget disk, project a point off the plates' face or into the crystal's circle, pull
    /// it toward the skim band). A Gauss-Newton solver was tried first and is the reason for this
    /// one: with the demand's sensitivity at 300 u/s (35 rad/s per unit of third difference), the
    /// hinge activating and releasing between iterations made its steps overshoot by an order of
    /// magnitude, and it crawled. The problem is convex for a fixed choice of which face of the
    /// plates each point is off, and ADMM converges on such problems without line searches.</para>
    ///
    /// <para><b>The plates are not convex.</b> A point is held off the face of their clearance box it
    /// is on, and is handed from face to face as the line goes round an edge — which is why the seed
    /// matters, and why the brain seeds both ways round.</para>
    ///
    /// Pure: no Unity object, so Tools/Build/squirrel_ai_harness compiles and races it.
    /// </summary>
    public sealed class SkimPathOptimizer
    {
        public struct Settings
        {
            /// <summary>Stick rate (rad/s) the line may ask for before the hinge bites.</summary>
            public float Budget;
            /// <summary>The ship's rotation follow rate <c>k</c> (1/s).</summary>
            public float FollowRate;
            /// <summary>Weight per (rad/s)² of demand above <see cref="Budget"/>.</summary>
            public float HingeWeight;
            /// <summary>Weight per (rad/s)² of demand anywhere: what picks the smoothest line.</summary>
            public float DemandWeight;
            /// <summary>Weight per u² outside the skimmer's reach.</summary>
            public float BandWeight;
            /// <summary>ADMM penalty on the point copies (per u²). The demand copies are scaled to
            /// match through their own stencil's size.</summary>
            public float Rho;
            /// <summary>Clearance (u) kept beyond the hull's reach of the plates.</summary>
            public float ClearMargin;
            /// <summary>How far (u) beyond a plate the skimmer reaches it.</summary>
            public float SkimReach;
            /// <summary>Clearance (u) kept between the hull's reach and a rail prism.</summary>
            public float ObstacleMargin;
        }

        public const int MaxPoints = 160;
        const int MaxObstacles = 512;
        const int Kb = 7;              // half-bandwidth: 2 unknowns per point, a stencil coupling +-3 points
        const int FixedCount = 3;      // position, slope and curvature are where the ship already is
        const float ClearPad = 0.25f;  // a hard constraint is met this far inside its own boundary

        readonly Vector3[] _c = new Vector3[MaxPoints];
        readonly Vector3[] _r = new Vector3[MaxPoints];
        readonly Vector3[] _u = new Vector3[MaxPoints];
        readonly float[] _cw = new float[MaxPoints], _ch = new float[MaxPoints];   // clearance box
        readonly float[] _pw = new float[MaxPoints], _ph = new float[MaxPoints];   // plate box
        readonly float[] _v = new float[MaxPoints];
        readonly float[] _a = new float[MaxPoints], _b = new float[MaxPoints];
        readonly int[] _side = new int[MaxPoints];   // the face of the clearance box a point is held off

        // ADMM copies and scaled duals: demand per stencil (2), point offsets (2), rail points (3).
        readonly double[] _zdr = new double[MaxPoints], _zdu = new double[MaxPoints];
        readonly double[] _wdr = new double[MaxPoints], _wdu = new double[MaxPoints];
        readonly double[] _rhoD = new double[MaxPoints];
        readonly double[] _zpa = new double[MaxPoints], _zpb = new double[MaxPoints];
        readonly double[] _wpa = new double[MaxPoints], _wpb = new double[MaxPoints];
        // Each obstacle's copy is the curve point at its arc, held outside the obstacle's capsule —
        // but only while the obstacle is ACTIVE (near the line). See UpdateActivation.
        readonly Vector3[] _zo = new Vector3[MaxObstacles], _wo = new Vector3[MaxObstacles];
        readonly bool[] _oActive = new bool[MaxObstacles];
        int _activeObstacles;
        readonly int[] _oSeg = new int[MaxObstacles];    // B-spline segment the obstacle sits beside
        readonly float[] _oT = new float[MaxObstacles];  // and where along it

        // The demand rows never change during a solve (geometry and speed only): kept, per stencil,
        // as 4 points x (a, b) coefficients plus the constant, for the across (R) and up (U) rows.
        readonly double[] _rowR = new double[MaxPoints * 8], _rowU = new double[MaxPoints * 8];
        readonly double[] _rowRc = new double[MaxPoints], _rowUc = new double[MaxPoints];

        double[] _h = new double[2 * MaxPoints * (Kb + 1)];
        double[] _rhs = new double[2 * MaxPoints];
        double[] _x = new double[2 * MaxPoints];

        // One scalar row of the linear model under construction: sum_k ga_k a_k + gb_k b_k + c.
        readonly int[] _ti = new int[8];
        readonly double[] _tga = new double[8], _tgb = new double[8];
        int _tn;
        double _tc;

        // Obstacles are capsules: a centre, a unit axis and a half-length (a rail prism is a 6 u rod),
        // inflated by a radius; each is held off the line at the route arc it stands beside.
        readonly List<Vector3> _obsCenters = new List<Vector3>(256);
        readonly List<Vector3> _obsAxes = new List<Vector3>(256);
        readonly List<float> _obsHalf = new List<float>(256);
        readonly List<float> _obsRadii = new List<float>(256);
        readonly List<float> _obsU = new List<float>(256);   // arc parameter, in knots from the first

        int _n;
        float _s0, _step;
        int _crystalIndex = -1;
        float _crystalA, _crystalB, _crystalR;
        Settings _set;

        public int Count => _n;
        public float Start => _s0;
        public float Step => _step;
        public float A(int i) => _a[i];
        public float B(int i) => _b[i];
        public float DesignSpeed(int i) => _v[i];
        public int CrystalIndex => _crystalIndex;

        // ---- diagnostics of the last solve
        public float WorstDemand { get; private set; }
        public float WorstExcess { get; private set; }
        public float CrystalMiss { get; private set; }
        public float WorstClearDeficit { get; private set; }
        public float WorstObstacleDeficit { get; private set; }
        public int Iterations { get; private set; }
        public int ObstacleCount => _obsCenters.Count;

        /// <summary>Tooling: receives a line every few iterations when set.</summary>
        public Action<string> DebugLog;

        /// <summary>
        /// Lays out <paramref name="count"/> points from arc <paramref name="s0"/>, <paramref name="step"/>
        /// apart, and reads the ribbon there: centre, frame, the box the hull must stay out of (the
        /// shells' envelope over <paramref name="clearAlong"/> grown by <paramref name="hullReach"/>
        /// and the margin) and the plates the skimmer reaches.
        /// </summary>
        public void Setup(SkimRoute route, float s0, float step, int count, float clearAlong, float hullReach, in Settings settings)
        {
            if (route == null) throw new ArgumentNullException(nameof(route));
            _n = Mathf.Clamp(count, FixedCount + 2, MaxPoints);
            _s0 = s0;
            _step = Mathf.Max(1f, step);
            _set = settings;
            _crystalIndex = -1;
            _obsCenters.Clear();
            _obsAxes.Clear();
            _obsHalf.Clear();
            _obsRadii.Clear();
            _obsU.Clear();
            for (int i = 0; i < _n; i++)
            {
                float s = s0 + i * _step;
                route.Frame(s, out _c[i], out _, out _r[i], out _u[i]);
                // The tightest clearance over the stretch this point stands for.
                float hw = 0f, hh = 0f;
                for (float d = -_step * 0.5f; d <= _step * 0.5f + 0.01f; d += 4f)
                {
                    route.Envelope(s + d, clearAlong, out float w, out float h);
                    if (w > hw) hw = w;
                    if (h > hh) hh = h;
                }
                _cw[i] = hw + hullReach + settings.ClearMargin;
                _ch[i] = hh + hullReach + settings.ClearMargin;
                // The plate itself, bridged across the gap between consecutive shells.
                route.Envelope(s, 4f, out _pw[i], out _ph[i]);
                _v[i] = 60f;
            }
        }

        public void SetSpeed(int i, float v) => _v[i] = Mathf.Max(1f, v);

        public void SetPoint(int i, float a, float b)
        {
            _a[i] = a;
            _b[i] = b;
        }

        /// <summary>The line must pass within <paramref name="radius"/> of (a, b) at point
        /// <paramref name="index"/>.</summary>
        public void SetCrystal(int index, float a, float b, float radius)
        {
            _crystalIndex = index >= FixedCount && index < _n ? index : -1;
            _crystalA = a;
            _crystalB = b;
            _crystalR = radius;
        }

        /// <summary>
        /// Keep the line <paramref name="radius"/> from a rod of half-length
        /// <paramref name="halfLength"/> along <paramref name="axis"/> through <paramref name="center"/>.
        /// The line is held off it at the route arc the rod stands beside — the curve point there, not
        /// a point of the control polygon — which is what lets a line that runs alongside a chain of
        /// rails stay clear of every one of them: the rails are a line of rods 7 u apart, and a curve
        /// that is clear at each rod's arc cannot dip into the gap between two of them by more than
        /// its curvature allows over 3.5 u. Ignored when it stands nowhere near the stretch planned.
        /// </summary>
        public void AddObstacle(Vector3 center, Vector3 axis, float halfLength, float radius)
        {
            if (_obsCenters.Count >= MaxObstacles || _n < FixedCount + 2) return;
            int best = -1;
            float bestD = float.MaxValue;
            for (int i = 0; i < _n; i++)
            {
                float d = (_c[i] - center).sqrMagnitude;
                if (d < bestD) { bestD = d; best = i; }
            }
            // Its arc: the nearest knot, slid along the route's tangent there.
            int i0 = Mathf.Max(0, best - 1), i1 = Mathf.Min(_n - 1, best + 1);
            Vector3 tangent = (_c[i1] - _c[i0]).normalized;
            float u = best + Vector3.Dot(center - _c[best], tangent) / _step;
            // Only beside the line's own segments (1 .. n-3): behind the ship it has been flown.
            if (u < FixedCount - 2 + 0.5f || u > _n - 2.01f) return;
            Vector3 lateral = center - _c[best] - tangent * Vector3.Dot(center - _c[best], tangent);
            if (lateral.sqrMagnitude > MaxObstacleLateral * MaxObstacleLateral) return;
            _obsCenters.Add(center);
            _obsAxes.Add(axis.sqrMagnitude > 1e-6f ? axis.normalized : tangent);
            _obsHalf.Add(Mathf.Max(0f, halfLength));
            _obsRadii.Add(radius);
            _obsU.Add(u);
        }

        /// <summary>Obstacles further than this (u) from the ribbon's centre line are not planned
        /// around: a crystal is never further out than 70 u, and the line never strays much past it.</summary>
        const float MaxObstacleLateral = 110f;

        /// <summary>World point of the B-spline at arc parameter <paramref name="u"/> (in knots from the
        /// first): what the line actually flies, as opposed to <see cref="Point"/>, its control points.</summary>
        public Vector3 CurvePoint(float u)
        {
            int j = Mathf.Clamp(Mathf.FloorToInt(u), 1, _n - 3);
            float t = Mathf.Clamp01(u - j);
            SplineWeights(t, out float w0, out float w1, out float w2, out float w3);
            return Point(j - 1) * w0 + Point(j) * w1 + Point(j + 1) * w2 + Point(j + 2) * w3;
        }

        static void SplineWeights(float t, out float w0, out float w1, out float w2, out float w3)
        {
            float t2 = t * t, t3 = t2 * t, mt = 1f - t;
            w0 = mt * mt * mt / 6f;
            w1 = (3f * t3 - 6f * t2 + 4f) / 6f;
            w2 = (-3f * t3 + 3f * t2 + 3f * t + 1f) / 6f;
            w3 = t3 / 6f;
        }

        /// <summary>The point of obstacle <paramref name="o"/>'s rod nearest <paramref name="q"/>.</summary>
        Vector3 RodPoint(int o, Vector3 q)
        {
            Vector3 c = _obsCenters[o], ax = _obsAxes[o];
            float h = _obsHalf[o];
            return c + ax * Mathf.Clamp(Vector3.Dot(q - c, ax), -h, h);
        }

        /// <summary>World point of the line at grid point <paramref name="i"/>.</summary>
        public Vector3 Point(int i) => _c[i] + _r[i] * _a[i] + _u[i] * _b[i];

        /// <summary>True when grid point <paramref name="i"/> is within the skimmer's reach of the
        /// plates.</summary>
        public bool InBand(int i) => PlateDistance(i, _a[i], _b[i]) <= _set.SkimReach;

        float PlateDistance(int i, float a, float b)
        {
            float dx = Mathf.Max(0f, Mathf.Abs(a) - _pw[i]);
            float dy = Mathf.Max(0f, Mathf.Abs(b) - _ph[i]);
            return Mathf.Sqrt(dx * dx + dy * dy);
        }

        // ========================================================================== solve

        /// <summary>
        /// <paramref name="iterations"/> ADMM iterations from the line as set. The first
        /// <see cref="FixedCount"/> points are held where they were set.
        /// </summary>
        public void Solve(int iterations)
        {
            int m = _n - FixedCount;
            int nu = 2 * m;
            int need = nu * (Kb + 1);
            if (_h.Length < need) _h = new double[need];
            if (_rhs.Length < nu) _rhs = new double[nu];
            if (_x.Length < nu) _x = new double[nu];
            double rho = Math.Max(1e-6, _set.Rho);
            float k = Mathf.Max(0.05f, _set.FollowRate);

            for (int i = 0; i < _n; i++) _side[i] = NearestSide(i, _a[i], _b[i]);
            for (int o = 0; o < _obsCenters.Count; o++)
            {
                float u = _obsU[o];
                _oSeg[o] = Mathf.Clamp(Mathf.FloorToInt(u), 1, _n - 3);
                _oT[o] = Mathf.Clamp01(u - _oSeg[o]);
                _oActive[o] = false;
            }
            _activeObstacles = 0;
            UpdateActivation();

            // ---- the fixed system: every copy's least-squares fit.
            for (int i = 1; i <= _n - 3; i++)
            {
                Stencil(0.5f * (_v[i] + _v[i + 1]), k, _step, out double c0, out double c1, out double c2, out double c3);
                // Scale each demand copy's penalty by its stencil's size, so a unit of slack means
                // as much to the fit at 300 u/s as at 60.
                double norm = c0 * c0 + c1 * c1 + c2 * c2 + c3 * c3;
                _rhoD[i] = rho / Math.Max(1e-9, norm);
                StoreRow(i, _r[i], c0, c1, c2, c3, _rowR, out _rowRc[i]);
                StoreRow(i, _u[i], c0, c1, c2, c3, _rowU, out _rowUc[i]);
                DemandVector(i, out _zdr[i], out _zdu[i]);
                _wdr[i] = _wdu[i] = 0.0;
            }
            for (int i = FixedCount; i < _n; i++)
            {
                _zpa[i] = _a[i];
                _zpb[i] = _b[i];
                _wpa[i] = _wpb[i] = 0.0;
            }
            AssembleAndFactor(nu, need, rho);

            const double relax = 1.6;   // over-relaxation: the standard 1.5-1.8 roughly halves the iterations
            int it = 0;
            for (; it < Mathf.Max(1, iterations); it++)
            {
                // A line that has moved toward an obstacle the solve started clear of brings it in.
                if (it > 0 && it % ActivationCheckInterval == 0 && UpdateActivation())
                    AssembleAndFactor(nu, need, rho);

                // ---- x: least-squares fit of the line to every copy (minus its dual).
                Array.Clear(_rhs, 0, nu);
                for (int i = 1; i <= _n - 3; i++)
                {
                    RowRhs(i, _rowR, _rowRc[i], _rhoD[i], _zdr[i] - _wdr[i]);
                    RowRhs(i, _rowU, _rowUc[i], _rhoD[i], _zdu[i] - _wdu[i]);
                }
                for (int i = FixedCount; i < _n; i++)
                {
                    int u = 2 * (i - FixedCount);
                    _rhs[u] += rho * (_zpa[i] - _wpa[i]);
                    _rhs[u + 1] += rho * (_zpb[i] - _wpb[i]);
                }
                for (int o = 0; o < _obsCenters.Count; o++)
                {
                    if (!_oActive[o]) continue;
                    for (int axis = 0; axis < 3; axis++)
                    {
                        ObstacleRow(o, axis);
                        CommitRhs(rho, _zo[o][axis] - _wo[o][axis]);
                    }
                }
                SolveFactored(nu);
                for (int f = 0; f < m; f++)
                {
                    int i = f + FixedCount;
                    float na = (float)_x[2 * f], nb = (float)_x[2 * f + 1];
                    if (float.IsNaN(na) || float.IsNaN(nb)) continue;
                    _a[i] = na;
                    _b[i] = nb;
                }

                // ---- copies: each term's proximal step on (relaxed image + dual); then the duals.
                for (int i = 1; i <= _n - 3; i++)
                {
                    DemandVector(i, out double dr, out double du);
                    dr = relax * dr + (1.0 - relax) * _zdr[i];
                    du = relax * du + (1.0 - relax) * _zdu[i];
                    double vr = dr + _wdr[i], vu = du + _wdu[i];
                    ProxDemand(_rhoD[i], ref vr, ref vu);
                    _wdr[i] += dr - vr;
                    _wdu[i] += du - vu;
                    _zdr[i] = vr;
                    _zdu[i] = vu;
                }
                for (int i = FixedCount; i < _n; i++)
                {
                    double ha = relax * _a[i] + (1.0 - relax) * _zpa[i];
                    double hb = relax * _b[i] + (1.0 - relax) * _zpb[i];
                    double va = ha + _wpa[i], vb = hb + _wpb[i];
                    ProxPoint(i, rho, ref va, ref vb);
                    _wpa[i] += ha - va;
                    _wpb[i] += hb - vb;
                    _zpa[i] = va;
                    _zpb[i] = vb;
                }
                for (int o = 0; o < _obsCenters.Count; o++)
                {
                    if (!_oActive[o]) continue;
                    Vector3 q = CurvePoint(_oSeg[o] + _oT[o]);
                    Vector3 hq = (float)relax * q + (float)(1.0 - relax) * _zo[o];
                    Vector3 vq = hq + _wo[o];
                    Vector3 rod = RodPoint(o, vq);
                    Vector3 away = vq - rod;
                    float d = away.magnitude;
                    float rad = _obsRadii[o];
                    if (d < rad)
                    {
                        if (d < 1e-3f) away = PushDirection(_oSeg[o]);
                        else away /= d;
                        vq = rod + away * rad;
                    }
                    _wo[o] += hq - vq;
                    _zo[o] = vq;
                }

                if (DebugLog != null && (it % 20 == 0 || it == iterations - 1))
                {
                    Diagnose();
                    DebugLog($"admm {it,3}: worst demand {WorstDemand:F2} miss {CrystalMiss:F2} clear {WorstClearDeficit:F2} obs {WorstObstacleDeficit:F2}");
                }
            }
            Iterations = it;
            Diagnose();
        }

        /// <summary>
        /// The banded system for the current set of copies: every demand stencil, every free
        /// point, and every ACTIVE obstacle — then factored. Rebuilt whenever an obstacle joins.
        /// </summary>
        void AssembleAndFactor(int nu, int need, double rho)
        {
            Array.Clear(_h, 0, need);
            for (int i = 1; i <= _n - 3; i++)
            {
                LoadRow(i, _rowR, _rowRc[i]);
                CommitMatrix(_rhoD[i]);
                LoadRow(i, _rowU, _rowUc[i]);
                CommitMatrix(_rhoD[i]);
            }
            for (int i = FixedCount; i < _n; i++)
            {
                Begin(0.0); Add(i, 1.0, 0.0); CommitMatrix(rho + 1e-6);
                Begin(0.0); Add(i, 0.0, 1.0); CommitMatrix(rho + 1e-6);
            }
            for (int o = 0; o < _obsCenters.Count; o++)
            {
                if (!_oActive[o]) continue;
                for (int axis = 0; axis < 3; axis++)
                {
                    ObstacleRow(o, axis);
                    CommitMatrix(rho);
                }
            }
            Factor(nu);
        }

        /// <summary>
        /// Brings into the solve every obstacle the line now passes within its radius plus
        /// <see cref="ObstacleActivation"/> of; true when any joined. An obstacle far from the line
        /// constrains nothing, and its copy is not free: ADMM pulls the line toward every copy, so
        /// hundreds of idle ones (a lap of rails along the route) act as inertia on the whole line,
        /// slow every iteration, and stop it moving to where the near ones need it. Once in, an
        /// obstacle stays in for the solve, so the system cannot flap.
        /// </summary>
        bool UpdateActivation()
        {
            bool changed = false;
            for (int o = 0; o < _obsCenters.Count; o++)
            {
                if (_oActive[o]) continue;
                Vector3 q = CurvePoint(_oSeg[o] + _oT[o]);
                float reach = _obsRadii[o] + ObstacleActivation;
                if ((q - RodPoint(o, q)).sqrMagnitude > reach * reach) continue;
                _oActive[o] = true;
                _zo[o] = q;
                _wo[o] = Vector3.zero;
                _activeObstacles++;
                changed = true;
            }
            return changed;
        }

        /// <summary>Obstacles further than this beyond their radius from the line are left out of a
        /// solve until the line comes near them.</summary>
        const float ObstacleActivation = 12f;

        /// <summary>Iterations between checks for obstacles the line has moved toward.</summary>
        const int ActivationCheckInterval = 10;

        /// <summary>Obstacles taking part in the last solve.</summary>
        public int ActiveObstacleCount => _activeObstacles;

        /// <summary>
        /// Proximal step of <c>w_D |z|² + w_H (|z| - B)²₊</c> against <c>rho |z - v|²</c>: the
        /// demand is shrunk radially — gently everywhere, hard beyond the budget.
        /// </summary>
        void ProxDemand(double rho, ref double zr, ref double zu)
        {
            double mag = Math.Sqrt(zr * zr + zu * zu);
            if (mag < 1e-12) return;
            double wD = _set.DemandWeight, wH = _set.HingeWeight, b = _set.Budget;
            double r = rho * mag / (wD + rho);
            if (r > b)
            {
                r = (wH * b + rho * mag) / (wD + wH + rho);
                if (r < b) r = b;
            }
            double scale = r / mag;
            zr *= scale;
            zu *= scale;
        }

        /// <summary>
        /// Proximal step for one point's offset: pulled toward the skim band in proportion to its
        /// weight, then off the face of the plates' clearance it is held off, then (for the crystal's
        /// point) into the pass circle — alternated, so a circle beside the plates lands on a point
        /// that satisfies both.
        /// </summary>
        void ProxPoint(int i, double rho, ref double va, ref double vb)
        {
            // Hand the point to the face it is now clearest of, if it is outside the box.
            float fa = (float)va, fb = (float)vb;
            if (Mathf.Abs(fa) >= _cw[i] || Mathf.Abs(fb) >= _ch[i]) _side[i] = NearestSide(i, fa, fb);

            // Toward the band: exact prox of w_B dist² to the reach region.
            if (_set.BandWeight > 0f)
            {
                double dx = Math.Abs(va) - _pw[i], dy = Math.Abs(vb) - _ph[i];
                double reach = _set.SkimReach;
                double ta = va, tb = vb;
                if (dx > 0.0 && dy > 0.0)
                {
                    double d = Math.Sqrt(dx * dx + dy * dy);
                    if (d > reach)
                    {
                        double ca = Math.Sign(va) * _pw[i], cb = Math.Sign(vb) * _ph[i];
                        ta = ca + (va - ca) * reach / d;
                        tb = cb + (vb - cb) * reach / d;
                    }
                }
                else if (dx > reach) ta = Math.Sign(va) * (_pw[i] + reach);
                else if (dy > reach) tb = Math.Sign(vb) * (_ph[i] + reach);
                double f = _set.BandWeight / (_set.BandWeight + rho);
                va -= f * (va - ta);
                vb -= f * (vb - tb);
            }

            for (int pass = 0; pass < 3; pass++)
            {
                ProjectClear(i, ref va, ref vb);
                if (i != _crystalIndex) break;
                double da = va - _crystalA, db = vb - _crystalB;
                double d = Math.Sqrt(da * da + db * db);
                double lim = _crystalR - ClearPad;
                if (d <= lim) break;
                va = _crystalA + da * lim / d;
                vb = _crystalB + db * lim / d;
            }
            ProjectClear(i, ref va, ref vb);
        }

        void ProjectClear(int i, ref double va, ref double vb)
        {
            switch (_side[i])
            {
                case 0: if (va < _cw[i] + ClearPad) va = _cw[i] + ClearPad; break;
                case 1: if (va > -(_cw[i] + ClearPad)) va = -(_cw[i] + ClearPad); break;
                case 2: if (vb < _ch[i] + ClearPad) vb = _ch[i] + ClearPad; break;
                default: if (vb > -(_ch[i] + ClearPad)) vb = -(_ch[i] + ClearPad); break;
            }
        }

        Vector3 PushDirection(int i)
        {
            Vector3 n = _r[i] * _a[i] + _u[i] * _b[i];
            return n.sqrMagnitude > 1e-4f ? n.normalized : _u[i];
        }

        /// <summary>The face of the clearance box (0 +a, 1 -a, 2 +b, 3 -b) the ray to (a, b) leaves
        /// through — the side of the plates the point is on.</summary>
        int NearestSide(int i, float a, float b)
        {
            float ra = Mathf.Abs(a) / Mathf.Max(1e-3f, _cw[i]);
            float rb = Mathf.Abs(b) / Mathf.Max(1e-3f, _ch[i]);
            if (ra >= rb) return a >= 0f ? 0 : 1;
            return b >= 0f ? 2 : 3;
        }

        // ========================================================================== terms

        /// <summary>
        /// Demand stencil at the midpoint between points i and i+1, from points i-1..i+2: the
        /// curvature as the average of the two second differences there, its rate of change as the
        /// third difference across them. Staggered on purpose: the CENTRED third difference
        /// (p[i+2] - 2p[i+1] + 2p[i-1] - p[i-2]) / 2 is exactly zero on a sequence that alternates
        /// point to point, so a solver using it is free to zig-zag the line at the grid's own
        /// wavelength — and does. This one charges that mode eight units of third difference.
        /// </summary>
        static void Stencil(float v, float k, float h, out double c0, out double c1, out double c2, out double c3)
        {
            double beta = v / (h * h);              // per unit of second difference
            double alpha = v * v / (k * h * h * h);  // per unit of third difference
            c0 = 0.5 * beta - alpha;
            c1 = -0.5 * beta + 3.0 * alpha;
            c2 = -0.5 * beta - 3.0 * alpha;
            c3 = 0.5 * beta + alpha;
        }

        static double Coef(int j, double c0, double c1, double c2, double c3)
            => j == -1 ? c0 : j == 0 ? c1 : j == 1 ? c2 : c3;

        /// <summary>Demand at stencil <paramref name="i"/> on the current line, in the ribbon's frame
        /// there, off the stored rows.</summary>
        void DemandVector(int i, out double dr, out double du)
        {
            dr = _rowRc[i];
            du = _rowUc[i];
            int b = i * 8;
            for (int j = 0; j < 4; j++)
            {
                int q = i - 1 + j;
                if (q < FixedCount) continue;   // folded into the constant
                dr += _rowR[b + 2 * j] * _a[q] + _rowR[b + 2 * j + 1] * _b[q];
                du += _rowU[b + 2 * j] * _a[q] + _rowU[b + 2 * j + 1] * _b[q];
            }
        }

        /// <summary>Stores stencil <paramref name="i"/>'s row along <paramref name="e"/>: per point
        /// its (a, b) coefficients — zero for a fixed point, whose part goes into the constant.</summary>
        void StoreRow(int i, Vector3 e, double c0, double c1, double c2, double c3, double[] row, out double constant)
        {
            constant = 0.0;
            int b = i * 8;
            for (int j = 0; j < 4; j++)
            {
                int q = i - 1 + j;
                double cj = Coef(j - 1, c0, c1, c2, c3);
                double ga = cj * Vector3.Dot(e, _r[q]), gb = cj * Vector3.Dot(e, _u[q]);
                constant += cj * Vector3.Dot(e, _c[q]);
                if (q < FixedCount)
                {
                    constant += ga * _a[q] + gb * _b[q];
                    ga = gb = 0.0;
                }
                row[b + 2 * j] = ga;
                row[b + 2 * j + 1] = gb;
            }
        }

        void LoadRow(int i, double[] row, double constant)
        {
            Begin(constant);
            int b = i * 8;
            for (int j = 0; j < 4; j++)
            {
                int q = i - 1 + j;
                if (q < FixedCount) continue;
                Add(q, row[b + 2 * j], row[b + 2 * j + 1]);
            }
        }

        /// <summary>Right-hand side of weight × (row·x + constant - target)² for a stored row.</summary>
        void RowRhs(int i, double[] row, double constant, double w, double target)
        {
            double c = constant - target;
            int b = i * 8;
            for (int j = 0; j < 4; j++)
            {
                int q = i - 1 + j;
                if (q < FixedCount) continue;
                int u = 2 * (q - FixedCount);
                _rhs[u] -= w * c * row[b + 2 * j];
                _rhs[u + 1] -= w * c * row[b + 2 * j + 1];
            }
        }

        void DemandRow(int i, Vector3 e, double c0, double c1, double c2, double c3, double offset)
        {
            Begin(offset);
            for (int j = -1; j <= 2; j++)
            {
                double cj = Coef(j, c0, c1, c2, c3);
                int q = i + j;
                _tc += cj * Vector3.Dot(e, _c[q]);
                Add(q, cj * Vector3.Dot(e, _r[q]), cj * Vector3.Dot(e, _u[q]));
            }
        }

        /// <summary>World coordinate <paramref name="axis"/> of the curve point obstacle
        /// <paramref name="o"/> is held off: the B-spline's four control points at its arc.</summary>
        void ObstacleRow(int o, int axis)
        {
            int j = _oSeg[o];
            SplineWeights(_oT[o], out float w0, out float w1, out float w2, out float w3);
            Begin(0.0);
            AddCurve(j - 1, w0, axis);
            AddCurve(j, w1, axis);
            AddCurve(j + 1, w2, axis);
            AddCurve(j + 2, w3, axis);
        }

        void AddCurve(int k, double w, int axis)
        {
            _tc += w * _c[k][axis];
            Add(k, w * _r[k][axis], w * _u[k][axis]);
        }

        // ========================================================================== assembly

        void Begin(double c)
        {
            _tn = 0;
            _tc = c;
        }

        void Add(int point, double ga, double gb)
        {
            if (point < FixedCount)
            {
                _tc += ga * _a[point] + gb * _b[point];
                return;
            }
            for (int k = 0; k < _tn; k++)
            {
                if (_ti[k] != point) continue;
                _tga[k] += ga;
                _tgb[k] += gb;
                return;
            }
            _ti[_tn] = point;
            _tga[_tn] = ga;
            _tgb[_tn] = gb;
            _tn++;
        }

        /// <summary>Adds weight × g gᵀ (the row's normal-equation matrix) to the system.</summary>
        void CommitMatrix(double w)
        {
            for (int p = 0; p < _tn; p++)
            {
                int up = 2 * (_ti[p] - FixedCount);
                for (int q = 0; q < _tn; q++)
                {
                    int uq = 2 * (_ti[q] - FixedCount);
                    AddH(up, uq, w * _tga[p] * _tga[q]);
                    AddH(up, uq + 1, w * _tga[p] * _tgb[q]);
                    AddH(up + 1, uq, w * _tgb[p] * _tga[q]);
                    AddH(up + 1, uq + 1, w * _tgb[p] * _tgb[q]);
                }
            }
        }

        /// <summary>Adds the right-hand side of weight × (g·x + c - target)².</summary>
        void CommitRhs(double w, double target)
        {
            double c = _tc - target;
            for (int p = 0; p < _tn; p++)
            {
                int up = 2 * (_ti[p] - FixedCount);
                _rhs[up] -= w * c * _tga[p];
                _rhs[up + 1] -= w * c * _tgb[p];
            }
        }

        void AddH(int row, int col, double v)
        {
            if (col > row) return;
            int k = row - col;
            if (k > Kb) throw new InvalidOperationException("coupling outside the band");
            _h[row * (Kb + 1) + k] += v;
        }

        /// <summary>In-place banded Cholesky: row r, column r - k lives at [r * (Kb + 1) + k].</summary>
        void Factor(int n)
        {
            for (int i = 0; i < n; i++)
            {
                int j0 = Math.Max(0, i - Kb);
                for (int j = j0; j <= i; j++)
                {
                    double sum = _h[i * (Kb + 1) + (i - j)];
                    int k0 = Math.Max(j0, j - Kb);
                    for (int k = k0; k < j; k++)
                        sum -= _h[i * (Kb + 1) + (i - k)] * _h[j * (Kb + 1) + (j - k)];
                    if (i == j) _h[i * (Kb + 1)] = Math.Sqrt(Math.Max(sum, 1e-12));
                    else _h[i * (Kb + 1) + (i - j)] = sum / _h[j * (Kb + 1)];
                }
            }
        }

        void SolveFactored(int n)
        {
            // L y = rhs
            for (int i = 0; i < n; i++)
            {
                double sum = _rhs[i];
                for (int k = Math.Max(0, i - Kb); k < i; k++) sum -= _h[i * (Kb + 1) + (i - k)] * _x[k];
                _x[i] = sum / _h[i * (Kb + 1)];
            }
            // L^T x = y
            for (int i = n - 1; i >= 0; i--)
            {
                double sum = _x[i];
                for (int k = i + 1; k <= Math.Min(n - 1, i + Kb); k++) sum -= _h[k * (Kb + 1) + (k - i)] * _x[k];
                _x[i] = sum / _h[i * (Kb + 1)];
            }
        }

        // ========================================================================== diagnostics

        void Diagnose()
        {
            float worst = 0f, excess = 0f;
            for (int i = 1; i <= _n - 3; i++)
            {
                float d = Demand(i);
                if (d > worst) worst = d;
                excess = Mathf.Max(excess, d - _set.Budget);
            }
            WorstDemand = worst;
            WorstExcess = excess;

            float clear = 0f;
            for (int i = FixedCount; i < _n; i++)
            {
                float da = _cw[i] - Mathf.Abs(_a[i]), db = _ch[i] - Mathf.Abs(_b[i]);
                if (da > 0f && db > 0f) clear = Mathf.Max(clear, Mathf.Min(da, db));
            }
            WorstClearDeficit = clear;

            CrystalMiss = 0f;
            if (_crystalIndex >= 0)
            {
                float da = _a[_crystalIndex] - _crystalA, db = _b[_crystalIndex] - _crystalB;
                CrystalMiss = Mathf.Max(0f, Mathf.Sqrt(da * da + db * db) - _crystalR);
            }

            float obs = 0f;
            for (int o = 0; o < _obsCenters.Count; o++)
            {
                Vector3 q = CurvePoint(_oSeg[o] + _oT[o]);
                obs = Mathf.Max(obs, _obsRadii[o] - (q - RodPoint(o, q)).magnitude);
            }
            WorstObstacleDeficit = obs;
        }

        /// <summary>
        /// The fastest (u/s, at most <paramref name="vCap"/>) the line as it now stands can be flown
        /// between points <paramref name="i"/> and i+1 inside <paramref name="budget"/> rad/s of stick:
        /// the largest v with <c>|v K + (v² / k) J| ≤ budget</c>, K and J the line's curvature and
        /// its rate of change there (the same stencil the solve bounds). Exact by bisection — the two
        /// terms are vectors, and where they point apart the sum is less than the bound on their sizes.
        /// </summary>
        public float MaxSpeed(int i, float budget, float vCap)
        {
            if (i < 1 || i > _n - 3) return vCap;
            Vector3 p0 = Point(i - 1), p1 = Point(i), p2 = Point(i + 1), p3 = Point(i + 2);
            float h = _step;
            Vector3 kv = 0.5f * (p0 - p1 - p2 + p3) / (h * h);
            Vector3 jv = (-p0 + 3f * p1 - 3f * p2 + p3) / (h * h * h);
            float kr = Vector3.Dot(kv, _r[i]), ku = Vector3.Dot(kv, _u[i]);
            float jr = Vector3.Dot(jv, _r[i]), ju = Vector3.Dot(jv, _u[i]);
            float k = Mathf.Max(0.05f, _set.FollowRate);
            float Demand(float v)
            {
                float dr = v * kr + v * v / k * jr, du = v * ku + v * v / k * ju;
                return Mathf.Sqrt(dr * dr + du * du);
            }
            if (Demand(vCap) <= budget) return vCap;
            float lo = 0f, hi = vCap;
            for (int it = 0; it < 24; it++)
            {
                float mid = 0.5f * (lo + hi);
                if (Demand(mid) <= budget) lo = mid; else hi = mid;
            }
            return lo;
        }

        /// <summary>Tooling: the last solve's view of the obstacle nearest <paramref name="p"/>.</summary>
        public string DescribeObstacleNear(Vector3 p)
        {
            int best = -1; float bd = float.MaxValue;
            for (int o = 0; o < _obsCenters.Count; o++)
            {
                float d = (_obsCenters[o] - p).sqrMagnitude;
                if (d < bd) { bd = d; best = o; }
            }
            if (best < 0) return "no obstacles";
            Vector3 q = CurvePoint(_oSeg[best] + _oT[best]);
            Vector3 rp = RodPoint(best, q);
            return $"nearest obstacle {Mathf.Sqrt(bd):F1} away: u={_obsU[best]:F2} (seg {_oSeg[best]} t {_oT[best]:F2} of n {_n}) " +
                   $"curve-to-rod {(q - rp).magnitude:F2} radius {_obsRadii[best]:F2}";
        }

        /// <summary>Tooling: one point's constraint state.</summary>
        public string Describe(int i)
            => $"side {_side[i]} clr({_cw[i]:F1},{_ch[i]:F1}) plate({_pw[i]:F1},{_ph[i]:F2}) v {_v[i]:F0}";

        /// <summary>The stick (rad/s) the line asks for between grid points <paramref name="i"/>
        /// and i+1.</summary>
        public float Demand(int i)
        {
            if (i < 1 || i > _n - 3) return 0f;
            DemandVector(i, out double dr, out double du);
            return (float)Math.Sqrt(dr * dr + du * du);
        }
    }
}
