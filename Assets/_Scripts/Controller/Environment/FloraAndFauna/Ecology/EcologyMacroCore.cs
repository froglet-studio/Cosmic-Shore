// Hierarchical ecology (round 11f, Docs/ECOLOGY_LOD.md): the MACRO level - per-region COHORT population dynamics, an
// Escalator Boxcar Train. Port of Tools/Ecology/hierarchy/macro.py (research branch cece/eco-hierarchy).
// Pure C# (no UnityEngine), struct-of-arrays over regions x cohort slots, one deterministic 1 Hz step: the shape a
// Burst job takes (Docs/ECOLOGY_LOD.md §3). Compiled and RUN headless by Tools/Build/ecology_lod_harness.
//
// State per species, per region r, per cohort slot c:
//     N[r,c,e]  long    individuals of element e (charge/mass/space/time)
//     S[r,c]    double  Σ stomach      (exact - this is what conserves mass)
//     Q[r,c]    double  Σ stomach²     (the cohort's spread: var = Q/N - (S/N)²)
//     Lo, Hi    double  the cohort's real min / max stomach (bounded cohorts: a Normal tail past the real max would
//                       breed individuals nobody is - births leaked ~90 s early in the enriched regime, measured)
// All three moments are ADDITIVE: merging cohorts, moving individuals, births and kills are plain additions, so
// conservation is exact by construction. Within a cohort the stomach is taken as Normal(mean, var): births are the
// part inside [EBirth, Hi], starvation the part inside [Lo, 0]. Every count moves by a Binomial / Poisson /
// hypergeometric / multinomial draw (EcologyRng).
using System;
using System.Collections.Generic;

namespace CosmicShore.Gameplay
{
    /// <summary>A migrant bound for a HOT region: it becomes an agent at the source region's representative.</summary>
    public struct EcologyMigrant
    {
        public int Species, Dst, Src, Elem;
        public double Stomach;
    }

    /// <summary>One species' cohorts over every region.</summary>
    public sealed class MacroCohorts
    {
        public const int NE = 4;
        public readonly EcologySpecies Sp;
        public readonly int NReg, C;
        public readonly long[] N;      // [(r*C + c)*NE + e]
        public readonly double[] S, Q, Lo, Hi;   // [r*C + c]
        readonly double _mergeTol;

        public MacroCohorts(EcologySpecies sp, int nreg, int cohorts, double mergeTol)
        {
            Sp = sp; NReg = nreg; C = cohorts;
            N = new long[nreg * cohorts * NE];
            S = new double[nreg * cohorts]; Q = new double[nreg * cohorts];
            Lo = new double[nreg * cohorts]; Hi = new double[nreg * cohorts];
            _mergeTol = mergeTol * sp.EBirth;
        }

        public long Count(int rc)
        {
            int b = rc * NE;
            return N[b] + N[b + 1] + N[b + 2] + N[b + 3];
        }

        public long RegionCount(int r)
        {
            long s = 0;
            for (int i = r * C * NE, e = i + C * NE; i < e; i++) s += N[i];
            return s;
        }

        public long Total()
        {
            long s = 0;
            for (int i = 0; i < N.Length; i++) s += N[i];
            return s;
        }

        public void MeanVar(int rc, out long n, out double m, out double v)
        {
            n = Count(rc);
            if (n <= 0) { m = 0; v = 0; return; }
            m = S[rc] / n;
            v = Math.Max(Q[rc] / n - m * m, 0.0);
        }

        /// <summary>Σ stomach + count x body: this species' share of the ledger.</summary>
        public double Mass()
        {
            double s = 0;
            for (int i = 0; i < S.Length; i++) s += S[i];
            return s + Total() * Sp.Body;
        }

        public double StomachSum()
        {
            double s = 0;
            for (int i = 0; i < S.Length; i++) s += S[i];
            return s;
        }

