// Hierarchical ecology (round 11f, Docs/ECOLOGY_LOD.md): the MICRO level - individual agents near pilots, struct-of-
// arrays, fixed 10 Hz tick. Port of Tools/Ecology/hierarchy/micro.py (research branch cece/eco-hierarchy); the
// research's cKDTree nearest-neighbour queries are a uniform-grid hash here (Burst has no KD-tree).
// Pure C#: compiled and RUN headless by Tools/Build/ecology_lod_harness.
//
// The SAME biology as the macro level: stomach, satiation-scaled Holling II grazing on the shared voxels, metabolism to
// the region's soil, a split at EBirth, starvation at 0, predators that eat a herbivore whole. What micro ADDS is
// space: grazers climb the food gradient and flee predators inside HFlee; predators chase the nearest grazer inside
// PSense and catch inside PCatch. The macro rates are FITTED to this level, never the other way round. Render-only
// state (Bloom, Emerge, Origin) never feeds back into the simulation.
using System;
using System.Collections.Generic;

namespace CosmicShore.Gameplay
{
    public sealed class EcologyMicroCore
    {
        public readonly EcologyWorld W;
        public readonly EcologyParams P;
        readonly EcologyRng _rng;
        public int Count;
        int _cap;
        public double[] Px, Py, Pz, Vx, Vy, Vz, Hx, Hy, Hz, E, Bloom, Emerge, Ox, Oy, Oz;
        public byte[] Sp, Elem;
        public long[] Id;
        long _nextId = 1;
        public long Kills;
        public readonly long[] Births = new long[2], Deaths = new long[2];
        public double LastStepMs;

        public EcologyMicroCore(EcologyWorld world, EcologyParams p, EcologyRng rng, int cap = 4096)
        {
            W = world; P = p; _rng = rng;
            Alloc(cap);
        }

        void Alloc(int cap)
        {
            T[] Grow<T>(T[] a, T fill)
            {
                var b = new T[cap];
                if (fill is not null && !fill.Equals(default(T))) Array.Fill(b, fill);
                if (a != null) Array.Copy(a, b, Count);
                return b;
            }
            Px = Grow(Px, 0.0); Py = Grow(Py, 0.0); Pz = Grow(Pz, 0.0);
            Vx = Grow(Vx, 0.0); Vy = Grow(Vy, 0.0); Vz = Grow(Vz, 0.0);
            Hx = Grow(Hx, 0.0); Hy = Grow(Hy, 0.0); Hz = Grow(Hz, 0.0);
            E = Grow(E, 0.0); Bloom = Grow(Bloom, 1.0); Emerge = Grow(Emerge, 1.0);
            Ox = Grow(Ox, 0.0); Oy = Grow(Oy, 0.0); Oz = Grow(Oz, 0.0);
            Sp = Grow(Sp, (byte)0); Elem = Grow(Elem, (byte)0); Id = Grow(Id, 0L);
            _cap = cap;
        }

        public EcologySpecies SpeciesOf(int i) => P.Species[Sp[i]];

        /// <summary>Adds one agent. Returns its row.</summary>
        public int Add(double x, double y, double z, int sp, int elem, double e, double ox, double oy, double oz,
                       double bloom = 1.0, double emerge = 1.0, double hx = double.NaN, double hy = 0, double hz = 0)
        {
            if (Count >= _cap) Alloc(Math.Max(_cap * 2, Count + 1));
            int i = Count++;
            Px[i] = x; Py[i] = y; Pz[i] = z;
            Sp[i] = (byte)sp; Elem[i] = (byte)elem; E[i] = e; Id[i] = _nextId++;
            if (double.IsNaN(hx)) { hx = _rng.Normal(); hy = _rng.Normal(); hz = _rng.Normal(); }
            double hn = Math.Max(Math.Sqrt(hx * hx + hy * hy + hz * hz), 1e-9);
            Hx[i] = hx / hn; Hy[i] = hy / hn; Hz[i] = hz / hn;
            double spd = P.Species[sp].Speed;
            Vx[i] = Hx[i] * spd; Vy[i] = Hy[i] * spd; Vz[i] = Hz[i] * spd;
            Ox[i] = ox; Oy[i] = oy; Oz[i] = oz;
            Bloom[i] = bloom; Emerge[i] = emerge;
            return i;
        }

