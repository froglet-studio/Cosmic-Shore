// Hierarchical ecology (round 11f, Docs/ECOLOGY_LOD.md): the LOD manager between the two levels, the impostor
// representatives and the closed ledger. Port of Tools/Ecology/hierarchy/sim.py (research branch cece/eco-hierarchy).
// Pure C#: compiled and RUN headless by Tools/Build/ecology_lod_harness.
//
// LOD rules (all in EcologyLodRules, shared with the game's per-population LOD - IMacroPopulation):
//   HOT       a region whose centre is within ExpandRadius (280) of a pilot, or within ExpandAhead (450) inside its
//             forward cone (cos 0.5): prefetch - the region is populated before the pilot gets there.
//   COLD      a hot region with no pilot inside CollapseRadius (400) nor ExpandAhead + 80 inside the cone (hysteresis).
//   EXPAND    cold -> hot: every macro individual of the region becomes an agent NOW, sampled from its cohort
//             (stomach from the cohort's Normal inside [Lo, Hi], shifted so the cohort's Σstomach is exact; grazers
//             rejection-sampled outside the predators' flee radius - a fresh uniform scatter is a massacre, 114 vs 11
//             kills in 30 s) and DRAWN easing out of one of the region's impostor representatives over BloomS.
//   ABSORB    agent -> macro, only when NO pilot can see it at either its simulated or its drawn position.
//   ARRIVE    a macro hop from a cold region into a hot one becomes an agent walking out of the source region's
//             representative nearest the destination.
using System;
using System.Collections.Generic;

namespace CosmicShore.Gameplay
{
    /// <summary>A pilot as the LOD sees it: a position, a velocity (its forward cone), and the research's wanderer.</summary>
    public struct EcologyPilot
    {
        public double X, Y, Z, Vx, Vy, Vz;
        public double Speed, Turn, Gx, Gy, Gz;
    }

    /// <summary>The LOD's geometric rules - one copy for the region manager here and for every IMacroPopulation.</summary>
    public static class EcologyLodRules
    {
        static void Rel(in EcologyPilot p, double x, double y, double z, out double r, out double cos)
        {
            double dx = x - p.X, dy = y - p.Y, dz = z - p.Z;
            r = Math.Sqrt(dx * dx + dy * dy + dz * dz);
            double vn = Math.Sqrt(p.Vx * p.Vx + p.Vy * p.Vy + p.Vz * p.Vz);
            cos = vn > 1e-9 && r > 1e-9 ? (dx * p.Vx + dy * p.Vy + dz * p.Vz) / (r * vn) : 0.0;
        }

        /// <summary>Can any pilot SEE a body at (x,y,z) whose extent is <paramref name="extent"/>? Within NearSeeRadius
        /// regardless of heading, or within VisibleRadius inside the forward cone (research sim.visible; the extent
        /// widens both, conservatively, for a population drawn as one body - a whole swarm).</summary>
        public static bool Visible(ReadOnlySpan<EcologyPilot> pilots, EcologyParams P, double x, double y, double z, double extent = 0)
        {
            for (int i = 0; i < pilots.Length; i++)
            {
                Rel(pilots[i], x, y, z, out double r, out double cos);
                if (r < P.NearSeeRadius + extent) return true;
                if (r >= P.VisibleRadius + extent) continue;
                if (extent <= 0) { if (cos > P.ConeCos) return true; continue; }
                // a body of radius `extent` is in the cone when the cone, widened by the body's angular radius, holds
                // its centre
                double half = Math.Acos(Math.Clamp(P.ConeCos, -1, 1)) + Math.Asin(Math.Min(1.0, extent / Math.Max(r, 1e-9)));
                if (half >= Math.PI || cos > Math.Cos(half)) return true;
            }
            return false;
        }

        /// <summary>Should a population at (x,y,z) be expanded? <paramref name="hot"/> is its current state (the
        /// hysteresis); <paramref name="scale"/> the agent-budget radius scale (1 = the research radii).</summary>
        public static bool WantHot(ReadOnlySpan<EcologyPilot> pilots, EcologyParams P, double x, double y, double z, bool hot,
                                   double extent = 0, double scale = 1)
        {
            for (int i = 0; i < pilots.Length; i++)
            {
                Rel(pilots[i], x, y, z, out double r, out double cos);
                r = Math.Max(0, r - extent);
                if (r < P.ExpandRadius * scale || (r < P.ExpandAhead * scale && cos > P.ConeCos)) return true;
                if (hot && (r < P.CollapseRadius * scale || (r < (P.ExpandAhead + 80) * scale && cos > P.ConeCos))) return true;
            }
            return false;
        }