        /// <summary>
        /// Adds a cohort to region <paramref name="r"/>: merged into the live cohort whose mean is nearest when within
        /// MergeTol x EBirth, else into a free slot, else into the nearest (research MacroPop.insert). Exact in N, S, Q.
        /// </summary>
        public void Insert(int r, ReadOnlySpan<long> ne, double s, double q, double lo, double hi)
        {
            long k = ne[0] + ne[1] + ne[2] + ne[3];
            if (k <= 0) return;
            double mean = s / k;
            lo = Math.Min(lo, mean); hi = Math.Max(hi, mean);
            int best = -1, free = -1; double bd = double.PositiveInfinity;
            for (int c = 0; c < C; c++)
            {
                int rc = r * C + c;
                long n = Count(rc);
                if (n <= 0) { if (free < 0) free = c; continue; }
                double d = Math.Abs(S[rc] / n - mean);
                if (d < bd) { bd = d; best = c; }
            }
            int tgt = best >= 0 && bd <= _mergeTol ? best : free >= 0 ? free : best;
            int t = r * C + tgt;
            if (Count(t) <= 0) { Lo[t] = lo; Hi[t] = hi; }
            else { Lo[t] = Math.Min(Lo[t], lo); Hi[t] = Math.Max(Hi[t], hi); }
            for (int e = 0; e < NE; e++) N[t * NE + e] += ne[e];
            S[t] += s; Q[t] += q;
            Compact(r);
        }

        /// <summary>Individuals with stomach <paramref name="e"/> each, one zero-spread cohort (absorb, seeding).</summary>
        public void Place(int r, int elem, double e, long k = 1)
        {
            Span<long> ne = stackalloc long[NE];
            ne[elem] = k;
            Insert(r, ne, e * k, e * e * k, e, e);
        }

        /// <summary>Keep at least <paramref name="minFree"/> free slots in region r by merging the two closest cohorts.</summary>
        public void Compact(int r, int minFree = 2)
        {
            for (int it = 0; it < 6; it++)
            {
                int free = 0;
                for (int c = 0; c < C; c++) if (Count(r * C + c) <= 0) free++;
                if (free >= minFree) return;
                // closest pair of means (sorted-adjacent pair = research's argsort/diff)
                int a = -1, b = -1; double gap = double.PositiveInfinity;
                for (int c1 = 0; c1 < C; c1++)
                {
                    int rc1 = r * C + c1; long n1 = Count(rc1);
                    if (n1 <= 0) continue;
                    double m1 = S[rc1] / n1;
                    for (int c2 = c1 + 1; c2 < C; c2++)
                    {
                        int rc2 = r * C + c2; long n2 = Count(rc2);
                        if (n2 <= 0) continue;
                        double g = Math.Abs(S[rc2] / n2 - m1);
                        if (g < gap) { gap = g; a = c1; b = c2; }
                    }
                }
                if (a < 0) return;
                int ra = r * C + a, rb = r * C + b;
                for (int e = 0; e < NE; e++) { N[ra * NE + e] += N[rb * NE + e]; N[rb * NE + e] = 0; }
                S[ra] += S[rb]; Q[ra] += Q[rb];
                Lo[ra] = Math.Min(Lo[ra], Lo[rb]); Hi[ra] = Math.Max(Hi[ra], Hi[rb]);
                S[rb] = 0; Q[rb] = 0; Lo[rb] = 0; Hi[rb] = 0;
            }
        }

        /// <summary>Remove ke[4] individuals from cohort rc, each carrying stomach mean mu / second moment mu2.</summary>
        public void Take(int rc, ReadOnlySpan<long> ke, double mu, double mu2)
        {
            long k = 0;
            for (int e = 0; e < NE; e++) { N[rc * NE + e] -= ke[e]; k += ke[e]; }
            S[rc] -= k * mu; Q[rc] -= k * mu2;
        }

        /// <summary>An empty cohort must carry exactly nothing: its residual stomach (rounding) is returned (for the
        /// soil). Also keeps Q >= S²/n (a non-negative spread).</summary>
        public double TidyRegion(int r)
        {
            double resid = 0;
            for (int c = 0; c < C; c++)
            {
                int rc = r * C + c;
                long n = Count(rc);
                if (n <= 0) { resid += S[rc]; S[rc] = 0; Q[rc] = 0; Lo[rc] = 0; Hi[rc] = 0; continue; }
                Q[rc] = Math.Max(Q[rc], S[rc] * S[rc] / n);
            }
            return resid;
        }

        /// <summary>Expected (sated, forage, hungry) counts from each cohort's Normal over the selected regions.</summary>
        public void PhaseCounts(Func<int, bool> sel, Span<double> outPh)
        {
            outPh[0] = outPh[1] = outPh[2] = 0;
            double lo = Sp.PhaseLo * Sp.EBirth, hi = Sp.PhaseHi * Sp.EBirth;
            for (int r = 0; r < NReg; r++)
            {
                if (sel != null && !sel(r)) continue;
                for (int c = 0; c < C; c++)
                {
                    MeanVar(r * C + c, out long n, out double m, out double v);
                    if (n <= 0) continue;
                    double sd = Math.Sqrt(v) + 1e-6;
                    double fLo = EcologyRng.Ndtr((lo - m) / sd), fHi = 1 - EcologyRng.Ndtr((hi - m) / sd);
                    outPh[0] += n * fHi; outPh[1] += n * (1 - fLo - fHi); outPh[2] += n * fLo;
                }
            }
        }
    }