        /// <summary>Compacts away rows where <paramref name="remove"/> is true (the caller has booked their mass).</summary>
        public void Remove(bool[] remove)
        {
            int m = 0;
            for (int i = 0; i < Count; i++)
            {
                if (remove[i]) continue;
                if (m != i)
                {
                    Px[m] = Px[i]; Py[m] = Py[i]; Pz[m] = Pz[i]; Vx[m] = Vx[i]; Vy[m] = Vy[i]; Vz[m] = Vz[i];
                    Hx[m] = Hx[i]; Hy[m] = Hy[i]; Hz[m] = Hz[i]; E[m] = E[i]; Bloom[m] = Bloom[i]; Emerge[m] = Emerge[i];
                    Ox[m] = Ox[i]; Oy[m] = Oy[i]; Oz[m] = Oz[i]; Sp[m] = Sp[i]; Elem[m] = Elem[i]; Id[m] = Id[i];
                }
                m++;
            }
            Count = m;
        }

        public double Mass()
        {
            double s = 0;
            for (int i = 0; i < Count; i++) s += E[i] + P.Species[Sp[i]].Body;
            return s;
        }

        /// <summary>Where agent i is DRAWN: its emergence eases it out of the representative it came from.</summary>
        public void Drawn(int i, out double x, out double y, out double z)
        {
            double e = Emerge[i], ease = e * e * (3 - 2 * e);
            x = Ox[i] * (1 - ease) + Px[i] * ease; y = Oy[i] * (1 - ease) + Py[i] * ease; z = Oz[i] * (1 - ease) + Pz[i] * ease;
        }

        // ── the neighbour grid (stands in for the research's cKDTree) ────────────────────────────────────────────

        sealed class Grid
        {
            public double H; public int Mod;
            public int[] Head = Array.Empty<int>(), Next = Array.Empty<int>();
            public void Build(double h, EcologyMicroCore a, int sp, double[] px, double[] py, double[] pz, int n)
            {
                H = h; Mod = 1; while (Mod < 2 * n + 16) Mod <<= 1;
                if (Head.Length < Mod) Head = new int[Mod];
                Array.Fill(Head, -1, 0, Mod);
                if (Next.Length < n) Next = new int[Math.Max(n, 16)];
                for (int i = 0; i < n; i++)
                {
                    if (a.Sp[i] != sp) continue;
                    int b = Bucket(Cell(px[i]), Cell(py[i]), Cell(pz[i]));
                    Next[i] = Head[b]; Head[b] = i;
                }
            }
            public long Cell(double x) => (long)Math.Floor(x / H);
            public int Bucket(long cx, long cy, long cz) =>
                (int)((ulong)((cx * 73856093L) ^ (cy * 19349663L) ^ (cz * 83492791L)) & (ulong)(Mod - 1));

            /// <summary>Nearest member within <paramref name="rmax"/> of (x,y,z), or -1.</summary>
            public int Nearest(double x, double y, double z, double rmax, double[] px, double[] py, double[] pz, out double dist)
            {
                long cx = Cell(x), cy = Cell(y), cz = Cell(z);
                int best = -1; double bd = rmax * rmax;
                Span<int> seen = stackalloc int[27]; int ns = 0;
                for (long dx = -1; dx <= 1; dx++) for (long dy = -1; dy <= 1; dy++) for (long dz = -1; dz <= 1; dz++)
                {
                    int b = Bucket(cx + dx, cy + dy, cz + dz);
                    bool dup = false; for (int q = 0; q < ns; q++) dup |= seen[q] == b;
                    if (dup) continue; seen[ns++] = b;
                    for (int j = Head[b]; j >= 0; j = Next[j])
                    {
                        double ex = px[j] - x, ey = py[j] - y, ez = pz[j] - z, d = ex * ex + ey * ey + ez * ez;
                        if (d < bd) { bd = d; best = j; }
                    }
                }
                dist = best >= 0 ? Math.Sqrt(bd) : double.PositiveInfinity;
                return best;
            }
        }