        /// <summary>The research wanderer (common/arena.py): turn toward a goal at Turn rad/s, a new volume-uniform goal
        /// in the 0.2R-0.9R shell when within 60 u, clamped inside 0.97R.</summary>
        public static void StepWanderer(ref EcologyPilot p, double R, double dt, EcologyRng rng)
        {
            double wx = p.Gx - p.X, wy = p.Gy - p.Y, wz = p.Gz - p.Z, wn = Math.Sqrt(wx * wx + wy * wy + wz * wz);
            if (wn < 60.0) { Ball(rng, 0.2 * R, 0.9 * R, out p.Gx, out p.Gy, out p.Gz); wx = p.Gx - p.X; wy = p.Gy - p.Y; wz = p.Gz - p.Z; wn = Math.Sqrt(wx * wx + wy * wy + wz * wz); }
            if (wn > 1e-6)
            {
                double vn = Math.Max(Math.Sqrt(p.Vx * p.Vx + p.Vy * p.Vy + p.Vz * p.Vz), 1e-6);
                double vx = p.Vx / vn, vy = p.Vy / vn, vz = p.Vz / vn; wx /= wn; wy /= wn; wz /= wn;
                double ang = Math.Acos(Math.Clamp(vx * wx + vy * wy + vz * wz, -1, 1));
                double k = Math.Min(1.0, p.Turn * dt / Math.Max(ang, 1e-6));
                double nx = vx + (wx - vx) * k, ny = vy + (wy - vy) * k, nz = vz + (wz - vz) * k, nn = Math.Max(Math.Sqrt(nx * nx + ny * ny + nz * nz), 1e-6);
                p.Vx = nx / nn * p.Speed; p.Vy = ny / nn * p.Speed; p.Vz = nz / nn * p.Speed;
            }
            p.X += p.Vx * dt; p.Y += p.Vy * dt; p.Z += p.Vz * dt;
            double r = Math.Sqrt(p.X * p.X + p.Y * p.Y + p.Z * p.Z);
            if (r > R * 0.97) { double s = R * 0.97 / r; p.X *= s; p.Y *= s; p.Z *= s; }
        }

        public static void Ball(EcologyRng rng, double rLo, double rHi, out double x, out double y, out double z)
        {
            double dx = rng.Normal(), dy = rng.Normal(), dz = rng.Normal(), n = Math.Max(Math.Sqrt(dx * dx + dy * dy + dz * dz), 1e-12);
            double r = Math.Cbrt(rLo * rLo * rLo + rng.Uniform() * (rHi * rHi * rHi - rLo * rLo * rLo));
            x = dx / n * r; y = dy / n * r; z = dz / n * r;
        }

        public static EcologyPilot Wanderer(EcologyRng rng, double R, double speed, double turn = 2.0)
        {
            var p = new EcologyPilot { Speed = speed, Turn = turn };
            Ball(rng, 0.5 * R, 0.8 * R, out p.X, out p.Y, out p.Z);
            double dx = rng.Normal(), dy = rng.Normal(), dz = rng.Normal(), n = Math.Max(Math.Sqrt(dx * dx + dy * dy + dz * dz), 1e-12);
            p.Vx = dx / n * speed; p.Vy = dy / n * speed; p.Vz = dz / n * speed;
            Ball(rng, 0.2 * R, 0.9 * R, out p.Gx, out p.Gy, out p.Gz);
            return p;
        }
    }

    /// <summary>Per-species summary over a set of regions (research HierSim.summary).</summary>
    public struct EcologySpeciesSummary
    {
        public long Count;
        public double MeanE, SdE, Sated, Forage, Hungry;
    }