    /// <summary>The macro step over the COLD regions (research macro.Macro).</summary>
    public sealed class EcologyMacroCore
    {
        public const int NE = MacroCohorts.NE;
        public readonly EcologyWorld W;
        public readonly EcologyParams P;
        public readonly EcologyRng Rng;
        public readonly MacroCohorts[] Pops;   // [0] grazer, [1] predator
        public MacroCohorts H => Pops[0];
        public MacroCohorts Pr => Pops[1];
        /// <summary>Where a region's grazers stand over its voxels: a STATE relaxing toward food^OccTheta.</summary>
        public readonly double[] Occ;
        public readonly List<EcologyMigrant> Inbox = new();
        public long Kills, Hops;
        public readonly long[] Births = new long[2], Deaths = new long[2];
        public double Settled;
        /// <summary>Wall-clock ms of the last <see cref="Step"/> (the cost gate).</summary>
        public double LastStepMs;

        // scratch
        readonly double[] _occTarget;
        readonly long[] _vic, _ke;
        readonly double[] _w, _hc;

        public EcologyMacroCore(EcologyWorld world, EcologyParams p, EcologyRng rng)
        {
            W = world; P = p; Rng = rng;
            Pops = new[]
            {
                new MacroCohorts(p.Species[0], world.NReg, p.Cohorts, p.MergeTol),
                new MacroCohorts(p.Species[1], world.NReg, p.Cohorts, p.MergeTol),
            };
            Occ = new double[world.NReg * world.NVox];
            _occTarget = new double[world.NVox];
            for (int r = 0; r < world.NReg; r++)
                for (int v = 0; v < world.NVox; v++)
                    Occ[r * world.NVox + v] = world.VoxOk[r * world.NVox + v] / Math.Max(world.VoxOkSum[r], 1);
            _vic = new long[p.Cohorts * NE]; _ke = new long[NE];
            _w = new double[p.Cohorts]; _hc = new double[p.Cohorts];
        }

        public double Mass()
        {
            double m = H.Mass() + Pr.Mass();
            for (int i = 0; i < Inbox.Count; i++) m += Pops[Inbox[i].Species].Sp.Body + Inbox[i].Stomach;
            return m;
        }

        // ── occupancy ────────────────────────────────────────────────────────────────────────────────────────────

        void OccTarget(int r, Span<double> outW)
        {
            int nv = W.NVox; double s = 0;
            for (int v = 0; v < nv; v++)
            {
                int i = r * nv + v;
                double g = Math.Max(W.F[i] + W.K[i], 0.0);
                outW[v] = Math.Pow(g, P.OccTheta) * W.VoxOk[i];
                s += outW[v];
            }
            for (int v = 0; v < nv; v++)
                outW[v] = s > 1e-9 ? outW[v] / Math.Max(s, 1e-12) : W.VoxOk[r * nv + v] / Math.Max(W.VoxOkSum[r], 1);
        }

        void RelaxOccupancy(double dt)
        {
            double k = Math.Min(1.0, dt / Math.Max(P.OccTau, 1e-6));
            int nv = W.NVox;
            for (int r = 0; r < W.NReg; r++)
            {
                OccTarget(r, _occTarget);
                double s = 0;
                for (int v = 0; v < nv; v++)
                {
                    int i = r * nv + v;
                    Occ[i] = (Occ[i] + (_occTarget[v] - Occ[i]) * k) * W.VoxOk[i];
                    s += Occ[i];
                }
                s = Math.Max(s, 1e-12);
                for (int v = 0; v < nv; v++) Occ[r * nv + v] /= s;
            }
        }

        /// <summary>Fold absorbed grazers' voxels into the occupancy field (their positions are the best evidence).</summary>
        public void AbsorbOccupancy(int r, ReadOnlySpan<int> voxels)
        {
            if (voxels.Length == 0) return;
            int nv = W.NVox;
            double tot = voxels.Length, nH = H.RegionCount(r);
            double mix = tot / Math.Max(tot + nH, 1e-9);
            for (int v = 0; v < nv; v++) Occ[r * nv + v] *= 1 - mix;
            for (int q = 0; q < voxels.Length; q++) Occ[r * nv + voxels[q]] += mix / tot;
        }