        readonly Grid _gH = new(), _gP = new();
        double[] _demand = Array.Empty<double>(), _scale = Array.Empty<double>();
        int[] _stamp = Array.Empty<int>();
        int _stampNow;
        readonly List<int> _touched = new();
        bool[] _dead = Array.Empty<bool>(), _sprint = Array.Empty<bool>(), _starve = Array.Empty<bool>();
        int[] _reg = Array.Empty<int>(), _vox = Array.Empty<int>();
        double[] _sx = Array.Empty<double>(), _sy = Array.Empty<double>(), _sz = Array.Empty<double>(), _want = Array.Empty<double>();

        // ── one micro tick ───────────────────────────────────────────────────────────────────────────────────────

        public void Step(double dt)
        {
            long t0 = System.Diagnostics.Stopwatch.GetTimestamp();
            int n = Count;
            if (n == 0) { LastStepMs = 0; return; }
            if (_dead.Length < n)
            {
                int c = Math.Max(n, 2 * _dead.Length);
                _dead = new bool[c]; _sprint = new bool[c]; _starve = new bool[c]; _reg = new int[c]; _vox = new int[c];
                _sx = new double[c]; _sy = new double[c]; _sz = new double[c]; _want = new double[c];
            }
            if (_demand.Length < W.NReg * W.NVox)
            {
                _demand = new double[W.NReg * W.NVox]; _scale = new double[W.NReg * W.NVox]; _stamp = new int[W.NReg * W.NVox];
            }
            _stampNow++;
            var Hs = P.Species[0]; var Ps = P.Species[1];
            int nv = W.NVox;
            for (int i = 0; i < n; i++)
            {
                _reg[i] = W.RegionOfInside(Px[i], Py[i], Pz[i]);
                _vox[i] = W.VoxelOf(Px[i], Py[i], Pz[i], _reg[i]);
                _dead[i] = false; _sprint[i] = false; _starve[i] = false;
                _sx[i] = _sy[i] = _sz[i] = 0;
            }

            // ---- grazing (herbivores) on the shared voxel field, demand scaled per voxel when it exceeds the food
            _touched.Clear();
            for (int i = 0; i < n; i++)
            {
                if (Sp[i] != 0) continue;
                int key = _reg[i] * nv + _vox[i];
                double G = W.F[key] + W.K[key], sat = Math.Clamp(1.0 - E[i] / Hs.EMax, 0.0, 1.0);
                _want[i] = P.HIntake * G / (G + P.HHalf) * sat * dt;
                if (_stamp[key] != _stampNow) { _stamp[key] = _stampNow; _demand[key] = 0; _touched.Add(key); }
                _demand[key] += _want[i];
            }
            for (int q = 0; q < _touched.Count; q++)
            {
                int key = _touched[q];
                double G = W.F[key] + W.K[key], dem = _demand[key];
                double sc = dem > G ? G / Math.Max(dem, 1e-12) : 1.0, took = dem * sc;
                double fF = G > 0 ? W.F[key] / Math.Max(G, 1e-12) : 0.0;
                W.F[key] -= took * fF; W.K[key] -= took * (1 - fF);
                _scale[key] = sc;
            }
            for (int i = 0; i < n; i++)
            {
                if (Sp[i] != 0) continue;
                int key = _reg[i] * nv + _vox[i];
                E[i] += _want[i] * _scale[key];
            }

            // ---- neighbour grids
            double h = Math.Max(P.PSense, P.HFlee);
            _gH.Build(h, this, 0, Px, Py, Pz, n);
            _gP.Build(h, this, 1, Px, Py, Pz, n);

            // ---- predators: chase the nearest herbivore inside PSense, catch inside PCatch (first predator wins)
            for (int i = 0; i < n; i++)
            {
                if (Sp[i] != 1 || E[i] / Ps.EMax >= P.PHuntBelow) continue;
                int j = _gH.Nearest(Px[i], Py[i], Pz[i], P.PSense, Px, Py, Pz, out double d);
                if (j < 0) continue;
                double inv = 3.0 / Math.Max(d, 1e-6);
                _sx[i] = (Px[j] - Px[i]) * inv; _sy[i] = (Py[j] - Py[i]) * inv; _sz[i] = (Pz[j] - Pz[i]) * inv;
                _sprint[i] = true;
                if (d < P.PCatch && !_dead[j])
                {
                    E[i] += Hs.Body + E[j];
                    _dead[j] = true;
                    Kills++;
                }
            }
            // ---- herbivores: flee the nearest predator inside HFlee; else climb the food gradient
            for (int i = 0; i < n; i++)
            {
                if (Sp[i] != 0) continue;
                int j = _gP.Nearest(Px[i], Py[i], Pz[i], P.HFlee, Px, Py, Pz, out double d);
                if (j >= 0)
                {
                    double inv = 4.0 / Math.Max(d, 1e-6);
                    _sx[i] += (Px[i] - Px[j]) * inv; _sy[i] += (Py[i] - Py[j]) * inv; _sz[i] += (Pz[i] - Pz[j]) * inv;
                    _sprint[i] = true;
                }
                if (!_sprint[i])
                {
                    FoodGradient(i, _reg[i], _vox[i], out double gx, out double gy, out double gz);
                    double k = E[i] / Hs.EBirth < Hs.PhaseLo ? 0.4 : 1.0;
                    _sx[i] += gx * k; _sy[i] += gy * k; _sz[i] += gz * k;
                }
            }
            // ---- persistent wander heading, velocity, membrane
            double sq = Math.Sqrt(dt), kv = Math.Min(1.0, 4.0 * dt), rIn = W.R * 0.985;
            for (int i = 0; i < n; i++)
            {
                var s = P.Species[Sp[i]];
                double fb = E[i] / s.EBirth;
                bool hungry = fb < s.PhaseLo, sated = fb > s.PhaseHi;
                double jit = 0.35 * sq * (hungry ? 0.4 : 1.0);
                double hx = Hx[i] + _rng.Normal() * jit, hy = Hy[i] + _rng.Normal() * jit, hz = Hz[i] + _rng.Normal() * jit;
                double hn = Math.Max(Math.Sqrt(hx * hx + hy * hy + hz * hz), 1e-9);
                Hx[i] = hx / hn; Hy[i] = hy / hn; Hz[i] = hz / hn;
                double wk = hungry ? 1.2 : 0.6;
                double sx = _sx[i] + Hx[i] * wk, sy = _sy[i] + Hy[i] * wk, sz = _sz[i] + Hz[i] * wk;
                double spd = _sprint[i] ? s.Sprint : s.Speed;
                if (sated && !_sprint[i]) spd *= 0.5;
                double nrm = Math.Sqrt(sx * sx + sy * sy + sz * sz);
                double wx, wy, wz;
                if (nrm > 1e-9) { wx = sx / nrm; wy = sy / nrm; wz = sz / nrm; } else { wx = Hx[i]; wy = Hy[i]; wz = Hz[i]; }
                Vx[i] += (wx * spd - Vx[i]) * kv; Vy[i] += (wy * spd - Vy[i]) * kv; Vz[i] += (wz * spd - Vz[i]) * kv;
                Px[i] += Vx[i] * dt; Py[i] += Vy[i] * dt; Pz[i] += Vz[i] * dt;
                double r = Math.Sqrt(Px[i] * Px[i] + Py[i] * Py[i] + Pz[i] * Pz[i]);
                if (r > rIn)
                {
                    double ux = Px[i] / r, uy = Py[i] / r, uz = Pz[i] / r;
                    Px[i] = ux * rIn; Py[i] = uy * rIn; Pz[i] = uz * rIn;
                    double vn = Math.Max(Vx[i] * ux + Vy[i] * uy + Vz[i] * uz, 0);
                    Vx[i] -= 2 * vn * ux; Vy[i] -= 2 * vn * uy; Vz[i] -= 2 * vn * uz;
                    double hd = Math.Max(Hx[i] * ux + Hy[i] * uy + Hz[i] * uz, 0);
                    Hx[i] -= 2 * hd * ux; Hy[i] -= 2 * hd * uy; Hz[i] -= 2 * hd * uz;
                }
            }
            // ---- metabolism -> the soil of the region the agent stood in; starvation -> skeleton in its voxel
            for (int i = 0; i < n; i++)
            {
                if (_dead[i]) continue;
                var s = P.Species[Sp[i]];
                double burn = Math.Min((s.Metab + (_sprint[i] ? s.SprintMetab : 0.0)) * dt, Math.Max(E[i], 0.0));
                E[i] -= burn;
                W.N[_reg[i]] += burn;
                if (E[i] <= 1e-9)
                {
                    _starve[i] = true;
                    W.K[_reg[i] * nv + _vox[i]] += s.Body;
                    W.N[_reg[i]] += E[i];          // the last (~0) stomach -> soil, exactly
                    Deaths[Sp[i]]++;
                }
            }
            // ---- births: split at EBirth, the offspring at the parent's side (blooms from nothing)
            for (int i = 0; i < n; i++)
            {
                Bloom[i] = Math.Min(Bloom[i] + dt / Math.Max(P.BloomS, 1e-6), 1.0);
                Emerge[i] = Math.Min(Emerge[i] + dt / Math.Max(P.BloomS, 1e-6), 1.0);
            }
            for (int i = 0; i < n; i++)
            {
                if (_dead[i] || _starve[i]) continue;
                var s = P.Species[Sp[i]];
                if (E[i] < s.EBirth) continue;
                E[i] -= (P.Bug == EcologyBug.BirthFreeBody ? 0.0 : s.Body) + s.E0;
                Births[Sp[i]]++;
                Add(Px[i] + _rng.Normal() * 2.0, Py[i] + _rng.Normal() * 2.0, Pz[i] + _rng.Normal() * 2.0, Sp[i], Elem[i], s.E0,
                    Px[i], Py[i], Pz[i], bloom: 0.0);
            }
            if (_dead.Length < Count) Array.Resize(ref _dead, Count);
            for (int i = 0; i < Count; i++) _dead[i] = i < n && (_dead[i] || _starve[i]);
            Remove(_dead);
            LastStepMs = (System.Diagnostics.Stopwatch.GetTimestamp() - t0) * 1000.0 / System.Diagnostics.Stopwatch.Frequency;
        }