    public sealed class EcologyLodSim
    {
        public readonly EcologyParams P;
        public readonly EcologyRng Rng;
        public readonly EcologyWorld W;
        public readonly EcologyMacroCore M;
        public readonly EcologyMicroCore A;
        public readonly List<EcologyPilot> Pilots = new();
        public double T;
        public long K;
        public bool[] Hot;
        /// <summary>Overrides the pilot rule (tests): every region hot where true.</summary>
        public bool[] ForceHot;
        public bool Lod = true;
        public double LodScale = 1.0;
        /// <summary>The visibility the ABSORB rule asks (a planted bug may blind it; the continuity gate keeps its own).</summary>
        public Func<double, double, double, bool> VisibleForAbsorb;
        public long Expands, Absorbs, Arrives;
        public double Settle;
        public readonly HashSet<long> ArrivedIds = new();
        public double MacroMs, MicroMs, LodMs, FloraMs; public long MacroSteps, Steps;
        /// <summary>Called around every LOD pass and inbox drain (the conservation gate checks mass and headcount
        /// before == after); null in normal runs.</summary>
        public Action<bool> LodHook;

        // impostors: RepsPerRegion per (region, species), drifting toward fresh samples of the occupancy
        public readonly double[] RepX, RepY, RepZ, TgtX, TgtY, TgtZ;   // [(r*2 + s)*k + j]

        public EcologyLodSim(EcologyParams p, ulong seed = 7, double? radius = null)
        {
            P = p; Rng = new EcologyRng(seed);
            W = new EcologyWorld(p, radius);
            M = new EcologyMacroCore(W, p, Rng);
            A = new EcologyMicroCore(W, p, Rng);
            Hot = new bool[W.NReg];
            int n = W.NReg * 2 * p.RepsPerRegion;
            RepX = new double[n]; RepY = new double[n]; RepZ = new double[n];
            TgtX = new double[n]; TgtY = new double[n]; TgtZ = new double[n];
            for (int r = 0; r < W.NReg; r++)
                for (int q = 0; q < 2 * p.RepsPerRegion; q++)
                {
                    int i = r * 2 * p.RepsPerRegion + q;
                    RepX[i] = W.Cx[r] + Rng.Uniform(-60, 60); RepY[i] = W.Cy[r] + Rng.Uniform(-60, 60); RepZ[i] = W.Cz[r] + Rng.Uniform(-60, 60);
                }
            VisibleForAbsorb = Visible;
        }

        /// <summary>Plant flora, then seed the macro cohorts (research HierSim.populate).</summary>
        public void Populate(long nHerb, long nPred, double floraFill = 0.5, double nutrient = 3000.0)
        {
            W.SeedFlora(Rng, floraFill, nutrient);
            var pr = new double[W.NReg]; double gs = 0;
            for (int r = 0; r < W.NReg; r++) { pr[r] = W.FloraIn(r); gs += pr[r]; }
            var cum = new double[W.NReg]; double acc = 0;
            for (int r = 0; r < W.NReg; r++) { acc += pr[r] / gs; cum[r] = acc; }
            for (int s = 0; s < 2; s++)
            {
                long N = s == 0 ? nHerb : nPred; double e = s == 0 ? 12.0 : 60.0;
                for (long i = 0; i < N; i++)
                {
                    double u = Rng.Uniform();
                    int r = Array.BinarySearch(cum, u); if (r < 0) r = ~r; r = Math.Min(r, W.NReg - 1);
                    int el = Rng.Int(4);
                    double st = Math.Clamp(Rng.Normal(e, 0.15 * e), 0.2 * e, 1.6 * e);
                    M.Pops[s].Place(r, el, st);
                }
            }
            RetargetReps(true);
            Array.Copy(TgtX, RepX, RepX.Length); Array.Copy(TgtY, RepY, RepY.Length); Array.Copy(TgtZ, RepZ, RepZ.Length);
        }

        public void AddWanderer(double speed, double turn = 1.0) => Pilots.Add(EcologyLodRules.Wanderer(Rng, W.R, speed, turn));

        public double Ledger() => W.Mass() + M.Mass() + A.Mass();

        public bool Visible(double x, double y, double z)
        {
            var ps = Pilots.Count == 0 ? ReadOnlySpan<EcologyPilot>.Empty : PilotSpan();
            return EcologyLodRules.Visible(ps, P, x, y, z);
        }

        EcologyPilot[] _pilotArr = Array.Empty<EcologyPilot>();
        ReadOnlySpan<EcologyPilot> PilotSpan()
        {
            if (_pilotArr.Length != Pilots.Count) _pilotArr = new EcologyPilot[Pilots.Count];
            Pilots.CopyTo(_pilotArr);
            return _pilotArr;
        }