        // ── one step ─────────────────────────────────────────────────────────────────────────────────────────────

        /// <summary>One macro step over the regions where <paramref name="hot"/> is false.</summary>
        public void Step(double dt, bool[] hot)
        {
            long t0 = System.Diagnostics.Stopwatch.GetTimestamp();
            RelaxOccupancy(dt);
            var beta = Graze(dt, hot);
            Predate(dt, hot);
            Energetics(0, beta, dt, hot);
            Energetics(1, null, dt, hot);
            Move(dt, hot);
            SettleEmpty();
            LastStepMs = (System.Diagnostics.Stopwatch.GetTimestamp() - t0) * 1000.0 / System.Diagnostics.Stopwatch.Frequency;
        }

        double[] _beta;

        /// <summary>Grazing: per-capita intake = sat(cohort mean) x f, f = the region's food per unit satiation, credited
        /// NOW (before predation removes anyone - negative 3: the 0.5 volume/step credit leak). Returns d(gain)/d(stomach).</summary>
        double[] Graze(double dt, bool[] hot)
        {
            var Hc = H; int C = P.Cohorts, nv = W.NVox;
            _beta ??= new double[W.NReg * C];
            Array.Clear(_beta, 0, _beta.Length);
            for (int r = 0; r < W.NReg; r++)
            {
                if (hot[r]) continue;
                double eff = 0;
                for (int c = 0; c < C; c++)
                {
                    Hc.MeanVar(r * C + c, out long n, out double m, out _);
                    if (n > 0) eff += n * Math.Clamp(1.0 - m / Hc.Sp.EMax, 0.0, 1.0);
                }
                if (eff <= 0) continue;
                double tot = 0;
                for (int v = 0; v < nv; v++)
                {
                    int i = r * nv + v;
                    double G = W.F[i] + W.K[i];
                    if (G <= 0) continue;
                    double want = eff * Occ[i] * P.HIntake * (G / (G + P.HHalf)) * dt;
                    if (P.Bug == EcologyBug.MacroGrazeX15) want *= 1.5;
                    double took = Math.Min(want, G), fracF = W.F[i] / Math.Max(G, 1e-12);
                    W.F[i] -= took * fracF; W.K[i] -= took * (1 - fracF);
                    tot += took;
                }
                double f = tot / eff / dt;
                for (int c = 0; c < C; c++)
                {
                    int rc = r * C + c;
                    Hc.MeanVar(rc, out long n, out double m, out _);
                    if (n <= 0) continue;
                    double sat = Math.Clamp(1.0 - m / Hc.Sp.EMax, 0.0, 1.0), gain = f * sat * dt;
                    Hc.Q[rc] += 2 * gain * Hc.S[rc] + n * gain * gain;
                    Hc.S[rc] += n * gain;
                    Hc.Lo[rc] += f * Math.Clamp(1 - Hc.Lo[rc] / Hc.Sp.EMax, 0, 1) * dt;
                    Hc.Hi[rc] += f * Math.Clamp(1 - Hc.Hi[rc] / Hc.Sp.EMax, 0, 1) * dt;
                    _beta[rc] = sat > 0 ? -f / Hc.Sp.EMax : 0.0;
                }
            }
            return _beta;
        }