        /// <summary>Toward the richest of the 6 neighbouring voxels (crossing region faces), scaled by the improvement.</summary>
        void FoodGradient(int i, int reg, int vox, out double gx, out double gy, out double gz)
        {
            int nv = W.NVox; double hh = W.VoxH;
            double here = W.F[reg * nv + vox] + W.K[reg * nv + vox], bestv = here;
            gx = gy = gz = 0; int bd = -1;
            for (int d = 0; d < 6; d++)
            {
                double dx = d == 0 ? 1 : d == 3 ? -1 : 0, dy = d == 1 ? 1 : d == 4 ? -1 : 0, dz = d == 2 ? 1 : d == 5 ? -1 : 0;
                double qx = Px[i] + dx * hh, qy = Py[i] + dy * hh, qz = Pz[i] + dz * hh;
                int rq = W.RegionOf(qx, qy, qz);
                if (rq < 0) continue;
                int vq = W.VoxelOf(qx, qy, qz, rq);
                double v = W.F[rq * nv + vq] + W.K[rq * nv + vq];
                if (v > bestv * 1.05) { bestv = v; bd = d; }
            }
            if (bd < 0) return;
            double gain = Math.Clamp((bestv - here) / Math.Max(here + 10.0, 1e-6), 0, 1), k = 0.3 + gain;
            gx = (bd == 0 ? 1 : bd == 3 ? -1 : 0) * k; gy = (bd == 1 ? 1 : bd == 4 ? -1 : 0) * k; gz = (bd == 2 ? 1 : bd == 5 ? -1 : 0) * k;
        }
    }
}