        // ── stepping ─────────────────────────────────────────────────────────────────────────────────────────────

        static double Now() => System.Diagnostics.Stopwatch.GetTimestamp() * 1000.0 / System.Diagnostics.Stopwatch.Frequency;

        /// <summary>One micro tick (DtMicro); flora, LOD, the macro step and the inbox run every DtMacro.</summary>
        public void Step()
        {
            double dt = P.DtMicro;
            int per = Math.Max(1, (int)Math.Round(P.DtMacro / dt));
            if (K % per == 0)
            {
                double t0 = Now();
                W.StepFlora(P.DtMacro);
                double t1 = Now();
                LodHook?.Invoke(true); Lod_(); LodHook?.Invoke(false);
                double t2 = Now();
                M.Step(P.DtMacro, Hot);
                LodHook?.Invoke(true); DrainInbox(); LodHook?.Invoke(false);
                AdvectReps(P.DtMacro);
                double t3 = Now();
                FloraMs += t1 - t0; LodMs += t2 - t1; MacroMs += t3 - t2; MacroSteps++;
            }
            double m0 = Now();
            A.Step(dt);
            MicroMs += Now() - m0;
            for (int i = 0; i < Pilots.Count; i++) { var p = Pilots[i]; EcologyLodRules.StepWanderer(ref p, W.R, dt, Rng); Pilots[i] = p; }
            T += dt; K++; Steps++;
        }

        /// <summary>Runs the LOD pass now (tests: e.g. expand everything to read a summary off agents).</summary>
        public void LodNow() => Lod_();

        void Lod_()
        {
            var want = new bool[W.NReg];
            if (ForceHot != null) Array.Copy(ForceHot, want, want.Length);
            else if (Lod && Pilots.Count > 0)
            {
                double target = A.Count > P.AgentBudget ? Math.Min(1.0, Math.Cbrt((double)P.AgentBudget / Math.Max(A.Count, 1))) : 1.0;
                LodScale += (target - LodScale) * 0.2;
                var ps = PilotSpan();
                for (int r = 0; r < W.NReg; r++)
                    want[r] = EcologyLodRules.WantHot(ps, P, W.Cx[r], W.Cy[r], W.Cz[r], Hot[r], 0, LodScale);
            }
            for (int r = 0; r < W.NReg; r++)
            {
                bool newly = want[r] && !Hot[r];
                Hot[r] = want[r];
                if (newly) Expand(r);
            }
            Absorb();
        }

        // ── expand ───────────────────────────────────────────────────────────────────────────────────────────────

        readonly List<(double x, double y, double z)> _predPos = new();
        double[] _cumOcc = Array.Empty<double>();

        int SampleVoxel(int r, bool occupancy)
        {
            int nv = W.NVox;
            if (_cumOcc.Length < nv) _cumOcc = new double[nv];
            double acc = 0;
            for (int v = 0; v < nv; v++)
            {
                acc += occupancy ? M.Occ[r * nv + v] : W.VoxOk[r * nv + v] / Math.Max(W.VoxOkSum[r], 1);
                _cumOcc[v] = acc;
            }
            double u = Rng.Uniform() * acc;
            for (int v = 0; v < nv; v++) if (u < _cumOcc[v]) return v;
            return nv - 1;
        }

        void SamplePos(int r, bool occupancy, out double x, out double y, out double z)
        {
            int v = SampleVoxel(r, occupancy);
            x = W.Cx[r] + W.VoxOffX[v] + Rng.Uniform(-0.5, 0.5) * W.VoxH;
            y = W.Cy[r] + W.VoxOffY[v] + Rng.Uniform(-0.5, 0.5) * W.VoxH;
            z = W.Cz[r] + W.VoxOffZ[v] + Rng.Uniform(-0.5, 0.5) * W.VoxH;
        }