        /// <summary>Predation: Holling II on the region's herbivore count, a = PAttack, h = PHandle (FITTED, calibrate.py:
        /// 149k predator-seconds, 548 kills), only while the predator's stomach is below PHuntBelow x EMax.</summary>
        void Predate(double dt, bool[] hot)
        {
            var Hc = H; var Pc = Pr; int C = P.Cohorts;
            double thr = P.PHuntBelow * Pc.Sp.EMax;
            double a = P.PAttack * (P.Bug == EcologyBug.MacroAttackX2 ? 2.0 : 1.0);
            Span<long> ks = stackalloc long[C];
            Span<double> w = _w, hc = _hc;
            for (int r = 0; r < W.NReg; r++)
            {
                if (hot[r]) continue;
                double hunters = 0;
                for (int c = 0; c < C; c++)
                {
                    Pc.MeanVar(r * C + c, out long n, out double m, out double v);
                    hc[c] = n > 0 ? n * EcologyRng.Ndtr((thr - m) / Math.Max(Math.Sqrt(v), 1e-6)) : 0.0;
                    hunters += hc[c];
                }
                long nH = Hc.RegionCount(r);
                if (hunters <= 0 || nH <= 0) continue;
                double rate = hunters * a * nH / (1.0 + a * P.PHandle * nH) * dt;
                long kk = Math.Min(Rng.Poisson(rate), nH);
                if (kk <= 0) continue;
                // victims: a multivariate hypergeometric draw over the region's (cohort, element) cells
                var cells = new ReadOnlySpan<long>(Hc.N, r * C * NE, C * NE);
                Rng.MultiHypergeometric(cells, kk, _vic);
                double meal = 0;
                for (int c = 0; c < C; c++)
                {
                    int rc = r * C + c;
                    long vc = 0;
                    for (int e = 0; e < NE; e++) vc += _vic[c * NE + e];
                    if (vc == 0) continue;
                    Hc.MeanVar(rc, out _, out double mh, out double vh);
                    meal += vc * (Hc.Sp.Body + mh);
                    Hc.Take(rc, new ReadOnlySpan<long>(_vic, c * NE, NE), mh, vh + mh * mh);
                }
                double wsum = 0;
                for (int c = 0; c < C; c++) { w[c] = hc[c]; wsum += w[c]; }
                if (wsum <= 0) for (int c = 0; c < C; c++) { w[c] = Pc.Count(r * C + c); wsum += w[c]; }
                Rng.Multinomial(kk, w, ks);
                long got = 0;
                for (int c = 0; c < C; c++) { ks[c] = Math.Min(ks[c], Pc.Count(r * C + c)); got += ks[c]; }
                double per = meal / Math.Max(got, 1);
                for (int c = 0; c < C; c++)
                {
                    if (ks[c] <= 0) continue;
                    int rc = r * C + c;
                    Pc.MeanVar(rc, out _, out double mc, out double vc2);
                    Rng.MultiHypergeometric(new ReadOnlySpan<long>(Pc.N, rc * NE, NE), ks[c], _ke);
                    double lo = Pc.Lo[rc] + per, hi = Pc.Hi[rc] + per;
                    Pc.Take(rc, _ke, mc, vc2 + mc * mc);
                    double nm = mc + per;
                    _pending.Add(new Pending { R = r, E0 = _ke[0], E1 = _ke[1], E2 = _ke[2], E3 = _ke[3],
                                               S = ks[c] * nm, Q = ks[c] * (vc2 + nm * nm), Lo = lo, Hi = hi });
                }
                Flush(Pc);   // after the cohort loop: an insert may merge slots the loop is still reading
                if (got == 0) W.N[r] += meal;
                Kills += kk;
            }
        }