        void Expand(int r)
        {
            _predPos.Clear();
            int C = P.Cohorts, k = P.RepsPerRegion;
            for (int s = 1; s >= 0; s--)            // predators first; grazers then avoid them
            {
                var pop = M.Pops[s]; var sp = pop.Sp;
                long regN = pop.RegionCount(r); double regS = 0;
                for (int c = 0; c < C; c++) regS += pop.S[r * C + c];
                double regMean = regS / Math.Max(regN, 1);
                for (int c = 0; c < C; c++)
                {
                    int rc = r * C + c;
                    pop.MeanVar(rc, out long cnt, out double m, out double v);
                    if (cnt <= 0) continue;
                    double target = pop.S[rc], sd = Math.Sqrt(v);
                    var Ev = new double[cnt]; var el = new int[cnt];
                    int q = 0;
                    for (int e = 0; e < 4; e++) for (long j = 0; j < pop.N[rc * 4 + e]; j++) el[q++] = e;
                    double lo = Math.Max(pop.Lo[rc], 0.05 * sp.E0), hi = Math.Min(pop.Hi[rc], sp.EBirth - 1e-3);
                    double sum = 0;
                    for (int i = 0; i < cnt; i++) { Ev[i] = Math.Clamp(Rng.Normal(m, sd), Math.Min(lo, hi), Math.Max(lo, hi)); sum += Ev[i]; }
                    for (int i = 0; i < cnt; i++) Ev[i] += (target - sum) / cnt;
                    if (P.Bug == EcologyBug.ExpandMeanField)
                    {
                        // expanding from a mean-field state: cohorts lost (mass still conserved: residual -> soil)
                        for (int i = 0; i < cnt; i++) Ev[i] = regMean;
                        double t2 = regMean * cnt;
                        W.N[r] += target - t2;
                        target = t2;
                    }
                    double need = 0; int nLow = 0;
                    for (int i = 0; i < cnt; i++) if (Ev[i] < 0.01) { need += 0.01 - Ev[i]; Ev[i] = 0.01; nLow++; }
                    if (nLow > 0 && cnt - nLow > 0)
                        for (int i = 0; i < cnt; i++) if (Ev[i] > 0.01) Ev[i] -= need / (cnt - nLow);
                    if (P.Bug == EcologyBug.ExpandLosePool) for (int i = 0; i < cnt; i++) Ev[i] -= 0.05 * target / cnt;
                    sum = 0; for (int i = 0; i < cnt; i++) sum += Ev[i];
                    double resid = target - sum;
                    if (P.Bug == EcologyBug.ExpandLosePool) resid = 0.0;
                    if (resid != 0) { W.N[r] += resid; Settle += Math.Abs(resid); }
                    for (int i = 0; i < cnt; i++)
                    {
                        SamplePos(r, s == 0, out double x, out double y, out double z);
                        if (s == 0 && _predPos.Count > 0)
                            for (int tries = 0; tries < 8 && NearPred(x, y, z); tries++) SamplePos(r, true, out x, out y, out z);
                        if (s == 1) _predPos.Add((x, y, z));
                        int rep = (r * 2 + s) * k + Rng.Int(k);
                        A.Add(x, y, z, s, el[i], Ev[i], RepX[rep], RepY[rep], RepZ[rep], 1.0, 0.0);
                    }
                    Expands += cnt;
                    for (int e = 0; e < 4; e++) pop.N[rc * 4 + e] = 0;
                    pop.S[rc] = 0; pop.Q[rc] = 0; pop.Lo[rc] = 0; pop.Hi[rc] = 0;
                }
            }
        }

        bool NearPred(double x, double y, double z)
        {
            double r2 = P.HFlee * P.HFlee;
            for (int i = 0; i < _predPos.Count; i++)
            {
                var (a, b, c) = _predPos[i];
                double dx = x - a, dy = y - b, dz = z - c;
                if (dx * dx + dy * dy + dz * dz < r2) return true;
            }
            return false;
        }

        // ── absorb ───────────────────────────────────────────────────────────────────────────────────────────────

        bool[] _rm = Array.Empty<bool>();
        readonly List<int> _absorbVox = new();

        void Absorb()
        {
            int n = A.Count;
            if (n == 0) return;
            if (_rm.Length < n) _rm = new bool[Math.Max(n, 2 * _rm.Length)];
            bool any = false;
            for (int i = 0; i < n; i++)
            {
                _rm[i] = false;
                int reg = W.RegionOfInside(A.Px[i], A.Py[i], A.Pz[i]);
                if (Hot[reg]) continue;
                A.Drawn(i, out double dx, out double dy, out double dz);
                if (VisibleForAbsorb(A.Px[i], A.Py[i], A.Pz[i]) || VisibleForAbsorb(dx, dy, dz)) continue;   // never absorb what is SEEN
                int s = A.Sp[i];
                if (s == 0)
                {
                    _absorbVox.Clear(); _absorbVox.Add(W.VoxelOf(A.Px[i], A.Py[i], A.Pz[i], reg));
                    M.AbsorbOccupancy(reg, _absorbVox.ToArray());
                }
                M.Pops[s].Place(reg, A.Elem[i], A.E[i] * (P.Bug == EcologyBug.AbsorbDropStomach ? 0.99 : 1.0));
                _rm[i] = true; any = true; Absorbs++;
            }
            if (any) A.Remove(_rm);
        }

        void DrainInbox()
        {
            ArrivedIds.Clear();
            int k = P.RepsPerRegion;
            for (int q = 0; q < M.Inbox.Count; q++)
            {
                var g = M.Inbox[q];
                int best = 0; double bd = double.MaxValue;
                for (int j = 0; j < k; j++)
                {
                    int i = (g.Src * 2 + g.Species) * k + j;
                    double dx = RepX[i] - W.Cx[g.Dst], dy = RepY[i] - W.Cy[g.Dst], dz = RepZ[i] - W.Cz[g.Dst], d = dx * dx + dy * dy + dz * dz;
                    if (d < bd) { bd = d; best = i; }
                }
                double hx = W.Cx[g.Dst] - RepX[best], hy = W.Cy[g.Dst] - RepY[best], hz = W.Cz[g.Dst] - RepZ[best];
                int row = A.Add(RepX[best], RepY[best], RepZ[best], g.Species, g.Elem, g.Stomach, RepX[best], RepY[best], RepZ[best],
                                1.0, 1.0, hx, hy, hz);
                ArrivedIds.Add(A.Id[row]);
                Arrives++;
            }
            M.Inbox.Clear();
        }

        // ── impostors ────────────────────────────────────────────────────────────────────────────────────────────

        void RetargetReps(bool all, double frac = 0.1)
        {
            int k = P.RepsPerRegion;
            int count = all ? W.NReg : Math.Max(1, (int)(frac * W.NReg));
            for (int q = 0; q < count; q++)
            {
                int r = all ? q : Rng.Int(W.NReg);
                for (int j = 0; j < 2 * k; j++)
                {
                    SamplePos(r, true, out double x, out double y, out double z);
                    int i = r * 2 * k + j;
                    TgtX[i] = x; TgtY[i] = y; TgtZ[i] = z;
                }
            }
        }

        void AdvectReps(double dt, double tau = 12.0)
        {
            RetargetReps(false);
            double a = Math.Min(1.0, dt / tau);
            for (int i = 0; i < RepX.Length; i++)
            {
                RepX[i] += (TgtX[i] - RepX[i]) * a; RepY[i] += (TgtY[i] - RepY[i]) * a; RepZ[i] += (TgtZ[i] - RepZ[i]) * a;
            }
        }

        // ── summaries ────────────────────────────────────────────────────────────────────────────────────────────

        public EcologySpeciesSummary Summary(int s)
        {
            var pop = M.Pops[s]; var sp = pop.Sp;
            long cnt = pop.Total(); double st = pop.StomachSum();
            Span<double> ph = stackalloc double[3];
            pop.PhaseCounts(null, ph);
            double q2 = 0; int C = P.Cohorts;
            for (int rc = 0; rc < W.NReg * C; rc++)
            {
                pop.MeanVar(rc, out long n, out double m, out double v);
                if (n > 0) q2 += n * (v + m * m);
            }
            for (int i = 0; i < A.Count; i++)
            {
                if (A.Sp[i] != s) continue;
                cnt++; st += A.E[i]; q2 += A.E[i] * A.E[i];
                ph[sp.PhaseOf(A.E[i])] += 1;
            }
            double mean = st / Math.Max(cnt, 1);
            return new EcologySpeciesSummary
            {
                Count = cnt, MeanE = mean, SdE = Math.Sqrt(Math.Max(q2 / Math.Max(cnt, 1) - mean * mean, 0.0)),
                Sated = ph[0] / Math.Max(cnt, 1), Forage = ph[1] / Math.Max(cnt, 1), Hungry = ph[2] / Math.Max(cnt, 1),
            };
        }

        public long Individuals() => M.H.Total() + M.Pr.Total() + A.Count + M.Inbox.Count;
    }
}