        /// <summary>Metabolism (+ the measured sprint cost) to the soil, births from the cohort's [EBirth, Hi] tail and
        /// starvation from its [Lo, 0] tail (research macro._energetics, bounded cohorts).</summary>
        void Energetics(int k, double[] beta, double dt, bool[] hot)
        {
            var pop = Pops[k]; var sp = pop.Sp; int C = P.Cohorts, nv = W.NVox;
            double Vreg = P.L * P.L * P.L;
            double diff = k == 0 ? P.EDiffuseHerb : P.EDiffusePred;
            Span<long> ke = stackalloc long[NE];
            for (int r = 0; r < W.NReg; r++)
            {
                if (hot[r]) continue;
                double pSpr;
                if (k == 0)
                {
                    double other = Pr.RegionCount(r);
                    pSpr = 1.0 - Math.Exp(-other / Vreg * (4.0 / 3.0) * Math.PI * P.HFlee * P.HFlee * P.HFlee);
                }
                else
                {
                    double other = H.RegionCount(r);
                    pSpr = 1.0 - Math.Exp(-other / Vreg * (4.0 / 3.0) * Math.PI * P.PSense * P.PSense * P.PSense);
                }
                for (int c = 0; c < C; c++)
                {
                    int rc = r * C + c;
                    pop.MeanVar(rc, out long n, out double m, out double v);
                    if (n <= 0) continue;
                    double spr = k == 0
                        ? P.SprintKappaHerb * pSpr
                        : P.SprintKappaPred * pSpr * EcologyRng.Ndtr((P.PHuntBelow * sp.EMax - m) / Math.Max(Math.Sqrt(v), 1e-6));
                    double burnPc = (sp.Metab + sp.SprintMetab * spr) * dt, burn = n * burnPc;
                    W.N[r] += burn;                 // metabolism: stomach -> soil, exactly
                    pop.S[rc] -= burn;
                    pop.Lo[rc] -= burnPc; pop.Hi[rc] -= burnPc;
                    double m2 = pop.S[rc] / n;
                    double b = beta != null ? beta[rc] : 0.0;
                    double v2 = Math.Max(v * (1 + 2 * b * dt) + 2 * diff * dt, 0.0);
                    pop.Q[rc] = n * (v2 + m2 * m2);
                    double sd2 = Math.Sqrt(v2);

                    // births: the part of the cohort's Normal inside [EBirth, Hi], over the part inside [Lo, Hi]
                    double hiB = Math.Max(pop.Hi[rc], sp.EBirth);
                    EcologyTails.Interval(m2, sd2, sp.EBirth, hiB, out double pb, out double mub, out double mu2b);
                    EcologyTails.Interval(m2, sd2, Math.Min(pop.Lo[rc], m2), Math.Max(pop.Hi[rc], m2), out double whole, out _, out _);
                    pb = pop.Hi[rc] >= sp.EBirth ? pb / Math.Max(whole, 1e-12) : 0.0;
                    long nb = Rng.Binomial(n, Math.Clamp(pb, 0, 1));
                    if (nb > 0)
                    {
                        Rng.MultiHypergeometric(new ReadOnlySpan<long>(pop.N, rc * NE, NE), nb, ke);
                        pop.Take(rc, ke, mub, mu2b);
                        double pm = mub - sp.Body - sp.E0, pv = Math.Max(mu2b - mub * mub, 0.0);
                        double sNew = nb * pm + nb * sp.E0, qNew = nb * (pv + pm * pm) + nb * sp.E0 * sp.E0;
                        double hiOld = pop.Hi[rc];
                        pop.Hi[rc] = Math.Min(pop.Hi[rc], sp.EBirth);   // the survivors are below the threshold
                        double plo = sp.EBirth - sp.Body - sp.E0;
                        _pending.Add(new Pending { R = r, E0 = 2 * ke[0], E1 = 2 * ke[1], E2 = 2 * ke[2], E3 = 2 * ke[3],
                                                   S = sNew, Q = qNew, Lo = Math.Min(plo, sp.E0), Hi = Math.Max(hiOld - sp.Body - sp.E0, sp.E0) });
                        Births[k] += nb;
                    }
                }
                Flush(pop);
                // starvation: the part inside [Lo, 0]; body -> skeleton (spread over the occupancy), the tail's
                // (sub-zero) stomach stays with the survivors - exact in S
                for (int c = 0; c < C; c++)
                {
                    int rc = r * C + c;
                    pop.MeanVar(rc, out long n, out double m, out double v);
                    if (n <= 0 || pop.Lo[rc] > 0.0) continue;
                    double sd = Math.Sqrt(v);
                    EcologyTails.Interval(m, sd, Math.Min(pop.Lo[rc], 0.0), 0.0, out double pd, out _, out _);
                    EcologyTails.Interval(m, sd, Math.Min(pop.Lo[rc], m), Math.Max(pop.Hi[rc], m), out double whole, out _, out _);
                    pd /= Math.Max(whole, 1e-12);
                    long nd = Rng.Binomial(n, Math.Clamp(pd, 0, 1));
                    if (nd <= 0) continue;
                    Rng.MultiHypergeometric(new ReadOnlySpan<long>(pop.N, rc * NE, NE), nd, ke);
                    EcologyTails.Tail(m, sd, 0.0, true, out _, out double mus, out double mu2s);
                    for (int e = 0; e < NE; e++) pop.N[rc * NE + e] -= ke[e];
                    pop.Lo[rc] = Math.Max(pop.Lo[rc], 0.0);
                    long rest = pop.Count(rc);
                    double mm = rest > 0 ? pop.S[rc] / rest : 0.0, vv = Math.Max(mu2s - mus * mus, 0.0);
                    pop.Q[rc] = rest > 0 ? rest * (vv + mm * mm) : 0.0;
                    double body = nd * sp.Body;
                    for (int vx = 0; vx < nv; vx++) W.K[r * nv + vx] += body * Occ[r * nv + vx];
                    Deaths[k] += nd;
                }
            }
        }

        struct Pending { public int R; public long E0, E1, E2, E3; public double S, Q, Lo, Hi; }
        readonly List<Pending> _pending = new();

        void Flush(MacroCohorts pop)
        {
            Span<long> ne = stackalloc long[NE];
            for (int i = 0; i < _pending.Count; i++)
            {
                var q = _pending[i];
                ne[0] = q.E0; ne[1] = q.E1; ne[2] = q.E2; ne[3] = q.E3;
                pop.Insert(q.R, ne, q.S, q.Q, q.Lo, q.Hi);
            }
            _pending.Clear();
        }

        // per-move scratch
        struct Move1 { public int Src, Dst, Species; public long E0, E1, E2, E3; public double Mu, Mu2, Lo, Hi; }
        readonly List<Move1> _moves = new();

        /// <summary>Regional hops out of cold regions by phase (hop rates FITTED to micro diffusion). Into a cold region
        /// movers are a cohort (merging); into a hot one they go to the inbox as individuals whose stomachs are drawn
        /// from the cohort's Normal within [Lo, Hi] and shifted so their total is exact.</summary>
        void Move(double dt, bool[] hot)
        {
            int C = P.Cohorts, NR = W.NReg;
            var G = new double[NR]; var nH = new double[NR]; var nP = new double[NR];
            for (int r = 0; r < NR; r++) { G[r] = W.FloraIn(r); nH[r] = H.RegionCount(r); nP[r] = Pr.RegionCount(r); }
            Span<double> w = stackalloc double[6];
            Span<double> pv = stackalloc double[7];
            Span<long> mv = stackalloc long[7];
            Span<long> tk = stackalloc long[NE];
            _moves.Clear();
            for (int k = 0; k < 2; k++)
            {
                var pop = Pops[k];
                for (int r = 0; r < NR; r++)
                {
                    if (hot[r]) continue;
                    int nvalid = 0; double wsum = 0;
                    for (int d = 0; d < 6; d++)
                    {
                        int j = W.Nb[r * 6 + d];
                        if (j < 0) { w[d] = 0; continue; }
                        nvalid++;
                        w[d] = k == 0
                            ? Math.Pow((G[j] + 1.0) / (G[r] + 1.0), P.FoodBias) * Math.Pow((nP[r] + 1.0) / (nP[j] + 1.0), P.FleeBias)
                            : Math.Pow((nH[j] + 1.0) / (nH[r] + 1.0), P.PreyBias);
                        wsum += w[d];
                    }
                    if (nvalid == 0) continue;
                    double norm = Math.Max(wsum / nvalid, 1e-12);
                    for (int d = 0; d < 6; d++) w[d] /= norm;
                    for (int c = 0; c < C; c++)
                    {
                        int rc = r * C + c;
                        pop.MeanVar(rc, out long n, out double m, out double v);
                        if (n <= 0) continue;
                        double rate = pop.Sp.HopRate[pop.Sp.PhaseOf(m)], s = 0;
                        for (int d = 0; d < 6; d++) { pv[d] = Math.Max(w[d] * rate * dt, 0); s += pv[d]; }
                        if (s > 0.9) { for (int d = 0; d < 6; d++) pv[d] *= 0.9 / s; s = 0.9; }
                        pv[6] = 1 - s;
                        for (int e = 0; e < NE; e++)
                        {
                            long ne = pop.N[rc * NE + e];
                            if (ne <= 0) continue;
                            Rng.Multinomial(ne, pv, mv);
                            for (int d = 0; d < 6; d++)
                            {
                                if (mv[d] <= 0) continue;
                                var mo = new Move1 { Src = r, Dst = W.Nb[r * 6 + d], Species = k, Mu = m, Mu2 = v + m * m, Lo = pop.Lo[rc], Hi = pop.Hi[rc] };
                                switch (e) { case 0: mo.E0 = mv[d]; break; case 1: mo.E1 = mv[d]; break; case 2: mo.E2 = mv[d]; break; default: mo.E3 = mv[d]; break; }
                                _moves.Add(mo);
                                tk.Clear(); tk[e] = mv[d];
                                pop.Take(rc, tk, m, v + m * m);
                                Hops += mv[d];
                            }
                        }
                    }
                }
            }
            Span<long> ne4 = stackalloc long[NE];
            for (int q = 0; q < _moves.Count; q++)
            {
                var mo = _moves[q];
                var pop = Pops[mo.Species];
                ne4[0] = mo.E0; ne4[1] = mo.E1; ne4[2] = mo.E2; ne4[3] = mo.E3;
                long cnt = mo.E0 + mo.E1 + mo.E2 + mo.E3;
                if (!hot[mo.Dst]) { pop.Insert(mo.Dst, ne4, cnt * mo.Mu, cnt * mo.Mu2, mo.Lo, mo.Hi); continue; }
                double sd = Math.Sqrt(Math.Max(mo.Mu2 - mo.Mu * mo.Mu, 0.0));
                double lo = Math.Max(mo.Lo, 0.05), hi = Math.Min(mo.Hi, pop.Sp.EBirth - 0.05);
                int first = Inbox.Count; double sum = 0;
                for (int e = 0; e < NE; e++)
                    for (long i = 0; i < ne4[e]; i++)
                    {
                        double x = Math.Clamp(Rng.Normal(mo.Mu, sd), Math.Min(lo, hi), Math.Max(lo, hi));
                        Inbox.Add(new EcologyMigrant { Species = mo.Species, Dst = mo.Dst, Src = mo.Src, Elem = e, Stomach = x });
                        sum += x;
                    }
                double shift = (cnt * mo.Mu - sum) / cnt;
                for (int i = first; i < Inbox.Count; i++) { var g = Inbox[i]; g.Stomach += shift; Inbox[i] = g; }
            }
        }

        void SettleEmpty()
        {
            for (int k = 0; k < 2; k++)
                for (int r = 0; r < W.NReg; r++)
                {
                    double res = Pops[k].TidyRegion(r);
                    if (res != 0) { W.N[r] += res; Settled += Math.Abs(res); }
                }
        }
    }

    /// <summary>Moments of a Normal's tails and intervals (research macro.tail_moments / interval_moments).</summary>
    public static class EcologyTails
    {
        /// <summary>Fraction, mean and second moment of Normal(m, sd) beyond <paramref name="cut"/>.</summary>
        public static void Tail(double m, double sd, double cut, bool upper, out double p, out double mu, out double mu2)
        {
            sd = Math.Max(sd, 1e-6);
            double a = (cut - m) / sd, lam, var;
            if (upper)
            {
                p = 1.0 - EcologyRng.Ndtr(a);
                lam = EcologyRng.Phi(a) / Math.Max(p, 1e-300);
                mu = m + sd * lam; var = sd * sd * (1 + a * lam - lam * lam);
            }
            else
            {
                p = EcologyRng.Ndtr(a);
                lam = EcologyRng.Phi(a) / Math.Max(p, 1e-300);
                mu = m - sd * lam; var = sd * sd * (1 - a * lam - lam * lam);
            }
            var = Math.Max(var, 0.0);
            if (p < 1e-12 || double.IsNaN(mu) || double.IsInfinity(mu) || double.IsNaN(var) || double.IsInfinity(var)) { mu = cut; var = 0.0; }
            mu2 = mu * mu + var;
        }

        /// <summary>Mass (fraction of the WHOLE Normal), mean and second moment of Normal(m, sd) restricted to [a, b].</summary>
        public static void Interval(double m, double sd, double a, double b, out double z, out double mu, out double mu2)
        {
            sd = Math.Max(sd, 1e-6);
            double al = (a - m) / sd, be = (b - m) / sd;
            z = EcologyRng.Ndtr(be) - EcologyRng.Ndtr(al);
            double pa = double.IsInfinity(al) ? 0 : EcologyRng.Phi(al), pb = double.IsInfinity(be) ? 0 : EcologyRng.Phi(be);
            double ala = double.IsInfinity(al) ? 0 : al, beb = double.IsInfinity(be) ? 0 : be;
            double zs = Math.Max(z, 1e-300);
            mu = m + sd * (pa - pb) / zs;
            double var = sd * sd * (1 + (ala * pa - beb * pb) / zs - ((pa - pb) / zs) * ((pa - pb) / zs));
            bool tiny = z < 1e-12;
            if (tiny || double.IsNaN(mu) || double.IsInfinity(mu)) mu = Math.Clamp(m, Math.Min(a, b), Math.Max(a, b));
            else mu = Math.Clamp(mu, Math.Min(a, b), Math.Max(a, b));
            if (tiny || double.IsNaN(var) || double.IsInfinity(var)) var = 0.0;
            var = Math.Max(var, 0.0);
            if (double.IsNaN(z) || double.IsInfinity(z)) z = 0.0;
            mu2 = mu * mu + var;
        }
    }
}
